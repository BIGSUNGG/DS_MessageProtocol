using MessageProtocol.CodeGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Immutable;
using Xunit;

namespace MessageProtocol.Tests;

/// <summary>생성기 진단(MSGPROT001–006)과 정상 생성을 GeneratorDriver 로 검증한다.</summary>
public class GeneratorDiagnosticTests
{
    static (ImmutableArray<Diagnostic> Diagnostics, string GeneratedText) RunGenerator(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(System.IO.Path.PathSeparator)
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
            .ToList();
        references.Add(MetadataReference.CreateFromFile(typeof(Serialize.MessageSerializer).Assembly.Location));

        var compilation = CSharpCompilation.Create(
            "GeneratorTest",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = CSharpGeneratorDriver.Create(new MessageCodeGenerator().AsSourceGenerator());
        driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);
        var runResult = driver.GetRunResult();

        var generated = string.Concat(
            runResult.Results.SelectMany(r => r.GeneratedSources).Select(s => s.SourceText.ToString()));
        return (diagnostics, generated);
    }

    const string Header = """
        using MessageProtocol;
        using MessageProtocol.Serialize;
        namespace TestNs
        {
        """;

    const string Footer = """
        }
        """;

    [Fact]
    public void MSGPROT001_partial_아닌_메시지()
    {
        var (diagnostics, _) = RunGenerator(Header + """
            [StandaloneMessage(1)]
            public class NotPartial { public int X { get; set; } }
            """ + Footer);

        Assert.Contains(diagnostics, d => d.Id == "MSGPROT001");
    }

    [Fact]
    public void MSGPROT002_컨테이닝_타입이_partial_아님()
    {
        var (diagnostics, _) = RunGenerator(Header + """
            public class Outer
            {
                [StandaloneMessage(1)]
                public partial class Inner { public int X { get; set; } }
            }
            """ + Footer);

        Assert.Contains(diagnostics, d => d.Id == "MSGPROT002");
    }

    [Fact]
    public void MSGPROT003_요소_메시지에_루트가_없음()
    {
        var (diagnostics, _) = RunGenerator(Header + """
            [GroupElementMessage(1)]
            public partial class OrphanElement { public int X { get; set; } }
            """ + Footer);

        Assert.Contains(diagnostics, d => d.Id == "MSGPROT003");
    }

    [Fact]
    public void MSGPROT004_루트의_부모가_루트()
    {
        var (diagnostics, _) = RunGenerator(Header + """
            [GroupRootMessage(1)]
            public partial class RootA { }

            [GroupRootMessage(2)]
            public partial class RootB : RootA { }
            """ + Footer);

        Assert.Contains(diagnostics, d => d.Id == "MSGPROT004");
    }

    [Fact]
    public void MSGPROT005_ID_범위_초과()
    {
        var (diagnostics, _) = RunGenerator(Header + """
            [StandaloneMessage(16777216)]
            public partial class TooBig { public int X { get; set; } }
            """ + Footer);

        Assert.Contains(diagnostics, d => d.Id == "MSGPROT005");
    }

    [Fact]
    public void MSGPROT006_미지원_멤버_타입()
    {
        var (diagnostics, _) = RunGenerator(Header + """
            [StandaloneMessage(1)]
            public partial class BadMember
            {
                public System.Collections.Generic.Dictionary<string, int>? Map { get; set; }
            }
            """ + Footer);

        Assert.Contains(diagnostics, d => d.Id == "MSGPROT006");
    }

    [Fact]
    public void 정상_타입은_등록_코드를_생성한다()
    {
        var (diagnostics, generated) = RunGenerator(Header + """
            [StandaloneMessage(7)]
            [MessageCategory(MessageCategory.Category2)]
            public partial class Good
            {
                public int X { get; set; }
                public string? Y { get; set; }
            }
            """ + Footer);

        Assert.DoesNotContain(diagnostics, d => d.Id.StartsWith("MSGPROT"));
        Assert.Contains("RegisterHasIdMessage<Good>", generated);
        Assert.Contains("[ModuleInitializer]", generated);
        Assert.Contains("public static uint MessageId => ", generated);
    }

