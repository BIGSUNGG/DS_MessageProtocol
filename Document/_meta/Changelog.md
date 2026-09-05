# Changelog

문서 변경 기록. 최신이 위.

## 2026-09-05

- KI-24 해결 — 추상 메시지 타입 멤버가 **무진단 CS0117** 생성 코드를 내던 결함: `abstract [GroupRootMessage]`(상속 전용이라 생성기가 의도적으로 정적 멤버를 만들지 않음)를 멤버로 쓰면 `AbstractEvent.Serialize(…)` 위임 코드가 방출되어 소비자 빌드가 깨졌다. 생성기가 멤버 타입 `IsAbstract` 를 봐서 추상 메시지 멤버에는 정적 위임 대신 **런타임 메시지 디스패치**(`SerializeToWriter`·`DeserializeFromReader`, `T` 멤버와 공용 — 이미터 `EmitRuntimeDispatch*` 개명)를 방출하므로, 구체 요소의 헤더가 실려 수신 측이 **구체 타입과 파생 멤버를 그대로 복원**한다(다형 멤버 지원 + 베이스 페이로드로 쓸 때의 파생 필드 유실 차단). 실험 검증: GeneratorDriver 로 수정 전 생성기 진단 0건·`CS0117` 2건 확인, 이미터만 되돌려 테스트 빌드가 깨지는 것(회귀 테스트 이빨)도 확인. 회귀 테스트 5개(생성기 2 — 역방향 가드 포함, 런타임 3)·픽스처 `AbstractCommand` 계열, Sandbox S13(3체크). 테스트 124→129(net8.0·net9.0), Sandbox 전체 통과(38 체크), Release 빌드 경고 0·오류 0. `Known-Issues` KI-24 해결 섹션 추가, `Feature-Spec` F3 메시지 타입 멤버 3경로(그래프 내부 · 그래프 밖 구체 위임 · 추상 디스패치)와 백레퍼런스 미추적 제약 명문화. 남은 꼬리(구체 베이스 타입 멤버의 파생 필드 유실)는 원장 HIGH 로 별도 추적.
- KI-14 해결 — 중첩 객체 역직렬화 재귀에 깊이 상한 도입: `MessageBufferReader` 가 `EnterNestedObject`·`LeaveNestedObject`·`MaxNestingDepth`·`NestingDepth`(기본 상한 `DefaultMaxNestingDepth = 64`)를 노출하고, 생성기가 재귀 지점 2곳(그래프 내부 중첩 객체 판독·그래프 밖 메시지 위임)에서 쌍을 방출하며, 런타임 경유 지점 `MessageSerializer.DeserializeFromReader`(타입 매개변수 멤버·외부 호출자 재귀)는 `try/finally` 로 자체 계상 — 상한 초과 시 `InvalidDataException`. 깊은 합법 그래프용 탈출구는 reader 단위 상한 `new MessageBufferReader(buffer, maxNestingDepth)`. 실험 검증: 소비자 프로젝트에서 자기참조 멤버 1개 메시지의 **20,005바이트 적대 프레임이 `Stack overflow.`(exit 127, catch 불가)로 프로세스를 죽이던 것**이 수정 후 catch 가능한 `InvalidDataException` 거부로 바뀌었다(5,005바이트는 수정 전에도 생존 — 임계는 스택 크기·빌드 구성에 따라 내려감). 회귀 테스트 10개(케이스, `NestingDepthTests` + 픽스처 `ChainMessage`·`WideChainMessage`). 테스트 114→124(net8.0·net9.0), Sandbox 전체 통과, Release 빌드 경고 0·오류 0. `Known-Issues` KI-14 해결 섹션 승격(잠재 결함 표에서 이동), `Feature-Spec` F3 깊이 가드·F7 비용, `Public-API` 중첩 깊이 계약 명문화.
- KI-6 해결 — `MessageBufferReader.ReadString` 음수 길이 접두사 검증: `-1` 만 null 로 복호하고 나머지 음수(`-2`…`int.MinValue`)는 `InvalidDataException` 거부(KI-15·KI-20 와이어 무결성 엄격 기조 동일, 경계 `EndOfStreamException` 과 구분). 회귀 테스트 4개(`WireAndBufferTests`). 테스트 93→97. `Known-Issues` KI-6 해결 섹션 승격(잠재 결함 표에서 이동), `Feature-Spec` F3 문자열 길이 접두 규약 명문화.
- KI-21 해결 — `MessageBufferReader.Skip`·`MessageBufferWriter.Advance` 음수 `count` 거부(`ArgumentOutOfRangeException`, 공개 경계에서 검증) — forward-only 규약 강제. 되돌림으로 소비한 바이트 재읽기·기록한 페이로드 덮어쓰기가 불가능해졌고, 경계 위반(`EndOfStreamException`)과 호출자 프로그래밍 오류가 분리됐다. 회귀 테스트 7개(케이스). 테스트 97→104. `Known-Issues` KI-21 해결 섹션 승격, `Public-API` 버퍼 I/O 계약 명문화.
- KI-22 해결 — `MessageBufferWriter.WriteString` 용량 산술 오버플로 제거: `4 + GetMaxByteCount(int)`(약 7.15억 자에서 음수 오버플로 → `EnsureCapacity` 증설 누락 → `GetBytes` 내부 실패) 대신 long 산술 내부 헬퍼 `GetStringBufferRequirement` 사용, 버퍼(배열) 상한 `0X7FEFFFFF` 초과 문자열은 명확한 `ArgumentException` 으로 거부. 임계값 미만 동작 불변(회귀 테스트로 고정). 회귀 테스트 9개(케이스). 테스트 104→113. `Core` 에 `InternalsVisibleTo(MessageProtocol.Tests)` 추가, `Known-Issues` KI-22 해결 섹션 승격, `Public-API` 버퍼 상한 계약 명문화.
- KI-12 해결 — 분석기 릴리스 추적 도입: `Source/MessageProtocol.CodeGenerator/AnalyzerReleases.Shipped.md`(릴리스 2.0 = `v2.0.0` 태그 기준 MSGPROT001~008, 007 만 Warning) · `AnalyzerReleases.Unshipped.md`(태그 이후 추가된 MSGPROT010·011) 추가 — SDK 자동 `AdditionalFiles` 포함에 맡기고 csproj 중복 선언은 제거. `dotnet build MessageProtocol.sln -t:Rebuild` 클린 리빌드 경고 16개(RS2008)→0개, 오류 0개. 테스트 113개(net8.0·net9.0)·Sandbox 전체 통과로 동작 불변 확인. `Known-Issues` KI-12 해결 섹션 승격(구분 행 공백 시 RS2007·중복 `AdditionalFiles` 함정 기록), `Packages` 진단 규칙 추가 시 추적 파일 갱신 규약 명문화.
- KI-23 해결 — 공개 인덱서가 직렬화 멤버로 선택돼 `message.this[]` 형태 문법 오류 코드가 무진단 생성되던 결함: `TypeMetadata` 멤버 선택에 `IPropertySymbol.IsIndexer` 제외 추가(그래프 수집측 `GetAllMembers` 도 같은 `Members` 를 써서 일괄 차단). 회귀 테스트는 수정 전 실패(생성 코드에 `this[` 존재)를 확인 후 통과. 테스트 113→114. `Known-Issues` KI-23 해결 섹션 추가, `Feature-Spec` F3 멤버 선택 규칙 명문화.
- 백로그 소진 후 신규 감사 패스 수행(생성기·Core 런타임·문서 드리프트 3개 영역 병렬) — HIGH 6건·MEDIUM 11건·LOW 9건 신규 발견. KI-3·KI-4·KI-11(2형태)·KI-14 는 현재 코드에서 잔존 재확인, 그 외 신규 결함(추상 그룹 루트 멤버 CS0117·백레퍼런스 InvalidCastException·ClassId 상한 미검증·등록 경쟁·Public-API 제네릭 표면 누락·죽은 공개 API 등)은 감사 루프 원장에 미해결로 등록.

