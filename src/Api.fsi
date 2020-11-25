namespace CloudLayerIo

open System

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
      Source: Source }

[<RequireQualifiedAccess>]
module CloudLayerApi =
    /// Check if the service is online.
    /// Returns true if online.
    val isOnline: connection:Connection -> Async<bool>

    /// Returns the account operational status and limits
    val accountStatus: connection:Connection -> Async<Result<ApiStatus, FailureReason>>

    /// Creates an image with the specified options and returns a stream containing the image
    val fetchImageWith:
        options:ImageOptions -> connection:Connection -> Async<Result<(System.IO.Stream * ApiStatus), FailureReason>>

    /// Creates an image with the default options and returns a stream containing the image
    val fetchImage:
        source:Source -> connection:Connection -> Async<Result<(System.IO.Stream * ApiStatus), FailureReason>>

    /// Creates a pdf with the specified options and returns a stream containing the pdf file
    val fetchPdfWith:
        options:PdfOptions -> connection:Connection -> Async<Result<(System.IO.Stream * ApiStatus), FailureReason>>

    /// Creates a pdf with the default options and returns a stream containing the pdf file
    val fetchPdf: source:Source -> connection:Connection -> Async<Result<(System.IO.Stream * ApiStatus), FailureReason>>

    /// If generation was successful, saves a generated file to the specified path
    val saveToFile: path:string -> result:Async<Result<(System.IO.Stream * 'a), 'b>> -> Async<Result<'a, 'b>>

    /// Reads the entire response into a byte-array
    val toByteArray: result:Async<Result<(System.IO.Stream * 'a), 'b>> -> Async<Result<byte [], 'b>>
