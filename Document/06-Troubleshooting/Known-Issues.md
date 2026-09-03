---
project: DS_MessageProtocol
type: troubleshoot
status: draft
tags: [known-issues, generator, runtime]
updated: 2026-09-04
---

# Known Issues

v2 코드 리뷰(2026-08-31)에서 확인된 문제점. KI-13·KI-15는 2026-09-01 프로덕션 적합성·공격 표면 검토 중 추가·같은 날 해결, KI-14는 미해결로 남음. 2026-09-04 감사에서 KI-16·KI-17·KI-18·KI-19 추가·같은 날 해결, KI-20~KI-22 추가. 빌드·테스트 56개·Sandbox 28 시나리오는 전부 통과하는 상태에서 발견한 것들이다.

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

### KI-15. `ReadDecimal` 무검증 비트 재해석 → 원격 프로세스 크래시 (해결)

**상태: 해결 (2026-09-01).** `MessageBufferReader.ReadDecimal`이 재해석 전에 flags 검증 — 스케일 >28 또는 부호·스케일 외 예약 비트 존재 시 `InvalidDataException`으로 거부. 유효 `decimal`이 가질 수 없는 비트만 거부하므로 호환성 손실 없음. 회귀 테스트 3개(`BufferIOTests`: 스케일 78 거부·예약 비트 거부·경계 스케일 28 허용).

원본 발견 내용 (실험 검증):

와이어 16바이트를 검증 없이 `decimal` 비트로 재해석해 공격자가 flags 전체를 제어할 수 있었다. 스케일 78 이상으로 만든 값은 **파싱은 통과**한 뒤 게임 로직에서 덧셈·뺄셈에 쓰이는 순간 런타임 `DecCalc` 내부 고정 스택 버퍼를 오버플로시켜 **SIGSEGV — try/catch 불가, 프로세스 즉시 사망**. 스케일 ≤77 안전(값 왜곡만), ≥78 크래시, 곱·비교는 생존(실험: 덧셈 기준 임계 스케일 78). 파싱 직후가 아니라 로직 한가운데서 터지는 지연 지뢰라 추적이 어렵고 패킷 하나로 100% 재현 가능.

조치 방향: 판독 시 flags 검증(거부) → 완료. 무결성 위반은 `EndOfStreamException`(경계)과 구분해 `InvalidDataException`(와이어 내용 불법)으로 보고.

### KI-16. 동일 제네릭 페이로드 두 구성의 헬퍼 이름 충돌 → 소비자 CS0111 (해결)

**상태: 해결 (2026-09-04).** `SerializationGraph.MakeHelperSuffix`가 타입 인자·중첩 타입 체인을 포함한 유일 접미사를 만들고, 그래프 단위 사용 접미사 집합으로 잔여 충돌에 구분자를 붙인다. 회귀 픽스처 `DuplicateGenericPayloadsMessage`(`GenericPair<int>`+`GenericPair<string>` 공존)·왕복 테스트 추가로 수정 전 CS0111 재현 검증. 테스트 83→84.

원본 발견 내용:

한 메시지 그래프에 동일 제네릭 페이로드의 두 구성(`Pair<int>`·`Pair<string>`)이 도달 가능하면 접미사가 `네임스페이스+MetadataName`(`Ns_Pair_1`)으로 동일해 `__WritePayload_…`/`__CreateInstance_…` 헬퍼가 같은 partial 클래스에 중복 방출 → 소비자 프로젝트가 CS0111로 컴파일 실패. 같은 형태가 동명 중첩 타입(`Outer1.Point`·`Outer2.Point`)에서도 발생. 기존 테스트는 한 그래프에 두 구성을 넣지 않아 미발견.

조치 방향: 접미사 유일성 보장 → 완료. `MessageCodeGenerator.MakeCarrierSuffix`(구성 등록 캐리어)는 동일 형태 결함으로 KI-19 에서 같은 전략으로 해결.

### KI-17. `CollectionsMarshal` 미지원 `List<T>` 벌크 가드 누락 → 불신 피어 최대 8배 선할당 (해결)

**상태: 해결 (2026-09-04).** `EmitListRead` 의 `CollectionsMarshal` 미지원(요소별 판독) 분기 할당 전 가드를 `개수 ≤ Remaining` 에서 `개수×요소크기 ≤ Remaining`(long 산술)으로 격상 — 동일 타입의 `CollectionsMarshal` 고속 경로와 동일 검증. 회귀 테스트는 `InternalsVisibleTo` 로 이미터 진입점(`TryEmit`, `hasCollectionsMarshal: false`)을 직접 구동해 생성 가드 텍스트를 검증(약한 가드로 역전 시 실패 확인). 테스트 84→85.

원본 발견 내용:

KI-13 가드가 5 변형 전부 적용됐다고 기록됐으나, `CollectionsMarshal` 미지원 타깃(예: netstandard2.0 소비자)의 `List<long>`·`List<double>` 벌크 판독 분기는 개수만 검증했다. 불신 피어가 `개수 = Remaining` 을 보내면 가드를 통과하고 `new List<T>(개수)` 가 남은 버퍼의 최대 8배(8바이트 요소 기준)를 선할당한 뒤에야 요소 판독이 예외를 던진다.

