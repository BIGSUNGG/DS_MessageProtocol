using MessageProtocol;

namespace SandboxMessages;

// ---------- S1: Standalone — 전체 멤버 타입 ----------

public enum Color : byte { Red, Green, Blue }

[StandaloneMessage(1)]
[MessageCategory(MessageCategory.Category3)]
public partial class AllPrimitives
{
    public bool Bool { get; set; }
    public byte Byte { get; set; }
    public sbyte SByte { get; set; }
    public short Int16 { get; set; }
    public ushort UInt16 { get; set; }
    public int Int32 { get; set; }
    public uint UInt32 { get; set; }
    public long Int64 { get; set; }
    public ulong UInt64 { get; set; }
    public float Single { get; set; }
    public double Double { get; set; }
    public decimal Decimal { get; set; }
    public char Char { get; set; }
    public string? Text { get; set; }
    public Color Color { get; set; }
}

// ---------- S2: NonId ----------

[NonIdMessage]
public partial class Ping
{
    public int Seq { get; set; }
}

// ---------- S3: 그룹 루트/요소 ----------

[GroupRootMessage(10)]
public partial class ShapeRoot
{
    public string? Name { get; set; }
}

[GroupElementMessage(11)]
public partial class Circle : ShapeRoot
{
    public double Radius { get; set; }
}

// ---------- S4: 컬렉션 ----------

[StandaloneMessage(2)]
public partial class Collections
{
    public byte[]? Bytes { get; set; }
    public int[]? Ints { get; set; }
    public List<string>? Names { get; set; }
    public List<AllPrimitives>? Items { get; set; }
    public IList<int>? View { get; set; }
}

// ---------- S5: 중첩 객체·그래프 ----------

public class NestedPoco
{
    public int X { get; set; }
    public string? Tag { get; set; }
}

[StandaloneMessage(3)]
public partial class TreeNode
{
    public string? Label { get; set; }
    public TreeNode? Left { get; set; }
    public TreeNode? Right { get; set; }
    public NestedPoco? Poco { get; set; }
}

// ---------- S6: 멤버 제어 ----------

[StandaloneMessage(4)]
public partial class MemberControl
{
    public int Kept { get; set; }

    [MessageIgnore]
    public int Skipped { get; set; }

    [MessageInclude]
    int _hidden;

    public void SetHidden(int value) => _hidden = value;
    public int GetHidden() => _hidden;
}
