---
project: DS_MessageProtocol
type: troubleshoot
status: draft
tags: [known-issues, generator, runtime]
updated: 2026-09-05
---

# Known Issues

v2 코드 리뷰(2026-08-31)에서 확인된 문제점. KI-13·KI-15는 2026-09-01 프로덕션 적합성·공격 표면 검토 중 추가·같은 날 해결, KI-14는 미해결로 남음. 2026-09-04 감사에서 KI-16·KI-17·KI-18·KI-19·KI-20 추가·같은 날 해결, KI-21~KI-22 추가. 2026-09-05 감사 루프에서 KI-6·KI-12·KI-21·KI-22 해결. 빌드·테스트 56개·Sandbox 28 시나리오는 전부 통과하는 상태에서 발견한 것들이다.

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

### KI-20. UTF8 관대한 폴백 → 왕복 시 조용한 문자열 변형 (해결)

**상태: 해결 (2026-09-04).** 정책 결정: 와이어 무결성 엄격 정책(KI-15와 동일 기조). `WriteString`·`ReadString` 이 엄격 UTF-8(`EncoderFallback.ExceptionFallback`·`DecoderFallback.ExceptionFallback`) 사용 — 고립 서로게이트는 쓰기에서 인코딩 예외로 실패(대체 바이트로 조용한 변환 없음), 무효 UTF-8 바이트는 읽기에서 `InvalidDataException` 거부(경계 `EndOfStreamException` 과 구분). 회귀 테스트 2개. 테스트 91→93.

원본 발견 내용:

기본(교체 폴백) `Encoding.UTF8` 이 송신 측에서는 고립 서로게이트를 대체 바이트로 조용히 재인코딩하고, 수신 측에서는 무효 바이트를 U+FFFD 로 변환했다 — 왕복이 송신과 다른 문자열을 반환할 수 있고, 손상 패킷이 오류 없이 복호되어 와이어 손상이 은폐됐다.

조치 방향: 교체 대신 거부 → 완료. 송신 측 예외는 자연스러운 `EncoderFallbackException`(프로그래밍 오류), 수신 측은 `InvalidDataException`(와이어 내용 불법)으로 보고.

### KI-6. `ReadString` 음수 길이 접두사 → 손상 데이터가 null 로 조용히 통과 (해결)

**상태: 해결 (2026-09-05).** `ReadString` 이 `-1` 만 null 로 복호하고, 나머지 음수(`-2`…`int.MinValue`)는 `InvalidDataException` 으로 거부 — KI-15·KI-20 와이어 무결성 엄격 기조와 동일(경계 위반 `EndOfStreamException` 과 와이어 내용 불법을 구분). 회귀 테스트 4개(`-2`·`-3`·`int.MinValue` 거부 + `-1` null 복호 유지). 테스트 93→97.

원본 발견 내용:

null 규약은 길이 접두 `-1` 인데 판독 코드가 `length < 0` 전체를 null 로 처리했다. 손상되거나 악意적인 프레임의 `-2`·`int.MinValue` 접두사가 오류 없이 null 문자열로 복호되어, 필드 누락(전송 실패)과 와이어 손상을 수신 측이 구분할 수 없었다.

조치 방향: 규약 외 음수 거부 → 완료.

### KI-21. `Skip`·`Advance` 음수 허용 → 위치 되돌림 (해결)

**상태: 해결 (2026-09-05).** `MessageBufferReader.Skip`·`MessageBufferWriter.Advance` 가 음수 `count` 를 공개 경계에서 `ArgumentOutOfRangeException` 으로 거부 — forward-only 규약 강제. 되돌림이 실제로 일으키던 두 행동(소비한 바이트 재읽기, 기록한 페이로드 덮어쓰기)에 대한 행동 회귀 테스트 + `0`·양수 정상 경로 보존 테스트. 회귀 테스트 7개(케이스). 테스트 97→104. `Public-API` 버퍼 I/O 계약 명문화.

