using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Benchmarks;
using MessageProtocol;
using MessageProtocol.Serialize;

BenchmarkRunner.Run<SerializationBenchmarks>();

namespace Benchmarks
{
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
