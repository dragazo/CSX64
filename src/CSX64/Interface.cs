using System;
using static CSX64.Utility;

// -- Interface -- //

namespace CSX64
{
    public partial class Computer : IDisposable
    {
        /// <summary>
        /// Version number
        /// </summary>
        public const UInt64 Version = 0x0413;

        /// <summary>
        /// The bitmask representing the flags that can be modified by executing code
        /// </summary>
        public const UInt64 PublicFlags = 0xffff_ffff;

        /// <summary>
        /// Indicates if rmdir will remove non-empty directories recursively
        /// </summary>
        public const bool RecursiveRmdir = false;

        /// <summary>
        /// The number of file descriptors available to the processor
        /// </summary>
        public const int NFileDescriptors = 16;

        // ----------------------------------------

        protected byte[] Memory = new byte[0];

        protected FileDescriptor[] FileDescriptors = new FileDescriptor[NFileDescriptors];

        protected Random Rand = new Random();

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
        /// Gets the total amount of memory the processor currently has access to
        /// </summary>
        public UInt64 MemorySize => (UInt64)Memory.Length;

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

        /// <summary>
        /// Gets the current time as used by the assembler
        /// </summary>
        public static UInt64 Time => (UInt64)DateTime.UtcNow.Ticks;

        // ----------------------------------------

