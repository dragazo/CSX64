using System;
using System.IO;

// -- Syscall -- //

namespace CSX64
{
    public partial class Computer
    {
        /// <summary>
        /// Attempts to read a series of bytes from the file and store them in memory
        /// </summary>
        /// <param name="fd">the file descriptor to use</param>
        /// <param name="pos">the position in memory to store the read data</param>
        /// <param name="count">the number of bytes to read</param>
        private bool Sys_Read()
        {
            // get fd index
            UInt64 fd_index = Registers[1].x64;
            if (fd_index >= NFileDescriptors) { Terminate(ErrorCode.OutOfBounds); return false; }

            // get fd
            FileDescriptor fd = FileDescriptors[fd_index];
            if (!fd.InUse) { Terminate(ErrorCode.FDNotInUse); return false; }

            // attempt to read from the file into memory
            try
            {
                // read from the file
                int n = fd.__Stream.Read(Memory, (int)Registers[2].x64, (int)Registers[3].x64);

                // if we got nothing but it's interactive
                if (n == 0 && fd.Interactive)
                {
                    --Pos;                // await further data by repeating the syscall
                    SuspendedRead = true; // suspend execution until there's more data
                }
                // otherwise return num chars read from file
                else Flags.Z = (Registers[0].x64 = (UInt64)n) == 0;

                return true;
            }
            catch (Exception) { Terminate(ErrorCode.IOFailure); return false; }
        }
        /// <summary>
        /// Attempts to write a series of bytes from memory to the file
        /// </summary>
        /// <param name="fd">the file descriptor to use</param>
        /// <param name="pos">the position in memory of the data to write</param>
        /// <param name="count">the number of bytes to read</param>
        private bool Sys_Write()
        {
            // get fd index
            UInt64 fd_index = Registers[1].x64;
            if (fd_index >= NFileDescriptors) { Terminate(ErrorCode.OutOfBounds); return false; }

            // get fd
            FileDescriptor fd = FileDescriptors[fd_index];
            if (!fd.InUse) { Terminate(ErrorCode.FDNotInUse); return false; }

            // attempt to write from memory to the file
            try { fd.__Stream.Write(Memory, (int)Registers[2].x64, (int)Registers[3].x64); return true; }
            catch (Exception) { Terminate(ErrorCode.IOFailure); return false; }
        }

        /// <summary>
        /// Opens a file in the specified mode. Returns the file descriptor created if successful, otherwise 0.
        /// Streams opened with this method are "managed" and will be closed upon termination.
        /// </summary>
        /// <param name="path">the path of the file to open</param>
        /// <param name="mode">the mode to open the file in (see System.IO.FileAccess)"/></param>
        private bool Sys_Open()
        {
            // make sure we're allowed to do this
            if (!Flags.FileSystem) { Terminate(ErrorCode.FSDisabled); return false; }

            // get an available file descriptor
            FileDescriptor fd = FindAvailableFD(out UInt64 fd_index);
            if (fd == null) { Terminate(ErrorCode.InsufficientFDs); return false; }

            // get path
            if (!GetCString(Registers[1].x64, out string path)) return false;

            FileStream f; // resulting stream

            // attempt to open the file
            try { f = new FileStream(path, (FileMode)Registers[2].x64, (FileAccess)Registers[3].x64); }
            catch (Exception) { Terminate(ErrorCode.IOFailure); return false; }

            // store in the file descriptor
            fd.Open(f, true, false);
            // return file descriptor index
            Registers[0].x64 = fd_index;

            return true;
        }
        /// <summary>
        /// Closes a file. Returns true if successful, otherwise failes with IOFailure and returns false.
        /// Cannot be used to close an unmanaged stream set by <see cref="SetUnmanagedStream(int, Stream)"/>
        /// </summary>
        /// <param name="fd">The file descriptor to close</param>
        private bool Sys_Close()
        {
            // get fd index
            UInt64 fd_index = Registers[1].x64;
            if (fd_index >= NFileDescriptors) { Terminate(ErrorCode.OutOfBounds); return false; }

            // get fd
            FileDescriptor fd = FileDescriptors[fd_index];

            // close it (un/managed and not-in-use cases are handled internally)
            fd.Close();
            return true;
        }

