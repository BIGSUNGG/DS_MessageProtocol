using BenchmarkDotNet.Attributes;
using MessageProtocol;
using MessageProtocol.Serialize;

namespace MessageProtocol.Benchmarks;

[MemoryDiagnoser]
public class FlatStandaloneBenchmarks
{
    FlatStandaloneMessage _message = null!;
    object _boxedMessage = null!;
    byte[] _serialized = null!;

    [GlobalSetup]
    public void Setup()
    {
        _message = new FlatStandaloneMessage
        {
            Id = 42,
            Sequence = 123456789,
            Accuracy = 0.98f,
            Enabled = true,
            Name = "flat-payload"
        };
        _boxedMessage = _message;
        _serialized = MessageSerializer.Serialize(_message);
    }

    [Benchmark]
    public byte[] SerializeGeneric() => MessageSerializer.Serialize(_message);

    [Benchmark]
    public byte[] SerializeObject() => MessageSerializer.Serialize(_boxedMessage);

    [Benchmark]
    public FlatStandaloneMessage DeserializeGeneric() => MessageSerializer.Deserialize<FlatStandaloneMessage>(_serialized);

    [Benchmark]
    public object DeserializeObject() => MessageSerializer.Deserialize(_serialized);
}

[MemoryDiagnoser]
public class DeepGraphBenchmarks
{
    DeepGraphMessage _message = null!;
    object _boxedMessage = null!;
    byte[] _serialized = null!;

    [GlobalSetup]
    public void Setup()
    {
        _message = new DeepGraphMessage
        {
            Root = BenchmarkDataFactory.CreateNodeChain(depth: 32)
        };
        _boxedMessage = _message;
        _serialized = MessageSerializer.Serialize(_message);
    }

    [Benchmark]
    public byte[] SerializeGeneric() => MessageSerializer.Serialize(_message);

    [Benchmark]
    public byte[] SerializeObject() => MessageSerializer.Serialize(_boxedMessage);

    [Benchmark]
    public DeepGraphMessage DeserializeGeneric() => MessageSerializer.Deserialize<DeepGraphMessage>(_serialized);

    [Benchmark]
    public object DeserializeObject() => MessageSerializer.Deserialize(_serialized);
}

[MemoryDiagnoser]
public class LargeCollectionBenchmarks
{
    LargeCollectionMessage _message = null!;
    object _boxedMessage = null!;
    byte[] _serialized = null!;

    [GlobalSetup]
    public void Setup()
    {
        var items = new List<int>(capacity: 4096);
        for (int i = 0; i < 4096; i++)
        {
            items.Add(i);
        }

        _message = new LargeCollectionMessage
        {
            Items = items
        };
        _boxedMessage = _message;
        _serialized = MessageSerializer.Serialize(_message);
    }

    [Benchmark]
    public byte[] SerializeGeneric() => MessageSerializer.Serialize(_message);

    [Benchmark]
    public byte[] SerializeObject() => MessageSerializer.Serialize(_boxedMessage);

    [Benchmark]
    public LargeCollectionMessage DeserializeGeneric() => MessageSerializer.Deserialize<LargeCollectionMessage>(_serialized);

    [Benchmark]
    public object DeserializeObject() => MessageSerializer.Deserialize(_serialized);
}

[MemoryDiagnoser]
public class SharedReferenceBenchmarks
{
    SharedReferenceBenchmarkMessage _message = null!;
    object _boxedMessage = null!;
    byte[] _serialized = null!;

    [GlobalSetup]
    public void Setup()
    {
        var shared = BenchmarkDataFactory.CreateNodeChain(depth: 12);
        _message = new SharedReferenceBenchmarkMessage
        {
            Left = shared,
            Right = shared
        };
        _boxedMessage = _message;
        _serialized = MessageSerializer.Serialize(_message);
    }

    [Benchmark]
    public byte[] SerializeGeneric() => MessageSerializer.Serialize(_message);

    [Benchmark]
    public byte[] SerializeObject() => MessageSerializer.Serialize(_boxedMessage);

    [Benchmark]
    public SharedReferenceBenchmarkMessage DeserializeGeneric() => MessageSerializer.Deserialize<SharedReferenceBenchmarkMessage>(_serialized);

    [Benchmark]
    public object DeserializeObject() => MessageSerializer.Deserialize(_serialized);
}

static class BenchmarkDataFactory
{
    public static BenchmarkNode CreateNodeChain(int depth)
    {
        BenchmarkNode? current = null;
        for (int i = depth; i >= 1; i--)
        {
            current = new BenchmarkNode
            {
                Value = i,
                Next = current
            };
        }

        return current ?? new BenchmarkNode();
    }
}

[StandaloneMessage(101)]
public partial class FlatStandaloneMessage
{
    public int Id { get; set; }
    public long Sequence { get; set; }
    public float Accuracy { get; set; }
    public bool Enabled { get; set; }
    public string Name { get; set; } = string.Empty;
}

[StandaloneMessage(102)]
public partial class DeepGraphMessage
{
    public BenchmarkNode Root { get; set; } = new();
}

[StandaloneMessage(103)]
public partial class LargeCollectionMessage
{
    public List<int> Items { get; set; } = new();
}

[StandaloneMessage(104)]
public partial class SharedReferenceBenchmarkMessage
{
    public BenchmarkNode Left { get; set; } = new();
    public BenchmarkNode Right { get; set; } = new();
}

public class BenchmarkNode
{
    public int Value { get; set; }
    public BenchmarkNode? Next { get; set; }
}
