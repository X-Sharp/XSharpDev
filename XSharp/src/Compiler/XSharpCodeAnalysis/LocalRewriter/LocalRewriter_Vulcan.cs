﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Roslyn.Utilities;
using Microsoft.CodeAnalysis.CSharp.Syntax.InternalSyntax;
using Microsoft.CodeAnalysis.CSharp.Emit;
namespace Microsoft.CodeAnalysis.CSharp
{
    internal partial class LocalRewriter
    {
        public static BoundStatement RewriteAppExit(
                 CSharpCompilation compilation,
                 MethodSymbol method,
                 int methodOrdinal,
                 NamedTypeSymbol containingType,
                 BoundStatement statement,
                 TypeCompilationState compilationState,
                 DiagnosticBag diagnostics)

        {
            if (method.Name != XSharpVOTreeTransformation.AppExit)
                return statement;
            var refMan = compilation.GetBoundReferenceManager();
            var vcla = compilation.GetWellKnownType(WellKnownType.Vulcan_Internal_VulcanClassLibraryAttribute);
            var newstatements = new List<BoundStatement>();

            foreach (var r in refMan.ReferencedAssemblies)
            {
                foreach (var attr in r.GetAttributes())
                {
                    if (attr.AttributeClass.ConstructedFrom == vcla)
                    {
                        var attargs = attr.CommonConstructorArguments;
                        if (attargs.Length == 2)
                        {
                            var functionsClassName = attargs[0].Value.ToString();
                            if (!string.IsNullOrEmpty(functionsClassName))
                            {
                                var type = r.GetTypeByMetadataName(functionsClassName) as TypeSymbol;
                                var members = type.GetMembers();
                                foreach (var member in members)
                                {
                                    if (member.IsStatic && member.Kind == SymbolKind.Field  )
                                    {
                                        var field = member as FieldSymbol;
                                        var fldtype = field.Type;
                                        if (field.DeclaredAccessibility == Accessibility.Public
                                            && fldtype.TypeKind == TypeKind.Class
                                            && !field.IsReadOnly
                                            && !field.IsConst
                                            )
                                        {
                                            var lhs = new BoundFieldAccess(statement.Syntax, null, field, null) { WasCompilerGenerated = true };
                                            var rhs = new BoundLiteral(statement.Syntax, ConstantValue.Null, compilation.GetSpecialType(SpecialType.System_Object)) { WasCompilerGenerated = true };
                                            var op = new BoundAssignmentOperator(
                                                statement.Syntax,
                                                lhs,
                                                rhs,field.Type)
                                            { WasCompilerGenerated = true };
                                            var stmt = new BoundExpressionStatement(statement.Syntax, op);
                                            newstatements.Add(stmt);
                                        }
                                    }
                                }

                                
                            }
                        }
                    }
                }
            }
            newstatements.Add(new BoundReturnStatement(statement.Syntax, null));
            var oldbody = statement as BoundBlock;
            var newbody = oldbody.Update(oldbody.Locals, newstatements.ToImmutableArray<BoundStatement>());
            return newbody;
        }
        public static BoundStatement RewriteAppInit(
            CSharpCompilation compilation,
            MethodSymbol method,
            int methodOrdinal,
            NamedTypeSymbol containingType,
            BoundStatement statement,
            TypeCompilationState compilationState,
            DiagnosticBag diagnostics)

