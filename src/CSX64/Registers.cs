using System;

// -- Interface -- //

namespace CSX64
{
    public partial class Computer
    {
        // source: https://en.wikipedia.org/wiki/FLAGS_register

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
        public bool ID1
        {
            get => (RFLAGS & 0x0020_0000ul) != 0;
            set => RFLAGS = (RFLAGS & ~0x0020_0000ul) | (value ? 0x0020_0000ul : 0);
        }
        public bool ID2
        {
            get => (RFLAGS & 0x0040_0000ul) != 0;
            set => RFLAGS = (RFLAGS & ~0x0040_0000ul) | (value ? 0x0040_0000ul : 0);
        }
        public UInt16 VAD
        {
            get => (UInt16)((RFLAGS >> 23) & 0x1ff);
            set => RFLAGS = (RFLAGS & ~0xff80_0000ul) | ((UInt64)(value & 0x1ff) << 23);
        }

        public bool cc_b { get => CF; }
        public bool cc_be { get => CF || ZF; }
        public bool cc_a { get => !CF && !ZF; }
        public bool cc_ae { get => !CF; }

        public bool cc_l { get => SF != OF; }
        public bool cc_le { get => ZF || SF != OF; }
        public bool cc_g { get => !ZF && SF == OF; }
        public bool cc_ge { get => SF == OF; }

        /// <summary>
        /// Indicates that we're allowed to run file system instructions
        /// </summary>
        public bool FSF
        {
            get => (RFLAGS & 0x000_0001_0000_0000ul) != 0;
            set => RFLAGS = (RFLAGS & ~0x000_0001_0000_0000ul) | (value ? 0x000_0001_0000_0000ul : 0);
        }

        // --------------------------------------------

        private Register _RFLAGS;
        private Register _RIP;
        private Register[] Registers = new Register[16];

        private FPURegister[] FPURegisters = new FPURegister[8];
        private UInt16 FPU_status;

        public UInt64 RFLAGS { get => _RFLAGS.x64; set => _RFLAGS.x64 = value; }
        public UInt32 EFLAGS { get => _RFLAGS.x32; set => _RFLAGS.x32 = value; }
        public UInt16 FLAGS { get => _RFLAGS.x16; set => _RFLAGS.x16 = value; }

        public UInt64 RIP { get => _RIP.x64; set => _RIP.x64 = value; }
        public UInt32 EIP { get => _RIP.x32; set => _RIP.x32 = value; }
        public UInt16 IP { get => _RIP.x16; set => _RIP.x16 = value; }

        public UInt64 RAX { get => Registers[0].x64; set => Registers[0].x64 = value; }
        public UInt64 RBX { get => Registers[1].x64; set => Registers[1].x64 = value; }
        public UInt64 RCX { get => Registers[2].x64; set => Registers[2].x64 = value; }
        public UInt64 RDX { get => Registers[3].x64; set => Registers[3].x64 = value; }
        public UInt64 RSI { get => Registers[4].x64; set => Registers[4].x64 = value; }
        public UInt64 RDI { get => Registers[5].x64; set => Registers[5].x64 = value; }
        public UInt64 RBP { get => Registers[6].x64; set => Registers[6].x64 = value; }
        public UInt64 RSP { get => Registers[7].x64; set => Registers[7].x64 = value; }
        public UInt64 R8 { get => Registers[8].x64; set => Registers[8].x64 = value; }
        public UInt64 R9 { get => Registers[9].x64; set => Registers[9].x64 = value; }
        public UInt64 R10 { get => Registers[10].x64; set => Registers[10].x64 = value; }
        public UInt64 R11 { get => Registers[11].x64; set => Registers[11].x64 = value; }
        public UInt64 R12 { get => Registers[12].x64; set => Registers[12].x64 = value; }
        public UInt64 R13 { get => Registers[13].x64; set => Registers[13].x64 = value; }
        public UInt64 R14 { get => Registers[14].x64; set => Registers[14].x64 = value; }
        public UInt64 R15 { get => Registers[15].x64; set => Registers[15].x64 = value; }

