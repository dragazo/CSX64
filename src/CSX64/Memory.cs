using System;
using static CSX64.Utility;

// -- Memory -- //

namespace CSX64
{
    public partial class Computer
    {
        // -- misc memory utilities -- //

        /// <summary>
        /// Reads a C-style string from memory. Returns true if successful, otherwise fails with OutOfBounds and returns false
        /// </summary>
        /// <param name="pos">the address in memory of the first character in the string</param>
        /// <param name="charsize">the size of each character in bytes</param>
        /// <param name="str">the resulting string</param>
        /// <param name="_abide_slow">if the memory access should abide by SMF. only pass false if it makes sense, otherwise slow should be slow</param>
        public bool GetCString(UInt64 pos, out string str)
        {
            // refer to utility function
            if (!Memory.ReadCString(pos, out str)) { Terminate(ErrorCode.OutOfBounds); return false; }

            return true;
        }
        /// <summary>
        /// Writes a C-style string to memory. Returns true if successful, otherwise fails with OutOfBounds and returns false
        /// </summary>
        /// <param name="pos">the address in memory of the first character to write</param>
        /// <param name="charsize">the size of each character in bytes</param>
        /// <param name="str">the string to write</param>
        /// <param name="_abide_slow">if the memory access should abide by SMF. only pass false if it makes sense, otherwise slow should be slow</param>
        public bool SetCString(UInt64 pos, string str)
        {
            // make sure we're not in the readonly segment
            if (pos < ReadonlyBarrier) { Terminate(ErrorCode.AccessViolation); return false; }

            // refer to utility function
            if (!Memory.WriteCString(pos, str)) { Terminate(ErrorCode.OutOfBounds); return false; }

            return true;
        }

        // -- unsigned memory access -- //

        /// <summary>
        /// Reads a 64-bit unsigned integer from memory
        /// </summary>
        /// <param name="pos">the position of the value in memory</param>
        /// <param name="res">the resulting value</param>
        /// <param name="_abide_slow">if the memory access should abide by SMF. only pass false if it makes sense, otherwise slow should be slow</param>
        public bool GetMem(UInt64 pos, out UInt64 res)
        {
            return GetMemRaw(pos, 8, out res);
        }
        /// <summary>
        /// Writes a 64-bit unsigned integer to memory
        /// </summary>
        /// <param name="pos">the position of the value in memory</param>
        /// <param name="res">the resulting value</param>
        /// <param name="_abide_slow">if the memory access should abide by SMF. only pass false if it makes sense, otherwise slow should be slow</param>
        public bool SetMem(UInt64 pos, UInt64 val)
        {
            return SetMemRaw(pos, 8, val);
        }

        /// <summary>
        /// Reads a 32-bit unsigned integer from memory
        /// </summary>
        /// <param name="pos">the position of the value in memory</param>
        /// <param name="res">the resulting value</param>
        /// <param name="_abide_slow">if the memory access should abide by SMF. only pass false if it makes sense, otherwise slow should be slow</param>
        public bool GetMem(UInt64 pos, out UInt32 res)
        {
            if (GetMemRaw(pos, 4, out UInt64 temp)) { res = (UInt32)temp; return true; }
            else { res = 0; return false; }
        }
        /// <summary>
        /// Writes a 32-bit unsigned integer to memory
        /// </summary>
        /// <param name="pos">the position of the value in memory</param>
        /// <param name="res">the resulting value</param>
        /// <param name="_abide_slow">if the memory access should abide by SMF. only pass false if it makes sense, otherwise slow should be slow</param>
        public bool SetMem(UInt64 pos, UInt32 val)
        {
            return SetMemRaw(pos, 4, val);
        }

