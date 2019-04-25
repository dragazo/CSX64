using System;

namespace CSX64
{
	/// <summary>
	/// exception type thrown when attempting to use an invalid executable
	/// </summary>
	public class ExecutableFormatError : FormatException
	{
		public ExecutableFormatError(string msg) : base(msg) { }
		public ExecutableFormatError(string msg, Exception inner) : base(msg, inner) { }
	}

	/// <summary>
	/// exception type thrown when program code attempts to violate memory requirements
	/// </summary>
	public class MemoryAllocException : OutOfMemoryException
	{
		public MemoryAllocException(string msg) : base(msg) { }
		public MemoryAllocException(string msg, Exception inner) : base(msg, inner) { }
	}

	/// <summary>
	/// exception type thrown when IFileWrapper permissions are violated
	/// </summary>
	public class FileWrapperPermissionsException : InvalidOperationException
	{
		public FileWrapperPermissionsException(string msg) : base(msg) { }
		public FileWrapperPermissionsException(string msg, Exception inner) : base(msg, inner) { }
	}
}
