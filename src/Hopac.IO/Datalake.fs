namespace Hopac.IO

module Datalake = 

    open System.Net
    open System.IO
    open System
    open Stream
    open Hopac

    let internal baseUriTemplate = sprintf "https://%s.azuredatalakestore.net/webhdfs/v1/"

    type SyncFlag = 
        | Data
        | Metadata
        | Close
        member self.AsString () =
            match self with
            | Data     -> "DATA"
            | Metadata -> "METADATA"
            | Close    -> "CLOSE"

    type UploadParam = 
        { StorageName : string
          Path        : string
          AccessToken : string 
          Stream      : Stream  
          SyncFlag    : SyncFlag option 
          LeaseId     : Guid     option 
          Overwrite   : bool     option 
          Permission  : string   option
          ApiVersion  : string   option }
        static member Default = 
            { StorageName = ""
              Path        = ""
              AccessToken = ""
              Stream      = null
              SyncFlag    = None
              LeaseId     = None
              Overwrite   = None
              Permission  = None
              ApiVersion  = None }

    let internal getQueryString uploadParams = 
        seq {
            if uploadParams.SyncFlag.IsSome then
                yield (sprintf "syncFlag=%s" 
                <| uploadParams.SyncFlag.Value.AsString()
                |> WebUtility.UrlEncode)

            if uploadParams.LeaseId.IsSome then
                yield (sprintf "leaseId=%s" 
                <| uploadParams.LeaseId.Value.ToString()            
                |> WebUtility.UrlEncode)

            if uploadParams.Overwrite.IsSome then
                yield (sprintf "overwrite=%s" 
                <| uploadParams.Overwrite.Value.ToString()            
                |> WebUtility.UrlEncode)

            if uploadParams.Permission.IsSome then
                yield (sprintf "permission=%s" 
                <| uploadParams.Permission.Value.ToString()            
                |> WebUtility.UrlEncode)

            if uploadParams.ApiVersion.IsSome then
                yield (sprintf "api-version=%s" 
                <| uploadParams.ApiVersion.Value.ToString()            
                |> WebUtility.UrlEncode)
            yield "write=true"
            yield "op=CREATE"
        } |> String.concat "&"

    
    ///**Description**
    ///Uploads stream to Azure DataLake
    ///**Parameters**
    ///  * `setUploadParams` - function to setup upload parameters. 
    ///Example ```fun p -> { p with Path = "\myFolder\file.txt"``` }
    ///
    ///**Output Type**
    ///  * `Job<Choice<HttpWebResponse,exn>>` - On success returns `HttpWebResponse`, on failure - `exn` as Hopac job
    let uploadStreamJob setUploadParams =
        job {
            let uploadParams = setUploadParams UploadParam.Default
            let encodedPath  = WebUtility.UrlEncode uploadParams.Path
            let uri = 
                (baseUriTemplate uploadParams.StorageName) + 
                (if encodedPath.StartsWith("/") then "" else "/") + 
                encodedPath + 
                (if encodedPath.EndsWith("?") then "" else "?") +
                getQueryString uploadParams

            let req = HttpWebRequest.CreateHttp(uri)
            req.Method      <- "PUT"
            req.ContentType <- "application/octet-stream"
            req.Headers.["Authorization"] <- "Bearer " + uploadParams.AccessToken

            let! stream = req.GetRequestStreamJob()
            do! uploadParams.Stream.CopyToJob stream
            stream.Dispose()

            return! req.GetResponseJob()
        }
        
    let downloadStreamJob () = ()