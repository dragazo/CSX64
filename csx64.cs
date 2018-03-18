using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Numerics;

namespace csx64
{
    public static class ComputerExtensions
    {
        public static Int64 MakeSigned(this UInt64 val)
        {
            return (val & 0x8000000000000000) != 0 ? -(Int64)(~val + 1) : (Int64)val;
        }
        public static UInt64 MakeUnsigned(this Int64 val)
        {
            return val < 0 ? ~(UInt64)(-val) + 1 : (UInt64)val;
        }
    }

    /// <summary>
    /// Represents a computer executing a binary program (little-endian)
    /// </summary>
    public class CSX64
    {
        /// <summary>
        /// Version number
        /// </summary>
        public const UInt64 Version = 0x0314;

        // -----------
        // -- Types --
        // -----------

        public enum ErrorCode
        {
            None, OutOfBounds, UnhandledSyscall, UndefinedBehavior, ArithmeticError, Abort
        }
        public enum OPCode
        {
            NOP, STOP, SYSCALL,

            MOV,
            MOVa, MOVae, MOVb, MOVbe, MOVg, MOVge, MOVl, MOVle,
            MOVz, MOVnz, MOVs, MOVns, MOVp, MOVnp, MOVo, MOVno, MOVc, MOVnc,

            SWAP,

            UEXTEND, SEXTEND,

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
            POW, SQRT, EXP, LN, FNEG, FABS, FCMPZ,

            SIN, COS, TAN,
            SINH, COSH, TANH,
            ASIN, ACOS, ATAN, ATAN2,

            FLOOR, CEIL, ROUND, TRUNC,

            FCMP,

            FTOI, ITOF,

            PUSH, POP, CALL, RET,

            BSWAP, BEXTR, BLSI, BLSMSK, BLSR, ANDN,

            GETF, SETF,

            LOOP
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

            /// <summary>
            /// Gets the register partition with the specified size code
            /// </summary>
            /// <param name="code">The size code</param>
            public UInt64 Get(UInt64 code)
            {
                switch (code)
                {
                    case 0: return x8;
                    case 1: return x16;
                    case 2: return x32;
                    case 3: return x64;

                    default: throw new ArgumentOutOfRangeException("Register code out of range");
                }
            }
            /// <summary>
            /// Sets the register partition with the specified size code
            /// </summary>
            /// <param name="code">The size code</param>
            /// <param name="value">The value to set</param>
            public void Set(UInt64 code, UInt64 value)
            {
                switch (code)
                {
                    case 0: x8 = value; return;
                    case 1: x16 = value; return;
                    case 2: x32 = value; return;
                    case 3: x64 = value; return;

                    default: throw new ArgumentOutOfRangeException("Register code out of range");
                }
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
                get { return (Flags & 0x01) != 0; }
                set { Flags = (Flags & 0xfe) | (value ? 0x01 : 0ul); }
            }
            /// <summary>
            /// The Sign flag
            /// </summary>
            public bool S
            {
                get { return (Flags & 0x02) != 0; }
                set { Flags = (Flags & 0xfd) | (value ? 0x02 : 0ul); }
            }
            /// <summary>
            /// The Parity flag
            /// </summary>
            public bool P
            {
                get { return (Flags & 0x04) != 0; }
                set { Flags = (Flags & 0xfb) | (value ? 0x04 : 0ul); }
            }
            /// <summary>
            /// The Overflow flag
            /// </summary>
            public bool O
            {
                get { return (Flags & 0x08) != 0; }
                set { Flags = (Flags & 0xf7) | (value ? 0x08 : 0ul); }
            }
            /// <summary>
            /// The Carry flag
            /// </summary>
            public bool C
            {
                get { return (Flags & 0x10) != 0; }
                set { Flags = (Flags & 0xef) | (value ? 0x10 : 0ul); }
            }

            public bool a { get => !C && !Z; }
            public bool ae { get => !C; }
            public bool b { get => C; }
            public bool be { get => C || Z; }

            public bool g { get => !Z && S == O; }
            public bool ge { get => S == O; }
            public bool l { get => S != O; }
            public bool le { get => Z || S != O; }
        }

        // --------------------
        // -- Execution Data --
        // --------------------

        private Register[] Registers = new Register[16];
        private FlagsRegister Flags = new FlagsRegister();

        private byte[] Memory = null;

        /// <summary>
        /// Gets the total amount of memory the processor currently has access to
        /// </summary>
        public UInt64 MemorySize { get => (UInt64)Memory.LongLength; }

        /// <summary>
        /// The current execution positon (executed on next tick)
        /// </summary>
        public UInt64 Pos { get; private set; }
        /// <summary>
        /// Flag marking if the program is still executing
        /// </summary>
        public bool Running { get; private set; }

        /// <summary>
        /// Gets the current error code
        /// </summary>
        public ErrorCode Error { get; private set; }

        private static Random Rand = new Random();

        // -----------------------
        // -- Utility Functions --
        // -----------------------

        /// <summary>
        /// Returns if the value with specified size code is positive
        /// </summary>
        /// <param name="val">the value to process</param>
        /// <param name="sizecode">the current size code of the value</param>
        protected static bool Positive(UInt64 val, UInt64 sizecode)
        {
            return ((val >> (8 * (ushort)Size(sizecode) - 1)) & 1) == 0;
        }
        /// <summary>
        /// Returns if the value with specified size code is negative
        /// </summary>
        /// <param name="val">the value to process</param>
        /// <param name="sizecode">the current size code of the value</param>
        protected static bool Negative(UInt64 val, UInt64 sizecode)
        {
            return ((val >> (8 * (ushort)Size(sizecode) - 1)) & 1) != 0;
        }

        /// <summary>
        /// Sign extends a value to 64-bits
        /// </summary>
        /// <param name="val">the value to sign extend</param>
        /// <param name="sizecode">the current size code</param>
        protected static UInt64 SignExtend(UInt64 val, UInt64 sizecode)
        {
            // if val is positive, do nothing
            if (Positive(val, sizecode)) return val;

            // otherwise, pad with 1's
            switch (sizecode)
            {
                case 0: return 0xffffffffffffff00 | val;
                case 1: return 0xffffffffffff0000 | val;
                case 2: return 0xffffffff00000000 | val;

                default: return val; // can't extend 64-bit value any further
            }
        }
        /// <summary>
        /// Truncates the value to the specified size code (can also be used to zero extend a value)
        /// </summary>
        /// <param name="val">the value to truncate</param>
        /// <param name="sizecode">the size code to truncate to</param>
        protected static UInt64 Truncate(UInt64 val, UInt64 sizecode)
        {
            switch (sizecode)
            {
                case 0: return 0x00000000000000ff & val;
                case 1: return 0x000000000000ffff & val;
                case 2: return 0x00000000ffffffff & val;

                default: return val; // can't truncate 64-bit value
            }
        }

        /// <summary>
        /// Parses a 2-bit size code into an actual size (in bytes) 0:1  1:2  2:4  3:8
        /// </summary>
        /// <param name="sizecode">the code to parse</param>
        protected static UInt64 Size(UInt64 sizecode)
        {
            return 1ul << (ushort)sizecode;
        }
        /// <summary>
        /// Parses a 2-bit size code into an actual size (in bits) 0:8  1:16  2:32  3:64
        /// </summary>
        /// <param name="sizecode">the code to parse</param>
        protected static UInt64 SizeBits(UInt64 sizecode)
        {
            return 8ul << (ushort)sizecode;
        }

        /// <summary>
        /// Gets the multiplier from a 3-bit mult code. 0:0  1:1  2:2  3:4  4:8  5:16  6:32  7:64
        /// </summary>
        /// <param name="code">the code to parse</param>
        protected static UInt64 MultCode(UInt64 code)
        {
            return code == 0 ? 0ul : 1ul << (ushort)(code - 1);
        }
        /// <summary>
        /// As MultCode but returns negative value if neg is nonzero
        /// </summary>
        /// <param name="code">the code to parse</param>
        /// <param name="neg">the negative boolean</param>
        protected static UInt64 MultCode(UInt64 code, UInt64 neg)
        {
            return neg == 0 ? MultCode(code) : ~MultCode(code) + 1;
        }

        /// <summary>
        /// Interprets a double as its raw bits
        /// </summary>
        /// <param name="val">value to interpret</param>
        public static unsafe UInt64 DoubleAsUInt64(double val)
        {
            return *(UInt64*)&val;
        }
        /// <summary>
        /// Interprets raw bits as a double
        /// </summary>
        /// <param name="val">value to interpret</param>
        public static unsafe double AsDouble(UInt64 val)
        {
            return *(double*)&val;
        }

        /// <summary>
        /// Interprets a float as its raw bits (placed in low 32 bits)
        /// </summary>
        /// <param name="val">the float to interpret</param>
        public static unsafe UInt64 FloatAsUInt64(float val)
        {
            return *(UInt32*)&val;
        }
        /// <summary>
        /// Interprets raw bits as a float (low 32 bits)
        /// </summary>
        /// <param name="val">the bits to interpret</param>
        public static unsafe float AsFloat(UInt64 val)
        {
            UInt32 bits = (UInt32)val; // getting the low bits allows this code to work even on big-endian platforms
            return *(float*)&bits;
        }

        // ---------------
        // -- Operators --
        // ---------------

        #region

        // -- op utilities --

        /*
        [8: binary op]   [4: dest][2: size][2: mode]
	        mode = 0: [size: imm]
		        dest <- imm
	        mode = 1: [address]
		        dest <- M[address]
	        mode = 2: [3:][1: mode2][4: src]
		        mode2 = 0:
			        dest <- src
		        mode2 = 1: [address]
			        M[address] <- src
	        mode = 3: [size: imm]   [address]
		        M[address] <- imm
        */
        private bool FetchBinaryOpFormat(ref UInt64 s, ref UInt64 m, ref UInt64 a, ref UInt64 b, int _a_sizecode = -1, int _b_sizecode = -1)
        {
            // read settings
            if (!GetMemAdv(1, ref s)) return false;

            UInt64 a_sizecode = _a_sizecode == -1 ? (s >> 2) & 3 : (UInt64)_a_sizecode;
            UInt64 b_sizecode = _b_sizecode == -1 ? (s >> 2) & 3 : (UInt64)_b_sizecode;

            // switch through mode
            switch (s & 3)
            {
                case 0:
                    a = Registers[s >> 4].Get(a_sizecode);
                    if (!GetMemAdv(Size(b_sizecode), ref b)) return false;
                    break;
                case 1:
                    a = Registers[s >> 4].Get(a_sizecode);
                    if (!GetAddressAdv(ref b) || !GetMem(b, Size(b_sizecode), ref b)) return false;
                    break;
                case 2:
                    if (!GetMemAdv(1, ref b)) return false;
                    switch ((b >> 4) & 1)
                    {
                        case 0:
                            a = Registers[s >> 4].Get(a_sizecode);
                            b = Registers[b & 15].Get(b_sizecode);
                            break;
                        case 1:
                            if (!GetAddressAdv(ref m) || !GetMem(m, Size(a_sizecode), ref a)) return false;
                            b = Registers[b & 15].Get(b_sizecode);
                            s |= 256; // mark as memory path of mode 2
                            break;
                    }
                    break;
                case 3:
                    if (!GetMemAdv(Size(b_sizecode), ref b)) return false;
                    if (!GetAddressAdv(ref m) || !GetMem(m, Size(a_sizecode), ref a)) return false;
                    break;
            }

            return true;
        }
        private bool StoreBinaryOpFormat(UInt64 s, UInt64 m, UInt64 res, int _sizecode = -1)
        {
            UInt64 sizecode = _sizecode == -1 ? (s >> 2) & 3 : (UInt64)_sizecode;

            // switch through mode
            switch (s & 3)
            {
                case 0:
                case 1:
                reg:
                    Registers[s >> 4].Set(sizecode, res);
                    break;
                case 2:
                    if (s < 256) goto reg; else goto mem;
                case 3:
                mem:
                    if (!SetMem(m, Size(sizecode), res)) return false;
                    break;
            }

            return true;
        }

        /*
        [8: unary op]   [4: dest][2: size][1:][1: mem]
	        mem = 0:
		        dest <- dest
	        mem = 1: [address]
		        M[address] <- M[address]
        */
        private bool FetchUnaryOpFormat(ref UInt64 s, ref UInt64 m, ref UInt64 a, int _a_sizecode = -1)
        {
            // read settings
            if (!GetMemAdv(1, ref s)) return false;

            UInt64 a_sizecode = _a_sizecode == -1 ? (s >> 2) & 3 : (UInt64)_a_sizecode;

            // switch through mode
            switch (s & 1)
            {
                case 0:
                    a = Registers[s >> 4].Get(a_sizecode);
                    break;
                case 1:
                    if (!GetAddressAdv(ref m) || !GetMem(m, Size(a_sizecode), ref a)) return false;
                    break;
            }

            return true;
        }
        private bool StoreUnaryOpFormat(UInt64 s, UInt64 m, UInt64 res, int _sizecode = -1)
        {
            UInt64 sizecode = _sizecode == -1 ? (s >> 2) & 3 : (UInt64)_sizecode;

            // switch through mode
            switch (s & 1)
            {
                case 0:
                    Registers[s >> 4].Set(sizecode, res);
                    break;
                case 1:
                    if (!SetMem(m, Size(sizecode), res)) return false;
                    break;
            }

            return true;
        }

        // updates the ZSP flags for integral ops
        private void UpdateFlagsI(UInt64 value, UInt64 sizecode)
        {
            Flags.Z = value == 0;
            Flags.S = Negative(value, sizecode);

            // compute parity flag (only of low 8 bits)
            bool parity = true;
            for (int i = 0; i < 8; ++i)
                if (((value >> i) & 1) != 0) parity = !parity;
            Flags.P = parity;
        }
        // updates the ZSOC flags for floating point ops
        private void UpdateFlagsDouble(double value)
        {
            Flags.Z = value == 0;
            Flags.S = value < 0;

            Flags.O = double.IsInfinity(value);
            Flags.C = double.IsNaN(value);
        }
        private void UpdateFlagsFloat(float value)
        {
            Flags.Z = value == 0;
            Flags.S = value < 0;

            Flags.O = float.IsInfinity(value);
            Flags.C = float.IsNaN(value);
        }

        // -- special ops --

        private bool ProcessMov(bool apply = true)
        {
            UInt64 s = 0, m = 0, a = 0, b = 0;
            if (!FetchBinaryOpFormat(ref s, ref m, ref a, ref b)) return false;

            return apply ? StoreBinaryOpFormat(s, m, b) : true;
        }

        // -- integral ops --

