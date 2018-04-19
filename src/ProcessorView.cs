using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;

namespace csx64
{
    /// <summary>
    /// Represents a debugging window for a CSX64 processor, as well as an editor and compiler for CSX64 assembly
    /// </summary>
    public partial class ProcessorView : Form
    {
        /// <summary>
        /// The processor to display debugging data for
        /// </summary>
        private CSX64 C;

        private ulong Ticks = 0;
        private bool Sim = false;

        private CodeEditor Editor;
        private CSX64 RawCSX64;
        private ConsoleDisplay Console;
        private GraphicalDisplay Graphical;

        private SolidBrush DebuggingBrush = new SolidBrush(Color.LimeGreen);
        private Font DebuggingFont = new Font(FontFamily.GenericMonospace, 20);

        // --------------------------------------

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
            Editor.FormClosing += (o, e) => { e.Cancel = true; Editor.Hide(); };
            Editor.Font = new Font(FontFamily.GenericMonospace, 12);

            // create the raw CSX64 (only used for debugging mode i.e. no console, no graphial client)
            C = RawCSX64 = new CSX64();

            // create the console display
            Console = new ConsoleDisplay();
            Console.OnTickCycle += ExternRenderCycle;

            // create the graphical display
            Graphical = new GraphicalDisplay();
            Graphical.OnTickCycle += ExternRenderCycle;

            // load the settings file
            LoadSettings();
        }

        private bool disposed = false;
        protected override void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    components?.Dispose(); // from auto-generated code (they hinted it might be null)

                    // -- free things we allocated -- //

                    Editor.Dispose();
                    RawCSX64.Dispose();
                    Graphical.Dispose();
                    Console.Dispose();

                    DebuggingBrush.Dispose();
                    DebuggingFont.Dispose();
                }

                // ensure base dispose is called
                base.Dispose(disposing);
                disposed = true;
            }
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

        /// <summary>
        /// Ticks the processor by the specified number of cycles, then updates the debugging data
        /// </summary>
        /// <param name="n">the number of cycles to advance by</param>
        private void Tick(ulong n)
        {
            uint i;
            for (i = 0; i < n && C.Tick(); ++i) ;
            Ticks += i;

            Invalidate();
        }

        private void TickButton_Click(object sender, EventArgs e) => Tick(1);
        private void Tick10Button_Click(object sender, EventArgs e) => Tick(10);
        private void Tick100Button_Click(object sender, EventArgs e) => Tick(100);
        private void Tick1000Button_Click(object sender, EventArgs e) => Tick(1000);

        /// <summary>
        /// Initializes the specified processor for execution. Run this before beginning execution of any simulators
        /// </summary>
        /// <param name="processor">the processor to initialize and begin debugging for</param>
        private bool InitializeComputer(CSX64 processor)
        {
            // get the source files
            Tuple<string, string>[] source = Editor.Programs.ToArray();

            // assemble them
            CSX64.ObjectFile[] objs = new CSX64.ObjectFile[source.Length];
            for (int i = 0; i < source.Length; ++i)
            {
                CSX64.AssembleResult res = CSX64.Assemble(source[i].Item2, out objs[i]);
                if (res.Error != CSX64.AssembleError.None) { MessageBox.Show(res.ErrorMsg, $"Assemble Error in \"{source[i].Item1}\""); return false; }
            }

            // link them
            CSX64.LinkResult link_res = CSX64.Link(out byte[] exe, objs);
            if (link_res.Error != CSX64.LinkError.None) { MessageBox.Show(link_res.ErrorMsg, "Link Error"); return false; }

            C = processor; // set debugging processor

            // load the program
            if (!C.Initialize(exe)) { MessageBox.Show("Something went wrong initializing the program", "Initialization Error"); return false; }

            // set private flags
            C.GetFlags().SlowMem = SlowMemCheck.Checked;
            C.GetFlags().FileSystem = FileSystemCheck.Checked;

            // successfully initialized
            return true;
        }

        private void CompileButton_Click(object sender, EventArgs e)
        {
            // initialize raw computer
            if (!InitializeComputer(RawCSX64)) return;

            // set execution data
            CInitialized = true;
            Ticks = 0;

            // update debugging data
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

            // reset form controls (not waiting before this can cause some button enabled states to have race conditions)
            CInitialized = false;
        }

        private void GraphicalButton_Click(object sender, EventArgs e)
        {
            // initialize graphical computer
            if (!InitializeComputer(Graphical.C)) return;

            Graphical.ShowDialog(); // begin program execution
            C.Terminate(CSX64.ErrorCode.Abort); // abort execution (otherwise it'll be running in the background)
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

        private void ConsoleButton_Click(object sender, EventArgs e)
        {
            // initialize console computer
            if (!InitializeComputer(Console.C)) return;

            Console.BackColor = BackColor;
            Console.TextColor = DebuggingBrush.Color;

            using (Stream stdin = new MemoryStream(), stdout = new MemoryStream())
            {
                Console.SetupStdio(stdin, stdout, stdout, true); // set up streams for execution
                Console.ShowDialog(); // begin program execution
            }

            C.Terminate(CSX64.ErrorCode.Abort); // abort execution (otherwise it'll be running in the background)
        }
    }
}
