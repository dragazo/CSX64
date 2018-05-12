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
        public const UInt64 PublicFlags = 0x1f;

        /// <summary>
        /// Indicates if rmdir will remove non-empty directories recursively
        /// </summary>
        public const bool RecursiveRmdir = false;

        /// <summary>
        /// The number of file descriptors available to the processor
        /// </summary>
        public const int NFileDescriptors = 16;

        // ----------------------------------------

        protected Register[] Registers = new Register[16];
        protected FlagsRegister Flags = new FlagsRegister();

        protected byte[] Memory = new byte[0];

        protected FileDescriptor[] FileDescriptors = new FileDescriptor[NFileDescriptors];

        protected Random Rand = new Random();

        /// <summary>
        /// The current execution positon (executed on next tick)
        /// </summary>
        public UInt64 Pos { get; protected set; }
        /// <summary>
        /// Flag marking if the program is still executing
        /// </summary>
        public bool Running { get; protected set; }
        /// <summary>
        /// Returns if we're currently suspended pending data from an interactive unmanaged input stream. Execution will resume when this is false
        /// </summary>
        public bool SuspendedRead { get; set; }
        /// <summary>
        /// The number of ticks the processor is currently sleeping for
        /// </summary>
        public UInt64 Sleep { get; protected set; }

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
        /// Gets the current time as used by the assembler
        /// </summary>
        public static UInt64 Time => DateTime.UtcNow.Ticks.MakeUnsigned();

        // ----------------------------------------

        /// <summary>
        /// Validates the machine for operation, but does not prepare it for execute (see Initialize)
        /// </summary>
        public Computer()
        {
            // allocate registers
            for (int i = 0; i < Registers.Length; ++i) Registers[i] = new Register();

            // allocate file descriptors
            for (int i = 0; i < NFileDescriptors; ++i) FileDescriptors[i] = new FileDescriptor();

            // define initial state
            Running = false;
            Error = ErrorCode.None;
        }

        /// <summary>
        /// Initializes the computer for execution
        /// </summary>
        /// <param name="data">the memory to load before starting execution (memory beyond this range is undefined)</param>
        /// <param name="args">the command line arguments to provide to the computer. pass null or empty array for none</param>
        /// <param name="stacksize">the amount of additional space to allocate for the program's stack</param>
        public void Initialize(byte[] data, string[] args, UInt64 stacksize = 2 * 1024 * 1024)
        {
            // get new memory array
            Memory = new byte[(UInt64)data.Length + stacksize];
            
            // copy over the data
            data.CopyTo(Memory, 0);

            // randomize registers
            for (int i = 0; i < Registers.Length; ++i)
            {
                Registers[i].x32 = (UInt64)Rand.Next();
                Registers[i].x64 <<= 32;
                Registers[i].x32 = (UInt64)Rand.Next();
            }
            // randomize public flags
            Flags.Flags = (Flags.Flags & ~PublicFlags) | ((UInt64)Rand.Next() & PublicFlags);

            // set execution state
            Pos = 0;
            Running = true;
            SuspendedRead = false;
            Sleep = 0;
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

            // load arg count and arg array pointer to $0 and $1 respecively
            Registers[0].x64 = args != null ? (UInt64)args.Length : 0;
            Registers[1].x64 = stack;

            // initialize stack register
            Registers[15].x64 = stack;

            // also push $0 and $1 onto the stack so it'll work with stack calling conventions
            Push(Registers[0].x64);
            Push(Registers[1].x64);
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
        public void Exit(int ret = 0)
        {
            // only do this if we're currently running (so we don't override what error caused the initial termination)
            if (Running)
            {
                // set error and stop execution
                ReturnValue = ret;
                Running = false;

                CloseFiles(); // close all the file descriptors
            }
        }

        /// <summary>
        /// Gets the specified register in this computer (no bounds checking: test index against NRegisters)
        /// </summary>
        /// <param name="index">The index of the register</param>
        public Register GetRegister(int index) => Registers[index];
        /// <summary>
        /// Returns the flags register
        /// </summary>
        public FlagsRegister GetFlags() => Flags;

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
            switch (Registers[0].x64)
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
        /// Performs a single operation. Returns true if successful
        /// </summary>
        public bool Tick()
        {
            // fail to execute ins if terminated
            if (!Running) return false;

            // if we're suspended, no-op
            if (SuspendedRead) return true;
            // if we're sleeping, no-op
            if (Sleep > 0) { --Sleep; return true; }

            UInt64 a = 0, b = 0, c = 0, d = 0; // the potential args (initialized for compiler)

            // fetch the instruction
            if (!GetMemAdv(1, out a)) return false;

            // switch through the opcodes
            switch ((OPCode)a)
            {
                case OPCode.NOP: return true;
                case OPCode.STOP: Terminate(ErrorCode.Abort); return true;
                case OPCode.SYSCALL: if (Syscall()) return true; Terminate(ErrorCode.UnhandledSyscall); return false;

                case OPCode.MOV: return ProcessMOV();

                case OPCode.MOVa: return ProcessMOV(Flags.a);
                case OPCode.MOVae: return ProcessMOV(Flags.ae);
                case OPCode.MOVb: return ProcessMOV(Flags.b);
                case OPCode.MOVbe: return ProcessMOV(Flags.be);

                case OPCode.MOVg: return ProcessMOV(Flags.g);
                case OPCode.MOVge: return ProcessMOV(Flags.ge);
                case OPCode.MOVl: return ProcessMOV(Flags.l);
                case OPCode.MOVle: return ProcessMOV(Flags.le);

                case OPCode.MOVz: return ProcessMOV(Flags.Z);
                case OPCode.MOVnz: return ProcessMOV(!Flags.Z);
                case OPCode.MOVs: return ProcessMOV(Flags.S);
                case OPCode.MOVns: return ProcessMOV(!Flags.S);
                case OPCode.MOVp: return ProcessMOV(Flags.P);
                case OPCode.MOVnp: return ProcessMOV(!Flags.P);
                case OPCode.MOVo: return ProcessMOV(Flags.O);
                case OPCode.MOVno: return ProcessMOV(!Flags.O);
                case OPCode.MOVc: return ProcessMOV(Flags.C);
                case OPCode.MOVnc: return ProcessMOV(!Flags.C);

                case OPCode.SWAP: return ProcessSWAP();

                case OPCode.ZX: if (!GetMemAdv(1, out a)) return false; Registers[a >> 4].Set(a & 3, Registers[a >> 4].Get((a >> 2) & 3)); return true;
                case OPCode.SX: if (!GetMemAdv(1, out a)) return false; Registers[a >> 4].Set(a & 3, SignExtend(Registers[a >> 4].Get((a >> 2) & 3), (a >> 2) & 3)); return true;

                case OPCode.UMUL: return ProcessUMUL();
                case OPCode.SMUL: return ProcessSMUL();
                case OPCode.UDIV: return ProcessUDIV();
                case OPCode.SDIV: return ProcessSDIV();

                case OPCode.ADD: return ProcessADD();
                case OPCode.SUB: return ProcessSUB();
                case OPCode.BMUL: return ProcessBMUL();
                case OPCode.BUDIV: return ProcessBUDIV();
                case OPCode.BUMOD: return ProcessBUMOD();
                case OPCode.BSDIV: return ProcessBSDIV();
                case OPCode.BSMOD: return ProcessBSMOD();

                case OPCode.SHL: return ProcessSL();
                case OPCode.SHR: return ProcessSR();
                case OPCode.SAL: return ProcessSAL();
                case OPCode.SAR: return ProcessSAR();
                case OPCode.ROL: return ProcessRL();
                case OPCode.ROR: return ProcessRR();

                case OPCode.AND: return ProcessAND();
                case OPCode.OR: return ProcessOR();
                case OPCode.XOR: return ProcessXOR();

                case OPCode.CMP: return ProcessSUB(false);
                case OPCode.TEST: return ProcessAND(false);

                case OPCode.INC: return ProcessINC();
                case OPCode.DEC: return ProcessDEC();
                case OPCode.NEG: return ProcessNEG();
                case OPCode.NOT: return ProcessNOT();
                case OPCode.ABS: return ProcessABS();
                case OPCode.CMPZ: return ProcessCMPZ();

                case OPCode.LEA: return ProcessLEA();

                case OPCode.JMP: return ProcessJMP(true, ref a);

                case OPCode.Ja: return ProcessJMP(Flags.a, ref a);
                case OPCode.Jae: return ProcessJMP(Flags.ae, ref a);
                case OPCode.Jb: return ProcessJMP(Flags.b, ref a);
                case OPCode.Jbe: return ProcessJMP(Flags.be, ref a);

                case OPCode.Jg: return ProcessJMP(Flags.g, ref a);
                case OPCode.Jge: return ProcessJMP(Flags.ge, ref a);
                case OPCode.Jl: return ProcessJMP(Flags.l, ref a);
                case OPCode.Jle: return ProcessJMP(Flags.le, ref a);

                case OPCode.Jz: return ProcessJMP(Flags.Z, ref a);
                case OPCode.Jnz: return ProcessJMP(!Flags.Z, ref a);
                case OPCode.Js: return ProcessJMP(Flags.S, ref a);
                case OPCode.Jns: return ProcessJMP(!Flags.S, ref a);
                case OPCode.Jp: return ProcessJMP(Flags.P, ref a);
                case OPCode.Jnp: return ProcessJMP(!Flags.P, ref a);
                case OPCode.Jo: return ProcessJMP(Flags.O, ref a);
                case OPCode.Jno: return ProcessJMP(!Flags.O, ref a);
                case OPCode.Jc: return ProcessJMP(Flags.C, ref a);
                case OPCode.Jnc: return ProcessJMP(!Flags.C, ref a);

                case OPCode.FADD: return ProcessFADD();
                case OPCode.FSUB: return ProcessFSUB();
                case OPCode.FMUL: return ProcessFMUL();
                case OPCode.FDIV: return ProcessFDIV();
                case OPCode.FMOD: return ProcessFMOD();

                case OPCode.FPOW: return ProcessFPOW();
                case OPCode.FSQRT: return ProcessFSQRT();
                case OPCode.FEXP: return ProcessFEXP();
                case OPCode.FLN: return ProcessFLN();
                case OPCode.FNEG: return ProcessFNEG();
                case OPCode.FABS: return ProcessFABS();
                case OPCode.FCMPZ: return ProcessFCMPZ();

                case OPCode.FSIN: return ProcessFSIN();
                case OPCode.FCOS: return ProcessFCOS();
                case OPCode.FTAN: return ProcessFTAN();

                case OPCode.FSINH: return ProcessFSINH();
                case OPCode.FCOSH: return ProcessFCOSH();
                case OPCode.FTANH: return ProcessFTANH();

                case OPCode.FASIN: return ProcessFASIN();
                case OPCode.FACOS: return ProcessFACOS();
                case OPCode.FATAN: return ProcessFATAN();
                case OPCode.FATAN2: return ProcessFATAN2();

                case OPCode.FLOOR: return ProcessFLOOR();
                case OPCode.CEIL: return ProcessCEIL();
                case OPCode.ROUND: return ProcessROUND();
                case OPCode.TRUNC: return ProcessTRUNC();

                case OPCode.FCMP: return ProcessFSUB(false);

                case OPCode.FTOI: return ProcessFTOI();
                case OPCode.ITOF: return ProcessITOF();

                case OPCode.PUSH: return ProcessPUSH();
                case OPCode.POP: return ProcessPOP();

                case OPCode.CALL: return ProcessJMP(true, ref a) && PushRaw(8, a);
                case OPCode.RET: if (!PopRaw(8, out a)) return false; Pos = a; return true;

                case OPCode.BSWAP: return ProcessBSWAP();
                case OPCode.BEXTR: return ProcessBEXTR();
                case OPCode.BLSI: return ProcessBLSI();
                case OPCode.BLSMSK: return ProcessBLSMSK();
                case OPCode.BLSR: return ProcessBLSR();
                case OPCode.ANDN: return ProcessANDN();

                case OPCode.GETF: return ProcessGETF();
                case OPCode.SETF: return ProcessSETF();

                case OPCode.LOOP: return ProcessJMP(--Registers[0].x64 != 0, ref a);

                case OPCode.FX: return ProcessFX();

                case OPCode.SLP: if (!FetchIMMRMFormat(out a, out b)) return false; Sleep = b; return true;

                // otherwise, unknown opcode
                default: Terminate(ErrorCode.UndefinedBehavior); return false;
            }
        }
    }
}
