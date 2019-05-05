using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using static CSX64.Utility;

// -- Assembly -- //

// LIMITATIONS:
// HoleData assumes 32-bit addresses to cut down on memory/disk usage
// Assembler/linker use List<byte>, which uses 32-bit indexing

namespace CSX64
{
    public enum AssembleError
    {
        None, ArgCount, MissingSize, ArgError, FormatError, UsageError, UnknownOp, EmptyFile, InvalidLabel, SymbolRedefinition, UnknownSymbol, NotImplemented
    }
    public enum LinkError
    {
        None, EmptyResult, SymbolRedefinition, MissingSymbol, FormatError
    }

    public struct AssembleResult
    {
        /// <summary>
        /// The error that occurred (or None for success)
        /// </summary>
        public AssembleError Error;
        /// <summary>
        /// What caused the error
        /// </summary>
        public string ErrorMsg;

        public AssembleResult(AssembleError error, string errorMsg)
        {
            Error = error;
            ErrorMsg = errorMsg;
        }
    }
    public struct LinkResult
    {
        /// <summary>
        /// The error that occurred (or None for success)
        /// </summary>
        public LinkError Error;
        /// <summary>
        /// What caused the error
        /// </summary>
        public string ErrorMsg;

        public LinkResult(LinkError error, string errorMsg)
        {
            Error = error;
            ErrorMsg = errorMsg;
        }
    }

    /// <summary>
    /// Represents an assembled object file used to create an executable
    /// </summary>
    public class ObjectFile
    {
        /// <summary>
        /// The list of exported symbol names
        /// </summary>
        internal List<string> GlobalSymbols = new List<string>();
        /// <summary>
        /// The list of imported symbol names
        /// </summary>
        internal List<string> ExternalSymbols = new List<string>();

        /// <summary>
        /// The symbols defined in the file
        /// </summary>
        internal Dictionary<string, Expr> Symbols = new Dictionary<string, Expr>();

        /// <summary>
        /// The largest alignment in the text segment
        /// </summary>
        internal UInt32 TextAlign = 1;
        /// <summary>
        /// The largest alignment in the rodata segment
        /// </summary>
        internal UInt32 RodataAlign = 1;
        /// <summary>
        /// The largest alignment in the data segment
        /// </summary>
        internal UInt32 DataAlign = 1;
        /// <summary>
        /// The largest alignment in the bss segment
        /// </summary>
        internal UInt32 BSSAlign = 1;

        /// <summary>
        /// The holes in the text segment that need to be patched
        /// </summary>
        internal List<HoleData> TextHoles = new List<HoleData>();
        /// <summary>
        /// The holes in the rodata segment that need to be patched
        /// </summary>
        internal List<HoleData> RodataHoles = new List<HoleData>();
        /// <summary>
        /// The holes in the data segment that need to be patched
        /// </summary>
        internal List<HoleData> DataHoles = new List<HoleData>();

        /// <summary>
        /// The contents of the text segment
        /// </summary>
        internal List<byte> Text = new List<byte>();
        /// <summary>
        /// The contents of the rodata segment
        /// </summary>
        internal List<byte> Rodata = new List<byte>();
        /// <summary>
        /// The contents of the data segment
        /// </summary>
        internal List<byte> Data = new List<byte>();
        /// <summary>
        /// The length of the Bss segment
        /// </summary>
        internal UInt32 BssLen = 0;

        /// <summary>
        /// Marks that this object file is in a valid, usable state
        /// </summary>
        public bool Clean { get; internal set; } = false;

        internal ObjectFile() { }

        // ---------------------------

        /// <summary>
        /// Writes a binary representation of an object file to the stream. Throws <see cref="ArgumentException"/> if file is dirty
        /// </summary>
        /// <param name="writer">the binary writer to use. must be clean</param>
        /// <param name="obj">the object file to write</param>
        /// <exception cref="ArgumentException"></exception>
        public static void WriteTo(BinaryWriter writer, ObjectFile obj)
        {
            // ensure the object is clean
            if (!obj.Clean) throw new ArgumentException("Attempt to use dirty object file");

            // write the global symbols (length-prefixed)
            writer.Write(obj.GlobalSymbols.Count);
            foreach (string symbol in obj.GlobalSymbols)
                writer.Write(symbol);

            // write the external symbols (length-prefixed)
            writer.Write(obj.ExternalSymbols.Count);
            foreach (string symbol in obj.ExternalSymbols)
                writer.Write(symbol);

            // write the symbols (length-prefixed)
            writer.Write(obj.Symbols.Count);
            foreach (var entry in obj.Symbols)
            {
                writer.Write(entry.Key);
                Expr.WriteTo(writer, entry.Value);
            }

            // write alignments
            writer.Write(obj.TextAlign);
            writer.Write(obj.RodataAlign);
            writer.Write(obj.DataAlign);
            writer.Write(obj.BSSAlign);

            // write the text holes (length-prefixed)
            writer.Write(obj.TextHoles.Count);
            foreach (HoleData hole in obj.TextHoles)
                HoleData.WriteTo(writer, hole);

            // write the rodata holes (length-prefixed)
            writer.Write(obj.RodataHoles.Count);
            foreach (HoleData hole in obj.RodataHoles)
                HoleData.WriteTo(writer, hole);

            // write the data holes (length-prefixed)
            writer.Write(obj.DataHoles.Count);
            foreach (HoleData hole in obj.DataHoles)
                HoleData.WriteTo(writer, hole);

            // write the text segment (length-prefixed)
            writer.Write(obj.Text.Count);
            writer.Write(obj.Text.ToArray()); // ToArray() costs an O(n) copy, but still beats an equal number of function calls

            // write the rodata segment (length-prefixed)
            writer.Write(obj.Rodata.Count);
            writer.Write(obj.Rodata.ToArray()); // ToArray() costs an O(n) copy, but still beats an equal number of function calls

            // write the data segment (length-prefixed)
            writer.Write(obj.Data.Count);
            writer.Write(obj.Data.ToArray()); // ToArray() costs an O(n) copy, but still beats an equal number of function calls

            // write length of bss
            writer.Write(obj.BssLen);
        }
        /// <summary>
        /// Reads a binary representation of an object file from the stream
        /// </summary>
        /// <param name="reader">the binary reader to use</param>
        /// <param name="hole">the resulting object file</param>
        public static void ReadFrom(BinaryReader reader, out ObjectFile obj)
        {
            // create the object file
            obj = new ObjectFile();

            // read the global symbols (length-prefixed)
            int count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
                obj.GlobalSymbols.Add(reader.ReadString());

            // read the external symbols (length-prefixed)
            count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
                obj.ExternalSymbols.Add(reader.ReadString());

            // read the symbols (length-prefixed)
            count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                string key = reader.ReadString();
                Expr.ReadFrom(reader, out Expr value);

                obj.Symbols.Add(key, value);
            }

            // read alignments
            if (!(obj.TextAlign = reader.ReadUInt32()).IsPowerOf2()) throw new FormatException("Object file was corrupted");
            if (!(obj.RodataAlign = reader.ReadUInt32()).IsPowerOf2()) throw new FormatException("Object file was corrupted");
            if (!(obj.DataAlign = reader.ReadUInt32()).IsPowerOf2()) throw new FormatException("Object file was corrupted");
            if (!(obj.BSSAlign = reader.ReadUInt32()).IsPowerOf2()) throw new FormatException("Object file was corrupted");

            // read the text holes (length-prefixed)
            count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                HoleData.ReadFrom(reader, out HoleData hole);
                obj.TextHoles.Add(hole);
            }

