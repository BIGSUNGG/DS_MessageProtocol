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

        [GlobalSetup]
        public void Setup() => _bytes = MessageSerializer.Serialize(_message);

        [Benchmark]
        public byte[] SerializeBytes() => MessageSerializer.Serialize(_message);

        [Benchmark]
        public BenchMessage DeserializeTyped() => MessageSerializer.Deserialize<BenchMessage>(_bytes);

        [Benchmark]
        public object DeserializeDispatch() => MessageSerializer.Deserialize(_bytes);
    }
}
