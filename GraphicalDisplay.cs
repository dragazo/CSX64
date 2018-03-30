using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace csx64
{
    public partial class GraphicalDisplay : Form
    {
        /// <summary>
        /// The time (in ms) to delay between tick cycles
        /// </summary>
        private const int RenderDelay = 1;

        public GraphicalComputer C = new GraphicalComputer();
        public UInt64 Ticks = 0;

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            C.MousePos = e.Location;
        }
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            // don't call base. we want everything to be handled by client code

            C.MouseDelta += e.Delta;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            // don't call base. we want everything to be handled by client code

            C.MouseDown = e.Button;
        }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            // don't call base. we want everything to be handled by client code

            C.MouseDown = MouseButtons.None;
        }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            // don't call base. we want everything to be handled by client code

            C.KeyDown = e.Modifiers | e.KeyCode;
        }
        protected override void OnKeyUp(KeyEventArgs e)
        {
            // don't call base. we want everything to be handled by client code

            C.KeyDown = Keys.None;
        }

        // ---------------------------------- //

        /// <summary>
        /// Called after every tick cycle with the number of ticks that have elapsed
        /// </summary>
        public event Action<UInt64> OnTickCycle = null;

        public GraphicalDisplay()
        {
            InitializeComponent();

            // create the images
            C.RenderImage = new Bitmap(DisplayRectangle.Width, DisplayRectangle.Height);
            C.DisplayImage = new Bitmap(DisplayRectangle.Width, DisplayRectangle.Height);
            // and the graphics handle
            C.Graphics = Graphics.FromImage(C.RenderImage);

            // and the rendering tools
            C.Brush = new SolidBrush(Color.Black);
            C.Pen = new Pen(Color.Black);
            C.Font = new Font(FontFamily.GenericSansSerif, 16);
        }

        private bool disposed = false;
        protected override void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    // dispose the managed objects we allocated
                    C.RenderImage.Dispose();
                    C.DisplayImage.Dispose();
                    C.Graphics.Dispose();

                    C.Brush.Dispose();
                    C.Pen.Dispose();
                    C.Font.Dispose();
                }

                // ensure base dispose is called
                base.Dispose(disposing);
                disposed = true;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            // render the graphics object over the display surface
            e.Graphics.DrawImage(C.DisplayImage, Point.Empty);
        }

        /// <summary>
        /// Begins execution
        /// </summary>
        public async void Run()
        {
            Text = "Running";
            
            while (C.Running)
            {
                await Task.Delay(RenderDelay);

                // tick processor
                UInt32 i;
                for (i = 0; i < 10000 && C.Tick(); ++i) ;
                Ticks += i;

                // acount for re-rendering
                if (C.Invalidated)
                {
                    // swap render and display images
                    Utility.Swap(ref C.RenderImage, ref C.DisplayImage);

                    // if going to a different size
                    if (C.RenderImage.Size != ClientRectangle.Size)
                    {
                        // create the new image
                        C.RenderImage.Dispose();
                        C.RenderImage = new Bitmap(ClientRectangle.Width, ClientRectangle.Height);
                    }

                    // create the new graphics handle
                    C.Graphics.Dispose();
                    C.Graphics = Graphics.FromImage(C.RenderImage);

                    C.Invalidated = false; // mark invalidation as filled
                    Invalidate(); // redraw the form
                }

                // notify tick cycle event
                OnTickCycle?.Invoke(Ticks);
            }

            Text = $"Terminated - Error Code: {C.Error}";
        }
    }

    public class GraphicalComputer : CSX64
    {
        public enum GraphicalSyscallCodes
        {
            GetRenderSize = 64,

            GetMousePos, GetMouseDelta,
            GetMouseDown, GetKeyDown,

            SetBrush, SetPen, SetFont,

            Render,
            Clear,
            FillRect, DrawRect,
            FillEllipse, DrawEllipse,
            DrawString, DrawStringBounded
        }

        static GraphicalComputer()
        {
            // create definitions for all the syscall codes
            foreach (GraphicalSyscallCodes item in Enum.GetValues(typeof(GraphicalSyscallCodes)))
                DefineSymbol($"sys_{item.ToString().ToLower()}", (UInt64)item);
        }

        public Bitmap RenderImage, DisplayImage;
        public Graphics Graphics;

        public Brush Brush;
        public Pen Pen;
        public Font Font;

        // --------------- //

        public Point MousePos = Point.Empty;
        public int MouseDelta = 0;

        public MouseButtons MouseDown = MouseButtons.None;
        public Keys KeyDown = Keys.None;

        public bool Invalidated = false; // flag for if the processor has finished re-rendering

        protected override bool Syscall()
        {
            Rectangle rect;
            Point point;
            string str;
            
            bool ret = true; // return value (stored here because we need to dispose everything before returning)
            
            // register 0 contains a 64-bit syscall code
            switch (GetRegister(0).x64)
            {
                // -- data utilities -- //

                case (UInt64)GraphicalSyscallCodes.GetRenderSize:
                    GetRegister(1).x32 = (UInt64)RenderImage.Width;
                    GetRegister(2).x32 = (UInt64)RenderImage.Height;
                    break;

                case (UInt64)GraphicalSyscallCodes.GetMousePos:
                    GetRegister(1).x32 = (UInt64)MousePos.X;
                    GetRegister(2).x32 = (UInt64)MousePos.Y;
                    break;
                case (UInt64)GraphicalSyscallCodes.GetMouseDelta:
                    GetFlags().Z = (GetRegister(1).x64 = ((Int64)MouseDelta).MakeUnsigned()) == 0;
                    MouseDelta = 0;
                    break;

                case (UInt64)GraphicalSyscallCodes.GetMouseDown: GetFlags().Z = (GetRegister(1).x64 = (UInt64)MouseDown) == 0; break;
                case (UInt64)GraphicalSyscallCodes.GetKeyDown: GetFlags().Z = (GetRegister(1).x64 = (UInt64)KeyDown) == 0; break;

                // -- drawing settings -- //

                case (UInt64)GraphicalSyscallCodes.SetBrush: return GetBrush(GetRegister(1).x64);
                case (UInt64)GraphicalSyscallCodes.SetPen: return GetPen(GetRegister(1).x64);
                case (UInt64)GraphicalSyscallCodes.SetFont: return GetFont(GetRegister(1).x64);

                // -- drawing utilities -- //

                case (UInt64)GraphicalSyscallCodes.Render: Invalidated = true; break;

                case (UInt64)GraphicalSyscallCodes.Clear: // ($1 32:color)
                    Graphics.Clear(Color.FromArgb((int)GetRegister(1).x32));
                    break;

                case (UInt64)GraphicalSyscallCodes.FillRect: // ($1 rect)
                    if (!GetRect(GetRegister(1).x64, out rect)) { ret = false; break; }
                    Graphics.FillRectangle(Brush, rect);
                    break;
                case (UInt64)GraphicalSyscallCodes.DrawRect: // ($1 rect)
                    if (!GetRect(GetRegister(1).x64, out rect)) { ret = false; break; }
                    Graphics.DrawRectangle(Pen, rect);
                    break;

                case (UInt64)GraphicalSyscallCodes.FillEllipse: // ($1 rect)
                    if (!GetRect(GetRegister(1).x64, out rect)) { ret = false; break; }
                    Graphics.FillEllipse(Brush, rect);
                    break;
                case (UInt64)GraphicalSyscallCodes.DrawEllipse: // ($1 rect)
                    if (!GetRect(GetRegister(1).x64, out rect)) { ret = false; break; }
                    Graphics.DrawEllipse(Pen, rect);
                    break;

                case (UInt64)GraphicalSyscallCodes.DrawString: // ($1 point) ($2 string)
                    if (!GetPoint(GetRegister(1).x64, out point) || !GetString(GetRegister(2).x64, 2, out str)) { ret = false; break; }
                    Graphics.DrawString(str, Font, Brush, point);
                    break;
                case (UInt64)GraphicalSyscallCodes.DrawStringBounded: // ($1 rect) ($2 string)
                    if (!GetRect(GetRegister(1).x64, out rect) || !GetString(GetRegister(2).x64, 2, out str)) { ret = false; break; }
                    Graphics.DrawString(str, Font, Brush, rect);
                    break;
                    
                // otherwise defer to parent
                default: ret = base.Syscall(); break;
            }

            // return result
            return ret;
        }

        // ptr struct {8:type, 32:forecolor, 32:backcolor}
        private bool GetBrush(UInt64 pos)
        {
            if (!GetMem(pos, 1, out UInt64 _type) || !GetMem(pos + 1, 4, out UInt64 _fore) || !GetMem(pos + 5, 4, out UInt64 _back)) return false;

            Color forecolor = Color.FromArgb((int)_fore);
            Color backcolor = Color.FromArgb((int)_back);

            Brush.Dispose();

            // type: [1: category][7: settings]
            if ((_type & 0x80) == 0) Brush = new SolidBrush(forecolor);
            else Brush = new HatchBrush((HatchStyle)(_type & 0x7f), forecolor, backcolor);

            return true;
        }
        // ptr struct {32:pen type, 32:color}
        private bool GetPen(UInt64 pos)
        {
            if (!GetMem(pos, 4, out UInt64 _type) || !GetMem(pos + 4, 4, out UInt64 _color)) return false;

            Pen.Dispose();
            Pen = new Pen(Color.FromArgb((int)_color));

            return true;
        }
        // ptr struct {16:font type, 16:font style, 32f:size}
        private bool GetFont(UInt64 pos)
        {
            if (!GetMem(pos, 2, out UInt64 _type) || !GetMem(pos + 2, 2, out UInt64 _style) || !GetMem(pos + 4, 4, out UInt64 _size)) return false;

            // get font type
            FontFamily family;
            switch (_type)
            {
                case 0: family = FontFamily.GenericSansSerif; break;
                case 1: family = FontFamily.GenericSerif; break;
                case 2: family = FontFamily.GenericMonospace; break;

                default: return false;
            }

            // create the font
            Font.Dispose();
            Font = new Font(family, AsFloat(_size), (FontStyle)_style);

            return true;
        }

        // ptr struct {32:x, 32:y, 32:width, 32:height}
        private bool GetRect(UInt64 pos, out Rectangle rect)
        {
            UInt64 x, y, w, h;

            if (!GetMem(pos, 4, out x) || !GetMem(pos + 4, 4, out y) || !GetMem(pos + 8, 4, out w) || !GetMem(pos + 12, 4, out h))
            { rect = Rectangle.Empty; return false; }

            rect = new Rectangle((int)x, (int)y, (int)w, (int)h);

            return true;
        }
        // ptr struct {32:x, 32:y}
        private bool GetPoint(UInt64 pos, out Point point)
        {
            UInt64 x, y;

            if (!GetMem(pos, 4, out x) || !GetMem(pos + 4, 4, out y))
            { point = Point.Empty; return false; }

            point = new Point((int)x, (int)y);
            
            return true;
        }
    }
}
