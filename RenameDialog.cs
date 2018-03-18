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
    public partial class RenameDialog : Form
    {
        /// <summary>
        /// Gets or sets the text to be displayed in the rename box
        /// </summary>
        public string Result
        {
            get => ResultBox.Text;
            set => ResultBox.Text = value;
        }

        public RenameDialog()
        {
            InitializeComponent();
        }
    }
}
