using System;
using System.Text;
using static CSX64.Utility;

// -- Interface -- //

namespace CSX64
{
    public partial class Computer : IDisposable
    {
        /// <summary>
        /// Indicates if rmdir will remove non-empty directories recursively
        /// </summary>
        public const bool RecursiveRmdir = false;

        // ----------------------------------------

        protected byte[] Memory = { };
        protected IFileWrapper[] FileDescriptors = new IFileWrapper[16];

        protected Random Rand = new Random();

        // ----------------------------------------

        /// <summary>
        /// The maximum amount of memory the client can request
        /// </summary>
        public UInt64 MaxMemory = int.MaxValue;
        /// <summary>
        /// The minimum amount of memory the client can request.
		/// This is the address of the base of the primary execution stack.
		/// Anything at or beyond this point is regarded as heap memory.
        /// </summary>
        public UInt64 MinMemory { get; private set; }

		/// <summary>
		/// Gets the amount of memory (in bytes) the computer currently has access to
		/// </summary>
		public UInt64 MemorySize => (UInt64)Memory.Length;

		/// <summary>
		/// Gets the maximum number of file descriptors
		/// </summary>
		public int FDCount => FileDescriptors.Length;

        /// <summary>
        /// Flag marking if the program is still executing (still true even in halted state)
        /// </summary>
        public bool Running { get; protected set; }
        /// <summary>
        /// Gets if the processor is awaiting data from an interactive stream
        /// </summary>
        public bool SuspendedRead { get; protected set; }
        /// <summary>
        /// Gets the current error code
        /// </summary>
        public ErrorCode Error { get; protected set; }
        /// <summary>
        /// The return value from the program after errorless termination
        /// </summary>
        public int ReturnValue { get; protected set; }

        /// <summary>
        /// The barrier before which memory is executable
        /// </summary>
        public UInt64 ExeBarrier { get; protected set; }
        /// <summary>
        /// The barrier before which memory is read-only
        /// </summary>
        public UInt64 ReadonlyBarrier { get; protected set; }
        /// <summary>
        /// Gets the barrier before which the stack can't enter
        /// </summary>
        public UInt64 StackBarrier { get; protected set; }

        // ----------------------------------------

        /// <summary>
        /// Validates the machine for operation, but does not prepare it for execute (see Initialize)
        /// </summary>
        public Computer()
        {
            // allocate file descriptors
            for (int i = 0; i < FileDescriptors.Length; ++i) FileDescriptors[i] = null;

            // define initial state
            Running = false;
            Error = ErrorCode.None;
        }

