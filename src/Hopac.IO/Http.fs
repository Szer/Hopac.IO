namespace Hopac.IO

[<AutoOpen>]
module Net = 

    open System.Net
    open Hopac

    type System.Net.HttpWebRequest with

        //Copy pasted from HttpFs https://github.com/haf/Http.fs/blob/releases/v4.x/HttpFs/HttpFs.fs#L812
        //Thanks Haf!
        
        ///**Description**
        ///Returns a response to an Internet request as a Hopac job
        member request.GetResponseJob () =
            let inline succeed (wr : WebResponse) : Choice<WebResponse,exn> = downcast wr |> Choice1Of2
            let inline failure (ex : exn)         : Choice<WebResponse,exn> = Choice2Of2 ex
            let tryEndGetResponse ar =
                try
                    request.EndGetResponse ar
                    |> succeed
                with
                | :? WebException as wex when isNull wex.Response -> succeed wex.Response
                | ex -> failure ex
            Alt.fromBeginEnd request.BeginGetResponse tryEndGetResponse (fun _ -> request.Abort())