﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CrosspostSharp3 {
    public partial class DeviantArtCategoryBrowser : Form {
        public class Category {
            public string CategoryPath;
            public IEnumerable<string> NamePath;
        }

        public Category InitialCategory { get; set; }

        public Category SelectedCategory => new Category {
            CategoryPath = treeView1.SelectedNode.Name,
            NamePath = GetReverseNamePath(treeView1.SelectedNode).Reverse()
        };

        public DeviantArtCategoryBrowser() {
            InitializeComponent();
        }

        private async Task SetCategoryAsync(Category category) {
            if (category == null) {
                treeView1.SelectedNode = null;
                return;
            }
            
            string categoryPath = category.CategoryPath;
            System.Diagnostics.Debug.WriteLine($"Looking for {categoryPath}");

            TreeNode existing = treeView1.Nodes.Find(categoryPath, true).FirstOrDefault();
            if (existing != null) {
                System.Diagnostics.Debug.WriteLine($"Found {existing.Name}");
                treeView1.SelectedNode = existing;
                return;
            }

            for (int i = categoryPath.Length - 1; i > 0; i--) {
                existing = treeView1.Nodes.Find(categoryPath.Substring(0, i), true).FirstOrDefault();
                if (existing != null) {
                    System.Diagnostics.Debug.WriteLine($"Populating {existing.Name}");
                    await PopulateAsync(existing.Nodes, existing.Name);
                    existing.Expand();
                    await SetCategoryAsync(category);
                    return;
                }
            }

            throw new Exception("Category not found: " + categoryPath);
        }

        private IEnumerable<string> GetReverseNamePath(TreeNode node) {
            while (node != null) {
                yield return node.Text;
                node = node.Parent;
            }
        }

        private async Task PopulateAsync(TreeNodeCollection nodes, string path = null) {
			throw new NotImplementedException();
			//var result = await new CategoryTreeRequest {
            //    Catpath = path ?? "/"
            //}.ExecuteAsync();
            //if (result.IsError) {
            //    throw new Exception("The list of categories could not be loaded. You probably need to log in with one of the methods in DeviantartApi.Login.");
            //}
            //if (!string.IsNullOrEmpty(result.Result.Error)) {
            //    throw new Exception(result.Result.ErrorDescription);
            //}
            //foreach (var c in result.Result.Categories) {
            //    TreeNode node = nodes.Add(c.Catpath, c.Title);
            //    if (c.HasSubcategory) {
            //        node.Nodes.Add("loading", "Loading...");
            //    }
            //}
        }

        private async void DeviantArtCategoryBrowser_Load(object sender, EventArgs e) {
            try {
                this.Enabled = false;

                treeView1.Nodes.Add("", "None");
                await PopulateAsync(treeView1.Nodes);

                if (InitialCategory != null) {
                    await SetCategoryAsync(InitialCategory);
                }

                this.Enabled = true;
            } catch (Exception ex) {
                MessageBox.Show(this.ParentForm, ex.Message, $"{this.GetType().Name}: {ex.GetType().Name}");
                this.Close();
            }
        }

        private async void treeView1_BeforeExpand(object sender, TreeViewCancelEventArgs e) {
            try {
                TreeNode target = e.Node;
                if (target.Nodes?.ContainsKey("loading") == true) {
                    target.Nodes.Clear();
                    await PopulateAsync(target.Nodes, target.Name);
                    target.Expand();
                }
            } catch (Exception ex) {
                MessageBox.Show(this.ParentForm, ex.Message, $"{this.GetType().Name}: {ex.GetType().Name}");
            }
        }
    }
}
