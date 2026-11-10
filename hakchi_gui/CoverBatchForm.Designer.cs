namespace com.clusterrr.hakchi_gui
{
    partial class CoverBatchForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(CoverBatchForm));
            this.panelTop = new System.Windows.Forms.Panel();
            this.checkBoxIncludeExisting = new System.Windows.Forms.CheckBox();
            this.buttonSearch = new System.Windows.Forms.Button();
            this.buttonCancel = new System.Windows.Forms.Button();
            this.progressBar = new System.Windows.Forms.ProgressBar();
            this.labelStatus = new System.Windows.Forms.Label();
            this.listViewResults = new System.Windows.Forms.ListView();
            this.columnGame = new System.Windows.Forms.ColumnHeader();
            this.columnSource = new System.Windows.Forms.ColumnHeader();
            this.columnMatch = new System.Windows.Forms.ColumnHeader();
            this.columnState = new System.Windows.Forms.ColumnHeader();
            this.panelAlternatives = new System.Windows.Forms.Panel();
            this.labelAlternatives = new System.Windows.Forms.Label();
            this.flowAlternatives = new System.Windows.Forms.FlowLayoutPanel();
            this.panelBottom = new System.Windows.Forms.Panel();
            this.labelSummary = new System.Windows.Forms.Label();
            this.buttonApply = new System.Windows.Forms.Button();
            this.buttonClose = new System.Windows.Forms.Button();
            this.panelTop.SuspendLayout();
            this.panelAlternatives.SuspendLayout();
            this.panelBottom.SuspendLayout();
            this.SuspendLayout();
            //
            // panelTop
            //
            this.panelTop.Controls.Add(this.checkBoxIncludeExisting);
            this.panelTop.Controls.Add(this.buttonSearch);
            this.panelTop.Controls.Add(this.buttonCancel);
            this.panelTop.Controls.Add(this.progressBar);
            this.panelTop.Controls.Add(this.labelStatus);
            this.panelTop.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelTop.Name = "panelTop";
            this.panelTop.Size = new System.Drawing.Size(900, 92);
            //
            // checkBoxIncludeExisting
            //
            this.checkBoxIncludeExisting.AutoSize = true;
            this.checkBoxIncludeExisting.Location = new System.Drawing.Point(12, 10);
            this.checkBoxIncludeExisting.Name = "checkBoxIncludeExisting";
            this.checkBoxIncludeExisting.Size = new System.Drawing.Size(300, 17);
            this.checkBoxIncludeExisting.TabIndex = 0;
            this.checkBoxIncludeExisting.CheckedChanged += new System.EventHandler(this.checkBoxIncludeExisting_CheckedChanged);
            //
            // buttonSearch
            //
            this.buttonSearch.Location = new System.Drawing.Point(12, 36);
            this.buttonSearch.Name = "buttonSearch";
            this.buttonSearch.Size = new System.Drawing.Size(110, 26);
            this.buttonSearch.TabIndex = 1;
            this.buttonSearch.UseVisualStyleBackColor = true;
            this.buttonSearch.Click += new System.EventHandler(this.buttonSearch_Click);
            //
            // buttonCancel
            //
            this.buttonCancel.Enabled = false;
            this.buttonCancel.Location = new System.Drawing.Point(128, 36);
            this.buttonCancel.Name = "buttonCancel";
            this.buttonCancel.Size = new System.Drawing.Size(110, 26);
            this.buttonCancel.TabIndex = 2;
            this.buttonCancel.UseVisualStyleBackColor = true;
            this.buttonCancel.Click += new System.EventHandler(this.buttonCancel_Click);
            //
            // progressBar
            //
            this.progressBar.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.progressBar.Location = new System.Drawing.Point(250, 40);
            this.progressBar.Name = "progressBar";
            this.progressBar.Size = new System.Drawing.Size(638, 18);
            this.progressBar.TabIndex = 3;
            //
            // labelStatus
            //
            this.labelStatus.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.labelStatus.Location = new System.Drawing.Point(12, 68);
            this.labelStatus.Name = "labelStatus";
            this.labelStatus.Size = new System.Drawing.Size(876, 18);
            this.labelStatus.TabIndex = 4;
            //
            // listViewResults
            //
            this.listViewResults.CheckBoxes = true;
            this.listViewResults.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnGame,
            this.columnSource,
            this.columnMatch,
            this.columnState});
            this.listViewResults.Dock = System.Windows.Forms.DockStyle.Fill;
            this.listViewResults.FullRowSelect = true;
            this.listViewResults.HideSelection = false;
            this.listViewResults.MultiSelect = false;
            this.listViewResults.Name = "listViewResults";
            this.listViewResults.TabIndex = 5;
            this.listViewResults.UseCompatibleStateImageBehavior = false;
            this.listViewResults.View = System.Windows.Forms.View.Details;
            this.listViewResults.ItemChecked += new System.Windows.Forms.ItemCheckedEventHandler(this.listViewResults_ItemChecked);
            this.listViewResults.SelectedIndexChanged += new System.EventHandler(this.listViewResults_SelectedIndexChanged);
            //
            // columns
            //
            this.columnGame.Width = 330;
            this.columnSource.Width = 230;
            this.columnMatch.Width = 140;
            this.columnState.Width = 160;
            //
            // panelAlternatives
            //
            this.panelAlternatives.Controls.Add(this.flowAlternatives);
            this.panelAlternatives.Controls.Add(this.labelAlternatives);
            this.panelAlternatives.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panelAlternatives.Name = "panelAlternatives";
            this.panelAlternatives.Size = new System.Drawing.Size(900, 124);
            //
            // labelAlternatives
            //
            this.labelAlternatives.Dock = System.Windows.Forms.DockStyle.Top;
            this.labelAlternatives.Name = "labelAlternatives";
            this.labelAlternatives.Padding = new System.Windows.Forms.Padding(12, 4, 0, 0);
            this.labelAlternatives.Size = new System.Drawing.Size(900, 20);
            //
            // flowAlternatives
            //
            this.flowAlternatives.AutoScroll = true;
            this.flowAlternatives.Dock = System.Windows.Forms.DockStyle.Fill;
            this.flowAlternatives.Name = "flowAlternatives";
            this.flowAlternatives.Padding = new System.Windows.Forms.Padding(8, 0, 0, 0);
            this.flowAlternatives.WrapContents = false;
            //
            // panelBottom
            //
            this.panelBottom.Controls.Add(this.labelSummary);
            this.panelBottom.Controls.Add(this.buttonApply);
            this.panelBottom.Controls.Add(this.buttonClose);
            this.panelBottom.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panelBottom.Name = "panelBottom";
            this.panelBottom.Size = new System.Drawing.Size(900, 44);
            //
            // labelSummary
            //
            this.labelSummary.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.labelSummary.Location = new System.Drawing.Point(12, 14);
            this.labelSummary.Name = "labelSummary";
            this.labelSummary.Size = new System.Drawing.Size(640, 18);
            //
            // buttonApply
            //
            this.buttonApply.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonApply.Enabled = false;
            this.buttonApply.Location = new System.Drawing.Point(662, 9);
            this.buttonApply.Name = "buttonApply";
            this.buttonApply.Size = new System.Drawing.Size(110, 26);
            this.buttonApply.TabIndex = 6;
            this.buttonApply.UseVisualStyleBackColor = true;
            this.buttonApply.Click += new System.EventHandler(this.buttonApply_Click);
            //
            // buttonClose
            //
            this.buttonClose.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonClose.Location = new System.Drawing.Point(778, 9);
            this.buttonClose.Name = "buttonClose";
            this.buttonClose.Size = new System.Drawing.Size(110, 26);
            this.buttonClose.TabIndex = 7;
            this.buttonClose.UseVisualStyleBackColor = true;
            this.buttonClose.Click += new System.EventHandler(this.buttonClose_Click);
            //
            // CoverBatchForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(900, 640);
            this.Controls.Add(this.listViewResults);
            this.Controls.Add(this.panelAlternatives);
            this.Controls.Add(this.panelBottom);
            this.Controls.Add(this.panelTop);
            this.MinimumSize = new System.Drawing.Size(720, 480);
            this.Name = "CoverBatchForm";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            resources.ApplyResources(this, "$this");
            resources.ApplyResources(this.checkBoxIncludeExisting, "checkBoxIncludeExisting");
            resources.ApplyResources(this.buttonSearch, "buttonSearch");
            resources.ApplyResources(this.buttonCancel, "buttonCancel");
            resources.ApplyResources(this.buttonApply, "buttonApply");
            resources.ApplyResources(this.buttonClose, "buttonClose");
            resources.ApplyResources(this.labelAlternatives, "labelAlternatives");
            resources.ApplyResources(this.columnGame, "columnGame");
            resources.ApplyResources(this.columnSource, "columnSource");
            resources.ApplyResources(this.columnMatch, "columnMatch");
            resources.ApplyResources(this.columnState, "columnState");
            this.panelTop.ResumeLayout(false);
            this.panelTop.PerformLayout();
            this.panelAlternatives.ResumeLayout(false);
            this.panelBottom.ResumeLayout(false);
            this.ResumeLayout(false);
        }

        private System.Windows.Forms.Panel panelTop;
        private System.Windows.Forms.CheckBox checkBoxIncludeExisting;
        private System.Windows.Forms.Button buttonSearch;
        private System.Windows.Forms.Button buttonCancel;
        private System.Windows.Forms.ProgressBar progressBar;
        private System.Windows.Forms.Label labelStatus;
        private System.Windows.Forms.ListView listViewResults;
        private System.Windows.Forms.ColumnHeader columnGame;
        private System.Windows.Forms.ColumnHeader columnSource;
        private System.Windows.Forms.ColumnHeader columnMatch;
        private System.Windows.Forms.ColumnHeader columnState;
        private System.Windows.Forms.Panel panelAlternatives;
        private System.Windows.Forms.Label labelAlternatives;
        private System.Windows.Forms.FlowLayoutPanel flowAlternatives;
        private System.Windows.Forms.Panel panelBottom;
        private System.Windows.Forms.Label labelSummary;
        private System.Windows.Forms.Button buttonApply;
        private System.Windows.Forms.Button buttonClose;
    }
}
