# CloudLayer for F#
This is the [CloudLayer](https://cloudlayer.io) API for easy access to our REST based API services using F#.

To read about how to get started using CloudLayer, see [documentation here](https://cloudlayer.io/docs/getstarted).

# Installation

You can reference it directly from Nuget or Paket.

```powershell
PS> Install-Package CloudLayer.FSharp
```

The assembly targets `NetStandard 2.0`.

# Usage

To begin, create an API key from the [dashboard](https://cloudlayer.io/dashboard/account/api).



## Basics

All API calls take in a `Connection`:

```fsharp
let connection = 
	{ Connection.Defaults with ApiKey = "ca-644907a519df4f84b0db24b822b37c5e" }
```

If you are using this from an Asp.Net Core app, you can specify a `IHttpClientFactory` to be used (this is usually available through Dependency Injection),

```fsharp
let connection' = { connection with ClientFactory = factory }
```

`IHttpClientFactory` avoids socket exhaustion problems and maintains a pool of `HttpClient` instances for reuse. See [this article](https://docs.microsoft.com/en-us/dotnet/architecture/microservices/implement-resilient-applications/use-httpclientfactory-to-implement-resilient-http-requests) for more.

API calls have, as the last argument a `Connection`, and they take the shape:

```fsharp
connection |> CloudLayerApi.apiCall : Async<Result<ReturnValue, FailureReason>>
```

All API calls return the `Result` type, and they follow the [railway-oriented approach](https://fsharpforfunandprofit.com/posts/recipe-part2/). 

## Account Status

You can check the status of your account with 

```fsharp
let status =
    connection |> CloudLayerApi.accountStatus |> Async.RunSynchronously
```

The results can be pattern matched.

```fsharp
match status with
| Ok status ->
    $"{status.Remaining} of {status.Limit} remaining. " +
    $"Limit resets at {status.ResetsAt.LocalDateTime}"
| Error err ->
    match err with
    | FailureReason.InvalidApiKey -> "Check your api key"
    | FailureReason.InsufficientCredit -> "Buy more credit pls"
    | FailureReason.SubscriptionInactive -> "Please activate your account"
    | FailureReason.Unauthorized -> "Please check your credentials or proxy"
    | other -> $"There was an error: {other}"
|> printfn "%s"
```

## Creating Images

CloudLayer can create images of public URLs:

```fsharp
let image = 
    connection |> CloudLayerApi.fetchImage (Url "https://google.com")
```

and raw html:

```fsharp
let image = 
    connection |> CloudLayerApi.fetchImage (Html "<h1>Hello World!</h1>")
```

and returns either a `System.IO.Stream` or a `FailureReason`.

```fsharp
match image with
| Ok (stream, status) ->
    //do something with stream
| Error err -> 
    failwithf "Something went wrong: %A" err
```

You can save the result to a file with `saveToFile`, or read directly to memory with `toByteArray`.

```fsharp
connection 
|> CloudLayerApi.fetchImage (Url "https://google.com") 
|> CloudLayerApi.saveToFile "google.jpg"
|> Async.RunSynchronously
```

![HtmlImage](https://raw.githubusercontent.com/cloudlayerio/cloudlayerio-fsharp/main/tests/google.jpg)

To use more configuration options, use `fetchImageWith`. Options are specified by the `ImageOptions` record.

```fsharp

connection 
|> CloudLayerApi.fetchImageWith 
    { ImageOptions.Defaults with 
        Source = Url "https://www.openstreetmap.org#map=13/-6.1918/71.2976" 
        Timeout = TimeSpan.FromSeconds 60. 
        Inline = false }
|> CloudLayerApi.saveToFile "eagle-island.jpg"
|> Async.RunSynchronously
```

## Creating PDFs

Creating PDFs is similar to the API for creating images.

```fsharp
connection |> CloudLayerApi.fetchPdf (Url "https://en.wikipedia.org/wiki/Marine_snow")
connection |> CloudLayerApi.fetchPdf (Html "<h1>Hello from PDF!</h1>")
```

For more options, use `fetchPdfWith`. Options are specified by the `PdfOptions` record.

```fsharp
connection 
|> CloudLayerApi.fetchPdfWith 
    { PdfOptions.Defaults with
        Source = (Url "https://en.wikipedia.org/wiki/Marine_snow") 
        PrintBackground = false
        Format = "A4" }
|> CloudLayerApi.saveToFile "snow.pdf"
|> Async.RunSynchronously

```



### Note

This library is specifically for F#, if you are using C# you should [use our C# library](github.com/cloudlayerio/cloudlayerio-csharp). We did this because we wanted to give F# developers first class support instead of wrapping a C# library.