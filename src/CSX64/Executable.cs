using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using static CSX64.Utility;

namespace CSX64
{
	/// <summary>
	/// Represents a CSX64 executable.
	/// Executable instances are at all times either empty or filled with well-formed information (refered to collectively as "valid").
	/// Provides utilities to save and load Executables (with additional format validation built-in).
	/// </summary>
	public class Executable
	{
		// -- data -- //

		public UInt64 text_seglen { get; private set; }
		public UInt64 rodata_seglen { get; private set; }
		public UInt64 data_seglen { get; private set; }
		public UInt64 bss_seglen { get; private set; }

		/// <summary>
		/// executable contents (actually loaded into memory for execution)
		/// </summary>
		public byte[] Content { get; private set; }

		// -- construction -- //

		/// <summary>
		/// Creates an empty executable
		/// </summary>
		public Executable()
		{
			Clear();
		}

		/// <summary>
		/// Assigns this executable a value constructed from component segments.
		/// Throws <see cref="OverflowException"/> if the sum of all the segments exceeds int max.
		/// If an exception is thrown, this executable is left in the empty state.
		/// </summary>
		/// <param name="text">the text segment of the resulting executable (readonly, executable)</param>
		/// <param name="rodata">the rodata segment of the resulting executable (readonly)</param>
		/// <param name="data">the data segment of the resulting executable (read-write)</param>
		/// <param name="bsslen">the bss segment of the resulting executable (read-write, zero-initialized)</param>
		/// <exception cref="OverflowException"></exception>
		public void Construct(List<byte> text, List<byte> rodata, List<byte> data, UInt64 bsslen)
		{
			// record segment lengths
			text_seglen = (UInt64)text.Count;
			rodata_seglen = (UInt64)rodata.Count;
			data_seglen = (UInt64)data.Count;
			bss_seglen = bsslen;

			// make sure seg lengths don't overflow int (C# using int32 for indexing instead of uint64)
			if (text_seglen + rodata_seglen + data_seglen + bss_seglen > (UInt64)int.MaxValue)
			{
				Clear(); // if we throw an exception we must leave the exe in the empty state
				throw new OverflowException("Total executable length exceeds maximum size");
			}

			// resize content to holds all the segmens (except bss) - if this throws, set to empty state and rethrow
			Content = null;
			try { Content = new byte[text.Count + rodata.Count + data.Count]; }
			catch (Exception) { Clear(); throw; }

			// copy over all the segments
			text.CopyTo(Content, 0);
			rodata.CopyTo(Content, text.Count);
			data.CopyTo(Content, text.Count + rodata.Count);
		}

		// -- state -- //

		/// <summary>
		/// Returns thue iff the executable is empty (i.e. all segment lengths are zero)
		/// </summary>
		public bool Empty()
		{
			return text_seglen == 0 && rodata_seglen == 0 && data_seglen == 0 && bss_seglen == 0;
		}
		/// <summary>
		/// Changes the executable to the empty state (i.e. all segment lengths are zero)
		/// </summary>
		public void Clear()
		{
			text_seglen = rodata_seglen = data_seglen = bss_seglen = 0;
		}

		// -- access -- //

		/// <summary>
		/// Gets the length of the content array.
		/// </summary>
		public UInt64 ContentSize { get => text_seglen + rodata_seglen + data_seglen; }

		/// <summary>
		/// Returns the total size of all segments (incuding bss) (>= ContentSize)
		/// </summary>
		public UInt64 TotalSize { get => ContentSize + bss_seglen; }

		// -- IO -- //

		private static readonly byte[] header = { (byte)'C', (byte)'S', (byte)'X', (byte)'6', (byte)'4', (byte)'e', (byte)'x', (byte)'e' };

		/// <summary>
		/// Saves this executable to a file located at (path).
		/// throws <see cref="ArgumentException"/> if the executable is empty.
		/// </summary>
		/// <param name="path">the file path to save to</param>
		/// <exception cref="ArgumentException"></exception>
		public void Save(string path)
		{
			// make sure the executable is not empty
			if (Empty()) throw new ArgumentException("Attempt to save empty executable");

			using (BinaryWriter file = new BinaryWriter(File.OpenWrite(path)))
			{
				// write exe header and CSX64 version number
				file.Write(header);
				file.Write(Utility.Version);

				// write the segment lengths
				file.Write(text_seglen);
				file.Write(rodata_seglen);
				file.Write(data_seglen);
				file.Write(bss_seglen);

				// write the content of the executable
				file.Write(Content);
			}
		}
		/// <summary>
		/// Loads this executable with the content of a file located at (path).
		/// </summary>
		/// <param name="path">the file path to read from</param>
		public void Load(string path)
		{
			try
			{
				using (BinaryReader file = new BinaryReader(File.OpenRead(path)))
				{
					// read the header from the file and make sure it matches - match failure is a type error, not a format error
					if (!header.SequenceEqual(file.ReadBytes(header.Length))) throw new TypeError("File was not a CSX64 executable");

					// read the version number from the file and make sure it matches - match failure is a version error, not a format error
					if (Utility.Version != file.ReadUInt64()) throw new VersionError("Executable was from an incompatible version of CSX64");

					// read the segment lengths
					UInt64[] seg = new UInt64[4];
					for (int i = 0; i < seg.Length; ++i)
					{
						seg[i] = file.ReadUInt64(); // read each seg length - each must not exceed int32 max
						if (seg[i] > int.MaxValue) goto err;
					}

					// make sure seg lengths don't overflow int (C# uses int32 instead of uint64 for indexing)
					if (seg[0] + seg[1] + seg[2] + seg[3] > (UInt64)int.MaxValue) goto err;

					// apply loaded segment lengths
					text_seglen = seg[0];
					rodata_seglen = seg[1];
					data_seglen = seg[2];
					bss_seglen = seg[3];

					// make sure the file is the correct size
					if ((UInt64)file.BaseStream.Length != 48ul + text_seglen + rodata_seglen + data_seglen) goto err;

					// read the content - make sure we got everything
					Content = file.ReadBytes((int)(text_seglen + rodata_seglen + data_seglen));
					if ((UInt64)Content.Length != text_seglen + rodata_seglen + data_seglen) goto err;

					return;
				}
			}
			// if we get any exceptions (e.g. io error), catch, clear, and rethrow
			catch (Exception) { Clear(); throw; }

			err:
			Clear(); // if we throw an exception we must leave the exe in the empty state
			throw new FormatException("Executable file was corrupted");
		}
	}
}
