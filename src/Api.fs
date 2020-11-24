namespace CloudLayerIo

open System
open System.IO
open CloudLayerIo.Async
open CloudLayerIo.Http
    
type ImageOptions =
    { Delay: TimeSpan
      Timeout: TimeSpan
      Filename: string
      Inline: bool 
      Source: Source }
    static member Defaults = 
        { Delay = TimeSpan.Zero
          Timeout = TimeSpan.FromSeconds 30.
          Inline = false
          Filename = "image.jpeg"
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
       Filename: string
       Inline: bool
       Source: Source
       }
     static member Defaults =
         { Format = "A4"
           Margin = None
           PrintBackground = true
           Delay = TimeSpan.Zero
           Timeout = TimeSpan.FromSeconds 30.
           Filename = "file.pdf"
           Inline = false
           Source = Html "<h1>Hello World</h1>" }

[<RequireQualifiedAccess>]
module CloudLayerApi =   
    
    /// Check if the service is online.
    /// Returns true if online.
    let isOnline connection =                 
        connection |> fetch "/oapi" None |> map (fun res -> res.IsSuccessStatusCode)    
 
    /// Returns the account operational status and limits
    let accountStatus connection = 
        connection |> fetch "/v1/getStatus" None |> bind tryParseResponse
    
    /// Creates an image with the specified options and returns a stream containing the image
    let fetchImageWith (options : ImageOptions) connection =
        let uri = 
            match options.Source with
            | Url _ -> "/v1/url/image"
            | Html _ -> "/v1/html/image"
        
        connection |> fetch uri (Some options) |> bind tryReadStream

    /// Creates an image with the default options and returns a stream containing the image
    let fetchImage (source : Source) connection =
        connection |> fetchImageWith { ImageOptions.Defaults with Source = source }

    /// Creates a pdf with the specified options and returns a stream containing the pdf file
    let fetchPdfWith (options : PdfOptions) connection =
        let uri = 
           match options.Source with
           | Url _ -> "/v1/url/pdf"
           | Html _ -> "/v1/html/pdf"

        connection |> fetch uri (Some options) |> bind tryReadStream

    /// Creates a pdf with the default options and returns a stream containing the pdf file
    let fetchPdf (source : Source) connection =
        connection |> fetchPdfWith { PdfOptions.Defaults with Source = source }

    /// If generation was successful, saves a generated file to the specified path
    let saveToFile (path: string) result =
        result
        |> Result.mapAsync(fun (stream : Stream, resp) -> 
            async { 
                let! token = Async.CancellationToken
                use file = File.OpenWrite(path)
                do! stream.CopyToAsync(file, 1024, token) |> Async.AwaitTask
                return resp
        })
            
    /// Reads the entire response into a byte-array
    let toByteArray result =
        result
        |> Result.mapAsync(fun (stream : Stream, _) -> 
            async { 
                let! token = Async.CancellationToken
                use memstream = new MemoryStream()
                do! stream.CopyToAsync(memstream, 1024, token) |> Async.AwaitTask
                return memstream.ToArray()
        })
