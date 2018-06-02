﻿using ArtSourceWrapper;
using ArtSourceWrapperFs;
using DontPanic.TumblrSharp;
using DontPanic.TumblrSharp.Client;
using FurryNetworkLib;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CrosspostSharp3 {
	public partial class MainForm : Form {
		private ISiteWrapper _currentWrapper;
		private int _currentPosition = 0;

		private async void Populate() {
			if (_currentWrapper == null) return;

			tableLayoutPanel1.Controls.Clear();

			int i = _currentPosition;
			int stop = _currentPosition + (tableLayoutPanel1.RowCount * tableLayoutPanel1.ColumnCount);

			btnLoad.Enabled = false;
			btnPrevious.Enabled = false;
			btnNext.Enabled = false;

			try {
				await UpdateAvatar();
			} catch (Exception ex) {
				Console.Error.WriteLine($"Could not load avatar: {ex.Message}");
			}

			try {
				while (true) {
					for (; i < stop && i < _currentWrapper.Cache.Count(); i++) {
						var item = _currentWrapper.Cache.Skip(i).First();

						Image image;
						var req = WebRequestFactory.Create(item.ThumbnailURL);
						using (var resp = await req.GetResponseAsync())
						using (var stream = resp.GetResponseStream())
						using (var ms = new MemoryStream()) {
							await stream.CopyToAsync(ms);
							ms.Position = 0;
							image = Image.FromStream(ms);
						}

						var p = new Panel {
							BackgroundImage = image,
							BackgroundImageLayout = ImageLayout.Zoom,
							Cursor = Cursors.Hand,
							Dock = DockStyle.Fill
						};
						p.Click += (o, e) => {
							using (var f = new ArtworkForm(item)) {
								f.ShowDialog(this);
							}
						};
						tableLayoutPanel1.Controls.Add(p);
					}

					if (i == stop) break;

					if (_currentWrapper.IsEnded) break;
					await _currentWrapper.FetchAsync();
				}
			} catch (Exception ex) {
				MessageBox.Show(this, ex.Message, ex.GetType().Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
			}

			btnLoad.Enabled = true;
			btnPrevious.Enabled = _currentPosition > 0;
			btnNext.Enabled = _currentWrapper.Cache.Count() > stop || !_currentWrapper.IsEnded;
		}

		private async Task UpdateAvatar() {
			picUserIcon.Image = null;
			lblUsername.Text = "";
			lblSiteName.Text = "";
			string avatarUrl = await _currentWrapper.GetUserIconAsync(picUserIcon.Width);

			if (avatarUrl != null) {
				var req = WebRequestFactory.Create(avatarUrl);
				using (var resp = await req.GetResponseAsync())
				using (var stream = resp.GetResponseStream())
				using (var ms = new MemoryStream()) {
					await stream.CopyToAsync(ms);
					ms.Position = 0;
					picUserIcon.Image = Image.FromStream(ms);
				}
			}

			lblUsername.Text = await _currentWrapper.WhoamiAsync();
			lblSiteName.Text = _currentWrapper.WrapperName;
		}

		private async Task ReloadWrapperList() {
			ddlSource.Items.Clear();

			var list = new List<ISiteWrapper>();

			lblLoadStatus.Visible = true;
			lblLoadStatus.Text = "Loading settings...";

			var s = Settings.Load();
			if (s.DeviantArt.RefreshToken != null) {
				lblLoadStatus.Text = "Adding DeviantArt...";
				if (await UpdateDeviantArtTokens()) {
					list.Add(new DeviantArtWrapper());
					list.Add(new DeviantArtStatusWrapper());
					list.Add(new StashOrderedWrapper());
				} else {
					MessageBox.Show(this, "DeviantArt refresh token is no longer valid", Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
					s.DeviantArt = new Settings.DeviantArtSettings {
						RefreshToken = null
					};
					s.Save();
				}
			}
			foreach (var fl in s.Flickr) {
				lblLoadStatus.Text = $"Adding Flickr {fl.username}...";
				list.Add(new FlickrWrapper(fl.CreateClient()));
			}
			foreach (var fa in s.FurAffinity) {
				lblLoadStatus.Text = $"Adding FurAffinity {fa.username}...";
				list.Add(new MetaWrapper("FurAffinity", new[] {
					new FurAffinityWrapper(new FurAffinityIdWrapper(
						a: fa.a,
						b: fa.b)),
					new FurAffinityWrapper(new FurAffinityIdWrapper(
						a: fa.a,
						b: fa.b,
						scraps: true))
				}));
			}
			foreach (var fn in s.FurryNetwork) {
				lblLoadStatus.Text = $"Adding Furry Network ({fn.characterName})...";
				var client = new FurryNetworkClient(fn.refreshToken);
				list.Add(new FurryNetworkWrapper(client, fn.characterName));
			}
			foreach (var i in s.Inkbunny) {
				lblLoadStatus.Text = $"Adding Inkbunny {i.username}...";
				list.Add(new InkbunnyWrapper(new InkbunnyLib.InkbunnyClient(i.sid, i.userId)));
			}
			foreach (var t in s.Twitter) {
				lblLoadStatus.Text = $"Adding Twitter ({t.screenName})...";
				list.Add(new TwitterWrapper(t.GetCredentials(), photosOnly: true));
				list.Add(new TwitterWrapper(t.GetCredentials(), photosOnly: false));
			}
			foreach (var m in s.MediaRSS) {
				lblLoadStatus.Text = $"Adding Media RSS feed ({m.name})...";
				list.Add(new MediaRSSWrapper(new Uri(m.url), m.name));
			}
			foreach (var p in s.Pixiv) {
				lblLoadStatus.Text = $"Adding Pixiv ({p.username})...";
				list.Add(new PixivWrapper(p.username, p.password));
			}
			TumblrClientFactory tcf = null;
			foreach (var t in s.Tumblr) {
				if (tcf == null) tcf = new TumblrClientFactory();
				lblLoadStatus.Text = $"Adding Tumblr ({t.blogName})...";
				var client = tcf.Create<TumblrClient>(
					OAuthConsumer.Tumblr.CONSUMER_KEY,
					OAuthConsumer.Tumblr.CONSUMER_SECRET,
					new DontPanic.TumblrSharp.OAuth.Token(t.tokenKey, t.tokenSecret));
				list.Add(new TumblrWrapper(client, t.blogName, photosOnly: true));
				list.Add(new TumblrWrapper(client, t.blogName, photosOnly: false));
			}
			foreach (var w in s.Weasyl) {
				lblLoadStatus.Text = $"Adding Weasyl ({w.username})...";
				list.Add(new MetaWrapper("Weasyl", new[] {
					new WeasylWrapper(new WeasylGalleryIdWrapper(w.apiKey)),
					new WeasylWrapper(new WeasylCharacterWrapper(w.apiKey))
				}));
			}

			foreach (var wrapper in list) {
				if (!wrapper.SubmissionsFiltered) {
					wrapper.BatchSize = Math.Max(wrapper.MinBatchSize, Math.Min(wrapper.MaxBatchSize, 4));
				}
			}
			
			lblLoadStatus.Text = "Connecting to sites...";

			var tasks = list.Select(async w => {
				try {
					return new WrapperMenuItem(w, $"{await w.WhoamiAsync()} - {w.WrapperName}");
				} catch (FurryNetworkClient.TokenException ex) {
					return new WrapperMenuItem(w, $"{w.WrapperName} (cannot connect: {ex.Message})");
				} catch (Exception) {
					return new WrapperMenuItem(w, $"{w.WrapperName} (cannot connect)");
				}
			}).Where(item => item != null).ToArray();
			var wrappers = await Task.WhenAll(tasks);
			wrappers = wrappers
				.OrderBy(w => new string(w.DisplayName.Where(c => char.IsLetterOrDigit(c)).ToArray()))
				.ToArray();
			ddlSource.Items.AddRange(wrappers);

			lblLoadStatus.Visible = false;

			if (ddlSource.SelectedIndex < 0 && ddlSource.Items.Count > 0) {
				ddlSource.SelectedIndex = 0;
			}

			btnLoad.Enabled = ddlSource.Items.Count > 0;
		}

		public MainForm() {
			InitializeComponent();
		}

		private async void Form1_Shown(object sender, EventArgs e) {
			try {
				await ReloadWrapperList();
			} catch (Exception) {
				MessageBox.Show(this, "Could not load all source sites", Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
				lblLoadStatus.Visible = false;
			}
		}

		private void btnLoad_Click(object sender, EventArgs e) {
			_currentWrapper = (ddlSource.SelectedItem as WrapperMenuItem)?.BaseWrapper;
			_currentPosition = 0;
			Populate();
		}

		private void btnPrevious_Click(object sender, EventArgs e) {
			_currentPosition = Math.Max(0, _currentPosition - 4);
			Populate();
		}

		private void btnNext_Click(object sender, EventArgs e) {
			_currentPosition += tableLayoutPanel1.RowCount + tableLayoutPanel1.ColumnCount;
			Populate();
		}

		private void openToolStripMenuItem_Click(object sender, EventArgs e) {
			using (var openFileDialog = new OpenFileDialog()) {
				openFileDialog.Filter = ArtworkForm.OpenFilter;
				openFileDialog.Multiselect = false;
				if (openFileDialog.ShowDialog() == DialogResult.OK) {
					using (var f = new ArtworkForm(openFileDialog.FileName)) {
						f.ShowDialog(this);
					}
				}
			}
		}
		
		private void exitToolStripMenuItem_Click(object sender, EventArgs e) {
			Application.Exit();
		}

		private void refreshAllToolStripMenuItem_Click(object sender, EventArgs e) {
			foreach (var w in GetWrappers()) {
				w.Clear();
			}
			Populate();
		}

		private void helpToolStripMenuItem1_Click(object sender, EventArgs e) {
			Process.Start("https://github.com/libertyernie/CrosspostSharp/blob/v3.0/README.md");
		}

		private void aboutToolStripMenuItem_Click(object sender, EventArgs e) {
			using (var f = new AboutForm()) {
				f.ShowDialog(this);
			}
		}

		private IEnumerable<ISiteWrapper> GetWrappers() {
			foreach (var o in ddlSource.Items) {
				if (o is WrapperMenuItem w) yield return w.BaseWrapper;
			}
		}

		private void exportToolStripMenuItem_Click_1(object sender, EventArgs e) {
			using (var f = new BatchExportForm(GetWrappers())) {
				f.ShowDialog(this);
			}
		}
	}
}
