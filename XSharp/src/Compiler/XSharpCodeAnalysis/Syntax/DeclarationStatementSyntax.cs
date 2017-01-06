﻿/*
   Copyright 2016-2017 XSharp B.V.

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
namespace Microsoft.CodeAnalysis.CSharp.Syntax
{
    public partial class LocalDeclarationStatementSyntax : StatementSyntax
    {
        public bool IsRef
        {
            get
            {
                return this.Modifiers.Any(SyntaxKind.RefKeyword);
            }
        }
        public bool IsStatic
        {
            get
            {
                return this.Modifiers.Any(SyntaxKind.StaticKeyword);
            }
        }
    }
}