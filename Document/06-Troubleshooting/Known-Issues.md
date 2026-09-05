---
project: DS_MessageProtocol
type: troubleshoot
status: draft
tags: [known-issues, generator, runtime]
updated: 2026-09-05
---

# Known Issues

v2 코드 리뷰(2026-08-31)에서 확인된 문제점. KI-13·KI-15는 2026-09-01 프로덕션 적합성·공격 표면 검토 중 추가·같은 날 해결, KI-14는 미해결로 남았다가 2026-09-05 해결. 2026-09-04 감사에서 KI-16·KI-17·KI-18·KI-19·KI-20 추가·같은 날 해결, KI-21~KI-22 추가. 2026-09-05 감사 루프에서 KI-6·KI-12·KI-14·KI-21·KI-22 해결, 같은 날 백로그 소진 후 감사 패스에서 KI-23 추가·해결, 이어서 개선 루프에서 KI-24·KI-26·KI-27·KI-28 추가·해결, KI-25·KI-11·KI-4·KI-3 해결(그 외 신규 항목은 감사 루프 원장 `.pi-glla/audit-loop/findings.md` 에서 추적). 빌드·테스트 56개·Sandbox 28 시나리오는 전부 통과하는 상태에서 발견한 것들이다.

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

### KI-23. 공개 인덱서 → 문법 오류 생성 코드 (해결)

**상태: 해결 (2026-09-05).** `TypeMetadata` 멤버 선택에서 `IPropertySymbol.IsIndexer` 제외 — 인덱서는 인수를 받아야 하므로 직렬화 멤버가 될 수 없다. 회귀 테스트(`GeneratorDiagnosticTests.공개_인덱서는_직렬화_멤버에서_제외된다`)는 수정 전 `this[` 부분열 발견으로 실패함을 확인 후 추가(진단 없음·생성 코드 컴파일 오류 없음·일반 멤버는 그대로 방출). 테스트 113→114. `Feature-Spec` F3 멤버 선택 규칙 명문화.

원본 발견 내용:

멤버 선택이 `m is IFieldSymbol || m is IPropertySymbol` + 비상속 + (ignore > include > public) 만 봤다. C# 인덱서는 `IsIndexer == true` 인 `IPropertySymbol` 이고 Roslyn `Name` 이 `this[]` 라, 공개 인덱서를 가진 메시지 타입은 `writer.WriteInt32(message.this[]);` 같은 **문법적으로 불가능한 코드**가 생성됐다. MSGPROT 진단도 없으므로 소비자 프로젝트가 CS0026·CS0443·CS1001·CS1002·CS1003 으로 붕괴하고, 원인이 생성기라는 단서도 주어지지 않았다. 그래프 수집측 `GetAllMembers` 도 동일한 `TypeMetadata.Members` 를 쓰므로 한 지점 수정으로 양쪽 경로 모두 차단됐다.

조치 방향: 멤버 선택에서 인덱서 제외 → 완료.

### KI-14. 중첩 역직렬화 재귀 깊이 무제한 → 작은 적대 프레임으로 원격 프로세스 사망 (해결)

**상태: 해결 (2026-09-05).** `MessageBufferReader` 가 중첩 깊이를 세는 단일 지점이 된다 — `EnterNestedObject()`(상한 도달 시 `InvalidDataException`)·`LeaveNestedObject()`(0 아래 클램프)·`MaxNestingDepth`·`NestingDepth`, 기본 상한 `DefaultMaxNestingDepth = 64`(`System.Text.Json`·`Newtonsoft.Json` 과 동일). 계상 지점은 재귀가 일어나는 세 갈래 전부: 생성기 방출 2곳(그래프 내부 중첩 객체 판독 `EmitInGraphMessageRead`·그래프 밖 메시지 위임 `EmitOutOfGraphMessageRead`)과 런타임 경유 지점 1곳(`MessageSerializer.DeserializeFromReader` — 타입 매개변수 멤버·외부 호출자 재귀, `try/finally`). 깊은 합법 그래프용 탈출구는 reader 단위 상한 `new MessageBufferReader(buffer, maxNestingDepth)`(0 이하 거부). 회귀 테스트 10개(케이스) — `NestingDepthTests`(상한+1 거부·상한 정확히 허용·object dispatch 경로·상한 상향 후 200단계 복호·넓은 그래프 무영향·기존 순환 참조 왕복·Enter/Leave 계약과 0 클램프·0 이하 상한 거부), 픽스처 `ChainMessage`(자기참조)·`WideChainMessage`(500개 형제 중첩). 테스트 114→124. `Feature-Spec` F3·F7, `Public-API` 중첩 깊이 계약 명문화.

원본 발견 내용 (실험 검증):