## 2026-09-04

- KI-16 해결 — 동일 제네릭 페이로드의 두 구성이 한 그래프에 공존할 때 헬퍼 메서드 이름 충돌(소비자 CS0111 컴파일 실패). `SerializationGraph` 헬퍼 접미사에 타입 인자·중첩 타입 체인 반영 + 그래프 단위 유일성 구분자. 회귀 픽스처·왕복 테스트로 수정 전 재현 검증. 테스트 83→84. `Known-Issues` KI-17~KI-22 추가(감사 발견 미해결: `CollectionsMarshal` 미지원 벌크 가드 누락·생성 불가 페이로드 무진단·캐리어 접미사 충돌·UTF8 관대 폴백·음수 Skip/Advance·WriteString 오버플로).
- KI-17 해결 — `CollectionsMarshal` 미지원 타깃의 `List<T>` 벌크 판독 분기 할당 전 가드 격상(`개수 ≤ Remaining` → `개수×요소크기 ≤ Remaining`, long 산술, `EmitListRead`). `InternalsVisibleTo` 추가 후 `TryEmit(hasCollectionsMarshal: false)` 직접 구동 회귀 테스트(약한 가드로 역전 시 실패 검증). 테스트 84→85. `Known-Issues` KI-17 해결 승격.
- KI-18 해결 — 생성 불가 페이로드 진단 승격: 루트 메시지 추상·매개변수 없는 생성자 없음 → `MSGPROT010`, 중첩 페이로드 기본 생성 불가(추상·포지셔널 레코드) → 그래프 제외 후 멤버 단위 `MSGPROT006`, 대입 불가 멤버(get-only·init-only·읽기전용 필드) → `MSGPROT011` (`SerializationGraph.IsSerializableObjectType`·`MessageCodeGenerator.IsConstructibleMessageType`·`Member.IsDeserializableMember`, `EmitState` 사유 열거 확장). 회귀 테스트 5개. 테스트 85→90. `Feature-Spec` F5 진단 목록 갱신.
- KI-19 해결 — 제네릭 구성 등록 캐리어 접미사 충돌: `MakeCarrierSuffix` 제거 후 KI-16과 동일 전략의 공용 `SymbolNaming.MakeUniqueSuffix`(네임스페이스·중첩 체인·제네릭 인자 + 사용 접미사 집합 구분자)로 교체 — 그래프 헬퍼·캐리어 이름 체계 단일화. 동명 중첩 캐리어 회귀 테스트(수정 전 충돌 검증). 테스트 90→91.
- KI-20 해결 — 문자열 엄격 UTF-8 정책 확정: `WriteString`·`ReadString` 이 엄격 폴백(`EncoderFallback.ExceptionFallback`·`DecoderFallback.ExceptionFallback`) 사용 — 고립 서로게이트 쓰기 인코딩 예외 거부, 무효 UTF-8 읽기 `InvalidDataException` 거부(KI-15 와이어 무결성 기조 동일). 회귀 테스트 2개. 테스트 91→93. `Feature-Spec` F3 엄격 UTF-8 규칙 명문화.

