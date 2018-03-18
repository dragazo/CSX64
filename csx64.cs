using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace csx64
{
    public static class ComputerExtensions
    {
        public static UInt64 Negative(this UInt64 val, bool floating)
        {
            return floating ? Computer.AsUInt64(-Computer.AsDouble(val)) : ~val + 1;
        }
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
    public class Computer
    {
        // -----------
        // -- Types --
        // -----------
        
        public enum ErrorCode
        {
            None, OutOfBounds, UnhandledSyscall, UndefinedBehavior, Placeholder
        }
        public enum OPCode
        {
            Nop, Stop, Syscall,

            Mov,
            MOVa, MOVae, MOVb, MOVbe, MOVg, MOVge, MOVl, MOVle,
            MOVz, MOVnz, MOVs, MOVns, MOVp, MOVnp, MOVo, MOVno, MOVc, MOVnc,

            Swap,

            UExtend, SExtend,

            Add, Sub, Mul, UDiv, UMod, SDiv, SMod,
            SLL, SLR, SAL, SAR, RL, RR,
            And, Or, Xor,

            Cmp, Test,

            Inc, Dec, Neg, Not, Abs, Id,

            La,

            Jmp,
            Ja, Jae, Jb, Jbe, Jg, Jge, Jl, Jle,
            Jz, Jnz, Js, Jns, Jp, Jnp, Jo, Jno, Jc, Jnc,

            Fadd, Fsub, Fmul, Fdiv, Fmod,
            Fpow, Fsqrt, Fexp, Fln, Fabs, Fid,

            Fsin, Fcos, Ftan,
            Fsinh, Fcosh, Ftanh,
            Fasin, Facos, Fatan, Fatan2,

            Ffloor, Fceil, Fround, Ftrunc,

            Fcmp,

            FtoI, ItoF,

            Push, Pop, Call, Ret
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
            public uint Flags;

            /// <summary>
            /// The Zero flag
            /// </summary>
            public bool Z
            {
                get { return (Flags & 0x01) != 0; }
                set { Flags = (Flags & 0xfe) | (value ? 0x01 : 0u); }
            }
            /// <summary>
            /// The Sign flag
            /// </summary>
            public bool S
            {
                get { return (Flags & 0x02) != 0; }
                set { Flags = (Flags & 0xfd) | (value ? 0x02 : 0u); }
            }
            /// <summary>
            /// The Parity flag
            /// </summary>
            public bool P
            {
                get { return (Flags & 0x04) != 0; }
                set { Flags = (Flags & 0xfb) | (value ? 0x04 : 0u); }
            }
            /// <summary>
            /// The Overflow flag
            /// </summary>
            public bool O
            {
                get { return (Flags & 0x08) != 0; }
                set { Flags = (Flags & 0xf7) | (value ? 0x08 : 0u); }
            }
            /// <summary>
            /// The Carry flag
            /// </summary>
            public bool C
            {
                get { return (Flags & 0x10) != 0; }
                set { Flags = (Flags & 0xef) | (value ? 0x10 : 0u); }
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
        /// <param name="sizecode">the codeto parse</param>
        protected static UInt64 Size(UInt64 sizecode)
        {
            return 1ul << (ushort)sizecode;
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

        // [1: literal][3: m1][1: -m2][3: m2]   ([4: r1][4: r2])   ([64: imm])
        protected bool GetAddress(ref UInt64 res)
        {
            UInt64 mults = 0, regs = 0, imm = 0; // the mult codes, regs, and literal (mults and regs only initialized for compiler, but literal must be initialized to 0)

            // parse the address
            if (!GetMem(1, ref mults) || (mults & 0x77) != 0 && !GetMem(1, ref regs) || (mults & 0x80) != 0 && !GetMem(8, ref imm)) return false;

            // compute the result into res
            res = MultCode((mults >> 4) & 7) * Registers[regs >> 4].x64 + MultCode(mults & 7, mults & 8) * Registers[regs & 15].x64 + imm;

            // got an address
            return true;
        }

        /// <summary>
        /// Interprets a double as its raw bits
        /// </summary>
        /// <param name="val">value to interpret</param>
        public static unsafe UInt64 AsUInt64(double val)
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

        // ---------------
        // -- Operators --
        // ---------------

        private interface IBinaryEvaluator { UInt64 Evaluate(UInt64 sizecode, UInt64 a, UInt64 b, FlagsRegister flags = null); }
        private interface IUnaryEvaluator { UInt64 Evaluate(UInt64 sizecode, UInt64 val, FlagsRegister flags = null); }

        // -- integral ops --

        private struct ADD : IBinaryEvaluator
        {
            public UInt64 Evaluate(UInt64 sizecode, UInt64 a, UInt64 b, FlagsRegister flags)
            {
                UInt64 res = Truncate(a + b, sizecode);

                if (flags != null)
                {
                    UpdateFlags(sizecode, res, flags);

                    flags.C = res < a && res < b;
                    flags.O = Positive(a, sizecode) == Positive(b, sizecode) && Positive(a, sizecode) != Positive(res, sizecode);
                }

                return res;
            }
        }
        private struct SUB : IBinaryEvaluator
        {
            public UInt64 Evaluate(UInt64 sizecode, UInt64 a, UInt64 b, FlagsRegister flags)
            {
                UInt64 res = Truncate(a - b, sizecode);

                if (flags != null)
                {
                    UpdateFlags(sizecode, res, flags);

                    flags.C = a < b;
                    flags.O = Positive(a, sizecode) != Positive(b, sizecode) && Positive(a, sizecode) != Positive(res, sizecode);
                }

                return res;
            }
        }

        private struct UHIMUL : IBinaryEvaluator
        {
            public UInt64 Evaluate(UInt64 sizecode, UInt64 a, UInt64 b, FlagsRegister flags)
            {
                UInt64 res;

                // if multiplying 64-bit values, use special algorithm
                if (sizecode == 3)
                {
                    res = (UInt64)(UInt32)a * (UInt64)(UInt32)b;
                    res >>= 32;
                    res += (a >> 32) * (UInt64)(UInt32)b;
                    res += (b >> 32) * (UInt64)(UInt32)a;
                    res >>= 32;
                    res += (a >> 32) * (b >> 32);
                }
                // otherwise we have enough space for the whole product anyway
                else res = (a * b) >> ((ushort)Size(sizecode) * 8);

                if (flags != null)
                {
                    UpdateFlags(sizecode, res, flags);

                    flags.C = flags.O = false;
                }

                return res;
            }
        }
        private struct MUL : IBinaryEvaluator
        {
            public UInt64 Evaluate(UInt64 sizecode, UInt64 a, UInt64 b, FlagsRegister flags)
            {
                UInt64 res = Truncate(a * b, sizecode);

                if (flags != null)
                {
                    UpdateFlags(sizecode, res, flags);

                    flags.C = new UHIMUL().Evaluate(sizecode, a, b, null) != 0;
                    flags.O = (Positive(a, sizecode) == Positive(b, sizecode)) != Positive(res, sizecode);
                }

                return res;
            }
        }

        private struct UDIV : IBinaryEvaluator
        {
            public UInt64 Evaluate(UInt64 sizecode, UInt64 a, UInt64 b, FlagsRegister flags)
            {
                UInt64 res = Truncate(b != 0 ? a / b : 0, sizecode);

                if (flags != null)
                {
                    UpdateFlags(sizecode, res, flags);

                    flags.C = flags.O = new UMOD().Evaluate(sizecode, a, b, null) != 0;
                }

                return res;
            }
        }
        private struct UMOD : IBinaryEvaluator
        {
            public UInt64 Evaluate(UInt64 sizecode, UInt64 a, UInt64 b, FlagsRegister flags)
            {
                UInt64 res = Truncate(b != 0 ? a % b : 0, sizecode);

                if (flags != null)
                {
                    UpdateFlags(sizecode, res, flags);

                    flags.C = flags.O = false;
                }

                return res;
            }
        }

        private struct SDIV : IBinaryEvaluator
        {
            public UInt64 Evaluate(UInt64 sizecode, UInt64 a, UInt64 b, FlagsRegister flags)
            {
                bool neg = false;
                UInt64 _a, _b;

                if (Negative(a, sizecode)) { neg = !neg; _a = Truncate(~a + 1, sizecode); } else _a = a;
                if (Negative(b, sizecode)) { neg = !neg; _b = Truncate(~b + 1, sizecode); } else _b = b;

                UInt64 res = Truncate(_b != 0 ? (neg ? ~(_a / _b) + 1 : _a / _b) : 0, sizecode);

                if (flags != null)
                {
                    UpdateFlags(sizecode, res, flags);

                    flags.C = flags.O = new SMOD().Evaluate(sizecode, a, b, null) != 0;
                }

                return res;
            }
        }
        private struct SMOD : IBinaryEvaluator
        {
            public UInt64 Evaluate(UInt64 sizecode, UInt64 a, UInt64 b, FlagsRegister flags)
            {
                // a     = (a / b) * b + a % b
                // a % b = a - (a / b) * b

                UInt64 res = Truncate(a - new SDIV().Evaluate(sizecode, a, b, null) * b, sizecode);

                if (flags != null)
                {
                    UpdateFlags(sizecode, res, flags);

                    flags.C = flags.O = false;
                }

                return res;
            }
        }

        private struct SLL : IBinaryEvaluator
        {
            public UInt64 Evaluate(UInt64 sizecode, UInt64 a, UInt64 b, FlagsRegister flags)
            {
                int sh = (ushort)b % ((ushort)Size(sizecode) * 8); // amount to shift by

                UInt64 res = Truncate(a << sh, sizecode);

                if (flags != null)
                {
                    UpdateFlags(sizecode, res, flags);

                    // sets O and C if a 1 was pushed off the register
                    flags.C = flags.O = ((((1ul << sh) - 1) << ((ushort)Size(sizecode) * 8 - sh)) & res) != 0;
                }

                return res;
            }
        }
        private struct SLR : IBinaryEvaluator
        {
            public UInt64 Evaluate(UInt64 sizecode, UInt64 a, UInt64 b, FlagsRegister flags)
            {
                int sh = (ushort)b % ((ushort)Size(sizecode) * 8); // amount to shift by

                UInt64 res = a >> sh;

                if (flags != null)
                {
                    UpdateFlags(sizecode, res, flags);

                    // sets O and C if a 1 was pushed off the register
                    flags.C = flags.O = (((1ul << sh) - 1) & res) != 0;
                }

                return res;
            }
        }

        /*
        // ## !! THESE 2 AREN'T DONE !! ## //
        private struct SAL : IBinaryEvaluator
        {
            public UInt64 Evaluate(UInt64 sizecode, UInt64 a, UInt64 b, FlagsRegister flags)
            {
                return a << ((ushort)b % 64);
            }
        }
        private struct SAR : IBinaryEvaluator
        {
            public UInt64 Evaluate(UInt64 sizecode, UInt64 a, UInt64 b)
            {
                return a >> ((ushort)b % 64);
            }
        }

        private struct RL : IBinaryEvaluator
        {
            public UInt64 Evaluate(UInt64 sizecode, UInt64 a, UInt64 b)
            {
                int shift = (ushort)b % 64;            // amount to shift by
                int bits = 8 * (ushort)Size(sizecode); // number of bits that are being inspected

                return (a << shift) | ((a & (((1ul << shift) - 1) << (bits - shift))) >> (bits - shift));
            }
        }
        private struct RR : IBinaryEvaluator
        {
            public UInt64 Evaluate(UInt64 sizecode, UInt64 a, UInt64 b)
            {
                int shift = (ushort)b % 64;            // amount to shift by
                int bits = 8 * (ushort)Size(sizecode); // number of bits that are being inspected

                return (a >> shift) | ((a & ((1ul << shift) - 1)) << (bits - shift));
            }
        }*/

        private struct AND : IBinaryEvaluator
        {
            public UInt64 Evaluate(UInt64 sizecode, UInt64 a, UInt64 b, FlagsRegister flags)
            {
                UInt64 res = a & b;

                if (flags != null)
                {
                    UpdateFlags(sizecode, res, flags);
                }

                return res;
            }
        }
        private struct OR : IBinaryEvaluator
        {
            public UInt64 Evaluate(UInt64 sizecode, UInt64 a, UInt64 b, FlagsRegister flags)
            {
                UInt64 res = a | b;

                if (flags != null)
                {
                    UpdateFlags(sizecode, res, flags);
                }

                return res;
            }
        }
        private struct XOR : IBinaryEvaluator
        {
            public UInt64 Evaluate(UInt64 sizecode, UInt64 a, UInt64 b, FlagsRegister flags)
            {
                UInt64 res = a ^ b;

                if (flags != null)
                {
                    UpdateFlags(sizecode, res, flags);
                }

                return res;
            }
        }

        private struct INC : IUnaryEvaluator { public UInt64 Evaluate(UInt64 sizecode, UInt64 val, FlagsRegister flags) { return new ADD().Evaluate(sizecode, val, 1, flags); } }
        private struct DEC : IUnaryEvaluator { public UInt64 Evaluate(UInt64 sizecode, UInt64 val, FlagsRegister flags) { return new SUB().Evaluate(sizecode, val, 1, flags); } }
        private struct NOT : IUnaryEvaluator
        {
            public UInt64 Evaluate(UInt64 sizecode, UInt64 val, FlagsRegister flags)
            {
                UInt64 res = Truncate(~val, sizecode);

                if (flags != null)
                {
                    UpdateFlags(sizecode, res, flags);
                }

                return res;
            }
        }
        private struct ABS : IUnaryEvaluator
        {
            public UInt64 Evaluate(UInt64 sizecode, UInt64 val, FlagsRegister flags)
            {
                UInt64 res = Negative(val, sizecode) ? Truncate(~val + 1, sizecode) : val;

                if (flags != null)
                {
                    UpdateFlags(sizecode, res, flags);
                }

                return res;
            }
        }

        private struct ID : IUnaryEvaluator
        {
            public UInt64 Evaluate(UInt64 sizecode, UInt64 a, FlagsRegister flags)
            {
                return new SUB().Evaluate(sizecode, a, 0, flags);
            }
        }

        // -- floatint point ops --

        private struct FADD : IBinaryEvaluator
        {
            public UInt64 Evaluate(UInt64 sizecode, UInt64 a, UInt64 b, FlagsRegister flags)
            {
                double res = AsDouble(a) + AsDouble(b);

                if (flags != null) UpdateFlagsF(res, flags);

                return AsUInt64(res);
            }
        }
        private struct FSUB : IBinaryEvaluator
        {
            public UInt64 Evaluate(UInt64 sizecode, UInt64 a, UInt64 b, FlagsRegister flags)
            {
                double res = AsDouble(a) - AsDouble(b);

                if (flags != null) UpdateFlagsF(res, flags);

                return AsUInt64(res);
            }
        }

        private struct FMUL : IBinaryEvaluator
        {
            public UInt64 Evaluate(UInt64 sizecode, UInt64 a, UInt64 b, FlagsRegister flags)
            {
                double res = AsDouble(a) * AsDouble(b);

                if (flags != null) UpdateFlagsF(res, flags);

                return AsUInt64(res);
            }
        }

        private struct FDIV : IBinaryEvaluator
        {
            public UInt64 Evaluate(UInt64 sizecode, UInt64 a, UInt64 b, FlagsRegister flags)
            {
                double res = AsDouble(a) / AsDouble(b);

                if (flags != null) UpdateFlagsF(res, flags);

                return AsUInt64(res);
            }
        }
        private struct FMOD : IBinaryEvaluator
        {
            public UInt64 Evaluate(UInt64 sizecode, UInt64 a, UInt64 b, FlagsRegister flags)
            {
                double res = AsDouble(a) % AsDouble(b);

                if (flags != null) UpdateFlagsF(res, flags);

                return AsUInt64(res);
            }
        }

        private struct FPOW : IBinaryEvaluator
        {
            public UInt64 Evaluate(UInt64 sizecode, UInt64 a, UInt64 b, FlagsRegister flags)
            {
                double res = Math.Pow(AsDouble(a), AsDouble(b));

                if (flags != null) UpdateFlagsF(res, flags);

                return AsUInt64(res);
            }
        }
        private struct FSQRT : IUnaryEvaluator
        {
            public UInt64 Evaluate(UInt64 sizecode, UInt64 a, FlagsRegister flags)
            {
                double res = Math.Sqrt(AsDouble(a));

                if (flags != null) UpdateFlagsF(res, flags);

                return AsUInt64(res);
            }
        }
        private struct FEXP : IUnaryEvaluator
        {
            public UInt64 Evaluate(UInt64 sizecode, UInt64 a, FlagsRegister flags)
            {
                double res = Math.Exp(AsDouble(a));

                if (flags != null) UpdateFlagsF(res, flags);

                return AsUInt64(res);
            }
        }
        private struct FLN : IUnaryEvaluator
        {
            public UInt64 Evaluate(UInt64 sizecode, UInt64 a, FlagsRegister flags)
            {
                double res = Math.Log(AsDouble(a));

                if (flags != null) UpdateFlagsF(res, flags);

                return AsUInt64(res);
            }
        }
        private struct FABS : IUnaryEvaluator
        {
            public UInt64 Evaluate(UInt64 sizecode, UInt64 a, FlagsRegister flags)
            {
                double res = Math.Abs(AsDouble(a));

                if (flags != null) UpdateFlagsF(res, flags);

                return AsUInt64(res);
            }
        }

        private struct FSIN : IUnaryEvaluator
        {
            public UInt64 Evaluate(UInt64 sizecode, UInt64 a, FlagsRegister flags)
            {
                double res = Math.Sin(AsDouble(a));

                if (flags != null) UpdateFlagsF(res, flags);

                return AsUInt64(res);
            }
        }
        private struct FCOS : IUnaryEvaluator
        {
            public UInt64 Evaluate(UInt64 sizecode, UInt64 a, FlagsRegister flags)
            {
                double res = Math.Cos(AsDouble(a));

                if (flags != null) UpdateFlagsF(res, flags);

                return AsUInt64(res);
            }
        }
        private struct FTAN : IUnaryEvaluator
        {
            public UInt64 Evaluate(UInt64 sizecode, UInt64 a, FlagsRegister flags)
            {
                double res = Math.Tan(AsDouble(a));

                if (flags != null) UpdateFlagsF(res, flags);

                return AsUInt64(res);
            }
        }

        private struct FSINH : IUnaryEvaluator
        {
            public UInt64 Evaluate(UInt64 sizecode, UInt64 a, FlagsRegister flags)
            {
                double res = Math.Sinh(AsDouble(a));

                if (flags != null) UpdateFlagsF(res, flags);

                return AsUInt64(res);
            }
        }
        private struct FCOSH : IUnaryEvaluator
        {
            public UInt64 Evaluate(UInt64 sizecode, UInt64 a, FlagsRegister flags)
            {
                double res = Math.Cosh(AsDouble(a));

                if (flags != null) UpdateFlagsF(res, flags);

                return AsUInt64(res);
            }
        }
        private struct FTANH : IUnaryEvaluator
        {
            public UInt64 Evaluate(UInt64 sizecode, UInt64 a, FlagsRegister flags)
            {
                double res = Math.Tanh(AsDouble(a));

                if (flags != null) UpdateFlagsF(res, flags);

                return AsUInt64(res);
            }
        }

        private struct FASIN : IUnaryEvaluator
        {
            public UInt64 Evaluate(UInt64 sizecode, UInt64 a, FlagsRegister flags)
            {
                double res = Math.Asin(AsDouble(a));

                if (flags != null) UpdateFlagsF(res, flags);

                return AsUInt64(res);
            }
        }
        private struct FACOS : IUnaryEvaluator
        {
            public UInt64 Evaluate(UInt64 sizecode, UInt64 a, FlagsRegister flags)
            {
                double res = Math.Acos(AsDouble(a));

                if (flags != null) UpdateFlagsF(res, flags);

                return AsUInt64(res);
            }
        }
        private struct FATAN : IUnaryEvaluator
        {
            public UInt64 Evaluate(UInt64 sizecode, UInt64 a, FlagsRegister flags)
            {
                double res = Math.Atan(AsDouble(a));

                if (flags != null) UpdateFlagsF(res, flags);

                return AsUInt64(res);
            }
        }
        private struct FATAN2 : IBinaryEvaluator
        {
            public UInt64 Evaluate(UInt64 sizecode, UInt64 a, UInt64 b, FlagsRegister flags)
            {
                double res = Math.Atan2(AsDouble(a), AsDouble(b));

                if (flags != null) UpdateFlagsF(res, flags);

                return AsUInt64(res);
            }
        }

        private struct FFLOOR : IUnaryEvaluator
        {
            public UInt64 Evaluate(UInt64 sizecode, UInt64 a, FlagsRegister flags)
            {
                double res = Math.Floor(AsDouble(a));

                if (flags != null) UpdateFlagsF(res, flags);

                return AsUInt64(res);
            }
        }
        private struct FCEIL : IUnaryEvaluator
        {
            public UInt64 Evaluate(UInt64 sizecode, UInt64 a, FlagsRegister flags)
            {
                double res = Math.Ceiling(AsDouble(a));

                if (flags != null) UpdateFlagsF(res, flags);

                return AsUInt64(res);
            }
        }
        private struct FROUND : IUnaryEvaluator
        {
            public UInt64 Evaluate(UInt64 sizecode, UInt64 a, FlagsRegister flags)
            {
                double res = Math.Round(AsDouble(a));

                if (flags != null) UpdateFlagsF(res, flags);

                return AsUInt64(res);
            }
        }
        private struct FTRUNC : IUnaryEvaluator
        {
            public UInt64 Evaluate(UInt64 sizecode, UInt64 a, FlagsRegister flags)
            {
                double res = Math.Truncate(AsDouble(a));

                if (flags != null) UpdateFlagsF(res, flags);

                return AsUInt64(res);
            }
        }

        private struct FID : IUnaryEvaluator
        {
            public UInt64 Evaluate(UInt64 sizecode, UInt64 a, FlagsRegister flags)
            {
                return new FSUB().Evaluate(sizecode, a, AsUInt64(0.0), flags);
            }
        }

        private struct FTOI : IUnaryEvaluator
        {
            public UInt64 Evaluate(UInt64 sizecode, UInt64 a, FlagsRegister flags)
            {
                return new ID().Evaluate(sizecode, ((long)AsDouble(a)).MakeUnsigned(), flags);
            }
        }
        private struct ITOF : IUnaryEvaluator
        {
            public UInt64 Evaluate(UInt64 sizecode, UInt64 a, FlagsRegister flags)
            {
                return new FID().Evaluate(sizecode, AsUInt64((double)a.MakeSigned()), flags);
            }
        }

        // -- generic op applicators --

        // [8: binary op]   [4: dest][2: size][2: mode]   (mode = 0: [size: imm]   mode = 1: [4:][4: r]   mode = 2: [address]   mode = 3: UND)
        private bool BinaryOp<Evaluator>(bool apply = true) where Evaluator : struct, IBinaryEvaluator
        {
            UInt64 settings = 0, arg = 0;
            if (!GetMem(1, ref settings)) return false;

            UInt64 sizecode = (settings >> 2) & 3; // store sizecode for readability

            // the result to store back to the destination
            UInt64 res = Registers[settings >> 4].Get(sizecode);

            switch (settings & 3)
            {
                case 0: if (!GetMem(Size(sizecode), ref arg)) return false; res = new Evaluator().Evaluate(sizecode, res, arg, Flags); break;
                case 1: if (!GetMem(1, ref arg)) return false; res = new Evaluator().Evaluate(sizecode, res, Registers[arg & 15].Get(sizecode), Flags); break;
                case 2: if (!GetAddress(ref arg) || !GetMem(Size(sizecode), ref arg)) return false; res = new Evaluator().Evaluate(sizecode, res, arg, Flags); break;

                // otherwise undefined
                default: Fail(ErrorCode.UndefinedBehavior); return false;
            }

            // store back in register
            if(apply) Registers[settings >> 4].Set(sizecode, res);

            return true;
        }

        // [8: unary op]   [4: dest][2:][2: size]
        private bool UnaryOp<Evaluator>(bool apply = true) where Evaluator : struct, IUnaryEvaluator
        {
            UInt64 settings = 0;
            if (!GetMem(1, ref settings)) return false;

            UInt64 sizecode = settings & 3; // store sizecode for readability

            // compute the result
            UInt64 res = new Evaluator().Evaluate(sizecode, Registers[settings >> 4].Get(sizecode), Flags);

            // store the result
            if(apply) Registers[settings >> 4].Set(sizecode, res);
            
            return true;
        }

        private static void UpdateFlags(UInt64 sizecode, UInt64 value, FlagsRegister flags)
        {
            flags.Z = value == 0;
            flags.S = Negative(value, sizecode);

            // compute parity flag (only of low 8 bits)
            bool parity = true;
            for (int i = 0; i < 8; ++i)
                if (((value >> i) & 1) != 0) parity = !parity;
            flags.P = parity;
        }
        private static void UpdateFlagsF(double value, FlagsRegister flags)
        {
            flags.Z = value == 0;
            flags.S = value < 0;

            flags.O = double.IsInfinity(value);
            flags.C = double.IsNaN(value);
        }

        // --------------------
        // -- Execution Data --
        // --------------------

        private Register[] Registers = new Register[16];
        private FlagsRegister Flags = new FlagsRegister();

        private byte[] Memory = null;

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

        // ----------------------
        // -- Public Interface --
        // ----------------------

        /// <summary>
        /// Validates the machine for operation, but does not prepare it for execute (see Initialize)
        /// </summary>
        public Computer()
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
            Error = code;
            Running = false;
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
        public bool GetMem(UInt64 size, ref UInt64 res)
        {
            bool r = GetMem(Pos, size, ref res);
            Pos += size;

            return r;
        }

        /// <summary>
        /// Handles syscall instructions from the processor. Returns true iff the syscall was handled successfully.
        /// Should not be called directly: only by interpreted syscall instructions
        /// </summary>
        protected virtual bool Syscall() { return false; }

        // [8: load]   [4: dest][2: size][2: mode]   (mode = 0: [size: imm]   mode = 1: [4:][4: r]   mode = 2: [address]   mode = 3: UND)
        private bool ProcessLoad(bool keep = true)
        {
            UInt64 a = 0, b = 0;

            if (!GetMem(1, ref a)) return false;
            switch (a & 3)
            {
                case 0: if (!GetMem(Size((a >> 2) & 3), ref b)) return false; break;
                case 1: if (!GetMem(1, ref b)) return false; b = Registers[b & 15].x64; break;
                case 2: if (!GetAddress(ref b) || !GetMem(b, Size((a >> 2) & 3), ref b)) return false; break;
                default: Fail(ErrorCode.UndefinedBehavior); return false;
            }
            if (keep) Registers[a >> 4].Set((a >> 2) & 3, b);
            return true;
        }
        // [8: store]   [4: source][2:][2: size]   [address]
        private bool ProcessStore(bool keep = true)
        {
            UInt64 a = 0, b = 0;

            if (!GetMem(1, ref a) || !GetAddress(ref b)) return false;
            return !keep || SetMem(b, Size(a & 3), Registers[a >> 4].x64);
        }

        // [8: mov]   [4: source/dest][2: size][2: mode]   (mode = 0: load imm [size: imm]   mode = 1: load register [4:][4: source]   mode = 2: load memory [address]   mode = 3: store register [address])
        private bool ProcessMove(bool keep = true)
        {
            UInt64 a = 0, b = 0;

            // consume all instruction data
            if (!GetMem(1, ref a)) return false;
            switch (a & 3)
            {
                case 0: if (!GetMem(Size((a >> 2) & 3), ref b)) return false; break;
                case 1: if (!GetMem(1, ref b)) return false; b = Registers[b & 15].x64; break;
                case 2:
                case 3: if (!GetAddress(ref b)) return false; break;
            }

            // conditionally follow through with transfer
            if (keep)
            {
                switch (a & 3)
                {
                    case 0:
                    case 1: Registers[a >> 4].Set((a >> 2) & 3, b); break;
                    case 2: if (!GetMem(b, Size((a >> 2) & 3), ref b)) return false; Registers[a >> 4].Set((a >> 2) & 3, b); break;
                    case 3: if (!SetMem(b, Size((a >> 2) & 3), Registers[a >> 4].x64)) return false; break;
                }
            }

            return true;
        }
        /// <summary>
        /// Performs a single operation. Returns true if successful
        /// </summary>
        public bool Tick()
        {
            // fail to execute ins if terminated
            if (!Running) return false;

            UInt64 a = 0, b = 0; // the potential args (initialized for compiler)

            // fetch the instruction
            if (!GetMem(1, ref a)) return false;

            // switch through the opcodes
            switch ((OPCode)a)
            {
                case OPCode.Nop: return true;
                case OPCode.Stop: Running = false; return true;
                case OPCode.Syscall: if (Syscall()) return true; Fail(ErrorCode.UnhandledSyscall); return false;

                // [8: mov]   [4: source/dest][2: size][2: mode]   (mode = 0: load imm [size: imm]   mode = 1: load register [4:][4: source]   mode = 2: load memory [address]   mode = 3: store register [address])
                case OPCode.Mov: return ProcessMove();

                case OPCode.MOVa: return ProcessMove(Flags.a);
                case OPCode.MOVae: return ProcessMove(Flags.ae);
                case OPCode.MOVb: return ProcessMove(Flags.b);
                case OPCode.MOVbe: return ProcessMove(Flags.be);

                case OPCode.MOVg: return ProcessMove(Flags.g);
                case OPCode.MOVge: return ProcessMove(Flags.ge);
                case OPCode.MOVl: return ProcessMove(Flags.l);
                case OPCode.MOVle: return ProcessMove(Flags.le);

                case OPCode.MOVz: return ProcessMove(Flags.Z);
                case OPCode.MOVnz: return ProcessMove(!Flags.Z);
                case OPCode.MOVs: return ProcessMove(Flags.S);
                case OPCode.MOVns: return ProcessMove(!Flags.S);
                case OPCode.MOVp: return ProcessMove(Flags.P);
                case OPCode.MOVnp: return ProcessMove(!Flags.P);
                case OPCode.MOVo: return ProcessMove(Flags.O);
                case OPCode.MOVno: return ProcessMove(!Flags.O);
                case OPCode.MOVc: return ProcessMove(Flags.C);
                case OPCode.MOVnc: return ProcessMove(!Flags.C);

                // [8: swap]   [4: r1][4: r2]
                case OPCode.Swap:
                    if (!GetMem(1, ref a)) return false;
                    b = Registers[a >> 4].x64;
                    Registers[a >> 4].x64 = Registers[a & 15].x64;
                    Registers[a & 15].x64 = b;
                    return true;
                
                // [8: XExtend]   [4: register][2: from size][2: to size]
                case OPCode.UExtend: if (!GetMem(1, ref a)) return false; Registers[a >> 4].Set(a & 3, Registers[a >> 4].Get((a >> 2) & 3)); return true;
                case OPCode.SExtend: if (!GetMem(1, ref a)) return false; Registers[a >> 4].Set(a & 3, SignExtend(Registers[a >> 4].Get((a >> 2) & 3), (a >> 2) & 3)); return true;
                
                // [8: binary op]   [4: dest][2: size][2: mode]   (mode = 0: [size: imm]   mode = 1: [4:][4: r]   mode = 2: [address]   mode = 3: UND)
                case OPCode.Add:  return BinaryOp<ADD>();
                case OPCode.Sub:  return BinaryOp<SUB>();
                case OPCode.Mul:  return BinaryOp<MUL>();
                case OPCode.UDiv: return BinaryOp<UDIV>();
                case OPCode.UMod: return BinaryOp<UMOD>();
                case OPCode.SDiv: return BinaryOp<SDIV>();
                case OPCode.SMod: return BinaryOp<SMOD>();

                case OPCode.SLL: return BinaryOp<SLL>();
                case OPCode.SLR: return BinaryOp<SLR>();
                //case OPCode.SAL: return BinaryOp<SAL>();
                //case OPCode.SAR: return BinaryOp<SAR>();
                //case OPCode.RL: return BinaryOp<RL>();
                //case OPCode.RR: return BinaryOp<RR>();

                case OPCode.And: return BinaryOp<AND>();
                case OPCode.Or:  return BinaryOp<OR>();
                case OPCode.Xor: return BinaryOp<XOR>();

                case OPCode.Cmp:  return BinaryOp<SUB>(false);
                case OPCode.Test: return BinaryOp<AND>(false);

                // [8: unary op]   [4: dest][2:][2: size]
                case OPCode.Inc: return UnaryOp<INC>();
                case OPCode.Dec: return UnaryOp<DEC>();
                case OPCode.Not: return UnaryOp<NOT>();
                case OPCode.Abs: return UnaryOp<ABS>();
                case OPCode.Id: return UnaryOp<ID>(false);

                // [8: la]   [4:][4: dest]   [address]
                case OPCode.La:
                    if (!GetMem(1, ref a) || !GetAddress(ref b)) return false;
                    Registers[a & 15].x64 = b;
                    return true;

                // [8: Jcc]   [address]
                case OPCode.Jmp: if (!GetAddress(ref a)) return false; Pos = a; return true;

                case OPCode.Ja:  if (!GetAddress(ref a)) return false; if (Flags.a) Pos = a;  return true;
                case OPCode.Jae: if (!GetAddress(ref a)) return false; if (Flags.ae) Pos = a; return true;
                case OPCode.Jb:  if (!GetAddress(ref a)) return false; if (Flags.b) Pos = a;  return true;
                case OPCode.Jbe: if (!GetAddress(ref a)) return false; if (Flags.be) Pos = a; return true;

                case OPCode.Jg:  if (!GetAddress(ref a)) return false; if (Flags.g) Pos = a;  return true;
                case OPCode.Jge: if (!GetAddress(ref a)) return false; if (Flags.ge) Pos = a; return true;
                case OPCode.Jl:  if (!GetAddress(ref a)) return false; if (Flags.l) Pos = a;  return true;
                case OPCode.Jle: if (!GetAddress(ref a)) return false; if (Flags.le) Pos = a; return true;

                case OPCode.Jz:  if (!GetAddress(ref a)) return false; if (Flags.Z) Pos = a;  return true;
                case OPCode.Jnz: if (!GetAddress(ref a)) return false; if (!Flags.Z) Pos = a; return true;
                case OPCode.Js:  if (!GetAddress(ref a)) return false; if (Flags.S) Pos = a;  return true;
                case OPCode.Jns: if (!GetAddress(ref a)) return false; if (!Flags.S) Pos = a; return true;
                case OPCode.Jp:  if (!GetAddress(ref a)) return false; if (Flags.P) Pos = a;  return true;
                case OPCode.Jnp: if (!GetAddress(ref a)) return false; if (!Flags.P) Pos = a; return true;
                case OPCode.Jo:  if (!GetAddress(ref a)) return false; if (Flags.O) Pos = a;  return true;
                case OPCode.Jno: if (!GetAddress(ref a)) return false; if (!Flags.O) Pos = a; return true;
                case OPCode.Jc:  if (!GetAddress(ref a)) return false; if (Flags.C) Pos = a;  return true;
                case OPCode.Jnc: if (!GetAddress(ref a)) return false; if (!Flags.C) Pos = a; return true;

                case OPCode.Fadd: return BinaryOp<FADD>();
                case OPCode.Fsub: return BinaryOp<FSUB>();
                case OPCode.Fmul: return BinaryOp<FMUL>();
                case OPCode.Fdiv: return BinaryOp<FDIV>();
                case OPCode.Fmod: return BinaryOp<FMOD>();

                case OPCode.Fpow: return BinaryOp<FPOW>();
                case OPCode.Fsqrt: return UnaryOp<FSQRT>();
                case OPCode.Fexp: return UnaryOp<FEXP>();
                case OPCode.Fln: return UnaryOp<FLN>();
                case OPCode.Fabs: return UnaryOp<FABS>();
                case OPCode.Fid: return UnaryOp<FID>(false);

                case OPCode.Fsin: return UnaryOp<FSIN>();
                case OPCode.Fcos: return UnaryOp<FCOS>();
                case OPCode.Ftan: return UnaryOp<FTAN>();

                case OPCode.Fsinh: return UnaryOp<FSINH>();
                case OPCode.Fcosh: return UnaryOp<FCOSH>();
                case OPCode.Ftanh: return UnaryOp<FTANH>();

                case OPCode.Fasin: return UnaryOp<FASIN>();
                case OPCode.Facos: return UnaryOp<FACOS>();
                case OPCode.Fatan: return UnaryOp<FATAN>();
                case OPCode.Fatan2: return BinaryOp<FATAN2>();

                case OPCode.Ffloor: return UnaryOp<FFLOOR>();
                case OPCode.Fceil: return UnaryOp<FCEIL>();
                case OPCode.Fround: return UnaryOp<FROUND>();
                case OPCode.Ftrunc: return UnaryOp<FTRUNC>();

                case OPCode.Fcmp: return BinaryOp<FSUB>(false);

                case OPCode.FtoI: return UnaryOp<FTOI>();
                case OPCode.ItoF: return UnaryOp<ITOF>();

                // [8: push]   [4: source][2: size][2: mode]   ([size: imm])   (mode = 0: push imm   mode = 0: push register   otherwise undefined)
                case OPCode.Push:
                    if (!GetMem(1, ref a)) return false;
                    switch (a & 3)
                    {
                        case 0: if (!GetMem(Size((a >> 2) & 3), ref b)) return false; break;
                        case 1: b = Registers[a >> 4].x64; break;
                        default: return false;
                    }
                    Registers[15].x64 -= Size((a >> 2) & 3);
                    if (!SetMem(Registers[15].x64, Size((a >> 2) & 3), b)) return false;
                    return true;
                // [8: pop]   [4: dest][2:][2: size]
                case OPCode.Pop:
                    if (!GetMem(1, ref a) || !GetMem(Registers[15].x64, Size(a & 3), ref b)) return false;
                    Registers[15].x64 += Size(a & 3);
                    Registers[a >> 4].Set(a & 3, b);
                    return true;
                // [8: call]   [address]
                case OPCode.Call:
                    if (!GetAddress(ref a)) return false;
                    Registers[15].x64 -= 8;
                    if (!SetMem(Registers[15].x64, 8, Pos)) return false;
                    Pos = a;
                    return true;
                // [8: ret]
                case OPCode.Ret:
                    if (!GetMem(Registers[15].x64, 8, ref a)) return false;
                    Registers[15].x64 += 8;
                    Pos = a;
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
            None, ArgCount, MissingSize, ArgError, FormatError, UsageError, UnknownOp, EmptyFile, InvalidLabel, SymbolRedefinition
        }
        public enum LinkError
        {
            None, EmptyResult, SymbolRedefinition, MissingSymbol
        }

        public struct Symbol
        {
            public UInt64 Value;
            public bool IsAddress;
            public bool IsFloating;
        }
        public class Hole
        {
            public struct Segment
            {
                public string Symbol;
                public bool IsNegative;
            }

            // -------------------

            public UInt64 Address;
            public UInt64 Size;

            public UInt64 Value = 0;
            public double FValue = 0;
            public bool Floating = false;

            public List<Segment> Segments = new List<Segment>();

            // -------------------

            public bool Append(ObjectFile file, string sub)
            {
                if (sub.Length == 0) return false;
                string _sub = sub[0] == '+' || sub[0] == '-' ? sub.Substring(1) : sub;
                if (_sub.Length == 0) return false;

                // if we can get a value for it, add it
                if (TryParseInstantImm(file, sub, out UInt64 temp, out bool floating))
                {
                    // add location depends on floating or not
                    if (floating) { Floating = true; FValue += AsDouble(temp); }
                    else Value += temp;
                }
                // account for the __pos__ symbol (due to being an address, it's value is meaningless until link time)
                else if (_sub == "__pos__")
                {
                    // get the position symbol
                    Symbol __pos__ = file.Symbols["__pos__"];
                    // create a virtual label name to refer to current value of __pos__
                    string virt_label = $"{__pos__.Value:x16}";

                    // create the clone symbol
                    file.Symbols[virt_label] = __pos__;
                    // add virtual symbol to segments
                    Segments.Add(new Segment() { Symbol = virt_label, IsNegative = sub[0] == '-' });
                }
                // otherwise add a segment if it's a legal label
                else if (IsValidLabel(_sub)) Segments.Add(new Segment() { Symbol = _sub, IsNegative = sub[0] == '-' });
                else return false;

                return true;
            }
        }
        public class ObjectFile
        {
            public Dictionary<string, Symbol> Symbols = new Dictionary<string, Symbol>();

            public List<string> GlobalSymbols = new List<string>();
            public List<Hole> Holes = new List<Hole>();
            public List<byte> Data = new List<byte>();
        }

        /// <summary>
        /// The maximum value for an emission multiplier
        /// </summary>
        public const UInt64 EmissionMaxMultiplier = 1024;
        public const UInt64 Version = 0;

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

        private static void Append(ObjectFile file, UInt64 size, UInt64 val)
        {
            // write the value (little-endian)
            for (ushort i = 0; i < size; ++i)
                file.Data.Add((byte)(val >> (8 * i)));
        }
        private static void Append(ObjectFile file, UInt64 size, Hole hole)
        {
            // if we can fill it immediately, do so
            if (hole.Segments.Count == 0)
            {
                Append(file, size, hole.Floating ? AsUInt64(hole.Value.MakeSigned() + hole.FValue) : hole.Value);
            }
            // otherwise there really is a hole
            else
            {
                // store position data
                hole.Size = size;
                hole.Address = (UInt64)file.Data.LongCount();

                // add the hole for later linking
                file.Holes.Add(hole);
                // write a dummy (all 1's for easy manual identification)
                Append(file, size, 0xffffffffffffffff);
            }
        }
        private static void AppendAddress(ObjectFile file, UInt64 a, UInt64 b, Hole hole)
        {
            // [1: literal][3: m1][1: -m2][3: m2]   ([4: r1][4: r2])   ([64: imm])
            Append(file, 1, a);
            if ((a & 0x77) != 0) Append(file, 1, b);
            if ((a & 0x80) != 0) Append(file, 8, hole);
        }

        private static bool TryParseInstantImm(ObjectFile file, string token, out UInt64 res, out bool floating)
        {
            int pos = 0, end = 0;   // position in token
            UInt64 temp = 0;        // placeholders for parsing
            double ftemp, fsum = 0; // floating point parsing temporary and sum

            // result initially integral zero
            res = 0; 
            floating = false;

            if (token.Length == 0) return false;

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
                if (_sub.Length == 0) return false;

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
                    else return false;
                    aft:;
                }
                // if it's an instant symbol
                else if (file.Symbols.TryGetValue(_sub, out Symbol symbol) && !symbol.IsAddress)
                {
                    // add depending on floating or not
                    if (symbol.IsFloating) { floating = true; fsum += sub[0] == '-' ? -AsDouble(symbol.Value) : AsDouble(symbol.Value); }
                    else res += sub[0] == '-' ? ~symbol.Value + 1 : symbol.Value;
                }
                // otherwise it's a dud
                else return false;

                // start of next token includes separator
                pos = end;
            }

            // if result is floating, recalculate res as sum of floating and integral components
            if (floating) res = AsUInt64(res + fsum);

            return true;
        }

        private static bool TryParseRegister(ObjectFile file, string token, out UInt64 res)
        {
            res = 0;
            return token.Length >= 2 && token[0] == '$' && TryParseInstantImm(file, token.Substring(1), out res, out bool floating) && !floating && res < 16;
        }
        private static bool TryParseSizecode(ObjectFile file, string token, out UInt64 res)
        {
            // must be able ti get an instant imm
            if (!TryParseInstantImm(file, token, out res, out bool floating) || floating) return false;

            // convert to size code
            switch (res)
            {
                case 1: res = 0; return true;
                case 2: res = 1; return true;
                case 4: res = 2; return true;
                case 8: res = 3; return true;

                default: return false;
            }
        }
        private static bool TryParseMultcode(ObjectFile file, string token, out UInt64 res)
        {
            // must be able ti get an instant imm
            if (!TryParseInstantImm(file, token, out res, out bool floating) || floating) return false;

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

                default: return false;
            }
        }

        private static bool TryParseImm(ObjectFile file, string token, out Hole hole)
        {
            int pos = 0, end = 0; // position in token
            hole = new Hole();    // resulting hole

            if (token.Length == 0) return false;

            while (pos < token.Length)
            {
                // find the next separator
                for (int i = pos + 1; i < token.Length; ++i)
                    if (token[i] == '+' || token[i] == '-') { end = i; break; }
                // if nothing found, end is end of token
                if (pos == end) end = token.Length;

                string sub = token.Substring(pos, end - pos);

                // append subtoken to the hole
                if (!hole.Append(file, sub)) return false;

                // start of next token includes separator
                pos = end;
            }

            return true;
        }

        private static bool TryParseAddressReg(ObjectFile file, string token, out UInt64 r, out UInt64 m)
        {
            r = m = 0;

            // remove sign
            string _seg = token[0] == '+' || token[0] == '-' ? token.Substring(1) : token;
            if (_seg.Length == 0) return false;

            // split on multiplication
            string[] _r = _seg.Split(new char[] { '*' });

            // if 1 token, is a register
            if (_r.Length == 1)
            {
                m = 1;
                return TryParseRegister(file, _r[0], out r);
            }
            // if 2 tokens, has a multcode
            else if (_r.Length == 2)
            {
                if (TryParseRegister(file, _r[0], out r) && TryParseMultcode(file, _r[1], out m)) ;
                else if (TryParseRegister(file, _r[1], out r) && TryParseMultcode(file, _r[0], out m)) ;
                else return false;
            }
            // otherwise is illegal
            else return false;

            return true;
        }
        // [1: literal][3: m1][1: -m2][3: m2]   ([4: r1][4: r2])   ([64: imm])
        private static bool TryParseAddress(ObjectFile file, string token, out UInt64 a, out UInt64 b, out Hole hole)
        {
            a = b = 0;
            hole = new Hole();

            // must be of [*] format
            if (token.Length < 3 || token[0] != '[' || token[token.Length - 1] != ']') return false;

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
                        else return false;
                    }
                    // otherwise positive
                    else
                    {
                        // if r1 empty, put it there
                        if (r1_seg == null) r1_seg = sub;
                        // otherwise try r2
                        else if (r2_seg == null) r2_seg = sub;
                        // otherwise fail
                        else return false;
                    }
                }
                // otherwise tack it onto the hole
                else if (!hole.Append(file, sub)) return false;

                // start of next token includes separator
                pos = end;
            }

            // register parsing temporaries
            UInt64 r1 = 0, r2 = 0, m1 = 0, m2 = 0;

            // process regs
            if (r1_seg != null && !TryParseAddressReg(file, r1_seg, out r1, out m1)) return false;
            if (r2_seg != null && !TryParseAddressReg(file, r2_seg, out r2, out m2)) return false;

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
        public static UInt64 Time()
        {
            return (UInt64)DateTime.UtcNow.Ticks;
        }

        private static bool TryProcessBinaryOp(ObjectFile file, string[] tokens, int line, OPCode op, ref Tuple<AssembleError, string> err)
        {
            // [8: binary op]   [4: dest][2: size][2: mode]   (mode = 0: [size: imm]   mode = 1: [4:][4: r]   mode = 2: [address]   mode = 3: UND)

            UInt64 a, b, c, d; // parsing temporaries
            Hole hole;

            if (tokens.Length != 4) { err = new Tuple<AssembleError, string>(AssembleError.ArgCount, $"line {line}: Binary OP expected 3 args"); return false; }
            if (!TryParseSizecode(file, tokens[1], out a)) { err = new Tuple<AssembleError, string>(AssembleError.MissingSize, $"line {line}: Binary OP expected size parameter as first arg"); return false; }
            if (!TryParseRegister(file, tokens[2], out b)) { err = new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {line}: Binary OP expected register parameter as second arg"); return false; }

            Append(file, 1, (UInt64)op);
            if (TryParseImm(file, tokens[3], out hole))
            {
                Append(file, 1, (b << 4) | (a << 2) | 0);
                Append(file, Size(a), hole);
            }
            else if (TryParseRegister(file, tokens[3], out c))
            {
                Append(file, 1, (b << 4) | (a << 2) | 1);
                Append(file, 1, c);
            }
            else if (TryParseAddress(file, tokens[3], out c, out d, out hole))
            {
                Append(file, 1, (b << 4) | (a << 2) | 2);
                AppendAddress(file, c, d, hole);
            }
            else { err = new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {line}: Unknown binary OP format"); return false; }

            return true;
        }
        private static bool TryProcessUnaryOp(ObjectFile file, string[] tokens, int line, OPCode op, ref Tuple<AssembleError, string> err)
        {
            // [8: unary op]   [4: dest][2:][2: size]

            if (tokens.Length != 3) { err = new Tuple<AssembleError, string>(AssembleError.ArgCount, $"line {line}: Unary OP expected 2 args"); return false; }
            if (!TryParseSizecode(file, tokens[1], out UInt64 sizecode)) { err = new Tuple<AssembleError, string>(AssembleError.MissingSize, $"line {line}: Unary OP expected size parameter as first arg"); return false; }
            if (!TryParseRegister(file, tokens[2], out UInt64 reg)) { err = new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {line}: Unary OP expected register parameter as second arg"); return false; }

            Append(file, 1, (UInt64)op);
            Append(file, 1, (reg << 4) | sizecode);

            return true;
        }
        private static bool TryProcessJump(ObjectFile file, string[] tokens, int line, OPCode op, ref Tuple<AssembleError, string> err)
        {
            // [8: Jcc]   [address]

            if (tokens.Length != 2) { err = new Tuple<AssembleError, string>(AssembleError.ArgCount, $"line {line}: Jump expected 1 arg"); return false; }
            if (!TryParseAddress(file, tokens[1], out UInt64 a, out UInt64 b, out Hole hole)) { err = new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {line}: Jump expected address as first arg"); return false; }

            Append(file, 1, (UInt64)op);
            AppendAddress(file, a, b, hole);

            return true;
        }
        private static bool TryProcessEmission(ObjectFile file, string[] tokens, int line, UInt64 size, ref Tuple<AssembleError, string> err)
        {
            if (tokens.Length < 2) { err = new Tuple<AssembleError, string>(AssembleError.ArgCount, $"line {line}: Emission expected at least one value"); return false; }
            
            Hole hole = new Hole(); // initially empty hole (to allow for buffer shorthand e.x. "emit x32")
            UInt64 mult;
            bool floating;

            for (int i = 1; i < tokens.Length; ++i)
            {
                // if a multiplier
                if (tokens[i][0] == 'x')
                {
                    // get the multiplier and ensure is valid
                    if (!TryParseInstantImm(file, tokens[i].Substring(1), out mult, out floating)) { err = new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {line}: Unable to parse multiplier \"{tokens[i]}\""); return false; }
                    if (floating) { err = new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {line}: Emission multiplier cannot be floating point"); return false; }
                    if (mult > EmissionMaxMultiplier) { err = new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {line}: Emission multiplier cannot exceed {EmissionMaxMultiplier}"); return false; }
                    if (mult == 0) { err = new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {line}: Emission multiplier cannot be zero"); return false; }

                    // account for first written value
                    if (tokens[i - 1][0] != 'x') --mult;
                }
                // otherwise is a value
                else
                {
                    // get the value
                    if (!TryParseImm(file, tokens[i], out hole)) { err = new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {line}: Emission unable to parse value \"{tokens[i]}\""); return false; }

                    // make one of them
                    mult = 1;
                }

                // write the value(s)
                for (UInt64 j = 0; j < mult; ++j)
                    Append(file, size, hole);
            }

            return true;
        }
        private static bool TryProcessMove(ObjectFile file, string[] tokens, int line, OPCode op, ref Tuple<AssembleError, string> err)
        {
            // [8: mov]   [4: source/dest][2: size][2: mode]   (mode = 0: load imm [size: imm]   mode = 1: load register [4:][4: source]   mode = 2: load memory [address]   mode = 3: store register [address])

            UInt64 sizecode, a, b, c;
            Hole hole;

            if (tokens.Length != 4) { err = new Tuple<AssembleError, string>(AssembleError.ArgCount, $"line {line}: MOV expected 3 args"); return false; }
            if (!TryParseSizecode(file, tokens[1], out sizecode)) { err = new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {line}: MOV expected size parameter as first arg"); return false; }

            Append(file, 1, (UInt64)op);

            // loading
            if (TryParseRegister(file, tokens[2], out a))
            {
                // from imm
                if (TryParseImm(file, tokens[3], out hole))
                {
                    Append(file, 1, (a << 4) | (sizecode << 2) | 0);
                    Append(file, Size(sizecode), hole);
                }
                // from register
                else if (TryParseRegister(file, tokens[3], out b))
                {
                    Append(file, 1, (a << 4) | (sizecode << 2) | 1);
                    Append(file, 1, b);
                }
                // from memory
                else if (TryParseAddress(file, tokens[3], out b, out c, out hole))
                {
                    Append(file, 1, (a << 4) | (sizecode << 2) | 2);
                    AppendAddress(file, b, c, hole);
                }
                else { err = new Tuple<AssembleError, string>(AssembleError.UsageError, $"line {line}: Unknown usage of MOV"); return false; }
            }
            // storing
            else if (TryParseAddress(file, tokens[2], out a, out b, out hole))
            {
                // from register
                if (TryParseRegister(file, tokens[3], out c))
                {
                    Append(file, 1, (c << 4) | (sizecode << 2) | 3);
                    AppendAddress(file, a, b, hole);
                }
                else { err = new Tuple<AssembleError, string>(AssembleError.UsageError, $"line {line}: Unknown usage of MOV"); return false; }
            }
            else { err = new Tuple<AssembleError, string>(AssembleError.UsageError, $"line {line}: Unknown usage of MOV"); return false; }

            return true;
        }
        public static Tuple<AssembleError, string> Assemble(string code, out ObjectFile file)
        {
            file = new ObjectFile();
            
            // predefined symbols
            file.Symbols = new Dictionary<string, Symbol>()
            {
                ["__time__"] = new Symbol() { Value = Time(), IsAddress = false, IsFloating = false },
                ["__version__"] = new Symbol() { Value = Version, IsAddress = false, IsFloating = false },

                ["__pinf__"] = new Symbol() { Value = AsUInt64(double.PositiveInfinity), IsAddress = false, IsFloating = true },
                ["__ninf__"] = new Symbol() { Value = AsUInt64(double.NegativeInfinity), IsAddress = false, IsFloating = true },
                ["__nan__"] = new Symbol() { Value = AsUInt64(double.NaN), IsAddress = false, IsFloating = true },

                ["__fmax__"] = new Symbol() { Value = AsUInt64(double.MaxValue), IsAddress = false, IsFloating = true },
                ["__fmin__"] = new Symbol() { Value = AsUInt64(double.MinValue), IsAddress = false, IsFloating = true },
                ["__fepsilon__"] = new Symbol() { Value = AsUInt64(double.Epsilon), IsAddress = false, IsFloating = true },

                ["__pi__"] = new Symbol() { Value = AsUInt64(Math.PI), IsAddress = false, IsFloating = true },
                ["__e__"] = new Symbol() { Value = AsUInt64(Math.E), IsAddress = false, IsFloating = true }
            };

            int line = 0; // current line number
            int pos = 0, end = 0; // position in code

            // potential parsing args for an instruction
            UInt64 a = 0, b = 0, c = 0, d = 0;
            Hole hole;
            Tuple<AssembleError, string> err = null;
            bool floating;

            if (code.Length == 0) return new Tuple<AssembleError, string>(AssembleError.EmptyFile, "The file was empty");

            while (pos < code.Length)
            {
                // find the next separator
                for (int i = pos + 1; i < code.Length; ++i)
                    if (code[i] == '\n') { end = i; break; }
                // if nothing found, end is end of token
                if (pos >= end) end = code.Length;

                // split line into tokens
                string[] tokens = code.Substring(pos, end - pos).Split(new char[] { ' ', '\t', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                ++line; // advance line counter

                // if this marks a label
                if (tokens.Length > 0 && tokens[0][tokens[0].Length - 1] == ':')
                {
                    string label = tokens[0].Substring(0, tokens[0].Length - 1);
                    if (!IsValidLabel(label)) return new Tuple<AssembleError, string>(AssembleError.InvalidLabel, $"line {line}: Symbol name \"{label}\" invalid");
                    if (file.Symbols.ContainsKey(label)) return new Tuple<AssembleError, string>(AssembleError.SymbolRedefinition, $"line {line}: Symbol \"{label}\" was already defined");

                    // add the symbol as an address
                    file.Symbols.Add(label, new Symbol() { Value = (UInt64)file.Data.LongCount(), IsAddress = true, IsFloating = false });

                    // remove the first token
                    string[] _tokens = new string[tokens.Length - 1];
                    for (int i = 1; i < tokens.Length; ++i)
                        _tokens[i - 1] = tokens[i];
                    tokens = _tokens;
                }

                // remove anything after a comment initiator
                for (int i = 0; i < tokens.Length; ++i)
                    if (tokens[i][0] == '#')
                    {
                        string[] _tokens = new string[i];
                        for (int j = 0; j < i; ++j)
                            _tokens[j] = tokens[j];
                        tokens = _tokens;
                        break;
                    }

                // empty lines are ignored
                if (tokens.Length > 0)
                {
                    // update compile-time symbols (for modifications, also update TryParseImm())
                    file.Symbols["__line__"] = new Symbol() { Value = (UInt64)line, IsAddress = false, IsFloating = false };
                    file.Symbols["__pos__"] = new Symbol() { Value = (UInt64)file.Data.LongCount(), IsAddress = true, IsFloating = false };

                    switch (tokens[0].ToUpper())
                    {
                        case "GLOBAL":
                            if (tokens.Length != 2) return new Tuple<AssembleError, string>(AssembleError.ArgCount, $"line {line}: GLOBAL expected 1 arg");
                            if (!IsValidLabel(tokens[1])) return new Tuple<AssembleError, string>(AssembleError.InvalidLabel, $"line {line}: Invalid label name \"{tokens[1]}\"");
                            file.GlobalSymbols.Add(tokens[1]);
                            break;
                        case "DEF":
                            if (tokens.Length != 3) return new Tuple<AssembleError, string>(AssembleError.ArgCount, $"line {line}: DEF expected 2 args");
                            if (!IsValidLabel(tokens[1])) return new Tuple<AssembleError, string>(AssembleError.InvalidLabel, $"line {line}: Invalid label name \"{tokens[1]}\"");
                            if (!TryParseInstantImm(file, tokens[2], out a, out floating)) return new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {line}: DEF expected a number as third arg");
                            if (file.Symbols.ContainsKey(tokens[1])) return new Tuple<AssembleError, string>(AssembleError.SymbolRedefinition, $"line {line}: Symbol \"{tokens[1]}\" was already defined");
                            file.Symbols.Add(tokens[1], new Symbol() { Value = a, IsAddress = false, IsFloating = floating });
                            break;

                        case "BYTE": if (!TryProcessEmission(file, tokens, line, 1, ref err)) return err; break;
                        case "WORD": if (!TryProcessEmission(file, tokens, line, 2, ref err)) return err; break;
                        case "DWORD": if (!TryProcessEmission(file, tokens, line, 4, ref err)) return err; break;
                        case "QWORD": if (!TryProcessEmission(file, tokens, line, 8, ref err)) return err; break;

                        // --------------------------
                        // -- OPCode assembly impl --
                        // --------------------------

                        // [8: op]
                        case "NOP":
                            if (tokens.Length != 1) return new Tuple<AssembleError, string>(AssembleError.ArgCount, $"line {line}: NOP expected 0 args");
                            Append(file, 1, (UInt64)OPCode.Nop); break;
                        case "STOP":
                            if (tokens.Length != 1) return new Tuple<AssembleError, string>(AssembleError.ArgCount, $"line {line}: STOP expected 0 args");
                            Append(file, 1, (UInt64)OPCode.Stop); break;
                        case "SYSCALL":
                            if (tokens.Length != 1) return new Tuple<AssembleError, string>(AssembleError.ArgCount, $"line {line}: SYSCALL expected 0 args");
                            Append(file, 1, (UInt64)OPCode.Syscall); break;

                        // [8: MOVcc]   [4: source/dest][2: size][2: mode]   (mode = 0: load imm [size: imm]   mode = 1: load register [4:][4: source]   mode = 2: load memory [address]   mode = 3: store register [address])
                        case "MOV": if (!TryProcessMove(file, tokens, line, OPCode.Mov, ref err)) return err; break;

                        case "MOVA": case "MOVNBE": if (!TryProcessMove(file, tokens, line, OPCode.MOVa, ref err)) return err; break;
                        case "MOVAE": case "MOVNB": if (!TryProcessMove(file, tokens, line, OPCode.MOVae, ref err)) return err; break;
                        case "MOVB": case "MOVNAE": if (!TryProcessMove(file, tokens, line, OPCode.MOVb, ref err)) return err; break;
                        case "MOVBE": case "MOVNA": if (!TryProcessMove(file, tokens, line, OPCode.MOVbe, ref err)) return err; break;

                        case "MOVG": case "MOVNLE": if (!TryProcessMove(file, tokens, line, OPCode.MOVg, ref err)) return err; break;
                        case "MOVGE": case "MOVNL": if (!TryProcessMove(file, tokens, line, OPCode.MOVge, ref err)) return err; break;
                        case "MOVL": case "MOVNGE": if (!TryProcessMove(file, tokens, line, OPCode.MOVl, ref err)) return err; break;
                        case "MOVLE": case "MOVNG": if (!TryProcessMove(file, tokens, line, OPCode.MOVle, ref err)) return err; break;

                        case "MOVZ": case "MOVE": if (!TryProcessMove(file, tokens, line, OPCode.MOVz, ref err)) return err; break;
                        case "MOVNZ": case "MOVNE": if (!TryProcessMove(file, tokens, line, OPCode.MOVnz, ref err)) return err; break;
                        case "MOVS": if (!TryProcessMove(file, tokens, line, OPCode.MOVs, ref err)) return err; break;
                        case "MOVNS": if (!TryProcessMove(file, tokens, line, OPCode.MOVns, ref err)) return err; break;
                        case "MOVP": if (!TryProcessMove(file, tokens, line, OPCode.MOVp, ref err)) return err; break;
                        case "MOVNP": if (!TryProcessMove(file, tokens, line, OPCode.MOVnp, ref err)) return err; break;
                        case "MOVO": if (!TryProcessMove(file, tokens, line, OPCode.MOVo, ref err)) return err; break;
                        case "MOVNO": if (!TryProcessMove(file, tokens, line, OPCode.MOVno, ref err)) return err; break;
                        case "MOVC": if (!TryProcessMove(file, tokens, line, OPCode.MOVc, ref err)) return err; break;
                        case "MOVNC": if (!TryProcessMove(file, tokens, line, OPCode.MOVnc, ref err)) return err; break;

                        // [8: swap]   [4: r1][4: r2]
                        case "SWAP":
                            if (tokens.Length != 3) return new Tuple<AssembleError, string>(AssembleError.ArgCount, $"line {line}: SWAP expected 2 args");
                            if (!TryParseRegister(file, tokens[1], out a)) return new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {line}: SWAP expected a register as first arg");
                            if (!TryParseRegister(file, tokens[2], out b)) return new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {line}: SWAP expected a register as second arg");

                            Append(file, 1, (UInt64)OPCode.Swap);
                            Append(file, 1, (a << 4) | b);

                            break;

                        // [8: XExtend]   [4: register][2: from size][2: to size]     ---     XExtend from to reg
                        case "UEXTEND":
                        case "SEXTEND":
                            if (!TryParseSizecode(file, tokens[1], out a)) return new Tuple<AssembleError, string>(AssembleError.MissingSize, $"line {line}: UEXTEND expected size parameter as first arg");
                            if (!TryParseSizecode(file, tokens[1], out b)) return new Tuple<AssembleError, string>(AssembleError.MissingSize, $"line {line}: UEXTEND expected size parameter as second arg");
                            if (!TryParseRegister(file, tokens[2], out c)) return new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {line}: UEXTEND expected register parameter as third arg");

                            Append(file, 1, (UInt64)(tokens[0].ToUpper() == "UEXTEND" ? OPCode.UExtend : OPCode.SExtend));
                            Append(file, 1, (c << 4) | (a << 2) | b);

                            break;

                        // [8: binary op]   [4: dest][2: size][2: mode]   (mode = 0: [size: imm]   mode = 1: [4:][4: r]   mode = 2: [address]   mode = 3: UND)
                        case "ADD": if (!TryProcessBinaryOp(file, tokens, line, OPCode.Add, ref err)) return err; break;
                        case "SUB": if (!TryProcessBinaryOp(file, tokens, line, OPCode.Sub, ref err)) return err; break;
                        case "MUL": if (!TryProcessBinaryOp(file, tokens, line, OPCode.Mul, ref err)) return err; break;
                        case "UDIV": if (!TryProcessBinaryOp(file, tokens, line, OPCode.UDiv, ref err)) return err; break;
                        case "UMOD": if (!TryProcessBinaryOp(file, tokens, line, OPCode.UMod, ref err)) return err; break;
                        case "SDIV": if (!TryProcessBinaryOp(file, tokens, line, OPCode.SDiv, ref err)) return err; break;
                        case "SMOD": if (!TryProcessBinaryOp(file, tokens, line, OPCode.SMod, ref err)) return err; break;

                        case "SLL": if (!TryProcessBinaryOp(file, tokens, line, OPCode.SLL, ref err)) return err; break;
                        case "SLR": if (!TryProcessBinaryOp(file, tokens, line, OPCode.SLR, ref err)) return err; break;
                        //case "SAL": if (!TryProcessBinaryOp(file, tokens, line, OPCode.SAL, ref err)) return err; break;
                        //case "SAR": if (!TryProcessBinaryOp(file, tokens, line, OPCode.SAR, ref err)) return err; break;
                        //case "RL": if (!TryProcessBinaryOp(file, tokens, line, OPCode.RL, ref err)) return err; break;
                        //case "RR": if (!TryProcessBinaryOp(file, tokens, line, OPCode.RR, ref err)) return err; break;

                        case "AND": if (!TryProcessBinaryOp(file, tokens, line, OPCode.And, ref err)) return err; break;
                        case "OR": if (!TryProcessBinaryOp(file, tokens, line, OPCode.Or, ref err)) return err; break;
                        case "XOR": if (!TryProcessBinaryOp(file, tokens, line, OPCode.Xor, ref err)) return err; break;

                        case "CMP": if (!TryProcessBinaryOp(file, tokens, line, OPCode.Cmp, ref err)) return err; break;
                        case "TEST": if (!TryProcessBinaryOp(file, tokens, line, OPCode.Test, ref err)) return err; break;

                        // [8: unary op]   [4: dest][2:][2: size]
                        case "INC": if (!TryProcessUnaryOp(file, tokens, line, OPCode.Inc, ref err)) return err; break;
                        case "DEC": if (!TryProcessUnaryOp(file, tokens, line, OPCode.Dec, ref err)) return err; break;
                        case "NEG": if (!TryProcessUnaryOp(file, tokens, line, OPCode.Neg, ref err)) return err; break;
                        case "NOT": if (!TryProcessUnaryOp(file, tokens, line, OPCode.Not, ref err)) return err; break;
                        case "ABS": if (!TryProcessUnaryOp(file, tokens, line, OPCode.Abs, ref err)) return err; break;
                        case "ID":  if (!TryProcessUnaryOp(file, tokens, line, OPCode.Id, ref err)) return err; break;

                        // [8: la]   [4:][4: dest]   [address]
                        case "LA":
                            if (tokens.Length != 3) return new Tuple<AssembleError, string>(AssembleError.ArgCount, $"line {line}: LA expected 2 args");
                            if (!TryParseRegister(file, tokens[1], out a)) return new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {line}: LA expecetd register as first arg");
                            if (!TryParseAddress(file, tokens[2], out b, out c, out hole)) return new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {line}: LA expected address as second arg");

                            Append(file, 1, (UInt64)OPCode.La);
                            Append(file, 1, a);
                            AppendAddress(file, b, c, hole);

                            break;

                        // [8: Jcc]   [address]
                        case "JMP": if (!TryProcessJump(file, tokens, line, OPCode.Jmp, ref err)) return err; break;

                        case "JA": case "JNBE": if (!TryProcessJump(file, tokens, line, OPCode.Ja, ref err)) return err; break;
                        case "JAE": case "JNB": if (!TryProcessJump(file, tokens, line, OPCode.Jae, ref err)) return err; break;
                        case "JB": case "JNAE": if (!TryProcessJump(file, tokens, line, OPCode.Jb, ref err)) return err; break;
                        case "JBE": case "JNA": if (!TryProcessJump(file, tokens, line, OPCode.Jbe, ref err)) return err; break;

                        case "JG": case "JNLE": if (!TryProcessJump(file, tokens, line, OPCode.Jg, ref err)) return err; break;
                        case "JGE": case "JNL": if (!TryProcessJump(file, tokens, line, OPCode.Jge, ref err)) return err; break;
                        case "JL": case "JNGE": if (!TryProcessJump(file, tokens, line, OPCode.Jl, ref err)) return err; break;
                        case "JLE": case "JNG": if (!TryProcessJump(file, tokens, line, OPCode.Jle, ref err)) return err; break;

                        case "JZ": case "JE": if (!TryProcessJump(file, tokens, line, OPCode.Jz, ref err)) return err; break;
                        case "JNZ": case "JNE": if (!TryProcessJump(file, tokens, line, OPCode.Jnz, ref err)) return err; break;
                        case "JS": if (!TryProcessJump(file, tokens, line, OPCode.Js, ref err)) return err; break;
                        case "JNS": if (!TryProcessJump(file, tokens, line, OPCode.Jns, ref err)) return err; break;
                        case "JP": if (!TryProcessJump(file, tokens, line, OPCode.Jp, ref err)) return err; break;
                        case "JNP": if (!TryProcessJump(file, tokens, line, OPCode.Jnp, ref err)) return err; break;
                        case "JO": if (!TryProcessJump(file, tokens, line, OPCode.Jo, ref err)) return err; break;
                        case "JNO": if (!TryProcessJump(file, tokens, line, OPCode.Jno, ref err)) return err; break;
                        case "JC": if (!TryProcessJump(file, tokens, line, OPCode.Jc, ref err)) return err; break;
                        case "JNC": if (!TryProcessJump(file, tokens, line, OPCode.Jnc, ref err)) return err; break;

                        case "FADD": if (!TryProcessBinaryOp(file, tokens, line, OPCode.Fadd, ref err)) return err; break;
                        case "FSUB": if (!TryProcessBinaryOp(file, tokens, line, OPCode.Fsub, ref err)) return err; break;
                        case "FMUL": if (!TryProcessBinaryOp(file, tokens, line, OPCode.Fmul, ref err)) return err; break;
                        case "FDIV": if (!TryProcessBinaryOp(file, tokens, line, OPCode.Fdiv, ref err)) return err; break;
                        case "FMOD": if (!TryProcessBinaryOp(file, tokens, line, OPCode.Fmod, ref err)) return err; break;

                        case "FPOW": if (!TryProcessBinaryOp(file, tokens, line, OPCode.Fpow, ref err)) return err; break;
                        case "FSQRT": if (!TryProcessUnaryOp(file, tokens, line, OPCode.Fsqrt, ref err)) return err; break;
                        case "FEXP": if (!TryProcessUnaryOp(file, tokens, line, OPCode.Fexp, ref err)) return err; break;
                        case "FLN": if (!TryProcessUnaryOp(file, tokens, line, OPCode.Fln, ref err)) return err; break;
                        case "FABS": if (!TryProcessUnaryOp(file, tokens, line, OPCode.Fabs, ref err)) return err; break;
                        case "FID": if (!TryProcessUnaryOp(file, tokens, line, OPCode.Fid, ref err)) return err; break;

                        case "FSIN": if (!TryProcessUnaryOp(file, tokens, line, OPCode.Fsin, ref err)) return err; break;
                        case "FCOS": if (!TryProcessUnaryOp(file, tokens, line, OPCode.Fcos, ref err)) return err; break;
                        case "FTAN": if (!TryProcessUnaryOp(file, tokens, line, OPCode.Ftan, ref err)) return err; break;

                        case "FSINH": if (!TryProcessUnaryOp(file, tokens, line, OPCode.Fsinh, ref err)) return err; break;
                        case "FCOSH": if (!TryProcessUnaryOp(file, tokens, line, OPCode.Fcosh, ref err)) return err; break;
                        case "FTANH": if (!TryProcessUnaryOp(file, tokens, line, OPCode.Ftanh, ref err)) return err; break;

                        case "FASIN": if (!TryProcessUnaryOp(file, tokens, line, OPCode.Fasin, ref err)) return err; break;
                        case "FACOS": if (!TryProcessUnaryOp(file, tokens, line, OPCode.Facos, ref err)) return err; break;
                        case "FATAN": if (!TryProcessUnaryOp(file, tokens, line, OPCode.Fatan, ref err)) return err; break;
                        case "FATAN2": if (!TryProcessBinaryOp(file, tokens, line, OPCode.Fatan2, ref err)) return err; break;

                        case "FFLOOR": if (!TryProcessUnaryOp(file, tokens, line, OPCode.Ffloor, ref err)) return err; break;
                        case "FCEIL": if (!TryProcessUnaryOp(file, tokens, line, OPCode.Fceil, ref err)) return err; break;
                        case "FROUND": if (!TryProcessUnaryOp(file, tokens, line, OPCode.Fround, ref err)) return err; break;
                        case "FTRUNC": if (!TryProcessUnaryOp(file, tokens, line, OPCode.Ftrunc, ref err)) return err; break;

                        case "FCMP": if (!TryProcessBinaryOp(file, tokens, line, OPCode.Fcmp, ref err)) return err; break;

                        case "FTOI": if (!TryProcessUnaryOp(file, tokens, line, OPCode.FtoI, ref err)) return err; break;
                        case "ITOF": if (!TryProcessUnaryOp(file, tokens, line, OPCode.ItoF, ref err)) return err; break;

                        // [8: push]   [4: source][2: size][2: mode]   ([size: imm])   (mode = 0: push imm   mode = 0: push register   otherwise undefined)
                        case "PUSH":
                            if (tokens.Length != 3) return new Tuple<AssembleError, string>(AssembleError.ArgCount, $"line {line}: PUSH expected 2 args");
                            if (!TryParseSizecode(file, tokens[1], out a)) return new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {line}: PUSH size parameter as first arg");

                            Append(file, 1, (UInt64)OPCode.Push);
                            if (TryParseImm(file, tokens[2], out hole))
                            {
                                Append(file, 1, (a << 2) | 0);
                                Append(file, Size(a), hole);
                            }
                            else if (TryParseRegister(file, tokens[2], out b))
                            {
                                Append(file, 1, (b << 4) | (a << 2) | 1);
                            }
                            else return new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {line}: Unknown usage of PUSH");
                            break;
                        // [8: pop]   [4: dest][2:][2: size]
                        case "POP":
                            if (tokens.Length != 3) return new Tuple<AssembleError, string>(AssembleError.ArgCount, $"line {line}: POP expected 2 args");
                            if (!TryParseSizecode(file, tokens[1], out a)) return new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {line}: POP expected size parameter as first arg");
                            if (!TryParseRegister(file, tokens[2], out b)) return new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {line}: POP expected register as second arg");
                            Append(file, 1, (UInt64)OPCode.Pop);
                            Append(file, 1, (b << 4) | a);
                            break;
                        // [8: call]   [address]
                        case "CALL":
                            if (tokens.Length != 2) return new Tuple<AssembleError, string>(AssembleError.ArgCount, $"line {line}: CALL expected one arg");
                            if (!TryParseAddress(file, tokens[1], out a, out b, out hole)) return new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {line}: CALLexpected address as first arg");
                            Append(file, 1, (UInt64)OPCode.Call);
                            AppendAddress(file, a, b, hole);
                            break;
                        // [8: ret]
                        case "RET":
                            if (tokens.Length != 1) return new Tuple<AssembleError, string>(AssembleError.ArgCount, $"line {line}: CALL expected one arg");
                            Append(file, 1, (UInt64)OPCode.Ret);
                            break;

                        default: return new Tuple<AssembleError, string>(AssembleError.UnknownOp, $"line {line}: Couldn't process operator \"{tokens[0]}\"");
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
            UInt64 size = 0;
            foreach (ObjectFile obj in objs) size += (UInt64)obj.Data.LongCount();
            // if zero, there is nothing to link
            if (size == 0) return new Tuple<LinkError, string>(LinkError.EmptyResult, "Resulting file is empty");

            res = new byte[size + 10]; // give it enough memory to write the whole file plus a header and a stack
            size = 10;                 // set size to after header (points to writing position)

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
                offsets[i] = size; // record its starting offset

                objs[i].Data.CopyTo(res, (int)size); // copy its data (POTENTIAL SIZE LIMITATION)
                size += (UInt64)objs[i].Data.LongCount(); // advance write cursor
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
                foreach (Hole hole in objs[i].Holes)
                {
                    // compute the hole value
                    UInt64 value = hole.Value;
                    double fvalue = hole.FValue;
                    bool floating = hole.Floating;

                    Symbol symbol;
                    foreach(Hole.Segment seg in hole.Segments)
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
                    Write(res, hole.Address + offsets[i], hole.Size, floating ? AsUInt64(value.MakeSigned() + fvalue) : value);
                }

            // write the header
            if (!symbols.TryGetValue("main", out Symbol main) || !main.IsAddress) return new Tuple<LinkError, string>(LinkError.MissingSymbol, "No entry point");
            Write(res, 0, 1, (UInt64)OPCode.Jmp);
            Write(res, 1, 1, 0x80);
            Write(res, 2, 8, main.Value);

            // linked successfully
            return new Tuple<LinkError, string>(LinkError.None, string.Empty);
        }
    }
}
