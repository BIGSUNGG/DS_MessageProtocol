extern alias generator;

using generator::MessageProtocol.CodeGenerator;
using generator::MessageProtocol.CodeGenerator.Generate;
using generator::MessageProtocol.CodeGenerator.Metadata;
using generator::MessageProtocol.CodeGenerator.Reference;
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
        var (diagnostics, generated, _) = RunGeneratorWithCompilation(source);
        return (diagnostics, generated);
    }

    static (ImmutableArray<Diagnostic> Diagnostics, string GeneratedText, ImmutableArray<Diagnostic> CompileErrors) RunGeneratorWithCompilation(string source)
    {
        var compilation = CreateTpaCompilation(source);

        var driver = CSharpGeneratorDriver.Create(new MessageCodeGenerator().AsSourceGenerator());
        driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(compilation, out var updated, out var diagnostics);
        var runResult = driver.GetRunResult();

        var generated = string.Concat(
            runResult.Results.SelectMany(r => r.GeneratedSources).Select(s => s.SourceText.ToString()));
        var compileErrors = updated.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToImmutableArray();
        return (diagnostics, generated, compileErrors);
    }

    /// <summary>테스트 런타임 TPA 참조로 소스 컴파일을 만든다 (생성기 내부 구동 테스트 공용).</summary>
    static CSharpCompilation CreateTpaCompilation(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(System.IO.Path.PathSeparator)
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
            .ToList();
        references.Add(MetadataReference.CreateFromFile(typeof(Serialize.MessageSerializer).Assembly.Location));

        return CSharpCompilation.Create(
            "GeneratorTest",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
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
    public void MSGPROT007_메시지_속성_중복이면_경고하고_생성을_건너뛴다()
    {
        var (diagnostics, generated) = RunGenerator(Header + """
            [NonIdMessage]
            [StandaloneMessage(1)]
            public partial class Confused { public int X { get; set; } }
            """ + Footer);

        Assert.Contains(diagnostics, d => d.Id == "MSGPROT007" && d.Severity == DiagnosticSeverity.Warning);
        Assert.DoesNotContain("RegisterHasIdMessage<Confused>", generated);
        Assert.DoesNotContain("RegisterNonIdMessage<Confused>", generated);
    }

    [Fact]
    public void MSGPROT007_스탠드얼론과_그룹루트_중복도_경고한다()
    {
        var (diagnostics, generated) = RunGenerator(Header + """
            [StandaloneMessage(1)]
            [GroupRootMessage(2)]
            public partial class DoubleId { public int X { get; set; } }
            """ + Footer);

        Assert.Contains(diagnostics, d => d.Id == "MSGPROT007");
        Assert.DoesNotContain("RegisterHasIdMessage<DoubleId>", generated);
    }

    [Fact]
    public void 제네릭_메시지_타입은_매개변수를_유지한_채_컴파일_가능한_코드를_생성한다()
    {
        var (diagnostics, generated, compileErrors) = RunGeneratorWithCompilation(Header + """
            [StandaloneMessage(1)]
            public partial class Msg<T> { public T? Value { get; set; } }
            """ + Footer);

        Assert.DoesNotContain(diagnostics, d => d.Id.StartsWith("MSGPROT"));
        Assert.Empty(compileErrors);
        Assert.Contains("public partial class Msg<T>", generated);
        Assert.Contains("IHasIdMessageSerializable<Msg<T>>", generated);
        Assert.Contains("Serialize(Msg<T> message", generated);
    }

    [Fact]
    public void 제네릭_메시지_타입은_자동_등록_코드를_생성하지_않는다()
    {
        var (diagnostics, generated, compileErrors) = RunGeneratorWithCompilation(Header + """
            [NonIdMessage]
            public partial class Pair<T> { public T? First { get; set; } public int Tag { get; set; } }
            """ + Footer);

        Assert.DoesNotContain(diagnostics, d => d.Id.StartsWith("MSGPROT"));
        Assert.Empty(compileErrors);
        Assert.DoesNotContain("RegisterNonIdMessage", generated);
        Assert.DoesNotContain("[ModuleInitializer]", generated);
    }

    [Fact]
    public void GenericMessage_구성_선언은_자동_등록_클래스를_생성한다()
    {
        var (diagnostics, generated, compileErrors) = RunGeneratorWithCompilation(Header + """
            [StandaloneMessage(1)]
            public partial class Target { public int X { get; set; } }

            [StandaloneMessage(2)]
            [GenericMessage(typeof(Box<Target>), ClassId = 7)]
            public partial class Box<T> { public T? Value { get; set; } }
            """ + Footer);

        Assert.DoesNotContain(diagnostics, d => d.Id.StartsWith("MSGPROT"));
        Assert.Empty(compileErrors);
        Assert.Contains("RegisterGenericConstruction<global::TestNs.Box<global::TestNs.Target>>(7)", generated);
        // 클래스 ID는 런타임 레지스트리 조회 (내부 필드 미사용).
        Assert.Contains("MessageSerializer.GetGenericClassId<Box<T>>()", generated);
        Assert.DoesNotContain("__GenericClassId", generated);
        // 제네릭 헤더 플래그 0: MessageId 구성에 제네릭 플래그가 쓰인다.
        Assert.Contains("MessageId => 2;", generated);
    }

    [Fact]
    public void MSGPROT008_제네릭이_아닌_타입에_GenericMessage를_붙이면_에러()
    {
        var (diagnostics, _) = RunGenerator(Header + """
            [StandaloneMessage(1)]
            [GenericMessage(typeof(int), ClassId = 1)]
            public partial class NotGeneric { public int X { get; set; } }
            """ + Footer);

        Assert.Contains(diagnostics, d => d.Id == "MSGPROT008");
    }

    [Fact]
    public void MSGPROT008_미바운드_제네릭_구성_선언은_에러()
    {
        var (diagnostics, _) = RunGenerator(Header + """
            [StandaloneMessage(2)]
            public partial class Box<T> { public T? Value { get; set; } }

            [GenericMessage(typeof(Box<>), ClassId = 1)]
            static class Carrier { }
            """ + Footer);

        Assert.Contains(diagnostics, d => d.Id == "MSGPROT008");
    }

    [Fact]
    public void MSGPROT008_제네릭_선언에_StandaloneMessage가_없으면_에러()
    {
        var (diagnostics, _) = RunGenerator(Header + """
            [NonIdMessage]
            public partial class NoId<T> { public T? V { get; set; } }

            [GenericMessage(typeof(NoId<int>), ClassId = 1)]
            static class Carrier { }
            """ + Footer);

        Assert.Contains(diagnostics, d => d.Id == "MSGPROT008");
    }

    [Fact]
    public void 분산_선언_캐리어는_구성_등록_코드를_생성한다()
    {
        var (diagnostics, generated, compileErrors) = RunGeneratorWithCompilation(Header + """
            [StandaloneMessage(1)]
            public partial class Target { public int X { get; set; } }

            [StandaloneMessage(2)]
            public partial class Box<T> { public T? Value { get; set; } }

            [GenericMessage(typeof(Box<Target>), ClassId = 5)]
            static class Carrier { }
            """ + Footer);

        Assert.DoesNotContain(diagnostics, d => d.Id.StartsWith("MSGPROT"));
        Assert.Empty(compileErrors);
        Assert.Contains("RegisterGenericConstruction<global::TestNs.Box<global::TestNs.Target>>(5)", generated);
    }

    [Fact]
    public void MSGPROT008_한_컴파일에서_같은_구성을_두_번_선언하면_에러()
    {
        var (diagnostics, generated) = RunGenerator(Header + """
            [StandaloneMessage(1)]
            public partial class Target { public int X { get; set; } }

            [StandaloneMessage(2)]
            public partial class Box<T> { public T? Value { get; set; } }

            [GenericMessage(typeof(Box<Target>), ClassId = 1)]
            static class CarrierA { }

            [GenericMessage(typeof(Box<Target>), ClassId = 1)]
            static class CarrierB { }
            """ + Footer);

        Assert.Contains(diagnostics, d => d.Id == "MSGPROT008");
        Assert.DoesNotContain("RegisterGenericConstruction<global::TestNs.Box<global::TestNs.Target>>(1)", generated);
    }

    [Fact]
    public void MSGPROT008_분산_선언_구성이_제네릭_메시지가_아니면_에러()
    {
        var (diagnostics, _) = RunGenerator(Header + """
            [GenericMessage(typeof(int), ClassId = 1)]
            static class Carrier { }
            """ + Footer);

        Assert.Contains(diagnostics, d => d.Id == "MSGPROT008");
    }

    [Fact]
    public void MSGPROT008_분산_선언에_ClassId가_없으면_에러()
    {
        var (diagnostics, _) = RunGenerator(Header + """
            [StandaloneMessage(1)]
            public partial class Target { public int X { get; set; } }

            [StandaloneMessage(2)]
            public partial class Box<T> { public T? Value { get; set; } }

            [GenericMessage(typeof(Box<Target>))]
            static class Carrier { }
            """ + Footer);

        Assert.Contains(diagnostics, d => d.Id == "MSGPROT008");
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

    [Fact]
    public void CollectionsMarshal_미지원_타깃의_List_벌크_판독도_개수_곱하기_요소크기를_검증한다()
    {
        // KI-17 회귀: CollectionsMarshal 이 없는 타깃(예: netstandard2.0 소비자)의 요소별 판독 경로도
        // List 사전 할당 전에 개수×요소크기 ≤ 남은 바이트 를 검증해야 한다 (개수만 검증하면 8배 선할당 강요).
        var compilation = CreateTpaCompilation(Header + """
            [StandaloneMessage(1)]
            public partial class BulkFallbackMessage
            {
                public System.Collections.Generic.List<long>? Values { get; set; }
            }
            """ + Footer);

        var rootType = compilation.GetTypeByMetadataName("TestNs.BulkFallbackMessage")!;
        var attributeReferences = new AttributeReferences(compilation);
        var typeMeta = new TypeMetadata(rootType, attributeReferences);

        bool emitted = MessageSerializeCodeEmitter.TryEmit(
            typeMeta, attributeReferences, hasCollectionsMarshal: false, out var code, out _);

        Assert.True(emitted);
        Assert.NotNull(code);
        Assert.Contains("* 8 > reader.Remaining", code);
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
