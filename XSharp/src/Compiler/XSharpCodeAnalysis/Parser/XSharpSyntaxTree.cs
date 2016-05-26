﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Utilities;
using InternalSyntax = Microsoft.CodeAnalysis.CSharp.Syntax.InternalSyntax;
using Antlr4.Runtime;
using Antlr4.Runtime.Atn;
using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Tree;
using LanguageService.CodeAnalysis.XSharp.SyntaxParser;

namespace LanguageService.CodeAnalysis.XSharp.SyntaxParser
{
    public partial class XSharpParser
    {
        //internal ParseTreeTypedProperty Errors = new ParseTreeTypedProperty();

        //internal ParseTreeTypedProperty CsNodes = new ParseTreeTypedProperty();
    }
}

namespace Antlr4.Runtime
{
    namespace Tree
    {
        using Microsoft.CodeAnalysis.CSharp;

        internal class ParseErrorData
        {
            internal readonly IParseTree Node;
            internal readonly ErrorCode Code;
            internal readonly object[] Args;
            internal ParseErrorData(ErrorCode code) : this(node: null, code: code, args: SpecializedCollections.EmptyObjects) { }
            internal ParseErrorData(ErrorCode code, params object[] args) : this(node: null, code: code, args: args) { }
            internal ParseErrorData(IParseTree node, ErrorCode code) : this(node, code, SpecializedCollections.EmptyObjects) { }
            internal ParseErrorData(IParseTree node, ErrorCode code, params object[] args)
            {
                this.Node = node;
                this.Code = code;
                this.Args = args;
            }
            internal ParseErrorData(IToken token, ErrorCode code, params object[] args)
            {
                this.Node = new TerminalNodeImpl(token);
                this.Code = code;
                this.Args = args;
            }
            /*protected static SyntaxDiagnosticInfo MakeError(CSharpSyntaxNode node, ErrorCode code, params object[] args)
            {
                return new SyntaxDiagnosticInfo(node.GetLeadingTriviaWidth(), node.Width, code, args);
            }*/
            internal static List<ParseErrorData> NewBag()
            {
                return new List<ParseErrorData>();
            }
        }

        public partial interface IParseTree
        {
            object CsNode { get; set;  }
            bool IsHidden { get; } 
            int Position { get; }
            int FullWidth { get; }
            string SourceFileName { get; }
            string MappedFileName { get; }
            int MappedLine { get; }
        }
        public partial class TerminalNodeImpl: Microsoft.CodeAnalysis.IMessageSerializable
        {
            public object CsNode { get; set; }
            public bool IsHidden { get { return false; } }
            public int Position { get { return Symbol.StartIndex; } }
            public int FullWidth {  get { return Symbol.StopIndex - Symbol.StartIndex + 1; } }
            public string SourceFileName { get { return (Symbol as CommonToken).SourceFileName; } }
            public string MappedFileName { get { return (Symbol as CommonToken).MappedFileName; } }
            public int MappedLine { get { return (Symbol as CommonToken).MappedLine; } }
            public override string ToString() { return this.GetText(); }
        }
    }

    public partial class Lexer {
        protected Tuple<ITokenSource, ICharStream> TokenFactorySourcePair { get { return _tokenFactorySourcePair; } }
    }

    public partial class RuleContext
    {
        public object CsNode { get; set; }
        public virtual int Position { get; }
        public virtual bool IsHidden { get { return false; } }
        public virtual int FullWidth { get; }
        public virtual string SourceFileName { get; }
        public virtual string MappedFileName { get; }
        public virtual int MappedLine { get; }

        internal List<ParseErrorData> ErrorData;

        internal bool HasErrors()
        {
            return (ErrorData != null) && ErrorData.Count > 0;
        }

        internal void AddError(ParseErrorData e)
        {
            if (ErrorData == null)
                ErrorData = new List<ParseErrorData>();
            ErrorData.Add(e);
        }
    }

 

