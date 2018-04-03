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
    public partial class ConsoleDisplay : Form
    {
        /// <summary>
        /// Delay between tick cycles in ms
        /// </summary>
        private const int RenderDelay = 1;
        /// <summary>
        /// The number of ticks per render cycle
        /// </summary>
        private const UInt64 TicksPerCycle = 10000;

        // ------------------------------------

        public CSX64 C = new CSX64();
        private UInt64 Ticks;

        Stream stdin, stdout, stderr;
        long last_stderr_pos;
        bool stdin_interactive;
        bool stdio_ready = false;

        private OverflowQueue<string> Lines = new OverflowQueue<string>(400);

        private SolidBrush TextBrush = new SolidBrush(Color.Black);
        private Font TextFont = new Font(FontFamily.GenericMonospace, 16f);

        private int DispLine
        {
            get => MainScroll.Value;
            set => MainScroll.Value = value < MainScroll.Minimum ? MainScroll.Minimum : value > MainScroll.Maximum ? MainScroll.Maximum : value;
        }
        private float LineHeight => 1.4f * TextFont.Size;
        private int LinesPerPage => (int)(DisplayRectangle.Height / LineHeight);

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

        public ConsoleDisplay()
        {
            InitializeComponent();
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
        /// Updates the display with data from stderr. Returns true if there were differences
        /// </summary>
        private void Pull()
        {
            // if stderr has advanced (checking for CanRead ensured stream is still valid)
            if (stderr.CanRead && stderr.Position > last_stderr_pos)
            {
                // store new position
                long new_pos = stderr.Position;

                // read all the new stuff (16-bit words)
                stderr.Seek(last_stderr_pos, SeekOrigin.Begin);
                StringBuilder b = new StringBuilder();
                while (stderr.Position + 1 < stderr.Length)
                {
                    char ch = (char)(stderr.ReadByte() | (stderr.ReadByte() >> 8));
                    b.Append(ch);
                }
                Puts(b.ToString());

                // go back to the old position
                stderr.Seek(new_pos, SeekOrigin.Begin);
                last_stderr_pos = new_pos;

                // redraw form
                Invalidate();
            }
        }

        private void Puts(string str)
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
        private void Putc(char ch)
        {
            // non-new line appends to last line
            if (ch != '\n') Lines[Lines.Count - 1] += ch;
            // otherwise, start a fresh line
            else Lines.Enqueue(string.Empty);
        }

        private void stdin_append(char ch)
        {
            // store previous pos
            long pos = stdin.Position;

            // append the character
            stdin.Seek(0, SeekOrigin.End);
            stdin.Write(new byte[] { (byte)ch, (byte)(ch >> 8) }, 0, 2);

            // go back to where we were
            stdin.Seek(pos, SeekOrigin.Begin);
            
            // also append this char to the output window
            Putc(ch);
        }
        private void stdin_append(string str)
        {
            // append each character
            for (int i = 0; i < str.Length; ++i) stdin_append(str[i]);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics; // alias graphics handle for convenience

            float y = 0; // y position of line

            // for each line
            for (int i = DispLine; i < Lines.Count; ++i)
            {
                // render the line
                g.DrawString(Lines[i], TextFont, TextBrush, 0, y);

                // go to next line
                y += LineHeight;
                if (y >= DisplayRectangle.Height) break;
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
            C.SetUnmanagedStream(0, stdin, stdin_interactive);
            C.SetUnmanagedStream(1, stdout, false);
            C.SetUnmanagedStream(2, stderr, false);

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

            // unlink the streams
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

            last_stderr_pos = 0;

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

            // if we're in interactive mode and awaiting data, pressing keys should append to stdin
            if (stdin_interactive && C.SuspendedRead && stdin != null && stdin.CanWrite)
            {
                switch (e.KeyChar)
                {
                    // if we backspaced we need to do some special stuff
                    case '\b':
                        // if we can remove a character before the processor has gotten to it
                        if (stdin.Position <= stdin.Length - 2)
                        {
                            // remove that character from the stream
                            stdin.SetLength(stdin.Length - 2);
                            // remove that character from the display
                            Lines[Lines.Count - 1] = Lines[Lines.Count - 1].Substring(0, Lines[Lines.Count - 1].Length - 1);
                        }
                        // otherwise we've backed all the way to the beginning of the line
                        else System.Media.SystemSounds.Beep.Play();
                        break;

                    // carriage returns should append a new line and flag as having data
                    case '\r':
                        stdin_append(Environment.NewLine); // use environment new line (e.g. might be "\r\n" on windows)
                        C.SuspendedRead = false;
                        break;

                    // otherwise it's a normal char to print
                    default: stdin_append(e.KeyChar); break;
                }

                Invalidate();
            }
        }
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // don't call base, we want to handle everything ourselves

            // perform special actions for command chars
            switch (keyData)
            {

                default: return false;
            }
        }

        private void MainScroll_ValueChanged(object sender, EventArgs e)
        {
            // when scroll level changes, redraw text
            Invalidate();
        }
    }

    /// <summary>
    /// Represents a queue of fixed size where adding new items beyond the size of the queue removes the oldest items
    /// </summary>
    public sealed class OverflowQueue<T>
    {
        /// <summary>
        /// The raw container for the queue
        /// </summary>
        private T[] Data;
        /// <summary>
        /// The index of the first item in the queue
        /// </summary>
        private int Pos;
        /// <summary>
        /// The number of items in the queue
        /// </summary>
        public int Count { get; private set; }
        /// <summary>
        /// Gets the maximum capacity of the queue before adding subsequent items removes older items
        /// </summary>
        public int Capacity => Data.Length;

        /// <summary>
        /// Creates an overflow queue with the specified capacity
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public OverflowQueue(int cap)
        {
            // make sure capacity is valid
            if (cap <= 0) throw new ArgumentOutOfRangeException("Size of list must be greater than zero");

            Data = new T[cap];
            Pos = 0;
            Count = 0;
        }

        /// <summary>
        /// Gets the item at the specified index
        /// </summary>
        public T this[int index]
        {
            get => Data[(Pos + index) % Capacity];
            set => Data[(Pos + index) % Capacity] = value;
        }

        /// <summary>
        /// Adds an item to the queue
        /// </summary>
        public void Enqueue(T item)
        {
            // if we have enough room, just add the item
            if (Count < Capacity) Data[Count++] = item;
            // otherwise we need to replace the oldest item
            else
            {
                Data[Pos++] = item;
                if (Pos == Capacity) Pos = 0;
            }
        }
        /// <summary>
        /// Removes the oldest item from the list
        /// </summary>
        public T Dequeue()
        {
            --Count;
            T ret = Data[Pos++];
            if (Pos == Capacity) Pos = 0;
            return ret;
        }

        /// <summary>
        /// Clears the contents
        /// </summary>
        public void Clear()
        {
            Pos = Count = 0;
        }
    }
}