        /// <summary>
        /// Initializes the computer for execution
        /// </summary>
        /// <param name="exe">the memory to load before starting execution (memory beyond this range is undefined)</param>
        /// <param name="args">the command line arguments to provide to the computer. pass null or empty array for none</param>
        /// <param name="stacksize">the amount of additional space to allocate for the program's stack</param>
        public void Initialize(Executable exe, string[] args, UInt64 stacksize = 2 * 1024 * 1024)
        {
            // get size of memory we need to allocate
            UInt64 size = exe.TotalSize + stacksize;

			// make sure we catch overflow from adding stacksize
			if (size < stacksize) throw new OverflowException("memory size overflow uint64");
			// make sure it's within max  memory usage limits
			if (size > MaxMemory) throw new MemoryAllocException("executable size exceeded max memory");

            // get new memory array (does not include header)
            Memory = new byte[size];
			// mark the minimum amount of memory (minimum sys_brk value) (so user can't truncate program data/stack/etc.)
			MinMemory = size;

			// copy the executable content into our memory array
			exe.Content.CopyTo(Memory, 0);
            // zero the bss segment (should already be done by C# but this makes it more clear and explicit)
            for (int i = 0; i < (int)exe.bss_seglen; ++i) Memory[exe.Content.Length + i] = 0;
            // randomize the heap/stack segments (C# won't let us create an uninitialized array and bit-zero is too safe - more realistic danger)
            for (int i = (int)exe.TotalSize; i < Memory.Length; ++i) Memory[i] = (byte)Rand.Next();

            // set up memory barriers
            ExeBarrier = exe.text_seglen;
            ReadonlyBarrier = exe.text_seglen + exe.rodata_seglen;
            StackBarrier = exe.text_seglen + exe.rodata_seglen + exe.data_seglen + exe.bss_seglen;

            // set up cpu registers
            for (int i = 0; i < CPURegisters.Length; ++i) CPURegisters[i].x64 = Rand.NextUInt64();

            // set up fpu registers
            FINIT();

            // set up vpu registers
            for (int i = 0; i < ZMMRegisters.Length; ++i)
                for (int j = 0; j < 8; ++j) ZMMRegisters[i].uint64(j) = Rand.NextUInt64();
            _MXCSR = 0x1f80;

            // set execution state
            RIP = 0;
            RFLAGS = 2; // x86 standard dictates this initial state
            Running = true;
            SuspendedRead = false;
            Error = ErrorCode.None;

            // get the stack pointer
            UInt64 stack = (UInt64)Memory.Length;
            RBP = stack; // RBP points to before we start pushing args

			// if we have args, handle those
			if (args != null)
			{
				// an array of pointers to command line args in computer memory.
				// one for each arg, plus a null terminator.
				UInt64[] cmdarg_pointers = new UInt64[args.Length + 1];

				// put each arg on the stack and get their addresses
				for (int i = 0; i < args.Length; ++i)
				{
					// push the arg onto the stack
					stack -= (UInt64)args[i].Length + 1;
					SetCString(stack, args[i]);

					// record pointer to this arg
					cmdarg_pointers[i] = stack;
				}
				// the last pointer is null (C guarantees this, so we will as well)
				cmdarg_pointers[args.Length] = 0;

				// make room for the pointer array
				stack -= 8 * (UInt64)cmdarg_pointers.Length;
				// write pointer array to memory
				for (int i = 0; i < cmdarg_pointers.Length; ++i) SetMem(stack + (UInt64)i * 8, cmdarg_pointers[i]);
			}
			// otherwise we have no cmd line args
			else
			{
				// this case is as above, but where args is empty, so it's extremely trivial
				stack -= 8;
				SetMem(stack, 0);
			}

            // load arg count and arg array pointer to RDI, RSI
            RDI = args != null ? (UInt64)args.Length : 0;
            RSI = stack;

            // initialize RSP
            RSP = stack;

            // also push args to stack (RTL)
            Push(RSI);
            Push(RDI);
        }

        /// <summary>
        /// Causes the machine to end execution with an error code and release various system resources (e.g. file handles).
        /// </summary>
        /// <param name="err">the error code to emit</param>
        public void Terminate(ErrorCode err = ErrorCode.None)
        {
            // only do this if we're currently running (so we don't override what error caused the initial termination)
            if (Running)
            {
                // set error and stop execution
                Error = err;
                Running = false;

                CloseFiles(); // close all the file descriptors
            }
        }
        /// <summary>
        /// Causes the machine to end execution with a return value and release various system resources (e.g. file handles).
        /// </summary>
        /// <param name="ret">the program return value to emit</param>
        protected void Exit(int ret = 0)
        {
            // only do this if we're currently running
            if (Running)
            {
                // set return value and stop execution
                ReturnValue = ret;
                Running = false;

                CloseFiles(); // close all the file descriptors
            }
        }

        /// <summary>
        /// Unsets the suspended read state
        /// </summary>
        public void ResumeSuspendedRead()
        {
            if (Running) SuspendedRead = false;
        }

        /// <summary>
        /// Links the provided file to the first available file descriptor.
        /// returns the file descriptor that was used. if none were available, does not link the file and returns -1.
        /// </summary>
        public int OpenFileWrapper(IFileWrapper f)
        {
            int fd = FindAvailableFD();
            if (fd >= 0) FileDescriptors[fd] = f;
            return fd;
        }
        /// <summary>
        /// Links the provided file descriptor to the file (no bounds checking - see FDCount).
        /// If the file descriptor is already in use, it is first closed.
        /// </summary>
        public void OpenFileWrapper(int fd, IFileWrapper f)
        {
            FileDescriptors[fd]?.Close();
            FileDescriptors[fd] = f;
        }
        
