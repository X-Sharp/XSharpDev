// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeStyle;
using Microsoft.CodeAnalysis.CSharp.CodeFixes.GenerateConstructor;
using Microsoft.CodeAnalysis.CSharp.CodeStyle;
using Microsoft.CodeAnalysis.CSharp.Diagnostics;
using Microsoft.CodeAnalysis.CSharp.Test.Utilities;
using Microsoft.CodeAnalysis.Diagnostics;
using Roslyn.Test.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.Editor.CSharp.UnitTests.Diagnostics.GenerateConstructor
{
    public class GenerateConstructorTests : AbstractCSharpDiagnosticProviderBasedUserDiagnosticTest
    {
        internal override Tuple<DiagnosticAnalyzer, CodeFixProvider> CreateDiagnosticProviderAndFixer(Workspace workspace)
        {
            return new Tuple<DiagnosticAnalyzer, CodeFixProvider>(
                null, new GenerateConstructorCodeFixProvider());
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateConstructor)]
        public async Task TestWithSimpleArgument()
        {
            await TestAsync(
@"class C
{
    void M()
    {
        new [|C|](1);
    }
}",
@"class C
{
    private int v;

    public C(int v)
    {
        this.v = v;
    }

    void M()
    {
        new C(1);
    }
}");
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateConstructor)]
        public async Task TestWithSimpleArgument_UseExpressionBody1()
        {
            await TestAsync(
@"class C
{
    void M()
    {
        new [|C|](1);
    }
}",
@"class C
{
    private int v;

    public C(int v) => this.v = v; void M()
    {
        new C(1);
    }
}",
options: Option(CSharpCodeStyleOptions.PreferExpressionBodiedConstructors, CodeStyleOptions.TrueWithNoneEnforcement));
        }

        [Fact, WorkItem(910589, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/910589"), Trait(Traits.Feature, Traits.Features.CodeActionsGenerateConstructor)]
        public async Task TestWithNoArgs()
        {
            await TestAsync(
@"class C
{
    public C(int v)
    {
    }

    void M()
    {
        new [|C|]();
    }
}",
@"class C
{
    public C()
    {
    }

    public C(int v)
    {
    }

    void M()
    {
        new C();
    }
}");
        }

        [Fact, WorkItem(910589, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/910589"), Trait(Traits.Feature, Traits.Features.CodeActionsGenerateConstructor)]
        public async Task TestWithNamedArg()
        {
            await TestAsync(
@"class C
{
    void M()
    {
        new [|C(foo: 1)|];
    }
}",
@"class C
{
    private int foo;

    public C(int foo)
    {
        this.foo = foo;
    }

    void M()
    {
        new C(foo: 1);
    }
}");
        }

        [Fact, WorkItem(910589, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/910589"), Trait(Traits.Feature, Traits.Features.CodeActionsGenerateConstructor)]
        public async Task TestWithExistingField1()
        {
            await TestAsync(
@"class C
{
    void M()
    {
        new [|D(foo: 1)|];
    }
}

class D
{
    private int foo;
}",
@"class C
{
    void M()
    {
        new D(foo: 1);
    }
}

class D
{
    private int foo;

    public D(int foo)
    {
        this.foo = foo;
    }
}");
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateConstructor)]
        public async Task TestWithExistingField2()
        {
            await TestAsync(
@"class C
{
    void M()
    {
        new [|D|](1);
    }
}

class D
{
    private string v;
}",
@"class C
{
    void M()
    {
        new D(1);
    }
}

class D
{
    private string v;
    private int v1;

    public D(int v1)
    {
        this.v1 = v1;
    }
}");
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateConstructor)]
        public async Task TestWithExistingField3()
        {
            await TestAsync(
@"class C
{
    void M()
    {
        new [|D|](1);
    }
}

class B
{
    protected int v;
}

class D : B
{
}",
@"class C
{
    void M()
    {
        new D(1);
    }
}

class B
{
    protected int v;
}

class D : B
{
    public D(int v)
    {
        this.v = v;
    }
}");
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateConstructor)]
        public async Task TestWithExistingField4()
        {
            await TestAsync(
@"class C
{
    void M()
    {
        new [|D|](1);
    }
}

class B
{
    private int v;
}

class D : B
{
}",
@"class C
{
    void M()
    {
        new D(1);
    }
}

class B
{
    private int v;
}

class D : B
{
    private int v;

    public D(int v)
    {
        this.v = v;
    }
}");
        }

        [WorkItem(539444, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/539444")]
        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateConstructor)]
        public async Task TestWithExistingField5()
        {
            await TestAsync(
@"class C
{
    void M(int X)
    {
        new [|D|](X);
    }
}

class D
{
    int X;
}",
@"class C
{
    void M(int X)
    {
        new D(X);
    }
}

class D
{
    int X;

    public D(int x)
    {
        X = x;
    }
}");
        }

        [WorkItem(539444, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/539444")]
        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateConstructor)]
        public async Task TestWithExistingField5WithQualification()
        {
            await TestAsync(
@"class C
{
    void M(int X)
    {
        new [|D|](X);
    }
}

class D
{
    int X;
}",
@"class C
{
    void M(int X)
    {
        new D(X);
    }
}

class D
{
    int X;

    public D(int x)
    {
        this.X = x;
    }
}",
                options: Option(CodeStyleOptions.QualifyFieldAccess, true, NotificationOption.Error));
        }

        [WorkItem(539444, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/539444")]
        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateConstructor)]
        public async Task TestWithExistingField6()
        {
            await TestAsync(
@"class C
{
    void M(int X)
    {
        new [|D|](X);
    }
}

class B
{
    private int X;
}

class D : B
{
}",
@"class C
{
    void M(int X)
    {
        new D(X);
    }
}

class B
{
    private int X;
}

class D : B
{
    private int x;

    public D(int x)
    {
        this.x = x;
    }
}");
        }

        [WorkItem(539444, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/539444")]
        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateConstructor)]
        public async Task TestWithExistingField7()
        {
            await TestAsync(
@"class C
{
    void M(int X)
    {
        new [|D|](X);
    }
}

class B
{
    protected int X;
}

class D : B
{
}",
@"class C
{
    void M(int X)
    {
        new D(X);
    }
}

class B
{
    protected int X;
}

class D : B
{
    public D(int x)
    {
        X = x;
    }
}");
        }

        [WorkItem(539444, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/539444")]
        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateConstructor)]
        public async Task TestWithExistingField7WithQualification()
        {
            await TestAsync(
@"class C
{
    void M(int X)
    {
        new [|D|](X);
    }
}

class B
{
    protected int X;
}

class D : B
{
}",
@"class C
{
    void M(int X)
    {
        new D(X);
    }
}

class B
{
    protected int X;
}

class D : B
{
    public D(int x)
    {
        this.X = x;
    }
}",
                options: Option(CodeStyleOptions.QualifyFieldAccess, true, NotificationOption.Error));
        }

        [WorkItem(539444, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/539444")]
        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateConstructor)]
        public async Task TestWithExistingField8()
        {
            await TestAsync(
@"class C
{
    void M(int X)
    {
        new [|D|](X);
    }
}

class B
{
    protected static int x;
}

class D : B
{
}",
@"class C
{
    void M(int X)
    {
        new D(X);
    }
}

class B
{
    protected static int x;
}

class D : B
{
    private int x1;

    public D(int x1)
    {
        this.x1 = x1;
    }
}");
        }

        [WorkItem(539444, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/539444")]
        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateConstructor)]
        public async Task TestWithExistingField9()
        {
            await TestAsync(
@"class C
{
    void M(int X)
    {
        new [|D|](X);
    }
}

class B
{
    protected int x;
}

class D : B
{
    int X;
}",
@"class C
{
    void M(int X)
    {
        new D(X);
    }
}

class B
{
    protected int x;
}

class D : B
{
    int X;

    public D(int x)
    {
        this.x = x;
    }
}");
        }

        [WorkItem(539444, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/539444")]
        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateConstructor)]
        public async Task TestWithExistingProperty1()
        {
            await TestAsync(
@"class C
{
    void M(int X)
    {
        new [|D|](X);
    }
}

class D
{
    public int X { get; private set; }
}",
@"class C
{
    void M(int X)
    {
        new D(X);
    }
}

class D
{
    public D(int x)
    {
        X = x;
    }

    public int X { get; private set; }
}");
        }

        [WorkItem(539444, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/539444")]
        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateConstructor)]
        public async Task TestWithExistingProperty1WithQualification()
        {
            await TestAsync(
@"class C
{
    void M(int X)
    {
        new [|D|](X);
    }
}

class D
{
    public int X { get; private set; }
}",
@"class C
{
    void M(int X)
    {
        new D(X);
    }
}

class D
{
    public D(int x)
    {
        this.X = x;
    }

    public int X { get; private set; }
}",
                options: Option(CodeStyleOptions.QualifyPropertyAccess, true, NotificationOption.Error));
        }

        [WorkItem(539444, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/539444")]
        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateConstructor)]
        public async Task TestWithExistingProperty2()
        {
            await TestAsync(
@"class C
{
    void M(int X)
    {
        new [|D|](X);
    }
}

class B
{
    public int X { get; private set; }
}

class D : B
{
}",
@"class C
{
    void M(int X)
    {
        new D(X);
    }
}

class B
{
    public int X { get; private set; }
}

class D : B
{
    private int x;

    public D(int x)
    {
        this.x = x;
    }
}");
        }

        [WorkItem(539444, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/539444")]
        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateConstructor)]
        public async Task TestWithExistingProperty3()
        {
            await TestAsync(
@"class C
{
    void M(int X)
    {
        new [|D|](X);
    }
}

class B
{
    public int X { get; protected set; }
}

class D : B
{
}",
@"class C
{
    void M(int X)
    {
        new D(X);
    }
}

class B
{
    public int X { get; protected set; }
}

class D : B
{
    public D(int x)
    {
        X = x;
    }
}");
        }

        [WorkItem(539444, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/539444")]
        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateConstructor)]
        public async Task TestWithExistingProperty3WithQualification()
        {
            await TestAsync(
@"class C
{
    void M(int X)
    {
        new [|D|](X);
    }
}

class B
{
    public int X { get; protected set; }
}

class D : B
{
}",
@"class C
{
    void M(int X)
    {
        new D(X);
    }
}

class B
{
    public int X { get; protected set; }
}

class D : B
{
    public D(int x)
    {
        this.X = x;
    }
}",
                options: Option(CodeStyleOptions.QualifyPropertyAccess, true, NotificationOption.Error));
        }

        [WorkItem(539444, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/539444")]
        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateConstructor)]
        public async Task TestWithExistingProperty4()
        {
            await TestAsync(
@"class C
{
    void M(int X)
    {
        new [|D|](X);
    }
}

class B
{
    protected int X { get; set; }
}

class D : B
{
}",
@"class C
{
    void M(int X)
    {
        new D(X);
    }
}

class B
{
    protected int X { get; set; }
}

class D : B
{
    public D(int x)
    {
        X = x;
    }
}");
        }

        [WorkItem(539444, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/539444")]
        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateConstructor)]
        public async Task TestWithExistingProperty4WithQualification()
        {
            await TestAsync(
@"class C
{
    void M(int X)
    {
        new [|D|](X);
    }
}

class B
{
    protected int X { get; set; }
}

class D : B
{
}",
@"class C
{
    void M(int X)
    {
        new D(X);
    }
}

class B
{
    protected int X { get; set; }
}

class D : B
{
    public D(int x)
    {
        this.X = x;
    }
}",
                options: Option(CodeStyleOptions.QualifyPropertyAccess, true, NotificationOption.Error));
        }

        [WorkItem(539444, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/539444")]
        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateConstructor)]
        public async Task TestWithExistingProperty5()
        {
            await TestAsync(
@"class C
{
    void M(int X)
    {
        new [|D|](X);
    }
}

class B
{
    protected int X { get; }
}

class D : B
{
}",
@"class C
{
    void M(int X)
    {
        new D(X);
    }
}

class B
{
    protected int X { get; }
}

class D : B
{
    private int x;

    public D(int x)
    {
        this.x = x;
    }
}");
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateConstructor)]
        public async Task TestWithOutParam()
        {
            await TestAsync(
@"class C
{
    void M(int i)
    {
        new [|D|](out i);
    }
}

class D
{
}",
@"class C
{
    void M(int i)
    {
        new D(out i);
    }
}

class D
{
    public D(out int i)
    {
        i = 0;
    }
}");
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateConstructor)]
        public async Task TestWithBaseDelegatingConstructor1()
        {
            await TestAsync(
@"class C
{
    void M()
    {
        new [|D|](1);
    }
}

class B
{
    protected B(int x)
    {
    }
}

class D : B
{
}",
@"class C
{
    void M()
    {
        new D(1);
    }
}

class B
{
    protected B(int x)
    {
    }
}

class D : B
{
    public D(int x) : base(x)
    {
    }
}");
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateConstructor)]
        public async Task TestWithBaseDelegatingConstructor2()
        {
            await TestAsync(
@"class C
{
    void M()
    {
        new [|D|](1);
    }
}

class B
{
    private B(int x)
    {
    }
}

class D : B
{
}",
@"class C
{
    void M()
    {
        new D(1);
    }
}

class B
{
    private B(int x)
    {
    }
}

class D : B
{
    private int v;

    public D(int v)
    {
        this.v = v;
    }
}");
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateConstructor)]
        public async Task TestStructInLocalInitializerWithSystemType()
        {
            await TestAsync(
@"struct S
{
    void M()
    {
        S s = new [|S|](System.DateTime.Now);
    }
}",
@"using System;

struct S
{
    private DateTime now;

    public S(DateTime now)
    {
        this.now = now;
    }

    void M()
    {
        S s = new S(System.DateTime.Now);
    }
}");
        }

        [WorkItem(539489, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/539489")]
        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateConstructor)]
        public async Task TestEscapedName()
        {
            await TestAsync(
@"class C
{
    void M()
    {
        new [|@C|](1);
    }
}",
@"class C
{
    private int v;

    public C(int v)
    {
        this.v = v;
    }

    void M()
    {
        new @C(1);
    }
}");
        }

        [WorkItem(539489, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/539489")]
        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateConstructor)]
        public async Task TestEscapedKeyword()
        {
            await TestAsync(
@"class @int
{
    void M()
    {
        new [|@int|](1);
    }
}",
@"class @int
{
    private int v;

    public @int(int v)
    {
        this.v = v;
    }

    void M()
    {
        new @int(1);
    }
}");
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateConstructor)]
        public async Task TestIsSymbolAccessibleWithInternalField()
        {
            await TestAsync(
@"class Base
{
    internal long field;

    void Main()
    {
        int field = 5;
        new [|Derived|](field);
    }
}

class Derived : Base
{
}",
@"class Base
{
    internal long field;

    void Main()
    {
        int field = 5;
        new Derived(field);
    }
}

class Derived : Base
{
    public Derived(int field)
    {
        this.field = field;
    }
}");
        }

        [WorkItem(539548, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/539548")]
        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateConstructor)]
        public async Task TestFormatting()
        {
            await TestAsync(
@"class C
{
    void M()
    {
        new [|C|](1);
    }
}",
@"class C
{
    private int v;

    public C(int v)
    {
        this.v = v;
    }

    void M()
    {
        new C(1);
    }
}",
compareTokens: false);
        }

        [WorkItem(5864, "DevDiv_Projects/Roslyn")]
        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateConstructor)]
        public async Task TestNotOnStructConstructor()
        {
            await TestMissingAsync(
@"struct Struct
{
    void Main()
    {
        Struct s = new [|Struct|]();
    }
}");
        }

        [WorkItem(539787, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/539787")]
        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateConstructor)]
        public async Task TestGenerateIntoCorrectPart()
        {
            await TestAsync(
@"partial class C
{
}

partial class C
{
    void Method()
    {
        C c = new [|C|](""a"");
    }
}",
@"partial class C
{
}

partial class C
{
    private string v;

    public C(string v)
    {
        this.v = v;
    }

    void Method()
    {
        C c = new C(""a"");
    }
}");
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateConstructor)]
        public async Task TestDelegateToSmallerConstructor1()
        {
            await TestAsync(
@"class A
{
    void M()
    {
        Delta d1 = new Delta(""ss"", 3);
        Delta d2 = new [|Delta|](""ss"", 5, true);
    }
}

class Delta
{
    private string v1;
    private int v2;

    public Delta(string v1, int v2)
    {
        this.v1 = v1;
        this.v2 = v2;
    }
}",
@"class A
{
    void M()
    {
        Delta d1 = new Delta(""ss"", 3);
        Delta d2 = new Delta(""ss"", 5, true);
    }
}

class Delta
{
    private string v1;
    private int v2;
    private bool v;

    public Delta(string v1, int v2)
    {
        this.v1 = v1;
        this.v2 = v2;
    }

    public Delta(string v1, int v2, bool v) : this(v1, v2)
    {
        this.v = v;
    }
}");
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateConstructor)]
        public async Task TestDelegateToSmallerConstructor2()
        {
            await TestAsync(
@"class A
{
    void M()
    {
        Delta d1 = new Delta(""ss"", 3);
        Delta d2 = new [|Delta|](""ss"", 5, true);
    }
}

class Delta
{
    private string a;
    private int b;

    public Delta(string a, int b)
    {
        this.a = a;
        this.b = b;
    }
}",
@"class A
{
    void M()
    {
        Delta d1 = new Delta(""ss"", 3);
        Delta d2 = new Delta(""ss"", 5, true);
    }
}

class Delta
{
    private string a;
    private int b;
    private bool v;

    public Delta(string a, int b)
    {
        this.a = a;
        this.b = b;
    }

    public Delta(string a, int b, bool v) : this(a, b)
    {
        this.v = v;
    }
}");
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateConstructor)]
        public async Task TestDelegateToSmallerConstructor3()
        {
            await TestAsync(
@"class A
{
    void M()
    {
        var d1 = new Base(""ss"", 3);
        var d2 = new [|Delta|](""ss"", 5, true);
    }
}

class Base
{
    private string v1;
    private int v2;

    public Base(string v1, int v2)
    {
        this.v1 = v1;
        this.v2 = v2;
    }
}

class Delta : Base
{
}",
@"class A
{
    void M()
    {
        var d1 = new Base(""ss"", 3);
        var d2 = new Delta(""ss"", 5, true);
    }
}

class Base
{
    private string v1;
    private int v2;

    public Base(string v1, int v2)
    {
        this.v1 = v1;
        this.v2 = v2;
    }
}

class Delta : Base
{
    private bool v;

    public Delta(string v1, int v2, bool v) : base(v1, v2)
    {
        this.v = v;
    }
}");
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateConstructor)]
        public async Task TestDelegateToSmallerConstructor4()
        {
            await TestAsync(
@"class A
{
    void M()
    {
        Delta d1 = new Delta(""ss"", 3);
        Delta d2 = new [|Delta|](""ss"", 5, true);
    }
}

class Delta
{
    private string v1;
    private int v2;

    public Delta(string v1, int v2)
    {
        this.v1 = v1;
        this.v2 = v2;
    }
}",
@"class A
{
    void M()
    {
        Delta d1 = new Delta(""ss"", 3);
        Delta d2 = new Delta(""ss"", 5, true);
    }
}

class Delta
{
    private string v1;
    private int v2;
    private bool v;

    public Delta(string v1, int v2)
    {
        this.v1 = v1;
        this.v2 = v2;
    }

    public Delta(string v1, int v2, bool v) : this(v1, v2)
    {
        this.v = v;
    }
}");
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateConstructor)]
        public async Task TestGenerateFromThisInitializer1()
        {
            await TestAsync(
@"class C
{
    public C() [|: this(4)|]
    {
    }
}",
@"class C
{
    private int v;

    public C() : this(4)
    {
    }

    public C(int v)
    {
        this.v = v;
    }
}");
        }

        [Fact, WorkItem(910589, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/910589"), Trait(Traits.Feature, Traits.Features.CodeActionsGenerateConstructor)]
        public async Task TestGenerateFromThisInitializer2()
        {
            await TestAsync(
@"class C
{
    public C(int i) [|: this()|]
    {
    }
}",
@"class C
{
    public C()
    {
    }

    public C(int i) : this()
    {
    }
}");
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateConstructor)]
        public async Task TestGenerateFromBaseInitializer1()
        {
            await TestAsync(
@"class C : B
{
    public C(int i) [|: base(i)|]
    {
    }
}

class B
{
}",
@"class C : B
{
    public C(int i) : base(i)
    {
    }
}

class B
{
    private int i;

    public B(int i)
    {
        this.i = i;
    }
}");
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateConstructor)]
        public async Task TestGenerateFromBaseInitializer2()
        {
            await TestAsync(
@"class C : B
{
    public C(int i) [|: base(i)|]
    {
    }
}

class B
{
    int i;
}",
@"class C : B
{
    public C(int i) : base(i)
    {
    }
}

class B
{
    int i;

    public B(int i)
    {
        this.i = i;
    }
}");
        }

        [WorkItem(539969, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/539969")]
        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateConstructor)]
        public async Task TestNotOnExistingConstructor()
        {
            await TestMissingAsync(
@"class C
{
    private class D
    {
    }
}

class A
{
    void M()
    {
        C.D d = new C.[|D|]();
    }
}");
        }

        [WorkItem(539972, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/539972")]
        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateConstructor)]
        public async Task TestUnavailableTypeParameters()
        {
            await TestAsync(
@"class C<T1, T2>
{
    public void Foo(T1 t1, T2 t2)
    {
        A a = new [|A|](t1, t2);
    }
}

internal class A
{
}",
@"class C<T1, T2>
{
    public void Foo(T1 t1, T2 t2)
    {
        A a = new A(t1, t2);
    }
}

internal class A
{
    private object t1;
    private object t2;

    public A(object t1, object t2)
    {
        this.t1 = t1;
        this.t2 = t2;
    }
}");
        }

        [WorkItem(541020, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/541020")]
        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateConstructor)]
        public async Task TestGenerateCallToDefaultConstructorInStruct()
        {
            await TestAsync(
@"class Program
{
    void Main()
    {
        Apartment Metropolitan = new Apartment([|""Pine""|]);
    }
}

struct Apartment
{
    private int v1;

    public Apartment(int v1)
    {
        this.v1 = v1;
    }
}",
@"class Program
{
    void Main()
    {
        Apartment Metropolitan = new Apartment(""Pine"");
    }
}

struct Apartment
{
    private int v1;
    private string v;

    public Apartment(int v1)
    {
        this.v1 = v1;
    }

    public Apartment(string v) : this()
    {
        this.v = v;
    }
}");
        }

        [WorkItem(541121, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/541121")]
        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateConstructor)]
        public async Task TestReadonlyFieldDelegation()
        {
            await TestAsync(
@"class C
{
    private readonly int x;

    void Test()
    {
        int x = 10;
        C c = new [|C|](x);
    }
}",
@"class C
{
    private readonly int x;

    public C(int x)
    {
        this.x = x;
    }

    void Test()
    {
        int x = 10;
        C c = new C(x);
    }
}");
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateConstructor)]
        public async Task TestNoGenerationIntoEntirelyHiddenType()
        {
            await TestMissingAsync(
@"class C
{
    void Foo()
    {
        new [|D|](1, 2, 3);
    }
}

#line hidden
class D
{
}
#line default");
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateConstructor)]
        public async Task TestNestedConstructorCall()
        {
            await TestAsync(
@"class C
{
    void Foo()
    {
        var d = new D([|v|]: new D(u: 1));
    }
}

class D
{
    private int u;

    public D(int u)
    {
    }
}",
@"class C
{
    void Foo()
    {
        var d = new D(v: new D(u: 1));
    }
}

class D
{
    private int u;
    private D v;

    public D(int u)
    {
    }

    public D(D v)
    {
        this.v = v;
    }
}");
        }

        [WorkItem(530003, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/530003")]
        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateConstructor)]
        public async Task TestAttributesWithArgument()
        {
            await TestAsync(
@"using System;

[AttributeUsage(AttributeTargets.Class)]
class MyAttribute : Attribute
{
}

[[|MyAttribute(123)|]]
class D
{
}",
@"using System;

[AttributeUsage(AttributeTargets.Class)]
class MyAttribute : Attribute
{
    private int v;

    public MyAttribute(int v)
    {
        this.v = v;
    }
}

[MyAttribute(123)]
class D
{
}");
        }

        [WorkItem(530003, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/530003")]
        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateConstructor)]
        public async Task TestAttributesWithMultipleArguments()
        {
            await TestAsync(
@"using System;

[AttributeUsage(AttributeTargets.Class)]
class MyAttribute : Attribute
{
}

[[|MyAttribute(true, 1, ""hello"")|]]
class D
{
}",
@"using System;

[AttributeUsage(AttributeTargets.Class)]
class MyAttribute : Attribute
{
    private bool v1;
    private int v2;
    private string v3;

    public MyAttribute(bool v1, int v2, string v3)
    {
        this.v1 = v1;
        this.v2 = v2;
        this.v3 = v3;
    }
}

[MyAttribute(true, 1, ""hello"")]
class D
{
}");
        }

        [WorkItem(530003, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/530003")]
        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateConstructor)]
        public async Task TestAttributesWithNamedArguments()
        {
            await TestAsync(
@"using System;

[AttributeUsage(AttributeTargets.Class)]
class MyAttribute : Attribute
{
}

[[|MyAttribute(true, 1, topic = ""hello"")|]]
class D
{
}",
@"using System;

[AttributeUsage(AttributeTargets.Class)]
class MyAttribute : Attribute
{
    private bool v1;
    private int v2;
    private string topic;

    public MyAttribute(bool v1, int v2, string topic)
    {
        this.v1 = v1;
        this.v2 = v2;
        this.topic = topic;
    }
}

[MyAttribute(true, 1, topic = ""hello"")]
class D
{
}");
        }

        [WorkItem(530003, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/530003")]
        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateConstructor)]
        public async Task TestAttributesWithAdditionalConstructors()
        {
            await TestAsync(
@"using System;

[AttributeUsage(AttributeTargets.Class)]
class MyAttribute : Attribute
{
    private int v;

    public MyAttribute(int v)
    {
        this.v = v;
    }
}

[[|MyAttribute(true, 1)|]]
class D
{
}",
@"using System;

[AttributeUsage(AttributeTargets.Class)]
class MyAttribute : Attribute
{
    private int v;
    private bool v1;
    private int v2;

    public MyAttribute(int v)
    {
        this.v = v;
    }

    public MyAttribute(bool v1, int v2)
    {
        this.v1 = v1;
        this.v2 = v2;
    }
}

[MyAttribute(true, 1)]
class D
{
}");
        }

        [WorkItem(530003, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/530003")]
        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateConstructor)]
        public async Task TestAttributesWithOverloading()
        {
            await TestAsync(
@"using System;

[AttributeUsage(AttributeTargets.Class)]
class MyAttribute : Attribute
{
    private int v;

    public MyAttribute(int v)
    {
        this.v = v;
    }
}

[[|MyAttribute(true)|]]
class D
{
}",
@"using System;

[AttributeUsage(AttributeTargets.Class)]
class MyAttribute : Attribute
{
    private int v;
    private bool v1;

    public MyAttribute(int v)
    {
        this.v = v;
    }

    public MyAttribute(bool v1)
    {
        this.v1 = v1;
    }
}

[MyAttribute(true)]
class D
{
}");
        }

        [WorkItem(530003, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/530003")]
        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateConstructor)]
        public async Task TestAttributesWithOverloadingMultipleParameters()
        {
            await TestAsync(
@"using System;

[AttributeUsage(AttributeTargets.Class)]
class MyAttrAttribute : Attribute
{
    private bool v1;
    private int v2;

    public MyAttrAttribute(bool v1, int v2)
    {
        this.v1 = v1;
        this.v2 = v2;
    }
}

[|[MyAttrAttribute(1, true)]|]
class D
{
}",
@"using System;

[AttributeUsage(AttributeTargets.Class)]
class MyAttrAttribute : Attribute
{
    private bool v1;
    private int v2;
    private int v;
    private bool v3;

    public MyAttrAttribute(bool v1, int v2)
    {
        this.v1 = v1;
        this.v2 = v2;
    }

    public MyAttrAttribute(int v, bool v3)
    {
        this.v = v;
        this.v3 = v3;
    }
}

[MyAttrAttribute(1, true)]
class D
{
}");
        }

        [WorkItem(530003, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/530003")]
        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateConstructor)]
        public async Task TestAttributesWithAllValidParameters()
        {
            await TestAsync(
@"using System;

enum A
{
    A1
}

[AttributeUsage(AttributeTargets.Class)]
class MyAttrAttribute : Attribute
{
}

[|[MyAttrAttribute(new int[] { 1, 2, 3 }, A.A1, true, (byte)1, 'a', (short)12, (int)1, (long)5L, 5D, 3.5F, ""hello"")]|]
class D
{
}",
@"using System;

enum A
{
    A1
}

[AttributeUsage(AttributeTargets.Class)]
class MyAttrAttribute : Attribute
{
    private int[] v1;
    private A a1;
    private bool v2;
    private byte v3;
    private char v4;
    private short v5;
    private int v6;
    private long v7;
    private double v8;
    private float v9;
    private string v10;

    public MyAttrAttribute(int[] v1, A a1, bool v2, byte v3, char v4, short v5, int v6, long v7, double v8, float v9, string v10)
    {
        this.v1 = v1;
        this.a1 = a1;
        this.v2 = v2;
        this.v3 = v3;
        this.v4 = v4;
        this.v5 = v5;
        this.v6 = v6;
        this.v7 = v7;
        this.v8 = v8;
        this.v9 = v9;
        this.v10 = v10;
    }
}

[MyAttrAttribute(new int[] { 1, 2, 3 }, A.A1, true, (byte)1, 'a', (short)12, (int)1, (long)5L, 5D, 3.5F, ""hello"")]
class D
{
}");
        }

        [WorkItem(530003, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/530003")]
        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateConstructor)]
        public async Task TestAttributesWithDelegation()
        {
            await TestMissingAsync(
@"using System;

[AttributeUsage(AttributeTargets.Class)]
class MyAttrAttribute : Attribute
{
}

[|[MyAttrAttribute(() => {
    return;
})]|]
class D
{
}");
        }

        [WorkItem(530003, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/530003")]
        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateConstructor)]
        public async Task TestAttributesWithLambda()
        {
            await TestMissingAsync(
@"using System;

[AttributeUsage(AttributeTargets.Class)]
class MyAttrAttribute : Attribute
{
}

[|[MyAttrAttribute(() => 5)]|]
class D
{
}");
        }

        [WorkItem(889349, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/889349")]
        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateConstructor)]
        public async Task TestConstructorGenerationForDifferentNamedParameter()
        {
            await TestAsync(
@"
class Program
{
    static void Main(string[] args)
    {
        var ss = new [|Program(wde: 1)|];
    }

    Program(int s)
    {

    }
}
",
@"
class Program
{
    private int wde;

    static void Main(string[] args)
    {
        var ss = new Program(wde: 1);
    }

    Program(int s)
    {

    }

    public Program(int wde)
    {
        this.wde = wde;
    }
}
", compareTokens: false);
        }

        [WorkItem(528257, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/528257")]
        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateConstructor)]
        public async Task TestGenerateInInaccessibleType()
        {
            await TestAsync(
@"class Foo
{
    class Bar
    {
    }
}

class A
{
    static void Main(string[] args)
    {
        var s = new [|Foo.Bar(5)|];
    }
}",
@"class Foo
{
    class Bar
    {
        private int v;

        public Bar(int v)
        {
            this.v = v;
        }
    }
}

class A
{
    static void Main(string[] args)
    {
        var s = new Foo.Bar(5);
    }
}");
        }

        public partial class GenerateConstructorTestsWithFindMissingIdentifiersAnalyzer : AbstractCSharpDiagnosticProviderBasedUserDiagnosticTest
        {
            internal override Tuple<DiagnosticAnalyzer, CodeFixProvider> CreateDiagnosticProviderAndFixer(Workspace workspace)
            {
                return new Tuple<DiagnosticAnalyzer, CodeFixProvider>(
                new CSharpUnboundIdentifiersDiagnosticAnalyzer(), new GenerateConstructorCodeFixProvider());
            }

            [WorkItem(1241, @"https://github.com/dotnet/roslyn/issues/1241")]
            [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateConstructor)]
            public async Task TestGenerateConstructorInIncompleteLambda()
            {
                await TestAsync(
@"using System.Threading.Tasks;

class C
{
    C()
    {
        Task.Run(() => {
            new [|C|](0) });
    }
}",
@"using System.Threading.Tasks;

class C
{
    private int v;

    public C(int v)
    {
        this.v = v;
    }

    C()
    {
        Task.Run(() => {
            new C(0) });
    }
}");
            }
        }

        [WorkItem(5274, "https://github.com/dotnet/roslyn/issues/5274")]
        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateConstructor)]
        public async Task TestGenerateIntoDerivedClassWithAbstractBase()
        {
            await TestAsync(
@"class Class1
{
    private void Foo(string value)
    {
        var rewriter = new [|Derived|](value);
    }

    private class Derived : Base
    {
    }

    public abstract partial class Base
    {
        private readonly bool _val;

        public Base(bool val = false)
        {
            _val = val;
        }
    }
}",
@"class Class1
{
    private void Foo(string value)
    {
        var rewriter = new Derived(value);
    }

    private class Derived : Base
    {
        private string value;

        public Derived(string value)
        {
            this.value = value;
        }
    }

    public abstract partial class Base
    {
        private readonly bool _val;

        public Base(bool val = false)
        {
            _val = val;
        }
    }
}");
        }

        [WorkItem(6541, "https://github.com/dotnet/Roslyn/issues/6541")]
        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateConstructor)]
        public async Task TestGenerateFromDerivedClass()
        {
            await TestAsync(
@"class Base
{
    public Base(string value)
    {
    }
}

class [||]Derived : Base
{
}",
@"class Base
{
    public Base(string value)
    {
    }
}

class Derived : Base
{
    public Derived(string value) : base(value)
    {
    }
}");
        }

        [WorkItem(6541, "https://github.com/dotnet/Roslyn/issues/6541")]
        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateConstructor)]
        public async Task TestGenerateFromDerivedClass2()
        {
            await TestAsync(
@"class Base
{
    public Base(int a, string value = null)
    {
    }
}

class [||]Derived : Base
{
}",
@"class Base
{
    public Base(int a, string value = null)
    {
    }
}

class Derived : Base
{
    public Derived(int a, string value = null) : base(a, value)
    {
    }
}");
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateConstructor)]
        public async Task TestGenerateWithIncorrectConstructorArguments_Crash()
        {
            await TestAsync(
@"using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

abstract class Y
{
    class X : Y
    {
        void M()
        {
            new X(new [|string|]());
        }
    }
}",
@"using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

abstract class Y
{
    class X : Y
    {
        private string v;

        public X(string v)
        {
            this.v = v;
        }

        void M()
        {
            new X(new string());
        }
    }
}");
        }

        [WorkItem(9575, "https://github.com/dotnet/roslyn/issues/9575")]
        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateConstructor)]
        public async Task TestMissingOnMethodCall()
        {
            await TestMissingAsync(
@"class C
{
    public C(int arg)
    {
    }

    public bool M(string s, int i, bool b)
    {
        return [|M|](i, b);
    }
}");
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateConstructor)]
        public async Task Tuple()
        {
            await TestAsync(
@"class C
{
    void M()
    {
        new [|C|]((1, ""hello""), true);
    }
}",
@"class C
{
    private (int, string) p;
    private bool v;

    public C((int, string) p, bool v)
    {
        this.p = p;
        this.v = v;
    }

    void M()
    {
        new C((1, ""hello""), true);
    }
}",
parseOptions: TestOptions.Regular,
withScriptOption: true);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateConstructor)]
        public async Task TupleWithNames()
        {
            await TestAsync(
@"class C
{
    void M()
    {
        new [|C|]((a: 1, b: ""hello""));
    }
}",
@"class C
{
    private (int a, string b) p;

    public C((int a, string b) p)
    {
        this.p = p;
    }

    void M()
    {
        new C((a: 1, b: ""hello""));
    }
}",
parseOptions: TestOptions.Regular,
withScriptOption: true);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateConstructor)]
        public async Task TupleWithOneName()
        {
            await TestAsync(
@"class C
{
    void M()
    {
        new [|C|]((a: 1, ""hello""));
    }
}",
@"class C
{
    private (int a, string) p;

    public C((int a, string) p)
    {
        this.p = p;
    }

    void M()
    {
        new C((a: 1, ""hello""));
    }
}",
parseOptions: TestOptions.Regular,
withScriptOption: true);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateConstructor)]
        public async Task TupleAndExistingField()
        {
            await TestAsync(
@"class C
{
    void M()
    {
        new [|D(existing: (1, ""hello""))|];
    }
}

class D
{
    private (int, string) existing;
}",
@"class C
{
    void M()
    {
        new D(existing: (1, ""hello""));
    }
}

class D
{
    private (int, string) existing;

    public D((int, string) existing)
    {
        this.existing = existing;
    }
}",
parseOptions: TestOptions.Regular,
withScriptOption: true);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateConstructor)]
        public async Task TupleWithNamesAndExistingField()
        {
            await TestAsync(
@"class C
{
    void M()
    {
        new [|D(existing: (a: 1, b: ""hello""))|];
    }
}

class D
{
    private (int a, string b) existing;
}",
@"class C
{
    void M()
    {
        new D(existing: (a: 1, b: ""hello""));
    }
}

class D
{
    private (int a, string b) existing;

    public D((int a, string b) existing)
    {
        this.existing = existing;
    }
}",
parseOptions: TestOptions.Regular,
withScriptOption: true);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateConstructor)]
        public async Task TupleWithDifferentNamesAndExistingField()
        {
            await TestAsync(
@"class C
{
    void M()
    {
        new [|D(existing: (a: 1, b: ""hello""))|];
    }
}

class D
{
    private (int c, string d) existing;
}",
@"class C
{
    void M()
    {
        new D(existing: (a: 1, b: ""hello""));
    }
}

class D
{
    private (int c, string d) existing;

    public D((int a, string b) existing)
    {
        this.existing = existing;
    }
}",
parseOptions: TestOptions.Regular,
withScriptOption: true);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateConstructor)]
        public async Task TupleAndDelegatingConstructor()
        {
            await TestAsync(
@"class C
{
    void M()
    {
        new [|D|]((1, ""hello""));
    }
}

class B
{
    protected B((int, string) x)
    {
    }
}

class D : B
{
}",
@"class C
{
    void M()
    {
        new D((1, ""hello""));
    }
}

class B
{
    protected B((int, string) x)
    {
    }
}

class D : B
{
    public D((int, string) x) : base(x)
    {
    }
}",
parseOptions: TestOptions.Regular,
withScriptOption: true);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateConstructor)]
        public async Task TupleWithNamesAndDelegatingConstructor()
        {
            await TestAsync(
@"class C
{
    void M()
    {
        new [|D|]((a: 1, b: ""hello""));
    }
}

class B
{
    protected B((int a, string b) x)
    {
    }
}

class D : B
{
}",
@"class C
{
    void M()
    {
        new D((a: 1, b: ""hello""));
    }
}

class B
{
    protected B((int a, string b) x)
    {
    }
}

class D : B
{
    public D((int a, string b) x) : base(x)
    {
    }
}",
parseOptions: TestOptions.Regular,
withScriptOption: true);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateConstructor)]
        public async Task TupleWithDifferentNamesAndDelegatingConstructor()
        {
            await TestAsync(
@"class C
{
    void M()
    {
        new [|D|]((a: 1, b: ""hello""));
    }
}

class B
{
    protected B((int c, string d) x)
    {
    }
}

class D : B
{
}",
@"class C
{
    void M()
    {
        new D((a: 1, b: ""hello""));
    }
}

class B
{
    protected B((int c, string d) x)
    {
    }
}

class D : B
{
    public D((int c, string d) x) : base(x)
    {
    }
}",
parseOptions: TestOptions.Regular,
withScriptOption: true);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateConstructor)]
        [WorkItem(11563, "https://github.com/dotnet/roslyn/issues/11563")]
        public async Task StripUnderscoresFromParameterNames()
        {
            await TestAsync(
@"class C
{
    int _i;
    string _s;

    void M()
    {
        new [|D|](_i, _s);
    }
}

class D
{
}",
@"class C
{
    int _i;
    string _s;

    void M()
    {
        new D(_i, _s);
    }
}

class D
{
    private int _i;
    private string _s;

    public D(int i, string s)
    {
        _i = i;
        _s = s;
    }
}");
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateConstructor)]
        [WorkItem(11563, "https://github.com/dotnet/roslyn/issues/11563")]
        public async Task DoNotStripSingleUnderscore()
        {
            await TestAsync(
@"class C
{
    int _;

    void M()
    {
        new [|D|](_);
    }
}

class D
{
}",
@"class C
{
    int _;

    void M()
    {
        new D(_);
    }
}

class D
{
    private int _;

    public D(int _)
    {
        this._ = _;
    }
}");
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateConstructor)]
        [WorkItem(12147, "https://github.com/dotnet/roslyn/issues/12147")]
        public async Task TestOutVariableDeclaration_ImplicitlyTyped()
        {
            await TestAsync(
@"class C
{
    void M()
    {
        new [|C|](out var a);
    }
}",
@"class C
{
    public C(out object a)
    {
        a = null;
    }

    void M()
    {
        new C(out var a);
    }
}",
parseOptions: TestOptions.Regular,
withScriptOption: true);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateConstructor)]
        [WorkItem(12147, "https://github.com/dotnet/roslyn/issues/12147")]
        public async Task TestOutVariableDeclaration_ImplicitlyTyped_NamedArgument()
        {
            await TestAsync(
@"class C
{
    void M()
    {
        new C([|b|]: out var a);
    }
}",
@"class C
{
    public C(out object b)
    {
        b = null;
    }

    void M()
    {
        new C(b: out var a);
    }
}",
parseOptions: TestOptions.Regular,
withScriptOption: true);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateConstructor)]
        [WorkItem(12147, "https://github.com/dotnet/roslyn/issues/12147")]
        public async Task TestOutVariableDeclaration_ExplicitlyTyped()
        {
            await TestAsync(
@"class C
{
    void M()
    {
        new [|C|](out int a);
    }
}",
@"class C
{
    public C(out int a)
    {
        a = 0;
    }

    void M()
    {
        new C(out int a);
    }
}",
parseOptions: TestOptions.Regular,
withScriptOption: true);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateConstructor)]
        [WorkItem(12147, "https://github.com/dotnet/roslyn/issues/12147")]
        public async Task TestOutVariableDeclaration_ExplicitlyTyped_NamedArgument()
        {
            await TestAsync(
@"class C
{
    void M()
    {
        new C([|b|]: out int a);
    }
}",
@"class C
{
    public C(out int b)
    {
        b = 0;
    }

    void M()
    {
        new C(b: out int a);
    }
}",
parseOptions: TestOptions.Regular,
withScriptOption: true);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateConstructor)]
        [WorkItem(12182, "https://github.com/dotnet/roslyn/issues/12182")]
        public async Task TestOutVariableDeclaration_ImplicitlyTyped_CSharp6()
        {
            await TestAsync(
@"class C
{
    void M()
    {
        new [|C|](out var a);
    }
}",
@"class C
{
    public C(out object a)
    {
        a = null;
    }

    void M()
    {
        new C(out var a);
    }
}",
parseOptions: TestOptions.Regular.WithLanguageVersion(CodeAnalysis.CSharp.LanguageVersion.CSharp6),
withScriptOption: true);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateConstructor)]
        [WorkItem(12182, "https://github.com/dotnet/roslyn/issues/12182")]
        public async Task TestOutVariableDeclaration_ImplicitlyTyped_NamedArgument_CSharp6()
        {
            await TestAsync(
@"class C
{
    void M()
    {
        new C([|b|]: out var a);
    }
}",
@"class C
{
    public C(out object b)
    {
        b = null;
    }

    void M()
    {
        new C(b: out var a);
    }
}",
parseOptions: TestOptions.Regular.WithLanguageVersion(CodeAnalysis.CSharp.LanguageVersion.CSharp6),
withScriptOption: true);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateConstructor)]
        [WorkItem(12182, "https://github.com/dotnet/roslyn/issues/12182")]
        public async Task TestOutVariableDeclaration_ExplicitlyTyped_CSharp6()
        {
            await TestAsync(
@"class C
{
    void M()
    {
        new [|C|](out int a);
    }
}",
@"class C
{
    public C(out int a)
    {
        a = 0;
    }

    void M()
    {
        new C(out int a);
    }
}",
parseOptions: TestOptions.Regular.WithLanguageVersion(CodeAnalysis.CSharp.LanguageVersion.CSharp6),
withScriptOption: true);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateConstructor)]
        [WorkItem(12182, "https://github.com/dotnet/roslyn/issues/12182")]
        public async Task TestOutVariableDeclaration_ExplicitlyTyped_NamedArgument_CSharp6()
        {
            await TestAsync(
@"class C
{
    void M()
    {
        new C([|b|]: out int a);
    }
}",
@"class C
{
    public C(out int b)
    {
        b = 0;
    }

    void M()
    {
        new C(b: out int a);
    }
}",
parseOptions: TestOptions.Regular.WithLanguageVersion(CodeAnalysis.CSharp.LanguageVersion.CSharp6),
withScriptOption: true);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateConstructor)]
        [WorkItem(13749, "https://github.com/dotnet/roslyn/issues/13749")]
        public async Task Support_Readonly_Properties()
        {
            await TestAsync(
@"class C {
    public int Prop { get ; }
}

class P { 
    static void M ( ) { 
        var prop = 42 ;
        var c = new [|C|] ( prop ) ;
    }
} ",
@"class C {
    public C ( int prop ) {
        Prop = prop ;
    } 
    public int Prop { get ; }
}

class P {
    static void M ( ) {
        var prop = 42 ;
        var c = new C ( prop ) ;
    }
}",
parseOptions: TestOptions.Regular,
withScriptOption: true);
        }
    }
}