## 2026-09-01

- KI-15 해결 — `MessageBufferReader.ReadDecimal` flags 검증(스케일 >28·예약 비트 → `InvalidDataException`), 무효 스케일 `decimal`이 덧셈·뺄셈에서 일으키던 원격 프로세스 크래시 경로 차단. 회귀 테스트 3개(`BufferIOTests`). 테스트 80→83. `Known-Issues` KI-15 해결 섹션 승격, `Feature-Spec` F3 decimal 검증 명문화.
- `Known-Issues` KI-14·KI-15 추가 — 온라인 게임 공격 표면 검토: 중첩 객체 재귀 판독 깊이 미제한(스택 오버플로 가능성), `ReadDecimal` 무검증 비트 재해석. 엔진 단 권고(프레임 크기 상한·레이트 제한·핸들러 인가)는 패키지 범위 밖.
- KI-15 심각도 격상(실험 검증) — 공격자 제어 `decimal` 플래그(스케일 ≥78)가 덧셈·뺄셈에서 `DecCalc` 스택 버퍼 오버플로 유발, **SIGSEGV 프로세스 종료(try/catch 불가)**. 스케일 ≤77은 안전, 곱·비교는 생존. 원격 킬 스위치 수준의 DoS.
- KI-13 해결 — 컬렉션 길이·개수 접두사 할당 전 남은 버퍼 가드: `MessageSerializeCodeEmitter.Member`의 `EmitArrayRead`·`EmitListRead` 5 변형 전부(고정 크기 `길이×요소크기 ≤ Remaining` 정확 검증, 가변 크기 `개수 ≤ Remaining` 상한, 초과 시 `EndOfStreamException`, 정책 옵션 없음). 회귀 테스트 4개 신규(악성 길이 3 + 정상 왕복 1). 테스트 76→80. `Feature-Spec` F3 컬렉션 가드 명문화, `Known-Issues` KI-13 해결 처리.
- `Known-Issues` KI-13 추가 — 프로덕션 적합성 검토: 생성 역직렬화의 길이 접두사가 남은 바이트 검증 전 컬렉션 할당 → 불신 피어 OOM DoS 가능성, 채용 전 상한 가드 권고.

## 2026-08-31