    [Fact]
    public void NonId_타입은_NonId_등록을_생성한다()
    {
        var (diagnostics, generated) = RunGenerator(Header + """
            [NonIdMessage]
            public partial class NoId { public byte B { get; set; } }
            """ + Footer);

        Assert.DoesNotContain(diagnostics, d => d.Id.StartsWith("MSGPROT"));
        Assert.Contains("RegisterNonIdMessage<NoId>", generated);
    }

    [Fact]
    public void abstract_그룹_루트는_생성을_건너뛴다()
    {
        var (diagnostics, generated) = RunGenerator(Header + """
            [GroupRootMessage(1)]
            public abstract partial class AbstractRoot { public int X { get; set; } }

            [GroupElementMessage(2)]
            public partial class Concrete : AbstractRoot { public int Y { get; set; } }
            """ + Footer);

        Assert.DoesNotContain(diagnostics, d => d.Id.StartsWith("MSGPROT"));
        Assert.DoesNotContain("__WritePayload_TestNs_AbstractRoot", generated);
        Assert.Contains("RegisterHasIdMessage<Concrete>", generated);
    }

    [Fact]
    public void 그룹_계층_요소는_루트_멤버를_포함한다()
    {
        var (diagnostics, generated) = RunGenerator(Header + """
            [GroupRootMessage(1)]
            public partial class R { public int BaseField { get; set; } }

            [GroupElementMessage(2)]
            public partial class E : R { public int ChildField { get; set; } }
            """ + Footer);

        Assert.DoesNotContain(diagnostics, d => d.Id.StartsWith("MSGPROT"));
        Assert.Contains("BaseField", generated);
        Assert.Contains("ChildField", generated);
    }

    [Fact]
    public void 다른_네임스페이스의_동명_타입도_충돌없이_모두_생성된다()
    {
        // 힌트 이름이 단순 타입 이름만 쓰면 AddSource 가 ArgumentException 을 던져 전체 생성이 유실된다.
        var (diagnostics, generated) = RunGenerator("""
            using MessageProtocol;
            namespace NsA
            {
                [StandaloneMessage(1)]
                public partial class Same { public int X { get; set; } }
            }
            namespace NsB
            {
                [StandaloneMessage(2)]
                public partial class Same { public int Y { get; set; } }
            }
            """);

        Assert.DoesNotContain(diagnostics, d => d.Id.StartsWith("MSGPROT"));
        Assert.Contains("namespace NsA", generated);
        Assert.Contains("namespace NsB", generated);
        Assert.Equal(2, CountOccurrences(generated, "RegisterHasIdMessage<Same>"));
    }

    [Fact]
    public void 중첩_타입과_네임스페이스_점이_동일한_모양이어도_충돌하지_않는다()
    {
        // 네임스페이스 A.B의 클래스 C → 'A.B.C', 네임스페이스 A의 중첩 B.C → 'A.B+C'.
        // 중첩 구분자가 '.' 이면 두 힌트 이름이 충돌해 전체 생성이 유실된다.
        var (diagnostics, generated) = RunGenerator("""
            using MessageProtocol;
            namespace A.B
            {
                [StandaloneMessage(1)]
                public partial class C { public int X { get; set; } }
            }
            namespace A
            {
                public partial class B
                {
                    [StandaloneMessage(2)]
                    public partial class C { public int Y { get; set; } }
                }
            }
            """);

        Assert.DoesNotContain(diagnostics, d => d.Id.StartsWith("MSGPROT"));
        Assert.Contains("namespace A.B", generated);
        Assert.Contains("namespace A", generated);
        Assert.Equal(2, CountOccurrences(generated, "RegisterHasIdMessage<C>"));
    }

    static int CountOccurrences(string text, string pattern)
    {
        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(pattern, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += pattern.Length;
        }
        return count;
    }
}
