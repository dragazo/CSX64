using System;
using System.Numerics;
using static CSX64.Utility;

// -- Operators -- //

namespace CSX64
{
    public partial class Computer
    {
        // -- op utilities -- //

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
                    a = CPURegisters[a & 15].x8h;
                }
                else a = CPURegisters[a & 15][sizecode];
                return true;
            }
            else return GetAddressAdv(out a) && GetMemRaw(a, Size(sizecode), out a);
        }
        private bool StoreTernaryOPFormat(UInt64 s, UInt64 res)
        {
            if ((s & 2) != 0) CPURegisters[s >> 4].x8h = (byte)res;
            else CPURegisters[s >> 4][(s >> 2) & 3] = res;
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
                        if (get_a) a = CPURegisters[s1 >> 4].x8h;
                    }
                    else if (get_a) a = CPURegisters[s1 >> 4][a_sizecode];
                    // if sh is flagged
                    if ((s1 & 1) != 0)
                    {
                        // make sure we're in registers 0-3 and 8-bit mode
                        if ((s2 & 0x0c) != 0 || b_sizecode != 0) { Terminate(ErrorCode.UndefinedBehavior); return false; }
                        b = CPURegisters[s2 & 15].x8h;
                    }
                    else b = CPURegisters[s2 & 15][b_sizecode];
                    return true;

                case 1:
                    // if dh is flagged
                    if ((s1 & 2) != 0)
                    {
                        // make sure we're in registers 0-3 and 8-bit mode
                        if ((s1 & 0xc0) != 0 || a_sizecode != 0) { Terminate(ErrorCode.UndefinedBehavior); return false; }
                        if (get_a) a = CPURegisters[s1 >> 4].x8h;
                    }
                    else if (get_a) a = CPURegisters[s1 >> 4][a_sizecode];
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
                        if (get_a) a = CPURegisters[s1 >> 4].x8h;
                    }
                    else if (get_a) a = CPURegisters[s1 >> 4][a_sizecode];
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
                        b = CPURegisters[s2 & 15].x8h;
                    }
                    else b = CPURegisters[s2 & 15][b_sizecode];
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
                    if ((s1 & 2) != 0) CPURegisters[s1 >> 4].x8h = (byte)res;
                    else CPURegisters[s1 >> 4][sizecode] = res;
                    return true;

                case 3:
                case 4:
                    return SetMemRaw(m, Size(sizecode), res);

                default: Terminate(ErrorCode.UndefinedBehavior); return false;
            }
        }

        /*
        [4: dest][2: size][1: dh][1: mem]
            mem = 0:             dest <- f(dest)
            mem = 1: [address]   M[address] <- f(M[address])
            (dh marks AH, BH, CH, or DH for dest)
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
                        if (get_a) a = CPURegisters[s >> 4].x8h;
                    }
                    else if (get_a) a = CPURegisters[s >> 4][a_sizecode];
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
                    if ((s & 2) != 0) CPURegisters[s >> 4].x8h = (byte)res;
                    else CPURegisters[s >> 4][sizecode] = res;
                    return true;

                case 1:
                    return SetMemRaw(m, Size(sizecode), res);

                default: return true; // this can't happen but compiler is stupid
            }
        }

        /*
        [4: dest][2: size][1: dh][1: mem]   [1: CL][1:][6: count]   ([address])
        */
        private bool FetchShiftOpFormat(out UInt64 s, out UInt64 m, out UInt64 val, out UInt64 count)
        {
            m = val = count = 0; // zero these because compiler is a vengeful god

            // read settings byte
            if (!GetMemAdv(1, out s) || !GetMemAdv(1, out count)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            // if count set CL flag, replace it with that
            if ((count & 0x80) != 0) count = CL;
            // mask count
            count = count & (sizecode == 3 ? 0x3ful : 0x1ful);

            // if dest is a register
            if ((s & 1) == 0)
            {
                // if high flag set
                if ((s & 2) != 0)
                {
                    // need to be in (ABCD)H
                    if ((s & 0xc0) != 0 || sizecode != 0) { Terminate(ErrorCode.UndefinedBehavior); return false; }
                    val = CPURegisters[s >> 4].x8h;
                }
                else val = CPURegisters[s >> 4][sizecode];

                return true;
            }
            // otherwise is memory value
            else return GetAddressAdv(out m) && GetMemRaw(m, Size(sizecode), out val);
        }
        private bool StoreShiftOpFormat(UInt64 s, UInt64 m, UInt64 res)
        {
            UInt64 sizecode = (s >> 2) & 3;

            // if dest is a register
            if ((s & 1) == 0)
            {
                // if high flag set
                if ((s & 2) != 0) CPURegisters[s >> 4].x8h = (byte)res;
                else CPURegisters[s >> 4][sizecode] = res;

                return true;
            }
            // otherwise dest is memory
            else return SetMemRaw(m, Size(sizecode), res);
        }

        /*
        [4: reg][2: size][2: mode]
            mode = 0:               reg
            mode = 1:               h reg (AH, BH, CH, or DH)
            mode = 2: [size: imm]   imm
            mode = 3: [address]     M[address]
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
                    a = CPURegisters[s >> 4][a_sizecode];
                    return true;

                case 1:
                    if ((s & 0xc0) != 0) { Terminate(ErrorCode.UndefinedBehavior); return false; }
                    a = CPURegisters[s >> 4].x8h;
                    return true;

                case 2: return GetMemAdv(Size(a_sizecode), out a);

                case 3: return GetAddressAdv(out a) && GetMemRaw(a, Size(a_sizecode), out a);
            }

            return true;
        }

        /*
        [4: dest][2: size][1: dh][1: mem]   [1: src_1_h][3:][4: src_1]
            mem = 0: [1: src_2_h][3:][4: src_2]
            mem = 1: [address_src_2]
        */
        private bool FetchRR_RMFormat(out UInt64 s, out UInt64 dest, out UInt64 a, out UInt64 b)
        {
            dest = a = b = 0; // zero these so compiler won't complain

            if (!GetMemAdv(1, out s) || !GetMemAdv(1, out a)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            // if dest is high
            if ((s & 2) != 0)
            {
                if (sizecode != 0 || (s & 0xc0) != 0) { Terminate(ErrorCode.UndefinedBehavior); return false; }
                dest = CPURegisters[s >> 4].x8h;
            }
            else dest = CPURegisters[s >> 4][sizecode];

            // if a is high
            if ((a & 128) != 0)
            {
                if (sizecode != 0 || (a & 0x0c) != 0) { Terminate(ErrorCode.UndefinedBehavior); return false; }
                a = CPURegisters[a & 15].x8h;
            }
            else a = CPURegisters[a & 15][sizecode];

            // if b is register
            if ((s & 1) == 0)
            {
                if (!GetMemAdv(1, out b)) return false;

                // if b is high
                if ((b & 128) != 0)
                {
                    if (sizecode != 0 || (b & 0x0c) != 0) { Terminate(ErrorCode.UndefinedBehavior); return false; }
                    b = CPURegisters[b & 15].x8h;
                }
                else b = CPURegisters[b & 15][sizecode];
            }
            // otherwise b is memory
            else
            {
                if (!GetAddressAdv(out b) || !GetMemRaw(b, Size(sizecode), out b)) return false;
            }

            return true;
        }
        private bool StoreRR_RMFormat(UInt64 s, UInt64 res)
        {
            // if dest is high
            if ((s & 2) != 0) CPURegisters[s >> 4].x8h = (byte)res;
            else CPURegisters[s >> 4][(s >> 2) & 3] = res;

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
                case 0:
                case 1: // VM and RF flags are cleared in the stored image
                case 2:
                    return PushRaw(Size(ext + 1), RFLAGS & ~0x30000ul);

                default: Terminate(ErrorCode.UndefinedBehavior); return false;
            }
        }
        private bool ProcessPOPF()
        {
            if (!GetMemAdv(1, out UInt64 ext)) return false;

            switch (ext)
            {
                case 0:
                case 1: // can't modify reserved flags
                case 2:
                    if (!PopRaw(Size(ext + 1), out ext)) return false;
                    RFLAGS = RFLAGS & ~0x003f0fd5ul | ext & 0x003f0fd5ul;
                    return true;

                default: Terminate(ErrorCode.UndefinedBehavior); return false;
            }
        }

        /*
        [1: value][7: flag_id]
            flag = 0: CF
            flag = 1: IF
            flag = 2: DF
            flag = 3: AC
            else UND
        */
        private bool ProcessFlagManip()
        {
            if (!GetMemAdv(1, out UInt64 s)) return false;

            bool value = (s & 0x80) != 0;
            switch (s & 0x7f)
            {
                case 0: CF = value; return true;
                case 1: IF = value; return true;
                case 2: DF = value; return true;
                case 3: AC = value; return true;

                default: Terminate(ErrorCode.UndefinedBehavior); return false;
            }
        }

        /*
        [op][cnd]
            cnd = 0: Z
            cnd = 1: NZ
            cnd = 2: S
            cnd = 3: NS
            cnd = 4: P
            cnd = 5: NP
            cnd = 6: O
            cnd = 7: NO
            cnd = 8: C
            cnd = 9: NC
            cnd = 10: B
            cnd = 11: BE
            cnd = 12: A
            cnd = 13: AE
            cnd = 14: L
            cnd = 15: LE
            cnd = 16: G
            cnd = 17: GE
        */
        private bool ProcessSETcc()
        {
            if (!GetMemAdv(1, out UInt64 ext)) return false;
            if (!FetchUnaryOpFormat(out UInt64 s, out UInt64 m, out UInt64 _dest, false, 0)) return false;

            // get the flag
            bool flag;
            switch (ext)
            {
                case 0: flag = ZF; break;
                case 1: flag = !ZF; break;
                case 2: flag = SF; break;
                case 3: flag = !SF; break;
                case 4: flag = PF; break;
                case 5: flag = !PF; break;
                case 6: flag = OF; break;
                case 7: flag = !OF; break;
                case 8: flag = CF; break;
                case 9: flag = !CF; break;
                case 10: flag = cc_b; break;
                case 11: flag = cc_be; break;
                case 12: flag = cc_a; break;
                case 13: flag = cc_ae; break;
                case 14: flag = cc_l; break;
                case 15: flag = cc_le; break;
                case 16: flag = cc_g; break;
                case 17: flag = cc_ge; break;

                default: Terminate(ErrorCode.UndefinedBehavior); return false;
            }

            return StoreUnaryOpFormat(s, m, flag ? 1 : 0ul);
        }

        private bool ProcessMOV()
        {
            if (!FetchBinaryOpFormatNew(out UInt64 s1, out UInt64 s2, out UInt64 m, out UInt64 a, out UInt64 b, false)) return false;

            return StoreBinaryOpFormatNew(s1, s2, m, b);
        }
        /*
        [op][cnd]
            cnd = 0: Z
            cnd = 1: NZ
            cnd = 2: S
            cnd = 3: NS
            cnd = 4: P
            cnd = 5: NP
            cnd = 6: O
            cnd = 7: NO
            cnd = 8: C
            cnd = 9: NC
            cnd = 10: B
            cnd = 11: BE
            cnd = 12: A
            cnd = 13: AE
            cnd = 14: L
            cnd = 15: LE
            cnd = 16: G
            cnd = 17: GE
        */
        private bool ProcessMOVcc()
        {
            if (!GetMemAdv(1, out UInt64 ext)) return false;
            if (!FetchBinaryOpFormatNew(out UInt64 s1, out UInt64 s2, out UInt64 m, out UInt64 _dest, out UInt64 src, false)) return false;

            // get the flag
            bool flag;
            switch (ext)
            {
                case 0: flag = ZF; break;
                case 1: flag = !ZF; break;
                case 2: flag = SF; break;
                case 3: flag = !SF; break;
                case 4: flag = PF; break;
                case 5: flag = !PF; break;
                case 6: flag = OF; break;
                case 7: flag = !OF; break;
                case 8: flag = CF; break;
                case 9: flag = !CF; break;
                case 10: flag = cc_b; break;
                case 11: flag = cc_be; break;
                case 12: flag = cc_a; break;
                case 13: flag = cc_ae; break;
                case 14: flag = cc_l; break;
                case 15: flag = cc_le; break;
                case 16: flag = cc_g; break;
                case 17: flag = cc_ge; break;

                default: Terminate(ErrorCode.UndefinedBehavior); return false;
            }

            // if flag is true, store result
            if (flag) return StoreBinaryOpFormatNew(s1, s2, m, src);
            // even in false case upper 32 bits must be cleared in the case of a conditional 32-bit register load
            else
            {
                // if it's a 32-bit register load
                if (((s1 >> 2) & 3) == 2 && (s2 >> 4) <= 2)
                {
                    // load 32-bit partition to itself (internally zeroes high bits)
                    CPURegisters[s1 >> 4].x32 = CPURegisters[s1 >> 4].x32;
                }

                return true;
            }
        }

        /*
        [4: r1][2: size][1: r1h][1: mem]
	        mem = 0: [1: r2h][3:][4: r2]
		        r1 <- r2
		        r2 <- r1
	        mem = 1: [address]
		        r1 <- M[address]
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
                temp_1 = CPURegisters[a >> 4].x8h;
            }
            else temp_1 = CPURegisters[a >> 4][sizecode];

            // if b is reg
            if ((a & 1) == 0)
            {
                if (!GetMemAdv(1, out b)) return false;

                // if b is high
                if ((b & 128) != 0)
                {
                    if ((b & 0x0c) != 0 || sizecode != 0) { Terminate(ErrorCode.UndefinedBehavior); return false; }
                    temp_2 = CPURegisters[b & 15].x8h;
                    CPURegisters[b & 15].x8h = (byte)temp_1;
                }
                else
                {
                    temp_2 = CPURegisters[b & 15][sizecode];
                    CPURegisters[b & 15][sizecode] = temp_1;
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
            if ((a & 2) != 0) CPURegisters[a >> 4].x8h = (byte)temp_2;
            else CPURegisters[a >> 4][sizecode] = temp_2;

            return true;
        }

        private bool ProcessJMP(ref UInt64 aft)
        {
            if (!FetchIMMRMFormat(out UInt64 s, out UInt64 val)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            // 8-bit addressing not allowed
            if (sizecode == 0) { Terminate(ErrorCode.UndefinedBehavior); return false; }

            aft = RIP; // record point immediately after reading (for CALL return address)
            RIP = val; // jump

            return true;
        }
        /*
        [op][cnd]
            cnd = 0: Z
            cnd = 1: NZ
            cnd = 2: S
            cnd = 3: NS
            cnd = 4: P
            cnd = 5: NP
            cnd = 6: O
            cnd = 7: NO
            cnd = 8: C
            cnd = 9: NC
            cnd = 10: B
            cnd = 11: BE
            cnd = 12: A
            cnd = 13: AE
            cnd = 14: L
            cnd = 15: LE
            cnd = 16: G
            cnd = 17: GE
            cnd = 18: CXZ
            cnd = 19: ECXZ
            cnd = 20: RCXZ
        */
        private bool ProcessJcc()
        {
            if (!GetMemAdv(1, out UInt64 ext)) return false;
            if (!FetchIMMRMFormat(out UInt64 s, out UInt64 val)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            // 8-bit addressing not allowed
            if (sizecode == 0) { Terminate(ErrorCode.UndefinedBehavior); return false; }

            // get the flag
            bool flag;
            switch (ext)
            {
                case 0: flag = ZF; break;
                case 1: flag = !ZF; break;
                case 2: flag = SF; break;
                case 3: flag = !SF; break;
                case 4: flag = PF; break;
                case 5: flag = !PF; break;
                case 6: flag = OF; break;
                case 7: flag = !OF; break;
                case 8: flag = CF; break;
                case 9: flag = !CF; break;
                case 10: flag = cc_b; break;
                case 11: flag = cc_be; break;
                case 12: flag = cc_a; break;
                case 13: flag = cc_ae; break;
                case 14: flag = cc_l; break;
                case 15: flag = cc_le; break;
                case 16: flag = cc_g; break;
                case 17: flag = cc_ge; break;
                case 18: flag = CX == 0; break;
                case 19: flag = ECX == 0; break;
                case 20: flag = RCX == 0; break;

                default: Terminate(ErrorCode.UndefinedBehavior); return false;
            }

            if (flag) RIP = val; // jump

            return true;
        }
        private bool ProcessLOOPcc()
        {
            // get the cc continue flag
            bool continue_flag;
            if (!GetMemAdv(1, out UInt64 ext)) return false;
            switch (ext)
            {
                case 0: continue_flag = true; break; // LOOP
                case 1: continue_flag = ZF; break;   // LOOPe
                case 2: continue_flag = !ZF; break;  // LOOPne

                default: Terminate(ErrorCode.UndefinedBehavior); return false;
            }

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
        [4: dest][2: size][1:][1: mem]
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
                CPURegisters[s >> 4][sizecode] = val;
                return true;
            }
            // otherwise is memory
            else return GetAddressAdv(out s) && SetMemRaw(s, Size(sizecode), val);
        }

        /*
        [4: dest][2: size][2:]   [address]
            dest <- address
        */
        private bool ProcessLEA()
        {
            if (!GetMemAdv(1, out UInt64 s) || !GetAddressAdv(out UInt64 address)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            // LEA doesn't allow 8-bit addressing
            if (sizecode == 0) { Terminate(ErrorCode.UndefinedBehavior); return false; }

            CPURegisters[s >> 4][sizecode] = address;
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
            if (!FetchShiftOpFormat(out UInt64 s, out UInt64 m, out UInt64 val, out UInt64 count)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            // shift of zero is no-op
            if (count != 0)
            {
                UInt64 res = Truncate(val << (UInt16)count, sizecode);

                UpdateFlagsZSP(res, sizecode);
                CF = count < SizeBits(sizecode) ? ((val >> (UInt16)(SizeBits(sizecode) - count)) & 1) == 1 : Rand.NextBool(); // CF holds last bit shifted out (UND for sh >= #bits)
                OF = count == 1 ? Negative(res, sizecode) != CF : Rand.NextBool(); // OF is 1 if top 2 bits of original value were different (UND for sh != 1)
                AF = Rand.NextBool(); // AF is undefined

                return StoreShiftOpFormat(s, m, res);
            }
            else return true;
        }
        private bool ProcessSHR()
        {
            if (!FetchShiftOpFormat(out UInt64 s, out UInt64 m, out UInt64 val, out UInt64 count)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            // shift of zero is no-op
            if (count != 0)
            {
                UInt64 res = val >> (UInt16)count;

                UpdateFlagsZSP(res, sizecode);
                CF = count < SizeBits(sizecode) ? ((val >> (UInt16)(count - 1)) & 1) == 1 : Rand.NextBool(); // CF holds last bit shifted out (UND for sh >= #bits)
                OF = count == 1 ? Negative(val, sizecode) : Rand.NextBool(); // OF is high bit of original value (UND for sh != 1)
                AF = Rand.NextBool(); // AF is undefined

                return StoreShiftOpFormat(s, m, res);
            }
            else return true;
        }

        private bool ProcessSAL()
        {
            if (!FetchShiftOpFormat(out UInt64 s, out UInt64 m, out UInt64 val, out UInt64 count)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            // shift of zero is no-op
            if (count != 0)
            {
                UInt64 res = Truncate((UInt64)((Int64)SignExtend(val, sizecode) << (UInt16)count), sizecode);

                UpdateFlagsZSP(res, sizecode);
                CF = count < SizeBits(sizecode) ? ((val >> (UInt16)(SizeBits(sizecode) - count)) & 1) == 1 : Rand.NextBool(); // CF holds last bit shifted out (UND for sh >= #bits)
                OF = count == 1 ? Negative(res, sizecode) != CF : Rand.NextBool(); // OF is 1 if top 2 bits of original value were different (UND for sh != 1)
                AF = Rand.NextBool(); // AF is undefined

                return StoreShiftOpFormat(s, m, res);
            }
            else return true;
        }
        private bool ProcessSAR()
        {
            if (!FetchShiftOpFormat(out UInt64 s, out UInt64 m, out UInt64 val, out UInt64 count)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            // shift of zero is no-op
            if (count != 0)
            {
                UInt64 res = Truncate((UInt64)((Int64)SignExtend(val, sizecode) >> (UInt16)count), sizecode);

                UpdateFlagsZSP(res, sizecode);
                CF = count < SizeBits(sizecode) ? ((val >> (UInt16)(count - 1)) & 1) == 1 : Rand.NextBool(); // CF holds last bit shifted out (UND for sh >= #bits)
                OF = count == 1 ? false : Rand.NextBool(); // OF is cleared (UND for sh != 1)
                AF = Rand.NextBool(); // AF is undefined

                return StoreShiftOpFormat(s, m, res);
            }
            else return false;
        }

        private bool ProcessROL()
        {
            if (!FetchShiftOpFormat(out UInt64 s, out UInt64 m, out UInt64 val, out UInt64 count)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            count %= SizeBits(sizecode); // rotate performed modulo-n

            // shift of zero is no-op
            if (count != 0)
            {
                UInt64 res = Truncate((val << (UInt16)count) | (val >> (UInt16)(SizeBits(sizecode) - count)), sizecode);

                CF = ((val >> (UInt16)(SizeBits(sizecode) - count)) & 1) == 1; // CF holds last bit shifted around
                OF = count == 1 ? CF ^ Negative(res, sizecode) : Rand.NextBool(); // OF is xor of CF (after rotate) and high bit of result (UND if sh != 1)

                return StoreShiftOpFormat(s, m, res);
            }
            else return true;
        }
        private bool ProcessROR()
        {
            if (!FetchShiftOpFormat(out UInt64 s, out UInt64 m, out UInt64 val, out UInt64 count)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            count %= SizeBits(sizecode); // rotate performed modulo-n

            // shift of zero is no-op
            if (count != 0)
            {
                UInt64 res = Truncate((val >> (UInt16)count) | (val << (UInt16)(SizeBits(sizecode) - count)), sizecode);

                CF = ((val >> (UInt16)(count - 1)) & 1) == 1; // CF holds last bit shifted around
                OF = count == 1 ? Negative(res, sizecode) ^ (((res >> (UInt16)(SizeBits(sizecode) - 2)) & 1) != 0) : Rand.NextBool(); // OF is xor of 2 highest bits of result

                return StoreShiftOpFormat(s, m, res);
            }
            else return true;
        }

        private bool ProcessRCL()
        {
            if (!FetchShiftOpFormat(out UInt64 s, out UInt64 m, out UInt64 val, out UInt64 count)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            count %= SizeBits(sizecode) + 1; // rotate performed modulo-n+1

            // shift of zero is no-op
            if (count != 0)
            {
                UInt64 res = val, temp;
                UInt64 high_mask = 1ul << (UInt16)(SizeBits(sizecode) - 1); // mask for highest bit
                for (UInt16 i = 0; i < count; ++i)
                {
                    temp = res << 1; // shift res left by 1, store in temp
                    temp |= CF ? 1 : 0ul; // or in CF to the lowest bit
                    CF = (res & high_mask) != 0; // get the previous highest bit into CF
                    res = temp; // store back to res
                }

                OF = count == 1 ? CF ^ Negative(res, sizecode) : Rand.NextBool(); // OF is xor of CF (after rotate) and high bit of result (UND if sh != 1)

                return StoreShiftOpFormat(s, m, res);
            }
            else return true;
        }
        private bool ProcessRCR()
        {
            if (!FetchShiftOpFormat(out UInt64 s, out UInt64 m, out UInt64 val, out UInt64 count)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            count %= SizeBits(sizecode) + 1; // rotate performed modulo-n+1

            // shift of zero is no-op
            if (count != 0)
            {
                UInt64 res = val, temp;
                UInt64 high_mask = 1ul << (UInt16)(SizeBits(sizecode) - 1); // mask for highest bit
                for (UInt16 i = 0; i < count; ++i)
                {
                    temp = res >> 1; // shift res right by 1, store in temp
                    temp |= CF ? high_mask : 0ul; // or in CF to the highest bit
                    CF = (res & 1) != 0; // get the previous low bit into CF
                    res = temp; // store back to res
                }

                OF = count == 1 ? Negative(res, sizecode) ^ (((res >> (UInt16)(SizeBits(sizecode) - 2)) & 1) != 0) : Rand.NextBool(); // OF is xor of 2 highest bits of result

                return StoreShiftOpFormat(s, m, res);
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
                    res = (a << 32) | (a >> 32);
                    res = ((a & 0x0000ffff0000ffff) << 16) | ((a & 0xffff0000ffff0000) >> 16);
                    res = ((a & 0x00ff00ff00ff00ff) << 8) | ((a & 0xff00ff00ff00ff00) >> 8);
                    break;
                case 2:
                    res = (a << 16) | (a >> 16);
                    res = ((a & 0x00ff00ff) << 8) | ((a & 0xff00ff00) >> 8);
                    break;
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
            if (!FetchRR_RMFormat(out UInt64 s, out UInt64 dest, out UInt64 a, out UInt64 b)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            // only supports 32 and 64-bit operands
            if (sizecode != 2 && sizecode != 3) { Terminate(ErrorCode.UndefinedBehavior); return false; }

            UInt64 res = ~a & b;

            ZF = res == 0;
            SF = Negative(res, sizecode);
            OF = false;
            CF = false;
            AF = Rand.NextBool();
            PF = Rand.NextBool();

            return StoreRR_RMFormat(s, res);
        }

        private bool ProcessBTx()
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

        /*
        [8: ext]
            ext = 0: CWD
            ext = 1: CDQ
            ext = 2: CQO
            ext = 3: CBW
            ext = 4: CWDE
            ext = 5: CDQE
        */
        private bool ProcessCxy()
        {
            if (!GetMemAdv(1, out UInt64 ext)) return false;

            switch (ext)
            {
                case 0: DX = (AX & 0x8000) == 0 ? (UInt16)0 : (UInt16)0xffff; return true;
                case 1: EDX = (EAX & 0x80000000) == 0 ? 0u : 0xffffffff; return true;
                case 2: RDX = (RAX & 0x8000000000000000) == 0 ? 0ul : 0xffffffffffffffff; return true;

                case 3: AX = (UInt16)SignExtend(AL, 0); return true;
                case 4: EAX = (UInt32)SignExtend(AX, 1); return true;
                case 5: RAX = SignExtend(EAX, 2); return true;

                default: Terminate(ErrorCode.UndefinedBehavior); return false;
            }
        }
        /*
        [4: dest][4: mode]   [1: mem][1: sh][2:][4: src]
            mode = 0: 16 <- 8  Zero
            mode = 1: 16 <- 8  Sign
            mode = 2: 32 <- 8  Zero
            mode = 3: 32 <- 16 Zero
            mode = 4: 32 <- 8  Sign
            mode = 5: 32 <- 16 Sign
            mode = 6: 64 <- 8  Zero
            mode = 7: 64 <- 16 Zero
            mode = 8: 64 <- 8  Sign
            mode = 9: 64 <- 16 Sign
            else UND
            (sh marks that source is (ABCD)H)
        */
        private bool ProcessMOVxX()
        {
            if (!GetMemAdv(1, out UInt64 s1) || !GetMemAdv(1, out UInt64 s2)) return false;

            UInt64 src; // source value to be extended

            // if source is register
            if ((s2 & 128) == 0)
            {
                switch (s1 & 15)
                {
                    case 0: case 1: case 2: case 4: case 6: case 8:
                        if ((s2 & 64) != 0) // if high register
                        {
                            // make sure we're in registers A-D
                            if ((s2 & 0x0c) != 0) { Terminate(ErrorCode.UndefinedBehavior); return false; }
                            src = CPURegisters[s2 & 15].x8h;
                        }
                        else src = CPURegisters[s2 & 15].x8;
                        break;
                    case 3: case 5: case 7: case 9: src = CPURegisters[s2 & 15].x16; break;

                    default: Terminate(ErrorCode.UndefinedBehavior); return false;
                }
            }
            // otherwise is memory value
            else
            {
                if (!GetAddressAdv(out src)) return false;
                switch (s1 & 15)
                {
                    case 0: case 1: case 2: case 4: case 6: case 8: if (!GetMemRaw(src, 1, out src)) return false; break;
                    case 3: case 5: case 7: case 9: if (!GetMemRaw(src, 2, out src)) return false; break;

                    default: Terminate(ErrorCode.UndefinedBehavior); return false;
                }
            }

            // store the value
            switch (s1 & 15)
            {
                case 0: CPURegisters[s1 >> 4].x16 = (UInt16)src; break;
                case 1: CPURegisters[s1 >> 4].x16 = (UInt16)SignExtend(src, 0); break;

                case 2: case 3: CPURegisters[s1 >> 4].x32 = (UInt32)src; break;
                case 4: CPURegisters[s1 >> 4].x32 = (UInt32)SignExtend(src, 0); break;
                case 5: CPURegisters[s1 >> 4].x32 = (UInt32)SignExtend(src, 1); break;

                case 6: case 7: CPURegisters[s1 >> 4].x64 = src; break;
                case 8: CPURegisters[s1 >> 4].x64 = SignExtend(src, 0); break;
                case 9: CPURegisters[s1 >> 4].x64 = SignExtend(src, 1); break;
            }

            return true;
        }

        // -- floating point stuff -- //

        /*
        [1:][3: i][1:][3: mode]
            mode = 0: st(0) <- f(st(0), st(i))
            mode = 1: st(i) <- f(st(i), st(0))
            mode = 2: || + pop
            mode = 3: st(0) <- f(st(0), fp32M)
            mode = 4: st(0) <- f(st(0), fp64M)
            mode = 5: st(0) <- f(st(0), int16M)
            mode = 6: st(0) <- f(st(0), int32M)
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
                    a = FPURegisters[TOP].Value; b = FPURegisters[(TOP + (s >> 4)) & 7].Value; return true;
                case 1:
                case 2:
                    if (!FPURegisters[TOP].InUse || !FPURegisters[(TOP + (s >> 4)) & 7].InUse) { Terminate(ErrorCode.FPUAccessViolation); a = b = 0; return false; }
                    b = FPURegisters[TOP].Value; a = FPURegisters[(TOP + (s >> 4)) & 7].Value; return true;
                
                default:
                    if (!FPURegisters[TOP].InUse) { Terminate(ErrorCode.FPUAccessViolation); a = b = 0; return false; }
                    a = FPURegisters[TOP].Value; b = 0;
                    if (!GetAddressAdv(out UInt64 m)) return false;
                    switch (s & 7)
                    {
                        case 3: if (!GetMemRaw(m, 4, out m)) return false; b = AsFloat((UInt32)m); return true;
                        case 4: if (!GetMemRaw(m, 8, out m)) return false; b = AsDouble(m); return true;
                        case 5: if (!GetMemRaw(m, 2, out m)) return false; b = (Int64)SignExtend(m, 1); return true;
                        case 6: if (!GetMemRaw(m, 4, out m)) return false; b = (Int64)SignExtend(m, 2); return true;

                        default: Terminate(ErrorCode.UndefinedBehavior); return false;
                    }
            }
        }
        private bool StoreFPUBinaryFormat(UInt64 s, double res)
        {
            switch (s & 7)
            {
                case 1: FPURegisters[(TOP + (s >> 4)) & 7].Value = res; return true;
                case 2: FPURegisters[(TOP + (s >> 4)) & 7].Value = res; return PopFPU(out res);

                default: FPURegisters[TOP].Value = res; return true;
            }
        }

        private bool PushFPU(double val)
        {
            // decrement top (wraps automatically as a 3-bit unsigned value)
            --TOP;

            // if this fpu reg is in use, it's an error
            if (FPURegisters[TOP].InUse) { Terminate(ErrorCode.FPUStackOverflow); return false; }

            // store the value
            FPURegisters[TOP].Value = val;
            FPURegisters[TOP].InUse = true;

            return true;
        }
        private bool PopFPU(out double val)
        {
            // if this register is not in use, it's an error
            if (!FPURegisters[TOP].InUse) { Terminate(ErrorCode.FPUStackUnderflow); val = 0; return false; }

            // extract the value
            val = FPURegisters[TOP].Value;
            FPURegisters[TOP].InUse = false;

            // increment top (wraps automatically as a 3-bit unsigned value)
            ++TOP;
            
            return true;
        }

        private bool ProcessFLD_const()
        {
            if (!GetMemAdv(1, out UInt64 ext)) return false;

            C0 = Rand.NextBool();
            C1 = Rand.NextBool();
            C2 = Rand.NextBool();
            C3 = Rand.NextBool();

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
        /*
        [1:][3: i][1:][3: mode]
            mode = 0: push st(i)
            mode = 1: push m32fp
            mode = 2: push m64fp
            mode = 3: push m16int
            mode = 4: push m32int
            mode = 5: push m64int
            else UND
        */
        private bool ProcessFLD()
        {
            if (!GetMemAdv(1, out UInt64 s)) return false;

            C0 = Rand.NextBool();
            C1 = Rand.NextBool();
            C2 = Rand.NextBool();
            C3 = Rand.NextBool();

            // switch through mode
            switch (s & 7)
            {
                case 0:
                    if (!FPURegisters[(TOP + (s >> 4)) & 7].InUse) { Terminate(ErrorCode.FPUAccessViolation); return false; }
                    return PushFPU(FPURegisters[(TOP + (s >> 4)) & 7].Value);

                default:
                    if (!GetAddressAdv(out UInt64 m)) return false;
                    switch (s & 7)
                    {
                        case 1: if (!GetMemRaw(m, 4, out m)) return false; return PushFPU(AsFloat((UInt32)m));
                        case 2: if (!GetMemRaw(m, 8, out m)) return false; return PushFPU(AsDouble(m));

                        case 3: if (!GetMemRaw(m, 2, out m)) return false; return PushFPU((Int64)SignExtend(m, 1));
                        case 4: if (!GetMemRaw(m, 4, out m)) return false; return PushFPU((Int64)SignExtend(m, 2));
                        case 5: if (!GetMemRaw(m, 8, out m)) return false; return PushFPU((Int64)m);

                        default: Terminate(ErrorCode.UndefinedBehavior); return false;
                    }
            }
        }
        /*
        [1:][3: i][4: mode]
            mode = 0: st(i) <- st(0)
            mode = 1: || + pop
            mode = 2: fp32M <- st(0)
            mode = 3: || + pop
            mode = 4: fp64M <- st(0)
            mode = 5: || + pop
            mode = 6: int16M <- st(0)
            mode = 7: || + pop
            mode = 8: int32M <- st(0)
            mode = 9: || + pop
            mode = 10: int64M <- st(0) + pop
            else UND
        */
        private bool ProcessFST()
        {
            if (!GetMemAdv(1, out UInt64 s)) return false;

            C0 = Rand.NextBool();
            C1 = Rand.NextBool();
            C2 = Rand.NextBool();
            C3 = Rand.NextBool();

            switch (s & 15)
            {
                case 0:
                case 1:
                    // make sure we can read the value
                    if (!FPURegisters[TOP].InUse) { Terminate(ErrorCode.FPUAccessViolation); return false; }
                    // record the value (is allowed to be not in use)
                    FPURegisters[(TOP + (s >> 4)) & 7].Value = FPURegisters[TOP].Value;
                    FPURegisters[(TOP + (s >> 4)) & 7].InUse = true;
                    break;

                default:
                    if (!FPURegisters[TOP].InUse) { Terminate(ErrorCode.FPUAccessViolation); return false; }
                    if (!GetAddressAdv(out UInt64 m)) return false;
                    switch (s & 15)
                    {
                        case 2: case 3: if (!SetMemRaw(m, 4, FloatAsUInt64((float)FPURegisters[TOP].Value))) return false; break;
                        case 4: case 5: if (!SetMemRaw(m, 8, DoubleAsUInt64(FPURegisters[TOP].Value))) return false; break;
                        case 6: case 7: if (!SetMemRaw(m, 2, (UInt64)(Int64)FPURegisters[TOP].Value)) return false; break;
                        case 8: case 9: if (!SetMemRaw(m, 4, (UInt64)(Int64)FPURegisters[TOP].Value)) return false; break;
                        case 10: if (!SetMemRaw(m, 8, (UInt64)(Int64)FPURegisters[TOP].Value)) return false; break;

                        default: Terminate(ErrorCode.UndefinedBehavior); return false;
                    }
                    break;
            }

            switch (s & 15)
            {
                case 1: case 3: case 5: case 7: case 9: return PopFPU(out double temp);
                default: return true;
            }
        }
        private bool ProcessFXCH()
        {
            if (!GetMemAdv(1, out UInt64 i)) return false;

            // make sure they're both in use
            if (!FPURegisters[TOP].InUse || !FPURegisters[(TOP + i) & 7].InUse) { Terminate(ErrorCode.FPUAccessViolation); return false; }

            double temp = FPURegisters[TOP].Value;
            FPURegisters[TOP].Value = FPURegisters[(TOP + i) & 7].Value;
            FPURegisters[(TOP + i) & 7].Value = temp;

            C0 = Rand.NextBool();
            C1 = false;
            C2 = Rand.NextBool();
            C3 = Rand.NextBool();

            return true;
        }
        /*
        [1:][3: i][1:][3: cc]
            cc = 0: E  (=Z)
            cc = 1: NE (=NZ)
            cc = 2: B
            cc = 3: BE
            cc = 4: A
            cc = 5: AE
            cc = 6: U  (=P)
            cc = 7: NU (=NP)
        */
        private bool ProcessFMOVcc()
        {
            if (!GetMemAdv(1, out UInt64 s)) return false;

            // get the flag
            bool flag;
            switch (s & 7)
            {
                case 0: flag = ZF; break;
                case 1: flag = !ZF; break;
                case 2: flag = cc_b; break;
                case 3: flag = cc_be; break;
                case 4: flag = cc_a; break;
                case 5: flag = cc_ae; break;
                case 6: flag = PF; break;
                case 7: flag = !PF; break;

                default: Terminate(ErrorCode.UndefinedBehavior); return false;
            }

            // if flag is set, do the move
            if (flag)
            {
                if (!FPURegisters[TOP].InUse || !FPURegisters[(TOP + (s >> 4)) & 7].InUse) { Terminate(ErrorCode.FPUAccessViolation); return false; }
                FPURegisters[TOP].Value = FPURegisters[(TOP + (s >> 4)) & 7].Value;
            }

            C0 = Rand.NextBool();
            C1 = Rand.NextBool();
            C2 = Rand.NextBool();
            C3 = Rand.NextBool();

            return true;
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

        private bool ProcessF2XM1()
        {
            if (!FPURegisters[TOP].InUse) { Terminate(ErrorCode.FPUAccessViolation); return false; }

            // get the value
            double val = FPURegisters[TOP].Value;
            // val must be in range [-1, 1]
            if (val < -1 || val > 1) { Terminate(ErrorCode.FPUError); return false; }

            FPURegisters[TOP].Value = Math.Pow(2, val) - 1;

            C0 = Rand.NextBool();
            C1 = Rand.NextBool();
            C2 = Rand.NextBool();
            C3 = Rand.NextBool();

            return true;
        }
        private bool ProcessFABS()
        {
            if (!FPURegisters[TOP].InUse) { Terminate(ErrorCode.FPUAccessViolation); return false; }

            FPURegisters[TOP].Value = Math.Abs(FPURegisters[TOP].Value);

            C0 = Rand.NextBool();
            C1 = false;
            C2 = Rand.NextBool();
            C3 = Rand.NextBool();

            return true;
        }
        private bool ProcessFCHS()
        {
            if (!FPURegisters[TOP].InUse) { Terminate(ErrorCode.FPUAccessViolation); return false; }

            FPURegisters[TOP].Value = -FPURegisters[TOP].Value;

            C0 = Rand.NextBool();
            C1 = false;
            C2 = Rand.NextBool();
            C3 = Rand.NextBool();

            return true;
        }
        private bool ProcessFPREM()
        {
            if (!FPURegisters[TOP].InUse || !FPURegisters[(TOP + 1) & 7].InUse) { Terminate(ErrorCode.FPUAccessViolation); return false; }

            double a = FPURegisters[TOP].Value;
            double b = FPURegisters[(TOP + 1) & 7].Value;

            // compute remainder with truncated quotient
            double res = a - (Int64)(a / b) * b;

            // store value
            FPURegisters[TOP].Value = res;

            // get the bits
            UInt64 bits = DoubleAsUInt64(res);

            C0 = (bits & 4) != 0;
            C1 = (bits & 1) != 0;
            C2 = false;
            C3 = (bits & 2) != 0;

            return true;
        }
        private bool ProcessFPREM1()
        {
            if (!FPURegisters[TOP].InUse || !FPURegisters[(TOP + 1) & 7].InUse) { Terminate(ErrorCode.FPUAccessViolation); return false; }

            double a = FPURegisters[TOP].Value;
            double b = FPURegisters[(TOP + 1) & 7].Value;

            // compute remainder with truncated quotient (IEEE)
            double res = Math.IEEERemainder(a, b);

            // store value
            FPURegisters[TOP].Value = res;

            // get the bits
            UInt64 bits = DoubleAsUInt64(res);

            C0 = (bits & 4) != 0;
            C1 = (bits & 1) != 0;
            C2 = false;
            C3 = (bits & 2) != 0;
            
            return true;
        }
        private bool ProcessFRNDINT()
        {
            if (!FPURegisters[TOP].InUse) { Terminate(ErrorCode.FPUAccessViolation); return false; }

            double val = FPURegisters[TOP].Value;
            double res = (Int64)val;

            FPURegisters[TOP].Value = res;

            C0 = Rand.NextBool();
            C1 = res > val;
            C2 = Rand.NextBool();
            C3 = Rand.NextBool();
            
            return true;
        }
        private bool ProcessFSQRT()
        {
            if (!FPURegisters[TOP].InUse) { Terminate(ErrorCode.FPUAccessViolation); return false; }

            FPURegisters[TOP].Value = Math.Sqrt(FPURegisters[TOP].Value);

            C0 = Rand.NextBool();
            C1 = Rand.NextBool();
            C2 = Rand.NextBool();
            C3 = Rand.NextBool();

            return true;
        }
        private bool ProcessFYL2X()
        {
            if (!FPURegisters[TOP].InUse || !FPURegisters[(TOP + 1) & 7].InUse) { Terminate(ErrorCode.FPUAccessViolation); return false; }

            double a = FPURegisters[TOP].Value;
            double b = FPURegisters[(TOP + 1) & 7].Value;

            ++TOP; // pop stack and place in the new st(0)
            FPURegisters[TOP].Value = b * Math.Log(a, 2);

            C0 = Rand.NextBool();
            C1 = Rand.NextBool();
            C2 = Rand.NextBool();
            C3 = Rand.NextBool();
            
            return true;
        }
        private bool ProcessFYL2XP1()
        {
            if (!FPURegisters[TOP].InUse || !FPURegisters[(TOP + 1) & 7].InUse) { Terminate(ErrorCode.FPUAccessViolation); return false; }

            double a = FPURegisters[TOP].Value;
            double b = FPURegisters[(TOP + 1) & 7].Value;

            ++TOP; // pop stack and place in the new st(0)
            FPURegisters[TOP].Value = b * Math.Log(a + 1, 2);

            C0 = Rand.NextBool();
            C1 = Rand.NextBool();
            C2 = Rand.NextBool();
            C3 = Rand.NextBool();

            return true;
        }
        private bool ProcessFXTRACT()
        {
            if (!FPURegisters[TOP].InUse) { Terminate(ErrorCode.FPUAccessViolation); return false; }

            C0 = Rand.NextBool();
            C1 = Rand.NextBool();
            C2 = Rand.NextBool();
            C3 = Rand.NextBool();

            // get value and extract exponent/significand
            double val = FPURegisters[TOP].Value;
            ExtractDouble(val, out double exp, out double sig);

            // exponent in st0, then push the significand
            FPURegisters[TOP].Value = exp;
            return PushFPU(sig);
        }
        private bool ProcessFSCALE()
        {
            if (!FPURegisters[TOP].InUse || !FPURegisters[(TOP + 1) & 7].InUse) { Terminate(ErrorCode.FPUAccessViolation); return false; }

            double a = FPURegisters[TOP].Value;
            double b = FPURegisters[(TOP + 1) & 7].Value;

            // get exponent and significand of st0
            ExtractDouble(a, out double exp, out double sig);

            // add (truncated) st1 to exponent of st0
            FPURegisters[TOP].Value = AssembleDouble(exp + (Int64)b, sig);

            C0 = Rand.NextBool();
            C1 = Rand.NextBool();
            C2 = Rand.NextBool();
            C3 = Rand.NextBool();

            return true;
        }

        private bool ProcessFXAM()
        {
            double val = FPURegisters[TOP].Value;
            UInt64 bits = DoubleAsUInt64(val);

            // C1 gets sign bit
            C1 = (bits & 0x8000000000000000) != 0;

            // empty
            if (!FPURegisters[TOP].InUse) { C3 = true; C2 = false; C0 = true; }
            // NaN
            else if (double.IsNaN(val)) { C3 = false; C2 = false; C0 = true; }
            // inf
            else if (double.IsInfinity(val)) { C3 = false; C2 = true; C0 = true; }
            // zero
            else if (val == 0) { C3 = true; C2 = false; C0 = false; }
            // denormalized
            else if ((bits & 0x7ff0000000000000) == 0) { C3 = true; C2 = true; C0 = false; }
            // normal
            else { C3 = false; C2 = true; C0 = false; }

            return true;
        }
        private bool ProcessFTST()
        {
            if (!FPURegisters[TOP].InUse) { Terminate(ErrorCode.FPUAccessViolation); return false; }

            double a = FPURegisters[TOP].Value;

            // for FTST, nan is an arithmetic error
            if (double.IsNaN(a)) { Terminate(ErrorCode.ArithmeticError); return false; }

            // do the comparison
            if (a > 0) { C3 = false; C2 = false; C0 = false; }
            else if (a < 0) { C3 = false; C2 = false; C0 = true; }
            else { C3 = true; C2 = false; C0 = false; }

            // C1 is cleared
            C1 = false;

            return true;
        }

        /*
        [1:][3: i][4: mode]
            mode = 0: cmp st(0), st(i)
            mode = 1: || + pop
            mode = 2: || + 2x pop
            mode = 3: cmp st(0), fp32
            mode = 4: || + pop
            mode = 5: cmp st(0), fp64
            mode = 6: || + pop
            mode = 7: cmp st(0), int16
            mode = 8: || + pop
            mode = 9: cmp st(0), int32
            mode = 10: || + pop
            else UND
        */
        private bool ProcessFCOM()
        {
            if (!GetMemAdv(1, out UInt64 s)) return false;

            double a, b;

            // swith through mode
            switch (s & 15)
            {
                case 0:
                case 1:
                case 2:
                    if (!FPURegisters[TOP].InUse || !FPURegisters[(TOP + s >> 4) & 7].InUse) { Terminate(ErrorCode.FPUAccessViolation); return false; }
                    a = FPURegisters[TOP].Value; b = FPURegisters[(TOP + s >> 4) & 7].Value;
                    break;

                default:
                    if (!FPURegisters[TOP].InUse) { Terminate(ErrorCode.FPUAccessViolation); return false; }
                    a = FPURegisters[TOP].Value;
                    if (!GetAddressAdv(out UInt64 m)) return false;
                    switch (s & 15)
                    {
                        case 3: case 4: if (!GetMemRaw(m, 4, out m)) return false; b = AsFloat((UInt32)m); break;
                        case 5: case 6: if (!GetMemRaw(m, 8, out m)) return false; b = AsDouble(m); break;

                        case 7: case 8: if (!GetMemRaw(m, 2, out m)) return false; b = (Int64)SignExtend(m, 1); break;
                        case 9: case 10: if (!GetMemRaw(m, 4, out m)) return false; b = (Int64)SignExtend(m, 2); break;

                        default: Terminate(ErrorCode.UndefinedBehavior); return false;
                    }
                    break;
            }

            // do the comparison
            if (a > b) { C3 = false; C2 = false; C0 = false; }
            else if (a < b) { C3 = false; C2 = false; C0 = true; }
            else if (a == b) { C3 = true; C2 = false; C0 = false; }
            // otherwise is unordered
            else
            {
                // FCOM throws in this case (FUCOM is the one that doesn't)
                Terminate(ErrorCode.ArithmeticError); return false;
            }

            // C1 is cleared
            C1 = false;

            // handle popping cases
            switch (s & 7)
            {
                case 2: return PopFPU(out a) && PopFPU(out a);
                case 1: case 4: case 6: case 8: case 10: return PopFPU(out a);
            }

            return true;
        }
        /*
        [1: pop][1: unordered][2:][1:][3: i]
            cmp st(0), st(i)
            unordered allows NaN
        */
        private bool ProcessFCOMI()
        {
            if (!GetMemAdv(1, out UInt64 s)) return false;

            if (!FPURegisters[TOP].InUse || !FPURegisters[(TOP + s) & 7].InUse) { Terminate(ErrorCode.FPUAccessViolation); return false; }

            double a = FPURegisters[TOP].Value;
            double b = FPURegisters[(TOP + s) & 7].Value;

            // do the comparison
            if (a > b) { ZF = false; PF = false; CF = false; }
            else if (a < b) { ZF = false; PF = false; CF = true; }
            else if (a == b) { ZF = true; PF = false; CF = false; }
            // otherwise is unordered
            else
            {
                // if unordered, set flags accordingly, otherwise is arithmetic error
                if ((s & 64) != 0) { ZF = true; PF = true; CF = true; }
                else { Terminate(ErrorCode.ArithmeticError); return false; }
            }

            // C1 is cleared (C0, C2, C3 not affected)
            C1 = false;

            // handle popping case
            if ((s & 128) != 0) return PopFPU(out a);
            else return true;
        }

        private bool ProcessFSIN()
        {
            if (!FPURegisters[TOP].InUse) { Terminate(ErrorCode.FPUAccessViolation); return false; }

            FPURegisters[TOP].Value = Math.Sin(FPURegisters[TOP].Value);

            C0 = Rand.NextBool();
            C1 = Rand.NextBool();
            C2 = false;
            C3 = Rand.NextBool();

            return true;
        }
        private bool ProcessFCOS()
        {
            if (!FPURegisters[TOP].InUse) { Terminate(ErrorCode.FPUAccessViolation); return false; }

            FPURegisters[TOP].Value = Math.Cos(FPURegisters[TOP].Value);

            C0 = Rand.NextBool();
            C1 = Rand.NextBool();
            C2 = false;
            C3 = Rand.NextBool();

            return true;
        }
        private bool ProcessFSINCOS()
        {
            if (!FPURegisters[TOP].InUse) { Terminate(ErrorCode.FPUAccessViolation); return false; }

            C0 = Rand.NextBool();
            C1 = Rand.NextBool();
            C2 = false;
            C3 = Rand.NextBool();

            // get the value
            double val = FPURegisters[TOP].Value;

            // st(0) <- sin, push cos
            FPURegisters[TOP].Value = Math.Sin(val);
            return PushFPU(Math.Cos(val));
        }
        private bool ProcessFPTAN()
        {
            if (!FPURegisters[TOP].InUse) { Terminate(ErrorCode.FPUAccessViolation); return false; }

            FPURegisters[TOP].Value = Math.Tan(FPURegisters[TOP].Value);

            C0 = Rand.NextBool();
            C1 = Rand.NextBool();
            C2 = false;
            C3 = Rand.NextBool();
            
            // also push 1 onto fpu stack
            return PushFPU(1);
        }
        private bool ProcessFPATAN()
        {
            if (!FPURegisters[TOP].InUse || !FPURegisters[(TOP + 1) & 7].InUse) { Terminate(ErrorCode.FPUAccessViolation); return false; }

            double a = FPURegisters[TOP].Value;
            double b = FPURegisters[(TOP + 1) & 7].Value;

            ++TOP; // pop stack and place in new st(0)
            FPURegisters[TOP].Value = Math.Atan2(b, a);

            C0 = Rand.NextBool();
            C1 = Rand.NextBool();
            C2 = false;
            C3 = Rand.NextBool();

            return true;
        }

        /*
        [7:][1: mode]
            mode = 0: inc
            mode = 1: dec
        */
        private bool ProcessFINCDECSTP()
        {
            if (!GetMemAdv(1, out UInt64 ext)) return false;

            // does not modify tag word
            switch (ext & 1)
            {
                case 0: ++TOP; break;
                case 1: --TOP; break;

                default: return true; // can't happen but compiler is stupid
            }

            C0 = Rand.NextBool();
            C1 = false;
            C2 = Rand.NextBool();
            C3 = Rand.NextBool();

            return true;
        }
        private bool ProcessFFREE()
        {
            if (!GetMemAdv(1, out UInt64 i)) return false;

            // mark as not in use
            FPURegisters[(TOP + i) & 7].InUse = false;

            C0 = Rand.NextBool();
            C1 = Rand.NextBool();
            C2 = Rand.NextBool();
            C3 = Rand.NextBool();
            
            return true;
        }

        // -- vpu stuff -- //

        /*
        [5: reg][1: aligned][2: reg_size]   [1: has_mask][1: zmask][1: scalar][1:][2: elem_size][2: mode]   ([count: mask])
            mode = 0: [3:][5: src]   reg <- src
            mode = 1: [address]      reg <- M[address]
            mode = 2: [address]      M[address] <- src
            else UND
        */
        private bool ProcessVPUMove()
        {
            // read settings bytes
            if (!GetMemAdv(1, out UInt64 s1) || !GetMemAdv(1, out UInt64 s2)) return false;
            UInt64 reg_sizecode = s1 & 3;
            UInt64 elem_sizecode = (s2 >> 2) & 3;

            // get the register to work with
            if (reg_sizecode == 3) { Terminate(ErrorCode.UndefinedBehavior); return false; }
            if (reg_sizecode != 2 && (s1 & 0x80) != 0) { Terminate(ErrorCode.UndefinedBehavior); return false; }
            int reg = (int)(s1 >> 3);

            // get number of elements to process (accounting for scalar flag)
            int elem_count = (s2 & 0x20) != 0 ? 1 : (int)(Size(reg_sizecode + 4) >> (UInt16)elem_sizecode);

            // get the mask (default of all 1's)
            UInt64 mask = UInt64.MaxValue;
            if ((s2 & 0x80) != 0 && !GetMemAdv(BitsToBytes((UInt64)elem_count), out mask)) return false;
            // get the zmask flag
            bool zmask = (s2 & 0x40) != 0;

            // switch through mode
            switch (s2 & 3)
            {
                case 0:
                    if (!GetMemAdv(1, out UInt64 _src)) return false;
                    if (reg_sizecode != 2 && (_src & 0x10) != 0) { Terminate(ErrorCode.UndefinedBehavior); return false; }
                    int src = (int)_src & 0x1f;

                    for (int i = 0; i < elem_count; ++i, mask >>= 1)
                        if ((mask & 1) != 0) ZMMRegisters[reg]._uint(elem_sizecode, i, ZMMRegisters[src]._uint(elem_sizecode, i));
                        else if (zmask) ZMMRegisters[reg]._uint(elem_sizecode, i, 0);

                    break;
                case 1:
                    if (!GetAddressAdv(out UInt64 m)) return false;
                    // if we're in vector mode and aligned flag is set, make sure address is aligned
                    if (elem_count > 1 && (s1 & 4) != 0 && m % Size(reg_sizecode + 4) != 0) { Terminate(ErrorCode.AlignmentViolation); return false; }

                    for (int i = 0; i < elem_count; ++i, mask >>= 1, m += Size(elem_sizecode))
                        if ((mask & 1) != 0)
                        {
                            if (!GetMemRaw(m, Size(elem_sizecode), out UInt64 temp)) return false;
                            ZMMRegisters[reg]._uint(elem_sizecode, i, temp);
                        }
                        else if (zmask) ZMMRegisters[reg]._uint(elem_sizecode, i, 0);

                    break;
                case 2:
                    if (!GetAddressAdv(out m)) return false;
                    // if we're in vector mode and aligned flag is set, make sure address is aligned
                    if (elem_count > 1 && (s1 & 4) != 0 && m % Size(reg_sizecode + 4) != 0) { Terminate(ErrorCode.AlignmentViolation); return false; }

                    for (int i = 0; i < elem_count; ++i, mask >>= 1, m += Size(elem_sizecode))
                        if ((mask & 1) != 0)
                        {
                            if (!SetMemRaw(m, Size(elem_sizecode), ZMMRegisters[reg]._uint(elem_sizecode, i))) return false;
                        }
                        else if(!SetMemRaw(m, Size(elem_sizecode), 0)) return false;

                    break;

                default: Terminate(ErrorCode.UndefinedBehavior); return false;
            }
            
            return true;
        }
    }
}
