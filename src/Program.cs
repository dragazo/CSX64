using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security;

namespace csx64
{
    static class Program
    {
        /// <summary>
        /// Represents a desired action
        /// </summary>
        private enum ProgramAction
        {
            ExecuteConsole, ExecuteGraphical,
            Assemble, Link
        }

        // ---------------------------------

        /// <summary>
        /// Gets the user's background color
        /// </summary>
        public static Color BackColor => Properties.Settings.Default.ProcessorBackColor;
        /// <summary>
        /// Gets the user's foreground color
        /// </summary>
        public static Color ForeColor => Properties.Settings.Default.ProcessorForeColor;

        // ---------------------------------

        private const string HelpMessage =
            "\n" +
            "usage: csx64 [<options>] [--] <pathspec>...\n" +
            "\n" +
            "    -h, --help             shows this help mesage\n" +
            "    -g, --graphical        executes a program via the graphical client\n" +
            "    -a, --assemble         assembly files should be assembled into object files\n" +
            "    -l, --link             object files should be linked into an executable\n" +
            "    -o, --out <pathspec>   specifies the output path. if not provided, will usually take on a default value\n" +
            "\n" +
            "if no options are provided, will execute a program via the console client\n" +
            "";

        /// <summary>
        /// Prints a message to the user (console or message box)
        /// </summary>
        /// <param name="msg">message to print</param>
        private static void Print(string msg)
        {
            Console.WriteLine(msg);
        }

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        private static int Main(string[] args)
        {
            // set up initilization thingys
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // if there were no command args, launch the GUI version
            if (args.Length == 0)
            {
                Application.Run(new CodeEditor());
            }
            // otherwise we're doing something special
            else
            {
                List<string> paths = new List<string>();             // input paths
                string output = null;                                // output path
                ProgramAction action = ProgramAction.ExecuteConsole; // requested action

                // process the terminal args
                for (int i = 0; i < args.Length; ++i)
                {
                    switch (args[i])
                    {
                        // do the long names
                        case "--help": Print(HelpMessage); return 0;
                        case "--graphical": if (action != ProgramAction.ExecuteConsole) { Print("usage error - see -h for help"); return 0; } action = ProgramAction.ExecuteGraphical; break;
                        case "--assemble": if (action != ProgramAction.ExecuteConsole) { Print("usage error - see -h for help"); return 0; } action = ProgramAction.Assemble; break;
                        case "--link": if (action != ProgramAction.ExecuteConsole) { Print("usage error - see -h for help"); return 0; } action = ProgramAction.Link; break;
                        case "--output": if (output != null || i + 1 >= args.Length) { Print("usage error - see -h for help"); return 0; } output = args[++i]; break;

                        default:
                            // do the short names
                            if (0 < args[i].Length && args[i][0] == '-')
                            {
                                string arg = args[i]; // record parsing arg (i may change upon -o option)
                                for (int j = 1; j < arg.Length; ++j)
                                {
                                    switch (arg[j])
                                    {
                                        case 'h': Print(HelpMessage); return 0;
                                        case 'g': if (action != ProgramAction.ExecuteConsole) { Print("usage error - see -h for help"); return 0; } action = ProgramAction.ExecuteGraphical; break;
                                        case 'a': if (action != ProgramAction.ExecuteConsole) { Print("usage error - see -h for help"); return 0; } action = ProgramAction.Assemble; break;
                                        case 'l': if (action != ProgramAction.ExecuteConsole) { Print("usage error - see -h for help"); return 0; } action = ProgramAction.Link; break;
                                        case 'o': if (output != null || i + 1 >= args.Length) { Print("usage error - see -h for help"); return 0; } output = args[++i]; break;

                                        default: Print($"unknown option '{arg[j]}' see -h for help"); return 0;
                                    }
                                }
                            }
                            // otherwise it's a file path
                            else paths.Add(args[i]);

                            break;
                    }
                }

                // perform the action
                switch (action)
                {
                    case ProgramAction.ExecuteConsole:
                        if (paths.Count != 1) { Print("Execution mode expected one argument\n"); return 0; }

                        RunRawConsole(paths[0]);
                        break;
                    case ProgramAction.ExecuteGraphical:
                        if (paths.Count != 1) { Print("Execution mode expected one argument\n"); return 0; }

                        RunGraphicalClient(paths[0]);
                        break;

                    case ProgramAction.Assemble:
                        // if no output is provided, batch process
                        if (output == null)
                        {
                            foreach (string path in paths) Assemble(path);
                        }
                        // otherwise, we're expecting only one input with a named output
                        else
                        {
                            if (paths.Count != 1) { Print("Assemble mode with an explicit output expected only one input assembly file\n"); return 0; }
                            Assemble(paths[0], output);
                        }
                        break;
                    case ProgramAction.Link:
                        // if no output was specified, provide a default
                        if (output == null) output = "a.exe";
                        
                        Link(paths, output);
                        break;
                }
            }

            return 0;
        }

