module CloudLayer.Testing.ApiSpecs

open CloudLayerIo

open NUnit.Framework
open FsCheck

[<Test>]
let ``Service is online`` () =    
    let conn = CloudLayerApi.Connection.defaults
    Assert.That(conn |> CloudLayerApi.isOnline |> Async.RunSynchronously)

