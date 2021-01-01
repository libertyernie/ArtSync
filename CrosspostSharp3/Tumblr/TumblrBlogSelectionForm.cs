﻿using DontPanic.TumblrSharp.Client;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CrosspostSharp3.Tumblr {
	public partial class TumblrBlogSelectionForm : Form {
		public IEnumerable<UserBlogInfo> SelectedItems {
			get {
				foreach (object o in listBox1.SelectedItems) {
					if (o is UserBlogInfo i) yield return i;
				}
			}
		}

		public TumblrBlogSelectionForm(
			IEnumerable<UserBlogInfo> list
		) {
			InitializeComponent();
			foreach (var o in list) {
				listBox1.Items.Add(o);
			}
			if (listBox1.Items.Count > 0) {
				listBox1.SelectedIndex = 0;
			}
		}
	}
}