        /// <summary>
        /// Validates the machine for operation, but does not prepare it for execute (see Initialize)
        /// </summary>
        public Computer()
        {
            // allocate file descriptors
            for (int i = 0; i < NFileDescriptors; ++i) FileDescriptors[i] = new FileDescriptor();

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
        public bool Initialize(byte[] exe, string[] args, UInt64 stacksize = 2 * 1024 * 1024)
        {
            // read header
            if (!exe.Read(0, 8, out UInt64 text_seglen) || !exe.Read(8, 8, out UInt64 rodata_seglen)
                || !exe.Read(16, 8, out UInt64 data_seglen) || !exe.Read(24, 8, out UInt64 bss_seglen)) return false;

            // get size of memory and make sure it's doable (C# arrays use 32-bit signed integers as indexers)
            UInt64 size = (UInt64)exe.Length - 32 + bss_seglen + stacksize;
            if (size > Int32.MaxValue) return false;

            // get new memory array (does not include header)
            Memory = new byte[size];
            
            // copy over the text/rodata/data segments (not including header)
            for (int i = 32; i < exe.Length; ++i) Memory[i - 32] = exe[i];
            // zero the bss segment (should already be done by C# but this makes it more clear and explicit)
            for (int i = 0; i < (int)bss_seglen; ++i) Memory[i + exe.Length - 32] = 0;
            // randomize the heap/stack segments (C# won't let us create an uninitialized array and bit-zero is too safe - more realistic danger)
            for (int i = exe.Length - 32 + (int)bss_seglen; i < Memory.Length; ++i) Memory[i] = (byte)Rand.Next();

            // set up mmory barriers
            ExeBarrier = text_seglen;
            ReadonlyBarrier = text_seglen + rodata_seglen;
            StackBarrier = text_seglen + rodata_seglen + data_seglen + bss_seglen;

            // randomize registers
            for (int i = 0; i < Registers.Length; ++i) Registers[i].x64 = Rand.NextUInt64();
            // set public flags: make sure flag 1 is set (from Intel x86_64 standard)
            EFLAGS = 2;

            // free FPU registers
            for (int i = 0; i < FPURegisters.Length; ++i) FPURegisters[i].InUse = false;
            // set TOP
            TOP = 0;

            // set execution state
            RIP = 0;
            Running = true;
            SuspendedRead = false;
            Error = ErrorCode.None;

            // get the stack pointer
            UInt64 stack = (UInt64)Memory.Length;

            // if we have cmd line args, load them
            if (args != null && args.Length > 0)
            {
                UInt64[] pointers = new UInt64[args.Length]; // an array of pointers to args in computer memory

                // for each arg (backwards to make more sense visually, but the order doesn't actually matter)
                for (int i = args.Length - 1; i >= 0; --i)
                {
                    // push the arg onto the stack
                    stack -= (UInt64)args[i].Length + 1;
                    SetCString(stack, args[i]);

                    // record pointer to this arg
                    pointers[i] = stack;
                }

                // make room for the pointer array
                stack -= 8 * (UInt64)pointers.Length;
                // write pointer array to memory
                for (int i = 0; i < pointers.Length; ++i) SetMem(stack + (UInt64)i * 8, pointers[i]);
            }

            // load arg count and arg array pointer to RDI, RSI
            RDI = args != null ? (UInt64)args.Length : 0;
            RSI = stack;

            // initialize base/stack pointer registers
            RBP = RSP = stack;

            // also push args to stack (RTL)
            Push(RSI);
            Push(RDI);

            return true;
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
        /// Gets the file descriptor at the specified index. (no bounds checking)
        /// </summary>
        /// <param name="index">the index of the file descriptor</param>
        public FileDescriptor GetFD(int index) => FileDescriptors[index];
        /// <summary>
        /// Finds the first available file descriptor, or null if there are none available
        /// </summary>
        /// <param name="index">the index of the result</param>
        public FileDescriptor FindAvailableFD(out UInt64 index)
        {
            index = UInt64.MaxValue;

            // get the first available file descriptor
            for (int i = 0; i < FileDescriptors.Length; ++i)
                if (!FileDescriptors[i].InUse)
                {
                    index = (UInt64)i;
                    return FileDescriptors[i];
                }

            return null;
        }

        /// <summary>
        /// Closes all the managed file descriptors and severs ties to unmanaged file descriptors.
        /// </summary>
        public void CloseFiles()
        {
            // close all files
            foreach (FileDescriptor fd in FileDescriptors) fd.Close();
        }

        /// <summary>
        /// Handles syscall instructions from the processor. Returns true iff the syscall was handled successfully.
        /// Should not be called directly: only by interpreted syscall instructions
        /// </summary>
        protected virtual bool Syscall()
        {
            switch (RAX)
            {
                case (UInt64)SyscallCode.Read: return Sys_Read();
                case (UInt64)SyscallCode.Write: return Sys_Write();
                
                case (UInt64)SyscallCode.Open: return Sys_Open();
                case (UInt64)SyscallCode.Close: return Sys_Close();

                case (UInt64)SyscallCode.Flush: return Sys_Flush();

                case (UInt64)SyscallCode.Seek: return Sys_Seek();
                case (UInt64)SyscallCode.Tell: return Sys_Tell();

                case (UInt64)SyscallCode.Move: return Sys_Move();
                case (UInt64)SyscallCode.Remove: return Sys_Remove();
                case (UInt64)SyscallCode.Mkdir: return Sys_Mkdir();
                case (UInt64)SyscallCode.Rmdir: return Sys_Rmdir();

                case (UInt64)SyscallCode.Exit: Exit((int)Registers[1].x64); return true;

                // ----------------------------------

                // otherwise syscall not found
                default: return false;
            }
        }

        /// <summary>
        /// Performs a single operation. Returns true if an instruction is successfully interpreted.
        /// </summary>
        public bool Tick()
        {
            // fail if terminated or awaiting data
            if (!Running || SuspendedRead) return false;

            UInt64 op; // parsing location

            // make sure we're before the executable barrier
            if (RIP >= ExeBarrier) { Terminate(ErrorCode.AccessViolation); return false; }

            // fetch the instruction
            if (!GetMemAdv(1, out op)) return false;

            for (int i = 0; i < 8; ++i)
                Console.WriteLine($"st{i}: {(FPURegisters[(TOP + i) & 7].InUse ? FPURegisters[(TOP + i) & 7].Value.ToString() : "Not In Use")}");
            Console.WriteLine($"{RIP:x8} - {(OPCode)op}\n");

            // switch through the opcodes
            switch ((OPCode)op)
            {
                case OPCode.NOP: return true;

                case OPCode.HLT: Terminate(ErrorCode.Abort); return true;
                case OPCode.SYSCALL: if (Syscall()) return true; Terminate(ErrorCode.UnhandledSyscall); return false;

                case OPCode.PUSHF: return ProcessPUSHF();
                case OPCode.POPF: return ProcessPOPF();

                case OPCode.SETcc: return ProcessSETcc();

                case OPCode.MOV: return ProcessMOV();
                case OPCode.MOVcc: return ProcessMOVcc();

                case OPCode.XCHG: return ProcessXCHG();

                case OPCode.JMP: return ProcessJMP(ref op);
                case OPCode.Jcc: return ProcessJcc();
                case OPCode.LOOP: return ProcessLOOP(true);
                case OPCode.LOOPe: return ProcessLOOP(ZF);
                case OPCode.LOOPne: return ProcessLOOP(!ZF);

                case OPCode.CALL: return ProcessJMP(ref op) && PushRaw(8, op);
                case OPCode.RET: if (!PopRaw(8, out op)) return false; RIP = op; return true;

                case OPCode.PUSH: return ProcessPUSH();
                case OPCode.POP: return ProcessPOP();

                case OPCode.LEA: return ProcessLEA();

                case OPCode.ADD: return ProcessADD();
                case OPCode.SUB: return ProcessSUB();

                case OPCode.MUL: return ProcessMUL();
                case OPCode.IMUL: return ProcessIMUL();
                case OPCode.DIV: return ProcessDIV();
                case OPCode.IDIV: return ProcessIDIV();

                case OPCode.SHL: return ProcessSHL();
                case OPCode.SHR: return ProcessSHR();
                case OPCode.SAL: return ProcessSAL();
                case OPCode.SAR: return ProcessSAR();
                case OPCode.ROL: return ProcessROL();
                case OPCode.ROR: return ProcessROR();

                case OPCode.AND: return ProcessAND();
                case OPCode.OR: return ProcessOR();
                case OPCode.XOR: return ProcessXOR();

                case OPCode.INC: return ProcessINC();
                case OPCode.DEC: return ProcessDEC();
                case OPCode.NEG: return ProcessNEG();
                case OPCode.NOT: return ProcessNOT();

                case OPCode.CMP: return ProcessSUB(false);
                case OPCode.TEST: return ProcessAND(false);
                case OPCode.CMPZ: return ProcessCMPZ();

                case OPCode.BSWAP: return ProcessBSWAP();
                case OPCode.BEXTR: return ProcessBEXTR();
                case OPCode.BLSI: return ProcessBLSI();
                case OPCode.BLSMSK: return ProcessBLSMSK();
                case OPCode.BLSR: return ProcessBLSR();
                case OPCode.ANDN: return ProcessANDN();
                case OPCode.BT: return ProcessBT();

                case OPCode.Cxy: return ProcessCxy();
                case OPCode.MOVxX: return ProcessMOVxX();

                // x87 instructions

                case OPCode.FLD_const: return ProcessFLD_const();
                case OPCode.FLD: return ProcessFLD();
                case OPCode.FST: return ProcessFST();
                case OPCode.FXCH: return ProcessFXCH();
                case OPCode.FMOVcc: return ProcessFMOVcc();

                case OPCode.FADD: return ProcessFADD();
                case OPCode.FSUB: return ProcessFSUB();
                case OPCode.FSUBR: return ProcessFSUBR();

                case OPCode.FMUL: return ProcessFMUL();
                case OPCode.FDIV: return ProcessFDIV();
                case OPCode.FDIVR: return ProcessFDIVR();

                case OPCode.F2XM1: return ProcessF2XM1();
                case OPCode.FABS: return ProcessFABS();
                case OPCode.FCHS: return ProcessFCHS();
                case OPCode.FPREM: return ProcessFPREM();
                case OPCode.FPREM1: return ProcessFPREM1();
                case OPCode.FRNDINT: return ProcessFRNDINT();
                case OPCode.FSQRT: return ProcessFSQRT();
                case OPCode.FYL2X: return ProcessFYL2X();
                case OPCode.FYL2XP1: return ProcessFYL2XP1();
                case OPCode.FXTRACT: return ProcessFXTRACT();
                case OPCode.FSCALE: return ProcessFSCALE();

                case OPCode.FXAM: return ProcessFXAM();
                case OPCode.FTST: return ProcessFTST();
                case OPCode.FCOM: return ProcessFCOM();
                case OPCode.FCOMI: return ProcessFCOMI();

                case OPCode.FSIN: return ProcessFSIN();
                case OPCode.FCOS: return ProcessFCOS();
                case OPCode.FSINCOS: return ProcessFSINCOS();
                case OPCode.FPTAN: return ProcessFPTAN();
                case OPCode.FPATAN: return ProcessFPATAN();

                case OPCode.FDECSTP: return ProcessFDECSTP();
                case OPCode.FINCSTP: return ProcessFINCSTP();
                case OPCode.FFREE: return ProcessFFREE();

                // otherwise, unknown opcode
                default: Terminate(ErrorCode.UndefinedBehavior); return false;
            }
        }
    }
}
