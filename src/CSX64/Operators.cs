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
        private bool TryGet_cc(ccOPCode code, out bool res)
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

        /*
        [4: dest][2: size][1:dh][1: mem]   [size: imm]
            mem = 0: [1: sh][3:][4: src]
                dest <- f(reg, imm)
            mem = 1: [address]
                dest <- f(M[address], imm)
            (dh and sh mark AH, BH, CH, or DH for dest or src)
        */
        private bool FetchTernaryOpFormat(out UInt64 s, out UInt64 a, out UInt64 b)
        {
            if (!GetMemAdv(1, out s)) { a = b = 0; return false; }
            UInt64 sizecode = (s >> 2) & 3;

            // make sure dest will be valid for storing (high flag)
            if ((s & 2) != 0 && ((s & 0xc0) != 0 || sizecode != 0)) { Terminate(ErrorCode.UndefinedBehavior); a = b = 0; return false; }

            // get b (imm)
            if (!GetMemAdv(Size(sizecode), out b)) { a = 0; return false; }

            // get a (reg or mem)
            if ((s & 1) == 0)
            {
                if (!GetMemAdv(1, out a)) return false;
                if ((a & 128) != 0)
                {
                    if ((a & 0x0c) != 0 || sizecode != 0) { Terminate(ErrorCode.UndefinedBehavior); return false; }
                    a = Registers[a & 15].x8h;
                }
                else a = Registers[a & 15][sizecode];
                return true;
            }
            else return GetAddressAdv(out a) && GetMemRaw(a, Size(sizecode), out a);
        }
        private bool StoreTernaryOPFormat(UInt64 s, UInt64 res)
        {
            if ((s & 2) != 0) Registers[s >> 4].x8h = (byte)res;
            else Registers[s >> 4][(s >> 2) & 3] = res;
            return true;
        }

        /*
        [4: dest][2: size][1:dh][1: sh]   [4: mode][4: src]
            Mode = 0:                           dest <- f(dest, src)
            Mode = 1: [size: imm]               dest <- f(dest, imm)
            Mode = 2: [address]                 dest <- f(dest, M[address])
            Mode = 3: [address]                 M[address] <- f(M[address], src)
            Mode = 4: [address]   [size: imm]   M[address] <- f(M[address], imm)
            Else UND
            (dh and sh mark AH, BH, CH, or DH for dest or src)
        */
        private bool FetchBinaryOpFormatNew(out UInt64 s1, out UInt64 s2, out UInt64 m, out UInt64 a, out UInt64 b,
            bool get_a = true, int _a_sizecode = -1, int _b_sizecode = -1, bool allow_b_mem = true)
        {
            m = a = b = 0; // zero the things (compiler is annoying me)

            // read settings
            if (!GetMemAdv(1, out s1) || !GetMemAdv(1, out s2)) { s2 = 0; return false; }

            // if they requested an explicit size for a, change it in the settings byte
            if (_a_sizecode != -1) s1 = (s1 & 0xf3) | ((UInt64)_a_sizecode << 2);

            // get size codes
            UInt64 a_sizecode = (s1 >> 2) & 3;
            UInt64 b_sizecode = _b_sizecode == -1 ? a_sizecode : (UInt64)_b_sizecode;

            // switch through mode
            switch (s2 >> 4)
            {
                case 0:
                    // if dh is flagged
                    if ((s1 & 2) != 0)
                    {
                        // make sure we're in registers 0-3 and 8-bit mode
                        if ((s1 & 0xc0) != 0 || a_sizecode != 0) { Terminate(ErrorCode.UndefinedBehavior); return false; }
                        if (get_a) a = Registers[s1 >> 4].x8h;
                    }
                    else if (get_a) a = Registers[s1 >> 4][a_sizecode];
                    // if sh is flagged
                    if ((s1 & 1) != 0)
                    {
                        // make sure we're in registers 0-3 and 8-bit mode
                        if ((s2 & 0x0c) != 0 || b_sizecode != 0) { Terminate(ErrorCode.UndefinedBehavior); return false; }
                        b = Registers[s2 & 15].x8h;
                    }
                    else b = Registers[s2 & 15][b_sizecode];
                    return true;

                case 1:
                    // if dh is flagged
                    if ((s1 & 2) != 0)
                    {
                        // make sure we're in registers 0-3 and 8-bit mode
                        if ((s1 & 0xc0) != 0 || a_sizecode != 0) { Terminate(ErrorCode.UndefinedBehavior); return false; }
                        if (get_a) a = Registers[s1 >> 4].x8h;
                    }
                    else if (get_a) a = Registers[s1 >> 4][a_sizecode];
                    // get imm
                    return GetMemAdv(Size(b_sizecode), out b);

                case 2:
                    // handle allow_b_mem case
                    if (!allow_b_mem) { Terminate(ErrorCode.UndefinedBehavior); return false; }

                    // if dh is flagged
                    if ((s1 & 2) != 0)
                    {
                        // make sure we're in registers 0-3 and 8-bit mode
                        if ((s1 & 0xc0) != 0 || a_sizecode != 0) { Terminate(ErrorCode.UndefinedBehavior); return false; }
                        if (get_a) a = Registers[s1 >> 4].x8h;
                    }
                    else if (get_a) a = Registers[s1 >> 4][a_sizecode];
                    // get mem
                    return GetAddressAdv(out m) && GetMemRaw(m, Size(b_sizecode), out b);

                case 3:
                    // get mem
                    if (!GetAddressAdv(out m) || get_a && !GetMemRaw(m, Size(a_sizecode), out a)) return false;
                    // if sh is flagged
                    if ((s1 & 1) != 0)
                    {
                        // make sure we're in registers 0-3 and 8-bit mode
                        if ((s2 & 0x0c) != 0 || b_sizecode != 0) { Terminate(ErrorCode.UndefinedBehavior); return false; }
                        b = Registers[s2 & 15].x8h;
                    }
                    else b = Registers[s2 & 15][b_sizecode];
                    return true;

                case 4:
                    // get mem
                    if (!GetAddressAdv(out m) || get_a && !GetMemRaw(m, Size(a_sizecode), out a)) return false;
                    // get imm
                    return GetMemAdv(Size(b_sizecode), out b);

                default: Terminate(ErrorCode.UndefinedBehavior); return false;
            }
        }
        private bool StoreBinaryOpFormatNew(UInt64 s1, UInt64 s2, UInt64 m, UInt64 res)
        {
            UInt64 sizecode = (s1 >> 2) & 3;

            // switch through mode
            switch (s2 >> 4)
            {
                case 0:
                case 1:
                case 2:
                    if ((s1 & 2) != 0) Registers[s1 >> 4].x8h = (byte)res;
                    else Registers[s1 >> 4][sizecode] = res;
                    return true;

                case 3:
                case 4:
                    return SetMemRaw(m, Size(sizecode), res);

                default: Terminate(ErrorCode.UndefinedBehavior); return false;
            }
        }

        /*
        [4: dest][2: size][1: h][1: mem]
            mem = 0:             dest <- f(dest)
            mem = 1: [address]   M[address] <- f(M[address])
        */
        private bool FetchUnaryOpFormat(out UInt64 s, out UInt64 m, out UInt64 a, bool get_a = true, int _a_sizecode = -1)
        {
            m = a = 0; // zero a and m (for get_a logic and so compiler won't complain)

            // read settings
            if (!GetMemAdv(1, out s)) return false;

            // if they requested an explicit size for a, change it in the settings byte
            if (_a_sizecode != -1) s = (s & 0xf3) | ((UInt64)_a_sizecode << 2);

            UInt64 a_sizecode = (s >> 2) & 3;

            // switch through mode
            switch (s & 1)
            {
                case 0:
                    // if h is flagged
                    if ((s & 2) != 0)
                    {
                        // make sure we're in registers 0-3 and 8-bit mode
                        if ((s & 0xc0) != 0 || a_sizecode != 0) { Terminate(ErrorCode.UndefinedBehavior); return false; }
                        if (get_a) a = Registers[s >> 4].x8h;
                    }
                    else if (get_a) a = Registers[s >> 4][a_sizecode];
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
                    if ((s & 2) != 0) Registers[s >> 4].x8h = (byte)res;
                    else Registers[s >> 4][sizecode] = res;
                    return true;

                case 1:
                    return SetMemRaw(m, Size(sizecode), res);

                default: return true; // this can't happen but compiler is stupid
            }
        }

        /*
        [4: reg][2: size][2: mode]
            mode = 0:               reg
            mode = 1:               h reg (AH, BH, CH, or DH)
            mode = 2: [size: imm]   imm
            mode = 3: [address],    M[address]
        */
        private bool FetchIMMRMFormat(out UInt64 s, out UInt64 a, int _a_sizecode = -1)
        {
            a = 0; // so compiler won't complain

            if (!GetMemAdv(1, out s)) return false;

            UInt64 a_sizecode = _a_sizecode == -1 ? (s >> 2) & 3 : (UInt64)_a_sizecode;

            // get the value into b
            switch (s & 3)
            {
                case 0:
                    a = Registers[s >> 4][a_sizecode];
                    return true;

                case 1:
                    if ((s & 0xc0) != 0) { Terminate(ErrorCode.UndefinedBehavior); return false; }
                    a = Registers[s >> 4].x8h;
                    return true;

                case 2: return GetMemAdv(Size(a_sizecode), out a);

                case 3: return GetAddressAdv(out a) && GetMemRaw(a, Size(a_sizecode), out a);
            }

            return true;
        }

        // updates the flags for integral ops (identical for most integral ops)
        private void UpdateFlagsZSP(UInt64 value, UInt64 sizecode)
        {
            ZF = value == 0;
            SF = Negative(value, sizecode);

            // compute parity flag (only of low 8 bits)
            bool parity = true;
            for (UInt64 i = 128; i != 0; i >>= 1) if ((value & i) != 0) parity = !parity;
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
            if (!FetchBinaryOpFormatNew(out UInt64 s1, out UInt64 s2, out UInt64 m, out UInt64 a, out UInt64 b, false)) return false;

            return !apply || StoreBinaryOpFormatNew(s1, s2, m, b);
        }

        /*
        [4: r1][2: size][1: r1h][1: mem]
	        mem = 0: [1: r2h][3:][4: r2]
		        r1 <- r2
		        r2 <- r1
	        mem = 1: [address]
		        r1         <- M[address]
		        M[address] <- r1
            (r1h and r2h mark AH, BH, CH, or DH for r1 or r2)
        */
        private bool ProcessXCHG()
        {
            UInt64 a, b, temp_1, temp_2;

            if (!GetMemAdv(1, out a)) return false;
            UInt64 sizecode = (a >> 2) & 3;

            // if a is high
            if ((a & 2) != 0)
            {
                if ((a & 0xc0) != 0 || sizecode != 0) { Terminate(ErrorCode.UndefinedBehavior); return false; }
                temp_1 = Registers[a >> 4].x8h;
            }
            else temp_1 = Registers[a >> 4][sizecode];

            // if b is reg
            if ((a & 1) == 0)
            {
                if (!GetMemAdv(1, out b)) return false;

                // if b is high
                if ((b & 128) != 0)
                {
                    if ((b & 0x0c) != 0 || sizecode != 0) { Terminate(ErrorCode.UndefinedBehavior); return false; }
                    temp_2 = Registers[b & 15].x8h;
                    Registers[b & 15].x8h = (byte)temp_1;
                }
                else
                {
                    temp_2 = Registers[b & 15][sizecode];
                    Registers[b & 15][sizecode] = temp_1;
                }
            }
            // otherwise b is mem
            else
            {
                // get mem value into temp_2 (address in b)
                if (!GetAddressAdv(out b) || !GetMemRaw(b, Size(sizecode), out temp_2)) return false;
                // store b result
                if (!SetMemRaw(b, Size(sizecode), temp_1)) return false;
            }

            // store a's result (b's was handled internally above)
            if ((a & 2) != 0) Registers[a >> 4].x8h = (byte)temp_2;
            else Registers[a >> 4][sizecode] = temp_2;

            return true;
        }

        private bool ProcessJMP(bool apply, ref UInt64 aft)
        {
            if (!FetchIMMRMFormat(out UInt64 s, out UInt64 val)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            // 8-bit addressing not allowed
            if (sizecode == 0) { Terminate(ErrorCode.UndefinedBehavior); return false; }

            aft = RIP; // record point immediately after reading (for CALL return address)

            if (apply) RIP = val; // jump

            return true;
        }
        private bool ProcessLOOP(bool continue_flag)
        {
            if (!FetchIMMRMFormat(out UInt64 s, out UInt64 val)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UInt64 count;
            switch (sizecode)
            {
                case 3: count = --RCX; break;
                case 2: count = --ECX; break;
                case 1: count = --CX; break;
                case 0: Terminate(ErrorCode.UndefinedBehavior); return false; // 8-bit not allowed

                default: return true; // this can't happen but compiler is stupid
            }

            if (count != 0 && continue_flag) RIP = val; // jump if nonzero count and continue flag set

            return true;
        }

        private bool ProcessPUSH()
        {
            if (!FetchIMMRMFormat(out UInt64 s, out UInt64 a)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            // 8-bit push not allowed
            if (sizecode == 0) { Terminate(ErrorCode.UndefinedBehavior); return false; }

            return PushRaw(Size(sizecode), a);
        }
        /*
        [4: reg][2: size][1:][1: mem]
            mem = 0:             reg
            mem = 1: [address]   M[address]
        */
        private bool ProcessPOP()
        {
            if (!GetMemAdv(1, out UInt64 s)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            // 8-bit pop not allowed
            if (sizecode == 0) { Terminate(ErrorCode.UndefinedBehavior); return false; }

            // get the value
            if (!PopRaw(Size(sizecode), out UInt64 val)) return false;

            // if register
            if ((s & 1) == 0)
            {
                Registers[s >> 4][sizecode] = val;
                return true;
            }
            // otherwise is memory
            else return GetAddressAdv(out s) && SetMemRaw(s, Size(sizecode), val);
        }

        private bool ProcessLEA()
        {
            if (!GetMemAdv(1, out UInt64 s) || !GetAddressAdv(out UInt64 address)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            // LEA doesn't allow 8-bit addressing
            if (sizecode == 0) { Terminate(ErrorCode.UndefinedBehavior); return false; }

            Registers[s >> 4][sizecode] = address;
            return true;
        }

        private bool ProcessADD()
        {
            if (!FetchBinaryOpFormatNew(out UInt64 s1, out UInt64 s2, out UInt64 m, out UInt64 a, out UInt64 b)) return false;
            UInt64 sizecode = (s1 >> 2) & 3;

            UInt64 res = Truncate(a + b, sizecode);

            UpdateFlagsZSP(res, sizecode);
            CF = res < a && res < b; // if overflow is caused, some of one value must go toward it, so the truncated result must necessarily be less than both args
            AF = (res & 0xf) < (a & 0xf) && (res & 0xf) < (b & 0xf); // AF is just like CF but only the low nibble
            OF = Positive(a, sizecode) == Positive(b, sizecode) && Positive(a, sizecode) != Positive(res, sizecode);

            return StoreBinaryOpFormatNew(s1, s2, m, res);
        }
        private bool ProcessSUB(bool apply = true)
        {
            if (!FetchBinaryOpFormatNew(out UInt64 s1, out UInt64 s2, out UInt64 m, out UInt64 a, out UInt64 b)) return false;
            UInt64 sizecode = (s1 >> 2) & 3;

            UInt64 res = Truncate(a - b, sizecode);

            UpdateFlagsZSP(res, sizecode);
            CF = a < b; // if a < b, a borrow was taken from the highest bit
            AF = (a & 0xf) < (b & 0xf); // AF is just like CF but only the low nibble
            OF = Positive(a, sizecode) != Positive(b, sizecode) && Positive(a, sizecode) != Positive(res, sizecode);

            return !apply || StoreBinaryOpFormatNew(s1, s2, m, res);
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
            if (!FetchBinaryOpFormatNew(out UInt64 s1, out UInt64 s2, out UInt64 m, out UInt64 _a, out UInt64 _b)) return false;
            UInt64 sizecode = (s1 >> 2) & 3;

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

            return StoreBinaryOpFormatNew(s1, s2, m, (UInt64)res);
        }
        private bool ProcessTernary_IMUL()
        {
            if (!FetchTernaryOpFormat(out UInt64 s, out UInt64 _a, out UInt64 _b)) return false;
            UInt64 sizecode = (s >> 2) & 3;

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

            return StoreTernaryOPFormat(s, (UInt64)res);
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
            if (!FetchBinaryOpFormatNew(out UInt64 s1, out UInt64 s2, out UInt64 m, out UInt64 a, out UInt64 b, true, -1, 0)) return false;
            UInt64 sizecode = (s1 >> 2) & 3;

            UInt16 sh = (UInt16)(b & (sizecode == 3 ? 0x3ful : 0x1ful)); // mask shift val to 6 bits on 64-bit op, otherwise to 5 bits

            // shift of zero is no-op
            if (sh != 0)
            {
                UInt64 res = Truncate(a << sh, sizecode);

                UpdateFlagsZSP(res, sizecode);
                CF = sh < SizeBits(sizecode) ? ((a >> ((UInt16)SizeBits(sizecode) - sh)) & 1) == 1 : Rand.NextBool(); // CF holds last bit shifted out (UND for sh >= #bits)
                OF = sh == 1 ? Negative(res, sizecode) != CF : Rand.NextBool(); // OF is 1 if top 2 bits of original value were different (UND for sh != 1)
                AF = Rand.NextBool(); // AF is undefined

                return StoreBinaryOpFormatNew(s1, s2, m, res);
            }
            else return true;
        }
        private bool ProcessSHR()
        {
            if (!FetchBinaryOpFormatNew(out UInt64 s1, out UInt64 s2, out UInt64 m, out UInt64 a, out UInt64 b, true, -1, 0)) return false;
            UInt64 sizecode = (s1 >> 2) & 3;

            UInt16 sh = (UInt16)(b & (sizecode == 3 ? 0x3ful : 0x1ful)); // mask shift val to 6 bits on 64-bit op, otherwise to 5 bits

            // shift of zero is no-op
            if (sh != 0)
            {
                UInt64 res = a >> sh;

                UpdateFlagsZSP(res, sizecode);
                CF = sh < SizeBits(sizecode) ? ((a >> (sh - 1)) & 1) == 1 : Rand.NextBool(); // CF holds last bit shifted out (UND for sh >= #bits)
                OF = sh == 1 ? Negative(a, sizecode) : Rand.NextBool(); // OF is high bit of original value (UND for sh != 1)
                AF = Rand.NextBool(); // AF is undefined

                return StoreBinaryOpFormatNew(s1, s2, m, res);
            }
            else return true;
        }

        private bool ProcessSAL()
        {
            if (!FetchBinaryOpFormatNew(out UInt64 s1, out UInt64 s2, out UInt64 m, out UInt64 a, out UInt64 b, true, -1, 0)) return false;
            UInt64 sizecode = (s1 >> 2) & 3;

            UInt16 sh = (UInt16)(b & (sizecode == 3 ? 0x3ful : 0x1ful)); // mask shift val to 6 bits on 64-bit op, otherwise to 5 bits

            // shift of zero is no-op
            if (sh != 0)
            {
                UInt64 res = Truncate((UInt64)((Int64)SignExtend(a, sizecode) << sh), sizecode);

                UpdateFlagsZSP(res, sizecode);
                CF = sh < SizeBits(sizecode) ? ((a >> ((UInt16)SizeBits(sizecode) - sh)) & 1) == 1 : Rand.NextBool(); // CF holds last bit shifted out (UND for sh >= #bits)
                OF = sh == 1 ? Negative(res, sizecode) != CF : Rand.NextBool(); // OF is 1 if top 2 bits of original value were different (UND for sh != 1)
                AF = Rand.NextBool(); // AF is undefined

                return StoreBinaryOpFormatNew(s1, s2, m, res);
            }
            else return true;
        }
        private bool ProcessSAR()
        {
            if (!FetchBinaryOpFormatNew(out UInt64 s1, out UInt64 s2, out UInt64 m, out UInt64 a, out UInt64 b, true, -1, 0)) return false;
            UInt64 sizecode = (s1 >> 2) & 3;

            UInt16 sh = (UInt16)(b & (sizecode == 3 ? 0x3ful : 0x1ful)); // mask shift val to 6 bits on 64-bit op, otherwise to 5 bits

            // shift of zero is no-op
            if (sh != 0)
            {
                UInt64 res = Truncate((UInt64)((Int64)SignExtend(a, sizecode) >> sh), sizecode);

                UpdateFlagsZSP(res, sizecode);
                CF = sh < SizeBits(sizecode) ? ((a >> (sh - 1)) & 1) == 1 : Rand.NextBool(); // CF holds last bit shifted out (UND for sh >= #bits)
                OF = sh == 1 ? false : Rand.NextBool(); // OF is cleared (UND for sh != 1)
                AF = Rand.NextBool(); // AF is undefined

                return StoreBinaryOpFormatNew(s1, s2, m, res);
            }
            else return false;
        }

        private bool ProcessROL()
        {
            if (!FetchBinaryOpFormatNew(out UInt64 s1, out UInt64 s2, out UInt64 m, out UInt64 a, out UInt64 b, true, -1, 0)) return false;
            UInt64 sizecode = (s1 >> 2) & 3;

            UInt16 sh = (UInt16)(b % SizeBits(sizecode)); // rotate performed modulo-n

            // shift of zero is no-op
            if (sh != 0)
            {
                UInt64 res = Truncate((a << sh) | (a >> ((UInt16)SizeBits(sizecode) - sh)), sizecode);

                CF = ((a >> ((UInt16)SizeBits(sizecode) - sh)) & 1) == 1; // CF holds last bit shifted around
                OF = sh == 1 ? CF ^ Negative(res, sizecode) : Rand.NextBool(); // OF is xor of CF (after rotate) and high bit of result (UND if sh != 1)

                return StoreBinaryOpFormatNew(s1, s2, m, res);
            }
            else return true;
        }
        private bool ProcessROR()
        {
            if (!FetchBinaryOpFormatNew(out UInt64 s1, out UInt64 s2, out UInt64 m, out UInt64 a, out UInt64 b, true, -1, 0)) return false;
            UInt64 sizecode = (s1 >> 2) & 3;

            UInt16 sh = (UInt16)(b % SizeBits(sizecode)); // rotate performed modulo-n

            // shift of zero is no-op
            if (sh != 0)
            {
                UInt64 res = Truncate((a >> sh) | (a << ((UInt16)SizeBits(sizecode) - sh)), sizecode);

                CF = ((a >> (sh - 1)) & 1) == 1; // CF holds last bit shifted around
                OF = sh == 1 ? Negative(res, sizecode) ^ (((res >> ((8 << (ushort)sizecode) - 2)) & 1) != 0) : Rand.NextBool(); // OF is xor of 2 highest bits of result

                return StoreBinaryOpFormatNew(s1, s2, m, res);
            }
            else return true;
        }

        private bool ProcessAND(bool apply = true)
        {
            if (!FetchBinaryOpFormatNew(out UInt64 s1, out UInt64 s2, out UInt64 m, out UInt64 a, out UInt64 b)) return false;
            UInt64 sizecode = (s1 >> 2) & 3;

            UInt64 res = a & b;

            UpdateFlagsZSP(res, sizecode);
            OF = CF = false;
            AF = Rand.NextBool();

            return !apply || StoreBinaryOpFormatNew(s1, s2, m, res);
        }
        private bool ProcessOR()
        {
            if (!FetchBinaryOpFormatNew(out UInt64 s1, out UInt64 s2, out UInt64 m, out UInt64 a, out UInt64 b)) return false;
            UInt64 sizecode = (s1 >> 2) & 3;

            UInt64 res = a | b;

            UpdateFlagsZSP(res, sizecode);
            OF = CF = false;
            AF = Rand.NextBool();

            return StoreBinaryOpFormatNew(s1, s2, m, res);
        }
        private bool ProcessXOR()
        {
            if (!FetchBinaryOpFormatNew(out UInt64 s1, out UInt64 s2, out UInt64 m, out UInt64 a, out UInt64 b)) return false;
            UInt64 sizecode = (s1 >> 2) & 3;

            UInt64 res = a ^ b;

            UpdateFlagsZSP(res, sizecode);
            OF = CF = false;
            AF = Rand.NextBool();

            return StoreBinaryOpFormatNew(s1, s2, m, res);
        }

        private bool ProcessINC()
        {
            if (!FetchUnaryOpFormat(out UInt64 s, out UInt64 m, out UInt64 a)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UInt64 res = Truncate(a + 1, sizecode);

            UpdateFlagsZSP(res, sizecode);
            AF = (res & 0xf) == 0; // low nibble of 0 was a nibble overflow (TM)
            OF = Positive(a, sizecode) && Negative(res, sizecode); // + -> - is overflow

            return StoreUnaryOpFormat(s, m, res);
        }
        private bool ProcessDEC()
        {
            if (!FetchUnaryOpFormat(out UInt64 s, out UInt64 m, out UInt64 a)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UInt64 res = Truncate(a - 1, sizecode);

            UpdateFlagsZSP(res, sizecode);
            AF = (a & 0xf) == 0; // nibble a = 0 results in borrow from the low nibble
            OF = Negative(a, sizecode) && Positive(res, sizecode); // - -> + is overflow

            return StoreUnaryOpFormat(s, m, res);
        }
        private bool ProcessNEG()
        {
            if (!FetchUnaryOpFormat(out UInt64 s, out UInt64 m, out UInt64 a)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UInt64 res = Truncate(0 - a, sizecode);

            UpdateFlagsZSP(res, sizecode);
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

            UpdateFlagsZSP(a, sizecode);
            CF = OF = AF = false;

            return true;
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
            if (!FetchBinaryOpFormatNew(out UInt64 s1, out UInt64 s2, out UInt64 m, out UInt64 a, out UInt64 b, true, -1, 1)) return false;
            UInt64 sizecode = (s1 >> 2) & 3;

            ushort pos = (ushort)((b >> 8) % SizeBits(sizecode));
            ushort len = (ushort)((b & 0xff) % SizeBits(sizecode));

            UInt64 res = (a >> pos) & ((1ul << len) - 1);

            EFLAGS = 2; // clear all the (public) flags (flag 1 must always be set)
            ZF = res == 0; // ZF is set on zero
            AF = Rand.NextBool(); // AF, SF, and PF are undefined
            SF = Rand.NextBool();
            PF = Rand.NextBool();

            return StoreBinaryOpFormatNew(s1, s2, m, res);
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
            if (!FetchBinaryOpFormatNew(out UInt64 s1, out UInt64 s2, out UInt64 m, out UInt64 a, out UInt64 b)) return false;
            UInt64 sizecode = (s1 >> 2) & 3;

            UInt64 res = a & ~b;

            ZF = res == 0;
            SF = Negative(res, sizecode);
            OF = CF = false;
            AF = Rand.NextBool();
            PF = Rand.NextBool();

            return StoreBinaryOpFormatNew(s1, s2, m, res);
        }

        private bool ProcessBT()
        {
            if (!GetMemAdv(1, out UInt64 ext)) return false;

            if (!FetchBinaryOpFormatNew(out UInt64 s1, out UInt64 s2, out UInt64 m, out UInt64 a, out UInt64 b, true, -1, 0, false)) return false;
            UInt64 sizecode = (s1 >> 2) & 3;

            UInt64 mask = 1ul << (UInt16)(b % SizeBits(sizecode)); // performed modulo-n

            CF = (a & mask) != 0;
            OF = Rand.NextBool();
            SF = Rand.NextBool();
            AF = Rand.NextBool();
            PF = Rand.NextBool();

            switch (ext)
            {
                case 0: return true;
                case 1: return StoreBinaryOpFormatNew(s1, s2, m, a | mask);
                case 2: return StoreBinaryOpFormatNew(s1, s2, m, a & ~mask);
                case 3: return StoreBinaryOpFormatNew(s1, s2, m, a ^ mask);

                default: Terminate(ErrorCode.UndefinedBehavior); return false;
            }
        }

        // -- floating point stuff -- //

        /*
        [1: pop][3: i][1:][3: mode]
            mode = 0: st(0) <- f(st(0), st(i))
            mode = 1: st(i) <- f(st(i), st(0))
            mode = 2: st(i) <- f(st(i), fp32M)
            mode = 3: st(i) <- f(st(i), fp64M)
            mode = 4: st(i) <- f(st(i), int32M)
            mode = 5: st(i) <- f(st(i), int64M)
            else UND
        */
        private bool FetchFPUBinaryFormat(out UInt64 s, out double a, out double b)
        {
            if (!GetMemAdv(1, out s)) { a = b = 0; return false; }

            // switch through mode
            switch (s & 7)
            {
                case 0:
                    if (!FPURegisters[TOP].InUse || !FPURegisters[(TOP + (s >> 4)) & 7].InUse) { Terminate(ErrorCode.FPUAccessViolation); a = b = 0; return false; }
                    a = FPURegisters[TOP].Float; b = FPURegisters[(TOP + (s >> 4)) & 7].Float; return true;
                case 1:
                    if (!FPURegisters[TOP].InUse || !FPURegisters[(TOP + (s >> 4)) & 7].InUse) { Terminate(ErrorCode.FPUAccessViolation); a = b = 0; return false; }
                    b = FPURegisters[TOP].Float; a = FPURegisters[(TOP + (s >> 4)) & 7].Float; return true;
                
                default:
                    if (!FPURegisters[TOP].InUse) { Terminate(ErrorCode.FPUAccessViolation); a = b = 0; return false; }
                    a = FPURegisters[TOP].Float; b = 0;
                    if (!GetAddressAdv(out UInt64 m)) return false;
                    switch (s & 7)
                    {
                        case 2: if (!GetMemRaw(m, 4, out m)) return false; b = AsFloat(m); return true;
                        case 3: if (!GetMemRaw(m, 8, out m)) return false; b = AsDouble(m); return true;
                        case 4: if (!GetMemRaw(m, 4, out m)) return false; b = (Int64)SignExtend(m, 2); return true;
                        case 5: if (!GetMemRaw(m, 8, out m)) return false; b = (Int64)m; return true;

                        default: Terminate(ErrorCode.UndefinedBehavior); return false;
                    }
            }
        }
        private bool StoreFPUBinaryFormat(UInt64 s, double res)
        {
            // record result
            if ((s & 7) == 1) FPURegisters[(TOP + (s >> 4)) & 7].Float = res;
            else FPURegisters[TOP].Float = res;

            // if popping, pop
            if ((s & 128) != 0) return PopFPU(out res);

            return true;
        }

        private bool PushFPU(double val)
        {
            // decrement top (wraps automatically as a 3-bit unsigned value)
            --TOP;

            // if this fpu reg is in use, it's an error
            if (FPURegisters[TOP].InUse) { Terminate(ErrorCode.FPUStackOverflow); return false; }

            // store the value
            FPURegisters[TOP].Float = val;
            FPURegisters[TOP].InUse = true;

            return true;
        }
        private bool PopFPU(out double val)
        {
            // if this register is not in use, it's an error
            if (!FPURegisters[TOP].InUse) { Terminate(ErrorCode.FPUStackUnderflow); val = 0; return false; }

            // extract the value
            val = FPURegisters[TOP].Float;
            FPURegisters[TOP].InUse = false;

            // increment top (wraps automatically as a 3-bit unsigned value)
            ++TOP;
            
            return true;
        }

        private bool ProcessFLD_const()
        {
            if (!GetMemAdv(1, out UInt64 ext)) return false;

            switch (ext)
            {
                case 0: return PushFPU(1);
                case 1: return PushFPU(Math.Log(10, 2));
                case 2: return PushFPU(Math.Log(Math.E, 2));
                case 3: return PushFPU(Math.PI);
                case 4: return PushFPU(Math.Log10(2));
                case 5: return PushFPU(Math.Log(2));
                case 6: return PushFPU(0);

                default: Terminate(ErrorCode.UndefinedBehavior); return false;
            }
        }

        private bool ProcessFADD()
        {
            if (!FetchFPUBinaryFormat(out UInt64 s, out double a, out double b)) return false;

            double res = a + b;

            C0 = Rand.NextBool();
            C1 = Rand.NextBool();
            C2 = Rand.NextBool();
            C3 = Rand.NextBool();

            return StoreFPUBinaryFormat(s, res);
        }
        private bool ProcessFSUB()
        {
            if (!FetchFPUBinaryFormat(out UInt64 s, out double a, out double b)) return false;

            double res = a - b;

            C0 = Rand.NextBool();
            C1 = Rand.NextBool();
            C2 = Rand.NextBool();
            C3 = Rand.NextBool();

            return StoreFPUBinaryFormat(s, res);
        }
        private bool ProcessFSUBR()
        {
            if (!FetchFPUBinaryFormat(out UInt64 s, out double a, out double b)) return false;

            double res = b - a;

            C0 = Rand.NextBool();
            C1 = Rand.NextBool();
            C2 = Rand.NextBool();
            C3 = Rand.NextBool();

            return StoreFPUBinaryFormat(s, res);
        }

        private bool ProcessFMUL()
        {
            if (!FetchFPUBinaryFormat(out UInt64 s, out double a, out double b)) return false;

            double res = a * b;

            C0 = Rand.NextBool();
            C1 = Rand.NextBool();
            C2 = Rand.NextBool();
            C3 = Rand.NextBool();

            return StoreFPUBinaryFormat(s, res);
        }
        private bool ProcessFDIV()
        {
            if (!FetchFPUBinaryFormat(out UInt64 s, out double a, out double b)) return false;

            double res = a / b;

            C0 = Rand.NextBool();
            C1 = Rand.NextBool();
            C2 = Rand.NextBool();
            C3 = Rand.NextBool();

            return StoreFPUBinaryFormat(s, res);
        }
        private bool ProcessFDIVR()
        {
            if (!FetchFPUBinaryFormat(out UInt64 s, out double a, out double b)) return false;

            double res = b / a;

            C0 = Rand.NextBool();
            C1 = Rand.NextBool();
            C2 = Rand.NextBool();
            C3 = Rand.NextBool();

            return StoreFPUBinaryFormat(s, res);
        }
    }
}
