namespace CloudLayerIo

open System
open System.IO
open CloudLayerIo.Async
open CloudLayerIo.Http
    

type DomSelector =
    { Selector: string
      Options: {| Visible: bool
                  Hidden: bool
                  Timeout: TimeSpan |} option 
    }

type ImageOptions =
    { Delay: TimeSpan
      Timeout: TimeSpan
      Filename: string option
      Inline: bool 
      WaitForSelector: DomSelector option
      Source: Source }
    static member Defaults = 
        { Delay = TimeSpan.Zero
          Timeout = TimeSpan.FromSeconds 30.
          Inline = false
          WaitForSelector = None
          Filename = None
          Source = Html "<h1>Hello World</h1>" }

type PdfOptions =
     { Format: string
       Margin: {| left: string
                  top: string
                  right: string
                  bottom: string |} option
       PrintBackground: bool
       Timeout: TimeSpan
       Delay: TimeSpan
       Filename: string option
       Inline: bool
       WaitForSelector: DomSelector option
       Source: Source
       }
     static member Defaults =
         { Format = "A4"
           Margin = None
           PrintBackground = true
           Delay = TimeSpan.Zero
           Timeout = TimeSpan.FromSeconds 30.
           Filename = None
           Inline = false
           WaitForSelector = None
           Source = Html "<h1>Hello World</h1>" }

[<RequireQualifiedAccess>]
module CloudLayerApi =   
    

    let isOnline connection =                 
        connection |> fetch "/oapi" None |> map (fun res -> res.IsSuccessStatusCode)    
 
    
    let accountStatus connection = 
        connection |> fetch "/v1/getStatus" None |> bind tryParseResponse    
    
    let fetchImageWith (options : ImageOptions) connection =
        let uri = 
            match options.Source with
            | Url _ -> "/v1/url/image"
            | Html _ -> "/v1/html/image"
        
        connection |> fetch uri (Some options) |> bind tryReadStream
    
    let fetchImage source connection =
        connection |> fetchImageWith { ImageOptions.Defaults with Source = source }
    
    let fetchPdfWith (options : PdfOptions) connection =
        let uri = 
           match options.Source with
           | Url _ -> "/v1/url/pdf"
           | Html _ -> "/v1/html/pdf"

        connection |> fetch uri (Some options) |> bind tryReadStream

    
    let fetchPdf source connection =
        connection |> fetchPdfWith { PdfOptions.Defaults with Source = source }

    let saveToFile (path: string) result =
        result
        |> Result.mapAsync(fun (stream : Stream, resp) -> 
            async { 
                let! token = Async.CancellationToken
                use file = File.OpenWrite(path)
                do! stream.CopyToAsync(file, 1024, token) |> Async.AwaitTask
                return resp
        })
            
    let toByteArray result =
        result
        |> Result.mapAsync(fun (stream : Stream, _) -> 
            async { 
                let! token = Async.CancellationToken
                use memstream = new MemoryStream()
                do! stream.CopyToAsync(memstream, 1024, token) |> Async.AwaitTask
                return memstream.ToArray()
        })
