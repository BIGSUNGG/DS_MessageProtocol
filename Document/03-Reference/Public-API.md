---
project: DS_MessageProtocol
type: reference
status: stable
tags: [api]
updated: 2026-09-05
---

# Public API

공개 타입·진입점 레퍼런스. 구현 세부보다 계약 표면을 적는다. (모든 공개 타입 네임스페이스: `MessageProtocol` 또는 `MessageProtocol.Serialize`.)

## 진입점

| API | 설명 |
| ----- | ------ |
| `MessageSerializer` | 등록·Serialize·Deserialize 정적 진입점 (`MessageProtocol.Serialize`) |
| `MessageBufferWriter` / `MessageBufferReader` | 페이로드 버퍼 I/O (리틀엔디안, forward-only) |
| `PooledBuffer` | 풀링된 직렬화 결과 (Dispose 멱등) |
| `MessageWireFormat` | 헤더 크기·상수·MessageId 조립/분해 헬퍼 |
| `MessageFlag` | 헤더 flags 니블 (`NonIdMessage` / `Standalone` / `GroupRoot` / `GroupElement`) |

`MessageWireFormat` 상수: `NonIdHeaderSize=1`, `IdHeaderSize=4`, `NullSizedPayloadLength=-1`, `DefaultStreamCapacity=256`, `NibbleMask=0x0F`, `MessageIdValueMask=0x00FFFFFF`.

버퍼 I/O 계약: 위치는 forward-only — `Skip`·`Advance` 는 음수 `count` 를 `ArgumentOutOfRangeException` 으로 거부하고(되돌려 이미 소비·기록한 구간을 다시 읽거나 덮어쓰는 것 차단), 범위를 넘는 전진은 reader `EndOfStreamException` / writer `InvalidOperationException` 을 던진다. 문자열 길이 접두사는 `-1` 만 null 이고 그 외 음수는 `InvalidDataException` 으로 거부된다. 버퍼는 단일 `byte[]` 이라 페이로드 상한은 배열 상한(`0X7FEFFFFF` 바이트)이며, 이를 넘는 문자열은 `WriteString` 이 `ArgumentException` 으로 거부한다(용량 산술은 `long` — int 오버플로로 증설이 건너뛰어지지 않음).

중첩 깊이 계약: `MessageBufferReader` 는 중첩 객체 역직렬화 깊이를 reader 단위로 센다 — `EnterNestedObject()` 는 상한(`MaxNestingDepth`, 기본 `MessageBufferReader.DefaultMaxNestingDepth = 64`)에 도달하면 `InvalidDataException` 을 던지고(와이어 내용 불법 — 경계 `EndOfStreamException` 과 구분), `LeaveNestedObject()` 는 깊이를 1 낮춘다(0 아래로 클램프 — 짝이 맞지 않는 호출이 가드를 무력화하지 못함). 현재 깊이는 `NestingDepth` 로 관찰한다. 생성 코드(그래프 내부 중첩 객체·그래프 밖 메시지 위임)와 `MessageSerializer.DeserializeFromReader`(타입 매개변수 멤버·외부 호출자)가 재귀 지점에서 이 쌍을 호출하므로, 불신 피어가 작은 프레임에 `ReferenceKind.NewObject` 만 늘어놓아 재귀 스택을 소진시키는 것(스택 오버플로 — catch 불가, 프로세스 사망)이 차단된다. **수동 구현도 중첩 객체를 재귀로 판독할 때 같은 쌍을 호출해야 한다.** 합법적으로 깊은 객체 그래프는 `new MessageBufferReader(buffer, maxNestingDepth)` 로 상한을 올려 처리하며(0 이하 `ArgumentOutOfRangeException`), 상한은 스레드 스택 크기보다 작아야 한다.

쓰기 쪽도 동일한 계약을 갖는다 — `MessageBufferWriter.EnterNestedObject()` 는 상한(`MaxNestingDepth`, 기본 `MessageBufferWriter.DefaultMaxNestingDepth` = reader 기본값과 **동일하게 고정**) 도달 시 `InvalidOperationException` 을 던지고(와이어 손상이 아니라 호출자 객체 그래프가 너무 깊은 경우라 예외 타입이 다르다), `LeaveNestedObject()` 는 0 아래로 클램프하며 낮춘다. 생성 코드(그래프 내부 중첩 객체·그래프 밖 메시지 위임)와 `MessageSerializer.SerializeToWriter`(타입 매개변수·추상 메시지 멤버, 수동 구현)가 재귀 지점에서 이 쌍을 호출하므로, 수만 노드 연결 리스트·깊은 트리나 **디스패치 멤버로 돌아가는 순환 그래프**(그 경로는 백레퍼런스를 추적하지 않음)가 쓰기 재귀로 스택을 소진시키는 것(catch 불가 스택 오버플로)이 차단된다. 상한은 `MessageBufferWriter.Create(initialCapacity, maxNestingDepth)` 로 올리며(0 이하 `ArgumentOutOfRangeException`), **깊은 그래프를 보내려면 수신 측 reader 상한도 같이 올려야 한다**(기본값이 서로 같으므로 기본 설정끼리는 쓴 것은 항상 읽힌다).

