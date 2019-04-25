using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static CSX64.Utility;

// -- Types -- //

namespace CSX64
{
	/// <summary>
	/// error codes resulting from executing client code (these don't trigger exceptions)
	/// </summary>
    public enum ErrorCode
    {
        None, OutOfBounds, UnhandledSyscall, UndefinedBehavior, ArithmeticError, Abort,
        IOFailure, FSDisabled, AccessViolation, InsufficientFDs, FDNotInUse, NotImplemented, StackOverflow,
        FPUStackOverflow, FPUStackUnderflow, FPUError, FPUAccessViolation,
        AlignmentViolation, UnknownOp, FilePermissions,
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

        VPU_FCMP, VPU_FCOMI,

        VPU_FSQRT, VPU_FRSQRT,

        VPU_CVT,

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

        // -- fill utilities -- //

        public void Clear() { fixed (UInt64* ptr = data) ptr[0] = ptr[1] = ptr[2] = ptr[3] = ptr[4] = ptr[5] = ptr[6] = ptr[7] = 0; }

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
    /// the interface used by CSX64 file descriptors to reference files
    /// </summary>
    public interface IFileWrapper
    {
        /// <summary>
        /// returns true iff this stream is interactive (see CSX64 documentation)
        /// </summary>
        bool IsInteractive();

        /// <summary>
        /// Returns true iff this stream can read
        /// </summary>
        bool CanRead();
        /// <summary>
        /// Returns true iff this stream can write
        /// </summary>
        bool CanWrite();

        /// <summary>
        /// returns true iff this stream can seek
        /// </summary>
        bool CanSeek();

        /// <summary>
        /// reads at most (cap) bytes info buf (beginning at start). no null terminator is appended.
        /// returns the number of bytes read.
        /// throws <see cref="FileWrapperPermissionsException"/> if the file cannot read.
        /// </summary>
        Int64 Read(byte[] buf, Int64 start, Int64 cap);
        /// <summary>
        /// writes (len) bytes from buf (beginning at start)
        /// returns the number of bytes written (may be less than len due to an io error)
        /// throws <see cref="FileWrapperPermissionsException"/> if the file cannot write.
        /// </summary>
        Int64 Write(byte[] buf, Int64 start, Int64 len);

        /// <summary>
        /// sets the current position in the file based on an offset and on origin.
        /// returns the resulting position (offset from beginning).
        /// throws <see cref="FileWrapperPermissionsException"/> if the file cannot seek.
        /// </summary>
        Int64 Seek(Int64 off, SeekOrigin orig);

        /// <summary>
        /// Closes the file and releases its resources.
        /// If the file is already closed, does nothing.
        /// It is undefined behavior to use the object after this call is made (except to close an already-closed file).
        /// </summary>
        void Close();
    }

    public class BasicFileWrapper : IFileWrapper
    {
        private Stream f;
        private bool _managed;
        private bool _interactive;

        private bool _CanRead;
        private bool _CanWrite;

        private bool _CanSeek;

        /// <summary>
        /// constructs a new BasicFileWrapper from the given file (which cannot be null).
        /// if managed is true, the stream is closed and disposed when this object is destroyed.
        /// throws <see cref="ArgumentNullException"/> if file is null.
        /// </summary>
        public BasicFileWrapper(Stream file, bool managed, bool interactive, bool canRead, bool canWrite, bool canSeek)
        {
            f = file ?? throw new ArgumentNullException("file cannot be null");

            _managed = managed;
            _interactive = interactive;
            _CanRead = canRead;
            _CanWrite = canWrite;
            _CanSeek = canSeek;
        }

        public bool IsInteractive() { return _interactive; }
        public bool IsManaged() { return _managed; }

        public bool CanRead() { return _CanRead; }
        public bool CanWrite() { return _CanWrite; }

        public bool CanSeek() { return _CanSeek; }

        public Int64 Read(byte[] buf, Int64 start, Int64 cap)
        {
            if (!CanRead()) throw new FileWrapperPermissionsException("FileWrapper not flagged for reading");
            return (Int64)f.Read(buf, (int)start, (int)cap);
        }
        public Int64 Write(byte[] buf, Int64 start, Int64 len)
        {
            if (!CanWrite()) throw new FileWrapperPermissionsException("FileWrapper not flagged for writing");
            f.Write(buf, (int)start, (int)len);
            return len;
        }

        public Int64 Seek(Int64 off, SeekOrigin orig)
        {
            if (!CanSeek()) throw new FileWrapperPermissionsException("FileWrapper not flagged for seeking");
            f.Seek(off, orig);
            return f.Position;
        }

        public void Close()
        {
            f?.Dispose();
            f = null;
        }
    }
}
