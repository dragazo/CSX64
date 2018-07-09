using System;

// -- Interface -- //

namespace CSX64
{
    public partial class Computer
    {
        private CPURegister[] CPURegisters = new CPURegister[16];

        private double[] FPURegisters = new double[8];
        private UInt16 FPU_control, FPU_status, FPU_tag;

        private ZMMRegister[] ZMMRegisters = new ZMMRegister[32];

        public UInt64 RFLAGS;
        public UInt32 EFLAGS { get => (UInt32)RFLAGS; set => RFLAGS = RFLAGS & ~0xfffffffful | value; }
        public UInt16 FLAGS { get => (UInt16)RFLAGS; set => RFLAGS = RFLAGS & ~0xfffful | value; }

        public UInt64 RIP;
        public UInt32 EIP { get => (UInt32)RIP; set => RIP = RIP & ~0xfffffffful | value; }
        public UInt16 IP { get => (UInt16)RIP; set => RIP = RIP & ~0xfffful | value; }

        public UInt64 RAX { get => CPURegisters[0].x64; set => CPURegisters[0].x64 = value; }
        public UInt64 RBX { get => CPURegisters[1].x64; set => CPURegisters[1].x64 = value; }
        public UInt64 RCX { get => CPURegisters[2].x64; set => CPURegisters[2].x64 = value; }
        public UInt64 RDX { get => CPURegisters[3].x64; set => CPURegisters[3].x64 = value; }
        public UInt64 RSI { get => CPURegisters[4].x64; set => CPURegisters[4].x64 = value; }
        public UInt64 RDI { get => CPURegisters[5].x64; set => CPURegisters[5].x64 = value; }
        public UInt64 RBP { get => CPURegisters[6].x64; set => CPURegisters[6].x64 = value; }
        public UInt64 RSP { get => CPURegisters[7].x64; set => CPURegisters[7].x64 = value; }
        public UInt64 R8 { get => CPURegisters[8].x64; set => CPURegisters[8].x64 = value; }
        public UInt64 R9 { get => CPURegisters[9].x64; set => CPURegisters[9].x64 = value; }
        public UInt64 R10 { get => CPURegisters[10].x64; set => CPURegisters[10].x64 = value; }
        public UInt64 R11 { get => CPURegisters[11].x64; set => CPURegisters[11].x64 = value; }
        public UInt64 R12 { get => CPURegisters[12].x64; set => CPURegisters[12].x64 = value; }
        public UInt64 R13 { get => CPURegisters[13].x64; set => CPURegisters[13].x64 = value; }
        public UInt64 R14 { get => CPURegisters[14].x64; set => CPURegisters[14].x64 = value; }
        public UInt64 R15 { get => CPURegisters[15].x64; set => CPURegisters[15].x64 = value; }

        public UInt32 EAX { get => CPURegisters[0].x32; set => CPURegisters[0].x32 = value; }
        public UInt32 EBX { get => CPURegisters[1].x32; set => CPURegisters[1].x32 = value; }
        public UInt32 ECX { get => CPURegisters[2].x32; set => CPURegisters[2].x32 = value; }
        public UInt32 EDX { get => CPURegisters[3].x32; set => CPURegisters[3].x32 = value; }
        public UInt32 ESI { get => CPURegisters[4].x32; set => CPURegisters[4].x32 = value; }
        public UInt32 EDI { get => CPURegisters[5].x32; set => CPURegisters[5].x32 = value; }
        public UInt32 EBP { get => CPURegisters[6].x32; set => CPURegisters[6].x32 = value; }
        public UInt32 ESP { get => CPURegisters[7].x32; set => CPURegisters[7].x32 = value; }
        public UInt32 R8D { get => CPURegisters[8].x32; set => CPURegisters[8].x32 = value; }
        public UInt32 R9D { get => CPURegisters[9].x32; set => CPURegisters[9].x32 = value; }
        public UInt32 R10D { get => CPURegisters[10].x32; set => CPURegisters[10].x32 = value; }
        public UInt32 R11D { get => CPURegisters[11].x32; set => CPURegisters[11].x32 = value; }
        public UInt32 R12D { get => CPURegisters[12].x32; set => CPURegisters[12].x32 = value; }
        public UInt32 R13D { get => CPURegisters[13].x32; set => CPURegisters[13].x32 = value; }
        public UInt32 R14D { get => CPURegisters[14].x32; set => CPURegisters[14].x32 = value; }
        public UInt32 R15D { get => CPURegisters[15].x32; set => CPURegisters[15].x32 = value; }

