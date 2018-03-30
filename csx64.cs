using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Numerics;
using System.IO;

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
        /// Gets the register partition with the specified size code
        /// </summary>
        /// <param name="code">The size code</param>
        public static UInt64 Get(this CSX64.Register reg, UInt64 code)
        {
            switch (code)
            {
                case 0: return reg.x8;
                case 1: return reg.x16;
                case 2: return reg.x32;
                case 3: return reg.x64;

                default: throw new ArgumentOutOfRangeException("Register code out of range");
            }
        }
        /// <summary>
        /// Sets the register partition with the specified size code
        /// </summary>
        /// <param name="code">The size code</param>
        /// <param name="value">The value to set</param>
        public static void Set(this CSX64.Register reg, UInt64 code, UInt64 value)
        {
            switch (code)
            {
                case 0: reg.x8 = value; return;
                case 1: reg.x16 = value; return;
                case 2: reg.x32 = value; return;
                case 3: reg.x64 = value; return;

                default: throw new ArgumentOutOfRangeException("Register code out of range");
            }
        }

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

        /// <summary>
        /// Writes a little-endian value
        /// </summary>
        /// <param name="arr">the data to write to</param>
        /// <param name="pos">the index to begin at</param>
        /// <param name="size">the size of the value in bytes</param>
        /// <param name="val">the value to write</param>
        public static bool Write<T>(this T arr, UInt64 pos, UInt64 size, UInt64 val) where T : IList<byte>
        {
            // make sure we're not exceeding memory bounds
            if (pos < 0 || pos + size > (UInt64)arr.Count) return false;

            // write the value (little-endian)
            for (ushort i = 0; i < size; ++i)
                arr[(int)pos + i] = (byte)(val >> (8 * i));

            return true;
        }
        /// <summary>
        /// Reads a little-endian value
        /// </summary>
        /// <param name="arr">the data to write to</param>
        /// <param name="pos">the index to begin at</param>
        /// <param name="size">the size of the value in bytes</param>
        /// <param name="res">the read value</param>
        public static bool Read<T>(this T arr, UInt64 pos, UInt64 size, out UInt64 res) where T : IList<byte>
        {
            res = 0; // initialize out param

            // make sure we're not exceeding memory bounds
            if (pos < 0 || pos + size > (UInt64)arr.Count) return false;

            // read the value (little-endian)
            for (ushort i = 0; i < size; ++i)
                res |= (UInt64)arr[(int)pos + i] << (8 * i);

            return true;
        }
    }

    /// <summary>
    /// Represents a computer executing a binary program (little-endian)
    /// </summary>
    public class CSX64 : IDisposable
    {
        // ---------------
        // -- Constants --
        // ---------------

        /// <summary>
        /// Version number
        /// </summary>
        public const UInt64 Version = 0x0413;

        /// <summary>
        /// The flags that can be modified by executing code
        /// </summary>
        private const UInt64 PublicFlags = 0x1f;

        // -----------
        // -- Types --
        // -----------

        public enum ErrorCode
        {
            None, OutOfBounds, UnhandledSyscall, UndefinedBehavior, ArithmeticError, Abort,
            IOFailure
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

        public enum SyscallCodes
        {
            Read, Write,
            Open, Close,
            Move
        }

        /// <summary>
        /// Represents a 64 bit register
        /// </summary>
        public sealed class Register
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
        public sealed class FlagsRegister
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
            public bool SlowMem
            {
                get => (Flags & 0x20ul) != 0;
                set => Flags = (Flags & ~0x20ul) | (value ? 0x20ul : 0);

            }
        }

        // --------------------
        // -- Execution Data --
        // --------------------

        private Register[] Registers = new Register[16];
        private FlagsRegister Flags = new FlagsRegister();

        private byte[] Memory = null;

        private FileStream[] FileDescriptors = new FileStream[4];

        /// <summary>
        /// The current execution positon (executed on next tick)
        /// </summary>
        public UInt64 Pos { get; protected set; }
        /// <summary>
        /// Flag marking if the program is still executing
        /// </summary>
        public bool Running { get; protected set; }

        /// <summary>
        /// The number of ticks the processor is currently sleeping for
        /// </summary>
        public UInt64 Sleep { get; protected set; }

        /// <summary>
        /// Gets the current error code
        /// </summary>
        public ErrorCode Error { get; protected set; }

        /// <summary>
        /// Random object used for randomizing values after initialization
        /// </summary>
        protected readonly Random Rand = new Random();

        // -- accessors -- //

        /// <summary>
        /// Gets the total amount of memory the processor currently has access to
        /// </summary>
        public UInt64 MemorySize => (UInt64)Memory.Length;

        /// <summary>
        /// Gets the current time as used by the assembler
        /// </summary>
        public static UInt64 Time => DateTime.UtcNow.Ticks.MakeUnsigned();

        // -- static initialization -- //

        static CSX64()
        {
            // create definitions for all the syscall codes
            foreach (SyscallCodes item in Enum.GetValues(typeof(SyscallCodes)))
                DefineSymbol($"sys_{item.ToString().ToLower()}", (UInt64)item);
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
        /// <param name="multcode">the code to parse</param>
        protected static UInt64 Mult(UInt64 multcode)
        {
            return multcode == 0 ? 0ul : 1ul << (ushort)(multcode - 1);
        }
        /// <summary>
        /// As MultCode but returns negative value if neg is nonzero
        /// </summary>
        /// <param name="multcode">the code to parse</param>
        /// <param name="neg">the negative boolean</param>
        protected static UInt64 Mult(UInt64 multcode, UInt64 neg)
        {
            return neg == 0 ? Mult(multcode) : ~Mult(multcode) + 1;
        }

        /// <summary>
        /// Interprets a double as its raw bits
        /// </summary>
        /// <param name="val">value to interpret</param>
        protected static unsafe UInt64 DoubleAsUInt64(double val)
        {
            return *(UInt64*)&val;
        }
        /// <summary>
        /// Interprets raw bits as a double
        /// </summary>
        /// <param name="val">value to interpret</param>
        protected static unsafe double AsDouble(UInt64 val)
        {
            return *(double*)&val;
        }

        /// <summary>
        /// Interprets a float as its raw bits (placed in low 32 bits)
        /// </summary>
        /// <param name="val">the float to interpret</param>
        protected static unsafe UInt64 FloatAsUInt64(float val)
        {
            return *(UInt32*)&val;
        }
        /// <summary>
        /// Interprets raw bits as a float (low 32 bits)
        /// </summary>
        /// <param name="val">the bits to interpret</param>
        protected static unsafe float AsFloat(UInt64 val)
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
            if (!GetMemAdv(1, out s)) return false;

            UInt64 a_sizecode = (s >> 2) & 3;
            UInt64 b_sizecode = _b_sizecode == -1 ? (s >> 2) & 3 : (UInt64)_b_sizecode;

            // switch through mode
            switch (s & 3)
            {
                case 0:
                    a = Registers[s >> 4].Get(a_sizecode);
                    if (!GetMemAdv(Size(b_sizecode), out b)) return false;
                    break;
                case 1:
                    a = Registers[s >> 4].Get(a_sizecode);
                    if (!GetAddressAdv(out b) || !GetMem(b, Size(b_sizecode), out b)) return false;
                    break;
                case 2:
                    if (!GetMemAdv(1, out b)) return false;
                    switch ((b >> 4) & 1)
                    {
                        case 0:
                            a = Registers[s >> 4].Get(a_sizecode);
                            b = Registers[b & 15].Get(b_sizecode);
                            break;
                        case 1:
                            if (!GetAddressAdv(out m) || !GetMem(m, Size(a_sizecode), out a)) return false;
                            b = Registers[b & 15].Get(b_sizecode);
                            s |= 256; // mark as memory path of mode 2
                            break;
                    }
                    break;
                case 3:
                    if (!GetMemAdv(Size(b_sizecode), out b)) return false;
                    if (!GetAddressAdv(out m) || !GetMem(m, Size(a_sizecode), out a)) return false;
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
            if (!GetMemAdv(1, out s)) return false;

            UInt64 a_sizecode = (s >> 2) & 3;

            // switch through mode
            switch (s & 1)
            {
                case 0:
                    a = Registers[s >> 4].Get(a_sizecode);
                    break;
                case 1:
                    if (!GetAddressAdv(out m) || !GetMem(m, Size(a_sizecode), out a)) return false;
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

        /*
        [8: imm r m]   [4: reg][2: size][2: mode]
            mode = 0: [size: imm]
            mode = 1: use reg
            mode = 2: [address]
        */
        private bool FetchIMMRMFormat(out UInt64 s, out UInt64 a, int _a_sizecode = -1)
        {
            if (!GetMemAdv(1, out s)) { a = 0; return false; }

            UInt64 a_sizecode = _a_sizecode == -1 ? (s >> 2) & 3 : (UInt64)_a_sizecode;

            // get the value into b
            switch (s & 3)
            {
                case 0: if (!GetMemAdv(Size((s >> 2) & 3), out a)) return false; break;
                case 1: a = Registers[s >> 4].Get((s >> 2) & 3); break;
                case 2: if (!GetAddressAdv(out a) || !GetMem(a, Size((s >> 2) & 3), out a)) return false; break;
                default: Fail(ErrorCode.UndefinedBehavior); { a = 0; return false; }
            }

            return true;
        }

        // updates the flags for integral ops (identical for most integral ops)
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
            if (!FetchBinaryOpFormat(ref s, ref m, ref a, ref b, 0)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UInt16 sh = (UInt16)(b % SizeBits(sizecode));
            UInt64 res = Truncate(a << sh, sizecode);

            UpdateFlagsInt(res, sizecode);

            return StoreBinaryOpFormat(s, m, res);
        }
        private bool ProcessSR()
        {
            UInt64 s = 0, m = 0, a = 0, b = 0;
            if (!FetchBinaryOpFormat(ref s, ref m, ref a, ref b, 0)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UInt16 sh = (UInt16)(b % SizeBits(sizecode));
            UInt64 res = a >> sh;

            UpdateFlagsInt(res, sizecode);

            return StoreBinaryOpFormat(s, m, res);
        }

        private bool ProcessSAL()
        {
            UInt64 s = 0, m = 0, a = 0, b = 0;
            if (!FetchBinaryOpFormat(ref s, ref m, ref a, ref b, 0)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UInt16 sh = (UInt16)(b % SizeBits(sizecode));
            UInt64 res = Truncate((SignExtend(a, sizecode).MakeSigned() << sh).MakeUnsigned(), sizecode);

            UpdateFlagsInt(res, sizecode);

            return StoreBinaryOpFormat(s, m, res);
        }
        private bool ProcessSAR()
        {
            UInt64 s = 0, m = 0, a = 0, b = 0;
            if (!FetchBinaryOpFormat(ref s, ref m, ref a, ref b, 0)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UInt16 sh = (UInt16)(b % SizeBits(sizecode));
            UInt64 res = Truncate((SignExtend(a, sizecode).MakeSigned() >> sh).MakeUnsigned(), sizecode);

            UpdateFlagsInt(res, sizecode);

            return StoreBinaryOpFormat(s, m, res);
        }

        private bool ProcessRL()
        {
            UInt64 s = 0, m = 0, a = 0, b = 0;
            if (!FetchBinaryOpFormat(ref s, ref m, ref a, ref b, 0)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UInt16 sh = (UInt16)(b % SizeBits(sizecode));
            UInt64 res = Truncate((a << sh) | (a >> ((UInt16)SizeBits(sizecode) - sh)), sizecode);

            UpdateFlagsInt(res, sizecode);

            return StoreBinaryOpFormat(s, m, res);
        }
        private bool ProcessRR()
        {
            UInt64 s = 0, m = 0, a = 0, b = 0;
            if (!FetchBinaryOpFormat(ref s, ref m, ref a, ref b, 0)) return false;
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

        private bool ProcessFPOW()
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
        private bool ProcessFSQRT()
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
        private bool ProcessFEXP()
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
        private bool ProcessFLN()
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

        private bool ProcessFSIN()
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
        private bool ProcessFCOS()
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
        private bool ProcessFTAN()
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

        private bool ProcessFSINH()
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
        private bool ProcessFCOSH()
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
        private bool ProcessFTANH()
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

        private bool ProcessFASIN()
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
        private bool ProcessFACOS()
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
        private bool ProcessFATAN()
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
        private bool ProcessFATAN2()
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
            UInt64 s, a;

            if (!FetchIMMRMFormat(out s, out a)) return false;

            // switch through register sizes
            switch ((s >> 2) & 3)
            {
                case 0:
                    Registers[0].x16 = Registers[0].x8 * a;
                    Flags.C = Flags.O = (Registers[0].x16 >> 8) != 0;
                    break;
                case 1:
                    Registers[0].x32 = Registers[0].x16 * a;
                    Flags.C = Flags.O = (Registers[0].x32 >> 16) != 0;
                    break;
                case 2:
                    Registers[0].x64 = Registers[0].x32 * a;
                    Flags.C = Flags.O = (Registers[0].x64 >> 32) != 0;
                    break;
                case 3: // 64 bits requires extra logic
                    BigInteger full = new BigInteger(Registers[0].x64) * new BigInteger(a);
                    Registers[0].x64 = (UInt64)(full & 0xffffffffffffffff);
                    Registers[1].x64 = (UInt64)(full >> 64);
                    Flags.C = Flags.O = Registers[1].x64 != 0;
                    break;
            }

            return true;
        }
        private bool ProcessSMUL()
        {
            UInt64 s, a;

            if (!FetchIMMRMFormat(out s, out a)) return false;

            // switch through register sizes
            switch ((s >> 2) & 3)
            {
                case 0:
                    Registers[0].x16 = (SignExtend(Registers[0].x8, 0).MakeSigned() * SignExtend(a, 0).MakeSigned()).MakeUnsigned();
                    Flags.C = Flags.O = (Registers[0].x16 >> 8) == 0 && Positive(Registers[0].x8, 0) || (Registers[0].x16 >> 8) == 0xff && Negative(Registers[0].x8, 0);
                    Flags.S = Negative(Registers[0].x16, 1);
                    break;
                case 1:
                    Registers[0].x32 = (SignExtend(Registers[0].x16, 1).MakeSigned() * SignExtend(a, 1).MakeSigned()).MakeUnsigned();
                    Flags.C = Flags.O = (Registers[0].x32 >> 16) == 0 && Positive(Registers[0].x16, 1) || (Registers[0].x32 >> 16) == 0xffff && Negative(Registers[0].x16, 1);
                    Flags.S = Negative(Registers[0].x32, 2);
                    break;
                case 2:
                    Registers[0].x64 = (SignExtend(Registers[0].x32, 2).MakeSigned() * SignExtend(a, 2).MakeSigned()).MakeUnsigned();
                    Flags.C = Flags.O = (Registers[0].x64 >> 32) == 0 && Positive(Registers[0].x32, 2) || (Registers[0].x64 >> 32) == 0xffffffff && Negative(Registers[0].x32, 2);
                    Flags.S = Negative(Registers[0].x64, 3);
                    break;
                case 3: // 64 bits requires extra logic
                    // store negative flag (we'll do the multiplication in signed values since bit shifting is well-defined for positive BigInteger)
                    bool neg = false;
                    if (Negative(Registers[0].x64, 3)) { neg = !neg; Registers[0].x64 = ~Registers[0].x64 + 1; }
                    if (Negative(a, 3)) { neg = !neg; a = ~a + 1; }

                    // form the full (positive) product
                    BigInteger full = new BigInteger(Registers[0].x64) * new BigInteger(a);
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
            UInt64 s, a, full;
            BigInteger bigraw, bigfull;

            if (!FetchIMMRMFormat(out s, out a)) return false;

            if (a == 0) { Fail(ErrorCode.ArithmeticError); return false; }

            // switch through register sizes
            switch ((s >> 2) & 3)
            {
                case 0:
                    full = Registers[0].x16 / a;
                    if ((full >> 8) != 0) { Fail(ErrorCode.ArithmeticError); return false; }
                    Registers[1].x8 = Registers[0].x16 % a;
                    Registers[0].x8 = full;
                    Flags.C = Registers[1].x8 != 0;
                    break;
                case 1:
                    full = Registers[0].x32 / a;
                    if ((full >> 16) != 0) { Fail(ErrorCode.ArithmeticError); return false; }
                    Registers[1].x16 = Registers[0].x32 % a;
                    Registers[0].x16 = full;
                    Flags.C = Registers[1].x16 != 0;
                    break;
                case 2:
                    full = Registers[0].x64 / a;
                    if ((full >> 32) != 0) { Fail(ErrorCode.ArithmeticError); return false; }
                    Registers[1].x32 = Registers[0].x64 % a;
                    Registers[0].x32 = full;
                    Flags.C = Registers[1].x32 != 0;
                    break;
                case 3: // 64 bits requires extra logic
                    bigraw = (new BigInteger(Registers[1].x64) << 64) | new BigInteger(Registers[0].x64);
                    bigfull = bigraw / new BigInteger(a);

                    if ((bigfull >> 64) != 0) { Fail(ErrorCode.ArithmeticError); return false; }

                    Registers[1].x64 = (UInt64)(bigraw % new BigInteger(a));
                    Registers[0].x64 = (UInt64)bigfull;
                    Flags.C = Registers[1].x64 != 0;
                    break;
            }

            return true;
        }
        private bool ProcessSDIV()
        {
            UInt64 s, a;
            Int64 _a, _b, full;
            BigInteger bigraw, bigfull;

            if (!FetchIMMRMFormat(out s, out a)) return false;

            if (a == 0) { Fail(ErrorCode.ArithmeticError); return false; }

            // switch through register sizes
            switch ((s >> 2) & 3)
            {
                case 0:
                    _a = SignExtend(Registers[0].x16, 1).MakeSigned();
                    _b = SignExtend(a, 0).MakeSigned();
                    full = _a / _b;

                    if (full != (sbyte)full) { Fail(ErrorCode.ArithmeticError); return false; }

                    Registers[0].x8 = full.MakeUnsigned();
                    Registers[1].x8 = (_a % _b).MakeUnsigned();
                    Flags.C = Registers[1].x8 != 0;
                    Flags.S = Negative(Registers[0].x8, 0);
                    break;
                case 1:
                    _a = SignExtend(Registers[0].x32, 2).MakeSigned();
                    _b = SignExtend(a, 1).MakeSigned();
                    full = _a / _b;

                    if (full != (Int16)full) { Fail(ErrorCode.ArithmeticError); return false; }

                    Registers[0].x16 = full.MakeUnsigned();
                    Registers[1].x16 = (_a % _b).MakeUnsigned();
                    Flags.C = Registers[1].x16 != 0;
                    Flags.S = Negative(Registers[0].x16, 1);
                    break;
                case 2:
                    _a = Registers[0].x64.MakeSigned();
                    _b = SignExtend(a, 2).MakeSigned();
                    full = _a / _b;

                    if (full != (Int32)full) { Fail(ErrorCode.ArithmeticError); return false; }

                    Registers[0].x32 = full.MakeUnsigned();
                    Registers[1].x32 = (_a % _b).MakeUnsigned();
                    Flags.C = Registers[1].x32 != 0;
                    Flags.S = Negative(Registers[0].x32, 2);
                    break;
                case 3: // 64 bits requires extra logic
                    _b = a.MakeSigned();
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
            UInt64 a, b, c, d;

            if (!GetMemAdv(1, out a)) return false;
            switch (a & 1)
            {
                case 0:
                    if (!GetMemAdv(1, out b)) return false;
                    c = Registers[a >> 4].x64;
                    Registers[a >> 4].Set((a >> 2) & 3, Registers[b & 15].x64);
                    Registers[b & 15].Set((a >> 2) & 3, c);
                    break;
                case 1:
                    if (!GetAddressAdv(out b) || !GetMem(b, Size((a >> 2) & 3), out c)) return false;
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

            // define initial state
            Running = false;
            Error = ErrorCode.None;
        }

        /// <summary>
        /// Disposes of any unmanaged resources that were allocated during execution
        /// </summary>
        ~CSX64()
        {
            Dispose(false);
        }

        /// <summary>
        /// Disposes of unmanaged resources
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Marks if this object has already been disposed. DO NOT MODIFY
        /// </summary>
        private bool disposed = false;
        /// <summary>
        /// Relaeses all the resources used by this object
        /// </summary>
        /// <param name="disposing">if managed resources should be released</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    // close all the file descriptors
                    for (UInt64 i = 0; i < (UInt64)FileDescriptors.Length; ++i) FS_Close(i);
                }

                disposed = true;
            }
        }

        /// <summary>
        /// Initializes the computer for execution. Returns true if successful (fails on insufficient memory)
        /// </summary>
        /// <param name="data">The memory to load before starting execution (extra memory is undefined)</param>
        /// <param name="stacksize">the amount of additional space to allocate for the program's stack</param>
        public bool Initialize(Byte[] data, UInt64 stacksize = 2 * 1024 * 1024)
        {
            // make sure we're not loading null array
            if (data == null || data.Length == 0) return false;
            
            // get new memory array
            Memory = new byte[(UInt64)data.Length + stacksize];
            
            // copy over the data
            data.CopyTo(Memory, 0);

            // randomize registers except stack register
            for (int i = Registers.Length - 2; i >= 0; --i)
            {
                Registers[i].x32 = (UInt64)Rand.Next();
                Registers[i].x64 <<= 32;
                Registers[i].x32 = (UInt64)Rand.Next();
            }
            // randomize flags
            Flags.Flags = (UInt64)Rand.Next() & PublicFlags;
            // initialize stack register to end of memory segment
            Registers[15].x64 = (UInt64)Memory.Length;

            // set execution state
            Pos = 0;
            Running = true;
            Error = ErrorCode.None;
            Sleep = 0;

            return true;
        }

        /// <summary>
        /// Causes the machine to fail. Releases unmanaged resources allocated during execution
        /// </summary>
        /// <param name="code">The error code to emit</param>
        public void Fail(ErrorCode code)
        {
            if (Running)
            {
                Error = code;
                Running = false;

                // close all the file descriptors
                for (UInt64 i = 0; i < (UInt64)FileDescriptors.Length; ++i) FS_Close(i);
            }
        }

        /// <summary>
        /// Gets the specified register in this computer (no bounds checking: test index against NRegisters)
        /// </summary>
        /// <param name="index">The index of the register</param>
        public Register GetRegister(int index) => Registers[index];
        /// <summary>
        /// Returns the flags register
        /// </summary>
        public FlagsRegister GetFlags() => Flags;

        /// <summary>
        /// Gets the file descriptor at the specified index. DO NOT CLOSE/OPEN THIS OBJECT. USE CLOSE/OPEN UTILITY FUNCTIONS
        /// </summary>
        /// <param name="index">the index of the file descriptor</param>
        public FileStream GetFileDescriptor(int index) => FileDescriptors[index];

        /// <summary>
        /// Handles syscall instructions from the processor. Returns true iff the syscall was handled successfully.
        /// Should not be called directly: only by interpreted syscall instructions
        /// </summary>
        protected virtual bool Syscall()
        {
            string str, str2;

            // requested syscall in R0
            switch (Registers[0].x64)
            {
                // R1 fd, R2 pos, R3 count
                // R0 <- #bytes read
                case (UInt64)SyscallCodes.Read: Registers[0].x64 = FS_Read(Registers[1].x64, Registers[2].x64, Registers[3].x64); return true;
                // R1 fd, R2 pos, R3 count
                case (UInt64)SyscallCodes.Write: return FS_Write(Registers[1].x64, Registers[2].x64, Registers[3].x64);
                
                // R1 path, R2 mode
                // R0 <- fd
                case (UInt64)SyscallCodes.Open:
                    if (!GetString(Registers[1].x64, 2, out str)) return false;
                    Registers[0].x64 = FS_Open(str, Registers[2].x64);
                    return true;
                // R1 fd
                case (UInt64)SyscallCodes.Close: return FS_Close(Registers[1].x64);
                
                // R1 from_path, R2 to_path
                case (UInt64)SyscallCodes.Move:
                    if (!GetString(Registers[1].x64, 2, out str) || !GetString(Registers[2].x64, 2, out str2)) return false;
                    return FS_Move(str, str2);

                // ----------------------------------

                // otherwise syscall not found
                default: return false;
            }
        }

        /// <summary>
        /// Performs a single operation. Returns true if successful
        /// </summary>
        public bool Tick()
        {
            // fail to execute ins if terminated
            if (!Running) return false;

            // if we're sleeping, no-op
            if (Sleep > 0) { --Sleep; return true; }

            UInt64 a = 0, b = 0, c = 0, d = 0; // the potential args (initialized for compiler)

            // fetch the instruction
            if (!GetMemAdv(1, out a)) return false;

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

                case OPCode.UX: if (!GetMemAdv(1, out a)) return false; Registers[a >> 4].Set(a & 3, Registers[a >> 4].Get((a >> 2) & 3)); return true;
                case OPCode.SX: if (!GetMemAdv(1, out a)) return false; Registers[a >> 4].Set(a & 3, SignExtend(Registers[a >> 4].Get((a >> 2) & 3), (a >> 2) & 3)); return true;

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
                    if (!GetMemAdv(1, out a) || !GetAddressAdv(out b)) return false;
                    Registers[a & 15].x64 = b;
                    return true;

                case OPCode.JMP: if (!GetAddressAdv(out a)) return false; Pos = a; return true;

                case OPCode.Ja: if (!GetAddressAdv(out a)) return false; if (Flags.a) Pos = a; return true;
                case OPCode.Jae: if (!GetAddressAdv(out a)) return false; if (Flags.ae) Pos = a; return true;
                case OPCode.Jb: if (!GetAddressAdv(out a)) return false; if (Flags.b) Pos = a; return true;
                case OPCode.Jbe: if (!GetAddressAdv(out a)) return false; if (Flags.be) Pos = a; return true;

                case OPCode.Jg: if (!GetAddressAdv(out a)) return false; if (Flags.g) Pos = a; return true;
                case OPCode.Jge: if (!GetAddressAdv(out a)) return false; if (Flags.ge) Pos = a; return true;
                case OPCode.Jl: if (!GetAddressAdv(out a)) return false; if (Flags.l) Pos = a; return true;
                case OPCode.Jle: if (!GetAddressAdv(out a)) return false; if (Flags.le) Pos = a; return true;

                case OPCode.Jz: if (!GetAddressAdv(out a)) return false; if (Flags.Z) Pos = a; return true;
                case OPCode.Jnz: if (!GetAddressAdv(out a)) return false; if (!Flags.Z) Pos = a; return true;
                case OPCode.Js: if (!GetAddressAdv(out a)) return false; if (Flags.S) Pos = a; return true;
                case OPCode.Jns: if (!GetAddressAdv(out a)) return false; if (!Flags.S) Pos = a; return true;
                case OPCode.Jp: if (!GetAddressAdv(out a)) return false; if (Flags.P) Pos = a; return true;
                case OPCode.Jnp: if (!GetAddressAdv(out a)) return false; if (!Flags.P) Pos = a; return true;
                case OPCode.Jo: if (!GetAddressAdv(out a)) return false; if (Flags.O) Pos = a; return true;
                case OPCode.Jno: if (!GetAddressAdv(out a)) return false; if (!Flags.O) Pos = a; return true;
                case OPCode.Jc: if (!GetAddressAdv(out a)) return false; if (Flags.C) Pos = a; return true;
                case OPCode.Jnc: if (!GetAddressAdv(out a)) return false; if (!Flags.C) Pos = a; return true;

                case OPCode.FADD: return ProcessFADD();
                case OPCode.FSUB: return ProcessFSUB();
                case OPCode.FMUL: return ProcessFMUL();
                case OPCode.FDIV: return ProcessFDIV();
                case OPCode.FMOD: return ProcessFMOD();

                case OPCode.FPOW: return ProcessFPOW();
                case OPCode.FSQRT: return ProcessFSQRT();
                case OPCode.FEXP: return ProcessFEXP();
                case OPCode.FLN: return ProcessFLN();
                case OPCode.FNEG: return ProcessFNEG();
                case OPCode.FABS: return ProcessFABS();
                case OPCode.FCMPZ: return ProcessFCMPZ();

                case OPCode.FSIN: return ProcessFSIN();
                case OPCode.FCOS: return ProcessFCOS();
                case OPCode.FTAN: return ProcessFTAN();

                case OPCode.FSINH: return ProcessFSINH();
                case OPCode.FCOSH: return ProcessFCOSH();
                case OPCode.FTANH: return ProcessFTANH();

                case OPCode.FASIN: return ProcessFASIN();
                case OPCode.FACOS: return ProcessFACOS();
                case OPCode.FATAN: return ProcessFATAN();
                case OPCode.FATAN2: return ProcessFATAN2();

                case OPCode.FLOOR: return ProcessFLOOR();
                case OPCode.CEIL: return ProcessCEIL();
                case OPCode.ROUND: return ProcessROUND();
                case OPCode.TRUNC: return ProcessTRUNC();

                case OPCode.FCMP: return ProcessFSUB(false);

                case OPCode.FTOI: return ProcessFTOI();
                case OPCode.ITOF: return ProcessITOF();

                case OPCode.PUSH:
                    if (!GetMemAdv(1, out a)) return false;
                    switch (a & 1)
                    {
                        case 0: if (!GetMemAdv(Size((a >> 2) & 3), out b)) return false; break;
                        case 1: b = Registers[a >> 4].x64; break;
                    }
                    return Push(Size((a >> 2) & 3), b);
                case OPCode.POP:
                    if (!GetMemAdv(1, out a) || !Pop(Size((a >> 2) & 3), out b)) return false;
                    Registers[a >> 4].Set((a >> 2) & 3, b);
                    return true;
                case OPCode.CALL:
                    if (!GetAddressAdv(out a) || !Push(8, Pos)) return false;
                    Pos = a; return true;
                case OPCode.RET:
                    if (!Pop(8, out a)) return false;
                    Pos = a; return true;

                case OPCode.BSWAP: return ProcessBSWAP();
                case OPCode.BEXTR: return ProcessBEXTR();
                case OPCode.BLSI: return ProcessBLSI();
                case OPCode.BLSMSK: return ProcessBLSMSK();
                case OPCode.BLSR: return ProcessBLSR();
                case OPCode.ANDN: return ProcessANDN();

                case OPCode.GETF: if (!GetMemAdv(1, out a)) return false; Registers[a & 15].x64 = Flags.Flags; return true;
                case OPCode.SETF: if (!GetMemAdv(1, out a)) return false; Flags.Flags = (Registers[a & 15].x64 & PublicFlags) | (Flags.Flags & ~PublicFlags); return true;

                case OPCode.LOOP:
                    if (!GetMemAdv(1, out a) || !GetAddressAdv(out b)) return false;
                    c = Registers[a >> 4].Get((a >> 2) & 3);
                    c = c - Size(a & 3); // since we know we're subtracting a positive value and only comparing to zero, no need to truncate
                    Registers[a >> 4].Set((a >> 2) & 3, c);
                    if (c != 0) Pos = b;
                    return true;

                case OPCode.FX:
                    if (!GetMemAdv(1, out a)) return false;
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

                case OPCode.SLP: if (!FetchIMMRMFormat(out a, out b)) return false; Sleep = b; return true;


                // otherwise, unknown opcode
                default: Fail(ErrorCode.UndefinedBehavior); return false;
            }
        }

        // -------------------
        // -- Memory Access --
        // -------------------

        /// <summary>
        /// Reads a value from memory (fails with OutOfBounds if invalid). if SMF is set, delays 
        /// </summary>
        /// <param name="pos">Address to read</param>
        /// <param name="size">Number of bytes to read</param>
        /// <param name="res">The result</param>
        /// <param name="_abide_slow">if the memory access should abide by SMF. only pass false if it makes sense, otherwise slow should be slow</param>
        public bool GetMem(UInt64 pos, UInt64 size, out UInt64 res, bool _abide_slow = true)
        {
            if (Memory.Read(pos, size, out res))
            {
                if (_abide_slow && Flags.SlowMem) Sleep += size;
                return true;
            }

            Fail(ErrorCode.OutOfBounds); return false;
        }
        /// <summary>
        /// Writes a value to memory (fails with OutOfBounds if invalid)
        /// </summary>
        /// <param name="pos">Address to write</param>
        /// <param name="size">Number of bytes to write</param>
        /// <param name="val">The value to write</param>
        /// <param name="_abide_slow">if the memory access should abide by SMF. only pass false if it makes sense, otherwise slow should be slow</param>
        public bool SetMem(UInt64 pos, UInt64 size, UInt64 val, bool _abide_slow = true)
        {
            if (Memory.Write(pos, size, val))
            {
                if (_abide_slow && Flags.SlowMem) Sleep += size;
                return true;
            }

            Fail(ErrorCode.OutOfBounds); return false;
        }

        /// <summary>
        /// Reads a series of bytes from memory. Returns true iff successful, otherwise fails with OutOfBounds
        /// </summary>
        /// <param name="pos">The position in memory to begin reading</param>
        /// <param name="count">The number of bytes to read</param>
        /// <param name="data">The location to store the data. Should be at least as large as count</param>
        /// <param name="_abide_slow">if the memory access should abide by SMF. only pass false if it makes sense, otherwise slow should be slow</param>
        public bool GetMem(UInt64 pos, UInt64 count, byte[] data, bool _abide_slow = true)
        {
            for (UInt64 i = 0; i < count; ++i)
            {
                if (!GetMem(pos + i, 1, out UInt64 temp, _abide_slow)) return false;
                data[i] = (byte)temp;
            }

            return true;
        }
        /// <summary>
        /// Writes a series of bytes to memory. Returns true iff successful, otherwise fails with OutOfBounds
        /// </summary>
        /// <param name="pos">The position in memory to begin writing</param>
        /// <param name="count">The number of bytes to write</param>
        /// <param name="data">The data to write. Should be at least as large as count</param>
        /// <param name="_abide_slow">if the memory access should abide by SMF. only pass false if it makes sense, otherwise slow should be slow</param>
        public bool SetMem(UInt64 pos, UInt64 count, byte[] data, bool _abide_slow = true)
        {
            for (UInt64 i = 0; i < count; ++i)
                if (!SetMem(pos + i, 1, data[i], _abide_slow)) return false;

            return true;
        }

        /// <summary>
        /// Reads a series of 2-byte words from memory. Returns true iff successful, otherwise fails with OutOfBounds
        /// </summary>
        /// <param name="pos">The position in memory to begin reading</param>
        /// <param name="count">The number of words to read</param>
        /// <param name="data">The location to store the data. Should be at least as large as count</param>
        /// <param name="_abide_slow">if the memory access should abide by SMF. only pass false if it makes sense, otherwise slow should be slow</param>
        public bool GetMem(UInt64 pos, UInt64 count, UInt16[] data, bool _abide_slow = true)
        {
            for (UInt64 i = 0; i < count; ++i)
            {
                if (!GetMem(pos + i, 2, out UInt64 temp, _abide_slow)) return false;
                data[i] = (byte)temp;
            }

            return true;
        }
        /// <summary>
        /// Writes a series of 2-byte words to memory. Returns true iff successful, otherwise fails with OutOfBounds
        /// </summary>
        /// <param name="pos">The position in memory to begin writing</param>
        /// <param name="count">The number of words to write</param>
        /// <param name="data">The data to write. Should be at least as large as count</param>
        /// <param name="_abide_slow">if the memory access should abide by SMF. only pass false if it makes sense, otherwise slow should be slow</param>
        public bool SetMem(UInt64 pos, UInt64 count, UInt16[] data, bool _abide_slow = true)
        {
            for (UInt64 i = 0; i < count; ++i)
                if (!SetMem(pos + i, 2, data[i], _abide_slow)) return false;

            return true;
        }

        /// <summary>
        /// Reads a series of 4-byte words from memory. Returns true iff successful, otherwise fails with OutOfBounds
        /// </summary>
        /// <param name="pos">The position in memory to begin reading</param>
        /// <param name="count">The number of words to read</param>
        /// <param name="data">The location to store the data. Should be at least as large as count</param>
        /// <param name="_abide_slow">if the memory access should abide by SMF. only pass false if it makes sense, otherwise slow should be slow</param>
        public bool GetMem(UInt64 pos, UInt64 count, UInt32[] data, bool _abide_slow = true)
        {
            for (UInt64 i = 0; i < count; ++i)
            {
                if (!GetMem(pos + i, 4, out UInt64 temp, _abide_slow)) return false;
                data[i] = (byte)temp;
            }

            return true;
        }
        /// <summary>
        /// Writes a series of 4-byte words to memory. Returns true iff successful, otherwise fails with OutOfBounds
        /// </summary>
        /// <param name="pos">The position in memory to begin writing</param>
        /// <param name="count">The number of words to write</param>
        /// <param name="data">The data to write. Should be at least as large as count</param>
        /// <param name="_abide_slow">if the memory access should abide by SMF. only pass false if it makes sense, otherwise slow should be slow</param>
        public bool SetMem(UInt64 pos, UInt64 count, UInt32[] data, bool _abide_slow = true)
        {
            for (UInt64 i = 0; i < count; ++i)
                if (!SetMem(pos + i, 4, data[i], _abide_slow)) return false;

            return true;
        }

        /// <summary>
        /// Reads a series of 8-byte words from memory. Returns true iff successful, otherwise fails with OutOfBounds
        /// </summary>
        /// <param name="pos">The position in memory to begin reading</param>
        /// <param name="count">The number of words to read</param>
        /// <param name="data">The location to store the data. Should be at least as large as count</param>
        /// <param name="_abide_slow">if the memory access should abide by SMF. only pass false if it makes sense, otherwise slow should be slow</param>
        public bool GetMem(UInt64 pos, UInt64 count, UInt64[] data, bool _abide_slow = true)
        {
            for (UInt64 i = 0; i < count; ++i)
            {
                if (!GetMem(pos + i, 8, out UInt64 temp, _abide_slow)) return false;
                data[i] = (byte)temp;
            }

            return true;
        }
        /// <summary>
        /// Writes a series of 8-byte words to memory. Returns true iff successful, otherwise fails with OutOfBounds
        /// </summary>
        /// <param name="pos">The position in memory to begin writing</param>
        /// <param name="count">The number of words to write</param>
        /// <param name="data">The data to write. Should be at least as large as count</param>
        /// <param name="_abide_slow">if the memory access should abide by SMF. only pass false if it makes sense, otherwise slow should be slow</param>
        public bool SetMem(UInt64 pos, UInt64 count, UInt64[] data, bool _abide_slow = true)
        {
            for (UInt64 i = 0; i < count; ++i)
                if (!SetMem(pos + i, 8, data[i], _abide_slow)) return false;

            return true;
        }

        /// <summary>
        /// Reads a null-terminated string from memory. Returns true if successful, otherwise fails with OutOfBounds and returns false
        /// </summary>
        /// <param name="pos">the address in memory of the first character in the string</param>
        /// <param name="charsize">the size of each character in bytes</param>
        /// <param name="str">the resulting string</param>
        /// <param name="_abide_slow">if the memory access should abide by SMF. only pass false if it makes sense, otherwise slow should be slow</param>
        public bool GetString(UInt64 pos, UInt64 charsize, out string str, bool _abide_slow = true)
        {
            // use builder for efficiency
            StringBuilder b = new StringBuilder();
            UInt64 val;

            // read the string from memory
            for (UInt64 i = 0; ; ++i)
            {
                // get a char from memory
                if (!GetMem(pos + i * charsize, charsize, out val, _abide_slow)) { str = null; return false; }

                // add if non-null, otherwise done
                if (val != 0) b.Append((char)val);
                else break;
            }

            // return the resulting string
            str = b.ToString();
            return true;
        }
        /// <summary>
        /// Writes a null-terminated string to memory. Returns true if successful, otherwise fails with OutOfBounds and returns false
        /// </summary>
        /// <param name="pos">the address in memory of the first character to write</param>
        /// <param name="charsize">the size of each character in bytes</param>
        /// <param name="str">the string to write</param>
        /// <param name="_abide_slow">if the memory access should abide by SMF. only pass false if it makes sense, otherwise slow should be slow</param>
        public bool SetString(UInt64 pos, UInt64 charsize, string str, bool _abide_slow = true)
        {
            // read the string from memory
            for (UInt64 i = 0; i < (UInt64)str.Length; ++i)
                if (!SetMem(pos + i * charsize, charsize, str[(int)i], _abide_slow)) return false;

            // write the null terminator
            if (!SetMem(pos + (UInt64)str.Length * charsize, charsize, 0, _abide_slow)) return false;

            return true;
        }

        /// <summary>
        /// Gets a value at and advances the execution pointer (fails with OutOfBounds if invalid)
        /// </summary>
        /// <param name="size">Number of bytes to read</param>
        /// <param name="res">The result</param>
        private bool GetMemAdv(UInt64 size, out UInt64 res)
        {
            bool r = GetMem(Pos, size, out res);
            Pos += size;

            return r;
        }
        // [1: literal][3: m1][1: -m2][3: m2]   ([4: r1][4: r2])   ([64: imm])
        /// <summary>
        /// Gets an address and advances the execution pointer
        /// </summary>
        /// <param name="res">resulting address</param>
        private bool GetAddressAdv(out UInt64 res)
        {
            res = 0; // initialize out param

            UInt64 mults, regs = 0, imm = 0; // the mult codes, regs, and literal

            // parse the address
            if (!GetMemAdv(1, out mults) || (mults & 0x77) != 0 && !GetMemAdv(1, out regs) || (mults & 0x80) != 0 && !GetMemAdv(8, out imm)) return false;

            // compute the result into res
            res = Mult((mults >> 4) & 7) * Registers[regs >> 4].x64 + Mult(mults & 7, mults & 8) * Registers[regs & 15].x64 + imm;

            // got an address
            return true;
        }

        /// <summary>
        /// Pushes a value onto the stack
        /// </summary>
        /// <param name="size">the size of the value (in bytes)</param>
        /// <param name="val">the value to push</param>
        public bool Push(UInt64 size, UInt64 val)
        {
            Registers[15].x64 -= size;
            return SetMem(Registers[15].x64, size, val);
        }
        /// <summary>
        /// Pops a value from the stack
        /// </summary>
        /// <param name="size">the size of the value (in bytes)</param>
        /// <param name="val">the resulting value</param>
        public bool Pop(UInt64 size, out UInt64 val)
        {
            if (!GetMem(Registers[15].x64, size, out val)) return false;
            Registers[15].x64 += size;
            return true;
        }

        // -------------
        // -- File IO --
        // -------------

        /// <summary>
        /// Finds the first available file descriptor. Returns the index of the available spot, or UInt64.MaxValue upon failure
        /// </summary>
        private UInt64 FindOpenFileDescriptor()
        {
            for (int i = 0; i < FileDescriptors.Length; ++i)
                if (FileDescriptors[i] == null) return (UInt64)i;

            return UInt64.MaxValue;
        }

        /// <summary>
        /// Opens a file in the specified mode. Returns the file descriptor created if successful, or UInt64.MaxValue upon failure
        /// </summary>
        /// <param name="path">the path of the file to open</param>
        /// <param name="mode">the mode to open the file in (see System.IO.FileAccess)"/></param>
        public UInt64 FS_Open(string path, UInt64 mode)
        {
            UInt64 index = FindOpenFileDescriptor();
            if (index == UInt64.MaxValue) return UInt64.MaxValue;

            FileStream f; // resulting stream

            // attempt to open the file
            try { f = new FileStream(path, FileMode.OpenOrCreate, (FileAccess)mode); }
            catch (Exception) { return UInt64.MaxValue; }
            
            // store in file descriptors
            FileDescriptors[index] = f;
            return index;
        }
        /// <summary>
        /// Closes a file. Returns true if successful, otherwise failes with IOFailure and returns false
        /// </summary>
        /// <param name="fd">The file descriptor to close</param>
        public bool FS_Close(UInt64 fd)
        {
            // attempt to close the file
            try { FileDescriptors[fd].Close(); FileDescriptors[fd] = null; }
            catch (Exception) { Fail(ErrorCode.IOFailure); return false; }

            // file closed successfully
            return true;
        }

        /// <summary>
        /// Attempts to read a series of bytes from the file and store them in memory
        /// </summary>
        /// <param name="fd">the file descriptor to use</param>
        /// <param name="pos">the position in memory to store the read data</param>
        /// <param name="count">the number of bytes to read</param>
        public UInt64 FS_Read(UInt64 fd, UInt64 pos, UInt64 count)
        {
            // attempt to read from the file into memory
            try { return (UInt64)FileDescriptors[fd].Read(Memory, (int)pos, (int)count); }
            catch (Exception) { Fail(ErrorCode.IOFailure); return 0; }
        }
        /// <summary>
        /// Attempts to write a series of bytes from memory to the file
        /// </summary>
        /// <param name="fd">the file descriptor to use</param>
        /// <param name="pos">the position in memory of the data to write</param>
        /// <param name="count">the number of bytes to read</param>
        public bool FS_Write(UInt64 fd, UInt64 pos, UInt64 count)
        {
            // attempt to read from memory to the file
            try { FileDescriptors[fd].Write(Memory, (int)pos, (int)count); return true; }
            catch (Exception) { Fail(ErrorCode.IOFailure); return false; }
        }

        /// <summary>
        /// Moves a file. Returns true iff successful
        /// </summary>
        /// <param name="from">the file to move</param>
        /// <param name="to">the destination path</param>
        public bool FS_Move(string from, string to)
        {
            // attempt the move operation
            try { File.Move(from, to); return true; }
            catch (Exception) { return false; }
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
        private enum PatchError
        {
            None, Unevaluated, Error
        }

        public struct AssembleResult
        {
            /// <summary>
            /// The error that occurred (or None for success)
            /// </summary>
            public AssembleError Error;
            /// <summary>
            /// What caused the error
            /// </summary>
            public string ErrorMsg;

            public AssembleResult(AssembleError error, string errorMsg)
            {
                Error = error;
                ErrorMsg = errorMsg;
            }
        }
        public struct LinkResult
        {
            /// <summary>
            /// The error that occurred (or None for success)
            /// </summary>
            public LinkError Error;
            /// <summary>
            /// What caused the error
            /// </summary>
            public string ErrorMsg;

            public LinkResult(LinkError error, string errorMsg)
            {
                Error = error;
                ErrorMsg = errorMsg;
            }
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

        /// <summary>
        /// Represents an assembled object file used to create an executable
        /// </summary>
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

            //public string rawline;
            public string[] label_defs; // must be array for ref params
            public string op;
            public UInt64 sizecode;
            public string[] args;       // must be array for ref params

            public string last_static_label;
            public AssembleResult res;

            public UInt64 time;

            // ------------------------
            // -- Assembly Functions --
            // ------------------------

            public const UInt64 EmissionMaxMultiplier = 1000000;
            private const char EmissionMultiplierChar = ':';

            public bool SplitLine(string rawline)
            {
                // (label: label: ...) (op(:size) (arg, arg, ...))

                int pos = 0, end; // position in line parsing
                int quote;        // index of openning quote in args

                List<string> tokens = new List<string>();
                StringBuilder b = new StringBuilder();

                // parse labels
                for (; pos < rawline.Length; pos = end)
                {
                    // skip leading white space
                    for (; pos < rawline.Length && char.IsWhiteSpace(rawline[pos]); ++pos) ;
                    // get a white space-delimited token
                    for (end = pos; end < rawline.Length && !char.IsWhiteSpace(rawline[end]); ++end) ;

                    // if it's a label, add to tokens
                    if (pos != end && rawline[end - 1] == ':') tokens.Add(rawline.Substring(pos, end - pos - 1));
                    // otherwise we're done with labels
                    else break; // break ensures we also keep pos pointing to start of next section
                }
                label_defs = tokens.ToArray(); // dump tokens as label defs
                tokens.Clear(); // empty tokens for reuse

                // parse op
                if (pos < rawline.Length)
                {
                    // get up till size separator or white space
                    for (end = pos; end < rawline.Length && rawline[end] != ':' && !char.IsWhiteSpace(rawline[end]); ++end) ;

                    // make sure we got a well-formed op
                    if (pos == end) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Operation size specification encountered without an operation"); return false; }

                    // save this as op
                    op = rawline.Substring(pos, end - pos);

                    // if we got a size specification
                    if (end < rawline.Length && rawline[end] == ':')
                    {
                        pos = end + 1; // position to beginning of size specification

                        // if starting parenthetical section
                        if (pos < rawline.Length && rawline[pos] == '(')
                        {
                            int depth = 1; // parenthetical depth

                            // get till depth of zero
                            for (end = pos + 1; end < rawline.Length && depth > 0; ++end)
                            {
                                if (rawline[end] == '(') ++depth;
                                else if (rawline[end] == ')') --depth;
                            }

                            if (depth != 0) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Mismatched parenthesis encountered in operation size specification"); return false; }
                        }
                        // ohterwise standard imm
                        else
                        {
                            // take all legal chars
                            for (end = pos; end < rawline.Length && (rawline[end] == '_' || char.IsLetterOrDigit(rawline[end])); ++end) ;
                        }

                        // make sure we didn't end on non-white space
                        if (end < rawline.Length && !char.IsWhiteSpace(rawline[end])) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Size parameter must be separated from arguments by white space"); return false; }

                        // parse the read size code
                        if (!TryParseSizecode(rawline.Substring(pos, end - pos).RemoveWhiteSpace(), out sizecode))
                        { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Failed to parse operation size specification\n-> {res.ErrorMsg}"); return false; }
                    }
                    // otherwise use default size (64-bit)
                    else sizecode = 3;

                    pos = end; // pass parsed section before next section
                }
                // otherwise there is no op
                else op = string.Empty;

                // parse the rest of the line as comma-separated tokens
                for (; pos < rawline.Length; ++pos)
                {
                    // skip leading white space
                    for (; pos < rawline.Length && char.IsWhiteSpace(rawline[pos]); ++pos) ;
                    // when pos reaches end of token, we're done parsing
                    if (pos >= rawline.Length) break;

                    b.Clear(); // clear the string builder

                    // find the next terminator (comma-separated)
                    for (quote = -1; pos < rawline.Length && (rawline[pos] != ',' || quote >= 0); ++pos)
                    {
                        if (rawline[pos] == '"' || rawline[pos] == '\'')
                            quote = quote < 0 ? pos : (rawline[pos] == rawline[quote] ? -1 : quote);

                        // omit white space unless in a quote
                        if (quote >= 0 || !char.IsWhiteSpace(rawline[pos])) b.Append(rawline[pos]);
                    }

                    // make sure we closed any quotations
                    if (quote >= 0) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Unmatched quotation encountered in argument list"); return false; }

                    // make sure arg isn't empty
                    if (b.Length == 0) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Empty operation argument encountered"); return false; }

                    // add this token
                    tokens.Add(b.ToString());
                }
                // output tokens to assemble args
                args = tokens.ToArray();

                // successfully parsed line
                return true;
            }

            public static bool IsValidLabel(string token)
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
            public bool MutateLabel(ref string label)
            {
                // if defining a local label
                if (label[0] == '.')
                {
                    string sub = label.Substring(1); // local symbol name

                    // local name can't be empty
                    if (!IsValidLabel(sub)) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Local label name must be legal"); return false; }
                    // can't make a local label before any non-local ones exist
                    if (last_static_label == null) { res = new AssembleResult(AssembleError.InvalidLabel, $"line {line}: Cannot define a local label before the first static label"); return false; }
                    
                    // mutate the label
                    label = $"__local_{time:x16}_{last_static_label}_{sub}";
                }

                return true;
            }

            public void AppendVal(UInt64 size, UInt64 val)
            {
                // write the value (little-endian)
                for (ushort i = 0; i < size; ++i)
                    file.Data.Add((byte)(val >> (8 * i)));
            }
            public bool TryAppendExpr(UInt64 size, Expr expr, int type = 3)
            {
                string err = null; // evaluation error

                // create the hole data
                HoleData data = new HoleData() { Address = (UInt64)file.Data.Count, Size = size, Line = line, Expr = expr };
                // write a dummy (all 1's for easy manual identification)
                AppendVal(size, 0xffffffffffffffff);

                // try to patch it
                switch (TryPatchHole(file.Data, file.Symbols, data, ref err))
                {
                    case PatchError.None: break;
                    case PatchError.Unevaluated: file.Holes.Add(data); break;
                    case PatchError.Error: res = new AssembleResult(AssembleError.ArgError, $"line {line}: Error encountered while patching expression\n-> {err}"); return false;

                    default: throw new ArgumentException("Unknown patch error encountered");
                }

                return true;
            }
            public bool TryAppendAddress(UInt64 a, UInt64 b, Expr hole)
            {
                // [1: literal][3: m1][1: -m2][3: m2]   ([4: r1][4: r2])   ([64: imm])
                AppendVal(1, a);
                if ((a & 0x77) != 0) AppendVal(1, b);
                if ((a & 0x80) != 0) { if (!TryAppendExpr(8, hole)) { res = new AssembleResult(AssembleError.ArgError, $"line {line}: Failed to append address base\n-> {res.ErrorMsg}"); return false; } }

                return true;
            }

            public static readonly Dictionary<Expr.OPs, int> Precedence = new Dictionary<Expr.OPs, int>()
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
            public static readonly List<char> UnaryOps = new List<char>() { '+', '-', '~', '!', '*', '/' };

            public static bool TryGetOp(string token, int pos, out Expr.OPs op, out int oplen)
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
            public bool TryParseImm(string token, out Expr hole)
            {
                hole = null; // initially-nulled result

                Expr temp; // temporary for node creation

                int pos = 0, end; // position in token
                int depth;        // parenthesis depth

                bool numeric; // flags for enabling exponent notation for floating-point
                bool exp;

                bool binPair = false;          // marker if tree contains complete binary pairs (i.e. N+1 values and N binary ops)
                int unpaired_conditionals = 0; // number of unpaired conditional ops

                Expr.OPs op = Expr.OPs.None; // extracted binary op (initialized so compiler doesn't complain)
                int oplen = 0;               // length of operator found (in characters)

                string err = null; // error location for hole evaluation

                Stack<char> unaryOps = new Stack<char>(8); // holds unary ops for processing
                Stack<Expr> stack = new Stack<Expr>();     // the stack used to manage operator precedence rules

                // top of stack shall be refered to as current

                stack.Push(null); // stack will always have a null at its base (simplifies code slightly)

                if (token.Length == 0) { res = new AssembleResult(AssembleError.InvalidLabel, $"line {line}: Empty expression encountered"); return false; }

                while (pos < token.Length)
                {
                    // -- read val(op) -- //

                    // consume unary ops
                    for (; pos < token.Length && UnaryOps.Contains(token[pos]); ++pos) unaryOps.Push(token[pos]);

                    depth = 0; // initial depth of 0

                    numeric = pos < token.Length && char.IsDigit(token[pos]); // flag if this is a numeric literal
                    exp = false; // no exponent yet

                    // find next binary op
                    for (end = pos; end < token.Length && (depth > 0 || !TryGetOp(token, end, out op, out oplen) || (numeric && exp)); ++end)
                    {
                        if (token[end] == '(') ++depth;
                        else if (token[end] == ')') --depth;
                        else if (numeric && token[end] == 'e' || token[end] == 'E') exp = true; // e or E begins exponent
                        else if (numeric && token[end] == '+' || token[end] == '-' || char.IsDigit(token[end])) exp = false; // + or - or a digit ends exponent safety net

                        // can't ever have negative depth
                        if (depth < 0) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Mismatched parenthesis \"{token}\""); return false; }
                    }
                    // if depth isn't back to 0, there was a parens mismatch
                    if (depth != 0) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Mismatched parenthesis \"{token}\""); return false; }
                    // if pos == end we'll have an empty token
                    if (pos == end) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Empty token encountered in expression \"{token}\""); return false; }

                    // -- process value -- //
                    {
                        // -- convert value to an expression tree --

                        // if sub-expression
                        if (token[pos] == '(')
                        {
                            // parse it into temp
                            if (!TryParseImm(token.Substring(pos + 1, end - pos - 2), out temp)) return false;
                        }
                        // otherwise is value
                        else
                        {
                            // get the value to insert
                            string val = token.Substring(pos, end - pos);

                            // mutate it
                            if (!MutateLabel(ref val)) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Failed to parse imm \"{token}\"\n-> {res.ErrorMsg}"); return false; }

                            // create the hole for it
                            temp = new Expr() { Value = val };

                            // it either needs to be evaluatable or a valid label name
                            if (!temp.Evaluate(file.Symbols, out UInt64 _res, out bool floating, ref err) && !IsValidLabel(val))
                            { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Failed to resolve symbol as a valid imm \"{val}\"\n-> {err}"); return false; }
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
                            if (stack.Peek() == null) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Expression contained a ternary conditional pair without a corresponding condition \"{token}\""); return false; }
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
                if (!binPair) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Expression contained a mismatched binary op: \"{token}\""); return false; }

                // make sure all conditionals were matched
                if (unpaired_conditionals != 0) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Expression contained {unpaired_conditionals} incomplete ternary {(unpaired_conditionals == 1 ? "conditional" : "conditionals")}"); return false; }

                return true;
            }
            public bool TryParseInstantImm(string token, out UInt64 val, out bool floating)
            {
                string err = null; // error location for evaluation

                if (!TryParseImm(token, out Expr hole)) { val = 0; floating = false; return false; }
                if (!hole.Evaluate(file.Symbols, out val, out floating, ref err)) { res = new AssembleResult(AssembleError.ArgError, $"line {line}: Failed to parse instant imm \"{token}\"\n-> {err}"); return false; }

                return true;
            }

            public bool TryParseRegister(string token, out UInt64 val)
            {
                val = 0;

                // registers prefaced with $
                if (token.Length < 2 || token[0] != '$') { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Invalid register format encountered \"{token}\""); return false; }

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
                    if (depth != 0) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Mismatched parenthesis in register expression \"{token.Substring(1, end - 1)}\""); return false; }
                }
                // otherwise normal symbol
                else
                {
                    // take all legal chars
                    for (end = 1; end < token.Length && (char.IsLetterOrDigit(token[end]) || token[end] == '_'); ++end) ;
                }

                // make sure we consumed the entire string
                if (end != token.Length) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Expressions used as register ids must be parenthesized"); return false; }

                // register index must be instant imm
                if (!TryParseInstantImm(token.Substring(1, end - 1), out val, out bool floating)) { res = new AssembleResult(AssembleError.ArgError, $"line {line}: Failed to parse register index \"{token.Substring(1, end - 1)}\"\n-> {res.ErrorMsg}"); return false; }
                
                // ensure not floating and in proper range
                if (floating) { res = new AssembleResult(AssembleError.ArgError, $"line {line}: Attempt to use floating point value to specify register \"{token}\""); return false; }
                if (val >= 16) { res = new AssembleResult(AssembleError.ArgError, $"line {line}: Register index out of range \"{token}\" (evaluated to {val})"); return false; }

                return true;
            }
            public bool TryParseSizecode(string token, out UInt64 val)
            {
                // size code must be instant imm
                if (!TryParseInstantImm(token, out val, out bool floating)) { res = new AssembleResult(AssembleError.ArgError, $"line {line}: Failed to parse size code \"{token}\"\n-> {res.ErrorMsg}"); return false; }

                // ensure not floating
                if (floating) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Attempt to use floating point value to specify register size \"{token}\""); return false; }

                // convert to size code
                switch (val)
                {
                    case 8: val = 0; return true;
                    case 16: val = 1; return true;
                    case 32: val = 2; return true;
                    case 64: val = 3; return true;

                    default: res = new AssembleResult(AssembleError.ArgError, $"line {line}: Invalid register size: {val}"); return false;
                }
            }
            public bool TryParseMultcode(string token, out UInt64 val)
            {
                // mult code must be instant imm
                if (!TryParseInstantImm(token, out val, out bool floating)) { res = new AssembleResult(AssembleError.ArgError, $"line {line}: Failed to parse mult code \"{token}\"\n-> {res.ErrorMsg}"); return false; }

                // ensure not floating
                if (floating) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Attempt to use floating point value to specify size multiplier \"{token}\""); return false; }

                // convert to mult code
                switch (val)
                {
                    case 0: val = 0; return true;
                    case 1: val = 1; return true;
                    case 2: val = 2; return true;
                    case 4: val = 3; return true;
                    case 8: val = 4; return true;
                    case 16: val = 5; return true;
                    case 32: val = 6; return true;
                    case 64: val = 7; return true;

                    default: res = new AssembleResult(AssembleError.ArgError, $"line {line}: Invalid size multiplier: {val}"); return false;
                }
            }

            public bool TryParseAddressReg(string label, ref Expr hole, out UInt64 m, out bool neg)
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
                    for (int i = 2; i < list.Count;)
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

                            default: res = new AssembleResult(AssembleError.FormatError, $"line {line}: Register may not be connected by {list[i].OP}"); return false;
                        }
                    }

                    // -- finally done with all the algebra -- //

                    // extract mult code fragment
                    if (!(list[1].Left == list[0] ? list[1].Right : list[1].Left).Evaluate(file.Symbols, out UInt64 val, out bool floating, ref err))
                    { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Failed to evaluate register multiplier as an instant imm\n-> {err}"); return false; }
                    // make sure it's not floating-point
                    if (floating) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Register multiplier may not be floating-point"); return false; }

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

                    default: res = new AssembleResult(AssembleError.ArgError, $"line {line}: Invalid register multiplier encountered ({m.MakeSigned()})"); return false;
                }

                // register successfully parsed
                return true;
            }
            public bool TryParseAddress(string token, out UInt64 a, out UInt64 b, out Expr hole)
            {
                a = b = 0;
                hole = new Expr();

                // must be of [*] format
                if (token.Length < 3 || token[0] != '[' || token[token.Length - 1] != ']') { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Invalid address format encountered \"{token}\""); return false; }

                int pos, end; // parsing positions

                UInt64 temp = 0; // parsing temporaries
                bool btemp = false;

                int reg_count = 0; // number of registers parsed
                UInt64 r1 = 0, m1 = 0, r2 = 0, m2 = 0; // final register info
                bool n1 = false, n2 = false;

                string preface = $"__reg_{time:x16}"; // preface used for registers
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
                        if (depth != 0) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Mismatched parenthesis in register expression \"{token.Substring(pos, end - pos)}\""); return false; }
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
                    if (!TryParseRegister(token.Substring(pos, end - pos), out temp)) return false;

                    // put it in a register slot
                    if (!regs.Contains(temp)) regs.Add(temp);

                    // modify the register label in the expression to be a legal symbol name
                    token = $"{token.Substring(0, pos)}{preface}_{temp}{token.Substring(end)}";
                }

                // turn into an expression
                if (!TryParseImm(token.Substring(1, token.Length - 2), out hole)) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Failed to parse address expression\n-> {res.ErrorMsg}"); return false; }

                // look through each register found
                foreach (UInt64 reg in regs)
                {
                    // get the register data
                    if (!TryParseAddressReg($"{preface}_{reg}", ref hole, out temp, out btemp)) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Failed to extract register data\n-> {res.ErrorMsg}"); return false; }

                    // if the multiplier was nonzero, the register is really being used
                    if (temp != 0)
                    {
                        // put it into an available r slot
                        if (reg_count == 0) { r1 = reg; m1 = temp; n1 = btemp; }
                        else if (reg_count == 1) { r2 = reg; m2 = temp; n2 = btemp; }
                        else { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Can't use more than 2 registers to specify an address"); return false; }

                        ++reg_count; // mark this slot as filled
                    }
                }

                // make sure only one register is negative
                if (n1 && n2) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Only one register may be negative in an address expression"); return false; }
                // if the negative register is r1, swap with r2
                if (n1)
                {
                    Utility.Swap(ref r1, ref r2);
                    Utility.Swap(ref m1, ref m2);
                    Utility.Swap(ref n1, ref n2);
                }

                // if we can evaluate the hole to zero, there is no hole (null it)
                if (hole.Evaluate(file.Symbols, out temp, out btemp, ref err) && temp == 0) hole = null;

                // -- apply final touches -- //

                // [1: literal][3: m1][1: -m2][3: m2]   [4: r1][4: r2]   ([64: imm])
                a = (hole != null ? 128 : 0ul) | (m1 << 4) | (n2 ? 8 : 0ul) | m2;
                b = (r1 << 4) | r2;

                // address successfully parsed
                return true;
            }

            // -- op formats -- //

            public bool TryProcessBinaryOp(OPCode op, int _b_sizecode = -1, UInt64 sizemask = 15)
            {
                UInt64 a, b, c; // parsing temporaries
                Expr hole1, hole2;

                if (args.Length != 2) { res = new AssembleResult(AssembleError.ArgCount, $"line {line}: {op} expected 2 args"); return false; }
                if ((Size(sizecode) & sizemask) == 0) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: {op} does not support the specified size code"); return false; }

                AppendVal(1, (UInt64)op);

                UInt64 b_sizecode = _b_sizecode == -1 ? sizecode : (UInt64)_b_sizecode;

                // reg, *
                if (args[0][0] == '$')
                {
                    if (!TryParseRegister(args[0], out a)) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Failed to parse \"{args[0]}\" as a register\n-> {res.ErrorMsg}"); return false; }

                    // reg, reg
                    if (args[1][0] == '$')
                    {
                        if (!TryParseRegister(args[1], out b)) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Failed to parse \"{args[1]}\" as a register\n-> {res.ErrorMsg}"); return false; }

                        AppendVal(1, (a << 4) | (sizecode << 2) | 2);
                        AppendVal(1, b);
                    }
                    // reg, mem
                    else if (args[1][0] == '[')
                    {
                        if (!TryParseAddress(args[1], out b, out c, out hole1)) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Failed to parse \"{args[1]}\" as an address\n-> {res.ErrorMsg}"); return false; }

                        AppendVal(1, (a << 4) | (sizecode << 2) | 1);
                        if (!TryAppendAddress(b, c, hole1)) { res = new AssembleResult(AssembleError.ArgError, $"line {line}: Failed to append value\n-> {res.ErrorMsg}"); return false; }
                    }
                    // reg, imm
                    else
                    {
                        if (!TryParseImm(args[1], out hole1)) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Failed to parse \"{args[1]}\" as an imm\n-> {res.ErrorMsg}"); return false; }

                        AppendVal(1, (a << 4) | (sizecode << 2) | 0);
                        if (!TryAppendExpr(Size(b_sizecode), hole1)) { res = new AssembleResult(AssembleError.ArgError, $"line {line}: Failed to append value\n-> {res.ErrorMsg}"); return false; }
                    }
                }
                // mem, *
                else if (args[0][0] == '[')
                {
                    if (!TryParseAddress(args[0], out a, out b, out hole1)) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Failed to parse \"{args[0]}\" as an address\n-> {res.ErrorMsg}"); return false; }

                    // mem, reg
                    if (args[1][0] == '$')
                    {
                        if (!TryParseRegister(args[1], out c)) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Failed to parse \"{args[1]}\" as a register\n-> {res.ErrorMsg}"); return false; }

                        AppendVal(1, (sizecode << 2) | 2);
                        AppendVal(1, 16 | c);
                        if (!TryAppendAddress(a, b, hole1)) { res = new AssembleResult(AssembleError.ArgError, $"line {line}: Failed to append value\n-> {res.ErrorMsg}"); return false; };
                    }
                    // mem, mem
                    else if (args[1][0] == '[') { res = new AssembleResult(AssembleError.FormatError, $"line {line}: {op} does not support memory-to-memory"); return false; }
                    // mem, imm
                    else
                    {
                        if (!TryParseImm(args[1], out hole2)) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Failed to parse \"{args[1]}\" as an imm\n-> {res.ErrorMsg}"); return false; }

                        AppendVal(1, (sizecode << 2) | 3);
                        if (!TryAppendExpr(Size(b_sizecode), hole2)) { res = new AssembleResult(AssembleError.ArgError, $"line {line}: Failed to append value\n-> {res.ErrorMsg}"); return false; }
                        if (!TryAppendAddress(a, b, hole1)) { res = new AssembleResult(AssembleError.ArgError, $"line {line}: Failed to append value\n-> {res.ErrorMsg}"); return false; }
                    }
                }
                else { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Destination must be register or memory"); return false; }

                return true;
            }
            public bool TryProcessUnaryOp(OPCode op, UInt64 sizemask = 15)
            {
                UInt64 a, b;
                Expr hole;

                if (args.Length != 1) { res = new AssembleResult(AssembleError.ArgCount, $"line {line}: {op} expected 1 arg"); return false; }
                if ((Size(sizecode) & sizemask) == 0) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: {op} does not support the specified size code"); return false; }

                AppendVal(1, (UInt64)op);

                // reg
                if (args[0][0] == '$')
                {
                    if (!TryParseRegister(args[0], out a)) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Failed to parse \"{args[0]}\" as a register\n-> {res.ErrorMsg}"); return false; }

                    AppendVal(1, (a << 4) | (sizecode << 2) | 0);
                }
                // mem
                else if (args[0][0] == '[')
                {
                    if (!TryParseAddress(args[0], out a, out b, out hole)) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Failed to parse \"{args[0]}\" as an address\n-> {res.ErrorMsg}"); return false; }

                    AppendVal(1, (sizecode << 2) | 1);
                    if (!TryAppendAddress(a, b, hole)) { res = new AssembleResult(AssembleError.ArgError, $"line {line}: Failed to append value\n-> {res.ErrorMsg}"); return false; }
                }
                // imm
                else { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Destination must be register or memory"); return false; }

                return true;
            }
            public bool TryProcessJump(OPCode op)
            {
                if (args.Length != 1) { res = new AssembleResult(AssembleError.ArgCount, $"line {line}: {op} expected 1 arg"); return false; }

                if (!TryParseAddress(args[0], out UInt64 a, out UInt64 b, out Expr hole)) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Jump expected address as first arg\n-> {res.ErrorMsg}"); return false; }

                AppendVal(1, (UInt64)op);
                if (!TryAppendAddress(a, b, hole)) { res = new AssembleResult(AssembleError.ArgError, $"line {line}: Failed to append value\n-> {res.ErrorMsg}"); return false; }

                return true;
            }
            public bool TryProcessEmission()
            {
                if (args.Length == 0) { res = new AssembleResult(AssembleError.ArgCount, $"line {line}: Emission expected at least one value"); return false; }

                Expr hole = new Expr(); // initially empty hole (to allow for buffer shorthand e.x. "emit x32")
                UInt64 mult;
                bool floating;

                for (int i = 0; i < args.Length; ++i)
                {
                    if (args[i].Length == 0) { res = new AssembleResult(AssembleError.ArgError, $"line {line}: Emission encountered empty argument"); return false; }

                    // if a multiplier
                    if (args[i][0] == EmissionMultiplierChar)
                    {
                        // get the multiplier and ensure is valid
                        if (!TryParseInstantImm(args[i].Substring(1), out mult, out floating)) return false;
                        if (floating) { res = new AssembleResult(AssembleError.ArgError, $"line {line}: Emission multiplier cannot be floating point"); return false; }
                        if (mult > EmissionMaxMultiplier) { res = new AssembleResult(AssembleError.ArgError, $"line {line}: Emission multiplier cannot exceed {EmissionMaxMultiplier}. Got {mult}"); return false; }
                        if (mult == 0) { res = new AssembleResult(AssembleError.ArgError, $"line {line}: Emission multiplier cannot be zero"); return false; }

                        // account for first written value
                        if (i > 0 && args[i - 1][0] != EmissionMultiplierChar) --mult;

                        for (UInt64 j = 0; j < mult; ++j)
                            if (!TryAppendExpr(Size(sizecode), hole)) { res = new AssembleResult(AssembleError.ArgError, $"line {line}: Failed to append value\n-> {res.ErrorMsg}"); return false; }
                    }
                    // if a string
                    else if (args[i][0] == '"' || args[i][0] == '\'')
                    {
                        if (args[i][0] != args[i][args[i].Length - 1]) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: String literal must be enclosed in single or double quotes"); return false; }

                        // dump the contents into memory
                        for (int j = 1; j < args[i].Length - 1; ++j)
                        {
                            // make sure there's no string splicing
                            if (args[i][j] == args[i][0]) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: String emission prematurely reached a terminating quote"); return false; }

                            AppendVal(Size(sizecode), args[i][j]);
                        }
                    }
                    // otherwise is a value
                    else
                    {
                        // get the value
                        if (!TryParseImm(args[i], out hole)) return false;

                        // make one of them
                        if (!TryAppendExpr(Size(sizecode), hole)) { res = new AssembleResult(AssembleError.ArgError, $"line {line}: Failed to append value\n-> {res.ErrorMsg}"); return false; }
                    }
                }

                return true;
            }
            public bool TryProcessIMMRM(OPCode op)
            {
                UInt64 a, b;
                Expr hole;

                if (args.Length != 1) { res = new AssembleResult(AssembleError.ArgCount, $"line {line}: {op} expected 1 arg"); return false; }

                AppendVal(1, (UInt64)op);

                // reg
                if (args[0][0] == '$')
                {
                    if (!TryParseRegister(args[0], out a)) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Failed to parse \"{args[0]}\" as a register\n-> {res.ErrorMsg}"); return false; }

                    AppendVal(1, (a << 4) | (sizecode << 2) | 1);
                }
                // mem
                else if (args[0][0] == '[')
                {
                    if (!TryParseAddress(args[0], out a, out b, out hole)) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Failed to parse \"{args[0]}\" as an address\n-> {res.ErrorMsg}"); return false; }

                    AppendVal(1, (sizecode << 2) | 2);
                    if (!TryAppendAddress(a, b, hole)) { res = new AssembleResult(AssembleError.ArgError, $"line {line}: Failed to append value\n-> {res.ErrorMsg}"); return false; }
                }
                // imm
                else
                {
                    if (!TryParseImm(args[0], out hole)) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Failed to parse \"{args[0]}\" as an imm\n-> {res.ErrorMsg}"); return false; }

                    AppendVal(1, (sizecode << 2) | 0);
                    if (!TryAppendExpr(Size(sizecode), hole)) { res = new AssembleResult(AssembleError.ArgError, $"line {line}: Failed to append value\n-> {res.ErrorMsg}"); return false; }
                }

                return true;
            }
        }

        /// <summary>
        /// Attempts to patch the hole. returns PatchError.None on success
        /// </summary>
        /// <param name="res">data array to patch</param>
        /// <param name="symbols">the symbols used for lookup</param>
        /// <param name="data">the hole's data</param>
        /// <param name="err">the resulting error on failure</param>
        private static PatchError TryPatchHole<T>(T res, Dictionary<string, Expr> symbols, HoleData data, ref string err) where T : IList<byte>
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
                        case 8: res.Write(data.Address, 8, val); break;
                        case 4: res.Write(data.Address, 4, FloatAsUInt64((float)AsDouble(val))); break;

                        default: err = $"line {data.Line}: Attempt to use unsupported floating-point size"; return PatchError.Error;
                    }
                }
                // otherwise it's integral
                else res.Write(data.Address, data.Size, val);
            }
            else { err = $"line {data.Line}: Failed to evaluate expression\n-> {err}"; return PatchError.Unevaluated; }

            // successfully patched
            return PatchError.None;
        }

        /// <summary>
        /// Stores all the external predefined symbols that are not defined by the assembler itself
        /// </summary>
        private static Dictionary<string, Expr> PredefinedSymbols = new Dictionary<string, Expr>();

        /// <summary>
        /// Creates a new predefined symbol for the assembler
        /// </summary>
        /// <param name="key">the symol name</param>
        /// <param name="value">the symbol value</param>
        public static void DefineSymbol(string key, string value) { PredefinedSymbols.Add(key, new Expr() { Value = value }); }
        /// <summary>
        /// Creates a new predefined symbol for the assembler
        /// </summary>
        /// <param name="key">the symol name</param>
        /// <param name="value">the symbol value</param>
        public static void DefineSymbol(string key, UInt64 value) { PredefinedSymbols.Add(key, new Expr() { IntResult = value }); }
        /// <summary>
        /// Creates a new predefined symbol for the assembler
        /// </summary>
        /// <param name="key">the symol name</param>
        /// <param name="value">the symbol value</param>
        public static void DefineSymbol(string key, double value) { PredefinedSymbols.Add(key, new Expr() { FloatResult = value }); }

        /// <summary>
        /// Assembles the code into an object file
        /// </summary>
        /// <param name="code">the code to assemble</param>
        /// <param name="file">the resulting object file if no errors occur</param>
        public static AssembleResult Assemble(string code, out ObjectFile file)
        {
            file = new ObjectFile();
            AssembleArgs args = new AssembleArgs()
            {
                file = file,
                line = 0,

                last_static_label = null,
                res = default(AssembleResult),

                time = Time
            };

            // create the table of predefined symbols
            args.file.Symbols = new Dictionary<string, Expr>(PredefinedSymbols)
            {
                ["__time__"] = new Expr() { IntResult = args.time },
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
                string test = "1.2ee2";
                //string test = "a?b?c:d:e?f:g";

                if (!args.TryParseImm(test, out Expr _test)) MessageBox.Show(args.err.ToString());
                MessageBox.Show(_test.ToString());

                if (!_test.Evaluate(file.Symbols, out a, out floating, ref err)) MessageBox.Show(err);
                MessageBox.Show(_test.ToString());
            }
            */



            if (code.Length == 0) return new AssembleResult(AssembleError.EmptyFile, "The file was empty");

            while (pos < code.Length)
            {
                // find the next separator
                for (end = pos; end < code.Length && code[end] != '\n' && code[end] != '#'; ++end) ;

                ++args.line; // advance line counter
                // split the line
                if (!args.SplitLine(code.Substring(pos, end - pos))) return new AssembleResult(AssembleError.FormatError, $"line {args.line}: Failed to parse line\n-> {args.res.ErrorMsg}");
                // if the separator was a comment character, consume the rest of the line as well as no-op
                if (end < code.Length && code[end] == '#')
                    for (; end < code.Length && code[end] != '\n'; ++end) ;

                // process marked labels
                for (int i = 0; i < args.label_defs.Length; ++i)
                {
                    string label = args.label_defs[i]; // shorthand reference to current label

                    // handle local mutation
                    if (label.Length > 0 && label[0] != '.') args.last_static_label = label;
                    if (!args.MutateLabel(ref label)) return args.res;

                    if (!AssembleArgs.IsValidLabel(label)) return new AssembleResult(AssembleError.InvalidLabel, $"line {args.line}: Symbol name \"{label}\" invalid");
                    if (file.Symbols.ContainsKey(label)) return new AssembleResult(AssembleError.SymbolRedefinition, $"line {args.line}: Symbol \"{label}\" was already defined");

                    // add the symbol as an address (uses illegal symbol #base, which will be defined at link time)
                    file.Symbols.Add(label, new Expr() { OP = Expr.OPs.Add, Left = new Expr() { Value = "#base" }, Right = new Expr() { Value = file.Data.LongCount().MakeUnsigned().ToString() } });
                }

                // empty lines are ignored
                if (args.op != string.Empty)
                {
                    switch (args.op.ToUpper())
                    {
                        case "GLOBAL":
                            if (args.args.Length == 0) return new AssembleResult(AssembleError.ArgCount, $"line {args.line}: GLOBAL expected at least one symbol to export");

                            foreach (string symbol in args.args)
                            {
                                // special error message for using global on local labels
                                if (symbol[0] == '.') return new AssembleResult(AssembleError.ArgError, $"line {args.line}: Cannot export local symbols");

                                // test name for legality
                                if (!AssembleArgs.IsValidLabel(symbol)) return new AssembleResult(AssembleError.InvalidLabel, $"line {args.line}: Invalid symbol name \"{symbol}\"");

                                // don't add to global table twice
                                if (file.GlobalSymbols.Contains(symbol)) return new AssembleResult(AssembleError.SymbolRedefinition, $"line {args.line}: Attempt to export symbol \"{symbol}\" multiple times");

                                // add it to the globals list
                                file.GlobalSymbols.Add(symbol);
                            }

                            break;
                        case "DEF":
                            if (args.args.Length != 2) return new AssembleResult(AssembleError.ArgCount, $"line {args.line}: DEF expected 2 args");

                            // mutate and test name for legality
                            if (!args.MutateLabel(ref args.args[0])) return args.res;
                            if (!AssembleArgs.IsValidLabel(args.args[0])) return new AssembleResult(AssembleError.InvalidLabel, $"line {args.line}: Invalid label name \"{args.args[0]}\"");

                            // get the expression
                            if (!args.TryParseImm(args.args[1], out hole)) return new AssembleResult(AssembleError.FormatError, $"line {args.line}: DEF expected an expression as third arg\n-> {args.res.ErrorMsg}");

                            // don't redefine a symbol
                            if (file.Symbols.ContainsKey(args.args[0])) return new AssembleResult(AssembleError.SymbolRedefinition, $"line {args.line}: Symbol \"{args.args[0]}\" was already defined");

                            // add it to the dictionary
                            file.Symbols.Add(args.args[0], hole);
                            break;

                        case "EMIT": if (!args.TryProcessEmission()) return args.res; break;

                        // --------------------------
                        // -- OPCode assembly impl --
                        // --------------------------

                        // [8: op]
                        case "NOP":
                            if (args.args.Length != 0) return new AssembleResult(AssembleError.ArgCount, $"line {args.line}: NOP expected 0 args");
                            args.AppendVal(1, (UInt64)OPCode.NOP);
                            break;
                        case "STOP":
                            if (args.args.Length != 0) return new AssembleResult(AssembleError.ArgCount, $"line {args.line}: STOP expected 0 args");
                            args.AppendVal(1, (UInt64)OPCode.STOP);
                            break;
                        case "SYSCALL":
                            if (args.args.Length != 0) return new AssembleResult(AssembleError.ArgCount, $"line {args.line}: SYSCALL expected 0 args");
                            args.AppendVal(1, (UInt64)OPCode.SYSCALL);
                            break;

                        case "MOV": if (!args.TryProcessBinaryOp(OPCode.MOV)) return args.res; break;

                        case "MOVA": case "MOVNBE": if (!args.TryProcessBinaryOp(OPCode.MOVa)) return args.res; break;
                        case "MOVAE": case "MOVNB": if (!args.TryProcessBinaryOp(OPCode.MOVae)) return args.res; break;
                        case "MOVB": case "MOVNAE": if (!args.TryProcessBinaryOp(OPCode.MOVb)) return args.res; break;
                        case "MOVBE": case "MOVNA": if (!args.TryProcessBinaryOp(OPCode.MOVbe)) return args.res; break;

                        case "MOVG": case "MOVNLE": if (!args.TryProcessBinaryOp(OPCode.MOVg)) return args.res; break;
                        case "MOVGE": case "MOVNL": if (!args.TryProcessBinaryOp(OPCode.MOVge)) return args.res; break;
                        case "MOVL": case "MOVNGE": if (!args.TryProcessBinaryOp(OPCode.MOVl)) return args.res; break;
                        case "MOVLE": case "MOVNG": if (!args.TryProcessBinaryOp(OPCode.MOVle)) return args.res; break;

                        case "MOVZ": case "MOVE": if (!args.TryProcessBinaryOp(OPCode.MOVz)) return args.res; break;
                        case "MOVNZ": case "MOVNE": if (!args.TryProcessBinaryOp(OPCode.MOVnz)) return args.res; break;
                        case "MOVS": if (!args.TryProcessBinaryOp(OPCode.MOVs)) return args.res; break;
                        case "MOVNS": if (!args.TryProcessBinaryOp(OPCode.MOVns)) return args.res; break;
                        case "MOVP": case "MOVPE": if (!args.TryProcessBinaryOp(OPCode.MOVp)) return args.res; break;
                        case "MOVNP": case "MOVPO": if (!args.TryProcessBinaryOp(OPCode.MOVnp)) return args.res; break;
                        case "MOVO": if (!args.TryProcessBinaryOp(OPCode.MOVo)) return args.res; break;
                        case "MOVNO": if (!args.TryProcessBinaryOp(OPCode.MOVno)) return args.res; break;
                        case "MOVC": if (!args.TryProcessBinaryOp(OPCode.MOVc)) return args.res; break;
                        case "MOVNC": if (!args.TryProcessBinaryOp(OPCode.MOVnc)) return args.res; break;

                        case "SWAP":
                            if (args.args.Length != 2) return new AssembleResult(AssembleError.ArgCount, $"line {args.line}: SWAP expected 2 args");

                            args.AppendVal(1, (UInt64)OPCode.SWAP);

                            // reg, *
                            if (args.args[0][0] == '$')
                            {
                                if(!args.TryParseRegister(args.args[0], out a)) return new AssembleResult(AssembleError.FormatError, $"line {args.line}: Failed to parse \"{args.args[0]}\" as a register\n-> {args.res.ErrorMsg}");

                                // reg, reg
                                if (args.args[1][0] == '$')
                                {
                                    if(!args.TryParseRegister(args.args[1], out b)) return new AssembleResult(AssembleError.FormatError, $"line {args.line}: Failed to parse \"{args.args[1]}\" as a register\n-> {args.res.ErrorMsg}");

                                    args.AppendVal(1, (a << 4) | (args.sizecode << 2) | 0);
                                    args.AppendVal(1, b);
                                }
                                // reg, mem
                                else if (args.args[1][0] == '[')
                                {
                                    if(!args.TryParseAddress(args.args[1], out b, out c, out hole)) return new AssembleResult(AssembleError.FormatError, $"line {args.line}: Failed to parse \"{args.args[1]}\" as an address\n-> {args.res.ErrorMsg}");

                                    args.AppendVal(1, (a << 4) | (args.sizecode << 2) | 1);
                                    if (!args.TryAppendAddress(b, c, hole)) return new AssembleResult(AssembleError.ArgError, $"line {args.line}: Failed to append value\n-> {args.res.ErrorMsg}");
                                }
                                // reg, imm
                                else return new AssembleResult(AssembleError.FormatError, $"line {args.line}: Cannot use SWAP with an imm");
                            }
                            // mem, *
                            else if (args.args[0][0] == '[')
                            {
                                if(!args.TryParseAddress(args.args[0], out a, out b, out hole)) return new AssembleResult(AssembleError.FormatError, $"line {args.line}: Failed to parse \"{args.args[0]}\" as an address\n-> {args.res.ErrorMsg}");

                                // mem, reg
                                if (args.args[1][0] == '$')
                                {
                                    if (!args.TryParseRegister(args.args[1], out c)) return new AssembleResult(AssembleError.FormatError, $"line {args.line}: Failed to parse \"{args.args[1]}\" as a register\n-> {args.res.ErrorMsg}");

                                    args.AppendVal(1, (c << 4) | (args.sizecode << 2) | 1);
                                    if (!args.TryAppendAddress(a, b, hole)) return new AssembleResult(AssembleError.ArgError, $"line {args.line}: Failed to append value");
                                }
                                // mem, mem
                                else if (args.args[1][0] == '[') return new AssembleResult(AssembleError.FormatError, $"line {args.line}: Cannot use SWAP with two memory values");
                                // mem, imm
                                else return new AssembleResult(AssembleError.FormatError, $"line {args.line}: Cannot use SWAP with an imm");
                            }
                            // imm, *
                            else return new AssembleResult(AssembleError.FormatError, $"line {args.line}: Cannot use SWAP with an imm");

                            break;

                        case "UX": a = (UInt64)OPCode.UX; goto XEXTEND;
                        case "SX":
                            a = (UInt64)OPCode.SX;
                            XEXTEND:
                            if (args.args.Length != 2) return new AssembleResult(AssembleError.ArgCount, $"line {args.line}: XEXTEND expected 2 args");

                            if (!args.TryParseSizecode(args.args[0], out b)) return new AssembleResult(AssembleError.MissingSize, $"line {args.line}: UEXTEND expected size parameter as second arg\n-> {args.res.ErrorMsg}");
                            if (!args.TryParseRegister(args.args[1], out c)) return new AssembleResult(AssembleError.ArgError, $"line {args.line}: UEXTEND expected register parameter as third arg\n-> {args.res.ErrorMsg}");

                            args.AppendVal(1, a);
                            args.AppendVal(1, (c << 4) | (b << 2) | args.sizecode);

                            break;

                        case "UMUL": if (!args.TryProcessIMMRM(OPCode.UMUL)) return args.res; break;
                        case "SMUL": if (!args.TryProcessIMMRM(OPCode.SMUL)) return args.res; break;
                        case "UDIV": if (!args.TryProcessIMMRM(OPCode.UDIV)) return args.res; break;
                        case "SDIV": if (!args.TryProcessIMMRM(OPCode.SDIV)) return args.res; break;

                        case "ADD": if (!args.TryProcessBinaryOp(OPCode.ADD)) return args.res; break;
                        case "SUB": if (!args.TryProcessBinaryOp(OPCode.SUB)) return args.res; break;
                        case "BMUL": if (!args.TryProcessBinaryOp(OPCode.BMUL)) return args.res; break;
                        case "BUDIV": if (!args.TryProcessBinaryOp(OPCode.BUDIV)) return args.res; break;
                        case "BUMOD": if (!args.TryProcessBinaryOp(OPCode.BUMOD)) return args.res; break;
                        case "BSDIV": if (!args.TryProcessBinaryOp(OPCode.BSDIV)) return args.res; break;
                        case "BSMOD": if (!args.TryProcessBinaryOp(OPCode.BSMOD)) return args.res; break;

                        case "SL": if (!args.TryProcessBinaryOp(OPCode.SL, 0)) return args.res; break;
                        case "SR": if (!args.TryProcessBinaryOp(OPCode.SR, 0)) return args.res; break;
                        case "SAL": if (!args.TryProcessBinaryOp(OPCode.SAL, 0)) return args.res; break;
                        case "SAR": if (!args.TryProcessBinaryOp(OPCode.SAR, 0)) return args.res; break;
                        case "RL": if (!args.TryProcessBinaryOp(OPCode.RL, 0)) return args.res; break;
                        case "RR": if (!args.TryProcessBinaryOp(OPCode.RR, 0)) return args.res; break;

                        case "AND": if (!args.TryProcessBinaryOp(OPCode.AND)) return args.res; break;
                        case "OR": if (!args.TryProcessBinaryOp(OPCode.OR)) return args.res; break;
                        case "XOR": if (!args.TryProcessBinaryOp(OPCode.XOR)) return args.res; break;

                        case "CMP": if (!args.TryProcessBinaryOp(OPCode.CMP)) return args.res; break;
                        case "TEST": if (!args.TryProcessBinaryOp(OPCode.TEST)) return args.res; break;

                        // [8: unary op]   [4: dest][2:][2: size]
                        case "INC": if (!args.TryProcessUnaryOp(OPCode.INC)) return args.res; break;
                        case "DEC": if (!args.TryProcessUnaryOp(OPCode.DEC)) return args.res; break;
                        case "NEG": if (!args.TryProcessUnaryOp(OPCode.NEG)) return args.res; break;
                        case "NOT": if (!args.TryProcessUnaryOp(OPCode.NOT)) return args.res; break;
                        case "ABS": if (!args.TryProcessUnaryOp(OPCode.ABS)) return args.res; break;
                        case "CMPZ": if (!args.TryProcessUnaryOp(OPCode.CMPZ)) return args.res; break;

                        // [8: la]   [4:][4: dest]   [address]
                        case "LA":
                            if (args.args.Length != 2) return new AssembleResult(AssembleError.ArgCount, $"line {args.line}: LA expected 2 args");
                            if (args.sizecode != 3) return new AssembleResult(AssembleError.UsageError, $"line {args.line}: LA does not support the specified size code");

                            if (!args.TryParseRegister(args.args[0], out a)) return new AssembleResult(AssembleError.ArgError, $"line {args.line}: LA expecetd register as first arg\n-> {args.res.ErrorMsg}");
                            if (!args.TryParseAddress(args.args[1], out b, out c, out hole)) return new AssembleResult(AssembleError.ArgError, $"line {args.line}: LA expected address as second arg\n-> {args.res.ErrorMsg}");

                            args.AppendVal(1, (UInt64)OPCode.LA);
                            args.AppendVal(1, a);
                            if (!args.TryAppendAddress(b, c, hole)) return new AssembleResult(AssembleError.ArgError, $"line {args.line}: Failed to append value\n-> {args.res.ErrorMsg}");

                            break;

                        // [8: Jcc]   [address]
                        case "JMP": if (!args.TryProcessJump(OPCode.JMP)) return args.res; break;

                        case "JA": case "JNBE": if (!args.TryProcessJump(OPCode.Ja)) return args.res; break;
                        case "JAE": case "JNB": if (!args.TryProcessJump(OPCode.Jae)) return args.res; break;
                        case "JB": case "JNAE": if (!args.TryProcessJump(OPCode.Jb)) return args.res; break;
                        case "JBE": case "JNA": if (!args.TryProcessJump(OPCode.Jbe)) return args.res; break;

                        case "JG": case "JNLE": if (!args.TryProcessJump(OPCode.Jg)) return args.res; break;
                        case "JGE": case "JNL": if (!args.TryProcessJump(OPCode.Jge)) return args.res; break;
                        case "JL": case "JNGE": if (!args.TryProcessJump(OPCode.Jl)) return args.res; break;
                        case "JLE": case "JNG": if (!args.TryProcessJump(OPCode.Jle)) return args.res; break;

                        case "JZ": case "JE": if (!args.TryProcessJump(OPCode.Jz)) return args.res; break;
                        case "JNZ": case "JNE": if (!args.TryProcessJump(OPCode.Jnz)) return args.res; break;
                        case "JS": if (!args.TryProcessJump(OPCode.Js)) return args.res; break;
                        case "JNS": if (!args.TryProcessJump(OPCode.Jns)) return args.res; break;
                        case "JP": case "JPE": if (!args.TryProcessJump(OPCode.Jp)) return args.res; break;
                        case "JNP": case "JPO": if (!args.TryProcessJump(OPCode.Jnp)) return args.res; break;
                        case "JO": if (!args.TryProcessJump(OPCode.Jo)) return args.res; break;
                        case "JNO": if (!args.TryProcessJump(OPCode.Jno)) return args.res; break;
                        case "JC": if (!args.TryProcessJump(OPCode.Jc)) return args.res; break;
                        case "JNC": if (!args.TryProcessJump(OPCode.Jnc)) return args.res; break;

                        case "FADD": if (!args.TryProcessBinaryOp(OPCode.FADD, -1, 12)) return args.res; break;
                        case "FSUB": if (!args.TryProcessBinaryOp(OPCode.FSUB, -1, 12)) return args.res; break;
                        case "FMUL": if (!args.TryProcessBinaryOp(OPCode.FMUL, -1, 12)) return args.res; break;
                        case "FDIV": if (!args.TryProcessBinaryOp(OPCode.FDIV, -1, 12)) return args.res; break;
                        case "FMOD": if (!args.TryProcessBinaryOp(OPCode.FMOD, -1, 12)) return args.res; break;

                        case "FPOW": if (!args.TryProcessBinaryOp(OPCode.FPOW, -1, 12)) return args.res; break;
                        case "FSQRT": if (!args.TryProcessUnaryOp(OPCode.FSQRT, 12)) return args.res; break;
                        case "FEXP": if (!args.TryProcessUnaryOp(OPCode.FEXP, 12)) return args.res; break;
                        case "FLN": if (!args.TryProcessUnaryOp(OPCode.FLN, 12)) return args.res; break;
                        case "FNEG": if (!args.TryProcessUnaryOp(OPCode.FNEG, 12)) return args.res; break;
                        case "FABS": if (!args.TryProcessUnaryOp(OPCode.FABS, 12)) return args.res; break;
                        case "FCMPZ": if (!args.TryProcessUnaryOp(OPCode.FCMPZ, 12)) return args.res; break;

                        case "FSIN": if (!args.TryProcessUnaryOp(OPCode.FSIN, 12)) return args.res; break;
                        case "FCOS": if (!args.TryProcessUnaryOp(OPCode.FCOS, 12)) return args.res; break;
                        case "FTAN": if (!args.TryProcessUnaryOp(OPCode.FTAN, 12)) return args.res; break;

                        case "FSINH": if (!args.TryProcessUnaryOp(OPCode.FSINH, 12)) return args.res; break;
                        case "FCOSH": if (!args.TryProcessUnaryOp(OPCode.FCOSH, 12)) return args.res; break;
                        case "FTANH": if (!args.TryProcessUnaryOp(OPCode.FTANH, 12)) return args.res; break;

                        case "FASIN": if (!args.TryProcessUnaryOp(OPCode.FASIN, 12)) return args.res; break;
                        case "FACOS": if (!args.TryProcessUnaryOp(OPCode.FACOS, 12)) return args.res; break;
                        case "FATAN": if (!args.TryProcessUnaryOp(OPCode.FATAN, 12)) return args.res; break;
                        case "FATAN2": if (!args.TryProcessBinaryOp(OPCode.FATAN2, -1, 12)) return args.res; break;

                        case "FLOOR": if (!args.TryProcessUnaryOp(OPCode.FLOOR, 12)) return args.res; break;
                        case "CEIL": if (!args.TryProcessUnaryOp(OPCode.CEIL, 12)) return args.res; break;
                        case "ROUND": if (!args.TryProcessUnaryOp(OPCode.ROUND, 12)) return args.res; break;
                        case "TRUNC": if (!args.TryProcessUnaryOp(OPCode.TRUNC, 12)) return args.res; break;

                        case "FCMP": if (!args.TryProcessBinaryOp(OPCode.FCMP, -1, 12)) return args.res; break;

                        case "FTOI": if (!args.TryProcessUnaryOp(OPCode.FTOI, 12)) return args.res; break;
                        case "ITOF": if (!args.TryProcessUnaryOp(OPCode.ITOF, 12)) return args.res; break;

                        case "PUSH":
                            if (args.args.Length != 1) return new AssembleResult(AssembleError.ArgCount, $"line {args.line}: PUSH expected 1 arg");

                            args.AppendVal(1, (UInt64)OPCode.PUSH);

                            if (args.TryParseImm(args.args[0], out hole))
                            {
                                args.AppendVal(1, (args.sizecode << 2) | 0);
                                if (!args.TryAppendExpr(Size(args.sizecode), hole)) return new AssembleResult(AssembleError.ArgError, $"line {args.line}: Failed to append value\n-> {args.res.ErrorMsg}");
                            }
                            else if (args.TryParseRegister(args.args[0], out a))
                            {
                                args.AppendVal(1, (a << 4) | (args.sizecode << 2) | 1);
                            }
                            else return new AssembleResult(AssembleError.FormatError, $"line {args.line}: Couldn't parse \"{args.args[0]}\" as an imm or register");

                            break;
                        case "POP":
                            if (args.args.Length != 1) return new AssembleResult(AssembleError.ArgCount, $"line {args.line}: POP expected 1 arg");

                            if (!args.TryParseRegister(args.args[0], out a)) return new AssembleResult(AssembleError.ArgError, $"line {args.line}: POP expected register as second arg\n-> {args.res.ErrorMsg}");

                            args.AppendVal(1, (UInt64)OPCode.POP);
                            args.AppendVal(1, (a << 4) | (args.sizecode << 2));

                            break;
                        case "CALL":
                            if (args.args.Length != 1) return new AssembleResult(AssembleError.ArgCount, $"line {args.line}: CALL expected 1 arg");
                            if (!args.TryParseAddress(args.args[0], out a, out b, out hole)) return new AssembleResult(AssembleError.ArgError, $"line {args.line}: CALL expected address as first arg\n-> {args.res.ErrorMsg}");

                            args.AppendVal(1, (UInt64)OPCode.CALL);
                            if (!args.TryAppendAddress(a, b, hole)) return new AssembleResult(AssembleError.ArgError, $"line {args.line}: Failed to append value\n-> {args.res.ErrorMsg}");

                            break;
                        case "RET":
                            if (args.args.Length != 0) return new AssembleResult(AssembleError.ArgCount, $"line {args.line}: CALL expected 0 args");

                            args.AppendVal(1, (UInt64)OPCode.RET);

                            break;

                        case "BSWAP": if (!args.TryProcessUnaryOp(OPCode.BSWAP)) return args.res; break;
                        case "BEXTR": if (!args.TryProcessBinaryOp(OPCode.BEXTR, 1)) return args.res; break;
                        case "BLSI": if (!args.TryProcessUnaryOp(OPCode.BLSI)) return args.res; break;
                        case "BLSMSK": if (!args.TryProcessUnaryOp(OPCode.BLSMSK)) return args.res; break;
                        case "BLSR": if (!args.TryProcessUnaryOp(OPCode.BLSR)) return args.res; break;
                        case "ANDN": if (!args.TryProcessBinaryOp(OPCode.ANDN)) return args.res; break;

                        case "GETF": a = (UInt64)OPCode.GETF; goto GETSETF;
                        case "SETF":
                            a = (UInt64)OPCode.SETF;
                            GETSETF:
                            if (args.args.Length != 1) return new AssembleResult(AssembleError.ArgCount, $"line {args.line}: GETF expected one arg");
                            if (!args.TryParseRegister(args.args[0], out b)) return new AssembleResult(AssembleError.UsageError, $"line {args.line}: GETF expected arg one to be a register");
                            if (args.sizecode != 3) return new AssembleResult(AssembleError.UsageError, $"line {args.line}: GETF does not support the specified size code");

                            args.AppendVal(1, a);
                            args.AppendVal(1, b);

                            break;

                        case "LOOP": // loop reg, address, (step = 1)
                            // 2 args default step
                            if (args.args.Length == 2) a = 0;
                            // 3 args explicit step
                            else if (args.args.Length == 3)
                            {
                                if (!args.TryParseInstantImm(args.args[2], out a, out floating))
                                    return new AssembleResult(AssembleError.ArgError, $"line {args.line}: LOOP third argument (explicit step) expected an instant imm");
                                if (floating) return new AssembleResult(AssembleError.ArgError, $"line {args.line}: LOOP third argument (explicit step) may not be floating-point");

                                switch (a)
                                {
                                    case 1: a = 0; break;
                                    case 2: a = 1; break;
                                    case 4: a = 2; break;
                                    case 8: a = 3; break;

                                    default: return new AssembleResult(AssembleError.ArgError, $"line {args.line}: LOOP third argument (explicit step) must be 1, 2, 4, or 8");
                                }
                            }
                            else return new AssembleResult(AssembleError.ArgCount, $"line {args.line}: LOOP expected two args (3 for explicit step)");

                            if (!args.TryParseRegister(args.args[0], out b)) return new AssembleResult(AssembleError.UsageError, $"line {args.line}: LOOP expected register as first arg");
                            if (!args.TryParseAddress(args.args[1], out c, out d, out hole)) return new AssembleResult(AssembleError.UsageError, $"line {args.line}: LOOP expected an address as second arg");

                            args.AppendVal(1, (UInt64)OPCode.LOOP);
                            args.AppendVal(1, (b << 4) | (args.sizecode << 2) | a);
                            if (!args.TryAppendAddress(c, d, hole)) return args.res;

                            break;

                        case "FX":
                            if (args.args.Length != 2) return new AssembleResult(AssembleError.ArgCount, $"line {args.line}: XEXTEND expected 2 args");

                            if (!args.TryParseSizecode(args.args[0], out a)) return new AssembleResult(AssembleError.MissingSize, $"line {args.line}: UEXTEND expected size parameter as second arg\n-> {args.res.ErrorMsg}");
                            if (!args.TryParseRegister(args.args[1], out b)) return new AssembleResult(AssembleError.ArgError, $"line {args.line}: UEXTEND expected register parameter as third arg\n-> {args.res.ErrorMsg}");

                            args.AppendVal(1, (UInt64)OPCode.FX);
                            args.AppendVal(1, (b << 4) | (a << 2) | args.sizecode);

                            break;

                        case "SLP": if (!args.TryProcessIMMRM(OPCode.SLP)) return args.res; break;

                        default: return new AssembleResult(AssembleError.UnknownOp, $"line {args.line}: Unknown operation \"{args.op}\"");
                    }
                }

                // advance to after the new line
                pos = end + 1;
            }

            // make sure all global symbols were actually defined prior to link-time
            foreach (string str in file.GlobalSymbols) if (!file.Symbols.ContainsKey(str)) return new AssembleResult(AssembleError.UnknownSymbol, $"Global symbol \"{str}\" was never defined");

            // evaluate each symbol to link all internal symbols and minimize object file complexity
            foreach (var entry in file.Symbols) entry.Value.Evaluate(file.Symbols, out a, out floating, ref err);

            // try to eliminate as many holes as possible (we want as clean an object file as possible)
            for (int i = file.Holes.Count - 1; i >= 0; --i)
            {
                switch (TryPatchHole(file.Data, file.Symbols, file.Holes[i], ref err))
                {
                    case PatchError.None: file.Holes.RemoveAt(i); break; // remove the hole if we solved it
                    case PatchError.Unevaluated: break;
                    case PatchError.Error: return new AssembleResult(AssembleError.ArgError, err);

                    default: throw new ArgumentException("Unknown patch error encountered");
                }
            }

            // return no error
            return new AssembleResult(AssembleError.None, string.Empty);
        }
        /// <summary>
        /// Links the object files together and creates an executable
        /// </summary>
        /// <param name="res">the resulting executable data if no errors occur</param>
        /// <param name="objs">the object files to link</param>
        public static LinkResult Link(out byte[] res, params ObjectFile[] objs)
        {
            res = null; // initially null result

            // get total size of objet files
            UInt64 filesize = 0;
            foreach (ObjectFile obj in objs) filesize += (UInt64)obj.Data.LongCount();
            // if zero, there is nothing to link
            if (filesize == 0) return new LinkResult(LinkError.EmptyResult, "Resulting file is empty");

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
                    if (G_symbols.ContainsKey(symbol)) return new LinkResult(LinkError.SymbolRedefinition, $"Global symbol \"{symbol}\" was already defined");
                    // make sure the symbol exists in locals dictionary (if using built-in assembler above, this should never happen with valid object files)
                    if (!symbols[i].TryGetValue(symbol, out Expr hole)) return new LinkResult(LinkError.MissingSymbol, $"Global symbol \"{symbol}\" undefined");

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
                    PatchError _err = TryPatchHole(res, symbols[i], data, ref err);
                    if (_err == PatchError.Unevaluated) _err = TryPatchHole(res, G_symbols, data, ref err);
                    switch (_err)
                    {
                        case PatchError.None: break;
                        case PatchError.Unevaluated: return new LinkResult(LinkError.MissingSymbol, err);
                        case PatchError.Error: return new LinkResult(LinkError.FormatError, err);

                        default: throw new ArgumentException("Unknown patch error encountered");
                    }
                }
            }

            // write the header
            if (main == null) return new LinkResult(LinkError.MissingSymbol, "No entry point \"main\"");
            if (!main.Evaluate(G_symbols, out val, out floating, ref err)) return new LinkResult(LinkError.MissingSymbol, $"Failed to evaluate global symbol \"main\"\n-> {err}");

            res.Write(0, 1, (UInt64)OPCode.JMP);
            res.Write(1, 1, 0x80);
            res.Write(2, 8, val);

            // linked successfully
            return new LinkResult(LinkError.None, string.Empty);
        }
    }
}
