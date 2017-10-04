namespace XIVLauncher
{
    partial class MainForm
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
            this.IDTextBox = new System.Windows.Forms.TextBox();
            this.PWTextBox = new System.Windows.Forms.TextBox();
            this.optionsButton = new System.Windows.Forms.Button();
            this.loginButton = new System.Windows.Forms.Button();
            this.saveCheckBox = new System.Windows.Forms.CheckBox();
            this.autoLoginCheckBox = new System.Windows.Forms.CheckBox();
            this.IDLabel = new System.Windows.Forms.Label();
            this.PWLabel = new System.Windows.Forms.Label();
            this.OTPLabel = new System.Windows.Forms.Label();
            this.OTPTextBox = new System.Windows.Forms.TextBox();
            this.StatusLabel = new System.Windows.Forms.Label();
            this.GamePathDialog = new System.Windows.Forms.FolderBrowserDialog();
            this.QueueButton = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // IDTextBox
            // 
            this.IDTextBox.Location = new System.Drawing.Point(99, 27);
            this.IDTextBox.MaxLength = 16;
            this.IDTextBox.Name = "IDTextBox";
            this.IDTextBox.Size = new System.Drawing.Size(129, 20);
            this.IDTextBox.TabIndex = 0;
            // 
            // PWTextBox
            // 
            this.PWTextBox.Location = new System.Drawing.Point(99, 62);
            this.PWTextBox.MaxLength = 32;
            this.PWTextBox.Name = "PWTextBox";
            this.PWTextBox.Size = new System.Drawing.Size(129, 20);
            this.PWTextBox.TabIndex = 1;
            this.PWTextBox.UseSystemPasswordChar = true;
            // 
            // optionsButton
            // 
            this.optionsButton.Location = new System.Drawing.Point(40, 167);
            this.optionsButton.Name = "optionsButton";
            this.optionsButton.Size = new System.Drawing.Size(75, 23);
            this.optionsButton.TabIndex = 4;
            this.optionsButton.Text = "Options";
            this.optionsButton.UseVisualStyleBackColor = true;
            this.optionsButton.Click += new System.EventHandler(this.OpenOptions);
            // 
            // loginButton
            // 
            this.loginButton.Location = new System.Drawing.Point(121, 167);
            this.loginButton.Name = "loginButton";
            this.loginButton.Size = new System.Drawing.Size(75, 23);
            this.loginButton.TabIndex = 3;
            this.loginButton.Text = "Login";
            this.loginButton.UseVisualStyleBackColor = true;
            this.loginButton.Click += new System.EventHandler(this.Login);
            // 
            // saveCheckBox
            // 
            this.saveCheckBox.AutoSize = true;
            this.saveCheckBox.Location = new System.Drawing.Point(40, 119);
            this.saveCheckBox.Name = "saveCheckBox";
            this.saveCheckBox.Size = new System.Drawing.Size(122, 17);
            this.saveCheckBox.TabIndex = 5;
            this.saveCheckBox.Text = "save for next startup";
            this.saveCheckBox.UseVisualStyleBackColor = true;
            this.saveCheckBox.CheckedChanged += new System.EventHandler(this.SaveBox_CheckedChanged);
            // 
            // autoLoginCheckBox
            // 
            this.autoLoginCheckBox.AutoSize = true;
            this.autoLoginCheckBox.Enabled = false;
            this.autoLoginCheckBox.Location = new System.Drawing.Point(40, 141);
            this.autoLoginCheckBox.Name = "autoLoginCheckBox";
            this.autoLoginCheckBox.Size = new System.Drawing.Size(115, 17);
            this.autoLoginCheckBox.TabIndex = 6;
            this.autoLoginCheckBox.Text = "log in automatically";
            this.autoLoginCheckBox.UseVisualStyleBackColor = true;
            // 
            // IDLabel
            // 
            this.IDLabel.AutoSize = true;
            this.IDLabel.Location = new System.Drawing.Point(12, 30);
            this.IDLabel.Name = "IDLabel";
            this.IDLabel.Size = new System.Drawing.Size(78, 13);
            this.IDLabel.TabIndex = 6;
            this.IDLabel.Text = "Square Enix ID";
            // 
            // PWLabel
            // 
            this.PWLabel.AutoSize = true;
            this.PWLabel.Location = new System.Drawing.Point(12, 65);
            this.PWLabel.Name = "PWLabel";
            this.PWLabel.Size = new System.Drawing.Size(53, 13);
            this.PWLabel.TabIndex = 7;
            this.PWLabel.Text = "Password";
            // 
            // OTPLabel
            // 
            this.OTPLabel.AutoSize = true;
            this.OTPLabel.Location = new System.Drawing.Point(12, 89);
            this.OTPLabel.Name = "OTPLabel";
            this.OTPLabel.Size = new System.Drawing.Size(29, 13);
            this.OTPLabel.TabIndex = 9;
            this.OTPLabel.Text = "OTP";
            // 
            // OTPTextBox
            // 
            this.OTPTextBox.Location = new System.Drawing.Point(99, 87);
            this.OTPTextBox.MaxLength = 6;
            this.OTPTextBox.Name = "OTPTextBox";
            this.OTPTextBox.Size = new System.Drawing.Size(129, 20);
            this.OTPTextBox.TabIndex = 2;
            this.OTPTextBox.UseSystemPasswordChar = true;
            // 
            // StatusLabel
            // 
            this.StatusLabel.AutoSize = true;
            this.StatusLabel.ForeColor = System.Drawing.SystemColors.GrayText;
            this.StatusLabel.Location = new System.Drawing.Point(117, 208);
            this.StatusLabel.Name = "StatusLabel";
            this.StatusLabel.Size = new System.Drawing.Size(0, 13);
            this.StatusLabel.TabIndex = 10;
            this.StatusLabel.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            // 
            // GamePathDialog
            // 
            this.GamePathDialog.Description = "Please select your game path.";
            this.GamePathDialog.ShowNewFolderButton = false;
            // 
            // QueueButton
            // 
            this.QueueButton.Location = new System.Drawing.Point(61, 240);
            this.QueueButton.Name = "QueueButton";
            this.QueueButton.Size = new System.Drawing.Size(116, 20);
            this.QueueButton.TabIndex = 11;
            this.QueueButton.Text = "Maintenance Queue";
            this.QueueButton.UseVisualStyleBackColor = true;
            this.QueueButton.Click += new System.EventHandler(this.QueueButton_Click);
            // 
            // MainForm
            // 
            this.AcceptButton = this.loginButton;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(243, 272);
            this.Controls.Add(this.QueueButton);
            this.Controls.Add(this.StatusLabel);
            this.Controls.Add(this.OTPLabel);
            this.Controls.Add(this.OTPTextBox);
            this.Controls.Add(this.PWLabel);
            this.Controls.Add(this.IDLabel);
            this.Controls.Add(this.autoLoginCheckBox);
            this.Controls.Add(this.saveCheckBox);
            this.Controls.Add(this.loginButton);
            this.Controls.Add(this.optionsButton);
            this.Controls.Add(this.PWTextBox);
            this.Controls.Add(this.IDTextBox);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Name = "MainForm";
            this.ShowIcon = false;
            this.Text = "XIV Launcher";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox IDTextBox;
        private System.Windows.Forms.TextBox PWTextBox;
        private System.Windows.Forms.Button optionsButton;
        private System.Windows.Forms.Button loginButton;
        private System.Windows.Forms.CheckBox saveCheckBox;
        private System.Windows.Forms.CheckBox autoLoginCheckBox;
        private System.Windows.Forms.Label IDLabel;
        private System.Windows.Forms.Label PWLabel;
        private System.Windows.Forms.Label OTPLabel;
        private System.Windows.Forms.TextBox OTPTextBox;
        private System.Windows.Forms.Label StatusLabel;
        private System.Windows.Forms.FolderBrowserDialog GamePathDialog;
        private System.Windows.Forms.Button QueueButton;
    }
}

