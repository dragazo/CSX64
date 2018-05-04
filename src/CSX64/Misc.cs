using System;

// -- Misc -- //

namespace CSX64
{
    public partial class Computer
    {
        static Computer()
        {
            // create definitions for all the syscall codes
            foreach (SyscallCode item in Enum.GetValues(typeof(SyscallCode)))
                Assembly.DefineSymbol($"sys_{item.ToString().ToLower()}", (UInt64)item);

            // create definitions for all the error codes
            foreach (ErrorCode item in Enum.GetValues(typeof(ErrorCode)))
                Assembly.DefineSymbol($"err_{item.ToString().ToLower()}", (UInt64)item);
        }

        // ------------------------------------------

        ~Computer()
        {
            Dispose(false);
        }

        /// <summary>
        /// Disposes of unmanaged resources
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this); // ensure CLR doesn't call destructor for this object as well
        }

        /// <summary>
        /// Marks if this object has already been disposed. DO NOT MODIFY
        /// </summary>
        private bool _Disposed = false;
        /// <summary>
        /// Relaeses all the resources used by this object
        /// </summary>
        /// <param name="disposing">if managed resources should be released</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_Disposed)
            {
                if (disposing)
                {
                    CloseFiles(); // close all the file descriptors
                }

                _Disposed = true;
            }
        }
    }
}