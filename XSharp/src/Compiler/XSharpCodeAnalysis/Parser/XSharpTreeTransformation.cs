﻿/*
   Copyright 2016 XSharp B.V.

Licensed under the X# compiler source code License, Version 1.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

http://www.xsharp.info/licenses

Unless required by applicable law or agreed to in writing, software
Distributed under the License is distributed on an "as is" basis,
without warranties or conditions of any kind, either express or implied.
See the License for the specific language governing permissions and   
limitations under the License.
*/
// Uncomment this define to dump the AST to the debug console.
//#define DUMP_TREE

using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Utilities;
using InternalSyntax = Microsoft.CodeAnalysis.CSharp.Syntax.InternalSyntax;
using Antlr4.Runtime;
using Antlr4.Runtime.Atn;
using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Tree;
using LanguageService.CodeAnalysis.XSharp.SyntaxParser;
using XP = LanguageService.CodeAnalysis.XSharp.SyntaxParser.XSharpParser;
namespace Microsoft.CodeAnalysis.CSharp.Syntax.InternalSyntax
{
    internal class XSharpTreeTransformation : XSharpBaseListener
    {
        internal class SyntaxEntities
        {
            internal SyntaxListPool _pool;
            public SyntaxListBuilder<ExternAliasDirectiveSyntax> Externs;
            public SyntaxListBuilder<UsingDirectiveSyntax> Usings;
            public SyntaxListBuilder<AttributeListSyntax> Attributes;
            public SyntaxListBuilder<MemberDeclarationSyntax> Members;

            internal SyntaxEntities(SyntaxListPool pool) {
                Externs = pool.Allocate<ExternAliasDirectiveSyntax>();
                Usings = pool.Allocate<UsingDirectiveSyntax>();
                Attributes = pool.Allocate<AttributeListSyntax>();
                Members = pool.Allocate<MemberDeclarationSyntax>();
                _pool = pool;
            }

            internal void Free()
            {
                _pool.Free(Members);
                _pool.Free(Attributes);
                _pool.Free(Usings);
                _pool.Free(Externs);
            }
        }

        internal class SyntaxClassEntities
        {
            internal class VoPropertyInfo {
                internal SyntaxToken idName;
                internal XP.MethodContext AccessMethodCtx = null;
                internal XP.MethodContext AssignMethodCtx = null;
            }

            internal SyntaxListPool _pool;
            public SyntaxListBuilder<MemberDeclarationSyntax> Members;
            public Dictionary<string,VoPropertyInfo> VoProperties;

            internal SyntaxClassEntities(SyntaxListPool pool) {
                Members = pool.Allocate<MemberDeclarationSyntax>();
                _pool = pool;
            }

            internal void Free()
            {
                _pool.Free(Members);
            }

            internal void AddVoPropertyAccessor(XP.MethodContext accessor)
            {
                if (VoProperties == null)
                    VoProperties = new Dictionary<string, VoPropertyInfo>(CaseInsensitiveComparison.Comparer);
                string name = accessor.Id.Get<SyntaxToken>().Text;
                VoPropertyInfo propertyInfo;
                if (!VoProperties.TryGetValue(name,out propertyInfo)) {
                    propertyInfo = new VoPropertyInfo();
                    propertyInfo.idName = accessor.Id.Get<SyntaxToken>();
                    VoProperties.Add(name,propertyInfo);
                }
                switch (accessor.T.Token.Type) {
                    case XP.ACCESS:
                        if (propertyInfo.AccessMethodCtx != null)
                            accessor.AddError(new ParseErrorData(ErrorCode.ERR_DuplicateAccessor, accessor));
                        else
                            propertyInfo.AccessMethodCtx = accessor;
                        break;
                    case XP.ASSIGN:
                        if (propertyInfo.AssignMethodCtx != null)
                            accessor.AddError(new ParseErrorData(ErrorCode.ERR_DuplicateAccessor, accessor));
                        else
                            propertyInfo.AssignMethodCtx = accessor;
                        break;
                    default:
                        break;
                }
            }
        }

        public const string XSharpGlobalClassName = "Xs$Globals";
        protected const string ImpliedTypeName = "Xs$var";
        protected const string StaticLocalFieldNamePrefix = "Xs$StaticLocal$";
        protected const string StaticLocalInitFieldNameSuffix = "$init";
        protected const string StaticLocalLockFieldNameSuffix = "$lock";
        protected const string EventFieldNamePrefix = "Xs$Event$";
        protected const string VoPropertyAccessPrefix = "Xs$Access$";
        protected const string VoPropertyAssignPrefix = "Xs$Assign$";
        protected const string CompilerGenerated = "global::System.Runtime.CompilerServices.CompilerGenerated";
        private static int _unique = 0;

        protected string GlobalClassName = XSharpGlobalClassName;

        internal SyntaxListPool _pool;
        protected readonly ContextAwareSyntax _syntaxFactory; // Has context, the fields of which are resettable.
        protected XSharpParser _parser;
        protected readonly CSharpParseOptions _options;
        protected TypeSyntax _ptrType;
        protected TypeSyntax _objectType;
        protected TypeSyntax _voidType;
        protected TypeSyntax _impliedType;
        protected string _fileName;

        internal SyntaxEntities GlobalEntities;
        internal SyntaxClassEntities GlobalClassEntities;
        internal Stack<SyntaxClassEntities> ClassEntities = new Stack<SyntaxClassEntities> ();
        internal Stack<XP.IEntityContext> Entities = new Stack<XP.IEntityContext>();

        protected XP.IEntityContext CurrentEntity
        {
            get
            {
                if (Entities.Count > 0)
                        return Entities.Peek();
                return null;
            }
        }
        public XSharpTreeTransformation(XSharpParser parser, CSharpParseOptions options, SyntaxListPool pool, 
            ContextAwareSyntax syntaxFactory, string fileName)
        {
            _pool = pool;
            _syntaxFactory = syntaxFactory;
            _parser = parser;
            _options = options;
            GlobalEntities = CreateEntities();
            _ptrType = GenerateQualifiedName("global::System.IntPtr");
            _objectType = _syntaxFactory.PredefinedType(SyntaxFactory.MakeToken(SyntaxKind.ObjectKeyword));
            _voidType = _syntaxFactory.PredefinedType(SyntaxFactory.MakeToken(SyntaxKind.VoidKeyword));
            _impliedType = GenerateSimpleName(ImpliedTypeName);
            _fileName = fileName;
        }

        internal CSharpSyntaxNode NotInDialect(string feature)
        {
            CSharpSyntaxNode node = _syntaxFactory.EmptyStatement(SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken));
            return NotInDialect(node, feature);
        }
        internal CSharpSyntaxNode NotInDialect(CSharpSyntaxNode node, string feature)
        {
            return node.WithAdditionalDiagnostics(
                new SyntaxDiagnosticInfo(ErrorCode.ERR_FeatureNotAvailableInDialect, feature, _options.Dialect.ToString()));
        }

        internal void Free()
        {
            GlobalEntities.Free();
        }

        internal SyntaxEntities CreateEntities() {
            return new SyntaxEntities(_pool);
        }

        internal SyntaxClassEntities CreateClassEntities() {
            return new SyntaxClassEntities(_pool);
        }

        internal string UniqueNameSuffix {
            get { return "$"+_unique++; }
        }

        internal T FixPosition<T>(T r, IToken t) where T: ParserRuleContext
        {
            r.Start = r.Stop = t;
            return r;
        }

        protected SyntaxToken GetRShiftToken(IToken firstGT, IToken secondGT)
        {
            if (secondGT == null)
                return SyntaxFactory.MakeToken(SyntaxKind.GreaterThanToken);
            int iFirst = firstGT.Column;
            int iSecond = secondGT.Column;
            SyntaxToken result;
            if (iSecond != iFirst + 1) // extra whitespace detected
                result = SyntaxFactory.MissingToken(SyntaxKind.GreaterThanGreaterThanToken).WithAdditionalDiagnostics(new SyntaxDiagnosticInfo(ErrorCode.ERR_UnexpectedToken, "> >"));
            else
                result = SyntaxFactory.MakeToken(SyntaxKind.GreaterThanGreaterThanToken);

            return result;
        }

        protected bool IsEntity(ParserRuleContext context)
        {
            return context is XP.IEntityContext;
        }
        #region Entitynames
        protected string GetNestedName(IRuleNode ctx)
        {
            string name = "";
            int iNest = 0;
            while (ctx != null)
            {
                if (ctx is XP.Class_Context)
                {
                    name = ((XP.Class_Context)ctx).Id.GetText() + "." + name;
                    iNest++;
                } else if (ctx is XP.Structure_Context)
                {
                    name = ((XP.Structure_Context)ctx).Id.GetText() + "." + name;
                    iNest++;
                } else if (ctx is XP.Namespace_Context)
                {
                    name = ((XP.Namespace_Context)ctx).Name.GetText() + "." + name;
                    iNest++;
                } else if (ctx is XP.Interface_Context)
                {
                    name = ((XP.Interface_Context)ctx).Id.GetText() + "." + name;
                    iNest++;
                }
                else if (ctx is XP.PropertyContext)
                {
                    name = ((XP.PropertyContext)ctx).Id.GetText() + "." + name;
                }
                else if (ctx is XP.Event_Context)
                {
                    name = ((XP.Event_Context)ctx).Id.GetText() + "." + name;
                }
                ctx = ctx.Parent;
            }
            if (iNest == 1 && !String.IsNullOrEmpty(_options.DefaultNamespace )) {
                name = _options.DefaultNamespace + "." + name;
            }
            return name;
            
        }
        protected virtual BlockSyntax AddMissingReturnStatement(BlockSyntax body, XP.StatementBlockContext block, TypeSyntax returnType)
        {
            return body;
        }
        protected string GetEntityName(Boolean Full)
        {
            string name = "";
            string suffix = "";
            XP.IEntityContext context = Entities.Peek();
            if (context == null)
                return "";
            XP.DatatypeContext RetType = null;
            XP.ParameterListContext Params = null;
            XP.PropertyParameterListContext PParams = null;
            name = GetNestedName(context.Parent);
            if (context is XP.FunctionContext)
            {
                XP.FunctionContext fc = (XP.FunctionContext)context;
                if (name.Length == 0)
                    name = GlobalClassName + "." +fc.Id.GetText();
                else
                    name += fc.Id.GetText();
                
                RetType = fc.Type;
                Params = fc.ParamList;
            }
            else if (context is XP.ProcedureContext)
            {
                XP.ProcedureContext pc = (XP.ProcedureContext)context;
                if(name.Length == 0)
                    name = GlobalClassName + "." + pc.Id.GetText();
                else
                    name += pc.Id.GetText();
                Params = pc.ParamList;
            }
            else if (context is XP.ClsctorContext)
            {
                XP.ClsctorContext cc = (XP.ClsctorContext)context;
                if (name.Length > 0) // Remove the dot
                    name = name.Substring(0, name.Length - 1);
                suffix= ".CTOR";
                Params = cc.ParamList;
            }
            else if (context is XP.ClsdtorContext)
            {
                XP.ClsdtorContext dc = (XP.ClsdtorContext)context;
                name += "Finalize()";
            }
            else if (context is XP.MethodContext)
            {
                XP.MethodContext mc = (XP.MethodContext)context;
                if (mc.ClassId != null)
                    name += mc.ClassId.GetText() + "." + mc.Id.GetText();
                else
                {
                    name += mc.Id.GetText();
                }
                RetType = mc.Type;
                Params = mc.ParamList;
                switch (mc.T.Token.Type)
                {
                    case XP.ACCESS:
                        suffix = ":Access";
                        break;
                    case XP.ASSIGN:
                        suffix = ":Assign";
                        break;
                }
            }
            else if (context is XP.PropertyContext)
            {
                XP.PropertyContext pc = (XP.PropertyContext)context;
                if (pc.Id != null)
                    name += pc.Id.GetText();
                if (pc.SELF() != null)
                    name += pc.SELF()?.GetText();
                PParams = pc.ParamList;
                RetType = pc.Type;
                suffix = ":Property";

            }
            else if (context is XP.Event_Context)
            {
                XP.Event_Context ec = (XP.Event_Context)context;
                name += ec.Id.GetText();
                RetType = ec.Type;
                suffix = ":Event";
            }
            else if (context is XP.VodllContext)
            {
                XP.VodllContext vdc = (XP.VodllContext)context;
                name += vdc.Id.GetText();
                RetType = vdc.Type;
                Params = vdc.ParamList;
                suffix = ":VoDll";
            }
            else if (context is XP.Delegate_Context)
            {
                XP.Delegate_Context dc = (XP.Delegate_Context)context;
                name += dc.Id.GetText();
                RetType = dc.Type;
                Params = dc.ParamList;
                suffix = ":Delegate";
            }
            else if (context is XP.Class_Context)
            {
                XP.Class_Context cc = (XP.Class_Context)context;
                name += cc.Id.GetText();
                suffix = ":Class";
            }
            else if (context is XP.Structure_Context)
            {
                XP.Structure_Context sc = (XP.Structure_Context)context;
                name += sc.Id.GetText();
                suffix = ":Structure";
            }
            else if (context is XP.EventAccessorContext)
            {
                XP.EventAccessorContext ec = (XP.EventAccessorContext)context;
                name += ec.Key.Text;
            }
            else if (context is XP.PropertyAccessorContext)
            {
                XP.PropertyAccessorContext pc = (XP.PropertyAccessorContext)context;
                name += pc.Key.Text;
            }
            if (Full)
            {
                if (RetType != null)
                {
                    name = RetType.GetText() + " " + name;
                }
                else {
                    name = "VOID " + name;
                }
                string strParams = "";
                if (Params != null)
                {
                    foreach (XP.ParameterContext _par in Params._Params)
                    {
                        if (strParams?.Length > 0)
                            strParams += ", ";
                        if (_par.Type != null)
                            strParams += _par.Type.GetText();
                        else
                            strParams += "USUAL";
                    }
                }
                if (PParams != null)
                {
                    foreach (XP.ParameterContext _par in PParams._Params)
                    {
                        if (strParams?.Length > 0)
                            strParams += ", ";
                        if (_par.Type != null)
                            strParams += _par.Type.GetText();
                        else
                            strParams += "USUAL";
                    }
                }
                if (String.Compare(suffix, ".CTOR", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    name += "{ " + strParams + " }";
                    suffix = "";
                }
                else
                {
                    name += "( " + strParams + " )";
                    if (!string.IsNullOrEmpty(suffix))
                        name += suffix;
                }

            }
            else
                name = name.ToUpper()+suffix;
            return name;

        }
        #endregion
        protected SyntaxList<SyntaxToken> TokenList(params SyntaxKind[] kinds)
        {
            var rb = _pool.Allocate();
            foreach(var k in kinds) {
                rb.Add(SyntaxFactory.MakeToken(k));
            }
            var r = rb.ToTokenList();
            _pool.Free(rb);
            return r;
        }

        protected SyntaxList<SyntaxToken> TokenListWithDefaultVisibility(Boolean inInterface = false,  params SyntaxKind[] kinds)
        {
            var rb = _pool.Allocate();
            foreach(var k in kinds) {
                rb.Add(SyntaxFactory.MakeToken(k));
            }
            if (!inInterface)
                rb.FixDefaultVisibility();
            var r = rb.ToTokenList();
            _pool.Free(rb);
            return r;
        }

        protected SyntaxList<SyntaxToken> DefaultMethodModifiers(bool inInterface = false)
        {
            var rb = _pool.Allocate();
            if (!inInterface)
            {
                rb.FixDefaultVisibility();
                if (_options.VirtualInstanceMethods)
                    rb.FixDefaultVirtual();
                else
                    rb.FixDefaultMethod();
            }
            var r = rb.ToTokenList();
            _pool.Free(rb);
            return r;
        }

        protected SyntaxList<SyntaxToken> EmptyList()
        {
            var rb = _pool.Allocate();
            var r = rb.ToTokenList();
            _pool.Free(rb);
            return r;
        }

        protected SyntaxList<T> EmptyList<T>() where T : CSharpSyntaxNode
        {
            var rb = _pool.Allocate<T>();
            var r = rb.ToList();
            _pool.Free(rb);
            return r;
        }

        protected SeparatedSyntaxList<T> EmptySeparatedList<T>() where T : CSharpSyntaxNode
        {
            var rb = _pool.AllocateSeparated<T>();
            var r = rb.ToList();
            _pool.Free(rb);
            return r;
        }

        protected ArgumentListSyntax EmptyArgumentList()
        {
            return _syntaxFactory.ArgumentList(
                    SyntaxFactory.MakeToken(SyntaxKind.OpenParenToken), 
                    default(SeparatedSyntaxList<ArgumentSyntax>), 
                    SyntaxFactory.MakeToken(SyntaxKind.CloseParenToken));
        }

        protected BracketedArgumentListSyntax EmptyBracketedArgumentList()
        {
            return _syntaxFactory.BracketedArgumentList(
                    SyntaxFactory.MakeToken(SyntaxKind.OpenBracketToken), 
                    default(SeparatedSyntaxList<ArgumentSyntax>), 
                    SyntaxFactory.MakeToken(SyntaxKind.CloseBracketToken));
        }

        protected SyntaxList<T> MakeList<T>(IEnumerable<IParseTree> t) where T : InternalSyntax.CSharpSyntaxNode
        {
            if (t == null)
                return default(SyntaxList<T>);
            var l = _pool.Allocate<T>();
            foreach (var item in t) {
                if (item != null) {
                    if (item.CsNode is SyntaxList<T>)
                        l.AddRange(item.GetList<T>());
                    else
                        l.Add(item.Get<T>());
                }
            }
            var list = l.ToList();
            _pool.Free(l);
            return list;
        }

        protected SyntaxList<T> MakeList<T>(IEnumerable<T> t, params T[] items) where T : InternalSyntax.CSharpSyntaxNode
        {
            var l = _pool.Allocate<T>();
            if (t != null)
            {
                foreach (var item in t)
                {
                    if (item != null)
                        l.Add(item);
                }
            }
            foreach (var item in items)
            {
                if (item != null)
                    l.Add(item);
            }
            var list = l.ToList();
            _pool.Free(l);
            return list;
        }

        protected SyntaxList<T> MakeList<T>(params T[] items) where T : InternalSyntax.CSharpSyntaxNode
        {
            var l = _pool.Allocate<T>();
            foreach (var item in items) {
                if (item != null)
                    l.Add(item);
            }
            var list = l.ToList();
            _pool.Free(l);
            return list;
        }

        protected SeparatedSyntaxList<T> MakeSeparatedList<T>(System.Collections.IEnumerable t) where T : InternalSyntax.CSharpSyntaxNode
        {
            if (t == null)
                return default(SeparatedSyntaxList<T>);
            var l = _pool.AllocateSeparated<T>();
            foreach (var item in t) {
                if (item != null) {
                    if (l.Count>0)
                        l.AddSeparator(SyntaxFactory.MakeToken(SyntaxKind.CommaToken));
                    l.Add(((IParseTree)item).Get<T>());
                }
            }
            var list = l.ToList();
            _pool.Free(l);
            return list;
        }

        protected SeparatedSyntaxList<T> MakeSeparatedList<T>(params T[] items) where T : InternalSyntax.CSharpSyntaxNode
        {
            var l = _pool.AllocateSeparated<T>();
            foreach (var item in items) {
                if (item != null) {
                    if (l.Count>0)
                        l.AddSeparator(SyntaxFactory.MakeToken(SyntaxKind.CommaToken));
                    l.Add(item);
                }
            }
            var list = l.ToList();
            _pool.Free(l);
            return list;
        }

        protected ArgumentListSyntax MakeArgumentList(params ArgumentSyntax[] items)
        {
            return _syntaxFactory.ArgumentList(
                    SyntaxFactory.MakeToken(SyntaxKind.OpenParenToken), 
                    MakeSeparatedList<ArgumentSyntax>(items), 
                    SyntaxFactory.MakeToken(SyntaxKind.CloseParenToken));
        }

        protected ArrayRankSpecifierSyntax MakeArrayRankSpecifier(int ranks)
        {
            var sizes = _pool.AllocateSeparated<ExpressionSyntax>();
            for(int i = 0; i < ranks; i++) {
                if (i > 0)
                    sizes.AddSeparator(SyntaxFactory.MakeToken(SyntaxKind.CommaToken));
                sizes.Add(_syntaxFactory.OmittedArraySizeExpression(SyntaxFactory.MakeToken(SyntaxKind.OmittedArraySizeExpressionToken)));
            }
            var r = _syntaxFactory.ArrayRankSpecifier(SyntaxFactory.MakeToken(SyntaxKind.OpenBracketToken),
                sizes,
                SyntaxFactory.MakeToken(SyntaxKind.CloseBracketToken));
            _pool.Free(sizes);
            return r;
        }
        protected ExpressionSyntax MakeCastTo(TypeSyntax type, ExpressionSyntax expr)
        {
            return _syntaxFactory.CastExpression(SyntaxFactory.MakeToken(SyntaxKind.OpenParenToken),
                type, SyntaxFactory.MakeToken(SyntaxKind.CloseParenToken), expr);
        }


        protected AssignmentExpressionSyntax MakeSimpleAssignment(ExpressionSyntax lhs, ExpressionSyntax rhs)
        {
            return _syntaxFactory.AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,
                     lhs, SyntaxFactory.MakeToken(SyntaxKind.EqualsToken), rhs);
        }

        protected TypeSyntax VoidType()
        {
            return _voidType;
        }

        protected TypeSyntax MissingType()
        {
            return _objectType
                .WithAdditionalDiagnostics(new SyntaxDiagnosticInfo(ErrorCode.ERR_TypeExpected));
        }

        protected ParameterListSyntax EmptyParameterList()
        {
            return _syntaxFactory.ParameterList(
                SyntaxFactory.MakeToken(SyntaxKind.OpenParenToken),
                EmptySeparatedList<ParameterSyntax>(),
                SyntaxFactory.MakeToken(SyntaxKind.CloseParenToken));
        }

        protected IfStatementSyntax GenerateIfStatement(ExpressionSyntax condition, StatementSyntax statement, ElseClauseSyntax @else = null) { 
            return _syntaxFactory.IfStatement(
                        SyntaxFactory.MakeToken(SyntaxKind.IfKeyword),
                        SyntaxFactory.MakeToken(SyntaxKind.OpenParenToken),
                        condition,
                        SyntaxFactory.MakeToken(SyntaxKind.CloseParenToken),
                        statement, @else);
        }

