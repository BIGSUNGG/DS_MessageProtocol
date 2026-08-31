---
project: DS_MessageProtocol
type: troubleshoot
status: draft
tags: [performance, structure, bottlenecks, known-issues]
updated: 2026-07-11
---

# Known Issues — 구조·성능·병목

코드 기준 분석일: **2026-07-11**. 수정하면 해당 항목을 갱신하고 [[Changelog]]에 한 줄 남긴다.

관련 구조 문서: [[Overview]] · [[Components]] · [[Data-Flow]] · [[Scope]]

---

## 1. 프로젝트 구조 평가

### 1.1 현재 레이아웃

```
Source/
├── Directory.Build.props          # netstandard2.1, packable
├── Shared/                        # MessageFlag, MessageWireFormat (Link Compile)
├── MessageProtocol.Core/          # 런타임 NuGet (netstandard2.1;net6.0)
├── MessageProtocol.CodeGenerator/ # Roslyn 생성기 NuGet
└── MessageProtocol/               # 통합 meta 패키지
Test/
├── MessageProtocol.CoreTests/     # xUnit (net8.0;net9.0)
└── MessageProtocol.Benchmarks/    # BenchmarkDotNet (net9.0)
Examples/
└── MinimalConsole/                # Standalone round-trip 샘플
Document/                          # Obsidian vault
```

### 1.2 잘 된 점

| 항목 | 설명 |
|------|------|
| 3층 분리 | Core(런타임) / CodeGenerator(컴파일) / MessageProtocol(통합) — 소비자 진입점이 명확 |
| Shared Link | 와이어 헤더 규칙을 Core·Generator가 동일 소스로 공유 → 드리프트 방지 |
| 컴파일 타임 생성 | 핫 경로를 리플렉션 직렬화가 아닌 생성 코드로 처리 |
| 테스트·벤치·예제 분리 | round-trip / 진단 / BenchmarkDotNet / MinimalConsole이 역할별로 나뉨 |
| API 이중화 | `Serialize<T>`(제네릭 캐시) vs `Serialize(object)`(다형성) 경로가 의도적으로 분리됨 |

---

## 2. 성능 프로필 (강점)

핫 경로에서 이미 잘 갖춰진 부분. 회귀 시 벤치(`Test/MessageProtocol.Benchmarks`)로 확인.

| 강점 | 구현 |
|------|------|
| 제네릭 직렬화 | `SerializerCache<T>` 정적 필드 + 델리게이트 — Dictionary lookup/박싱 없음 |
| 버퍼 풀링 | `MessageBufferWriter` + `ArrayPool` + `PooledBuffer` / `SerializePooled` |
| flat 그래프 할당 지연 | `SerializeContext`/`DeserializeContext`가 첫 객체는 슬롯만 사용, 2번째부터 Dictionary |
| 문자열 읽기 | `Encoding.UTF8.GetString(ReadOnlySpan<byte>)` — 중간 `ToArray` 없음 |
| Span 기반 읽기 | `MessageBufferReader`는 `ReadOnlySpan<byte>` forward-only |
| AggressiveInlining | 프리미티브 Write/Read, 제네릭 Serialize/Deserialize 진입점 |
| EnsureCapacity 일괄 | 생성기가 고정 크기 프리미티브 구간을 합산해 `writer.EnsureCapacity(n)` 1회 |
| decimal 무할당 | `stackalloc` + `MemoryMarshal.Cast` (GetBits `int[4]` 없음) |
| 등록 fast path | 생성 `ModuleInitializer`가 `RegisterHasIdMessage<T>(serialize, deserialize, messageId)` 직접 호출 |

### 권장 핫 패스 API

| 시나리오 | 사용 |
|----------|------|
| 타입 고정, 최소 할당 | `SerializePooled<T>` 또는 `Serialize<T>(…, ref MessageBufferWriter)` |
| 다형성(베이스→파생) | `Serialize(object)` / `SerializeToWriter` |
| ID 기반 수신 | `Deserialize(ReadOnlySpan<byte>)` (object) 또는 타입 알면 `Deserialize<T>(span)` |
| 호환용 `byte[]` | `Serialize<T>` → 내부 `ToArray()` 복사 발생. 핫 패스 비권장 |

상세: [[Public-API]] · [[Data-Flow]].

---

## 3. 잔여·수용 이슈

### P3. object `Deserialize` 고정 오버헤드 (낮음~중간, by design)

**현상:** `ConcurrentDictionary<uint, BufferReaderFunc>` lookup + `object` 반환. 라우터/게이트웨이처럼 타입을 모를 때 필수.

