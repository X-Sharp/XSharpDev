﻿//
// Copyright (c) XSharp B.V.  All Rights Reserved.
// Licensed under the Apache License, Version 2.0.
// See License.txt in the project root for license information.
// 
#nullable disable

using InternalSyntax = Microsoft.CodeAnalysis.CSharp.Syntax.InternalSyntax;

using Microsoft.CodeAnalysis.CSharp;
using System.Linq;
using System.Collections.Generic; 
using System;
using Antlr4.Runtime.Tree;
using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using Microsoft.CodeAnalysis;
using System.Diagnostics;
using Microsoft.CodeAnalysis.CSharp.Syntax.InternalSyntax;
#if !VSPARSER
using MCT = Microsoft.CodeAnalysis.Text;
using CoreInternalSyntax = Microsoft.CodeAnalysis.Syntax.InternalSyntax;

#endif 
namespace LanguageService.CodeAnalysis.XSharp.SyntaxParser
{
    public partial class XSharpParser
    {

        public CSharpParseOptions Options { get; set; }
        public bool AllowNamedArgs => Options.AllowNamedArguments;
        public bool IsXPP => Options.Dialect == XSharpDialect.XPP;
        public bool IsFox => Options.Dialect == XSharpDialect.FoxPro;
        void unexpectedToken(string token)
        {
            if (Interpreter.PredictionMode == Antlr4.Runtime.Atn.PredictionMode.Sll)
                throw new ParseCanceledException("Unexpected '" + token + "'token");

            NotifyErrorListeners("Unexpected '" + token + "' token");
        }
        void eosExpected(IToken token)
        {
            if (Interpreter.PredictionMode == Antlr4.Runtime.Atn.PredictionMode.Sll)
                unexpectedToken(token?.Text);
            string msg = "Expecting end of statement, found '" + token?.Text + "'";
            NotifyErrorListeners(token, msg, null);

        }

        bool IsTypeCastAllowed()
        {
            // after we added support for the WITH statement the following code was incorrectly recognized as typecast
            // ? (n):ToString()
            // we don't worry here about the correct order of () and []. The parser rule takes care of that.
            if (InputStream.La(1) != LPAREN)
                return false;
            // Quick check for (DWORD) or similar with type keyword
            if (InputStream.La(3) == RPAREN && XSharpLexer.IsType(InputStream.La(2)))
            {
                return true;
            }
            int nestedlevel = 1;
            int la = 2;
            while (true)
            {
                var c = InputStream.La(la);
                switch (c)
                {
                    case LPAREN:
                        nestedlevel += 1;
                        break;
                    case RPAREN:
                        nestedlevel -= 1;
                        break;
                    case EOS:
                        // EOS so no valid typecast
                        return false;
                    default:
                        break;

                }
                if (nestedlevel == 0)
                    break;
                la += 1;
            }
            if (InputStream.La(la) == RPAREN)
            {
                var c = InputStream.La(la + 1);
                return CanFollowCast(c);
            }
            return true;
        }

        private bool CanFollowCast(int c)
        {
            // This code is derived from the Roslyn code for CanFollowCast() In LanguageParser.Cs
            switch (c)
            {
                // the case order is the same as that method
                case ASTYPE:                          // SyntaxKind.AsKeyword:
                case IS:                              // SyntaxKind.IsKeyword:
                case EOS:                             // SyntaxKind.SemicolonToken:
                case RPAREN:                          // SyntaxKind.CloseParenToken:
                case RBRKT:                           // SyntaxKind.CloseBracketToken:
                //case LCURLY:                          // SyntaxKind.OpenBraceToken:    Disabled because this should be allowed (TYPE) { => dosomething() }
                case RCURLY:                          // SyntaxKind.CloseBraceToken:
                case COMMA:                           // SyntaxKind.CommaToken:
                case ASSIGN:                          // SyntaxKind.EqualsToken:
                case ASSIGN_ADD:                      // SyntaxKind.PlusEqualsToken:
                case ASSIGN_SUB:                      // SyntaxKind.MinusEqualsToken:
                case ASSIGN_MUL:                      // SyntaxKind.AsteriskEqualsToken:
                case ASSIGN_DIV:                      // SyntaxKind.SlashEqualsToken:
                case ASSIGN_MOD:                      // SyntaxKind.PercentEqualsToken:
                case ASSIGN_BITAND:                   // SyntaxKind.AmpersandEqualsToken:
                case ASSIGN_XOR:                      // SyntaxKind.CaretEqualsToken:
                case ASSIGN_BITOR:                    // SyntaxKind.BarEqualsToken:
                case ASSIGN_LSHIFT:                   // SyntaxKind.LessThanLessThanEqualsToken:
                case ASSIGN_RSHIFT:                   // SyntaxKind.GreaterThanGreaterThanEqualsToken:
                case QMARK:                           // SyntaxKind.QuestionToken:
                case COLON:                           // SyntaxKind.ColonToken:
                case OR:                              // SyntaxKind.BarBarToken:
                case AND:                             // SyntaxKind.AmpersandAmpersandToken:
                case PIPE:                            // SyntaxKind.BarToken
                case TILDE:                           // SyntaxKind.CaretToken:
                case AMP:                             // SyntaxKind.AmpersandToken:
                case EQ:                              // Unique in VO
                case EEQ:                             // SyntaxKind.EqualsEqualsToken:
                case NEQ:                             // SyntaxKind.ExclamationEqualsToken:
                case LT:                              // SyntaxKind.LessThanToken:
                case LTE:                             // SyntaxKind.LessThanEqualsToken:
                case GT:                              // SyntaxKind.GreaterThanToken:
                case GTE:                             // SyntaxKind.GreaterThanEqualsToken:
                case LSHIFT:                          // SyntaxKind.LessThanLessThanToken:
                case RSHIFT:                          // SyntaxKind.GreaterThanGreaterThanToken:
                case PLUS:                            // SyntaxKind.PlusToken:
                case MINUS:                           // SyntaxKind.MinusToken:
                case MULT:                            // SyntaxKind.AsteriskToken:
                case DIV:                             // SyntaxKind.SlashToken:
                case MOD:                             // SyntaxKind.PercentToken:
                case DEC:                             // SyntaxKind.PlusPlusToken:
                case INC:                             // SyntaxKind.MinusMinusToken:
                case LBRKT:                           // SyntaxKind.OpenBracketToken:
                case DOT:                             // SyntaxKind.DotToken:
                                                      // SyntaxKind.MinusGreaterThanToken     used for pointer type in C#
                case DEFAULT:                         // SyntaxKind.QuestionQuestionToken:
                case Eof:                             // SyntaxKind.EndOfFileToken:     
                    return false;
            }
            return true;
        }

