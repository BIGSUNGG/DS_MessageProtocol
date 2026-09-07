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

    [StandaloneMessage(1)]
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
    [StandaloneMessage(2)]
    public partial class StringHeavyMessage
    {
        public string? Name { get; set; }
        public string? Channel { get; set; }
        public string? Text { get; set; }
        public string? Tag { get; set; }
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

        readonly StringHeavyMessage _stringMessage = new()
        {
            Name = new string('a', 100),
            Channel = new string('b', 100),
            Text = new string('c', 100),
            Tag = new string('d', 100),
        };

        [GlobalSetup]
        public void Setup()
        {
            _bytes = MessageSerializer.Serialize(_message);
            _stringBytes = MessageSerializer.Serialize(_stringMessage);
        }

        [Benchmark]
        public byte[] SerializeBytes() => MessageSerializer.Serialize(_message);

        [Benchmark]
        public byte[] SerializeStringHeavy() => MessageSerializer.Serialize(_stringMessage);

        [Benchmark]
        public object DeserializeStringHeavy() => MessageSerializer.Deserialize(_stringBytes);

        [Benchmark]
        public BenchMessage DeserializeTyped() => MessageSerializer.Deserialize<BenchMessage>(_bytes);

        [Benchmark]
        public object DeserializeDispatch() => MessageSerializer.Deserialize(_bytes);
    }
}
