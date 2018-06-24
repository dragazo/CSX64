using System;
using System.IO;
using System.Runtime.InteropServices;
using static CSX64.Utility;

// -- Types -- //

namespace CSX64
{
    public enum ErrorCode
    {
        None, OutOfBounds, UnhandledSyscall, UndefinedBehavior, ArithmeticError, Abort,
        IOFailure, FSDisabled, AccessViolation, InsufficientFDs, FDNotInUse, NotImplemented, StackOverflow,
        FPUStackOverflow, FPUStackUnderflow, FPUError, FPUAccessViolation,
        AlignmentViolation
    }
    public enum OPCode
    {
        // x86 instructions

        NOP,
        HLT, SYSCALL,
        PUSHF, POPF,
        FlagManip,

        SETcc, MOV, MOVcc, XCHG,

        JMP, Jcc, LOOPcc, CALL, RET,
        PUSH, POP,
        LEA,

        ADD, SUB,
        MUL, IMUL, DIV, IDIV,
        SHL, SHR, SAL, SAR, ROL, ROR, RCL, RCR,
        AND, OR, XOR,
        INC, DEC, NEG, NOT,

        CMP, CMPZ, TEST,

        BSWAP, BEXTR, BLSI, BLSMSK, BLSR, ANDN, BTx,
        Cxy, MOVxX,

        // x87 instructions

        FLD_const, FLD, FST, FXCH, FMOVcc,

        FADD, FSUB, FSUBR,
        FMUL, FDIV, FDIVR,

        F2XM1, FABS, FCHS, FPREM, FPREM1, FRNDINT, FSQRT, FYL2X, FYL2XP1, FXTRACT, FSCALE,
        FXAM, FTST, FCOM, FUCOM, FCOMI,
        FSIN, FCOS, FSINCOS, FPTAN, FPATAN,
        FINCDECSTP, FFREE,

        // SIMD instructions

        VPU_MOV,

        // misc instructions

        DEBUG = 255
    }
    public enum SyscallCode
    {
        Read, Write,
        Open, Close,
        Flush,
        Seek, Tell,

        Move, Remove,
        Mkdir, Rmdir,

        Exit,
    }

