namespace csx64
{
    partial class Form1
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
            this.TickButton = new System.Windows.Forms.Button();
            this.Tick10Button = new System.Windows.Forms.Button();
            this.Tick100Button = new System.Windows.Forms.Button();
            this.Tick1000Button = new System.Windows.Forms.Button();
            this.CompileButton = new System.Windows.Forms.Button();
            this.RunButton = new System.Windows.Forms.Button();
            this.StopButton = new System.Windows.Forms.Button();
            this.CodeBox = new System.Windows.Forms.TextBox();
            this.PauseButton = new System.Windows.Forms.Button();
            this.button1 = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // TickButton
            // 
            this.TickButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.TickButton.Location = new System.Drawing.Point(12, 658);
            this.TickButton.Name = "TickButton";
            this.TickButton.Size = new System.Drawing.Size(75, 23);
            this.TickButton.TabIndex = 0;
            this.TickButton.Text = "Tick";
            this.TickButton.UseVisualStyleBackColor = true;
            this.TickButton.Click += new System.EventHandler(this.TickButton_Click);
            // 
            // Tick10Button
            // 
            this.Tick10Button.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.Tick10Button.Location = new System.Drawing.Point(93, 658);
            this.Tick10Button.Name = "Tick10Button";
            this.Tick10Button.Size = new System.Drawing.Size(75, 23);
            this.Tick10Button.TabIndex = 1;
            this.Tick10Button.Text = "Tick 10";
            this.Tick10Button.UseVisualStyleBackColor = true;
            this.Tick10Button.Click += new System.EventHandler(this.Tick10Button_Click);
            // 
            // Tick100Button
            // 
            this.Tick100Button.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.Tick100Button.Location = new System.Drawing.Point(174, 658);
            this.Tick100Button.Name = "Tick100Button";
            this.Tick100Button.Size = new System.Drawing.Size(75, 23);
            this.Tick100Button.TabIndex = 2;
            this.Tick100Button.Text = "Tick 100";
            this.Tick100Button.UseVisualStyleBackColor = true;
            this.Tick100Button.Click += new System.EventHandler(this.Tick100Button_Click);
            // 
            // Tick1000Button
            // 
            this.Tick1000Button.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.Tick1000Button.Location = new System.Drawing.Point(255, 658);
            this.Tick1000Button.Name = "Tick1000Button";
            this.Tick1000Button.Size = new System.Drawing.Size(75, 23);
            this.Tick1000Button.TabIndex = 3;
            this.Tick1000Button.Text = "Tick 1000";
            this.Tick1000Button.UseVisualStyleBackColor = true;
            this.Tick1000Button.Click += new System.EventHandler(this.Tick1000Button_Click);
            // 
            // CompileButton
            // 
            this.CompileButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.CompileButton.Location = new System.Drawing.Point(12, 629);
            this.CompileButton.Name = "CompileButton";
            this.CompileButton.Size = new System.Drawing.Size(75, 23);
            this.CompileButton.TabIndex = 4;
            this.CompileButton.Text = "Compile";
            this.CompileButton.UseVisualStyleBackColor = true;
            this.CompileButton.Click += new System.EventHandler(this.CompileButton_Click);
            // 
            // RunButton
            // 
            this.RunButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.RunButton.Location = new System.Drawing.Point(174, 629);
            this.RunButton.Name = "RunButton";
            this.RunButton.Size = new System.Drawing.Size(75, 23);
            this.RunButton.TabIndex = 5;
            this.RunButton.Text = "Run";
            this.RunButton.UseVisualStyleBackColor = true;
            this.RunButton.Click += new System.EventHandler(this.RunButton_Click);
            // 
            // StopButton
            // 
            this.StopButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.StopButton.Location = new System.Drawing.Point(93, 629);
            this.StopButton.Name = "StopButton";
            this.StopButton.Size = new System.Drawing.Size(75, 23);
            this.StopButton.TabIndex = 6;
            this.StopButton.Text = "Stop";
            this.StopButton.UseVisualStyleBackColor = true;
            this.StopButton.Click += new System.EventHandler(this.StopButton_Click);
            // 
            // CodeBox
            // 
            this.CodeBox.AcceptsTab = true;
            this.CodeBox.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.CodeBox.Font = new System.Drawing.Font("Monotype Corsiva", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)), true);
            this.CodeBox.HideSelection = false;
            this.CodeBox.Location = new System.Drawing.Point(375, 171);
            this.CodeBox.Multiline = true;
            this.CodeBox.Name = "CodeBox";
            this.CodeBox.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.CodeBox.Size = new System.Drawing.Size(528, 510);
            this.CodeBox.TabIndex = 7;
            this.CodeBox.WordWrap = false;
            this.CodeBox.KeyDown += new System.Windows.Forms.KeyEventHandler(this.CodeBox_KeyDown);
            // 
            // PauseButton
            // 
            this.PauseButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.PauseButton.Location = new System.Drawing.Point(255, 629);
            this.PauseButton.Name = "PauseButton";
            this.PauseButton.Size = new System.Drawing.Size(75, 23);
            this.PauseButton.TabIndex = 8;
            this.PauseButton.Text = "Pause";
            this.PauseButton.UseVisualStyleBackColor = true;
            this.PauseButton.Click += new System.EventHandler(this.PauseButton_Click);
            // 
            // button1
            // 
            this.button1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.button1.Location = new System.Drawing.Point(284, 600);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(75, 23);
            this.button1.TabIndex = 9;
            this.button1.Text = "Test";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.Black;
            this.ClientSize = new System.Drawing.Size(915, 693);
            this.Controls.Add(this.button1);
            this.Controls.Add(this.PauseButton);
            this.Controls.Add(this.CodeBox);
            this.Controls.Add(this.StopButton);
            this.Controls.Add(this.RunButton);
            this.Controls.Add(this.CompileButton);
            this.Controls.Add(this.Tick1000Button);
            this.Controls.Add(this.Tick100Button);
            this.Controls.Add(this.Tick10Button);
            this.Controls.Add(this.TickButton);
            this.DoubleBuffered = true;
            this.MinimumSize = new System.Drawing.Size(931, 513);
            this.Name = "Form1";
            this.Text = "Form1";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button TickButton;
        private System.Windows.Forms.Button Tick10Button;
        private System.Windows.Forms.Button Tick100Button;
        private System.Windows.Forms.Button Tick1000Button;
        private System.Windows.Forms.Button CompileButton;
        private System.Windows.Forms.Button RunButton;
        private System.Windows.Forms.Button StopButton;
        private System.Windows.Forms.Button PauseButton;
        private System.Windows.Forms.TextBox CodeBox;
        private System.Windows.Forms.Button button1;
    }
}

