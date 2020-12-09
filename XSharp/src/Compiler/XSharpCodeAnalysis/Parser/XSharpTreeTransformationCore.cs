﻿//
// Copyright (c) XSharp B.V.  All Rights Reserved.
// Licensed under the Apache License, Version 2.0.
// See License.txt in the project root for license information.
//

// Uncomment this define to dump the AST to the debug console.
//#define DUMP_TREE

using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Roslyn.Utilities;
using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Tree;
using LanguageService.CodeAnalysis.XSharp.SyntaxParser;
using System.Diagnostics; // PLEASE DO NOT REMOVE THIS!!!!
using XP = LanguageService.CodeAnalysis.XSharp.SyntaxParser.XSharpParser;
using Microsoft.CodeAnalysis.PooledObjects;
namespace Microsoft.CodeAnalysis.CSharp.Syntax.InternalSyntax
{
    using Microsoft.CodeAnalysis.Syntax.InternalSyntax;

    internal partial class XSharpTreeTransformationCore : XSharpBaseListener
    {
        #region Nested classes
        internal class SyntaxEntities
        {
            internal SyntaxListPool _pool;
            public SyntaxListBuilder<ExternAliasDirectiveSyntax> Externs;
            public SyntaxListBuilder<UsingDirectiveSyntax> Usings;
            public SyntaxListBuilder<AttributeListSyntax> Attributes;
            public SyntaxListBuilder<MemberDeclarationSyntax> Members;
            public SyntaxListBuilder<MemberDeclarationSyntax> GlobalClassMembers;
            public SyntaxListBuilder<MemberDeclarationSyntax> StaticGlobalClassMembers;
            public List<MemVarFieldInfo> FileWidePublics;
            public object LastMember;
            public bool LastIsStatic;
            public List<Tuple<int, String>> InitProcedures;
            public List<FieldDeclarationSyntax> Globals;
            public List<PragmaWarningDirectiveTriviaSyntax> PragmaWarnings ;
            public List<PragmaOption> PragmaOptions;


            public bool HasPCall;
            public bool NeedsProcessing;

            internal SyntaxEntities(SyntaxListPool pool)
            {
                Externs = pool.Allocate<ExternAliasDirectiveSyntax>();
                Usings = pool.Allocate<UsingDirectiveSyntax>();
                Attributes = pool.Allocate<AttributeListSyntax>();
                Members = pool.Allocate<MemberDeclarationSyntax>();
                GlobalClassMembers = pool.Allocate<MemberDeclarationSyntax>();
                StaticGlobalClassMembers = pool.Allocate<MemberDeclarationSyntax>();
                InitProcedures = new List<Tuple<int, String>>();
                Globals = new List<FieldDeclarationSyntax>();
                FileWidePublics = new List<MemVarFieldInfo>();
                _pool = pool;
                HasPCall = false;
                NeedsProcessing = false;
                LastIsStatic = false;
                LastMember = null;
                PragmaWarnings = null;
                PragmaOptions = null;
        }

            internal void Free()
            {
                _pool.Free(Members);
                _pool.Free(Attributes);
                _pool.Free(Usings);
                _pool.Free(Externs);
                _pool.Free(GlobalClassMembers);
                _pool.Free(StaticGlobalClassMembers);
            }
        }

        internal class SyntaxClassEntities
        {
            internal class VoPropertyInfo
            {
                internal SyntaxToken idName;
                internal XP.IMethodContext AccessMethodCtx = null;
                internal XP.IMethodContext AssignMethodCtx = null;
                internal XP.IMethodContext DupAccess = null;
                internal XP.IMethodContext DupAssign = null;
            }

            internal SyntaxListPool _pool;
            public SyntaxListBuilder<MemberDeclarationSyntax> Members;
            public Dictionary<string, VoPropertyInfo> VoProperties;

            internal SyntaxClassEntities(SyntaxListPool pool)
            {
                Members = pool.Allocate<MemberDeclarationSyntax>();
                _pool = pool;
            }

            internal void Free()
            {
                _pool.Free(Members);
            }

            internal void AddVoPropertyAccessor(XP.IMethodContext accessor, int Type, SyntaxToken idName)
            {
                if (VoProperties == null)
                    VoProperties = new Dictionary<string, VoPropertyInfo>(XSharpString.Comparer);
                string name = idName.Text;
                VoPropertyInfo propertyInfo;
                if (!VoProperties.TryGetValue(name, out propertyInfo))
                {
                    propertyInfo = new VoPropertyInfo();
                    propertyInfo.idName = idName;
                    VoProperties.Add(name, propertyInfo);
                }
                switch (Type)
                {
                    case XP.ACCESS:
                        if (propertyInfo.AccessMethodCtx != null)
                            propertyInfo.DupAccess = accessor;
                        else
                            propertyInfo.AccessMethodCtx = accessor;
                        break;
                    case XP.ASSIGN:
                        if (propertyInfo.AssignMethodCtx != null)
                            propertyInfo.DupAssign = accessor;
                        else
                            propertyInfo.AssignMethodCtx = accessor;
                        break;
                    default:
                        break;
                }
            }
        }
        #endregion

       

        #region Fields
        protected static object gate = new object();

        protected string GlobalClassName = XSharpSpecialNames.FunctionsClass;

        internal SyntaxListPool _pool;
        protected readonly ContextAwareSyntax _syntaxFactory; // Has context, the fields of which are resettable.
        protected readonly XSharpParser _parser;
        protected readonly CSharpParseOptions _options;
        protected readonly TypeSyntax _ptrType;
        protected readonly TypeSyntax _impliedType;
        protected readonly TypeSyntax _intType;
        protected readonly TypeSyntax _dateTimeType;
        protected readonly ArrayTypeSyntax _byteArrayType;

        protected string _fileName;
        protected bool _isScript;
        protected string _entryPoint;
        internal List<PragmaOption> PragmaOptions;
        protected List<PragmaWarningDirectiveTriviaSyntax> PragmaWarnings;

        internal SyntaxEntities GlobalEntities;
        internal SyntaxClassEntities GlobalClassEntities;
        internal Stack<SyntaxClassEntities> ClassEntities = new Stack<SyntaxClassEntities>();
        internal Stack<XP.IEntityContext> Entities = new Stack<XP.IEntityContext>();
        #endregion

        #region Static Fields
        protected static TypeSyntax intType = null;
        protected static TypeSyntax uintType = null;
        protected static TypeSyntax floatType = null;
        protected static TypeSyntax decimalType = null;
        protected static TypeSyntax doubleType = null;
        protected static TypeSyntax ulongType = null;
        protected static TypeSyntax longType = null;
        protected static TypeSyntax stringType = null;
        protected static TypeSyntax voidType;
        protected static TypeSyntax objectType;

        static readonly object oGate = new object();
        #endregion  


        #region Properties
        protected XP.IEntityContext CurrentEntity
        {
            get
            {
                if (Entities.Count > 0)
                    return Entities.Peek();
                return null;
            }
        }
        #endregion

        #region Global ClassNames
        public static string GlobalFunctionClassName(XSharpTargetDLL targetDLL)
        {
            string className;
            switch (targetDLL)
            {
                case XSharpTargetDLL.Core:
                    className = XSharpSpecialNames.XSharpCoreFunctionsClass;
                    break;
                case XSharpTargetDLL.Data:
                    className = XSharpSpecialNames.XSharpDataFunctionsClass;
                    break;
                case XSharpTargetDLL.RDD:
                    className = XSharpSpecialNames.XSharpRDDFunctionsClass;
                    break;
                case XSharpTargetDLL.RT:
                    className = XSharpSpecialNames.XSharpRTFunctionsClass;
                    break;
                case XSharpTargetDLL.VO:
                    className = XSharpSpecialNames.XSharpVOFunctionsClass;
                    break;
                case XSharpTargetDLL.XPP:
                    className = XSharpSpecialNames.XSharpXPPFunctionsClass;
                    break;
                case XSharpTargetDLL.VFP:
                    className = XSharpSpecialNames.XSharpVFPFunctionsClass;
                    break;
                default:
                    className = XSharpSpecialNames.FunctionsClass;
                    break;
            }
            return className;

        } 

        public virtual string GetGlobalClassName(XSharpTargetDLL targetDLL)
        {
            return GlobalFunctionClassName(targetDLL);
        }
        #endregion

        #region Construction and destruction
        public XSharpTreeTransformationCore(XSharpParser parser, CSharpParseOptions options, SyntaxListPool pool,
            ContextAwareSyntax syntaxFactory, string fileName)
        {
            _pool = pool;
            _syntaxFactory = syntaxFactory;
            _parser = parser;
            _options = options;
            _isScript = options.Kind == SourceCodeKind.Script;
            GlobalClassName = GetGlobalClassName(_options.TargetDLL);
            GlobalEntities = CreateEntities();
            _ptrType = GenerateQualifiedName(SystemQualifiedNames.IntPtr);
            _intType = _syntaxFactory.PredefinedType(SyntaxFactory.MakeToken(SyntaxKind.IntKeyword));
            _dateTimeType = GenerateQualifiedName("System.DateTime");
            var emptysizes = _pool.AllocateSeparated<ExpressionSyntax>();
            emptysizes.Add(_syntaxFactory.OmittedArraySizeExpression(SyntaxFactory.MakeToken(SyntaxKind.OmittedArraySizeExpressionToken)));
            var emptyrank = _syntaxFactory.ArrayRankSpecifier(
                             SyntaxFactory.MakeToken(SyntaxKind.OpenBracketToken),
                             emptysizes,
                             SyntaxFactory.MakeToken(SyntaxKind.CloseBracketToken));

            _byteArrayType = _syntaxFactory.ArrayType(_syntaxFactory.PredefinedType(SyntaxFactory.MakeToken(SyntaxKind.ByteKeyword)), emptyrank);
            _impliedType = GenerateSimpleName(XSharpSpecialNames.ImpliedTypeName);
            _fileName = fileName;
            _entryPoint = "Start";
            initTypes();
            PragmaOptions = new List<PragmaOption>();
            PragmaWarnings = new List<PragmaWarningDirectiveTriviaSyntax>();
    }


        public static SyntaxTree DefaultXSharpSyntaxTree(IEnumerable<SyntaxTree> trees, bool isApp, XSharpTargetDLL targetDLL)
        {
            // trees is NOT used here, but it IS used in the VOTreeTransForm
            var opt = CSharpParseOptions.Default;
            XSharpSpecificCompilationOptions xopt = new XSharpSpecificCompilationOptions();
            xopt.TargetDLL = targetDLL;
            opt = opt.WithXSharpSpecificOptions(xopt);
            var t = new XSharpTreeTransformationCore(null, opt, new SyntaxListPool(), new ContextAwareSyntax(new SyntaxFactoryContext()), "");

            string globalClassName = t.GetGlobalClassName(targetDLL);

            t.GlobalEntities.Members.Add(t.GenerateGlobalClass(globalClassName, false, true));
            var eof = SyntaxFactory.Token(SyntaxKind.EndOfFileToken);
            var cu = t._syntaxFactory.CompilationUnit(
                    t.GlobalEntities.Externs,
                    t.GlobalEntities.Usings,
                    t.GlobalEntities.Attributes,
                    t.GlobalEntities.Members, eof);
            cu.XGenerated = true;
            var red = (Syntax.CompilationUnitSyntax)cu.CreateRed();
            return CSharpSyntaxTree.Create(red);
        }
        internal T NotInDialect<T>(T node, string feature, string additional = "") where T : CSharpSyntaxNode
        {
            return node.WithAdditionalDiagnostics(
                new SyntaxDiagnosticInfo(ErrorCode.ERR_FeatureNotAvailableInDialect, feature, _options.Dialect.ToString() + " " + additional));
        }

        internal void Free()
        {
            GlobalEntities.Free();
        }

        internal SyntaxEntities CreateEntities()
        {
            return new SyntaxEntities(_pool);
        }

        internal SyntaxClassEntities CreateClassEntities()
        {
            return new SyntaxClassEntities(_pool);
        }
        #endregion

        #region Entitynames

        protected CSharpSyntaxNode CheckTypeName(XP.IEntityContext context, string typeKind, CSharpSyntaxNode node)
        {
            if (context.Parent.Parent is XP.Namespace_Context)
                return node;
            string name = context.Name;
            if (String.Compare(this.GlobalClassName, 0, name + ".", 0, name.Length + 1,XSharpString.Comparison) == 0)
            {
                node = node.WithAdditionalDiagnostics(new SyntaxDiagnosticInfo(ErrorCode.ERR_TypeNameMatchesGlobalNamespace, typeKind, name, GlobalClassName));
            }
            return node;
        }
        protected string GetNestedName(IRuleNode ctx)
        {
            string name = "";
            int iNest = 0;
            while (ctx != null)
            {
                if (ctx is XP.IEntityContext ent)
                {
                    name = ent.Name + "." + name;
                    iNest++;
                }
                else if (ctx is XP.Namespace_Context nc)
                {
                    name = nc.Name.GetText() + "." + name;
                    iNest++;
                }
              ctx = ctx.Parent;
            }
            if (iNest == 1 && !String.IsNullOrEmpty(_options.DefaultNamespace))
            {
                name = _options.DefaultNamespace + "." + name;
            }
            return name;

        }
        protected virtual BlockSyntax AddMissingReturnStatement(BlockSyntax body, XP.StatementBlockContext block, TypeSyntax returnType)
        {
            return body;
        }
        protected string GetEntityName(Boolean Full, Boolean funcNameOnly = false)
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
            if (context is XP.FuncprocContext fc)
            {
                string modName = _options.CommandLineArguments?.CompilationOptions.ModuleName;
                if (modName == null)
                {
                    modName = _options.CommandLineArguments?.SourceFiles.FirstOrDefault().Path;
                    modName = PathUtilities.GetFileName(modName, false);
                }

                if (name.Length == 0)
                    name = GlobalClassName + "." + fc.Id.GetText();
                else
                    name += fc.Id.GetText();
                if (name.Contains("."))
                {
                    name = modName + ":" + name.Substring(name.IndexOf(".") + 1);
                }

                RetType = fc.Type;
                Params = fc.ParamList;
            }
            
            else if (context is XP.ConstructorContext cc)
            {
                if (name.Length > 0) // Remove the dot
                    name = name.Substring(0, name.Length - 1);
                suffix = ".CTOR";
                Params = cc.ParamList;
            }
            else if (context is XP.DestructorContext dc)
            {
                name += "Finalize()";
            }
            else if (context is XP.MethodContext mc)
            {
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
            else if (context is XP.PropertyContext pc)
            {
                if (pc.Id != null)
                    name += pc.Id.GetText();
                if (pc.SELF() != null)
                    name += pc.SELF()?.GetText();
                PParams = pc.ParamList;
                RetType = pc.Type;
                suffix = ":Property";

            }
            else if (context is XP.Event_Context ec)
            {
                name += ec.Id.GetText();
                RetType = ec.Type;
                suffix = ":Event";
            }
            else if (context is XP.VodllContext vdc)
            {
                name += vdc.Id.GetText();
                RetType = vdc.Type;
                Params = vdc.ParamList;
                suffix = ":VoDll";
            }
            else if (context is XP.Delegate_Context del)
            {
                name += del.Id.GetText();
                RetType = del.Type;
                Params = del.ParamList;
                suffix = ":Delegate";
            }
            else if (context is XP.Class_Context cls)
            {
                name += cls.Id.GetText();
                suffix = ":Class";
            }
            else if (context is XP.FoxclassContext fcls)
            {
                name += fcls.Id.GetText();
                suffix = ":Class";
            }
            else if (context is XP.FoxmethodContext fmc)
            {
                name += fmc.Id.GetText();
            }
            else if (context is XP.Structure_Context sc)
            {
                name += sc.Id.GetText();
                suffix = ":Structure";
            }
            else if (context is XP.EventAccessorContext evtc)
            {
                name += evtc.Key.Text;
            }
            else if (context is XP.PropertyAccessorContext pac)
            {
                name += pac.Key.Text;
            }
            if (Full)
            {
                if (RetType != null)
                {
                    name = RetType.GetText() + " " + name;
                }
                else
                {
                    name = "VOID " + name;
                }
                string strParams = "";
                if (Params != null)
                {
                    foreach (XP.ParameterContext _par in Params._Params)
                    {
                        if (strParams?.Length > 0)
                            strParams += ", ";
                        if (_par.Modifiers != null)
                        {
                            string mod = "";
                            foreach (var m in _par.Modifiers._Tokens)
                            {
                                if (m.Type != XP.AS)
                                    mod += m.Text + " ";
                            }
                            strParams += mod;
                        }
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
                if (XSharpString.Compare(suffix, ".ctor") == 0)
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
            {
                if (funcNameOnly)
                {
                    int nPos = name.LastIndexOf('.');
                    name = name.Substring(nPos + 1);
                }
                else
                {
                    name = name.ToUpper() + suffix;
                }
            }
            return name;

        }
        #endregion

        #region Lists
        internal SyntaxList<SyntaxToken> TokenList(params SyntaxKind[] kinds)
        {
            var rb = _pool.Allocate();
            foreach (var k in kinds)
                if (k != SyntaxKind.None)
                {
                    rb.Add(SyntaxFactory.MakeToken(k));
                }
            var r = rb.ToList();
            _pool.Free(rb);
            return r;
        }

        protected SyntaxList<SyntaxToken> TokenListWithDefaultVisibility(Boolean inInterface = false, params SyntaxKind[] kinds)
        {
            var rb = _pool.Allocate();
            foreach (var k in kinds)
            {
                rb.Add(SyntaxFactory.MakeToken(k));
            }
            if (!inInterface)
                rb.FixDefaultVisibility();
            var r = rb.ToList<SyntaxToken>();
            _pool.Free(rb);
            return r;
        }

        protected SyntaxList<SyntaxToken> DefaultMethodModifiers(bool inInterface, bool inStructure, bool noOverride = false)
        {
            var rb = _pool.Allocate();
            if (!inInterface)
            {
                rb.FixDefaultVisibility();
                // structures do not get virtual or override modifiers
                if (!inStructure && !noOverride)
                {
                    if (_options.VirtualInstanceMethods)
                        rb.FixDefaultVirtual();
                    else
                        rb.FixDefaultMethod();
                }
            }
            var r = rb.ToList<SyntaxToken>();
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

        protected SyntaxList<T> MakeList<T>(IEnumerable<IXParseTree> t)
            where T : InternalSyntax.CSharpSyntaxNode
        {
            if (t == null)
                return default(SyntaxList<T>);
            var l = _pool.Allocate<T>();
            foreach (var item in t)
            {
                if (item != null)
                {
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

        protected SyntaxList<T> MakeList<T>(IEnumerable<T> t, params T[] items)
            where T : InternalSyntax.CSharpSyntaxNode
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
            foreach (var item in items)
            {
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
            foreach (var item in t)
            {
                if (item != null)
                {
                    if (l.Count > 0)
                        l.AddSeparator(SyntaxFactory.MakeToken(SyntaxKind.CommaToken));
                    l.Add(((IXParseTree)item).Get<T>());
                }
            }
            var list = l.ToList();
            _pool.Free(l);
            return list;
        }

        protected SeparatedSyntaxList<T> MakeSeparatedList<T>(params T[] items) where T : InternalSyntax.CSharpSyntaxNode
        {
            var l = _pool.AllocateSeparated<T>();
            foreach (var item in items)
            {
                if (item != null)
                {
                    if (l.Count > 0)
                        l.AddSeparator(SyntaxFactory.MakeToken(SyntaxKind.CommaToken));
                    l.Add(item);
                }
            }
            var list = l.ToList();
            _pool.Free(l);
            return list;
        }
        #endregion

        #region Helpers


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


        internal string UniqueNameSuffix(XSharpParserRuleContext context)
        {
            var prefix = _fileName == null ? "XX" : System.IO.Path.GetFileNameWithoutExtension(_fileName).Replace(".","#");
            var token = context != null ? context.Start : new XSharpToken(XP.WS) { Line = 42, Column = 42, StartIndex = 42*42 };
            return "$" + prefix+"_"+token.StartIndex.ToString()+"_"+ token.Line.ToString()+"_"+ token.Column.ToString(); 
        }

        internal T FixPosition<T>(T r, IToken t) where T : XSharpParserRuleContext
        {
            r.Start = r.Stop = t;
            return r;
        }


        protected TypeSyntax VoidType()
        {
            return voidType;
        }


        protected virtual TypeSyntax DefaultType()
        {
            return objectType; ;
        }

        protected TypeSyntax MissingType()
        {
            return objectType
                .WithAdditionalDiagnostics(new SyntaxDiagnosticInfo(ErrorCode.ERR_TypeExpected));
        }

        protected void initTypes()
        {
            lock (oGate)
            {
                if (intType == null)
                {
                    intType = _syntaxFactory.PredefinedType(SyntaxFactory.MakeToken(SyntaxKind.IntKeyword));
                    uintType = _syntaxFactory.PredefinedType(SyntaxFactory.MakeToken(SyntaxKind.UIntKeyword));
                    floatType = _syntaxFactory.PredefinedType(SyntaxFactory.MakeToken(SyntaxKind.FloatKeyword));
                    doubleType = _syntaxFactory.PredefinedType(SyntaxFactory.MakeToken(SyntaxKind.DoubleKeyword));
                    decimalType = _syntaxFactory.PredefinedType(SyntaxFactory.MakeToken(SyntaxKind.DecimalKeyword));
                    stringType = _syntaxFactory.PredefinedType(SyntaxFactory.MakeToken(SyntaxKind.StringKeyword));
                    ulongType = _syntaxFactory.PredefinedType(SyntaxFactory.MakeToken(SyntaxKind.ULongKeyword));
                    longType = _syntaxFactory.PredefinedType(SyntaxFactory.MakeToken(SyntaxKind.LongKeyword));
                    objectType = _syntaxFactory.PredefinedType(SyntaxFactory.MakeToken(SyntaxKind.ObjectKeyword));
                    voidType = _syntaxFactory.PredefinedType(SyntaxFactory.MakeToken(SyntaxKind.VoidKeyword));
                }


            }
        }
        protected virtual TypeSyntax GetExpressionType(XP.ExpressionContext expr, ref bool isConst)
        {

            TypeSyntax type = null;
            var token = expr.GetLiteralToken();
            isConst = false;
            if (token != null)
            {
                // Try to imply the type from the expression
                string text = token.Text;
                switch (token.Type)
                {
                    case XP.INT_CONST:
                    case XP.HEX_CONST:
                    case XP.REAL_CONST:
                        // call SyntaxLiteralValue because it inspects the size of the number
                        SyntaxToken val = token.SyntaxLiteralValue(_options);
                        if (val.Value is int)
                            type = intType;
                        else if (val.Value is uint)
                            type = uintType;
                        else if (val.Value is double)
                            type = doubleType;
                        else if (val.Value is float)
                            type = floatType;
                        else if (val.Value is decimal)
                            type = decimalType;
                        else if (val.Value is ulong)
                            type = ulongType;
                        else if (val.Value is long)
                            type = longType;
                        else
                            type = objectType;
                        break;

                    case XP.INVALID_NUMBER:
                        type = objectType;
                        break;
                    case XP.CHAR_CONST:
                        type = _syntaxFactory.PredefinedType(SyntaxFactory.MakeToken(SyntaxKind.CharKeyword));
                        break;
                    case XP.NULL_PTR:
                        type = _ptrType;
                        break;
                    default:
                        if (XSharpLexer.IsString(token.Type))
                        {
                            type = stringType;
                        }
                        break;

                }
            }
            else if (expr is XP.PrimaryExpressionContext)
            {
                var prim = expr as XP.PrimaryExpressionContext;
                if (prim.Expr is XP.VoCastExpressionContext)
                {
                    var e = prim.Expr as XP.VoCastExpressionContext;
                    if (e.Type != null)
                    {
                        type = e.Type.Get<TypeSyntax>();
                        if (e.Type.Token.Type == XP.PTR
                            && e.Expr is XP.PrimaryExpressionContext pe
                            && pe.Expr is XP.LiteralExpressionContext le
                            && le.Literal.Token.IsZeroLiteral()) // treat PTR(_CAST,0) as NULL_PTR
                        {
                            type = _ptrType;
                        }
                    }
                    else if (e.XType != null)
                    {
                        type = e.XType.Get<TypeSyntax>();
                    }
                    isConst = e.Expr.GetLiteralToken() != null;
                }
                else if (prim.Expr is XP.VoCastPtrExpressionContext)
                {
                    var e = prim.Expr as XP.VoCastPtrExpressionContext;
                    var e2 = prim.Expr.Get<CastExpressionSyntax>();
                    type = e2.Type;
                    isConst = e.Expr.GetLiteralToken() != null;
                }
                else if (prim.Expr is XP.VoConversionExpressionContext)
                {
                    var e = prim.Expr as XP.VoConversionExpressionContext;
                    if (e.Type != null)
                    {
                        type = e.Type.Get<TypeSyntax>();
                    }
                    else if (e.XType != null)
                    {
                        type = e.XType.Get<TypeSyntax>();
                    }
                    isConst = e.Expr.GetLiteralToken() != null;
                }
                else if (prim.Expr is XP.DefaultExpressionContext)
                {
                    var e = prim.Expr as XP.DefaultExpressionContext;
                    type = e.Type.Get<TypeSyntax>();
                }
                else if (prim.Expr is XP.CtorCallContext)
                {
                    var e = prim.Expr as XP.CtorCallContext;
                    type = e.Type.Get<TypeSyntax>();
                }
                else if (prim.Expr is XP.CodeblockExpressionContext)
                {
                    if (_options.XSharpRuntime)
                        type = GenerateQualifiedName(XSharpQualifiedTypeNames.Codeblock);
                    else
                        type = GenerateQualifiedName(VulcanQualifiedTypeNames.Codeblock);
                }
                else if (prim.Expr is XP.LiteralArrayExpressionContext)
                {
                    if (_options.XSharpRuntime)
                        type = GenerateQualifiedName(XSharpQualifiedTypeNames.Array);
                    else
                        type = GenerateQualifiedName(VulcanQualifiedTypeNames.Array);
                }
                else if (prim.Expr is XP.UsualTypeNameExpressionContext)
                {
                    type = intType;
                    isConst = true;
                }
                else if (prim.Expr is XP.TypeExpressionContext)
                {
                    var e = prim.Expr as XP.TypeExpressionContext;
                    type = e.Type.Get<TypeSyntax>();
                }
                else if (prim.Expr is XP.ParenExpressionContext)
                {
                    var e = prim.Expr as XP.ParenExpressionContext;
                    type = GetExpressionType(e.Expr, ref isConst);
                    isConst = e.Expr.GetLiteralToken() != null;
                }
                else if (prim.Expr is XP.IifExpressionContext)
                {
                    var e = prim.Expr as XP.IifExpressionContext;
                    var i = e.Expr;
                    type = GetExpressionType(i.TrueExpr, ref isConst);
                }
            }
            else if (expr is XP.TypeCastContext)
            {
                var e = expr as XP.TypeCastContext;
                type = e.Type.Get<TypeSyntax>();
                isConst = e.Expr.GetLiteralToken() != null;

            }
            else if (expr is XP.TypeCheckExpressionContext)
            {
                var e = expr as XP.TypeCheckExpressionContext;
                if (e.Op.Type == XP.ASTYPE)
                {
                    type = e.Type.Get<TypeSyntax>();
                    isConst = e.Expr.GetLiteralToken() != null;
                }
            }
            else if (expr is XP.BinaryExpressionContext)
            {
                var e = expr as XP.BinaryExpressionContext;
                bool leftIsConst = false;
                bool rightIsConst = false;
                type = GetExpressionType(e.Left, ref leftIsConst);
                var type2 = GetExpressionType(e.Right, ref rightIsConst);
                isConst = leftIsConst && rightIsConst;
                if (type != type2 && type.ToFullString() != type2.ToFullString())
                {
                    if (type == objectType)
                    {
                        type = type2;
                    }
                }
            }
            else if (expr is XP.AssignmentExpressionContext)
            {
                var e = expr as XP.AssignmentExpressionContext;
                bool leftIsConst = false;
                bool rightIsConst = false;
                type = GetExpressionType(e.Left, ref leftIsConst);
                var type2 = GetExpressionType(e.Right, ref rightIsConst);
                isConst = leftIsConst && rightIsConst;
                if (type != type2 && type.ToFullString() != type2.ToFullString())
                {
                    if (type == objectType)
                    {
                        type = type2;
                    }
                }
            }
            else if (expr is XP.PrefixExpressionContext)
            {
                var e = expr as XP.PrefixExpressionContext;
                type = GetExpressionType(e.Expr, ref isConst);
                if (e.Op.Type == XP.ADDROF)
                    type = _syntaxFactory.PointerType(voidType, SyntaxFactory.MakeToken(SyntaxKind.AmpersandToken));
                else
                    type = GetExpressionType(e.Expr, ref isConst);
            }
            if (type == null)
            {
                type = objectType;
            }
            if (type is PredefinedTypeSyntax)
            {
                var pdt = type as PredefinedTypeSyntax;
                switch (pdt.Keyword.Kind)
                {
                    case SyntaxKind.IntKeyword:
                    case SyntaxKind.UIntKeyword:
                    case SyntaxKind.BoolKeyword:
                    case SyntaxKind.FloatKeyword:
                    case SyntaxKind.DecimalKeyword:
                    case SyntaxKind.DoubleKeyword:
                    case SyntaxKind.StringKeyword:
                    case SyntaxKind.CharKeyword:
                    case SyntaxKind.ByteKeyword:
                    case SyntaxKind.ShortKeyword:
                    case SyntaxKind.UShortKeyword:
                        isConst = true;
                        break;
                    default:
                        isConst = false;
                        break;

                }
            }
            else if (type == _ptrType)
            {
                isConst = true;
            }
            else
            {
                isConst = false;
            }
            return type;
        }

        #endregion

        #region Code Generation Helpers


        protected ArrayRankSpecifierSyntax MakeArrayRankSpecifier(int ranks)
        {
            var sizes = _pool.AllocateSeparated<ExpressionSyntax>();
            for (int i = 0; i < ranks; i++)
            {
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

        protected ExpressionSyntax MakeChecked(ExpressionSyntax expr, bool @checked)
        {
            if (@checked)
            {
                return _syntaxFactory.CheckedExpression(SyntaxKind.CheckedExpression,
                    SyntaxFactory.MakeToken(SyntaxKind.CheckedKeyword),
                    SyntaxFactory.MakeToken(SyntaxKind.OpenParenToken),
                    expr,
                    SyntaxFactory.MakeToken(SyntaxKind.CloseParenToken));
            }
            else
            {
                return _syntaxFactory.CheckedExpression(SyntaxKind.UncheckedExpression,
                    SyntaxFactory.MakeToken(SyntaxKind.UncheckedKeyword),
                    SyntaxFactory.MakeToken(SyntaxKind.OpenParenToken),
                    expr,
                    SyntaxFactory.MakeToken(SyntaxKind.CloseParenToken));
            }
        }
        protected LockStatementSyntax MakeLock(ExpressionSyntax expr, StatementSyntax statement, IToken token = null)
        {
            var kwd = token == null ? SyntaxFactory.MakeToken(SyntaxKind.LockKeyword) : token.SyntaxKeyword();
            return _syntaxFactory.LockStatement(kwd,
                                               SyntaxFactory.MakeToken(SyntaxKind.OpenParenToken),
                                                expr,
                                                SyntaxFactory.MakeToken(SyntaxKind.CloseParenToken),
                                                statement);


        }
        protected AssignmentExpressionSyntax MakeSimpleAssignment(ExpressionSyntax lhs, ExpressionSyntax rhs)
        {
            return _syntaxFactory.AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,
                     lhs, SyntaxFactory.MakeToken(SyntaxKind.EqualsToken), rhs);
        }

        protected MemberAccessExpressionSyntax MakeSimpleMemberAccess(ExpressionSyntax lhs, SimpleNameSyntax rhs)
        {
            return _syntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, lhs,
                                                        SyntaxFactory.MakeToken(SyntaxKind.DotToken), rhs);
        }
        protected TypeOfExpressionSyntax MakeTypeOf(TypeSyntax type)
        {
            return _syntaxFactory.TypeOfExpression(
                    SyntaxFactory.MakeToken(SyntaxKind.TypeOfKeyword),
                    SyntaxFactory.MakeToken(SyntaxKind.OpenParenToken),
                    type,
                    SyntaxFactory.MakeToken(SyntaxKind.CloseParenToken));
        }


        protected IfStatementSyntax GenerateIfStatement(ExpressionSyntax condition, StatementSyntax statement, ElseClauseSyntax @else = null)
        {
            return _syntaxFactory.IfStatement(
                        SyntaxFactory.MakeToken(SyntaxKind.IfKeyword),
                        SyntaxFactory.MakeToken(SyntaxKind.OpenParenToken),
                        condition,
                        SyntaxFactory.MakeToken(SyntaxKind.CloseParenToken),
                        statement, @else);
        }

        protected virtual ExpressionSyntax GenerateMissingExpression()
        {
            return GenerateMissingExpression(true);
        }

        protected virtual ExpressionSyntax GenerateMissingExpression(bool withError)
        {
            var result = GenerateLiteralNull();
            if (withError)
            {
                result = result.WithAdditionalDiagnostics(new SyntaxDiagnosticInfo(ErrorCode.ERR_MissingArgument));
            }
            return result;
        }

        protected LiteralExpressionSyntax GenerateLiteral(IToken token,XSharpParserRuleContext context = null)
        {
            if (token != null)
                return SyntaxFactory.LiteralExpression(token.ExpressionKindLiteral(), token.SyntaxLiteralValue(_options, context, PragmaOptions));
            return null;
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
            if (text.StartsWith("@@"))
                text = text.Substring(2);
            return _syntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression,
                                                SyntaxFactory.Literal(null, text, text, null));
        }
        protected LiteralExpressionSyntax GenerateLiteral(int value)
        {
            return _syntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression,
                                                SyntaxFactory.Literal(null, value.ToString(), value, null));
        }
        protected LiteralExpressionSyntax GenerateLiteral(long value)
        {
            return _syntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression,
                                                SyntaxFactory.Literal(null, value.ToString(), value, null));
        }
        protected LiteralExpressionSyntax GenerateLiteral(double value)
        {
            return _syntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression,
                                                SyntaxFactory.Literal(null, value.ToString(), value, null));
        }
        protected LiteralExpressionSyntax GenerateLiteral(string source, int value)
        {
            return _syntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression,
                                                SyntaxFactory.Literal(null, source, value, null));
        }
        protected LiteralExpressionSyntax GenerateLiteralNull()
        {
            return _syntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression, SyntaxFactory.MakeToken(SyntaxKind.NullKeyword));
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
        protected IdentifierNameSyntax GenerateIdentifier(XSharpParserRuleContext context)
        {
            return _syntaxFactory.IdentifierName(context.Get<SyntaxToken>());
        }
        protected VariableDeclaratorSyntax GenerateBuffer(SyntaxToken nameToken, BracketedArgumentListSyntax dims)
        {
            return _syntaxFactory.VariableDeclarator(nameToken, dims, null);
        }

        protected LocalDeclarationStatementSyntax GenerateLocalDecl(string name, TypeSyntax type, ExpressionSyntax initexpr = null)
        {
            var decl = _syntaxFactory.VariableDeclaration(type,
                        MakeSeparatedList<VariableDeclaratorSyntax>(GenerateVariable(name, initexpr)));
            decl.XGenerated = true;
            var result = _syntaxFactory.LocalDeclarationStatement(TokenList(), decl,SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken));
            result.XGenerated = true;
            return result;
        }

        protected ReturnStatementSyntax GenerateReturn(ExpressionSyntax result = null, bool markAsGenerated = false, IToken returnToken = null)
        {
            var kwd = returnToken == null ? SyntaxFactory.MakeToken(SyntaxKind.ReturnKeyword) : returnToken.SyntaxKeyword();
            var stmt = _syntaxFactory.ReturnStatement(kwd, result, SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken));
            stmt.XGenerated = markAsGenerated;
            return stmt;
        }
        protected IdentifierNameSyntax GenerateSimpleName(string name)
        {
            return _syntaxFactory.IdentifierName(SyntaxFactory.MakeIdentifier(name));
        }
        protected ExpressionSyntax GenerateSelf()
        {
            return _syntaxFactory.ThisExpression(SyntaxFactory.MakeToken(SyntaxKind.ThisKeyword));
        }
        protected ExpressionSyntax GenerateSuper()
        {
            return _syntaxFactory.BaseExpression(SyntaxFactory.MakeToken(SyntaxKind.BaseKeyword));
        }

        protected NameEqualsSyntax GenerateNameEquals(string name)
        {
            return _syntaxFactory.NameEquals(
                  GenerateSimpleName(name), SyntaxFactory.MakeToken(SyntaxKind.EqualsToken));
        }