조치 방향: 형제 경로와 동일한 `개수×요소크기` 가드 → 완료.

### KI-18. 생성 불가 페이로드 → 진단 없이 컴파일 불가 코드 방출 (해결)

**상태: 해결 (2026-09-04).** 세 갈래 검출: (1) 루트 메시지 타입이 추상이거나 매개변수 없는 생성자가 없으면 `MSGPROT010` 으로 생성 거부, (2) 중첩 페이로드가 추상 클래스·포지셔널 레코드 등 기본 생성 불가 타입이면 그래프 수집에서 제외 → 멤버 단위 `MSGPROT006`(미지원 타입), (3) 읽기 전용·초기화 전용 프로퍼티·읽기전용 필드처럼 대입 불가 멤버는 `MSGPROT011` 으로 생성 거부(루트는 자기 partial 이라 모든 접근 수준 허용, 중첩 페이로드는 internal 이상 설정자 요구). 회귀 테스트 5개. 테스트 85→90.

원본 발견 내용:

`EmitReferenceTypeMethods` 가 페이로드 구축 가능성을 검사하지 않고 `new {TypeName}()` 를 방출해 추상 클래스 멤버는 CS0712, 포지셔널 레코드는 매개변수 없는 생성자가 없어 CS7036, init-only 멤버는 대입 불가(CS0200/CS8852)가 생성 코드에서 났다. 멤버 선택(`TypeMetadata`)도 설정 가능성을 검사하지 않아 get-only 프로퍼티가 어떤 메시지에서든 대입 에러를 만들었다. 사용자는 속성 없이 불투명한 생성 코드 에러만 봤다.

조치 방향: 진단 승격 → 완료. 새 진단 `MSGPROT010`(생성 불가 메시지 타입)·`MSGPROT011`(대입 불가 멤버), `EmitState` 미지원 사유 열거 확장.

### KI-19. 구성 등록 캐리어 접미사 충돌 → 동명 중첩 호스트 CS0102 (해결)

**상태: 해결 (2026-09-04).** `MakeCarrierSuffix` 제거, KI-16과 동일 전략의 공용 `SymbolNaming.MakeUniqueSuffix`(네임스페이스·중첩 체인·제네릭 인자 + 컴파일 단위 사용 접미사 집합으로 구분자 부여)로 교체 — 그래프 헬퍼와 캐리어가 하나의 이름 체계 공유. 회귀 테스트: 같은 네임스페이스 동명 중첩 캐리어 2개(수정 전 충돌 재현 검증). 테스트 90→91.

원본 발견 내용:

`__GenericConstructionRegistration_{접미사}` 캐리어 클래스의 접미사가 `네임스페이스+MetadataName` 이라 같은 네임스페이스의 동명 중첩 호스트(`OuterA.Carrier`·`OuterB.Carrier`)가 동일 접미사 → 두 최상위 클래스 동명 충돌(CS0102) + `AddSource` 힌트 이름 중복으로 생성기 실행 자체가 깨진다. KI-16과 동일 형태의 결함.

조치 방향: 유일 접미사 체계 공유 → 완료.

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
| KI-12 | 빌드 경고 | RS2008 — 분석기 릴리스 추적(`AnalyzerReleases.Shipped.md`) 미사용, 경고 16개(클린 빌드 기준, 증분 빌드에 가려짐) |
| KI-14 | 생성 역직렬화 중첩 객체 판독 | 자기참조 메시지 중첩이 재귀로 판독 — 깊이가 프레임 크기 ÷ 최소 페이로드로만 제한됨. 큰 프레임 상한 환경에서 스택 오버플로 DoS 가능. 프레임 상한을 크게 잡을 경우 깊이 카운터 필요 |
| KI-20 | `MessageBufferWriter.WriteString`·`MessageBufferReader.ReadString` | UTF8 기본(관대한) 폴백 — 고립 서로게이트·무효 바이트가 대체 문자로 조용히 변환되어 왕복 시 문자열 변형·와이어 손상 은폐. 정책 결정 필요(엄격 거부 또는 문서화) |
| KI-21 | `MessageBufferReader.Skip`·`MessageBufferWriter.Advance` | 음수 허용 — `Skip(-n)`이 리더를 뒤로 이동. 생성 코드는 음수를 넘기지 않아 수동·외부 호출자 대상 위험 |
| KI-22 | `MessageBufferWriter.WriteString:203` | `4 + GetMaxByteCount` 미검사 정수 덧셈 — 특정 초대형 문자열 길이에서 오버플로로 용량 증설 누락(메모리 손상 없음, `ArgumentException`으로 표면화) |

## 관련

- [Feature-Spec](../02-Architecture/Feature-Spec.md)
- [CONTEXT](../00-AI/CONTEXT.md)
