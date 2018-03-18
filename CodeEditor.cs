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
    public partial class CodeEditor : Form
    {
        /// <summary>
        /// Gets an iterator over all the open programs in the editor. Tuples: (file name, code)
        /// </summary>
        public IEnumerable<Tuple<string, string>> Programs
        {
            get
            {
                foreach (TabPage item in MainTabs.TabPages)
                    yield return new Tuple<string, string>(item.Text, ((CodeBox)item.Controls[0]).Code);
            }
        }

        public CodeEditor()
        {
            InitializeComponent();

            NewProgram();
        }

        /// <summary>
        /// Creates a new program
        /// </summary>
        private void NewProgram()
        {
            // create the tab
            TabPage tab = new TabPage("Untitled");
            MainTabs.TabPages.Add(tab);

            // give it a code box
            CodeBox box = new CodeBox();
            box.Parent = tab;
            box.Dock = DockStyle.Fill;

            // create its context menu
            ContextMenuStrip m = tab.ContextMenuStrip = new ContextMenuStrip();

            // give it menu items
            m.Items.Add("Add", null, (o, e) => NewProgram());
            m.Items.Add("Remove", null, (o, e) => { MainTabs.TabPages.Remove(tab); if (MainTabs.TabCount == 0) NewProgram(); });
            m.Items.Add("Rename", null, (o, e) =>
            {
                using (RenameDialog d = new RenameDialog() { Result = tab.Text })
                    if (d.ShowDialog() == DialogResult.OK) tab.Text = d.Result;
            });
        }

        /// <summary>
        /// Handles displaying context menus for tabs
        /// </summary>
        private void MainTabs_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                // look for the tab we right clicked on
                for (int i = 0; i < MainTabs.TabCount; ++i)
                    if (MainTabs.GetTabRect(i).Contains(e.Location))
                    {
                        MainTabs.TabPages[i].ContextMenuStrip.Show(MainTabs.TabPages[i], 0, 0);
                        break;
                    }
            }
        }
    }
}