        // -- file io -- //

        /// <summary>
        /// Saves binary data to a file. Returns true if there were no errors
        /// </summary>
        /// <param name="path">the file to save to</param>
        /// <param name="exe">the binary data to save</param>
        private static bool SaveBinaryFile(string path, byte[] exe)
        {
            FileStream f = null; // file handle

            try
            {
                // open the file
                f = File.Open(path, FileMode.Create);

                // write the data
                f.Write(exe, 0, exe.Length);

                return true;
            }

            // things from File.OpenRead
            catch (ArgumentNullException) { Print($"Path was null"); return false; }
            catch (ArgumentException) { Print($"Path \"{path}\" was invalid"); return false; }
            catch (PathTooLongException) { Print($"Path \"{path}\" was too long"); return false; }
            catch (DirectoryNotFoundException) { Print($"Path \"{path}\" directory was not found"); return false; }
            catch (UnauthorizedAccessException) { Print($"You do not have permission to open \"{path}\" for reading"); return false; }
            catch (FileNotFoundException) { Print($"File \"{path}\" could not be found"); return false; }
            catch (NotSupportedException) { Print($"Path \"{path}\" was of an unsupported format"); return false; }
            catch (IOException) { Print($"An error occurred while reading file \"{path}\""); return false; }

            // everything else that might happen for some reason
            catch (Exception ex) { Print($"An error occurred while attempting to execute \"{path}\"\n-> {ex}"); return false; }

            // close the file
            finally { f?.Dispose(); }
        }
        /// <summary>
        /// Loads the contents of a binary file. Returns true if there were no errors
        /// </summary>
        /// <param name="path">the file to read</param>
        /// <param name="exe">the resulting binary data</param>
        private static bool LoadBinaryFile(string path, out byte[] exe)
        {
            exe = null;

            FileStream f = null; // file handle

            try
            {
                // open the file
                f = File.OpenRead(path);

                // read the contents
                exe = new byte[f.Length];
                f.Read(exe, 0, (int)f.Length);

                return true;
            }

            // things from File.OpenRead
            catch (ArgumentNullException) { Print($"Path was null"); return false; }
            catch (ArgumentException) { Print($"Path \"{path}\" was invalid"); return false; }
            catch (PathTooLongException) { Print($"Path \"{path}\" was too long"); return false; }
            catch (DirectoryNotFoundException) { Print($"Path \"{path}\" directory was not found"); return false; }
            catch (UnauthorizedAccessException) { Print($"You do not have permission to open \"{path}\" for reading"); return false; }
            catch (FileNotFoundException) { Print($"File \"{path}\" could not be found"); return false; }
            catch (NotSupportedException) { Print($"Path \"{path}\" was of an unsupported format"); return false; }
            catch (IOException) { Print($"An error occurred while reading file \"{path}\""); return false; }

            // everything else that might happen for some reason
            catch (Exception ex) { Print($"An error occurred while attempting to execute \"{path}\"\n-> {ex}"); return false; }

            // close the file
            finally { f?.Dispose(); }
        }

