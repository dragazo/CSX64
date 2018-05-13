﻿using System;
using System.Numerics;
using static CSX64.Utility;

// -- Operators -- //

namespace CSX64
{
    public partial class Computer
    {
        // -- op utilities -- //

        private bool FetchBinaryOpFormat(out UInt64 s, out UInt64 m, out UInt64 a, out UInt64 b, bool get_a = true, int _a_sizecode = -1, int _b_sizecode = -1)
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
                    if (get_a) a = Registers[s >> 4].Get(a_sizecode);
                    if (!GetMemAdv(Size(b_sizecode), out b)) return false;
                    return true;
                case 1:
                    if (get_a) a = Registers[s >> 4].Get(a_sizecode);
                    if (!GetAddressAdv(out b) || !GetMemRaw(b, Size(b_sizecode), out b)) return false;
                    return true;
                case 2:
                    if (!GetMemAdv(1, out b)) return false;
                    switch ((b >> 4) & 1)
                    {
                        case 0:
                            if (get_a) a = Registers[s >> 4].Get(a_sizecode);
                            b = Registers[b & 15].Get(b_sizecode);
                            return true;
                        case 1:
                            if (!GetAddressAdv(out m) || get_a && !GetMemRaw(m, Size(a_sizecode), out a)) return false;
                            b = Registers[b & 15].Get(b_sizecode);
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
                    Registers[s >> 4].Set(sizecode, res);
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
                    if (get_a) a = Registers[s >> 4].Get(a_sizecode);
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
                    Registers[s >> 4].Set(sizecode, res);
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
                case 1: a = Registers[s >> 4].Get((s >> 2) & 3); break;
                case 2: if (!GetAddressAdv(out a) || !GetMemRaw(a, Size((s >> 2) & 3), out a)) return false; break;
                default: Terminate(ErrorCode.UndefinedBehavior); { a = 0; return false; }
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

        // -- impl -- //

        private bool ProcessGETF()
        {
            if (!FetchUnaryOpFormat(out UInt64 s, out UInt64 m, out UInt64 a, false)) return false;

            return StoreUnaryOpFormat(s, m, Flags.Flags);
        }
        private bool ProcessSETF()
        {
            if (!FetchIMMRMFormat(out UInt64 s, out UInt64 a)) return false;

            Flags.SetPublicFlags(a);
            return true;
        }

        private bool ProcessSETcc()
        {
            if (!GetMemAdv(1, out UInt64 ext) || !Flags.TryGet_cc((ccOPCode)ext, out bool flag)) { Terminate(ErrorCode.UndefinedBehavior); return false; }

            if (!FetchUnaryOpFormat(out UInt64 s, out UInt64 m, out UInt64 a, false, 0)) return false;

            return StoreUnaryOpFormat(s, m, flag ? 1 : 0ul);
        }

        private bool ProcessMOV(bool apply = true)
        {
            if (!FetchBinaryOpFormat(out UInt64 s, out UInt64 m, out UInt64 a, out UInt64 b, false)) return false;

            return !apply || StoreBinaryOpFormat(s, m, b);
        }
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
                    if (!GetAddressAdv(out b) || !GetMemRaw(b, Size((a >> 2) & 3), out c)) return false;
                    d = Registers[a >> 4].x64;
                    Registers[a >> 4].Set((a >> 2) & 3, c);
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
                case 1: val = Registers[s >> 4].Get(sizecode); break;
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
            if (!GetMemAdv(1, out UInt64 s)) return false;

            UInt64 b = 0;
            switch (s & 1)
            {
                case 0: if (!GetMemAdv(Size((s >> 2) & 3), out b)) return false; break;
                case 1: b = Registers[s >> 4].x64; break;
            }

            return PushRaw(Size((s >> 2) & 3), b);
        }
        private bool ProcessPOP()
        {
            if (!GetMemAdv(1, out UInt64 s) || !PopRaw(Size((s >> 2) & 3), out UInt64 val)) return false;

            Registers[s >> 4].Set((s >> 2) & 3, val);
            return true;
        }