        public UInt16 AX { get => CPURegisters[0].x16; set => CPURegisters[0].x16 = value; }
        public UInt16 BX { get => CPURegisters[1].x16; set => CPURegisters[1].x16 = value; }
        public UInt16 CX { get => CPURegisters[2].x16; set => CPURegisters[2].x16 = value; }
        public UInt16 DX { get => CPURegisters[3].x16; set => CPURegisters[3].x16 = value; }
        public UInt16 SI { get => CPURegisters[4].x16; set => CPURegisters[4].x16 = value; }
        public UInt16 DI { get => CPURegisters[5].x16; set => CPURegisters[5].x16 = value; }
        public UInt16 BP { get => CPURegisters[6].x16; set => CPURegisters[6].x16 = value; }
        public UInt16 SP { get => CPURegisters[7].x16; set => CPURegisters[7].x16 = value; }
        public UInt16 R8W { get => CPURegisters[8].x16; set => CPURegisters[8].x16 = value; }
        public UInt16 R9W { get => CPURegisters[9].x16; set => CPURegisters[9].x16 = value; }
        public UInt16 R10W { get => CPURegisters[10].x16; set => CPURegisters[10].x16 = value; }
        public UInt16 R11W { get => CPURegisters[11].x16; set => CPURegisters[11].x16 = value; }
        public UInt16 R12W { get => CPURegisters[12].x16; set => CPURegisters[12].x16 = value; }
        public UInt16 R13W { get => CPURegisters[13].x16; set => CPURegisters[13].x16 = value; }
        public UInt16 R14W { get => CPURegisters[14].x16; set => CPURegisters[14].x16 = value; }
        public UInt16 R15W { get => CPURegisters[15].x16; set => CPURegisters[15].x16 = value; }

        public byte AL { get => CPURegisters[0].x8; set => CPURegisters[0].x8 = value; }
        public byte BL { get => CPURegisters[1].x8; set => CPURegisters[1].x8 = value; }
        public byte CL { get => CPURegisters[2].x8; set => CPURegisters[2].x8 = value; }
        public byte DL { get => CPURegisters[3].x8; set => CPURegisters[3].x8 = value; }
        public byte SIL { get => CPURegisters[4].x8; set => CPURegisters[4].x8 = value; }
        public byte DIL { get => CPURegisters[5].x8; set => CPURegisters[5].x8 = value; }
        public byte BPL { get => CPURegisters[6].x8; set => CPURegisters[6].x8 = value; }
        public byte SPL { get => CPURegisters[7].x8; set => CPURegisters[7].x8 = value; }
        public byte R8B { get => CPURegisters[8].x8; set => CPURegisters[8].x8 = value; }
        public byte R9B { get => CPURegisters[9].x8; set => CPURegisters[9].x8 = value; }
        public byte R10B { get => CPURegisters[10].x8; set => CPURegisters[10].x8 = value; }
        public byte R11B { get => CPURegisters[11].x8; set => CPURegisters[11].x8 = value; }
        public byte R12B { get => CPURegisters[12].x8; set => CPURegisters[12].x8 = value; }
        public byte R13B { get => CPURegisters[13].x8; set => CPURegisters[13].x8 = value; }
        public byte R14B { get => CPURegisters[14].x8; set => CPURegisters[14].x8 = value; }
        public byte R15B { get => CPURegisters[15].x8; set => CPURegisters[15].x8 = value; }

