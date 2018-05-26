﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.Drawing;
using System.IO;
using System.Runtime.Serialization;
using System.Security;

namespace CSX64
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

        /// <summary>
        /// An extension of assembly and link error codes for use specifically in <see cref="Main(string[])"/>
        /// </summary>
        private enum AsmLnkErrorExt
        {
            FailOpen = 100,
            NullPath,
            InvalidPath,
            DirectoryNotFound,
            AccessViolation,
            FileNotFound,
            PathFormatUnsupported,
            IOError,
            FormatError,

            UnknownError = 199
        }

        // ---------------------------------

        /// <summary>
        /// The return value to use in the case of error during execution
        /// </summary>
        private const int ExecReturnCode = -1;

        private const string HelpMessage =
            "\n" +
            "usage: csx64 [<options>] [--] <pathspec>...\n" +
            "\n" +
            "    -h, --help             shows this help mesage\n" +
            "    -g, --graphical        executes a graphical program\n" +
            "    -a, --assemble         assembe files into object files\n" +
            "    -l, --link             link object files into an executable\n" +
            "    -o, --out <pathspec>   specifies explicit output path\n" +
            "        --end              remaining args are pathspec\n" +
            "        --fs               sets the file system flag\n" +
            "\n" +
            "if no -g/-a/-l provided, executes a console program\n" +
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
            if (!BitConverter.IsLittleEndian) { Print("ERROR: This platform is not little-endian"); return 1; }

            // set up initilization thingys
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // run statics fom client sources
            GraphicalComputer.InitStatics();
            
            List<string> pathspec = new List<string>();          // input paths
            string output = null;                                // output path
            ProgramAction action = ProgramAction.ExecuteConsole; // requested action
            bool accepting_options = true;                       // marks that we're still accepting options
            bool fsf = false;                                    // fsf flag

            /*
            // performance testing stuff

            Random rand = new Random();
            byte[] data = new byte[1024];
            rand.NextBytes(data);
            Console.WriteLine("starting");
            UInt64 temp;
            DateTime start = DateTime.Now;
            
            for (int i = 0; i < 100000000; ++i)
            {
                data.Read((UInt64)rand.Next(900), 1ul << rand.Next(4), out temp);
            }

            TimeSpan ts = DateTime.Now - start;
            Console.WriteLine($"t: {ts.TotalMilliseconds}ms");
            Console.ReadLine();
            */

            // process the terminal args
            for (int i = 0; i < args.Length; ++i)
            {
                // if we're still accepting options
                if (accepting_options)
                {
                    // parse as an option
                    switch (args[i])
                    {
                        // do the long names
                        case "--help": Print(HelpMessage); return 0;
                        case "--graphical": if (action != ProgramAction.ExecuteConsole) { Print("usage error - see -h for help"); return 0; } action = ProgramAction.ExecuteGraphical; break;
                        case "--assemble": if (action != ProgramAction.ExecuteConsole) { Print("usage error - see -h for help"); return 0; } action = ProgramAction.Assemble; break;
                        case "--link": if (action != ProgramAction.ExecuteConsole) { Print("usage error - see -h for help"); return 0; } action = ProgramAction.Link; break;
                        case "--output": if (output != null || i + 1 >= args.Length) { Print("usage error - see -h for help"); return 0; } output = args[++i]; break;
                        case "--end": accepting_options = false; break;
                        case "--fs": fsf = true; break;
                        case "--": break; // -- is a no-op separator

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
                            // otherwise it's part of pathspec
                            else pathspec.Add(args[i]);

                            break;
                    }
                }
                // if we're not accepting options, it's part of pathspec
                else pathspec.Add(args[i]);
            }

            // perform the action
            switch (action)
            {
                case ProgramAction.ExecuteConsole:
                    if (pathspec.Count == 0) { Print("Execution mode expected a file to execute"); return 0; }

                    // first path is file to run, then pass all of pathspec as command line args for client code
                    return RunRawConsole(pathspec[0], pathspec.ToArray(), fsf);
                case ProgramAction.ExecuteGraphical:
                    if (pathspec.Count == 0) { Print("Graphical execution mode expected a file to execute"); return 0; }

                    // first path is file to run, then pass all of pathspec as command line args for client code
                    return RunGraphicalClient(pathspec[0], pathspec.ToArray(), fsf);

                case ProgramAction.Assemble:
                    // if no output is provided, batch process
                    if (output == null)
                    {
                        foreach (string path in pathspec)
                        {
                            int res = Assemble(path);
                            if (res != 0) return res;
                        }
                    }
                    // otherwise, we're expecting only one input with a named output
                    else
                    {
                        if (pathspec.Count != 1) { Print("Assemble mode with an explicit output expected only one input assembly file\n"); return 0; }
                        return Assemble(pathspec[0], output);
                    }
                    break;
                case ProgramAction.Link:
                    // if no output was specified, provide a default
                    if (output == null) output = "a.exe";

                    Link(pathspec, output);
                    break;
            }

            return 0;
        }

        // -- file io -- //

        /// <summary>
        /// Saves binary data to a file. Returns true if there were no errors
        /// </summary>
        /// <param name="path">the file to save to</param>
        /// <param name="exe">the binary data to save</param>
        private static int SaveBinaryFile(string path, byte[] exe)
        {
            FileStream f = null; // file handle

            try
            {
                // open the file
                f = File.Open(path, FileMode.Create);

                // write the data
                f.Write(exe, 0, exe.Length);

                return 0;
            }

            // things from File.OpenRead
            catch (ArgumentNullException) { Print($"Path was null"); return (int)AsmLnkErrorExt.NullPath; }
            catch (ArgumentException) { Print($"Path \"{path}\" was invalid"); return (int)AsmLnkErrorExt.InvalidPath; }
            catch (PathTooLongException) { Print($"Path \"{path}\" was too long"); return (int)AsmLnkErrorExt.InvalidPath; }
            catch (DirectoryNotFoundException) { Print($"Path \"{path}\" directory was not found"); return (int)AsmLnkErrorExt.DirectoryNotFound; }
            catch (UnauthorizedAccessException) { Print($"You do not have permission to open \"{path}\" for reading"); return (int)AsmLnkErrorExt.AccessViolation; }
            catch (FileNotFoundException) { Print($"File \"{path}\" could not be found"); return (int)AsmLnkErrorExt.FileNotFound; }
            catch (NotSupportedException) { Print($"Path \"{path}\" was of an unsupported format"); return (int)AsmLnkErrorExt.PathFormatUnsupported; }
            catch (IOException) { Print($"An error occurred while reading file \"{path}\""); return (int)AsmLnkErrorExt.IOError; }

            // everything else that might happen for some reason
            catch (Exception ex) { Print($"An error occurred while attempting to execute \"{path}\"\n-> {ex}"); return (int)AsmLnkErrorExt.UnknownError; }

            // close the file
            finally { f?.Dispose(); }
        }
        /// <summary>
        /// Loads the contents of a binary file. Returns true if there were no errors
        /// </summary>
        /// <param name="path">the file to read</param>
        /// <param name="exe">the resulting binary data</param>
        private static int LoadBinaryFile(string path, out byte[] exe)
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

                return 0;
            }

            // things from File.OpenRead
            catch (ArgumentNullException) { Print($"Path was null"); return (int)AsmLnkErrorExt.NullPath; }
            catch (ArgumentException) { Print($"Path \"{path}\" was invalid"); return (int)AsmLnkErrorExt.InvalidPath; }
            catch (PathTooLongException) { Print($"Path \"{path}\" was too long"); return (int)AsmLnkErrorExt.InvalidPath; }
            catch (DirectoryNotFoundException) { Print($"Path \"{path}\" directory was not found"); return (int)AsmLnkErrorExt.DirectoryNotFound; }
            catch (UnauthorizedAccessException) { Print($"You do not have permission to open \"{path}\" for reading"); return (int)AsmLnkErrorExt.AccessViolation; }
            catch (FileNotFoundException) { Print($"File \"{path}\" could not be found"); return (int)AsmLnkErrorExt.FileNotFound; }
            catch (NotSupportedException) { Print($"Path \"{path}\" was of an unsupported format"); return (int)AsmLnkErrorExt.PathFormatUnsupported; }
            catch (IOException) { Print($"An error occurred while reading file \"{path}\""); return (int)AsmLnkErrorExt.IOError; }

            // everything else that might happen for some reason
            catch (Exception ex) { Print($"An error occurred while attempting to execute \"{path}\"\n-> {ex}"); return (int)AsmLnkErrorExt.UnknownError; }

            // close the file
            finally { f?.Dispose(); }
        }

        /// <summary>
        /// Saves the contents of a text file. Returns true if there were no errors
        /// </summary>
        /// <param name="path">the file to save to</param>
        /// <param name="txt">the text data to save</param>
        private static int SaveTextFile(string path, out string txt)
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

                return 0;
            }

            // things from File.OpenRead
            catch (ArgumentNullException) { Print($"Path was null"); return (int)AsmLnkErrorExt.NullPath; }
            catch (ArgumentException) { Print($"Path \"{path}\" was invalid"); return (int)AsmLnkErrorExt.InvalidPath; }
            catch (PathTooLongException) { Print($"Path \"{path}\" was too long"); return (int)AsmLnkErrorExt.InvalidPath; }
            catch (DirectoryNotFoundException) { Print($"Path \"{path}\" directory was not found"); return (int)AsmLnkErrorExt.DirectoryNotFound; }
            catch (UnauthorizedAccessException) { Print($"You do not have permission to open \"{path}\" for reading"); return (int)AsmLnkErrorExt.AccessViolation; }
            catch (FileNotFoundException) { Print($"File \"{path}\" could not be found"); return (int)AsmLnkErrorExt.FileNotFound; }
            catch (NotSupportedException) { Print($"Path \"{path}\" was of an unsupported format"); return (int)AsmLnkErrorExt.PathFormatUnsupported; }
            catch (IOException) { Print($"An error occurred while reading file \"{path}\""); return (int)AsmLnkErrorExt.IOError; }

            // everything else that might happen for some reason
            catch (Exception ex) { Print($"An error occurred while attempting to execute \"{path}\"\n-> {ex}"); return (int)AsmLnkErrorExt.UnknownError; }

            // close the file
            finally { f?.Dispose(); }
        }
        /// <summary>
        /// Loads the contents of a text file. Returns true if there were no errors
        /// </summary>
        /// <param name="path">the file to read</param>
        /// <param name="txt">the resulting text data</param>
        private static int LoadTextFile(string path, out string txt)
        {
            txt = null;

            StreamReader f = null; // file handle

            try
            {
                // open the file
                f = File.OpenText(path);

                // read the contents
                txt = f.ReadToEnd();

                return 0;
            }

            // things from File.OpenRead
            catch (ArgumentNullException) { Print($"Path was null"); return (int)AsmLnkErrorExt.NullPath; }
            catch (ArgumentException) { Print($"Path \"{path}\" was invalid"); return (int)AsmLnkErrorExt.InvalidPath; }
            catch (PathTooLongException) { Print($"Path \"{path}\" was too long"); return (int)AsmLnkErrorExt.InvalidPath; }
            catch (DirectoryNotFoundException) { Print($"Path \"{path}\" directory was not found"); return (int)AsmLnkErrorExt.DirectoryNotFound; }
            catch (UnauthorizedAccessException) { Print($"You do not have permission to open \"{path}\" for reading"); return (int)AsmLnkErrorExt.AccessViolation; }
            catch (FileNotFoundException) { Print($"File \"{path}\" could not be found"); return (int)AsmLnkErrorExt.FileNotFound; }
            catch (NotSupportedException) { Print($"Path \"{path}\" was of an unsupported format"); return (int)AsmLnkErrorExt.PathFormatUnsupported; }
            catch (IOException) { Print($"An error occurred while reading file \"{path}\""); return (int)AsmLnkErrorExt.IOError; }

            // everything else that might happen for some reason
            catch (Exception ex) { Print($"An error occurred while attempting to execute \"{path}\"\n-> {ex}"); return (int)AsmLnkErrorExt.UnknownError; }

            // close the file
            finally { f?.Dispose(); }
        }

        /// <summary>
        /// Serializes an object file to a file. Returns true if there were no errors
        /// </summary>
        /// <param name="path">the destination file to save to</param>
        /// <param name="obj">the object file to serialize</param>
        private static int SaveObjectFile(string path, ObjectFile obj)
        {
            FileStream f = null; // file handle

            try
            {
                // open the file
                f = File.Open(path, FileMode.Create);

                // serialize the object
                using (BinaryWriter writer = new BinaryWriter(f))
                    ObjectFile.WriteTo(writer, obj);

                return 0;
            }

            // things from File.OpenRead
            catch (ArgumentNullException) { Print($"Path was null"); return (int)AsmLnkErrorExt.NullPath; }
            catch (ArgumentException) { Print($"Path \"{path}\" was invalid"); return (int)AsmLnkErrorExt.InvalidPath; }
            catch (PathTooLongException) { Print($"Path \"{path}\" was too long"); return (int)AsmLnkErrorExt.InvalidPath; }
            catch (DirectoryNotFoundException) { Print($"Path \"{path}\" directory was not found"); return (int)AsmLnkErrorExt.DirectoryNotFound; }
            catch (UnauthorizedAccessException) { Print($"You do not have permission to open \"{path}\" for reading"); return (int)AsmLnkErrorExt.AccessViolation; }
            catch (FileNotFoundException) { Print($"File \"{path}\" could not be found"); return (int)AsmLnkErrorExt.FileNotFound; }
            catch (NotSupportedException) { Print($"Path \"{path}\" was of an unsupported format"); return (int)AsmLnkErrorExt.PathFormatUnsupported; }
            catch (IOException) { Print($"An error occurred while reading file \"{path}\""); return (int)AsmLnkErrorExt.IOError; }

            // everything else that might happen for some reason
            catch (Exception ex) { Print($"An error occurred while attempting to execute \"{path}\"\n-> {ex}"); return (int)AsmLnkErrorExt.UnknownError; }

            // close the file
            finally { f?.Dispose(); }
        }
        /// <summary>
        /// Deserializes an object file from a file. Returns true if there were no errors
        /// </summary>
        /// <param name="path">the source file to read from</param>
        /// <param name="obj">the resulting object file</param>
        private static int LoadObjectFile(string path, out ObjectFile obj)
        {
            obj = null;

            FileStream f = null; // file handle

            try
            {
                // open the file
                f = File.OpenRead(path);

                // deserialize the object
                using (BinaryReader reader = new BinaryReader(f))
                    ObjectFile.ReadFrom(reader, out obj);

                return 0;
            }

            // things from File.OpenRead
            catch (ArgumentNullException) { Print($"Path was null"); return (int)AsmLnkErrorExt.NullPath; }
            catch (ArgumentException) { Print($"Path \"{path}\" was invalid"); return (int)AsmLnkErrorExt.InvalidPath; }
            catch (PathTooLongException) { Print($"Path \"{path}\" was too long"); return (int)AsmLnkErrorExt.InvalidPath; }
            catch (DirectoryNotFoundException) { Print($"Path \"{path}\" directory was not found"); return (int)AsmLnkErrorExt.DirectoryNotFound; }
            catch (UnauthorizedAccessException) { Print($"You do not have permission to open \"{path}\" for reading"); return (int)AsmLnkErrorExt.AccessViolation; }
            catch (FileNotFoundException) { Print($"File \"{path}\" could not be found"); return (int)AsmLnkErrorExt.FileNotFound; }
            catch (NotSupportedException) { Print($"Path \"{path}\" was of an unsupported format"); return (int)AsmLnkErrorExt.PathFormatUnsupported; }
            catch (IOException) { Print($"An error occurred while reading file \"{path}\""); return (int)AsmLnkErrorExt.IOError; }

            // things from ObjectFile.ReadFrom
            catch (FormatException) { Print($"Object file \"{path}\" was corrupted"); return (int)AsmLnkErrorExt.FormatError; }

            // things from casting after deserialization
            catch (InvalidCastException) { Print($"file \"{path}\" was incorrectly-formatted"); return (int)AsmLnkErrorExt.FormatError; }

            // everything else that might happen for some reason
            catch (Exception ex) { Print($"An error occurred while attempting to execute \"{path}\"\n-> {ex}"); return (int)AsmLnkErrorExt.UnknownError; }

            // close the file
            finally { f?.Dispose(); }
        }

        // -- assembly / linking -- //

        /// <summary>
        /// Assembles the (from) file into an object file and saves it to the (to) file. Returns true if successful
        /// </summary>
        /// <param name="from">source assembly file</param>
        /// <param name="to">destination for resulting object file</param>
        private static int Assemble(string from, string to)
        {
            // read the file contents
            int ret = LoadTextFile(from, out string code);
            if (ret != 0) return ret;

            // assemble the program
            AssembleResult res = Assembly.Assemble(code, out ObjectFile obj);

            // if there was no error
            if (res.Error == AssembleError.None)
            {
                // save result
                return SaveObjectFile(to, obj);
            }
            // otherwise show error message
            else { Print($"Assemble Error:\n{res.ErrorMsg}"); return (int)res.Error; }
        }
        /// <summary>
        /// Assembles the (from) file into an object file and saves it as the same name but with a .o extension
        /// </summary>
        /// <param name="from">the source assembly file</param>
        private static int Assemble(string from)
        {
            return Assemble(from, Path.ChangeExtension(from, ".o"));
        }

        /// <summary>
        /// Links several object files to create an executable and saves it to the (to) file. Returns true if successful
        /// </summary>
        /// <param name="paths">the object files to link</param>
        /// <param name="to">destination for the resulting executable</param>
        private static int Link(List<string> paths, string to)
        {
            // get all the object files
            ObjectFile[] objs = new ObjectFile[paths.Count];
            for (int i = 0; i < paths.Count; ++i)
            {
                int ret = LoadObjectFile(paths[i], out objs[i]);
                if (ret != 0) return ret; 
            }

            // link the object files
            LinkResult res = Assembly.Link(out byte[] exe, objs);

            // if there was no error
            if (res.Error == LinkError.None)
            {
                // save result
                return SaveBinaryFile(to, exe);
            }
            // otherwise show error message
            else { Print($"Link Error:\n{res.ErrorMsg}"); return (int)res.Error; }
        }

        // -- execution -- //

        /// <summary>
        /// Executes a program via the console client. returns true if there were no errors
        /// </summary>
        /// <param name="path">the file to execute</param>
        /// <param name="args">the command line arguments for the client program</param>
        private static int RunRawConsole(string path, string[] args, bool fsf)
        {
            // read the binary data
            int ret = LoadBinaryFile(path, out byte[] exe);
            if (ret != 0) return ret;

            // run as a console client and return success flag
            return RunRawConsole(exe, args, fsf);
        }
        /// <summary>
        /// Executes a program via the graphical client. returns true if there were no errors
        /// </summary>
        /// <param name="path">the file to execute</param>
        private static int RunGraphicalClient(string path, string[] args, bool fsf)
        {
            // read the binary data
            int ret = LoadBinaryFile(path, out byte[] exe);
            if (ret != 0) return ret;

            // run as a console client and return success flag
            return RunGraphicalClient(exe, args, fsf);
        }

        /// <summary>
        /// Executes a program via the console client. returns true if there were no errors
        /// </summary>
        /// <param name="exe">the code to execute</param>
        /// <param name="args">the command line arguments for the client program</param>
        private static int RunRawConsole(byte[] exe, string[] args, bool fsf)
        {
            // create the computer
            using (Computer computer = new Computer())
            {
                // initialize program
                computer.Initialize(exe, args);

                // set private flags
                computer.FSF = fsf;

                // tie standard streams
                computer.GetFD(0).Open(Console.OpenStandardInput(), false, false); // stdin is non-interactive because we're not the ones that will be adding data to it. that's the console's responsibility
                computer.GetFD(1).Open(Console.OpenStandardOutput(), false, false);
                computer.GetFD(2).Open(Console.OpenStandardError(), false, false);

                // begin execution
                while (computer.Tick()) ;

                // if there was an error
                if (computer.Error != ErrorCode.None)
                {
                    // print error message
                    Print($"\n\nError Encountered: {computer.Error}");
                    // return execution error code
                    return ExecReturnCode;
                }
                // otherwise use return value
                else return computer.ReturnValue;
            }
        }
        /// <summary>
        /// Executes a program via the graphical client. returns true if there were no errors
        /// </summary>
        /// <param name="exe">the code to execute</param>
        private static int RunGraphicalClient(byte[] exe, string[] args, bool fsf)
        {
            // create the computer
            using (GraphicalComputer computer = new GraphicalComputer())
            {
                // initialize program
                computer.Initialize(exe, args);

                // set private flags
                computer.FSF = fsf;

                // create the console client
                using (GraphicalClient graphics = new GraphicalClient(computer))
                {
                    // begin execution
                    graphics.ShowDialog();

                    // if there was an error
                    if (computer.Error != ErrorCode.None)
                    {
                        // print error message
                        Print($"\n\nError Encountered: {computer.Error}");
                        // return execution error code
                        return ExecReturnCode;
                    }
                    // otherwise use return value
                    else return computer.ReturnValue;
                }
            }
        }
    }
}