        bool validExpressionStmt()
        {
            var la = InputStream.La(2);
            if (la != LPAREN)
                return true;
            la = InputStream.La(1);
            return la != CONSTRUCTOR && la != DESTRUCTOR;
        }
        public partial class ParenExpressionContext
        {
            public ExpressionContext Expr => _Exprs[_Exprs.Count - 1];
        }
        public partial class PragmaContext
        {
            public PragmaBase Pragma;
        }

#if !VSPARSER
        #region Interfaces
        public interface IPartialPropertyContext : IEntityContext
        {
            List<IMethodContext> PartialProperties { get; set; }
        }

        public interface IGlobalEntityContext : IEntityContext
        {
            FuncprocModifiersContext FuncProcModifiers { get; }
        }
        public interface ILoopStmtContext
        {
            StatementBlockContext Statements { get; }
        }

        public interface IEntityWithBodyContext : IEntityContext
        {
            StatementBlockContext Statements { get; }
        }

        internal interface IBodyWithLocalFunctions
        {
            IList<object> LocalFunctions { get; set; }
        }

        public interface ISourceContext
        {
            IList<PragmaOption> PragmaOptions { get; set; }
            IList<EntityContext> Entities { get; }
        }

        public interface IEntityContext : IRuleNode, IXParseTree
        {
            EntityData Data { get; }
            ParameterListContext Params { get; }
            DatatypeContext ReturnType { get; }
            string Name { get; }
            string ShortName { get; }
        }
        internal interface IXPPEntityContext : IEntityWithBodyContext, IBodyWithLocalFunctions
        {
            XppmemberModifiersContext Mods { get; }
            AttributesContext Atts { get; }
            InternalSyntax.XppDeclaredMethodInfo Info { get; }
            ParameterListContext Parameters { get; }
            new StatementBlockContext Statements { get; set; }
            ExpressionContext ExprBody { get; }
        }
        #endregion
        #region Flags
        [FlagsAttribute]
        enum EntityFlags : int
        {
            None = 0,
            ClipperCallingConvention = 1 << 0, // Member property
            MissingReturnType = 1 << 1, // Member property
            UsesPSZ = 1 << 2,           // Member property
            MustBeUnsafe = 1 << 3,      // Member property
            HasTypedParameter = 1 << 4, // Member property
            HasLParametersStmt = 1 << 5, // Member property
            HasParametersStmt = 1 << 6, // Member property
            MustBeVoid = 1 << 7,        // Member property
            IsInitAxit = 1 << 8,        // Member property
            HasInstanceCtor = 1 << 9,   // Class property
            Partial = 1 << 10,          // Class property
            HasStatic = 1 << 10,        // XPP Class property
            PartialProps = 1 << 11,     // Class property
            HasDimVar = 1 << 12,        // Member property
            HasSync = 1 << 13,          // Member property
            HasAddressOf = 1 << 14,     // Member property
            IsInitProcedure = 1 << 15,  // Member property
            HasMemVars = 1 << 16,       // Member property
            HasYield = 1 << 17,         // Member property
            HasFormalParameters = 1 << 18,  // Member property
            HasInit = 1 << 19,          // class property
            IsEntryPoint = 1 << 20,     // member property
            HasUndeclared = 1 << 21,    // member property
            HasMemVarLevel = 1 << 22,   // member property
            UsesPCount = 1 << 23,       // member property
            ParameterAssign = 1 << 24, // member property
        }
        #endregion

        public class EntityData
        {
            EntityFlags setFlag(EntityFlags oldFlag, EntityFlags newFlag, bool set)
            {
                if (set)
                    oldFlag |= newFlag;
                else
                    oldFlag &= ~newFlag;
                return oldFlag;
            }

            EntityFlags flags;

            public bool HasClipperCallingConvention
            {
                get { return flags.HasFlag(EntityFlags.ClipperCallingConvention); }
                set { flags = setFlag(flags, EntityFlags.ClipperCallingConvention, value); }
            }

            public bool HasParametersStmt
            {
                get { return flags.HasFlag(EntityFlags.HasParametersStmt); }
                set { flags = setFlag(flags, EntityFlags.HasParametersStmt, value); }
            }
            public bool HasLParametersStmt
            {
                get { return flags.HasFlag(EntityFlags.HasLParametersStmt); }
                set { flags = setFlag(flags, EntityFlags.HasLParametersStmt, value); }
            }
            public bool HasFormalParameters
            {
                get { return flags.HasFlag(EntityFlags.HasFormalParameters); }
                set { flags = setFlag(flags, EntityFlags.HasFormalParameters, value); }
            }
            public bool HasMissingReturnType
            {
                get { return flags.HasFlag(EntityFlags.MissingReturnType); }
                set { flags = setFlag(flags, EntityFlags.MissingReturnType, value); }
            }
            public bool HasTypedParameter
            {
                get { return flags.HasFlag(EntityFlags.HasTypedParameter); }
                set { flags = setFlag(flags, EntityFlags.HasTypedParameter, value); }
            }
            public bool UsesPSZ
            {
                get { return flags.HasFlag(EntityFlags.UsesPSZ); }
                set { flags = setFlag(flags, EntityFlags.UsesPSZ, value); }
            }
            public bool MustBeUnsafe
            {
                get { return flags.HasFlag(EntityFlags.MustBeUnsafe); }
                set { flags = setFlag(flags, EntityFlags.MustBeUnsafe, value); }
            }

