using System;

// -- Interface -- //

namespace CSX64
{
    public partial class Computer
    {
        public const UInt64 CF_Mask = 0x0001;
        public const UInt64 RESERVED1_Mask = 0x0002;
        public const UInt64 PF_Mask = 0x0004;
        public const UInt64 RESERVED2_Mask = 0x0008;
        public const UInt64 AF_Mask = 0x0010;
        public const UInt64 RESERVED3_Mask = 0x0020;
        public const UInt64 ZF_Mask = 0x0040;
        public const UInt64 SF_Mask = 0x0080;
        public const UInt64 TF_Mask = 0x0100;
        public const UInt64 IF_Mask = 0x0200;
        public const UInt64 DF_Mask = 0x0400;
        public const UInt64 OF_Mask = 0x0800;

        public const UInt64 FSF_Mask = 0x000_0001_0000_0000;

        // --------------------------------------------

        /// <summary>
        /// The full 64-bit flags data
        /// </summary>
        public UInt64 RFLAGS = 0;
        /// <summary>
        /// The lower 32 flags
        /// </summary>
        public UInt32 EFLAGS
        {
            get => (UInt32)RFLAGS;
            set => RFLAGS = (RFLAGS & ~0xffffffff) | value;
        }
        /// <summary>
        /// The lower 16 flags
        /// </summary>
        public UInt16 FLAGS
        {
            get => (UInt16)RFLAGS;
            set => RFLAGS = (RFLAGS & ~0xfffful) | value;
        }

        /// <summary>
        /// The Carry flag
        /// </summary>
        public bool CF
        {
            get => (RFLAGS & CF_Mask) != 0;
            set => RFLAGS = (RFLAGS & ~CF_Mask) | (value ? CF_Mask : 0);
        }

        /// <summary>
        /// The (even) Parity flag
        /// </summary>
        public bool PF
        {
            get => (RFLAGS & PF_Mask) != 0;
            set => RFLAGS = (RFLAGS & ~PF_Mask) | (value ? PF_Mask : 0);
        }

        /// <summary>
        /// The Adjust flag
        /// </summary>
        public bool AF
        {
            get => (RFLAGS & AF_Mask) != 0;
            set => RFLAGS = (RFLAGS & ~AF_Mask) | (value ? AF_Mask : 0);
        }

        /// <summary>
        /// The Zero flag
        /// </summary>
        public bool ZF
        {
            get => (RFLAGS & ZF_Mask) != 0;
            set => RFLAGS = (RFLAGS & ~ZF_Mask) | (value ? ZF_Mask : 0);
        }

        /// <summary>
        /// The Sign flag
        /// </summary>
        public bool SF
        {
            get => (RFLAGS & SF_Mask) != 0;
            set => RFLAGS = (RFLAGS & ~SF_Mask) | (value ? SF_Mask : 0);
        }

        /// <summary>
        /// The Trap flag (single step)
        /// </summary>
        public bool TF
        {
            get => (RFLAGS & TF_Mask) != 0;
            set => RFLAGS = (RFLAGS & ~TF_Mask) | (value ? TF_Mask : 0);
        }

        /// <summary>
        /// The Interrupt enabled flag
        /// </summary>
        public bool IF
        {
            get => (RFLAGS & IF_Mask) != 0;
            set => RFLAGS = (RFLAGS & ~IF_Mask) | (value ? IF_Mask : 0);
        }

        /// <summary>
        /// The Direction flag
        /// </summary>
        public bool DF
        {
            get => (RFLAGS & DF_Mask) != 0;
            set => RFLAGS = (RFLAGS & ~DF_Mask) | (value ? DF_Mask : 0);
        }

        /// <summary>
        /// The Overflow flag
        /// </summary>
        public bool OF
        {
            get => (RFLAGS & OF_Mask) != 0;
            set => RFLAGS = (RFLAGS & ~OF_Mask) | (value ? OF_Mask : 0);
        }

        public bool a { get => !CF && !ZF; }
        public bool ae { get => !CF; }
        public bool b { get => CF; }
        public bool be { get => CF || ZF; }

        public bool g { get => !ZF && SF == OF; }
        public bool ge { get => SF == OF; }
        public bool l { get => SF != OF; }
        public bool le { get => ZF || SF != OF; }

        /// <summary>
        /// The flag that indicates that we're allowed to run commands that may potentially modify the file system
        /// </summary>
        public bool FSF
        {
            get => (RFLAGS & FSF_Mask) != 0;
            set => RFLAGS = (RFLAGS & ~FSF_Mask) | (value ? FSF_Mask : 0);
        }

        // --------------------------------------------

        private Register[] Registers = new Register[16];

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
    }
}
