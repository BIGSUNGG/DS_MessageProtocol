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

    [Fact]
    public void MSGPROT010_추상_메시지_타입은_생성_거부()
    {
        var (diagnostics, generated) = RunGenerator(Header + """
            [NonIdMessage]
            public abstract partial class AbstractMessage { public int X { get; set; } }
            """ + Footer);

        Assert.Contains(diagnostics, d => d.Id == "MSGPROT010");
        Assert.DoesNotContain("__WritePayload", generated);
    }

    [Fact]
    public void MSGPROT010_포지셔널_레코드_메시지는_생성_거부()
    {
        var (diagnostics, generated) = RunGenerator(Header + """
            [NonIdMessage]
            public partial record PointRecord(int X);
            """ + Footer);

        Assert.Contains(diagnostics, d => d.Id == "MSGPROT010");
        Assert.DoesNotContain("__WritePayload", generated);
    }

    [Fact]
    public void MSGPROT011_읽기_전용_멤버는_생성_거부()
    {
        var (diagnostics, generated) = RunGenerator(Header + """
            [NonIdMessage]
            public partial class GetOnlyMessage { public int X { get; } }
            """ + Footer);

        Assert.Contains(diagnostics, d => d.Id == "MSGPROT011");
        Assert.DoesNotContain("__WritePayload", generated);
    }

    [Fact]
    public void MSGPROT006_생성_불가_페이로드_멤버는_미지원_타입_진단()
    {
        // 추상 클래스·포지셔널 레코드 페이로드는 기본 생성자로 인스턴스를 만들 수 없어 멤버 단위 진단으로 거부한다.
        var (diagnostics, _) = RunGenerator(Header + """
            public abstract class AbstractPayload { public int X { get; set; } }
            public partial record PositionalPayload(int X);

            [StandaloneMessage(1)]
            public partial class Host
            {
                public AbstractPayload? A { get; set; }
                public PositionalPayload? B { get; set; }
            }
            """ + Footer);

        Assert.Equal(2, diagnostics.Count(d => d.Id == "MSGPROT006"));
    }

    [Fact]
    public void MSGPROT011_페이로드의_읽기_전용_멤버는_생성_거부()
    {
        var (diagnostics, _) = RunGenerator(Header + """
            public partial class GetOnlyPayload { public int X { get; } }

            [StandaloneMessage(1)]
            public partial class Host
            {
                public GetOnlyPayload? Payload { get; set; }
            }
            """ + Footer);

        Assert.Contains(diagnostics, d => d.Id == "MSGPROT011");
    }

    [Fact]
    public void 동명_중첩_구성_캐리어도_유일한_등록_클래스를_생성한다()
    {
        // KI-19 회귀: 같은 네임스페이스의 동명 중첩 캐리어 두 개가 충돌 없이 각각 유일한 등록 클래스를 방출한다.
        var (diagnostics, generated, compileErrors) = RunGeneratorWithCompilation(Header + """
            [StandaloneMessage(1)]
            [GenericMessage(typeof(Envelope<int>), ClassId = 1)]
            public partial class Envelope<T>
            {
                public T? Value { get; set; }
            }

            static class OuterA
            {
                [GenericMessage(typeof(Envelope<string>), ClassId = 2)]
                internal static class Carrier { }
            }

            static class OuterB
            {
                [GenericMessage(typeof(Envelope<long>), ClassId = 3)]
                internal static class Carrier { }
            }
            """ + Footer);

        Assert.DoesNotContain(diagnostics, d => d.Id.StartsWith("MSGPROT"));
        Assert.Empty(compileErrors);
        Assert.Contains("__GenericConstructionRegistration_TestNs_OuterA_Carrier", generated);
        Assert.Contains("__GenericConstructionRegistration_TestNs_OuterB_Carrier", generated);
        Assert.Contains("RegisterGenericConstruction<global::TestNs.Envelope<string>>(2)", generated);
        Assert.Contains("RegisterGenericConstruction<global::TestNs.Envelope<long>>(3)", generated);
    }

    [Fact]
    public void 공개_인덱서는_직렬화_멤버에서_제외된다()
    {
        // KI-23 회귀: 인덱서도 `IPropertySymbol`(Name = "this[]")이라 멤버로 뽑히면 `message.this[]` 같은
        // 문법 오류 코드가 진단 없이 생성되어 소비자 빌드가 깨진다.
        var (diagnostics, generated, compileErrors) = RunGeneratorWithCompilation(Header + """
            [StandaloneMessage(1)]
            public partial class WithIndexer
            {
                public int Value { get; set; }
                public int this[int index] { get => Value; set => Value = value; }
            }
            """ + Footer);

        Assert.DoesNotContain(diagnostics, d => d.Id.StartsWith("MSGPROT"));
        Assert.DoesNotContain("this[", generated);
        Assert.Contains("Value", generated);
        Assert.Empty(compileErrors);
    }

    [Fact]
    public void 추상_메시지_타입_멤버는_위임_대신_런타임_디스패치를_생성한다()
    {
        // KI-24 회귀: abstract [GroupRootMessage] 는 다형 그룹의 자연스러운 선언이지만 생성기는 인스턴스를
        // 만들 수 없어 정적 Serialize/Deserialize 를 방출하지 않는다 — 위임 코드는 진단 없이 소비자 빌드를
        // CS0117('AbstractEvent'에 'Serialize' 정의가 없음)로 깨뜨렸다.
        var (diagnostics, generated, compileErrors) = RunGeneratorWithCompilation(Header + """
            [GroupRootMessage(300)]
            public abstract partial class AbstractEvent { public long Timestamp { get; set; } }

            [GroupElementMessage(301)]
            public partial class LoginEvent : AbstractEvent { public string? User { get; set; } }

            [StandaloneMessage(302)]
            public partial class Envelope
            {
                public AbstractEvent? Payload { get; set; }
                public System.Collections.Generic.List<AbstractEvent>? History { get; set; }
            }
            """ + Footer);

        Assert.DoesNotContain(diagnostics, d => d.Id.StartsWith("MSGPROT"));
        Assert.DoesNotContain("AbstractEvent.Serialize", generated);
        Assert.DoesNotContain("AbstractEvent.Deserialize", generated);
        Assert.Contains("MessageSerializer.SerializeToWriter(message.Payload, ref writer)", generated);
        Assert.Contains("(global::TestNs.AbstractEvent)MessageSerializer.DeserializeFromReader(ref reader)", generated);
        Assert.Empty(compileErrors);
    }

    [Fact]
    public void 그래프_밖_구체_메시지_멤버는_정적_위임을_유지한다()
    {
        // KI-24 수정의 역방향 가드: 비공개 매개변수 없는 생성자 때문에 그래프에서 빠진 *구체* 메시지 타입은
        // 여전히 생성 정적 멤버를 가지므로 정적 위임이 맞다 (런타임 디스패치로 과도하게 돌리면 안 된다).
        var (diagnostics, generated, compileErrors) = RunGeneratorWithCompilation(Header + """
            [StandaloneMessage(310)]
            public partial class PrivateCtorHost { public PrivateCtorPayload? Payload { get; set; } }

            [NonIdMessage]
            public partial class PrivateCtorPayload
            {
                PrivateCtorPayload() { }
                public int X { get; set; }
            }
            """ + Footer);

        Assert.DoesNotContain(diagnostics, d => d.Id.StartsWith("MSGPROT"));
        Assert.Contains("global::TestNs.PrivateCtorPayload.Serialize(message.Payload, ref writer)", generated);
        Assert.Contains("global::TestNs.PrivateCtorPayload.Deserialize(ref reader)", generated);
        Assert.DoesNotContain("SerializeToWriter", generated);
        Assert.Empty(compileErrors);
    }

    [Fact]
    public void 와이어_멤버_순서는_베이스_선언순서와_그림자_제거_위치를_고정한다()
    {
        // KI-4 회귀: 페이로드 바이트 순서는 송수신이 반드시 일치해야 하는 와이어 형식인데, 이전에는
        // `Dictionary.Values` 열거 순서(삽입 순서일 뿐 규약이 아닌 BCL 구현 세부)에 얹혀 있었다.
        // 이제 `TypeMetadata.GetWireMembers` 가 명시적으로 고정하며, 이 테스트가 그 레이아웃을 못박는다.
        var compilation = CreateTpaCompilation(Header + """
            [GroupRootMessage(400)]
            public partial class OrderBase
            {
                public int BaseFirst { get; set; }
                public string? Shadowed { get; set; }
                public long BaseLast { get; set; }
            }

            [GroupElementMessage(401)]
            public partial class OrderDerived : OrderBase
            {
                public new long Shadowed { get; set; }
                public byte DerivedOwn { get; set; }
            }
            """ + Footer);

        var rootType = compilation.GetTypeByMetadataName("TestNs.OrderDerived")!;
        var attributeReferences = new AttributeReferences(compilation);
        var typeMeta = new TypeMetadata(rootType, attributeReferences);

        bool emitted = MessageSerializeCodeEmitter.TryEmit(
            typeMeta, attributeReferences, hasCollectionsMarshal: true, out var code, out _);

        Assert.True(emitted);
        Assert.NotNull(code);
        // 베이스 선언 순서 먼저, 파생 고유 멤버 나중.
        Assert.Equal(
            new[] { "BaseFirst", "Shadowed", "BaseLast", "DerivedOwn" },
            ExtractWriteOrder(code!));
        // 그림자 제거된 멤버는 **베이스 위치**를 유지한 채 파생 타입(long)으로 기록된다.
        Assert.Contains("writer.WriteInt64(message.Shadowed)", code);
        Assert.DoesNotContain("writer.WriteString(message.Shadowed)", code);
    }

    [Fact]
    public void 중첩_페이로드_헬퍼의_멤버_순서도_같은_규칙을_쓴다()
    {
        // 이미터(루트 페이로드)와 그래프(중첩 페이로드 헬퍼)가 예전에는 동일한 병합 로직을 각각 갖고 있었다.
        // 한 구현(`TypeMetadata.GetWireMembers`)을 공유하므로 중첩 헬퍼의 바이트 순서도 같은 규칙임을 고정한다.
        var compilation = CreateTpaCompilation(Header + """
            public class NestedOrderBase
            {
                public int NestedBaseFirst { get; set; }
                public string? NestedShadow { get; set; }
            }

            public class NestedOrderDerived : NestedOrderBase
            {
                public new long NestedShadow { get; set; }
                public byte NestedOwn { get; set; }
            }

            [StandaloneMessage(402)]
            public partial class NestedOrderHost
            {
                public int HostOwn { get; set; }
                public NestedOrderDerived? Payload { get; set; }
            }
            """ + Footer);

        var rootType = compilation.GetTypeByMetadataName("TestNs.NestedOrderHost")!;
        var attributeReferences = new AttributeReferences(compilation);
        var typeMeta = new TypeMetadata(rootType, attributeReferences);

        bool emitted = MessageSerializeCodeEmitter.TryEmit(
            typeMeta, attributeReferences, hasCollectionsMarshal: true, out var code, out _);

        Assert.True(emitted);
        Assert.NotNull(code);

        // 중첩 페이로드 기록 헬퍼(시그니처가 `NestedOrderDerived message`) 이후의 기록 순서만 잘라 검증한다.
        int helperStart = code!.IndexOf("NestedOrderDerived message", StringComparison.Ordinal);
        Assert.True(helperStart >= 0, "nested payload write helper not found");

        Assert.Equal(
            new[] { "NestedBaseFirst", "NestedShadow", "NestedOwn" },
            ExtractWriteOrder(code[helperStart..]));
        Assert.Contains("writer.WriteInt64(message.NestedShadow)", code);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void 컬렉션_쓰기는_멤버를_로컬로_스냅샷한다(bool hasCollectionsMarshal)
    {
        // KI-26 회귀: 길이 접두·루프 조건·요소 접근이 각자 `message.Values` 를 다시 평가하면 게터가 2N+2회 돌고,
        // 계산형 프로퍼티에서는 길이와 요소가 서로 다른 인스턴스에서 나와 프레임이 스스로 모순된다.
        // hasCollectionsMarshal=false 는 Unity/netstandard2.1 소비자 경로라 이 저장소에서는 실행되지 않음 → 생성 텍스트로 고정.
        var compilation = CreateTpaCompilation(Header + """
            [StandaloneMessage(1)]
            public partial class SnapshotMessage
            {
                public System.Collections.Generic.List<int>? Values { get; set; }
                public System.Collections.Generic.List<string>? Names { get; set; }
                public string[]? Tags { get; set; }
            }
            """ + Footer);

        var rootType = compilation.GetTypeByMetadataName("TestNs.SnapshotMessage")!;
        var attributeReferences = new AttributeReferences(compilation);
        var typeMeta = new TypeMetadata(rootType, attributeReferences);

        bool emitted = MessageSerializeCodeEmitter.TryEmit(
            typeMeta, attributeReferences, hasCollectionsMarshal, out var code, out _);

        Assert.True(emitted);
        Assert.NotNull(code);

        // 멤버 표현식은 스냅샷 한 곳에서만 등장한다 — null 판정도 스냅샷 로컬로 하므로
        // 계산형 프로퍼티가 두 번째 평가에서 null 을 돌려줘도 NRE 가 나지 않는다(TOCTOU 차단).
        Assert.Equal(1, CountOccurrences(code!, "message.Values"));
        Assert.Equal(1, CountOccurrences(code!, "message.Names"));
        Assert.Equal(1, CountOccurrences(code!, "message.Tags"));
        Assert.DoesNotContain("if (message.Values is null)", code);

        Assert.Contains(hasCollectionsMarshal ? "var __list" : "var __coll", code);
        Assert.Contains("var __arr", code);      // 배열은 양쪽 구성 모두 스냅샷
    }

    /// <summary>생성 코드에서 `writer.Write*(message.멤버)` 호출의 멤버 이름을 나온 순서대로뽑는다 = 와이어 기록 순서.</summary>
    static IReadOnlyList<string> ExtractWriteOrder(string generated)
    {
        return System.Text.RegularExpressions.Regex
            .Matches(generated, @"writer\.Write\w+\(message\.(\w+)")
            .Select(static match => match.Groups[1].Value)
            .ToList();
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