            public bool MustBeVoid            // Assign, SET, Event Accessor
            {
                get { return flags.HasFlag(EntityFlags.MustBeVoid); }
                set { flags = setFlag(flags, EntityFlags.MustBeVoid, value); }
            }
            public bool IsInitAxit            // init or axit with /vo1
            {
                get { return flags.HasFlag(EntityFlags.IsInitAxit); }
                set { flags = setFlag(flags, EntityFlags.IsInitAxit, value); }
            }

            public bool HasInstanceCtor
            {
                get { return flags.HasFlag(EntityFlags.HasInstanceCtor); }
                set { flags = setFlag(flags, EntityFlags.HasInstanceCtor, value); }
            }
            public bool Partial
            {
                get { return flags.HasFlag(EntityFlags.Partial); }
                set { flags = setFlag(flags, EntityFlags.Partial, value); }
            }
            public bool PartialProps
            {
                get { return flags.HasFlag(EntityFlags.PartialProps); }
                set { flags = setFlag(flags, EntityFlags.PartialProps, value); }
            }
            public bool HasDimVar
            {
                get { return flags.HasFlag(EntityFlags.HasDimVar); }
                set { flags = setFlag(flags, EntityFlags.HasDimVar, value); }
            }
            public bool HasSync
            {
                get { return flags.HasFlag(EntityFlags.HasSync); }
                set { flags = setFlag(flags, EntityFlags.HasSync, value); }
            }
            public bool HasAddressOf
            {
                get { return flags.HasFlag(EntityFlags.HasAddressOf); }
                set { flags = setFlag(flags, EntityFlags.HasAddressOf, value); }
            }
            public bool HasStatic
            {
                get { return flags.HasFlag(EntityFlags.HasStatic); }
                set { flags = setFlag(flags, EntityFlags.HasStatic, value); }
            }
            public bool HasMemVars
            {
                get { return flags.HasFlag(EntityFlags.HasMemVars); }
                set { flags = setFlag(flags, EntityFlags.HasMemVars, value); }
            }

            public bool HasUndeclared
            {
                get { return flags.HasFlag(EntityFlags.HasUndeclared); }
                set { flags = setFlag(flags, EntityFlags.HasUndeclared, value); }
            }

            public bool HasMemVarLevel
            {
                get { return flags.HasFlag(EntityFlags.HasMemVarLevel); }
                set { flags = setFlag(flags, EntityFlags.HasMemVarLevel, value); }
            }
            public bool UsesPCount
            {
                get { return flags.HasFlag(EntityFlags.UsesPCount); }
                set { flags = setFlag(flags, EntityFlags.UsesPCount, value); }
            }
            public bool HasYield
            {
                get { return flags.HasFlag(EntityFlags.HasYield); }
                set { flags = setFlag(flags, EntityFlags.HasYield, value); }
            }

            public bool IsInitProcedure
            {
                get { return flags.HasFlag(EntityFlags.IsInitProcedure); }
                set { flags = setFlag(flags, EntityFlags.IsInitProcedure, value); }
            }
            public bool HasInit
            {
                get { return flags.HasFlag(EntityFlags.HasInit); }
                set { flags = setFlag(flags, EntityFlags.HasInit, value); }
            }
            public bool IsEntryPoint
            {
                get { return flags.HasFlag(EntityFlags.IsEntryPoint); }
                set { flags = setFlag(flags, EntityFlags.IsEntryPoint, value); }
            }

            public bool ParameterAssign
            {
                get { return flags.HasFlag(EntityFlags.ParameterAssign); }
                set { flags = setFlag(flags, EntityFlags.ParameterAssign, value); }
            }

            internal Dictionary<string, MemVarFieldInfo> Fields = null;
            internal MemVarFieldInfo AddField(string Name, string Alias, XSharpParserRuleContext context)
            {
                if (Fields == null)
                {
                    Fields = new Dictionary<string, MemVarFieldInfo>(XSharpString.Comparer);
                }
                var info = new MemVarFieldInfo(Name, Alias);
                info.Context = context;
                Fields.Add(info.Name, info);
                if (info.Name != info.FullName)
                    Fields.Add(info.FullName, info);
                return info;
            }
            internal MemVarFieldInfo GetField(string Name)
            {
                if (Fields != null && Fields.ContainsKey(Name))
                {
                    return Fields[Name];
                }
                return null;
            }
        }

        public partial class ScriptContext : IEntityContext
        {
            readonly EntityData data = new();
            public EntityData Data => data;
            public ParameterListContext Params => null;
            public DatatypeContext ReturnType => null;
            public String Name => null;
            public String ShortName => null;
        }

        public partial class MethodCallContext
        {
            public bool HasRefArguments;
        }
        public partial class CtorCallContext
        {
            public bool HasRefArguments;
        }

        public partial class DoStmtContext
        {
            public bool HasRefArguments;
        }

        public partial class SourceContext : ISourceContext
        {
            public IList<PragmaOption> PragmaOptions { get; set; }
            public IList<EntityContext> Entities => _Entities;

        }

        public partial class FoxsourceContext : ISourceContext
        {
            public IList<PragmaOption> PragmaOptions { get; set; }
            public IList<EntityContext> Entities => _Entities;
        }
        public partial class RepeatStmtContext : ILoopStmtContext
        {
            public StatementBlockContext Statements { get { return StmtBlk; } }
        }
        public partial class WhileStmtContext : ILoopStmtContext
        {
            public StatementBlockContext Statements { get { return StmtBlk; } }
        }
        public partial class ForeachStmtContext : ILoopStmtContext
        {
            public StatementBlockContext Statements { get { return StmtBlk; } }
        }
        public partial class ForStmtContext : ILoopStmtContext
        {
            public StatementBlockContext Statements { get { return StmtBlk; } }
        }

        public partial class LocalfuncprocContext : IEntityWithBodyContext, IBodyWithLocalFunctions
        {

