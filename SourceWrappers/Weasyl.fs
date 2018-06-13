﻿namespace SourceWrappers

open AsyncHelpers
open WeasylLib.Api
open WeasylLib.Frontend
open System.Threading.Tasks

type WeasylPostWrapper(submission: WeasylSubmissionBaseDetail) =
    interface IPostWrapper with
        member this.Title = ""
        member this.HTMLDescription = submission.HTMLDescription
        member this.Mature = submission.rating = "mature"
        member this.Adult = submission.rating = "explicit"
        member this.Tags = submission.tags |> Seq.map id
        member this.Timestamp = submission.posted_at
        member this.ViewURL = submission.link
        member this.ImageURL = submission.media.submission |> Seq.map (fun s -> s.url) |> Seq.head
        member this.ThumbnailURL = submission.media.thumbnail |> Seq.map (fun s -> s.url) |> Seq.head

type WeasylSourceWrapper(username: string) =
    inherit SourceWrapper<int>()

    let apiClient = new WeasylApiClient()
    
    override this.Name = "Weasyl"

    override this.Fetch cursor take = async {
        let gallery_options = new WeasylApiClient.GalleryRequestOptions()
        gallery_options.nextid <- cursor |> Option.toNullable
        gallery_options.count <- Some take |> Option.toNullable

        let! gallery = apiClient.GetUserGalleryAsync(username, gallery_options) |> Async.AwaitTask

        let! submissions =
            gallery.submissions
            |> Seq.map (fun s -> apiClient.GetSubmissionAsync(s.submitid))
            |> Task.WhenAll
            |> Async.AwaitTask

        return {
            Posts = submissions
                |> Seq.map (fun s -> s :> WeasylSubmissionBaseDetail)
                |> Seq.map WeasylPostWrapper
                |> Seq.map (fun w -> w :> IPostWrapper)
            Next = gallery.nextid |> Option.ofNullable |> Option.defaultValue 0
            HasMore = gallery.nextid.HasValue
        }
    }

    override this.Whoami = async {
        return username
    }

    override this.GetUserIcon size = async {
        return! apiClient.GetAvatarUrlAsync(username) |> Async.AwaitTask
    }

type WeasylCharacterSourceWrapper(username: string) =
    inherit SourceWrapper<int>()

    let apiClient = new WeasylApiClient()
    
    override this.Name = "Weasyl (characters)"

    override this.Fetch cursor take = async {
        let! allIds = Scraper.GetCharacterIdsAsync(username) |> Async.AwaitTask
        let skip = cursor |> Option.defaultValue 0
        let ids = allIds |> skipSafe skip |> Seq.truncate take

        let! submissions =
            ids
            |> Seq.map apiClient.GetCharacterAsync
            |> Seq.map Async.AwaitTask
            |> Async.Parallel

        return {
            Posts = submissions
                |> Seq.map (fun s -> s :> WeasylSubmissionBaseDetail)
                |> Seq.map WeasylPostWrapper
                |> Seq.map (fun w -> w :> IPostWrapper)
            Next = skip + take
            HasMore = allIds.Count > skip + take
        }
    }

    override this.Whoami = async {
        return username
    }

    override this.GetUserIcon size = async {
        return! apiClient.GetAvatarUrlAsync(username) |> Async.AwaitTask
    }