using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CSX64
{
    public partial class CodeBox : UserControl
    {
        /// <summary>
        /// Gets or sets the code to display in the editor
        /// </summary>
        public string Code
        {
            get => RawBox.Text;
            set => RawBox.Text = value;
        }

        public CodeBox()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Remaps some controls to make it more code-friendly
        /// </summary>
        private void RawBox_KeyDown(object sender, KeyEventArgs e)
        {
            // support for select all
            if (e.Control && e.KeyCode == Keys.A)
            {
                RawBox.SelectAll();
                e.SuppressKeyPress = true;
            }
            // replace tabs with spaces
            else if (e.KeyCode == Keys.Tab)
            {
                RawBox.Paste("    ");
                e.SuppressKeyPress = true;
            }
            // copy current line spacing on return
            else if (e.KeyCode == Keys.Return)
            {
                int end = RawBox.SelectionStart;
                int start = 0, count;

                if (end > 0)
                {
                    for (start = end - 1; start > 0 && RawBox.Text[start] != '\n'; --start) ;
                    if (RawBox.Text[start] == '\n') ++start;
                }

                for (count = 0; start + count < end && RawBox.Text[start + count] == ' '; ++count) ;

                RawBox.Paste("\r\n" + RawBox.Text.Substring(start, count));
                e.SuppressKeyPress = true;
            }
        }
    }
}
