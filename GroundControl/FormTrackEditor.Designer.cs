namespace GroundControl
{
    partial class FormTrackEditor
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.listTracks = new System.Windows.Forms.ListView();
            this.columnHeader1 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.SuspendLayout();
            // 
            // listTracks
            // 
            this.listTracks.AllowDrop = true;
            this.listTracks.CheckBoxes = true;
            this.listTracks.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnHeader1});
            this.listTracks.Dock = System.Windows.Forms.DockStyle.Fill;
            this.listTracks.FullRowSelect = true;
            this.listTracks.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.None;
            this.listTracks.LabelEdit = true;
            this.listTracks.Location = new System.Drawing.Point(0, 0);
            this.listTracks.Name = "listTracks";
            this.listTracks.Size = new System.Drawing.Size(178, 360);
            this.listTracks.TabIndex = 0;
            this.listTracks.UseCompatibleStateImageBehavior = false;
            this.listTracks.View = System.Windows.Forms.View.Details;
            this.listTracks.AfterLabelEdit += new System.Windows.Forms.LabelEditEventHandler(this.listTracks_AfterLabelEdit);
            this.listTracks.ItemCheck += new System.Windows.Forms.ItemCheckEventHandler(this.listTracks_ItemCheck);
            this.listTracks.DragDrop += new System.Windows.Forms.DragEventHandler(this.listTracks_DragDrop);
            this.listTracks.DragOver += new System.Windows.Forms.DragEventHandler(this.listTracks_DragOver);
            this.listTracks.KeyDown += new System.Windows.Forms.KeyEventHandler(this.listTracks_KeyDown);
            this.listTracks.MouseMove += new System.Windows.Forms.MouseEventHandler(this.listTracks_MouseMove);
            this.listTracks.Resize += new System.EventHandler(this.listTracks_Resize);
            // 
            // columnHeader1
            // 
            this.columnHeader1.Width = 174;
            // 
            // FormTrackEditor
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(178, 360);
            this.Controls.Add(this.listTracks);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "FormTrackEditor";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.Text = "Tracks";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.FormTrackEditor_FormClosing);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.ListView listTracks;
        private System.Windows.Forms.ColumnHeader columnHeader1;
    }
}