- 제네릭 구성 선언 속성 통합 ([ADR-0005](../05-Decisions/ADR-0005-Generic-Attribute-Unification.md), ADR-0004 선언 모델 대체): `[GenericMessage(typeof(닫힌 구성), ClassId)]` 단일 속성(선언부·캐리어 무관), `GenericConstructionAttribute` 제거, 제네릭+스탠드얼론=항상 제네릭 와이어(구성 선언 필수, 미선언 직렬화 예외+안내), 동일 컴파일 내 구성 중복 선언 컴파일 진단 승격, 미바운드 제네릭 거부, `MSGPROT009` 삭제. 테스트 74→76, 픽스처·Sandbox 통합 문법 이전. `Feature-Spec` F2·F5, `GLOSSARY` 동기화.
- 제네릭 구성 분산 선언 추가 (ADR-0004 보완): `[GenericConstruction(typeof(구성), ClassId)]` 캐리어 속성 — 선언부 수정 없이 타 파일/프로젝트에서 구성 추가, 생성기가 등록 클래스 출력. ClassId 보관을 내부 필드에서 런타임 레지스트리(`GetGenericClassId<T>`)로 이전(타 어셈블리 캐리어 지원), 테스트 프로젝트에 `EmitCompilerGeneratedFiles` 활성화(생성 코드 감사). 테스트 70→74, Sandbox S12 추가. `Feature-Spec` F2·F5, `GLOSSARY` 동기화.
- 제네릭 와이어 재설계 ([ADR-0004](../05-Decisions/ADR-0004-Generic-Message-Wire-Format.md), ADR-0003 대체): `[GenericMessage(typeof(...), ClassId)]` 구성 선언 속성, 헤더 플래그 Generic(0) + MessageId 뒤 구성 클래스 ID 24비트 와이어(`GenericIdHeaderSize = 7`), (MessageId, ClassId) 디스패치·모듈 로드 자동 등록(송수신 무설정), 구성 공존·다중 타입 매개변수, `MSGPROT008`·`MSGPROT009` 진단. 테스트 63→70, Sandbox S11 추가. `Feature-Spec` F1·F2·F5·범위 밖, `GLOSSARY`, `Known-Issues` 동기화.
- 제네릭 직렬화 수신 측 제약 명문화: 지연 등록은 `Serialize(object)` 경로 한정 — 역직렬화만 하는 수신 프로세스는 닫힌 구성 명시적 등록 필요 (`ADR-0003`·`Feature-Spec` 갱신).
- 제네릭 메시지 직렬화 구현 ([ADR-0003](../05-Decisions/ADR-0003-Generic-Message-Serialization.md), Known-Issues KI-1 해결): 타입 매개변수 유지 생성(partial arity·헬퍼 이름 백틱 변환), `T` 멤버 런타임 메시지 디스패치, 제네릭 타입 자동 등록 미생성(닫힌 구성 지연/수동 등록). 회귀 테스트 5개 추가(테스트 58→63), Sandbox S10 시나리오(제네릭 round-trip·T 컬렉션·object dispatch) 추가. `ADR-0002` superseded 처리, `Feature-Spec` F5·범위 밖 동기화.
- `MSGPROT007` 메시지 속성 중복 경고 진단 추가 (`Source/MessageProtocol.CodeGenerator`) — 한 타입에 메시지 속성 2개 이상이면 경고·생성 건너뜀, 회귀 테스트 2개 (테스트 56→58). `Feature-Spec` F5 진단 목록 갱신.
- `05-Decisions/ADR-0002-Generic-Message-Serialization.md` 신규 — 제네릭 메시지 직렬화 추후 지원 연기 결정. `Feature-Spec` 범위 밖·`Known-Issues` KI-1 유예·KI-2 해결 동기화.
- `06-Troubleshooting/Known-Issues.md` 신규 — 코드 리뷰 발견 문제점: 확인 버그 2건(제네릭 메시지 무진단 깨진 생성, 속성 충돌 무진단·런타임 실패) + 잠재 결함 10건.

- `README.md` v2 동기화: 런타임 타깃에 net6.0 명시, 생성기 netstandard2.0 표기, 속성 네임스페이스 안내, 저장소 구조 표 추가.
- 리뷰 라운드 2 수정: 생성기 힌트 이름 중첩 구분자를 `+`로 변경(네임스페이스 점과 충돌 제거 + 회귀 테스트), `generated-out` 컴파일 제외 가드를 `DefaultItemExcludes`로 교체(`Compile Remove`는 기본 글롭 이전 평가라 무효).
- 리뷰 라운드 1 수정: 생성기 힌트 이름 충돌 수정(네임스페이스 포함 유일 힌트 + 회귀 테스트), 벤치마크 InProcess 도구 체인 전환(실행 가능), `MessageWireFormat` 상수 2개 복원(`NullSizedPayloadLength`·`DefaultStreamCapacity`), `generated-out` 정리·컴파일 제외 가드, vault 문서 동기화(`00-AI/CONTEXT·GLOSSARY·CONVENTIONS`, `01-Overview/Home`, `02-Architecture/Overview`, `03-Reference/Public-API·Packages`).
- 재작성 구현 완료: `Source/` (Core·CodeGenerator·메타), `Test/` (Tests 54·Benchmarks), `Sandbox/` 인수 조건(전체 통과), 패키지 3종 2.0.0 pack 검증.
- `02-Architecture/Feature-Spec.md` status → approved, "Legacy 대비 재작성 변경점" 추가.
- `05-Decisions/ADR-0001-Rewrite-Bootstrap.md` 신규 — 네임스페이스 유지·테스트 신규 작성·예시 우선·패키지 2.0.0 결정.
- `02-Architecture/Feature-Spec.md` 신규 — 재작성 프로젝트 지원 기능 스펙 (Legacy 기능 패리티 기준).
