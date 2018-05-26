using System;
using System.Numerics;
using static CSX64.Utility;

// -- Operators -- //

namespace CSX64
{
    public partial class Computer
    {
        // -- op utilities -- //

        /// <summary>
        /// Attempts to extract the specified <see cref="ccOPCode"/> status flag. Returns true on success.
        /// </summary>
        /// <param name="computer">the flags object to examine</param>
        /// <param name="code">the code to extract</param>
        public bool TryGet_cc(ccOPCode code, out bool res)
        {
            switch (code)
            {
                case ccOPCode.z: res = ZF; return true;
                case ccOPCode.nz: res = !ZF; return true;
                case ccOPCode.s: res = SF; return true;
                case ccOPCode.ns: res = !SF; return true;
                case ccOPCode.p: res = PF; return true;
                case ccOPCode.np: res = !PF; return true;
                case ccOPCode.o: res = OF; return true;
                case ccOPCode.no: res = !OF; return true;
                case ccOPCode.c: res = CF; return true;
                case ccOPCode.nc: res = !CF; return true;

                case ccOPCode.a: res = a; return true;
                case ccOPCode.ae: res = ae; return true;
                case ccOPCode.b: res = b; return true;
                case ccOPCode.be: res = be; return true;

                case ccOPCode.g: res = g; return true;
                case ccOPCode.ge: res = ge; return true;
                case ccOPCode.l: res = l; return true;
                case ccOPCode.le: res = le; return true;

                // otherwise code unknown
                default: return res = false;
            }
        }

        private bool FetchBinaryOpFormat(out UInt64 s, out UInt64 m, out UInt64 a, out UInt64 b,
            bool get_a = true, int _a_sizecode = -1, int _b_sizecode = -1, bool allow_b_mem = true)
        {
            // read settings
            if (!GetMemAdv(1, out s)) { m = a = b = 0; return false; }

            // if they requested an explicit size for a, change it in the settings byte
            if (_a_sizecode != -1) s = (s & 0xf3) | ((UInt64)_a_sizecode << 2);

            UInt64 a_sizecode = (s >> 2) & 3;
            UInt64 b_sizecode = _b_sizecode == -1 ? a_sizecode : (UInt64)_b_sizecode;

            a = m = 0; // zero a and m (for get_a logic and so compiler won't complain)

            // switch through mode
            switch (s & 3)
            {
                case 0:
                    if (get_a) a = Registers[s >> 4][a_sizecode];
                    if (!GetMemAdv(Size(b_sizecode), out b)) return false;
                    return true;
                case 1:
                    if (!allow_b_mem) { b = 0; Terminate(ErrorCode.UndefinedBehavior); return false; }

                    if (get_a) a = Registers[s >> 4][a_sizecode];
                    if (!GetAddressAdv(out b) || !GetMemRaw(b, Size(b_sizecode), out b)) return false;
                    return true;
                case 2:
                    if (!GetMemAdv(1, out b)) return false;
                    switch ((b >> 4) & 1)
                    {
                        case 0:
                            if (get_a) a = Registers[s >> 4][a_sizecode];
                            b = Registers[b & 15][b_sizecode];
                            return true;
                        case 1:
                            if (!GetAddressAdv(out m) || get_a && !GetMemRaw(m, Size(a_sizecode), out a)) return false;
                            b = Registers[b & 15][b_sizecode];
                            s |= 256; // mark as memory path of mode 2
                            return true;

                        default: return true; // this should never happen but compiler is complainy
                    }
                case 3:
                    if (!GetMemAdv(Size(b_sizecode), out b)) return false;
                    if (!GetAddressAdv(out m) || get_a && !GetMemRaw(m, Size(a_sizecode), out a)) return false;
                    return true;

                default: b = 0; return true; // this should never happen but compiler is complainy
            }
        }
        private bool StoreBinaryOpFormat(UInt64 s, UInt64 m, UInt64 res)
        {
            UInt64 sizecode = (s >> 2) & 3;

            // switch through mode
            switch (s & 3)
            {
                case 0:
                case 1:
                    Registers[s >> 4][sizecode] = res;
                    return true;
                case 2:
                    if (s < 256) goto case 1; else goto case 3;
                case 3:
                    return SetMemRaw(m, Size(sizecode), res);
            }

            return true;
        }

        private bool FetchUnaryOpFormat(out UInt64 s, out UInt64 m, out UInt64 a, bool get_a = true, int _a_sizecode = -1)
        {
            // read settings
            if (!GetMemAdv(1, out s)) { m = a = 0; return false; }

            // if they requested an explicit size for a, change it in the settings byte
            if (_a_sizecode != -1) s = (s & 0xf3) | ((UInt64)_a_sizecode << 2);

            UInt64 a_sizecode = (s >> 2) & 3;

            a = m = 0; // zero a and m (for get_a logic and so compiler won't complain)

            // switch through mode
            switch (s & 1)
            {
                case 0:
                    if (get_a) a = Registers[s >> 4][a_sizecode];
                    return true;
                case 1:
                    return GetAddressAdv(out m) && (!get_a || GetMemRaw(m, Size(a_sizecode), out a));

                default: return true; // this should never happen but compiler is complainy
            }
        }
        private bool StoreUnaryOpFormat(UInt64 s, UInt64 m, UInt64 res)
        {
            UInt64 sizecode = (s >> 2) & 3;

            // switch through mode
            switch (s & 1)
            {
                case 0:
                    Registers[s >> 4][sizecode] = res;
                    return true;
                case 1:
                    return SetMemRaw(m, Size(sizecode), res);

                default: return true; // this should never happen but compiler is complainy
            }
        }

        private bool FetchIMMRMFormat(out UInt64 s, out UInt64 a, int _a_sizecode = -1)
        {
            if (!GetMemAdv(1, out s)) { a = 0; return false; }

            UInt64 a_sizecode = _a_sizecode == -1 ? (s >> 2) & 3 : (UInt64)_a_sizecode;

            // get the value into b
            switch (s & 3)
            {
                case 0: if (!GetMemAdv(Size((s >> 2) & 3), out a)) return false; break;
                case 1: a = Registers[s >> 4][(s >> 2) & 3]; break;
                case 2: if (!GetAddressAdv(out a) || !GetMemRaw(a, Size((s >> 2) & 3), out a)) return false; break;
                default: Terminate(ErrorCode.UndefinedBehavior); { a = 0; return false; }
            }

            return true;
        }

        // updates the flags for integral ops (identical for most integral ops)
        private void UpdateFlagsInt(UInt64 value, UInt64 sizecode)
        {
            ZF = value == 0;
            SF = Negative(value, sizecode);

            // compute parity flag (only of low 8 bits)
            bool parity = true;
            for (int i = 0; i < 8; ++i)
                if (((value >> i) & 1) != 0) parity = !parity;
            PF = parity;
        }
        // updates the flags for floating point ops
        private void UpdateFlagsDouble(double value)
        {
            ZF = value == 0;
            SF = value < 0;
            OF = false;

            CF = double.IsInfinity(value);
            PF = double.IsNaN(value);
        }
        private void UpdateFlagsFloat(float value)
        {
            ZF = value == 0;
            SF = value < 0;
            OF = false;

            CF = float.IsInfinity(value);
            PF = float.IsNaN(value);
        }