## 메시지 타입 속성

| 속성 | 역할 |
| ------ | ------ |
| `StandaloneMessage(uint id)` | 독립 ID 메시지 |
| `GroupRootMessage(uint id)` | 그룹 루트 |
| `GroupElementMessage(uint id)` | 그룹 요소 (id ≠ 0) |
| `NonIdMessage` | ID 없는 메시지 (헤더 1바이트) |
| `MessageCategory(MessageCategory)` | category 니블 0..15 |

ID 값 범위: `0 .. 2^24-1`.

## 멤버 속성

| 속성 | 대상 | 역할 |
|------|------|------|
| `MessageIgnoreAttribute` | field/property | 직렬화 제외 |
| `MessageIncludeAttribute` | field/property | 직렬화 포함 (비공개 멤버 포함 시 사용) |

우선순위: `MessageIgnore` > `MessageInclude` > public 접근성.

## 계약 인터페이스

| 인터페이스 | 기대 static 멤버 |
|------------|------------------|
| `IMessageSerializable<T>` | `Serialize` / `Deserialize` (writer·reader·byte[]) |
| `IHasIdMessageSerializable<T>` | 위 + `public static uint MessageId` |

생성기가 partial에 구현을 붙이거나, 수동으로 동일 형태를 노출한 뒤 등록한다. 수동 구현 시 헤더는 와이어 순서(헤더 바이트 → ID 3바이트)로 직접 기록해야 한다.

참조 추적 계약(수동 구현 시 필수): 중첩 객체 그래프의 공유·순환 참조는 `MessageSerializer.SerializeContext`·`DeserializeContext` 와 `ReferenceKind`(Null=0 · NewObject=1 · BackReference=2)로 복원한다. 쓰기는 참조 타입 멤버마다 ① null → `ReferenceKind.Null` 1바이트, ② `TryGetObjectId` 가 id 를 주면 `BackReference` + int32 id, ③ 처음이면 `RegisterObject` 후 `NewObject` + 페이로드 순서로 기록하고, 읽기는 같은 순서로 `RegisterNewObject`·`GetObject` 을 호출해야 한다(**양쪽 등록 순서가 동일해야 id 가 맞는다**). **null 은 컨텍스트에 등록·조회하면 안 된다** — `_firstObject is null` 이 빈 슬롯 sentinel 이라 null 등록 시 슬롯이 차지되지 않아 **id 1 이 중복 발급**되고 백레퍼런스가 다른 인스턴스로 해석된다(예외 없는 객체 그래프 손상 — Known-Issues KI-30). 세 메서드(`RegisterObject`·`TryGetObjectId`·`RegisterNewObject`)는 null 을 `ArgumentNullException` 으로 거부한다. 재귀 지점에서 중첩 깊이 계상(`EnterNestedObject`·`LeaveNestedObject`)도 호출해야 한다(KI-14·KI-25).

## MessageSerializer

| 메서드 | 설명 |
| -------- | ------ |
| `RegisterHasIdMessage<T>()` / 델리게이트 오버로드 | ID 메시지 등록 |
| `RegisterNonIdMessage<T>()` / 델리게이트 오버로드 | NonId 등록 |
| `RegisterType(Type)` | 리플렉션 기반 등록 |
| `Serialize<T>(T)` / `Serialize<T>(T, ref writer)` | 선언 타입 `T`의 제네릭 캐시 경로 — 런타임 타입 미참조 |
| `Serialize(object)` / `SerializeToWriter` | 런타임 타입 dispatch — 다형성(베이스 변수 + 파생 인스턴스) |
| `SerializePooled<T>` / `SerializePooled(object)` | ArrayPool 기반 결과 (`PooledBuffer`) |
| `Deserialize<T>(...)` | 제네릭 역직렬화 (byte[]/Span/Memory/reader) |
| `Deserialize(byte[]\|Span\|Memory)` | MessageId 기반 object 역직렬화 (Standalone/Group만) |

핫 경로 권장: `Serialize(T, ref MessageBufferWriter)` / `SerializePooled<T>` / `Deserialize<T>(Span)`.

예외 계약: 미등록·계약 미구현 타입의 `Serialize<T>`·`Deserialize<T>` 는 필요한 멤버를 안내하는 `InvalidOperationException` 을 던진다 — CLR 이 타입별로 영구 캐싱하는 `TypeInitializationException` 이 아니며, 등록 전에 캐시를 먼저 건드렸더라도 이후 델리게이트 등록(`RegisterHasIdMessage<T>(…)`, `RegisterNonIdMessage<T>(…)`)으로 복구된다. 리플렉션 등록 경로(`RegisterHasIdMessage<T>()`·`RegisterNonIdMessage<T>()`·`RegisterGenericConstruction<T>`)는 직렬화 델리게이트 부재를 나중 NRE 가 아니라 **등록 시점**에 같은 예외로 알린다.

흐름·스펙: [Feature-Spec](../02-Architecture/Feature-Spec.md). 구조: [Overview](../02-Architecture/Overview.md).

## 관련

- [Packages](./Packages.md)
- [GLOSSARY](../00-AI/GLOSSARY.md)