            public IdentifierContext Id => this.Sig.Id;
            public TypeparametersContext TypeParameters => Sig.TypeParameters;
            public IList<TypeparameterconstraintsclauseContext> _ConstraintsClauses => Sig._ConstraintsClauses;
            public ParameterListContext ParamList => Sig.ParamList;
            public DatatypeContext Type => Sig.Type;
            public CallingconventionContext CallingConvention => Sig.CallingConvention;
            readonly EntityData data = new();
            public EntityData Data => data;
            public ParameterListContext Params => this.ParamList;
            public DatatypeContext ReturnType => this.Type;
            public String Name => ParentName + ShortName;
            public String ShortName => this.Id.GetText();
            public LocalfuncprocModifiersContext LocalFuncProcModifiers => Modifiers;
            public StatementBlockContext Statements => StmtBlk;
            public IList<object> LocalFunctions { get; set; } = null;
        }

        public partial class FuncprocContext : IEntityWithBodyContext, IGlobalEntityContext, IBodyWithLocalFunctions
        {

            public IdentifierContext Id => this.Sig.Id;
            public TypeparametersContext TypeParameters => Sig.TypeParameters;
            public IList<TypeparameterconstraintsclauseContext> _ConstraintsClauses => Sig._ConstraintsClauses;
            public ParameterListContext ParamList => Sig.ParamList;
            public DatatypeContext Type => Sig.Type;
            public CallingconventionContext CallingConvention => Sig.CallingConvention;
            readonly EntityData data = new();
            public EntityData Data => data;
            public ParameterListContext Params => this.ParamList;
            public DatatypeContext ReturnType => this.Type;
            public String Name => ParentName + ShortName;
            public String ShortName => this.Id.GetText();
            public FuncprocModifiersContext FuncProcModifiers => Modifiers;
            public StatementBlockContext Statements => StmtBlk;
            public int RealType { get; set; } // fox FoxPro Function and Procedure will be come method, access or assign
            public IList<object> LocalFunctions { get; set; } = null;
        }

        public interface IMethodContext : IEntityWithBodyContext
        {
            IdentifierContext Id { get; }
            TypeparametersContext TypeParameters { get; }
            IList<TypeparameterconstraintsclauseContext> _ConstraintsClauses { get; }
            ParameterListContext ParamList { get; }
            DatatypeContext Type { get; }
            MemberModifiersContext Mods { get; }
            ExpressionContext ExpressionBody { get; }
            bool IsInInterface { get; }
            bool IsInStructure { get; }
            int RealType { get; }
        }
        public partial class MethodContext : IMethodContext, IBodyWithLocalFunctions
        {
            public IdentifierContext Id => Sig.Id;
            public TypeparametersContext TypeParameters => Sig.TypeParameters;
            public IList<TypeparameterconstraintsclauseContext> _ConstraintsClauses => Sig._ConstraintsClauses;
            public ParameterListContext ParamList => Sig.ParamList;
            public DatatypeContext Type => Sig.Type;
            public CallingconventionContext CallingConvention => Sig.CallingConvention;
            public bool IsInInterface => this.isInInterface();
            public bool IsInStructure => this.isInStructure();
            readonly EntityData data = new();
            public EntityData Data => data;
            public ParameterListContext Params => this.ParamList;
            public DatatypeContext ReturnType => this.Type;
            public MemberModifiersContext Mods => this.Modifiers;
            public String ShortName => this.Id.GetText();
            public ExpressionContext ExpressionBody => Sig.ExpressionBody;
            public String Name
            {
                get
                {
                    string name = this.Id.GetText();
                    if (this.T.Token.Type == XSharpParser.ACCESS)
                        name += ":Access";
                    else if (this.T.Token.Type == XSharpParser.ASSIGN)
                        name += ":Assign";
                    else
                        name += "()";
                    return ParentName + name;
                }
            }
            public StatementBlockContext Statements => StmtBlk;
            public int RealType { get; set; } // fox FoxPro Function and Procedure will be come method, access or assign
            public IList<object> LocalFunctions { get; set; } = null;

        }

        public partial class FoxmethodContext : IMethodContext, IBodyWithLocalFunctions
        {
            public IdentifierContext Id => Sig.Id;
            public TypeparametersContext TypeParameters => Sig.TypeParameters;
            public IList<TypeparameterconstraintsclauseContext> _ConstraintsClauses => Sig._ConstraintsClauses;
            public ParameterListContext ParamList => Sig.ParamList;
            public DatatypeContext Type => Sig.Type;
            public CallingconventionContext CallingConvention => Sig.CallingConvention;
            public MemberModifiersContext Mods => this.Modifiers;
            public bool IsInInterface => false;
            public bool IsInStructure => false;

            readonly EntityData data = new();
            public EntityData Data => data;
            public ParameterListContext Params => this.Sig.ParamList;
            public DatatypeContext ReturnType => this.Sig.Type;
            public String ShortName => this.Sig.Id.GetText();
            public ExpressionContext ExpressionBody => Sig.ExpressionBody;
            public String Name
            {
                get
                {
                    string name = this.Id.GetText();
                    name += "()";
                    return ParentName + name;
                }
            }
            public StatementBlockContext Statements => StmtBlk;
            public int RealType { get; set; } // fox FoxPro Function and Procedure will be come method, access or assign
            public IList<object> LocalFunctions { get; set; } = null;
        }

        public partial class EventAccessorContext : IEntityWithBodyContext, IBodyWithLocalFunctions
        {
            readonly EntityData data = new();
            public EntityData Data => data;
            public ParameterListContext Params => null;
            public DatatypeContext ReturnType => null;
            public String Name => ParentName + Key.Text;
            public String ShortName => ParentName + Key.Text;
            public StatementBlockContext Statements => StmtBlk;
            public bool HasReturnValue => false;
            public IList<object> LocalFunctions { get; set; } = null;
        }