        // -- impl -- //

        private bool ProcessPUSHF()
        {
            if (!GetMemAdv(1, out UInt64 ext)) return false;

            switch (ext)
            {
                case 0: return PushRaw(2, RFLAGS);
                case 1: return PushRaw(4, RFLAGS & 0x00fcffff); // VM and RF flags are not saved in the stored image for PUSHD and PUSHQ
                case 2: return PushRaw(8, RFLAGS & 0x00fcffff);

                default: Terminate(ErrorCode.UndefinedBehavior); return false;
            }
        }
        private bool ProcessPOPF()
        {
            if (!GetMemAdv(1, out UInt64 ext)) return false;

            switch (ext)
            {
                case 0:
                case 1:
                case 2:
                    if (!PopRaw(Size(ext + 1), out ext)) return false;
                    RFLAGS = RFLAGS & ~0x7fd5ul | ext & 0x7fd5ul; // can't modify reserved flags (for now we only care about the low 16 flags)
                    return true;

                default: Terminate(ErrorCode.UndefinedBehavior); return false;
            }
        }

        private bool ProcessSETcc()
        {
            if (!GetMemAdv(1, out UInt64 ext) || !TryGet_cc((ccOPCode)ext, out bool flag)) { Terminate(ErrorCode.UndefinedBehavior); return false; }

            if (!FetchUnaryOpFormat(out UInt64 s, out UInt64 m, out UInt64 a, false, 0)) return false;

            return StoreUnaryOpFormat(s, m, flag ? 1 : 0ul);
        }

        private bool ProcessMOV(bool apply = true)
        {
            if (!FetchBinaryOpFormat(out UInt64 s, out UInt64 m, out UInt64 a, out UInt64 b, false)) return false;

            return !apply || StoreBinaryOpFormat(s, m, b);
        }
        private bool ProcessXCHG()
        {
            UInt64 a, b, c, d;

            if (!GetMemAdv(1, out a)) return false;
            switch (a & 1)
            {
                case 0:
                    if (!GetMemAdv(1, out b)) return false;
                    c = Registers[a >> 4].x64;
                    Registers[a >> 4][(a >> 2) & 3] = Registers[b & 15].x64;
                    Registers[b & 15][(a >> 2) & 3] = c;
                    break;
                case 1:
                    if (!GetAddressAdv(out b) || !GetMemRaw(b, Size((a >> 2) & 3), out c)) return false;
                    d = Registers[a >> 4].x64;
                    Registers[a >> 4][(a >> 2) & 3] = c;
                    if (!SetMemRaw(b, Size((a >> 2) & 3), d)) return false;
                    break;
            }

            return true;
        }

