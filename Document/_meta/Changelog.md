# Changelog

문서 변경 기록. 최신이 위.

## 2026-09-01

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
