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
using static CSX64.Utility;

namespace CSX64
{
    /// <summary>
    /// Represents a CSX64 derivation that offers graphical controls via windows forms
    /// </summary>
    public partial class GraphicalClient : Form
    {
        /// <summary>
        /// The time (in ms) to delay between tick cycles
        /// </summary>
        private const int RenderDelay = 1;

        // ----------------------------------

        private GraphicalComputer Computer;
        private UInt64 Ticks;

        // ----------------------------------

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            Computer.MousePos = e.Location;
        }
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            // don't call base. we want everything to be handled by client code

            Computer.MouseDelta += e.Delta;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            // don't call base. we want everything to be handled by client code

            Computer.MouseDown = e.Button;
        }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            // don't call base. we want everything to be handled by client code

            Computer.MouseDown = MouseButtons.None;
        }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            // don't call base. we want everything to be handled by client code

            Computer.KeyDown = e.Modifiers | e.KeyCode;
        }
        protected override void OnKeyUp(KeyEventArgs e)
        {
            // don't call base. we want everything to be handled by client code

            Computer.KeyDown = Keys.None;
        }

        // ----------------------------------

        /// <summary>
        /// Called after every tick cycle with the number of ticks that have elapsed
        /// </summary>
        public event Action<UInt64> OnTickCycle = null;

        public GraphicalClient(GraphicalComputer computer)
        {
            InitializeComponent();

            Computer = computer;

            // create the images
            computer.RenderImage = new Bitmap(DisplayRectangle.Width, DisplayRectangle.Height);
            computer.DisplayImage = new Bitmap(DisplayRectangle.Width, DisplayRectangle.Height);
            // and the graphics handle
            computer.Graphics = Graphics.FromImage(computer.RenderImage);

            // and the rendering tools
            computer.Brush = new SolidBrush(Color.Black);
            computer.Pen = new Pen(Color.Black);
            computer.Font = new Font(FontFamily.GenericSansSerif, 16);
        }

        private bool __Disposed = false;
        protected override void Dispose(bool disposing)
        {
            if (!__Disposed)
            {
                if (disposing)
                {
                    components?.Dispose(); // from auto-generated code (they hinted it might be null)

                    // -- dispose the managed objects we allocated -- //

                    Computer.RenderImage.Dispose();
                    Computer.DisplayImage.Dispose();
                    Computer.Graphics.Dispose();

                    Computer.Brush.Dispose();
                    Computer.Pen.Dispose();
                    Computer.Font.Dispose();
                }

                // ensure base dispose is called
                base.Dispose(disposing);
                __Disposed = true;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            // render the graphics object over the display surface
            e.Graphics.DrawImage(Computer.DisplayImage, Point.Empty);
        }
        
        /// <summary>
        /// Begins execution
        /// </summary>
        private async void Run()
        {
            Text = "Running";
            
            while (Computer.Running)
            {
                await Task.Delay(RenderDelay);

                // tick processor
                UInt32 i;
                for (i = 0; i < 10000 && Computer.Tick(); ++i) ;
                Ticks += i;

                // acount for re-rendering
                if (Computer.Invalidated)
                {
                    // swap render and display images
                    Utility.Swap(ref Computer.RenderImage, ref Computer.DisplayImage);

                    // if going to a different size
                    if (Computer.RenderImage.Size != ClientRectangle.Size)
                    {
                        // create the new image
                        Computer.RenderImage.Dispose();
                        Computer.RenderImage = new Bitmap(ClientRectangle.Width, ClientRectangle.Height);
                    }

                    // create the new graphics handle
                    Computer.Graphics.Dispose();
                    Computer.Graphics = Graphics.FromImage(Computer.RenderImage);

                    Computer.Invalidated = false; // mark invalidation as filled
                    Invalidate(); // redraw the form
                }

                // notify tick cycle event
                OnTickCycle?.Invoke(Ticks);
            }

            Text = $"Terminated - Error Code: {Computer.Error}";
        }

        /// <summary>
        /// Shows the form and begins execution of the program
        /// </summary>
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            Computer.MousePos = Point.Empty;
            Computer.MouseDelta = 0;
            Computer.MouseDown = MouseButtons.None;
            Computer.KeyDown = Keys.None;

            Computer.Invalidated = false;

            Ticks = 0;
            Run();
        }
    }

    public class GraphicalComputer : Computer
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

        internal static void InitStatics() { }
        static GraphicalComputer()
        {
            // create definitions for all the syscall codes
            foreach (GraphicalSyscallCodes item in Enum.GetValues(typeof(GraphicalSyscallCodes)))
                Assembly.DefineSymbol($"sys_{item.ToString().ToLower()}", (UInt64)item);
        }

        public Bitmap RenderImage, DisplayImage;
        public Graphics Graphics;

        public Brush Brush;
        public Pen Pen;
        public Font Font;

        // --------------- //

        public Point MousePos;
        public int MouseDelta;
        public MouseButtons MouseDown;
        public Keys KeyDown;

        public bool Invalidated; // flag for if the processor has finished re-rendering

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
                    if (!GetPoint(GetRegister(1).x64, out point) || !GetCString(GetRegister(2).x64, out str)) { ret = false; break; }
                    Graphics.DrawString(str, Font, Brush, point);
                    break;
                case (UInt64)GraphicalSyscallCodes.DrawStringBounded: // ($1 rect) ($2 string)
                    if (!GetRect(GetRegister(1).x64, out rect) || !GetCString(GetRegister(2).x64, out str)) { ret = false; break; }
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