        public byte AH { get => CPURegisters[0].x8h; set => CPURegisters[0].x8h = value; }
        public byte BH { get => CPURegisters[1].x8h; set => CPURegisters[1].x8h = value; }
        public byte CH { get => CPURegisters[2].x8h; set => CPURegisters[2].x8h = value; }
        public byte DH { get => CPURegisters[3].x8h; set => CPURegisters[3].x8h = value; }

        // source: https://en.wikipedia.org/wiki/FLAGS_register
        // source: http://www.eecg.toronto.edu/~amza/www.mindsec.com/files/x86regs.html

        public bool CF
        {
            get => (RFLAGS & 0x0001ul) != 0;
            set => RFLAGS = (RFLAGS & ~0x0001ul) | (value ? 0x0001ul : 0);
        }
        public bool PF
        {
            get => (RFLAGS & 0x0004ul) != 0;
            set => RFLAGS = (RFLAGS & ~0x0004ul) | (value ? 0x0004ul : 0);
        }
        public bool AF
        {
            get => (RFLAGS & 0x0010ul) != 0;
            set => RFLAGS = (RFLAGS & ~0x0010ul) | (value ? 0x0010ul : 0);
        }
        public bool ZF
        {
            get => (RFLAGS & 0x0040ul) != 0;
            set => RFLAGS = (RFLAGS & ~0x0040ul) | (value ? 0x0040ul : 0);
        }
        public bool SF
        {
            get => (RFLAGS & 0x0080ul) != 0;
            set => RFLAGS = (RFLAGS & ~0x0080ul) | (value ? 0x0080ul : 0);
        }
        public bool TF
        {
            get => (RFLAGS & 0x0100ul) != 0;
            set => RFLAGS = (RFLAGS & ~0x0100ul) | (value ? 0x0100ul : 0);
        }
        public bool IF
        {
            get => (RFLAGS & 0x0200ul) != 0;
            set => RFLAGS = (RFLAGS & ~0x0200ul) | (value ? 0x0200ul : 0);
        }
        public bool DF
        {
            get => (RFLAGS & 0x0400ul) != 0;
            set => RFLAGS = (RFLAGS & ~0x0400ul) | (value ? 0x0400ul : 0);
        }
        public bool OF
        {
            get => (RFLAGS & 0x0800ul) != 0;
            set => RFLAGS = (RFLAGS & ~0x0800ul) | (value ? 0x0800ul : 0);
        }
        public byte IOPL
        {
            get => (byte)((RFLAGS >> 12) & 3);
            set => RFLAGS = (RFLAGS & ~0x3000ul) | ((UInt64)(value & 3) << 12);
        }
        public bool NT
        {
            get => (RFLAGS & 0x4000ul) != 0;
            set => RFLAGS = (RFLAGS & ~0x4000ul) | (value ? 0x4000ul : 0);
        }

        public bool RF
        {
            get => (RFLAGS & 0x0001_0000ul) != 0;
            set => RFLAGS = (RFLAGS & ~0x0001_0000ul) | (value ? 0x0001_0000ul : 0);
        }
        public bool VM
        {
            get => (RFLAGS & 0x0002_0000ul) != 0;
            set => RFLAGS = (RFLAGS & ~0x0002_0000ul) | (value ? 0x0002_0000ul : 0);
        }
        public bool AC
        {
            get => (RFLAGS & 0x0004_0000ul) != 0;
            set => RFLAGS = (RFLAGS & ~0x0004_0000ul) | (value ? 0x0004_0000ul : 0);
        }
        public bool VIF
        {
            get => (RFLAGS & 0x0008_0000ul) != 0;
            set => RFLAGS = (RFLAGS & ~0x0008_0000ul) | (value ? 0x0008_0000ul : 0);
        }
        public bool VIP
        {
            get => (RFLAGS & 0x0010_0000ul) != 0;
            set => RFLAGS = (RFLAGS & ~0x0010_0000ul) | (value ? 0x0010_0000ul : 0);
        }
        public bool ID
        {
            get => (RFLAGS & 0x0020_0000ul) != 0;
            set => RFLAGS = (RFLAGS & ~0x0020_0000ul) | (value ? 0x0020_0000ul : 0);
        }