        protected internal NameSyntax GenerateQualifiedName(string name)
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
            NameSyntax r = GenerateSimpleName(idName);
            if (alias != null)
            {
                if (XSharpString.Compare(alias, "global") == 0)
                    r = _syntaxFactory.AliasQualifiedName(
                        _syntaxFactory.IdentifierName(SyntaxFactory.MakeToken(SyntaxKind.GlobalKeyword, alias)),
                        SyntaxFactory.MakeTokenNoWs(SyntaxKind.ColonColonToken),
                        (SimpleNameSyntax)r);
                else
                    r = _syntaxFactory.AliasQualifiedName(
                        GenerateSimpleName(alias),
                        SyntaxFactory.MakeTokenNoWs(SyntaxKind.ColonColonToken),
                        (SimpleNameSyntax)r);
            }
            for (int i = 1; i < ids.Length; i++)
            {
                r = _syntaxFactory.QualifiedName(
                    r,
                    SyntaxFactory.MakeTokenNoWs(SyntaxKind.DotToken),
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
                if (XSharpString.Compare(alias, "global") == 0)
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
                SyntaxFactory.MakeTokenNoWs(SyntaxKind.ColonColonToken),
                (SimpleNameSyntax)r);
            foreach (var dotName in dotNames)
            {
                r = _syntaxFactory.QualifiedName(
                    r,
                    SyntaxFactory.MakeTokenNoWs(SyntaxKind.DotToken),
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
            attributeLists.Add(MakeAttributeList(null, attributes));
            _pool.Free(attributes);
        }

        protected ExpressionSyntax CreateObject(TypeSyntax type, ArgumentListSyntax args, InitializerExpressionSyntax init = null)
        {
            ExpressionSyntax expr;
            expr = _syntaxFactory.ObjectCreationExpression(
                SyntaxFactory.MakeToken(SyntaxKind.NewKeyword),
                type, args, init);
            return expr;
        }

        protected ExpressionSyntax GenerateMethodCall(string MethodName, bool markAsGenerated = false)
        {
            return GenerateMethodCall(MethodName, EmptyArgumentList(), markAsGenerated);
        }

        protected ExpressionSyntax GenerateMethodCall(string MethodName, ArgumentListSyntax args, bool markAsGenerated = false)
        {
            ExpressionSyntax expr = _syntaxFactory.InvocationExpression(GenerateQualifiedName(MethodName), args);
            expr.XGenerated = markAsGenerated;
            return expr;
        }

        protected virtual void ImplementClipperAndPSZ(XP.IEntityWithBodyContext context,
            ref SyntaxList<AttributeListSyntax> attributes, ref ParameterListSyntax parameters, ref BlockSyntax body,
            ref TypeSyntax dataType)
        {
            return;
        }

        protected ExpressionStatementSyntax GenerateExpressionStatement(ExpressionSyntax expr, bool markAsGenerated = false)
        {
            expr.XGenerated = markAsGenerated;
            var stmt = _syntaxFactory.ExpressionStatement(expr, SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken));
            stmt.XGenerated = markAsGenerated;
            return stmt;
        }

        protected ArgumentSyntax MakeArgument(ExpressionSyntax expr, bool byref = false)
        {
            SyntaxToken byrefToken = null;
            if (byref)
                byrefToken = SyntaxFactory.MakeToken(SyntaxKind.RefKeyword);
            return _syntaxFactory.Argument(null, byrefToken, expr);
        }
        protected BlockSyntax MakeBlock(IList<StatementSyntax> statements)
        {
            var stmts = _pool.Allocate<StatementSyntax>();
            foreach (var stmt in statements)
                stmts.Add(stmt);
            var result = MakeBlock(stmts);
            _pool.Free(stmts);
            return result;
        }
        protected BlockSyntax MakeBlock(SyntaxList<StatementSyntax> statements)
        {
            var block = _syntaxFactory.Block(
                        SyntaxFactory.MakeToken(SyntaxKind.OpenBraceToken),
                        statements,
                        SyntaxFactory.MakeToken(SyntaxKind.CloseBraceToken));
            block.XGenerated = true;
            return block;
        }

        protected ExpressionSyntax MakeDefault(TypeSyntax type)
        {
            if (type is PredefinedTypeSyntax && ((PredefinedTypeSyntax)type).Keyword.Kind == SyntaxKind.StringKeyword)
            {
                return GenerateLiteral("");
            }
            else
            {
                return _syntaxFactory.DefaultExpression(
                    SyntaxFactory.MakeToken(SyntaxKind.DefaultKeyword),
                    SyntaxFactory.MakeToken(SyntaxKind.OpenParenToken),
                    type,
                    SyntaxFactory.MakeToken(SyntaxKind.CloseParenToken));
            }
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


        protected internal void AddUsingWhenMissing(SyntaxListBuilder<UsingDirectiveSyntax> usings, NameSyntax usingName, bool bStatic, NameEqualsSyntax alias)
        {
            bool found = false;

            for (int i = 0; i < usings.Count; i++)
            {
                UsingDirectiveSyntax u = usings[i];
                if (alias != null)
                {
                    // match alias AND name
                    if (u.Alias != null && XSharpString.Compare(u.Alias.Name.ToString(), alias.Name.ToString()) == 0
                        && XSharpString.Compare(u.Name.ToString(), usingName.ToString()) == 0)
                    {
                        found = true;
                        break;
                    }

                }
                else if (XSharpString.Compare(u.Name.ToString(), usingName.ToString()) == 0)
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
                    alias,
                    usingName,
                    SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken)));
            }

        }
        protected void AddUsingWhenMissing(SyntaxListBuilder<UsingDirectiveSyntax> usings, string name, bool bStatic, NameEqualsSyntax alias)
        {
            NameSyntax usingName = GenerateQualifiedName(name);
            AddUsingWhenMissing(usings, usingName, bStatic, alias);
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

        private void splitClassNameAndNamespace(ref string className, out string nameSpace)
        {
            nameSpace = "";
            if (className.Contains("."))
            {
                nameSpace = className.Substring(0, className.LastIndexOf("."));
                className = className.Substring(className.LastIndexOf(".") + 1);
            }
        }

        protected MemberDeclarationSyntax GenerateGlobalClass(string className, bool bInternalClass, bool withAttribs, SyntaxList<MemberDeclarationSyntax> members)
        {
            string nameSpace;
            splitClassNameAndNamespace(ref className, out nameSpace);
            SyntaxListBuilder modifiers = _pool.Allocate();
            modifiers.Add(SyntaxFactory.MakeToken(SyntaxKind.PartialKeyword));
            if (bInternalClass)
                modifiers.Add(SyntaxFactory.MakeToken(SyntaxKind.InternalKeyword));
            else
                modifiers.Add(SyntaxFactory.MakeToken(SyntaxKind.PublicKeyword));
            modifiers.Add(SyntaxFactory.MakeToken(SyntaxKind.StaticKeyword));
            MemberDeclarationSyntax r =
                _syntaxFactory.ClassDeclaration(
                attributeLists: withAttribs ? MakeCompilerGeneratedAttribute(true) : EmptyList<AttributeListSyntax>(),
                modifiers: modifiers.ToList<SyntaxToken>(),
                keyword: SyntaxFactory.MakeToken(SyntaxKind.ClassKeyword),
                identifier: SyntaxFactory.Identifier(className),
                typeParameterList: null,
                baseList: null, // BaseListSyntax baseList = _syntaxFactory.BaseList(colon, list)
                constraintClauses: default(SyntaxListBuilder<TypeParameterConstraintClauseSyntax>),
                openBraceToken: SyntaxFactory.MakeToken(SyntaxKind.OpenBraceToken),
                members: members,
                closeBraceToken: SyntaxFactory.MakeToken(SyntaxKind.CloseBraceToken),
                semicolonToken: SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken)) /*))*/;
            _pool.Free(modifiers);
            r.XGenerated = true;
            if (nameSpace.Length > 0)
            {
                r = GenerateNamespace(nameSpace, MakeList<MemberDeclarationSyntax>(r));
                r.XGenerated = true;
            }
            return r;
        }