        /// <summary>
        /// Closes the file wrapper with specified file descriptor. (no bounds checking)
        /// If the file descriptor was not in use, does nothing.
        /// </summary>
        public void CloseFileWrapper(int fd)
        {
            FileDescriptors[fd]?.Close();
            FileDescriptors[fd] = null;
        }
        /// <summary>
        /// Closes all the managed file descriptors and severs ties to unmanaged file descriptors.
        /// </summary>
        public void CloseFiles()
        {
            for (int i = 0; i < FileDescriptors.Length; ++i)
            {
                FileDescriptors[i]?.Close();
                FileDescriptors[i] = null;
            }
        }

        /// <summary>
        /// Gets the file wrapper with specified file descriptor (no bounds checking).
        /// if this is null, the fd is not in use.
        /// </summary>
        public IFileWrapper GetFileWrapper(int fd) => FileDescriptors[fd];
        
        /// <summary>
        /// Finds the first available file descriptor. reutrns -1 if there are none available
        /// </summary>
        public int FindAvailableFD()
        {
            for (int i = 0; i < FileDescriptors.Length; ++i)
                if (FileDescriptors[i] == null) return i;

            return -1;
        }

        /// <summary>
        /// Handles syscall instructions from the processor. Returns true iff the syscall was handled successfully.
        /// Should not be called directly: only by interpreted syscall instructions
        /// </summary>
        protected virtual bool Syscall()
        {
            switch (RAX)
            {
                case (UInt64)SyscallCode.sys_exit: Exit((int)RBX); return true;

                case (UInt64)SyscallCode.sys_read: return Sys_Read();
                case (UInt64)SyscallCode.sys_write: return Sys_Write();
                case (UInt64)SyscallCode.sys_open: return Sys_Open();
                case (UInt64)SyscallCode.sys_close: return Sys_Close();
                case (UInt64)SyscallCode.sys_lseek: return Sys_Lseek();

                case (UInt64)SyscallCode.sys_brk: return Sys_Brk();

                case (UInt64)SyscallCode.sys_rename: return Sys_Rename();
                case (UInt64)SyscallCode.sys_unlink: return Sys_Unlink();
                case (UInt64)SyscallCode.sys_mkdir: return Sys_Mkdir();
                case (UInt64)SyscallCode.sys_rmdir: return Sys_Rmdir();

                // ----------------------------------

                // otherwise syscall not found
                default: Terminate(ErrorCode.UnhandledSyscall); return false;
            }
        }

