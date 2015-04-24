namespace GroundControl
{
    partial class FormSettings
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
            this.udBPM = new System.Windows.Forms.NumericUpDown();
            this.lblsTiming = new System.Windows.Forms.Label();
            this.lblsBeatsPerMin = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.udRowsPerBeat = new System.Windows.Forms.NumericUpDown();
            this.lblsBeatsPerSecond = new System.Windows.Forms.Label();
            this.lblBeasPerSecond = new System.Windows.Forms.Label();
            this.lblsInputFile = new System.Windows.Forms.Label();
            this.lblsFile = new System.Windows.Forms.Label();
            this.txtInputFile = new System.Windows.Forms.TextBox();
            this.btnOpenFile = new System.Windows.Forms.Button();
            this.lblsTimeOffset = new System.Windows.Forms.Label();
            this.udTimeOffset = new System.Windows.Forms.NumericUpDown();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnOK = new System.Windows.Forms.Button();
            this.openFileDialog = new System.Windows.Forms.OpenFileDialog();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.comboTimeFormatting = new System.Windows.Forms.ComboBox();
            this.lblsTotalRows = new System.Windows.Forms.Label();
            this.udRows = new System.Windows.Forms.NumericUpDown();
            ((System.ComponentModel.ISupportInitialize)(this.udBPM)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.udRowsPerBeat)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.udTimeOffset)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.udRows)).BeginInit();
            this.SuspendLayout();
            // 
            // udBPM
            // 
            this.udBPM.Location = new System.Drawing.Point(148, 109);
            this.udBPM.Maximum = new decimal(new int[] {
            250,
            0,
            0,
            0});
            this.udBPM.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.udBPM.Name = "udBPM";
            this.udBPM.Size = new System.Drawing.Size(68, 20);
            this.udBPM.TabIndex = 3;
            this.udBPM.Value = new decimal(new int[] {
            120,
            0,
            0,
            0});
            this.udBPM.ValueChanged += new System.EventHandler(this.TimingValuesChanged);
            // 
            // lblsTiming
            // 
            this.lblsTiming.AutoSize = true;
            this.lblsTiming.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(177)));
            this.lblsTiming.Location = new System.Drawing.Point(7, 60);
            this.lblsTiming.Margin = new System.Windows.Forms.Padding(3, 0, 3, 10);
            this.lblsTiming.Name = "lblsTiming";
            this.lblsTiming.Size = new System.Drawing.Size(44, 13);
            this.lblsTiming.TabIndex = 1;
            this.lblsTiming.Text = "Timing";
            // 
            // lblsBeatsPerMin
            // 
            this.lblsBeatsPerMin.AutoSize = true;
            this.lblsBeatsPerMin.Location = new System.Drawing.Point(7, 111);
            this.lblsBeatsPerMin.Margin = new System.Windows.Forms.Padding(3, 0, 3, 15);
            this.lblsBeatsPerMin.Name = "lblsBeatsPerMin";
            this.lblsBeatsPerMin.Size = new System.Drawing.Size(109, 13);
            this.lblsBeatsPerMin.TabIndex = 2;
            this.lblsBeatsPerMin.Text = "Beats / Minute (BPM)";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(7, 139);
            this.label3.Margin = new System.Windows.Forms.Padding(3, 0, 3, 15);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(67, 13);
            this.label3.TabIndex = 4;
            this.label3.Text = "Rows / Beat";
            // 
            // udRowsPerBeat
            // 
            this.udRowsPerBeat.Location = new System.Drawing.Point(148, 137);
            this.udRowsPerBeat.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.udRowsPerBeat.Name = "udRowsPerBeat";
            this.udRowsPerBeat.Size = new System.Drawing.Size(68, 20);
            this.udRowsPerBeat.TabIndex = 4;
            this.udRowsPerBeat.Value = new decimal(new int[] {
            8,
            0,
            0,
            0});
            this.udRowsPerBeat.ValueChanged += new System.EventHandler(this.TimingValuesChanged);
            // 
            // lblsBeatsPerSecond
            // 
            this.lblsBeatsPerSecond.AutoSize = true;
            this.lblsBeatsPerSecond.Location = new System.Drawing.Point(7, 167);
            this.lblsBeatsPerSecond.Margin = new System.Windows.Forms.Padding(3, 0, 3, 15);
            this.lblsBeatsPerSecond.Name = "lblsBeatsPerSecond";
            this.lblsBeatsPerSecond.Size = new System.Drawing.Size(82, 13);
            this.lblsBeatsPerSecond.TabIndex = 5;
            this.lblsBeatsPerSecond.Text = "Beats / Second";
            // 
            // lblBeasPerSecond
            // 
            this.lblBeasPerSecond.AutoSize = true;
            this.lblBeasPerSecond.Location = new System.Drawing.Point(145, 167);
            this.lblBeasPerSecond.Margin = new System.Windows.Forms.Padding(3, 0, 3, 15);
            this.lblBeasPerSecond.Name = "lblBeasPerSecond";
            this.lblBeasPerSecond.Size = new System.Drawing.Size(16, 13);
            this.lblBeasPerSecond.TabIndex = 6;
            this.lblBeasPerSecond.Text = "---";
            // 
            // lblsInputFile
            // 
            this.lblsInputFile.AutoSize = true;
            this.lblsInputFile.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(177)));
            this.lblsInputFile.Location = new System.Drawing.Point(7, 9);
            this.lblsInputFile.Margin = new System.Windows.Forms.Padding(3, 0, 3, 10);
            this.lblsInputFile.Name = "lblsInputFile";
            this.lblsInputFile.Size = new System.Drawing.Size(102, 13);
            this.lblsInputFile.TabIndex = 7;
            this.lblsInputFile.Text = "Audio Reference";
            // 
            // lblsFile
            // 
            this.lblsFile.AutoSize = true;
            this.lblsFile.Location = new System.Drawing.Point(7, 32);
            this.lblsFile.Margin = new System.Windows.Forms.Padding(3, 0, 3, 15);
            this.lblsFile.Name = "lblsFile";
            this.lblsFile.Size = new System.Drawing.Size(23, 13);
            this.lblsFile.TabIndex = 8;
            this.lblsFile.Text = "File";
            // 
            // txtInputFile
            // 
            this.txtInputFile.Location = new System.Drawing.Point(37, 29);
            this.txtInputFile.Margin = new System.Windows.Forms.Padding(3, 3, 0, 3);
            this.txtInputFile.Name = "txtInputFile";
            this.txtInputFile.Size = new System.Drawing.Size(152, 20);
            this.txtInputFile.TabIndex = 0;
            // 
            // btnOpenFile
            // 
            this.btnOpenFile.Location = new System.Drawing.Point(189, 28);
            this.btnOpenFile.Margin = new System.Windows.Forms.Padding(0, 3, 3, 3);
            this.btnOpenFile.Name = "btnOpenFile";
            this.btnOpenFile.Size = new System.Drawing.Size(26, 22);
            this.btnOpenFile.TabIndex = 1;
            this.btnOpenFile.Text = "...";
            this.btnOpenFile.UseVisualStyleBackColor = true;
            this.btnOpenFile.Click += new System.EventHandler(this.btnOpenFile_Click);
            // 
            // lblsTimeOffset
            // 
            this.lblsTimeOffset.AutoSize = true;
            this.lblsTimeOffset.Location = new System.Drawing.Point(7, 195);
            this.lblsTimeOffset.Margin = new System.Windows.Forms.Padding(3, 0, 3, 15);
            this.lblsTimeOffset.Name = "lblsTimeOffset";
            this.lblsTimeOffset.Size = new System.Drawing.Size(83, 13);
            this.lblsTimeOffset.TabIndex = 12;
            this.lblsTimeOffset.Text = "Time Offset [ms]";
            // 
            // udTimeOffset
            // 
            this.udTimeOffset.Location = new System.Drawing.Point(148, 193);
            this.udTimeOffset.Maximum = new decimal(new int[] {
            10000000,
            0,
            0,
            0});
            this.udTimeOffset.Minimum = new decimal(new int[] {
            10000000,
            0,
            0,
            -2147483648});
            this.udTimeOffset.Name = "udTimeOffset";
            this.udTimeOffset.Size = new System.Drawing.Size(68, 20);
            this.udTimeOffset.TabIndex = 5;
            // 
            // btnCancel
            // 
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(10, 279);
            this.btnCancel.Margin = new System.Windows.Forms.Padding(5);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(99, 34);
            this.btnCancel.TabIndex = 8;
            this.btnCancel.Text = "&Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // btnOK
            // 
            this.btnOK.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btnOK.Location = new System.Drawing.Point(116, 279);
            this.btnOK.Margin = new System.Windows.Forms.Padding(5);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(99, 34);
            this.btnOK.TabIndex = 7;
            this.btnOK.Text = "&OK";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            // 
            // openFileDialog
            // 
            this.openFileDialog.Filter = "MP3 files|*.mp3|All files|*.*";
            this.openFileDialog.Title = "Open Audio File";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(7, 246);
            this.label1.Margin = new System.Windows.Forms.Padding(3, 0, 3, 15);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(65, 13);
            this.label1.TabIndex = 14;
            this.label1.Text = "Time Format";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(177)));
            this.label2.Location = new System.Drawing.Point(7, 223);
            this.label2.Margin = new System.Windows.Forms.Padding(3, 0, 3, 10);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(90, 13);
            this.label2.TabIndex = 13;
            this.label2.Text = "Editor Settings";
            // 
            // comboTimeFormatting
            // 
            this.comboTimeFormatting.FormattingEnabled = true;
            this.comboTimeFormatting.Items.AddRange(new object[] {
            "{row}",
            "{row:0000000}",
            "{row:X4}",
            "{row:x5}",
            "{seconds:0.0}",
            "{seconds:000000.00}",
            "{time:mm\\:ss\\.ff}"});
            this.comboTimeFormatting.Location = new System.Drawing.Point(88, 243);
            this.comboTimeFormatting.Name = "comboTimeFormatting";
            this.comboTimeFormatting.Size = new System.Drawing.Size(128, 21);
            this.comboTimeFormatting.TabIndex = 6;
            // 
            // lblsTotalRows
            // 
            this.lblsTotalRows.AutoSize = true;
            this.lblsTotalRows.Location = new System.Drawing.Point(7, 83);
            this.lblsTotalRows.Margin = new System.Windows.Forms.Padding(3, 0, 3, 15);
            this.lblsTotalRows.Name = "lblsTotalRows";
            this.lblsTotalRows.Size = new System.Drawing.Size(61, 13);
            this.lblsTotalRows.TabIndex = 15;
            this.lblsTotalRows.Text = "Total Rows";
            // 
            // udRows
            // 
            this.udRows.Location = new System.Drawing.Point(148, 81);
            this.udRows.Maximum = new decimal(new int[] {
            100000000,
            0,
            0,
            0});
            this.udRows.Name = "udRows";
            this.udRows.Size = new System.Drawing.Size(68, 20);
            this.udRows.TabIndex = 2;
            this.udRows.Value = new decimal(new int[] {
            4096,
            0,
            0,
            0});
            // 
            // FormSettings
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSize = true;
            this.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(229, 328);
            this.Controls.Add(this.lblsTotalRows);
            this.Controls.Add(this.udRows);
            this.Controls.Add(this.comboTimeFormatting);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.lblsTimeOffset);
            this.Controls.Add(this.udTimeOffset);
            this.Controls.Add(this.btnOpenFile);
            this.Controls.Add(this.txtInputFile);
            this.Controls.Add(this.lblsFile);
            this.Controls.Add(this.lblsInputFile);
            this.Controls.Add(this.lblBeasPerSecond);
            this.Controls.Add(this.lblsBeatsPerSecond);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.udRowsPerBeat);
            this.Controls.Add(this.lblsBeatsPerMin);
            this.Controls.Add(this.lblsTiming);
            this.Controls.Add(this.udBPM);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "FormSettings";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Settings";
            ((System.ComponentModel.ISupportInitialize)(this.udBPM)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.udRowsPerBeat)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.udTimeOffset)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.udRows)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.NumericUpDown udBPM;
        private System.Windows.Forms.Label lblsTiming;
        private System.Windows.Forms.Label lblsBeatsPerMin;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.NumericUpDown udRowsPerBeat;
        private System.Windows.Forms.Label lblsBeatsPerSecond;
        private System.Windows.Forms.Label lblBeasPerSecond;
        private System.Windows.Forms.Label lblsInputFile;
        private System.Windows.Forms.Label lblsFile;
        private System.Windows.Forms.TextBox txtInputFile;
        private System.Windows.Forms.Button btnOpenFile;
        private System.Windows.Forms.Label lblsTimeOffset;
        private System.Windows.Forms.NumericUpDown udTimeOffset;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.OpenFileDialog openFileDialog;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.ComboBox comboTimeFormatting;
        private System.Windows.Forms.Label lblsTotalRows;
        private System.Windows.Forms.NumericUpDown udRows;
    }
}