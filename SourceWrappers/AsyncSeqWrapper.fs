namespace SourceWrappers

open System
open System.Threading.Tasks
open FSharp.Control

module internal Swu =
    let skipSafe num = 
        Seq.zip (Seq.initInfinite id)
        >> Seq.skipWhile (fun (i, _) -> i < num)
        >> Seq.map snd

    let whenDone (f: 'a -> 'b) (workflow: Async<'a>) = async {
        let! result = workflow
        return f result
    }

    let fromUnixTime (secs: int) = (new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).AddSeconds(float secs)

type AsyncSeqWrapperUserInfo = {
    username: string
    icon_url: string option
}

[<AbstractClass>]
type AsyncSeqWrapper() as this =
    let cache = lazy (
        printfn "%s: Initializing cache" this.Name
        this.FetchSubmissionsInternal() |> AsyncSeq.cache
    )

    let user = lazy (this.FetchUserInternal() |> Async.StartAsTask)

    abstract member Name: string with get
    abstract member FetchUserInternal: unit -> Async<AsyncSeqWrapperUserInfo>
    abstract member FetchSubmissionsInternal: unit -> AsyncSeq<IPostBase>

    member __.GetSubmissions() = cache.Force()
    member __.GetUserAsync() = user.Force()

    member __.WhoamiAsync() =
        this.GetUserAsync()
        |> Async.AwaitTask
        |> Swu.whenDone (fun u -> u.username)
        |> Async.StartAsTask
    member __.GetUserIconAsync() =
        this.GetUserAsync()
        |> Async.AwaitTask
        |> Swu.whenDone (fun u -> u.icon_url)
        |> Swu.whenDone (Option.defaultValue "https://upload.wikimedia.org/wikipedia/commons/c/ce/Transparent.gif")
        |> Async.StartAsTask

    interface AsyncSeq<IPostBase> with
        member __.GetEnumerator() = cache.Value.GetEnumerator()

type OrderedAsyncSeqWrapper(wrapper: AsyncSeqWrapper) =
    inherit AsyncSeqWrapper()

    override __.Name = wrapper.Name
    override __.FetchSubmissionsInternal() = asyncSeq {
        let! all = wrapper.GetSubmissions() |> AsyncSeq.toListAsync
        for x in all |> Seq.sortByDescending (fun x -> x.Timestamp) do
            yield x
    }
    override __.FetchUserInternal() = wrapper.FetchUserInternal()

type AsyncSeqWrapperOfSeq(name: string, seq: seq<IPostBase>) =
    inherit AsyncSeqWrapper()

    override __.Name = name
    override __.FetchSubmissionsInternal() = AsyncSeq.ofSeq seq
    override __.FetchUserInternal() = async {
        return {
            username = ""
            icon_url = None
        }
    }