        public bool cc_b => CF;
        public bool cc_be => CF || ZF;
        public bool cc_a => !CF && !ZF;
        public bool cc_ae => !CF;

        public bool cc_l => SF != OF;
        public bool cc_le => ZF || SF != OF;
        public bool cc_g => !ZF && SF == OF;
        public bool cc_ge => SF == OF;

        /// <summary>
        /// Indicates that we're allowed to run file system instructions
        /// </summary>
        public bool FSF
        {
            get => (RFLAGS & 0x000_0001_0000_0000ul) != 0;
            set => RFLAGS = (RFLAGS & ~0x000_0001_0000_0000ul) | (value ? 0x000_0001_0000_0000ul : 0);
        }

        // source : http://www.website.masmforum.com/tutorials/fptute/fpuchap1.htm

        /// <summary>
        /// FPU Invalid Operation Mask
        /// </summary>
        public bool FPU_IM
        {
            get => (FPU_control & 0x0001) != 0;
            set => FPU_control = (UInt16)((FPU_control & ~0x0001) | (value ? 0x0001 : 0));
        }
        /// <summary>
        /// FPU Denormalized Operand Mask
        /// </summary>
        public bool FPU_DM
        {
            get => (FPU_control & 0x0002ul) != 0;
            set => FPU_control = (UInt16)((FPU_control & ~0x0002ul) | (value ? 0x0002ul : 0));
        }
        /// <summary>
        /// FPU Zero Divide Mask
        /// </summary>
        public bool FPU_ZM
        {
            get => (FPU_control & 0x0004ul) != 0;
            set => FPU_control = (UInt16)((FPU_control & ~0x0004ul) | (value ? 0x0004ul : 0));
        }
        /// <summary>
        /// FPU Overflow Mask
        /// </summary>
        public bool FPU_OM
        {
            get => (FPU_control & 0x0008ul) != 0;
            set => FPU_control = (UInt16)((FPU_control & ~0x0008ul) | (value ? 0x0008ul : 0));
        }
        /// <summary>
        /// FPU Underflow Mask
        /// </summary>
        public bool FPU_UM
        {
            get => (FPU_control & 0x0010) != 0;
            set => FPU_control = (UInt16)((FPU_control & ~0x0010) | (value ? 0x0010 : 0));
        }
        /// <summary>
        /// FPU Precision Mask
        /// </summary>
        public bool FPU_PM
        {
            get => (FPU_control & 0x0020) != 0;
            set => FPU_control = (UInt16)((FPU_control & ~0x0020) | (value ? 0x0020 : 0));
        }
        /// <summary>
        /// FPU Interrupt Enable Mask
        /// </summary>
        public bool FPU_IEM
        {
            get => (FPU_control & 0x0080) != 0;
            set => FPU_control = (UInt16)((FPU_control & ~0x0080) | (value ? 0x0080 : 0));
        }
        /// <summary>
        /// FPU Precision Control
        /// </summary>
        public byte FPU_PC
        {
            get => (byte)((FPU_control >> 8) & 3);
            set => FPU_control = (UInt16)((FPU_control & ~0x300) | ((value & 3) << 8));
        }
        /// <summary>
        /// FPU Rounding Control
        /// </summary>
        public byte FPU_RC
        {
            get => (byte)((FPU_control >> 10) & 3);
            set => FPU_control = (UInt16)((FPU_control & ~0xc00) | ((value & 3) << 10));
        }
        /// <summary>
        /// FPU Infinity Control
        /// </summary>
        public bool FPU_IC
        {
            get => (FPU_control & 0x1000) != 0;
            set => FPU_control = (UInt16)((FPU_control & ~0x1000) | (value ? 0x1000 : 0));
        }

