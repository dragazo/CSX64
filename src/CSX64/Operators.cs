using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using static CSX64.Utility;

// -- Operators -- //

namespace CSX64
{
    public partial class Computer
    {
        // -- op tables -- //

        /// <summary>
        /// A lookup table of (even) parity for 8-bit values
        /// </summary>
        private static readonly bool[] ParityTable =
        {
            true, false, false, true, false, true, true, false, false, true, true, false, true, false, false, true, false, true, true, false, true, false, false, true, true, false, false, true, false, true, true, false,
            false, true, true, false, true, false, false, true, true, false, false, true, false, true, true, false, true, false, false, true, false, true, true, false, false, true, true, false, true, false, false, true,
            false, true, true, false, true, false, false, true, true, false, false, true, false, true, true, false, true, false, false, true, false, true, true, false, false, true, true, false, true, false, false, true,
            true, false, false, true, false, true, true, false, false, true, true, false, true, false, false, true, false, true, true, false, true, false, false, true, true, false, false, true, false, true, true, false,
            false, true, true, false, true, false, false, true, true, false, false, true, false, true, true, false, true, false, false, true, false, true, true, false, false, true, true, false, true, false, false, true,
            true, false, false, true, false, true, true, false, false, true, true, false, true, false, false, true, false, true, true, false, true, false, false, true, true, false, false, true, false, true, true, false,
            true, false, false, true, false, true, true, false, false, true, true, false, true, false, false, true, false, true, true, false, true, false, false, true, true, false, false, true, false, true, true, false,
            false, true, true, false, true, false, false, true, true, false, false, true, false, true, true, false, true, false, false, true, false, true, true, false, false, true, true, false, true, false, false, true,
        };

        // -- op utilities -- //

        /*
        [4: dest][2: size][1:dh][1: mem]   [size: imm]
            mem = 0: [1: sh][3:][4: src]
                dest <- f(reg, imm)
            mem = 1: [address]
                dest <- f(M[address], imm)
            (dh and sh mark AH, BH, CH, or DH for dest or src)
        */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool FetchBinaryOpFormat(out UInt64 s1, out UInt64 s2, out UInt64 m, out UInt64 a, out UInt64 b,
            bool get_a = true, int _a_sizecode = -1, int _b_sizecode = -1, bool allow_b_mem = true)
        {
            // read settings
            if (!GetMemAdv(1, out s1) || !GetMemAdv(1, out s2)) { s2 = m = a = b = 0; return false; }

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
                        if ((s1 & 0xc0) != 0 || a_sizecode != 0) { Terminate(ErrorCode.UndefinedBehavior); m = a = b = 0; return false; }
                        a = CPURegisters[s1 >> 4].x8h;
                    }
                    else a = CPURegisters[s1 >> 4][a_sizecode];
                    // if sh is flagged
                    if ((s1 & 1) != 0)
                    {
                        // make sure we're in registers 0-3 and 8-bit mode
                        if ((s2 & 0x0c) != 0 || b_sizecode != 0) { Terminate(ErrorCode.UndefinedBehavior); m = b = 0; return false; }
                        b = CPURegisters[s2 & 15].x8h;
                    }
                    else b = CPURegisters[s2 & 15][b_sizecode];
                    m = 0; // for compiler
                    return true;

                case 1:
                    // if dh is flagged
                    if ((s1 & 2) != 0)
                    {
                        // make sure we're in registers 0-3 and 8-bit mode
                        if ((s1 & 0xc0) != 0 || a_sizecode != 0) { Terminate(ErrorCode.UndefinedBehavior); m = a = b = 0; return false; }
                        a = CPURegisters[s1 >> 4].x8h;
                    }
                    else a = CPURegisters[s1 >> 4][a_sizecode];
                    m = 0; // for compiler
                    // get imm
                    return GetMemAdv(Size(b_sizecode), out b);

                case 2:
                    // handle allow_b_mem case
                    if (!allow_b_mem) { Terminate(ErrorCode.UndefinedBehavior); m = a = b = 0; return false; }

                    // if dh is flagged
                    if ((s1 & 2) != 0)
                    {
                        // make sure we're in registers 0-3 and 8-bit mode
                        if ((s1 & 0xc0) != 0 || a_sizecode != 0) { Terminate(ErrorCode.UndefinedBehavior); m = a = b = 0; return false; }
                        a = CPURegisters[s1 >> 4].x8h;
                    }
                    else a = CPURegisters[s1 >> 4][a_sizecode];
                    // get mem
                    if (!GetAddressAdv(out m) || !GetMemRaw(m, Size(b_sizecode), out b)) { b = 0; return false; }
                    return true;

                case 3:
                    // get mem
                    if (!GetAddressAdv(out m)) { a = b = 0; return false; }
                    if (get_a) { if (!GetMemRaw(m, Size(a_sizecode), out a)) { b = 0; return false; } }
                    else a = 0;
                    // if sh is flagged
                    if ((s1 & 1) != 0)
                    {
                        // make sure we're in registers 0-3 and 8-bit mode
                        if ((s2 & 0x0c) != 0 || b_sizecode != 0) { Terminate(ErrorCode.UndefinedBehavior); b = 0; return false; }
                        b = CPURegisters[s2 & 15].x8h;
                    }
                    else b = CPURegisters[s2 & 15][b_sizecode];
                    return true;

                case 4:
                    // get mem
                    if (!GetAddressAdv(out m)) { a = b = 0; return false; }
                    if (get_a) { if (!GetMemRaw(m, Size(a_sizecode), out a)) { b = 0; return false; } }
                    else a = 0;
                    // get imm
                    return GetMemAdv(Size(b_sizecode), out b);