자기참조 메시지 멤버는 재귀로 판독되는데 깊이를 세는 곳이 없어, 깊이가 오직 프레임 크기 ÷ 최소 페이로드(자기참조 멤버 1개 기준 수준당 1바이트)로만 제한됐다. 저장소 밖 소비자 프로젝트로 재현한 결과 **5,005바이트 프레임(깊이 5,000)은 생존, 20,005바이트(깊이 20,000)는 `Stack overflow.` 로 프로세스 즉시 종료**(exit 127, try/catch 불가, 100,005바이트도 동일). 수준당 스택은 약 50~200바이트라 사망 임계는 런타임·스레드 스택 크기·빌드 구성에 따라 **내려간다**(스택이 작은 워커 스레드·Unity 는 더 얕은 프레임에서도 사망) — 20KB 안팎 프레임은 어떤 실전 프레임 상한으로도 걸러지지 않는다. 수정 후 같은 20,005·100,005바이트 프레임이 모두 `InvalidDataException` 으로 catch 되고 프로세스는 생존한다(동일 재현 프로그램, exit 0).

조치 방향: reader 단위 깊이 카운터 + 생성 코드·런타임 경유 지점 계상 → 완료. 생성 코드 쪽은 hot path 비용을 아끼려고 `try/finally` 를 쓰지 않는다 — 판독 중 예외로 `Leave` 가 생략되면 깊이는 **부풀기만** 하므로(0 아래 클램프) 가드는 실패 방향으로 안전하고, 예외가 난 reader 는 위치가 객체 중간이라 재사용 대상이 아니다(재사용하는 공개 경유 지점 `DeserializeFromReader` 만 `finally` 로 짝을 맞춘다). 값 타입(구조체) 중첩은 자기 포함이 컴파일러에 의해 불가능해 깊이가 정적 그래프로 제한되므로 계상하지 않는다.

### KI-24. 추상 메시지 타입 멤버 → 무진단 CS0117 생성 코드 (해결)

**상태: 해결 (2026-09-05).** 진단 승격이 아니라 **지원 승격**으로 처리 — 생성기가 멤버 타입의 `IsAbstract` 를 봐서 추상 메시지 타입 멤버에는 정적 위임 대신 **런타임 메시지 디스패치** 코드를 방출한다(쓰기 `MessageSerializer.SerializeToWriter(value, ref writer)`, 읽기 `(선언타입)MessageSerializer.DeserializeFromReader(ref reader)` — ADR-0003 의 타입 매개변수(`T`) 멤버와 동일 기제, 이미터 메서드도 `EmitRuntimeDispatchWrite`·`EmitRuntimeDispatchRead` 로 개명해 공용). 와이어에 구체 요소의 헤더(MessageId)가 실리므로 수신 측은 등록된 **구체 요소 타입과 파생 멤버를 그대로 복원**한다(다형 멤버 — 베이스 페이로드로 써서 파생 필드를 조용히 잃는 형태도 함께 차단). KI-14 의 깊이 가드가 이 경로(`DeserializeFromReader`)에도 걸려 있어 적대 중첩은 동일하게 거부된다. 회귀 테스트 5개 — 생성기 2개(추상 루트 멤버: 위임 미방출·디스패치 방출·MSGPROT 진단 0·컴파일 오류 0 / **역방향 가드**: 비공개 매개변수 없는 생성자로 그래프에서 빠진 *구체* 메시지 멤버는 정적 위임 유지) + 런타임 3개(`DispatchTests`: 구체 타입·베이스+파생 멤버 복원, `List<추상루트>` 다형 복원, null 왕복). 픽스처 `AbstractCommand`(126)·`StartCommand`(127)·`StopCommand`(128)·`CommandEnvelope`(129), Sandbox S13(3체크). 테스트 124→129, Sandbox 35→38 체크. `Feature-Spec` F3 메시지 타입 멤버 3경로·백레퍼런스 미추적 제약 명문화.

원본 발견 내용 (실험 검증):

`abstract [GroupRootMessage] AbstractEvent` 를 멤버로 가진 메시지는 **MSGPROT 진단이 하나도 없이** `global::TestNs.AbstractEvent.Serialize(message.Payload, ref writer);` · `result.Payload = global::TestNs.AbstractEvent.Deserialize(ref reader);` 를 방출해 소비자 빌드가 `CS0117` 2건으로 깨졌다(GeneratorDriver 실험으로 확인 — 생성기 진단 0건, 컴파일 오류 `CS0117: 'AbstractEvent'에 'Serialize' 정의가 없음` ×2). 추상 루트 + 구체 요소는 그룹 메시지의 **정상적인 선언 형태**(상속 전용 루트라 `MessageCodeGenerator` 가 의도적으로 생성을 건너뜀)라, 다형 페이로드를 담은 봉투 메시지라는 자연스러운 사용이 곧장 빌드 붕괴로 이어졌고 원인이 생성기라는 단서도 없었다. 구조적 원인: 그래프 수집(`SerializationGraph.IsSerializableObjectType`)이 추상 타입을 제외한 뒤 `IsMessageType` 분기로 빠지는데, 이 분기는 **위임 대상에 정적 멤버가 실제로 존재하는지 검증하지 않는** 사각지대였다(KI-18 은 비메시지 추상 페이로드만 MSGPROT006 으로 걸렀다). 회귀 테스트의 이빨도 확인 — 이미터 변경만 되돌리면 테스트 프로젝트가 생성 코드 CS0117 로 빌드 실패한다.

