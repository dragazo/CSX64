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

            // create the images
            C.RenderImage = new Bitmap(DisplayRectangle.Width, DisplayRectangle.Height);
            C.DisplayImage = new Bitmap(DisplayRectangle.Width, DisplayRectangle.Height);
            // store size settings
            C.ImageSize = DisplayRectangle.Size;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            // render the graphics object over the display surface
            e.Graphics.DrawImage(C.DisplayImage, Point.Empty);
        }
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);

            C.ImageSize = DisplayRectangle.Size; // update size settings
            C.NeedsRender = true; // mark that we need a render

            Invalidate();
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            // update computer mouse pos
            C.MousePos = e.Location;
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
                UInt64 i;
                for (i = 0; i < 10000 && C.Tick(); ++i) ;
                Ticks += i;

                // acount for re-rendering
                if (C.Invalidated)
                {
                    C.Invalidated = false; // mark invalidation as filled
                    Invalidate(); // redraw the form
                }

                OnTickCycle?.Invoke(Ticks);
            }

            Text = $"Terminated - Error Code: {C.Error}";
        }
    }

    public class GraphicalComputer : CSX64
    {
        public Bitmap RenderImage = null, DisplayImage = null;

        public Size ImageSize = System.Drawing.Size.Empty; //new Size(100, 100); // size to use for making new images
        public Point MousePos = Point.Empty; // mouse position

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
                    GetRegister(1).x64 = (UInt64)RenderImage.Width;
                    GetRegister(2).x64 = (UInt64)RenderImage.Height;
                    break;
                case 1: // ask if render needed (e.g. window resize)
                    GetRegister(1).x8 = NeedsRender ? 1 : 0ul;
                    break;
                case 2: // render
                    {
                        // display the new render
                        DisplayImage?.Dispose();
                        DisplayImage = RenderImage;
                        // make a new image to render
                        RenderImage = new Bitmap(ImageSize.Width, ImageSize.Height);
                    }
                    Invalidated = true;  // flag as invalidated
                    NeedsRender = false; // mark that we rendered
                    break;
                case 3: // mouse pos
                    GetRegister(1).x64 = (UInt64)MousePos.X;
                    GetRegister(2).x64 = (UInt64)MousePos.Y;
                    break;

                // -- drawing utilities --

                case 4: // clear ($1 32:color)
                    if (RenderImage == null || !GetMem(GetRegister(1).x64, 4, ref val)) { ret = false; break; }
                    using (Graphics g = Graphics.FromImage(RenderImage)) g.Clear(Color.FromArgb((int)val));
                    break;

                case 5: // fill rect ($1 brush) ($2 rect)
                    if (RenderImage == null || !GetBrush(GetRegister(1).x64, ref brush) || !GetRect(GetRegister(2).x64, ref rect)) { ret = false; break; }
                    using (Graphics g = Graphics.FromImage(RenderImage)) g.FillRectangle(brush, rect);
                    break;
                case 6: // draw rect ($1 pen) ($2 rect)
                    if (RenderImage == null || !GetPen(GetRegister(1).x64, ref pen) || !GetRect(GetRegister(2).x64, ref rect)) { ret = false; break; }
                    using (Graphics g = Graphics.FromImage(RenderImage)) g.DrawRectangle(pen, rect);
                    break;

                case 7: // fill ellipse ($1 brush) ($2 rect)
                    if (RenderImage == null || !GetBrush(GetRegister(1).x64, ref brush) || !GetRect(GetRegister(2).x64, ref rect)) { ret = false; break; }
                    using (Graphics g = Graphics.FromImage(RenderImage)) g.FillEllipse(brush, rect);
                    break;
                case 8: // draw ellipse ($1 pen) ($2 rect)
                    if (RenderImage == null || !GetPen(GetRegister(1).x64, ref pen) || !GetRect(GetRegister(2).x64, ref rect)) { ret = false; break; }
                    using (Graphics g = Graphics.FromImage(RenderImage)) g.DrawEllipse(pen, rect);
                    break;

                case 9: // draw string ($1 brush) ($2 font) ($3 point) ($4 string) ($5 length)
                    if (RenderImage == null || !GetBrush(GetRegister(1).x64, ref brush) || !GetFont(GetRegister(2).x64, ref font)
                        || !GetPoint(GetRegister(3).x64, ref point) || !GetString(GetRegister(4).x64, GetRegister(5).x64, ref str)) { ret = false; break; }
                    using (Graphics g = Graphics.FromImage(RenderImage)) g.DrawString(str, font, brush, point);
                    MessageBox.Show(str);
                    break;
                case 10: // draw string ($1 brush) ($2 font) ($3 rect) ($4 string) ($5 length)
                    if (RenderImage == null || !GetBrush(GetRegister(1).x64, ref brush) || !GetFont(GetRegister(2).x64, ref font)
                        || !GetRect(GetRegister(3).x64, ref rect) || !GetString(GetRegister(4).x64, GetRegister(5).x64, ref str)) { ret = false; break; }
                    using (Graphics g = Graphics.FromImage(RenderImage)) g.DrawString(str, font, brush, rect);
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