        {
            if (method.Name != XSharpVOTreeTransformation.AppInit)
                return statement;
            var newstatements = new List<BoundStatement>();
            var oldbody = statement as BoundBlock;
            var trystmt = oldbody.Statements[0] as BoundTryStatement;
            var tryblock = trystmt.TryBlock;
            foreach (var stmt in tryblock.Statements)
                newstatements.Add(stmt);
            var vcla = compilation.GetWellKnownType(WellKnownType.Vulcan_Internal_VulcanClassLibraryAttribute);
            var refMan = compilation.GetBoundReferenceManager();
            var init1 = new List<Symbol>();
            var init2 = new List<Symbol>();
            var init3 = new List<Symbol>();
            foreach (var r in refMan.ReferencedAssemblies)
            {
                foreach (var attr in r.GetAttributes())
                {
                    if (attr.AttributeClass.ConstructedFrom == vcla)
                    {
                        var attargs = attr.CommonConstructorArguments;
                        if (attargs.Length == 2)
                        {
                            var functionsClassName = attargs[0].Value.ToString();
                            if (!string.IsNullOrEmpty(functionsClassName))
                            {
                                var type = r.GetTypeByMetadataName(functionsClassName);
                                init1.AddRange(type.GetMembers(XSharpVOTreeTransformation.InitProc1));
                                init2.AddRange(type.GetMembers(XSharpVOTreeTransformation.InitProc2));
                                init3.AddRange(type.GetMembers(XSharpVOTreeTransformation.InitProc3));
                            }
                        }
                    }
                }
            }
            foreach (var tree in compilation.SyntaxTrees)
            {
                
                var root = tree.GetRoot();
                var cu = root.Green as CompilationUnitSyntax;
                if (cu?.InitProcedures !=null)
                {
                    foreach (var element in cu?.InitProcedures)
                    {
                        var methods = FindMethods(compilation, element.Item2);
                        foreach (MethodSymbol symMethod in methods)
                        {
                            if (symMethod.IsFromCompilation(compilation) &&
                                symMethod.IsStatic &&
                                symMethod.ParameterCount == 0 &&
                                symMethod.ReturnsVoid &&
                                symMethod.ContainingNamespaceOrType() == method.ContainingNamespaceOrType())
                            {
                                switch (element.Item1)
                                {
                                    case 1:
                                        init1.Add(symMethod);
                                        break;
                                    case 2:
                                        init2.Add(symMethod);
                                        break;
                                    case 3:
                                        init3.Add(symMethod);
                                        break;
                                }
                                break;
                            }
                        }

                    }
                }
            }

            init1.AddRange(init2);
            init1.AddRange(init3);
            var args = ImmutableArray<BoundExpression>.Empty;
            var rettype = compilation.GetSpecialType(SpecialType.System_Void);
            foreach (MethodSymbol sym in init1)
            {
                var call = new BoundCall(statement.Syntax, null, sym, 
                    arguments: args,
                    argumentNamesOpt: default(ImmutableArray<string>),
                    argumentRefKindsOpt: default(ImmutableArray<RefKind>),
                    isDelegateCall: false,
                    expanded: false,
                    invokedAsExtensionMethod: false,
                    argsToParamsOpt: default(ImmutableArray<int>),
                    resultKind: LookupResultKind.Viable,
                    type: rettype,
                    hasErrors: false);

                call.WasCompilerGenerated = true;
                var stmt = new BoundExpressionStatement(statement.Syntax, call);
                stmt.WasCompilerGenerated = true;
                newstatements.Add(stmt);
            }
            tryblock = tryblock.Update(tryblock.Locals, newstatements.ToImmutableArray<BoundStatement>());
            trystmt = trystmt.Update(tryblock, trystmt.CatchBlocks, trystmt.FinallyBlockOpt, trystmt.PreferFaultHandler);
            newstatements.Clear();
            newstatements.Add(trystmt);
            int i = 0;
            foreach (var stmt in oldbody.Statements)
            {
                if (i > 0)
                    newstatements.Add(stmt);
                ++i;
            }
            oldbody = oldbody.Update(oldbody.Locals, newstatements.ToImmutableArray<BoundStatement>());
            return oldbody;

        }

        static IEnumerable<ISymbol> FindMethods(CSharpCompilation compilation, string name)
        {
            Func<string, bool> predicate = n => StringComparer.Ordinal.Equals(name, n);
            SymbolFilter filter = SymbolFilter.Member;
            return compilation.GetSymbolsWithName(predicate, filter);

        }
    }
}