**대응:** 타입이 알려진 경로는 항상 `Deserialize<T>`. Dictionary → 밀집 ID 배열 테이블은 ID 공간(`2^24`)이 커서 비현실적. 현 구조 유지.

### P6. 문자열 쓰기 max 바이트 예약 (낮음, **수용**)

**현상:** `WriteString`이 `GetMaxByteCount`로 상한만큼 `EnsureCapacity` 후 실제 길이만 사용. 과도한 Grow 가능(드묾).

**결정:** 코드 변경 없음. 긴 문자열 위주 워크로드가 실측으로 병목이 되면 `GetByteCount`/2-pass를 재검토.

### P7. 깊은 그래프·공유 참조 Dictionary (낮음, **수용**)

**현상:** 객체 2개 이상이면 `Dictionary` 할당. 공유/순환 참조 프로토콜에 필요.

**결정:** 코드 변경 없음. flat은 이미 지연 할당. 깊은 그래프는 `SharedReferenceBenchmarks`로 모니터링. 스택 소형 map 등은 복잡도 대비 이득이 작아 보류.

---

## 4. 해결됨

| 항목 | 조치 |
|------|------|
| flat 메시지 `Dictionary` 할당 | Context가 첫 객체는 슬롯만, 두 번째부터 Dictionary |
| `ReadString` `ToArray` | Span 직접 `GetString` |
| `Serialize<T>` HasId object 우회 | 제네릭은 항상 `SerializerCache<T>.SerializeBytes` |
| `PooledBuffer` 이중 Dispose | Dispose 시 필드 clear로 멱등 |
| **S1** 생성기 incremental | `ForAttributeWithMetadataName` 속성별 SyntaxProvider + Collect |
| **S2** 미지원 멤버 타입 | `MSGPROT006 UnsupportedMemberType` Error 진단 |
| **S3** 하드코딩 디버그 I/O | `C:\Debug` 덤프 제거 |
| **S4** 등록 리플렉션 | `RegisterHasIdMessage`/`RegisterNonIdMessage` 델리게이트 오버로드; 캐시는 등록 시 채움 |
| **S5** 테스트 TFM | CoreTests `net8.0;net9.0` 멀티타깃 |
| **S6** Examples 부재 | `Examples/MinimalConsole` + 솔루션 `Examples` 폴더 + [[Getting-Started]] 링크 |
| **P1** EnsureCapacity 반복 | 생성기 고정 크기 구간 일괄 `EnsureCapacity` |
| **P2** decimal 할당 | Writer/Reader stackalloc·MemoryMarshal 경로 |
| **P4** CollectionsMarshal | 컴파일 API 존재 시 고속, 없으면 `for`/`Add` 폴백 |
| **P5** ModuleInitializer | Core `netstandard2.1` polyfill + `net6.0` 멀티타깃 (CS0436 방지) |

---

## 5. 후속 우선순위

| 순위 | ID | 항목 | 비고 |
|------|-----|------|------|
| — | P3 | object Deserialize 오버헤드 | by design — 문서·가이드만 |
| — | P6 | WriteString max 예약 | **수용** — 실측 병목 시 재검토 |
| — | P7 | 깊은 그래프 Dictionary | **수용** — 벤치 모니터링 |

구조·호환·핫 패스 코드 이슈(S1–S6, P1–P2, P4–P5)는 §4로 이관됨.

---

## 6. 검증 체크리스트

변경 후:

- [x] `dotnet test` (`Test/MessageProtocol.CoreTests` — net8.0 + net9.0)
- [x] `dotnet build Examples/MinimalConsole -c Release`
- [x] `dotnet build MessageProtocol.sln -c Release`
- [ ] (선택) `dotnet run -c Release` on Benchmarks — Flat / DeepGraph / LargeCollection / SharedReference
- [ ] Document: 이 노트 항목 상태 갱신 + [[Changelog]] 한 줄

---

## 관련 코드

- `Source/MessageProtocol.Core/Serialize/MessageSerializer*.cs`
- `Source/MessageProtocol.Core/Serialize/MessageBufferReader.cs` / `MessageBufferWriter.cs` / `PooledBuffer.cs`
- `Source/MessageProtocol.Core/Polyfill/ModuleInitializerAttribute.cs`
- `Source/MessageProtocol.CodeGenerator/Generate/MessageCodeGenerator.cs`
- `Source/MessageProtocol.CodeGenerator/Generate/MessageSerializeCodeEmitter*.cs`
- `Test/MessageProtocol.Benchmarks/SerializationBenchmarks.cs`
- `Examples/MinimalConsole/`

## 관련

- [[FAQ]]
- [[Data-Flow]]
- [[Overview]]
- [[Components]]
- [[Scope]]
- [[Public-API]]
- [[How-To]]
- [[Getting-Started]]
