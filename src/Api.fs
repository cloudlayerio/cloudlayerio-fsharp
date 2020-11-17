module CloudLayerIo

open System
open System.Net.Http

module internal HttpClientFactory =
    type Factory() =
        interface IHttpClientFactory with
            member _.CreateClient(name) = new HttpClient()

    let instance = Factory()

[<RequireQualifiedAccess>]
module CloudLayerApi =

    [<Literal>]
    let Uriv1 =
#if DEBUG
        "https://dev-api.cloudlayer.io/oapi"
#else
        "https://api.cloudlayer.io/oapi"
#endif
    [<Literal>]
    let ClientName = "cloudlayerio"

    type Connection =
        { ClientFactory: IHttpClientFactory
          ApiKey: string }
        static member defaults =
            { ClientFactory = HttpClientFactory.instance
              ApiKey = Environment.GetEnvironmentVariable("CLOUDLAYER_API_KEY") }

    let internal request (uri: string) (connection: Connection) =
        async {
            let! ct = Async.CancellationToken
            let client = connection.ClientFactory.CreateClient(ClientName)            
            let uri = sprintf "%s%s" Uriv1 uri
            use! request = client.GetAsync(uri) |> Async.AwaitTask            
            return request
        }


    let isOnline connection =
        async {
            let! req = connection |> request "/"
            return req.IsSuccessStatusCode
        }
        
        
