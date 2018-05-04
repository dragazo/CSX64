using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CSX64
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

        /// <summary>
        /// The context menu used for program tabs
        /// </summary>
        private ContextMenuStrip Context;

        public CodeEditor()
        {
            InitializeComponent();

            // create the context menu
            Context = new ContextMenuStrip();
            // create the context menu
            Context.Items.Add("New Program", null, (o, e) => NewProgram());
            Context.Items.Add("Remove", null, (o, e) => { MainTabs.TabPages.Remove((TabPage)Context.SourceControl); if (MainTabs.TabCount == 0) NewProgram(); });
            Context.Items.Add("Rename", null, (o, e) =>
            {
                TabPage tab = (TabPage)Context.SourceControl;
                using (RenameDialog d = new RenameDialog() { Result = tab.Text })
                    if (d.ShowDialog() == DialogResult.OK) tab.Text = d.Result;
            });
            
            // create the first program (cannot have zero files open)
            NewProgram();
        }
        ~CodeEditor()
        {
            Context.Dispose();
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
                        // display the context menu on that tab
                        Context.Show(MainTabs.TabPages[i], 0, 0);
                        break;
                    }
            }
        }
    }
}
