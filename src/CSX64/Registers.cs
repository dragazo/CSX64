using System;

// -- Interface -- //

namespace CSX64
{
    public partial class Computer
    {
        public UInt64 RFLAGS { get => Flags.RFLAGS; set => Flags.RFLAGS = value; }
        public UInt32 EFLAGS { get => Flags.EFLAGS; set => Flags.EFLAGS = value; }
        public UInt16 FLAGS { get => Flags.FLAGS; set => Flags.FLAGS = value; }

        public bool CF { get => Flags.CF; set => Flags.CF = value; }
        public bool PF { get => Flags.PF; set => Flags.PF = value; }
        public bool AF { get => Flags.AF; set => Flags.AF = value; }
        public bool ZF { get => Flags.ZF; set => Flags.ZF = value; }
        public bool SF { get => Flags.SF; set => Flags.SF = value; }
        public bool TF { get => Flags.TF; set => Flags.TF = value; }
        public bool IF { get => Flags.IF; set => Flags.IF = value; }
        public bool DF { get => Flags.DF; set => Flags.DF = value; }
        public bool OF { get => Flags.OF; set => Flags.OF = value; }

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