        /// <summary>
        /// Flushes the stream associated with the file
        /// </summary>
        /// <param name="fd">the file descriptor to flush</param>
        private bool Sys_Flush()
        {
            // get fd index
            UInt64 fd_index = Registers[1].x64;
            if (fd_index >= NFileDescriptors) { Terminate(ErrorCode.OutOfBounds); return false; }

            // get fd
            FileDescriptor fd = FileDescriptors[fd_index];
            if (!fd.InUse) { Terminate(ErrorCode.FDNotInUse); return false; }

            // attempt to flush buffer
            try { fd.__Stream.Flush(); return true; }
            catch (Exception) { Terminate(ErrorCode.IOFailure); return false; }
        }

        /// <summary>
        /// Seeks the specified position in the file
        /// </summary>
        /// <param name="fd">the file descriptor to perform seek on</param>
        /// <param name="pos">the position to seek to</param>
        /// <param name="mode">the offset mode</param>
        private bool Sys_Seek()
        {
            // get fd index
            UInt64 fd_index = Registers[1].x64;
            if (fd_index >= NFileDescriptors) { Terminate(ErrorCode.OutOfBounds); return false; }

            // get fd
            FileDescriptor fd = FileDescriptors[fd_index];
            if (!fd.InUse) { Terminate(ErrorCode.FDNotInUse); return false; }

            // attempt to seek in the file
            try { fd.__Stream.Seek((long)Registers[2].x64, (SeekOrigin)Registers[3].x64); return true; }
            catch (Exception) { Terminate(ErrorCode.IOFailure); return false; }
        }
        /// <summary>
        /// Attempts to get the current position in the file
        /// </summary>
        /// <param name="fd">the file to get current position of</param>
        /// <param name="pos">position of stream upon success</param>
        private bool Sys_Tell()
        {
            // get fd index
            UInt64 fd_index = Registers[1].x64;
            if (fd_index >= NFileDescriptors) { Terminate(ErrorCode.OutOfBounds); return false; }

            // get fd
            FileDescriptor fd = FileDescriptors[fd_index];
            if (!fd.InUse) { Terminate(ErrorCode.FDNotInUse); return false; }

            // attempt to read from memory to the file
            try { Registers[0].x64 = (UInt64)fd.__Stream.Position; return true; }
            catch (Exception) { Terminate(ErrorCode.IOFailure); return false; }
        }

        /// <summary>
        /// Moves a file. Returns true iff successful
        /// </summary>
        /// <param name="from">the file to move</param>
        /// <param name="to">the destination path</param>
        private bool Sys_Move()
        {
            // make sure we're allowed to do this
            if (!Flags.FileSystem) { Terminate(ErrorCode.FSDisabled); return false; }

            // get the paths
            if (!GetCString(Registers[1].x64, out string from) || !GetCString(Registers[2].x64, out string to)) return false;

            // attempt the move operation
            try { File.Move(from, to); return true; }
            catch (Exception) { Terminate(ErrorCode.IOFailure); return false; }
        }
        /// <summary>
        /// Attempts to remove the specified file
        /// </summary>
        /// <param name="path">the file to remove</param>
        /// <returns></returns>
        private bool Sys_Remove()
        {
            // make sure we're allowed to do this
            if (!Flags.FileSystem) { Terminate(ErrorCode.FSDisabled); return false; }

            // get the path
            if (!GetCString(Registers[1].x64, out string path)) return false;

            // attempt the move operation
            try { File.Delete(path); return true; }
            catch (Exception) { Terminate(ErrorCode.IOFailure); return false; }
        }

        /// <summary>
        /// Attempts to make a new directory
        /// </summary>
        /// <param name="path">path to new directory</param>
        private bool Sys_Mkdir()
        {
            // make sure we're allowed to do this
            if (!Flags.FileSystem) { Terminate(ErrorCode.FSDisabled); return false; }

            // get the path
            if (!GetCString(Registers[1].x64, out string path)) return false;

            // attempt the move operation
            try { Directory.CreateDirectory(path); return true; }
            catch (Exception) { Terminate(ErrorCode.IOFailure); return false; }
        }
        /// <summary>
        /// Attempts to remove a directory
        /// </summary>
        /// <param name="path">path to directory to remove</param>
        private bool Sys_Rmdir()
        {
            // make sure we're allowed to do this
            if (!Flags.FileSystem) { Terminate(ErrorCode.FSDisabled); return false; }

            // get the path
            if (!GetCString(Registers[1].x64, out string path)) return false;

            // attempt the move operation
            try { Directory.Delete(path, RecursiveRmdir); return true; }
            catch (Exception) { Terminate(ErrorCode.IOFailure); return false; }
        }
    }
}