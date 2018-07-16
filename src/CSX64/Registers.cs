using System;
using System.Runtime.CompilerServices;

// -- Interface -- //

namespace CSX64
{
    public partial class Computer
    {
        protected CPURegister[] CPURegisters = new CPURegister[16];

        protected double[] FPURegisters = new double[8];
        protected UInt16 FPU_control, FPU_status, FPU_tag;

        protected ZMMRegister[] ZMMRegisters = new ZMMRegister[32];

        // ------------------------------------

        public UInt64 RFLAGS;
        public UInt32 EFLAGS { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => (UInt32)RFLAGS; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => RFLAGS = RFLAGS & ~0xfffffffful | value; }
        public UInt16 FLAGS { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => (UInt16)RFLAGS; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => RFLAGS = RFLAGS & ~0xfffful | value; }

        public UInt64 RIP;
        public UInt32 EIP { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => (UInt32)RIP; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => RIP = value; }
        public UInt16 IP { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => (UInt16)RIP; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => RIP = value; }

        public UInt64 RAX { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => CPURegisters[0].x64; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => CPURegisters[0].x64 = value; }
        public UInt64 RBX { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => CPURegisters[1].x64; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => CPURegisters[1].x64 = value; }
        public UInt64 RCX { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => CPURegisters[2].x64; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => CPURegisters[2].x64 = value; }
        public UInt64 RDX { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => CPURegisters[3].x64; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => CPURegisters[3].x64 = value; }
        public UInt64 RSI { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => CPURegisters[4].x64; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => CPURegisters[4].x64 = value; }
        public UInt64 RDI { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => CPURegisters[5].x64; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => CPURegisters[5].x64 = value; }
        public UInt64 RBP { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => CPURegisters[6].x64; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => CPURegisters[6].x64 = value; }
        public UInt64 RSP { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => CPURegisters[7].x64; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => CPURegisters[7].x64 = value; }
        public UInt64 R8 { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => CPURegisters[8].x64; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => CPURegisters[8].x64 = value; }
        public UInt64 R9 { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => CPURegisters[9].x64; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => CPURegisters[9].x64 = value; }
        public UInt64 R10 { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => CPURegisters[10].x64; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => CPURegisters[10].x64 = value; }
        public UInt64 R11 { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => CPURegisters[11].x64; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => CPURegisters[11].x64 = value; }
        public UInt64 R12 { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => CPURegisters[12].x64; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => CPURegisters[12].x64 = value; }
        public UInt64 R13 { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => CPURegisters[13].x64; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => CPURegisters[13].x64 = value; }
        public UInt64 R14 { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => CPURegisters[14].x64; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => CPURegisters[14].x64 = value; }
        public UInt64 R15 { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => CPURegisters[15].x64; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => CPURegisters[15].x64 = value; }

        public UInt32 EAX { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => CPURegisters[0].x32; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => CPURegisters[0].x32 = value; }
        public UInt32 EBX { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => CPURegisters[1].x32; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => CPURegisters[1].x32 = value; }
        public UInt32 ECX { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => CPURegisters[2].x32; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => CPURegisters[2].x32 = value; }
        public UInt32 EDX { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => CPURegisters[3].x32; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => CPURegisters[3].x32 = value; }
        public UInt32 ESI { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => CPURegisters[4].x32; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => CPURegisters[4].x32 = value; }
        public UInt32 EDI { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => CPURegisters[5].x32; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => CPURegisters[5].x32 = value; }
        public UInt32 EBP { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => CPURegisters[6].x32; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => CPURegisters[6].x32 = value; }
        public UInt32 ESP { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => CPURegisters[7].x32; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => CPURegisters[7].x32 = value; }
        public UInt32 R8D { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => CPURegisters[8].x32; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => CPURegisters[8].x32 = value; }
        public UInt32 R9D { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => CPURegisters[9].x32; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => CPURegisters[9].x32 = value; }
        public UInt32 R10D { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => CPURegisters[10].x32; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => CPURegisters[10].x32 = value; }
        public UInt32 R11D { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => CPURegisters[11].x32; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => CPURegisters[11].x32 = value; }
        public UInt32 R12D { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => CPURegisters[12].x32; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => CPURegisters[12].x32 = value; }
        public UInt32 R13D { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => CPURegisters[13].x32; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => CPURegisters[13].x32 = value; }
        public UInt32 R14D { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => CPURegisters[14].x32; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => CPURegisters[14].x32 = value; }
        public UInt32 R15D { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => CPURegisters[15].x32; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => CPURegisters[15].x32 = value; }

