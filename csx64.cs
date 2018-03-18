using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace csx64
{
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

            Load, Store,
            Swap,

            UExtend, SExtend,

            Add, Sub, Mul, UDiv, UMod, SDiv, SMod,
            SLL, SLR, SAL, SAR, RL, RR,
            And, Or, Xor,

            Cmp, Test,

            Inc, Dec, Neg, Not,

            Jmp,
            Ja, Jae, Jb, Jbe, Jg, Jge, Jl, Jle,
            Jz, Jnz, Js, Jns, Jp, Jnp, Jo, Jno, Jc, Jnc,

            SETa, SETae, SETb, SETbe, SETg, SETge, SETl, SETle,
            SETz, SETnz, SETs, SETns, SETp, SETnp, SETo, SETno, SETc, SETnc
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

        // -------------------------------
        // -- Private Utility Functions --
        // -------------------------------

        private static bool Positive(UInt64 val, UInt64 sizecode)
        {
            return ((val >> (8 * (ushort)Size(sizecode) - 1)) & 1) == 0;
        }
        private static bool Negative(UInt64 val, UInt64 sizecode)
        {
            return ((val >> (8 * (ushort)Size(sizecode) - 1)) & 1) != 0;
        }

        private static UInt64 SignExtend(UInt64 val, UInt64 sizecode)
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
        private static UInt64 Truncate(UInt64 val, UInt64 sizecode)
        {
            switch (sizecode)
            {
                case 0: return 0x00000000000000ff & val;
                case 1: return 0x000000000000ffff & val;
                case 2: return 0x00000000ffffffff & val;

                default: return val; // can't truncate 64-bit value
            }
        }

        // Parses a size code into an actual size (in bytes) 0:1  1:2  2:4  3:8
        private static UInt64 Size(UInt64 sizecode)
        {
            return 1ul << (ushort)sizecode;
        }

        // Gets the multiplier from a mult code. 0:0  1:1  2:2  3:4  4:8  5:16  6:32  7:64
        private static UInt64 MultCode(UInt64 code)
        {
            return code == 0 ? 0ul : 1ul << (ushort)(code - 1);
        }
        // as MultCode but returns negative if neg is nonzero
        private static UInt64 MultCode(UInt64 code, UInt64 neg)
        {
            return neg == 0 ? MultCode(code) : ~MultCode(code) + 1;
        }

        // [1: literal][3: m1][1: -m2][3: m2]   [4: r1][4: r2]   ([64: imm])
        private bool GetAddress(ref UInt64 res)
        {
            UInt64 mults = 0, regs = 0, imm = 0; // the mult codes, regs, and literal (mults and regs only initialized for compiler, but literal must be initialized to 0)

            // parse the address
            if (!GetMem(1, ref mults) || !GetMem(1, ref regs) || (mults & 128) != 0 && !GetMem(8, ref imm)) return false;

            // compute the result into res
            res = MultCode((mults >> 4) & 7) * Registers[regs >> 4].x64 + MultCode(mults & 7, mults & 8) * Registers[regs & 15].x64 + imm;

            // got an address
            return true;
        }

        // ---------------
        // -- Operators --
        // ---------------

        private interface IBinaryEvaluator { UInt64 Evaluate(UInt64 sizecode, UInt64 a, UInt64 b, FlagsRegister flags = null); }
        private interface IUnaryEvaluator { UInt64 Evaluate(UInt64 sizecode, UInt64 val, FlagsRegister flags = null); }

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
        private struct SUB : IBinaryEvaluator { public UInt64 Evaluate(UInt64 sizecode, UInt64 a, UInt64 b, FlagsRegister flags) { return new ADD().Evaluate(sizecode, a, ~b + 1, flags); } }

        private struct MUL : IBinaryEvaluator
        {
            public UInt64 Evaluate(UInt64 sizecode, UInt64 a, UInt64 b, FlagsRegister flags)
            {
                UInt64 res = Truncate(a * b, sizecode);

                if (flags != null)
                {
                    UpdateFlags(sizecode, res, flags);

                    flags.C = res < a && res < b;
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

                    flags.C = flags.O = false;
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
                bool neg = false; // negative result flag

                // test negative args
                if (Negative(a, sizecode))
                {
                    neg = !neg;
                    a = ~a + 1;
                }
                if (Negative(b, sizecode))
                {
                    neg = !neg;
                    b = ~b + 1;
                }

                // calculate result with proper signage
                UInt64 res = Truncate(b != 0 ? (neg ? ~(a / b) + 1 : a / b) : 0, sizecode);

                if (flags != null)
                {
                    UpdateFlags(sizecode, res, flags);

                    flags.C = flags.O = false;
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
                int sh = ((ushort)b % 64); // amount to shift by

                UInt64 res = Truncate(a << sh, sizecode);

                if (flags != null)
                {
                    UpdateFlags(sizecode, res, flags);

                    // CHANGE MAYBE IDK -- DEPENDS HOW I FEEL
                    flags.C = flags.O = ((a >> ((ushort)Size(sizecode) * 8 - sh)) & 1) != 0;
                }

                return res;
            }
        }
        private struct SLR : IBinaryEvaluator
        {
            public UInt64 Evaluate(UInt64 sizecode, UInt64 a, UInt64 b, FlagsRegister flags)
            {
                int sh = ((ushort)b % 64); // amount to shift by

                UInt64 res = a >> sh;

                if (flags != null)
                {
                    UpdateFlags(sizecode, res, flags);

                    flags.C = flags.O = false;
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
            flags.S = Negative(sizecode, value);

            // compute parity flag (only of low 8 bits)
            bool parity = true;
            for (int i = 0; i < 8; ++i)
                if (((value >> i) & 1) != 0) parity = !parity;
            flags.P = parity;
        }

        // --------------------
        // -- Execution Data --
        // --------------------

        /// <summary>
        /// The number of registers in the computer
        /// </summary>
        public const int NRegisters = 16;
        private Register[] Registers = new Register[NRegisters];
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
        public bool Initialize(byte[] data)
        {
            // make sure we're not loading null array
            if (data == null || data.LongLength == 0) return false;

            // get new memory array
            Memory = new byte[data.LongLength];

            // copy over the data
            data.CopyTo(Memory, 0L);

            // randomize registers
            for (int i = 0; i < Registers.Length; ++i)
            {
                Registers[i] = new Register();

                Registers[i].x32 = (UInt64)Rand.Next();
                Registers[i].x64 <<= 32;
                Registers[i].x32 = (UInt64)Rand.Next();
            }
            // randomize flags
            Flags.Flags = (uint)Rand.Next();

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
        /// Handles syscall instructions from the processor. Returns true iff the syscall was handled successfully
        /// </summary>
        protected virtual bool Syscall() { return false; }

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

                // [8: load]   [4: dest][2: size][2: mode]   (mode = 0: [size: imm]   mode = 1: [4:][4: r]   mode = 2: [address]   mode = 3: UND)
                case OPCode.Load:
                    if (!GetMem(1, ref a)) return false;
                    switch (a & 3)
                    {
                        case 0: if (!GetMem(Size((a >> 2) & 3), ref b)) return false; Registers[a >> 4].Set((a >> 2) & 3, b); return true;
                        case 1: if (!GetMem(1, ref b)) return false; Registers[a >> 4].Set((a >> 2) & 3, Registers[b & 15].x64); return true;
                        case 2: if (!GetAddress(ref b) || !GetMem(b, Size((a >> 2) & 3), ref b)) return false;
                            Registers[a >> 4].Set((a >> 2) & 3, b); return false;
                        default: Fail(ErrorCode.UndefinedBehavior); return false;
                    }
                // [8: store]   [4: source][2:][2: size]   [address]
                case OPCode.Store: if (!GetMem(1, ref a) || !GetAddress(ref b)) return false; return SetMem(b, Size(a & 3), Registers[a >> 4].x64);
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

                // [8: SETcc]   [4: dest][2:][2: size]
                case OPCode.SETa:  if (!GetMem(1, ref a)) return false; Registers[a >> 4].Set(a & 3, Flags.a ? 1ul : 0ul); return true;
                case OPCode.SETae: if (!GetMem(1, ref a)) return false; Registers[a >> 4].Set(a & 3, Flags.ae ? 1ul : 0ul); return true;
                case OPCode.SETb:  if (!GetMem(1, ref a)) return false; Registers[a >> 4].Set(a & 3, Flags.b ? 1ul : 0ul); return true;
                case OPCode.SETbe: if (!GetMem(1, ref a)) return false; Registers[a >> 4].Set(a & 3, Flags.be ? 1ul : 0ul); return true;

                case OPCode.SETg:  if (!GetMem(1, ref a)) return false; Registers[a >> 4].Set(a & 3, Flags.g ? 1ul : 0ul); return true;
                case OPCode.SETge: if (!GetMem(1, ref a)) return false; Registers[a >> 4].Set(a & 3, Flags.ge ? 1ul : 0ul); return true;
                case OPCode.SETl:  if (!GetMem(1, ref a)) return false; Registers[a >> 4].Set(a & 3, Flags.l ? 1ul : 0ul); return true;
                case OPCode.SETle: if (!GetMem(1, ref a)) return false; Registers[a >> 4].Set(a & 3, Flags.le ? 1ul : 0ul); return true;

                case OPCode.SETz:  if (!GetMem(1, ref a)) return false; Registers[a >> 4].Set(a & 3, Flags.Z ? 1ul : 0ul); return true;
                case OPCode.SETnz: if (!GetMem(1, ref a)) return false; Registers[a >> 4].Set(a & 3, Flags.Z ? 0ul : 1ul); return true;
                case OPCode.SETs:  if (!GetMem(1, ref a)) return false; Registers[a >> 4].Set(a & 3, Flags.S ? 1ul : 0ul); return true;
                case OPCode.SETns: if (!GetMem(1, ref a)) return false; Registers[a >> 4].Set(a & 3, Flags.S ? 0ul : 1ul); return true;
                case OPCode.SETp:  if (!GetMem(1, ref a)) return false; Registers[a >> 4].Set(a & 3, Flags.P ? 1ul : 0ul); return true;
                case OPCode.SETnp: if (!GetMem(1, ref a)) return false; Registers[a >> 4].Set(a & 3, Flags.P ? 0ul : 1ul); return true;
                case OPCode.SETo:  if (!GetMem(1, ref a)) return false; Registers[a >> 4].Set(a & 3, Flags.O ? 1ul : 0ul); return true;
                case OPCode.SETno: if (!GetMem(1, ref a)) return false; Registers[a >> 4].Set(a & 3, Flags.O ? 0ul : 1ul); return true;
                case OPCode.SETc:  if (!GetMem(1, ref a)) return false; Registers[a >> 4].Set(a & 3, Flags.C ? 1ul : 0ul); return true;
                case OPCode.SETnc: if (!GetMem(1, ref a)) return false; Registers[a >> 4].Set(a & 3, Flags.C ? 0ul : 1ul); return true;

                // otherwise, unknown opcode
                default: Fail(ErrorCode.UndefinedBehavior); return false;
            }
        }

        // --------------
        // -- Assembly --
        // --------------

        public enum AssembleError
        {
            None, ArgCount, MissingSize, ArgError, FormatError, UsageError, UnknownOp, EmptyFile, InvalidLabel, LabelRedefinition
        }
        public enum LinkError
        {
            None, EmptyResult, SymbolRedefinition, MissingSymbol
        }

        public struct Symbol
        {
            public UInt64 Value;
            public bool IsAddress;
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
            public List<Segment> Segments = new List<Segment>();

            // -------------------

            public bool Append(ObjectFile file, string sub)
            {
                if (sub.Length == 0) return false;
                string _sub = sub[0] == '+' || sub[0] == '-' ? sub.Substring(1) : sub;
                if (_sub.Length == 0) return false;

                // if we can get a value for it, add it
                if (TryParseInstantImm(file, sub, out UInt64 temp)) Value += temp;
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
            if (hole.Segments.Count == 0) Append(file, size, hole.Value);
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

        private static bool TryParseInstantImm(ObjectFile file, string token, out UInt64 res)
        {
            int pos = 0, end = 0; // position in token
            UInt64 temp = 0;      // placeholder for parsing
            res = 0;              // result initially zero

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

                // if it's a number
                if (char.IsDigit(_sub[0]))
                {
                    try
                    {
                        if (_sub.StartsWith("0x")) temp = Convert.ToUInt64(_sub.Substring(2), 16);
                        else if (_sub.StartsWith("0b")) temp = Convert.ToUInt64(_sub.Substring(2), 2);
                        else if (_sub[0] == '0' && _sub.Length > 1) temp = Convert.ToUInt64(_sub.Substring(1), 8);
                        else temp = Convert.ToUInt64(_sub, 10);
                    }
                    catch (Exception) { return false; }
                }
                // if it's an instant symbol
                else if (file.Symbols.TryGetValue(_sub, out Symbol symbol) && !symbol.IsAddress) temp = symbol.Value;
                // otherwise it's a dud
                else return false;

                // if token was negative, take negative
                if (sub[0] == '-') temp = ~temp + 1;

                // add it to res
                res += temp;

                // start of next token includes separator
                pos = end;
            }

            return true;
        }

        private static bool TryParseRegister(ObjectFile file, string token, out UInt64 res)
        {
            res = 0;
            return token.Length >= 2 && token[0] == '$' && TryParseInstantImm(file, token.Substring(1), out res);
        }
        private static bool TryParseSizecode(ObjectFile file, string token, out UInt64 res)
        {
            // must be able ti get an instant imm
            if (!TryParseInstantImm(file, token, out res)) return false;

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
            if (!TryParseInstantImm(file, token, out res)) return false;

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
        // [1: literal][3: m1][1: -m2][3: m2]   [4: r1][4: r2]   ([64: imm])
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
            if (hole.Value == 0 && hole.Segments.Count == 0) hole = null;

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
            DateTime now = DateTime.UtcNow;

            return (UInt64)now.Ticks;
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
                Append(file, 1, c);
                Append(file, 1, d);
                if (hole != null) Append(file, 8, hole);
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
            Append(file, 1, a);
            Append(file, 1, b);
            if (hole != null) Append(file, 8, hole);

            return true;
        }
        private static bool TryProcessEmission(ObjectFile file, string[] tokens, int line, UInt64 size, ref Tuple<AssembleError, string> err)
        {
            if (tokens.Length < 2) { err = new Tuple<AssembleError, string>(AssembleError.ArgCount, $"line {line}: Emission expected at least one value"); return false; }
            
            Hole hole = null;
            UInt64 mult;

            for (int i = 1; i < tokens.Length; ++i)
            {
                // if a multiplier
                if (tokens[i][0] == 'x')
                {
                    // get the multiplier and ensure is valid
                    if (!TryParseInstantImm(file, tokens[i].Substring(1), out mult)) { err = new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {line}: Unable to parse multiplier \"{tokens[i]}\""); return false; }
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
        public static Tuple<AssembleError, string> Assemble(string code, out ObjectFile file)
        {
            file = new ObjectFile();
            
            // predefined symbols
            file.Symbols = new Dictionary<string, Symbol>()
            {
                ["__registers__"] = new Symbol() { Value = 16, IsAddress = false },
                ["__time__"] = new Symbol() { Value = Time(), IsAddress = false },
                ["__version__"] = new Symbol() { Value = Version, IsAddress = false }
            };

            int line = 0; // current line number
            int pos = 0, end = 0;  // position in code

            UInt64 a = 0, b = 0, c = 0, d = 0; // potential parsing args for an instruction
            Hole hole;
            Tuple<AssembleError, string> err = null;

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
                    if (file.Symbols.TryGetValue(label, out Symbol symbol)) return new Tuple<AssembleError, string>(AssembleError.LabelRedefinition, $"line {line}: Symbol \"{label}\" was already defined");

                    // add the symbol as an address
                    file.Symbols.Add(label, new Symbol() { Value = (UInt64)file.Data.LongCount(), IsAddress = true });

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
                    file.Symbols["__line__"] = new Symbol() { Value = (UInt64)line, IsAddress = false };
                    file.Symbols["__pos__"] = new Symbol() { Value = (UInt64)file.Data.LongCount(), IsAddress = true };

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
                            if (!TryParseInstantImm(file, tokens[2], out a)) return new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {line}: DEF expected a number as third arg");
                            file.Symbols.Add(tokens[1], new Symbol() { Value = a, IsAddress = false });
                            break;

                        case "BYTE": if (!TryProcessEmission(file, tokens, line, 1, ref err)) return err; break;
                        case "WORD": if (!TryProcessEmission(file, tokens, line, 2, ref err)) return err; break;
                        case "DWORD": if (!TryProcessEmission(file, tokens, line, 4, ref err)) return err; break;
                        case "QWORD": if (!TryProcessEmission(file, tokens, line, 8, ref err)) return err; break;

                        // --------------------------
                        // -- OPCode assembly impl --
                        // --------------------------

                        case "NOP": Append(file, 1, (UInt64)OPCode.Nop); break;
                        case "STOP": Append(file, 1, (UInt64)OPCode.Stop); break;
                        case "SYSCALL": Append(file, 1, (UInt64)OPCode.Syscall); break;

                        // [8: load]   [4: dest][2: size][2: mode]   (mode = 0: [size: imm]   mode = 1: [4:][4: r]   mode = 2: [address]   mode = 3: UND)
                        case "LOAD": if (!TryProcessBinaryOp(file, tokens, line, OPCode.Load, ref err)) return err; break;
                        // [8: store]   [4: source][2:][2: size]   [address]
                        case "STORE":
                            if (tokens.Length != 4) return new Tuple<AssembleError, string>(AssembleError.ArgCount, $"line {line}: STORE expected 4 args");
                            if (!TryParseSizecode(file, tokens[1], out a)) return new Tuple<AssembleError, string>(AssembleError.MissingSize, $"line {line}: STORE expected size parameter as first arg");
                            if (!TryParseRegister(file, tokens[2], out b)) return new Tuple<AssembleError, string>(AssembleError.ArgError, $"line {line}: STORE expected register parameter as second arg");
                            if (!TryParseAddress(file, tokens[3], out c, out d, out hole)) return new Tuple<AssembleError, string>(AssembleError.FormatError, $"line {line}: STORE expected address parameter as third arg");

                            Append(file, 1, (UInt64)OPCode.Store);
                            Append(file, 1, (b << 4) | a);
                            Append(file, 1, c);
                            Append(file, 1, d);
                            if (hole != null) Append(file, 8, hole);

                            break;
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

                        // [8: SETcc]   [4: dest][2:][2: size]
                        case "SETA": case "SETNBE": if (!TryProcessUnaryOp(file, tokens, line, OPCode.SETa, ref err)) return err; break;
                        case "SETAE": case "SETNB": if (!TryProcessUnaryOp(file, tokens, line, OPCode.SETae, ref err)) return err; break;
                        case "SETB": case "SETNAE": if (!TryProcessUnaryOp(file, tokens, line, OPCode.SETb, ref err)) return err; break;
                        case "SETBE": case "SETNA": if (!TryProcessUnaryOp(file, tokens, line, OPCode.SETbe, ref err)) return err; break;
                        case "SETG": case "SETNLE": if (!TryProcessUnaryOp(file, tokens, line, OPCode.SETg, ref err)) return err; break;
                        case "SETGE": case "SETNL": if (!TryProcessUnaryOp(file, tokens, line, OPCode.SETge, ref err)) return err; break;
                        case "SETL": case "SETNGE": if (!TryProcessUnaryOp(file, tokens, line, OPCode.SETl, ref err)) return err; break;
                        case "SETLE": case "SETNG": if (!TryProcessUnaryOp(file, tokens, line, OPCode.SETle, ref err)) return err; break;

                        case "SETZ": case "SETE": if (!TryProcessUnaryOp(file, tokens, line, OPCode.SETz, ref err)) return err; break;
                        case "SETNZ": case "SETNE": if (!TryProcessUnaryOp(file, tokens, line, OPCode.SETnz, ref err)) return err; break;
                        case "SETS": if (!TryProcessUnaryOp(file, tokens, line, OPCode.SETs, ref err)) return err; break;
                        case "SETNS": if (!TryProcessUnaryOp(file, tokens, line, OPCode.SETns, ref err)) return err; break;
                        case "SETP": if (!TryProcessUnaryOp(file, tokens, line, OPCode.SETp, ref err)) return err; break;
                        case "SETNP": if (!TryProcessUnaryOp(file, tokens, line, OPCode.SETnp, ref err)) return err; break;
                        case "SETO": if (!TryProcessUnaryOp(file, tokens, line, OPCode.SETo, ref err)) return err; break;
                        case "SETNO": if (!TryProcessUnaryOp(file, tokens, line, OPCode.SETno, ref err)) return err; break;
                        case "SETC": if (!TryProcessUnaryOp(file, tokens, line, OPCode.SETc, ref err)) return err; break;
                        case "SETNC": if (!TryProcessUnaryOp(file, tokens, line, OPCode.SETnc, ref err)) return err; break;

                        default: return new Tuple<AssembleError, string>(AssembleError.UnknownOp, $"line {line}: Couldn't process operator \"{tokens[0]}\"");
                    }
                }

                // advance to after the new line
                pos = end + 1;
            }
            
            return new Tuple<AssembleError, string>(AssembleError.None, string.Empty);
        }
        public static Tuple<LinkError, string> Link(UInt64 stacksize, ref byte[] res, params ObjectFile[] objs)
        {
            // get total size of objet files
            UInt64 size = 0;
            foreach (ObjectFile obj in objs) size += (UInt64)obj.Data.LongCount();
            // if zero, there is nothing to link
            if (size == 0) return new Tuple<LinkError, string>(LinkError.EmptyResult, "Resulting file is empty");

            res = new byte[size + 11 + stacksize]; // give it enough memory to write the whole file plus a header and a stack
            size = 11;                             // set size to after header (points to writing position)

            UInt64[] offsets = new UInt64[objs.Length];     // offsets for where an object file begins in the resulting exe
            // create a combined symbols table with predefined values
            var symbols = new Dictionary<string, UInt64>()
            {
                ["__stack_high__"] = (UInt64)res.LongLength,
                ["__stack_size__"] = stacksize,
                ["__stack_low__"] = (UInt64)res.LongLength - stacksize
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
                    symbols.Add(symbol, _symbol.IsAddress ? _symbol.Value + offsets[i] : _symbol.Value);
                }

            // patch holes
            for (int i = 0; i < objs.Length; ++i)
                foreach (Hole hole in objs[i].Holes)
                {
                    // compute the hole value
                    UInt64 val = hole.Value, temp = 0;
                    Symbol symbol;

                    foreach(Hole.Segment seg in hole.Segments)
                    {
                        // prefer static definitions
                        if (objs[i].Symbols.TryGetValue(seg.Symbol, out symbol)) temp = symbol.IsAddress ? symbol.Value + offsets[i] : symbol.Value;
                        else if (!symbols.TryGetValue(seg.Symbol, out temp)) return new Tuple<LinkError, string>(LinkError.MissingSymbol, $"Symbol \"{seg.Symbol}\" undefined");

                        if (seg.IsNegative) temp = ~temp + 1;

                        val += temp;
                    }

                    // fill it in
                    Write(res, hole.Address + offsets[i], hole.Size, val);
                }

            // write the header
            if (!symbols.TryGetValue("main", out UInt64 main)) return new Tuple<LinkError, string>(LinkError.MissingSymbol, "No entry point: \"main\"");
            Write(res, 0, 1, (UInt64)OPCode.Jmp);
            Write(res, 1, 1, 0x80);
            Write(res, 2, 1, 0);
            Write(res, 3, 8, main);

            // linked successfully
            return new Tuple<LinkError, string>(LinkError.None, string.Empty);
        }
    }
}