        private bool ProcessJMP(bool apply, ref UInt64 aft)
        {
            if (!GetMemAdv(1, out UInt64 s)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UInt64 val = 0;
            switch (s & 3)
            {
                case 0: if (!GetMemAdv(Size(sizecode), out val)) return false; break;
                case 1: val = Registers[s >> 4][sizecode]; break;
                case 2: if (!GetAddressAdv(out val) || !GetMemRaw(val, Size(sizecode), out val)) return false; break;
                case 3:
                    UInt64 tempPos = Pos - 2; // hold initial pos (-2 to account for op code and settings bytes that were already read)
                    if (!GetMemAdv(Size(sizecode), out val)) return false;
                    val = tempPos + SignExtend(val, sizecode); // offset from temp pos
                    break;
            }

            aft = Pos; // record point immediately after reading (for CALL return address)

            if (apply) Pos = val; // jump

            return true;
        }

        private bool ProcessPUSH()
        {
            if (!FetchIMMRMFormat(out UInt64 s, out UInt64 a)) return false;

            return PushRaw(Size((s >> 2) & 3), a);
        }
        private bool ProcessPOP()
        {
            if (!GetMemAdv(1, out UInt64 s) || !PopRaw(Size((s >> 2) & 3), out UInt64 val)) return false;

            Registers[s >> 4][(s >> 2) & 3] = val;
            return true;
        }

        private bool ProcessLEA()
        {
            if (!GetMemAdv(1, out UInt64 s) || !GetAddressAdv(out UInt64 address)) return false;

            Registers[s >> 4][(s >> 2) & 3] = address;
            return true;
        }

        private bool ProcessADD()
        {
            if (!FetchBinaryOpFormat(out UInt64 s, out UInt64 m, out UInt64 a, out UInt64 b)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UInt64 res = Truncate(a + b, sizecode);

            UpdateFlagsInt(res, sizecode);
            CF = res < a && res < b; // if overflow is caused, some of one value must go toward it, so the truncated result must necessarily be less than both args
            AF = (res & 0xf) < (a & 0xf) && (res & 0xf) < (b & 0xf); // AF is just like CF but only the low nibble
            OF = Positive(a, sizecode) == Positive(b, sizecode) && Positive(a, sizecode) != Positive(res, sizecode);

            return StoreBinaryOpFormat(s, m, res);
        }
        private bool ProcessSUB(bool apply = true)
        {
            if (!FetchBinaryOpFormat(out UInt64 s, out UInt64 m, out UInt64 a, out UInt64 b)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UInt64 res = Truncate(a - b, sizecode);

            UpdateFlagsInt(res, sizecode);
            CF = a < b; // if a < b, a borrow was taken from the highest bit
            AF = (a & 0xf) < (b & 0xf); // AF is just like CF but only the low nibble
            OF = Positive(a, sizecode) != Positive(b, sizecode) && Positive(a, sizecode) != Positive(res, sizecode);

            return !apply || StoreBinaryOpFormat(s, m, res);
        }

        private bool ProcessMUL()
        {
            if (!FetchIMMRMFormat(out UInt64 s, out UInt64 a)) return false;

            UInt64 res;
            BigInteger full;

            // switch through register sizes
            switch ((s >> 2) & 3)
            {
                case 0:
                    res = AL * a;
                    AX = (UInt16)res;
                    CF = OF = AH != 0;
                    break;
                case 1:
                    res = AX * a;
                    DX = (UInt16)(res >> 16); AX = (UInt16)res;
                    CF = OF = DX != 0;
                    break;
                case 2:
                    res = EAX * a;
                    EDX = (UInt32)(res >> 32); EAX = (UInt32)res;
                    CF = OF = EDX != 0;
                    break;
                case 3:
                    full = new BigInteger(RAX) * a;
                    RDX = (UInt64)(full >> 64); RAX = (UInt64)(full & 0xffffffffffffffff);
                    CF = OF = RDX != 0;
                    break;
            }

            SF = Rand.NextBool();
            ZF = Rand.NextBool();
            AF = Rand.NextBool();
            PF = Rand.NextBool();

            return true;
        }
        private bool ProcessIMUL()
        {
            if (!GetMemAdv(1, out UInt64 mode)) return false;

            switch (mode)
            {
                case 0: return ProcessUnary_IMUL();
                case 1: return ProcessBinary_IMUL();
                case 2: return ProcessTernary_IMUL();

                default: Terminate(ErrorCode.UndefinedBehavior); return false;
            }
        }
        private bool ProcessUnary_IMUL()
        {
            if (!FetchIMMRMFormat(out UInt64 s, out UInt64 _a)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            // get val as sign extended
            Int64 a = (Int64)SignExtend(_a, sizecode);

            Int64 res;
            BigInteger full;

            // switch through register sizes
            switch (sizecode)
            {
                case 0:
                    res = AL * a;
                    AX = (UInt16)res;
                    CF = OF = res != (sbyte)res;
                    break;
                case 1:
                    res = AX * a;
                    DX = (UInt16)(res >> 16); AX = (UInt16)res;
                    CF = OF = res != (Int16)res;
                    break;
                case 2:
                    res = EAX * a;
                    EDX = (UInt32)(res >> 32); EAX = (UInt32)res;
                    CF = OF = res != (Int32)res;
                    break;
                case 3:
                    full = new BigInteger(RAX) * a;
                    RDX = (UInt64)(full >> 64); RAX = (UInt64)(full & 0xffffffffffffffff);
                    CF = OF = full != (Int64)RAX;
                    break;
            }

            SF = Rand.NextBool();
            ZF = Rand.NextBool();
            AF = Rand.NextBool();
            PF = Rand.NextBool();

            return true;
        }
        private bool ProcessBinary_IMUL()
        {
            if (!FetchBinaryOpFormat(out UInt64 s, out UInt64 m, out UInt64 _a, out UInt64 _b)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            // get vals as sign extended
            Int64 a = (Int64)SignExtend(_a, sizecode);
            Int64 b = (Int64)SignExtend(_b, sizecode);

            Int64 res = 0;
            BigInteger full;

            // switch through register sizes
            switch (sizecode)
            {
                case 0:
                    res = a * b;
                    CF = OF = res != (sbyte)res;
                    break;
                case 1:
                    res = a * b;
                    CF = OF = res != (Int16)res;
                    break;
                case 2:
                    res = a * b;
                    CF = OF = res != (Int32)res;
                    break;
                case 3:
                    full = new BigInteger(a) * b;
                    res = (Int64)(full & 0xffffffffffffffff);
                    CF = OF = full != res;
                    break;
            }

            SF = Rand.NextBool();
            ZF = Rand.NextBool();
            AF = Rand.NextBool();
            PF = Rand.NextBool();

            return StoreBinaryOpFormat(s, m, (UInt64)res);
        }
        private bool ProcessTernary_IMUL()
        {
            if (!GetMemAdv(1, out UInt64 s)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            // read raw vals
            UInt64 _a;
            if ((s & 1) == 0)
            {
                if (!GetMemAdv(1, out _a)) return false;
                _a = Registers[s & 15][sizecode];
            }
            else if (!GetAddressAdv(out _a) || !GetMemRaw(_a, Size(sizecode), out _a)) return false;

            if (!GetMemAdv(Size(sizecode), out UInt64 _b)) return false;

            // get vals as signed
            Int64 a = (Int64)SignExtend(_a, sizecode);
            Int64 b = (Int64)SignExtend(_b, sizecode);

            Int64 res = 0;
            BigInteger full;

            // switch through register sizes
            switch (sizecode)
            {
                case 0:
                    res = a * b;
                    CF = OF = res != (sbyte)res;
                    break;
                case 1:
                    res = a * b;
                    CF = OF = res != (Int16)res;
                    break;
                case 2:
                    res = a * b;
                    CF = OF = res != (Int32)res;
                    break;
                case 3:
                    full = new BigInteger(a) * b;
                    res = (Int64)(full & 0xffffffffffffffff);
                    CF = OF = full != res;
                    break;
            }

            SF = Rand.NextBool();
            ZF = Rand.NextBool();
            AF = Rand.NextBool();
            PF = Rand.NextBool();

            Registers[s >> 4][sizecode] = (UInt64)res;
            return true;
        }

        private bool ProcessDIV()
        {
            if (!FetchIMMRMFormat(out UInt64 s, out UInt64 a)) return false;

            if (a == 0) { Terminate(ErrorCode.ArithmeticError); return false; }

            UInt64 full, quo, rem;
            BigInteger bigfull, bigquo, bigrem;

            // switch through register sizes
            switch ((s >> 2) & 3)
            {
                case 0:
                    full = AX;
                    quo = full / a; rem = full % a;
                    if (quo > 0xfful) { Terminate(ErrorCode.ArithmeticError); return false; }
                    AL = (byte)quo; AH = (byte)rem;
                    break;
                case 1:
                    full = ((UInt64)DX << 16) | AX;
                    quo = full / a; rem = full % a;
                    if (quo > 0xfffful) { Terminate(ErrorCode.ArithmeticError); return false; }
                    AX = (UInt16)quo; DX = (UInt16)rem;
                    break;
                case 2:
                    full = ((UInt64)EDX << 32) | EAX;
                    quo = full / a; rem = full % a;
                    if (quo > 0xfffffffful) { Terminate(ErrorCode.ArithmeticError); return false; }
                    EAX = (UInt32)quo; EDX = (UInt32)rem;
                    break;
                case 3:
                    bigfull = (new BigInteger(RDX) << 64) | RAX;
                    bigquo = BigInteger.DivRem(bigfull, a, out bigrem);
                    if (bigquo > 0xfffffffffffffffful) { Terminate(ErrorCode.ArithmeticError); return false; }
                    RAX = (UInt64)bigquo; RDX = (UInt64)bigrem;
                    break;
            }

            CF = Rand.NextBool();
            OF = Rand.NextBool();
            SF = Rand.NextBool();
            ZF = Rand.NextBool();
            AF = Rand.NextBool();
            PF = Rand.NextBool();

            return true;
        }
        private bool ProcessIDIV()
        {
            if (!FetchIMMRMFormat(out UInt64 s, out UInt64 _a)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            if (_a == 0) { Terminate(ErrorCode.ArithmeticError); return false; }

            // get val as signed
            Int64 a = (Int64)SignExtend(_a, sizecode);

            Int64 full, quo, rem;
            BigInteger bigfull, bigquo, bigrem;

            // switch through register sizes
            switch ((s >> 2) & 3)
            {
                case 0:
                    full = AX;
                    quo = full / a; rem = full % a;
                    if (quo != (sbyte)quo) { Terminate(ErrorCode.ArithmeticError); return false; }
                    AL = (byte)quo; AH = (byte)rem;
                    break;
                case 1:
                    full = (DX << 16) | AX;
                    quo = full / a; rem = full % a;
                    if (quo != (Int16)quo) { Terminate(ErrorCode.ArithmeticError); return false; }
                    AX = (UInt16)quo; DX = (UInt16)rem;
                    break;
                case 2:
                    full = ((Int64)EDX << 32) | EAX;
                    quo = full / a; rem = full % a;
                    if (quo != (Int32)quo) { Terminate(ErrorCode.ArithmeticError); return false; }
                    EAX = (UInt32)quo; EDX = (UInt32)rem;
                    break;
                case 3:
                    bigfull = (new BigInteger((Int64)RDX) << 64) | (Int64)RAX;
                    bigquo = BigInteger.DivRem(bigfull, a, out bigrem);
                    if (bigquo > Int64.MaxValue || bigquo < Int64.MinValue) { Terminate(ErrorCode.ArithmeticError); return false; }
                    RAX = (UInt64)bigquo; RDX = (UInt64)bigrem;
                    break;
            }

            CF = Rand.NextBool();
            OF = Rand.NextBool();
            SF = Rand.NextBool();
            ZF = Rand.NextBool();
            AF = Rand.NextBool();
            PF = Rand.NextBool();

            return true;
        }

        private bool ProcessSHL()
        {
            if (!FetchBinaryOpFormat(out UInt64 s, out UInt64 m, out UInt64 a, out UInt64 b, true, -1, 0)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UInt16 sh = (UInt16)(b & (sizecode == 3 ? 0x3ful : 0x1ful)); // mask shift val to 6 bits on 64-bit op, otherwise to 5 bits

            // shift of zero is no-op
            if (sh != 0)
            {
                UInt64 res = Truncate(a << sh, sizecode);

                UpdateFlagsInt(res, sizecode);
                CF = sh < SizeBits(sizecode) ? ((a >> ((UInt16)SizeBits(sizecode) - sh)) & 1) == 1 : Rand.NextBool(); // CF holds last bit shifted out (UND for sh >= #bits)
                OF = sh == 1 ? Negative(res, sizecode) != CF : Rand.NextBool(); // OF is 1 if top 2 bits of original value were different (UND for sh != 1)
                AF = Rand.NextBool(); // AF is undefined

                return StoreBinaryOpFormat(s, m, res);
            }
            else return true;
        }
        private bool ProcessSHR()
        {
            if (!FetchBinaryOpFormat(out UInt64 s, out UInt64 m, out UInt64 a, out UInt64 b, true, -1, 0)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UInt16 sh = (UInt16)(b & (sizecode == 3 ? 0x3ful : 0x1ful)); // mask shift val to 6 bits on 64-bit op, otherwise to 5 bits

            // shift of zero is no-op
            if (sh != 0)
            {
                UInt64 res = a >> sh;

                UpdateFlagsInt(res, sizecode);
                CF = sh < SizeBits(sizecode) ? ((a >> (sh - 1)) & 1) == 1 : Rand.NextBool(); // CF holds last bit shifted out (UND for sh >= #bits)
                OF = sh == 1 ? Negative(a, sizecode) : Rand.NextBool(); // OF is high bit of original value (UND for sh != 1)
                AF = Rand.NextBool(); // AF is undefined

                return StoreBinaryOpFormat(s, m, res);
            }
            else return true;
        }

        private bool ProcessSAL()
        {
            if (!FetchBinaryOpFormat(out UInt64 s, out UInt64 m, out UInt64 a, out UInt64 b, true, -1, 0)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UInt16 sh = (UInt16)(b & (sizecode == 3 ? 0x3ful : 0x1ful)); // mask shift val to 6 bits on 64-bit op, otherwise to 5 bits

            // shift of zero is no-op
            if (sh != 0)
            {
                UInt64 res = Truncate((UInt64)((Int64)SignExtend(a, sizecode) << sh), sizecode);

                UpdateFlagsInt(res, sizecode);
                CF = sh < SizeBits(sizecode) ? ((a >> ((UInt16)SizeBits(sizecode) - sh)) & 1) == 1 : Rand.NextBool(); // CF holds last bit shifted out (UND for sh >= #bits)
                OF = sh == 1 ? Negative(res, sizecode) != CF : Rand.NextBool(); // OF is 1 if top 2 bits of original value were different (UND for sh != 1)
                AF = Rand.NextBool(); // AF is undefined

                return StoreBinaryOpFormat(s, m, res);
            }
            else return true;
        }
        private bool ProcessSAR()
        {
            if (!FetchBinaryOpFormat(out UInt64 s, out UInt64 m, out UInt64 a, out UInt64 b, true, -1, 0)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UInt16 sh = (UInt16)(b & (sizecode == 3 ? 0x3ful : 0x1ful)); // mask shift val to 6 bits on 64-bit op, otherwise to 5 bits

            // shift of zero is no-op
            if (sh != 0)
            {
                UInt64 res = Truncate((UInt64)((Int64)SignExtend(a, sizecode) >> sh), sizecode);

                UpdateFlagsInt(res, sizecode);
                CF = sh < SizeBits(sizecode) ? ((a >> (sh - 1)) & 1) == 1 : Rand.NextBool(); // CF holds last bit shifted out (UND for sh >= #bits)
                OF = sh == 1 ? false : Rand.NextBool(); // OF is cleared (UND for sh != 1)
                AF = Rand.NextBool(); // AF is undefined

                return StoreBinaryOpFormat(s, m, res);
            }
            else return false;
        }

        private bool ProcessROL()
        {
            if (!FetchBinaryOpFormat(out UInt64 s, out UInt64 m, out UInt64 a, out UInt64 b, true, -1, 0)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UInt16 sh = (UInt16)(b % SizeBits(sizecode)); // rotate performed modulo-n

            // shift of zero is no-op
            if (sh != 0)
            {
                UInt64 res = Truncate((a << sh) | (a >> ((UInt16)SizeBits(sizecode) - sh)), sizecode);

                CF = ((a >> ((UInt16)SizeBits(sizecode) - sh)) & 1) == 1; // CF holds last bit shifted around
                OF = sh == 1 ? CF ^ Negative(res, sizecode) : Rand.NextBool(); // OF is xor of CF (after rotate) and high bit of result (UND if sh != 1)

                return StoreBinaryOpFormat(s, m, res);
            }
            else return true;
        }
        private bool ProcessROR()
        {
            if (!FetchBinaryOpFormat(out UInt64 s, out UInt64 m, out UInt64 a, out UInt64 b, true, -1, 0)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UInt16 sh = (UInt16)(b % SizeBits(sizecode)); // rotate performed modulo-n

            // shift of zero is no-op
            if (sh != 0)
            {
                UInt64 res = Truncate((a >> sh) | (a << ((UInt16)SizeBits(sizecode) - sh)), sizecode);

                CF = ((a >> (sh - 1)) & 1) == 1; // CF holds last bit shifted around
                OF = sh == 1 ? Negative(res, sizecode) ^ (((res >> ((8 << (ushort)sizecode) - 2)) & 1) != 0) : Rand.NextBool(); // OF is xor of 2 highest bits of result

                return StoreBinaryOpFormat(s, m, res);
            }
            else return true;
        }

        private bool ProcessAND(bool apply = true)
        {
            if (!FetchBinaryOpFormat(out UInt64 s, out UInt64 m, out UInt64 a, out UInt64 b)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UInt64 res = a & b;

            UpdateFlagsInt(res, sizecode);
            OF = CF = false;
            AF = Rand.NextBool();

            return !apply || StoreBinaryOpFormat(s, m, res);
        }
        private bool ProcessOR()
        {
            if (!FetchBinaryOpFormat(out UInt64 s, out UInt64 m, out UInt64 a, out UInt64 b)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UInt64 res = a | b;

            UpdateFlagsInt(res, sizecode);
            OF = CF = false;
            AF = Rand.NextBool();

            return StoreBinaryOpFormat(s, m, res);
        }
        private bool ProcessXOR()
        {
            if (!FetchBinaryOpFormat(out UInt64 s, out UInt64 m, out UInt64 a, out UInt64 b)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UInt64 res = a ^ b;

            UpdateFlagsInt(res, sizecode);
            OF = CF = false;
            AF = Rand.NextBool();

            return StoreBinaryOpFormat(s, m, res);
        }

        private bool ProcessINC()
        {
            if (!FetchUnaryOpFormat(out UInt64 s, out UInt64 m, out UInt64 a)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UInt64 res = Truncate(a + 1, sizecode);

            UpdateFlagsInt(res, sizecode);
            AF = (res & 0xf) == 0; // low nibble of 0 was a nibble overflow (TM)
            OF = Positive(a, sizecode) && Negative(res, sizecode); // + -> - is overflow

            return StoreUnaryOpFormat(s, m, res);
        }
        private bool ProcessDEC()
        {
            if (!FetchUnaryOpFormat(out UInt64 s, out UInt64 m, out UInt64 a)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UInt64 res = Truncate(a - 1, sizecode);

            UpdateFlagsInt(res, sizecode);
            AF = (a & 0xf) == 0; // nibble a = 0 results in borrow from the low nibble
            OF = Negative(a, sizecode) && Positive(res, sizecode); // - -> + is overflow

            return StoreUnaryOpFormat(s, m, res);
        }
        private bool ProcessNEG()
        {
            if (!FetchUnaryOpFormat(out UInt64 s, out UInt64 m, out UInt64 a)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UInt64 res = Truncate(0 - a, sizecode);

            UpdateFlagsInt(res, sizecode);
            CF = 0 < a; // if 0 < a, a borrow was taken from the highest bit (see SUB code where a=0, b=a)
            AF = 0 < (a & 0xf); // AF is just like CF but only the low nibble
            OF = Negative(a, sizecode) && Negative(res, sizecode);

            return StoreUnaryOpFormat(s, m, res);
        }
        private bool ProcessNOT()
        {
            if (!FetchUnaryOpFormat(out UInt64 s, out UInt64 m, out UInt64 a)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UInt64 res = Truncate(~a, sizecode);

            return StoreUnaryOpFormat(s, m, res);
        }

        private bool ProcessCMPZ()
        {
            if (!FetchUnaryOpFormat(out UInt64 s, out UInt64 m, out UInt64 a)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UpdateFlagsInt(a, sizecode);
            CF = OF = AF = false;

            return true;
        }
        private bool ProcessFCMPZ()
        {
            if (!FetchUnaryOpFormat(out UInt64 s, out UInt64 m, out UInt64 a)) return false;

            switch ((s >> 2) & 3)
            {
                case 3:
                    {
                        double res = AsDouble(a);

                        UpdateFlagsDouble(res);

                        return true;
                    }
                case 2:
                    {
                        float res = AsFloat(a);

                        UpdateFlagsFloat(res);

                        return true;
                    }

                default: Terminate(ErrorCode.UndefinedBehavior); return false;
            }
        }

        private bool ProcessFADD()
        {
            if (!FetchBinaryOpFormat(out UInt64 s, out UInt64 m, out UInt64 a, out UInt64 b)) return false;

            switch ((s >> 2) & 3)
            {
                case 3:
                    {
                        double res = AsDouble(a) + AsDouble(b);

                        return StoreBinaryOpFormat(s, m, DoubleAsUInt64(res));
                    }
                case 2:
                    {
                        float res = AsFloat(a) + AsFloat(b);

                        return StoreBinaryOpFormat(s, m, FloatAsUInt64(res));
                    }

                default: Terminate(ErrorCode.UndefinedBehavior); return false;
            }
        }
        private bool ProcessFSUB(bool apply = true)
        {
            if (!FetchBinaryOpFormat(out UInt64 s, out UInt64 m, out UInt64 a, out UInt64 b)) return false;

            switch ((s >> 2) & 3)
            {
                case 3:
                    {
                        double res = AsDouble(a) - AsDouble(b);

                        // if applying change, is FSUB
                        if (apply) return StoreBinaryOpFormat(s, m, DoubleAsUInt64(res));
                        // otherwise is an FCMP
                        else
                        {
                            // update flags
                            UpdateFlagsDouble(res);
                            return true;
                        }
                    }
                case 2:
                    {
                        float res = AsFloat(a) - AsFloat(b);

                        // if applying change, is FSUB
                        if (apply) return StoreBinaryOpFormat(s, m, FloatAsUInt64(res));
                        // otherwise is an FCMP
                        else
                        {
                            // update flags
                            UpdateFlagsFloat(res);
                            return true;
                        }
                    }

                default: Terminate(ErrorCode.UndefinedBehavior); return false;
            }
        }
        private bool ProcessFSUBR()
        {
            if (!FetchBinaryOpFormat(out UInt64 s, out UInt64 m, out UInt64 a, out UInt64 b)) return false;

            switch ((s >> 2) & 3)
            {
                case 3:
                    {
                        double res = AsDouble(b) - AsDouble(a);

                        return StoreBinaryOpFormat(s, m, DoubleAsUInt64(res));
                    }
                case 2:
                    {
                        float res = AsFloat(b) - AsFloat(a);

                        return StoreBinaryOpFormat(s, m, FloatAsUInt64(res));
                    }

                default: Terminate(ErrorCode.UndefinedBehavior); return false;
            }
        }

        private bool ProcessFMUL()
        {
            if (!FetchBinaryOpFormat(out UInt64 s, out UInt64 m, out UInt64 a, out UInt64 b)) return false;

            switch ((s >> 2) & 3)
            {
                case 3:
                    {
                        double res = AsDouble(a) * AsDouble(b);

                        return StoreBinaryOpFormat(s, m, DoubleAsUInt64(res));
                    }
                case 2:
                    {
                        float res = AsFloat(a) * AsFloat(b);

                        return StoreBinaryOpFormat(s, m, FloatAsUInt64(res));
                    }

                default: Terminate(ErrorCode.UndefinedBehavior); return false;
            }
        }

        private bool ProcessFDIV()
        {
            if (!FetchBinaryOpFormat(out UInt64 s, out UInt64 m, out UInt64 a, out UInt64 b)) return false;

            switch ((s >> 2) & 3)
            {
                case 3:
                    {
                        double res = AsDouble(a) / AsDouble(b);

                        return StoreBinaryOpFormat(s, m, DoubleAsUInt64(res));
                    }
                case 2:
                    {
                        float res = AsFloat(a) / AsFloat(b);

                        return StoreBinaryOpFormat(s, m, FloatAsUInt64(res));
                    }

                default: Terminate(ErrorCode.UndefinedBehavior); return false;
            }
        }
        private bool ProcessFDIVR()
        {
            if (!FetchBinaryOpFormat(out UInt64 s, out UInt64 m, out UInt64 a, out UInt64 b)) return false;

            switch ((s >> 2) & 3)
            {
                case 3:
                    {
                        double res = AsDouble(b) / AsDouble(a);

                        return StoreBinaryOpFormat(s, m, DoubleAsUInt64(res));
                    }
                case 2:
                    {
                        float res = AsFloat(b) / AsFloat(a);

                        return StoreBinaryOpFormat(s, m, FloatAsUInt64(res));
                    }

                default: Terminate(ErrorCode.UndefinedBehavior); return false;
            }
        }

        private bool ProcessFPOW()
        {
            if (!FetchBinaryOpFormat(out UInt64 s, out UInt64 m, out UInt64 a, out UInt64 b)) return false;

            switch ((s >> 2) & 3)
            {
                case 3:
                    {
                        double res = Math.Pow(AsDouble(a), AsDouble(b));

                        return StoreBinaryOpFormat(s, m, DoubleAsUInt64(res));
                    }
                case 2:
                    {
                        float res = (float)Math.Pow(AsFloat(a), AsFloat(b));

                        return StoreBinaryOpFormat(s, m, FloatAsUInt64(res));
                    }

                default: Terminate(ErrorCode.UndefinedBehavior); return false;
            }
        }
        private bool ProcessFPOWR()
        {
            if (!FetchBinaryOpFormat(out UInt64 s, out UInt64 m, out UInt64 a, out UInt64 b)) return false;

            switch ((s >> 2) & 3)
            {
                case 3:
                    {
                        double res = Math.Pow(AsDouble(b), AsDouble(a));

                        return StoreBinaryOpFormat(s, m, DoubleAsUInt64(res));
                    }
                case 2:
                    {
                        float res = (float)Math.Pow(AsFloat(b), AsFloat(a));

                        return StoreBinaryOpFormat(s, m, FloatAsUInt64(res));
                    }

                default: Terminate(ErrorCode.UndefinedBehavior); return false;
            }
        }

        private bool ProcessFLOG()
        {
            if (!FetchBinaryOpFormat(out UInt64 s, out UInt64 m, out UInt64 a, out UInt64 b)) return false;

            switch ((s >> 2) & 3)
            {
                case 3:
                    {
                        double res = Math.Log(AsDouble(a), AsDouble(b));

                        return StoreBinaryOpFormat(s, m, DoubleAsUInt64(res));
                    }
                case 2:
                    {
                        float res = (float)Math.Log(AsFloat(a), AsFloat(b));

                        return StoreBinaryOpFormat(s, m, FloatAsUInt64(res));
                    }

                default: Terminate(ErrorCode.UndefinedBehavior); return false;
            }
        }
        private bool ProcessFLOGR()
        {
            if (!FetchBinaryOpFormat(out UInt64 s, out UInt64 m, out UInt64 a, out UInt64 b)) return false;

            switch ((s >> 2) & 3)
            {
                case 3:
                    {
                        double res = Math.Log(AsDouble(b), AsDouble(a));

                        return StoreBinaryOpFormat(s, m, DoubleAsUInt64(res));
                    }
                case 2:
                    {
                        float res = (float)Math.Log(AsFloat(b), AsFloat(a));

                        return StoreBinaryOpFormat(s, m, FloatAsUInt64(res));
                    }

                default: Terminate(ErrorCode.UndefinedBehavior); return false;
            }
        }

        private bool ProcessFSQRT()
        {
            if (!FetchUnaryOpFormat(out UInt64 s, out UInt64 m, out UInt64 a)) return false;

            switch ((s >> 2) & 3)
            {
                case 3:
                    {
                        double res = Math.Sqrt(AsDouble(a));

                        return StoreUnaryOpFormat(s, m, DoubleAsUInt64(res));
                    }
                case 2:
                    {
                        float res = (float)Math.Sqrt(AsFloat(a));

                        return StoreUnaryOpFormat(s, m, FloatAsUInt64(res));
                    }

                default: Terminate(ErrorCode.UndefinedBehavior); return false;
            }
        }
        private bool ProcessFNEG()
        {
            if (!FetchUnaryOpFormat(out UInt64 s, out UInt64 m, out UInt64 a)) return false;

            switch ((s >> 2) & 3)
            {
                case 3:
                    {
                        double res = -AsDouble(a);

                        return StoreUnaryOpFormat(s, m, DoubleAsUInt64(res));
                    }
                case 2:
                    {
                        float res = -AsFloat(a);

                        return StoreUnaryOpFormat(s, m, FloatAsUInt64(res));
                    }

                default: Terminate(ErrorCode.UndefinedBehavior); return false;
            }
        }
        private bool ProcessFABS()
        {
            if (!FetchUnaryOpFormat(out UInt64 s, out UInt64 m, out UInt64 a)) return false;

            switch ((s >> 2) & 3)
            {
                case 3:
                    {
                        double res = Math.Abs(AsDouble(a));

                        return StoreUnaryOpFormat(s, m, DoubleAsUInt64(res));
                    }
                case 2:
                    {
                        float res = Math.Abs(AsFloat(a));

                        return StoreUnaryOpFormat(s, m, FloatAsUInt64(res));
                    }

                default: Terminate(ErrorCode.UndefinedBehavior); return false;
            }
        }

        private bool ProcessFFLOOR()
        {
            if (!FetchUnaryOpFormat(out UInt64 s, out UInt64 m, out UInt64 a)) return false;

            switch ((s >> 2) & 3)
            {
                case 3:
                    {
                        double res = Math.Floor(AsDouble(a));

                        return StoreUnaryOpFormat(s, m, DoubleAsUInt64(res));
                    }
                case 2:
                    {
                        float res = (float)Math.Floor(AsFloat(a));

                        return StoreUnaryOpFormat(s, m, FloatAsUInt64(res));
                    }

                default: Terminate(ErrorCode.UndefinedBehavior); return false;
            }
        }
        private bool ProcessFCEIL()
        {
            if (!FetchUnaryOpFormat(out UInt64 s, out UInt64 m, out UInt64 a)) return false;

            switch ((s >> 2) & 3)
            {
                case 3:
                    {
                        double res = Math.Ceiling(AsDouble(a));

                        return StoreUnaryOpFormat(s, m, DoubleAsUInt64(res));
                    }
                case 2:
                    {
                        float res = (float)Math.Ceiling(AsFloat(a));

                        return StoreUnaryOpFormat(s, m, FloatAsUInt64(res));
                    }

                default: Terminate(ErrorCode.UndefinedBehavior); return false;
            }
        }
        private bool ProcessFROUND()
        {
            if (!FetchUnaryOpFormat(out UInt64 s, out UInt64 m, out UInt64 a)) return false;

            switch ((s >> 2) & 3)
            {
                case 3:
                    {
                        double res = Math.Round(AsDouble(a));

                        return StoreUnaryOpFormat(s, m, DoubleAsUInt64(res));
                    }
                case 2:
                    {
                        float res = (float)Math.Round(AsFloat(a));

                        return StoreUnaryOpFormat(s, m, FloatAsUInt64(res));
                    }

                default: Terminate(ErrorCode.UndefinedBehavior); return false;
            }
        }
        private bool ProcessFTRUNC()
        {
            if (!FetchUnaryOpFormat(out UInt64 s, out UInt64 m, out UInt64 a)) return false;

            switch ((s >> 2) & 3)
            {
                case 3:
                    {
                        double res = Math.Truncate(AsDouble(a));

                        return StoreUnaryOpFormat(s, m, DoubleAsUInt64(res));
                    }
                case 2:
                    {
                        float res = (float)Math.Truncate(AsFloat(a));

                        return StoreUnaryOpFormat(s, m, FloatAsUInt64(res));
                    }

                default: Terminate(ErrorCode.UndefinedBehavior); return false;
            }
        }

        private bool ProcessFSIN()
        {
            if (!FetchUnaryOpFormat(out UInt64 s, out UInt64 m, out UInt64 a)) return false;

            switch ((s >> 2) & 3)
            {
                case 3:
                    {
                        double res = Math.Sin(AsDouble(a));

                        return StoreUnaryOpFormat(s, m, DoubleAsUInt64(res));
                    }
                case 2:
                    {
                        float res = (float)Math.Sin(AsFloat(a));

                        return StoreUnaryOpFormat(s, m, FloatAsUInt64(res));
                    }

                default: Terminate(ErrorCode.UndefinedBehavior); return false;
            }
        }
        private bool ProcessFCOS()
        {
            if (!FetchUnaryOpFormat(out UInt64 s, out UInt64 m, out UInt64 a)) return false;

            switch ((s >> 2) & 3)
            {
                case 3:
                    {
                        double res = Math.Cos(AsDouble(a));

                        return StoreUnaryOpFormat(s, m, DoubleAsUInt64(res));
                    }
                case 2:
                    {
                        float res = (float)Math.Cos(AsFloat(a));

                        return StoreUnaryOpFormat(s, m, FloatAsUInt64(res));
                    }

                default: Terminate(ErrorCode.UndefinedBehavior); return false;
            }
        }
        private bool ProcessFTAN()
        {
            if (!FetchUnaryOpFormat(out UInt64 s, out UInt64 m, out UInt64 a)) return false;

            switch ((s >> 2) & 3)
            {
                case 3:
                    {
                        double res = Math.Tan(AsDouble(a));

                        return StoreUnaryOpFormat(s, m, DoubleAsUInt64(res));
                    }
                case 2:
                    {
                        float res = (float)Math.Tan(AsFloat(a));

                        return StoreUnaryOpFormat(s, m, FloatAsUInt64(res));
                    }

                default: Terminate(ErrorCode.UndefinedBehavior); return false;
            }
        }

        private bool ProcessFSINH()
        {
            if (!FetchUnaryOpFormat(out UInt64 s, out UInt64 m, out UInt64 a)) return false;

            switch ((s >> 2) & 3)
            {
                case 3:
                    {
                        double res = Math.Sinh(AsDouble(a));

                        return StoreUnaryOpFormat(s, m, DoubleAsUInt64(res));
                    }
                case 2:
                    {
                        float res = (float)Math.Sinh(AsFloat(a));

                        return StoreUnaryOpFormat(s, m, FloatAsUInt64(res));
                    }

                default: Terminate(ErrorCode.UndefinedBehavior); return false;
            }
        }
        private bool ProcessFCOSH()
        {
            if (!FetchUnaryOpFormat(out UInt64 s, out UInt64 m, out UInt64 a)) return false;

            switch ((s >> 2) & 3)
            {
                case 3:
                    {
                        double res = Math.Cosh(AsDouble(a));

                        return StoreUnaryOpFormat(s, m, DoubleAsUInt64(res));
                    }
                case 2:
                    {
                        float res = (float)Math.Cosh(AsFloat(a));

                        return StoreUnaryOpFormat(s, m, FloatAsUInt64(res));
                    }

                default: Terminate(ErrorCode.UndefinedBehavior); return false;
            }
        }
        private bool ProcessFTANH()
        {
            if (!FetchUnaryOpFormat(out UInt64 s, out UInt64 m, out UInt64 a)) return false;

            switch ((s >> 2) & 3)
            {
                case 3:
                    {
                        double res = Math.Tanh(AsDouble(a));

                        return StoreUnaryOpFormat(s, m, DoubleAsUInt64(res));
                    }
                case 2:
                    {
                        float res = (float)Math.Tanh(AsFloat(a));

                        return StoreUnaryOpFormat(s, m, FloatAsUInt64(res));
                    }

                default: Terminate(ErrorCode.UndefinedBehavior); return false;
            }
        }

        private bool ProcessFASIN()
        {
            if (!FetchUnaryOpFormat(out UInt64 s, out UInt64 m, out UInt64 a)) return false;

            switch ((s >> 2) & 3)
            {
                case 3:
                    {
                        double res = Math.Asin(AsDouble(a));

                        return StoreUnaryOpFormat(s, m, DoubleAsUInt64(res));
                    }
                case 2:
                    {
                        float res = (float)Math.Asin(AsFloat(a));

                        return StoreUnaryOpFormat(s, m, FloatAsUInt64(res));
                    }

                default: Terminate(ErrorCode.UndefinedBehavior); return false;
            }
        }
        private bool ProcessFACOS()
        {
            if (!FetchUnaryOpFormat(out UInt64 s, out UInt64 m, out UInt64 a)) return false;

            switch ((s >> 2) & 3)
            {
                case 3:
                    {
                        double res = Math.Acos(AsDouble(a));

                        return StoreUnaryOpFormat(s, m, DoubleAsUInt64(res));
                    }
                case 2:
                    {
                        float res = (float)Math.Acos(AsFloat(a));

                        return StoreUnaryOpFormat(s, m, FloatAsUInt64(res));
                    }

                default: Terminate(ErrorCode.UndefinedBehavior); return false;
            }
        }
        private bool ProcessFATAN()
        {
            if (!FetchUnaryOpFormat(out UInt64 s, out UInt64 m, out UInt64 a)) return false;

            switch ((s >> 2) & 3)
            {
                case 3:
                    {
                        double res = Math.Atan(AsDouble(a));

                        return StoreUnaryOpFormat(s, m, DoubleAsUInt64(res));
                    }
                case 2:
                    {
                        float res = (float)Math.Atan(AsFloat(a));

                        return StoreUnaryOpFormat(s, m, FloatAsUInt64(res));
                    }

                default: Terminate(ErrorCode.UndefinedBehavior); return false;
            }
        }
        private bool ProcessFATAN2()
        {
            if (!FetchBinaryOpFormat(out UInt64 s, out UInt64 m, out UInt64 a, out UInt64 b)) return false;

            switch ((s >> 2) & 3)
            {
                case 3:
                    {
                        double res = Math.Atan2(AsDouble(a), AsDouble(b));

                        return StoreBinaryOpFormat(s, m, DoubleAsUInt64(res));
                    }
                case 2:
                    {
                        float res = (float)Math.Atan2(AsFloat(a), AsFloat(b));

                        return StoreBinaryOpFormat(s, m, FloatAsUInt64(res));
                    }

                default: Terminate(ErrorCode.UndefinedBehavior); return false;
            }
        }

        private bool ProcessFTOI()
        {
            if (!FetchUnaryOpFormat(out UInt64 s, out UInt64 m, out UInt64 a)) return false;

            switch ((s >> 2) & 3)
            {
                case 3: return StoreUnaryOpFormat(s, m, (UInt64)(Int64)AsDouble(a));
                case 2: return StoreUnaryOpFormat(s, m, (UInt64)(Int64)AsFloat(a));

                default: Terminate(ErrorCode.UndefinedBehavior); return false;
            }
        }
        private bool ProcessITOF()
        {
            if (!FetchUnaryOpFormat(out UInt64 s, out UInt64 m, out UInt64 a)) return false;

            switch ((s >> 2) & 3)
            {
                case 3: return StoreUnaryOpFormat(s, m, DoubleAsUInt64((Int64)a));
                case 2: return StoreUnaryOpFormat(s, m, FloatAsUInt64((Int64)SignExtend(a, 2)));

                default: Terminate(ErrorCode.UndefinedBehavior); return false;
            }
        }

        private bool ProcessBSWAP()
        {
            if (!FetchUnaryOpFormat(out UInt64 s, out UInt64 m, out UInt64 a)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UInt64 res = 0;
            switch (sizecode)
            {
                case 3:
                    res = (a << 56) | ((a & 0x000000000000ff00) << 40) | ((a & 0x0000000000ff0000) << 24) | ((a & 0x00000000ff000000) << 8)
                    | ((a & 0x000000ff00000000) >> 8) | ((a & 0x0000ff0000000000) >> 24) | ((a & 0x00ff000000000000) >> 40) | (a >> 56); break;
                case 2: res = (a << 24) | ((a & 0x0000ff00) << 8) | ((a & 0x00ff0000) >> 8) | (a >> 24); break;
                case 1: res = (a << 8) | (a >> 8); break;
                case 0: res = a; break;
            }

            return StoreUnaryOpFormat(s, m, res);
        }
        private bool ProcessBEXTR()
        {
            if (!FetchBinaryOpFormat(out UInt64 s, out UInt64 m, out UInt64 a, out UInt64 b, true, -1, 1)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            ushort pos = (ushort)((b >> 8) % SizeBits(sizecode));
            ushort len = (ushort)((b & 0xff) % SizeBits(sizecode));

            UInt64 res = (a >> pos) & ((1ul << len) - 1);

            EFLAGS = 2; // clear all the (public) flags (flag 1 must always be set)
            ZF = res == 0; // ZF is set on zero
            AF = Rand.NextBool(); // AF, SF, and PF are undefined
            SF = Rand.NextBool();
            PF = Rand.NextBool();

            return StoreBinaryOpFormat(s, m, res);
        }
        private bool ProcessBLSI()
        {
            if (!FetchUnaryOpFormat(out UInt64 s, out UInt64 m, out UInt64 a)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UInt64 res = a & (~a + 1);

            ZF = res == 0;
            SF = Negative(res, sizecode);
            CF = a != 0;
            OF = false;
            AF = Rand.NextBool();
            PF = Rand.NextBool();

            return StoreUnaryOpFormat(s, m, res);
        }
        private bool ProcessBLSMSK()
        {
            if (!FetchUnaryOpFormat(out UInt64 s, out UInt64 m, out UInt64 a)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UInt64 res = Truncate(a ^ (a - 1), sizecode);

            SF = Negative(res, sizecode);
            CF = a == 0;
            ZF = OF = false;
            AF = Rand.NextBool();
            PF = Rand.NextBool();

            return StoreUnaryOpFormat(s, m, res);
        }
        private bool ProcessBLSR()
        {
            if (!FetchUnaryOpFormat(out UInt64 s, out UInt64 m, out UInt64 a)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UInt64 res = a & (a - 1);

            ZF = res == 0;
            SF = Negative(res, sizecode);
            CF = a == 0;
            OF = false;
            AF = Rand.NextBool();
            PF = Rand.NextBool();

            return StoreUnaryOpFormat(s, m, res);
        }
        private bool ProcessANDN()
        {
            if (!FetchBinaryOpFormat(out UInt64 s, out UInt64 m, out UInt64 a, out UInt64 b)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UInt64 res = a & ~b;

            ZF = res == 0;
            SF = Negative(res, sizecode);
            OF = CF = false;
            AF = Rand.NextBool();
            PF = Rand.NextBool();

            return StoreBinaryOpFormat(s, m, res);
        }

        private bool ProcessBT()
        {
            if (!GetMemAdv(1, out UInt64 ext)) return false;

            if (!FetchBinaryOpFormat(out UInt64 s, out UInt64 m, out UInt64 a, out UInt64 b, true, -1, 0, false)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UInt64 mask = 1ul << (UInt16)(b % SizeBits(sizecode));

            CF = (a & mask) != 0;
            OF = Rand.NextBool();
            SF = Rand.NextBool();
            AF = Rand.NextBool();
            PF = Rand.NextBool();

            switch (ext)
            {
                case 0: return true;
                case 1: return StoreBinaryOpFormat(s, m, a | mask);
                case 2: return StoreBinaryOpFormat(s, m, a & ~mask);
                case 3: return StoreBinaryOpFormat(s, m, a ^ mask);

                default: Terminate(ErrorCode.UndefinedBehavior); return false;
            }
        }
    }
}
