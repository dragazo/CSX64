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
    internal static class Utility
    {
        /// <summary>
        /// Swaps the contents of the specified l-values
        /// </summary>
        public static void Swap<T>(ref T a, ref T b)
        {
            T temp = a;
            a = b;
            b = temp;
        }

        /// <summary>
        /// Removes all white space from a string
        /// </summary>
        public static string RemoveWhiteSpace(this string str)
        {
            StringBuilder res = new StringBuilder();

            for (int i = 0; i < str.Length; ++i)
                if (!char.IsWhiteSpace(str[i])) res.Append(str[i]);

            return res.ToString();
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

            LOOP,

            FX
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
            /// The Parity flag
            /// </summary>
            public bool P
            {
                get { return (Flags & 0x02) != 0; }
                set { Flags = (Flags & 0xfd) | (value ? 0x02 : 0ul); }
            }
            /// <summary>
            /// The Overflow flag
            /// </summary>
            public bool O
            {
                get { return (Flags & 0x04) != 0; }
                set { Flags = (Flags & 0xfb) | (value ? 0x04 : 0ul); }
            }
            /// <summary>
            /// The Carry flag
            /// </summary>
            public bool C
            {
                get { return (Flags & 0x08) != 0; }
                set { Flags = (Flags & 0xf7) | (value ? 0x08 : 0ul); }
            }
            /// <summary>
            /// The Sign flag
            /// </summary>
            public bool S
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
        private bool FetchBinaryOpFormat(ref UInt64 s, ref UInt64 m, ref UInt64 a, ref UInt64 b, int _b_sizecode = -1)
        {
            // read settings
            if (!GetMemAdv(1, ref s)) return false;

            UInt64 a_sizecode = (s >> 2) & 3;
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
        private bool StoreBinaryOpFormat(UInt64 s, UInt64 m, UInt64 res)
        {
            UInt64 sizecode = (s >> 2) & 3;

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
        private bool FetchUnaryOpFormat(ref UInt64 s, ref UInt64 m, ref UInt64 a)
        {
            // read settings
            if (!GetMemAdv(1, ref s)) return false;

            UInt64 a_sizecode = (s >> 2) & 3;

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
        private bool StoreUnaryOpFormat(UInt64 s, UInt64 m, UInt64 res)
        {
            UInt64 sizecode = (s >> 2) & 3;

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

        // updates the ZSP flags for integral ops (identical for most integral ops)
        private void UpdateFlagsInt(UInt64 value, UInt64 sizecode)
        {
            Flags.Z = value == 0;
            Flags.S = Negative(value, sizecode);

            // compute parity flag (only of low 8 bits)
            bool parity = true;
            for (int i = 0; i < 8; ++i)
                if (((value >> i) & 1) != 0) parity = !parity;
            Flags.P = parity;
        }
        // updates the flags for floating point ops
        private void UpdateFlagsDouble(double value)
        {
            Flags.Z = value == 0;
            Flags.S = value < 0;
            Flags.O = false;

            Flags.C = double.IsInfinity(value);
            Flags.P = double.IsNaN(value);
        }
        private void UpdateFlagsFloat(float value)
        {
            Flags.Z = value == 0;
            Flags.S = value < 0;
            Flags.O = false;

            Flags.C = float.IsInfinity(value);
            Flags.P = float.IsNaN(value);
        }

        // -- special ops --

        private bool ProcessMOV(bool apply = true)
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

            UpdateFlagsInt(res, sizecode);
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

            UpdateFlagsInt(res, sizecode);
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

            UpdateFlagsInt(res, sizecode);

            return StoreBinaryOpFormat(s, m, res);
        }

        private bool ProcessBUDIV()
        {
            UInt64 s = 0, m = 0, a = 0, b = 0;
            if (!FetchBinaryOpFormat(ref s, ref m, ref a, ref b)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            if (b == 0) { Fail(ErrorCode.ArithmeticError); return false; }

            UInt64 res = Truncate(a / b, sizecode);

            UpdateFlagsInt(res, sizecode);

            return StoreBinaryOpFormat(s, m, res);
        }
        private bool ProcessBUMOD()
        {
            UInt64 s = 0, m = 0, a = 0, b = 0;
            if (!FetchBinaryOpFormat(ref s, ref m, ref a, ref b)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            if (b == 0) { Fail(ErrorCode.ArithmeticError); return false; }

            UInt64 res = Truncate(a % b, sizecode);

            UpdateFlagsInt(res, sizecode);

            return StoreBinaryOpFormat(s, m, res);
        }

        private bool ProcessBSDIV()
        {
            UInt64 s = 0, m = 0, a = 0, b = 0;
            if (!FetchBinaryOpFormat(ref s, ref m, ref a, ref b)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            if (b == 0) { Fail(ErrorCode.ArithmeticError); return false; }

            UInt64 res = Truncate((SignExtend(a, sizecode).MakeSigned() / SignExtend(b, sizecode).MakeSigned()).MakeUnsigned(), sizecode);

            UpdateFlagsInt(res, sizecode);

            return StoreBinaryOpFormat(s, m, res);
        }
        private bool ProcessBSMOD()
        {
            UInt64 s = 0, m = 0, a = 0, b = 0;
            if (!FetchBinaryOpFormat(ref s, ref m, ref a, ref b)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            if (b == 0) { Fail(ErrorCode.ArithmeticError); return false; }

            UInt64 res = Truncate((SignExtend(a, sizecode).MakeSigned() % SignExtend(b, sizecode).MakeSigned()).MakeUnsigned(), sizecode);

            UpdateFlagsInt(res, sizecode);

            return StoreBinaryOpFormat(s, m, res);
        }

        private bool ProcessSL()
        {
            UInt64 s = 0, m = 0, a = 0, b = 0;
            if (!FetchBinaryOpFormat(ref s, ref m, ref a, ref b)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UInt16 sh = (UInt16)(b % SizeBits(sizecode));
            UInt64 res = Truncate(a << sh, sizecode);

            UpdateFlagsInt(res, sizecode);

            return StoreBinaryOpFormat(s, m, res);
        }
        private bool ProcessSR()
        {
            UInt64 s = 0, m = 0, a = 0, b = 0;
            if (!FetchBinaryOpFormat(ref s, ref m, ref a, ref b)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UInt16 sh = (UInt16)(b % SizeBits(sizecode));
            UInt64 res = a >> sh;

            UpdateFlagsInt(res, sizecode);

            return StoreBinaryOpFormat(s, m, res);
        }

        private bool ProcessSAL()
        {
            UInt64 s = 0, m = 0, a = 0, b = 0;
            if (!FetchBinaryOpFormat(ref s, ref m, ref a, ref b)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UInt16 sh = (UInt16)(b % SizeBits(sizecode));
            UInt64 res = Truncate((SignExtend(a, sizecode).MakeSigned() << sh).MakeUnsigned(), sizecode);

            UpdateFlagsInt(res, sizecode);

            return StoreBinaryOpFormat(s, m, res);
        }
        private bool ProcessSAR()
        {
            UInt64 s = 0, m = 0, a = 0, b = 0;
            if (!FetchBinaryOpFormat(ref s, ref m, ref a, ref b)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UInt16 sh = (UInt16)(b % SizeBits(sizecode));
            UInt64 res = Truncate((SignExtend(a, sizecode).MakeSigned() >> sh).MakeUnsigned(), sizecode);

            UpdateFlagsInt(res, sizecode);

            return StoreBinaryOpFormat(s, m, res);
        }

        private bool ProcessRL()
        {
            UInt64 s = 0, m = 0, a = 0, b = 0;
            if (!FetchBinaryOpFormat(ref s, ref m, ref a, ref b)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UInt16 sh = (UInt16)(b % SizeBits(sizecode));
            UInt64 res = Truncate((a << sh) | (a >> ((UInt16)SizeBits(sizecode) - sh)), sizecode);

            UpdateFlagsInt(res, sizecode);

            return StoreBinaryOpFormat(s, m, res);
        }
        private bool ProcessRR()
        {
            UInt64 s = 0, m = 0, a = 0, b = 0;
            if (!FetchBinaryOpFormat(ref s, ref m, ref a, ref b)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UInt16 sh = (UInt16)(b % SizeBits(sizecode));
            UInt64 res = Truncate((a >> sh) | (a << ((UInt16)SizeBits(sizecode) - sh)), sizecode);

            UpdateFlagsInt(res, sizecode);

            return StoreBinaryOpFormat(s, m, res);
        }

        private bool ProcessAND(bool apply = true)
        {
            UInt64 s = 0, m = 0, a = 0, b = 0;
            if (!FetchBinaryOpFormat(ref s, ref m, ref a, ref b)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UInt64 res = a & b;

            UpdateFlagsInt(res, sizecode);

            return apply ? StoreBinaryOpFormat(s, m, res) : true;
        }
        private bool ProcessOR()
        {
            UInt64 s = 0, m = 0, a = 0, b = 0;
            if (!FetchBinaryOpFormat(ref s, ref m, ref a, ref b)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UInt64 res = a | b;

            UpdateFlagsInt(res, sizecode);

            return StoreBinaryOpFormat(s, m, res);
        }
        private bool ProcessXOR()
        {
            UInt64 s = 0, m = 0, a = 0, b = 0;
            if (!FetchBinaryOpFormat(ref s, ref m, ref a, ref b)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UInt64 res = a ^ b;

            UpdateFlagsInt(res, sizecode);

            return StoreBinaryOpFormat(s, m, res);
        }

        private bool ProcessINC()
        {
            UInt64 s = 0, m = 0, a = 0;
            if (!FetchUnaryOpFormat(ref s, ref m, ref a)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UInt64 res = Truncate(a + 1, sizecode);

            UpdateFlagsInt(res, sizecode);
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

            UpdateFlagsInt(res, sizecode);
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

            UpdateFlagsInt(res, sizecode);

            return StoreUnaryOpFormat(s, m, res);
        }
        private bool ProcessNEG()
        {
            UInt64 s = 0, m = 0, a = 0;
            if (!FetchUnaryOpFormat(ref s, ref m, ref a)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UInt64 res = Truncate(~a + 1, sizecode);

            UpdateFlagsInt(res, sizecode);

            return StoreUnaryOpFormat(s, m, res);
        }
        private bool ProcessABS()
        {
            UInt64 s = 0, m = 0, a = 0;
            if (!FetchUnaryOpFormat(ref s, ref m, ref a)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UInt64 res = Positive(a, sizecode) ? a : Truncate(~a + 1, sizecode);

            UpdateFlagsInt(res, sizecode);

            return StoreUnaryOpFormat(s, m, res);
        }
        private bool ProcessCMPZ()
        {
            UInt64 s = 0, m = 0, a = 0;
            if (!FetchUnaryOpFormat(ref s, ref m, ref a)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UpdateFlagsInt(a, sizecode);
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
                case 3: return StoreBinaryOpFormat(s, m, ((Int64)AsDouble(a)).MakeUnsigned());
                case 2: return StoreBinaryOpFormat(s, m, ((Int64)AsFloat(a)).MakeUnsigned());

                default: Fail(ErrorCode.UndefinedBehavior); return false;
            }
        }
        private bool ProcessITOF()
        {
            UInt64 s = 0, m = 0, a = 0;
            if (!FetchUnaryOpFormat(ref s, ref m, ref a)) return false;

            switch ((s >> 2) & 3)
            {
                case 3: return StoreBinaryOpFormat(s, m, DoubleAsUInt64(a.MakeSigned()));
                case 2: return StoreBinaryOpFormat(s, m, FloatAsUInt64(SignExtend(a, 2).MakeSigned()));

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
            if (!FetchBinaryOpFormat(ref s, ref m, ref a, ref b, 1)) return false;
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

            UpdateFlagsInt(res, sizecode);

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

                case OPCode.UX: if (!GetMemAdv(1, ref a)) return false; Registers[a >> 4].Set(a & 3, Registers[a >> 4].Get((a >> 2) & 3)); return true;
                case OPCode.SX: if (!GetMemAdv(1, ref a)) return false; Registers[a >> 4].Set(a & 3, SignExtend(Registers[a >> 4].Get((a >> 2) & 3), (a >> 2) & 3)); return true;

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

                case OPCode.FX:
                    if (!GetMemAdv(1, ref a)) return false;
                    switch ((a >> 2) & 3)
                    {
                        case 2:
                            switch (a & 3)
                            {
                                case 2: return true;
                                case 3: Registers[a >> 4].x64 = DoubleAsUInt64((double)AsFloat(Registers[a >> 4].x32)); return true;

                                default: Fail(ErrorCode.UndefinedBehavior); return false;
                            }
                            
                        case 3:
                            switch (a & 3)
                            {
                                case 2: Registers[a >> 4].x32 = FloatAsUInt64((float)AsDouble(Registers[a >> 4].x64)); return true;
                                case 3: return true;

                                default: Fail(ErrorCode.UndefinedBehavior); return false;
                            }

                        default: Fail(ErrorCode.UndefinedBehavior); return false;
                    }


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
        internal enum PatchError
        {
            None, Unevaluated, Error
        }

        /// <summary>
        /// Represents an expression used to compute a value, with options for using a symbol table for lookup
        /// </summary>
        internal class Expr
        {
            public enum OPs
            {
                None,

                // binary ops

                Mul, Div, Mod,
                Add, Sub,

                SL, SR,

                Less, LessE, Great, GreatE,
                Eq, Neq,

                BitAnd, BitXor, BitOr,
                LogAnd, LogOr,

                // unary ops

                Neg, BitNot, LogNot, Int, Float,

                // special

                Condition, Pair,
                NullCoalesce
            }

            /// <summary>
            /// The operation used to compute the value (or None if leaf)
            /// </summary>
            public OPs OP = OPs.None;

            public Expr Left = null, Right = null;

            private string _Value = null;
            private UInt64 _Result = 0;
            private bool _Evaluated = false, _Floating = false;

            /// <summary>
            /// Gets the string that was evaluated or sets it (and marks as unevaluated)
            /// </summary>
            public string Value
            {
                get { return _Value; }
                set
                {
                    _Value = value; // set the value
                    _Evaluated = false; // mark as unevaluated
                }
            }
            /// <summary>
            /// Assigns this expression to be an evaluated integer
            /// </summary>
            public UInt64 IntResult
            {
                set
                {
                    _Evaluated = true;
                    _Result = value;
                    _Floating = false;
                }
            }
            /// <summary>
            /// Assigns this expression to be an evaluated floating-point value
            /// </summary>
            public double FloatResult
            {
                set
                {
                    _Evaluated = true;
                    _Result = DoubleAsUInt64(value);
                    _Floating = true;
                }
            }

            /// <summary>
            /// Attempts to evaluate the hole, returning true on success
            /// </summary>
            /// <param name="symbols">the symbols table to use for lookup</param>
            /// <param name="res">the resulting value upon success</param>
            /// <param name="floating">flag denoting result is floating-point</param>
            /// <param name="err">error emitted upon failure</param>
            /// <param name="visited">DO NOT PROVIDE THIS</param>
            public bool Evaluate(Dictionary<string, Expr> symbols, out UInt64 res, out bool floating, ref string err, Stack<string> visited = null)
            {
                res = 0; // initialize out params
                floating = false;

                UInt64 L, R; // parsing locations for left and right subtrees
                bool LF, RF;
                
                // switch through op
                switch (OP)
                {
                    // value
                    case OPs.None:
                        // if this has already been evaluated, return the cached result
                        if (_Evaluated) { res = _Result; floating = _Floating; return true; }

                        // try several integral radicies
                        try
                        {
                            // prefixes only allowed for unsigned values (raw from parsing)
                            if (Value.StartsWith("0x")) res = Convert.ToUInt64(Value.Substring(2), 16);
                            else if (Value.StartsWith("0b")) res = Convert.ToUInt64(Value.Substring(2), 2);
                            else if (Value[0] == '0' && Value.Length > 1) res = Convert.ToUInt64(Value.Substring(1), 8);
                            else res = Convert.ToUInt64(Value, 10);

                            break;
                        }
                        catch (Exception) { }

                        // if those fail, try floating-point
                        if (double.TryParse(Value, out double f)) { res = DoubleAsUInt64(f); floating = true; break; }

                        // if it's a character
                        if (Value[0] == '\'')
                        {
                            if (Value.Length != 3 || Value[2] != '\'') { err = $"Ill-formed character literal encountered \"{Value}\""; return false; }

                            res = Value[1];
                            break;
                        }

                        // otherwise if it's a defined symbol
                        if (symbols.TryGetValue(Value, out Expr hole))
                        {
                            // create the visited stack if it wasn't already
                            if (visited == null) visited = new Stack<string>();

                            // fail if looking up a symbol we've already looked up (infinite recursion)
                            if (visited.Contains(Value)) { err = $"Cyclic dependence on \"{Value}\" encountered"; return false; }

                            visited.Push(Value); // mark value as visited

                            // if we can't evaluate it, fail
                            if (!hole.Evaluate(symbols, out res, out floating, ref err)) { err = $"Failed to evaluate referenced symbol \"{Value}\"\n-> {err}"; return false; }

                            visited.Pop(); // unmark value (must be done for diamond expressions i.e. a=b+c, b=d, c=d, d=0)

                            break; // break so we can resolve the reference
                        }

                        err = $"Failed to parse \"{Value}\" as a number or defined symbol";
                        return false;

                    // -- operators -- //

                    // binary ops

                    case OPs.Mul:
                        if (!Left.Evaluate(symbols, out L, out LF, ref err) || !Right.Evaluate(symbols, out R, out RF, ref err)) return false;
                        if (LF || RF) { res = DoubleAsUInt64((LF ? AsDouble(L) : L.MakeSigned()) * (RF ? AsDouble(R) : R.MakeSigned())); floating = true; }
                        else res = L * R;
                        break;
                    case OPs.Div:
                        if (!Left.Evaluate(symbols, out L, out LF, ref err) || !Right.Evaluate(symbols, out R, out RF, ref err)) return false;
                        if (LF || RF) { res = DoubleAsUInt64((LF ? AsDouble(L) : L.MakeSigned()) / (RF ? AsDouble(R) : R.MakeSigned())); floating = true; }
                        else res = (L.MakeSigned() / R.MakeSigned()).MakeUnsigned();
                        break;
                    case OPs.Mod:
                        if (!Left.Evaluate(symbols, out L, out LF, ref err) || !Right.Evaluate(symbols, out R, out RF, ref err)) return false;
                        if (LF || RF) { res = DoubleAsUInt64((LF ? AsDouble(L) : L.MakeSigned()) % (RF ? AsDouble(R) : R.MakeSigned())); floating = true; }
                        else res = (L.MakeSigned() % R.MakeSigned()).MakeUnsigned();
                        break;
                    case OPs.Add:
                        if (!Left.Evaluate(symbols, out L, out LF, ref err) || !Right.Evaluate(symbols, out R, out RF, ref err)) return false;
                        if (LF || RF) { res = DoubleAsUInt64((LF ? AsDouble(L) : L.MakeSigned()) + (RF ? AsDouble(R) : R.MakeSigned())); floating = true; }
                        else res = L + R;
                        break;
                    case OPs.Sub:
                        if (!Left.Evaluate(symbols, out L, out LF, ref err) || !Right.Evaluate(symbols, out R, out RF, ref err)) return false;
                        if (LF || RF) { res = DoubleAsUInt64((LF ? AsDouble(L) : L.MakeSigned()) - (RF ? AsDouble(R) : R.MakeSigned())); floating = true; }
                        else res = L - R;
                        break;

                    case OPs.SL:
                        if (!Left.Evaluate(symbols, out L, out LF, ref err) || !Right.Evaluate(symbols, out R, out RF, ref err)) return false;
                        res = L << (ushort)R; floating = LF || RF;
                        break;
                    case OPs.SR:
                        if (!Left.Evaluate(symbols, out L, out LF, ref err) || !Right.Evaluate(symbols, out R, out RF, ref err)) return false;
                        res = L >> (ushort)R; floating = LF || RF;
                        break;

                    case OPs.Less:
                        if (!Left.Evaluate(symbols, out L, out LF, ref err) || !Right.Evaluate(symbols, out R, out RF, ref err)) return false;
                        if (LF || RF) res = (LF ? AsDouble(L) : L.MakeSigned()) < (RF ? AsDouble(R) : R.MakeSigned()) ? 1 : 0ul;
                        else res = L.MakeSigned() < R.MakeSigned() ? 1 : 0ul;
                        break;
                    case OPs.LessE:
                        if (!Left.Evaluate(symbols, out L, out LF, ref err) || !Right.Evaluate(symbols, out R, out RF, ref err)) return false;
                        if (LF || RF) res = (LF ? AsDouble(L) : L.MakeSigned()) <= (RF ? AsDouble(R) : R.MakeSigned()) ? 1 : 0ul;
                        else res = L.MakeSigned() <= R.MakeSigned() ? 1 : 0ul;
                        break;
                    case OPs.Great:
                        if (!Left.Evaluate(symbols, out L, out LF, ref err) || !Right.Evaluate(symbols, out R, out RF, ref err)) return false;
                        if (LF || RF) res = (LF ? AsDouble(L) : L.MakeSigned()) > (RF ? AsDouble(R) : R.MakeSigned()) ? 1 : 0ul;
                        else res = L.MakeSigned() > R.MakeSigned() ? 1 : 0ul;
                        break;
                    case OPs.GreatE:
                        if (!Left.Evaluate(symbols, out L, out LF, ref err) || !Right.Evaluate(symbols, out R, out RF, ref err)) return false;
                        if (LF || RF) res = (LF ? AsDouble(L) : L.MakeSigned()) >= (RF ? AsDouble(R) : R.MakeSigned()) ? 1 : 0ul;
                        else res = L.MakeSigned() >= R.MakeSigned() ? 1 : 0ul;
                        break;

                    case OPs.Eq:
                        if (!Left.Evaluate(symbols, out L, out LF, ref err) || !Right.Evaluate(symbols, out R, out RF, ref err)) return false;
                        if (LF || RF) res = (LF ? AsDouble(L) : L.MakeSigned()) == (RF ? AsDouble(R) : R.MakeSigned()) ? 1 : 0ul;
                        else res = L == R ? 1 : 0ul;
                        break;
                    case OPs.Neq:
                        if (!Left.Evaluate(symbols, out L, out LF, ref err) || !Right.Evaluate(symbols, out R, out RF, ref err)) return false;
                        if (LF || RF) res = (LF ? AsDouble(L) : L.MakeSigned()) != (RF ? AsDouble(R) : R.MakeSigned()) ? 1 : 0ul;
                        else res = L != R ? 1 : 0ul;
                        break;

                    case OPs.BitAnd:
                        if (!Left.Evaluate(symbols, out L, out LF, ref err) || !Right.Evaluate(symbols, out R, out RF, ref err)) return false;
                        res = L & R; floating = LF || RF;
                        break;
                    case OPs.BitXor:
                        if (!Left.Evaluate(symbols, out L, out LF, ref err) || !Right.Evaluate(symbols, out R, out RF, ref err)) return false;
                        res = L ^ R; floating = LF || RF;
                        break;
                    case OPs.BitOr:
                        if (!Left.Evaluate(symbols, out L, out LF, ref err) || !Right.Evaluate(symbols, out R, out RF, ref err)) return false;
                        res = L | R; floating = LF || RF;
                        break;

                    case OPs.LogAnd:
                        if (!Left.Evaluate(symbols, out L, out LF, ref err) || L != 0 && !Right.Evaluate(symbols, out L, out LF, ref err)) return false;
                        res = L != 0 ? 1 : 0ul;
                        break;
                    case OPs.LogOr:
                        if (!Left.Evaluate(symbols, out L, out LF, ref err) || L == 0 && !Right.Evaluate(symbols, out L, out LF, ref err)) return false;
                        res = L != 0 ? 1 : 0ul;
                        break;

                    // unary ops

                    case OPs.Neg:
                        if (!Left.Evaluate(symbols, out L, out LF, ref err)) return false;
                        res = LF ? DoubleAsUInt64(-AsDouble(L)) : ~L + 1; floating = LF;
                        break;
                    case OPs.BitNot:
                        if (!Left.Evaluate(symbols, out L, out LF, ref err)) return false;
                        res = ~L; floating = LF;
                        break;
                    case OPs.LogNot:
                        if (!Left.Evaluate(symbols, out L, out LF, ref err)) return false;
                        res = L == 0 ? 1 : 0ul;
                        break;
                    case OPs.Int:
                        if (!Left.Evaluate(symbols, out L, out LF, ref err)) return false;
                        res = LF ? ((Int64)AsDouble(L)).MakeUnsigned() : L;
                        break;
                    case OPs.Float:
                        if (!Left.Evaluate(symbols, out L, out LF, ref err)) return false;
                        res = LF ? L : DoubleAsUInt64((double)L.MakeSigned());
                        floating = true;
                        break;

                    // misc

                    case OPs.NullCoalesce:
                        if (!Left.Evaluate(symbols, out res, out floating, ref err) || res == 0 && !Right.Evaluate(symbols, out res, out floating, ref err)) return false;
                        break;
                    case OPs.Condition:
                        if (!Left.Evaluate(symbols, out L, out LF, ref err)) return false;
                        if (L != 0) { if (!Right.Left.Evaluate(symbols, out res, out floating, ref err)) return false; }
                        else { if (!Right.Right.Evaluate(symbols, out res, out floating, ref err)) return false; }
                        break;

                    default: err = "Unknown operation"; return false;
                }

                // since we got a value, turn this into a leaf so future accesses are faster
                OP = OPs.None; Left = Right = null;

                // cache the result
                _Evaluated = true;
                _Result = res;
                _Floating = floating;

                return true;
            }

            /// <summary>
            /// Creates a replica of this expression tree
            public Expr Clone()
            {
                return new Expr()
                {
                    OP = OP,
                    Left = Left?.Clone(),
                    Right = Right?.Clone(),

                    _Value = _Value,

                    _Evaluated = _Evaluated,
                    _Result = _Result,
                    _Floating = _Floating
                };
            }
            /// <summary>
            /// Creates a replica of this expression tree, replacing a string with another string
            /// </summary>
            /// <param name="from">the string to find</param>
            /// <param name="to">the string to replace with</param>
            public Expr Clone(string from, string to)
            {
                Expr temp = new Expr() { OP = OP, Left = Left?.Clone(from, to), Right = Right?.Clone(from, to) };

                // if this is a replacement, swap for new value
                if (Value == from)
                {
                    temp._Value = to;
                    temp._Evaluated = false;
                }
                // otherwise copy cache data
                else
                {
                    temp._Value = _Value;

                    temp._Evaluated = _Evaluated;
                    temp._Result = _Result;
                    temp._Floating = _Floating;
                }

                return temp;
            }
            /// <summary>
            /// Creates a replica of this expression tree, replacing a string with a value
            /// </summary>
            /// <param name="from">the string to find</param>
            /// <param name="to">the value to replace with</param>
            /// <param name="floating">flag if the specified value is floating-point</param>
            public Expr Clone(string from, UInt64 to, bool floating)
            {
                Expr temp = new Expr() { OP = OP, Left = Left?.Clone(from, to, floating), Right = Right?.Clone(from, to, floating) };

                if (Value == from)
                {
                    temp._Evaluated = true;
                    temp._Result = to;
                    temp._Floating = floating;
                }
                else
                {
                    temp._Value = _Value;

                    temp._Evaluated = _Evaluated;
                    temp._Result = _Result;
                    temp._Floating = _Floating;
                }

                return temp;
            }

            /// <summary>
            /// Finds the value in the specified expression tree. Returns true on success
            /// </summary>
            /// <param name="value">the value to find</param>
            /// <param name="path">the resulting path (with the root at the bottom of the stack and the found node at the top)</param>
            public bool Find(string value, Stack<Expr> path)
            {
                path.Push(this);

                if (OP == OPs.None)
                {
                    if (value == Value) return true;
                }
                else
                {
                    if (Left.Find(value, path) || Right != null && Right.Find(value, path)) return true;
                }

                path.Pop();
                return false;
            }
            /// <summary>
            /// Finds the value in the specified expression tree. Returns it on success, otherwise null
            /// </summary>
            /// <param name="value">the found node or null</param>
            public Expr Find(string value)
            {
                if (OP == OPs.None) return Value == value ? this : null;
                else return Left.Find(value) ?? Right?.Find(value);
            }

            private void _ToString(StringBuilder b)
            {
                if (OP == 0) b.Append($"({(_Evaluated ? (_Floating ? AsDouble(_Result).ToString("e17") : _Result.MakeSigned().ToString()) : Value)})");
                else
                {
                    Left._ToString(b);
                    if (Right != null) Right._ToString(b);
                    
                    b.Append(OP);
                }
            }
            public override string ToString()
            {
                StringBuilder b = new StringBuilder();
                _ToString(b);
                return b.ToString();
            }

            /// <summary>
            /// Swaps the contents of the expressions
            /// </summary>
            public static void Swap(Expr a, Expr b)
            {
                Utility.Swap(ref a.OP, ref b.OP);

                Utility.Swap(ref a.Left, ref b.Left);
                Utility.Swap(ref a.Right, ref b.Right);

                Utility.Swap(ref a._Value, ref b._Value);
                Utility.Swap(ref a._Result, ref b._Result);
                Utility.Swap(ref a._Evaluated, ref b._Evaluated);
                Utility.Swap(ref a._Floating, ref b._Floating);
            }
        }
        internal class HoleData
        {
            /// <summary>
            /// The local address of the hole in the file
            /// </summary>
            public UInt64 Address;
            /// <summary>
            /// The size of the hole
            /// </summary>
            public UInt64 Size;
            
            /// <summary>
            /// The line where this hole was created
            /// </summary>
            public int Line;
            /// <summary>
            /// The expression that represents this hole's value
            /// </summary>
            public Expr Expr;
        }
        public class ObjectFile
        {
            /// <summary>
            /// The symbols defined in the file
            /// </summary>
            internal Dictionary<string, Expr> Symbols = new Dictionary<string, Expr>();
            /// <summary>
            /// All the holes that need to be patched by the linker
            /// </summary>
            internal List<HoleData> Holes = new List<HoleData>();

            /// <summary>
            /// The list of exported symbol names
            /// </summary>
            internal List<string> GlobalSymbols = new List<string>();
            /// <summary>
            /// The executable data
            /// </summary>
            internal List<byte> Data = new List<byte>();
        }

        /// <summary>
        /// Holds all the variables used during assembly
        /// </summary>
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

            public UInt64 time;
        }

        /// <summary>
        /// The maximum value for an emission multiplier
        /// </summary>
        public const UInt64 EmissionMaxMultiplier = 1000000;

        private static bool Write<T>(T arr, UInt64 pos, UInt64 size, UInt64 val) where T : IList<byte>
        {
            // make sure we're not exceeding memory bounds
            if (pos < 0 || pos + size > (UInt64)arr.LongCount()) return false;

            // write the value (little-endian)
            for (ushort i = 0; i < size; ++i)
                arr[(int)pos + i] = (byte)(val >> (8 * i)); // POTENTIAL SIZE LIMITATION

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
        private static bool TryAppendHole(AssembleArgs args, UInt64 size, Expr hole, int type = 3)
        {
            string err = null; // evaluation error

            // create the hole data
            HoleData data = new HoleData() { Address = (UInt64)args.file.Data.LongCount(), Size = size, Line = args.line, Expr = hole };
            // write a dummy (all 1's for easy manual identification)
            AppendVal(args, size, 0xffffffffffffffff);

            // try to patch it
            switch (TryPatchHole(args.file.Symbols, args.file.Data, data, ref err))
            {
                case PatchError.None: break;
                case PatchError.Unevaluated: args.file.Holes.Add(data); break;
                case PatchError.Error: args.err = new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {args.line}: Error encountered while patching expression\n-> {err}"); return false;

                default: throw new ArgumentException("Unknown patch error encountered");
            }

            return true;
        }
        private static bool TryAppendAddress(AssembleArgs args, UInt64 a, UInt64 b, Expr hole)
        {
            // [1: literal][3: m1][1: -m2][3: m2]   ([4: r1][4: r2])   ([64: imm])
            AppendVal(args, 1, a);
            if ((a & 0x77) != 0) AppendVal(args, 1, b);
            if ((a & 0x80) != 0) { if (!TryAppendHole(args, 8, hole)) { args.err = new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {args.line}: Failed to append address base\n-> {args.err.Item2}"); return false; } }
            
            return true;
        }

        private static PatchError TryPatchHole<T>(Dictionary<string, Expr> symbols, T res, HoleData data, ref string err) where T : IList<byte>
        {
            // if we can fill it immediately, do so
            if (data.Expr.Evaluate(symbols, out UInt64 val, out bool floating, ref err))
            {
                // if it's floating-point
                if (floating)
                {
                    // only 64-bit and 32-bit are supported
                    switch (data.Size)
                    {
                        case 8: Write(res, data.Address, 8, val); break;
                        case 4: Write(res, data.Address, 4, FloatAsUInt64((float)AsDouble(val))); break;

                        default: err = $"line {data.Line}: Attempt to use unsupported floating-point size"; return PatchError.Error;
                    }
                }
                // otherwise it's integral
                else Write(res, data.Address, data.Size, val);
            }
            else { err = $"line {data.Line}: Failed to evaluate expression\n-> {err}"; return PatchError.Unevaluated; }

            // successfully patched
            return PatchError.None;
        }

        private static bool SplitLine(AssembleArgs args)
        {
            // (label: label: ...) (op(:size) (arg, arg, ...))

            int pos = 0, end; // position in line parsing
            int quote;        // index of openning quote in args

            List<string> tokens = new List<string>();
            StringBuilder b = new StringBuilder();

            // parse labels
            for (; pos < args.rawline.Length; pos = end)
            {
                // skip leading white space
                for (; pos < args.rawline.Length && char.IsWhiteSpace(args.rawline[pos]); ++pos) ;
                // get a white space-delimited token
                for (end = pos; end < args.rawline.Length && !char.IsWhiteSpace(args.rawline[end]); ++end) ;

                // if it's a label, add to tokens
                if (pos != end && args.rawline[end - 1] == ':') tokens.Add(args.rawline.Substring(pos, end - pos - 1));
                // otherwise we're done with labels
                else break; // break ensures we also keep pos pointing to start of next section
            }
            args.label_defs = tokens.ToArray(); // dump tokens as label defs
            tokens.Clear(); // empty tokens for reuse

            // parse op
            if (pos < args.rawline.Length)
            {
                // get up till size separator or white space
                for (end = pos; end < args.rawline.Length && args.rawline[end] != ':' && !char.IsWhiteSpace(args.rawline[end]); ++end) ;

                // make sure we got a well-formed op
                if (pos == end) { args.err = new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {args.line}: Operation size specification encountered without an operation"); return false; }
                
                // save this as op
                args.op = args.rawline.Substring(pos, end - pos);

                // if we got a size specification
                if (end < args.rawline.Length && args.rawline[end] == ':')
                {
                    pos = end + 1; // position to beginning of size specification

                    // if starting parenthetical section
                    if (pos < args.rawline.Length && args.rawline[pos] == '(')
                    {
                        int depth = 1; // parenthetical depth

                        // get till depth of zero
                        for (end = pos + 1; end < args.rawline.Length && depth > 0; ++end)
                        {
                            if (args.rawline[end] == '(') ++depth;
                            else if (args.rawline[end] == ')') --depth;
                        }

                        if (depth != 0) { args.err = new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {args.line}: Mismatched parenthesis encountered in operation size specification"); return false; }
                    }
                    // ohterwise standard imm
                    else
                    {
                        // take all legal chars
                        for (end = pos; end < args.rawline.Length && (args.rawline[end] == '_' || char.IsLetterOrDigit(args.rawline[end])); ++end) ;
                    }

                    // parse the read size code
                    if (!TryParseSizecode(args, args.rawline.Substring(pos, end - pos).RemoveWhiteSpace(), out args.sizecode))
                    { args.err = new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {args.line}: Failed to parse operation size specification\n-> {args.err.Item2}"); return false; }
                }
                // otherwise use default size (64-bit)
                else args.sizecode = 3;

                pos = end; // pass parsed section before next section
            }
            // otherwise there is no op
            else args.op = string.Empty;

            // parse the rest of the line as comma-separated tokens
            for (; pos < args.rawline.Length; ++pos)
            {
                // skip leading white space
                for (; pos < args.rawline.Length && char.IsWhiteSpace(args.rawline[pos]); ++pos) ;
                // when pos reaches end of token, we're done parsing
                if (pos >= args.rawline.Length) break;

                b.Clear(); // clear the string builder

                // find the next terminator (comma-separated)
                for (quote = -1; pos < args.rawline.Length && (args.rawline[pos] != ',' || quote >= 0); ++pos)
                {
                    if (args.rawline[pos] == '"' || args.rawline[pos] == '\'')
                        quote = quote < 0 ? pos : (args.rawline[pos] == args.rawline[quote] ? -1 : quote);

                    // omit white space unless in a quote
                    if (quote >= 0 || !char.IsWhiteSpace(args.rawline[pos])) b.Append(args.rawline[pos]);
                }

                // make sure we closed any quotations
                if (quote >= 0) { args.err = new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {args.line}: Unmatched quotation encountered in argument list"); return false; }

                // make sure arg isn't empty
                if (b.Length == 0) { args.err = new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {args.line}: Empty operation argument encountered"); return false; }

                // add this token
                tokens.Add(b.ToString());
            }
            // output tokens to assemble args
            args.args = tokens.ToArray();

            // successfully parsed line
            return true;
        }

        private static readonly Dictionary<Expr.OPs, int> Precedence = new Dictionary<Expr.OPs, int>()
        {
            { Expr.OPs.Mul, 5 },
            { Expr.OPs.Div, 5 },
            { Expr.OPs.Mod, 5 },

            { Expr.OPs.Add, 6 },
            { Expr.OPs.Sub, 6 },

            { Expr.OPs.SL, 7 },
            { Expr.OPs.SR, 7 },

            { Expr.OPs.Less, 9 },
            { Expr.OPs.LessE, 9 },
            { Expr.OPs.Great, 9 },
            { Expr.OPs.GreatE, 9 },

            { Expr.OPs.Eq, 10 },
            { Expr.OPs.Neq, 10 },

            { Expr.OPs.BitAnd, 11 },
            { Expr.OPs.BitXor, 12 },
            { Expr.OPs.BitOr, 13 },
            { Expr.OPs.LogAnd, 14 },
            { Expr.OPs.LogOr, 15 },

            { Expr.OPs.NullCoalesce, 99 },
            { Expr.OPs.Pair, 100 },
            { Expr.OPs.Condition, 100 }
        };
        private static readonly List<char> UnaryOps = new List<char>() { '+', '-', '~', '!', '*', '/' };

        private static bool TryGetOp(string token, int pos, out Expr.OPs op, out int oplen)
        {
            // default to invalid op
            op = Expr.OPs.None;
            oplen = 0;

            // try to take as many characters as possible (greedy)
            if (pos + 2 <= token.Length)
            {
                oplen = 2; // record oplen
                switch (token.Substring(pos, 2))
                {
                    case "<<": op = Expr.OPs.SL; return true;
                    case ">>": op = Expr.OPs.SR; return true;

                    case "<=": op = Expr.OPs.LessE; return true;
                    case ">=": op = Expr.OPs.GreatE; return true;

                    case "==": op = Expr.OPs.Eq; return true;
                    case "!=": op = Expr.OPs.Neq; return true;

                    case "&&": op = Expr.OPs.LogAnd; return true;
                    case "||": op = Expr.OPs.LogOr; return true;

                    case "??": op = Expr.OPs.NullCoalesce; return true;
                }
            }
            if (pos + 1 <= token.Length)
            {
                oplen = 1; // record oplen
                switch (token[pos])
                {
                    case '*': op = Expr.OPs.Mul; return true;
                    case '/': op = Expr.OPs.Div; return true;
                    case '%': op = Expr.OPs.Mod; return true;

                    case '+': op = Expr.OPs.Add; return true;
                    case '-': op = Expr.OPs.Sub; return true;

                    case '<': op = Expr.OPs.Less; return true;
                    case '>': op = Expr.OPs.Great; return true;

                    case '&': op = Expr.OPs.BitAnd; return true;
                    case '^': op = Expr.OPs.BitXor; return true;
                    case '|': op = Expr.OPs.BitOr; return true;

                    case '?': op = Expr.OPs.Condition; return true;
                    case ':': op = Expr.OPs.Pair; return true;
                }
            }

            // if nothing found, fail
            return false;
        }
        private static bool TryParseImm(AssembleArgs args, string token, out Expr hole)
        {
            hole = null; // initially-nulled result

            Expr temp; // temporary for node creation
            
            int pos = 0, end; // position in token
            int depth;        // parenthesis depth

            bool binPair = false;          // marker if tree contains complete binary pairs (i.e. N+1 values and N binary ops)
            int unpaired_conditionals = 0; // number of unpaired conditional ops

            Expr.OPs op = Expr.OPs.None; // extracted binary op (initialized so compiler doesn't complain)
            int oplen = 0;               // length of operator found (in characters)

            string err = null; // error location for hole evaluation

            Stack<char> unaryOps = new Stack<char>(8); // holds unary ops for processing
            Stack<Expr> stack = new Stack<Expr>();     // the stack used to manage operator precedence rules

            // top of stack shall be refered to as current

            stack.Push(null); // stack will always have a null at its base (simplifies code slightly)

            if (token.Length == 0) { args.err = new Tuple<AssembleError, string>(AssembleError.InvalidLabel, $"line {args.line}: Empty expression encountered"); return false; }

            while (pos < token.Length)
            {
                // -- read val(op) -- //

                depth = 0; // initial depth of 0

                // consume unary ops
                for (; pos < token.Length && UnaryOps.Contains(token[pos]); ++pos) unaryOps.Push(token[pos]);
                
                // find next binary op
                for (end = pos; end < token.Length && (depth > 0 || !TryGetOp(token, end, out op, out oplen)); ++end)
                {
                    if (token[end] == '(') ++depth;
                    else if (token[end] == ')') --depth;

                    // can't ever have negative depth
                    if (depth < 0) { args.err = new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {args.line}: Mismatched parenthesis \"{token}\""); return false; }
                }
                // if depth isn't back to 0, there was a parens mismatch
                if (depth != 0) { args.err = new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {args.line}: Mismatched parenthesis \"{token}\""); return false; }
                // if pos == end we'll have an empty token
                if (pos == end) { args.err = new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {args.line}: Empty token encountered in expression \"{token}\""); return false; }

                // -- process value -- //
                {
                    // -- convert value to an expression tree --

                    // if sub-expression
                    if (token[pos] == '(')
                    {
                        // parse it into temp
                        if (!TryParseImm(args, token.Substring(pos + 1, end - pos - 2), out temp)) return false;
                    }
                    // otherwise is value
                    else
                    {
                        // get the value to insert
                        string val = token.Substring(pos, end - pos);

                        // mutate it
                        if (!MutateLabel(args, ref val)) { args.err = new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {args.line}: Failed to parse imm \"{token}\"\n-> {args.err.Item2}"); return false; }

                        // create the hole for it
                        temp = new Expr() { Value = val };

                        // it either needs to be evaluatable or a valid label name
                        if (!temp.Evaluate(args.file.Symbols, out UInt64 res, out bool floating, ref err) && !IsValidLabel(val))
                        { args.err = new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {args.line}: Failed to resolve symbol as a valid imm \"{val}\"\n-> {err}"); return false; }
                    }

                    // -- handle unary op by modifying subtree --

                    // handle the unary ops in terms of binary ops (stack provides right-to-left evaluation)
                    while (unaryOps.Count > 0)
                    {
                        char uop = unaryOps.Pop();
                        switch (uop)
                        {
                            case '+': break;
                            case '-': temp = new Expr() { OP = Expr.OPs.Neg, Left = temp }; break;
                            case '~': temp = new Expr() { OP = Expr.OPs.BitNot, Left = temp }; break;
                            case '!': temp = new Expr() { OP = Expr.OPs.LogNot, Left = temp }; break;
                            case '*': temp = new Expr() { OP = Expr.OPs.Float, Left = temp }; break;
                            case '/': temp = new Expr() { OP = Expr.OPs.Int, Left = temp }; break;

                            default: throw new NotImplementedException($"unary op \'{uop}\' not implemented");
                        }
                    }

                    // -- append subtree to main tree --

                    // if no tree yet, use this one
                    if (hole == null) hole = temp;
                    // otherwise append to current (guaranteed to be defined by second pass)
                    else
                    {
                        //if (stack.Peek().Left == null) stack.Peek().Left = temp; else stack.Peek().Right = temp;

                        // put it in the right (guaranteed by this algorithm to be empty)
                        stack.Peek().Right = temp;
                    }

                    // flag as a valid binary pair
                    binPair = true;
                }

                // -- process op -- //
                if (end < token.Length)
                {
                    // ternary conditional has special rules
                    if (op == Expr.OPs.Pair)
                    {
                        // seek out nearest conditional without a pair
                        for (; stack.Peek() != null && (stack.Peek().OP != Expr.OPs.Condition || stack.Peek().Right.OP == Expr.OPs.Pair); stack.Pop()) ;

                        // if we didn't find anywhere to put it, this is an error
                        if (stack.Peek() == null) { args.err = new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {args.line}: Expression contained a ternary conditional pair without a corresponding condition \"{token}\""); return false; }
                    }
                    // right-to-left operators
                    else if (op == Expr.OPs.Condition)
                    {
                        // wind current up to correct precedence (right-to-left evaluation, so don't skip equal precedence)
                        for (; stack.Peek() != null && Precedence[stack.Peek().OP] < Precedence[op]; stack.Pop()) ;
                    }
                    // left-to-right operators
                    else
                    {
                        // wind current up to correct precedence (left-to-right evaluation, so also skip equal precedence)
                        for (; stack.Peek() != null && Precedence[stack.Peek().OP] <= Precedence[op]; stack.Pop()) ;
                    }

                    // if we have a valid current
                    if (stack.Peek() != null)
                    {
                        // splice in the new operator, moving current's right sub-tree to left of new node
                        stack.Push(stack.Peek().Right = new Expr() { OP = op, Left = stack.Peek().Right });
                    }
                    // otherwise we'll have to move the root
                    else
                    {
                        // splice in the new operator, moving entire tree to left of new node
                        stack.Push(hole = new Expr() { OP = op, Left = hole });
                    }

                    binPair = false; // flag as invalid binary pair

                    // update unpaired conditionals
                    if (op == Expr.OPs.Condition) ++unpaired_conditionals;
                    else if (op == Expr.OPs.Pair) --unpaired_conditionals;
                }

                // pass last delimiter
                pos = end + oplen;
            }

            // handle binary pair mismatch
            if (!binPair) { args.err = new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {args.line}: Expression contained a mismatched binary op: \"{token}\""); return false; }

            // make sure all conditionals were matched
            if (unpaired_conditionals != 0) { args.err = new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {args.line}: Expression contained {unpaired_conditionals} incomplete ternary {(unpaired_conditionals == 1 ? "conditional" : "conditionals")}"); return false; }

            return true;
        }
        private static bool TryParseInstantImm(AssembleArgs args, string token, out UInt64 res, out bool floating)
        {
            string err = null; // error location for evaluation

            if (!TryParseImm(args, token, out Expr hole)) { res = 0; floating = false; return false; }
            if (!hole.Evaluate(args.file.Symbols, out res, out floating, ref err)) { args.err = new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {args.line}: Failed to parse instant imm \"{token}\"\n-> {err}"); return false; }

            return true;
        }

        private static bool TryParseRegister(AssembleArgs args, string token, out UInt64 res)
        {
            res = 0;

            // registers prefaced with $
            if (token.Length < 2 || token[0] != '$') { args.err = new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {args.line}: Invalid register format encountered \"{token}\""); return false; }

            int end = 0; // ending of register expression token

            // if this starts parenthetical region
            if (token[1] == '(')
            {
                int depth = 1; // depth of 1

                // start searching for ending parens after first parens
                for (end = 2; end < token.Length && depth > 0; ++end)
                {
                    if (token[end] == '(') ++depth;
                    else if (token[end] == ')') --depth;
                }

                // make sure we reached zero depth
                if (depth != 0) { args.err = new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {args.line}: Mismatched parenthesis in register expression \"{token.Substring(1, end - 1)}\""); return false; }
            }
            // otherwise normal symbol
            else
            {
                // take all legal chars
                for (end = 1; end < token.Length && (char.IsLetterOrDigit(token[end]) || token[end] == '_'); ++end) ;
            }

            // make sure we consumed the entire string
            if (end != token.Length) { args.err = new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {args.line}: Expressions used as register ids must be parenthesized"); return false; }

            // register index must be instant imm
            if (!TryParseInstantImm(args, token.Substring(1, end - 1), out res, out bool floating)) { args.err = new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {args.line}: Failed to parse register index \"{token.Substring(1, end - 1)}\"\n-> {args.err.Item2}"); return false; }

            // ensure not floating and in proper range
            if (floating) { args.err = new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {args.line}: Attempt to use floating point value to specify register \"{token}\""); return false; }
            if (res >= 16) { args.err = new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {args.line}: Register index out of range \"{token}\" (evaluated to {res})"); return false; }

            return true;
        }
        private static bool TryParseSizecode(AssembleArgs args, string token, out UInt64 res)
        {
            // size code must be instant imm
            if (!TryParseInstantImm(args, token, out res, out bool floating)) { args.err = new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {args.line}: Failed to parse size code \"{token}\"\n-> {args.err.Item2}"); return false; }

            // ensure not floating
            if (floating) { args.err = new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {args.line}: Attempt to use floating point value to specify register size \"{token}\" -> {res}"); return false; }

            // convert to size code
            switch (res)
            {
                case 8: res = 0; return true;
                case 16: res = 1; return true;
                case 32: res = 2; return true;
                case 64: res = 3; return true;

                default: args.err = new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {args.line}: Invalid register size: {res}"); return false;
            }
        }
        private static bool TryParseMultcode(AssembleArgs args, string token, out UInt64 res)
        {
            // mult code must be instant imm
            if (!TryParseInstantImm(args, token, out res, out bool floating)) { args.err = new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {args.line}: Failed to parse mult code \"{token}\"\n-> {args.err.Item2}"); return false; }

            // ensure not floating
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

                default: args.err = new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {args.line}: Invalid size multiplier: {res}"); return false;
            }
        }

        private static bool TryParseAddressReg(AssembleArgs args, string label, ref Expr hole, out UInt64 m, out bool neg)
        {
            m = 0; neg = false; // initialize out params

            Stack<Expr> path = new Stack<Expr>();
            List<Expr> list = new List<Expr>();

            string err = string.Empty; // evaluation error

            // while we can find this symbol
            while (hole.Find(label, path))
            {
                // move path into list
                while (path.Count > 0) list.Add(path.Pop());

                // if it doesn't have a mult section
                if (list.Count == 1 || list.Count > 1 && list[1].OP != Expr.OPs.Mul)
                {
                    // add in a multiplier of 1
                    list[0].OP = Expr.OPs.Mul;
                    list[0].Left = new Expr() { Value = "1" };
                    list[0].Right = new Expr() { Value = list[0].Value };

                    // insert new register location as beginning of path
                    list.Insert(0, list[0].Right);
                }

                // start 2 above (just above regular mult code)
                for (int i = 2; i < list.Count; )
                {
                    switch (list[i].OP)
                    {
                        case Expr.OPs.Add: case Expr.OPs.Sub: case Expr.OPs.Neg: ++i; break;

                        case Expr.OPs.Mul:
                            {
                                // toward leads to register, mult leads to mult value
                                Expr toward = list[i - 1], mult = list[i].Left == list[i - 1] ? list[i].Right : list[i].Left;

                                // if pos is add/sub, we need to distribute
                                if (toward.OP == Expr.OPs.Add || toward.OP == Expr.OPs.Sub)
                                {
                                    // swap operators with toward
                                    list[i].OP = toward.OP;
                                    toward.OP = Expr.OPs.Mul;

                                    // create the distribution node
                                    Expr temp = new Expr() { OP = Expr.OPs.Mul, Left = mult };

                                    // compute right and transfer mult to toward
                                    if (toward.Left == list[i - 2]) { temp.Right = toward.Right; toward.Right = mult; }
                                    else { temp.Right = toward.Left; toward.Left = mult; }

                                    // add it in
                                    if (list[i].Left == mult) list[i].Left = temp; else list[i].Right = temp;
                                }
                                // if pos is mul, we need to combine with pre-existing mult code
                                else if (toward.OP == Expr.OPs.Mul)
                                {
                                    // create the combination node
                                    Expr temp = new Expr() { OP = Expr.OPs.Mul, Left = mult, Right = toward.Left == list[i - 2] ? toward.Right : toward.Left };

                                    // add it in
                                    if (list[i].Left == mult)
                                    {
                                        list[i].Left = temp; // replace mult with combination
                                        list[i].Right = list[i - 2]; // bump up toward
                                    }
                                    else
                                    {
                                        list[i].Right = temp;
                                        list[i].Left = list[i - 2];
                                    }

                                    // remove the skipped list[i - 1]
                                    list.RemoveAt(i - 1);
                                }
                                // if pos is neg, we need to put the negative on the mult
                                else if (toward.OP == Expr.OPs.Neg)
                                {
                                    // create the combinartion node
                                    Expr temp = new Expr() { OP = Expr.OPs.Neg, Left = mult };

                                    // add it in
                                    if (list[i].Left == mult)
                                    {
                                        list[i].Left = temp; // replace mult with combination
                                        list[i].Right = list[i - 2]; // bump up toward
                                    }
                                    else
                                    {
                                        list[i].Right = temp;
                                        list[i].Left = list[i - 2];
                                    }

                                    // remove the skipped list[i - 1]
                                    list.RemoveAt(i - 1);
                                }
                                // otherwise something horrible happened (this should never happen, but is left in for sanity-checking and future-proofing)
                                else throw new ArgumentException($"Unknown address simplification step: {toward.OP}");

                                --i; // decrement i to follow the multiplication all the way down the rabbit hole
                                if (i < 2) i = 2; // but if it gets under the starting point, reset it

                                break;
                            }

                        default: args.err = new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {args.line}: Register may not be connected by {list[i].OP}"); return false;
                    }
                }

                // -- finally done with all the algebra -- //
                
                // extract mult code fragment
                if (!(list[1].Left == list[0] ? list[1].Right : list[1].Left).Evaluate(args.file.Symbols, out UInt64 val, out bool floating, ref err))
                { args.err = new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {args.line}: Failed to evaluate register multiplier as an instant imm\n-> {err}"); return false; }
                // make sure it's not floating-point
                if (floating) { args.err = new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {args.line}: Register multiplier may not be floating-point"); return false; }

                // look through from top to bottom
                for (int i = list.Count - 1; i >= 2; --i)
                {
                    // if this will negate the register
                    if (list[i].OP == Expr.OPs.Neg || list[i].OP == Expr.OPs.Sub && list[i].Right == list[i - 1])
                    {
                        // negate found partial mult
                        val = ~val + 1;
                    }
                }

                // remove the register section from the expression (replace with integral 0)
                list[1].OP = Expr.OPs.None;
                list[1].Left = list[1].Right = null;
                list[1].Value = "0";

                m += val; // add extracted mult to total mult
                list.Clear(); // clear list for next pass
            }

            // -- final task: get mult code and negative flag -- //

            // if m is pretty big, it's negative
            if (m > 64) { m = ~m + 1; neg = true; } else neg = false;
            // only other thing is transforming the multiplier into a mult code
            switch (m)
            {
                case 0: m = 0; break;
                case 1: m = 1; break;
                case 2: m = 2; break;
                case 4: m = 3; break;
                case 8: m = 4; break;
                case 16: m = 5; break;
                case 32: m = 6; break;
                case 64: m = 7; break;

                default: args.err = new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {args.line}: Invalid register multiplier encountered ({m.MakeSigned()})"); return false;
            }

            // register successfully parsed
            return true;
        }
        private static bool TryParseAddress(AssembleArgs args, string token, out UInt64 a, out UInt64 b, out Expr hole)
        {
            a = b = 0;
            hole = new Expr();

            // must be of [*] format
            if (token.Length < 3 || token[0] != '[' || token[token.Length - 1] != ']') { args.err = new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {args.line}: Invalid address format encountered \"{token}\""); return false; }

            int pos, end; // parsing positions

            UInt64 temp = 0; // parsing temporaries
            bool btemp = false;

            int reg_count = 0; // number of registers parsed
            UInt64 r1 = 0, m1 = 0, r2 = 0, m2 = 0; // final register info
            bool n1 = false, n2 = false;

            string preface = $"__reg_{args.time:x16}"; // preface used for registers
            string err = string.Empty; // evaluation error

            List<UInt64> regs = new List<UInt64>(); // the registers found in the expression

            // replace registers with temporary names
            while (true)
            {
                // find the next register marker
                for (pos = 1; pos < token.Length && token[pos] != '$'; ++pos) ;
                // if this starts parenthetical region
                if (pos + 1 < token.Length && token[pos + 1] == '(')
                {
                    int depth = 1; // depth of 1

                    // start searching for ending parens after first parens
                    for (end = pos + 2; end < token.Length && depth > 0; ++end)
                    {
                        if (token[end] == '(') ++depth;
                        else if (token[end] == ')') --depth;
                    }

                    // make sure we reached zero depth
                    if (depth != 0) { args.err = new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {args.line}: Mismatched parenthesis in register expression \"{token.Substring(pos, end - pos)}\""); return false; }
                }
                // otherwise normal symbol
                else
                {
                    // take all legal chars
                    for (end = pos + 1; end < token.Length && (char.IsLetterOrDigit(token[end]) || token[end] == '_'); ++end) ;
                }

                // break out if we've reached the end
                if (pos >= token.Length) break;

                // parse this as a register
                if (!TryParseRegister(args, token.Substring(pos, end - pos), out temp)) return false;

                // put it in a register slot
                if (!regs.Contains(temp)) regs.Add(temp);

                // modify the register label in the expression to be a legal symbol name
                token = $"{token.Substring(0, pos)}{preface}_{temp}{token.Substring(end)}";
            }
            
            // turn into an expression
            if (!TryParseImm(args, token.Substring(1, token.Length - 2), out hole)) { args.err = new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {args.line}: Failed to parse address expression\n-> {args.err.Item2}"); return false; }

            // look through each register found
            foreach (UInt64 reg in regs)
            {
                // get the register data
                if (!TryParseAddressReg(args, $"{preface}_{reg}", ref hole, out temp, out btemp)) { args.err = new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {args.line}: Failed to extract register data\n-> {args.err.Item2}"); return false; }

                // if the multiplier was nonzero, the register is really being used
                if (temp != 0)
                {
                    // put it into an available r slot
                    if (reg_count == 0) { r1 = reg; m1 = temp; n1 = btemp; }
                    else if (reg_count == 1) { r2 = reg; m2 = temp; n2 = btemp; }
                    else { args.err = new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {args.line}: Can't use more than 2 registers to specify an address"); return false; }

                    ++reg_count; // mark this slot as filled
                }
            }

            // make sure only one register is negative
            if (n1 && n2) { args.err = new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {args.line}: Only one register may be negative in an address expression"); return false; }
            // if the negative register is r1, swap with r2
            if (n1)
            {
                Utility.Swap(ref r1, ref r2);
                Utility.Swap(ref m1, ref m2);
                Utility.Swap(ref n1, ref n2);
            }

            // if we can evaluate the hole to zero, there is no hole (null it)
            if (hole.Evaluate(args.file.Symbols, out temp, out btemp, ref err) && temp == 0) hole = null;

            // -- apply final touches -- //

            // [1: literal][3: m1][1: -m2][3: m2]   [4: r1][4: r2]   ([64: imm])
            a = (hole != null ? 128 : 0ul) | (m1 << 4) | (n2 ? 8 : 0ul) | m2;
            b = (r1 << 4) | r2;

            // address successfully parsed
            return true;
        }

        private static bool IsValidLabel(string token)
        {
            // can't be nothing
            if (token.Length == 0) return false;

            // first char is underscore or letter
            if (token[0] != '_' && !char.IsLetter(token[0])) return false;
            // all other chars may additionally be numbers
            for (int i = 1; i < token.Length; ++i)
                if (token[i] != '_' && !char.IsLetterOrDigit(token[i])) return false;

            return true;
        }
        private static bool MutateLabel(AssembleArgs args, ref string label)
        {
            // if defining a local label
            if (label[0] == '.')
            {
                // local name can't be empty
                if (label.Length == 1) { args.err = new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {args.line}: Local label name cannot be empty"); return false; }
                // can't make a local label before any non-local ones exist
                if (args.last_static_label == null) { args.err = new Tuple<AssembleError, string>(AssembleError.InvalidLabel, $"line {args.line}: Cannot define a local label before the first static label"); return false; }

                // mutate the label
                label = $"__local_{args.time:x16}_{args.last_static_label}_{label.Substring(1)}";
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
            Expr hole1, hole2;

            if (args.args.Length != 2) { args.err = new Tuple<AssembleError, string>(AssembleError.ArgCount, $"line {args.line}: {op} expected 2 args"); return false; }
            if ((Size(args.sizecode) & sizemask) == 0) { args.err = new Tuple<AssembleError, string>(AssembleError.UsageError, $"line {args.line}: {op} does not support the specified size code"); return false; }

            AppendVal(args, 1, (UInt64)op);

            UInt64 b_sizecode = _b_sizecode == -1 ? args.sizecode : (UInt64)_b_sizecode;

            // reg, *
            if (args.args[0][0] == '$')
            {
                if (!TryParseRegister(args, args.args[0], out a)) { args.err = new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {args.line}: Failed to parse \"{args.args[0]}\" as a register\n-> {args.err.Item2}"); return false; }

                // reg, reg
                if (args.args[1][0] == '$')
                {
                    if (!TryParseRegister(args, args.args[1], out b)) { args.err = new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {args.line}: Failed to parse \"{args.args[1]}\" as a register\n-> {args.err.Item2}"); return false; }

                    AppendVal(args, 1, (a << 4) | (args.sizecode << 2) | 2);
                    AppendVal(args, 1, b);
                }
                // reg, mem
                else if (args.args[1][0] == '[')
                {
                    if (!TryParseAddress(args, args.args[1], out b, out c, out hole1)) { args.err = new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {args.line}: Failed to parse \"{args.args[1]}\" as an address\n-> {args.err.Item2}"); return false; }

                    AppendVal(args, 1, (a << 4) | (args.sizecode << 2) | 1);
                    if (!TryAppendAddress(args, b, c, hole1)) { args.err = new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {args.line}: Failed to append value\n-> {args.err.Item2}"); return false; }
                }
                // reg, imm
                else
                {
                    if (!TryParseImm(args, args.args[1], out hole1)) { args.err = new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {args.line}: Failed to parse \"{args.args[1]}\" as an imm\n-> {args.err.Item2}"); return false; }

                    AppendVal(args, 1, (a << 4) | (args.sizecode << 2) | 0);
                    if (!TryAppendHole(args, Size(b_sizecode), hole1)) { args.err = new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {args.line}: Failed to append value\n-> {args.err.Item2}"); return false; }
                }
            }
            // mem, *
            else if (args.args[0][0] == '[')
            {
                if (!TryParseAddress(args, args.args[0], out a, out b, out hole1)) { args.err = new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {args.line}: Failed to parse \"{args.args[0]}\" as an address\n-> {args.err.Item2}"); return false; }

                // mem, reg
                if (args.args[1][0] == '$')
                {
                    if (!TryParseRegister(args, args.args[1], out c)) { args.err = new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {args.line}: Failed to parse \"{args.args[1]}\" as a register\n-> {args.err.Item2}"); return false; }

                    AppendVal(args, 1, (args.sizecode << 2) | 2);
                    AppendVal(args, 1, 16 | c);
                    if (!TryAppendAddress(args, a, b, hole1)) { args.err = new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {args.line}: Failed to append value\n-> {args.err.Item2}"); return false; };
                }
                // mem, mem
                else if (args.args[1][0] == '[') { args.err = new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {args.line}: {op} does not support memory-to-memory"); return false; }
                // mem, imm
                else
                {
                    if (!TryParseImm(args, args.args[1], out hole2)) { args.err = new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {args.line}: Failed to parse \"{args.args[1]}\" as an imm\n-> {args.err.Item2}"); return false; }

                    AppendVal(args, 1, (args.sizecode << 2) | 3);
                    if (!TryAppendHole(args, Size(b_sizecode), hole2)) { args.err = new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {args.line}: Failed to append value\n-> {args.err.Item2}"); return false; }
                    if (!TryAppendAddress(args, a, b, hole1)) { args.err = new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {args.line}: Failed to append value\n-> {args.err.Item2}"); return false; }
                }
            }
            else { args.err = new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {args.line}: Destination must be register or memory"); return false; }

            return true;
        }
        private static bool TryProcessUnaryOp(AssembleArgs args, OPCode op, UInt64 sizemask = 15)
        {
            UInt64 a, b;
            Expr hole;

            if (args.args.Length != 1) { args.err = new Tuple<AssembleError, string>(AssembleError.ArgCount, $"line {args.line}: {op} expected 1 arg"); return false; }
            if ((Size(args.sizecode) & sizemask) == 0) { args.err = new Tuple<AssembleError, string>(AssembleError.UsageError, $"line {args.line}: {op} does not support the specified size code"); return false; }

            AppendVal(args, 1, (UInt64)op);

            // reg
            if (args.args[0][0] == '$')
            {
                if (!TryParseRegister(args, args.args[0], out a)) { args.err = new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {args.line}: Failed to parse \"{args.args[0]}\" as a register\n-> {args.err.Item2}"); return false; }

                AppendVal(args, 1, (a << 4) | (args.sizecode << 2) | 0);
            }
            // mem
            else if (args.args[0][0] == '[')
            {
                if (!TryParseAddress(args, args.args[0], out a, out b, out hole)) { args.err = new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {args.line}: Failed to parse \"{args.args[0]}\" as an address\n-> {args.err.Item2}"); return false; }

                AppendVal(args, 1, (args.sizecode << 2) | 1);
                if (!TryAppendAddress(args, a, b, hole)) { args.err = new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {args.line}: Failed to append value\n-> {args.err.Item2}"); return false; }
            }
            // imm
            else { args.err = new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {args.line}: Destination must be register or memory"); return false; }

            return true;
        }
        private static bool TryProcessJump(AssembleArgs args, OPCode op)
        {
            if (args.args.Length != 1) { args.err = new Tuple<AssembleError, string>(AssembleError.ArgCount, $"line {args.line}: {op} expected 1 arg"); return false; }

            if (!TryParseAddress(args, args.args[0], out UInt64 a, out UInt64 b, out Expr hole)) { args.err = new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {args.line}: Jump expected address as first arg\n-> {args.err.Item2}"); return false; }

            AppendVal(args, 1, (UInt64)op);
            if (!TryAppendAddress(args, a, b, hole)) { args.err = new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {args.line}: Failed to append value\n-> {args.err.Item2}"); return false; }

            return true;
        }
        private static bool TryProcessEmission(AssembleArgs args)
        {
            if (args.args.Length == 0) { args.err = new Tuple<AssembleError, string>(AssembleError.ArgCount, $"line {args.line}: Emission expected at least one value"); return false; }

            Expr hole = new Expr(); // initially empty hole (to allow for buffer shorthand e.x. "emit x32")
            UInt64 mult;
            bool floating;

            for (int i = 0; i < args.args.Length; ++i)
            {
                if (args.args[i].Length == 0) { args.err = new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {args.line}: Emission encountered empty argument"); return false; }

                // if a multiplier
                if (args.args[i][0] == '#')
                {
                    // get the multiplier and ensure is valid
                    if (!TryParseInstantImm(args, args.args[i].Substring(1), out mult, out floating)) return false;
                    if (floating) { args.err = new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {args.line}: Emission multiplier cannot be floating point"); return false; }
                    if (mult > EmissionMaxMultiplier) { args.err = new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {args.line}: Emission multiplier cannot exceed {EmissionMaxMultiplier}. Got {mult}"); return false; }
                    if (mult == 0) { args.err = new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {args.line}: Emission multiplier cannot be zero"); return false; }

                    // account for first written value
                    if (i > 0 && args.args[i - 1][0] != '#') --mult;

                    for (UInt64 j = 0; j < mult; ++j)
                        if (!TryAppendHole(args, Size(args.sizecode), hole)) { args.err = new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {args.line}: Failed to append value\n-> {args.err.Item2}"); return false; }
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
                    if (!TryAppendHole(args, Size(args.sizecode), hole)) { args.err = new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {args.line}: Failed to append value\n-> {args.err.Item2}"); return false; }
                }
            }

            return true;
        }
        private static bool TryProcessXMULXDIV(AssembleArgs args, OPCode op)
        {
            UInt64 a, b;
            Expr hole;

            if (args.args.Length != 1) { args.err = new Tuple<AssembleError, string>(AssembleError.ArgCount, $"line {args.line}: {op} expected 1 arg"); return false; }

            AppendVal(args, 1, (UInt64)op);

            // reg
            if (args.args[0][0] == '$')
            {
                if (!TryParseRegister(args, args.args[0], out a)) { args.err = new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {args.line}: Failed to parse \"{args.args[0]}\" as a register\n-> {args.err.Item2}"); return false; }

                AppendVal(args, 1, (a << 4) | (args.sizecode << 2) | 1);
            }
            // mem
            else if (args.args[0][0] == '[')
            {
                if (!TryParseAddress(args, args.args[0], out a, out b, out hole)) { args.err = new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {args.line}: Failed to parse \"{args.args[0]}\" as an address\n-> {args.err.Item2}"); return false; }

                AppendVal(args, 1, (args.sizecode << 2) | 2);
                if (!TryAppendAddress(args, a, b, hole)) { args.err = new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {args.line}: Failed to append value\n-> {args.err.Item2}"); return false; }
            }
            // imm
            else
            {
                if (!TryParseImm(args, args.args[0], out hole)) { args.err = new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {args.line}: Failed to parse \"{args.args[0]}\" as an imm\n-> {args.err.Item2}"); return false; }

                AppendVal(args, 1, (args.sizecode << 2) | 0);
                if (!TryAppendHole(args, Size(args.sizecode), hole)) { args.err = new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {args.line}: Failed to append value\n-> {args.err.Item2}"); return false; }
            }

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
                err = null,

                time = Time()
            };

            // predefined symbols
            args.file.Symbols = new Dictionary<string, Expr>()
            {
                ["__time__"] = new Expr() { IntResult = Time() },
                ["__version__"] = new Expr() { IntResult = Version },

                ["__pinf__"] = new Expr() { FloatResult = double.PositiveInfinity },
                ["__ninf__"] = new Expr() { FloatResult = double.NegativeInfinity },
                ["__nan__"] = new Expr() { FloatResult = double.NaN },

                ["__fmax__"] = new Expr() { FloatResult = double.MaxValue },
                ["__fmin__"] = new Expr() { FloatResult = double.MinValue },
                ["__fepsilon__"] = new Expr() { FloatResult = double.Epsilon },

                ["__pi__"] = new Expr() { FloatResult = Math.PI },
                ["__e__"] = new Expr() { FloatResult = Math.E },
            };

            int pos = 0, end = 0; // position in code

            // potential parsing args for an instruction
            UInt64 a = 0, b = 0, c = 0, d = 0;
            Expr hole;
            bool floating;

            string err = null; // error location for evaluation



            /* testing for expressions
            {
                string test = "a?b?c:d?e:f:g";
                //string test = "a?b?c:d:e?f:g";

                if (!TryParseImm(args, test, out Expr _test)) MessageBox.Show(args.err.ToString());
                MessageBox.Show(_test.ToString());

                if (!_test.Evaluate(file.Symbols, out a, out floating, ref err)) MessageBox.Show(err);
                MessageBox.Show(_test.ToString());
            }
            */



            if (code.Length == 0) return new Tuple<AssembleError, string>(AssembleError.EmptyFile, "The file was empty");

            while (pos < code.Length)
            {
                // find the next separator
                for (end = pos; end < code.Length && code[end] != '\n' && code[end] != '#'; ++end) ;

                ++args.line; // advance line counter
                // split the line
                args.rawline = code.Substring(pos, end - pos);
                if (!SplitLine(args)) return new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {args.line}: Failed to parse line\n-> {args.err.Item2}");
                // if the separator was a comment character, consume the rest of the line as well as no-op
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

                    // add the symbol as an address (uses illegal symbol #base, which will be defined at link time)
                    file.Symbols.Add(label, new Expr() { OP = Expr.OPs.Add, Left = new Expr() { Value = "#base" }, Right = new Expr() { Value = file.Data.LongCount().MakeUnsigned().ToString() } });
                }

                // empty lines are ignored
                if (args.op != string.Empty)
                {
                    switch (args.op.ToUpper())
                    {
                        case "GLOBAL":
                            if (args.args.Length == 0) return new Tuple<AssembleError, string>(AssembleError.ArgCount, $"line {args.line}: GLOBAL expected at least one symbol to export");

                            foreach (string symbol in args.args)
                            {
                                // special error message for using global on local labels
                                if (symbol[0] == '.') return new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {args.line}: Cannot export local symbols");

                                // test name for legality
                                if (!IsValidLabel(symbol)) return new Tuple<AssembleError, string>(AssembleError.InvalidLabel, $"line {args.line}: Invalid symbol name \"{symbol}\"");

                                // don't add to global table twice
                                if (file.GlobalSymbols.Contains(symbol)) return new Tuple<AssembleError, string>(AssembleError.SymbolRedefinition, $"line {args.line}: Attempt to export symbol \"{symbol}\" multiple times");

                                // add it to the globals list
                                file.GlobalSymbols.Add(symbol);
                            }

                            break;
                        case "DEF":
                            if (args.args.Length != 2) return new Tuple<AssembleError, string>(AssembleError.ArgCount, $"line {args.line}: DEF expected 2 args");

                            // mutate and test name for legality
                            if (!MutateLabel(args, ref args.args[0])) return args.err;
                            if (!IsValidLabel(args.args[0])) return new Tuple<AssembleError, string>(AssembleError.InvalidLabel, $"line {args.line}: Invalid label name \"{args.args[0]}\"");

                            // get the expression
                            if (!TryParseImm(args, args.args[1], out hole)) return new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {args.line}: DEF expected an expression as third arg\n-> {args.err.Item2}");

                            // don't redefine a symbol
                            if (file.Symbols.ContainsKey(args.args[0])) return new Tuple<AssembleError, string>(AssembleError.SymbolRedefinition, $"line {args.line}: Symbol \"{args.args[0]}\" was already defined");

                            // add it to the dictionary
                            file.Symbols.Add(args.args[0], hole);
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
                        case "MOVP": case "MOVPE": if (!TryProcessBinaryOp(args, OPCode.MOVp)) return args.err; break;
                        case "MOVNP": case "MOVPO": if (!TryProcessBinaryOp(args, OPCode.MOVnp)) return args.err; break;
                        case "MOVO": if (!TryProcessBinaryOp(args, OPCode.MOVo)) return args.err; break;
                        case "MOVNO": if (!TryProcessBinaryOp(args, OPCode.MOVno)) return args.err; break;
                        case "MOVC": if (!TryProcessBinaryOp(args, OPCode.MOVc)) return args.err; break;
                        case "MOVNC": if (!TryProcessBinaryOp(args, OPCode.MOVnc)) return args.err; break;

                        case "SWAP":
                            if (args.args.Length != 2) return new Tuple<AssembleError, string>(AssembleError.ArgCount, $"line {args.line}: SWAP expected 2 args");

                            AppendVal(args, 1, (UInt64)OPCode.SWAP);

                            // reg, *
                            if (args.args[0][0] == '$')
                            {
                                if(!TryParseRegister(args, args.args[0], out a)) return new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {args.line}: Failed to parse \"{args.args[0]}\" as a register\n-> {args.err.Item2}");

                                // reg, reg
                                if (args.args[1][0] == '$')
                                {
                                    if(!TryParseRegister(args, args.args[1], out b)) return new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {args.line}: Failed to parse \"{args.args[1]}\" as a register\n-> {args.err.Item2}");

                                    AppendVal(args, 1, (a << 4) | (args.sizecode << 2) | 0);
                                    AppendVal(args, 1, b);
                                }
                                // reg, mem
                                else if (args.args[1][0] == '[')
                                {
                                    if(!TryParseAddress(args, args.args[1], out b, out c, out hole)) return new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {args.line}: Failed to parse \"{args.args[1]}\" as an address\n-> {args.err.Item2}");

                                    AppendVal(args, 1, (a << 4) | (args.sizecode << 2) | 1);
                                    if (!TryAppendAddress(args, b, c, hole)) return new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {args.line}: Failed to append value\n-> {args.err.Item2}");
                                }
                                // reg, imm
                                else return new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {args.line}: Cannot use SWAP with an imm");
                            }
                            // mem, *
                            else if (args.args[0][0] == '[')
                            {
                                if(!TryParseAddress(args, args.args[0], out a, out b, out hole)) return new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {args.line}: Failed to parse \"{args.args[0]}\" as an address\n-> {args.err.Item2}");

                                // mem, reg
                                if (args.args[1][0] == '$')
                                {
                                    if (!TryParseRegister(args, args.args[1], out c)) return new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {args.line}: Failed to parse \"{args.args[1]}\" as a register\n-> {args.err.Item2}");

                                    AppendVal(args, 1, (c << 4) | (args.sizecode << 2) | 1);
                                    if (!TryAppendAddress(args, a, b, hole)) return new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {args.line}: Failed to append value");
                                }
                                // mem, mem
                                else if (args.args[1][0] == '[') return new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {args.line}: Cannot use SWAP with two memory values");
                                // mem, imm
                                else return new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {args.line}: Cannot use SWAP with an imm");
                            }
                            // imm, *
                            else return new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {args.line}: Cannot use SWAP with an imm");

                            break;

                        case "UX": a = (UInt64)OPCode.UX; goto XEXTEND;
                        case "SX":
                            a = (UInt64)OPCode.SX;
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
                            if (!TryAppendAddress(args, b, c, hole)) return new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {args.line}: Failed to append value\n-> {args.err.Item2}");

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
                        case "JP": case "JPE": if (!TryProcessJump(args, OPCode.Jp)) return args.err; break;
                        case "JNP": case "JPO": if (!TryProcessJump(args, OPCode.Jnp)) return args.err; break;
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
                                if (!TryAppendHole(args, Size(args.sizecode), hole)) return new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {args.line}: Failed to append value\n-> {args.err.Item2}");
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
                            if (!TryAppendAddress(args, a, b, hole)) return new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {args.line}: Failed to append value\n-> {args.err.Item2}");

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
                            if (!TryAppendAddress(args, c, d, hole)) return args.err;

                            break;

                        case "FX":
                            if (args.args.Length != 2) return new Tuple<AssembleError, string>(AssembleError.ArgCount, $"line {args.line}: XEXTEND expected 2 args");

                            if (!TryParseSizecode(args, args.args[0], out a)) return new Tuple<AssembleError, string>(AssembleError.MissingSize, $"line {args.line}: UEXTEND expected size parameter as second arg\n-> {args.err.Item2}");
                            if (!TryParseRegister(args, args.args[1], out b)) return new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {args.line}: UEXTEND expected register parameter as third arg\n-> {args.err.Item2}");

                            AppendVal(args, 1, (UInt64)OPCode.FX);
                            AppendVal(args, 1, (b << 4) | (a << 2) | args.sizecode);

                            break;

                        default: return new Tuple<AssembleError, string>(AssembleError.UnknownOp, $"line {args.line}: Unknown operation \"{args.op}\"");
                    }
                }

                // advance to after the new line
                pos = end + 1;
            }

            // make sure all global symbols were actually defined prior to link-time
            foreach (string str in file.GlobalSymbols) if (!file.Symbols.ContainsKey(str)) return new Tuple<AssembleError, string>(AssembleError.UnknownSymbol, $"Global symbol \"{str}\" was never defined");

            // evaluate each symbol to link all internal symbols and minimize object file complexity
            foreach (var entry in file.Symbols) entry.Value.Evaluate(file.Symbols, out a, out floating, ref err);

            // try to eliminate as many holes as possible (we want as clean an object file as possible)
            for (int i = file.Holes.Count - 1; i >= 0; --i)
            {
                switch (TryPatchHole(file.Symbols, file.Data, file.Holes[i], ref err))
                {
                    case PatchError.None: file.Holes.RemoveAt(i); break; // remove the hole if we solved it
                    case PatchError.Unevaluated: break;
                    case PatchError.Error: return new Tuple<AssembleError, string>(AssembleError.ArgError, err);

                    default: throw new ArgumentException("Unknown patch error encountered");
                }
            }

            // return no error
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

            UInt64[] offsets = new UInt64[objs.Length]; // offsets for where an object file begins in the resulting exe
            var symbols = new Dictionary<string, Expr>[objs.Length]; // processed symbols for each object file

            // create a combined symbols table with predefined values
            var G_symbols = new Dictionary<string, Expr>()
            {
                ["__prog_end__"] = new Expr() { IntResult = res.LongLength.MakeUnsigned() }
            };

            UInt64 val; // parsing locations
            bool floating;

            string err = null; // error location for evaluation

            Expr main = null; // entry point expression

            // -------------------------------------------

            // merge the files into res
            for (int i = 0; i < objs.Length; ++i)
            {
                offsets[i] = filesize; // record its starting offset

                objs[i].Data.CopyTo(res, (int)filesize); // copy its data (POTENTIAL SIZE LIMITATION)
                filesize += (UInt64)objs[i].Data.LongCount(); // advance write cursor
            }

            // for each file
            for (int i = 0; i < objs.Length; ++i)
            {
                // create temporary symbols dictionary
                symbols[i] = new Dictionary<string, Expr>(objs[i].Symbols.Count);

                // populate local symbols (define #base)
                foreach (var symbol in objs[i].Symbols) symbols[i].Add(symbol.Key, symbol.Value.Clone("#base", offsets[i], false));

                // merge global symbols
                foreach (string symbol in objs[i].GlobalSymbols)
                {
                    // don't redefine the same symbol
                    if (G_symbols.ContainsKey(symbol)) return new Tuple<LinkError, string>(LinkError.SymbolRedefinition, $"Global symbol \"{symbol}\" was already defined");
                    // make sure the symbol exists in locals dictionary (if using built-in assembler above, this should never happen with valid object files)
                    if (!symbols[i].TryGetValue(symbol, out Expr hole)) return new Tuple<LinkError, string>(LinkError.MissingSymbol, $"Global symbol \"{symbol}\" undefined");

                    // add to global symbols
                    G_symbols.Add(symbol, hole);

                    // if this is main
                    if (symbol == "main")
                    {
                        // mark as main
                        main = hole;
                        // link to local symbols (won't be done later cause it's not a hole) (can't be a hole cause file order is undefined)
                        main.Evaluate(symbols[i], out val, out floating, ref err);
                    }
                }
            }

            // for each object file
            for (int i = 0; i < objs.Length; ++i)
            {
                // patch all the holes
                foreach (HoleData _data in objs[i].Holes)
                {
                    // create a copy of hole and data (so as not to corrupt object file)
                    HoleData data = new HoleData { Address = _data.Address + offsets[i], Size = _data.Size, Line = _data.Line, Expr = _data.Expr.Clone() };

                    // try to patch it
                    PatchError _err = TryPatchHole(symbols[i], res, data, ref err);
                    if (_err == PatchError.Unevaluated) _err = TryPatchHole(G_symbols, res, data, ref err);
                    switch (_err)
                    {
                        case PatchError.None: break;
                        case PatchError.Unevaluated: return new Tuple<LinkError, string>(LinkError.MissingSymbol, err);
                        case PatchError.Error: return new Tuple<LinkError, string>(LinkError.FormatError, err);

                        default: throw new ArgumentException("Unknown patch error encountered");
                    }
                }
            }

            // write the header
            if (main == null) return new Tuple<LinkError, string>(LinkError.MissingSymbol, "No entry point \"main\"");
            if (!main.Evaluate(G_symbols, out val, out floating, ref err)) return new Tuple<LinkError, string>(LinkError.MissingSymbol, $"Failed to evaluate global symbol \"main\"\n-> {err}");

            Write(res, 0, 1, (UInt64)OPCode.JMP);
            Write(res, 1, 1, 0x80);
            Write(res, 2, 8, val);

            // linked successfully
            return new Tuple<LinkError, string>(LinkError.None, string.Empty);
        }
    }
}
