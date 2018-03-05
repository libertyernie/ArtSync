﻿using ArtSourceWrapper;
using DeviantArtControls;
using Newtonsoft.Json;
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
		private byte[] _data;
		private ISubmissionWrapper _originalWrapper;

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

		public struct ArtworkData {
			public byte[] data;
			public string title, description, url;
			public IEnumerable<string> tags;
			public bool mature;

			public ArtworkData(ArtworkForm f) {
				data = f._data;
				title = f.txtTitle.Text;
				description = f.txtDescription.Text;
				url = f._originalWrapper?.ViewURL;
				tags = f.txtTags.Text.Split(' ').Where(s => s != "");
				mature = f.chkPotentiallySensitiveMaterial.Checked;
			}
		}

		public ArtworkForm() {
			InitializeComponent();

			Settings settings = Settings.Load();
			if (settings.DeviantArt?.RefreshToken != null) {
				listBox1.Items.Add(new DestinationOption("DeviantArt / Sta.sh", () => {
					using (var f = new Form()) {
						f.Width = 600;
						f.Height = 350;
						var d = new DeviantArtUploadControl {
							Dock = DockStyle.Fill
						};
						f.Controls.Add(d);
						d.Uploaded += url => f.Close();
						d.SetSubmission(
							_data,
							txtTitle.Text,
							txtDescription.Text,
							txtTags.Text.Split(' ').Where(s => s != ""),
							chkPotentiallySensitiveMaterial.Checked,
							_originalWrapper?.ViewURL);
						f.ShowDialog(this);
					}
				}));
			}
			foreach (var t in settings.Twitter) {
				listBox1.Items.Add(new DestinationOption($"Twitter ({t.Username})", () => {
					using (var f = new TwitterPostForm(t, new ArtworkData(this))) {
						f.ShowDialog(this);
					}
				}));
			}
			if (File.Exists("efc.jar")) {
				listBox1.Items.Add(new DestinationOption($"FurAffinity / Weasyl", () => {
					LaunchEFC(new ArtworkData(this));
				}));
			}
		}

		public ArtworkForm(byte[] data) : this() {
			this.Shown += (o, e) => LoadImage(data);
		}

		public ArtworkForm(ISubmissionWrapper wrapper) : this() {
			this.Shown += (o, e) => LoadImage(wrapper);
		}

		public void LoadImage(byte[] data) {
			_data = data.ToArray();
			using (var ms = new MemoryStream(_data, false)) {
				var image = Image.FromStream(ms);
				splitContainer1.Panel1.BackgroundImage = image;
				splitContainer1.Panel1.BackgroundImageLayout = ImageLayout.Zoom;
			}
			txtTitle.Text = "";
			txtDescription.Text = "";
			txtTags.Text = "";
			_originalWrapper = null;
		}

		public async void LoadImage(ISubmissionWrapper wrapper) {
			try {
				var req = WebRequestFactory.Create(wrapper.ImageURL);
				using (var resp = await req.GetResponseAsync())
				using (var stream = resp.GetResponseStream())
				using (var ms = new MemoryStream()) {
					await stream.CopyToAsync(ms);
					LoadImage(ms.ToArray());
				}
				txtTitle.Text = wrapper.Title;
				txtDescription.Text = wrapper.HTMLDescription;
				txtTags.Text = string.Join(" ", wrapper.Tags);
				_originalWrapper = wrapper;
				btnDelete.Enabled = _originalWrapper is IDeletable;
				btnView.Enabled = true;
			} catch (Exception ex) {
				splitContainer1.Panel1.Controls.Add(new TextBox {
					Text = ex.Message + Environment.NewLine + ex.StackTrace,
					Multiline = true,
					Dock = DockStyle.Fill,
					ReadOnly = true
				});
			}
		}

		private static void LaunchEFC(ArtworkData artwork) {
			string jsonFile = null, imageFile = null;
			char[] invalid = Path.GetInvalidFileNameChars();
			string basename = artwork.title;
			if (string.IsNullOrEmpty(basename)) {
				basename = "image";
			}
			basename = new string(basename.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
			string ext = "";
			try {
				using (var ms = new MemoryStream(artwork.data, false)) {
					var image = Image.FromStream(ms);
					if (image.RawFormat.Guid == ImageFormat.Png.Guid) {
						ext = ".png";
					} else if (image.RawFormat.Guid == ImageFormat.Jpeg.Guid) {
						ext = ".jpg";
					} else if (image.RawFormat.Guid == ImageFormat.Gif.Guid) {
						ext = ".gif";
					}
				}
			} catch (Exception) { }
			string imageFilename = basename + ext;

			imageFile = Path.Combine(Path.GetTempPath(), imageFilename);
			File.WriteAllBytes(imageFile, artwork.data);
			
			jsonFile = Path.GetTempFileName();
			File.WriteAllText(jsonFile, JsonConvert.SerializeObject(new {
				imagePath = imageFile,
				title = artwork.title,
				description = HtmlConversion.ConvertHtmlToText(artwork.description),
				tags = artwork.tags,
				nudity = new {
					@explicit = artwork.mature
				}
			}));

			Process process = Process.Start(new ProcessStartInfo("java", $"-jar efc.jar {jsonFile}") {
				RedirectStandardError = true,
				UseShellExecute = false,
				WorkingDirectory = Environment.CurrentDirectory,
				CreateNoWindow = true
			});
			process.EnableRaisingEvents = true;
			process.Exited += (o, a) => {
				if (process.ExitCode != 0) {
					string stderr = process.StandardError.ReadToEnd();
					MessageBox.Show(null, stderr, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
				}

				if (jsonFile != null) File.Delete(jsonFile);
				if (imageFile != null) File.Delete(imageFile);
			};
		}

		private void btnPost_Click(object sender, EventArgs ea) {
			var o = listBox1.SelectedItem as DestinationOption;
			o?.Click?.Invoke();
		}

		private void lnkPreview_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e) {
			using (var f = new Form()) {
				f.Text = "Post to DeviantArt";
				f.Width = 600;
				f.Height = 350;
				var w = new WebBrowser {
					Dock = DockStyle.Fill
				};
				f.Controls.Add(w);
				w.Navigate("about:blank");
				w.Document.Write(txtDescription.Text);
				f.ShowDialog(this);
			}
		}

		private void openToolStripMenuItem_Click(object sender, EventArgs e) {
			using (var openFileDialog = new OpenFileDialog()) {
				openFileDialog.Filter = "Image files|*.png;*.jpg;*.jpeg;*.gif";
				openFileDialog.Multiselect = false;
				if (openFileDialog.ShowDialog() == DialogResult.OK) {
					LoadImage(File.ReadAllBytes(openFileDialog.FileName));
				}
			}
		}

		private void exportAsToolStripMenuItem_Click(object sender, EventArgs e) {
			using (var saveFileDialog = new SaveFileDialog()) {
				using (var ms = new MemoryStream(_data, false))
				using (var image = Image.FromStream(ms)) {
					saveFileDialog.Filter = image.RawFormat.Equals(ImageFormat.Png) ? "PNG images|*.png"
						: image.RawFormat.Equals(ImageFormat.Jpeg) ? "JPEG images|*.jpg;*.jpeg"
						: image.RawFormat.Equals(ImageFormat.Gif) ? "GIF images|*.gif"
						: "All files|*.*";
				}
				if (saveFileDialog.ShowDialog() == DialogResult.OK) {
					File.WriteAllBytes(saveFileDialog.FileName, _data);
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
			if (_originalWrapper is IDeletable d) {
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
			if (_originalWrapper?.ViewURL != null) {
				System.Diagnostics.Process.Start(_originalWrapper.ViewURL);
			}
		}
	}
}
