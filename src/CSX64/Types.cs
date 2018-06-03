using System;
using System.IO;
using static CSX64.Utility;

// -- Types -- //

namespace CSX64
{
    public enum ErrorCode
    {
        None, OutOfBounds, UnhandledSyscall, UndefinedBehavior, ArithmeticError, Abort,
        IOFailure, FSDisabled, AccessViolation, InsufficientFDs, FDNotInUse, NotImplemented, StackOverflow,
        FPUStackOverflow, FPUStackUnderflow, FPUError, FPUAccessViolation
    }
    public enum OPCode
    {
        // x86 instructions

        NOP,
        HLT, SYSCALL,
        PUSHF, POPF,
        SETcc,

        MOV, MOVcc, XCHG,

        JMP, Jcc, LOOP, LOOPe, LOOPne, CALL, RET,
        PUSH, POP,
        LEA,

        ADD, SUB,
        MUL, IMUL, DIV, IDIV,
        SHL, SHR, SAL, SAR, ROL, ROR, RCL, RCR,
        AND, OR, XOR,
        INC, DEC, NEG, NOT,

        CMP, TEST, CMPZ,

        BSWAP, BEXTR, BLSI, BLSMSK, BLSR, ANDN, BTx,
        Cxy, CxyE, MOVxX,

        // x87 instructions

        FLD_const, FLD, FST, FXCH, FMOVcc,

        FADD, FSUB, FSUBR,
        FMUL, FDIV, FDIVR,

        F2XM1, FABS, FCHS, FPREM, FPREM1, FRNDINT, FSQRT, FYL2X, FYL2XP1, FXTRACT, FSCALE,
        FXAM, FTST, FCOM, FCOMI,
        FSIN, FCOS, FSINCOS, FPTAN, FPATAN,
        FDECSTP, FINCSTP, FFREE
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
    public struct Register
    {
        /// <summary>
        /// gets/sets the full qword
        /// </summary>
        public UInt64 x64;

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

        /// <summary>
        /// Gets/sets the register partition with the specified size code
        /// </summary>
        /// <param name="sizecode">the size code to select</param>
        internal UInt64 this[UInt64 sizecode]
        {
            get => (((1ul << (8 << (ushort)sizecode)) & ~1ul) - 1) & x64;
            set => x64 = ~(((1ul << (8 << (ushort)sizecode)) & ~1ul) - 1) & x64 | (((1ul << (8 << (ushort)sizecode)) & ~1ul) - 1) & value;
        }
    }

    /// <summary>
    /// Represents an FPU register
    /// </summary>
    public struct FPURegister
    {
        public double Value;
        public bool InUse;
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