using System;
using System.Collections.Generic;
using System.Text;

// -- Utility -- //

namespace CSX64
{
    public static class Utility
    {
        /// <summary>
        /// Gets the register partition with the specified size code
        /// </summary>
        /// <param name="code">The size code</param>
        public static UInt64 Get(this Register reg, UInt64 code)
        {
            switch (code)
            {
                case 0: return reg.x8;
                case 1: return reg.x16;
                case 2: return reg.x32;
                case 3: return reg.x64;

                default: throw new ArgumentOutOfRangeException("Register code out of range");
            }
        }
        /// <summary>
        /// Sets the register partition with the specified size code
        /// </summary>
        /// <param name="code">The size code</param>
        /// <param name="value">The value to set</param>
        public static void Set(this Register reg, UInt64 code, UInt64 value)
        {
            switch (code)
            {
                case 0: reg.x8 = value; return;
                case 1: reg.x16 = value; return;
                case 2: reg.x32 = value; return;
                case 3: reg.x64 = value; return;

                default: throw new ArgumentOutOfRangeException("Register code out of range");
            }
        }

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

            byte[] bytes; // destination for raw bytes

            switch (size)
            {
                case 1: arr[(int)pos] = (byte)val; return true;
                case 2: bytes = BitConverter.GetBytes((UInt16)val); break;
                case 4: bytes = BitConverter.GetBytes((UInt32)val); break;
                case 8: bytes = BitConverter.GetBytes(val); break;

                default: throw new ArgumentException("Specified data size is non-standard");
            }

            // write the value
            for (int i = 0; i < bytes.Length; ++i)
                arr[(int)pos + i] = bytes[i];

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

            switch (size)
            {
                case 1: res = arr[(int)pos]; return true;
                case 2: res = BitConverter.ToUInt16(arr, (int)pos); return true;
                case 4: res = BitConverter.ToUInt32(arr, (int)pos); return true;
                case 8: res = BitConverter.ToUInt64(arr, (int)pos); return true;

                default: throw new ArgumentException("Specified data size is non-standard");
            }
        }

        /// <summary>
        /// Appends a value to an array of bytes in a list
        /// </summary>
        /// <param name="data">the byte array</param>
        /// <param name="size">the size in bytes of the value to write</param>
        /// <param name="val">the value to write</param>
        public static void Append(this List<byte> arr, UInt64 size, UInt64 val)
        {
            byte[] bytes; // destination for raw bytes

            switch (size)
            {
                case 1: arr.Add((byte)val); return;
                case 2: bytes = BitConverter.GetBytes((UInt16)val); break;
                case 4: bytes = BitConverter.GetBytes((UInt32)val); break;
                case 8: bytes = BitConverter.GetBytes(val); break;

                default: throw new ArgumentException("Specified data size is non-standard");
            }

            // write the value
            for (int i = 0; i < bytes.Length; ++i)
                arr.Add(bytes[i]);
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
    }

    public partial class Computer
    {
        /// <summary>
        /// Returns if the value with specified size code is positive
        /// </summary>
        /// <param name="val">the value to process</param>
        /// <param name="sizecode">the current size code of the value</param>
        public static bool Positive(UInt64 val, UInt64 sizecode)
        {
            return ((val >> (8 * (ushort)Size(sizecode) - 1)) & 1) == 0;
        }
        /// <summary>
        /// Returns if the value with specified size code is negative
        /// </summary>
        /// <param name="val">the value to process</param>
        /// <param name="sizecode">the current size code of the value</param>
        public static bool Negative(UInt64 val, UInt64 sizecode)
        {
            return ((val >> (8 * (ushort)Size(sizecode) - 1)) & 1) != 0;
        }

        /// <summary>
        /// Sign extends a value to 64-bits
        /// </summary>
        /// <param name="val">the value to sign extend</param>
        /// <param name="sizecode">the current size code</param>
        public static UInt64 SignExtend(UInt64 val, UInt64 sizecode)
        {
            // if val is positive, do nothing
            if (Positive(val, sizecode)) return val;

            // otherwise, pad with 1's
            switch (sizecode)
            {
                case 0: return 0xffffffffffffff00 | val;
                case 1: return 0xffffffffffff0000 | val;
                case 2: return 0xffffffff00000000 | val;

                default: return val; // can't extend 64-bit value any further
            }
        }
        /// <summary>
        /// Truncates the value to the specified size code (can also be used to zero extend a value)
        /// </summary>
        /// <param name="val">the value to truncate</param>
        /// <param name="sizecode">the size code to truncate to</param>
        public static UInt64 Truncate(UInt64 val, UInt64 sizecode)
        {
            switch (sizecode)
            {
                case 0: return 0x00000000000000ff & val;
                case 1: return 0x000000000000ffff & val;
                case 2: return 0x00000000ffffffff & val;

                default: return val; // can't truncate 64-bit value
            }
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