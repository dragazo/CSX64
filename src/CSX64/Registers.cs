using System;

// -- Interface -- //

namespace CSX64
{
    public partial class Computer
    {
        private CPURegister[] CPURegisters = new CPURegister[16];

        private FPURegister[] FPURegisters = new FPURegister[8];
        private UInt16 FPU_status;

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
        public bool C0
        {
            get => (FPU_status & 0x0100) != 0;
            set => FPU_status = (UInt16)((FPU_status & ~0x0100) | (value ? 0x0100 : 0));
        }
        public bool C1
        {
            get => (FPU_status & 0x0200) != 0;
            set => FPU_status = (UInt16)((FPU_status & ~0x0200) | (value ? 0x0200 : 0));
        }
        public bool C2
        {
            get => (FPU_status & 0x0400) != 0;
            set => FPU_status = (UInt16)((FPU_status & ~0x0400) | (value ? 0x0400 : 0));
        }
        public bool C3
        {
            get => (FPU_status & 0x4000) != 0;
            set => FPU_status = (UInt16)((FPU_status & ~0x4000) | (value ? 0x4000 : 0));
        }

        public byte TOP
        {
            get => (byte)((FPU_status & 0x3800) >> 11);
            set => FPU_status = (UInt16)(FPU_status & ~0x3800 | ((value & 7) << 11));
        }

        public double ST0 { get => FPURegisters[(TOP + 0) & 7].Value; set => FPURegisters[(TOP + 0) & 7].Value = value; }
        public double ST1 { get => FPURegisters[(TOP + 1) & 7].Value; set => FPURegisters[(TOP + 1) & 7].Value = value; }
        public double ST2 { get => FPURegisters[(TOP + 2) & 7].Value; set => FPURegisters[(TOP + 2) & 7].Value = value; }
        public double ST3 { get => FPURegisters[(TOP + 3) & 7].Value; set => FPURegisters[(TOP + 3) & 7].Value = value; }
        public double ST4 { get => FPURegisters[(TOP + 4) & 7].Value; set => FPURegisters[(TOP + 4) & 7].Value = value; }
        public double ST5 { get => FPURegisters[(TOP + 5) & 7].Value; set => FPURegisters[(TOP + 5) & 7].Value = value; }
        public double ST6 { get => FPURegisters[(TOP + 6) & 7].Value; set => FPURegisters[(TOP + 6) & 7].Value = value; }
        public double ST7 { get => FPURegisters[(TOP + 7) & 7].Value; set => FPURegisters[(TOP + 7) & 7].Value = value; }

        public bool ST0_InUse { get => FPURegisters[(TOP + 0) & 7].InUse; set => FPURegisters[(TOP + 0) & 7].InUse = value; }
        public bool ST1_InUse { get => FPURegisters[(TOP + 1) & 7].InUse; set => FPURegisters[(TOP + 1) & 7].InUse = value; }
        public bool ST2_InUse { get => FPURegisters[(TOP + 2) & 7].InUse; set => FPURegisters[(TOP + 2) & 7].InUse = value; }
        public bool ST3_InUse { get => FPURegisters[(TOP + 3) & 7].InUse; set => FPURegisters[(TOP + 3) & 7].InUse = value; }
        public bool ST4_InUse { get => FPURegisters[(TOP + 4) & 7].InUse; set => FPURegisters[(TOP + 4) & 7].InUse = value; }
        public bool ST5_InUse { get => FPURegisters[(TOP + 5) & 7].InUse; set => FPURegisters[(TOP + 5) & 7].InUse = value; }
        public bool ST6_InUse { get => FPURegisters[(TOP + 6) & 7].InUse; set => FPURegisters[(TOP + 6) & 7].InUse = value; }
        public bool ST7_InUse { get => FPURegisters[(TOP + 7) & 7].InUse; set => FPURegisters[(TOP + 7) & 7].InUse = value; }
    }
}
