﻿//
// Copyright (c) XSharp B.V.  All Rights Reserved.
// Licensed under the Apache License, Version 2.0.
// See License.txt in the project root for license information.
//
#nullable disable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.CodeAnalysis.Collections;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.CSharp.Symbols
{
    internal abstract partial class SourceOrdinaryMethodSymbolBase 
    {

        protected bool XsGenerateDebugInfo
        {
            get
            {
                if (IsAsync || IsIterator)
                {
                    return false;
                }
                var node = this.GetNonNullSyntaxNode();
                if (node.XGenerated)
                    return false;
                return true;
            }
        }

        private int _overrideState = 0; // 0 = unchecked, 1 = checked and false, 2 = checked and true
        public sealed override bool IsOverride
        {
            get
            {
                return (this.DeclarationModifiers & DeclarationModifiers.Override) != 0 && _overrideState != 1;
            }
        }

        internal MethodSymbol validateMethod(MethodSymbol overriddenMethod, DiagnosticBag diagnostics, Location location)
        {
            if (overriddenMethod is { })
            {
                if (this.HasClipperCallingConvention() != overriddenMethod.HasClipperCallingConvention())
                {
                    if (overriddenMethod.ContainingType.TypesChanged())
                    {
                        diagnostics.Add(ErrorCode.ERR_MethodSignatureChanged, this.Locations[0], this, overriddenMethod);
                    }
                    else
                    {
                        if (this.HasClipperCallingConvention())
                        {
                            diagnostics.Add(ErrorCode.ERR_ClipperInSubClass, location, this.Name);
                        }
                        else
                        {
                            diagnostics.Add(ErrorCode.ERR_ClipperInParentClass, location, this.Name);
                        }
                    }
                    overriddenMethod = null;
                }
            }
            else if (this.HasClipperCallingConvention())
            {
                var baseType = this.ContainingType.BaseTypeNoUseSiteDiagnostics;
                var members = baseType.GetMembersUnordered().Where(member =>
                   member.Kind == SymbolKind.Method && string.Equals(member.Name, this.Name, StringComparison.OrdinalIgnoreCase));
                if (members.Count() > 0 && members.First() is MethodSymbol ms)
                {
                    if (ms.IsVirtual)
                    { 
                        diagnostics.Add(ErrorCode.ERR_ClipperInSubClass, location, this.Name);
                    }
                }
            }
            else if (XSharpString.CaseSensitive && !this.DeclarationModifiers.HasFlag(DeclarationModifiers.New) &&
                (this.DeclarationModifiers.HasFlag(DeclarationModifiers.Virtual) || this.DeclarationModifiers.HasFlag(DeclarationModifiers.Override)))
            {
                // check if we have a base type and if the base type has a method with the same name but different casing
                var baseType = this.ContainingType.BaseTypeNoUseSiteDiagnostics;
                var members = baseType.GetMembersUnordered().Where(member =>
                    member.Kind == SymbolKind.Method && member.IsVirtual && String.Equals(member.Name, this.Name, StringComparison.OrdinalIgnoreCase));
                if (members.Count() > 0)
                {
                    foreach (var member in members)
                    {
                        var metSym = member as MethodSymbol;
                        bool equalSignature = metSym.ParameterCount == this.ParameterCount && 
                            TypeSymbol.Equals(this.ReturnType, metSym.ReturnType);
                        if (equalSignature)
                        {
                            var thisTypes = this.ParameterTypesWithAnnotations;
                            var theirTypes = metSym.Parameters;
                            for (int i = 0; i < thisTypes.Length; i++)
                            {
                                if (!TypeSymbol.Equals(thisTypes[i].Type,theirTypes[i].Type))
                                {
                                    equalSignature = false;
                                    break;
                                }
                            }
                        }
                        if (equalSignature)
                        {
                            diagnostics.Add(ErrorCode.ERR_CaseDifference, location, baseType.Name, "method", member.Name, this.Name);
                        }

                    }
                }
            }

            if (overriddenMethod is null )
                _overrideState = 1; // checked and does not have an override
            else
            {
                _overrideState = 2;  // checked and has a (correct) override

                CustomModifierUtils.CopyMethodCustomModifiers(overriddenMethod, this, out _lazyReturnType,
                                                              out _lazyRefCustomModifiers,
                                                              out _lazyParameters, alsoCopyParamsModifier: true);
            }
            var node = this.SyntaxNode.Green as Microsoft.CodeAnalysis.CSharp.Syntax.InternalSyntax.MethodDeclarationSyntax;
            if (node != null)   // This can be a generated node, for example for Records
            {
                var mods = this.DeclarationModifiers;
                if (_overrideState == 2)
                {
                    if (this.Name != overriddenMethod.Name)
                    {
                        this._name = overriddenMethod.Name;
                    }
                    // remove generated Virtual Modifiers
                    foreach (var token in node.Modifiers)
                    {
                        if (token.Kind == SyntaxKind.VirtualKeyword && token.XGenerated)
                        {
                            mods = mods & ~DeclarationModifiers.Virtual;
                        }
                    }

                    flags = new Flags(flags.MethodKind, mods, this.ReturnsVoid, flags.IsExtensionMethod, flags.IsNullableAnalysisEnabled, flags.IsMetadataVirtual(true));
                }
                else
                {
                    // remove generated Override Modifiers
                    foreach (var token in node.Modifiers)
                    {
                        if (token.Kind == SyntaxKind.OverrideKeyword && token.XGenerated)
                        {
                            mods = mods & ~DeclarationModifiers.Override;
                        }
                    }
                    flags = new Flags(flags.MethodKind, mods, this.ReturnsVoid, flags.IsExtensionMethod, flags.IsNullableAnalysisEnabled, flags.IsMetadataVirtual(true));
                }
                this.DeclarationModifiers = mods;
            }
            return overriddenMethod;
        }
    }

}
