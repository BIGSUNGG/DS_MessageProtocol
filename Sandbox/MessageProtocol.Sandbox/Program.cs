using MessageProtocol;
using MessageProtocol.Serialize;
using SandboxMessages;

// Sandbox 인수 조건: Feature-Spec F1–F7 을 실행으로 검증한다.
// 실패 시 0 이 아닌 종료 코드.

int failures = 0;

void Check(string name, bool condition)
{
    Console.WriteLine($"{(condition ? "PASS" : "FAIL")}  {name}");
    if (!condition) failures++;
}

// ---------- S1: Standalone + 전체 멤버 타입 round-trip ----------
{
    var msg = new AllPrimitives
    {
        Bool = true,
        Byte = 255,
        SByte = -1,
        Int16 = short.MinValue,
        UInt16 = ushort.MaxValue,
        Int32 = int.MinValue,
        UInt32 = uint.MaxValue,
        Int64 = long.MinValue,
        UInt64 = ulong.MaxValue,
        Single = 3.14f,
        Double = -2.71828,
        Decimal = 12345.6789m,
        Char = '한',
        Text = "메시지 프로토콜",
        Color = Color.Green,
    };

    byte[] bytes = MessageSerializer.Serialize(msg);
    var roundTrip = MessageSerializer.Deserialize<AllPrimitives>(bytes);

    Check("S1 round-trip 값 동일",
        roundTrip.Bool == msg.Bool && roundTrip.Byte == msg.Byte && roundTrip.SByte == msg.SByte &&
        roundTrip.Int16 == msg.Int16 && roundTrip.UInt16 == msg.UInt16 &&
        roundTrip.Int32 == msg.Int32 && roundTrip.UInt32 == msg.UInt32 &&
        roundTrip.Int64 == msg.Int64 && roundTrip.UInt64 == msg.UInt64 &&
        roundTrip.Single == msg.Single && roundTrip.Double == msg.Double &&
        roundTrip.Decimal == msg.Decimal && roundTrip.Char == msg.Char &&
        roundTrip.Text == msg.Text && roundTrip.Color == msg.Color);

    // 헤더: flags=Standalone(상위 니블) + category=3(하위 니블)
    byte expectedHeader = MessageWireFormat.ComposeHeaderByte(MessageFlag.Standalone, 3);
    Check("S1 헤더 바이트", bytes[0] == expectedHeader);

    uint expectedId = MessageWireFormat.ComposeMessageId(MessageFlag.Standalone, 3, 1);
    Check("S1 MessageId 조립", AllPrimitives.MessageId == expectedId);

    // null / 빈 문자열
    var nullText = new AllPrimitives { Text = null };
    var emptyText = new AllPrimitives { Text = string.Empty };
    Check("S1 null 문자열", MessageSerializer.Deserialize<AllPrimitives>(MessageSerializer.Serialize(nullText)).Text == null);
    Check("S1 빈 문자열", MessageSerializer.Deserialize<AllPrimitives>(MessageSerializer.Serialize(emptyText)).Text == string.Empty);
}

// ---------- S2: NonId ----------
{
    var ping = new Ping { Seq = 42 };
    byte[] bytes = MessageSerializer.Serialize(ping);
    Check("S2 NonId 헤더 1바이트", bytes[0] == MessageWireFormat.ComposeHeaderByte(MessageFlag.NonIdMessage, 0) && bytes.Length == 5);
    Check("S2 round-trip", MessageSerializer.Deserialize<Ping>(bytes).Seq == 42);

    bool rejected = false;
    try { MessageSerializer.Deserialize(bytes); }
    catch (InvalidCastException) { rejected = true; }
    Check("S2 object Deserialize는 NonId 거부", rejected);
}

// ---------- S3: 그룹 + object dispatch ----------
{
    var circle = new Circle { Name = "c1", Radius = 2.5 };
    byte[] bytes = MessageSerializer.Serialize((object)circle);
    object? decoded = MessageSerializer.Deserialize(bytes);

    Check("S3 object dispatch가 요소 타입 복원",
        decoded is Circle c && c.Name == "c1" && c.Radius == 2.5);

    byte header = bytes[0];
    Check("S3 요소 헤더 플래그", MessageWireFormat.GetFlags(header) == MessageFlag.GroupElement);
}

// ---------- S4: 컬렉션 ----------
{
    var msg = new Collections
    {
        Bytes = new byte[] { 1, 2, 3 },
        Ints = null,
        Names = new List<string> { "a", "b", null! },
        Items = new List<AllPrimitives> { new() { Int32 = 7, Text = "x" } },
        View = new List<int> { 10, 20 },
    };

    var rt = MessageSerializer.Deserialize<Collections>(MessageSerializer.Serialize(msg));
    Check("S4 byte[] round-trip", rt.Bytes != null && rt.Bytes.SequenceEqual(new byte[] { 1, 2, 3 }));
    Check("S4 null 배열", rt.Ints == null);
    Check("S4 List<string> round-trip", rt.Names != null && rt.Names.Count == 3 && rt.Names[0] == "a" && rt.Names[1] == "b");
    Check("S4 List<중첩 메시지>", rt.Items != null && rt.Items.Count == 1 && rt.Items[0].Int32 == 7 && rt.Items[0].Text == "x");
    Check("S4 IList<int>", rt.View != null && rt.View.Count == 2 && rt.View[1] == 20);
}