        /// <summary>
        /// Performs a single operation. Returns the number of successful operations.
        /// Returning a lower number than requested (even zero) does not necessarily indicate termination or error.
        /// To check for termination/error, use <see cref="Running"/>.
        /// </summary>
        /// <param name="count">The maximum number of operations to perform</param>
        public UInt64 Tick(UInt64 count)
        {
            UInt64 ticks, op;
            for (ticks = 0; ticks < count; ++ticks)
            {
                // fail if terminated or awaiting data
                if (!Running || SuspendedRead) break;

                // make sure we're before the executable barrier
                if (RIP >= ExeBarrier) { Terminate(ErrorCode.AccessViolation); break; }

                // fetch the instruction
                if (!GetMemAdv(1, out op)) break;

                //Console.WriteLine($"{RIP:x8} - {(OPCode)op}");

                // switch through the opcodes
                switch ((OPCode)op)
                {
                    case OPCode.NOP: break;

                    case OPCode.HLT: Terminate(ErrorCode.Abort); break;
                    case OPCode.SYSCALL: Syscall(); break;

                    case OPCode.STLDF: ProcessSTLDF(); break;

                    case OPCode.FlagManip: ProcessFlagManip(); break;

                    case OPCode.SETcc: ProcessSETcc(); break;

                    case OPCode.MOV: ProcessMOV(); break;
                    case OPCode.MOVcc: ProcessMOVcc(); break;

                    case OPCode.XCHG: ProcessXCHG(); break;

                    case OPCode.JMP: ProcessJMP(ref op); break;
                    case OPCode.Jcc: ProcessJcc(); break;
                    case OPCode.LOOPcc: ProcessLOOPcc(); break;

                    case OPCode.CALL: if (!ProcessJMP(ref op)) break; PushRaw(8, op); break;
                    case OPCode.RET: if (!PopRaw(8, out op)) break; RIP = op; break;

                    case OPCode.PUSH: ProcessPUSH(); break;
                    case OPCode.POP: ProcessPOP(); break;

                    case OPCode.LEA: ProcessLEA(); break;

                    case OPCode.ADD: ProcessADD(); break;
                    case OPCode.SUB: ProcessSUB(); break;

                    case OPCode.MUL_x: ProcessMUL_x(); break;
                    case OPCode.IMUL: ProcessIMUL(); break;
                    case OPCode.DIV: ProcessDIV(); break;
                    case OPCode.IDIV: ProcessIDIV(); break;

                    case OPCode.SHL: ProcessSHL(); break;
                    case OPCode.SHR: ProcessSHR(); break;
                    case OPCode.SAL: ProcessSAL(); break;
                    case OPCode.SAR: ProcessSAR(); break;
                    case OPCode.ROL: ProcessROL(); break;
                    case OPCode.ROR: ProcessROR(); break;
                    case OPCode.RCL: ProcessRCL(); break;
                    case OPCode.RCR: ProcessRCR(); break;

                    case OPCode.AND: ProcessAND(); break;
                    case OPCode.OR: ProcessOR(); break;
                    case OPCode.XOR: ProcessXOR(); break;

                    case OPCode.INC: ProcessINC(); break;
                    case OPCode.DEC: ProcessDEC(); break;
                    case OPCode.NEG: ProcessNEG(); break;
                    case OPCode.NOT: ProcessNOT(); break;

                    case OPCode.CMP: ProcessSUB(false); break;
                    case OPCode.CMPZ: ProcessCMPZ(); break;
                    case OPCode.TEST: ProcessAND(false); break;

                    case OPCode.BSWAP: ProcessBSWAP(); break;
                    case OPCode.BEXTR: ProcessBEXTR(); break;
                    case OPCode.BLSI: ProcessBLSI(); break;
                    case OPCode.BLSMSK: ProcessBLSMSK(); break;
                    case OPCode.BLSR: ProcessBLSR(); break;
                    case OPCode.ANDN: ProcessANDN(); break;
                    case OPCode.BTx: ProcessBTx(); break;

                    case OPCode.Cxy: ProcessCxy(); break;
                    case OPCode.MOVxX: ProcessMOVxX(); break;

                    case OPCode.ADXX: ProcessADXX(); break;
                    case OPCode.AAX: ProcessAAX(); break;

                    case OPCode.string_ops: ProcessSTRING(); break;

                    case OPCode.BSx: ProcessBSx(); break;
                    case OPCode.TZCNT: ProcessTZCNT(); break;

                    case OPCode.UD: ProcessUD(); break;

                    // x87 instructions

                    case OPCode.FWAIT: break; // thus far fpu ops are synchronous with cpu ops

                    case OPCode.FINIT: FINIT(); break;
                    case OPCode.FCLEX: FPU_status &= 0xff00; break;

                    case OPCode.FSTLD_WORD: ProcessFSTLD_WORD(); break;

                    case OPCode.FLD_const: ProcessFLD_const(); break;
                    case OPCode.FLD: ProcessFLD(); break;
                    case OPCode.FST: ProcessFST(); break;
                    case OPCode.FXCH: ProcessFXCH(); break;
                    case OPCode.FMOVcc: ProcessFMOVcc(); break;

                    case OPCode.FADD: ProcessFADD(); break;
                    case OPCode.FSUB: ProcessFSUB(); break;
                    case OPCode.FSUBR: ProcessFSUBR(); break;

                    case OPCode.FMUL: ProcessFMUL(); break;
                    case OPCode.FDIV: ProcessFDIV(); break;
                    case OPCode.FDIVR: ProcessFDIVR(); break;

                    case OPCode.F2XM1: ProcessF2XM1(); break;
                    case OPCode.FABS: ProcessFABS(); break;
                    case OPCode.FCHS: ProcessFCHS(); break;
                    case OPCode.FPREM: ProcessFPREM(); break;
                    case OPCode.FPREM1: ProcessFPREM1(); break;
                    case OPCode.FRNDINT: ProcessFRNDINT(); break;
                    case OPCode.FSQRT: ProcessFSQRT(); break;
                    case OPCode.FYL2X: ProcessFYL2X(); break;
                    case OPCode.FYL2XP1: ProcessFYL2XP1(); break;
                    case OPCode.FXTRACT: ProcessFXTRACT(); break;
                    case OPCode.FSCALE: ProcessFSCALE(); break;

                    case OPCode.FXAM: ProcessFXAM(); break;
                    case OPCode.FTST: ProcessFTST(); break;
                    case OPCode.FCOM: ProcessFCOM(); break;

                    case OPCode.FSIN: ProcessFSIN(); break;
                    case OPCode.FCOS: ProcessFCOS(); break;
                    case OPCode.FSINCOS: ProcessFSINCOS(); break;
                    case OPCode.FPTAN: ProcessFPTAN(); break;
                    case OPCode.FPATAN: ProcessFPATAN(); break;

                    case OPCode.FINCDECSTP: ProcessFINCDECSTP(); break;
                    case OPCode.FFREE: ProcessFFREE(); break;

                    // vpu instructions

                    case OPCode.VPU_MOV: ProcessVPUMove(); break;

                    case OPCode.VPU_FADD: TryProcessVEC_FADD(); break;
                    case OPCode.VPU_FSUB: TryProcessVEC_FSUB(); break;
                    case OPCode.VPU_FMUL: TryProcessVEC_FMUL(); break;
                    case OPCode.VPU_FDIV: TryProcessVEC_FDIV(); break;

                    case OPCode.VPU_AND: TryProcessVEC_AND(); break;
                    case OPCode.VPU_OR: TryProcessVEC_OR(); break;
                    case OPCode.VPU_XOR: TryProcessVEC_XOR(); break;
                    case OPCode.VPU_ANDN: TryProcessVEC_ANDN(); break;

                    case OPCode.VPU_ADD: TryProcessVEC_ADD(); break;
                    case OPCode.VPU_ADDS: TryProcessVEC_ADDS(); break;
                    case OPCode.VPU_ADDUS: TryProcessVEC_ADDUS(); break;

                    case OPCode.VPU_SUB: TryProcessVEC_SUB(); break;
                    case OPCode.VPU_SUBS: TryProcessVEC_SUBS(); break;
                    case OPCode.VPU_SUBUS: TryProcessVEC_SUBUS(); break;

                    case OPCode.VPU_MULL: TryProcessVEC_MULL(); break;

                    case OPCode.VPU_FMIN: TryProcessVEC_FMIN(); break;
                    case OPCode.VPU_FMAX: TryProcessVEC_FMAX(); break;

                    case OPCode.VPU_UMIN: TryProcessVEC_UMIN(); break;
                    case OPCode.VPU_SMIN: TryProcessVEC_SMIN(); break;
                    case OPCode.VPU_UMAX: TryProcessVEC_UMAX(); break;
                    case OPCode.VPU_SMAX: TryProcessVEC_SMAX(); break;

                    case OPCode.VPU_FADDSUB: TryProcessVEC_FADDSUB(); break;
                    case OPCode.VPU_AVG: TryProcessVEC_AVG(); break;

                    case OPCode.VPU_FCMP: TryProcessVEC_FCMP(); break;
                    case OPCode.VPU_FCOMI: TryProcessVEC_FCOMI(); break;

                    case OPCode.VPU_FSQRT: TryProcessVEC_FSQRT(); break;
                    case OPCode.VPU_FRSQRT: TryProcessVEC_FRSQRT(); break;

                    case OPCode.VPU_CVT: TryProcessVEC_CVT(); break;

                    // misc instructions

                    case OPCode.DEBUG: ProcessDEBUG(); break;

                    // otherwise, unknown opcode
                    default: Terminate(ErrorCode.UnknownOp); break;
                }
            }

            return ticks;
        }

