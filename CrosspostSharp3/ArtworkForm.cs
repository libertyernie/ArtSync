﻿using ArtworkSourceSpecification;
using CrosspostSharp3.DeviantArt;
using CrosspostSharp3.FurAffinity;
using CrosspostSharp3.FurryNetwork;
using CrosspostSharp3.Inkbunny;
using CrosspostSharp3.Mastodon;
using CrosspostSharp3.Tumblr;
using CrosspostSharp3.Twitter;
using CrosspostSharp3.Weasyl;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
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
			LoadImage(LocalPhotoPost.FromFile(filename));
		}

		public async void LoadImage(IPostBase artwork) {
			// Store original post object (used for View and Delete actions)
			_origWrapper = artwork;
			btnView.Enabled = _origWrapper.ViewURL != null;

			// Download image (if applicable)
			try {
				_downloaded = await Downloader.DownloadAsync(_origWrapper);
			} catch (WebException ex) {
				MessageBox.Show(this, ex.Message);
				_downloaded = null;
			}

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

			exportAsToolStripMenuItem.Enabled = false;

			if (_downloaded != null) {
				listBox1.Items.Add("--- Post as photo ---");

				exportAsToolStripMenuItem.Enabled = true;

				foreach (var da in settings.DeviantArtTokens) {
					listBox1.Items.Add(new DestinationOption($"DeviantArt / Sta.sh {da.Username}", () => {
						var toPost = _downloaded;
						if (toPost.ContentType == "image/gif") {
							switch (MessageBox.Show(this, "GIF images on DeviantArt require a separate preview image, which isn't possible via the API. Would you like to upload this image in PNG format instead?", Text, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question)) {
								case DialogResult.Cancel:
									return;
								case DialogResult.Yes:
									toPost = new PngRendition(toPost);
									break;
							}
						}
						using (var f = new Form()) {
							f.Width = 600;
							f.Height = 350;
							var d = new DeviantArtUploadControl(da) {
								Dock = DockStyle.Fill
							};
							f.Controls.Add(d);
							d.Uploaded += url => f.Close();

							// Submit original sta.sh item instead of uploading a new one (if unchanged)
							long? originalItemId = toPost == _downloaded && _origWrapper is StashSource.StashPostWrapper sw
								? sw.ItemId
								: null;
							d.SetSubmission(ExportAsText(), toPost, originalItemId);

							f.ShowDialog(this);
						}
					}));
					listBox1.Items.Add(new DestinationOption($"DeviantArt status update ({da.Username})", () => {
						using (var f = new DeviantArtStatusUpdateForm(da, ExportAsText(), _downloaded)) {
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
				foreach (var m in settings.Pleronet) {
					listBox1.Items.Add(new DestinationOption($"{m.AppRegistration.Instance} ({m.Username})", () => {
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
				listBox1.Items.Add("");
			}

			if (_origWrapper is IRemoteVideoPost video) {
				listBox1.Items.Add("--- Post as video ---");
				foreach (var m in settings.Pleronet) {
					listBox1.Items.Add(new DestinationOption($"{m.AppRegistration.Instance} ({m.Username})", () => {
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
			foreach (var m in settings.Pleronet) {
				listBox1.Items.Add(new DestinationOption($"{m.AppRegistration.Instance} ({m.Username})", () => {
					using (var f = new MastodonCwPostForm(m, ExportAsText())) {
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

		private void btnView_Click(object sender, EventArgs e) {
			Process.Start("explorer.exe", _origWrapper.ViewURL);
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