    public partial class ParserRuleContext: Microsoft.CodeAnalysis.IMessageSerializable
    {
        [FlagsAttribute]
        enum MethodFlags 
        {
            None = 0,
            ClipperCallingConvention = 1,
            MissingReturnType = 2,
            UsesPSZ = 4,
            MustBeUnsafe = 8,
            MustHaveReturnType = 16,
            UsesClipperCallingFunction = 32,     // PCOUNT, _GETMPARAM, _GETFPARAM
            HasTypedParameter = 64,
        }
        int iBPLength = -1;
        MethodFlags flags = MethodFlags.None;
        public bool HasClipperCallingConvention
        {
            get { return (flags & MethodFlags.ClipperCallingConvention) == MethodFlags.ClipperCallingConvention; }
            set { if (value) flags |= MethodFlags.ClipperCallingConvention; else flags &= ~MethodFlags.ClipperCallingConvention;  }
        }

        public bool HasMissingReturnType
        {
            get { return (flags & MethodFlags.MissingReturnType) == MethodFlags.MissingReturnType; }
            set { if (value) flags |= MethodFlags.MissingReturnType; else flags &= ~MethodFlags.MissingReturnType; }
        }
        public bool HasTypedParameter
        {
            get { return (flags & MethodFlags.HasTypedParameter) == MethodFlags.HasTypedParameter; }
            set { if (value) flags |= MethodFlags.HasTypedParameter; else flags &= ~MethodFlags.HasTypedParameter; }
        }
        public bool UsesClipperCallingFunction
        {
            get { return (flags & MethodFlags.UsesClipperCallingFunction) == MethodFlags.UsesClipperCallingFunction; }
            set { if (value) flags |= MethodFlags.UsesClipperCallingFunction; else flags &= ~MethodFlags.UsesClipperCallingFunction; }
        }
        public bool UsesPSZ
        {
            get { return (flags & MethodFlags.UsesPSZ) == MethodFlags.UsesPSZ; }
            set { if (value) flags |= MethodFlags.UsesPSZ; else flags &= ~MethodFlags.UsesPSZ; }
        }
        public bool MustBeUnsafe
        {
            get { return (flags & MethodFlags.MustBeUnsafe) == MethodFlags.MustBeUnsafe; }
            set { if (value) flags |= MethodFlags.MustBeUnsafe; else flags &= ~MethodFlags.MustBeUnsafe; }
        }

        public bool MustHaveReturnType
        {
            get { return (flags & MethodFlags.MustHaveReturnType) == MethodFlags.MustHaveReturnType; }
            set { if (value) flags |= MethodFlags.MustHaveReturnType; else flags &= ~MethodFlags.MustHaveReturnType; }
        }

        public override bool IsHidden { get { return iBPLength == -1; } }
        public override int Position { get { return Start.StartIndex; } }
        public override int FullWidth
        {
            get
            {
                if (iBPLength > 0)
                    return iBPLength;
                if (Stop != null)
                    return Stop.StopIndex - Start.StartIndex + 1;
                else
                    return Start.StopIndex - Start.StartIndex + 1;

            }
        }
        public override string SourceFileName { get { return (Start as CommonToken).SourceFileName; } }
        public override string MappedFileName { get { return (Start as CommonToken).MappedFileName; } }
        public override int MappedLine { get { return (Start as CommonToken).MappedLine; } }
        public override string ToString() {
            /*return this.GetText();*/
            var s = this.GetType().ToString();
            return s.Substring(s.LastIndexOfAny(".+".ToCharArray()) + 1).Replace("Context", "");
        }
        public void SetSequencePoint(int iEndPoint)
        {
            if (iEndPoint > 0)
                iBPLength = iEndPoint - this.Start.StartIndex + 1;
        }
    }

    public partial class CommonToken : Microsoft.CodeAnalysis.IMessageSerializable
    {
        internal string SourceFileName;
        internal string MappedFileName;
        internal int MappedLine = -1;
    }