        protected MemberDeclarationSyntax GenerateGlobalClass(string className, bool internalClass, bool withAttribs, params MemberDeclarationSyntax[] members)
        {
            SyntaxListBuilder<MemberDeclarationSyntax> globalClassMembers = _pool.Allocate<MemberDeclarationSyntax>();
            string nameSpace;
            splitClassNameAndNamespace(ref className, out nameSpace);
            if (members.Length > 0)
            {
                foreach (var m in members)
                    globalClassMembers.Add(m);
            }
            else
            {
                // When no members defined then we create an empty static constructor 
                var statements = _pool.Allocate<StatementSyntax>();
                statements.Add(_syntaxFactory.EmptyStatement(SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken)));
                var block = MakeBlock(statements);
                _pool.Free(statements);
                globalClassMembers.Add(
                        _syntaxFactory.ConstructorDeclaration(
                        attributeLists: MakeCompilerGeneratedAttribute(),
                        modifiers: TokenList(SyntaxKind.StaticKeyword),
                        identifier: SyntaxFactory.Identifier(className),
                        parameterList: EmptyParameterList(),
                        initializer: null,
                        body: block,
                        expressionBody: null,
                        semicolonToken: null)
                    );
            }
            SyntaxListBuilder modifiers = _pool.Allocate();
            modifiers.Add(SyntaxFactory.MakeToken(SyntaxKind.PartialKeyword));
            if (internalClass)
                modifiers.Add(SyntaxFactory.MakeToken(SyntaxKind.InternalKeyword));
            else
                modifiers.Add(SyntaxFactory.MakeToken(SyntaxKind.PublicKeyword));
            modifiers.Add(SyntaxFactory.MakeToken(SyntaxKind.StaticKeyword));
            MemberDeclarationSyntax classdecl =
                _syntaxFactory.ClassDeclaration(
                attributeLists: withAttribs ? MakeCompilerGeneratedAttribute(true) : EmptyList<AttributeListSyntax>(),
                modifiers: modifiers.ToList<SyntaxToken>(),
                keyword: SyntaxFactory.MakeToken(SyntaxKind.ClassKeyword),
                identifier: SyntaxFactory.Identifier(className),
                typeParameterList: null,
                baseList: null, // BaseListSyntax baseList = _syntaxFactory.BaseList(colon, list)
                constraintClauses: default(SyntaxListBuilder<TypeParameterConstraintClauseSyntax>),
                openBraceToken: SyntaxFactory.MakeToken(SyntaxKind.OpenBraceToken),
                members: globalClassMembers,
                closeBraceToken: SyntaxFactory.MakeToken(SyntaxKind.CloseBraceToken),
                semicolonToken: SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken)) /*))*/;
            _pool.Free(modifiers);
            _pool.Free(globalClassMembers);
            classdecl.XGenerated = true;
            if (nameSpace.Length > 0)
            {
                classdecl = GenerateNamespace(nameSpace, MakeList<MemberDeclarationSyntax>(classdecl));
                classdecl.XGenerated = true;
            }
            return classdecl;
        }

        bool IsTypeEqual(TypeSyntax t1, TypeSyntax t2)
        {
            var id1 = t1.ToFullString().Replace(" ", "");
            var id2 = t2.ToFullString().Replace(" ", "");
            if (id1.IndexOf(".") > 0)
                id1 = id1.Substring(id1.LastIndexOf(".") + 1);
            if (id2.IndexOf(".") > 0)
                id2 = id2.Substring(id2.LastIndexOf(".") + 1);

            return XSharpString.Compare(id1, id2) == 0;
        }

        internal MemberDeclarationSyntax GeneratePartialProperyMethod(XP.IMethodContext context, bool Access, bool Static)
        {
            string suffix;
            TypeSyntax returntype;
            if (Access)
            {
                suffix = XSharpSpecialNames.AccessSuffix;
                returntype = getReturnType(context);
            }
            else
            {
                suffix = XSharpSpecialNames.AssignSuffix;
                returntype = voidType;
            }
            var nobody = context.ExpressionBody != null;
            var name = SyntaxFactory.Identifier(context.Id.GetText() + suffix);
            var parameters = getParameters(context.ParamList);
            SyntaxList<SyntaxToken> mods;
            if (Static)
                mods = TokenList(SyntaxKind.PrivateKeyword, SyntaxKind.StaticKeyword);
            else
                mods = TokenList(SyntaxKind.PrivateKeyword);


            var body = nobody ? null : context.Statements.Get<BlockSyntax>();
            var expressionBody = GetExpressionBody(context.ExpressionBody);

            MemberDeclarationSyntax m = _syntaxFactory.MethodDeclaration(
                 attributeLists: null,
                 modifiers: mods,
                 returnType: returntype,
                 explicitInterfaceSpecifier: null,
                 identifier: name,
                 typeParameterList: getTypeParameters(context.TypeParameters),
                 parameterList: parameters,
                 constraintClauses: getTypeConstraints(context._ConstraintsClauses),
                 body: body,
                 expressionBody: expressionBody, 
                 semicolonToken: SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken)
                 );
            var rule = context as XSharpParserRuleContext;
            rule.Put(m);
            return m;
        }


        internal MemberDeclarationSyntax GenerateVoProperty(SyntaxClassEntities.VoPropertyInfo vop, XSharpParserRuleContext context)
        {
            var getMods = _pool.Allocate();
            var setMods = _pool.Allocate();
            bool mergePartialDeclarations = (context == null);
            var outerMods = _pool.Allocate();
            int getVisLvl;
            int setVisLvl;
            var AccMet = vop.DupAccess ?? vop.AccessMethodCtx;
            var AssMet = vop.DupAssign ?? vop.AssignMethodCtx;

            if ((AccMet == null || AssMet == null) && !mergePartialDeclarations)
            {
                // When one of the two is missing and we are in a partial class
                // then we generate a normal method. Later from the LanguageParser
                // we will generate a property and in its body we will call the generated method
                var ent = context as XP.IEntityContext;
                if (ent != null && ent.Data.Partial)
                {
                    MemberDeclarationSyntax result;
                    ent.Data.PartialProps = true;
                    if (ent is XP.IPartialPropertyContext)
                    {
                        var cls = ent as XP.IPartialPropertyContext;
                        if (cls.PartialProperties == null)
                            cls.PartialProperties = new List<XP.IMethodContext>();

                        var met = AccMet ?? AssMet;
                        cls.PartialProperties.Add(met);
                        var rule = met as XSharpParserRuleContext;
                        if (rule.Parent is XP.ClsmethodContext cmc)
                        {
                            if (rule.CsNode == null)
                                rule.CsNode = cmc.CsNode;
                            cmc.CsNode = null;
                        }
                        else if (rule.Parent is XP.FoxclsmethodContext fmc)
                        {
                            if (rule.CsNode == null)
                                rule.CsNode = fmc.CsNode;
                            fmc.CsNode = null;
                        }

                    }
                    if (AccMet != null)
                    {
                        bool bStatic = false;
                        if (AccMet.Mods?._Tokens != null)
                        {
                            bStatic = AccMet.Mods._Tokens.Any(t => t.Type == XSharpLexer.STATIC);
                        }
                        result = GeneratePartialProperyMethod(AccMet, true, bStatic);
                    }
                    else
                    {
                        bool bStatic = false;
                        if (AssMet.Mods?._Tokens != null)
                        {
                            bStatic = AssMet.Mods._Tokens.Any(t => t.Type == XSharpLexer.STATIC);
                        }
                        result = GeneratePartialProperyMethod(AssMet, false, bStatic);
                    }
                    GlobalEntities.NeedsProcessing = true;
                    return result;
                }
            }


            #region modifiers and visibility
            if (AccMet != null)
            {
                if (AccMet.Mods != null)
                {
                    getMods.AddRange(AccMet.Mods.GetList<SyntaxToken>());
                }
                else if (!  AccMet.IsInInterface)
                {
                    getMods.FixDefaultVisibility();
                    if (_options.VirtualInstanceMethods && !AccMet.IsInStructure)
                    {
                        getMods.FixDefaultVirtual();
                    }
                    else
                    {
                        getMods.FixDefaultMethod();
                    }
                }
                getVisLvl = getMods.GetVisibilityLevel();
            }
            else
                getVisLvl = 15;
            if (AssMet != null)
            {
                if (AssMet.Mods != null)
                {
                    setMods.AddRange(AssMet.Mods.GetList<SyntaxToken>());
                }
                else if (!((XSharpParserRuleContext)AssMet).isInInterface())
                {
                    setMods.FixDefaultVisibility();
                    if (_options.VirtualInstanceMethods && !((XSharpParserRuleContext)AssMet).isInStructure())
                    {
                        setMods.FixDefaultVirtual();
                    }
                    else
                    {
                        setMods.FixDefaultMethod();
                    }
                }
                setVisLvl = setMods.GetVisibilityLevel();
            }
            else
            {
                setVisLvl = 15;
            }
            if (getVisLvl <= setVisLvl)
            {
                outerMods.AddRange(getMods.ToList());
                getMods.Clear();
            }
            else
            {
                outerMods.AddRange(setMods.ToList());
                setMods.Clear();
            }
            var rawMods = getVisLvl <= setVisLvl ? setMods : getMods;
            var innerMods = _pool.Allocate();
            for (int i = 0; i < rawMods.Count; i++)
            {
                var t = rawMods[i];
                if (!outerMods.Any(t.RawKind))
                {
                    if (!SyntaxFacts.IsAccessibilityModifier((SyntaxKind)t.RawKind))
                    {
                        t = ((CSharpSyntaxNode)t).WithAdditionalDiagnostics(new SyntaxDiagnosticInfo(ErrorCode.ERR_BadMemberFlag, t));
                    }
                    innerMods.Add(t);
                }
            }
            _pool.Free(rawMods);
            if (getVisLvl <= setVisLvl)
            {
                setMods = innerMods;
            }
            else
            {
                getMods = innerMods;
            }
            #endregion
            #region Property Type
            TypeSyntax voPropType;
            var typeMatch = true;
            if (AssMet != null && AssMet.ParamList != null && AssMet.ParamList._Params?.Count > 0)
            {
                voPropType = getDataType(AssMet.ParamList._Params[0].Type);
                if (AccMet != null)
                {
                    var accType = getReturnType(AccMet);
                    typeMatch = IsTypeEqual(voPropType, accType);
                }
            }
            else if (AccMet != null)
            {
                voPropType = getReturnType(AccMet);
            }
            else
            {
                voPropType = _getMissingType();
            }
            voPropType.XVoDecl = true;
            #endregion
            #region Parameters
            BracketedParameterListSyntax voPropParams;
            ArgumentSyntax[] voPropArgs;
            int accParamCount = 0;
            int assParamCount = 0;
            if (AssMet != null && AssMet?.ParamList != null)
            {
                assParamCount = AssMet.ParamList._Params.Count;
            }
            if (AccMet != null && AccMet.ParamList != null)
            {
                accParamCount = AccMet.ParamList._Params.Count;
            }
            if (assParamCount > 1)
            {
                var @params = ArrayBuilder<XP.ParameterContext>.GetInstance();
                foreach (var p in AssMet.ParamList._Params.Skip(1))
                {
                    @params.Add(p);
                }
                voPropParams = _syntaxFactory.BracketedParameterList(
                    SyntaxFactory.MakeToken(SyntaxKind.OpenBracketToken),
                    MakeSeparatedList<ParameterSyntax>(@params),
                    SyntaxFactory.MakeToken(SyntaxKind.CloseBracketToken));
                voPropArgs = @params.Select(pCtx => _syntaxFactory.Argument(null, null, GenerateSimpleName(pCtx.Id.Start.Text))).ToArray();
            }
            else if (accParamCount > 0)
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
            #endregion
            var accessors = _pool.Allocate<AccessorDeclarationSyntax>();
            IXParseTree xnode = null;
            bool paramMatch = true;
            #region ACCESS = Get Accessor
            if (AccMet != null)
            {
                
                // Create the GET accessor.
                bool isInInterfaceOrAbstract = AccMet.IsInInterface ||
                    outerMods.Any((int)SyntaxKind.AbstractKeyword) ||
                    outerMods.Any((int)SyntaxKind.ExternKeyword);
                var m = AccMet.Get<MethodDeclarationSyntax>();
                var args = MakeArgumentList(voPropArgs);
                var nobody = isInInterfaceOrAbstract || AccMet.ExpressionBody != null;
                BlockSyntax block = nobody ? null : m.Body;
                var expressionBody = GetExpressionBody(AccMet.ExpressionBody);
                if (AssMet != null)
                {
                    // When both Access and Assign exist, then check the parameter names and types
                    if (accParamCount != assParamCount - 1)
                    {
                        paramMatch = false;
                    }
                    else if (assParamCount > 1)
                    {
                        // Match the parameter names
                        // The correct order of the parameters in an assign is:
                        // value, [index1 [, index2 [, index3]]]
                        // and the matching Access parameters must be (of course)
                        // [index1 [, index2 [, index3]]]
                        for (int iParam = 0; iParam < assParamCount - 1 && paramMatch; iParam++)
                        {
                            var name1 = AccMet.ParamList._Params[iParam].Id.GetText();
                            var name2 = AssMet.ParamList._Params[iParam + 1].Id.GetText();
                            var type1 = getDataType(AccMet.ParamList._Params[iParam].Type);
                            var type2 = getDataType(AssMet.ParamList._Params[iParam + 1].Type);
                            if (XSharpString.Compare(name1, name2) != 0
                                || !IsTypeEqual(type1, type2))
                            {
                                paramMatch = false;
                            }

                        }
                    }

                }
                if (mergePartialDeclarations)
                {
                    // change the body to call the generated access
                    ExpressionSyntax methodCall;
                    var name = AccMet.Id.GetText() + XSharpSpecialNames.AccessSuffix;
                    if (accParamCount == 0)
                    {
                        methodCall = GenerateMethodCall(name);
                    }
                    else
                    {
                        var a = new List<ArgumentSyntax>();
                        foreach (var p in AccMet.ParamList._Params)
                        {
                            a.Add(MakeArgument(GenerateSimpleName(p.Id.GetText())));
                        }
                        methodCall = GenerateMethodCall(name, MakeArgumentList(a.ToArray()));
                    }
                    block = MakeBlock(GenerateReturn(methodCall,true));
                    block.XGenerated = true;
                }

                var accessor = _syntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration,
                        EmptyList<AttributeListSyntax>(), getMods.ToList<SyntaxToken>(),
                        SyntaxFactory.MakeToken(SyntaxKind.GetKeyword),
                        nobody ? null : block,
                        expressionBody,
                        isInInterfaceOrAbstract ? null : m.SemicolonToken);
                accessor.XGenerated = mergePartialDeclarations;
                if (vop.DupAccess != null)
                {
                    accessor = accessor.WithAdditionalDiagnostics(new SyntaxDiagnosticInfo(ErrorCode.ERR_DuplicateAccessor));
                }
                if (!paramMatch)
                {
                    var diag = new SyntaxDiagnosticInfo(ErrorCode.ERR_AccessAssignParametersMutchMatch);
                    accessor = accessor.WithAdditionalDiagnostics(diag);
                }
                if (!typeMatch)
                {
                    var diag = new SyntaxDiagnosticInfo(ErrorCode.ERR_AccessAssignTypesMutchMatch);
                    accessor = accessor.WithAdditionalDiagnostics(diag);
                }
                var node = AccMet.CsNode as CSharpSyntaxNode;
                if (node != null && node.ContainsDiagnostics)
                {
                    var diag = node.GetDiagnostics();
                    accessor = accessor.WithAdditionalDiagnostics(diag);
                }
                accessors.Add(accessor);
                AccMet.Put(accessor);
                if (AccMet.Parent is XP.ClsmethodContext cmc)
                {
                    cmc.CsNode = null;
                }
                else if (AccMet.Parent is XP.FoxmethodContext fmc)
                {
                    fmc.CsNode = null;
                }
                xnode = AccMet;
            }
            #endregion
            #region ASSIGN = Set Accessor
            bool missingParam = false;
            if (AssMet != null)
            {
                bool isInInterfaceOrAbstract = AssMet.IsInInterface ||
                    outerMods.Any((int)SyntaxKind.AbstractKeyword) ||
                    outerMods.Any((int)SyntaxKind.ExternKeyword);
                var m = AssMet.Get<MethodDeclarationSyntax>();
                var nobody = isInInterfaceOrAbstract || AssMet.ExpressionBody != null;
                var expressionBody = GetExpressionBody(AssMet.ExpressionBody);
                BlockSyntax block = nobody ? null : m.Body;
                if (!isInInterfaceOrAbstract)
                {
                    if (mergePartialDeclarations)
                    {
                        var a = new List<ArgumentSyntax>() { MakeArgument(GenerateSimpleName("value")) };
                        if (assParamCount > 1)
                        {
                            for (int i = 1; i < assParamCount; i++)
                            {
                                var paramName = AssMet.ParamList._Params[i].Id.GetText();
                                a.Add(MakeArgument(GenerateSimpleName(paramName)));
                            }
                        }
                        string mName = AssMet.Id.GetText() + XSharpSpecialNames.AssignSuffix;
                        var stmt = GenerateExpressionStatement(GenerateMethodCall(mName, MakeArgumentList(a.ToArray())));
                        block = MakeBlock(stmt);
                        block.XGenerated = true;
                    }
                    else
                    {
                        var paramName = "value";
                        if (assParamCount > 0)
                        {
                            paramName = AssMet.ParamList._Params[0].Id.GetText();

                        }
                        else
                        {
                            missingParam = true;
                        }
                        // when the name of the original parameter is value, then there is no need to change things
                        if (XSharpString.Compare(paramName, "value") == 0)
                        {
                            block = m.Body;
                        }
                        else
                        {
                            // else create a local with the original parameter name and an assignment from the new value parameter
                            var stmts = new List<StatementSyntax>();
                            var locdecl = GenerateLocalDecl(paramName, _impliedType, GenerateSimpleName("value"));
                            stmts.Add(locdecl);
                            foreach (var stmt in m.Body.Statements)
                            {
                                stmts.Add(stmt);
                            }
                            block = MakeBlock(stmts);
                        }
                    }
                }
                var accessor = _syntaxFactory.AccessorDeclaration(SyntaxKind.SetAccessorDeclaration,
                        EmptyList<AttributeListSyntax>(), setMods.ToList<SyntaxToken>(),
                        SyntaxFactory.MakeToken(SyntaxKind.SetKeyword),
                        nobody ? null : block,
                        expressionBody,
                        isInInterfaceOrAbstract ? null : m.SemicolonToken);
                accessor.XGenerated = mergePartialDeclarations;
                if (vop.DupAssign != null)
                {
                    accessor = accessor.WithAdditionalDiagnostics(new SyntaxDiagnosticInfo(ErrorCode.ERR_DuplicateAccessor));
                }
                if (missingParam)
                {
                    accessor = accessor.WithAdditionalDiagnostics(new SyntaxDiagnosticInfo(ErrorCode.ERR_AssignMethodsMustHaveAParameter));
                }
                if (!paramMatch)
                {
                    var diag = new SyntaxDiagnosticInfo(ErrorCode.ERR_AccessAssignParametersMutchMatch);
                    accessor = accessor.WithAdditionalDiagnostics(diag);
                }
                if (!typeMatch)
                {
                    var diag = new SyntaxDiagnosticInfo(ErrorCode.ERR_AccessAssignTypesMutchMatch);
                    accessor = accessor.WithAdditionalDiagnostics(diag);
                }
                if (AssMet.CsNode is CSharpSyntaxNode node && node.ContainsDiagnostics)
                {
                    var diag = node.GetDiagnostics();
                    accessor = accessor.WithAdditionalDiagnostics(diag);
                }
                accessors.Add(accessor);
                AssMet.Put(accessor);
                if (AssMet.Parent is XP.ClsmethodContext cmc)
                {
                    cmc.CsNode = null;
                }
                else if (AssMet.Parent is XP.FoxmethodContext fmc)
                {
                    fmc.CsNode = null;
                }
                if (xnode == null)
                    xnode = AssMet;
            }
            #endregion
            BasePropertyDeclarationSyntax prop;
            var accessorList = _syntaxFactory.AccessorList(SyntaxFactory.MakeToken(SyntaxKind.OpenBraceToken),
                        accessors, SyntaxFactory.MakeToken(SyntaxKind.CloseBraceToken));
            // A Property in Roslyn is either an Indexer (when there are parameters)
            // or a property
            if (voPropParams != null)
            {
                prop = _syntaxFactory.IndexerDeclaration(
                    attributeLists: EmptyList<AttributeListSyntax>(),
                    modifiers: outerMods.ToList<SyntaxToken>(),
                    type: voPropType,
                    explicitInterfaceSpecifier: null,
                    thisKeyword: SyntaxFactory.MakeToken(SyntaxKind.ThisKeyword, vop.idName.Text),
                    parameterList: voPropParams,
                    accessorList: accessorList,
                    expressionBody: null,
                    semicolonToken: SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken));
            }
            else
            {
                prop = _syntaxFactory.PropertyDeclaration(
                    attributeLists: EmptyList<AttributeListSyntax>(),
                    modifiers: outerMods.ToList<SyntaxToken>(),
                    type: voPropType,
                    explicitInterfaceSpecifier: null,
                    identifier: vop.idName,
                    accessorList: accessorList,
                    expressionBody: null,
                    initializer: null,
                    semicolonToken: SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken));
            }

            _pool.Free(accessors);
            _pool.Free(getMods);
            _pool.Free(setMods);
            _pool.Free(outerMods);
            prop.XNode = xnode;
            return prop;
        }

        #endregion

        #region Generic Visitor methods
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

        public override void ExitEveryRule([NotNull] ParserRuleContext ctxt)
        {
            var context = ctxt as XSharpParserRuleContext;
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
            if (context is XP.IEntityContext && CurrentEntity == context)
                Entities.Pop();
        }
        #endregion

        #region Main Entrypoints

        public override void EnterScript([NotNull] XP.ScriptContext context)
        {
            GlobalClassEntities = CreateClassEntities();
            ClassEntities.Push(GlobalClassEntities);

            if (context._References?.Count > 0 || context._Includes?.Count > 0)
            {
                var dirs = _pool.Allocate();
                foreach (var @ref in context._References)
                {
                    var arg = @ref.SyntaxLiteralValue(_options);
                    dirs.Add(_syntaxFactory.ReferenceDirectiveTrivia(SyntaxFactory.MissingToken(SyntaxKind.HashToken),
                        SyntaxFactory.MissingToken(SyntaxKind.ReferenceKeyword),
                        arg,
                        SyntaxFactory.MissingToken(SyntaxKind.EndOfDirectiveToken), true));
                }
                foreach (var inc in context._Includes)
                {
                    var arg = inc.SyntaxLiteralValue(_options);
                    dirs.Add(_syntaxFactory.LoadDirectiveTrivia(SyntaxFactory.MissingToken(SyntaxKind.HashToken),
                        SyntaxFactory.MissingToken(SyntaxKind.LoadKeyword),
                        arg,
                        SyntaxFactory.MissingToken(SyntaxKind.EndOfDirectiveToken), true));
                }
                // HACK: Insert a dummy extern alias to ensure it is the first token in the compilation unit (it will be ignored during imports handling)
                GlobalEntities.Externs.Add(_syntaxFactory.ExternAliasDirective(
                    SyntaxFactory.MissingToken(dirs.ToListNode(), SyntaxKind.ExternKeyword, null),
                    SyntaxFactory.MissingToken(SyntaxKind.AliasKeyword),
                    SyntaxFactory.Identifier(XSharpSpecialNames.ScriptDummy),
                    SyntaxFactory.MissingToken(SyntaxKind.SemicolonToken)
                    ));
                _pool.Free(dirs);
            }
        }

        public override void ExitScript([NotNull] XP.ScriptContext context)
        {
            var generated = ClassEntities.Pop();
            GlobalEntities.Members.AddRange(generated.Members);
            generated.Free();

            // Add: using static Functions
            AddUsingWhenMissing(GlobalEntities.Usings, this.GlobalClassName, true, null);

            // Add: using System
            AddUsingWhenMissing(GlobalEntities.Usings, "System", false, null);
        }

        public override void ExitScriptEntity([NotNull] XP.ScriptEntityContext context)
        {
            if (context.Entity != null)
            {
                var s = context.Entity.CsNode;

                if (s is NamespaceDeclarationSyntax)
                {
                    context.AddError(new ParseErrorData(context.Entity, ErrorCode.ERR_NamespaceNotAllowedInScript));
                    GlobalEntities.Members.Add(s as MemberDeclarationSyntax);
                }
                else if (s is MemberDeclarationSyntax)
                    GlobalEntities.Members.Add(s as MemberDeclarationSyntax);
                else if (s is UsingDirectiveSyntax)
                {
                    var u = s as UsingDirectiveSyntax;
                    AddUsingWhenMissing(GlobalEntities.Usings, u.Name, u.StaticKeyword != null, u.Alias);
                }
                else if (s is AttributeListSyntax)
                {
                    context.AddError(new ParseErrorData(context.Entity, ErrorCode.ERR_AttributesNotAllowed));
                    GlobalEntities.Attributes.Add(s as AttributeListSyntax);
                }
                else if (s is ExternAliasDirectiveSyntax)
                {
                    GlobalEntities.Externs.Add(s as ExternAliasDirectiveSyntax);
                }
            }
            else if (context.Stmt != null)
            {
                var s = context.Stmt.CsNode;
                if (s is SyntaxList<StatementSyntax>)
                {
                    foreach (var stmt in context.Stmt.GetList<StatementSyntax>())
                    {
                        if ((stmt is LocalDeclarationStatementSyntax) && !stmt.ContainsDiagnostics)
                        {
                            var local = (LocalDeclarationStatementSyntax)stmt;
                            var decl = _syntaxFactory.FieldDeclaration(
                                EmptyList<AttributeListSyntax>(),
                                local.Modifiers,
                                local.Declaration,
                                local.SemicolonToken);
                            GlobalClassEntities.Members.Add(decl);
                        }
                        else
                        {
                            GlobalClassEntities.Members.Add(_syntaxFactory.GlobalStatement(stmt));
                        }
                    }
                }
                else if (context.Stmt is XP.ExpressionStmtContext)
                {
                    if (s is BlockSyntax)
                    {
                        var b = (BlockSyntax)s;
                        foreach (var stmt in b.Statements)
                        {
                            GlobalClassEntities.Members.Add(_syntaxFactory.GlobalStatement(
                                _syntaxFactory.ExpressionStatement(
                                    ((ExpressionStatementSyntax)stmt).Expression,
                                    SyntaxFactory.MissingToken(SyntaxKind.SemicolonToken))
                                ));
                        }
                    }
                    else
                    {
                        GlobalClassEntities.Members.Add(_syntaxFactory.GlobalStatement(
                            _syntaxFactory.ExpressionStatement(
                                ((ExpressionStatementSyntax)s).Expression,
                                SyntaxFactory.MissingToken(SyntaxKind.SemicolonToken))
                            ));

                    }
                }
                else
                {
                    GlobalClassEntities.Members.Add(_syntaxFactory.GlobalStatement(context.Stmt.Get<StatementSyntax>()));
                }
            }
            /*else if (context.Decl != null)
            {
                GlobalEntities.Members.Add(context.Decl.Get<MemberDeclarationSyntax>());
            }*/
        }

        public override void EnterMacroScript([NotNull] XP.MacroScriptContext context)
        {
            GlobalClassEntities = CreateClassEntities();
            ClassEntities.Push(GlobalClassEntities);
        }

        public override void ExitMacroScript([NotNull] XP.MacroScriptContext context)
        {
            ExpressionSyntax e;
            if (context.CbExpr != null)
            {
                e = context.CbExpr.Get<ExpressionSyntax>();
                e.XNode = null;
            }
            else if (context.Code != null)
            {
                e = _syntaxFactory.ParenthesizedLambdaExpression(
                    asyncKeyword: null,
                    parameterList: EmptyParameterList(),
                    arrowToken: SyntaxFactory.MakeToken(SyntaxKind.EqualsGreaterThanToken),
                    body: context.Code.Get<CSharpSyntaxNode>());
            }
            else /* should never happen! */
            {
                e = _syntaxFactory.ParenthesizedLambdaExpression(
                    asyncKeyword: null,
                    parameterList: EmptyParameterList(),
                    arrowToken: SyntaxFactory.MakeToken(SyntaxKind.EqualsGreaterThanToken),
                    body: MakeBlock(_syntaxFactory.EmptyStatement(SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken))));
            }
            /*if (_options.HasRuntime)
            {
                var decl = GenerateLocalDecl("$result", _syntaxFactory.IdentifierName(SyntaxFactory.MakeIdentifier("CODEBLOCK")), e);
                GlobalClassEntities.Members.Add(_syntaxFactory.GlobalStatement(decl));
                e = _syntaxFactory.IdentifierName(SyntaxFactory.MakeIdentifier("$res"));
            }*/
            GlobalClassEntities.Members.Add(_syntaxFactory.GlobalStatement(
                _syntaxFactory.ExpressionStatement(
                    e,
                    SyntaxFactory.MissingToken(SyntaxKind.SemicolonToken))
                ));

            var generated = ClassEntities.Pop();
            GlobalEntities.Members.AddRange(generated.Members);
            generated.Free();

            // Add: using static Functions
            AddUsingWhenMissing(GlobalEntities.Usings, this.GlobalClassName, true, null);

            // Add: using System
            AddUsingWhenMissing(GlobalEntities.Usings, "System", false, null);
        }

        protected void _enterSource(XP.ISourceContext context)
        {
            //System.Diagnostics.Debug.WriteLine("Enter Source " + _fileName);
            GlobalClassEntities = CreateClassEntities();
            ClassEntities.Push(GlobalClassEntities);
        }
        public override void EnterSource([NotNull] XP.SourceContext context)
        {
            _enterSource(context);
        }

        protected void ProcessEntity(SyntaxListBuilder<MemberDeclarationSyntax> globalTypes, XSharpParserRuleContext context)
        {
            var s = context.CsNode;
            if (s == null)
                return;
            if (s is NamespaceDeclarationSyntax)
            {
                GlobalEntities.Members.Add(s as MemberDeclarationSyntax);
            }
            else if (s is MemberDeclarationSyntax)
            {
                globalTypes.Add(s as MemberDeclarationSyntax);
            }
            else if (s is UsingDirectiveSyntax)
            {
                var u = s as UsingDirectiveSyntax;
                AddUsingWhenMissing(GlobalEntities.Usings, u.Name, u.StaticKeyword != null, u.Alias);
            }
            else if (s is AttributeListSyntax)
            {
                GlobalEntities.Attributes.Add(s as AttributeListSyntax);
            }
            else if (s is ExternAliasDirectiveSyntax)
            {
                GlobalEntities.Externs.Add(s as ExternAliasDirectiveSyntax);
            }

        }
        protected BlockSyntax processEntityBody(XP.IEntityWithBodyContext entity)
        {
            if (entity.Statements == null)
                return null;
            entity.Statements.SetSequencePoint();
            var body = entity.Statements.Get<BlockSyntax>();
            if (entity is XP.IBodyWithLocalFunctions iblf)
            {
                if (iblf.LocalFunctions?.Count > 0)
                {
                    var stmts = _pool.Allocate<StatementSyntax>();
                    stmts.AddRange(body.Statements);
                    foreach (LocalFunctionStatementSyntax local in iblf.LocalFunctions)
                    {
                        stmts.Add(local);
                    }
                    var newbody = body.Update(body.OpenBraceToken, stmts, body.CloseBraceToken);
                    _pool.Free(stmts);

                    if (body.XNode != null)
                    {
                        body.XNode.Put(newbody);
                    }
                    body = newbody;
                }
            }
            return body;
        }


        protected ParameterListSyntax getParameters(XP.ParameterListContext context)
        {
            return context?.Get<ParameterListSyntax>() ?? EmptyParameterList();
        }
        protected SyntaxList<AttributeListSyntax> getAttributes (XP.AttributesContext attributes)
        {
            return attributes?.GetList<AttributeListSyntax>() ?? EmptyList<AttributeListSyntax>();
        }


        protected TypeParameterListSyntax getTypeParameters(XP.TypeparametersContext context)
        {
            if (context != null)
                return context.Get<TypeParameterListSyntax>();
            return null;

        }
        protected SyntaxList<TypeParameterConstraintClauseSyntax>  getTypeConstraints(IList<XP.TypeparameterconstraintsclauseContext> context)
        {
            return MakeList<TypeParameterConstraintClauseSyntax>(context);
        }

        protected TypeSyntax getDataType(XP.DatatypeContext context)
        {
            if (context != null)
                return context.Get<TypeSyntax>();
            return _getMissingType();
        }
        protected TypeSyntax getReturnType(XP.IEntityContext context)
        {
            return getDataType(context.ReturnType);
        }
        protected void finishCompilationUnit(SyntaxListBuilder<MemberDeclarationSyntax> globalTypes)
        {
            FinalizeGlobalEntities();

            var generated = ClassEntities.Pop();
            if (generated.Members.Count > 0)
            {
                GlobalEntities.Members.Add(GenerateGlobalClass(GlobalClassName, false, false, generated.Members));
            }
            generated.Free();

            if (!string.IsNullOrEmpty(_options.DefaultNamespace))
            {
                GlobalEntities.Members.Add(
                    _syntaxFactory.NamespaceDeclaration(SyntaxFactory.MakeToken(SyntaxKind.NamespaceKeyword),
                        name: GenerateQualifiedName(_options.DefaultNamespace),
                        openBraceToken: SyntaxFactory.MakeToken(SyntaxKind.OpenBraceToken),
                        externs: MakeList<ExternAliasDirectiveSyntax>(),
                        usings: MakeList<UsingDirectiveSyntax>(),
                        members: globalTypes,
                        closeBraceToken: SyntaxFactory.MakeToken(SyntaxKind.CloseBraceToken),
                        semicolonToken: SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken))
                    );

                AddUsingWhenMissing(GlobalEntities.Usings, _options.DefaultNamespace, false, null);
            }
            else
            {
                GlobalEntities.Members.AddRange(globalTypes);
            }
            // Add: using static Functions
            AddUsingWhenMissing(GlobalEntities.Usings, this.GlobalClassName, true, null);

            // Add: using System
            AddUsingWhenMissing(GlobalEntities.Usings, "System", false, null);
        }


        protected void _exitSource(XP.ISourceContext context)
        {
            _exitSource(context, context.Entities);
        }
        protected void _exitSource(XP.ISourceContext context, IList<XP.EntityContext> Entities)
        {
            // globaltypes are the types that are not embedded in a namespace
            // they will be embedded in the default namespace when the 
            // compiler option to do so is selected
            // GlobalEntities.Members will be added to the output without extra
            // namespace
            var globalTypes = _pool.Allocate<MemberDeclarationSyntax>();
            foreach (var entityCtx in Entities)
            {
                ProcessEntity(globalTypes, entityCtx);
            }
            finishCompilationUnit(globalTypes);
            _pool.Free(globalTypes);
            context.PragmaOptions = null;
            if (PragmaOptions.Count > 0)
            {
                context.PragmaOptions = PragmaOptions;
                GlobalEntities.PragmaOptions = PragmaOptions;
            }
            if (PragmaWarnings.Count > 0)
            {
                GlobalEntities.PragmaWarnings = PragmaWarnings;
            }

            //System.Diagnostics.Debug.WriteLine("Exit Source " + _fileName);
        }
        public override void ExitSource([NotNull] XP.SourceContext context)
        {
            _exitSource(context);
        }
        private string RemoveUnwantedCharacters(string input)
        {
            StringBuilder result = new StringBuilder(input.Length);
            foreach (char ch in input)
            {
                if (Char.IsLetterOrDigit(ch))
                    result.Append(ch);
                else
                    result.Append('_');
            }
            return result.ToString();

        }

        public void FinalizeGlobalEntities()
        {
            if (GlobalEntities.GlobalClassMembers.Count > 0)
            {
                AddUsingWhenMissing(GlobalEntities.Usings, GlobalClassName, true, null);
                GlobalEntities.Members.Add(GenerateGlobalClass(GlobalClassName, false, false, GlobalEntities.GlobalClassMembers));
                GlobalEntities.GlobalClassMembers.Clear();

            }
            if (GlobalEntities.StaticGlobalClassMembers.Count > 0)
            {
                string filename = PathUtilities.GetFileName(_fileName);
                filename = PathUtilities.RemoveExtension(filename);
                filename = RemoveUnwantedCharacters(filename);
                string className = GlobalClassName + "$" + filename + "$";
                AddUsingWhenMissing(GlobalEntities.Usings, className, true, null);
                GlobalEntities.Members.Add(GenerateGlobalClass(className, true, false, GlobalEntities.StaticGlobalClassMembers));
                GlobalEntities.StaticGlobalClassMembers.Clear();
            }

        }

        public override void ExitUsing_([NotNull] XP.Using_Context context)
        {
            context.Put(_syntaxFactory.UsingDirective(SyntaxFactory.MakeToken(SyntaxKind.UsingKeyword),
                staticKeyword: context.Static?.SyntaxKeyword(),
                alias: context.Alias == null ? null : _syntaxFactory.NameEquals(context.Alias.Get<IdentifierNameSyntax>(), SyntaxFactory.MakeToken(SyntaxKind.EqualsToken)),
                name: context.Name.Get<NameSyntax>(),
                semicolonToken: SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken)));
        }

        protected void _exitNamespace(XSharpParserRuleContext context, string name, IList<XSharpParserRuleContext> entities)
        {
            var externs = _pool.Allocate<ExternAliasDirectiveSyntax>();
            var usings = _pool.Allocate<UsingDirectiveSyntax>();
            var members = _pool.Allocate<MemberDeclarationSyntax>();
            foreach (var entityCtx in entities)
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

            MemberDeclarationSyntax ns = _syntaxFactory.NamespaceDeclaration(SyntaxFactory.MakeToken(SyntaxKind.NamespaceKeyword),
                name: GenerateQualifiedName(name),
                openBraceToken: SyntaxFactory.MakeToken(SyntaxKind.OpenBraceToken),
                externs: externs,
                usings: usings,
                members: members,
                closeBraceToken: SyntaxFactory.MakeToken(SyntaxKind.CloseBraceToken),
                semicolonToken: SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken));

            _pool.Free(externs);
            _pool.Free(usings);
            _pool.Free(members);
            context.Put(ns);
            // Now add our namespace to the usings list so functions etc can find members 
            string ourname = name;
            var parent = context.Parent;
            while (parent is XP.EntityContext)
            {
                var parentns = parent.Parent as XP.Namespace_Context;
                if (parentns != null)
                {
                    ourname = parentns.Name.GetText() + "." + ourname;
                    parent = parentns.Parent;
                }
                else
                    break;
            }
            AddUsingWhenMissing(GlobalEntities.Usings, ourname, false, null);

        }
        public override void ExitNamespace_([NotNull] XP.Namespace_Context context)
        {
            var entities = new List<XSharpParserRuleContext>();
            entities.AddRange(context._Entities);
            _exitNamespace(context, context.Name.GetText(), entities);
        }

        protected void addGlobalEntity(MemberDeclarationSyntax m, bool isStatic)
        {
            // make sure we do not add entities twice
            if (isStatic)
            {
                // When last entity did not go to the functions class or wasn't a static member
                if (GlobalEntities.LastMember is XP.IGlobalEntityContext && !GlobalEntities.LastIsStatic)
                {
                    FinalizeGlobalEntities();
                }
                GlobalEntities.StaticGlobalClassMembers.Add(m);
            }
            else
            {
                // When last entity did not go to the functions class or was a static member
                if (GlobalEntities.LastMember is XP.IGlobalEntityContext && GlobalEntities.LastIsStatic)
                {
                    FinalizeGlobalEntities();
                }
                GlobalEntities.GlobalClassMembers.Add(m);
            }
            GlobalEntities.LastMember = m;
            GlobalEntities.LastIsStatic = isStatic;

        }

        protected void ProcessGlobalEntityContext(XP.IGlobalEntityContext entity)
        {
            var bStaticVisibility = false;
            var modifiers = entity.FuncProcModifiers;
            if (entity is XP.FuncprocContext proc)
            {
                if (proc.InitExit != null)  // Init & Exit procedures are never static
                {
                    modifiers = null;
                }
            }
            else if (entity is XP.VoglobalContext glob)
            {
                if (glob.Static != null)
                { 
                    bStaticVisibility = true;
                }
            }
            if (modifiers != null)
                bStaticVisibility = modifiers.IsStaticVisible;


            var m = entity.Get<MemberDeclarationSyntax>();
            addGlobalEntity(m, bStaticVisibility);
        }
        protected void ProcessNonGlobalEntity(XSharpParserRuleContext context, IXParseTree entity)
        {
            // When last entity has to go to the functions class 
            if (GlobalEntities.LastMember is XP.IGlobalEntityContext)
            {
                FinalizeGlobalEntities();
            }
            context.Put(entity.Get<CSharpSyntaxNode>());
            GlobalEntities.LastMember = entity;
        }

        protected void _exitEntity(XSharpParserRuleContext context)
        {
            var entity = context.children[0] as IXParseTree;
            if (! (entity is XP.PragmaContext))
            {
                if (_isScript)
                {
                    context.Put(entity.Get<CSharpSyntaxNode>());
                }
                else if (entity is XP.IGlobalEntityContext) // procedure, function, DLL function, GLobal, Define
                {
                    ProcessGlobalEntityContext(entity as XP.IGlobalEntityContext);
                }
                else
                {
                    // When last entity has to go to the functions class 
                    ProcessNonGlobalEntity(context, entity);
                }
            }
        }

        public override void EnterPragma([NotNull] XP.PragmaContext context)
        {
            if (context.Pragma is PragmaWarning)
            {
                var warning = context.Pragma as PragmaWarning;
                SyntaxToken kind1;
                SyntaxToken kind2;
                kind1 = SyntaxFactory.MakeToken(SyntaxKind.WarningKeyword, warning.Warning.Text);
                if (warning.State == Pragmastate.Off)
                {
                    kind2 = SyntaxFactory.MakeToken(SyntaxKind.DisableKeyword, warning.Switch.Text);
                }
                else
                {
                    kind2 = SyntaxFactory.MakeToken(SyntaxKind.RestoreKeyword, warning.Switch.Text);
                }

                var list = new List<ExpressionSyntax>();
                foreach (var token in warning.Numbers)
                {
                    if (token.Type == XSharpParser.INT_CONST)
                    {
                        var num = token.SyntaxLiteralValue(_options);
                        list.Add(GenerateLiteral((int)num.Value));
                    }
                    else
                    {
                        var id = _syntaxFactory.IdentifierName(SyntaxFactory.MakeIdentifier(token.Text.ToUpper()));
                        list.Add(id);
                    }
                }

                var pragma = SyntaxFactory.PragmaWarningDirectiveTrivia(
                    SyntaxFactory.MakeToken(SyntaxKind.HashToken),
                    context.Start.SyntaxKeyword(),
                    kind1, kind2, MakeSeparatedList(list.ToArray()),
                    SyntaxFactory.MakeToken(SyntaxKind.EndOfDirectiveToken),
                    true);
                context.Put(pragma);
                PragmaWarnings.Add(pragma);
            }
            else if (context.Pragma is PragmaOption option)
            {
                PragmaOptions.Add(option);
            }
            return;
        }

        public override void ExitEntity([NotNull] XP.EntityContext context)
        {
            _exitEntity(context);
        }
        #endregion
        

        #region User Defined Types
        public override void EnterInterface_([NotNull] XP.Interface_Context context)
        {
            ClassEntities.Push(CreateClassEntities());
        }

        public override void ExitInterface_([NotNull] XP.Interface_Context context)
        {
            context.SetSequencePoint(context.I, context.e.Stop);
            var members = _pool.Allocate<MemberDeclarationSyntax>();
            var generated = ClassEntities.Pop();
            var mods = context.Modifiers?.GetList<SyntaxToken>() ?? TokenListWithDefaultVisibility();
            context.Data.Partial = mods.Any((int)SyntaxKind.PartialKeyword);
            if (generated.Members.Count > 0)
            {
                members.AddRange(generated.Members);
            }
            if (generated.VoProperties != null)
            {
                foreach (var vop in generated.VoProperties.Values)
                {
                    var prop = GenerateVoProperty(vop, context);
                    if (prop != null)
                        members.Add(prop);
                }
            }
            // Do this after VOProps generation because GenerateVOProperty sets the members
            // for Access & Assign to NULL
            foreach (var mCtx in context._Members)
            {
                if (mCtx.CsNode != null)
                    members.Add(mCtx.Get<MemberDeclarationSyntax>());
            }
            generated.Free();
            var baseTypes = _pool.AllocateSeparated<BaseTypeSyntax>();
            foreach (var pCtx in context._Parents)
            {
                if (baseTypes.Count > 0)
                    baseTypes.AddSeparator(SyntaxFactory.MakeToken(SyntaxKind.CommaToken));
                baseTypes.Add(_syntaxFactory.SimpleBaseType(pCtx.Get<TypeSyntax>()));
            }
            MemberDeclarationSyntax m = _syntaxFactory.InterfaceDeclaration(
                attributeLists: getAttributes(context.Attributes),
                modifiers: mods,
                keyword: SyntaxFactory.MakeToken(SyntaxKind.InterfaceKeyword),
                identifier: context.Id.Get<SyntaxToken>(),
                typeParameterList: getTypeParameters(context.TypeParameters),
                baseList: _syntaxFactory.BaseList(SyntaxFactory.MakeToken(SyntaxKind.ColonToken), baseTypes),
                constraintClauses: getTypeConstraints(context._ConstraintsClauses),
                openBraceToken: SyntaxFactory.MakeToken(SyntaxKind.OpenBraceToken),
                members: members,
                closeBraceToken: SyntaxFactory.MakeToken(SyntaxKind.CloseBraceToken),
                semicolonToken: null);
            _pool.Free(members);
            _pool.Free(baseTypes);
            if (context.Namespace != null)
            {
                m = AddNameSpaceToMember(context.Namespace, m);
            }
            else
            {
                m = (MemberDeclarationSyntax)CheckTypeName(context, "INTERFACE", m);
            }
            context.Put(m);
            if (context.Data.Partial)
            {
                GlobalEntities.NeedsProcessing = true;
            }
        }

        public override void EnterClass_([NotNull] XP.Class_Context context)
        {
            ClassEntities.Push(CreateClassEntities());
        }

        public override void ExitClass_([NotNull] XP.Class_Context context)
        {
            context.SetSequencePoint(context.C, context.e.Stop);
            var members = _pool.Allocate<MemberDeclarationSyntax>();
            var generated = ClassEntities.Pop();
            var mods = context.Modifiers?.GetList<SyntaxToken>() ?? TokenListWithDefaultVisibility();
            context.Data.Partial = mods.Any((int)SyntaxKind.PartialKeyword);
            if (generated.Members.Count > 0)
            {
                members.AddRange(generated.Members);
            }
            if (generated.VoProperties != null)
            {
                foreach (var vop in generated.VoProperties.Values)
                {
                    var prop = GenerateVoProperty(vop, context);
                    if (prop != null)
                        members.Add(prop);
                }
            }
            // check if class has Ctor. 
            foreach (var mem in members.ToList())
            {
                // when an instant constructors then remember this
                if (mem is ConstructorDeclarationSyntax && !((ConstructorDeclarationSyntax)mem).IsStatic())
                {
                    context.Data.HasInstanceCtor = true;
                    break;
                }
            }

            // Do this after VOProps generation because GenerateVOProperty sets the members
            // for Access & Assign to NULL
            foreach (var mCtx in context._Members)
            {
                if (mCtx.CsNode != null)
                    members.Add(mCtx.Get<MemberDeclarationSyntax>());
            }
            generated.Free();
            var baseTypes = _pool.AllocateSeparated<BaseTypeSyntax>();
            var baseType = context.BaseType?.Get<TypeSyntax>();
            if (baseType != null)
            {
                baseTypes.Add(_syntaxFactory.SimpleBaseType(baseType));
            }
            foreach (var iCtx in context._Implements)
            {
                if (baseTypes.Count > 0)
                    baseTypes.AddSeparator(SyntaxFactory.MakeToken(SyntaxKind.CommaToken));
                baseTypes.Add(_syntaxFactory.SimpleBaseType(iCtx.Get<TypeSyntax>()));
            }

            MemberDeclarationSyntax m = _syntaxFactory.ClassDeclaration(
                attributeLists: getAttributes(context.Attributes),
                modifiers: mods,
                keyword: SyntaxFactory.MakeToken(SyntaxKind.ClassKeyword),
                identifier: context.Id.Get<SyntaxToken>(),
                typeParameterList: getTypeParameters(context.TypeParameters),
                baseList: _syntaxFactory.BaseList(SyntaxFactory.MakeToken(SyntaxKind.ColonToken), baseTypes),
                constraintClauses: getTypeConstraints(context._ConstraintsClauses),
                openBraceToken: SyntaxFactory.MakeToken(SyntaxKind.OpenBraceToken),
                members: members,
                closeBraceToken: SyntaxFactory.MakeToken(SyntaxKind.CloseBraceToken),
                semicolonToken: null);
            _pool.Free(members);
            _pool.Free(baseTypes);
            if (context.Namespace != null)
            {
                m = AddNameSpaceToMember(context.Namespace, m);

            }
            else
            {
                m = (MemberDeclarationSyntax)CheckTypeName(context, "CLASS", m);
            }
            context.Put(m);
            if (context.Data.Partial)
            {
                GlobalEntities.NeedsProcessing = true;
            }
        }

        public override void EnterStructure_([NotNull] XP.Structure_Context context)
        {
            ClassEntities.Push(CreateClassEntities());
        }

        public override void ExitStructure_([NotNull] XP.Structure_Context context)
        {
            context.SetSequencePoint(context.S, context.e.Stop);
            var members = _pool.Allocate<MemberDeclarationSyntax>();
            var generated = ClassEntities.Pop();
            var mods = context.Modifiers?.GetList<SyntaxToken>() ?? TokenListWithDefaultVisibility();
            context.Data.Partial = mods.Any((int)SyntaxKind.PartialKeyword);
            if (generated.Members.Count > 0)
            {
                members.AddRange(generated.Members);
            }
            if (generated.VoProperties != null)
            {
                foreach (var vop in generated.VoProperties.Values)
                {
                    var prop = GenerateVoProperty(vop, context);
                    if (prop != null)
                        members.Add(prop);
                }
            }
            // Do this after VOProps generation because GenerateVOProperty sets the members
            // for Access & Assign to NULL
            foreach (var mCtx in context._Members)
            {
                if (mCtx.CsNode != null)
                    members.Add(mCtx.Get<MemberDeclarationSyntax>());
            }
            generated.Free();
            var baseTypes = _pool.AllocateSeparated<BaseTypeSyntax>();
            foreach (var iCtx in context._Implements)
            {
                if (baseTypes.Count > 0)
                    baseTypes.AddSeparator(SyntaxFactory.MakeToken(SyntaxKind.CommaToken));
                baseTypes.Add(_syntaxFactory.SimpleBaseType(iCtx.Get<TypeSyntax>()));
            }

            MemberDeclarationSyntax m = _syntaxFactory.StructDeclaration(
                attributeLists: getAttributes(context.Attributes),
                modifiers: mods,
                keyword: SyntaxFactory.MakeToken(SyntaxKind.StructKeyword),
                identifier: context.Id.Get<SyntaxToken>(),
                typeParameterList: getTypeParameters(context.TypeParameters),
                baseList: _syntaxFactory.BaseList(SyntaxFactory.MakeToken(SyntaxKind.ColonToken), baseTypes),
                constraintClauses: getTypeConstraints(context._ConstraintsClauses),
                openBraceToken: SyntaxFactory.MakeToken(SyntaxKind.OpenBraceToken),
                members: members,
                closeBraceToken: SyntaxFactory.MakeToken(SyntaxKind.CloseBraceToken),
                semicolonToken: null);
            _pool.Free(members);
            _pool.Free(baseTypes);
            if (context.Namespace != null)
            {
                m = AddNameSpaceToMember(context.Namespace, m);
            }
            else
            {
                m = (MemberDeclarationSyntax)CheckTypeName(context, "STRUCTURE", m);
            }
            context.Put(m);
            if (context.Data.Partial)
            {
                GlobalEntities.NeedsProcessing = true;
            }
        }

        public override void ExitDelegate_([NotNull] XP.Delegate_Context context)
        {
            context.SetSequencePoint(context.D, context.e);
            MemberDeclarationSyntax m = _syntaxFactory.DelegateDeclaration(
                attributeLists: getAttributes(context.Attributes),
                modifiers: context.Modifiers?.GetList<SyntaxToken>() ?? TokenListWithDefaultVisibility(),
                delegateKeyword: SyntaxFactory.MakeToken(SyntaxKind.DelegateKeyword),
                returnType: getReturnType(context),
                identifier: context.Id.Get<SyntaxToken>(),
                typeParameterList: getTypeParameters(context.TypeParameters),
                parameterList: getParameters(context.ParamList),
                constraintClauses: getTypeConstraints(context._ConstraintsClauses),
                semicolonToken: SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken));
            if (context.Namespace != null)
            {
                m = AddNameSpaceToMember(context.Namespace, m);
            }
            else
            {
                m = (MemberDeclarationSyntax)CheckTypeName(context, "STRUCTURE", m);
            }
            context.Put(m);
        }
        #endregion

        #region Enums
        public override void ExitEnum_([NotNull] XP.Enum_Context context)
        {
            context.SetSequencePoint(context.E, context.e.Stop);
            BaseListSyntax baselist = default(BaseListSyntax);
            var baseTypes = _pool.AllocateSeparated<BaseTypeSyntax>();
            if (context.Type != null)
            {
                baseTypes.Add(_syntaxFactory.SimpleBaseType(context.Type.Get<TypeSyntax>()));
                baselist = _syntaxFactory.BaseList(
                    SyntaxFactory.MakeToken(SyntaxKind.ColonToken), baseTypes);

            }
            MemberDeclarationSyntax m = _syntaxFactory.EnumDeclaration(
                attributeLists: getAttributes(context.Attributes),
                modifiers: context.Modifiers?.GetList<SyntaxToken>() ?? TokenListWithDefaultVisibility(),
                enumKeyword: SyntaxFactory.MakeToken(SyntaxKind.EnumKeyword),
                identifier: context.Id.Get<SyntaxToken>(),
                baseList: baselist,
                openBraceToken: SyntaxFactory.MakeToken(SyntaxKind.OpenBraceToken),
                members: MakeSeparatedList<EnumMemberDeclarationSyntax>(context._Members),
                closeBraceToken: SyntaxFactory.MakeToken(SyntaxKind.CloseBraceToken),
                semicolonToken: SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken));
            if (context.Namespace != null)
            {
                m = AddNameSpaceToMember(context.Namespace, m);
            }
            context.Put(m);
            _pool.Free(baseTypes);
        }

        public override void ExitEnummember([NotNull] XP.EnummemberContext context)
        {
            context.Put(_syntaxFactory.EnumMemberDeclaration(
                attributeLists: getAttributes(context.Attributes),
                identifier: context.Id.Get<SyntaxToken>(),
                equalsValue: context.Expr == null ? null : _syntaxFactory.EqualsValueClause(SyntaxFactory.MakeToken(SyntaxKind.EqualsToken),
                    context.Expr.Get<ExpressionSyntax>())));
        }

        #endregion
        
        #region Events
        public override void ExitEvent_([NotNull] XP.Event_Context context)
        {

            if (context.Multi != null)
                context.SetSequencePoint(context.E, context.Multi.Start);
            else
                context.SetSequencePoint(context.E, context.end);

            var attrLists = getAttributes(context.Attributes);
            var type_ = getReturnType(context);
            var singleLine = context._LineAccessors != null && context._LineAccessors.Count > 0;
            var multiLine = context.Multi != null && context._Accessors.Count > 0;
            ExplicitInterfaceSpecifierSyntax explif = null;
            if (context.ExplicitIface != null)
            {
                explif = _syntaxFactory.ExplicitInterfaceSpecifier(
                        name: context.ExplicitIface.Get<NameSyntax>(),
                        dotToken: SyntaxFactory.MakeToken(SyntaxKind.DotToken));
            }
            var mods = context.Modifiers?.GetList<SyntaxToken>() ?? DefaultMethodModifiers(context.isInInterface(), context.isInStructure());
            //if (context.ExplicitIface != null)
            {
                var m = _pool.Allocate();
                foreach (var mod in mods)
                {
                    if (singleLine || multiLine)
                    {
                        if (mod.Kind != SyntaxKind.VirtualKeyword && mod.Kind != SyntaxKind.OverrideKeyword)
                            m.Add(mod);
                    }
                    else
                    {
                        if (mod.Kind != SyntaxKind.VirtualKeyword && mod.Kind != SyntaxKind.OverrideKeyword && mod.Kind != SyntaxKind.PublicKeyword)
                            m.Add(mod);
                    }
                }
                mods = m.ToList<SyntaxToken>();
                _pool.Free(m);
            }

            if (singleLine)         // Single Line Syntax
            {
                var acclist = _syntaxFactory.AccessorList(
                    SyntaxFactory.MakeToken(SyntaxKind.OpenBraceToken),
                    MakeList<AccessorDeclarationSyntax>(context._LineAccessors),
                    SyntaxFactory.MakeToken(SyntaxKind.CloseBraceToken));
                MemberDeclarationSyntax decl = _syntaxFactory.EventDeclaration(
                    attributeLists: attrLists,
                    modifiers: mods,
                    eventKeyword: SyntaxFactory.MakeToken(SyntaxKind.EventKeyword),
                    type: type_,
                    explicitInterfaceSpecifier: explif,
                    identifier: context.Id.Get<SyntaxToken>(),
                    accessorList: acclist);
                context.Put(decl);
            }
            else if (multiLine)        // Multi line Syntax
            {
                var acclist = _syntaxFactory.AccessorList(
                    SyntaxFactory.MakeToken(SyntaxKind.OpenBraceToken),
                    MakeList<AccessorDeclarationSyntax>(context._Accessors),
                    SyntaxFactory.MakeToken(SyntaxKind.CloseBraceToken));
                MemberDeclarationSyntax decl = _syntaxFactory.EventDeclaration(
                    attributeLists: attrLists,
                    modifiers: mods,
                    eventKeyword: SyntaxFactory.MakeToken(SyntaxKind.EventKeyword),
                    type: type_,
                    explicitInterfaceSpecifier: explif,
                    identifier: context.Id.Get<SyntaxToken>(),
                    accessorList: acclist);
                context.Put(decl);
            }
            else // Old Syntax, auto generate accessors
            {
                if (context.ExplicitIface != null)
                {
                    string evtFldName = XSharpSpecialNames.EventFieldNamePrefix + context.Id.Get<SyntaxToken>();
                    ClassEntities.Peek().Members.Add(
                        _syntaxFactory.FieldDeclaration(
                            EmptyList<AttributeListSyntax>(),
                            TokenList(SyntaxKind.StaticKeyword, SyntaxKind.InternalKeyword),
                            _syntaxFactory.VariableDeclaration(getReturnType(context),
                                MakeSeparatedList(GenerateVariable(evtFldName))),
                            SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken))
                        );
                    var add_ = _syntaxFactory.AccessorDeclaration(SyntaxKind.AddAccessorDeclaration,
                            attributeLists: EmptyList<AttributeListSyntax>(),
                            modifiers: EmptyList<SyntaxToken>(),
                            keyword: SyntaxFactory.MakeToken(SyntaxKind.AddKeyword),
                            body: MakeBlock(
                                MakeLock(GenerateSimpleName(evtFldName),
                                    GenerateExpressionStatement(
                                        _syntaxFactory.AssignmentExpression(SyntaxKind.AddAssignmentExpression,
                                            GenerateSimpleName(evtFldName),
                                            SyntaxFactory.MakeToken(SyntaxKind.PlusEqualsToken),
                                            GenerateSimpleName("value"))))
                                ),
                            expressionBody: null,
                            semicolonToken: null);
                    var remove_ = _syntaxFactory.AccessorDeclaration(SyntaxKind.RemoveAccessorDeclaration,
                                    attributeLists: EmptyList<AttributeListSyntax>(),
                                    modifiers: EmptyList<SyntaxToken>(),
                                    keyword: SyntaxFactory.MakeToken(SyntaxKind.RemoveKeyword),
                                    body: MakeBlock(MakeLock(GenerateSimpleName(evtFldName),
                                            GenerateExpressionStatement(
                                                _syntaxFactory.AssignmentExpression(SyntaxKind.SubtractAssignmentExpression,
                                                    GenerateSimpleName(evtFldName),
                                                    SyntaxFactory.MakeToken(SyntaxKind.MinusEqualsToken),
                                                    GenerateSimpleName("value"))))
                                        ),
                                    expressionBody: null,
                                    semicolonToken: null);
                    var acclist = _syntaxFactory.AccessorList(
                        SyntaxFactory.MakeToken(SyntaxKind.OpenBraceToken),
                        MakeList<AccessorDeclarationSyntax>(add_, remove_),
                        SyntaxFactory.MakeToken(SyntaxKind.CloseBraceToken));
                    MemberDeclarationSyntax decl = _syntaxFactory.EventDeclaration(
                        attributeLists: attrLists,
                        modifiers: mods,
                        eventKeyword: SyntaxFactory.MakeToken(SyntaxKind.EventKeyword),
                        type: type_,
                        explicitInterfaceSpecifier: explif,
                        identifier: context.Id.Get<SyntaxToken>(),
                        accessorList: acclist);
                    context.Put(decl);
                }
                else
                {
                    context.Put(_syntaxFactory.EventFieldDeclaration(
                        attributeLists: getAttributes(context.Attributes),
                        modifiers: context.Modifiers?.GetList<SyntaxToken>() ?? DefaultMethodModifiers(context.isInInterface(), context.isInStructure()),
                        eventKeyword: context.E.SyntaxKeyword(),
                        declaration: _syntaxFactory.VariableDeclaration(
                            getReturnType(context),
                            MakeSeparatedList<VariableDeclaratorSyntax>(
                                GenerateVariable(context.Id.Get<SyntaxToken>()))),
                        semicolonToken: SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken)));
                }
            }
        }
          
        public override void ExitEventLineAccessor([NotNull] XP.EventLineAccessorContext context)
        {
            if (context.ExprList != null)
            {
                context.ExprList.SetSequencePoint();
                context.Put(_syntaxFactory.AccessorDeclaration(context.Key.AccessorKind(),
                attributeLists: getAttributes(context.Attributes),
                modifiers: context.Modifiers?.GetList<SyntaxToken>() ?? EmptyList<SyntaxToken>(),
                keyword: context.Key.SyntaxKeyword(),
                body: MakeBlock(context.ExprList?.GetList<StatementSyntax>() ?? EmptyList<StatementSyntax>()),
                expressionBody: null,
                semicolonToken: SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken)));
            }
        }
        public override void EnterEventAccessor([NotNull] XP.EventAccessorContext context)
        {
            context.Data.MustBeVoid = true;
        }
        public override void ExitEventAccessor([NotNull] XP.EventAccessorContext context)
        {
            var nobody = context.ExpressionBody != null;
            var body = nobody ? null : processEntityBody(context);
            var expressionBody = GetExpressionBody(context.ExpressionBody);
            context.Put(_syntaxFactory.AccessorDeclaration(context.Key.AccessorKind(),
                attributeLists: getAttributes(context.Attributes),
                modifiers: context.Modifiers?.GetList<SyntaxToken>() ?? EmptyList<SyntaxToken>(),
                keyword: context.Key.SyntaxKeyword(),
                body: body,
                expressionBody: expressionBody,
                semicolonToken: SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken)));
        }
        #endregion

        #region Class Vars

        public override void ExitArraysub([NotNull] XP.ArraysubContext context)
        {
            context.Put(_syntaxFactory.ArrayRankSpecifier(
                SyntaxFactory.MakeToken(SyntaxKind.OpenBracketToken),
                MakeSeparatedList<ExpressionSyntax>(context._ArrayIndex),
                SyntaxFactory.MakeToken(SyntaxKind.CloseBracketToken)));
        }



        public override void ExitClassvars([NotNull] XP.ClassvarsContext context)
        {
            var varList = _pool.AllocateSeparated<VariableDeclaratorSyntax>();
            var varType = getDataType(context.Vars?.DataType);
            bool isFixed = context.Modifiers?._FIXED != null;
            varType.XVoDecl = true;
            if (context.Vars?.As?.Type == XP.IS)
            {
                varType.XVoIsDecl = true;
            }
            foreach (var varCtx in context.Vars._Var)
            {
                bool isDim = varCtx.Dim != null && varCtx.ArraySub != null;
                if (isDim)
                {
                    if (isFixed)
                    {
                        ClassEntities.Peek().Members.Add(_syntaxFactory.FieldDeclaration(
                            attributeLists: getAttributes(context.Attributes),
                            modifiers: context.Modifiers?.GetList<SyntaxToken>() ?? TokenListWithDefaultVisibility(),
                            declaration: _syntaxFactory.VariableDeclaration(
                                type: varType,
                                variables: MakeSeparatedList(varCtx.Get<VariableDeclaratorSyntax>())),
                            semicolonToken: SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken)));
                    }
                    else
                    {
                        ClassEntities.Peek().Members.Add(_syntaxFactory.FieldDeclaration(
                            attributeLists: getAttributes(context.Attributes),
                            modifiers: context.Modifiers?.GetList<SyntaxToken>() ?? TokenListWithDefaultVisibility(),
                            declaration: _syntaxFactory.VariableDeclaration(
                                type: _syntaxFactory.ArrayType(varType, MakeArrayRankSpecifier(varCtx.ArraySub._ArrayIndex.Count)),
                                variables: MakeSeparatedList(varCtx.Get<VariableDeclaratorSyntax>())),
                            semicolonToken: SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken)));
                    }
                }
                else
                {
                    if (varList.Count > 0)
                        varList.AddSeparator(SyntaxFactory.MakeToken(SyntaxKind.CommaToken));
                    varList.Add(varCtx.Get<VariableDeclaratorSyntax>());
                }
            }
            if (varList.Count > 0)
            {
                var decl = _syntaxFactory.VariableDeclaration(
                        type: varType,
                        variables: varList);
                var attributeList = getAttributes(context.Attributes);
                if (_options.HasRuntime)
                {

                    bool isInstance = context.Modifiers._Tokens.Any(t => t.Type == XSharpLexer.INSTANCE);
                    if (isInstance)
                    {
                        var attr = _pool.Allocate<AttributeListSyntax>();
                        attr.AddRange(attributeList);
                        GenerateAttributeList(attr, _options.XSharpRuntime ? XSharpQualifiedTypeNames.IsInstance : VulcanQualifiedTypeNames.IsInstance);
                        attributeList = attr.ToList();
                        _pool.Free(attr);
                    }
                }

                context.Put(_syntaxFactory.FieldDeclaration(
                    attributeLists: attributeList,
                    modifiers: context.Modifiers?.GetList<SyntaxToken>() ?? TokenListWithDefaultVisibility(),
                    declaration: decl,
                    semicolonToken: SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken)));
            }
            _pool.Free(varList);
        }

        public override void ExitClassVarList([NotNull] XP.ClassVarListContext context)
        {
            foreach (var cvCtx in context._Var)
            { 
                VisitClassvar(cvCtx);
            }
            // Check for dwDim := 512 IS DWORD which will be parsed as
            // dwDim := (512 IS DWORD)
            if (context.DataType == null)
            {
                var last = context._Var.Last();
                if (last.Initializer is XP.TypeCheckExpressionContext tcec)
                {
                    // expression was incorrectly parsed. Fix it here
                    last.Initializer = tcec.Expr;
                    context.As = tcec.Op;
                    context.DataType = tcec.Type;
                }
            }
        }

 
        public override void ExitClassvar([NotNull] XP.ClassvarContext context)
        {
            // nvk: Not handled here due to datatype, which is processed later
        }

    

        protected virtual void VisitClassvar([NotNull] XP.ClassvarContext context)
        {
            bool isDim = context.Dim != null && context.ArraySub != null;
            // make sure we do not initialize Interface and Structure members
            // context.Parent = classvarList
            // classvarList is used in VOGLobal, classvars
            // classvars is used in classmember, classmember is used in interface, class and structure
            var candefault = context.Parent.Parent is XP.VoglobalContext || context.isInClass();
            var initExpr = context.Initializer?.Get<ExpressionSyntax>();
            bool isFixed = (context.Parent.Parent as XP.ClassvarsContext)?.Modifiers?._FIXED != null;
            var dataType = ((XP.ClassVarListContext)context.Parent).DataType;
            var varType = getDataType(dataType);

            if (isDim)
            {
                if (isFixed)
                {
                    if (initExpr != null)
                    {
                        initExpr = initExpr.WithAdditionalDiagnostics(new SyntaxDiagnosticInfo(ErrorCode.ERR_SyntaxError, "AS"));
                        context.Initializer.Put(initExpr);
                        context.Op.Put(initExpr);
                        context.Put(GenerateVariable(context.Id.Get<SyntaxToken>(), initExpr));
                    }
                    else
                    {
                        context.Put(GenerateBuffer(context.Id.Get<SyntaxToken>(),
                            MakeBracketedArgumentList(context.ArraySub._ArrayIndex.Select(e => _syntaxFactory.Argument(null, null, e.Get<ExpressionSyntax>())).ToArray())
                            ));
                    }
                    return;
                }
                else
                {
                    var cvl = context.Parent as XP.ClassVarListContext;
                    if (initExpr == null)
                    {
                        var arrayType = _syntaxFactory.ArrayType(varType, context.ArraySub.Get<ArrayRankSpecifierSyntax>());
                        initExpr = GenerateDimArrayInitExpression(arrayType, context.ArraySub );
                    }
                }
            }
            if (context.Initializer != null)
            {
                context.Initializer.SetSequencePoint();
            }
            else if (dataType != null && !isDim && candefault)
            {
                // no filtering on InitLocals here. That is done inside GenerateInitializer
                initExpr = GenerateInitializer(dataType);
            }
            context.Put(GenerateVariable(context.Id.Get<SyntaxToken>(), initExpr));
        }

        #endregion

        #region Properties
        public override void ExitProperty([NotNull] XP.PropertyContext context)
        {
            if (context.Multi != null)
                context.SetSequencePoint(context.P, context.Multi.Start);
            else
                context.SetSequencePoint(context.P, context.end);
            var isInInterface = context.isInInterface();
            var isInStruct = context.isInStructure();
            var mods = context.Modifiers?.GetList<SyntaxToken>() ?? DefaultMethodModifiers(isInInterface, context.isInStructure());
            if (context.ExplicitIface != null)
            {
                var m = _pool.Allocate();
                foreach (var mod in mods)
                {
                    if (mod.Kind != SyntaxKind.VirtualKeyword && mod.Kind != SyntaxKind.OverrideKeyword && mod.Kind != SyntaxKind.PublicKeyword)
                        m.Add(mod);
                }
                mods = m.ToList<SyntaxToken>();
                _pool.Free(m);
            }
            var isExtern = mods.Any((int) SyntaxKind.ExternKeyword);
            var type = getReturnType(context);
            type.XVoDecl = true;
            var atts = getAttributes(context.Attributes);
            var id = context.Id.Get<SyntaxToken>();
            // check for a property with a just a set accessor and no get accessor
            // when its body is empty then we cannot generate a property so we have to add
            // the get accessor as well
            if (!isInInterface)
            {
                if (context._LineAccessors?.Count == 1)
                {
                    var accessor = context._LineAccessors[0];
                    if (accessor.Key.Type == XSharpLexer.SET && accessor.ExprList == null)
                    {
                        // create a Get Accessor
                        var newaccessor = new XP.PropertyLineAccessorContext(context, 0);
                        newaccessor.CopyFrom(accessor);
                        newaccessor.Key = new XSharpToken(XSharpLexer.GET, "GET");
                        var decl = _syntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration,
                            attributeLists: EmptyList<AttributeListSyntax>(),
                            modifiers: EmptyList<SyntaxToken>(),
                            keyword: SyntaxFactory.Identifier("get"),
                            body: null,
                            expressionBody: null,
                            semicolonToken: SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken));
                        decl = decl.WithAdditionalDiagnostics(new SyntaxDiagnosticInfo(ErrorCode.WRN_GeneratingGetAccessor));
                        newaccessor.Put(decl);
                        context._LineAccessors.Add(newaccessor);
                    }

                }
            }

            var accessorList = _syntaxFactory.AccessorList(SyntaxFactory.MakeToken(SyntaxKind.OpenBraceToken),
                        (context.Auto != null) ?
                            ((context._AutoAccessors?.Count ?? 0) > 0) ? MakeList<AccessorDeclarationSyntax>(context._AutoAccessors) :
                            MakeList(_syntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration, EmptyList<AttributeListSyntax>(), EmptyList<SyntaxToken>(),
                                    SyntaxFactory.MakeToken(SyntaxKind.GetKeyword), null, null, SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken)),
                                _syntaxFactory.AccessorDeclaration(SyntaxKind.SetAccessorDeclaration, EmptyList<AttributeListSyntax>(), EmptyList<SyntaxToken>(),
                                    SyntaxFactory.MakeToken(SyntaxKind.SetKeyword), null, null, SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken))) :
                        ((context._LineAccessors?.Count ?? 0) > 0) ? MakeList<AccessorDeclarationSyntax>(context._LineAccessors) :
                        MakeList<AccessorDeclarationSyntax>(context._Accessors),
                        SyntaxFactory.MakeToken(SyntaxKind.CloseBraceToken));
            var explicitif = context.ExplicitIface == null ? null : _syntaxFactory.ExplicitInterfaceSpecifier(
                        name: context.ExplicitIface.Get<NameSyntax>(),
                        dotToken: SyntaxFactory.MakeToken(SyntaxKind.DotToken));

            // check the accessor list. if none of the Gets/Sets have a body then generate a warning
            var emulateAuto = false;
            if (context.Auto == null && context.Multi == null  && !isInInterface && !mods.Any((int)SyntaxKind.AbstractKeyword))
            {
                // single line property
                var hasBody = false;
                foreach (var accessor in accessorList.Accessors)
                {
                    if (accessor.Body != null && accessor.Body.Statements.Count > 0)
                    {
                        hasBody = true;
                        break;
                    }
                    else if (accessor.ExpressionBody != null)
                    {
                        hasBody = true;
                        break;
                    }
                }
                
                if (!hasBody && ! isExtern)
                {
                    emulateAuto = true;
                    accessorList = accessorList.WithAdditionalDiagnostics(new SyntaxDiagnosticInfo(ErrorCode.WRN_GetSetMustHaveBody));
                }
            }
            if (context.ParamList == null || context.ParamList._Params.Count == 0)
            {
                var initValue = context.Initializer.Get<ExpressionSyntax>();
                if (initValue == null && (context.Auto != null || emulateAuto) && ! isInStruct)
                {
                	// no filtering on InitLocals here. That is done inside GenerateInitializer
            		initValue = GenerateInitializer(context.Type);
                }
                var initializer = initValue == null ? null :
                    _syntaxFactory.EqualsValueClause(SyntaxFactory.MakeToken(SyntaxKind.EqualsToken), initValue) ;

                MemberDeclarationSyntax propertydecl = _syntaxFactory.PropertyDeclaration(
                     attributeLists: atts,
                     modifiers: mods,
                     type: type,
                     explicitInterfaceSpecifier: explicitif,
                     identifier: id,
                     accessorList: accessorList,
                     expressionBody: null,
                     initializer: initializer,
                     semicolonToken: SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken));
                context.Put(propertydecl);
            }
            else
            {
                if (context.Auto != null)
                    context.AddError(new ParseErrorData(context.Auto, ErrorCode.ERR_SyntaxError, SyntaxFactory.MakeToken(SyntaxKind.GetKeyword)));
                MemberDeclarationSyntax indexer = _syntaxFactory.IndexerDeclaration(
                    attributeLists: atts,
                    modifiers: mods,
                    type: type,
                    explicitInterfaceSpecifier: explicitif,
                    thisKeyword: SyntaxFactory.MakeToken(SyntaxKind.ThisKeyword, context.Id?.Start.Text ?? ""),
                    parameterList: context.ParamList?.Get<BracketedParameterListSyntax>(),
                    accessorList: accessorList,
                    expressionBody: null, // TODO: (grammar) expressionBody methods
                    semicolonToken: SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken));
                context.Put(indexer);
            }
        }

        protected virtual ExpressionSyntax GenerateInitializer(XP.DatatypeContext datatype)
        {
            if (_options.HasOption(CompilerOption.NullStrings,datatype, PragmaOptions)  && datatype != null)
            {
                var isString = datatype.CsNode is PredefinedTypeSyntax pts
                    && pts.Keyword.Kind == SyntaxKind.StringKeyword;
                if (!isString)
                {
                    var typeText = datatype.GetText().ToLower();
                    isString = typeText == "string" || typeText == "system.string";
                }
                if (isString)
                {
                    var value = GenerateLiteral("");
                    value.XGenerated = true;
                    return value;
                }
            }
            if (_options.HasOption(CompilerOption.InitLocals, datatype, PragmaOptions))
            {
                ExpressionSyntax value;
                if (datatype == null)
                {
                    value = GenerateLiteralNull();
                }
                else
                {
                    value = MakeDefault(datatype.Get<TypeSyntax>());
                }
                value.XGenerated = true;
                return value;
            }
            return null;
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
                attributeLists: getAttributes(context.Attributes),
                modifiers: context.Modifiers?.GetList<SyntaxToken>() ?? EmptyList<SyntaxToken>(),
                keyword: context.Key.SyntaxKeyword(),
                body: null,
                expressionBody: null,
                semicolonToken: SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken)));
        }

        public override void ExitPropertyLineAccessor([NotNull] XP.PropertyLineAccessorContext context)
        {
            bool forceBody = false;
            int type = context.Key.Type;
            var property = context.Parent as XP.PropertyContext;
            var isExtern = property.Modifiers?.EXTERN().Count() > 0;
            var isAbstract = property.Modifiers?.ABSTRACT().Count() > 0;
            if (type == XP.SET)
            {
                if (context.ExprList == null)
                {
                    if (!isExtern && !isAbstract && !property.isInInterface() && property._LineAccessors.Count > 1 &&
                        (property._LineAccessors[0].Expr != null || property._LineAccessors[1].Expr != null))
                    {
                        forceBody = true;
                    }
                }
                else
                {
                    context.ExprList.SetSequencePoint();
                }
            }

            if ((type == XP.GET || type == XP.UDCSEP) && context.Expr != null)
            {
                context.Expr.SetSequencePoint();
            }
            var attList = getAttributes(context.Attributes);
            var mods = context.Modifiers?.GetList<SyntaxToken>() ?? EmptyList<SyntaxToken>();
            var key = context.Key.SyntaxKeyword();
            var kind = context.Key.AccessorKind();
            ArrowExpressionClauseSyntax expressionBody = null;
            BlockSyntax body = null;
            if (type == XP.GET || type == XP.UDCSEP)
            {
                expressionBody = GetExpressionBody(context.Expr);
            }
            else // SET
            {
                if (context.ExprList != null)
                {
                    body = MakeBlock(context.ExprList.GetList<StatementSyntax>());
                }
                else if (forceBody)
                {
                    body = MakeBlock(EmptyList<StatementSyntax>());
                }
            }
            var decl = _syntaxFactory.AccessorDeclaration(kind,attributeLists: attList,modifiers: mods,keyword: key,
                body: body, expressionBody: expressionBody, semicolonToken: SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken));
            context.Put(decl);
        }

        #endregion

        #region Class, Interface and Structure Members
        public virtual ConstructorDeclarationSyntax GenerateDefaultCtor(SyntaxToken id, XP.Class_Context classctx)
        {
            return null;
        }
        protected MemberDeclarationSyntax GenerateClassWrapper(SyntaxToken identifier, MemberDeclarationSyntax member, XP.NameDotContext namedot)
        {
            // This method generates a class wrapper for standalone Methods with a Class Clause
            MemberDeclarationSyntax cls = _syntaxFactory.ClassDeclaration(
                attributeLists: EmptyList<AttributeListSyntax>(),
                modifiers: TokenList(SyntaxKind.PartialKeyword, SyntaxKind.PublicKeyword),
                keyword: SyntaxFactory.MakeToken(SyntaxKind.ClassKeyword),
                identifier: identifier,
                typeParameterList: default(TypeParameterListSyntax),
                baseList: default(BaseListSyntax),
                constraintClauses: default(SyntaxList<TypeParameterConstraintClauseSyntax>),
                openBraceToken: SyntaxFactory.MakeToken(SyntaxKind.OpenBraceToken),
                members: MakeList<MemberDeclarationSyntax>(member),
                closeBraceToken: SyntaxFactory.MakeToken(SyntaxKind.CloseBraceToken),
                semicolonToken: null);
            if (namedot != null)
            {
                cls = AddNameSpaceToMember(namedot, cls);
            }
            return cls;
        }

        public override void EnterConstructor([NotNull] XP.ConstructorContext context)
        {
            context.Data.MustBeVoid = true;
        }


        protected ConstructorInitializerSyntax createInitializer(XP.ConstructorchainContext chain)
        {
            if (chain == null)
                return null;

            else
            {
                var result = _syntaxFactory.ConstructorInitializer(chain.Start.CtorInitializerKind(),
                                            SyntaxFactory.MakeToken(SyntaxKind.ColonToken),
                                            chain.Start.SyntaxKeyword(),
                                            chain.ArgList?.Get<ArgumentListSyntax>() ?? EmptyArgumentList());
                chain.Put(result);
                return result;
            }
                
        }

        protected SyntaxToken getParentId(XSharpParserRuleContext context)
        {
            // assumes we are in a member rule of a class, structure or type
            if (context != null)
            {
                switch (context.Parent.Parent)
                {
                    case XP.Class_Context cls:
                        return cls.Id.Get<SyntaxToken>();
                    case XP.Structure_Context str:
                        return str.Id.Get<SyntaxToken>();
                    case XP.Interface_Context interf:
                        return interf.Id.Get<SyntaxToken>();
                    case XP.FoxclassContext fox:
                        return fox.Id.Get<SyntaxToken>();
                    case XP.XppclassContext xpp:
                        return xpp.Id.Get<SyntaxToken>();
                }
            }
            return null;

        }

        public override void ExitConstructor([NotNull] XP.ConstructorContext context)
        {
            context.SetSequencePoint(context.end);
            var attributes = getAttributes(context.Attributes);
            var parameters = getParameters(context.ParamList);
            var nobody = context.ExpressionBody != null;
            var body = nobody ? null : processEntityBody(context);
            var mods = context.Modifiers?.GetList<SyntaxToken>() ?? TokenListWithDefaultVisibility();
            TypeSyntax returntype = null;
            ImplementClipperAndPSZ(context, ref attributes, ref parameters, ref body, ref returntype);
            // no return statement needed  in CONSTRUCTOR
            // body = AddMissingReturnStatement(body, context.StmtBlk, null);
            bool missingParent = false;
            bool genClass = false;
            var parentId = getParentId(context);
            if (parentId == null && context.ClassId != null)
            {
                parentId = context.ClassId.Get<SyntaxToken>();
                genClass = true;
            }
            if (parentId == null)
            {
                parentId = SyntaxFactory.MakeIdentifier("unknown");
                missingParent = true;
            }
            var expressionBody = GetExpressionBody(context.ExpressionBody);
            var ctor = _syntaxFactory.ConstructorDeclaration(
                attributeLists: attributes,
                modifiers: mods,
                identifier: parentId,
                parameterList: parameters,
                initializer: createInitializer(context.Chain),
                body: body,
                expressionBody: expressionBody,
                semicolonToken: (context.StmtBlk?._Stmts?.Count > 0) ? null : SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken));
            if (context.Chain != null)
            {
                context.Chain.SetSequencePoint();
            }

            if (missingParent)
            {
                ctor = ctor.WithAdditionalDiagnostics(new SyntaxDiagnosticInfo(ErrorCode.ERR_ParserError, "Missing CLASS clause"));
            }
            if (!ctor.ContainsDiagnostics && genClass)
            {
                var cls = GenerateClassWrapper(context.ClassId.Get<SyntaxToken>(), ctor, context.Namespace);
                context.Put(cls);
            }
            else
            {
                context.Put(ctor);
            }
        }

        public override void EnterDestructor([NotNull] XP.DestructorContext context)
        {
            context.Data.MustBeVoid = true;
        }
        public override void ExitDestructor([NotNull] XP.DestructorContext context)
        {
            context.SetSequencePoint(context.end);
            // no return statement needed in DESTRUCTOR
            // body = AddMissingReturnStatement(body, context.StmtBlk, null);
            bool missingParent = false;
            bool genClass = false;
            var nobody = context.ExpressionBody != null;
            var parentId = getParentId(context);
            if (parentId == null && context.ClassId != null)
            {
                parentId = context.ClassId.Get<SyntaxToken>();
                genClass = true;
            }
            if (parentId == null)
            {
                parentId = SyntaxFactory.MakeIdentifier("unknown");
                missingParent = true;
            }
            var body = nobody ? null :processEntityBody(context);
            var expressionBody = GetExpressionBody(context.ExpressionBody);
            var dtor = _syntaxFactory.DestructorDeclaration(
                attributeLists: getAttributes(context.Attributes),
                modifiers: context.Modifiers?.GetList<SyntaxToken>() ?? EmptyList<SyntaxToken>(),
                tildeToken: SyntaxFactory.MakeToken(SyntaxKind.TildeToken),
                identifier: parentId,
                parameterList: EmptyParameterList(),
                body: body,
                expressionBody: expressionBody,
                semicolonToken: (context.StmtBlk != null) ? null : SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken));
            if (missingParent)
            {
                dtor = dtor.WithAdditionalDiagnostics(new SyntaxDiagnosticInfo(ErrorCode.ERR_ParserError, "Missing CLASS clause"));
            }
            if (!dtor.ContainsDiagnostics && genClass)
            {
                var cls = GenerateClassWrapper(context.ClassId.Get<SyntaxToken>(), dtor, context.Namespace);
                context.Put(cls);
            }
            else
            {
                context.Put(dtor);
            }
        }


        public override void EnterPropertyAccessor([NotNull] XP.PropertyAccessorContext context)
        {
            context.Data.MustBeVoid = context.Key.Type == XP.SET;
        }

        public override void ExitPropertyAccessor([NotNull] XP.PropertyAccessorContext context)
        {
            context.SetSequencePoint(context.end);
            var attributes = getAttributes(context.Attributes);
            ParameterListSyntax parameters = null;
            var nobody = context.ExpressionBody != null;
            var body = nobody ? null : processEntityBody(context);
            var expressionBody = GetExpressionBody(context.ExpressionBody);
            TypeSyntax returntype = null;
            ImplementClipperAndPSZ(context, ref attributes, ref parameters, ref body, ref returntype);

            context.Put(_syntaxFactory.AccessorDeclaration(context.Key.AccessorKind(),
                attributeLists: attributes,
                modifiers: context.Modifiers?.GetList<SyntaxToken>() ?? EmptyList<SyntaxToken>(),
                keyword: context.Key.SyntaxKeyword(),
                body: body,
                expressionBody: expressionBody,
                semicolonToken: SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken)));
        }
        public override void EnterMethod([NotNull] XP.MethodContext context)
        {
            context.RealType = context.T.Token.Type;
            context.Data.MustBeVoid = context.RealType == XP.ASSIGN;
        }

        protected bool hasAttribute(SyntaxList<AttributeListSyntax> attributes, string attributeName)
        {
            foreach (AttributeListSyntax al in attributes)
            {
                for (int iAttr = 0; iAttr < al.Attributes.Count; iAttr++)
                {
                    var attr = al.Attributes[iAttr];
                    var name = attr.Name.ToFullString().Trim();
                    var pos = name.LastIndexOf(".");
                    if (pos > 0)
                    {
                        name = name.Substring(pos + 1).Trim();
                    }
                    if (XSharpString.Equals(name, attributeName) ||
                        XSharpString.Equals(name, attributeName + "Attribute"))
                    {
                        return true;
                    }
                }
            }
            return false;
        }
        protected void removeAttribute(XP.AttributesContext attributes, string attributeName)
        {
            bool found = false;
            foreach (var attrblock in attributes._AttrBlk)
            {
                foreach (var attr in attrblock._Attributes)
                {
                    var name = attr.Name.GetText();
                    var pos = name.LastIndexOf(".");
                    if (pos > 0)
                        name = name.Substring(pos + 1).Trim();
                    if (XSharpString.Equals(name, attributeName) ||
                        XSharpString.Equals(name, attributeName + "Attribute"))
                    {
                        attrblock._Attributes.Remove(attr);
                        found = true;
                        break;
                    }
                }
                ExitAttributeBlock(attrblock);
                if (found)
                    break;
            }
            ExitAttributes(attributes);
        }
        protected bool isAccessAssign(int t)
        {
            switch (t)
            {
                case XP.ASSIGN:
                case XP.ACCESS:
                    return true;
                default:
                    return false;
            }
        }

        private XP.IEntityContext GetMethodParent(XP.MethodContext context)
        {
            var parent = context.Parent;
            if (parent is XP.FoxmethodContext fmc)
            {
                var clsmember = fmc.Parent as XP.FoxclassmemberContext;
                var cls = clsmember.Parent as XP.FoxclassContext;
                return cls;
            }
            if (parent is XP.EntityContext)
            {
                return null;
            }
            if (parent is XP.ClassmemberContext cmc)
            {
                if (cmc.Parent is XP.Class_Context cls)
                {
                    return cls;
                }
                if (cmc.Parent is XP.Interface_Context interf_)
                {
                    return interf_;
                }
                if (cmc.Parent is XP.Structure_Context struct_)
                {
                    return struct_;
                }
            }
            return null;
        }

        public override void ExitMethod([NotNull] XP.MethodContext context)
        {
            context.SetSequencePoint(context.T.Start,context.end.Stop);
            var idName = context.Id.Get<SyntaxToken>();
            var isInInterface = context.isInInterface();
            var mods = context.Modifiers?.GetList<SyntaxToken>() ?? DefaultMethodModifiers(isInInterface, context.isInStructure(), context.TypeParameters != null);
            var isExtern = mods.Any((int)SyntaxKind.ExternKeyword);
            var isAbstract = mods.Any((int)SyntaxKind.AbstractKeyword);
            var hasNoBody = isInInterface || isExtern || isAbstract | context.Sig.ExpressionBody != null;
            var expressionBody = GetExpressionBody(context.Sig.ExpressionBody);
            if (_options.Dialect == XSharpDialect.FoxPro)
            {
                var mName = idName.Text;
                if (mName.EndsWith("_ACCESS",StringComparison.OrdinalIgnoreCase))
                {
                    mName = mName.Substring(0, mName.Length - "_ACCESS".Length);
                }
                else if (mName.EndsWith("_ASSIGN", StringComparison.OrdinalIgnoreCase))
                {
                    mName = mName.Substring(0, mName.Length - "_ASSIGN".Length);
                }
                idName = SyntaxFactory.MakeIdentifier(mName);
            }
            bool isAccessAssign = this.isAccessAssign(context.RealType);
            var attributes = getAttributes(context.Attributes);
            bool hasExtensionAttribute = false;
            if (isAccessAssign)
            {
                var vomods = _pool.Allocate();
                vomods.Add(SyntaxFactory.MakeToken(SyntaxKind.PrivateKeyword));
                if (mods.Any((int)SyntaxKind.StaticKeyword))
                    vomods.Add(SyntaxFactory.MakeToken(SyntaxKind.StaticKeyword));
                if (mods.Any((int)SyntaxKind.UnsafeKeyword))
                    vomods.Add(SyntaxFactory.MakeToken(SyntaxKind.UnsafeKeyword));
                mods = vomods.ToList<SyntaxToken>();
                _pool.Free(vomods);
            }
            else
            {
                // Check for Extension Attribute. When found then set the self token for the first parameter
                // and generate the parameters again

                if (context.ParamList?._Params.Count > 0)
                {
                    bool hasSelf = context.ParamList?._Params[0].Self != null;
                    if (attributes.Count > 0 && !hasSelf)
                    {
                        if (hasAttribute(attributes, "Extension"))
                        {
                            hasExtensionAttribute = true;
                            var par = context.ParamList._Params[0];
                            par.Self = new XSharpToken(XSharpLexer.SELF, "SELF");
                            hasSelf = true;
                            par.Modifiers._Tokens.Add(par.Self);
                            ExitParameterDeclMods(par.Modifiers);
                            ExitParameter(par);
                            ExitParameterList(context.ParamList);
                            removeAttribute(context.Attributes, "Extension");
                            attributes = getAttributes(context.Attributes);
                        }
                    }

                    if (hasSelf && !mods.Any((int)SyntaxKind.StaticKeyword))
                    {
                        var mTemp = _pool.Allocate();
                        mTemp.AddRange(mods);
                        mTemp.Add(SyntaxFactory.MakeToken(SyntaxKind.StaticKeyword));
                        mods = mTemp.ToList<SyntaxToken>();
                        _pool.Free(mTemp);
                    }
                }

            }
            if (context.ExplicitIface != null)
            {
                var mTemp = _pool.Allocate();
                foreach (var mod in mods)
                {
                    if (mod.Kind != SyntaxKind.VirtualKeyword &&
                        mod.Kind != SyntaxKind.OverrideKeyword &&
                        mod.Kind != SyntaxKind.PublicKeyword)
                        mTemp.Add(mod);
                }
                mods = mTemp.ToList<SyntaxToken>();
                _pool.Free(mTemp);
            }
            if (!isExtern)
            {
                isExtern = hasDllImport(attributes);
                hasNoBody = hasNoBody || isExtern;
            }
            if (isExtern && !mods.Any((int)SyntaxKind.ExternKeyword))
            {
                // Add Extern Keyword to modifiers
                var m1 = _pool.Allocate();
                m1.AddRange(mods);
                if (!m1.Any((int)SyntaxKind.ExternKeyword))
                    m1.Add(SyntaxFactory.MakeToken(SyntaxKind.ExternKeyword));
                mods = m1.ToList<SyntaxToken>();
                _pool.Free(m1);
            }
            var parameters = getParameters(context.ParamList);
            if (isExtern)
            {
                parameters = UpdateVODLLParameters(parameters);
            }

            var body = hasNoBody ? null : processEntityBody(context);
            var returntype = context.Type?.Get<TypeSyntax>();
            if (returntype == null)
            {
                if (context.RealType == XP.ASSIGN)
                {
                    returntype = VoidType();
                }
                else  // method and access
                {
                    returntype = _getMissingType();
                    returntype.XNode = context;
                }
            }
            else
            {
                returntype.XVoDecl = true;
            }
            var oldbody = body;
            ImplementClipperAndPSZ(context, ref attributes, ref parameters, ref body, ref returntype);
            if (body != oldbody)
            {
                context.StmtBlk.Put(body);
            }
            if (context.RealType == XP.ASSIGN)
            {
                // Assign does not need a return. 
                // So do not add missing returns
                returntype = VoidType();
            }
            else if (context.StmtBlk != null && !hasNoBody)
            {
                body = AddMissingReturnStatement(body, context.StmtBlk, returntype);
            }
            MemberDeclarationSyntax m = _syntaxFactory.MethodDeclaration(
                attributeLists: attributes,
                modifiers: mods,
                returnType: returntype,
                explicitInterfaceSpecifier: context.ExplicitIface == null ? null : _syntaxFactory.ExplicitInterfaceSpecifier(
                    name: context.ExplicitIface.Get<NameSyntax>(),
                    dotToken: SyntaxFactory.MakeToken(SyntaxKind.DotToken)),
                identifier: idName,
                typeParameterList: getTypeParameters(context.TypeParameters),
                parameterList: parameters,
                constraintClauses: getTypeConstraints(context._ConstraintsClauses),
                body: body,
                expressionBody: expressionBody, 
                semicolonToken: (!hasNoBody && context.StmtBlk != null) ? null : SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken));
            if (hasExtensionAttribute)
            {
                m = m.WithAdditionalDiagnostics(new SyntaxDiagnosticInfo(ErrorCode.ERR_ExplicitExtension));
            }
            bool separateMethod = false;
            if (context.ClassId != null)
            {
                bool GenClass = true;
                if (context.isInClass())
                {
                    var parent = GetMethodParent(context);
                    string parentName;
                    if (parent != null)
                    {
                        parentName = parent.Name;
                        string className;
                        className = context.ClassId.GetText();
                        if (context.Namespace != null)
                            className = context.Namespace.GetText() + className;
                        if (XSharpString.Compare(parentName, className) != 0)
                        {
                            m = m.WithAdditionalDiagnostics(
                            new SyntaxDiagnosticInfo(
                                    ErrorCode.ERR_NestedMethodMustHaveSameNameAsParentClass, className, parentName));
                        }
                        else
                            GenClass = false;
                    }
                }

                if (GenClass)
                {
                    MemberDeclarationSyntax mem = null;
                    var ce = new SyntaxClassEntities(_pool);
                    if (isAccessAssign && context.Parent is XP.EntityContext)
                    {
                        var parent = (ParserRuleContext)context.Parent;
                        // replace context in parent with new Class_Context
                        // and add this context as child
                        // This is needed because our code in the XSharpLanguageParser that merges properties expects Class_Context nodes
                        var cls = new XP.Class_Context(parent, 0);
                        cls.Id = context.ClassId;
                        cls.Start = context.Start;
                        cls.Stop = context.Stop;
                        parent.children.Remove(context);
                        parent.AddChild(cls);
                        cls.AddChild(context);
                        cls.Data.Partial = true;
                        context.parent = cls;
                        ce.AddVoPropertyAccessor(context,context.RealType, idName);
                        context.Put(m); // this is needed by GenerateVOProperty
                        var vop = ce.VoProperties.Values.First();
                        var prop = GenerateVoProperty(vop, cls);
                        mem = prop;
                        GlobalEntities.NeedsProcessing = true;
                        m = GenerateClassWrapper(context.ClassId.Get<SyntaxToken>(), mem, context.Namespace);
                        cls.Put(m);
                    }
                    else
                    {
                        mem = m;
                        m = GenerateClassWrapper(context.ClassId.Get<SyntaxToken>(), mem, context.Namespace);
                        context.Put(m);
                    }
                    ce.Free();
                    return;
                }
                else
                {
                    context.Put(m);
                }
            }
            else
            {
                context.Put(m);
            }
            if ( isAccessAssign && ! separateMethod)
            {
                if (context.Data.HasClipperCallingConvention && context.CallingConvention != null)
                {
                    m = m.WithAdditionalDiagnostics(new SyntaxDiagnosticInfo(
                                    ErrorCode.ERR_NoClipperCallingConventionForAccessAssign));
                }
                context.Put(m);
                ClassEntities.Peek().AddVoPropertyAccessor(context, context.RealType, idName);
            }
        }

        public override void ExitMethodtype([NotNull] XP.MethodtypeContext context)
        {
            // nvk: Handled by the method rule
        }

        public override void ExitOperator_([NotNull] XP.Operator_Context context)
        {
            context.SetSequencePoint(context.end);
            var nobody = context.ExpressionBody != null;
            var body = nobody ? null : processEntityBody(context);
            var expressionBody = GetExpressionBody(context.ExpressionBody);
            if (context.Conversion != null)
                context.Put(_syntaxFactory.ConversionOperatorDeclaration(
                    attributeLists: getAttributes(context.Attributes),
                    modifiers: context.Modifiers?.GetList<SyntaxToken>() ?? TokenListWithDefaultVisibility(false, SyntaxKind.StaticKeyword),
                    implicitOrExplicitKeyword: context.Conversion.Get<SyntaxToken>(),
                    operatorKeyword: SyntaxFactory.MakeToken(SyntaxKind.OperatorKeyword),
                    type: getReturnType(context),
                    parameterList: getParameters(context.ParamList),
                    body: body,
                    expressionBody: expressionBody,  
                    semicolonToken: (context.StmtBlk != null) ? null : SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken)));
            else
            {
                SyntaxToken opToken;
                if (context.Operation.Token.Type == XP.GT && context.Gt != null) // right shift
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

                if (context.ParamList?._Params.Count == 1)
                {
                    opToken = context.Operation.Token.SyntaxPrefixOp();
                }
                context.Put(_syntaxFactory.OperatorDeclaration(
                    attributeLists: getAttributes(context.Attributes),
                    modifiers: context.Modifiers?.GetList<SyntaxToken>() ?? TokenListWithDefaultVisibility(false, SyntaxKind.StaticKeyword),
                    returnType: getReturnType(context),
                    operatorKeyword: SyntaxFactory.MakeToken(SyntaxKind.OperatorKeyword),
                    operatorToken: opToken,
                    parameterList: getParameters(context.ParamList),
                    body: body,
                    expressionBody: expressionBody, 
                    semicolonToken: (context.StmtBlk != null) ? null : SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken)));
            }
        }


        public override void ExitOverloadedOps([NotNull] XP.OverloadedOpsContext context)
        {
            switch (context.Token.Type)
            {
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

        #endregion

        #region Generics
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

        public override void ExitTypeparameters([NotNull] XP.TypeparametersContext context)
        {
            var parameters = _pool.AllocateSeparated<TypeParameterSyntax>();
            foreach (var tpCtx in context._TypeParams)
            {
                if (parameters.Count > 0)
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
                attributeLists: getAttributes(context.Attributes),
                varianceKeyword: context.VarianceKeyword?.SyntaxKeyword(),
                identifier: context.Id.Get<SyntaxToken>()));
        }

        public override void ExitTypeparameterconstraintsclause([NotNull] XP.TypeparameterconstraintsclauseContext context)
        {
            var constraints = _pool.AllocateSeparated<TypeParameterConstraintSyntax>();
            foreach (var cCtx in context._Constraints)
            {
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

        #endregion

        #region ClassMember alternatives
        public override void ExitClsctor([NotNull] XP.ClsctorContext context)
        {
            context.SetSequencePoint(context.Member.end);
            context.Put(context.Member.Get<MemberDeclarationSyntax>());
        }
        public override void ExitClsdtor([NotNull] XP.ClsdtorContext context)
        {
            context.SetSequencePoint(context.Member.end);
            context.Put(context.Member.Get<MemberDeclarationSyntax>());
        }
        public override void ExitClsmethod([NotNull] XP.ClsmethodContext context)
        {
            context.SetSequencePoint(context.Member.end);
            context.Put(context.Member.Get<MemberDeclarationSyntax>());
        }

        public override void ExitClsoperator([NotNull] XP.ClsoperatorContext context)
        {
            context.SetSequencePoint(context.Member.end);
            context.Put(context.Member.Get<MemberDeclarationSyntax>());
        }

        public override void ExitClsproperty([NotNull] XP.ClspropertyContext context)
        {
            context.SetSequencePoint(context.Member.end);
            context.Put(context.Member.Get<MemberDeclarationSyntax>());
        }

        public override void ExitClsvars([NotNull] XP.ClsvarsContext context)
        {
            if (context.Member.CsNode != null)
                context.Put(context.Member.Get<MemberDeclarationSyntax>());
        }


        public override void ExitNestedClass([NotNull] XP.NestedClassContext context)
        {
            context.Put(context.Member.Get<MemberDeclarationSyntax>());
        }

        public override void ExitNestedDelegate([NotNull] XP.NestedDelegateContext context)
        {
            context.Put(context.Member.Get<MemberDeclarationSyntax>());
        }

        public override void ExitNestedEnum([NotNull] XP.NestedEnumContext context)
        {
            context.Put(context.Member.Get<MemberDeclarationSyntax>());
        }

        public override void ExitNestedEvent([NotNull] XP.NestedEventContext context)
        {
            context.Put(context.Member.Get<MemberDeclarationSyntax>());
        }

        public override void ExitNestedInterface([NotNull] XP.NestedInterfaceContext context)
        {
           context.Put(context.Member.Get<MemberDeclarationSyntax>());
        }

        public override void ExitNestedStructure([NotNull] XP.NestedStructureContext context)
        {
            context.Put(context.Member.Get<MemberDeclarationSyntax>());
        }

        #endregion

        #region Attributes

        protected AttributeListSyntax MakeAttributeList(AttributeTargetSpecifierSyntax target, SeparatedSyntaxList<AttributeSyntax> attributes)
        {
            return _syntaxFactory.AttributeList(
                        openBracketToken: SyntaxFactory.MakeToken(SyntaxKind.OpenBracketToken),
                        target: target,
                        attributes: attributes,
                        closeBracketToken: SyntaxFactory.MakeToken(SyntaxKind.CloseBracketToken)
                        );
        }
        protected AttributeArgumentListSyntax MakeAttributeArgumentList(SeparatedSyntaxList<AttributeArgumentSyntax> args)
        {
            return _syntaxFactory.AttributeArgumentList(
                        SyntaxFactory.MakeToken(SyntaxKind.OpenParenToken),
                        args,
                        SyntaxFactory.MakeToken(SyntaxKind.CloseParenToken)
                        );
        }

        internal ExpressionSyntax MakeConditional(ExpressionSyntax condition, ExpressionSyntax left, ExpressionSyntax right)
        {
            return _syntaxFactory.ConditionalExpression(
                                       condition,
                                       SyntaxFactory.MakeToken(SyntaxKind.QuestionToken),
                                       left,
                                       SyntaxFactory.MakeToken(SyntaxKind.ColonToken),
                                       right);
        }

        internal SyntaxList<AttributeListSyntax> MakeCompilerGeneratedAttribute(bool lWithGlobalScope = false)
        {
            SyntaxListBuilder<AttributeListSyntax> attributeLists = _pool.Allocate<AttributeListSyntax>();
            GenerateAttributeList(attributeLists, SystemQualifiedNames.CompilerGenerated);
            //if (lWithGlobalScope)
            //{
            //    GenerateAttributeList(attributeLists, SystemQualifiedNames.CompilerGlobalScope);
            //}
            var compilerGenerated = attributeLists.ToList();
            _pool.Free(attributeLists);
            return compilerGenerated;
        }


        public override void ExitAttributes([NotNull] XP.AttributesContext context)
        {
            var attributeLists = _pool.Allocate<AttributeListSyntax>();
            foreach (var attrBlkCtx in context._AttrBlk)
            {
                attributeLists.Add(attrBlkCtx.Get<AttributeListSyntax>());
            }
            context.PutList(attributeLists.ToList());
            _pool.Free(attributeLists);
        }

        public override void ExitAttributeBlock([NotNull] XP.AttributeBlockContext context)
        {
            if (context.String != null && context.CsNode == null)
            {
                // take the string and parse it as an attribute block
                string source = context.String.Text;
                var lexer = XSharpLexer.Create(source, _fileName, _options);
                lexer.Options = _options;
                var tokens = lexer.GetTokenStream();
                var attparser = new XSharpParser(tokens) { Options = _options };
                try
                {
                    attparser.ErrorHandler = new XSharpErrorStrategy();
                    var attblock = attparser.attributeBlock();
                    var walker = new ParseTreeWalker();
                    walker.Walk(this, attblock);
                    var list = attblock.CsNode as AttributeListSyntax;
                    context.Put(list);
                    if (list.Attributes.Count == 0)
                    {
                        context.AddError(new ParseErrorData(context, ErrorCode.ERR_BadAttributeArgument, source));
                    }
                    return;
                }
                catch
                {
                    context.AddError(new ParseErrorData(context, ErrorCode.ERR_BadAttributeArgument, source));
                }
            }
            var attributes = _pool.AllocateSeparated<AttributeSyntax>();
            foreach (var attrCtx in context._Attributes)
            {
                if (attributes.Count > 0)
                {
                    attributes.AddSeparator(SyntaxFactory.MakeToken(SyntaxKind.CommaToken));
                }
                attributes.Add(attrCtx.Get<AttributeSyntax>());
            } 
            context.Put(MakeAttributeList(
                context.Target?.Get<AttributeTargetSpecifierSyntax>(),
                attributes));
            _pool.Free(attributes);
        }

        public override void ExitAttributeTarget([NotNull] XP.AttributeTargetContext context)
        {
            string target = context.Token.Text.ToLower();
            switch (target)
            {
                // Token=(ID | CLASS | CONSTRUCTOR | DELEGATE | ENUM | EVENT | FIELD | INTERFACE | METHOD | PROPERTY  | STRUCT )
                case "parameter":
                    target = "param";
                    goto case "param";
                case "assembly":
                case "genericparameter":
                case "module":
                case "return":
                case "class":
                case "constructor":
                case "delegate":
                case "enum":
                case "event":
                case "field":
                case "interface":
                case "method":
                case "param":
                case "property":
                case "struct":
                    var id = SyntaxFactory.MakeIdentifier(target);
                    context.Put(_syntaxFactory.AttributeTargetSpecifier(id, SyntaxFactory.MakeToken(SyntaxKind.ColonToken)));
                    break;
                default:
                    context.AddError(new ParseErrorData(context, ErrorCode.ERR_UnexpectedToken, context.Token.Text));
                    break;
            }
        }

        public override void ExitAttribute([NotNull] XP.AttributeContext context)
        {
            var arguments = _pool.AllocateSeparated<AttributeArgumentSyntax>();
            if (context._Params != null)
            {
                foreach (var paramCtx in context._Params)
                {
                    if (arguments.Count != 0)
                    {
                        arguments.AddSeparator(SyntaxFactory.MakeToken(SyntaxKind.CommaToken));
                    }
                    arguments.Add(paramCtx.Get<AttributeArgumentSyntax>());
                }
            }
            context.Put(_syntaxFactory.Attribute(
                name: context.Name.Get<NameSyntax>(),
                argumentList: MakeAttributeArgumentList(arguments)));
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
            // process wild cards in the AssemblyVersionAttribute, AssemblyFileVersionAttribute and AssemblyInformationalVersionAttribute
            // [assembly: AssemblyVersionAttribute("1.0.*")]
            // [assembly: AssemblyFileVersionAttribute("1.0.*")]
            // [assembly: AssemblyInformationalVersionAttribute("1.0.*")]
            foreach (var attrCtx in context._Attributes)
            {
                if (attributes.Count > 0)
                {
                    attributes.AddSeparator(SyntaxFactory.MakeToken(SyntaxKind.CommaToken));
                }
                if (context.Target.Token.Text.ToLower() == "assembly")
                {
                    string[] names = {  "AssemblyVersionAttribute","AssemblyVersion",
                                        "AssemblyFileVersionAttribute","AssemblyFileVersion",
                                        "AssemblyInformationalVersionAttribute","AssemblyInformationalVersion"};

                    string sSRef = "System.Reflection.";
                    foreach (var name in names)
                    {
                        string ctxName = attrCtx.Name.GetText();
                        if (XSharpString.Equals(ctxName, name) ||
                            XSharpString.Equals(ctxName, sSRef + name))
                        {
                            // check to see if the attribute has a wild card
                            if (attrCtx._Params.Count == 1 && attrCtx._Params[0].Start.Type == XP.STRING_CONST)
                            {
                                string version = attrCtx._Params[0].Start.Text;
                                if (version.StartsWith("\"") && version.EndsWith("\""))
                                    version = version.Substring(1, version.Length - 2);
                                System.Version vers;
                                if (VersionHelper.TryParseAssemblyVersion(version, allowWildcard: true, version: out vers))
                                {
                                    var arguments = _pool.AllocateSeparated<AttributeArgumentSyntax>();
                                    var newarg = _syntaxFactory.AttributeArgument(null, null, GenerateLiteral(vers.ToString()));
                                    arguments.Add(newarg);

                                    attrCtx.Put(_syntaxFactory.Attribute(attrCtx.Get<AttributeSyntax>().Name,
                                        MakeAttributeArgumentList(arguments)));
                                    _pool.Free(arguments);
                                }
                                if (version.IndexOf('*') > 0 && _options.CommandLineArguments.CompilationOptions.Deterministic)
                                {
                                    var node = attrCtx.CsNode as AttributeSyntax;
                                    node = node.WithAdditionalDiagnostics(new SyntaxDiagnosticInfo(ErrorCode.ERR_InvalidVersionFormatDeterministic));
                                    attrCtx.Put(node);
                                }

                            }
                            break;
                        }

                    }
                }
                attributes.Add(attrCtx.Get<AttributeSyntax>());
            }
            context.Put(MakeAttributeList(
                context.Target.Get<AttributeTargetSpecifierSyntax>(),
                attributes));
            _pool.Free(attributes);
        }

        public override void ExitGlobalAttributeTarget([NotNull] XP.GlobalAttributeTargetContext context)
        {
            var text = context.Token.Text.ToLower();
            if (text != "assembly" && text != "module")
            {
                context.AddError(new ParseErrorData(context, ErrorCode.ERR_UnexpectedToken, context.Token.Text));
            }
            else
            {
                context.Put(_syntaxFactory.AttributeTargetSpecifier(
                    SyntaxFactory.Identifier(text),
                    SyntaxFactory.MakeToken(SyntaxKind.ColonToken)));
            }
        }

        #endregion  

        #region Local Functions
        public override void EnterLocalfuncproc([NotNull] XP.LocalfuncprocContext context)
        {
            if (context.T != null && context.T.Type == XP.PROCEDURE)
            {
                context.Data.MustBeVoid = true;
            }
        }
        public override void ExitLocalfuncproc([NotNull] XP.LocalfuncprocContext context)
        {
            if (context.T != null && context.end != null)
            {
                context.SetSequencePoint(context.T, context.end.Stop);
            }
            var isprocedure = context.T != null && context.T.Type == XP.PROCEDURE;
            SyntaxListBuilder modifiers = _pool.Allocate();
            modifiers.Add(SyntaxFactory.MakeToken(SyntaxKind.PrivateKeyword));
            if (context.Modifiers != null)
            {
                var list = context.Modifiers.GetList<SyntaxToken>();
                modifiers.AddRange(list);
            }
            var mods = modifiers.ToList();
            var parameters = getParameters(context.ParamList);
            var nobody = context.Sig.ExpressionBody != null;
            var body = nobody ? null : processEntityBody(context);
            var expressionBody = GetExpressionBody(context.Sig.ExpressionBody);
            var returntype = isprocedure ? voidType : context.Type.Get<TypeSyntax>();
            returntype.XVoDecl = true;
            var id = context.Id.Get<SyntaxToken>();
            var attributes = EmptyList<AttributeListSyntax>();
            ImplementClipperAndPSZ(context, ref attributes, ref parameters, ref body, ref returntype);
            if (!isprocedure)
            {
                body = AddMissingReturnStatement(body, context.StmtBlk, returntype);
            }
            var localfuncdecl = _syntaxFactory.LocalFunctionStatement(
                modifiers: mods,
                returnType: returntype,
                identifier: id,
                typeParameterList: getTypeParameters(context.TypeParameters),
                parameterList: parameters,
                constraintClauses: getTypeConstraints(context._ConstraintsClauses),
                body: body,
                expressionBody: expressionBody,
                semicolonToken: SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken));
            context.Put(localfuncdecl);
        }
 

        // This is actually a statement rule but it logically belongs to the local functions
        public override void ExitLocalFunctionStmt([NotNull] XP.LocalFunctionStmtContext context)
        {
            context.Put(context.Decl.Get<StatementSyntax>());
        }


        #endregion
        #region Parameters
        private static ParameterListSyntax _emptyParameterList = null;
        protected ParameterListSyntax EmptyParameterList()
        {
            if (_emptyParameterList == null)
            {
                lock (gate)
                {
                    if (_emptyParameterList == null)
                    {

                        _emptyParameterList = _syntaxFactory.ParameterList(
                            SyntaxFactory.MakeToken(SyntaxKind.OpenParenToken),
                            EmptySeparatedList<ParameterSyntax>(),
                            SyntaxFactory.MakeToken(SyntaxKind.CloseParenToken));
                    }
                }

            }
            return _emptyParameterList;
        }

        protected ParameterListSyntax MakeParameterList (IList<ParameterSyntax> parameters)
        {
            if (parameters == null || parameters.Count == 0)
                return EmptyParameterList();
            var @params = _pool.AllocateSeparated<ParameterSyntax>();
            foreach (var param in parameters)
            {
                if (@params.Count > 0)
                    @params.AddSeparator(SyntaxFactory.MakeToken(SyntaxKind.CommaToken));
                @params.Add(param);
            }
            var result = _syntaxFactory.ParameterList(
                SyntaxFactory.MakeToken(SyntaxKind.OpenParenToken),
                @params,
                SyntaxFactory.MakeToken(SyntaxKind.CloseParenToken));
            _pool.Free(@params);
            return result;
        }

        public override void EnterParameterList([NotNull] XP.ParameterListContext context)
        {
            if (context._Params?.Count > 0)
            {
                CurrentEntity.Data.HasFormalParameters = true;
            }
        }
        public override void ExitParameterList([NotNull] XP.ParameterListContext context)
        {
            var @params = _pool.AllocateSeparated<ParameterSyntax>();
            foreach (var paramCtx in context._Params)
            {
                if (@params.Count > 0)
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

        protected virtual TypeSyntax _getParameterType([NotNull] XP.ParameterContext context)
        {
            TypeSyntax type = context.Type?.Get<TypeSyntax>();
            if (type == null)
            {
                type = _getMissingType();
            }
            return type;
        }

        public override void ExitParameter([NotNull] XP.ParameterContext context)
        {
            bool hasParamArrayAttribute = false;
            var attributeList = getAttributes(context.Attributes);

            if (hasAttribute(attributeList, "ParamArray"))
            {
                hasParamArrayAttribute = true;
                removeAttribute(context.Attributes, "ParamArray");
                attributeList = getAttributes(context.Attributes);
                foreach (XSharpToken t in context.Modifiers._Tokens)
                {
                    // replace any AS, IS, REF or OUT with PARAMS. Leave CONST
                    if (t.Type != XP.CONST)
                    {
                        t.Type = XP.PARAMS;
                        t.Text = "PARAMS";
                    }
                }
                ExitParameterDeclMods(context.Modifiers);
            }

            if (context.Ellipsis != null)
            {
                context.Put(_syntaxFactory.Parameter(
                    EmptyList<AttributeListSyntax>(),
                    EmptyList<SyntaxToken>(),
                    null,
                    context.Ellipsis.SyntaxLiteralValue(_options), null));
                return;
            }
            TypeSyntax type = _getParameterType(context);
            type.XVoDecl = true;
            if (context.Modifiers != null && context.Modifiers._Tokens != null)
            {
                foreach (var token in context.Modifiers._Tokens)
                {
                    if (token.Type == XP.IS)
                    {
                        type.XVoIsDecl = true;
                        break;
                    }
                }
            }

            var par = _syntaxFactory.Parameter(
                attributeLists: attributeList,
                modifiers: context.Modifiers?.GetList<SyntaxToken>() ?? EmptyList<SyntaxToken>(),
                type: type,
                identifier: context.Id.Get<SyntaxToken>(),
                @default: context.Default == null ? null : _syntaxFactory.EqualsValueClause(
                    SyntaxFactory.MakeToken(SyntaxKind.EqualsToken),
                    context.Default.Get<ExpressionSyntax>()));

            if (hasParamArrayAttribute)
            {
                par = par.WithAdditionalDiagnostics(new SyntaxDiagnosticInfo(ErrorCode.ERR_ExplicitParamArray));
            }
            context.Put(par);
        }

        #endregion

        #region Modifiers

        protected void HandleDefaultTypeModifiers(XSharpParserRuleContext context, IList<IToken> tokens, bool fixDefault = false)
        {
            SyntaxListBuilder modifiers = _pool.Allocate();
            AddUniqueModifiers(modifiers, tokens, fixDefault, false);
            context.PutList(modifiers.ToList<SyntaxToken>());
            _pool.Free(modifiers);
        }

        private void AddUniqueModifiers(SyntaxListBuilder modifiers, IList<IToken> tokens, bool fixDefault, bool isInInterface)
        {
            foreach (var m in tokens)
            {
                var kw = m.SyntaxKeyword();
                if (isInInterface)
                {
                    if (kw.Kind != SyntaxKind.PublicKeyword)
                        modifiers.AddCheckUnique(kw);
                }
                else
                    modifiers.AddCheckUnique(kw);
            }
            if (fixDefault)
            {
                modifiers.FixDefaultVisibility();
            }
        }
        public override void ExitAccessorModifiers([NotNull] XP.AccessorModifiersContext context)
        {
            SyntaxListBuilder modifiers = _pool.Allocate();
            bool isInInterface = context.isInInterface();
            AddUniqueModifiers(modifiers, context._Tokens, false, isInInterface);
            context.PutList(modifiers.ToList<SyntaxToken>());
            _pool.Free(modifiers);
        }
        public override void ExitClassModifiers([NotNull] XP.ClassModifiersContext context)
        {
            HandleDefaultTypeModifiers(context, context._Tokens, true); 
        }
        public override void ExitClassvarModifiers([NotNull] XP.ClassvarModifiersContext context)
        {
            SyntaxListBuilder modifiers = _pool.Allocate();
            foreach (var m in context._Tokens)
            {
                modifiers.AddCheckUnique(m.SyntaxKeyword());
                if (m.Type == XP.FIXED && context._FIXED == null)
                    context._FIXED = m;
            }
            modifiers.FixDefaultVisibility();
            context.PutList(modifiers.ToList<SyntaxToken>());
            _pool.Free(modifiers);
        }

        public override void ExitConstructorModifiers([NotNull] XP.ConstructorModifiersContext context)
        {
            SyntaxListBuilder modifiers = _pool.Allocate();
            AddUniqueModifiers(modifiers, context._Tokens, false, false);
            if (!modifiers.Any((int)SyntaxKind.StaticKeyword))
                modifiers.FixDefaultVisibility();
            context.PutList(modifiers.ToList<SyntaxToken>());
            _pool.Free(modifiers);
        }



        public override void ExitDestructorModifiers([NotNull] XP.DestructorModifiersContext context)
        {
            HandleDefaultTypeModifiers(context, context._Tokens, true);
        }
        public override void ExitFuncprocModifiers([NotNull] XP.FuncprocModifiersContext context)
        {
            SyntaxListBuilder modifiers = _pool.Allocate();
            AddUniqueModifiers(modifiers, context._Tokens, false, false);
            // STATIC FUNCTION is implemented as INTERNAL and will be moved to a special class
            if (modifiers.Any((int)SyntaxKind.StaticKeyword))
            {
                // in this context static has to do with visibility
                modifiers.Add(SyntaxFactory.MakeToken(SyntaxKind.InternalKeyword));
                context.IsStaticVisible = true;
            }
            else
            {
                // in this context static means that .Net static = class method as opposed to instance method
                context.IsStaticVisible = false;
                modifiers.Add(SyntaxFactory.MakeToken(SyntaxKind.StaticKeyword));
                modifiers.FixDefaultVisibility();
            }
            context.PutList(modifiers.ToList<SyntaxToken>());
            _pool.Free(modifiers);
        }

 
        public override void ExitLocalfuncprocModifiers([NotNull] XP.LocalfuncprocModifiersContext context)
        {
            SyntaxListBuilder modifiers = _pool.Allocate();
            AddUniqueModifiers(modifiers, context._Tokens, false, false);
            context.PutList(modifiers.ToList<SyntaxToken>());
            _pool.Free(modifiers);
        }
        public override void ExitMemberModifiers([NotNull] XP.MemberModifiersContext context)
        {
            SyntaxListBuilder modifiers = _pool.Allocate();
            bool genericParent = false;
            // Membermodifiers are used for Methods and Properties
            if (context.Parent is XP.MethodContext mc)
            {
                genericParent = mc.TypeParameters != null || mc._ConstraintsClauses.Count > 0;
            }
            bool isInInterface = context.isInInterface();
            AddUniqueModifiers(modifiers, context._Tokens, false, isInInterface);
            if (!isInInterface)
            {
                modifiers.FixDefaultVisibility();
                if (_options.VirtualInstanceMethods && !context.isInStructure())
                    modifiers.FixDefaultVirtual();
                else if (!genericParent)
                {
                    modifiers.FixDefaultMethod();
                }
            }
            context.PutList(modifiers.ToList<SyntaxToken>());
            _pool.Free(modifiers);
        }

        public override void ExitOperatorModifiers([NotNull] XP.OperatorModifiersContext context)
        {
            SyntaxListBuilder modifiers = _pool.Allocate();
            AddUniqueModifiers(modifiers, context._Tokens, false, false);
            if (!modifiers.Any((int)SyntaxKind.StaticKeyword))
                modifiers.Add(SyntaxFactory.MakeToken(SyntaxKind.StaticKeyword));
            modifiers.FixDefaultVisibility();
            context.PutList(modifiers.ToList<SyntaxToken>());
            _pool.Free(modifiers);
        }
         
        public override void ExitParameterDeclMods([NotNull] XP.ParameterDeclModsContext context)
        {
            SyntaxListBuilder modifiers = _pool.Allocate();
            foreach (var m in context._Tokens)
            {
                if (m.Type != XP.AS && m.Type != XP.IS)
                    modifiers.AddCheckUnique(m.SyntaxKeyword());
            }
            //modifiers.FixDefaultVisibility();
            context.PutList(modifiers.ToList<SyntaxToken>());
            _pool.Free(modifiers);
        }

  


        public override void ExitVotypeModifiers([NotNull] XP.VotypeModifiersContext context)
        {
            IList<IToken> tokens = context._Tokens;
            if (tokens != null)
            {
                foreach (XSharpToken t in tokens)
                {
                    if (t.Type == XSharpLexer.STATIC)
                    {
                        t.Type = XSharpLexer.INTERNAL;
                        t.Text = "INTERNAL";
                    }
                }
            }

            HandleDefaultTypeModifiers(context, tokens, true); 
        }

        SyntaxList<SyntaxToken> GetFuncProcModifiers(XP.FuncprocModifiersContext context, bool isExtern, bool isInInterface)
        {
            if (isExtern)
                return context?.GetList<SyntaxToken>() ?? TokenListWithDefaultVisibility(isInInterface, SyntaxKind.StaticKeyword, SyntaxKind.ExternKeyword);
            else
                return context?.GetList<SyntaxToken>() ?? TokenListWithDefaultVisibility(isInInterface, SyntaxKind.StaticKeyword);

        }

        #endregion
        #region Functions and Procedures
        public override void EnterFuncproc([NotNull] XP.FuncprocContext context)
        {
            if (context.T != null && context.T.Type == XP.PROCEDURE )
            {
                context.Data.MustBeVoid = _options.Dialect != XSharpDialect.FoxPro ;
            }
            string name = context.Id.GetText();
            context.Data.IsEntryPoint = XSharpString.Equals(name, _entryPoint)
                    && _options.CommandLineArguments.CompilationOptions.OutputKind.IsApplication();
            if (context.Data.IsEntryPoint && context.Type == null)
            {
                context.Data.MustBeVoid = true;
            }
        }

        private void handleInitExit([NotNull] XP.FuncprocContext context)
        {
            if (context.InitExit != null)
            {
                int level = 0;
                switch (context.InitExit.Type)
                {
                    case XP.INIT1:
                        level = 1;
                        break;
                    case XP.INIT2:
                        level = 2;
                        break;
                    case XP.INIT3:
                        level = 3;
                        break;
                    case XP.EXIT:
                        level = -1;
                        break;
                }
                GlobalEntities.InitProcedures.Add(new Tuple<int, string>(level, context.Id.GetText()));
                context.Data.IsInitProcedure = true;
                context.Data.HasClipperCallingConvention = false;
            }
        }

        protected virtual ArrowExpressionClauseSyntax GetExpressionBody([NotNull] XP.ExpressionContext context)
        {
            ArrowExpressionClauseSyntax expressionBody = null;
            if (context!= null)
            {
                expressionBody = _syntaxFactory.ArrowExpressionClause(
                    SyntaxFactory.MakeToken(SyntaxKind.EqualsGreaterThanToken),
                    context.Get<ExpressionSyntax>());
            }
            return expressionBody;
        }

        public override void ExitFuncproc([NotNull] XP.FuncprocContext context)
        {
            if (context.T != null && context.end  != null)
            {
                context.SetSequencePoint(context.T, context.end.Stop);
            }
            // FUNCTION and PROCEDURE for FoxPro are implemented in the Method Rule, so FUNCTION is never inside an interface

            var isprocedure = context.T != null && context.T.Type == XP.PROCEDURE;
            if (isprocedure)
            {
                handleInitExit(context);
            }

            var isInInterface = false;      
            var attributes = getAttributes(context.Attributes);
            bool isextern = hasDllImport(attributes);
            var modifiers = GetFuncProcModifiers(context.Modifiers, isextern, isInInterface);
            if (!isextern)
            {
                isextern = modifiers.Any((int)SyntaxKind.ExternKeyword);
            }
            var hasnobody = (isInInterface || isextern || context.Sig.ExpressionBody != null);
            var parameters = getParameters(context.ParamList);
            if (isextern)
            {
                parameters = UpdateVODLLParameters(parameters);
            }
            var body = hasnobody ? null : processEntityBody(context);
            var expressionBody =  GetExpressionBody(context.Sig.ExpressionBody);
            var returntype = getReturnType(context);
            if (isprocedure)
            {
                if (_options.Dialect != XSharpDialect.FoxPro)
                {
                    if (context.Type != null && context.Type.GetText().ToLower() != "void")
                    {
                        returntype = NotInDialect(returntype, "Procedure with non VOID return type");
                    }
                    else
                    {
                        returntype = voidType;
                    }
                }
                else if (context.Data.IsEntryPoint)
                {
                    returntype = voidType;
                }
            }

            returntype.XVoDecl = true;
            var id = context.Id.Get<SyntaxToken>();

            if (!hasnobody)
            {
                ImplementClipperAndPSZ(context, ref attributes, ref parameters, ref body, ref returntype);
                // Functions and FoxPro procedures must have a return value
                bool addreturn = !isprocedure;
                if (_options.Dialect == XSharpDialect.FoxPro && isprocedure)
                {
                    addreturn = !context.Data.IsEntryPoint;
                }
                if (addreturn)
                {
                    body = AddMissingReturnStatement(body, context.StmtBlk, returntype);
                }
                // Special Handling of EntryPoint
                if (context.Data.IsEntryPoint)
                {
                    body = GenerateEntryPoint(modifiers, context, body, attributes, parameters);
                }
            }
            context.Put(_syntaxFactory.MethodDeclaration(
                attributeLists: attributes,
                modifiers: modifiers,
                returnType: returntype,
                explicitInterfaceSpecifier: null,
                identifier: id,
                typeParameterList: getTypeParameters(context.TypeParameters),
                parameterList: parameters,
                constraintClauses: getTypeConstraints(context._ConstraintsClauses),
                body: body,
                expressionBody: expressionBody,
                semicolonToken: (body != null) ? null : SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken)));

        }
     

        public override void ExitCallingconvention([NotNull] XP.CallingconventionContext context)
        {
            // TODO nvk (calling convention is silently ignored for now)
        }

        #endregion

        #region VO Compatible Global types and fields


        public override void ExitVodefine([NotNull] XP.VodefineContext context)
        {
            context.SetSequencePoint(context.D, context.end);
            var variables = _pool.AllocateSeparated<VariableDeclaratorSyntax>();
            var expr = context.Expr.Get<ExpressionSyntax>();
            variables.Add(GenerateVariable(context.Id.Get<SyntaxToken>(), MakeChecked(expr,false)));
            var isConst = false;
            // always process the expression to determine if it is a const
            var type = GetExpressionType(context.Expr, ref isConst);
            if (context.DataType != null)
            {
                type = context.DataType?.Get<TypeSyntax>();
                isConst = false;
            }
            var list = _pool.Allocate();
            SyntaxList<SyntaxToken> modifiers = null;
            if (context.Modifiers != null)
            {
                // We are not including the "STATIC" modifier here. If there is any diagnostics attached
                // to this modifier, we will attach that to the Const Keyword that we are adding
                // This may happen if we have STATIC STATIC DEFINE Foo := 123 AS LONG
                DiagnosticInfo[] diags = null;
                foreach (var m in context.Modifiers.GetList<SyntaxToken>())
                {
                    if (m.Kind != SyntaxKind.StaticKeyword)
                        list.Add(m);
                    else if (m.ContainsDiagnostics)
                        diags = m.GetDiagnostics();
                }
                if (isConst)
                {
                    var token = SyntaxFactory.MakeToken(SyntaxKind.ConstKeyword);
                    if (diags != null)
                        token = token.WithAdditionalDiagnostics(diags);
                    list.Add(token);
                }
                else
                {
                    var token = SyntaxFactory.MakeToken(SyntaxKind.ReadOnlyKeyword);
                    if (diags != null)
                        token = token.WithAdditionalDiagnostics(diags);
                    list.Add(token);
                    token = SyntaxFactory.MakeToken(SyntaxKind.StaticKeyword);
                    list.Add(token);
                }
                modifiers = list.ToList<SyntaxToken>();
                _pool.Free(list);
            }
            else
            {
                // Will be a public const
                if (isConst)
                {
                    modifiers = TokenList(SyntaxKind.PublicKeyword, SyntaxKind.ConstKeyword);
                }
                else
                {
                    // public readonly static.
                    // if we are lucky we may change this to a const inside SourceMemberSymbol.cs later if the 
                    // initializer expression gets folded to a const
                    // such as in
                    // DEFINE Foo := 0
                    // DEFINE Bar := Foo +1
                    modifiers = TokenList(SyntaxKind.PublicKeyword, SyntaxKind.ReadOnlyKeyword, SyntaxKind.StaticKeyword);
                }
            }
            var field = _syntaxFactory.FieldDeclaration(
                EmptyList<AttributeListSyntax>(),
                modifiers,
                _syntaxFactory.VariableDeclaration(type, variables),
                SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken));
            context.Put(field);
            _pool.Free(variables);
            GlobalEntities.Globals.Add(field);
        }


        public override void EnterVoglobal([NotNull] XP.VoglobalContext context)
        {
            if (context.Const != null)
            {
                if (context.Modifiers != null)
                    context.Modifiers._Tokens.Add(context.Const);
                else
                {
                    context.Modifiers = FixPosition(new XP.FuncprocModifiersContext(context, 0), context.Start);
                    context.Modifiers.PutList(TokenList(SyntaxKind.ConstKeyword, SyntaxKind.PublicKeyword));
                }
            }
        }

        public override void ExitVoglobal([NotNull] XP.VoglobalContext context)
        {
            var varList = _pool.AllocateSeparated<VariableDeclaratorSyntax>();
            var varType = getDataType(context.Vars.DataType);
            context.SetSequencePoint(context.end);
            varType.XVoDecl = true;
            if (context.Vars?.As?.Type == XP.IS)
            {
                varType.XVoIsDecl = true;
            }
            SyntaxList<SyntaxToken> mods;
            if (context.Static == null)
            { 
                mods = context.Modifiers?.GetList<SyntaxToken>() ?? TokenListWithDefaultVisibility(false, SyntaxKind.StaticKeyword);
            }
            else
            {
                mods = TokenListWithDefaultVisibility(false, SyntaxKind.StaticKeyword,SyntaxKind.InternalKeyword);
            }

            foreach (var varCtx in context.Vars._Var)
            {
                bool isDim = varCtx.Dim != null && varCtx.ArraySub != null;
                if (isDim)
                {
                    var global = _syntaxFactory.FieldDeclaration(
                        attributeLists: getAttributes(context.Attributes),
                        modifiers: mods,
                        declaration: _syntaxFactory.VariableDeclaration(
                            type: _syntaxFactory.ArrayType(varType, MakeArrayRankSpecifier(varCtx.ArraySub._ArrayIndex.Count)),
                            variables: MakeSeparatedList(varCtx.Get<VariableDeclaratorSyntax>())),
                        semicolonToken: SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken));
                    context.Put(global);
                    GlobalEntities.Globals.Add(global);
                }
                else
                {
                    if (varList.Count > 0)
                        varList.AddSeparator(SyntaxFactory.MakeToken(SyntaxKind.CommaToken));
                    varList.Add(varCtx.Get<VariableDeclaratorSyntax>());
                }
            }
            if (varList.Count > 0)
            {
                var global = _syntaxFactory.FieldDeclaration(
                    attributeLists: getAttributes(context.Attributes),
                    modifiers: mods,
                    declaration: _syntaxFactory.VariableDeclaration(
                        type: varType,
                        variables: varList),
                    semicolonToken: SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken));
                context.Put(global);
                GlobalEntities.Globals.Add(global);
            }
            _pool.Free(varList);
        }


        public override void EnterVodll([NotNull] XP.VodllContext context)
        {
            // No need to set the MustBeVoid flag. _DLL PROCEDURE  has no statement list
            //context.Data.MustBeVoid = context.T.Type == XP.PROCEDURE;
            if (context.Modifiers != null)
            {
                context.Modifiers._Tokens.Add(_parser.TokenFactory.Create(XP.EXTERN, ""));
            }
        }


        internal virtual ParameterListSyntax UpdateVODLLParameters(ParameterListSyntax parameters)
        {
            // real work implemented in the subclass to check for PSZ parameters
            return parameters;
        }

        protected AttributeSyntax _unmanagedCodeAttribute()
        {
            var attrargs = _pool.AllocateSeparated<AttributeArgumentSyntax>();
            var attrib = _syntaxFactory.Attribute(
                                name: GenerateQualifiedName("System.Security.SuppressUnmanagedCodeSecurityAttribute"),
                                argumentList: MakeAttributeArgumentList(attrargs));
            _pool.Free(attrargs);
            return attrib;

        }
        private AttributeSyntax _dllImportAttribute(XP.VodllContext context, ExpressionSyntax dllExpr, ExpressionSyntax entrypointExpr)
        {
            AttributeArgumentSyntax charset;
            SyntaxToken id;
            if (context.CharSet != null)
            {
                // convert to 1 Upper char and lower chars
                string cs = context.CharSet.Text;
                cs = cs.Substring(0, 1).ToUpper() + cs.Substring(1).ToLower();
                id = SyntaxFactory.Identifier(cs);
            }
            else
            {
                id = SyntaxFactory.Identifier("Auto");
            }
            charset = _syntaxFactory.AttributeArgument(GenerateNameEquals("CharSet"), null,
                             MakeSimpleMemberAccess(GenerateQualifiedName(SystemQualifiedNames.CharSet),
                                  _syntaxFactory.IdentifierName(id)));
            var attribs = new List<AttributeArgumentSyntax>() { _syntaxFactory.AttributeArgument(null, null, dllExpr), charset };
            if (entrypointExpr != null)
            {
                attribs.Add(_syntaxFactory.AttributeArgument(GenerateNameEquals("EntryPoint"), null, entrypointExpr));
                attribs.Add(_syntaxFactory.AttributeArgument(GenerateNameEquals("ExactSpelling"), null, GenerateLiteral(true)));
            }
            else
            {
                attribs.Add(_syntaxFactory.AttributeArgument(GenerateNameEquals("ExactSpelling"), null, GenerateLiteral(false)));
            }
            attribs.Add(_syntaxFactory.AttributeArgument(GenerateNameEquals("SetLastError"), null, GenerateLiteral(true)));
            if (context.CallingConvention != null)
                attribs.Add(context.CallingConvention.Get<AttributeArgumentSyntax>());
            return _syntaxFactory.Attribute(
                name: GenerateQualifiedName(SystemQualifiedNames.DllImport),
                argumentList: MakeAttributeArgumentList(MakeSeparatedList(attribs.ToArray())));

        }
        public override void ExitVodll([NotNull] XP.VodllContext context)
        {
            // todo: declare and process attributes 
            string dllName = context.Dll.GetText();
            if (context.Extension != null)
            {
                dllName += "." + context.Extension.GetText();
            }
            ExpressionSyntax dllExpr = GenerateLiteral(dllName);
            ExpressionSyntax entrypointExpr;
            string entrypoint = context.Entrypoint.GetText();

            // the whole string from entrypointExpr - @int is the entrypoint
            if (context.Address != null && context.Number != null)
            {
                entrypoint = entrypoint + context.Address.Text + context.Number.Text;
            }
            entrypointExpr = GenerateLiteral(entrypoint);

            var returnType = context.Type?.Get<TypeSyntax>() ?? (context.T.Type == XP.FUNCTION ? _getMissingType() : VoidType());
            returnType.XVoDecl = true;

            var parameters = getParameters(context.ParamList);
            parameters = UpdateVODLLParameters(parameters);
            var modifiers = context.Modifiers?.GetList<SyntaxToken>() ?? TokenListWithDefaultVisibility(false, SyntaxKind.StaticKeyword, SyntaxKind.ExternKeyword);
            var attributes = MakeSeparatedList(_unmanagedCodeAttribute(),
                                                _dllImportAttribute(context, dllExpr, entrypointExpr)
                                              );
           
            var attList = MakeList(MakeAttributeList(target: null, attributes: attributes));

            context.Put(_syntaxFactory.MethodDeclaration(
                attributeLists: attList,
                modifiers: modifiers,
                returnType: returnType,
                explicitInterfaceSpecifier: null,
                identifier: context.Id.Get<SyntaxToken>(),
                typeParameterList: null,
                parameterList: parameters,
                constraintClauses: default(SyntaxList<TypeParameterConstraintClauseSyntax>),
                body: null,
                expressionBody: null,
                semicolonToken: SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken)));
        }

        public override void ExitDllcallconv([NotNull] XP.DllcallconvContext context)
        {
            string conv = null;
            switch (context.Cc.Type)
            {
                case XP.CLIPPER:
                case XP.STRICT:
                case XP.ASPEN:
                    conv = SystemQualifiedNames.Cdecl;
                    break;
                case XP.PASCAL:
                case XP.WINCALL:
                case XP.CALLBACK:
                    conv = null; // set no attribute at all.
                    break;
                case XP.THISCALL:
                    conv = SystemQualifiedNames.ThisCall; ;
                    break;
                case XP.FASTCALL:
                    conv = SystemQualifiedNames.Cdecl;
                    break;
            }
            if (!String.IsNullOrEmpty(conv))
            {
                context.Put(_syntaxFactory.AttributeArgument(
                    GenerateNameEquals("CallingConvention"),
                    null,
                    GenerateQualifiedName(conv)));
            }
        }


        protected bool IsStringType(TypeSyntax type)
        {
            if (type is PredefinedTypeSyntax)
            {
                var pretype = type as PredefinedTypeSyntax;
                switch (pretype.keyword.Kind)
                {
                    case SyntaxKind.StringKeyword:
                        return true;
                    default:
                        return false;
                }
            }
            else
            {
                if (type is QualifiedNameSyntax)
                {
                    var qns = type as QualifiedNameSyntax;
                    // System.Void
                    var sName = qns.ToFullString().Replace(" ", "");
                    if (sName.Equals("System.String", XSharpString.Comparison))
                    {
                        return true;
                    }
                    // global::System.Void
                    if (sName.Equals("global::System.String", XSharpString.Comparison))
                    {
                        return true;
                    }
                }
            }
            return false;
        }
        protected bool isVoidType(TypeSyntax type)
        {
            if (type is PredefinedTypeSyntax)
            {
                var pretype = type as PredefinedTypeSyntax;
                switch (pretype.keyword.Kind)
                {
                    case SyntaxKind.VoidKeyword:
                        return true;
                    default:
                        return false;
                }
            }
            else
            {
                if (type is QualifiedNameSyntax)
                {
                    var qns = type as QualifiedNameSyntax;
                    // System.Void
                    var sName = qns.ToFullString().Replace(" ", "");
                    var v1 = GenerateQualifiedName(SystemQualifiedNames.Void1).ToFullString().Replace(" ", "");
                    var v2 = GenerateQualifiedName(SystemQualifiedNames.Void2).ToFullString().Replace(" ", "");
                    if (sName.Equals(v1, XSharpString.Comparison))
                    {
                        return true;
                    }
                    // global::System.Void
                    if (sName.Equals(v2, XSharpString.Comparison))
                    {
                        return true;
                    }
                }

            }
            return false;
        }

        protected virtual BlockSyntax GenerateEntryPoint(SyntaxList<SyntaxToken> modifiers, [NotNull] XP.IEntityContext context, BlockSyntax body,
                    SyntaxList<AttributeListSyntax> attributeList, ParameterListSyntax parList)
        {
            // In the core dialect GenerateEntryPoint does nothing special
            // In other dialects the body of the start function will be changed
            // and an additional function may be generated
            return body;
        }


        protected bool hasDllImport(SyntaxList<AttributeListSyntax> attributes)
        {
            if (attributes != null && attributes.Count > 0)
            {
                foreach (AttributeListSyntax al in attributes)
                {
                    var ats = al.Attributes;
                    for (int i = 0; i < ats.Count; i++)
                    {
                        var at = ats[i] as AttributeSyntax;
                        if (at.ToString().ToLower().Contains("dllimport"))
                        {
                            return true;
                        }
                    }

                }
            }
            return false;
        }

        #endregion
        #region Statements


        #region Declaration Statements

        public override void ExitDeclarationStmt([NotNull] XP.DeclarationStmtContext context)
        {
            context.PutList(context.Decl.GetList<StatementSyntax>());
        }

        public override void EnterCommonLocalDecl([NotNull] XP.CommonLocalDeclContext context)
        {
            XP.DatatypeContext t = null;
            for (var i = context._LocalVars.Count - 1; i >= 0; i--)
            {
                var locCtx = context._LocalVars[i];
                if (locCtx.DataType != null)
                    t = locCtx.DataType;
                else if (t != null)
                    locCtx.DataType = t;
            }
        }

        public override void ExitCommonLocalDecl([NotNull] XP.CommonLocalDeclContext context)
        {
            context.SetSequencePoint();
            foreach (var lvCtx in context._LocalVars)
                VisitLocalvar(lvCtx);
            context.PutList(MakeList<StatementSyntax>(context._LocalVars));
        }

        public override void ExitVarLocalDecl([NotNull] XP.VarLocalDeclContext context)
        {
            context.SetSequencePoint();
            context.PutList(MakeList<StatementSyntax>(context._ImpliedVars));
        }


        public override void ExitLocalvar([NotNull] XP.LocalvarContext context)
        {
            // nvk: Do nothing here. It will be handled by the visitor after Datatype(s) are processed.
            context.SetSequencePoint();
        }

        protected virtual TypeSyntax _getMissingType()
        {
            return MissingType();
        }

        protected ExpressionSyntax GenerateDimArrayInitExpression(ArrayTypeSyntax arrayType, XP.ArraysubContext sub)
        {
            InitializerExpressionSyntax init = null;
            var dims = _syntaxFactory.ArrayCreationExpression(SyntaxFactory.MakeToken(SyntaxKind.NewKeyword),arrayType, null);
            bool isstring = false;
            if (_options.HasOption(CompilerOption.NullStrings, sub, PragmaOptions) && arrayType.ElementType is PredefinedTypeSyntax pdt)
            {
                isstring = pdt.Keyword.Kind == SyntaxKind.StringKeyword;

            }
            if (!isstring)
                return dims;
            if (sub._ArrayIndex.Count == 1)
            {
                // for single dim indexes we inline the initialization
                var l = _pool.AllocateSeparated<ExpressionSyntax>();
                int dimensions = Int32.Parse(sub._ArrayIndex[0].GetText());
                for (int i = 0; i < dimensions; i++)
                {
                    if (i > 0)
                    {
                        l.AddSeparator(SyntaxFactory.MakeToken(SyntaxKind.CommaToken));
                    }
                    l.Add(GenerateLiteral(""));
                }
                init = _syntaxFactory.InitializerExpression(SyntaxKind.ArrayInitializerExpression,
                    SyntaxFactory.MakeToken(SyntaxKind.OpenBraceToken),
                    l.ToList(),
                    SyntaxFactory.MakeToken(SyntaxKind.CloseBraceToken));
                _pool.Free(l);
                return _syntaxFactory.ArrayCreationExpression(SyntaxFactory.MakeToken(SyntaxKind.NewKeyword), arrayType, init);
            }
            // multi dim is not supported yet in X# runtime.
            // Vulcan has StringArrayInit, but that does not return a string, so we have to generate a special function for this
            // this function creates the array, initializes it and returns it.
            // StringArrayInit has no return type because it can handle arrays of different dimensions
            // 
            var funcname = "$StringArrayInit" + UniqueNameSuffix(sub);
            var stmts = _pool.Allocate<StatementSyntax>();
            StatementSyntax stmt = GenerateLocalDecl(XSharpSpecialNames.ArrayName, arrayType, dims);
            stmt.XNode = sub.Parent as XSharpParserRuleContext;
            stmt.XGenerated = true;
            stmts.Add(stmt);

            var varname = GenerateSimpleName(XSharpSpecialNames.ArrayName);
            var args = MakeArgumentList(MakeArgument(varname));
            stmt = GenerateExpressionStatement(GenerateMethodCall(
                _options.XSharpRuntime ?
                XSharpQualifiedFunctionNames.StringArrayInit : 
                VulcanQualifiedFunctionNames.StringArrayInit, args, true));
            stmt.XNode = sub.Parent as XSharpParserRuleContext;
            stmts.Add(stmt);

            stmt = GenerateReturn(varname,true);
            stmt.XNode = sub.Parent as XSharpParserRuleContext;
            stmts.Add(stmt);
            var body = MakeBlock(stmts);
            _pool.Free(stmts);
            var attributes = MakeCompilerGeneratedAttribute();
            var modifiers = MakeList<SyntaxToken>(SyntaxFactory.MakeToken(SyntaxKind.StaticKeyword),
                                                    SyntaxFactory.MakeToken(SyntaxKind.InternalKeyword));
            var funcdecl = _syntaxFactory.MethodDeclaration(
                    attributeLists: attributes,
                    modifiers: modifiers,
                    returnType: arrayType,
                    explicitInterfaceSpecifier: null,
                    identifier: SyntaxFactory.MakeIdentifier(funcname),
                    typeParameterList: null,
                    parameterList: EmptyParameterList(),
                    constraintClauses: null,
                    body: body,
                    expressionBody: null,
                    semicolonToken: SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken));
            funcdecl.XNode = sub.Parent as XSharpParserRuleContext;
            GlobalClassEntities.Members.Add(funcdecl);
            var expr = GenerateMethodCall(funcname);
            return expr;
        }
    

        protected virtual void VisitLocalvar([NotNull] XP.LocalvarContext context)
        {
            bool isConst = context.Const != null;
            bool isStatic = (context.Parent as XP.CommonLocalDeclContext).Static != null;
            bool isDim = context.Dim != null && context.ArraySub != null;
            string staticName = null;
            TypeSyntax varType;
            if (context.DataType is null && context.Expression is XP.TypeCheckExpressionContext tcec)
            {
                // expression was incorrectly parsed. Fix it here
                // local x := 512 IS DWORD 
                context.Expression = tcec.Expr;
                context.As = tcec.Op;
                context.DataType = tcec.Type;
            }
            var initExpr = context.Expression?.Get<ExpressionSyntax>();

            varType = getDataType(context.DataType);
            bool simpleInit = false;
            varType.XVoDecl = true;
            if (context.As?.Type == XP.IS)
            { 
                varType.XVoIsDecl = true;
            }
            if (isDim )
            {
                if (CurrentEntity != null)
                { 
                    CurrentEntity.Data.HasDimVar = true;
                }
                varType.XVoIsDim = true;
            }
            if (initExpr == null && isDim)
            {
                var arrayType = _syntaxFactory.ArrayType(varType, context.ArraySub.Get<ArrayRankSpecifierSyntax>());            // with dimensions      string[10,10]
                varType = _syntaxFactory.ArrayType(varType, MakeArrayRankSpecifier(context.ArraySub._ArrayIndex.Count));        // without dimensions   string[,]
                initExpr = GenerateDimArrayInitExpression(arrayType, context.ArraySub );
            }
            if (_options.HasOption(CompilerOption.InitLocals, context, PragmaOptions) && initExpr == null)
            {
                // no filtering on InitLocals here. That is done inside GenerateInitializer
                initExpr = GenerateInitializer(context.DataType);
            }
            if (isStatic)
            {
                staticName = XSharpSpecialNames.StaticLocalFieldNamePrefix + CurrentEntity.ShortName+"$"+context.Id.Get<SyntaxToken>().Text + UniqueNameSuffix(context);
                if (initExpr == null)
                {
                	// no filtering on InitLocals here. That is done inside GenerateInitializer
                    initExpr = GenerateInitializer(context.DataType);
                }
                simpleInit = (initExpr is LiteralExpressionSyntax);
                ClassEntities.Peek().Members.Add(
                    _syntaxFactory.FieldDeclaration(
                        EmptyList<AttributeListSyntax>(),
                        TokenList(SyntaxKind.StaticKeyword, SyntaxKind.InternalKeyword),
                        _syntaxFactory.VariableDeclaration(varType,
                            MakeSeparatedList(GenerateVariable(SyntaxFactory.Identifier(staticName),simpleInit ? initExpr : null))),
                        SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken))
                    );
                if (initExpr != null && ! simpleInit)
                {
                    ClassEntities.Peek().Members.Add(
                        _syntaxFactory.FieldDeclaration(
                            EmptyList<AttributeListSyntax>(),
                            TokenList(SyntaxKind.StaticKeyword, SyntaxKind.InternalKeyword),
                            _syntaxFactory.VariableDeclaration(_syntaxFactory.PredefinedType(SyntaxFactory.MakeToken(SyntaxKind.BoolKeyword)),
                                MakeSeparatedList(
                                        GenerateVariable(SyntaxFactory.Identifier(staticName + XSharpSpecialNames.StaticLocalInitFieldNameSuffix),
                                        GenerateLiteral(true)))),
                            SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken))
                        );
                    ClassEntities.Peek().Members.Add(
                        _syntaxFactory.FieldDeclaration(
                            EmptyList<AttributeListSyntax>(),
                            TokenList(SyntaxKind.StaticKeyword, SyntaxKind.InternalKeyword),
                            _syntaxFactory.VariableDeclaration(objectType,
                                MakeSeparatedList(GenerateVariable(SyntaxFactory.Identifier(staticName + XSharpSpecialNames.StaticLocalLockFieldNameSuffix),
                                        CreateObject(objectType, EmptyArgumentList())))),
                            SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken))
                        );
                }
            }
            var variables = _pool.AllocateSeparated<VariableDeclaratorSyntax>();
            var vardecl = _syntaxFactory.VariableDeclarator(context.Id.Get<SyntaxToken>(), null,
                isStatic ? _syntaxFactory.EqualsValueClause(SyntaxFactory.MakeToken(SyntaxKind.EqualsToken),
                    _syntaxFactory.RefExpression(SyntaxFactory.MakeToken(SyntaxKind.RefKeyword), GenerateSimpleName(staticName)))
                : (initExpr == null) ? null : _syntaxFactory.EqualsValueClause(SyntaxFactory.MakeToken(SyntaxKind.EqualsToken), initExpr));
            vardecl.XVoIsDim = isDim;
            var name = context.Id.GetText();
            if (CurrentEntity?.Data.GetField(name) != null)
            {
                vardecl = vardecl.WithAdditionalDiagnostics( new SyntaxDiagnosticInfo(ErrorCode.ERR_MemvarFieldWithSameName, name));
            }
            variables.Add(vardecl);
            var modifiers = _pool.Allocate();
            if (isConst)
                modifiers.Add(SyntaxFactory.MakeToken(SyntaxKind.ConstKeyword));
                
            if (!isStatic)
            {
                var ldecl = _syntaxFactory.LocalDeclarationStatement(
                    modifiers.ToList<SyntaxToken>(),
                    _syntaxFactory.VariableDeclaration(varType, variables),
                    SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken));
                ldecl.XVoIsDim = isDim;
                context.Put(ldecl);
            }
            else
            {
                var decl = _pool.Allocate<StatementSyntax>();
                var ldecl = _syntaxFactory.LocalDeclarationStatement(
                    modifiers.ToList<SyntaxToken>(),
                    _syntaxFactory.VariableDeclaration(
                        _syntaxFactory.RefType(SyntaxFactory.MakeToken(SyntaxKind.RefKeyword), null, varType), variables),
                    SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken));
                decl.Add(ldecl);
                if (initExpr != null && ! simpleInit)
                {
                    decl.Add(GenerateIfStatement(
                        GenerateSimpleName(staticName + XSharpSpecialNames.StaticLocalInitFieldNameSuffix),

                        MakeLock(GenerateSimpleName(staticName + XSharpSpecialNames.StaticLocalLockFieldNameSuffix),
                            GenerateIfStatement(GenerateSimpleName(staticName + XSharpSpecialNames.StaticLocalInitFieldNameSuffix),

                                MakeBlock(MakeList<StatementSyntax>(
                                        GenerateExpressionStatement(MakeSimpleAssignment(GenerateSimpleName(staticName), initExpr)),
                                        GenerateExpressionStatement(
                                            MakeSimpleAssignment(
                                                GenerateSimpleName(staticName + XSharpSpecialNames.StaticLocalInitFieldNameSuffix),
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
            context.SetSequencePoint();
            var variables = _pool.AllocateSeparated<VariableDeclaratorSyntax>();
            var name = context.Id.GetText();
            if (CurrentEntity?.Data.GetField(name) != null)
            {
                context.AddError(new ParseErrorData(context, ErrorCode.ERR_MemvarFieldWithSameName, name));
            }
            var variable = _syntaxFactory.VariableDeclarator(context.Id.Get<SyntaxToken>(), null,
                (context.Expression == null) ? null :
                _syntaxFactory.EqualsValueClause(SyntaxFactory.MakeToken(SyntaxKind.EqualsToken), context.Expression.Get<ExpressionSyntax>()));
            variables.Add(variable);
            if (isConst)
                context.AddError(new ParseErrorData(context, ErrorCode.ERR_ImplicitlyTypedVariableCannotBeConst));
            if (isStatic)
                context.AddError(new ParseErrorData(context, ErrorCode.ERR_ImplicitlyTypedVariableCannotBeStatic));
            context.Put(_syntaxFactory.LocalDeclarationStatement(
                EmptyList<SyntaxToken>(),
                _syntaxFactory.VariableDeclaration(_impliedType, variables),
                SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken)));
            _pool.Free(variables);
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
            var decl = _syntaxFactory.VariableDeclarator(
                context.Id.Get<SyntaxToken>(),
                null,
                _syntaxFactory.EqualsValueClause(SyntaxFactory.MakeToken(SyntaxKind.EqualsToken), context.Expr.Get<ExpressionSyntax>()));
            context.Put(decl);
        }

        public override void ExitFieldStmt([NotNull] XP.FieldStmtContext context)
        {
            context.Put(context.Decl.Get<StatementSyntax>());
        }

        #endregion

        #region Loop Statements
        public override void ExitWhileStmt([NotNull] XP.WhileStmtContext context)
        {
            context.SetSequencePoint(context.Expr);
            var whileKwd = context.w.SyntaxKeyword();

            StatementSyntax whileStmt = _syntaxFactory.WhileStatement(whileKwd,
                SyntaxFactory.MakeToken(SyntaxKind.OpenParenToken),
                context.Expr.Get<ExpressionSyntax>(),
                SyntaxFactory.MakeToken(SyntaxKind.CloseParenToken),
                context.StmtBlk.Get<BlockSyntax>());
            context.Put(whileStmt);
        }

        public override void ExitRepeatStmt([NotNull] XP.RepeatStmtContext context)
        {
            context.SetSequencePoint(context.end);
            context.Expr.SetSequencePoint();
            var doKwd = context.r.SyntaxKeyword();
            var doStmt = _syntaxFactory.DoStatement(doKwd,
                context.StmtBlk.Get<BlockSyntax>(),
                SyntaxFactory.MakeToken(SyntaxKind.WhileKeyword),
                SyntaxFactory.MakeToken(SyntaxKind.OpenParenToken),
                _syntaxFactory.PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, SyntaxFactory.MakeToken(SyntaxKind.ExclamationToken),
                    context.Expr.Get<ExpressionSyntax>()),
                SyntaxFactory.MakeToken(SyntaxKind.CloseParenToken),
                SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken));
            context.Put(doStmt);
        }

        public override void ExitForStmt([NotNull] XP.ForStmtContext context)
        {
            ExpressionSyntax assignExpr, whileExpr, incrExpr, iterExpr, initExpr;
            context.SetSequencePoint(context.end);
            if (context.AssignExpr != null)
            {
                var assign = context.AssignExpr as XP.AssignmentExpressionContext;
                var bin = context.AssignExpr as XP.BinaryExpressionContext;
                if (assign == null && bin == null)
                {
                    context.Put(_syntaxFactory.EmptyStatement(SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken)));
                    context.AddError(new ParseErrorData(context.Dir, ErrorCode.ERR_SyntaxError, ":="));
                    return;
                }
                if (assign != null)
                { 
                    context.AssignExpr.SetSequencePoint();
                    iterExpr = assign.Left.Get<ExpressionSyntax>();
                    initExpr = assign.Right.Get<ExpressionSyntax>();
                    assignExpr = assign.Get<ExpressionSyntax>();
                    iterExpr.XNode = assign.Left;
                    initExpr.XNode = assign.Right;
                }
                else // must be a binary expression then
                {
                    if (bin.Op.Type != XP.EQ)
                    {
                        context.Put(_syntaxFactory.EmptyStatement(SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken)));
                        context.AddError(new ParseErrorData(context.Dir, ErrorCode.ERR_SyntaxError, ":="));
                        return;
                    }
                    context.AssignExpr.SetSequencePoint();
                    iterExpr = bin.Left.Get<ExpressionSyntax>();
                    initExpr = bin.Right.Get<ExpressionSyntax>();
                    assignExpr = MakeSimpleAssignment(iterExpr, initExpr);
                    iterExpr.XNode = bin.Left;
                    initExpr.XNode = bin.Right;
                }
            }
            else
            {
                iterExpr = _syntaxFactory.IdentifierName(context.ForIter.Get<SyntaxToken>());
                iterExpr.XNode = context.ForIter;
                initExpr = context.Expr.Get<ExpressionSyntax>();
                assignExpr = MakeSimpleAssignment(iterExpr, initExpr);
                assignExpr.XNode = context.Expr;
                initExpr.XNode = context.Expr;
                context.Expr.SetSequencePoint();
                context.ForIter.SetSequencePoint();
            }
            if (context.Step == null)
            {
                context.Step = FixPosition(new XP.PrimaryExpressionContext(FixPosition(new XP.ExpressionContext(), context.Stop)), context.Stop);
                context.Step.Put(GenerateLiteral("1", 1));
            }
            else
            {
                context.Step.SetSequencePoint();
            }
            switch (context.Dir.Type)
            {
                case XP.DOWNTO:
                    whileExpr = _syntaxFactory.BinaryExpression(SyntaxKind.GreaterThanOrEqualExpression,
                        iterExpr,
                        SyntaxFactory.MakeToken(SyntaxKind.GreaterThanEqualsToken),
                        context.FinalExpr.Get<ExpressionSyntax>());
                    incrExpr = _syntaxFactory.AssignmentExpression(SyntaxKind.SubtractAssignmentExpression,
                        iterExpr,
                        SyntaxFactory.MakeToken(SyntaxKind.MinusEqualsToken),
                        context.Step.Get<ExpressionSyntax>());
                    whileExpr.XNode = context.FinalExpr;
                    incrExpr.XNode = context.Step;
                    context.FinalExpr.SetSequencePoint();
                    context.Step.SetSequencePoint();
                    break;
                case XP.UPTO:
                    whileExpr = _syntaxFactory.BinaryExpression(SyntaxKind.LessThanOrEqualExpression,
                            iterExpr,
                            SyntaxFactory.MakeToken(SyntaxKind.LessThanEqualsToken),
                            context.FinalExpr.Get<ExpressionSyntax>());

                    incrExpr = _syntaxFactory.AssignmentExpression(SyntaxKind.AddAssignmentExpression,
                                iterExpr,
                                SyntaxFactory.MakeToken(SyntaxKind.PlusEqualsToken),
                                context.Step.Get<ExpressionSyntax>());
                    whileExpr.XNode = context.FinalExpr;
                    incrExpr.XNode = context.Step;
                    context.FinalExpr.SetSequencePoint();
                    context.Step.SetSequencePoint();
                    break;
                case XP.TO:
                default:
                    // 
                    var compExpr = _syntaxFactory.BinaryExpression(SyntaxKind.GreaterThanExpression,
                        context.Step.Get<ExpressionSyntax>(), 
                        SyntaxFactory.MakeToken(SyntaxKind.GreaterThanToken),
                        GenerateLiteral(0));
                    var ltExpr = _syntaxFactory.BinaryExpression(SyntaxKind.LessThanOrEqualExpression,
                            iterExpr,
                            SyntaxFactory.MakeToken(SyntaxKind.LessThanEqualsToken),
                            context.FinalExpr.Get<ExpressionSyntax>());
                    var gtExpr = _syntaxFactory.BinaryExpression(SyntaxKind.GreaterThanOrEqualExpression,
                            iterExpr,
                            SyntaxFactory.MakeToken(SyntaxKind.GreaterThanEqualsToken),
                            context.FinalExpr.Get<ExpressionSyntax>());
                    whileExpr = MakeConditional(compExpr,ltExpr,gtExpr);
                    whileExpr.XGenerated = true;
                    incrExpr = _syntaxFactory.AssignmentExpression(SyntaxKind.AddAssignmentExpression,
                                iterExpr,
                                SyntaxFactory.MakeToken(SyntaxKind.PlusEqualsToken),
                                context.Step.Get<ExpressionSyntax>());
                    incrExpr.XGenerated = true;
                    whileExpr.XNode = context.FinalExpr;
                    incrExpr.XNode = context.Step;
                    context.FinalExpr.SetSequencePoint();
                    context.Step.SetSequencePoint();
                    break;
            }
            var decl = default(VariableDeclarationSyntax);
            var init = _pool.AllocateSeparated<ExpressionSyntax>();
            if (context.ForDecl != null)
            {
                decl = _syntaxFactory.VariableDeclaration(
                    context.Type?.Get<TypeSyntax>() ?? _impliedType,
                    MakeSeparatedList(GenerateVariable(
                        context.ForIter.Get<SyntaxToken>(), initExpr)));
                decl.XNode = context.ForIter;
                context.ForIter.SetSequencePoint();
            }
            else
            {
                init.Add(assignExpr);
            }
            var incr = _pool.AllocateSeparated<ExpressionSyntax>();
            incr.Add(incrExpr);
            var forKwd = context.f.SyntaxKeyword();
            StatementSyntax forStmt = _syntaxFactory.ForStatement(forKwd,
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
            context.Put(forStmt);
        }

        public override void ExitForeachStmt([NotNull] XP.ForeachStmtContext context)
        {
            context.SetSequencePoint(context.end);
            context.Id.SetSequencePoint();
            context.Container.SetSequencePoint();
            var foreachKwd = context.f.SyntaxKeyword();
            StatementSyntax forStmt = _syntaxFactory.ForEachStatement(foreachKwd,
                SyntaxFactory.MakeToken(SyntaxKind.OpenParenToken),
                context.Type?.Get<TypeSyntax>() ?? _impliedType,
                context.Id.Get<SyntaxToken>(),
                SyntaxFactory.MakeToken(SyntaxKind.InKeyword),
                context.Container.Get<ExpressionSyntax>(),
                SyntaxFactory.MakeToken(SyntaxKind.CloseParenToken),
                context.StmtBlk.Get<BlockSyntax>());
            context.Put(forStmt);

        }

        public override void EnterWithBlock([NotNull] XP.WithBlockContext context)
        {
            context.VarName = XSharpSpecialNames.WithVarName+ UniqueNameSuffix(context);
        }

        public override void ExitWithBlock([NotNull] XP.WithBlockContext context)
        {

            var stmts = new List<StatementSyntax>();
            var declstmt = GenerateLocalDecl(context.VarName, _impliedType, context.Expr.Get<ExpressionSyntax>());
            stmts.Add(declstmt);
            BlockSyntax block = context.StmtBlk.Get<BlockSyntax>();
            stmts.AddRange(block.Statements.Nodes);
            context.Put(MakeBlock(stmts));
        }                    


        #endregion

        #region Conditional Statements
        public override void ExitIfStmt([NotNull] XP.IfStmtContext context)
        {
            IfStatementSyntax ifStmt = context.IfStmt.Get<IfStatementSyntax>();
            ifStmt = ifStmt.Update(context.i.SyntaxKeyword(), ifStmt.OpenParenToken, ifStmt.Condition, ifStmt.CloseParenToken, ifStmt.Statement, ifStmt.Else);
            context.SetSequencePoint(context.IfStmt.Cond);
            if (context.IfStmt.Cond.CsNode is IsPatternExpressionSyntax)
            {
                // wrap the ifStmt in a block to make sure that variable introduced in IF x IS Foo oFoo is not visible after the block
                var stmts = new List<StatementSyntax>();
                stmts.Add(ifStmt);
                context.Put(MakeBlock(stmts));
            }
            else
            { 
                context.Put(ifStmt);
            }
        }

        public override void ExitIfElseBlock([NotNull] XP.IfElseBlockContext context)
        {
            context.SetSequencePoint(context.Cond);
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
            if (context.CaseStmt != null)
            {
                context.SetSequencePoint(context.CaseStmt.Start, context.CaseStmt.end.Start);
            }
            else
                context.SetSequencePoint(context.end);
            if (context.CaseStmt.Key.Type == XP.OTHERWISE)
            {
                // DO case without case block, so we should always execute the otherwise block
                var block = context.CaseStmt.CsNode as StatementSyntax;
                block = block.WithAdditionalDiagnostics(new SyntaxDiagnosticInfo(ErrorCode.WRN_EmptyCase));
                context.Put(block);
            }
            else
            { 
                StatementSyntax caseStmt = (StatementSyntax)context.CaseStmt?.Get<IfStatementSyntax>() ??
                    _syntaxFactory.EmptyStatement(SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken));
                context.Put(caseStmt);
            }
        }

        public override void ExitCaseBlock([NotNull] XP.CaseBlockContext context)
        {
            if (context.Key.Type == XP.OTHERWISE)
            {
                context.SetSequencePoint(context.end);
                context.Put(context.StmtBlk.Get<StatementSyntax>());
            }
            else
            {
                context.SetSequencePoint(context.Cond);
                context.Put(GenerateIfStatement(
                    context.Cond.Get<ExpressionSyntax>(),
                    context.StmtBlk.Get<BlockSyntax>(),
                    (context.NextCase == null) ? null :
                        _syntaxFactory.ElseClause(SyntaxFactory.MakeToken(SyntaxKind.ElseKeyword),
                            context.NextCase.Get<StatementSyntax>())));
            }
        }


        public override void ExitSwitchStmt([NotNull] XP.SwitchStmtContext context)
        {
            context.SetSequencePoint(context.end);
            var sections = _pool.Allocate<SwitchSectionSyntax>();
            var emptyLabels = _pool.Allocate<SwitchLabelSyntax>();
            foreach (var switchBlkCtx in context._SwitchBlock)
            {
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

            StatementSyntax switchStmt = _syntaxFactory.SwitchStatement(context.S.SyntaxKeyword(),
                SyntaxFactory.MakeToken(SyntaxKind.OpenParenToken),
                context.Expr.Get<ExpressionSyntax>(),
                SyntaxFactory.MakeToken(SyntaxKind.CloseParenToken),
                SyntaxFactory.MakeToken(SyntaxKind.OpenBraceToken),
                sections,
                SyntaxFactory.MakeToken(SyntaxKind.CloseBraceToken));

            context.Put(switchStmt);
            _pool.Free(sections);
            _pool.Free(emptyLabels);
        }

        public override void ExitSwitchBlock([NotNull] XP.SwitchBlockContext context)
        {
            var labels = _pool.Allocate<SwitchLabelSyntax>();
            var kw = context.Key.SyntaxKeyword();
            var colon = SyntaxFactory.MakeToken(SyntaxKind.ColonToken);
            context.SetSequencePoint(context.end);
            SwitchLabelSyntax label;
            if (kw.Kind == SyntaxKind.CaseKeyword)
            {
                if (context.Const != null)
                {
                    var expr = context.Const?.Get<ExpressionSyntax>();
                    if (context.whenexpr != null)
                    {
                        var whenClause = _syntaxFactory.WhenClause(context.W.SyntaxKeyword(), context.whenexpr.Get<ExpressionSyntax>());
                        var pattern = _syntaxFactory.ConstantPattern(expr);
                        label = _syntaxFactory.CasePatternSwitchLabel(kw, pattern, whenClause, colon);
                    }
                    else
                    {
                        label = _syntaxFactory.CaseSwitchLabel(kw, expr, colon);
                    }
                }
                else
                {
                    VariableDesignationSyntax designation;
                    if (context.Id.GetText() == "_")
                        designation = _syntaxFactory.DiscardDesignation(SyntaxFactory.MakeToken(SyntaxKind.UnderscoreToken,"_"));
                    else
                        designation = _syntaxFactory.SingleVariableDesignation(context.Id.Get<SyntaxToken>());
                    var type = context.DataType.Get<TypeSyntax>();
                    var node = _syntaxFactory.DeclarationPattern(type, designation);
                    WhenClauseSyntax whenexpr = null;
                    if (context.whenexpr != null)
                    {
                        whenexpr = _syntaxFactory.WhenClause(context.W.SyntaxKeyword(), context.whenexpr.Get<ExpressionSyntax>());
                    }
                    label = _syntaxFactory.CasePatternSwitchLabel(kw, node, whenexpr, colon);
                }
            }
            else
            {
                label = _syntaxFactory.DefaultSwitchLabel(kw, SyntaxFactory.MakeToken(SyntaxKind.ColonToken));
            }
            labels.Add(label);
            var stmts = _pool.Allocate<StatementSyntax>();
            if (context.StmtBlk._Stmts.Count > 0)
            {
                stmts.Add(context.StmtBlk.Get<BlockSyntax>());
                if (NeedsBreak(context.StmtBlk._Stmts))
                {
                    var brk = _syntaxFactory.BreakStatement(SyntaxFactory.MakeToken(SyntaxKind.BreakKeyword),
                        SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken));
                    brk.XNode = context.StmtBlk._Stmts.Last();
                    stmts.Add(brk);
                }
            }
            context.Put(_syntaxFactory.SwitchSection(labels, stmts));
            _pool.Free(labels);
            _pool.Free(stmts);
        }

        #endregion

        #region Error Handling Statements

        public override void ExitTryStmt([NotNull] XP.TryStmtContext context)
        {
            // Add default Catch block for try stmt without catch blocks
            context.SetSequencePoint(context.end);
            if (context._CatchBlock?.Count == 0  && context.FinBlock == null)
            {
                var cb = FixPosition(new XP.CatchBlockContext(context, 0), context.Stop);
                cb.StmtBlk = FixPosition(new XP.StatementBlockContext(cb, 0), context.Stop);
                this.ExitStatementBlock(cb.StmtBlk);
                this.ExitCatchBlock(cb);
                context._CatchBlock.Add(cb);
            }
            var catches = _pool.Allocate<CatchClauseSyntax>();
            foreach (var catchCtx in context._CatchBlock)
            {
                catches.Add(catchCtx.Get<CatchClauseSyntax>());
            }
            var trykwd = context.T.SyntaxKeyword();
            var finkwd = context.F?.SyntaxKeyword();
            StatementSyntax tryStmt =
            _syntaxFactory.TryStatement(trykwd,
                context.StmtBlk.Get<BlockSyntax>(),
                catches,
                context.FinBlock == null ? null : _syntaxFactory.FinallyClause(finkwd,
                    context.FinBlock.Get<BlockSyntax>()));
            _pool.Free(catches);
            context.Put(tryStmt);
        }

        public override void ExitCatchBlock([NotNull] XP.CatchBlockContext context)
        {
            context.SetSequencePoint(context.end);
            CatchDeclarationSyntax decl = null;
            if (context.Type != null || context.Id != null)
            {
                SyntaxToken id = null;
                TypeSyntax type = null;
                if (context.Id != null)
                {
                    id = context.Id.Get<SyntaxToken>();
                }
                if (context.Type != null)
                {
                    type = context.Type.Get<TypeSyntax>();
                }
                else
                {
                    type = GenerateQualifiedName("System.Exception");
                }
                decl = _syntaxFactory.CatchDeclaration(
                    SyntaxFactory.MakeToken(SyntaxKind.OpenParenToken),
                    type, 
                    id,
                    SyntaxFactory.MakeToken(SyntaxKind.CloseParenToken));
            }
            CatchFilterClauseSyntax filter = null;
            if (context.whenexpr != null)
            {
                filter = _syntaxFactory.CatchFilterClause(
                    context.W.SyntaxKeyword(),
                    SyntaxFactory.MakeToken(SyntaxKind.OpenParenToken),
                    context.whenexpr.Get<ExpressionSyntax>(),
                    SyntaxFactory.MakeToken(SyntaxKind.CloseParenToken));
            }

            context.Put(_syntaxFactory.CatchClause(SyntaxFactory.MakeToken(SyntaxKind.CatchKeyword),
                decl,
                filter, 
                context.StmtBlk.Get<BlockSyntax>()));
        }


        #endregion

        #region Other Statements

        public override void ExitStatementBlock([NotNull] XP.StatementBlockContext context)
        {
            List<StatementSyntax> statements = new List<StatementSyntax>();
            if (context._Stmts.Count > 0)
            {
                context.SetSequencePoint(context._Stmts[0].Stop);
            }
            foreach (var stmtCtx in context._Stmts)
            {
                // Sometimes we generate more than 1 C# statement for a xBase statement
                if (stmtCtx.CsNode is SyntaxList<StatementSyntax>)
                {
                    var list = (SyntaxList < StatementSyntax > ) stmtCtx.CsNode ;
                    for (int i = 0; i < list.Count; i++)
                    {
                        var stmt = list[i];
                        statements.Add(stmt);
                    }
                }
                else
                {
                    statements.Add(stmtCtx.Get<StatementSyntax>());
                }
            }
            if (CurrentEntity != null && CurrentEntity.Data.HasDimVar)
            {
                // Check for LOCAL DIM arrays and change them to Fixed statements
                statements = CheckForLocalDimArrays(statements);
            }
            context.Put(MakeBlock(statements));
        }

        private List<StatementSyntax> CheckForLocalDimArrays(List<StatementSyntax> statements)
        {
            if (!CurrentEntity?.Data.HasAddressOf == true)
                return statements;

            foreach (var stmt in statements.ToList())
            {
                if (stmt is LocalDeclarationStatementSyntax)
                {
                    var localdecl = stmt as LocalDeclarationStatementSyntax;
                    if (localdecl.XVoIsDim)
                    {
                        // we only have one variable per localdecl
                        var vars = localdecl.Declaration.Variables;
                        var vardecl = vars[0];
                            
                        // create new declaration where the local is a ptr
                        // copy rest of statements to array
                        // remove rest of statements from the current array

                        int iPos = statements.IndexOf(stmt);
                        var substmts = new StatementSyntax[statements.Count - iPos - 1];
                        statements.CopyTo(iPos + 1, substmts, 0, substmts.Length);
                        statements.RemoveRange(iPos + 1, substmts.Length);
                        var newStmts = CheckForLocalDimArrays(substmts.ToList());
                        var newBlock = MakeBlock(newStmts);

                        // create new declaration for a pointer variable of the type 
                        // that the original declaration points to
                        // The new name will be the originalname + "$Dim"
                        var newId = SyntaxFactory.MakeIdentifier(vardecl.Identifier.Text + "$dim");
                        var atype = localdecl.Declaration.Type as ArrayTypeSyntax;
                        var element = atype.ElementType;
                        var ptype = _syntaxFactory.PointerType(atype.ElementType, SyntaxFactory.MakeToken(SyntaxKind.AsteriskToken));
                        var addressof = _syntaxFactory.PrefixUnaryExpression(SyntaxKind.AddressOfExpression,
                                        SyntaxFactory.MakeToken(SyntaxKind.AmpersandToken),
                                        GenerateSimpleName(vardecl.Identifier.Text));

                        var initexpr = _syntaxFactory.EqualsValueClause(SyntaxFactory.MakeToken(SyntaxKind.EqualsToken),
                                        addressof);
                        var newvardecl = _syntaxFactory.VariableDeclarator(newId, null, initexpr);
                        var newvardecl2 = _syntaxFactory.VariableDeclaration(ptype, MakeSeparatedList(newvardecl));

                        var newStmt = _syntaxFactory.FixedStatement(
                                    SyntaxFactory.MakeToken(SyntaxKind.FixedKeyword),
                                    SyntaxFactory.MakeToken(SyntaxKind.OpenParenToken),
                                    newvardecl2,
                                    SyntaxFactory.MakeToken(SyntaxKind.CloseParenToken),
                                newBlock);
                        statements.Add(newStmt);
                        return statements;
                    }
                }
            }
            return statements;
        }

        private bool ContainsExitStatement(IList<XP.StatementContext> stmts)
        {
            // Checks for EXIT statements that are not inside a LoopStmt
            // Because these are not allowed inside a Switch
            foreach (var stmt in stmts)
            {
                if (stmt is XP.JumpStmtContext)
                {
                    var jmpstmt = stmt as XP.JumpStmtContext;
                    if (jmpstmt.Key.Type == XP.EXIT)
                        return true;
                }
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
            if (stmt is XP.ReturnStmtContext)
            {
                return false;
            }
            if (stmt is XP.JumpStmtContext)
            {
                var jmpstmt = stmt as XP.JumpStmtContext;
                if (jmpstmt.Key.Type == XP.THROW || jmpstmt.Key.Type == XP.BREAK)
                    return false;

                if ((jmpstmt.Key.Type == XP.EXIT || jmpstmt.Key.Type == XP.LOOP) && !inSideLoop)
                {
                    // LOOP or EXIT inside a nested Loop block
                    return false;
                }
            }
            if (stmt is XP.IfStmtContext)
            {
                var ifstmt = stmt as XP.IfStmtContext;
                var ifelsestmt = ifstmt.IfStmt;
                var elsestmt = ifelsestmt?.ElseBlock;           // The first ifelsestmt should always have a value, but better safe than sorry
                // process to the end of the list
                // when there is no else, then we need a break
                // otherwise process every statement list
                while (ifelsestmt != null)                     //
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
                    return NeedsBreak(elsestmt._Stmts);
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

        public override void ExitJumpStmt([NotNull] XP.JumpStmtContext context)
        {
            context.SetSequencePoint(context.end);
            switch (context.Key.Type)
            {
                case XP.EXIT:
                    context.Put(_syntaxFactory.BreakStatement(context.Key.SyntaxKeyword(),
                        SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken)));
                    break;
                case XP.LOOP:
                    context.Put(_syntaxFactory.ContinueStatement(context.Key.SyntaxKeyword(),
                        SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken)));
                    break;
                case XP.BREAK:
                    // Error already handled in ParseErrorAnalysis
                    break;
                case XP.THROW:
                    context.Put(_syntaxFactory.ThrowStatement(context.Key.SyntaxKeyword(),
                        context.Expr.Get<ExpressionSyntax>(),
                        SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken)));
                    break;

            }
        }

        protected virtual StatementSyntax HandleExpressionStmt(IList<XP.ExpressionContext> expressions)
        {
            var statements = _pool.Allocate<StatementSyntax>();
            StatementSyntax result;
            foreach (var exprCtx in expressions)
            {
                // check because there may already be statements in here, such as the IF statement generated for AltD()
                var node = exprCtx.CsNode;
                if (node is StatementSyntax)
                {
                    statements.Add((StatementSyntax)node);
                }
                else
                {
                    // check for binary expression with '=' operator.
                    // convert to assignment and add a warning when not in FoxPro dialect
                    // please note that the expression
                    // x = y = z
                    // is represented as
                    //    Binary Expression
                    //       Left = Binary Expression x == y
                    //       Op   = ==
                    //       Right = Simple Name z
                    // so we need to 'rebuild' the expression
                    exprCtx.SetSequencePoint(exprCtx.Start, exprCtx.Stop);
                    var expr = exprCtx.Get<ExpressionSyntax>();
                    if (expr is BinaryExpressionSyntax bin)
                    {
                        bool nestedAssign = false;
                        var xNode1 = bin.XNode as XP.BinaryExpressionContext;
                        var oldStyleAssign = bin.OperatorToken.Kind == SyntaxKind.EqualsEqualsToken && xNode1 != null && xNode1.Op.Type == XP.EQ;
                        if (bin.Left is BinaryExpressionSyntax binLeft)
                        {
                            if (binLeft.OperatorToken.Kind == SyntaxKind.EqualsEqualsToken)
                            {
                                var xNode2 = binLeft.XNode as XP.BinaryExpressionContext;
                                nestedAssign = xNode2 != null && xNode2.Op.Type == XP.EQ;
                            }

                        }
                        if (oldStyleAssign || nestedAssign)
                        {
                            ExpressionSyntax RHS = bin.Right;
                            ExpressionSyntax LHS = bin.Left;
                            if (nestedAssign)
                            {
                                // check for x = y = z, but also x = y > z
                                var Left = (BinaryExpressionSyntax)LHS;
                                LHS = Left.Left;
                                RHS = _syntaxFactory.BinaryExpression(
                                                    bin.Kind,
                                                    Left.Right,
                                                    bin.OperatorToken,
                                                    bin.Right);
                            }
                            // other LHS to do a fieldput or memvar put are handled in XSharpTreeTransformationRT
                            expr = MakeSimpleAssignment(bin.Left, bin.Right);
                        }
                        if (_options.Dialect != XSharpDialect.FoxPro)
                        {
                            expr = expr.WithAdditionalDiagnostics(new SyntaxDiagnosticInfo(ErrorCode.WRN_AssignmentOperatorExpected));
                        }
                    }
                    var stmt = GenerateExpressionStatement(expr);
                    stmt.XNode = exprCtx;
                    statements.Add(stmt);
                }
            }

            if (statements.Count == 1)
            {
                result = statements[0];
            }
            else
            {
                result = MakeBlock(statements);
            }
            _pool.Free(statements);
            return result;
        }

        public override void ExitExpressionStmt([NotNull] XP.ExpressionStmtContext context)
        {
            context.SetSequencePoint(context._Exprs[0].Start, context._Exprs[0].Stop);
            var stmt = HandleExpressionStmt(context._Exprs);
            context.Put(stmt);
        }



        public override void ExitReturnStmt([NotNull] XP.ReturnStmtContext context)
        {
            context.SetSequencePoint(context.end);
            var expr = context.Expr?.Get<ExpressionSyntax>();
            var ent = CurrentEntity;
            if (context.Void != null && ent != null && !ent.Data.MustBeVoid)
            {
                expr = GenerateLiteral(0);
            }
            // / vo9 is handled in the Subclass
            context.Put(GenerateReturn(expr,false, context.R));
        }

        public override void ExitYieldStmt([NotNull] XP.YieldStmtContext context)
        {
            SyntaxKind kind;
            ExpressionSyntax arg;
            SyntaxToken token;
            context.SetSequencePoint(context.end);
            if (CurrentEntity != null)
                CurrentEntity.Data.HasYield = true;
            if (context.Break != null)  // yield exit or yield break
            {
                kind = SyntaxKind.YieldBreakStatement;
                arg = null;
                token = context.Break.SyntaxKeyword();
            }
            else                   // yield return
            {
                kind = SyntaxKind.YieldReturnStatement;
                arg = context.Expr?.Get<ExpressionSyntax>();
                token = context.R.SyntaxKeyword();
            }
            context.Put(_syntaxFactory.YieldStatement(kind, context.Y.SyntaxKeyword(), token, arg,
                SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken)));
        }

        public override void ExitQoutStmt([NotNull] XP.QoutStmtContext context)
        {
            ArgumentSyntax arg;
            ExpressionSyntax expr;
            // If dialect VO and VulcanRTFuncs included
            // Simply generate call to VulcanRTFuncs.Functions.QOut or QQOut
            // This is done in VOTreeTransForm
            context.SetSequencePoint(context.end);
            if (!(context._Exprs?.Count > 0))
            {
                if (context.Q.Type == XP.QMARK)
                {
                    expr = GenerateMethodCall(SystemQualifiedNames.WriteLine);
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
                string mask = context.Q.Type == XP.QMARK ? "\r\n" : String.Empty;
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

                expr = GenerateMethodCall(SystemQualifiedNames.Write, arglist);
                context.Put(GenerateExpressionStatement(expr));
            }
        }

        public override void ExitBlockStmt([NotNull] XP.BlockStmtContext context)
        {
            context.SetSequencePoint(context.end);
            StatementSyntax node;
            switch (context.Key.Type)
            {
                case XP.SCOPE:
                    node = context.StmtBlk.Get<BlockSyntax>();
                    break;
                case XP.LOCK:
                    node = MakeLock(context.Expr.Get<ExpressionSyntax>(),
                        context.StmtBlk.Get<BlockSyntax>(),context.Key);
                    break;
                case XP.UNSAFE:
                    node = _syntaxFactory.UnsafeStatement(context.Key.SyntaxKeyword(),
                        context.StmtBlk.Get<BlockSyntax>());
                    break;
                case XP.CHECKED:
                    node = _syntaxFactory.CheckedStatement(SyntaxKind.CheckedStatement,
                        context.Key.SyntaxKeyword(),
                        context.StmtBlk.Get<BlockSyntax>());
                    break;
                case XP.FIXED:
                    node = _syntaxFactory.FixedStatement(context.Key.SyntaxKeyword(),
                           SyntaxFactory.MakeToken(SyntaxKind.OpenParenToken),
                           context.VarDecl?.Get<VariableDeclarationSyntax>(),
                           SyntaxFactory.MakeToken(SyntaxKind.CloseParenToken),
                        context.StmtBlk.Get<BlockSyntax>());
                    break;
                case XP.UNCHECKED:
                    node = _syntaxFactory.CheckedStatement(SyntaxKind.UncheckedStatement,
                        context.Key.SyntaxKeyword(),
                        context.StmtBlk.Get<BlockSyntax>());
                    break;
                case XP.USING:
                    node = _syntaxFactory.UsingStatement(context.Key.SyntaxKeyword(),
                           SyntaxFactory.MakeToken(SyntaxKind.OpenParenToken),
                           context.VarDecl?.Get<VariableDeclarationSyntax>(),
                           context.Expr?.Get<ExpressionSyntax>(),
                           SyntaxFactory.MakeToken(SyntaxKind.CloseParenToken),
                           context.StmtBlk.Get<BlockSyntax>());
                    break;
                default:
                    // what else;
                    node = null;
                    break;
            }
            if (node != null)
            {
                context.Put(node);
            }

        }

        public override void ExitNopStmt([NotNull] XP.NopStmtContext context)
        {
            context.SetSequencePoint(context.end);
            context.Put(_syntaxFactory.EmptyStatement(SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken)));
        }

        #endregion
        #endregion
        #region Expressions
        #region Conditional Expressions
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

        public override void ExitIif([NotNull] XP.IifContext context)
        {
            ExpressionSyntax left;
            ExpressionSyntax right;
            if (context.TrueExpr == null)
            {
                // Harbour does not warn when NIL is missing
                left = GenerateMissingExpression(_options.Dialect != XSharpDialect.Harbour);
            }
            else
            {
                left = context.TrueExpr.Get<ExpressionSyntax>();
            }
            if (context.FalseExpr == null)
            {
                // Harbour does not warn when NIL is missing
                right = GenerateMissingExpression(_options.Dialect != XSharpDialect.Harbour);
            }
            else
            {
                right = context.FalseExpr.Get<ExpressionSyntax>();
            }
            context.Put(MakeConditional(context.Cond.Get<ExpressionSyntax>(),left,right));
        }

        #endregion
        
        #region Bound Expressions
        public override void ExitBoundAccessMember([NotNull] XP.BoundAccessMemberContext context)
        {
            context.Put(MakeSimpleMemberAccess(
                context.Expr.Get<ExpressionSyntax>(),
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

        protected virtual XP.WithBlockContext FindWithBlock(XSharpParserRuleContext context)
        {
            var parent = context.Parent;
            while (parent != null && !(parent is XP.IEntityContext))
            {
                if (parent is XP.WithBlockContext)
                {
                    break;
                }
                parent = parent.Parent;
            }
            return parent as XP.WithBlockContext;
        }

        protected virtual void CoreAccessMember([NotNull] XP.AccessMemberContext context)
        {
            if (context.Op.Type == XP.COLONCOLON)
            {
                context.Put(MakeSimpleMemberAccess(
                    GenerateSelf(),
                    context.Name.Get<SimpleNameSyntax>()));
            }
            else if (context.Expr == null)
            {
                // find surrounding WITH expression.
                var parent = FindWithBlock(context);
                if (parent is XP.WithBlockContext wb)
                {
                    var VarName = GenerateSimpleName(wb.VarName);
                    context.Put(MakeSimpleMemberAccess(VarName,context.Name.Get<SimpleNameSyntax>()));
                }
                else
                {
                    var node = GenerateLiteral(0);
                    node = node.WithAdditionalDiagnostics(new SyntaxDiagnosticInfo(ErrorCode.ERR_MissingWithStatement));
                    context.Put(node);
                }
            }
            else
            {
                context.Put(MakeSimpleMemberAccess(context.Expr.Get<ExpressionSyntax>(),context.Name.Get<SimpleNameSyntax>()));
            }
        }

        public override void ExitAccessMember([NotNull] XP.AccessMemberContext context)
        {
            CoreAccessMember(context);
        }

        #endregion  

        #region Common Expressions
        public override void ExitExpressionList([NotNull] XP.ExpressionListContext context)
        {
            var stmts = _pool.Allocate<StatementSyntax>();
            foreach (var eCtx in context._Exprs)
            {
                stmts.Add(GenerateExpressionStatement(eCtx.Get<ExpressionSyntax>()));
            }
            context.PutList(stmts.ToList());
            _pool.Free(stmts);
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
            if (CurrentEntity != null && context.Op.Type == XP.ADDROF)
            {
                CurrentEntity.Data.HasAddressOf = true;
            }

            context.Put(_syntaxFactory.PrefixUnaryExpression(
                context.Op.ExpressionKindPrefixOp(),
                context.Op.SyntaxPrefixOp(),
                context.Expr.Get<ExpressionSyntax>()));
        }

        public override void ExitBinaryExpression([NotNull] XP.BinaryExpressionContext context)
        {
            // when /vo12 is used then for the types .DIV add conversion for the LHS and RHS to Double


            switch (context.Op.Type)
            {
                case XP.EXP:
                    context.Put(GenerateMethodCall(SystemQualifiedNames.Pow,
                        _syntaxFactory.ArgumentList(SyntaxFactory.MakeToken(SyntaxKind.OpenParenToken),
                            MakeSeparatedList(MakeArgument(context.Left.Get<ExpressionSyntax>()),
                                MakeArgument(context.Right.Get<ExpressionSyntax>())),
                            SyntaxFactory.MakeToken(SyntaxKind.CloseParenToken))));
                    break;
                case XP.SUBSTR:
                    // Convert LHS $ RHS to RHS:IndexOf(LHS) >= 0
                    // but since they both can be NULL add a condition:
                    // LHS == NULL ? FALSE: RHS:IndexOf(LHS)

                    var condition = _syntaxFactory.BinaryExpression(SyntaxKind.EqualsExpression, context.Left.Get<ExpressionSyntax>(),
                        SyntaxFactory.MakeToken(SyntaxKind.EqualsEqualsToken), GenerateLiteralNull());
                    var indexof = _syntaxFactory.ConditionalAccessExpression(
                                    context.Right.Get<ExpressionSyntax>(),
                                    SyntaxFactory.MakeToken(SyntaxKind.QuestionToken),
                                    _syntaxFactory.InvocationExpression(_syntaxFactory.MemberBindingExpression(SyntaxFactory.MakeToken(SyntaxKind.DotToken), GenerateSimpleName("IndexOf")),
                                     MakeArgumentList(MakeArgument(context.Left.Get<ExpressionSyntax>()))));
                    var rhsExp = _syntaxFactory.BinaryExpression(SyntaxKind.GreaterThanExpression, indexof,
                                SyntaxFactory.MakeToken(SyntaxKind.GreaterThanToken),
                                GenerateLiteral("-1", -1));

                    var exp = MakeConditional(condition, GenerateLiteral(false), rhsExp);

                    context.Put(exp);
                    break;
                case XP.ASSIGN_EXP:
                    context.Put(MakeSimpleAssignment(
                        context.Left.Get<ExpressionSyntax>(),
                        GenerateMethodCall(SystemQualifiedNames.Pow,
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
            context.Put(context.Expr.Get<CSharpSyntaxNode>());
        }

        public override void ExitCheckedExpression([NotNull] XP.CheckedExpressionContext context)
        {
            var expr = MakeChecked(context.Expr.Get<ExpressionSyntax>(), context.ch.Type == XP.CHECKED);
            context.Put(expr);
        }

        private bool GenerateAltD(XP.MethodCallContext context)
        {
            // Pseudo function AltD()
            ArgumentListSyntax argList;
            if (context.ArgList != null)
            {
                argList = context.ArgList.Get<ArgumentListSyntax>();
            }
            else
            {
                argList = EmptyArgumentList();
            }
            var stmt = GenerateExpressionStatement(GenerateMethodCall(SystemQualifiedNames.DebuggerBreak, argList));
            var cond = MakeSimpleMemberAccess(
                        GenerateQualifiedName(SystemQualifiedNames.Debugger),
                        GenerateSimpleName("IsAttached"));
            context.Put(GenerateIfStatement(cond, stmt));
            return true;
        }

        private bool GenerateGetInst(XP.MethodCallContext context)
        {
            // Pseudo function _GetInst()
            ArgumentListSyntax argList;
            ExpressionSyntax expr;
            if (context.ArgList != null)
            {
                argList = context.ArgList.Get<ArgumentListSyntax>();
                if (argList.Arguments.Count != 0)
                {
                    context.Put(GenerateLiteral(0).WithAdditionalDiagnostics(new SyntaxDiagnosticInfo(ErrorCode.ERR_BadArgCount, "_getInst", argList.Arguments.Count)));
                    return true;
                }
            }

            TypeSyntax globaltype = GenerateQualifiedName(GlobalClassName);
            expr = MakeTypeOf(globaltype);

            expr = MakeSimpleMemberAccess(expr, GenerateSimpleName("Module"));
            argList = MakeArgumentList(MakeArgument(expr));
            expr = GenerateMethodCall(SystemQualifiedNames.GetHInstance, argList);
            context.Put(expr);
            return true;
        }

        private bool GenerateChr(XP.MethodCallContext context)
        {
            // Pseudo function _Chr and Chr()
            ArgumentListSyntax argList;
            ExpressionSyntax expr;
            int count = 0;
            if (context.ArgList != null)
            {
                argList = context.ArgList.Get<ArgumentListSyntax>();
                count = argList.Arguments.Count;
            }
            else
            {
                argList = null;
            }
            if (count != 1)
            {
                expr = context.Expr.Get<ExpressionSyntax>();
                string name = String.Empty;
                if (expr is IdentifierNameSyntax)
                {
                    name = ((IdentifierNameSyntax)expr).Identifier.Text;
                }
                else if (expr is GenericNameSyntax)
                {
                    name = ((GenericNameSyntax)expr).Identifier.Text;
                }

                context.Put(GenerateLiteral(0).WithAdditionalDiagnostics(new SyntaxDiagnosticInfo(ErrorCode.ERR_BadArgCount, name, count)));
                return true;
            }
            var arg = argList.Arguments[0];
            var exp = arg.Expression;
            var lit = exp.XNode.GetLiteralToken();
            bool resolvelater = false;
            if (lit != null && (lit.Type == XP.INT_CONST || lit.Type == XP.HEX_CONST || lit.Type == XP.BIN_CONST))
            {
                // get number and create a string literal value
                var value = lit.SyntaxLiteralValue(_options);
                Int64 number = Convert.ToInt64(value.Value);
                char ch = ' ';
                bool overflow = false; ;
                if (number < UInt16.MaxValue)
                    ch = (char)number;
                else
                    overflow = true;
                var literal = GenerateLiteral(ch.ToString());
                if (overflow)
                {
                    literal = literal.WithAdditionalDiagnostics(new SyntaxDiagnosticInfo(ErrorCode.ERR_IntOverflow));
                }
                if (number >= 0 && number <= 127)	// Also allow Chr(0)
                {
                    context.Put(literal);
                    return true;
                }
                else if (CurrentEntity is XP.VodefineContext)
                {
                    if (!overflow)
                    {
                        literal = literal.WithAdditionalDiagnostics(new SyntaxDiagnosticInfo(ErrorCode.WRN_ChrInDefine));
                    }
                    context.Put(literal);
                    return true;
                }
            }
            else
            {
                resolvelater = true;
            }
            ExpressionSyntax mcall;
            if (_options.HasRuntime)
            {
                mcall = GenerateMethodCall(_options.XSharpRuntime ? XSharpQualifiedFunctionNames.Chr : VulcanQualifiedFunctionNames.Chr, argList);
            }
            else
            {
                mcall = GenerateMethodCall(context.Expr.GetText(), argList);
            }
            mcall.XIsChr = resolvelater;
            context.Put(mcall);
            return true;
        }

        public override void ExitMethodCall([NotNull] XP.MethodCallContext context)
        {
            var expr = context.Expr.Get<ExpressionSyntax>();
            string name = String.Empty;
            if (expr is IdentifierNameSyntax)
            {
                name = ((IdentifierNameSyntax)expr).Identifier.Text.ToUpper();
            }
            else if (expr is GenericNameSyntax)
            {
                name = ((GenericNameSyntax)expr).Identifier.Text.ToUpper();
            }
            switch (name)
            {
                case XSharpIntrinsicNames.AltD:
                    if (GenerateAltD(context))
                        return;
                    break;
                case XSharpIntrinsicNames.SLen:
                    if (GenerateSLen(context))
                        return;
                    break;

                case XSharpIntrinsicNames.GetInst:
                    if (GenerateGetInst(context))
                        return;
                    break;
                case XSharpIntrinsicNames.Chr:
                case XSharpIntrinsicNames._Chr:
                    if (GenerateChr(context))
                        return;
                    break;
                    //case "PCALL":
                    //case "CCALL":
                    //case "PCALLNATIVE":
                    //case "CCALLNATIVE":
                    //    expr = NotInDialect(expr, name + " pseudo function");
                    //    break;
            }
            ArgumentListSyntax argList;
            if (context.ArgList != null)
            {
                argList = context.ArgList.Get<ArgumentListSyntax>();
            }
            else
            {
                argList = EmptyArgumentList();
            }
            context.HasRefArguments = HasRefArguments(argList,context);
            context.Put(_syntaxFactory.InvocationExpression(expr, argList));

        }
        protected bool HasRefArguments(ArgumentListSyntax list, XSharpParserRuleContext context)
        {
            // Check for parameters by reference
            for (int i = 0; i < list.Arguments.Count; i++)
            {
                var arg = list.Arguments[i];
                if ((arg.Expression is PrefixUnaryExpressionSyntax pes
                    && pes.Kind == SyntaxKind.AddressOfExpression
                    && _options.HasOption(CompilerOption.ImplicitCastsAndConversions, context, PragmaOptions))
                    || arg.RefKindKeyword != null)
                {
                return true;
                }
            }
            return false;
        }

        public override void ExitDoStmt([NotNull] XP.DoStmtContext context)
        {
            var name = context.Id.GetText();
            ArgumentListSyntax argList;
            if (context.ArgList != null)
            {
                argList = context.ArgList.Get<ArgumentListSyntax>();
            }
            else
            {
                argList = EmptyArgumentList();
            }
            // DO .. WITH ...,... passes arguments by reference
            if (argList.Arguments.Count > 0)
            {
                var newargs = new List<ArgumentSyntax>();
                for (int iArg = 0; iArg < argList.Arguments.Count; iArg++)
                {
                    var arg = argList.Arguments[iArg];
                    var exp = arg.Expression;
                    if (exp is IdentifierNameSyntax ins)
                    {
                        arg = MakeArgument(exp, true);
                        context.HasRefArguments = true;
                    }
                    newargs.Add(arg);
                }
                argList = MakeArgumentList(newargs.ToArray());
                context.ArgList.Put(argList);
            }
            var expr = GenerateMethodCall(name, argList);
            context.Put(GenerateExpressionStatement(expr));
        }

        private bool GenerateSLen(XP.MethodCallContext context)
        {
            // Pseudo function SLen
            ArgumentListSyntax argList;
            ExpressionSyntax expr;
            if (context.ArgList != null)
            {
                argList = context.ArgList.Get<ArgumentListSyntax>();
            }
            else
            {
                return false;
            }
            if (argList.Arguments.Count != 1)
                return false;
            expr = argList.Arguments[0].Expression;
            expr = MakeCastTo(stringType, expr);
            var cond = _syntaxFactory.BinaryExpression(SyntaxKind.EqualsExpression,
                        expr,
                        SyntaxFactory.MakeToken(SyntaxKind.EqualsEqualsToken),
                        GenerateLiteralNull());
            var left = GenerateLiteral(0);
            var right = MakeSimpleMemberAccess(expr, GenerateSimpleName("Length"));
            expr = MakeConditional(cond, left, right);
            expr = MakeCastTo(_syntaxFactory.PredefinedType(SyntaxFactory.MakeToken(SyntaxKind.UIntKeyword)), expr);
            context.Put(expr);
            return true;
        }
        public override void ExitCtorCall([NotNull] XP.CtorCallContext context)
        {
            if (!(context.Type is XP.ArrayDatatypeContext))
            {
                var type = context.Type.Get<TypeSyntax>();
                ArgumentListSyntax argList;
                InitializerExpressionSyntax init = null;
                if (context.ArgList != null)
                {
                    argList = context.ArgList.Get<ArgumentListSyntax>();
                }
                else
                {
                    argList = EmptyArgumentList();
                }
                context.HasRefArguments = HasRefArguments(argList, context);
                if (context.Init != null)
                {
                    init = context.Init.Get<InitializerExpressionSyntax>();
                }
                context.Put(CreateObject(type, argList, init));
            }
            else
            {
                var type = (context.Type as XP.ArrayDatatypeContext).TypeName.Get<TypeSyntax>();
                var arrayType = context.Type.Get<ArrayTypeSyntax>();
                var rankSpecifiers = new ArrayRankSpecifierSyntax[arrayType.RankSpecifiers.Count];
                for (int i = 0; i < rankSpecifiers.Length; i++)
                {
                    rankSpecifiers[i] = arrayType.RankSpecifiers[i];
                }
                int ranks = rankSpecifiers[0].Sizes.Count;
                if (ranks != context.ArgList?._Args?.Count)
                    context.AddError(new ParseErrorData(context, ErrorCode.ERR_BadCtorArgCount, context.Type.GetText(), context.ArgList?._Args?.Count ?? 0));
                var sizes = _pool.AllocateSeparated<ExpressionSyntax>();
                if (context.ArgList?._Args != null)
                {
                    foreach (var size in context.ArgList?._Args)
                    {
                        if (size.Name != null)
                            context.AddError(new ParseErrorData(size, ErrorCode.ERR_BadNamedArgument, size));
                        if (size.RefOut != null)
                            context.AddError(new ParseErrorData(size, ErrorCode.ERR_BadTypeArgument, size));
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
                    context.Init?.Get<InitializerExpressionSyntax>() ?? null));
            }
        }

        public override void ExitDelegateCtorCall([NotNull] XP.DelegateCtorCallContext context)
        {
            if (((context.Obj as XP.PrimaryExpressionContext)?.Expr as XP.LiteralExpressionContext)?.Literal.Token.Type == XP.NULL)
            {
                context.Put(MakeCastTo(context.Type.Get<TypeSyntax>(), context.Func.Get<NameSyntax>()));
            }
            else
            {
                var fobj = context.Obj.Get<ExpressionSyntax>();
                SimpleNameSyntax fname;
                if (context.Func.CsNode is SimpleNameSyntax)
                {
                    fname = context.Func.Get<SimpleNameSyntax>();
                }
                else
                {
                    var fCtx = context.Func as XP.QualifiedNameContext;
                    if (fCtx != null)
                    {
                        fobj = MakeCastTo(fCtx.Left.Get<NameSyntax>(), fobj);
                        fname = fCtx.Right.Get<SimpleNameSyntax>();
                    }
                    else
                    {
                        fname = _syntaxFactory.IdentifierName(SyntaxFactory.MakeIdentifier("<missing>"));
                        context.AddError(new ParseErrorData(fCtx, ErrorCode.ERR_IdentifierExpected));
                    }
                }
                context.Put(MakeCastTo(
                    context.Type.Get<TypeSyntax>(),
                    MakeSimpleMemberAccess(fobj, fname)
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
            context.Put(_syntaxFactory.ElementAccessExpression(expr, argList));
        }

        public override void ExitNameExpression([NotNull] XP.NameExpressionContext context)
        {
            ExpressionSyntax expr = context.Name.Get<NameSyntax>();
            context.Put(expr);
        }


        public override void ExitIifExpression([NotNull] XP.IifExpressionContext context)
        {
            context.Put(context.Expr.Get<ExpressionSyntax>());
        }

        public override void ExitParenExpression([NotNull] XP.ParenExpressionContext context)
        {

            if (context._Exprs.Count == 1)
            {
                context.Put(_syntaxFactory.ParenthesizedExpression(
                    SyntaxFactory.MakeToken(SyntaxKind.OpenParenToken),
                    context.Expr.Get<ExpressionSyntax>(),
                    SyntaxFactory.MakeToken(SyntaxKind.CloseParenToken)));
            }
            else
            {
                // move the expressions into a local function
                // and call this local function here
                var statements = new List<StatementSyntax>();
                var last = context._Exprs.Last();
                ExpressionSyntax expr;
                // create statements too so we can set breakpoint info
                var decl = GenerateLocalDecl("_", objectType);
                statements.Add(decl);
                var name = GenerateSimpleName("_");
                statements.Add(GenerateExpressionStatement(MakeSimpleAssignment(name, GenerateLiteralNull())));
                foreach (var e in context._Exprs)
                {
                    StatementSyntax stmt;
                    expr = e.Get<ExpressionSyntax>();
                    var stmtctx = new XP.StatementContext(context, 0);
                    stmtctx.Start = e.Start;
                    stmtctx.Stop = e.Stop;
                    
                    if (e == last)
                    {
                        var ctx = new XP.ReturnStmtContext(stmtctx) { Expr = e };
                        stmt = GenerateReturn(expr);
                        ctx.Put(stmt);
                        stmtctx = ctx;
                    }
                    else
                    {
                        var ctx = new XP.ExpressionStmtContext(stmtctx);
                        ctx._Exprs.Add(e);
                        switch (expr)
                        {
                            case BinaryExpressionSyntax _:
                            case InvocationExpressionSyntax _:
                            case PostfixUnaryExpressionSyntax _:
                            case PrefixUnaryExpressionSyntax _:
                            case AssignmentExpressionSyntax _:
                                break;
                            case LiteralExpressionSyntax _:
                            default:
                                expr = MakeSimpleAssignment(name, expr);
                                break;
                        }
                        stmt = GenerateExpressionStatement(expr);

                        ctx.Put(stmt);
                        stmtctx = ctx;
                    }
                    stmtctx.SetSequencePoint(e.Start);
                    statements.Add(stmt);
                }
                var mods = TokenList(SyntaxKind.PrivateKeyword);
                IList<object> localFunctions ;
                if (CurrentEntity is XP.IBodyWithLocalFunctions ieb)
                {
                    if (ieb.LocalFunctions == null)
                    {
                        ieb.LocalFunctions = new List<object>();
                    }
                    localFunctions = ieb.LocalFunctions;
                }
                else
                {
                    localFunctions = new List<object>();
                }
                var fname = CurrentEntity.ShortName + XSharpSpecialNames.ParenExprSuffix+ (localFunctions.Count + 1).ToString();
                var id = SyntaxFactory.MakeIdentifier(fname);
                var func = _syntaxFactory.LocalFunctionStatement(
                                    mods,
                                    DefaultType(),
                                    id,
                                    typeParameterList: null,
                                    parameterList: EmptyParameterList(),
                                    constraintClauses: null,
                                    MakeBlock(statements),
                                    expressionBody: null,
                                    SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken));
                localFunctions.Add(func);
                expr = GenerateMethodCall(fname, false);
                context.Put(_syntaxFactory.ParenthesizedExpression(
                    SyntaxFactory.MakeToken(SyntaxKind.OpenParenToken),
                    expr,
                    SyntaxFactory.MakeToken(SyntaxKind.CloseParenToken)));

            }
        }

        public override void ExitIntrinsicExpression([NotNull] XP.IntrinsicExpressionContext context)
        {
            var kind = context.Op.ExpressionKindBinaryOp();
            var syntax = context.Op.SyntaxOp();
            if (kind == SyntaxKind.BitwiseNotExpression)
            {
                if (context._Exprs.Count > 1)
                {
                    context.AddError(new ParseErrorData(context.COMMA()[0], ErrorCode.ERR_CloseParenExpected));
                }
                context.Put(_syntaxFactory.PrefixUnaryExpression(
                    kind,
                    syntax,
                    context._Exprs[0].Get<ExpressionSyntax>()));
            }
            else
            {
                var e = context._Exprs[0].Get<ExpressionSyntax>();
                if (context._Exprs.Count > 1)
                {
                    for (int i = 1; i < context._Exprs.Count; i++)
                    {
                        e = _syntaxFactory.BinaryExpression(kind,
                                                            e,
                                                            syntax,
                                                            context._Exprs[i].Get<ExpressionSyntax>());
                    }
                    context.Put(e);
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
            if (context.Op.Type == XP.IS)
            {
                if (context.Id != null)
                {
                    var id = SyntaxFactory.Identifier(context.Id.GetText());
                    var designation = _syntaxFactory.SingleVariableDesignation(id);
                    var pattern =  _syntaxFactory.DeclarationPattern(context.Type.Get<TypeSyntax>(), designation);
                    context.Put(_syntaxFactory.IsPatternExpression(
                        context.Expr.Get<ExpressionSyntax>(),
                        SyntaxFactory.MakeToken(SyntaxKind.IsKeyword),
                        (PatternSyntax)pattern));
                }
                else
                { 
                    context.Put(_syntaxFactory.BinaryExpression(
                        SyntaxKind.IsExpression,
                        context.Expr.Get<ExpressionSyntax>(),
                        SyntaxFactory.MakeToken(SyntaxKind.IsKeyword),
                        context.Type.Get<ExpressionSyntax>()));
                }
            }
            else if (context.Op.Type == XP.ASTYPE)
            {
                context.Put(_syntaxFactory.BinaryExpression(
                    SyntaxKind.AsExpression,
                    context.Expr.Get<ExpressionSyntax>(),
                    SyntaxFactory.MakeToken(SyntaxKind.AsKeyword),
                    context.Type.Get<ExpressionSyntax>()));
            }

        }

        public override void ExitTypeCast([NotNull] XP.TypeCastContext context)
        {
            var expr = context.Expr.Get<ExpressionSyntax>();
            expr = MakeCastTo(context.Type.Get<TypeSyntax>(), expr);
            // If the expression is part of a CHECKED or UNCHECKED
            // Syntax then there is no need to explicitely add the Checked
            // for example C578: 
            // DEFINE d2 := unchecked ((WORD) -1)
            if (_options.HasRuntime  && _options.TargetDLL  == XSharpTargetDLL.Other && !(context.Parent is XP.CheckedExpressionContext))
            {
                expr = MakeChecked(expr, _options.Overflow);
            }
            context.Put(expr);
        }

        public override void ExitVoConversionExpression([NotNull] XP.VoConversionExpressionContext context)
        {
            var expr = MakeChecked(context.Expr.Get<ExpressionSyntax>(), false);
            if (context.Type != null)
            {
                expr = MakeChecked(MakeCastTo(context.Type.Get<TypeSyntax>(), expr), false);
            }
            else if (context.XType != null)
            {
                expr = MakeChecked(MakeCastTo(context.XType.Get<TypeSyntax>(), expr),false);
            }
            context.Put(expr);
        }

        public override void ExitVoCastExpression([NotNull] XP.VoCastExpressionContext context)
        {
            TypeSyntax type;
            int mask = 0;
            if (context.Type != null)
            {
                type = context.Type.Get<TypeSyntax>();
                switch (context.Type.Token.Type)
                {
                    case XP.BYTE:
                        mask = 0xff;
                        break;
                    case XP.CHAR:
                    case XP.WORD:
                    case XP.SHORTINT:
                        mask = 0xffff;
                        break;
                }
            }
            else
            {
                type = context.XType.Get<TypeSyntax>();
            }
            var expr = context.Expr.Get<ExpressionSyntax>();
            // check for cast from a logical literal to a numeric
            bool numeric = false;
            bool signed = false;
            // in that case replace FALSE with 0 and TRUE with 1
            if (type is PredefinedTypeSyntax)
            {
                var pdt = type as PredefinedTypeSyntax;
                switch (pdt.Keyword.Kind)
                {
                    // 4 x unsigned
                    case SyntaxKind.ByteKeyword:
                    case SyntaxKind.CharKeyword:
                    case SyntaxKind.UShortKeyword:
                    case SyntaxKind.UIntKeyword:
                    case SyntaxKind.ULongKeyword:
                        signed = false;
                        numeric = true;
                        break;
                    // 4 x signed
                    case SyntaxKind.SByteKeyword:
                    case SyntaxKind.ShortKeyword:
                    case SyntaxKind.IntKeyword:
                    case SyntaxKind.LongKeyword:
                    // floating point
                    case SyntaxKind.FloatKeyword:
                    case SyntaxKind.DoubleKeyword:
                    case SyntaxKind.DecimalKeyword:
                        signed = true;
                        numeric = true;
                        break;
                }
            }
            if (numeric)
            {
                if (expr.Kind == SyntaxKind.TrueLiteralExpression || expr.Kind == SyntaxKind.FalseLiteralExpression)
                {
                    LiteralExpressionSyntax lit = expr as LiteralExpressionSyntax;
                    if (lit.Kind == SyntaxKind.TrueLiteralExpression)
                    {
                        expr = GenerateLiteral(1);
                    }
                    else
                    {
                        expr = GenerateLiteral(0);
                    }
                }
                else if (mask != 0)
                {
                    var destType = signed ? _intType : _syntaxFactory.PredefinedType(SyntaxFactory.MakeToken(SyntaxKind.UIntKeyword));
                    expr = MakeCastTo(destType, expr);
                    expr = MakeChecked(expr, false);

                }
            }
            if (mask != 0)
            {
                expr = MakeChecked(_syntaxFactory.BinaryExpression(
                        SyntaxKind.BitwiseAndExpression,
                        expr,
                        SyntaxFactory.MakeToken(SyntaxKind.AmpersandToken),
                        GenerateLiteral(mask)), false);
            }
            context.Put(MakeChecked(MakeCastTo(type, expr), false));
            return;
        }

        public override void ExitVoCastPtrExpression([NotNull] XP.VoCastPtrExpressionContext context)
        {
            if (context.Expr is XP.MethodCallContext)
            {
                var expr = GenerateLiteral(0).WithAdditionalDiagnostics(
                    new SyntaxDiagnosticInfo(ErrorCode.ERR_PtrCastNotAllowed));
                context.Put(expr);
            }
            else
            {
                context.Put(MakeCastTo(
                    _syntaxFactory.PointerType(context.Type.Get<TypeSyntax>(), SyntaxFactory.MakeToken(SyntaxKind.AsteriskToken)),
                    _syntaxFactory.PrefixUnaryExpression(SyntaxKind.AddressOfExpression,
                        SyntaxFactory.MakeToken(SyntaxKind.AmpersandToken),
                        context.Expr.Get<ExpressionSyntax>())));
            }
        }

        public override void ExitSizeOfExpression([NotNull] XP.SizeOfExpressionContext context)
        {
            context.Put(
                _syntaxFactory.SizeOfExpression(
                SyntaxFactory.MakeToken(SyntaxKind.SizeOfKeyword),
                SyntaxFactory.MakeToken(SyntaxKind.OpenParenToken),
                context.Type.Get<TypeSyntax>(),
                SyntaxFactory.MakeToken(SyntaxKind.CloseParenToken)));
        }

        public override void ExitTypeOfExpression([NotNull] XP.TypeOfExpressionContext context)
        {
            context.Put(MakeTypeOf(context.Type.Get<TypeSyntax>()));
        }

        public override void ExitDefaultExpression([NotNull] XP.DefaultExpressionContext context)
        {
            context.Put(MakeDefault(context.Type.Get<TypeSyntax>()));
        }

        public override void ExitAwaitExpression([NotNull] XP.AwaitExpressionContext context)
        {
            context.Put(_syntaxFactory.AwaitExpression(SyntaxFactory.MakeToken(SyntaxKind.AwaitKeyword), context.Expr.Get<ExpressionSyntax>()));
        }

        public override void ExitSelfExpression([NotNull] XP.SelfExpressionContext context)
        {
            context.Put(GenerateSelf());
        }

        public override void ExitSuperExpression([NotNull] XP.SuperExpressionContext context)
        {
            context.Put(GenerateSuper());
        }

        public override void ExitArgListExpression([NotNull] XP.ArgListExpressionContext context)
        {
            context.Put(_syntaxFactory.LiteralExpression(SyntaxKind.ArgListExpression, SyntaxFactory.MakeToken(SyntaxKind.ArgListKeyword)));
        }


        #endregion

        #region Arguments 
        static private ArgumentListSyntax _emptyArgs = null;
        protected ArgumentListSyntax EmptyArgumentList()
        {
            if (_emptyArgs == null)
            {
                lock (gate)
                {
                    if (_emptyArgs == null)
                    {
                        _emptyArgs = _syntaxFactory.ArgumentList(
                                SyntaxFactory.MakeToken(SyntaxKind.OpenParenToken),
                                default(SeparatedSyntaxList<ArgumentSyntax>),
                                SyntaxFactory.MakeToken(SyntaxKind.CloseParenToken));
                    }
                }
            }
            return _emptyArgs;
        }

        static private BracketedArgumentListSyntax _emptyBracketedArgs = null;
        protected BracketedArgumentListSyntax EmptyBracketedArgumentList()
        {
            if (_emptyBracketedArgs == null)
            {
                lock (gate)
                {

                    if (_emptyBracketedArgs == null)
                    {
                        _emptyBracketedArgs = _syntaxFactory.BracketedArgumentList(
                        SyntaxFactory.MakeToken(SyntaxKind.OpenBracketToken),
                        default(SeparatedSyntaxList<ArgumentSyntax>),
                        SyntaxFactory.MakeToken(SyntaxKind.CloseBracketToken));
                    }
                }
            }
            return _emptyBracketedArgs;
        }

        protected ArgumentListSyntax MakeArgumentList(params ArgumentSyntax[] items)
        {
            return _syntaxFactory.ArgumentList(
                    SyntaxFactory.MakeToken(SyntaxKind.OpenParenToken),
                    MakeSeparatedList<ArgumentSyntax>(items),
                    SyntaxFactory.MakeToken(SyntaxKind.CloseParenToken));
        }

        protected BracketedArgumentListSyntax MakeBracketedArgumentList(params ArgumentSyntax[] items)
        {
            return _syntaxFactory.BracketedArgumentList(
                    SyntaxFactory.MakeToken(SyntaxKind.OpenBracketToken),
                    MakeSeparatedList<ArgumentSyntax>(items),
                    SyntaxFactory.MakeToken(SyntaxKind.CloseBracketToken));
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

        public override void ExitUnnamedArgument([NotNull] XP.UnnamedArgumentContext context)
        {
            if (context.Expr == null)
            {
                context.Put(MakeArgument(GenerateMissingExpression(_options.Dialect == XSharpDialect.Core)));
                return;
            }
            context.Put(MakeArgument(context.Expr.Get<ExpressionSyntax>()));
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

        public override void ExitNamedArgument([NotNull] XP.NamedArgumentContext context)
        {
            /*
           namedArgument       :  {AllowNamedArgs}?  Name=identifierName Op=assignoperator  ( RefOut=(REF | OUT) )? Expr=expression?
                                |   RefOut=OUT Var=VAR  Id=identifier
                                |   RefOut=OUT Null=NULL
                                |  ( RefOut=(REF | OUT) )? Expr=expression?
                              ;
           */
            var refKeyword = context.RefOut?.SyntaxKeyword();
            if (context.Null != null || context.Id != null)
            {
                TypeSyntax type = context.Type != null ? context.Type.Get<TypeSyntax>() : _impliedType;
                VariableDesignationSyntax desig;
                if (context.Null != null || context.Id.GetText() == "_")
                {
                    var token = SyntaxFactory.MakeToken(SyntaxKind.UnderscoreToken, "_");
                    desig = _syntaxFactory.DiscardDesignation(token);
                }
                else
                {
                    var token = context.Id.Get<SyntaxToken>();
                    desig = _syntaxFactory.SingleVariableDesignation(token);
                }
                var decl = _syntaxFactory.DeclarationExpression(type, desig);
                var arg = _syntaxFactory.Argument(null, refKeyword, decl);
                context.Put(arg);
                return;
            }
            if (context.Expr == null)
            {
                context.Put(MakeArgument(GenerateMissingExpression(_options.Dialect == XSharpDialect.Core)));
                return;
            }
            var expr = context.Expr.Get<ExpressionSyntax>();
            if (expr is PrefixUnaryExpressionSyntax pues && pues.OperatorToken.Kind == SyntaxKind.AmpersandToken)
            {
                bool alwaysByRef = _options.Dialect.AddressOfIsAlwaysByRef() ;
                if (alwaysByRef)
                {
                    var xnode = pues.XNode as XP.PrefixExpressionContext;
                    if (xnode.Op.Type == XP.ADDROF)
                    {
                        expr = pues.Operand;
                        refKeyword = SyntaxFactory.MakeToken(SyntaxKind.RefKeyword, xnode.Op.Text);
                    }
                }
            }
            context.Put(_syntaxFactory.Argument(
                context.Name == null ? null : _syntaxFactory.NameColon(context.Name.Get<IdentifierNameSyntax>(), SyntaxFactory.MakeToken(SyntaxKind.ColonToken)),
                refKeyword, expr));
        }

        #endregion

        #region Names and Identifiers


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
        public override void ExitIdentifierName([NotNull] XP.IdentifierNameContext context)
        {
            context.Put(_syntaxFactory.IdentifierName(context.Id.Get<SyntaxToken>()));
        }

        public override void ExitVaridentifierName([NotNull] XP.VaridentifierNameContext context)
        {
            context.Put(context.Id.Get<IdentifierNameSyntax>());
        }


        public override void ExitIdentifierString([NotNull] XP.IdentifierStringContext context)
        {
            context.Put(GenerateLiteral(context.Start.Text));
        }

        public override void ExitVaridentifier([NotNull] XP.VaridentifierContext context)
        {
            context.Put(context.Id.Get<SyntaxToken>());
        }

        public override void ExitIdentifier([NotNull] XP.IdentifierContext context)
        {
            // Value in a property accessor will be converted to "value" but only when not part of a access member
            if (CurrentEntity is XP.PropertyLineAccessorContext || CurrentEntity is XP.PropertyAccessorContext)
            {
                int key = 0;
                if (CurrentEntity is XP.PropertyLineAccessorContext plac)
                {
                    key = plac.Key.Type;
                }
                else if (CurrentEntity is XP.PropertyAccessorContext pac)
                {
                    key = pac.Key.Type;
                }
                // Only inside SET accessor we make sure that "value" is lowercase
                if (key == XSharpParser.SET)
                {
                    // something like SELF:Value should not be lowercased
                    if (!(context.Parent is XP.AccessMemberContext))
                    {
                        if (String.Compare(context.Start.Text, "value", true) == 0)
                        {
                            var token = context.Start as XSharpToken;
                            token.Text = token.Text.ToLower();
                        }
                    }
                }
            }
            context.Put(context.Start.SyntaxIdentifier());
        }


        public override void ExitKeywordxs([NotNull] XP.KeywordxsContext context)
        {
            // caught by the keyword/identifier rule
        }


        public override void ExitKeywordvo([NotNull] XP.KeywordvoContext context)
        {
            // caught by the keyword/identifier rule
        }

        public override void ExitKeywordxpp([NotNull] XP.KeywordxppContext context)
        {
            // caught by the keyword/identifier rule
        }
        public override void ExitSimpleName([NotNull] XP.SimpleNameContext context)
        {
            if (context.GenericArgList == null || context.GenericArgList._GenericArgs.Count == 0)
                context.Put(_syntaxFactory.IdentifierName(context.Id.Get<SyntaxToken>()));
            else
                context.Put(_syntaxFactory.GenericName(context.Id.Get<SyntaxToken>(), context.GenericArgList.Get<TypeArgumentListSyntax>()));
        }


        #endregion

        #region Data Types


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
            bool hasError = false;
            char errorchar = '\0'; 
            var omittedArraySizeExpressionInstance = _syntaxFactory.OmittedArraySizeExpression(SyntaxFactory.MakeToken(SyntaxKind.OmittedArraySizeExpressionToken));
            if (context.String != null)
            {
                string text = context.String.Text;
                for (int i = 1; i < text.Length - 1; i++)
                {
                    char c = text[i];
                    if (c == ',')
                    {
                        sizes.Add(omittedArraySizeExpressionInstance);
                        sizes.AddSeparator(SyntaxFactory.MakeToken(SyntaxKind.CommaToken));
                    }
                    else if (!char.IsWhiteSpace(c))
                    {
                        errorchar = text[i];
                        hasError = true;
                        break;
                    }
                }
            }
            else
            {
                foreach (var comma in context._Commas)
                {
                    sizes.Add(omittedArraySizeExpressionInstance);
                    sizes.AddSeparator(SyntaxFactory.MakeToken(SyntaxKind.CommaToken));
                }

            }
            sizes.Add(omittedArraySizeExpressionInstance);
            var ars = _syntaxFactory.ArrayRankSpecifier(
                SyntaxFactory.MakeToken(SyntaxKind.OpenBracketToken),
                sizes,
                SyntaxFactory.MakeToken(SyntaxKind.CloseBracketToken));
            if (hasError)
            {
                ars = ars.WithAdditionalDiagnostics(new SyntaxDiagnosticInfo(ErrorCode.ERR_UnexpectedCharacter, errorchar));
            }
            context.Put(ars);
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

        #endregion

        #region Literal Expressions
        public override void ExitLiteralExpression([NotNull] XP.LiteralExpressionContext context)
        {
            context.Put(context.Literal.Get<ExpressionSyntax>());
        }

        public override void ExitLiteralArrayExpression([NotNull] XP.LiteralArrayExpressionContext context)
        {
            context.Put(context.LiteralArray.Get<ExpressionSyntax>());
        }

        protected ExpressionSyntax CreateInterPolatedStringExpression(IToken token)
        {
            string text = token.Text;
            bool extended = false;
            // cut off i" and ", could also be ei" and ie"
            if (text[1] == '"')
            {
                text = text.Substring(2, text.Length - 3);
            }
            else
            {
                text = text.Substring(3, text.Length - 4);
                extended = true;
            }
            ExpressionSyntax result = GenerateLiteral(text);
            var pos1 = text.IndexOf("{");
            var pos2= text.IndexOf("}");
            if (pos1 == -1 && pos2 == -1)
            {
                // no curly braces, so nothing to do
                if (extended)
                {
                    text = "e\"" + text + "\"";
                    result = GenerateLiteral(TokenExtensions.EscapedStringValue(text));
                }

                return result;
            }
            if (pos1 == -1 || pos2 == -1 || pos1 > pos2)
            {
                if (pos1 == -1)
                {
                    result= result.WithAdditionalDiagnostics(new SyntaxDiagnosticInfo(ErrorCode.ERR_UnexpectedCharacter, "}"));
                }
                else
                {
                    result = result.WithAdditionalDiagnostics(new SyntaxDiagnosticInfo(ErrorCode.ERR_UnclosedExpressionHole));
                }
                return result;
            }

            var expressions = new List<string>();
            var sbMask = new StringBuilder();
            var sbExpr = new StringBuilder();
            StringBuilder sbCurrent = sbMask;
            bool afterBackSlash = false;
            bool inString = false;
            int nestLevel = 0;
            char last = '\0';
            foreach (char c in text)
            {
                var charToAppend = c;
                if (inString && c != '\\' && !afterBackSlash)
                {
                    sbCurrent.Append(c);
                    last = c; 
                    continue;
                }
                else
                {
                    switch (c)
                    {
                        case '{':
                            if (last != '{')        // double { is escaped {
                            {
                                nestLevel++;
                                if (nestLevel == 1)
                                {
                                    sbCurrent = sbExpr;
                                    sbMask.Append("{" + expressions.Count.ToString() + "}");
                                    last = c;
                                    continue;
                                }
                            }
                            else
                            {
                                ;// normal processing so we add a {
                            }
                            break;
                        case '}':
                            if (last != '}')// double } is escaped }
                            {
                                nestLevel--;
                                if (nestLevel == 0)
                                {
                                    sbCurrent = sbMask;
                                    expressions.Add(sbExpr.ToString());
                                    sbExpr.Clear();
                                    last = c;
                                    continue;
                                }
                            }
                            else
                            {
                                ;// normal processing so we add a }
                            }
                            break;
                        case '\\':
                            if (extended)
                            {
                                if (afterBackSlash)
                                {
                                    afterBackSlash = false;
                                    // normal processing so we add a \
                                }
                                else
                                {
                                    afterBackSlash = true;
                                    last = c;
                                    // do not add character
                                    continue;
                                }
                            }
                            break;
                        case '"':
                        case 'r':
                        case 't':
                        case 'n':
                        case 'R':
                        case 'T':
                        case 'N':
                            if (afterBackSlash)
                            {
                                if (c == 'n' || c == 'N')
                                {
                                    charToAppend = '\n';
                                }
                                else if (c == 'r' || c == 'R')
                                {
                                    charToAppend = '\r';
                                }
                                else if (c == 't' || c == 'T')
                                {
                                    charToAppend = '\t';
                                }
                                else
                                {
                                    // normal processing add "
                                    inString = !inString;
                                }
                                afterBackSlash = false;
                            }
                            break;
                    }
                }
                sbCurrent.Append(charToAppend);
                last = charToAppend;
            }

            if (nestLevel != 0)
            {
                result = result.WithAdditionalDiagnostics(new SyntaxDiagnosticInfo(ErrorCode.ERR_UnclosedExpressionHole));
                return result;
            }
            // now we have a list of expressions and a mask
            // parse each individual expression
            var exprSyntax = new List<ExpressionSyntax>();
            int iparam = 0;
            string sMask = sbMask.ToString();
            ExpressionSyntax res;
            foreach (var e in expressions)
            {
                string extra;
                string format = null;
                string expr = e;
                int pos = expr.IndexOf(":");
                if (pos > 0)
                {
                    var lhs = expr.Substring(0, pos).ToUpper();
                    if (lhs == "SELF" || lhs == "SUPER" || lhs == "THIS")
                    {
                        ; // do nothing. Assume SELF:, SUPER: and THIS: are not shown with format specifier
                    }
                    else
                    {
                        format = expr.Substring(pos);
                        expr = expr.Substring(0, pos);
                    }
                }
                res = ParseSubExpression(expr, out extra, token);
                if (!String.IsNullOrEmpty(format))
                {
                    format = format.Trim();
                    if (format.Length > 1 && (format[0] == ',' || format[0] == ':'))
                    {
                        sMask = sMask.Replace("{" + iparam.ToString() + "}", "{" + iparam.ToString() + format + "}");
                    }
                    else
                    {
                        res = res.WithAdditionalDiagnostics(new SyntaxDiagnosticInfo(ErrorCode.ERR_UnexpectedCharacter, format));
                    }
                }
                exprSyntax.Add(res);
                iparam++;
            }

            // now we have a list of expression syntax node and a mask, so we can call String.Format
            var args = new List<ArgumentSyntax>();
            args.Add(MakeArgument(GenerateLiteral(sMask)));
            foreach (var expr in exprSyntax)
            {
                args.Add(MakeArgument(expr));
            }
            result = GenerateMethodCall("String.Format", MakeArgumentList(args.ToArray()), true);
            return result;
        }

        protected int[] DecodeDateTimeConst(string dateliteral)
        {
            string[] args;
            dateliteral = dateliteral.Trim();
            if (dateliteral.StartsWith("{^") && dateliteral.EndsWith("}"))
            {
                // Foxpro date time format
                dateliteral = dateliteral.Substring(2, dateliteral.Length - 3);
                args = dateliteral.Split("- :".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                if (args.Length >= 4)
                {
                    int mins = 0;
                    int secs = 0;
                    var ok = false;
                    int year=0, month=0, day=0, hour=0;
                    ok = int.TryParse(args[0], out year) &&
                        int.TryParse(args[1], out month) &&
                        int.TryParse(args[2], out day) &&
                        int.TryParse(args[3], out hour);

                    if (ok && args.Length > 4)
                    { 
                        ok = int.TryParse(args[4], out mins);
                    }
                    if (ok && args.Length > 5)
                    { 
                        int.TryParse(args[5], out secs);
                    }
                    if (ok)
                    {
                        if (args.Length == 7)
                        {
                            bool pm = args[6].Trim().ToLower().StartsWith("p");
                            if (pm && hour < 12)
                                hour += 12;
                        }
                        return new int[] { year, month, day, hour, mins, secs };
                    }
                }
            }
            return null;
        }

        protected int[] DecodeDateConst(string dateliteral)
        {
            string[] args;
            dateliteral = dateliteral.Trim();
            if (dateliteral.StartsWith("{^") && dateliteral.EndsWith("}"))
            {
                // Foxpro date format
                dateliteral = dateliteral.Substring(2, dateliteral.Length - 3);
                args = dateliteral.Split('-');
                if (args.Length == 3)
                {
                    if (int.TryParse(args[0], out int year) &&
                        int.TryParse(args[1], out int month) &&
                        int.TryParse(args[2], out int day))
                    {
                        return new int[] { year, month, day };
                    }
                }
            }
            args = dateliteral.Split('.');
            if (args.Length == 3)
            {
                
                if (int.TryParse(args[0], out int year) &&
                    int.TryParse(args[1], out int month) &&
                    int.TryParse(args[2], out int day))
                {
                    return new int[] { year, month, day };
                }
            }
            return null;
        }


        public override void ExitLiteralValue([NotNull] XP.LiteralValueContext context)
        {
            int[] elements;
            switch (context.Token.Type)
            {
                case XP.BINARY_CONST:
                    var source = context.Token.Text.Substring(2);
                    var error = false;
                    if (source.Length % 2 != 0)
                    {
                        error = true;
                        source += "0";
                    }
                    var values = new List<ExpressionSyntax>();
                    for (var i = 0; i < source.Length; i+=2)
                    {
                        var substr = source.Substring(i, 2);
                        var value = (int) TokenExtensions.HexValue(substr);
                        values.Add(GenerateLiteral(value));
                    }
                    var bin = _syntaxFactory.ArrayCreationExpression(
                        SyntaxFactory.MakeToken(SyntaxKind.NewKeyword),
                            _byteArrayType,
                            _syntaxFactory.InitializerExpression(SyntaxKind.ArrayInitializerExpression,
                                SyntaxFactory.MakeToken(SyntaxKind.OpenBraceToken),
                                MakeSeparatedList<ExpressionSyntax>(values.ToArray()),
                                SyntaxFactory.MakeToken(SyntaxKind.CloseBraceToken)));
                    if (error)
                    {
                        var msg = "length of literal must be an even number of characters";
                        bin = bin.WithAdditionalDiagnostics(new SyntaxDiagnosticInfo(ErrorCode.ERR_InvalidLiteral, "Binary", context.Token.Text, msg));
                    }
                    context.Put(bin);
                    return;

                case XP.INTERPOLATED_STRING_CONST:
                    context.Put(CreateInterPolatedStringExpression(context.Token));
                    return;
                case XP.DATE_CONST:
                    elements = DecodeDateConst(context.Token.Text);
                    if (elements != null && elements.Length == 3)
                    {
                        try
                        {
                            var dt = new DateTime(elements[0], elements[1], elements[2],0,0,0);
                        }
                        catch (Exception e)
                        {
                            var result = GenerateLiteral(0);
                            result = result.WithAdditionalDiagnostics(new SyntaxDiagnosticInfo(ErrorCode.ERR_InvalidLiteral, "DATE", context.Token.Text, e.Message));
                            context.Put(result);
                            return;
                        }
                        var arg0 = MakeArgument(GenerateLiteral(elements[0]));
                        var arg1 = MakeArgument(GenerateLiteral(elements[1]));
                        var arg2 = MakeArgument(GenerateLiteral(elements[2]));
                        var expr = CreateObject(_dateTimeType, MakeArgumentList(arg0, arg1, arg2));
                        context.Put(expr);
                        return;
                    }
                    break;
                case XP.DATETIME_CONST:
                    elements = DecodeDateTimeConst(context.Token.Text);
                    if (elements != null && elements.Length >= 6)
                    {
                        try
                        { 
                              var dt = new DateTime(elements[0], elements[1], elements[2], elements[3], elements[4], elements[5]);
                        }
                        catch (Exception e)
                        {
                            var result = GenerateLiteral(0);
                            result = result.WithAdditionalDiagnostics(new SyntaxDiagnosticInfo(ErrorCode.ERR_InvalidLiteral, "DateTime", context.Token.Text, e.Message));
                            context.Put(result);
                            return;
                        }
                        var arg0 = MakeArgument(GenerateLiteral(elements[0]));
                        var arg1 = MakeArgument(GenerateLiteral(elements[1]));
                        var arg2 = MakeArgument(GenerateLiteral(elements[2]));
                        var arg3 = MakeArgument(GenerateLiteral(elements[3]));
                        var arg4 = MakeArgument(GenerateLiteral(elements[4]));
                        var arg5 = MakeArgument(GenerateLiteral(elements[5]));
                        var expr = CreateObject(_dateTimeType, MakeArgumentList(arg0, arg1, arg2, arg3, arg4, arg5));
                        context.Put(expr);
                        return;
                    }
                    break;
                default:
                    break;
            }
            string replacement = null;
            if (context.Token.Type == XP.STRING_CONST && context.Token.Text.StartsWith("\"__"))
            {
                switch (context.Token.Text.ToLowerInvariant())
                {
                    case "\"__entity__\"":
                        replacement = GetEntityName(false);
                        break;
                    case "\"__function__\"":
                        replacement = GetEntityName(false,true);
                        break;
                    case "\"__sig__\"":
                        replacement = GetEntityName(true);
                        break;
                    case "\"__functions__\"":
                        replacement = GlobalClassName;
                        break;
                    default:
                        break;
                }
            }

            if (!String.IsNullOrEmpty(replacement))
            {
                context.Put(_syntaxFactory.LiteralExpression(context.Token.ExpressionKindLiteral(),
                    SyntaxToken.WithValue(SyntaxKind.StringLiteralToken, replacement, replacement)));
            }
            else
            {
                context.Put(GenerateLiteral(context.Token,context));
                if (context.Token.Type == XP.INCOMPLETE_STRING_CONST)
                {
                    var litExpr = context.Get<LiteralExpressionSyntax>();
                    var diag = new SyntaxDiagnosticInfo(ErrorCode.ERR_UnterminatedStringLit);
                    context.Put(litExpr.WithAdditionalDiagnostics(diag));
                }
            }
        }
        public override void ExitLiteralArray([NotNull] XP.LiteralArrayContext context)
        {
            TypeSyntax type = null;
            ExpressionSyntax expr = null;
            // detect typed arrays.
            // <LONG> {...} indicates an array of type LONG
            // when no type is specified and the dialect VO or Vulcan the type is USUAL
            if (context.Type != null)
            {
                type = context.Type.Get<TypeSyntax>();
            }
            SeparatedSyntaxList<ExpressionSyntax> exprs;
            if ((context._Elements?.Count ?? 0) > 0)
            {
                //
                var l = _pool.AllocateSeparated<ExpressionSyntax>();
                foreach (var item in context._Elements)
                {
                    if (l.Count > 0)
                        l.AddSeparator(SyntaxFactory.MakeToken(SyntaxKind.CommaToken));
                    if (item.Expr != null)
                        l.Add(item.Expr.Get<ExpressionSyntax>());
                    else
                        l.Add(NotInDialect(GenerateLiteral(false), "omitting (typed) array elements"));
                }
                exprs = l.ToList();
                _pool.Free(l);
            }
            else
            {
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
                    EmptyList<SyntaxToken>(),
                    SyntaxFactory.MakeToken(SyntaxKind.CloseBracketToken),
                    initializer);
                if (!isNestedArray(context))
                {
                    expr = expr.WithAdditionalDiagnostics(new SyntaxDiagnosticInfo(ErrorCode.ERR_UntypedArrayNotAvailableInDialect, _options.Dialect.ToString()));
                }
            }
            context.Put(expr);
        }

        private bool isNestedArray(XP.LiteralArrayContext context)
        {
            var parent = context.Parent as ParserRuleContext;
            while (parent != null)
            {
                if (parent is XP.LiteralArrayContext)
                    return true;
                if (parent is XP.StatementContext)
                    return false;
                parent = parent.Parent as ParserRuleContext;
            }
            return false;
        }
        public override void ExitArrayElement([NotNull] XP.ArrayElementContext context)
        {
            if (context.Expr != null)
                context.Put(context.Expr.Get<ExpressionSyntax>());
            return;
        }



        #endregion

          
        #region Anonymous Types
        public override void ExitAnonTypeExpression([NotNull] XP.AnonTypeExpressionContext context)
        {
            context.Put(context.AnonType.Get<AnonymousObjectCreationExpressionSyntax>());
        }

 
        public override void ExitAnonType([NotNull] XP.AnonTypeContext context)
        {
            context.Put(_syntaxFactory.AnonymousObjectCreationExpression(
                SyntaxFactory.MakeToken(SyntaxKind.NewKeyword),
                SyntaxFactory.MakeToken(SyntaxKind.OpenBraceToken),
                MakeSeparatedList<AnonymousObjectMemberDeclaratorSyntax>(context._Members),
                SyntaxFactory.MakeToken(SyntaxKind.CloseBraceToken)));
        }

        private bool isAnonymousTypeExpression(ExpressionSyntax expr)
        {
            // COpyied from the Roslyn Language Parser
            while (true)
            {
                switch (expr.Kind)
                {
                    case SyntaxKind.QualifiedName:
                        expr = ((QualifiedNameSyntax)expr).Right;
                        continue;
                    case SyntaxKind.ConditionalAccessExpression:
                        expr = ((ConditionalAccessExpressionSyntax)expr).WhenNotNull;
                        if (expr.Kind == SyntaxKind.MemberBindingExpression)
                        {
                            return true;
                        }

                        continue;
                    case SyntaxKind.IdentifierName:
                    case SyntaxKind.SimpleMemberAccessExpression:
                        return true;
                    default:
                        return false;
                }
            }
        }
        public override void ExitAnonMember([NotNull] XP.AnonMemberContext context)
        {
            NameEqualsSyntax nameEquals = null;
            var expr = context.Expr.Get<ExpressionSyntax>();
            if (context.Name != null)
            {
                nameEquals = _syntaxFactory.NameEquals(context.Name.Get<IdentifierNameSyntax>(), SyntaxFactory.MakeToken(SyntaxKind.EqualsToken));
            }
            var amd = _syntaxFactory.AnonymousObjectMemberDeclarator(nameEquals, expr);
            {
                if (nameEquals == null && !isAnonymousTypeExpression(expr))
                {
                    amd = amd.WithAdditionalDiagnostics(new SyntaxDiagnosticInfo(ErrorCode.ERR_InvalidAnonymousTypeMemberDeclarator));
                }
            }
            context.Put(amd);
        }
        #endregion

        #region Codeblocks
        public override void ExitCodeblockExpression([NotNull] XP.CodeblockExpressionContext context)
        {
            if (context.CbExpr != null)
                context.Put(context.CbExpr.Get<ExpressionSyntax>());
            else if (context.AnoExpr != null)
                context.Put(context.AnoExpr.Get<ExpressionSyntax>());
        }

        public override void ExitLambdaParameterList([NotNull] XP.LambdaParameterListContext context)
        {
            ParameterListSyntax paramList;
            if (context.ImplicitParams != null)
                paramList = context.ImplicitParams.Get<ParameterListSyntax>();
            else
                paramList = context.ExplicitParams.Get<ParameterListSyntax>();
            context.Put(paramList);
        }

        public override void ExitCodeblockCode([NotNull] XP.CodeblockCodeContext context)
        {
            CSharpSyntaxNode  block;
            // Convert everything to a stmt like block
            // so it is easier to fix Void expressions as last expression in the list
            if (context.Expr != null)
            {
                context.SetSequencePoint(context.Expr.Start, context.Expr.Stop);
                block = context.Expr?.Get<ExpressionSyntax>();
            }
            else
            {
               block = context.StmtBlk?.Get<BlockSyntax>()
                        ?? context.ExprList?.Get<BlockSyntax>()
                        ?? MakeBlock(_syntaxFactory.EmptyStatement(SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken)));

            }
            // set debugger sequence point to first statement or first expression
            if (context.StmtBlk != null && context.StmtBlk._Stmts.Count > 0)
            {
                context.SetSequencePoint(context.StmtBlk._Stmts[0].Start, context.StmtBlk._Stmts[0].Stop);
            }
            else if (context.ExprList != null && context.ExprList._Exprs.Count > 0)
            {
                context.SetSequencePoint(context.ExprList._Exprs[0].Start, context.ExprList._Exprs[0].Stop);
            }
            //context.Put(block);
            context.CsNode = block;
        }

        public override void ExitCodeblock([NotNull] XP.CodeblockContext context)
        {
            ParameterListSyntax paramList = context.LambdaParamList?.Get<ParameterListSyntax>() ?? EmptyParameterList();
            bool updateparams = false;
            if (context.lambda == null &&
                context.LambdaParamList?.ExplicitParams != null)
            {
                //paramList = paramList.WithAdditionalDiagnostics(new SyntaxDiagnosticInfo(ErrorCode.ERR_CodeBlockWithTypeParameters));
                updateparams = true;
                
            }
            if (context.lambda != null)
            {
                context.SetSequencePoint(context.Start, context.lambda);
                bool bWarn;
                if (context.Or == null && context.P1 == null && context.P2 == null)
                {
                    // correct lambda
                    bWarn = false;
                }
                else if (context.Or != null)        // no parameters with 2 pipes  || =>
                {
                    bWarn = true;
                }
                else if (context.P1 != null && context.P2 != null) // params, or pipes separated with space | a | => or | | =>
                {
                    bWarn = true;
                }
                else // one of the pipes is missing
                {
                    bWarn = false;
                    if (context.P1 == null)
                    {
                        paramList = paramList.WithAdditionalDiagnostics(new SyntaxDiagnosticInfo(ErrorCode.ERR_SyntaxError, "Opening Pipe ('|') character"));
                    }
                    if (context.P2 == null)
                    {
                        paramList = paramList.WithAdditionalDiagnostics(new SyntaxDiagnosticInfo(ErrorCode.ERR_SyntaxError, "Closing Pipe ('|') character"));
                    }
                }
                if (bWarn)
                {
                    paramList = paramList.WithAdditionalDiagnostics(new SyntaxDiagnosticInfo(ErrorCode.WRN_LamdaExpressionWithPipes));
                }
            }
            else
            {
                context.SetSequencePoint(context.Start, context.Code.Start);
            }
            var body = context.Code.Get<CSharpSyntaxNode>();

            if (updateparams)
            {
                // we will have to remove the parameter types and rename the parameters
                // and add typed variables in the body
                var newstmts = new List<StatementSyntax>();
                var newparams = new List<ParameterSyntax>();
                for (int i = 0; i < paramList.Parameters.Count; i++)
                {
                    var param = paramList.Parameters[i];
                    var id = param.Identifier.Text;
                    var newid = XSharpSpecialNames.ClipperParamPrefix + "_"+id;
                    var newparam = _syntaxFactory.Parameter(
                        attributeLists: param.AttributeLists,
                        modifiers: param.Modifiers,
                        type: null, // codeblock parameters have no type !
                        identifier: SyntaxFactory.MakeIdentifier(newid),
                        param.@default);
                    newparams.Add(newparam);
                    var variables = _pool.AllocateSeparated<VariableDeclaratorSyntax>();
                    var expr = _syntaxFactory.IdentifierName(newparam.Identifier);
                    expr.XGenerated = true;
                    var variable = _syntaxFactory.VariableDeclarator(param.Identifier, null,
                        _syntaxFactory.EqualsValueClause(SyntaxFactory.MakeToken(SyntaxKind.EqualsToken), expr));
                    variable.XGenerated = true;
                    variables.Add(variable);
                    var decl = _syntaxFactory.LocalDeclarationStatement(
                        EmptyList<SyntaxToken>(),
                        _syntaxFactory.VariableDeclaration(param.Type, variables),
                        SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken));
                    decl.XGenerated = true;
                    newstmts.Add(decl);
                    _pool.Free(variables);
                }
                var @params = new List<ParameterSyntax>();
                foreach (var param in newparams)
                {
                    @params.Add(param);
                }
                paramList = MakeParameterList(@params);

                if (body is BlockSyntax block)
                {
                    foreach (var stmt in block.Statements)
                    {
                        newstmts.Add(stmt);
                    }
                    body = MakeBlock(newstmts);
                }
                else if (body is ExpressionSyntax expr)
                {
                    newstmts.Add(GenerateReturn(expr, true));
                    body = MakeBlock(newstmts);


                }
            }


            var node = _syntaxFactory.ParenthesizedLambdaExpression(
                asyncKeyword: null,
                parameterList: paramList,
                arrowToken: SyntaxFactory.MakeToken(SyntaxKind.EqualsGreaterThanToken),
                body: body
                );
            context.Put(node);

        }

        public override void ExitCodeblockParamList([NotNull] XP.CodeblockParamListContext context)
        {
            var @params = new List<ParameterSyntax>();
            foreach (var idCtx in context._Ids)
            {
                @params.Add(_syntaxFactory.Parameter(
                    attributeLists: EmptyList<AttributeListSyntax>(),
                    modifiers: EmptyList<SyntaxToken>(),
                    type: null, // codeblock parameters have no type !
                    identifier: idCtx.Get<SyntaxToken>(),
                    @default: null));
            }

            context.Put(MakeParameterList(@params));
        }

        public override void ExitCodeblockExprList([NotNull] XP.CodeblockExprListContext context)
        {
            context.Put(MakeBlock(MakeList<StatementSyntax>(
                from ctx in context._Exprs select _syntaxFactory.ExpressionStatement(ctx.Get<ExpressionSyntax>(), SyntaxFactory.MakeToken(SyntaxKind.SemicolonToken)),
                GenerateReturn(context.ReturnExpr.Get<ExpressionSyntax>())
                )));
        }
        public override void ExitExplicitAnonymousFunctionParameter([NotNull] XP.ExplicitAnonymousFunctionParameterContext context)
        {
            var type = context.Type.Get<TypeSyntax>();
            var par = _syntaxFactory.Parameter(
                attributeLists: EmptyList<AttributeListSyntax>(),
                modifiers: context.Mod.GetList<SyntaxToken>() ,
                type: type,
                identifier: context.Id.Get<SyntaxToken>(),
                @default: null 
                );
            context.Put(par);
        }
    
        public override void ExitExplicitAnonymousFunctionParamList([NotNull] XP.ExplicitAnonymousFunctionParamListContext context)
        {
            var @params = new List<ParameterSyntax>();
            foreach (XP.ExplicitAnonymousFunctionParameterContext param in context._Params)
            {
                @params.Add(param.Get<ParameterSyntax>());
            }
            context.Put(MakeParameterList(@params));
        }


        public override void ExitAnonymousMethodExpression([NotNull] XP.AnonymousMethodExpressionContext context)
        {
            var block = context.Code.CsNode as BlockSyntax;
            if (block == null)
            {
                block = MakeBlock(GenerateExpressionStatement(context.Code.Get<ExpressionSyntax>()));
            }
            var ame = _syntaxFactory.AnonymousMethodExpression(
                context.Async?.SyntaxKeyword(),
                context.Delegate.SyntaxKeyword(),
                context.ParamList.Get<ParameterListSyntax>(),
                block);
            context.Put(ame);


        }

        #endregion

        #region LINQ
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
                context.W.SyntaxKeyword(),
                context.Expr.Get<ExpressionSyntax>()
                ));
        }

        public override void ExitJoinClause([NotNull] XP.JoinClauseContext context)
        {
            context.Put(_syntaxFactory.JoinClause(
                context.J.SyntaxKeyword(),
                context.Type?.Get<TypeSyntax>(),
                context.Id.Get<SyntaxToken>(),
                context.I.SyntaxKeyword(),
                context.Expr.Get<ExpressionSyntax>(),
                context.O.SyntaxKeyword(),
                context.OnExpr.Get<ExpressionSyntax>(),
                context.E.SyntaxKeyword(),
                context.EqExpr.Get<ExpressionSyntax>(),
                context.Into?.Get<JoinIntoClauseSyntax>()
                ));
        }

        public override void ExitJoinIntoClause([NotNull] XP.JoinIntoClauseContext context)
        {
            context.Put(_syntaxFactory.JoinIntoClause(
                context.I.SyntaxKeyword(),
                context.Id.Get<SyntaxToken>()));
        }

        public override void ExitOrderbyClause([NotNull] XP.OrderbyClauseContext context)
        {
            context.Put(_syntaxFactory.OrderByClause(
                context.O.SyntaxKeyword(),
                MakeSeparatedList<OrderingSyntax>(context._Orders)
                ));
        }

        public override void ExitOrdering([NotNull] XP.OrderingContext context)
        {
            SyntaxToken direction = context.Direction != null ? context.Direction.SyntaxKeyword() : SyntaxFactory.MakeToken(SyntaxKind.AscendingKeyword); 
            SyntaxKind kind;
            if (context.Direction != null && context.Direction.Type == XP.DESCENDING)
            {
                kind = SyntaxKind.DescendingOrdering;
            }
            else
            {
                kind = SyntaxKind.AscendingOrdering;
            }
            context.Put(_syntaxFactory.Ordering(kind, context.Expr.Get<ExpressionSyntax>(), direction));

        }

        public override void ExitSelectClause([NotNull] XP.SelectClauseContext context)
        {
            context.Put(_syntaxFactory.SelectClause(
                context.S.SyntaxKeyword(),
                context.Expr.Get<ExpressionSyntax>()
                ));
        }

        public override void ExitGroupClause([NotNull] XP.GroupClauseContext context)
        {
            context.Put(_syntaxFactory.GroupClause(
                context.G.SyntaxKeyword(),
                context.Expr.Get<ExpressionSyntax>(),
                context.B.SyntaxKeyword(),
                context.ByExpr.Get<ExpressionSyntax>()
                ));
        }

        public override void ExitQueryContinuation([NotNull] XP.QueryContinuationContext context)
        {
            context.Put(_syntaxFactory.QueryContinuation(
                context.I.SyntaxKeyword(),
                context.Id.Get<SyntaxToken>(),
                context.Body.Get<QueryBodySyntax>()
                ));
        }

        #endregion
        #region Type Names
        public override void ExitNativeType([NotNull] XP.NativeTypeContext context)
        {
            switch (context.Token.Type)
            {
                case XP.PTR:
                    context.Put(_syntaxFactory.PointerType(VoidType(), SyntaxFactory.MakeToken(SyntaxKind.AsteriskToken)));
                    break;
                case XP.DYNAMIC:
                    context.Put(_syntaxFactory.IdentifierName(context.Token.SyntaxIdentifier()));
                    break;
                case XP.DATETIME:
                    context.Put(_syntaxFactory.IdentifierName(context.Token.SyntaxIdentifier()));
                    break;
                default:
                    context.Put(_syntaxFactory.PredefinedType(context.Token.SyntaxNativeType()));
                    break;
            }
        }

        public override void ExitUsualTypeNameExpression([NotNull] XP.UsualTypeNameExpressionContext context)
        {
            context.Put(context.Name.Get<LiteralExpressionSyntax>());
        }

        public override void ExitTypeExpression([NotNull] XP.TypeExpressionContext context)
        {
            context.Put(context.Type.Get<TypeSyntax>());
        }

        public override void ExitUsualTypeName([NotNull] XP.UsualTypeNameContext context)
        {
            /*
             * Please note that these numbers must match the BT_  types in basetype.h from VO:
             * Vulcan has added 2 numbers for 64 bit types INT64 (22) and UINT64 (23)
             * We have added 4 numbers for CHAR (24), DYNAMIC (25), DATETIME(26) and DECIMAL (27)
             * The Vulcan Runtime has an internal UsualType Enum that has matching values.
             *   #define BT_VOID         0       // == NIL
             *   #define BT_LONG         1       // signed long
             *   #define BT_DATE         2       //
             *   #define BT_FLOAT        3       // internal real format
             *   #define BT_FIXED        4       // internal fixed format
             *   #define BT_ARRAY        5       // array (ptr)
             *   #define BT_OP           6       // object pointer
             *   #define BT_STR          7       // string
             *   #define BT_LOGIC        8
             *   #define BT_CODE         9       // code block
             *   #define BT_SYMBOL       10      // atom nr in symbol atom table
             *
             *   #define BT_BYTE         11      // byte
             *   #define BT_INT          12      // signed int
             *   #define BT_WORD         13      // word
             *   #define BT_DWORD        14      // dword
             *   #define BT_REAL4        15      // C float
             *   #define BT_REAL8        16      // C double
             *   #define BT_PSZ          17      // PSZ
             *   #define BT_PTR          18      // raw ptr
             *   #define BT_POLY         19      // polymorphic
             * */
            int type;
            if (context.XType != null)
                type = context.XType.Token.Type;
            else
                type = context.NativeType.Token.Type;
            switch (type)
            {
                case XSharpLexer.ARRAY:
                    context.Put(GenerateLiteral(5));
                    break;
                case XSharpLexer.BINARY:                  // New in XSharp
                    context.Put(GenerateLiteral(29));
                    break;
                case XSharpLexer.BYTE:
                    context.Put(GenerateLiteral(11));
                    break;
                case XSharpLexer.CHAR:
                    context.Put(GenerateLiteral(24));         // New in XSharp
                    break;
                case XSharpLexer.CODEBLOCK:
                    context.Put(GenerateLiteral(9));
                    break;      
                case XSharpLexer.CURRENCY:                  // New in XSharp
                    context.Put(GenerateLiteral(28));
                    break;
                case XSharpLexer.DATE:
                    context.Put(GenerateLiteral(2));
                    break;
                case XSharpLexer.DATETIME:                  // New in XSharp
                    context.Put(GenerateLiteral(26));
                    break;
                case XSharpLexer.DECIMAL:                   // New in XSharp
                    context.Put(GenerateLiteral(27));
                    break;
                case XSharpLexer.DYNAMIC:                   // New in XSharp
                    context.Put(GenerateLiteral(25));
                    break;
                case XSharpLexer.DWORD:
                    context.Put(GenerateLiteral(14));
                    break;
                case XSharpLexer.FLOAT:
                    context.Put(GenerateLiteral(3));
                    break;
                case XSharpLexer.INT:
                case XSharpLexer.LONGINT:
                    context.Put(GenerateLiteral(1));
                    break;
                case XSharpLexer.INT64:
                    context.Put(GenerateLiteral(22));     // New in Vulcan
                    break;
                case XSharpLexer.LOGIC:
                    context.Put(GenerateLiteral(8));
                    break;
                case XSharpLexer.OBJECT:
                    context.Put(GenerateLiteral(6));
                    break;
                case XSharpLexer.PSZ:
                    context.Put(GenerateLiteral(17));
                    break;
                case XSharpLexer.PTR:
                    context.Put(GenerateLiteral(18));
                    break;
                case XSharpLexer.REAL4:
                    context.Put(GenerateLiteral(15));
                    break;
                case XSharpLexer.REAL8:
                    context.Put(GenerateLiteral(16));
                    break;
                case XSharpLexer.SHORTINT:
                    context.Put(GenerateLiteral(12));
                    break;
                case XSharpLexer.STRING:
                    context.Put(GenerateLiteral(7));
                    break;
                case XSharpLexer.SYMBOL:
                    context.Put(GenerateLiteral(10));
                    break;
                case XSharpLexer.UINT64:
                    context.Put(GenerateLiteral(23));         // New in Vulcan
                    break;
                case XSharpLexer.USUAL:
                    context.Put(GenerateLiteral(19));
                    break;
                case XSharpLexer.VOID:
                    context.Put(GenerateLiteral(0));
                    break;
                case XSharpLexer.WORD:
                    context.Put(GenerateLiteral(13));
                    break;
                default:
                    context.Put(GenerateLiteral(0).WithAdditionalDiagnostics(
                        new SyntaxDiagnosticInfo(ErrorCode.ERR_UnknownLiteralTypeName, context.GetText())));
                    break;
            }
        }
        #endregion  

 
        #region  object and collection initializers
        bool inArrayCtorCall([NotNull] XSharpParserRuleContext context)
        {
            var p = context.Parent;
            while (p != null & !(p is XP.CtorCallContext))
                p = p.Parent;
            return (p is XP.CtorCallContext cc) ? cc.Type is XP.ArrayDatatypeContext : false;
        }
        public override void ExitObjectinitializer([NotNull] XP.ObjectinitializerContext context)
        {
            var objinit = _syntaxFactory.InitializerExpression(
                SyntaxKind.ObjectInitializerExpression,
                SyntaxFactory.MakeToken(SyntaxKind.OpenBraceToken),
                MakeSeparatedList<ExpressionSyntax>(context._Members),
                SyntaxFactory.MakeToken(SyntaxKind.CloseBraceToken));
            context.Put(objinit);
        }
        public override void ExitCollectioninitializer([NotNull] XP.CollectioninitializerContext context)
        {
            var collinit = _syntaxFactory.InitializerExpression(
                inArrayCtorCall(context) ? SyntaxKind.ArrayInitializerExpression : SyntaxKind.CollectionInitializerExpression,
                SyntaxFactory.MakeToken(SyntaxKind.OpenBraceToken),
                MakeSeparatedList<ExpressionSyntax>(context._Members),
                SyntaxFactory.MakeToken(SyntaxKind.CloseBraceToken));
            context.Put(collinit);
        }
        public override void ExitMemberinitializer([NotNull] XP.MemberinitializerContext context)
        {
            var memberinit = _syntaxFactory.AssignmentExpression(
                SyntaxKind.SimpleAssignmentExpression,
                context.Name.Get<ExpressionSyntax>(),
                SyntaxFactory.MakeToken(SyntaxKind.EqualsToken),
                context.Expr.Get<ExpressionSyntax>());
            context.Put(memberinit);
        }
        public override void ExitObjectOrCollectioninitializer([NotNull] XP.ObjectOrCollectioninitializerContext context)
        {
            if (context.ObjInit != null)
                context.Put(context.ObjInit.Get<ExpressionSyntax>());
            else
                context.Put(context.CollInit.Get<ExpressionSyntax>());
        }
        public override void ExitInitializervalue([NotNull] XP.InitializervalueContext context)
        {
            if (context.Expr != null)
                context.Put(context.Expr.Get<ExpressionSyntax>());
            else
                context.Put(context.Init.Get<ExpressionSyntax>());
        }
        public override void ExitComplexInitExpr([NotNull] XP.ComplexInitExprContext context)
        {
            var collinit = _syntaxFactory.InitializerExpression(
                inArrayCtorCall(context) ? SyntaxKind.ArrayInitializerExpression : SyntaxKind.ComplexElementInitializerExpression,
                SyntaxFactory.MakeToken(SyntaxKind.OpenBraceToken),
                MakeSeparatedList<ExpressionSyntax>(context._Members),
                SyntaxFactory.MakeToken(SyntaxKind.CloseBraceToken));
            context.Put(collinit);
        }
        public override void ExitInitializerMember([NotNull] XP.InitializerMemberContext context)
        {
            if (context.Expr != null)
                context.Put(context.Expr.Get<ExpressionSyntax>());
            else
                context.Put(context.Init.Get<ExpressionSyntax>());
        }
        #endregion
        #endregion

        #region ExpressionParser

        protected ExpressionSyntax ParseSubExpression(string expression, out string extraText, IToken starttoken)
        {
            // do not include the standard defs here. These have already been processed at the file level
            var options = _options.WithNoStdDef(true);
            // add spaces to the offset of the first token in the result matches the offset of the "anchor"
            var spaces = new string(' ', starttoken.StartIndex);
            var lexer = XSharpLexer.Create(spaces+expression, _fileName, options);
            var parseErrors = ParseErrorData.NewBag();
            extraText = null;
            BufferedTokenStream tokenStream;
            try
            {
                tokenStream = lexer.GetTokenStream();
                tokenStream.Fill();
                if (lexer.HasPreprocessorTokens )
                {
                    var pp = new XSharpPreprocessor(lexer, tokenStream, options, _fileName, Encoding.Unicode, Text.SourceHashAlgorithm.Sha256, parseErrors);
                    var ppTokens = pp.PreProcess();
                    tokenStream = new CommonTokenStream(new XSharpListTokenSource(lexer, ppTokens));
                    tokenStream.Fill();
                }
                else
                {
                    tokenStream = new CommonTokenStream(new XSharpListTokenSource(lexer, tokenStream.tokens));
                    tokenStream.Fill();
                }
            }
            catch (Exception e)
            {
                parseErrors.Add(new ParseErrorData(_fileName, ErrorCode.ERR_Internal, e.Message, e.StackTrace));
                tokenStream = new BufferedTokenStream(new XSharpListTokenSource(lexer, new List<IToken>()));
                tokenStream.Fill();
            }
            var parser = new XSharpParser(tokenStream);
            parser.Options = _options;
            XSharpParserRuleContext tree = null;

            try
            {
                tree = parser.expression();

                // check to see if the whole expression was matched. If not then there may be a syntax error
                int t = tokenStream.La(1);
                string textafter = "";
                while (t != XSharpLexer.EOS && t != XSharpLexer.Eof)
                {
                    var token = tokenStream.Lt(1);
                    tokenStream.Consume();
                    textafter += token.Text;
                    t = tokenStream.La(1);
                }
                if (textafter.Length > 0)
                    extraText = textafter;
            }
            catch (Exception e)
            {
                parseErrors.Add(new ParseErrorData(_fileName, ErrorCode.ERR_Internal, e.Message, e.StackTrace));
                tree = new XSharpParserRuleContext();
            }
            var errchecker = new XSharpParseErrorAnalysis(parser, parseErrors, _options);
            var walker = new ParseTreeWalker();
            walker.Walk(errchecker, tree);
            if (parseErrors.Count == 0)
            {
                try
                {
                    var transform = CreateWalker(parser);
                    // add our current entity so there is a context for memvars and fields
                    transform.Entities.Push(this.CurrentEntity);
                    walker.Walk(transform, tree);
                    walker.Walk(new XSharpClearSequences(), tree);

                }
                catch (Exception e)
                {
                    parseErrors.Add(new ParseErrorData(_fileName, ErrorCode.ERR_Internal, e.Message, e.StackTrace));
                }
            }
            return tree.Get<ExpressionSyntax>();
        }
        protected virtual XSharpTreeTransformationCore CreateWalker(XSharpParser parser)
        {
            return new XSharpTreeTransformationCore(parser, _options, _pool, _syntaxFactory, _fileName);
        }

        #endregion

    }
}

