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
            this.OptionButton = new System.Windows.Forms.Button();
            this.LoginButton = new System.Windows.Forms.Button();
            this.SaveBox = new System.Windows.Forms.CheckBox();
            this.AutoLoginBox = new System.Windows.Forms.CheckBox();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.OTPTextBox = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.GamePathDialog = new System.Windows.Forms.FolderBrowserDialog();
            this.SuspendLayout();
            // 
            // IDTextBox
            // 
            this.IDTextBox.Location = new System.Drawing.Point(91, 29);
            this.IDTextBox.MaxLength = 16;
            this.IDTextBox.Name = "IDTextBox";
            this.IDTextBox.Size = new System.Drawing.Size(129, 20);
            this.IDTextBox.TabIndex = 0;
            // 
            // PWTextBox
            // 
            this.PWTextBox.Location = new System.Drawing.Point(91, 67);
            this.PWTextBox.MaxLength = 32;
            this.PWTextBox.Name = "PWTextBox";
            this.PWTextBox.Size = new System.Drawing.Size(129, 20);
            this.PWTextBox.TabIndex = 1;
            this.PWTextBox.UseSystemPasswordChar = true;
            // 
            // OptionButton
            // 
            this.OptionButton.Location = new System.Drawing.Point(40, 181);
            this.OptionButton.Name = "OptionButton";
            this.OptionButton.Size = new System.Drawing.Size(75, 23);
            this.OptionButton.TabIndex = 2;
            this.OptionButton.Text = "Options";
            this.OptionButton.UseVisualStyleBackColor = true;
            this.OptionButton.Click += new System.EventHandler(this.OpenOptions);
            // 
            // LoginButton
            // 
            this.LoginButton.Location = new System.Drawing.Point(121, 181);
            this.LoginButton.Name = "LoginButton";
            this.LoginButton.Size = new System.Drawing.Size(75, 23);
            this.LoginButton.TabIndex = 3;
            this.LoginButton.Text = "Login";
            this.LoginButton.UseVisualStyleBackColor = true;
            this.LoginButton.Click += new System.EventHandler(this.login);
            // 
            // SaveBox
            // 
            this.SaveBox.AutoSize = true;
            this.SaveBox.Location = new System.Drawing.Point(40, 135);
            this.SaveBox.Name = "SaveBox";
            this.SaveBox.Size = new System.Drawing.Size(122, 17);
            this.SaveBox.TabIndex = 4;
            this.SaveBox.Text = "save for next startup";
            this.SaveBox.UseVisualStyleBackColor = true;
            this.SaveBox.CheckedChanged += new System.EventHandler(this.SaveBox_CheckedChanged);
            // 
            // AutoLoginBox
            // 
            this.AutoLoginBox.AutoSize = true;
            this.AutoLoginBox.Enabled = false;
            this.AutoLoginBox.Location = new System.Drawing.Point(40, 158);
            this.AutoLoginBox.Name = "AutoLoginBox";
            this.AutoLoginBox.Size = new System.Drawing.Size(115, 17);
            this.AutoLoginBox.TabIndex = 5;
            this.AutoLoginBox.Text = "log in automatically";
            this.AutoLoginBox.UseVisualStyleBackColor = true;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(12, 32);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(78, 13);
            this.label1.TabIndex = 6;
            this.label1.Text = "Square Enix ID";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(12, 70);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(53, 13);
            this.label2.TabIndex = 7;
            this.label2.Text = "Password";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(12, 96);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(29, 13);
            this.label3.TabIndex = 9;
            this.label3.Text = "OTP";
            // 
            // OTPTextBox
            // 
            this.OTPTextBox.Location = new System.Drawing.Point(91, 93);
            this.OTPTextBox.MaxLength = 6;
            this.OTPTextBox.Name = "OTPTextBox";
            this.OTPTextBox.Size = new System.Drawing.Size(129, 20);
            this.OTPTextBox.TabIndex = 8;
            this.OTPTextBox.UseSystemPasswordChar = true;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.ForeColor = System.Drawing.SystemColors.GrayText;
            this.label4.Location = new System.Drawing.Point(88, 225);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(0, 13);
            this.label4.TabIndex = 10;
            this.label4.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            // 
            // GamePathDialog
            // 
            this.GamePathDialog.Description = "Please select your game path.";
            this.GamePathDialog.ShowNewFolderButton = false;
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(243, 260);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.OTPTextBox);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.AutoLoginBox);
            this.Controls.Add(this.SaveBox);
            this.Controls.Add(this.LoginButton);
            this.Controls.Add(this.OptionButton);
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
        private System.Windows.Forms.Button OptionButton;
        private System.Windows.Forms.Button LoginButton;
        private System.Windows.Forms.CheckBox SaveBox;
        private System.Windows.Forms.CheckBox AutoLoginBox;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox OTPTextBox;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.FolderBrowserDialog GamePathDialog;
    }
}

