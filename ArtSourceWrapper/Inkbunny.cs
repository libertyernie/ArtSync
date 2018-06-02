﻿using InkbunnyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using InkbunnyLib.Responses;

namespace ArtSourceWrapper {
	public class InkbunnyWrapper : SiteWrapper<InkbunnySubmissionWrapper, int> {
		private InkbunnyClient _client;
        private string _rid;

        public InkbunnyWrapper(InkbunnyClient client) {
			_client = client;
		}
		
        public override string WrapperName => "Inkbunny";
		public override bool SubmissionsFiltered => true;

		public override int BatchSize { get; set; } = 30;
        public override int MinBatchSize => 1;
        public override int MaxBatchSize => 100;

        protected override async Task<InternalFetchResult<InkbunnySubmissionWrapper, int>> InternalFetchAsync(int? startPosition, int maxCount) {
            var response = startPosition == null
                ? await _client.SearchAsync(
                    new InkbunnySearchParameters { UserId = _client.UserId, DaysLimit = 30 },
                    maxCount)
                : await _client.SearchAsync(_rid,
                    startPosition.Value + 1,
                    maxCount);
            
            if (response.pages_count < (startPosition ?? 1)) {
                return new InternalFetchResult<InkbunnySubmissionWrapper, int>(response.page + 1, isEnded: true);
            }

            _rid = response.rid;
            var details = await _client.GetSubmissionsAsync(response.submissions.Select(s => s.submission_id), show_description_bbcode_parsed: true);
            var wrappers = details.submissions
                .Where(s => s.@public)
                .OrderByDescending(s => s.create_datetime)
                .Select(s => new DeletableInkbunnySubmissionWrapper(_client, s));
            return new InternalFetchResult<InkbunnySubmissionWrapper, int>(wrappers, response.page + 1);
        }

        public override async Task<string> WhoamiAsync() {
            AsynchronousCachedEnumerable<InkbunnySubmissionWrapper, int> wrapper = this;
			if (!wrapper.Cache.Any()) {
                await wrapper.FetchAsync();
            }
            if (!wrapper.Cache.Any()) {
                throw new Exception("No Inkbunny submissions - cannot determine username");
            }
            return wrapper.Cache.First().Username;
        }

        public override async Task<string> GetUserIconAsync(int size) {
            AsynchronousCachedEnumerable<InkbunnySubmissionWrapper, int> wrapper = this;
            if (!wrapper.Cache.Any()) {
                await wrapper.FetchAsync();
            }
            if (!wrapper.Cache.Any()) {
                throw new Exception("No Inkbunny submissions - cannot determine username");
            }
            return wrapper.Cache.First().UserIcon;
        }
    }

	public class InkbunnySubmissionWrapper : ISubmissionWrapper {
		public readonly InkbunnySubmissionDetail Submission;

		public Color? BorderColor {
			get {
				switch (Submission.rating_id) {
					case InkbunnyRating.Mature:
						return Color.FromArgb(170, 187, 34);
					case InkbunnyRating.Adult:
						return Color.FromArgb(185, 30, 35);
					default:
						return null;
				}
			}
		}
        
		public string HTMLDescription => Submission.description_bbcode_parsed;
        public string ImageURL => Submission.file_url_full;
		public bool Mature => Submission.rating_id == InkbunnyRating.Mature;
		public bool Adult => Submission.rating_id == InkbunnyRating.Adult;
		public IEnumerable<string> Tags => Submission.keywords.Select(k => k.keyword_name);
        public string ThumbnailURL => Submission.thumbnail_url_medium ?? Submission.thumbnail_url_medium_noncustom;
		public DateTime Timestamp => Submission.create_datetime.ToLocalTime().LocalDateTime;
		public string Title => Submission.title;
		public string ViewURL => "https://inkbunny.net/submissionview.php?id=" + Submission.submission_id;
		
        // For user info
        public string Username => Submission.username;
        public string UserIcon => Submission.user_icon_url_small;

        public InkbunnySubmissionWrapper(InkbunnySubmissionDetail submission) {
			Submission = submission;
        }
	}

	public class DeletableInkbunnySubmissionWrapper : InkbunnySubmissionWrapper, IDeletable {
		private readonly InkbunnyClient _client;
		private readonly int _id;

		public DeletableInkbunnySubmissionWrapper(InkbunnyClient client, InkbunnySubmissionDetail submission) : base(submission) {
			_client = client;
			_id = submission.submission_id;
		}

		public string SiteName => "Inkbunny";

		public async Task DeleteAsync() {
			await _client.DeleteSubmissionAsync(_id);
		}
	}
}
