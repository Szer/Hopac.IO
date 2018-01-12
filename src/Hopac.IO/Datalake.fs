namespace Hopac.IO

module Datalake = 

    open System.Net
    open System.IO
    open System
    open Stream
    open Hopac

    let internal baseUriTemplate = sprintf "https://%s.azuredatalakestore.net/webhdfs/v1"

    type SyncFlag = 
        | Data
        | Metadata
        | Close
        member self.AsString () =
            match self with
            | Data     -> "DATA"
            | Metadata -> "METADATA"
            | Close    -> "CLOSE"

    type ContentType = 
        | StringContent of string
        | StreamContent of Stream
    type StorageName  = StorageName  of string
    type DataLakePath = DataLakePath of string
    type AccessToken  = AccessToken  of string

    type UploadParamOptional = 
        { SyncFlag    : SyncFlag option
          LeaseId     : Guid     option
          Overwrite   : bool     option
          Permission  : string   option
          ApiVersion  : string   option }
        static member Default = 
            { SyncFlag    = None
              LeaseId     = None
              Overwrite   = None
              Permission  = None
              ApiVersion  = None }

    let internal getQueryString uploadParams = 
        seq {
            if uploadParams.SyncFlag.IsSome then
                yield (sprintf "syncFlag=%s" 
                <| uploadParams.SyncFlag.Value.AsString())

            if uploadParams.LeaseId.IsSome then
                yield (sprintf "leaseId=%s" 
                <| uploadParams.LeaseId.Value.ToString())

            if uploadParams.Overwrite.IsSome then
                yield (sprintf "overwrite=%s" 
                <| uploadParams.Overwrite.Value.ToString().ToLowerInvariant())

            if uploadParams.Permission.IsSome then
                yield (sprintf "permission=%s" 
                <| uploadParams.Permission.Value.ToString())

            if uploadParams.ApiVersion.IsSome then
                yield (sprintf "api-version=%s" 
                <| uploadParams.ApiVersion.Value.ToString())
            yield "write=true"
            yield "op=CREATE"
        } |> String.concat "&"
    
    ///**Description**
    ///Uploads stream to Azure DataLake
    ///**Parameters**
    ///  * `setOptUploadParams` - function to setup optional upload parameters. 
    ///Example ```fun p -> { p with Overwrite = Some true``` }
    ///for the most use cases `id` function will be enough
    ///  * `StorageName` - storage account name 
    ///  * `DataLakePath` - path inside DataLake where to upload
    ///  * `AccessToken` - access token
    ///  * `ContentType` - either String or Stream 
    ///
    ///**Output Type**
    ///  * `Job<Choice<HttpWebResponse,exn>>` - On success returns `HttpWebResponse`, on failure - `exn` as Hopac job
    let uploadJob setOptUploadParams (StorageName storage, DataLakePath path, AccessToken token, content) =
        job {
            let optUploadParams = setOptUploadParams UploadParamOptional.Default
            let encodedPath     = WebUtility.UrlEncode path
            let uri =
                (baseUriTemplate storage) + 
                (if path.StartsWith("/") then "" else "/") + 
                encodedPath + 
                (if encodedPath.EndsWith("?") then "" else "?") +
                getQueryString optUploadParams

            let originStream =
                match content with
                | StreamContent x -> x
                | StringContent s ->
                    let stream = new MemoryStream()
                    let writer = new StreamWriter(stream)
                    writer.Write(s)
                    writer.Flush()
                    stream.Position <- 0L
                    stream :> Stream

            let req = HttpWebRequest.CreateHttp(uri)
            req.Method      <- "PUT"
            req.ContentType <- "application/octet-stream"
            req.Headers.["Authorization"] <- "Bearer " + token

            let! destStream = req.GetRequestStreamJob()
            do!  originStream.CopyToJob destStream
            destStream.Dispose()

            return! req.GetResponseJob()
        }

    ///**Description**
    ///Download file from Azure DataLake as `Stream`
    ///**Parameters**
    ///  * `StorageName` - storage account name 
    ///  * `DataLakePath` - what to download from DataLake
    ///  * `AccessToken` - access token
    ///
    ///**Output Type**
    ///  * `Job<Choice<Stream,exn>>` - On success returns `Stream`, on failure - `exn` as Hopac job        
    let downloadStreamJob (StorageName storage, DataLakePath path, AccessToken token) = 
        job {
            let encodedPath  = WebUtility.UrlEncode path
            let uri = 
                (baseUriTemplate storage) + 
                (if path.StartsWith("/") then "" else "/") + 
                encodedPath + 
                (if encodedPath.EndsWith("?") then "" else "?") +
                "op=OPEN&read=true"

            let req = HttpWebRequest.CreateHttp(uri)
            req.Method <- "GET"
            req.Headers.["Authorization"] <- "Bearer " + token

            let! resp = req.GetResponseJob()
            match resp with
            | Choice1Of2 resp ->
                let stream = resp.GetResponseStream()
                return Choice1Of2 stream
            | Choice2Of2 ex   ->
                return Choice2Of2 ex
        }

    ///**Description**
    ///Download file from Azure DataLake as `String`
    ///**Parameters**
    ///  * `StorageName` - storage account name 
    ///  * `DataLakePath` - what to download from DataLake
    ///  * `AccessToken` - access token
    ///
    ///**Output Type**
    ///  * `Job<Choice<String,exn>>` - On success returns `String`, on failure - `exn` as Hopac job   
    let downloadStringJob openParams = 
        job {
            let! resp = downloadStreamJob openParams
            match resp with
            | Choice1Of2 stream ->
                let! content = stream.ReadToEndJob()
                return Choice1Of2 content
            | Choice2Of2 ex   ->
                return Choice2Of2 ex
        }