조치 방향: 추상 메시지 멤버를 런타임 디스패치로 연결 → 완료. 제약: 이 경로는 백레퍼런스를 추적하지 않으므로 추상 멤버를 통한 공유·순환 참조는 미지원(`T` 멤버·KI-9 와 동일 제약, `Feature-Spec` F3 명문화). 남은 꼬리: **구체** 베이스 타입 멤버(예: `EventBase` 타입 멤버에 `LoginEvent` 인스턴스)는 여전히 그래프 내부 페이로드 경로라 선언 타입 기준으로 직렬화되어 파생 필드가 유실된다 — 감사 원장 HIGH 항목(백레퍼런스 캐스트·파생 필드 유실)으로 별도 추적 중이며 와이어 형식 정책 결정이 필요해 이번 변경에서 분리했다.

### KI-25. 쓰기 측 중첩 재귀 무제한 → 송신 측 스택 오버플로·읽기 가드와의 비대칭 (해결)

**상태: 해결 (2026-09-05).** KI-14 의 읽기 가드와 **대칭**으로 writer 가 깊이를 센다 — `MessageBufferWriter.DefaultMaxNestingDepth`(= `MessageBufferReader.DefaultMaxNestingDepth` 를 참조해 구조적으로 동일 고정)·`EnterNestedObject()`(상한 도달 시 `InvalidOperationException`)·`LeaveNestedObject()`(0 아래 클램프)·`MaxNestingDepth`·`NestingDepth`, 상한 상향 탈출구 `MessageBufferWriter.Create(initialCapacity, maxNestingDepth)`(0 이하 `ArgumentOutOfRangeException`). 계상 지점은 읽기 측과 동일한 세 갈래: 생성기 방출 2곳(그래프 내부 중첩 객체 기록·그래프 밖 메시지 위임)과 런타임 경유 지점 `MessageSerializer.SerializeToWriter`(타입 매개변수·추상 메시지 멤버, 수동 구현 — `try/finally`). 예외 타입은 reader(`InvalidDataException` = 와이어 내용 불법)와 달리 `InvalidOperationException` — writer 쪽 한계 위반은 호출자 데이터·상태 문제라 `ThrowAdvanceBeyondCapacity` 와 같은 기조. 회귀 테스트 9개(케이스) — `NestingDepthTests` 쓰기 섹션(기본 상한 대칭 고정·깊은 체인 거부·**디스패치 멤버 순환 그래프 거부**·상한 상향 후 200단계 쓰기+맞춤 reader 로 되읽기·넓은 그래프 무영향·Enter/Leave 계약과 0 클램프·0 이하 상한 거부 ×3), 순환 픽스처 `WrapCommand`(130, 추상 루트 요소가 `CommandEnvelope` 을 되참조). 테스트 129→138(net8.0·net9.0), Sandbox 전체 통과, Release 빌드 경고 0·오류 0. `Feature-Spec` F3·F7, `Public-API` writer 중첩 깊이 계약 명문화.

원본 발견 내용 (실험 검증):

KI-14 는 읽기만 막았다. 쓰기 측은 깊이를 세는 곳이 아예 없어 두 가지가 **catch 불가한 스택 오버플로(프로세스 즉시 사망)** 로 끝났다 — 저장소 밖 소비자 프로젝트에서 확인: ① 송신자가 스스로 만든 **합법적 데이터**인 20,000노드 자기참조 연결 리스트를 `Serialize` 하면 사망(10,000노드는 생존 — 임계는 스택 크기·빌드 구성에 따라 내려감), ② 런타임 디스패치 멤버(KI-24 로 지원된 추상 메시지 멤버, 기존 `T` 멤버)로 돌아가는 **순환 그래프**(`batch.Head = new WrapCommand { Inner = batch }`)는 디스패치마다 `SerializeContext` 가 새로 만들어져 백레퍼런스가 동작하지 않아 재귀가 무한히 깊어지고 사망. 수정 후 세 경우(20,000·100,000노드·순환) 모두 `InvalidOperationException` 으로 catch 되고 프로세스는 생존한다(exit 0).

부수적으로 **비대칭**이 사라졌다: KI-14 이후 수신 측은 깊이 64 초과 프레임을 거부하는데 송신 측은 훨씬 깊은 프레임도 문제없이 만들어냈다 — 즉 성공적으로 직렬화된 메시지가 상대에게서 읽히지 않을 수 있었다. 양쪽 기본 상한을 하나의 상수로 묶어( writer 가 reader 상한을 참조) "기본 설정으로 쓴 것은 기본 설정으로 읽힌다" 가 구조적으로 보장되고, 송신 측이 **먼저** 실패하므로 원인 추적이 수신 측에서 끝나지 않는다.