원본 발견 내용:

두 메서드 모두 `(uint)(_position + count) > (uint)_buffer.Length` 상한 검증만 했고, 음수는 이 검증을 통과해 `_position` 을 줄였다. `Skip(-1)` 은 예외 없이 이미 소비한 바이트를 다시 읽게 하고(경계 검사도 잘못된 `EndOfStreamException` 을 던져 프로그래밍 오류와 혼동됨), `Advance(-1)` 은 `Length` 를 줄여 다음 쓰기가 기록된 페이로드를 덮어쓰게 했다. 생성 코드는 음수를 넘기지 않아 수동·외부 호출자 대상 위험.

조치 방향: 공개 경계에서 음수 거부 → 완료. 되돌림은 `EndOfStreamException`(와이어 경계) 이 아니라 `ArgumentOutOfRangeException`(호출자 프로그래밍 오류) 으로 보고.

### KI-22. `WriteString` 용량 산술 int 오버플로 → 증설 누락 (해결)

**상태: 해결 (2026-09-05).** 필요 용량을 `long` 으로 계산하는 내부 헬퍼 `GetStringBufferRequirement(charCount)` 도입(UTF-8 상한 공식 `4 + 문자당 3바이트 + 프리앰블 3` 을 직접 long 산술) — `Encoding.GetMaxByteCount(int)` 의 int 오버플로 의존 제거. 버퍼(배열) 상한 `0X7FEFFFFF` 를 넘으면 `GetBytes` 의 내부 `ArgumentException` 대신 명확한 메시지의 `ArgumentException` 으로 거부. 가드 후 `required ≤ MaxBufferLength - _position < int.MaxValue` 라 좁힘·이후 `EnsureCapacity` int 합산도 오버플로하지 않는다. 회귀 테스트 9개(케이스) — 자체 공식이 `GetMaxByteCount + 4` 와 일치함을 715,827,881자까지 검증(상한을 좁히거나 헤프게 바꾸지 않음), int 상한 너머에서 양수·단조증가 유지, 30만 바이트 한글 문자열 증설·왕복. 테스트 104→113. `Core` 에 `InternalsVisibleTo(MessageProtocol.Tests)` 추가.

원본 발견 내용:

`WriteString` 이 `EnsureCapacity(4 + StrictUtf8.GetMaxByteCount(value.Length))` 로 용량을 확보했는데, `GetMaxByteCount` 는 `charCount * 3 + 3` 을 int 로 계산하므로 약 7.15억 자(문자 수 > 715,827,881)에서 음수로 오버플로한다. 음수가 `EnsureCapacity` 에 들어가면 `_position + additional > _buffer.Length` 비교가 거짓이 되어 증설이 건너뛰어지고, 이은 `GetBytes` 가 공간 부족으로 실패했다. `GetBytes` 가 출력 배열 경계를 검사하므로 메모리 손상은 없고 예외만 내부 원인(버퍼 부족)을 가리는 형태였다.

조치 방향: long 산술 + 명확한 상한 거부 → 완료. 임계값 미만에서는 기존과 동일한 용량을 요청하므로 동작 변화 없음(회귀 테스트로 고정).

### KI-12. RS2008 — 분석기 릴리스 추적 미사용 (해결)

**상태: 해결 (2026-09-05).** `Source/MessageProtocol.CodeGenerator/` 에 `AnalyzerReleases.Shipped.md`(릴리스 2.0 = `v2.0.0` 태그 기준 MSGPROT001~008, 007 만 Warning) · `AnalyzerReleases.Unshipped.md`(태그 이후 추가된 MSGPROT010·011) 추가. SDK 가 두 파일을 자동으로 `AdditionalFiles` 에 포함하므로 csproj 에 별도 선언하지 않는다(중복 선언 시 컴파일러에 두 번 전달됨). 검증: `dotnet build MessageProtocol.sln -t:Rebuild` 클린 리빌드 결과 **경고 0개·오류 0개**(RS2008·RS2007 모두 소멸), 테스트 113개·Sandbox 전체 통과. `Packages` 에 새 진단 규칙 추가 시 추적 파일 갱신 규약 명문화.