        /// <summary>
        /// Reads a 16-bit unsigned integer from memory
        /// </summary>
        /// <param name="pos">the position of the value in memory</param>
        /// <param name="res">the resulting value</param>
        /// <param name="_abide_slow">if the memory access should abide by SMF. only pass false if it makes sense, otherwise slow should be slow</param>
        public bool GetMem(UInt64 pos, out UInt16 res)
        {
            if (GetMemRaw(pos, 2, out UInt64 temp)) { res = (UInt16)temp; return true; }
            else { res = 0; return false; }
        }
        /// <summary>
        /// Writes a 16-bit unsigned integer to memory
        /// </summary>
        /// <param name="pos">the position of the value in memory</param>
        /// <param name="res">the resulting value</param>
        /// <param name="_abide_slow">if the memory access should abide by SMF. only pass false if it makes sense, otherwise slow should be slow</param>
        public bool SetMem(UInt64 pos, UInt16 val)
        {
            return SetMemRaw(pos, 2, val);
        }

        /// <summary>
        /// Reads an 8-bit unsigned integer from memory
        /// </summary>
        /// <param name="pos">the position of the value in memory</param>
        /// <param name="res">the resulting value</param>
        /// <param name="_abide_slow">if the memory access should abide by SMF. only pass false if it makes sense, otherwise slow should be slow</param>
        public bool GetMem(UInt64 pos, out byte res)
        {
            if (GetMemRaw(pos, 1, out UInt64 temp)) { res = (byte)temp; return true; }
            else { res = 0; return false; }
        }
        /// <summary>
        /// Writes an 8-bit unsigned integer to memory
        /// </summary>
        /// <param name="pos">the position of the value in memory</param>
        /// <param name="res">the resulting value</param>
        /// <param name="_abide_slow">if the memory access should abide by SMF. only pass false if it makes sense, otherwise slow should be slow</param>
        public bool SetMem(UInt64 pos, byte val)
        {
            return SetMemRaw(pos, 1, val);
        }

        // -- signed memory access -- //

        /// <summary>
        /// Reads a 64-bit signed integer from memory
        /// </summary>
        /// <param name="pos">the position of the value in memory</param>
        /// <param name="res">the resulting value</param>
        /// <param name="_abide_slow">if the memory access should abide by SMF. only pass false if it makes sense, otherwise slow should be slow</param>
        public bool GetMem(UInt64 pos, out Int64 res)
        {
            if (GetMemRaw(pos, 8, out UInt64 temp)) { res = (Int64)temp; return true; }
            else { res = 0; return false; }
        }
        /// <summary>
        /// Writes a 64-bit signed integer to memory
        /// </summary>
        /// <param name="pos">the position of the value in memory</param>
        /// <param name="res">the resulting value</param>
        /// <param name="_abide_slow">if the memory access should abide by SMF. only pass false if it makes sense, otherwise slow should be slow</param>
        public bool SetMem(UInt64 pos, Int64 val)
        {
            return SetMemRaw(pos, 8, (UInt64)val);
        }

        /// <summary>
        /// Reads a 32-bit signed integer from memory
        /// </summary>
        /// <param name="pos">the position of the value in memory</param>
        /// <param name="res">the resulting value</param>
        /// <param name="_abide_slow">if the memory access should abide by SMF. only pass false if it makes sense, otherwise slow should be slow</param>
        public bool GetMem(UInt64 pos, out Int32 res)
        {
            if (GetMemRaw(pos, 4, out UInt64 temp)) { res = (Int32)temp; return true; }
            else { res = 0; return false; }
        }
        /// <summary>
        /// Writes a 32-bit unsigned integer to memory
        /// </summary>
        /// <param name="pos">the position of the value in memory</param>
        /// <param name="res">the resulting value</param>
        /// <param name="_abide_slow">if the memory access should abide by SMF. only pass false if it makes sense, otherwise slow should be slow</param>
        public bool SetMem(UInt64 pos, Int32 val)
        {
            return SetMemRaw(pos, 4, (UInt64)val);
        }

