#r "nuget: System.Net.Http"
#r "nuget: System.Text.Json"
#r "nuget: System.Net.Http.Json"
#r "nuget: Microsoft.Extensions.Http"
#r @"..\src\bin\Debug\netstandard2.0\CloudLayer.FSharp.dll"  

open System
open CloudLayerIo

let connection = { Connection.Defaults with ApiKey = "ca-666907a519df4f84b0db24b822b37c5e" }

let online = 
    connection |> CloudLayerApi.isOnline |> Async.RunSynchronously

let status =
    connection |> CloudLayerApi.accountStatus |> Async.RunSynchronously

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

let image = 
    connection |> CloudLayerApi.fetchImage (Url "https://google.com") |> Async.RunSynchronously

match image with
| Ok (stream, status) ->
    //do something with stream
    ()
| Error err -> 
    failwithf "Something went wrong: %A" err

connection 
|> CloudLayerApi.fetchImage (Url "https://google.com") 
|> CloudLayerApi.saveToFile "google.jpg"
|> Async.RunSynchronously


connection 
|> CloudLayerApi.fetchImageWith 
    { ImageOptions.Defaults with 
        Source = Url "https://www.openstreetmap.org#map=13/-6.1918/71.2976" 
        Timeout = TimeSpan.FromSeconds 60. 
        Inline = false }
|> CloudLayerApi.saveToFile "eagle-island.jpg"
|> Async.RunSynchronously

connection 
|> CloudLayerApi.fetchPdfWith 
    { PdfOptions.Defaults with
        Source = (Url "https://en.wikipedia.org/wiki/Marine_snow") 
        PrintBackground = false
        Format = "A4" }
|> CloudLayerApi.saveToFile "snow.pdf"
|> Async.RunSynchronously

