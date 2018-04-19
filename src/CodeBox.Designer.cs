namespace csx64
{
    partial class CodeBox
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

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.RawBox = new System.Windows.Forms.TextBox();
            this.SuspendLayout();
            // 
            // RawBox
            // 
            this.RawBox.AcceptsReturn = true;
            this.RawBox.AcceptsTab = true;
            this.RawBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.RawBox.Location = new System.Drawing.Point(0, 0);
            this.RawBox.Multiline = true;
            this.RawBox.Name = "RawBox";
            this.RawBox.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.RawBox.Size = new System.Drawing.Size(317, 212);
            this.RawBox.TabIndex = 0;
            this.RawBox.WordWrap = false;
            this.RawBox.KeyDown += new System.Windows.Forms.KeyEventHandler(this.RawBox_KeyDown);
            // 
            // CodeBox
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.RawBox);
            this.Name = "CodeBox";
            this.Size = new System.Drawing.Size(317, 212);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox RawBox;
    }
}
