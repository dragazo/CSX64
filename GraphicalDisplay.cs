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
    public partial class GraphicalDisplay : Form
    {
        public GraphicalComputer C = new GraphicalComputer();
        public UInt64 Ticks = 0;

        public GraphicalDisplay()
        {
            InitializeComponent();


            OnResize(EventArgs.Empty);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            // render the graphics object over the display surface
            e.Graphics.DrawImage(C.GraphicSurface, Point.Empty);
        }
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);

            // create a new graphic surface
            C.GraphicSurface?.Dispose();
            C.GraphicSurface = new Bitmap(DisplayRectangle.Width, DisplayRectangle.Height);

            C.NeedsRender = true; // mark that we need a render
        }

        public event Action<UInt64> OnTickCycle = null;
        public async void Run()
        {
            Text = "Running";
            Random r = new Random();
            while (C.Running)
            {
                await Task.Delay(10);

                // tick processor
                int i;
                for (i = 0; i < 10000; ++i)
                {
                    C.Tick();

                    // acount for re-rendering
                    if (C.Invalidated)
                    {
                        // mark render requests as filled
                        C.Invalidated = false;
                        C.NeedsRender = false;

                        // redraw the form
                        Invalidate();
                    }
                }
                Ticks += (UInt64)i;

                OnTickCycle?.Invoke(Ticks);
            }

            Text = $"Terminated - Error Code: {C.Error}";
        }
    }

    public class GraphicalComputer : Computer
    {
        // the surface to render
        public Bitmap GraphicSurface = null;

        public bool Invalidated = false; // flag for if the processor has finished re-rendering
        public bool NeedsRender = false; // flag for if the virtual operating system recommends re-rendering

        protected override bool Syscall()
        {
            UInt64 val = 0; // parsing destination

            Brush brush = null; // brush to use for filling
            Pen pen = null;     // pen to use for drawing
            Font font = null;   // font to use for drawing strings

            Rectangle rect = new Rectangle();
            Point point = new Point();
            string str = null;

            bool ret = true; // return value (stored here because we need to dispose everything before returning)

            // register 0 contains a 64-bit syscall code
            switch (GetRegister(0).x64)
            {
                case 0: // get graphic surface dimensions
                    GetRegister(1).x64 = (UInt64)GraphicSurface.Width;
                    GetRegister(2).x64 = (UInt64)GraphicSurface.Height;
                    break;
                case 1: // ask if render needed (e.g. window resize)
                    GetRegister(1).x8 = NeedsRender ? 1 : 0ul;
                    break;
                case 2: // render
                    Invalidated = true;
                    break;

                // -- drawing utilities --

                case 3: // clear ($1 32:color)
                    if (GraphicSurface == null || !GetMem(GetRegister(1).x64, 4, ref val)) { ret = false; break; }
                    using (Graphics g = Graphics.FromImage(GraphicSurface)) g.Clear(Color.FromArgb((int)val));
                    break;

                case 4: // fill rect ($1 brush) ($2 rect)
                    if (GraphicSurface == null || !GetBrush(GetRegister(1).x64, ref brush) || !GetRect(GetRegister(2).x64, ref rect)) { ret = false; break; }
                    using (Graphics g = Graphics.FromImage(GraphicSurface)) g.FillRectangle(brush, rect);
                    break;
                case 5: // draw rect ($1 pen) ($2 rect)
                    if (GraphicSurface == null || !GetPen(GetRegister(1).x64, ref pen) || !GetRect(GetRegister(2).x64, ref rect)) { ret = false; break; }
                    using (Graphics g = Graphics.FromImage(GraphicSurface)) g.DrawRectangle(pen, rect);
                    break;

                case 6: // fill ellipse ($1 brush) ($2 rect)
                    if (GraphicSurface == null || !GetBrush(GetRegister(1).x64, ref brush) || !GetRect(GetRegister(2).x64, ref rect)) { ret = false; break; }
                    using (Graphics g = Graphics.FromImage(GraphicSurface)) g.FillEllipse(brush, rect);
                    break;
                case 7: // draw ellipse ($1 pen) ($2 rect)
                    if (GraphicSurface == null || !GetPen(GetRegister(1).x64, ref pen) || !GetRect(GetRegister(2).x64, ref rect)) { ret = false; break; }
                    using (Graphics g = Graphics.FromImage(GraphicSurface)) g.DrawEllipse(pen, rect);
                    break;

                case 8: // draw string ($1 brush) ($2 font) ($3 point) ($4 string) ($5 length)
                    if (GraphicSurface == null || !GetBrush(GetRegister(1).x64, ref brush) || !GetFont(GetRegister(2).x64, ref font)
                        || !GetPoint(GetRegister(3).x64, ref point) || !GetMem(GetRegister(5).x64, ref val) || !GetString(GetRegister(4).x64, val, ref str)) { ret = false; break; }
                    using (Graphics g = Graphics.FromImage(GraphicSurface)) g.DrawString(str, font, brush, point);
                    break;
                case 9: // draw string ($1 brush) ($2 font) ($3 rect) ($4 string) ($5 length)
                    if (GraphicSurface == null || !GetBrush(GetRegister(1).x64, ref brush) || !GetFont(GetRegister(2).x64, ref font)
                        || !GetRect(GetRegister(3).x64, ref rect) || !GetMem(GetRegister(5).x64, ref val) || !GetString(GetRegister(4).x64, val, ref str)) { ret = false; break; }
                    using (Graphics g = Graphics.FromImage(GraphicSurface)) g.DrawString(str, font, brush, rect);
                    break;

                // otherwise refer to parent
                default:
                    ret = base.Syscall();
                    break;
            }

            // dispose vars
            brush?.Dispose();
            pen?.Dispose();
            font?.Dispose();

            // return result
            return ret;
        }

        // (32:brush type, 32:color)
        private bool GetBrush(UInt64 code, ref Brush brush)
        {
            switch (code >> 32)
            {
                case 0: brush = new SolidBrush(Color.FromArgb((int)code)); return true;

                default: return false;
            }
        }
        // (32:pen type, 32:color)
        private bool GetPen(UInt64 code, ref Pen pen)
        {
            pen = new Pen(Color.FromArgb((int)code));

            switch (code >> 32)
            {
                case 0: return true;

                default: return false;
            }
        }
        // (16:font type, 16:font style, 32:size (integer 10th of point))
        private bool GetFont(UInt64 code, ref Font font)
        {
            FontFamily family;
            FontStyle style = FontStyle.Regular;

            // get font type
            switch (code >> 48)
            {
                case 0: family = FontFamily.GenericSansSerif; break;
                case 1: family = FontFamily.GenericSerif; break;
                case 2: family = FontFamily.GenericMonospace; break;

                default: return false;
            }

            // apply style (flags)
            if (((code >> 32) & 1) != 0) style |= FontStyle.Bold;
            if (((code >> 32) & 2) != 0) style |= FontStyle.Italic;
            if (((code >> 32) & 4) != 0) style |= FontStyle.Underline;
            if (((code >> 32) & 8) != 0) style |= FontStyle.Strikeout;

            // create the font
            font = new Font(family, (code & 0xffffffff) / 10f, style);
            return true;
        }

        // ptr struct {32:x, 32:y, 32:width, 32:height}
        private bool GetRect(UInt64 pos, ref Rectangle rect)
        {
            UInt64 val = 0;

            if (!GetMem(pos, 4, ref val)) return false;
            rect.X = (int)val;
            if (!GetMem(pos + 4, 4, ref val)) return false;
            rect.Y = (int)val;

            if (!GetMem(pos + 8, 4, ref val)) return false;
            rect.Width = (int)val;
            if (!GetMem(pos + 12, 4, ref val)) return false;
            rect.Height = (int)val;

            return true;
        }
        // ptr struct {32:x, 32:y}
        private bool GetPoint(UInt64 pos, ref Point point)
        {
            UInt64 val = 0;

            if (!GetMem(pos, 4, ref val)) return false;
            point.X = (int)val;
            if (!GetMem(pos + 4, 4, ref val)) return false;
            point.Y = (int)val;
            
            return true;
        }
        private bool GetString(UInt64 pos, UInt64 length, ref string str)
        {
            StringBuilder b = new StringBuilder();
            UInt64 val = 0;

            // read the string from memory (UTF-16)
            for (UInt64 i = 0; i < length; ++i)
            {
                if (!GetMem(pos + i * 2, 2, ref val)) return false;
                b.Append((char)val);
            }

            str = b.ToString();
            return true;
        }
    }
}