            // read the rodata holes (length-prefixed)
            count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                HoleData.ReadFrom(reader, out HoleData hole);
                obj.RodataHoles.Add(hole);
            }

            // read the data holes (length-prefixed)
            count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                HoleData.ReadFrom(reader, out HoleData hole);
                obj.DataHoles.Add(hole);
            }

            // read the text segment (length-prefixed)
            count = reader.ReadInt32();
            byte[] raw = new byte[count];
            if (reader.Read(raw, 0, count) != count) throw new FormatException("Object file was corrupted");
            obj.Text = raw.ToList();

            // read the rodata (length-prefixed)
            count = reader.ReadInt32();
            raw = new byte[count];
            if (reader.Read(raw, 0, count) != count) throw new FormatException("Object file was corrupted");
            obj.Rodata = raw.ToList();

            // read the data (length-prefixed)
            count = reader.ReadInt32();
            raw = new byte[count];
            if (reader.Read(raw, 0, count) != count) throw new FormatException("Object file was corrupted");
            obj.Data = raw.ToList();

            // read the length of bss segment
            obj.BssLen = reader.ReadUInt32();

            // validate the object
            obj.Clean = true;
        }

        /// <summary>
        /// Reads only the global symbols from the object file
        /// </summary>
        /// <param name="reader">the binary reader to use</param>
        /// <param name="globals">the resulting list of global symbols</param>
        public static void ReadGlobalsFrom(BinaryReader reader, out List<string> globals)
        {
            // allocate list
            globals = new List<string>();

            // read the global symbols (length-prefixed)
            int count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
                globals.Add(reader.ReadString());
        }
    }

    // -----------------------------

    internal enum PatchError
    {
        None, Unevaluated, Error
    }
    internal enum AsmSegment
    {
        INVALID = 0, TEXT = 1, RODATA = 2, DATA = 4, BSS = 8
    }

    /// <summary>
    /// Represents an expression used to compute a value, with options for using a symbol table for lookup
    /// </summary>
    internal class Expr
    {
        internal enum OPs
        {
            None,

            // binary ops

            Mul,
            UDiv, UMod,
            SDiv, SMod,
            Add, Sub,

            SL, SR,

            Less, LessE, Great, GreatE,
            Eq, Neq,

            BitAnd, BitXor, BitOr,
            LogAnd, LogOr,

            // unary ops

            Neg, BitNot, LogNot, Int, Float,

            // special

            Condition, Pair,
            NullCoalesce
        }

        /// <summary>
        /// The operation used to compute the value (or None if leaf)
        /// </summary>
        public OPs OP = OPs.None;

        /// <summary>
        /// A subexpression of this expression
        /// </summary>
        public Expr Left = null, Right = null;

        private string _Token = null;
        /// <summary>
        /// Gets/sets the string that will be evaluated to determine the value of this node
        /// </summary>
        public string Token
        {
            get { return _Token; }
            set
            {
                // set the value
                _Token = value ?? throw new ArgumentNullException("Token cannot be set to null");

                // ensure this is now a leaf (expression) node
                OP = OPs.None;
                Left = Right = null;
            }
        }
        /// <summary>
        /// The cached result of this node if there is no token
        /// </summary>
        private UInt64 _Result = 0;
        /// <summary>
        /// Marks that the <see cref="_Result"/> of this node is floating-point
        /// </summary>
        private bool _Floating = false;

        /// <summary>
        /// Gets if this node is a leaf
        /// </summary>
        public bool IsLeaf => OP == OPs.None;
        /// <summary>
        /// Gets if this node has been evaluated
        /// </summary>
        public bool IsEvaluated => OP == OPs.None && _Token == null;

        /// <summary>
        /// Assigns this expression to be an evaluated integer
        /// </summary>
        public UInt64 IntResult
        {
            set => CacheResult(value, false);
        }
        /// <summary>
        /// Assigns this expression to be an evaluated floating-point value
        /// </summary>
        public double FloatResult
        {
            set => CacheResult(DoubleAsUInt64(value), true);
        }

        /// <summary>
        /// Caches the specified result
        /// </summary>
        /// <param name="result">the resulting value</param>
        /// <param name="floating">flag marking if result is floating-point</param>
        private void CacheResult(UInt64 result, bool floating)
        {
            // ensure this is now a leaf node
            OP = OPs.None;
            Left = Right = null;

            // discard token
            _Token = null;

            // store data
            _Result = result;
            _Floating = floating;
        }

        private bool __Evaluate__(Dictionary<string, Expr> symbols, out UInt64 res, out bool floating, ref string err, Stack<string> visited)
        {
            res = 0; // initialize out params
            floating = false;

            UInt64 L, R, Aux; // parsing locations for left and right subtrees
            bool LF, RF, AuxF;

            bool ret = true; // return value

            // switch through op
            switch (OP)
            {
                // value
                case OPs.None:
                    // if this has already been evaluated, return the cached result
                    if (Token == null) { res = _Result; floating = _Floating; return true; }

                    // if it's a number
                    if (char.IsDigit(Token[0]))
                    {
                        // remove underscores (e.g. 0b_0011_1101_1101_1111)
                        string token = Token.Replace("_", "").ToLower();

                        // -- try parsing as int -- //
                        
                        // hex prefixes
                        if (token.StartsWith("0x") || token.StartsWith("0h")) { if (token.Substring(2).TryParseUInt64(out res, 16)) break; }
                        // hex suffixes
                        else if (token[token.Length - 1] == 'x' || token[token.Length - 1] == 'h') { if (token.Substring(0, token.Length - 1).TryParseUInt64(out res, 16)) break; }

                        // dec prefixes
                        else if (token.StartsWith("0d") || token.StartsWith("0t")) { if (token.Substring(2).TryParseUInt64(out res, 10)) break; }
                        // dec suffixes
                        else if (token[token.Length - 1] == 'd' || token[token.Length - 1] == 't') { if (token.Substring(0, token.Length - 1).TryParseUInt64(out res, 10)) break; }

                        // oct prefixes
                        else if (token.StartsWith("0o") || token.StartsWith("0q")) { if (token.Substring(2).TryParseUInt64(out res, 8)) break; }
                        // oct suffixes
                        else if (token[token.Length - 1] == 'o' || token[token.Length - 1] == 'q') { if (token.Substring(0, token.Length - 1).TryParseUInt64(out res, 8)) break; }

                        // bin prefixes
                        else if (token.StartsWith("0b") || token.StartsWith("0y")) { if (token.Substring(2).TryParseUInt64(out res, 2)) break; }
                        // bin suffixes
                        else if (token[token.Length - 1] == 'b' || token[token.Length - 1] == 'y') { if (token.Substring(0, token.Length - 1).TryParseUInt64(out res, 2)) break; }

                        // otherwise is dec
                        else { if (token.TryParseUInt64(out res, 10)) break; }

                        // -- try parsing as float -- //

                        // try floating-point
                        if (double.TryParse(token, out double f)) { res = DoubleAsUInt64(f); floating = true; break; }

                        // if nothing worked, it's an ill-formed numeric literal
                        err = $"Ill-formed numeric literal encountered: \"{Token}\"";
                        return false;
                    }
                    // if it's a character constant
                    else if (Token[0] == '"' || Token[0] == '\'' || Token[0] == '`')
                    {
                        // get the characters
                        if (!Assembly.TryExtractStringChars(Token, out string chars, ref err)) return false;

                        // must be 1-8 chars
                        if (chars.Length == 0) { err = $"Ill-formed character literal encountered (empty): {Token}"; return false; }
                        if (chars.Length > 8) { err = $"Ill-formed character literal encountered (too long): {Token}"; return false; }

                        res = 0; // zero res just in case that's removed from the top of the function later on

                        // build the value
                        for (int i = 0; i < chars.Length; ++i)
                            res |= (UInt64)(chars[i] & 0xff) << (i * 8);

                        break;
                    }
                    // if it's a defined symbol we haven't already visited
                    else if (!visited.Contains(Token) && symbols.TryGetValue(Token, out Expr expr))
                    {
                        visited.Push(Token); // mark token as visited

                        // if we can't evaluate it, fail
                        if (!expr.__Evaluate__(symbols, out res, out floating, ref err, visited)) { err = $"Failed to evaluate referenced symbol \"{Token}\"\n-> {err}"; return false; }

                        visited.Pop(); // unmark token (must be done for diamond expressions i.e. a=b+c, b=d, c=d, d=0)

                        break; // break so we can resolve the reference
                    }
                    // otherwise we can't evaluate it
                    else { err = $"Failed to evaluate \"{Token}\""; return false; }

                // -- operators -- //

                // binary ops

                case OPs.Mul:
                    if (!Left.__Evaluate__(symbols, out L, out LF, ref err, visited)) ret = false;
                    if (!Right.__Evaluate__(symbols, out R, out RF, ref err, visited)) ret = false;
                    if (ret == false) return false;

                    if (LF || RF) { res = DoubleAsUInt64((LF ? AsDouble(L) : (Int64)L) * (RF ? AsDouble(R) : (Int64)R)); floating = true; }
                    else res = L * R;
                    break;

                case OPs.UDiv:
                    if (!Left.__Evaluate__(symbols, out L, out LF, ref err, visited)) ret = false;
                    if (!Right.__Evaluate__(symbols, out R, out RF, ref err, visited)) ret = false;
                    if (ret == false) return false;

                    if (LF || RF)
					{
						double _num = LF ? AsDouble(L) : (double)L;
						double _denom = RF ? AsDouble(R) : (double)R;

						// catch division by zero
						if (_denom == 0) { err = "divide by zero"; return false; }

						res = DoubleAsUInt64(_num / _denom);
						floating = true;
					}
                    else
                    {
                        // catch division by zero
                        if (R == 0) { err = "divide by zero"; return false; }
                        res = L / R;
                    }
                    break;
                case OPs.UMod:
                    if (!Left.__Evaluate__(symbols, out L, out LF, ref err, visited)) ret = false;
                    if (!Right.__Evaluate__(symbols, out R, out RF, ref err, visited)) ret = false;
                    if (ret == false) return false;

                    if (LF || RF)
                    {
						double _num = LF ? AsDouble(L) : (double)L;
						double _denom = RF ? AsDouble(R) : (double)R;

                        // catch division by zero
                        if (_denom == 0) { err = "divide by zero"; return false; }

                        res = DoubleAsUInt64(_num % _denom);
                        floating = true;
                    }
                    else
                    {
                        // catch division by zero
                        if (R == 0) { err = "divide by zero"; return false; }
                        res = L % R;
                    }
                    break;

                case OPs.SDiv:
                    if (!Left.__Evaluate__(symbols, out L, out LF, ref err, visited)) ret = false;
                    if (!Right.__Evaluate__(symbols, out R, out RF, ref err, visited)) ret = false;
                    if (ret == false) return false;

                    if (LF || RF)
					{
						double _num = LF ? AsDouble(L) : (double)(Int64)L;
						double _denom = RF ? AsDouble(R) : (double)(Int64)R;

						// catch division by zero
						if (_denom == 0) { err = "divide by zero"; return false; }

						res = DoubleAsUInt64(_num / _denom);
						floating = true;
					}
                    else
                    {
                        // catch division by zero
                        if (R == 0) { err = "divide by zero"; return false; }
                        res = (UInt64)((Int64)L / (Int64)R);
                    }
                    break;
                case OPs.SMod:
                    if (!Left.__Evaluate__(symbols, out L, out LF, ref err, visited)) ret = false;
                    if (!Right.__Evaluate__(symbols, out R, out RF, ref err, visited)) ret = false;
                    if (ret == false) return false;

                    if (LF || RF)
                    {
						double _num = LF ? AsDouble(L) : (double)(Int64)L;
						double _denom = RF ? AsDouble(R) : (double)(Int64)R;

                        // catch division by zero
                        if (_denom == 0) { err = "divide by zero"; return false; }

                        res = DoubleAsUInt64(_num % _denom);
                        floating = true;
                    }
                    else
                    {
                        // catch division by zero
                        if (R == 0) { err = "divide by zero"; return false; }

                        res = (UInt64)((Int64)L % (Int64)R);
                    }
                    break;
                case OPs.Add:
                    if (!Left.__Evaluate__(symbols, out L, out LF, ref err, visited)) ret = false;
                    if (!Right.__Evaluate__(symbols, out R, out RF, ref err, visited)) ret = false;
                    if (ret == false) return false;

                    if (LF || RF) { res = DoubleAsUInt64((LF ? AsDouble(L) : (Int64)L) + (RF ? AsDouble(R) : (Int64)R)); floating = true; }
                    else res = L + R;
                    break;
                case OPs.Sub:
                    if (!Left.__Evaluate__(symbols, out L, out LF, ref err, visited)) ret = false;
                    if (!Right.__Evaluate__(symbols, out R, out RF, ref err, visited)) ret = false;
                    if (ret == false) return false;

                    if (LF || RF) { res = DoubleAsUInt64((LF ? AsDouble(L) : (Int64)L) - (RF ? AsDouble(R) : (Int64)R)); floating = true; }
                    else res = L - R;
                    break;

                case OPs.SL:
                    if (!Left.__Evaluate__(symbols, out L, out LF, ref err, visited)) ret = false;
                    if (!Right.__Evaluate__(symbols, out R, out RF, ref err, visited)) ret = false;
                    if (ret == false) return false;

                    res = L << (ushort)R; floating = LF || RF;
                    break;
                case OPs.SR:
                    if (!Left.__Evaluate__(symbols, out L, out LF, ref err, visited)) ret = false;
                    if (!Right.__Evaluate__(symbols, out R, out RF, ref err, visited)) ret = false;
                    if (ret == false) return false;

                    res = L >> (ushort)R; floating = LF || RF;
                    break;

                case OPs.Less:
                    if (!Left.__Evaluate__(symbols, out L, out LF, ref err, visited)) ret = false;
                    if (!Right.__Evaluate__(symbols, out R, out RF, ref err, visited)) ret = false;
                    if (ret == false) return false;

                    if (LF || RF) res = (LF ? AsDouble(L) : (Int64)L) < (RF ? AsDouble(R) : (Int64)R) ? 1 : 0ul;
                    else res = (Int64)L < (Int64)R ? 1 : 0ul;
                    break;
                case OPs.LessE:
                    if (!Left.__Evaluate__(symbols, out L, out LF, ref err, visited)) ret = false;
                    if (!Right.__Evaluate__(symbols, out R, out RF, ref err, visited)) ret = false;
                    if (ret == false) return false;

                    if (LF || RF) res = (LF ? AsDouble(L) : (Int64)L) <= (RF ? AsDouble(R) : (Int64)R) ? 1 : 0ul;
                    else res = (Int64)L <= (Int64)R ? 1 : 0ul;
                    break;
                case OPs.Great:
                    if (!Left.__Evaluate__(symbols, out L, out LF, ref err, visited)) ret = false;
                    if (!Right.__Evaluate__(symbols, out R, out RF, ref err, visited)) ret = false;
                    if (ret == false) return false;

                    if (LF || RF) res = (LF ? AsDouble(L) : (Int64)L) > (RF ? AsDouble(R) : (Int64)R) ? 1 : 0ul;
                    else res = (Int64)L > (Int64)R ? 1 : 0ul;
                    break;
                case OPs.GreatE:
                    if (!Left.__Evaluate__(symbols, out L, out LF, ref err, visited)) ret = false;
                    if (!Right.__Evaluate__(symbols, out R, out RF, ref err, visited)) ret = false;
                    if (ret == false) return false;

                    if (LF || RF) res = (LF ? AsDouble(L) : (Int64)L) >= (RF ? AsDouble(R) : (Int64)R) ? 1 : 0ul;
                    else res = (Int64)L >= (Int64)R ? 1 : 0ul;
                    break;

                case OPs.Eq:
                    if (!Left.__Evaluate__(symbols, out L, out LF, ref err, visited)) ret = false;
                    if (!Right.__Evaluate__(symbols, out R, out RF, ref err, visited)) ret = false;
                    if (ret == false) return false;

                    if (LF || RF) res = (LF ? AsDouble(L) : (Int64)L) == (RF ? AsDouble(R) : (Int64)R) ? 1 : 0ul;
                    else res = L == R ? 1 : 0ul;
                    break;
                case OPs.Neq:
                    if (!Left.__Evaluate__(symbols, out L, out LF, ref err, visited)) ret = false;
                    if (!Right.__Evaluate__(symbols, out R, out RF, ref err, visited)) ret = false;
                    if (ret == false) return false;

                    if (LF || RF) res = (LF ? AsDouble(L) : (Int64)L) != (RF ? AsDouble(R) : (Int64)R) ? 1 : 0ul;
                    else res = L != R ? 1 : 0ul;
                    break;

                case OPs.BitAnd:
                    if (!Left.__Evaluate__(symbols, out L, out LF, ref err, visited)) ret = false;
                    if (!Right.__Evaluate__(symbols, out R, out RF, ref err, visited)) ret = false;
                    if (ret == false) return false;

                    res = L & R; floating = LF || RF;
                    break;
                case OPs.BitXor:
                    if (!Left.__Evaluate__(symbols, out L, out LF, ref err, visited)) ret = false;
                    if (!Right.__Evaluate__(symbols, out R, out RF, ref err, visited)) ret = false;
                    if (ret == false) return false;

                    res = L ^ R; floating = LF || RF;
                    break;
                case OPs.BitOr:
                    if (!Left.__Evaluate__(symbols, out L, out LF, ref err, visited)) ret = false;
                    if (!Right.__Evaluate__(symbols, out R, out RF, ref err, visited)) ret = false;
                    if (ret == false) return false;

                    res = L | R; floating = LF || RF;
                    break;

                case OPs.LogAnd:
                    if (!Left.__Evaluate__(symbols, out L, out LF, ref err, visited)) ret = false;
                    if (!Right.__Evaluate__(symbols, out R, out RF, ref err, visited)) ret = false;
                    if (ret == false) return false;

                    res = L != 0 ? 1 : 0ul;
                    break;
                case OPs.LogOr:
                    if (!Left.__Evaluate__(symbols, out L, out LF, ref err, visited)) ret = false;
                    if (!Right.__Evaluate__(symbols, out R, out RF, ref err, visited)) ret = false;
                    if (ret == false) return false;

                    res = L != 0 ? 1 : 0ul;
                    break;

                // unary ops

                case OPs.Neg:
                    if (!Left.__Evaluate__(symbols, out L, out LF, ref err, visited)) return false;

                    res = LF ? DoubleAsUInt64(-AsDouble(L)) : ~L + 1; floating = LF;
                    break;
                case OPs.BitNot:
                    if (!Left.__Evaluate__(symbols, out L, out LF, ref err, visited)) return false;

                    res = ~L; floating = LF;
                    break;
                case OPs.LogNot:
                    if (!Left.__Evaluate__(symbols, out L, out LF, ref err, visited)) return false;

                    res = L == 0 ? 1 : 0ul;
                    break;
                case OPs.Int:
                    if (!Left.__Evaluate__(symbols, out L, out LF, ref err, visited)) return false;

                    res = LF ? (UInt64)(Int64)AsDouble(L) : L;
                    break;
                case OPs.Float:
                    if (!Left.__Evaluate__(symbols, out L, out LF, ref err, visited)) return false;

                    res = LF ? L : DoubleAsUInt64((double)(Int64)L);
                    floating = true;
                    break;

                // misc

                case OPs.NullCoalesce:
                    if (!Left.__Evaluate__(symbols, out L, out LF, ref err, visited)) ret = false;
                    if (!Right.__Evaluate__(symbols, out R, out RF, ref err, visited)) ret = false;
                    if (ret == false) return false;

                    res = L != 0 ? L : R;
                    floating = L != 0 ? LF : RF;
                    break;
                case OPs.Condition:
                    if (!Left.__Evaluate__(symbols, out Aux, out AuxF, ref err, visited)) ret = false;
                    if (!Right.Left.__Evaluate__(symbols, out L, out LF, ref err, visited)) ret = false;
                    if (!Right.Right.__Evaluate__(symbols, out R, out RF, ref err, visited)) ret = false;
                    if (ret == false) return false;

                    res = Aux != 0 ? L : R;
                    floating = Aux != 0 ? LF : RF;
                    break;

                default: err = "Unknown operation"; return false;
            }

            // cache the result
            CacheResult(res, floating);

            return true;
        }
        /// <summary>
        /// Attempts to evaluate the hole, returning true on success
        /// </summary>
        /// <param name="symbols">the symbols table to use for lookup</param>
        /// <param name="res">the resulting value upon success</param>
        /// <param name="floating">flag denoting result is floating-point</param>
        /// <param name="err">error emitted upon failure</param>
        public bool Evaluate(Dictionary<string, Expr> symbols, out UInt64 res, out bool floating, ref string err)
        {
            // refer to helper function
            return __Evaluate__(symbols, out res, out floating, ref err, new Stack<string>());
        }
        /// <summary>
        /// Returns true if the expression is evaluatable
        /// </summary>
        /// <param name="symbols">the symbols table for lookup</param>
        public bool Evaluatable(Dictionary<string, Expr> symbols)
        {
            string err = null;
            return Evaluate(symbols, out UInt64 res, out bool floating, ref err);
        }

        /// <summary>
        /// Creates a replica of this expression tree
        public Expr Clone()
        {
            return new Expr()
            {
                OP = OP,

                Left = Left?.Clone(),
                Right = Right?.Clone(),

                _Token = _Token,
                _Result = _Result,
                _Floating = _Floating
            };
        }

        private bool _FindPath(string value, Stack<Expr> path, bool upper)
        {
            // mark ourselves as a candidate
            path.Push(this);

            // if we're a leaf test ourself
            if (OP == OPs.None)
            {
                // if we found the value, we're done
                if ((upper ? Token?.ToUpper() : Token) == value) return true;
            }
            // otherwise test children
            else
            {
                // if they found it, we're done
                if (Left._FindPath(value, path, upper) || Right != null && Right._FindPath(value, path, upper)) return true;
            }

            // otherwise we couldn't find it
            path.Pop();
            return false;
        }
        /// <summary>
        /// Finds the path to the specified value in the expression tree. Returns true on success.
        /// </summary>
        /// <param name="value">the value to find</param>
        /// <param name="path">the path to the specified value, with the value at the top of the stack and the root at the bottom</param>
        /// <param name="upper">true if the token should be converted to upper case before the comparison</param>
        public bool FindPath(string value, out Stack<Expr> path, bool upper = false)
        {
            // create the stack
            path = new Stack<Expr>();

            // refer to helper
            return _FindPath(value, path, upper);
        }
        /// <summary>
        /// Finds the path to the specified value in the expression tree. Returns true on success. This version reuses the stack object by first clearing its contents.
        /// </summary>
        /// <param name="value">the value to find</param>
        /// <param name="path">the path to the specified value, with the root at the bottom of the stack and the found node at the top</param>
        /// <param name="upper">true if the token should be converted to upper case before the comparison</param>
        public bool FindPath(string value, Stack<Expr> path, bool upper = false)
        {
            // ensure stack is empty
            path.Clear();

            // refer to helper
            return _FindPath(value, path, upper);
        }

        /// <summary>
        /// Finds the value in the specified expression tree. Returns it on success, otherwise null
        /// </summary>
        /// <param name="value">the found node or null</param>
        /// <param name="upper">true if the token should be converted to upper case before the comparison</param>
        public Expr Find(string value, bool upper = false)
        {
            // if we're a leaf, test ourself
            if (OP == OPs.None) return (upper ? Token?.ToUpper() : Token) == value ? this : null;
            // otherwise test children
            else return Left.Find(value, upper) ?? Right?.Find(value, upper);
        }

        /// <summary>
        /// Resolves all occurrences of (expr) with the specified value
        /// </summary>
        /// <param name="expr">the expression to be replaced</param>
        /// <param name="result">the value to resolve to</param>
        /// <param name="floating">marks if the value if floating point</param>
        public void Resolve(string expr, UInt64 result, bool floating)
        {
            // if we're a leaf
            if (OP == OPs.None)
            {
                // if we have this value, replace with result
                if (Token == expr) CacheResult(result, floating);
            }
            // otherwise call on children
            else
            {
                Left.Resolve(expr, result, floating);
                Right?.Resolve(expr, result, floating);
            }
        }
        /// <summary>
        /// Resolves all occurrences of (expr) with the specified value
        /// </summary>
        /// <param name="expr">the expression to be replaced</param>
        /// <param name="value">The value to replace it with</param>
        public void Resolve(string expr, string value)
        {
            // if we're a leaf
            if (OP == OPs.None)
            {
                // if we have this value, replace with result
                if (Token == expr) Token = value;
            }
            // otherwise call on children
            else
            {
                Left.Resolve(expr, value);
                Right?.Resolve(expr, value);
            }
        }

        private void _GetStringValues(List<string> vals)
        {
            // if we're a leaf
            if (OP == OPs.None)
            {
                // if we have a string value, add it
                if (Token != null) vals.Add(Token);
            }
            // otherwise call on children
            else
            {
                Left._GetStringValues(vals);
                Right?._GetStringValues(vals);
            }
        }
        /// <summary>
        /// Gets a list of all the unevaluated string values in this expression
        /// </summary>
        public List<string> GetStringValues()
        {
            // call helper with an empty list
            List<string> vals = new List<string>();
            _GetStringValues(vals);
            return vals;
        }

        /// <summary>
        /// Populates add and sub lists with terms that are strictly being added and subtracted. All items in add are being added. All items in sub are subtracted.
        /// </summary>
        /// <param name="add">the resulting added terms. should be empty before the call</param>
        /// <param name="sub">the resulting subtracted terms. should be empty before the call</param>
        public void PopulateAddSub(List<Expr> add, List<Expr> sub)
        {
            // if it's addition
            if (OP == OPs.Add)
            {
                // recurse to children
                Left.PopulateAddSub(add, sub);
                Right.PopulateAddSub(add, sub);
            }
            // if it's subtraction
            else if (OP == OPs.Sub)
            {
                // recurse to children
                Left.PopulateAddSub(add, sub);
                Right.PopulateAddSub(sub, add); // reverse add/sub lists to account for subtraction
            }
            // if it's negation
            else if (OP == OPs.Neg)
            {
                // recurse to child
                Left.PopulateAddSub(sub, add); // reverse add/sub lists to account for subtraction
            }
            // otherwise it's not part of addition or subtraction
            else
            {
                // add to addition tree
                add.Add(this);
            }
        }

        private void _ToString(StringBuilder b)
        {
            if (OP == OPs.None)
            {
                b.Append(Token == null ? _Floating ? AsDouble(_Result).ToString("e17") : ((Int64)_Result).ToString() : Token);
            }
            else
            {
                // if we're a unary op
                if (Right == null)
                {
                    b.Append(OP.ToString());

                    b.Append('(');
                    Left._ToString(b);
                    b.Append(')');
                }
                // otherwise we're a binary op
                else
                {
                    b.Append('(');
                    Left._ToString(b);
                    b.Append(')');

                    b.Append(OP.ToString());

                    b.Append('(');
                    Right._ToString(b);
                    b.Append(')');
                }
            }
        }
        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            _ToString(b);
            return b.ToString();
        }

        // ----------------------------

        /// <summary>
        /// Creates an expression tree that adds all the items
        /// </summary>
        /// <param name="items">the items to create a tree from</param>
        public static Expr ChainAddition(List<Expr> items)
        {
            // if there's nothing, return a zero
            if (items.Count == 0) return new Expr() { IntResult = 0 };
            // otherwise we have work to do
            else
            {
                Expr res = items[0]; // resulting tree begins as first item

                // for each item additionl item
                for (int i = 1; i < items.Count; ++i)
                {
                    // add this item to the tree
                    res = new Expr() { OP = Expr.OPs.Add, Left = res, Right = items[i] };
                }

                return res;
            }
        }

        /// <summary>
        /// Writes a binary representation of an expression to the stream
        /// </summary>
        /// <param name="writer">the binary writer to use</param>
        /// <param name="expr">the expression to write (can be null)</param>
        public static void WriteTo(BinaryWriter writer, Expr expr)
        {
            // write type header
            writer.Write((byte)((expr._Token != null ? 128 : 0) | (expr._Floating ? 64 : 0) | (expr.Right != null ? 32 : 0) | (int)expr.OP));

            // if it's a leaf
            if (expr.OP == OPs.None)
            {
                // if it's a token, write that
                if (expr._Token != null) writer.Write(expr._Token);
                // otherwise write the cached data
                else writer.Write(expr._Result);
            }
            // otherwise it's an expression
            else
            {
                // do left branch
                WriteTo(writer, expr.Left);
                // do right branch if non-null
                if (expr.Right != null) WriteTo(writer, expr.Right);
            }
        }
        /// <summary>
        /// Reads a binary representation of an expresion from the stream
        /// </summary>
        /// <param name="reader">the binary reader to use</param>
        /// <param name="expr">the resulting expression (can be null)</param>
        public static void ReadFrom(BinaryReader reader, out Expr expr)
        {
            // allocate the expression
            expr = new Expr();

            // read the type header
            int type = reader.ReadByte();

            // extract op
            expr.OP = (OPs)(type & 0x1f);

            // if it's a leaf
            if (expr.OP == OPs.None)
            {
                // if it's a token, read that
                if ((type & 128) != 0) expr._Token = reader.ReadString();
                // otherwise read the cached data
                else
                {
                    expr._Result = reader.ReadUInt64();
                    expr._Floating = (type & 64) != 0;
                }
            }
            // otherwise it's an expression
            else
            {
                // do left branch
                ReadFrom(reader, out expr.Left);
                // do right branch if non-null
                if ((type & 32) != 0) ReadFrom(reader, out expr.Right);
                else expr.Right = null;
            }
        }
    }
    /// <summary>
    /// Holds information on the location and value of missing pieces of information in an object file
    /// </summary>
    internal class HoleData
    {
        /// <summary>
        /// The local address of the hole in the file
        /// </summary>
        internal UInt32 Address;
        /// <summary>
        /// The size of the hole
        /// </summary>
        internal byte Size;

        /// <summary>
        /// The line where this hole was created
        /// </summary>
        internal int Line;
        /// <summary>
        /// The expression that represents this hole's value
        /// </summary>
        internal Expr Expr;

        // --------------------------

        /// <summary>
        /// Writes a binary representation of a hole to the stream
        /// </summary>
        /// <param name="writer">the binary writer to use</param>
        /// <param name="hole">the hole to write</param>
        public static void WriteTo(BinaryWriter writer, HoleData hole)
        {
            writer.Write(hole.Address);
            writer.Write(hole.Size);

            writer.Write(hole.Line);
            Expr.WriteTo(writer, hole.Expr);
        }
        /// <summary>
        /// Reads a binary representation of a hole from the stream
        /// </summary>
        /// <param name="reader">the binary reader to use</param>
        /// <param name="hole">the resulting hole</param>
        public static void ReadFrom(BinaryReader reader, out HoleData hole)
        {
            // create the hole
            hole = new HoleData();

            hole.Address = reader.ReadUInt32();
            hole.Size = reader.ReadByte();

            hole.Line = reader.ReadInt32();
            Expr.ReadFrom(reader, out hole.Expr);
        }
    }

    // -----------------------------

    public static class Assembly
    {
        private const char CommentChar = ';';
        private const char LabelDefChar = ':';

        private const string CurrentLineMacro = "$";
        private const string StartOfSegMacro = "$$";

        private static readonly Dictionary<Expr.OPs, int> Precedence = new Dictionary<Expr.OPs, int>()
        {
            { Expr.OPs.Mul, 5 },

            { Expr.OPs.UDiv, 5 },
            { Expr.OPs.UMod, 5 },

            { Expr.OPs.SDiv, 5 },
            { Expr.OPs.SMod, 5 },

            { Expr.OPs.Add, 6 },
            { Expr.OPs.Sub, 6 },

            { Expr.OPs.SL, 7 },
            { Expr.OPs.SR, 7 },

            { Expr.OPs.Less, 9 },
            { Expr.OPs.LessE, 9 },
            { Expr.OPs.Great, 9 },
            { Expr.OPs.GreatE, 9 },

            { Expr.OPs.Eq, 10 },
            { Expr.OPs.Neq, 10 },

            { Expr.OPs.BitAnd, 11 },
            { Expr.OPs.BitXor, 12 },
            { Expr.OPs.BitOr, 13 },
            { Expr.OPs.LogAnd, 14 },
            { Expr.OPs.LogOr, 15 },

            { Expr.OPs.NullCoalesce, 99 },
            { Expr.OPs.Pair, 100 },
            { Expr.OPs.Condition, 100 }
        };
        private static readonly HashSet<char> UnaryOps = new HashSet<char>() { '+', '-', '~', '!', '*', '/' };

        private static readonly Dictionary<AsmSegment, string> SegOffsets = new Dictionary<AsmSegment, string>()
        {
            [AsmSegment.TEXT] = "#t",
            [AsmSegment.RODATA] = "#r",
            [AsmSegment.DATA] = "#d",
            [AsmSegment.BSS] = "#b",
        };
        private static readonly Dictionary<AsmSegment, string> SegOrigins = new Dictionary<AsmSegment, string>()
        {
            [AsmSegment.TEXT] = "#T",
            [AsmSegment.RODATA] = "#R",
            [AsmSegment.DATA] = "#D",
            [AsmSegment.BSS] = "#B",
        };

        private static readonly HashSet<string> PtrdiffIDs = new HashSet<string>()
        {
            "#t", "#r", "#d", "#b",
            "#T", "#R", "#D", "#B",

            "__heap__",
        };

        private static readonly HashSet<string> VerifyLegalExpressionIgnores = new HashSet<string>()
        {
            "__heap__"
        };

        /// <summary>
        /// Maps CPU register names (all caps) to tuples of (id, sizecode, high)
        /// </summary>
        private static readonly Dictionary<string, Tuple<byte, byte, bool>> CPURegisterInfo = new Dictionary<string, Tuple<byte, byte, bool>>()
        {
            ["RAX"] = new Tuple<byte, byte, bool>(0, 3, false),
            ["RBX"] = new Tuple<byte, byte, bool>(1, 3, false),
            ["RCX"] = new Tuple<byte, byte, bool>(2, 3, false),
            ["RDX"] = new Tuple<byte, byte, bool>(3, 3, false),
            ["RSI"] = new Tuple<byte, byte, bool>(4, 3, false),
            ["RDI"] = new Tuple<byte, byte, bool>(5, 3, false),
            ["RBP"] = new Tuple<byte, byte, bool>(6, 3, false),
            ["RSP"] = new Tuple<byte, byte, bool>(7, 3, false),
            ["R8"] = new Tuple<byte, byte, bool>(8, 3, false),
            ["R9"] = new Tuple<byte, byte, bool>(9, 3, false),
            ["R10"] = new Tuple<byte, byte, bool>(10, 3, false),
            ["R11"] = new Tuple<byte, byte, bool>(11, 3, false),
            ["R12"] = new Tuple<byte, byte, bool>(12, 3, false),
            ["R13"] = new Tuple<byte, byte, bool>(13, 3, false),
            ["R14"] = new Tuple<byte, byte, bool>(14, 3, false),
            ["R15"] = new Tuple<byte, byte, bool>(15, 3, false),

            ["EAX"] = new Tuple<byte, byte, bool>(0, 2, false),
            ["EBX"] = new Tuple<byte, byte, bool>(1, 2, false),
            ["ECX"] = new Tuple<byte, byte, bool>(2, 2, false),
            ["EDX"] = new Tuple<byte, byte, bool>(3, 2, false),
            ["ESI"] = new Tuple<byte, byte, bool>(4, 2, false),
            ["EDI"] = new Tuple<byte, byte, bool>(5, 2, false),
            ["EBP"] = new Tuple<byte, byte, bool>(6, 2, false),
            ["ESP"] = new Tuple<byte, byte, bool>(7, 2, false),
            ["R8D"] = new Tuple<byte, byte, bool>(8, 2, false),
            ["R9D"] = new Tuple<byte, byte, bool>(9, 2, false),
            ["R10D"] = new Tuple<byte, byte, bool>(10, 2, false),
            ["R11D"] = new Tuple<byte, byte, bool>(11, 2, false),
            ["R12D"] = new Tuple<byte, byte, bool>(12, 2, false),
            ["R13D"] = new Tuple<byte, byte, bool>(13, 2, false),
            ["R14D"] = new Tuple<byte, byte, bool>(14, 2, false),
            ["R15D"] = new Tuple<byte, byte, bool>(15, 2, false),

            ["AX"] = new Tuple<byte, byte, bool>(0, 1, false),
            ["BX"] = new Tuple<byte, byte, bool>(1, 1, false),
            ["CX"] = new Tuple<byte, byte, bool>(2, 1, false),
            ["DX"] = new Tuple<byte, byte, bool>(3, 1, false),
            ["SI"] = new Tuple<byte, byte, bool>(4, 1, false),
            ["DI"] = new Tuple<byte, byte, bool>(5, 1, false),
            ["BP"] = new Tuple<byte, byte, bool>(6, 1, false),
            ["SP"] = new Tuple<byte, byte, bool>(7, 1, false),
            ["R8W"] = new Tuple<byte, byte, bool>(8, 1, false),
            ["R9W"] = new Tuple<byte, byte, bool>(9, 1, false),
            ["R10W"] = new Tuple<byte, byte, bool>(10, 1, false),
            ["R11W"] = new Tuple<byte, byte, bool>(11, 1, false),
            ["R12W"] = new Tuple<byte, byte, bool>(12, 1, false),
            ["R13W"] = new Tuple<byte, byte, bool>(13, 1, false),
            ["R14W"] = new Tuple<byte, byte, bool>(14, 1, false),
            ["R15W"] = new Tuple<byte, byte, bool>(15, 1, false),

            ["AL"] = new Tuple<byte, byte, bool>(0, 0, false),
            ["BL"] = new Tuple<byte, byte, bool>(1, 0, false),
            ["CL"] = new Tuple<byte, byte, bool>(2, 0, false),
            ["DL"] = new Tuple<byte, byte, bool>(3, 0, false),
            ["SIL"] = new Tuple<byte, byte, bool>(4, 0, false),
            ["DIL"] = new Tuple<byte, byte, bool>(5, 0, false),
            ["BPL"] = new Tuple<byte, byte, bool>(6, 0, false),
            ["SPL"] = new Tuple<byte, byte, bool>(7, 0, false),
            ["R8B"] = new Tuple<byte, byte, bool>(8, 0, false),
            ["R9B"] = new Tuple<byte, byte, bool>(9, 0, false),
            ["R10B"] = new Tuple<byte, byte, bool>(10, 0, false),
            ["R11B"] = new Tuple<byte, byte, bool>(11, 0, false),
            ["R12B"] = new Tuple<byte, byte, bool>(12, 0, false),
            ["R13B"] = new Tuple<byte, byte, bool>(13, 0, false),
            ["R14B"] = new Tuple<byte, byte, bool>(14, 0, false),
            ["R15B"] = new Tuple<byte, byte, bool>(15, 0, false),

            ["AH"] = new Tuple<byte, byte, bool>(0, 0, true),
            ["BH"] = new Tuple<byte, byte, bool>(1, 0, true),
            ["CH"] = new Tuple<byte, byte, bool>(2, 0, true),
            ["DH"] = new Tuple<byte, byte, bool>(3, 0, true)
        };
        /// <summary>
        /// Maps FPU register names (all caps) to their ids
        /// </summary>
        private static readonly Dictionary<string, byte> FPURegisterInfo = new Dictionary<string, byte>()
        {
            ["ST"] = 0,

            ["ST0"] = 0,
            ["ST1"] = 1,
            ["ST2"] = 2,
            ["ST3"] = 3,
            ["ST4"] = 4,
            ["ST5"] = 5,
            ["ST6"] = 6,
            ["ST7"] = 7,

            ["ST(0)"] = 0,
            ["ST(1)"] = 1,
            ["ST(2)"] = 2,
            ["ST(3)"] = 3,
            ["ST(4)"] = 4,
            ["ST(5)"] = 5,
            ["ST(6)"] = 6,
            ["ST(7)"] = 7
        };
        /// <summary>
        /// Maps VPU register names (all caps) to tuples of (id, sizecode)
        /// </summary>
        private static readonly Dictionary<string, Tuple<byte, byte>> VPURegisterInfo = new Dictionary<string, Tuple<byte, byte>>()
        {
            ["XMM0"] = new Tuple<byte, byte>(0, 4),
            ["XMM1"] = new Tuple<byte, byte>(1, 4),
            ["XMM2"] = new Tuple<byte, byte>(2, 4),
            ["XMM3"] = new Tuple<byte, byte>(3, 4),
            ["XMM4"] = new Tuple<byte, byte>(4, 4),
            ["XMM5"] = new Tuple<byte, byte>(5, 4),
            ["XMM6"] = new Tuple<byte, byte>(6, 4),
            ["XMM7"] = new Tuple<byte, byte>(7, 4),
            ["XMM8"] = new Tuple<byte, byte>(8, 4),
            ["XMM9"] = new Tuple<byte, byte>(9, 4),
            ["XMM10"] = new Tuple<byte, byte>(10, 4),
            ["XMM11"] = new Tuple<byte, byte>(11, 4),
            ["XMM12"] = new Tuple<byte, byte>(12, 4),
            ["XMM13"] = new Tuple<byte, byte>(13, 4),
            ["XMM14"] = new Tuple<byte, byte>(14, 4),
            ["XMM15"] = new Tuple<byte, byte>(15, 4),

            ["YMM0"] = new Tuple<byte, byte>(0, 5),
            ["YMM1"] = new Tuple<byte, byte>(1, 5),
            ["YMM2"] = new Tuple<byte, byte>(2, 5),
            ["YMM3"] = new Tuple<byte, byte>(3, 5),
            ["YMM4"] = new Tuple<byte, byte>(4, 5),
            ["YMM5"] = new Tuple<byte, byte>(5, 5),
            ["YMM6"] = new Tuple<byte, byte>(6, 5),
            ["YMM7"] = new Tuple<byte, byte>(7, 5),
            ["YMM8"] = new Tuple<byte, byte>(8, 5),
            ["YMM9"] = new Tuple<byte, byte>(9, 5),
            ["YMM10"] = new Tuple<byte, byte>(10, 5),
            ["YMM11"] = new Tuple<byte, byte>(11, 5),
            ["YMM12"] = new Tuple<byte, byte>(12, 5),
            ["YMM13"] = new Tuple<byte, byte>(13, 5),
            ["YMM14"] = new Tuple<byte, byte>(14, 5),
            ["YMM15"] = new Tuple<byte, byte>(15, 5),

            ["ZMM0"] = new Tuple<byte, byte>(0, 6),
            ["ZMM1"] = new Tuple<byte, byte>(1, 6),
            ["ZMM2"] = new Tuple<byte, byte>(2, 6),
            ["ZMM3"] = new Tuple<byte, byte>(3, 6),
            ["ZMM4"] = new Tuple<byte, byte>(4, 6),
            ["ZMM5"] = new Tuple<byte, byte>(5, 6),
            ["ZMM6"] = new Tuple<byte, byte>(6, 6),
            ["ZMM7"] = new Tuple<byte, byte>(7, 6),
            ["ZMM8"] = new Tuple<byte, byte>(8, 6),
            ["ZMM9"] = new Tuple<byte, byte>(9, 6),
            ["ZMM10"] = new Tuple<byte, byte>(10, 6),
            ["ZMM11"] = new Tuple<byte, byte>(11, 6),
            ["ZMM12"] = new Tuple<byte, byte>(12, 6),
            ["ZMM13"] = new Tuple<byte, byte>(13, 6),
            ["ZMM14"] = new Tuple<byte, byte>(14, 6),
            ["ZMM15"] = new Tuple<byte, byte>(15, 6),
            ["ZMM16"] = new Tuple<byte, byte>(16, 6),
            ["ZMM17"] = new Tuple<byte, byte>(17, 6),
            ["ZMM18"] = new Tuple<byte, byte>(18, 6),
            ["ZMM19"] = new Tuple<byte, byte>(19, 6),
            ["ZMM20"] = new Tuple<byte, byte>(20, 6),
            ["ZMM21"] = new Tuple<byte, byte>(21, 6),
            ["ZMM22"] = new Tuple<byte, byte>(22, 6),
            ["ZMM23"] = new Tuple<byte, byte>(23, 6),
            ["ZMM24"] = new Tuple<byte, byte>(24, 6),
            ["ZMM25"] = new Tuple<byte, byte>(25, 6),
            ["ZMM26"] = new Tuple<byte, byte>(26, 6),
            ["ZMM27"] = new Tuple<byte, byte>(27, 6),
            ["ZMM28"] = new Tuple<byte, byte>(28, 6),
            ["ZMM29"] = new Tuple<byte, byte>(29, 6),
            ["ZMM30"] = new Tuple<byte, byte>(30, 6),
            ["ZMM31"] = new Tuple<byte, byte>(31, 6)
        };

        /// <summary>
        /// Converts a string token into its character internals (accouting for C-style escapes in the case of `backquotes`)
        /// </summary>
        /// <param name="token">the string token to process (with quotes around it)</param>
        /// <param name="chars">the resulting character internals (without quotes around it)</param>
        /// <param name="err">the error message if there was an error</param>
        internal static bool TryExtractStringChars(string token, out string chars, ref string err)
        {
            chars = null; // null result so compiler won't complain

            // make sure it starts with a quote and is terminated
            if (token[0] != '"' && token[0] != '\'' && token[0] != '`' || token[0] != token[token.Length - 1]) { err = $"Ill-formed string: {token}"; return false; }

            StringBuilder b = new StringBuilder();

            // read all the characters inside
            for (int i = 1; i < token.Length - 1; ++i)
            {
                // if this is a `backquote` literal, it allows \escapes
                if (token[0] == '`' && token[i] == '\\')
                {
                    // bump up i and make sure it's still good
                    if (++i >= token.Length - 1) { err = $"Ill-formed string (ends with beginning of an escape sequence): {token}"; return false; }

                    int temp, temp2;
                    switch (token[i])
                    {
                        case '\'': temp = '\''; break;
                        case '"': temp = '"'; break;
                        case '`': temp = '`'; break;
                        case '\\': temp = '\\'; break;
                        case '?': temp = '?'; break;
                        case 'a': temp = '\a'; break;
                        case 'b': temp = '\b'; break;
                        case 't': temp = '\t'; break;
                        case 'n': temp = '\n'; break;
                        case 'v': temp = '\v'; break;
                        case 'f': temp = '\f'; break;
                        case 'r': temp = '\r'; break;
                        case 'e': temp = 27; break;

                        case '0':
                        case '1':
                        case '2':
                        case '3':
                        case '4':
                        case '5':
                        case '6':
                        case '7':
                            temp = 0;
                            // read the octal value into temp (up to 3 octal digits)
                            for (int oct_count = 0; oct_count < 3 && token[i] >= '0' && token[i] <= '7'; ++i, ++oct_count)
                                temp = (temp << 3) | (token[i] - '0');
                            --i; // undo the last i increment (otherwise outer loop will skip a char)
                            break;

                        case 'x':
                            // bump up i and make sure it's a hex digit
                            if (!GetHexValue(token[++i], out temp)) { err = $"Ill-formed string (invalid hexadecimal escape): {token}"; return false; }
                            // if the next char is also a hex digit
                            if (GetHexValue(token[i + 1], out temp2))
                            {
                                // read it into the escape value as well
                                ++i;
                                temp = (temp << 4) | temp2;
                            }
                            break;

                        case 'u': case 'U': err = $"Unicode character escapes are not yet supported: {token}"; return false;

                        default: err = $"Ill-formed string (escape sequence not recognized): {token}"; return false;
                    }

                    // append the character
                    b.Append((char)(temp & 0xff));
                }
                // otherwise just read the character verbatim
                else b.Append(token[i]);
            }

            // return extracted chars
            chars = b.ToString();
            return true;
        }

        /// <summary>
        /// Gets the smallest size code that will support the unsigned value
        /// </summary>
        /// <param name="val">the value to test</param>
        private static UInt64 SmallestUnsignedSizeCode(UInt64 val)
        {
            // filter through till we get a size that will contain it
            if (val <= 0xfful) return 0;
            else if (val <= 0xfffful) return 1;
            else if (val <= 0xfffffffful) return 2;
            else return 3;
        }

        /// <summary>
        /// Renames "from" to "to" in the object file. The symbol to rename may be internal or external.
        /// The object file is assumed to be complete and verified. The symbol must not have already been evaluated.
        /// Throws <see cref="ArgumentException"/> if "to" already exists or if "from" has already been evaluated (because it may have already been linked to other expressions).
        /// </summary>
        /// <param name="file">the file to act on</param>
        /// <param name="from">the original name</param>
        /// <param name="to">the resulting name</param>
        /// <exception cref="ArgumentException"></exception>
        private static void RenameSymbol(this ObjectFile file, string from, string to)
        {
            // make sure "to" doesn't already exist
            if (file.Symbols.ContainsKey(to) || file.ExternalSymbols.Contains(to))
                throw new ArgumentException($"Attempt to rename symbol \"{from}\" to \"{to}\" (already exists)");

            // if it's a symbol defined in this file
            if (file.Symbols.TryGetValue(from, out Expr expr))
            {
                // make sure it hasn't already been evaluated (because it may have already been linked to other expressions)
                if (expr.IsEvaluated) throw new ArgumentException($"Attempt to rename symbol \"{from}\" to \"{to}\" (already evaluated)");

                // rename the symbol
                file.Symbols.Add(to, expr);
                file.Symbols.Remove(from);

                // find and replace in global table (may not be global - that's ok)
                for (int i = 0; i < file.GlobalSymbols.Count; ++i)
                    if (file.GlobalSymbols[i] == from)
                    {
                        file.GlobalSymbols[i] = to;
                        break;
                    }
            }
            // if it's a symbol defined externally
            else if (file.ExternalSymbols.Contains(from))
            {
                // find and replace in global table
                for (int i = 0; i < file.ExternalSymbols.Count; ++i)
                    if (file.ExternalSymbols[i] == from)
                    {
                        file.ExternalSymbols[i] = to;
                        break;
                    }
            }
            // otherwise we don't know what it is
            else throw new ArgumentException($"Attempt to rename symbol \"{from}\" to \"{to}\" (does not exist)");

            // -- now the easy part -- //

            // find and replace in symbol table expressions
            foreach (var entry in file.Symbols) entry.Value.Resolve(from, to);

            // find and replace in hole expressions
            foreach (var entry in file.TextHoles) entry.Expr.Resolve(from, to);
            foreach (var entry in file.RodataHoles) entry.Expr.Resolve(from, to);
            foreach (var entry in file.DataHoles) entry.Expr.Resolve(from, to);
        }

        /// <summary>
        /// Holds all the variables used during assembly
        /// </summary>
        private class AssembleArgs
        {
            // -- data -- //

            public ObjectFile file;

            public UInt64 time;

            public AsmSegment current_seg;
            public AsmSegment done_segs;

            public int line;
            public UInt64 line_pos_in_seg;

            public string last_nonlocal_label;

            public string label_def;
            public string op;
            public string[] args; // must be array for ref params

            public AssembleResult res;

            // -- Assembly Functions -- //

            /// <summary>
            /// Splits the raw line into its separate components. The raw line should not have a comment section.
            /// </summary>
            /// <param name="rawline">the raw line to parse</param>
            public bool SplitLine(string rawline)
            {
                // (label:) (op (arg, arg, ...))

                int pos = 0, end; // position in line parsing
                int quote;        // index of openning quote in args

                List<string> args = new List<string>();

                // -- parse label and op -- //

                // skip leading white space
                for (; pos < rawline.Length && char.IsWhiteSpace(rawline[pos]); ++pos) ;
                // get a white space-delimited token
                for (end = pos; end < rawline.Length && !char.IsWhiteSpace(rawline[end]); ++end) ;

                // if we got a label
                if (pos < rawline.Length && rawline[end - 1] == LabelDefChar)
                {
                    // set as label def
                    label_def = rawline.Substring(pos, end - pos - 1);

                    // get another token for op to use

                    // skip leading white space
                    for (pos = end; pos < rawline.Length && char.IsWhiteSpace(rawline[pos]); ++pos) ;
                    // get a white space-delimited token
                    for (end = pos; end < rawline.Length && !char.IsWhiteSpace(rawline[end]); ++end) ;
                }
                // otherwise there's no label for this line
                else label_def = null;

                // if we got something, record as op, otherwise is empty string
                op = pos < rawline.Length ? rawline.Substring(pos, end - pos) : string.Empty;

                // -- parse args -- //

                // parse the rest of the line as comma-separated tokens
                while (true)
                {
                    // skip leading white space
                    for (pos = end + 1; pos < rawline.Length && char.IsWhiteSpace(rawline[pos]); ++pos) ;
                    // when pos reaches end of token, we're done parsing
                    if (pos >= rawline.Length) break;

                    // find the next terminator (comma-separated)
                    for (end = pos, quote = -1; end < rawline.Length; ++end)
                    {
                        if (rawline[end] == '"' || rawline[end] == '\'' || rawline[end] == '`') quote = quote < 0 ? end : rawline[end] == rawline[quote] ? -1 : quote;
                        else if (quote < 0 && rawline[end] == ',') break; // comma marks end of token
                    }
                    // make sure we closed any quotations
                    if (quote >= 0) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Unmatched quotation encountered in argument list"); return false; }

                    // get the arg (remove leading/trailing white space - some logic requires them not be there e.g. address parser)
                    string arg = rawline.Substring(pos, end - pos).Trim();
                    // make sure arg isn't empty
                    if (arg.Length == 0) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Empty operation argument encountered"); return false; }
                    // add this token
                    args.Add(arg);
                }
                // output tokens to assemble args
                this.args = args.ToArray();

                // successfully parsed line
                return true;
            }

            public static bool IsValidName(string token, ref string err)
            {
                // can't be empty string
                if (token.Length == 0) { err = $"Symbol name was empty string"; return false; }

                // first char is underscore or letter
                if (token[0] != '_' && !char.IsLetter(token[0])) { err = $"Symbol contained an illegal character: {token}"; return false; }
                // all other chars may additionally be numbers or periods
                for (int i = 1; i < token.Length; ++i)
                    if (token[i] != '_' && token[i] != '.' && !char.IsLetterOrDigit(token[i])) { err = $"Symbol contained an illegal character: {token}"; return false; }

                return true;
            }
            public bool MutateName(ref string label)
            {
                // if defining a local label
                if (label[0] == '.')
                {
                    string sub = label.Substring(1); // local symbol name
                    string err = null;

                    // local name can't be empty
                    if (!IsValidName(sub, ref err)) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: {err}"); return false; }
                    // can't make a local symbol before any non-local ones exist
                    if (last_nonlocal_label == null) { res = new AssembleResult(AssembleError.InvalidLabel, $"line {line}: Local symbol encountered before any non-local declarations"); return false; }

                    // mutate the label
                    label = last_nonlocal_label + label;
                }

                return true;
            }

            public bool TryReserve(UInt64 size)
            {
                // reserve only works in the bss segment
                if (current_seg != AsmSegment.BSS) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Attempt to reserve space outside of the BSS segment"); return false; }

                // reserve the space
                file.BssLen += (UInt32)size;
                return true;
            }
            public bool TryAppendVal(UInt64 size, UInt64 val)
            {
                switch (current_seg)
                {
                    // text and ro/data segments are writable
                    case AsmSegment.TEXT: file.Text.Append(size, val); return true;
                    case AsmSegment.RODATA: file.Rodata.Append(size, val); return true;
                    case AsmSegment.DATA: file.Data.Append(size, val); return true;

                    // others are not
                    default: res = new AssembleResult(AssembleError.FormatError, $"line {line}: Attempt to write to the {current_seg} segment"); return false;
                }
            }
            public bool TryAppendByte(byte val)
            {
                switch (current_seg)
                {
                    // text and ro/data segments are writable
                    case AsmSegment.TEXT: file.Text.Add(val); return true;
                    case AsmSegment.RODATA: file.Rodata.Add(val); return true;
                    case AsmSegment.DATA: file.Data.Add(val); return true;

                    // others are not
                    default: res = new AssembleResult(AssembleError.FormatError, $"line {line}: Attempt to write to the {current_seg} segment"); return false;
                }
            }

            private bool TryAppendExpr(UInt64 size, Expr expr, List<HoleData> holes, List<byte> segment)
            {
                string err = null; // evaluation error parsing location

                // create the hole data
                HoleData data = new HoleData() { Address = (UInt32)segment.Count, Size = (byte)size, Line = line, Expr = expr };
                // write a dummy (all 1's for easy manual identification)
                if (!TryAppendVal(size, 0xffffffffffffffff)) return false;

                // try to patch it
                switch (TryPatchHole(segment, file.Symbols, data, ref err))
                {
                    case PatchError.None: break;
                    case PatchError.Unevaluated: holes.Add(data); break;
                    case PatchError.Error: res = new AssembleResult(AssembleError.ArgError, $"line {line}: {err}"); return false;

                    default: throw new ArgumentException("Unknown patch error encountered");
                }

                return true;
            }
            public bool TryAppendExpr(UInt64 size, Expr expr)
            {
                switch (current_seg)
                {
                    case AsmSegment.TEXT: return TryAppendExpr(size, expr, file.TextHoles, file.Text);
                    case AsmSegment.RODATA: return TryAppendExpr(size, expr, file.RodataHoles, file.Rodata);
                    case AsmSegment.DATA: return TryAppendExpr(size, expr, file.DataHoles, file.Data);

                    default: res = new AssembleResult(AssembleError.FormatError, $"line {line}: Attempt to write to the {current_seg} segment"); return false;
                }
            }
            public bool TryAppendAddress(UInt64 a, UInt64 b, Expr hole)
            {
                if (!TryAppendVal(1, a)) return false;
                if ((a & 3) != 0) { if (!TryAppendVal(1, b)) return false; }
                if ((a & 0x80) != 0) { if (!TryAppendExpr(Size((a >> 2) & 3), hole)) return false; }

                return true;
            }

            public bool TryAlign(UInt64 size)
            {
                // it's really important that size is a power of 2, so do a (hopefully redundant) check
                if (!size.IsPowerOf2()) throw new ArgumentException("alignment size must be a power of 2");

                switch (current_seg)
                {
                    case AsmSegment.TEXT:
                        file.Text.Align(size);
                        file.TextAlign = (UInt32)Math.Max(file.TextAlign, size);
                        return true;
                    case AsmSegment.RODATA:
                        file.Rodata.Align(size);
                        file.RodataAlign = (UInt32)Math.Max(file.RodataAlign, size);
                        return true;
                    case AsmSegment.DATA:
                        file.Data.Align(size);
                        file.DataAlign = (UInt32)Math.Max(file.DataAlign, size);
                        return true;
                    case AsmSegment.BSS:
                        file.BssLen = (UInt32)Align(file.BssLen, size);
                        file.BSSAlign = (UInt32)Math.Max(file.BSSAlign, size);
                        return true;

                    default: res = new AssembleResult(AssembleError.FormatError, $"line {line}: Attempt to align the {current_seg} segment"); return false;
                }
            }
            public bool TryPad(UInt64 size)
            {
                switch (current_seg)
                {
                    case AsmSegment.TEXT: file.Text.Pad(size); return true;
                    case AsmSegment.RODATA: file.Rodata.Pad(size); return true;
                    case AsmSegment.DATA: file.Data.Pad(size); return true;
                    case AsmSegment.BSS: file.BssLen += (UInt32)size; return true;

                    default: res = new AssembleResult(AssembleError.FormatError, $"line {line}: Attempt to pad the {current_seg} segment"); return false;
                }
            }

            public static bool TryGetOp(string token, int pos, out Expr.OPs op, out int oplen)
            {
                // default to invalid op
                op = Expr.OPs.None;
                oplen = 0;

                // try to take as many characters as possible (greedy)
                if (pos + 2 <= token.Length)
                {
                    oplen = 2; // record oplen
                    switch (token.Substring(pos, 2))
                    {
                        case "//": op = Expr.OPs.SDiv; return true;
                        case "%%": op = Expr.OPs.SMod; return true;

                        case "<<": op = Expr.OPs.SL; return true;
                        case ">>": op = Expr.OPs.SR; return true;

                        case "<=": op = Expr.OPs.LessE; return true;
                        case ">=": op = Expr.OPs.GreatE; return true;

                        case "==": op = Expr.OPs.Eq; return true;
                        case "!=": op = Expr.OPs.Neq; return true;

                        case "&&": op = Expr.OPs.LogAnd; return true;
                        case "||": op = Expr.OPs.LogOr; return true;

                        case "??": op = Expr.OPs.NullCoalesce; return true;
                    }
                }
                if (pos + 1 <= token.Length)
                {
                    oplen = 1; // record oplen
                    switch (token[pos])
                    {
                        case '*': op = Expr.OPs.Mul; return true;

                        case '/': op = Expr.OPs.UDiv; return true;
                        case '%': op = Expr.OPs.UMod; return true;

                        case '+': op = Expr.OPs.Add; return true;
                        case '-': op = Expr.OPs.Sub; return true;

                        case '<': op = Expr.OPs.Less; return true;
                        case '>': op = Expr.OPs.Great; return true;

                        case '&': op = Expr.OPs.BitAnd; return true;
                        case '^': op = Expr.OPs.BitXor; return true;
                        case '|': op = Expr.OPs.BitOr; return true;

                        case '?': op = Expr.OPs.Condition; return true;
                        case ':': op = Expr.OPs.Pair; return true;
                    }
                }

                // if nothing found, fail
                return false;
            }
            private bool __TryParseImm(string token, out Expr expr)
            {
                expr = null; // initially-nulled result

                Expr temp; // temporary for node creation

                int pos = 0, end; // position in token

                bool binPair = false;          // marker if tree contains complete binary pairs (i.e. N+1 values and N binary ops)
                int unpaired_conditionals = 0; // number of unpaired conditional ops

                Expr.OPs op = Expr.OPs.None; // extracted binary op (initialized so compiler doesn't complain)
                int oplen = 0;               // length of operator found (in characters)

                string err = null; // error location for hole evaluation

                Stack<char> unaryOps = new Stack<char>(8); // holds unary ops for processing
                Stack<Expr> stack = new Stack<Expr>();     // the stack used to manage operator precedence rules

                // top of stack shall be refered to as current

                stack.Push(null); // stack will always have a null at its base (simplifies code slightly)

                // skip white space
                for (; pos < token.Length && char.IsWhiteSpace(token[pos]); ++pos) ;
                // if we're past the end, token was empty
                if (pos >= token.Length) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Empty expression encountered"); return false; }

                while (true)
                {
                    // -- read (unary op...)[operand](binary op) -- //

                    // consume unary ops (allows white space)
                    for (; pos < token.Length; ++pos)
                    {
                        if (UnaryOps.Contains(token[pos])) unaryOps.Push(token[pos]); // absorb unary ops
                        else if (!char.IsWhiteSpace(token[pos])) break; // non-white is start of operand
                    }
                    // if we're past the end, there were unary ops with no operand
                    if (pos >= token.Length) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Unary ops encountered without an operand"); return false; }

                    int depth = 0;  // parens depth - initially 0
                    int quote = -1; // index of current quote char - initially not in one

                    bool numeric = char.IsDigit(token[pos]); // flag if this is a numeric literal

                    // move end to next logical separator (white space or binary op)
                    for (end = pos; end < token.Length; ++end)
                    {
                        // if we're not in a quote
                        if (quote < 0)
                        {
                            // account for important characters
                            if (token[end] == '(') ++depth;
                            else if (token[end] == ')') --depth; // depth control
                            else if (numeric && (token[end] == 'e' || token[end] == 'E') && end + 1 < token.Length && (token[end + 1] == '+' || token[end + 1] == '-')) ++end; // make sure an exponent sign won't be parsed as binary + or - by skipping it
                            else if (token[end] == '"' || token[end] == '\'' || token[end] == '`') quote = end; // quotes mark start of a string
                            else if (depth == 0 && (char.IsWhiteSpace(token[end]) || TryGetOp(token, end, out op, out oplen))) break; // break on white space or binary op

                            // can't ever have negative depth
                            if (depth < 0) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Mismatched parenthesis: {token}"); return false; }
                        }
                        // otherwise we're in a quote
                        else
                        {
                            // if we have a matching quote, break out of quote mode
                            if (token[end] == token[quote]) quote = -1;
                        }
                    }
                    // if depth isn't back to 0, there was a parens mismatch
                    if (depth != 0) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Mismatched parenthesis: {token}"); return false; }
                    // if quote isn't back to -1, there was a quote mismatch
                    if (quote >= 0) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Mismatched quotation: {token}"); return false; }
                    // if pos == end we'll have an empty token (e.g. expression was just a binary op)
                    if (pos == end) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Empty token encountered in expression: {token}"); return false; }

                    // -- convert to expression tree -- //

                    // if sub-expression
                    if (token[pos] == '(')
                    {
                        // parse the inside into temp
                        if (!__TryParseImm(token.Substring(pos + 1, end - pos - 2), out temp)) return false;
                    }
                    // otherwise is value
                    else
                    {
                        // get the value to insert
                        string val = token.Substring(pos, end - pos);

                        // mutate it
                        if (!MutateName(ref val)) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Failed to parse imm \"{token}\"\n-> {res.ErrorMsg}"); return false; }

                        // if it's the current line macro
                        if (val == CurrentLineMacro)
                        {
                            // must be in a segment
                            if (current_seg == AsmSegment.INVALID) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Attempt to take an address outside of a segment"); return false; }

                            temp = new Expr() { OP = Expr.OPs.Add, Left = new Expr() { Token = SegOffsets[current_seg] }, Right = new Expr() { IntResult = line_pos_in_seg } };
                        }
                        // if it's the start of segment macro
                        else if (val == StartOfSegMacro)
                        {
                            // must be in a segment
                            if (current_seg == AsmSegment.INVALID) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Attempt to take an address outside of a segment"); return false; }

                            temp = new Expr() { Token = SegOrigins[current_seg] };
                        }
                        // otherwise it's a normal value/symbol
                        else
                        {
                            // create the hole for it
                            temp = new Expr() { Token = val };

                            // it either needs to be evaluatable or a valid label name
                            if (!temp.Evaluate(file.Symbols, out UInt64 _res, out bool floating, ref err) && !IsValidName(val, ref err))
                            { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Failed to resolve token as a valid imm or symbol name: {val}\n-> {err}"); return false; }
                        }
                    }

                    // handle parsed unary ops (stack provides right-to-left evaluation)
                    while (unaryOps.Count > 0)
                    {
                        char uop = unaryOps.Pop();
                        switch (uop)
                        {
                            case '+': break; // unary plus does nothing
                            case '-': temp = new Expr() { OP = Expr.OPs.Neg, Left = temp }; break;
                            case '~': temp = new Expr() { OP = Expr.OPs.BitNot, Left = temp }; break;
                            case '!': temp = new Expr() { OP = Expr.OPs.LogNot, Left = temp }; break;
                            case '*': temp = new Expr() { OP = Expr.OPs.Float, Left = temp }; break;
                            case '/': temp = new Expr() { OP = Expr.OPs.Int, Left = temp }; break;

                            default: throw new NotImplementedException($"unary op \'{uop}\' not implemented");
                        }
                    }

                    // -- append subtree to main tree --

                    // if no tree yet, use this one
                    if (expr == null) expr = temp;
                    // otherwise append to current (guaranteed to be defined by second pass)
                    else
                    {
                        // put it in the right (guaranteed by this algorithm to be empty)
                        stack.Peek().Right = temp;
                    }

                    // flag as a valid binary pair (i.e. every binary op now has 2 operands)
                    binPair = true;

                    // -- get binary op -- //

                    // we may have stopped token parsing on white space, so wind up to find a binary op
                    for (; end < token.Length; ++end)
                    {
                        if (TryGetOp(token, end, out op, out oplen)) break; // break when we find an op
                        // if we hit a non-white character, there are tokens with no binary ops between them
                        else if (!char.IsWhiteSpace(token[end])) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Encountered two tokens with no binary op between them: {token}"); return false; }
                    }
                    // if we didn't find any binary ops, we're done
                    if (end >= token.Length) break;

                    // -- process binary op -- //

                    // ternary conditional has special rules
                    if (op == Expr.OPs.Pair)
                    {
                        // seek out nearest conditional without a pair
                        for (; stack.Peek() != null && (stack.Peek().OP != Expr.OPs.Condition || stack.Peek().Right.OP == Expr.OPs.Pair); stack.Pop()) ;
                        // if we didn't find anywhere to put it, this is an error
                        if (stack.Peek() == null) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Expression contained a ternary conditional pair without a corresponding condition: {token}"); return false; }
                    }
                    // right-to-left operators
                    else if (op == Expr.OPs.Condition)
                    {
                        // wind current up to correct precedence (right-to-left evaluation, so don't skip equal precedence)
                        for (; stack.Peek() != null && Precedence[stack.Peek().OP] < Precedence[op]; stack.Pop()) ;
                    }
                    // left-to-right operators
                    else
                    {
                        // wind current up to correct precedence (left-to-right evaluation, so also skip equal precedence)
                        for (; stack.Peek() != null && Precedence[stack.Peek().OP] <= Precedence[op]; stack.Pop()) ;
                    }

                    // if we have a valid current
                    if (stack.Peek() != null)
                    {
                        // splice in the new operator, moving current's right sub-tree to left of new node
                        stack.Push(stack.Peek().Right = new Expr() { OP = op, Left = stack.Peek().Right });
                    }
                    // otherwise we'll have to move the root
                    else
                    {
                        // splice in the new operator, moving entire tree to left of new node
                        stack.Push(expr = new Expr() { OP = op, Left = expr });
                    }

                    binPair = false; // flag as invalid binary pair

                    // update unpaired conditionals
                    if (op == Expr.OPs.Condition) ++unpaired_conditionals;
                    else if (op == Expr.OPs.Pair) --unpaired_conditionals;

                    // pass last delimiter
                    pos = end + oplen;
                }

                // handle binary pair mismatch
                if (!binPair) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Expression contained a mismatched binary op: {token}"); return false; }
                // make sure all conditionals were matched
                if (unpaired_conditionals != 0) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Expression contained {unpaired_conditionals} incomplete ternary {(unpaired_conditionals == 1 ? "conditional" : "conditionals")}: {token}"); return false; }

                // run ptrdiff logic on result
                expr = Ptrdiff(expr);

                return true;
            }
            public bool TryParseImm(string token, out Expr expr, out UInt64 sizecode, out bool explicit_size)
            {
                sizecode = 3; explicit_size = false; // initially no explicit size

                // handle explicit sizes directives
                string utoken = token.ToUpper();
                if (utoken.StartsWithToken("BYTE")) { sizecode = 0; explicit_size = true; token = token.Substring(4).TrimStart(); }
                else if (utoken.StartsWithToken("WORD")) { sizecode = 1; explicit_size = true; token = token.Substring(4).TrimStart(); }
                else if (utoken.StartsWithToken("DWORD")) { sizecode = 2; explicit_size = true; token = token.Substring(5).TrimStart(); }
                else if (utoken.StartsWithToken("QWORD")) { sizecode = 3; explicit_size = true; token = token.Substring(5).TrimStart(); }

                // refer to helper
                return __TryParseImm(token, out expr);
            }
            public bool TryParseInstantImm(string token, out UInt64 val, out bool floating, out UInt64 sizecode, out bool explicit_size)
            {
                string err = null; // error location for evaluation

                if (!TryParseImm(token, out Expr hole, out sizecode, out explicit_size)) { val = 0; floating = false; return false; }
                if (!hole.Evaluate(file.Symbols, out val, out floating, ref err)) { res = new AssembleResult(AssembleError.ArgError, $"line {line}: Failed to evaluate instant imm: {token}\n-> {err}"); return false; }

                return true;
            }

            /// <summary>
            /// Attempts to extract the numeric portion of a standard label: val in (#base + val). Returns true on success
            /// </summary>
            /// <param name="expr">the expression representing the label (either the label itself or a token expression reference to it)</param>
            /// <param name="val">the resulting value portion</param>
            private bool TryExtractPtrVal(Expr expr, out Expr val, string _base)
            {
                // if this is a leaf
                if (expr.OP == Expr.OPs.None)
                {
                    // if there's no token, fail (not a pointer)
                    if (expr.Token == null) { val = null; return false; }

                    // if this is the #base offset itself, value is zero (this can happen with the current line macro)
                    if (expr.Token == _base) { val = new Expr(); return true; }

                    // otherwise get the symbol
                    if (!file.Symbols.TryGetValue(expr.Token, out expr)) { val = null; return false; }
                }

                // must be of standard label form
                if (expr.OP != Expr.OPs.Add || expr.Left.Token != _base) { val = null; return false; }

                // return the value portion
                val = expr.Right;
                return true;
            }
            /// <summary>
            /// Performs pointer difference arithmetic on the expression tree and returns the result
            /// </summary>
            /// <param name="expr">the expression tree to operate on</param>
            private Expr Ptrdiff(Expr expr)
            {
                // on null, return null as well
                if (expr == null) return null;

                List<Expr> add = new List<Expr>(); // list of added terms
                List<Expr> sub = new List<Expr>(); // list of subtracted terms

                Expr a = null, b = null; // expression temporaries (initiailzed so compiler won't complain)

                // populate lists
                expr.PopulateAddSub(add, sub);

                // perform ptrdiff reduction on anything defined by the linker
                foreach (string seg_name in PtrdiffIDs)
                {
                    for (int i = 0, j = 0; ; ++i, ++j)
                    {
                        // wind i up to next add label
                        for (; i < add.Count && !TryExtractPtrVal(add[i], out a, seg_name); ++i) ;
                        // if this exceeds bounds, break
                        if (i == add.Count) break;

                        // wind j up to next sub label
                        for (; j < sub.Count && !TryExtractPtrVal(sub[j], out b, seg_name); ++j) ;
                        // if this exceeds bounds, break
                        if (j == sub.Count) break;

                        // we got a pair: replace items in add/sub with their pointer values
                        add[i] = a;
                        sub[j] = b;
                    }
                }

                // for each add item
                for (int i = 0; i < add.Count; ++i)
                {
                    // if it's not a leaf
                    if (!add[i].IsLeaf)
                    {
                        // recurse on children
                        add[i] = new Expr() { OP = add[i].OP, Left = Ptrdiff(add[i].Left), Right = Ptrdiff(add[i].Right) };
                    }
                }
                // for each sub item
                for (int i = 0; i < sub.Count; ++i)
                {
                    // if it's not a leaf
                    if (!sub[i].IsLeaf)
                    {
                        // recurse on children
                        sub[i] = new Expr() { OP = sub[i].OP, Left = Ptrdiff(sub[i].Left), Right = Ptrdiff(sub[i].Right) };
                    }
                }

                // stitch together the new tree
                if (sub.Count == 0) return Expr.ChainAddition(add);
                else if (add.Count == 0) return new Expr() { OP = Expr.OPs.Neg, Left = Expr.ChainAddition(sub) };
                else return new Expr() { OP = Expr.OPs.Sub, Left = Expr.ChainAddition(add), Right = Expr.ChainAddition(sub) };
            }

            /// <summary>
            /// Attempts to parse an imm that has a prefix. If the imm is a compound expression, it must be parenthesized
            /// </summary>
            /// <param name="token">token to parse</param>
            /// <param name="prefix">the prefix the imm is required to have</param>
            /// <param name="val">resulting value</param>
            /// <param name="floating">results in true if val is floating-point</param>
            public bool TryParseInstantPrefixedImm(string token, string prefix, out UInt64 val, out bool floating, out UInt64 sizecode, out bool explicit_size)
            {
                val = sizecode = 0;
                floating = explicit_size = false;

                // must begin with prefix
                if (!token.StartsWith(prefix)) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Token did not start with \"{prefix}\" prefix: \"{token}\""); return false; }
                // aside from the prefix, must not be empty
                if (token.Length == prefix.Length) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Empty token encountered after \"{prefix}\" prefix: \"{token}\""); return false; }

                int end; // ending of expression token

                // if this starts parenthetical region
                if (token[prefix.Length] == '(')
                {
                    int depth = 1; // depth of 1

                    // start searching for ending parens after first parens
                    for (end = prefix.Length + 1; end < token.Length && depth > 0; ++end)
                    {
                        if (token[end] == '(') ++depth;
                        else if (token[end] == ')') --depth;
                    }

                    // make sure we reached zero depth
                    if (depth != 0) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Mismatched parenthesis in prefixed expression \"{token}\""); return false; }
                }
                // otherwise normal symbol
                else
                {
                    // take all legal chars
                    for (end = prefix.Length; end < token.Length && (char.IsLetterOrDigit(token[end]) || token[end] == '_' || token[end] == '.'); ++end) ;
                }

                // make sure we consumed the entire string
                if (end != token.Length) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Compound expressions used as prefixed expressions must be parenthesized \"{token}\""); return false; }

                // prefix index must be instant imm
                if (!TryParseInstantImm(token.Substring(prefix.Length), out val, out floating, out sizecode, out explicit_size)) { res = new AssembleResult(AssembleError.ArgError, $"line {line}: Failed to parse instant prefixed imm \"{token}\"\n-> {res.ErrorMsg}"); return false; }

                return true;
            }

            public bool TryParseCPURegister(string token, out UInt64 reg, out UInt64 sizecode, out bool high)
            {
                // copy data if we can parse it
                if (CPURegisterInfo.TryGetValue(token.ToUpper(), out var info))
                {
                    reg = info.Item1;
                    sizecode = info.Item2;
                    high = info.Item3;
                    return true;
                }
                // otherwise it's not a cpu register
                else
                {
                    res = new AssembleResult(AssembleError.FormatError, $"line {line}: Failed to parse as cpu register: {token}");
                    reg = sizecode = 0;
                    high = false;
                    return false;
                }
            }
            public bool TryParseFPURegister(string token, out UInt64 reg)
            {
                if (FPURegisterInfo.TryGetValue(token.ToUpper(), out var info))
                {
                    reg = info;
                    return true;
                }
                else
                {
                    res = new AssembleResult(AssembleError.FormatError, $"line {line}: Failed to parse as fpu register: {token}");
                    reg = 0;
                    return false;
                }
            }
            public bool TryParseVPURegister(string token, out UInt64 reg, out UInt64 sizecode)
            {
                // copy data if we can parse it
                if (VPURegisterInfo.TryGetValue(token.ToUpper(), out var info))
                {
                    reg = info.Item1;
                    sizecode = info.Item2;
                    return true;
                }
                // otherwise it's not a vpu register
                else
                {
                    res = new AssembleResult(AssembleError.FormatError, $"line {line}: Failed to parse as vpu register: {token}");
                    reg = sizecode = 0;
                    return false;
                }
            }

            private bool TryGetRegMult(string label, ref Expr hole, out UInt64 mult_res)
            {
                mult_res = 0; // mult_res starts at zero (for logic below)

                Stack<Expr> path = new Stack<Expr>();
                List<Expr> list = new List<Expr>();

                string err = string.Empty; // evaluation error

                // while we can find this symbol
                while (hole.FindPath(label, path, true))
                {
                    // move path into list
                    while (path.Count > 0) list.Add(path.Pop());

                    // if it doesn't have a mult section
                    if (list.Count == 1 || list.Count > 1 && list[1].OP != Expr.OPs.Mul)
                    {
                        // add in a multiplier of 1
                        list[0].OP = Expr.OPs.Mul;
                        list[0].Left = new Expr() { IntResult = 1 };
                        list[0].Right = new Expr() { Token = list[0].Token };

                        // insert new register location as beginning of path
                        list.Insert(0, list[0].Right);
                    }

                    // start 2 above (just above regular mult code)
                    for (int i = 2; i < list.Count;)
                    {
                        switch (list[i].OP)
                        {
                            case Expr.OPs.Add: case Expr.OPs.Sub: case Expr.OPs.Neg: ++i; break;

                            case Expr.OPs.Mul:
                                {
                                    // toward leads to register, mult leads to mult value
                                    Expr toward = list[i - 1], mult = list[i].Left == list[i - 1] ? list[i].Right : list[i].Left;

                                    // if pos is add/sub, we need to distribute
                                    if (toward.OP == Expr.OPs.Add || toward.OP == Expr.OPs.Sub)
                                    {
                                        // swap operators with toward
                                        list[i].OP = toward.OP;
                                        toward.OP = Expr.OPs.Mul;

                                        // create the distribution node
                                        Expr temp = new Expr() { OP = Expr.OPs.Mul, Left = mult };

                                        // compute right and transfer mult to toward
                                        if (toward.Left == list[i - 2]) { temp.Right = toward.Right; toward.Right = mult; }
                                        else { temp.Right = toward.Left; toward.Left = mult; }

                                        // add it in
                                        if (list[i].Left == mult) list[i].Left = temp; else list[i].Right = temp;
                                    }
                                    // if pos is mul, we need to combine with pre-existing mult code
                                    else if (toward.OP == Expr.OPs.Mul)
                                    {
                                        // create the combination node
                                        Expr temp = new Expr() { OP = Expr.OPs.Mul, Left = mult, Right = toward.Left == list[i - 2] ? toward.Right : toward.Left };

                                        // add it in
                                        if (list[i].Left == mult)
                                        {
                                            list[i].Left = temp; // replace mult with combination
                                            list[i].Right = list[i - 2]; // bump up toward
                                        }
                                        else
                                        {
                                            list[i].Right = temp;
                                            list[i].Left = list[i - 2];
                                        }

                                        // remove the skipped list[i - 1]
                                        list.RemoveAt(i - 1);
                                    }
                                    // if pos is neg, we need to put the negative on the mult
                                    else if (toward.OP == Expr.OPs.Neg)
                                    {
                                        // create the combinartion node
                                        Expr temp = new Expr() { OP = Expr.OPs.Neg, Left = mult };

                                        // add it in
                                        if (list[i].Left == mult)
                                        {
                                            list[i].Left = temp; // replace mult with combination
                                            list[i].Right = list[i - 2]; // bump up toward
                                        }
                                        else
                                        {
                                            list[i].Right = temp;
                                            list[i].Left = list[i - 2];
                                        }

                                        // remove the skipped list[i - 1]
                                        list.RemoveAt(i - 1);
                                    }
                                    // otherwise something horrible happened (this should never happen, but is left in for sanity-checking and future-proofing)
                                    else throw new ArgumentException($"Unknown address simplification step: {toward.OP}");

                                    --i; // decrement i to follow the multiplication all the way down the rabbit hole
                                    if (i < 2) i = 2; // but if it gets under the starting point, reset it

                                    break;
                                }

                            default: res = new AssembleResult(AssembleError.FormatError, $"line {line}: Register may not be connected by {list[i].OP}"); return false;
                        }
                    }

                    // -- finally done with all the algebra -- //

                    // extract mult code fragment
                    if (!(list[1].Left == list[0] ? list[1].Right : list[1].Left).Evaluate(file.Symbols, out UInt64 val, out bool floating, ref err))
                    { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Failed to evaluate register multiplier as an instant imm\n-> {err}"); return false; }
                    // make sure it's not floating-point
                    if (floating) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Register multiplier may not be floating-point"); return false; }

                    // look through from top to bottom
                    for (int i = list.Count - 1; i >= 2; --i)
                    {
                        // if this will negate the register
                        if (list[i].OP == Expr.OPs.Neg || list[i].OP == Expr.OPs.Sub && list[i].Right == list[i - 1])
                        {
                            // negate found partial mult
                            val = ~val + 1;
                        }
                    }

                    // remove the register section from the expression (replace with integral 0)
                    list[1].IntResult = 0;

                    mult_res += val; // add extracted mult to total mult
                    list.Clear(); // clear list for next pass
                }

                // register successfully parsed
                return true;
            }
            public bool TryParseAddress(string token, out UInt64 a, out UInt64 b, out Expr ptr_base, out UInt64 sizecode, out bool explicit_size)
            {
                a = b = 0; ptr_base = null;
                sizecode = 0; explicit_size = false;

                // account for exlicit sizecode prefix
                string utoken = token.ToUpper();
                if (utoken.StartsWithToken("BYTE")) { sizecode = 0; explicit_size = true; utoken = utoken.Substring(4).TrimStart(); }
                else if (utoken.StartsWithToken("WORD")) { sizecode = 1; explicit_size = true; utoken = utoken.Substring(4).TrimStart(); }
                else if (utoken.StartsWithToken("DWORD")) { sizecode = 2; explicit_size = true; utoken = utoken.Substring(5).TrimStart(); }
                else if (utoken.StartsWithToken("QWORD")) { sizecode = 3; explicit_size = true; utoken = utoken.Substring(5).TrimStart(); }
                else if (utoken.StartsWithToken("XMMWORD")) { sizecode = 4; explicit_size = true; utoken = utoken.Substring(7).TrimStart(); }
                else if (utoken.StartsWithToken("YMMWORD")) { sizecode = 5; explicit_size = true; utoken = utoken.Substring(7).TrimStart(); }
                else if (utoken.StartsWithToken("ZMMWORD")) { sizecode = 6; explicit_size = true; utoken = utoken.Substring(7).TrimStart(); }

                // if there was an explicit size
                if (explicit_size)
                {
                    // CSX64 uses the DWORD PTR syntax, so now we need to start with PTR
                    if (!utoken.StartsWith("PTR")) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Explicit memory operand size encountered without the PTR designator"); return false; }

                    // take all of that stuff off of token
                    token = token.Substring(token.Length - utoken.Length + 3).TrimStart();
                }

                // must be of [*] format
                if (token.Length < 3 || token[0] != '[' || token[token.Length - 1] != ']') { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Invalid address format encountered: {token}"); return false; }

                UInt64 m1 = 0, r1 = UInt64.MaxValue, r2 = UInt64.MaxValue, sz; // final register info - maxval denotes no value - m1 must default to 0 - sz defaults to 64-bit in the case that there's only an imm ptr_base
                bool explicit_sz; // denotes that the ptr_base sizecode is explicit

                // extract the address internals
                token = token.Substring(1, token.Length - 2);

                // turn into an expression
                if (!TryParseImm(token, out ptr_base, out sz, out explicit_sz)) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Failed to parse address expression\n-> {res.ErrorMsg}"); return false; }

                // look through all the register names
                foreach (var entry in CPURegisterInfo)
                {
                    // extract the register data
                    if (!TryGetRegMult(entry.Key, ref ptr_base, out UInt64 mult)) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Failed to extract register data\n-> {res.ErrorMsg}"); return false; }

                    // if the register is present we need to do something with it
                    if (mult != 0)
                    {
                        // if we have an explicit address component size to enforce
                        if (explicit_sz)
                        {
                            // if this conflicts with the current one, it's an error
                            if (sz != entry.Value.Item2) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Encountered address components of conflicting sizes"); return false; }
                        }
                        // otherwise record this as the size to enforce
                        else { sz = entry.Value.Item2; explicit_sz = true; }
                    }

                    // if the multiplier is trivial or has a trivial component
                    if ((mult & 1) != 0)
                    {
                        mult &= ~(UInt64)1; // remove the trivial component

                        // if r2 is empty, put it there
                        if (r2 == UInt64.MaxValue) r2 = entry.Value.Item1;
                        // then try r1
                        else if (r1 == UInt64.MaxValue) r1 = entry.Value.Item1;
                        // otherwise we ran out of registers to use
                        else { res = new AssembleResult(AssembleError.FormatError, $"line {line}: An address expression may use up to 2 registers"); return false; }
                    }

                    // if a non-trivial multiplier is present
                    if (mult != 0)
                    {
                        // decode the mult code into m1
                        switch (mult)
                        {
                            // (mult 1 is trivial and thus handled above)
                            case 2: m1 = 1; break;
                            case 4: m1 = 2; break;
                            case 8: m1 = 3; break;

                            default: res = new AssembleResult(AssembleError.UsageError, $"line {line}: Register multiplier must be 1, 2, 4, or 8. Got {(Int64)mult}*{entry.Key}"); return false;
                        }

                        // if r1 is empty, put it there
                        if (r1 == UInt64.MaxValue) r1 = entry.Value.Item1;
                        // otherwise we don't have anywhere to put it
                        else { res = new AssembleResult(AssembleError.FormatError, $"line {line}: An address expression may only use one non-trivial multiplier"); return false; }
                    }
                }

                // -- apply final touches -- //

                // if we still don't have an explicit address size code, use 64-bit
                if (!explicit_sz) sz = 3;
                // 8-bit addressing is not allowed
                else if (sz == 0) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: 8-bit addressing is not allowed"); return false; }

                // if we can evaluate the hole to zero, there is no hole (null it)
                if (ptr_base.Evaluate(file.Symbols, out UInt64 _temp, out bool _btemp, ref utoken) && _temp == 0) ptr_base = null;

                // [1: imm][1:][2: mult_1][2: size][1: r1][1: r2]   ([4: r1][4: r2])   ([size: imm])

                a = (ptr_base != null ? 0x80 : 0ul) | (m1 << 4) | (sz << 2) | (r1 != UInt64.MaxValue ? 2 : 0ul) | (r2 != UInt64.MaxValue ? 1 : 0ul);
                b = (r1 != UInt64.MaxValue ? r1 << 4 : 0ul) | (r2 != UInt64.MaxValue ? r2 : 0ul);

                // address successfully parsed
                return true;
            }

            public bool VerifyLegalExpression(Expr expr)
            {
                // if it's a leaf, it must be something that is defined
                if (expr.IsLeaf)
                {
                    // if it's already been evaluated or we know about it somehow, we're good
                    if (expr.IsEvaluated || file.Symbols.ContainsKey(expr.Token) || file.ExternalSymbols.Contains(expr.Token)
                        || SegOffsets.ContainsValue(expr.Token) || SegOrigins.ContainsValue(expr.Token)
                        || VerifyLegalExpressionIgnores.Contains(expr.Token))
                        return true;
                    // otherwise we don't know what it is
                    else { res = new AssembleResult(AssembleError.UnknownSymbol, $"Unknown symbol: {expr.Token}"); return false; }
                }
                // otherwise children must be legal
                else return VerifyLegalExpression(expr.Left) && (expr.Right == null || VerifyLegalExpression(expr.Right));
            }
            /// <summary>
            /// Ensures that all is good in the hood. Returns true if the hood is good
            /// </summary>
            public bool VerifyIntegrity()
            {
                // make sure all global symbols were actually defined prior to link-time
                foreach (string global in file.GlobalSymbols)
                    if (!file.Symbols.ContainsKey(global)) { res = new AssembleResult(AssembleError.UnknownSymbol, $"Global symbol was never defined: {global}"); return false; }

                // make sure all symbol expressions were valid
                foreach (var entry in file.Symbols) if (!VerifyLegalExpression(entry.Value)) return false;

                // make sure all hole expressions were valid
                foreach (HoleData hole in file.TextHoles) if (!VerifyLegalExpression(hole.Expr)) return false;
                foreach (HoleData hole in file.RodataHoles) if (!VerifyLegalExpression(hole.Expr)) return false;
                foreach (HoleData hole in file.DataHoles) if (!VerifyLegalExpression(hole.Expr)) return false;

                // the hood is good
                return true;
            }

            // -- misc -- //

            public bool IsReservedSymbol(string symbol)
            {
                // make the symbol uppercase (all reserved symbols are case insensitive)
                symbol = symbol.ToUpper();

                // check against register dictionaries
                if (CPURegisterInfo.ContainsKey(symbol) || FPURegisterInfo.ContainsKey(symbol) || VPURegisterInfo.ContainsKey(symbol)) return true;

                // check against special tokens
                switch (symbol)
                {
                    // size directives
                    case "BYTE":
                    case "WORD":
                    case "DWORD":
                    case "QWORD":
                    case "XMMWORD":
                    case "YMMWORD":
                    case "ZMMWORD":

                    // potential future size directives
                    case "OWORD":
                    case "TWORD":

                        return true;

                    default: return false;
                }
            }
            public bool TryProcessLabel()
            {
                if (label_def != null)
                {
                    string err = null;

                    // ensure it's not empty
                    if (label_def.Length == 0) { res = new AssembleResult(AssembleError.InvalidLabel, $"line {line}: Empty label encountered"); return false; }

                    // if it's not a local, mark as last non-local label
                    if (label_def[0] != '.') last_nonlocal_label = label_def;

                    // mutate and test result for legality
                    if (!MutateName(ref label_def)) return false;
                    if (!IsValidName(label_def, ref err)) { res = new AssembleResult(AssembleError.InvalidLabel, $"line {line}: {err}"); return false; }

                    // it can't be a reserved symbol
                    if (IsReservedSymbol(label_def)) { res = new AssembleResult(AssembleError.InvalidLabel, $"line {line}: Symbol name is reserved: {label_def}"); return false; }

                    // ensure we don't redefine a symbol
                    if (file.Symbols.ContainsKey(label_def)) { res = new AssembleResult(AssembleError.SymbolRedefinition, $"line {line}: Symbol was already defined: {label_def}"); return false; }
                    // ensure we don't define an external
                    if (file.ExternalSymbols.Contains(label_def)) { res = new AssembleResult(AssembleError.SymbolRedefinition, $"line {line}: Cannot define external symbol internally: {label_def}"); return false; }

                    // if it's not an EQU expression, inject a label
                    if (op.ToUpper() != "EQU")
                    {
                        // addresses must be in a valid segment
                        if (current_seg == AsmSegment.INVALID) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Attempt to address outside of a segment"); return false; }

                        file.Symbols.Add(label_def, new Expr() { OP = Expr.OPs.Add, Left = new Expr() { Token = SegOffsets[current_seg] }, Right = new Expr() { IntResult = line_pos_in_seg } });
                    }
                }

                return true;
            }

            public bool TryProcessAlignXX(UInt64 size)
            {
                if (args.Length != 0) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Expected no operands"); return false; }

                return TryAlign(size);
            }
            public bool TryProcessAlign()
            {
                if (args.Length != 1) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Expected 1 operand"); return false; }

                if (!TryParseInstantImm(args[0], out UInt64 val, out bool floating, out UInt64 sizecode, out bool explicit_size)) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Alignment value must be instant\n-> {res.ErrorMsg}"); return false; }
                if (floating) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Alignment value cannot be floating-point"); return false; }
                if (val == 0) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Attempt to align to a multiple of zero"); return false; }

                if (!val.IsPowerOf2()) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Alignment value must be a power of 2. Got {val}"); return false; }

                return TryAlign(val);
            }

            public bool TryProcessGlobal()
            {
                if (args.Length == 0) { res = new AssembleResult(AssembleError.ArgCount, $"line {line}: Expected at least one symbol to export"); return false; }

                string err = null;
                foreach (string symbol in args)
                {
                    // special error message for using global on local labels
                    if (symbol[0] == '.') { res = new AssembleResult(AssembleError.ArgError, $"line {line}: Cannot export local symbols without their full declaration"); return false; }
                    // test name for legality
                    if (!IsValidName(symbol, ref err)) { res = new AssembleResult(AssembleError.InvalidLabel, $"line {line}: {err}"); return false; }

                    // don't add to global list twice
                    if (file.GlobalSymbols.Contains(symbol)) { res = new AssembleResult(AssembleError.SymbolRedefinition, $"line {line}: Attempt to export \"{symbol}\" multiple times"); return false; }
                    // ensure we don't global an external
                    if (file.ExternalSymbols.Contains(symbol)) { res = new AssembleResult(AssembleError.SymbolRedefinition, $"line {line}: Cannot define external \"{symbol}\" as global"); return false; }

                    // add it to the globals list
                    file.GlobalSymbols.Add(symbol);
                }

                return true;
            }
            public bool TryProcessExtern()
            {
                if (args.Length == 0) { res = new AssembleResult(AssembleError.ArgCount, $"line {line}: Expected at least one symbol to import"); return false; }

                string err = null;
                foreach (string symbol in args)
                {
                    // special error message for using extern on local labels
                    if (symbol[0] == '.') { res = new AssembleResult(AssembleError.ArgError, $"line {line}: Cannot import local symbols"); return false; }
                    // test name for legality
                    if (!IsValidName(symbol, ref err)) { res = new AssembleResult(AssembleError.InvalidLabel, $"line {line}: {err}"); return false; }

                    // ensure we don't extern a symbol that already exists
                    if (file.Symbols.ContainsKey(symbol)) { res = new AssembleResult(AssembleError.SymbolRedefinition, $"line {line}: Cannot define symbol \"{symbol}\" (defined internally) as external"); return false; }

                    // don't add to external list twice
                    if (file.ExternalSymbols.Contains(symbol)) { res = new AssembleResult(AssembleError.SymbolRedefinition, $"line {line}: Attempt to import \"{symbol}\" multiple times"); return false; }
                    // ensure we don't extern a global
                    if (file.GlobalSymbols.Contains(symbol)) { res = new AssembleResult(AssembleError.SymbolRedefinition, $"line {line}: Cannot define global \"{symbol}\" as external"); return false; }

                    // add it to the external list
                    file.ExternalSymbols.Add(symbol);
                }

                return true;
            }

            public bool TryProcessDeclare(UInt64 size)
            {
                if (args.Length == 0) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Expected at least 1 value to write"); return false; }

                Expr expr;
                string chars, err = null;

                // for each argument (not using foreach because order is incredibly important and i'm paranoid)
                for (int i = 0; i < args.Length; ++i)
                {
                    // if it's a string
                    if (args[i][0] == '"' || args[i][0] == '\'' || args[i][0] == '`')
                    {
                        // get its chars
                        if (!TryExtractStringChars(args[i], out chars, ref err)) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Invalid string literal: {args[i]}\n-> {err}"); return false; }

                        // dump into memory (one byte each)
                        for (int j = 0; j < chars.Length; ++j) if (!TryAppendByte((byte)chars[j])) return false;
                        // make sure we write a multiple of size
                        if (!TryPad(AlignOffset((UInt64)chars.Length, size))) return false;
                    }
                    // otherwise it's a value
                    else
                    {
                        // can only use standard sizes
                        if (size > 8) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Attempt to write a numeric value in an unsuported format"); return false; }

                        // get the value
                        if (!TryParseImm(args[i], out expr, out UInt64 sizecode, out bool explicit_size)) return false;
                        if (explicit_size) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: A size directive in this context is not allowed"); return false; }

                        // write the value
                        if (!TryAppendExpr(size, expr)) return false;
                    }
                }

                return true;
            }
            public bool TryProcessReserve(UInt64 size)
            {
                if (args.Length != 1) { res = new AssembleResult(AssembleError.ArgCount, $"line {line}: Reserve expected one arg"); return false; }

                // parse the number to reserve
                if (!TryParseInstantImm(args[0], out UInt64 count, out bool floating, out UInt64 sizecode, out bool explicit_size)) return false;
                if (floating) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Reserve count cannot be floating-point"); return false; }
                if (explicit_size) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: A size directive in this context is not allowed"); return false; }

                // reserve the space
                if (!TryReserve(count * size)) return false;

                return true;
            }

            public bool TryProcessEQU()
            {
                if (args.Length != 1) { res = new AssembleResult(AssembleError.ArgCount, $"line {line}: Expected 1 operand"); return false; }

                // make sure we have a label on this line
                if (label_def == null) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Expected a label declaration to link to the value"); return false; }

                // get the expression
                if (!TryParseImm(args[0], out Expr expr, out UInt64 sizecode, out bool explicit_size)) return false;
                if (explicit_size) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: A size directive in this context is not allowed"); return false; }

                // inject the symbol
                file.Symbols.Add(label_def, expr);

                return true;
            }

            public bool TryProcessSegment()
            {
                if (args.Length != 1) { res = new AssembleResult(AssembleError.ArgCount, $"line {line}: Expected 1 operand"); return false; }

                // get the segment we're going to
                switch (args[0].ToUpper())
                {
                    case ".TEXT": current_seg = AsmSegment.TEXT; break;
                    case ".RODATA": current_seg = AsmSegment.RODATA; break;
                    case ".DATA": current_seg = AsmSegment.DATA; break;
                    case ".BSS": current_seg = AsmSegment.BSS; break;

                    default: res = new AssembleResult(AssembleError.ArgError, $"line {line}: Unknown segment: {args[0]}"); return false;
                }

                // if this segment has already been done, fail
                if ((done_segs & current_seg) != 0) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Attempt to redeclare segment {current_seg}"); return false; }
                // add to list of completed segments
                done_segs |= current_seg;

                // we don't want to have cross-segment local symbols
                last_nonlocal_label = null;

                return true;
            }

            // -- x86 op formats -- //
            
            public bool TryProcessTernaryOp(OPCode op, bool has_ext_op = false, byte ext_op = 0, UInt64 sizemask = 15)
            {
                if (args.Length != 3) { res = new AssembleResult(AssembleError.ArgCount, $"line {line}: Expected 3 args"); return false; }

                // write op code
                if (!TryAppendByte((byte)op)) return false;
                if (has_ext_op) { if (!TryAppendByte(ext_op)) return false; }

                if (!TryParseCPURegister(args[0], out UInt64 dest, out UInt64 a_sizecode, out bool dest_high)) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Expected cpu register as first operand"); return false; }
                if (!TryParseImm(args[2], out Expr imm, out UInt64 imm_sz, out bool imm_sz_explicit)) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Expected imm as third operand"); return false; }

                if (imm_sz_explicit && imm_sz != a_sizecode) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Operand size mismatch"); return false; }
                if ((Size(a_sizecode) & sizemask) == 0) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Specified size is not supported"); return false; }

                // reg
                if (TryParseCPURegister(args[1], out UInt64 reg, out UInt64 b_sizecode, out bool reg_high))
                {
                    if (a_sizecode != b_sizecode) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Operand size missmatch"); return false; }

                    if (!TryAppendVal(1, (dest << 4) | (a_sizecode << 2) | (dest_high ? 2 : 0ul) | 0)) return false;
                    if (!TryAppendExpr(Size(a_sizecode), imm)) return false;
                    if (!TryAppendVal(1, (reg_high ? 128 : 0ul) | reg)) return false;
                }
                // mem
                else if (args[1][args[1].Length - 1] == ']')
                {
                    if (!TryParseAddress(args[1], out UInt64 a, out UInt64 b, out Expr ptr_base, out b_sizecode, out bool explicit_size)) return false;

                    if (explicit_size && a_sizecode != b_sizecode) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Operand size missmatch"); return false; }

                    if (!TryAppendVal(1, (dest << 4) | (a_sizecode << 2) | (dest_high ? 2 : 0ul) | 1)) return false;
                    if (!TryAppendExpr(Size(a_sizecode), imm)) return false;
                    if (!TryAppendAddress(a, b, ptr_base)) return false;
                }
                // imm
                else { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Expected cpu register or memory value as second operand"); return false; }

                return true;
            }
            public bool TryProcessBinaryOp(OPCode op, bool has_ext_op = false, byte ext_op = 0, UInt64 sizemask = 15, int _force_b_imm_sizecode = -1)
            {
                if (args.Length != 2) { res = new AssembleResult(AssembleError.ArgCount, $"line {line}: Expected 2 operands"); return false; }

                // write op code
                if (!TryAppendByte((byte)op)) return false;
                if (has_ext_op) { if (!TryAppendByte(ext_op)) return false; }

                // reg, *
                if (TryParseCPURegister(args[0], out UInt64 dest, out UInt64 a_sizecode, out bool dest_high))
                {
                    if ((Size(a_sizecode) & sizemask) == 0) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Specified size not supported"); return false; }

                    // reg, reg
                    if (TryParseCPURegister(args[1], out UInt64 src, out UInt64 b_sizecode, out bool src_high))
                    {
                        if (a_sizecode != b_sizecode) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Operand size missmatch"); return false; }

                        if (!TryAppendVal(1, (dest << 4) | (a_sizecode << 2) | (dest_high ? 2 : 0ul) | (src_high ? 1 : 0ul))) return false;
                        if (!TryAppendVal(1, src)) return false;
                    }
                    // reg, mem
                    else if (args[1][args[1].Length - 1] == ']')
                    {
                        if (!TryParseAddress(args[1], out UInt64 a, out UInt64 b, out Expr ptr_base, out b_sizecode, out bool explicit_size)) return false;

                        if (explicit_size && a_sizecode != b_sizecode) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Operand size missmatch"); return false; }

                        if (!TryAppendVal(1, (dest << 4) | (a_sizecode << 2) | (dest_high ? 2 : 0ul))) return false;
                        if (!TryAppendVal(1, (2 << 4))) return false;
                        if (!TryAppendAddress(a, b, ptr_base)) return false;
                    }
                    // reg, imm
                    else
                    {
                        if (!TryParseImm(args[1], out Expr imm, out b_sizecode, out bool explicit_size)) return false;

                        // fix up size codes
                        if (_force_b_imm_sizecode == -1)
                        {
                            if (explicit_size) { if (a_sizecode != b_sizecode) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Operand size missmatch"); return false; } }
                            else b_sizecode = a_sizecode;
                        }
                        else b_sizecode = (UInt64)_force_b_imm_sizecode;

                        if (!TryAppendVal(1, (dest << 4) | (a_sizecode << 2) | (dest_high ? 2 : 0ul))) return false;
                        if (!TryAppendVal(1, (1 << 4))) return false;
                        if (!TryAppendExpr(Size(b_sizecode), imm)) return false;
                    }
                }
                // mem, *
                else if (args[0][args[0].Length - 1] == ']')
                {
                    if (!TryParseAddress(args[0], out UInt64 a, out UInt64 b, out Expr ptr_base, out a_sizecode, out bool a_explicit)) return false;

                    // mem, reg
                    if (TryParseCPURegister(args[1], out UInt64 src, out UInt64 b_sizecode, out bool src_high))
                    {
                        if ((Size(b_sizecode) & sizemask) == 0) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Specified size not supported"); return false; }

                        if (a_explicit && a_sizecode != b_sizecode) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Argument size missmatch"); return false; }

                        if (!TryAppendVal(1, (b_sizecode << 2) | (src_high ? 1 : 0ul))) return false;
                        if (!TryAppendVal(1, (3 << 4) | src)) return false;
                        if (!TryAppendAddress(a, b, ptr_base)) return false;
                    }
                    // mem, mem
                    else if (args[1][args[1].Length - 1] == ']') { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Only one operand may be a memory value"); return false; }
                    // mem, imm
                    else
                    {
                        if (!TryParseImm(args[1], out Expr imm, out b_sizecode, out bool b_explicit)) return false;

                        // fix up the size codes
                        if (_force_b_imm_sizecode == -1)
                        {
                            if (a_explicit && b_explicit) { if (a_sizecode != b_sizecode) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Operand size mismatch"); return false; } }
                            else if (b_explicit) a_sizecode = b_sizecode;
                            else if (a_explicit) b_sizecode = a_sizecode;
                            else { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Could not deduce operand size"); return false; }
                        }
                        else b_sizecode = (UInt64)_force_b_imm_sizecode;

                        if ((Size(a_sizecode) & sizemask) == 0) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Specified size not supported"); return false; }

                        if (!TryAppendVal(1, a_sizecode << 2)) return false;
                        if (!TryAppendVal(1, 4 << 4)) return false;
                        if (!TryAppendAddress(a, b, ptr_base)) return false;
                        if (!TryAppendExpr(Size(b_sizecode), imm)) return false;
                    }
                }
                // imm, *
                else { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Expected cpu register or memory value as first operand"); return false; }

                return true;
            }
            public bool TryProcessUnaryOp(OPCode op, bool has_ext_op = false, byte ext_op = 0, UInt64 sizemask = 15)
            {
                if (args.Length != 1) { res = new AssembleResult(AssembleError.ArgCount, $"line {line}: Expected 1 operand"); return false; }

                // write op code
                if (!TryAppendByte((byte)op)) return false;
                if (has_ext_op) { if (!TryAppendByte(ext_op)) return false; }

                // reg
                if (TryParseCPURegister(args[0], out UInt64 reg, out UInt64 a_sizecode, out bool reg_high))
                {
                    if ((Size(a_sizecode) & sizemask) == 0) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Specified size not supported"); return false; }

                    if (!TryAppendVal(1, (reg << 4) | (a_sizecode << 2) | (reg_high ? 2 : 0ul))) return false;
                }
                // mem
                else if (args[0][args[0].Length - 1] == ']')
                {
                    if (!TryParseAddress(args[0], out UInt64 a, out UInt64 b, out Expr ptr_base, out a_sizecode, out bool explicit_size)) return false;

                    if (!explicit_size) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Could not deduce operand size"); return false; }

                    if ((Size(a_sizecode) & sizemask) == 0) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Specified size not supported"); return false; }

                    if (!TryAppendVal(1, (a_sizecode << 2) | 1)) return false;
                    if (!TryAppendAddress(a, b, ptr_base)) return false;
                }
                // imm
                else { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Expected a cpu register or memory value"); return false; }

                return true;
            }
            public bool TryProcessIMMRM(OPCode op, bool has_ext_op = false, byte ext_op = 0, UInt64 sizemask = 15, int default_sizecode = -1)
            {
                if (args.Length != 1) { res = new AssembleResult(AssembleError.ArgCount, $"line {line}: Expected 1 operand"); return false; }

                // write op code
                if (!TryAppendByte((byte)op)) return false;
                if (has_ext_op) { if (!TryAppendByte(ext_op)) return false; }

                // reg
                if (TryParseCPURegister(args[0], out UInt64 reg, out UInt64 a_sizecode, out bool reg_high))
                {
                    if ((Size(a_sizecode) & sizemask) == 0) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Specified size not supported"); return false; }

                    if (!TryAppendVal(1, (reg << 4) | (a_sizecode << 2) | (reg_high ? 1 : 0ul))) return false;
                }
                // mem
                else if (args[0][args[0].Length - 1] == ']')
                {
                    if (!TryParseAddress(args[0], out UInt64 a, out UInt64 b, out Expr ptr_base, out a_sizecode, out bool explicit_size)) return false;

                    if (!explicit_size)
                    {
                        if (default_sizecode == -1) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Could not deduce operand size"); return false; }
                        else a_sizecode = (UInt64)default_sizecode;
                    }
                    if ((Size(a_sizecode) & sizemask) == 0) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Specified size not supported"); return false; }

                    if (!TryAppendVal(1, (a_sizecode << 2) | 3)) return false;
                    if (!TryAppendAddress(a, b, ptr_base)) return false;
                }
                // imm
                else
                {
                    if (!TryParseImm(args[0], out Expr imm, out a_sizecode, out bool explicit_size)) return false;

                    if (!explicit_size)
                    {
                        if (default_sizecode == -1) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Could not deduce operand size"); return false; }
                        else a_sizecode = (UInt64)default_sizecode;
                    }
                    if ((Size(a_sizecode) & sizemask) == 0) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Specified size not supported"); return false; }

                    if (!TryAppendVal(1, (a_sizecode << 2) | 2)) return false;
                    if (!TryAppendExpr(Size(a_sizecode), imm)) return false;
                }

                return true;
            }
            public bool TryProcessRR_RM(OPCode op, bool has_ext_op = false, byte ext_op = 0, UInt64 sizemask = 15)
            {
                if (args.Length != 3) { res = new AssembleResult(AssembleError.ArgCount, $"line {line}: Expected 3 operands"); return false; }

                // write op code
                if (!TryAppendByte((byte)op)) return false;
                if (has_ext_op) { if (!TryAppendByte(ext_op)) return false; }

                // reg, *, *
                if (TryParseCPURegister(args[0], out UInt64 dest, out UInt64 sizecode, out bool dest_high))
                {
                    // apply size mask
                    if ((Size(sizecode) & sizemask) == 0) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Specified size not supported"); return false; }

                    // reg, reg, *
                    if (TryParseCPURegister(args[1], out UInt64 src_1, out UInt64 src_1_sizecode, out bool src_1_high))
                    {
                        // reg, reg, reg
                        if (TryParseCPURegister(args[2], out UInt64 src_2, out UInt64 src_2_sizecode, out bool src_2_high))
                        {
                            if (sizecode != src_1_sizecode || sizecode != src_2_sizecode) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Operand size mismatch"); return false; }

                            if (!TryAppendVal(1, (dest << 4) | (sizecode << 2) | (dest_high ? 2 : 0ul) | 0)) return false;
                            if (!TryAppendVal(1, (src_1_high ? 128 : 0ul) | src_1)) return false;
                            if (!TryAppendVal(1, (src_2_high ? 128 : 0ul) | src_2)) return false;
                        }
                        // reg, reg, mem
                        else if (args[2][args[2].Length - 1] == ']')
                        {
                            if (!TryParseAddress(args[2], out UInt64 a, out UInt64 b, out Expr ptr_base, out src_2_sizecode, out bool src_2_explicit_size)) return false;

                            if (sizecode != src_1_sizecode || src_2_explicit_size && sizecode != src_2_sizecode) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Operand size mismatch"); return false; }

                            if (!TryAppendVal(1, (dest << 4) | (sizecode << 2) | (dest_high ? 2 : 0ul) | 1)) return false;
                            if (!TryAppendVal(1, (src_1_high ? 128 : 0ul) | src_1)) return false;
                            if (!TryAppendAddress(a, b, ptr_base)) return false;
                        }
                        else { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Third operand must be a cpu register or memory value"); return false; }
                    }
                    else { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Second operand must be a cpu register"); return false; }
                }
                else { res = new AssembleResult(AssembleError.UsageError, $"line {line}: First operand must be a cpu register"); return false; }

                return true;
            }

            public bool TryProcessBinaryOp_NoBMem(OPCode op, bool has_ext_op = false, byte ext_op = 0, UInt64 sizemask = 15, int _force_b_imm_sizecode = -1)
            {
                // b can't be memory
                if (args.Length > 1 && args[1][args[1].Length - 1] == ']') { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Second operand may not be a memory value"); return false; }

                // otherwise refer to binary formatter
                return TryProcessBinaryOp(op, has_ext_op, ext_op, sizemask, _force_b_imm_sizecode);
            }
            public bool TryProcessBinaryOp_R_RM(OPCode op, bool has_ext_op = false, byte ext_op = 0, UInt64 sizemask = 15, int _force_b_imm_sizecode = -1)
            {
                // a must be register
                if (args.Length > 0 && !TryParseCPURegister(args[0], out UInt64 reg, out UInt64 sz, out bool high)) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: First operand must be a cpu register"); return false; }
                // b must be register or memory
                if (args.Length > 1 && !TryParseCPURegister(args[1], out reg, out sz, out high) && args[1][args[1].Length - 1] != ']') { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Second operand must be a cpu register or memory value"); return false; }

                // otherwise refer to binary formatter
                return TryProcessBinaryOp(op, has_ext_op, ext_op, sizemask, _force_b_imm_sizecode);
            }

            public bool TryProcessNoArgOp(OPCode op, bool has_ext_op = false, byte ext_op = 0)
            {
                if (args.Length != 0) { res = new AssembleResult(AssembleError.ArgCount, $"line {line}: Expected no operands"); return false; }

                // write op code
                if (!TryAppendByte((byte)op)) return false;
                if (has_ext_op) { if (!TryAppendByte(ext_op)) return false; }

                return true;
            }
            public bool TryProcessNoArgOp_no_write()
            {
                // only check to make sure there were no operands
                if (args.Length != 0) { res = new AssembleResult(AssembleError.ArgCount, $"line {line}: Expected no operands"); return false; }
                return true;
            }

            public bool TryProcessXCHG(OPCode op)
            {
                if (args.Length != 2) { res = new AssembleResult(AssembleError.ArgCount, $"line {line}: Expected 2 operands"); return false; }

                // write op code
                if (!TryAppendByte((byte)op)) return false;

                // reg, *
                if (TryParseCPURegister(args[0], out UInt64 reg, out UInt64 a_sizecode, out bool reg_high))
                {
                    // reg, reg
                    if (TryParseCPURegister(args[1], out UInt64 src, out UInt64 b_sizecode, out bool src_high))
                    {
                        if (a_sizecode != b_sizecode) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Operand size missmatch"); return false; }

                        if (!TryAppendVal(1, (reg << 4) | (a_sizecode << 2) | (reg_high ? 2 : 0ul) | 0)) return false;
                        if (!TryAppendVal(1, (src_high ? 128 : 0ul) | src)) return false;
                    }
                    // reg, mem
                    else if (args[1][args[1].Length - 1] == ']')
                    {
                        if (!TryParseAddress(args[1], out UInt64 a, out UInt64 b, out Expr ptr_base, out b_sizecode, out bool explicit_size)) return false;

                        if (explicit_size && a_sizecode != b_sizecode) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Operand size missmatch"); return false; }

                        if (!TryAppendVal(1, (reg << 4) | (a_sizecode << 2) | (reg_high ? 2 : 0ul) | 1)) return false;
                        if (!TryAppendAddress(a, b, ptr_base)) return false;
                    }
                    // reg, imm
                    else { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Expected a cpu register or memory value as second operand"); return false; }
                }
                // mem, *
                else if (args[0][args[0].Length - 1] == ']')
                {
                    if (!TryParseAddress(args[0], out UInt64 a, out UInt64 b, out Expr ptr_base, out a_sizecode, out bool explicit_size)) return false;

                    // mem, reg
                    if (TryParseCPURegister(args[1], out reg, out UInt64 b_sizecode, out reg_high))
                    {
                        if (explicit_size && a_sizecode != b_sizecode) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Operand size missmatch"); return false; }

                        if (!TryAppendVal(1, (reg << 4) | (b_sizecode << 2) | (reg_high ? 2 : 0ul) | 1)) return false;
                        if (!TryAppendAddress(a, b, ptr_base)) return false;
                    }
                    // mem, mem
                    else if (args[1][args[1].Length - 1] == ']') { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Only one operand may be a memory value"); return false; }
                    // mem, imm
                    else { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Expected a cpu register or memory value as second operand"); return false; }
                }
                // imm, *
                else { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Expected a cpu register or memory value as first operand"); return false; }

                return true;
            }
            public bool TryProcessLEA(OPCode op)
            {
                if (args.Length != 2) { res = new AssembleResult(AssembleError.ArgCount, $"line {line}: Expected 2 operands"); return false; }

                if (!TryParseCPURegister(args[0], out UInt64 dest, out UInt64 a_sizecode, out bool dest_high)) return false;
                if (a_sizecode == 0) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: 8-bit addressing is not supported"); return false; }

                if (!TryParseAddress(args[1], out UInt64 a, out UInt64 b, out Expr ptr_base, out UInt64 b_sizecode, out bool explicit_size)) return false;
                
                if (!TryAppendByte((byte)op)) return false;
                if (!TryAppendVal(1, (dest << 4) | (a_sizecode << 2))) return false;
                if (!TryAppendAddress(a, b, ptr_base)) return false;

                return true;
            }
            public bool TryProcessPOP(OPCode op)
            {
                if (args.Length != 1) { res = new AssembleResult(AssembleError.ArgCount, $"line {line}: Expected 1 operand"); return false; }

                // write op code
                if (!TryAppendByte((byte)op)) return false;

                // reg
                if (TryParseCPURegister(args[0], out UInt64 reg, out UInt64 a_sizecode, out bool a_high))
                {
                    if ((Size(a_sizecode) & 14) == 0) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Specified size is not supported"); return false; }

                    if (!TryAppendVal(1, (reg << 4) | (a_sizecode << 2))) return false;
                }
                // mem
                else if (args[0][args[0].Length - 1] == ']')
                {
                    if (!TryParseAddress(args[0], out UInt64 a, out UInt64 b, out Expr ptr_base, out a_sizecode, out bool explicit_size)) return false;

                    if (!explicit_size) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Could not deduce operand size"); return false; }

                    if ((Size(a_sizecode) & 14) == 0) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Specified size is not supported"); return false; }

                    if (!TryAppendVal(1, (a_sizecode << 2) | 1)) return false;
                    if (!TryAppendAddress(a, b, ptr_base)) return false;
                }
                else { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Expected a cpu register or memory value"); return false; }

                return true;
            }

            private bool __TryProcessShift_mid()
            {
                // reg/mem, reg
                if (TryParseCPURegister(args[1], out UInt64 src, out UInt64 b_sizecode, out bool b_high))
                {
                    if (src != 2 || b_sizecode != 0 || b_high) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Shifts using a register as count source must use CL"); return false; }

                    if (!TryAppendByte(0x80)) return false;
                }
                // reg/mem, imm
                else
                {
                    if (!TryParseImm(args[1], out Expr imm, out b_sizecode, out bool explicit_size)) return false;

                    // mask the shift count to 6 bits (we just need to make sure it can't set the CL flag)
                    imm = new Expr() { OP = Expr.OPs.BitAnd, Left = imm, Right = new Expr() { IntResult = 0x3f } };

                    if (!TryAppendExpr(1, imm)) return false;
                }

                return true;
            }
            public bool TryProcessShift(OPCode op)
            {
                if (args.Length != 2) { res = new AssembleResult(AssembleError.ArgCount, $"line {line}: Expected 2 operands"); return false; }

                // write op code
                if (!TryAppendByte((byte)op)) return false;

                // reg, *
                if (TryParseCPURegister(args[0], out UInt64 dest, out UInt64 a_sizecode, out bool a_high))
                {
                    if (!TryAppendVal(1, (dest << 4) | (a_sizecode << 2) | (a_high ? 2 : 0ul))) return false;
                    if (!__TryProcessShift_mid()) return false;
                }
                // mem, *
                else if (args[0][args[0].Length - 1] == ']')
                {
                    if (!TryParseAddress(args[0], out UInt64 a, out UInt64 b, out Expr ptr_base, out a_sizecode, out bool explicit_size)) return false;

                    if (!explicit_size) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Could not deduce operand size"); return false; }

                    // make sure we're using a normal word size
                    if (a_sizecode > 3) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Specified size is not supported"); return false; }

                    if (!TryAppendVal(1, (a_sizecode << 2) | 1)) return false;
                    if (!__TryProcessShift_mid()) return false;
                    if (!TryAppendAddress(1, b, ptr_base)) return false;
                }
                else { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Expected a cpu register or memory value as first operand"); return false; }
                
                return true;
            }

            private bool __TryProcessMOVxX_settings_byte(bool sign, UInt64 dest, UInt64 dest_sizecode, UInt64 src_sizecode)
            {
                // 16, *
                if (dest_sizecode == 1)
                {
                    // 16, 8
                    if (src_sizecode == 0)
                    {
                        if (!TryAppendVal(1, (dest << 4) | (sign ? 1 : 0ul))) return false;
                    }
                    else { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Specified size combination is not supported"); return false; }
                }
                // 32/64, *
                else if (dest_sizecode == 2 || dest_sizecode == 3)
                {
                    // 32/64, 8
                    if (src_sizecode == 0)
                    {
                        if (!TryAppendVal(1, (dest << 4) | (dest_sizecode == 2 ? sign ? 4 : 2ul : sign ? 8 : 6ul))) return false;
                    }
                    // 32/64, 16
                    else if (src_sizecode == 1)
                    {
                        if (!TryAppendVal(1, (dest << 4) | (dest_sizecode == 2 ? sign ? 5 : 3ul : sign ? 9 : 7ul))) return false;
                    }
                    else { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Specified size combination is not supported"); return false; }
                }
                else { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Specified size combination is not supported"); return false; }

                return true;
            }
            public bool TryProcessMOVxX(OPCode op, bool sign)
            {
                if (args.Length != 2) { res = new AssembleResult(AssembleError.ArgCount, $"line {line}: Expected 2 operands"); return false; }

                if (!TryParseCPURegister(args[0], out UInt64 dest, out UInt64 dest_sizecode, out bool __dest_high)) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: First operand must be a cpu register"); return false; }

                // write op code
                if (!TryAppendByte((byte)op)) return false;

                // reg, reg
                if (TryParseCPURegister(args[1], out UInt64 src, out UInt64 src_sizecode, out bool src_high))
                {
                    // write the settings byte
                    if (!__TryProcessMOVxX_settings_byte(sign, dest, dest_sizecode, src_sizecode)) return false;

                    // mark source as register
                    if (!TryAppendVal(1, (src_high ? 64 : 0ul) | src)) return false;
                }
                // reg, mem
                else if (args[1][args[1].Length - 1] == ']')
                {
                    if (!TryParseAddress(args[1], out UInt64 a, out UInt64 b, out Expr ptr_base, out src_sizecode, out bool explicit_size)) return false;

                    if (!explicit_size) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Could not deduce operand size"); return false; }

                    // write the settings byte
                    if (!__TryProcessMOVxX_settings_byte(sign, dest, dest_sizecode, src_sizecode)) return false;

                    // mark source as a memory value and append the address
                    if (!TryAppendByte(0x80)) return false;
                    if (!TryAppendAddress(a, b, ptr_base)) return false;
                }
                else { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Expected a cpu register or memory value as second operand"); return false; }

                return true;
            }

            public bool __TryGetBinaryStringOpSize(out UInt64 sizecode)
            {
                sizecode = 0;

                // must have 2 args
                if (args.Length != 2) { res = new AssembleResult(AssembleError.ArgCount, $"line {line}: Expected 2 operands"); return false; }

                // args must both be memory
                UInt64 a, b, sz_1, sz_2;
                Expr expr;
                bool expl_1, expl_2;
                if (!TryParseAddress(args[0], out a, out b, out expr, out sz_1, out expl_1) || !TryParseAddress(args[1], out a, out b, out expr, out sz_2, out expl_2)) return false;

                // need an explicit size (that is consistent)
                if (expl_1 && expl_2) { if (sz_1 != sz_2) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Operand size mismatch"); return false; } sizecode = sz_1; }
                else if (expl_1) sizecode = sz_1;
                else if (expl_2) sizecode = sz_2;
                else { res =  new AssembleResult(AssembleError.UsageError, $"line {line}: Could not deduce operand size"); return false; }

                // make sure sizecode is in range
                if (sizecode > 3) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Specified operand size is not supported"); return false; }

                return true;
            }

            public bool TryProcessMOVS_string(OPCode op, bool rep)
            {
                if (!__TryGetBinaryStringOpSize(out UInt64 sizecode)) return false;

                if (!TryAppendByte((byte)op)) return false;
                if (!TryAppendByte((byte)(((rep ? 1 : 0ul) << 2) | sizecode))) return false;

                return true;
            }
            public bool TryProcessCMPS_string(OPCode op, bool repe, bool repne)
            {
                if (!__TryGetBinaryStringOpSize(out UInt64 sizecode)) return false;

                if (!TryAppendByte((byte)op)) return false;
                if (!TryAppendByte((byte)(((repne ? 4 : repe ? 3 : 2ul) << 2) | sizecode))) return false;

                return true;
            }
            public bool TryProcessLODS_string(OPCode op, bool rep)
            {
                if (args.Length != 1) { res = new AssembleResult(AssembleError.ArgCount, $"line {line}: Expected 1 operand"); return false; }

                // takes one cpu register that is a partition of RAX (may not be AH)
                if (!TryParseCPURegister(args[0], out UInt64 reg, out UInt64 sizecode, out bool high)) return false;
                if (reg != 0) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Operand must be a partition of RAX"); return false; }
                if (high) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Operand may not be AH"); return false; }

                if (!TryAppendByte((byte)op)) return false;
                if (!TryAppendByte((byte)(((rep ? 6 : 5ul) << 2) | sizecode))) return false;

                return true;
            }
            public bool TryProcessSTOS_string(OPCode op, bool rep)
            {
                if (args.Length != 1) { res = new AssembleResult(AssembleError.ArgCount, $"line {line}: Expected 1 operand"); return false; }

                // takes one memory operand of a standard size
                if (!TryParseAddress(args[0], out UInt64 a, out UInt64 b, out Expr ptr_base, out UInt64  sizecode, out bool explicit_size)) return false;
                if (!explicit_size) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Could not deduce operand size"); return false; }
                if (sizecode > 3) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Specified operand size is not supported"); return false; }

                if (!TryAppendByte((byte)op)) return false;
                if (!TryAppendByte((byte)(((rep ? 8 : 7ul) << 2) | sizecode))) return false;

                return true;
            }
            public bool TryProcessSCAS_string(OPCode op, bool repe, bool repne)
            {
                if (args.Length != 1) { res = new AssembleResult(AssembleError.ArgCount, $"line {line}: Expected 1 operand"); return false; }

                // takes one memory operand of a standard size
                if (!TryParseAddress(args[0], out UInt64 a, out UInt64 b, out Expr ptr_base, out UInt64 sizecode, out bool explicit_size)) return false;
                if (!explicit_size) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Could not deduce operand size"); return false; }
                if (sizecode > 3) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Specified operand size is not supported"); return false; }

                if (!TryAppendByte((byte)op)) return false;
                if (!TryAppendByte((byte)(((repne ? 11 : repe ? 10 : 9ul) << 2) | sizecode))) return false;

                return true;
            }

			/// <summary>
			/// Extracts a prefix from the args and returns the actual instruction token (as uppercase)
			/// </summary>
			/// <param name="actual">the resulting actual (augmented) instruction</param>
            public bool TryProcessPrefixOp(out string actual)
            {
                if (args.Length == 0) { res = new AssembleResult(AssembleError.ArgCount, $"line {line}: expected an instruction to augment"); actual = ""; return false; }

                // first arg contains the instrucion to execute - find first white-space delimiter
                int len;
                for (len = 0; len < args[0].Length && !char.IsWhiteSpace(args[0][len]); ++len) ;

                // extract that as the "actual" instruction to modify - convert to uppercase
                actual = args[0].Substring(0, len).ToUpper();

                // if we got the whole arg, remove first arg entirely
                if (len == args[0].Length)
                {
                    string[] new_args = new string[args.Length - 1];
                    for (int i = 1; i < args.Length; ++i) new_args[i - 1] = args[i];
                    args = new_args;
                }
                // otherwise, remove what we took and chop off leading white space
                else args[0] = args[0].Substring(len).TrimStart();

                return true;
            }

            public bool TryProcessREP()
            {
                if (!TryProcessPrefixOp(out string actual)) return false;

				// route to proper handlers
				if (actual == "RET") return TryProcessNoArgOp(OPCode.RET);

                else if (actual == "MOVS") return TryProcessMOVS_string(OPCode.string_ops, true);
                else if (actual == "MOVSB") return TryProcessNoArgOp(OPCode.string_ops, true, (1 << 2) | 0);
                else if (actual == "MOVSW") return TryProcessNoArgOp(OPCode.string_ops, true, (1 << 2) | 1);
                else if (actual == "MOVSD") return TryProcessNoArgOp(OPCode.string_ops, true, (1 << 2) | 2);
                else if (actual == "MOVSQ") return TryProcessNoArgOp(OPCode.string_ops, true, (1 << 2) | 3);

                else if (actual == "LODS") return TryProcessLODS_string(OPCode.string_ops, true);
                else if (actual == "LODSB") return TryProcessNoArgOp(OPCode.string_ops, true, (6 << 2) | 0);
                else if (actual == "LODSW") return TryProcessNoArgOp(OPCode.string_ops, true, (6 << 2) | 1);
                else if (actual == "LODSD") return TryProcessNoArgOp(OPCode.string_ops, true, (6 << 2) | 2);
                else if (actual == "LODSQ") return TryProcessNoArgOp(OPCode.string_ops, true, (6 << 2) | 3);

                else if (actual == "STOS") return TryProcessSTOS_string(OPCode.string_ops, true);
                else if (actual == "STOSB") return TryProcessNoArgOp(OPCode.string_ops, true, (8 << 2) | 0);
                else if (actual == "STOSW") return TryProcessNoArgOp(OPCode.string_ops, true, (8 << 2) | 1);
                else if (actual == "STOSD") return TryProcessNoArgOp(OPCode.string_ops, true, (8 << 2) | 2);
                else if (actual == "STOSQ") return TryProcessNoArgOp(OPCode.string_ops, true, (8 << 2) | 3);

                // otherwise this is illegal usage of REP
                else { res = new AssembleResult(AssembleError.UsageError, $"line {line}: REP cannot be used with the specified instruction"); return false; }
            }
            public bool TryProcessREPE()
            {
                if (!TryProcessPrefixOp(out string actual)) return false;

				// route to proper handlers
				if (actual == "RET") return TryProcessNoArgOp(OPCode.RET);

                else if (actual == "CMPS") return TryProcessCMPS_string(OPCode.string_ops, true, false);
                else if (actual == "CMPSB") return TryProcessNoArgOp(OPCode.string_ops, true, (3 << 2) | 0);
                else if (actual == "CMPSW") return TryProcessNoArgOp(OPCode.string_ops, true, (3 << 2) | 1);
                else if (actual == "CMPSD") return TryProcessNoArgOp(OPCode.string_ops, true, (3 << 2) | 2);
                else if (actual == "CMPSQ") return TryProcessNoArgOp(OPCode.string_ops, true, (3 << 2) | 3);

                else if (actual == "SCAS") return TryProcessSCAS_string(OPCode.string_ops, true, false);
                else if (actual == "SCASB") return TryProcessNoArgOp(OPCode.string_ops, true, (10 << 2) | 0);
                else if (actual == "SCASW") return TryProcessNoArgOp(OPCode.string_ops, true, (10 << 2) | 1);
                else if (actual == "SCASD") return TryProcessNoArgOp(OPCode.string_ops, true, (10 << 2) | 2);
                else if (actual == "SCASQ") return TryProcessNoArgOp(OPCode.string_ops, true, (10 << 2) | 3);

                // otherwise this is illegal usage of REP
                else { res = new AssembleResult(AssembleError.UsageError, $"line {line}: REPE cannot be used with the specified instruction"); return false; }
            }
            public bool TryProcessREPNE()
            {
                if (!TryProcessPrefixOp(out string actual)) return false;

				// route to proper handlers
				if (actual == "RET") return TryProcessNoArgOp(OPCode.RET);

				else if (actual == "CMPS") return TryProcessCMPS_string(OPCode.string_ops, false, true);
                else if (actual == "CMPSB") return TryProcessNoArgOp(OPCode.string_ops, true, (4 << 2) | 0);
                else if (actual == "CMPSW") return TryProcessNoArgOp(OPCode.string_ops, true, (4 << 2) | 1);
                else if (actual == "CMPSD") return TryProcessNoArgOp(OPCode.string_ops, true, (4 << 2) | 2);
                else if (actual == "CMPSQ") return TryProcessNoArgOp(OPCode.string_ops, true, (4 << 2) | 3);

                else if (actual == "SCAS") return TryProcessSCAS_string(OPCode.string_ops, false, true);
                else if (actual == "SCASB") return TryProcessNoArgOp(OPCode.string_ops, true, (11 << 2) | 0);
                else if (actual == "SCASW") return TryProcessNoArgOp(OPCode.string_ops, true, (11 << 2) | 1);
                else if (actual == "SCASD") return TryProcessNoArgOp(OPCode.string_ops, true, (11 << 2) | 2);
                else if (actual == "SCASQ") return TryProcessNoArgOp(OPCode.string_ops, true, (11 << 2) | 3);

                // otherwise this is illegal usage of REP
                else { res = new AssembleResult(AssembleError.UsageError, $"line {line}: REPNE cannot be used with the specified instruction"); return false; }
            }

            public bool TryProcessBSx(OPCode op, bool forward)
            {
                if (args.Length != 2) { res = new AssembleResult(AssembleError.ArgCount, $"line {line}: Expected 2 operands"); return false; }

                // write the op code
                if (!TryAppendByte((byte)op)) return false;

                // first arg must be reg
                if (!TryParseCPURegister(args[0], out UInt64 dest, out UInt64 dest_sz, out bool high)) return false;
                // 8-bit mode is not allowed
                if (dest_sz == 0) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: 8-bit mode is not supported"); return false; }

                // reg, reg
                if (TryParseCPURegister(args[1], out UInt64 src, out UInt64 src_sz, out high))
                {
                    // make sure sizes match
                    if (dest_sz != src_sz) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Operand size mismatch"); return false; }
                    if (!TryAppendByte((byte)((forward ? 128 : 0ul) | (dest_sz << 4) | dest))) return false;
                    if (!TryAppendByte((byte)src)) return false;
                }
                // reg, mem
                else if (args[1][args[1].Length - 1] == ']')
                {
                    if (!TryParseAddress(args[1], out UInt64 a, out UInt64 b, out Expr ptr_base, out src_sz, out bool src_explicit)) return false;

                    // make sure sizes match
                    if (src_explicit && dest_sz != src_sz) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Operand size mismatch"); return false; }

                    if (!TryAppendByte((byte)((forward ? 128 : 0ul) | 64 | (dest_sz << 4) | dest))) return false;
                    if (!TryAppendAddress(a, b, ptr_base)) return false;
                }
                else { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Second operand must be a cpu register or memory value"); return false; }

                return true;
            }

            // -- x87 op formats -- //

            public bool TryProcessFPUBinaryOp(OPCode op, bool integral, bool pop)
            {
                // write op code
                if (!TryAppendByte((byte)op)) return false;

                // make sure the programmer doesn't pull any funny business due to our arg-count-based approach
                if (integral && args.Length != 1) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Integral {op} requires 1 arg"); return false; }
                if (pop && args.Length != 0 && args.Length != 2) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Popping {op} requires 0 or 2 args"); return false; }

                // handle arg count cases
                if (args.Length == 0)
                {
                    // no args is st(1) <- f(st(1), st(0)), pop
                    if (!pop) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: This form requires operands"); return false; }

                    if (!TryAppendByte(0x12)) return false;
                }
                else if (args.Length == 1)
                {
                    if (!TryParseAddress(args[0], out UInt64 a, out UInt64 b, out Expr ptr_base, out UInt64 sizecode, out bool explicit_size)) return false;

                    if (!explicit_size) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Could not deduce operand size"); return false; }

                    // integral
                    if (integral)
                    {
                        if (sizecode == 1) { if (!TryAppendByte(5)) return false; }
                        else if (sizecode == 2) { if (!TryAppendByte(6)) return false; }
                        else { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Specified size is not supported"); return false; }
                    }
                    // floatint-point
                    else
                    {
                        if (sizecode == 2) { if (!TryAppendByte(3)) return false; }
                        else if (sizecode == 3) { if (!TryAppendByte(4)) return false; }
                        else { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Specified size is not supported"); return false; }
                    }

                    // write the address
                    if (!TryAppendAddress(a, b, ptr_base)) return false;
                }
                else if (args.Length == 2)
                {
                    if (!TryParseFPURegister(args[0], out UInt64 a) || !TryParseFPURegister(args[1], out UInt64 b)) return false;

                    // if b is st(0) (do this one first since it handles the pop form)
                    if (b == 0)
                    {
                        if (!TryAppendVal(1, (a << 4) | (pop ? 2 : 1ul))) return false;
                    }
                    // if a is st(0)
                    else if (a == 0)
                    {
                        if (pop) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Expected ST(0) as second operand"); return false; }

                        if (!TryAppendVal(1, b << 4)) return false;
                    }
                    // x87 requires one of them be st(0)
                    else { res = new AssembleResult(AssembleError.UsageError, $"line {line}: One operand must be ST(0)"); return false; }
                }
                else { res = new AssembleResult(AssembleError.ArgCount, $"line {line}: Too many operands"); return false; }

                return true;
            }
            public bool TryProcessFPURegisterOp(OPCode op, bool has_ext_op = false, byte ext_op = 0)
            {
                if (args.Length != 1) { res = new AssembleResult(AssembleError.ArgCount, $"line {line}: Expected 1 operand"); return false; }

                if (!TryParseFPURegister(args[0], out UInt64 reg)) return false;

                // write op code
                if (!TryAppendByte((byte)op)) return false;
                if (has_ext_op) { if (!TryAppendByte(ext_op)) return false; }

                // write the register
                if (!TryAppendVal(1, reg)) return false;

                return true;
            }

            /// <summary>
            /// _sizecode is only used for validating explicit memory operand sizes
            /// </summary>
            public bool TryProcessFSTLD_WORD(OPCode op, byte mode, UInt64 _sizecode)
            {
                if (args.Length != 1) { res = new AssembleResult(AssembleError.ArgCount, $"line {line}: Expected 1 operand"); return false; }

                // operand has to be mem
                if (!TryParseAddress(args[0], out UInt64 a, out UInt64 b, out Expr ptr_base, out UInt64 sizecode, out bool explicit_size)) return false;

                // must be the dictated size
                if (explicit_size && sizecode != _sizecode) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Specified operand size is not supported"); return false; }

                // write data
                if (!TryAppendByte((byte)op)) return false;
                if (!TryAppendByte(mode)) return false;
                if (!TryAppendAddress(a, b, ptr_base)) return false;

                return true;
            }

            public bool TryProcessFLD(OPCode op, bool integral)
            {
                if (args.Length != 1) { res = new AssembleResult(AssembleError.ArgCount, $"line {line}: Expected 1 operand"); return false; }

                // write op code
                if (!TryAppendByte((byte)op)) return false;

                // pushing st(i)
                if (TryParseFPURegister(args[0], out UInt64 reg))
                {
                    if (!TryAppendVal(1, reg << 4)) return false;
                }
                // pushing memory value
                else if (args[0][args[0].Length - 1] == ']')
                {
                    if (!TryParseAddress(args[0], out UInt64 a, out UInt64 b, out Expr ptr_base, out UInt64 sizecode, out bool explicit_size)) return false;

                    if (!explicit_size) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Could not deduce operand size"); return false; }

                    // handle integral cases
                    if (integral)
                    {
                        if (sizecode != 1 && sizecode != 2 && sizecode != 3) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Specified size is not supported"); return false; }

                        if (!TryAppendVal(1, sizecode + 2)) return false;
                    }
                    // otherwise floating-point
                    else
                    {
                        if (sizecode != 2 && sizecode != 3) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Specified size is not supported"); return false; }

                        if (!TryAppendVal(1, sizecode - 1)) return false;
                    }

                    // and write the address
                    if (!TryAppendAddress(a, b, ptr_base)) return false;
                }
                else { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Expected an fpu register or a memory value"); return false; }

                return true;
            }
            public bool TryProcessFST(OPCode op, bool integral, bool pop, bool trunc)
            {
                if (args.Length != 1) { res = new AssembleResult(AssembleError.ArgCount, $"line {line}: Expected 1 operand"); return false; }

                // write the op code
                if (!TryAppendByte((byte)op)) return false;

                // if it's an fpu register
                if (TryParseFPURegister(args[0], out UInt64 reg))
                {
                    // can't be an integral op
                    if (integral) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Expected a memory value"); return false; }

                    if (!TryAppendVal(1, (reg << 4) | (pop ? 1 : 0ul))) return false;
                }
                // if it's a memory destination
                else if (args[0][args[0].Length - 1] == ']')
                {
                    if (!TryParseAddress(args[0], out UInt64 a, out UInt64 b, out Expr ptr_base, out UInt64 sizecode, out bool explicit_size)) return false;

                    if (!explicit_size) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Could not deduce operand size"); return false; }

                    // if this is integral (i.e. truncation store)
                    if (integral)
                    {
                        if (sizecode == 1) { if (!TryAppendVal(1, pop ? trunc ? 11 : 7ul : 6)) return false; }
                        else if (sizecode == 2) { if (!TryAppendVal(1, pop ? trunc ? 12 : 9ul : 8)) return false; }
                        else if (sizecode == 3)
                        {
                            // there isn't a non-popping 64-bit int store
                            if (!pop) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Specified size is not supported"); return false; }

                            if (!TryAppendVal(1, trunc ? 13 : 10ul)) return false;
                        }
                        else { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Specified size is not supported"); return false; }
                    }
                    // otherwise is floating-point
                    else
                    {
                        if (sizecode == 2) { if (!TryAppendVal(1, pop ? 3 : 2ul)) return false; }
                        else if (sizecode == 3) { if (!TryAppendVal(1, pop ? 5 : 4ul)) return false; }
                        else { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Specified size is not supported"); return false; }
                    }

                    // and write the address
                    if (!TryAppendAddress(a, b, ptr_base)) return false;
                }
                else { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Expected an fpu register or memory value"); return false; }

                return true;
            }
            public bool TryProcessFCOM(OPCode op, bool integral, bool pop, bool pop2, bool eflags, bool unordered)
            {
                // write the op code
                if (!TryAppendByte((byte)op)) return false;

                // handle arg count cases
                if (args.Length == 0)
                {
                    if (integral) { res = new AssembleResult(AssembleError.ArgCount, $"line {line}: Expected 1 operand"); return false; }
                    if (eflags) { res = new AssembleResult(AssembleError.ArgCount, $"line {line}: Expected 2 operands"); return false; }

                    // no args is same as using st(1) (plus additional case of double pop)
                    if (!TryAppendVal(1, (unordered ? 128 : 0ul) | (1 << 4) | (pop2 ? 2 : pop ? 1 : 0ul))) return false;
                }
                else if (args.Length == 1)
                {
                    // double pop doesn't accept operands
                    if (pop2) { res = new AssembleResult(AssembleError.ArgCount, $"line {line}: Expected no operands"); return false; }
                    if (eflags) { res = new AssembleResult(AssembleError.ArgCount, $"line {line}: Expected 2 operands"); return false; }

                    // register
                    if (TryParseFPURegister(args[0], out UInt64 reg))
                    {
                        // integral forms only store to memory
                        if (integral) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Expected a memory value"); return false; }

                        if (!TryAppendVal(1, (unordered ? 128 : 0ul) | (reg << 4) | (pop ? 1 : 0ul))) return false;
                    }
                    // memory
                    else if (args[0][args[0].Length - 1] == ']')
                    {
                        if (!TryParseAddress(args[0], out UInt64 a, out UInt64 b, out Expr ptr_base, out UInt64 sizecode, out bool explicit_size)) return false;

                        if (!explicit_size) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Could not deduce operand size"); return false; }

                        // handle size cases
                        if (sizecode == 1)
                        {
                            // this mode only allows int
                            if (!integral) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Specified size is not supported"); return false; }

                            if (!TryAppendVal(1, (unordered ? 128 : 0ul) | (pop ? 8 : 7ul))) return false;
                        }
                        else if (sizecode == 2)
                        {
                            if (!TryAppendVal(1, (unordered ? 128 : 0ul) | (integral ? pop ? 10 : 9ul : pop ? 4 : 3ul))) return false;
                        }
                        else if (sizecode == 3)
                        {
                            // this mode only allows fp
                            if (integral) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Specified size is not supported"); return false; }

                            if (!TryAppendVal(1, (unordered ? 128 : 0ul) | (pop ? 6 : 5ul))) return false;
                        }
                        else { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Specified size is not supported"); return false; }

                        // and write the address
                        if (!TryAppendAddress(a, b, ptr_base)) return false;
                    }
                    else { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Expected an fpu register or a memory value"); return false; }
                }
                else if (args.Length == 2)
                {
                    if (integral) { res = new AssembleResult(AssembleError.ArgCount, $"line {line}: Expected 1 operand"); return false; }
                    if (pop2) { res = new AssembleResult(AssembleError.ArgCount, $"line {line}: Expected no operands"); return false; }

                    if (!TryParseFPURegister(args[0], out UInt64 reg_a) || !TryParseFPURegister(args[1], out UInt64 reg_b)) return false;

                    // first arg must be st0
                    if (reg_a != 0) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: First operand must be ST(0)"); return false; }

                    if (!TryAppendVal(1, (unordered ? 128 : 0ul) | (reg_b << 4) | (pop ? 12 : 11ul))) return false;
                }
                else { res = new AssembleResult(AssembleError.ArgCount, $"line {line}: Too many operands"); return false; }

                return true;
            }
            public bool TryProcessFMOVcc(OPCode op, UInt64 condition)
            {
                if (args.Length != 2) { res = new AssembleResult(AssembleError.ArgCount, $"line {line}: Expected 2 operands"); return false; }

                if (!TryParseFPURegister(args[0], out UInt64 reg) || reg != 0) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: First operand must be ST(0)"); return false; }
                if (!TryParseFPURegister(args[1], out reg)) return false;

                if (!TryAppendByte((byte)op)) return false;
                if (!TryAppendVal(1, (reg << 4) | condition)) return false;

                return true;
            }

            // -- SIMD op formats -- //

            public bool TryExtractVPUMask(ref string arg, out Expr mask, out bool zmask)
            {
                mask = null;   // no mask is denoted by null
                zmask = false; // by default, mask is not a zmask

                // if it ends in z or Z, it's a zmask
                if (arg[arg.Length - 1] == 'z' || arg[arg.Length - 1] == 'Z')
                {
                    // remove the z
                    arg = arg.Substring(0, arg.Length - 1).TrimEnd();

                    // ensure validity - must be preceded by }
                    if (arg.Length == 0 || arg[arg.Length - 1] != '}') { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Zmask declarator encountered without a corresponding mask"); return false; }

                    // mark as being a zmask
                    zmask = true;
                }
                
                // if it ends in }, there's a white mask
                if (arg[arg.Length - 1] == '}')
                {
                    // find the opening bracket
                    int pos = arg.IndexOf('{');
                    if (pos < 0) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Ill-formed vpu whitemask encountered"); return false; }
                    if (pos == 0) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Lone vpu whitemask encountered");; return false; }

                    // extract the whitemask internals
                    string innards = arg.Substring(pos + 1, arg.Length - 2 - pos);
                    // pop the whitemask off the arg
                    arg = arg.Substring(0, pos).TrimEnd();

                    // parse the mask expression
                    if (!TryParseImm(innards, out mask, out UInt64 sizecode, out bool explicit_size)) return false;
                }

                return true;
            }
            public bool VPUMaskPresent(Expr mask, UInt64 elem_count)
            {
                string err = null;

                // if it's null, it's not present
                if (mask == null) return false;

                // if we can't evaluate it, it's present
                if (!mask.Evaluate(file.Symbols, out UInt64 val, out bool _f, ref err)) return true;

                // otherwise, if the mask value isn't all 1's over the relevant region, it's present
                switch (elem_count)
                {
                    case 1: return (val & 1) != 1;
                    case 2: return (val & 3) != 3;
                    case 4: return (val & 0xf) != 0xf;
                    case 8: return (byte)val != byte.MaxValue;
                    case 16: return (UInt16)val != UInt16.MaxValue;
                    case 32: return (UInt32)val != UInt32.MaxValue;
                    case 64: return (UInt64)val != UInt64.MaxValue;

                    default: throw new ArgumentException($"elem_count was invalid. got: {elem_count}");
                }
            }

            public bool TryProcessVPUMove(OPCode op, UInt64 elem_sizecode, bool maskable, bool aligned, bool scalar)
            {
                if (args.Length != 2) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Expected 2 operands"); return false; }

                // write the op code
                if (!TryAppendByte((byte)op)) return false;

                // extract the mask
                if (!TryExtractVPUMask(ref args[0], out Expr mask, out bool zmask)) return false;
                // if it had an explicit mask and we were told not to allow that, it's an error
                if (mask != null && !maskable) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Instruction does not support masking"); return false; }

                // vreg, *
                if (TryParseVPURegister(args[0], out UInt64 dest, out UInt64 dest_sizecode))
                {
                    UInt64 elem_count = scalar ? 1 : Size(dest_sizecode) >> (UInt16)elem_sizecode;
                    bool mask_present = VPUMaskPresent(mask, elem_count);

                    // if we're in vector mode and the mask is not present, we can kick it up to 64-bit mode (for performance)
                    if (!scalar && !mask_present) elem_sizecode = 3;

                    // vreg, vreg
                    if (TryParseVPURegister(args[1], out UInt64 src, out UInt64 src_sizecode))
                    {
                        if (dest_sizecode != src_sizecode) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Operand size mismatch"); return false; }

                        if (!TryAppendVal(1, (dest << 3) | (aligned ? 4 : 0ul) | (dest_sizecode - 4))) return false;
                        if (!TryAppendVal(1, (mask_present ? 128 : 0ul) | (zmask ? 64 : 0ul) | (scalar ? 32 : 0ul) | (elem_sizecode << 2) | 0)) return false;
                        if (mask_present && !TryAppendExpr(BitsToBytes(elem_count), mask)) return false;
                        if (!TryAppendVal(1, src)) return false;
                    }
                    // vreg, mem
                    else if (args[1][args[1].Length - 1] == ']')
                    {
                        if (!TryParseAddress(args[1], out UInt64 a, out UInt64 b, out Expr ptr_base, out src_sizecode, out bool src_explicit)) return false;

                        if (src_explicit && src_sizecode != (scalar ? elem_sizecode : dest_sizecode)) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Operand size mismatch"); return false; }

                        if (!TryAppendVal(1, (dest << 3) | (aligned ? 4 : 0ul) | (dest_sizecode - 4))) return false;
                        if (!TryAppendVal(1, (mask_present ? 128 : 0ul) | (zmask ? 64 : 0ul) | (scalar ? 32 : 0ul) | (elem_sizecode << 2) | 1)) return false;
                        if (mask_present && !TryAppendExpr(BitsToBytes(elem_count), mask)) return false;
                        if (!TryAppendAddress(a, b, ptr_base)) return false;
                    }
                    // vreg, imm
                    else { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Expected a vpu register or memory value as second operand"); return false; }
                }
                // mem, *
                else if (args[0][args[0].Length - 1] == ']')
                {
                    if (!TryParseAddress(args[0], out UInt64 a, out UInt64 b, out Expr ptr_base, out dest_sizecode, out bool dest_explicit)) return false;

                    // mem, vreg
                    if (TryParseVPURegister(args[1], out UInt64 src, out UInt64 src_sizecode))
                    {
                        if (dest_explicit && dest_sizecode != (scalar ? elem_sizecode : src_sizecode)) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Operand size mismatch"); return false; }

                        UInt64 elem_count = scalar ? 1 : Size(src_sizecode) >> (UInt16)elem_sizecode;
                        bool mask_present = VPUMaskPresent(mask, elem_count);

                        // if we're in vector mode and the mask is not present, we can kick it up to 64-bit mode (for performance)
                        if (!scalar && !mask_present) elem_sizecode = 3;

                        if (!TryAppendVal(1, (src << 3) | (aligned ? 4 : 0ul) | (src_sizecode - 4))) return false;
                        if (!TryAppendVal(1, (mask_present ? 128 : 0ul) | (zmask ? 64 : 0ul) | (scalar ? 32 : 0ul) | (elem_sizecode << 2) | 2)) return false;
                        if (mask_present && !TryAppendExpr(BitsToBytes(elem_count), mask)) return false;
                        if (!TryAppendAddress(a, b, ptr_base)) return false;
                    }
                    // mem, mem/imm
                    else { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Expected a vpu register as second operand"); return false; }
                }
                else { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Expected a vpu register or a memory value as first operand"); return false; }

                return true;
            }
            public bool TryProcessVPUBinary(OPCode op, UInt64 elem_sizecode, bool maskable, bool aligned, bool scalar, bool has_ext_op = false, byte ext_op = 0)
            {
                if (args.Length != 2 && args.Length != 3) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Expected 2 or 3 operands"); return false; }

                // write the op code
                if (!TryAppendByte((byte)op)) return false;
                if (has_ext_op) { if (!TryAppendByte(ext_op)) return false; }

                // extract the mask
                if (!TryExtractVPUMask(ref args[0], out Expr mask, out bool zmask)) return false;
                // if it had an explicit mask and we were told not to allow that, it's an error
                if (mask != null && !maskable) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Instruction does not support masking"); return false; }

                // vreg, *
                if (TryParseVPURegister(args[0], out UInt64 dest, out UInt64 dest_sizecode))
                {
                    UInt64 elem_count = scalar ? 1 : Size(dest_sizecode) >> (UInt16)elem_sizecode;
                    bool mask_present = VPUMaskPresent(mask, elem_count);

                    // 2 args case
                    if (args.Length == 2)
                    {
                        // vreg, vreg
                        if (TryParseVPURegister(args[1], out UInt64 src, out UInt64 src_sizecode))
                        {
                            if (dest_sizecode != src_sizecode) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Operand size mismatch"); return false; }

                            if (!TryAppendVal(1, (dest << 3) | (aligned ? 4 : 0ul) | (dest_sizecode - 4))) return false;
                            if (!TryAppendVal(1, (mask_present ? 128 : 0ul) | (zmask ? 64 : 0ul) | (scalar ? 32 : 0ul) | (elem_sizecode << 2) | 0)) return false;
                            if (mask_present && !TryAppendExpr(BitsToBytes(elem_count), mask)) return false;
                            if (!TryAppendVal(1, dest)) return false;
                            if (!TryAppendVal(1, src)) return false;
                        }
                        // vreg, mem
                        else if (args[1][args[1].Length - 1] == ']')
                        {
                            if (!TryParseAddress(args[1], out UInt64 a, out UInt64 b, out Expr ptr_base, out src_sizecode, out bool src_explicit)) return false;

                            if (src_explicit && src_sizecode != (scalar ? elem_sizecode : dest_sizecode)) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Operand size mismatch"); return false; }

                            if (!TryAppendVal(1, (dest << 3) | (aligned ? 4 : 0ul) | (dest_sizecode - 4))) return false;
                            if (!TryAppendVal(1, (mask_present ? 128 : 0ul) | (zmask ? 64 : 0ul) | (scalar ? 32 : 0ul) | (elem_sizecode << 2) | 1)) return false;
                            if (mask_present && !TryAppendExpr(BitsToBytes(elem_count), mask)) return false;
                            if (!TryAppendVal(1, dest)) return false;
                            if (!TryAppendAddress(a, b, ptr_base)) return false;
                        }
                        // vreg, imm
                        else { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Expected vpu register or memory value as second operand"); return false; }
                    }
                    // 3 args case
                    else
                    {
                        // vreg, vreg, *
                        if (TryParseVPURegister(args[1], out UInt64 src1, out UInt64 src1_sizecode))
                        {
                            // vreg, vreg, vreg
                            if (TryParseVPURegister(args[2], out UInt64 src2, out UInt64 src2_sizecode))
                            {
                                if (dest_sizecode != src1_sizecode || src1_sizecode != src2_sizecode) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Operand size mismatch"); return false; }

                                if (!TryAppendVal(1, (dest << 3) | (aligned ? 4 : 0ul) | (dest_sizecode - 4))) return false;
                                if (!TryAppendVal(1, (mask_present ? 128 : 0ul) | (zmask ? 64 : 0ul) | (scalar ? 32 : 0ul) | (elem_sizecode << 2) | 0)) return false;
                                if (mask_present && !TryAppendExpr(BitsToBytes(elem_count), mask)) return false;
                                if (!TryAppendVal(1, src1)) return false;
                                if (!TryAppendVal(1, src2)) return false;
                            }
                            // vreg, vreg, mem
                            else if (args[2][args[2].Length - 1] == ']')
                            {
                                if (!TryParseAddress(args[2], out UInt64 a, out UInt64 b, out Expr ptr_base, out src2_sizecode, out bool src2_explicit)) return false;

                                if (dest_sizecode != src1_sizecode || src2_explicit && src2_sizecode != (scalar ? elem_sizecode : dest_sizecode)) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Operand size mismatch"); return false; }

                                if (!TryAppendVal(1, (dest << 3) | (aligned ? 4 : 0ul) | (dest_sizecode - 4))) return false;
                                if (!TryAppendVal(1, (mask_present ? 128 : 0ul) | (zmask ? 64 : 0ul) | (scalar ? 32 : 0ul) | (elem_sizecode << 2) | 1)) return false;
                                if (mask_present && !TryAppendExpr(BitsToBytes(elem_count), mask)) return false;
                                if (!TryAppendVal(1, src1)) return false;
                                if (!TryAppendAddress(a, b, ptr_base)) return false;
                            }
                            // vreg, imm
                            else { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Expected vpu register or memory value as third operand"); return false; }
                        }
                        // vreg, mem/imm, *
                        else { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Expected vpu register as second operand"); return false; }
                    }
                }
                // mem/imm, *
                else { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Expected a vpu register as first operand"); return false; }

                return true;
            }
            public bool TryProcessVPUUnary(OPCode op, UInt64 elem_sizecode, bool maskable, bool aligned, bool scalar, bool has_ext_op = false, byte ext_op = 0)
            {
                if (args.Length!= 2) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Expected 2 operands"); return false; }

                // write the op code
                if (!TryAppendByte((byte)op)) return false;
                if (has_ext_op) { if (!TryAppendByte(ext_op)) return false; }

                // extract the mask
                if (!TryExtractVPUMask(ref args[0], out Expr mask, out bool zmask)) return false;
                // if it had an explicit mask and we were told not to allow that, it's an error
                if (mask != null && !maskable) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Instruction does not support masking"); return false; }

                // vreg, *
                if (TryParseVPURegister(args[0], out UInt64 dest, out UInt64 dest_sizecode))
                {
                    UInt64 elem_count = scalar ? 1 : Size(dest_sizecode) >> (UInt16)elem_sizecode;
                    bool mask_present = VPUMaskPresent(mask, elem_count);

                    // vreg, vreg
                    if (TryParseVPURegister(args[1], out UInt64 src, out UInt64 src_sizecode))
                    {
                        if (dest_sizecode != src_sizecode) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Operand size mismatch"); return false; }
                        if (!TryAppendVal(1, (dest << 3) | (aligned ? 4 : 0ul) | (dest_sizecode - 4))) return false;
                        if (!TryAppendVal(1, (mask_present ? 128 : 0ul) | (zmask ? 64 : 0ul) | (scalar ? 32 : 0ul) | (elem_sizecode << 2) | 0)) return false;
                        if (mask_present && !TryAppendExpr(BitsToBytes(elem_count), mask)) return false;
                        if (!TryAppendVal(1, src)) return false;
                    }
                    // vreg, mem
                    else if (args[1][args[1].Length - 1] == ']')
                    {
                        if (!TryParseAddress(args[1], out UInt64 a, out UInt64 b, out Expr ptr_base, out src_sizecode, out bool src_explicit)) return false;
                        if (src_explicit && src_sizecode != (scalar ? elem_sizecode : dest_sizecode)) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Operand size mismatch"); return false; }
                        if (!TryAppendVal(1, (dest << 3) | (aligned ? 4 : 0ul) | (dest_sizecode - 4))) return false;
                        if (!TryAppendVal(1, (mask_present ? 128 : 0ul) | (zmask ? 64 : 0ul) | (scalar ? 32 : 0ul) | (elem_sizecode << 2) | 1)) return false;
                        if (mask_present && !TryAppendExpr(BitsToBytes(elem_count), mask)) return false;
                        if (!TryAppendAddress(a, b, ptr_base)) return false;
                    }
                    // vreg, imm
                    else { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Expected vpu register or memory value as second operand"); return false; }
                }
                // mem/imm, *
                else { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Expected a vpu register as first operand"); return false; }

                return true;
            }

            /// <summary>
            /// same as TryProcessVPUBinary() except that it forces the 2 arg pathway
            /// </summary>
            public bool TryProcessVPUBinary_2arg(OPCode op, UInt64 elem_sizecode, bool maskable, bool aligned, bool scalar, bool has_ext_op = false, byte ext_op = 0)
            {
                // ensure we select the 2 arg pathway
                if (args.Length != 2) { res = new AssembleResult(AssembleError.ArgCount, $"line {line}: Expected 2 operands"); return false; }
                // then refer to VPUBinary
                return TryProcessVPUBinary(op, elem_sizecode, maskable, aligned, scalar, has_ext_op, ext_op);
            }

            public bool TryProcessVPU_FCMP(OPCode op, UInt64 elem_sizecode, bool maskable, bool aligned, bool scalar)
            {
                // has the 2-3 args + 1 for the comparison predicate
                if (args.Length != 3 && args.Length != 4) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Expected 3 or 4 operands"); return false; }

                // last arg must be the comarison predicate imm - instant integral imm [0-31]
                if (!TryParseInstantImm(args[args.Length - 1], out UInt64 pred, out bool floating, out UInt64 sizecode, out bool explicit_sizecode)) return false;
                if (floating) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Comparison predicate must be an integer"); return false; }
                if (pred >= 32) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Comparison predicate out of range"); return false; }

                // remove the comparison predicate arg
                string[] new_args = new string[args.Length - 1];
                for (int i = 0; i < new_args.Length; ++i) new_args[i] = args[i];
                args = new_args;

                // now refer to binary vpu formatter
                return TryProcessVPUBinary(op, elem_sizecode, maskable, aligned, scalar, true, (byte)pred);
            }

            public bool TryProcessVPUCVT_scalar_f2i(OPCode op, bool trunc, bool single)
            {
                if (args.Length != 2) { res = new AssembleResult(AssembleError.ArgCount, $"line {line}: Expected 2 operands"); return false; }

                // write the opcode
                if (!TryAppendByte((byte)op)) return false;

                byte mode = (byte)(trunc ? 8 : 0); // decoded vpucvt mode
                if (single) mode += 4;

                // dest must be cpu register
                if (!TryParseCPURegister(args[0], out UInt64 dest, out UInt64 dest_sizecode, out bool dest_high)) return false;

                // account for dest size
                if (dest_sizecode == 3) mode += 2;
                else if (dest_sizecode != 2) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Specified destination size not supported"); return false; }

                // if source is xmm register
                if (TryParseVPURegister(args[1], out UInt64 src, out UInt64 src_sizecode))
                {
                    // only xmm registers are supported
                    if (src_sizecode != 4) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Specified source size not supported"); return false; }
                    // this check /should/ be redundant, but better safe than sorry
                    if (src > 15) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Source register must be xmm0-15"); return false; }

                    // write the data
                    if (!TryAppendByte(mode)) return false;
                    if (!TryAppendByte((byte)((dest << 4) | src))) return false;
                }
                // if source is memory
                else if (args[1][args[1].Length - 1] == ']')
                {
                    if (!TryParseAddress(args[1], out UInt64 a, out UInt64 b, out Expr ptr_base, out src_sizecode, out bool src_explicit)) return false;

                    // make sure the size matches what we're expecting
                    if (src_explicit && src_sizecode != (single ? 2 : 3ul)) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Specified source size not supported"); return false; }

                    ++mode; // account for the memory mode case

                    // write the data
                    if (!TryAppendByte(mode)) return false;
                    if (!TryAppendByte((byte)(dest << 4))) return false;
                    if (!TryAppendAddress(a, b, ptr_base)) return false;
                }
                else { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Expected xmm register or memory value as second operand"); return false; }

                return true;
            }
            public bool TryProcessVPUCVT_scalar_i2f(OPCode op, bool single)
            {
                if (args.Length != 2) { res = new AssembleResult(AssembleError.ArgCount, $"line {line}: Expected 2 operands"); return false; }

                // write the opcode
                if (!TryAppendByte((byte)op)) return false;
                byte mode = (byte)(single ? 20 : 16); // decoded vpucvt mode

                // dest must be xmm register
                if (!TryParseVPURegister(args[0], out UInt64 dest, out UInt64 dest_sizecode)) return false;

                // only xmm registers are supported
                if (dest_sizecode != 4) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Specified destination size not supported"); return false; }
                // this check /should/ be redundant, but better safe than sorry
                if (dest > 15) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Destination register must be xmm0-15"); return false; }

                // if source is reg
                if (TryParseCPURegister(args[1], out UInt64 src, out UInt64 src_sizecode, out bool src_high))
                {
                    // account for size case
                    if (src_sizecode == 3) mode += 2;
                    else if (src_sizecode != 2) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Specified source size not supported"); return false; }

                    // write the data
                    if (!TryAppendByte(mode)) return false;
                    if (!TryAppendByte((byte)((dest << 4) | src))) return false;
                }
                // if source is mem
                else if (args[1][args[1].Length - 1] == ']')
                {
                    if (!TryParseAddress(args[1], out UInt64 a, out UInt64 b, out Expr ptr_base, out src_sizecode, out bool src_explicit)) return false;

                    // we need to know what format we're converting from
                    if (!src_explicit) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Could not deduce operand size"); return false; }

                    // account for size case
                    if (src_sizecode == 3) mode += 2;
                    else if (src_sizecode != 2) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Specified source size not supported"); return false; }

                    ++mode; // account for memory mode case

                    // write the data
                    if (!TryAppendByte(mode)) return false;
                    if (!TryAppendByte((byte)(dest << 4))) return false;
                    if (!TryAppendAddress(a, b, ptr_base)) return false;
                }
                else { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Expected cpu register or memory value as second operand"); return false; }

                return true;
            }
            public bool TryProcessVPUCVT_scalar_f2f(OPCode op, bool extend)
            {
                if (args.Length != 2) { res = new AssembleResult(AssembleError.ArgCount, $"line {line}: Expected 2 operands"); return false; }

                // write the opcode
                if (!TryAppendByte((byte)op)) return false;
                byte mode = (byte)(extend ? 26 : 24); // decoded vpucvt mode

                // dest must be xmm register
                if (!TryParseVPURegister(args[0], out UInt64 dest, out UInt64 dest_sizecode)) return false;

                // only xmm registers are supported
                if (dest_sizecode != 4) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Specified destination size not supported"); return false; }
                // this check /should/ be redundant, but better safe than sorry
                if (dest > 15) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Destination register must be xmm0-15"); return false; }

                // if source is xmm register
                if (TryParseVPURegister(args[1], out UInt64 src, out UInt64 src_sizecode))
                {
                    // only xmm registers are supported
                    if (src_sizecode != 4) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Specified source size not supported"); return false; }
                    // this check /should/ be redundant, but better safe than sorry
                    if (src > 15) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Source register must be xmm0-15"); return false; }

                    // write the data
                    if (!TryAppendByte(mode)) return false;
                    if (!TryAppendByte((byte)((dest << 4) | src))) return false;
                }
                // if source is memory
                else if (args[1][args[1].Length - 1] == ']')
                {
                    if (!TryParseAddress(args[1], out UInt64 a, out UInt64 b, out Expr ptr_base, out src_sizecode, out bool src_explicit)) return false;
                    
                    // make sure the size matches what we're expecting
                    if (src_explicit && src_sizecode != (extend ? 2 : 3ul)) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Specified source size not supported"); return false; }

                    ++mode; // account for the memory mode case
                    
                    // write the data
                    if (!TryAppendByte(mode)) return false;
                    if (!TryAppendByte((byte)(dest << 4))) return false;
                    if (!TryAppendAddress(a, b, ptr_base)) return false;
                }
                else { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Expected xmm register or memory value as second operand"); return false; }
                return true;
            }

            private bool __TryProcessVPUCVT_packed_formatter_reg(OPCode op, byte mode, UInt64 elem_count, UInt64 dest, Expr mask, bool zmask, UInt64 src)
            {
                bool mask_present = VPUMaskPresent(mask, elem_count);

                if (!TryAppendByte((byte)op)) return false;
                if (!TryAppendByte(mode)) return false;
                if (!TryAppendByte((byte)((dest << 3) | (0ul) | (mask_present ? 2 : 0ul) | (zmask ? 1 : 0ul)))) return false;
                if (mask_present && !TryAppendExpr(BitsToBytes(elem_count), mask)) return false;
                if (!TryAppendByte((byte)src)) return false;

                return true;
            }
            private bool __TryProcessVPUCVT_packed_formatter_mem(OPCode op, byte mode, UInt64 elem_count, UInt64 dest, Expr mask, bool zmask, UInt64 a, UInt64 b, Expr ptr_base)
            {
                bool mask_present = VPUMaskPresent(mask, elem_count);
                if (!TryAppendByte((byte)op)) return false;
                if (!TryAppendByte(mode)) return false;
                if (!TryAppendByte((byte)((dest << 3) | (4ul) | (mask_present ? 2 : 0ul) | (zmask ? 1 : 0ul)))) return false;
                if (mask_present && !TryAppendExpr(BitsToBytes(elem_count), mask)) return false;
                if (!TryAppendAddress(a, b, ptr_base)) return false;

                return true;
            }

            public bool TryProcessVPUCVT_packed_f2i(OPCode op, bool trunc, bool single)
            {
                if (args.Length != 2) { res = new AssembleResult(AssembleError.ArgCount, $"line {line}: Expected 2 operands"); return false; }

                byte mode = (byte)(trunc ? 34 : 28); // decoded vpucvt mode
                if (single) mode += 3;

                // extract the mask
                if (!TryExtractVPUMask(ref args[0], out Expr mask, out bool zmask)) return false;

                // dest must be vpu register
                if (!TryParseVPURegister(args[0], out UInt64 dest, out UInt64 dest_sizecode)) return false;

                // if src is a vpu register
                if (TryParseVPURegister(args[1], out UInt64 src, out UInt64 src_sizecode))
                {
                    // validate operand sizes
                    if (dest_sizecode != (single ? src_sizecode : src_sizecode == 4 ? 4 : src_sizecode - 1)) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Specified operand size combination not supported"); return false; }
                    
                    // decode size and get number of elements
                    mode += (byte)(src_sizecode - 4);
                    UInt64 elem_count = (single ? 4 : 2ul) << (ushort)(src_sizecode - 4);

                    // write the data
                    if (!__TryProcessVPUCVT_packed_formatter_reg(op, mode, elem_count, dest, mask, zmask, src)) return false;
                }
                // if src is mem
                else if (args[1][args[1].Length - 1] == ']')
                {
                    if (!TryParseAddress(args[1], out UInt64 a, out UInt64 b, out Expr ptr_base, out src_sizecode, out bool src_explicit)) return false;

                    // validate operand sizes
                    if (src_explicit && dest_sizecode != (single ? src_sizecode : src_sizecode - 1)) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Specified operand size combination not supported"); return false; }
                    
                    // decode size and get number of elements
                    UInt64 elem_count;
                    // single is converting to same size, so we can use dest_sz instead
                    if (single)
                    {
                        mode += (byte)(dest_sizecode - 4);
                        elem_count = 4ul << (ushort)(dest_sizecode - 4);
                    }
                    // otherwise we can't tell 2 vs 4 doubles since they both use xmm dest - we need explicit source size
                    else if (src_explicit)
                    {
                        mode += (byte)(src_sizecode - 4);
                        elem_count = 2ul << (ushort)(src_sizecode - 4);
                    }
                    // however, if dest is ymm, we know source is zmm
                    else if (dest_sizecode == 5)
                    {
                        mode += 2;
                        elem_count = 8;
                    }
                    else { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Could not deduce operand size"); return false; }

                    // write the data
                    if (!__TryProcessVPUCVT_packed_formatter_mem(op, mode, elem_count, dest, mask, zmask, a, b, ptr_base)) return false;
                }
                else { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Expected a VPU register or memory value as second operand"); return false; }

                return true;
            }
            public bool TryProcessVPUCVT_packed_i2f(OPCode op, bool single)
            {
                if (args.Length != 2) { res = new AssembleResult(AssembleError.ArgCount, $"line {line}: Expected 2 operands"); return false; }

                byte mode = (byte)(single ? 43 : 40); // decoded vpucvt mode

                // extract the mask
                if (!TryExtractVPUMask(ref args[0], out Expr mask, out bool zmask)) return false;

                // dest must be vpu register
                if (!TryParseVPURegister(args[0], out UInt64 dest, out UInt64 dest_sizecode)) return false;

                // if src is a vpu register
                if (TryParseVPURegister(args[1], out UInt64 src, out UInt64 src_sizecode))
                {
                    // validate operand sizes
                    if (src_sizecode != (single ? dest_sizecode : dest_sizecode == 4 ? 4 : dest_sizecode - 1)) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Specified operand size combination not supported"); return false; }
                    
                    // decode size and get number of elements
                    mode += (byte)(dest_sizecode - 4);
                    UInt64 elem_count = (single ? 4 : 2ul) << (ushort)(dest_sizecode - 4);

                    // write the data
                    if (!__TryProcessVPUCVT_packed_formatter_reg(op, mode, elem_count, dest, mask, zmask, src)) return false;
                }
                // if src is mem
                else if (args[1][args[1].Length - 1] == ']')
                {
                    if (!TryParseAddress(args[1], out UInt64 a, out UInt64 b, out Expr ptr_base, out src_sizecode, out bool src_explicit)) return false;

                    // validate operand sizes
                    if (src_explicit && src_sizecode != (single ? dest_sizecode : dest_sizecode - 1)) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Specified operand size combination not supported"); return false; }
                    
                    // decode size and get number of elements
                    mode += (byte)(dest_sizecode - 4);
                    UInt64 elem_count = (single ? 4 : 2ul) << (ushort)(dest_sizecode - 4);
                    
                    // write the data
                    if (!__TryProcessVPUCVT_packed_formatter_mem(op, mode, elem_count, dest, mask, zmask, a, b, ptr_base)) return false;
                }
                else { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Expected a VPU register or memory value as second operand"); return false; }

                return true;
            }
            public bool TryProcessVPUCVT_packed_f2f(OPCode op, bool extend)
            {
                if (args.Length != 2) { res = new AssembleResult(AssembleError.ArgCount, $"line {line}: Expected 2 operands"); return false; }

                byte mode = (byte)(extend ? 49 : 46); // decoded vpucvt mode

                // extract the mask
                if (!TryExtractVPUMask(ref args[0], out Expr mask, out bool zmask)) return false;

                // dest must be vpu register
                if (!TryParseVPURegister(args[0], out UInt64 dest, out UInt64 dest_sizecode)) return false;

                // if src is a vpu register
                if (TryParseVPURegister(args[1], out UInt64 src, out UInt64 src_sizecode))
                {
                    UInt64 elem_count;
                    if (extend)
                    {
                        // validate operand sizes
                        if (src_sizecode != (dest_sizecode == 4 ? 4 : dest_sizecode - 1)) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Specified operand size combination not supported"); return false; }
                        
                        // decode size and get number of elements
                        mode += (byte)(dest_sizecode - 4);
                        elem_count = 2ul << (ushort)(dest_sizecode - 4);
                    }
                    else
                    {
                        // validate operand sizes
                        if (dest_sizecode != (src_sizecode == 4 ? 4 : src_sizecode - 1)) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Specified operand size combination not supported"); return false; }
                        
                        // decode size and get number of elements
                        mode += (byte)(src_sizecode - 4);
                        elem_count = 2ul << (ushort)(src_sizecode - 4);
                    }

                    // write the data
                    if (!__TryProcessVPUCVT_packed_formatter_reg(op, mode, elem_count, dest, mask, zmask, src)) return false;
                }
                // if src is mem
                else if (args[1][args[1].Length - 1] == ']')
                {
                    if (!TryParseAddress(args[1], out UInt64 a, out UInt64 b, out Expr ptr_base, out src_sizecode, out bool src_explicit)) return false;

                    UInt64 elem_count;
                    if (extend)
                    {
                        // validate operand sizes
                        if (src_explicit && src_sizecode != dest_sizecode - 1) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Specified operand size combination not supported"); return false; }
                        
                        // decode size and get number of elements
                        mode += (byte)(dest_sizecode - 4);
                        elem_count = 2ul << (ushort)(dest_sizecode - 4);
                    }
                    else
                    {
                        // validate operand sizes
                        if (src_explicit && dest_sizecode != src_sizecode - 1) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Specified operand size combination not supported"); return false; }
                        
                        // we can't tell 2 vs 4 elements since they both use xmm dest - we need explicit source size
                        if (src_explicit)
                        {
                            mode += (byte)(src_sizecode - 4);
                            elem_count = 2ul << (ushort)(src_sizecode - 4);
                        }
                        // however, if dest is ymm, we know source is zmm
                        else if (dest_sizecode == 5)
                        {
                            mode += 2;
                            elem_count = 8;
                        }
                        else { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Could not deduce operand size"); return false; }
                    }

                    // write the data
                    if (!__TryProcessVPUCVT_packed_formatter_mem(op, mode, elem_count, dest, mask, zmask, a, b, ptr_base)) return false;
                }
                else { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Expected a VPU register or memory value as second operand"); return false; }

                return true;
            }
        }

        /// <summary>
        /// Stores all the external predefined symbols that are not defined by the assembler itself
        /// </summary>
        private static readonly Dictionary<string, Expr> PredefinedSymbols = new Dictionary<string, Expr>();

        /// <summary>
        /// Creates a new predefined symbol for the assembler
        /// </summary>
        /// <param name="key">the symol name</param>
        /// <param name="value">the symbol value</param>
        public static void DefineSymbol(string key, string value) { PredefinedSymbols.Add(key, new Expr() { Token = value }); }
        /// <summary>
        /// Creates a new predefined symbol for the assembler
        /// </summary>
        /// <param name="key">the symol name</param>
        /// <param name="value">the symbol value</param>
        public static void DefineSymbol(string key, UInt64 value) { PredefinedSymbols.Add(key, new Expr() { IntResult = value }); }
        /// <summary>
        /// Creates a new predefined symbol for the assembler
        /// </summary>
        /// <param name="key">the symol name</param>
        /// <param name="value">the symbol value</param>
        public static void DefineSymbol(string key, double value) { PredefinedSymbols.Add(key, new Expr() { FloatResult = value }); }

        // -----------------------------------

        /// <summary>
        /// Attempts to patch the hole. returns PatchError.None on success
        /// </summary>
        /// <param name="res">data array to patch</param>
        /// <param name="symbols">the symbols used for lookup</param>
        /// <param name="data">the hole's data</param>
        /// <param name="err">the resulting error on failure</param>
        private static PatchError TryPatchHole<T>(T res, Dictionary<string, Expr> symbols, HoleData data, ref string err) where T : IList<byte>
        {
            // if we can fill it immediately, do so
            if (data.Expr.Evaluate(symbols, out UInt64 val, out bool floating, ref err))
            {
                // if it's floating-point
                if (floating)
                {
                    // only 64-bit and 32-bit are supported
                    switch (data.Size)
                    {
                        case 8: if (!res.Write(data.Address, 8, val)) { err = $"line {data.Line}: Error writing value"; return PatchError.Error; } break;
                        case 4: if (!res.Write(data.Address, 4, FloatAsUInt64((float)AsDouble(val)))) { err = $"line {data.Line}: Error writing value"; return PatchError.Error; } break;

                        default: err = $"line {data.Line}: Attempt to use unsupported floating-point format"; return PatchError.Error;
                    }
                }
                // otherwise it's integral
                else if (!res.Write(data.Address, data.Size, val)) { err = $"line {data.Line}: Error writing value"; return PatchError.Error; }

                // successfully patched
                return PatchError.None;
            }
            // otherwise it's unevaluated
            else { err = $"line {data.Line}: Failed to evaluate expression\n-> {err}"; return PatchError.Unevaluated; }
        }

        /// <summary>
        /// tries to patch and eliminate as many holes as possible. returns true unless there was a hard error during evaluation (e.g. unsupported floating-point format).
        /// </summary>
        private static bool _ElimHoles(Dictionary<string, Expr> symbols, List<HoleData> holes, List<byte> seg, ref AssembleResult res)
        {
            for (int i = holes.Count - 1; i >= 0; --i)
            {
                switch (TryPatchHole(seg, symbols, holes[i], ref res.ErrorMsg))
                {
                    case PatchError.None: holes.RemoveAt(i); break; // remove the hole if we solved it
                    case PatchError.Unevaluated: break;
                    case PatchError.Error: res.Error = AssembleError.ArgError; return false;

                    default: throw new ArgumentException("Unknown patch error encountered");
                }
            }

            return true;
        }
        /// <summary>
        /// tries to patch all holes. returns true only if all holes were patched.
        /// </summary>
        private static bool _FixAllHoles(Dictionary<string, Expr> symbols, List<HoleData> holes, List<byte> seg, ref LinkResult res)
        {
            for (int i = 0; i < holes.Count; ++i)
            {
                switch (TryPatchHole(seg, symbols, holes[i], ref res.ErrorMsg))
                {
                    case PatchError.None: break;
                    case PatchError.Unevaluated: res.Error = LinkError.MissingSymbol; return false;
                    case PatchError.Error: res.Error = LinkError.FormatError; return false;

                    default: throw new ArgumentException("Unknown patch error encountered");
                }
            }

            return true;
        }

        // ----------------------------------------------

        /// <summary>
        /// Assembles the code into an object file
        /// </summary>
        /// <param name="code">the code to assemble</param>
        /// <param name="file">the resulting object file if no errors occur</param>
        public static AssembleResult Assemble(string code, out ObjectFile file)
        {
            AssembleArgs args = new AssembleArgs()
            {
                file = file = new ObjectFile(),

                current_seg = AsmSegment.INVALID,
                done_segs = AsmSegment.INVALID,

                line = 0,
                line_pos_in_seg = 0,

                last_nonlocal_label = null,

                res = default(AssembleResult)
            };

            // create the table of predefined symbols
            args.file.Symbols = new Dictionary<string, Expr>(PredefinedSymbols)
            {
                ["__version__"] = new Expr() { IntResult = Utility.Version },

                ["__pinf__"] = new Expr() { FloatResult = double.PositiveInfinity },
                ["__ninf__"] = new Expr() { FloatResult = double.NegativeInfinity },
                ["__nan__"] = new Expr() { FloatResult = double.NaN },

                ["__fmax__"] = new Expr() { FloatResult = double.MaxValue },
                ["__fmin__"] = new Expr() { FloatResult = double.MinValue },
                ["__fepsilon__"] = new Expr() { FloatResult = double.Epsilon },

                ["__pi__"] = new Expr() { FloatResult = Math.PI },
                ["__e__"] = new Expr() { FloatResult = Math.E }
            };

            int pos = 0, end = 0; // position in code

            // potential parsing args for an instruction
            UInt64 a = 0, b = 0;
            bool floating, btemp;

            string err = null; // error location for evaluation

            if (code.Length == 0) return new AssembleResult(AssembleError.EmptyFile, "The file was empty");

            while (pos < code.Length)
            {
                // update current line pos
                switch (args.current_seg)
                {
                    case AsmSegment.TEXT: args.line_pos_in_seg = (UInt64)args.file.Text.Count; break;
                    case AsmSegment.RODATA: args.line_pos_in_seg = (UInt64)args.file.Rodata.Count; break;
                    case AsmSegment.DATA: args.line_pos_in_seg = (UInt64)args.file.Data.Count; break;
                    case AsmSegment.BSS: args.line_pos_in_seg = args.file.BssLen; break;

                    // default does nothing - it is ill-formed to make an address outside of any segment
                }

                // find the next separator
                for (end = pos; end < code.Length && code[end] != '\n' && code[end] != CommentChar; ++end) ;

                ++args.line; // advance line counter

				// extract the line
				string rawline = code.Substring(pos, end - pos);

				// if this is a shebang line (must have "#!" at the start of line 1)
				if (args.line == 1 && rawline.Length >= 2 && rawline[0] == '#' && rawline[1] == '!')
				{
					// ignore it - do this by pretending the line was empty
					rawline = string.Empty;
				}

				// split the line
				if (!args.SplitLine(rawline)) return new AssembleResult(AssembleError.FormatError, $"line {args.line}: Failed to parse line\n-> {args.res.ErrorMsg}");
                // if the separator was a comment character, consume the rest of the line as well as no-op
                if (end < code.Length && code[end] == CommentChar)
                    for (; end < code.Length && code[end] != '\n'; ++end) ;

                // process marked label
                if (!args.TryProcessLabel()) return args.res;

                // empty lines are ignored
                if (args.op != string.Empty)
                {
                    // -- directive routing -- //
                    switch (args.op.ToUpper())
                    {
                        case "GLOBAL": if (!args.TryProcessGlobal()) return args.res; goto op_done;
                        case "EXTERN": if (!args.TryProcessExtern()) return args.res; goto op_done;

                        case "ALIGN": if (!args.TryProcessAlign()) return args.res; goto op_done;

                        case "ALIGNB": goto op_done;
                        case "ALIGNW": if (!args.TryProcessAlignXX(2)) return args.res; goto op_done;
                        case "ALIGND": if (!args.TryProcessAlignXX(4)) return args.res; goto op_done;
                        case "ALIGNQ": if (!args.TryProcessAlignXX(8)) return args.res; goto op_done;
                        case "ALIGNX": if (!args.TryProcessAlignXX(16)) return args.res; goto op_done;
                        case "ALIGNY": if (!args.TryProcessAlignXX(32)) return args.res; goto op_done;
                        case "ALIGNZ": if (!args.TryProcessAlignXX(64)) return args.res; goto op_done;

                        case "DB": if (!args.TryProcessDeclare(1)) return args.res; goto op_done;
                        case "DW": if (!args.TryProcessDeclare(2)) return args.res; goto op_done;
                        case "DD": if (!args.TryProcessDeclare(4)) return args.res; goto op_done;
                        case "DQ": if (!args.TryProcessDeclare(8)) return args.res; goto op_done;
                        case "DX": if (!args.TryProcessDeclare(16)) return args.res; goto op_done;
                        case "DY": if (!args.TryProcessDeclare(32)) return args.res; goto op_done;
                        case "DZ": if (!args.TryProcessDeclare(64)) return args.res; goto op_done;

                        case "RESB": if (!args.TryProcessReserve(1)) return args.res; goto op_done;
                        case "RESW": if (!args.TryProcessReserve(2)) return args.res; goto op_done;
                        case "RESD": if (!args.TryProcessReserve(4)) return args.res; goto op_done;
                        case "RESQ": if (!args.TryProcessReserve(8)) return args.res; goto op_done;
                        case "RESX": if (!args.TryProcessReserve(16)) return args.res; goto op_done;
                        case "RESY": if (!args.TryProcessReserve(32)) return args.res; goto op_done;
                        case "RESZ": if (!args.TryProcessReserve(64)) return args.res; goto op_done;

                        case "EQU": if (!args.TryProcessEQU()) return args.res; goto op_done;

                        case "SEGMENT": case "SECTION": if (!args.TryProcessSegment()) return args.res; goto op_done;
                    }

                    // if it wasn't a directive it's about to be an instruction: make sure we're in the text segment
                    if (args.current_seg != AsmSegment.TEXT) return new AssembleResult(AssembleError.FormatError, $"line {args.line}: Attempt to write executable instructions to the {args.current_seg} segment");

                    // -- instruction routing -- //
                    switch (args.op.ToUpper())
                    {
                        // unmapped

                        case "LFENCE": if (!args.TryProcessNoArgOp_no_write()) return args.res; break;
                        case "SFENCE": if (!args.TryProcessNoArgOp_no_write()) return args.res; break;
                        case "MFENCE": if (!args.TryProcessNoArgOp_no_write()) return args.res; break;

                        case "PAUSE": if (!args.TryProcessNoArgOp_no_write()) return args.res; break;

                        // x86 instructions

                        case "NOP": if (!args.TryProcessNoArgOp(OPCode.NOP)) return args.res; break;

                        case "HLT": if (!args.TryProcessNoArgOp(OPCode.HLT)) return args.res; break;
                        case "SYSCALL": if (!args.TryProcessNoArgOp(OPCode.SYSCALL)) return args.res; break;

                        case "PUSHF": if (!args.TryProcessNoArgOp(OPCode.STLDF, true, 0)) return args.res; break;
                        case "PUSHFD": if (!args.TryProcessNoArgOp(OPCode.STLDF, true, 1)) return args.res; break;
                        case "PUSHFQ": if (!args.TryProcessNoArgOp(OPCode.STLDF, true, 2)) return args.res; break;

                        case "POPF": if (!args.TryProcessNoArgOp(OPCode.STLDF, true, 3)) return args.res; break;
                        case "POPFD": if (!args.TryProcessNoArgOp(OPCode.STLDF, true, 4)) return args.res; break;
                        case "POPFQ": if (!args.TryProcessNoArgOp(OPCode.STLDF, true, 5)) return args.res; break;

                        case "SAHF": if (!args.TryProcessNoArgOp(OPCode.STLDF, true, 6)) return args.res; break;
                        case "LAHF": if (!args.TryProcessNoArgOp(OPCode.STLDF, true, 7)) return args.res; break;

                        case "STC": if (!args.TryProcessNoArgOp(OPCode.FlagManip, true, 0)) return args.res; break;
                        case "CLC": if (!args.TryProcessNoArgOp(OPCode.FlagManip, true, 1)) return args.res; break;
                        case "STI": if (!args.TryProcessNoArgOp(OPCode.FlagManip, true, 2)) return args.res; break;
                        case "CLI": if (!args.TryProcessNoArgOp(OPCode.FlagManip, true, 3)) return args.res; break;
                        case "STD": if (!args.TryProcessNoArgOp(OPCode.FlagManip, true, 4)) return args.res; break;
                        case "CLD": if (!args.TryProcessNoArgOp(OPCode.FlagManip, true, 5)) return args.res; break;
                        case "STAC": if (!args.TryProcessNoArgOp(OPCode.FlagManip, true, 6)) return args.res; break;
                        case "CLAC": if (!args.TryProcessNoArgOp(OPCode.FlagManip, true, 7)) return args.res; break;
                        case "CMC": if (!args.TryProcessNoArgOp(OPCode.FlagManip, true, 8)) return args.res; break;

                        case "SETZ": case "SETE": if (!args.TryProcessUnaryOp(OPCode.SETcc, true, 0, 1)) return args.res; break;
                        case "SETNZ": case "SETNE": if (!args.TryProcessUnaryOp(OPCode.SETcc, true, 1, 1)) return args.res; break;
                        case "SETS": if (!args.TryProcessUnaryOp(OPCode.SETcc, true, 2, 1)) return args.res; break;
                        case "SETNS": if (!args.TryProcessUnaryOp(OPCode.SETcc, true, 3, 1)) return args.res; break;
                        case "SETP": case "SETPE": if (!args.TryProcessUnaryOp(OPCode.SETcc, true, 4, 1)) return args.res; break;
                        case "SETNP": case "SETPO": if (!args.TryProcessUnaryOp(OPCode.SETcc, true, 5, 1)) return args.res; break;
                        case "SETO": if (!args.TryProcessUnaryOp(OPCode.SETcc, true, 6, 1)) return args.res; break;
                        case "SETNO": if (!args.TryProcessUnaryOp(OPCode.SETcc, true, 7, 1)) return args.res; break;
                        case "SETC": if (!args.TryProcessUnaryOp(OPCode.SETcc, true, 8, 1)) return args.res; break;
                        case "SETNC": if (!args.TryProcessUnaryOp(OPCode.SETcc, true, 9, 1)) return args.res; break;

                        case "SETB": case "SETNAE": if (!args.TryProcessUnaryOp(OPCode.SETcc, true, 10, 1)) return args.res; break;
                        case "SETBE": case "SETNA": if (!args.TryProcessUnaryOp(OPCode.SETcc, true, 11, 1)) return args.res; break;
                        case "SETA": case "SETNBE": if (!args.TryProcessUnaryOp(OPCode.SETcc, true, 12, 1)) return args.res; break;
                        case "SETAE": case "SETNB": if (!args.TryProcessUnaryOp(OPCode.SETcc, true, 13, 1)) return args.res; break;

                        case "SETL": case "SETNGE": if (!args.TryProcessUnaryOp(OPCode.SETcc, true, 14, 1)) return args.res; break;
                        case "SETLE": case "SETNG": if (!args.TryProcessUnaryOp(OPCode.SETcc, true, 15, 1)) return args.res; break;
                        case "SETG": case "SETNLE": if (!args.TryProcessUnaryOp(OPCode.SETcc, true, 16, 1)) return args.res; break;
                        case "SETGE": case "SETNL": if (!args.TryProcessUnaryOp(OPCode.SETcc, true, 17, 1)) return args.res; break;

                        case "MOV": if (!args.TryProcessBinaryOp(OPCode.MOV)) return args.res; break;

                        case "MOVZ": case "MOVE": if (!args.TryProcessBinaryOp(OPCode.MOVcc, true, 0)) return args.res; break;
                        case "MOVNZ": case "MOVNE": if (!args.TryProcessBinaryOp(OPCode.MOVcc, true, 1)) return args.res; break;
                        // MOVS (mov) requires disambiguation
                        case "MOVNS": if (!args.TryProcessBinaryOp(OPCode.MOVcc, true, 3)) return args.res; break;
                        case "MOVP": case "MOVPE": if (!args.TryProcessBinaryOp(OPCode.MOVcc, true, 4)) return args.res; break;
                        case "MOVNP": case "MOVPO": if (!args.TryProcessBinaryOp(OPCode.MOVcc, true, 5)) return args.res; break;
                        case "MOVO": if (!args.TryProcessBinaryOp(OPCode.MOVcc, true, 6)) return args.res; break;
                        case "MOVNO": if (!args.TryProcessBinaryOp(OPCode.MOVcc, true, 7)) return args.res; break;
                        case "MOVC": if (!args.TryProcessBinaryOp(OPCode.MOVcc, true, 8)) return args.res; break;
                        case "MOVNC": if (!args.TryProcessBinaryOp(OPCode.MOVcc, true, 9)) return args.res; break;

                        case "MOVB": case "MOVNAE": if (!args.TryProcessBinaryOp(OPCode.MOVcc, true, 10)) return args.res; break;
                        case "MOVBE": case "MOVNA": if (!args.TryProcessBinaryOp(OPCode.MOVcc, true, 11)) return args.res; break;
                        case "MOVA": case "MOVNBE": if (!args.TryProcessBinaryOp(OPCode.MOVcc, true, 12)) return args.res; break;
                        case "MOVAE": case "MOVNB": if (!args.TryProcessBinaryOp(OPCode.MOVcc, true, 13)) return args.res; break;

                        case "MOVL": case "MOVNGE": if (!args.TryProcessBinaryOp(OPCode.MOVcc, true, 14)) return args.res; break;
                        case "MOVLE": case "MOVNG": if (!args.TryProcessBinaryOp(OPCode.MOVcc, true, 15)) return args.res; break;
                        case "MOVG": case "MOVNLE": if (!args.TryProcessBinaryOp(OPCode.MOVcc, true, 16)) return args.res; break;
                        case "MOVGE": case "MOVNL": if (!args.TryProcessBinaryOp(OPCode.MOVcc, true, 17)) return args.res; break;

                        case "XCHG": if (!args.TryProcessXCHG(OPCode.XCHG)) return args.res; break;

                        case "JMP": if (!args.TryProcessIMMRM(OPCode.JMP, false, 0, 14, 3)) return args.res; break;

                        case "JZ": case "JE": if (!args.TryProcessIMMRM(OPCode.Jcc, true, 0, 14, 3)) return args.res; break;
                        case "JNZ": case "JNE": if (!args.TryProcessIMMRM(OPCode.Jcc, true, 1, 14, 3)) return args.res; break;
                        case "JS": if (!args.TryProcessIMMRM(OPCode.Jcc, true, 2, 14, 3)) return args.res; break;
                        case "JNS": if (!args.TryProcessIMMRM(OPCode.Jcc, true, 3, 14, 3)) return args.res; break;
                        case "JP": case "JPE": if (!args.TryProcessIMMRM(OPCode.Jcc, true, 4, 14, 3)) return args.res; break;
                        case "JNP": case "JPO": if (!args.TryProcessIMMRM(OPCode.Jcc, true, 5, 14, 3)) return args.res; break;
                        case "JO": if (!args.TryProcessIMMRM(OPCode.Jcc, true, 6, 14, 3)) return args.res; break;
                        case "JNO": if (!args.TryProcessIMMRM(OPCode.Jcc, true, 7, 14, 3)) return args.res; break;
                        case "JC": if (!args.TryProcessIMMRM(OPCode.Jcc, true, 8, 14, 3)) return args.res; break;
                        case "JNC": if (!args.TryProcessIMMRM(OPCode.Jcc, true, 9, 14, 3)) return args.res; break;

                        case "JB": case "JNAE": if (!args.TryProcessIMMRM(OPCode.Jcc, true, 10, 14, 3)) return args.res; break;
                        case "JBE": case "JNA": if (!args.TryProcessIMMRM(OPCode.Jcc, true, 11, 14, 3)) return args.res; break;
                        case "JA": case "JNBE": if (!args.TryProcessIMMRM(OPCode.Jcc, true, 12, 14, 3)) return args.res; break;
                        case "JAE": case "JNB": if (!args.TryProcessIMMRM(OPCode.Jcc, true, 13, 14, 3)) return args.res; break;

                        case "JL": case "JNGE": if (!args.TryProcessIMMRM(OPCode.Jcc, true, 14, 14, 3)) return args.res; break;
                        case "JLE": case "JNG": if (!args.TryProcessIMMRM(OPCode.Jcc, true, 15, 14, 3)) return args.res; break;
                        case "JG": case "JNLE": if (!args.TryProcessIMMRM(OPCode.Jcc, true, 16, 14, 3)) return args.res; break;
                        case "JGE": case "JNL": if (!args.TryProcessIMMRM(OPCode.Jcc, true, 17, 14, 3)) return args.res; break;

                        case "JCXZ": if (!args.TryProcessIMMRM(OPCode.Jcc, true, 18, 2, 1)) return args.res; break;
                        case "JECXZ": if (!args.TryProcessIMMRM(OPCode.Jcc, true, 18, 4, 2)) return args.res; break;
                        case "JRCXZ": if (!args.TryProcessIMMRM(OPCode.Jcc, true, 18, 8, 3)) return args.res; break;

                        case "LOOP": if (!args.TryProcessIMMRM(OPCode.LOOPcc, true, 0, 14, 3)) return args.res; break;
                        case "LOOPZ": case "LOOPE": if (!args.TryProcessIMMRM(OPCode.LOOPcc, true, 1, 14, 3)) return args.res; break;
                        case "LOOPNZ": case "LOOPNE": if (!args.TryProcessIMMRM(OPCode.LOOPcc, true, 2, 14, 3)) return args.res; break;

                        case "CALL": if (!args.TryProcessIMMRM(OPCode.CALL, false, 0, 14, 3)) return args.res; break;
                        case "RET": if (!args.TryProcessNoArgOp(OPCode.RET)) return args.res; break;

                        case "PUSH": if (!args.TryProcessIMMRM(OPCode.PUSH, false, 0, 14)) return args.res; break;
                        case "POP": if (!args.TryProcessPOP(OPCode.POP)) return args.res; break;

                        case "LEA": if (!args.TryProcessLEA(OPCode.LEA)) return args.res; break;

                        case "ADD": if (!args.TryProcessBinaryOp(OPCode.ADD)) return args.res; break;
                        case "SUB": if (!args.TryProcessBinaryOp(OPCode.SUB)) return args.res; break;

                        case "MUL": if (!args.TryProcessIMMRM(OPCode.MUL_x, true, 0)) return args.res; break;
                        case "MULX": if (!args.TryProcessRR_RM(OPCode.MUL_x, true, 1, 12)) return args.res; break;
                        case "IMUL":
                            switch (args.args.Length)
                            {
                                case 1: if (!args.TryProcessIMMRM(OPCode.IMUL, true, 0)) return args.res; break;
                                case 2: if (!args.TryProcessBinaryOp(OPCode.IMUL, true, 1)) return args.res; break;
                                case 3: if (!args.TryProcessTernaryOp(OPCode.IMUL, true, 2)) return args.res; break;

                                default: return new AssembleResult(AssembleError.ArgCount, $"line {args.line}: IMUL expected 1, 2, or 3 args");
                            }
                            break;
                        case "DIV": if (!args.TryProcessIMMRM(OPCode.DIV)) return args.res; break;
                        case "IDIV": if (!args.TryProcessIMMRM(OPCode.IDIV)) return args.res; break;

                        case "SHL": if (!args.TryProcessShift(OPCode.SHL)) return args.res; break;
                        case "SHR": if (!args.TryProcessShift(OPCode.SHR)) return args.res; break;
                        case "SAL": if (!args.TryProcessShift(OPCode.SAL)) return args.res; break;
                        case "SAR": if (!args.TryProcessShift(OPCode.SAR)) return args.res; break;
                        case "ROL": if (!args.TryProcessShift(OPCode.ROL)) return args.res; break;
                        case "ROR": if (!args.TryProcessShift(OPCode.ROR)) return args.res; break;
                        case "RCL": if (!args.TryProcessShift(OPCode.RCL)) return args.res; break;
                        case "RCR": if (!args.TryProcessShift(OPCode.RCR)) return args.res; break;

                        case "AND": if (!args.TryProcessBinaryOp(OPCode.AND)) return args.res; break;
                        case "OR": if (!args.TryProcessBinaryOp(OPCode.OR)) return args.res; break;
                        case "XOR": if (!args.TryProcessBinaryOp(OPCode.XOR)) return args.res; break;

                        case "INC": if (!args.TryProcessUnaryOp(OPCode.INC)) return args.res; break;
                        case "DEC": if (!args.TryProcessUnaryOp(OPCode.DEC)) return args.res; break;
                        case "NEG": if (!args.TryProcessUnaryOp(OPCode.NEG)) return args.res; break;
                        case "NOT": if (!args.TryProcessUnaryOp(OPCode.NOT)) return args.res; break;

                        case "CMP":
                            // if there are 2 args and the second one is an instant 0, we can make this a CMPZ instruction
                            if (args.args.Length == 2 && args.TryParseInstantImm(args.args[1], out a, out floating, out b, out btemp) && a == 0)
                            {
                                // set new args for the unary version
                                args.args = new string[] { args.args[0] };
                                if (!args.TryProcessUnaryOp(OPCode.CMPZ)) return args.res;
                            }
                            // otherwise normal binary
                            else if (!args.TryProcessBinaryOp(OPCode.CMP)) return args.res;
                            break;
                        case "TEST": if (!args.TryProcessBinaryOp(OPCode.TEST)) return args.res; break;

                        case "BSWAP": if (!args.TryProcessUnaryOp(OPCode.BSWAP)) return args.res; break;
                        case "BEXTR": if (!args.TryProcessBinaryOp(OPCode.BEXTR, false, 0, 15, 1)) return args.res; break;
                        case "BLSI": if (!args.TryProcessUnaryOp(OPCode.BLSI)) return args.res; break;
                        case "BLSMSK": if (!args.TryProcessUnaryOp(OPCode.BLSMSK)) return args.res; break;
                        case "BLSR": if (!args.TryProcessUnaryOp(OPCode.BLSR)) return args.res; break;
                        case "ANDN": if (!args.TryProcessRR_RM(OPCode.ANDN, false, 0, 12)) return args.res; break;

                        case "BT": if (!args.TryProcessBinaryOp_NoBMem(OPCode.BTx, true, 0, 15, 0)) return args.res; break;
                        case "BTS": if (!args.TryProcessBinaryOp_NoBMem(OPCode.BTx, true, 1, 15, 0)) return args.res; break;
                        case "BTR": if (!args.TryProcessBinaryOp_NoBMem(OPCode.BTx, true, 2, 15, 0)) return args.res; break;
                        case "BTC": if (!args.TryProcessBinaryOp_NoBMem(OPCode.BTx, true, 3, 15, 0)) return args.res; break;
                            
                        case "CWD": if (!args.TryProcessNoArgOp(OPCode.Cxy, true, 0)) return args.res; break;
                        case "CDQ": if (!args.TryProcessNoArgOp(OPCode.Cxy, true, 1)) return args.res; break;
                        case "CQO": if (!args.TryProcessNoArgOp(OPCode.Cxy, true, 2)) return args.res; break;

                        case "CBW": if (!args.TryProcessNoArgOp(OPCode.Cxy, true, 3)) return args.res; break;
                        case "CWDE": if (!args.TryProcessNoArgOp(OPCode.Cxy, true, 4)) return args.res; break;
                        case "CDQE": if (!args.TryProcessNoArgOp(OPCode.Cxy, true, 5)) return args.res; break;

                        case "MOVZX": if (!args.TryProcessMOVxX(OPCode.MOVxX, false)) return args.res; break;
                        case "MOVSX": if (!args.TryProcessMOVxX(OPCode.MOVxX, true)) return args.res; break;

                        case "ADC": if (!args.TryProcessBinaryOp(OPCode.ADXX, true, 0)) return args.res; break;
                        case "ADCX": if (!args.TryProcessBinaryOp_R_RM(OPCode.ADXX, true, 1, 12)) return args.res; break;
                        case "ADOX": if (!args.TryProcessBinaryOp_R_RM(OPCode.ADXX, true, 2, 12)) return args.res; break;

                        case "AAA": if (!args.TryProcessNoArgOp(OPCode.AAX, true, 0)) return args.res; break;
                        case "AAS": if (!args.TryProcessNoArgOp(OPCode.AAX, true, 1)) return args.res; break;
                        case "DAA": if (!args.TryProcessNoArgOp(OPCode.AAX, true, 2)) return args.res; break;
                        case "DAS": if (!args.TryProcessNoArgOp(OPCode.AAX, true, 3)) return args.res; break;

                        // MOVS (string) requires disambiguation
                        case "MOVSB": if (!args.TryProcessNoArgOp(OPCode.string_ops, true, 0)) return args.res; break;
                        case "MOVSW": if (!args.TryProcessNoArgOp(OPCode.string_ops, true, 1)) return args.res; break;
                        // MOVSD (string) requires disambiguation
                        case "MOVSQ": if (!args.TryProcessNoArgOp(OPCode.string_ops, true, 3)) return args.res; break;

                        case "CMPS": if (!args.TryProcessCMPS_string(OPCode.string_ops, false, false)) return args.res; break;
                        case "CMPSB": if (!args.TryProcessNoArgOp(OPCode.string_ops, true, (2 << 2) | 0)) return args.res; break;
                        case "CMPSW": if (!args.TryProcessNoArgOp(OPCode.string_ops, true, (2 << 2) | 1)) return args.res; break;
                        // CMPSD (string) requires disambiguation
                        case "CMPSQ": if (!args.TryProcessNoArgOp(OPCode.string_ops, true, (2 << 2) | 3)) return args.res; break;

                        case "LODS": if (!args.TryProcessLODS_string(OPCode.string_ops, false)) return args.res; break;
                        case "LODSB": if (!args.TryProcessNoArgOp(OPCode.string_ops, true, (5 << 2) | 0)) return args.res; break;
                        case "LODSW": if (!args.TryProcessNoArgOp(OPCode.string_ops, true, (5 << 2) | 1)) return args.res; break;
                        case "LODSD": if (!args.TryProcessNoArgOp(OPCode.string_ops, true, (5 << 2) | 2)) return args.res; break;
                        case "LODSQ": if (!args.TryProcessNoArgOp(OPCode.string_ops, true, (5 << 2) | 3)) return args.res; break;

                        case "STOS": if (!args.TryProcessSTOS_string(OPCode.string_ops, false)) return args.res; break;
                        case "STOSB": if (!args.TryProcessNoArgOp(OPCode.string_ops, true, (7 << 2) | 0)) return args.res; break;
                        case "STOSW": if (!args.TryProcessNoArgOp(OPCode.string_ops, true, (7 << 2) | 1)) return args.res; break;
                        case "STOSD": if (!args.TryProcessNoArgOp(OPCode.string_ops, true, (7 << 2) | 2)) return args.res; break;
                        case "STOSQ": if (!args.TryProcessNoArgOp(OPCode.string_ops, true, (7 << 2) | 3)) return args.res; break;

                        case "SCAS": if (!args.TryProcessSCAS_string(OPCode.string_ops, false, false)) return args.res; break;
                        case "SCASB": if (!args.TryProcessNoArgOp(OPCode.string_ops, true, (9 << 2) | 0)) return args.res; break;
                        case "SCASW": if (!args.TryProcessNoArgOp(OPCode.string_ops, true, (9 << 2) | 1)) return args.res; break;
                        case "SCASD": if (!args.TryProcessNoArgOp(OPCode.string_ops, true, (9 << 2) | 2)) return args.res; break;
                        case "SCASQ": if (!args.TryProcessNoArgOp(OPCode.string_ops, true, (9 << 2) | 3)) return args.res; break;

                        case "REP": if (!args.TryProcessREP()) return args.res; break;
                        case "REPE": case "REPZ": if (!args.TryProcessREPE()) return args.res; break;
                        case "REPNE": case "REPNZ": if (!args.TryProcessREPNE()) return args.res; break;

						case "LOCK":
							{
								if (!args.TryProcessPrefixOp(out string actual)) return args.res;

								// decide what handler to use (can't use switch because goto can't jump to an external case label)
								if (actual == "ADD") goto case "ADD";
								if (actual == "ADC") goto case "ADC";
								//if (actual == "XADD") goto case "XADD";

								if (actual == "SUB") goto case "SUB";
								//if (actual == "SBB") goto case "SBB";

								if (actual == "AND") goto case "AND";
								if (actual == "OR") goto case "OR";
								if (actual == "XOR") goto case "XOR";

								if (actual == "BTC") goto case "BTC";
								if (actual == "BTR") goto case "BTR";
								if (actual == "BTS") goto case "BTS";

								if (actual == "DEC") goto case "DEC";
								if (actual == "INC") goto case "INC";

								if (actual == "NEG") goto case "NEG";
								if (actual == "NOT") goto case "NOT";
								if (actual == "XCHG") goto case "XCHG";

								//if (actual == "CMPXCHG") goto case "CMPXCHG";
								//if (actual == "CMPXCH8B") goto case "CMPXCH8B";
								//if (actual == "CMPXCHG16B") goto case "CMPXCHG16B";

								return new AssembleResult(AssembleError.UsageError, $"line {args.line}: LOCK cannot be used with the specified instruction");
							}

                        case "BSF": if (!args.TryProcessBSx(OPCode.BSx, true)) return args.res; break;
                        case "BSR": if (!args.TryProcessBSx(OPCode.BSx, false)) return args.res; break;

                        case "TZCNT": if (!args.TryProcessBSx(OPCode.TZCNT, true)) return args.res; break; // forward flag doesn't matter

                        case "UD": if (!args.TryProcessNoArgOp(OPCode.UD)) return args.res; break;

                        // x87 instructions

                        case "FNOP": if (!args.TryProcessNoArgOp(OPCode.NOP)) return args.res; break; // no sense in wasting another opcode on no-op

                        case "FWAIT": case "WAIT": if (!args.TryProcessNoArgOp(OPCode.FWAIT)) return args.res; break;

                        case "FINIT":
                            if (!args.TryAppendByte((byte)OPCode.FWAIT)) return args.res;
                            goto case "FNINIT"; // c# doesn't allow implicit fallthrough
                        case "FNINIT": if (!args.TryProcessNoArgOp(OPCode.FINIT)) return args.res; break;

                        case "FCLEX":
                            if (!args.TryAppendByte((byte)OPCode.FWAIT)) return args.res;
                            goto case "FNCLEX"; // c# doesn't allow implicit fallthrough
                        case "FNCLEX": if (!args.TryProcessNoArgOp(OPCode.FCLEX)) return args.res; break;

                        case "FSTSW":
                            if (!args.TryAppendByte((byte)OPCode.FWAIT)) return args.res;
                            goto case "FNSTSW"; // c# doesn't allow implicit fallthrough
                        case "FNSTSW": // handle FNSTSW AX case here since the other forms don't allow it
                            if (args.args.Length == 1 && args.args[0].ToUpper() == "AX")
                            {
                                if (!args.TryAppendByte((byte)OPCode.FSTLD_WORD)) return args.res;
                                if (!args.TryAppendByte(0)) return args.res;
                            }
                            else if (!args.TryProcessFSTLD_WORD(OPCode.FSTLD_WORD, 1, 1)) return args.res;
                            break;

                        case "FSTCW":
                            if (!args.TryAppendByte((byte)OPCode.FWAIT)) return args.res;
                            goto case "FNSTCW"; // c# doesn't allow implicit fallthrough
                        case "FNSTCW": if (!args.TryProcessFSTLD_WORD(OPCode.FSTLD_WORD, 2, 1)) return args.res; break;

                        case "FLDCW": if (!args.TryProcessFSTLD_WORD(OPCode.FSTLD_WORD, 3, 1)) return args.res; break;

                        case "FLD1": if (!args.TryProcessNoArgOp(OPCode.FLD_const, true, 0)) return args.res; break;
                        case "FLDL2T": if (!args.TryProcessNoArgOp(OPCode.FLD_const, true, 1)) return args.res; break;
                        case "FLDL2E": if (!args.TryProcessNoArgOp(OPCode.FLD_const, true, 2)) return args.res; break;
                        case "FLDPI": if (!args.TryProcessNoArgOp(OPCode.FLD_const, true, 3)) return args.res; break;
                        case "FLDLG2": if (!args.TryProcessNoArgOp(OPCode.FLD_const, true, 4)) return args.res; break;
                        case "FLDLN2": if (!args.TryProcessNoArgOp(OPCode.FLD_const, true, 5)) return args.res; break;
                        case "FLDZ": if (!args.TryProcessNoArgOp(OPCode.FLD_const, true, 6)) return args.res; break;

                        case "FLD": if (!args.TryProcessFLD(OPCode.FLD, false)) return args.res; break;
                        case "FILD": if (!args.TryProcessFLD(OPCode.FLD, true)) return args.res; break;

                        case "FST": if (!args.TryProcessFST(OPCode.FST, false, false, false)) return args.res; break;
                        case "FIST": if (!args.TryProcessFST(OPCode.FST, true, false, false)) return args.res; break;
                        case "FSTP": if (!args.TryProcessFST(OPCode.FST, false, true, false)) return args.res; break;
                        case "FISTP": if (!args.TryProcessFST(OPCode.FST, true, true, false)) return args.res; break;
                        case "FISTTP": if (!args.TryProcessFST(OPCode.FST, true, true, true)) return args.res; break;

                        case "FXCH": // no arg version swaps st0 and st1
                            if (args.args.Length == 0) { if (!args.TryProcessNoArgOp(OPCode.FXCH, true, 1)) return args.res; break; }
                            else { if (!args.TryProcessFPURegisterOp(OPCode.FXCH)) return args.res; break; }

                        case "FMOVE": if (!args.TryProcessFMOVcc(OPCode.FMOVcc, 0)) return args.res; break;
                        case "FMOVNE": if (!args.TryProcessFMOVcc(OPCode.FMOVcc, 1)) return args.res; break;
                        case "FMOVB": case "FMOVNAE": if (!args.TryProcessFMOVcc(OPCode.FMOVcc, 2)) return args.res; break;
                        case "FMOVBE": case "FMOVNA": if (!args.TryProcessFMOVcc(OPCode.FMOVcc, 3)) return args.res; break;
                        case "FMOVA": case "FMOVNBE": if (!args.TryProcessFMOVcc(OPCode.FMOVcc, 4)) return args.res; break;
                        case "FMOVAE": case "FMOVNB": if (!args.TryProcessFMOVcc(OPCode.FMOVcc, 5)) return args.res; break;
                        case "FMOVU": if (!args.TryProcessFMOVcc(OPCode.FMOVcc, 6)) return args.res; break;
                        case "FMOVNU": if (!args.TryProcessFMOVcc(OPCode.FMOVcc, 7)) return args.res; break;

                        case "FADD": if (!args.TryProcessFPUBinaryOp(OPCode.FADD, false, false)) return args.res; break;
                        case "FADDP": if (!args.TryProcessFPUBinaryOp(OPCode.FADD, false, true)) return args.res; break;
                        case "FIADD": if (!args.TryProcessFPUBinaryOp(OPCode.FADD, true, false)) return args.res; break;

                        case "FSUB": if (!args.TryProcessFPUBinaryOp(OPCode.FSUB, false, false)) return args.res; break;
                        case "FSUBP": if (!args.TryProcessFPUBinaryOp(OPCode.FSUB, false, true)) return args.res; break;
                        case "FISUB": if (!args.TryProcessFPUBinaryOp(OPCode.FSUB, true, false)) return args.res; break;

                        case "FSUBR": if (!args.TryProcessFPUBinaryOp(OPCode.FSUBR, false, false)) return args.res; break;
                        case "FSUBRP": if (!args.TryProcessFPUBinaryOp(OPCode.FSUBR, false, true)) return args.res; break;
                        case "FISUBR": if (!args.TryProcessFPUBinaryOp(OPCode.FSUBR, true, false)) return args.res; break;

                        case "FMUL": if (!args.TryProcessFPUBinaryOp(OPCode.FMUL, false, false)) return args.res; break;
                        case "FMULP": if (!args.TryProcessFPUBinaryOp(OPCode.FMUL, false, true)) return args.res; break;
                        case "FIMUL": if (!args.TryProcessFPUBinaryOp(OPCode.FMUL, true, false)) return args.res; break;

                        case "FDIV": if (!args.TryProcessFPUBinaryOp(OPCode.FDIV, false, false)) return args.res; break;
                        case "FDIVP": if (!args.TryProcessFPUBinaryOp(OPCode.FDIV, false, true)) return args.res; break;
                        case "FIDIV": if (!args.TryProcessFPUBinaryOp(OPCode.FDIV, true, false)) return args.res; break;

                        case "FDIVR": if (!args.TryProcessFPUBinaryOp(OPCode.FDIVR, false, false)) return args.res; break;
                        case "FDIVRP": if (!args.TryProcessFPUBinaryOp(OPCode.FDIVR, false, true)) return args.res; break;
                        case "FIDIVR": if (!args.TryProcessFPUBinaryOp(OPCode.FDIVR, true, false)) return args.res; break;

                        case "F2XM1": if (!args.TryProcessNoArgOp(OPCode.F2XM1)) return args.res; break;
                        case "FABS": if (!args.TryProcessNoArgOp(OPCode.FABS)) return args.res; break;
                        case "FCHS": if (!args.TryProcessNoArgOp(OPCode.FCHS)) return args.res; break;
                        case "FPREM": if (!args.TryProcessNoArgOp(OPCode.FPREM)) return args.res; break;
                        case "FPREM1": if (!args.TryProcessNoArgOp(OPCode.FPREM1)) return args.res; break;
                        case "FRNDINT": if (!args.TryProcessNoArgOp(OPCode.FRNDINT)) return args.res; break;
                        case "FSQRT": if (!args.TryProcessNoArgOp(OPCode.FSQRT)) return args.res; break;
                        case "FYL2X": if (!args.TryProcessNoArgOp(OPCode.FYL2X)) return args.res; break;
                        case "FYL2XP1": if (!args.TryProcessNoArgOp(OPCode.FYL2XP1)) return args.res; break;
                        case "FXTRACT": if (!args.TryProcessNoArgOp(OPCode.FXTRACT)) return args.res; break;
                        case "FSCALE": if (!args.TryProcessNoArgOp(OPCode.FSCALE)) return args.res; break;

                        case "FXAM": if (!args.TryProcessNoArgOp(OPCode.FXAM)) return args.res; break;
                        case "FTST": if (!args.TryProcessNoArgOp(OPCode.FTST)) return args.res; break;

                        case "FCOM": if (!args.TryProcessFCOM(OPCode.FCOM, false, false, false, false, false)) return args.res; break;
                        case "FCOMP": if (!args.TryProcessFCOM(OPCode.FCOM, false, true, false, false, false)) return args.res; break;
                        case "FCOMPP": if (!args.TryProcessFCOM(OPCode.FCOM, false, false, true, false, false)) return args.res; break;

                        case "FUCOM": if (!args.TryProcessFCOM(OPCode.FCOM, false, false, false, false, true)) return args.res; break;
                        case "FUCOMP": if (!args.TryProcessFCOM(OPCode.FCOM, false, true, false, false, true)) return args.res; break;
                        case "FUCOMPP": if (!args.TryProcessFCOM(OPCode.FCOM, false, false, true, false, true)) return args.res; break;

                        case "FCOMI": if (!args.TryProcessFCOM(OPCode.FCOM, false, false, false, true, false)) return args.res; break;
                        case "FCOMIP": if (!args.TryProcessFCOM(OPCode.FCOM, false, true, false, true, false)) return args.res; break;

                        case "FUCOMI": if (!args.TryProcessFCOM(OPCode.FCOM, false, false, false, true, true)) return args.res; break;
                        case "FUCOMIP": if (!args.TryProcessFCOM(OPCode.FCOM, false, true, false, true, true)) return args.res; break;

                        case "FICOM": if (!args.TryProcessFCOM(OPCode.FCOM, true, false, false, false, false)) return args.res; break;
                        case "FICOMP": if (!args.TryProcessFCOM(OPCode.FCOM, true, true, false, false, false)) return args.res; break;

                        case "FSIN": if (!args.TryProcessNoArgOp(OPCode.FSIN)) return args.res; break;
                        case "FCOS": if (!args.TryProcessNoArgOp(OPCode.FCOS)) return args.res; break;
                        case "FSINCOS": if (!args.TryProcessNoArgOp(OPCode.FSINCOS)) return args.res; break;
                        case "FPTAN": if (!args.TryProcessNoArgOp(OPCode.FPTAN)) return args.res; break;
                        case "FPATAN": if (!args.TryProcessNoArgOp(OPCode.FPATAN)) return args.res; break;

                        case "FINCSTP": if (!args.TryProcessNoArgOp(OPCode.FINCDECSTP, true, 0)) return args.res; break;
                        case "FDECSTP": if (!args.TryProcessNoArgOp(OPCode.FINCDECSTP, true, 1)) return args.res; break;
                        
                        case "FFREE": if (!args.TryProcessFPURegisterOp(OPCode.FFREE)) return args.res; break;

                        case "FNSAVE": if (!args.TryProcessFSTLD_WORD(OPCode.FSTLD_WORD, 6, ~(UInt64)0)) return args.res; break; // sizecode = 0xff.ff to ensure user can't use explicit size (since it's not a standard size)
                        case "FSAVE": if (!args.TryAppendByte((byte)OPCode.FWAIT)) return args.res; goto case "FNSAVE";

                        case "FRSTOR": if (!args.TryProcessFSTLD_WORD(OPCode.FSTLD_WORD, 7, ~(UInt64)0)) return args.res; break; // sizecode = 0xff.ff to ensure user can't use explicit size (since it's not a standard size)

                        case "FNSTENV": if (!args.TryProcessFSTLD_WORD(OPCode.FSTLD_WORD, 8, ~(UInt64)0)) return args.res; break; // sizecode = 0xff.ff to ensure user can't use explicit size (since it's not a standard size)
                        case "FSTENV": if (!args.TryAppendByte((byte)OPCode.FWAIT)) return args.res; goto case "FNSTENV";

                        case "FLDENV": if (!args.TryProcessFSTLD_WORD(OPCode.FSTLD_WORD, 9, ~(UInt64)0)) return args.res; break; // sizecode = 0xff.ff to ensure user can't use explicit size (since it's not a standard size)

                        // vpu instructions

                        case "MOVQ": if (!args.TryProcessVPUMove(OPCode.VPU_MOV, 3, false, false, true)) return args.res; break;
                        case "MOVD": if (!args.TryProcessVPUMove(OPCode.VPU_MOV, 2, false, false, true)) return args.res; break;

                        // MOVSD (vec) requires disambiguation
                        case "MOVSS": if (!args.TryProcessVPUMove(OPCode.VPU_MOV, 2, false, false, true)) return args.res; break;

                        case "MOVDQA": if (!args.TryProcessVPUMove(OPCode.VPU_MOV, 3, false, true, false)) return args.res; break; // size codes for these 2 don't matter
                        case "MOVDQU": if (!args.TryProcessVPUMove(OPCode.VPU_MOV, 3, false, false, false)) return args.res; break;

                        case "MOVDQA64": if (!args.TryProcessVPUMove(OPCode.VPU_MOV, 3, true, true, false)) return args.res; break;
                        case "MOVDQA32": if (!args.TryProcessVPUMove(OPCode.VPU_MOV, 2, true, true, false)) return args.res; break;
                        case "MOVDQA16": if (!args.TryProcessVPUMove(OPCode.VPU_MOV, 1, true, true, false)) return args.res; break;
                        case "MOVDQA8": if (!args.TryProcessVPUMove(OPCode.VPU_MOV, 0, true, true, false)) return args.res; break;

                        case "MOVDQU64": if (!args.TryProcessVPUMove(OPCode.VPU_MOV, 3, true, false, false)) return args.res; break;
                        case "MOVDQU32": if (!args.TryProcessVPUMove(OPCode.VPU_MOV, 2, true, false, false)) return args.res; break;
                        case "MOVDQU16": if (!args.TryProcessVPUMove(OPCode.VPU_MOV, 1, true, false, false)) return args.res; break;
                        case "MOVDQU8": if (!args.TryProcessVPUMove(OPCode.VPU_MOV, 0, true, false, false)) return args.res; break;

                        case "MOVAPD": if (!args.TryProcessVPUMove(OPCode.VPU_MOV, 3, true, true, false)) return args.res; break;
                        case "MOVAPS": if (!args.TryProcessVPUMove(OPCode.VPU_MOV, 2, true, true, false)) return args.res; break;

                        case "MOVUPD": if (!args.TryProcessVPUMove(OPCode.VPU_MOV, 3, true, false, false)) return args.res; break;
                        case "MOVUPS": if (!args.TryProcessVPUMove(OPCode.VPU_MOV, 2, true, false, false)) return args.res; break;

                        case "ADDSD": if (!args.TryProcessVPUBinary(OPCode.VPU_FADD, 3, false, false, true)) return args.res; break;
                        case "SUBSD": if (!args.TryProcessVPUBinary(OPCode.VPU_FSUB, 3, false, false, true)) return args.res; break;
                        case "MULSD": if (!args.TryProcessVPUBinary(OPCode.VPU_FMUL, 3, false, false, true)) return args.res; break;
                        case "DIVSD": if (!args.TryProcessVPUBinary(OPCode.VPU_FDIV, 3, false, false, true)) return args.res; break;

                        case "ADDSS": if (!args.TryProcessVPUBinary(OPCode.VPU_FADD, 2, false, false, true)) return args.res; break;
                        case "SUBSS": if (!args.TryProcessVPUBinary(OPCode.VPU_FSUB, 2, false, false, true)) return args.res; break;
                        case "MULSS": if (!args.TryProcessVPUBinary(OPCode.VPU_FMUL, 2, false, false, true)) return args.res; break;
                        case "DIVSS": if (!args.TryProcessVPUBinary(OPCode.VPU_FDIV, 2, false, false, true)) return args.res; break;

                        case "ADDPD": if (!args.TryProcessVPUBinary(OPCode.VPU_FADD, 3, true, true, false)) return args.res; break;
                        case "SUBPD": if (!args.TryProcessVPUBinary(OPCode.VPU_FSUB, 3, true, true, false)) return args.res; break;
                        case "MULPD": if (!args.TryProcessVPUBinary(OPCode.VPU_FMUL, 3, true, true, false)) return args.res; break;
                        case "DIVPD": if (!args.TryProcessVPUBinary(OPCode.VPU_FDIV, 3, true, true, false)) return args.res; break;

                        case "ADDPS": if (!args.TryProcessVPUBinary(OPCode.VPU_FADD, 2, true, true, false)) return args.res; break;
                        case "SUBPS": if (!args.TryProcessVPUBinary(OPCode.VPU_FSUB, 2, true, true, false)) return args.res; break;
                        case "MULPS": if (!args.TryProcessVPUBinary(OPCode.VPU_FMUL, 2, true, true, false)) return args.res; break;
                        case "DIVPS": if (!args.TryProcessVPUBinary(OPCode.VPU_FDIV, 2, true, true, false)) return args.res; break;

                        case "PAND": if (!args.TryProcessVPUBinary(OPCode.VPU_AND, 3, false, true, false)) return args.res; break;
                        case "POR": if (!args.TryProcessVPUBinary(OPCode.VPU_OR, 3, false, true, false)) return args.res; break;
                        case "PXOR": if (!args.TryProcessVPUBinary(OPCode.VPU_XOR, 3, false, true, false)) return args.res; break;
                        case "PANDN": if (!args.TryProcessVPUBinary(OPCode.VPU_ANDN, 3, false, true, false)) return args.res; break;

                        case "PANDQ": case "ANDPD": if (!args.TryProcessVPUBinary(OPCode.VPU_AND, 3, true, true, false)) return args.res; break;
                        case "PORQ": case "ORPD": if (!args.TryProcessVPUBinary(OPCode.VPU_OR, 3, true, true, false)) return args.res; break;
                        case "PXORQ": case "XORPD": if (!args.TryProcessVPUBinary(OPCode.VPU_XOR, 3, true, true, false)) return args.res; break;
                        case "PANDNQ": case "ANDNPD": if (!args.TryProcessVPUBinary(OPCode.VPU_ANDN, 3, true, true, false)) return args.res; break;

                        case "PANDD": case "ANDPS": if (!args.TryProcessVPUBinary(OPCode.VPU_AND, 2, true, true, false)) return args.res; break;
                        case "PORD": case "ORPS": if (!args.TryProcessVPUBinary(OPCode.VPU_OR, 2, true, true, false)) return args.res; break;
                        case "PXORD": case "XORPS": if (!args.TryProcessVPUBinary(OPCode.VPU_XOR, 2, true, true, false)) return args.res; break;
                        case "PANDND": case "ANDNPS": if (!args.TryProcessVPUBinary(OPCode.VPU_ANDN, 2, true, true, false)) return args.res; break;

                        case "PADDQ": if (!args.TryProcessVPUBinary(OPCode.VPU_ADD, 3, true, true, false)) return args.res; break;
                        case "PADDD": if (!args.TryProcessVPUBinary(OPCode.VPU_ADD, 2, true, true, false)) return args.res; break;
                        case "PADDW": if (!args.TryProcessVPUBinary(OPCode.VPU_ADD, 1, true, true, false)) return args.res; break;
                        case "PADDB": if (!args.TryProcessVPUBinary(OPCode.VPU_ADD, 0, true, true, false)) return args.res; break;

                        case "PADDSW": if (!args.TryProcessVPUBinary(OPCode.VPU_ADDS, 1, true, true, false)) return args.res; break;
                        case "PADDSB": if (!args.TryProcessVPUBinary(OPCode.VPU_ADDS, 0, true, true, false)) return args.res; break;

                        case "PADDUSW": if (!args.TryProcessVPUBinary(OPCode.VPU_ADDUS, 1, true, true, false)) return args.res; break;
                        case "PADDUSB": if (!args.TryProcessVPUBinary(OPCode.VPU_ADDUS, 0, true, true, false)) return args.res; break;

                        case "PSUBQ": if (!args.TryProcessVPUBinary(OPCode.VPU_SUB, 3, true, true, false)) return args.res; break;
                        case "PSUBD": if (!args.TryProcessVPUBinary(OPCode.VPU_SUB, 2, true, true, false)) return args.res; break;
                        case "PSUBW": if (!args.TryProcessVPUBinary(OPCode.VPU_SUB, 1, true, true, false)) return args.res; break;
                        case "PSUBB": if (!args.TryProcessVPUBinary(OPCode.VPU_SUB, 0, true, true, false)) return args.res; break;

                        case "PSUBSW": if (!args.TryProcessVPUBinary(OPCode.VPU_SUBS, 1, true, true, false)) return args.res; break;
                        case "PSUBSB": if (!args.TryProcessVPUBinary(OPCode.VPU_SUBS, 0, true, true, false)) return args.res; break;

                        case "PSUBUSW": if (!args.TryProcessVPUBinary(OPCode.VPU_SUBUS, 1, true, true, false)) return args.res; break;
                        case "PSUBUSB": if (!args.TryProcessVPUBinary(OPCode.VPU_SUBUS, 0, true, true, false)) return args.res; break;

                        case "PMULLQ": if (!args.TryProcessVPUBinary(OPCode.VPU_MULL, 3, true, true, false)) return args.res; break;
                        case "PMULLD": if (!args.TryProcessVPUBinary(OPCode.VPU_MULL, 2, true, true, false)) return args.res; break;
                        case "PMULLW": if (!args.TryProcessVPUBinary(OPCode.VPU_MULL, 1, true, true, false)) return args.res; break;

                        case "MINSD": if (!args.TryProcessVPUBinary(OPCode.VPU_FMIN, 3, false, false, true)) return args.res; break;
                        case "MINSS": if (!args.TryProcessVPUBinary(OPCode.VPU_FMIN, 2, false, false, true)) return args.res; break;

                        case "MINPD": if (!args.TryProcessVPUBinary(OPCode.VPU_FMIN, 3, true, true, false)) return args.res; break;
                        case "MINPS": if (!args.TryProcessVPUBinary(OPCode.VPU_FMIN, 2, true, true, false)) return args.res; break;

                        case "MAXSD": if (!args.TryProcessVPUBinary(OPCode.VPU_FMAX, 3, false, false, true)) return args.res; break;
                        case "MAXSS": if (!args.TryProcessVPUBinary(OPCode.VPU_FMAX, 2, false, false, true)) return args.res; break;

                        case "MAXPD": if (!args.TryProcessVPUBinary(OPCode.VPU_FMAX, 3, true, true, false)) return args.res; break;
                        case "MAXPS": if (!args.TryProcessVPUBinary(OPCode.VPU_FMAX, 2, true, true, false)) return args.res; break;

                        case "PMINUQ": if (!args.TryProcessVPUBinary(OPCode.VPU_UMIN, 3, true, true, false)) return args.res; break;
                        case "PMINUD": if (!args.TryProcessVPUBinary(OPCode.VPU_UMIN, 2, true, true, false)) return args.res; break;
                        case "PMINUW": if (!args.TryProcessVPUBinary(OPCode.VPU_UMIN, 1, true, true, false)) return args.res; break;
                        case "PMINUB": if (!args.TryProcessVPUBinary(OPCode.VPU_UMIN, 0, true, true, false)) return args.res; break;

                        case "PMINSQ": if (!args.TryProcessVPUBinary(OPCode.VPU_SMIN, 3, true, true, false)) return args.res; break;
                        case "PMINSD": if (!args.TryProcessVPUBinary(OPCode.VPU_SMIN, 2, true, true, false)) return args.res; break;
                        case "PMINSW": if (!args.TryProcessVPUBinary(OPCode.VPU_SMIN, 1, true, true, false)) return args.res; break;
                        case "PMINSB": if (!args.TryProcessVPUBinary(OPCode.VPU_SMIN, 0, true, true, false)) return args.res; break;

                        case "PMAXUQ": if (!args.TryProcessVPUBinary(OPCode.VPU_UMAX, 3, true, true, false)) return args.res; break;
                        case "PMAXUD": if (!args.TryProcessVPUBinary(OPCode.VPU_UMAX, 2, true, true, false)) return args.res; break;
                        case "PMAXUW": if (!args.TryProcessVPUBinary(OPCode.VPU_UMAX, 1, true, true, false)) return args.res; break;
                        case "PMAXUB": if (!args.TryProcessVPUBinary(OPCode.VPU_UMAX, 0, true, true, false)) return args.res; break;

                        case "PMAXSQ": if (!args.TryProcessVPUBinary(OPCode.VPU_SMAX, 3, true, true, false)) return args.res; break;
                        case "PMAXSD": if (!args.TryProcessVPUBinary(OPCode.VPU_SMAX, 2, true, true, false)) return args.res; break;
                        case "PMAXSW": if (!args.TryProcessVPUBinary(OPCode.VPU_SMAX, 1, true, true, false)) return args.res; break;
                        case "PMAXSB": if (!args.TryProcessVPUBinary(OPCode.VPU_SMAX, 0, true, true, false)) return args.res; break;

                        case "ADDSUBPD": if (!args.TryProcessVPUBinary(OPCode.VPU_FADDSUB, 3, true, true, false)) return args.res; break;
                        case "ADDSUBPS": if (!args.TryProcessVPUBinary(OPCode.VPU_FADDSUB, 2, true, true, false)) return args.res; break;

                        case "PAVGW": if (!args.TryProcessVPUBinary(OPCode.VPU_AVG, 1, true, true, false)) return args.res; break;
                        case "PAVGB": if (!args.TryProcessVPUBinary(OPCode.VPU_AVG, 0, true, true, false)) return args.res; break;

                        case "CMPPD": if (!args.TryProcessVPU_FCMP(OPCode.VPU_FCMP, 3, true, true, false)) return args.res; break;
                        case "CMPPS": if (!args.TryProcessVPU_FCMP(OPCode.VPU_FCMP, 2, true, true, false)) return args.res; break;

                        // CMPSD (vec) requires disambiguation
                        case "CMPSS": if (!args.TryProcessVPU_FCMP(OPCode.VPU_FCMP, 2, false, false, true)) return args.res; break;

                        // packed double comparisons
                        case "CMPEQPD": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 3, true, true, false, true, 0)) return args.res; break;
                        case "CMPLTPD": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 3, true, true, false, true, 1)) return args.res; break;
                        case "CMPLEPD": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 3, true, true, false, true, 2)) return args.res; break;
                        case "CMPUNORDPD": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 3, true, true, false, true, 3)) return args.res; break;
                        case "CMPNEQPD": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 3, true, true, false, true, 4)) return args.res; break;
                        case "CMPNLTPD": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 3, true, true, false, true, 5)) return args.res; break;
                        case "CMPNLEPD": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 3, true, true, false, true, 6)) return args.res; break;
                        case "CMPORDPD": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 3, true, true, false, true, 7)) return args.res; break;
                        case "CMPEQ_UQPD": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 3, true, true, false, true, 8)) return args.res; break;
                        case "CMPNGEPD": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 3, true, true, false, true, 9)) return args.res; break;
                        case "CMPNGTPD": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 3, true, true, false, true, 10)) return args.res; break;
                        case "CMPFALSEPD": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 3, true, true, false, true, 11)) return args.res; break;
                        case "CMPNEQ_OQPD": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 3, true, true, false, true, 12)) return args.res; break;
                        case "CMPGEPD": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 3, true, true, false, true, 13)) return args.res; break;
                        case "CMPGTPD": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 3, true, true, false, true, 14)) return args.res; break;
                        case "CMPTRUEPD": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 3, true, true, false, true, 15)) return args.res; break;
                        case "CMPEQ_OSPD": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 3, true, true, false, true, 16)) return args.res; break;
                        case "CMPLT_OQPD": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 3, true, true, false, true, 17)) return args.res; break;
                        case "CMPLE_OQPD": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 3, true, true, false, true, 18)) return args.res; break;
                        case "CMPUNORD_SPD": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 3, true, true, false, true, 19)) return args.res; break;
                        case "CMPNEQ_USPD": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 3, true, true, false, true, 20)) return args.res; break;
                        case "CMPNLT_UQPD": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 3, true, true, false, true, 21)) return args.res; break;
                        case "CMPNLE_UQPD": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 3, true, true, false, true, 22)) return args.res; break;
                        case "CMPORD_SPD": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 3, true, true, false, true, 23)) return args.res; break;
                        case "CMPEQ_USPD": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 3, true, true, false, true, 24)) return args.res; break;
                        case "CMPNGE_UQPD": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 3, true, true, false, true, 25)) return args.res; break;
                        case "CMPNGT_UQPD": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 3, true, true, false, true, 26)) return args.res; break;
                        case "CMPFALSE_OSPD": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 3, true, true, false, true, 27)) return args.res; break;
                        case "CMPNEQ_OSPD": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 3, true, true, false, true, 28)) return args.res; break;
                        case "CMPGE_OQPD": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 3, true, true, false, true, 29)) return args.res; break;
                        case "CMPGT_OQPD": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 3, true, true, false, true, 30)) return args.res; break;
                        case "CMPTRUE_USPD": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 3, true, true, false, true, 31)) return args.res; break;

                        // packed single comparisons
                        case "CMPEQPS": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 2, true, true, false, true, 0)) return args.res; break;
                        case "CMPLTPS": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 2, true, true, false, true, 1)) return args.res; break;
                        case "CMPLEPS": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 2, true, true, false, true, 2)) return args.res; break;
                        case "CMPUNORDPS": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 2, true, true, false, true, 3)) return args.res; break;
                        case "CMPNEQPS": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 2, true, true, false, true, 4)) return args.res; break;
                        case "CMPNLTPS": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 2, true, true, false, true, 5)) return args.res; break;
                        case "CMPNLEPS": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 2, true, true, false, true, 6)) return args.res; break;
                        case "CMPORDPS": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 2, true, true, false, true, 7)) return args.res; break;
                        case "CMPEQ_UQPS": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 2, true, true, false, true, 8)) return args.res; break;
                        case "CMPNGEPS": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 2, true, true, false, true, 9)) return args.res; break;
                        case "CMPNGTPS": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 2, true, true, false, true, 10)) return args.res; break;
                        case "CMPFALSEPS": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 2, true, true, false, true, 11)) return args.res; break;
                        case "CMPNEQ_OQPS": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 2, true, true, false, true, 12)) return args.res; break;
                        case "CMPGEPS": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 2, true, true, false, true, 13)) return args.res; break;
                        case "CMPGTPS": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 2, true, true, false, true, 14)) return args.res; break;
                        case "CMPTRUEPS": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 2, true, true, false, true, 15)) return args.res; break;
                        case "CMPEQ_OSPS": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 2, true, true, false, true, 16)) return args.res; break;
                        case "CMPLT_OQPS": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 2, true, true, false, true, 17)) return args.res; break;
                        case "CMPLE_OQPS": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 2, true, true, false, true, 18)) return args.res; break;
                        case "CMPUNORD_SPS": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 2, true, true, false, true, 19)) return args.res; break;
                        case "CMPNEQ_USPS": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 2, true, true, false, true, 20)) return args.res; break;
                        case "CMPNLT_UQPS": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 2, true, true, false, true, 21)) return args.res; break;
                        case "CMPNLE_UQPS": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 2, true, true, false, true, 22)) return args.res; break;
                        case "CMPORD_SPS": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 2, true, true, false, true, 23)) return args.res; break;
                        case "CMPEQ_USPS": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 2, true, true, false, true, 24)) return args.res; break;
                        case "CMPNGE_UQPS": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 2, true, true, false, true, 25)) return args.res; break;
                        case "CMPNGT_UQPS": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 2, true, true, false, true, 26)) return args.res; break;
                        case "CMPFALSE_OSPS": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 2, true, true, false, true, 27)) return args.res; break;
                        case "CMPNEQ_OSPS": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 2, true, true, false, true, 28)) return args.res; break;
                        case "CMPGE_OQPS": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 2, true, true, false, true, 29)) return args.res; break;
                        case "CMPGT_OQPS": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 2, true, true, false, true, 30)) return args.res; break;
                        case "CMPTRUE_USPS": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 2, true, true, false, true, 31)) return args.res; break;

                        // scalar double comparisons
                        case "CMPEQSD": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 3, false, false, true, true, 0)) return args.res; break;
                        case "CMPLTSD": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 3, false, false, true, true, 1)) return args.res; break;
                        case "CMPLESD": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 3, false, false, true, true, 2)) return args.res; break;
                        case "CMPUNORDSD": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 3, false, false, true, true, 3)) return args.res; break;
                        case "CMPNEQSD": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 3, false, false, true, true, 4)) return args.res; break;
                        case "CMPNLTSD": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 3, false, false, true, true, 5)) return args.res; break;
                        case "CMPNLESD": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 3, false, false, true, true, 6)) return args.res; break;
                        case "CMPORDSD": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 3, false, false, true, true, 7)) return args.res; break;
                        case "CMPEQ_UQSD": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 3, false, false, true, true, 8)) return args.res; break;
                        case "CMPNGESD": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 3, false, false, true, true, 9)) return args.res; break;
                        case "CMPNGTSD": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 3, false, false, true, true, 10)) return args.res; break;
                        case "CMPFALSESD": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 3, false, false, true, true, 11)) return args.res; break;
                        case "CMPNEQ_OQSD": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 3, false, false, true, true, 12)) return args.res; break;
                        case "CMPGESD": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 3, false, false, true, true, 13)) return args.res; break;
                        case "CMPGTSD": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 3, false, false, true, true, 14)) return args.res; break;
                        case "CMPTRUESD": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 3, false, false, true, true, 15)) return args.res; break;
                        case "CMPEQ_OSSD": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 3, false, false, true, true, 16)) return args.res; break;
                        case "CMPLT_OQSD": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 3, false, false, true, true, 17)) return args.res; break;
                        case "CMPLE_OQSD": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 3, false, false, true, true, 18)) return args.res; break;
                        case "CMPUNORD_SSD": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 3, false, false, true, true, 19)) return args.res; break;
                        case "CMPNEQ_USSD": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 3, false, false, true, true, 20)) return args.res; break;
                        case "CMPNLT_UQSD": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 3, false, false, true, true, 21)) return args.res; break;
                        case "CMPNLE_UQSD": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 3, false, false, true, true, 22)) return args.res; break;
                        case "CMPORD_SSD": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 3, false, false, true, true, 23)) return args.res; break;
                        case "CMPEQ_USSD": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 3, false, false, true, true, 24)) return args.res; break;
                        case "CMPNGE_UQSD": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 3, false, false, true, true, 25)) return args.res; break;
                        case "CMPNGT_UQSD": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 3, false, false, true, true, 26)) return args.res; break;
                        case "CMPFALSE_OSSD": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 3, false, false, true, true, 27)) return args.res; break;
                        case "CMPNEQ_OSSD": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 3, false, false, true, true, 28)) return args.res; break;
                        case "CMPGE_OQSD": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 3, false, false, true, true, 29)) return args.res; break;
                        case "CMPGT_OQSD": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 3, false, false, true, true, 30)) return args.res; break;
                        case "CMPTRUE_USSD": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 3, false, false, true, true, 31)) return args.res; break;

                        // scalar single comparisons
                        case "CMPEQSS": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 2, false, false, true, true, 0)) return args.res; break;
                        case "CMPLTSS": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 2, false, false, true, true, 1)) return args.res; break;
                        case "CMPLESS": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 2, false, false, true, true, 2)) return args.res; break;
                        case "CMPUNORDSS": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 2, false, false, true, true, 3)) return args.res; break;
                        case "CMPNEQSS": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 2, false, false, true, true, 4)) return args.res; break;
                        case "CMPNLTSS": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 2, false, false, true, true, 5)) return args.res; break;
                        case "CMPNLESS": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 2, false, false, true, true, 6)) return args.res; break;
                        case "CMPORDSS": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 2, false, false, true, true, 7)) return args.res; break;
                        case "CMPEQ_UQSS": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 2, false, false, true, true, 8)) return args.res; break;
                        case "CMPNGESS": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 2, false, false, true, true, 9)) return args.res; break;
                        case "CMPNGTSS": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 2, false, false, true, true, 10)) return args.res; break;
                        case "CMPFALSESS": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 2, false, false, true, true, 11)) return args.res; break;
                        case "CMPNEQ_OQSS": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 2, false, false, true, true, 12)) return args.res; break;
                        case "CMPGESS": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 2, false, false, true, true, 13)) return args.res; break;
                        case "CMPGTSS": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 2, false, false, true, true, 14)) return args.res; break;
                        case "CMPTRUESS": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 2, false, false, true, true, 15)) return args.res; break;
                        case "CMPEQ_OSSS": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 2, false, false, true, true, 16)) return args.res; break;
                        case "CMPLT_OQSS": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 2, false, false, true, true, 17)) return args.res; break;
                        case "CMPLE_OQSS": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 2, false, false, true, true, 18)) return args.res; break;
                        case "CMPUNORD_SSS": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 2, false, false, true, true, 19)) return args.res; break;
                        case "CMPNEQ_USSS": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 2, false, false, true, true, 20)) return args.res; break;
                        case "CMPNLT_UQSS": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 2, false, false, true, true, 21)) return args.res; break;
                        case "CMPNLE_UQSS": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 2, false, false, true, true, 22)) return args.res; break;
                        case "CMPORD_SSS": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 2, false, false, true, true, 23)) return args.res; break;
                        case "CMPEQ_USSS": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 2, false, false, true, true, 24)) return args.res; break;
                        case "CMPNGE_UQSS": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 2, false, false, true, true, 25)) return args.res; break;
                        case "CMPNGT_UQSS": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 2, false, false, true, true, 26)) return args.res; break;
                        case "CMPFALSE_OSSS": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 2, false, false, true, true, 27)) return args.res; break;
                        case "CMPNEQ_OSSS": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 2, false, false, true, true, 28)) return args.res; break;
                        case "CMPGE_OQSS": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 2, false, false, true, true, 29)) return args.res; break;
                        case "CMPGT_OQSS": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 2, false, false, true, true, 30)) return args.res; break;
                        case "CMPTRUE_USSS": if (!args.TryProcessVPUBinary(OPCode.VPU_FCMP, 2, false, false, true, true, 31)) return args.res; break;

                        case "COMISD": if (!args.TryProcessVPUBinary_2arg(OPCode.VPU_FCOMI, 3, false, false, true)) return args.res; break;
                        case "COMISS": if (!args.TryProcessVPUBinary_2arg(OPCode.VPU_FCOMI, 2, false, false, true)) return args.res; break;

                        case "SQRTPD": if (!args.TryProcessVPUUnary(OPCode.VPU_FSQRT, 3, true, true, false)) return args.res; break;
                        case "SQRTPS": if (!args.TryProcessVPUUnary(OPCode.VPU_FSQRT, 2, true, true, false)) return args.res; break;

                        case "SQRTSD": if (!args.TryProcessVPUUnary(OPCode.VPU_FSQRT, 3, false, false, true)) return args.res; break;
                        case "SQRTSS": if (!args.TryProcessVPUUnary(OPCode.VPU_FSQRT, 2, false, false, true)) return args.res; break;

                        case "RSQRTPD": if (!args.TryProcessVPUUnary(OPCode.VPU_FRSQRT, 3, true, true, false)) return args.res; break;
                        case "RSQRTPS": if (!args.TryProcessVPUUnary(OPCode.VPU_FRSQRT, 2, true, true, false)) return args.res; break;

                        case "RSQRTSD": if (!args.TryProcessVPUUnary(OPCode.VPU_FRSQRT, 3, false, false, true)) return args.res; break;
                        case "RSQRTSS": if (!args.TryProcessVPUUnary(OPCode.VPU_FRSQRT, 2, false, false, true)) return args.res; break;

                        case "STMXCSR": if (!args.TryProcessFSTLD_WORD(OPCode.FSTLD_WORD, 4, 2)) return args.res; break;
                        case "LDMXCSR": if (!args.TryProcessFSTLD_WORD(OPCode.FSTLD_WORD, 5, 2)) return args.res; break;

                        case "CVTSD2SI": if (!args.TryProcessVPUCVT_scalar_f2i(OPCode.VPU_CVT, false, false)) return args.res; break;
                        case "CVTSS2SI": if (!args.TryProcessVPUCVT_scalar_f2i(OPCode.VPU_CVT, false, true)) return args.res; break;
                        case "CVTTSD2SI": if (!args.TryProcessVPUCVT_scalar_f2i(OPCode.VPU_CVT, true, false)) return args.res; break;
                        case "CVTTSS2SI": if (!args.TryProcessVPUCVT_scalar_f2i(OPCode.VPU_CVT, true, true)) return args.res; break;

                        case "CVTSI2SD": if (!args.TryProcessVPUCVT_scalar_i2f(OPCode.VPU_CVT, false)) return args.res; break;
                        case "CVTSI2SS": if (!args.TryProcessVPUCVT_scalar_i2f(OPCode.VPU_CVT, true)) return args.res; break;

                        case "CVTSD2SS": if (!args.TryProcessVPUCVT_scalar_f2f(OPCode.VPU_CVT, false)) return args.res; break;
                        case "CVTSS2SD": if (!args.TryProcessVPUCVT_scalar_f2f(OPCode.VPU_CVT, true)) return args.res; break;

                        case "CVTPD2DQ": if (!args.TryProcessVPUCVT_packed_f2i(OPCode.VPU_CVT, false, false)) return args.res; break;
                        case "CVTPS2DQ": if (!args.TryProcessVPUCVT_packed_f2i(OPCode.VPU_CVT, false, true)) return args.res; break;
                        case "CVTTPD2DQ": if (!args.TryProcessVPUCVT_packed_f2i(OPCode.VPU_CVT, true, false)) return args.res; break;
                        case "CVTTPS2DQ": if (!args.TryProcessVPUCVT_packed_f2i(OPCode.VPU_CVT, true, true)) return args.res; break;

                        case "CVTDQ2PD": if (!args.TryProcessVPUCVT_packed_i2f(OPCode.VPU_CVT, false)) return args.res; break;
                        case "CVTDQ2PS": if (!args.TryProcessVPUCVT_packed_i2f(OPCode.VPU_CVT, true)) return args.res; break;

                        case "CVTPD2PS": if (!args.TryProcessVPUCVT_packed_f2f(OPCode.VPU_CVT, false)) return args.res; break;
                        case "CVTPS2PD": if (!args.TryProcessVPUCVT_packed_f2f(OPCode.VPU_CVT, true)) return args.res; break;


                        // misc instructions

                        case "DEBUG_CPU": if (!args.TryProcessNoArgOp(OPCode.DEBUG, true, 0)) return args.res; break;
                        case "DEBUG_VPU": if (!args.TryProcessNoArgOp(OPCode.DEBUG, true, 1)) return args.res; break;
                        case "DEBUG_FULL": if (!args.TryProcessNoArgOp(OPCode.DEBUG, true, 2)) return args.res; break;

                        // disambiguation

                        case "MOVS":
                            // MOVS (string) has 2 memory operands
                            if (args.args.Length == 2 && args.args[0].EndsWith(']') && args.args[1].EndsWith(']'))
                            {
                                if (!args.TryProcessMOVS_string(OPCode.string_ops, false)) return args.res;
                            }
                            // otherwise is MOVS (mov)
                            else
                            {
                                if (!args.TryProcessBinaryOp(OPCode.MOVcc, true, 2)) return args.res;
                            }
                            break;
                        case "MOVSD":
                            // MOVSD (string) takes no operands
                            if (args.args.Length == 0)
                            {
                                if (!args.TryProcessNoArgOp(OPCode.string_ops, true, 2)) return args.res;
                            }
                            // otherwise is MOVSD (vec)
                            else
                            {
                                if (!args.TryProcessVPUMove(OPCode.VPU_MOV, 3, false, false, true)) return args.res;
                            }
                            break;
                        case "CMPSD":
                            // CMPSD (string) takes no operands
                            if (args.args.Length == 0)
                            {
                                if (!args.TryProcessNoArgOp(OPCode.string_ops, true, (2 << 2) | 2)) return args.res;
                            }
                            // otherwise is CMPSD(vec)
                            else
                            {
                                if (!args.TryProcessVPU_FCMP(OPCode.VPU_FCMP, 3, false, false, true)) return args.res;
                            }
                            break;

                        default: return new AssembleResult(AssembleError.UnknownOp, $"line {args.line}: Unknown operation \"{args.op}\"");
                    }
                }

                op_done:

                // advance to after the new line
                pos = end + 1;
            }

            // -- minimize symbols and holes -- //

            // link each symbol to internal symbols (minimizes file size)
            foreach (var entry in file.Symbols) entry.Value.Evaluate(file.Symbols, out a, out floating, ref err);
            // eliminate as many holes as possible
            if (!_ElimHoles(file.Symbols, file.TextHoles, file.Text, ref args.res)) return args.res;
            if (!_ElimHoles(file.Symbols, file.RodataHoles, file.Rodata, ref args.res)) return args.res;
            if (!_ElimHoles(file.Symbols, file.DataHoles, file.Data, ref args.res)) return args.res;

            // -- eliminate as many unnecessary symbols as we can -- //

            List<string> elim_symbols = new List<string>();   // symbol names to be eliminated
            List<string> rename_symbols = new List<string>(); // symbol names that we can rename to be shorter

            // for each symbol
            foreach (var entry in file.Symbols)
            {
                // if this symbol is non-global
                if (!file.GlobalSymbols.Contains(entry.Key))
                {
                    // if this symbol has already been evaluated
                    if (entry.Value.IsEvaluated)
                    {
                        // we can eliminate it (because it's already been linked internally and won't be needed externally)
                        elim_symbols.Add(entry.Key);
                    }
                    // otherwise we can rename it to something shorter (because it's still needed internally, but not needed externally)
                    else rename_symbols.Add(entry.Key);
                }
            }
            // remove all the symbols we can eliminate
            foreach (string elim in elim_symbols) file.Symbols.Remove(elim);

            // -- finalize -- //

            // verify integrity of file
            if (!args.VerifyIntegrity()) return args.res;

            // rename all the symbols we can shorten (done after verify to ensure there's no verify error messages with the renamed symbols)
            for (int i = 0; i < rename_symbols.Count; ++i) file.RenameSymbol(rename_symbols[i], $"^{i:x}");

            // validate result
            file.Clean = true;

            // return no error
            return new AssembleResult(AssembleError.None, string.Empty);
        }
        /// <summary>
        /// Links object files together into an executable. Throws <see cref="ArgumentException"/> if any of the object files are dirty.
        /// Object files may be rendered dirty after this process (regardless of success). Any files that are still clean may be reused.
        /// </summary>
        /// <param name="exe">the resulting executable</param>
        /// <param name="objs">the object files to link. should all be clean. the first item in this array is the _start file</param>
        /// <param name="entry_point">the raw starting file</param>
        /// <exception cref="ArgumentException"></exception>
        public static LinkResult Link(Executable exe, ObjectFile[] objs, string entry_point = "main")
        {
            // parsing locations for evaluation
            UInt64 _res;
            bool _floating;
            string _err = null;
            LinkResult res = default(LinkResult);

            // -- ensure args are good -- //

            // ensure entry point is legal
            if (!AssembleArgs.IsValidName(entry_point, ref _err)) return new LinkResult(LinkError.FormatError, $"Entry point \"{entry_point}\" is not a legal symbol name");

            // ensure we got at least 1 object file
            if (objs == null || objs.Length == 0) return new LinkResult(LinkError.EmptyResult, $"Got no object files");

            // make sure all object files are starting out clean
            foreach (ObjectFile obj in objs) if (!obj.Clean) throw new ArgumentException("Attempt to use dirty object file");

            // -- validate _start file -- //

            // _start file must declare an external named "_start"
            if (!objs[0].ExternalSymbols.Contains("_start")) return new LinkResult(LinkError.FormatError, "_start file must declare an external named \"_start\"");

            // rename "_start" symbol in _start file to whatever the entry point is (makes _start dirty)
            try { objs[0].Clean = false; objs[0].RenameSymbol("_start", entry_point); }
            catch (Exception ex) { return new LinkResult(LinkError.FormatError, ex.ToString()); }

            // -- define things -- //

            // create segments (we don't know how large the resulting file will be, so it needs to be expandable)
            List<byte> text = new List<byte>();
            List<byte> rodata = new List<byte>();
            List<byte> data = new List<byte>();
            UInt64 bsslen = 0;

            // segment alignments
            UInt64 textalign = 1;
            UInt64 rodataalign = 1;
            UInt64 dataalign = 1;
            UInt64 bssalign = 1;

            // a table for relating global symbols to their object file
            var global_to_obj = new Dictionary<string, ObjectFile>();

            // the queue of object files that need to be added to the executable
            var include_queue = new Queue<ObjectFile>();
            // a table for relating included object files to their beginning positions in the resulting binary (text, rodata, data, bss) tuples
            var included = new Dictionary<ObjectFile, Tuple<UInt64, UInt64, UInt64, UInt64>>();

            // -- populate things -- //

            // populate global_to_obj with ALL global symbols
            foreach (ObjectFile obj in objs)
            {
                foreach (string global in obj.GlobalSymbols)
                {
                    // make sure source actually defined this symbol (just in case of corrupted object file)
                    if (!obj.Symbols.TryGetValue(global, out Expr value)) return new LinkResult(LinkError.MissingSymbol, $"Global symbol \"{global}\" was not defined");
                    // make sure it wasn't already defined
                    if (global_to_obj.ContainsKey(global)) return new LinkResult(LinkError.SymbolRedefinition, $"Global symbol \"{global}\" was defined by multiple sources");

                    // add to the table
                    global_to_obj.Add(global, obj);
                }
            }

            // -- verify things -- //

            // make sure no one defined over reserved symbol names
            foreach (ObjectFile obj in objs)
            {
                // only the verify ignores are a problem (because we'll be defining those)
                foreach (string reserved in VerifyLegalExpressionIgnores)
                    if (obj.Symbols.ContainsKey(reserved)) return new LinkResult(LinkError.SymbolRedefinition, $"Object file defined symbol with name \"{reserved}\" (reserved)");
            }

            // start the merge process with the _start file
            include_queue.Enqueue(objs[0]);

            // -- merge things -- //

            // while there are still things in queue
            while (include_queue.Count > 0)
            {
                // get the object file we need to incorporate
                ObjectFile obj = include_queue.Dequeue();
                // all included files are dirty
                obj.Clean = false;

                // account for alignment requirements
                text.Align(obj.TextAlign);
                rodata.Align(obj.RodataAlign);
                data.Align(obj.DataAlign);
                bsslen = Align(bsslen, obj.BSSAlign);

                // update segment alignments
                textalign = Math.Max(textalign, obj.TextAlign);
                rodataalign = Math.Max(rodataalign, obj.RodataAlign);
                dataalign = Math.Max(dataalign, obj.DataAlign);
                bssalign = Math.Max(bssalign, obj.BSSAlign);

                // add it to the set of included files
                included.Add(obj, new Tuple<UInt64, UInt64, UInt64, UInt64>((UInt64)text.Count, (UInt64)rodata.Count, (UInt64)data.Count, bsslen));
                
                // offset holes to be relative to the start of their total segment (not relative to resulting file)
                foreach (HoleData hole in obj.TextHoles) hole.Address += (UInt32)text.Count;
                foreach (HoleData hole in obj.RodataHoles) hole.Address += (UInt32)rodata.Count;
                foreach (HoleData hole in obj.DataHoles) hole.Address += (UInt32)data.Count;

                // append segments
                text.AddRange(obj.Text);
                rodata.AddRange(obj.Rodata);
                data.AddRange(obj.Data);
                bsslen += obj.BssLen;

                // for each external symbol
                foreach (string external in obj.ExternalSymbols)
                {
                    // if this is a global symbol somewhere
                    if (global_to_obj.TryGetValue(external, out ObjectFile global_source))
                    {
                        // if the source isn't already included and it isn't already in queue to be included
                        if (!included.ContainsKey(global_source) && !include_queue.Contains(global_source))
                        {
                            // add it to the queue
                            include_queue.Enqueue(global_source);
                        }
                    }
                    // otherwise it wasn't defined
                    else return new LinkResult(LinkError.MissingSymbol, $"No global symbol found to match external symbol \"{external}\"");
                }
            }

            // account for segment alignments
            text.Pad(AlignOffset((UInt64)(text.Count), rodataalign));
            rodata.Pad(AlignOffset((UInt64)(text.Count + rodata.Count), dataalign));
            data.Pad(AlignOffset((UInt64)(text.Count + rodata.Count + data.Count), bssalign));
            bsslen += AlignOffset((UInt64)(text.Count + rodata.Count + data.Count + (int)bsslen), 2); // the whole executable is 16-bit aligned (for stack)

            // now that we're done merging we need to define segment offsets in the result
            foreach (var entry in included)
            {
                // alias the object file
                ObjectFile obj = entry.Key;

                // define the segment origins
                obj.Symbols.Add(SegOrigins[AsmSegment.TEXT], new Expr() { IntResult = 0 });
                obj.Symbols.Add(SegOrigins[AsmSegment.RODATA], new Expr() { IntResult = (UInt64)text.Count });
                obj.Symbols.Add(SegOrigins[AsmSegment.DATA], new Expr() { IntResult = (UInt64)text.Count + (UInt64)rodata.Count });
                obj.Symbols.Add(SegOrigins[AsmSegment.BSS], new Expr() { IntResult = (UInt64)text.Count + (UInt64)rodata.Count + (UInt64)data.Count });

                // and file-scope segment offsets
                obj.Symbols.Add(SegOffsets[AsmSegment.TEXT], new Expr() { IntResult = entry.Value.Item1 });
                obj.Symbols.Add(SegOffsets[AsmSegment.RODATA], new Expr() { IntResult = (UInt64)text.Count + entry.Value.Item2 });
                obj.Symbols.Add(SegOffsets[AsmSegment.DATA], new Expr() { IntResult = (UInt64)text.Count + (UInt64)rodata.Count + entry.Value.Item3 });
                obj.Symbols.Add(SegOffsets[AsmSegment.BSS], new Expr() { IntResult = (UInt64)text.Count + (UInt64)rodata.Count + (UInt64)data.Count + entry.Value.Item4 });

                // and everything else
                obj.Symbols.Add("__heap__", new Expr() { IntResult = (UInt64)text.Count + (UInt64)rodata.Count + (UInt64)data.Count + bsslen });

                // for each global symbol
                foreach (string global in obj.GlobalSymbols)
                {
                    // if it can't be evaluated internally, it's an error (i.e. cannot define a global in terms of another file's globals)
                    if (!obj.Symbols[global].Evaluate(obj.Symbols, out _res, out _floating, ref _err))
                        return new LinkResult(LinkError.MissingSymbol, $"Global symbol \"{global}\" could not be evaluated internally");
                }

                // for each external symbol
                foreach (string external in obj.ExternalSymbols)
                {
                    // add externals to local scope //

                    // if obj already has a symbol of the same name
                    if (obj.Symbols.ContainsKey(external)) return new LinkResult(LinkError.SymbolRedefinition, $"Object file defined external symbol \"{external}\"");
                    // otherwise define it as a local in obj
                    else obj.Symbols.Add(external, global_to_obj[external].Symbols[external]);
                }
            }

            // -- patch things -- //

            // for each object file
            foreach (var entry in included)
            {
                // alias object file
                ObjectFile obj = entry.Key;

                // patch all the holes
                if (!_FixAllHoles(obj.Symbols, obj.TextHoles, text, ref res)) return res;
                if (!_FixAllHoles(obj.Symbols, obj.RodataHoles, rodata, ref res)) return res;
                if (!_FixAllHoles(obj.Symbols, obj.DataHoles, data, ref res)) return res;
            }

			// -- finalize things -- //

			// construct the executable
			exe.Construct(text, rodata, data, bsslen);

            // linked successfully
            return new LinkResult(LinkError.None, string.Empty);
        }
    }
}
