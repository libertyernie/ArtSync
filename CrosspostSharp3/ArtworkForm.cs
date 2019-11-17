﻿using CrosspostSharp3.Imgur;
using CrosspostSharp3.Weasyl;
using Imgur.API.Authentication.Impl;
using Imgur.API.Endpoints.Impl;
using Newtonsoft.Json;
using SourceWrappers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CrosspostSharp3 {
	public partial class ArtworkForm : Form {
		private IPostBase _origWrapper;
		private IDownloadedData _downloaded;

		private class DestinationOption {
			public readonly string Name;
			public readonly Action Click;

			public DestinationOption(string name, Action click) {
				Name = name;
				Click = click;
			}

			public override string ToString() {
				return Name;
			}
		}

		public TextPost ExportAsText() {
			return new TextPost {
				Title = txtTitle.Text,
				HTMLDescription = wbrDescription.Document.Body.InnerHtml,
				Tags = txtTags.Text.Split(' ').Where(s => s != ""),
				Mature = chkMature.Checked,
				Adult = chkAdult.Checked
			};
		}

		public ArtworkForm() {
			InitializeComponent();

			Shown += (o, e) => {
				if (Owner is MainForm) {
					mainWindowAccountSetupToolStripMenuItem.Enabled = false;
				}
			};
		}

		public ArtworkForm(string filename) : this() {
			this.Shown += (o, e) => LoadImage(filename);
		}

		public ArtworkForm(IPostBase post) : this() {
			this.Shown += (o, e) => LoadImage(post);
		}

		public void LoadImage(string filename) {
			LoadImage(SavedPhotoPost.FromFile(filename));
		}

		public async void LoadImage(IPostBase artwork) {
			// Store original post object (used for View and Delete actions)
			_origWrapper = artwork;
			btnView.Enabled = _origWrapper.ViewURL != null;
			btnDelete.Enabled = _origWrapper is IDeletable;

			// Convert DeferredPhotoPost to IRemotePhotoPost
			if (_origWrapper is DeferredPhotoPost deferred) {
				for (int i = 0; i < Controls.Count; i++) {
					Controls[i].Enabled = false;
				}
				string origText = this.Text;
				this.Text = "Loading...";

				try {
					_origWrapper = await deferred.GetActualAsync();
				} catch (Exception) { }

				if (this.IsDisposed) return;

				this.Text = origText;
				for (int i = 0; i < Controls.Count; i++) {
					Controls[i].Enabled = true;
				}
			}

			// Download image (if applicable)
			_downloaded = await Downloader.DownloadAsync(_origWrapper);

			// Get photo (or thumbnail of video)
			if (_downloaded != null) {
				using (var ms = new MemoryStream(_downloaded.Data, false)) {
					var image = Image.FromStream(ms);
					pictureBox1.Image = image;
				}
			} else if (_origWrapper is IThumbnailPost thumb) {
				pictureBox1.ImageLocation = thumb.ThumbnailURL;
			}

			btnView.Enabled = _origWrapper.ViewURL != null;
			saveAsToolStripMenuItem.Enabled = exportAsToolStripMenuItem.Enabled = (_origWrapper is SavedPhotoPost x && x.url != null);
			txtTitle.Text = _origWrapper.Title;
			wbrDescription.Navigate("about:blank");
			string html = _origWrapper.HTMLDescription;
			wbrDescription.Document.Write($"<html><head></head><body>{html}</body></html>");
			wbrDescription.Document.Body.SetAttribute("contenteditable", "true");
			txtTags.Text = string.Join(" ", _origWrapper.Tags);
			chkMature.Checked = _origWrapper.Mature;
			chkAdult.Checked = _origWrapper.Adult;
			lblDateTime.Text = $"{_origWrapper.Timestamp} ({_origWrapper.Timestamp.Kind})";

			try {
				MakeLinksAbsolute();
			} catch (Exception ex) {
				Console.Error.WriteLine(ex);
			}

			Settings settings = Settings.Load();

			saveAsToolStripMenuItem.Enabled = false;
			exportAsToolStripMenuItem.Enabled = false;

			if (_downloaded != null) {
				listBox1.Items.Add("--- Post as photo ---");

				saveAsToolStripMenuItem.Enabled = true;
				exportAsToolStripMenuItem.Enabled = true;

				foreach (var da in settings.DeviantArtTokens) {
					listBox1.Items.Add(new DestinationOption($"DeviantArt / Sta.sh {da.Username}", () => {
						long? itemId = (_origWrapper as StashPostWrapper)?.ItemId;
						var toPost = _downloaded;
						using (var f = new Form()) {
							f.Width = 600;
							f.Height = 350;
							var d = new DeviantArtUploadControl(da) {
								Dock = DockStyle.Fill
							};
							f.Controls.Add(d);
							d.Uploaded += url => f.Close();
							d.SetSubmission(ExportAsText(), toPost, itemId);
							f.ShowDialog(this);
						}
					}));
					listBox1.Items.Add(new DestinationOption($"DeviantArt status update ({da.Username})", () => {
						using (var f = new DeviantArtStatusUpdateForm(da, ExportAsText(), _downloaded)) {
							f.ShowDialog(this);
						}
					}));
				}
				foreach (var fl in settings.Flickr) {
					listBox1.Items.Add(new DestinationOption($"Flickr ({fl.username})", () => {
						using (var f = new FlickrPostForm(fl, ExportAsText(), _downloaded)) {
							f.ShowDialog(this);
						}
					}));
				}
				foreach (var fa in settings.FurAffinity) {
					listBox1.Items.Add(new DestinationOption($"Fur Affinity ({fa.username})", () => {
						using (var f = new FurAffinityPostForm(fa, ExportAsText(), _downloaded)) {
							f.ShowDialog(this);
						}
					}));
				}
				foreach (var fn in settings.FurryNetwork) {
					listBox1.Items.Add(new DestinationOption($"Furry Network ({fn.characterName})", () => {
						using (var f = new FurryNetworkPostForm(fn, ExportAsText(), _downloaded)) {
							f.ShowDialog(this);
						}
					}));
				}
				foreach (var i in settings.Inkbunny) {
					listBox1.Items.Add(new DestinationOption($"Inkbunny ({i.username})", () => {
						using (var f = new InkbunnyPostForm(i, ExportAsText(), _downloaded)) {
							f.ShowDialog(this);
						}
					}));
				}
				foreach (var m in settings.Mastodon) {
					listBox1.Items.Add(new DestinationOption($"{m.Instance} ({m.username})", () => {
						using (var f = new MastodonCwPostForm(m, ExportAsText(), _downloaded)) {
							f.ShowDialog(this);
						}
					}));
				}
				foreach (var p in settings.Pillowfort) {
					listBox1.Items.Add(new DestinationOption($"Pillowfort ({p.username})", () => {
						using (var f = new PillowfortPostForm(p, ExportAsText(), _downloaded)) {
							f.ShowDialog(this);
						}
					}));
				}
				foreach (var p in settings.PixivUpload) {
					Settings.IAccountCredentials s = p;
					listBox1.Items.Add(new DestinationOption($"Pixiv ({s.Username})", () => {
						using (var f = new PixivPostForm(p, ExportAsText(), _downloaded)) {
							f.ShowDialog(this);
						}
					}));
				}
				foreach (var t in settings.Twitter) {
					listBox1.Items.Add(new DestinationOption($"Twitter ({t.screenName})", () => {
						using (var f = new TwitterPostForm(t, ExportAsText(), _downloaded)) {
							f.ShowDialog(this);
						}
					}));
				}
				foreach (var t in settings.Tumblr) {
					listBox1.Items.Add(new DestinationOption($"Tumblr ({t.blogName})", () => {
						using (var f = new TumblrPostForm(t, ExportAsText(), _downloaded)) {
							f.ShowDialog(this);
						}
					}));
				}
				foreach (var w in settings.WeasylApi) {
					if (w.apiKey == null) continue;
					listBox1.Items.Add(new DestinationOption($"Weasyl ({w.username})", () => {
						using (var f = new WeasylPostForm(w, ExportAsText(), _downloaded)) {
							f.ShowDialog(this);
						}
					}));
				}
				listBox1.Items.Add(new DestinationOption($"Imgur (anonymous upload)", async () => {
					if (MessageBox.Show(this, "Would you like to upload this image to Imgur?", Text, MessageBoxButtons.OKCancel) == DialogResult.OK) {
						try {
							var image = await ImgurAnonymousUpload.UploadAsync(_downloaded.Data,
								title: txtTitle.Text,
								description: wbrDescription.Document.Body.InnerHtml);
							Process.Start(image);
						} catch (Exception ex) {
							Console.Error.WriteLine(ex);
							MessageBox.Show(this, "Could not upload to Imgur (an unknown error occured.)");
						}
					}
				}));
				listBox1.Items.Add("");
			}

			if (_origWrapper is IRemoteVideoPost video) {
				listBox1.Items.Add("--- Post as video ---");
				foreach (var m in settings.Mastodon) {
					listBox1.Items.Add(new DestinationOption($"{m.Instance} ({m.username})", () => {
						using (var f = new MastodonCwPostForm(m, ExportAsText(), _downloaded)) {
							f.ShowDialog(this);
						}
					}));
				}
				foreach (var t in settings.Twitter) {
					listBox1.Items.Add(new DestinationOption($"Twitter ({t.screenName})", () => {
						using (var f = new TwitterPostForm(t, ExportAsText(), _downloaded)) {
							f.ShowDialog(this);
						}
					}));
				}
				listBox1.Items.Add("");
			}

			listBox1.Items.Add("--- Post as text ---");

			foreach (var da in settings.DeviantArtTokens) {
				listBox1.Items.Add(new DestinationOption($"DeviantArt status update ({da.Username})", () => {
					using (var f = new DeviantArtStatusUpdateForm(da, ExportAsText())) {
						f.ShowDialog(this);
					}
				}));
			}
			foreach (var m in settings.Mastodon) {
				listBox1.Items.Add(new DestinationOption($"{m.Instance} ({m.username})", () => {
					using (var f = new MastodonCwPostForm(m, ExportAsText())) {
						f.ShowDialog(this);
					}
				}));
			}
			foreach (var p in settings.Pillowfort) {
				listBox1.Items.Add(new DestinationOption($"Pillowfort ({p.username})", () => {
					using (var f = new PillowfortPostForm(p, ExportAsText())) {
						f.ShowDialog(this);
					}
				}));
			}
			foreach (var t in settings.Twitter) {
				listBox1.Items.Add(new DestinationOption($"Twitter ({t.screenName})", () => {
					using (var f = new TwitterPostForm(t, ExportAsText())) {
						f.ShowDialog(this);
					}
				}));
			}
			foreach (var t in settings.Tumblr) {
				listBox1.Items.Add(new DestinationOption($"Tumblr ({t.blogName})", () => {
					using (var f = new TumblrPostForm(t, ExportAsText(), _downloaded)) {
						f.ShowDialog(this);
					}
				}));
			}
		}

		private void btnPost_Click(object sender, EventArgs ea) {
			var o = listBox1.SelectedItem as DestinationOption;
			o?.Click?.Invoke();
		}

		public const string OpenFilter = "All supported formats|*.png;*.jpg;*.jpeg;*.gif;*.cps|Image files|*.png;*.jpg;*.jpeg;*.gif|CrosspostSharp JSON|*.cps|All files|*.*";

		private void openToolStripMenuItem_Click(object sender, EventArgs e) {
			using (var openFileDialog = new OpenFileDialog()) {
				openFileDialog.Filter = OpenFilter;
				openFileDialog.Multiselect = false;
				if (openFileDialog.ShowDialog() == DialogResult.OK) {
					LoadImage(openFileDialog.FileName);
				}
			}
		}

		private void saveAsToolStripMenuItem_Click(object sender, EventArgs e) {
			if (_downloaded == null) {
				MessageBox.Show(this, "This post does not have image data.", Text);
				return;
			}

			var cps = new SavedPhotoPost(
				_downloaded.Data,
				txtTitle.Text,
				wbrDescription.Document.Body.InnerHtml,
				_origWrapper.ViewURL,
				txtTags.Text.Split(' ').Where(s => s != ""),
				chkMature.Checked,
				chkAdult.Checked);

			using (var saveFileDialog = new SaveFileDialog()) {
				saveFileDialog.Filter = "CrosspostSharp JSON|*.cps|All files|*.*";
				if (saveFileDialog.ShowDialog() == DialogResult.OK) {
					File.WriteAllText(saveFileDialog.FileName, JsonConvert.SerializeObject(cps, Formatting.Indented));
				}
			}
		}

		private void exportAsToolStripMenuItem_Click(object sender, EventArgs e) {
			if (_downloaded == null) {
				MessageBox.Show(this, "This post does not have image data.", Text);
				return;
			}

			using (var saveFileDialog = new SaveFileDialog()) {
				using (var ms = new MemoryStream(_downloaded.Data, false))
				using (var image = Image.FromStream(ms)) {
					saveFileDialog.Filter = image.RawFormat.Equals(ImageFormat.Png) ? "PNG images|*.png"
						: image.RawFormat.Equals(ImageFormat.Jpeg) ? "JPEG images|*.jpg;*.jpeg"
						: image.RawFormat.Equals(ImageFormat.Gif) ? "GIF images|*.gif"
						: "All files|*.*";
				}
				if (saveFileDialog.ShowDialog() == DialogResult.OK) {
					File.WriteAllBytes(saveFileDialog.FileName, _downloaded.Data);
				}
			}
		}

		private void closeToolStripMenuItem_Click(object sender, EventArgs e) {
			Close();
		}

		private void exitToolStripMenuItem_Click(object sender, EventArgs e) {
			Application.Exit();
		}

		private void listBox1_DoubleClick(object sender, EventArgs e) {
			btnPost.PerformClick();
		}

		private async void btnDelete_Click(object sender, EventArgs e) {
			if (_origWrapper is IDeletable d) {
				if (MessageBox.Show(this, $"Are you sure you want to permanently delete this submission from {d.SiteName}?", "Delete Item", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) == DialogResult.OK) {
					try {
						await d.DeleteAsync();
						Close();
					} catch (Exception ex) {
						MessageBox.Show(this, ex.Message, ex.GetType().Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
					}
				}
			}
		}

		private void btnView_Click(object sender, EventArgs e) {
			Process.Start(_origWrapper.ViewURL);
		}

		private void helpToolStripMenuItem1_Click(object sender, EventArgs e) {
			Process.Start("https://github.com/libertyernie/CrosspostSharp/blob/v3.6/README.md");
		}

		private void aboutToolStripMenuItem_Click(object sender, EventArgs e) {
			using (var f = new AboutForm()) {
				f.ShowDialog(this);
			}
		}

		private void mainWindowAccountSetupToolStripMenuItem_Click(object sender, EventArgs e) {
			using (var f = new MainForm()) {
				f.ShowDialog(this);
			}
		}

		private void MakeLinksAbsolute() {
			if (!Uri.TryCreate(_origWrapper.ViewURL, UriKind.Absolute, out Uri u1))
				return;

			var links = wbrDescription.Document.GetElementsByTagName("a");
			for (int i = 0; i < links.Count; i++) {
				var a = links[i];
				string href = a.GetAttribute("href");
				if (href.StartsWith("about:"))
					href = href.Substring(6);
				if (Uri.TryCreate(href, UriKind.RelativeOrAbsolute, out Uri u2)) {
					a.SetAttribute("href", new Uri(u1, u2).AbsoluteUri);
				}
			}

			var images = wbrDescription.Document.GetElementsByTagName("img");
			for (int i = 0; i < images.Count; i++) {
				var img = images[i];
				string src = img.GetAttribute("src");
				if (src.StartsWith("about:"))
					src = src.Substring(6);
				if (Uri.TryCreate(src, UriKind.RelativeOrAbsolute, out Uri u2)) {
					img.SetAttribute("src", new Uri(u1, u2).AbsoluteUri);
				}
			}
		}
	}
}
