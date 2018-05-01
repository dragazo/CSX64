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
    public partial class SettingsDialog : Form
    {
        private Color BackgroundColor
        {
            get => BackgroundColorPicker.Color;
            set => BackgroundColorPicker.Color = value;
        }
        private Color TextColor
        {
            get => TextColorPicker.Color;
            set => TextColorPicker.Color = value;
        }

        private bool SlowMemory
        {
            get => SlowMemoryCheck.Checked;
            set => SlowMemoryCheck.Checked = value;
        }
        private bool FileSystem
        {
            get => FileSystemCheck.Checked;
            set => FileSystemCheck.Checked = value;
        }

        // ---------------------------------

        private SettingsDialog()
        {
            InitializeComponent();
        }
        
        /// <summary>
        /// Prompts the user to change settings. Returns true if the user changed any settings
        /// </summary>
        public static bool Prompt()
        {
            // show the dialog
            using (SettingsDialog d = new SettingsDialog())
            {
                // load settings
                d.BackgroundColor = Program.BackgroundColor;
                d.TextColor = Program.TextColor;

                d.SlowMemory = Program.SlowMemory;
                d.FileSystem = Program.FileSystem;

                // and if the user said ok
                if (d.ShowDialog() == DialogResult.OK)
                {
                    // update settings
                    Program.BackgroundColor = d.BackgroundColor;
                    Program.TextColor = d.TextColor;

                    Program.SlowMemory = d.SlowMemory;
                    Program.FileSystem = d.FileSystem;

                    // save changes
                    Properties.Settings.Default.Save();

                    // report that settings were changed
                    return true;
                }
                // otherwise, report that no settings were changed
                else return false;
            }
        }

        // ---------------------------------

        private void HandlePreset(object sender, EventArgs e)
        {
            Button b = (Button)sender;

            BackgroundColor = b.BackColor;
            TextColor = b.ForeColor;
        }

        private void ColorSwapButton_Click(object sender, EventArgs e)
        {
            Color temp = BackgroundColor;
            BackgroundColor = TextColor;
            TextColor = temp;
        }
    }
}
