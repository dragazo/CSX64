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
        None, ArgCount, MissingSize, ArgError, FormatError, UsageError, UnknownOp, EmptyFile, InvalidLabel, SymbolRedefinition, UnknownSymbol
    }
    public enum LinkError
    {
        None, EmptyResult, SymbolRedefinition, MissingSymbol, FormatError
    }
    internal enum PatchError
    {
        None, Unevaluated, Error
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
        /// All the holes that need to be patched by the linker
        /// </summary>
        internal List<HoleData> Holes = new List<HoleData>();

        /// <summary>
        /// The executable data
        /// </summary>
        internal List<byte> Data = new List<byte>();

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

            // write the holes (length-prefixed)
            writer.Write(obj.Holes.Count);
            foreach (HoleData hole in obj.Holes)
                HoleData.WriteTo(writer, hole);

            // write the data (length-prefixed)
            writer.Write(obj.Data.Count);
            writer.Write(obj.Data.ToArray()); // ToArray() costs an O(n) copy, but still beats an equal number of function calls
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

            // read the holes (length-prefixed)
            count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                HoleData.ReadFrom(reader, out HoleData hole);
                obj.Holes.Add(hole);
            }

            // read the data (length-prefixed)
            count = reader.ReadInt32();
            byte[] data = new byte[count];
            if (reader.Read(data, 0, count) != count) throw new FormatException("Object file was corrupted");
            obj.Data = data.ToList();

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

                    if (LF || RF) { res = DoubleAsUInt64((LF ? AsDouble(L) : L.MakeSigned()) * (RF ? AsDouble(R) : R.MakeSigned())); floating = true; }
                    else res = L * R;
                    break;
                case OPs.Div:
                    if (!Left.__Evaluate__(symbols, out L, out LF, ref err, visited)) ret = false;
                    if (!Right.__Evaluate__(symbols, out R, out RF, ref err, visited)) ret = false;
                    if (ret == false) return false;

                    if (LF || RF) { res = DoubleAsUInt64((LF ? AsDouble(L) : L.MakeSigned()) / (RF ? AsDouble(R) : R.MakeSigned())); floating = true; }
                    else res = (L.MakeSigned() / R.MakeSigned()).MakeUnsigned();
                    break;
                case OPs.Mod:
                    if (!Left.__Evaluate__(symbols, out L, out LF, ref err, visited)) ret = false;
                    if (!Right.__Evaluate__(symbols, out R, out RF, ref err, visited)) ret = false;
                    if (ret == false) return false;

                    if (LF || RF) { res = DoubleAsUInt64((LF ? AsDouble(L) : L.MakeSigned()) % (RF ? AsDouble(R) : R.MakeSigned())); floating = true; }
                    else res = (L.MakeSigned() % R.MakeSigned()).MakeUnsigned();
                    break;
                case OPs.Add:
                    if (!Left.__Evaluate__(symbols, out L, out LF, ref err, visited)) ret = false;
                    if (!Right.__Evaluate__(symbols, out R, out RF, ref err, visited)) ret = false;
                    if (ret == false) return false;

                    if (LF || RF) { res = DoubleAsUInt64((LF ? AsDouble(L) : L.MakeSigned()) + (RF ? AsDouble(R) : R.MakeSigned())); floating = true; }
                    else res = L + R;
                    break;
                case OPs.Sub:
                    if (!Left.__Evaluate__(symbols, out L, out LF, ref err, visited)) ret = false;
                    if (!Right.__Evaluate__(symbols, out R, out RF, ref err, visited)) ret = false;
                    if (ret == false) return false;

                    if (LF || RF) { res = DoubleAsUInt64((LF ? AsDouble(L) : L.MakeSigned()) - (RF ? AsDouble(R) : R.MakeSigned())); floating = true; }
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

                    if (LF || RF) res = (LF ? AsDouble(L) : L.MakeSigned()) < (RF ? AsDouble(R) : R.MakeSigned()) ? 1 : 0ul;
                    else res = L.MakeSigned() < R.MakeSigned() ? 1 : 0ul;
                    break;
                case OPs.LessE:
                    if (!Left.__Evaluate__(symbols, out L, out LF, ref err, visited)) ret = false;
                    if (!Right.__Evaluate__(symbols, out R, out RF, ref err, visited)) ret = false;
                    if (ret == false) return false;

                    if (LF || RF) res = (LF ? AsDouble(L) : L.MakeSigned()) <= (RF ? AsDouble(R) : R.MakeSigned()) ? 1 : 0ul;
                    else res = L.MakeSigned() <= R.MakeSigned() ? 1 : 0ul;
                    break;
                case OPs.Great:
                    if (!Left.__Evaluate__(symbols, out L, out LF, ref err, visited)) ret = false;
                    if (!Right.__Evaluate__(symbols, out R, out RF, ref err, visited)) ret = false;
                    if (ret == false) return false;

                    if (LF || RF) res = (LF ? AsDouble(L) : L.MakeSigned()) > (RF ? AsDouble(R) : R.MakeSigned()) ? 1 : 0ul;
                    else res = L.MakeSigned() > R.MakeSigned() ? 1 : 0ul;
                    break;
                case OPs.GreatE:
                    if (!Left.__Evaluate__(symbols, out L, out LF, ref err, visited)) ret = false;
                    if (!Right.__Evaluate__(symbols, out R, out RF, ref err, visited)) ret = false;
                    if (ret == false) return false;

                    if (LF || RF) res = (LF ? AsDouble(L) : L.MakeSigned()) >= (RF ? AsDouble(R) : R.MakeSigned()) ? 1 : 0ul;
                    else res = L.MakeSigned() >= R.MakeSigned() ? 1 : 0ul;
                    break;

                case OPs.Eq:
                    if (!Left.__Evaluate__(symbols, out L, out LF, ref err, visited)) ret = false;
                    if (!Right.__Evaluate__(symbols, out R, out RF, ref err, visited)) ret = false;
                    if (ret == false) return false;

                    if (LF || RF) res = (LF ? AsDouble(L) : L.MakeSigned()) == (RF ? AsDouble(R) : R.MakeSigned()) ? 1 : 0ul;
                    else res = L == R ? 1 : 0ul;
                    break;
                case OPs.Neq:
                    if (!Left.__Evaluate__(symbols, out L, out LF, ref err, visited)) ret = false;
                    if (!Right.__Evaluate__(symbols, out R, out RF, ref err, visited)) ret = false;
                    if (ret == false) return false;

                    if (LF || RF) res = (LF ? AsDouble(L) : L.MakeSigned()) != (RF ? AsDouble(R) : R.MakeSigned()) ? 1 : 0ul;
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

                    res = LF ? ((Int64)AsDouble(L)).MakeUnsigned() : L;
                    break;
                case OPs.Float:
                    if (!Left.__Evaluate__(symbols, out L, out LF, ref err, visited)) return false;

                    res = LF ? L : DoubleAsUInt64((double)L.MakeSigned());
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

        private bool _FindPath(string value, Stack<Expr> path)
        {
            // mark ourselves as a candidate
            path.Push(this);

            // if we're a leaf test ourself
            if (OP == OPs.None)
            {
                // if we found the value, we're done
                if (value == Token) return true;
            }
            // otherwise test children
            else
            {
                // if they found it, we're done
                if (Left._FindPath(value, path) || Right != null && Right._FindPath(value, path)) return true;
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
        public bool FindPath(string value, out Stack<Expr> path)
        {
            // create the stack
            path = new Stack<Expr>();

            // refer to helper
            return _FindPath(value, path);
        }
        /// <summary>
        /// Finds the path to the specified value in the expression tree. Returns true on success. This version reuses the stack object by first clearing its contents.
        /// </summary>
        /// <param name="value">the value to find</param>
        /// <param name="path">the path to the specified value, with the root at the bottom of the stack and the found node at the top</param>
        public bool FindPath(string value, Stack<Expr> path)
        {
            // ensure stack is empty
            path.Clear();

            // refer to helper
            return _FindPath(value, path);
        }
        /// <summary>
        /// Finds the value in the specified expression tree. Returns it on success, otherwise null
        /// </summary>
        /// <param name="value">the found node or null</param>
        public Expr Find(string value)
        {
            // if we're a leaf, test ourself
            if (OP == OPs.None) return Token == value ? this : null;
            // otherwise test children
            else return Left.Find(value) ?? Right?.Find(value);
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

        private void _ToString(StringBuilder b)
        {
            if (OP == OPs.None)
            {
                b.Append(Token == null ? _Floating ? AsDouble(_Result).ToString("e17") : _Result.MakeSigned().ToString() : Token);
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
        /// <summary>
        /// Holds all the variables used during assembly
        /// </summary>
        private class AssembleArgs
        {
            public ObjectFile file;
            public int line;

            public string[] label_defs; // must be array for ref params
            public string op;
            public UInt64 sizecode;
            public string[] args;       // must be array for ref params

            private int last_static_label_index = -1; // unique index for static labels
            private string _last_static_label;
            public string last_static_label // string value of last static label
            {
                get => _last_static_label;
                set
                {
                    _last_static_label = value;
                    ++last_static_label_index;
                }
            }

            public UInt64 current_line_pos; // value used for the $ macro

            public AssembleResult res;

            public UInt64 time;

            // -- Assembly Functions --

            public const char CommentChar = ';';
            public const char LabelDefChar = ':';
            public const char OpSizeSeparatorChar = ':';

            public const UInt64 EmissionMaxMultiplier = 1000000;
            public const char EmissionMultiplierChar = '#';

            public static readonly List<string> VerifyLegalExpressionIgnores = new List<string>()
            { "#base", "__prog_end__" };

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

                // parse labels
                for (; pos < rawline.Length; pos = end)
                {
                    // skip leading white space
                    for (; pos < rawline.Length && char.IsWhiteSpace(rawline[pos]); ++pos) ;
                    // get a white space-delimited token
                    for (end = pos; end < rawline.Length && !char.IsWhiteSpace(rawline[end]); ++end) ;

                    // if it's a label, add to tokens
                    if (pos != end && rawline[end - 1] == LabelDefChar) tokens.Add(rawline.Substring(pos, end - pos - 1));
                    // otherwise we're done with labels
                    else break; // break ensures we also keep pos pointing to start of next section
                }
                label_defs = tokens.ToArray(); // dump tokens as label defs
                tokens.Clear(); // empty tokens for reuse

                // parse op
                if (pos < rawline.Length)
                {
                    // get up till size separator or white space
                    for (end = pos; end < rawline.Length && rawline[end] != OpSizeSeparatorChar && !char.IsWhiteSpace(rawline[end]); ++end) ;

                    // make sure we got a well-formed op
                    if (pos == end) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Operation size specification encountered without an operation"); return false; }

                    // save this as op
                    op = rawline.Substring(pos, end - pos);

                    // if we got a size specification
                    if (end < rawline.Length && rawline[end] == OpSizeSeparatorChar)
                    {
                        pos = end + 1; // position to beginning of size specification

                        // if starting parenthetical section
                        if (pos < rawline.Length && rawline[pos] == '(')
                        {
                            int depth = 1; // parenthetical depth

                            // get till depth of zero
                            for (end = pos + 1; end < rawline.Length && depth > 0; ++end)
                            {
                                if (rawline[end] == '(') ++depth;
                                else if (rawline[end] == ')') --depth;
                            }

                            if (depth != 0) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Mismatched parenthesis encountered in operation size specification"); return false; }
                        }
                        // ohterwise standard imm
                        else
                        {
                            // take all legal chars
                            for (end = pos; end < rawline.Length && (rawline[end] == '_' || char.IsLetterOrDigit(rawline[end])); ++end) ;
                        }

                        // make sure we didn't end on non-white space
                        if (end < rawline.Length && !char.IsWhiteSpace(rawline[end])) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Size parameter must be separated from arguments by white space"); return false; }

                        // parse the read size code
                        if (!TryParseSizecode(rawline.Substring(pos, end - pos).RemoveWhiteSpace(), out sizecode))
                        { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Failed to parse operation size specification\n-> {res.ErrorMsg}"); return false; }
                    }
                    // otherwise use default size (64-bit)
                    else sizecode = 3;

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

            public static bool IsValidName(string token)
            {
                // can't be empty or over 255 chars long
                if (token.Length == 0 || token.Length > 255) return false;

                // first char is underscore or letter
                if (token[0] != '_' && !char.IsLetter(token[0])) return false;
                // all other chars may additionally be numbers
                for (int i = 1; i < token.Length; ++i)
                    if (token[i] != '_' && !char.IsLetterOrDigit(token[i])) return false;

                return true;
            }
            public bool MutateName(ref string label)
            {
                // if defining a local label
                if (label[0] == '.')
                {
                    string sub = label.Substring(1); // local symbol name
                    
                    // local name can't be empty
                    if (!IsValidName(sub)) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: \"{label}\" is not a legal local symbol name"); return false; }
                    // can't make a local label before any non-local ones exist
                    if (last_static_label == null) { res = new AssembleResult(AssembleError.InvalidLabel, $"line {line}: Cannot define a local symbol before the first static label"); return false; }

                    // mutate the label
                    //label = $"__local_{time:x16}_{last_static_label}_{sub}";
                    label = $"L{last_static_label_index:x}_{(time & 0xffff):x}_{sub}";
                }

                return true;
            }

            public void AppendVal(UInt64 size, UInt64 val)
            {
                // write the value (little-endian)
                file.Data.Append(size, val);
            }
            public bool TryAppendExpr(UInt64 size, Expr expr, int type = 3)
            {
                string err = null; // evaluation error

                // create the hole data
                HoleData data = new HoleData() { Address = (UInt32)file.Data.Count, Size = (byte)size, Line = line, Expr = expr };
                // write a dummy (all 1's for easy manual identification)
                AppendVal(size, 0xffffffffffffffff);

                // try to patch it
                switch (TryPatchHole(file.Data, file.Symbols, data, ref err))
                {
                    case PatchError.None: break;
                    case PatchError.Unevaluated: file.Holes.Add(data); break;
                    case PatchError.Error: res = new AssembleResult(AssembleError.ArgError, $"line {line}: Error encountered while patching expression\n-> {err}"); return false;

                    default: throw new ArgumentException("Unknown patch error encountered");
                }

                return true;
            }
            public bool TryAppendAddress(UInt64 a, UInt64 b, Expr hole)
            {
                // [1: literal][3: m1][1: -m2][3: m2]   ([4: r1][4: r2])   ([64: imm])
                AppendVal(1, a);
                if ((a & 0x77) != 0) AppendVal(1, b);
                if ((a & 0x80) != 0) { if (!TryAppendExpr(8, hole)) { res = new AssembleResult(AssembleError.ArgError, $"line {line}: Failed to append address base\n-> {res.ErrorMsg}"); return false; } }

                return true;
            }

            public static readonly Dictionary<Expr.OPs, int> Precedence = new Dictionary<Expr.OPs, int>()
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
            public static readonly List<char> UnaryOps = new List<char>() { '+', '-', '~', '!', '*', '/' };

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

                        // if it's the $ current line position macro
                        if (val == "$")
                        {
                            temp = new Expr() { OP = Expr.OPs.Add, Left = new Expr() { Token = "#base" }, Right = new Expr() { IntResult = current_line_pos } };
                        }
                        // if it's the $$ last static label macro
                        else if (val == "$$")
                        {
                            // can't use this before there's a static label
                            if (last_static_label == null) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Cannot use the last static label macro $$ before the first static label"); return false; }

                            temp = new Expr() { Token = last_static_label };
                        }
                        // otherwise it's a normal value/symbol
                        else
                        {
                            // create the hole for it
                            temp = new Expr() { Token = val };

                            // it either needs to be evaluatable or a valid label name
                            if (!temp.Evaluate(file.Symbols, out UInt64 _res, out bool floating, ref err) && !IsValidName(val))
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
                Ptrdiff(expr);

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
            public bool TryExtractPtrVal(Expr expr, out Expr val)
            {
                // if this is a leaf, extract the expression from symbols table
                if (expr.OP == Expr.OPs.None)
                {
                    // if there's no token, fail
                    if (expr.Token == null) { val = null; return false; }

                    // get the symbol
                    if (!file.Symbols.TryGetValue(expr.Token, out expr)) { val = null; return false; }
                }

                // must be of standard label form
                if (expr.OP != Expr.OPs.Add || expr.Left.Token != "#base") { val = null; return false; }

                // return the value portion
                val = expr.Right;
                return true;
            }
            /// <summary>
            /// Performs pointer subtraction on the specified expression where possible
            /// </summary>
            /// <param name="expr">the expression to operate on</param>
            public void Ptrdiff(Expr expr)
            {
                // -- fix up the expression tree -- //

                top:

                // if we're subtracting
                if (expr.OP == Expr.OPs.Sub)
                {
                    // if right is negated
                    if (expr.Right.OP == Expr.OPs.Neg)
                    {
                        // bypass negation by making ourselves addition
                        expr.OP = Expr.OPs.Add;
                        expr.Right = expr.Right.Left;

                        // back to top
                        goto top;
                    }
                    // if left is double negated
                    else if (expr.Left.OP == Expr.OPs.Neg && expr.Left.Left.OP == Expr.OPs.Neg)
                    {
                        // eliminate it trivially
                        expr.Left = expr.Left.Left.Left;

                        // back to top
                        goto top;
                    }
                }
                // if we're adding
                else if (expr.OP == Expr.OPs.Add)
                {
                    // if right is negated
                    if (expr.Right.OP == Expr.OPs.Neg)
                    {
                        // bypass negation by making ourselves subtraction
                        expr.OP = Expr.OPs.Sub;
                        expr.Right = expr.Right.Left;

                        // back to top
                        goto top;
                    }
                    // if left is negated
                    else if (expr.Left.OP == Expr.OPs.Neg)
                    {
                        // bypass negation by making ourselves subtraction
                        expr.OP = Expr.OPs.Sub;
                        expr.Left = expr.Left.Left;
                        Swap(ref expr.Left, ref expr.Right);

                        // back to top
                        goto top;
                    }
                }

                // -- the main attraction -- //

                // if we're subtracting, we can eliminate #base if both sides are pointers
                if (expr.OP == Expr.OPs.Sub)
                {
                    // if we can get their pointer values
                    if (TryExtractPtrVal(expr.Left, out Expr a) && TryExtractPtrVal(expr.Right, out Expr b))
                    {
                        // replace left and right with a and b (eliminating #base)
                        expr.Left = a;
                        expr.Right = b;
                    }
                }

                // recurse to children (to ensure we cover all non-trivial uses)
                if (expr.Left != null) Ptrdiff(expr.Left);
                if (expr.Right != null) Ptrdiff(expr.Right);
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
            public bool TryParseRegister(string token, out UInt64 val)
            {
                // get the prefixed instant imm
                if (!TryParseInstantPrefixedImm(token, "$", out val, out bool floating)) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Failed to parse \"{token}\" as a valid register address\n-> {res.ErrorMsg}"); return false; }

                // ensure not floating and in proper range
                if (floating) { res = new AssembleResult(AssembleError.ArgError, $"line {line}: Attempt to use floating point value to specify register \"{token}\""); return false; }
                if (val >= 16) { res = new AssembleResult(AssembleError.ArgError, $"line {line}: Register index out of range \"{token}\" (evaluated to {val})"); return false; }

                return true;
            }
            public bool TryParseSizecode(string token, out UInt64 val)
            {
                // size code must be instant imm
                if (!TryParseInstantImm(token, out val, out bool floating)) { res = new AssembleResult(AssembleError.ArgError, $"line {line}: Failed to parse size code \"{token}\"\n-> {res.ErrorMsg}"); return false; }

                // ensure not floating
                if (floating) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Attempt to use floating point value to specify register size \"{token}\""); return false; }

                // convert to size code
                switch (val)
                {
                    case 8: val = 0; return true;
                    case 16: val = 1; return true;
                    case 32: val = 2; return true;
                    case 64: val = 3; return true;

                    default: res = new AssembleResult(AssembleError.ArgError, $"line {line}: Invalid register size: {val}"); return false;
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
                while (hole.FindPath(label, path))
                {
                    // move path into list
                    while (path.Count > 0) list.Add(path.Pop());

                    // if it doesn't have a mult section
                    if (list.Count == 1 || list.Count > 1 && list[1].OP != Expr.OPs.Mul)
                    {
                        // add in a multiplier of 1
                        list[0].OP = Expr.OPs.Mul;
                        list[0].Left = new Expr() { Token = "1" };
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
                    list[1].OP = Expr.OPs.None;
                    list[1].Left = list[1].Right = null;
                    list[1].Token = "0";

                    m += val; // add extracted mult to total mult
                    list.Clear(); // clear list for next pass
                }

                // -- final task: get mult code and negative flag -- //

                // if m is pretty big, it's negative
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

                    default: res = new AssembleResult(AssembleError.ArgError, $"line {line}: Invalid register multiplier encountered ({m.MakeSigned()})"); return false;
                }

                // register successfully parsed
                return true;
            }
            public bool TryParseAddress(string token, out UInt64 a, out UInt64 b, out Expr hole)
            {
                a = b = 0;
                hole = new Expr();

                // must be of [*] format
                if (token.Length < 3 || token[0] != '[' || token[token.Length - 1] != ']') { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Invalid address format encountered \"{token}\""); return false; }

                int pos, end; // parsing positions

                UInt64 temp = 0; // parsing temporaries
                bool btemp = false;

                int reg_count = 0; // number of registers parsed
                UInt64 r1 = 0, m1 = 0, r2 = 0, m2 = 0; // final register info
                bool n1 = false, n2 = false;

                string preface = $"__reg_{time:x16}"; // preface used for registers
                string err = string.Empty; // evaluation error

                List<UInt64> regs = new List<UInt64>(); // the registers found in the expression

                // replace registers with temporary names
                while (true)
                {
                    // find the next register marker
                    for (pos = 1; pos < token.Length && token[pos] != '$'; ++pos) ;
                    // if this starts parenthetical region
                    if (pos + 1 < token.Length && token[pos + 1] == '(')
                    {
                        int depth = 1; // depth of 1

                        // start searching for ending parens after first parens
                        for (end = pos + 2; end < token.Length && depth > 0; ++end)
                        {
                            if (token[end] == '(') ++depth;
                            else if (token[end] == ')') --depth;
                        }

                        // make sure we reached zero depth
                        if (depth != 0) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Mismatched parenthesis in register expression \"{token.Substring(pos, end - pos)}\""); return false; }
                    }
                    // otherwise normal symbol
                    else
                    {
                        // take all legal chars
                        for (end = pos + 1; end < token.Length && (char.IsLetterOrDigit(token[end]) || token[end] == '_'); ++end) ;
                    }

                    // break out if we've reached the end
                    if (pos >= token.Length) break;

                    // parse this as a register
                    if (!TryParseRegister(token.Substring(pos, end - pos), out temp)) return false;

                    // put it in a register slot
                    if (!regs.Contains(temp)) regs.Add(temp);

                    // modify the register label in the expression to be a legal symbol name
                    token = $"{token.Substring(0, pos)}{preface}_{temp}{token.Substring(end)}";
                }

                // turn into an expression
                if (!TryParseImm(token.Substring(1, token.Length - 2), out hole)) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Failed to parse address expression\n-> {res.ErrorMsg}"); return false; }

                // look through each register found
                foreach (UInt64 reg in regs)
                {
                    // get the register data
                    if (!TryParseAddressReg($"{preface}_{reg}", ref hole, out temp, out btemp)) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Failed to extract register data\n-> {res.ErrorMsg}"); return false; }

                    // if the multiplier was nonzero, the register is really being used
                    if (temp != 0)
                    {
                        // put it into an available r slot
                        if (reg_count == 0) { r1 = reg; m1 = temp; n1 = btemp; }
                        else if (reg_count == 1) { r2 = reg; m2 = temp; n2 = btemp; }
                        else { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Can't use more than 2 registers to specify an address"); return false; }

                        ++reg_count; // mark this slot as filled
                    }
                }

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

                // make sure we didn't define an external symbol
                foreach (string external in file.ExternalSymbols)
                    if (file.Symbols.ContainsKey(external)) { res = new AssembleResult(AssembleError.SymbolRedefinition, $"External symbol \"{external}\" was defined within the file"); return false; }

                // make sure all symbols expressions were valid
                foreach (var entry in file.Symbols)
                {
                    if (!VerifyLegalExpression(entry.Value)) return false;

                    // sanity check - ensure they also had legal names
                    if (!IsValidName(entry.Key)) { res = new AssembleResult(AssembleError.InvalidLabel, $"Symbol name \"{entry.Key}\" is not valid"); return false; }
                }

                // make sure all hole expressions were valid
                foreach (HoleData hole in file.Holes)
                    if (!VerifyLegalExpression(hole.Expr)) return false;

                // the hood is good
                return true;
            }

            // -- op formats -- //

            public bool TryProcessBinaryOp(OPCode op, int _b_sizecode = -1, UInt64 sizemask = 15)
            {
                UInt64 a, b, c; // parsing temporaries
                Expr hole1, hole2;

                if (args.Length != 2) { res = new AssembleResult(AssembleError.ArgCount, $"line {line}: {op} expected 2 args"); return false; }
                if ((Size(sizecode) & sizemask) == 0) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: {op} does not support the specified size code"); return false; }

                AppendVal(1, (UInt64)op);

                UInt64 b_sizecode = _b_sizecode == -1 ? sizecode : (UInt64)_b_sizecode;

                // reg, *
                if (args[0][0] == '$')
                {
                    if (!TryParseRegister(args[0], out a)) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Failed to parse \"{args[0]}\" as a register\n-> {res.ErrorMsg}"); return false; }

                    // reg, reg
                    if (args[1][0] == '$')
                    {
                        if (!TryParseRegister(args[1], out b)) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Failed to parse \"{args[1]}\" as a register\n-> {res.ErrorMsg}"); return false; }

                        AppendVal(1, (a << 4) | (sizecode << 2) | 2);
                        AppendVal(1, b);
                    }
                    // reg, mem
                    else if (args[1][0] == '[')
                    {
                        if (!TryParseAddress(args[1], out b, out c, out hole1)) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Failed to parse \"{args[1]}\" as an address\n-> {res.ErrorMsg}"); return false; }

                        AppendVal(1, (a << 4) | (sizecode << 2) | 1);
                        if (!TryAppendAddress(b, c, hole1)) { res = new AssembleResult(AssembleError.ArgError, $"line {line}: Failed to append value\n-> {res.ErrorMsg}"); return false; }
                    }
                    // reg, imm
                    else
                    {
                        if (!TryParseImm(args[1], out hole1)) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Failed to parse \"{args[1]}\" as an imm\n-> {res.ErrorMsg}"); return false; }

                        AppendVal(1, (a << 4) | (sizecode << 2) | 0);
                        if (!TryAppendExpr(Size(b_sizecode), hole1)) { res = new AssembleResult(AssembleError.ArgError, $"line {line}: Failed to append value\n-> {res.ErrorMsg}"); return false; }
                    }
                }
                // mem, *
                else if (args[0][0] == '[')
                {
                    if (!TryParseAddress(args[0], out a, out b, out hole1)) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Failed to parse \"{args[0]}\" as an address\n-> {res.ErrorMsg}"); return false; }

                    // mem, reg
                    if (args[1][0] == '$')
                    {
                        if (!TryParseRegister(args[1], out c)) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Failed to parse \"{args[1]}\" as a register\n-> {res.ErrorMsg}"); return false; }

                        AppendVal(1, (sizecode << 2) | 2);
                        AppendVal(1, 16 | c);
                        if (!TryAppendAddress(a, b, hole1)) { res = new AssembleResult(AssembleError.ArgError, $"line {line}: Failed to append value\n-> {res.ErrorMsg}"); return false; };
                    }
                    // mem, mem
                    else if (args[1][0] == '[') { res = new AssembleResult(AssembleError.FormatError, $"line {line}: {op} does not support memory-to-memory"); return false; }
                    // mem, imm
                    else
                    {
                        if (!TryParseImm(args[1], out hole2)) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Failed to parse \"{args[1]}\" as an imm\n-> {res.ErrorMsg}"); return false; }

                        AppendVal(1, (sizecode << 2) | 3);
                        if (!TryAppendExpr(Size(b_sizecode), hole2)) { res = new AssembleResult(AssembleError.ArgError, $"line {line}: Failed to append value\n-> {res.ErrorMsg}"); return false; }
                        if (!TryAppendAddress(a, b, hole1)) { res = new AssembleResult(AssembleError.ArgError, $"line {line}: Failed to append value\n-> {res.ErrorMsg}"); return false; }
                    }
                }
                else { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Destination must be register or memory"); return false; }

                return true;
            }
            public bool TryProcessUnaryOp(OPCode op, UInt64 sizemask = 15)
            {
                UInt64 a, b;
                Expr hole;

                if (args.Length != 1) { res = new AssembleResult(AssembleError.ArgCount, $"line {line}: {op} expected 1 arg"); return false; }
                if ((Size(sizecode) & sizemask) == 0) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: {op} does not support the specified size code"); return false; }

                AppendVal(1, (UInt64)op);

                // reg
                if (args[0][0] == '$')
                {
                    if (!TryParseRegister(args[0], out a)) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Failed to parse \"{args[0]}\" as a register\n-> {res.ErrorMsg}"); return false; }

                    AppendVal(1, (a << 4) | (sizecode << 2) | 0);
                }
                // mem
                else if (args[0][0] == '[')
                {
                    if (!TryParseAddress(args[0], out a, out b, out hole)) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Failed to parse \"{args[0]}\" as an address\n-> {res.ErrorMsg}"); return false; }

                    AppendVal(1, (sizecode << 2) | 1);
                    if (!TryAppendAddress(a, b, hole)) { res = new AssembleResult(AssembleError.ArgError, $"line {line}: Failed to append value\n-> {res.ErrorMsg}"); return false; }
                }
                // imm
                else { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Destination must be register or memory"); return false; }

                return true;
            }
            public bool TryProcessJump(OPCode op)
            {
                if (args.Length != 1) { res = new AssembleResult(AssembleError.ArgCount, $"line {line}: {op} expected 1 arg"); return false; }

                if (!TryParseAddress(args[0], out UInt64 a, out UInt64 b, out Expr hole)) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Jump expected address as first arg\n-> {res.ErrorMsg}"); return false; }

                AppendVal(1, (UInt64)op);
                if (!TryAppendAddress(a, b, hole)) { res = new AssembleResult(AssembleError.ArgError, $"line {line}: Failed to append value\n-> {res.ErrorMsg}"); return false; }

                return true;
            }
            public bool TryProcessEmission()
            {
                if (args.Length == 0) { res = new AssembleResult(AssembleError.ArgCount, $"line {line}: Emission expected at least one value"); return false; }

                Expr hole = new Expr() { IntResult = 0 }; // initially integral zero (shorthand for empty buffer)
                UInt64 mult;
                bool floating;

                for (int i = 0; i < args.Length; ++i)
                {
                    if (args[i].Length == 0) { res = new AssembleResult(AssembleError.ArgError, $"line {line}: Emission encountered empty argument"); return false; }

                    // if a multiplier
                    if (args[i][0] == EmissionMultiplierChar)
                    {
                        // cannot be used immediately following another multiplier
                        if (i > 0 && args[i - 1][0] == EmissionMultiplierChar) { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Emission multiplier cannot immediately follow an emission multiplier"); return false; }
                        // cannot be used immediately following a string
                        if (i > 0 && args[i - 1][0] == '"') { res = new AssembleResult(AssembleError.UsageError, $"line {line}: Emission multiplier cannot immediately follow a string argument"); return false; }

                        // get the prefixed multiplier
                        if (!TryParseInstantPrefixedImm(args[i], EmissionMultiplierChar.ToString(), out mult, out floating)) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Failed to parse \"{args[i]}\" as an emission multiplier\n-> {res.ErrorMsg}"); return false; }

                        // ensure the multiplier we got was valid
                        if (floating) { res = new AssembleResult(AssembleError.ArgError, $"line {line}: Emission multiplier cannot be floating point"); return false; }
                        if (mult > EmissionMaxMultiplier) { res = new AssembleResult(AssembleError.ArgError, $"line {line}: Emission multiplier cannot exceed {EmissionMaxMultiplier}. Got {mult}"); return false; }
                        if (mult == 0) { res = new AssembleResult(AssembleError.ArgError, $"line {line}: Emission multiplier cannot be zero"); return false; }

                        // already wrote 1 copy, now write the others
                        for (UInt64 j = 1; j < mult; ++j)
                            if (!TryAppendExpr(Size(sizecode), hole)) { res = new AssembleResult(AssembleError.ArgError, $"line {line}: Failed to append value\n-> {res.ErrorMsg}"); return false; }
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

                            AppendVal(Size(sizecode), args[i][j]);
                        }
                    }
                    // otherwise is a value
                    else
                    {
                        // get the value
                        if (!TryParseImm(args[i], out hole)) return false;

                        // make one of them
                        if (!TryAppendExpr(Size(sizecode), hole)) { res = new AssembleResult(AssembleError.ArgError, $"line {line}: Failed to append value\n-> {res.ErrorMsg}"); return false; }
                    }
                }

                return true;
            }
            public bool TryProcessIMMRM(OPCode op)
            {
                UInt64 a, b;
                Expr hole;

                if (args.Length != 1) { res = new AssembleResult(AssembleError.ArgCount, $"line {line}: {op} expected 1 arg"); return false; }

                AppendVal(1, (UInt64)op);

                // reg
                if (args[0][0] == '$')
                {
                    if (!TryParseRegister(args[0], out a)) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Failed to parse \"{args[0]}\" as a register\n-> {res.ErrorMsg}"); return false; }

                    AppendVal(1, (a << 4) | (sizecode << 2) | 1);
                }
                // mem
                else if (args[0][0] == '[')
                {
                    if (!TryParseAddress(args[0], out a, out b, out hole)) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Failed to parse \"{args[0]}\" as an address\n-> {res.ErrorMsg}"); return false; }

                    AppendVal(1, (sizecode << 2) | 2);
                    if (!TryAppendAddress(a, b, hole)) { res = new AssembleResult(AssembleError.ArgError, $"line {line}: Failed to append value\n-> {res.ErrorMsg}"); return false; }
                }
                // imm
                else
                {
                    if (!TryParseImm(args[0], out hole)) { res = new AssembleResult(AssembleError.FormatError, $"line {line}: Failed to parse \"{args[0]}\" as an imm\n-> {res.ErrorMsg}"); return false; }

                    AppendVal(1, (sizecode << 2) | 0);
                    if (!TryAppendExpr(Size(sizecode), hole)) { res = new AssembleResult(AssembleError.ArgError, $"line {line}: Failed to append value\n-> {res.ErrorMsg}"); return false; }
                }

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
                        case 8: res.Write(data.Address, 8, val); break;
                        case 4: res.Write(data.Address, 4, FloatAsUInt64((float)AsDouble(val))); break;

                        default: err = $"line {data.Line}: Attempt to use unsupported floating-point size"; return PatchError.Error;
                    }
                }
                // otherwise it's integral
                else res.Write(data.Address, data.Size, val);
            }
            else { err = $"line {data.Line}: Failed to evaluate expression\n-> {err}"; return PatchError.Unevaluated; }

            // successfully patched
            return PatchError.None;
        }

        /// <summary>
        /// Assembles the code into an object file
        /// </summary>
        /// <param name="code">the code to assemble</param>
        /// <param name="file">the resulting object file if no errors occur</param>
        public static AssembleResult Assemble(string code, out ObjectFile file)
        {
            file = new ObjectFile();
            AssembleArgs args = new AssembleArgs()
            {
                file = file,
                line = 0,

                last_static_label = null,
                res = default(AssembleResult),

                time = Computer.Time
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
            UInt64 a = 0, b = 0, c = 0, d = 0;
            Expr hole;
            bool floating;

            string err = null; // error location for evaluation

            if (code.Length == 0) return new AssembleResult(AssembleError.EmptyFile, "The file was empty");

            while (pos < code.Length)
            {
                // update current line pos
                args.current_line_pos = (UInt64)args.file.Data.Count;

                // find the next separator
                for (end = pos; end < code.Length && code[end] != '\n' && code[end] != AssembleArgs.CommentChar; ++end) ;

                ++args.line; // advance line counter
                // split the line
                if (!args.SplitLine(code.Substring(pos, end - pos))) return new AssembleResult(AssembleError.FormatError, $"line {args.line}: Failed to parse line\n-> {args.res.ErrorMsg}");
                // if the separator was a comment character, consume the rest of the line as well as no-op
                if (end < code.Length && code[end] == AssembleArgs.CommentChar)
                    for (; end < code.Length && code[end] != '\n'; ++end) ;

                // process marked labels
                for (int i = 0; i < args.label_defs.Length; ++i)
                {
                    string label = args.label_defs[i]; // shorthand reference to current label

                    // ensure it's not empty
                    if (label.Length == 0) return new AssembleResult(AssembleError.InvalidLabel, $"line {args.line}: Empty label encountered");

                    // handle local mutation
                    if (label[0] != '.') args.last_static_label = label;

                    // mutate and test result for legality
                    if (!args.MutateName(ref label)) return args.res;
                    if (!AssembleArgs.IsValidName(label)) return new AssembleResult(AssembleError.InvalidLabel, $"line {args.line}: Symbol name \"{label}\" is invalid");

                    // ensure we don't redefine a symbol
                    if (file.Symbols.ContainsKey(label)) return new AssembleResult(AssembleError.SymbolRedefinition, $"line {args.line}: Symbol \"{label}\" was already defined");

                    // add the symbol as an address (uses illegal symbol #base, which will be defined at link time)
                    file.Symbols.Add(label, new Expr() { OP = Expr.OPs.Add, Left = new Expr() { Token = "#base" }, Right = new Expr() { IntResult = args.current_line_pos } });
                }

                // empty lines are ignored
                if (args.op != string.Empty)
                {
                    switch (args.op.ToUpper())
                    {
                        case "GLOBAL":
                            if (args.args.Length == 0) return new AssembleResult(AssembleError.ArgCount, $"line {args.line}: GLOBAL expected at least one symbol to export");

                            foreach (string symbol in args.args)
                            {
                                // special error message for using global on local labels
                                if (symbol[0] == '.') return new AssembleResult(AssembleError.ArgError, $"line {args.line}: Cannot export local symbols");

                                // test name for legality
                                if (!AssembleArgs.IsValidName(symbol)) return new AssembleResult(AssembleError.InvalidLabel, $"line {args.line}: Invalid symbol name \"{symbol}\"");

                                // don't add to global list twice
                                if (file.GlobalSymbols.Contains(symbol)) return new AssembleResult(AssembleError.SymbolRedefinition, $"line {args.line}: Attempt to export symbol \"{symbol}\" multiple times");

                                // add it to the globals list
                                file.GlobalSymbols.Add(symbol);
                            }

                            break;
                        case "EXTERN":
                            if (args.args.Length == 0) return new AssembleResult(AssembleError.ArgCount, $"line {args.line}: EXTERN expected at least one symbol to import");

                            foreach (string symbol in args.args)
                            {
                                // special error message for using extern on local labels
                                if (symbol[0] == '.') return new AssembleResult(AssembleError.ArgError, $"line {args.line}: Cannot import local symbols");

                                // test name for legality
                                if (!AssembleArgs.IsValidName(symbol)) return new AssembleResult(AssembleError.InvalidLabel, $"line {args.line}: Invalid symbol name \"{symbol}\"");

                                // don't add to external list twice
                                if (file.ExternalSymbols.Contains(symbol)) return new AssembleResult(AssembleError.SymbolRedefinition, $"line {args.line}: Attempt to import symbol \"{symbol}\" multiple times");

                                // add it to the external list
                                file.ExternalSymbols.Add(symbol);
                            }

                            break;
                        case "DEF":
                            if (args.args.Length != 2) return new AssembleResult(AssembleError.ArgCount, $"line {args.line}: DEF expected 2 args");

                            // mutate and test result for legality
                            if (!args.MutateName(ref args.args[0])) return args.res;
                            if (!AssembleArgs.IsValidName(args.args[0])) return new AssembleResult(AssembleError.InvalidLabel, $"line {args.line}: Invalid label name \"{args.args[0]}\"");

                            // get the expression
                            if (!args.TryParseImm(args.args[1], out hole)) return new AssembleResult(AssembleError.FormatError, $"line {args.line}: DEF expected an expression as second arg\n-> {args.res.ErrorMsg}");

                            // don't redefine a symbol
                            if (file.Symbols.ContainsKey(args.args[0])) return new AssembleResult(AssembleError.SymbolRedefinition, $"line {args.line}: Symbol \"{args.args[0]}\" was already defined");

                            // add it to the dictionary
                            file.Symbols.Add(args.args[0], hole);
                            break;

                        case "EMIT": if (!args.TryProcessEmission()) return args.res; break;

                        // --------------------------
                        // -- OPCode assembly impl --
                        // --------------------------

                        // [8: op]
                        case "NOP":
                            if (args.args.Length != 0) return new AssembleResult(AssembleError.ArgCount, $"line {args.line}: NOP expected 0 args");
                            args.AppendVal(1, (UInt64)OPCode.NOP);
                            break;
                        case "STOP":
                            if (args.args.Length != 0) return new AssembleResult(AssembleError.ArgCount, $"line {args.line}: STOP expected 0 args");
                            args.AppendVal(1, (UInt64)OPCode.STOP);
                            break;
                        case "SYSCALL":
                            if (args.args.Length != 0) return new AssembleResult(AssembleError.ArgCount, $"line {args.line}: SYSCALL expected 0 args");
                            args.AppendVal(1, (UInt64)OPCode.SYSCALL);
                            break;

                        case "MOV": if (!args.TryProcessBinaryOp(OPCode.MOV)) return args.res; break;

                        case "MOVA": case "MOVNBE": if (!args.TryProcessBinaryOp(OPCode.MOVa)) return args.res; break;
                        case "MOVAE": case "MOVNB": if (!args.TryProcessBinaryOp(OPCode.MOVae)) return args.res; break;
                        case "MOVB": case "MOVNAE": if (!args.TryProcessBinaryOp(OPCode.MOVb)) return args.res; break;
                        case "MOVBE": case "MOVNA": if (!args.TryProcessBinaryOp(OPCode.MOVbe)) return args.res; break;

                        case "MOVG": case "MOVNLE": if (!args.TryProcessBinaryOp(OPCode.MOVg)) return args.res; break;
                        case "MOVGE": case "MOVNL": if (!args.TryProcessBinaryOp(OPCode.MOVge)) return args.res; break;
                        case "MOVL": case "MOVNGE": if (!args.TryProcessBinaryOp(OPCode.MOVl)) return args.res; break;
                        case "MOVLE": case "MOVNG": if (!args.TryProcessBinaryOp(OPCode.MOVle)) return args.res; break;

                        case "MOVZ": case "MOVE": if (!args.TryProcessBinaryOp(OPCode.MOVz)) return args.res; break;
                        case "MOVNZ": case "MOVNE": if (!args.TryProcessBinaryOp(OPCode.MOVnz)) return args.res; break;
                        case "MOVS": if (!args.TryProcessBinaryOp(OPCode.MOVs)) return args.res; break;
                        case "MOVNS": if (!args.TryProcessBinaryOp(OPCode.MOVns)) return args.res; break;
                        case "MOVP": case "MOVPE": if (!args.TryProcessBinaryOp(OPCode.MOVp)) return args.res; break;
                        case "MOVNP": case "MOVPO": if (!args.TryProcessBinaryOp(OPCode.MOVnp)) return args.res; break;
                        case "MOVO": if (!args.TryProcessBinaryOp(OPCode.MOVo)) return args.res; break;
                        case "MOVNO": if (!args.TryProcessBinaryOp(OPCode.MOVno)) return args.res; break;
                        case "MOVC": if (!args.TryProcessBinaryOp(OPCode.MOVc)) return args.res; break;
                        case "MOVNC": if (!args.TryProcessBinaryOp(OPCode.MOVnc)) return args.res; break;

                        case "SWAP":
                            if (args.args.Length != 2) return new AssembleResult(AssembleError.ArgCount, $"line {args.line}: SWAP expected 2 args");

                            args.AppendVal(1, (UInt64)OPCode.SWAP);

                            // reg, *
                            if (args.args[0][0] == '$')
                            {
                                if (!args.TryParseRegister(args.args[0], out a)) return new AssembleResult(AssembleError.FormatError, $"line {args.line}: Failed to parse \"{args.args[0]}\" as a register\n-> {args.res.ErrorMsg}");

                                // reg, reg
                                if (args.args[1][0] == '$')
                                {
                                    if (!args.TryParseRegister(args.args[1], out b)) return new AssembleResult(AssembleError.FormatError, $"line {args.line}: Failed to parse \"{args.args[1]}\" as a register\n-> {args.res.ErrorMsg}");

                                    args.AppendVal(1, (a << 4) | (args.sizecode << 2) | 0);
                                    args.AppendVal(1, b);
                                }
                                // reg, mem
                                else if (args.args[1][0] == '[')
                                {
                                    if (!args.TryParseAddress(args.args[1], out b, out c, out hole)) return new AssembleResult(AssembleError.FormatError, $"line {args.line}: Failed to parse \"{args.args[1]}\" as an address\n-> {args.res.ErrorMsg}");

                                    args.AppendVal(1, (a << 4) | (args.sizecode << 2) | 1);
                                    if (!args.TryAppendAddress(b, c, hole)) return new AssembleResult(AssembleError.ArgError, $"line {args.line}: Failed to append value\n-> {args.res.ErrorMsg}");
                                }
                                // reg, imm
                                else return new AssembleResult(AssembleError.FormatError, $"line {args.line}: Cannot use SWAP with an imm");
                            }
                            // mem, *
                            else if (args.args[0][0] == '[')
                            {
                                if (!args.TryParseAddress(args.args[0], out a, out b, out hole)) return new AssembleResult(AssembleError.FormatError, $"line {args.line}: Failed to parse \"{args.args[0]}\" as an address\n-> {args.res.ErrorMsg}");

                                // mem, reg
                                if (args.args[1][0] == '$')
                                {
                                    if (!args.TryParseRegister(args.args[1], out c)) return new AssembleResult(AssembleError.FormatError, $"line {args.line}: Failed to parse \"{args.args[1]}\" as a register\n-> {args.res.ErrorMsg}");

                                    args.AppendVal(1, (c << 4) | (args.sizecode << 2) | 1);
                                    if (!args.TryAppendAddress(a, b, hole)) return new AssembleResult(AssembleError.ArgError, $"line {args.line}: Failed to append value");
                                }
                                // mem, mem
                                else if (args.args[1][0] == '[') return new AssembleResult(AssembleError.FormatError, $"line {args.line}: Cannot use SWAP with two memory values");
                                // mem, imm
                                else return new AssembleResult(AssembleError.FormatError, $"line {args.line}: Cannot use SWAP with an imm");
                            }
                            // imm, *
                            else return new AssembleResult(AssembleError.FormatError, $"line {args.line}: Cannot use SWAP with an imm");

                            break;

                        case "UX": a = (UInt64)OPCode.UX; goto XEXTEND;
                        case "SX":
                            a = (UInt64)OPCode.SX;
                            XEXTEND:
                            if (args.args.Length != 2) return new AssembleResult(AssembleError.ArgCount, $"line {args.line}: XEXTEND expected 2 args");

                            if (!args.TryParseSizecode(args.args[0], out b)) return new AssembleResult(AssembleError.MissingSize, $"line {args.line}: UEXTEND expected size parameter as second arg\n-> {args.res.ErrorMsg}");
                            if (!args.TryParseRegister(args.args[1], out c)) return new AssembleResult(AssembleError.ArgError, $"line {args.line}: UEXTEND expected register parameter as third arg\n-> {args.res.ErrorMsg}");

                            args.AppendVal(1, a);
                            args.AppendVal(1, (c << 4) | (b << 2) | args.sizecode);

                            break;

                        case "UMUL": if (!args.TryProcessIMMRM(OPCode.UMUL)) return args.res; break;
                        case "SMUL": if (!args.TryProcessIMMRM(OPCode.SMUL)) return args.res; break;
                        case "UDIV": if (!args.TryProcessIMMRM(OPCode.UDIV)) return args.res; break;
                        case "SDIV": if (!args.TryProcessIMMRM(OPCode.SDIV)) return args.res; break;

                        case "ADD": if (!args.TryProcessBinaryOp(OPCode.ADD)) return args.res; break;
                        case "SUB": if (!args.TryProcessBinaryOp(OPCode.SUB)) return args.res; break;
                        case "BMUL": if (!args.TryProcessBinaryOp(OPCode.BMUL)) return args.res; break;
                        case "BUDIV": if (!args.TryProcessBinaryOp(OPCode.BUDIV)) return args.res; break;
                        case "BUMOD": if (!args.TryProcessBinaryOp(OPCode.BUMOD)) return args.res; break;
                        case "BSDIV": if (!args.TryProcessBinaryOp(OPCode.BSDIV)) return args.res; break;
                        case "BSMOD": if (!args.TryProcessBinaryOp(OPCode.BSMOD)) return args.res; break;

                        case "SL": if (!args.TryProcessBinaryOp(OPCode.SL, 0)) return args.res; break;
                        case "SR": if (!args.TryProcessBinaryOp(OPCode.SR, 0)) return args.res; break;
                        case "SAL": if (!args.TryProcessBinaryOp(OPCode.SAL, 0)) return args.res; break;
                        case "SAR": if (!args.TryProcessBinaryOp(OPCode.SAR, 0)) return args.res; break;
                        case "RL": if (!args.TryProcessBinaryOp(OPCode.RL, 0)) return args.res; break;
                        case "RR": if (!args.TryProcessBinaryOp(OPCode.RR, 0)) return args.res; break;

                        case "AND": if (!args.TryProcessBinaryOp(OPCode.AND)) return args.res; break;
                        case "OR": if (!args.TryProcessBinaryOp(OPCode.OR)) return args.res; break;
                        case "XOR": if (!args.TryProcessBinaryOp(OPCode.XOR)) return args.res; break;

                        case "CMP": if (!args.TryProcessBinaryOp(OPCode.CMP)) return args.res; break;
                        case "TEST": if (!args.TryProcessBinaryOp(OPCode.TEST)) return args.res; break;

                        // [8: unary op]   [4: dest][2:][2: size]
                        case "INC": if (!args.TryProcessUnaryOp(OPCode.INC)) return args.res; break;
                        case "DEC": if (!args.TryProcessUnaryOp(OPCode.DEC)) return args.res; break;
                        case "NEG": if (!args.TryProcessUnaryOp(OPCode.NEG)) return args.res; break;
                        case "NOT": if (!args.TryProcessUnaryOp(OPCode.NOT)) return args.res; break;
                        case "ABS": if (!args.TryProcessUnaryOp(OPCode.ABS)) return args.res; break;
                        case "CMPZ": if (!args.TryProcessUnaryOp(OPCode.CMPZ)) return args.res; break;

                        // [8: la]   [4:][4: dest]   [address]
                        case "LA":
                            if (args.args.Length != 2) return new AssembleResult(AssembleError.ArgCount, $"line {args.line}: LA expected 2 args");
                            if (args.sizecode != 3) return new AssembleResult(AssembleError.UsageError, $"line {args.line}: LA does not support the specified size code");

                            if (!args.TryParseRegister(args.args[0], out a)) return new AssembleResult(AssembleError.ArgError, $"line {args.line}: LA expecetd register as first arg\n-> {args.res.ErrorMsg}");
                            if (!args.TryParseAddress(args.args[1], out b, out c, out hole)) return new AssembleResult(AssembleError.ArgError, $"line {args.line}: LA expected address as second arg\n-> {args.res.ErrorMsg}");

                            args.AppendVal(1, (UInt64)OPCode.LA);
                            args.AppendVal(1, a);
                            if (!args.TryAppendAddress(b, c, hole)) return new AssembleResult(AssembleError.ArgError, $"line {args.line}: Failed to append value\n-> {args.res.ErrorMsg}");

                            break;

                        // [8: Jcc]   [address]
                        case "JMP": if (!args.TryProcessJump(OPCode.JMP)) return args.res; break;

                        case "JA": case "JNBE": if (!args.TryProcessJump(OPCode.Ja)) return args.res; break;
                        case "JAE": case "JNB": if (!args.TryProcessJump(OPCode.Jae)) return args.res; break;
                        case "JB": case "JNAE": if (!args.TryProcessJump(OPCode.Jb)) return args.res; break;
                        case "JBE": case "JNA": if (!args.TryProcessJump(OPCode.Jbe)) return args.res; break;

                        case "JG": case "JNLE": if (!args.TryProcessJump(OPCode.Jg)) return args.res; break;
                        case "JGE": case "JNL": if (!args.TryProcessJump(OPCode.Jge)) return args.res; break;
                        case "JL": case "JNGE": if (!args.TryProcessJump(OPCode.Jl)) return args.res; break;
                        case "JLE": case "JNG": if (!args.TryProcessJump(OPCode.Jle)) return args.res; break;

                        case "JZ": case "JE": if (!args.TryProcessJump(OPCode.Jz)) return args.res; break;
                        case "JNZ": case "JNE": if (!args.TryProcessJump(OPCode.Jnz)) return args.res; break;
                        case "JS": if (!args.TryProcessJump(OPCode.Js)) return args.res; break;
                        case "JNS": if (!args.TryProcessJump(OPCode.Jns)) return args.res; break;
                        case "JP": case "JPE": if (!args.TryProcessJump(OPCode.Jp)) return args.res; break;
                        case "JNP": case "JPO": if (!args.TryProcessJump(OPCode.Jnp)) return args.res; break;
                        case "JO": if (!args.TryProcessJump(OPCode.Jo)) return args.res; break;
                        case "JNO": if (!args.TryProcessJump(OPCode.Jno)) return args.res; break;
                        case "JC": if (!args.TryProcessJump(OPCode.Jc)) return args.res; break;
                        case "JNC": if (!args.TryProcessJump(OPCode.Jnc)) return args.res; break;

                        case "FADD": if (!args.TryProcessBinaryOp(OPCode.FADD, -1, 12)) return args.res; break;
                        case "FSUB": if (!args.TryProcessBinaryOp(OPCode.FSUB, -1, 12)) return args.res; break;
                        case "FMUL": if (!args.TryProcessBinaryOp(OPCode.FMUL, -1, 12)) return args.res; break;
                        case "FDIV": if (!args.TryProcessBinaryOp(OPCode.FDIV, -1, 12)) return args.res; break;
                        case "FMOD": if (!args.TryProcessBinaryOp(OPCode.FMOD, -1, 12)) return args.res; break;

                        case "FPOW": if (!args.TryProcessBinaryOp(OPCode.FPOW, -1, 12)) return args.res; break;
                        case "FSQRT": if (!args.TryProcessUnaryOp(OPCode.FSQRT, 12)) return args.res; break;
                        case "FEXP": if (!args.TryProcessUnaryOp(OPCode.FEXP, 12)) return args.res; break;
                        case "FLN": if (!args.TryProcessUnaryOp(OPCode.FLN, 12)) return args.res; break;
                        case "FNEG": if (!args.TryProcessUnaryOp(OPCode.FNEG, 12)) return args.res; break;
                        case "FABS": if (!args.TryProcessUnaryOp(OPCode.FABS, 12)) return args.res; break;
                        case "FCMPZ": if (!args.TryProcessUnaryOp(OPCode.FCMPZ, 12)) return args.res; break;

                        case "FSIN": if (!args.TryProcessUnaryOp(OPCode.FSIN, 12)) return args.res; break;
                        case "FCOS": if (!args.TryProcessUnaryOp(OPCode.FCOS, 12)) return args.res; break;
                        case "FTAN": if (!args.TryProcessUnaryOp(OPCode.FTAN, 12)) return args.res; break;

                        case "FSINH": if (!args.TryProcessUnaryOp(OPCode.FSINH, 12)) return args.res; break;
                        case "FCOSH": if (!args.TryProcessUnaryOp(OPCode.FCOSH, 12)) return args.res; break;
                        case "FTANH": if (!args.TryProcessUnaryOp(OPCode.FTANH, 12)) return args.res; break;

                        case "FASIN": if (!args.TryProcessUnaryOp(OPCode.FASIN, 12)) return args.res; break;
                        case "FACOS": if (!args.TryProcessUnaryOp(OPCode.FACOS, 12)) return args.res; break;
                        case "FATAN": if (!args.TryProcessUnaryOp(OPCode.FATAN, 12)) return args.res; break;
                        case "FATAN2": if (!args.TryProcessBinaryOp(OPCode.FATAN2, -1, 12)) return args.res; break;

                        case "FLOOR": if (!args.TryProcessUnaryOp(OPCode.FLOOR, 12)) return args.res; break;
                        case "CEIL": if (!args.TryProcessUnaryOp(OPCode.CEIL, 12)) return args.res; break;
                        case "ROUND": if (!args.TryProcessUnaryOp(OPCode.ROUND, 12)) return args.res; break;
                        case "TRUNC": if (!args.TryProcessUnaryOp(OPCode.TRUNC, 12)) return args.res; break;

                        case "FCMP": if (!args.TryProcessBinaryOp(OPCode.FCMP, -1, 12)) return args.res; break;

                        case "FTOI": if (!args.TryProcessUnaryOp(OPCode.FTOI, 12)) return args.res; break;
                        case "ITOF": if (!args.TryProcessUnaryOp(OPCode.ITOF, 12)) return args.res; break;

                        case "PUSH":
                            if (args.args.Length != 1) return new AssembleResult(AssembleError.ArgCount, $"line {args.line}: PUSH expected 1 arg");

                            args.AppendVal(1, (UInt64)OPCode.PUSH);

                            if (args.TryParseImm(args.args[0], out hole))
                            {
                                args.AppendVal(1, (args.sizecode << 2) | 0);
                                if (!args.TryAppendExpr(Size(args.sizecode), hole)) return new AssembleResult(AssembleError.ArgError, $"line {args.line}: Failed to append value\n-> {args.res.ErrorMsg}");
                            }
                            else if (args.TryParseRegister(args.args[0], out a))
                            {
                                args.AppendVal(1, (a << 4) | (args.sizecode << 2) | 1);
                            }
                            else return new AssembleResult(AssembleError.FormatError, $"line {args.line}: Couldn't parse \"{args.args[0]}\" as an imm or register");

                            break;
                        case "POP":
                            if (args.args.Length != 1) return new AssembleResult(AssembleError.ArgCount, $"line {args.line}: POP expected 1 arg");

                            if (!args.TryParseRegister(args.args[0], out a)) return new AssembleResult(AssembleError.ArgError, $"line {args.line}: POP expected register as second arg\n-> {args.res.ErrorMsg}");

                            args.AppendVal(1, (UInt64)OPCode.POP);
                            args.AppendVal(1, (a << 4) | (args.sizecode << 2));

                            break;
                        case "CALL":
                            if (args.args.Length != 1) return new AssembleResult(AssembleError.ArgCount, $"line {args.line}: CALL expected 1 arg");
                            if (!args.TryParseAddress(args.args[0], out a, out b, out hole)) return new AssembleResult(AssembleError.ArgError, $"line {args.line}: CALL expected address as first arg\n-> {args.res.ErrorMsg}");

                            args.AppendVal(1, (UInt64)OPCode.CALL);
                            if (!args.TryAppendAddress(a, b, hole)) return new AssembleResult(AssembleError.ArgError, $"line {args.line}: Failed to append value\n-> {args.res.ErrorMsg}");

                            break;
                        case "RET":
                            if (args.args.Length != 0) return new AssembleResult(AssembleError.ArgCount, $"line {args.line}: CALL expected 0 args");

                            args.AppendVal(1, (UInt64)OPCode.RET);

                            break;

                        case "BSWAP": if (!args.TryProcessUnaryOp(OPCode.BSWAP)) return args.res; break;
                        case "BEXTR": if (!args.TryProcessBinaryOp(OPCode.BEXTR, 1)) return args.res; break;
                        case "BLSI": if (!args.TryProcessUnaryOp(OPCode.BLSI)) return args.res; break;
                        case "BLSMSK": if (!args.TryProcessUnaryOp(OPCode.BLSMSK)) return args.res; break;
                        case "BLSR": if (!args.TryProcessUnaryOp(OPCode.BLSR)) return args.res; break;
                        case "ANDN": if (!args.TryProcessBinaryOp(OPCode.ANDN)) return args.res; break;

                        case "GETF": a = (UInt64)OPCode.GETF; goto GETSETF;
                        case "SETF":
                            a = (UInt64)OPCode.SETF;
                            GETSETF:
                            if (args.args.Length != 1) return new AssembleResult(AssembleError.ArgCount, $"line {args.line}: GETF expected one arg");
                            if (!args.TryParseRegister(args.args[0], out b)) return new AssembleResult(AssembleError.UsageError, $"line {args.line}: GETF expected arg one to be a register");
                            if (args.sizecode != 3) return new AssembleResult(AssembleError.UsageError, $"line {args.line}: GETF does not support the specified size code");

                            args.AppendVal(1, a);
                            args.AppendVal(1, b);

                            break;

                        case "LOOP": // loop reg, address, (step = 1)
                            // 2 args default step
                            if (args.args.Length == 2) a = 0;
                            // 3 args explicit step
                            else if (args.args.Length == 3)
                            {
                                if (!args.TryParseInstantImm(args.args[2], out a, out floating))
                                    return new AssembleResult(AssembleError.ArgError, $"line {args.line}: LOOP third argument (explicit step) expected an instant imm");
                                if (floating) return new AssembleResult(AssembleError.ArgError, $"line {args.line}: LOOP third argument (explicit step) may not be floating-point");

                                switch (a)
                                {
                                    case 1: a = 0; break;
                                    case 2: a = 1; break;
                                    case 4: a = 2; break;
                                    case 8: a = 3; break;

                                    default: return new AssembleResult(AssembleError.ArgError, $"line {args.line}: LOOP third argument (explicit step) must be 1, 2, 4, or 8");
                                }
                            }
                            else return new AssembleResult(AssembleError.ArgCount, $"line {args.line}: LOOP expected two args (3 for explicit step)");

                            if (!args.TryParseRegister(args.args[0], out b)) return new AssembleResult(AssembleError.UsageError, $"line {args.line}: LOOP expected register as first arg");
                            if (!args.TryParseAddress(args.args[1], out c, out d, out hole)) return new AssembleResult(AssembleError.UsageError, $"line {args.line}: LOOP expected an address as second arg");

                            args.AppendVal(1, (UInt64)OPCode.LOOP);
                            args.AppendVal(1, (b << 4) | (args.sizecode << 2) | a);
                            if (!args.TryAppendAddress(c, d, hole)) return args.res;

                            break;

                        case "FX":
                            if (args.args.Length != 2) return new AssembleResult(AssembleError.ArgCount, $"line {args.line}: XEXTEND expected 2 args");

                            if (!args.TryParseSizecode(args.args[0], out a)) return new AssembleResult(AssembleError.MissingSize, $"line {args.line}: UEXTEND expected size parameter as second arg\n-> {args.res.ErrorMsg}");
                            if (!args.TryParseRegister(args.args[1], out b)) return new AssembleResult(AssembleError.ArgError, $"line {args.line}: UEXTEND expected register parameter as third arg\n-> {args.res.ErrorMsg}");

                            args.AppendVal(1, (UInt64)OPCode.FX);
                            args.AppendVal(1, (b << 4) | (a << 2) | args.sizecode);

                            break;

                        case "SLP": if (!args.TryProcessIMMRM(OPCode.SLP)) return args.res; break;

                        default: return new AssembleResult(AssembleError.UnknownOp, $"line {args.line}: Unknown operation \"{args.op}\"");
                    }
                }

                // advance to after the new line
                pos = end + 1;
            }

            // -- minimize symbols and holes -- //

            // link each symbol to internal symbols (minimizes file size)
            foreach (var entry in file.Symbols) entry.Value.Evaluate(file.Symbols, out a, out floating, ref err);
            // eliminate as many holes as possible
            for (int i = file.Holes.Count - 1; i >= 0; --i)
            {
                switch (TryPatchHole(file.Data, file.Symbols, file.Holes[i], ref err))
                {
                    case PatchError.None: file.Holes.RemoveAt(i); break; // remove the hole if we solved it
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
            // for each symbol we can eliminate
            foreach (string elim in elim_symbols)
            {
                file.Symbols.Remove(elim);
            }

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

            // resulting binary (we don't know how large the resulting file will be, so it needs to be expandable) (sets aside space for header)
            List<byte> data = new List<byte>() { (byte)OPCode.CALL, 0x80, 0, 0, 0, 0, 0, 0, 0, 0, (byte)OPCode.STOP };

            // a table for relating global symbols to their object file
            var global_to_obj = new Dictionary<string, ObjectFile>();

            // the queue of object files that need to be added to the executable
            var include_queue = new Queue<ObjectFile>();
            // a table for relating included object files to their beginning position in the resulting binary
            var included = new HashSet<ObjectFile>();

            // parsing locations for evaluation
            UInt64 _res;
            bool _floating;
            string _err = string.Empty;

            // expressions for pre-defined symbols
            Expr __prog_end__ = new Expr();

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
                if (obj.Symbols.ContainsKey("__prog_end__")) return new LinkResult(LinkError.SymbolRedefinition, "Object file defined symbol with name \"__prog_end__\" (reserved)");
            }

            // ensure we got a "main" global symbol
            if (!global_to_obj.TryGetValue("main", out ObjectFile main_obj)) return new LinkResult(LinkError.MissingSymbol, "No entry point");

            // make sure we start the merge process with the main object file
            include_queue.Enqueue(main_obj);

            // add a hole for the call location of the header
            main_obj.Holes.Add(new HoleData() { Address = 2 - (UInt32)data.Count, Size = 8, Expr = new Expr() { Token = "main" }, Line = -1 });
            // this makes main_obj dirty
            main_obj.Clean = false;

            // -- merge things -- //

            // while there are still things in queue
            while (include_queue.Count > 0)
            {
                // get the object file we need to incorporate
                ObjectFile obj = include_queue.Dequeue();
                // add it to the set of included files
                included.Add(obj);

                // get this file's #base offset
                UInt32 offset = (UInt32)data.Count;

                // offset each hole by #base offset
                foreach (HoleData hole in obj.Holes) hole.Address += offset;
                // define a local symbol for #base
                obj.Symbols.Add("#base", new Expr() { IntResult = offset });
                // this makes obj dirty
                obj.Clean = false;

                // append all its data
                for (int i = 0; i < obj.Data.Count; ++i)
                    data.Add(obj.Data[i]);

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
                    // if this is a global symbol somewhere
                    if (global_to_obj.TryGetValue(external, out ObjectFile global_source))
                    {
                        // if the source isn't already included and it isn't already in queue to be included
                        if (!included.Contains(global_source) && !include_queue.Contains(global_source))
                        {
                            // add it to the queue
                            include_queue.Enqueue(global_source);
                        }

                        // add externals to local scope //

                        // if obj already has a symbol of the same name
                        if (obj.Symbols.ContainsKey(external)) return new LinkResult(LinkError.SymbolRedefinition, $"Object file defined external symbol \"{external}\"");
                        // otherwise define it as a local in obj
                        else obj.Symbols.Add(external, global_source.Symbols[external]);
                    }
                    // otherwise it wasn't defined
                    else return new LinkResult(LinkError.MissingSymbol, $"No global symbol found to match external symbol \"{external}\"");
                }

                // inject pre-defined linker symbols
                obj.Symbols.Add("__prog_end__", __prog_end__);
            }

            // now we can define __prog_end__
            __prog_end__.IntResult = (UInt64)data.Count;

            // -- patch things -- //

            // for each object file
            foreach (ObjectFile obj in included)
            {
                // patch all the holes
                foreach (HoleData hole in obj.Holes)
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

            // copy data into out parameter
            exe = new byte[data.Count];
            data.CopyTo(exe);

            // linked successfully
            return new LinkResult(LinkError.None, string.Empty);
        }
    }
}