        /// <summary>
        /// Reads a 16-bit signed integer from memory
        /// </summary>
        /// <param name="pos">the position of the value in memory</param>
        /// <param name="res">the resulting value</param>
        /// <param name="_abide_slow">if the memory access should abide by SMF. only pass false if it makes sense, otherwise slow should be slow</param>
        public bool GetMem(UInt64 pos, out Int16 res)
        {
            if (GetMemRaw(pos, 2, out UInt64 temp)) { res = (Int16)temp; return true; }
            else { res = 0; return false; }
        }
        /// <summary>
        /// Writes a 16-bit unsigned integer to memory
        /// </summary>
        /// <param name="pos">the position of the value in memory</param>
        /// <param name="res">the resulting value</param>
        /// <param name="_abide_slow">if the memory access should abide by SMF. only pass false if it makes sense, otherwise slow should be slow</param>
        public bool SetMem(UInt64 pos, Int16 val)
        {
            return SetMemRaw(pos, 2, (UInt64)val);
        }

        /// <summary>
        /// Reads an 8-bit signed integer from memory
        /// </summary>
        /// <param name="pos">the position of the value in memory</param>
        /// <param name="res">the resulting value</param>
        /// <param name="_abide_slow">if the memory access should abide by SMF. only pass false if it makes sense, otherwise slow should be slow</param>
        public bool GetMem(UInt64 pos, out sbyte res)
        {
            if (GetMemRaw(pos, 1, out UInt64 temp)) { res = (sbyte)temp; return true; }
            else { res = 0; return false; }
        }
        /// <summary>
        /// Writes an 8-bit unsigned integer to memory
        /// </summary>
        /// <param name="pos">the position of the value in memory</param>
        /// <param name="res">the resulting value</param>
        /// <param name="_abide_slow">if the memory access should abide by SMF. only pass false if it makes sense, otherwise slow should be slow</param>
        public bool SetMem(UInt64 pos, sbyte val)
        {
            return SetMemRaw(pos, 1, (UInt64)val);
        }

        // -- floating-point access -- //

        /// <summary>
        /// Reads a 64-bit floating-point value from memory
        /// </summary>
        /// <param name="pos">the position of the value in memory</param>
        /// <param name="res">the resulting value</param>
        /// <param name="_abide_slow">if the memory access should abide by SMF. only pass false if it makes sense, otherwise slow should be slow</param>
        public bool GetMem(UInt64 pos, out double res)
        {
            if (GetMemRaw(pos, 8, out UInt64 temp)) { res = AsDouble(temp); return true; }
            else { res = 0; return false; }
        }
        /// <summary>
        /// Writes a 64-bit floating-point value to memory
        /// </summary>
        /// <param name="pos">the position of the value in memory</param>
        /// <param name="res">the resulting value</param>
        /// <param name="_abide_slow">if the memory access should abide by SMF. only pass false if it makes sense, otherwise slow should be slow</param>
        public bool SetMem(UInt64 pos, double val)
        {
            return SetMemRaw(pos, 8, DoubleAsUInt64(val));
        }

        /// <summary>
        /// Reads a 32-bit floating-point value from memory
        /// </summary>
        /// <param name="pos">the position of the value in memory</param>
        /// <param name="res">the resulting value</param>
        /// <param name="_abide_slow">if the memory access should abide by SMF. only pass false if it makes sense, otherwise slow should be slow</param>
        public bool GetMem(UInt64 pos, out float res)
        {
            if (GetMemRaw(pos, 4, out UInt64 temp)) { res = AsFloat(temp); return true; }
            else { res = 0; return false; }
        }
        /// <summary>
        /// Writes a 32-bit floating-point value to memory
        /// </summary>
        /// <param name="pos">the position of the value in memory</param>
        /// <param name="res">the resulting value</param>
        /// <param name="_abide_slow">if the memory access should abide by SMF. only pass false if it makes sense, otherwise slow should be slow</param>
        public bool SetMem(UInt64 pos, float val)
        {
            return SetMemRaw(pos, 4, FloatAsUInt64(val));
        }

        // -- unsigned push/pop -- //

        /// <summary>
        /// Reads a 64-bit unsigned integer from memory
        /// </summary>
        /// <param name="pos">the position of the value in memory</param>
        /// <param name="res">the resulting value</param>
        /// <param name="_abide_slow">if the memory access should abide by SMF. only pass false if it makes sense, otherwise slow should be slow</param>
        public bool Pop(out UInt64 res)
        {
            return PopRaw(8, out res);
        }
        /// <summary>
        /// Writes a 64-bit unsigned integer to memory
        /// </summary>
        /// <param name="pos">the position of the value in memory</param>
        /// <param name="res">the resulting value</param>
        /// <param name="_abide_slow">if the memory access should abide by SMF. only pass false if it makes sense, otherwise slow should be slow</param>
        public bool Push(UInt64 val)
        {
            return PushRaw(8, val);
        }

