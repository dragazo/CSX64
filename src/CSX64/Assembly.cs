﻿using System;
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
    internal enum PatchError
    {
        None, Unevaluated, Error
    }
    internal enum AsmSegment
    {
        INVALID = 0, TEXT = 1, RODATA = 2, DATA = 4, BSS = 8
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

    /// <summary>
    /// Represents an expression used to compute a value, with options for using a symbol table for lookup
    /// </summary>
    internal class Expr
    {
        internal enum OPs
        {
            None,

            // binary ops

            Mul, Div, Mod,
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
                        // try several integral radicies
                        if (Token.StartsWith("0x")) { if (Token.Substring(2).TryParseUInt64(out res, 16)) break; }
                        else if (Token.StartsWith("0b")) { if (Token.Substring(2).TryParseUInt64(out res, 2)) break; }
                        else if (Token[0] == '0' && Token.Length > 1) { if (Token.Substring(1).TryParseUInt64(out res, 8)) break; }
                        else { if (Token.TryParseUInt64(out res, 10)) break; }

                        // try floating-point
                        if (double.TryParse(Token, out double f)) { res = DoubleAsUInt64(f); floating = true; break; }

                        // if nothing worked, it's an ill-formed numeric literal
                        err = $"Ill-formed numeric literal encountered: \"{Token}\"";
                        return false;
                    }
                    // if it's a character
                    else if (Token[0] == '\'')
                    {
                        // make sure it's terminated
                        if (Token.Length != 3 || Token[2] != '\'') { err = $"Ill-formed character literal encountered: \"{Token}\""; return false; }

                        // extract the character
                        res = Token[1];
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
                case OPs.Div:
                    if (!Left.__Evaluate__(symbols, out L, out LF, ref err, visited)) ret = false;
                    if (!Right.__Evaluate__(symbols, out R, out RF, ref err, visited)) ret = false;
                    if (ret == false) return false;

                    if (LF || RF) { res = DoubleAsUInt64((LF ? AsDouble(L) : (Int64)L) / (RF ? AsDouble(R) : (Int64)R)); floating = true; }
                    else res = (UInt64)((Int64)L / (Int64)R);
                    break;
                case OPs.Mod:
                    if (!Left.__Evaluate__(symbols, out L, out LF, ref err, visited)) ret = false;
                    if (!Right.__Evaluate__(symbols, out R, out RF, ref err, visited)) ret = false;
                    if (ret == false) return false;

                    if (LF || RF) { res = DoubleAsUInt64((LF ? AsDouble(L) : (Int64)L) % (RF ? AsDouble(R) : (Int64)R)); floating = true; }
                    else res = (UInt64)((Int64)L % (Int64)R);
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

    public static class Assembly
    {
        private const char CommentChar = ';';
        private const char LabelDefChar = ':';

        private const string CurrentLineMacro = "$";
        private const string StartOfSegMacro = "$$";

        private const string EmissionMultPrefix = "#";
        private const UInt64 EmissionMaxMultiplier = 1000000;

        private static readonly Dictionary<Expr.OPs, int> Precedence = new Dictionary<Expr.OPs, int>()
        {
            { Expr.OPs.Mul, 5 },
            { Expr.OPs.Div, 5 },
            { Expr.OPs.Mod, 5 },

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
        private static readonly char[] UnaryOps = { '+', '-', '~', '!', '*', '/' };

        private static readonly string[] VerifyLegalExpressionIgnores =
        {
            "#TEXT", "#RODATA", "#DATA", "#BSS",
            "##TEXT", "##RODATA", "##DATA", "##BSS",
            "__heap__"
        };
        
        private static readonly string[] AddressPrefixes = // the explicit address expression size prefixes (must be in correct binary (size code) order)
        {
            "BYTEPTR", "WORDPTR", "DWORDPTR", "QWORDPTR"
        };

        private static readonly string[] AddressRegs = // the registers allowed in address expressions (must be in correct register id order)
        {
            "RAX", "RBX", "RCX", "RDX", "RSI", "RDI", "RBP", "RSP", "R8", "R9", "R10", "R11", "R12", "R13", "R14", "R15"
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
                // (label: label: ...) (op(:size) (arg, arg, ...))

                int pos = 0, end; // position in line parsing
                int quote;        // index of openning quote in args

                List<string> tokens = new List<string>();
                StringBuilder b = new StringBuilder();

                // parse label

                // skip leading white space
                for (; pos < rawline.Length && char.IsWhiteSpace(rawline[pos]); ++pos) ;
                // get a white space-delimited token
                for (end = pos; end < rawline.Length && !char.IsWhiteSpace(rawline[end]); ++end) ;

                // if it's a label
                if (pos != end && rawline[end - 1] == LabelDefChar)
                {
                    // set as label def
                    label_def = rawline.Substring(pos, end - pos - 1);

                    // reposition pos to next non-whitespace
                    for (pos = end; pos < rawline.Length && char.IsWhiteSpace(rawline[pos]); ++pos) ;
                }
                // otherwise null label
                else label_def = null;

                // parse op

                // if we're still inside the line
                if (pos < rawline.Length)
                {
                    // get a white space-delimited token
                    for (end = pos; end < rawline.Length && !char.IsWhiteSpace(rawline[end]); ++end) ;

                    // save this as op
                    op = rawline.Substring(pos, end - pos);

                    pos = end; // pass parsed section before next section
                }
                // otherwise there is no op
                else op = string.Empty;

                // parse the rest of the line as comma-separated tokens
                for (; pos < rawline.Length; ++pos)
                {
                    // skip leading white space
                    for (; pos < rawline.Length && char.IsWhiteSpace(rawline[pos]); ++pos) ;
                    // when pos reaches end of token, we're done parsing
                    if (pos >= rawline.Length) break;

                    b.Clear(); // clear the string builder

                    // find the next terminator (comma-separated)
                    for (quote = -1; pos < rawline.Length && (rawline[pos] != ',' || quote >= 0); ++pos)
                    {
                        if (rawline[pos] == '"' || rawline[pos] == '\'')
                            quote = quote < 0 ? pos : (rawline[pos] == rawline[quote] ? -1 : quote);

                        // omit white space unless in a quote
                        if (quote >= 0 || !char.IsWhiteSpace(rawline[pos])) b.Append(rawline[pos]);
                    }

                    // make sure we closed any quotations
                    if (quote >= 0) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Unmatched quotation encountered in argument list"); return false; }

                    // make sure arg isn't empty
                    if (b.Length == 0) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Empty operation argument encountered"); return false; }

                    // add this token
                    tokens.Add(b.ToString());
                }
                // output tokens to assemble args
                args = tokens.ToArray();

                // successfully parsed line
                return true;
            }

            public static bool IsValidName(string token, ref string err)
            {
                // can't be empty or over 255 chars long
                if (token.Length == 0 || token.Length > 255) { err = $"Symbol \"{token}\" is too long"; return false; }

                // first char is underscore or letter
                if (token[0] != '_' && !char.IsLetter(token[0])) { err = $"Symbol \"{token}\" contained an illegal character"; return false; }
                // all other chars may additionally be numbers or periods
                for (int i = 1; i < token.Length; ++i)
                    if (token[i] != '_' && token[i] != '.' && !char.IsLetterOrDigit(token[i])) { err = $"Symbol \"{token}\" contained an illegal character"; return false; }

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
                    if (last_nonlocal_label == null) { res = new AssembleResult(AssembleError.InvalidLabel, $"line {line}: Cannot define a local symbol before the first non-local symbol in the segment"); return false; }

                    // mutate the label
                    label = $"{last_nonlocal_label}{label}";
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
                    // text and data segments are writable
                    case AsmSegment.TEXT: file.Text.Append(size, val); return true;
                    case AsmSegment.RODATA: file.Rodata.Append(size, val); return true;
                    case AsmSegment.DATA: file.Data.Append(size, val); return true;

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
                    case PatchError.Error: res = new AssembleResult(AssembleError.ArgError, $"line {line}: Error encountered while patching expression\n-> {err}"); return false;

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
                if ((a & 0x77) != 0) { if (!TryAppendVal(1, b)) return false; }
                if ((a & 0x80) != 0) { if (!TryAppendExpr(8, hole)) { res = new AssembleResult(AssembleError.ArgError, $"line {line}: Failed to append address base\n-> {res.ErrorMsg}"); return false; } }

                return true;
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
                        case '/': op = Expr.OPs.Div; return true;
                        case '%': op = Expr.OPs.Mod; return true;

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
            public bool TryParseImm(string token, out Expr expr)
            {
                expr = null; // initially-nulled result

                Expr temp; // temporary for node creation

                int pos = 0, end; // position in token
                int depth;        // parenthesis depth
                int quote;        // the starting index of a quote (or -1 if not currently inside a quote)

                bool numeric; // flags for enabling exponent notation for floating-point
                bool exp;

                bool binPair = false;          // marker if tree contains complete binary pairs (i.e. N+1 values and N binary ops)
                int unpaired_conditionals = 0; // number of unpaired conditional ops

                Expr.OPs op = Expr.OPs.None; // extracted binary op (initialized so compiler doesn't complain)
                int oplen = 0;               // length of operator found (in characters)

                string err = null; // error location for hole evaluation

                Stack<char> unaryOps = new Stack<char>(8); // holds unary ops for processing
                Stack<Expr> stack = new Stack<Expr>();     // the stack used to manage operator precedence rules

                // top of stack shall be refered to as current

                stack.Push(null); // stack will always have a null at its base (simplifies code slightly)

                if (token.Length == 0) { res = new AssembleResult(AssembleError.InvalidLabel, $"line {line}: Empty expression encountered"); return false; }

                while (pos < token.Length)
                {
                    // -- read val(op) -- //

                    // consume unary ops
                    for (; pos < token.Length && UnaryOps.Contains(token[pos]); ++pos) unaryOps.Push(token[pos]);

                    depth = 0;  // initial depth of 0
                    quote = -1; // initially not in a quote

                    numeric = pos < token.Length && char.IsDigit(token[pos]); // flag if this is a numeric literal
                    exp = false; // no exponent yet

                    // find next binary op
                    for (end = pos; end < token.Length && (depth > 0 || quote >= 0 || (numeric && exp) || !TryGetOp(token, end, out op, out oplen)); ++end)
                    {
                        // if we're not in a quote
                        if (quote < 0)
                        {
                            // account for important characters
                            if (token[end] == '(') ++depth;
                            else if (token[end] == ')') --depth;
                            else if (numeric && token[end] == 'e' || token[end] == 'E') exp = true; // e or E begins exponent
                            else if (numeric && token[end] == '+' || token[end] == '-' || char.IsDigit(token[end])) exp = false; // + or - or a digit ends exponent safety net
                            else if (token[end] == '\'') quote = end; // single quote marks start of character literal

                            // can't ever have negative depth
                            if (depth < 0) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Mismatched parenthesis \"{token}\""); return false; }
                        }
                        // otherwise we're in a quote
                        else
                        {
                            // if we have a matching quote, break out of quote mode
                            if (token[end] == token[quote]) quote = -1;
                        }
                    }
                    // if depth isn't back to 0, there was a parens mismatch
                    if (depth != 0) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Mismatched parenthesis \"{token}\""); return false; }
                    // if depth isn't back to 0, there was a parens mismatch
                    if (quote >= 0) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Mismatched quotation in character literal \"{token}\""); return false; }
                    // if pos == end we'll have an empty token
                    if (pos == end) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Empty token encountered in expression \"{token}\""); return false; }

                    // -- process value -- //

                    // -- convert value to an expression tree -- //

                    // if sub-expression
                    if (token[pos] == '(')
                    {
                        // parse it into temp
                        if (!TryParseImm(token.Substring(pos + 1, end - pos - 2), out temp)) return false;
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

                            temp = new Expr() { OP = Expr.OPs.Add, Left = new Expr() { Token = $"#{current_seg}" }, Right = new Expr() { IntResult = line_pos_in_seg } };
                        }
                        // if it's the start of segment
                        else if (val == StartOfSegMacro)
                        {
                            // must be in a segment
                            if (current_seg == AsmSegment.INVALID) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Attempt to take an address outside of a segment"); return false; }

                            temp = new Expr() { Token = $"##{current_seg}" };
                        }
                        // otherwise it's a normal value/symbol
                        else
                        {
                            // create the hole for it
                            temp = new Expr() { Token = val };

                            // it either needs to be evaluatable or a valid label name
                            if (!temp.Evaluate(file.Symbols, out UInt64 _res, out bool floating, ref err) && !IsValidName(val, ref err))
                            { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Failed to resolve token as a valid imm or symbol name \"{val}\"\n-> {err}"); return false; }
                        }
                    }

                    // -- handle unary op by modifying subtree -- //

                    // handle the unary ops in terms of binary ops (stack provides right-to-left evaluation)
                    while (unaryOps.Count > 0)
                    {
                        char uop = unaryOps.Pop();
                        switch (uop)
                        {
                            case '+': break;
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

                    // flag as a valid binary pair
                    binPair = true;

                    // -- process op -- //

                    if (end < token.Length)
                    {
                        // ternary conditional has special rules
                        if (op == Expr.OPs.Pair)
                        {
                            // seek out nearest conditional without a pair
                            for (; stack.Peek() != null && (stack.Peek().OP != Expr.OPs.Condition || stack.Peek().Right.OP == Expr.OPs.Pair); stack.Pop()) ;

                            // if we didn't find anywhere to put it, this is an error
                            if (stack.Peek() == null) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Expression contained a ternary conditional pair without a corresponding condition \"{token}\""); return false; }
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
                    }

                    // pass last delimiter
                    pos = end + oplen;
                }

                // handle binary pair mismatch
                if (!binPair) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Expression contained a mismatched binary op: \"{token}\""); return false; }

                // make sure all conditionals were matched
                if (unpaired_conditionals != 0) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Expression contained {unpaired_conditionals} incomplete ternary {(unpaired_conditionals == 1 ? "conditional" : "conditionals")}"); return false; }

                // run ptrdiff logic on result
                expr = Ptrdiff(expr);

                return true;
            }
            public bool TryParseInstantImm(string token, out UInt64 val, out bool floating)
            {
                string err = null; // error location for evaluation

                if (!TryParseImm(token, out Expr hole)) { val = 0; floating = false; return false; }
                if (!hole.Evaluate(file.Symbols, out val, out floating, ref err)) { res = new AssembleResult(AssembleError.ArgError, $"line {line}: Failed to parse instant imm \"{token}\"\n-> {err}"); return false; }

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
                foreach (string seg_name in VerifyLegalExpressionIgnores)
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
            public bool TryParseInstantPrefixedImm(string token, string prefix, out UInt64 val, out bool floating)
            {
                val = 0;
                floating = false;

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
                    for (end = prefix.Length; end < token.Length && (char.IsLetterOrDigit(token[end]) || token[end] == '_'); ++end) ;
                }

                // make sure we consumed the entire string
                if (end != token.Length) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Compound expressions used as prefixed expressions must be parenthesized \"{token}\""); return false; }

                // prefix index must be instant imm
                if (!TryParseInstantImm(token.Substring(prefix.Length), out val, out floating)) { res = new AssembleResult(AssembleError.ArgError, $"line {line}: Failed to parse instant prefixed imm \"{token}\"\n-> {res.ErrorMsg}"); return false; }

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
                // otherwise is not a register
                else
                {
                    res = new AssembleResult(AssembleError.FormatError, $"line {line}: Failed to parse \"{token}\" as a cpu register");
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
                    res = new AssembleResult(AssembleError.FormatError, $"line {line}: Failed to parse \"{token}\" as an fpu register");
                    reg = 0;
                    return false;
                }
            }

            public bool TryParseMultcode(string token, out UInt64 val)
            {
                // mult code must be instant imm
                if (!TryParseInstantImm(token, out val, out bool floating)) { res = new AssembleResult(AssembleError.ArgError, $"line {line}: Failed to parse mult code \"{token}\"\n-> {res.ErrorMsg}"); return false; }

                // ensure not floating
                if (floating) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Attempt to use floating point value to specify size multiplier \"{token}\""); return false; }

                // convert to mult code
                switch (val)
                {
                    case 0: val = 0; return true;
                    case 1: val = 1; return true;
                    case 2: val = 2; return true;
                    case 4: val = 3; return true;
                    case 8: val = 4; return true;
                    case 16: val = 5; return true;
                    case 32: val = 6; return true;
                    case 64: val = 7; return true;

                    default: res = new AssembleResult(AssembleError.ArgError, $"line {line}: Invalid size multiplier: {val}"); return false;
                }
            }
            public bool TryParseAddressReg(string label, ref Expr hole, out UInt64 m, out bool neg)
            {
                m = 0; neg = false; // initialize out params

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

                    m += val; // add extracted mult to total mult
                    list.Clear(); // clear list for next pass
                }

                // -- final task: get mult code and negative flag -- //

                // if m is pretty big (TM), it's negative
                if (m > 64) { m = ~m + 1; neg = true; } else neg = false;
                // only other thing is transforming the multiplier into a mult code
                switch (m)
                {
                    case 0: m = 0; break;
                    case 1: m = 1; break;
                    case 2: m = 2; break;
                    case 4: m = 3; break;
                    case 8: m = 4; break;
                    case 16: m = 5; break;
                    case 32: m = 6; break;
                    case 64: m = 7; break;

                    default: res = new AssembleResult(AssembleError.ArgError, $"line {line}: Invalid register multiplier encountered ({(Int64)m})"); return false;
                }

                // register successfully parsed
                return true;
            }
            public bool TryParseAddress(string token, out UInt64 a, out UInt64 b, out Expr hole, out UInt64 sizecode, out bool explicit_size)
            {
                a = b = 0; hole = new Expr();
                sizecode = 0; explicit_size = false;

                // account for exlicit sizecode prefix
                string utoken = token.ToUpper();
                for (int i = 0; i < AddressPrefixes.Length; ++i)
                    if (utoken.StartsWith(AddressPrefixes[i]))
                    {
                        // pop the prefix off and take this size code
                        token = token.Substring(AddressPrefixes[i].Length);
                        explicit_size = true;
                        sizecode = (UInt64)i;
                        break;
                    }

                // must be of [*] format
                if (token.Length < 3 || token[0] != '[' || token[token.Length - 1] != ']') { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Invalid address format encountered \"{token}\""); return false; }

                UInt64 temp = 0; // parsing temporaries
                bool btemp = false;

                int reg_count = 0; // number of registers parsed
                UInt64 r1 = 0, m1 = 0, r2 = 0, m2 = 0; // final register info
                bool n1 = false, n2 = false;

                string err = string.Empty; // evaluation error

                // turn into an expression
                if (!TryParseImm(token.Substring(1, token.Length - 2), out hole)) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Failed to parse address expression\n-> {res.ErrorMsg}"); return false; }

                // look through each register that's legal in an address expression
                for (int i = 0; i < AddressRegs.Length; ++i)
                {
                    // get the register data
                    if (!TryParseAddressReg(AddressRegs[i], ref hole, out temp, out btemp)) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Failed to extract register data\n-> {res.ErrorMsg}"); return false; }

                    // if the multiplier was nonzero, the register is really being used
                    if (temp != 0)
                    {
                        // put it into an available r slot
                        if (reg_count == 0) { r1 = (UInt64)i; m1 = temp; n1 = btemp; }
                        else if (reg_count == 1) { r2 = (UInt64)i; m2 = temp; n2 = btemp; }
                        else { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Can't use more than 2 registers to specify an address"); return false; }

                        ++reg_count; // mark this slot as filled
                    }
                }

                // if it still contains any registers they weren't 64-bit
                foreach (var entry in CPURegisterInfo)
                    if (hole.Find(entry.Key, true) != null) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Attempt to use register {entry.Key} in an address expression. Only 64-bit registers are allowed."); return false; }

                // make sure only one register is negative
                if (n1 && n2) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Only one register may be negative in an address expression"); return false; }
                // if the negative register is r1, swap with r2
                if (n1)
                {
                    Swap(ref r1, ref r2);
                    Swap(ref m1, ref m2);
                    Swap(ref n1, ref n2);
                }

                // if we can evaluate the hole to zero, there is no hole (null it)
                if (hole.Evaluate(file.Symbols, out temp, out btemp, ref err) && temp == 0) hole = null;

                // -- apply final touches -- //

                // [1: literal][3: m1][1: -m2][3: m2]   [4: r1][4: r2]   ([64: imm])
                a = (hole != null ? 128 : 0ul) | (m1 << 4) | (n2 ? 8 : 0ul) | m2;
                b = (r1 << 4) | r2;

                // address successfully parsed
                return true;
            }

            /// <summary>
            /// Gets the smallest size code that will support the unsigned value
            /// </summary>
            /// <param name="val">the value to test</param>
            public UInt64 SmallestUnsignedSizeCode(UInt64 val)
            {
                // filter through till we get a size that will contain it
                if (val <= 0xfful) return 0;
                else if (val <= 0xfffful) return 1;
                else if (val <= 0xfffffffful) return 2;
                else return 3;
            }

            public bool VerifyLegalExpression(Expr expr)
            {
                // if it's a leaf, it must be something that is defined
                if (expr.IsLeaf)
                {
                    // if it's already been evaluated or we know about it somehow, we're good
                    if (expr.IsEvaluated || file.Symbols.ContainsKey(expr.Token) || VerifyLegalExpressionIgnores.Contains(expr.Token) || file.ExternalSymbols.Contains(expr.Token)) return true;
                    // otherwise we don't know what it is
                    else { res = new AssembleResult(AssembleError.UnknownSymbol, $"Unknown symbol: \"{expr.Token}\""); return false; }
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
                    if (!file.Symbols.ContainsKey(global)) { res = new AssembleResult(AssembleError.UnknownSymbol, $"Global symbol \"{global}\" was never defined"); return false; }

                // make sure all symbol expressions were valid
                foreach (var entry in file.Symbols)
                    if (!VerifyLegalExpression(entry.Value)) return false;

                // make sure all hole expressions were valid
                foreach (HoleData hole in file.TextHoles)
                    if (!VerifyLegalExpression(hole.Expr)) return false;
                foreach (HoleData hole in file.DataHoles)
                    if (!VerifyLegalExpression(hole.Expr)) return false;

                // the hood is good
                return true;
            }

            // -- misc -- //

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

                    // can't name same as a register (could be problematic) (not in IsValidName() because then address parser would fail if address contained a register)
                    if (CPURegisterInfo.ContainsKey(label_def.ToUpper())) { res = new AssembleResult(AssembleError.InvalidLabel, $"Symbol \"{label_def}\" is reserved"); return false; }

                    // ensure we don't redefine a symbol
                    if (file.Symbols.ContainsKey(label_def)) { res = new AssembleResult(AssembleError.SymbolRedefinition, $"line {line}: Symbol \"{label_def}\" was already defined"); return false; }
                    // ensure we don't define an external
                    if (file.ExternalSymbols.Contains(label_def)) { res = new AssembleResult(AssembleError.SymbolRedefinition, $"line {line}: Cannot define external symbol \"{label_def}\" internally"); return false; }

                    // must be in a segment unless op is EQU
                    if (current_seg == AsmSegment.INVALID && op.ToUpper() != "EQU")
                    { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Attempt to take an address outside of a segment"); return false; }

                    // add the symbol as an address (uses illegal symbol #base, which will be defined at link time)
                    file.Symbols.Add(label_def, new Expr() { OP = Expr.OPs.Add, Left = new Expr() { Token = $"#{current_seg}" }, Right = new Expr() { IntResult = line_pos_in_seg } });
                }

                return true;
            }

            public bool TryProcessGlobal()
            {
                if (args.Length == 0) { res = new AssembleResult(AssembleError.ArgCount, $"line {line}: GLOBAL expected at least one symbol to export"); return false; }

                string err = null;
                foreach (string symbol in args)
                {
                    // special error message for using global on local labels
                    if (symbol[0] == '.') { res = new AssembleResult(AssembleError.ArgError, $"line {line}: Cannot export local symbols"); return false; }
                    // test name for legality
                    if (!IsValidName(symbol, ref err)) { res = new AssembleResult(AssembleError.InvalidLabel, $"line {line}: {err}"); return false; }

                    // don't add to global list twice
                    if (file.GlobalSymbols.Contains(symbol)) { res = new AssembleResult(AssembleError.SymbolRedefinition, $"line {line}: Attempt to global symbol \"{symbol}\" multiple times"); return false; }
                    // ensure we don't global an external
                    if (file.ExternalSymbols.Contains(symbol)) { res = new AssembleResult(AssembleError.SymbolRedefinition, $"line {line}: Cannot define external symbol \"{symbol}\" as global"); return false; }

                    // add it to the globals list
                    file.GlobalSymbols.Add(symbol);
                }

                return true;
            }
            public bool TryProcessExtern()
            {
                if (args.Length == 0) { res = new AssembleResult(AssembleError.ArgCount, $"line {line}: EXTERN expected at least one symbol to import"); return false; }

                string err = null;
                foreach (string symbol in args)
                {
                    // special error message for using extern on local labels
                    if (symbol[0] == '.') { res = new AssembleResult(AssembleError.ArgError, $"line {line}: Cannot import local symbols"); return false; }
                    // test name for legality
                    if (!IsValidName(symbol, ref err)) { res = new AssembleResult(AssembleError.InvalidLabel, $"line {line}: {err}"); return false; }

                    // ensure we don't extern a symbol that already exists
                    if (file.Symbols.ContainsKey(label_def)) { res = new AssembleResult(AssembleError.SymbolRedefinition, $"line {line}: Cannot define symbol \"{label_def}\" (defined internally) as external"); return false; }

                    // don't add to external list twice
                    if (file.ExternalSymbols.Contains(symbol)) { res = new AssembleResult(AssembleError.SymbolRedefinition, $"line {line}: Attempt to import symbol \"{symbol}\" multiple times"); return false; }
                    // ensure we don't extern a global
                    if (file.GlobalSymbols.Contains(symbol)) { res = new AssembleResult(AssembleError.SymbolRedefinition, $"line {line}: Cannot define global symbol \"{symbol}\" as external"); return false; }

                    // add it to the external list
                    file.ExternalSymbols.Add(symbol);
                }

                return true;
            }

            public bool TryProcessDeclare(UInt64 size)
            {
                if (args.Length == 0) { res = new AssembleResult(AssembleError.ArgCount, $"line {line}: Emission expected at least one value"); return false; }

                Expr hole = new Expr() { IntResult = 0 }; // initially integral zero (shorthand for empty buffer)
                UInt64 mult;
                bool floating;

                for (int i = 0; i < args.Length; ++i)
                {
                    if (args[i].Length == 0) { res = new AssembleResult(AssembleError.ArgError, $"line {line}: Emission encountered empty argument"); return false; }

                    // if a multiplier
                    if (args[i].StartsWith(EmissionMultPrefix))
                    {
                        // cannot be used immediately following another multiplier
                        if (i > 0 && args[i - 1].StartsWith(EmissionMultPrefix)) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Emission multiplier cannot immediately follow an emission multiplier"); return false; }
                        // cannot be used immediately following a string
                        if (i > 0 && args[i - 1][0] == '"') { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Emission multiplier cannot immediately follow a string argument"); return false; }

                        // get the prefixed multiplier
                        if (!TryParseInstantPrefixedImm(args[i], EmissionMultPrefix, out mult, out floating)) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Failed to parse \"{args[i]}\" as an emission multiplier\n-> {res.ErrorMsg}"); return false; }

                        // ensure the multiplier we got was valid
                        if (floating) { res = new AssembleResult(AssembleError.ArgError, $"line {line}: Emission multiplier cannot be floating point"); return false; }
                        if (mult > EmissionMaxMultiplier) { res = new AssembleResult(AssembleError.ArgError, $"line {line}: Emission multiplier cannot exceed {EmissionMaxMultiplier}. Got {mult}"); return false; }
                        if (mult == 0) { res = new AssembleResult(AssembleError.ArgError, $"line {line}: Emission multiplier cannot be zero"); return false; }

                        // already wrote 1 copy, now write the others
                        for (UInt64 j = 1; j < mult; ++j)
                            if (!TryAppendExpr(size, hole)) { res = new AssembleResult(AssembleError.ArgError, $"line {line}: Failed to append value\n-> {res.ErrorMsg}"); return false; }
                    }
                    // if a string
                    else if (args[i][0] == '"')
                    {
                        // make sure it's properly closed
                        if (args[i][0] != args[i][args[i].Length - 1]) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: String literal must be enclosed in single or double quotes"); return false; }

                        // dump the contents into memory
                        for (int j = 1; j < args[i].Length - 1; ++j)
                        {
                            // make sure there's no string splicing
                            if (args[i][j] == args[i][0]) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: String emission prematurely reached a terminating quote"); return false; }

                            if (!TryAppendVal(size, args[i][j])) return false;
                        }
                    }
                    // otherwise is a value
                    else
                    {
                        // get the value
                        if (!TryParseImm(args[i], out hole)) return false;

                        // make one of them
                        if (!TryAppendExpr(size, hole)) { res = new AssembleResult(AssembleError.ArgError, $"line {line}: Failed to append value\n-> {res.ErrorMsg}"); return false; }
                    }
                }

                return true;
            }
            public bool TryProcessReserve(UInt64 size)
            {
                if (args.Length != 1) { res = new AssembleResult(AssembleError.ArgCount, $"line {line}: RES expected one arg"); return false; }

                // parse the number to reserve
                if (!TryParseInstantImm(args[0], out UInt64 count, out bool floating)) return false;

                // make sure it's not floating
                if (floating) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: RES value cannot be floating-point"); return false; }

                // reserve the space
                if (!TryReserve(count * size)) return false;

                return true;
            }

            public bool TryProcessEQU()
            {
                if (args.Length != 1) { res = new AssembleResult(AssembleError.ArgCount, $"line {line}: EQU expected 1 arg"); return false; }

                // get the expression
                if (!TryParseImm(args[0], out Expr expr)) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: EQU expected an expression\n-> {res.ErrorMsg}"); return false; }

                // make sure we have a label
                if (label_def == null) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: EQU requires a label to attach the expression to"); return false; }

                // redefine this line's label to expr
                file.Symbols[label_def] = expr;

                return true;
            }

            public bool TryProcessSegment()
            {
                if (args.Length != 1) { res = new AssembleResult(AssembleError.ArgCount, $"line {line}: SEGMENT directive expected 1 arg"); return false; }

                // get the segment we're going to
                switch (args[0].ToUpper())
                {
                    case ".TEXT": current_seg = AsmSegment.TEXT; break;
                    case ".RODATA": current_seg = AsmSegment.RODATA; break;
                    case ".DATA": current_seg = AsmSegment.DATA; break;
                    case ".BSS": current_seg = AsmSegment.BSS; break;

                    default: res = new AssembleResult(AssembleError.ArgError, $"line {line}: Unknown segment \"{args[0]}\""); return false;
                }

                // if this segment has already been done, fail
                if ((done_segs & current_seg) != 0) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Attempt to redeclare the {current_seg} segment"); return false; }
                // add to list of completed segments
                done_segs |= current_seg;

                // we don't want to have cross-segment local symbols
                last_nonlocal_label = null;

                return true;
            }

            // -- x86 op formats -- //
            
            public bool TryProcessTernaryOp(OPCode op, bool has_ext_op = false, UInt64 ext_op = 0, UInt64 sizemask = 15)
            {
                if (args.Length != 3) { res = new AssembleResult(AssembleError.ArgCount, $"line {line}: {op} expected 3 args"); return false; }

                // write op code
                if (!TryAppendVal(1, (UInt64)op)) return false;
                if (has_ext_op) { if (!TryAppendVal(1, ext_op)) return false; }

                if (!TryParseCPURegister(args[0], out UInt64 dest, out UInt64 a_sizecode, out bool dest_high)) { res = new AssembleResult(AssembleError.ArgError, $"line {line}: {op} expected a register as the first argument"); return false; }
                if (!TryParseImm(args[2], out Expr imm)) { res = new AssembleResult(AssembleError.ArgError, $"line {line}: {op} expected an imm as the third argument"); return false; }

                if ((Size(a_sizecode) & sizemask) == 0) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: {op} does not support the specified size code"); return false; }

                UInt64 b_sizecode; // only used for ensuring no size conflicts (machine code only uses a_sizecode)

                // reg
                if (TryParseCPURegister(args[1], out UInt64 reg, out b_sizecode, out bool reg_high))
                {
                    if (a_sizecode != b_sizecode) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Argument size missmatch"); return false; }

                    if (!TryAppendVal(1, (dest << 4) | (a_sizecode << 2) | (dest_high ? 2 : 0ul) | 0)) return false;
                    if (!TryAppendExpr(Size(a_sizecode), imm)) return false;
                    if (!TryAppendVal(1, (reg_high ? 128 : 0ul) | reg)) return false;
                }
                // mem
                else if (TryParseAddress(args[1], out UInt64 a, out UInt64 b, out Expr ptr_base, out b_sizecode, out bool explicit_size))
                {
                    if (explicit_size && a_sizecode != b_sizecode) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Argument size missmatch"); return false; }

                    if (!TryAppendVal(1, (dest << 4) | (a_sizecode << 2) | (dest_high ? 2 : 0ul) | 1)) return false;
                    if (!TryAppendExpr(Size(a_sizecode), imm)) return false;
                    if (!TryAppendAddress(a, b, ptr_base)) return false;
                }
                // imm
                else { res = new AssembleResult(AssembleError.ArgError, $"line {line}: {op} requires second arg be a register or memory value"); return false; }

                return true;
            }
            public bool TryProcessBinaryOp(OPCode op, bool has_ext_op = false, UInt64 ext_op = 0, UInt64 sizemask = 15, int _b_sizecode = -1, bool allow_b_mem = true)
            {
                if (args.Length != 2) { res = new AssembleResult(AssembleError.ArgCount, $"line {line}: {op} expected 2 args"); return false; }

                // write op code
                if (!TryAppendVal(1, (UInt64)op)) return false;
                if (has_ext_op) { if (!TryAppendVal(1, ext_op)) return false; }

                UInt64 a_sizecode, b_sizecode;

                // reg, *
                if (TryParseCPURegister(args[0], out UInt64 dest, out a_sizecode, out bool dest_high))
                {
                    if ((Size(a_sizecode) & sizemask) == 0) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: {op} does not support the specified size code"); return false; }
                    
                    // reg, reg
                    if (TryParseCPURegister(args[1], out UInt64 src, out b_sizecode, out bool src_high))
                    {
                        if (a_sizecode != b_sizecode) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Argument size missmatch"); return false; }

                        if (!TryAppendVal(1, (dest << 4) | (a_sizecode << 2) | (dest_high ? 2 : 0ul) | (src_high ? 1 : 0ul))) return false;
                        if (!TryAppendVal(1, src)) return false;
                    }
                    // reg, mem
                    else if (TryParseAddress(args[1], out UInt64 a, out UInt64 b, out Expr ptr_base, out b_sizecode, out bool explicit_size))
                    {
                        if (explicit_size && a_sizecode != b_sizecode) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Argument size missmatch"); return false; }

                        if (!allow_b_mem) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: {op} does not allow the second argument to be a memory value"); return false; }

                        if (!TryAppendVal(1, (dest << 4) | (a_sizecode << 2) | (dest_high ? 2 : 0ul))) return false;
                        if (!TryAppendVal(1, (2 << 4))) return false;
                        if (!TryAppendAddress(a, b, ptr_base)) { res = new AssembleResult(AssembleError.ArgError, $"line {line}: Failed to append value\n-> {res.ErrorMsg}"); return false; }
                    }
                    // reg, imm
                    else
                    {
                        if (!TryParseImm(args[1], out Expr imm)) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Failed to parse \"{args[1]}\" as a register, memory value, or imm\n-> {res.ErrorMsg}"); return false; }

                        b_sizecode = _b_sizecode == -1 ? a_sizecode : (UInt64)_b_sizecode;

                        if (!TryAppendVal(1, (dest << 4) | (a_sizecode << 2) | (dest_high ? 2 : 0ul))) return false;
                        if (!TryAppendVal(1, (1 << 4))) return false;
                        if (!TryAppendExpr(Size(b_sizecode), imm)) { res = new AssembleResult(AssembleError.ArgError, $"line {line}: Failed to append value\n-> {res.ErrorMsg}"); return false; }
                    }
                }
                // mem, *
                else if (TryParseAddress(args[0], out UInt64 a, out UInt64 b, out Expr ptr_base, out a_sizecode, out bool explicit_size))
                {
                    // mem, reg
                    if (TryParseCPURegister(args[1], out UInt64 src, out b_sizecode, out bool src_high))
                    {
                        if ((Size(b_sizecode) & sizemask) == 0) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: {op} does not support the specified data size"); return false; }

                        if (explicit_size && a_sizecode != b_sizecode) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Argument size missmatch"); return false; }

                        if (!TryAppendVal(1, (b_sizecode << 2) | (src_high ? 1 : 0ul))) return false; // uses b_sizecode because address may not define it
                        if (!TryAppendVal(1, (3 << 4) | src)) return false;
                        if (!TryAppendAddress(a, b, ptr_base)) { res = new AssembleResult(AssembleError.ArgError, $"line {line}: Failed to append value\n-> {res.ErrorMsg}"); return false; };
                    }
                    // mem, mem
                    else if (TryParseAddress(args[1], out UInt64 _a, out UInt64 _b, out Expr _ptr_base, out UInt64 _sc, out bool _exp_sz))
                        { res = new AssembleResult(AssembleError.FormatError, $"line {line}: {op} does not support memory-to-memory"); return false; }
                    // mem, imm
                    else
                    {
                        if (!explicit_size) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Data size could not be deduced"); return false; }

                        if ((Size(a_sizecode) & sizemask) == 0) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: {op} does not support the specified data size"); return false; }
                        
                        if (!TryParseImm(args[1], out Expr imm)) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Failed to parse \"{args[1]}\" as a register, memory value, or imm\n-> {res.ErrorMsg}"); return false; }

                        b_sizecode = _b_sizecode == -1 ? a_sizecode : (UInt64)_b_sizecode;

                        if (!TryAppendVal(1, a_sizecode << 2)) return false;
                        if (!TryAppendVal(1, 4 << 4)) return false;
                        if (!TryAppendAddress(a, b, ptr_base)) { res = new AssembleResult(AssembleError.ArgError, $"line {line}: Failed to append value\n-> {res.ErrorMsg}"); return false; }
                        if (!TryAppendExpr(Size(b_sizecode), imm)) { res = new AssembleResult(AssembleError.ArgError, $"line {line}: Failed to append value\n-> {res.ErrorMsg}"); return false; }
                    }
                }
                // imm, *
                else { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Destination must be register or memory"); return false; }

                return true;
            }
            public bool TryProcessUnaryOp(OPCode op, bool has_ext_op = false, UInt64 ext_op = 0, UInt64 sizemask = 15)
            {
                if (args.Length != 1) { res = new AssembleResult(AssembleError.ArgCount, $"line {line}: {op} expected 1 arg"); return false; }

                // write op code
                if (!TryAppendVal(1, (UInt64)op)) return false;
                if (has_ext_op) { if (!TryAppendVal(1, ext_op)) return false; }

                UInt64 a_sizecode;

                // reg
                if (TryParseCPURegister(args[0], out UInt64 reg, out a_sizecode, out bool reg_high))
                {
                    if ((Size(a_sizecode) & sizemask) == 0) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: {op} does not support the specified size code"); return false; }
                    
                    if (!TryAppendVal(1, (reg << 4) | (a_sizecode << 2) | (reg_high ? 2 : 0ul))) return false;
                }
                // mem
                else if (TryParseAddress(args[0], out UInt64 a, out UInt64 b, out Expr ptr_base, out a_sizecode, out bool explicit_size))
                {
                    if (!explicit_size) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Data size could not be deduced"); return false; }

                    if ((Size(a_sizecode) & sizemask) == 0) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: {op} does not support the specified size code"); return false; }

                    if (!TryAppendVal(1, (a_sizecode << 2) | 1)) return false;
                    if (!TryAppendAddress(a, b, ptr_base)) { res = new AssembleResult(AssembleError.ArgError, $"line {line}: Failed to append value\n-> {res.ErrorMsg}"); return false; }
                }
                // imm
                else { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Destination must be register or memory"); return false; }

                return true;
            }
            public bool TryProcessIMMRM(OPCode op, bool has_ext_op = false, UInt64 ext_op = 0, UInt64 sizemask = 15, UInt64 imm_sizecode = 3)
            {
                if (args.Length != 1) { res = new AssembleResult(AssembleError.ArgCount, $"line {line}: {op} expected 1 arg"); return false; }

                // write op code
                if (!TryAppendVal(1, (UInt64)op)) return false;
                if (has_ext_op) { if (!TryAppendVal(1, ext_op)) return false; }

                UInt64 a_sizecode;

                // reg
                if (TryParseCPURegister(args[0], out UInt64 reg, out a_sizecode, out bool reg_high))
                {
                    if ((Size(a_sizecode) & sizemask) == 0) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: {op} does not support the specified size code"); return false; }

                    if (!TryAppendVal(1, (reg << 4) | (a_sizecode << 2) | (reg_high ? 1 : 0ul))) return false;
                }
                // mem
                else if (TryParseAddress(args[0], out UInt64 a, out UInt64 b, out Expr ptr_base, out a_sizecode, out bool explicit_size))
                {
                    if (!explicit_size) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Data size could not be deduced"); return false; }

                    if ((Size(a_sizecode) & sizemask) == 0) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: {op} does not support the specified size code"); return false; }

                    if (!TryAppendVal(1, (a_sizecode << 2) | 3)) return false;
                    if (!TryAppendAddress(a, b, ptr_base)) { res = new AssembleResult(AssembleError.ArgError, $"line {line}: Failed to append value\n-> {res.ErrorMsg}"); return false; }
                }
                // imm
                else
                {
                    if (!TryParseImm(args[0], out Expr imm)) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Failed to parse \"{args[0]}\" as a register, memory value, or imm\n-> {res.ErrorMsg}"); return false; }

                    if (!TryAppendVal(1, (imm_sizecode << 2) | 2)) return false;
                    if (!TryAppendExpr(Size(imm_sizecode), imm)) { res = new AssembleResult(AssembleError.ArgError, $"line {line}: Failed to append value\n-> {res.ErrorMsg}"); return false; }
                }

                return true;
            }

            public bool TryProcessNoArgOp(OPCode op, bool has_ext_op = false, UInt64 ext_op = 0)
            {
                if (args.Length != 0) { res = new AssembleResult(AssembleError.ArgCount, $"line {line}: {op} expected 0 args"); return false; }

                // write op code
                if (!TryAppendVal(1, (UInt64)op)) return false;
                if (has_ext_op) { if (!TryAppendVal(1, ext_op)) return false; }

                return true;
            }

            public bool TryProcessXCHG(OPCode op)
            {
                if (args.Length != 2) { res = new AssembleResult(AssembleError.ArgCount, $"line {line}: {op} expected 2 args"); return false; }

                if (!TryAppendVal(1, (UInt64)op)) return false;

                UInt64 a_sizecode, b_sizecode;

                // reg, *
                if (TryParseCPURegister(args[0], out UInt64 reg, out a_sizecode, out bool reg_high))
                {
                    // reg, reg
                    if (TryParseCPURegister(args[1], out UInt64 src, out b_sizecode, out bool src_high))
                    {
                        if (a_sizecode != b_sizecode) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Argument size missmatch"); return false; }

                        if (!TryAppendVal(1, (reg << 4) | (a_sizecode << 2) | (reg_high ? 2 : 0ul) | 0)) return false;
                        if (!TryAppendVal(1, (src_high ? 128 : 0ul) | src)) return false;
                    }
                    // reg, mem
                    else if (TryParseAddress(args[1], out UInt64 a, out UInt64 b, out Expr ptr_base, out b_sizecode, out bool explicit_size))
                    {
                        if (explicit_size && a_sizecode != b_sizecode) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Argument size missmatch"); return false; }

                        if (!TryAppendVal(1, (reg << 4) | (a_sizecode << 2) | (reg_high ? 2 : 0ul) | 1)) return false;
                        if (!TryAppendAddress(a, b, ptr_base)) return false;
                    }
                    // reg, imm
                    else { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Cannot use {op} with an imm"); return false; }
                }
                // mem, *
                else if (TryParseAddress(args[0], out UInt64 a, out UInt64 b, out Expr ptr_base, out a_sizecode, out bool explicit_size))
                {
                    // mem, reg
                    if (TryParseCPURegister(args[1], out reg, out b_sizecode, out reg_high))
                    {
                        if (explicit_size && a_sizecode != b_sizecode) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Argument size missmatch"); return false; }

                        if (!TryAppendVal(1, (reg << 4) | (b_sizecode << 2) | (reg_high ? 2 : 0ul) | 1)) return false;
                        if (!TryAppendAddress(a, b, ptr_base)) return false;
                    }
                    // mem, mem
                    else if (TryParseAddress(args[1], out UInt64 _a, out UInt64 _b, out Expr _ptr_base, out UInt64 _sc, out bool _exp_sz))
                    { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Cannot use {op} with two memory values"); return false; }
                    // mem, imm
                    else { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Cannot use {op} with an imm"); return false; }
                }
                // imm, *
                else { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Cannot use {op} with an imm"); return false; }

                return true;
            }
            public bool TryProcessLEA(OPCode op)
            {
                if (args.Length != 2) { res = new AssembleResult(AssembleError.ArgCount, $"line {line}: {op} expected 2 args"); return false; }

                if (!TryParseCPURegister(args[0], out UInt64 dest, out UInt64 a_sizecode, out bool dest_high)) { res = new AssembleResult(AssembleError.ArgError, $"line {line}: {op} expecetd register as first arg\n-> {res.ErrorMsg}"); return false; }
                if (a_sizecode == 0) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: {op} does not allow 8-bit addressing"); return false; }

                if (!TryParseAddress(args[1], out UInt64 a, out UInt64 b, out Expr ptr_base, out UInt64 b_sizecode, out bool explicit_size)) { res = new AssembleResult(AssembleError.ArgError, $"line {line}: {op} expected address as second arg\n-> {res.ErrorMsg}"); return false; }

                if (explicit_size && a_sizecode != b_sizecode) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Argument size missmatch"); return false; }
                
                if (!TryAppendVal(1, (UInt64)op)) return false;
                if (!TryAppendVal(1, (dest << 4) | (a_sizecode << 2))) return false;
                if (!TryAppendAddress(a, b, ptr_base)) { res = new AssembleResult(AssembleError.ArgError, $"line {line}: Failed to append value\n-> {res.ErrorMsg}"); return false; }

                return true;
            }
            public bool TryProcessPOP(OPCode op)
            {
                if (args.Length != 1) { res = new AssembleResult(AssembleError.ArgCount, $"line {line}: {op} expected 1 arg"); return false; }

                UInt64 a_sizecode;

                // reg
                if (TryParseCPURegister(args[0], out UInt64 reg, out a_sizecode, out bool a_high))
                {
                    if (a_sizecode == 0) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: {op} does not support 8-bit operands"); return false; }

                    if (!TryAppendVal(1, (reg << 4) | (a_sizecode << 2))) return false;
                }
                // mem
                else if (TryParseAddress(args[0], out UInt64 a, out UInt64 b, out Expr ptr_base, out a_sizecode, out bool explicit_size))
                {
                    if (!explicit_size) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Could not deduce data size"); return false; }

                    if (a_sizecode == 0) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: {op} does not support 8-bit operands"); return false; }

                    if (!TryAppendVal(1, (a_sizecode << 2) | 1)) return false;
                    if (!TryAppendAddress(a, b, ptr_base)) return false;
                }
                else { res = new AssembleResult(AssembleError.UsageError, $"line {line}: {op} expected a register or memory argument"); return false; }

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
                    else { res = new AssembleResult(AssembleError.UsageError, $"line {line}: The specified size combination is not supported by this instruction"); return false; }
                }
                // 32/64, *
                else
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
                    else { res = new AssembleResult(AssembleError.UsageError, $"line {line}: The specified size combination is not supported by this instruction"); return false; }
                }

                return true;
            }
            public bool TryProcessMOVxX(OPCode op, bool sign)
            {
                if (args.Length != 2) { res = new AssembleResult(AssembleError.ArgCount, $"line {line}: Expected 2 operands"); return false; }

                if (!TryParseCPURegister(args[0], out UInt64 dest, out UInt64 dest_sizecode, out bool __dest_high))
                { res = new AssembleResult(AssembleError.UsageError, $"line {line}: First operand must be a CPU register"); return false; }
                
                // can't extend into an 8-bit register (DO NOT REMOVE THIS, NOT HANDLED ELSEWHERE)
                if (dest_sizecode == 0) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: First operand cannot be 8-bit"); return false; }

                // write op code
                if (!TryAppendVal(1, (UInt64)op)) return false;

                // reg, reg
                if (TryParseCPURegister(args[1], out UInt64 src, out UInt64 src_sizecode, out bool src_high))
                {
                    // write the settings byte
                    if (!__TryProcessMOVxX_settings_byte(sign, dest, dest_sizecode, src_sizecode)) return false;

                    // mark source as register
                    if (!TryAppendVal(1, (src_high ? 64 : 0ul) | src)) return false;
                }
                // reg, mem
                else if (TryParseAddress(args[1], out UInt64 a, out UInt64 b, out Expr ptr_base, out src_sizecode, out bool explicit_size))
                {
                    if (!explicit_size) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Could not deduce second operand's size"); return false; }
                    // write the settings byte
                    if (!__TryProcessMOVxX_settings_byte(sign, dest, dest_sizecode, src_sizecode)) return false;

                    // mark source as a memory value and append the address
                    if (!TryAppendVal(1, 128)) return false;
                    if (!TryAppendAddress(a, b, ptr_base)) return false;
                }
                else { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Second operand must be a CPU register or memory value"); return false; }

                return true;
            }

            // -- x87 op formats -- //

            public bool TryProcessFPUBinaryOp(OPCode op, bool integral, bool pop)
            {
                // write op code
                if (!TryAppendVal(1, (UInt64)op)) return false;

                // make sure the programmer doesn't pull any funny business due to our arg-count-based approach
                if (integral && args.Length != 1) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Integral {op} requires 1 arg"); return false; }
                if (pop && args.Length != 0 && args.Length != 2) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Popping {op} requires 0 or 2 args"); return false; }

                // handle arg count cases
                if (args.Length == 0)
                {
                    // no args is st(1) <- f(st(1), st(0)), pop
                    if (!pop) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: No args requires a pop operation"); return false; }

                    if (!TryAppendVal(1, 0x12)) return false;
                }
                else if (args.Length == 1)
                {
                    if (!TryParseAddress(args[0], out UInt64 a, out UInt64 b, out Expr ptr_base, out UInt64 sizecode, out bool explicit_size))
                    { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Expected a memory value"); return false; }

                    if (!explicit_size) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Could not deduce data size"); return false; }

                    // 32-bit
                    if (sizecode == 2)
                    {
                        if (!TryAppendVal(1, integral ? 5 : 3ul)) return false;
                    }
                    // 64-bit
                    else if (sizecode == 3)
                    {
                        if (!TryAppendVal(1, integral ? 6 : 4ul)) return false;
                    }
                    else { res = new AssembleResult(AssembleError.UsageError, $"line {line}: {op} requires 32 or 64-bit operands"); return false; }

                    // write the address
                    if (!TryAppendAddress(a, b, ptr_base)) return false;
                }
                else if (args.Length == 2)
                {
                    if (!TryParseFPURegister(args[0], out UInt64 a) || !TryParseFPURegister(args[1], out UInt64 b))
                    { res = new AssembleResult(AssembleError.UsageError, $"line {line}: {op} with 2 args requires both args be fpu registers"); return false; }

                    // if b is st(0) (do this one first since it handles the pop form)
                    if (b == 0)
                    {
                        // while popping the dest is legal, it's probably not what the programmer intended
                        if (pop && a == 0) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Attempt to pop the destination"); return false; }

                        if (!TryAppendVal(1, (a << 4) | (pop ? 2 : 1ul))) return false;
                    }
                    // if a is st(0)
                    else if (a == 0)
                    {
                        if (pop) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Popping {op} requires arg 2 be st(0)"); return false; }

                        if (!TryAppendVal(1, b << 4)) return false;
                    }
                    // x87 requires one of them be st(0)
                    else { res = new AssembleResult(AssembleError.UsageError, $"line {line}: {op} with 2 args requires one of the registers be st(0)"); return false; }
                }
                else { res = new AssembleResult(AssembleError.ArgCount, $"line {line}: {op} expected between 0 and 2 args"); return false; }

                return true;
            }
            public bool TryProcessFPURegisterOp(OPCode op, bool has_ext_op = false, UInt64 ext_op = 0)
            {
                if (args.Length != 1) { res = new AssembleResult(AssembleError.ArgCount, $"line {line}: {op} expected 1 operands"); return false; }

                if (!TryParseFPURegister(args[0], out UInt64 reg)) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: {op} requires a register operand"); return false; }

                // write op code
                if (!TryAppendVal(1, (UInt64)op)) return false;
                if (has_ext_op) { if (!TryAppendVal(1, ext_op)) return false; }

                // write the register
                if (!TryAppendVal(1, reg)) return false;

                return true;
            }

            public bool TryProcessFLD(OPCode op, bool integral)
            {
                if (args.Length != 1) { res = new AssembleResult(AssembleError.ArgCount, $"line {line}: Expected 1 arg"); return false; }

                // write op code
                if (!TryAppendVal(1, (UInt64)op)) return false;

                // pushing st(i)
                if (TryParseFPURegister(args[0], out UInt64 reg))
                {
                    if (!TryAppendVal(1, reg << 4)) return false;
                }
                // pushing memory value
                else if (TryParseAddress(args[0], out UInt64 a, out UInt64 b, out Expr ptr_base, out UInt64 sizecode, out bool explicit_size))
                {
                    if (!explicit_size) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Could not deduce operand size"); return false; }

                    // handle integral cases
                    if (integral)
                    {
                        if (sizecode == 0) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Instruction does not support 8-bit integral operands"); return false; }

                        if (!TryAppendVal(1, sizecode + 2)) return false;
                    }
                    // otherwise floating-point
                    else
                    {
                        if (sizecode != 2 && sizecode != 3) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Instruction only supports 32 or 64-bit floating-point operands"); return false; }

                        if (!TryAppendVal(1, sizecode - 1)) return false;
                    }

                    // and write the address
                    if (!TryAppendAddress(a, b, ptr_base)) return false;
                }
                else { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Expected an FPU register or a memory value"); return false; }

                return true;
            }
            public bool TryProcessFST(OPCode op, bool integral, bool pop)
            {
                if (args.Length != 1) { res = new AssembleResult(AssembleError.ArgCount, $"line {line}: Expected 1 arg"); return false; }

                // write the op code
                if (!TryAppendVal(1, (UInt64)op)) return false;

                // if it's an fpu register
                if (TryParseFPURegister(args[0], out UInt64 reg))
                {
                    // can't be an integral op
                    if (integral) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Attempt to use integral {op} on an fpu register"); return false; }

                    if (!TryAppendVal(1, (reg << 4) | (pop ? 1 : 0ul))) return false;
                }
                // if it's a memory destination
                else if (TryParseAddress(args[0], out UInt64 a, out UInt64 b, out Expr ptr_base, out UInt64 sizecode, out bool explicit_size))
                {
                    if (!explicit_size) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Could not deduce operand size"); return false; }

                    // if this is integral (i.e. truncation store)
                    if (integral)
                    {
                        if (sizecode == 0) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Integral {op} does not allow 8-bit operands"); return false; }
                        else if (sizecode == 1) { if (!TryAppendVal(1, pop ? 7 : 6ul)) return false; }
                        else if (sizecode == 2) { if (!TryAppendVal(1, pop ? 9 : 8ul)) return false; }
                        else if (sizecode == 3)
                        {
                            // there isn't a non-popping 64-bit int store
                            if (!pop) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: 64-bit integral {op} must be a popping operation"); return false; }

                            if (!TryAppendVal(1, 10)) return false;
                        }
                    }
                    // otherwise is floating-point
                    else
                    {
                        if (sizecode == 2) { if (!TryAppendVal(1, pop ? 3 : 2ul)) return false; }
                        else if (sizecode == 3) { if (!TryAppendVal(1, pop ? 5 : 4ul)) return false; }
                        else { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Attempt to store to unsupported floating-point size"); return false; }
                    }

                    // and write the address
                    if (!TryAppendAddress(a, b, ptr_base)) return false;
                }
                else { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Expected an fpu register or memory value"); return false; }

                return true;
            }
            public bool TryProcessFCOM(OPCode op, bool integral, bool pop, bool pop2)
            {
                // write the op code
                if (!TryAppendVal(1, (UInt64)op)) return false;

                // handle arg count cases
                if (args.Length == 0)
                {
                    if (integral) { res = new AssembleResult(AssembleError.ArgCount, $"line {line}: Integral {op} expected 1 operand"); return false; }

                    // no args is same as using st(1) (plus additional case of double pop)
                    if (!TryAppendVal(1, (1 << 4) | (pop2 ? 2 : pop ? 1 : 0ul))) return false;
                }
                else if (args.Length == 1)
                {
                    if (pop2) { res = new AssembleResult(AssembleError.ArgCount, $"line {line}: Double-popping {op} expected 0 operands"); return false; }

                    // register
                    if (TryParseFPURegister(args[0], out UInt64 reg))
                    {
                        if (integral) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Integral {op} cannot be used on an fpu register"); return false; }

                        if (!TryAppendVal(1, (reg << 4) | (pop ? 1 : 0ul))) return false;
                    }
                    // memory
                    else if (TryParseAddress(args[0], out UInt64 a, out UInt64 b, out Expr ptr_base, out UInt64 sizecode, out bool explicit_size))
                    {
                        if (!explicit_size) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Could not deduce operand size"); return false; }

                        // handle size cases
                        if (sizecode == 0) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: {op} does not support 8-bit operands"); return false; }
                        else if (sizecode == 1)
                        {
                            if (!integral) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: {op} does not support 16-bit floating-point"); return false; }

                            if (!TryAppendVal(1, pop ? 8 : 7ul)) return false;
                        }
                        else if (sizecode == 2)
                        {
                            if (!TryAppendVal(1, integral ? pop ? 10 : 9ul : pop ? 4 : 3ul)) return false;
                        }
                        else if (sizecode == 3)
                        {
                            if (integral) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Integral {op} does not support 64-bit operands"); return false; }

                            if (!TryAppendVal(1, pop ? 6 : 5ul)) return false;
                        }

                        // and write the address
                        if (!TryAppendAddress(a, b, ptr_base)) return false;
                    }
                    else { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Expected an fpu register or a memory value"); return false; }
                }
                else { res = new AssembleResult(AssembleError.ArgCount, $"line {line}: Expected 0 or 1 operands"); return false; }

                return true;
            }
            public bool TryProcessFCOMI(OPCode op, bool unordered, bool pop)
            {
                if (args.Length != 2) { res = new AssembleResult(AssembleError.ArgCount, $"line {line}: Expected 2 operands"); return false; }

                if (!TryParseFPURegister(args[0], out UInt64 reg) || reg != 0) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: First operand must be st(0)"); return false; }
                if (!TryParseFPURegister(args[1], out reg)) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Second operand must be an fpu register"); return false; }

                // write the op code
                if (!TryAppendVal(1, (UInt64)op)) return false;

                if (!TryAppendVal(1, (pop ? 128 : 0ul) | (unordered ? 64 : 0ul) | reg)) return false;

                return true;
            }
            public bool TryProcessFMOVcc(OPCode op, UInt64 condition)
            {
                if (args.Length != 2) { res = new AssembleResult(AssembleError.ArgCount, $"line {line}: {op} expected 2 operands"); return false; }

                if (!TryParseFPURegister(args[0], out UInt64 reg) || reg != 0) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: operand 1 must be st(0)"); return false; }
                if (!TryParseFPURegister(args[1], out reg)) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: operand 2 must be an fpu register"); return false; }

                // write op code
                if (!TryAppendVal(1, (UInt64)op)) return false;

                if (!TryAppendVal(1, (reg << 4) | condition)) return false;

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

        static Assembly()
        {
            // create definitions for all the syscall codes
            foreach (SyscallCode item in Enum.GetValues(typeof(SyscallCode)))
                DefineSymbol($"sys_{item.ToString().ToLower()}", (UInt64)item);

            // create definitions for all the error codes
            foreach (ErrorCode item in Enum.GetValues(typeof(ErrorCode)))
                DefineSymbol($"err_{item.ToString().ToLower()}", (UInt64)item);
        }

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

                        default: err = $"line {data.Line}: Attempt to use unsupported floating-point size"; return PatchError.Error;
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
        /// Assembles the code into an object file
        /// </summary>
        /// <param name="code">the code to assemble</param>
        /// <param name="file">the resulting object file if no errors occur</param>
        public static AssembleResult Assemble(string code, out ObjectFile file)
        {
            AssembleArgs args = new AssembleArgs()
            {
                file = file = new ObjectFile(),

                time = Computer.Time,

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
                ["__time__"] = new Expr() { IntResult = args.time },
                ["__version__"] = new Expr() { IntResult = Computer.Version },

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
            UInt64 a = 0;
            bool floating;

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
                // split the line
                if (!args.SplitLine(code.Substring(pos, end - pos))) return new AssembleResult(AssembleError.FormatError, $"line {args.line}: Failed to parse line\n-> {args.res.ErrorMsg}");
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

                        case "DB": if (!args.TryProcessDeclare(1)) return args.res; goto op_done;
                        case "DW": if (!args.TryProcessDeclare(2)) return args.res; goto op_done;
                        case "DD": if (!args.TryProcessDeclare(4)) return args.res; goto op_done;
                        case "DQ": if (!args.TryProcessDeclare(8)) return args.res; goto op_done;

                        case "RESB": if (!args.TryProcessReserve(1)) return args.res; goto op_done;
                        case "RESW": if (!args.TryProcessReserve(2)) return args.res; goto op_done;
                        case "RESD": if (!args.TryProcessReserve(4)) return args.res; goto op_done;
                        case "RESQ": if (!args.TryProcessReserve(8)) return args.res; goto op_done;
                        case "REST": if (!args.TryProcessReserve(10)) return args.res; goto op_done;

                        case "EQU": if (!args.TryProcessEQU()) return args.res; goto op_done;

                        case "SEGMENT": case "SECTION": if (!args.TryProcessSegment()) return args.res; goto op_done;
                    }

                    // if it wasn't a directive it's about to be an instruction: make sure we're in the text segment
                    if (args.current_seg != AsmSegment.TEXT) return new AssembleResult(AssembleError.FormatError, $"line {args.line}: Attempt to write executable instructions to the {args.current_seg} segment");

                    // -- instruction routing -- //
                    switch (args.op.ToUpper())
                    {
                        // x86 instructions

                        case "NOP": if (!args.TryProcessNoArgOp(OPCode.NOP)) return args.res; break;

                        case "HLT": if (!args.TryProcessNoArgOp(OPCode.HLT)) return args.res; break;
                        case "SYSCALL": if (!args.TryProcessNoArgOp(OPCode.SYSCALL)) return args.res; break;

                        case "PUSHF": if (!args.TryProcessNoArgOp(OPCode.PUSHF, true, 0)) return args.res; break;
                        case "PUSHFD": if (!args.TryProcessNoArgOp(OPCode.PUSHF, true, 1)) return args.res; break;
                        case "PUSHFQ": if (!args.TryProcessNoArgOp(OPCode.PUSHF, true, 2)) return args.res; break;

                        case "POPF": if (!args.TryProcessNoArgOp(OPCode.POPF, true, 0)) return args.res; break;
                        case "POPFD": if (!args.TryProcessNoArgOp(OPCode.POPF, true, 1)) return args.res; break;
                        case "POPFQ": if (!args.TryProcessNoArgOp(OPCode.POPF, true, 2)) return args.res; break;

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
                        case "MOVS": if (!args.TryProcessBinaryOp(OPCode.MOVcc, true, 2)) return args.res; break;
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

                        case "JMP": if (!args.TryProcessIMMRM(OPCode.JMP, false, 0, 14)) return args.res; break;

                        case "JZ": case "JE": if (!args.TryProcessIMMRM(OPCode.Jcc, true, 0, 14)) return args.res; break;
                        case "JNZ": case "JNE": if (!args.TryProcessIMMRM(OPCode.Jcc, true, 1, 14)) return args.res; break;
                        case "JS": if (!args.TryProcessIMMRM(OPCode.Jcc, true, 2, 14)) return args.res; break;
                        case "JNS": if (!args.TryProcessIMMRM(OPCode.Jcc, true, 3, 14)) return args.res; break;
                        case "JP": case "JPE": if (!args.TryProcessIMMRM(OPCode.Jcc, true, 4, 14)) return args.res; break;
                        case "JNP": case "JPO": if (!args.TryProcessIMMRM(OPCode.Jcc, true, 5, 14)) return args.res; break;
                        case "JO": if (!args.TryProcessIMMRM(OPCode.Jcc, true, 6, 14)) return args.res; break;
                        case "JNO": if (!args.TryProcessIMMRM(OPCode.Jcc, true, 7, 14)) return args.res; break;
                        case "JC": if (!args.TryProcessIMMRM(OPCode.Jcc, true, 8, 14)) return args.res; break;
                        case "JNC": if (!args.TryProcessIMMRM(OPCode.Jcc, true, 9, 14)) return args.res; break;

                        case "JB": case "JNAE": if (!args.TryProcessIMMRM(OPCode.Jcc, true, 10, 14)) return args.res; break;
                        case "JBE": case "JNA": if (!args.TryProcessIMMRM(OPCode.Jcc, true, 11, 14)) return args.res; break;
                        case "JA": case "JNBE": if (!args.TryProcessIMMRM(OPCode.Jcc, true, 12, 14)) return args.res; break;
                        case "JAE": case "JNB": if (!args.TryProcessIMMRM(OPCode.Jcc, true, 13, 14)) return args.res; break;

                        case "JL": case "JNGE": if (!args.TryProcessIMMRM(OPCode.Jcc, true, 14, 14)) return args.res; break;
                        case "JLE": case "JNG": if (!args.TryProcessIMMRM(OPCode.Jcc, true, 15, 14)) return args.res; break;
                        case "JG": case "JNLE": if (!args.TryProcessIMMRM(OPCode.Jcc, true, 16, 14)) return args.res; break;
                        case "JGE": case "JNL": if (!args.TryProcessIMMRM(OPCode.Jcc, true, 17, 14)) return args.res; break;

                        case "JCXZ": if (!args.TryProcessIMMRM(OPCode.Jcc, true, 18, 14)) return args.res; break;
                        case "JECXZ": if (!args.TryProcessIMMRM(OPCode.Jcc, true, 19, 14)) return args.res; break;
                        case "JRCXZ": if (!args.TryProcessIMMRM(OPCode.Jcc, true, 20, 14)) return args.res; break;

                        case "LOOP": if (!args.TryProcessIMMRM(OPCode.LOOP, false, 0, 14)) return args.res; break;
                        case "LOOPZ": case "LOOPE": if (!args.TryProcessIMMRM(OPCode.LOOPe, false, 0, 14)) return args.res; break;
                        case "LOOPNZ": case "LOOPNE": if (!args.TryProcessIMMRM(OPCode.LOOPne, false, 0, 14)) return args.res; break;

                        case "CALL": if (!args.TryProcessIMMRM(OPCode.CALL, false, 0, 14)) return args.res; break;
                        case "RET": if (!args.TryProcessNoArgOp(OPCode.RET)) return args.res; break;

                        case "PUSH": if (!args.TryProcessIMMRM(OPCode.PUSH, false, 0, 14)) return args.res; break;
                        case "POP": if (!args.TryProcessPOP(OPCode.POP)) return args.res; break;

                        case "LEA": if (!args.TryProcessLEA(OPCode.LEA)) return args.res; break;

                        case "ADD": if (!args.TryProcessBinaryOp(OPCode.ADD)) return args.res; break;
                        case "SUB": if (!args.TryProcessBinaryOp(OPCode.SUB)) return args.res; break;

                        case "MUL": if (!args.TryProcessIMMRM(OPCode.MUL)) return args.res; break;
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

                        case "SHL": if (!args.TryProcessBinaryOp(OPCode.SHL, false, 0, 15, 0)) return args.res; break;
                        case "SHR": if (!args.TryProcessBinaryOp(OPCode.SHR, false, 0, 15, 0)) return args.res; break;
                        case "SAL": if (!args.TryProcessBinaryOp(OPCode.SAL, false, 0, 15, 0)) return args.res; break;
                        case "SAR": if (!args.TryProcessBinaryOp(OPCode.SAR, false, 0, 15, 0)) return args.res; break;
                        case "ROL": if (!args.TryProcessBinaryOp(OPCode.ROL, false, 0, 15, 0)) return args.res; break;
                        case "ROR": if (!args.TryProcessBinaryOp(OPCode.ROR, false, 0, 15, 0)) return args.res; break;

                        case "AND": if (!args.TryProcessBinaryOp(OPCode.AND)) return args.res; break;
                        case "OR": if (!args.TryProcessBinaryOp(OPCode.OR)) return args.res; break;
                        case "XOR": if (!args.TryProcessBinaryOp(OPCode.XOR)) return args.res; break;

                        case "INC": if (!args.TryProcessUnaryOp(OPCode.INC)) return args.res; break;
                        case "DEC": if (!args.TryProcessUnaryOp(OPCode.DEC)) return args.res; break;
                        case "NEG": if (!args.TryProcessUnaryOp(OPCode.NEG)) return args.res; break;
                        case "NOT": if (!args.TryProcessUnaryOp(OPCode.NOT)) return args.res; break;

                        case "CMP":
                            // if there are 2 args and the second one is an instant 0, we can make this a CMPZ instruction
                            if (args.args.Length == 2 && args.TryParseInstantImm(args.args[1], out a, out floating) && a == 0)
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
                        case "ANDN": if (!args.TryProcessBinaryOp(OPCode.ANDN)) return args.res; break;

                        case "BT": if (!args.TryProcessBinaryOp(OPCode.BT, true, 0, 15, 0, false)) return args.res; break;
                        case "BTS": if (!args.TryProcessBinaryOp(OPCode.BT, true, 1, 15, 0, false)) return args.res; break;
                        case "BTR": if (!args.TryProcessBinaryOp(OPCode.BT, true, 2, 15, 0, false)) return args.res; break;
                        case "BTC": if (!args.TryProcessBinaryOp(OPCode.BT, true, 3, 15, 0, false)) return args.res; break;
                            
                        case "CWD": if (!args.TryProcessNoArgOp(OPCode.Cxy, true, 0)) return args.res; break;
                        case "CDQ": if (!args.TryProcessNoArgOp(OPCode.Cxy, true, 1)) return args.res; break;
                        case "CQO": if (!args.TryProcessNoArgOp(OPCode.Cxy, true, 2)) return args.res; break;

                        case "MOVZX": if (!args.TryProcessMOVxX(OPCode.MOVxX, false)) return args.res; break;
                        case "MOVSX": if (!args.TryProcessMOVxX(OPCode.MOVxX, true)) return args.res; break;

                        // x87 instructions

                        case "FNOP": if (!args.TryProcessNoArgOp(OPCode.NOP)) return args.res; break; // no sense in wasting another opcode on no-op
                        case "FWAIT": break; // allow FWAIT and WAIT but don't do anything (exceptions in CSX64 are immediate)
                        case "WAIT": break;

                        case "FLD1": if (!args.TryProcessNoArgOp(OPCode.FLD_const, true, 0)) return args.res; break;
                        case "FLDL2T": if (!args.TryProcessNoArgOp(OPCode.FLD_const, true, 1)) return args.res; break;
                        case "FLDL2E": if (!args.TryProcessNoArgOp(OPCode.FLD_const, true, 2)) return args.res; break;
                        case "FLDPI": if (!args.TryProcessNoArgOp(OPCode.FLD_const, true, 3)) return args.res; break;
                        case "FLDLG2": if (!args.TryProcessNoArgOp(OPCode.FLD_const, true, 4)) return args.res; break;
                        case "FLDLN2": if (!args.TryProcessNoArgOp(OPCode.FLD_const, true, 4)) return args.res; break;
                        case "FLDZ": if (!args.TryProcessNoArgOp(OPCode.FLD_const, true, 5)) return args.res; break;

                        case "FLD": if (!args.TryProcessFLD(OPCode.FLD, false)) return args.res; break;
                        case "FILD": if (!args.TryProcessFLD(OPCode.FLD, true)) return args.res; break;

                        case "FST": if (!args.TryProcessFST(OPCode.FST, false, false)) return args.res; break;
                        case "FIST": if (!args.TryProcessFST(OPCode.FST, true, false)) return args.res; break;
                        case "FSTP": if (!args.TryProcessFST(OPCode.FST, false, true)) return args.res; break;
                        case "FISTP": if (!args.TryProcessFST(OPCode.FST, true, true)) return args.res; break;

                        case "FXCH": // no arg version swaps st0 and st1
                            if (args.args.Length == 0) { if (!args.TryProcessNoArgOp(OPCode.FXCH, true, 1)) return args.res; break; }
                            else { if (!args.TryProcessFPURegisterOp(OPCode.FXCH)) return args.res; break; }

                        case "FMOVE": if (!args.TryProcessFMOVcc(OPCode.FMOVcc, 0)) return args.res; break;
                        case "FMOVNE": if (!args.TryProcessFMOVcc(OPCode.FMOVcc, 1)) return args.res; break;
                        case "FMOVB": if (!args.TryProcessFMOVcc(OPCode.FMOVcc, 2)) return args.res; break;
                        case "FMOVBE": if (!args.TryProcessFMOVcc(OPCode.FMOVcc, 3)) return args.res; break;
                        case "FMOVA": if (!args.TryProcessFMOVcc(OPCode.FMOVcc, 4)) return args.res; break;
                        case "FMOVAE": if (!args.TryProcessFMOVcc(OPCode.FMOVcc, 5)) return args.res; break;
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

                        case "FXAM": if (!args.TryProcessNoArgOp(OPCode.FXAM)) return args.res; break;
                        case "FTST": if (!args.TryProcessNoArgOp(OPCode.FTST)) return args.res; break;

                        case "FCOM": if (!args.TryProcessFCOM(OPCode.FCOM, false, false, false)) return args.res; break;
                        case "FCOMP": if (!args.TryProcessFCOM(OPCode.FCOM, false, true, false)) return args.res; break;
                        case "FCOMPP": if (!args.TryProcessFCOM(OPCode.FCOM, false, false, true)) return args.res; break;
                        case "FICOM": if (!args.TryProcessFCOM(OPCode.FCOM, true, false, false)) return args.res; break;
                        case "FICOMP": if (!args.TryProcessFCOM(OPCode.FCOM, true, true, false)) return args.res; break;

                        case "FCOMI": if (!args.TryProcessFCOMI(OPCode.FCOMI, false, false)) return args.res; break;
                        case "FCOMIP": if (!args.TryProcessFCOMI(OPCode.FCOMI, false, true)) return args.res; break;
                        case "FUCOMI": if (!args.TryProcessFCOMI(OPCode.FCOMI, true, false)) return args.res; break;
                        case "FUCOMIP": if (!args.TryProcessFCOMI(OPCode.FCOMI, true, true)) return args.res; break;

                        case "FSIN": if (!args.TryProcessNoArgOp(OPCode.FSIN)) return args.res; break;
                        case "FCOS": if (!args.TryProcessNoArgOp(OPCode.FCOS)) return args.res; break;
                        case "FSINCOS": if (!args.TryProcessNoArgOp(OPCode.FSINCOS)) return args.res; break;
                        case "FPTAN": if (!args.TryProcessNoArgOp(OPCode.FPTAN)) return args.res; break;
                        case "FPATAN": if (!args.TryProcessNoArgOp(OPCode.FPATAN)) return args.res; break;
                        
                        case "FDECSTP": if (!args.TryProcessNoArgOp(OPCode.FDECSTP)) return args.res; break;
                        case "FINCSTP": if (!args.TryProcessNoArgOp(OPCode.FINCSTP)) return args.res; break;
                        case "FFREE": if (!args.TryProcessFPURegisterOp(OPCode.FFREE)) return args.res; break;

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
            for (int i = file.TextHoles.Count - 1; i >= 0; --i)
            {
                switch (TryPatchHole(file.Text, file.Symbols, file.TextHoles[i], ref err))
                {
                    case PatchError.None: file.TextHoles.RemoveAt(i); break; // remove the hole if we solved it
                    case PatchError.Unevaluated: break;
                    case PatchError.Error: return new AssembleResult(AssembleError.ArgError, err);

                    default: throw new ArgumentException("Unknown patch error encountered");
                }
            }
            for (int i = file.RodataHoles.Count - 1; i >= 0; --i)
            {
                switch (TryPatchHole(file.Rodata, file.Symbols, file.RodataHoles[i], ref err))
                {
                    case PatchError.None: file.RodataHoles.RemoveAt(i); break; // remove the hole if we solved it
                    case PatchError.Unevaluated: break;
                    case PatchError.Error: return new AssembleResult(AssembleError.ArgError, err);

                    default: throw new ArgumentException("Unknown patch error encountered");
                }
            }
            for (int i = file.DataHoles.Count - 1; i >= 0; --i)
            {
                switch (TryPatchHole(file.Data, file.Symbols, file.DataHoles[i], ref err))
                {
                    case PatchError.None: file.DataHoles.RemoveAt(i); break; // remove the hole if we solved it
                    case PatchError.Unevaluated: break;
                    case PatchError.Error: return new AssembleResult(AssembleError.ArgError, err);

                    default: throw new ArgumentException("Unknown patch error encountered");
                }
            }

            // -- eliminate as many unnecessary symbols as we can -- //

            List<string> elim_symbols = new List<string>(); // symbol names to be eliminated

            // for each symbol
            foreach (var entry in file.Symbols)
            {
                // if this symbol has already been evaluated and isn't global
                if (entry.Value.IsEvaluated && !file.GlobalSymbols.Contains(entry.Key))
                {
                    // we can eliminate it (because it's already been linked internally and won't be needed externally)
                    elim_symbols.Add(entry.Key);
                }
            }
            // remove all the symbols we can eliminate
            foreach (string elim in elim_symbols) file.Symbols.Remove(elim);

            // -- finalize -- //

            // verify integrity of file
            if (!args.VerifyIntegrity()) return args.res;

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
        /// <param name="objs">the object files to link. should all be clean</param>
        /// <exception cref="ArgumentException"></exception>
        public static LinkResult Link(out byte[] exe, params ObjectFile[] objs)
        {
            exe = null; // initially null result

            // for each object file to be linked
            foreach (ObjectFile obj in objs)
            {
                // make sure it's starting out clean
                if (!obj.Clean) throw new ArgumentException("Attempt to use dirty object file");
            }

            // -- define things -- //

            // create data segments (we don't know how large the resulting file will be, so it needs to be expandable) (write header to text segment)
            List<byte> text = new List<byte>()
            {
                (byte)OPCode.CALL, 0x0e, 0, 0, 0, 0, 0, 0, 0, 0,      // call main         ; call main function
                (byte)OPCode.MOV, 0x1c, 0x00,                         // mov  rbx, rax     ; mov ret value to rbx for sys_exit
                (byte)OPCode.XOR, 0x0c, 0x00,                         // xor  rax, rax     ; clear rax
                (byte)OPCode.MOV, 0x00, 0x10, (byte)SyscallCode.Exit, // mov  al, sys_exit ; load sys_exit (bypasses endianness by only using low byte)
                (byte)OPCode.SYSCALL                                  // syscall           ; perform sys_exit to set error code and stop execution
            };
            List<byte> rodata = new List<byte>();
            List<byte> data = new List<byte>();
            UInt64 bsslen = 0;

            // a table for relating global symbols to their object file
            var global_to_obj = new Dictionary<string, ObjectFile>();

            // the queue of object files that need to be added to the executable
            var include_queue = new Queue<ObjectFile>();
            // a table for relating included object files to their beginning positions in the resulting binary (text, data, bss) tuples
            var included = new Dictionary<ObjectFile, Tuple<UInt64, UInt64, UInt64, UInt64>>();

            // parsing locations for evaluation
            UInt64 _res;
            bool _floating;
            string _err = string.Empty;

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
                foreach (string reserved in VerifyLegalExpressionIgnores)
                    if (obj.Symbols.ContainsKey(reserved)) return new LinkResult(LinkError.SymbolRedefinition, $"Object file defined symbol with name \"{reserved}\" (reserved)");
            }

            // ensure we got a "main" global symbol
            if (!global_to_obj.TryGetValue("main", out ObjectFile main_obj)) return new LinkResult(LinkError.MissingSymbol, "No entry point");

            // make sure we start the merge process with the main object file
            include_queue.Enqueue(main_obj);

            // add a hole for the call location of the header
            main_obj.TextHoles.Add(new HoleData() { Address = 2 - (UInt32)text.Count, Size = 8, Expr = new Expr() { Token = "main" }, Line = -1 });

            // -- merge things -- //

            // while there are still things in queue
            while (include_queue.Count > 0)
            {
                // get the object file we need to incorporate
                ObjectFile obj = include_queue.Dequeue();
                // all included files are dirty
                obj.Clean = false;

                // add it to the set of included files
                included.Add(obj, new Tuple<UInt64, UInt64, UInt64, UInt64>((UInt64)text.Count, (UInt64)rodata.Count, (UInt64)data.Count, bsslen));
                
                // offset holes to be relative to the start of their total segment (not relative to resulting file)
                foreach (HoleData hole in obj.TextHoles) hole.Address += (UInt32)text.Count;
                foreach (HoleData hole in obj.RodataHoles) hole.Address += (UInt32)rodata.Count;
                foreach (HoleData hole in obj.DataHoles) hole.Address += (UInt32)data.Count;

                // append segments
                for (int i = 0; i < obj.Text.Count; ++i) text.Add(obj.Text[i]);
                for (int i = 0; i < obj.Rodata.Count; ++i) rodata.Add(obj.Rodata[i]);
                for (int i = 0; i < obj.Data.Count; ++i) data.Add(obj.Data[i]);
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

            // now that we're done merging we need to define segment offsets in the result
            foreach (var entry in included)
            {
                // alias the object file
                ObjectFile obj = entry.Key;

                // define the segment offsets
                obj.Symbols.Add("##TEXT", new Expr() { IntResult = 0 });
                obj.Symbols.Add("##RODATA", new Expr() { IntResult = (UInt64)text.Count });
                obj.Symbols.Add("##DATA", new Expr() { IntResult = (UInt64)text.Count + (UInt64)rodata.Count });
                obj.Symbols.Add("##BSS", new Expr() { IntResult = (UInt64)text.Count + (UInt64)rodata.Count + (UInt64)data.Count });

                // and file-scope segment offsets
                obj.Symbols.Add("#TEXT", new Expr() { IntResult = entry.Value.Item1 });
                obj.Symbols.Add("#RODATA", new Expr() { IntResult = (UInt64)text.Count + entry.Value.Item2 });
                obj.Symbols.Add("#DATA", new Expr() { IntResult = (UInt64)text.Count + (UInt64)rodata.Count + entry.Value.Item3 });
                obj.Symbols.Add("#BSS", new Expr() { IntResult = (UInt64)text.Count + (UInt64)rodata.Count + (UInt64)data.Count + entry.Value.Item4 });

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
                foreach (HoleData hole in obj.TextHoles)
                {
                    switch (TryPatchHole(text, obj.Symbols, hole, ref _err))
                    {
                        case PatchError.None: break;
                        case PatchError.Unevaluated: return new LinkResult(LinkError.MissingSymbol, _err);
                        case PatchError.Error: return new LinkResult(LinkError.FormatError, _err);

                        default: throw new ArgumentException("Unknown patch error encountered");
                    }
                }
                foreach (HoleData hole in obj.RodataHoles)
                {
                    switch (TryPatchHole(rodata, obj.Symbols, hole, ref _err))
                    {
                        case PatchError.None: break;
                        case PatchError.Unevaluated: return new LinkResult(LinkError.MissingSymbol, _err);
                        case PatchError.Error: return new LinkResult(LinkError.FormatError, _err);

                        default: throw new ArgumentException("Unknown patch error encountered");
                    }
                }
                foreach (HoleData hole in obj.DataHoles)
                {
                    switch (TryPatchHole(data, obj.Symbols, hole, ref _err))
                    {
                        case PatchError.None: break;
                        case PatchError.Unevaluated: return new LinkResult(LinkError.MissingSymbol, _err);
                        case PatchError.Error: return new LinkResult(LinkError.FormatError, _err);

                        default: throw new ArgumentException("Unknown patch error encountered");
                    }
                }
            }

            // -- finalize things -- //
            
            // allocate executable space (header + text + data)
            exe = new byte[32 + (UInt64)text.Count + (UInt64)rodata.Count + (UInt64)data.Count];

            // write header (length of each segment)
            exe.Write(0, 8, (UInt64)text.Count);
            exe.Write(8, 8, (UInt64)rodata.Count);
            exe.Write(16, 8, (UInt64)data.Count);
            exe.Write(24, 8, bsslen);

            // copy text and data
            text.CopyTo(0, exe, 32, text.Count);
            rodata.CopyTo(0, exe, 32 + text.Count, rodata.Count);
            data.CopyTo(0, exe, 32 + text.Count + rodata.Count, data.Count);

            // linked successfully
            return new LinkResult(LinkError.None, string.Empty);
        }
    }
}