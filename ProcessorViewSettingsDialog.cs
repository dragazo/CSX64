using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace csx64
{
    public partial class ProcessorViewSettingsDialog : Form
    {
        /// <summary>
        /// Gets or sets the selected background color
        /// </summary>
        public Color BackgroundColor
        {
            get => BackgroundColorPicker.Color;
            set => BackgroundColorPicker.Color = value;
        }
        /// <summary>
        /// Gets or sets the selected foreground color
        /// </summary>
        public Color ForegroundColor
        {
            get => ForegroundColorPicker.Color;
            set => ForegroundColorPicker.Color = value;
        }

        public ProcessorViewSettingsDialog()
        {
            InitializeComponent();

            RetroButton.Click += HandlePreset;
            OldSchoolButton.Click += HandlePreset;
            PaperButton.Click += HandlePreset;
            HollywoodHackerButton.Click += HandlePreset;
            CoolButton.Click += HandlePreset;
            HalloweenButton.Click += HandlePreset;
        }

        private void HandlePreset(object sender, EventArgs e)
        {
            Button b = (Button)sender;

            BackgroundColor = b.BackColor;
            ForegroundColor = b.ForeColor;
        }

        private void ColorSwapButton_Click(object sender, EventArgs e)
        {
            Color temp = BackgroundColor;
            BackgroundColor = ForegroundColor;
            ForegroundColor = temp;
        }
    }
}