        public UInt32 EAX { get => Registers[0].x32; set => Registers[0].x32 = value; }
        public UInt32 EBX { get => Registers[1].x32; set => Registers[1].x32 = value; }
        public UInt32 ECX { get => Registers[2].x32; set => Registers[2].x32 = value; }
        public UInt32 EDX { get => Registers[3].x32; set => Registers[3].x32 = value; }
        public UInt32 ESI { get => Registers[4].x32; set => Registers[4].x32 = value; }
        public UInt32 EDI { get => Registers[5].x32; set => Registers[5].x32 = value; }
        public UInt32 EBP { get => Registers[6].x32; set => Registers[6].x32 = value; }
        public UInt32 ESP { get => Registers[7].x32; set => Registers[7].x32 = value; }
        public UInt32 R8D { get => Registers[8].x32; set => Registers[8].x32 = value; }
        public UInt32 R9D { get => Registers[9].x32; set => Registers[9].x32 = value; }
        public UInt32 R10D { get => Registers[10].x32; set => Registers[10].x32 = value; }
        public UInt32 R11D { get => Registers[11].x32; set => Registers[11].x32 = value; }
        public UInt32 R12D { get => Registers[12].x32; set => Registers[12].x32 = value; }
        public UInt32 R13D { get => Registers[13].x32; set => Registers[13].x32 = value; }
        public UInt32 R14D { get => Registers[14].x32; set => Registers[14].x32 = value; }
        public UInt32 R15D { get => Registers[15].x32; set => Registers[15].x32 = value; }

        public UInt16 AX { get => Registers[0].x16; set => Registers[0].x16 = value; }
        public UInt16 BX { get => Registers[1].x16; set => Registers[1].x16 = value; }
        public UInt16 CX { get => Registers[2].x16; set => Registers[2].x16 = value; }
        public UInt16 DX { get => Registers[3].x16; set => Registers[3].x16 = value; }
        public UInt16 SI { get => Registers[4].x16; set => Registers[4].x16 = value; }
        public UInt16 DI { get => Registers[5].x16; set => Registers[5].x16 = value; }
        public UInt16 BP { get => Registers[6].x16; set => Registers[6].x16 = value; }
        public UInt16 SP { get => Registers[7].x16; set => Registers[7].x16 = value; }
        public UInt16 R8W { get => Registers[8].x16; set => Registers[8].x16 = value; }
        public UInt16 R9W { get => Registers[9].x16; set => Registers[9].x16 = value; }
        public UInt16 R10W { get => Registers[10].x16; set => Registers[10].x16 = value; }
        public UInt16 R11W { get => Registers[11].x16; set => Registers[11].x16 = value; }
        public UInt16 R12W { get => Registers[12].x16; set => Registers[12].x16 = value; }
        public UInt16 R13W { get => Registers[13].x16; set => Registers[13].x16 = value; }
        public UInt16 R14W { get => Registers[14].x16; set => Registers[14].x16 = value; }
        public UInt16 R15W { get => Registers[15].x16; set => Registers[15].x16 = value; }

        public byte AL { get => Registers[0].x8; set => Registers[0].x8 = value; }
        public byte BL { get => Registers[1].x8; set => Registers[1].x8 = value; }
        public byte CL { get => Registers[2].x8; set => Registers[2].x8 = value; }
        public byte DL { get => Registers[3].x8; set => Registers[3].x8 = value; }
        public byte SIL { get => Registers[4].x8; set => Registers[4].x8 = value; }
        public byte DIL { get => Registers[5].x8; set => Registers[5].x8 = value; }
        public byte BPL { get => Registers[6].x8; set => Registers[6].x8 = value; }
        public byte SPL { get => Registers[7].x8; set => Registers[7].x8 = value; }
        public byte R8B { get => Registers[8].x8; set => Registers[8].x8 = value; }
        public byte R9B { get => Registers[9].x8; set => Registers[9].x8 = value; }
        public byte R10B { get => Registers[10].x8; set => Registers[10].x8 = value; }
        public byte R11B { get => Registers[11].x8; set => Registers[11].x8 = value; }
        public byte R12B { get => Registers[12].x8; set => Registers[12].x8 = value; }
        public byte R13B { get => Registers[13].x8; set => Registers[13].x8 = value; }
        public byte R14B { get => Registers[14].x8; set => Registers[14].x8 = value; }
        public byte R15B { get => Registers[15].x8; set => Registers[15].x8 = value; }

        public byte AH { get => Registers[0].x8h; set => Registers[0].x8h = value; }
        public byte BH { get => Registers[1].x8h; set => Registers[1].x8h = value; }
        public byte CH { get => Registers[2].x8h; set => Registers[2].x8h = value; }
        public byte DH { get => Registers[3].x8h; set => Registers[3].x8h = value; }

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
