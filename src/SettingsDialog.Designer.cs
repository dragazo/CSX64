namespace csx64
{
    partial class SettingsDialog
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
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this._OKButton = new System.Windows.Forms.Button();
            this._CancelButton = new System.Windows.Forms.Button();
            this.RetroButton = new System.Windows.Forms.Button();
            this.label3 = new System.Windows.Forms.Label();
            this.OldSchoolButton = new System.Windows.Forms.Button();
            this.PaperButton = new System.Windows.Forms.Button();
            this.HollywoodHackerButton = new System.Windows.Forms.Button();
            this.TextColorPicker = new csx64.ColorPicker();
            this.BackgroundColorPicker = new csx64.ColorPicker();
            this.ColorSwapButton = new System.Windows.Forms.Button();
            this.CoolButton = new System.Windows.Forms.Button();
            this.HalloweenButton = new System.Windows.Forms.Button();
            this.SlowMemoryCheck = new System.Windows.Forms.CheckBox();
            this.FileSystemCheck = new System.Windows.Forms.CheckBox();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(12, 9);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(95, 13);
            this.label1.TabIndex = 2;
            this.label1.Text = "Background Color:";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(12, 44);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(55, 13);
            this.label2.TabIndex = 3;
            this.label2.Text = "Tex Color:";
            // 
            // _OKButton
            // 
            this._OKButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this._OKButton.DialogResult = System.Windows.Forms.DialogResult.OK;
            this._OKButton.Location = new System.Drawing.Point(361, 272);
            this._OKButton.Margin = new System.Windows.Forms.Padding(2);
            this._OKButton.Name = "_OKButton";
            this._OKButton.Size = new System.Drawing.Size(75, 25);
            this._OKButton.TabIndex = 4;
            this._OKButton.Text = "Save";
            this._OKButton.UseVisualStyleBackColor = true;
            // 
            // _CancelButton
            // 
            this._CancelButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this._CancelButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this._CancelButton.Location = new System.Drawing.Point(440, 272);
            this._CancelButton.Margin = new System.Windows.Forms.Padding(2);
            this._CancelButton.Name = "_CancelButton";
            this._CancelButton.Size = new System.Drawing.Size(75, 25);
            this._CancelButton.TabIndex = 5;
            this._CancelButton.Text = "Cancel";
            this._CancelButton.UseVisualStyleBackColor = true;
            // 
            // RetroButton
            // 
            this.RetroButton.BackColor = System.Drawing.Color.Black;
            this.RetroButton.ForeColor = System.Drawing.Color.LimeGreen;
            this.RetroButton.Location = new System.Drawing.Point(11, 114);
            this.RetroButton.Margin = new System.Windows.Forms.Padding(2);
            this.RetroButton.Name = "RetroButton";
            this.RetroButton.Size = new System.Drawing.Size(125, 25);
            this.RetroButton.TabIndex = 6;
            this.RetroButton.Text = "Retro";
            this.RetroButton.UseVisualStyleBackColor = false;
            this.RetroButton.Click += new System.EventHandler(this.HandlePreset);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(12, 99);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(69, 13);
            this.label3.TabIndex = 7;
            this.label3.Text = "Color Presets";
            // 
            // OldSchoolButton
            // 
            this.OldSchoolButton.BackColor = System.Drawing.Color.Black;
            this.OldSchoolButton.ForeColor = System.Drawing.Color.White;
            this.OldSchoolButton.Location = new System.Drawing.Point(11, 143);
            this.OldSchoolButton.Margin = new System.Windows.Forms.Padding(2);
            this.OldSchoolButton.Name = "OldSchoolButton";
            this.OldSchoolButton.Size = new System.Drawing.Size(125, 25);
            this.OldSchoolButton.TabIndex = 8;
            this.OldSchoolButton.Text = "Old School";
            this.OldSchoolButton.UseVisualStyleBackColor = false;
            this.OldSchoolButton.Click += new System.EventHandler(this.HandlePreset);
            // 
            // PaperButton
            // 
            this.PaperButton.BackColor = System.Drawing.Color.White;
            this.PaperButton.ForeColor = System.Drawing.Color.Black;
            this.PaperButton.Location = new System.Drawing.Point(11, 172);
            this.PaperButton.Margin = new System.Windows.Forms.Padding(2);
            this.PaperButton.Name = "PaperButton";
            this.PaperButton.Size = new System.Drawing.Size(125, 25);
            this.PaperButton.TabIndex = 9;
            this.PaperButton.Text = "Paper";
            this.PaperButton.UseVisualStyleBackColor = false;
            this.PaperButton.Click += new System.EventHandler(this.HandlePreset);
            // 
            // HollywoodHackerButton
            // 
            this.HollywoodHackerButton.BackColor = System.Drawing.Color.Maroon;
            this.HollywoodHackerButton.ForeColor = System.Drawing.Color.White;
            this.HollywoodHackerButton.Location = new System.Drawing.Point(11, 201);
            this.HollywoodHackerButton.Margin = new System.Windows.Forms.Padding(2);
            this.HollywoodHackerButton.Name = "HollywoodHackerButton";
            this.HollywoodHackerButton.Size = new System.Drawing.Size(125, 25);
            this.HollywoodHackerButton.TabIndex = 10;
            this.HollywoodHackerButton.Text = "Hollywood Hacker";
            this.HollywoodHackerButton.UseVisualStyleBackColor = false;
            this.HollywoodHackerButton.Click += new System.EventHandler(this.HandlePreset);
            // 
            // TextColorPicker
            // 
            this.TextColorPicker.BackColor = System.Drawing.Color.Black;
            this.TextColorPicker.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.TextColorPicker.Color = System.Drawing.Color.Black;
            this.TextColorPicker.Location = new System.Drawing.Point(111, 44);
            this.TextColorPicker.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.TextColorPicker.Name = "TextColorPicker";
            this.TextColorPicker.Size = new System.Drawing.Size(25, 25);
            this.TextColorPicker.TabIndex = 1;
            // 
            // BackgroundColorPicker
            // 
            this.BackgroundColorPicker.BackColor = System.Drawing.Color.Black;
            this.BackgroundColorPicker.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.BackgroundColorPicker.Color = System.Drawing.Color.Black;
            this.BackgroundColorPicker.Location = new System.Drawing.Point(111, 9);
            this.BackgroundColorPicker.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.BackgroundColorPicker.Name = "BackgroundColorPicker";
            this.BackgroundColorPicker.Size = new System.Drawing.Size(25, 25);
            this.BackgroundColorPicker.TabIndex = 0;
            // 
            // ColorSwapButton
            // 
            this.ColorSwapButton.Location = new System.Drawing.Point(144, 26);
            this.ColorSwapButton.Name = "ColorSwapButton";
            this.ColorSwapButton.Size = new System.Drawing.Size(50, 25);
            this.ColorSwapButton.TabIndex = 11;
            this.ColorSwapButton.Text = "Swap";
            this.ColorSwapButton.UseVisualStyleBackColor = true;
            this.ColorSwapButton.Click += new System.EventHandler(this.ColorSwapButton_Click);
            // 
            // CoolButton
            // 
            this.CoolButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(28)))), ((int)(((byte)(132)))), ((int)(((byte)(187)))));
            this.CoolButton.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(133)))), ((int)(((byte)(250)))), ((int)(((byte)(250)))));
            this.CoolButton.Location = new System.Drawing.Point(11, 230);
            this.CoolButton.Margin = new System.Windows.Forms.Padding(2);
            this.CoolButton.Name = "CoolButton";
            this.CoolButton.Size = new System.Drawing.Size(125, 25);
            this.CoolButton.TabIndex = 12;
            this.CoolButton.Text = "Cool";
            this.CoolButton.UseVisualStyleBackColor = false;
            this.CoolButton.Click += new System.EventHandler(this.HandlePreset);
            // 
            // HalloweenButton
            // 
            this.HalloweenButton.BackColor = System.Drawing.Color.Black;
            this.HalloweenButton.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(128)))), ((int)(((byte)(0)))));
            this.HalloweenButton.Location = new System.Drawing.Point(11, 259);
            this.HalloweenButton.Margin = new System.Windows.Forms.Padding(2);
            this.HalloweenButton.Name = "HalloweenButton";
            this.HalloweenButton.Size = new System.Drawing.Size(125, 25);
            this.HalloweenButton.TabIndex = 13;
            this.HalloweenButton.Text = "Halloween";
            this.HalloweenButton.UseVisualStyleBackColor = false;
            this.HalloweenButton.Click += new System.EventHandler(this.HandlePreset);
            // 
            // SlowMemoryCheck
            // 
            this.SlowMemoryCheck.AutoSize = true;
            this.SlowMemoryCheck.Location = new System.Drawing.Point(233, 12);
            this.SlowMemoryCheck.Name = "SlowMemoryCheck";
            this.SlowMemoryCheck.Size = new System.Drawing.Size(89, 17);
            this.SlowMemoryCheck.TabIndex = 14;
            this.SlowMemoryCheck.Text = "Slow Memory";
            this.SlowMemoryCheck.UseVisualStyleBackColor = true;
            // 
            // FileSystemCheck
            // 
            this.FileSystemCheck.AutoSize = true;
            this.FileSystemCheck.Location = new System.Drawing.Point(233, 35);
            this.FileSystemCheck.Name = "FileSystemCheck";
            this.FileSystemCheck.Size = new System.Drawing.Size(79, 17);
            this.FileSystemCheck.TabIndex = 15;
            this.FileSystemCheck.Text = "File System";
            this.FileSystemCheck.UseVisualStyleBackColor = true;
            // 
            // SettingsDialog
            // 
            this.AcceptButton = this._OKButton;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this._CancelButton;
            this.ClientSize = new System.Drawing.Size(526, 308);
            this.Controls.Add(this.FileSystemCheck);
            this.Controls.Add(this.SlowMemoryCheck);
            this.Controls.Add(this.HalloweenButton);
            this.Controls.Add(this.CoolButton);
            this.Controls.Add(this.ColorSwapButton);
            this.Controls.Add(this.HollywoodHackerButton);
            this.Controls.Add(this.PaperButton);
            this.Controls.Add(this.OldSchoolButton);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.RetroButton);
            this.Controls.Add(this._CancelButton);
            this.Controls.Add(this._OKButton);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.TextColorPicker);
            this.Controls.Add(this.BackgroundColorPicker);
            this.Name = "SettingsDialog";
            this.Text = "Settings Dialog";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private ColorPicker BackgroundColorPicker;
        private ColorPicker TextColorPicker;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Button _OKButton;
        private System.Windows.Forms.Button _CancelButton;
        private System.Windows.Forms.Button RetroButton;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Button OldSchoolButton;
        private System.Windows.Forms.Button PaperButton;
        private System.Windows.Forms.Button HollywoodHackerButton;
        private System.Windows.Forms.Button ColorSwapButton;
        private System.Windows.Forms.Button CoolButton;
        private System.Windows.Forms.Button HalloweenButton;
        private System.Windows.Forms.CheckBox SlowMemoryCheck;
        private System.Windows.Forms.CheckBox FileSystemCheck;
    }
}