조치 방향: reader 와 대칭인 writer 깊이 카운터 + 생성 코드·런타임 경유 지점 계상 → 완료. 생성 코드 쪽은 읽기 측과 동일하게 `try/finally` 없이 짝만 맞춘다(기록 중 예외 시 깊이는 부풀기만 하므로 실패 방향 안전, 부분 기록된 writer 는 재사용 대상 아님). 남은 꼬리: 디스패치 멤버의 백레퍼런스 미추적 자체는 여전하므로 **공유 참조**(순환이 아닌 동일 인스턴스 두 번 등장)는 디스패치 멤버를 통과하면 중복 기록되고 참조 동일성이 복원되지 않는다 — KI-9 제약으로 문서화됨, 가드는 그 경로가 프로세스를 죽이지 못하게 막는 것까지가 범위.

### KI-11. 등록 캐시 경쟁·조기 접근 → 영구 `TypeInitializationException` (해결)

**상태: 해결 (2026-09-05).** `SerializerCache<T>` 가 **영구 상태를 남기지 않도록** 세 가지를 바꿨다. ① cctor 는 더 이상 던지지 않는다 — 리플렉션으로 계약 멤버를 못 찾으면 null 로 남기고(`ResolveSerialize*` → `TryResolveSerialize*`, 공용 `TryCreateDelegate<TDelegate>`), 사용 지점(`Serialize<T>` 3개 오버로드)과 등록 지점(리플렉션 등록 3경로)이 `ThrowMissingSerialize<T>()` 로 명확히 보고한다(기존 `ThrowMissingDeserialize<T>` 와 대칭). ② 캐시 필드에서 `readonly` 를 벗기고 `PrefillSerializerCache` 가 `RunClassConstructor` 뒤 **캐시가 비어 있으면 직접 채워 복구**한다(CLR 은 cctor 를 다시 돌리지 않으므로 이 단계가 없으면 등록 전 조기 접근이 그 타입을 영구히 unusable 하게 만든다). ③ Prefill 홀더의 `IsSet` 을 `volatile`(release store)로 바꿔 델리게이트 6개 쓰기의 publication 을 플래그에 묶고, 복구 경로도 핫 경로가 먼저 읽는 `Serialize` 를 `Volatile.Write` 로 마지막에 발행한다 — 동시 cctor 가 `IsSet=true` 만 보고 델리게이트는 null 인 찢어진 상태를 고정하는 것 차단(x86 에선 관찰이 어렵지만 **Unity ARM 은 store-store 재배열이 가능**). 부수적으로 object dispatch 델리게이트가 캐시 필드를 `!` 로 직접 호출하던 것을 공용 `Serialize<T>`·`Deserialize<T>` 경유로 바꿔 null 델리게이트 NRE 대신 같은 안내 예외를 타게 했다. 회귀 테스트 4개(`SerializerCacheTests`) — 수정 전 **3개 실패 확인**(`System.TypeInitializationException` 16건 관측): 조기 접근이 영구 초기화 실패가 아니라 안내 예외를 던지고 두 번째 접근도 동일, 조기 접근 후 델리게이트 등록으로 제네릭·object dispatch 양쪽 복구, 계약 멤버 없는 타입의 리플렉션 등록은 등록 시점에 보고, + 역방향 가드(수동 구현 타입 리플렉션 등록·왕복 불변). 테스트 138→142(net8.0·net9.0), Sandbox 전체 통과, Release 빌드 경고 0·오류 0. `Feature-Spec` F6 등록 캐시 규약, `Public-API` 예외 형식 명문화.

원본 발견 내용:

`PrefillSerializerCache` 가 정적 필드 6개를 fence 없이 쓰고 `IsSet = true` 를 일반 쓰기로 발행해, 다른 스레드의 캐시 cctor 가 찢어진 상태(null·혼합 델리게이트)를 `readonly` 필드에 영구 고정할 수 있었다. 더 쉽게 밟히는 제2형태는 순서 문제였다 — 등록 전에 누군가 `SerializerCache<T>` 를 건드리면(예: 미등록 타입을 `Serialize<T>` 하려다 실패) cctor 가 리플렉션 경로로 돌고, 계약 멤버가 없는 타입에서는 `ResolveSerializeRefMethod` 가 **cctor 안에서** 던진다. CLR 은 정적 생성자 실패를 타입별로 영구 캐싱하므로 이후 델리게이트 등록이 성공해도 그 타입은 영원히 `TypeInitializationException` 이었다(실험: 수정 전 회귀 테스트에서 16건 관측). 즉 **일시적 순서 실수가 영구 고장으로 고정**되는 형태였고, 오류 메시지도 진짜 원인(등록 누락)이 아니라 CLR 내부 예외로 가려졌다.