        /// <summary>
        /// Reads a 32-bit unsigned integer from memory
        /// </summary>
        /// <param name="pos">the position of the value in memory</param>
        /// <param name="res">the resulting value</param>
        /// <param name="_abide_slow">if the memory access should abide by SMF. only pass false if it makes sense, otherwise slow should be slow</param>
        public bool Pop(out UInt32 res)
        {
            if (PopRaw(4, out UInt64 temp)) { res = (UInt32)temp; return true; }
            else { res = 0; return false; }
        }
        /// <summary>
        /// Writes a 32-bit unsigned integer to memory
        /// </summary>
        /// <param name="pos">the position of the value in memory</param>
        /// <param name="res">the resulting value</param>
        /// <param name="_abide_slow">if the memory access should abide by SMF. only pass false if it makes sense, otherwise slow should be slow</param>
        public bool Push(UInt32 val)
        {
            return PushRaw(4, val);
        }

        /// <summary>
        /// Reads a 16-bit unsigned integer from memory
        /// </summary>
        /// <param name="pos">the position of the value in memory</param>
        /// <param name="res">the resulting value</param>
        /// <param name="_abide_slow">if the memory access should abide by SMF. only pass false if it makes sense, otherwise slow should be slow</param>
        public bool Pop(out UInt16 res)
        {
            if (PopRaw(2, out UInt64 temp)) { res = (UInt16)temp; return true; }
            else { res = 0; return false; }
        }
        /// <summary>
        /// Writes a 16-bit unsigned integer to memory
        /// </summary>
        /// <param name="pos">the position of the value in memory</param>
        /// <param name="res">the resulting value</param>
        /// <param name="_abide_slow">if the memory access should abide by SMF. only pass false if it makes sense, otherwise slow should be slow</param>
        public bool Push(UInt16 val)
        {
            return PushRaw(2, val);
        }

        /// <summary>
        /// Reads an 8-bit unsigned integer from memory
        /// </summary>
        /// <param name="pos">the position of the value in memory</param>
        /// <param name="res">the resulting value</param>
        /// <param name="_abide_slow">if the memory access should abide by SMF. only pass false if it makes sense, otherwise slow should be slow</param>
        public bool Pop(out byte res)
        {
            if (PopRaw(1, out UInt64 temp)) { res = (byte)temp; return true; }
            else { res = 0; return false; }
        }
        /// <summary>
        /// Writes an 8-bit unsigned integer to memory
        /// </summary>
        /// <param name="pos">the position of the value in memory</param>
        /// <param name="res">the resulting value</param>
        /// <param name="_abide_slow">if the memory access should abide by SMF. only pass false if it makes sense, otherwise slow should be slow</param>
        public bool Push(byte val)
        {
            return PushRaw(1, val);
        }

        // -- signed memory push/pop -- //

        /// <summary>
        /// Reads a 64-bit signed integer from memory
        /// </summary>
        /// <param name="pos">the position of the value in memory</param>
        /// <param name="res">the resulting value</param>
        /// <param name="_abide_slow">if the memory access should abide by SMF. only pass false if it makes sense, otherwise slow should be slow</param>
        public bool Pop(out Int64 res)
        {
            if (PopRaw(8, out UInt64 temp)) { res = (Int64)temp; return true; }
            else { res = 0; return false; }
        }
        /// <summary>
        /// Writes a 64-bit signed integer to memory
        /// </summary>
        /// <param name="pos">the position of the value in memory</param>
        /// <param name="res">the resulting value</param>
        /// <param name="_abide_slow">if the memory access should abide by SMF. only pass false if it makes sense, otherwise slow should be slow</param>
        public bool Push(Int64 val)
        {
            return PushRaw(8, (UInt64)val);
        }