        public UInt16 AX { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => CPURegisters[0].x16; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => CPURegisters[0].x16 = value; }
        public UInt16 BX { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => CPURegisters[1].x16; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => CPURegisters[1].x16 = value; }
        public UInt16 CX { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => CPURegisters[2].x16; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => CPURegisters[2].x16 = value; }
        public UInt16 DX { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => CPURegisters[3].x16; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => CPURegisters[3].x16 = value; }
        public UInt16 SI { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => CPURegisters[4].x16; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => CPURegisters[4].x16 = value; }
        public UInt16 DI { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => CPURegisters[5].x16; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => CPURegisters[5].x16 = value; }
        public UInt16 BP { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => CPURegisters[6].x16; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => CPURegisters[6].x16 = value; }
        public UInt16 SP { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => CPURegisters[7].x16; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => CPURegisters[7].x16 = value; }
        public UInt16 R8W { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => CPURegisters[8].x16; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => CPURegisters[8].x16 = value; }
        public UInt16 R9W { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => CPURegisters[9].x16; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => CPURegisters[9].x16 = value; }
        public UInt16 R10W { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => CPURegisters[10].x16; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => CPURegisters[10].x16 = value; }
        public UInt16 R11W { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => CPURegisters[11].x16; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => CPURegisters[11].x16 = value; }
        public UInt16 R12W { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => CPURegisters[12].x16; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => CPURegisters[12].x16 = value; }
        public UInt16 R13W { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => CPURegisters[13].x16; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => CPURegisters[13].x16 = value; }
        public UInt16 R14W { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => CPURegisters[14].x16; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => CPURegisters[14].x16 = value; }
        public UInt16 R15W { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => CPURegisters[15].x16; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => CPURegisters[15].x16 = value; }

        public byte AL { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => CPURegisters[0].x8; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => CPURegisters[0].x8 = value; }
        public byte BL { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => CPURegisters[1].x8; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => CPURegisters[1].x8 = value; }
        public byte CL { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => CPURegisters[2].x8; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => CPURegisters[2].x8 = value; }
        public byte DL { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => CPURegisters[3].x8; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => CPURegisters[3].x8 = value; }
        public byte SIL { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => CPURegisters[4].x8; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => CPURegisters[4].x8 = value; }
        public byte DIL { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => CPURegisters[5].x8; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => CPURegisters[5].x8 = value; }
        public byte BPL { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => CPURegisters[6].x8; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => CPURegisters[6].x8 = value; }
        public byte SPL { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => CPURegisters[7].x8; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => CPURegisters[7].x8 = value; }
        public byte R8B { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => CPURegisters[8].x8; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => CPURegisters[8].x8 = value; }
        public byte R9B { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => CPURegisters[9].x8; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => CPURegisters[9].x8 = value; }
        public byte R10B { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => CPURegisters[10].x8; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => CPURegisters[10].x8 = value; }
        public byte R11B { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => CPURegisters[11].x8; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => CPURegisters[11].x8 = value; }
        public byte R12B { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => CPURegisters[12].x8; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => CPURegisters[12].x8 = value; }
        public byte R13B { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => CPURegisters[13].x8; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => CPURegisters[13].x8 = value; }
        public byte R14B { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => CPURegisters[14].x8; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => CPURegisters[14].x8 = value; }
        public byte R15B { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => CPURegisters[15].x8; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => CPURegisters[15].x8 = value; }

        public byte AH { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => CPURegisters[0].x8h; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => CPURegisters[0].x8h = value; }
        public byte BH { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => CPURegisters[1].x8h; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => CPURegisters[1].x8h = value; }
        public byte CH { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => CPURegisters[2].x8h; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => CPURegisters[2].x8h = value; }
        public byte DH { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => CPURegisters[3].x8h; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => CPURegisters[3].x8h = value; }

        // source: https://en.wikipedia.org/wiki/FLAGS_register
        // source: http://www.eecg.toronto.edu/~amza/www.mindsec.com/files/x86regs.html

