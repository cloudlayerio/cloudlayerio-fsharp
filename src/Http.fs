namespace CloudLayerIo

open System
open System.Net
open System.Net.Http
open System.Net.Http.Json
open System.Text
open System.Text.Json
open System.Text.Json.Serialization

type ApiStatus =
    { Limit: int
      Remaining: int
      ResetsAt: DateTimeOffset }

[<RequireQualifiedAccess>]
type FailureReason =
    | InvalidApiKey
    | InsufficientCredit
    | SubscriptionInactive
    | InvalidRequest
    | Unauthorized
    | ServerError
    | ErrorCode of statusCode: HttpStatusCode * content: string
    | Faulted of Exception

type CloudLayerResponse =
    { mutable reason: string
      mutable allowed: bool }

type Source = 
    | Url of string
    | Html of string

module Service = 
        
    let [<Literal>] Uriv1 =
#if DEV
      "https://dev-api.cloudlayer.io/oapi"
#else
      "https://api.cloudlayer.io"
#endif

    let [<Literal>] ClientName = "cloudlayerio"
    let [<Literal>] UserAgent = "cloudlayerio-fsharp"
    let [<Literal>] ContentType = "application/json"

    let BaseUri = Uri Uriv1  

    let internal ClientFactory = {
        new IHttpClientFactory with
        member _.CreateClient(_name) = new HttpClient()
    }

type Connection =
    { ClientFactory: IHttpClientFactory
      ApiKey: string }
    static member Defaults =
        { ClientFactory = Service.ClientFactory
          ApiKey = Environment.GetEnvironmentVariable("CLOUDLAYER_API_KEY") }

module internal Async =  

    let bind fn asyncInstance = 
        async.Bind(asyncInstance, fn)
    
    let map fn asyncInstance = async {
        let! value = asyncInstance
        return fn value
    }

module internal Result =
    let mapAsync mapping asyncResult = 
        asyncResult 
        |> Async.bind(function 
            | Ok value ->
                mapping value |> Async.map Ok
            | Error err ->
                async.Return(Error err)
        )

module internal Http =
    
    open Service
    open Async

    type 'a Verb =
    | Head
    | Get
    | Post of data: 'a
    
    
    let parseRateLimit (response: HttpResponseMessage) =        
        let getValue key = 
            response.Headers.GetValues key
            |> Seq.map Int64.TryParse
            |> Seq.tryHead
            |> Option.bind(function true, v -> Some v | _ -> None)
            |> Option.defaultValue 0L
        
        {
            Limit = getValue "X-RateLimit-Limit" |> int
            Remaining = getValue "X-RateLimit-Remaining" |> int
            ResetsAt = getValue "X-RateLimit-Reset" |> DateTimeOffset.FromUnixTimeSeconds
        }
        
    let parseResponse (resp: HttpResponseMessage) = async {
        let! token = Async.CancellationToken
        let! response =             
            if resp.Content.Headers.ContentType.MediaType = ContentType then
                resp.Content.ReadFromJsonAsync<CloudLayerResponse>(cancellationToken = token) 
                |> Async.AwaitTask 
                |> map Some
            else
                async.Return None
        
        return
            match resp.StatusCode with
            | HttpStatusCode.OK 
            | HttpStatusCode.Created ->                        
                Ok (parseRateLimit resp)
            | HttpStatusCode.Unauthorized ->
                match response with
                | Some res ->                     
                    match res.reason with
                    | "Invalid API key." -> FailureReason.InvalidApiKey
                    | "Insufficient credit." -> FailureReason.InsufficientCredit
                    | "Subscription inactive." -> FailureReason.SubscriptionInactive
                    | _ -> FailureReason.Unauthorized
                | None -> FailureReason.Unauthorized
                |> Error
            | HttpStatusCode.BadRequest -> 
                Error FailureReason.InvalidRequest
            | HttpStatusCode.InternalServerError ->
                Error FailureReason.ServerError
            | other ->
                let content = resp.Content.ReadAsStringAsync()
                do content.Wait(token)
                Error (FailureReason.ErrorCode (other, content.Result))
    }

    let tryParseResponse response = async {
        try
            return! parseResponse response
        with ex ->
            return Error (FailureReason.Faulted ex)
    }

    let tryReadStream response = 
        response 
        |> tryParseResponse
        |> Result.mapAsync(fun status -> 
                response.Content.ReadAsStreamAsync() 
                |> Async.AwaitTask
                |> map (fun stream -> stream, status)
        )

    let notSupported () = raise (NotSupportedException())

    type OptionValueConverter<'T>() =
        inherit JsonConverter<'T option>()    
    
        override _.Write (writer, value: 'T option, options: JsonSerializerOptions) =
            match value with
            | Some value -> JsonSerializer.Serialize(writer, value, options)
            | None -> writer.WriteNullValue ()
        
        override _.Read (_r, _t, _o) =
            notSupported ()

    let serializerOptions = 
        let opts = JsonSerializerOptions(PropertyNamingPolicy = JsonNamingPolicy.CamelCase)        

        opts.Converters.Add <| {
            new Serialization.JsonConverter<TimeSpan>() with 
                override _.Write(writer, timespan, _options) =
                    writer.WriteNumberValue(int timespan.TotalMilliseconds)                        
                override _.Read(_r, _t, _opts) =
                    notSupported ()
        }

        opts.Converters.Add <| {
            new Serialization.JsonConverter<Source>() with 
                override _.Write(writer, source, _options) =
                    match source with
                    | Url url ->                        
                        writer.WriteStringValue("url")
                        writer.WriteString("url", url)
                    | Html html ->
                        writer.WriteStringValue("html")
                        writer.WriteString("html", html |> Encoding.UTF8.GetBytes |> Convert.ToBase64String)

                override _.Read(r, t, opts) =
                    notSupported ()
        }

        opts.Converters.Add <| { new Serialization.JsonConverterFactory() with
                override _.CanConvert(t: Type) : bool =
                    t.IsGenericType &&
                    t.GetGenericTypeDefinition() = typedefof<Option<_>>
        
                override _.CreateConverter(t: Type, _options) : JsonConverter =
                    let typ = t.GetGenericArguments() |> Array.head
                    let converterType = typedefof<OptionValueConverter<_>>.MakeGenericType(typ)
                    Activator.CreateInstance(converterType) :?> JsonConverter        
        }

        opts

    let fetch (content: 'content Verb) (path: string) (connection: Connection) = async {
        let! token = Async.CancellationToken
        let client = connection.ClientFactory.CreateClient(ClientName)
        client.BaseAddress <- Service.BaseUri
        client.DefaultRequestHeaders.Add ("X-API-Key", connection.ApiKey)
        client.DefaultRequestHeaders.Add ("User-Agent", UserAgent)
        
        return! 
            match content with
            | Post obj -> 
            #if DEBUG
                let json = JsonSerializer.Serialize(obj, serializerOptions)
                do System.Diagnostics.Debug.Print $"POST:{path}\n{json}"
            #endif
                client.PostAsJsonAsync(path, obj, serializerOptions, token) 
            | Get -> 
            #if DEBUG
                do System.Diagnostics.Debug.Print $"GET:{path}"
            #endif
                client.GetAsync(path, token)
            | Head ->
                client.GetAsync(path, HttpCompletionOption.ResponseHeadersRead, token)
            |> Async.AwaitTask
    }

