namespace XIVLauncher
{
    partial class OptionsForm
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
            this.LanguageSelector = new System.Windows.Forms.ComboBox();
            this.label1 = new System.Windows.Forms.Label();
            this.SaveButton = new System.Windows.Forms.Button();
            this.dxBox = new System.Windows.Forms.CheckBox();
            this.LaunchBackupButton = new System.Windows.Forms.Button();
            this.ChangePathButton = new System.Windows.Forms.Button();
            this.GamePathDialog = new System.Windows.Forms.FolderBrowserDialog();
            this.pathLabel = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // LanguageSelector
            // 
            this.LanguageSelector.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.LanguageSelector.FormattingEnabled = true;
            this.LanguageSelector.Items.AddRange(new object[] {
            "Japanese",
            "English",
            "German",
            "French"});
            this.LanguageSelector.Location = new System.Drawing.Point(12, 28);
            this.LanguageSelector.Name = "LanguageSelector";
            this.LanguageSelector.Size = new System.Drawing.Size(121, 21);
            this.LanguageSelector.TabIndex = 0;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(12, 12);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(55, 13);
            this.label1.TabIndex = 1;
            this.label1.Text = "Language";
            // 
            // SaveButton
            // 
            this.SaveButton.Location = new System.Drawing.Point(35, 167);
            this.SaveButton.Name = "SaveButton";
            this.SaveButton.Size = new System.Drawing.Size(75, 23);
            this.SaveButton.TabIndex = 2;
            this.SaveButton.Text = "Save";
            this.SaveButton.UseVisualStyleBackColor = true;
            this.SaveButton.Click += new System.EventHandler(this.SaveButton_Click);
            // 
            // dxBox
            // 
            this.dxBox.AutoSize = true;
            this.dxBox.Location = new System.Drawing.Point(15, 55);
            this.dxBox.Name = "dxBox";
            this.dxBox.Size = new System.Drawing.Size(95, 17);
            this.dxBox.TabIndex = 3;
            this.dxBox.Text = "Use DirectX11";
            this.dxBox.UseVisualStyleBackColor = true;
            // 
            // LaunchBackupButton
            // 
            this.LaunchBackupButton.Location = new System.Drawing.Point(12, 78);
            this.LaunchBackupButton.Name = "LaunchBackupButton";
            this.LaunchBackupButton.Size = new System.Drawing.Size(121, 23);
            this.LaunchBackupButton.TabIndex = 4;
            this.LaunchBackupButton.Text = "Launch Backup Utility";
            this.LaunchBackupButton.UseVisualStyleBackColor = true;
            this.LaunchBackupButton.Click += new System.EventHandler(this.LaunchBackupTool);
            // 
            // ChangePathButton
            // 
            this.ChangePathButton.Location = new System.Drawing.Point(12, 107);
            this.ChangePathButton.Name = "ChangePathButton";
            this.ChangePathButton.Size = new System.Drawing.Size(121, 23);
            this.ChangePathButton.TabIndex = 5;
            this.ChangePathButton.Text = "Change game path";
            this.ChangePathButton.UseVisualStyleBackColor = true;
            this.ChangePathButton.Click += new System.EventHandler(this.ChangeGamePath);
            // 
            // GamePathDialog
            // 
            this.GamePathDialog.Description = "Please select your game path.";
            this.GamePathDialog.ShowNewFolderButton = false;
            // 
            // pathLabel
            // 
            this.pathLabel.AutoSize = true;
            this.pathLabel.Location = new System.Drawing.Point(12, 133);
            this.pathLabel.Name = "pathLabel";
            this.pathLabel.Size = new System.Drawing.Size(100, 13);
            this.pathLabel.TabIndex = 7;
            this.pathLabel.Text = "Current Game Path:";
            // 
            // OptionsForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(371, 198);
            this.Controls.Add(this.pathLabel);
            this.Controls.Add(this.ChangePathButton);
            this.Controls.Add(this.LaunchBackupButton);
            this.Controls.Add(this.dxBox);
            this.Controls.Add(this.SaveButton);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.LanguageSelector);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Name = "OptionsForm";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.Text = "Options";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ComboBox LanguageSelector;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button SaveButton;
        private System.Windows.Forms.CheckBox dxBox;
        private System.Windows.Forms.Button LaunchBackupButton;
        private System.Windows.Forms.Button ChangePathButton;
        private System.Windows.Forms.FolderBrowserDialog GamePathDialog;
        private System.Windows.Forms.Label pathLabel;
    }
}