using System;
using System.IO;

/* -- ISSUES --
c# doesn't offer binary file open directly - only through the BinaryReader/BinaryWriter classes, which woulcn't allow for read/write.
because of this, sys_open currently is not affected by the OpenFlags.binary flag, which may be a problem later...
*/

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
            IFileWrapper fd = FileDescriptors[fd_index];
            if (fd == null) { Terminate(ErrorCode.FDNotInUse); return false; }

            // make sure we can read from it
            if (!fd.CanRead()) { Terminate(ErrorCode.FilePermissions); return false; }

            // make sure we're in bounds
            if (RCX >= MemorySize || RDX >= MemorySize || RCX + RDX > MemorySize) { Terminate(ErrorCode.OutOfBounds); return false; }
            // make sure we're not in the readonly segment
            if (RCX < ReadonlyBarrier) { Terminate(ErrorCode.AccessViolation); return false; }

            // read from the file
            try
            {
                Int64 n = fd.Read(Memory, (int)RCX, (int)RDX);

                // if we got nothing but it's interactive
                if (n == 0 && fd.IsInteractive())
                {
                    --RIP;                // await further data by repeating the syscall
                    SuspendedRead = true; // suspend execution until there's more data
                }
                // otherwise success - return num chars read from file
                else RAX = (UInt64)n;
            }
            // errors are failures - return -1
            catch (Exception) { RAX = ~(UInt64)0; }

            return true;
        }
        private bool Sys_Write()
        {
            // get fd index
            UInt64 fd_index = RBX;
            if (fd_index >= (UInt64)FDCount) { Terminate(ErrorCode.OutOfBounds); return false; }

            // get fd
            IFileWrapper fd = FileDescriptors[fd_index];
            if (fd == null) { Terminate(ErrorCode.FDNotInUse); return false; }

            // make sure we can write
            if (!fd.CanWrite()) { Terminate(ErrorCode.FilePermissions); return false; }

            // make sure we're in bounds
            if (RCX >= MemorySize || RDX >= MemorySize || RCX + RDX > MemorySize) { Terminate(ErrorCode.OutOfBounds); return false; }

            // attempt to write from memory to the file - success = num written, fail = -1
            try { RAX = (UInt64)fd.Write(Memory, (int)RCX, (int)RDX); }
            catch (Exception) { RAX = ~(UInt64)0; }

            return true;
        }

        private bool Sys_Open()
        {
			// make sure we're allowed to do this
			if (!FSF) { Terminate(ErrorCode.FSDisabled); return false; }

            // get an available file descriptor
            int fd_index = FindAvailableFD();
			if (fd_index < 0) { RAX = ~(UInt64)0; return true; }

            // get path
            if (!GetCString(RBX, out string path)) return false;

            int raw_flags = (int)RCX; // flags provided by user
            FileAccess file_access;   // file access for c#
            FileMode file_mode;       // file mode for c#

            // alias permissions flags for convenience
            bool read = (raw_flags & (int)OpenFlags.read) != 0;
            bool write = (raw_flags & (int)OpenFlags.write) != 0;
			bool create = (raw_flags & (int)OpenFlags.create) != 0;
			bool temp = (raw_flags & (int)OpenFlags.temp) != 0;
			bool trunc = (raw_flags & (int)OpenFlags.trunc) != 0;
			bool append = (raw_flags & (int)OpenFlags.append) != 0;

			// temp files not currently supported (no platform-independent way to pull it off)
			if (temp) { RAX = ~(UInt64)0; return true; }

			// process raw flags - i know this could be shortened, but it's far more readable this way
			if (read && write)
			{
				file_access = FileAccess.ReadWrite;

				if (create && trunc) file_mode = FileMode.Create;
				else if (create) file_mode = FileMode.OpenOrCreate;
				else if (trunc) file_mode = FileMode.Truncate;
				else file_mode = FileMode.Open;

				// does opening in append mode for read/write even make sense? no c# option to do it, anyway
				if (append) { RAX = ~(UInt64)0; return true; }
			}
			else if (read)
			{
				file_access = FileAccess.Read;

				if (create && trunc) file_mode = FileMode.Create;
				else if (create) file_mode = FileMode.OpenOrCreate;
				else if (trunc) file_mode = FileMode.Truncate;
				else file_mode = FileMode.Open;

				if (append) { RAX = ~(UInt64)0; return true; }
			}
			else if (write)
			{
				file_access = FileAccess.Write;

				if (append) file_mode = FileMode.Append;
				else if (create && trunc) file_mode = FileMode.Create;
				else if (create) file_mode = FileMode.OpenOrCreate;
				else if (trunc) file_mode = FileMode.Truncate;
				else file_mode = FileMode.Open;
			}
			else { RAX = ~(UInt64)0; return true; }
            
            // open the file
            FileStream f = null;
            try { f = new FileStream(path, file_mode, file_access); }
			catch (Exception) { RAX = ~(UInt64)0; return true; }

            // store in the file descriptor
            FileDescriptors[fd_index] = new BasicFileWrapper(f, true, false, read, write, true);
            RAX = (UInt64)fd_index;

			return true;
        }
        private bool Sys_Close()
        {
            // get fd index
            int fd_index = (int)RBX;
            if (fd_index >= FDCount) { Terminate(ErrorCode.OutOfBounds); return false; }

            CloseFileWrapper(fd_index);

            RAX = 0;
            return true;
        }

        private bool Sys_Lseek()
        {
            // get fd index
            UInt64 fd_index = RBX;
            if (fd_index >= (UInt64)FDCount) { Terminate(ErrorCode.OutOfBounds); return false; }

            // get fd
            IFileWrapper fd = FileDescriptors[fd_index];
            if (fd == null) { Terminate(ErrorCode.FDNotInUse); return false; }

            // make sure it can seek
            if (!fd.CanSeek()) { Terminate(ErrorCode.FilePermissions); return false; }

            int raw_mode = (int)RDX;
            SeekOrigin seek_origin;

            // process raw mode
            if (raw_mode == (int)SeekMode.set) seek_origin = SeekOrigin.Begin;
            else if (raw_mode == (int)SeekMode.cur) seek_origin = SeekOrigin.Current;
            else if (raw_mode == (int)SeekMode.end) seek_origin = SeekOrigin.End;
            // otherwise unknown seek mode - return -1
            else { RAX = ~(UInt64)0; return true; }

            // attempt the seek
            try { RAX = (UInt64)fd.Seek((Int64)RCX, seek_origin); }
            catch (Exception) { RAX = ~(UInt64)0; }

            return true;
        }

        private bool Sys_Brk()
        {
            // special request of 0 returns current break
            if (RBX == 0) RAX = MemorySize;
            // if the request is too high or goes below init size, don't do it
            else if (RBX > MaxMemory || RBX < MinMemory) RAX = ~(UInt64)0; // RAX = -1
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

        private bool Sys_Rename()
        {
            // make sure we're allowed to do this
            if (!FSF) { Terminate(ErrorCode.FSDisabled); return false; }

            // get the paths
            if (!GetCString(RBX, out string from) || !GetCString(RCX, out string to)) return false;

            // attempt the move operation - success = 0, fail = -1
            try { File.Move(from, to); RAX = 0; }
            catch (Exception) { RAX = ~(UInt64)0; }

            return true;
        }
        private bool Sys_Unlink()
        {
            // make sure we're allowed to do this
            if (!FSF) { Terminate(ErrorCode.FSDisabled); return false; }

            // get the path
            if (!GetCString(RBX, out string path)) return false;

			// attempt the unlink operation - using delete, but it must not be a directory - success = 0, failure = -1
			try
			{
				if (!Directory.Exists(path) & File.Exists(path))
				{
					File.Delete(path); // extra exists() check is because this returns void and doesn't throw if file doesn't exist
					RAX = 0;
				}
				else RAX = ~(UInt64)0;
			}
			catch (Exception) { RAX = ~(UInt64)0; }

            return true;
        }
		
        private bool Sys_Mkdir()
        {
            // make sure we're allowed to do this
            if (!FSF) { Terminate(ErrorCode.FSDisabled); return false; }

            // get the path
            if (!GetCString(RBX, out string path)) return false;

            // attempt the mkdir operation - success = 0, failure = -1
            try { Directory.CreateDirectory(path); RAX = 0; }
            catch (Exception) { RAX = ~(UInt64)0; }

            return true;
        }
        private bool Sys_Rmdir()
        {
            // make sure we're allowed to do this
            if (!FSF) { Terminate(ErrorCode.FSDisabled); return false; }

            // get the path
            if (!GetCString(RBX, out string path)) return false;

            // attempt the rmdir - using remove, but it must be a directory - success = 0, failure = -1
            try { Directory.Delete(path); RAX = 0; }
            catch (Exception) { RAX = ~(UInt64)0; }

            return true;
        }
    }
}
