---
project: DS_MessageProtocol
type: troubleshoot
status: draft
tags: [known-issues, generator, runtime]
updated: 2026-09-01
---

# Known Issues

v2 코드 리뷰(2026-08-31)에서 확인된 문제점. KI-13은 2026-09-01 프로덕션 적합성 검토 중 추가·같은 날 해결. 빌드·테스트 56개·Sandbox 28 시나리오는 전부 통과하는 상태에서 발견한 것들이다.

## 확인된 버그 (실험 검증)

### KI-1. 제네릭 메시지 타입 → 진단 없이 깨진 코드 생성 (해결)

**상태: 해결 (2026-08-31).** [ADR-0002](../05-Decisions/ADR-0002-Generic-Message-Serialization.md)로 연기 결정 후 같은 날 [ADR-0003](../05-Decisions/ADR-0003-Generic-Message-Serialization.md)으로 지원 구현, 이어 [ADR-0004](../05-Decisions/ADR-0004-Generic-Message-Wire-Format.md)로 전용 속성 + 구성 클래스 ID 와이어 재설계(구성 공존·수신 측 무설정). 회귀 테스트·Sandbox S10/S11 포함.

원본 발견 내용:

`[StandaloneMessage(1)] partial class Msg<T> { public int Value; }` 처럼 멤버 타입이 전부 지원 대상인 제네릭 메시지 타입은 진단 없이 생성 코드를 방출하는데 그 코드가 컴파일 불가다.

- `MakeHelperSuffix`가 `MetadataName`(`Msg`1`)을 써서 헬퍼 메서드 이름에 백틱이 들어간다 (`__WritePayload_N_Msg`1`).
- `Define.Emit`은 `Symbol.Name`만 써서 partial 선언이 타입 매개변수를 잃는다 (`partial class Msg` vs `Msg<T>`).
- 사용자 프로젝트에 수십 개의 무관한 CS 구문 에러가 뜨고 MSGPROT 진단은 없다.

조치 방향: ~~제네릭 메시지 타입 거부 진단 추가~~ → ADR-0002로 연기 → **ADR-0003으로 지원 구현 완료**.

### KI-2. 메시지 속성 충돌 → 무진단, 런타임 object 역직렬화 실패 (해결)

**상태: 해결 (2026-08-31).** `MSGPROT007` 경고 진단 추가 — 한 타입에 메시지 속성 2개 이상이면 경고하고 생성을 건너뛴다 (`MessageCodeGenerator.TryReportDuplicateMessageAttributes`, 회귀 테스트 2개).

원본 발견 내용:

`[NonIdMessage]`와 `[StandaloneMessage(1)]`을 동시에 붙이면 진단 없이 `flags = NonId|Standalone` 헤더(0x30)를 만들고:

- 생성 코드는 4바이트 헤더를 쓴다 (`IsStandalone || IsGroup` 기준).
- 런타임 `RegisterCore`는 `HasEmbeddedMessageId(0x30) == false`(NonId 비트)로 판정해 ID·reader 등록을 **조용히 건너뛴다**.
- 결과: `Deserialize(object)` 호출 시 `KeyNotFoundException`. 제네릭 경로만 동작.

조치 방향: ~~네 메시지 속성 상호 배타 진단 추가~~ → 완료. 남은 꼬리: `RegisterCore`가 NonId 비트 가진 HasId 등록을 조용히 건너뛰는 동작은 수동 등록 경로에 남아 있음 (필요 시 별도 가드).

### KI-13. 컬렉션 길이 접두사 선할당 → 불신 피어 OOM DoS (해결)

**상태: 해결 (2026-09-01).** 생성기가 배열·리스트 역직렬화 전 변형에 할당 전 남은 버퍼 가드를 출력한다 — 고정 크기 요소 `길이×요소크기 ≤ Remaining` 정확 검증, 가변 크기 요소 `개수 ≤ Remaining`(요소 최소 와이어 1바이트) 상한 검증, 초과 시 `EndOfStreamException`. `MessageSerializeCodeEmitter.Member`의 `EmitArrayRead`·`EmitListRead` 5 변형, 회귀 테스트 4개(`CollectionGuardTests`).

원본 발견 내용:

생성 `Deserialize`가 길이·개수 접두사를 남은 바이트 검증 **전에** `new T[len]`·`new List<T>(len)` 할당에 써서, 불신 피어가 악성 길이(`int.MaxValue`)를 보내면 데이터 검증이 돌기도 전에 거대 할당→`OutOfMemoryException`으로 프로세스가 죽는다. bulk 경로의 `len * elemSize` 곱셈은 int 오버플로 가능. 문자열(`ReadString`)은 `ReadBytes` 경계 검증이 먼저라 영향 없음.

조치 방향: 할당 전 상한 가드 생성 코드 출력 → 완료. 정책 옵션 없이 통일 상한(설정 없음)으로 결정.

## 잠재 결함 (코드 리뷰)

| 번호 | 위치 | 내용 |
| ---- | ---- | ---- |
| KI-3 | `MessageSerializeCodeEmitter.Member._uniqueIdCounter` | 프로세스 전역 정적 카운터 → 생성 코드가 이전 컴파일 이력에 의존(비결정적). 증분 캐싱·재현성 저하. `EmitState`로 옮겨야 함 |
| KI-4 | `GetAllMembers` (emitter·graph 중복 정의) | 와이어 멤버 순서가 `Dictionary.Values` 열거 순서에 의존 — 현 .NET에서는 삽입 순서지만 규약 아님. `List` 권장 |
| KI-5 | 생성 `Deserialize(ref reader)` | 헤더·MessageId 를 검증하지 않고 건너뜀 — 다른 타입 바이트를 먹이면 조용히 재해석 (성능 트레이드오프, 문서화 필요) |
| KI-6 | `MessageBufferReader.ReadString` | `-1`만 null 규약인데 모든 음수를 null 처리 — 손상 데이터가 조용히 통과 |
| KI-7 | `MessageBufferWriter.PatchInt32` | 오프셋 경계 검증 없음. `Grow`의 `Length * 2`는 1GB 부근 int 오버플로 가능 |
| KI-8 | `MessageCategoryAttribute` | 범위 밖 카테고리 값이 `& 0x0F` 로 조용히 마스킹 |
| KI-9 | 그래프 밖 메시지 위임 (`EmitOutOfGraphMessage*`) | 위임 시 새 `SerializeContext` — 어셈블리 경계 넘는 공유 참조 복원 불가, 경계 넘는 순환 참조는 스택 오버플로. 제약 문서화 필요 |
| KI-10 | 증분 파이프라인 | `Collect` 결과 `ImmutableArray`는 참조 동등성이라 후보 캐시가 매번 무효 — 편집마다 전체 재방출 (성능) |
| KI-11 | `SerializerCachePrefill` | 같은 타입 병렬 등록/재등록 시 경쟁·잔존 상태 가능 (엣지) |
| KI-12 | 빌드 경고 | RS2008 — 분석기 릴리스 추적(`AnalyzerReleases.Shipped.md`) 미사용, 경고 12개 |

## 관련

- [Feature-Spec](../02-Architecture/Feature-Spec.md)
- [CONTEXT](../00-AI/CONTEXT.md)
