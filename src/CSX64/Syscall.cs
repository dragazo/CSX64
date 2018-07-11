using System;
using System.IO;

// -- Syscall -- //

namespace CSX64
{
    public partial class Computer
    {
        private bool Sys_Read()
        {
            // get fd index
            UInt64 fd_index = RBX;
            if (fd_index >= (UInt64)FDCount) { Terminate(ErrorCode.OutOfBounds); return false; }

            // get fd
            FileDescriptor fd = FileDescriptors[fd_index];
            if (!fd.InUse) { Terminate(ErrorCode.FDNotInUse); return false; }

            // attempt to read from the file into memory
            try
            {
                // make sure we're not in the readonly segment
                if (RCX < ReadonlyBarrier) { Terminate(ErrorCode.AccessViolation); return false; }

                // read from the file
                int n = fd.BaseStream.Read(Memory, (int)RCX, (int)RDX);

                // if we got nothing but it's interactive
                if (n == 0 && fd.Interactive)
                {
                    --RIP;                // await further data by repeating the syscall
                    SuspendedRead = true; // suspend execution until there's more data
                }
                // otherwise return num chars read from file
                else
                {
                    RAX = (UInt64)n; // count in RAX
                }

                return true;
            }
            catch (Exception) { Terminate(ErrorCode.IOFailure); return false; }
        }
        private bool Sys_Write()
        {
            // get fd index
            UInt64 fd_index = RBX;
            if (fd_index >= (UInt64)FDCount) { Terminate(ErrorCode.OutOfBounds); return false; }

            // get fd
            FileDescriptor fd = FileDescriptors[fd_index];
            if (!fd.InUse) { Terminate(ErrorCode.FDNotInUse); return false; }
            
            // attempt to write from memory to the file
            try { fd.BaseStream.Write(Memory, (int)RCX, (int)RDX); return true; }
            catch (Exception) { Terminate(ErrorCode.IOFailure); return false; }
        }

        private bool Sys_Open()
        {
            // make sure we're allowed to do this
            if (!FSF) { Terminate(ErrorCode.FSDisabled); return false; }

            // get an available file descriptor
            FileDescriptor fd = FindAvailableFD(out UInt64 fd_index);
            if (fd == null) { Terminate(ErrorCode.InsufficientFDs); return false; }

            // get path
            if (!GetCString(RBX, out string path)) return false;

            // attempt to open the file
            try
            {
                FileStream f = new FileStream(path, (FileMode)RCX, (FileAccess)RDX);

                // store in the file descriptor
                fd.Open(f, true, false);
                // return file descriptor index in RAX
                RAX = fd_index;
            }
            catch (Exception) { Terminate(ErrorCode.IOFailure); return false; }

            return true;
        }
        private bool Sys_Close()
        {
            // get fd index
            UInt64 fd_index = RBX;
            if (fd_index >= (UInt64)FDCount) { Terminate(ErrorCode.OutOfBounds); return false; }

            // get fd
            FileDescriptor fd = FileDescriptors[fd_index];

            // close the file
            if (!fd.Close()) { Terminate(ErrorCode.IOFailure); return false; }

            return true;
        }
        private bool Sys_Flush()
        {
            // get fd index
            UInt64 fd_index = RBX;
            if (fd_index >= (UInt64)FDCount) { Terminate(ErrorCode.OutOfBounds); return false; }

            // get fd
            FileDescriptor fd = FileDescriptors[fd_index];
            if (!fd.InUse) { Terminate(ErrorCode.FDNotInUse); return false; }

            // flush the file
            if (!fd.Flush()) { Terminate(ErrorCode.IOFailure); return false; }

            return true;
        }

        private bool Sys_Seek()
        {
            // get fd index
            UInt64 fd_index = RBX;
            if (fd_index >= (UInt64)FDCount) { Terminate(ErrorCode.OutOfBounds); return false; }

            // get fd
            FileDescriptor fd = FileDescriptors[fd_index];
            if (!fd.InUse) { Terminate(ErrorCode.FDNotInUse); return false; }

            // attempt to seek in the file
            if (!fd.Seek((long)RCX, (SeekOrigin)RDX)) { Terminate(ErrorCode.IOFailure); return false; }

            return true;
        }
        private bool Sys_Tell()
        {
            // get fd index
            UInt64 fd_index = RBX;
            if (fd_index >= (UInt64)FDCount) { Terminate(ErrorCode.OutOfBounds); return false; }

            // get fd
            FileDescriptor fd = FileDescriptors[fd_index];
            if (!fd.InUse) { Terminate(ErrorCode.FDNotInUse); return false; }

            // attempt to get current position in file
            if (!fd.Tell(out long pos)) { Terminate(ErrorCode.IOFailure); return false; }

            // store position in RAX
            RAX = (UInt64)pos;
            
            return true;
        }

        private bool Sys_Move()
        {
            // make sure we're allowed to do this
            if (!FSF) { Terminate(ErrorCode.FSDisabled); return false; }

            // get the paths
            if (!GetCString(RBX, out string from) || !GetCString(RCX, out string to)) return false;

            // attempt the move operation
            try { File.Move(from, to); return true; }
            catch (Exception) { Terminate(ErrorCode.IOFailure); return false; }
        }
        private bool Sys_Remove()
        {
            // make sure we're allowed to do this
            if (!FSF) { Terminate(ErrorCode.FSDisabled); return false; }

            // get the path
            if (!GetCString(RBX, out string path)) return false;

            // attempt the move operation
            try { File.Delete(path); return true; }
            catch (Exception) { Terminate(ErrorCode.IOFailure); return false; }
        }
        
        private bool Sys_Mkdir()
        {
            // make sure we're allowed to do this
            if (!FSF) { Terminate(ErrorCode.FSDisabled); return false; }

            // get the path
            if (!GetCString(RBX, out string path)) return false;
            
            // attempt the move operation
            try { Directory.CreateDirectory(path); return true; }
            catch (Exception) { Terminate(ErrorCode.IOFailure); return false; }
        }
        private bool Sys_Rmdir()
        {
            // make sure we're allowed to do this
            if (!FSF) { Terminate(ErrorCode.FSDisabled); return false; }

            // get the path
            if (!GetCString(RBX, out string path)) return false;

            // attempt the move operation
            try { Directory.Delete(path, RecursiveRmdir); return true; }
            catch (Exception) { Terminate(ErrorCode.IOFailure); return false; }
        }

        private bool Sys_Brk()
        {
            // special request of 0 returns current break
            if (RBX == 0) RAX = MemorySize;
            // if the request is too high or goes below init size, don't do it
            else if (RBX > MaxMemory || RBX < InitMemorySize) RAX = UInt32.MaxValue; // RAX = -1
            // otherwise perform the reallocation
            else
            {
                byte[] newmem = new byte[RBX];
                Memory.CopyTo(newmem, 0);
                Memory = newmem;
                RAX = 0;
            }

            return true;
        }
    }
}
