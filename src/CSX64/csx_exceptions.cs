using System;

namespace CSX64
{
	/// <summary>
	/// Exception type thrown when attempting to use an object of incompatible version.
	/// </summary>
	public class VersionError : ArgumentException
	{
		public VersionError(string msg) : base(msg) { }
		public VersionError(string msg, Exception inner) : base(msg, inner) { }
	}
	/// <summary>
	/// Exception type thrown when attempting to use an object of the incorrect type
	/// </summary>
	public class TypeError : ArgumentException
	{
		public TypeError(string msg) : base(msg) { }
		public TypeError(string msg, Exception inner) : base(msg, inner) { }
	}

	/// <summary>
	/// Exception type thrown when attempting to use a dirty object
	/// </summary>
	public class DirtyError : ArgumentException
	{
		public DirtyError(string msg) : base(msg) { }
		public DirtyError(string msg, Exception inner) : base(msg, inner) { }
	}
	/// <summary>
	/// Exception type thrown when attempting to use an empty object
	/// </summary>
	public class EmptyError : ArgumentException
	{
		public EmptyError(string msg) : base(msg) { }
		public EmptyError(string msg, Exception inner) : base(msg, inner) { }
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
