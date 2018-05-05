using System;
using System.IO;

// -- Types -- //

namespace CSX64
{
    public enum ErrorCode
    {
        None, OutOfBounds, UnhandledSyscall, UndefinedBehavior, ArithmeticError, Abort,
        IOFailure, FSDisabled, AccessViolation, InsufficientFDs, FDNotInUse
    }
    public enum OPCode
    {
        NOP, STOP, SYSCALL,

        MOV,
        MOVa, MOVae, MOVb, MOVbe, MOVg, MOVge, MOVl, MOVle,
        MOVz, MOVnz, MOVs, MOVns, MOVp, MOVnp, MOVo, MOVno, MOVc, MOVnc,

        SWAP,

        UX, SX,

        UMUL, SMUL, UDIV, SDIV,

        ADD, SUB, BMUL, BUDIV, BUMOD, BSDIV, BSMOD,
        SL, SR, SAL, SAR, RL, RR,
        AND, OR, XOR,

        CMP, TEST,

        INC, DEC, NEG, NOT, ABS, CMPZ,

        LA,

        JMP,
        Ja, Jae, Jb, Jbe, Jg, Jge, Jl, Jle,
        Jz, Jnz, Js, Jns, Jp, Jnp, Jo, Jno, Jc, Jnc,

        FADD, FSUB, FMUL, FDIV, FMOD,
        FPOW, FSQRT, FEXP, FLN, FNEG, FABS, FCMPZ,

        FSIN, FCOS, FTAN,
        FSINH, FCOSH, FTANH,
        FASIN, FACOS, FATAN, FATAN2,

        FLOOR, CEIL, ROUND, TRUNC,

        FCMP,

        FTOI, ITOF,

        PUSH, POP, CALL, RET,

        BSWAP, BEXTR, BLSI, BLSMSK, BLSR, ANDN,

        GETF, SETF,

        LOOP,

        FX,

        SLP
    }
    public enum SyscallCode
    {
        Read, Write,
        Open, Close,
        Flush,
        Seek, Tell,

        Move, Remove,
        Mkdir, Rmdir,

        Exit
    }

    /// <summary>
    /// Represents a 64 bit register
    /// </summary>
    public class Register
    {
        /// <summary>
        /// gets/sets the full 64 bits of the register
        /// </summary>
        public UInt64 x64;

        /// <summary>
        /// gets/sets the low 32 bits of the register
        /// </summary>
        public UInt64 x32
        {
            get { return x64 & 0x00000000ffffffff; }
            set { x64 = x64 & 0xffffffff00000000 | value & 0x00000000ffffffff; }
        }

        /// <summary>
        /// gets/sets the low 16 bits of the register
        /// </summary>
        public UInt64 x16
        {
            get { return x64 & 0x000000000000ffff; }
            set { x64 = x64 & 0xffffffffffff0000 | value & 0x000000000000ffff; }
        }

        /// <summary>
        /// gets/sets the low 8 bits of the register
        /// </summary>
        public UInt64 x8
        {
            get { return x64 & 0x00000000000000ff; }
            set { x64 = x64 & 0xffffffffffffff00 | value & 0x00000000000000ff; }
        }
    }

    /// <summary>
    /// Represents a collection of 1-bit flags used by the processor
    /// </summary>
    public class FlagsRegister
    {
        /// <summary>
        /// Contains the actual flag data
        /// </summary>
        public UInt64 Flags;

        /// <summary>
        /// The Zero flag
        /// </summary>
        public bool Z
        {
            get => (Flags & 0x01ul) != 0;
            set => Flags = (Flags & ~0x01ul) | (value ? 0x01ul : 0);
        }
        /// <summary>
        /// The Parity flag
        /// </summary>
        public bool P
        {
            get => (Flags & 0x02ul) != 0;
            set => Flags = (Flags & ~0x02ul) | (value ? 0x02ul : 0);
        }
        /// <summary>
        /// The Overflow flag
        /// </summary>
        public bool O
        {
            get => (Flags & 0x04ul) != 0;
            set => Flags = (Flags & ~0x04ul) | (value ? 0x04ul : 0);
        }
        /// <summary>
        /// The Carry flag
        /// </summary>
        public bool C
        {
            get => (Flags & 0x08ul) != 0;
            set => Flags = (Flags & ~0x08ul) | (value ? 0x08ul : 0);
        }
        /// <summary>
        /// The Sign flag
        /// </summary>
        public bool S
        {
            get => (Flags & 0x10ul) != 0;
            set => Flags = (Flags & ~0x10ul) | (value ? 0x10ul : 0);
        }

        public bool a { get => !C && !Z; }
        public bool ae { get => !C; }
        public bool b { get => C; }
        public bool be { get => C || Z; }

        public bool g { get => !Z && S == O; }
        public bool ge { get => S == O; }
        public bool l { get => S != O; }
        public bool le { get => Z || S != O; }

        /// <summary>
        /// The flag that indicates that memory access should be artificially slowed
        /// </summary>
        public bool SlowMemory
        {
            get => (Flags & 0x20ul) != 0;
            set => Flags = (Flags & ~0x20ul) | (value ? 0x20ul : 0);
        }
        /// <summary>
        /// The flag that indicates that we're allowed to run commands that may potentially modify the file system
        /// </summary>
        public bool FileSystem
        {
            get => (Flags & 0x40ul) != 0;
            set => Flags = (Flags & ~0x40ul) | (value ? 0x40ul : 0);
        }
    }

    /// <summary>
    /// Represents a file descriptor used by the <see cref="CSX64"/> processor
    /// </summary>
    public class FileDescriptor
    {
        /// <summary>
        /// Marks that this stream is managed by the processor.
        /// Managed files can be opened and closed by the processor, and are closed upon client program termination
        /// </summary>
        public bool Managed = false;
        /// <summary>
        /// Marks that the stream has an associated interactive input from external code (i.e. not client code).
        /// Reading past EOF on an interactive stream sets the SuspendedRead flag of the associated <see cref="CSX64"/>
        /// </summary>
        public bool Interactive = false;

        /// <summary>
        /// Returns true iff the file descriptor is currently in use
        /// </summary>
        public bool InUse => __Stream != null;

        /// <summary>
        /// The underlying stream associated with this file descriptor
        /// </summary>
        internal Stream __Stream = null;

        // ---------------------

        /// <summary>
        /// Assigns the given stream to this file descriptor. Throws <see cref="AccessViolationException"/> if already in use
        /// </summary>
        /// <param name="stream">the stream source</param>
        /// <param name="managed">marks that this stream is considered "managed". see CSX64 manual for more information</param>
        /// <param name="interactive">marks that this stream is considered "interactive" see CSX64 manual for more information</param>
        /// <exception cref="AccessViolationException"></exception>
        public void Open(Stream stream, bool managed, bool interactive)
        {
            if (InUse) throw new AccessViolationException("Attempt to assign to a FileDescriptor that was currently in use");

            __Stream = stream;
            Managed = managed;
            Interactive = interactive;
        }
        /// <summary>
        /// Unlinks the stream and makes this file descriptor unused. If managed, first closes the stream. If not currenty in use, does nothing.
        /// </summary>
        public void Close()
        {
            if (InUse)
            {
                if (Managed)
                {
                    // close the stream
                    try { __Stream.Close(); }
                    catch (Exception) { }
                    // ensure stream is nulled even if close() throws
                    finally { __Stream = null; }
                }
                else __Stream = null;
            }
        }
    }
}