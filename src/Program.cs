using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.Drawing;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Diagnostics;

namespace CSX64
{
    static class Program
    {
        /// <summary>
        /// Represents a desired action
        /// </summary>
        private enum ProgramAction
        {
            ExecuteConsole, // takes 1 csx64 executable + args -- executes as a console application

			ExecuteConsoleScript, // takes 1 csx64 assembly file + args -- compiles, links, and executes in memory as a console application
			ExecuteConsoleMultiscript, // takes 1+ csx64 assembly files -- compiles, links, and executes in memory as a console application

			ExecuteGraphical, // takes 1 csx64 executable + args -- executes as a graphical application

            Assemble, // assembles 1+ csx64 assembly files into csx64 object files
			Link,     // links 1+ csx64 object files into a csx64 executable
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

			MemoryAllocError = 197,
			ComputerInitError = 198,

            UnknownError = 199
        }

        // ---------------------------------

        /// <summary>
        /// The return value to use in the case of error during execution
        /// </summary>
        private const int ExecErrorReturnCode = -1;

        private const string HelpMessage =
@"(
usage: csx [<options>...] [--] <pathspec>...

 -h, --help             shows this help info

 -a, --assemble         assemble csx64 asm files into csx64 object files
 -l, --link             link csx64 object files into a csx64 executable
 -s, --script           assemble, link, and execute a csx64 asm file in memory
 -S, --multiscript      as --script, but takes multiple csx64 asm files
 otherwise              executes a csx64 executable as a console application

 -o, --out <pathspec>   specifies explicit output path
 --entry <entry>        main entry point for linker

 --fs                   sets the file system flag during execution
 --time                 after execution display elapsed time
 --end                  remaining args are pathspec

report bugs to https://github.com/dragazo/CSX64-cpp/issues
)";

        /// <summary>
        /// The path to the executable's directory
        /// </summary>
        private static string ExeDir => AppDomain.CurrentDomain.BaseDirectory;

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
        private static unsafe int Main(string[] args)
        {
            if (!BitConverter.IsLittleEndian) { Print("ERROR: This platform is not little-endian"); return -1; }

            // set up initilization thingys for graphical stuff
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // run statics fom client sources
            GraphicalComputer.InitStatics();

            ProgramAction action = ProgramAction.ExecuteConsole; // requested action
            List<string> pathspec = new List<string>();          // input paths
            string entry_point = null;                           // main entry point for linker
            string output = null;                                // output path
			string rootdir = null;                               // root directory to use for std lookup 
            bool fsf = false;                                    // fsf flag
            bool time = false;                                   // time flag
            bool accepting_options = true;                       // marks that we're still accepting options

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
						case "--script": if (action != ProgramAction.ExecuteConsole) { Print("usage error - see -h for help"); return 0; } action = ProgramAction.ExecuteConsoleScript; break;
						case "--multiscript": if (action != ProgramAction.ExecuteConsole) { Print("usage error - see -h for help"); return 0; } action = ProgramAction.ExecuteConsoleMultiscript; break;
                        case "--output": if (output != null || i + 1 >= args.Length) { Print("usage error - see -h for help"); return 0; } output = args[++i]; break;
                        case "--entry": if (entry_point != null || i + 1 >= args.Length) { Print("usage error - see -h for help"); return 0; } entry_point = args[++i]; break;
						case "--rootdir": if (rootdir != null || i + 1 >= args.Length) { Print("usage error - see -h for help"); return 0; } rootdir = args[++i]; break;
                        case "--end": accepting_options = false; break;
                        case "--fs": fsf = true; break;
                        case "--time": time = true; break;
                        
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
										case 's': if (action != ProgramAction.ExecuteConsole) { Print("usage error - see -h for help"); return 0; } action = ProgramAction.ExecuteConsoleScript; break;
										case 'S': if (action != ProgramAction.ExecuteConsole) { Print("usage error - see -h for help"); return 0; } action = ProgramAction.ExecuteConsoleMultiscript; break;
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
                    if (pathspec.Count == 0) { Print("Expected a file to execute"); return 0; }
                    return RunConsole(pathspec[0], pathspec.ToArray(), fsf, time);

				case ProgramAction.ExecuteConsoleScript:

					// add the assembler predefines now
					AddPredefines();

					if (pathspec.Count == 0) { Print("Expected a file to assemble, link, and execute"); return 0; }
					return RunConsoleScript(new List<string>() { pathspec[0] }, entry_point ?? "main", rootdir, pathspec.ToArray(), fsf, time);

				case ProgramAction.ExecuteConsoleMultiscript:

					// add the assembler predefines now
					AddPredefines();

					if (pathspec.Count != 0) { Print("Expected 1+ files to assemble, link, and execute"); return 0; }
					return RunConsoleScript(pathspec, entry_point ?? "main", rootdir, new string[] { "<script>" }, fsf, time);

				case ProgramAction.ExecuteGraphical:
                    if (pathspec.Count == 0) { Print("Expected a file to execute"); return 0; }
                    return RunGraphical(pathspec[0], pathspec.ToArray(), fsf);

                case ProgramAction.Assemble:
                    if (pathspec.Count == 0) { Print("Assembler expected at least 1 file to assemble"); return 0; }

                    // add the assembler predefines now
                    AddPredefines();

                    if (output == null) // if no output is provided, batch process each pathspec
                    {
                        foreach (string path in pathspec)
                        {
                            int res = Assemble(path);
                            if (res != 0) return res;
                        }
                        
                        return 0;
                    }
                    else // otherwise, we're expecting only one input with a named output
                    {
                        if (pathspec.Count != 1) { Print("Assembler with an explicit output expected only one input\n"); return 0; }
                        return Assemble(pathspec[0], output);
                    }

                case ProgramAction.Link:
                    if (pathspec.Count == 0) { Print("Linker expected at least 1 file to link"); return 0; }
                    return Link(pathspec, output ?? "a.exe", entry_point ?? "main", rootdir);
            }

