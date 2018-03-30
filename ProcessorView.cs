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
    public partial class ProcessorView : Form
    {
        private CSX64 C = new GraphicalComputer(); // initialized with graphical computer to ensure its static ctor executes
        private ulong Ticks = 0;
        private bool Sim = false;

        /// <summary>
        /// The code editor used by this processor (contains all the assembly files)
        /// </summary>
        private CodeEditor Editor;

        private bool CInitialized
        {
            get => StopButton.Enabled;
            set
            {
                CompileButton.Enabled = GraphicalButton.Enabled = !value;
                //SlowMemCheck.Enabled = !value; render bug causes text to be black, which in this case makes it unreadable

                StopButton.Enabled = value;

                RunButton.Enabled = value;
                PauseButton.Enabled = false;

                TickButton.Enabled = Tick10Button.Enabled = Tick100Button.Enabled = Tick1000Button.Enabled = value;
            }
        }

        public ProcessorView()
        {
            InitializeComponent();

            // mark as not executing
            CInitialized = false;

            // create the code editor
            Editor = new CodeEditor();
            // rebind form close to instead just hide
            Editor.FormClosing += (o, e) => { e.Cancel = true; Editor.Hide(); };
            // set fort
            Editor.Font = new Font(FontFamily.GenericMonospace, 12);

            // load the settings file
            LoadSettings();
        }
        ~ProcessorView()
        {
            Editor.Dispose();
        }

        /// <summary>
        /// Loads data from the settings file
        /// </summary>
        private void LoadSettings()
        {
            BackColor = Properties.Settings.Default.ProcessorBackColor;
            DebuggingBrush.Color = Properties.Settings.Default.ProcessorForeColor;

            // make sure to redraw form after loading settings
            Invalidate();
        }

        private static SolidBrush DebuggingBrush = new SolidBrush(Color.LimeGreen);
        private static Font DebuggingFont = new Font(FontFamily.GenericMonospace, 20);
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            Graphics g = e.Graphics;
            CSX64.FlagsRegister f = C.GetFlags();

            float x, y;
            float h = 20;

            // registers
            x = 0; y = 0;
            g.DrawString("Registers", DebuggingFont, DebuggingBrush, x, y); y += h;
            for (int i = 0; i < 16; ++i)
                g.DrawString($"R{i:x}: {C.GetRegister(i).x64:x16}", DebuggingFont, DebuggingBrush, x, y += h);

            // flags
            x = 400; y = 0;
            g.DrawString("Flags", DebuggingFont, DebuggingBrush, x, y); y += h;
            g.DrawString($"Z:  {(f.Z ? 1 : 0)}", DebuggingFont, DebuggingBrush, x, y += h);
            g.DrawString($"P:  {(f.P ? 1 : 0)}", DebuggingFont, DebuggingBrush, x, y += h);
            g.DrawString($"O:  {(f.O ? 1 : 0)}", DebuggingFont, DebuggingBrush, x, y += h);
            g.DrawString($"C:  {(f.C ? 1 : 0)}", DebuggingFont, DebuggingBrush, x, y += h);
            g.DrawString($"S:  {(f.S ? 1 : 0)}", DebuggingFont, DebuggingBrush, x, y += h);

            y += h;
            g.DrawString($"a:  {(f.a ? 1 : 0)}", DebuggingFont, DebuggingBrush, x, y += h);
            g.DrawString($"ae: {(f.ae ? 1 : 0)}", DebuggingFont, DebuggingBrush, x, y += h);
            g.DrawString($"b:  {(f.b ? 1 : 0)}", DebuggingFont, DebuggingBrush, x, y += h);
            g.DrawString($"be: {(f.be ? 1 : 0)}", DebuggingFont, DebuggingBrush, x, y += h);

            y += h;
            g.DrawString($"g:  {(f.g ? 1 : 0)}", DebuggingFont, DebuggingBrush, x, y += h);
            g.DrawString($"ge: {(f.ge ? 1 : 0)}", DebuggingFont, DebuggingBrush, x, y += h);
            g.DrawString($"l:  {(f.l ? 1 : 0)}", DebuggingFont, DebuggingBrush, x, y += h);
            g.DrawString($"le: {(f.le ? 1 : 0)}", DebuggingFont, DebuggingBrush, x, y += h);

            // state
            x = 550; y = 0;
            g.DrawString("State", DebuggingFont, DebuggingBrush, x, y); y += h;
            g.DrawString($"Pos: {C.Pos:x16}", DebuggingFont, DebuggingBrush, x, y += h);
            g.DrawString($"Exe: {(C.Running ? 1 : 0)}", DebuggingFont, DebuggingBrush, x, y += h);
            g.DrawString($"Slp: {C.Sleep:x16}", DebuggingFont, DebuggingBrush, x, y += h);
            g.DrawString($"Err: {C.Error}", DebuggingFont, DebuggingBrush, x, y += h);
            g.DrawString($"T  : {Ticks:x16}", DebuggingFont, DebuggingBrush, x, y += h);
        }

        private void Tick(ulong n)
        {
            ulong i;
            for (i = 0; i < n && C.Tick(); ++i) ;
            Ticks += i;

            Invalidate();
        }

        private void TickButton_Click(object sender, EventArgs e) => Tick(1);
        private void Tick10Button_Click(object sender, EventArgs e) => Tick(10);
        private void Tick100Button_Click(object sender, EventArgs e) => Tick(100);
        private void Tick1000Button_Click(object sender, EventArgs e) => Tick(1000);

        private void CompileButton_Click(object sender, EventArgs e)
        {
            // get the source files
            Tuple<string, string>[] source = Editor.Programs.ToArray();

            // assemble them
            CSX64.ObjectFile[] objs = new CSX64.ObjectFile[source.Length];
            for (int i = 0; i < source.Length; ++i)
            {
                CSX64.AssembleResult res = CSX64.Assemble(source[i].Item2, out objs[i]);
                if (res.Error != CSX64.AssembleError.None) { MessageBox.Show(res.ErrorMsg, $"Assemble Error in \"{source[i].Item1}\""); return; }
            }

            // link them
            CSX64.LinkResult link_res = CSX64.Link(out byte[] exe, objs);
            if (link_res.Error != CSX64.LinkError.None) { MessageBox.Show(link_res.ErrorMsg, "Link Error"); return; }

            C.Dispose();
            C = new CSX64();
            if (!C.Initialize(exe)) { MessageBox.Show("Something went wrong initializing the program", "Initialization Error"); return; }

            // set private flags
            C.GetFlags().SlowMem = SlowMemCheck.Checked;

            CInitialized = true;
            Ticks = 0;

            Invalidate();
        }

        private async void RunButton_Click(object sender, EventArgs e)
        {
            RunButton.Enabled = false;
            PauseButton.Enabled = true;

            Sim = true;
            while (Sim && C.Running)
            {
                Tick(10000);
                Invalidate();

                await Task.Delay(10);
            }

            RunButton.Enabled = true;
            PauseButton.Enabled = false;
        }
        private void ExternRenderCycle(UInt64 ticks)
        {
            Ticks = ticks;
            Invalidate();
        }

        private void PauseButton_Click(object sender, EventArgs e)
        {
            Sim = false;
        }

        private async void StopButton_Click(object sender, EventArgs e)
        {
            // stop simulation and wait for it to end
            Sim = false;
            while (PauseButton.Enabled) await Task.Delay(1);

            CInitialized = false;
        }

        private void GraphicalButton_Click(object sender, EventArgs e)
        {
            // get the source files
            Tuple<string, string>[] source = Editor.Programs.ToArray();

            // assemble them
            CSX64.ObjectFile[] objs = new CSX64.ObjectFile[source.Length];
            for (int i = 0; i < source.Length; ++i)
            {
                CSX64.AssembleResult res = CSX64.Assemble(source[i].Item2, out objs[i]);
                if (res.Error != CSX64.AssembleError.None) { MessageBox.Show(res.ErrorMsg, $"Assemble Error in \"{source[i].Item1}\""); return; }
            }

            // link them
            CSX64.LinkResult link_res = CSX64.Link(out byte[] exe, objs);
            if (link_res.Error != CSX64.LinkError.None) { MessageBox.Show(link_res.ErrorMsg, "Link Error"); return; }

            using (GraphicalDisplay g = new GraphicalDisplay())
            {
                if (!g.C.Initialize(exe)) { MessageBox.Show("Something went wrong initializing the program", "Initialization Error"); return; }
                C.Dispose();
                C = g.C; // link computers for local logging

                // set private flags
                C.GetFlags().SlowMem = SlowMemCheck.Checked;

                g.OnTickCycle += ExternRenderCycle; // sync tick cycles
                g.Run();                            // begin execution
                g.ShowDialog();                     // display graphical client
                
                C.Fail(CSX64.ErrorCode.Abort); // cancellation results in an abort error
            }
        }

        private void EditButton_Click(object sender, EventArgs e)
        {
            Editor.Show();
            Editor.Focus();
        }

        private void SettingsButton_Click(object sender, EventArgs e)
        {
            using (ProcessorViewSettingsDialog d = new ProcessorViewSettingsDialog())
            {
                // load settings
                d.BackgroundColor = BackColor;
                d.ForegroundColor = DebuggingBrush.Color;

                // if user says ok
                if (d.ShowDialog() == DialogResult.OK)
                {
                    // updates settings
                    Properties.Settings.Default.ProcessorBackColor = d.BackgroundColor;
                    Properties.Settings.Default.ProcessorForeColor = d.ForegroundColor;
                    
                    // save
                    Properties.Settings.Default.Save();

                    // reload settings
                    LoadSettings();
                }
            }
        }
    }
}