        /// <summary>
        /// Creates a string containing all non-vpu register/flag states"/>
        /// </summary>
        public string GetCPUDebugString()
        {
            return CreateTable(new int[] { 26, 10, 0 }, new string[][]
               {
                new string[] { $"RAX: {RAX:x16}", $"CF: {(CF ? 1 : 0)}", $"RFLAGS: {RFLAGS:x16}" },
                new string[] { $"RBX: {RBX:x16}", $"PF: {(PF ? 1 : 0)}", $"RIP:    {RIP:x16}" },
                new string[] { $"RCX: {RCX:x16}", $"AF: {(AF ? 1 : 0)}" },
                new string[] { $"RDX: {RDX:x16}", $"ZF: {(ZF ? 1 : 0)}", $"ST0: {(ST_Tag(0) != FPU_Tag_empty ? ST0.ToString() : "Empty")}" },
                new string[] { $"RSI: {RSI:x16}", $"SF: {(SF ? 1 : 0)}",$"ST1: {(ST_Tag(1) != FPU_Tag_empty ? ST1.ToString() : "Empty")}" },
                new string[] { $"RDI: {RDI:x16}", $"OF: {(OF ? 1 : 0)}", $"ST2: {(ST_Tag(2) != FPU_Tag_empty ? ST2.ToString() : "Empty")}" },
                new string[] { $"RBP: {RBP:x16}", null, $"ST3: {(ST_Tag(3) != FPU_Tag_empty ? ST3.ToString() : "Empty")}" },
                new string[] { $"RSP: {RSP:x16}", $"b:  {(cc_b ? 1 : 0)}",$"ST4: {(ST_Tag(4) != FPU_Tag_empty ? ST4.ToString() : "Empty")}" },
                new string[] { $"R8:  {R8:x16}", $"be: {(cc_be ? 1 : 0)}", $"ST5: {(ST_Tag(5) != FPU_Tag_empty ? ST5.ToString() : "Empty")}" },
                new string[] { $"R9:  {R9:x16}", $"a:  {(cc_a ? 1 : 0)}",$"ST6: {(ST_Tag(6) != FPU_Tag_empty ? ST6.ToString() : "Empty")}" },
                new string[] { $"R10: {R10:x16}", $"ae: {(cc_ae ? 1 : 0)}",$"ST7: {(ST_Tag(7) != FPU_Tag_empty ? ST7.ToString() : "Empty")}" },
                new string[] { $"R11: {R11:x16}" },
                new string[] { $"R12: {R12:x16}", $"l:  {(cc_l ? 1 : 0)}",$"C0: {(FPU_C0 ? 1 : 0)}" },
                new string[] { $"R13: {R13:x16}", $"le: {(cc_le ? 1 : 0)}", $"C1: {(FPU_C1 ? 1 : 0)}" },
                new string[] { $"R14: {R14:x16}", $"g:  {(cc_g ? 1 : 0)}",$"C2: {(FPU_C2 ? 1 : 0)}" },
                new string[] { $"R15: {R15:x16}", $"ge: {(cc_ge ? 1 : 0)}", $"C3: {(FPU_C3 ? 1 : 0)}" },
               });
        }
        /// <summary>
        /// Creates a string containing all vpu register states
        /// </summary>
        public string GetVPUDebugString()
        {
            StringBuilder b = new StringBuilder();

            for (int i = 0; i < ZMMRegisters.Length; ++i)
            {
                b.Append($"ZMM{i}: ");
                if (i < 10) b.Append(' ');

                for (int j = 7; j >= 0; --j) b.Append($"{ZMMRegisters[i].int64(j):x16} ");

                if (i != ZMMRegisters.Length - 1) b.Append('\n');
            }

            return b.ToString();
        }
    }
}
