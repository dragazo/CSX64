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
    [Obsolete]
    public partial class ConsoleClient : Form
    {
        /// <summary>
        /// Delay between tick cycles in ms
        /// </summary>
        private const int RenderDelay = 1;
        /// <summary>
        /// The number of ticks per render cycle
        /// </summary>
        private const UInt64 TicksPerCycle = 10000;

        /// <summary>
        /// The number of ticks from <see cref="DateTime.UtcNow"/> that represents a complete cursor blink cycle
        /// </summary>
        private const long TimeTicksPerCursorBlinkCycle = 10000000;

        // ------------------------------------
        
        /// <summary>
        /// The processor used for simulation
        /// </summary>
        public Computer C;
        /// <summary>
        /// The number of ticks that have elapsed
        /// </summary>
        private UInt64 Ticks;

        /// <summary>
        /// The standard streams used by this application
        /// </summary>
        private Stream stdin, stdout, stderr;
        /// <summary>
        /// The position of stderr since the last pull operation
        /// </summary>
        private long last_stderr_len;
        /// <summary>
        /// Indicates that stdin accepts keyboard input upon reaching eof
        /// </summary>
        private bool stdin_interactive;
        /// <summary>
        /// Indicates that the standard streams have been initialized and we're ready to begin execution
        /// </summary>
        private bool stdio_ready = false;

        /// <summary>
        /// The buffer used as temporary storage during a pull operation
        /// </summary>
        private byte[] Buffer = new byte[1024]; // size must be even (must store complete unicode characters)

        /// <summary>
        /// The lines that are displayed on the console window
        /// </summary>
        private OverflowQueue<string> Lines = new OverflowQueue<string>(400);

        /// <summary>
        /// The brush used for rendering text
        /// </summary>
        private SolidBrush TextBrush = new SolidBrush(Color.Black);
        /// <summary>
        /// The font used for rendering text
        /// </summary>
        private Font TextFont = new Font(FontFamily.GenericMonospace, 16f);

        /// <summary>
        /// Gets or sets the line of text that is at the top of the console window
        /// </summary>
        private int DispLine
        {
            get => MainScroll.Value;
            set => MainScroll.Value = value < MainScroll.Minimum ? MainScroll.Minimum : value > MainScroll.Maximum ? MainScroll.Maximum : value;
        }
        /// <summary>
        /// Returns the height of a line of text
        /// </summary>
        private float LineHeight => 1.45f * TextFont.Size;
        /// <summary>
        /// Returns the number of complete lines that can fit in the console window
        /// </summary>
        private int LinesPerPage => (int)(DisplayRectangle.Height / LineHeight);

        /// <summary>
        /// Holds the raw keyboard data used during interactive input for stdin
        /// </summary>
        private StringBuilder InputLine = new StringBuilder();
        /// <summary>
        /// The cursor position in <see cref="InputLine"/>
        /// </summary>
        private int CursorPosition = 0;

        /// <summary>
        /// the base time for the cursor blink cycle
        /// </summary>
        private long CursorBlinkCycleBase;
        /// <summary>
        /// returns if the cursor blink cycle is currently in the "on" position
        /// </summary>
        private bool CursorBlinkCycle => (DateTime.UtcNow.Ticks - CursorBlinkCycleBase) % TimeTicksPerCursorBlinkCycle < TimeTicksPerCursorBlinkCycle / 2;

        /// <summary>
        /// Gets or sets the color of the text in the console
        /// </summary>
        public Color TextColor
        {
            get => TextBrush.Color;
            set => TextBrush.Color = value;
        }

        /// <summary>
        /// Fired after each tick cycle with the current number of ticks that have elapsed
        /// </summary>
        public event Action<UInt64> OnTickCycle = null;

        /// <summary>
        /// Creates a new graphical client with the specified computer
        /// </summary>
        /// <param name="computer">the computer to use for execution</param>
        public ConsoleClient(Computer computer)
        {
            InitializeComponent();

            // assign the computer object
            C = computer;
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

                    TextBrush.Dispose();
                    TextFont.Dispose();

                    C.Dispose();
                }

                // ensure base dispose is called
                base.Dispose(disposing);
                disposed = true;
            }
        }

        /// <summary>
        /// Updates the display with data from stderr
        /// </summary>
        private void Pull()
        {
            // if stderr is ahead of us
            if (stderr.CanSeek && stderr.CanRead && stderr.Length > last_stderr_len)
            {
                // store current position and seek to old length
                long pos = stderr.Position;
                stderr.Seek(last_stderr_len, SeekOrigin.Begin);

                int count; // number of bytes read from stderr

                // read all the stuff to display
                do
                {
                    count = stderr.Read(Buffer, 0, Buffer.Length);
                    Put(Buffer.ConvertToString(0, count));
                }
                while (count == Buffer.Length);

                // go back to the old position
                stderr.Seek(pos, SeekOrigin.Begin);
                last_stderr_len = stderr.Length;

                // redraw form
                Invalidate();
            }
            // otherwise if we're awaiting data
            else if (C.SuspendedRead)
            {
                // redraw (for cursor blink)
                Invalidate();
            }
        }

        /// <summary>
        /// Appends a string to the display
        /// </summary>
        private void Put(string str)
        {
            int start, stop;

            // get all the lines separated
            for (start = 0; start < str.Length; start = stop + 1)
            {
                // wind up to the next new line
                for (stop = start; stop < str.Length && str[stop] != '\n'; ++stop) ;

                // append to last line
                Lines[Lines.Count - 1] += str.Substring(start, stop - start);
                // if we ended on a new line char, tack on a new line
                if (stop < str.Length) Lines.Enqueue(string.Empty);
            }
        }
        /// <summary>
        /// Appends a character to the display
        /// </summary>
        /// <param name="ch"></param>
        private void Put(char ch)
        {
            // non-new line appends to last line
            if (ch != '\n') Lines[Lines.Count - 1] += ch;
            // otherwise, start a fresh line
            else Lines.Enqueue(string.Empty);
        }

        /// <summary>
        /// Appends a string to stdin and the display
        /// </summary>
        private void stdin_append(string str)
        {
            // store previous pos and seek to end of stream
            long pos = stdin.Position;
            stdin.Seek(0, SeekOrigin.End);

            // write the data
            byte[] data = str.ConvertToBytes();
            stdin.Write(data, 0, data.Length);

            // go back to where we were
            stdin.Seek(pos, SeekOrigin.Begin);

            // also append to display
            Put(str);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics; // alias graphics handle for convenience

            float y = 0; // y position of line
            SizeF disp_size = new SizeF(ClientRectangle.Width - MainScroll.Width, ClientRectangle.Height); // the size of the "real" drawing surface

            // for each line except the last
            for (int i = DispLine; i < Lines.Count - 1; ++i)
            {
                // render the line
                g.DrawString(Lines[i], TextFont, TextBrush, new RectangleF(0, y, disp_size.Width, 0));

                // go to next line
                y += g.MeasureString(Lines[i], TextFont, disp_size).Height;
                if (y >= disp_size.Height) break;
            }

            // do the last line if we're still on the screen
            if (y < disp_size.Height)
            {
                // get the last line
                string last = Lines[Lines.Count - 1];
                // if we're awaiting data, also print the line we've got so far
                if (C.SuspendedRead) last += InputLine.ToString();

                // render the last line
                g.DrawString(last, TextFont, TextBrush, new RectangleF(0, y, disp_size.Width, 0));

                // if we should indicate the cursor pos
                if (C.SuspendedRead && CursorBlinkCycle)
                {
                    // get real position of cursor pos in complete line
                    int real_pos = Lines[Lines.Count - 1].Length + CursorPosition;
                    // add an extra space to the end of the line (this ensures it will not be empty and that 1 past end is legal)
                    last += " ";

                    // get the character ranges array (only maps cursor character or last character if beyond end of string
                    CharacterRange[] ch_ranges = new CharacterRange[] { new CharacterRange(real_pos, 1) };

                    // create the format object for measurement
                    using (StringFormat format = new StringFormat())
                    {
                        format.FormatFlags = StringFormatFlags.MeasureTrailingSpaces; // ensure space characters are properly measured
                        format.SetMeasurableCharacterRanges(ch_ranges); // add the desired range (real cursor position) to the measurement queue

                        // measure the string and get the region objects
                        Region[] regions = g.MeasureCharacterRanges(last, TextFont, new RectangleF(0, y, disp_size.Width, disp_size.Height), format);
                        // get the bounding box for the real cursor position
                        RectangleF bounds = regions[0].GetBounds(g);

                        // if this goes outside the display surface
                        // there are clipping issues that seem particularly annoying to solve for overhanging consecutive white space
                        // in this case we can either not draw the cursor, or not attempt to fix it. the latter looks better

                        // if not in insert mode, make it a bar cursor
                        if (!IsKeyLocked(Keys.Insert)) bounds.Width = 1;

                        // draw the cursor
                        g.FillRectangle(TextBrush, bounds);
                    }
                }
            }

            // update the scroll bar
            MainScroll.Maximum = Lines.Count - 1;
            MainScroll.LargeChange = LinesPerPage;
        }

        private async void Run()
        {
            Text = "Running";

            // make sure stdio is set up
            if (!stdio_ready) throw new NullReferenceException("Must call ConsoleDisplay.SetupStdio before beginning execution");

            // link stdio streams to the processor
            C.GetFD(0).Open(stdin, false, stdin_interactive);
            C.GetFD(1).Open(stdout, false, false);
            C.GetFD(2).Open(stderr, false, false);

            while (C.Running)
            {
                await Task.Delay(RenderDelay);

                // tick processor
                UInt64 i;
                for (i = 0; i < TicksPerCycle && !C.SuspendedRead && C.Tick(); ++i) ;
                Ticks += i;

                Pull(); // pull data

                // notify tick cycle event
                OnTickCycle?.Invoke(Ticks);
            }

            // unlink the streams (don't close them)
            stdin = stdout = stderr = null;
            stdio_ready = false;

            Text = $"Terminated - Error Code: {C.Error}";
        }

        /// <summary>
        /// Sets the standard streams used by this object. Must call this method before beginning execution.
        /// These streams are not closed after execution ends
        /// </summary>
        /// <param name="_stdin_interactive">marks that stdin should take input from the keyboard</param>
        public void SetupStdio(Stream _stdin, Stream _stdout, Stream _stderr, bool _stdin_interactive)
        {
            stdin = _stdin;
            stdout = _stdout;
            stderr = _stderr;
            stdin_interactive = _stdin_interactive;

            stdio_ready = true;
        }
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            // lines container should always have something to append to
            Lines.Clear();
            Lines.Enqueue(string.Empty);
            DispLine = 0;

            last_stderr_len = 0;

            InputLine.Clear();
            CursorPosition = 0;

            Ticks = 0;
            Run();
        }
        
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);

            // update the display line
            DispLine = e.Delta > 0 ? MainScroll.Value - MainScroll.SmallChange : MainScroll.Value + MainScroll.SmallChange;
        }
        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            // don't call base, we want to handle everything ourselves

            // if we're awaiting data, pressing keys should append to stdin
            if (C.SuspendedRead)
            {
                switch (e.KeyChar)
                {
                    // if we backspaced we need to remove a char from input line
                    case '\b':
                        // if we're not at the front of the line, remove a character
                        if (CursorPosition > 0)
                        {
                            // remove the character to the left
                            InputLine.Remove(CursorPosition - 1, 1);
                            --CursorPosition;
                        }
                        break;
                    // if we pressed delete, remove a char from input line
                    case '⌂':
                        // if we're not at the end of the line, remove a character
                        if (CursorPosition < InputLine.Length)
                        {
                            // remove the character to the left
                            InputLine.Remove(CursorPosition, 1);
                        }
                        break;

                    // carriage returns should append a new line and flag as having data
                    case '\r':
                        // input the line of text we generated
                        stdin_append(InputLine.ToString() + Environment.NewLine); // use environment new line (e.g. might be "\r\n" on windows)
                        // clear input line for reuse
                        InputLine.Clear();
                        CursorPosition = 0;
                        // resume execution
                        C.ResumeSuspendedRead();
                        break;

                    default:
                        // otherwise, if it's not a control char, print it
                        if (!char.IsControl(e.KeyChar) || true)
                        {
                            // if in insert mode and we're on a character, replace it
                            if (IsKeyLocked(Keys.Insert) && CursorPosition < InputLine.Length) InputLine[CursorPosition] = e.KeyChar;
                            // otherwise insert at cursor pos
                            else InputLine.Insert(CursorPosition, e.KeyChar);

                            // in either case, advance cursor position
                            ++CursorPosition;
                        }
                        break;
                }

                // now that we've pressed a key, mark that the cursor should now be in the on position
                CursorBlinkCycleBase = DateTime.UtcNow.Ticks;
                Invalidate();
            }
        }
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // don't call base, we want to handle everything ourselves

            // perform special control commands
            switch (keyData)
            {
                // rebind ctrl+C to abort
                case Keys.Control | Keys.C: C.Terminate(ErrorCode.Abort); return true;
            }

            // perform special interactive edit commands
            if (C.SuspendedRead)
            {
                switch (keyData)
                {
                    // bind home and end to move to beginning or end of input string
                    case Keys.Home: CursorPosition = 0; break;
                    case Keys.End: CursorPosition = InputLine.Length; break;

                    // bind ctrl+v to paste clipboard in current position
                    case Keys.Control | Keys.V:
                        // get the stuff to paste in
                        string pasta = Clipboard.GetText();
                        InputLine.Insert(CursorPosition, pasta);
                        CursorPosition += pasta.Length;
                        break;

                    // bind left and right to control cursor position during suspended read mode
                    case Keys.Left: if (CursorPosition > 0) --CursorPosition; break;
                    case Keys.Right: if (CursorPosition < InputLine.Length) ++CursorPosition; break;

                    // --------------------------------------------------------------------------------------------

                    // reroute delete to key press event
                    case Keys.Delete: OnKeyPress(new KeyPressEventArgs('⌂')); return true; // should return, not break (key press will invalidate form anyway)

                    // return false here will forward anything else to key press event
                    default: return false;
                }
                
                // now that we've pressed a key, mark that the cursor should now be in the on position
                CursorBlinkCycleBase = DateTime.UtcNow.Ticks;
                Invalidate();
            }

            // if we didn't handle it, we'll ignore it
            return true;
        }

        private void MainScroll_ValueChanged(object sender, EventArgs e)
        {
            // when scroll level changes, redraw text
            Invalidate();
        }
    }
}
