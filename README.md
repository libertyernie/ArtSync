CrosspostSharp 3.5
==================

Source: https://github.com/libertyernie/CrosspostSharp

--------------------

CrosspostSharp is a Windows desktop application (written in C# and F#) that
loads individual submissions from art sites and lets you post them to your
other accounts, maintaining the title, description, and tags.

In most cases, only photo posts are supported. However, Twitter and Tumblr
text posts and DeviantArt status updates will be recognized as simple text
posts, and you will be able to crosspost them easily without uploading a
photo.

Requirements:

* Internet Explorer 11
* .NET Framework 4.6.1 or higher

Supported Sites
---------------

Use the Tools menu to add and remove accounts. Multiple accounts per site are
supported on all sites except DeviantArt and Sta.sh.

On Tumblr and Furry Network, CrosspostSharp lets you use read from and post to
multiple blogs/characters using a single account.

* DeviantArt / Sta.sh
* Flickr
* FurAffinity
* Furry Network
* Inkbunny
* Mastodon
* Pillowfort
* Tumblr
* Twitter
* Weasyl

You can also upload images anonymously to Imgur. When you do this, a key will
be stored in the file imgur-uploads.txt, which CrosspostSharp will use to find
previously uploaded images on Imgur and let you delete them.

You can also open or save local files, either in PNG or JPEG format or in .cps
files which contain metadata like description, tags, etc.

File Formats
------------

CrosspostSharp can read PNG, JPEG, and GIF image formats, either from the web
or from a file (with File > Open.)

CrosspostSharp can also open and save .cps files. These are JSON files that
contain encoded image data and metadata. The .cps file format looks like this:

	{
		"data": "[base 64 encoded image data]",
		"title": "text string",
		"description": "html string",
		"url": "text string",
		"tags": ["tag1", "tag2"],
		"mature": false,
		"adult": false
	}

Credentials
-----------

Credentials are stored in the file CrosspostSharp.json. This includes tokens
are used to give CrosspostSharp access to your accounts. Make sure you keep
this file safe!

Compiling from Source
---------------------

This project can be built with Visual Studio 2015 or 2017. When you clone the
Git repository, make sure you do it recursively (or you can manually clone all
the submodules.)

The file OAuthConsumer.cs is missing from the CrosspostSharp3 project. Get your own
OAuth keys, then put the following into OAuthConsumer.cs:

    namespace CrosspostSharp {
        public static class OAuthConsumer {
            public static class Tumblr {
                public static string CONSUMER_KEY = "consumer key goes here";
                public static string CONSUMER_SECRET = "secret key goes here";
            }
            public static class Twitter {
                public static string CONSUMER_KEY = "consumer key goes here";
                public static string CONSUMER_SECRET = "secret key goes here";
            }
            public static class DeviantArt {
                public static string CLIENT_ID = "client_id goes here";
                public static string CLIENT_SECRET = "client_secret goes here";
            }
            public static class Flickr {
                public static string KEY = "consumer key goes here";
                public static string SECRET = "secret key goes here";
            }
        }
    }

Libraries Used
----------------

From NuGet:

* [CommonMark.NET](https://www.nuget.org/packages/CommonMark.NET) (Markdown support)
* [FlickrNet](https://www.nuget.org/packages/FlickrNet) (Flickr support)
* [FSharp.Control.AsyncSeq](https://www.nuget.org/packages/FSharp.Control.AsyncSeq) (asynchronous sequence support)
* [Furloader](https://github.com/Kycklingar/Furloader) (Fur Affinity login window)
* [Imgur.API](https://www.nuget.org/packages/Imgur.API) (Imgur support)
* [ISchemm.WinFormsOAuth](https://www.nuget.org/packages/ISchemm.WinFormsOAuth) (Flickr, Tumblr, and Twitter login windows)
* [Mastonet](https://www.nuget.org/packages/Mastonet) (Mastodon support)
* [Newtonsoft.Json](https://www.nuget.org/packages/Newtonsoft.Json) (JSON support)
* [NewTumblrSharp](https://www.nuget.org/packages/NewTumblrSharp) (Tumblr support)
* [TweetinviAPI](https://www.nuget.org/packages/TweetinviAPI) (Twitter support)

From GitHub:

* [DeviantartApi](https://github.com/libertyernie/DeviantartApi) (DeviantArt and Sta.sh support)
* [FAWinFormsLogin](https://github.com/libertyernie/FAWinFormsLogin) (Fur Affinity login window) - originally part of [Furloader](https://github.com/Kycklingar/Furloader)

From this repository:

* FAExportLib (Fur Affinity download via [FAExport](https://faexport.boothale.net/))
* FurAffinityFs (Fur Affinity upload)
* FurryNetworkLib
* InkbunnyLib
* PillowfortFs
* WeasylLib