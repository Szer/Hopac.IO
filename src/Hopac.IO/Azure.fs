namespace Hopac.IO

module Azure =

    open Newtonsoft.Json
    open Net
    open System
    open System.Text
    open System.Net
    open Hopac
    open Hopac.Infixes
    open Stream

    let defaultEncoding = UTF8Encoding()

    type GrantType = 
        | AuthorizationCode
        | ClientCredentials
        | Other of string
        member self.AsString() = 
            match self with
            | AuthorizationCode -> "authorization_code"
            | ClientCredentials -> "client_credentials"
            | Other x -> x

    type AdToken = 
        { [<JsonProperty("token_type")>]     TokenType    : string
          [<JsonProperty("expires_in")>]     ExpiresIn    : int
          [<JsonProperty("ext_expires_in")>] ExtExpiresIn : int
          [<JsonProperty("expires_on")>]     ExpiresOn    : int
          [<JsonProperty("not_before")>]     NotBefore    : int
          [<JsonProperty("resource")>]       Resource     : string
          [<JsonProperty("access_token")>]   AccessToken  : string }

    type AdTokenRequest = 
        { ClientId         : string
          ClientSecret     : string
          TenantId         : string          
          Resource         : string
          LoginUriTemplate : string
          GrantType        : GrantType }
        static member Default =
            { ClientId         = ""
              ClientSecret     = ""
              TenantId         = ""
              Resource         = "https://management.core.windows.net/"
              LoginUriTemplate = "https://login.microsoftonline.com/{tenantId}/oauth2/token"
              GrantType        = ClientCredentials }

    let internal reqToBody req = 
        sprintf "resource=%s&client_id=%s&grant_type=%s&client_secret=%s"
        <| WebUtility.UrlEncode(req.Resource)
        <| WebUtility.UrlEncode(req.ClientId)
        <| WebUtility.UrlEncode(req.GrantType.AsString())
        <| WebUtility.UrlEncode(req.ClientSecret)        

    
    ///**Description**
    ///Retrive token to Azure ActiveDirectory to do other useful things. Usual expiration interval is 1 hour
    ///**Parameters**
    ///  * `setRequest` - function to setup upload parameters. 
    ///Example ```fun p -> { p with TenantId = "abc"``` }
    ///
    ///**Output Type**
    ///  * `Job<Choice<AdToken,exn>>` - On success returns `AdToken`, on failure - `exn` as Hopac job
    let getAdTokenJob setRequest =
        let tokenRequest = setRequest AdTokenRequest.Default
        
        let url = Uri(tokenRequest.LoginUriTemplate.Replace("{tenantId}", tokenRequest.TenantId))
        let req = HttpWebRequest.CreateHttp(url)
        req.Method      <- "POST"
        req.ContentType <- "application/x-www-form-urlencoded"            

        req.GetRequestStreamJob()
        >>= Job.useIn (fun s ->
            reqToBody tokenRequest
            |> defaultEncoding.GetBytes
            |> s.WriteJob)
        >>= req.GetResponseJob
        >>= function
        | Choice1Of2 resp ->
            Job.tryIn
                (resp.GetResponseStream().ReadToEndJob()
                 >>- JsonConvert.DeserializeObject<AdToken>)
                <| (Choice1Of2 >> Job.result)
                <| (Choice2Of2 >> Job.result)
        | Choice2Of2 ex   -> 
            Job.result <| Choice2Of2 ex