        /// <summary>
        /// Saves the contents of a text file. Returns true if there were no errors
        /// </summary>
        /// <param name="path">the file to save to</param>
        /// <param name="txt">the text data to save</param>
        private static bool SaveTextFile(string path, out string txt)
        {
            txt = null;

            FileStream f = null; // file handle

            try
            {
                // open the file
                f = File.Open(path, FileMode.Create);

                // write the contents
                using (StreamWriter writer = new StreamWriter(f))
                    writer.Write(txt);

                return true;
            }

            // things from File.OpenRead
            catch (ArgumentNullException) { Print($"Path was null"); return false; }
            catch (ArgumentException) { Print($"Path \"{path}\" was invalid"); return false; }
            catch (PathTooLongException) { Print($"Path \"{path}\" was too long"); return false; }
            catch (DirectoryNotFoundException) { Print($"Path \"{path}\" directory was not found"); return false; }
            catch (UnauthorizedAccessException) { Print($"You do not have permission to open \"{path}\" for reading"); return false; }
            catch (FileNotFoundException) { Print($"File \"{path}\" could not be found"); return false; }
            catch (NotSupportedException) { Print($"Path \"{path}\" was of an unsupported format"); return false; }
            catch (IOException) { Print($"An error occurred while reading file \"{path}\""); return false; }

            // everything else that might happen for some reason
            catch (Exception ex) { Print($"An error occurred while attempting to execute \"{path}\"\n-> {ex}"); return false; }

            // close the file
            finally { f?.Dispose(); }
        }
        /// <summary>
        /// Loads the contents of a text file. Returns true if there were no errors
        /// </summary>
        /// <param name="path">the file to read</param>
        /// <param name="txt">the resulting text data</param>
        private static bool LoadTextFile(string path, out string txt)
        {
            txt = null;

            StreamReader f = null; // file handle

            try
            {
                // open the file
                f = File.OpenText(path);

                // read the contents
                txt = f.ReadToEnd();

                return true;
            }

            // things from File.OpenRead
            catch (ArgumentNullException) { Print($"Path was null"); return false; }
            catch (ArgumentException) { Print($"Path \"{path}\" was invalid"); return false; }
            catch (PathTooLongException) { Print($"Path \"{path}\" was too long"); return false; }
            catch (DirectoryNotFoundException) { Print($"Path \"{path}\" directory was not found"); return false; }
            catch (UnauthorizedAccessException) { Print($"You do not have permission to open \"{path}\" for reading"); return false; }
            catch (FileNotFoundException) { Print($"File \"{path}\" could not be found"); return false; }
            catch (NotSupportedException) { Print($"Path \"{path}\" was of an unsupported format"); return false; }
            catch (IOException) { Print($"An error occurred while reading file \"{path}\""); return false; }

            // everything else that might happen for some reason
            catch (Exception ex) { Print($"An error occurred while attempting to execute \"{path}\"\n-> {ex}"); return false; }

            // close the file
            finally { f?.Dispose(); }
        }

        /// <summary>
        /// Serializes an object file to a file. Returns true if there were no errors
        /// </summary>
        /// <param name="path">the destination file to save to</param>
        /// <param name="obj">the object file to serialize</param>
        private static bool SaveObjectFile(string path, CSX64.ObjectFile obj)
        {
            FileStream f = null; // file handle

            try
            {
                // open the file
                f = File.Open(path, FileMode.Create);

                // serialize the object
                BinaryFormatter bin = new BinaryFormatter();
                bin.Serialize(f, obj);

                return true;
            }

            // things from File.OpenRead
            catch (ArgumentNullException) { Print($"Path was null"); return false; }
            catch (ArgumentException) { Print($"Path \"{path}\" was invalid"); return false; }
            catch (PathTooLongException) { Print($"Path \"{path}\" was too long"); return false; }
            catch (DirectoryNotFoundException) { Print($"Path \"{path}\" directory was not found"); return false; }
            catch (UnauthorizedAccessException) { Print($"You do not have permission to open \"{path}\" for reading"); return false; }
            catch (FileNotFoundException) { Print($"File \"{path}\" could not be found"); return false; }
            catch (NotSupportedException) { Print($"Path \"{path}\" was of an unsupported format"); return false; }
            catch (IOException) { Print($"An error occurred while reading file \"{path}\""); return false; }

            // things from BinaryFormatter.Serialize
            catch (SerializationException) { Print($"An error occurred while saving object file \"{path}\""); return false; }
            catch (SecurityException) { Print($"You do not have permission to serialize object file \"{path}\""); return false; }

            // everything else that might happen for some reason
            catch (Exception ex) { Print($"An error occurred while attempting to execute \"{path}\"\n-> {ex}"); return false; }

            // close the file
            finally { f?.Dispose(); }
        }
        /// <summary>
        /// Deserializes an object file from a file. Returns true if there were no errors
        /// </summary>
        /// <param name="path">the source file to read from</param>
        /// <param name="obj">the resulting object file</param>
        private static bool LoadObjectFile(string path, out CSX64.ObjectFile obj)
        {
            obj = null;

            FileStream f = null; // file handle
            
            try
            {
                // open the file
                f = File.OpenRead(path);
                
                // deserialize the object
                BinaryFormatter bin = new BinaryFormatter();
                obj = (CSX64.ObjectFile)bin.Deserialize(f);

                return true;
            }

            // things from File.OpenRead
            catch (ArgumentNullException) { Print($"Path was null"); return false; }
            catch (ArgumentException) { Print($"Path \"{path}\" was invalid"); return false; }
            catch (PathTooLongException) { Print($"Path \"{path}\" was too long"); return false; }
            catch (DirectoryNotFoundException) { Print($"Path \"{path}\" directory was not found"); return false; }
            catch (UnauthorizedAccessException) { Print($"You do not have permission to open \"{path}\" for reading"); return false; }
            catch (FileNotFoundException) { Print($"File \"{path}\" could not be found"); return false; }
            catch (NotSupportedException) { Print($"Path \"{path}\" was of an unsupported format"); return false; }
            catch (IOException) { Print($"An error occurred while reading file \"{path}\""); return false; }

            // things from BinaryFormatter.Deserialize
            catch (SerializationException) { Print($"file \"{path}\" was not an object file"); return false; }
            catch (SecurityException) { Print($"You do not have permission to serialize object file \"{path}\""); return false; }

            // things from casting after deserialization
            catch (InvalidCastException) { Print($"file \"{path}\" was incorrectly-formatted"); return false; }

            // everything else that might happen for some reason
            catch (Exception ex) { Print($"An error occurred while attempting to execute \"{path}\"\n-> {ex}"); return false; }

            // close the file
            finally { f?.Dispose(); }
        }