        private bool ProcessADD()
        {
            UInt64 s = 0, m = 0, a = 0, b = 0;
            if (!FetchBinaryOpFormat(ref s, ref m, ref a, ref b)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UInt64 res = Truncate(a + b, sizecode);

            UpdateFlagsI(res, sizecode);
            Flags.C = res < a && res < b; // if overflow is caused, some of one value must go toward it, so the truncated result must necessarily be less than both args
            Flags.O = Positive(a, sizecode) == Positive(b, sizecode) && Positive(a, sizecode) != Positive(res, sizecode);

            return StoreBinaryOpFormat(s, m, res);
        }
        private bool ProcessSUB(bool apply = true)
        {
            UInt64 s = 0, m = 0, a = 0, b = 0;
            if (!FetchBinaryOpFormat(ref s, ref m, ref a, ref b)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UInt64 res = Truncate(a - b, sizecode);

            UpdateFlagsI(res, sizecode);
            Flags.C = a < b; // if a < b, a borrow was taken from the highest bit
            Flags.O = Positive(a, sizecode) != Positive(b, sizecode) && Positive(a, sizecode) != Positive(res, sizecode);

            return apply ? StoreBinaryOpFormat(s, m, res) : true;
        }

        private bool ProcessBMUL()
        {
            UInt64 s = 0, m = 0, a = 0, b = 0;
            if (!FetchBinaryOpFormat(ref s, ref m, ref a, ref b)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UInt64 res = Truncate(a * b, sizecode);

            UpdateFlagsI(res, sizecode);

            return StoreBinaryOpFormat(s, m, res);
        }

        private bool ProcessBUDIV()
        {
            UInt64 s = 0, m = 0, a = 0, b = 0;
            if (!FetchBinaryOpFormat(ref s, ref m, ref a, ref b)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            if (b == 0) { Fail(ErrorCode.ArithmeticError); return false; }

            UInt64 res = Truncate(a / b, sizecode);

            UpdateFlagsI(res, sizecode);

            return StoreBinaryOpFormat(s, m, res);
        }
        private bool ProcessBUMOD()
        {
            UInt64 s = 0, m = 0, a = 0, b = 0;
            if (!FetchBinaryOpFormat(ref s, ref m, ref a, ref b)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            if (b == 0) { Fail(ErrorCode.ArithmeticError); return false; }

            UInt64 res = Truncate(a % b, sizecode);

            UpdateFlagsI(res, sizecode);

            return StoreBinaryOpFormat(s, m, res);
        }

        private bool ProcessBSDIV()
        {
            UInt64 s = 0, m = 0, a = 0, b = 0;
            if (!FetchBinaryOpFormat(ref s, ref m, ref a, ref b)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            if (b == 0) { Fail(ErrorCode.ArithmeticError); return false; }

            UInt64 res = Truncate((SignExtend(a, sizecode).MakeSigned() / SignExtend(b, sizecode).MakeSigned()).MakeUnsigned(), sizecode);

            UpdateFlagsI(res, sizecode);

            return StoreBinaryOpFormat(s, m, res);
        }
        private bool ProcessBSMOD()
        {
            UInt64 s = 0, m = 0, a = 0, b = 0;
            if (!FetchBinaryOpFormat(ref s, ref m, ref a, ref b)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            if (b == 0) { Fail(ErrorCode.ArithmeticError); return false; }

            UInt64 res = Truncate((SignExtend(a, sizecode).MakeSigned() % SignExtend(b, sizecode).MakeSigned()).MakeUnsigned(), sizecode);

            UpdateFlagsI(res, sizecode);

            return StoreBinaryOpFormat(s, m, res);
        }

        private bool ProcessSL()
        {
            UInt64 s = 0, m = 0, a = 0, b = 0;
            if (!FetchBinaryOpFormat(ref s, ref m, ref a, ref b)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UInt16 sh = (UInt16)(b % SizeBits(sizecode));
            UInt64 res = Truncate(a << sh, sizecode);

            UpdateFlagsI(res, sizecode);

            return StoreBinaryOpFormat(s, m, res);
        }
        private bool ProcessSR()
        {
            UInt64 s = 0, m = 0, a = 0, b = 0;
            if (!FetchBinaryOpFormat(ref s, ref m, ref a, ref b)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UInt16 sh = (UInt16)(b % SizeBits(sizecode));
            UInt64 res = a >> sh;

            UpdateFlagsI(res, sizecode);

            return StoreBinaryOpFormat(s, m, res);
        }

        private bool ProcessSAL()
        {
            UInt64 s = 0, m = 0, a = 0, b = 0;
            if (!FetchBinaryOpFormat(ref s, ref m, ref a, ref b)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UInt16 sh = (UInt16)(b % SizeBits(sizecode));
            UInt64 res = Truncate((SignExtend(a, sizecode).MakeSigned() << sh).MakeUnsigned(), sizecode);

            UpdateFlagsI(res, sizecode);

            return StoreBinaryOpFormat(s, m, res);
        }
        private bool ProcessSAR()
        {
            UInt64 s = 0, m = 0, a = 0, b = 0;
            if (!FetchBinaryOpFormat(ref s, ref m, ref a, ref b)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UInt16 sh = (UInt16)(b % SizeBits(sizecode));
            UInt64 res = Truncate((SignExtend(a, sizecode).MakeSigned() >> sh).MakeUnsigned(), sizecode);

            UpdateFlagsI(res, sizecode);

            return StoreBinaryOpFormat(s, m, res);
        }

        private bool ProcessRL()
        {
            UInt64 s = 0, m = 0, a = 0, b = 0;
            if (!FetchBinaryOpFormat(ref s, ref m, ref a, ref b)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UInt16 sh = (UInt16)(b % SizeBits(sizecode));
            UInt64 res = Truncate((a << sh) | (a >> ((UInt16)SizeBits(sizecode) - sh)), sizecode);

            UpdateFlagsI(res, sizecode);

            return StoreBinaryOpFormat(s, m, res);
        }
        private bool ProcessRR()
        {
            UInt64 s = 0, m = 0, a = 0, b = 0;
            if (!FetchBinaryOpFormat(ref s, ref m, ref a, ref b)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UInt16 sh = (UInt16)(b % SizeBits(sizecode));
            UInt64 res = Truncate((a >> sh) | (a << ((UInt16)SizeBits(sizecode) - sh)), sizecode);

            UpdateFlagsI(res, sizecode);

            return StoreBinaryOpFormat(s, m, res);
        }

        private bool ProcessAND(bool apply = true)
        {
            UInt64 s = 0, m = 0, a = 0, b = 0;
            if (!FetchBinaryOpFormat(ref s, ref m, ref a, ref b)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UInt64 res = a & b;

            UpdateFlagsI(res, sizecode);

            return apply ? StoreBinaryOpFormat(s, m, res) : true;
        }
        private bool ProcessOR()
        {
            UInt64 s = 0, m = 0, a = 0, b = 0;
            if (!FetchBinaryOpFormat(ref s, ref m, ref a, ref b)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UInt64 res = a | b;

            UpdateFlagsI(res, sizecode);

            return StoreBinaryOpFormat(s, m, res);
        }
        private bool ProcessXOR()
        {
            UInt64 s = 0, m = 0, a = 0, b = 0;
            if (!FetchBinaryOpFormat(ref s, ref m, ref a, ref b)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UInt64 res = a ^ b;

            UpdateFlagsI(res, sizecode);

            return StoreBinaryOpFormat(s, m, res);
        }

        private bool ProcessINC()
        {
            UInt64 s = 0, m = 0, a = 0;
            if (!FetchUnaryOpFormat(ref s, ref m, ref a)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UInt64 res = Truncate(a + 1, sizecode);

            UpdateFlagsI(res, sizecode);
            Flags.C = res == 0; // carry results in zero
            Flags.O = Positive(a, sizecode) && Negative(res, sizecode); // + -> - is overflow

            return StoreUnaryOpFormat(s, m, res);
        }
        private bool ProcessDEC()
        {
            UInt64 s = 0, m = 0, a = 0;
            if (!FetchUnaryOpFormat(ref s, ref m, ref a)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UInt64 res = Truncate(a - 1, sizecode);

            UpdateFlagsI(res, sizecode);
            Flags.C = a == 0; // a = 0 results in borrow from high bit (carry)
            Flags.O = Negative(a, sizecode) && Positive(res, sizecode); // - -> + is overflow

            return StoreUnaryOpFormat(s, m, res);
        }
        private bool ProcessNOT()
        {
            UInt64 s = 0, m = 0, a = 0;
            if (!FetchUnaryOpFormat(ref s, ref m, ref a)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UInt64 res = Truncate(~a, sizecode);

            UpdateFlagsI(res, sizecode);

            return StoreUnaryOpFormat(s, m, res);
        }
        private bool ProcessNEG()
        {
            UInt64 s = 0, m = 0, a = 0;
            if (!FetchUnaryOpFormat(ref s, ref m, ref a)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UInt64 res = Truncate(~a + 1, sizecode);

            UpdateFlagsI(res, sizecode);

            return StoreUnaryOpFormat(s, m, res);
        }
        private bool ProcessABS()
        {
            UInt64 s = 0, m = 0, a = 0;
            if (!FetchUnaryOpFormat(ref s, ref m, ref a)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UInt64 res = Positive(a, sizecode) ? a : Truncate(~a + 1, sizecode);

            UpdateFlagsI(res, sizecode);

            return StoreUnaryOpFormat(s, m, res);
        }
        private bool ProcessCMPZ()
        {
            UInt64 s = 0, m = 0, a = 0;
            if (!FetchUnaryOpFormat(ref s, ref m, ref a)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UpdateFlagsI(a, sizecode);
            Flags.C = Flags.O = false;

            return true;
        }

        // -- floatint point ops --

        private bool ProcessFADD()
        {
            UInt64 s = 0, m = 0, a = 0, b = 0;
            if (!FetchBinaryOpFormat(ref s, ref m, ref a, ref b)) return false;

            switch ((s >> 2) & 3)
            {
                case 3:
                    {
                        double res = AsDouble(a) + AsDouble(b);

                        UpdateFlagsDouble(res);

                        return StoreBinaryOpFormat(s, m, DoubleAsUInt64(res));
                    }
                case 2:
                    {
                        float res = AsFloat(a) + AsFloat(b);

                        UpdateFlagsFloat(res);

                        return StoreBinaryOpFormat(s, m, FloatAsUInt64(res));
                    }

                default: Fail(ErrorCode.UndefinedBehavior); return false;
            }
        }
        private bool ProcessFSUB(bool apply = true)
        {
            UInt64 s = 0, m = 0, a = 0, b = 0;
            if (!FetchBinaryOpFormat(ref s, ref m, ref a, ref b)) return false;

            switch ((s >> 2) & 3)
            {
                case 3:
                    {
                        double res = AsDouble(a) - AsDouble(b);

                        UpdateFlagsDouble(res);

                        return apply ? StoreBinaryOpFormat(s, m, DoubleAsUInt64(res)) : true;
                    }
                case 2:
                    {
                        float res = AsFloat(a) - AsFloat(b);

                        UpdateFlagsFloat(res);

                        return apply ? StoreBinaryOpFormat(s, m, FloatAsUInt64(res)) : true;
                    }

                default: Fail(ErrorCode.UndefinedBehavior); return false;
            }
        }

        private bool ProcessFMUL()
        {
            UInt64 s = 0, m = 0, a = 0, b = 0;
            if (!FetchBinaryOpFormat(ref s, ref m, ref a, ref b)) return false;

            switch ((s >> 2) & 3)
            {
                case 3:
                    {
                        double res = AsDouble(a) * AsDouble(b);

                        UpdateFlagsDouble(res);

                        return StoreBinaryOpFormat(s, m, DoubleAsUInt64(res));
                    }
                case 2:
                    {
                        float res = AsFloat(a) * AsFloat(b);

                        UpdateFlagsFloat(res);

                        return StoreBinaryOpFormat(s, m, FloatAsUInt64(res));
                    }

                default: Fail(ErrorCode.UndefinedBehavior); return false;
            }
        }

        private bool ProcessFDIV()
        {
            UInt64 s = 0, m = 0, a = 0, b = 0;
            if (!FetchBinaryOpFormat(ref s, ref m, ref a, ref b)) return false;

            switch ((s >> 2) & 3)
            {
                case 3:
                    {
                        double res = AsDouble(a) / AsDouble(b);

                        UpdateFlagsDouble(res);

                        return StoreBinaryOpFormat(s, m, DoubleAsUInt64(res));
                    }
                case 2:
                    {
                        float res = AsFloat(a) / AsFloat(b);

                        UpdateFlagsFloat(res);

                        return StoreBinaryOpFormat(s, m, FloatAsUInt64(res));
                    }

                default: Fail(ErrorCode.UndefinedBehavior); return false;
            }
        }
        private bool ProcessFMOD()
        {
            UInt64 s = 0, m = 0, a = 0, b = 0;
            if (!FetchBinaryOpFormat(ref s, ref m, ref a, ref b)) return false;

            switch ((s >> 2) & 3)
            {
                case 3:
                    {
                        double res = AsDouble(a) % AsDouble(b);

                        UpdateFlagsDouble(res);

                        return StoreBinaryOpFormat(s, m, DoubleAsUInt64(res));
                    }
                case 2:
                    {
                        float res = AsFloat(a) % AsFloat(b);

                        UpdateFlagsFloat(res);

                        return StoreBinaryOpFormat(s, m, FloatAsUInt64(res));
                    }

                default: Fail(ErrorCode.UndefinedBehavior); return false;
            }
        }

        private bool ProcessPOW()
        {
            UInt64 s = 0, m = 0, a = 0, b = 0;
            if (!FetchBinaryOpFormat(ref s, ref m, ref a, ref b)) return false;

            switch ((s >> 2) & 3)
            {
                case 3:
                    {
                        double res = Math.Pow(AsDouble(a), AsDouble(b));

                        UpdateFlagsDouble(res);

                        return StoreBinaryOpFormat(s, m, DoubleAsUInt64(res));
                    }
                case 2:
                    {
                        float res = (float)Math.Pow(AsFloat(a), AsFloat(b));

                        UpdateFlagsFloat(res);

                        return StoreBinaryOpFormat(s, m, FloatAsUInt64(res));
                    }

                default: Fail(ErrorCode.UndefinedBehavior); return false;
            }
        }
        private bool ProcessSQRT()
        {
            UInt64 s = 0, m = 0, a = 0;
            if (!FetchUnaryOpFormat(ref s, ref m, ref a)) return false;

            switch ((s >> 2) & 3)
            {
                case 3:
                    {
                        double res = Math.Sqrt(AsDouble(a));

                        UpdateFlagsDouble(res);

                        return StoreBinaryOpFormat(s, m, DoubleAsUInt64(res));
                    }
                case 2:
                    {
                        float res = (float)Math.Sqrt(AsFloat(a));

                        UpdateFlagsFloat(res);

                        return StoreBinaryOpFormat(s, m, FloatAsUInt64(res));
                    }

                default: Fail(ErrorCode.UndefinedBehavior); return false;
            }
        }
        private bool ProcessEXP()
        {
            UInt64 s = 0, m = 0, a = 0;
            if (!FetchUnaryOpFormat(ref s, ref m, ref a)) return false;

            switch ((s >> 2) & 3)
            {
                case 3:
                    {
                        double res = Math.Exp(AsDouble(a));

                        UpdateFlagsDouble(res);

                        return StoreBinaryOpFormat(s, m, DoubleAsUInt64(res));
                    }
                case 2:
                    {
                        float res = (float)Math.Exp(AsFloat(a));

                        UpdateFlagsFloat(res);

                        return StoreBinaryOpFormat(s, m, FloatAsUInt64(res));
                    }

                default: Fail(ErrorCode.UndefinedBehavior); return false;
            }
        }
        private bool ProcessLN()
        {
            UInt64 s = 0, m = 0, a = 0;
            if (!FetchUnaryOpFormat(ref s, ref m, ref a)) return false;

            switch ((s >> 2) & 3)
            {
                case 3:
                    {
                        double res = Math.Log(AsDouble(a));

                        UpdateFlagsDouble(res);

                        return StoreBinaryOpFormat(s, m, DoubleAsUInt64(res));
                    }
                case 2:
                    {
                        float res = (float)Math.Log(AsFloat(a));

                        UpdateFlagsFloat(res);

                        return StoreBinaryOpFormat(s, m, FloatAsUInt64(res));
                    }

                default: Fail(ErrorCode.UndefinedBehavior); return false;
            }
        }
        private bool ProcessFNEG()
        {
            UInt64 s = 0, m = 0, a = 0;
            if (!FetchUnaryOpFormat(ref s, ref m, ref a)) return false;

            switch ((s >> 2) & 3)
            {
                case 3:
                    {
                        double res = -AsDouble(a);

                        UpdateFlagsDouble(res);

                        return StoreBinaryOpFormat(s, m, DoubleAsUInt64(res));
                    }
                case 2:
                    {
                        float res = -AsFloat(a);

                        UpdateFlagsFloat(res);

                        return StoreBinaryOpFormat(s, m, FloatAsUInt64(res));
                    }

                default: Fail(ErrorCode.UndefinedBehavior); return false;
            }
        }
        private bool ProcessFABS()
        {
            UInt64 s = 0, m = 0, a = 0;
            if (!FetchUnaryOpFormat(ref s, ref m, ref a)) return false;

            switch ((s >> 2) & 3)
            {
                case 3:
                    {
                        double res = Math.Abs(AsDouble(a));

                        UpdateFlagsDouble(res);

                        return StoreBinaryOpFormat(s, m, DoubleAsUInt64(res));
                    }
                case 2:
                    {
                        float res = Math.Abs(AsFloat(a));

                        UpdateFlagsFloat(res);

                        return StoreBinaryOpFormat(s, m, FloatAsUInt64(res));
                    }

                default: Fail(ErrorCode.UndefinedBehavior); return false;
            }
        }
        private bool ProcessFCMPZ()
        {
            UInt64 s = 0, m = 0, a = 0;
            if (!FetchUnaryOpFormat(ref s, ref m, ref a)) return false;

            switch ((s >> 2) & 3)
            {
                case 3:
                    {
                        double res = AsDouble(a);

                        UpdateFlagsDouble(res);

                        return StoreBinaryOpFormat(s, m, DoubleAsUInt64(res));
                    }
                case 2:
                    {
                        float res = AsFloat(a);

                        UpdateFlagsFloat(res);

                        return StoreBinaryOpFormat(s, m, FloatAsUInt64(res));
                    }

                default: Fail(ErrorCode.UndefinedBehavior); return false;
            }
        }

        private bool ProcessSIN()
        {
            UInt64 s = 0, m = 0, a = 0;
            if (!FetchUnaryOpFormat(ref s, ref m, ref a)) return false;

            switch ((s >> 2) & 3)
            {
                case 3:
                    {
                        double res = Math.Sin(AsDouble(a));

                        UpdateFlagsDouble(res);

                        return StoreBinaryOpFormat(s, m, DoubleAsUInt64(res));
                    }
                case 2:
                    {
                        float res = (float)Math.Sin(AsFloat(a));

                        UpdateFlagsFloat(res);

                        return StoreBinaryOpFormat(s, m, FloatAsUInt64(res));
                    }

                default: Fail(ErrorCode.UndefinedBehavior); return false;
            }
        }
        private bool ProcessCOS()
        {
            UInt64 s = 0, m = 0, a = 0;
            if (!FetchUnaryOpFormat(ref s, ref m, ref a)) return false;

            switch ((s >> 2) & 3)
            {
                case 3:
                    {
                        double res = Math.Cos(AsDouble(a));

                        UpdateFlagsDouble(res);

                        return StoreBinaryOpFormat(s, m, DoubleAsUInt64(res));
                    }
                case 2:
                    {
                        float res = (float)Math.Cos(AsFloat(a));

                        UpdateFlagsFloat(res);

                        return StoreBinaryOpFormat(s, m, FloatAsUInt64(res));
                    }

                default: Fail(ErrorCode.UndefinedBehavior); return false;
            }
        }
        private bool ProcessTAN()
        {
            UInt64 s = 0, m = 0, a = 0;
            if (!FetchUnaryOpFormat(ref s, ref m, ref a)) return false;

            switch ((s >> 2) & 3)
            {
                case 3:
                    {
                        double res = Math.Tan(AsDouble(a));

                        UpdateFlagsDouble(res);

                        return StoreBinaryOpFormat(s, m, DoubleAsUInt64(res));
                    }
                case 2:
                    {
                        float res = (float)Math.Tan(AsFloat(a));

                        UpdateFlagsFloat(res);

                        return StoreBinaryOpFormat(s, m, FloatAsUInt64(res));
                    }

                default: Fail(ErrorCode.UndefinedBehavior); return false;
            }
        }

        private bool ProcessSINH()
        {
            UInt64 s = 0, m = 0, a = 0;
            if (!FetchUnaryOpFormat(ref s, ref m, ref a)) return false;

            switch ((s >> 2) & 3)
            {
                case 3:
                    {
                        double res = Math.Sinh(AsDouble(a));

                        UpdateFlagsDouble(res);

                        return StoreBinaryOpFormat(s, m, DoubleAsUInt64(res));
                    }
                case 2:
                    {
                        float res = (float)Math.Sinh(AsFloat(a));

                        UpdateFlagsFloat(res);

                        return StoreBinaryOpFormat(s, m, FloatAsUInt64(res));
                    }

                default: Fail(ErrorCode.UndefinedBehavior); return false;
            }
        }
        private bool ProcessCOSH()
        {
            UInt64 s = 0, m = 0, a = 0;
            if (!FetchUnaryOpFormat(ref s, ref m, ref a)) return false;

            switch ((s >> 2) & 3)
            {
                case 3:
                    {
                        double res = Math.Cosh(AsDouble(a));

                        UpdateFlagsDouble(res);

                        return StoreBinaryOpFormat(s, m, DoubleAsUInt64(res));
                    }
                case 2:
                    {
                        float res = (float)Math.Cosh(AsFloat(a));

                        UpdateFlagsFloat(res);

                        return StoreBinaryOpFormat(s, m, FloatAsUInt64(res));
                    }

                default: Fail(ErrorCode.UndefinedBehavior); return false;
            }
        }
        private bool ProcessTANH()
        {
            UInt64 s = 0, m = 0, a = 0;
            if (!FetchUnaryOpFormat(ref s, ref m, ref a)) return false;

            switch ((s >> 2) & 3)
            {
                case 3:
                    {
                        double res = Math.Tanh(AsDouble(a));

                        UpdateFlagsDouble(res);

                        return StoreBinaryOpFormat(s, m, DoubleAsUInt64(res));
                    }
                case 2:
                    {
                        float res = (float)Math.Tanh(AsFloat(a));

                        UpdateFlagsFloat(res);

                        return StoreBinaryOpFormat(s, m, FloatAsUInt64(res));
                    }

                default: Fail(ErrorCode.UndefinedBehavior); return false;
            }
        }

        private bool ProcessASIN()
        {
            UInt64 s = 0, m = 0, a = 0;
            if (!FetchUnaryOpFormat(ref s, ref m, ref a)) return false;

            switch ((s >> 2) & 3)
            {
                case 3:
                    {
                        double res = Math.Asin(AsDouble(a));

                        UpdateFlagsDouble(res);

                        return StoreBinaryOpFormat(s, m, DoubleAsUInt64(res));
                    }
                case 2:
                    {
                        float res = (float)Math.Asin(AsFloat(a));

                        UpdateFlagsFloat(res);

                        return StoreBinaryOpFormat(s, m, FloatAsUInt64(res));
                    }

                default: Fail(ErrorCode.UndefinedBehavior); return false;
            }
        }
        private bool ProcessACOS()
        {
            UInt64 s = 0, m = 0, a = 0;
            if (!FetchUnaryOpFormat(ref s, ref m, ref a)) return false;

            switch ((s >> 2) & 3)
            {
                case 3:
                    {
                        double res = Math.Acos(AsDouble(a));

                        UpdateFlagsDouble(res);

                        return StoreBinaryOpFormat(s, m, DoubleAsUInt64(res));
                    }
                case 2:
                    {
                        float res = (float)Math.Acos(AsFloat(a));

                        UpdateFlagsFloat(res);

                        return StoreBinaryOpFormat(s, m, FloatAsUInt64(res));
                    }

                default: Fail(ErrorCode.UndefinedBehavior); return false;
            }
        }
        private bool ProcessATAN()
        {
            UInt64 s = 0, m = 0, a = 0;
            if (!FetchUnaryOpFormat(ref s, ref m, ref a)) return false;

            switch ((s >> 2) & 3)
            {
                case 3:
                    {
                        double res = Math.Atan(AsDouble(a));

                        UpdateFlagsDouble(res);

                        return StoreBinaryOpFormat(s, m, DoubleAsUInt64(res));
                    }
                case 2:
                    {
                        float res = (float)Math.Atan(AsFloat(a));

                        UpdateFlagsFloat(res);

                        return StoreBinaryOpFormat(s, m, FloatAsUInt64(res));
                    }

                default: Fail(ErrorCode.UndefinedBehavior); return false;
            }
        }
        private bool ProcessATAN2()
        {
            UInt64 s = 0, m = 0, a = 0, b = 0;
            if (!FetchBinaryOpFormat(ref s, ref m, ref a, ref b)) return false;

            switch ((s >> 2) & 3)
            {
                case 3:
                    {
                        double res = Math.Atan2(AsDouble(a), AsDouble(b));

                        UpdateFlagsDouble(res);

                        return StoreBinaryOpFormat(s, m, DoubleAsUInt64(res));
                    }
                case 2:
                    {
                        float res = (float)Math.Atan2(AsFloat(a), AsFloat(b));

                        UpdateFlagsFloat(res);

                        return StoreBinaryOpFormat(s, m, FloatAsUInt64(res));
                    }

                default: Fail(ErrorCode.UndefinedBehavior); return false;
            }
        }

        private bool ProcessFLOOR()
        {
            UInt64 s = 0, m = 0, a = 0;
            if (!FetchUnaryOpFormat(ref s, ref m, ref a)) return false;

            switch ((s >> 2) & 3)
            {
                case 3:
                    {
                        double res = Math.Floor(AsDouble(a));

                        UpdateFlagsDouble(res);

                        return StoreBinaryOpFormat(s, m, DoubleAsUInt64(res));
                    }
                case 2:
                    {
                        float res = (float)Math.Floor(AsFloat(a));

                        UpdateFlagsFloat(res);

                        return StoreBinaryOpFormat(s, m, FloatAsUInt64(res));
                    }

                default: Fail(ErrorCode.UndefinedBehavior); return false;
            }
        }
        private bool ProcessCEIL()
        {
            UInt64 s = 0, m = 0, a = 0;
            if (!FetchUnaryOpFormat(ref s, ref m, ref a)) return false;

            switch ((s >> 2) & 3)
            {
                case 3:
                    {
                        double res = Math.Ceiling(AsDouble(a));

                        UpdateFlagsDouble(res);

                        return StoreBinaryOpFormat(s, m, DoubleAsUInt64(res));
                    }
                case 2:
                    {
                        float res = (float)Math.Ceiling(AsFloat(a));

                        UpdateFlagsFloat(res);

                        return StoreBinaryOpFormat(s, m, FloatAsUInt64(res));
                    }

                default: Fail(ErrorCode.UndefinedBehavior); return false;
            }
        }
        private bool ProcessROUND()
        {
            UInt64 s = 0, m = 0, a = 0;
            if (!FetchUnaryOpFormat(ref s, ref m, ref a)) return false;

            switch ((s >> 2) & 3)
            {
                case 3:
                    {
                        double res = Math.Round(AsDouble(a));

                        UpdateFlagsDouble(res);

                        return StoreBinaryOpFormat(s, m, DoubleAsUInt64(res));
                    }
                case 2:
                    {
                        float res = (float)Math.Round(AsFloat(a));

                        UpdateFlagsFloat(res);

                        return StoreBinaryOpFormat(s, m, FloatAsUInt64(res));
                    }

                default: Fail(ErrorCode.UndefinedBehavior); return false;
            }
        }
        private bool ProcessTRUNC()
        {
            UInt64 s = 0, m = 0, a = 0;
            if (!FetchUnaryOpFormat(ref s, ref m, ref a)) return false;

            switch ((s >> 2) & 3)
            {
                case 3:
                    {
                        double res = Math.Truncate(AsDouble(a));

                        UpdateFlagsDouble(res);

                        return StoreBinaryOpFormat(s, m, DoubleAsUInt64(res));
                    }
                case 2:
                    {
                        float res = (float)Math.Truncate(AsFloat(a));

                        UpdateFlagsFloat(res);

                        return StoreBinaryOpFormat(s, m, FloatAsUInt64(res));
                    }

                default: Fail(ErrorCode.UndefinedBehavior); return false;
            }
        }

        private bool ProcessFTOI()
        {
            UInt64 s = 0, m = 0, a = 0;
            if (!FetchUnaryOpFormat(ref s, ref m, ref a)) return false;

            switch ((s >> 2) & 3)
            {
                case 3:
                    {
                        return StoreBinaryOpFormat(s, m, ((Int64)AsDouble(a)).MakeUnsigned());
                    }
                case 2:
                    {
                        return StoreBinaryOpFormat(s, m, ((Int64)AsFloat(a)).MakeUnsigned());
                    }

                default: Fail(ErrorCode.UndefinedBehavior); return false;
            }
        }
        private bool ProcessITOF()
        {
            UInt64 s = 0, m = 0, a = 0;
            if (!FetchUnaryOpFormat(ref s, ref m, ref a)) return false;

            switch ((s >> 2) & 3)
            {
                case 3:
                    {
                        return StoreBinaryOpFormat(s, m, DoubleAsUInt64(a.MakeSigned()));
                    }
                case 2:
                    {
                        return StoreBinaryOpFormat(s, m, FloatAsUInt64(SignExtend(a, 2).MakeSigned()));
                    }

                default: Fail(ErrorCode.UndefinedBehavior); return false;
            }
        }

        // -- extended register ops --

        private bool ProcessUMUL()
        {
            UInt64 a = 0, b = 0;

            if (!GetMemAdv(1, ref a)) return false;

            // get the value into b
            switch (a & 3)
            {
                case 0: if (!GetMemAdv(Size((a >> 2) & 3), ref b)) return false; break;
                case 1: b = Registers[a >> 4].Get((a >> 2) & 3); break;
                case 2: if (!GetAddressAdv(ref b) || !GetMem(b, Size((a >> 2) & 3), ref b)) return false; break;
                default: Fail(ErrorCode.UndefinedBehavior); return false;
            }

            // switch through register sizes
            switch ((a >> 2) & 3)
            {
                case 0:
                    Registers[0].x16 = Registers[0].x8 * b;
                    Flags.C = Flags.O = (Registers[0].x16 >> 8) != 0;
                    break;
                case 1:
                    Registers[0].x32 = Registers[0].x16 * b;
                    Flags.C = Flags.O = (Registers[0].x32 >> 16) != 0;
                    break;
                case 2:
                    Registers[0].x64 = Registers[0].x32 * b;
                    Flags.C = Flags.O = (Registers[0].x64 >> 32) != 0;
                    break;
                case 3: // 64 bits requires extra logic
                    BigInteger full = new BigInteger(Registers[0].x64) * new BigInteger(b);
                    Registers[0].x64 = (UInt64)(full & 0xffffffffffffffff);
                    Registers[1].x64 = (UInt64)(full >> 64);
                    Flags.C = Flags.O = Registers[1].x64 != 0;
                    break;
            }

            return true;
        }
        private bool ProcessSMUL()
        {
            UInt64 a = 0, b = 0;

            if (!GetMemAdv(1, ref a)) return false;

            // get the value into b
            switch (a & 3)
            {
                case 0: if (!GetMemAdv(Size((a >> 2) & 3), ref b)) return false; break;
                case 1: b = Registers[a >> 4].Get((a >> 2) & 3); break;
                case 2: if (!GetAddressAdv(ref b) || !GetMem(b, Size((a >> 2) & 3), ref b)) return false; break;
                default: Fail(ErrorCode.UndefinedBehavior); return false;
            }

            // switch through register sizes
            switch ((a >> 2) & 3)
            {
                case 0:
                    Registers[0].x16 = (SignExtend(Registers[0].x8, 0).MakeSigned() * SignExtend(b, 0).MakeSigned()).MakeUnsigned();
                    Flags.C = Flags.O = (Registers[0].x16 >> 8) == 0 && Positive(Registers[0].x8, 0) || (Registers[0].x16 >> 8) == 0xff && Negative(Registers[0].x8, 0);
                    Flags.S = Negative(Registers[0].x16, 1);
                    break;
                case 1:
                    Registers[0].x32 = (SignExtend(Registers[0].x16, 1).MakeSigned() * SignExtend(b, 1).MakeSigned()).MakeUnsigned();
                    Flags.C = Flags.O = (Registers[0].x32 >> 16) == 0 && Positive(Registers[0].x16, 1) || (Registers[0].x32 >> 16) == 0xffff && Negative(Registers[0].x16, 1);
                    Flags.S = Negative(Registers[0].x32, 2);
                    break;
                case 2:
                    Registers[0].x64 = (SignExtend(Registers[0].x32, 2).MakeSigned() * SignExtend(b, 2).MakeSigned()).MakeUnsigned();
                    Flags.C = Flags.O = (Registers[0].x64 >> 32) == 0 && Positive(Registers[0].x32, 2) || (Registers[0].x64 >> 32) == 0xffffffff && Negative(Registers[0].x32, 2);
                    Flags.S = Negative(Registers[0].x64, 3);
                    break;
                case 3: // 64 bits requires extra logic
                    // store negative flag (we'll do the multiplication in signed values since bit shifting is well-defined for positive BigInteger)
                    bool neg = false;
                    if (Negative(Registers[0].x64, 3)) { neg = !neg; Registers[0].x64 = ~Registers[0].x64 + 1; }
                    if (Negative(b, 3)) { neg = !neg; b = ~b + 1; }

                    // form the full (positive) product
                    BigInteger full = new BigInteger(Registers[0].x64) * new BigInteger(b);
                    Registers[0].x64 = (UInt64)(full & 0xffffffffffffffff);
                    Registers[1].x64 = (UInt64)(full >> 64);

                    // if it should be negative, apply that change now
                    if (neg)
                    {
                        Registers[0].x64 = ~Registers[0].x64 + 1;
                        Registers[1].x64 = ~Registers[1].x64;

                        // account for carry from low 64 bits
                        if (Registers[0].x64 == 0) ++Registers[1].x64;
                    }
                    Flags.C = Flags.O = Registers[1].x64 == 0 && Positive(Registers[0].x64, 3) || Registers[1].x64 == 0xffffffffffffffff && Negative(Registers[0].x64, 3);
                    Flags.S = Negative(Registers[1].x64, 3);
                    break;
            }

            return true;
        }

        private bool ProcessUDIV()
        {
            UInt64 a = 0, b = 0, full;
            BigInteger bigraw, bigfull;

            if (!GetMemAdv(1, ref a)) return false;

            // get the value into b
            switch (a & 3)
            {
                case 0: if (!GetMemAdv(Size((a >> 2) & 3), ref b)) return false; break;
                case 1: b = Registers[a >> 4].Get((a >> 2) & 3); break;
                case 2: if (!GetAddressAdv(ref b) || !GetMem(b, Size((a >> 2) & 3), ref b)) return false; break;
                default: Fail(ErrorCode.UndefinedBehavior); return false;
            }

            if (b == 0) { Fail(ErrorCode.ArithmeticError); return false; }

            // switch through register sizes
            switch ((a >> 2) & 3)
            {
                case 0:
                    full = Registers[0].x16 / b;
                    if ((full >> 8) != 0) { Fail(ErrorCode.ArithmeticError); return false; }
                    Registers[1].x8 = Registers[0].x16 % b;
                    Registers[0].x8 = full;
                    Flags.C = Registers[1].x8 != 0;
                    break;
                case 1:
                    full = Registers[0].x32 / b;
                    if ((full >> 16) != 0) { Fail(ErrorCode.ArithmeticError); return false; }
                    Registers[1].x16 = Registers[0].x32 % b;
                    Registers[0].x16 = full;
                    Flags.C = Registers[1].x16 != 0;
                    break;
                case 2:
                    full = Registers[0].x64 / b;
                    if ((full >> 32) != 0) { Fail(ErrorCode.ArithmeticError); return false; }
                    Registers[1].x32 = Registers[0].x64 % b;
                    Registers[0].x32 = full;
                    Flags.C = Registers[1].x32 != 0;
                    break;
                case 3: // 64 bits requires extra logic
                    bigraw = (new BigInteger(Registers[1].x64) << 64) | new BigInteger(Registers[0].x64);
                    bigfull = bigraw / new BigInteger(b);

                    if ((bigfull >> 64) != 0) { Fail(ErrorCode.ArithmeticError); return false; }

                    Registers[1].x64 = (UInt64)(bigraw % new BigInteger(b));
                    Registers[0].x64 = (UInt64)bigfull;
                    Flags.C = Registers[1].x64 != 0;
                    break;
            }

            return true;
        }
        private bool ProcessSDIV()
        {
            UInt64 a = 0, b = 0;
            Int64 _a, _b, full;
            BigInteger bigraw, bigfull;

            if (!GetMemAdv(1, ref a)) return false;

            // get the value into b
            switch (a & 3)
            {
                case 0: if (!GetMemAdv(Size((a >> 2) & 3), ref b)) return false; break;
                case 1: b = Registers[a >> 4].Get((a >> 2) & 3); break;
                case 2: if (!GetAddressAdv(ref b) || !GetMem(b, Size((a >> 2) & 3), ref b)) return false; break;
                default: Fail(ErrorCode.UndefinedBehavior); return false;
            }

            if (b == 0) { Fail(ErrorCode.ArithmeticError); return false; }

            // switch through register sizes
            switch ((a >> 2) & 3)
            {
                case 0:
                    _a = SignExtend(Registers[0].x16, 1).MakeSigned();
                    _b = SignExtend(b, 0).MakeSigned();
                    full = _a / _b;

                    if (full != (sbyte)full) { Fail(ErrorCode.ArithmeticError); return false; }

                    Registers[0].x8 = full.MakeUnsigned();
                    Registers[1].x8 = (_a % _b).MakeUnsigned();
                    Flags.C = Registers[1].x8 != 0;
                    Flags.S = Negative(Registers[0].x8, 0);
                    break;
                case 1:
                    _a = SignExtend(Registers[0].x32, 2).MakeSigned();
                    _b = SignExtend(b, 1).MakeSigned();
                    full = _a / _b;

                    if (full != (Int16)full) { Fail(ErrorCode.ArithmeticError); return false; }

                    Registers[0].x16 = full.MakeUnsigned();
                    Registers[1].x16 = (_a % _b).MakeUnsigned();
                    Flags.C = Registers[1].x16 != 0;
                    Flags.S = Negative(Registers[0].x16, 1);
                    break;
                case 2:
                    _a = Registers[0].x64.MakeSigned();
                    _b = SignExtend(b, 2).MakeSigned();
                    full = _a / _b;

                    if (full != (Int32)full) { Fail(ErrorCode.ArithmeticError); return false; }

                    Registers[0].x32 = full.MakeUnsigned();
                    Registers[1].x32 = (_a % _b).MakeUnsigned();
                    Flags.C = Registers[1].x32 != 0;
                    Flags.S = Negative(Registers[0].x32, 2);
                    break;
                case 3: // 64 bits requires extra logic
                    _b = b.MakeSigned();
                    bigraw = (new BigInteger(Registers[1].x64.MakeSigned()) << 64) + new BigInteger(Registers[0].x64.MakeSigned());
                    bigfull = bigraw / _b;

                    if (bigfull != (Int64)bigfull) { Fail(ErrorCode.ArithmeticError); return false; }

                    Registers[1].x64 = ((Int64)(bigraw % _b)).MakeUnsigned();
                    Registers[0].x64 = ((Int64)bigfull).MakeUnsigned();
                    Flags.C = Registers[1].x64 != 0;
                    break;
            }

            return true;
        }

        // -- misc operations --

        private bool ProcessSWAP()
        {
            UInt64 a = 0, b = 0, c = 0, d = 0;

            if (!GetMemAdv(1, ref a)) return false;
            switch (a & 1)
            {
                case 0:
                    if (!GetMemAdv(1, ref b)) return false;
                    c = Registers[a >> 4].x64;
                    Registers[a >> 4].Set((a >> 2) & 3, Registers[b & 15].x64);
                    Registers[b & 15].Set((a >> 2) & 3, c);
                    break;
                case 1:
                    if (!GetAddressAdv(ref b) || !GetMem(b, Size((a >> 2) & 3), ref c)) return false;
                    d = Registers[a >> 4].x64;
                    Registers[a >> 4].Set((a >> 2) & 3, c);
                    if (!SetMem(b, Size((a >> 2) & 3), d)) return false;
                    break;
            }

            return true;
        }

        private bool ProcessBSWAP()
        {
            UInt64 s = 0, m = 0, a = 0;
            if (!FetchUnaryOpFormat(ref s, ref m, ref a)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UInt64 res = 0;
            switch (sizecode)
            {
                case 3: res = (a << 56) | ((a & 0x000000000000ff00) << 40) | ((a & 0x0000000000ff0000) << 24) | ((a & 0x00000000ff000000) << 8)
                        | ((a & 0x000000ff00000000) >> 8) | ((a & 0x0000ff0000000000) >> 24) | ((a & 0x00ff000000000000) >> 40) | (a >> 56); break;
                case 2: res = (a << 24) | ((a & 0x0000ff00) << 8) | ((a & 0x00ff0000) >> 8) | (a >> 24); break;
                case 1: res = (a << 8) | (a >> 8); break;
                case 0: res = a; break;
            }

            return StoreUnaryOpFormat(s, m, res);
        }
        private bool ProcessBEXTR()
        {
            UInt64 s = 0, m = 0, a = 0, b = 0;
            if (!FetchBinaryOpFormat(ref s, ref m, ref a, ref b, -1, 1)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            ushort pos = (ushort)((b >> 8) % SizeBits(sizecode));
            ushort len = (ushort)((b & 0xff) % SizeBits(sizecode));

            UInt64 res = (a >> pos) & ((1ul << len) - 1);

            return StoreUnaryOpFormat(s, m, res);
        }
        private bool ProcessBLSI()
        {
            UInt64 s = 0, m = 0, a = 0;
            if (!FetchUnaryOpFormat(ref s, ref m, ref a)) return false;

            UInt64 res = a & (~a + 1);

            Flags.Z = res == 0;

            return StoreUnaryOpFormat(s, m, res);
        }
        private bool ProcessBLSMSK()
        {
            UInt64 s = 0, m = 0, a = 0;
            if (!FetchUnaryOpFormat(ref s, ref m, ref a)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UInt64 res = Truncate(a ^ (a - 1), sizecode);

            return StoreUnaryOpFormat(s, m, res);
        }
        private bool ProcessBLSR()
        {
            UInt64 s = 0, m = 0, a = 0;
            if (!FetchUnaryOpFormat(ref s, ref m, ref a)) return false;

            UInt64 res = a & (a - 1);

            Flags.Z = res == 0;

            return StoreUnaryOpFormat(s, m, res);
        }
        private bool ProcessANDN()
        {
            UInt64 s = 0, m = 0, a = 0, b = 0;
            if (!FetchBinaryOpFormat(ref s, ref m, ref a, ref b)) return false;
            UInt64 sizecode = (a >> 2) & 3;

            UInt64 res = a & ~b;

            UpdateFlagsI(res, sizecode);

            return StoreUnaryOpFormat(s, m, res);
        }

        #endregion

        // ----------------------
        // -- Public Interface --
        // ----------------------

        /// <summary>
        /// Validates the machine for operation, but does not prepare it for execute (see Initialize)
        /// </summary>
        public CSX64()
        {
            // allocate registers
            for (int i = 0; i < Registers.Length; ++i) Registers[i] = new Register();

            Running = false;
            Error = ErrorCode.None;
        }

        /// <summary>
        /// Initializes the computer for execution. Returns true if successful (fails on insufficient memory)
        /// </summary>
        /// <param name="data">The memory to load before starting execution (extra memory is undefined)</param>
        public bool Initialize(byte[] data, UInt64 stacksize = 1024)
        {
            // make sure we're not loading null array
            if (data == null || data.LongLength == 0) return false;

            // get new memory array
            Memory = new byte[(UInt64)data.LongLength + stacksize];

            // copy over the data
            data.CopyTo(Memory, 0L);

            // randomize registers except stack register
            for (int i = Registers.Length - 2; i >= 0; --i)
            {
                Registers[i].x32 = (UInt64)Rand.Next();
                Registers[i].x64 <<= 32;
                Registers[i].x32 = (UInt64)Rand.Next();
            }
            // randomize flags
            Flags.Flags = (uint)Rand.Next();
            // initialize stack register to end of memory segment
            Registers[15].x64 = (UInt64)Memory.LongLength;

            // set execution state
            Pos = 0;
            Running = true;
            Error = ErrorCode.None;

            return true;
        }

        /// <summary>
        /// Causes the machine to fail
        /// </summary>
        /// <param name="code">The error code to emit</param>
        public void Fail(ErrorCode code)
        {
            if (Running)
            {
                Error = code;
                Running = false;
            }
        }

        /// <summary>
        /// Gets the specified register in this computer (no bounds checking: test index against NRegisters)
        /// </summary>
        /// <param name="index">The index of the register</param>
        public Register GetRegister(UInt64 index) => Registers[index];
        /// <summary>
        /// Returns the flags register
        /// </summary>
        public FlagsRegister GetFlags() => Flags;

        /// <summary>
        /// Reads a value from memory (fails with OutOfBounds if invalid)
        /// </summary>
        /// <param name="pos">Address to read</param>
        /// <param name="size">Number of bytes to read</param>
        /// <param name="res">The result</param>
        public bool GetMem(UInt64 pos, UInt64 size, ref UInt64 res)
        {
            if (Read(Memory, pos, size, ref res)) return true;

            Fail(ErrorCode.OutOfBounds); return false;
        }
        /// <summary>
        /// Writes a value to memory (fails with OutOfBounds if invalid)
        /// </summary>
        /// <param name="pos">Address to write</param>
        /// <param name="size">Number of bytes to write</param>
        /// <param name="val">The value to write</param>
        public bool SetMem(UInt64 pos, UInt64 size, UInt64 val)
        {
            if (Write(Memory, pos, size, val)) return true;

            Fail(ErrorCode.OutOfBounds); return false;
        }

        /// <summary>
        /// Gets a value at and advances the execution pointer (fails with OutOfBounds if invalid)
        /// </summary>
        /// <param name="size">Number of bytes to read</param>
        /// <param name="res">The result</param>
        private bool GetMemAdv(UInt64 size, ref UInt64 res)
        {
            bool r = GetMem(Pos, size, ref res);
            Pos += size;

            return r;
        }
        // [1: literal][3: m1][1: -m2][3: m2]   ([4: r1][4: r2])   ([64: imm])
        /// <summary>
        /// Gets an address and advances the execution pointer
        /// </summary>
        /// <param name="res">resulting address</param>
        private bool GetAddressAdv(ref UInt64 res)
        {
            UInt64 mults = 0, regs = 0, imm = 0; // the mult codes, regs, and literal (mults and regs only initialized for compiler, but literal must be initialized to 0)

            // parse the address
            if (!GetMemAdv(1, ref mults) || (mults & 0x77) != 0 && !GetMemAdv(1, ref regs) || (mults & 0x80) != 0 && !GetMemAdv(8, ref imm)) return false;

            // compute the result into res
            res = MultCode((mults >> 4) & 7) * Registers[regs >> 4].x64 + MultCode(mults & 7, mults & 8) * Registers[regs & 15].x64 + imm;

            // got an address
            return true;
        }

        /// <summary>
        /// Pushes a value onto the stack
        /// </summary>
        /// <param name="size">the size of the value (in bytes)</param>
        /// <param name="val">the value to push</param>
        protected bool Push(UInt64 size, UInt64 val)
        {
            Registers[15].x64 -= size;
            return SetMem(Registers[15].x64, size, val);
        }
        /// <summary>
        /// Pops a value from the stack
        /// </summary>
        /// <param name="size">the size of the value (in bytes)</param>
        /// <param name="val">the resulting value</param>
        protected bool Pop(UInt64 size, ref UInt64 val)
        {
            if (!GetMem(Registers[15].x64, size, ref val)) return false;
            Registers[15].x64 += size;
            return true;
        }

        /// <summary>
        /// Handles syscall instructions from the processor. Returns true iff the syscall was handled successfully.
        /// Should not be called directly: only by interpreted syscall instructions
        /// </summary>
        protected virtual bool Syscall() { return false; }

        /// <summary>
        /// Performs a single operation. Returns true if successful
        /// </summary>
        public bool Tick()
        {
            // fail to execute ins if terminated
            if (!Running) return false;

            UInt64 a = 0, b = 0, c = 0, d = 0; // the potential args (initialized for compiler)

            // fetch the instruction
            if (!GetMemAdv(1, ref a)) return false;

            // switch through the opcodes
            switch ((OPCode)a)
            {
                case OPCode.NOP: return true;
                case OPCode.STOP: Running = false; return true;
                case OPCode.SYSCALL: if (Syscall()) return true; Fail(ErrorCode.UnhandledSyscall); return false;

                case OPCode.MOV: return ProcessMov();

                case OPCode.MOVa: return ProcessMov(Flags.a);
                case OPCode.MOVae: return ProcessMov(Flags.ae);
                case OPCode.MOVb: return ProcessMov(Flags.b);
                case OPCode.MOVbe: return ProcessMov(Flags.be);

                case OPCode.MOVg: return ProcessMov(Flags.g);
                case OPCode.MOVge: return ProcessMov(Flags.ge);
                case OPCode.MOVl: return ProcessMov(Flags.l);
                case OPCode.MOVle: return ProcessMov(Flags.le);

                case OPCode.MOVz: return ProcessMov(Flags.Z);
                case OPCode.MOVnz: return ProcessMov(!Flags.Z);
                case OPCode.MOVs: return ProcessMov(Flags.S);
                case OPCode.MOVns: return ProcessMov(!Flags.S);
                case OPCode.MOVp: return ProcessMov(Flags.P);
                case OPCode.MOVnp: return ProcessMov(!Flags.P);
                case OPCode.MOVo: return ProcessMov(Flags.O);
                case OPCode.MOVno: return ProcessMov(!Flags.O);
                case OPCode.MOVc: return ProcessMov(Flags.C);
                case OPCode.MOVnc: return ProcessMov(!Flags.C);

                case OPCode.SWAP: return ProcessSWAP();

                case OPCode.UEXTEND: if (!GetMemAdv(1, ref a)) return false; Registers[a >> 4].Set(a & 3, Registers[a >> 4].Get((a >> 2) & 3)); return true;
                case OPCode.SEXTEND: if (!GetMemAdv(1, ref a)) return false; Registers[a >> 4].Set(a & 3, SignExtend(Registers[a >> 4].Get((a >> 2) & 3), (a >> 2) & 3)); return true;

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

                case OPCode.SL: return ProcessSL();
                case OPCode.SR: return ProcessSR();
                case OPCode.SAL: return ProcessSAL();
                case OPCode.SAR: return ProcessSAR();
                case OPCode.RL: return ProcessRL();
                case OPCode.RR: return ProcessRR();

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

                case OPCode.LA:
                    if (!GetMemAdv(1, ref a) || !GetAddressAdv(ref b)) return false;
                    Registers[a & 15].x64 = b;
                    return true;

                case OPCode.JMP: if (!GetAddressAdv(ref a)) return false; Pos = a; return true;

                case OPCode.Ja: if (!GetAddressAdv(ref a)) return false; if (Flags.a) Pos = a; return true;
                case OPCode.Jae: if (!GetAddressAdv(ref a)) return false; if (Flags.ae) Pos = a; return true;
                case OPCode.Jb: if (!GetAddressAdv(ref a)) return false; if (Flags.b) Pos = a; return true;
                case OPCode.Jbe: if (!GetAddressAdv(ref a)) return false; if (Flags.be) Pos = a; return true;

                case OPCode.Jg: if (!GetAddressAdv(ref a)) return false; if (Flags.g) Pos = a; return true;
                case OPCode.Jge: if (!GetAddressAdv(ref a)) return false; if (Flags.ge) Pos = a; return true;
                case OPCode.Jl: if (!GetAddressAdv(ref a)) return false; if (Flags.l) Pos = a; return true;
                case OPCode.Jle: if (!GetAddressAdv(ref a)) return false; if (Flags.le) Pos = a; return true;

                case OPCode.Jz: if (!GetAddressAdv(ref a)) return false; if (Flags.Z) Pos = a; return true;
                case OPCode.Jnz: if (!GetAddressAdv(ref a)) return false; if (!Flags.Z) Pos = a; return true;
                case OPCode.Js: if (!GetAddressAdv(ref a)) return false; if (Flags.S) Pos = a; return true;
                case OPCode.Jns: if (!GetAddressAdv(ref a)) return false; if (!Flags.S) Pos = a; return true;
                case OPCode.Jp: if (!GetAddressAdv(ref a)) return false; if (Flags.P) Pos = a; return true;
                case OPCode.Jnp: if (!GetAddressAdv(ref a)) return false; if (!Flags.P) Pos = a; return true;
                case OPCode.Jo: if (!GetAddressAdv(ref a)) return false; if (Flags.O) Pos = a; return true;
                case OPCode.Jno: if (!GetAddressAdv(ref a)) return false; if (!Flags.O) Pos = a; return true;
                case OPCode.Jc: if (!GetAddressAdv(ref a)) return false; if (Flags.C) Pos = a; return true;
                case OPCode.Jnc: if (!GetAddressAdv(ref a)) return false; if (!Flags.C) Pos = a; return true;

                case OPCode.FADD: return ProcessFADD();
                case OPCode.FSUB: return ProcessFSUB();
                case OPCode.FMUL: return ProcessFMUL();
                case OPCode.FDIV: return ProcessFDIV();
                case OPCode.FMOD: return ProcessFMOD();

                case OPCode.POW: return ProcessPOW();
                case OPCode.SQRT: return ProcessSQRT();
                case OPCode.EXP: return ProcessEXP();
                case OPCode.LN: return ProcessLN();
                case OPCode.FNEG: return ProcessFNEG();
                case OPCode.FABS: return ProcessFABS();
                case OPCode.FCMPZ: return ProcessFCMPZ();

                case OPCode.SIN: return ProcessSIN();
                case OPCode.COS: return ProcessCOS();
                case OPCode.TAN: return ProcessTAN();

                case OPCode.SINH: return ProcessSINH();
                case OPCode.COSH: return ProcessCOSH();
                case OPCode.TANH: return ProcessTANH();

                case OPCode.ASIN: return ProcessASIN();
                case OPCode.ACOS: return ProcessACOS();
                case OPCode.ATAN: return ProcessATAN();
                case OPCode.ATAN2: return ProcessATAN2();

                case OPCode.FLOOR: return ProcessFLOOR();
                case OPCode.CEIL: return ProcessCEIL();
                case OPCode.ROUND: return ProcessROUND();
                case OPCode.TRUNC: return ProcessTRUNC();

                case OPCode.FCMP: return ProcessFSUB(false);

                case OPCode.FTOI: return ProcessFTOI();
                case OPCode.ITOF: return ProcessITOF();

                case OPCode.PUSH:
                    if (!GetMemAdv(1, ref a)) return false;
                    switch (a & 1)
                    {
                        case 0: if (!GetMemAdv(Size((a >> 2) & 3), ref b)) return false; break;
                        case 1: b = Registers[a >> 4].x64; break;
                    }
                    return Push(Size((a >> 2) & 3), b);
                case OPCode.POP:
                    if (!GetMemAdv(1, ref a) || !Pop(Size((a >> 2) & 3), ref b)) return false;
                    Registers[a >> 4].Set((a >> 2) & 3, b);
                    return true;
                case OPCode.CALL:
                    if (!GetAddressAdv(ref a) || !Push(8, Pos)) return false;
                    Pos = a; return true;
                case OPCode.RET:
                    if (!Pop(8, ref a)) return false;
                    Pos = a; return true;

                case OPCode.BSWAP: return ProcessBSWAP();
                case OPCode.BEXTR: return ProcessBEXTR();
                case OPCode.BLSI: return ProcessBLSI();
                case OPCode.BLSMSK: return ProcessBLSMSK();
                case OPCode.BLSR: return ProcessBLSR();
                case OPCode.ANDN: return ProcessANDN();

                case OPCode.GETF: if (!GetMemAdv(1, ref a)) return false; Registers[a & 15].x64 = Flags.Flags; return true;
                case OPCode.SETF: if (!GetMemAdv(1, ref a)) return false; Flags.Flags = Registers[a & 15].x64; return true;

                case OPCode.LOOP:
                    if (!GetMemAdv(1, ref a) || !GetAddressAdv(ref b)) return false;
                    c = Registers[a >> 4].Get((a >> 2) & 3);
                    c = c - Size(a & 3); // since we know we're subtracting a positive value and only comparing to zero, no need to truncate
                    Registers[a >> 4].Set((a >> 2) & 3, c);
                    if (c != 0) Pos = b;
                    return true;

                // otherwise, unknown opcode
                default: Fail(ErrorCode.UndefinedBehavior); return false;
            }
        }

        // --------------
        // -- Assembly --
        // -------------- 

        public enum AssembleError
        {
            None, ArgCount, MissingSize, ArgError, FormatError, UsageError, UnknownOp, EmptyFile, InvalidLabel, SymbolRedefinition, UnknownSymbol
        }
        public enum LinkError
        {
            None, EmptyResult, SymbolRedefinition, MissingSymbol, FormatError
        }

        internal struct Symbol
        {
            public UInt64 Value;
            public bool IsAddress;
            public bool IsFloating;
        }
        internal class Hole
        {
            public struct Segment
            {
                public string Symbol;
                public bool IsNegative;
            }

            // -------------------

            public UInt64 Value = 0;
            public double FValue = 0;
            public bool Floating = false;

            public List<Segment> Segments = new List<Segment>();

            // -------------------

            public bool Append(AssembleArgs args, string sub)
            {
                string _sub = sub.Length > 0 && sub[0] == '+' || sub[0] == '-' ? sub.Substring(1) : sub;
                if (_sub.Length == 0) { args.err = new Tuple<AssembleError, string>(AssembleError.InvalidLabel, $"line {args.line}: Empty label encountered"); return false; }

                // if we can get a value for it, add it
                if (TryParseInstantImm(args, sub, out UInt64 temp, out bool floating))
                {
                    // add location depends on floating or not
                    if (floating) { Floating = true; FValue += AsDouble(temp); }
                    else Value += temp;
                }
                // account for the __pos__ symbol (due to being an address, it's value is meaningless until link time)
                else if (_sub == "__pos__")
                {
                    // get the position symbol
                    Symbol __pos__ = args.file.Symbols["__pos__"];
                    // create a virtual label name to refer to current value of __pos__
                    string virt_label = $"{__pos__.Value:x16}";

                    // create the clone symbol
                    args.file.Symbols[virt_label] = __pos__;
                    // add virtual symbol to segments
                    Segments.Add(new Segment() { Symbol = virt_label, IsNegative = sub[0] == '-' });
                }
                // otherwise add a segment if it's a legal label
                else
                {
                    if (!MutateLabel(args, ref _sub)) return false;
                    if (IsValidLabel(_sub)) Segments.Add(new Segment() { Symbol = _sub, IsNegative = sub[0] == '-' });
                    else { args.err = new Tuple<AssembleError, string>(AssembleError.InvalidLabel, $"line {args.line}: \"{_sub}\" is not a valid label"); return false; }
                }

                return true;
            }
        }
        internal struct HoleData
        {
            public UInt64 Address;
            public UInt64 Size;
        }
        public class ObjectFile
        {
            internal Dictionary<string, Symbol> Symbols = new Dictionary<string, Symbol>();
            internal Dictionary<HoleData, Hole> Holes = new Dictionary<HoleData, Hole>();

            internal List<string> GlobalSymbols = new List<string>();
            internal List<byte> Data = new List<byte>();
        }

        internal class AssembleArgs
        {
            public ObjectFile file;
            public int line;

            public string rawline;
            public string[] label_defs; // must be array for ref params
            public string op;
            public UInt64 sizecode;
            public string[] args;       // must be array for ref params

            public string last_static_label;
            public Tuple<AssembleError, string> err;
        }

        /// <summary>
        /// The maximum value for an emission multiplier
        /// </summary>
        public const UInt64 EmissionMaxMultiplier = 1000000;

        private static bool Write(byte[] arr, UInt64 pos, UInt64 size, UInt64 val)
        {
            // make sure we're not exceeding memory bounds
            if (pos < 0 || pos + size > (UInt64)arr.LongLength) return false;

            // write the value (little-endian)
            for (ushort i = 0; i < size; ++i)
                arr[pos + i] = (byte)(val >> (8 * i));

            return true;
        }
        private static bool Read(byte[] arr, UInt64 pos, UInt64 size, ref UInt64 res)
        {
            // make sure we're not exceeding memory bounds
            if (pos < 0 || pos + size > (UInt64)arr.LongLength) return false;

            // read the value (little-endian)
            res = 0;
            for (ushort i = 0; i < size; ++i)
                res |= (UInt64)arr[pos + i] << (8 * i);

            return true;
        }

        private static void AppendVal(AssembleArgs args, UInt64 size, UInt64 val)
        {
            // write the value (little-endian)
            for (ushort i = 0; i < size; ++i)
                args.file.Data.Add((byte)(val >> (8 * i)));
        }
        private static bool AppendHole(AssembleArgs args, UInt64 size, Hole hole)
        {
            // if we can fill it immediately, do so
            if (hole.Segments.Count == 0)
            {
                /* !! IF MODIFIED HERE, MUST ALSO BE MODIFIED IN LINKER HOLE PATCHING !! */

                // if it's floating-point
                if (hole.Floating)
                {
                    // only 64-bit and 32-bit are supported
                    switch (size)
                    {
                        case 8: AppendVal(args, 8, DoubleAsUInt64(hole.Value.MakeSigned() + hole.FValue)); break;
                        case 4: AppendVal(args, 4, FloatAsUInt64(hole.Value.MakeSigned() + (float)hole.FValue)); break;

                        default: args.err = new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {args.line}: Attempt to use unsupported floating-point size"); return false;
                    }
                }
                // otherwise it's integral
                else AppendVal(args, size, hole.Value);
            }
            // otherwise there really is a hole
            else
            {
                // add the hole for later linking
                args.file.Holes.Add(new HoleData() { Address = (UInt64)args.file.Data.LongCount(), Size = size }, hole);
                // write a dummy (all 1's for easy manual identification)
                AppendVal(args, size, 0xffffffffffffffff);
            }

            return true;
        }
        private static bool AppendAddress(AssembleArgs args, UInt64 a, UInt64 b, Hole hole)
        {
            // [1: literal][3: m1][1: -m2][3: m2]   ([4: r1][4: r2])   ([64: imm])
            AppendVal(args, 1, a);
            if ((a & 0x77) != 0) AppendVal(args, 1, b);
            if ((a & 0x80) != 0) { if (!AppendHole(args, 8, hole)) { args.err = new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {args.line}: Failed to append address base\n-> {args.err.Item2}"); return false; } }

            return true;
        }

        private static bool SplitLine(AssembleArgs args)
        {
            // (label: label: ...) op(:size) (arg, arg, ...)
            int pos, end = 0; // position in line parsing
            int quote;
            string token;

            string[] subs;
            List<string> tokens = new List<string>();
            StringBuilder b = new StringBuilder();

            // parse label defs and op
            while (true)
            {
                // skip leading white space
                for (pos = end; pos < args.rawline.Length && char.IsWhiteSpace(args.rawline[pos]); ++pos) ;
                // get a white space-delimited token
                for (end = pos; end < args.rawline.Length && !char.IsWhiteSpace(args.rawline[end]); ++end) ;
                token = args.rawline.Substring(pos, end - pos);

                // if it's a token, add it to the tokens list
                if (token.Length > 0 && token[token.Length - 1] == ':') tokens.Add(token.Substring(0, token.Length - 1));
                // otherwise, this is op
                else
                {
                    // split the op:size token
                    subs = token.Split(':');
                    args.op = subs[0]; // op is first arg regardless

                    // handle sizecode (2 = explicit size, 1 = default 64 bit, else is format error)
                    if (subs.Length == 2)
                    {
                        if (!TryParseSizecode(args, subs[1], out args.sizecode))
                        { args.err = new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {args.line}: Failed to parse register size code \"{subs[1]}\"\n-> {args.err.Item2}"); return false; }
                    }
                    else if (subs.Length == 1) args.sizecode = 3; // use default size
                    else { args.err = new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {args.line}: {subs.Length}Operation token may have at most one explicit size parameter"); return false; }

                    break;
                }
            }

            args.label_defs = tokens.ToArray(); // dump tokens as label defs
            tokens.Clear(); // empty tokens for reuse

            // parse the rest of the line as comma-separated tokens
            while (true)
            {
                // skip leading white space
                for (pos = end + 1; pos < args.rawline.Length && char.IsWhiteSpace(args.rawline[pos]); ++pos) ;
                // when pos reaches end of token, we're done parsing
                if (pos >= args.rawline.Length) break;

                b.Clear(); // clear the string builder

                // find the next terminator (comma-separated)
                for (quote = -1, end = pos; end < args.rawline.Length && (args.rawline[end] != ',' || quote >= 0); ++end)
                {
                    if (args.rawline[end] == '"' || args.rawline[end] == '\'')
                        quote = quote < 0 ? end : (args.rawline[end] == args.rawline[quote] ? -1 : quote);

                    // omit white space unless in a quote
                    if (quote >= 0 || !char.IsWhiteSpace(args.rawline[end])) b.Append(args.rawline[end]);
                }

                // add this token
                tokens.Add(b.ToString());
            }
            // output tokens to assemble args
            args.args = tokens.ToArray();

            // successfully parsed line
            return true;
        }

        private static bool TryParseInstantImm(AssembleArgs args, string token, out UInt64 res, out bool floating)
        {
            int pos = 0, end = 0;   // position in token
            UInt64 temp = 0;        // placeholders for parsing
            double ftemp, fsum = 0; // floating point parsing temporary and sum

            // result initially integral zero
            res = 0;
            floating = false;

            if (token.Length == 0) { args.err = new Tuple<AssembleError, string>(AssembleError.InvalidLabel, $"line {args.line}: Empty label encountered"); return false; }

            while (pos < token.Length)
            {
                // find the next separator
                for (int i = pos + 1; i < token.Length; ++i)
                    if (token[i] == '+' || token[i] == '-') { end = i; break; }
                // if nothing found, end is end of token
                if (pos == end) end = token.Length;

                // get subtoken to process
                string sub = token.Substring(pos, end - pos);
                string _sub = sub[0] == '+' || sub[0] == '-' ? sub.Substring(1) : sub;
                if (_sub.Length == 0) { args.err = new Tuple<AssembleError, string>(AssembleError.InvalidLabel, $"line {args.line}: Empty label encountered"); return false; }

                // if it's a numeric literal
                if (char.IsDigit(_sub[0]))
                {
                    // try several integral radix conversions
                    try
                    {
                        if (_sub.StartsWith("0x")) temp = Convert.ToUInt64(_sub.Substring(2), 16);
                        else if (_sub.StartsWith("0b")) temp = Convert.ToUInt64(_sub.Substring(2), 2);
                        else if (_sub[0] == '0' && _sub.Length > 1) temp = Convert.ToUInt64(_sub.Substring(1), 8);
                        else temp = Convert.ToUInt64(_sub, 10);

                        res += sub[0] == '-' ? ~temp + 1 : temp;
                        goto aft;
                    }
                    catch (Exception) { }

                    // if none of thise worked, try a floating point conversion
                    if (double.TryParse(sub, out ftemp)) { floating = true; fsum += ftemp; }
                    else { args.err = new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {args.line}: Unknown numeric literal encountered \"{_sub}\""); return false; }
                aft:;
                }
                // if it's a character
                else if (_sub[0] == '\'')
                {
                    if (_sub[_sub.Length - 1] != '\'') { args.err = new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {args.line}: Character literal must be enclosed in single quotes"); return false; }
                    if (_sub.Length != 3) { args.err = new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {args.line}: character literal must contain exactly one character"); return false; }

                    temp = _sub[1];
                    res += sub[0] == '-' ? ~temp + 1 : temp;
                }
                // if it's an instant symbol
                else
                {
                    if (!MutateLabel(args, ref _sub)) return false;
                    if (args.file.Symbols.TryGetValue(_sub, out Symbol symbol) && !symbol.IsAddress)
                    {
                        // add depending on floating or not
                        if (symbol.IsFloating) { floating = true; fsum += sub[0] == '-' ? -AsDouble(symbol.Value) : AsDouble(symbol.Value); }
                        else res += sub[0] == '-' ? ~symbol.Value + 1 : symbol.Value;
                    }
                    // otherwise it's a dud
                    else { args.err = new Tuple<AssembleError, string>(AssembleError.UnknownSymbol, $"line {args.line}: Undefined instant symbol encountered \"{_sub}\""); return false; }
                }

                // start of next token includes separator
                pos = end;
            }

            // if result is floating, recalculate res as sum of floating and integral components
            if (floating) res = DoubleAsUInt64(res + fsum);

            return true;
        }

        private static bool TryParseRegister(AssembleArgs args, string token, out UInt64 res)
        {
            res = 0;
            if (token.Length < 2 || token[0] != '$') { args.err = new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {args.line}: Invalid register format encountered \"{token}\""); return false; }
            if (!TryParseInstantImm(args, token.Substring(1), out res, out bool floating)) { args.err = new Tuple<AssembleError, string>(AssembleError.ArgError, $"line{args.line}: Failed to parse register \"{token}\"\n-> {args.err.Item2}"); return false; }
            if (floating) { args.err = new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {args.line}: Attempt to use floating point value to specify register \"{token}\""); return false; }
            if (res >= 16) { args.err = new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {args.line}: Register out of range \"{token}\" -> {res}"); return false; }

            return true;
        }
        private static bool TryParseSizecode(AssembleArgs args, string token, out UInt64 res)
        {
            // must be able ti get an instant imm
            if (!TryParseInstantImm(args, token, out res, out bool floating)) { args.err = new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {args.line}: Failed to parse size code \"{token}\"\n-> {args.err.Item2}"); return false; }
            if (floating) { args.err = new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {args.line}: Attempt to use floating point value to specify register size \"{token}\" -> {res}"); return false; }

            // convert to size code
            switch (res)
            {
                case 8: res = 0; return true;
                case 16: res = 1; return true;
                case 32: res = 2; return true;
                case 64: res = 3; return true;

                default: args.err = new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {args.line}: Invalid register size {res}"); return false;
            }
        }
        private static bool TryParseMultcode(AssembleArgs args, string token, out UInt64 res)
        {
            // must be able ti get an instant imm
            if (!TryParseInstantImm(args, token, out res, out bool floating)) { args.err = new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {args.line}: Failed to parse multiplier \"{token}\"\n-> {args.err.Item2}"); return false; }
            if (floating) { args.err = new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {args.line}: Attempt to use floating point value to specify size multiplier \"{token}\" -> {res}"); return false; }

            // convert to mult code
            switch (res)
            {
                case 0: res = 0; return true;
                case 1: res = 1; return true;
                case 2: res = 2; return true;
                case 4: res = 3; return true;
                case 8: res = 4; return true;
                case 16: res = 5; return true;
                case 32: res = 6; return true;
                case 64: res = 7; return true;

                default: args.err = new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {args.line}: Invalid size multiplier {res}"); return false;
            }
        }

        private static bool TryParseImm(AssembleArgs args, string token, out Hole hole)
        {
            int pos = 0, end = 0; // position in token
            hole = new Hole();    // resulting hole

            if (token.Length == 0) { args.err = new Tuple<AssembleError, string>(AssembleError.InvalidLabel, $"line {args.line}: Empty label encountered"); return false; }

            while (pos < token.Length)
            {
                // find the next separator
                for (int i = pos + 1; i < token.Length; ++i)
                    if (token[i] == '+' || token[i] == '-') { end = i; break; }
                // if nothing found, end is end of token
                if (pos == end) end = token.Length;

                string sub = token.Substring(pos, end - pos);

                // append subtoken to the hole
                if (!hole.Append(args, sub)) { args.err = new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {args.line}: Failed to parse imm \"{token}\"\n-> {args.err.Item2}"); return false; }

                // start of next token includes separator
                pos = end;
            }

            return true;
        }

        private static bool TryParseAddressReg(AssembleArgs args, string token, out UInt64 r, out UInt64 m)
        {
            r = m = 0;

            // remove sign
            string _seg = token[0] == '+' || token[0] == '-' ? token.Substring(1) : token;
            if (_seg.Length == 0) { args.err = new Tuple<AssembleError, string>(AssembleError.InvalidLabel, $"line {args.line}: Empty symbol encountered"); return false; }

            // split on multiplication
            string[] _r = _seg.Split(new char[] { '*' });

            // if 1 token, is a register
            if (_r.Length == 1)
            {
                m = 1;
                return TryParseRegister(args, _r[0], out r);
            }
            // if 2 tokens, has a multcode
            else if (_r.Length == 2)
            {
                if ((!TryParseRegister(args, _r[0], out r) || !TryParseMultcode(args, _r[1], out m)) && (!TryParseRegister(args, _r[1], out r) || !TryParseMultcode(args, _r[0], out m)))
                { args.err = new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {args.line}: Invalid multiplier-register pair encountered \"{token}\""); return false; }
            }
            // otherwise is illegal
            else { args.err = new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {args.line}: Cannot use more than one multiplier and one register per address register subtoken"); return false; }

            return true;
        }
        // [1: literal][3: m1][1: -m2][3: m2]   ([4: r1][4: r2])   ([64: imm])
        private static bool TryParseAddress(AssembleArgs args, string token, out UInt64 a, out UInt64 b, out Hole hole)
        {
            a = b = 0;
            hole = new Hole();

            // must be of [*] format
            if (token.Length < 3 || token[0] != '[' || token[token.Length - 1] != ']')
            { args.err = new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {args.line}: Invalid address format encountered \"{token}\""); return false; }

            string r1_seg = null, r2_seg = null; // register parsing temporaries

            int pos = 1, end = 1; // position in token
            while (pos < token.Length - 1)
            {
                // find the next separator
                for (int i = pos + 1; i < token.Length; ++i)
                    if (token[i] == '+' || token[i] == '-') { end = i; break; }
                // if nothing found, end is end of token
                if (pos == end) end = token.Length - 1;

                // get subtoken to process
                string sub = token.Substring(pos, end - pos);

                // if it's a register
                if (sub.Contains('$'))
                {
                    // if negative register
                    if (sub[0] == '-')
                    {
                        // if r2 empty, put it there
                        if (r2_seg == null) r2_seg = sub;
                        // otherwise fail
                        else { args.err = new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {args.line}: Address cannot have more than one negative register"); return false; }
                    }
                    // otherwise positive
                    else
                    {
                        // if r1 empty, put it there
                        if (r1_seg == null) r1_seg = sub;
                        // otherwise try r2
                        else if (r2_seg == null) r2_seg = sub;
                        // otherwise fail
                        else { args.err = new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {args.line}: Address may only use one register"); return false; }
                    }
                }
                // otherwise tack it onto the hole
                else if (!hole.Append(args, sub)) return false;

                // start of next token includes separator
                pos = end;
            }

            // register parsing temporaries
            UInt64 r1 = 0, r2 = 0, m1 = 0, m2 = 0;

            // process regs
            if (r1_seg != null && !TryParseAddressReg(args, r1_seg, out r1, out m1)) return false;
            if (r2_seg != null && !TryParseAddressReg(args, r2_seg, out r2, out m2)) return false;

            // if there was no hole, null it
            if (hole.Value == 0 && hole.FValue == 0 && hole.Segments.Count == 0) hole = null;

            // [1: literal][3: m1][1: -m2][3: m2]   [4: r1][4: r2]   ([64: imm])
            a = (hole != null ? 128 : 0ul) | (m1 << 4) | (r2_seg != null && r2_seg[0] == '-' ? 8 : 0ul) | m2;
            b = (r1 << 4) | r2;

            return true;
        }

        private static bool IsValidLabel(string token)
        {
            if (token.Length == 0) return false;
            if (token[0] != '_' && !char.IsLetter(token[0])) return false;
            for (int i = 1; i < token.Length; ++i)
                if (token[i] != '_' && !char.IsLetterOrDigit(token[i])) return false;
            return true;
        }
        private static bool MutateLabel(AssembleArgs args, ref string label)
        {
            // if defining a local label
            if (label.Length >= 2 && label[0] == '.')
            {
                if (args.last_static_label == null) { args.err = new Tuple<AssembleError, string>(AssembleError.InvalidLabel, $"line {args.line}: Cannot define a local label before the first static label"); return false; }

                // mutate the label
                label = $"{args.last_static_label}_{label.Substring(1)}";
            }

            return true;
        }

        public static UInt64 Time()
        {
            return DateTime.UtcNow.Ticks.MakeUnsigned();
        }

        private static bool TryProcessBinaryOp(AssembleArgs args, OPCode op, int _b_sizecode = -1, UInt64 sizemask = 15)
        {
            UInt64 a, b, c; // parsing temporaries
            Hole hole1, hole2;

            if (args.args.Length != 2) { args.err = new Tuple<AssembleError, string>(AssembleError.ArgCount, $"line {args.line}: {op} expected 2 args"); return false; }
            if ((Size(args.sizecode) & sizemask) == 0) { args.err = new Tuple<AssembleError, string>(AssembleError.UsageError, $"line {args.line}: {op} does not support the specified size code"); return false; }

            AppendVal(args, 1, (UInt64)op);

            UInt64 b_sizecode = _b_sizecode == -1 ? args.sizecode : (UInt64)_b_sizecode;

            if (TryParseRegister(args, args.args[0], out a))
            {
                if (TryParseImm(args, args.args[1], out hole1))
                {
                    AppendVal(args, 1, (a << 4) | (args.sizecode << 2) | 0);
                    if (!AppendHole(args, Size(b_sizecode), hole1)) { args.err = new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {args.line}: Failed to append value\n-> {args.err.Item2}"); return false; }
                }
                else if (TryParseAddress(args, args.args[1], out b, out c, out hole1))
                {
                    AppendVal(args, 1, (a << 4) | (args.sizecode << 2) | 1);
                    if (!AppendAddress(args, b, c, hole1)) { args.err = new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {args.line}: Failed to append value\n-> {args.err.Item2}"); return false; }
                }
                else if (TryParseRegister(args, args.args[1], out b))
                {
                    AppendVal(args, 1, (a << 4) | (args.sizecode << 2) | 2);
                    AppendVal(args, 1, b);
                }
                else { args.err = new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {args.line}: Couldn't parse \"{args.args[1]}\" as an imm, address, or register"); return false; }
            }
            else if (TryParseAddress(args, args.args[0], out a, out b, out hole1))
            {
                if (TryParseRegister(args, args.args[1], out c))
                {
                    AppendVal(args, 1, (args.sizecode << 2) | 2);
                    AppendVal(args, 1, 16 | c);
                    if (!AppendAddress(args, a, b, hole1)) { args.err = new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {args.line}: Failed to append value\n-> {args.err.Item2}"); return false; };
                }
                else if (TryParseImm(args, args.args[1], out hole2))
                {
                    AppendVal(args, 1, (args.sizecode << 2) | 3);
                    if (!AppendHole(args, Size(b_sizecode), hole2)) { args.err = new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {args.line}: Failed to append value\n-> {args.err.Item2}"); return false; }
                    if (!AppendAddress(args, a, b, hole1)) { args.err = new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {args.line}: Failed to append value\n-> {args.err.Item2}"); return false; }
                }
                else { args.err = new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {args.line}: Couldn't parse \"{args.args[1]}\" as a register or imm"); return false; }
            }
            else { args.err = new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {args.line}: Couldn't parse \"{args.args[0]}\" as a register or address"); return false; }

            return true;
        }
        private static bool TryProcessUnaryOp(AssembleArgs args, OPCode op, UInt64 sizemask = 15)
        {
            UInt64 a, b;
            Hole hole;

            if (args.args.Length != 1) { args.err = new Tuple<AssembleError, string>(AssembleError.ArgCount, $"line {args.line}: {op} expected 1 arg"); return false; }
            if ((Size(args.sizecode) & sizemask) == 0) { args.err = new Tuple<AssembleError, string>(AssembleError.UsageError, $"line {args.line}: {op} does not support the specified size code"); return false; }

            AppendVal(args, 1, (UInt64)op);

            if (TryParseRegister(args, args.args[0], out b))
            {
                AppendVal(args, 1, (b << 4) | (args.sizecode << 2) | 0);
            }
            else if (TryParseAddress(args, args.args[0], out a, out b, out hole))
            {
                AppendVal(args, 1, (args.sizecode << 2) | 1);
                if (!AppendAddress(args, a, b, hole)) { args.err = new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {args.line}: Failed to append value\n-> {args.err.Item2}"); return false; }
            }
            else { args.err = new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {args.line}: Couldn't parse \"{args.args[0]}\" as a register or address"); return false; }

            return true;
        }
        private static bool TryProcessJump(AssembleArgs args, OPCode op)
        {
            if (args.args.Length != 1) { args.err = new Tuple<AssembleError, string>(AssembleError.ArgCount, $"line {args.line}: {op} expected 1 arg"); return false; }

            if (!TryParseAddress(args, args.args[0], out UInt64 a, out UInt64 b, out Hole hole)) { args.err = new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {args.line}: Jump expected address as first arg\n-> {args.err.Item2}"); return false; }

            AppendVal(args, 1, (UInt64)op);
            if (!AppendAddress(args, a, b, hole)) { args.err = new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {args.line}: Failed to append value\n-> {args.err.Item2}"); return false; }

            return true;
        }
        private static bool TryProcessEmission(AssembleArgs args)
        {
            if (args.args.Length == 0) { args.err = new Tuple<AssembleError, string>(AssembleError.ArgCount, $"line {args.line}: Emission expected at least one value"); return false; }

            Hole hole = new Hole(); // initially empty hole (to allow for buffer shorthand e.x. "emit x32")
            UInt64 mult;
            bool floating;

            for (int i = 0; i < args.args.Length; ++i)
            {
                if (args.args[i].Length == 0) { args.err = new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {args.line}: Emission encountered empty argument"); return false; }

                // if a multiplier
                if (args.args[i][0] == 'x')
                {
                    // get the multiplier and ensure is valid
                    if (!TryParseInstantImm(args, args.args[i].Substring(1), out mult, out floating)) return false;
                    if (floating) { args.err = new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {args.line}: Emission multiplier cannot be floating point"); return false; }
                    if (mult > EmissionMaxMultiplier) { args.err = new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {args.line}: Emission multiplier cannot exceed {EmissionMaxMultiplier}"); return false; }
                    if (mult == 0) { args.err = new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {args.line}: Emission multiplier cannot be zero"); return false; }

                    // account for first written value
                    if (i > 0 && args.args[i - 1][0] != 'x') --mult;

                    for (UInt64 j = 0; j < mult; ++j)
                        if (!AppendHole(args, Size(args.sizecode), hole)) { args.err = new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {args.line}: Failed to append value\n-> {args.err.Item2}"); return false; }
                }
                // if a string
                else if (args.args[i][0] == '"' || args.args[i][0] == '\'')
                {
                    if (args.args[i][0] != args.args[i][args.args[i].Length - 1]) { args.err = new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {args.line}: String literal must be enclosed in single or double quotes"); return false; }

                    // dump the contents into memory
                    for (int j = 1; j < args.args[i].Length - 1; ++j)
                    {
                        // make sure there's no string splicing
                        if (args.args[i][j] == args.args[i][0]) { args.err = new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {args.line}: String emission prematurely reached a terminating quote"); return false; }

                        AppendVal(args, Size(args.sizecode), args.args[i][j]);
                    }
                }
                // otherwise is a value
                else
                {
                    // get the value
                    if (!TryParseImm(args, args.args[i], out hole)) return false;

                    // make one of them
                    if (!AppendHole(args, Size(args.sizecode), hole)) { args.err = new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {args.line}: Failed to append value\n-> {args.err.Item2}"); return false; }
                }
            }

            return true;
        }
        private static bool TryProcessXMULXDIV(AssembleArgs args, OPCode op)
        {
            UInt64 a, b;
            Hole hole;

            if (args.args.Length != 1) { args.err = new Tuple<AssembleError, string>(AssembleError.ArgCount, $"line {args.line}: {op} expected 1 arg"); return false; }

            AppendVal(args, 1, (UInt64)op);

            if (TryParseImm(args, args.args[0], out hole))
            {
                AppendVal(args, 1, (args.sizecode << 2) | 0);
                if (!AppendHole(args, Size(args.sizecode), hole)) { args.err = new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {args.line}: Failed to append value\n-> {args.err.Item2}"); return false; }
            }
            else if (TryParseRegister(args, args.args[0], out a))
            {
                AppendVal(args, 1, (a << 4) | (args.sizecode << 2) | 1);
            }
            else if (TryParseAddress(args, args.args[0], out a, out b, out hole))
            {
                AppendVal(args, 1, (args.sizecode << 2) | 2);
                if (!AppendAddress(args, a, b, hole)) { args.err = new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {args.line}: Failed to append value\n-> {args.err.Item2}"); return false; }
            }
            else { args.err = new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {args.line}: Couldn't parse \"{args.args[0]}\" as an imm, register, or address"); return false; }

            return true;
        }

        public static Tuple<AssembleError, string> Assemble(string code, out ObjectFile file)
        {
            file = new ObjectFile();
            AssembleArgs args = new AssembleArgs()
            {
                file = file,
                line = 0,

                last_static_label = null,
                err = null
            };

            // predefined symbols
            args.file.Symbols = new Dictionary<string, Symbol>()
            {
                ["__time__"] = new Symbol() { Value = Time(), IsAddress = false, IsFloating = false },
                ["__version__"] = new Symbol() { Value = Version, IsAddress = false, IsFloating = false },

                ["__pinf__"] = new Symbol() { Value = DoubleAsUInt64(double.PositiveInfinity), IsAddress = false, IsFloating = true },
                ["__ninf__"] = new Symbol() { Value = DoubleAsUInt64(double.NegativeInfinity), IsAddress = false, IsFloating = true },
                ["__nan__"] = new Symbol() { Value = DoubleAsUInt64(double.NaN), IsAddress = false, IsFloating = true },

                ["__fmax__"] = new Symbol() { Value = DoubleAsUInt64(double.MaxValue), IsAddress = false, IsFloating = true },
                ["__fmin__"] = new Symbol() { Value = DoubleAsUInt64(double.MinValue), IsAddress = false, IsFloating = true },
                ["__fepsilon__"] = new Symbol() { Value = DoubleAsUInt64(double.Epsilon), IsAddress = false, IsFloating = true },

                ["__pi__"] = new Symbol() { Value = DoubleAsUInt64(Math.PI), IsAddress = false, IsFloating = true },
                ["__e__"] = new Symbol() { Value = DoubleAsUInt64(Math.E), IsAddress = false, IsFloating = true }
            };

            int pos = 0, end = 0; // position in code

            // potential parsing args for an instruction
            UInt64 a = 0, b = 0, c = 0, d = 0;
            Hole hole;
            bool floating;

            if (code.Length == 0) return new Tuple<AssembleError, string>(AssembleError.EmptyFile, "The file was empty");

            while (pos < code.Length)
            {
                // find the next separator
                for (end = pos; end < code.Length && code[end] != '\n' && code[end] != '#'; ++end) ;

                ++args.line; // advance line counter
                // split the line
                args.rawline = code.Substring(pos, end - pos);
                if (!SplitLine(args)) return new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {args.line}: Failed to parse line\n-> {args.err.Item2}");
                // if the separator was a comment character, consume the rest of the line as well as noop
                if (end < code.Length && code[end] == '#')
                    for (; end < code.Length && code[end] != '\n'; ++end) ;

                // process marked labels
                for (int i = 0; i < args.label_defs.Length; ++i)
                {
                    string label = args.label_defs[i]; // shorthand reference to current label

                    // handle local mutation
                    if (label.Length > 0 && label[0] != '.') args.last_static_label = label;
                    if (!MutateLabel(args, ref label)) return args.err;

                    if (!IsValidLabel(label)) return new Tuple<AssembleError, string>(AssembleError.InvalidLabel, $"line {args.line}: Symbol name \"{label}\" invalid");
                    if (file.Symbols.ContainsKey(label)) return new Tuple<AssembleError, string>(AssembleError.SymbolRedefinition, $"line {args.line}: Symbol \"{label}\" was already defined");

                    // add the symbol as an address
                    file.Symbols.Add(label, new Symbol() { Value = (UInt64)file.Data.LongCount(), IsAddress = true, IsFloating = false });
                }

                // empty lines are ignored
                if (args.op != string.Empty)
                {
                    // update compile-time symbols (for modifications, also update TryParseImm())
                    file.Symbols["__line__"] = new Symbol() { Value = (UInt64)args.line, IsAddress = false, IsFloating = false };
                    file.Symbols["__pos__"] = new Symbol() { Value = (UInt64)file.Data.LongCount(), IsAddress = true, IsFloating = false };

                    switch (args.op.ToUpper())
                    {
                        case "GLOBAL":
                            if (args.args.Length != 1) return new Tuple<AssembleError, string>(AssembleError.ArgCount, $"line {args.line}: GLOBAL expected 1 arg");
                            if (!IsValidLabel(args.args[0])) return new Tuple<AssembleError, string>(AssembleError.InvalidLabel, $"line {args.line}: Invalid label name \"{args.args[0]}\"");
                            file.GlobalSymbols.Add(args.args[0]);
                            break;
                        case "DEF":
                            if (args.args.Length != 2) return new Tuple<AssembleError, string>(AssembleError.ArgCount, $"line {args.line}: DEF expected 2 args");
                            if (!MutateLabel(args, ref args.args[0])) return args.err;
                            if (!IsValidLabel(args.args[0])) return new Tuple<AssembleError, string>(AssembleError.InvalidLabel, $"line {args.line}: Invalid label name \"{args.args[0]}\"");
                            if (!TryParseInstantImm(args, args.args[1], out a, out floating)) return new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {args.line}: DEF expected a number as third arg\n-> {args.err.Item2}");
                            if (file.Symbols.ContainsKey(args.args[0])) return new Tuple<AssembleError, string>(AssembleError.SymbolRedefinition, $"line {args.line}: Symbol \"{args.args[0]}\" was already defined");
                            file.Symbols.Add(args.args[0], new Symbol() { Value = a, IsAddress = false, IsFloating = floating });
                            break;

                        case "EMIT": if (!TryProcessEmission(args)) return args.err; break;

                        // --------------------------
                        // -- OPCode assembly impl --
                        // --------------------------

                        // [8: op]
                        case "NOP":
                            if (args.args.Length != 0) return new Tuple<AssembleError, string>(AssembleError.ArgCount, $"line {args.line}: NOP expected 0 args");
                            AppendVal(args, 1, (UInt64)OPCode.NOP);
                            break;
                        case "STOP":
                            if (args.args.Length != 0) return new Tuple<AssembleError, string>(AssembleError.ArgCount, $"line {args.line}: STOP expected 0 args");
                            AppendVal(args, 1, (UInt64)OPCode.STOP);
                            break;
                        case "SYSCALL":
                            if (args.args.Length != 0) return new Tuple<AssembleError, string>(AssembleError.ArgCount, $"line {args.line}: SYSCALL expected 0 args");
                            AppendVal(args, 1, (UInt64)OPCode.SYSCALL);
                            break;

                        case "MOV": if (!TryProcessBinaryOp(args, OPCode.MOV)) return args.err; break;

                        case "MOVA": case "MOVNBE": if (!TryProcessBinaryOp(args, OPCode.MOVa)) return args.err; break;
                        case "MOVAE": case "MOVNB": if (!TryProcessBinaryOp(args, OPCode.MOVae)) return args.err; break;
                        case "MOVB": case "MOVNAE": if (!TryProcessBinaryOp(args, OPCode.MOVb)) return args.err; break;
                        case "MOVBE": case "MOVNA": if (!TryProcessBinaryOp(args, OPCode.MOVbe)) return args.err; break;

                        case "MOVG": case "MOVNLE": if (!TryProcessBinaryOp(args, OPCode.MOVg)) return args.err; break;
                        case "MOVGE": case "MOVNL": if (!TryProcessBinaryOp(args, OPCode.MOVge)) return args.err; break;
                        case "MOVL": case "MOVNGE": if (!TryProcessBinaryOp(args, OPCode.MOVl)) return args.err; break;
                        case "MOVLE": case "MOVNG": if (!TryProcessBinaryOp(args, OPCode.MOVle)) return args.err; break;

                        case "MOVZ": case "MOVE": if (!TryProcessBinaryOp(args, OPCode.MOVz)) return args.err; break;
                        case "MOVNZ": case "MOVNE": if (!TryProcessBinaryOp(args, OPCode.MOVnz)) return args.err; break;
                        case "MOVS": if (!TryProcessBinaryOp(args, OPCode.MOVs)) return args.err; break;
                        case "MOVNS": if (!TryProcessBinaryOp(args, OPCode.MOVns)) return args.err; break;
                        case "MOVP": if (!TryProcessBinaryOp(args, OPCode.MOVp)) return args.err; break;
                        case "MOVNP": if (!TryProcessBinaryOp(args, OPCode.MOVnp)) return args.err; break;
                        case "MOVO": if (!TryProcessBinaryOp(args, OPCode.MOVo)) return args.err; break;
                        case "MOVNO": if (!TryProcessBinaryOp(args, OPCode.MOVno)) return args.err; break;
                        case "MOVC": if (!TryProcessBinaryOp(args, OPCode.MOVc)) return args.err; break;
                        case "MOVNC": if (!TryProcessBinaryOp(args, OPCode.MOVnc)) return args.err; break;

                        case "SWAP":
                            if (args.args.Length != 2) return new Tuple<AssembleError, string>(AssembleError.ArgCount, $"line {args.line}: SWAP expected 2 args");

                            AppendVal(args, 1, (UInt64)OPCode.SWAP);

                            if (TryParseRegister(args, args.args[0], out a))
                            {
                                if (TryParseRegister(args, args.args[1], out b))
                                {
                                    AppendVal(args, 1, (a << 4) | (args.sizecode << 2) | 0);
                                    AppendVal(args, 1, b);
                                }
                                else if (TryParseAddress(args, args.args[1], out b, out c, out hole))
                                {
                                    AppendVal(args, 1, (a << 4) | (args.sizecode << 2) | 1);
                                    if (!AppendAddress(args, b, c, hole)) return new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {args.line}: Failed to append value\n-> {args.err.Item2}");
                                }
                                else return new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {args.line}: Unknown usage of SWAP");
                            }
                            else if (TryParseAddress(args, args.args[0], out a, out b, out hole))
                            {
                                if (TryParseRegister(args, args.args[1], out c))
                                {
                                    AppendVal(args, 1, (c << 4) | (args.sizecode << 2) | 1);
                                    if (!AppendAddress(args, a, b, hole)) return new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {args.line}: Failed to append value");
                                }
                                else return new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {args.line}: Unknown usage of SWAP");
                            }
                            else return new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {args.line}: Unknown usage of SWAP");

                            break;

                        case "UEXTEND": case "UX": a = (UInt64)OPCode.UEXTEND; goto XEXTEND;
                        case "SEXTEND":
                        case "SX":
                            a = (UInt64)OPCode.SEXTEND;
                        XEXTEND:
                            if (args.args.Length != 2) return new Tuple<AssembleError, string>(AssembleError.ArgCount, $"line {args.line}: XEXTEND expected 2 args");

                            if (!TryParseSizecode(args, args.args[0], out b)) return new Tuple<AssembleError, string>(AssembleError.MissingSize, $"line {args.line}: UEXTEND expected size parameter as second arg\n-> {args.err.Item2}");
                            if (!TryParseRegister(args, args.args[1], out c)) return new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {args.line}: UEXTEND expected register parameter as third arg\n-> {args.err.Item2}");

                            AppendVal(args, 1, a);
                            AppendVal(args, 1, (c << 4) | (b << 2) | args.sizecode);

                            break;

                        case "UMUL": if (!TryProcessXMULXDIV(args, OPCode.UMUL)) return args.err; break;
                        case "SMUL": if (!TryProcessXMULXDIV(args, OPCode.SMUL)) return args.err; break;
                        case "UDIV": if (!TryProcessXMULXDIV(args, OPCode.UDIV)) return args.err; break;
                        case "SDIV": if (!TryProcessXMULXDIV(args, OPCode.SDIV)) return args.err; break;

                        case "ADD": if (!TryProcessBinaryOp(args, OPCode.ADD)) return args.err; break;
                        case "SUB": if (!TryProcessBinaryOp(args, OPCode.SUB)) return args.err; break;
                        case "BMUL": if (!TryProcessBinaryOp(args, OPCode.BMUL)) return args.err; break;
                        case "BUDIV": if (!TryProcessBinaryOp(args, OPCode.BUDIV)) return args.err; break;
                        case "BUMOD": if (!TryProcessBinaryOp(args, OPCode.BUMOD)) return args.err; break;
                        case "BSDIV": if (!TryProcessBinaryOp(args, OPCode.BSDIV)) return args.err; break;
                        case "BSMOD": if (!TryProcessBinaryOp(args, OPCode.BSMOD)) return args.err; break;

                        case "SL": if (!TryProcessBinaryOp(args, OPCode.SL)) return args.err; break;
                        case "SR": if (!TryProcessBinaryOp(args, OPCode.SR)) return args.err; break;
                        case "SAL": if (!TryProcessBinaryOp(args, OPCode.SAL)) return args.err; break;
                        case "SAR": if (!TryProcessBinaryOp(args, OPCode.SAR)) return args.err; break;
                        case "RL": if (!TryProcessBinaryOp(args, OPCode.RL)) return args.err; break;
                        case "RR": if (!TryProcessBinaryOp(args, OPCode.RR)) return args.err; break;

                        case "AND": if (!TryProcessBinaryOp(args, OPCode.AND)) return args.err; break;
                        case "OR": if (!TryProcessBinaryOp(args, OPCode.OR)) return args.err; break;
                        case "XOR": if (!TryProcessBinaryOp(args, OPCode.XOR)) return args.err; break;

                        case "CMP": if (!TryProcessBinaryOp(args, OPCode.CMP)) return args.err; break;
                        case "TEST": if (!TryProcessBinaryOp(args, OPCode.TEST)) return args.err; break;

                        // [8: unary op]   [4: dest][2:][2: size]
                        case "INC": if (!TryProcessUnaryOp(args, OPCode.INC)) return args.err; break;
                        case "DEC": if (!TryProcessUnaryOp(args, OPCode.DEC)) return args.err; break;
                        case "NEG": if (!TryProcessUnaryOp(args, OPCode.NEG)) return args.err; break;
                        case "NOT": if (!TryProcessUnaryOp(args, OPCode.NOT)) return args.err; break;
                        case "ABS": if (!TryProcessUnaryOp(args, OPCode.ABS)) return args.err; break;
                        case "CMPZ": if (!TryProcessUnaryOp(args, OPCode.CMPZ)) return args.err; break;

                        // [8: la]   [4:][4: dest]   [address]
                        case "LA":
                            if (args.args.Length != 2) return new Tuple<AssembleError, string>(AssembleError.ArgCount, $"line {args.line}: LA expected 2 args");
                            if (args.sizecode != 3) return new Tuple<AssembleError, string>(AssembleError.UsageError, $"line {args.line}: LA does not support the specified size code");

                            if (!TryParseRegister(args, args.args[0], out a)) return new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {args.line}: LA expecetd register as first arg\n-> {args.err.Item2}");
                            if (!TryParseAddress(args, args.args[1], out b, out c, out hole)) return new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {args.line}: LA expected address as second arg\n-> {args.err.Item2}");

                            AppendVal(args, 1, (UInt64)OPCode.LA);
                            AppendVal(args, 1, a);
                            if (!AppendAddress(args, b, c, hole)) return new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {args.line}: Failed to append value\n-> {args.err.Item2}");

                            break;

                        // [8: Jcc]   [address]
                        case "JMP": if (!TryProcessJump(args, OPCode.JMP)) return args.err; break;

                        case "JA": case "JNBE": if (!TryProcessJump(args, OPCode.Ja)) return args.err; break;
                        case "JAE": case "JNB": if (!TryProcessJump(args, OPCode.Jae)) return args.err; break;
                        case "JB": case "JNAE": if (!TryProcessJump(args, OPCode.Jb)) return args.err; break;
                        case "JBE": case "JNA": if (!TryProcessJump(args, OPCode.Jbe)) return args.err; break;

                        case "JG": case "JNLE": if (!TryProcessJump(args, OPCode.Jg)) return args.err; break;
                        case "JGE": case "JNL": if (!TryProcessJump(args, OPCode.Jge)) return args.err; break;
                        case "JL": case "JNGE": if (!TryProcessJump(args, OPCode.Jl)) return args.err; break;
                        case "JLE": case "JNG": if (!TryProcessJump(args, OPCode.Jle)) return args.err; break;

                        case "JZ": case "JE": if (!TryProcessJump(args, OPCode.Jz)) return args.err; break;
                        case "JNZ": case "JNE": if (!TryProcessJump(args, OPCode.Jnz)) return args.err; break;
                        case "JS": if (!TryProcessJump(args, OPCode.Js)) return args.err; break;
                        case "JNS": if (!TryProcessJump(args, OPCode.Jns)) return args.err; break;
                        case "JP": if (!TryProcessJump(args, OPCode.Jp)) return args.err; break;
                        case "JNP": if (!TryProcessJump(args, OPCode.Jnp)) return args.err; break;
                        case "JO": if (!TryProcessJump(args, OPCode.Jo)) return args.err; break;
                        case "JNO": if (!TryProcessJump(args, OPCode.Jno)) return args.err; break;
                        case "JC": if (!TryProcessJump(args, OPCode.Jc)) return args.err; break;
                        case "JNC": if (!TryProcessJump(args, OPCode.Jnc)) return args.err; break;

                        case "FADD": if (!TryProcessBinaryOp(args, OPCode.FADD, -1, 12)) return args.err; break;
                        case "FSUB": if (!TryProcessBinaryOp(args, OPCode.FSUB, -1, 12)) return args.err; break;
                        case "FMUL": if (!TryProcessBinaryOp(args, OPCode.FMUL, -1, 12)) return args.err; break;
                        case "FDIV": if (!TryProcessBinaryOp(args, OPCode.FDIV, -1, 12)) return args.err; break;
                        case "FMOD": if (!TryProcessBinaryOp(args, OPCode.FMOD, -1, 12)) return args.err; break;

                        case "POW": if (!TryProcessBinaryOp(args, OPCode.POW, -1, 12)) return args.err; break;
                        case "SQRT": if (!TryProcessUnaryOp(args, OPCode.SQRT, 12)) return args.err; break;
                        case "EXP": if (!TryProcessUnaryOp(args, OPCode.EXP, 12)) return args.err; break;
                        case "LN": if (!TryProcessUnaryOp(args, OPCode.LN, 12)) return args.err; break;
                        case "FNEG": if (!TryProcessUnaryOp(args, OPCode.FNEG, 12)) return args.err; break;
                        case "FABS": if (!TryProcessUnaryOp(args, OPCode.FABS, 12)) return args.err; break;
                        case "FCMPZ": if (!TryProcessUnaryOp(args, OPCode.FCMPZ, 12)) return args.err; break;

                        case "SIN": if (!TryProcessUnaryOp(args, OPCode.SIN, 12)) return args.err; break;
                        case "COS": if (!TryProcessUnaryOp(args, OPCode.COS, 12)) return args.err; break;
                        case "TAN": if (!TryProcessUnaryOp(args, OPCode.TAN, 12)) return args.err; break;

                        case "SINH": if (!TryProcessUnaryOp(args, OPCode.SINH, 12)) return args.err; break;
                        case "COSH": if (!TryProcessUnaryOp(args, OPCode.COSH, 12)) return args.err; break;
                        case "TANH": if (!TryProcessUnaryOp(args, OPCode.TANH, 12)) return args.err; break;

                        case "ASIN": if (!TryProcessUnaryOp(args, OPCode.ASIN, 12)) return args.err; break;
                        case "ACOS": if (!TryProcessUnaryOp(args, OPCode.ACOS, 12)) return args.err; break;
                        case "ATAN": if (!TryProcessUnaryOp(args, OPCode.ATAN, 12)) return args.err; break;
                        case "ATAN2": if (!TryProcessBinaryOp(args, OPCode.ATAN2, -1, 12)) return args.err; break;

                        case "FLOOR": if (!TryProcessUnaryOp(args, OPCode.FLOOR, 12)) return args.err; break;
                        case "CEIL": if (!TryProcessUnaryOp(args, OPCode.CEIL, 12)) return args.err; break;
                        case "ROUND": if (!TryProcessUnaryOp(args, OPCode.ROUND, 12)) return args.err; break;
                        case "TRUNC": if (!TryProcessUnaryOp(args, OPCode.TRUNC, 12)) return args.err; break;

                        case "FCMP": if (!TryProcessBinaryOp(args, OPCode.FCMP, -1, 12)) return args.err; break;

                        case "FTOI": if (!TryProcessUnaryOp(args, OPCode.FTOI, 12)) return args.err; break;
                        case "ITOF": if (!TryProcessUnaryOp(args, OPCode.ITOF, 12)) return args.err; break;

                        case "PUSH":
                            if (args.args.Length != 1) return new Tuple<AssembleError, string>(AssembleError.ArgCount, $"line {args.line}: PUSH expected 1 arg");

                            AppendVal(args, 1, (UInt64)OPCode.PUSH);

                            if (TryParseImm(args, args.args[0], out hole))
                            {
                                AppendVal(args, 1, (args.sizecode << 2) | 0);
                                if (!AppendHole(args, Size(args.sizecode), hole)) return new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {args.line}: Failed to append value\n-> {args.err.Item2}");
                            }
                            else if (TryParseRegister(args, args.args[0], out a))
                            {
                                AppendVal(args, 1, (a << 4) | (args.sizecode << 2) | 1);
                            }
                            else return new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {args.line}: Couldn't parse \"{args.args[0]}\" as an imm or register");

                            break;
                        case "POP":
                            if (args.args.Length != 1) return new Tuple<AssembleError, string>(AssembleError.ArgCount, $"line {args.line}: POP expected 1 arg");

                            if (!TryParseRegister(args, args.args[0], out a)) return new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {args.line}: POP expected register as second arg\n-> {args.err.Item2}");

                            AppendVal(args, 1, (UInt64)OPCode.POP);
                            AppendVal(args, 1, (a << 4) | (args.sizecode << 2));

                            break;
                        case "CALL":
                            if (args.args.Length != 1) return new Tuple<AssembleError, string>(AssembleError.ArgCount, $"line {args.line}: CALL expected 1 arg");
                            if (!TryParseAddress(args, args.args[0], out a, out b, out hole)) return new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {args.line}: CALL expected address as first arg\n-> {args.err.Item2}");

                            AppendVal(args, 1, (UInt64)OPCode.CALL);
                            if (!AppendAddress(args, a, b, hole)) return new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {args.line}: Failed to append value\n-> {args.err.Item2}");

                            break;
                        case "RET":
                            if (args.args.Length != 0) return new Tuple<AssembleError, string>(AssembleError.ArgCount, $"line {args.line}: CALL expected 0 args");

                            AppendVal(args, 1, (UInt64)OPCode.RET);

                            break;

                        case "BSWAP": if (!TryProcessUnaryOp(args, OPCode.BSWAP)) return args.err; break;
                        case "BEXTR": if (!TryProcessBinaryOp(args, OPCode.BEXTR, 1)) return args.err; break;
                        case "BLSI": if (!TryProcessUnaryOp(args, OPCode.BLSI)) return args.err; break;
                        case "BLSMSK": if (!TryProcessUnaryOp(args, OPCode.BLSMSK)) return args.err; break;
                        case "BLSR": if (!TryProcessUnaryOp(args, OPCode.BLSR)) return args.err; break;
                        case "ANDN": if (!TryProcessBinaryOp(args, OPCode.ANDN)) return args.err; break;

                        case "GETF": a = (UInt64)OPCode.GETF; goto GETSETF;
                        case "SETF":
                            a = (UInt64)OPCode.SETF;
                        GETSETF:
                            if (args.args.Length != 1) return new Tuple<AssembleError, string>(AssembleError.ArgCount, $"line {args.line}: GETF expected one arg");
                            if (!TryParseRegister(args, args.args[0], out b)) return new Tuple<AssembleError, string>(AssembleError.UsageError, $"line {args.line}: GETF expected arg one to be a register");
                            if (args.sizecode != 3) return new Tuple<AssembleError, string>(AssembleError.UsageError, $"line {args.line}: GETF does not support the specified size code");

                            AppendVal(args, 1, a);
                            AppendVal(args, 1, b);

                            break;

                        case "LOOP": // loop reg, address, (step = 1)
                            // 2 args default step
                            if (args.args.Length == 2) a = 0;
                            // 3 args explicit step
                            else if (args.args.Length == 3)
                            {
                                if (!TryParseInstantImm(args, args.args[2], out a, out floating))
                                    return new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {args.line}: LOOP third argument (explicit step) expected an instant imm");
                                if (floating) return new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {args.line}: LOOP third argument (explicit step) may not be floating-point");

                                switch (a)
                                {
                                    case 1: a = 0; break;
                                    case 2: a = 1; break;
                                    case 4: a = 2; break;
                                    case 8: a = 3; break;

                                    default: return new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {args.line}: LOOP third argument (explicit step) must be 1, 2, 4, or 8");
                                }
                            }
                            else return new Tuple<AssembleError, string>(AssembleError.ArgCount, $"line {args.line}: LOOP expected two args (3 for explicit step)");

                            if (!TryParseRegister(args, args.args[0], out b)) return new Tuple<AssembleError, string>(AssembleError.UsageError, $"line {args.line}: LOOP expected register as first arg");
                            if (!TryParseAddress(args, args.args[1], out c, out d, out hole)) return new Tuple<AssembleError, string>(AssembleError.UsageError, $"line {args.line}: LOOP expected an address as second arg");

                            AppendVal(args, 1, (UInt64)OPCode.LOOP);
                            AppendVal(args, 1, (b << 4) | (args.sizecode << 2) | a);
                            if (!AppendAddress(args, c, d, hole)) return args.err;

                            break;

                        default: return new Tuple<AssembleError, string>(AssembleError.UnknownOp, $"line {args.line}: Unknown operation \"{args.op}\"");
                    }
                }

                // advance to after the new line
                pos = end + 1;
            }

            return new Tuple<AssembleError, string>(AssembleError.None, string.Empty);
        }
        public static Tuple<LinkError, string> Link(ref byte[] res, params ObjectFile[] objs)
        {
            // get total size of objet files
            UInt64 filesize = 0;
            foreach (ObjectFile obj in objs) filesize += (UInt64)obj.Data.LongCount();
            // if zero, there is nothing to link
            if (filesize == 0) return new Tuple<LinkError, string>(LinkError.EmptyResult, "Resulting file is empty");

            res = new byte[filesize + 10]; // give it enough memory to write the whole file plus a header
            filesize = 10;                 // set size to after header (points to writing position)

            UInt64[] offsets = new UInt64[objs.Length];     // offsets for where an object file begins in the resulting exe
            // create a combined symbols table with predefined values
            var symbols = new Dictionary<string, Symbol>()
            {
                ["__prog_end__"] = new Symbol() { Value = (UInt64)res.LongLength, IsAddress = true, IsFloating = false }
            };

            // -------------------------------------------

            // merge the files into res
            for (int i = 0; i < objs.Length; ++i)
            {
                offsets[i] = filesize; // record its starting offset

                objs[i].Data.CopyTo(res, (int)filesize); // copy its data (POTENTIAL SIZE LIMITATION)
                filesize += (UInt64)objs[i].Data.LongCount(); // advance write cursor
            }

            // merge symbols
            for (int i = 0; i < objs.Length; ++i)
                foreach (string symbol in objs[i].GlobalSymbols)
                {
                    if (symbols.ContainsKey(symbol)) return new Tuple<LinkError, string>(LinkError.SymbolRedefinition, $"Symbol \"{symbol}\" was already defined");
                    if (!objs[i].Symbols.TryGetValue(symbol, out Symbol _symbol)) return new Tuple<LinkError, string>(LinkError.MissingSymbol, $"Global symbol \"{symbol}\" undefined");
                    symbols.Add(symbol, new Symbol() { Value = _symbol.IsAddress ? offsets[i] + _symbol.Value : _symbol.Value, IsAddress = _symbol.IsAddress, IsFloating = _symbol.IsFloating });
                }

            // patch holes
            for (int i = 0; i < objs.Length; ++i)
                foreach (var hole in objs[i].Holes)
                {
                    // compute the hole value
                    UInt64 value = hole.Value.Value;
                    double fvalue = hole.Value.FValue;
                    bool floating = hole.Value.Floating;

                    Symbol symbol;
                    foreach (Hole.Segment seg in hole.Value.Segments)
                    {
                        // prefer static definitions
                        if (objs[i].Symbols.TryGetValue(seg.Symbol, out symbol))
                        {
                            if (symbol.IsFloating) { floating = true; fvalue += seg.IsNegative ? -AsDouble(symbol.Value) : AsDouble(symbol.Value); }
                            else
                            {
                                UInt64 temp = symbol.IsAddress ? symbol.Value + offsets[i] : symbol.Value;
                                value += seg.IsNegative ? ~temp + 1 : temp;
                            }
                        }
                        else if (symbols.TryGetValue(seg.Symbol, out symbol))
                        {
                            if (symbol.IsFloating) { floating = true; fvalue += seg.IsNegative ? -AsDouble(symbol.Value) : AsDouble(symbol.Value); }
                            else value += seg.IsNegative ? ~symbol.Value + 1 : symbol.Value;
                        }
                        else return new Tuple<LinkError, string>(LinkError.MissingSymbol, $"Symbol \"{seg.Symbol}\" undefined");
                    }

                    // fill it in
                    {
                        /* !! IF MODIFIED HERE, MUST ALSO BE MODIFIED IN AppendHole() !! */

                        // if it's floating-point
                        if (floating)
                        {
                            // only 64-bit and 32-bit are supported
                            switch (hole.Key.Size)
                            {
                                case 8: Write(res, hole.Key.Address + offsets[i], 8, DoubleAsUInt64(value.MakeSigned() + fvalue)); break;
                                case 4: Write(res, hole.Key.Address + offsets[i], 4, FloatAsUInt64(value.MakeSigned() + (float)fvalue)); break;

                                default: return new Tuple<LinkError, string>(LinkError.FormatError, $"Attempt to use unsupported floating-point size");
                            }
                        }
                        // otherwise it's integral
                        else Write(res, hole.Key.Address + offsets[i], hole.Key.Size, value);
                    }
                    // fill it in
                    Write(res, hole.Key.Address + offsets[i], hole.Key.Size, floating ? DoubleAsUInt64(value.MakeSigned() + fvalue) : value);
                }

            // write the header
            if (!symbols.TryGetValue("main", out Symbol main) || !main.IsAddress) return new Tuple<LinkError, string>(LinkError.MissingSymbol, "No entry point");
            Write(res, 0, 1, (UInt64)OPCode.JMP);
            Write(res, 1, 1, 0x80);
            Write(res, 2, 8, main.Value);

            // linked successfully
            return new Tuple<LinkError, string>(LinkError.None, string.Empty);
        }
    }
}
