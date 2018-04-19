using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace csx64
{
    /// <summary>
    /// A colored box that can be clicked to open a color selection dialog
    /// </summary>
    public partial class ColorPicker : UserControl
    {
        /// <summary>
        /// Gets or sets the selected color
        /// </summary>
        public Color Color
        {
            get => BackColor;
            set => BackColor = value;
        }

        public ColorPicker()
        {
            InitializeComponent();
        }

        private void ColorPicker_Click(object sender, EventArgs e)
        {
            using (ColorDialog d = new ColorDialog())
            {
                d.Color = Color; // load current color

                // change settings
                d.FullOpen = true;

                // if user said ok, update color
                if (d.ShowDialog() == DialogResult.OK) Color = d.Color;
            }
        }
    }
}
