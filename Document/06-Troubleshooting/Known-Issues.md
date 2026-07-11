---
project: DS_MessageProtocol
type: troubleshoot
status: draft
tags: [performance, structure, bottlenecks, known-issues]
updated: 2026-07-11
---

# Known Issues — 구조·성능·병목

코드 분석(2026-07-11) 기준. 수정 시 이 노트의 해당 항목을 갱신하고 [[Changelog]]에 한 줄 남긴다.

## 요약

핫 경로 이슈(flat `Dictionary` 지연, `ReadString` `ToArray` 제거, `Serialize<T>` 제네릭 캐시, `PooledBuffer` Dispose 멱등)는 해결됨. 생성기 incremental·미지원 타입 진단·Unity polyfill은 잔여.

## 해결됨 (2026-07-11)

| 항목 | 조치 |
|------|------|
| flat 메시지 `Dictionary` 할당 | `SerializeContext` / `DeserializeContext`가 첫 객체는 슬롯만 쓰고, 두 번째 등록부터 Dictionary 할당 |
| `ReadString` `ToArray` | `Encoding.UTF8.GetString(ReadOnlySpan<byte>)` 직접 사용 |
| `Serialize<T>` HasId object 우회 | 제네릭은 항상 `SerializerCache<T>.SerializeBytes`. 다형성은 `Serialize(object)` |
| `PooledBuffer` 이중 Dispose | struct 필드를 Dispose 시 clear하여 동일 인스턴스 멱등 |

## 잔여 — 성능 (중간)

### 프리미티브마다 `EnsureCapacity`

`MessageBufferWriter`의 `WriteInt32` 등이 호출마다 용량을 검사한다. 멤버 크기를 미리 합산해 `EnsureCapacity` 한 번으로 줄일 수 있다.

### `decimal` / `byte[]` 호환 API 할당

- `WriteDecimal` / `ReadDecimal`: `GetBits`·`new int[4]` 할당
- 생성 `Serialize(T) → ToArray()`: 풀 버퍼 → 새 `byte[]` 복사 (핫 경로는 `SerializePooled` / writer 권장)

### object `Deserialize`

`ConcurrentDictionary<uint, BufferReaderFunc>` lookup + `object` 반환. 수신 쪽에서 자주 쓰면 제네릭 경로 대비 고정 오버헤드.

## 잔여 — 구조·설계

### 생성기가 incremental이 아님

`MessageCodeGenerator`가 `CompilationProvider`에 `RegisterSourceOutput`한다. 컴파일 변경마다 전체 named type 스캔이라 `IIncrementalGenerator`의 이점을 거의 쓰지 못한다.

### 미지원 타입을 TODO 주석으로 삼킴

`MessageSerializeCodeEmitter.Member`가 지원하지 않는 타입에 `// TODO`만 넣고 진단(MSGPROT)을 내지 않는다. 필드가 조용히 누락될 수 있다.

### 컬렉션 지원 범위·생성 API

지원: `List<T>` / `IList<T>` / 1차원 배열. 생성 코드의 `CollectionsMarshal`은 .NET 5/6+ API라 Unity/구형 런타임에서 깨질 수 있다.

### 하드코딩된 디버그 I/O

생성기가 `C:\Debug\`에 `.g.debug.cs`를 쓴다.

### 등록/캐시가 리플렉션에 의존

`SerializerCache<T>` 정적 생성자가 `GetMethods` + `CreateDelegate`로 메서드를 찾는다.

## 잔여 — 호환·패키지

| 이슈 | 내용 |
|------|------|
| `ModuleInitializer` | netstandard2.1에 속성 타입이 없음. Core polyfill이 없으면 Unity 등에서 생성 코드가 깨질 수 있음 |
| `CollectionsMarshal` | 문서의 Unity 호환([[Scope]])과 충돌 |
| 테스트/벤치만 net9.0 | 소비자 TFM에서의 생성 코드 컴파일을 검증하지 못함 |
| API 이중화 | 핫 패스 선택이 문서에 덜 드러남 — [[Public-API]]에 `Serialize<T>` vs `Serialize(object)` 안내 추가됨 |

## 후속 우선순위

| 순위 | 항목 |
|------|------|
| 1 | 생성기를 syntax/attribute 기반 incremental로 |
| 2 | 미지원 타입 → 진단 에러 |
| 3 | Unity용 `CollectionsMarshal`/`ModuleInitializer` polyfill 또는 폴백 코드 생성 |
| 4 | EnsureCapacity 일괄 / decimal 무할당 |

## 관련 코드

- `Source/MessageProtocol.Core/Serialize/MessageSerializer*.cs`
- `Source/MessageProtocol.Core/Serialize/MessageBufferReader.cs` / `MessageBufferWriter.cs` / `PooledBuffer.cs`
- `Source/MessageProtocol.CodeGenerator/Generate/MessageCodeGenerator.cs`
- `Source/MessageProtocol.CodeGenerator/Generate/MessageSerializeCodeEmitter*.cs`
- `Test/MessageProtocol.Benchmarks/SerializationBenchmarks.cs`

## 관련

- [[FAQ]]
- [[Data-Flow]]
- [[Overview]]
- [[Scope]]
- [[Public-API]]
