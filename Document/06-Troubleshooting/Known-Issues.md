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

런타임은 「생성 코드 + 제네릭 캐시」 방향은 맞지만, flat 메시지의 강제 `Dictionary`·문자열 `ToArray`·HasId의 object 우회가 핫 경로를 막고, 생성기는 incremental이 아니며 Unity/netstandard과 맞지 않는 API를 낸다.

## 1. 성능 병목 (핫 경로)

### (심각) flat 메시지마다 `Dictionary` 할당

참조형 루트는 중첩 객체가 없어도 직렬화/역직렬화 시 `SerializeContext.RegisterObject` / `DeserializeContext.RegisterNewObject`를 항상 호출한다. 첫 호출에서 `Dictionary`가 할당된다.

| 위치 | 내용 |
|------|------|
| 생성기 | `MessageSerializeCodeEmitter.Method.EmitSerialize` / `EmitDeserialize` |
| 런타임 | `MessageSerializer.Utility` — `SerializeContext` / `DeserializeContext` |

공유·순환 참조가 없는 flat Standalone도 메시지마다 GC 압력이 생긴다. context를 생략하거나 첫 back-reference 필요 시점에만 지연 할당하는 편이 맞다.

### (심각) `ReadString`의 불필요한 `ToArray`

`MessageBufferReader.ReadString`이 `ReadBytes`로 얻은 `ReadOnlySpan<byte>`에 `.ToArray()`를 한 뒤 `Encoding.UTF8.GetString`을 호출한다. netstandard2.1에는 span 오버로드가 있으므로 임시 배열은 제거 가능하다. 문자열이 있는 메시지에서 역직렬화 GC의 주요 원인이 된다.

### (높음) `Serialize<T>(T)` ID 메시지가 object 경로로 우회

`MessageSerializer.Serialize<T>`가 `IHasIdMessageSerializable<T>`이면 `Serialize((object)message)`로 보낸다. 다형성 보존 목적이지만, 구체 타입 제네릭 호출에서도 `GetType()` + `ConcurrentDictionary` lookup이 발생한다. 벤치 `SerializeByteArray` baseline이 이미 이 경로다. 다형성은 별도 API로 두고 제네릭은 `SerializerCache<T>.SerializeBytes`를 쓰는 편이 낫다.

### (중간) 프리미티브마다 `EnsureCapacity`

`MessageBufferWriter`의 `WriteInt32` 등이 호출마다 용량을 검사한다. 헤더만 `WriteByte`×4. flat 페이로드에서도 분기 비용이 쌓인다. 멤버 크기를 미리 합산해 `EnsureCapacity` 한 번으로 줄일 수 있다.

### (중간) `decimal` / `byte[]` 호환 API 할당

- `WriteDecimal` / `ReadDecimal`: `GetBits`·`new int[4]` 할당
- 생성 `Serialize(T) → ToArray()`: 풀 버퍼 → 새 `byte[]` 복사 (호환 API이지만 핫 경로는 `SerializePooled` / writer 권장)

### (중간) object `Deserialize`

`ConcurrentDictionary<uint, BufferReaderFunc>` lookup + `object` 반환. 수신 쪽에서 자주 쓰면 제네릭 경로 대비 고정 오버헤드.

## 2. 구조·설계

### 생성기가 incremental이 아님

`MessageCodeGenerator`가 `CompilationProvider`에 `RegisterSourceOutput`한다. 컴파일 변경마다 전체 named type 스캔이라 `IIncrementalGenerator`의 이점을 거의 쓰지 못한다. 메시지 타입이 늘수록 IDE/빌드 체감이 나빠진다.

### 미지원 타입을 TODO 주석으로 삼킴

`MessageSerializeCodeEmitter.Member`가 지원하지 않는 타입에 `// TODO: Serialize/Deserialize`만 넣고 진단(MSGPROT)을 내지 않는다. 필드가 조용히 누락되어 와이어가 불완전해질 수 있다. (`Dictionary`, `HashSet`, 다차원 배열 등)

### 컬렉션 지원 범위·생성 API

지원: `List<T>` / `IList<T>` / 1차원 배열. 생성 코드는 `CollectionsMarshal.AsSpan`·`SetCount`를 쓰는데 이는 .NET 5/6+ API다. 패키지 타깃은 netstandard2.1·Unity인데, 구형 런타임에서는 생성 코드가 컴파일 실패할 수 있다. `IList<T>` 프로퍼티에 `CollectionsMarshal.AsSpan`을 붙이면 `List<T>`가 아니면 컴파일도 깨진다.

### `PooledBuffer` 이중 Dispose

`readonly struct`이고 `Dispose`가 풀 반환 플래그를 지우지 않는다. 복사본 dispose 또는 이중 dispose 시 ArrayPool 이중 반환 → 버퍼 오염 위험.

### 하드코딩된 디버그 I/O

생성기가 `C:\Debug\`에 `.g.debug.cs`를 쓴다. 빌드 중 파일 I/O, Windows 전용, analyzer 금지 API(RS1035) 우회.

### 등록/캐시가 리플렉션에 의존

`SerializerCache<T>` 정적 생성자가 `GetMethods` + `CreateDelegate`로 메서드를 찾는다. 첫 등록/첫 사용 비용과 깨지기 쉬운 계약. 생성기가 델리게이트를 직접 연결하면 완화 가능.

## 3. 호환·패키지

| 이슈 | 내용 |
|------|------|
| `ModuleInitializer` | netstandard2.1에 속성 타입이 없음. Core polyfill이 없으면 Unity 등에서 생성 코드가 깨질 수 있음 |
| `CollectionsMarshal` | 위와 동일 — 문서의 Unity 호환([[Scope]])과 충돌 |
| 테스트/벤치만 net9.0 | 소비자 TFM(netstandard2.1)에서의 생성 코드 컴파일을 검증하지 못함 |
| API 이중화 | `Serialize` / `SerializePooled` / writer / object — 핫 패스 선택이 문서·API에 덜 드러남 |

## 4. 개선 우선순위 (제안)

| 순위 | 항목 | 예상 효과 |
|------|------|-----------|
| 1 | flat 경로에서 `SerializeContext` Dictionary 제거/지연 | 메시지당 할당 대폭 감소 |
| 2 | `ReadString`에서 `ToArray` 제거 | 문자열 역직렬화 GC |
| 3 | `Serialize<T>` HasId를 제네릭 캐시로 (다형성은 별도 API) | 기본 API latency |
| 4 | 생성기를 syntax/attribute 기반 incremental로 | 빌드/IDE |
| 5 | 미지원 타입 → 진단 에러 | 조용한 데이터 유실 방지 |
| 6 | Unity용 `CollectionsMarshal`/`ModuleInitializer` polyfill 또는 폴백 코드 생성 | 실제 타깃 호환 |
| 7 | `PooledBuffer` 안전한 Dispose | 풀 오염 방지 |

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
