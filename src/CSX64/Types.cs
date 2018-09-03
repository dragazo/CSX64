using System;
using System.IO;
using System.Runtime.CompilerServices;
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
        AlignmentViolation, UnknownOp,
    }
    public enum OPCode
    {
        // x86 instructions

        NOP,
        HLT, SYSCALL,
        STLDF,
        FlagManip,

        SETcc, MOV, MOVcc, XCHG,

        JMP, Jcc, LOOPcc, CALL, RET,
        PUSH, POP,
        LEA,

        ADD, SUB,
        MUL_x, IMUL, DIV, IDIV,
        SHL, SHR, SAL, SAR, ROL, ROR, RCL, RCR,
        AND, OR, XOR,
        INC, DEC, NEG, NOT,

        CMP, CMPZ, TEST,

        BSWAP, BEXTR, BLSI, BLSMSK, BLSR, ANDN, BTx,
        Cxy, MOVxX,
        ADXX, AAX,

        string_ops,

        BSx, TZCNT,

        UD,

        // x87 instructions

        FWAIT,
        FINIT, FCLEX,
        FSTLD_WORD,
        FLD_const, FLD, FST, FXCH, FMOVcc,

        FADD, FSUB, FSUBR,
        FMUL, FDIV, FDIVR,

        F2XM1, FABS, FCHS, FPREM, FPREM1, FRNDINT, FSQRT, FYL2X, FYL2XP1, FXTRACT, FSCALE,
        FXAM, FTST, FCOM,
        FSIN, FCOS, FSINCOS, FPTAN, FPATAN,
        FINCDECSTP, FFREE,

        // SIMD instructions

        VPU_MOV,

        VPU_FADD, VPU_FSUB, VPU_FMUL, VPU_FDIV,
        VPU_AND, VPU_OR, VPU_XOR, VPU_ANDN,
        VPU_ADD, VPU_ADDS, VPU_ADDUS,
        VPU_SUB, VPU_SUBS, VPU_SUBUS,
        VPU_MULL,

        VPU_FMIN, VPU_FMAX,
        VPU_UMIN, VPU_SMIN, VPU_UMAX, VPU_SMAX,

        VPU_FADDSUB,
        VPU_AVG,

        VPU_FCMP,

        // misc instructions

        DEBUG = 255
    }
    public enum SyscallCode
    {
        sys_exit,

        sys_read, sys_write,
        sys_open, sys_close,
        sys_lseek,

        sys_brk,

        sys_rename, sys_unlink,
        sys_mkdir, sys_rmdir,
    }
    enum OpenFlags
    {
        // access flags
        read = 1,
		write = 2,
		read_write = 3,

		// creation flags
		create = 4,
		temp = 8,
		trunc = 16,

		// status flags
		append = 32,
		binary = 64, // this one's non-standard but we need it
	}
    enum SeekMode
    {
        set, cur, end
    }

    /// <summary>
    /// Represents a 64 bit register
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    public struct CPURegister
    {
        [FieldOffset(0)] public UInt64 x64;
        public UInt32 x32 { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => (UInt32)x64; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => x64 = value; }
        [FieldOffset(0)] public UInt16 x16;
        [FieldOffset(0)] public byte x8;

        [FieldOffset(1)] public byte x8h;

        /// <summary>
        /// Gets/sets the register partition with the specified size code
        /// </summary>
        /// <param name="sizecode">the size code to select</param>
        internal UInt64 this[UInt64 sizecode]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
    /// Represents a 512-bit register used by vpu instructions
    /// </summary>
    public unsafe struct ZMMRegister
    {
        private fixed UInt64 data[8];

        // -- index access utilities -- //

        public ref UInt64 uint64(int index) { fixed (UInt64* ptr = data) return ref ((UInt64*)ptr)[index]; }
        public ref UInt32 uint32(int index) { fixed (UInt64* ptr = data) return ref ((UInt32*)ptr)[index]; }
        public ref UInt16 uint16(int index) { fixed (UInt64* ptr = data) return ref ((UInt16*)ptr)[index]; }
        public ref byte uint8(int index) { fixed (UInt64* ptr = data) return ref ((byte*)ptr)[index]; }

        public ref Int64 int64(int index) { fixed (UInt64* ptr = data) return ref ((Int64*)ptr)[index]; }
        public ref Int32 int32(int index) { fixed (UInt64* ptr = data) return ref ((Int32*)ptr)[index]; }
        public ref Int16 int16(int index) { fixed (UInt64* ptr = data) return ref ((Int16*)ptr)[index]; }
        public ref sbyte int8(int index) { fixed (UInt64* ptr = data) return ref ((sbyte*)ptr)[index]; }

        public ref double fp64(int index) { fixed (UInt64* ptr = data) return ref ((double*)ptr)[index]; }
        public ref float fp32(int index) { fixed (UInt64* ptr = data) return ref ((float*)ptr)[index]; }

        // -- sizecode access utilities -- //

        internal UInt64 _uint(UInt64 sizecode, int index)
        {
            switch (sizecode)
            {
                case 0: return uint8(index);
                case 1: return uint16(index);
                case 2: return uint32(index);
                case 3: return uint64(index);

                default: throw new ArgumentOutOfRangeException($"specified size code ({sizecode}) was out of range");
            }
        }
        internal void _uint(UInt64 sizecode, int index, UInt64 val)
        {
            switch (sizecode)
            {
                case 0: uint8(index) = (byte)val; return;
                case 1: uint16(index) = (UInt16)val; return;
                case 2: uint32(index) = (UInt32)val; return;
                case 3: uint64(index) = val; return;

                default: throw new ArgumentOutOfRangeException($"specified size code ({sizecode}) was out of range");
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
        public Stream BaseStream { get; private set; } = null;

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
    }
}
