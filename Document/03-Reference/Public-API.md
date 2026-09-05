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

버퍼 I/O 계약: 위치는 forward-only — `Skip`·`Advance` 는 음수 `count` 를 `ArgumentOutOfRangeException` 으로 거부하고(되돌려 이미 소비·기록한 구간을 다시 읽거나 덮어쓰는 것 차단), 범위를 넘는 전진은 reader `EndOfStreamException` / writer `InvalidOperationException` 을 던진다. 문자열 길이 접두사는 `-1` 만 null 이고 그 외 음수는 `InvalidDataException` 으로 거부된다.

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

흐름·스펙: [Feature-Spec](../02-Architecture/Feature-Spec.md). 구조: [Overview](../02-Architecture/Overview.md).

## 관련

- [Packages](./Packages.md)
- [GLOSSARY](../00-AI/GLOSSARY.md)
