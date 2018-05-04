using System;

// -- Data -- //

namespace CSX64
{
    public partial class Computer
    {
        protected Register[] Registers = new Register[16];
        protected FlagsRegister Flags = new FlagsRegister();

        protected byte[] Memory = null;

        protected FileDescriptor[] FileDescriptors = new FileDescriptor[NFileDescriptors];

        protected readonly Random Rand = new Random();
    }
}