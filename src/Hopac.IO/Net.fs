namespace Hopac.IO

[<AutoOpen>]
module Net =

    open System.IO
    open System.Net
    open System.Text
    open Hopac

    type HttpWebRequest with

        //Copy-pasted from HttpFs https://github.com/haf/Http.fs/blob/releases/v4.x/HttpFs/HttpFs.fs#L812
        //Thanks Haf!

        ///**Description**
        ///Returns a response to an Internet request as a Hopac job
        member request.GetResponseJob () =
            let inline succeed (wr : WebResponse) = wr :?> HttpWebResponse |> Ok
            let tryEndGetResponse ar =
                try
                    request.EndGetResponse ar
                    |> succeed
                with
                | :? WebException as wex when not <| isNull(wex.Response) -> succeed wex.Response
                | ex -> Error ex
            Alt.fromBeginEnd request.BeginGetResponse tryEndGetResponse (fun _ -> request.Abort())

        ///**Description**
        ///Returns a System.IO.Stream for writing data to the Internet resource as a Hopac job
        member request.GetRequestStreamJob () =
            Job.fromBeginEnd request.BeginGetRequestStream request.EndGetRequestStream

    type WebResponse with
        ///**Description**
        ///Returns a string with the content of a WebResponse as a Hopac job
        ///**Parameters**
        ///  * `encoding` - The text encoding of the response. Defaults to UTF-8.
        member response.GetResponseAsStringJob (?encoding) =
            let encoding = defaultArg encoding Encoding.UTF8
            let sr = new StreamReader(response.GetResponseStream(), encoding)
            Job.using sr (fun sr -> sr.ReadToEndJob())
