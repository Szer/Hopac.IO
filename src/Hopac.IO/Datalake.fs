namespace Hopac.IO

module Datalake = 

    open System.Net
    open System.IO
    open System
    open Stream
    open Hopac
    open Hopac.Infixes
    open Newtonsoft.Json
    open Newtonsoft.Json.Linq

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

    type FileStatus =
        { [<JsonProperty("length")>]           Length           : int
          [<JsonProperty("pathSuffix")>]       PathSuffix       : string
          [<JsonProperty("type")>]             Type             : string
          [<JsonProperty("blockSize")>]        BlockSize        : int64
          [<JsonProperty("accessTime")>]       AccessTime       : int64
          [<JsonProperty("modificationTime")>] ModificationTime : int64
          [<JsonProperty("replication")>]      Replication      : int
          [<JsonProperty("permission")>]       Permission       : string
          [<JsonProperty("owner")>]            Owner            : Guid
          [<JsonProperty("group")>]            Group            : Guid }

    type FileStatuses =
        { [<JsonProperty("FileStatus")>] FileStatuses           : FileStatus array}

    let internal selectJPath (path: string) (jTok: JToken) = jTok.SelectTokens path
    let internal toObject<'a> (jTok: JToken) = jTok.ToObject<'a>()

    let internal getQueryStringForUpload uploadParams = 
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
        let optUploadParams = setOptUploadParams UploadParamOptional.Default
        let encodedPath     = WebUtility.UrlEncode path
        let uri =
            (baseUriTemplate storage) + 
            (if path.StartsWith("/") then "" else "/") + 
            encodedPath + 
            (if encodedPath.EndsWith("?") then "" else "?") +
            getQueryStringForUpload optUploadParams

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

        req.GetRequestStreamJob()
        >>= Job.useIn originStream.CopyToJob
        >>= req.GetResponseJob

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
        
        req.GetResponseJob()
        >>- function
        | Choice1Of2 resp -> Choice1Of2 <| resp.GetResponseStream()
        | Choice2Of2 ex   -> Choice2Of2 ex

    ///**Description**
    ///Download file from Azure DataLake as `String`
    ///**Parameters**
    ///  * `StorageName` - storage account name 
    ///  * `DataLakePath` - what to download from DataLake
    ///  * `AccessToken` - access token
    ///
    ///**Output Type**
    ///  * `Job<Choice<String,exn>>` - On success returns `String`, on failure - `exn` as Hopac job   
    let downloadStringJob =
        downloadStreamJob
        >=> function
        | Choice1Of2 stream -> stream.ReadToEndJob() >>- Choice1Of2
        | Choice2Of2 ex     ->            Job.result <|  Choice2Of2 ex

    ///**Description**
    ///Get file list from folder
    ///**Parameters**
    ///  * `StorageName` - storage account name 
    ///  * `DataLakePath` - what to download from DataLake
    ///  * `AccessToken` - access token
    ///
    ///**Output Type**
    ///  * `Job<Choice<FileStatus [],exn>>` - On success returns array of `FileStatus`, on failure - `exn` as Hopac job   
    let getFileListJob (StorageName storage, DataLakePath path, AccessToken token) = 
        let encodedPath = WebUtility.UrlEncode path
        let uri =
            (baseUriTemplate storage) + 
            (if path.StartsWith("/") then "" else "/") + 
            encodedPath + 
            (if encodedPath.EndsWith("?") then "" else "?") +
            "op=LISTSTATUS"

        let req = HttpWebRequest.CreateHttp(uri)
        req.Method <- "GET"
        req.Headers.["Authorization"] <- "Bearer " + token

        req.GetResponseJob()
        >>= function
        | Choice1Of2 resp ->
            resp.GetResponseStream().ReadToEndJob()
            >>- (JToken.Parse 
                 >> selectJPath "$.FileStatuses.FileStatus[*]"
                 >> Seq.map toObject<FileStatus>
                 >> Array.ofSeq
                 >> Choice1Of2)
        | Choice2Of2 ex -> Job.result <| Choice2Of2 ex