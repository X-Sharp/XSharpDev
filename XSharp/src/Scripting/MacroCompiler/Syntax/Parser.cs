﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XSharp.MacroCompiler
{
    using Syntax;

    partial class Parser
    {
        // Input source
        IList<Token> _Input;

        // State
        int _index = 0;

        internal Parser(IList<Token> input)
        {
            _Input = input;
        }

        TokenType La()
        {
            return _index < _Input.Count ? _Input[_index].type : TokenType.EOF;
        }

        TokenType La(int n)
        {
            return (_index + n - 1) < _Input.Count ? _Input[_index + n - 1].type : TokenType.EOF;
        }

        bool Eoi()
        {
            return _index >= _Input.Count;
        }

        void Consume()
        {
            _index++;
        }

        void Consume(int n)
        {
            _index += n;
        }

        int Mark()
        {
            return _index;
        }

        void Rewind(int pos)
        {
            _index = pos;
        }

        Token ConsumeAndGet()
        {
            var t = _Input[_index];
            _index++;
            return t;
        }

        bool Expect(TokenType c)
        {
            if (La() == c)
            {
                Consume();
                return true;
            }
            return false;
        }

        bool Require(bool p, string error)
        {
            if (!p)
            {
                throw new Exception(error);
            }
            return p;
        }

        T Require<T>(T n, string error)
        {
            if (n == null)
            {
                throw new Exception(error);
            }
            return n;
        }

        internal bool CanParseTerm()
        {
            switch (La())
            {
                case TokenType.ID:
                case TokenType.SELF:
                case TokenType.SUPER:
                case TokenType.CHECKED:
                case TokenType.UNCHECKED:
                case TokenType.TYPEOF:
                case TokenType.SIZEOF:
                case TokenType.DEFAULT:
                case TokenType.TRUE_CONST:
                case TokenType.FALSE_CONST:
                case TokenType.CHAR_CONST:
                case TokenType.STRING_CONST:
                case TokenType.ESCAPED_STRING_CONST:
                case TokenType.INTERPOLATED_STRING_CONST:
                case TokenType.SYMBOL_CONST:
                case TokenType.HEX_CONST:
                case TokenType.BIN_CONST:
                case TokenType.REAL_CONST:
                case TokenType.INT_CONST:
                case TokenType.DATE_CONST:
                case TokenType.NIL:
                case TokenType.NULL:
                case TokenType.NULL_ARRAY:
                case TokenType.NULL_CODEBLOCK:
                case TokenType.NULL_DATE:
                case TokenType.NULL_OBJECT:
                case TokenType.NULL_PSZ:
                case TokenType.NULL_PTR:
                case TokenType.NULL_STRING:
                case TokenType.NULL_SYMBOL:
                case TokenType.INCOMPLETE_STRING_CONST:
                case TokenType.ARRAY:
                case TokenType.CODEBLOCK:
                case TokenType.DATE:
                case TokenType.FLOAT:
                case TokenType.PSZ:
                case TokenType.SYMBOL:
                case TokenType.USUAL:
                case TokenType.BYTE:
                case TokenType.CHAR:
                case TokenType.DATETIME:
                case TokenType.DECIMAL:
                case TokenType.DWORD:
                case TokenType.DYNAMIC:
                case TokenType.INT:
                case TokenType.INT64:
                case TokenType.LOGIC:
                case TokenType.LONGINT:
                case TokenType.OBJECT:
                case TokenType.PTR:
                case TokenType.REAL4:
                case TokenType.REAL8:
                case TokenType.SHORTINT:
                case TokenType.STRING:
                case TokenType.UINT64:
                case TokenType.VOID:
                case TokenType.WORD:
                case TokenType.LPAREN:
                case TokenType.LCURLY:
                    return true;
                case TokenType.LT:
                    {
                        var p = Mark();
                        var la = ParseTypedLiteralArray();
                        Rewind(p);
                        return la != null;
                    }
                default:
                    return TokenAttr.IsSoftKeyword(La());
            }
        }

        internal Expr ParseTerm()
        {
            var t = La();
            switch (t)
            {
                case TokenType.ID:
                    return ParseNameOrCtorCall(ParseTypeSuffix(ParseQualifiedName()));
                case TokenType.SELF:
                    Consume();
                    return new SelfExpr();
                case TokenType.SUPER:
                    Consume();
                    return new SuperExpr();
                case TokenType.CHECKED:
                    Consume();
                    return new CheckedExpr(ParseParenExpr());
                case TokenType.UNCHECKED:
                    Consume();
                    return new UncheckedExpr(ParseParenExpr());
                case TokenType.TYPEOF:
                    Consume();
                    return new TypeOfExpr(ParseParenType());
                case TokenType.SIZEOF:
                    Consume();
                    return new SizeOfExpr(ParseParenType());
                case TokenType.DEFAULT:
                    Consume();
                    return new DefaultExpr(ParseParenType());
                case TokenType.TRUE_CONST:
                case TokenType.FALSE_CONST:
                case TokenType.CHAR_CONST:
                case TokenType.STRING_CONST:
                case TokenType.ESCAPED_STRING_CONST:
                case TokenType.INTERPOLATED_STRING_CONST:
                case TokenType.SYMBOL_CONST:
                case TokenType.HEX_CONST:
                case TokenType.BIN_CONST:
                case TokenType.REAL_CONST:
                case TokenType.INT_CONST:
                case TokenType.DATE_CONST:
                case TokenType.NIL:
                case TokenType.NULL:
                case TokenType.NULL_ARRAY:
                case TokenType.NULL_CODEBLOCK:
                case TokenType.NULL_DATE:
                case TokenType.NULL_OBJECT:
                case TokenType.NULL_PSZ:
                case TokenType.NULL_PTR:
                case TokenType.NULL_STRING:
                case TokenType.NULL_SYMBOL:
                    return new LiteralExpr(t, ConsumeAndGet().value);
                case TokenType.INCOMPLETE_STRING_CONST:
                    throw new Exception("Unterminated string");
                case TokenType.ARRAY:
                case TokenType.CODEBLOCK:
                case TokenType.DATE:
                case TokenType.FLOAT:
                case TokenType.PSZ:
                case TokenType.SYMBOL:
                case TokenType.USUAL:
                    return ParseNativeTypeOrCast(new NativeTypeExpr(ConsumeAndGet().type));
                case TokenType.BYTE:
                case TokenType.CHAR:
                case TokenType.DATETIME:
                case TokenType.DECIMAL:
                case TokenType.DWORD:
                case TokenType.DYNAMIC:
                case TokenType.INT:
                case TokenType.INT64:
                case TokenType.LOGIC:
                case TokenType.LONGINT:
                case TokenType.OBJECT:
                case TokenType.PTR:
                case TokenType.REAL4:
                case TokenType.REAL8:
                case TokenType.SHORTINT:
                case TokenType.STRING:
                case TokenType.UINT64:
                case TokenType.VOID:
                case TokenType.WORD:
                    return ParseNativeTypeOrCast(new NativeTypeExpr(ConsumeAndGet().type));
                case TokenType.LPAREN:
                    return ParseParenExpr();
                case TokenType.LCURLY:
                    return ParseLiteralArray();
                case TokenType.LT:
                    return ParseTypedLiteralArray();
                // TODO nvk: PTR LPAREN Type=datatype COMMA Expr=expression RPAREN		#voCastPtrExpression	// PTR( typeName, expr )
                // TODO nvk: Expr=iif													#iifExpression			// iif( expr, expr, expr )
                // TODO nvk: Op=(VO_AND | VO_OR | VO_XOR | VO_NOT) LPAREN Exprs+=expression (COMMA Exprs+=expression)* RPAREN							#intrinsicExpression	// _Or(expr, expr, expr)
                // TODO nvk: FIELD_ ALIAS (Alias=identifier ALIAS)? Field=identifier   #aliasedField		    // _FIELD->CUSTOMER->NAME is equal to CUSTOMER->NAME
                // TODO nvk: {InputStream.La(4) != LPAREN}?                            // this makes sure that CUSTOMER->NAME() is not matched
                //               Alias=identifier ALIAS Field=identifier               #aliasedField		    // CUSTOMER->NAME
                // TODO nvk: Id=identifier ALIAS Expr=expression                       #aliasedExpr            // id -> expr
                // TODO nvk: LPAREN Alias=expression RPAREN ALIAS Expr=expression		#aliasedExpr            // (expr) -> expr
                // TODO nvk: AMP LPAREN Expr=expression RPAREN							#macro					// &( expr )
                // TODO nvk: AMP Id=identifierName										#macro					// &id
                // TODO nvk: Key=ARGLIST												#argListExpression		// __ARGLIST
                default:
                    if (TokenAttr.IsSoftKeyword(La()))
                        return ParseNameOrCtorCall(ParseTypeSuffix(ParseQualifiedName()));
                    return null;
            }
        }

        internal TypeExpr ParseType()
        {
            var t = La();
            switch (t)
            {
                case TokenType.ID:
                    return ParseTypeSuffix(ParseQualifiedName());
                case TokenType.ARRAY:
                case TokenType.CODEBLOCK:
                case TokenType.DATE:
                case TokenType.FLOAT:
                case TokenType.PSZ:
                case TokenType.SYMBOL:
                case TokenType.USUAL:
                    return ParseTypeSuffix(new NativeTypeExpr(ConsumeAndGet().type));
                case TokenType.BYTE:
                case TokenType.CHAR:
                case TokenType.DATETIME:
                case TokenType.DECIMAL:
                case TokenType.DWORD:
                case TokenType.DYNAMIC:
                case TokenType.INT:
                case TokenType.INT64:
                case TokenType.LOGIC:
                case TokenType.LONGINT:
                case TokenType.OBJECT:
                case TokenType.PTR:
                case TokenType.REAL4:
                case TokenType.REAL8:
                case TokenType.SHORTINT:
                case TokenType.STRING:
                case TokenType.UINT64:
                case TokenType.VOID:
                case TokenType.WORD:
                    return ParseTypeSuffix(new NativeTypeExpr(ConsumeAndGet().type));
                default:
                    if (TokenAttr.IsSoftKeyword(La()))
                        return ParseTypeSuffix(ParseQualifiedName());
                    return null;
            }
        }

        internal IdExpr ParseId()
        {
            if (La() == TokenType.ID || TokenAttr.IsSoftKeyword(La()))
                return new IdExpr(ConsumeAndGet().value);
            return null;
        }

        internal Expr ParseNativeTypeOrCast(NativeTypeExpr nt)
        {
            var e = ParseNameOrCtorCall(ParseTypeSuffix(nt));

            if ((e as NativeTypeExpr) == nt && La() == TokenType.LPAREN)
            {
                bool cast = false;

                Require(Expect(TokenType.LPAREN), "Expected '('");

                if (Expect(TokenType.CAST))
                {
                    Require(Expect(TokenType.COMMA), "Expected ','");
                    cast = true;
                }

                var expr = ParseExpression();

                Require(Expect(TokenType.RPAREN), "Expected ')'");

                return cast ? new TypeCast((TypeExpr)e, expr) : new TypeConversion((TypeExpr)e, expr);
            }

            return e;
        }

        internal NameExpr ParseName()
        {
            NameExpr n = ParseId();

            if (n != null)
            {
                // TODO nvk: Parse generic arguments
            }

            return n;
        }

        internal TypeExpr ParseTypeSuffix(TypeExpr t)
        {
            if (t != null)
            {
                // TODO nvk: parse PTR, array specifiers
            }

            return t;
        }

        internal NameExpr ParseQualifiedName()
        {
            NameExpr n = ParseName();
            while (La() == TokenType.DOT && (La(2) == TokenType.ID || TokenAttr.IsSoftKeyword(La(2))))
            {
                Consume();
                n = new QualifiedNameExpr(n, ParseName());
            }
            return n;
        }

        internal Expr ParseNameOrCtorCall(TypeExpr t)
        {
            if (t != null && La() == TokenType.LCURLY)
            {
                var args = ParseCurlyArgList();

                // TODO nvk: Parse property initializers { name := expr }

                return new CtorCallExpr(t, args);
            }

            return t;
        }

        Oper ParseOper(out Node n)
        {
            return Opers[(int)La()].Parse(this, out n);
        }

        Oper ParsePrefixOper(out Node n)
        {
            return PrefixOpers[(int)La()].Parse(this, out n);
        }

        internal Expr ParseExpression()
        {
            var exprs = new Stack<Tuple<Expr, Oper, Node>>();

            Expr e;

            do
            {
                Node n;
                Oper op;

                bool mayBeCast = La() == TokenType.LPAREN;

                e = ParseTerm();
                op = e == null ? ParsePrefixOper(out n) : ParseOper(out n);

                if (op == null && mayBeCast && e is TypeExpr && CanParseTerm())
                {
                    n = e;
                    e = null;
                    op = Opers[(int)TokenType.TYPECAST];
                }

                do
                {
                    var at = op?.assoc ?? AssocType.None;
                    if (e == null && op != null && at != AssocType.Prefix)
                        throw new Exception("Expr expected");
                    if (e != null && at == AssocType.Prefix)
                        throw new Exception("Unexpected prefix operator");
                    while (exprs.Count > 0 && e != null && (op == null || exprs.Peek().Item2 < op))
                    {
                        var s = exprs.Pop();
                        e = s.Item2.Combine(s.Item1, s.Item3, e);
                    }
                    if (at == AssocType.Postfix)
                    {
                        e = op.Combine(e,n,null);
                        op = ParseOper(out n);
                    }
                } while (op?.assoc == AssocType.Postfix);

                if (op == null)
                    break;

                exprs.Push(new Tuple<Expr, Oper, Node>(e, op, n));
            } while (true);

            return e;
        }

        internal ExprList ParseExprList()
        {
            IList<Expr> l = new List<Expr>();

            var e = ParseExpression();
            while (Expect(TokenType.COMMA))
            {
                l.Add(e);
                e = ParseExpression();
            }
            if (e != null)
                l.Add(e);

            return new ExprList(l);
        }

        internal Codeblock ParseCodeblock()
        {
            Require(Expect(TokenType.LCURLY), "Expected '{'");

            List<IdExpr> p = null;

            if (Expect(TokenType.PIPE))
            {
                var a = ParseId();
                if (a != null)
                {
                    p = new List<IdExpr>();
                    do
                    {
                        p.Add(a);
                        if (!Expect(TokenType.COMMA))
                            break;
                        a = Require(ParseId(), "Expected identifier");
                    } while (true);
                }

                Require(Expect(TokenType.PIPE), "Expected '|'");
            }
            else Require(Expect(TokenType.OR), "Expected '|'");

            var l = ParseExprList();

            Require(Expect(TokenType.RCURLY), "Expected '}'");

            return new Codeblock(p,l);
        }

        internal Codeblock ParseMacro()
        {
            var p = new List<IdExpr>();
            if (La() == TokenType.LCURLY && (La(2) == TokenType.PIPE || La(2) == TokenType.OR))
                return ParseCodeblock();

            var l = ParseExprList();
            if (l != null)
                return new Codeblock(null,l);

            return null;
        }

        internal TypeExpr ParseParenType()
        {
            Require(Expect(TokenType.LPAREN), "Expected '('");

            var t = ParseType();

            Require(Expect(TokenType.RPAREN), "Expected ')'");

            return t;
        }

        internal Expr ParseParenExpr()
        {
            Require(Expect(TokenType.LPAREN), "Expected '('");

            var e = ParseExpression();

            Require(Expect(TokenType.RPAREN), "Expected ')'");

            return e;
        }

        internal Expr ParseLiteralArray(TypeExpr t = null)
        {
            Require(Expect(TokenType.LCURLY), "Expected '{'");

            var e = ParseExprList();

            Require(Expect(TokenType.RCURLY), "Expected '}'");

            return new LiteralArray(e);
        }

        internal Expr ParseTypedLiteralArray()
        {
            var p = Mark();

            TypeExpr t = null;

            if (Expect(TokenType.LT))
            {
                t = ParseType();

                if (!Expect(TokenType.GT))
                    t = null;
            }

            if (t != null && La() == TokenType.LCURLY)
            {
                var la = ParseLiteralArray(t);

                return la;
            }

            Rewind(p);

            return null;
        }

        internal Arg ParseArg()
        {
            var e = ParseExpression();
            if (e != null)
                return new Arg(e);
            return null;
        }

        internal ArgList ParseArgList()
        {
            IList<Arg> l = new List<Arg>();

            var a = ParseArg();
            while (Expect(TokenType.COMMA))
            {
                l.Add(a);
                a = ParseArg();
            }
            if (a != null)
                l.Add(a);

            return new ArgList(l);
        }

        internal ArgList ParseParenArgList()
        {
            Require(Expect(TokenType.LPAREN), "Expected '('");

            var l = ParseArgList();

            Require(Expect(TokenType.RPAREN), "Expected ')'");

            return l;
        }

        internal ArgList ParseBrktArgList()
        {
            Require(Expect(TokenType.LBRKT), "Expected '['");

            var l = ParseArgList();

            Require(Expect(TokenType.RBRKT), "Expected ']'");

            return l;
        }

        internal ArgList ParseCurlyArgList()
        {
            Require(Expect(TokenType.LCURLY), "Expected '{'");

            var l = ParseArgList();

            Require(Expect(TokenType.RCURLY), "Expected '}'");

            return l;
        }

        enum AssocType
        {
            None,
            BinaryLeft,
            BinaryRight,
            Prefix,
            Postfix,
        }

        class Oper
        {
            internal static readonly Oper Empty = new Oper(AssocType.None, TokenType.UNRECOGNIZED, int.MaxValue);
            internal delegate Oper ParseDelegate(Parser p, out Node n);
            internal delegate Expr CombineDelegate(Expr l, Node o, Expr r);
            internal readonly AssocType assoc;
            internal readonly TokenType type;
            internal readonly int level;
            internal readonly ParseDelegate Parse;
            internal readonly CombineDelegate Combine;
            internal Oper(AssocType assoc, TokenType type, int level)
            {
                this.assoc = assoc;
                this.type = type;
                this.level = level;
                switch (assoc)
                {
                    case AssocType.BinaryLeft:
                    case AssocType.BinaryRight:
                        Parse = _parse;
                        Combine = _combine_binary;
                        break;
                    case AssocType.Postfix:
                        Parse = _parse;
                        Combine = _combine_postfix;
                        break;
                    case AssocType.Prefix:
                        Parse = _parse;
                        Combine = _combine_prefix;
                        break;
                    case AssocType.None:
                        Parse = _parse_empty;
                        Combine = null;
                        break;
                }
            }
            internal Oper(AssocType assoc, TokenType type, int level, ParseDelegate parseFunc = null, CombineDelegate combineFunc = null) : this(assoc, type, level)
            {
                if (parseFunc != null) Parse = parseFunc;
                if (combineFunc != null) Combine = combineFunc;
            }
            Oper _parse(Parser p, out Node n)
            {
                n = null;
                if (assoc != AssocType.None)
                {
                    p.Consume();
                    return this;
                }
                return null;
            }
            Oper _parse_empty(Parser p, out Node n)
            {
                n = null;
                return null;
            }
            Expr _combine_prefix(Expr l, Node o, Expr r)
            {
                return new PrefixExpr(r, type);
            }
            Expr _combine_postfix(Expr l, Node o, Expr r)
            {
                return new PostfixExpr(l, type);
            }
            Expr _combine_prefix_or_postfix(Expr l, Node o, Expr r)
            {
                return l == null ? (Expr)new PrefixExpr(r, type) : new PostfixExpr(l, type);
            }
            Expr _combine_binary(Expr l, Node o, Expr r)
            {
                return new BinaryExpr(l, type, r);
            }
            Expr _combine_binary_or_prefix(Expr l, Node o, Expr r)
            {
                return l == null ? (Expr)new PrefixExpr(r, type) : new BinaryExpr(l, type, r);
            }
            public static bool operator <(Oper a, Oper b)
            {
                return a.level < b.level || (a.level == b.level && a.assoc != AssocType.BinaryRight);
            }
            public static bool operator >(Oper a, Oper b)
            {
                return a.level > b.level || (a.level == b.level && a.assoc == AssocType.BinaryRight);
            }
        }

        static readonly Oper[] Opers;
        static readonly Oper[] PrefixOpers;

        static Parser()
        {
            Opers = new Oper[(int)TokenType.LAST];
            PrefixOpers = new Oper[(int)TokenType.LAST];

            for (var i = 0; i < Opers.Length; i++)
                Opers[i] = Oper.Empty;

            Opers[(int)TokenType.DOT] = new Oper(AssocType.Postfix, TokenType.DOT, 1,
                (Parser p, out Node nn) => { p.Consume(); nn = p.Require(p.ParseName(),"Name expected"); return Opers[(int)TokenType.DOT]; },
                (l, o, r) => { if (!(l is NameExpr)) throw new Exception("Name required"); return new QualifiedNameExpr((NameExpr)l, (NameExpr)o); });
            Opers[(int)TokenType.COLON] = new Oper(AssocType.Postfix, TokenType.COLON, 1,
                (Parser p, out Node nn) => { p.Consume(); nn = p.Require(p.ParseName(), "Name expected"); return Opers[(int)TokenType.COLON]; },
                (l, o, r) => { return new MemberAccessExpr(l, (NameExpr)o); });

            Opers[(int)TokenType.LPAREN] = new Oper(AssocType.Postfix, TokenType.LPAREN, 2,
                (Parser p, out Node nn) => { nn = p.ParseParenArgList(); return Opers[(int)TokenType.LPAREN]; },
                (l, o, r) => { return new MethodCallExpr(l, (ArgList)o); });
            Opers[(int)TokenType.LBRKT] = new Oper(AssocType.Postfix, TokenType.LBRKT, 2,
                (Parser p, out Node nn) => { nn = p.ParseBrktArgList(); return Opers[(int)TokenType.LBRKT]; },
                (l, o, r) => { return new ArrayAccessExpr(l, (ArgList)o); });

            Opers[(int)TokenType.QMARK] = new Oper(AssocType.Postfix, TokenType.QMARK, 3);

            Opers[(int)TokenType.TYPECAST] = new Oper(AssocType.Prefix, TokenType.TYPECAST, 4, null,
                (l, o, r) => { return new TypeCast((TypeExpr)o, r); });

            Opers[(int)TokenType.INC] = new Oper(AssocType.Postfix, TokenType.INC, 5);
            Opers[(int)TokenType.DEC] = new Oper(AssocType.Postfix, TokenType.DEC, 5);

            PrefixOpers[(int)TokenType.AWAIT] = new Oper(AssocType.Prefix, TokenType.AWAIT, 6);
            PrefixOpers[(int)TokenType.INC] = new Oper(AssocType.Prefix, TokenType.INC, 6);
            PrefixOpers[(int)TokenType.DEC] = new Oper(AssocType.Prefix, TokenType.DEC, 6);
            PrefixOpers[(int)TokenType.PLUS] = new Oper(AssocType.Prefix, TokenType.PLUS, 6);
            PrefixOpers[(int)TokenType.MINUS] = new Oper(AssocType.Prefix, TokenType.MINUS, 6);
            PrefixOpers[(int)TokenType.TILDE] = new Oper(AssocType.Prefix, TokenType.TILDE, 6);
            PrefixOpers[(int)TokenType.ADDROF] = new Oper(AssocType.Prefix, TokenType.ADDROF, 6);

            Opers[(int)TokenType.IS] = new Oper(AssocType.Postfix, TokenType.IS, 7,
                (Parser p, out Node nn) => { p.Consume(); nn = p.ParseQualifiedName(); return Opers[(int)TokenType.IS]; },
                (l, o, r) => { return new IsExpr(l,  (TypeExpr)o); });

            Opers[(int)TokenType.ASTYPE] = new Oper(AssocType.Postfix, TokenType.AS, 8,
                (Parser p, out Node nn) => { p.Consume(); nn = p.ParseQualifiedName(); return Opers[(int)TokenType.ASTYPE]; },
                (l, o, r) => { return new IsExpr(l, (TypeExpr)o); });

            Opers[(int)TokenType.EXP] = new Oper(AssocType.BinaryLeft, TokenType.EXP, 9);

            Opers[(int)TokenType.MULT] = new Oper(AssocType.BinaryLeft, TokenType.MULT, 10);
            Opers[(int)TokenType.DIV] = new Oper(AssocType.BinaryLeft, TokenType.DIV, 10);
            Opers[(int)TokenType.MOD] = new Oper(AssocType.BinaryLeft, TokenType.MOD, 10);

            Opers[(int)TokenType.PLUS] = new Oper(AssocType.BinaryLeft, TokenType.PLUS,11);
            Opers[(int)TokenType.MINUS] = new Oper(AssocType.BinaryLeft, TokenType.MINUS, 11);

            Opers[(int)TokenType.LSHIFT] = new Oper(AssocType.BinaryLeft, TokenType.LSHIFT, 12);
            Opers[(int)TokenType.RSHIFT] = new Oper(AssocType.BinaryLeft, TokenType.RSHIFT, 12);

            Opers[(int)TokenType.GT] = new Oper(AssocType.BinaryLeft, TokenType.GT, 13,
                (Parser p, out Node nn) => 
                    { nn = null; p.Consume(); return p.Expect(TokenType.GT) ? Opers[(int)TokenType.RSHIFT] : Opers[(int)TokenType.GT]; });
            Opers[(int)TokenType.LT] = new Oper(AssocType.BinaryLeft, TokenType.LT, 13,
                (Parser p, out Node nn) =>
                { nn = null; p.Consume(); return p.Expect(TokenType.LT) ? Opers[(int)TokenType.LSHIFT] : Opers[(int)TokenType.LT]; });
            Opers[(int)TokenType.GTE] = new Oper(AssocType.BinaryLeft, TokenType.GTE, 13);
            Opers[(int)TokenType.LTE] = new Oper(AssocType.BinaryLeft, TokenType.LTE, 13);
            Opers[(int)TokenType.EQ] = new Oper(AssocType.BinaryLeft, TokenType.EQ, 13);
            Opers[(int)TokenType.EEQ] = new Oper(AssocType.BinaryLeft, TokenType.EEQ, 13);
            Opers[(int)TokenType.SUBSTR] = new Oper(AssocType.BinaryLeft, TokenType.SUBSTR, 13);
            Opers[(int)TokenType.NEQ] = new Oper(AssocType.BinaryLeft, TokenType.NEQ, 13);
            Opers[(int)TokenType.NEQ2] = new Oper(AssocType.BinaryLeft, TokenType.NEQ2, 13);

            Opers[(int)TokenType.AMP] = new Oper(AssocType.BinaryLeft, TokenType.AMP, 14);

            Opers[(int)TokenType.TILDE] = new Oper(AssocType.BinaryLeft, TokenType.TILDE, 15);

            Opers[(int)TokenType.PIPE] = new Oper(AssocType.BinaryLeft, TokenType.PIPE, 16);

            Opers[(int)TokenType.NOT] = new Oper(AssocType.Prefix, TokenType.NOT, 17);
            Opers[(int)TokenType.LOGIC_NOT] = new Oper(AssocType.Prefix, TokenType.LOGIC_NOT, 17);

            Opers[(int)TokenType.AND] = new Oper(AssocType.BinaryLeft, TokenType.AND, 18);
            Opers[(int)TokenType.LOGIC_AND] = new Oper(AssocType.BinaryLeft, TokenType.LOGIC_AND, 18);

            Opers[(int)TokenType.LOGIC_XOR] = new Oper(AssocType.BinaryLeft, TokenType.LOGIC_XOR, 19);

            Opers[(int)TokenType.OR] = new Oper(AssocType.BinaryLeft, TokenType.OR, 20);
            Opers[(int)TokenType.LOGIC_OR] = new Oper(AssocType.BinaryLeft, TokenType.LOGIC_OR, 20);

            Opers[(int)TokenType.DEFAULT] = new Oper(AssocType.BinaryLeft, TokenType.DEFAULT, 21);

            Opers[(int)TokenType.ASSIGN_OP] = new Oper(AssocType.BinaryRight, TokenType.ASSIGN_OP, 22);
            Opers[(int)TokenType.ASSIGN_ADD] = new Oper(AssocType.BinaryRight, TokenType.ASSIGN_ADD, 22);
            Opers[(int)TokenType.ASSIGN_SUB] = new Oper(AssocType.BinaryRight, TokenType.ASSIGN_SUB, 22);
            Opers[(int)TokenType.ASSIGN_EXP] = new Oper(AssocType.BinaryRight, TokenType.ASSIGN_EXP, 22);
            Opers[(int)TokenType.ASSIGN_MUL] = new Oper(AssocType.BinaryRight, TokenType.ASSIGN_MUL, 22);
            Opers[(int)TokenType.ASSIGN_DIV] = new Oper(AssocType.BinaryRight, TokenType.ASSIGN_DIV, 22);
            Opers[(int)TokenType.ASSIGN_MOD] = new Oper(AssocType.BinaryRight, TokenType.ASSIGN_MOD, 22);
            Opers[(int)TokenType.ASSIGN_BITAND] = new Oper(AssocType.BinaryRight, TokenType.ASSIGN_BITAND, 22);
            Opers[(int)TokenType.ASSIGN_BITOR] = new Oper(AssocType.BinaryRight, TokenType.ASSIGN_BITOR, 22);
            Opers[(int)TokenType.ASSIGN_LSHIFT] = new Oper(AssocType.BinaryRight, TokenType.ASSIGN_LSHIFT, 22);
            Opers[(int)TokenType.ASSIGN_RSHIFT] = new Oper(AssocType.BinaryRight, TokenType.ASSIGN_RSHIFT, 22);
            Opers[(int)TokenType.ASSIGN_XOR] = new Oper(AssocType.BinaryRight, TokenType.ASSIGN_XOR, 22);
        }
    }
}