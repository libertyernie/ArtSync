﻿namespace SourceWrappers

open FAExportLib
open System
open AsyncHelpers

type FurAffinityMinimalPostWrapper(submission: FAFolderSubmission) =
    member this.Id = submission.id

    interface IPostWrapper with
        member this.Title = submission.title
        member this.HTMLDescription = ""
        member this.Mature = true
        member this.Adult = true
        member this.Tags = Seq.empty
        member this.Timestamp = DateTime.UtcNow
        member this.ViewURL = submission.link
        member this.ImageURL = submission.thumbnail
        member this.ThumbnailURL = submission.thumbnail

type FurAffinityPostWrapper(submission: FASubmission) =
    interface IPostWrapper with
        member this.Title = submission.title
        member this.HTMLDescription =
            let html = submission.description
            let index = html.IndexOf("<br><br>")
            if index > 1 then
                html.Substring(index + 8).TrimStart()
            else
                html
        member this.Mature = submission.rating.ToLowerInvariant() = "mature"
        member this.Adult = submission.rating.ToLowerInvariant() = "explicit"
        member this.Tags = submission.keywords |> Seq.map id
        member this.Timestamp = submission.posted_at.UtcDateTime
        member this.ViewURL = submission.link
        member this.ImageURL = submission.download
        member this.ThumbnailURL = submission.thumbnail

type FurAffinityMinimalSourceWrapper(a: string, b: string, scraps: bool) =
    inherit SourceWrapper<int>()

    let apiClient = new FAUserClient(a, b)

    let mutable cached_username: string = null

    let getUser = async {
        if isNull cached_username then
            let! u = apiClient.WhoamiAsync() |> Async.AwaitTask
            cached_username <- u
        return cached_username
    }

    member this.GetSubmission id = async {
        return! apiClient.GetSubmissionAsync id |> Async.AwaitTask
    }
    
    override this.Name = if scraps then "Fur Affinity (scraps)" else "Fur Affinity (gallery)"

    override this.Fetch cursor take = async {
        let! username = getUser
        let folder = if scraps then FAFolder.scraps else FAFolder.gallery
        let page = cursor |> Option.defaultValue 1

        let! gallery = apiClient.GetSubmissionsAsync(username, folder, page) |> Async.AwaitTask
        
        return {
            Posts = gallery |> Seq.map FurAffinityMinimalPostWrapper |> Seq.map (fun w -> w :> IPostWrapper)
            Next = page + 1
            HasMore = Seq.length gallery > 0
        }
    }

    override this.Whoami = getUser

    override this.GetUserIcon size = async {
        let! username = getUser
        let! user = apiClient.GetUserAsync(username) |> Async.AwaitTask
        return user.avatar
    }

type FurAffinitySourceWrapper(a: string, b: string, scraps: bool) =
    inherit SourceWrapper<int>()

    let source = new FurAffinityMinimalSourceWrapper(a, b, scraps)
    
    let cache = new System.Collections.Generic.List<int>()
    let mutable cache_cursor: int option = None
    let mutable cache_has_more = true
    
    override this.Name = source.Name

    override this.Fetch cursor take = async {
        let skip = cursor |> Option.defaultValue 0
        while cache.Count < skip + take && cache_has_more do
            let! result = source.Fetch cache_cursor 60
            result.Posts
                |> Seq.map (fun w -> w :?> FurAffinityMinimalPostWrapper)
                |> Seq.map (fun w -> w.Id)
                |> cache.AddRange
            cache_cursor <- Some result.Next
            cache_has_more <- result.HasMore

        let ids =
            cache
            |> skipSafe skip
            |> Seq.truncate take

        let v = Seq.length ids

        let! gallery =
            ids
            |> Seq.map source.GetSubmission
            |> Async.Parallel

        return {
            Posts = gallery |> Seq.map FurAffinityPostWrapper |> Seq.map (fun w -> w :> IPostWrapper)
            Next = skip + take
            HasMore = Seq.length gallery > 0
        }
    }

    override this.Whoami = source.Whoami
    override this.GetUserIcon size = source.GetUserIcon size