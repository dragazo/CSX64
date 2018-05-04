using System;
using System.Text;
using System.IO;

// -- Extensions -- //

namespace CSX64
{
    public static class ComputerExtensions
    {
        public static Int64 MakeSigned(this UInt64 val)
        {
            //return (val & 0x8000000000000000) != 0 ? -(Int64)(~val + 1) : (Int64)val;
            return (Int64)val;
        }
        public static UInt64 MakeUnsigned(this Int64 val)
        {
            //return val < 0 ? ~(UInt64)(-val) + 1 : (UInt64)val;
            return (UInt64)val;
        }

        /// <summary>
        /// Converts a byte array representation into a unicode string, obeying current system endianness
        /// </summary>
        /// <param name="bytes">the byte array to decode</param>
        /// <param name="index">the index of the first byte in the array</param>
        /// <param name="count">the number of bytes to decode</param>
        public static string ConvertToString(this byte[] bytes, int index, int count)
        {
            if (BitConverter.IsLittleEndian) return Encoding.Unicode.GetString(bytes, index, count);
            else return Encoding.BigEndianUnicode.GetString(bytes, index, count);
        }
        /// <summary>
        /// Converts a string into its byte representation, obeying current system endianness
        /// </summary>
        /// <param name="str">the string to convert</param>
        public static byte[] ConvertToBytes(this string str)
        {
            if (BitConverter.IsLittleEndian) return Encoding.Unicode.GetBytes(str);
            else return Encoding.BigEndianUnicode.GetBytes(str);
        }

        /// <summary>
        /// Converts a string into a stream onject
        /// </summary>
        /// <param name="str">the string source</param>
        public static Stream ToStream(this string str)
        {
            // create the stream
            Stream stream = new MemoryStream();

            // write the string contents
            using (StreamWriter writer = new StreamWriter(stream))
                writer.Write(str);

            // reposition to beginning
            stream.Position = 0;

            return stream;
        }
    }
}