        /// <summary>
        /// Reads a 32-bit signed integer from memory
        /// </summary>
        /// <param name="pos">the position of the value in memory</param>
        /// <param name="res">the resulting value</param>
        /// <param name="_abide_slow">if the memory access should abide by SMF. only pass false if it makes sense, otherwise slow should be slow</param>
        public bool Pop(out Int32 res)
        {
            if (PopRaw(4, out UInt64 temp)) { res = (Int32)temp; return true; }
            else { res = 0; return false; }
        }
        /// <summary>
        /// Writes a 32-bit unsigned integer to memory
        /// </summary>
        /// <param name="pos">the position of the value in memory</param>
        /// <param name="res">the resulting value</param>
        /// <param name="_abide_slow">if the memory access should abide by SMF. only pass false if it makes sense, otherwise slow should be slow</param>
        public bool Push(Int32 val)
        {
            return PushRaw(4, (UInt64)val);
        }

        /// <summary>
        /// Reads a 16-bit signed integer from memory
        /// </summary>
        /// <param name="pos">the position of the value in memory</param>
        /// <param name="res">the resulting value</param>
        /// <param name="_abide_slow">if the memory access should abide by SMF. only pass false if it makes sense, otherwise slow should be slow</param>
        public bool Pop(out Int16 res)
        {
            if (PopRaw(2, out UInt64 temp)) { res = (Int16)temp; return true; }
            else { res = 0; return false; }
        }
        /// <summary>
        /// Writes a 16-bit unsigned integer to memory
        /// </summary>
        /// <param name="pos">the position of the value in memory</param>
        /// <param name="res">the resulting value</param>
        /// <param name="_abide_slow">if the memory access should abide by SMF. only pass false if it makes sense, otherwise slow should be slow</param>
        public bool Push(Int16 val)
        {
            return PushRaw(2, (UInt64)val);
        }

        /// <summary>
        /// Reads an 8-bit signed integer from memory
        /// </summary>
        /// <param name="pos">the position of the value in memory</param>
        /// <param name="res">the resulting value</param>
        /// <param name="_abide_slow">if the memory access should abide by SMF. only pass false if it makes sense, otherwise slow should be slow</param>
        public bool Pop(out sbyte res)
        {
            if (PopRaw(1, out UInt64 temp)) { res = (sbyte)temp; return true; }
            else { res = 0; return false; }
        }
        /// <summary>
        /// Writes an 8-bit unsigned integer to memory
        /// </summary>
        /// <param name="pos">the position of the value in memory</param>
        /// <param name="res">the resulting value</param>
        /// <param name="_abide_slow">if the memory access should abide by SMF. only pass false if it makes sense, otherwise slow should be slow</param>
        public bool Push(sbyte val)
        {
            return PushRaw(1, (UInt64)val);
        }

        // -- floating-point push/pop -- //

        /// <summary>
        /// Reads a 64-bit floating-point value from memory
        /// </summary>
        /// <param name="pos">the position of the value in memory</param>
        /// <param name="res">the resulting value</param>
        /// <param name="_abide_slow">if the memory access should abide by SMF. only pass false if it makes sense, otherwise slow should be slow</param>
        public bool Pop(out double res)
        {
            if (PopRaw(8, out UInt64 temp)) { res = AsDouble(temp); return true; }
            else { res = 0; return false; }
        }
        /// <summary>
        /// Writes a 64-bit floating-point value to memory
        /// </summary>
        /// <param name="pos">the position of the value in memory</param>
        /// <param name="res">the resulting value</param>
        /// <param name="_abide_slow">if the memory access should abide by SMF. only pass false if it makes sense, otherwise slow should be slow</param>
        public bool Push(double val)
        {
            return PushRaw(8, DoubleAsUInt64(val));
        }