    internal static class RuleExtensions
    {
        internal static void Put<T>([NotNull] this IParseTree t, T node) where T : InternalSyntax.CSharpSyntaxNode
        {
            if (node != null)
            {
                node.XNode = t;
                t.CsNode = node;
            }
        }

        internal static T Get<T>([NotNull] this IParseTree t) where T : InternalSyntax.CSharpSyntaxNode
        {
            if (t == null || t.CsNode == null)
                return default(T);

            return (T)t.CsNode;
        }

        internal static void PutList<T>([NotNull] this IParseTree t, InternalSyntax.SyntaxList<T> node) where T : InternalSyntax.CSharpSyntaxNode
        {
            //node.XNode = t;
            t.CsNode = node;
        }

        internal static InternalSyntax.SyntaxList<T> GetList<T>([NotNull] this IParseTree t) where T : InternalSyntax.CSharpSyntaxNode
        {
            if (t.CsNode == null)
                return default(InternalSyntax.SyntaxList<T>);

            return (InternalSyntax.SyntaxList<T>)t.CsNode;
        }

        internal static TNode WithAdditionalDiagnostics<TNode>([NotNull] this TNode node, params DiagnosticInfo[] diagnostics) where TNode : InternalSyntax.CSharpSyntaxNode
        {
            DiagnosticInfo[] existingDiags = node.GetDiagnostics();
            int existingLength = existingDiags.Length;
            if (existingLength == 0)
            {
                return node.WithDiagnosticsGreen(diagnostics);
            }
            else
            {
                DiagnosticInfo[] result = new DiagnosticInfo[existingDiags.Length + diagnostics.Length];
                existingDiags.CopyTo(result, 0);
                diagnostics.CopyTo(result, existingLength);
                return node.WithDiagnosticsGreen(result);
            }
        }

        internal static bool isInInterface([NotNull] this RuleContext context)
        {
            return ((context.Parent is XSharpParser.ClassmemberContext) && (context.Parent.Parent is XSharpParser.Interface_Context)) 
                || (context.Parent is XSharpParser.Interface_Context);
        }

    }
}

namespace Microsoft.CodeAnalysis.CSharp.Syntax.InternalSyntax
{
    internal static partial class SyntaxFactory
    {
        readonly static internal SyntaxTrivia WS = Whitespace(" ");

        internal static SyntaxToken MakeToken(SyntaxKind kind)
        {
            return Token(WS, kind, WS);
        }

        internal static SyntaxToken MakeToken(SyntaxKind kind, string text)
        {
            return Token(WS, kind, text, text, WS);
        }

        internal static SyntaxToken MakeIdentifier(string text)
        {
            return Identifier(WS, text, WS);
        }
    }

    internal abstract partial class CSharpSyntaxNode
    {
        public IParseTree XNode { get; internal set; }
        public ITokenStream XTokens { get; internal set; }
        public Dictionary<string, SourceText> IncludedFiles { get; internal set; }

    }
}

namespace Microsoft.CodeAnalysis.CSharp
{
    public abstract partial class CSharpSyntaxNode
    {
        internal IParseTree XNode { get { return (((InternalSyntax.CSharpSyntaxNode)(Green)).XNode) ?? Parent?.XNode; } }
    }
}

namespace Microsoft.CodeAnalysis.CSharp.Syntax
{
    public sealed partial class CompilationUnitSyntax
    {
        public XSharpParser.SourceContext XSource { get { return (XSharpParser.SourceContext)(((InternalSyntax.CompilationUnitSyntax)(this.Green)).XNode); } }
        public ITokenStream XTokenStream { get { return (((InternalSyntax.CompilationUnitSyntax)(this.Green)).XTokens); } }
        internal Dictionary<string, SourceText> IncludedFiles { get { return (((InternalSyntax.CompilationUnitSyntax)(this.Green)).IncludedFiles); } }
    }
}