using System;
using System.IO;

// -- Types -- //

namespace CSX64
{
    public enum ErrorCode
    {
        None, OutOfBounds, UnhandledSyscall, UndefinedBehavior, ArithmeticError, Abort,
        IOFailure, FSDisabled, AccessViolation, InsufficientFDs, FDNotInUse, NotImplemented, StackOverflow
    }
    public enum OPCode
    {
        NOP,
        HLT, SYSCALL,
        PUSHF, POPF,
        SETcc,

        MOV, MOVcc,
        XCHG,

        JMP, Jcc, LOOP, LOOPcc, CALL, RET,
        PUSH, POP,
        LEA,

        ADD, SUB,
        MUL, IMUL, DIV, IDIV,
        SHL, SHR, SAL, SAR, ROL, ROR,
        AND, OR, XOR,
        INC, DEC, NEG, NOT,

        CMP, FCMP, TEST, CMPZ, FCMPZ,

        FADD, FSUB, FSUBR,
        FMUL, FDIV, FDIVR,
        FPOW, FPOWR, FLOG, FLOGR,
        FSQRT, FNEG, FABS,
        FFLOOR, FCEIL, FROUND, FTRUNC,
        FSIN, FCOS, FTAN,
        FSINH, FCOSH, FTANH,
        FASIN, FACOS, FATAN, FATAN2,

        BSWAP, BEXTR, BLSI, BLSMSK, BLSR, ANDN,
        BT
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

    public enum ccOPCode
    {
        z, nz, s, ns, p, np, o, no, c, nc,
        a, ae, b, be, g, ge, l, le
    }

    /// <summary>
    /// Represents a 64 bit register
    /// </summary>
    public class Register
    {
        /// <summary>
        /// gets/sets the full qword
        /// </summary>
        public UInt64 x64 = 0;

        /// <summary>
        /// gets/sets the low dword
        /// </summary>
        public UInt32 x32
        {
            get => (UInt32)x64;
            set => x64 = x64 & ~0xfffffffful | value;
        }

        /// <summary>
        /// gets/sets the low word
        /// </summary>
        public UInt16 x16
        {
            get => (UInt16)x64;
            set => x64 = x64 & ~0xfffful | value;
        }
        
        /// <summary>
        /// gets/sets the high byte of the low word
        /// </summary>
        public byte x8h
        {
            get => (byte)(x64 >> 8);
            set => x64 = x64 & ~0xff00ul | ((UInt64)value << 8);
        }

        /// <summary>
        /// gets/sets the low byte
        /// </summary>
        public byte x8
        {
            get => (byte)x64;
            set => x64 = x64 & ~0xfful | value;
        }
    }

    /// <summary>
    /// Represents a collection of 1-bit flags used by the processor
    /// </summary>
    public class FlagsRegister
    {
        public const UInt64 CF_Mask = 0x0001;
        public const UInt64 RESERVED1_Mask = 0x0002;
        public const UInt64 PF_Mask = 0x0004;
        public const UInt64 RESERVED2_Mask = 0x0008;
        public const UInt64 AF_Mask = 0x0010;
        public const UInt64 RESERVED3_Mask = 0x0020;
        public const UInt64 ZF_Mask = 0x0040;
        public const UInt64 SF_Mask = 0x0080;
        public const UInt64 TF_Mask = 0x0100;
        public const UInt64 IF_Mask = 0x0200;
        public const UInt64 DF_Mask = 0x0400;
        public const UInt64 OF_Mask = 0x0800;

        public const UInt64 FSF_Mask = 0x000_0001_0000_0000;

        /// <summary>
        /// The full 64-bit flags data
        /// </summary>
        public UInt64 RFLAGS = 0;
        /// <summary>
        /// The lower 32 flags
        /// </summary>
        public UInt32 EFLAGS
        {
            get => (UInt32)RFLAGS;
            set => RFLAGS = (RFLAGS & ~0xffffffff) | value;
        }
        /// <summary>
        /// The lower 16 flags
        /// </summary>
        public UInt16 FLAGS
        {
            get => (UInt16)RFLAGS;
            set => RFLAGS = (RFLAGS & ~0xfffful) | value;
        }