        /// <summary>
        /// Reads a 32-bit floating-point value from memory
        /// </summary>
        /// <param name="pos">the position of the value in memory</param>
        /// <param name="res">the resulting value</param>
        /// <param name="_abide_slow">if the memory access should abide by SMF. only pass false if it makes sense, otherwise slow should be slow</param>
        public bool Pop(out float res)
        {
            if (PopRaw(4, out UInt64 temp)) { res = AsFloat(temp); return true; }
            else { res = 0; return false; }
        }
        /// <summary>
        /// Writes a 32-bit floating-point value to memory
        /// </summary>
        /// <param name="pos">the position of the value in memory</param>
        /// <param name="res">the resulting value</param>
        /// <param name="_abide_slow">if the memory access should abide by SMF. only pass false if it makes sense, otherwise slow should be slow</param>
        public bool Push(float val)
        {
            return PushRaw(4, FloatAsUInt64(val));
        }

        // -- private memory utilities -- //

        /// <summary>
        /// Pushes a value onto the stack
        /// </summary>
        /// <param name="size">the size of the value (in bytes)</param>
        /// <param name="val">the value to push</param>
        private bool PushRaw(UInt64 size, UInt64 val)
        {
            RSP -= size;
            return SetMemRaw(RSP, size, val);
        }
        /// <summary>
        /// Pops a value from the stack
        /// </summary>
        /// <param name="size">the size of the value (in bytes)</param>
        /// <param name="val">the resulting value</param>
        private bool PopRaw(UInt64 size, out UInt64 val)
        {
            if (!GetMemRaw(RSP, size, out val)) return false;
            RSP += size;
            return true;
        }

        /// <summary>
        /// Reads a value from memory (fails with OutOfBounds if invalid). if SMF is set, delays 
        /// </summary>
        /// <param name="pos">Address to read</param>
        /// <param name="size">Number of bytes to read</param>
        /// <param name="res">The result</param>
        /// <param name="_abide_slow">if the memory access should abide by SMF. only pass false if it makes sense, otherwise slow should be slow</param>
        private bool GetMemRaw(UInt64 pos, UInt64 size, out UInt64 res)
        {
            // refer to utility function
            if (!Memory.Read(pos, size, out res)) { Terminate(ErrorCode.OutOfBounds); return false; }

            return true;
        }
        /// <summary>
        /// Writes a value to memory (fails with OutOfBounds if invalid)
        /// </summary>
        /// <param name="pos">Address to write</param>
        /// <param name="size">Number of bytes to write</param>
        /// <param name="val">The value to write</param>
        /// <param name="_abide_slow">if the memory access should abide by SMF. only pass false if it makes sense, otherwise slow should be slow</param>
        private bool SetMemRaw(UInt64 pos, UInt64 size, UInt64 val)
        {
            // make sure we're not in the readonly segment
            if (pos < ReadonlyBarrier) { Terminate(ErrorCode.AccessViolation); return false; }

            // refer to utility function
            if (!Memory.Write(pos, size, val)) { Terminate(ErrorCode.OutOfBounds); return false; }

            return true;
        }

        /// <summary>
        /// Gets a value at and advances the execution pointer (fails with OutOfBounds if invalid)
        /// </summary>
        /// <param name="size">Number of bytes to read</param>
        /// <param name="res">The result</param>
        private bool GetMemAdv(UInt64 size, out UInt64 res)
        {
            // make sure we can get the memory
            if (!GetMemRaw(RIP, size, out res)) return false;
            RIP += size;
            return true;
        }
        /// <summary>
        /// Gets an address and advances the execution pointer
        /// </summary>
        /// <param name="res">resulting address</param>
        private bool GetAddressAdv(out UInt64 res)
        {
            res = 0; // initialize out param

            // [1: literal][3: m1][1: -m2][3: m2]   ([4: r1][4: r2])   ([64: imm])

            UInt64 mults, regs = 0, imm = 0; // the mult codes, regs, and literal

            // parse the address
            if (!GetMemAdv(1, out mults) || (mults & 0x77) != 0 && !GetMemAdv(1, out regs) || (mults & 0x80) != 0 && !GetMemAdv(8, out imm)) return false;

            // compute the result into res
            res = Mult((mults >> 4) & 7) * Registers[regs >> 4].x64 + Mult(mults & 7, mults & 8) * Registers[regs & 15].x64 + imm;

            // got an address
            return true;
        }
    }
}