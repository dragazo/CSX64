namespace csx64
{
    partial class ConsoleClient
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.MainScroll = new System.Windows.Forms.VScrollBar();
            this.SuspendLayout();
            // 
            // MainScroll
            // 
            this.MainScroll.Dock = System.Windows.Forms.DockStyle.Right;
            this.MainScroll.Location = new System.Drawing.Point(783, 0);
            this.MainScroll.Name = "MainScroll";
            this.MainScroll.Size = new System.Drawing.Size(17, 450);
            this.MainScroll.SmallChange = 3;
            this.MainScroll.TabIndex = 0;
            this.MainScroll.ValueChanged += new System.EventHandler(this.MainScroll_ValueChanged);
            // 
            // ConsoleClient
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Controls.Add(this.MainScroll);
            this.DoubleBuffered = true;
            this.KeyPreview = true;
            this.Name = "ConsoleClient";
            this.Text = "ConsoleClient";
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.VScrollBar MainScroll;
    }
}