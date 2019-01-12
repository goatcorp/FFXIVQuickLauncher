namespace XIVLauncher
{
    partial class OTPForm
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
            this.otpField = new System.Windows.Forms.TextBox();
            this.SuspendLayout();
            // 
            // otpField
            // 
            this.otpField.Font = new System.Drawing.Font("Courier New", 15.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.otpField.Location = new System.Drawing.Point(13, 13);
            this.otpField.Name = "otpField";
            this.otpField.Size = new System.Drawing.Size(259, 31);
            this.otpField.TabIndex = 0;
            // 
            // OTPForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(284, 58);
            this.Controls.Add(this.otpField);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "OTPForm";
            this.ShowIcon = false;
            this.Text = "One-Time Password";
            this.Load += new System.EventHandler(this.OTPForm_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox otpField;
    }
}