            return 0;
        }

        // -- init -- //

        // adds standard symbols to the assembler predefine table
        private static void AddPredefines()
        {
            // -- syscall codes -- //

            Assembly.DefineSymbol("sys_exit", (UInt64)SyscallCode.sys_exit);

            Assembly.DefineSymbol("sys_read", (UInt64)SyscallCode.sys_read);
            Assembly.DefineSymbol("sys_write", (UInt64)SyscallCode.sys_write);
            Assembly.DefineSymbol("sys_open", (UInt64)SyscallCode.sys_open);
            Assembly.DefineSymbol("sys_close", (UInt64)SyscallCode.sys_close);
            Assembly.DefineSymbol("sys_lseek", (UInt64)SyscallCode.sys_lseek);

            Assembly.DefineSymbol("sys_brk", (UInt64)SyscallCode.sys_brk);

            Assembly.DefineSymbol("sys_rename", (UInt64)SyscallCode.sys_rename);
            Assembly.DefineSymbol("sys_unlink", (UInt64)SyscallCode.sys_unlink);
            Assembly.DefineSymbol("sys_mkdir", (UInt64)SyscallCode.sys_mkdir);
            Assembly.DefineSymbol("sys_rmdir", (UInt64)SyscallCode.sys_rmdir);

            // -- error codes -- //

            Assembly.DefineSymbol("err_none", (UInt64)ErrorCode.None);
            Assembly.DefineSymbol("err_outofbounds", (UInt64)ErrorCode.OutOfBounds);
            Assembly.DefineSymbol("err_unhandledsyscall", (UInt64)ErrorCode.UnhandledSyscall);
            Assembly.DefineSymbol("err_undefinedbehavior", (UInt64)ErrorCode.UndefinedBehavior);
            Assembly.DefineSymbol("err_arithmeticerror", (UInt64)ErrorCode.ArithmeticError);
            Assembly.DefineSymbol("err_abort", (UInt64)ErrorCode.Abort);
            Assembly.DefineSymbol("err_iofailure", (UInt64)ErrorCode.IOFailure);
            Assembly.DefineSymbol("err_fsdisabled", (UInt64)ErrorCode.FSDisabled);
            Assembly.DefineSymbol("err_accessviolation", (UInt64)ErrorCode.AccessViolation);
            Assembly.DefineSymbol("err_insufficientfds", (UInt64)ErrorCode.InsufficientFDs);
            Assembly.DefineSymbol("err_fdnotinuse", (UInt64)ErrorCode.FDNotInUse);
            Assembly.DefineSymbol("err_notimplemented", (UInt64)ErrorCode.NotImplemented);
            Assembly.DefineSymbol("err_stackoverflow", (UInt64)ErrorCode.StackOverflow);
            Assembly.DefineSymbol("err_fpustackoverflow", (UInt64)ErrorCode.FPUStackOverflow);
            Assembly.DefineSymbol("err_fpustackunderflow", (UInt64)ErrorCode.FPUStackUnderflow);
            Assembly.DefineSymbol("err_fpuerror", (UInt64)ErrorCode.FPUError);
            Assembly.DefineSymbol("err_fpuaccessviolation", (UInt64)ErrorCode.FPUAccessViolation);
            Assembly.DefineSymbol("err_alignmentviolation", (UInt64)ErrorCode.AlignmentViolation);
            Assembly.DefineSymbol("err_unknownop", (UInt64)ErrorCode.UnknownOp);

            // -- file open modes -- //

            Assembly.DefineSymbol("O_RDONLY", (UInt64)OpenFlags.read);
            Assembly.DefineSymbol("O_WRONLY", (UInt64)OpenFlags.write);
            Assembly.DefineSymbol("O_RDWR", (UInt64)OpenFlags.read_write);

            Assembly.DefineSymbol("O_CREAT", (UInt64)OpenFlags.create);
            Assembly.DefineSymbol("O_TMPFILE", (UInt64)OpenFlags.temp);
            Assembly.DefineSymbol("O_TRUNC", (UInt64)OpenFlags.trunc);

            Assembly.DefineSymbol("O_APPEND", (UInt64)OpenFlags.append);

            // -- file seek modes -- //

            Assembly.DefineSymbol("SEEK_SET", (UInt64)SeekMode.set);
            Assembly.DefineSymbol("SEEK_CUR", (UInt64)SeekMode.cur);
            Assembly.DefineSymbol("SEEK_END", (UInt64)SeekMode.end);
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

        /// <summary>
        /// Loads an object file and adds it to the list
        /// </summary>
        /// <param name="objs">the list of object files</param>
        /// <param name="path">the file to to load</param>
        private static int LoadObjectFile(this List<ObjectFile> objs, string path)
        {
            int ret = LoadObjectFile(path, out ObjectFile obj);
            if (ret != 0) return ret;

            objs.Add(obj);

            return 0;
        }
        /// <summary>
        /// Loads the .o object files from a directory and adds them to the list
        /// </summary>
        /// <param name="objs">the list of object files</param>
        /// <param name="path">the directory to load</param>
        private static int LoadObjectFileDir(this List<ObjectFile> objs, string path)
        {
            if (!Directory.Exists(path)) { Print($"no directory found: {path}"); return (int)AsmLnkErrorExt.DirectoryNotFound; }

            string[] files = Directory.GetFiles(path, "*.o");
            foreach (string file in files)
            {
                int ret = LoadObjectFile(file, out ObjectFile obj);
                if (ret != 0) return ret;

                objs.Add(obj);
            }

            return 0;
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
            else
			{
				Print($"Assemble Error in {from}:\n{res.ErrorMsg}");
				return (int)res.Error;
			}
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
		/// Loads the stdlib object files and appends them to the back of the list.
		/// </summary>
		/// <param name="objs">the destination for storing loaded stdlib object files.</param>
		/// <param name="rootdir">the root directory to use for code file lookup - null for default</param>
		private static int LoadStdlibObjs(List<ObjectFile> objs, string rootdir)
		{
			// get exe directory - default to provided root dir if present
			string dir = rootdir ?? ExeDir;

			// in the C++ impl that can fail, but in C# it can't - fail case omitted

			// load the _start fie
			int ret = LoadObjectFile(objs, dir + "/_start.o");
			if (ret != 0) return ret;

			// load the stdlib files
			ret = LoadObjectFileDir(objs, dir + "/stdlib");
			if (ret != 0) return ret;

			return 0;
		}

        /// <summary>
        /// Links several object files to create an executable and saves it to the (to) file. Returns true if successful
        /// </summary>
        /// <param name="paths">the object files to link</param>
        /// <param name="to">destination for the resulting executable</param>
        /// <param name="entry_point">the main entry point</param>
		/// <param name="rootdir">the root directory to use for core file lookup - null for default</param>
        private static int Link(List<string> paths, string to, string entry_point, string rootdir)
        {
            List<ObjectFile> objs = new List<ObjectFile>(paths.Count);

			// load the stdlib files
			int ret = LoadStdlibObjs(objs, rootdir);
			if (ret != 0) return ret;

            // load the user-defined pathspecs
            foreach (string path in paths)
            {
                ret = objs.LoadObjectFile(path);
                if (ret != 0) return ret;
            }

            // link the object files
            LinkResult res = Assembly.Link(out byte[] exe, objs.ToArray(), entry_point);

            // if there was no error
            if (res.Error == LinkError.None)
            {
                // save result
                return SaveBinaryFile(to, exe);
            }
            // otherwise show error message
            else
			{
				Print($"Link Error:\n{res.ErrorMsg}");
				return (int)res.Error;
			}
        }

		/// <summary>
		/// Takes one or more assembly files. Assembles and links them into an executable (stored in exe).
		/// </summary>
		/// <param name="exe">destination for resulting executable data (on success).</param>
		/// <param name="files">a list of one or more assembly files.</param>
		/// <param name="entry_point">the main program entry point to use (for linking).</param>
		/// <param name="rootdir">the root directory to use for core file lookup - null for default.</param>
		private static int AssembleAndLink(out byte[] exe, List<string> files, string entry_point, string rootdir)
		{
			var objs = new List<ObjectFile>();
			exe = null;

			// load the stdlib object files
			int ret = LoadStdlibObjs(objs, rootdir);
			if (ret != 0) return ret;

			// assemble all the provided (assembly) files
			foreach (string file in files)
			{
				// read the file contents
				ret = LoadTextFile(file, out string code);
				if (ret != 0) return ret;

				// assemble the program
				AssembleResult asm_res = Assembly.Assemble(code, out ObjectFile obj);

				// if there was an error shor error message
				if (asm_res.Error != AssembleError.None)
				{
					Print($"Assemble Error in {file}:\n{asm_res.ErrorMsg}");
					return (int)asm_res.Error;
				}

				// add the new object file to the objs list
				objs.Add(obj);
			}

			// link all the object files and get the resulting executable
			LinkResult lnk_res = Assembly.Link(out exe, objs.ToArray(), entry_point);

			// if there was an error show error message
			if (lnk_res.Error != LinkError.None)
			{
				Print($"Link Error:\n{lnk_res.ErrorMsg}");
				return (int)lnk_res.Error;
			}

			return 0;
		}

        // -- execution -- //

        /// <summary>
        /// Executes a program via the console client. returns true if there were no errors
        /// </summary>
        /// <param name="path">the file to execute</param>
        /// <param name="args">the command line arguments for the client program</param>
        private static int RunConsole(string path, string[] args, bool fsf, bool time)
        {
            // read the binary data
            int ret = LoadBinaryFile(path, out byte[] exe);
            if (ret != 0) return ret;

            // run as a console client and return success flag
            return RunConsole(exe, args, fsf, time);
        }
        /// <summary>
        /// Executes a program via the graphical client. returns true if there were no errors
        /// </summary>
        /// <param name="path">the file to execute</param>
        private static int RunGraphical(string path, string[] args, bool fsf)
        {
            // read the binary data
            int ret = LoadBinaryFile(path, out byte[] exe);
            if (ret != 0) return ret;

            // run as a console client and return success flag
            return RunGraphical(exe, args, fsf);
        }

		/// <summary>
		/// Takes one or more assembly files. Assembles, links, and executes the result in memory as a console application.
		/// </summary>
		/// <param name="files">a list of one or more assembly files.</param>
		/// <param name="entry_point">the main program entry point to use (for linking).</param>
		/// <param name="rootdir">the root directory to use for core file lookup - null for default.</param>
		/// <param name="args">a list of command line args to provide the program during execution.</param>
		/// <param name="fsf">marks if the fsf flag is set during execution.</param>
		/// <param name="time">marks if the execution phase should be times.</param>
		/// <returns></returns>
		private static int RunConsoleScript(List<string> files, string entry_point, string rootdir, string[] args, bool fsf, bool time)
		{
			int ret = AssembleAndLink(out byte[] exe, files, entry_point, rootdir);
			if (ret != 0) return ret;

			return RunConsole(exe, args, fsf, time);
		}

        /// <summary>
        /// Executes a program via the console client. returns true if there were no errors
        /// </summary>
        /// <param name="exe">the code to execute</param>
        /// <param name="args">the command line arguments for the client program</param>
        private static int RunConsole(byte[] exe, string[] args, bool fsf, bool time)
        {
            // create the computer
            using (Computer computer = new Computer())
            {
				try
				{
					// initialize program
					computer.Initialize(exe, args);
				}
				catch (ExecutableFormatError ex)
				{
					Print(ex.Message);
					return (int)AsmLnkErrorExt.FormatError;
				}
				catch (MemoryAllocException ex)
				{
					Print(ex.Message);
					return (int)AsmLnkErrorExt.MemoryAllocError;
				}

                // set private flags
                computer.FSF = fsf;

                // this usage is just going for raw speed, so enable OTRF
                computer.OTRF = true;

                // tie standard streams - stdin is non-interactive because we don't control it
                computer.OpenFileWrapper(0, new BasicFileWrapper(Console.OpenStandardInput(), false, false, true, false, false)); 
                computer.OpenFileWrapper(1, new BasicFileWrapper(Console.OpenStandardOutput(), false, false, false, true, false));
                computer.OpenFileWrapper(2, new BasicFileWrapper(Console.OpenStandardError(), false, false, false, true, false));

                // begin execution
                DateTime start = DateTime.Now;
                while (computer.Running) computer.Tick(UInt64.MaxValue - 1);
                DateTime stop = DateTime.Now;

                // if there was an error
                if (computer.Error != ErrorCode.None)
                {
                    // print error message
                    Print($"\n\nError Encountered: {computer.Error}");
                    // return execution error code
                    return ExecErrorReturnCode;
                }
                // otherwise no error
                else
                {
                    // print elapsed time
                    if (time) Print($"\n\nElapsed Time: {(stop - start)}");
                    // use return value
                    return computer.ReturnValue;
                }
            }
        }
        /// <summary>
        /// Executes a program via the graphical client. returns true if there were no errors
        /// </summary>
        /// <param name="exe">the code to execute</param>
        private static int RunGraphical(byte[] exe, string[] args, bool fsf)
        {
            // create the computer
            using (GraphicalComputer computer = new GraphicalComputer())
            {
				try
				{
					// initialize program
					computer.Initialize(exe, args);
				}
				catch (ExecutableFormatError ex)
				{
					Print(ex.Message);
					return (int)AsmLnkErrorExt.FormatError;
				}
				catch (MemoryAllocException ex)
				{
					Print(ex.Message);
					return (int)AsmLnkErrorExt.MemoryAllocError;
				}

				// set private flags
				computer.FSF = fsf;

                // this usage is just going for raw speed, so enable OTRF
                computer.OTRF = true;

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
                        return ExecErrorReturnCode;
                    }
                    // otherwise use return value
                    else return computer.ReturnValue;
                }
            }
        }
    }
}
