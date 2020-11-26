module CloudLayer.Testing.ApiSpecs

open System
open NUnit.Framework
open CloudLayerIo
open System.Security.Cryptography
open System.IO

let conn = Connection.Defaults
let badconn = { conn with ApiKey = "unauthorized-key" }

let isOk = function | Ok _ -> true | Error _ -> false
let run async' = Async.RunSynchronously(async', 5000000)

[<Test>]
let ``API key exists`` () =    
    Assert.That(not (String.IsNullOrWhiteSpace conn.ApiKey))

[<Test>]
let ``Service is online`` () =    
    Assert.That(conn |> CloudLayerApi.isOnline |> run)

[<Test>]
let ``Account is active`` () =        
    Assert.That(conn |> CloudLayerApi.accountStatus |> run |> isOk)

[<Test>]
let ``Has valid rate-limits`` () =
    let result = conn |> CloudLayerApi.accountStatus |> run
    match result with
    | Ok limit -> 
        Assert.That(limit.Limit > 0)
        Assert.That(limit.Remaining > 0)
        Assert.That(limit.ResetsAt > DateTimeOffset.UnixEpoch)
    | Error err ->
        Assert.Fail(string err)

[<Test>]
let ``Fails on bad API key`` () =
    let response = badconn |> CloudLayerApi.accountStatus |> run
    let isInvalid = response = Error FailureReason.InvalidApiKey    
    Assert.That(isInvalid)

let referenceUrl = Url "http://acid2.acidtests.org/reference.html"
let referenceHtml = Html "<h1>Hello World!</h1>"

let fileContents fileName = 
    File.ReadAllBytes(fileName) |> Ok
   
let isSame actual reference =
    match actual, reference with
    | Ok a, Ok b -> 
        let headerLength = 10
        
        // same header
        let headerMatches = 
            (a |> Array.take headerLength) = (b |> Array.take headerLength)
        let (la,lb) = Array.length a, Array.length b
        
        // file sizes match to within 1%
        let similarSize = (abs la - lb) <= (lb / 100)
        
        headerMatches && similarSize
    | _ -> false

[<Test>]
let ``Captures an image from a url`` () =   
    let res = 
        conn 
        |> CloudLayerApi.fetchImage referenceUrl
        |> CloudLayerApi.toByteArray
        |> Async.RunSynchronously

    let imgRef = fileContents "url-reference.jpg"

    Assert.That(isSame res imgRef)

[<Test>]
let ``Captures an image from html`` () =   
    let res = 
        conn 
        |> CloudLayerApi.fetchImage referenceHtml
        |> CloudLayerApi.toByteArray
        |> Async.RunSynchronously

    let imgRef = fileContents "html-reference.jpg"
    
    Assert.That(isSame res imgRef)

[<Test>]
let ``Captures a pdf from a url`` () =   
    let res = 
        conn 
        |> CloudLayerApi.fetchPdf referenceUrl
        |> CloudLayerApi.toByteArray
        |> Async.RunSynchronously

    let pdfRef = fileContents "url-reference.pdf"
    
    Assert.That(isSame res pdfRef)

[<Test>]
let ``Captures a pdf from html`` () =   
    let res = 
        conn 
        |> CloudLayerApi.fetchPdf referenceHtml
        |> CloudLayerApi.toByteArray
        |> Async.RunSynchronously

    let pdfRef = fileContents "html-reference.pdf"

    Assert.That(isSame res pdfRef)
