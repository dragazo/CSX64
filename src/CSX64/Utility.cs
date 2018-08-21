using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Runtime.CompilerServices;

// -- Utility -- //

namespace CSX64
{
    public static class Utility
    {
        // -- misc utilities -- //

        /// <summary>
        /// Swaps the contents of the specified l-values
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Swap<T>(ref T a, ref T b)
        {
            T temp = a;
            a = b;
            b = temp;
        }

        /// <summary>
        /// Returns true if this value is a power of two. (zero returns false)
        /// </summary>
        /// <param name="val">the value to test</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsPowerOf2(this UInt64 val)
        {
            return val != 0 && (val & (val - 1)) == 0;
        }
        /// <summary>
        /// Returns true if this value is a power of two. (zero returns false)
        /// </summary>
        /// <param name="val">the value to test</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsPowerOf2(this UInt32 val)
        {
            return val != 0 && (val & (val - 1)) == 0;
        }

        /// <summary>
        /// Extracts 2 distinct powers of 2 from the specified value. Returns true if the value is made up of exactly two non-zero powers of 2.
        /// </summary>
        /// <param name="val">the value to process</param>
        /// <param name="a">the first (larger) power of 2</param>
        /// <param name="b">the second (smaller) power of 2</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Extract2PowersOf2(this UInt64 val, out UInt64 a, out UInt64 b)
        {
            // isolate the lowest power of 2
            b = val & (~val + 1);
            // disable the lowest power of 2
            val = val & (val - 1);

            // isolate the next lowest power of 2
            a = val & (~val + 1);
            // disable the next lowest power of 2
            val = val & (val - 1);

            // if val is now zero and a and b are nonzero, we got 2 distinct powers of 2
            return val == 0 && a != 0 && b != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ExtractDouble(double val, out double exp, out double sig)
        {
            if (double.IsNaN(val))
            {
                exp = sig = double.NaN;
            }
            else if (double.IsPositiveInfinity(val))
            {
                exp = double.PositiveInfinity;
                sig = 1;
            }
            else if (double.IsNegativeInfinity(val))
            {
                exp = double.PositiveInfinity;
                sig = -1;
            }
            else
            {
                // get the raw bits
                UInt64 bits = DoubleAsUInt64(val);

                // get the raw exponent
                UInt64 raw_exp = (bits >> 52) & 0x7ff;

                // get exponent and subtract bias
                exp = (double)raw_exp - 1023;
                // get significand (m.0) and offset to 0.m
                sig = (double)(bits & 0xfffffffffffff) / (1ul << 52);

                // if it's denormalized, add 1 to exponent
                if (raw_exp == 0) exp += 1;
                // otherwise add the invisible 1 to get 1.m
                else sig += 1;

                // handle negative case
                if (val < 0) sig = -sig;
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double AssembleDouble(double exp, double sig)
        {
            return sig * Math.Pow(2, exp);
        }

        /// <summary>
        /// Returns true if the floating-point value is denormalized (including +-0)
        /// </summary>
        /// <param name="val">the value to test</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsDenorm(this double val)
        {
            // denorm has exponent field of zero
            return (DoubleAsUInt64(val) & 0x7ff0000000000000ul) == 0;
        }

        /// <summary>
        /// Gets a random UInt64 value
        /// </summary>
        /// <param name="rand">random object to use</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static UInt64 NextUInt64(this Random rand)
        {
            return ((UInt64)(UInt32)rand.Next() << 32) | (UInt32)rand.Next();
        }
        /// <summary>
        /// Gets a random boolean
        /// </summary>
        /// <param name="rand">the random object to use</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool NextBool(this Random rand)
        {
            return rand.Next(2) == 1;
        }

        // -- memory utilities -- //

        /// <summary>
        /// Writes a value to the array
        /// </summary>
        /// <param name="arr">the data to write to</param>
        /// <param name="pos">the index to begin at</param>
        /// <param name="size">the size of the value in bytes</param>
        /// <param name="val">the value to write</param>
        /// <exception cref="ArgumentException"></exception>
        public static bool Write<T>(this T arr, UInt64 pos, UInt64 size, UInt64 val) where T : IList<byte>
        {
            // make sure we're not exceeding memory bounds
            if (pos >= (UInt64)arr.Count || pos + size > (UInt64)arr.Count) return false;

            // write the value (little-endian)
            for (int i = 0; i < (int)size; ++i)
            {
                arr[(int)pos + i] = (byte)val;
                val >>= 8;
            }

            return true;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Write(this byte[] arr, UInt64 pos, UInt64 size, UInt64 val) // this template specialization is for speed
        {
            // make sure we're not exceeding memory bounds
            if (pos >= (UInt64)arr.Length || pos + size > (UInt64)arr.Length) return false;

            // write the value (little-endian)
            for (int i = 0; i < (int)size; ++i)
            {
                arr[(int)pos + i] = (byte)val;
                val >>= 8;
            }

            return true;
        }
        /// <summary>
        /// Reads a value from the array
        /// </summary>
        /// <param name="arr">the data to write to</param>
        /// <param name="pos">the index to begin at</param>
        /// <param name="size">the size of the value in bytes</param>
        /// <param name="res">the read value</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Read(this byte[] arr, UInt64 pos, UInt64 size, out UInt64 res)
        {
            // make sure we're not exceeding memory bounds
            if (pos >= (UInt64)arr.Length || pos + size > (UInt64)arr.Length) { res = 0; return false; }

            // read the value (little-endian)
            res = 0;
            for (int i = (int)size - 1; i >= 0; --i)
                res = (res << 8) | arr[(int)pos + i];

            return true;
        }

        /// <summary>
        /// Appends a value to an array of bytes in a list
        /// </summary>
        /// <param name="data">the byte array</param>
        /// <param name="size">the size in bytes of the value to write</param>
        /// <param name="val">the value to write</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Append(this List<byte> arr, UInt64 size, UInt64 val)
        {
            // write the value (little-endian)
            for (int i = 0; i < (int)size; ++i)
            {
                arr.Add((byte)val);
                val >>= 8;
            }
        }

        /// <summary>
        /// Gets the amount to offset address by to make it a multiple of size. if address is already a multiple of size, returns 0.
        /// </summary>
        /// <param name="address">the address to examine</param>
        /// <param name="size">the size to align to</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static UInt64 AlignOffset(UInt64 address, UInt64 size)
        {
            UInt64 pos = address % size;
            return pos == 0 ? 0 : size - pos;
        }
        /// <summary>
        /// Where address is the starting point, returns the next address aligned to the specified size
        /// </summary>
        /// <param name="address">the starting address</param>
        /// <param name="size">the size to align to</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static UInt64 Align(UInt64 address, UInt64 size)
        {
            return address + AlignOffset(address, size);
        }
        /// <summary>
        /// Adds the specified amount of zeroed padding (in bytes) to the array
        /// </summary>
        /// <param name="arr">the data array to pad</param>
        /// <param name="count">the amount of padding in bytes</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Pad(this List<byte> arr, UInt64 count)
        {
            for (; count > 0; --count) arr.Add(0);
        }
        /// <summary>
        /// Pads the array with 0's until the length is a multiple of the specified size
        /// </summary>
        /// <param name="arr">the array to align</param>
        /// <param name="size">the size to align to</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Align(this List<byte> arr, UInt64 size)
        {
            arr.Pad(AlignOffset((UInt64)arr.Count, size));
        }

        /// <summary>
        /// Writes an ASCII C-style string to memory. Returns true on success
        /// </summary>
        /// <param name="arr">the data array to write to</param>
        /// <param name="pos">the position in the array to begin writing</param>
        /// <param name="str">the string to write</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool WriteCString(this byte[] arr, UInt64 pos, string str)
        {
            // make sure we're not exceeding memory bounds
            if (pos >= (UInt64)arr.Length || pos + (UInt64)(str.Length + 1) > (UInt64)arr.Length) return false;

            // write each character
            for (int i = 0; i < str.Length; ++i) arr[pos + (UInt64)i] = (byte)str[i];
            // write a null terminator
            arr[pos + (UInt64)str.Length] = 0;

            return true;
        }
        /// <summary>
        /// Reads an ASCII C-style string from memory. Returns true on success
        /// </summary>
        /// <param name="arr">the data array to read from</param>
        /// <param name="pos">the position in the array to begin reading</param>
        /// <param name="str">the string read</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ReadCString(this byte[] arr, UInt64 pos, out string str)
        {
            StringBuilder b = new StringBuilder();

            // read the string
            for (; ; ++pos)
            {
                // ensure we're in bounds
                if (pos >= (UInt64)arr.Length) { str = null; return false; }

                // if it's not a terminator, append it
                if (arr[pos] != 0) b.Append((char)arr[pos]);
                // otherwise we're done reading
                else break;
            }

            // return the string
            str = b.ToString();
            return true;
        }

        // -- string stuff -- //

        /// <summary>
        /// Creates a table from an arrays of column entries and pads each row to a specified width
        /// </summary>
        /// <param name="col_width">the width for each column</param>
        /// <param name="rows">
        /// array of rows, which are arrays of column elementes.
        /// A null row is empty (new line).
        /// A null column entry will be skipped (filled with white space).
        /// If there are too few column entries, the ones not specified are ignored (immediate new line).
        /// If there are too many column entries, triggers an exception.
        /// </param>
        public static string CreateTable(int[] col_width, string[][] rows)
        {
            StringBuilder b = new StringBuilder();

            // process all the rows
            foreach (string[] row in rows)
            {
                // null allows for easy empty rows
                if (row != null)
                {
                    // for all the columns in this row
                    for (int i = 0; i < row.Length; ++i)
                    {
                        // append the cell (null allows for column skip)
                        if (row[i] != null) b.Append(row[i]);
                        // pad to column width
                        for (int j = row[i] != null ? row[i].Length : 0; j < col_width[i]; ++j) b.Append(' ');
                    }
                }

                // next line
                b.Append('\n');
            }

            return b.ToString();
        }

        /// <summary>
        /// Gets the numeric value of a hexadecimal digit. returns true if the character was in the hex range.
        /// </summary>
        /// <param name="ch">the character to test</param>
        /// <param name="val">the character's value [0-15]</param>
        public static bool GetHexValue(char ch, out int val)
        {
            if (ch >= '0' && ch <= '9') val = ch - '0';
            else if (ch >= 'a' && ch <= 'f') val = ch - 'a' + 10;
            else if (ch >= 'A' && ch <= 'F') val = ch - 'A' + 10;
            else { val = 0; return false; }

            return true;
        }
        /// <summary>
        /// Attempts to parse the string into an unsigned integer. Returns true on success.
        /// </summary>
        /// <param name="str">the string to parse</param>
        /// <param name="val">the resulting value</param>
        /// <param name="radix">the radix to use (must be 2-36)</param>
        public static bool TryParseUInt64(this string str, out UInt64 val, uint radix = 10)
        {
            // ensure radix is in range
            if (radix < 2 || radix > 36) throw new ArgumentException("radix must be in range 0-36");

            val = 0;  // initialize to zero
            uint add; // amount to add

            // fail on null or empty
            if (str == null || str.Length == 0) return false;

            // for each character
            for (int i = 0; i < str.Length; ++i)
            {
                val *= radix; // shift val

                // if it's a digit, add directly
                if (str[i] >= '0' && str[i] <= '9') add = (uint)(str[i] - '0');
                else if (str[i] >= 'a' && str[i] <= 'z') add = (uint)(str[i] - 'a' + 10);
                else if (str[i] >= 'A' && str[i] <= 'Z') add = (uint)(str[i] - 'A' + 10);
                // if it wasn't a known character, fail
                else return false;

                // if add value was out of range, fail
                if (add >= radix) return false;

                val += add; // add to val
            }

            return true;
        }

        /// <summary>
        /// Returns true if the string contains at least one occurrence of the specified character
        /// </summary>
        /// <param name="str">the string to test</param>
        /// <param name="ch">the character to look for</param>
        public static bool Contains(this string str, char ch)
        {
            for (int i = 0; i < str.Length; ++i)
                if (str[i] == ch) return true;

            return false;
        }
        /// <summary>
        /// Removes ALL white space from a string
        /// </summary>
        public static string RemoveWhiteSpace(this string str)
        {
            StringBuilder res = new StringBuilder();

            for (int i = 0; i < str.Length; ++i)
                if (!char.IsWhiteSpace(str[i])) res.Append(str[i]);

            return res.ToString();
        }

        /// <summary>
        /// Returns true if the string starts with the specified character
        /// </summary>
        /// <param name="str">the string to test</param>
        /// <param name="ch">the character it must start with</param>
        public static bool StartsWith(this string str, char ch)
        {
            return str.Length > 0 && str[0] == ch;
        }
        /// <summary>
        /// Returns true if the string ends with the specified character. if str is null or empty, returns false
        /// <param name="str">the string to test</param>
        /// <param name="ch">the character to look for</param>
        public static bool EndsWith(this string str, char ch)
        {
            return str != null && str.Length > 0 && str[str.Length - 1] == ch;
        }
        /// <summary>
        /// Returns true if the the string is equal to the specified value or begins with it and is followed by white space.
        /// </summary>
        /// <param name="str">the string to search in</param>
        /// <param name="val">the header value to test for</param>
        public static bool StartsWithToken(this string str, string val)
        {
            return str.StartsWith(val) && (str.Length == val.Length || char.IsWhiteSpace(str[val.Length]));
        }

        /// <summary>
        /// returns a binary dump representation of the data
        /// </summary>
        /// <param name="data">the data to dump</param>
        /// <param name="start">the index at which to begin dumping</param>
        /// <param name="count">the number of bytes to write</param>
        public static string Dump(this byte[] data, int start, int count)
        {
            StringBuilder dump = new StringBuilder();

            // make a header
            dump.Append("           ");
            for (int i = 0; i < 16; ++i) dump.Append($" {i:x} ");

            // if it's not starting on a new row
            if (start % 16 != 0)
            {
                // we need to write a line header
                dump.Append($"\n{start - start % 16:x8} - ");

                // and tack on some white space
                for (int i = 0; i < start % 16; ++i) dump.Append("   ");
            }

            // write the data
            for (int i = 0; i < count; ++i)
            {
                // start of new row gets a line header
                if ((start + i) % 16 == 0) dump.Append($"\n{start + i:x8} - ");

                dump.Append($"{data[start + i]:x2} ");
            }

            // end with a new line
            dump.Append('\n');

            return dump.ToString();
        }
        /// <summary>
        /// returns a binary dump representation of the data
        /// </summary>
        /// <param name="data">the data to dump</param>
        /// <param name="start">the index at which to begin dumping</param>
        public static string Dump(this byte[] data, int start) => data.Dump(start, data.Length - start);
        /// <summary>
        /// returns a binary dump representation of the data
        /// </summary>
        /// <param name="data">the data to dump</param>
        public static string Dump(this byte[] data) => data.Dump(0, data.Length);

        // -- serialization utilities -- //

        /// <summary>
        /// Writes an ASCII C-style string to the <see cref="BinaryWriter"/>
        /// </summary>
        /// <param name="writer">the writer to use</param>
        /// <param name="str">the string to write</param>
        public static void WriteASCII_CString(this BinaryWriter writer, string str)
        {
            // write each character as an ASCII byte
            foreach (char ch in str) writer.Write((byte)ch);
            // then a terminator
            writer.Write((byte)0);
        }
        /// <summary>
        /// Reads an ASCII C-style string from the <see cref="BinaryReader"/>. Throws <see cref="EndOfStreamException"/> if the entire sting cannot be read.
        /// </summary>
        /// <param name="reader">the reader to use</param>
        /// /// <exception cref="EndOfStreamException"></exception>
        public static string ReadASCII_CString(this BinaryReader reader)
        {
            StringBuilder b = new StringBuilder();
            byte ch;

            while (true)
            {
                // read a character (stored as ASCII byte
                ch = reader.ReadByte();

                // if non-null, append as a character
                if (ch != 0) b.Append((char)ch);
                // otherwise we're done
                else break;
            }

            // assign result
            return b.ToString();
        }

        // -- CSX64 encoding utilities -- //

        /// <summary>
        /// Gets the bitmask for the sign bit of an integer with the specified sizecode
        /// </summary>
        /// <param name="sizecode">the sizecode specifying the width of integer to examine</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static UInt64 SignMask(UInt64 sizecode)
        {
            return 1ul << ((8 << (UInt16)sizecode) - 1);
        }
        /// <summary>
        /// Gets the bitmask that includes the entire valid domain of an integer with the specified width
        /// </summary>
        /// <param name="sizecode">the sizecode specifying the width of integer to examine</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static UInt64 TruncMask(UInt64 sizecode)
        {
            UInt64 res = SignMask(sizecode);
            return res | (res - 1);
        }

        /// <summary>
        /// Returns if the value with specified size code is positive
        /// </summary>
        /// <param name="val">the value to process</param>
        /// <param name="sizecode">the current size code of the value</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Positive(UInt64 val, UInt64 sizecode)
        {
            return (val & SignMask(sizecode)) == 0;
        }
        /// <summary>
        /// Returns if the value with specified size code is negative
        /// </summary>
        /// <param name="val">the value to process</param>
        /// <param name="sizecode">the current size code of the value</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Negative(UInt64 val, UInt64 sizecode)
        {
            return (val & SignMask(sizecode)) != 0;
        }

        /// <summary>
        /// Sign extends a value to 64-bits
        /// </summary>
        /// <param name="val">the value to sign extend</param>
        /// <param name="sizecode">the current size code</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static UInt64 SignExtend(UInt64 val, UInt64 sizecode)
        {
            return Positive(val, sizecode) ? val : val | ~TruncMask(sizecode);
        }
        /// <summary>
        /// Truncates the value to the specified size code (can also be used to zero extend a value)
        /// </summary>
        /// <param name="val">the value to truncate</param>
        /// <param name="sizecode">the size code to truncate to</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static UInt64 Truncate(UInt64 val, UInt64 sizecode)
        {
            return val & TruncMask(sizecode);
        }

        /// <summary>
        /// Parses a 2-bit size code into an actual size (in bytes) 0:1  1:2  2:4  3:8
        /// </summary>
        /// <param name="sizecode">the code to parse</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static UInt64 Size(UInt64 sizecode)
        {
            return 1ul << (ushort)sizecode;
        }
        /// <summary>
        /// Parses a 2-bit size code into an actual size (in bits) 0:8  1:16  2:32  3:64
        /// </summary>
        /// <param name="sizecode">the code to parse</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static UInt64 SizeBits(UInt64 sizecode)
        {
            return 8ul << (ushort)sizecode;
        }

        /// <summary>
        /// Gets the sizecode of the specified size. Throws <see cref="ArgumentException"/> if the size is not a power of 2
        /// </summary>
        /// <param name="size">the size</param>
        /// <exception cref="ArgumentException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static UInt64 Sizecode(UInt64 size)
        {
            if (!IsPowerOf2(size)) throw new ArgumentException("argument to Sizecode() was not a power of 2");

            // compute sizecode by repeated shifting
            for (int i = 0; ; ++i)
            {
                size >>= 1;
                if (size == 0) return (UInt64)i;
            }
        }

        /// <summary>
        /// returns an elementary word size in bytes sufficient to hold the specified number of bits
        /// </summary>
        /// <param name="bits">the number of bits in the representation</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static UInt64 BitsToBytes(UInt64 bits)
        {
            if (bits <= 8) return 1;
            else if (bits <= 16) return 2;
            else if (bits <= 32) return 4;
            else if (bits <= 64) return 8;
            else throw new ArgumentException($"There is no elementary word size sufficient to hold {bits} bits");
        }

        /// <summary>
        /// Interprets a double as its raw bits
        /// </summary>
        /// <param name="val">value to interpret</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe UInt64 DoubleAsUInt64(double val)
        {
            return *(UInt64*)&val;
        }
        /// <summary>
        /// Interprets raw bits as a double
        /// </summary>
        /// <param name="val">value to interpret</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe double AsDouble(UInt64 val)
        {
            return *(double*)&val;
        }

        /// <summary>
        /// Interprets a float as its raw bits (placed in low 32 bits)
        /// </summary>
        /// <param name="val">the float to interpret</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe UInt64 FloatAsUInt64(float val)
        {
            return *(UInt32*)&val;
        }
        /// <summary>
        /// Interprets raw bits as a float (low 32 bits)
        /// </summary>
        /// <param name="val">the bits to interpret</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe float AsFloat(UInt32 val)
        {
            return *(float*)&val;
        }
    }
}
