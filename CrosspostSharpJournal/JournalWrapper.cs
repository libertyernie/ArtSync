﻿using ArtSourceWrapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CrosspostSharpJournal {
	/// <summary>
	/// An interface representing a client wrapper for a site that journals can be posted to.
	/// Normally a class will extend JournalSiteWrapper&lt;TWrapper, TPosition> instead of implementing this interface directly.
	/// </summary>
	public interface IJournalSiteWrapper {
		/// <summary>
		/// The batch size will be this amount if possible, or the greatest possible amount.
		/// Changing the batch size after some elements have been fetched may have unexpected effects in some cases.
		/// </summary>
		int BatchSize { get; set; }

		/// <summary>
		/// The minimum batch size for this wrapper. This property is read-only.
		/// </summary>
		int MinBatchSize { get; }

		/// <summary>
		/// The maximum batch size for this wrapper. This property is read-only.
		/// </summary>
		int MaxBatchSize { get; }

		/// <summary>
		/// The name of the site this wrapper is for (to be shown to the user), e.g. "DeviantArt".
		/// </summary>
		string SiteName { get; }
		
		/// <summary>
		/// A list of the currently cached submissions. Call FetchAsync to get more.
		/// </summary>
		IEnumerable<IJournalWrapper> Cache { get; }

		/// <summary>
		/// Whether the cache contains all of the submissions that are available.
		/// </summary>
		bool IsEnded { get; }

		/// <summary>
		/// Find the username of the currently logged in user.
		/// </summary>
		/// <returns>The username of the currently logged in user</returns>
		Task<string> WhoamiAsync();

		/// <summary>
		/// Get another batch of submissions and add them to the cache.
		/// </summary>
		/// <returns>The number of entries added to the cache, or -1 if no items were added because all of the submissions are already downloaded.</returns>
		Task<int> FetchAsync();

		/// <summary>
		/// Clears the cache and resets the internal position.
		/// </summary>
		void Clear();
	}

	/// <summary>
	/// An interface representing a client wrapper for a site that journals can be posted to.
	/// If you want to declare a variable that any type of wrapper can be assigned to, you may want to use IJournalSiteWrapper.
	/// </summary>
	/// <typeparam name="TWrapper">The type of object to wrap submissions in; must derive from IJournalWrapper</typeparam>
	/// <typeparam name="TPosition">The type of object to use for an internal position counter; must be a value type</typeparam>
	public abstract class JournalSiteWrapper<TWrapper, TPosition> : AsynchronousCachedEnumerable<TWrapper, TPosition>, IJournalSiteWrapper where TWrapper : IJournalWrapper where TPosition : struct {
		/// <summary>
		/// The name of the site this wrapper is for (to be shown to the user), e.g. "DeviantArt".
		/// </summary>
		public abstract string SiteName { get; }

		/// <summary>
		/// Looks up the username of the currently logged in user.
		/// </summary>
		/// <returns></returns>
		public abstract Task<string> WhoamiAsync();

		/// <summary>
		/// A list of the currently cached submissions. Call FetchAsync to get more.
		/// </summary>
		public new IEnumerable<IJournalWrapper> Cache {
			get {
				foreach (var w in base.Cache) yield return w;
			}
		}
	}

	public interface IJournalWrapper {
		string Title { get; }
		string HTMLDescription { get; }
		DateTime Timestamp { get; }
		string ViewURL { get; }
	}
}