        public partial class PropertyAccessorContext : IEntityWithBodyContext, IBodyWithLocalFunctions
        {
            readonly EntityData data = new();
            public EntityData Data => data;
            public ParameterListContext Params => null;
            public DatatypeContext ReturnType => null;
            public String Name => ParentName + Key.Text;
            public String ShortName => ParentName + Key.Text;
            public StatementBlockContext Statements => StmtBlk;
            public IList<object> LocalFunctions { get; set; } = null;
        }
        public partial class PropertyLineAccessorContext : IEntityContext
        {
            readonly EntityData data = new();
            public EntityData Data => data;
            public ParameterListContext Params => null;
            public DatatypeContext ReturnType => null;
            public String Name => ParentName + Key.Text;
            public String ShortName => ParentName + Key.Text;
        }
        public partial class ConstructorContext : IEntityWithBodyContext, IBodyWithLocalFunctions
        {
            readonly EntityData data = new();
            public EntityData Data => data;
            public ParameterListContext Params => this.ParamList;
            public DatatypeContext ReturnType => null;
            public String Name => ParentName + ShortName;
            public String ShortName => "ctor";
            public StatementBlockContext Statements => StmtBlk;
            public IList<object> LocalFunctions { get; set; } = null;
        }
        public partial class DestructorContext : IEntityWithBodyContext, IBodyWithLocalFunctions
        {
            readonly EntityData data = new();
            public EntityData Data => data;
            public ParameterListContext Params => null;
            public DatatypeContext ReturnType => null;
            public String Name => ParentName + ShortName;
            public String ShortName => "Finalize";
            public StatementBlockContext Statements => StmtBlk;
            public IList<object> LocalFunctions { get; set; } = null;
        }
        public partial class Event_Context : IEntityContext
        {
            readonly EntityData data = new();
            public EntityData Data => data;
            public ParameterListContext Params => null;
            public DatatypeContext ReturnType => this.Type;
            public String Name => ParentName + ShortName;
            public String ShortName => Id.GetText();
            public IList<object> LocalFunctions { get; set; } = null;
        }
        public partial class VodefineContext : IEntityContext, IGlobalEntityContext
        {
            readonly EntityData data = new();
            public EntityData Data => data;
            public ParameterListContext Params => null;
            public DatatypeContext ReturnType => null;
            public String Name => ParentName + ShortName;
            public String ShortName => Id.GetText();
            public FuncprocModifiersContext FuncProcModifiers => Modifiers;
        }
        public partial class PropertyContext : IEntityContext
        {
            readonly EntityData data = new();

            public EntityData Data { get { return data; } }
            public ParameterListContext Params => null;
            public DatatypeContext ReturnType => this.Type;
            public String Name => ParentName + ShortName;
            public String ShortName
            {
                get
                {
                    return Id != null ? Id.GetText() : "Item";
                }
            }
        }
        public partial class Operator_Context : IEntityWithBodyContext, IBodyWithLocalFunctions
        {
            readonly EntityData data = new();
            public EntityData Data => data;
            public ParameterListContext Params => this.ParamList;
            public DatatypeContext ReturnType => this.Type;
            public String Name => ParentName + ShortName;
            public String ShortName
            {
                get
                {
                    string name;
                    if (Operation != null)
                        name = Operation.GetText() + Gt?.Text;
                    else
                        name = Conversion.GetText();
                    return name;
                }
            }
            public StatementBlockContext Statements => StmtBlk;
            public IList<object> LocalFunctions { get; set; } = null;
        }
        public partial class Delegate_Context : IEntityContext
        {
            readonly EntityData data = new();
            public EntityData Data => data;
            public ParameterListContext Params => this.ParamList;
            public DatatypeContext ReturnType => this.Type;
            public String Name => ParentName + ShortName;
            public String ShortName => this.Id.GetText();
        }
        public partial class Interface_Context : IPartialPropertyContext, IEntityContext
        {
            readonly EntityData data = new();
            List<IMethodContext> partialProperties = null;
            public List<IMethodContext> PartialProperties
            {
                get { return partialProperties; }
                set { partialProperties = value; }
            }
            public EntityData Data => data;
            public ParameterListContext Params => null;
            public DatatypeContext ReturnType => null;
            public String Name => ParentName + ShortName;
            public String ShortName => this.Id.GetText();

        }
        public partial class Class_Context : IPartialPropertyContext, IEntityContext
        {
            readonly EntityData data = new();
            List<IMethodContext> partialProperties = null;
            public List<IMethodContext> PartialProperties
            {
                get { return partialProperties; }
                set { partialProperties = value; }
            }
            public EntityData Data => data;
            public ParameterListContext Params => null;
            public DatatypeContext ReturnType => null;
            public String Name => ParentName + ShortName;
            public String ShortName => Id.GetText();
        }
        public partial class Structure_Context : IPartialPropertyContext, IEntityContext
        {
            readonly EntityData data = new();
            List<IMethodContext> partialProperties = null;
            public List<IMethodContext> PartialProperties
            {
                get { return partialProperties; }
                set { partialProperties = value; }
            }
            public EntityData Data => data;
            public ParameterListContext Params => null;
            public DatatypeContext ReturnType => null;
            public String Name => ParentName + ShortName;
            public String ShortName => Id.GetText();
        }
        public partial class VodllContext : IEntityContext, IGlobalEntityContext
        {
            readonly EntityData data = new();
            public EntityData Data => data;
            public ParameterListContext Params => this.ParamList;
            public DatatypeContext ReturnType => this.Type;
            public String Name => this.Id.GetText();
            public String ShortName => this.Id.GetText();
            public FuncprocModifiersContext FuncProcModifiers => Modifiers;
        }

        public partial class VoglobalContext : IEntityContext, IGlobalEntityContext
        {
            readonly EntityData data = new();
            public EntityData Data => data;
            public ParameterListContext Params => null;
            public DatatypeContext ReturnType => this.Vars.DataType;
            public String Name => this.Vars._Var.FirstOrDefault().Id.GetText();
            public String ShortName => this.Vars._Var.FirstOrDefault().Id.GetText();
            public FuncprocModifiersContext FuncProcModifiers => Modifiers;
        }
        public partial class FoxdllContext : IEntityContext, IGlobalEntityContext
        {
            readonly EntityData data = new();
            public EntityData Data => data;
            public ParameterListContext Params => null;
            public DatatypeContext ReturnType => this.Type;
            public String Name => this.Id.GetText();
            public String ShortName => this.Id.GetText();
            public FuncprocModifiersContext FuncProcModifiers => Modifiers;
        }

        public partial class FuncprocModifiersContext
        {
            public bool IsStaticVisible { get; set; }
        }

