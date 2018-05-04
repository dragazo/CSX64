using System;
using System.Numerics;

// -- Operators -- //

namespace CSX64
{
    public partial class Computer
    {
        // -- op utilities -- //

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

        // -- special ops -- //

        private bool ProcessMOV(bool apply = true)
        {
            UInt64 s = 0, m = 0, a = 0, b = 0;
            if (!FetchBinaryOpFormat(ref s, ref m, ref a, ref b)) return false;

            return apply ? StoreBinaryOpFormat(s, m, b) : true;
        }

        // -- integral ops -- //

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

            if (b == 0) { Terminate(ErrorCode.ArithmeticError); return false; }

            UInt64 res = Truncate(a / b, sizecode);

            UpdateFlagsInt(res, sizecode);

            return StoreBinaryOpFormat(s, m, res);
        }
        private bool ProcessBUMOD()
        {
            UInt64 s = 0, m = 0, a = 0, b = 0;
            if (!FetchBinaryOpFormat(ref s, ref m, ref a, ref b)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            if (b == 0) { Terminate(ErrorCode.ArithmeticError); return false; }

            UInt64 res = Truncate(a % b, sizecode);

            UpdateFlagsInt(res, sizecode);

            return StoreBinaryOpFormat(s, m, res);
        }

        private bool ProcessBSDIV()
        {
            UInt64 s = 0, m = 0, a = 0, b = 0;
            if (!FetchBinaryOpFormat(ref s, ref m, ref a, ref b)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            if (b == 0) { Terminate(ErrorCode.ArithmeticError); return false; }

            UInt64 res = Truncate((SignExtend(a, sizecode).MakeSigned() / SignExtend(b, sizecode).MakeSigned()).MakeUnsigned(), sizecode);

            UpdateFlagsInt(res, sizecode);

            return StoreBinaryOpFormat(s, m, res);
        }
        private bool ProcessBSMOD()
        {
            UInt64 s = 0, m = 0, a = 0, b = 0;
            if (!FetchBinaryOpFormat(ref s, ref m, ref a, ref b)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            if (b == 0) { Terminate(ErrorCode.ArithmeticError); return false; }

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

        // -- floatint point ops -- //

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

                default: Terminate(ErrorCode.UndefinedBehavior); return false;
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

                default: Terminate(ErrorCode.UndefinedBehavior); return false;
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

                default: Terminate(ErrorCode.UndefinedBehavior); return false;
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

                default: Terminate(ErrorCode.UndefinedBehavior); return false;
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

                default: Terminate(ErrorCode.UndefinedBehavior); return false;
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

                default: Terminate(ErrorCode.UndefinedBehavior); return false;
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

                default: Terminate(ErrorCode.UndefinedBehavior); return false;
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

                default: Terminate(ErrorCode.UndefinedBehavior); return false;
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

                default: Terminate(ErrorCode.UndefinedBehavior); return false;
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

                default: Terminate(ErrorCode.UndefinedBehavior); return false;
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

                default: Terminate(ErrorCode.UndefinedBehavior); return false;
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

                default: Terminate(ErrorCode.UndefinedBehavior); return false;
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

                default: Terminate(ErrorCode.UndefinedBehavior); return false;
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

                default: Terminate(ErrorCode.UndefinedBehavior); return false;
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

                default: Terminate(ErrorCode.UndefinedBehavior); return false;
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

                default: Terminate(ErrorCode.UndefinedBehavior); return false;
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

                default: Terminate(ErrorCode.UndefinedBehavior); return false;
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

                default: Terminate(ErrorCode.UndefinedBehavior); return false;
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

                default: Terminate(ErrorCode.UndefinedBehavior); return false;
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

                default: Terminate(ErrorCode.UndefinedBehavior); return false;
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

                default: Terminate(ErrorCode.UndefinedBehavior); return false;
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

                default: Terminate(ErrorCode.UndefinedBehavior); return false;
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

                default: Terminate(ErrorCode.UndefinedBehavior); return false;
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

                default: Terminate(ErrorCode.UndefinedBehavior); return false;
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

                default: Terminate(ErrorCode.UndefinedBehavior); return false;
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

                default: Terminate(ErrorCode.UndefinedBehavior); return false;
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

                default: Terminate(ErrorCode.UndefinedBehavior); return false;
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

                default: Terminate(ErrorCode.UndefinedBehavior); return false;
            }
        }

        // -- extended register ops -- //

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
        private bool ProcessSDIV()
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

        // -- misc operations -- //

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
    }
}