using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
using Benchmarks;
using MessageProtocol;
using MessageProtocol.Serialize;

BenchmarkRunner.Run<SerializationBenchmarks>();

namespace Benchmarks
{
    /// <summary>
    /// 저장소에 동명 프로젝트가 두 개 있어(Legacy 포함) 기본 csproj 도구 체인이 실패하므로
    /// in-process emit 도구 체인을 사용한다.
    /// </summary>
    public class InProcessConfig : ManualConfig
    {
        public InProcessConfig()
        {
            AddJob(Job.Default.WithToolchain(InProcessEmitToolchain.Instance));
        }
    }

    [Message(MessageKind.Standalone, 1)]
    public partial class BenchMessage
    {
        public int Id { get; set; }
        public long Timestamp { get; set; }
        public float Value { get; set; }
        public string? Text { get; set; }
        public List<int>? Numbers { get; set; }
    }

    /// <summary>
    /// 문자열 많은 메시지(게임 서버 실제 프로파일: 이름·채팅·ASCII 키). WriteString 용량 산정이
    /// 3n+3 보수 예약에서 ASCII 정확 산정으로 바뀐 것의 효과를 재는 시나리오다.
    /// 100자 문자열 4개 — 보수 예약은 4·(4+303)=1,228B, ASCII 실제는 416B.
    /// </summary>
    [Message(MessageKind.Standalone, 2)]
    public partial class StringHeavyMessage
    {
        public string? Name { get; set; }
        public string? Channel { get; set; }
        public string? Text { get; set; }
        public string? Tag { get; set; }
    }

    /// <summary>
    /// 참조 추적 그래프 시나리오 — 같은 서브그래프를 두 갈래로 공유해 쓰기·읽기 양쪽에서 백레퍼런스를
    /// 강제한다(깊이 5·공유 노드 서브트리). 풀링 경로와 함께 기준선 갭이었던 두 경로를 잰다.
    /// </summary>
    [Message(MessageKind.Standalone, 3)]
    public partial class GraphNode
    {
        public string? Label { get; set; }
        public GraphNode? Left { get; set; }
        public GraphNode? Right { get; set; }
    }

    /// <summary>
    /// 대형 컬렉션 시나리오(인벤토리·엔티티 일괄 전송 실제 스케일) — `List&lt;int&gt;` 10만 요소는
    /// CollectionsMarshal 벌크 복사 경로, `string[]` 1천 요소는 요소별 쓰기 경로를 각각 압박한다.
    /// </summary>
    [Message(MessageKind.Standalone, 4)]
    public partial class LargeCollections
    {
        public List<int>? Numbers { get; set; }
        public string[]? Names { get; set; }
    }

    [MemoryDiagnoser]
    [Config(typeof(InProcessConfig))]
    public class SerializationBenchmarks
    {
        readonly BenchMessage _message = new()
        {
            Id = 42,
            Timestamp = 1717000000L,
            Value = 3.14f,
            Text = "benchmark payload",
            Numbers = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8 },
        };

        byte[] _bytes = null!;
        byte[] _stringBytes = null!;
        byte[] _graphBytes = null!;
        byte[] _largeBytes = null!;

        readonly StringHeavyMessage _stringMessage = new()
        {
            Name = new string('a', 100),
            Channel = new string('b', 100),
            Text = new string('c', 100),
            Tag = new string('d', 100),
        };

        // 깊이 5 체인의 끝을 두 갈래가 공유 — 쓰기는 백레퍼런스 태그를, 읽기는 GetObject 역참조를 강제한다.
        static GraphNode BuildSharedGraph()
        {
            GraphNode tail = new() { Label = "leaf" };
            for (int i = 0; i < 4; i++)
            {
                tail = new GraphNode { Label = "n" + i, Left = tail, Right = null };
            }
            return new GraphNode { Label = "root", Left = tail, Right = tail };   // 같은 서브그래프 공유
        }

        readonly GraphNode _graphRoot = BuildSharedGraph();

        readonly LargeCollections _largeCollections = new()
        {
            Numbers = Enumerable.Range(0, 100_000).Select(i => i * 7).ToList(),
            Names = Enumerable.Range(0, 1_000).Select(i => "name" + i).ToArray(),
        };

        [GlobalSetup]
        public void Setup()
        {
            _bytes = MessageSerializer.Serialize(_message);
            _stringBytes = MessageSerializer.Serialize(_stringMessage);
            _graphBytes = MessageSerializer.Serialize(_graphRoot);
            _largeBytes = MessageSerializer.Serialize(_largeCollections);
        }

        [Benchmark]
        public byte[] SerializeBytes() => MessageSerializer.Serialize(_message);

        [Benchmark]
        public byte[] SerializeStringHeavy() => MessageSerializer.Serialize(_stringMessage);

        [Benchmark]
        public object DeserializeStringHeavy() => MessageSerializer.Deserialize(_stringBytes);

        [Benchmark]
        public int SerializePooledFlat()   // byte[] 경로(SerializeBytes)와의 할당 대조 — 반환은 소유권 해제 포함
        {
            using var pooled = MessageSerializer.SerializePooled(_message);
            return pooled.Length;
        }

        [Benchmark]
        public byte[] SerializeSharedGraph() => MessageSerializer.Serialize(_graphRoot);

        [Benchmark]
        public GraphNode DeserializeSharedGraph() => MessageSerializer.Deserialize<GraphNode>(_graphBytes);

        [Benchmark]
        public byte[] SerializeLargeCollections() => MessageSerializer.Serialize(_largeCollections);

        [Benchmark]
        public LargeCollections DeserializeLargeCollections() => MessageSerializer.Deserialize<LargeCollections>(_largeBytes);

        [Benchmark]
        public BenchMessage DeserializeTyped() => MessageSerializer.Deserialize<BenchMessage>(_bytes);

        [Benchmark]
        public object DeserializeDispatch() => MessageSerializer.Deserialize(_bytes);
    }
}