        // -- assembly / linking -- //

        /// <summary>
        /// Assembles the (from) file into an object file and saves it to the (to) file. Returns true if successful
        /// </summary>
        /// <param name="from">source assembly file</param>
        /// <param name="to">destination for resulting object file</param>
        private static bool Assemble(string from, string to)
        {
            // read the file contents
            if (!LoadTextFile(from, out string code)) return false;

            // assemble the program
            CSX64.AssembleResult res = CSX64.Assemble(code, out CSX64.ObjectFile obj);

            // if there was no error
            if (res.Error == CSX64.AssembleError.None)
            {
                // save result
                return SaveObjectFile(to, obj);
            }
            // otherwise show error message
            else { Print($"Assemble Error:\n{res.ErrorMsg}"); return false; }
        }
        /// <summary>
        /// Assembles the (from) file into an object file and saves it as the same name but with a .o extension
        /// </summary>
        /// <param name="from">the source assembly file</param>
        private static bool Assemble(string from)
        {
            return Assemble(from, Path.ChangeExtension(from, ".o"));
        }

        /// <summary>
        /// Links several object files to create an executable and saves it to the (to) file. Returns true if successful
        /// </summary>
        /// <param name="paths">the object files to link</param>
        /// <param name="to">destination for the resulting executable</param>
        private static bool Link(List<string> paths, string to)
        {
            // get all the object files
            CSX64.ObjectFile[] objs = new CSX64.ObjectFile[paths.Count];
            for (int i = 0; i < paths.Count; ++i)
                if (!LoadObjectFile(paths[i], out objs[i])) return false;
            
            // link the object files
            CSX64.LinkResult res = CSX64.Link(out byte[] exe, objs);

            // if there was no error
            if (res.Error == CSX64.LinkError.None)
            {
                // save result
                return SaveBinaryFile(to, exe);
            }
            // otherwise show error message
            else { Print($"Link Error:\n{res.ErrorMsg}"); return false; }
        }

        // -- execution -- //

        /// <summary>
        /// Executes a program via the console client. returns true if there were no errors
        /// </summary>
        /// <param name="path">the file to execute</param>
        private static bool RunRawConsole(string path)
        {
            // read the binary data
            if (!LoadBinaryFile(path, out byte[] exe)) return false;

            // run as a console client and return success flag
            return RunRawConsole(exe);
        }
        /// <summary>
        /// Executes a program via the graphical client. returns true if there were no errors
        /// </summary>
        /// <param name="path">the file to execute</param>
        private static bool RunGraphicalClient(string path)
        {
            // read the binary data
            if (!LoadBinaryFile(path, out byte[] exe)) return false;

            // run as a console client and return success flag
            return RunGraphicalClient(exe);
        }

        /// <summary>
        /// Executes a program via the console client. returns true if there were no errors
        /// </summary>
        /// <param name="exe">the code to execute</param>
        private static bool RunRawConsole(byte[] exe)
        {
            // create the computer
            using (CSX64 computer = new CSX64())
            {
                // initialize program
                if (!computer.Initialize(exe)) { Print("Failed to initialize program"); return false; }

                // tie standard streams
                computer.GetFileDescriptor(0).Open(Console.OpenStandardInput(), false, true);
                computer.GetFileDescriptor(1).Open(Console.OpenStandardOutput(), false, false);
                computer.GetFileDescriptor(2).Open(Console.OpenStandardError(), false, false);

                // begin execution
                while (computer.Tick()) ;
            }

            return true;
        }
        /// <summary>
        /// Executes a program via the graphical client. returns true if there were no errors
        /// </summary>
        /// <param name="exe">the code to execute</param>
        private static bool RunGraphicalClient(byte[] exe)
        {
            // create the computer
            using (GraphicalComputer computer = new GraphicalComputer())
            {
                // initialize program
                if (!computer.Initialize(exe)) { Print("Failed to initialize program"); return false; }

                // create the console client
                using (ConsoleClient console = new ConsoleClient(computer))
                {
                    // load colors
                    console.BackColor = BackColor;
                    console.TextColor = ForeColor;

                    // begin execution
                    console.ShowDialog();
                }
            }

            return true;
        }
    }
}