        /// <summary>
        /// FPU Invalid Operation Exception
        /// </summary>
        public bool FPU_I
        {
            get => (FPU_status & 0x0001) != 0;
            set => FPU_status = (UInt16)((FPU_status & ~0x0001) | (value ? 0x0001 : 0));
        }
        /// <summary>
        /// FPU Denormalized Exception
        /// </summary>
        public bool FPU_D
        {
            get => (FPU_status & 0x0002) != 0;
            set => FPU_status = (UInt16)((FPU_status & ~0x0002) | (value ? 0x0002 : 0));
        }
        /// <summary>
        /// FPU Zero Divide Exception
        /// </summary>
        public bool FPU_Z
        {
            get => (FPU_status & 0x0004) != 0;
            set => FPU_status = (UInt16)((FPU_status & ~0x0004) | (value ? 0x0004 : 0));
        }
        /// <summary>
        /// FPU Overflow Exception
        /// </summary>
        public bool FPU_O
        {
            get => (FPU_status & 0x0008) != 0;
            set => FPU_status = (UInt16)((FPU_status & ~0x0008) | (value ? 0x0008 : 0));
        }
        /// <summary>
        /// FPU Underflow Exception
        /// </summary>
        public bool FPU_U
        {
            get => (FPU_status & 0x0010) != 0;
            set => FPU_status = (UInt16)((FPU_status & ~0x0010) | (value ? 0x0010 : 0));
        }
        /// <summary>
        /// FPU Precision Exception
        /// </summary>
        public bool FPU_P
        {
            get => (FPU_status & 0x0020) != 0;
            set => FPU_status = (UInt16)((FPU_status & ~0x0020) | (value ? 0x0020 : 0));
        }
        /// <summary>
        /// FPU Stack Fault Exception
        /// </summary>
        public bool FPU_SF
        {
            get => (FPU_status & 0x0040) != 0;
            set => FPU_status = (UInt16)((FPU_status & ~0x0040) | (value ? 0x0040 : 0));
        }
        /// <summary>
        /// FPU Interrupt Request
        /// </summary>
        public bool FPU_IR
        {
            get => (FPU_status & 0x0080) != 0;
            set => FPU_status = (UInt16)((FPU_status & ~0x0080) | (value ? 0x0080 : 0));
        }
        /// <summary>
        /// FPU Busy
        /// </summary>
        public bool FPU_B
        {
            get => (FPU_status & 0x8000) != 0;
            set => FPU_status = (UInt16)((FPU_status & ~0x8000) | (value ? 0x8000 : 0));
        }
        /// <summary>
        /// FPU Condition 0
        /// </summary>
        public bool FPU_C0
        {
            get => (FPU_status & 0x0100) != 0;
            set => FPU_status = (UInt16)((FPU_status & ~0x0100) | (value ? 0x0100 : 0));
        }
        /// <summary>
        /// FPU Condition 1
        /// </summary>
        public bool FPU_C1
        {
            get => (FPU_status & 0x0200) != 0;
            set => FPU_status = (UInt16)((FPU_status & ~0x0200) | (value ? 0x0200 : 0));
        }
        /// <summary>
        /// FPU Condition 2
        /// </summary>
        public bool FPU_C2
        {
            get => (FPU_status & 0x0400) != 0;
            set => FPU_status = (UInt16)((FPU_status & ~0x0400) | (value ? 0x0400 : 0));
        }
        /// <summary>
        /// FPU Top of Stack
        /// </summary>
        public byte FPU_TOP
        {
            get => (byte)((FPU_status >> 11) & 7);
            set => FPU_status = (UInt16)(FPU_status & ~0x3800 | ((value & 7) << 11));
        }
        /// <summary>
        /// FPU Condition 3
        /// </summary>
        public bool FPU_C3
        {
            get => (FPU_status & 0x4000) != 0;
            set => FPU_status = (UInt16)((FPU_status & ~0x4000) | (value ? 0x4000 : 0));
        }