조치 방향: 캐시를 오염 불가능하게 만들기 → 완료. 남은 꼬리(별도 추적): 첫 등록 시도가 `RegisterCore` 검증에서 거부되면 롤백이 Prefill·cctor 를 되돌리지 않아 `MessageId`·`HasId` 가 잔류한다(감사 원장 MEDIUM). 이번 복구 경로는 `Serialize is null` 일 때만 채우므로 **이미 등록된 타입의 중복 등록이 델리게이트를 조용히 갈아끼우는 불일치는 만들지 않는다**(중복 등록은 거부되지만 캐시·디스패치가 서로 다른 델리게이트를 가리키는 상태가 되지 않음).

### KI-4. 와이어 멤버 순서가 `Dictionary.Values` 열거에 의존 + 병합 로직 이중 정의 (해결)

**상태: 해결 (2026-09-05).** 페이로드 바이트 순서를 정하는 베이스 체인 멤버 병합이 `TypeMetadata.GetWireMembers`(신규 공용 정적) 한 곳으로 모였다 — 이전에는 **동일한 19줄이 이미터(`MessageSerializeCodeEmitter.GetAllMembers`)와 그래프(`SerializationGraph.GetAllMembers`)에 복제**되어 있었고 둘 다 `Dictionary<string, MemberMetadata>.Values` 를 반환했다. 새 구현은 `List<MemberMetadata>` + 이름→위치 인덱스로 순서를 **명시적으로** 만든다: 베이스 체인을 루트 쪽부터 내려오며 선언 순서로 추가, 같은 이름의 파생 멤버는 **베이스 위치를 유지한 채 심볼만 교체** — 와이어 형식이므로 기존 동작을 바이트 단위로 보존했다. 호출부 5곳(페이로드 기록·populate·고정 크기 합산·그래프 수집)이 모두 이 한 구현을 쓴다. 고정(characterization) 테스트 2개 — 루트의 베이스+파생+그림자 제거 순서(`BaseFirst, Shadowed, BaseLast, DerivedOwn`)와 그림자 멤버가 파생 타입(`WriteInt64`)으로 베이스 위치에 기록됨, 중첩 페이로드 헬퍼 순서(그래프 경로) — + **이빨 확인**: `GetWireMembers` 결과를 뒤집는 돌연변이를 넣자 두 테스트 모두 실패(2/2, net8.0·net9.0), 되돌려 144/144 통과. 테스트 142→144, Sandbox 전체 통과, Release 빌드 경고 0·오류 0. `Feature-Spec` F3 와이어 멤버 순서 규칙 명문화.

원본 발견 내용:

페이로드 멤버의 **바이트 순서**는 송수신이 반드시 일치해야 하는 와이어 형식의 일부인데, 그 순서가 두 곳에서 `Dictionary.Values` 열거에 얹혀 있었다. .NET `Dictionary` 는 제거가 없으면 삽입 순서로 열거하지만 그것은 **문서화된 규약이 아니라 구현 세부**다(공식 문서도 순서를 보장하지 않는다고 밝힌다). 즉 BCL 내부 변화 하나로 — 컴파일 오류도, 진단도, 테스트 실패도 없이 — 송신과 수신의 필드 배치가 어긋나 **조용한 데이터 손상**이 날 수 있는 형태였다. 게다가 동일 로직이 두 벌이라 한쪽만 고치면 이미터(실제 바이트 순서)와 그래프(도달 가능 타입 수집)가 다른 멤버 집합을 보는 구조 위험도 안고 있었다.

조치 방향: 명시적 순서 + 단일 구현 → 완료. 남은 꼬리(별도 추적): 한 타입 **내부**의 멤버 순서는 여전히 Roslyn `ISymbol.GetMembers()` 의 선언 순서다 — 여러 파일로 갈라진 `partial` 타입에서는 파트(구문 트리) 순서가 되므로, 두 피어가 같은 소스를 다른 파일 순서로 컴파일하면 이론상 배치가 갈릴 수 있다. 와이어 형식을 바꾸는 결정이 필요해 이번 변경에서 분리했다(감사 원장 등록).

### KI-26. 컬렉션 쓰기 루프의 멤버 재평가 → 게터 2N+2회·계산형 프로퍼티에서 프레임 자기모순 (해결)

