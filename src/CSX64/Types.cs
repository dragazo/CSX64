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
        ADC_x, AAA,

        // x87 instructions

        FSTLD_WORD,
        FLD_const, FLD, FST, FXCH, FMOVcc,

        FADD, FSUB, FSUBR,
        FMUL, FDIV, FDIVR,

        F2XM1, FABS, FCHS, FPREM, FPREM1, FRNDINT, FSQRT, FYL2X, FYL2XP1, FXTRACT, FSCALE,
        FXAM, FTST, FCOM, FUCOM, FCOMI,
        FSIN, FCOS, FSINCOS, FPTAN, FPATAN,
        FINCDECSTP, FFREE,

        // SIMD instructions

        VPU_MOV,

        VPU_FADD, VPU_FSUB, VPU_FMUL, VPU_FDIV,
        VPU_AND, VPU_OR, VPU_XOR, VPU_ANDN,
        VPU_ADD, VPU_ADDS, VPU_ADDUS,
        VPU_SUB, VPU_SUBS, VPU_SUBUS,
        VPU_MUL,

        VPU_FMIN, VPU_FMAX,
        VPU_UMIN, VPU_SMIN, VPU_UMAX, VPU_SMAX,
        
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

        Brk,
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
    /// Represents a 512-bit register used by vpu instructions
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    public struct ZMMRegister
    {
        [FieldOffset(0)] public UInt64 uint64_0;
        [FieldOffset(8)] public UInt64 uint64_1;
        [FieldOffset(16)] public UInt64 uint64_2;
        [FieldOffset(24)] public UInt64 uint64_3;
        [FieldOffset(32)] public UInt64 uint64_4;
        [FieldOffset(40)] public UInt64 uint64_5;
        [FieldOffset(48)] public UInt64 uint64_6;
        [FieldOffset(56)] public UInt64 uint64_7;

        [FieldOffset(0)] public UInt32 uint32_0;
        [FieldOffset(4)] public UInt32 uint32_1;
        [FieldOffset(8)] public UInt32 uint32_2;
        [FieldOffset(12)] public UInt32 uint32_3;
        [FieldOffset(16)] public UInt32 uint32_4;
        [FieldOffset(20)] public UInt32 uint32_5;
        [FieldOffset(24)] public UInt32 uint32_6;
        [FieldOffset(28)] public UInt32 uint32_7;
        [FieldOffset(32)] public UInt32 uint32_8;
        [FieldOffset(36)] public UInt32 uint32_9;
        [FieldOffset(40)] public UInt32 uint32_10;
        [FieldOffset(44)] public UInt32 uint32_11;
        [FieldOffset(48)] public UInt32 uint32_12;
        [FieldOffset(52)] public UInt32 uint32_13;
        [FieldOffset(56)] public UInt32 uint32_14;
        [FieldOffset(60)] public UInt32 uint32_15;

        [FieldOffset(0)] public UInt16 uint16_0;
        [FieldOffset(2)] public UInt16 uint16_1;
        [FieldOffset(4)] public UInt16 uint16_2;
        [FieldOffset(6)] public UInt16 uint16_3;
        [FieldOffset(8)] public UInt16 uint16_4;
        [FieldOffset(10)] public UInt16 uint16_5;
        [FieldOffset(12)] public UInt16 uint16_6;
        [FieldOffset(14)] public UInt16 uint16_7;
        [FieldOffset(16)] public UInt16 uint16_8;
        [FieldOffset(18)] public UInt16 uint16_9;
        [FieldOffset(20)] public UInt16 uint16_10;
        [FieldOffset(22)] public UInt16 uint16_11;
        [FieldOffset(24)] public UInt16 uint16_12;
        [FieldOffset(26)] public UInt16 uint16_13;
        [FieldOffset(28)] public UInt16 uint16_14;
        [FieldOffset(30)] public UInt16 uint16_15;
        [FieldOffset(32)] public UInt16 uint16_16;
        [FieldOffset(34)] public UInt16 uint16_17;
        [FieldOffset(36)] public UInt16 uint16_18;
        [FieldOffset(38)] public UInt16 uint16_19;
        [FieldOffset(40)] public UInt16 uint16_20;
        [FieldOffset(42)] public UInt16 uint16_21;
        [FieldOffset(44)] public UInt16 uint16_22;
        [FieldOffset(46)] public UInt16 uint16_23;
        [FieldOffset(48)] public UInt16 uint16_24;
        [FieldOffset(50)] public UInt16 uint16_25;
        [FieldOffset(52)] public UInt16 uint16_26;
        [FieldOffset(54)] public UInt16 uint16_27;
        [FieldOffset(56)] public UInt16 uint16_28;
        [FieldOffset(58)] public UInt16 uint16_29;
        [FieldOffset(60)] public UInt16 uint16_30;
        [FieldOffset(62)] public UInt16 uint16_31;

        [FieldOffset(0)] public byte uint8_0;
        [FieldOffset(1)] public byte uint8_1;
        [FieldOffset(2)] public byte uint8_2;
        [FieldOffset(3)] public byte uint8_3;
        [FieldOffset(4)] public byte uint8_4;
        [FieldOffset(5)] public byte uint8_5;
        [FieldOffset(6)] public byte uint8_6;
        [FieldOffset(7)] public byte uint8_7;
        [FieldOffset(8)] public byte uint8_8;
        [FieldOffset(9)] public byte uint8_9;
        [FieldOffset(10)] public byte uint8_10;
        [FieldOffset(11)] public byte uint8_11;
        [FieldOffset(12)] public byte uint8_12;
        [FieldOffset(13)] public byte uint8_13;
        [FieldOffset(14)] public byte uint8_14;
        [FieldOffset(15)] public byte uint8_15;
        [FieldOffset(16)] public byte uint8_16;
        [FieldOffset(17)] public byte uint8_17;
        [FieldOffset(18)] public byte uint8_18;
        [FieldOffset(19)] public byte uint8_19;
        [FieldOffset(20)] public byte uint8_20;
        [FieldOffset(21)] public byte uint8_21;
        [FieldOffset(22)] public byte uint8_22;
        [FieldOffset(23)] public byte uint8_23;
        [FieldOffset(24)] public byte uint8_24;
        [FieldOffset(25)] public byte uint8_25;
        [FieldOffset(26)] public byte uint8_26;
        [FieldOffset(27)] public byte uint8_27;
        [FieldOffset(28)] public byte uint8_28;
        [FieldOffset(29)] public byte uint8_29;
        [FieldOffset(30)] public byte uint8_30;
        [FieldOffset(31)] public byte uint8_31;
        [FieldOffset(32)] public byte uint8_32;
        [FieldOffset(33)] public byte uint8_33;
        [FieldOffset(34)] public byte uint8_34;
        [FieldOffset(35)] public byte uint8_35;
        [FieldOffset(36)] public byte uint8_36;
        [FieldOffset(37)] public byte uint8_37;
        [FieldOffset(38)] public byte uint8_38;
        [FieldOffset(39)] public byte uint8_39;
        [FieldOffset(40)] public byte uint8_40;
        [FieldOffset(41)] public byte uint8_41;
        [FieldOffset(42)] public byte uint8_42;
        [FieldOffset(43)] public byte uint8_43;
        [FieldOffset(44)] public byte uint8_44;
        [FieldOffset(45)] public byte uint8_45;
        [FieldOffset(46)] public byte uint8_46;
        [FieldOffset(47)] public byte uint8_47;
        [FieldOffset(48)] public byte uint8_48;
        [FieldOffset(49)] public byte uint8_49;
        [FieldOffset(50)] public byte uint8_50;
        [FieldOffset(51)] public byte uint8_51;
        [FieldOffset(52)] public byte uint8_52;
        [FieldOffset(53)] public byte uint8_53;
        [FieldOffset(54)] public byte uint8_54;
        [FieldOffset(55)] public byte uint8_55;
        [FieldOffset(56)] public byte uint8_56;
        [FieldOffset(57)] public byte uint8_57;
        [FieldOffset(58)] public byte uint8_58;
        [FieldOffset(59)] public byte uint8_59;
        [FieldOffset(60)] public byte uint8_60;
        [FieldOffset(61)] public byte uint8_61;
        [FieldOffset(62)] public byte uint8_62;
        [FieldOffset(63)] public byte uint8_63;

        [FieldOffset(0)] public Int64 int64_0;
        [FieldOffset(8)] public Int64 int64_1;
        [FieldOffset(16)] public Int64 int64_2;
        [FieldOffset(24)] public Int64 int64_3;
        [FieldOffset(32)] public Int64 int64_4;
        [FieldOffset(40)] public Int64 int64_5;
        [FieldOffset(48)] public Int64 int64_6;
        [FieldOffset(56)] public Int64 int64_7;

        [FieldOffset(0)] public Int32 int32_0;
        [FieldOffset(4)] public Int32 int32_1;
        [FieldOffset(8)] public Int32 int32_2;
        [FieldOffset(12)] public Int32 int32_3;
        [FieldOffset(16)] public Int32 int32_4;
        [FieldOffset(20)] public Int32 int32_5;
        [FieldOffset(24)] public Int32 int32_6;
        [FieldOffset(28)] public Int32 int32_7;
        [FieldOffset(32)] public Int32 int32_8;
        [FieldOffset(36)] public Int32 int32_9;
        [FieldOffset(40)] public Int32 int32_10;
        [FieldOffset(44)] public Int32 int32_11;
        [FieldOffset(48)] public Int32 int32_12;
        [FieldOffset(52)] public Int32 int32_13;
        [FieldOffset(56)] public Int32 int32_14;
        [FieldOffset(60)] public Int32 int32_15;

        [FieldOffset(0)] public Int16 int16_0;
        [FieldOffset(2)] public Int16 int16_1;
        [FieldOffset(4)] public Int16 int16_2;
        [FieldOffset(6)] public Int16 int16_3;
        [FieldOffset(8)] public Int16 int16_4;
        [FieldOffset(10)] public Int16 int16_5;
        [FieldOffset(12)] public Int16 int16_6;
        [FieldOffset(14)] public Int16 int16_7;
        [FieldOffset(16)] public Int16 int16_8;
        [FieldOffset(18)] public Int16 int16_9;
        [FieldOffset(20)] public Int16 int16_10;
        [FieldOffset(22)] public Int16 int16_11;
        [FieldOffset(24)] public Int16 int16_12;
        [FieldOffset(26)] public Int16 int16_13;
        [FieldOffset(28)] public Int16 int16_14;
        [FieldOffset(30)] public Int16 int16_15;
        [FieldOffset(32)] public Int16 int16_16;
        [FieldOffset(34)] public Int16 int16_17;
        [FieldOffset(36)] public Int16 int16_18;
        [FieldOffset(38)] public Int16 int16_19;
        [FieldOffset(40)] public Int16 int16_20;
        [FieldOffset(42)] public Int16 int16_21;
        [FieldOffset(44)] public Int16 int16_22;
        [FieldOffset(46)] public Int16 int16_23;
        [FieldOffset(48)] public Int16 int16_24;
        [FieldOffset(50)] public Int16 int16_25;
        [FieldOffset(52)] public Int16 int16_26;
        [FieldOffset(54)] public Int16 int16_27;
        [FieldOffset(56)] public Int16 int16_28;
        [FieldOffset(58)] public Int16 int16_29;
        [FieldOffset(60)] public Int16 int16_30;
        [FieldOffset(62)] public Int16 int16_31;

        [FieldOffset(0)] public sbyte int8_0;
        [FieldOffset(1)] public sbyte int8_1;
        [FieldOffset(2)] public sbyte int8_2;
        [FieldOffset(3)] public sbyte int8_3;
        [FieldOffset(4)] public sbyte int8_4;
        [FieldOffset(5)] public sbyte int8_5;
        [FieldOffset(6)] public sbyte int8_6;
        [FieldOffset(7)] public sbyte int8_7;
        [FieldOffset(8)] public sbyte int8_8;
        [FieldOffset(9)] public sbyte int8_9;
        [FieldOffset(10)] public sbyte int8_10;
        [FieldOffset(11)] public sbyte int8_11;
        [FieldOffset(12)] public sbyte int8_12;
        [FieldOffset(13)] public sbyte int8_13;
        [FieldOffset(14)] public sbyte int8_14;
        [FieldOffset(15)] public sbyte int8_15;
        [FieldOffset(16)] public sbyte int8_16;
        [FieldOffset(17)] public sbyte int8_17;
        [FieldOffset(18)] public sbyte int8_18;
        [FieldOffset(19)] public sbyte int8_19;
        [FieldOffset(20)] public sbyte int8_20;
        [FieldOffset(21)] public sbyte int8_21;
        [FieldOffset(22)] public sbyte int8_22;
        [FieldOffset(23)] public sbyte int8_23;
        [FieldOffset(24)] public sbyte int8_24;
        [FieldOffset(25)] public sbyte int8_25;
        [FieldOffset(26)] public sbyte int8_26;
        [FieldOffset(27)] public sbyte int8_27;
        [FieldOffset(28)] public sbyte int8_28;
        [FieldOffset(29)] public sbyte int8_29;
        [FieldOffset(30)] public sbyte int8_30;
        [FieldOffset(31)] public sbyte int8_31;
        [FieldOffset(32)] public sbyte int8_32;
        [FieldOffset(33)] public sbyte int8_33;
        [FieldOffset(34)] public sbyte int8_34;
        [FieldOffset(35)] public sbyte int8_35;
        [FieldOffset(36)] public sbyte int8_36;
        [FieldOffset(37)] public sbyte int8_37;
        [FieldOffset(38)] public sbyte int8_38;
        [FieldOffset(39)] public sbyte int8_39;
        [FieldOffset(40)] public sbyte int8_40;
        [FieldOffset(41)] public sbyte int8_41;
        [FieldOffset(42)] public sbyte int8_42;
        [FieldOffset(43)] public sbyte int8_43;
        [FieldOffset(44)] public sbyte int8_44;
        [FieldOffset(45)] public sbyte int8_45;
        [FieldOffset(46)] public sbyte int8_46;
        [FieldOffset(47)] public sbyte int8_47;
        [FieldOffset(48)] public sbyte int8_48;
        [FieldOffset(49)] public sbyte int8_49;
        [FieldOffset(50)] public sbyte int8_50;
        [FieldOffset(51)] public sbyte int8_51;
        [FieldOffset(52)] public sbyte int8_52;
        [FieldOffset(53)] public sbyte int8_53;
        [FieldOffset(54)] public sbyte int8_54;
        [FieldOffset(55)] public sbyte int8_55;
        [FieldOffset(56)] public sbyte int8_56;
        [FieldOffset(57)] public sbyte int8_57;
        [FieldOffset(58)] public sbyte int8_58;
        [FieldOffset(59)] public sbyte int8_59;
        [FieldOffset(60)] public sbyte int8_60;
        [FieldOffset(61)] public sbyte int8_61;
        [FieldOffset(62)] public sbyte int8_62;
        [FieldOffset(63)] public sbyte int8_63;

        [FieldOffset(0)] public double fp64_0;
        [FieldOffset(8)] public double fp64_1;
        [FieldOffset(16)] public double fp64_2;
        [FieldOffset(24)] public double fp64_3;
        [FieldOffset(32)] public double fp64_4;
        [FieldOffset(40)] public double fp64_5;
        [FieldOffset(48)] public double fp64_6;
        [FieldOffset(56)] public double fp64_7;

        [FieldOffset(0)] public float fp32_0;
        [FieldOffset(4)] public float fp32_1;
        [FieldOffset(8)] public float fp32_2;
        [FieldOffset(12)] public float fp32_3;
        [FieldOffset(16)] public float fp32_4;
        [FieldOffset(20)] public float fp32_5;
        [FieldOffset(24)] public float fp32_6;
        [FieldOffset(28)] public float fp32_7;
        [FieldOffset(32)] public float fp32_8;
        [FieldOffset(36)] public float fp32_9;
        [FieldOffset(40)] public float fp32_10;
        [FieldOffset(44)] public float fp32_11;
        [FieldOffset(48)] public float fp32_12;
        [FieldOffset(52)] public float fp32_13;
        [FieldOffset(56)] public float fp32_14;
        [FieldOffset(60)] public float fp32_15;

        // -- index access utilities -- //

        public unsafe UInt64 uint64(int index)
        {
            fixed (UInt64* ptr = &uint64_0) return ptr[index];
        }
        public unsafe void uint64(int index, UInt64 val)
        {
            fixed (UInt64* ptr = &uint64_0) ptr[index] = val;
        }

        public unsafe UInt32 uint32(int index)
        {
            fixed (UInt32* ptr = &uint32_0) return ptr[index];
        }
        public unsafe void uint32(int index, UInt32 val)
        {
            fixed (UInt32* ptr = &uint32_0) ptr[index] = val;
        }

        public unsafe UInt16 uint16(int index)
        {
            fixed (UInt16* ptr = &uint16_0) return ptr[index];
        }
        public unsafe void uint16(int index, UInt16 val)
        {
            fixed (UInt16* ptr = &uint16_0) ptr[index] = val;
        }

        public unsafe byte uint8(int index)
        {
            fixed (byte* ptr = &uint8_0) return ptr[index];
        }
        public unsafe void uint8(int index, byte val)
        {
            fixed (byte* ptr = &uint8_0) ptr[index] = val;
        }

        public unsafe Int64 int64(int index)
        {
            fixed (Int64* ptr = &int64_0) return ptr[index];
        }
        public unsafe void int64(int index, Int64 val)
        {
            fixed (Int64* ptr = &int64_0) ptr[index] = val;
        }

        public unsafe Int32 int32(int index)
        {
            fixed (Int32* ptr = &int32_0) return ptr[index];
        }
        public unsafe void int32(int index, Int32 val)
        {
            fixed (Int32* ptr = &int32_0) ptr[index] = val;
        }

        public unsafe Int16 int16(int index)
        {
            fixed (Int16* ptr = &int16_0) return ptr[index];
        }
        public unsafe void int16(int index, Int16 val)
        {
            fixed (Int16* ptr = &int16_0) ptr[index] = val;
        }

        public unsafe sbyte int8(int index)
        {
            fixed (sbyte* ptr = &int8_0) return ptr[index];
        }
        public unsafe void int8(int index, sbyte val)
        {
            fixed (sbyte* ptr = &int8_0) ptr[index] = val;
        }

        public unsafe double fp64(int index)
        {
            fixed (double* ptr = &fp64_0) return ptr[index];
        }
        public unsafe void fp64(int index, double val)
        {
            fixed (double* ptr = &fp64_0) ptr[index] = val;
        }

        public unsafe float fp32(int index)
        {
            fixed (float* ptr = &fp32_0) return ptr[index];
        }
        public unsafe void fp32(int index, float val)
        {
            fixed (float* ptr = &fp32_0) ptr[index] = val;
        }

        // -- sizecode access utilities -- //

        public UInt64 _uint(UInt64 sizecode, int index)
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
        public void _uint(UInt64 sizecode, int index, UInt64 val)
        {
            switch (sizecode)
            {
                case 0: uint8(index, (byte)val); return;
                case 1: uint16(index, (UInt16)val); return;
                case 2: uint32(index, (UInt32)val); return;
                case 3: uint64(index, val); return;

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