        public partial class VounionContext : IEntityContext
        {
            readonly EntityData data = new();
            public EntityData Data => data;
            public ParameterListContext Params => null;
            public DatatypeContext ReturnType => null;
            public String Name => this.Id.GetText();
            public String ShortName => this.Id.GetText();

        }
        public partial class VostructContext : IEntityContext
        {
            readonly EntityData data = new();
            public EntityData Data => data;
            public ParameterListContext Params => null;
            public DatatypeContext ReturnType => null;
            public String Name => this.Id.GetText();
            public String ShortName => this.Id.GetText();

        }
        public partial class XppclassContext : IEntityContext
        {
            readonly EntityData data = new();
            public EntityData Data => data;
            public ParameterListContext Params => null;
            public DatatypeContext ReturnType => null;
            public String Name => ParentName + ShortName;
            public String ShortName => Id.GetText();
        }
        public partial class XppclassvarsContext
        {
            public int Visibility { get; set; }
        }

        public partial class XppmethodContext : IXPPEntityContext
        {
            readonly EntityData data = new();
            public EntityData Data => data;
            public ParameterListContext Params => this.ParamList;
            public DatatypeContext ReturnType => this.Type;
            public String ShortName => this.Id.GetText();
            public String Name
            {
                get
                {
                    string name = this.Id.GetText() + "()";
                    return ParentName + name;
                }
            }
            public InternalSyntax.XppDeclaredMethodInfo Info { get; set; }
            public XppmemberModifiersContext Mods => this.Modifiers;
            public AttributesContext Atts => this.Attributes;
            public StatementBlockContext Statements { get { return this.StmtBlk; } set { this.StmtBlk = value; } }
            public ParameterListContext Parameters => this.ParamList;
            public IList<object> LocalFunctions { get; set; } = null;
            public ExpressionContext ExprBody { get { return this.ExpressionBody; } }
        }
        public partial class XppinlineMethodContext : IXPPEntityContext, IBodyWithLocalFunctions
        {
            readonly EntityData data = new();
            public EntityData Data => data;
            public ParameterListContext Params => this.ParamList;
            public DatatypeContext ReturnType => this.Type;
            public String ShortName => this.Id.GetText();
            public String Name
            {
                get
                {
                    string name = this.Id.GetText() + "()";
                    return ParentName + name;
                }
            }
            public InternalSyntax.XppDeclaredMethodInfo Info { get; set; }
            public XppmemberModifiersContext Mods => this.Modifiers;
            public AttributesContext Atts => this.Attributes;
            public StatementBlockContext Statements { get { return this.StmtBlk; } set { this.StmtBlk = value; } }
            public ParameterListContext Parameters => this.ParamList;
            public IList<object> LocalFunctions { get; set; } = null;
            public ExpressionContext ExprBody { get { return this.ExpressionBody; } }
        }

        public partial class WithBlockContext
        {
            public string VarName;
        }
        public partial class AliasedExpressionContext
        {
            public bool XSharpRuntime;
        }
        public partial class XpppropertyContext : IEntityContext
        {
            readonly EntityData data = new();
            public EntityData Data => data;
            public ParameterListContext Params => null;
            public DatatypeContext ReturnType => this.Type;
            public String ShortName => this.Id.GetText();
            public String Name
            {
                get
                {
                    string name = this.Id.GetText();
                    return ParentName + name;
                }
            }
        }
        public partial class FoxclassContext : IPartialPropertyContext, IEntityContext
        {
            readonly EntityData data = new();
            List<IMethodContext> partialProperties = null;

            public List<IMethodContext> PartialProperties
            {
                get { return partialProperties; }
                set { partialProperties = value; }
            }
            public EntityData Data => data;
            public ParameterListContext Params => null;
            public DatatypeContext ReturnType => null;
            public String Name => ParentName + ShortName;
            public String ShortName => Id.GetText();

        }

        public partial class NamedArgumentContext
        {
            internal bool IsMissing => Expr == null && Id == null && Null == null;
        }
        public partial class UnnamedArgumentContext
        {
            internal bool IsMissing => Expr == null;
        }

        [Flags]
        internal enum FoxFlags : byte
        {
            None = 0,
            MemberAccess = 1,
            MPrefix = 2,
        }
        public partial class AccessMemberContext
        {
            internal FoxFlags foxFlags = FoxFlags.None;
            internal bool IsFox => foxFlags != FoxFlags.None;
            internal bool HasMPrefix => foxFlags.HasFlag(FoxFlags.MPrefix);
            internal string AreaName => Expr == null ? "" : Expr.GetText().ToUpper();
            internal string FieldName => Name.GetText().ToUpper();
        }

#endif
    }

    [DebuggerDisplay("{DebuggerDisplay()}")]
    internal class ParseErrorData
    {
        internal readonly IXParseTree Node;
        internal readonly ErrorCode Code;
        internal readonly object[] Args;
        internal ParseErrorData(IErrorNode enode, ErrorCode code, params object[] args) :
            this(token: enode.Symbol, code: code, args: args)
        {
        }