**상태: 해결 (2026-09-05).** 컬렉션(배열·`List<T>`·`IList<T>`) 쓰기 템플릿 6변형 전부에서 멤버 표현식을 루프 밖으로 끌어올려 **한 번만** 평가한다 — `var __arr/__coll/__list = message.Member;` 뒤 null 판정도 그 로컬로 하고, 길이는 `__count`(또는 `CollectionsMarshal` 경로의 `__span.Length`)로 스냅샷한다. 이전 코드는 길이 접두(`Count`) + 루프 조건(`Count`, N+1회) + 인덱서(멤버 접근 N회)가 각자 `message.Member` 를 다시 평가했다. 회귀 테스트 4개 — 실행 검증 2개(`CollectionSnapshotTests`: 게터 호출 수가 `IList<int>` 3요소·`string[]` 2요소 각각 **정확히 1회**; 수정 전 8회·6회 + 빈 컬렉션·null 규약 보존) + 생성 텍스트 2개(`hasCollectionsMarshal` false/true Theory — `message.Values`·`Names`·`Tags` 가 생성 코드에 각각 1회만 등장, `if (message.X is null)` 미방출, 스냅샷 로컬 존재; false 는 Unity/netstandard2.1 경로라 이 저장소에서 실행되지 않아 텍스트로 고정). **이빨 확인**: 이미터만 되돌리자 신규 4개 중 3개 실패(행위 보존 테스트 1개는 양쪽 통과). 테스트 144→148(net8.0·net9.0), Sandbox 전체 통과, Release 빌드 경고 0·오류 0. `Feature-Spec` F7 컬렉션 스냅샷 계약 명문화.

원본 발견 내용:

두 가지 문제였다. ① **비용** — 요소마다 프로퍼티 게터와(`IList<T>` 는 인터페이스 `Count` 호출까지) 멤버 접근이 반복됐다. `CollectionsMarshal` 이 있는 타깃의 `List<T>` 는 이미 `AsSpan` 스냅샷으로 한 번만 평가했지만, 배열·`IList<T>`·그리고 `CollectionsMarshal` 이 없는 타깃(**Unity/netstandard2.1**)의 `List<T>` 는 루프가 멤버를 계속 다시 읽었다 — 이 저장소의 1차 타깃이 바로 그 경로다. ② **일관성(더 심각)** — 길이 접두와 요소를 *서로 다른 평가*에서 가져오므로, `public IList<int> Codes => Build();` 같은 계산형 프로퍼티에서는 길이와 요소가 다른 컬렉션 인스턴스에서 나와 프레임이 스스로 모순될 수 있고(수신 측에서 길이·내용 불일치), 게터가 두 번째 호출에서 null 을 돌려주면 `else` 분기 안에서 `NullReferenceException` 이 난다(TOCTOU). 스냅샷 방식은 `CollectionsMarshal` 경로가 이미 따르던 규약이라 6변형이 같은 의미가 됐다.

조치 방향: 멤버 표현식 1회 평가 + 로컬 스냅샷(null 판정 포함) → 완료. 부수 효과: 직렬화 중 컬렉션이 변하는 경우 프레임이 "한 순간 스냅샷"으로 일관되게 나온다(이전에는 길이와 요소가 다른 시점 값일 수 있었다). 직렬화 중 컬렉션 변경은 여전히 계약 위반이다.

### KI-27. `GenericMessage.ClassId` 상한 미검증 → 모듈 이니셜라이저에서 `TypeInitializationException` (해결)

**상태: 해결 (2026-09-05).** `ValidateConstructionEntries` 가 `classId == 0`(누락)만 거부하고 상한을 보지 않던 자리에 `classId > TypeMetadata.MaxMessageAttributeValue`(= `MessageWireFormat.MessageIdValueMask`, 2^24-1) 검증을 추가 — `MSGPROT008`(잘못된 GenericMessage 선언)로 컴파일 진단 승격하고 해당 구성의 등록 캐리어를 방출하지 않는다. ClassId 는 MessageId 와 같은 3바이트 와이어 슬롯(`GenericIdHeaderSize` 의 뒤 3바이트)에 담기므로 상한이 동일해야 맞다. 누락(0) 안내 메시지의 하드코딩 `16777215` 도 같은 상수로 교체. 런타임 검증(`RegisterGenericConstruction` 의 `ArgumentOutOfRangeException`, `GenericMessageAttribute.ClassId` 설정자의 `MessageAttributeRange.Validate`)은 방어층으로 유지. 회귀 테스트 4개(케이스) — `MSGPROT008_ClassId_상한_초과는_컴파일_진단으로_거부된다`(Theory: 2^24, `uint.MaxValue` → MSGPROT008 보고·`RegisterGenericConstruction<` 미방출·컴파일 오류 0) + 역방향 가드 `ClassId_경계값은_진단_없이_등록_코드를_생성한다`(Theory: 1, 2^24-1 → 진단 0·등록 코드 방출). **이빨 확인**: 수정 전 두 거부 케이스 실패(진단 없음), 경계 케이스는 통과(과잉 차단 아님). 테스트 148→152(net8.0·net9.0), Sandbox 전체 통과, Release 빌드 경고 0·오류 0. `Feature-Spec` F2 ClassId 범위·F5 MSGPROT008 사유 목록 갱신(진단 규칙 자체는 기존 것이라 분석기 릴리스 추적 파일 변경 없음).

원본 발견 내용:

