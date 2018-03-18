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
    public partial class Form1 : Form
    {
        private Computer C = new Computer();
        private ulong Ticks = 0;
        private bool Sim = false;

        private bool CInitialized
        {
            get => StopButton.Enabled;
            set
            {
                CompileButton.Enabled = !value;
                StopButton.Enabled = value;

                RunButton.Enabled = value;
                PauseButton.Enabled = false;

                TickButton.Enabled = Tick10Button.Enabled = Tick100Button.Enabled = Tick1000Button.Enabled = value;

                CodeBox.Enabled = !value;
            }
        }

        public Form1()
        {
            InitializeComponent();

            CInitialized = false;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            Graphics g = e.Graphics;

            float x, y;
            SolidBrush brush = new SolidBrush(Color.LimeGreen);
            Font font = new Font(FontFamily.GenericMonospace, 20);
            float h = 20;

            x = 0; y = 0;
            g.DrawString("Registers", font, brush, x, y); y += h;
            for (ulong i = 0; i < Computer.NRegisters; ++i)
            {
                y += h;
                g.DrawString($"R{i:x}: {C.GetRegister(i).x64:x16}", font, brush, x, y);
            }

            x = 400; y = 0;
            g.DrawString("Flags", font, brush, x, y); y += h * 2;
            g.DrawString($"Z: {(C.GetFlags().Z ? 1 : 0)}", font, brush, x, y); y += h;
            g.DrawString($"S: {(C.GetFlags().S ? 1 : 0)}", font, brush, x, y); y += h;
            g.DrawString($"P: {(C.GetFlags().P ? 1 : 0)}", font, brush, x, y); y += h;
            g.DrawString($"O: {(C.GetFlags().O ? 1 : 0)}", font, brush, x, y); y += h;
            g.DrawString($"C: {(C.GetFlags().C ? 1 : 0)}", font, brush, x, y); y += h;

            x = 550; y = 0;
            g.DrawString("State", font, brush, x, y); y += h * 2;
            g.DrawString($"Pos: {C.Pos:x16}", font, brush, x, y); y += h;
            g.DrawString($"Exe: {(C.Running ? 1 : 0)}", font, brush, x, y); y += h;
            g.DrawString($"Err: {C.Error}", font, brush, x, y); y += h;
            g.DrawString($"T  : {Ticks:x16}", font, brush, x, y); y += h;
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
            Computer.ObjectFile file;
            var assemble_res = Computer.Assemble(CodeBox.Text, out file);
            if (assemble_res.Item1 != Computer.AssembleError.None) { MessageBox.Show(assemble_res.Item2, "Assemble Error"); return; }

            byte[] exe = null;
            var link_res = Computer.Link(8, ref exe, file);
            if (link_res.Item1 != Computer.LinkError.None) { MessageBox.Show(link_res.Item2, "Link Error"); return; }

            /*
            StringBuilder b = new StringBuilder();
            for (int i = 0; i < exe.Length; ++i)
                b.Append($"{i.ToString().PadLeft(3, '0')}: {exe[i].ToString().PadLeft(3, '0')} - {Convert.ToString(exe[i], 2).PadLeft(8, '0')}\n");
            MessageBox.Show(b.ToString());
            */

            C.Initialize(exe);
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
    }
}
