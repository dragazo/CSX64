﻿using System;
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
    public class Computer
    {
        // -----------
        // -- Types --
        // -----------
        
        public enum ErrorCode
        {
            None, OutOfBounds, UnhandledSyscall, UndefinedBehavior, Placeholder, ArithmeticError
        }
        public enum OPCode
        {
            Nop, Stop, Syscall,

            Mov,
            MOVa, MOVae, MOVb, MOVbe, MOVg, MOVge, MOVl, MOVle,
            MOVz, MOVnz, MOVs, MOVns, MOVp, MOVnp, MOVo, MOVno, MOVc, MOVnc,

            Swap,

            UExtend, SExtend,

            UMUL, SMUL, UDIV, SDIV,

            Add, Sub, Bmul, Budiv, Bumod, Bsdiv, Bsmod,
            SL, SR, SAL, SAR, RL, RR,
            And, Or, Xor,

            Cmp, Test,

            Inc, Dec, Neg, Not, Abs, Cmp0,

            La,

            Jmp,
            Ja, Jae, Jb, Jbe, Jg, Jge, Jl, Jle,
            Jz, Jnz, Js, Jns, Jp, Jnp, Jo, Jno, Jc, Jnc,

            Fadd, Fsub, Fmul, Fdiv, Fmod,
            Fpow, Fsqrt, Fexp, Fln, Fabs, Fcmp0,

            Fsin, Fcos, Ftan,
            Fsinh, Fcosh, Ftanh,
            Fasin, Facos, Fatan, Fatan2,

            Ffloor, Fceil, Fround, Ftrunc,

            Fcmp,

            FTOI, ITOF,

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

        #region

        // -- op utilities --

        // [8: binary op]   [4: dest][2: size][2: mode]   (mode = 0: [size: imm]   mode = 1: [4:][4: r]   mode = 2: [address]   mode = 3: UND)
        private bool ParseBinaryOpFormat(ref UInt64 s, ref UInt64 a, ref UInt64 b)
        {
            // read settings
            if (!GetMem(1, ref s)) return false;

            // get b
            switch (s & 3)
            {
                case 0: if (!GetMem(Size((s >> 2) & 3), ref b)) return false; break;
                case 1: if (!GetMem(1, ref b)) return false; b = Registers[b & 15].Get((s >> 2) & 3); break;
                case 2: if (!GetAddress(ref b) || !GetMem(b, Size((s >> 2) & 3), ref b)) return false; break;

                // otherwise undefined
                default: Fail(ErrorCode.UndefinedBehavior); return false;
            }

            // get a
            a = Registers[s >> 4].Get((s >> 2) & 3);

            return true;
        }
        // [8: unary op]   [4: dest][2:][2: size]
        private bool ParseUnaryOpFormat(ref UInt64 s, ref UInt64 a)
        {
            // get settings
            if (!GetMem(1, ref s)) return false;

            a = Registers[s >> 4].Get(s & 3);

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
        private void UpdateFlagsF(double value)
        {
            Flags.Z = value == 0;
            Flags.S = value < 0;

            Flags.O = double.IsInfinity(value);
            Flags.C = double.IsNaN(value);
        }

        // -- integral ops --

        /* -- ADD --

        Description:
            Computes the addition of two integers.
            The destination must be a register, but the value to add may be an imm, reg, or value from memory.

        Operation:
            byte:  byte  dest <- byte  dest + byte  val
            word:  word  dest <- word  dest + word  val
            dword: dword dest <- dword dest + dword val
            qword: qword dest <- qword dest + qword val

        Flags Affected:
            ZF is set if the result is zero, and cleared otherwise.
            SF is set if the result is negative (high bit set), and cleared otherwise.
            PF is set if the result has even parity in the low 8 bits, and cleared otherwise.
            
            CF is set if the addition caused a carry out from the high bit, and cleared otherwise.
            OF is set if the addition resulted in arithmetic over/underflow, and cleared otherwise.

        OPCode Format: [8: add]   [4: dest][2: size][2: mode]   (mode = 0: [size: imm]   mode = 1: [4:][4: r]   mode = 2: [address]   mode = 3: UND)
        */
        private bool ProcessAdd()
        {
            UInt64 s = 0, a = 0, b = 0;
            if (!ParseBinaryOpFormat(ref s, ref a, ref b)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UInt64 res = Truncate(a + b, sizecode);
            Registers[s >> 4].Set(sizecode, res);

            UpdateFlagsI(res, sizecode);
            Flags.C = res < a && res < b; // if overflow is caused, some of one value must go toward it, so the truncated result must necessarily be less than both args
            Flags.O = Positive(a, sizecode) == Positive(b, sizecode) && Positive(a, sizecode) != Positive(res, sizecode);

            return true;
        }
        /* -- SUB --

        Description:
            Computes the subtraction of two integers.
            The destination must be a register, but the value to add may be an imm, reg, or value from memory.
            This operation sets the flags in such a way as to make conditional ops reflect their respective conditions for the two values (e.g. ja = dest u> val, jge = dest s>= val, etc.).

        Operation:
            byte:  byte  dest <- byte  dest - byte  val
            word:  word  dest <- word  dest - word  val
            dword: dword dest <- dword dest - dword val
            qword: qword dest <- qword dest - qword val

        Flags Affected:
            ZF is set if the result is zero, and cleared otherwise.
            SF is set if the result is negative (high bit set), and cleared otherwise.
            PF is set if the result has even parity in the low 8 bits, and cleared otherwise.
            
            CF is set if the addition caused a borrow from the high bit, and cleared otherwise.
            OF is set if the addition resulted in arithmetic over/underflow, and cleared otherwise.

        OPCode Format: [8: sub]   [4: dest][2: size][2: mode]   (mode = 0: [size: imm]   mode = 1: [4:][4: r]   mode = 2: [address]   mode = 3: UND)
        */
        private bool ProcessSub(bool apply = true)
        {
            UInt64 s = 0, a = 0, b = 0;
            if (!ParseBinaryOpFormat(ref s, ref a, ref b)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UInt64 res = Truncate(a - b, sizecode);
            if (apply) Registers[s >> 4].Set(sizecode, res);

            UpdateFlagsI(res, sizecode);
            Flags.C = a < b; // if a < b, a borrow was taken from the highest bit
            Flags.O = Positive(a, sizecode) != Positive(b, sizecode) && Positive(a, sizecode) != Positive(res, sizecode);

            return true;
        }

        /* -- BMUL --

        Description:
            Computes the multiplication of two integers, keeping only the low half of the result.
            The destination must be a register, but the value to add may be an imm, reg, or value from memory.

        Operation:
            byte:  byte  dest <- byte  dest * byte  val
            word:  word  dest <- word  dest * word  val
            dword: dword dest <- dword dest * dword val
            qword: qword dest <- qword dest * qword val

        Flags Affected:
            ZF is set if the result is zero, and cleared otherwise.
            SF is set if the result is negative (high bit set), and cleared otherwise.
            PF is set if the result has even parity in the low 8 bits, and cleared otherwise.

        OPCode Format: [8: bmul]   [4: dest][2: size][2: mode]   (mode = 0: [size: imm]   mode = 1: [4:][4: r]   mode = 2: [address]   mode = 3: UND)
        */
        private bool ProcessBmul()
        {
            UInt64 s = 0, a = 0, b = 0;
            if (!ParseBinaryOpFormat(ref s, ref a, ref b)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UInt64 res = Truncate(a * b, sizecode);
            Registers[s >> 4].Set(sizecode, res);

            UpdateFlagsI(res, sizecode);

            return true;
        }

        /* -- BUDIV --

        Description:
            Computes the division of two unsigned integers.
            The destination must be a register, but the value to add may be an imm, reg, or value from memory.
            Causes an arithmetic error on division by zero.

        Operation:
            byte:  byte  dest <- byte  dest / byte  val
            word:  word  dest <- word  dest / word  val
            dword: dword dest <- dword dest / dword val
            qword: qword dest <- qword dest / qword val

        Flags Affected:
            ZF is set if the result is zero, and cleared otherwise.
            SF is set if the result is negative (high bit set), and cleared otherwise.
            PF is set if the result has even parity in the low 8 bits, and cleared otherwise.

        OPCode Format: [8: budiv]   [4: dest][2: size][2: mode]   (mode = 0: [size: imm]   mode = 1: [4:][4: r]   mode = 2: [address]   mode = 3: UND)
        */
        private bool ProcessBudiv()
        {
            UInt64 s = 0, a = 0, b = 0;
            if (!ParseBinaryOpFormat(ref s, ref a, ref b)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            if (b == 0) { Fail(ErrorCode.ArithmeticError); return false; }

            UInt64 res = Truncate(a / b, sizecode);
            Registers[s >> 4].Set(sizecode, res);

            UpdateFlagsI(res, sizecode);

            return true;
        }
        /* -- BUMOD --

        Description:
            Computes the remainder of division of two unsigned integers.
            The destination must be a register, but the value to add may be an imm, reg, or value from memory.
            Causes an arithmetic error on division by zero.

        Operation:
            byte:  byte  dest <- byte  dest % byte  val
            word:  word  dest <- word  dest % word  val
            dword: dword dest <- dword dest % dword val
            qword: qword dest <- qword dest % qword val

        Flags Affected:
            ZF is set if the result is zero, and cleared otherwise.
            SF is set if the result is negative (high bit set), and cleared otherwise.
            PF is set if the result has even parity in the low 8 bits, and cleared otherwise.
            
            CF is set if the addition caused a carry out from the high bit, and cleared otherwise.
            OF is set if the addition resulted in arithmetic over/underflow, and cleared otherwise.

        OPCode Format: [8: bumod]   [4: dest][2: size][2: mode]   (mode = 0: [size: imm]   mode = 1: [4:][4: r]   mode = 2: [address]   mode = 3: UND)
        */
        private bool ProcessBumod()
        {
            UInt64 s = 0, a = 0, b = 0;
            if (!ParseBinaryOpFormat(ref s, ref a, ref b)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            if (b == 0) { Fail(ErrorCode.ArithmeticError); return false; }

            UInt64 res = Truncate(a % b, sizecode);
            Registers[s >> 4].Set(sizecode, res);

            UpdateFlagsI(res, sizecode);

            return true;
        }

        /* -- BSDIV --

        Description:
            Computes the division of two signed integers.
            The destination must be a register, but the value to add may be an imm, reg, or value from memory.
            Causes an arithmetic error on division by zero.

        Operation:
            byte:  byte  dest <- byte  dest / byte  val
            word:  word  dest <- word  dest / word  val
            dword: dword dest <- dword dest / dword val
            qword: qword dest <- qword dest / qword val

        Flags Affected:
            ZF is set if the result is zero, and cleared otherwise.
            SF is set if the result is negative (high bit set), and cleared otherwise.
            PF is set if the result has even parity in the low 8 bits, and cleared otherwise.

        OPCode Format: [8: bsdiv]   [4: dest][2: size][2: mode]   (mode = 0: [size: imm]   mode = 1: [4:][4: r]   mode = 2: [address]   mode = 3: UND)
        */
        private bool ProcessBsdiv()
        {
            UInt64 s = 0, a = 0, b = 0;
            if (!ParseBinaryOpFormat(ref s, ref a, ref b)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            if (b == 0) { Fail(ErrorCode.ArithmeticError); return false; }

            UInt64 res = Truncate((SignExtend(a, sizecode).MakeSigned() / SignExtend(b, sizecode).MakeSigned()).MakeUnsigned(), sizecode);
            Registers[s >> 4].Set(sizecode, res);

            UpdateFlagsI(res, sizecode);

            return true;
        }
        /* -- BSMOD --

        Description:
            Computes the remainder of division of two signed integers.
            The destination must be a register, but the value to add may be an imm, reg, or value from memory.
            Causes an arithmetic error on division by zero.
            If dest or val is negative, the result is platform-dependent.

        Operation:
            byte:  byte  dest <- byte  dest % byte  val
            word:  word  dest <- word  dest % word  val
            dword: dword dest <- dword dest % dword val
            qword: qword dest <- qword dest % qword val

        Flags Affected:
            ZF is set if the result is zero, and cleared otherwise.
            SF is set if the result is negative (high bit set), and cleared otherwise.
            PF is set if the result has even parity in the low 8 bits, and cleared otherwise.

        OPCode Format: [8: bsmod]   [4: dest][2: size][2: mode]   (mode = 0: [size: imm]   mode = 1: [4:][4: r]   mode = 2: [address]   mode = 3: UND)
        */
        private bool ProcessBsmod()
        {
            UInt64 s = 0, a = 0, b = 0;
            if (!ParseBinaryOpFormat(ref s, ref a, ref b)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            if (b == 0) { Fail(ErrorCode.ArithmeticError); return false; }

            UInt64 res = Truncate((SignExtend(a, sizecode).MakeSigned() % SignExtend(b, sizecode).MakeSigned()).MakeUnsigned(), sizecode);
            Registers[s >> 4].Set(sizecode, res);

            UpdateFlagsI(res, sizecode);

            return true;
        }

        /* -- SL --

        Description:
            Computes a logical left shift of an unsigned integer by an unsigned integer.
            The destination must be a register, but the value to add may be an imm, reg, or value from memory.
            Shifting an n-bit value is performed in modulo-n.
            Low-order shifted bits are filled with 0.

        Operation:
            byte:  byte  dest <- byte  dest << byte  val
            word:  word  dest <- word  dest << word  val
            dword: dword dest <- dword dest << dword val
            qword: qword dest <- qword dest << qword val

        Flags Affected:
            ZF is set if the result is zero, and cleared otherwise.
            SF is set if the result is negative (high bit set), and cleared otherwise.
            PF is set if the result has even parity in the low 8 bits, and cleared otherwise.

        OPCode Format: [8: sl]   [4: dest][2: size][2: mode]   (mode = 0: [size: imm]   mode = 1: [4:][4: r]   mode = 2: [address]   mode = 3: UND)
        */
        private bool ProcessSL()
        {
            UInt64 s = 0, a = 0, b = 0;
            if (!ParseBinaryOpFormat(ref s, ref a, ref b)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UInt16 sh = (UInt16)(b % SizeBits(sizecode));
            UInt64 res = Truncate(a << sh, sizecode);
            Registers[s >> 4].Set(sizecode, res);

            UpdateFlagsI(res, sizecode);

            return true;
        }
        /* -- SR --

        Description:
            Computes a logical right shift of an unsigned integer by an unsigned integer.
            The destination must be a register, but the value to add may be an imm, reg, or value from memory.
            Shifting an n-bit value is performed in modulo-n.
            High-order shifted bits are filled with 0.

        Operation:
            byte:  byte  dest <- byte  dest >> byte  val
            word:  word  dest <- word  dest >> word  val
            dword: dword dest <- dword dest >> dword val
            qword: qword dest <- qword dest >> qword val

        Flags Affected:
            ZF is set if the result is zero, and cleared otherwise.
            SF is set if the result is negative (high bit set), and cleared otherwise.
            PF is set if the result has even parity in the low 8 bits, and cleared otherwise.

        OPCode Format: [8: sr]   [4: dest][2: size][2: mode]   (mode = 0: [size: imm]   mode = 1: [4:][4: r]   mode = 2: [address]   mode = 3: UND)
        */
        private bool ProcessSR()
        {
            UInt64 s = 0, a = 0, b = 0;
            if (!ParseBinaryOpFormat(ref s, ref a, ref b)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UInt16 sh = (UInt16)(b % SizeBits(sizecode));
            UInt64 res = a >> sh;
            Registers[s >> 4].Set(sizecode, res);

            UpdateFlagsI(res, sizecode);

            return true;
        }

        /* -- SAL --

        Description:
            Computes an arithmetic left shift of a signed integer by an unsigned integer.
            The destination must be a register, but the value to add may be an imm, reg, or value from memory.
            Shifting an n-bit value is performed in modulo-n.
            Low-order shifted bits are filled with 0.

        Operation:
            byte:  byte  dest <- byte  dest << byte  val
            word:  word  dest <- word  dest << word  val
            dword: dword dest <- dword dest << dword val
            qword: qword dest <- qword dest << qword val

        Flags Affected:
            ZF is set if the result is zero, and cleared otherwise.
            SF is set if the result is negative (high bit set), and cleared otherwise.
            PF is set if the result has even parity in the low 8 bits, and cleared otherwise.

        OPCode Format: [8: sal]   [4: dest][2: size][2: mode]   (mode = 0: [size: imm]   mode = 1: [4:][4: r]   mode = 2: [address]   mode = 3: UND)
        */
        private bool ProcessSAL()
        {
            UInt64 s = 0, a = 0, b = 0;
            if (!ParseBinaryOpFormat(ref s, ref a, ref b)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UInt16 sh = (UInt16)(b % SizeBits(sizecode));
            UInt64 res = Truncate((SignExtend(a, sizecode).MakeSigned() << sh).MakeUnsigned(), sizecode);
            Registers[s >> 4].Set(sizecode, res);

            UpdateFlagsI(res, sizecode);

            return true;
        }
        /* -- SL --

        Description:
            Computes an arithmetic right shift of a signed integer by an unsigned integer.
            The destination must be a register, but the value to add may be an imm, reg, or value from memory.
            Shifting an n-bit value is performed in modulo-n.
            High-order shifted bits are filled with the sign bit.

        Operation:
            byte:  byte  dest <- byte  dest >> byte  val
            word:  word  dest <- word  dest >> word  val
            dword: dword dest <- dword dest >> dword val
            qword: qword dest <- qword dest >> qword val

        Flags Affected:
            ZF is set if the result is zero, and cleared otherwise.
            SF is set if the result is negative (high bit set), and cleared otherwise.
            PF is set if the result has even parity in the low 8 bits, and cleared otherwise.

        OPCode Format: [8: sar]   [4: dest][2: size][2: mode]   (mode = 0: [size: imm]   mode = 1: [4:][4: r]   mode = 2: [address]   mode = 3: UND)
        */
        private bool ProcessSAR()
        {
            UInt64 s = 0, a = 0, b = 0;
            if (!ParseBinaryOpFormat(ref s, ref a, ref b)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UInt16 sh = (UInt16)(b % SizeBits(sizecode));
            UInt64 res = Truncate((SignExtend(a, sizecode).MakeSigned() >> sh).MakeUnsigned(), sizecode);
            Registers[s >> 4].Set(sizecode, res);

            UpdateFlagsI(res, sizecode);

            return true;
        }

        /* -- RL --

        Description:
            Computes a logical left rotation of an unsigned integer by an unsigned integer.
            The destination must be a register, but the value to add may be an imm, reg, or value from memory.
            Shifting an n-bit value is performed in modulo-n.

        Operation:
            byte:  byte  dest <- byte  dest <<r byte  val
            word:  word  dest <- word  dest <<r word  val
            dword: dword dest <- dword dest <<r dword val
            qword: qword dest <- qword dest <<r qword val

        Flags Affected:
            ZF is set if the result is zero, and cleared otherwise.
            SF is set if the result is negative (high bit set), and cleared otherwise.
            PF is set if the result has even parity in the low 8 bits, and cleared otherwise.

        OPCode Format: [8: rl]   [4: dest][2: size][2: mode]   (mode = 0: [size: imm]   mode = 1: [4:][4: r]   mode = 2: [address]   mode = 3: UND)
        */
        private bool ProcessRL()
        {
            UInt64 s = 0, a = 0, b = 0;
            if (!ParseBinaryOpFormat(ref s, ref a, ref b)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UInt16 sh = (UInt16)(b % SizeBits(sizecode));
            UInt64 res = Truncate((a << sh) | (a >> ((UInt16)SizeBits(sizecode) - sh)), sizecode);
            Registers[s >> 4].Set(sizecode, res);

            UpdateFlagsI(res, sizecode);

            return true;
        }
        /* -- RR --

        Description:
            Computes a logical right rotation of an unsigned integer by an unsigned integer.
            The destination must be a register, but the value to add may be an imm, reg, or value from memory.
            Shifting an n-bit value is performed in modulo-n.

        Operation:
            byte:  byte  dest <- byte  dest >>r byte  val
            word:  word  dest <- word  dest >>r word  val
            dword: dword dest <- dword dest >>r dword val
            qword: qword dest <- qword dest >>r qword val

        Flags Affected:
            ZF is set if the result is zero, and cleared otherwise.
            SF is set if the result is negative (high bit set), and cleared otherwise.
            PF is set if the result has even parity in the low 8 bits, and cleared otherwise.

        OPCode Format: [8: rr]   [4: dest][2: size][2: mode]   (mode = 0: [size: imm]   mode = 1: [4:][4: r]   mode = 2: [address]   mode = 3: UND)
        */
        private bool ProcessRR()
        {
            UInt64 s = 0, a = 0, b = 0;
            if (!ParseBinaryOpFormat(ref s, ref a, ref b)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UInt16 sh = (UInt16)(b % SizeBits(sizecode));
            UInt64 res = Truncate((a >> sh) | (a << ((UInt16)SizeBits(sizecode) - sh)), sizecode);
            Registers[s >> 4].Set(sizecode, res);

            UpdateFlagsI(res, sizecode);

            return true;
        }

        /* -- AND --

        Description:
            Computes the logical and of two unsigned integers.
            The destination must be a register, but the value to add may be an imm, reg, or value from memory.

        Operation:
            byte:  byte  dest <- byte  dest and byte  val
            word:  word  dest <- word  dest and word  val
            dword: dword dest <- dword dest and dword val
            qword: qword dest <- qword dest and qword val

        Flags Affected:
            ZF is set if the result is zero, and cleared otherwise.
            SF is set if the result is negative (high bit set), and cleared otherwise.
            PF is set if the result has even parity in the low 8 bits, and cleared otherwise.

        OPCode Format: [8: and]   [4: dest][2: size][2: mode]   (mode = 0: [size: imm]   mode = 1: [4:][4: r]   mode = 2: [address]   mode = 3: UND)
        */
        private bool ProcessAnd(bool apply = true)
        {
            UInt64 s = 0, a = 0, b = 0;
            if (!ParseBinaryOpFormat(ref s, ref a, ref b)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UInt64 res = a & b;
            if (apply) Registers[s >> 4].Set(sizecode, res);

            UpdateFlagsI(res, sizecode);

            return true;
        }
        /* -- OR --

        Description:
            Computes the logical or of two unsigned integers.
            The destination must be a register, but the value to add may be an imm, reg, or value from memory.

        Operation:
            byte:  byte  dest <- byte  dest or byte  val
            word:  word  dest <- word  dest or word  val
            dword: dword dest <- dword dest or dword val
            qword: qword dest <- qword dest or qword val

        Flags Affected:
            ZF is set if the result is zero, and cleared otherwise.
            SF is set if the result is negative (high bit set), and cleared otherwise.
            PF is set if the result has even parity in the low 8 bits, and cleared otherwise.

        OPCode Format: [8: or]   [4: dest][2: size][2: mode]   (mode = 0: [size: imm]   mode = 1: [4:][4: r]   mode = 2: [address]   mode = 3: UND)
        */
        private bool ProcessOr()
        {
            UInt64 s = 0, a = 0, b = 0;
            if (!ParseBinaryOpFormat(ref s, ref a, ref b)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UInt64 res = a | b;
            Registers[s >> 4].Set(sizecode, res);

            UpdateFlagsI(res, sizecode);

            return true;
        }
        /* -- XOR --

        Description:
            Computes the logical exclusive or of two unsigned integers.
            The destination must be a register, but the value to add may be an imm, reg, or value from memory.

        Operation:
            byte:  byte  dest <- byte  dest xor byte  val
            word:  word  dest <- word  dest xor word  val
            dword: dword dest <- dword dest xor dword val
            qword: qword dest <- qword dest xor qword val

        Flags Affected:
            ZF is set if the result is zero, and cleared otherwise.
            SF is set if the result is negative (high bit set), and cleared otherwise.
            PF is set if the result has even parity in the low 8 bits, and cleared otherwise.

        OPCode Format: [8: xor]   [4: dest][2: size][2: mode]   (mode = 0: [size: imm]   mode = 1: [4:][4: r]   mode = 2: [address]   mode = 3: UND)
        */
        private bool ProcessXor()
        {
            UInt64 s = 0, a = 0, b = 0;
            if (!ParseBinaryOpFormat(ref s, ref a, ref b)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UInt64 res = a ^ b;
            Registers[s >> 4].Set(sizecode, res);

            UpdateFlagsI(res, sizecode);

            return true;
        }

        /* -- INC --

        Description:
            Equivalent to using ADD with a value of 1.

        Operation:
            byte:  byte  dest <- byte  dest + 1
            word:  word  dest <- word  dest + 1
            dword: dword dest <- dword dest + 1
            qword: qword dest <- qword dest + 1

        Flags Affected:
            Equivalent to using ADD with a value of 1

        OPCode Format: [8: inc]   [4: dest][2:][2: size]
        */
        private bool ProcessInc()
        {
            UInt64 s = 0, a = 0;
            if (!ParseUnaryOpFormat(ref s, ref a)) return false;
            UInt64 sizecode = s & 3;

            UInt64 res = Truncate(a + 1, sizecode);
            Registers[s >> 4].Set(sizecode, res);

            UpdateFlagsI(res, sizecode);
            Flags.C = res == 0; // carry results in zero
            Flags.O = Positive(a, sizecode) && Negative(res, sizecode); // + -> - is overflow

            return true;
        }
        /* -- INC --

        Description:
            Equivalent to using SUB with a value of 1.

        Operation:
            byte:  byte  dest <- byte  dest - 1
            word:  word  dest <- word  dest - 1
            dword: dword dest <- dword dest - 1
            qword: qword dest <- qword dest - 1

        Flags Affected:
            Equivalent to using SUB with a value of 1

        OPCode Format: [8: dec]   [4: dest][2:][2: size]
        */
        private bool ProcessDec()
        {
            UInt64 s = 0, a = 0;
            if (!ParseUnaryOpFormat(ref s, ref a)) return false;
            UInt64 sizecode = s & 3;

            UInt64 res = Truncate(a - 1, sizecode);
            Registers[s >> 4].Set(sizecode, res);

            UpdateFlagsI(res, sizecode);
            Flags.C = a == 0; // a = 0 results in borrow from high bit (carry)
            Flags.O = Negative(a, sizecode) && Positive(res, sizecode); // - -> + is overflow

            return true;
        }
        /* -- NOT --

        Description:
            Computes the logical not of an unsigned integer.
            Must be performed on a register.

        Operation:
            byte:  byte  dest <- ~ byte  dest
            word:  word  dest <- ~ word  dest
            dword: dword dest <- ~ dword dest
            qword: qword dest <- ~ qword dest

        Flags Affected:
            ZF is set if the result is zero, and cleared otherwise.
            SF is set if the result is negative (high bit set), and cleared otherwise.
            PF is set if the result has even parity in the low 8 bits, and cleared otherwise.

        OPCode Format: [8: not]   [4: dest][2:][2: size]
        */
        private bool ProcessNot()
        {
            UInt64 s = 0, a = 0;
            if (!ParseUnaryOpFormat(ref s, ref a)) return false;
            UInt64 sizecode = s & 3;

            UInt64 res = Truncate(~a, sizecode);
            Registers[s >> 4].Set(sizecode, res);

            UpdateFlagsI(res, sizecode);

            return true;
        }
        /* -- NEG --

        Description:
            Computes the negative of a signed integer.
            Must be performed on a register.

        Operation:
            byte:  byte  dest <- - byte  dest
            word:  word  dest <- - word  dest
            dword: dword dest <- - dword dest
            qword: qword dest <- - qword dest

        Flags Affected:
            ZF is set if the result is zero, and cleared otherwise.
            SF is set if the result is negative (high bit set), and cleared otherwise.
            PF is set if the result has even parity in the low 8 bits, and cleared otherwise.

        OPCode Format: [8: neg]   [4: dest][2:][2: size]
        */
        private bool ProcessNeg()
        {
            UInt64 s = 0, a = 0;
            if (!ParseUnaryOpFormat(ref s, ref a)) return false;
            UInt64 sizecode = s & 3;

            UInt64 res = Truncate(~a + 1, sizecode);
            Registers[s >> 4].Set(sizecode, res);

            UpdateFlagsI(res, sizecode);

            return true;
        }
        /* -- ABS --

        Description:
            Computes the absolute value of a signed integer.
            Must be performed on a register.
            Because 2's complement has more negative numbers than positive numbers, the result may be negative (e.g. byte -128 -> byte -128).

        Operation:
            byte:  byte  dest <- |byte  dest|
            word:  word  dest <- |word  dest|
            dword: dword dest <- |dword dest|
            qword: qword dest <- |qword dest|

        Flags Affected:
            ZF is set if the result is zero, and cleared otherwise.
            SF is set if the result is negative (high bit set), and cleared otherwise.
            PF is set if the result has even parity in the low 8 bits, and cleared otherwise.

        OPCode Format: [8: abs]   [4: dest][2:][2: size]
        */
        private bool ProcessAbs()
        {
            UInt64 s = 0, a = 0;
            if (!ParseUnaryOpFormat(ref s, ref a)) return false;
            UInt64 sizecode = s & 3;

            UInt64 res = Positive(a, sizecode) ? a : Truncate(~a + 1, sizecode);
            Registers[s >> 4].Set(sizecode, res);

            UpdateFlagsI(res, sizecode);

            return true;
        }
        /* -- CMP0 --

        Description:
            Equivalent to comparing a register to an integral 0.

        OPCode Format: [8: cmp0]   [4: dest][2:][2: size]
        */
        private bool ProcessCmp0()
        {
            UInt64 s = 0, a = 0;
            if (!ParseUnaryOpFormat(ref s, ref a)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UpdateFlagsI(a, sizecode);
            Flags.C = Flags.O = false;

            return true;
        }

        // -- floatint point ops --

        /* -- FADD --

        Description:
            Computes the addition of two floating point numbers.
            The destination must be a register, but the value to add may be an imm, reg, or value from memory.

        Operation: qword dest <- qword dest + qword val

        Flags Affected:
            ZF is set if the result is zero, and cleared otherwise.
            SF is set if the result is negative, and cleared otherwise.
            OF is set if the result is positive or negative infinity, and cleared otherwise.
            CF is set if the result is nan, and cleared otherwise.

        OPCode Format: [8: fadd]   [4: dest][2:][2: mode]   (mode = 0: [64: imm]   mode = 1: [4:][4: r]   mode = 2: [address]   mode = 3: UND)
        */
        private bool ProcessFadd()
        {
            UInt64 s = 0, a = 0, b = 0;
            if (!ParseBinaryOpFormat(ref s, ref a, ref b)) return false;

            double res = AsDouble(a) + AsDouble(b);
            Registers[s >> 4].x64 = AsUInt64(res);

            UpdateFlagsF(res);

            return true;
        }
        /* -- FSUB --

        Description:
            Computes the subtraction of two floating point numbers.
            The destination must be a register, but the value to add may be an imm, reg, or value from memory.

        Operation: qword dest <- qword dest - qword val

        Flags Affected:
            ZF is set if the result is zero, and cleared otherwise.
            SF is set if the result is negative, and cleared otherwise.
            OF is set if the result is positive or negative infinity, and cleared otherwise.
            CF is set if the result is nan, and cleared otherwise.

        OPCode Format: [8: fsub]   [4: dest][2:][2: mode]   (mode = 0: [64: imm]   mode = 1: [4:][4: r]   mode = 2: [address]   mode = 3: UND)
        */
        private bool ProcessFsub(bool apply = true)
        {
            UInt64 s = 0, a = 0, b = 0;
            if (!ParseBinaryOpFormat(ref s, ref a, ref b)) return false;

            double res = AsDouble(a) - AsDouble(b);
            if (apply) Registers[s >> 4].x64 = AsUInt64(res);

            UpdateFlagsF(res);

            return true;
        }

        /* -- FMUL --

        Description:
            Computes the multiplication of two floating point numbers.
            The destination must be a register, but the value to add may be an imm, reg, or value from memory.

        Operation: qword dest <- qword dest * qword val

        Flags Affected:
            ZF is set if the result is zero, and cleared otherwise.
            SF is set if the result is negative, and cleared otherwise.
            OF is set if the result is positive or negative infinity, and cleared otherwise.
            CF is set if the result is nan, and cleared otherwise.

        OPCode Format: [8: fmul]   [4: dest][2:][2: mode]   (mode = 0: [64: imm]   mode = 1: [4:][4: r]   mode = 2: [address]   mode = 3: UND)
        */
        private bool ProcessFmul()
        {
            UInt64 s = 0, a = 0, b = 0;
            if (!ParseBinaryOpFormat(ref s, ref a, ref b)) return false;

            double res = AsDouble(a) * AsDouble(b);
            Registers[s >> 4].x64 = AsUInt64(res);

            UpdateFlagsF(res);

            return true;
        }

        /* -- FDIV --

        Description:
            Computes the division of two floating point numbers.
            The destination must be a register, but the value to add may be an imm, reg, or value from memory.

        Operation: qword dest <- qword dest / qword val

        Flags Affected:
            ZF is set if the result is zero, and cleared otherwise.
            SF is set if the result is negative, and cleared otherwise.
            OF is set if the result is positive or negative infinity, and cleared otherwise.
            CF is set if the result is nan, and cleared otherwise.

        OPCode Format: [8: fdiv]   [4: dest][2:][2: mode]   (mode = 0: [64: imm]   mode = 1: [4:][4: r]   mode = 2: [address]   mode = 3: UND)
        */
        private bool ProcessFdiv()
        {
            UInt64 s = 0, a = 0, b = 0;
            if (!ParseBinaryOpFormat(ref s, ref a, ref b)) return false;

            double res = AsDouble(a) / AsDouble(b);
            Registers[s >> 4].x64 = AsUInt64(res);

            UpdateFlagsF(res);

            return true;
        }
        /* -- FMOD --

        Description:
            Computes the remainder of division of two floating point numbers.
            The destination must be a register, but the value to add may be an imm, reg, or value from memory.

        Operation: qword dest <- qword dest % qword val

        Flags Affected:
            ZF is set if the result is zero, and cleared otherwise.
            SF is set if the result is negative, and cleared otherwise.
            OF is set if the result is positive or negative infinity, and cleared otherwise.
            CF is set if the result is nan, and cleared otherwise.

        OPCode Format: [8: fmod]   [4: dest][2:][2: mode]   (mode = 0: [64: imm]   mode = 1: [4:][4: r]   mode = 2: [address]   mode = 3: UND)
        */
        private bool ProcessFmod()
        {
            UInt64 s = 0, a = 0, b = 0;
            if (!ParseBinaryOpFormat(ref s, ref a, ref b)) return false;

            double res = AsDouble(a) % AsDouble(b);
            Registers[s >> 4].x64 = AsUInt64(res);

            UpdateFlagsF(res);

            return true;
        }

        /* -- FPOW --

        Description:
            Computes the exponentiation of a floating point vlue by a floating point value.
            The destination must be a register, but the value to add may be an imm, reg, or value from memory.

        Operation: qword dest <- qword dest ^ qword val

        Flags Affected:
            ZF is set if the result is zero, and cleared otherwise.
            SF is set if the result is negative, and cleared otherwise.
            OF is set if the result is positive or negative infinity, and cleared otherwise.
            CF is set if the result is nan, and cleared otherwise.

        OPCode Format: [8: fpow]   [4: dest][2:][2: mode]   (mode = 0: [64: imm]   mode = 1: [4:][4: r]   mode = 2: [address]   mode = 3: UND)
        */
        private bool ProcessFpow()
        {
            UInt64 s = 0, a = 0, b = 0;
            if (!ParseBinaryOpFormat(ref s, ref a, ref b)) return false;

            double res = Math.Pow(AsDouble(a), AsDouble(b));
            Registers[s >> 4].x64 = AsUInt64(res);

            UpdateFlagsF(res);

            return true;
        }
        /* -- FSQRT --

        Description:
            Computes the square root of a floating point value.
            Must be performed on a register.

        Operation: qword dest <- sqrt(qword dest)

        Flags Affected:
            ZF is set if the result is zero, and cleared otherwise.
            SF is set if the result is negative, and cleared otherwise.
            OF is set if the result is positive or negative infinity, and cleared otherwise.
            CF is set if the result is nan, and cleared otherwise.

        OPCode Format: [8: fsqrt]   [4: dest][2:][2: size]
        */
        private bool ProcessFsqrt()
        {
            UInt64 s = 0, a = 0;
            if (!ParseUnaryOpFormat(ref s, ref a)) return false;

            double res = Math.Sqrt(AsDouble(a));
            Registers[s >> 4].x64 = AsUInt64(res);

            UpdateFlagsF(res);

            return true;
        }
        /* -- FEXP --

        Description:
            Computes e raised to a floating point value.
            Must be performed on a register.

        Operation: qword dest <- e ^ qword dest

        Flags Affected:
            ZF is set if the result is zero, and cleared otherwise.
            SF is set if the result is negative, and cleared otherwise.
            OF is set if the result is positive or negative infinity, and cleared otherwise.
            CF is set if the result is nan, and cleared otherwise.

        OPCode Format: [8: fexp]   [4: dest][2:][2: size]
        */
        private bool ProcessFexp()
        {
            UInt64 s = 0, a = 0;
            if (!ParseUnaryOpFormat(ref s, ref a)) return false;

            double res = Math.Exp(AsDouble(a));
            Registers[s >> 4].x64 = AsUInt64(res);

            UpdateFlagsF(res);

            return true;
        }
        /* -- FLN --

        Description:
            Computes the natural logarithm (base e) of a floating point value.
            Must be performed on a register.

        Operation: qword dest <- ln(qword dest)

        Flags Affected:
            ZF is set if the result is zero, and cleared otherwise.
            SF is set if the result is negative, and cleared otherwise.
            OF is set if the result is positive or negative infinity, and cleared otherwise.
            CF is set if the result is nan, and cleared otherwise.

        OPCode Format: [8: fln]   [4: dest][2:][2: size]
        */
        private bool ProcessFln()
        {
            UInt64 s = 0, a = 0;
            if (!ParseUnaryOpFormat(ref s, ref a)) return false;

            double res = Math.Log(AsDouble(a));
            Registers[s >> 4].x64 = AsUInt64(res);

            UpdateFlagsF(res);

            return true;
        }
        /* -- FABS --

        Description:
            Computes the absolute value of a floating point value.
            Must be performed on a register.

        Operation: qword dest <- |qword dest|

        Flags Affected:
            ZF is set if the result is zero, and cleared otherwise.
            SF is set if the result is negative, and cleared otherwise.
            OF is set if the result is positive or negative infinity, and cleared otherwise.
            CF is set if the result is nan, and cleared otherwise.

        OPCode Format: [8: fabs]   [4: dest][2:][2: size]
        */
        private bool ProcessFabs()
        {
            UInt64 s = 0, a = 0;
            if (!ParseUnaryOpFormat(ref s, ref a)) return false;

            double res = Math.Abs(AsDouble(a));
            Registers[s >> 4].x64 = AsUInt64(res);

            UpdateFlagsF(res);

            return true;
        }
        /* -- FCMP0 --

        Description:
            Equivalent to a floating point comparison to 0.0.

        OPCode Format: [8: fcmp0]   [4: dest][2:][2: size]
        */
        private bool ProcessFcmp0()
        {
            UInt64 s = 0, a = 0;
            if (!ParseUnaryOpFormat(ref s, ref a)) return false;

            double res = AsDouble(a);

            UpdateFlagsF(res);

            return true;
        }

        /* -- FSIN --

        Description:
            Computes the sine of a floating point value.
            Must be performed on a register.
            Angles are in radians.

        Operation: qword dest <- sin(qword dest)

        Flags Affected:
            ZF is set if the result is zero, and cleared otherwise.
            SF is set if the result is negative, and cleared otherwise.
            OF is set if the result is positive or negative infinity, and cleared otherwise.
            CF is set if the result is nan, and cleared otherwise.

        OPCode Format: [8: fsin]   [4: dest][2:][2: size]
        */
        private bool ProcessFsin()
        {
            UInt64 s = 0, a = 0;
            if (!ParseUnaryOpFormat(ref s, ref a)) return false;

            double res = Math.Sin(AsDouble(a));
            Registers[s >> 4].x64 = AsUInt64(res);

            UpdateFlagsF(res);

            return true;
        }
        /* -- FCOS --

        Description:
            Computes the cosine of a floating point value.
            Must be performed on a register.
            Angles are in radians.

        Operation: qword dest <- cos(qword dest)

        Flags Affected:
            ZF is set if the result is zero, and cleared otherwise.
            SF is set if the result is negative, and cleared otherwise.
            OF is set if the result is positive or negative infinity, and cleared otherwise.
            CF is set if the result is nan, and cleared otherwise.

        OPCode Format: [8: fcos]   [4: dest][2:][2: size]
        */
        private bool ProcessFcos()
        {
            UInt64 s = 0, a = 0;
            if (!ParseUnaryOpFormat(ref s, ref a)) return false;

            double res = Math.Cos(AsDouble(a));
            Registers[s >> 4].x64 = AsUInt64(res);

            UpdateFlagsF(res);

            return true;
        }
        /* -- FTAN --

        Description:
            Computes the tangent of a floating point value.
            Must be performed on a register.
            Angles are in radians.

        Operation: qword dest <- tan(qword dest)

        Flags Affected:
            ZF is set if the result is zero, and cleared otherwise.
            SF is set if the result is negative, and cleared otherwise.
            OF is set if the result is positive or negative infinity, and cleared otherwise.
            CF is set if the result is nan, and cleared otherwise.

        OPCode Format: [8: ftan]   [4: dest][2:][2: size]
        */
        private bool ProcessFtan()
        {
            UInt64 s = 0, a = 0;
            if (!ParseUnaryOpFormat(ref s, ref a)) return false;

            double res = Math.Tan(AsDouble(a));
            Registers[s >> 4].x64 = AsUInt64(res);

            UpdateFlagsF(res);

            return true;
        }

        /* -- FSINH --

        Description:
            Computes the hyperbolic sine of a floating point value.
            Must be performed on a register.
            Angles are in radians.

        Operation: qword dest <- sinh(qword dest)

        Flags Affected:
            ZF is set if the result is zero, and cleared otherwise.
            SF is set if the result is negative, and cleared otherwise.
            OF is set if the result is positive or negative infinity, and cleared otherwise.
            CF is set if the result is nan, and cleared otherwise.

        OPCode Format: [8: fsinh]   [4: dest][2:][2: size]
        */
        private bool ProcessFsinh()
        {
            UInt64 s = 0, a = 0;
            if (!ParseUnaryOpFormat(ref s, ref a)) return false;

            double res = Math.Sinh(AsDouble(a));
            Registers[s >> 4].x64 = AsUInt64(res);

            UpdateFlagsF(res);

            return true;
        }
        /* -- FCOSH --

        Description:
            Computes the hyperbolic cosine of a floating point value.
            Must be performed on a register.
            Angles are in radians.

        Operation: qword dest <- cosh(qword dest)

        Flags Affected:
            ZF is set if the result is zero, and cleared otherwise.
            SF is set if the result is negative, and cleared otherwise.
            OF is set if the result is positive or negative infinity, and cleared otherwise.
            CF is set if the result is nan, and cleared otherwise.

        OPCode Format: [8: fcosh]   [4: dest][2:][2: size]
        */
        private bool ProcessFcosh()
        {
            UInt64 s = 0, a = 0;
            if (!ParseUnaryOpFormat(ref s, ref a)) return false;

            double res = Math.Cosh(AsDouble(a));
            Registers[s >> 4].x64 = AsUInt64(res);

            UpdateFlagsF(res);

            return true;
        }
        /* -- FTANH --

        Description:
            Computes the hyperbolic tangent of a floating point value.
            Must be performed on a register.
            Angles are in radians.

        Operation: qword dest <- tanh(qword dest)

        Flags Affected:
            ZF is set if the result is zero, and cleared otherwise.
            SF is set if the result is negative, and cleared otherwise.
            OF is set if the result is positive or negative infinity, and cleared otherwise.
            CF is set if the result is nan, and cleared otherwise.

        OPCode Format: [8: ftanh]   [4: dest][2:][2: size]
        */
        private bool ProcessFtanh()
        {
            UInt64 s = 0, a = 0;
            if (!ParseUnaryOpFormat(ref s, ref a)) return false;

            double res = Math.Tanh(AsDouble(a));
            Registers[s >> 4].x64 = AsUInt64(res);

            UpdateFlagsF(res);

            return true;
        }

        /* -- FASIN --

        Description:
            Computes the arcsine of a floating point value.
            Must be performed on a register.
            Angles are in radians.

        Operation: qword dest <- asin(qword dest)

        Flags Affected:
            ZF is set if the result is zero, and cleared otherwise.
            SF is set if the result is negative, and cleared otherwise.
            OF is set if the result is positive or negative infinity, and cleared otherwise.
            CF is set if the result is nan, and cleared otherwise.

        OPCode Format: [8: fasin]   [4: dest][2:][2: size]
        */
        private bool ProcessFasin()
        {
            UInt64 s = 0, a = 0;
            if (!ParseUnaryOpFormat(ref s, ref a)) return false;

            double res = Math.Asin(AsDouble(a));
            Registers[s >> 4].x64 = AsUInt64(res);

            UpdateFlagsF(res);

            return true;
        }
        /* -- FACOS --

        Description:
            Computes the arccosine of a floating point value.
            Must be performed on a register.
            Angles are in radians.

        Operation: qword dest <- acos(qword dest)

        Flags Affected:
            ZF is set if the result is zero, and cleared otherwise.
            SF is set if the result is negative, and cleared otherwise.
            OF is set if the result is positive or negative infinity, and cleared otherwise.
            CF is set if the result is nan, and cleared otherwise.

        OPCode Format: [8: facos]   [4: dest][2:][2: size]
        */
        private bool ProcessFacos()
        {
            UInt64 s = 0, a = 0;
            if (!ParseUnaryOpFormat(ref s, ref a)) return false;

            double res = Math.Acos(AsDouble(a));
            Registers[s >> 4].x64 = AsUInt64(res);

            UpdateFlagsF(res);

            return true;
        }
        /* -- FATAN --

        Description:
            Computes the arctangent of a floating point value.
            Must be performed on a register.
            Angles are in radians.

        Operation: qword dest <- atan(qword dest)

        Flags Affected:
            ZF is set if the result is zero, and cleared otherwise.
            SF is set if the result is negative, and cleared otherwise.
            OF is set if the result is positive or negative infinity, and cleared otherwise.
            CF is set if the result is nan, and cleared otherwise.

        OPCode Format: [8: fatan]   [4: dest][2:][2: size]
        */
        private bool ProcessFatan()
        {
            UInt64 s = 0, a = 0;
            if (!ParseUnaryOpFormat(ref s, ref a)) return false;

            double res = Math.Atan(AsDouble(a));
            Registers[s >> 4].x64 = AsUInt64(res);

            UpdateFlagsF(res);

            return true;
        }
        /* -- FATAN2 --

        Description:
            Computes the arctangent of two floating point values, where y = dest and x = val
            The destination must be a register, but the value to add may be an imm, reg, or value from memory.
            Angles are in radians.

        Operation: qword dest <- atan2(qword dest, qword val)

        Flags Affected:
            ZF is set if the result is zero, and cleared otherwise.
            SF is set if the result is negative, and cleared otherwise.
            OF is set if the result is positive or negative infinity, and cleared otherwise.
            CF is set if the result is nan, and cleared otherwise.

        OPCode Format: [8: fatan2]   [4: dest][2:][2: mode]   (mode = 0: [64: imm]   mode = 1: [4:][4: r]   mode = 2: [address]   mode = 3: UND)
        */
        private bool ProcessFatan2()
        {
            UInt64 s = 0, a = 0, b = 0;
            if (!ParseBinaryOpFormat(ref s, ref a, ref b)) return false;

            double res = Math.Atan2(AsDouble(a), AsDouble(b));
            Registers[s >> 4].x64 = AsUInt64(res);

            UpdateFlagsF(res);

            return true;
        }

        /* -- FFLOOR --

        Description:
            Computes the floor of a floating point value.
            Must be performed on a register.
            Result is still floating point.

        Operation: qword dest <- floor(qword dest)

        Flags Affected:
            ZF is set if the result is zero, and cleared otherwise.
            SF is set if the result is negative, and cleared otherwise.
            OF is set if the result is positive or negative infinity, and cleared otherwise.
            CF is set if the result is nan, and cleared otherwise.

        OPCode Format: [8: ffloor]   [4: dest][2:][2: size]
        */
        private bool ProcessFfloor()
        {
            UInt64 s = 0, a = 0;
            if (!ParseUnaryOpFormat(ref s, ref a)) return false;

            double res = Math.Floor(AsDouble(a));
            Registers[s >> 4].x64 = AsUInt64(res);

            UpdateFlagsF(res);

            return true;
        }
        /* -- FCEIL --

        Description:
            Computes the ceiling of a floating point value.
            Must be performed on a register.
            Result is still floating point.

        Operation: qword dest <- ceil(qword dest)

        Flags Affected:
            ZF is set if the result is zero, and cleared otherwise.
            SF is set if the result is negative, and cleared otherwise.
            OF is set if the result is positive or negative infinity, and cleared otherwise.
            CF is set if the result is nan, and cleared otherwise.

        OPCode Format: [8: fceil]   [4: dest][2:][2: size]
        */
        private bool ProcessFceil()
        {
            UInt64 s = 0, a = 0;
            if (!ParseUnaryOpFormat(ref s, ref a)) return false;

            double res = Math.Ceiling(AsDouble(a));
            Registers[s >> 4].x64 = AsUInt64(res);

            UpdateFlagsF(res);

            return true;
        }
        /* -- FROUND --

        Description:
            Rounds the floating point value to the nearest integer.
            Must be performed on a register.
            Result is still floating point.

        Operation: qword dest <- round(qword dest)

        Flags Affected:
            ZF is set if the result is zero, and cleared otherwise.
            SF is set if the result is negative, and cleared otherwise.
            OF is set if the result is positive or negative infinity, and cleared otherwise.
            CF is set if the result is nan, and cleared otherwise.

        OPCode Format: [8: fround]   [4: dest][2:][2: size]
        */
        private bool ProcessFround()
        {
            UInt64 s = 0, a = 0;
            if (!ParseUnaryOpFormat(ref s, ref a)) return false;

            double res = Math.Round(AsDouble(a));
            Registers[s >> 4].x64 = AsUInt64(res);

            UpdateFlagsF(res);

            return true;
        }
        /* -- FTRUNC --

        Description:
            Rounds a floating point value toward zero by removing the decimal portion.
            Must be performed on a register.
            Result is still floating point.

        Operation: qword dest <- trunc(qword dest)

        Flags Affected:
            ZF is set if the result is zero, and cleared otherwise.
            SF is set if the result is negative, and cleared otherwise.
            OF is set if the result is positive or negative infinity, and cleared otherwise.
            CF is set if the result is nan, and cleared otherwise.

        OPCode Format: [8: ftrunc]   [4: dest][2:][2: size]
        */
        private bool ProcessFtrunc()
        {
            UInt64 s = 0, a = 0;
            if (!ParseUnaryOpFormat(ref s, ref a)) return false;

            double res = Math.Truncate(AsDouble(a));
            Registers[s >> 4].x64 = AsUInt64(res);

            UpdateFlagsF(res);

            return true;
        }

        /* -- FTOI --

        Description:
            Converts a floating point value into a signed integer after truncation.
            Must be performed on a register.

        Operation: qword dest <- qword dest as integer

        Flags Affected: None

        OPCode Format: [8: fround]   [4: dest][2:][2: size]
        */
        private bool ProcessFTOI()
        {
            UInt64 s = 0, a = 0;
            if (!ParseUnaryOpFormat(ref s, ref a)) return false;

            Registers[s >> 4].x64 = ((Int64)a).MakeUnsigned();

            return true;
        }
        /* -- ITOF --

        Description:
            Converts a signed integer into a floating point value.
            Must be performed on a register.

        Operation: qword dest <- qword dest as floating point

        Flags Affected: None

        OPCode Format: [8: fround]   [4: dest][2:][2: size]
        */
        private bool ProcessITOF()
        {
            UInt64 s = 0, a = 0;
            if (!ParseUnaryOpFormat(ref s, ref a)) return false;

            Registers[s >> 4].x64 = AsUInt64((double)a.MakeSigned());

            return true;
        }

        // -- extended register ops --

        /* -- UMUL --

            Description:
                Computes the full product of multiplying two unsigned values.

            Operation:
                byte:  word      R0 <- byte  R0 * byte  val
                word:  dword     R0 <- word  R0 * word  val
                dword: qword     R0 <- dword R0 * dword val
                qword: dqword R1:R0 <- qword R0 * qword val

            Flags Affected:
                OF and CF are both set if the high bits of the product are non-zero, and cleard otherwise.

            OPCode Format: [8: umul]   [4: reg][2: size][2: mode]   (mode = 0: [size: imm]   mode = 1: use reg   mode = 2: [address]   mode = 3: UND)
        */
        private bool ProcessUMUL()
        {
            UInt64 a = 0, b = 0;

            if (!GetMem(1, ref a)) return false;

            // get the value into b
            switch (a & 3)
            {
                case 0: if (!GetMem(Size((a >> 2) & 3), ref b)) return false; break;
                case 1: b = Registers[a >> 4].Get((a >> 2) & 3); break;
                case 2: if (!GetAddress(ref b) || !GetMem(b, Size((a >> 2) & 3), ref b)) return false; break;
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
        /* -- SMUL --

            Description:
                Computes the full product of multiplying two signed values.

            Operation:
                byte:  word      R0 <- byte  R0 * byte  val
                word:  dword     R0 <- word  R0 * word  val
                dword: qword     R0 <- dword R0 * dword val
                qword: dqword R1:R0 <- qword R0 * qword val

            Flags Affected:
                OF and CF are both set if there are significant bits in the high portion of the product (i.e. truncation yields a different value), and cleared otherwise.
                SF is set if the resulting value is negative (i.e. the highest bit of the result is set).

            OPCode Format: [8: umul]   [4: reg][2: size][2: mode]   (mode = 0: [size: imm]   mode = 1: use reg   mode = 2: [address]   mode = 3: UND)
        */
        private bool ProcessSMUL()
        {
            UInt64 a = 0, b = 0;

            if (!GetMem(1, ref a)) return false;

            // get the value into b
            switch (a & 3)
            {
                case 0: if (!GetMem(Size((a >> 2) & 3), ref b)) return false; break;
                case 1: b = Registers[a >> 4].Get((a >> 2) & 3); break;
                case 2: if (!GetAddress(ref b) || !GetMem(b, Size((a >> 2) & 3), ref b)) return false; break;
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

        /* -- UDIV --

            Description:
                Computes the quotient and remainder of the division of two unsigned values.
                Dividing by zero or producing a quotient that cannot be stored in the operand register produces an arithmetic error.

            Operation:
                byte:
                    word R0 / byte val
                    byte R0 <- quotient
                    byte R1 <- remainder
                word:
                    dword R0 / word val
                    word  R0 <- quotient
                    word  R1 <- remainder
                dword:
                    qword R0 / dword val
                    dword R0 <- quotient
                    dword R1 <- remainder
                qword:
                    dqword R1:R0 / qword val
                    qword  R0 <- quotient
                    qword  R1 <- remainder

            Flags Affected:
                CF is set if the resulting remainder is nonzero, and cleared otherwise.

            OPCode Format: [8: udiv]   [4: reg][2: size][2: mode]   (mode = 0: [size: imm]   mode = 1: use reg   mode = 2: [address]   mode = 3: UND)
        */
        private bool ProcessUDIV()
        {
            UInt64 a = 0, b = 0, full;
            BigInteger bigraw, bigfull;

            if (!GetMem(1, ref a)) return false;

            // get the value into b
            switch (a & 3)
            {
                case 0: if (!GetMem(Size((a >> 2) & 3), ref b)) return false; break;
                case 1: b = Registers[a >> 4].Get((a >> 2) & 3); break;
                case 2: if (!GetAddress(ref b) || !GetMem(b, Size((a >> 2) & 3), ref b)) return false; break;
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
        /* -- UDIV --

            Description:
                Computes the quotient and remainder of the division of two signed values.
                Dividing by zero or producing a quotient that cannot be stored in the operand register produces an arithmetic error.

            Operation:
                byte:
                    word R0 / byte val
                    byte R0 <- quotient
                    byte R1 <- remainder
                word:
                    dword R0 / word val
                    word  R0 <- quotient
                    word  R1 <- remainder
                dword:
                    qword R0 / dword val
                    dword R0 <- quotient
                    dword R1 <- remainder
                qword:
                    dqword R1:R0 / qword val
                    qword  R0 <- quotient
                    qword  R1 <- remainder

            Flags Affected:
                CF is set if the resulting remainder is nonzero, and cleared otherwise.
                SF is set if the resulting quotient is negative (i.e. highest bit of quotient is set), and cleared otherwise

            OPCode Format: [8: sdiv]   [4: reg][2: size][2: mode]   (mode = 0: [size: imm]   mode = 1: use reg   mode = 2: [address]   mode = 3: UND)
        */
        private bool ProcessSDIV()
        {
            UInt64 a = 0, b = 0;
            Int64 _a, _b, full;
            BigInteger bigraw, bigfull;

            if (!GetMem(1, ref a)) return false;

            // get the value into b
            switch (a & 3)
            {
                case 0: if (!GetMem(Size((a >> 2) & 3), ref b)) return false; break;
                case 1: b = Registers[a >> 4].Get((a >> 2) & 3); break;
                case 2: if (!GetAddress(ref b) || !GetMem(b, Size((a >> 2) & 3), ref b)) return false; break;
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

        #endregion

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

                case OPCode.UMUL: return ProcessUMUL();
                case OPCode.SMUL: return ProcessSMUL();
                case OPCode.UDIV: return ProcessUDIV();
                case OPCode.SDIV: return ProcessSDIV();

                case OPCode.Add:   return ProcessAdd();
                case OPCode.Sub:   return ProcessSub();
                case OPCode.Bmul:  return ProcessBmul();
                case OPCode.Budiv: return ProcessBudiv();
                case OPCode.Bumod: return ProcessBumod();
                case OPCode.Bsdiv: return ProcessBsdiv();
                case OPCode.Bsmod: return ProcessBsmod();

                case OPCode.SL: return ProcessSL();
                case OPCode.SR: return ProcessSR();
                case OPCode.SAL: return ProcessSAL();
                case OPCode.SAR: return ProcessSAR();
                case OPCode.RL: return ProcessRL();
                case OPCode.RR: return ProcessRR();

                case OPCode.And: return ProcessAnd();
                case OPCode.Or:  return ProcessOr();
                case OPCode.Xor: return ProcessXor();

                case OPCode.Cmp:  return ProcessSub(false);
                case OPCode.Test: return ProcessAnd(false);

                case OPCode.Inc: return ProcessInc();
                case OPCode.Dec: return ProcessDec();
                case OPCode.Not: return ProcessNot();
                case OPCode.Neg: return ProcessNeg();
                case OPCode.Abs: return ProcessAbs();
                case OPCode.Cmp0: return ProcessCmp0();
                
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

                case OPCode.Fadd: return ProcessFadd();
                case OPCode.Fsub: return ProcessFsub();
                case OPCode.Fmul: return ProcessFmul();
                case OPCode.Fdiv: return ProcessFdiv();
                case OPCode.Fmod: return ProcessFmod();

                case OPCode.Fpow: return ProcessFpow();
                case OPCode.Fsqrt: return ProcessFsqrt();
                case OPCode.Fexp: return ProcessFexp();
                case OPCode.Fln: return ProcessFln();
                case OPCode.Fabs: return ProcessFabs();
                case OPCode.Fcmp0: return ProcessFcmp0();

                case OPCode.Fsin: return ProcessFsin();
                case OPCode.Fcos: return ProcessFcos();
                case OPCode.Ftan: return ProcessFtan();

                case OPCode.Fsinh: return ProcessFsinh();
                case OPCode.Fcosh: return ProcessFcosh();
                case OPCode.Ftanh: return ProcessFtanh();

                case OPCode.Fasin: return ProcessFasin();
                case OPCode.Facos: return ProcessFacos();
                case OPCode.Fatan: return ProcessFatan();
                case OPCode.Fatan2: return ProcessFatan2();

                case OPCode.Ffloor: return ProcessFfloor();
                case OPCode.Fceil: return ProcessFceil();
                case OPCode.Fround: return ProcessFround();
                case OPCode.Ftrunc: return ProcessFtrunc();

                case OPCode.Fcmp: return ProcessFsub(false);

                case OPCode.FTOI: return ProcessFTOI();
                case OPCode.ITOF: return ProcessITOF();

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
            None, ArgCount, MissingSize, ArgError, FormatError, UsageError, UnknownOp, EmptyFile, InvalidLabel, SymbolRedefinition, UnknownSymbol
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

            public bool Append(ObjectFile file, string sub, string last_static_label, int line, ref Tuple<AssembleError, string> err)
            {
                string _sub = sub.Length > 0 && sub[0] == '+' || sub[0] == '-' ? sub.Substring(1) : sub;
                if (_sub.Length == 0) { err = new Tuple<AssembleError, string>(AssembleError.InvalidLabel, $"line {line}: Empty label encountered"); return false; }

                // if we can get a value for it, add it
                if (TryParseInstantImm(file, sub, out UInt64 temp, out bool floating, last_static_label, line, ref err))
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
                else
                {
                    if (!MutateLabel(ref _sub, last_static_label, line, ref err)) return false;
                    if (IsValidLabel(_sub)) Segments.Add(new Segment() { Symbol = _sub, IsNegative = sub[0] == '-' });
                    else { err = new Tuple<AssembleError, string>(AssembleError.InvalidLabel, $"line {line}: \"{_sub}\" is not a valid label"); return false; }
                }

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

        private static bool TryParseInstantImm(ObjectFile file, string token, out UInt64 res, out bool floating, string last_static_label, int line, ref Tuple<AssembleError, string> err)
        {
            int pos = 0, end = 0;   // position in token
            UInt64 temp = 0;        // placeholders for parsing
            double ftemp, fsum = 0; // floating point parsing temporary and sum

            // result initially integral zero
            res = 0; 
            floating = false;

            if (token.Length == 0) { err = new Tuple<AssembleError, string>(AssembleError.InvalidLabel, $"line {line}: Empty label encountered"); return false; }

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
                if (_sub.Length == 0) { err = new Tuple<AssembleError, string>(AssembleError.InvalidLabel, $"line {line}: Empty label encountered"); return false; }

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
                    else { err = new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {line}: Unknown numeric literal encountered \"{_sub}\""); return false; }
                    aft:;
                }
                // if it's an instant symbol
                else
                {
                    if (!MutateLabel(ref _sub, last_static_label, line, ref err)) return false;
                    if (file.Symbols.TryGetValue(_sub, out Symbol symbol) && !symbol.IsAddress)
                    {
                        // add depending on floating or not
                        if (symbol.IsFloating) { floating = true; fsum += sub[0] == '-' ? -AsDouble(symbol.Value) : AsDouble(symbol.Value); }
                        else res += sub[0] == '-' ? ~symbol.Value + 1 : symbol.Value;
                    }
                    // otherwise it's a dud
                    else { err = new Tuple<AssembleError, string>(AssembleError.UnknownSymbol, $"line {line}: Undefined instant symbol encountered \"{_sub}\""); return false; }
                }

                // start of next token includes separator
                pos = end;
            }

            // if result is floating, recalculate res as sum of floating and integral components
            if (floating) res = AsUInt64(res + fsum);

            return true;
        }

        private static bool TryParseRegister(ObjectFile file, string token, out UInt64 res, string last_static_label, int line, ref Tuple<AssembleError, string> err)
        {
            res = 0;
            if (token.Length < 2 || token[0] != '$') { err = new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {line}: Invalid register format encountered \"{token}\""); return false; }
            if (!TryParseInstantImm(file, token.Substring(1), out res, out bool floating, last_static_label, line, ref err)) { err = new Tuple<AssembleError, string>(AssembleError.ArgError, $"line{line}: Failed to parse register \"{token}\"\n-> {err.Item2}"); return false; }
            if (floating) { err = new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {line}: Attempt to use floating point value to specify register \"{token}\""); return false; }
            if (res >= 16) { err = new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {line}: Register out of range \"{token}\" -> {res}"); return false; }

            return true;
        }
        private static bool TryParseSizecode(ObjectFile file, string token, out UInt64 res, String last_static_label, int line, ref Tuple<AssembleError, string> err)
        {
            // must be able ti get an instant imm
            if (!TryParseInstantImm(file, token, out res, out bool floating, last_static_label, line, ref err)) { err = new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {line}: Failed to parse size code \"{token}\"\n-> {err.Item2}"); return false; }
            if (floating) { err = new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {line}: Attempt to use floating point value to specify register size \"{token}\" -> {res}"); return false; }

            // convert to size code
            switch (res)
            {
                case 1: res = 0; return true;
                case 2: res = 1; return true;
                case 4: res = 2; return true;
                case 8: res = 3; return true;

                default: err = new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {line}: Invalid register size {res}"); return false;
            }
        }
        private static bool TryParseMultcode(ObjectFile file, string token, out UInt64 res, string last_static_label, int line, ref Tuple<AssembleError, string> err)
        {
            // must be able ti get an instant imm
            if (!TryParseInstantImm(file, token, out res, out bool floating, last_static_label, line, ref err)) { err = new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {line}: Failed to parse multiplier \"{token}\"\n-> {err.Item2}"); return false; }
            if (floating) { err = new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {line}: Attempt to use floating point value to specify size multiplier \"{token}\" -> {res}"); return false; }

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

                default: err = new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {line}: Invalid size multiplier {res}"); return false;
            }
        }

        private static bool TryParseImm(ObjectFile file, string token, out Hole hole, string last_static_label, int line, ref Tuple<AssembleError, string> err)
        {
            int pos = 0, end = 0; // position in token
            hole = new Hole();    // resulting hole

            if (token.Length == 0) { err = new Tuple<AssembleError, string>(AssembleError.InvalidLabel, $"line {line}: Empty label encountered"); return false; }

            while (pos < token.Length)
            {
                // find the next separator
                for (int i = pos + 1; i < token.Length; ++i)
                    if (token[i] == '+' || token[i] == '-') { end = i; break; }
                // if nothing found, end is end of token
                if (pos == end) end = token.Length;

                string sub = token.Substring(pos, end - pos);

                // append subtoken to the hole
                if (!hole.Append(file, sub, last_static_label, line, ref err)) { err = new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {line}: Failed to parse imm \"{token}\"\n-> {err.Item2}"); return false; }

                // start of next token includes separator
                pos = end;
            }

            return true;
        }

        private static bool TryParseAddressReg(ObjectFile file, string token, out UInt64 r, out UInt64 m, string last_static_label, int line, ref Tuple<AssembleError, string> err)
        {
            r = m = 0;

            // remove sign
            string _seg = token[0] == '+' || token[0] == '-' ? token.Substring(1) : token;
            if (_seg.Length == 0) { err = new Tuple<AssembleError, string>(AssembleError.InvalidLabel, $"line {line}: Empty symbol encountered"); return false; }

            // split on multiplication
            string[] _r = _seg.Split(new char[] { '*' });

            // if 1 token, is a register
            if (_r.Length == 1)
            {
                m = 1;
                return TryParseRegister(file, _r[0], out r, last_static_label, line, ref err);
            }
            // if 2 tokens, has a multcode
            else if (_r.Length == 2)
            {
                if ((!TryParseRegister(file, _r[0], out r, last_static_label, line, ref err) || !TryParseMultcode(file, _r[1], out m, last_static_label, line, ref err))
                    && (!TryParseRegister(file, _r[1], out r, last_static_label, line, ref err) || !TryParseMultcode(file, _r[0], out m, last_static_label, line, ref err)))
                { err = new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {line}: Invalid multiplier-register pair encountered \"{token}\""); return false; }
            }
            // otherwise is illegal
            else { err = new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {line}: Cannot use more than one multiplier and one register per address register subtoken"); return false; }

            return true;
        }
        // [1: literal][3: m1][1: -m2][3: m2]   ([4: r1][4: r2])   ([64: imm])
        private static bool TryParseAddress(ObjectFile file, string token, out UInt64 a, out UInt64 b, out Hole hole, string last_static_label, int line, ref Tuple<AssembleError, string> err)
        {
            a = b = 0;
            hole = new Hole();

            // must be of [*] format
            if (token.Length < 3 || token[0] != '[' || token[token.Length - 1] != ']')
                { err = new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {line}: Invalid address format encountered \"{token}\""); return false; }

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
                        else { err = new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {line}: Address cannot have more than one negative register"); return false; }
                    }
                    // otherwise positive
                    else
                    {
                        // if r1 empty, put it there
                        if (r1_seg == null) r1_seg = sub;
                        // otherwise try r2
                        else if (r2_seg == null) r2_seg = sub;
                        // otherwise fail
                        else { err = new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {line}: Address may only use one register"); return false; }
                    }
                }
                // otherwise tack it onto the hole
                else if (!hole.Append(file, sub, last_static_label, line, ref err)) return false;

                // start of next token includes separator
                pos = end;
            }

            // register parsing temporaries
            UInt64 r1 = 0, r2 = 0, m1 = 0, m2 = 0;

            // process regs
            if (r1_seg != null && !TryParseAddressReg(file, r1_seg, out r1, out m1, last_static_label, line, ref err)) return false;
            if (r2_seg != null && !TryParseAddressReg(file, r2_seg, out r2, out m2, last_static_label, line, ref err)) return false;

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
        private static bool MutateLabel(ref string label, string last_static_label, int line, ref Tuple<AssembleError, string> err)
        {
            // if defining a local label
            if (label.Length >= 2 && label[0] == '.')
            {
                if (last_static_label == null) { err = new Tuple<AssembleError, string>(AssembleError.InvalidLabel, $"line {line}: Cannot define a local label before the first static label"); return false; }

                // mutate the label
                label = $"{last_static_label}_{label.Substring(1)}";
            }

            return true;
        }

        public static UInt64 Time()
        {
            return DateTime.UtcNow.Ticks.MakeUnsigned();
        }

        private static bool TryProcessBinaryOp(ObjectFile file, string[] tokens, int line, OPCode op, string last_static_label, ref Tuple<AssembleError, string> err)
        {
            // [8: binary op]   [4: dest][2: size][2: mode]   (mode = 0: [size: imm]   mode = 1: [4:][4: r]   mode = 2: [address]   mode = 3: UND)

            UInt64 a, b, c, d; // parsing temporaries
            Hole hole;

            if (tokens.Length != 4) { err = new Tuple<AssembleError, string>(AssembleError.ArgCount, $"line {line}: Binary OP expected 3 args"); return false; }
            if (!TryParseSizecode(file, tokens[1], out a, last_static_label, line, ref err)) { err = new Tuple<AssembleError, string>(AssembleError.MissingSize, $"line {line}: Binary OP expected size parameter as first arg\n-> {err.Item2}"); return false; }
            if (!TryParseRegister(file, tokens[2], out b, last_static_label, line, ref err)) { err = new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {line}: Binary OP expected register parameter as second arg\n-> {err.Item2}"); return false; }

            Append(file, 1, (UInt64)op);
            if (TryParseImm(file, tokens[3], out hole, last_static_label, line, ref err))
            {
                Append(file, 1, (b << 4) | (a << 2) | 0);
                Append(file, Size(a), hole);
            }
            else if (TryParseRegister(file, tokens[3], out c, last_static_label, line, ref err))
            {
                Append(file, 1, (b << 4) | (a << 2) | 1);
                Append(file, 1, c);
            }
            else if (TryParseAddress(file, tokens[3], out c, out d, out hole, last_static_label, line, ref err))
            {
                Append(file, 1, (b << 4) | (a << 2) | 2);
                AppendAddress(file, c, d, hole);
            }
            else { err = new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {line}: Unknown binary OP format"); return false; }

            return true;
        }
        private static bool TryProcessUnaryOp(ObjectFile file, string[] tokens, int line, OPCode op, string last_static_label, ref Tuple<AssembleError, string> err)
        {
            // [8: unary op]   [4: dest][2:][2: size]

            if (tokens.Length != 3) { err = new Tuple<AssembleError, string>(AssembleError.ArgCount, $"line {line}: Unary OP expected 2 args"); return false; }
            if (!TryParseSizecode(file, tokens[1], out UInt64 sizecode, last_static_label, line, ref err)) { err = new Tuple<AssembleError, string>(AssembleError.MissingSize, $"line {line}: Unary OP expected size parameter as first arg\n-> {err.Item2}"); return false; }
            if (!TryParseRegister(file, tokens[2], out UInt64 reg, last_static_label, line, ref err)) { err = new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {line}: Unary OP expected register parameter as second arg\n-> {err.Item2}"); return false; }

            Append(file, 1, (UInt64)op);
            Append(file, 1, (reg << 4) | sizecode);

            return true;
        }
        private static bool TryProcessJump(ObjectFile file, string[] tokens, int line, OPCode op, string last_static_label, ref Tuple<AssembleError, string> err)
        {
            // [8: Jcc]   [address]

            if (tokens.Length != 2) { err = new Tuple<AssembleError, string>(AssembleError.ArgCount, $"line {line}: Jump expected 1 arg"); return false; }
            if (!TryParseAddress(file, tokens[1], out UInt64 a, out UInt64 b, out Hole hole, last_static_label, line, ref err)) { err = new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {line}: Jump expected address as first arg\n-> {err.Item2}"); return false; }

            Append(file, 1, (UInt64)op);
            AppendAddress(file, a, b, hole);

            return true;
        }
        private static bool TryProcessEmission(ObjectFile file, string[] tokens, int line, UInt64 size, string last_static_label, ref Tuple<AssembleError, string> err)
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
                    if (!TryParseInstantImm(file, tokens[i].Substring(1), out mult, out floating, last_static_label, line, ref err)) return false;
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
                    if (!TryParseImm(file, tokens[i], out hole, last_static_label, line, ref err)) return false;

                    // make one of them
                    mult = 1;
                }

                // write the value(s)
                for (UInt64 j = 0; j < mult; ++j)
                    Append(file, size, hole);
            }

            return true;
        }
        private static bool TryProcessMove(ObjectFile file, string[] tokens, int line, OPCode op, string last_static_label, ref Tuple<AssembleError, string> err)
        {
            // [8: mov]   [4: source/dest][2: size][2: mode]   (mode = 0: load imm [size: imm]   mode = 1: load register [4:][4: source]   mode = 2: load memory [address]   mode = 3: store register [address])

            UInt64 sizecode, a, b, c;
            Hole hole;

            if (tokens.Length != 4) { err = new Tuple<AssembleError, string>(AssembleError.ArgCount, $"line {line}: MOV expected 3 args"); return false; }
            if (!TryParseSizecode(file, tokens[1], out sizecode, last_static_label, line, ref err)) { err = new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {line}: MOV expected size parameter as first arg\n-> {err.Item2}"); return false; }

            Append(file, 1, (UInt64)op);

            // loading
            if (TryParseRegister(file, tokens[2], out a, last_static_label, line, ref err))
            {
                // from imm
                if (TryParseImm(file, tokens[3], out hole, last_static_label, line, ref err))
                {
                    Append(file, 1, (a << 4) | (sizecode << 2) | 0);
                    Append(file, Size(sizecode), hole);
                }
                // from register
                else if (TryParseRegister(file, tokens[3], out b, last_static_label, line, ref err))
                {
                    Append(file, 1, (a << 4) | (sizecode << 2) | 1);
                    Append(file, 1, b);
                }
                // from memory
                else if (TryParseAddress(file, tokens[3], out b, out c, out hole, last_static_label, line, ref err))
                {
                    Append(file, 1, (a << 4) | (sizecode << 2) | 2);
                    AppendAddress(file, b, c, hole);
                }
                else { err = new Tuple<AssembleError, string>(AssembleError.UsageError, $"line {line}: Unknown usage of MOV\n-> Couldn't parse \"{tokens[3]}\" as an imm, register, or address"); return false; }
            }
            // storing
            else if (TryParseAddress(file, tokens[2], out a, out b, out hole, last_static_label, line, ref err))
            {
                // from register
                if (TryParseRegister(file, tokens[3], out c, last_static_label, line, ref err))
                {
                    Append(file, 1, (c << 4) | (sizecode << 2) | 3);
                    AppendAddress(file, a, b, hole);
                }
                else { err = new Tuple<AssembleError, string>(AssembleError.UsageError, $"line {line}: Unknown usage of MOV\n-> Couldn't parse \"{tokens[3]}\" as a register"); return false; }
            }
            else { err = new Tuple<AssembleError, string>(AssembleError.UsageError, $"line {line}: Unknown usage of MOV\n-> Couldn't parse \"{tokens[2]}\" as a register or address"); return false; }

            return true;
        }
        private static bool TryProcessXMULXDIV(ObjectFile file, string[] tokens, int line, OPCode op, string last_static_label, ref Tuple<AssembleError, string> err)
        {
            // [8: xmul/xdiv]   [4: reg][2: size][2: mode]   (mode = 0: [size: imm]   mode = 1: use reg   mode = 2: [address]   mode = 3: UND)

            UInt64 sizecode, a, b;
            Hole hole;

            if (tokens.Length != 3) { err = new Tuple<AssembleError, string>(AssembleError.ArgCount, $"line {line}: {op} expected 2 args"); return false; }
            if (!TryParseSizecode(file, tokens[1], out sizecode, last_static_label, line, ref err)) { err = new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {line}: {op} expected size parameter as first arg\n-> {err.Item2}"); return false; }

            Append(file, 1, (UInt64)op);
            if (TryParseImm(file, tokens[2], out hole, last_static_label, line, ref err))
            {
                Append(file, 1, (sizecode << 2) | 0);
                Append(file, Size(sizecode), hole);
            }
            else if (TryParseRegister(file, tokens[2], out a, last_static_label, line, ref err))
            {
                Append(file, 1, (a << 4) | (sizecode << 2) | 1);
            }
            else if (TryParseAddress(file, tokens[2], out a, out b, out hole, last_static_label, line, ref err))
            {
                Append(file, 1, (sizecode << 2) | 2);
                AppendAddress(file, a, b, hole);
            }
            else { err = new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {line}: Unknown usage of {op}"); return false; }

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
            string last_static_label = null; // the last non-local label created

            // potential parsing args for an instruction
            UInt64 a = 0, b = 0, c = 0;
            Hole hole;
            Tuple<AssembleError, string> err = null;
            bool floating;

            if (code.Length == 0) return new Tuple<AssembleError, string>(AssembleError.EmptyFile, "The file was empty");

            while (pos < code.Length)
            {
                // find the next separator
                end = code.Length; // if no separaor found, end of code is default terminator
                for (int i = pos; i < code.Length; ++i)
                    if (code[i] == '\n' || code[i] == '#') { end = i; break; }

                // split line into tokens
                string[] tokens = code.Substring(pos, end - pos).Split(new char[] { ' ', '\t', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                ++line; // advance line counter

                // if the separator was a comment character, consume the rest of the line as well as noop
                if (end < code.Length && code[end] == '#')
                {
                    for (; end < code.Length && code[end] != '\n'; ++end) ;
                }

                // if this marks a label
                while (tokens.Length > 0 && tokens[0][tokens[0].Length - 1] == ':')
                {
                    // take off the colon
                    string label = tokens[0].Substring(0, tokens[0].Length - 1);

                    // handle local mutation
                    if (label.Length > 0 && label[0] != '.') last_static_label = label;
                    if (!MutateLabel(ref label, last_static_label, line, ref err)) return err;

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
                            if (!MutateLabel(ref tokens[1], last_static_label, line, ref err)) return err;
                            if (!IsValidLabel(tokens[1])) return new Tuple<AssembleError, string>(AssembleError.InvalidLabel, $"line {line}: Invalid label name \"{tokens[1]}\"");
                            if (!TryParseInstantImm(file, tokens[2], out a, out floating, last_static_label, line, ref err)) return new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {line}: DEF expected a number as third arg\n-> {err.Item2}");
                            if (file.Symbols.ContainsKey(tokens[1])) return new Tuple<AssembleError, string>(AssembleError.SymbolRedefinition, $"line {line}: Symbol \"{tokens[1]}\" was already defined");
                            file.Symbols.Add(tokens[1], new Symbol() { Value = a, IsAddress = false, IsFloating = floating });
                            break;

                        case "BYTE": if (!TryProcessEmission(file, tokens, line, 1, last_static_label, ref err)) return err; break;
                        case "WORD": if (!TryProcessEmission(file, tokens, line, 2, last_static_label, ref err)) return err; break;
                        case "DWORD": if (!TryProcessEmission(file, tokens, line, 4, last_static_label, ref err)) return err; break;
                        case "QWORD": if (!TryProcessEmission(file, tokens, line, 8, last_static_label, ref err)) return err; break;

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
                        case "MOV": if (!TryProcessMove(file, tokens, line, OPCode.Mov, last_static_label, ref err)) return err; break;

                        case "MOVA": case "MOVNBE": if (!TryProcessMove(file, tokens, line, OPCode.MOVa, last_static_label, ref err)) return err; break;
                        case "MOVAE": case "MOVNB": if (!TryProcessMove(file, tokens, line, OPCode.MOVae, last_static_label, ref err)) return err; break;
                        case "MOVB": case "MOVNAE": if (!TryProcessMove(file, tokens, line, OPCode.MOVb, last_static_label, ref err)) return err; break;
                        case "MOVBE": case "MOVNA": if (!TryProcessMove(file, tokens, line, OPCode.MOVbe, last_static_label, ref err)) return err; break;

                        case "MOVG": case "MOVNLE": if (!TryProcessMove(file, tokens, line, OPCode.MOVg, last_static_label, ref err)) return err; break;
                        case "MOVGE": case "MOVNL": if (!TryProcessMove(file, tokens, line, OPCode.MOVge, last_static_label, ref err)) return err; break;
                        case "MOVL": case "MOVNGE": if (!TryProcessMove(file, tokens, line, OPCode.MOVl, last_static_label, ref err)) return err; break;
                        case "MOVLE": case "MOVNG": if (!TryProcessMove(file, tokens, line, OPCode.MOVle, last_static_label, ref err)) return err; break;

                        case "MOVZ": case "MOVE": if (!TryProcessMove(file, tokens, line, OPCode.MOVz, last_static_label, ref err)) return err; break;
                        case "MOVNZ": case "MOVNE": if (!TryProcessMove(file, tokens, line, OPCode.MOVnz, last_static_label, ref err)) return err; break;
                        case "MOVS": if (!TryProcessMove(file, tokens, line, OPCode.MOVs, last_static_label, ref err)) return err; break;
                        case "MOVNS": if (!TryProcessMove(file, tokens, line, OPCode.MOVns, last_static_label, ref err)) return err; break;
                        case "MOVP": if (!TryProcessMove(file, tokens, line, OPCode.MOVp, last_static_label, ref err)) return err; break;
                        case "MOVNP": if (!TryProcessMove(file, tokens, line, OPCode.MOVnp, last_static_label, ref err)) return err; break;
                        case "MOVO": if (!TryProcessMove(file, tokens, line, OPCode.MOVo, last_static_label, ref err)) return err; break;
                        case "MOVNO": if (!TryProcessMove(file, tokens, line, OPCode.MOVno, last_static_label, ref err)) return err; break;
                        case "MOVC": if (!TryProcessMove(file, tokens, line, OPCode.MOVc, last_static_label, ref err)) return err; break;
                        case "MOVNC": if (!TryProcessMove(file, tokens, line, OPCode.MOVnc, last_static_label, ref err)) return err; break;

                        // [8: swap]   [4: r1][4: r2]
                        case "SWAP":
                            if (tokens.Length != 3) return new Tuple<AssembleError, string>(AssembleError.ArgCount, $"line {line}: SWAP expected 2 args");
                            if (!TryParseRegister(file, tokens[1], out a, last_static_label, line, ref err)) return new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {line}: SWAP expected a register as first arg\n-> {err.Item2}");
                            if (!TryParseRegister(file, tokens[2], out b, last_static_label, line, ref err)) return new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {line}: SWAP expected a register as second arg\n-> {err.Item2}");

                            Append(file, 1, (UInt64)OPCode.Swap);
                            Append(file, 1, (a << 4) | b);

                            break;

                        // [8: XExtend]   [4: register][2: from size][2: to size]     ---     XExtend from to reg
                        case "UEXTEND":
                        case "SEXTEND":
                            if (tokens.Length != 4) return new Tuple<AssembleError, string>(AssembleError.ArgCount, $"line {line}: XEXTEND expected 3 args");
                            if (!TryParseSizecode(file, tokens[1], out a, last_static_label, line, ref err)) return new Tuple<AssembleError, string>(AssembleError.MissingSize, $"line {line}: UEXTEND expected size parameter as first arg\n-> {err.Item2}");
                            if (!TryParseSizecode(file, tokens[2], out b, last_static_label, line, ref err)) return new Tuple<AssembleError, string>(AssembleError.MissingSize, $"line {line}: UEXTEND expected size parameter as second arg\n-> {err.Item2}");
                            if (!TryParseRegister(file, tokens[3], out c, last_static_label, line, ref err)) return new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {line}: UEXTEND expected register parameter as third arg\n-> {err.Item2}");

                            Append(file, 1, (UInt64)(tokens[0].ToUpper() == "UEXTEND" ? OPCode.UExtend : OPCode.SExtend));
                            Append(file, 1, (c << 4) | (a << 2) | b);

                            break;

                        // [8: xmul/xdiv]   [4: reg][2: size][2: mode]   (mode = 0: [size: imm]   mode = 1: use reg   mode = 2: [address]   mode = 3: UND)
                        case "UMUL": if (!TryProcessXMULXDIV(file, tokens, line, OPCode.UMUL, last_static_label, ref err)) return err; break;
                        case "SMUL": if (!TryProcessXMULXDIV(file, tokens, line, OPCode.SMUL, last_static_label, ref err)) return err; break;
                        case "UDIV": if (!TryProcessXMULXDIV(file, tokens, line, OPCode.UDIV, last_static_label, ref err)) return err; break;
                        case "SDIV": if (!TryProcessXMULXDIV(file, tokens, line, OPCode.SDIV, last_static_label, ref err)) return err; break;

                        // [8: binary op]   [4: dest][2: size][2: mode]   (mode = 0: [size: imm]   mode = 1: [4:][4: r]   mode = 2: [address]   mode = 3: UND)
                        case "ADD": if (!TryProcessBinaryOp(file, tokens, line, OPCode.Add, last_static_label, ref err)) return err; break;
                        case "SUB": if (!TryProcessBinaryOp(file, tokens, line, OPCode.Sub, last_static_label, ref err)) return err; break;
                        case "BMUL": if (!TryProcessBinaryOp(file, tokens, line, OPCode.Bmul, last_static_label, ref err)) return err; break;
                        case "BUDIV": if (!TryProcessBinaryOp(file, tokens, line, OPCode.Budiv, last_static_label, ref err)) return err; break;
                        case "BUMOD": if (!TryProcessBinaryOp(file, tokens, line, OPCode.Bumod, last_static_label, ref err)) return err; break;
                        case "BSDIV": if (!TryProcessBinaryOp(file, tokens, line, OPCode.Bsdiv, last_static_label, ref err)) return err; break;
                        case "BSMOD": if (!TryProcessBinaryOp(file, tokens, line, OPCode.Bsmod, last_static_label, ref err)) return err; break;

                        case "SL": if (!TryProcessBinaryOp(file, tokens, line, OPCode.SL, last_static_label, ref err)) return err; break;
                        case "SR": if (!TryProcessBinaryOp(file, tokens, line, OPCode.SR, last_static_label, ref err)) return err; break;
                        case "SAL": if (!TryProcessBinaryOp(file, tokens, line, OPCode.SAL, last_static_label, ref err)) return err; break;
                        case "SAR": if (!TryProcessBinaryOp(file, tokens, line, OPCode.SAR, last_static_label, ref err)) return err; break;
                        case "RL": if (!TryProcessBinaryOp(file, tokens, line, OPCode.RL, last_static_label, ref err)) return err; break;
                        case "RR": if (!TryProcessBinaryOp(file, tokens, line, OPCode.RR, last_static_label, ref err)) return err; break;

                        case "AND": if (!TryProcessBinaryOp(file, tokens, line, OPCode.And, last_static_label, ref err)) return err; break;
                        case "OR": if (!TryProcessBinaryOp(file, tokens, line, OPCode.Or, last_static_label, ref err)) return err; break;
                        case "XOR": if (!TryProcessBinaryOp(file, tokens, line, OPCode.Xor, last_static_label, ref err)) return err; break;

                        case "CMP": if (!TryProcessBinaryOp(file, tokens, line, OPCode.Cmp, last_static_label, ref err)) return err; break;
                        case "TEST": if (!TryProcessBinaryOp(file, tokens, line, OPCode.Test, last_static_label, ref err)) return err; break;

                        // [8: unary op]   [4: dest][2:][2: size]
                        case "INC": if (!TryProcessUnaryOp(file, tokens, line, OPCode.Inc, last_static_label, ref err)) return err; break;
                        case "DEC": if (!TryProcessUnaryOp(file, tokens, line, OPCode.Dec, last_static_label, ref err)) return err; break;
                        case "NEG": if (!TryProcessUnaryOp(file, tokens, line, OPCode.Neg, last_static_label, ref err)) return err; break;
                        case "NOT": if (!TryProcessUnaryOp(file, tokens, line, OPCode.Not, last_static_label, ref err)) return err; break;
                        case "ABS": if (!TryProcessUnaryOp(file, tokens, line, OPCode.Abs, last_static_label, ref err)) return err; break;
                        case "CMP0": if (!TryProcessUnaryOp(file, tokens, line, OPCode.Cmp0, last_static_label, ref err)) return err; break;

                        // [8: la]   [4:][4: dest]   [address]
                        case "LA":
                            if (tokens.Length != 3) return new Tuple<AssembleError, string>(AssembleError.ArgCount, $"line {line}: LA expected 2 args");
                            if (!TryParseRegister(file, tokens[1], out a, last_static_label, line, ref err)) return new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {line}: LA expecetd register as first arg\n-> {err.Item2}");
                            if (!TryParseAddress(file, tokens[2], out b, out c, out hole, last_static_label, line, ref err)) return new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {line}: LA expected address as second arg\n-> {err.Item2}");

                            Append(file, 1, (UInt64)OPCode.La);
                            Append(file, 1, a);
                            AppendAddress(file, b, c, hole);

                            break;

                        // [8: Jcc]   [address]
                        case "JMP": if (!TryProcessJump(file, tokens, line, OPCode.Jmp, last_static_label, ref err)) return err; break;

                        case "JA": case "JNBE": if (!TryProcessJump(file, tokens, line, OPCode.Ja, last_static_label, ref err)) return err; break;
                        case "JAE": case "JNB": if (!TryProcessJump(file, tokens, line, OPCode.Jae, last_static_label, ref err)) return err; break;
                        case "JB": case "JNAE": if (!TryProcessJump(file, tokens, line, OPCode.Jb, last_static_label, ref err)) return err; break;
                        case "JBE": case "JNA": if (!TryProcessJump(file, tokens, line, OPCode.Jbe, last_static_label, ref err)) return err; break;

                        case "JG": case "JNLE": if (!TryProcessJump(file, tokens, line, OPCode.Jg, last_static_label, ref err)) return err; break;
                        case "JGE": case "JNL": if (!TryProcessJump(file, tokens, line, OPCode.Jge, last_static_label, ref err)) return err; break;
                        case "JL": case "JNGE": if (!TryProcessJump(file, tokens, line, OPCode.Jl, last_static_label, ref err)) return err; break;
                        case "JLE": case "JNG": if (!TryProcessJump(file, tokens, line, OPCode.Jle, last_static_label, ref err)) return err; break;

                        case "JZ": case "JE": if (!TryProcessJump(file, tokens, line, OPCode.Jz, last_static_label, ref err)) return err; break;
                        case "JNZ": case "JNE": if (!TryProcessJump(file, tokens, line, OPCode.Jnz, last_static_label, ref err)) return err; break;
                        case "JS": if (!TryProcessJump(file, tokens, line, OPCode.Js, last_static_label, ref err)) return err; break;
                        case "JNS": if (!TryProcessJump(file, tokens, line, OPCode.Jns, last_static_label, ref err)) return err; break;
                        case "JP": if (!TryProcessJump(file, tokens, line, OPCode.Jp, last_static_label, ref err)) return err; break;
                        case "JNP": if (!TryProcessJump(file, tokens, line, OPCode.Jnp, last_static_label, ref err)) return err; break;
                        case "JO": if (!TryProcessJump(file, tokens, line, OPCode.Jo, last_static_label, ref err)) return err; break;
                        case "JNO": if (!TryProcessJump(file, tokens, line, OPCode.Jno, last_static_label, ref err)) return err; break;
                        case "JC": if (!TryProcessJump(file, tokens, line, OPCode.Jc, last_static_label, ref err)) return err; break;
                        case "JNC": if (!TryProcessJump(file, tokens, line, OPCode.Jnc, last_static_label, ref err)) return err; break;

                        case "FADD": if (!TryProcessBinaryOp(file, tokens, line, OPCode.Fadd, last_static_label, ref err)) return err; break;
                        case "FSUB": if (!TryProcessBinaryOp(file, tokens, line, OPCode.Fsub, last_static_label, ref err)) return err; break;
                        case "FMUL": if (!TryProcessBinaryOp(file, tokens, line, OPCode.Fmul, last_static_label, ref err)) return err; break;
                        case "FDIV": if (!TryProcessBinaryOp(file, tokens, line, OPCode.Fdiv, last_static_label, ref err)) return err; break;
                        case "FMOD": if (!TryProcessBinaryOp(file, tokens, line, OPCode.Fmod, last_static_label, ref err)) return err; break;

                        case "FPOW": if (!TryProcessBinaryOp(file, tokens, line, OPCode.Fpow, last_static_label, ref err)) return err; break;
                        case "FSQRT": if (!TryProcessUnaryOp(file, tokens, line, OPCode.Fsqrt, last_static_label, ref err)) return err; break;
                        case "FEXP": if (!TryProcessUnaryOp(file, tokens, line, OPCode.Fexp, last_static_label, ref err)) return err; break;
                        case "FLN": if (!TryProcessUnaryOp(file, tokens, line, OPCode.Fln, last_static_label, ref err)) return err; break;
                        case "FABS": if (!TryProcessUnaryOp(file, tokens, line, OPCode.Fabs, last_static_label, ref err)) return err; break;
                        case "FCMP0": if (!TryProcessUnaryOp(file, tokens, line, OPCode.Fcmp0, last_static_label, ref err)) return err; break;

                        case "FSIN": if (!TryProcessUnaryOp(file, tokens, line, OPCode.Fsin, last_static_label, ref err)) return err; break;
                        case "FCOS": if (!TryProcessUnaryOp(file, tokens, line, OPCode.Fcos, last_static_label, ref err)) return err; break;
                        case "FTAN": if (!TryProcessUnaryOp(file, tokens, line, OPCode.Ftan, last_static_label, ref err)) return err; break;

                        case "FSINH": if (!TryProcessUnaryOp(file, tokens, line, OPCode.Fsinh, last_static_label, ref err)) return err; break;
                        case "FCOSH": if (!TryProcessUnaryOp(file, tokens, line, OPCode.Fcosh, last_static_label, ref err)) return err; break;
                        case "FTANH": if (!TryProcessUnaryOp(file, tokens, line, OPCode.Ftanh, last_static_label, ref err)) return err; break;

                        case "FASIN": if (!TryProcessUnaryOp(file, tokens, line, OPCode.Fasin, last_static_label, ref err)) return err; break;
                        case "FACOS": if (!TryProcessUnaryOp(file, tokens, line, OPCode.Facos, last_static_label, ref err)) return err; break;
                        case "FATAN": if (!TryProcessUnaryOp(file, tokens, line, OPCode.Fatan, last_static_label, ref err)) return err; break;
                        case "FATAN2": if (!TryProcessBinaryOp(file, tokens, line, OPCode.Fatan2, last_static_label, ref err)) return err; break;

                        case "FFLOOR": if (!TryProcessUnaryOp(file, tokens, line, OPCode.Ffloor, last_static_label, ref err)) return err; break;
                        case "FCEIL": if (!TryProcessUnaryOp(file, tokens, line, OPCode.Fceil, last_static_label, ref err)) return err; break;
                        case "FROUND": if (!TryProcessUnaryOp(file, tokens, line, OPCode.Fround, last_static_label, ref err)) return err; break;
                        case "FTRUNC": if (!TryProcessUnaryOp(file, tokens, line, OPCode.Ftrunc, last_static_label, ref err)) return err; break;

                        case "FCMP": if (!TryProcessBinaryOp(file, tokens, line, OPCode.Fcmp, last_static_label, ref err)) return err; break;

                        case "FTOI": if (!TryProcessUnaryOp(file, tokens, line, OPCode.FTOI, last_static_label, ref err)) return err; break;
                        case "ITOF": if (!TryProcessUnaryOp(file, tokens, line, OPCode.ITOF, last_static_label, ref err)) return err; break;

                        // [8: push]   [4: source][2: size][2: mode]   ([size: imm])   (mode = 0: push imm   mode = 0: push register   otherwise undefined)
                        case "PUSH":
                            if (tokens.Length != 3) return new Tuple<AssembleError, string>(AssembleError.ArgCount, $"line {line}: PUSH expected 2 args");
                            if (!TryParseSizecode(file, tokens[1], out a, last_static_label, line, ref err)) return new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {line}: PUSH size parameter as first arg\n-> {err.Item2}");

                            Append(file, 1, (UInt64)OPCode.Push);
                            if (TryParseImm(file, tokens[2], out hole, last_static_label, line, ref err))
                            {
                                Append(file, 1, (a << 2) | 0);
                                Append(file, Size(a), hole);
                            }
                            else if (TryParseRegister(file, tokens[2], out b, last_static_label, line, ref err))
                            {
                                Append(file, 1, (b << 4) | (a << 2) | 1);
                            }
                            else return new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {line}: Unknown usage of PUSH");
                            break;
                        // [8: pop]   [4: dest][2:][2: size]
                        case "POP":
                            if (tokens.Length != 3) return new Tuple<AssembleError, string>(AssembleError.ArgCount, $"line {line}: POP expected 2 args");
                            if (!TryParseSizecode(file, tokens[1], out a, last_static_label, line, ref err)) return new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {line}: POP expected size parameter as first arg\n-> {err.Item2}");
                            if (!TryParseRegister(file, tokens[2], out b, last_static_label, line, ref err)) return new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {line}: POP expected register as second arg\n-> {err.Item2}");
                            Append(file, 1, (UInt64)OPCode.Pop);
                            Append(file, 1, (b << 4) | a);
                            break;
                        // [8: call]   [address]
                        case "CALL":
                            if (tokens.Length != 2) return new Tuple<AssembleError, string>(AssembleError.ArgCount, $"line {line}: CALL expected one arg");
                            if (!TryParseAddress(file, tokens[1], out a, out b, out hole, last_static_label, line, ref err)) return new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {line}: CALLexpected address as first arg\n-> {err.Item2}");
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