`[GenericMessage(typeof(Box<int>), ClassId = 16777216)]` 처럼 24비트를 넘는 ClassId 는 생성기를 **진단 없이** 통과하고, 생성된 등록 캐리어가 `MessageSerializer.RegisterGenericConstruction<Box<int>>(16777216)` 을 `[ModuleInitializer]` 에서 호출한다. 런타임은 이미 상한을 검증하므로 예외 자체는 나지만 — 그것이 **모듈 이니셜라이저 안**이라 CLR 이 `TypeInitializationException` 으로 감싸 **모듈 로드 자체가 실패**한다. 즉 속성 값 오타 한 자리(`1677721` → `16777216`)의 증상이 "어셈블리 로드 실패"로 나타나고, 원인이 속성 값 범위라는 단서는 어디에도 없었다. 상한 검증이 없으면 값이 와이어에 3바이트로 잘려 들어갈 수 있다는 점도 문제다(그 경우 등록은 성공하지만 송수신 ClassId 가 어긋난다).

조치 방향: 컴파일 진단 승격 → 완료. `MSGPROT005`(ID 값 범위 초과)가 메시지 ID 에 대해 하는 일을 ClassId 에 대해 한 것이며, 새 진단 ID 를 늘이지 않고 기존 `MSGPROT008` 사유로 흡수했다.

### KI-28. `new` 수식어 판정이 베이스 속성만 봄 → 추상 루트 파생마다 CS0109 (해결)

**상태: 해결 (2026-09-05).** `GetStaticHidingModifier` 가 **베이스가 정적 계약을 실제로 방출하는지**를 보도록 바뀌었다 — 소스 베이스는 `MessageCodeGenerator.IsPartial`(MSGPROT001 로 거부되는 형태) && `IsConstructibleMessageType`(abstract·기본 생성 불가 = MSGPROT010, 그리고 상속 전용이라 생성을 건너뛰는 abstract 그룹 루트)일 때만 `new` 를 붙인다. 다른 어셈블리(메타데이터) 베이스는 구문 참조가 없어 partial 여부를 판정할 수 없으므로 그쪽 컴파일에서 생성됐다고 보고 **기존대로 `new` 유지**(내리면 CS0108/CS0114 로 역전). 부수적으로 `Define.cs`·`Method.cs` 에 **동일하게 복제**돼 있던 이 함수 2벌을 이미터 공용 헬퍼 하나로 통합(KI-4 와 같은 패턴)하고, 판정에 쓰는 `IsPartial`·`IsConstructibleMessageType` 을 `internal` 로 승격해 **생성 거부 조건과 `new` 조건이 한 사실 출처를 공유**하게 했다. 회귀 테스트 2개 — 추상 루트 파생은 `new static` 미방출(+진단 0·컴파일 오류 0), 역방향 가드로 구체 루트 파생은 `new static` 유지. **이빨 확인**: 판정을 옛 규칙(베이스 속성만)으로 되돌리는 돌연변이에서 추상 루트 테스트 실패(1/2 — 역방향 가드는 양쪽 통과), 복원 후 160/160. 효과: 클린 리빌드(`-t:Rebuild`) 기준 이 저장소 **CS0109 64건 → 0건**, 오류 0. 테스트 158→160(net8.0·net9.0), Sandbox 전체 통과.

원본 발견 내용:

`GetStaticHidingModifier` 는 베이스 타입의 **메시지 속성 유무**만으로 `new` 를 결정했다. 그런데 abstract `[GroupRootMessage]` 는 상속 전용이라 생성기가 정적 멤버를 아예 방출하지 않으므로(`MessageCodeGenerator` 의 의도된 건너뛰기), 그 파생 요소의 `new public static …` 는 가릴 대상이 없어 **CS0109**("멤버가 상속된 멤버를 숨기지 않습니다")가 된다. KI-24 로 추상 그룹 루트를 멤버 타입으로 쓰는 다형 패턴이 정상 지원되면서 이 형태가 일반화됐고, 클린 리빌드 기준 이 저장소에서만 64건이 쌓였다(파생 요소당 6개: `Serialize` ×2·`Deserialize` ×2·`MessageId`·`Initialize`). **증분 빌드에서는 컴파일이 건너뛰어져 경고가 전혀 보이지 않는다** — KI-12(RS2008)와 같은 함정이라, 이 저장소의 과거 "빌드 경고 0" 기록도 증분 빌드 기준이었다(실제 클린 리빌드에서는 64건). `TreatWarningsAsErrors` 를 켠 소비자에서는 이것이 곧 **빌드 실패**다.

조치 방향: 실제 방출 여부로 판정 + 복제 통합 → 완료. 남은 꼬리: 베이스가 MSGPROT002(컨테이닝 타입 non-partial)·MSGPROT003·MSGPROT005·MSGPROT007 로 거부되는 경우까지 `new` 판정에 반영하지는 않았다 — 그 경우들은 이미 컴파일 오류 진단이 떠서 소비자 빌드가 실패하므로 CS0109 하나가 더해져도 실질 영향이 없다. 검증 습관 교정: 빌드 경고 주장은 반드시 `-t:Rebuild` 로 한다.

### KI-3. 생성 로컬 이름 번호가 프로세스 전역 카운터 → 비결정적 생성 코드 (해결)

