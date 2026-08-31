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

    [Benchmark(Baseline = true)]
    public byte[] SerializeByteArray() => MessageSerializer.Serialize(_message);

    [Benchmark]
    public byte[] SerializeObject() => MessageSerializer.Serialize(_boxedMessage);

    [Benchmark]
    public int SerializePooled()
    {
        using var buffer = MessageSerializer.SerializePooled(_message);
        return buffer.Length;
    }

    [Benchmark]
    public int SerializeDirect()
    {
        var writer = MessageBufferWriter.Create();
        try
        {
            FlatStandaloneMessage.Serialize(_message, ref writer);
            return writer.Length;
        }
        finally
        {
            writer.Dispose();
        }
    }

    [Benchmark]
    public FlatStandaloneMessage DeserializeByteArray() => MessageSerializer.Deserialize<FlatStandaloneMessage>(_serialized);

    [Benchmark]
    public object DeserializeObject() => MessageSerializer.Deserialize(_serialized);

    [Benchmark]
    public FlatStandaloneMessage DeserializeSpan() => MessageSerializer.Deserialize<FlatStandaloneMessage>(new ReadOnlySpan<byte>(_serialized));

    [Benchmark]
    public FlatStandaloneMessage DeserializeDirect()
    {
        var reader = new MessageBufferReader(_serialized);
        return FlatStandaloneMessage.Deserialize(ref reader);
    }
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

    [Benchmark(Baseline = true)]
    public byte[] SerializeByteArray() => MessageSerializer.Serialize(_message);

    [Benchmark]
    public byte[] SerializeObject() => MessageSerializer.Serialize(_boxedMessage);

    [Benchmark]
    public int SerializePooled()
    {
        using var buffer = MessageSerializer.SerializePooled(_message);
        return buffer.Length;
    }

    [Benchmark]
    public int SerializeDirect()
    {
        var writer = MessageBufferWriter.Create();
        try
        {
            DeepGraphMessage.Serialize(_message, ref writer);
            return writer.Length;
        }
        finally
        {
            writer.Dispose();
        }
    }

    [Benchmark]
    public DeepGraphMessage DeserializeByteArray() => MessageSerializer.Deserialize<DeepGraphMessage>(_serialized);

    [Benchmark]
    public DeepGraphMessage DeserializeSpan() => MessageSerializer.Deserialize<DeepGraphMessage>(new ReadOnlySpan<byte>(_serialized));

    [Benchmark]
    public DeepGraphMessage DeserializeDirect()
    {
        var reader = new MessageBufferReader(_serialized);
        return DeepGraphMessage.Deserialize(ref reader);
    }
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

    [Benchmark(Baseline = true)]
    public byte[] SerializeByteArray() => MessageSerializer.Serialize(_message);

    [Benchmark]
    public byte[] SerializeObject() => MessageSerializer.Serialize(_boxedMessage);

    [Benchmark]
    public int SerializePooled()
    {
        using var buffer = MessageSerializer.SerializePooled(_message);
        return buffer.Length;
    }

    [Benchmark]
    public int SerializeDirect()
    {
        var writer = MessageBufferWriter.Create();
        try
        {
            LargeCollectionMessage.Serialize(_message, ref writer);
            return writer.Length;
        }
        finally
        {
            writer.Dispose();
        }
    }

    [Benchmark]
    public LargeCollectionMessage DeserializeByteArray() => MessageSerializer.Deserialize<LargeCollectionMessage>(_serialized);

    [Benchmark]
    public LargeCollectionMessage DeserializeSpan() => MessageSerializer.Deserialize<LargeCollectionMessage>(new ReadOnlySpan<byte>(_serialized));

    [Benchmark]
    public LargeCollectionMessage DeserializeDirect()
    {
        var reader = new MessageBufferReader(_serialized);
        return LargeCollectionMessage.Deserialize(ref reader);
    }
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

    [Benchmark(Baseline = true)]
    public byte[] SerializeByteArray() => MessageSerializer.Serialize(_message);

    [Benchmark]
    public byte[] SerializeObject() => MessageSerializer.Serialize(_boxedMessage);

    [Benchmark]
    public int SerializePooled()
    {
        using var buffer = MessageSerializer.SerializePooled(_message);
        return buffer.Length;
    }

    [Benchmark]
    public int SerializeDirect()
    {
        var writer = MessageBufferWriter.Create();
        try
        {
            SharedReferenceBenchmarkMessage.Serialize(_message, ref writer);
            return writer.Length;
        }
        finally
        {
            writer.Dispose();
        }
    }

    [Benchmark]
    public SharedReferenceBenchmarkMessage DeserializeByteArray() => MessageSerializer.Deserialize<SharedReferenceBenchmarkMessage>(_serialized);

    [Benchmark]
    public SharedReferenceBenchmarkMessage DeserializeSpan() => MessageSerializer.Deserialize<SharedReferenceBenchmarkMessage>(new ReadOnlySpan<byte>(_serialized));

    [Benchmark]
    public SharedReferenceBenchmarkMessage DeserializeDirect()
    {
        var reader = new MessageBufferReader(_serialized);
        return SharedReferenceBenchmarkMessage.Deserialize(ref reader);
    }
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