        private bool ProcessLEA()
        {
            if (!GetMemAdv(1, out UInt64 s) || !GetAddressAdv(out UInt64 address)) return false;

            Registers[s >> 4].Set((s >> 2) & 3, address);
            return true;
        }

        private bool ProcessFX()
        {
            if (!GetMemAdv(1, out UInt64 a)) return false;

            switch ((a >> 2) & 3)
            {
                case 3:
                    switch (a & 3)
                    {
                        case 3: return true;
                        case 2: Registers[a >> 4].x32 = FloatAsUInt64((float)AsDouble(Registers[a >> 4].x64)); return true;

                        default: Terminate(ErrorCode.UndefinedBehavior); return false;
                    }
                case 2:
                    switch (a & 3)
                    {
                        case 3: Registers[a >> 4].x64 = DoubleAsUInt64((double)AsFloat(Registers[a >> 4].x32)); return true;
                        case 2: return true;

                        default: Terminate(ErrorCode.UndefinedBehavior); return false;
                    }

                

                default: Terminate(ErrorCode.UndefinedBehavior); return false;
            }
        }

        private bool ProcessADD()
        {
            if (!FetchBinaryOpFormat(out UInt64 s, out UInt64 m, out UInt64 a, out UInt64 b)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UInt64 res = Truncate(a + b, sizecode);

            UpdateFlagsInt(res, sizecode);
            Flags.C = res < a && res < b; // if overflow is caused, some of one value must go toward it, so the truncated result must necessarily be less than both args
            Flags.O = Positive(a, sizecode) == Positive(b, sizecode) && Positive(a, sizecode) != Positive(res, sizecode);

            return StoreBinaryOpFormat(s, m, res);
        }
        private bool ProcessSUB(bool apply = true)
        {
            if (!FetchBinaryOpFormat(out UInt64 s, out UInt64 m, out UInt64 a, out UInt64 b)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UInt64 res = Truncate(a - b, sizecode);

            UpdateFlagsInt(res, sizecode);
            Flags.C = a < b; // if a < b, a borrow was taken from the highest bit
            Flags.O = Positive(a, sizecode) != Positive(b, sizecode) && Positive(a, sizecode) != Positive(res, sizecode);

            return !apply || StoreBinaryOpFormat(s, m, res);
        }

        private bool ProcessMUL()
        {
            if (!FetchIMMRMFormat(out UInt64 s, out UInt64 a)) return false;

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
        private bool ProcessBinary_IMUL()
        {
            if (!FetchBinaryOpFormat(out UInt64 s, out UInt64 m, out UInt64 a, out UInt64 b)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UInt64 res = Truncate(a * b, sizecode);

            UpdateFlagsInt(res, sizecode);

            return StoreBinaryOpFormat(s, m, res);
        }
        private bool ProcessTernary_IMUL()
        {
            Terminate(ErrorCode.NotImplemented);
            return false;
        }

        private bool ProcessDIV()
        {
            UInt64 s, a, full;
            BigInteger bigraw, bigfull;

            if (!FetchIMMRMFormat(out s, out a)) return false;

            if (a == 0) { Terminate(ErrorCode.ArithmeticError); return false; }

            // switch through register sizes
            switch ((s >> 2) & 3)
            {
                case 0:
                    full = Registers[0].x16 / a;
                    if ((full >> 8) != 0) { Terminate(ErrorCode.ArithmeticError); return false; }
                    Registers[1].x8 = Registers[0].x16 % a;
                    Registers[0].x8 = full;
                    Flags.C = Registers[1].x8 != 0;
                    break;
                case 1:
                    full = Registers[0].x32 / a;
                    if ((full >> 16) != 0) { Terminate(ErrorCode.ArithmeticError); return false; }
                    Registers[1].x16 = Registers[0].x32 % a;
                    Registers[0].x16 = full;
                    Flags.C = Registers[1].x16 != 0;
                    break;
                case 2:
                    full = Registers[0].x64 / a;
                    if ((full >> 32) != 0) { Terminate(ErrorCode.ArithmeticError); return false; }
                    Registers[1].x32 = Registers[0].x64 % a;
                    Registers[0].x32 = full;
                    Flags.C = Registers[1].x32 != 0;
                    break;
                case 3: // 64 bits requires extra logic
                    bigraw = (new BigInteger(Registers[1].x64) << 64) | new BigInteger(Registers[0].x64);
                    bigfull = bigraw / new BigInteger(a);

                    if ((bigfull >> 64) != 0) { Terminate(ErrorCode.ArithmeticError); return false; }

                    Registers[1].x64 = (UInt64)(bigraw % new BigInteger(a));
                    Registers[0].x64 = (UInt64)bigfull;
                    Flags.C = Registers[1].x64 != 0;
                    break;
            }

            return true;
        }
        private bool ProcessIDIV()
        {
            UInt64 s, a;
            Int64 _a, _b, full;
            BigInteger bigraw, bigfull;

            if (!FetchIMMRMFormat(out s, out a)) return false;

            if (a == 0) { Terminate(ErrorCode.ArithmeticError); return false; }

            // switch through register sizes
            switch ((s >> 2) & 3)
            {
                case 0:
                    _a = SignExtend(Registers[0].x16, 1).MakeSigned();
                    _b = SignExtend(a, 0).MakeSigned();
                    full = _a / _b;

                    if (full != (sbyte)full) { Terminate(ErrorCode.ArithmeticError); return false; }

                    Registers[0].x8 = full.MakeUnsigned();
                    Registers[1].x8 = (_a % _b).MakeUnsigned();
                    Flags.C = Registers[1].x8 != 0;
                    Flags.S = Negative(Registers[0].x8, 0);
                    break;
                case 1:
                    _a = SignExtend(Registers[0].x32, 2).MakeSigned();
                    _b = SignExtend(a, 1).MakeSigned();
                    full = _a / _b;

                    if (full != (Int16)full) { Terminate(ErrorCode.ArithmeticError); return false; }

                    Registers[0].x16 = full.MakeUnsigned();
                    Registers[1].x16 = (_a % _b).MakeUnsigned();
                    Flags.C = Registers[1].x16 != 0;
                    Flags.S = Negative(Registers[0].x16, 1);
                    break;
                case 2:
                    _a = Registers[0].x64.MakeSigned();
                    _b = SignExtend(a, 2).MakeSigned();
                    full = _a / _b;

                    if (full != (Int32)full) { Terminate(ErrorCode.ArithmeticError); return false; }

                    Registers[0].x32 = full.MakeUnsigned();
                    Registers[1].x32 = (_a % _b).MakeUnsigned();
                    Flags.C = Registers[1].x32 != 0;
                    Flags.S = Negative(Registers[0].x32, 2);
                    break;
                case 3: // 64 bits requires extra logic
                    _b = a.MakeSigned();
                    bigraw = (new BigInteger(Registers[1].x64.MakeSigned()) << 64) + new BigInteger(Registers[0].x64.MakeSigned());
                    bigfull = bigraw / _b;

                    if (bigfull != (Int64)bigfull) { Terminate(ErrorCode.ArithmeticError); return false; }

                    Registers[1].x64 = ((Int64)(bigraw % _b)).MakeUnsigned();
                    Registers[0].x64 = ((Int64)bigfull).MakeUnsigned();
                    Flags.C = Registers[1].x64 != 0;
                    break;
            }

            return true;
        }

        private bool ProcessSHL()
        {
            if (!FetchBinaryOpFormat(out UInt64 s, out UInt64 m, out UInt64 a, out UInt64 b, true, -1, 0)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UInt16 sh = (UInt16)(b % SizeBits(sizecode));
            UInt64 res = Truncate(a << sh, sizecode);

            UpdateFlagsInt(res, sizecode);

            return StoreBinaryOpFormat(s, m, res);
        }
        private bool ProcessSHR()
        {
            if (!FetchBinaryOpFormat(out UInt64 s, out UInt64 m, out UInt64 a, out UInt64 b, true, -1, 0)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UInt16 sh = (UInt16)(b % SizeBits(sizecode));
            UInt64 res = a >> sh;

            UpdateFlagsInt(res, sizecode);

            return StoreBinaryOpFormat(s, m, res);
        }

        private bool ProcessSAL()
        {
            if (!FetchBinaryOpFormat(out UInt64 s, out UInt64 m, out UInt64 a, out UInt64 b, true, -1, 0)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UInt16 sh = (UInt16)(b % SizeBits(sizecode));
            UInt64 res = Truncate((SignExtend(a, sizecode).MakeSigned() << sh).MakeUnsigned(), sizecode);

            UpdateFlagsInt(res, sizecode);

            return StoreBinaryOpFormat(s, m, res);
        }
        private bool ProcessSAR()
        {
            if (!FetchBinaryOpFormat(out UInt64 s, out UInt64 m, out UInt64 a, out UInt64 b, true, -1, 0)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UInt16 sh = (UInt16)(b % SizeBits(sizecode));
            UInt64 res = Truncate((SignExtend(a, sizecode).MakeSigned() >> sh).MakeUnsigned(), sizecode);

            UpdateFlagsInt(res, sizecode);

            return StoreBinaryOpFormat(s, m, res);
        }

        private bool ProcessROL()
        {
            if (!FetchBinaryOpFormat(out UInt64 s, out UInt64 m, out UInt64 a, out UInt64 b, true, -1, 0)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UInt16 sh = (UInt16)(b % SizeBits(sizecode));
            UInt64 res = Truncate((a << sh) | (a >> ((UInt16)SizeBits(sizecode) - sh)), sizecode);

            UpdateFlagsInt(res, sizecode);

            return StoreBinaryOpFormat(s, m, res);
        }
        private bool ProcessROR()
        {
            if (!FetchBinaryOpFormat(out UInt64 s, out UInt64 m, out UInt64 a, out UInt64 b, true, -1, 0)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UInt16 sh = (UInt16)(b % SizeBits(sizecode));
            UInt64 res = Truncate((a >> sh) | (a << ((UInt16)SizeBits(sizecode) - sh)), sizecode);

            UpdateFlagsInt(res, sizecode);

            return StoreBinaryOpFormat(s, m, res);
        }

        private bool ProcessAND(bool apply = true)
        {
            if (!FetchBinaryOpFormat(out UInt64 s, out UInt64 m, out UInt64 a, out UInt64 b)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UInt64 res = a & b;

            UpdateFlagsInt(res, sizecode);

            return !apply || StoreBinaryOpFormat(s, m, res);
        }
        private bool ProcessOR()
        {
            if (!FetchBinaryOpFormat(out UInt64 s, out UInt64 m, out UInt64 a, out UInt64 b)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UInt64 res = a | b;

            UpdateFlagsInt(res, sizecode);

            return StoreBinaryOpFormat(s, m, res);
        }
        private bool ProcessXOR()
        {
            if (!FetchBinaryOpFormat(out UInt64 s, out UInt64 m, out UInt64 a, out UInt64 b)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UInt64 res = a ^ b;

            UpdateFlagsInt(res, sizecode);

            return StoreBinaryOpFormat(s, m, res);
        }

        private bool ProcessINC()
        {
            if (!FetchUnaryOpFormat(out UInt64 s, out UInt64 m, out UInt64 a)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UInt64 res = Truncate(a + 1, sizecode);

            UpdateFlagsInt(res, sizecode);
            Flags.C = res == 0; // carry results in zero
            Flags.O = Positive(a, sizecode) && Negative(res, sizecode); // + -> - is overflow

            return StoreUnaryOpFormat(s, m, res);
        }
        private bool ProcessDEC()
        {
            if (!FetchUnaryOpFormat(out UInt64 s, out UInt64 m, out UInt64 a)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UInt64 res = Truncate(a - 1, sizecode);

            UpdateFlagsInt(res, sizecode);
            Flags.C = a == 0; // a = 0 results in borrow from high bit (carry)
            Flags.O = Negative(a, sizecode) && Positive(res, sizecode); // - -> + is overflow

            return StoreUnaryOpFormat(s, m, res);
        }
        private bool ProcessNEG()
        {
            if (!FetchUnaryOpFormat(out UInt64 s, out UInt64 m, out UInt64 a)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UInt64 res = Truncate(~a + 1, sizecode);

            UpdateFlagsInt(res, sizecode);

            return StoreUnaryOpFormat(s, m, res);
        }
        private bool ProcessNOT()
        {
            if (!FetchUnaryOpFormat(out UInt64 s, out UInt64 m, out UInt64 a)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UInt64 res = Truncate(~a, sizecode);

            UpdateFlagsInt(res, sizecode);

            return StoreUnaryOpFormat(s, m, res);
        }
        private bool ProcessABS()
        {
            if (!FetchUnaryOpFormat(out UInt64 s, out UInt64 m, out UInt64 a)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UInt64 res = Positive(a, sizecode) ? a : Truncate(~a + 1, sizecode);

            UpdateFlagsInt(res, sizecode);

            return StoreUnaryOpFormat(s, m, res);
        }

        private bool ProcessCMPZ()
        {
            if (!FetchUnaryOpFormat(out UInt64 s, out UInt64 m, out UInt64 a)) return false;

            UpdateFlagsInt(a, (s >> 2) & 3);
            Flags.C = Flags.O = false;

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

                        UpdateFlagsDouble(res);

                        return StoreBinaryOpFormat(s, m, DoubleAsUInt64(res));
                    }
                case 2:
                    {
                        float res = AsFloat(a) + AsFloat(b);

                        UpdateFlagsFloat(res);

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

                        UpdateFlagsDouble(res);

                        return !apply || StoreBinaryOpFormat(s, m, DoubleAsUInt64(res));
                    }
                case 2:
                    {
                        float res = AsFloat(a) - AsFloat(b);

                        UpdateFlagsFloat(res);

                        return !apply || StoreBinaryOpFormat(s, m, FloatAsUInt64(res));
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

                        UpdateFlagsDouble(res);

                        return StoreBinaryOpFormat(s, m, DoubleAsUInt64(res));
                    }
                case 2:
                    {
                        float res = AsFloat(b) - AsFloat(a);

                        UpdateFlagsFloat(res);

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

                        UpdateFlagsDouble(res);

                        return StoreBinaryOpFormat(s, m, DoubleAsUInt64(res));
                    }
                case 2:
                    {
                        float res = AsFloat(a) * AsFloat(b);

                        UpdateFlagsFloat(res);

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

                        UpdateFlagsDouble(res);

                        return StoreBinaryOpFormat(s, m, DoubleAsUInt64(res));
                    }
                case 2:
                    {
                        float res = AsFloat(a) / AsFloat(b);

                        UpdateFlagsFloat(res);

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

                        UpdateFlagsDouble(res);

                        return StoreBinaryOpFormat(s, m, DoubleAsUInt64(res));
                    }
                case 2:
                    {
                        float res = AsFloat(b) / AsFloat(a);

                        UpdateFlagsFloat(res);

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

                        UpdateFlagsDouble(res);

                        return StoreBinaryOpFormat(s, m, DoubleAsUInt64(res));
                    }
                case 2:
                    {
                        float res = (float)Math.Pow(AsFloat(a), AsFloat(b));

                        UpdateFlagsFloat(res);

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

                        UpdateFlagsDouble(res);

                        return StoreBinaryOpFormat(s, m, DoubleAsUInt64(res));
                    }
                case 2:
                    {
                        float res = (float)Math.Pow(AsFloat(b), AsFloat(a));

                        UpdateFlagsFloat(res);

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

                        UpdateFlagsDouble(res);

                        return StoreBinaryOpFormat(s, m, DoubleAsUInt64(res));
                    }
                case 2:
                    {
                        float res = (float)Math.Log(AsFloat(a), AsFloat(b));

                        UpdateFlagsFloat(res);

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

                        UpdateFlagsDouble(res);

                        return StoreBinaryOpFormat(s, m, DoubleAsUInt64(res));
                    }
                case 2:
                    {
                        float res = (float)Math.Log(AsFloat(b), AsFloat(a));

                        UpdateFlagsFloat(res);

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

                        UpdateFlagsDouble(res);

                        return StoreUnaryOpFormat(s, m, DoubleAsUInt64(res));
                    }
                case 2:
                    {
                        float res = (float)Math.Sqrt(AsFloat(a));

                        UpdateFlagsFloat(res);

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

                        UpdateFlagsDouble(res);

                        return StoreUnaryOpFormat(s, m, DoubleAsUInt64(res));
                    }
                case 2:
                    {
                        float res = -AsFloat(a);

                        UpdateFlagsFloat(res);

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

                        UpdateFlagsDouble(res);

                        return StoreUnaryOpFormat(s, m, DoubleAsUInt64(res));
                    }
                case 2:
                    {
                        float res = Math.Abs(AsFloat(a));

                        UpdateFlagsFloat(res);

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

                        UpdateFlagsDouble(res);

                        return StoreUnaryOpFormat(s, m, DoubleAsUInt64(res));
                    }
                case 2:
                    {
                        float res = (float)Math.Floor(AsFloat(a));

                        UpdateFlagsFloat(res);

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

                        UpdateFlagsDouble(res);

                        return StoreUnaryOpFormat(s, m, DoubleAsUInt64(res));
                    }
                case 2:
                    {
                        float res = (float)Math.Ceiling(AsFloat(a));

                        UpdateFlagsFloat(res);

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

                        UpdateFlagsDouble(res);

                        return StoreUnaryOpFormat(s, m, DoubleAsUInt64(res));
                    }
                case 2:
                    {
                        float res = (float)Math.Round(AsFloat(a));

                        UpdateFlagsFloat(res);

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

                        UpdateFlagsDouble(res);

                        return StoreUnaryOpFormat(s, m, DoubleAsUInt64(res));
                    }
                case 2:
                    {
                        float res = (float)Math.Truncate(AsFloat(a));

                        UpdateFlagsFloat(res);

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

                        UpdateFlagsDouble(res);

                        return StoreUnaryOpFormat(s, m, DoubleAsUInt64(res));
                    }
                case 2:
                    {
                        float res = (float)Math.Sin(AsFloat(a));

                        UpdateFlagsFloat(res);

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

                        UpdateFlagsDouble(res);

                        return StoreUnaryOpFormat(s, m, DoubleAsUInt64(res));
                    }
                case 2:
                    {
                        float res = (float)Math.Cos(AsFloat(a));

                        UpdateFlagsFloat(res);

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

                        UpdateFlagsDouble(res);

                        return StoreUnaryOpFormat(s, m, DoubleAsUInt64(res));
                    }
                case 2:
                    {
                        float res = (float)Math.Tan(AsFloat(a));

                        UpdateFlagsFloat(res);

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

                        UpdateFlagsDouble(res);

                        return StoreUnaryOpFormat(s, m, DoubleAsUInt64(res));
                    }
                case 2:
                    {
                        float res = (float)Math.Sinh(AsFloat(a));

                        UpdateFlagsFloat(res);

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

                        UpdateFlagsDouble(res);

                        return StoreUnaryOpFormat(s, m, DoubleAsUInt64(res));
                    }
                case 2:
                    {
                        float res = (float)Math.Cosh(AsFloat(a));

                        UpdateFlagsFloat(res);

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

                        UpdateFlagsDouble(res);

                        return StoreUnaryOpFormat(s, m, DoubleAsUInt64(res));
                    }
                case 2:
                    {
                        float res = (float)Math.Tanh(AsFloat(a));

                        UpdateFlagsFloat(res);

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

                        UpdateFlagsDouble(res);

                        return StoreUnaryOpFormat(s, m, DoubleAsUInt64(res));
                    }
                case 2:
                    {
                        float res = (float)Math.Asin(AsFloat(a));

                        UpdateFlagsFloat(res);

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

                        UpdateFlagsDouble(res);

                        return StoreUnaryOpFormat(s, m, DoubleAsUInt64(res));
                    }
                case 2:
                    {
                        float res = (float)Math.Acos(AsFloat(a));

                        UpdateFlagsFloat(res);

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

                        UpdateFlagsDouble(res);

                        return StoreUnaryOpFormat(s, m, DoubleAsUInt64(res));
                    }
                case 2:
                    {
                        float res = (float)Math.Atan(AsFloat(a));

                        UpdateFlagsFloat(res);

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

                        UpdateFlagsDouble(res);

                        return StoreBinaryOpFormat(s, m, DoubleAsUInt64(res));
                    }
                case 2:
                    {
                        float res = (float)Math.Atan2(AsFloat(a), AsFloat(b));

                        UpdateFlagsFloat(res);

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
                case 3: return StoreUnaryOpFormat(s, m, ((Int64)AsDouble(a)).MakeUnsigned());
                case 2: return StoreUnaryOpFormat(s, m, ((Int64)AsFloat(a)).MakeUnsigned());

                default: Terminate(ErrorCode.UndefinedBehavior); return false;
            }
        }
        private bool ProcessITOF()
        {
            if (!FetchUnaryOpFormat(out UInt64 s, out UInt64 m, out UInt64 a)) return false;

            switch ((s >> 2) & 3)
            {
                case 3: return StoreUnaryOpFormat(s, m, DoubleAsUInt64(a.MakeSigned()));
                case 2: return StoreUnaryOpFormat(s, m, FloatAsUInt64(SignExtend(a, 2).MakeSigned()));

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

            return StoreBinaryOpFormat(s, m, res);
        }
        private bool ProcessBLSI()
        {
            if (!FetchUnaryOpFormat(out UInt64 s, out UInt64 m, out UInt64 a)) return false;

            UInt64 res = a & (~a + 1);

            Flags.Z = res == 0;

            return StoreUnaryOpFormat(s, m, res);
        }
        private bool ProcessBLSMSK()
        {
            if (!FetchUnaryOpFormat(out UInt64 s, out UInt64 m, out UInt64 a)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UInt64 res = Truncate(a ^ (a - 1), sizecode);

            return StoreUnaryOpFormat(s, m, res);
        }
        private bool ProcessBLSR()
        {
            if (!FetchUnaryOpFormat(out UInt64 s, out UInt64 m, out UInt64 a)) return false;

            UInt64 res = a & (a - 1);

            Flags.Z = res == 0;

            return StoreUnaryOpFormat(s, m, res);
        }
        private bool ProcessANDN()
        {
            if (!FetchBinaryOpFormat(out UInt64 s, out UInt64 m, out UInt64 a, out UInt64 b)) return false;
            UInt64 sizecode = (a >> 2) & 3;

            UInt64 res = a & ~b;

            UpdateFlagsInt(res, sizecode);

            return StoreBinaryOpFormat(s, m, res);
        }
    }
}