**상태: 해결 (2026-09-05).** `_uniqueIdCounter`(프로세스 전역 정적 + `Interlocked.Increment`)를 제거하고 번호를 **이미트 단위 상태**인 `EmitState.NextUniqueId()` 로 옮겼다 — `TryEmit` 이 타입별로 새 `EmitState` 를 만들므로 같은 입력은 항상 같은 번호(같은 텍스트)를 얻는다. 번호를 쓰는데 `EmitState` 를 받지 않던 헬퍼 6개(`EmitInGraphMessageWrite`·`Read`, `EmitOutOfGraphMessageWrite`·`Read`, `EmitRuntimeDispatchWrite`·`Read`)에 `state` 를 전달하도록 시그니처를 정리했고, 전역 상태라서 필요했던 `Interlocked`(와 `using System.Threading;`)도 함께 사라졌다 — 이미트는 타입별 단일 스레드라 잠금이 필요 없다. 회귀 테스트 1개: 한 컴파일에서 A→B→A→B 순서로 두 번씩 이미트해 **A₁==A₂·B₁==B₂** 를 비교(번호를 실제로 쓰는 로컬 `__item`·`__arr`·`__span`·`__refKind`·`__backId` 존재를 먼저 확인해 비교가 vacuous 해지지 않게 함). **이빨 확인**: 이미터·`EmitState` 만 되돌리자 이 테스트 실패(1/1), 복원 후 통과. 부수 증거: 클린 리빌드 2회의 생성 텍스트 전체 md5 동일(`e969267305af`). 테스트 160→161(net8.0·net9.0), Sandbox 전체 통과, 클린 리빌드 경고 0·오류 0. `Feature-Spec` F5 결정적 생성 텍스트 명문화.

원본 발견 내용:

생성 코드의 로컬 이름(`__item3`, `__coll1`, `__refKind7` …) 번호가 **컴파일러 프로세스 전역 정적 카운터**에서 나왔다. 그래서 같은 소스라도 그 프로세스가 이전에 몇 개의 메시지를 이미트했는지에 따라 번호가 밀려 **동일 입력 → 다른 출력**이 됐다. 결과는 세 가지: ① Roslyn 은 생성 출력을 비교해 "변화 없음"을 판정하는데 텍스트가 매번 달라져 **무관한 편집에도 생성 트리가 교체·재컴파일**됐다(증분 빌드 이점 상실 — KI-10 과 별개로 동작하는 손실), ② 같은 커밋에서도 생성 파일이 달라질 수 있어 빌드 재현성·CI diff 가 흔들렸고, ③ 생성 코드 감사(`EmitCompilerGeneratedFiles`) diff 가 의미 없이 흔들렸다. 전역 상태라 `Interlocked` 로 보호해야 했던 것 자체가 이 설계의 부산물이었다.

조치 방향: `EmitState` 로 이전(원장 권고 그대로) → 완료. 남은 꼬리: 헬퍼 메서드 **방출 순서**는 여전히 그래프 `_lookup.Values` 반복에 얹혀 있다(감사 원장 LOW) — 수집 순서 자체는 KI-4 에서 명시화한 멤버 순서를 따르므로 같은 입력이면 안정적이지만, `Dictionary` 열거라는 BCL 구현 세부에 의존하는 형태는 KI-4 와 같은 종류의 잔존 위험이다.

## 잠재 결함 (코드 리뷰)

| 번호 | 위치 | 내용 |
| ---- | ---- | ---- |
| KI-5 | 생성 `Deserialize(ref reader)` | 헤더·MessageId 를 검증하지 않고 건너뜀 — 다른 타입 바이트를 먹이면 조용히 재해석 (성능 트레이드오프, 문서화 필요) |
| KI-7 | `MessageBufferWriter.PatchInt32` | 오프셋 경계 검증 없음. `Grow`의 `Length * 2`는 1GB 부근 int 오버플로 가능 |
| KI-8 | `MessageCategoryAttribute` | 범위 밖 카테고리 값이 `& 0x0F` 로 조용히 마스킹 |
| KI-9 | 그래프 밖 메시지 위임·런타임 디스패치 멤버 (`EmitOutOfGraphMessage*`·`EmitRuntimeDispatch*`) | 위임·디스패치 시 새 `SerializeContext` — 경계 넘는 공유 참조 복원 불가(중복 기록). 경계 넘는 순환 참조는 KI-25 writer 깊이 가드가 `InvalidOperationException` 으로 막는다(스택 오버플로 아님). 남은 제약 문서화 필요 |
| KI-10 | 증분 파이프라인 | `Collect` 결과 `ImmutableArray`는 참조 동등성이라 후보 캐시가 매번 무효 — 편집마다 전체 재방출 (성능) |

## 관련

- [Feature-Spec](../02-Architecture/Feature-Spec.md)
- [CONTEXT](../00-AI/CONTEXT.md)