                default: Terminate(ErrorCode.UndefinedBehavior); m = a = b = 0; return false;
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool StoreBinaryOpFormat(UInt64 s1, UInt64 s2, UInt64 m, UInt64 res)
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool FetchRR_RMFormat(out UInt64 s1, out UInt64 s2, out UInt64 dest, out UInt64 a, out UInt64 b)
        {
            s2 = dest = a = b = 0; // zero these so compiler won't complain

            if (!GetMemAdv(1, out s1) || !GetMemAdv(1, out s2)) return false;
            UInt64 sizecode = (s1 >> 2) & 3;

            // if dest is high
            if ((s1 & 2) != 0)
            {
                if (sizecode != 0 || (s1 & 0xc0) != 0) { Terminate(ErrorCode.UndefinedBehavior); return false; }
                dest = CPURegisters[s1 >> 4].x8h;
            }
            else dest = CPURegisters[s1 >> 4][sizecode];

            // if a is high
            if ((s2 & 128) != 0)
            {
                if (sizecode != 0 || (s2 & 0x0c) != 0) { Terminate(ErrorCode.UndefinedBehavior); return false; }
                a = CPURegisters[s2 & 15].x8h;
            }
            else a = CPURegisters[s2 & 15][sizecode];

            // if b is register
            if ((s1 & 1) == 0)
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool StoreRR_RMFormat(UInt64 s1, UInt64 res)
        {
            // if dest is high
            if ((s1 & 2) != 0) CPURegisters[s1 >> 4].x8h = (byte)res;
            else CPURegisters[s1 >> 4][(s1 >> 2) & 3] = res;

            return true;
        }

        // updates the flags for integral ops (identical for most integral ops)
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateFlagsZSP(UInt64 value, UInt64 sizecode)
        {
            ZF = value == 0;
            SF = Negative(value, sizecode);
            PF = ParityTable[value & 0xff];
        }
        // updates the flags for floating point ops
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateFlagsDouble(double value)
        {
            ZF = value == 0;
            SF = value < 0;
            OF = false;

            CF = double.IsInfinity(value);
            PF = double.IsNaN(value);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateFlagsFloat(float value)
        {
            ZF = value == 0;
            SF = value < 0;
            OF = false;

            CF = float.IsInfinity(value);
            PF = float.IsNaN(value);
        }

        // -- impl -- //

        private const UInt64 ModifiableFlags = 0x003f0fd5ul;

        /*
        [8: mode]
            mode = 0: pushf
            mode = 1: pushfd
            mode = 2: pushfq
            mode = 3: popf
            mode = 4: popfd
            mode = 5: popfq
            mode = 6: sahf
            mode = 7: lahf
        */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ProcessSTLDF()
        {
            if (!GetMemAdv(1, out UInt64 ext)) return false;

            switch (ext)
            {
                // pushf
                case 0:
                case 1: // VM and RF flags are cleared in the stored image
                case 2:
                    return PushRaw(Size(ext + 1), RFLAGS & ~0x30000ul);

                // popf
                case 3:
                case 4: // can't modify reserved flags
                case 5:
                    if (!PopRaw(Size(ext - 2), out ext)) return false;
                    RFLAGS = (RFLAGS & ~ModifiableFlags) | (ext & ModifiableFlags);
                    return true;

                // sahf
                case 6: RFLAGS = (RFLAGS & ~ModifiableFlags) | (AH & ModifiableFlags); return true;
                // lahf
                case 7: AH = (byte)RFLAGS; return true;

                default: Terminate(ErrorCode.UndefinedBehavior); return false;
            }
        }

        /*
        [8: ext]
            ext = 0: set   CF
            ext = 1: clear CF
            ext = 2: set   IF
            ext = 3: clear IF
            ext = 4: set   DF
            ext = 5: clear DF
            ext = 6: set   AC
            ext = 7: clear AC
            ext = 8: flip  CF
            elxe UND
        */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ProcessFlagManip()
        {
            if (!GetMemAdv(1, out UInt64 s)) return false;

            switch (s)
            {
                case 0: CF = true; return true;
                case 1: CF = false; return true;
                case 2: IF = true; return true;
                case 3: IF = false; return true;
                case 4: DF = true; return true;
                case 5: DF = false; return true;
                case 6: AC = true; return true;
                case 7: AC = false; return true;
                case 8: CF = !CF; return true;

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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ProcessMOV()
        {
            if (!FetchBinaryOpFormat(out UInt64 s1, out UInt64 s2, out UInt64 m, out UInt64 a, out UInt64 b, false)) return false;

            return StoreBinaryOpFormat(s1, s2, m, b);
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ProcessMOVcc()
        {
            if (!GetMemAdv(1, out UInt64 ext)) return false;
            if (!FetchBinaryOpFormat(out UInt64 s1, out UInt64 s2, out UInt64 m, out UInt64 _dest, out UInt64 src, false)) return false;

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
            if (flag) return StoreBinaryOpFormat(s1, s2, m, src);
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
            cnd = 18: CXZ/ECXZ/RCXZ
        */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
                case 18: flag = CPURegisters[2][sizecode] == 0; break;

                default: Terminate(ErrorCode.UndefinedBehavior); return false;
            }

            if (flag) RIP = val; // jump

            return true;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ProcessLEA()
        {
            if (!GetMemAdv(1, out UInt64 s) || !GetAddressAdv(out UInt64 address)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            // LEA doesn't allow 8-bit addressing
            if (sizecode == 0) { Terminate(ErrorCode.UndefinedBehavior); return false; }

            CPURegisters[s >> 4][sizecode] = address;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ProcessADD()
        {
            if (!FetchBinaryOpFormat(out UInt64 s1, out UInt64 s2, out UInt64 m, out UInt64 a, out UInt64 b)) return false;
            UInt64 sizecode = (s1 >> 2) & 3;

            UInt64 res = Truncate(a + b, sizecode);

            UpdateFlagsZSP(res, sizecode);
            CF = res < a;
            AF = (res & 0xf) < (a & 0xf); // AF is just like CF but only the low nibble
            OF = Positive(a, sizecode) == Positive(b, sizecode) && Positive(a, sizecode) != Positive(res, sizecode);

            return StoreBinaryOpFormat(s1, s2, m, res);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ProcessSUB(bool apply = true)
        {
            if (!FetchBinaryOpFormat(out UInt64 s1, out UInt64 s2, out UInt64 m, out UInt64 a, out UInt64 b)) return false;
            UInt64 sizecode = (s1 >> 2) & 3;

            UInt64 res = Truncate(a - b, sizecode);

            UpdateFlagsZSP(res, sizecode);
            CF = a < b; // if a < b, a borrow was taken from the highest bit
            AF = (a & 0xf) < (b & 0xf); // AF is just like CF but only the low nibble
            OF = Positive(a, sizecode) != Positive(b, sizecode) && Positive(a, sizecode) != Positive(res, sizecode);

            return !apply || StoreBinaryOpFormat(s1, s2, m, res);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ProcessMUL_x()
        {
            if (!GetMemAdv(1, out UInt64 ext)) return false;

            switch (ext)
            {
                case 0: return ProcessMUL();
                case 1: return ProcessMULX();

                default: Terminate(ErrorCode.UndefinedBehavior); return false;
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ProcessMULX()
        {
            if (!FetchRR_RMFormat(out UInt64 s1, out UInt64 s2, out UInt64 dest, out UInt64 a, out UInt64 b)) return false;

            UInt64 res;
            BigInteger full;

            // switch through register sizes
            switch ((s1 >> 2) & 3)
            {
                case 2:
                    res = a * b;
                    CPURegisters[s1 >> 4].x32 = (UInt32)(res >> 32); CPURegisters[s2 & 15].x32 = (UInt32)res;
                    break;
                case 3:
                    full = new BigInteger(a) * b;
                    CPURegisters[s1 >> 4].x64 = (UInt64)(full >> 64); CPURegisters[s2 & 15].x64 = (UInt64)(full & 0xffffffffffffffff);
                    break;

                default: Terminate(ErrorCode.UndefinedBehavior); return false;
            }

            return true;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
                    res = (sbyte)AL * a;
                    AX = (UInt16)res;
                    CF = OF = res != (sbyte)res;
                    break;
                case 1:
                    res = (Int16)AX * a;
                    DX = (UInt16)(res >> 16); AX = (UInt16)res;
                    CF = OF = res != (Int16)res;
                    break;
                case 2:
                    res = (Int32)EAX * a;
                    EDX = (UInt32)(res >> 32); EAX = (UInt32)res;
                    CF = OF = res != (Int32)res;
                    break;
                case 3:
                    full = new BigInteger((Int64)RAX) * a;
                    RDX = (UInt64)((full >> 64) & 0xfffffffffffffffful);
                    RAX = (UInt64)(full & 0xfffffffffffffffful);
                    CF = OF = full != (Int64)RAX;
                    break;
            }

            SF = Rand.NextBool();
            ZF = Rand.NextBool();
            AF = Rand.NextBool();
            PF = Rand.NextBool();

            return true;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ProcessBinary_IMUL()
        {
            if (!FetchBinaryOpFormat(out UInt64 s1, out UInt64 s2, out UInt64 m, out UInt64 _a, out UInt64 _b)) return false;
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

            return StoreBinaryOpFormat(s1, s2, m, (UInt64)res);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
                    full = (Int16)AX;
                    quo = full / a; rem = full % a;
                    if (quo != (sbyte)quo) { Terminate(ErrorCode.ArithmeticError); return false; }
                    AL = (byte)quo; AH = (byte)rem;
                    break;
                case 1:
                    full = ((Int32)DX << 16) | AX;
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
                    bigfull = (new BigInteger((Int64)RDX) << 64) | RAX;
                    bigquo = BigInteger.DivRem(bigfull, a, out bigrem);
                    if (bigquo > Int64.MaxValue || bigquo < Int64.MinValue) { Terminate(ErrorCode.ArithmeticError); return false; }
                    RAX = (UInt64)(Int64)bigquo; RDX = (UInt64)(Int64)bigrem;
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ProcessAND(bool apply = true)
        {
            if (!FetchBinaryOpFormat(out UInt64 s1, out UInt64 s2, out UInt64 m, out UInt64 a, out UInt64 b)) return false;
            UInt64 sizecode = (s1 >> 2) & 3;

            UInt64 res = a & b;

            UpdateFlagsZSP(res, sizecode);
            OF = CF = false;
            AF = Rand.NextBool();

            return !apply || StoreBinaryOpFormat(s1, s2, m, res);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ProcessOR()
        {
            if (!FetchBinaryOpFormat(out UInt64 s1, out UInt64 s2, out UInt64 m, out UInt64 a, out UInt64 b)) return false;
            UInt64 sizecode = (s1 >> 2) & 3;

            UInt64 res = a | b;

            UpdateFlagsZSP(res, sizecode);
            OF = CF = false;
            AF = Rand.NextBool();

            return StoreBinaryOpFormat(s1, s2, m, res);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ProcessXOR()
        {
            if (!FetchBinaryOpFormat(out UInt64 s1, out UInt64 s2, out UInt64 m, out UInt64 a, out UInt64 b)) return false;
            UInt64 sizecode = (s1 >> 2) & 3;

            UInt64 res = a ^ b;

            UpdateFlagsZSP(res, sizecode);
            OF = CF = false;
            AF = Rand.NextBool();

            return StoreBinaryOpFormat(s1, s2, m, res);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ProcessNOT()
        {
            if (!FetchUnaryOpFormat(out UInt64 s, out UInt64 m, out UInt64 a)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UInt64 res = Truncate(~a, sizecode);

            return StoreUnaryOpFormat(s, m, res);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ProcessCMPZ()
        {
            if (!FetchUnaryOpFormat(out UInt64 s, out UInt64 m, out UInt64 a)) return false;
            UInt64 sizecode = (s >> 2) & 3;

            UpdateFlagsZSP(a, sizecode);
            CF = OF = AF = false;

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ProcessBEXTR()
        {
            if (!FetchBinaryOpFormat(out UInt64 s1, out UInt64 s2, out UInt64 m, out UInt64 a, out UInt64 b, true, -1, 1)) return false;
            UInt64 sizecode = (s1 >> 2) & 3;

            ushort pos = (ushort)((b >> 8) % SizeBits(sizecode));
            ushort len = (ushort)((b & 0xff) % SizeBits(sizecode));

            UInt64 res = (a >> pos) & ((1ul << len) - 1);

            EFLAGS = 2; // clear all the (public) flags (flag 1 must always be set)
            ZF = res == 0; // ZF is set on zero
            AF = Rand.NextBool(); // AF, SF, and PF are undefined
            SF = Rand.NextBool();
            PF = Rand.NextBool();

            return StoreBinaryOpFormat(s1, s2, m, res);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ProcessANDN()
        {
            if (!FetchRR_RMFormat(out UInt64 s1, out UInt64 s2, out UInt64 dest, out UInt64 a, out UInt64 b)) return false;
            UInt64 sizecode = (s1 >> 2) & 3;

            // only supports 32 and 64-bit operands
            if (sizecode != 2 && sizecode != 3) { Terminate(ErrorCode.UndefinedBehavior); return false; }

            UInt64 res = ~a & b;

            ZF = res == 0;
            SF = Negative(res, sizecode);
            OF = false;
            CF = false;
            AF = Rand.NextBool();
            PF = Rand.NextBool();

            return StoreRR_RMFormat(s1, res);
        }

        /*
        [8: ext]   [binary]
            ext = 0: BT
            ext = 1: BTS
            ext = 2: BTR
            ext = 3: BTC
            else UND
        */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ProcessBTx()
        {
            if (!GetMemAdv(1, out UInt64 ext)) return false;

            if (!FetchBinaryOpFormat(out UInt64 s1, out UInt64 s2, out UInt64 m, out UInt64 a, out UInt64 b, true, -1, 0, false)) return false;
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
                case 1: return StoreBinaryOpFormat(s1, s2, m, a | mask);
                case 2: return StoreBinaryOpFormat(s1, s2, m, a & ~mask);
                case 3: return StoreBinaryOpFormat(s1, s2, m, a ^ mask);

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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ProcessMOVxX()
        {
            if (!GetMemAdv(1, out UInt64 s1) || !GetMemAdv(1, out UInt64 s2)) return false;

            UInt64 src; // source value to be extended

            // if source is register
            if ((s2 & 128) == 0)
            {
                switch (s1 & 15)
                {
                    case 0:
                    case 1:
                    case 2:
                    case 4:
                    case 6:
                    case 8:
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

        /*
        [8: ext]   [binary]
            ext = 0: ADC
            ext = 1: ADCX
            ext = 2: ADOX
            else UND
        */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ProcessADXX()
        {
            // get extended code - ensure it's valid
            if (!GetMemAdv(1, out UInt64 ext)) return false;

            if (!FetchBinaryOpFormat(out UInt64 s1, out UInt64 s2, out UInt64 m, out UInt64 a, out UInt64 b)) return false;
            UInt64 sizecode = (s1 >> 2) & 3;

            UInt64 res = a + b;

            switch (ext)
            {
                case 0: case 1: if (CF) ++res; break;
                case 2: if (OF) ++res; break;

                default: Terminate(ErrorCode.UndefinedBehavior); return false;
            }

            res = Truncate(res, sizecode);

            switch (ext)
            {
                case 0:
                    CF = res < a;
                    UpdateFlagsZSP(res, sizecode);
                    AF = (res & 0xf) < (a & 0xf); // AF is just like CF but only the low nibble
                    OF = Positive(a, sizecode) == Positive(b, sizecode) && Positive(a, sizecode) != Positive(res, sizecode);
                    break;
                case 1:
                    CF = res < a;
                    break;
                case 2:
                    OF = Positive(a, sizecode) == Positive(b, sizecode) && Positive(a, sizecode) != Positive(res, sizecode);
                    break;
            }

            return StoreBinaryOpFormat(s1, s2, m, res);
        }
        /*
        [8: ext]
            ext = 0: AAA
            ext = 1: AAS
            ext = 2: DAA
		    ext = 3: DAS
            else UND
        */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ProcessAAX()
        {
            byte temp_u8;
            bool temp_b;
            if (!GetMemAdv(1, out UInt64 ext)) return false;

            switch (ext)
            {
                case 0:
                    if ((AL & 0x0f) > 9 || AF)
                    {
                        AX += 0x106;
                        AF = true;
                        CF = true;
                    }
                    else
                    {
                        AF = false;
                        CF = false;
                    }
                    AL &= 0x0f;

                    OF = Rand.NextBool();
                    SF = Rand.NextBool();
                    ZF = Rand.NextBool();
                    PF = Rand.NextBool();

                    return true;

                case 1:
                    if ((AL & 0x0f) > 9 || AF)
                    {
                        AX -= 6;
                        --AH;
                        AF = true;
                        CF = true;
                    }
                    else
                    {
                        AF = false;
                        CF = false;
                    }
                    AL &= 0x0f;

                    OF = Rand.NextBool();
                    SF = Rand.NextBool();
                    ZF = Rand.NextBool();
                    PF = Rand.NextBool();

                    return true;

                case 2:
                    // Intel's reference has this instruction modify CF unnecessarily - leaving those lines in but commenting them out

                    temp_u8 = AL;
                    temp_b = CF;

                    //CF() = false;

                    if ((AL & 0x0f) > 9 || AF)
                    {
                        AL += 6;
                        //CF() = temp_b || AL() < 6; // AL() < 6 gets the carry flag we need from the above addition
                        AF = true;
                    }
                    else AF = false;

                    if (temp_u8 > 0x99 || temp_b)
                    {
                        AL += 0x60;
                        CF = true;
                    }
                    else CF = false;

                    // update flags
                    UpdateFlagsZSP(AL, 0);
                    OF = Rand.NextBool();

                    return true;

                case 3:
                    temp_u8 = AL;
                    temp_b = CF;

                    CF = false;

                    if ((AL & 0x0f) > 9 || AF)
                    {
                        CF = temp_b || AL < 6; // AL() < 6 gets the borrow flag we need from the next subtraction:
                        AL -= 6;
                        AF = true;
                    }
                    else AF = false;

                    if (temp_u8 > 0x99 || temp_b)
                    {
                        AL -= 0x60;
                        CF = true;
                    }

                    // update flags
                    UpdateFlagsZSP(AL, 0);
                    OF = Rand.NextBool();

                    return true;

                default: Terminate(ErrorCode.UndefinedBehavior); return false;
            }
        }

        // helper for MOVS - performs the actual move
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool __ProcessSTRING_MOVS(UInt64 sizecode)
        {
            UInt64 size = Size(sizecode);

            if (!GetMemRaw(RSI, size, out UInt64 temp) || !SetMemRaw(RDI, size, temp)) return false;

            if (DF) { RSI -= size; RDI -= size; }
            else { RSI += size; RDI += size; }

            return true;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool __ProcessSTRING_CMPS(UInt64 sizecode)
        {
            UInt64 size = Size(sizecode);

            if (!GetMemRaw(RSI, size, out UInt64 a) || !GetMemRaw(RDI, size, out UInt64 b)) return false;

            if (DF) { RSI -= size; RDI -= size; }
            else { RSI += size; RDI += size; }

            UInt64 res = Truncate(a - b, sizecode);

            // update flags
            UpdateFlagsZSP(res, sizecode);
            CF = a < b; // if a < b, a borrow was taken from the highest bit
            AF = (a & 0xf) < (b & 0xf); // AF is just like CF but only the low nibble
            OF = Negative(a ^ b, sizecode) && Negative(a ^ res, sizecode); // overflow if sign(a)!=sign(b) and sign(a)!=sign(res)

            return true;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool __ProcessSTRING_LODS(UInt64 sizecode)
        {
            UInt64 size = Size(sizecode);

            if (!GetMemRaw(RSI, size, out UInt64 temp)) return false;

            if (DF) RSI -= size;
            else RSI += size;

            CPURegisters[0][sizecode] = temp;

            return true;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool __ProcessSTRING_STOS(UInt64 sizecode)
        {
            UInt64 size = Size(sizecode);

            if (!SetMemRaw(RDI, size, CPURegisters[0][sizecode])) return false;

            if (DF) RDI -= size;
            else RDI += size;

            return true;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool __ProcessSTRING_SCAS(UInt64 sizecode)
        {
            UInt64 size = Size(sizecode);
            UInt64 a = CPURegisters[0][sizecode];

            if (!GetMemRaw(RDI, size, out UInt64 b)) return false;

            UInt64 res = Truncate(a - b, sizecode);

            // update flags
            UpdateFlagsZSP(res, sizecode);
            CF = a < b; // if a < b, a borrow was taken from the highest bit
            AF = (a & 0xf) < (b & 0xf); // AF is just like CF but only the low nibble
            OF = Negative(a ^ b, sizecode) && Negative(a ^ res, sizecode); // overflow if sign(a)!=sign(b) and sign(a)!=sign(res)

            if (DF) RDI -= size;
            else RDI += size;

            return true;
        }
        /*
		[6: mode][2: size]
			mode = 0:        MOVS
			mode = 1:  REP   MOVS
			mode = 2:        CMPS
			mode = 3:  REPE  CMPS
			mode = 4:  REPNE CMPS
			mode = 5:        LODS
			mode = 6:  REP   LODS
			mode = 7:        STOS
			mode = 8:  REP   STOS
			mode = 9:        SCAS
			mode = 10: REPE  SCAS
			mode = 11: REPNE SCAS
			else UND
		*/
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ProcessSTRING()
        {
            if (!GetMemAdv(1, out UInt64 s)) return false;
            UInt64 sizecode = s & 3;

            // switch through mode
            switch (s >> 2)
            {
                case 0: // MOVS
                    if (!__ProcessSTRING_MOVS(sizecode)) return false;
                    break;

                case 1: // REP MOVS

                    // if we can do the whole thing in a single tick
                    if (OTRF)
                    {
                        while (RCX != 0)
                        {
                            if (!__ProcessSTRING_MOVS(sizecode)) return false;
                            --RCX;
                        }
                    }
                    // otherwise perform a single iteration (if count is nonzero)
                    else if (RCX != 0)
                    {
                        if (!__ProcessSTRING_MOVS(sizecode)) return false;
                        --RCX;

                        // reset RIP to repeat instruction
                        RIP -= 2;
                    }
                    break;

                case 2: // CMPS
                    if (!__ProcessSTRING_CMPS(sizecode)) return false;
                    break;

                case 3: // REPE CMPS

                    if (OTRF)
                    {
                        while (RCX != 0)
                        {
                            if (!__ProcessSTRING_CMPS(sizecode)) return false;
                            --RCX;
                            if (!ZF) break;
                        }
                    }
                    else if (RCX != 0)
                    {
                        if (!__ProcessSTRING_CMPS(sizecode)) return false;
                        --RCX;
                        if (ZF) RIP -= 2; // if condition met, reset RIP to repeat instruction
                    }
                    break;

                case 4: // REPNE CMPS

                    if (OTRF)
                    {
                        while (RCX != 0)
                        {
                            if (!__ProcessSTRING_CMPS(sizecode)) return false;
                            --RCX;
                            if (ZF) break;
                        }
                    }
                    else if (RCX != 0)
                    {
                        if (!__ProcessSTRING_CMPS(sizecode)) return false;
                        --RCX;
                        if (!ZF) RIP -= 2; // if condition met, reset RIP to repeat instruction
                    }
                    break;

                case 5: // LODS
                    if (!__ProcessSTRING_LODS(sizecode)) return false;
                    break;

                case 6: // REP LODS
                    if (OTRF)
                    {
                        while (RCX != 0)
                        {
                            if (!__ProcessSTRING_LODS(sizecode)) return false;
                            --RCX;
                        }
                    }
                    else if (RCX != 0)
                    {
                        if (!__ProcessSTRING_LODS(sizecode)) return false;
                        --RCX;
                        RIP -= 2; // reset RIP to repeat instruction
                    }
                    break;

                case 7: // STOS
                    if (!__ProcessSTRING_STOS(sizecode)) return false;
                    break;

                case 8: // REP STOS

                    if (OTRF)
                    {
                        while (RCX != 0)
                        {
                            if (!__ProcessSTRING_STOS(sizecode)) return false;
                            --RCX;
                        }
                    }
                    else if (RCX != 0)
                    {
                        if (!__ProcessSTRING_STOS(sizecode)) return false;
                        --RCX;
                        RIP -= 2; // reset RIP to repeat instruction
                    }
                    break;

                case 9: // SCAS
                    if (!__ProcessSTRING_SCAS(sizecode)) return false;
                    break;

                case 10: // REPE CMPS
                         // if we can do the whole thing in a single tick
                    if (OTRF)
                    {
                        while (RCX != 0)
                        {
                            if (!__ProcessSTRING_SCAS(sizecode)) return false;
                            --RCX;
                            if (!ZF) break;
                        }
                    }
                    // otherwise perform a single iteration (if count is nonzero)
                    else if (RCX != 0)
                    {
                        if (!__ProcessSTRING_SCAS(sizecode)) return false;
                        --RCX;
                        if (ZF) RIP -= 2; // if condition met, reset RIP to repeat instruction
                    }
                    break;

                case 11: // REPNE CMPS
                         // if we can do the whole thing in a single tick
                    if (OTRF)
                    {
                        while (RCX != 0)
                        {
                            if (!__ProcessSTRING_SCAS(sizecode)) return false;
                            --RCX;
                            if (ZF) break;
                        }
                    }
                    // otherwise perform a single iteration (if count is nonzero)
                    else if (RCX != 0)
                    {
                        if (!__ProcessSTRING_SCAS(sizecode)) return false;
                        --RCX;
                        if (!ZF) RIP -= 2; // if condition met, reset RIP to repeat instruction
                    }
                    break;

                // otherwise unknown mode
                default: Terminate(ErrorCode.UndefinedBehavior); return false;
            }

            return true;
        }

        /*
        [1: forward][1: mem][2: size][4: dest]
            mem = 0: [4:][4: src]
            mem = 1: [address]
        */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool __Process_BSx_common(out UInt64 s, out UInt64 src, out UInt64 sizecode)
        {
            if (!GetMemAdv(1, out s)) { src = sizecode = 0; return false; }
            sizecode = (s >> 4) & 3;

            // if src is mem
            if ((s & 64) != 0)
            {
                if (!GetAddressAdv(out src) || !GetMemRaw(src, Size(sizecode), out src)) return false;
            }
            // otherwise src is reg
            else
            {
                if (!GetMemAdv(1, out src)) return false;
                src = CPURegisters[src & 15][sizecode];
            }

            return true;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ProcessBSx()
        {
            if (!__Process_BSx_common(out UInt64 s, out UInt64 src, out UInt64 sizecode)) return false;
            UInt64 res;

            // if src is zero
            if (src == 0)
            {
                ZF = true;
                res = Rand.NextUInt64(); // result is undefined in this case
            }
            // otherwise perform the search
            else
            {
                ZF = false;
                res = Sizecode((s & 128) != 0 ? IsolateLowBit(src) : IsolateHighBit(src));
            }

            // update dest and flags
            CPURegisters[s & 15][sizecode] = res;
            CF = Rand.NextBool();
            OF = Rand.NextBool();
            SF = Rand.NextBool();
            AF = Rand.NextBool();
            PF = Rand.NextBool();

            return true;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool ProcessTZCNT()
        {
            if (!__Process_BSx_common(out UInt64 s, out UInt64 src, out UInt64 sizecode)) return false;
            UInt64 res;

            // if src is zero
            if (src == 0)
            {
                CF = true;
                res = SizeBits(sizecode); // result is operand size (in bits) in this case
            }
            // otherwise perform the search
            else
            {
                CF = false;
                res = Sizecode(IsolateLowBit(src));
            }

            // update dest and flags
            CPURegisters[s & 15][sizecode] = res;
            ZF = res == 0;
            OF = Rand.NextBool();
            SF = Rand.NextBool();
            AF = Rand.NextBool();
            PF = Rand.NextBool();

            return true;
        }

        // identical to ProcessUNKNOWN() - added for clarity for UD instruction
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ProcessUD()
        {
            // ud explicitly triggers an unknown opcode error
            Terminate(ErrorCode.UnknownOp);
            return false;
        }

        // -- floating point stuff -- //

        /// <summary>
        /// Initializes the FPU as if by FINIT
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool FINIT()
        {
            FPU_control = 0x3bf;
            FPU_status = 0;
            FPU_tag = 0xffff;

            return true;
        }

        /// <summary>
        /// Computes the FPU tag for the specified value
        /// </summary>
        /// <param name="val">the value to test</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte ComputeFPUTag(double val)
        {
            if (double.IsNaN(val) || double.IsInfinity(val) || val.IsDenorm()) return FPU_Tag_special;
            else if (val == 0) return FPU_Tag_zero;
            else return FPU_Tag_normal;
        }

        /// <summary>
        /// Performs a round trip on the value based on the specified rounding mode (as per Intel x87)
        /// </summary>
        /// <param name="val">the value to round</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private double PerformRoundTrip(double val, UInt32 rc)
        {
            switch (FPU_RC)
            {
                case 0: return Math.Round(val, MidpointRounding.ToEven);
                case 1: return Math.Floor(val);
                case 2: return Math.Ceiling(val);
                case 3: return Math.Truncate(val);

                // because compiler is stupid
                default: throw new ArgumentException("RC out of range");
            }
        }

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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool FetchFPUBinaryFormat(out UInt64 s, out double a, out double b)
        {
            if (!GetMemAdv(1, out s)) { a = b = 0; return false; }

            // switch through mode
            switch (s & 7)
            {
                case 0:
                    if (ST_Tag(0) == FPU_Tag_empty || ST_Tag(s >> 4) == FPU_Tag_empty) { Terminate(ErrorCode.FPUAccessViolation); a = b = 0; return false; }
                    a = ST(0); b = ST(s >> 4); return true;
                case 1:
                case 2:
                    if (ST_Tag(0) == FPU_Tag_empty || ST_Tag(s >> 4) == FPU_Tag_empty) { Terminate(ErrorCode.FPUAccessViolation); a = b = 0; return false; }
                    b = ST(0); a = ST(s >> 4); return true;

                default:
                    if (ST_Tag(0) == FPU_Tag_empty) { Terminate(ErrorCode.FPUAccessViolation); a = b = 0; return false; }
                    a = ST(0); b = 0;
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool StoreFPUBinaryFormat(UInt64 s, double res)
        {
            switch (s & 7)
            {
                case 1: ST(s >> 4, res); return true;
                case 2: ST(s >> 4, res); return PopFPU(out res);

                default: ST(0, res); return true;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool PushFPU(double val)
        {
            // decrement top (wraps automatically as a 3-bit unsigned value)
            --FPU_TOP;

            // if this fpu reg is in use, it's an error
            if (ST_Tag(0) != FPU_Tag_empty) { Terminate(ErrorCode.FPUStackOverflow); return false; }

            // store the value
            ST(0, val);

            return true;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool PopFPU(out double val)
        {
            // if this register is not in use, it's an error
            if (ST_Tag(0) == FPU_Tag_empty) { Terminate(ErrorCode.FPUStackUnderflow); val = 0; return false; }

            // extract the value
            val = ST(0);
            ST_Free(0);

            // increment top (wraps automatically as a 3-bit unsigned value)
            ++FPU_TOP;

            return true;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)] private bool PopFPU() => PopFPU(out double _);

        /*
        [8: mode]   [address]
            mode = 0: FSTSW AX
            mode = 1: FSTSW
            mode = 2: FSTCW
            mode = 3: FLDCW
            mode = 4: STMXCSR
		    mode = 5: LDMXCSR
            mode = 6: FSAVE
	        mode = 7: FRSTOR
	        mode = 8: FSTENV
	        mode = 9: FLDENV
            else UND
        */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ProcessFSTLD_WORD()
        {
            UInt64 m, s, temp;
            if (!GetMemAdv(1, out s)) return false;

            // handle FSTSW AX case specially (doesn't have an address)
            if (s == 0)
            {
                AX = FPU_status;
                return true;
            }
            else if (!GetAddressAdv(out m)) return false;

            // switch through mode
            switch (s)
            {
                case 1: return SetMemRaw(m, 2, FPU_status);
                case 2: return SetMemRaw(m, 2, FPU_control);
                case 3:
                    if (!GetMemRaw(m, 2, out m)) return false;
                    FPU_control = (UInt16)m;
                    return true;

                case 4: return SetMemRaw(4, m, _MXCSR);
                case 5:
                    if (!GetMemRaw(4, m, out m)) return false;
                    _MXCSR = (_MXCSR & 0xffff0000) | (UInt16)m; // make sure user can't modify the upper 16 reserved bits
                    return true;

                case 6:
                    if (!SetMemRaw(m + 0, 2, FPU_control)) return false;
                    if (!SetMemRaw(m + 4, 2, FPU_status)) return false;
                    if (!SetMemRaw(m + 8, 2, FPU_tag)) return false;

                    if (!SetMemRaw(m + 12, 4, EIP)) return false;
                    if (!SetMemRaw(m + 16, 2, 0)) return false;

                    if (!SetMemRaw(m + 20, 4, (UInt32)m)) return false;
                    if (!SetMemRaw(m + 24, 2, 0)) return false;

                    // for cross-platformability/speed, writes native double instead of some tword hacks
                    if (!SetMemRaw(m + 28, 8, DoubleAsUInt64(ST(0)))) return false;
                    if (!SetMemRaw(m + 38, 8, DoubleAsUInt64(ST(1)))) return false;
                    if (!SetMemRaw(m + 48, 8, DoubleAsUInt64(ST(2)))) return false;
                    if (!SetMemRaw(m + 58, 8, DoubleAsUInt64(ST(3)))) return false;
                    if (!SetMemRaw(m + 68, 8, DoubleAsUInt64(ST(4)))) return false;
                    if (!SetMemRaw(m + 78, 8, DoubleAsUInt64(ST(5)))) return false;
                    if (!SetMemRaw(m + 88, 8, DoubleAsUInt64(ST(6)))) return false;
                    if (!SetMemRaw(m + 98, 8, DoubleAsUInt64(ST(7)))) return false;

                    // after storing fpu state, re-initializes
                    return FINIT();
                case 7:
                    if (!GetMemRaw(m + 0, 2, out temp)) return false; FPU_control = (UInt16)temp;
                    if (!GetMemRaw(m + 4, 2, out temp)) return false; FPU_status = (UInt16)temp;
                    if (!GetMemRaw(m + 8, 2, out temp)) return false; FPU_tag = (UInt16)temp;

                    // for cross-platformability/speed, writes native double instead of some tword hacks
                    if (!GetMemRaw(m + 28, 8, out temp)) return false; ST(0, AsDouble(temp));
                    if (!GetMemRaw(m + 38, 8, out temp)) return false; ST(1, AsDouble(temp));
                    if (!GetMemRaw(m + 48, 8, out temp)) return false; ST(2, AsDouble(temp));
                    if (!GetMemRaw(m + 58, 8, out temp)) return false; ST(3, AsDouble(temp));
                    if (!GetMemRaw(m + 68, 8, out temp)) return false; ST(4, AsDouble(temp));
                    if (!GetMemRaw(m + 78, 8, out temp)) return false; ST(5, AsDouble(temp));
                    if (!GetMemRaw(m + 88, 8, out temp)) return false; ST(6, AsDouble(temp));
                    if (!GetMemRaw(m + 98, 8, out temp)) return false; ST(7, AsDouble(temp));

                    return true;

                case 8:
                    if (!SetMemRaw(m + 0, 2, FPU_control)) return false;
                    if (!SetMemRaw(m + 4, 2, FPU_status)) return false;
                    if (!SetMemRaw(m + 8, 2, FPU_tag)) return false;

                    if (!SetMemRaw(m + 12, 4, EIP)) return false;
                    if (!SetMemRaw(m + 16, 2, 0)) return false;

                    if (!SetMemRaw(m + 20, 4, (UInt32)m)) return false;
                    if (!SetMemRaw(m + 24, 2, 0)) return false;

                    return true;

                case 9:
                    if (!GetMemRaw(m + 0, 2, out temp)) return false; FPU_control = (UInt16)temp;
                    if (!GetMemRaw(m + 4, 2, out temp)) return false; FPU_status = (UInt16)temp;
                    if (!GetMemRaw(m + 8, 2, out temp)) return false; FPU_tag = (UInt16)temp;

                    return true;

                default: Terminate(ErrorCode.UndefinedBehavior); return false;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ProcessFLD_const()
        {
            if (!GetMemAdv(1, out UInt64 ext)) return false;

            FPU_C0 = Rand.NextBool();
            FPU_C1 = Rand.NextBool();
            FPU_C2 = Rand.NextBool();
            FPU_C3 = Rand.NextBool();

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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ProcessFLD()
        {
            if (!GetMemAdv(1, out UInt64 s)) return false;

            FPU_C0 = Rand.NextBool();
            FPU_C1 = Rand.NextBool();
            FPU_C2 = Rand.NextBool();
            FPU_C3 = Rand.NextBool();

            // switch through mode
            switch (s & 7)
            {
                case 0:
                    if (ST_Tag(s >> 4) == FPU_Tag_empty) { Terminate(ErrorCode.FPUAccessViolation); return false; }
                    return PushFPU(ST(s >> 4));

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
            mode = 11: int16M <- st(0) + pop (truncation)
            mode = 12: int32M <- st(0) + pop (truncation)
            mode = 13: int64M <- st(0) + pop (truncation)
            else UND
        */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ProcessFST()
        {
            if (!GetMemAdv(1, out UInt64 s)) return false;

            FPU_C0 = Rand.NextBool();
            FPU_C1 = Rand.NextBool();
            FPU_C2 = Rand.NextBool();
            FPU_C3 = Rand.NextBool();

            switch (s & 15)
            {
                case 0:
                case 1:
                    // make sure we can read the value
                    if (ST_Tag(0) == FPU_Tag_empty) { Terminate(ErrorCode.FPUAccessViolation); return false; }
                    // record the value (is allowed to be not in use)
                    ST(s >> 4, ST(0));
                    break;

                default:
                    if (ST_Tag(0) == FPU_Tag_empty) { Terminate(ErrorCode.FPUAccessViolation); return false; }
                    if (!GetAddressAdv(out UInt64 m)) return false;
                    switch (s & 15)
                    {
                        case 2: case 3: if (!SetMemRaw(m, 4, FloatAsUInt64((float)ST(0)))) return false; break;
                        case 4: case 5: if (!SetMemRaw(m, 8, DoubleAsUInt64(ST(0)))) return false; break;
                        case 6: case 7: if (!SetMemRaw(m, 2, (UInt64)(Int64)PerformRoundTrip(ST(0), FPU_RC))) return false; break;
                        case 8: case 9: if (!SetMemRaw(m, 4, (UInt64)(Int64)PerformRoundTrip(ST(0), FPU_RC))) return false; break;
                        case 10: if (!SetMemRaw(m, 8, (UInt64)(Int64)PerformRoundTrip(ST(0), FPU_RC))) return false; break;
                        case 11: if (!SetMemRaw(m, 2, (UInt64)(Int64)ST(0))) return false; break;
                        case 12: if (!SetMemRaw(m, 4, (UInt64)(Int64)ST(0))) return false; break;
                        case 13: if (!SetMemRaw(m, 8, (UInt64)(Int64)ST(0))) return false; break;

                        default: Terminate(ErrorCode.UndefinedBehavior); return false;
                    }
                    break;
            }

            switch (s & 15)
            {
                case 0: case 2: case 4: case 6: case 8: return true;
                default: return PopFPU();
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ProcessFXCH()
        {
            if (!GetMemAdv(1, out UInt64 i)) return false;

            // make sure they're both in use
            if (ST_Tag(0) == FPU_Tag_empty || ST_Tag(i) == FPU_Tag_empty) { Terminate(ErrorCode.FPUAccessViolation); return false; }

            double temp = ST(0);
            ST(0, ST(i));
            ST(i, temp);

            FPU_C0 = Rand.NextBool();
            FPU_C1 = false;
            FPU_C2 = Rand.NextBool();
            FPU_C3 = Rand.NextBool();

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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
                if (ST_Tag(0) == FPU_Tag_empty || ST_Tag(s >> 4) == FPU_Tag_empty) { Terminate(ErrorCode.FPUAccessViolation); return false; }
                ST(0, ST(s >> 4));
            }

            FPU_C0 = Rand.NextBool();
            FPU_C1 = Rand.NextBool();
            FPU_C2 = Rand.NextBool();
            FPU_C3 = Rand.NextBool();

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ProcessFADD()
        {
            if (!FetchFPUBinaryFormat(out UInt64 s, out double a, out double b)) return false;

            double res = a + b;

            FPU_C0 = Rand.NextBool();
            FPU_C1 = Rand.NextBool();
            FPU_C2 = Rand.NextBool();
            FPU_C3 = Rand.NextBool();

            return StoreFPUBinaryFormat(s, res);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ProcessFSUB()
        {
            if (!FetchFPUBinaryFormat(out UInt64 s, out double a, out double b)) return false;

            double res = a - b;

            FPU_C0 = Rand.NextBool();
            FPU_C1 = Rand.NextBool();
            FPU_C2 = Rand.NextBool();
            FPU_C3 = Rand.NextBool();

            return StoreFPUBinaryFormat(s, res);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ProcessFSUBR()
        {
            if (!FetchFPUBinaryFormat(out UInt64 s, out double a, out double b)) return false;

            double res = b - a;

            FPU_C0 = Rand.NextBool();
            FPU_C1 = Rand.NextBool();
            FPU_C2 = Rand.NextBool();
            FPU_C3 = Rand.NextBool();

            return StoreFPUBinaryFormat(s, res);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ProcessFMUL()
        {
            if (!FetchFPUBinaryFormat(out UInt64 s, out double a, out double b)) return false;

            double res = a * b;

            FPU_C0 = Rand.NextBool();
            FPU_C1 = Rand.NextBool();
            FPU_C2 = Rand.NextBool();
            FPU_C3 = Rand.NextBool();

            return StoreFPUBinaryFormat(s, res);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ProcessFDIV()
        {
            if (!FetchFPUBinaryFormat(out UInt64 s, out double a, out double b)) return false;

            double res = a / b;

            FPU_C0 = Rand.NextBool();
            FPU_C1 = Rand.NextBool();
            FPU_C2 = Rand.NextBool();
            FPU_C3 = Rand.NextBool();

            return StoreFPUBinaryFormat(s, res);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ProcessFDIVR()
        {
            if (!FetchFPUBinaryFormat(out UInt64 s, out double a, out double b)) return false;

            double res = b / a;

            FPU_C0 = Rand.NextBool();
            FPU_C1 = Rand.NextBool();
            FPU_C2 = Rand.NextBool();
            FPU_C3 = Rand.NextBool();

            return StoreFPUBinaryFormat(s, res);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ProcessF2XM1()
        {
            if (ST_Tag(0) == FPU_Tag_empty) { Terminate(ErrorCode.FPUAccessViolation); return false; }

            // get the value
            double val = ST(0);
            // val must be in range [-1, 1]
            if (val < -1 || val > 1) { Terminate(ErrorCode.FPUError); return false; }

            ST(0, Math.Pow(2, val) - 1);

            FPU_C0 = Rand.NextBool();
            FPU_C1 = Rand.NextBool();
            FPU_C2 = Rand.NextBool();
            FPU_C3 = Rand.NextBool();

            return true;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ProcessFABS()
        {
            if (ST_Tag(0) == FPU_Tag_empty) { Terminate(ErrorCode.FPUAccessViolation); return false; }

            ST(0, Math.Abs(ST(0)));

            FPU_C0 = Rand.NextBool();
            FPU_C1 = false;
            FPU_C2 = Rand.NextBool();
            FPU_C3 = Rand.NextBool();

            return true;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ProcessFCHS()
        {
            if (ST_Tag(0) == FPU_Tag_empty) { Terminate(ErrorCode.FPUAccessViolation); return false; }

            ST(0, -ST(0));

            FPU_C0 = Rand.NextBool();
            FPU_C1 = false;
            FPU_C2 = Rand.NextBool();
            FPU_C3 = Rand.NextBool();

            return true;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ProcessFPREM()
        {
            if (ST_Tag(0) == FPU_Tag_empty || ST_Tag(1) == FPU_Tag_empty) { Terminate(ErrorCode.FPUAccessViolation); return false; }

            double a = ST(0);
            double b = ST(1);

            // compute remainder with truncated quotient
            double res = a - (Int64)(a / b) * b;

            // store value
            ST(0, res);

            // get the bits
            UInt64 bits = DoubleAsUInt64(res);

            FPU_C0 = (bits & 4) != 0;
            FPU_C1 = (bits & 1) != 0;
            FPU_C2 = false;
            FPU_C3 = (bits & 2) != 0;

            return true;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ProcessFPREM1()
        {
            if (ST_Tag(0) == FPU_Tag_empty || ST_Tag(1) == FPU_Tag_empty) { Terminate(ErrorCode.FPUAccessViolation); return false; }

            double a = ST(0);
            double b = ST(1);

            // compute remainder with rounded quotient (IEEE)
            double res = Math.IEEERemainder(a, b);

            // store value
            ST(0, res);

            // get the bits
            UInt64 bits = DoubleAsUInt64(res);

            FPU_C0 = (bits & 4) != 0;
            FPU_C1 = (bits & 1) != 0;
            FPU_C2 = false;
            FPU_C3 = (bits & 2) != 0;

            return true;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ProcessFRNDINT()
        {
            if (ST_Tag(0) == FPU_Tag_empty) { Terminate(ErrorCode.FPUAccessViolation); return false; }

            double val = ST(0);
            double res = PerformRoundTrip(val, FPU_RC);

            ST(0, res);

            FPU_C0 = Rand.NextBool();
            FPU_C1 = res > val;
            FPU_C2 = Rand.NextBool();
            FPU_C3 = Rand.NextBool();

            return true;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ProcessFSQRT()
        {
            if (ST_Tag(0) == FPU_Tag_empty) { Terminate(ErrorCode.FPUAccessViolation); return false; }

            ST(0, Math.Sqrt(ST(0)));

            FPU_C0 = Rand.NextBool();
            FPU_C1 = Rand.NextBool();
            FPU_C2 = Rand.NextBool();
            FPU_C3 = Rand.NextBool();

            return true;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ProcessFYL2X()
        {
            if (ST_Tag(0) == FPU_Tag_empty || ST_Tag(1) == FPU_Tag_empty) { Terminate(ErrorCode.FPUAccessViolation); return false; }

            double a = ST(0);
            double b = ST(1);

            PopFPU(); // pop stack and place in the new st(0)
            ST(0, b * Math.Log(a, 2));

            FPU_C0 = Rand.NextBool();
            FPU_C1 = Rand.NextBool();
            FPU_C2 = Rand.NextBool();
            FPU_C3 = Rand.NextBool();

            return true;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ProcessFYL2XP1()
        {
            if (ST_Tag(0) == FPU_Tag_empty || ST_Tag(1) == FPU_Tag_empty) { Terminate(ErrorCode.FPUAccessViolation); return false; }

            double a = ST(0);
            double b = ST(1);

            PopFPU(); // pop stack and place in the new st(0)
            ST(0, b * Math.Log(a + 1, 2));

            FPU_C0 = Rand.NextBool();
            FPU_C1 = Rand.NextBool();
            FPU_C2 = Rand.NextBool();
            FPU_C3 = Rand.NextBool();

            return true;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ProcessFXTRACT()
        {
            if (ST_Tag(0) == FPU_Tag_empty) { Terminate(ErrorCode.FPUAccessViolation); return false; }

            FPU_C0 = Rand.NextBool();
            FPU_C1 = Rand.NextBool();
            FPU_C2 = Rand.NextBool();
            FPU_C3 = Rand.NextBool();

            // get value and extract exponent/significand
            double val = ST(0);
            ExtractDouble(val, out double exp, out double sig);

            // exponent in st0, then push the significand
            ST(0, exp);
            return PushFPU(sig);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ProcessFSCALE()
        {
            if (ST_Tag(0) == FPU_Tag_empty || ST_Tag(1) == FPU_Tag_empty) { Terminate(ErrorCode.FPUAccessViolation); return false; }

            double a = ST(0);
            double b = ST(1);

            // get exponent and significand of st0
            ExtractDouble(a, out double exp, out double sig);

            // add (truncated) st1 to exponent of st0
            ST(0, AssembleDouble(exp + (Int64)b, sig));

            FPU_C0 = Rand.NextBool();
            FPU_C1 = Rand.NextBool();
            FPU_C2 = Rand.NextBool();
            FPU_C3 = Rand.NextBool();

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ProcessFXAM()
        {
            double val = ST(0);
            UInt64 bits = DoubleAsUInt64(val);

            // C1 gets sign bit
            FPU_C1 = (bits & 0x8000000000000000) != 0;

            // empty
            if (ST_Tag(0) == FPU_Tag_empty) { FPU_C3 = true; FPU_C2 = false; FPU_C0 = true; }
            // NaN
            else if (double.IsNaN(val)) { FPU_C3 = false; FPU_C2 = false; FPU_C0 = true; }
            // inf
            else if (double.IsInfinity(val)) { FPU_C3 = false; FPU_C2 = true; FPU_C0 = true; }
            // zero
            else if (val == 0) { FPU_C3 = true; FPU_C2 = false; FPU_C0 = false; }
            // denormalized
            else if (val.IsDenorm()) { FPU_C3 = true; FPU_C2 = true; FPU_C0 = false; }
            // normal
            else { FPU_C3 = false; FPU_C2 = true; FPU_C0 = false; }

            return true;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ProcessFTST()
        {
            if (ST_Tag(0) == FPU_Tag_empty) { Terminate(ErrorCode.FPUAccessViolation); return false; }

            double a = ST(0);

            // for FTST, nan is an arithmetic error
            if (double.IsNaN(a)) { Terminate(ErrorCode.ArithmeticError); return false; }

            // do the comparison
            if (a > 0) { FPU_C3 = false; FPU_C2 = false; FPU_C0 = false; }
            else if (a < 0) { FPU_C3 = false; FPU_C2 = false; FPU_C0 = true; }
            else { FPU_C3 = true; FPU_C2 = false; FPU_C0 = false; }

            // C1 is cleared
            FPU_C1 = false;

            return true;
        }

        /*
        [1: unordered][3: i][4: mode]
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
            mode = 11 cmp st(0), st(i) eflags
            mode = 12 || + pop
            else UND
        */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
                case 11:
                case 12:
                    if (ST_Tag(0) == FPU_Tag_empty || ST_Tag(s >> 4) == FPU_Tag_empty) { Terminate(ErrorCode.FPUAccessViolation); return false; }
                    a = ST(0); b = ST(s >> 4);
                    break;

                default:
                    if (ST_Tag(0) == FPU_Tag_empty) { Terminate(ErrorCode.FPUAccessViolation); return false; }
                    a = ST(0);
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

            bool x, y, z; // temporaries for cmp flag data

            // do the comparison
            if (a > b) { x = false; y = false; z = false; }
            else if (a < b) { x = false; y = false; z = true; }
            else if (a == b) { x = true; y = false; z = false; }
            // otherwise is unordered
            else
            {
                if ((s & 128) == 0) { Terminate(ErrorCode.ArithmeticError); return false; }
                x = y = z = true;
            }

            // eflags
            if (s == 11 || s == 12)
            {
                ZF = x;
                PF = y;
                CF = z;
            }
            // fflags
            else
            {
                FPU_C3 = x;
                FPU_C2 = y;
                FPU_C0 = z;
            }
            FPU_C1 = false; // C1 is cleared in either case

            // handle popping cases
            switch (s & 7)
            {
                case 2: return PopFPU() && PopFPU();
                case 1: case 4: case 6: case 8: case 10: case 12: return PopFPU();
                default: return true;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ProcessFSIN()
        {
            if (ST_Tag(0) == FPU_Tag_empty) { Terminate(ErrorCode.FPUAccessViolation); return false; }

            ST(0, Math.Sin(ST(0)));

            FPU_C0 = Rand.NextBool();
            FPU_C1 = Rand.NextBool();
            FPU_C2 = false;
            FPU_C3 = Rand.NextBool();

            return true;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ProcessFCOS()
        {
            if (ST_Tag(0) == FPU_Tag_empty) { Terminate(ErrorCode.FPUAccessViolation); return false; }

            ST(0, Math.Cos(ST(0)));

            FPU_C0 = Rand.NextBool();
            FPU_C1 = Rand.NextBool();
            FPU_C2 = false;
            FPU_C3 = Rand.NextBool();

            return true;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ProcessFSINCOS()
        {
            if (ST_Tag(0) == FPU_Tag_empty) { Terminate(ErrorCode.FPUAccessViolation); return false; }

            FPU_C0 = Rand.NextBool();
            FPU_C1 = Rand.NextBool();
            FPU_C2 = false;
            FPU_C3 = Rand.NextBool();

            // get the value
            double val = ST(0);

            // st(0) <- sin, push cos
            ST(0, Math.Sin(val));
            return PushFPU(Math.Cos(val));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ProcessFPTAN()
        {
            if (ST_Tag(0) == FPU_Tag_empty) { Terminate(ErrorCode.FPUAccessViolation); return false; }

            ST(0, Math.Tan(ST(0)));

            FPU_C0 = Rand.NextBool();
            FPU_C1 = Rand.NextBool();
            FPU_C2 = false;
            FPU_C3 = Rand.NextBool();

            // also push 1 onto fpu stack
            return PushFPU(1);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ProcessFPATAN()
        {
            if (ST_Tag(0) == FPU_Tag_empty || ST_Tag(1) == FPU_Tag_empty) { Terminate(ErrorCode.FPUAccessViolation); return false; }

            double a = ST(0);
            double b = ST(1);

            PopFPU(); // pop stack and place in new st(0)
            ST(0, Math.Atan2(b, a));

            FPU_C0 = Rand.NextBool();
            FPU_C1 = Rand.NextBool();
            FPU_C2 = false;
            FPU_C3 = Rand.NextBool();

            return true;
        }

        /*
        [7:][1: mode]
            mode = 0: inc
            mode = 1: dec
        */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ProcessFINCDECSTP()
        {
            if (!GetMemAdv(1, out UInt64 ext)) return false;

            // does not modify tag word
            switch (ext & 1)
            {
                case 0: ++FPU_TOP; break;
                case 1: --FPU_TOP; break;

                default: return true; // can't happen but compiler is stupid
            }

            FPU_C0 = Rand.NextBool();
            FPU_C1 = false;
            FPU_C2 = Rand.NextBool();
            FPU_C3 = Rand.NextBool();

            return true;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ProcessFFREE()
        {
            if (!GetMemAdv(1, out UInt64 i)) return false;

            // mark as not in use
            ST_Free(i);

            FPU_C0 = Rand.NextBool();
            FPU_C1 = Rand.NextBool();
            FPU_C2 = Rand.NextBool();
            FPU_C3 = Rand.NextBool();

            return true;
        }

        // -- vpu stuff -- //

        // types used for simd computation handlers
        private delegate bool VPUBinaryDelegate(UInt64 elem_sizecode, out UInt64 res, UInt64 a, UInt64 b, int index);
        private delegate bool VPUUnaryDelegate(UInt64 elem_sizecode, out UInt64 res, UInt64 a, int index);
        private delegate bool VPUCVTDelegate(out UInt64 res, UInt64 a);

        /*
        [5: reg][1: aligned][2: reg_size]   [1: has_mask][1: zmask][1: scalar][1:][2: elem_size][2: mode]   ([count: mask])
            mode = 0: [3:][5: src]   reg <- src
            mode = 1: [address]      reg <- M[address]
            mode = 2: [address]      M[address] <- src
            else UND
        */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
                        else if (zmask && !SetMemRaw(m, Size(elem_sizecode), 0)) return false;

                    break;

                default: Terminate(ErrorCode.UndefinedBehavior); return false;
            }

            return true;
        }
        /*
        [5: dest][1: aligned][2: dest_size]   [1: has_mask][1: zmask][1: scalar][1:][2: elem_size][1:][1: mem]   ([count: mask])   [3:][5: src1]
            mem = 0: [3:][5: src2]   dest <- f(src1, src2)
            mem = 1: [address]       dest <- f(src1, M[address])
        */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ProcessVPUBinary(UInt64 elem_size_mask, VPUBinaryDelegate func)
        {
            // read settings bytes
            if (!GetMemAdv(1, out UInt64 s1) || !GetMemAdv(1, out UInt64 s2)) return false;
            UInt64 dest_sizecode = s1 & 3;
            UInt64 elem_sizecode = (s2 >> 2) & 3;

            // make sure this element size is allowed
            if ((Size(elem_sizecode) & elem_size_mask) == 0) { Terminate(ErrorCode.UndefinedBehavior); return false; }

            // get the register to work with
            if (dest_sizecode == 3) { Terminate(ErrorCode.UndefinedBehavior); return false; }
            if (dest_sizecode != 2 && (s1 & 0x80) != 0) { Terminate(ErrorCode.UndefinedBehavior); return false; }
            int dest = (int)(s1 >> 3);

            // get number of elements to process (accounting for scalar flag)
            int elem_count = (s2 & 0x20) != 0 ? 1 : (int)(Size(dest_sizecode + 4) >> (UInt16)elem_sizecode);

            // get the mask (default of all 1's)
            UInt64 mask = UInt64.MaxValue;
            if ((s2 & 0x80) != 0 && !GetMemAdv(BitsToBytes((UInt64)elem_count), out mask)) return false;
            // get the zmask flag
            bool zmask = (s2 & 0x40) != 0;

            // get src1
            if (!GetMemAdv(1, out UInt64 _src1)) return false;
            if (dest_sizecode != 2 && (_src1 & 0x10) != 0) { Terminate(ErrorCode.UndefinedBehavior); return false; }
            int src1 = (int)(_src1 & 0x1f);

            // if src2 is a register
            if ((s2 & 1) == 0)
            {
                if (!GetMemAdv(1, out UInt64 _src2)) return false;
                if (dest_sizecode != 2 && (_src2 & 0x10) != 0) { Terminate(ErrorCode.UndefinedBehavior); return false; }
                int src2 = (int)(_src2 & 0x1f);

                for (int i = 0; i < elem_count; ++i, mask >>= 1)
                    if ((mask & 1) != 0)
                    {
                        // hand over to the delegate for processing
                        if (!func(elem_sizecode, out UInt64 res, ZMMRegisters[src1]._uint(elem_sizecode, i), ZMMRegisters[src2]._uint(elem_sizecode, i), i)) return false;
                        ZMMRegisters[dest]._uint(elem_sizecode, i, res);
                    }
                    else if (zmask) ZMMRegisters[dest]._uint(elem_sizecode, i, 0);
            }
            // otherwise src is memory
            else
            {
                if (!GetAddressAdv(out UInt64 m)) return false;
                // if we're in vector mode and aligned flag is set, make sure address is aligned
                if (elem_count > 1 && (s1 & 4) != 0 && m % Size(dest_sizecode + 4) != 0) { Terminate(ErrorCode.AlignmentViolation); return false; }

                for (int i = 0; i < elem_count; ++i, mask >>= 1, m += Size(elem_sizecode))
                    if ((mask & 1) != 0)
                    {
                        if (!GetMemRaw(m, Size(elem_sizecode), out UInt64 res)) return false;

                        // hand over to the delegate for processing
                        if (!func(elem_sizecode, out res, ZMMRegisters[src1]._uint(elem_sizecode, i), res, i)) return false;
                        ZMMRegisters[dest]._uint(elem_sizecode, i, res);
                    }
                    else if (zmask) ZMMRegisters[dest]._uint(elem_sizecode, i, 0);
            }

            return true;
        }
        /*
		[5: dest][1: aligned][2: dest_size]   [1: has_mask][1: zmask][1: scalar][1:][2: elem_size][1:][1: mem]   ([count: mask])
		mem = 0: [3:][5: src]   dest <- f(src)
		mem = 1: [address]      dest <- f(M[address])
		*/
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool ProcessVPUUnary(UInt64 elem_size_mask, VPUUnaryDelegate func)
        {
            // read settings bytes
            if (!GetMemAdv(1, out UInt64 s1) || !GetMemAdv(1, out UInt64 s2)) return false;
            UInt64 dest_sizecode = s1 & 3;
            UInt64 elem_sizecode = (s2 >> 2) & 3;

            // make sure this element size is allowed
            if ((Size(elem_sizecode) & elem_size_mask) == 0) { Terminate(ErrorCode.UndefinedBehavior); return false; }
            // get the register to work with
            if (dest_sizecode == 3) { Terminate(ErrorCode.UndefinedBehavior); return false; }
            if (dest_sizecode != 2 && (s1 & 0x80) != 0) { Terminate(ErrorCode.UndefinedBehavior); return false; }
            int dest = (int)(s1 >> 3);
            // get number of elements to process (accounting for scalar flag)
            int elem_count = (s2 & 0x20) != 0 ? 1 : (int)(Size(dest_sizecode + 4) >> (UInt16)elem_sizecode);

            // get the mask (default of all 1's)
            UInt64 mask = ~(UInt64)0;
            if ((s2 & 0x80) != 0 && !GetMemAdv(BitsToBytes((UInt64)elem_count), out mask)) return false;
            // get the zmask flag
            bool zmask = (s2 & 0x40) != 0;

            // if src is a register
            if ((s2 & 1) == 0)
            {
                if (!GetMemAdv(1, out UInt64 _src)) return false;
                if (dest_sizecode != 2 && (_src & 0x10) != 0) { Terminate(ErrorCode.UndefinedBehavior); return false; }
                int src = (int)(_src & 0x1f);

                for (int i = 0; i < elem_count; ++i, mask >>= 1)
                    if ((mask & 1) != 0)
                    {
                        // hand over to the delegate for processing
                        if (!func(elem_sizecode, out UInt64 res, ZMMRegisters[src]._uint(elem_sizecode, i), i)) return false;
                        ZMMRegisters[dest]._uint(elem_sizecode, i, res);
                    }
                    else if (zmask) ZMMRegisters[dest]._uint(elem_sizecode, i, 0);
            }
            // otherwise src is memory
            else
            {
                if (!GetAddressAdv(out UInt64 m)) return false;
                // if we're in vector mode and aligned flag is set, make sure address is aligned
                if (elem_count > 1 && (s1 & 4) != 0 && m % Size(dest_sizecode + 4) != 0) { Terminate(ErrorCode.AlignmentViolation); return false; }
                for (int i = 0; i < elem_count; ++i, mask >>= 1, m += Size(elem_sizecode))
                    if ((mask & 1) != 0)
                    {
                        if (!GetMemRaw(m, Size(elem_sizecode), out UInt64 res)) return false;

                        // hand over to the delegate for processing
                        if (!func(elem_sizecode, out res, res, i)) return false;
                        ZMMRegisters[dest]._uint(elem_sizecode, i, res);
                    }
                    else if (zmask) ZMMRegisters[dest]._uint(elem_sizecode, i, 0);
            }
            return true;
        }

        /*
		[5: dest][1: mem][1: has_mask][1: zmask]   ([count: mask])
		mem = 0: [3:][5: src]   dest <- f(src)
		mem = 1: [address]      dest <- f(M[address])
		*/
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool ProcessVPUCVT_packed(UInt64 elem_count, UInt64 to_elem_sizecode, UInt64 from_elem_sizecode, VPUCVTDelegate func)
        {
            // read settings byte
            if (!GetMemAdv(1, out UInt64 s)) return false;
            UInt64 dest = s >> 3;

            // get the mask (default of all 1's)
            UInt64 mask = 0xffffffffffffffff;
            if ((s & 2) != 0 && !GetMemAdv(BitsToBytes(elem_count), out mask)) return false;
            // get the zmask flag
            bool zmask = (s & 1) != 0;

            // because we may be changing sizes, and writing to the source register, we need to do our work in a temporary buffer
            ZMMRegister temp_dest;
            temp_dest.Clear();

            // if src is a register
            if ((s & 4) == 0)
            {
                UInt64 src, res;
                if (!GetMemAdv(1, out src)) return false;
                src &= 0x1f;

                for (UInt64 i = 0; i < elem_count; ++i, mask >>= 1)
                {
                    if ((mask & 1) != 0)
                    {
                        // hand over to the delegate for processing
                        if (!func(out res, ZMMRegisters[src]._uint(from_elem_sizecode, (int)i))) return false;
                        temp_dest._uint(to_elem_sizecode, (int)i, res);
                    }
                    else if (zmask) temp_dest._uint(to_elem_sizecode, (int)i, 0);
                }
            }
            // otherwise src is memory
            else
            {
                UInt64 m, res;
                if (!GetAddressAdv(out m)) return false;
                // make sure address is aligned
                if (m % (elem_count << (ushort)from_elem_sizecode) != 0) { Terminate(ErrorCode.AlignmentViolation); return false; }

                for (UInt64 i = 0; i < elem_count; ++i, mask >>= 1, m += Size(from_elem_sizecode))
                {
                    if ((mask & 1) != 0)
                    {
                        if (!GetMemRaw(m, Size(from_elem_sizecode), out res)) return false;

                        // hand over to the delegate for processing
                        if (!func(out res, res)) return false;
                        temp_dest._uint(to_elem_sizecode, (int)i, res);
                    }
                    else if (zmask) temp_dest._uint(to_elem_sizecode, (int)i, 0);
                }
            }

            // store resulting temporary back to the correct register
            ZMMRegisters[dest] = temp_dest;

            return true;
        }

        /*
		[4: dest][4: src]
		*/
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool ProcessVPUCVT_scalar_xmm_xmm(UInt64 to_elem_sizecode, UInt64 from_elem_sizecode, VPUCVTDelegate func)
        {
            // read the settings byte
            UInt64 s, temp;
            if (!GetMemAdv(1, out s)) return false;

            // perform the conversion
            if (!func(out temp, ZMMRegisters[s & 15]._uint(from_elem_sizecode, 0))) return false;

            // store the result
            ZMMRegisters[s >> 4]._uint(to_elem_sizecode, 0, temp);

            return true;
        }
        /*
		[4: dest][4: src]
		*/
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool ProcessVPUCVT_scalar_xmm_reg(UInt64 to_elem_sizecode, UInt64 from_elem_sizecode, VPUCVTDelegate func)
        {
            // read the settings byte
            UInt64 s, temp;
            if (!GetMemAdv(1, out s)) return false;

            // perform the conversion
            if (!func(out temp, CPURegisters[s & 15][from_elem_sizecode])) return false;

            // store the result
            ZMMRegisters[s >> 4]._uint(to_elem_sizecode, 0, temp);

            return true;
        }
        /*
		[4: dest][4:]   [address]
		*/
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool ProcessVPUCVT_scalar_xmm_mem(UInt64 to_elem_sizecode, UInt64 from_elem_sizecode, VPUCVTDelegate func)
        {
            // read the settings byte
            UInt64 s, temp;
            if (!GetMemAdv(1, out s)) return false;

            // get value to convert in temp
            if (!GetAddressAdv(out temp) || !GetMemRaw(temp, Size(from_elem_sizecode), out temp)) return false;

            // perform the conversion
            if (!func(out temp, temp)) return false;

            // store the result
            ZMMRegisters[s >> 4]._uint(to_elem_sizecode, 0, temp);

            return true;
        }
        /*
		[4: dest][4: src]
		*/
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool ProcessVPUCVT_scalar_reg_xmm(UInt64 to_elem_sizecode, UInt64 from_elem_sizecode, VPUCVTDelegate func)
        {
            // read the settings byte
            UInt64 s, temp;
            if (!GetMemAdv(1, out s)) return false;

            // perform the conversion
            if (!func(out temp, ZMMRegisters[s & 15]._uint(from_elem_sizecode, 0))) return false;

            // store the result
            CPURegisters[s >> 4][to_elem_sizecode] = temp;

            return true;
        }
        /*
		[4: dest][4:]   [address]
		*/
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool ProcessVPUCVT_scalar_reg_mem(UInt64 to_elem_sizecode, UInt64 from_elem_sizecode, VPUCVTDelegate func)
        {
            // read the settings byte
            UInt64 s, temp;
            if (!GetMemAdv(1, out s)) return false;

            // get value to convert in temp
            if (!GetAddressAdv(out temp) || !GetMemRaw(temp, Size(from_elem_sizecode), out temp)) return false;

            // perform the conversion
            if (!func(out temp, temp)) return false;

            // store the result
            CPURegisters[s >> 4][to_elem_sizecode] = temp;

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool __TryPerformVEC_FADD(UInt64 elem_sizecode, out UInt64 res, UInt64 a, UInt64 b, int index)
        {
            // 64-bit fp
            if (elem_sizecode == 3) res = DoubleAsUInt64(AsDouble(a) + AsDouble(b));
            // 32-bit fp
            else res = FloatAsUInt64(AsFloat((UInt32)a) + AsFloat((UInt32)b));

            return true;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool __TryPerformVEC_FSUB(UInt64 elem_sizecode, out UInt64 res, UInt64 a, UInt64 b, int index)
        {
            // 64-bit fp
            if (elem_sizecode == 3) res = DoubleAsUInt64(AsDouble(a) - AsDouble(b));
            // 32-bit fp
            else res = FloatAsUInt64(AsFloat((UInt32)a) - AsFloat((UInt32)b));

            return true;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool __TryPerformVEC_FMUL(UInt64 elem_sizecode, out UInt64 res, UInt64 a, UInt64 b, int index)
        {
            // 64-bit fp
            if (elem_sizecode == 3) res = DoubleAsUInt64(AsDouble(a) * AsDouble(b));
            // 32-bit fp
            else res = FloatAsUInt64(AsFloat((UInt32)a) * AsFloat((UInt32)b));

            return true;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool __TryPerformVEC_FDIV(UInt64 elem_sizecode, out UInt64 res, UInt64 a, UInt64 b, int index)
        {
            // 64-bit fp
            if (elem_sizecode == 3) res = DoubleAsUInt64(AsDouble(a) / AsDouble(b));
            // 32-bit fp
            else res = FloatAsUInt64(AsFloat((UInt32)a) / AsFloat((UInt32)b));

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)] private bool TryProcessVEC_FADD() => ProcessVPUBinary(12, __TryPerformVEC_FADD);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] private bool TryProcessVEC_FSUB() => ProcessVPUBinary(12, __TryPerformVEC_FSUB);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] private bool TryProcessVEC_FMUL() => ProcessVPUBinary(12, __TryPerformVEC_FMUL);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] private bool TryProcessVEC_FDIV() => ProcessVPUBinary(12, __TryPerformVEC_FDIV);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool __TryPerformVEC_AND(UInt64 elem_sizecode, out UInt64 res, UInt64 a, UInt64 b, int index)
        {
            res = a & b;
            return true;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool __TryPerformVEC_OR(UInt64 elem_sizecode, out UInt64 res, UInt64 a, UInt64 b, int index)
        {
            res = a | b;
            return true;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool __TryPerformVEC_XOR(UInt64 elem_sizecode, out UInt64 res, UInt64 a, UInt64 b, int index)
        {
            res = a ^ b;
            return true;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool __TryPerformVEC_ANDN(UInt64 elem_sizecode, out UInt64 res, UInt64 a, UInt64 b, int index)
        {
            res = ~a & b;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)] private bool TryProcessVEC_AND() => ProcessVPUBinary(15, __TryPerformVEC_AND);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] private bool TryProcessVEC_OR() => ProcessVPUBinary(15, __TryPerformVEC_OR);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] private bool TryProcessVEC_XOR() => ProcessVPUBinary(15, __TryPerformVEC_XOR);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] private bool TryProcessVEC_ANDN() => ProcessVPUBinary(15, __TryPerformVEC_ANDN);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool __TryPerformVEC_ADD(UInt64 elem_sizecode, out UInt64 res, UInt64 a, UInt64 b, int index)
        {
            res = a + b;
            return true;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool __TryPerformVEC_ADDS(UInt64 elem_sizecode, out UInt64 res, UInt64 a, UInt64 b, int index)
        {
            // get sign mask
            UInt64 smask = SignMask(elem_sizecode);

            res = a + b;

            // get sign bits
            bool res_sign = (res & smask) != 0;
            bool a_sign = (a & smask) != 0;
            bool b_sign = (b & smask) != 0;

            // if there was an over/underflow, handle saturation cases
            if (a_sign == b_sign && a_sign != res_sign) res = a_sign ? smask : smask - 1;

            return true;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool __TryPerformVEC_ADDUS(UInt64 elem_sizecode, out UInt64 res, UInt64 a, UInt64 b, int index)
        {
            // get trunc mask
            UInt64 tmask = TruncMask(elem_sizecode);

            res = (a + b) & tmask; // truncated for logic below

            // if there was an over/underflow, handle saturation cases
            if (res < a) res = tmask;

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)] private bool TryProcessVEC_ADD() => ProcessVPUBinary(15, __TryPerformVEC_ADD);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] private bool TryProcessVEC_ADDS() => ProcessVPUBinary(15, __TryPerformVEC_ADDS);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] private bool TryProcessVEC_ADDUS() => ProcessVPUBinary(15, __TryPerformVEC_ADDUS);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool __TryPerformVEC_SUB(UInt64 elem_sizecode, out UInt64 res, UInt64 a, UInt64 b, int index)
        {
            res = a - b;
            return true;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool __TryPerformVEC_SUBS(UInt64 elem_sizecode, out UInt64 res, UInt64 a, UInt64 b, int index)
        {
            // since this one's signed, we can just add the negative
            return __TryPerformVEC_ADDS(elem_sizecode, out res, a, Truncate(~b + 1, elem_sizecode), index);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool __TryPerformVEC_SUBUS(UInt64 elem_sizecode, out UInt64 res, UInt64 a, UInt64 b, int index)
        {
            // handle unsigned sub saturation
            res = a > b ? a - b : 0;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)] private bool TryProcessVEC_SUB() => ProcessVPUBinary(15, __TryPerformVEC_SUB);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] private bool TryProcessVEC_SUBS() => ProcessVPUBinary(15, __TryPerformVEC_SUBS);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] private bool TryProcessVEC_SUBUS() => ProcessVPUBinary(15, __TryPerformVEC_SUBUS);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool __TryPerformVEC_MULL(UInt64 elem_sizecode, out UInt64 res, UInt64 a, UInt64 b, int index)
        {
            res = (UInt64)((Int64)SignExtend(a, elem_sizecode) * (Int64)SignExtend(b, elem_sizecode));
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)] private bool TryProcessVEC_MULL() => ProcessVPUBinary(15, __TryPerformVEC_MULL);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool __TryProcessVEC_FMIN(UInt64 elem_sizecode, out UInt64 res, UInt64 a, UInt64 b, int index)
        {
            // this exploits c# returning false on comparison to NaN. see http://www.felixcloutier.com/x86/MINPD.html for the actual algorithm
            if (elem_sizecode == 3) res = AsDouble(a) < AsDouble(b) ? a : b;
            else res = AsFloat((UInt32)a) < AsFloat((UInt32)b) ? a : b;

            return true;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool __TryProcessVEC_FMAX(UInt64 elem_sizecode, out UInt64 res, UInt64 a, UInt64 b, int index)
        {
            // this exploits c# returning false on comparison to NaN. see http://www.felixcloutier.com/x86/MAXPD.html for the actual algorithm
            if (elem_sizecode == 3) res = AsDouble(a) > AsDouble(b) ? a : b;
            else res = AsFloat((UInt32)a) > AsFloat((UInt32)b) ? a : b;

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)] private bool TryProcessVEC_FMIN() => ProcessVPUBinary(12, __TryProcessVEC_FMIN);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] private bool TryProcessVEC_FMAX() => ProcessVPUBinary(12, __TryProcessVEC_FMAX);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool __TryProcessVEC_UMIN(UInt64 elem_sizecode, out UInt64 res, UInt64 a, UInt64 b, int index)
        {
            res = a < b ? a : b; // a and b are guaranteed to be properly truncated, so this is invariant of size
            return true;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool __TryProcessVEC_SMIN(UInt64 elem_sizecode, out UInt64 res, UInt64 a, UInt64 b, int index)
        {
            // just extend to 64-bit and do a signed compare
            res = (Int64)SignExtend(a, elem_sizecode) < (Int64)SignExtend(b, elem_sizecode) ? a : b;
            return true;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool __TryProcessVEC_UMAX(UInt64 elem_sizecode, out UInt64 res, UInt64 a, UInt64 b, int index)
        {
            res = a > b ? a : b; // a and b are guaranteed to be properly truncated, so this is invariant of size
            return true;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool __TryProcessVEC_SMAX(UInt64 elem_sizecode, out UInt64 res, UInt64 a, UInt64 b, int index)
        {
            // just extend to 64-bit and do a signed compare
            res = (Int64)SignExtend(a, elem_sizecode) > (Int64)SignExtend(b, elem_sizecode) ? a : b;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)] private bool TryProcessVEC_UMIN() => ProcessVPUBinary(15, __TryProcessVEC_UMIN);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] private bool TryProcessVEC_SMIN() => ProcessVPUBinary(15, __TryProcessVEC_SMIN);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] private bool TryProcessVEC_UMAX() => ProcessVPUBinary(15, __TryProcessVEC_UMAX);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] private bool TryProcessVEC_SMAX() => ProcessVPUBinary(15, __TryProcessVEC_SMAX);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool __TryPerformVEC_FADDSUB(UInt64 elem_sizecode, out UInt64 res, UInt64 a, UInt64 b, int index)
        {
            // 64-bit fp
            if (elem_sizecode == 3) res = DoubleAsUInt64(index % 2 == 0 ? AsDouble(a) - AsDouble(b) : AsDouble(a) + AsDouble(b));
            // 32-bit fp
            else res = FloatAsUInt64(index % 2 == 0 ? AsFloat((UInt32)a) - AsFloat((UInt32)b) : AsFloat((UInt32)a) + AsFloat((UInt32)b));

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)] private bool TryProcessVEC_FADDSUB() => ProcessVPUBinary(12, __TryPerformVEC_FADDSUB);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool __TryPerformVEC_AVG(UInt64 elem_sizecode, out UInt64 res, UInt64 a, UInt64 b, int index)
        {
            res = (a + b + 1) >> 1; // doesn't work for 64-bit, but Intel doesn't offer a 64-bit variant anyway, so that's fine
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)] private bool TryProcessVEC_AVG() => ProcessVPUBinary(3, __TryPerformVEC_AVG);

        // constants used to represent the result of a "true" simd floatint-point comparison
        private const UInt64 __fp64_simd_cmp_true = 0xffffffffffffffff;
        private const UInt64 __fp32_simd_cmp_true = 0xffffffff;

        // helper for FCMP comparators
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool __TryProcesVEC_FCMP_helper(UInt64 elem_sizecode, out UInt64 res, UInt64 a, UInt64 b, int index,
            bool great, bool less, bool equal, bool unord, bool signal)
        {
            if (elem_sizecode == 3)
            {
                double fa = AsDouble(a), fb = AsDouble(b);
                bool cmp;
                if (double.IsNaN(fa) || double.IsNaN(fb))
                {
                    cmp = unord;
                    if (signal) { Terminate(ErrorCode.ArithmeticError); res = 0; return false; }
                }
                else if (fa > fb) cmp = great;
                else if (fa < fb) cmp = less;
                else if (fa == fb) cmp = equal;
                else cmp = false; /* if something weird happens, catch as false */
                res = cmp ? __fp64_simd_cmp_true : 0;
            }
            else
            {
                float fa = AsFloat((UInt32)a), fb = AsFloat((UInt32)b);
                bool cmp;
                if (float.IsNaN(fa) || float.IsNaN(fb))
                {
                    cmp = unord;
                    if (signal) { Terminate(ErrorCode.ArithmeticError); res = 0; return false; }
                }
                else if (fa > fb) cmp = great;
                else if (fa < fb) cmp = less;
                else if (fa == fb) cmp = equal;
                else cmp = false; /* if something weird happens, catch as false */
                res = cmp ? __fp32_simd_cmp_true : 0;
            }
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)] private bool __TryProcesVEC_FCMP_EQ_OQ(UInt64 elem_sizecode, out UInt64 res, UInt64 a, UInt64 b, int index) => __TryProcesVEC_FCMP_helper(elem_sizecode, out res, a, b, index, false, false, true, false, false);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] private bool __TryProcesVEC_FCMP_LT_OS(UInt64 elem_sizecode, out UInt64 res, UInt64 a, UInt64 b, int index) => __TryProcesVEC_FCMP_helper(elem_sizecode, out res, a, b, index, false, true, false, false, true);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] private bool __TryProcesVEC_FCMP_LE_OS(UInt64 elem_sizecode, out UInt64 res, UInt64 a, UInt64 b, int index) => __TryProcesVEC_FCMP_helper(elem_sizecode, out res, a, b, index, false, true, true, false, true);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] private bool __TryProcesVEC_FCMP_UNORD_Q(UInt64 elem_sizecode, out UInt64 res, UInt64 a, UInt64 b, int index) => __TryProcesVEC_FCMP_helper(elem_sizecode, out res, a, b, index, false, false, false, true, false);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] private bool __TryProcesVEC_FCMP_NEQ_UQ(UInt64 elem_sizecode, out UInt64 res, UInt64 a, UInt64 b, int index) => __TryProcesVEC_FCMP_helper(elem_sizecode, out res, a, b, index, true, true, false, true, false);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] private bool __TryProcesVEC_FCMP_NLT_US(UInt64 elem_sizecode, out UInt64 res, UInt64 a, UInt64 b, int index) => __TryProcesVEC_FCMP_helper(elem_sizecode, out res, a, b, index, true, false, true, true, true);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] private bool __TryProcesVEC_FCMP_NLE_US(UInt64 elem_sizecode, out UInt64 res, UInt64 a, UInt64 b, int index) => __TryProcesVEC_FCMP_helper(elem_sizecode, out res, a, b, index, true, false, false, true, true);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] private bool __TryProcesVEC_FCMP_ORD_Q(UInt64 elem_sizecode, out UInt64 res, UInt64 a, UInt64 b, int index) => __TryProcesVEC_FCMP_helper(elem_sizecode, out res, a, b, index, true, true, true, false, false);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] private bool __TryProcesVEC_FCMP_EQ_UQ(UInt64 elem_sizecode, out UInt64 res, UInt64 a, UInt64 b, int index) => __TryProcesVEC_FCMP_helper(elem_sizecode, out res, a, b, index, false, false, true, true, false);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] private bool __TryProcesVEC_FCMP_NGE_US(UInt64 elem_sizecode, out UInt64 res, UInt64 a, UInt64 b, int index) => __TryProcesVEC_FCMP_helper(elem_sizecode, out res, a, b, index, false, true, false, true, true);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] private bool __TryProcesVEC_FCMP_NGT_US(UInt64 elem_sizecode, out UInt64 res, UInt64 a, UInt64 b, int index) => __TryProcesVEC_FCMP_helper(elem_sizecode, out res, a, b, index, false, true, true, true, true);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] private bool __TryProcesVEC_FCMP_FALSE_OQ(UInt64 elem_sizecode, out UInt64 res, UInt64 a, UInt64 b, int index) => __TryProcesVEC_FCMP_helper(elem_sizecode, out res, a, b, index, false, false, false, false, false);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] private bool __TryProcesVEC_FCMP_NEQ_OQ(UInt64 elem_sizecode, out UInt64 res, UInt64 a, UInt64 b, int index) => __TryProcesVEC_FCMP_helper(elem_sizecode, out res, a, b, index, true, true, false, false, false);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] private bool __TryProcesVEC_FCMP_GE_OS(UInt64 elem_sizecode, out UInt64 res, UInt64 a, UInt64 b, int index) => __TryProcesVEC_FCMP_helper(elem_sizecode, out res, a, b, index, true, false, true, false, true);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] private bool __TryProcesVEC_FCMP_GT_OS(UInt64 elem_sizecode, out UInt64 res, UInt64 a, UInt64 b, int index) => __TryProcesVEC_FCMP_helper(elem_sizecode, out res, a, b, index, true, false, false, false, true);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] private bool __TryProcesVEC_FCMP_TRUE_UQ(UInt64 elem_sizecode, out UInt64 res, UInt64 a, UInt64 b, int index) => __TryProcesVEC_FCMP_helper(elem_sizecode, out res, a, b, index, true, true, true, true, false);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] private bool __TryProcesVEC_FCMP_EQ_OS(UInt64 elem_sizecode, out UInt64 res, UInt64 a, UInt64 b, int index) => __TryProcesVEC_FCMP_helper(elem_sizecode, out res, a, b, index, false, false, true, false, true);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] private bool __TryProcesVEC_FCMP_LT_OQ(UInt64 elem_sizecode, out UInt64 res, UInt64 a, UInt64 b, int index) => __TryProcesVEC_FCMP_helper(elem_sizecode, out res, a, b, index, false, true, false, false, false);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] private bool __TryProcesVEC_FCMP_LE_OQ(UInt64 elem_sizecode, out UInt64 res, UInt64 a, UInt64 b, int index) => __TryProcesVEC_FCMP_helper(elem_sizecode, out res, a, b, index, false, true, true, false, false);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] private bool __TryProcesVEC_FCMP_UNORD_S(UInt64 elem_sizecode, out UInt64 res, UInt64 a, UInt64 b, int index) => __TryProcesVEC_FCMP_helper(elem_sizecode, out res, a, b, index, false, false, false, true, true);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] private bool __TryProcesVEC_FCMP_NEQ_US(UInt64 elem_sizecode, out UInt64 res, UInt64 a, UInt64 b, int index) => __TryProcesVEC_FCMP_helper(elem_sizecode, out res, a, b, index, true, true, false, true, true);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] private bool __TryProcesVEC_FCMP_NLT_UQ(UInt64 elem_sizecode, out UInt64 res, UInt64 a, UInt64 b, int index) => __TryProcesVEC_FCMP_helper(elem_sizecode, out res, a, b, index, true, false, true, true, false);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] private bool __TryProcesVEC_FCMP_NLE_UQ(UInt64 elem_sizecode, out UInt64 res, UInt64 a, UInt64 b, int index) => __TryProcesVEC_FCMP_helper(elem_sizecode, out res, a, b, index, true, false, false, true, false);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] private bool __TryProcesVEC_FCMP_ORD_S(UInt64 elem_sizecode, out UInt64 res, UInt64 a, UInt64 b, int index) => __TryProcesVEC_FCMP_helper(elem_sizecode, out res, a, b, index, true, true, true, false, true);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] private bool __TryProcesVEC_FCMP_EQ_US(UInt64 elem_sizecode, out UInt64 res, UInt64 a, UInt64 b, int index) => __TryProcesVEC_FCMP_helper(elem_sizecode, out res, a, b, index, false, false, true, true, true);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] private bool __TryProcesVEC_FCMP_NGE_UQ(UInt64 elem_sizecode, out UInt64 res, UInt64 a, UInt64 b, int index) => __TryProcesVEC_FCMP_helper(elem_sizecode, out res, a, b, index, false, true, false, true, false);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] private bool __TryProcesVEC_FCMP_NGT_UQ(UInt64 elem_sizecode, out UInt64 res, UInt64 a, UInt64 b, int index) => __TryProcesVEC_FCMP_helper(elem_sizecode, out res, a, b, index, false, true, true, true, false);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] private bool __TryProcesVEC_FCMP_FALSE_OS(UInt64 elem_sizecode, out UInt64 res, UInt64 a, UInt64 b, int index) => __TryProcesVEC_FCMP_helper(elem_sizecode, out res, a, b, index, false, false, false, false, true);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] private bool __TryProcesVEC_FCMP_NEQ_OS(UInt64 elem_sizecode, out UInt64 res, UInt64 a, UInt64 b, int index) => __TryProcesVEC_FCMP_helper(elem_sizecode, out res, a, b, index, true, true, false, false, true);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] private bool __TryProcesVEC_FCMP_GE_OQ(UInt64 elem_sizecode, out UInt64 res, UInt64 a, UInt64 b, int index) => __TryProcesVEC_FCMP_helper(elem_sizecode, out res, a, b, index, true, false, true, false, false);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] private bool __TryProcesVEC_FCMP_GT_OQ(UInt64 elem_sizecode, out UInt64 res, UInt64 a, UInt64 b, int index) => __TryProcesVEC_FCMP_helper(elem_sizecode, out res, a, b, index, true, false, false, false, false);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] private bool __TryProcesVEC_FCMP_TRUE_US(UInt64 elem_sizecode, out UInt64 res, UInt64 a, UInt64 b, int index) => __TryProcesVEC_FCMP_helper(elem_sizecode, out res, a, b, index, true, true, true, true, true);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryProcessVEC_FCMP()
        {
            // read condition byte
            if (!GetMemAdv(1, out UInt64 cond)) return false;
            
            // perform the instruction
            switch (cond)
            {
                case 00: return ProcessVPUBinary(12, __TryProcesVEC_FCMP_EQ_OQ);
                case 01: return ProcessVPUBinary(12, __TryProcesVEC_FCMP_LT_OS);
                case 02: return ProcessVPUBinary(12, __TryProcesVEC_FCMP_LE_OS);
                case 03: return ProcessVPUBinary(12, __TryProcesVEC_FCMP_UNORD_Q);
                case 04: return ProcessVPUBinary(12, __TryProcesVEC_FCMP_NEQ_UQ);
                case 05: return ProcessVPUBinary(12, __TryProcesVEC_FCMP_NLT_US);
                case 06: return ProcessVPUBinary(12, __TryProcesVEC_FCMP_NLE_US);
                case 07: return ProcessVPUBinary(12, __TryProcesVEC_FCMP_ORD_Q);
                case 08: return ProcessVPUBinary(12, __TryProcesVEC_FCMP_EQ_UQ);
                case 09: return ProcessVPUBinary(12, __TryProcesVEC_FCMP_NGE_US);
                case 10: return ProcessVPUBinary(12, __TryProcesVEC_FCMP_NGT_US);
                case 11: return ProcessVPUBinary(12, __TryProcesVEC_FCMP_FALSE_OQ);
                case 12: return ProcessVPUBinary(12, __TryProcesVEC_FCMP_NEQ_OQ);
                case 13: return ProcessVPUBinary(12, __TryProcesVEC_FCMP_GE_OS);
                case 14: return ProcessVPUBinary(12, __TryProcesVEC_FCMP_GT_OS);
                case 15: return ProcessVPUBinary(12, __TryProcesVEC_FCMP_TRUE_UQ);
                case 16: return ProcessVPUBinary(12, __TryProcesVEC_FCMP_EQ_OS);
                case 17: return ProcessVPUBinary(12, __TryProcesVEC_FCMP_LT_OQ);
                case 18: return ProcessVPUBinary(12, __TryProcesVEC_FCMP_LE_OQ);
                case 19: return ProcessVPUBinary(12, __TryProcesVEC_FCMP_UNORD_S);
                case 20: return ProcessVPUBinary(12, __TryProcesVEC_FCMP_NEQ_US);
                case 21: return ProcessVPUBinary(12, __TryProcesVEC_FCMP_NLT_UQ);
                case 22: return ProcessVPUBinary(12, __TryProcesVEC_FCMP_NLE_UQ);
                case 23: return ProcessVPUBinary(12, __TryProcesVEC_FCMP_ORD_S);
                case 24: return ProcessVPUBinary(12, __TryProcesVEC_FCMP_EQ_US);
                case 25: return ProcessVPUBinary(12, __TryProcesVEC_FCMP_NGE_UQ);
                case 26: return ProcessVPUBinary(12, __TryProcesVEC_FCMP_NGT_UQ);
                case 27: return ProcessVPUBinary(12, __TryProcesVEC_FCMP_FALSE_OS);
                case 28: return ProcessVPUBinary(12, __TryProcesVEC_FCMP_NEQ_OS);
                case 29: return ProcessVPUBinary(12, __TryProcesVEC_FCMP_GE_OQ);
                case 30: return ProcessVPUBinary(12, __TryProcesVEC_FCMP_GT_OQ);
                case 31: return ProcessVPUBinary(12, __TryProcesVEC_FCMP_TRUE_US);

                default: Terminate(ErrorCode.UndefinedBehavior); return false;
            }
        }

        // in order to avoid creating another format just for this, will use VPUBinary.
        // the result will be src1, thus creating the desired no-modification behavior so long as dest == src1.
        // each pair of elements will update the flags in turn, thus the total comparison is on the last pair processed - standard behavior requires it be scalar operation.
        // the assembler will ensure all of this is the case.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool __TryProcessVEC_FCOMI(UInt64 elem_sizecode, out UInt64 res, UInt64 _a, UInt64 _b, int index)
        {
            // temporaries for cmp results
            bool x, y, z;

            if (elem_sizecode == 3)
            {
                double a = AsDouble(_a), b = AsDouble(_b);

                if (a > b) { x = false; y = false; z = false; }
                else if (a < b) { x = false; y = false; z = true; }
                else if (a == b) { x = true; y = false; z = false; }
                // otherwise is unordered
                else { x = true; y = true; z = true; }
            }
            else
            {
                double a = AsFloat((UInt32)_a), b = AsFloat((UInt32)_b);

                if (a > b) { x = false; y = false; z = false; }
                else if (a < b) { x = false; y = false; z = true; }
                else if (a == b) { x = true; y = false; z = false; }
                // otherwise is unordered
                else { x = true; y = true; z = true; }
            }

            // update comparison flags
            ZF = x;
            PF = y;
            CF = z;

            // clear OF, AF, and SF
            OF = false;
            AF = false;
            SF = false;

            // result is src1 (see explanation above)
            res = _a;

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool TryProcessVEC_FCOMI() { return ProcessVPUBinary(12, __TryProcessVEC_FCOMI); }

        // these trigger ArithmeticError on negative sqrt - spec doesn't specify explicitly what to do
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool __TryProcessVEC_FSQRT(UInt64 elem_sizecode, out UInt64 res, UInt64 a, int index)
        {
            if (elem_sizecode == 3)
            {
                double f = AsDouble(a);
                if (f < 0) { Terminate(ErrorCode.ArithmeticError); res = 0; return false; }
                res = DoubleAsUInt64(Math.Sqrt(f));
            }
            else
            {
                float f = AsFloat((UInt32)a);
                if (f < 0) { Terminate(ErrorCode.ArithmeticError); res = 0; return false; }
                res = FloatAsUInt64((float)Math.Sqrt(f));
            }
            return true;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool __TryProcessVEC_FRSQRT(UInt64 elem_sizecode, out UInt64 res, UInt64 a, int index)
        {
            if (elem_sizecode == 3)
            {
                double f = AsDouble(a);
                if (f < 0) { Terminate(ErrorCode.ArithmeticError); res = 0; return false; }
                res = DoubleAsUInt64(1.0 / Math.Sqrt(f));
            }
            else
            {
                float f = AsFloat((UInt32)a);
                if (f < 0) { Terminate(ErrorCode.ArithmeticError); res = 0; return false; }
                res = FloatAsUInt64(1.0f / (float)Math.Sqrt(f));
            }
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool TryProcessVEC_FSQRT() { return ProcessVPUUnary(12, __TryProcessVEC_FSQRT); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool TryProcessVEC_FRSQRT() { return ProcessVPUUnary(12, __TryProcessVEC_FRSQRT); }

        // VPUCVTDelegates for conversions
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool __double_to_i32(out UInt64 res, UInt64 val)
        {
            res = (UInt32)(Int32)PerformRoundTrip(AsDouble(val), MXCSR_RC);
            return true;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool __single_to_i32(out UInt64 res, UInt64 val)
        {
            res = (UInt32)(Int32)PerformRoundTrip(AsFloat((UInt32)val), MXCSR_RC);
            return true;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool __double_to_i64(out UInt64 res, UInt64 val)
        {
            res = (UInt64)(Int64)PerformRoundTrip(AsDouble(val), MXCSR_RC);
            return true;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool __single_to_i64(out UInt64 res, UInt64 val)
        {
            res = (UInt64)(Int64)PerformRoundTrip(AsFloat((UInt32)val), MXCSR_RC);
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool __double_to_ti32(out UInt64 res, UInt64 val)
        {
            res = (UInt32)(Int32)AsDouble(val);
            return true;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool __single_to_ti32(out UInt64 res, UInt64 val)
        {
            res = (UInt32)(Int32)AsFloat((UInt32)val);
            return true;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool __double_to_ti64(out UInt64 res, UInt64 val)
        {
            res = (UInt64)(Int64)AsDouble(val);
            return true;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool __single_to_ti64(out UInt64 res, UInt64 val)
        {
            res = (UInt64)(Int64)AsFloat((UInt32)val);
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool __i32_to_double(out UInt64 res, UInt64 val)
        {
            res = DoubleAsUInt64((double)(Int32)val);
            return true;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool __i32_to_single(out UInt64 res, UInt64 val)
        {
            res = FloatAsUInt64((float)(Int32)val);
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool __i64_to_double(out UInt64 res, UInt64 val)
        {
            res = DoubleAsUInt64((double)(Int64)val);
            return true;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool __i64_to_single(out UInt64 res, UInt64 val)
        {
            res = FloatAsUInt64((float)(Int64)val);
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool __double_to_single(out UInt64 res, UInt64 val)
        {
            res = FloatAsUInt64((float)AsDouble(val));
            return true;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool __single_to_double(out UInt64 res, UInt64 val)
        {
            res = DoubleAsUInt64((double)AsFloat((UInt32)val));
            return true;
        }

        /*
		[8: mode]
		mode =  0: CVTSD2SI r32, xmm
		mode =  1: CVTSD2SI r32, m64
		mode =  2: CVTSD2SI r64, xmm
		mode =  3: CVTSD2SI r64, m64

		mode =  4: CVTSS2SI r32, xmm
		mode =  5: CVTSS2SI r32, m32
		mode =  6: CVTSS2SI r64, xmm
		mode =  7: CVTSS2SI r64, m32

		mode =  8: CVTTSD2SI r32, xmm
		mode =  9: CVTTSD2SI r32, m64
		mode = 10: CVTTSD2SI r64, xmm
		mode = 11: CVTTSD2SI r64, m64

		mode = 12: CVTTSS2SI r32, xmm
		mode = 13: CVTTSS2SI r32, m32
		mode = 14: CVTTSS2SI r64, xmm
		mode = 15: CVTTSS2SI r64, m32

		mode = 16: CVTSI2SD xmm, r32
		mode = 17: CVTSI2SD xmm, m32
		mode = 18: CVTSI2SD xmm, r64
		mode = 19: CVTSI2SD xmm, m64

		mode = 20: CVTSI2SS xmm, r32
		mode = 21: CVTSI2SS xmm, m32
		mode = 22: CVTSI2SS xmm, r64
		mode = 23: CVTSI2SS xmm, m64

		mode = 24: CVTSD2SS xmm, xmm
		mode = 25: CVTSD2SS xmm, m64

		mode = 26: CVTSS2SD xmm, xmm
		mode = 27: CVTSS2SD xmm, m32

        // ---------------------- //

		mode = 28: CVTPD2DQ 2
		mode = 29: CVTPD2DQ 4
		mode = 30: CVTPD2DQ 8

		mode = 31: CVTPS2DQ 4
		mode = 32: CVTPS2DQ 8
		mode = 33: CVTPS2DQ 16

		mode = 34: CVTTPD2DQ 2
		mode = 35: CVTTPD2DQ 4
		mode = 36: CVTTPD2DQ 8

		mode = 37: CVTTPS2DQ 4
		mode = 38: CVTTPS2DQ 8
		mode = 39: CVTTPS2DQ 16

		mode = 40: CVTDQ2PD 2
		mode = 41: CVTDQ2PD 4
		mode = 42: CVTDQ2PD 8

		mode = 43: CVTDQ2PS 4
		mode = 44: CVTDQ2PS 8
		mode = 45: CVTDQ2PS 16

		mode = 46: CVTPD2PS 2
		mode = 47: CVTPD2PS 4
		mode = 48: CVTPD2PS 8

		mode = 49: CVTPS2PD 2
		mode = 50: CVTPS2PD 4
		mode = 51: CVTPS2PD 8

		else UND
		*/
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool TryProcessVEC_CVT()
        {
            // read mode byte
            UInt64 mode;
            if (!GetMemAdv(1, out mode)) return false;

            // route to handlers
            switch (mode)
            {
                case 0: return ProcessVPUCVT_scalar_reg_xmm(2, 3, __double_to_i32);
                case 1: return ProcessVPUCVT_scalar_reg_mem(2, 3, __double_to_i32);
                case 2: return ProcessVPUCVT_scalar_reg_xmm(3, 3, __double_to_i64);
                case 3: return ProcessVPUCVT_scalar_reg_mem(3, 3, __double_to_i64);

                case 4: return ProcessVPUCVT_scalar_reg_xmm(2, 2, __single_to_i32);
                case 5: return ProcessVPUCVT_scalar_reg_mem(2, 2, __single_to_i32);
                case 6: return ProcessVPUCVT_scalar_reg_xmm(3, 2, __single_to_i64);
                case 7: return ProcessVPUCVT_scalar_reg_mem(3, 2, __single_to_i64);

                case 8: return ProcessVPUCVT_scalar_reg_xmm(2, 3, __double_to_ti32);
                case 9: return ProcessVPUCVT_scalar_reg_mem(2, 3, __double_to_ti32);
                case 10: return ProcessVPUCVT_scalar_reg_xmm(3, 3, __double_to_ti64);
                case 11: return ProcessVPUCVT_scalar_reg_mem(3, 3, __double_to_ti64);

                case 12: return ProcessVPUCVT_scalar_reg_xmm(2, 2, __single_to_ti32);
                case 13: return ProcessVPUCVT_scalar_reg_mem(2, 2, __single_to_ti32);
                case 14: return ProcessVPUCVT_scalar_reg_xmm(3, 2, __single_to_ti64);
                case 15: return ProcessVPUCVT_scalar_reg_mem(3, 2, __single_to_ti64);

                case 16: return ProcessVPUCVT_scalar_xmm_reg(3, 2, __i32_to_double);
                case 17: return ProcessVPUCVT_scalar_xmm_mem(3, 2, __i32_to_double);
                case 18: return ProcessVPUCVT_scalar_xmm_reg(3, 3, __i64_to_double);
                case 19: return ProcessVPUCVT_scalar_xmm_mem(3, 3, __i64_to_double);

                case 20: return ProcessVPUCVT_scalar_xmm_reg(2, 2, __i32_to_single);
                case 21: return ProcessVPUCVT_scalar_xmm_mem(2, 2, __i32_to_single);
                case 22: return ProcessVPUCVT_scalar_xmm_reg(2, 3, __i64_to_single);
                case 23: return ProcessVPUCVT_scalar_xmm_mem(2, 3, __i64_to_single);

                case 24: return ProcessVPUCVT_scalar_xmm_xmm(2, 3, __double_to_single);
                case 25: return ProcessVPUCVT_scalar_xmm_mem(2, 3, __double_to_single);

                case 26: return ProcessVPUCVT_scalar_xmm_xmm(3, 2, __single_to_double);
                case 27: return ProcessVPUCVT_scalar_xmm_mem(3, 2, __single_to_double);

                // ------------------------------------------------------------------------ //

                case 28: return ProcessVPUCVT_packed(2, 2, 3, __double_to_i32);
                case 29: return ProcessVPUCVT_packed(4, 2, 3, __double_to_i32);
                case 30: return ProcessVPUCVT_packed(8, 2, 3, __double_to_i32);

                case 31: return ProcessVPUCVT_packed(4, 2, 2, __single_to_i32);
                case 32: return ProcessVPUCVT_packed(8, 2, 2, __single_to_i32);
                case 33: return ProcessVPUCVT_packed(16, 2, 2, __single_to_i32);

                case 34: return ProcessVPUCVT_packed(2, 2, 3, __double_to_ti32);
                case 35: return ProcessVPUCVT_packed(4, 2, 3, __double_to_ti32);
                case 36: return ProcessVPUCVT_packed(8, 2, 3, __double_to_ti32);

                case 37: return ProcessVPUCVT_packed(4, 2, 2, __single_to_ti32);
                case 38: return ProcessVPUCVT_packed(8, 2, 2, __single_to_ti32);
                case 39: return ProcessVPUCVT_packed(16, 2, 2, __single_to_ti32);

                case 40: return ProcessVPUCVT_packed(2, 3, 2, __i32_to_double);
                case 41: return ProcessVPUCVT_packed(4, 3, 2, __i32_to_double);
                case 42: return ProcessVPUCVT_packed(8, 3, 2, __i32_to_double);

                case 43: return ProcessVPUCVT_packed(4, 2, 2, __i32_to_single);
                case 44: return ProcessVPUCVT_packed(8, 2, 2, __i32_to_single);
                case 45: return ProcessVPUCVT_packed(16, 2, 2, __i32_to_single);

                case 46: return ProcessVPUCVT_packed(2, 2, 3, __double_to_single);
                case 47: return ProcessVPUCVT_packed(4, 2, 3, __double_to_single);
                case 48: return ProcessVPUCVT_packed(8, 2, 3, __double_to_single);

                case 49: return ProcessVPUCVT_packed(2, 3, 2, __single_to_double);
                case 50: return ProcessVPUCVT_packed(4, 3, 2, __single_to_double);
                case 51: return ProcessVPUCVT_packed(8, 3, 2, __single_to_double);

                default: Terminate(ErrorCode.UndefinedBehavior); return false;
            }
        }
    }
}
