namespace Hopac.IO

[<AutoOpen>]
module Net = 

    open System.Net
    open Hopac

    type HttpWebRequest with

        //Copy-pasted from HttpFs https://github.com/haf/Http.fs/blob/releases/v4.x/HttpFs/HttpFs.fs#L812
        //Thanks Haf!
        
        ///**Description**
        ///Returns a response to an Internet request as a Hopac job
        member request.GetResponseJob () =
            let inline succeed (wr : WebResponse) : Choice<HttpWebResponse,exn> = downcast wr |> Choice1Of2
            let inline failure (ex : exn) : Choice<HttpWebResponse,exn> = Choice2Of2 ex
            let tryEndGetResponse ar =
                try
                  request.EndGetResponse ar
                  |> succeed
                with
                | :? WebException as wex when not <| isNull(wex.Response) -> succeed wex.Response
                | ex -> failure ex
            Alt.fromBeginEnd request.BeginGetResponse tryEndGetResponse (fun _ -> request.Abort())

        ///**Description**
        ///Returns a System.IO.Stream for writing data to the Internet resource as a Hopac job
        member request.GetRequestStreamJob () =
            Job.fromBeginEnd request.BeginGetRequestStream request.EndGetRequestStream