        /// <summary>
        /// The Carry flag
        /// </summary>
        public bool CF
        {
            get => (RFLAGS & CF_Mask) != 0;
            set => RFLAGS = (RFLAGS & ~CF_Mask) | (value ? CF_Mask : 0);
        }

        /// <summary>
        /// The (even) Parity flag
        /// </summary>
        public bool PF
        {
            get => (RFLAGS & PF_Mask) != 0;
            set => RFLAGS = (RFLAGS & ~PF_Mask) | (value ? PF_Mask : 0);
        }

        /// <summary>
        /// The Adjust flag
        /// </summary>
        public bool AF
        {
            get => (RFLAGS & AF_Mask) != 0;
            set => RFLAGS = (RFLAGS & ~AF_Mask) | (value ? AF_Mask : 0);
        }

        /// <summary>
        /// The Zero flag
        /// </summary>
        public bool ZF
        {
            get => (RFLAGS & ZF_Mask) != 0;
            set => RFLAGS = (RFLAGS & ~ZF_Mask) | (value ? ZF_Mask : 0);
        }

        /// <summary>
        /// The Sign flag
        /// </summary>
        public bool SF
        {
            get => (RFLAGS & SF_Mask) != 0;
            set => RFLAGS = (RFLAGS & ~SF_Mask) | (value ? SF_Mask : 0);
        }

        /// <summary>
        /// The Trap flag (single step)
        /// </summary>
        public bool TF
        {
            get => (RFLAGS & TF_Mask) != 0;
            set => RFLAGS = (RFLAGS & ~TF_Mask) | (value ? TF_Mask : 0);
        }

        /// <summary>
        /// The Interrupt enabled flag
        /// </summary>
        public bool IF
        {
            get => (RFLAGS & IF_Mask) != 0;
            set => RFLAGS = (RFLAGS & ~IF_Mask) | (value ? IF_Mask : 0);
        }

        /// <summary>
        /// The Direction flag
        /// </summary>
        public bool DF
        {
            get => (RFLAGS & DF_Mask) != 0;
            set => RFLAGS = (RFLAGS & ~DF_Mask) | (value ? DF_Mask : 0);
        }

        /// <summary>
        /// The Overflow flag
        /// </summary>
        public bool OF
        {
            get => (RFLAGS & OF_Mask) != 0;
            set => RFLAGS = (RFLAGS & ~OF_Mask) | (value ? OF_Mask : 0);
        }

        public bool a { get => !CF && !ZF; }
        public bool ae { get => !CF; }
        public bool b { get => CF; }
        public bool be { get => CF || ZF; }

        public bool g { get => !ZF && SF == OF; }
        public bool ge { get => SF == OF; }
        public bool l { get => SF != OF; }
        public bool le { get => ZF || SF != OF; }

        /// <summary>
        /// The flag that indicates that we're allowed to run commands that may potentially modify the file system
        /// </summary>
        public bool FileSystem
        {
            get => (RFLAGS & FSF_Mask) != 0;
            set => RFLAGS = (RFLAGS & ~FSF_Mask) | (value ? FSF_Mask : 0);
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
        public bool Managed { get; private set; }
        /// <summary>
        /// Marks that the stream has an associated interactive input from external code (i.e. not client code).
        /// Reading past EOF on an interactive stream sets the SuspendedRead flag of the associated <see cref="CSX64"/>
        /// </summary>
        public bool Interactive { get; private set; }

        /// <summary>
        /// The underlying stream associated with this file descriptor.
        /// If you close this, you should also null it, or - preferably - call <see cref="Close"/> instead of closing it yourself.
        /// </summary>
        public Stream BaseStream = null;

        /// <summary>
        /// Returns true iff the file descriptor is currently in use
        /// </summary>
        public bool InUse => BaseStream != null;

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

            BaseStream = stream;
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
                // only close managed streams
                if (Managed)
                {
                    // close the stream
                    try { BaseStream.Close(); }
                    catch (Exception) { }
                    // ensure stream is nulled even if close() throws
                    finally { BaseStream = null; }
                }
                // just unlink unmanaged streams
                else BaseStream = null;
            }
        }
    }
}