        /// <summary>
        /// The FPU tag value corresponding to a normal value
        /// </summary>
        public const byte FPU_Tag_normal = 0;
        /// <summary>
        /// The FPU tag value corresponding to zero
        /// </summary>
        public const byte FPU_Tag_zero = 1;
        /// <summary>
        /// The FPU tag value corresponding to a special value (NaN, +-inf, denorm)
        /// </summary>
        public const byte FPU_Tag_special = 2;
        /// <summary>
        /// The FPU tag value corresponding to no value
        /// </summary>
        public const byte FPU_Tag_empty = 3;

        /// <summary>
        /// Gets the ST register's value
        /// </summary>
        /// <param name="num">the ordinal number of the ST register (0 for ST0)</param>
        public double ST(int num)
        {
            num = (FPU_TOP + num) & 7;
            return FPURegisters[num];
        }
        /// <summary>
        /// Sets the ST register's value
        /// </summary>
        /// <param name="num">the ordinal number of the ST register (0 for ST0)</param>
        /// <param name="value">the value to set</param>
        public void ST(int num, double value)
        {
            num = (FPU_TOP + num) & 7;
            FPURegisters[num] = value;
            FPU_tag = (UInt16)((FPU_tag & ~(3 << (num * 2))) | (ComputeFPUTag(value) << (num * 2)));
        }
        /// <summary>
        /// Gets the ST register's tag
        /// </summary>
        /// <param name="num">the ordinal number of the ST register (0 for ST0)</param>
        public byte ST_Tag(int num)
        {
            num = (FPU_TOP + num) & 7;
            return (byte)((FPU_tag >> (num * 2)) & 3);
        }
        /// <summary>
        /// Sets the ST register's tag to <see cref="FPU_Tag_empty"/>
        /// </summary>
        /// <param name="num">the ordinal number of the ST register (0 for ST0)</param>
        public void ST_Free(int num)
        {
            num = (FPU_TOP + num) & 7;
            FPU_tag = (UInt16)(FPU_tag | (3 << (num * 2)));
        }

        /// <summary>
        /// Gets the ST register's value
        /// </summary>
        /// <param name="num">the ordinal number of the ST register (0 for ST0)</param>
        public double ST(UInt64 num) => ST((int)num);
        /// <summary>
        /// Sets the ST register's value
        /// </summary>
        /// <param name="num">the ordinal number of the ST register (0 for ST0)</param>
        /// <param name="value">the value to set</param>
        public void ST(UInt64 num, double value) => ST((int)num, value);
        /// <summary>
        /// Gets the ST register's tag
        /// </summary>
        /// <param name="num">the ordinal number of the ST register (0 for ST0)</param>
        public byte ST_Tag(UInt64 num) => ST_Tag((int)num);
        /// <summary>
        /// Sets the ST register's tag to <see cref="FPU_Tag_empty"/>
        /// </summary>
        /// <param name="num">the ordinal number of the ST register (0 for ST0)</param>
        public void ST_Free(UInt64 num) => ST_Free((int)num);

        public double ST0 { get => ST(0); set => ST(0, value); }
        public double ST1 { get => ST(1); set => ST(1, value); }
        public double ST2 { get => ST(2); set => ST(2, value); }
        public double ST3 { get => ST(3); set => ST(3, value); }
        public double ST4 { get => ST(4); set => ST(4, value); }
        public double ST5 { get => ST(5); set => ST(5, value); }
        public double ST6 { get => ST(6); set => ST(6, value); }
        public double ST7 { get => ST(7); set => ST(7, value); }