    /// <summary>
    /// Represents a 64 bit register
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    public struct CPURegister
    {
        [FieldOffset(0)] public UInt64 x64;
        public UInt32 x32 { get => (UInt32)x64; set => x64 = value; }
        [FieldOffset(0)] public UInt16 x16;
        [FieldOffset(0)] public byte x8;

        [FieldOffset(1)] public byte x8h;

        /// <summary>
        /// Gets/sets the register partition with the specified size code
        /// </summary>
        /// <param name="sizecode">the size code to select</param>
        internal UInt64 this[UInt64 sizecode]
        {
            get
            {
                switch (sizecode)
                {
                    case 3: return x64;
                    case 2: return x32;
                    case 1: return x16;
                    case 0: return x8;

                    default: throw new ArgumentOutOfRangeException("register sizecode must be on range [0,3]");
                }
            }
            set
            {
                switch (sizecode)
                {
                    case 3: x64 = value; return;
                    case 2: x32 = (UInt32)value; return;
                    case 1: x16 = (UInt16)value; return;
                    case 0: x8 = (byte)value; return;

                    default: throw new ArgumentOutOfRangeException("register sizecode must be on range [0,3]");
                }
            }
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
    /// Represents a 64-bit segment in a VPU register
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    public struct VPURegister
    {
        [FieldOffset(0)] public UInt64 int64;

        [FieldOffset(0)] public UInt32 int32_0;
        [FieldOffset(4)] public UInt32 int32_1;

        [FieldOffset(0)] public UInt16 int16_0;
        [FieldOffset(2)] public UInt16 int16_1;
        [FieldOffset(4)] public UInt16 int16_2;
        [FieldOffset(6)] public UInt16 int16_3;

        [FieldOffset(0)] public byte int8_0;
        [FieldOffset(1)] public byte int8_1;
        [FieldOffset(2)] public byte int8_2;
        [FieldOffset(3)] public byte int8_3;
        [FieldOffset(4)] public byte int8_4;
        [FieldOffset(5)] public byte int8_5;
        [FieldOffset(6)] public byte int8_6;
        [FieldOffset(7)] public byte int8_7;

        [FieldOffset(0)] public double fp64;

        [FieldOffset(0)] public float fp32_0;
        [FieldOffset(4)] public float fp32_1;

        // ------------------------------------------

        public UInt32 int32(int index)
        {
            switch (index)
            {
                case 0: return int32_0;
                case 1: return int32_1;

                default: throw new ArgumentOutOfRangeException("index out of bounds");
            }
        }
        public void int32(int index, UInt32 val)
        {
            switch (index)
            {
                case 0: int32_0 = val; return;
                case 1: int32_1 = val; return;

                default: throw new ArgumentOutOfRangeException("index out of bounds");
            }
        }

        public UInt16 int16(int index)
        {
            switch (index)
            {
                case 0: return int16_0;
                case 1: return int16_1;
                case 2: return int16_2;
                case 3: return int16_3;

                default: throw new ArgumentOutOfRangeException("index out of bounds");
            }
        }
        public void int16(int index, UInt16 val)
        {
            switch (index)
            {
                case 0: int16_0 = val; return;
                case 1: int16_1 = val; return;
                case 2: int16_2 = val; return;
                case 3: int16_3 = val; return;

                default: throw new ArgumentOutOfRangeException("index out of bounds");
            }
        }

        public byte int8(int index)
        {
            switch (index)
            {
                case 0: return int8_0;
                case 1: return int8_1;
                case 2: return int8_2;
                case 3: return int8_3;
                case 4: return int8_4;
                case 5: return int8_5;
                case 6: return int8_6;
                case 7: return int8_7;

                default: throw new ArgumentOutOfRangeException("index out of bounds");
            }
        }
        public void int8(int index, byte val)
        {
            switch (index)
            {
                case 0: int8_0 = val; return;
                case 1: int8_1 = val; return;
                case 2: int8_2 = val; return;
                case 3: int8_3 = val; return;
                case 4: int8_4 = val; return;
                case 5: int8_5 = val; return;
                case 6: int8_6 = val; return;
                case 7: int8_7 = val; return;

                default: throw new ArgumentOutOfRangeException("index out of bounds");
            }
        }

        public float fp32(int index)
        {
            switch (index)
            {
                case 0: return fp32_0;
                case 1: return fp32_1;

                default: throw new ArgumentOutOfRangeException("index out of bounds");
            }
        }
        public void fp32(int index, float val)
        {
            switch (index)
            {
                case 0: fp32_0 = val; return;
                case 1: fp32_1 = val; return;

                default: throw new ArgumentOutOfRangeException("index out of bounds");
            }
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
        /// Unlinks the stream and makes this file descriptor unused. If managed, first closes the stream.
        /// If not currenty in use, does nothing. Returns true if successful (no errors).
        /// </summary>
        public bool Close()
        {
            // closing unused file is no-op
            if (InUse)
            {
                // if the file is managed
                if (Managed)
                {
                    // close the stream
                    try { BaseStream.Close(); }
                    // fail case must still null the stream (user is free to ignore the error and reuse the object)
                    catch (Exception) { BaseStream = null; return false; }
                }

                // unlink the stream
                BaseStream = null;
            }

            return true;
        }

        /// <summary>
        /// Flushes the stream tied to this file descriptor. Throws <see cref="AccessViolationException"/> if already in use.
        /// Returns true on success (no errors).
        /// </summary>
        /// <exception cref="AccessViolationException"></exception>
        public bool Flush()
        {
            if (!InUse) throw new AccessViolationException("Attempt to flush a FileDescriptor that was not in use");

            // flush the stream
            try { BaseStream.Flush(); return true; }
            catch (Exception) { return false; }
        }

        /// <summary>
        /// Sets the current position in the file. Throws <see cref="AccessViolationException"/> if already in use.
        /// Returns true on success (no errors).
        /// </summary>
        /// <exception cref="AccessViolationException"></exception>
        public bool Seek(long offset, SeekOrigin origin)
        {
            if (!InUse) throw new AccessViolationException("Attempt to flush a FileDescriptor that was not in use");

            // seek to new position
            try { BaseStream.Seek(offset, origin); return true; }
            catch (Exception) { return false; }
        }
        /// <summary>
        /// Gets the current position in the file. Throws <see cref="AccessViolationException"/> if already in use.
        /// Returns true on success (no errors).
        /// </summary>
        /// <param name="pos">the position in the file if successful</param>
        /// <exception cref="AccessViolationException"></exception>
        public bool Tell(out long pos)
        {
            if (!InUse) throw new AccessViolationException("Attempt to flush a FileDescriptor that was not in use");
            
            // return position (the getter can throw)
            try { pos = BaseStream.Position; return true; }
            catch (Exception) { pos = -1; return false; }
        }
    }
}