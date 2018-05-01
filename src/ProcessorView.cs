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
        private CSX64 Computer;
        /// <summary>
        /// The number of ticks that have elapsed
        /// </summary>
        private ulong Ticks = 0;

        private SolidBrush DebuggingBrush = new SolidBrush(Color.LimeGreen);
        private Font DebuggingFont = new Font(FontFamily.GenericMonospace, 20);

        public Color TextColor
        {
            get => DebuggingBrush.Color;
            set => DebuggingBrush.Color = value;
        }

        // --------------------------------------

        /// <summary>
        /// Creates a new processor view to monitor the specified computer
        /// </summary>
        /// <param name="computer">the computer to monitor</param>
        public ProcessorView(CSX64 computer)
        {
            InitializeComponent();

            // assign computer to monitor
            Computer = computer;
        }

        // --------------------------------------

        private bool __Disposed = false;
        protected override void Dispose(bool disposing)
        {
            if (!__Disposed)
            {
                if (disposing)
                {
                    components?.Dispose(); // from auto-generated code (they hinted it might be null)

                    // -- free things we allocated -- //

                    DebuggingBrush.Dispose();
                    DebuggingFont.Dispose();
                }

                // ensure base dispose is called
                base.Dispose(disposing);
                __Disposed = true;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            // alias graphics object and flags register
            Graphics g = e.Graphics;
            CSX64.FlagsRegister f = Computer.GetFlags();

            // render position / settings
            float x, y;
            float h = 20;

            // -- registers -- //

            x = 0; y = 0;
            g.DrawString("Registers", DebuggingFont, DebuggingBrush, x, y); y += h;
            for (int i = 0; i < 16; ++i)
                g.DrawString($"R{i:x}: {Computer.GetRegister(i).x64:x16}", DebuggingFont, DebuggingBrush, x, y += h);

            // -- flags -- //

            x = 400; y = 0;
            g.DrawString("Flags", DebuggingFont, DebuggingBrush, x, y); y += h;
            g.DrawString($"Z:  {(f.Z ? 1 : 0)}", DebuggingFont, DebuggingBrush, x, y += h);
            g.DrawString($"S:  {(f.S ? 1 : 0)}", DebuggingFont, DebuggingBrush, x, y += h);
            g.DrawString($"P:  {(f.P ? 1 : 0)}", DebuggingFont, DebuggingBrush, x, y += h);
            g.DrawString($"O:  {(f.O ? 1 : 0)}", DebuggingFont, DebuggingBrush, x, y += h);
            g.DrawString($"C:  {(f.C ? 1 : 0)}", DebuggingFont, DebuggingBrush, x, y += h);

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

            // -- state -- //

            x = 550; y = 0;
            g.DrawString("State", DebuggingFont, DebuggingBrush, x, y); y += h;
            g.DrawString($"Pos: {Computer.Pos:x16}", DebuggingFont, DebuggingBrush, x, y += h);
            g.DrawString($"Exe: {(Computer.Running ? 1 : 0)}", DebuggingFont, DebuggingBrush, x, y += h);
            g.DrawString($"Slp: {Computer.Sleep:x16}", DebuggingFont, DebuggingBrush, x, y += h);
            g.DrawString($"Err: {Computer.Error}", DebuggingFont, DebuggingBrush, x, y += h);
            g.DrawString($"T  : {Ticks:x16}", DebuggingFont, DebuggingBrush, x, y += h);
        }
    }
}
