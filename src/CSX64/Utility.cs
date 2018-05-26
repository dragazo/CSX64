using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

// -- Utility -- //

namespace CSX64
{
    public static class Utility
    {
        // -- misc utilities -- //

        /// <summary>
        /// Swaps the contents of the specified l-values
        /// </summary>
        public static void Swap<T>(ref T a, ref T b)
        {
            T temp = a;
            a = b;
            b = temp;
        }

        /// <summary>
        /// Removes all white space from a string
        /// </summary>
        public static string RemoveWhiteSpace(this string str)
        {
            StringBuilder res = new StringBuilder();

            for (int i = 0; i < str.Length; ++i)
                if (!char.IsWhiteSpace(str[i])) res.Append(str[i]);

            return res.ToString();
        }

        /// <summary>
        /// Returns true if this value is a power of two. (zero returns false)
        /// </summary>
        /// <param name="val">the value to test</param>
        public static bool IsPowerOf2(this UInt64 val)
        {
            return val != 0 && (val & (val - 1)) == 0;
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
        public static bool Write(this byte[] arr, UInt64 pos, UInt64 size, UInt64 val)
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
        /// Writes an ASCII C-style string to memory. Returns true on success
        /// </summary>
        /// <param name="arr">the data array to write to</param>
        /// <param name="pos">the position in the array to begin writing</param>
        /// <param name="str">the string to write</param>
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
        
        // -- misc utilities -- //

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
        /// Writes a binary dump representation of the data to the console
        /// </summary>
        /// <param name="data">the data to dump</param>
        /// <param name="start">the index at which to begin dumping</param>
        /// <param name="count">the number of bytes to write</param>
        public static void Dump(this byte[] data, int start, int count)
        {
            // make a header
            Console.Write("           ");
            for (int i = 0; i < 16; ++i) Console.Write($" {i:x} ");

            // if it's not starting on a new row
            if (start % 16 != 0)
            {
                // we need to write a line header
                Console.Write($"\n{start - start % 16:x8} - ");

                // and tack on some white space
                for (int i = 0; i < start % 16; ++i) Console.Write("   ");
            }

            // write the data
            for (int i = 0; i < count; ++i)
            {
                // start of new row gets a line header
                if ((start + i) % 16 == 0) Console.Write($"\n{start + i:x8} - ");

                Console.Write($"{data[start + i]:x2} ");
            }

            // end with a new line
            Console.WriteLine();
        }
        /// <summary>
        /// Writes a binary dump representation of the data to the console
        /// </summary>
        /// <param name="data">the data to dump</param>
        /// <param name="start">the index at which to begin dumping</param>
        public static void Dump(this byte[] data, int start) => data.Dump(start, data.Length - start);
        /// <summary>
        /// Writes a binary dump representation of the data to the console
        /// </summary>
        /// <param name="data">the data to dump</param>
        public static void Dump(this byte[] data) => data.Dump(0, data.Length);

        /// <summary>
        /// Gets a random UInt64 value
        /// </summary>
        /// <param name="rand">random object to use</param>
        public static UInt64 NextUInt64(this Random rand)
        {
            return ((UInt64)(UInt32)rand.Next() << 32) | (UInt32)rand.Next();
        }
        /// <summary>
        /// Gets a random boolean
        /// </summary>
        /// <param name="rand">the random object to use</param>
        public static bool NextBool(this Random rand)
        {
            return rand.Next(2) == 1;
        }

        // -- CSX64 encoding utilities -- //

        /// <summary>
        /// Returns if the value with specified size code is positive
        /// </summary>
        /// <param name="val">the value to process</param>
        /// <param name="sizecode">the current size code of the value</param>
        public static bool Positive(UInt64 val, UInt64 sizecode)
        {
            return ((val >> ((8 << (ushort)sizecode) - 1)) & 1) == 0;
        }
        /// <summary>
        /// Returns if the value with specified size code is negative
        /// </summary>
        /// <param name="val">the value to process</param>
        /// <param name="sizecode">the current size code of the value</param>
        public static bool Negative(UInt64 val, UInt64 sizecode)
        {
            return ((val >> ((8 << (ushort)sizecode) - 1)) & 1) != 0;
        }

        /// <summary>
        /// Sign extends a value to 64-bits
        /// </summary>
        /// <param name="val">the value to sign extend</param>
        /// <param name="sizecode">the current size code</param>
        public static UInt64 SignExtend(UInt64 val, UInt64 sizecode)
        {
            return Positive(val, sizecode) ? val : ~(((1ul << (8 << (ushort)sizecode)) & ~1ul) - 1) | val;
        }
        /// <summary>
        /// Truncates the value to the specified size code (can also be used to zero extend a value)
        /// </summary>
        /// <param name="val">the value to truncate</param>
        /// <param name="sizecode">the size code to truncate to</param>
        public static UInt64 Truncate(UInt64 val, UInt64 sizecode)
        {
            return (((1ul << (8 << (ushort)sizecode)) & ~1ul) - 1) & val;
        }

        /// <summary>
        /// Parses a 2-bit size code into an actual size (in bytes) 0:1  1:2  2:4  3:8
        /// </summary>
        /// <param name="sizecode">the code to parse</param>
        public static UInt64 Size(UInt64 sizecode)
        {
            return 1ul << (ushort)sizecode;
        }
        /// <summary>
        /// Parses a 2-bit size code into an actual size (in bits) 0:8  1:16  2:32  3:64
        /// </summary>
        /// <param name="sizecode">the code to parse</param>
        public static UInt64 SizeBits(UInt64 sizecode)
        {
            return 8ul << (ushort)sizecode;
        }

        /// <summary>
        /// Gets the multiplier from a 3-bit mult code. 0:0  1:1  2:2  3:4  4:8  5:16  6:32  7:64
        /// </summary>
        /// <param name="multcode">the code to parse</param>
        public static UInt64 Mult(UInt64 multcode)
        {
            return multcode == 0 ? 0ul : 1ul << (ushort)(multcode - 1);
        }
        /// <summary>
        /// As MultCode but returns negative value if neg is nonzero
        /// </summary>
        /// <param name="multcode">the code to parse</param>
        /// <param name="neg">the negative boolean</param>
        public static UInt64 Mult(UInt64 multcode, UInt64 neg)
        {
            return neg == 0 ? Mult(multcode) : ~Mult(multcode) + 1;
        }

        /// <summary>
        /// Interprets a double as its raw bits
        /// </summary>
        /// <param name="val">value to interpret</param>
        public static unsafe UInt64 DoubleAsUInt64(double val)
        {
            return *(UInt64*)&val;
        }
        /// <summary>
        /// Interprets raw bits as a double
        /// </summary>
        /// <param name="val">value to interpret</param>
        public static unsafe double AsDouble(UInt64 val)
        {
            return *(double*)&val;
        }

        /// <summary>
        /// Interprets a float as its raw bits (placed in low 32 bits)
        /// </summary>
        /// <param name="val">the float to interpret</param>
        public static unsafe UInt64 FloatAsUInt64(float val)
        {
            return *(UInt32*)&val;
        }
        /// <summary>
        /// Interprets raw bits as a float (low 32 bits)
        /// </summary>
        /// <param name="val">the bits to interpret</param>
        public static unsafe float AsFloat(UInt64 val)
        {
            UInt32 bits = (UInt32)val; // getting the low bits allows this code to work even on big-endian platforms
            return *(float*)&bits;
        }
    }
}