        protected LiteralExpressionSyntax GenerateLiteral(bool value)
        {
            if (value)
                return _syntaxFactory.LiteralExpression(SyntaxKind.TrueLiteralExpression, SyntaxFactory.MakeToken(SyntaxKind.TrueKeyword));
            else
                return _syntaxFactory.LiteralExpression(SyntaxKind.FalseLiteralExpression, SyntaxFactory.MakeToken(SyntaxKind.FalseKeyword));
        }
        protected LiteralExpressionSyntax GenerateLiteral(string text)
        {
            return _syntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression,
                                                SyntaxFactory.Literal(null, text, text, null));
        }
        protected LiteralExpressionSyntax GenerateLiteral(int value)
        {
            return _syntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression,
                                                SyntaxFactory.Literal(null, value.ToString(),value, null));
        }
        protected LiteralExpressionSyntax GenerateLiteral(string source, int value)
        {
            return _syntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression,
                                                SyntaxFactory.Literal(null, source, value, null));
        }

        protected VariableDeclaratorSyntax GenerateVariable(string name, ExpressionSyntax initexpr = null)
        {
            return GenerateVariable(SyntaxFactory.Identifier(name), initexpr);
        }
        protected VariableDeclaratorSyntax GenerateVariable(SyntaxToken nameToken, ExpressionSyntax initexpr = null)
        {
            if (initexpr != null)
            {
                return _syntaxFactory.VariableDeclarator(
                    nameToken, null,
                    _syntaxFactory.EqualsValueClause(SyntaxFactory.MakeToken(SyntaxKind.EqualsToken), initexpr)
                    );
            }
            else
            {
                return _syntaxFactory.VariableDeclarator(
                    nameToken, null, null
                    );

            }
        }

        protected LocalDeclarationStatementSyntax GenerateLocalDecl(string name, TypeSyntax type, ExpressionSyntax initexpr = null)
        {
            SyntaxListBuilder modifiers = _pool.Allocate();
            var result =
                    _syntaxFactory.LocalDeclarationStatement(
                        modifiers.ToTokenList(),
                        _syntaxFactory.VariableDeclaration(type,
                        MakeSeparatedList<VariableDeclaratorSyntax>(GenerateVariable(name, initexpr))),
                        SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken));
            _pool.Free(modifiers);
            return result;
        }
        protected IdentifierNameSyntax GenerateSimpleName(string name)
        {
            return _syntaxFactory.IdentifierName(SyntaxFactory.Identifier(name));
        }

        protected NameEqualsSyntax GenerateNameEquals(string name)
        {
            return _syntaxFactory.NameEquals(
                  GenerateSimpleName(name), SyntaxFactory.MakeToken(SyntaxKind.EqualsToken));
        }

        protected NameSyntax GenerateQualifiedName(string name)
        {
            string[] ids = name.Split('.');
            string idName = ids[0];
            string alias = null;
            int cc = idName.IndexOf("::");
            if (cc >= 0) {
                alias = idName.Substring(0,cc);
                idName = idName.Substring(cc+2);
            }
            NameSyntax r = GenerateSimpleName(idName);
            if (alias != null) {
                if (string.Compare(alias,"global",StringComparison.OrdinalIgnoreCase) == 0)
                    r = _syntaxFactory.AliasQualifiedName(
                        _syntaxFactory.IdentifierName(SyntaxFactory.MakeToken(SyntaxKind.GlobalKeyword, alias)),
                        SyntaxFactory.MakeToken(SyntaxKind.ColonColonToken),
                        (SimpleNameSyntax)r);
                else
                    r = _syntaxFactory.AliasQualifiedName(
                        GenerateSimpleName(alias),
                        SyntaxFactory.MakeToken(SyntaxKind.ColonColonToken),
                        (SimpleNameSyntax)r);
            }
            for(int i = 1; i < ids.Length; i++)
            {
                r = _syntaxFactory.QualifiedName(
                    r,
                    SyntaxFactory.MakeToken(SyntaxKind.DotToken),
                    GenerateSimpleName(ids[i]));
            }
            return r;
        }

        internal static Syntax.NameSyntax ExtGenerateQualifiedName(string name)
        {
            string[] ids = name.Split('.');
            string idName = ids[0];
            string alias = null;
            int cc = idName.IndexOf("::");
            if (cc >= 0)
            {
                alias = idName.Substring(0, cc);
                idName = idName.Substring(cc + 2);
            }
            Syntax.NameSyntax r = CSharp.SyntaxFactory.IdentifierName(idName);
            if (alias != null)
            {
                if (string.Compare(alias, "global", StringComparison.OrdinalIgnoreCase) == 0)
                    r = CSharp.SyntaxFactory.AliasQualifiedName(
                        CSharp.SyntaxFactory.IdentifierName(CSharp.SyntaxFactory.Token(SyntaxKind.GlobalKeyword)),
                        CSharp.SyntaxFactory.Token(SyntaxKind.ColonColonToken),
                        (Syntax.SimpleNameSyntax)r);
                else
                    r = CSharp.SyntaxFactory.AliasQualifiedName(
                        CSharp.SyntaxFactory.IdentifierName(alias),
                        CSharp.SyntaxFactory.Token(SyntaxKind.ColonColonToken),
                        (Syntax.SimpleNameSyntax)r);
            }
            for (int i = 1; i < ids.Length; i++)
            {
                r = CSharp.SyntaxFactory.QualifiedName(
                    r,
                    CSharp.SyntaxFactory.Token(SyntaxKind.DotToken),
                    CSharp.SyntaxFactory.IdentifierName(ids[i]));
            }
            return r;
        }

        protected NameSyntax GenerateGlobalQualifiedNameFromList(string name, params string[] dotNames)
        {
            NameSyntax r = GenerateSimpleName(name);
            r = _syntaxFactory.AliasQualifiedName(
                _syntaxFactory.IdentifierName(SyntaxFactory.MakeToken(SyntaxKind.GlobalKeyword, "global")),
                SyntaxFactory.MakeToken(SyntaxKind.ColonColonToken),
                (SimpleNameSyntax)r);
            foreach (var dotName in dotNames)
            {
                r = _syntaxFactory.QualifiedName(
                    r,
                    SyntaxFactory.MakeToken(SyntaxKind.DotToken),
                    GenerateSimpleName(dotName));
            }
            return r;
        }

        protected void GenerateAttributeList(SyntaxListBuilder<AttributeListSyntax> attributeLists, params string[] attributeNames)
        {
            SeparatedSyntaxListBuilder<AttributeSyntax> attributes = _pool.AllocateSeparated<AttributeSyntax>();
            foreach (var attributeName in attributeNames)
            {
                if (attributes.Count > 0)
                {
                    attributes.AddSeparator(SyntaxFactory.MakeToken(SyntaxKind.CommaToken));
                }
                attributes.Add(_syntaxFactory.Attribute(
                    name: GenerateQualifiedName(attributeName),
                    argumentList: null));
            }
            attributeLists.Add(_syntaxFactory.AttributeList(
                openBracketToken: SyntaxFactory.MakeToken(SyntaxKind.OpenBracketToken),
                target: null,
                attributes: attributes,
                closeBracketToken: SyntaxFactory.MakeToken(SyntaxKind.CloseBracketToken)));
            _pool.Free(attributes);
        }

        protected ExpressionSyntax CreateObject(TypeSyntax type, ArgumentListSyntax args, InitializerExpressionSyntax init)
        {
            ExpressionSyntax expr;
            expr = _syntaxFactory.ObjectCreationExpression(
                SyntaxFactory.MakeToken(SyntaxKind.NewKeyword),
                type,args,init);
            return expr;
        }

        protected ExpressionSyntax GenerateMethodCall(string MethodName, ArgumentListSyntax args)
        {
            ExpressionSyntax expr = _syntaxFactory.InvocationExpression(GenerateQualifiedName(MethodName), args);
            return expr;
        }

        protected virtual void ImplementClipperAndPSZ(XP.IEntityContext context,
            ref  SyntaxList<AttributeListSyntax> attributes, ref ParameterListSyntax parameters, ref BlockSyntax body,
            ref TypeSyntax dataType )
        {
            return;
        }


        protected ExpressionStatementSyntax GenerateExpressionStatement(ExpressionSyntax expr)
        {
            return _syntaxFactory.ExpressionStatement(expr, SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken));
        }
        protected ArgumentSyntax MakeArgument (ExpressionSyntax expr)
        {
            return _syntaxFactory.Argument(null, null, expr);
        }
        protected BlockSyntax MakeBlock(SyntaxList<StatementSyntax> statements)
        {
            return _syntaxFactory.Block(
                        SyntaxFactory.MakeToken(SyntaxKind.OpenBraceToken),
                        statements,
                        SyntaxFactory.MakeToken(SyntaxKind.CloseBraceToken));
        }


        protected MemberDeclarationSyntax AddNameSpaceToMember(XP.NameDotContext ns, MemberDeclarationSyntax m)
        {
            if (ns != null)
            {
                m = _syntaxFactory.NamespaceDeclaration(SyntaxFactory.MakeToken(SyntaxKind.NamespaceKeyword),
                    name: ns.Get<NameSyntax>(),
                    openBraceToken: SyntaxFactory.MakeToken(SyntaxKind.OpenBraceToken),
                    externs: EmptyList<ExternAliasDirectiveSyntax>(),
                    usings: EmptyList<UsingDirectiveSyntax>(),
                    members: MakeList<MemberDeclarationSyntax>(m),
                    closeBraceToken: SyntaxFactory.MakeToken(SyntaxKind.CloseBraceToken),
                    semicolonToken: SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken));

            }
            return m;

        }

        protected void AddUsingWhenMissing(SyntaxListBuilder<UsingDirectiveSyntax> usings, string name, bool bStatic)
        {
            bool found = false;
            NameSyntax usingName = GenerateQualifiedName(name);
            for (int i = 0; i < usings.Count; i++)
            {
                if (CaseInsensitiveComparison.Compare(GlobalEntities.Usings[i].Name.ToString(), usingName.ToString()) == 0)
                {
                    found = true;
                    break;
                }
            }
            if (!found)
            {
                SyntaxToken tokenStatic = null;
                if (bStatic)
                    tokenStatic = SyntaxFactory.MakeToken(SyntaxKind.StaticKeyword);

                usings.Add(_syntaxFactory.UsingDirective(SyntaxFactory.MakeToken(SyntaxKind.UsingKeyword),
                    tokenStatic,
                    null,
                    usingName,
                    SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken)));
            }

        }

        protected ClassDeclarationSyntax GenerateClass(string className, SyntaxListBuilder<MemberDeclarationSyntax> members)
        {
            SyntaxListBuilder<AttributeListSyntax> attributeLists = _pool.Allocate<AttributeListSyntax>();
            GenerateAttributeList(attributeLists, CompilerGenerated);
            SyntaxListBuilder modifiers = _pool.Allocate();
            modifiers.Add(SyntaxFactory.MakeToken(SyntaxKind.InternalKeyword));
            modifiers.Add(SyntaxFactory.MakeToken(SyntaxKind.StaticKeyword));
            var r = _syntaxFactory.ClassDeclaration(
                attributeLists: attributeLists,
                modifiers: modifiers.ToTokenList(),
                keyword: SyntaxFactory.MakeToken(SyntaxKind.ClassKeyword),
                identifier: SyntaxFactory.Identifier(className),
                typeParameterList: null,
                baseList: null, // BaseListSyntax baseList = _syntaxFactory.BaseList(colon, list)
                constraintClauses: default(SyntaxListBuilder<TypeParameterConstraintClauseSyntax>),
                openBraceToken: SyntaxFactory.MakeToken(SyntaxKind.OpenBraceToken),
                members: members,
                closeBraceToken: SyntaxFactory.MakeToken(SyntaxKind.CloseBraceToken),
                semicolonToken: SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken));
            _pool.Free(attributeLists);
            _pool.Free(modifiers);
            return r;
        }

        protected NamespaceDeclarationSyntax GenerateNamespace(string name, SyntaxList<MemberDeclarationSyntax> members)
        {
            var externs = _pool.Allocate<ExternAliasDirectiveSyntax>();
            var usings = _pool.Allocate<UsingDirectiveSyntax>();
            var r = _syntaxFactory.NamespaceDeclaration(SyntaxFactory.MakeToken(SyntaxKind.NamespaceKeyword),
                name: GenerateQualifiedName(name),
                openBraceToken: SyntaxFactory.MakeToken(SyntaxKind.OpenBraceToken),
                externs: externs,
                usings: usings,
                members: members,
                closeBraceToken: SyntaxFactory.MakeToken(SyntaxKind.CloseBraceToken),
                semicolonToken: SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken));
            _pool.Free(externs);
            _pool.Free(usings);
            return r;
        }

        private void splitClassNameAndNamespace( ref string className, out string nameSpace) {
            nameSpace = "";
            if(className.Contains(".")) {
                nameSpace = className.Substring(0, className.LastIndexOf("."));
                className = className.Substring(className.LastIndexOf(".") + 1);
            }
        }

        protected MemberDeclarationSyntax GenerateGlobalClass(string className, bool bInternalClass, SyntaxList<MemberDeclarationSyntax> members)
        {
            string nameSpace ;
            splitClassNameAndNamespace(ref className, out nameSpace);
            SyntaxListBuilder<AttributeListSyntax> attributeLists = _pool.Allocate<AttributeListSyntax>();
            SyntaxListBuilder modifiers = _pool.Allocate();
            modifiers.Add(SyntaxFactory.MakeToken(SyntaxKind.PartialKeyword));
            if (bInternalClass)
                modifiers.Add(SyntaxFactory.MakeToken(SyntaxKind.InternalKeyword));
            else
                modifiers.Add(SyntaxFactory.MakeToken(SyntaxKind.PublicKeyword));
            modifiers.Add(SyntaxFactory.MakeToken(SyntaxKind.StaticKeyword));
            MemberDeclarationSyntax r = 
                _syntaxFactory.ClassDeclaration(
                attributeLists: attributeLists,
                modifiers: modifiers.ToTokenList(),
                keyword: SyntaxFactory.MakeToken(SyntaxKind.ClassKeyword),
                identifier: SyntaxFactory.Identifier(className),
                typeParameterList: null,
                baseList: null, // BaseListSyntax baseList = _syntaxFactory.BaseList(colon, list)
                constraintClauses: default(SyntaxListBuilder<TypeParameterConstraintClauseSyntax>),
                openBraceToken: SyntaxFactory.MakeToken(SyntaxKind.OpenBraceToken),
                members: members,
                closeBraceToken: SyntaxFactory.MakeToken(SyntaxKind.CloseBraceToken),
                semicolonToken: SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken) ) /*))*/;
            _pool.Free(attributeLists);
            _pool.Free(modifiers);
            if(nameSpace.Length > 0) {
                r = GenerateNamespace(nameSpace, MakeList<MemberDeclarationSyntax>(r));
           }
            return r;
        }

        protected MemberDeclarationSyntax GenerateGlobalClass(string className, bool internalClass , params MemberDeclarationSyntax[] members )
        {
            SyntaxListBuilder<MemberDeclarationSyntax> globalClassMembers = _pool.Allocate<MemberDeclarationSyntax>();
            SyntaxListBuilder<AttributeListSyntax> attributeLists = _pool.Allocate<AttributeListSyntax>();
            string nameSpace;
            splitClassNameAndNamespace(ref className, out nameSpace);
            if(members.Length == 0) {
                var statements = _pool.Allocate<StatementSyntax>();
                GenerateAttributeList(attributeLists, CompilerGenerated);
                statements.Add(_syntaxFactory.EmptyStatement(SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken)));
                var block = MakeBlock(statements);
                _pool.Free(statements);

                members = new MemberDeclarationSyntax[] {
                    _syntaxFactory.ConstructorDeclaration(
                    attributeLists: attributeLists,
                    modifiers: TokenList(SyntaxKind.StaticKeyword),
                    identifier: SyntaxFactory.Identifier(className),
                    parameterList: EmptyParameterList(),
                    initializer: null, 
                    body: block,
                    semicolonToken: null )

                };
                GenerateAttributeList(attributeLists,"global::System.Runtime.CompilerServices.CompilerGlobalScope");
            }
            if(members.Length > 0) {
                foreach(var m in members)
                    globalClassMembers.Add(m);
            }
            SyntaxListBuilder modifiers = _pool.Allocate();
            modifiers.Add(SyntaxFactory.MakeToken(SyntaxKind.PartialKeyword));
            if (internalClass)
                modifiers.Add(SyntaxFactory.MakeToken(SyntaxKind.InternalKeyword));
            else
                modifiers.Add(SyntaxFactory.MakeToken(SyntaxKind.PublicKeyword));
            modifiers.Add(SyntaxFactory.MakeToken(SyntaxKind.StaticKeyword));
            MemberDeclarationSyntax r = 
                _syntaxFactory.ClassDeclaration(
                attributeLists: attributeLists, // will only be filled when the static constructor is created
                modifiers: modifiers.ToTokenList(),
                keyword: SyntaxFactory.MakeToken(SyntaxKind.ClassKeyword),
                identifier: SyntaxFactory.Identifier(className),
                typeParameterList: null,
                baseList: null, // BaseListSyntax baseList = _syntaxFactory.BaseList(colon, list)
                constraintClauses: default(SyntaxListBuilder<TypeParameterConstraintClauseSyntax>),
                openBraceToken: SyntaxFactory.MakeToken(SyntaxKind.OpenBraceToken),
                members: globalClassMembers,
                closeBraceToken: SyntaxFactory.MakeToken(SyntaxKind.CloseBraceToken),
                semicolonToken: SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken) ) /*))*/;
            _pool.Free(attributeLists);
            _pool.Free(modifiers);
            _pool.Free(globalClassMembers);
            if (nameSpace.Length > 0) {
                r = GenerateNamespace(nameSpace, MakeList<MemberDeclarationSyntax>(r));
            }
            return r;

        }
        /*
        protected MethodDeclarationSyntax GenerateMainMethod(string startMethodName)
        {
            SyntaxListBuilder<AttributeListSyntax> attributeLists = _pool.Allocate<AttributeListSyntax>();
            GenerateAttributeList(attributeLists, CompilerGenerated);
            SyntaxListBuilder modifiers = _pool.Allocate();
            modifiers.Add(SyntaxFactory.MakeToken(SyntaxKind.StaticKeyword));
            ParameterListSyntax paramList;
            {
                var parameters = _pool.AllocateSeparated<ParameterSyntax>();
                paramList = _syntaxFactory.ParameterList(SyntaxFactory.MakeToken(SyntaxKind.OpenParenToken),
                    parameters,
                    SyntaxFactory.MakeToken(SyntaxKind.CloseParenToken));
                _pool.Free(parameters);
            }
            BlockSyntax blockBody;
            {
                var statements = _pool.Allocate<StatementSyntax>();
                {
                    ArgumentListSyntax argList = _syntaxFactory.ArgumentList(
                        openParenToken: SyntaxFactory.MakeToken(SyntaxKind.OpenParenToken), 
                        arguments: default(SeparatedSyntaxList<ArgumentSyntax>), 
                        closeParenToken: SyntaxFactory.MakeToken(SyntaxKind.CloseParenToken));
                    statements.Add(GenerateExpressionStatement(GenerateMethodCall(startMethodName, argList)));
                }
                blockBody = MakeBlock(statements);
                _pool.Free(statements);
            }
            var r = _syntaxFactory.MethodDeclaration(
                attributeLists: attributeLists,
                modifiers: modifiers.ToTokenList(),
                returnType: VoidType(),
                explicitInterfaceSpecifier: null,
                identifier: SyntaxFactory.Identifier("Main"),
                typeParameterList: null,
                parameterList: paramList,
                constraintClauses: default(SyntaxListBuilder<TypeParameterConstraintClauseSyntax>),
                body: blockBody,
                expressionBody: null,
                semicolonToken: SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken));
            _pool.Free(attributeLists);
            _pool.Free(modifiers);
            return r;
        }
        */
        protected BasePropertyDeclarationSyntax GenerateVoProperty(SyntaxClassEntities.VoPropertyInfo vop) {
            var getMods = _pool.Allocate();
            var setMods = _pool.Allocate();
            var outerMods = _pool.Allocate();
            int getVisLvl;
            int setVisLvl;
            if (vop.AccessMethodCtx != null) {
                if (vop.AccessMethodCtx.Modifiers != null)
                    getMods.AddRange(vop.AccessMethodCtx.Modifiers.GetList<SyntaxToken>());
                else if (!vop.AccessMethodCtx.isInInterface()) {
                    getMods.FixDefaultVisibility();
                    if (_options.VirtualInstanceMethods)
                        getMods.FixDefaultVirtual();
                    else
                        getMods.FixDefaultMethod();
                }
                getVisLvl = getMods.GetVisibilityLevel();
            }
            else
                getVisLvl = 15;
            if (vop.AssignMethodCtx != null) {
                if (vop.AssignMethodCtx.Modifiers != null)
                    setMods.AddRange(vop.AssignMethodCtx.Modifiers.GetList<SyntaxToken>());
                else if (!vop.AssignMethodCtx.isInInterface()) {
                    setMods.FixDefaultVisibility();
                    if (_options.VirtualInstanceMethods)
                        setMods.FixDefaultVirtual();
                    else
                        setMods.FixDefaultMethod();
                }
                setVisLvl = setMods.GetVisibilityLevel();
            }
            else
                setVisLvl = 15;
            if (getVisLvl <= setVisLvl) {
                outerMods.AddRange(getMods);
                getMods.Clear();
            }
            else {
                outerMods.AddRange(setMods);
                setMods.Clear();
            }
            var rawMods = getVisLvl <= setVisLvl ? setMods : getMods;
            var innerMods = _pool.Allocate();
            for (int i = 0; i < rawMods.Count; i++) {
                var t = rawMods[i];
                if (!outerMods.Any(t.Kind)) {
                    if (!SyntaxFacts.IsAccessibilityModifier(t.Kind))
                        t = t.WithAdditionalDiagnostics(new SyntaxDiagnosticInfo(ErrorCode.ERR_BadMemberFlag, t));
                    innerMods.Add(t);
                }
            }
            _pool.Free(rawMods);
            if (getVisLvl <= setVisLvl) {
                setMods = innerMods;
            }
            else {
                getMods = innerMods;
            }

            TypeSyntax voPropType;
            var AccMet = vop.AccessMethodCtx;
            var AssMet = vop.AssignMethodCtx;
            if (AssMet != null && AssMet.ParamList != null && AssMet.ParamList._Params?.Count > 0)
            {
                voPropType = AssMet.ParamList._Params[0].Type?.Get<TypeSyntax>() ?? _getMissingLocalType();
            }
            else if (AccMet != null)
            {
                voPropType = AccMet.Type?.Get<TypeSyntax>() ?? _getMissingLocalType();
            }
            else
            {
                voPropType = _getMissingLocalType();
            }

            object voPropParams;
            ArgumentSyntax[] voPropArgs;
            if (AssMet?.ParamList?._Params?.Count > 1)
            {
                voPropParams = _syntaxFactory.BracketedParameterList(
                    SyntaxFactory.MakeToken(SyntaxKind.OpenBracketToken),
                    MakeSeparatedList<ParameterSyntax>(AssMet.ParamList._Params.Skip(1)),
                    SyntaxFactory.MakeToken(SyntaxKind.CloseBracketToken));
                voPropArgs = AssMet.ParamList._Params.Skip(1).Select(pCtx => _syntaxFactory.Argument(null, null, GenerateSimpleName(pCtx.Id.Start.Text))).ToArray();
            }
            else if (AccMet?.ParamList?._Params?.Count > 0)
            {
                voPropParams = _syntaxFactory.BracketedParameterList(
                    SyntaxFactory.MakeToken(SyntaxKind.OpenBracketToken),
                    MakeSeparatedList<ParameterSyntax>(AccMet.ParamList._Params),
                    SyntaxFactory.MakeToken(SyntaxKind.CloseBracketToken));
                voPropArgs = AccMet.ParamList._Params.Select(pCtx => _syntaxFactory.Argument(null, null, GenerateSimpleName(pCtx.Id.Start.Text))).ToArray();
            }
            else
            {
                voPropParams = null;
                voPropArgs = new ArgumentSyntax[0];
            }

            var accessors = _pool.Allocate<AccessorDeclarationSyntax>();
            if (vop.AccessMethodCtx != null) {
                bool isInInterfaceOrAbstract = vop.AccessMethodCtx.isInInterface() || outerMods.Any(SyntaxKind.AbstractKeyword) || outerMods.Any(SyntaxKind.ExternKeyword);
                accessors.Add(
                    _syntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration,EmptyList<AttributeListSyntax>(),getMods.ToTokenList(),
                        SyntaxFactory.MakeToken(SyntaxKind.GetKeyword),
                        isInInterfaceOrAbstract ? null
                        : MakeBlock(
                            MakeList<StatementSyntax>(_syntaxFactory.ReturnStatement(SyntaxFactory.MakeToken(SyntaxKind.ReturnKeyword),
                                GenerateMethodCall(VoPropertyAccessPrefix+vop.idName.Text,MakeArgumentList(voPropArgs)),
                                SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken)))
                            ),
                        isInInterfaceOrAbstract ? SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken)
                        : null)
                    );
            }
            if (vop.AssignMethodCtx != null) {
                bool isInInterfaceOrAbstract = vop.AssignMethodCtx.isInInterface() || outerMods.Any(SyntaxKind.AbstractKeyword) || outerMods.Any(SyntaxKind.ExternKeyword);
                accessors.Add(
                    _syntaxFactory.AccessorDeclaration(SyntaxKind.SetAccessorDeclaration,EmptyList<AttributeListSyntax>(),setMods.ToTokenList(),
                        SyntaxFactory.MakeToken(SyntaxKind.SetKeyword),
                        isInInterfaceOrAbstract ? null
                        : MakeBlock(
                            MakeList<StatementSyntax>(GenerateExpressionStatement(
                                GenerateMethodCall(VoPropertyAssignPrefix+vop.idName.Text,
                                    MakeArgumentList(voPropArgs.InsertAt(0, MakeArgument(GenerateSimpleName("value")))))))
                            ),
                        isInInterfaceOrAbstract ? SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken)
                        : null)
                    );
            }

            BasePropertyDeclarationSyntax prop;

            if (voPropParams != null)
            {
                prop = _syntaxFactory.IndexerDeclaration(
                    attributeLists: EmptyList<AttributeListSyntax>(),
                    modifiers: outerMods.ToTokenList(),
                    type: voPropType,
                    explicitInterfaceSpecifier: null,
                    thisKeyword: SyntaxFactory.MakeToken(SyntaxKind.ThisKeyword, vop.idName.Text),
                    parameterList: (BracketedParameterListSyntax)voPropParams,
                    accessorList: _syntaxFactory.AccessorList(SyntaxFactory.MakeToken(SyntaxKind.OpenBraceToken),
                        accessors,
                        SyntaxFactory.MakeToken(SyntaxKind.CloseBraceToken)),
                    expressionBody: null,
                    semicolonToken: SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken));
            }
            else
            {
                prop = _syntaxFactory.PropertyDeclaration(
                    attributeLists: EmptyList<AttributeListSyntax>(),
                    modifiers: outerMods.ToTokenList(),
                    type: voPropType,
                    explicitInterfaceSpecifier: null,
                    identifier: vop.idName,
                    accessorList: _syntaxFactory.AccessorList(SyntaxFactory.MakeToken(SyntaxKind.OpenBraceToken),
                        accessors,
                        SyntaxFactory.MakeToken(SyntaxKind.CloseBraceToken)),
                    expressionBody: null,
                    initializer: null,
                    semicolonToken: SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken));
            }

            _pool.Free(accessors);
            _pool.Free(getMods);
            _pool.Free(setMods);
            _pool.Free(outerMods);

            return prop;
        }

        private static SyntaxTree _defTree;
        public static SyntaxTree DefaultXSharpSyntaxTree()
        {
            if (_defTree == null) {
                var t = new XSharpTreeTransformation(null, CSharpParseOptions.Default, new SyntaxListPool(), new ContextAwareSyntax(new SyntaxFactoryContext()), "");

                t.GlobalEntities.Members.Add(t.GenerateGlobalClass(XSharpGlobalClassName,false));

                var eof = SyntaxFactory.Token(SyntaxKind.EndOfFileToken);
                _defTree = CSharpSyntaxTree.Create(
                    (Syntax.CompilationUnitSyntax)t._syntaxFactory.CompilationUnit(
                        t.GlobalEntities.Externs, t.GlobalEntities.Usings, t.GlobalEntities.Attributes, t.GlobalEntities.Members, eof).CreateRed());

            }
            return _defTree;
        }

        public override void VisitErrorNode([NotNull] IErrorNode node)
        {
        }

        public override void VisitTerminal(ITerminalNode node)
        {
        }

        public override void EnterEveryRule([NotNull] ParserRuleContext context)
        {
#if DEBUG && DUMP_TREE
            var s = context.GetType().ToString();
            s = s.Substring(s.LastIndexOfAny(".+".ToCharArray())+1);
            s = s.Replace("Context","");
            Debug.WriteLine("{0}=> ({1},{2}) {3} [{4}] <{5}>",new string(' ',context.Depth()),context.Start.Line,context.Start.Column,s,context.Start.Text,XP.DefaultVocabulary.GetSymbolicName(context.Start.Type));
#endif

            if (context is XP.IEntityContext)
                Entities.Push((XP.IEntityContext)context);
                
        }

        public override void ExitEveryRule([NotNull] ParserRuleContext context)
        {
            if (context.HasErrors() && context.CsNode != null && context.CsNode is CSharpSyntaxNode)
            {
                foreach (var e in context.ErrorData)
                {
                    var csNode = (CSharpSyntaxNode)context.CsNode;
                    context.Put(csNode.WithAdditionalDiagnostics(
                        new SyntaxDiagnosticInfo(csNode.GetLeadingTriviaWidth(), csNode.Width, e.Code, e.Args)));
                }
            }
#if DEBUG && DUMP_TREE
            var s = context.GetType().ToString();
            s = s.Substring(s.LastIndexOfAny(".+".ToCharArray())+1);
            s = s.Replace("Context","");
            Debug.WriteLine("{0}<= ({1},{2}) {3} '{4}'",new string(' ',context.Depth()),context.Start.Line,context.Start.Column,s,context.Start.Text);
#endif
            if (IsEntity(context))
                Entities.Pop();
        }

        public override void EnterSource([NotNull] XP.SourceContext context)
        {
            GlobalClassEntities = CreateClassEntities();
            ClassEntities.Push(GlobalClassEntities);
        }

        public override void ExitSource([NotNull] XP.SourceContext context)
        {
            var globalMembers = GlobalEntities.Members;
            if (!string.IsNullOrEmpty(_options.DefaultNamespace))
            {
                globalMembers = _pool.Allocate<MemberDeclarationSyntax>();
            }
            foreach(var entityCtx in context._Entities)
            {
                var s = entityCtx.CsNode;
                if (s is NamespaceDeclarationSyntax)
                    GlobalEntities.Members.Add(s as MemberDeclarationSyntax);
                else if (s is MemberDeclarationSyntax)
                    globalMembers.Add(s as MemberDeclarationSyntax);
                else if (s is UsingDirectiveSyntax)
                    GlobalEntities.Usings.Add(s as UsingDirectiveSyntax);
                else if (s is AttributeListSyntax)
                    GlobalEntities.Attributes.Add(s as AttributeListSyntax);
                else if (s is ExternAliasDirectiveSyntax)
                    GlobalEntities.Externs.Add(s as ExternAliasDirectiveSyntax);
            }

            var generated = ClassEntities.Pop();
            if(generated.Members.Count > 0) {
                globalMembers.Add(GenerateGlobalClass(GlobalClassName, false, generated.Members));
            }
            generated.Free();

            if (!string.IsNullOrEmpty(_options.DefaultNamespace))
            {
                GlobalEntities.Members.Add(_syntaxFactory.NamespaceDeclaration(SyntaxFactory.MakeToken(SyntaxKind.NamespaceKeyword),
                    name: GenerateQualifiedName(_options.DefaultNamespace),
                    openBraceToken: SyntaxFactory.MakeToken(SyntaxKind.OpenBraceToken),
                    externs: MakeList<ExternAliasDirectiveSyntax>(),
                    usings: MakeList<UsingDirectiveSyntax>(),
                    members: globalMembers,
                    closeBraceToken: SyntaxFactory.MakeToken(SyntaxKind.CloseBraceToken),
                    semicolonToken: SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken)));

                AddUsingWhenMissing(GlobalEntities.Usings, _options.DefaultNamespace, false);
            }

            // Add: using static Xs$Globals
            AddUsingWhenMissing(GlobalEntities.Usings, XSharpGlobalClassName, true);

            // Add: using System
            AddUsingWhenMissing(GlobalEntities.Usings, "System",false);
        }

        public override void ExitNamespace_([NotNull] XP.Namespace_Context context)
        {
            var externs = _pool.Allocate<ExternAliasDirectiveSyntax>();
            var usings = _pool.Allocate<UsingDirectiveSyntax>();
            var members = _pool.Allocate<MemberDeclarationSyntax>();
            foreach(var entityCtx in context._Entities)
            {
                var s = entityCtx.CsNode;
                if (s is MemberDeclarationSyntax)
                    members.Add(s as MemberDeclarationSyntax);
                else if (s is UsingDirectiveSyntax)
                    usings.Add(s as UsingDirectiveSyntax);
                else if (s is AttributeListSyntax)
                    //Attributes.Add(s as AttributeListSyntax);
                    context.AddError(new ParseErrorData(entityCtx, ErrorCode.ERR_AttributesNotAllowed));
                else if (s is ExternAliasDirectiveSyntax)
                    externs.Add(s as ExternAliasDirectiveSyntax);
            }
            context.Put(_syntaxFactory.NamespaceDeclaration(SyntaxFactory.MakeToken(SyntaxKind.NamespaceKeyword),
                name: context.Name.Get<NameSyntax>(),
                openBraceToken: SyntaxFactory.MakeToken(SyntaxKind.OpenBraceToken),
                externs: externs,
                usings: usings,
                members: members,
                closeBraceToken: SyntaxFactory.MakeToken(SyntaxKind.CloseBraceToken),
                semicolonToken: SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken)));
            _pool.Free(externs);
            _pool.Free(usings);
            _pool.Free(members);
        }
        
        public override void ExitEntity([NotNull] XP.EntityContext context)
        {
            var ch = context.children[0];
            bool bProcess = false, bStaticVisibility = false;
            if (ch is XP.FunctionContext)
            {
                bProcess = true;
                var modifiers =  ((XP.FunctionContext)ch).Modifiers;
                if (modifiers != null)
                    bStaticVisibility = modifiers.IsStaticVisible;
            }
            else if (ch is XP.ProcedureContext)
            {
                bProcess = true;
                var modifiers = ((XP.ProcedureContext)ch).Modifiers;
                if (modifiers != null)
                    bStaticVisibility = modifiers.IsStaticVisible;
            }
            else if (ch is XP.VoglobalContext)
            {
                bProcess = true;
                var modifiers = ((XP.VoglobalContext)ch).Modifiers;
                if (modifiers != null)
                    bStaticVisibility = modifiers.IsStaticVisible;
            }
            else if (ch is XP.VodefineContext)
            {
                bProcess = true;
                var modifiers = ((XP.VodefineContext)ch).Modifiers;
                if (modifiers != null)
                    bStaticVisibility = modifiers.IsStaticVisible;
            }
            else if (ch is XP.VodllContext)
            {
                bProcess = true;
                var modifiers = ((XP.VodllContext)ch).Modifiers;
                if (modifiers != null)
                    bStaticVisibility = modifiers.IsStaticVisible;
            }
            if (bProcess) {
                string className = GlobalClassName;
                if (bStaticVisibility) {
                    string filename = PathUtilities.GetFileName(_fileName);
                    filename = PathUtilities.RemoveExtension(filename);
                    if(className.Contains(".Functions"))
                        className = className.Replace(".Functions", ".$" + filename + "$.Functions");
                    else
                        className = className.Replace("$Globals", "$" + filename + "$Globals");
                }
                AddUsingWhenMissing(GlobalEntities.Usings, className, true);
                GlobalEntities.Members.Add(GenerateGlobalClass(className, bStaticVisibility, ch.Get<MemberDeclarationSyntax>()));
            }
             else {
                context.Put(ch.Get<CSharpSyntaxNode>());
            }
        }

        public override void ExitUsing_([NotNull] XP.Using_Context context)
        {
            context.Put(_syntaxFactory.UsingDirective(SyntaxFactory.MakeToken(SyntaxKind.UsingKeyword),
                staticKeyword: context.Static == null ? null : context.Static.SyntaxKeyword(),
                alias: context.Alias == null ? null : _syntaxFactory.NameEquals(context.Alias.Get<IdentifierNameSyntax>(),SyntaxFactory.MakeToken(SyntaxKind.EqualsToken)),
                name: context.Name.Get<NameSyntax>(),
                semicolonToken: SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken)));
        }

        public override void ExitVodefine([NotNull] XP.VodefineContext context)
        {
            var variables = _pool.AllocateSeparated<VariableDeclaratorSyntax>();
            variables.Add(GenerateVariable(context.Id.Get<SyntaxToken>(),
                context.Expr.Get<ExpressionSyntax>()));
            // RvdH May need to change to PUBLIC STATIC later. 
            // Const does not support unsafe types such as Ptr, but has the advantage
            // that it is in-lined when used
            // We can probably inspect the type and depending on the type switch between
            // public Const and public Static
            var list = _pool.Allocate();
            SyntaxList<SyntaxToken> modifiers = null;
            if (context.Modifiers != null) {
                // We are not including the "STATIC" modifier here. If there is any diagnostics attached
                // to this modifier, we will attach that to the Const Keyword that we are adding
                // This may happen if we have STATIC STATIC DEFINE Foo := 123 AS LONG
                DiagnosticInfo[] diags = null;
                foreach(var m in context.Modifiers.GetList<SyntaxToken>()) {
                    if (m.Kind != SyntaxKind.StaticKeyword) 
                        list.Add(m);
                    else if(m.ContainsDiagnostics)
                       diags = m.GetDiagnostics();
                }
                // Static define becomes an Internal define
                if(context.Modifiers.IsStaticVisible) 
                    list.Add(SyntaxFactory.MakeToken(SyntaxKind.InternalKeyword));
                var constToken = SyntaxFactory.MakeToken(SyntaxKind.ConstKeyword);
                if(diags != null)
                    constToken = constToken.WithAdditionalDiagnostics(diags);
                list.Add(constToken);
                modifiers = list.ToTokenList();
                _pool.Free(list);
            } else {
                // No modifiers, so a regular DEFINE Foo := "abc" AS STRING
                // Will be a public const
                modifiers = TokenList(SyntaxKind.PublicKeyword, SyntaxKind.ConstKeyword);
            }

                
            context.Put(_syntaxFactory.FieldDeclaration(
                EmptyList<AttributeListSyntax>(),
                modifiers,
                _syntaxFactory.VariableDeclaration(context.DataType?.Get<TypeSyntax>() ?? MissingType(), variables),
                SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken)));
            _pool.Free(variables);
        }

        public override void EnterInterface_([NotNull] XP.Interface_Context context)
        {
            ClassEntities.Push(CreateClassEntities());
        }

        public override void ExitInterface_([NotNull] XP.Interface_Context context)
        {
            var members = _pool.Allocate<MemberDeclarationSyntax>();
            foreach(var mCtx in context._Members) {
                if (mCtx.CsNode != null)
                    members.Add(mCtx.Get<MemberDeclarationSyntax>());
            }
            var generated = ClassEntities.Pop();
            if(generated.Members.Count > 0) {
                members.AddRange(generated.Members);
            }
            if (generated.VoProperties != null) {
                foreach(var vop in generated.VoProperties.Values) {
                    members.Add(GenerateVoProperty(vop));
                }
            }


            generated.Free();
            var baseTypes = _pool.AllocateSeparated<BaseTypeSyntax>();
            foreach(var pCtx in context._Parents) {
                if (baseTypes.Count>0)
                    baseTypes.AddSeparator(SyntaxFactory.MakeToken(SyntaxKind.CommaToken));
                baseTypes.Add(_syntaxFactory.SimpleBaseType(pCtx.Get<TypeSyntax>()));
            }
            MemberDeclarationSyntax m = _syntaxFactory.InterfaceDeclaration(
                attributeLists: context.Attributes?.GetList<AttributeListSyntax>() ?? EmptyList<AttributeListSyntax>(),
                modifiers: context.Modifiers?.GetList<SyntaxToken>() ?? TokenListWithDefaultVisibility(),
                keyword: SyntaxFactory.MakeToken(SyntaxKind.InterfaceKeyword),
                identifier: context.Id.Get<SyntaxToken>(),
                typeParameterList: context.TypeParameters?.Get<TypeParameterListSyntax>(),
                baseList: _syntaxFactory.BaseList(SyntaxFactory.MakeToken(SyntaxKind.ColonToken), baseTypes),
                constraintClauses: MakeList<TypeParameterConstraintClauseSyntax>(context._ConstraintsClauses),
                openBraceToken: SyntaxFactory.MakeToken(SyntaxKind.OpenBraceToken),
                members: members,
                closeBraceToken: SyntaxFactory.MakeToken(SyntaxKind.CloseBraceToken),
                semicolonToken: null);
            _pool.Free(members);
            _pool.Free(baseTypes);
            if (context.Namespace != null) {
                m = AddNameSpaceToMember(context.Namespace, m);
            }
            context.Put(m);
        }

        public override void ExitInterfaceModifiers([NotNull] XP.InterfaceModifiersContext context)
        {
            SyntaxListBuilder modifiers = _pool.Allocate();
            foreach (var m in context._Tokens)
            {
                modifiers.AddCheckUnique(m.SyntaxKeyword());
            }
            modifiers.FixDefaultVisibility();
            context.PutList(modifiers.ToTokenList());
            _pool.Free(modifiers);
        }

        public override void EnterClass_([NotNull] XP.Class_Context context)
        {
            ClassEntities.Push(CreateClassEntities());
        }

        public override void ExitClass_([NotNull] XP.Class_Context context)
        {
            var members = _pool.Allocate<MemberDeclarationSyntax>();
            foreach(var mCtx in context._Members) {
                if (mCtx.CsNode != null)
                    members.Add(mCtx.Get<MemberDeclarationSyntax>());
            }
            var generated = ClassEntities.Pop();
            if(generated.Members.Count > 0) {
                members.AddRange(generated.Members);
            }
            if (generated.VoProperties != null) {
                foreach(var vop in generated.VoProperties.Values) {
                    members.Add(GenerateVoProperty(vop));
                }
            }
            generated.Free();
            var baseTypes = _pool.AllocateSeparated<BaseTypeSyntax>();
            baseTypes.Add(_syntaxFactory.SimpleBaseType(context.BaseType?.Get<TypeSyntax>() 
                ?? _objectType));
            foreach(var iCtx in context._Implements) {
                baseTypes.AddSeparator(SyntaxFactory.MakeToken(SyntaxKind.CommaToken));
                baseTypes.Add(_syntaxFactory.SimpleBaseType(iCtx.Get<TypeSyntax>()));
            }
            MemberDeclarationSyntax m = _syntaxFactory.ClassDeclaration(
                attributeLists: context.Attributes?.GetList<AttributeListSyntax>() ?? EmptyList<AttributeListSyntax>(),
                modifiers: context.Modifiers?.GetList<SyntaxToken>() ?? TokenListWithDefaultVisibility(),
                keyword: SyntaxFactory.MakeToken(SyntaxKind.ClassKeyword),
                identifier: context.Id.Get<SyntaxToken>(),
                typeParameterList: context.TypeParameters?.Get<TypeParameterListSyntax>(),
                baseList: _syntaxFactory.BaseList(SyntaxFactory.MakeToken(SyntaxKind.ColonToken), baseTypes),
                constraintClauses: MakeList<TypeParameterConstraintClauseSyntax>(context._ConstraintsClauses),
                openBraceToken: SyntaxFactory.MakeToken(SyntaxKind.OpenBraceToken),
                members: members,
                closeBraceToken: SyntaxFactory.MakeToken(SyntaxKind.CloseBraceToken),
                semicolonToken: null);
            _pool.Free(members);
            _pool.Free(baseTypes);
            if (context.Namespace != null) {
                m = AddNameSpaceToMember(context.Namespace, m);

            }
            context.Put(m);
        }

        public override void ExitClassModifiers([NotNull] XP.ClassModifiersContext context)
        {
            SyntaxListBuilder modifiers = _pool.Allocate();
            foreach (var m in context._Tokens)
            {
                modifiers.AddCheckUnique(m.SyntaxKeyword());
            }
            modifiers.FixDefaultVisibility();
            context.PutList(modifiers.ToTokenList());
            _pool.Free(modifiers);
        }

        public override void EnterStructure_([NotNull] XP.Structure_Context context)
        {
            ClassEntities.Push(CreateClassEntities());
        }

        public override void ExitStructure_([NotNull] XP.Structure_Context context)
        {
            var members = _pool.Allocate<MemberDeclarationSyntax>();
            foreach(var mCtx in context._Members) {
                if (mCtx.CsNode != null)
                    members.Add(mCtx.Get<MemberDeclarationSyntax>());
            }
            var generated = ClassEntities.Pop();
            if(generated.Members.Count > 0) {
                members.AddRange(generated.Members);
            }
            if (generated.VoProperties != null) {
                foreach(var vop in generated.VoProperties.Values) {
                    members.Add(GenerateVoProperty(vop));
                }
            }
            generated.Free();
            var baseTypes = _pool.AllocateSeparated<BaseTypeSyntax>();
            foreach(var iCtx in context._Implements) {
                if (baseTypes.Count>0)
                    baseTypes.AddSeparator(SyntaxFactory.MakeToken(SyntaxKind.CommaToken));
                baseTypes.Add(_syntaxFactory.SimpleBaseType(iCtx.Get<TypeSyntax>()));
            }
            MemberDeclarationSyntax m = _syntaxFactory.StructDeclaration(
                attributeLists: context.Attributes?.GetList<AttributeListSyntax>() ?? EmptyList<AttributeListSyntax>(),
                modifiers: context.Modifiers?.GetList<SyntaxToken>() ?? TokenListWithDefaultVisibility(),
                keyword: SyntaxFactory.MakeToken(SyntaxKind.StructKeyword),
                identifier: context.Id.Get<SyntaxToken>(),
                typeParameterList: context.TypeParameters?.Get<TypeParameterListSyntax>(),
                baseList: _syntaxFactory.BaseList(SyntaxFactory.MakeToken(SyntaxKind.ColonToken), baseTypes),
                constraintClauses: MakeList<TypeParameterConstraintClauseSyntax>(context._ConstraintsClauses),
                openBraceToken: SyntaxFactory.MakeToken(SyntaxKind.OpenBraceToken),
                members: members,
                closeBraceToken: SyntaxFactory.MakeToken(SyntaxKind.CloseBraceToken),
                semicolonToken: null);
            _pool.Free(members);
            _pool.Free(baseTypes);
            if (context.Namespace != null) {
                m = AddNameSpaceToMember(context.Namespace, m);
            }
            context.Put(m);
        }

        public override void ExitStructureModifiers([NotNull] XP.StructureModifiersContext context)
        {
            SyntaxListBuilder modifiers = _pool.Allocate();
            foreach (var m in context._Tokens)
            {
                modifiers.AddCheckUnique(m.SyntaxKeyword());
            }
            modifiers.FixDefaultVisibility();
            context.PutList(modifiers.ToTokenList());
            _pool.Free(modifiers);
        }

        public override void ExitDelegate_([NotNull] XP.Delegate_Context context)
        {
            MemberDeclarationSyntax m = _syntaxFactory.DelegateDeclaration(
                attributeLists: context.Attributes?.GetList<AttributeListSyntax>() ?? EmptyList<AttributeListSyntax>(),
                modifiers: context.Modifiers?.GetList<SyntaxToken>() ?? TokenListWithDefaultVisibility(),
                delegateKeyword: SyntaxFactory.MakeToken(SyntaxKind.DelegateKeyword),
                returnType: context.Type?.Get<TypeSyntax>() ?? MissingType(),
                identifier: context.Id.Get<SyntaxToken>(),
                typeParameterList: context.TypeParameters?.Get<TypeParameterListSyntax>(),
                parameterList: context.ParamList?.Get<ParameterListSyntax>() ?? EmptyParameterList(),
                constraintClauses: MakeList<TypeParameterConstraintClauseSyntax>(context._ConstraintsClauses),
                semicolonToken: SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken));
            if (context.Namespace != null) {
                m = AddNameSpaceToMember(context.Namespace, m);
            }
            context.Put(m);
        }

        public override void ExitDelegateModifiers([NotNull] XP.DelegateModifiersContext context)
        {
            SyntaxListBuilder modifiers = _pool.Allocate();
            foreach (var m in context._Tokens)
            {
                modifiers.AddCheckUnique(m.SyntaxKeyword());
            }
            modifiers.FixDefaultVisibility();
            context.PutList(modifiers.ToTokenList());
            _pool.Free(modifiers);
        }

        public override void ExitEnum_([NotNull] XP.Enum_Context context)
        {
            //todo RvdH BaseType is not implemented
            MemberDeclarationSyntax m = _syntaxFactory.EnumDeclaration(
                attributeLists: context.Attributes?.GetList<AttributeListSyntax>() ?? EmptyList<AttributeListSyntax>(),
                modifiers: context.Modifiers?.GetList<SyntaxToken>() ?? TokenListWithDefaultVisibility(),
                enumKeyword: SyntaxFactory.MakeToken(SyntaxKind.EnumKeyword),
                identifier: context.Id.Get<SyntaxToken>(),
                baseList: default(BaseListSyntax),
                openBraceToken: SyntaxFactory.MakeToken(SyntaxKind.OpenBraceToken),
                members: MakeSeparatedList<EnumMemberDeclarationSyntax>(context._Members),
                closeBraceToken: SyntaxFactory.MakeToken(SyntaxKind.CloseBraceToken),
                semicolonToken: SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken));
            if (context.Namespace != null) {
                m = AddNameSpaceToMember(context.Namespace, m);
            }
            context.Put(m);
        }

        public override void ExitEnumModifiers([NotNull] XP.EnumModifiersContext context)
        {
            SyntaxListBuilder modifiers = _pool.Allocate();
            foreach (var m in context._Tokens)
            {
                modifiers.AddCheckUnique(m.SyntaxKeyword());
            }
            modifiers.FixDefaultVisibility();
            context.PutList(modifiers.ToTokenList());
            _pool.Free(modifiers);
        }

        public override void ExitEnummember([NotNull] XP.EnummemberContext context)
        {
            context.Put(_syntaxFactory.EnumMemberDeclaration(
                attributeLists: context.Attributes?.GetList<AttributeListSyntax>() ?? EmptyList<AttributeListSyntax>(),
                identifier: context.Id.Get<SyntaxToken>(),
                equalsValue: context.Expr == null ? null : _syntaxFactory.EqualsValueClause(SyntaxFactory.MakeToken(SyntaxKind.EqualsToken),
                    context.Expr.Get<ExpressionSyntax>())));
        }

        public override void ExitEvent_([NotNull] XP.Event_Context context)
        {
            var attrLists = context.Attributes?.GetList<AttributeListSyntax>() ?? EmptyList<AttributeListSyntax>();
            var type_ = context.Type?.Get<TypeSyntax>() ?? MissingType();
            var singleLine = context._LineAccessors != null && context._LineAccessors.Count > 0;
            var multiLine = context._Accessors != null && context._Accessors.Count > 0;
            ExplicitInterfaceSpecifierSyntax explif = null;
            if (context.ExplicitIface != null)
            {
                explif = _syntaxFactory.ExplicitInterfaceSpecifier(
                        name: context.ExplicitIface.Get<NameSyntax>(),
                        dotToken: SyntaxFactory.MakeToken(SyntaxKind.DotToken));
            }
                var mods = context.Modifiers?.GetList<SyntaxToken>() ?? DefaultMethodModifiers(context.isInInterface());
            //if (context.ExplicitIface != null)
            {
                var m = _pool.Allocate();
                foreach (var mod in mods)
                {
                    if (singleLine || multiLine)
                    { 
                        if (mod.Kind != SyntaxKind.VirtualKeyword && mod.Kind != SyntaxKind.OverrideKeyword )
                            m.Add(mod);
                    }
                    else
                    {
                        if (mod.Kind != SyntaxKind.VirtualKeyword && mod.Kind != SyntaxKind.OverrideKeyword && mod.Kind != SyntaxKind.PublicKeyword)
                            m.Add(mod);
                    }
                }
                mods = m.ToTokenList();
                _pool.Free(m);
            }

            if (singleLine)         // Single Line Syntax
            {
                var acclist = _syntaxFactory.AccessorList(
                    SyntaxFactory.MakeToken(SyntaxKind.OpenBraceToken),
                    MakeList<AccessorDeclarationSyntax>(context._LineAccessors),
                    SyntaxFactory.MakeToken(SyntaxKind.CloseBraceToken));
                context.Put(_syntaxFactory.EventDeclaration(
                    attributeLists: attrLists,
                    modifiers: mods,
                    eventKeyword: SyntaxFactory.MakeToken(SyntaxKind.EventKeyword),
                    type: type_,
                    explicitInterfaceSpecifier: explif,
                    identifier: context.Id.Get<SyntaxToken>(),
                    accessorList: acclist));
            }
            else if (multiLine)        // Multi line Syntax
            {
                var acclist = _syntaxFactory.AccessorList(
                    SyntaxFactory.MakeToken(SyntaxKind.OpenBraceToken),
                    MakeList<AccessorDeclarationSyntax>(context._Accessors),
                    SyntaxFactory.MakeToken(SyntaxKind.CloseBraceToken));
                context.Put(_syntaxFactory.EventDeclaration(
                    attributeLists: attrLists,
                    modifiers: mods,
                    eventKeyword: SyntaxFactory.MakeToken(SyntaxKind.EventKeyword),
                    type: type_,
                    explicitInterfaceSpecifier: explif,
                    identifier: context.Id.Get<SyntaxToken>(),
                    accessorList: acclist));
            }
            else // Old Syntax, auto generate accessors
            {
                if (context.ExplicitIface != null)
                {
                    string evtFldName = EventFieldNamePrefix + context.Id.Get<SyntaxToken>();
                    ClassEntities.Peek().Members.Add(
                        _syntaxFactory.FieldDeclaration(
                            EmptyList<AttributeListSyntax>(),
                            TokenList(SyntaxKind.StaticKeyword, SyntaxKind.InternalKeyword),
                            _syntaxFactory.VariableDeclaration(context.Type?.Get<TypeSyntax>() ?? MissingType(),
                                MakeSeparatedList(GenerateVariable(evtFldName))),
                            SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken))
                        );
                    var add_ = _syntaxFactory.AccessorDeclaration(SyntaxKind.AddAccessorDeclaration,
                            attributeLists: EmptyList<AttributeListSyntax>(),
                            modifiers: EmptyList(),
                            keyword: SyntaxFactory.MakeToken(SyntaxKind.AddKeyword),
                            body: MakeBlock(
                                _syntaxFactory.LockStatement(SyntaxFactory.MakeToken(SyntaxKind.LockKeyword),
                                    SyntaxFactory.MakeToken(SyntaxKind.OpenParenToken),
                                    GenerateSimpleName(evtFldName),
                                    SyntaxFactory.MakeToken(SyntaxKind.CloseParenToken),
                                    GenerateExpressionStatement(
                                        _syntaxFactory.AssignmentExpression(SyntaxKind.AddAssignmentExpression,
                                            GenerateSimpleName(evtFldName),
                                            SyntaxFactory.MakeToken(SyntaxKind.PlusEqualsToken),
                                            GenerateSimpleName("value"))))
                                ), semicolonToken: null);
                    var remove_ = _syntaxFactory.AccessorDeclaration(SyntaxKind.RemoveAccessorDeclaration,
                                    attributeLists: EmptyList<AttributeListSyntax>(),
                                    modifiers: EmptyList(),
                                    keyword: SyntaxFactory.MakeToken(SyntaxKind.RemoveKeyword),
                                    body: MakeBlock(_syntaxFactory.LockStatement(SyntaxFactory.MakeToken(SyntaxKind.LockKeyword),
                                            SyntaxFactory.MakeToken(SyntaxKind.OpenParenToken),
                                            GenerateSimpleName(evtFldName),
                                            SyntaxFactory.MakeToken(SyntaxKind.CloseParenToken),
                                            GenerateExpressionStatement(
                                                _syntaxFactory.AssignmentExpression(SyntaxKind.SubtractAssignmentExpression,
                                                    GenerateSimpleName(evtFldName),
                                                    SyntaxFactory.MakeToken(SyntaxKind.MinusEqualsToken),
                                                    GenerateSimpleName("value"))))
                                        ), semicolonToken: null);
                    var acclist = _syntaxFactory.AccessorList(
                        SyntaxFactory.MakeToken(SyntaxKind.OpenBraceToken),
                        MakeList<AccessorDeclarationSyntax>(add_, remove_),
                        SyntaxFactory.MakeToken(SyntaxKind.CloseBraceToken));
                    context.Put(_syntaxFactory.EventDeclaration(
                        attributeLists: attrLists,
                        modifiers: mods,
                        eventKeyword: SyntaxFactory.MakeToken(SyntaxKind.EventKeyword),
                        type: type_,
                        explicitInterfaceSpecifier: explif,
                        identifier: context.Id.Get<SyntaxToken>(),
                        accessorList: acclist));
                }
                else
                {
                    context.Put(_syntaxFactory.EventFieldDeclaration(
                        attributeLists: context.Attributes?.GetList<AttributeListSyntax>() ?? EmptyList<AttributeListSyntax>(),
                        modifiers: context.Modifiers?.GetList<SyntaxToken>() ?? DefaultMethodModifiers(context.isInInterface()),
                        eventKeyword: SyntaxFactory.MakeToken(SyntaxKind.EventKeyword),
                        declaration: _syntaxFactory.VariableDeclaration(
                            context.Type?.Get<TypeSyntax>() ?? MissingType(),
                            MakeSeparatedList<VariableDeclaratorSyntax>(
                                GenerateVariable(context.Id.Get<SyntaxToken>()))),
                        semicolonToken: SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken)));
                }
            }
        }

        public override void ExitEventLineAccessor([NotNull] XP.EventLineAccessorContext context)
        {
            context.Put(_syntaxFactory.AccessorDeclaration(context.Key.AccessorKind(),
                attributeLists: context.Attributes?.GetList<AttributeListSyntax>() ?? EmptyList<AttributeListSyntax>(),
                modifiers: context.Modifiers?.GetList<SyntaxToken>() ?? EmptyList(),
                keyword: context.Key.SyntaxKeyword(),
                body: MakeBlock(context.ExprList?.GetList<StatementSyntax>() ?? EmptyList<StatementSyntax>()),
                semicolonToken: SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken)));
        }

        public override void ExitEventAccessor([NotNull] XP.EventAccessorContext context)
        {
            context.Put(_syntaxFactory.AccessorDeclaration(context.Key.AccessorKind(),
                attributeLists: context.Attributes?.GetList<AttributeListSyntax>() ?? EmptyList<AttributeListSyntax>(),
                modifiers: context.Modifiers?.GetList<SyntaxToken>() ?? EmptyList(),
                keyword: context.Key.SyntaxKeyword(),
                body: context.StmtBlk.Get<BlockSyntax>(),
                semicolonToken: SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken)));
        }


        public override void ExitEventModifiers([NotNull] XP.EventModifiersContext context)
        {
            SyntaxListBuilder modifiers = _pool.Allocate();
            foreach (var m in context._Tokens)
            {
                modifiers.AddCheckUnique(m.SyntaxKeyword());
            }
            if (!context.Parent.isInInterface())
            {
                modifiers.FixDefaultVisibility();
                if (_options.VirtualInstanceMethods)
                    modifiers.FixDefaultVirtual();
                else
                    modifiers.FixDefaultMethod();
            }
            context.PutList(modifiers.ToTokenList());
            _pool.Free(modifiers);
        }

        public override void ExitClassvars([NotNull] XP.ClassvarsContext context)
        {
            var varList = _pool.AllocateSeparated<VariableDeclaratorSyntax>();
            var varType = context.Vars?.DataType?.Get<TypeSyntax>() ?? MissingType();
            varType.XVoDecl = true;
            if (context.Vars?.As?.Type == XP.IS)
            {
                varType.XVoIsDecl = true;
            }
            foreach (var varCtx in context.Vars._Var) {
                bool isDim = varCtx.Dim != null && varCtx.ArraySub != null;
                if (isDim) {
                    ClassEntities.Peek().Members.Add(_syntaxFactory.FieldDeclaration(
                        attributeLists: context.Attributes?.GetList<AttributeListSyntax>() ?? EmptyList<AttributeListSyntax>(),
                        modifiers: context.Modifiers?.GetList<SyntaxToken>() ?? TokenListWithDefaultVisibility(),
                        declaration: _syntaxFactory.VariableDeclaration(
                            type: _syntaxFactory.ArrayType(varType, MakeArrayRankSpecifier(varCtx.ArraySub._ArrayIndex.Count)),
                            variables: MakeSeparatedList(varCtx.Get<VariableDeclaratorSyntax>())),
                        semicolonToken: SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken)));
                }
                else {
                    if (varList.Count > 0)
                        varList.AddSeparator(SyntaxFactory.MakeToken(SyntaxKind.CommaToken));
                    varList.Add(varCtx.Get<VariableDeclaratorSyntax>());
                }
            }
            if (varList.Count > 0) {
                context.Put(_syntaxFactory.FieldDeclaration(
                    attributeLists: context.Attributes?.GetList<AttributeListSyntax>() ?? EmptyList<AttributeListSyntax>(),
                    modifiers: context.Modifiers?.GetList<SyntaxToken>() ?? TokenListWithDefaultVisibility(),
                    declaration: _syntaxFactory.VariableDeclaration(
                        type: varType,
                        variables: varList),
                    semicolonToken: SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken)));
            }
            _pool.Free(varList);
        }

        public override void ExitClassvarModifiers([NotNull] XP.ClassvarModifiersContext context)
        {
            SyntaxListBuilder modifiers = _pool.Allocate();
            foreach (var m in context._Tokens)
            {
                modifiers.AddCheckUnique(m.SyntaxKeyword());
            }
            modifiers.FixDefaultVisibility();
            context.PutList(modifiers.ToTokenList());
            _pool.Free(modifiers);
        }

        public override void ExitClassVarList([NotNull] XP.ClassVarListContext context)
        {
            foreach(var cvCtx in context._Var)
                VisitClassvar(cvCtx);
        }

        public override void EnterClassvar([NotNull] XP.ClassvarContext context)
        {
            bool isDim = context.Dim != null;
            bool hasArraySub = context.ArraySub != null;
            if (isDim && !hasArraySub) {
                context.AddError(new ParseErrorData(context.DIM(), ErrorCode.ERR_ArrayInitializerExpected));
            }
            if (!isDim && hasArraySub) {
                context.ArraySub.AddError(new ParseErrorData(ErrorCode.ERR_FeatureNotAvailableInDialect, "Indexed Class variable", _options.Dialect.ToString()));
            }
        }

        public override void ExitClassvar([NotNull] XP.ClassvarContext context)
        {
            // nvk: Not handled here due to datatype, which is processed later
        }

        public void VisitClassvar([NotNull] XP.ClassvarContext context)
        {
            bool isDim = context.Dim != null && context.ArraySub != null;
            var initExpr = context.Initializer?.Get<ExpressionSyntax>();
            if (isDim) {
                var varType = ((XP.ClassVarListContext)context.Parent).DataType?.Get<TypeSyntax>() ?? MissingType();
                if (initExpr == null) {
                    initExpr = _syntaxFactory.ArrayCreationExpression(SyntaxFactory.MakeToken(SyntaxKind.NewKeyword),
                        _syntaxFactory.ArrayType(varType,context.ArraySub.Get<ArrayRankSpecifierSyntax>()),
                        null);
                }
            }
            context.Put(GenerateVariable(context.Id.Get<SyntaxToken>(),initExpr));
        }

        public override void ExitProperty([NotNull] XP.PropertyContext context)
        {
            var isInInterface = context.isInInterface();
            var isExtern = context.Modifiers?._EXTERN != null;
            var isAbstract = context.Modifiers?._ABSTRACT != null;
            bool HasBody = (context.Auto != null || context.Multi != null);
            if ( !HasBody)
            {
                foreach (var aCtx in context._LineAccessors)
                {
                    if (aCtx.Expr != null && aCtx.ExprList != null)
                    {
                        HasBody = true;
                    }
                }
            }
            if (HasBody)
            { 
                if (isInInterface)
                {
                    context.AddError(new ParseErrorData(context.Start, ErrorCode.ERR_InterfaceMemberHasBody));
                }
                if (isExtern)
                {
                    context.AddError(new ParseErrorData(context.Start, ErrorCode.ERR_ExternHasBody, "Property"));
                }
                if (isAbstract) {
                    context.AddError(new ParseErrorData(context.Start, ErrorCode.ERR_AbstractHasBody));
                }
            }
            if (isAbstract && context.Modifiers?._EXTERN != null)
            {
                    context.AddError(new ParseErrorData(context.Modifiers, ErrorCode.ERR_AbstractAndExtern));
            }
            var mods = context.Modifiers?.GetList<SyntaxToken>() ?? DefaultMethodModifiers(isInInterface);
            if (context.ExplicitIface != null)
            {
                var m = _pool.Allocate();
                foreach (var mod in mods)
                {
                    if (mod.Kind != SyntaxKind.VirtualKeyword && mod.Kind != SyntaxKind.OverrideKeyword && mod.Kind != SyntaxKind.PublicKeyword)
                        m.Add(mod);
                }
                mods = m.ToTokenList();
                _pool.Free(m);
            }
            if (context.ParamList == null || context.ParamList._Params.Count == 0)
                context.Put(_syntaxFactory.PropertyDeclaration(
                    attributeLists: context.Attributes?.GetList<AttributeListSyntax>() ?? EmptyList<AttributeListSyntax>(),
                    modifiers: mods,
                    type: context.Type?.Get<TypeSyntax>() ?? MissingType(),
                    explicitInterfaceSpecifier: context.ExplicitIface == null ? null : _syntaxFactory.ExplicitInterfaceSpecifier(
                        name: context.ExplicitIface.Get<NameSyntax>(),
                        dotToken: SyntaxFactory.MakeToken(SyntaxKind.DotToken)),
                    identifier: context.Id.Get<SyntaxToken>(),
                    accessorList: _syntaxFactory.AccessorList(SyntaxFactory.MakeToken(SyntaxKind.OpenBraceToken),
                        (context.Auto != null) ? 
                            ((context._AutoAccessors?.Count ?? 0) > 0) ? MakeList<AccessorDeclarationSyntax>(context._AutoAccessors) :
                            MakeList(_syntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration,EmptyList<AttributeListSyntax>(),EmptyList(),
                                    SyntaxFactory.MakeToken(SyntaxKind.GetKeyword),null,SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken)),
                                _syntaxFactory.AccessorDeclaration(SyntaxKind.SetAccessorDeclaration,EmptyList<AttributeListSyntax>(),EmptyList(),
                                    SyntaxFactory.MakeToken(SyntaxKind.SetKeyword),null,SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken))) :
                        ((context._LineAccessors?.Count ?? 0) > 0) ? MakeList<AccessorDeclarationSyntax>(context._LineAccessors) :
                        MakeList<AccessorDeclarationSyntax>(context._Accessors),
                        SyntaxFactory.MakeToken(SyntaxKind.CloseBraceToken)),
                    expressionBody: null,
                    initializer: context.Initializer != null ? _syntaxFactory.EqualsValueClause(SyntaxFactory.MakeToken(SyntaxKind.EqualsToken),
                        context.Initializer.Get<ExpressionSyntax>()) : null,
                    semicolonToken: SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken)));
            else {
                if (context.Auto != null)
                    context.AddError(new ParseErrorData(context.AUTO(),ErrorCode.ERR_SyntaxError,SyntaxFactory.MakeToken(SyntaxKind.GetKeyword)));
                context.Put(_syntaxFactory.IndexerDeclaration(
                    attributeLists: context.Attributes?.GetList<AttributeListSyntax>() ?? EmptyList<AttributeListSyntax>(),
                    modifiers: mods,
                    type: context.Type?.Get<TypeSyntax>() ?? MissingType(),
                    explicitInterfaceSpecifier: context.ExplicitIface == null ? null : _syntaxFactory.ExplicitInterfaceSpecifier(
                        name: context.ExplicitIface.Get<NameSyntax>(),
                        dotToken: SyntaxFactory.MakeToken(SyntaxKind.DotToken)),
                    thisKeyword: SyntaxFactory.MakeToken(SyntaxKind.ThisKeyword, context.Id?.Start.Text ?? ""),
                    parameterList: context.ParamList.Get<BracketedParameterListSyntax>(),
                    accessorList: _syntaxFactory.AccessorList(SyntaxFactory.MakeToken(SyntaxKind.OpenBraceToken),
                        (context.Auto != null) ? 
                            (context._AutoAccessors?.Count > 0) ? MakeList<AccessorDeclarationSyntax>(context._AutoAccessors) :
                            MakeList(_syntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration,EmptyList<AttributeListSyntax>(),EmptyList(),
                                    SyntaxFactory.MakeToken(SyntaxKind.GetKeyword),null,SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken)),
                                _syntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration,EmptyList<AttributeListSyntax>(),EmptyList(),
                                    SyntaxFactory.MakeToken(SyntaxKind.SetKeyword),null,SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken))) :
                        (context._LineAccessors?.Count > 0) ? MakeList<AccessorDeclarationSyntax>(context._LineAccessors) :
                        MakeList<AccessorDeclarationSyntax>(context._Accessors),
                        SyntaxFactory.MakeToken(SyntaxKind.CloseBraceToken)),
                    expressionBody: null, // TODO: (grammar) expressionBody methods
                    semicolonToken: SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken)));
            }
        }

        public override void ExitPropertyParameterList([NotNull] XP.PropertyParameterListContext context)
        {
            var @params = _pool.AllocateSeparated<ParameterSyntax>();
            if (context._Params.Count > 0)
            {
                foreach (var paramCtx in context._Params)
                {
                    if (@params.Count > 0)
                        @params.AddSeparator(SyntaxFactory.MakeToken(SyntaxKind.CommaToken));
                    @params.Add(paramCtx.Get<ParameterSyntax>());
                }
                context.Put(_syntaxFactory.BracketedParameterList(
                    SyntaxFactory.MakeToken(SyntaxKind.OpenBracketToken),
                    @params,
                    SyntaxFactory.MakeToken(SyntaxKind.CloseBracketToken)));
                _pool.Free(@params);
            }
        }

        public override void ExitPropertyAutoAccessor([NotNull] XP.PropertyAutoAccessorContext context)
        {
            context.Put(_syntaxFactory.AccessorDeclaration(context.Key.AccessorKind(),
                attributeLists: context.Attributes?.GetList<AttributeListSyntax>() ?? EmptyList<AttributeListSyntax>(),
                modifiers: context.Modifiers?.GetList<SyntaxToken>() ?? EmptyList(),
                keyword: context.Key.SyntaxKeyword(),
                body: null,
                semicolonToken: SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken)));
        }

        public override void ExitPropertyLineAccessor([NotNull] XP.PropertyLineAccessorContext context)
        {
            bool forceBody = false;
            if (context.Key.Type == XP.SET && context.ExprList == null)
            {
                var property = context.Parent as XP.PropertyContext;
                var isExtern = property.Modifiers?._EXTERN != null;
                var isAbstract = property.Modifiers?._ABSTRACT != null;
                if (!isExtern && !isAbstract && !property.isInInterface() && property._LineAccessors.Count > 1 &&
                    (property._LineAccessors[0].Expr != null || property._LineAccessors[1].Expr != null))
                {
                    forceBody = true;
                }
            }
            context.Put(_syntaxFactory.AccessorDeclaration(context.Key.AccessorKind(),
                attributeLists: context.Attributes?.GetList<AttributeListSyntax>() ?? EmptyList<AttributeListSyntax>(),
                modifiers: context.Modifiers?.GetList<SyntaxToken>() ?? EmptyList(),
                keyword: context.Key.SyntaxKeyword(),
                body: context.Key.Type == XP.GET ? 
                    ( context.Expr == null ? null : MakeBlock(
                        MakeList<StatementSyntax>(_syntaxFactory.ReturnStatement(SyntaxFactory.MakeToken(SyntaxKind.ReturnKeyword),
                            context.Expr.Get<ExpressionSyntax>(),SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken)))
                        ) )
                    : (context.ExprList == null && !forceBody) ? null 
                    : MakeBlock(context.ExprList?.GetList<StatementSyntax>() ?? EmptyList<StatementSyntax>())
                    ,
                semicolonToken: SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken)));
        }

        public override void ExitExpressionList([NotNull] XP.ExpressionListContext context)
        {
            var stmts = _pool.Allocate<StatementSyntax>();
            foreach(var eCtx in context._Exprs) {
                stmts.Add(GenerateExpressionStatement(eCtx.Get<ExpressionSyntax>()));
            }
            context.PutList(stmts.ToList());
            _pool.Free(stmts);
        }

        public override void ExitPropertyAccessor([NotNull] XP.PropertyAccessorContext context)
        {
            context.Put(_syntaxFactory.AccessorDeclaration(context.Key.AccessorKind(),
                attributeLists: context.Attributes?.GetList<AttributeListSyntax>() ?? EmptyList<AttributeListSyntax>(),
                modifiers: context.Modifiers?.GetList<SyntaxToken>() ?? EmptyList(),
                keyword: context.Key.SyntaxKeyword(),
                body: context.StmtBlk.Get<BlockSyntax>(),
                semicolonToken: SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken)));
        }

         public override void ExitMethod([NotNull] XP.MethodContext context)
        {
            var idName = context.Id.Get<SyntaxToken>();
            var isInInterface = context.isInInterface();
            var mods = context.Modifiers?.GetList<SyntaxToken>() ?? DefaultMethodModifiers(isInInterface);
            var isExtern = mods.Any(SyntaxKind.ExternKeyword);
            var isAbstract = mods.Any(SyntaxKind.AbstractKeyword);
            var hasNoBody = isInInterface || isExtern || isAbstract;
            if (isInInterface && context.StmtBlk != null && context.StmtBlk._Stmts.Count > 0) {
                context.AddError(new ParseErrorData(context.Id, ErrorCode.ERR_InterfaceMemberHasBody));
            }
            if (isInInterface && context.ClassId != null) {
                context.AddError(new ParseErrorData(context.ClassId, ErrorCode.ERR_InterfacesCannotContainTypes));
            }
            if (isAbstract) {
                if (isExtern) {
                    context.AddError(new ParseErrorData(context.Modifiers, ErrorCode.ERR_AbstractAndExtern));
                }
                if (context.StmtBlk?._Stmts?.Count > 0) {
                    context.AddError(new ParseErrorData(context.StmtBlk, ErrorCode.ERR_AbstractHasBody));
                }
                context.StmtBlk = null;
            }
            else if (isExtern) {
                if (context.StmtBlk?._Stmts?.Count > 0) {
                    context.AddError(new ParseErrorData(context.StmtBlk, ErrorCode.ERR_ExternHasBody, "Method"));
                }
                context.StmtBlk = null;
            }
            bool actualDeclaration = true;
            if (context.T.Token.Type != XP.METHOD) {
                string sName;
                switch (context.T.Token.Type) {
                    case XP.ACCESS:
                        sName = context.Id.GetText();
                        if(sName.StartsWith("@@"))
                            sName = sName.Substring(2);
                        idName = SyntaxFactory.Identifier(VoPropertyAccessPrefix + sName);
                        idName.XNode = context.Id;
                        break;
                    case XP.ASSIGN:
                        sName = context.Id.GetText();
                        if(sName.StartsWith("@@"))
                            sName = sName.Substring(2);
                        idName = SyntaxFactory.Identifier(VoPropertyAssignPrefix + sName);
                        idName.XNode = context.Id;
                        break;
                }
                var vomods = _pool.Allocate();
                vomods.Add(SyntaxFactory.MakeToken(SyntaxKind.PrivateKeyword));
                if (mods.Any(SyntaxKind.StaticKeyword))
                    vomods.Add(SyntaxFactory.MakeToken(SyntaxKind.StaticKeyword));
                if (mods.Any(SyntaxKind.UnsafeKeyword))
                    vomods.Add(SyntaxFactory.MakeToken(SyntaxKind.UnsafeKeyword));
                if (hasNoBody)
                    actualDeclaration = false;
                mods = vomods.ToTokenList();
                _pool.Free(vomods);
            }
            else
            {
                if (context.ParamList?._Params.Count > 0 && context.ParamList?._Params[0].Self != null && !mods.Any(SyntaxKind.StaticKeyword))
                {
                    var m = _pool.Allocate();
                    m.AddRange(mods);
                    m.Add(SyntaxFactory.MakeToken(SyntaxKind.StaticKeyword));
                    mods = m.ToTokenList();
                    _pool.Free(m);
                }
            }
            if (context.ExplicitIface != null)
            {
                var m = _pool.Allocate();
                foreach(var mod in mods)
                {
                    if (mod.Kind != SyntaxKind.VirtualKeyword && mod.Kind != SyntaxKind.OverrideKeyword && mod.Kind != SyntaxKind.PublicKeyword)
                        m.Add(mod);
                }
                mods = m.ToTokenList();
                _pool.Free(m);
            }
            if (actualDeclaration) {
                var attributes = context.Attributes?.GetList<AttributeListSyntax>() ?? EmptyList<AttributeListSyntax>();
                var parameters = context.ParamList?.Get<ParameterListSyntax>() ?? EmptyParameterList();
                var body = hasNoBody ? null : context.StmtBlk.Get<BlockSyntax>();
                var returntype = context.Type?.Get<TypeSyntax>();
                if (returntype == null)
                {
                    if (context.T.Token.Type == XP.ASSIGN)
                    {
                        returntype = VoidType();
                    }
                    else  // method and access
                    {
                        returntype = _getMissingLocalType();
                    }
                }

                ImplementClipperAndPSZ(context, ref attributes, ref parameters, ref body, ref returntype);
                body = AddMissingReturnStatement(body, context.StmtBlk, returntype);

                MemberDeclarationSyntax m = _syntaxFactory.MethodDeclaration(
                    attributeLists: attributes,
                    modifiers: mods,
                    returnType: returntype,
                    explicitInterfaceSpecifier: context.ExplicitIface == null ? null : _syntaxFactory.ExplicitInterfaceSpecifier(
                        name: context.ExplicitIface.Get<NameSyntax>(),
                        dotToken: SyntaxFactory.MakeToken(SyntaxKind.DotToken)),
                    identifier: idName,
                    typeParameterList: context.TypeParameters?.Get<TypeParameterListSyntax>(),
                    parameterList: parameters,
                    constraintClauses: MakeList<TypeParameterConstraintClauseSyntax>(context._ConstraintsClauses),
                    body:  body,
                    expressionBody: null, // TODO: (grammar) expressionBody methods
                    semicolonToken: (!hasNoBody && context.StmtBlk != null) ? null : SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken));

                if(context.ClassId != null) {
                    bool GenClass = true;
                    if(context.isInClass()) {
                        string parentName;
                        XP.Class_Context parent = null;
                        if(context.Parent is XP.Class_Context) {
                            parent = context.Parent as XP.Class_Context;
                        } else if(context.Parent.Parent is XP.Class_Context) {
                            parent = context.Parent.Parent as XP.Class_Context;
                        }
                        if(parent != null) {
                            parentName = parent.Id.GetText();
                            if(parent.Namespace != null)
                                parentName = parent.Namespace.GetText() + parentName;
                            string className;
                            className = context.ClassId.GetText();
                            if(context.Namespace != null)
                                className = context.Namespace.GetText() + className;
                            if(String.Compare(parentName, className, StringComparison.OrdinalIgnoreCase) != 0) {
                                m = m.WithAdditionalDiagnostics(
                                new SyntaxDiagnosticInfo(
                                        ErrorCode.ERR_NestedMethodMustHaveSameNameAsParentClass, className, parentName));
                            } else
                                GenClass = false;

                                
                        }
                    }
                    if(!m.ContainsDiagnostics && GenClass) {
                        m = _syntaxFactory.ClassDeclaration(
                            attributeLists: EmptyList<AttributeListSyntax>(),
                            modifiers: TokenList(SyntaxKind.PartialKeyword),
                            keyword: SyntaxFactory.MakeToken(SyntaxKind.ClassKeyword),
                            identifier: context.ClassId.Get<SyntaxToken>(),
                            typeParameterList: default(TypeParameterListSyntax),
                            baseList: default(BaseListSyntax),
                            constraintClauses: default(SyntaxList<TypeParameterConstraintClauseSyntax>),
                            openBraceToken: SyntaxFactory.MakeToken(SyntaxKind.OpenBraceToken),
                            members: MakeList<MemberDeclarationSyntax>(m),
                            closeBraceToken: SyntaxFactory.MakeToken(SyntaxKind.CloseBraceToken),
                            semicolonToken: null);
                    }
                    if(context.Namespace != null) {
                        m = AddNameSpaceToMember(context.Namespace, m);
                    }
                }
                context.Put(m);
            }
            if (context.T.Token.Type != XP.METHOD) {
                ClassEntities.Peek().AddVoPropertyAccessor(context);
            }
        }

        public override void ExitTypeparameters([NotNull] XP.TypeparametersContext context)
        {
            var parameters = _pool.AllocateSeparated<TypeParameterSyntax>();
            foreach(var tpCtx in context._TypeParams) {
                if (parameters.Count>0)
                    parameters.AddSeparator(SyntaxFactory.MakeToken(SyntaxKind.CommaToken));
                parameters.Add(tpCtx.Get<TypeParameterSyntax>());
            }
            context.Put(_syntaxFactory.TypeParameterList(SyntaxFactory.MakeToken(SyntaxKind.LessThanToken),
                parameters,
                SyntaxFactory.MakeToken(SyntaxKind.GreaterThanToken)));
            _pool.Free(parameters);
        }

        public override void ExitTypeparameter([NotNull] XP.TypeparameterContext context)
        {
            context.Put(_syntaxFactory.TypeParameter(
                attributeLists: context.Attributes?.GetList<AttributeListSyntax>() ?? EmptyList<AttributeListSyntax>(),
                varianceKeyword: context.VarianceKeyword?.SyntaxKeyword(),
                identifier: context.Id.Get<SyntaxToken>()));
        }

        public override void ExitTypeparameterconstraintsclause([NotNull] XP.TypeparameterconstraintsclauseContext context)
        {
            var constraints = _pool.AllocateSeparated<TypeParameterConstraintSyntax>();
            foreach(var cCtx in context._Constraints) {
                if (constraints.Count > 0)
                    constraints.AddSeparator(SyntaxFactory.MakeToken(SyntaxKind.CommaToken));
                constraints.Add(cCtx.Get<TypeParameterConstraintSyntax>());
            }
            context.Put(_syntaxFactory.TypeParameterConstraintClause(
                SyntaxFactory.MakeToken(SyntaxKind.WhereKeyword),
                context.Name.Get<IdentifierNameSyntax>(),
                SyntaxFactory.MakeToken(SyntaxKind.ColonToken),
                constraints));
            _pool.Free(constraints);
        }

        public override void ExitTypeConstraint([NotNull] XP.TypeConstraintContext context)
        {
            context.Put(_syntaxFactory.TypeConstraint(context.Type?.Get<TypeSyntax>() ?? MissingType()));
        }

        public override void ExitClassOrStructConstraint([NotNull] XP.ClassOrStructConstraintContext context)
        {
            context.Put(_syntaxFactory.ClassOrStructConstraint(
                context.Key.ConstraintKind(),
                context.Key.SyntaxKeyword()));
        }

        public override void ExitConstructorConstraint([NotNull] XP.ConstructorConstraintContext context)
        {
            context.Put(_syntaxFactory.ConstructorConstraint(
                SyntaxFactory.MakeToken(SyntaxKind.NewKeyword),
                SyntaxFactory.MakeToken(SyntaxKind.OpenParenToken),
                SyntaxFactory.MakeToken(SyntaxKind.CloseParenToken)));
        }

        public override void ExitMethodtype([NotNull] XP.MethodtypeContext context)
        {
            // nvk: Handled by the method rule
        }

        public override void ExitConstructorModifiers([NotNull] XP.ConstructorModifiersContext context)
        {
            SyntaxListBuilder modifiers = _pool.Allocate();
            foreach (var m in context._Tokens)
            {
                modifiers.AddCheckUnique(m.SyntaxKeyword());
            }
            if (!modifiers.Any(SyntaxKind.StaticKeyword))
                modifiers.FixDefaultVisibility();
            context.PutList(modifiers.ToTokenList());
            _pool.Free(modifiers);
        }

        public override void ExitDestructorModifiers([NotNull] XP.DestructorModifiersContext context)
        {
            SyntaxListBuilder modifiers = _pool.Allocate();
            foreach (var m in context._Tokens)
            {
                modifiers.AddCheckUnique(m.SyntaxKeyword());
            }
            context.PutList(modifiers.ToTokenList());
            _pool.Free(modifiers);
        }

        public override void ExitMemberModifiers([NotNull] XP.MemberModifiersContext context)
        {
            SyntaxListBuilder modifiers = _pool.Allocate();
            foreach (var m in context._Tokens)
            {
                modifiers.AddCheckUnique(m.SyntaxKeyword());
            }
            if (!context.Parent.isInInterface())
            {
                modifiers.FixDefaultVisibility();
                if (_options.VirtualInstanceMethods)
                    modifiers.FixDefaultVirtual();
                else
                    modifiers.FixDefaultMethod();
            }
            context.PutList(modifiers.ToTokenList());
            _pool.Free(modifiers);
        }

        public override void ExitOperator_([NotNull] XP.Operator_Context context)
        {
            if (context.Modifiers?._EXTERN != null) {
                if (context.StmtBlk?._Stmts?.Count > 0) {
                    context.AddError(new ParseErrorData(context.StmtBlk, ErrorCode.ERR_ExternHasBody,"Operator"));
                }
                context.StmtBlk = null;
            }
            if (context.Conversion != null)
                context.Put(_syntaxFactory.ConversionOperatorDeclaration(
                    attributeLists: context.Attributes?.GetList<AttributeListSyntax>() ?? EmptyList<AttributeListSyntax>(),
                    modifiers: context.Modifiers?.GetList<SyntaxToken>() ?? TokenListWithDefaultVisibility(false, SyntaxKind.StaticKeyword),
                    implicitOrExplicitKeyword: context.Conversion.Get<SyntaxToken>(),
                    operatorKeyword: SyntaxFactory.MakeToken(SyntaxKind.OperatorKeyword),
                    type: context.Type?.Get<TypeSyntax>() ?? MissingType(),
                    parameterList: context.ParamList?.Get<ParameterListSyntax>() ?? EmptyParameterList(),
                    body: context.StmtBlk.Get<BlockSyntax>(),
                    expressionBody: null, // TODO: (grammar) expressionBody methods
                    semicolonToken: (context.StmtBlk != null) ? null : SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken)));
            else
            { 
                SyntaxToken opToken;
                if (context.Operation.Token.Type == XP.GT && context.Gt != null  ) // right shift
                {
                    opToken = GetRShiftToken(context.Operation.Token, context.Gt);
                }
                else
                {
                    opToken = context.Operation.Get<SyntaxToken>();
                }
                // differentiate between unary and primary operators
                // overloadedops only handles binary operators
                // So for unary operator remap the token here.

                if(context.ParamList?._Params.Count ==1) {
                    opToken = context.Operation.Token.SyntaxPrefixOp();
                }
                context.Put(_syntaxFactory.OperatorDeclaration(
                    attributeLists: context.Attributes?.GetList<AttributeListSyntax>() ?? EmptyList<AttributeListSyntax>(),
                    modifiers: context.Modifiers?.GetList<SyntaxToken>() ?? TokenListWithDefaultVisibility(false, SyntaxKind.StaticKeyword),
                    returnType: context.Type?.Get<TypeSyntax>() ?? MissingType(),
                    operatorKeyword: SyntaxFactory.MakeToken(SyntaxKind.OperatorKeyword),
                    operatorToken: opToken,
                    parameterList: context.ParamList?.Get<ParameterListSyntax>() ?? EmptyParameterList(),
                    body: context.StmtBlk.Get<BlockSyntax>(),
                    expressionBody: null, // TODO: (grammar) expressionBody methods
                    semicolonToken: (context.StmtBlk != null) ? null : SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken)));
            }
        }

        public override void ExitOperatorModifiers([NotNull] XP.OperatorModifiersContext context)
        {
            SyntaxListBuilder modifiers = _pool.Allocate();
            foreach (var m in context._Tokens)
            {
                modifiers.AddCheckUnique(m.SyntaxKeyword());
            }
            if (!modifiers.Any(SyntaxKind.StaticKeyword))
                modifiers.Add(SyntaxFactory.MakeToken(SyntaxKind.StaticKeyword));
            modifiers.FixDefaultVisibility();
            context.PutList(modifiers.ToTokenList());
            _pool.Free(modifiers);
        }

        public override void ExitOverloadedOps([NotNull] XP.OverloadedOpsContext context)
        {
            switch (context.Token.Type) {
                case XP.OR:
                    // Please note that C# does not have an operator ||
                    context.Put(SyntaxFactory.MakeToken(SyntaxKind.BarToken));
                    break;
                case XP.AND:
                    // Please note that C# does not have an operator &&
                    context.Put(SyntaxFactory.MakeToken(SyntaxKind.AmpersandToken));
                    break;
                default:
                    context.Put(context.Token.SyntaxOp());
                    break;
            }
        }

        public override void ExitConversionOps([NotNull] XP.ConversionOpsContext context)
        {
            context.Put(context.Token.SyntaxKeyword());
        }

        public override void ExitClsmethod([NotNull] XP.ClsmethodContext context)
        {
            if (context.Member.CsNode != null)
                context.Put(context.Member.Get<MemberDeclarationSyntax>());
        }

        public override void ExitClsctor([NotNull] XP.ClsctorContext context)
        {
            if (context.Modifiers?._EXTERN != null) {
                if (context.StmtBlk?._Stmts?.Count > 0) {
                    context.AddError(new ParseErrorData(context.StmtBlk, ErrorCode.ERR_ExternHasBody, "Constructor"));
                }
                context.StmtBlk = null;
            }
            if (context.isInInterface()) {
                context.AddError(new ParseErrorData(context.CONSTRUCTOR(), ErrorCode.ERR_InterfacesCantContainConstructors));
            }
            else {
                var attributes = context.Attributes?.GetList<AttributeListSyntax>() ?? EmptyList<AttributeListSyntax>();
                var parameters = context.ParamList?.Get<ParameterListSyntax>() ?? EmptyParameterList();
                var body = context.StmtBlk?.Get<BlockSyntax>();
                TypeSyntax returntype = null;
                ImplementClipperAndPSZ(context, ref attributes, ref parameters, ref body, ref returntype);
                // no return statement needed  in CONSTRUCTOR
                // body = AddMissingReturnStatement(body, context.StmtBlk, null);
                var parentId = (context.Parent as XP.Class_Context)?.Id.Get<SyntaxToken>()
                    ?? (context.Parent as XP.Structure_Context)?.Id.Get<SyntaxToken>()
                    ?? (context.Parent as XP.Interface_Context)?.Id.Get<SyntaxToken>();
                context.Put(_syntaxFactory.ConstructorDeclaration(
                    attributeLists: attributes,
                    modifiers: context.Modifiers?.GetList<SyntaxToken>() ?? TokenListWithDefaultVisibility(),
                    identifier: parentId,
                    parameterList: parameters,
                    initializer: context.Chain == null ? null : 
                        _syntaxFactory.ConstructorInitializer(context.Chain.CtorInitializerKind(),
                            SyntaxFactory.MakeToken(SyntaxKind.ColonToken),
                            context.Chain.SyntaxKeyword(),
                            context.ArgList?.Get<ArgumentListSyntax>() ?? EmptyArgumentList()),
                    body: body,
                    semicolonToken: (context.StmtBlk?._Stmts?.Count > 0) ? null : SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken)));
            }
        }

        public override void ExitClsdtor([NotNull] XP.ClsdtorContext context)
        {
            if (context.Modifiers?._EXTERN != null) {
                if (context.StmtBlk?._Stmts?.Count > 0) {
                    context.AddError(new ParseErrorData(context.StmtBlk, ErrorCode.ERR_ExternHasBody, "Destructor"));
                }
                context.StmtBlk = null;
            }
            if (context.isInInterface()) {
                context.AddError(new ParseErrorData(context.DESTRUCTOR(), ErrorCode.ERR_InterfacesCantContainConstructors));
            }
            else {
                // no return statement needed in DESTRUCTOR
                // body = AddMissingReturnStatement(body, context.StmtBlk, null);
                var parentId = (context.Parent as XP.Class_Context)?.Id.Get<SyntaxToken>()
                    ?? (context.Parent as XP.Structure_Context)?.Id.Get<SyntaxToken>()
                    ?? (context.Parent as XP.Interface_Context)?.Id.Get<SyntaxToken>();
                context.Put(_syntaxFactory.DestructorDeclaration(
                    attributeLists: context.Attributes?.GetList<AttributeListSyntax>() ?? EmptyList<AttributeListSyntax>(),
                    modifiers: context.Modifiers?.GetList<SyntaxToken>() ?? EmptyList<SyntaxToken>(),
                    tildeToken: SyntaxFactory.MakeToken(SyntaxKind.TildeToken),
                    identifier: parentId,
                    parameterList: EmptyParameterList(),
                    body: context.StmtBlk.Get<BlockSyntax>(),
                    semicolonToken: (context.StmtBlk != null) ? null : SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken)));
            }
        }

        public override void ExitClsvars([NotNull] XP.ClsvarsContext context)
        {
            if (context.isInInterface()) {
                context.AddError(new ParseErrorData(context.Member, ErrorCode.ERR_InterfacesCantContainFields));
            }
            else if (context.Member.CsNode != null)
                context.Put(context.Member.Get<MemberDeclarationSyntax>());
        }

        public override void ExitClsproperty([NotNull] XP.ClspropertyContext context)
        {
            context.Put(context.Member.Get<MemberDeclarationSyntax>());
        }

        public override void ExitClsoperator([NotNull] XP.ClsoperatorContext context)
        {
            if (context.isInInterface()) {
                context.AddError(new ParseErrorData(context.Member, ErrorCode.ERR_InterfacesCantContainOperators));
            }
            else
                context.Put(context.Member.Get<MemberDeclarationSyntax>());
        }

        public override void ExitNestedStructure([NotNull] XP.NestedStructureContext context)
        {
            if (context.isInInterface()) {
                context.AddError(new ParseErrorData(context.Member, ErrorCode.ERR_InterfacesCannotContainTypes));
            }
            else
                context.Put(context.Member.Get<MemberDeclarationSyntax>());
        }

        public override void ExitNestedClass([NotNull] XP.NestedClassContext context)
        {
            if (context.isInInterface()) {
                context.AddError(new ParseErrorData(context.Member, ErrorCode.ERR_InterfacesCannotContainTypes));
            }
            else
                context.Put(context.Member.Get<MemberDeclarationSyntax>());
        }

        public override void ExitNestedDelegate([NotNull] XP.NestedDelegateContext context)
        {
            if (context.isInInterface()) {
                context.AddError(new ParseErrorData(context.Member, ErrorCode.ERR_InterfacesCannotContainTypes));
            }
            else
                context.Put(context.Member.Get<MemberDeclarationSyntax>());
        }

        public override void ExitNestedEnum([NotNull] XP.NestedEnumContext context)
        {
            if (context.isInInterface()) {
                context.AddError(new ParseErrorData(context.Member, ErrorCode.ERR_InterfacesCannotContainTypes));
            }
            else
                context.Put(context.Member.Get<MemberDeclarationSyntax>());
        }

        public override void ExitNestedEvent([NotNull] XP.NestedEventContext context)
        {
            context.Put(context.Member.Get<MemberDeclarationSyntax>());
        }

        public override void ExitNestedInterface([NotNull] XP.NestedInterfaceContext context)
        {
            if (context.isInInterface()) {
                context.AddError(new ParseErrorData(context.Member, ErrorCode.ERR_InterfacesCannotContainTypes));
            }
            else
                context.Put(context.Member.Get<MemberDeclarationSyntax>());
        }

        public override void ExitClsfunction([NotNull] XP.ClsfunctionContext context)
        {
            context.Put(context.Member.Get<MemberDeclarationSyntax>());
        }

        public override void ExitClsprocedure([NotNull] XP.ClsprocedureContext context)
        {
            context.Put(context.Member.Get<MemberDeclarationSyntax>());
        }

        public override void ExitAttributes([NotNull] XP.AttributesContext context)
        {
            var attributeLists = _pool.Allocate<AttributeListSyntax>();
            foreach(var attrBlkCtx in context._AttrBlk) {
                attributeLists.Add(attrBlkCtx.Get<AttributeListSyntax>());
            }
            context.PutList(attributeLists.ToList());
            _pool.Free(attributeLists);
        }

        public override void ExitAttributeBlock([NotNull] XP.AttributeBlockContext context)
        {
            var attributes = _pool.AllocateSeparated<AttributeSyntax>();
            foreach(var attrCtx in context._Attributes) {
                if (attributes.Count > 0)
                {
                    attributes.AddSeparator(SyntaxFactory.MakeToken(SyntaxKind.CommaToken));
                }
                attributes.Add(attrCtx.Get<AttributeSyntax>());
            }
            context.Put(_syntaxFactory.AttributeList(
                SyntaxFactory.MakeToken(SyntaxKind.OpenBracketToken),
                context.Target?.Get<AttributeTargetSpecifierSyntax>(),
                attributes,
                SyntaxFactory.MakeToken(SyntaxKind.CloseBracketToken)));
            _pool.Free(attributes);
        }

        public override void ExitAttributeTarget([NotNull] XP.AttributeTargetContext context)
        {
            context.Put(_syntaxFactory.AttributeTargetSpecifier(
                context.Id?.Get<SyntaxToken>() ?? context.Kw.Get<SyntaxToken>(),
                SyntaxFactory.MakeToken(SyntaxKind.ColonToken)));
        }

        public override void ExitAttribute([NotNull] XP.AttributeContext context)
        {
            var arguments = _pool.AllocateSeparated<AttributeArgumentSyntax>();
            if (context._Params != null) {
                foreach (var paramCtx in context._Params) {
                    if (arguments.Count != 0) {
                        arguments.AddSeparator(SyntaxFactory.MakeToken(SyntaxKind.CommaToken));
                    }
                    arguments.Add(paramCtx.Get<AttributeArgumentSyntax>());
                }
            }
            context.Put(_syntaxFactory.Attribute(
                name: context.Name.Get<NameSyntax>(),
                argumentList: _syntaxFactory.AttributeArgumentList(SyntaxFactory.MakeToken(SyntaxKind.OpenParenToken),
                    arguments,
                    SyntaxFactory.MakeToken(SyntaxKind.CloseParenToken))));
            _pool.Free(arguments);
        }

        public override void ExitPropertyAttributeParam([NotNull] XP.PropertyAttributeParamContext context)
        {
            context.Put(_syntaxFactory.AttributeArgument(
                _syntaxFactory.NameEquals(context.Name.Get<IdentifierNameSyntax>(), 
                    SyntaxFactory.MakeToken(SyntaxKind.EqualsToken)), 
                null, // TODO: (grammar) name: attr arg syntax?
                context.Expr.Get<ExpressionSyntax>()));
        }

        public override void ExitExprAttributeParam([NotNull] XP.ExprAttributeParamContext context)
        {
            context.Put(_syntaxFactory.AttributeArgument(null, null, context.Expr.Get<ExpressionSyntax>()));
        }

        public override void ExitGlobalAttributes([NotNull] XP.GlobalAttributesContext context)
        {
            var attributes = _pool.AllocateSeparated<AttributeSyntax>();
            foreach(var attrCtx in context._Attributes) {
                if (attributes.Count > 0)
                {
                    attributes.AddSeparator(SyntaxFactory.MakeToken(SyntaxKind.CommaToken));
                }
                attributes.Add(attrCtx.Get<AttributeSyntax>());
            }
            context.Put(_syntaxFactory.AttributeList(
                SyntaxFactory.MakeToken(SyntaxKind.OpenBracketToken),
                context.Target.Get<AttributeTargetSpecifierSyntax>(),
                attributes,
                SyntaxFactory.MakeToken(SyntaxKind.CloseBracketToken)));
            _pool.Free(attributes);
        }

        public override void ExitGlobalAttributeTarget([NotNull] XP.GlobalAttributeTargetContext context)
        {
            context.Put(_syntaxFactory.AttributeTargetSpecifier(
                context.Token.SyntaxKeywordIdentifier(),
                SyntaxFactory.MakeToken(SyntaxKind.ColonToken)));
        }

        public override void EnterVoglobal([NotNull] XP.VoglobalContext context)
        {
            if (context.Const != null) {
                if (context.Modifiers != null)
                    context.Modifiers._Tokens.Add(context.Const);
                else {
                    context.Modifiers = FixPosition(new XP.FuncprocModifiersContext(context,0),context.Start);
                    context.Modifiers.PutList(TokenList(SyntaxKind.ConstKeyword,SyntaxKind.PublicKeyword));
                }
            }
        }

        public override void ExitVoglobal([NotNull] XP.VoglobalContext context)
        {
            var varList = _pool.AllocateSeparated<VariableDeclaratorSyntax>();
            var varType = context.Vars.DataType?.Get<TypeSyntax>() ?? MissingType();
            varType.XVoDecl = true;
            if (context.Vars?.As?.Type == XP.IS)
            {
                varType.XVoIsDecl = true;
            }
            foreach (var varCtx in context.Vars._Var) {
                bool isDim = varCtx.Dim != null && varCtx.ArraySub != null;
                if (isDim) {
                    GlobalClassEntities.Members.Add(_syntaxFactory.FieldDeclaration(
                        attributeLists: context.Attributes?.GetList<AttributeListSyntax>() ?? EmptyList<AttributeListSyntax>(),
                        modifiers: context.Modifiers?.GetList<SyntaxToken>() ?? TokenListWithDefaultVisibility(false, SyntaxKind.StaticKeyword),
                        declaration: _syntaxFactory.VariableDeclaration(
                            type: _syntaxFactory.ArrayType(varType, MakeArrayRankSpecifier(varCtx.ArraySub._ArrayIndex.Count)),
                            variables: MakeSeparatedList(varCtx.Get<VariableDeclaratorSyntax>())),
                        semicolonToken: SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken)));
                }
                else {
                    if (varList.Count > 0)
                        varList.AddSeparator(SyntaxFactory.MakeToken(SyntaxKind.CommaToken));
                    varList.Add(varCtx.Get<VariableDeclaratorSyntax>());
                }
            }
            if (varList.Count > 0) {
                context.Put(_syntaxFactory.FieldDeclaration(
                    attributeLists: context.Attributes?.GetList<AttributeListSyntax>() ?? EmptyList<AttributeListSyntax>(),
                    modifiers: context.Modifiers?.GetList<SyntaxToken>() ?? TokenListWithDefaultVisibility(false, SyntaxKind.StaticKeyword),
                    declaration: _syntaxFactory.VariableDeclaration(
                        type: varType,
                        variables: varList),
                    semicolonToken: SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken)));
            }
            _pool.Free(varList);
        }

        public override void ExitNestedPragma([NotNull] XP.NestedPragmaContext context)
        {
            // TODO
        }

        public override void ExitPragmaOptions([NotNull] XP.PragmaOptionsContext context)
        {
            // TODO
        }

        public override void ExitPragmaswitch([NotNull] XP.PragmaswitchContext context)
        {
            // TODO
        }

        public override void ExitPragmaWarnings([NotNull] XP.PragmaWarningsContext context)
        {
            // TODO
        }

        public override void EnterVodll([NotNull] XP.VodllContext context)
        {
            if (context.Modifiers != null) {
                context.Modifiers._Tokens.Add(_parser.TokenFactory.Create(XP.EXTERN,""));
            }
        }

        
        public override void ExitVodll([NotNull] XP.VodllContext context)
        {
            string dllName = context.Dll.GetText();
            if (context.Extension != null) {
                dllName += "." + context.Extension.GetText();
            }
            ExpressionSyntax dllExpr = GenerateLiteral(dllName);
            ExpressionSyntax EntrypointExpr;
            if(context.Ordinal != null) {
                EntrypointExpr = GenerateLiteral(context.Ordinal.Text).WithAdditionalDiagnostics(
                    new SyntaxDiagnosticInfo(ErrorCode.ERR_InvalidDLLEntryPoint, context.Ordinal.Text));
            } else
                EntrypointExpr = GenerateLiteral(context.Entrypoint.GetText());
            context.Put(_syntaxFactory.MethodDeclaration(
                attributeLists: MakeList(
                    _syntaxFactory.AttributeList(
                        openBracketToken: SyntaxFactory.MakeToken(SyntaxKind.OpenBracketToken),
                        target: null,
                        attributes: MakeSeparatedList(
                            _syntaxFactory.Attribute(
                                name: GenerateQualifiedName("global::System.Runtime.InteropServices.DllImport"),
                                argumentList: _syntaxFactory.AttributeArgumentList(
                                    openParenToken: SyntaxFactory.MakeToken(SyntaxKind.OpenParenToken),
                                    arguments: MakeSeparatedList(
                                        _syntaxFactory.AttributeArgument(null,null,dllExpr),
                                        _syntaxFactory.AttributeArgument(GenerateNameEquals("EntryPoint"),null, EntrypointExpr),
                                        _syntaxFactory.AttributeArgument(GenerateNameEquals("SetLastError"),null, GenerateLiteral(true)),
                                        _syntaxFactory.AttributeArgument(GenerateNameEquals("ExactSpelling"), null, GenerateLiteral(true)),
                                        context.CharSet != null ? _syntaxFactory.AttributeArgument(GenerateNameEquals("Charset"), null,
                                                _syntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, GenerateQualifiedName("global::System.Runtime.InteropServices.CharSet"), 
                                                    SyntaxFactory.MakeToken(SyntaxKind.DotToken), _syntaxFactory.IdentifierName(context.CharSet.SyntaxIdentifier())))
                                            : null,
                                        context.CallingConvention?.Get<AttributeArgumentSyntax>()
                                    ),
                                    closeParenToken: SyntaxFactory.MakeToken(SyntaxKind.CloseParenToken))
                                )
                            ),
                        closeBracketToken: SyntaxFactory.MakeToken(SyntaxKind.CloseBracketToken))
                    ),
                modifiers: context.Modifiers?.GetList<SyntaxToken>() ?? TokenListWithDefaultVisibility(false, SyntaxKind.StaticKeyword,SyntaxKind.ExternKeyword),
                returnType: context.Type?.Get<TypeSyntax>() ?? (context.T.Type == XP.FUNCTION ? MissingType() : VoidType()),
                explicitInterfaceSpecifier: null,
                identifier: context.Id.Get<SyntaxToken>(),
                typeParameterList: null,
                parameterList: context.ParamList?.Get<ParameterListSyntax>() ?? EmptyParameterList(),
                constraintClauses: default(SyntaxList<TypeParameterConstraintClauseSyntax>),
                body: null,
                expressionBody: null,
                semicolonToken: SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken)));
        }

        public override void ExitDllcallconv([NotNull] XP.DllcallconvContext context)
        {
            string conv = null;
            switch (context.Cc.Type) {
                case XP.CLIPPER:
                case XP.STRICT:
                    break;
                case XP.PASCAL:
                    conv = "global::System.Runtime.InteropServices.CallingConvention.StdCall";
                    break;
                case XP.THISCALL:
                    conv = "global::System.Runtime.InteropServices.CallingConvention.ThisCall";
                    break;
                case XP.FASTCALL:
                    conv = "global::System.Runtime.InteropServices.CallingConvention.Cdecl";
                    break;
            }
            if (conv != null && conv != "") {
                context.Put(_syntaxFactory.AttributeArgument(
                    GenerateNameEquals("CallingConvention"),
                    null,
                    GenerateQualifiedName(conv)));
            }
        }

        public override void ExitVostruct([NotNull] XP.VostructContext context)
        {
            MemberDeclarationSyntax m = _syntaxFactory.StructDeclaration(
                attributeLists: MakeList(
                    _syntaxFactory.AttributeList(
                        openBracketToken: SyntaxFactory.MakeToken(SyntaxKind.OpenBracketToken),
                        target: null,
                        attributes: MakeSeparatedList(
                            _syntaxFactory.Attribute(
                                name: GenerateQualifiedName("global::System.Runtime.InteropServices.StructLayout"),
                                argumentList: _syntaxFactory.AttributeArgumentList(
                                    openParenToken: SyntaxFactory.MakeToken(SyntaxKind.OpenParenToken),
                                    arguments: MakeSeparatedList(
                                        _syntaxFactory.AttributeArgument(null,null,GenerateQualifiedName("global::System.Runtime.InteropServices.LayoutKind.Sequential")),
                                        _syntaxFactory.AttributeArgument(GenerateNameEquals("Pack"),null,
                                            context.Alignment == null ?
                                                GenerateLiteral("8", 8)
                                                : _syntaxFactory.LiteralExpression(context.Alignment.ExpressionKindLiteral(), context.Alignment.SyntaxLiteralValue(_options)))
                                    ),
                                    closeParenToken: SyntaxFactory.MakeToken(SyntaxKind.CloseParenToken))
                                ),
                            _options.IsDialectVO ?
                                _syntaxFactory.Attribute(
                                    name: GenerateQualifiedName("global::Vulcan.Internal.VOStruct"),
                                    argumentList: _syntaxFactory.AttributeArgumentList(
                                        openParenToken: SyntaxFactory.MakeToken(SyntaxKind.OpenParenToken),
                                        arguments: MakeSeparatedList(
                                            _syntaxFactory.AttributeArgument(null, null, GenerateLiteral(0)),
                                            _syntaxFactory.AttributeArgument(null, null, GenerateLiteral(0))
                                        ),
                                        closeParenToken: SyntaxFactory.MakeToken(SyntaxKind.CloseParenToken))
                                    )
                                : null
                            ),
                        closeBracketToken: SyntaxFactory.MakeToken(SyntaxKind.CloseBracketToken))
                    ),
                modifiers: context.Modifiers?.GetList<SyntaxToken>() ?? TokenListWithDefaultVisibility(),
                keyword: SyntaxFactory.MakeToken(SyntaxKind.StructKeyword),
                identifier: context.Id.Get<SyntaxToken>(),
                typeParameterList: null,
                baseList: null,
                constraintClauses: null,
                openBraceToken: SyntaxFactory.MakeToken(SyntaxKind.OpenBraceToken),
                members: (context._Members?.Count > 0) ? MakeList<MemberDeclarationSyntax>(context._Members) : EmptyList<MemberDeclarationSyntax>(),
                closeBraceToken: SyntaxFactory.MakeToken(SyntaxKind.CloseBraceToken),
                semicolonToken: null );
            m.XVoDecl = true;
            if (context.Namespace != null) {
                m = AddNameSpaceToMember(context.Namespace, m);
            }
            context.Put(m);
        }

        public override void ExitVostructmember([NotNull] XP.VostructmemberContext context)
        {
            bool isDim = context.Dim != null;
            var varType = context.DataType?.Get<TypeSyntax>() ?? MissingType();
            if (isDim) {
                varType = _syntaxFactory.ArrayType(varType, MakeArrayRankSpecifier(context.ArraySub._ArrayIndex.Count));
            }
            varType.XVoDecl = true;
            if (context.As?.Type == XP.IS)
            {
                varType.XVoIsDecl = true;
            }
            context.Put(_syntaxFactory.FieldDeclaration(
                EmptyList<AttributeListSyntax>(),
                TokenList(SyntaxKind.PublicKeyword),
                _syntaxFactory.VariableDeclaration(varType, 
                    MakeSeparatedList(GenerateVariable(context.Id.Get<SyntaxToken>()))),
                SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken)));
        }

        public override void ExitVounion([NotNull] XP.VounionContext context)
        {
            MemberDeclarationSyntax m = _syntaxFactory.StructDeclaration(
                attributeLists: MakeList(
                    _syntaxFactory.AttributeList(
                        openBracketToken: SyntaxFactory.MakeToken(SyntaxKind.OpenBracketToken),
                        target: null,
                        attributes: MakeSeparatedList(
                            _syntaxFactory.Attribute(
                                name: GenerateQualifiedName("global::System.Runtime.InteropServices.StructLayout"),
                                argumentList: _syntaxFactory.AttributeArgumentList(
                                    openParenToken: SyntaxFactory.MakeToken(SyntaxKind.OpenParenToken),
                                    arguments: MakeSeparatedList(
                                        _syntaxFactory.AttributeArgument(null,null,GenerateQualifiedName("global::System.Runtime.InteropServices.LayoutKind.Explicit"))
                                    ),
                                    closeParenToken: SyntaxFactory.MakeToken(SyntaxKind.CloseParenToken))
                                ),
                            _options.IsDialectVO ?
                                _syntaxFactory.Attribute(
                                    name: GenerateQualifiedName("global::Vulcan.Internal.VOStruct"),
                                    argumentList: _syntaxFactory.AttributeArgumentList(
                                        openParenToken: SyntaxFactory.MakeToken(SyntaxKind.OpenParenToken),
                                        arguments: MakeSeparatedList(
                                            _syntaxFactory.AttributeArgument(null, null, GenerateLiteral(0)),
                                            _syntaxFactory.AttributeArgument(null, null, GenerateLiteral(0))
                                        ),
                                        closeParenToken: SyntaxFactory.MakeToken(SyntaxKind.CloseParenToken))
                                    )
                                : null
                            ),
                        closeBracketToken: SyntaxFactory.MakeToken(SyntaxKind.CloseBracketToken))
                    ),
                modifiers: context.Modifiers?.GetList<SyntaxToken>() ?? TokenListWithDefaultVisibility(),
                keyword: SyntaxFactory.MakeToken(SyntaxKind.StructKeyword),
                identifier: context.Id.Get<SyntaxToken>(),
                typeParameterList: null,
                baseList: null,
                constraintClauses: null,
                openBraceToken: SyntaxFactory.MakeToken(SyntaxKind.OpenBraceToken),
                members: (context._Members?.Count > 0) ? MakeList<MemberDeclarationSyntax>(context._Members) : EmptyList<MemberDeclarationSyntax>(),
                closeBraceToken: SyntaxFactory.MakeToken(SyntaxKind.CloseBraceToken),
                semicolonToken: null);
            m.XVoDecl = true;
            if (context.Namespace != null) {
                m = AddNameSpaceToMember(context.Namespace, m);
            }
            context.Put(m);
        }

        public override void ExitVounionmember([NotNull] XP.VounionmemberContext context)
        {
            bool isDim = context.Dim != null;
            var varType = context.DataType?.Get<TypeSyntax>() ?? MissingType();
            if (isDim) {
                varType = _syntaxFactory.ArrayType(varType, MakeArrayRankSpecifier(context.ArraySub._ArrayIndex.Count));
            }
            varType.XVoDecl = true;
            if (context.As?.Type == XP.IS)
            {
                varType.XVoIsDecl = true;
            }
            context.Put(_syntaxFactory.FieldDeclaration(
                MakeList(
                    _syntaxFactory.AttributeList(
                        openBracketToken: SyntaxFactory.MakeToken(SyntaxKind.OpenBracketToken),
                        target: null,
                        attributes: MakeSeparatedList(
                            _syntaxFactory.Attribute(
                                name: GenerateQualifiedName("global::System.Runtime.InteropServices.FieldOffset"),
                                argumentList: _syntaxFactory.AttributeArgumentList(
                                    openParenToken: SyntaxFactory.MakeToken(SyntaxKind.OpenParenToken),
                                    arguments: MakeSeparatedList(
                                        _syntaxFactory.AttributeArgument(null,null,
                                            GenerateLiteral("0",0)
                                        )
                                    ),
                                    closeParenToken: SyntaxFactory.MakeToken(SyntaxKind.CloseParenToken))
                                )
                            ),
                        closeBracketToken: SyntaxFactory.MakeToken(SyntaxKind.CloseBracketToken))
                    ),
                TokenList(SyntaxKind.PublicKeyword),
                _syntaxFactory.VariableDeclaration(varType, 
                    MakeSeparatedList(GenerateVariable(context.Id.Get<SyntaxToken>()))),
                SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken)));
        }

        public override void ExitArraysub([NotNull] XP.ArraysubContext context)
        {
            context.Put(_syntaxFactory.ArrayRankSpecifier(
                SyntaxFactory.MakeToken(SyntaxKind.OpenBracketToken),
                MakeSeparatedList<ExpressionSyntax>(context._ArrayIndex),
                SyntaxFactory.MakeToken(SyntaxKind.CloseBracketToken)));
        }
        public override void ExitFunction([NotNull] XP.FunctionContext context)
        {
            var isInInterface = context.isInInterface();
            if (isInInterface && context.StmtBlk != null && context.StmtBlk._Stmts.Count > 0) {
                context.AddError(new ParseErrorData(context.Id, ErrorCode.ERR_InterfaceMemberHasBody));
            }
            var attributes = context.Attributes?.GetList<AttributeListSyntax>() ?? EmptyList<AttributeListSyntax>();
            var parameters = context.ParamList?.Get<ParameterListSyntax>() ?? EmptyParameterList();
            var body = isInInterface ? null : context.StmtBlk.Get<BlockSyntax>();
            var returntype = context.Type?.Get<TypeSyntax>() ?? MissingType();
            if (!isInInterface)
            {
                ImplementClipperAndPSZ(context, ref attributes, ref parameters, ref body, ref returntype);
                body = AddMissingReturnStatement(body, context.StmtBlk, returntype);
            }
            context.Put(_syntaxFactory.MethodDeclaration(
                attributeLists: attributes,
                modifiers: context.Modifiers?.GetList<SyntaxToken>() ?? TokenListWithDefaultVisibility(isInInterface, SyntaxKind.StaticKeyword),
                returnType: returntype,
                explicitInterfaceSpecifier: null,
                identifier: context.Id.Get<SyntaxToken>(),
                typeParameterList: context.TypeParameters?.Get<TypeParameterListSyntax>(),
                parameterList: parameters,
                constraintClauses: MakeList<TypeParameterConstraintClauseSyntax>(context._ConstraintsClauses),
                body: body,
                expressionBody: null,
                semicolonToken: (!isInInterface && context.StmtBlk != null) ? null : SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken)));
        }
        public override void ExitProcedure([NotNull] XP.ProcedureContext context)
        {
            var isInInterface = context.isInInterface();
            if (isInInterface && context.StmtBlk != null && context.StmtBlk._Stmts.Count > 0) {
                context.AddError(new ParseErrorData(context.Id, ErrorCode.ERR_InterfaceMemberHasBody));
            }
            if (context.Init != null )
            {
                context.AddError(new ParseErrorData(context.Id, ErrorCode.ERR_FeatureNotAvailableInDialect,"Init procedure", _options.Dialect.ToString()));
            }

            var attributes = context.Attributes?.GetList<AttributeListSyntax>() ?? EmptyList<AttributeListSyntax>();
            var parameters = context.ParamList?.Get<ParameterListSyntax>() ?? EmptyParameterList();
            var body = isInInterface ? null : context.StmtBlk.Get<BlockSyntax>();
            var returntype = VoidType();
            // no return statement needed in PROCEDURE
            // body = AddMissingReturnStatement(body, context.StmtBlk, null);
            ImplementClipperAndPSZ(context, ref attributes, ref parameters, ref body, ref returntype);
            context.Put(_syntaxFactory.MethodDeclaration(
                attributeLists: attributes,
                modifiers: context.Modifiers?.GetList<SyntaxToken>() ?? TokenListWithDefaultVisibility(isInInterface, SyntaxKind.StaticKeyword),
                returnType: returntype,
                explicitInterfaceSpecifier: null,
                identifier: context.Id.Get<SyntaxToken>(),
                typeParameterList: context.TypeParameters?.Get<TypeParameterListSyntax>(),
                parameterList: parameters,
                constraintClauses: MakeList<TypeParameterConstraintClauseSyntax>(context._ConstraintsClauses),
                body: body,
                expressionBody: null,
                semicolonToken: (!isInInterface && context.StmtBlk != null) ? null : SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken)));
        }

        public override void ExitCallingconvention([NotNull] XP.CallingconventionContext context)
        {
            // TODO nvk (calling convention is silently ignored for now)
        }

        public override void ExitParameterList([NotNull] XP.ParameterListContext context)
        {
            var @params = _pool.AllocateSeparated<ParameterSyntax>();
            foreach (var paramCtx in context._Params)
            {
                if (@params.Count>0)
                    @params.AddSeparator(SyntaxFactory.MakeToken(SyntaxKind.CommaToken));
                @params.Add(paramCtx.Get<ParameterSyntax>());
            }
            context.Put(_syntaxFactory.ParameterList(
                SyntaxFactory.MakeToken(SyntaxKind.OpenParenToken),
                @params,
                SyntaxFactory.MakeToken(SyntaxKind.CloseParenToken)));
            _pool.Free(@params);
        }

        public override void EnterParameter([NotNull] XP.ParameterContext context)
        {
            if (context.Self != null)
            {
                context.Modifiers._Tokens.Add(context.Self);
            }
        }

        protected virtual TypeSyntax _getParameterType([NotNull] XP.ParameterContext context) {
            TypeSyntax type = context.Type?.Get<TypeSyntax>();
            if(type == null) {
                type = MissingType();
            }
            return type;
        }

        public override void ExitParameter([NotNull] XP.ParameterContext context)
        {
            TypeSyntax type = _getParameterType(context);
            type.XVoDecl = true;
            if (context.Modifiers?._IS != null)
            {
                type.XVoIsDecl = true;
            }
            context.Put(_syntaxFactory.Parameter(
                attributeLists: context.Attributes?.GetList<AttributeListSyntax>() ?? EmptyList<AttributeListSyntax>(),
                modifiers: context.Modifiers?.GetList<SyntaxToken>() ?? EmptyList(),
                type: type,
                identifier: context.Id.Get<SyntaxToken>(),
                @default: context.Default == null ? null : _syntaxFactory.EqualsValueClause(
                    SyntaxFactory.MakeToken(SyntaxKind.EqualsToken),
                    context.Default.Get<ExpressionSyntax>())));
        }

        public override void ExitParameterDeclMods([NotNull] XP.ParameterDeclModsContext context)
        {
            SyntaxListBuilder modifiers = _pool.Allocate();
            foreach (var m in context._Tokens)
            {
                if (m.Type != XP.AS && m.Type != XP.IS)
                    modifiers.AddCheckUnique(m.SyntaxKeyword());
            }
            modifiers.FixDefaultVisibility();
            context.PutList(modifiers.ToTokenList());
            _pool.Free(modifiers);
        }

        public override void ExitFuncprocModifiers([NotNull] XP.FuncprocModifiersContext context) {
            SyntaxListBuilder modifiers = _pool.Allocate();
            foreach(var m in context._Tokens) {
                modifiers.AddCheckUnique(m.SyntaxKeyword());
            }
            // STATIC FUNCTION is implemented as INTERNAL and will be moved to a special class
            if(modifiers.Any(SyntaxKind.StaticKeyword)) { 
                // in this context static has to do with visibility
                modifiers.Add(SyntaxFactory.MakeToken(SyntaxKind.InternalKeyword));
                context.IsStaticVisible = true;
            } else {
                // in this context static means that .Net static = class method as opposed to instance method
                context.IsStaticVisible = false;
                modifiers.Add(SyntaxFactory.MakeToken(SyntaxKind.StaticKeyword));
                modifiers.FixDefaultVisibility();
            }
            context.PutList(modifiers.ToTokenList());
            _pool.Free(modifiers);
        }

        public override void ExitVotypeModifiers([NotNull] XP.VotypeModifiersContext context)
        {
            SyntaxListBuilder modifiers = _pool.Allocate();
            foreach (var m in context._Tokens)
            {
                modifiers.AddCheckUnique(m.SyntaxKeyword());
            }
            modifiers.FixDefaultVisibility();
            context.PutList(modifiers.ToTokenList());
            _pool.Free(modifiers);
        }


        public override void ExitStatementBlock([NotNull] XP.StatementBlockContext context)
        {
            var statements = _pool.Allocate<StatementSyntax>();
            foreach (var stmtCtx in context._Stmts)
            {
                if (stmtCtx.CsNode is SyntaxList<StatementSyntax>)
                    statements.AddRange(stmtCtx.GetList<StatementSyntax>());
                else
                    statements.Add(stmtCtx.Get<StatementSyntax>());
            }
            context.Put(MakeBlock(statements));
            _pool.Free(statements);
        }

        public override void ExitDeclarationStmt([NotNull] XP.DeclarationStmtContext context)
        {
            context.PutList(context.Decl.GetList<StatementSyntax>());
        }

        public override void EnterCommonLocalDecl([NotNull] XP.CommonLocalDeclContext context)
        {
            XP.DatatypeContext t = null;
            for(var i = context._LocalVars.Count-1; i >= 0; i--) {
                var locCtx = context._LocalVars[i];
                if (locCtx.DataType != null)
                    t = locCtx.DataType;
                else if (t != null)
                    locCtx.DataType = t;
            }
        }

        public override void ExitCommonLocalDecl([NotNull] XP.CommonLocalDeclContext context)
        {
            foreach(var lvCtx in context._LocalVars)
                VisitLocalvar(lvCtx);
            context.PutList(MakeList<StatementSyntax>(context._LocalVars));
        }

        public override void ExitVarLocalDecl([NotNull] XP.VarLocalDeclContext context)
        {
            context.PutList(MakeList<StatementSyntax>(context._ImpliedVars));
        }

        public override void EnterLocalvar([NotNull] XP.LocalvarContext context)
        {
            bool isDim = context.Dim != null;
            bool hasArraySub = context.ArraySub != null;
            if (isDim && !hasArraySub) {
                context.AddError(new ParseErrorData(context.DIM(), ErrorCode.ERR_ArrayInitializerExpected));
            }
            if (!isDim && hasArraySub) {
                context.ArraySub.AddError(new ParseErrorData(ErrorCode.ERR_FeatureNotAvailableInDialect, "Indexed Local",_options.Dialect.ToString()));
            }
        }

        public override void ExitLocalvar([NotNull] XP.LocalvarContext context)
        {
            // nvk: Do nothing here. It will be handled by the visitor after Datatype(s) are processed.
        }

        protected virtual TypeSyntax _getMissingLocalType() {
            return MissingType();
        }

        private void VisitLocalvar([NotNull] XP.LocalvarContext context)
        {
            bool isConst = context.Const != null;
            bool isStatic = (context.Parent as XP.CommonLocalDeclContext).Static != null;
            bool isDim = context.Dim != null && context.ArraySub != null;
            string staticName = null;
            TypeSyntax varType;
            if (context.DataType != null)
                varType = context.DataType?.Get<TypeSyntax>();
            else
            {
                varType = _getMissingLocalType();
            }
            var initExpr = context.Expression?.Get<ExpressionSyntax>();
            if (isDim) {
                if (initExpr == null) {
                    initExpr = _syntaxFactory.ArrayCreationExpression(SyntaxFactory.MakeToken(SyntaxKind.NewKeyword),
                        _syntaxFactory.ArrayType(varType,context.ArraySub.Get<ArrayRankSpecifierSyntax>()),
                        null);
                }
                varType = _syntaxFactory.ArrayType(varType, MakeArrayRankSpecifier(context.ArraySub._ArrayIndex.Count));
            }
            varType.XVoDecl = true;
            if (context.As?.Type == XP.IS)
            {
                varType.XVoIsDecl = true;
            }
            if (isStatic) {
                staticName = StaticLocalFieldNamePrefix+context.Id.Get<SyntaxToken>().Text+UniqueNameSuffix;
                ClassEntities.Peek().Members.Add(
                    _syntaxFactory.FieldDeclaration(
                        EmptyList<AttributeListSyntax>(),
                        TokenList(SyntaxKind.StaticKeyword,SyntaxKind.PrivateKeyword),
                        _syntaxFactory.VariableDeclaration(varType, 
                            MakeSeparatedList(GenerateVariable(SyntaxFactory.Identifier(staticName)))),
                        SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken))
                    );
                if (initExpr != null) {
                    ClassEntities.Peek().Members.Add(
                        _syntaxFactory.FieldDeclaration(
                            EmptyList<AttributeListSyntax>(),
                            TokenList(SyntaxKind.StaticKeyword,SyntaxKind.PrivateKeyword),
                            _syntaxFactory.VariableDeclaration(_syntaxFactory.PredefinedType(SyntaxFactory.MakeToken(SyntaxKind.BoolKeyword)), 
                                MakeSeparatedList(
										GenerateVariable(SyntaxFactory.Identifier(staticName+StaticLocalInitFieldNameSuffix), 
                                        GenerateLiteral(true)))),
                            SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken))
                        );
                    ClassEntities.Peek().Members.Add(
                        _syntaxFactory.FieldDeclaration(
                            EmptyList<AttributeListSyntax>(),
                            TokenList(SyntaxKind.StaticKeyword,SyntaxKind.PrivateKeyword),
                            _syntaxFactory.VariableDeclaration(_objectType, 
                                MakeSeparatedList(GenerateVariable(SyntaxFactory.Identifier(staticName+StaticLocalLockFieldNameSuffix), 
                                        CreateObject(_objectType,EmptyArgumentList(), null)))),
                            SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken))
                        );
                }
            }
            var variables = _pool.AllocateSeparated<VariableDeclaratorSyntax>();
            variables.Add(_syntaxFactory.VariableDeclarator(context.Id.Get<SyntaxToken>(), null,
                isStatic ? _syntaxFactory.EqualsValueClause(SyntaxFactory.MakeToken(SyntaxKind.EqualsToken),
                    GenerateSimpleName(staticName))
                : (initExpr == null) ? null : _syntaxFactory.EqualsValueClause(SyntaxFactory.MakeToken(SyntaxKind.EqualsToken), initExpr)));
            var modifiers = _pool.Allocate();
            if (isConst)
                modifiers.Add(SyntaxFactory.MakeToken(SyntaxKind.ConstKeyword));
            if (isStatic)
                modifiers.Add(SyntaxFactory.MakeToken(SyntaxKind.RefKeyword));
            if (!isStatic) {
                context.Put(_syntaxFactory.LocalDeclarationStatement(
                    modifiers.ToTokenList(),
                    _syntaxFactory.VariableDeclaration(varType, variables),
                    SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken)));
            }
            else {
                var decl = _pool.Allocate<StatementSyntax>();
                decl.Add(_syntaxFactory.LocalDeclarationStatement(
                    modifiers.ToTokenList(),
                    _syntaxFactory.VariableDeclaration(varType, variables),
                    SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken)));
                if (initExpr != null) {
                    decl.Add(GenerateIfStatement(
                        GenerateSimpleName(staticName+StaticLocalInitFieldNameSuffix),
                        
                        _syntaxFactory.LockStatement(SyntaxFactory.MakeToken(SyntaxKind.LockKeyword),
                            SyntaxFactory.MakeToken(SyntaxKind.OpenParenToken),
                            GenerateSimpleName(staticName+StaticLocalLockFieldNameSuffix),
                            SyntaxFactory.MakeToken(SyntaxKind.CloseParenToken),
                            GenerateIfStatement(GenerateSimpleName(staticName+StaticLocalInitFieldNameSuffix),
                                
                                MakeBlock(MakeList<StatementSyntax>(
                                        GenerateExpressionStatement(MakeSimpleAssignment(GenerateSimpleName(staticName),initExpr)),
                                        GenerateExpressionStatement(
                                            MakeSimpleAssignment(
                                                GenerateSimpleName(staticName+StaticLocalInitFieldNameSuffix),
                                                GenerateLiteral(false)))
                                        ))))));
                }
                context.PutList<StatementSyntax>(decl);
                _pool.Free(decl);
            }
            _pool.Free(variables);
            _pool.Free(modifiers);
        }

        public override void ExitImpliedvar([NotNull] XP.ImpliedvarContext context)
        {
            bool isConst = context.Const != null;
            bool isStatic = (context.Parent as XP.VarLocalDeclContext).Static != null;
            var variables = _pool.AllocateSeparated<VariableDeclaratorSyntax>();
            variables.Add(_syntaxFactory.VariableDeclarator(context.Id.Get<SyntaxToken>(),null,
                (context.Expression == null) ? null :
                _syntaxFactory.EqualsValueClause(SyntaxFactory.MakeToken(SyntaxKind.EqualsToken), context.Expression.Get<ExpressionSyntax>())));
            var modifiers = _pool.Allocate();
            if (isConst)
                context.AddError(new ParseErrorData(ErrorCode.ERR_ImplicitlyTypedVariableCannotBeConst));
            if (isStatic)
                context.AddError(new ParseErrorData(ErrorCode.ERR_BadVarDecl));
            context.Put(_syntaxFactory.LocalDeclarationStatement(
                modifiers.ToTokenList(),
                _syntaxFactory.VariableDeclaration(_impliedType, variables),
                SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken)));
            _pool.Free(variables);
            _pool.Free(modifiers);
        }

        public override void ExitXbasedeclStmt([NotNull] XP.XbasedeclStmtContext context)
        {
            context.Put(context.xbasedecl().Get<StatementSyntax>());
        }

        public override void ExitXbasedecl([NotNull] XP.XbasedeclContext context)
        {
            context.Put(NotInDialect(context.T.Text+" statement" ));
        }

        public override void EnterXbasedecl([NotNull] XP.XbasedeclContext context)
        {
            // declare memvars
            if (context.T.Type == XP.MEMVAR)
            {
                foreach (var memvar in context._Vars)
                {
                    CurrentEntity.Data.AddField(memvar.Id.GetText(), "M", false);
                }

            }
        }


        public override void ExitWhileStmt([NotNull] XP.WhileStmtContext context)
        {
            context.Put(_syntaxFactory.WhileStatement(SyntaxFactory.MakeToken(SyntaxKind.WhileKeyword),
                SyntaxFactory.MakeToken(SyntaxKind.OpenParenToken),
                context.Expr.Get<ExpressionSyntax>(),
                SyntaxFactory.MakeToken(SyntaxKind.CloseParenToken),
                context.StmtBlk.Get<BlockSyntax>()));
        }

        public override void ExitRepeatStmt([NotNull] XP.RepeatStmtContext context)
        {
            context.Put(_syntaxFactory.DoStatement(SyntaxFactory.MakeToken(SyntaxKind.DoKeyword),
                context.StmtBlk.Get<BlockSyntax>(),
                SyntaxFactory.MakeToken(SyntaxKind.WhileKeyword),
                SyntaxFactory.MakeToken(SyntaxKind.OpenParenToken),
                _syntaxFactory.PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, SyntaxFactory.MakeToken(SyntaxKind.ExclamationToken),
                    context.Expr.Get<ExpressionSyntax>()),
                SyntaxFactory.MakeToken(SyntaxKind.CloseParenToken),
                SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken)));
        }

        public override void ExitForStmt([NotNull] XP.ForStmtContext context)
        {
            object blockStmts = null;
            ExpressionSyntax assignExpr, whileExpr, incrExpr, iterExpr, initExpr;
            if (context.AssignExpr != null)
            {
                if (!(context.AssignExpr is XP.AssignmentExpressionContext))
                {
                    context.Put(_syntaxFactory.EmptyStatement(SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken)));
                    context.AddError(new ParseErrorData(context.Dir, ErrorCode.ERR_SyntaxError, ":="));
                    return;
                }
                if ((context.AssignExpr as XP.AssignmentExpressionContext).Op.Type != XP.ASSIGN_OP)
                {
                    context.Put(_syntaxFactory.EmptyStatement(SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken)));
                    context.AddError(new ParseErrorData((context.AssignExpr as XP.AssignmentExpressionContext).Op, ErrorCode.ERR_SyntaxError, ":="));
                    return;
                }
                iterExpr = (context.AssignExpr as XP.AssignmentExpressionContext).Left.Get<ExpressionSyntax>();
                initExpr = (context.AssignExpr as XP.AssignmentExpressionContext).Right.Get<ExpressionSyntax>();
                assignExpr = context.AssignExpr.Get<ExpressionSyntax>();
            }
            else
            {
                iterExpr = _syntaxFactory.IdentifierName(context.ForIter.Get<SyntaxToken>());
                initExpr = context.Expr.Get<ExpressionSyntax>();
                assignExpr = MakeSimpleAssignment(iterExpr, initExpr);
            }
            if (context.Step == null) {
                context.Step = FixPosition(new XP.PrimaryExpressionContext(FixPosition(new XP.ExpressionContext(),context.Stop)),context.Stop);
                context.Step.Put(GenerateLiteral("1", 1));
            }
            switch (context.Dir.Type) {
                case XP.DOWNTO:
                    whileExpr = _syntaxFactory.BinaryExpression(SyntaxKind.GreaterThanOrEqualExpression,
                        iterExpr,
                        SyntaxFactory.MakeToken(SyntaxKind.GreaterThanEqualsToken),
                        context.FinalExpr.Get<ExpressionSyntax>());
                    incrExpr = _syntaxFactory.AssignmentExpression(SyntaxKind.SubtractAssignmentExpression,
                        iterExpr,
                        SyntaxFactory.MakeToken(SyntaxKind.MinusEqualsToken),
                        context.Step.Get<ExpressionSyntax>());
                    break;
                case XP.UPTO:
                case XP.TO:
                default:
                    whileExpr = _syntaxFactory.BinaryExpression(SyntaxKind.LessThanOrEqualExpression,
                            iterExpr,
                            SyntaxFactory.MakeToken(SyntaxKind.LessThanEqualsToken),
                            context.FinalExpr.Get<ExpressionSyntax>());
                    incrExpr = _syntaxFactory.AssignmentExpression(SyntaxKind.AddAssignmentExpression,
                                iterExpr,
                                SyntaxFactory.MakeToken(SyntaxKind.PlusEqualsToken),
                                context.Step.Get<ExpressionSyntax>());
                                break;
            }
            var decl = default(VariableDeclarationSyntax);
            var init = _pool.AllocateSeparated<ExpressionSyntax>();
            if (context.ForDecl != null)
            {
                decl = _syntaxFactory.VariableDeclaration(
                    context.Type?.Get<TypeSyntax>() ?? _impliedType,
                    MakeSeparatedList(GenerateVariable(
                        context.ForIter.Get<SyntaxToken>(),initExpr)));
            }
            else
            {
                init.Add(assignExpr);
            }
            var incr = _pool.AllocateSeparated<ExpressionSyntax>();
            incr.Add(incrExpr);
            var forStmt = _syntaxFactory.ForStatement(SyntaxFactory.MakeToken(SyntaxKind.ForKeyword),
                SyntaxFactory.MakeToken(SyntaxKind.OpenParenToken),
                decl,
                init,
                SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken),
                whileExpr,
                SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken),
                incr,
                SyntaxFactory.MakeToken(SyntaxKind.CloseParenToken),
                context.StmtBlk.Get<BlockSyntax>());
            _pool.Free(init);
            _pool.Free(incr);
            if (blockStmts == null) { 
                context.Put(forStmt);
            }
            else {
                var stmts = (SyntaxListBuilder<StatementSyntax>)blockStmts;
                stmts.Add(forStmt);
                context.Put(MakeBlock(stmts));
                _pool.Free(stmts);
            }
        }

        public override void ExitForeachStmt([NotNull] XP.ForeachStmtContext context)
        {
            context.Put(_syntaxFactory.ForEachStatement(SyntaxFactory.MakeToken(SyntaxKind.ForEachKeyword),
                SyntaxFactory.MakeToken(SyntaxKind.OpenParenToken),
                context.Type?.Get<TypeSyntax>() ?? _impliedType,
                context.Id.Get<SyntaxToken>(),
                SyntaxFactory.MakeToken(SyntaxKind.InKeyword),
                context.Container.Get<ExpressionSyntax>(),
                SyntaxFactory.MakeToken(SyntaxKind.CloseParenToken),
                context.StmtBlk.Get<BlockSyntax>()));
        }

        public override void ExitIfStmt([NotNull] XP.IfStmtContext context)
        {
            context.Put(context.IfStmt.Get<IfStatementSyntax>());
        }

        public override void ExitIfElseBlock([NotNull] XP.IfElseBlockContext context)
        {
            context.Put(GenerateIfStatement(
                context.Cond.Get<ExpressionSyntax>(),
                context.StmtBlk.Get<BlockSyntax>(),
                (context.ElseIfBlock != null) ? 
                    _syntaxFactory.ElseClause(SyntaxFactory.MakeToken(SyntaxKind.ElseKeyword), context.ElseIfBlock.Get<IfStatementSyntax>())
                : (context.ElseBlock != null) ?
                    _syntaxFactory.ElseClause(SyntaxFactory.MakeToken(SyntaxKind.ElseKeyword), context.ElseBlock.Get<BlockSyntax>())
                : null));
        }

        public override void ExitCaseStmt([NotNull] XP.CaseStmtContext context)
        {
            context.Put((StatementSyntax)context.CaseStmt?.Get<IfStatementSyntax>() ?? 
                _syntaxFactory.EmptyStatement(SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken)));
        }

        public override void ExitCaseBlock([NotNull] XP.CaseBlockContext context)
        {
            if (context.Key.Type == XP.OTHERWISE)
                context.Put(context.StmtBlk.Get<StatementSyntax>());
            else {
                context.Put(GenerateIfStatement(
                    context.Cond.Get<ExpressionSyntax>(),
                    context.StmtBlk.Get<BlockSyntax>(),
                    (context.NextCase == null) ? null :
                        _syntaxFactory.ElseClause(SyntaxFactory.MakeToken(SyntaxKind.ElseKeyword),
                            context.NextCase.Get<StatementSyntax>())));
            }
        }

        public override void ExitExitStmt([NotNull] XP.ExitStmtContext context)
        {
            context.Put(_syntaxFactory.BreakStatement(SyntaxFactory.MakeToken(SyntaxKind.BreakKeyword),
                SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken)));
        }

        public override void ExitLoopStmt([NotNull] XP.LoopStmtContext context)
        {
            context.Put(_syntaxFactory.ContinueStatement(SyntaxFactory.MakeToken(SyntaxKind.ContinueKeyword),
                SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken)));
        }

        public override void ExitExpressionStmt([NotNull] XP.ExpressionStmtContext context)
        {
            var statements = _pool.Allocate<StatementSyntax>();
            foreach (var exprCtx in context._Exprs)
            {
                // check because there may already be statements in here, such as the IF statement generated for AltD()
                var node = exprCtx.CsNode;
                if (node is StatementSyntax)
                    statements.Add( (StatementSyntax) node);
                else
                    statements.Add(GenerateExpressionStatement(exprCtx.Get<ExpressionSyntax>()));
            }
            context.Put(MakeBlock(statements));
            _pool.Free(statements);
        }

        public override void ExitBreakStmt([NotNull] XP.BreakStmtContext context)
        {
            context.Put(NotInDialect("BREAK statement"));
        }

        public override void ExitThrowStmt([NotNull] XP.ThrowStmtContext context)
        {
            context.Put(_syntaxFactory.ThrowStatement(SyntaxFactory.MakeToken(SyntaxKind.ThrowKeyword),
                context.Expr.Get<ExpressionSyntax>(),
                SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken)));
        }

        public override void ExitTryStmt([NotNull] XP.TryStmtContext context)
        {
            if (!(context._CatchBlock.Count > 0) && context.FinBlock == null) {
                var cb = FixPosition(new XP.CatchBlockContext(context,0), context.Stop);
                cb.StmtBlk = FixPosition(new XP.StatementBlockContext(cb,0), context.Stop);
                this.ExitStatementBlock(cb.StmtBlk);
                this.ExitCatchBlock(cb);
                context._CatchBlock.Add(cb);
            }
            var catches = _pool.Allocate<CatchClauseSyntax>();
            foreach (var catchCtx in context._CatchBlock)
            {
                catches.Add(catchCtx.Get<CatchClauseSyntax>());
            }
            context.Put(_syntaxFactory.TryStatement(SyntaxFactory.MakeToken(SyntaxKind.TryKeyword),
                context.StmtBlk.Get<BlockSyntax>(),
                catches,
                context.FinBlock == null ? null : _syntaxFactory.FinallyClause(SyntaxFactory.MakeToken(SyntaxKind.FinallyKeyword),
                    context.FinBlock.Get<BlockSyntax>())));
            _pool.Free(catches);
        }

        public override void ExitCatchBlock([NotNull] XP.CatchBlockContext context)
        {
            context.Put(_syntaxFactory.CatchClause(SyntaxFactory.MakeToken(SyntaxKind.CatchKeyword),
                context.Id == null ? null : _syntaxFactory.CatchDeclaration(
                    SyntaxFactory.MakeToken(SyntaxKind.OpenParenToken),
                    context.Type?.Get<TypeSyntax>() ?? MissingType(),
                    context.Id.Get<SyntaxToken>(),
                    SyntaxFactory.MakeToken(SyntaxKind.CloseParenToken)),
                null, // TODO: (grammar) catch filters?
                context.StmtBlk.Get<BlockSyntax>()));
        }

        public override void ExitSeqStmt([NotNull] XP.SeqStmtContext context)
        {
            context.Put(NotInDialect("BEGIN SEQUENCE statement"));
            return;
        }

        public override void ExitRecoverBlock([NotNull] XP.RecoverBlockContext context)
        {
            context.Put(NotInDialect("RECOVER USING block"));
            return;
        }



        public override void ExitReturnStmt([NotNull] XP.ReturnStmtContext context)
        {
            var expr = context.Expr?.Get<ExpressionSyntax>();
            // / vo9 is handled in the Subclass
            context.Put(_syntaxFactory.ReturnStatement(SyntaxFactory.MakeToken(SyntaxKind.ReturnKeyword), 
                expr,
                SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken)));
        }

        public override void ExitYieldStmt([NotNull] XP.YieldStmtContext context)
        {
            SyntaxKind kind;
            ExpressionSyntax arg;
            SyntaxToken token;
            if (context.Break != null)  // yield exit or yield break
            {
                kind = SyntaxKind.YieldBreakStatement;
                arg = null;
                token = SyntaxFactory.MakeToken(SyntaxKind.BreakKeyword);
            }
            else                   // yield return
            {
                kind = SyntaxKind.YieldReturnStatement;
                arg = context.Expr?.Get<ExpressionSyntax>();
                token = SyntaxFactory.MakeToken(SyntaxKind.ReturnKeyword);
            }
            context.Put(_syntaxFactory.YieldStatement( kind,SyntaxFactory.MakeToken(SyntaxKind.YieldKeyword), 
                token, arg, 
                SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken)));
        }

        public override void ExitSwitchStmt([NotNull] XP.SwitchStmtContext context)
        {
            var sections = _pool.Allocate<SwitchSectionSyntax>();
            var emptyLabels = _pool.Allocate<SwitchLabelSyntax>();
            foreach (var switchBlkCtx in context._SwitchBlock) {
                // check for block with empty statement list
                // And concatenate it with the next switch block
                // but not the last block
                var sectionSyntax = switchBlkCtx.Get<SwitchSectionSyntax>();
                if (sectionSyntax.Statements.Count == 0 && switchBlkCtx != context._SwitchBlock.Last())
                {
                    emptyLabels.Add(sectionSyntax.Labels[0]);
                }
                else
                {
                    if (emptyLabels.Count != 0)
                    {
                        // create new labels for sectionSyntax that include the preceding labels
                        emptyLabels.Add(sectionSyntax.Labels[0]);
                        sectionSyntax = _syntaxFactory.SwitchSection(emptyLabels, sectionSyntax.Statements);
                    }
                    if (ContainsExitStatement(switchBlkCtx.StmtBlk._Stmts))
                    {
                        sectionSyntax = sectionSyntax.WithAdditionalDiagnostics(new SyntaxDiagnosticInfo(ErrorCode.ERR_ExitInsideSwitchStatementNotAllowed));
                    }
                    sections.Add(sectionSyntax);
                    emptyLabels.Clear();
                }
            }
            context.Put(_syntaxFactory.SwitchStatement(SyntaxFactory.MakeToken(SyntaxKind.SwitchKeyword),
                SyntaxFactory.MakeToken(SyntaxKind.OpenParenToken),
                context.Expr.Get<ExpressionSyntax>(),
                SyntaxFactory.MakeToken(SyntaxKind.CloseParenToken),
                SyntaxFactory.MakeToken(SyntaxKind.OpenBraceToken),
                sections,
                SyntaxFactory.MakeToken(SyntaxKind.CloseBraceToken)));
            _pool.Free(sections);
            _pool.Free(emptyLabels);
        }

        private bool ContainsExitStatement(IList<XP.StatementContext> stmts)
        {
            // Checks for EXIT statements that are not inside a LoopStmt
            // Because these are not allowed inside a Switch 
            foreach (var stmt in stmts)
            {
                if (stmt is XP.ExitStmtContext)
                    return true;
                if (stmt is XP.ILoopStmtContext)    // // For, Foreach, While, Repeat may have private exits
                    continue;
                if (stmt is XP.BlockStmtContext)    // Non looping block
                {
                    var blockstmt = stmt as XP.BlockStmtContext;
                    if (ContainsExitStatement(blockstmt.StmtBlk._Stmts))
                        return true;
                }
                if (stmt is XP.IfStmtContext)
                {
                    var ifstmt = stmt as XP.IfStmtContext;
                    var ifelsestmt = ifstmt.IfStmt;
                    if (ContainsExitStatement(ifelsestmt.StmtBlk._Stmts))           // First IF Block
                        return true;
                    while (ifelsestmt.ElseIfBlock != null)                          // Subsequent elseif blocks 
                    {
                        ifelsestmt = ifelsestmt.ElseIfBlock;
                        if (ContainsExitStatement(ifelsestmt.StmtBlk._Stmts))
                            return true;
                    }
                    if (ifelsestmt.ElseIfBlock != null &&                           // Else block
                         ContainsExitStatement(ifelsestmt.ElseBlock._Stmts))
                        return true;
                }
                if (stmt is XP.CaseStmtContext)
                {
                    var docasestmt = stmt as XP.CaseStmtContext;
                    var casestmt = docasestmt.CaseStmt;     // CaseBlock
                    while (casestmt != null)                // Handles the all case and otherwise blocks
                    {
                        if (ContainsExitStatement(casestmt.StmtBlk._Stmts))
                            return true;
                        casestmt = casestmt.NextCase;
                    }
                }
            }
            return false;
        }

        private bool NeedsBreak(IList<XP.StatementContext> stmts, bool inSideLoop = false)
        {
            // This code checks only the last statement. When there is a return or throw
            // on another line then the system will report 'Unreachable code' anyway.
            if (stmts.Count == 0)
                return true;
            var stmt = stmts.Last();
            if (stmt is XP.ReturnStmtContext || stmt is XP.ThrowStmtContext)
            {
                return false;
            }
            if ((stmt is XP.LoopStmtContext || stmt is XP.ExitStmtContext ) && !inSideLoop)
            {
                // LOOP or EXIT inside a nested Loop block
                return false;
            }
            if (stmt is XP.IfStmtContext)
            {
                var ifstmt = stmt as XP.IfStmtContext;
                var ifelsestmt = ifstmt.IfStmt;
                var elsestmt = ifelsestmt?.ElseBlock;           // The first ifelsestmt should always have a value, but better safe than sorry
                // process to the end of the list
                // when there is no else, then we need a break
                // otherwise process every statement list
                while (ifelsestmt != null )                     // 
                {
                    if (NeedsBreak(ifelsestmt.StmtBlk._Stmts))
                    {
                        return true;
                    }
                    elsestmt = ifelsestmt.ElseBlock;
                    ifelsestmt = ifelsestmt.ElseIfBlock;
                }
                // No Else, so there is at least one block that does not end with a RETURN etc.
                if (elsestmt == null)
                {
                    return true;
                }
                else
                {
                    return NeedsBreak(ifelsestmt.ElseBlock._Stmts);
                }
            }
            if (stmt is XP.CaseStmtContext)
            {
                var docasestmt = stmt as XP.CaseStmtContext;
                var casestmt = docasestmt.CaseStmt;     // CaseBlock, there may be no blocks at all.
                int lastkey = XP.CASE;
                while (casestmt != null)                // otherwise is also a CaseBlock stored in NextCase
                {
                    if (NeedsBreak(casestmt.StmtBlk._Stmts))
                        return true;
                    lastkey = casestmt.Key.Type;
                    casestmt = casestmt.NextCase;
                }
                if (lastkey == XP.CASE) // There is no otherwise
                    return true;
                return false;           // all branches end with a breaking statement
            }
            if (stmt is XP.BlockStmtContext)
            {
                var blockstmt = stmt as XP.BlockStmtContext;
                return NeedsBreak(blockstmt.StmtBlk._Stmts);
            }
            if (stmt is XP.ILoopStmtContext)        // For, Foreach, While, Repeat
            {
                var blockstmt = stmt as XP.ILoopStmtContext;
                return NeedsBreak(blockstmt.Statements._Stmts, true);
            }
            if (stmt is XP.SwitchStmtContext)
            {
                var swstmt = stmt as XP.SwitchStmtContext;
                bool hasdefault = false;
                foreach (var swBlock in swstmt._SwitchBlock)
                {
                    if (swBlock.StmtBlk._Stmts.Count > 0 && NeedsBreak(swBlock.StmtBlk._Stmts))
                        return true;
                    if (swBlock.Key.Type != XP.CASE)
                        hasdefault = true;
                }
                if (!hasdefault)
                    return true;
                return false;           // all branches end with a breaking statement
            }

            return true;
        }

        public override void ExitSwitchBlock([NotNull] XP.SwitchBlockContext context)
        {
            var labels = _pool.Allocate<SwitchLabelSyntax>();
            var kw = context.Key.SyntaxKeyword();
            if (kw.Kind == SyntaxKind.CaseKeyword)
            {
                labels.Add(_syntaxFactory.CaseSwitchLabel(kw, context.Const?.Get<ExpressionSyntax>(),
                    SyntaxFactory.MakeToken(SyntaxKind.ColonToken)));
            }
            else
            {
                labels.Add(_syntaxFactory.DefaultSwitchLabel(kw, SyntaxFactory.MakeToken(SyntaxKind.ColonToken)));
            }
            var stmts = _pool.Allocate<StatementSyntax>();
            if (context.StmtBlk._Stmts.Count > 0) {
                stmts.Add(context.StmtBlk.Get<BlockSyntax>());
                if (NeedsBreak(context.StmtBlk._Stmts))
                {
                    stmts.Add(_syntaxFactory.BreakStatement(SyntaxFactory.MakeToken(SyntaxKind.BreakKeyword),
                        SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken))); 
                }
            }
            context.Put(_syntaxFactory.SwitchSection(labels, stmts));
            _pool.Free(labels);
            _pool.Free(stmts);
        }


        public override void ExitVariableDeclaration([NotNull] XP.VariableDeclarationContext context)
        {
            context.Put(_syntaxFactory.VariableDeclaration(
                context.Type?.Get<TypeSyntax>() ?? (context.Var != null ? _impliedType : MissingType()),
                MakeSeparatedList<VariableDeclaratorSyntax>(context._Decl)
                ));
        }

        public override void ExitVariableDeclarator([NotNull] XP.VariableDeclaratorContext context)
        {
            context.Put(_syntaxFactory.VariableDeclarator(
                context.Id.Get<SyntaxToken>(),
                null,
                _syntaxFactory.EqualsValueClause(SyntaxFactory.MakeToken(SyntaxKind.EqualsToken), context.Expr.Get<ExpressionSyntax>())));
        }

        public override void ExitQoutStmt([NotNull] XP.QoutStmtContext context)
        {
            ArgumentSyntax arg;
            ExpressionSyntax expr;
            // If dialect VO and VulcanRTFuncs included
            // Simply generate call to VulcanRTFuncs.Functions.QOut or QQOut
            // This is done in VOTreeTransForm
            if (!(context._Exprs?.Count > 0)) {
                if (context.Q.Type == XP.QMARK)
                {
                    expr = GenerateMethodCall("global::System.Console.WriteLine", EmptyArgumentList());
                    context.Put(GenerateExpressionStatement(expr));
                }
                else
                {
                    context.Put(_syntaxFactory.EmptyStatement(SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken)));
                }
            }
            else
            {
                // build list of arguments and a matching string.Format mask
                // when single question mark, then start with newline
                string mask = context.Q.Type == XP.QMARK ? "\n" : String.Empty;
                var args = new List<ArgumentSyntax>();
                foreach (var eCtx in context._Exprs)
                {
                    if (args.Count > 0)
                    {
                        mask += " ";
                    }
                    mask += "{" + args.Count.ToString() + "}";
                    expr = eCtx.Get<ExpressionSyntax>();
                    arg = MakeArgument(expr);
                    args.Add(arg);
                }
                args.Insert(0, MakeArgument(GenerateLiteral(mask)));
                // convert args to Array because the overload that receives a collection expects another kind of object
                var arglist = _syntaxFactory.ArgumentList(
                                SyntaxFactory.MakeToken(SyntaxKind.OpenParenToken),
                                MakeSeparatedList<ArgumentSyntax>(args.ToArray()),
                                SyntaxFactory.MakeToken(SyntaxKind.CloseParenToken));

                expr = GenerateMethodCall("global::System.Console.Write", arglist);
                context.Put(GenerateExpressionStatement(expr));
            }
        }


        public override void ExitBlockStmt([NotNull] XP.BlockStmtContext context)
        {

            switch (context.Key.Type)
            {
                case XP.SCOPE:
                    context.Put(context.StmtBlk.Get<BlockSyntax>());
                    break;
                case XP.LOCK:
                    context.Put(_syntaxFactory.LockStatement(SyntaxFactory.MakeToken(SyntaxKind.LockKeyword),
                        SyntaxFactory.MakeToken(SyntaxKind.OpenParenToken),
                        context.Expr.Get<ExpressionSyntax>(),
                        SyntaxFactory.MakeToken(SyntaxKind.CloseParenToken),
                        context.StmtBlk.Get<BlockSyntax>()));
                    break;
                case XP.UNSAFE:
                    context.Put(_syntaxFactory.UnsafeStatement(SyntaxFactory.MakeToken(SyntaxKind.UnsafeKeyword),
                        context.StmtBlk.Get<BlockSyntax>()));
                    break;
                case XP.CHECKED:
                    context.Put(_syntaxFactory.CheckedStatement(SyntaxKind.CheckedStatement,
                        SyntaxFactory.MakeToken(SyntaxKind.CheckedKeyword),
                        context.StmtBlk.Get<BlockSyntax>()));
                    break;
                case XP.UNCHECKED:
                    context.Put(_syntaxFactory.CheckedStatement(SyntaxKind.UncheckedStatement,
                        SyntaxFactory.MakeToken(SyntaxKind.UncheckedKeyword),
                        context.StmtBlk.Get<BlockSyntax>()));
                    break;
                case XP.USING:
                    context.Put(_syntaxFactory.UsingStatement(SyntaxFactory.MakeToken(SyntaxKind.UsingKeyword),
                           SyntaxFactory.MakeToken(SyntaxKind.OpenParenToken),
                           context.VarDecl?.Get<VariableDeclarationSyntax>(),
                           context.Expr?.Get<ExpressionSyntax>(),
                           SyntaxFactory.MakeToken(SyntaxKind.CloseParenToken),
                           context.StmtBlk.Get<BlockSyntax>()));
                    break;
                default:
                    // what else;
                    break;
            }

        }

        public override void ExitNopStmt([NotNull] XP.NopStmtContext context)
        {
            context.Put(_syntaxFactory.EmptyStatement(SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken)));
        }

        public override void ExitCondAccessExpr([NotNull] XP.CondAccessExprContext context)
        {
#if false // nvk: check not needed because it is a separate rule now!
            switch (context.Right.Start.Type) {
                case XP.DOT:
                case XP.COLON:
                case XP.LBRKT:
                    break;
                default:
                    context.AddError(new ParseErrorData(context.Right.Start,ErrorCode.ERR_SyntaxError,"."));
                    break;
            }
#endif
            context.Put(_syntaxFactory.ConditionalAccessExpression(
                context.Left.Get<ExpressionSyntax>(),
                SyntaxFactory.MakeToken(SyntaxKind.QuestionToken),
                context.Right.Get<ExpressionSyntax>()
            ));
        }

        public override void ExitBoundAccessMember([NotNull] XP.BoundAccessMemberContext context)
        {
            context.Put(_syntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                context.Expr.Get<ExpressionSyntax>(),
                SyntaxFactory.MakeToken(SyntaxKind.DotToken),
                context.Name.Get<SimpleNameSyntax>()));
        }

        public override void ExitBoundArrayAccess([NotNull] XP.BoundArrayAccessContext context)
        {
            context.Put(_syntaxFactory.ElementAccessExpression(
                context.Expr.Get<ExpressionSyntax>(),
                context.ArgList?.Get<BracketedArgumentListSyntax>()
                    ?? _syntaxFactory.BracketedArgumentList(
                        SyntaxFactory.MakeToken(SyntaxKind.OpenBracketToken),
                        default(SeparatedSyntaxList<ArgumentSyntax>),
                        SyntaxFactory.MakeToken(SyntaxKind.CloseBracketToken))));
        }

        public override void ExitBoundMethodCall([NotNull] XP.BoundMethodCallContext context)
        {
            context.Put(_syntaxFactory.InvocationExpression(
                context.Expr.Get<ExpressionSyntax>(),
                context.ArgList?.Get<ArgumentListSyntax>()
                    ?? _syntaxFactory.ArgumentList(
                        SyntaxFactory.MakeToken(SyntaxKind.OpenParenToken),
                        default(SeparatedSyntaxList<ArgumentSyntax>),
                        SyntaxFactory.MakeToken(SyntaxKind.CloseParenToken))));
        }

        public override void ExitBoundCondAccessExpr([NotNull] XP.BoundCondAccessExprContext context)
        {
            context.Put(_syntaxFactory.ConditionalAccessExpression(
                context.Left.Get<ExpressionSyntax>(),
                SyntaxFactory.MakeToken(SyntaxKind.QuestionToken),
                context.Right.Get<ExpressionSyntax>()
            ));
        }

        public override void ExitBindMemberAccess([NotNull] XP.BindMemberAccessContext context)
        {
            context.Put(_syntaxFactory.MemberBindingExpression(
                SyntaxFactory.MakeToken(SyntaxKind.DotToken),
                context.Name.Get<SimpleNameSyntax>()));
        }

        public override void ExitBindArrayAccess([NotNull] XP.BindArrayAccessContext context)
        {
            context.Put(_syntaxFactory.ElementBindingExpression(
                context.ArgList?.Get<BracketedArgumentListSyntax>() ?? EmptyBracketedArgumentList()
            ));
        }

        public override void ExitAccessMember([NotNull] XP.AccessMemberContext context)
         {
            context.Put(_syntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                context.Expr.Get<ExpressionSyntax>(),
                SyntaxFactory.MakeToken(SyntaxKind.DotToken),
                context.Name.Get<SimpleNameSyntax>()));
        }

        public override void ExitPostfixExpression([NotNull] XP.PostfixExpressionContext context)
        {
            context.Put(_syntaxFactory.PostfixUnaryExpression(
                context.Op.ExpressionKindPostfixOp(),
                context.Expr.Get<ExpressionSyntax>(),
                context.Op.SyntaxOp()));
        }

        public override void ExitPrefixExpression([NotNull] XP.PrefixExpressionContext context)
        {
            // Note 
            // in VO ~is XOR for binary expressions and bitwise negation (ones complement) for unary expressions
            // in C# ^is XOR and ~is Bitwise negation (ones complement)
            // SyntaxPrefixOp() takes care of the Unary operators
            context.Put(_syntaxFactory.PrefixUnaryExpression(
                context.Op.ExpressionKindPrefixOp(),
                context.Op.SyntaxPrefixOp(),
                context.Expr.Get<ExpressionSyntax>()));
        }

        public override void ExitBinaryExpression([NotNull] XP.BinaryExpressionContext context)
        {
           // when /vo12 is used then for the types .DIV add conversion for the LHS and RHS to Double


            switch (context.Op.Type) {
                case XP.EXP:
                    context.Put(GenerateMethodCall("global::System.Math.Pow", 
                        _syntaxFactory.ArgumentList(SyntaxFactory.MakeToken(SyntaxKind.OpenParenToken),
                            MakeSeparatedList(MakeArgument(context.Left.Get<ExpressionSyntax>()),
                                MakeArgument(context.Right.Get<ExpressionSyntax>())),
                            SyntaxFactory.MakeToken(SyntaxKind.CloseParenToken))));
                    break;
                case XP.SUBSTR:
                    context.Put(
                        _syntaxFactory.BinaryExpression(
                            SyntaxKind.CoalesceExpression,
                            _syntaxFactory.BinaryExpression(SyntaxKind.GreaterThanExpression,
                                _syntaxFactory.ConditionalAccessExpression(
                                    context.Left.Get<ExpressionSyntax>(),
                                    SyntaxFactory.MakeToken(SyntaxKind.QuestionToken),
                                    _syntaxFactory.InvocationExpression(
                                        _syntaxFactory.MemberBindingExpression(SyntaxFactory.MakeToken(SyntaxKind.DotToken),
                                            GenerateSimpleName("IndexOf")
                                        ),
                                        _syntaxFactory.ArgumentList(SyntaxFactory.MakeToken(SyntaxKind.OpenParenToken),
                                            MakeSeparatedList(
                                                MakeArgument(_syntaxFactory.BinaryExpression(
                                                    SyntaxKind.CoalesceExpression,
                                                    context.Right.Get<ExpressionSyntax>(),
                                                    SyntaxFactory.MakeToken(SyntaxKind.QuestionQuestionToken),
                                                    GenerateLiteral("")
                                                )),
                                                MakeArgument(GenerateQualifiedName("global::System.StringComparison.Ordinal"))
                                            ), 
                                            SyntaxFactory.MakeToken(SyntaxKind.CloseParenToken)
                                        )
                                    )
                                ),
                                SyntaxFactory.MakeToken(SyntaxKind.GreaterThanToken),
                                GenerateLiteral("-1", -1)
                            ),
                            SyntaxFactory.MakeToken(SyntaxKind.QuestionQuestionToken),
                            GenerateLiteral(false)
                        )
                    );
                    break;
                case XP.ASSIGN_EXP:
                    context.Put(MakeSimpleAssignment(
                        context.Left.Get<ExpressionSyntax>(),
                        GenerateMethodCall("global::System.Math.Pow", 
                            _syntaxFactory.ArgumentList(SyntaxFactory.MakeToken(SyntaxKind.OpenParenToken),
                                MakeSeparatedList(MakeArgument(context.Left.Get<ExpressionSyntax>()),
                                    MakeArgument(context.Right.Get<ExpressionSyntax>())),
                                SyntaxFactory.MakeToken(SyntaxKind.CloseParenToken)))));
                    break;
                case XP.GT:
                    if (context.Gt == null)     // Normal Greater than
                        goto default;
                    
                    SyntaxToken token = GetRShiftToken(context.Op, context.Gt);
                    context.Put(_syntaxFactory.BinaryExpression(
                        SyntaxKind.RightShiftExpression,
                        context.Left.Get<ExpressionSyntax>(),
                        token,
                        context.Right.Get<ExpressionSyntax>()));

                    break;
                default:
                    // Note 
                    // in VO ~is XOR for binary expressions and bitwise negation for unary expressions
                    // in C# ^is XOR and ~is Bitwise negation 
                    // SyntaxOp() takes care of the Binary Operators
                    context.Put(_syntaxFactory.BinaryExpression(
                        context.Op.ExpressionKindBinaryOp(),
                        context.Left.Get<ExpressionSyntax>(),
                        context.Op.SyntaxOp(),
                        context.Right.Get<ExpressionSyntax>()));
                    break;
            }
        }

        public override void ExitAssignmentExpression([NotNull] XP.AssignmentExpressionContext context)
        {
            context.Put(_syntaxFactory.AssignmentExpression(
                context.Op.ExpressionKindBinaryOp(),
                context.Left.Get<ExpressionSyntax>(),
                context.Op.SyntaxOp(),
                context.Right.Get<ExpressionSyntax>()));
        }

        public override void ExitPrimaryExpression([NotNull] XP.PrimaryExpressionContext context)
        {
            context.Put(context.Expr.Get<ExpressionSyntax>());
        }

        public override void ExitCheckedExpression([NotNull] XP.CheckedExpressionContext context)
        {
            context.Put(_syntaxFactory.CheckedExpression(context.ch.ExpressionKind(),
                context.ch.SyntaxKeyword(),
                SyntaxFactory.MakeToken(SyntaxKind.OpenParenToken),
                context.Expr.Get<ExpressionSyntax>(),
                SyntaxFactory.MakeToken(SyntaxKind.CloseParenToken)));
        }

        private bool GenerateAltD(XP.MethodCallContext context) {
            // Pseudo function AltD()
            ArgumentListSyntax argList;
            if(context.ArgList != null) {
                argList = context.ArgList.Get<ArgumentListSyntax>();
            } else {
                argList = EmptyArgumentList();
            }
            var expr = GenerateMethodCall("global::System.Diagnostics.Debugger.Break", argList);
            var cond = _syntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                            GenerateQualifiedName("global::System.Diagnostics.Debugger"),
                        SyntaxFactory.MakeToken(SyntaxKind.DotToken),
                        GenerateSimpleName("IsAttached"));
            var stmt = _syntaxFactory.ExpressionStatement(expr, SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken));
            context.Put(GenerateIfStatement(cond, stmt));
            return true;
        }

        private bool GenerateGetInst(XP.MethodCallContext context) {
            // Pseudo function _GetInst()
            ArgumentListSyntax argList;
            ExpressionSyntax expr;
            if(context.ArgList != null) {
                argList = context.ArgList.Get<ArgumentListSyntax>();
                if(argList.Arguments.Count != 0) {
                    context.Put(GenerateLiteral(0).WithAdditionalDiagnostics(new SyntaxDiagnosticInfo(ErrorCode.ERR_BadArgCount, "_getInst", argList.Arguments.Count)));
                    return true;
                }
            }

            TypeSyntax globaltype = GenerateQualifiedName(GlobalClassName);
            expr = _syntaxFactory.TypeOfExpression(
                SyntaxFactory.MakeToken(SyntaxKind.TypeOfKeyword),
                SyntaxFactory.MakeToken(SyntaxKind.OpenParenToken),
                globaltype,
                SyntaxFactory.MakeToken(SyntaxKind.CloseParenToken));

            expr = _syntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                                        expr,
                                    SyntaxFactory.MakeToken(SyntaxKind.DotToken),
                                    GenerateSimpleName("Module"));
            var arg = MakeArgument(expr);
            argList = MakeArgumentList(arg);
            expr = GenerateMethodCall("global::System.Runtime.InteropServices.Marshal.GetHINSTANCE", argList);
            context.Put(expr);
            return true;
        }


        public override void ExitMethodCall([NotNull] XP.MethodCallContext context) {
            var expr = context.Expr.Get<ExpressionSyntax>();
            ArgumentListSyntax argList;
            if(context.ArgList != null) {
                argList = context.ArgList.Get<ArgumentListSyntax>();
            } else {
                argList = EmptyArgumentList();
            }
            string name = null;
            if(expr is IdentifierNameSyntax) {
                IdentifierNameSyntax ins = expr as IdentifierNameSyntax;
                name = ins.Identifier.Text.ToUpper();
                if(name == "ALTD" && GenerateAltD(context))
                    return;
                if(name == "_GETINST" && GenerateGetInst(context))
                    return;
            }
            context.Put(_syntaxFactory.InvocationExpression(expr, argList));
        }

        public override void ExitCtorCall([NotNull] XP.CtorCallContext context)
        {
            if (!(context.Type is XP.ArrayDatatypeContext)) {
                var type = context.Type.Get<TypeSyntax>() ;
                ArgumentListSyntax argList;
                if (context.ArgList != null)
                    argList = context.ArgList.Get<ArgumentListSyntax>();
                else
                {
                    var openParen = SyntaxFactory.MakeToken(SyntaxKind.OpenParenToken);
                    var closeParen = SyntaxFactory.MakeToken(SyntaxKind.CloseParenToken);
                    var args = default(SeparatedSyntaxList<ArgumentSyntax>);
                    argList = _syntaxFactory.ArgumentList(openParen, args, closeParen);
                }
                context.Put(CreateObject(type, argList,null)); // TODO: (grammar) object creation initializer
            }
            else {
                var type = (context.Type as XP.ArrayDatatypeContext).TypeName.Get<TypeSyntax>();
                var arrayType = context.Type.Get<ArrayTypeSyntax>();
                var rankSpecifiers = new ArrayRankSpecifierSyntax[arrayType.RankSpecifiers.Count];
                for(int i = 0; i < rankSpecifiers.Length; i++) {
                    rankSpecifiers[i] = arrayType.RankSpecifiers[i];
                }
                int ranks = rankSpecifiers[0].Sizes.Count;
                if (ranks != context.ArgList?._Args?.Count)
                    context.AddError(new ParseErrorData(ErrorCode.ERR_BadCtorArgCount,context.Type.GetText(), context.ArgList?._Args?.Count ?? 0));
                var sizes = _pool.AllocateSeparated<ExpressionSyntax>();
                if (context.ArgList?._Args != null) {
                    foreach (var size in context.ArgList?._Args) {
                        if (size.Name != null)
                            context.AddError(new ParseErrorData(size,ErrorCode.ERR_BadNamedArgument,size));
                        if (size.RefOut != null)
                            context.AddError(new ParseErrorData(size,ErrorCode.ERR_BadTypeArgument,size));
                        if (sizes.Count > 0)
                            sizes.AddSeparator(SyntaxFactory.MakeToken(SyntaxKind.CommaToken));
                        sizes.Add(size.Expr.Get<ExpressionSyntax>());
                    }
                }
                rankSpecifiers[0] = _syntaxFactory.ArrayRankSpecifier(
                            SyntaxFactory.MakeToken(SyntaxKind.OpenBracketToken),
                            sizes,
                            SyntaxFactory.MakeToken(SyntaxKind.CloseBracketToken));
                _pool.Free(sizes);
                context.Put(_syntaxFactory.ArrayCreationExpression(SyntaxFactory.MakeToken(SyntaxKind.NewKeyword),
                    _syntaxFactory.ArrayType(type, MakeList(rankSpecifiers)),
                    null));
            }
        }

        public override void ExitDelegateCtorCall([NotNull] XP.DelegateCtorCallContext context)
        {
            if (((context.Obj as XP.PrimaryExpressionContext)?.Expr as XP.LiteralExpressionContext)?.Literal.Token.Type == XP.NULL) {
                context.Put(MakeCastTo(context.Type.Get<TypeSyntax>(),context.Func.Get<NameSyntax>()));
            }
            else {
                var fobj = context.Obj.Get<ExpressionSyntax>();
                SimpleNameSyntax fname;
                if (context.Func.CsNode is SimpleNameSyntax) {
                    fname = context.Func.Get<SimpleNameSyntax>();
                }
                else {
                    var fCtx = context.Func as XP.QualifiedNameContext;
                    if (fCtx != null) {
                        fobj = MakeCastTo(fCtx.Left.Get<NameSyntax>(),fobj);
                        fname = fCtx.Right.Get<SimpleNameSyntax>();
                    }
                    else {
                        fname = _syntaxFactory.IdentifierName(SyntaxFactory.MakeIdentifier("<missing>"));
                        context.AddError(new ParseErrorData(fCtx, ErrorCode.ERR_IdentifierExpected));
                    }
                }
                context.Put(MakeCastTo(
                    context.Type.Get<TypeSyntax>(),
                    _syntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                        fobj,
                        SyntaxFactory.MakeToken(SyntaxKind.DotToken),
                        fname)
                    ));
            }
        }

        public override void ExitArrayAccess([NotNull] XP.ArrayAccessContext context)
        {
            var expr = context.Expr.Get<ExpressionSyntax>();
            BracketedArgumentListSyntax argList;
            if (context.ArgList != null)
            {
                argList = context.ArgList.Get<BracketedArgumentListSyntax>();
            }
            else
            {
                var openBracket = SyntaxFactory.MakeToken(SyntaxKind.OpenBracketToken);
                var closeBracket = SyntaxFactory.MakeToken(SyntaxKind.CloseBracketToken);
                var args = default(SeparatedSyntaxList<ArgumentSyntax>);
                argList = _syntaxFactory.BracketedArgumentList(openBracket, args, closeBracket);
            }
            context.Put(_syntaxFactory.ElementAccessExpression(expr,argList));
        }

        public override void ExitNameExpression([NotNull] XP.NameExpressionContext context)
        {
            ExpressionSyntax expr = context.Name.Get<NameSyntax>();
            context.Put(expr);
        }

        public override void ExitTypeExpression([NotNull] XP.TypeExpressionContext context)
        {
            if (context.Type != null)
                context.Put(context.Type.Get<TypeSyntax>());
            else
                context.Put(context.XType.Get<TypeSyntax>());
        }

        public override void ExitIifExpression([NotNull] XP.IifExpressionContext context)
        {
            context.Put(context.Expr.Get<ExpressionSyntax>());
        }

        public override void ExitParenExpression([NotNull] XP.ParenExpressionContext context)
        {
            context.Put(_syntaxFactory.ParenthesizedExpression(
                SyntaxFactory.MakeToken(SyntaxKind.OpenParenToken),
                context.Expr.Get<ExpressionSyntax>(),
                SyntaxFactory.MakeToken(SyntaxKind.CloseParenToken)));
        }

        public override void ExitIntrinsicExpression([NotNull] XP.IntrinsicExpressionContext context)
        {
            var kind = context.Op.ExpressionKindBinaryOp();
            var syntax = context.Op.SyntaxOp();
            if (kind == SyntaxKind.BitwiseNotExpression) {
                if (context._Exprs?.Count > 1) {
                    context.AddError(new ParseErrorData(context.COMMA()[0], ErrorCode.ERR_CloseParenExpected));
                }
                context.Put(_syntaxFactory.PrefixUnaryExpression(
                    kind,
                    syntax,
                    context._Exprs?[0].Get<ExpressionSyntax>()));
            }
            else {
                var e = context._Exprs?[0].Get<ExpressionSyntax>();
                if (context._Exprs.Count > 1)
                { 
                    for (int i = 1; i < context._Exprs?.Count; i++) {
                        context.Put(_syntaxFactory.BinaryExpression(
                            kind,
                            e,
                            syntax,
                            context._Exprs[i].Get<ExpressionSyntax>()));
                    }
                }
                else
                {
                    context.Put(e);
                    context.AddError(new ParseErrorData(context.Op, ErrorCode.ERR_MissingArgument));
                }
            }
        }

        public override void ExitTypeCheckExpression([NotNull] XP.TypeCheckExpressionContext context)
        {
            context.Put(_syntaxFactory.BinaryExpression(
                SyntaxKind.IsExpression,
                context.Expr.Get<ExpressionSyntax>(),
                SyntaxFactory.MakeToken(SyntaxKind.IsKeyword),
                context.Type.Get<ExpressionSyntax>()));
        }

        public override void ExitTypeCast([NotNull] XP.TypeCastContext context)
        {
            context.Put(MakeCastTo(context.Type.Get<TypeSyntax>(),context.Expr.Get<ExpressionSyntax>()));
        }

        public override void ExitVoConversionExpression([NotNull] XP.VoConversionExpressionContext context)
        {
            if (context.Type != null)
            {
                context.Put(MakeCastTo(context.Type.Get<TypeSyntax>(),context.Expr.Get<ExpressionSyntax>()));
            }
            else if (context.XType != null)
            {
                context.Put(MakeCastTo(context.XType.Get<TypeSyntax>(),context.Expr.Get<ExpressionSyntax>()));
            }
        }

        public override void ExitVoCastExpression([NotNull] XP.VoCastExpressionContext context)
        {
            context.Put(_syntaxFactory.CheckedExpression(SyntaxKind.UncheckedExpression,
                SyntaxFactory.MakeToken(SyntaxKind.UncheckedKeyword),
                SyntaxFactory.MakeToken(SyntaxKind.OpenParenToken),
                MakeCastTo(context.Type.Get<TypeSyntax>(),context.Expr.Get<ExpressionSyntax>()),
                SyntaxFactory.MakeToken(SyntaxKind.CloseParenToken)));
        }

        public override void ExitVoCastPtrExpression([NotNull] XP.VoCastPtrExpressionContext context)
        {
            context.Put(MakeCastTo(
                _syntaxFactory.PointerType(context.Type.Get<TypeSyntax>(),SyntaxFactory.MakeToken(SyntaxKind.AsteriskToken)),
                _syntaxFactory.PrefixUnaryExpression(SyntaxKind.AddressOfExpression,
                    SyntaxFactory.MakeToken(SyntaxKind.AmpersandToken),
                    context.Expr.Get<ExpressionSyntax>())));
        }

        public override void ExitSizeOfExpression([NotNull] XP.SizeOfExpressionContext context)
        {
            context.Put(_syntaxFactory.SizeOfExpression(
                SyntaxFactory.MakeToken(SyntaxKind.SizeOfKeyword),
                SyntaxFactory.MakeToken(SyntaxKind.OpenParenToken),
                context.Type.Get<TypeSyntax>(),
                SyntaxFactory.MakeToken(SyntaxKind.CloseParenToken)));
        }

        public override void ExitTypeOfExpression([NotNull] XP.TypeOfExpressionContext context)
        {
            context.Put(_syntaxFactory.TypeOfExpression(
                SyntaxFactory.MakeToken(SyntaxKind.TypeOfKeyword),
                SyntaxFactory.MakeToken(SyntaxKind.OpenParenToken),
                context.Type.Get<TypeSyntax>(),
                SyntaxFactory.MakeToken(SyntaxKind.CloseParenToken)));
        }

        public override void ExitDefaultExpression([NotNull] XP.DefaultExpressionContext context)
        {
            context.Put(_syntaxFactory.DefaultExpression(
                SyntaxFactory.MakeToken(SyntaxKind.DefaultKeyword),
                SyntaxFactory.MakeToken(SyntaxKind.OpenParenToken),
                context.Type.Get<TypeSyntax>(),
                SyntaxFactory.MakeToken(SyntaxKind.CloseParenToken)));
        }

        public override void ExitBracketedArgumentList([NotNull] XP.BracketedArgumentListContext context)
        {
            var args = _pool.AllocateSeparated<ArgumentSyntax>();
            foreach (var argCtx in context._Args)
            {
                if (args.Count != 0)
                    args.AddSeparator(SyntaxFactory.MakeToken(SyntaxKind.CommaToken));
                args.Add(argCtx.Get<ArgumentSyntax>());
            }
            context.Put(_syntaxFactory.BracketedArgumentList(
                SyntaxFactory.MakeToken(SyntaxKind.OpenBracketToken), 
                args, 
                SyntaxFactory.MakeToken(SyntaxKind.CloseBracketToken)));
            _pool.Free(args);
        }

        public override void ExitArgumentList([NotNull] XP.ArgumentListContext context)
        {
            var args = _pool.AllocateSeparated<ArgumentSyntax>();
            var openParen = SyntaxFactory.MakeToken(SyntaxKind.OpenParenToken);
            var closeParen = SyntaxFactory.MakeToken(SyntaxKind.CloseParenToken);
            foreach (var argCtx in context._Args)
            {
                if (args.Count != 0)
                    args.AddSeparator(SyntaxFactory.MakeToken(SyntaxKind.CommaToken));
                args.Add(argCtx.Get<ArgumentSyntax>());
            }
            context.Put(_syntaxFactory.ArgumentList(openParen, args, closeParen));
            _pool.Free(args);
        }

        public override void ExitArgument([NotNull] XP.ArgumentContext context)
        {
            if (context.Expr == null) {
                context.Put(NotInDialect(MakeArgument(GenerateLiteral("")), "Missing arguments"));
                return;
            }
            context.Put(_syntaxFactory.Argument(
                context.Name == null ? null : _syntaxFactory.NameColon(context.Name.Get<IdentifierNameSyntax>(), SyntaxFactory.MakeToken(SyntaxKind.ColonToken)), 
                context.RefOut?.SyntaxKeyword(), context.Expr.Get<ExpressionSyntax>()));
        }

        public override void ExitQualifiedNameDot([NotNull] XP.QualifiedNameDotContext context)
        {
            context.Put(_syntaxFactory.QualifiedName(context.Left.Get<NameSyntax>(),
                SyntaxFactory.MakeToken(SyntaxKind.DotToken),
                context.Right.Get<SimpleNameSyntax>()));
        }

        public override void ExitSimpleOrAliasedNameDot([NotNull] XP.SimpleOrAliasedNameDotContext context)
        {
            context.Put(context.Name.Get<NameSyntax>());
        }

        public override void ExitQualifiedName([NotNull] XP.QualifiedNameContext context)
        {
            context.Put(_syntaxFactory.QualifiedName(context.Left.Get<NameSyntax>(),
                SyntaxFactory.MakeToken(SyntaxKind.DotToken),
                context.Right.Get<SimpleNameSyntax>()));
        }

        public override void ExitAliasQualifiedName([NotNull] XP.AliasQualifiedNameContext context)
        {
            context.Put(_syntaxFactory.AliasQualifiedName(context.Alias.Get<IdentifierNameSyntax>(),
                SyntaxFactory.MakeToken(SyntaxKind.ColonColonToken),
                context.Right.Get<SimpleNameSyntax>()));
        }

        public override void ExitSimpleOrAliasedName([NotNull] XP.SimpleOrAliasedNameContext context)
        {
            context.Put(context.Name.Get<NameSyntax>());
        }

        public override void ExitGlobalQualifiedName([NotNull] XP.GlobalQualifiedNameContext context)
        {
            context.Put(_syntaxFactory.AliasQualifiedName(_syntaxFactory.IdentifierName(context.Global.SyntaxKeyword()),
                SyntaxFactory.MakeToken(SyntaxKind.ColonColonToken),
                context.Right.Get<SimpleNameSyntax>()));
        }

        public override void ExitIdentifierOrGenericName([NotNull] XP.IdentifierOrGenericNameContext context)
        {
            context.Put(context.Name.Get<SimpleNameSyntax>());
        }

        public override void ExitSimpleName([NotNull] XP.SimpleNameContext context)
        {
            if (context.GenericArgList == null)
                context.Put(_syntaxFactory.IdentifierName(context.Id.Get<SyntaxToken>()));
            else
                context.Put(_syntaxFactory.GenericName(context.Id.Get<SyntaxToken>(), context.GenericArgList.Get<TypeArgumentListSyntax>()));
        }

        public override void ExitGenericArgumentList([NotNull] XP.GenericArgumentListContext context)
        {
            var types = _pool.AllocateSeparated<TypeSyntax>();
            foreach (var type in context._GenericArgs)
            {
                if (types.Count != 0)
                    types.AddSeparator(SyntaxFactory.MakeToken(SyntaxKind.CommaToken));
                types.Add(type.Get<TypeSyntax>());
            }
            context.Put(_syntaxFactory.TypeArgumentList(
                SyntaxFactory.MakeToken(SyntaxKind.LessThanToken),
                types.ToList(),
                SyntaxFactory.MakeToken(SyntaxKind.GreaterThanToken)
                ));
            _pool.Free(types);
        }

        public override void ExitIdentifierName([NotNull] XP.IdentifierNameContext context)
        {
            context.Put(_syntaxFactory.IdentifierName(context.Id.Get<SyntaxToken>()));
        }

        public override void ExitPtrDatatype([NotNull] XP.PtrDatatypeContext context)
        {
            context.Put(
                _syntaxFactory.PointerType(context.TypeName.Get<TypeSyntax>(),
                SyntaxFactory.MakeToken(SyntaxKind.AsteriskToken)));
        }

        public override void ExitArrayDatatype([NotNull] XP.ArrayDatatypeContext context)
        {
            var ranks = _pool.Allocate<ArrayRankSpecifierSyntax>();
            foreach (var rankCtx in context._Ranks)
            {
                ranks.Add(rankCtx.Get<ArrayRankSpecifierSyntax>());
            }
            context.Put(_syntaxFactory.ArrayType(context.TypeName.Get<TypeSyntax>(), ranks));
            _pool.Free(ranks);
        }

        public override void ExitArrayRank([NotNull] XP.ArrayRankContext context)
        {
            var sizes = _pool.AllocateSeparated<ExpressionSyntax>();
            var omittedArraySizeExpressionInstance = _syntaxFactory.OmittedArraySizeExpression(SyntaxFactory.MakeToken(SyntaxKind.OmittedArraySizeExpressionToken));
            foreach (var comma in context._Commas)
            {
                sizes.Add(omittedArraySizeExpressionInstance);
                sizes.AddSeparator(SyntaxFactory.MakeToken(SyntaxKind.CommaToken));
            }
            sizes.Add(omittedArraySizeExpressionInstance);
            context.Put(_syntaxFactory.ArrayRankSpecifier(
                SyntaxFactory.MakeToken(SyntaxKind.OpenBracketToken),
                sizes,
                SyntaxFactory.MakeToken(SyntaxKind.CloseBracketToken)));
            _pool.Free(sizes);
        }

        public override void ExitSimpleDatatype([NotNull] XP.SimpleDatatypeContext context)
        {
            context.Put(context.TypeName.Get<TypeSyntax>());
        }

        public override void ExitNullableDatatype([NotNull] XP.NullableDatatypeContext context)
        {
            context.Put(_syntaxFactory.NullableType(context.TypeName.Get<TypeSyntax>(), SyntaxFactory.MakeToken(SyntaxKind.QuestionToken)));
        }

        public override void ExitTypeName([NotNull] XP.TypeNameContext context)
        {
            if (context.NativeType != null)
                context.Put(context.NativeType.Get<TypeSyntax>());
            else if (context.XType != null)
                context.Put(context.XType.Get<TypeSyntax>());
            else if (context.Name != null)
                context.Put(context.Name.Get<NameSyntax>());
        }

        public override void ExitAwaitExpression([NotNull] XP.AwaitExpressionContext context)
        {
            context.Put(_syntaxFactory.AwaitExpression(SyntaxFactory.MakeToken(SyntaxKind.AwaitKeyword),context.Expr.Get<ExpressionSyntax>()));
        }

        public override void ExitSelfExpression([NotNull] XP.SelfExpressionContext context)
        {
            context.Put(_syntaxFactory.ThisExpression(context.Key.SyntaxKeyword()));
        }

        public override void ExitSuperExpression([NotNull] XP.SuperExpressionContext context)
        {
            context.Put(_syntaxFactory.BaseExpression(context.Key.SyntaxKeyword()));
        }

        public override void ExitLiteralExpression([NotNull] XP.LiteralExpressionContext context)
        {
            context.Put(context.Literal.Get<ExpressionSyntax>());
        }

        public override void ExitLiteralArrayExpression([NotNull] XP.LiteralArrayExpressionContext context)
        {
            context.Put(context.LiteralArray.Get<ExpressionSyntax>());
        }

        public override void ExitAnonTypeExpression([NotNull] XP.AnonTypeExpressionContext context)
        {
            context.Put(context.AnonType.Get<AnonymousObjectCreationExpressionSyntax>());
        }


        public override void ExitIif([NotNull] XP.IifContext context)
        {
            // if /vo10 is used then cast the LHS and RHS to USUAL or OBJECT depending on the dialect
            ExpressionSyntax left = context.TrueExpr.Get<ExpressionSyntax>();
            ExpressionSyntax right = context.FalseExpr.Get<ExpressionSyntax>();
            if (_options.VOCompatibleIIF)
            {
                left = MakeCastTo(_objectType, left);
                right = MakeCastTo(_objectType, right);
            }
            context.Put(_syntaxFactory.ConditionalExpression(
                context.Cond.Get<ExpressionSyntax>(),
                SyntaxFactory.MakeToken(SyntaxKind.QuestionToken),
                left,
                SyntaxFactory.MakeToken(SyntaxKind.ColonToken),
                right));
        }

        public override void ExitLiteralArray([NotNull] XP.LiteralArrayContext context)
        {
            TypeSyntax type = null;
            ExpressionSyntax expr = null;
            // detect typed arrays.
            // <LONG> {...} indicates an array of type LONG
            // when no type is specified and the dialect VO or Vulcan the type is USUAL
            if(context.Type != null)
            {
                type = context.Type.Get<TypeSyntax>();
            }
            SeparatedSyntaxList<ExpressionSyntax> exprs;
            if((context._Elements?.Count ?? 0) > 0) {
                // 
                var l = _pool.AllocateSeparated<ExpressionSyntax>();
                foreach(var item in context._Elements) {
                    if(l.Count > 0)
                        l.AddSeparator(SyntaxFactory.MakeToken(SyntaxKind.CommaToken));
                    if(item.Expr != null)
                        l.Add(item.Expr.Get<ExpressionSyntax>());
                    else
                        l.Add((ExpressionSyntax)NotInDialect(GenerateLiteral(false), "omitting (typed) array elements"));
                }
                exprs = l.ToList();
                _pool.Free(l);
            } else {
                exprs = default(SeparatedSyntaxList<ExpressionSyntax>);
            }

            var initializer = _syntaxFactory.InitializerExpression(SyntaxKind.ArrayInitializerExpression, 
                SyntaxFactory.MakeToken(SyntaxKind.OpenBraceToken), 
                exprs, 
                SyntaxFactory.MakeToken(SyntaxKind.CloseBraceToken));
            if (type != null)
            {
                expr = _syntaxFactory.ArrayCreationExpression(SyntaxFactory.MakeToken(SyntaxKind.NewKeyword),
                    _syntaxFactory.ArrayType(type,
                    MakeList(_syntaxFactory.ArrayRankSpecifier(
                        SyntaxFactory.MakeToken(SyntaxKind.OpenBracketToken),
                        MakeSeparatedList<ExpressionSyntax>(
                            _syntaxFactory.OmittedArraySizeExpression(SyntaxFactory.MakeToken(SyntaxKind.OmittedArraySizeExpressionToken))),
                        SyntaxFactory.MakeToken(SyntaxKind.CloseBracketToken)))),
                    initializer);
            }
            else
            {
                expr = _syntaxFactory.ImplicitArrayCreationExpression(SyntaxFactory.MakeToken(SyntaxKind.NewKeyword),
                    SyntaxFactory.MakeToken(SyntaxKind.OpenBracketToken),
                    EmptyList(),
                    SyntaxFactory.MakeToken(SyntaxKind.CloseBracketToken),
                    initializer)
                    .WithAdditionalDiagnostics(new SyntaxDiagnosticInfo(ErrorCode.ERR_UntypedArrayNotAvailableInDialect, _options.Dialect.ToString()));
            }
            context.Put<ExpressionSyntax>(expr);
        }


        public override void ExitArrayElement([NotNull] XP.ArrayElementContext context) {
            if(context.Expr != null)
                context.Put(context.Expr.Get<ExpressionSyntax>());
            return;
        }


        public override void ExitAnonType([NotNull] XP.AnonTypeContext context)
        {
            context.Put(_syntaxFactory.AnonymousObjectCreationExpression(
                SyntaxFactory.MakeToken(SyntaxKind.NewKeyword),
                SyntaxFactory.MakeToken(SyntaxKind.OpenBraceToken),
                MakeSeparatedList<AnonymousObjectMemberDeclaratorSyntax>(context._Members),
                SyntaxFactory.MakeToken(SyntaxKind.CloseBraceToken)));
        }

        public override void ExitAnonMember([NotNull] XP.AnonMemberContext context)
        {
            context.Put(_syntaxFactory.AnonymousObjectMemberDeclarator(
                _syntaxFactory.NameEquals(context.Name.Get<IdentifierNameSyntax>(),SyntaxFactory.MakeToken(SyntaxKind.EqualsToken)),
                context.Expr.Get<ExpressionSyntax>()));
        }

        public override void ExitCodeblockExpression([NotNull] XP.CodeblockExpressionContext context)
        {
            context.Put(context.CbExpr.Get<LambdaExpressionSyntax>());
        }

        public override void ExitCodeblock([NotNull] XP.CodeblockContext context)
        {
            context.Put(_syntaxFactory.ParenthesizedLambdaExpression(
                asyncKeyword: null,
                parameterList: context.CbParamList?.Get<ParameterListSyntax>() ?? EmptyParameterList(),
                arrowToken: SyntaxFactory.MakeToken(SyntaxKind.EqualsGreaterThanToken), 
                body: (CSharpSyntaxNode)context.Expr?.Get<ExpressionSyntax>() ?? context.StmtBlk?.Get<BlockSyntax>() ?? context.ExprList?.Get<BlockSyntax>() ?? MakeBlock(MakeList<StatementSyntax>())
                ));
        }

        public override void ExitCodeblockParamList([NotNull] XP.CodeblockParamListContext context)
        {
            var @params = _pool.AllocateSeparated<ParameterSyntax>();
            foreach (var idCtx in context._Ids)
            {
                if (@params.Count>0)
                    @params.AddSeparator(SyntaxFactory.MakeToken(SyntaxKind.CommaToken));
                SyntaxListBuilder<AttributeListSyntax> attributeLists = _pool.Allocate<AttributeListSyntax>();
                SyntaxListBuilder modifiers = _pool.Allocate();
                @params.Add(_syntaxFactory.Parameter(
                    attributeLists: attributeLists,
                    modifiers: modifiers.ToTokenList(),
                    type: null, // TODO: (grammar) codeblock param type
                    identifier: idCtx.Get<SyntaxToken>(),
                    @default: null));
                _pool.Free(attributeLists);
                _pool.Free(modifiers);
            }
            context.Put(_syntaxFactory.ParameterList(
                SyntaxFactory.MakeToken(SyntaxKind.OpenParenToken),
                @params,
                SyntaxFactory.MakeToken(SyntaxKind.CloseParenToken)));
            _pool.Free(@params);
        }

        public override void ExitCodeblockExprList([NotNull] XP.CodeblockExprListContext context)
        {
            context.Put(MakeBlock(MakeList<StatementSyntax>(
                from ctx in context._Exprs select _syntaxFactory.ExpressionStatement(ctx.Get<ExpressionSyntax>(), SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken)),
                _syntaxFactory.ReturnStatement(SyntaxFactory.MakeToken(SyntaxKind.ReturnKeyword), context.ReturnExpr.Get<ExpressionSyntax>(), SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken))
                )));
        }

        public override void ExitQueryExpression([NotNull] XP.QueryExpressionContext context)
        {
            context.Put(context.Query.Get<QueryExpressionSyntax>());
        }

        public override void ExitLinqQuery([NotNull] XP.LinqQueryContext context)
        {
            context.Put(_syntaxFactory.QueryExpression(
                context.From.Get<FromClauseSyntax>(),
                context.Body.Get<QueryBodySyntax>()
                ));
        }

        public override void ExitFromClause([NotNull] XP.FromClauseContext context)
        {
            context.Put(_syntaxFactory.FromClause(SyntaxFactory.MakeToken(SyntaxKind.FromKeyword),
                context.Type?.Get<TypeSyntax>(),
                context.Id.Get<SyntaxToken>(),
                SyntaxFactory.MakeToken(SyntaxKind.InKeyword),
                context.Expr.Get<ExpressionSyntax>()
                ));
        }

        public override void ExitQueryBody([NotNull] XP.QueryBodyContext context)
        {
            context.Put(_syntaxFactory.QueryBody(
                MakeList<QueryClauseSyntax>(context._Bodyclauses),
                context.SorG.Get<SelectOrGroupClauseSyntax>(),
                context.Continuation?.Get<QueryContinuationSyntax>()
                ));
        }

        public override void ExitFromBodyClause([NotNull] XP.FromBodyClauseContext context)
        {
            context.Put(context.From.Get<FromClauseSyntax>());
        }

        public override void ExitLetClause([NotNull] XP.LetClauseContext context)
        {
            context.Put(_syntaxFactory.LetClause(
                SyntaxFactory.MakeToken(SyntaxKind.LetKeyword),
                context.Id.Get<SyntaxToken>(),
                SyntaxFactory.MakeToken(SyntaxKind.EqualsToken),
                context.Expr.Get<ExpressionSyntax>()
                ));
        }

        public override void ExitWhereClause([NotNull] XP.WhereClauseContext context)
        {
            context.Put(_syntaxFactory.WhereClause(
                SyntaxFactory.MakeToken(SyntaxKind.WhereKeyword),
                context.Expr.Get<ExpressionSyntax>()
                ));
        }

        public override void ExitJoinClause([NotNull] XP.JoinClauseContext context)
        {
            context.Put(_syntaxFactory.JoinClause(
                SyntaxFactory.MakeToken(SyntaxKind.JoinKeyword),
                context.Type?.Get<TypeSyntax>(),
                context.Id.Get<SyntaxToken>(),
                SyntaxFactory.MakeToken(SyntaxKind.InKeyword),
                context.Expr.Get<ExpressionSyntax>(),
                SyntaxFactory.MakeToken(SyntaxKind.OnKeyword),
                context.OnExpr.Get<ExpressionSyntax>(),
                SyntaxFactory.MakeToken(SyntaxKind.EqualsToken),
                context.EqExpr.Get<ExpressionSyntax>(),
                context.Into?.Get<JoinIntoClauseSyntax>()
                ));
        }

        public override void ExitJoinIntoClause([NotNull] XP.JoinIntoClauseContext context)
        {
            context.Put(_syntaxFactory.JoinIntoClause(
                SyntaxFactory.MakeToken(SyntaxKind.IntoKeyword),
                context.Id.Get<SyntaxToken>()));
        }

        public override void ExitOrderbyClause([NotNull] XP.OrderbyClauseContext context)
        {
            context.Put(_syntaxFactory.OrderByClause(
                SyntaxFactory.MakeToken(SyntaxKind.OrderByKeyword),
                MakeSeparatedList<OrderingSyntax>(context._Orders)
                ));
        }

        public override void ExitOrdering([NotNull] XP.OrderingContext context)
        {
            SyntaxToken direction;
            SyntaxKind kind;
            if (context.Direction != null && context.Direction.Type == XP.DESCENDING)
            {
                direction = SyntaxFactory.MakeToken(SyntaxKind.DescendingKeyword);
                kind = SyntaxKind.DescendingOrdering;
                }
            else
            {
                direction = SyntaxFactory.MakeToken(SyntaxKind.AscendingKeyword);
                kind = SyntaxKind.AscendingOrdering;
            }
            context.Put(_syntaxFactory.Ordering(kind,context.Expr.Get<ExpressionSyntax>(),direction));

        }

        public override void ExitSelectClause([NotNull] XP.SelectClauseContext context)
        {
            context.Put(_syntaxFactory.SelectClause(
                SyntaxFactory.MakeToken(SyntaxKind.SelectKeyword),
                context.Expr.Get<ExpressionSyntax>()
                ));
        }

        public override void ExitGroupClause([NotNull] XP.GroupClauseContext context)
        {
            context.Put(_syntaxFactory.GroupClause(
                SyntaxFactory.MakeToken(SyntaxKind.GroupKeyword),
                context.Expr.Get<ExpressionSyntax>(),
                SyntaxFactory.MakeToken(SyntaxKind.ByKeyword),
                context.ByExpr.Get<ExpressionSyntax>()
                ));
        }

        public override void ExitQueryContinuation([NotNull] XP.QueryContinuationContext context)
        {
            context.Put(_syntaxFactory.QueryContinuation(
                SyntaxFactory.MakeToken(SyntaxKind.IntoKeyword),
                context.Id.Get<SyntaxToken>(),
                context.Body.Get<QueryBodySyntax>()
                ));
        }

        public override void ExitLiteralValue([NotNull] XP.LiteralValueContext context)
        {
            string replacement = null;

            if (context.Token.Type == XP.STRING_CONST && context.Token.Text.StartsWith("\"__"))
            {
                switch (context.Token.Text.ToLowerInvariant())
                {
                    case "\"__entity__\"":
                        replacement = GetEntityName(false);
                        break;
                    case "\"__sig__\"":
                        replacement = GetEntityName(true);
                        break;
                    default:
                        break;
                }
            }


            if (!String.IsNullOrEmpty(replacement))
            {
                context.Put(_syntaxFactory.LiteralExpression(context.Token.ExpressionKindLiteral(),
                    SyntaxToken.WithValue(SyntaxKind.StringLiteralToken, replacement, replacement )));
            }
            else
                context.Put(_syntaxFactory.LiteralExpression(context.Token.ExpressionKindLiteral(), context.Token.SyntaxLiteralValue(_options)));
        }

        public override void ExitIdentifierString([NotNull] XP.IdentifierStringContext context)
        {
            context.Put(_syntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression,
                context.Token?.SyntaxLiteralValue(_options)
                ?? context.XsToken?.Token.SyntaxLiteralValue(_options)
                ?? context.VnToken?.Token.SyntaxLiteralValue(_options)));
        }

        public override void ExitIdentifier([NotNull] XP.IdentifierContext context)
        {
            context.Put(context.Token?.SyntaxIdentifier()
                ?? context.XsToken?.Token.SyntaxIdentifier()
                ?? context.VnToken?.Token.SyntaxIdentifier());
        }

        public override void ExitKeyword([NotNull] XP.KeywordContext context)
        {
            context.Put(context.KwXs?.Token.SyntaxKeywordIdentifier()
                ?? context.KwVn?.Token.SyntaxKeywordIdentifier()
                ?? context.KwVo?.Token.SyntaxKeywordIdentifier());
        }

        public override void ExitKeywordxs([NotNull] XP.KeywordxsContext context)
        {
            // caught by the keyword/identifier rule
        }

        public override void ExitKeywordvn([NotNull] XP.KeywordvnContext context)
        {
            // caught by the keyword/identifier rule
        }

        public override void ExitKeywordvo([NotNull] XP.KeywordvoContext context)
        {
            // caught by the keyword/identifier rule
        }

        public override void ExitNativeType([NotNull] XP.NativeTypeContext context)
        {
            switch (context.Token.Type) {
                case XP.PTR:
                    context.Put(_syntaxFactory.PointerType(VoidType(),SyntaxFactory.MakeToken(SyntaxKind.AsteriskToken)));
                    break;
                case XP.DYNAMIC:
                    context.Put(_syntaxFactory.IdentifierName(context.Token.SyntaxIdentifier()));
                    break;
                default:
                    context.Put(_syntaxFactory.PredefinedType(context.Token.SyntaxNativeType()));
                    break;
            }
        }
        public override void ExitXbaseType([NotNull] XP.XbaseTypeContext context)
        {
            var type = (TypeSyntax) NotInDialect(_objectType,context.Token.Text);
            context.Put(type);
        }
        public override void ExitFieldStmt([NotNull] XP.FieldStmtContext context)
        {
            context.Put(context.Decl.Get<StatementSyntax>());
        }
        public override void ExitFielddecl([NotNull] XP.FielddeclContext context)
        {
            context.Put(NotInDialect("FIELD statement"));
            return;
        }

        private ExpressionSyntax NoAlias() {
            return (ExpressionSyntax ) NotInDialect(GenerateLiteral("alias"), "ALIAS(->) operator");
        }
        public override void ExitAliasedExpr([NotNull] XP.AliasedExprContext context)
        {
            context.Put(NoAlias());
            return;
        }

        public override void ExitAliasedField([NotNull] XP.AliasedFieldContext context)
        {
            context.Put(NoAlias());
            return;
        }

        public override void ExitMacro([NotNull] XP.MacroContext context)
        {
            context.Put((ExpressionSyntax)NotInDialect(GenerateLiteral("macro"), "MACRO compiler"));
            return;
        }
    }
}