        internal string DebuggerDisplay()
        {
            if (Node is XTerminalNodeImpl xterm)
                return Code.ToString() + " " + xterm.Symbol.Line.ToString() + " " + xterm.Symbol.Text;
            if (Node.SourceSymbol != null)
                return Code.ToString() + " " + Node.SourceSymbol.Line.ToString() + " " + Node.SourceSymbol.Text;
            return Code.ToString();
        }
        //internal ParseErrorData(ErrorCode code) :
        //    this(node: null, code: code, args: Array.Empty<object>())
        //{ }
        internal ParseErrorData(string fileName, ErrorCode code, params object[] args) :
            this(node: null, code: code, args: args)
        {
            var token = new XSharpToken(0);
            var node = new XTerminalNodeImpl(token);
            node.SourceFileName = fileName;
            this.Node = node;
        }
        internal ParseErrorData(IXParseTree node, ErrorCode code) :
            this(node, code, Array.Empty<object>())
        { }
        internal ParseErrorData(IXParseTree node, ErrorCode code, params object[] args)
        {
            this.Node = node;
            this.Code = code;
            this.Args = args;
        }
        internal ParseErrorData(IToken token, ErrorCode code, params object[] args)
        {
            if (token == null)
                token = new XSharpToken(0, "");
            else if (!(token is XSharpToken))
            {
                token = new XSharpToken(token);
            }
            this.Node = new XTerminalNodeImpl(token);
            this.Code = code;
            this.Args = args;
        }
        internal ParseErrorData(ITerminalNode tnode, ErrorCode code, params object[] args)
        {
            this.Node = new XTerminalNodeImpl(tnode.Symbol);
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

    public interface IXParseTree : IParseTree
    {
        object CsNode { get; set; }
        int Position { get; }
        int FullWidth { get; }
        string SourceFileName { get; }
        string MappedFileName { get; }
        int MappedLine { get; }
        IToken SourceSymbol { get; }
#if !VSPARSER
        Microsoft.CodeAnalysis.Location GetLocation();
#endif
    }
    [Serializable]
    public class XTerminalNodeImpl : Antlr4.Runtime.Tree.TerminalNodeImpl,
        IFormattable,
        IXParseTree,
        IErrorNode
    {
        private string fileName = null;
        public XTerminalNodeImpl(IToken symbol) : base(symbol)
        { }
        public object CsNode { get; set; }
        public int Position { get { return Symbol.StartIndex; } }
        public int FullWidth { get { return Symbol.StopIndex - Symbol.StartIndex + 1; } }
        public string ToString(string format, IFormatProvider formatProvider)
        {
            return ToString();
        }
        public string SourceFileName
        {
            get
            {
                if (fileName != null)
                    return fileName;
                var ct = (Symbol as XSharpToken);
                if (ct != null)
                {
                    if (ct.TokenSource != null && !String.IsNullOrEmpty(ct.TokenSource.SourceName))
                        return ct.TokenSource.SourceName;
                    return ct.SourceName;
                }
                return "<unknown>";
            }
            set => fileName = value;
        }
        public string MappedFileName { get { return ((XSharpToken)Symbol).MappedFileName; } }
        public int MappedLine { get { return ((XSharpToken)Symbol).MappedLine; } }
        public IToken SourceSymbol
        {
            get
            {
                return ((XSharpToken)Symbol).SourceSymbol;
            }
        }
        public override string ToString() { return this.GetText(); }
#if !VSPARSER
        public Microsoft.CodeAnalysis.Location GetLocation()
        {
            var token = this.Symbol;
            var ts = new MCT.TextSpan(token.StartIndex, this.FullWidth);
            var lp1 = new MCT.LinePosition(token.Line - 1, token.Column);
            var lp2 = new MCT.LinePosition(token.Line - 1, token.Column + this.FullWidth - 1);
            // prevent error  at EOF
            if (lp2 < lp1)
            {
                lp2 = lp1;
            }
            var ls = new MCT.LinePositionSpan(lp1, lp2);
            return Microsoft.CodeAnalysis.Location.Create(this.SourceFileName, ts, ls);

        }
#endif
    }

#if !VSPARSER
    [DebuggerDisplay("{_fieldType} {FullName}")]
    public class MemVarFieldInfo
    {
        internal enum MemvarType
        {
            Memvar,
            Field,
            ClipperParameter,
            MacroMemvar,
            Local,
        }
        [Flags]
        enum FieldFlags : byte
        {
            IsFileWidePublic,
            IsCreated,
            IsParameter,
            IsWritten
        }
        private readonly MemvarType _fieldType;
        public string Name { get; private set; }
        public string Alias { get; private set; }
        public string FullName
        {
            get
            {
                if (_fieldType == MemvarType.MacroMemvar)
                {
                    var name = Name.Substring(0, Name.IndexOf(":"));
                    return Alias + "->" + name;
                }
                if (Alias != null)
                {
                    return Alias + "->" + Name;
                }
                else
                {
                    return Name;
                }
            }
        }

        private FieldFlags _flags;
        FieldFlags setFlag(FieldFlags oldFlag, FieldFlags newFlag, bool set)
        {
            if (set)
                oldFlag |= newFlag;
            else
                oldFlag &= ~newFlag;
            return oldFlag;
        }
        public bool IsClipperParameter => _fieldType == MemvarType.ClipperParameter;
        public bool IsLocal => _fieldType == MemvarType.Local;
        public bool IsFileWidePublic
        {
            get { return _flags.HasFlag(FieldFlags.IsFileWidePublic); }
            set { _flags = setFlag(_flags, FieldFlags.IsFileWidePublic, value); }
        }
        public bool IsParameter
        {
            get { return _flags.HasFlag(FieldFlags.IsParameter); }
            set { _flags = setFlag(_flags, FieldFlags.IsParameter, value); }
        }
        public bool IsWritten
        {
            get { return _flags.HasFlag(FieldFlags.IsWritten); }
            set { _flags = setFlag(_flags, FieldFlags.IsWritten, value); }
        }
        public bool IsCreated
        {
            get { return _flags.HasFlag(FieldFlags.IsCreated); }
            set { _flags = setFlag(_flags, FieldFlags.IsCreated, value); }
        }
        public XSharpParserRuleContext Context { get; set; }
        internal MemVarFieldInfo(string name, string alias, bool filewidepublic = false)
        {
            if (name.StartsWith("@@"))
                name = name.Substring(2);
            Name = name;
            Alias = alias;
            if (!string.IsNullOrEmpty(alias))
            {
                if (alias.StartsWith("@@"))
                    alias = alias.Substring(2);
                switch (alias.ToUpper())
                {
                 case "&":
                        _fieldType = MemvarType.MacroMemvar;
                        Alias = XSharpSpecialNames.MemVarPrefix;
                        break;
                    case "M":
                    case "MEMV":
                    case "MEMVA":
                    case "MEMVAR":
                        _fieldType = MemvarType.Memvar;
                        Alias = XSharpSpecialNames.MemVarPrefix;
                        break;
                    case "FIELD":
                    case "_FIELD":
                        Alias = XSharpSpecialNames.FieldPrefix;
                        _fieldType = MemvarType.Field;
                        break;
                    default:
                        switch (alias)
                        {
                            case XSharpSpecialNames.ClipperParamPrefix:
                                _fieldType = MemvarType.ClipperParameter;
                                IsParameter = true;
                                break;
                            case XSharpSpecialNames.MemVarPrefix:
                                _fieldType = MemvarType.Memvar;
                                break;
                            case XSharpSpecialNames.LocalPrefix:
                                _fieldType = MemvarType.Local;
                                Alias = null;
                                break;
                            case XSharpSpecialNames.FieldPrefix:
                            default:
                                _fieldType = MemvarType.Field;
                                break;
                        }
                        break;
                }
            }
            else
            {
                Alias = XSharpSpecialNames.FieldPrefix;
                _fieldType = MemvarType.Field;
            }
            IsFileWidePublic = filewidepublic;
        }
    }

    internal static class RuleExtensions
    {

        internal static bool isScript([NotNull] this XSharpParser.IEntityContext entitty) => entitty is XSharpParser.ScriptContext;

        internal static bool IsStatic(this InternalSyntax.ClassDeclarationSyntax classdecl)
        {
            return classdecl.Modifiers.Any((int)SyntaxKind.StaticKeyword);
        }

        internal static bool IsStatic(this InternalSyntax.ConstructorDeclarationSyntax ctordecl)
        {
            return ctordecl.Modifiers.Any((int)SyntaxKind.StaticKeyword);
        }

        internal static void Put<T>([NotNull] this IXParseTree t, T node)
            where T : InternalSyntax.CSharpSyntaxNode
        {
            if (node != null)
            {
                node.XNode = t;
                t.CsNode = node;
            }
        }

        internal static T Get<T>([NotNull] this IXParseTree t)
            where T : InternalSyntax.CSharpSyntaxNode
        {
            if (t == null || t.CsNode == null)
                return null;

            return (T)t.CsNode;
        }

        internal static void PutList<T>([NotNull] this IXParseTree t, CoreInternalSyntax.SyntaxList<T> node)
            where T : InternalSyntax.CSharpSyntaxNode
        {
            //node.XNode = t;
            t.CsNode = node;
        }

        internal static CoreInternalSyntax.SyntaxList<T> GetList<T>([NotNull] this IXParseTree t)
            where T : InternalSyntax.CSharpSyntaxNode
        {
            if (t.CsNode == null)
                return default(CoreInternalSyntax.SyntaxList<T>);

            return (CoreInternalSyntax.SyntaxList<T>)t.CsNode;
        }

        internal static TNode WithAdditionalDiagnostics<TNode>([NotNull] this TNode node, params DiagnosticInfo[] diagnostics)
            where TNode : InternalSyntax.CSharpSyntaxNode
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

        internal static bool IsRealCodeBlock([NotNull] this IXParseTree context)
        {

            if (context is XSharpParser.ArrayElementContext aelc)
                return aelc.Expr.IsRealCodeBlock();
            if (context is XSharpParser.PrimaryExpressionContext pec)
                return pec.Expr.IsRealCodeBlock();
            if (context is XSharpParser.CodeblockExpressionContext cec)
                return cec.CbExpr.IsRealCodeBlock();
            if (context is XSharpParser.AliasedExpressionContext aexc)
            {
                if (aexc.XSharpRuntime)
                {
                    return false;
                }
            }
            if (context is XSharpParser.CodeblockCodeContext)
                return ((IXParseTree)context.Parent).IsRealCodeBlock();
            if (context is XSharpParser.CodeblockContext cbc)
            {
                if (cbc.lambda != null)
                    return false;
                // when no => operator and no explicit parameters
                // then this is a true codeblock
                return cbc.LambdaParamList == null || cbc.LambdaParamList.ImplicitParams != null;
            }
            return false;
        }
        internal static string CodeBlockSource([NotNull] this IXParseTree context)
        {

            if (context is XSharpParser.ArrayElementContext aelc)
                return aelc.Expr.CodeBlockSource();
            if (context is XSharpParser.PrimaryExpressionContext pec)
                return pec.Expr.CodeBlockSource();
            if (context is XSharpParser.CodeblockExpressionContext cec)
                return cec.CbExpr.CodeBlockSource();
            if (context is XSharpParser.AliasedExpressionContext aexc)
            {
                if (aexc.XSharpRuntime)
                {
                    return null;
                }
            }
            if (context is XSharpParser.CodeblockCodeContext)
                return ((IXParseTree)context.Parent).CodeBlockSource();
            if (context is XSharpParser.CodeblockContext cbc)
            {
                if (cbc.lambda != null)
                    return null;
                // when no => operator and no explicit parameters
                // then this is a true codeblock
                return cbc.SourceText;
            }
            return null;
        }
        internal static bool isInInterface([NotNull] this RuleContext context)
        {
            var parent = context.Parent;
            if (parent == null)
                return false;
            if (parent is XSharpParser.ClassmemberContext)
                return parent.Parent is XSharpParser.Interface_Context;
            else
                return parent.isInInterface();
        }

        internal static XSharpParser.CodeblockContext GetParentCodeBlock([NotNull] this RuleContext context)
        {
            var parent = context.Parent;
            if (parent == null)
                return null;
            if (parent is XSharpParser.CodeblockContext cbc)
            {
                if (cbc.lambda != null || cbc.Or != null || cbc.P1 != null)
                    return cbc;
            }
            return parent.GetParentCodeBlock();

        }
        internal static bool IsInLambdaOrCodeBlock([NotNull] this RuleContext context)
        {
            return context.GetParentCodeBlock() != null;
        }
        internal static bool isInClass([NotNull] this RuleContext context)
        {
            var parent = context.Parent;
            if (parent == null)
                return false;
            if (parent is XSharpParser.ClassmemberContext)
            {
                if (parent.Parent is XSharpParser.Class_Context)
                    return true;
                if (parent.Parent is XSharpParser.FoxclassContext)
                    return true;
                return false;
            }
            else if (parent is XSharpParser.XppclassMemberContext)
            {
                return parent.Parent is XSharpParser.XppclassContext;
            }
            return parent.isInClass();
        }
        internal static bool isInStructure([NotNull] this RuleContext context)
        {
            var parent = context.Parent;
            if (parent == null)
                return false;
            if (parent is XSharpParser.ClassmemberContext)
                return parent.Parent is XSharpParser.Structure_Context;
            else
                return parent.isInStructure();
        }
}
#endif
}