// ---------- S5: 순환·공유 참조 ----------
{
    var node = new TreeNode { Label = "root", Poco = new NestedPoco { X = 9, Tag = "t" } };
    node.Left = node;               // 자기 참조 → 백레퍼런스
    node.Right = node.Left;         // 공유 참조

    var rt = MessageSerializer.Deserialize<TreeNode>(MessageSerializer.Serialize(node));
    Check("S5 자기 참조 복원", ReferenceEquals(rt.Left, rt));
    Check("S5 공유 참조 복원", ReferenceEquals(rt.Right, rt.Left));
    Check("S5 중첩 POCO 복원", rt.Poco != null && rt.Poco.X == 9 && rt.Poco.Tag == "t");
}

// ---------- S6: MessageIgnore / MessageInclude ----------
{
    var msg = new MemberControl { Kept = 1, Skipped = 99 };
    msg.SetHidden(7);

    var rt = MessageSerializer.Deserialize<MemberControl>(MessageSerializer.Serialize(msg));
    Check("S6 일반 멤버 유지", rt.Kept == 1);
    Check("S6 MessageIgnore 제외", rt.Skipped == 0);
    Check("S6 MessageInclude 포함", rt.GetHidden() == 7);
}

// ---------- S7: 다형성 Serialize(object) ----------
{
    ShapeRoot shape = new Circle { Name = "poly", Radius = 1.0 };
    byte[] bytes = MessageSerializer.Serialize((object)shape);   // 런타임 타입 직렬화
    object? decoded = MessageSerializer.Deserialize(bytes);
    Check("S7 파생 타입 직렬화", decoded is Circle pc && pc.Name == "poly" && pc.Radius == 1.0);

    // 루트 자체도 등록·라우팅 가능
    var root = new ShapeRoot { Name = "root" };
    var rt = MessageSerializer.Deserialize(MessageSerializer.Serialize((object)root));
    Check("S7 루트 타입 라우팅", rt is ShapeRoot sr && sr.Name == "root");
}

// ---------- S8: PooledBuffer ----------
{
    var msg = new AllPrimitives { Int32 = 5, Text = "pooled" };
    using (var pooled = MessageSerializer.SerializePooled(msg))
    {
        byte[] compat = MessageSerializer.Serialize(msg);
        Check("S8 pooled == byte[] 경로", pooled.Span.SequenceEqual(compat));
        pooled.Dispose(); // 멱등 Dispose
        Check("S8 Dispose 멱등", pooled.Length == 0);
    }
}

// ---------- S9: 수동 구현 + RegisterType ----------
{
    MessageSerializer.RegisterType(typeof(ManualMessage));

    var msg = new ManualMessage { Value = 1234 };
    byte[] bytes = MessageSerializer.Serialize(msg);      // 제네릭 경로
    Check("S9 수동 메시지 제네릭 round-trip", MessageSerializer.Deserialize<ManualMessage>(bytes).Value == 1234);

    object? decoded = MessageSerializer.Deserialize(bytes); // ID 라우팅
    Check("S9 수동 메시지 object dispatch", decoded is ManualMessage m && m.Value == 1234);
}

// ---------- S10: 제네릭 메시지 ----------
{
    // T 에는 object dispatch 가능한 ID 메시지(여기선 AllPrimitives)를 담는다.
    // NonId 메시지는 규격상 디스패치 대상이 아니라 T 구성으로 라우팅할 수 없다.
    var msg = new Envelope<AllPrimitives>
    {
        Note = "gen",
        Value = new AllPrimitives { Int32 = 7, Text = "t" },
        Items = new List<AllPrimitives?> { new() { Int32 = 1 }, null, new() { Int32 = 2 } },
    };

    var rt = MessageSerializer.Deserialize<Envelope<AllPrimitives>>(MessageSerializer.Serialize(msg));
    Check("S10 제네릭 round-trip", rt.Note == "gen" && rt.Value != null && rt.Value.Int32 == 7 && rt.Value.Text == "t");
    Check("S10 T 컬렉션 round-trip", rt.Items != null && rt.Items.Count == 3 && rt.Items[0]!.Int32 == 1 && rt.Items[1] == null && rt.Items[2]!.Int32 == 2);

    // 닫힌 구성은 선언 기반 자동 등록으로 object dispatch 가능 (수동 등록 없음)
    object? decodedGeneric = MessageSerializer.Deserialize(MessageSerializer.Serialize((object)msg));
    Check("S10 제네릭 object dispatch", decodedGeneric is Envelope<AllPrimitives> env && env.Value!.Int32 == 7);
}

// ---------- S11: 제네릭 구성 공존·와이어 헤더 ----------
{
    var a = new Envelope<AllPrimitives> { Value = new AllPrimitives { Int32 = 1 } };
    var b = new Envelope<Circle> { Value = new Circle { Name = "c", Radius = 2.0 } };

    byte[] bytesA = MessageSerializer.Serialize(a);
    Check("S11 제네릭 헤더 플래그 0", MessageWireFormat.GetFlags(bytesA[0]) == MessageFlag.Generic);
    Check("S11 클래스 ID 기록", bytesA[4] == 0 && bytesA[5] == 0 && bytesA[6] == 1);

    object? da = MessageSerializer.Deserialize(bytesA);
    object? db = MessageSerializer.Deserialize(MessageSerializer.Serialize((object)b));
    Check("S11 구성 공존 A", da is Envelope<AllPrimitives> ea && ea.Value!.Int32 == 1);
    Check("S11 구성 공존 B", db is Envelope<Circle> ec && ec.Value!.Radius == 2.0);
}

Console.WriteLine();
Console.WriteLine(failures == 0 ? "ALL SCENARIOS PASSED" : $"{failures} SCENARIO CHECK(S) FAILED");
return failures == 0 ? 0 : 1;