원본 발견 내용:

분석기 프로젝트에 `EnforceExtendedAnalyzerRules` 가 켜져 있는데 릴리스 추적 파일이 없어, `DiagnosticDescriptor` 마다 RS2008 경고가 발생했다(클린 빌드 기준 16건 보고). 증분 빌드에서는 컴파일이 건너뛰어져 경고가 보이지 않으므로 오랫동안 방치되기 쉬운 형태였고, 진단 규칙이 어떤 릴리스에 도입·변경됐는지에 대한 기록도 없었다.

함정 두 가지(수정 중 확인): ① 구분 행을 `-------- | ---------- | ---------- | -------` 처럼 공백 포함으로 쓰면 Roslyn 릴리스 추적 파서가 인식하지 못해 RS2007(잘못된 릴리스 헤더)이 발생 — 반드시 `--------|----------|----------|-------` 형태여야 한다(마크다운 자동 서식 도구가 이 행을 바꾸지 않도록 주의). ② csproj 에 `AdditionalFiles` 를 수동 선언하면 SDK 자동 포함과 겹쳐 같은 파일이 컴파일러에 두 번 전달된다.

조치 방향: 추적 파일 추가 + 형식 엄수 → 완료.

## 잠재 결함 (코드 리뷰)

| 번호 | 위치 | 내용 |
| ---- | ---- | ---- |
| KI-3 | `MessageSerializeCodeEmitter.Member._uniqueIdCounter` | 프로세스 전역 정적 카운터 → 생성 코드가 이전 컴파일 이력에 의존(비결정적). 증분 캐싱·재현성 저하. `EmitState`로 옮겨야 함 |
| KI-4 | `GetAllMembers` (emitter·graph 중복 정의) | 와이어 멤버 순서가 `Dictionary.Values` 열거 순서에 의존 — 현 .NET에서는 삽입 순서지만 규약 아님. `List` 권장 |
| KI-5 | 생성 `Deserialize(ref reader)` | 헤더·MessageId 를 검증하지 않고 건너뜀 — 다른 타입 바이트를 먹이면 조용히 재해석 (성능 트레이드오프, 문서화 필요) |
| KI-7 | `MessageBufferWriter.PatchInt32` | 오프셋 경계 검증 없음. `Grow`의 `Length * 2`는 1GB 부근 int 오버플로 가능 |
| KI-8 | `MessageCategoryAttribute` | 범위 밖 카테고리 값이 `& 0x0F` 로 조용히 마스킹 |
| KI-9 | 그래프 밖 메시지 위임 (`EmitOutOfGraphMessage*`) | 위임 시 새 `SerializeContext` — 어셈블리 경계 넘는 공유 참조 복원 불가, 경계 넘는 순환 참조는 스택 오버플로. 제약 문서화 필요 |
| KI-10 | 증분 파이프라인 | `Collect` 결과 `ImmutableArray`는 참조 동등성이라 후보 캐시가 매번 무효 — 편집마다 전체 재방출 (성능) |
| KI-11 | `SerializerCachePrefill` | 같은 타입 병렬 등록/재등록 시 경쟁·잔존 상태 가능 (엣지) |
| KI-14 | 생성 역직렬화 중첩 객체 판독 | 자기참조 메시지 중첩이 재귀로 판독 — 깊이가 프레임 크기 ÷ 최소 페이로드로만 제한됨. 큰 프레임 상한 환경에서 스택 오버플로 DoS 가능. 프레임 상한을 크게 잡을 경우 깊이 카운터 필요 |

## 관련

- [Feature-Spec](../02-Architecture/Feature-Spec.md)
- [CONTEXT](../00-AI/CONTEXT.md)