        public ZMMRegister ZMM0 { get => ZMMRegisters[0]; set => ZMMRegisters[0] = value; }
        public ZMMRegister ZMM1 { get => ZMMRegisters[1]; set => ZMMRegisters[1] = value; }
        public ZMMRegister ZMM2 { get => ZMMRegisters[2]; set => ZMMRegisters[2] = value; }
        public ZMMRegister ZMM3 { get => ZMMRegisters[3]; set => ZMMRegisters[3] = value; }
        public ZMMRegister ZMM4 { get => ZMMRegisters[4]; set => ZMMRegisters[4] = value; }
        public ZMMRegister ZMM5 { get => ZMMRegisters[5]; set => ZMMRegisters[5] = value; }
        public ZMMRegister ZMM6 { get => ZMMRegisters[6]; set => ZMMRegisters[6] = value; }
        public ZMMRegister ZMM7 { get => ZMMRegisters[7]; set => ZMMRegisters[7] = value; }
        public ZMMRegister ZMM8 { get => ZMMRegisters[8]; set => ZMMRegisters[8] = value; }
        public ZMMRegister ZMM9 { get => ZMMRegisters[9]; set => ZMMRegisters[9] = value; }
        public ZMMRegister ZMM10 { get => ZMMRegisters[10]; set => ZMMRegisters[10] = value; }
        public ZMMRegister ZMM11 { get => ZMMRegisters[11]; set => ZMMRegisters[11] = value; }
        public ZMMRegister ZMM12 { get => ZMMRegisters[12]; set => ZMMRegisters[12] = value; }
        public ZMMRegister ZMM13 { get => ZMMRegisters[13]; set => ZMMRegisters[13] = value; }
        public ZMMRegister ZMM14 { get => ZMMRegisters[14]; set => ZMMRegisters[14] = value; }
        public ZMMRegister ZMM15 { get => ZMMRegisters[15]; set => ZMMRegisters[15] = value; }
        public ZMMRegister ZMM16 { get => ZMMRegisters[16]; set => ZMMRegisters[16] = value; }
        public ZMMRegister ZMM17 { get => ZMMRegisters[17]; set => ZMMRegisters[17] = value; }
        public ZMMRegister ZMM18 { get => ZMMRegisters[18]; set => ZMMRegisters[18] = value; }
        public ZMMRegister ZMM19 { get => ZMMRegisters[19]; set => ZMMRegisters[19] = value; }
        public ZMMRegister ZMM20 { get => ZMMRegisters[20]; set => ZMMRegisters[20] = value; }
        public ZMMRegister ZMM21 { get => ZMMRegisters[21]; set => ZMMRegisters[21] = value; }
        public ZMMRegister ZMM22 { get => ZMMRegisters[22]; set => ZMMRegisters[22] = value; }
        public ZMMRegister ZMM23 { get => ZMMRegisters[23]; set => ZMMRegisters[23] = value; }
        public ZMMRegister ZMM24 { get => ZMMRegisters[24]; set => ZMMRegisters[24] = value; }
        public ZMMRegister ZMM25 { get => ZMMRegisters[25]; set => ZMMRegisters[25] = value; }
        public ZMMRegister ZMM26 { get => ZMMRegisters[26]; set => ZMMRegisters[26] = value; }
        public ZMMRegister ZMM27 { get => ZMMRegisters[27]; set => ZMMRegisters[27] = value; }
        public ZMMRegister ZMM28 { get => ZMMRegisters[28]; set => ZMMRegisters[28] = value; }
        public ZMMRegister ZMM29 { get => ZMMRegisters[29]; set => ZMMRegisters[29] = value; }
        public ZMMRegister ZMM30 { get => ZMMRegisters[30]; set => ZMMRegisters[30] = value; }
        public ZMMRegister ZMM31 { get => ZMMRegisters[31]; set => ZMMRegisters[31] = value; }
    }
}
