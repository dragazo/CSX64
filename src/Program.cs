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
@"Usage: csx [OPTION]... [ARG]...
Assemble, link, or execute CSX64 files.

  -h, --help                print this help page and exit

  -a, --assemble            assemble CSX64 asm files into CSX64 obj files
  -l, --link                link CSX64 asm/obj files into a CSX64 executable
  -s, --script              assemble, link, and execute a CSX64 asm/obj file in memory
  -S, --multiscript         as --script, but takes multiple CSX64 asm/obj files
  otherwise                 execute a CSX64 executable with provided args

  -o, --out <path>          specify an explicit output path
      --entry <entry>       main entry point for linker
      --rootdir <dir>       specify an explicit rootdir (contains _start.o and stdlib/*.o)

      --fs                  sets the file system flag during execution
  -u, --unsafe              sets all unsafe flags during execution (those in this section)

  -t, --time                after execution display elapsed time
      --end                 remaining args are not options (added to arg list)

Report bugs to: https://github.com/dragazo/CSX64/issues
";

		/// <summary>
		/// The path to the executable's directory
		/// </summary>
		private static string ExeDir => AppDomain.CurrentDomain.BaseDirectory;

		/// <summary>
		/// adds standard symbols to the assembler predefine table
		/// </summary>
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
			Assembly.DefineSymbol("err_filepermissions", (UInt64)ErrorCode.FilePermissions);

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

		// ------------------- //

		// -- executable io -- //

		// ------------------- //

		/// <summary>
		/// Saves a csx64 executable to a file (no format checking).
		/// </summary>
		/// <param name="path">the file to save to</param>
		/// <param name="exe">the csx64 executable to save</param>
		private static int SaveExecutable(string path, Executable exe)
		{
			try
			{
				exe.Save(path);
				return 0;
			}

			// things from CSX64
			catch (EmptyError) { Console.Error.WriteLine($"Attempt to save empty executable to {path}"); return (int)AsmLnkErrorExt.FormatError; }

			// things from File.OpenRead
			catch (ArgumentNullException) { Console.Error.WriteLine("Path was null"); return (int)AsmLnkErrorExt.NullPath; }
			catch (ArgumentException) { Console.Error.WriteLine($"Path \"{path}\" was invalid"); return (int)AsmLnkErrorExt.InvalidPath; }
			catch (PathTooLongException) { Console.Error.WriteLine($"Path \"{path}\" was too long"); return (int)AsmLnkErrorExt.InvalidPath; }
			catch (DirectoryNotFoundException) { Console.Error.WriteLine($"Path \"{path}\" directory was not found"); return (int)AsmLnkErrorExt.DirectoryNotFound; }
			catch (UnauthorizedAccessException) { Console.Error.WriteLine($"You do not have permission to open \"{path}\" for reading"); return (int)AsmLnkErrorExt.AccessViolation; }
			catch (FileNotFoundException) { Console.Error.WriteLine($"File \"{path}\" could not be found"); return (int)AsmLnkErrorExt.FileNotFound; }
			catch (NotSupportedException) { Console.Error.WriteLine($"Path \"{path}\" was of an unsupported format"); return (int)AsmLnkErrorExt.PathFormatUnsupported; }
			catch (IOException) { Console.Error.WriteLine($"An error occurred while reading file \"{path}\""); return (int)AsmLnkErrorExt.IOError; }

			// everything else that might happen for some reason
			catch (Exception ex) { Console.Error.WriteLine($"An error occurred while attempting to execute \"{path}\"\n-> {ex}"); return (int)AsmLnkErrorExt.UnknownError; }
		}
		/// <summary>
		/// Loads a csx64 executable from a file (no format checking).
		/// </summary>
		/// <param name="path">the file to read</param>
		/// <param name="exe">the resulting csx64 executable (on success) (non-null)</param>
		private static int LoadExecutable(string path, Executable exe)
		{
			try
			{
				exe.Load(path);
				return 0;
			}

			// things from CSX64
			catch (TypeError) { Console.Error.WriteLine($"{path} is not a CSX64 executable"); return (int)AsmLnkErrorExt.FormatError; }
			catch (VersionError) { Console.Error.WriteLine("Executable {path} is of an incompatible version of CSX64"); return (int)AsmLnkErrorExt.FormatError; }
			catch (FormatException) { Console.Error.WriteLine($"{path} is either not a CSX64 executable or is corrupted"); return (int)AsmLnkErrorExt.FormatError; }

			// things from File.OpenRead
			catch (ArgumentNullException) { Console.Error.WriteLine("Path was null"); return (int)AsmLnkErrorExt.NullPath; }
			catch (ArgumentException) { Console.Error.WriteLine($"Path \"{path}\" was invalid"); return (int)AsmLnkErrorExt.InvalidPath; }
			catch (PathTooLongException) { Console.Error.WriteLine($"Path \"{path}\" was too long"); return (int)AsmLnkErrorExt.InvalidPath; }
			catch (DirectoryNotFoundException) { Console.Error.WriteLine($"Path \"{path}\" directory was not found"); return (int)AsmLnkErrorExt.DirectoryNotFound; }
			catch (UnauthorizedAccessException) { Console.Error.WriteLine($"You do not have permission to open \"{path}\" for reading"); return (int)AsmLnkErrorExt.AccessViolation; }
			catch (FileNotFoundException) { Console.Error.WriteLine($"File \"{path}\" could not be found"); return (int)AsmLnkErrorExt.FileNotFound; }
			catch (NotSupportedException) { Console.Error.WriteLine($"Path \"{path}\" was of an unsupported format"); return (int)AsmLnkErrorExt.PathFormatUnsupported; }
			catch (IOException) { Console.Error.WriteLine($"An error occurred while reading file \"{path}\""); return (int)AsmLnkErrorExt.IOError; }

			// everything else that might happen for some reason
			catch (Exception ex) { Console.Error.WriteLine($"An error occurred while attempting to execute \"{path}\"\n-> {ex}"); return (int)AsmLnkErrorExt.UnknownError; }
		}

		// -------------------- //

		// -- object file io -- //

		// -------------------- //

		/// <summary>
		/// Saves an object file to a file.
		/// </summary>
		/// <param name="path">the destination file to save to</param>
		/// <param name="obj">the object file to save</param>
		private static int SaveObjectFile(string path, ObjectFile obj)
		{
			try
			{
				obj.Save(path);
				return 0;
			}

			// things from File.OpenRead
			catch (ArgumentNullException) { Console.Error.WriteLine("Path was null"); return (int)AsmLnkErrorExt.NullPath; }
			catch (ArgumentException) { Console.Error.WriteLine($"Path \"{path}\" was invalid"); return (int)AsmLnkErrorExt.InvalidPath; }
			catch (PathTooLongException) { Console.Error.WriteLine($"Path \"{path}\" was too long"); return (int)AsmLnkErrorExt.InvalidPath; }
			catch (DirectoryNotFoundException) { Console.Error.WriteLine($"Path \"{path}\" directory was not found"); return (int)AsmLnkErrorExt.DirectoryNotFound; }
			catch (UnauthorizedAccessException) { Console.Error.WriteLine($"You do not have permission to open \"{path}\" for reading"); return (int)AsmLnkErrorExt.AccessViolation; }
			catch (FileNotFoundException) { Console.Error.WriteLine($"File \"{path}\" could not be found"); return (int)AsmLnkErrorExt.FileNotFound; }
			catch (NotSupportedException) { Console.Error.WriteLine($"Path \"{path}\" was of an unsupported format"); return (int)AsmLnkErrorExt.PathFormatUnsupported; }
			catch (IOException) { Console.Error.WriteLine($"An error occurred while reading file \"{path}\""); return (int)AsmLnkErrorExt.IOError; }

			// everything else that might happen for some reason
			catch (Exception ex) { Console.Error.WriteLine($"An error occurred while attempting to execute \"{path}\"\n-> {ex}"); return (int)AsmLnkErrorExt.UnknownError; }
		}
		/// <summary>
		/// Loads an object file from a file.
		/// </summary>
		/// <param name="path">the source fie to read from</param>
		/// <param name="obj">the resulting object file (on success)</param>
		private static int LoadObjectFile(string path, ObjectFile obj)
		{
			try
			{
				obj.Load(path);
				return 0;
			}

			// things from CSX64
			catch (TypeError) { Console.Error.WriteLine($"{path} is not a CSX64 object file"); return (int)AsmLnkErrorExt.FormatError; }
			catch (VersionError) { Console.Error.WriteLine($"Object file {path} is of an incompatible version of CSX64"); return (int)AsmLnkErrorExt.FormatError; }
			catch (FormatException) { Console.Error.WriteLine($"Object file {path} is of an unrecognized format"); return (int)AsmLnkErrorExt.FormatError; }

			// things from File.OpenRead
			catch (ArgumentNullException) { Console.Error.WriteLine("Path was null"); return (int)AsmLnkErrorExt.NullPath; }
			catch (ArgumentException ex) { Console.Error.WriteLine($"Path \"{path}\" was invalid\n{ex}"); return (int)AsmLnkErrorExt.InvalidPath; }
			catch (PathTooLongException) { Console.Error.WriteLine($"Path \"{path}\" was too long"); return (int)AsmLnkErrorExt.InvalidPath; }
			catch (DirectoryNotFoundException) { Console.Error.WriteLine($"Path \"{path}\" directory was not found"); return (int)AsmLnkErrorExt.DirectoryNotFound; }
			catch (UnauthorizedAccessException) { Console.Error.WriteLine($"You do not have permission to open \"{path}\" for reading"); return (int)AsmLnkErrorExt.AccessViolation; }
			catch (FileNotFoundException) { Console.Error.WriteLine($"File \"{path}\" could not be found"); return (int)AsmLnkErrorExt.FileNotFound; }
			catch (NotSupportedException) { Console.Error.WriteLine($"Path \"{path}\" was of an unsupported format"); return (int)AsmLnkErrorExt.PathFormatUnsupported; }
			catch (IOException) { Console.Error.WriteLine($"An error occurred while reading file \"{path}\""); return (int)AsmLnkErrorExt.IOError; }

			// things from casting after deserialization
			catch (InvalidCastException) { Console.Error.WriteLine($"file \"{path}\" was incorrectly-formatted"); return (int)AsmLnkErrorExt.FormatError; }

			// everything else that might happen for some reason
			catch (Exception ex) { Console.Error.WriteLine($"An error occurred while attempting to execute \"{path}\"\n-> {ex}"); return (int)AsmLnkErrorExt.UnknownError; }
		}

		/// <summary>
		/// Loads the .o (obj) files from a directory and adds them to the list.
		/// </summary>
		/// <param name="objs">the resulting list of object files (new entries appended to the end)</param>
		/// <param name="path">the directory to load</param>
		private static int LoadObjectFileDir(this List<ObjectFile> objs, string path)
		{
			if (!Directory.Exists(path)) { Console.Error.WriteLine($"no directory found: {path}"); return (int)AsmLnkErrorExt.DirectoryNotFound; }

			string[] files = Directory.GetFiles(path, "*.o");
			foreach (string file in files)
			{
				ObjectFile obj = new ObjectFile();
				int ret = LoadObjectFile(file, obj);
				if (ret != 0) return ret;
				objs.Add(obj);
			}

			return 0;
		}

		// -------------- //

		// -- assembly -- //

		// -------------- //

		/// <summary>
		/// Loads the contents of a text file.
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
			catch (ArgumentNullException) { Console.Error.WriteLine("Path was null"); return (int)AsmLnkErrorExt.NullPath; }
			catch (ArgumentException) { Console.Error.WriteLine($"Path \"{path}\" was invalid\n"); return (int)AsmLnkErrorExt.InvalidPath; }
			catch (PathTooLongException) { Console.Error.WriteLine($"Path \"{path}\" was too long"); return (int)AsmLnkErrorExt.InvalidPath; }
			catch (DirectoryNotFoundException) { Console.Error.WriteLine($"Path \"{path}\" directory was not found"); return (int)AsmLnkErrorExt.DirectoryNotFound; }
			catch (UnauthorizedAccessException) { Console.Error.WriteLine($"You do not have permission to open \"{path}\" for reading"); return (int)AsmLnkErrorExt.AccessViolation; }
			catch (FileNotFoundException) { Console.Error.WriteLine($"File \"{path}\" could not be found"); return (int)AsmLnkErrorExt.FileNotFound; }
			catch (NotSupportedException) { Console.Error.WriteLine($"Path \"{path}\" was of an unsupported format"); return (int)AsmLnkErrorExt.PathFormatUnsupported; }
			catch (IOException) { Console.Error.WriteLine($"An error occurred while reading file \"{path}\""); return (int)AsmLnkErrorExt.IOError; }

			// everything else that might happen for some reason
			catch (Exception ex) { Console.Error.WriteLine($"An error occurred while attempting to execute \"{path}\"\n-> {ex}"); return (int)AsmLnkErrorExt.UnknownError; }

			// close the file
			finally { f?.Dispose(); }
		}

		/// <summary>
		/// assembles assembly source code from a file (file) into an object file (dest).
		/// </summary>
		/// <param name="file">the assembly source file</param>
		/// <param name="dest">the resulting object file (on success)</param>
		private static int Assemble(string file, ObjectFile dest)
		{
			// read the file contents
			int ret = LoadTextFile(file, out string source);
			if (ret != 0) { dest = null; return ret; }

			// assemble the source
			AssembleResult res = Assembly.Assemble(source, dest);

			// if there was an error, show error message
			if (res.Error != AssembleError.None)
			{
				Console.Error.WriteLine($"Assemble Error in {file}:\n{res.ErrorMsg}");
				return (int)res.Error;
			}

			return 0;
		}

		// ------------- //

		// -- linking -- //

		// ------------- //

		/// <summary>
		/// Loads the stdlib object file sand appends them to the end of the list.
		/// </summary>
		/// <param name="objs">the destination for storing loaded stdlib object files (appended to end)</param>
		/// <param name="rootdir">the root directory to use for core file lookup - null for default</param>
		private static int LoadStdlibObjs(List<ObjectFile> objs, string rootdir)
		{
			// get exe directory - default to provided root dir if present
			string dir = rootdir ?? ExeDir;

			// in the C++ impl that can fail, but in C# it can't - fail case omitted

			// load the _start fie
			ObjectFile _start = new ObjectFile();
			int ret = LoadObjectFile(dir + "/_start.o", _start);
			if (ret != 0) return ret;
			objs.Add(_start);

			// load the stdlib files
			return LoadObjectFileDir(objs, dir + "/stdlib");
		}

		/// <summary>
		/// Links several files to create an executable (stored to dest).
		/// </summary>
		/// <param name="dest">the resulting executable (on success) (non-null)</param>
		/// <param name="files">the files to link. ".o" files are loaded as object files, otherwise treated as assembly source and assembled</param>
		/// <param name="entry_point">the main entry point</param>
		/// <param name="rootdir">the root directory to use for core file lookup - null for default</param>
		private static int Link(Executable dest, List<string> files, string entry_point, string rootdir)
		{
			var objs = new List<ObjectFile>();

			// load the stdlib files
			int ret = LoadStdlibObjs(objs, rootdir);
			if (ret != 0) return ret;

			// load the provided files
			foreach (string file in files)
			{
				// treat ".o" as object file, otherwise as assembly source
				ObjectFile obj = new ObjectFile();
				ret = file.EndsWith(".o") ? LoadObjectFile(file, obj) : Assemble(file, obj);
				if (ret != 0) return ret;
				objs.Add(obj);
			}

			// link the resulting object files into an executable
			LinkResult res = Assembly.Link(dest, objs.ToArray(), entry_point);

			// if there was an error, show error message
			if (res.Error != LinkError.None)
			{
				Console.Error.WriteLine($"Link Error:\n{res.ErrorMsg}");
				return (int)res.Error;
			}

			return 0;
		}

		// --------------- //

		// -- execution -- //

		// --------------- //

		/// <summary>
		/// Executes a console program. Return value is either program exit code or a csx64 execution error code (delineated in stderr).
		/// </summary>
		/// <param name="exe">the client program to execute</param>
		/// <param name="args">command line args for the client program</param>
		/// <param name="fsf">value of FSF (file system flag) furing client program execution</param>
		/// <param name="time">marks if the execution time should be measured</param>
		private static int RunConsole(Executable exe, string[] args, bool fsf, bool time)
		{
			// create the computer
			using (Computer computer = new Computer())
			{
				// for this usage, remove max memory restrictions (C# limits array size to intmax)
				computer.MaxMemory = int.MaxValue;

				try
				{
					// initialize program
					computer.Initialize(exe, args);
				}
				catch (MemoryAllocException ex)
				{
					Console.Error.WriteLine(ex.Message);
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
					Console.Error.WriteLine($"\n\nError Encountered: ({computer.Error}) {ErrorCodeToString.Get(computer.Error)}");
					return ExecErrorReturnCode;
				}
				// otherwise no error
				else
				{
					if (time) Console.WriteLine($"\n\nElapsed Time: {stop - start}");
					return computer.ReturnValue;
				}
			}
		}
		/// <summary>
		/// Executes a program via the graphical client. returns true if there were no errors
		/// </summary>
		/// <param name="exe">the code to execute</param>
		private static int RunGraphical(Executable exe, string[] args, bool fsf)
		{
			// create the computer
			using (GraphicalComputer computer = new GraphicalComputer())
			{
				try
				{
					// initialize program
					computer.Initialize(exe, args);
				}
				catch (MemoryAllocException ex)
				{
					Console.Error.WriteLine(ex.Message);
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
						Console.Error.WriteLine($"\n\nError Encountered: ({computer.Error}) {ErrorCodeToString.Get(computer.Error)}");
						// return execution error code
						return ExecErrorReturnCode;
					}
					// otherwise use return value
					else return computer.ReturnValue;
				}
			}
		}

		// -------------------------- //

		// -- cmd line arg parsing -- //

		// -------------------------- //

		class cmdln_pack
		{
			public ProgramAction action = ProgramAction.ExecuteConsole; // requested action
			public List<string> pathspec = new List<string>();          // input paths
			public string entry_point = null;                           // main entry point for linker
			public string output = null;                                // output path
			public string rootdir = null;                               // root directory to use for std lookup 
			public bool fsf = false;                                    // fsf flag
			public bool time = false;                                   // time flag
			public bool accepting_options = true;                       // marks that we're still accepting options

			// these are parsing helpers - ignore
			public int i;
			public string[] args;

			/// <summary>
			/// parses the provided command line args and fills out this structure with the results.
			/// returns true on success. failure returns false after printing an error message to stderr.
			/// MUST BE CALLED ON A FRESHLY-CONSTRUCTED PACK.
			/// </summary>
			/// <param name="_args">the command line args to parse</param>
			public bool parse(string[] _args)
			{
				// set up args for the parsing pack
				args = _args;

				// for each arg (store index in this.i for handlers)
				for (i = 0; i < args.Length; ++i)
				{
					if (accepting_options)
					{
						// if we found a handler, call it
						if (long_names.TryGetValue(args[i], out cmdln_pack_handler handler)) { if (!handler(this)) return false; }
						// otherwise if it starts with a '-' it's a list of short options
						else if (args[i].StartsWith('-'))
						{
							// hold on to the current arg (some options can change i)
							string arg = args[i];

							for (int j = 1; j < arg.Length; ++j)
							{
								// if we found a handler, call it
								if (short_names.TryGetValue(arg[j], out handler)) { if (!handler(this)) return false; }
								// otherwise it's an unknown option
								else { Console.Error.WriteLine($"{arg}: Unknown option '{arg[j]}'"); return false; }
							}
						}
						// otherwise it's a pathspec
						else pathspec.Add(args[i]);
					}
					else pathspec.Add(args[i]);
				}

				return true;
			}
		}

		static bool _help(cmdln_pack p) { Console.Write(HelpMessage); return false; }

		static bool _assemble(cmdln_pack p)
		{
			if (p.action != ProgramAction.ExecuteConsole) { Console.Error.WriteLine($"{p.args[p.i]}: Already specified mode"); return false; }

			p.action = ProgramAction.Assemble;
			return true;
		}
		static bool _link(cmdln_pack p)
		{
			if (p.action != ProgramAction.ExecuteConsole) { Console.Error.WriteLine($"{p.args[p.i]}: Already specified mode"); return false; }

			p.action = ProgramAction.Link;
			return true;
		}
		static bool _script(cmdln_pack p)
		{
			if (p.action != ProgramAction.ExecuteConsole) { Console.Error.WriteLine($"{p.args[p.i]}: Already specified mode"); return false; }

			p.action = ProgramAction.ExecuteConsoleScript;
			return true;
		}
		static bool _multiscript(cmdln_pack p)
		{
			if (p.action != ProgramAction.ExecuteConsole) { Console.Error.WriteLine($"{p.args[p.i]}: Already specified mode"); return false; }

			p.action = ProgramAction.ExecuteConsoleMultiscript;
			return true;
		}

		static bool _out(cmdln_pack p)
		{
			if (p.output != null) { Console.Error.WriteLine($"{p.args[p.i]}: Already specified output path"); return false; }
			if (p.i + 1 >= p.args.Length) { Console.Error.WriteLine($"{p.args[p.i]}: Expected output path"); return false; }

			p.output = p.args[++p.i];
			return true;
		}
		static bool _entry(cmdln_pack p)
		{
			if (p.entry_point != null) { Console.Error.WriteLine($"{p.args[p.i]}: Already specified entry point"); return false; }
			if (p.i + 1 >= p.args.Length) { Console.Error.WriteLine($"{p.args[p.i]}: Expected entry point"); return false; }

			p.entry_point = p.args[++p.i];
			return true;
		}
		static bool _rootdir(cmdln_pack p)
		{
			if (p.rootdir != null) { Console.Error.WriteLine($"{p.args[p.i]}: Already specified root directory"); return false; }
			if (p.i + 1 >= p.args.Length) { Console.Error.WriteLine($"{p.args[p.i]}: Expected root directory"); return false; }

			p.rootdir = p.args[++p.i];
			return true;
		}

		static bool _fs(cmdln_pack p) { p.fsf = true; return true; }
		static bool _time(cmdln_pack p) { p.time = true; return true; }
		static bool _end(cmdln_pack p) { p.accepting_options = false; return true; }
		static bool _unsafe(cmdln_pack p) { p.fsf = true; return true; }

		private delegate bool cmdln_pack_handler(cmdln_pack p);

		// maps (long) options to their parsing handlers
		static readonly Dictionary<string, cmdln_pack_handler> long_names = new Dictionary<string, cmdln_pack_handler>()
		{
			["--help"] = _help,

			["--assemble"] = _assemble,
			["--link"] = _link,
			["--script"] = _script,
			["--multiscript"] = _multiscript,

			["--output"] = _out,
			["--entry"] = _entry,
			["--rootdir"] = _rootdir,

			["--fs"] = _fs,
			["--unsafe"] = _unsafe,

			["--time"] = _time,
			["--end"] = _end,
		};
		// maps (short) options to their parsing handlers
		static readonly Dictionary<char, cmdln_pack_handler> short_names = new Dictionary<char, cmdln_pack_handler>()
		{
			['-'] = p => true, // no-op separator

			['h'] = _help,

			['a'] = _assemble,
			['l'] = _link,
			['s'] = _script,
			['S'] = _multiscript,

			['o'] = _out,

			['u'] = _unsafe,

			['t'] = _time,
		};

		// -------------------- //

		// -- main interface -- //

		// -------------------- //

		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		[STAThread]
		private static unsafe int Main(string[] args)
		{
			try
			{
				if (!BitConverter.IsLittleEndian)
				{
					Console.Error.WriteLine(
	@"(Uhoh!! Looks like this platform isn't little-endian!
Most everything in CSX64 assumes little-endian,
so most of it won't work on this system!
)");
					return -1;
				}

				// ------------------------------------- //

				// set up initilization thingys for graphical stuff
				Application.EnableVisualStyles();
				Application.SetCompatibleTextRenderingDefault(false);

				// run statics fom client sources
				GraphicalComputer.InitStatics();

				// ------------------------------------- //

				cmdln_pack dat = new cmdln_pack();

				if (!dat.parse(args)) return 0;

				// ------------------------------------- //

				// perform the action
				switch (dat.action)
				{
					case ProgramAction.ExecuteConsole:

						{
							if (dat.pathspec.Count == 0) { Console.Error.WriteLine("Expected a file to execute"); return 0; }

							Executable exe = new Executable();
							int res = LoadExecutable(dat.pathspec[0], exe);

							return res != 0 ? res : RunConsole(exe, dat.pathspec.ToArray(), dat.fsf, dat.time);
						}

					case ProgramAction.ExecuteGraphical:

						{
							if (dat.pathspec.Count == 0) { Console.Error.WriteLine("Expected a file to execute"); return 0; }

							Executable exe = new Executable();
							int res = LoadExecutable(dat.pathspec[0], exe);

							return res != 0 ? res : RunGraphical(exe, dat.pathspec.ToArray(), dat.fsf);
						}

					case ProgramAction.ExecuteConsoleScript:

						{
							if (dat.pathspec.Count == 0) { Console.Error.WriteLine("Expected a file to assemble, link, and execute"); return 0; }

							AddPredefines();
							Executable exe = new Executable();

							int res = Link(exe, new List<string>() { dat.pathspec[0] }, dat.entry_point ?? "main", dat.rootdir);

							return res != 0 ? res : RunConsole(exe, dat.pathspec.ToArray(), dat.fsf, dat.time);
						}

					case ProgramAction.ExecuteConsoleMultiscript:

						{
							if (dat.pathspec.Count == 0) { Console.Error.WriteLine("Expected 1+ files to assemble, link, and execute"); return 0; }

							AddPredefines();
							Executable exe = new Executable();

							int res = Link(exe, dat.pathspec, dat.entry_point ?? "main", dat.rootdir);

							return res != 0 ? res : RunConsole(exe, new string[] { "<script>" }, dat.fsf, dat.time);
						}

					case ProgramAction.Assemble:

						{
							if (dat.pathspec.Count == 0) { Console.Error.WriteLine("Expected 1+ files to assemble"); return 0; }

							AddPredefines();
							ObjectFile obj = new ObjectFile();

							// if no explicit output is provided, batch process each pathspec
							if (dat.output == null)
							{
								foreach (string path in dat.pathspec)
								{
									int res = Assemble(path, obj);
									if (res != 0) return res;

									res = SaveObjectFile(Path.ChangeExtension(path, ".o"), obj);
									if (res != 0) return res;
								}
								return 0;
							}
							// otherwise, we're expecting only one input with a named output
							else
							{
								if (dat.pathspec.Count != 1) { Console.Error.WriteLine("Assembler with an explicit output expected only one input"); return 0; }

								int res = Assemble(dat.pathspec[0], obj);
								return res != 0 ? res : SaveObjectFile(dat.output, obj);
							}
						}

					case ProgramAction.Link:

						{
							if (dat.pathspec.Count == 0) { Console.Error.WriteLine("Linker expected 1+ files to link"); return 0; }

							AddPredefines();
							Executable exe = new Executable();

							int res = Link(exe, dat.pathspec, dat.entry_point ?? "main", dat.rootdir);
							return res != 0 ? res : SaveExecutable(dat.output ?? "a.out", exe);
						}
				} // end switch

				return 0;
			}
			catch (Exception ex)
			{
				Console.Error.WriteLine($"UNHANDLED EXCEPTION:\n{ex}");
				return -666;
			}
		}
	}
}
