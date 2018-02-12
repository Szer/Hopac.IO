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
    let internal defaultBlockSize = 4 * 1024 * 1024

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
    type AppendOffset = AppendOffset of int64

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
    
    let private append token offset (baseUri: string) (buffer: byte[]) method (stream: IO.Stream) =
        let uri = baseUri + "op=APPEND&append=true&offset=" + string offset
        stream.ReadJob buffer
        >>= fun count ->
            if count = 0 then Job.result <| Ok 0 else

            let req = HttpWebRequest.CreateHttp(uri)
            req.Method      <- method
            req.ContentType <- "application/octet-stream"
            req.Headers.["Authorization"] <- "Bearer " + token
            req.GetRequestStreamJob()
            >>= Job.useIn (fun reqStream -> reqStream.WriteJob (buffer, 0, count))
            >>= fun _ ->
                let rec getResp retries =
                    req.GetResponseJob()
                    >>= function
                        | Ok resp when resp.StatusCode = HttpStatusCode.OK -> 
                            Job.result <| Ok count
                        | Ok resp  -> Job.result <| Error resp.StatusDescription
                        | Error ex -> 
                            if retries <= 0 then Job.result <| Error ex.Message
                            else getResp (retries - 1)
                getResp 5

    ///**Description**
    ///Appends stream or string to Azure DataLake file
    ///**Parameters**
    ///  * `StorageName` - storage account name 
    ///  * `DataLakePath` - path inside DataLake where to upload
    ///  * `AccessToken` - access token
    ///  * `AppendOffset` - offset of file in DataLake
    ///  * `ContentType` - either String or Stream 
    ///
    ///**Output Type**
    ///  * `Job<Result<int,string>>` - On success returns number of bytes appended, on failure - error description as Hopac job
    let appendJob (StorageName storage, DataLakePath path, AccessToken token, AppendOffset offset, stream: IO.Stream) =
        let encodedPath     = WebUtility.UrlEncode path
        let uri =
            (baseUriTemplate storage) + 
            (if path.StartsWith("/") then "" else "/") + 
            encodedPath + 
            (if encodedPath.EndsWith("?") then "" else "?")

        let buffer: byte[] = Array.zeroCreate defaultBlockSize
        append token offset uri buffer "POST" stream

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
    ///  * `Job<Result<HttpStatusCode,string>>` - On success returns `HttpStatusCode`, on failure - error description as Hopac job
    let uploadJob setOptUploadParams (StorageName storage, DataLakePath path, AccessToken token, content) =
        let optUploadParams = setOptUploadParams UploadParamOptional.Default
        let encodedPath     = WebUtility.UrlEncode path
        let baseUri =
            (baseUriTemplate storage) + 
            (if path.StartsWith("/") then "" else "/") + 
            encodedPath + 
            (if encodedPath.EndsWith("?") then "" else "?")
        let createUri =
            baseUri +
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

        let buffer: byte[] = Array.zeroCreate defaultBlockSize

        originStream.ReadJob buffer
        >>= fun count ->
            let req = HttpWebRequest.CreateHttp(createUri)
            req.Method      <- "PUT"
            req.ContentType <- "application/octet-stream"
            req.Headers.["Authorization"] <- "Bearer " + token

            req.GetRequestStreamJob()
            >>= Job.useIn (fun reqStream -> reqStream.WriteJob (buffer, 0, count))
            >>= req.GetResponseJob
            >>= function
                | Ok resp ->
                    if count < defaultBlockSize then Job.result <| Ok HttpStatusCode.Created
                    else
                        let rec appendToTheEnd curOffset =
                            append token curOffset baseUri buffer "POST" originStream
                            >>= function
                                | Ok count when count = 0 -> Job.result <| Ok HttpStatusCode.Created
                                | Ok count -> appendToTheEnd (curOffset + int64 count)
                                | Error ex -> Job.result <| Error ex
                        appendToTheEnd (int64 defaultBlockSize)
                | Error ex -> Job.result <| Error ex.Message

    ///**Description**
    ///Download file from Azure DataLake as `Stream`
    ///**Parameters**
    ///  * `StorageName` - storage account name 
    ///  * `DataLakePath` - what to download from DataLake
    ///  * `AccessToken` - access token
    ///
    ///**Output Type**
    ///  * `Job<Result<Stream,string>>` - On success returns `Stream`, on failure - error description as Hopac job        
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
        | Ok resp when resp.StatusCode = HttpStatusCode.OK  -> 
            try  Ok <| resp.GetResponseStream()
            with e -> Error e.Message
        | Ok resp  -> Error resp.StatusDescription
        | Error ex -> Error ex.Message

    ///**Description**
    ///Download file from Azure DataLake as `String`
    ///**Parameters**
    ///  * `StorageName` - storage account name 
    ///  * `DataLakePath` - what to download from DataLake
    ///  * `AccessToken` - access token
    ///
    ///**Output Type**
    ///  * `Job<Result<String,string>>` - On success returns `String`, on failure - error description as Hopac job   
    let downloadStringJob =
        downloadStreamJob
        >=> function
        | Ok stream -> 
            Job.tryIn
                <| stream.ReadToEndJob()
                <| (Ok >> Job.result)
                <| fun e -> Job.result <| Error e.Message
        | Error ex  -> Job.result <| Error ex

    ///**Description**
    ///Get file list from folder
    ///**Parameters**
    ///  * `StorageName` - storage account name 
    ///  * `DataLakePath` - what to download from DataLake
    ///  * `AccessToken` - access token
    ///
    ///**Output Type**
    ///  * `Job<Result<FileStatus [],string>>` - On success returns array of `FileStatus`, on failure - error description as Hopac job   
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
        | Ok resp when resp.StatusCode = HttpStatusCode.OK ->
            Job.tryIn
                (resp.GetResponseStream().ReadToEndJob()
                 >>- (JToken.Parse 
                      >> selectJPath "$.FileStatuses.FileStatus[*]"
                      >> Seq.map toObject<FileStatus>
                      >> Array.ofSeq))
                <| (Ok >> Job.result)
                <| fun e -> Job.result <| Error e.Message
        | Ok resp  -> Job.result <| Error resp.StatusDescription
        | Error ex -> Job.result <| Error ex.Message