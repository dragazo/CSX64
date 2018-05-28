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

namespace CSX64
{
    /// <summary>
    /// Represents a debugging window for a CSX64 processor, as well as an editor and compiler for CSX64 assembly
    /// </summary>
    public partial class ProcessorView : Form
    {
        /// <summary>
        /// The processor to display debugging data for
        /// </summary>
        private Computer Computer;
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
        public ProcessorView(Computer computer)
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

            // render position / settings
            float x, y;
            float h = 20;

            // -- registers -- //

            x = 0; y = 0;
            g.DrawString("Registers", DebuggingFont, DebuggingBrush, x, y); y += h;
            g.DrawString($"RAX: {Computer.RAX:x16}", DebuggingFont, DebuggingBrush, x, y += h);
            g.DrawString($"RBX: {Computer.RBX:x16}", DebuggingFont, DebuggingBrush, x, y += h);
            g.DrawString($"RCX: {Computer.RCX:x16}", DebuggingFont, DebuggingBrush, x, y += h);
            g.DrawString($"RDX: {Computer.RDX:x16}", DebuggingFont, DebuggingBrush, x, y += h);
            g.DrawString($"RSI: {Computer.RSI:x16}", DebuggingFont, DebuggingBrush, x, y += h);
            g.DrawString($"RDI: {Computer.RDI:x16}", DebuggingFont, DebuggingBrush, x, y += h);
            g.DrawString($"RBP: {Computer.RBP:x16}", DebuggingFont, DebuggingBrush, x, y += h);
            g.DrawString($"RSP: {Computer.RSP:x16}", DebuggingFont, DebuggingBrush, x, y += h);
            g.DrawString($"R8:  {Computer.R8:x16}", DebuggingFont, DebuggingBrush, x, y += h);
            g.DrawString($"R9:  {Computer.R9:x16}", DebuggingFont, DebuggingBrush, x, y += h);
            g.DrawString($"R10: {Computer.R10:x16}", DebuggingFont, DebuggingBrush, x, y += h);
            g.DrawString($"R11: {Computer.R11:x16}", DebuggingFont, DebuggingBrush, x, y += h);
            g.DrawString($"R12: {Computer.R12:x16}", DebuggingFont, DebuggingBrush, x, y += h);
            g.DrawString($"R13: {Computer.R13:x16}", DebuggingFont, DebuggingBrush, x, y += h);
            g.DrawString($"R14: {Computer.R14:x16}", DebuggingFont, DebuggingBrush, x, y += h);
            g.DrawString($"R15: {Computer.R15:x16}", DebuggingFont, DebuggingBrush, x, y += h);

            // -- flags -- //
            
            x = 400; y = 0;
            g.DrawString("Flags", DebuggingFont, DebuggingBrush, x, y); y += h;
            g.DrawString($"Z:  {(Computer.ZF ? 1 : 0)}", DebuggingFont, DebuggingBrush, x, y += h);
            g.DrawString($"S:  {(Computer.SF ? 1 : 0)}", DebuggingFont, DebuggingBrush, x, y += h);
            g.DrawString($"P:  {(Computer.PF ? 1 : 0)}", DebuggingFont, DebuggingBrush, x, y += h);
            g.DrawString($"O:  {(Computer.OF ? 1 : 0)}", DebuggingFont, DebuggingBrush, x, y += h);
            g.DrawString($"C:  {(Computer.CF ? 1 : 0)}", DebuggingFont, DebuggingBrush, x, y += h);

            y += h;
            g.DrawString($"a:  {(Computer.a ? 1 : 0)}", DebuggingFont, DebuggingBrush, x, y += h);
            g.DrawString($"ae: {(Computer.ae ? 1 : 0)}", DebuggingFont, DebuggingBrush, x, y += h);
            g.DrawString($"b:  {(Computer.b ? 1 : 0)}", DebuggingFont, DebuggingBrush, x, y += h);
            g.DrawString($"be: {(Computer.be ? 1 : 0)}", DebuggingFont, DebuggingBrush, x, y += h);

            y += h;
            g.DrawString($"g:  {(Computer.g ? 1 : 0)}", DebuggingFont, DebuggingBrush, x, y += h);
            g.DrawString($"ge: {(Computer.ge ? 1 : 0)}", DebuggingFont, DebuggingBrush, x, y += h);
            g.DrawString($"l:  {(Computer.l ? 1 : 0)}", DebuggingFont, DebuggingBrush, x, y += h);
            g.DrawString($"le: {(Computer.le ? 1 : 0)}", DebuggingFont, DebuggingBrush, x, y += h);

            // -- state -- //

            x = 550; y = 0;
            g.DrawString("State", DebuggingFont, DebuggingBrush, x, y); y += h;
            g.DrawString($"Pos: {Computer.RIP:x16}", DebuggingFont, DebuggingBrush, x, y += h);
            g.DrawString($"Exe: {(Computer.Running ? 1 : 0)}", DebuggingFont, DebuggingBrush, x, y += h);
            g.DrawString($"Err: {Computer.Error}", DebuggingFont, DebuggingBrush, x, y += h);
            g.DrawString($"T  : {Ticks:x16}", DebuggingFont, DebuggingBrush, x, y += h);
        }
    }
}