        public bool CF
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (RFLAGS & 0x0001ul) != 0;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => RFLAGS = (RFLAGS & ~0x0001ul) | (value ? 0x0001ul : 0);
        }
        public bool PF
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (RFLAGS & 0x0004ul) != 0;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => RFLAGS = (RFLAGS & ~0x0004ul) | (value ? 0x0004ul : 0);
        }
        public bool AF
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (RFLAGS & 0x0010ul) != 0;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => RFLAGS = (RFLAGS & ~0x0010ul) | (value ? 0x0010ul : 0);
        }
        public bool ZF
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (RFLAGS & 0x0040ul) != 0;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => RFLAGS = (RFLAGS & ~0x0040ul) | (value ? 0x0040ul : 0);
        }
        public bool SF
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (RFLAGS & 0x0080ul) != 0;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => RFLAGS = (RFLAGS & ~0x0080ul) | (value ? 0x0080ul : 0);
        }
        public bool TF
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (RFLAGS & 0x0100ul) != 0;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => RFLAGS = (RFLAGS & ~0x0100ul) | (value ? 0x0100ul : 0);
        }
        public bool IF
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (RFLAGS & 0x0200ul) != 0;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => RFLAGS = (RFLAGS & ~0x0200ul) | (value ? 0x0200ul : 0);
        }
        public bool DF
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (RFLAGS & 0x0400ul) != 0;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => RFLAGS = (RFLAGS & ~0x0400ul) | (value ? 0x0400ul : 0);
        }
        public bool OF
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (RFLAGS & 0x0800ul) != 0;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => RFLAGS = (RFLAGS & ~0x0800ul) | (value ? 0x0800ul : 0);
        }
        public byte IOPL
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (byte)((RFLAGS >> 12) & 3);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => RFLAGS = (RFLAGS & ~0x3000ul) | ((UInt64)(value & 3) << 12);
        }
        public bool NT
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (RFLAGS & 0x4000ul) != 0;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => RFLAGS = (RFLAGS & ~0x4000ul) | (value ? 0x4000ul : 0);
        }

        public bool RF
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (RFLAGS & 0x0001_0000ul) != 0;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => RFLAGS = (RFLAGS & ~0x0001_0000ul) | (value ? 0x0001_0000ul : 0);
        }
        public bool VM
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (RFLAGS & 0x0002_0000ul) != 0;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => RFLAGS = (RFLAGS & ~0x0002_0000ul) | (value ? 0x0002_0000ul : 0);
        }
        public bool AC
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (RFLAGS & 0x0004_0000ul) != 0;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => RFLAGS = (RFLAGS & ~0x0004_0000ul) | (value ? 0x0004_0000ul : 0);
        }
        public bool VIF
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (RFLAGS & 0x0008_0000ul) != 0;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => RFLAGS = (RFLAGS & ~0x0008_0000ul) | (value ? 0x0008_0000ul : 0);
        }
        public bool VIP
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (RFLAGS & 0x0010_0000ul) != 0;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => RFLAGS = (RFLAGS & ~0x0010_0000ul) | (value ? 0x0010_0000ul : 0);
        }
        public bool ID
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (RFLAGS & 0x0020_0000ul) != 0;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => RFLAGS = (RFLAGS & ~0x0020_0000ul) | (value ? 0x0020_0000ul : 0);
        }

        public bool cc_b { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => CF; }
        public bool cc_be { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => CF || ZF; }
        public bool cc_a { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => !CF && !ZF; }
        public bool cc_ae { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => !CF; }

        public bool cc_l { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => SF != OF; }
        public bool cc_le { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => ZF || SF != OF; }
        public bool cc_g { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => !ZF && SF == OF; }
        public bool cc_ge { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => SF == OF; }

        /// <summary>
        /// Indicates that we're allowed to run file system instructions
        /// </summary>
        public bool FSF
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (RFLAGS & 0x000_0001_0000_0000ul) != 0;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => RFLAGS = (RFLAGS & ~0x000_0001_0000_0000ul) | (value ? 0x000_0001_0000_0000ul : 0);
        }

        // source : http://www.website.masmforum.com/tutorials/fptute/fpuchap1.htm

        /// <summary>
        /// FPU Invalid Operation Mask
        /// </summary>
        public bool FPU_IM
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (FPU_control & 0x0001) != 0;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => FPU_control = (UInt16)((FPU_control & ~0x0001) | (value ? 0x0001 : 0));
        }
        /// <summary>
        /// FPU Denormalized Operand Mask
        /// </summary>
        public bool FPU_DM
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (FPU_control & 0x0002ul) != 0;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => FPU_control = (UInt16)((FPU_control & ~0x0002ul) | (value ? 0x0002ul : 0));
        }
        /// <summary>
        /// FPU Zero Divide Mask
        /// </summary>
        public bool FPU_ZM
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (FPU_control & 0x0004ul) != 0;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => FPU_control = (UInt16)((FPU_control & ~0x0004ul) | (value ? 0x0004ul : 0));
        }
        /// <summary>
        /// FPU Overflow Mask
        /// </summary>
        public bool FPU_OM
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (FPU_control & 0x0008ul) != 0;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => FPU_control = (UInt16)((FPU_control & ~0x0008ul) | (value ? 0x0008ul : 0));
        }
        /// <summary>
        /// FPU Underflow Mask
        /// </summary>
        public bool FPU_UM
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (FPU_control & 0x0010) != 0;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => FPU_control = (UInt16)((FPU_control & ~0x0010) | (value ? 0x0010 : 0));
        }
        /// <summary>
        /// FPU Precision Mask
        /// </summary>
        public bool FPU_PM
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (FPU_control & 0x0020) != 0;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => FPU_control = (UInt16)((FPU_control & ~0x0020) | (value ? 0x0020 : 0));
        }
        /// <summary>
        /// FPU Interrupt Enable Mask
        /// </summary>
        public bool FPU_IEM
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (FPU_control & 0x0080) != 0;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => FPU_control = (UInt16)((FPU_control & ~0x0080) | (value ? 0x0080 : 0));
        }
        /// <summary>
        /// FPU Precision Control
        /// </summary>
        public byte FPU_PC
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (byte)((FPU_control >> 8) & 3);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => FPU_control = (UInt16)((FPU_control & ~0x300) | ((value & 3) << 8));
        }
        /// <summary>
        /// FPU Rounding Control
        /// </summary>
        public byte FPU_RC
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (byte)((FPU_control >> 10) & 3);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => FPU_control = (UInt16)((FPU_control & ~0xc00) | ((value & 3) << 10));
        }
        /// <summary>
        /// FPU Infinity Control
        /// </summary>
        public bool FPU_IC
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (FPU_control & 0x1000) != 0;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => FPU_control = (UInt16)((FPU_control & ~0x1000) | (value ? 0x1000 : 0));
        }

        /// <summary>
        /// FPU Invalid Operation Exception
        /// </summary>
        public bool FPU_I
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (FPU_status & 0x0001) != 0;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => FPU_status = (UInt16)((FPU_status & ~0x0001) | (value ? 0x0001 : 0));
        }
        /// <summary>
        /// FPU Denormalized Exception
        /// </summary>
        public bool FPU_D
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (FPU_status & 0x0002) != 0;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => FPU_status = (UInt16)((FPU_status & ~0x0002) | (value ? 0x0002 : 0));
        }
        /// <summary>
        /// FPU Zero Divide Exception
        /// </summary>
        public bool FPU_Z
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (FPU_status & 0x0004) != 0;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => FPU_status = (UInt16)((FPU_status & ~0x0004) | (value ? 0x0004 : 0));
        }
        /// <summary>
        /// FPU Overflow Exception
        /// </summary>
        public bool FPU_O
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (FPU_status & 0x0008) != 0;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => FPU_status = (UInt16)((FPU_status & ~0x0008) | (value ? 0x0008 : 0));
        }
        /// <summary>
        /// FPU Underflow Exception
        /// </summary>
        public bool FPU_U
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (FPU_status & 0x0010) != 0;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => FPU_status = (UInt16)((FPU_status & ~0x0010) | (value ? 0x0010 : 0));
        }
        /// <summary>
        /// FPU Precision Exception
        /// </summary>
        public bool FPU_P
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (FPU_status & 0x0020) != 0;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => FPU_status = (UInt16)((FPU_status & ~0x0020) | (value ? 0x0020 : 0));
        }
        /// <summary>
        /// FPU Stack Fault Exception
        /// </summary>
        public bool FPU_SF
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (FPU_status & 0x0040) != 0;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => FPU_status = (UInt16)((FPU_status & ~0x0040) | (value ? 0x0040 : 0));
        }
        /// <summary>
        /// FPU Interrupt Request
        /// </summary>
        public bool FPU_IR
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (FPU_status & 0x0080) != 0;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => FPU_status = (UInt16)((FPU_status & ~0x0080) | (value ? 0x0080 : 0));
        }
        /// <summary>
        /// FPU Busy
        /// </summary>
        public bool FPU_B
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (FPU_status & 0x8000) != 0;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => FPU_status = (UInt16)((FPU_status & ~0x8000) | (value ? 0x8000 : 0));
        }
        /// <summary>
        /// FPU Condition 0
        /// </summary>
        public bool FPU_C0
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (FPU_status & 0x0100) != 0;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => FPU_status = (UInt16)((FPU_status & ~0x0100) | (value ? 0x0100 : 0));
        }
        /// <summary>
        /// FPU Condition 1
        /// </summary>
        public bool FPU_C1
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (FPU_status & 0x0200) != 0;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => FPU_status = (UInt16)((FPU_status & ~0x0200) | (value ? 0x0200 : 0));
        }
        /// <summary>
        /// FPU Condition 2
        /// </summary>
        public bool FPU_C2
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (FPU_status & 0x0400) != 0;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => FPU_status = (UInt16)((FPU_status & ~0x0400) | (value ? 0x0400 : 0));
        }
        /// <summary>
        /// FPU Top of Stack
        /// </summary>
        public byte FPU_TOP
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (byte)((FPU_status >> 11) & 7);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => FPU_status = (UInt16)(FPU_status & ~0x3800 | ((value & 7) << 11));
        }
        /// <summary>
        /// FPU Condition 3
        /// </summary>
        public bool FPU_C3
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (FPU_status & 0x4000) != 0;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ST(int num, double value)
        {
            num = (FPU_TOP + num) & 7;
            FPURegisters[num] = FPU_PC == 0 ? (float)value : value; // store value, accounting for precision control flag
            FPU_tag = (UInt16)((FPU_tag & ~(3 << (num * 2))) | (ComputeFPUTag(value) << (num * 2)));
        }
        /// <summary>
        /// Gets the ST register's tag
        /// </summary>
        /// <param name="num">the ordinal number of the ST register (0 for ST0)</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte ST_Tag(int num)
        {
            num = (FPU_TOP + num) & 7;
            return (byte)((FPU_tag >> (num * 2)) & 3);
        }
        /// <summary>
        /// Sets the ST register's tag to <see cref="FPU_Tag_empty"/>
        /// </summary>
        /// <param name="num">the ordinal number of the ST register (0 for ST0)</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ST_Free(int num)
        {
            num = (FPU_TOP + num) & 7;
            FPU_tag = (UInt16)(FPU_tag | (3 << (num * 2)));
        }

        /// <summary>
        /// Gets the ST register's value
        /// </summary>
        /// <param name="num">the ordinal number of the ST register (0 for ST0)</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double ST(UInt64 num) => ST((int)num);
        /// <summary>
        /// Sets the ST register's value
        /// </summary>
        /// <param name="num">the ordinal number of the ST register (0 for ST0)</param>
        /// <param name="value">the value to set</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ST(UInt64 num, double value) => ST((int)num, value);
        /// <summary>
        /// Gets the ST register's tag
        /// </summary>
        /// <param name="num">the ordinal number of the ST register (0 for ST0)</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte ST_Tag(UInt64 num) => ST_Tag((int)num);
        /// <summary>
        /// Sets the ST register's tag to <see cref="FPU_Tag_empty"/>
        /// </summary>
        /// <param name="num">the ordinal number of the ST register (0 for ST0)</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ST_Free(UInt64 num) => ST_Free((int)num);

        public double ST0 { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => ST(0); [MethodImpl(MethodImplOptions.AggressiveInlining)] set => ST(0, value); }
        public double ST1 { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => ST(1); [MethodImpl(MethodImplOptions.AggressiveInlining)] set => ST(1, value); }
        public double ST2 { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => ST(2); [MethodImpl(MethodImplOptions.AggressiveInlining)] set => ST(2, value); }
        public double ST3 { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => ST(3); [MethodImpl(MethodImplOptions.AggressiveInlining)] set => ST(3, value); }
        public double ST4 { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => ST(4); [MethodImpl(MethodImplOptions.AggressiveInlining)] set => ST(4, value); }
        public double ST5 { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => ST(5); [MethodImpl(MethodImplOptions.AggressiveInlining)] set => ST(5, value); }
        public double ST6 { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => ST(6); [MethodImpl(MethodImplOptions.AggressiveInlining)] set => ST(6, value); }
        public double ST7 { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => ST(7); [MethodImpl(MethodImplOptions.AggressiveInlining)] set => ST(7, value); }

        public ZMMRegister ZMM0 { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => ZMMRegisters[0]; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => ZMMRegisters[0] = value; }
        public ZMMRegister ZMM1 { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => ZMMRegisters[1]; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => ZMMRegisters[1] = value; }
        public ZMMRegister ZMM2 { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => ZMMRegisters[2]; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => ZMMRegisters[2] = value; }
        public ZMMRegister ZMM3 { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => ZMMRegisters[3]; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => ZMMRegisters[3] = value; }
        public ZMMRegister ZMM4 { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => ZMMRegisters[4]; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => ZMMRegisters[4] = value; }
        public ZMMRegister ZMM5 { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => ZMMRegisters[5]; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => ZMMRegisters[5] = value; }
        public ZMMRegister ZMM6 { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => ZMMRegisters[6]; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => ZMMRegisters[6] = value; }
        public ZMMRegister ZMM7 { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => ZMMRegisters[7]; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => ZMMRegisters[7] = value; }
        public ZMMRegister ZMM8 { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => ZMMRegisters[8]; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => ZMMRegisters[8] = value; }
        public ZMMRegister ZMM9 { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => ZMMRegisters[9]; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => ZMMRegisters[9] = value; }
        public ZMMRegister ZMM10 { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => ZMMRegisters[10]; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => ZMMRegisters[10] = value; }
        public ZMMRegister ZMM11 { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => ZMMRegisters[11]; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => ZMMRegisters[11] = value; }
        public ZMMRegister ZMM12 { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => ZMMRegisters[12]; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => ZMMRegisters[12] = value; }
        public ZMMRegister ZMM13 { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => ZMMRegisters[13]; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => ZMMRegisters[13] = value; }
        public ZMMRegister ZMM14 { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => ZMMRegisters[14]; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => ZMMRegisters[14] = value; }
        public ZMMRegister ZMM15 { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => ZMMRegisters[15]; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => ZMMRegisters[15] = value; }
        public ZMMRegister ZMM16 { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => ZMMRegisters[16]; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => ZMMRegisters[16] = value; }
        public ZMMRegister ZMM17 { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => ZMMRegisters[17]; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => ZMMRegisters[17] = value; }
        public ZMMRegister ZMM18 { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => ZMMRegisters[18]; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => ZMMRegisters[18] = value; }
        public ZMMRegister ZMM19 { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => ZMMRegisters[19]; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => ZMMRegisters[19] = value; }
        public ZMMRegister ZMM20 { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => ZMMRegisters[20]; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => ZMMRegisters[20] = value; }
        public ZMMRegister ZMM21 { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => ZMMRegisters[21]; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => ZMMRegisters[21] = value; }
        public ZMMRegister ZMM22 { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => ZMMRegisters[22]; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => ZMMRegisters[22] = value; }
        public ZMMRegister ZMM23 { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => ZMMRegisters[23]; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => ZMMRegisters[23] = value; }
        public ZMMRegister ZMM24 { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => ZMMRegisters[24]; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => ZMMRegisters[24] = value; }
        public ZMMRegister ZMM25 { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => ZMMRegisters[25]; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => ZMMRegisters[25] = value; }
        public ZMMRegister ZMM26 { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => ZMMRegisters[26]; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => ZMMRegisters[26] = value; }
        public ZMMRegister ZMM27 { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => ZMMRegisters[27]; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => ZMMRegisters[27] = value; }
        public ZMMRegister ZMM28 { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => ZMMRegisters[28]; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => ZMMRegisters[28] = value; }
        public ZMMRegister ZMM29 { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => ZMMRegisters[29]; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => ZMMRegisters[29] = value; }
        public ZMMRegister ZMM30 { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => ZMMRegisters[30]; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => ZMMRegisters[30] = value; }
        public ZMMRegister ZMM31 { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => ZMMRegisters[31]; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => ZMMRegisters[31] = value; }
    }
}
