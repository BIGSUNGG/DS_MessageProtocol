---
project: DS_MessageProtocol
type: reference
status: draft
tags: [api]
updated: 2026-07-11
---

# Public API

공개 타입·진입점 레퍼런스. 구현 세부보다 계약 표면을 적는다.

## 진입점

| API | 설명 |
|-----|------|
| `MessageSerializer` | 등록·Serialize·Deserialize 정적 진입점 (`MessageProtocol.Serialize`) |
| `MessageBufferWriter` / `MessageBufferReader` | 페이로드 버퍼 I/O |
| `PooledBuffer` | 풀링된 직렬화 결과 |
| `MessageWireFormat` | 헤더 크기·MessageId 조립/분해 헬퍼 |
| `MessageFlag` | 헤더 flags 니블 (`NonId` / `Standalone` / `GroupRoot` / `GroupElement`) |

## 메시지 타입 속성

| 속성 | 역할 |
|------|------|
| `StandaloneMessageAttribute(uint id)` | 독립 ID 메시지 |
| `GroupRootMessageAttribute(uint id)` | 그룹 루트 |
| `GroupElementMessageAttribute(uint id)` | 그룹 요소 (id ≠ 0) |
| `NonIdMessageAttribute` | ID 없는 메시지 (헤더 1바이트) |
| `MessageCategoryAttribute(MessageCategory)` | category 니블 0..15 |

ID 값 범위: `0 .. 2^24-1`.

## 멤버 속성

| 속성 | 대상 | 역할 |
|------|------|------|
| `MessageIgnoreAttribute` | field/property | 직렬화 제외 |
| `MessageIncludeAttribute` | field/property | 직렬화 포함 힌트 |

## 계약 인터페이스

| 인터페이스 | 기대 static 멤버 |
|------------|------------------|
| `IMessageSerializable<T>` | `Serialize` / `Deserialize` (writer·reader·byte[]) |
| `IHasIdMessageSerializable<T>` | 위 + `public static uint MessageId` |

생성기가 partial에 구현을 붙이거나, 수동으로 동일 형태를 노출한 뒤 등록한다.

## MessageSerializer

| 메서드 | 설명 |
|--------|------|
| `RegisterHasIdMessage<T>()` | ID 메시지 등록 |
| `RegisterNonIdMessage<T>()` | NonId 등록 |
| `RegisterType(Type)` | 리플렉션 기반 등록 |
| `Serialize<T>(T)` | 선언 타입 `T`의 제네릭 캐시 경로 (`byte[]`) |
| `Serialize(object)` / `SerializeToWriter` | 런타임 타입 dispatch — **다형성**(베이스 변수 + 파생 인스턴스)에 사용 |
| `SerializePooled*` | ArrayPool 기반 결과 (`PooledBuffer`) |
| `Deserialize<T>(...)` | 제네릭 역직렬화 |
| `Deserialize(byte[]\|Span\|Memory)` | MessageId 기반 object 역직렬화 (Standalone/Group만) |

`Serialize<T>`는 런타임 타입을 보지 않는다. 파생 메시지로 직렬화하려면 `Serialize((object)msg)`를 쓴다.

핫 경로 권장: `Serialize(T, ref MessageBufferWriter)` / `SerializePooled<T>` / `Deserialize<T>(Span)`.

흐름: [[Data-Flow]]. 잔여 이슈: [[Known-Issues]].

## 관련

- [[Packages]]
- [[Components]]
- [[Getting-Started]]
- [[GLOSSARY]]
