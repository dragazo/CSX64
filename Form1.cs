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
        private CSX64 C = new CSX64();
        private ulong Ticks = 0;
        private bool Sim = false;

        private bool CInitialized
        {
            get => StopButton.Enabled;
            set
            {
                CompileButton.Enabled = GraphicalButton.Enabled = !value;
                StopButton.Enabled = value;

                RunButton.Enabled = value;
                PauseButton.Enabled = false;

                TickButton.Enabled = Tick10Button.Enabled = Tick100Button.Enabled = Tick1000Button.Enabled = value;

                CodeBox.ReadOnly = value;
            }
        }

        public ProcessorView()
        {
            InitializeComponent();

            CInitialized = false;
            CodeBox.Font = new Font(FontFamily.GenericMonospace, 12);
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

            x = 0; y = 0;
            g.DrawString("Registers", DebuggingFont, DebuggingBrush, x, y); y += h;
            for (ulong i = 0; i < 16; ++i)
                g.DrawString($"R{i:x}: {C.GetRegister(i).x64:x16}", DebuggingFont, DebuggingBrush, x, y += h);

            x = 400; y = 0;
            g.DrawString("Flags", DebuggingFont, DebuggingBrush, x, y); y += h;
            g.DrawString($"Z: {(f.Z ? 1 : 0)}", DebuggingFont, DebuggingBrush, x, y += h);
            g.DrawString($"S: {(f.S ? 1 : 0)}", DebuggingFont, DebuggingBrush, x, y += h);
            g.DrawString($"P: {(f.P ? 1 : 0)}", DebuggingFont, DebuggingBrush, x, y += h);
            g.DrawString($"O: {(f.O ? 1 : 0)}", DebuggingFont, DebuggingBrush, x, y += h);
            g.DrawString($"C: {(f.C ? 1 : 0)}", DebuggingFont, DebuggingBrush, x, y += h);

            x = 550; y = 0;
            g.DrawString("State", DebuggingFont, DebuggingBrush, x, y); y += h;
            g.DrawString($"Pos: {C.Pos:x16}", DebuggingFont, DebuggingBrush, x, y += h);
            g.DrawString($"Exe: {(C.Running ? 1 : 0)}", DebuggingFont, DebuggingBrush, x, y += h);
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
            CSX64.ObjectFile file;
            var assemble_res = CSX64.Assemble(CodeBox.Text, out file);
            if (assemble_res.Item1 != CSX64.AssembleError.None) { MessageBox.Show(assemble_res.Item2, "Assemble Error"); return; }

            byte[] exe = null;
            var link_res = CSX64.Link(ref exe, file);
            if (link_res.Item1 != CSX64.LinkError.None) { MessageBox.Show(link_res.Item2, "Link Error"); return; }

            /*
            StringBuilder b = new StringBuilder();
            for (int i = 0; i < exe.Length; ++i)
                b.Append($"{i.ToString().PadLeft(3, '0')}: {exe[i].ToString().PadLeft(3, '0')} - {Convert.ToString(exe[i], 2).PadLeft(8, '0')}\n");
            MessageBox.Show(b.ToString());
            */
            
            C = new CSX64();
            if (!C.Initialize(exe)) { MessageBox.Show("Something went wrong initializing the program", "Initialization Error"); return; }
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

        private void CodeBox_KeyDown(object sender, KeyEventArgs e)
        {
            // support for select all
            if (e.Control && e.KeyCode == Keys.A)
            {
                CodeBox.SelectAll();
                e.SuppressKeyPress = true;
            }
            // replace tabs with spaces
            else if (e.KeyCode == Keys.Tab)
            {
                CodeBox.Paste("    ");
                e.SuppressKeyPress = true;
            }
            // copy current line spacing on return
            else if (e.KeyCode == Keys.Return)
            {
                int end = CodeBox.SelectionStart;
                int start = 0, count;

                if (end > 0)
                {
                    for (start = end - 1; start > 0 && CodeBox.Text[start] != '\n'; --start) ;
                    if (CodeBox.Text[start] == '\n') ++start;
                }

                for (count = 0; start + count < end && CodeBox.Text[start + count] == ' '; ++count) ;

                CodeBox.Paste("\r\n" + CodeBox.Text.Substring(start, count));
                e.SuppressKeyPress = true;
            }
        }

        private void TestsButton_Click(object sender, EventArgs e)
        {
            CSX64.FlagsRegister f = C.GetFlags();
            MessageBox.Show($"a: {(f.a ? 1 : 0)}\nae: {(f.ae ? 1 : 0)}\nb: {(f.b ? 1 : 0)}\nbe: {(f.be ? 1 : 0)}\n" +
                $"g: {(f.g ? 1 : 0)}\nge: {(f.ge ? 1 : 0)}\nl: {(f.l ? 1 : 0)}\nle: {(f.le ? 1 : 0)}");
        }

        private void GraphicalButton_Click(object sender, EventArgs e)
        {
            CSX64.ObjectFile obj;
            var assemble_res = CSX64.Assemble(CodeBox.Text, out obj);
            if (assemble_res.Item1 != CSX64.AssembleError.None) { MessageBox.Show(assemble_res.Item2, "Assemble Error"); return; }

            byte[] exe = null;
            var link_res = CSX64.Link(ref exe, obj);
            if (link_res.Item1 != CSX64.LinkError.None) { MessageBox.Show(link_res.Item2, "Link Error"); return; }

            GraphicalDisplay g = new GraphicalDisplay();
            if (!g.C.Initialize(exe)) { MessageBox.Show("Something went wrong initializing the program", "Initialization Error"); return; }
            C = g.C; // link computers for local logging

            g.OnTickCycle += ExternRenderCycle; // sync tick cycles
            g.Run();                            // begin execution
            g.ShowDialog();                     // display graphical client

            g.C.Fail(CSX64.ErrorCode.Abort); // cancellation results in an abort error
        }
    }
}
