﻿namespace DeviantArtFs

open FSharp.Data
open System.Net
open System.IO
open System

type DeviantArtBaseResponse = JsonProvider<"""{"status":"error"}""">

type DeviantArtErrorResponse = JsonProvider<"""{"error":"invalid_request","error_description":"Must provide an access_token to access this resource.","status":"error"}""">

type DeviantArtException(resp: WebResponse, body: DeviantArtErrorResponse.Root) =
    inherit Exception(body.ErrorDescription)

    member __.ResponseBody = body
    member __.StatusCode =
        match resp with
        | :? HttpWebResponse as h -> Nullable h.StatusCode
        | _ -> Nullable()

type DeviantArtPagedResult<'a> = {
    HasMore: bool
    NextOffset: int option
    Results: seq<'a>
}

module internal dafs =
    let whenDone (f: 'a -> 'b) (workflow: Async<'a>) = async {
        let! result = workflow
        return f result
    }

    let urlEncode = WebUtility.UrlEncode

    let userAgent = "DeviantArtFs/0.1 (https://github.com/libertyernie/CrosspostSharp)"
    let createRequest (token: IDeviantArtAccessToken) (url: string) =
        let req = WebRequest.CreateHttp url
        req.UserAgent <- userAgent
        req.Headers.["Authorization"] <- sprintf "Bearer %s" token.AccessToken
        req
    let asyncRead (req: WebRequest) = async {
        try
            use! resp = req.AsyncGetResponse()
            use sr = new StreamReader(resp.GetResponseStream())
            let! json = sr.ReadToEndAsync() |> Async.AwaitTask
            let obj = DeviantArtBaseResponse.Parse json
            if obj.Status = "error" then
                let error_obj = DeviantArtErrorResponse.Parse json
                return raise (new DeviantArtException(resp, error_obj))
            else
                return json
        with
            | :? WebException as ex ->
                use resp = ex.Response
                use sr = new StreamReader(resp.GetResponseStream())
                let! json = sr.ReadToEndAsync() |> Async.AwaitTask
                let error_obj = DeviantArtErrorResponse.Parse json
                return raise (new DeviantArtException(resp, error_obj))
    }