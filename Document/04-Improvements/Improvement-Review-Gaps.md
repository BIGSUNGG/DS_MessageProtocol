---
project: DS_MessageProtocol
type: analysis
status: draft
tags: [review, gap-analysis, cross-validation, fresh-review]
updated: 2026-09-08
---

# Improvement — 교차 검증 및 미발굴 재심층 (Review Gaps)

> 1차 웨이브 3문서(Feature F1–F12 · Security SEC-01~08 · Performance PERF-01~09, 총 29항목)를 실제 코드와
> 대조해 **적대적으로 재검토**하고, 같은 문서들이 커버리지가 얕았던 영역(생성기 진단 커버리지 · 와이어 포맷
> 계약 · MessageSerializer 공개 API 계약 · reader/writer 계약-구현 괴리 · Known-Issues 원장 미반영)을
> 직접 심층 조사해 **신규 항목 9건(RG-01~09)**을 발굴했다. 기존 29항목과의 중복은 없다.

## 조사 범위·방법

- **읽은 문서**: `00-AI/CONTEXT.md` · `GLOSSARY.md` · `02-Architecture/Feature-Spec.md` · `03-Reference/Public-API.md` · `04-Improvements/` 3개 문서 · `06-Troubleshooting/Known-Issues.md` (전체).
- **읽은 소스** (obj/bin 제외 전수): `Source/Shared/` 2파일, `Source/MessageProtocol.Core/` 12파일(Serialize 9 + 속성 3 + polyfill·compat), `Source/MessageProtocol.CodeGenerator/` 16파일(파이프라인·GenericConstruction·TypeMetadataValidator·TypeMetadata·SerializationGraph·emitter 5분할·DiagnosticDescriptors·SymbolNaming·EmitState).
- **대조한 테스트**: `Test/MessageProtocol.Tests/` (DeserializerFuzzTests · CollectionGuardTests · WireAndBufferTests · GeneratorDiagnosticTests 등 — 발견이 이미 테스트로 커버됐는지 확인 목적).
- **검증 방법**: 각 선행 문서의 `파일:라인` 인용을 현재 작업 트리에서 재확인(grep -n)했고, 신규 주장은 코드 분기 조건을 직접 읽어 확정했다. 추측이 섞인 항목은 본문에 명시했다.

---

## 1. 정정 — 기존 29개 항목 검증 결과

**요약: 29개 항목 중 "본질이 틀렸거나 이미 해결된 항목과 중복"인 것은 0건.** 모든 항목의 기술적 주장은
현재 코드에서 재확인됐다. 찾은 것은 (a) 같은 날(2026-09-08) KI-33/38/39/40/41 및 구조 감사 수정으로 밀린
**라인 인용 드리프트 다수**, (b) **수치 만료 1건**(F11), (c) SEC-01 증폭 배율의 산술 재확인(정확했음)이다.

### 1.1 근거 위치 드리프트 (본질은 모두 유효)

| 항목 | 문서 인용 | 현재 위치 | 비고 |
| ---- | --------- | --------- | ---- |
| SEC-01 | `Member.cs:713 부근 PrimitiveWireTable decimal BulkSize=-1` | 표 전체 `:705-721`, decimal `:720` | "부근"이라 표현해 본질 유효 |
| SEC-06 | `Method.cs:157,161` (생성 Deserialize 잔여 바이트 무검사) | `EmitDeserialize` 본문 `:110-166`으로 이동 | **주장 자체는 재확인 유효** — 생성 `Deserialize`는 판독 후 `reader.Remaining` 검사 없이 반환 |
| SEC-07 | `Member.cs:15` | `:20` (`memberAccess = $"{instanceExpression}.{member.Name}"`) | 무이스케이프 문제는 그대로 존재 |
| PERF-09 | Writer `:298` / Reader `:252` | Writer `:315` / Reader `:226` | 필드 2줄 변경 제안은 유효 |
| F1 | `MessageSerializer.Deserialize.cs:52-76` | `:33-60` | Deserialize<T> 오버로드군 |
| F3 | `MessageSerializer.cs:286-319` (ValidateRegistration) | `:322-358` | F9의 동일 인용도 같은 드리프트 |
| F4 | `MessageBufferReader.cs:168-231` | 깊이 `:57-75` · decimal·UTF-8 `:145-190`으로 분산 | 거부 지점 나열의 본질 유효 |
| F6 | `MessageBufferWriter.cs:126` / `PooledBuffer.cs:22-36` | `:135` (Grow 내 Rent) / `:24-41` (Owner) | `:50`(Create 내 Rent)·`PooledBuffer.cs:28`(Return)은 정확 |
| F7 | `MessageBufferWriter.cs:35-51` / `:313-331` | `:37-53` / `:323-334` | `:91-99`(GetSpan)·`:336-361`(ToPooledBuffer/Dispose)은 정확 |
| F10 | `MessageSerializer.cs:231-262` (RegisterType) | `:213-249` | `MakeGenericMethod` :241은 **정확** (SEC-08) |
| F12 | `PooledBuffer.cs:56-59` (Span) | `:61-63` | |

정확했던 인용(드리프트 없음): SEC-02 (`Method.cs:137` · `Deserialize.cs:111,120,150,155` — 전부 일치),
SEC-03 (`:105,144,180` · `:39,58,86` — 전부 일치), SEC-04 (`Writer.cs:142,358` · `PooledBuffer.cs:28` — 전부
일치), PERF-01 (`Method.cs:73` · `MessageSerializer.cs:23-26`), PERF-02 (`Serialize.cs:44,75,92`),
PERF-03 (`Member.cs:708`), PERF-05 (`Method.cs:63-68` · `:126-134`), PERF-04 (`MessageCodeGenerator.cs:44-63`),
F2 (`:84-137` · `:170-186`), F3 (`MessageSerializer.cs:16-20` · `Deserialize.cs:15`).

### 1.2 수치 만료

- **F11** "이 저장소의 검증 자산(테스트 205개·Sandbox 38 시나리오…)" — KI-40(2026-09-08) 이후 원장 기준
  **테스트 244개 · Sandbox 42 시나리오**다(`Known-Issues` KI-40, Feature-Spec F10). 제안 자체(테스트 키트)는
  유효하나 인용 수치가 만료됐다.

### 1.3 과장/축소 판정

- **SEC-01 증폭 배율 산술 재확인 — 정확.** 비불크 가드(`Member.cs:477,661`)는 요소당 와이어 최소 1바이트
  가정인데 `List<decimal>` 선할당은 16B/요소(16:1), `string[]`는 참조 8B vs 빈 문자열 와이어 4B(~2:1),
  그래프 내부 참조형은 참조 태그 1B vs 참조 8B(~8:1) — 문서 수치와 일치했다.
- **PERF-01/PERF-04** — "추정"·"측정 근거로 연기"라는 자기 한정이 명시돼 있어 과장 없음. KI-10 측정 기록과도 정합.
- 축소(실제보다 심각성을 낮게 쓴) 항목도 발견하지 못했다.

### 1.4 이미 해결된 항목과의 중복

- 29개 항목 전수를 `Known-Issues` KI-1~41 해결 목록과 대조 — **해결된 항목의 재보고 없음.**
  (SEC-03은 KI-41이 최상위 NonId 플래그만 교정했을 뿐 잘린 헤더의 `ArgumentException`은 현재 코드에 그대로
  존재(`Deserialize.cs:105,144,180`) — 미해결 확인.)

---

## 2. 신규 발견 (RG-01 ~ RG-09)

우선순위 요약: **P2 = RG-01~05, P3 = RG-06~09.** 전부 와이어 형식 무변경으로 완화 가능하다.

### RG-01. 중첩 디스패치(`DeserializeFromReader`)의 플래그 검증 부재 + 최상위 가드의 혼합 플래그 누수 — 동일 입력이 경로에 따라 다른 예외 유형

- **근거 위치**: `Source/MessageProtocol.Core/Serialize/MessageSerializer.Deserialize.cs:91-96` (최상위 가드
  `(flags & StandaloneOrGroup) == 0` 만 검사), `:120`·`:155` (`KeyNotFoundException`), `:128-166`
  (`DeserializeFromReader` — **플래그 유효성 검사가 아예 없음**). 퍼저(`Test/DeserializerFuzzTests.cs:130,146`)
  는 최상위 `Deserialize`와 제네릭 타입 경로만 구동 — **중첩 디스패치 경로는 퍼저 관측 범위 밖**.
- **설명**: KI-41이 최상위 object dispatch에 NonId 플래프 프레임의 `InvalidDataException` 거부를 세웠지만
  ① 그 가드는 "Standalone/Group 비트가 하나라도 있으면 통과"라서 **혼합 비트**(NonId|Standalone=3,
  NonId|GroupRoot=5, Standalone|GroupRoot=6 등)를 가진 위조 헤더는 가드를 통과해 `KeyNotFoundException`
  ("not registered")으로 분류되고, ② `DeserializeFromReader`(타입 매개변수·추상 멤버·수동 구현의 중첩
  디스패치)에는 가드 자체가 없어 **같은 비합법 헤더가 `KeyNotFoundException`으로 보고**된다. 최상위에서
  `InvalidDataException`인 입력이 중첩에서는 `KeyNotFoundException` — KI-41/SEC-03이 해소하려던
  "경로별 예외 유형 불일치"가 그대로 남은 형태다.
- **영향**: 신뢰 경계 모니터링의 거부 분류 오염(악성 트래픽이 버전 불일치 버킷에 합류), 퍼저 불변식
  ①("거부는 깨끗한 유형")의 관측 공백. 메모리 안전 영향은 없음(추측 아님 — 조회 실패 후 예외가 전부).
- **구체적 제안**: (a) `DeserializeFromReader` 진입에 최상위와 동일한 플래그 가드(`InvalidDataException`) 추가,
  (b) 최상위 가드를 `flags` 전체 비트 정밀 검사(허용: Generic(0), NonId(1), Standalone(2), GroupRoot(4),
  GroupElement(8) 단독)로 강화, (c) `DeserializerFuzzTests` 진입 경로에 `DeserializeFromReader` 추가.
- **우선순위**: P2 / **난이도**: 하

### RG-02. 컬렉션 길이 접두의 음수 전체가 null로 복호 — KI-6 엄격 정책이 문자열에만 적용됨

- **근거 위치**: `Source/MessageProtocol.CodeGenerator/Generate/MessageSerializeCodeEmitter.Member.cs`
  생성 판독 **5변형 전부**가 `if (__len{uid} < 0) { target = null; }` — 배열 불크 `:449-454`, 배열 비불크
  `:470-475`, List CollectionsMarshal `:610-615`, List 불크 `:631-636`, List 비불크 `:655-660`.
  대조: 문자열은 `MessageBufferReader.cs:173-179`에서 **`-1`만 null**, 나머지 음수는 `InvalidDataException`
  (KI-6 해소 본문).
- **설명**: KI-6이 "손상 프레임의 음수 접두가 null 문자열로 조용히 복호 — 필드 누락과 와이어 손상을 수신
  측이 구분 불가"를 결함으로 정의하고 `-1` 외 전부 거부로 해소했는데, **컬렉션은 같은 결함이 그대로 남아
  있다.** `-2`…`int.MinValue` 접두가 예외 없이 null 컬렉션으로 복호된다. null 직렬화 규약은 `-1`인데 판독이
  전체 음수를 수용하므로 와이어 무결성 엄격 기조(KI-6·KI-15·KI-20·KI-36)와 불일치다.
- **영향**: 손상·변조 프레임의 컬렉션 필드가 "데이터 없음"으로 조용히 복호 — 로그·재현 없는 논리 오염.
  퍼저가 못 잡는 이유도 명확하다: 변이 프레임이 null로 **성공 판독**되고 재직렬화는 `-1`이 되어 멱등 왕복
  불변식을 만족하기 때문(관측 하한 안의 블라인드 스팟).
- **구체적 제안**: 5변형의 `< 0` 분기를 `== -1 → null` / `< -1 → InvalidDataException` 2분기로 교정(문자열과
  동일 규약). 생성 텍스트 변경이므로 회귀 테스트 5변형 + 퍼저 코퍼스에 음수 접두 추가. 와이어 형식 무변경
  (합법 프레임은 `-1`만 쓴다 — 생성 직렬화가 그렇게 쓴다).
- **우선순위**: P2 / **난이도**: 하

### RG-03. `SerializerCache<T>` cctor가 사용자 제공 `MessageId` 프로퍼티 게터를 무경비 호출 — "cctor은 절대 던지지 않는다" 불변식의 미커버 우회로

- **근거 위치**: `Source/MessageProtocol.Core/Serialize/MessageSerializer.Cache.cs:60-90` (cctor에서
  `TryResolveMessageIdGetter` 호출 `:82-87`), `:164-179` (`property.GetValue(null)` — **try/catch 없음**).
- **설명**: KI-11의 핵심 계약은 "cctor은 절대 던지지 않는다 — 던지면 CLR이 타입별 초기화 실패를 영구
  캐싱해 `TypeInitializationException`으로 고정"이다. 델리게이트 해석 4종은 전부 null 반환으로 무해화됐는데,
  `MessageId` 프로퍼티만은 **리플렉션으로 실제 호출**한다. 수동 구현 타입의 `public static uint MessageId`
  게터가 예외를 던지면(예: 초기화 순서 의존 구현, `=> throw` 실수) cctor가 그대로 터져 KI-11이 제거한
  영구 `TypeInitializationException`으로 돌아간다. `RegisterGenericConstruction`이 `SerializerCache<T>.HasId`
  를 먼저 읽으므로 등록 시점에도 동일.
- **영향**: 수동 구현 경로에서 "일시적 실수가 영구 고장으로 고정"되는 KI-11 원본 결함 클래스의 재발 통로.
  도달 조건이 수동 구현 + 예외 게터로 좁다는 점에서 Low.
- **구체적 제안**: `TryResolveMessageIdGetter`의 `GetValue(null)`을 try/catch로 감싸 예외 시 `false`(미해석)
  반환 — 이후 등록/사용 지점의 기존 안내 예외(`ThrowMissingSerialize` 계열)와 흐름이 자연히 합류한다.
  cctor 무던지기 불변식 회귀 테스트 1개(예외 게터 픽스처).
- **우선순위**: P2 / **난이도**: 하

### RG-04. 제네릭 컨테이닝 타입 안의 메시지 — 자동 등록(`[ModuleInitializer]`)이 조용히 생략되고 진단 없음

- **근거 위치**: `Source/MessageProtocol.CodeGenerator/Metadata/TypeMetadata.cs:33-35`
  (`CanUseModuleInitializer => !Symbol.IsGenericType && ContainingTypes.All(...)`),
  `Source/MessageProtocol.CodeGenerator/Generate/MessageSerializeCodeEmitter.Define.cs:35-38`
  (`if (typeMeta.CanUseModuleInitializer)` — **else 분기·진단 없음**). `CanUseModuleInitializer` 참조는
  이 한 곳뿐(grep 확인) — 어느 진단도 이 조건을 보지 않는다.
- **설명**: CLR 규칙상 모듈 이니셜라이저는 제네릭 타입의 멤버일 수 없으므로, 제네릭 선언(`GenA<T>`)과
  제네릭 클래스 안에 중첩된 메시지(`class Holder<T> { [StandaloneMessage(1)] partial class M }`)는 생성기가
  Serialize/Deserialize/MessageId를 **생성하면서 등록 코드만 생략**한다. 소비자는 "생성은 됐는데 등록이 안 된
  상태"로 런타임까지 가고, 첫 사용 시 `"Ensure the type is generated via MessageProtocol.CodeGenerator and
  referenced so its ModuleInitializer runs"` (`MessageSerializer.Serialize.cs:154-165`)라는 **사실과 반대인**
  안내(생성은 됐다)를 받는다. 이 저장소의 기조(SEC-07·KI-2·KI-24 — "진단 없는 소비자 런타임 붕괴 = 결함")
  와 정확히 같은 결함군의 미커버 변형이다. 참고로 제네릭 선언은 구성 캐리어가 등록해 주므로 무사하지만,
  **제네릭 컨테이너 안의 비제네릭 메시지**는 아무 경로도 없다.
- **영향**: 런타임 `InvalidOperationException`까지 원인 파악 불가 + 오안내 메시지. 발생 빈도는 드문 선언
  형태라 Low.
- **구체적 제안**: `TryEmit` 시점에 `!CanUseModuleInitializer && !IsGenericWireMessage`이면 진단 신설
  (예: MSGPROT016 Warning — "이 메시지는 자동 등록이 불가능하다. 수동 등록 필요"). 제네릭 선언은
  캐리어 등록이 있으므로 제외.
- **우선순위**: P2 / **난이도**: 하

### RG-05. GroupElement `id ≠ 0` 규칙 — 속성 생성자의 런타임 가드만 존재하고 파이프라인은 실행조차 하지 않음 (문서 계약과 컴파일 계약의 괴리)

- **근거 위치**: 규칙의 문서 근거 — Feature-Spec F2 "GroupElementMessage(uint id) | 그룹 요소 (id ≠ 0 …)" ·
  `Public-API` 메시지 타입 속성 표 "(id ≠ 0)". 유일한 강제 —
  `Source/MessageProtocol.Core/MessageTypeAttributes.cs:49-55` (`if (groupElementMessageId == 0) throw new
  InvalidOperationException(...)`). 생성기 경로 —
  `Source/MessageProtocol.CodeGenerator/Metadata/TypeMetadataValidator.cs:46-79`는 **0을 허용**(상한만 검사),
  `MessageCodeGenerator.cs` 계층 검사(ValidateRootHierarchy)도 id 값을 보지 않는다.
- **설명**: 속성 생성자의 가드는 **리플렉션이 속성을 실제 인스턴스화할 때만** 실행되는데, 이 파이프라인에서
  그런 일이 없다(생성기는 `ConstructorArguments` 상수만 읽고, 런타임은 속성을 반사 조회하지 않는다). 결과
  `[GroupElementMessage(0)]`은 컴파일을 통과하고, 와이어 id 0의 요소 메시지로 **정상 등록·디스패치**된다 —
  문서 규칙과 속성 자체의 가드가 모두 "불법"이라 말하는 상태가 조용히 동작한다. (이 id=0 요소가 런타임에
  즉시 깨지는 것은 아님 — 계약·문서 정합성의 문제라는 뜻이다.) GroupElement가 아닌
  `StandaloneMessage(0)`·`GroupRootMessage(0)`은 범위 규칙상 합법이라 구분된다.
- **영향**: id=0 요소가 다른 그룹 체계에서 "값 미지정" 관례로 재해석될 여지, 문서-동작 불일치. 즉시 장애는
  아니나 이 저장소의 "진단으로 거부" 원칙(F2: 벗어나면 진단)과 어긋난다.
- **구체적 제안**: `TypeMetadataValidator`에 GroupElement 0 검사 추가 → MSGPROT005 사유 흡수(KI-27이 ClassId
  상한을 MSGPROT008에 흡수한 선례와 동일). 생성기 + 런타임 등록 가드 양쪽 정리.
- **우선순위**: P2 / **난이도**: 하

### RG-06. 공개 헬퍼 `ComposeMessageId`·`ComposeHeaderByte`의 무이스케이프 마스킹 — 속성 검증(MSGPROT005/013)과 반대 동작

- **근거 위치**: `Source/Shared/MessageWireFormat.cs:24-31` — `messageIdValue & MessageIdValueMask`,
  `category & NibbleMask`, `flags & NibbleMask` — **범위 초과 값이 조용히 잘린다.**
- **설명**: 이 헬퍼들은 수동 구현자에게 공개된 조립 규칙이다(`Public-API` "수동 구현 시 헤더는 … 직접
  기록"). 속성 기반 선언은 2^24 초과 id·16 이상 카테고리를 MSGPROT005/013으로 **컴파일 거부**하는데
  (KI-8이 "조용한 마스킹"을 결함으로 승격한 것과 동일한 함정), 같은 조립을 코드로 하면 마스킹이
  허용된다. 수동 구현자가 `[StandaloneMessage(16777216)]`과 `ComposeMessageId(Standalone, 0, 16777216)`를
  섞어 쓰면 전자는 컴파일 실패, 후자는 `0x20000000`으로 **별칭 id 조용히 생성** — 피어와 영구 불일치.
- **영향**: 수동 구현 + 상수 계산 조합에서 예외 없는 와이어 불일치. 사용 빈도 낮음(Low).
- **구체적 제안**: 최소 변경 — `Public-API`/GLOSSARY에 "조립 헬퍼는 범위 밖 값을 마스킹한다(검증 없음)"
  계약 명시. 정합 변경 선호 시 `ComposeMessageId`에 범위 가드 추가는 와이어 무관한 공개 API 동작 변경이라
  별도 결정(마스킹 유지 + `Debug.Assert`/문서화 중 택일).
- **우선순위**: P3 / **난이도**: 하

### RG-07. `ReadBoolean`이 0 외 모든 바이트를 true로 복호 — 와이어 무결성 엄격 정책의 유일한 누락 프리미티브

- **근거 위치**: `Source/MessageProtocol.Core/Serialize/MessageBufferReader.cs:88-90`
  (`ReadBoolean() => ReadByte() != 0`). 대조: decimal 비트 검증(KI-15, `:145-170`), 문자열 접두(KI-6,
  `:173-179`), 참조 태그 3-255 거부(KI-36).
- **설명**: 이 저장소의 확립 정책은 "와이어 내용이 규약 밖이면 예외로 거부, 대체 해석 금지"인데 bool만
  `0x02..0xFF`를 전부 true로 수용한다. 생성 직렬화는 1/0만 쓰므로 **0 외 바이트는 정의상 손상·변조**다.
  bool은 참조 태그·decimal flags처럼 후속 파싱을 어긋나게 하지 않아(고정 1바이트) 피해가 작지만, "손상
  프레임이 오류 없이 복호되어 와이어 손상이 은폐"되는 KI-6/KI-20과 같은 부류의 관용이다.
- **영향**: 손상 감지율 저하(1바이트 오염이 bool 필드에서 무음 통과). 재직렬화 시 1로 정규화되어 원본
  바이트 소실 — 포렌식 관점 손해.
- **구체적 제안**: `ReadBoolean`에서 `byte != 0 && byte != 1`이면 `InvalidDataException`(KI-36과 동일 기조).
  와이어 형식 무변경(합법 프레임은 1/0만 담는다 — 생성 writer 보장). 마이그레이션 우려는 수동 구현이
  비표준 바이트를 쓰는 경우뿐 — 문서로 확인 필요(추측: 실사용 사례 없음).
- **우선순위**: P3 / **난이도**: 하

### RG-08. `RegisterNonIdMessage<T>`가 HasId 타입의 강등 등록을 무검증 수용 — "등록됐는데 디스패치 불가" 상태

- **근거 위치**: `Source/MessageProtocol.Core/Serialize/IMessageSerializable.cs:25-28`
  (`IHasIdMessageSerializable<T> : IMessageSerializable<T>` — 제약이 하위 호환),
  `Source/MessageProtocol.Core/Serialize/MessageSerializer.cs:117-128` (`RegisterNonIdMessage<T>()` 리플렉션
  경로 — Serialize 유무만 보고 `HasId`/`MessageId`를 검사하지 않음; 델리게이트 fast path `:86-115`도
  `hasId: false`로 `ValidateRegistration(type, 0u, hasId: false)` 통과).
- **설명**: 제약이 `IMessageSerializable<T>`이라 `IHasIdMessageSerializable<T>` 구현 타입을 NonId로 등록하는
  것이 컴파일·등록 모두 성공한다. 등록된 타입의 생성 Serialize는 **4바이트 헤더(id 포함)를 쓰므로**, 그
  프레임을 object dispatch(`Deserialize(Span)`)로 읽으면 id 조회 → `_registeredMessageIds` 미등록 →
  `KeyNotFoundException("Message type with ID … is not registered")` — 타입은 분명 "등록됐는데" 없다고
  나오는 혼란 상태. typed 경로만 동작해 반쯤 조용한 결함이 된다. (역방향 — HasId 자리에 NonId 등록 — 은
  `ValidateRegistration`이 generic/NonId 플래그를 검사하거나 `RegisterHasIdMessage<T>()`의 `HasId` 검사로
  걸린다.)
- **영향**: 수동 등록 실수 시 원인 파악이 어려운 반쪽 동작. 생성 경로는 속성으로 올바른 등록을 선택하므로
  수동 구현 전용 함정(Low).
- **구체적 제안**: `RegisterNonIdMessage<T>` 진입(양 경로)에서 `SerializerCache<T>.HasId`가 true면
  `InvalidOperationException`("registered as a HasId message; use RegisterHasIdMessage") — 이미 존재하는
  역방향 검사와 대칭.
- **우선순위**: P3 / **난이도**: 하

### RG-09. `ReadBytes(음수)` 계약 공백 — Skip/Advance와 달리 음수 거부 가드가 없어 예외 유형이 불규칙

- **근거 위치**: `Source/MessageProtocol.Core/Serialize/MessageBufferReader.cs:193-199` (`ReadBytes` — 음수
  `length` 가드 없음, `EnsureRemaining` 통과 조합에서 `Span.Slice(position, 음수)`의
  `ArgumentOutOfRangeException` 누출), 대조 `:204-209` (`Skip`은 `ThrowNegativeCount`).
  `Public-API` 버퍼 I/O 계약도 음수 거부를 `Skip`·`Advance`에만 명시.
- **설명**: KI-21이 forward-only 위반(음수 전진)을 공개 경계에서 거부하는 계약을 세웠는데 `ReadBytes`는
  같은 전진 계열임에도 빠져 있다. 입력·상태 조합에 따라 `EndOfStreamException`(uint 트릭이 걸리는 조합) 또는
  `ArgumentOutOfRangeException`(내부 `Slice`가 걸리는 조합)이 불규칙하게 선택된다. 생성 코드는 절대 음수를
  넘기지 않으므로 수동 구현·외부 호출자 전용 위험(KI-21과 동일 결론).
- **영향**: 계약 예외 분류표(운영 체크리스트 1번) 오염. 메모리 안전 영향 없음.
- **구체적 제안**: `ReadBytes` 선두에 `Skip`과 동일한 음수 거부 1줄 + `Public-API` 계약 문장에 `ReadBytes`
  추가. 회귀 테스트 1개.
- **우선순위**: P3 / **난이도**: 하

---

## 3. 원장 대조 — Known-Issues 미반영·만료 항목

### 3.1 원장 텍스트가 뒤처진 것 (코드는 이미 해결 — 개선 문서화 불필요, 원장 갱신 대상)

| 항목 | 원장 기재 | 현재 코드 |
| ---- | --------- | --------- |
| KI-2 "남은 꼬리" | "RegisterCore가 NonId 비트 가진 HasId 등록을 조용히 건너뛰는 동작은 수동 등록 경로에 남아 있음 (필요 시 별도 가드)" | **해소됨** — `ValidateRegistration`(`MessageSerializer.cs:347-356`)이 HasId+NonId 플래그 등록을 등록 시점에 거부. 델리게이트(`:66`)·리플렉션(`:101`) 경로 전부 통과. 조용한 건너뛰기는 도달 불가 |
| KI-3 "남은 꼬리" | "헬퍼 메서드 방출 순서는 여전히 그래프 `_lookup.Values` 반복에 얹혀 있다 (감사 원장 LOW)" | **해소됨** — `SerializationGraph.ReachableTypes`가 타입 이름 오름차순 **정렬 배열**(`SerializationGraph.cs:23-33` 주석, `:59-63` 구현 — "방출 순서 안정화 … BCL Dictionary 열거 순서 의존 제거") |

### 3.2 원장에 열려 있으나 3개 개선 문서 어디에도 반영되지 않은 것

| 항목 | 내용 | 왜 개선 문서에 있어야 하나 |
| ---- | ---- | ------------------------ |
| KI-29 (b)·KI-34 "여전히 열림" | 구체 베이스 멤버의 **파생 필드 유실 자체**(MSGPROT012는 탐지뿐, 손실은 와이어 변경 없이는 불가 — 정책 결정 대기) | F5 스키마 지문은 *탐지* 보조일 뿐이고, "중첩 메시지 디스패치 기록 전환"이라는 major급 정책 결정이 어느 개선 문서에도 로드맵 항목으로 없다. Proposal-VersionTolerantSchema와 함께 major 결정 큐로 명시할 가치 |
| `MessageCategory.CategoryMask` | Public-API 죽은 API 감사 표 — "[Flags] 조합 함정과 묶인 정책 결정 대기, 마킹 보류" | KI-8이 남긴 열거 형태 결정(조합 해석)이 개선 문서 미수록 — F2 함정 문서화로 "충분" 판정이긴 하나, 결정 대기 항목의 소재를 한 곳에 모을 필요 |

---

## 4. 소진 기록 (조사했으나 발견으로 채택하지 않은 것)

| 조사 지점 | 결과 | 판정 |
| --------- | ---- | ---- |
| 다차원 배열(`int[,]`) 멤버 | `Member.cs:101-107`·`:158-164` — `arrayType.Rank != 1` → MSGPROT006. 테스트 `GeneratorDiagnosticTests:1428`도 존재 | 이미 커버 — 결함 아님 |
| 재귀 배열(`int[][]`) | 랭크 1이라 재귀 지원 경로(F3 스펙 "요소 타입은 재귀적으로")로 정상 와이어 | 스펙 준수 — 결함 아님 |
| `Nullable<int>`·`DateTime` 등 메타데이터 타입 멤버 | 그래프 수집 제외(`SerializationGraph.IsSerializableObjectType` — out-of-source) → 이미터 폴백 `ReportUnsupported` → MSGPROT006 | 이미 커버 |
| 위조 플래그 헤더의 생성 `Deserialize` 경로 | KI-5가 예상 4바이트(NonId 1바이트) 전체 비교로 거부 — 혼합 플래그도 불일치로 거부됨 | 이미 커버 (RG-01은 디스패치 경로만의 문제) |
| `GenericDispatchKey` 비트 겹침 | `messageId << 24 | (classId & 0xFFFFFF)`— 56비트 내 무겹침 (`Deserialize.cs:23-25`) | 설계 정상 |
| `PooledBuffer` 라이프사이클 (default struct Dispose·ToPooledBuffer 후 Dispose·사본 이중 Dispose) | KI-37 공유 홀더로 전부 해소 — `Owner.ReturnToPoolOnce` 멱등 | 이미 해결 |
| `RegisterType(Type)` 병행 등록 | 클레임 선점 + 롤백(KI-38), lazy 경로는 잠금 + `when` 가드(`Serialize.cs:131-160`) | 이미 해결 |
| 제네릭 선언 vs 일반 등록 충돌 | 제네릭 선언은 캐리어만 등록(`CanUseModuleInitializer` false) — `RegisterHasIdMessage`와 충돌 불가. `ValidateRegistration`의 generic 플래그 거부와 정합 | 설계 정상 |
| writer `EnsureCapacity(음수)` | long 비교상 no-op — 부작용 없는 호출자 오류. `Skip`/`Advance`와 달리 위험 전이 없음 | 문서화 가치만, 발견으로 미채택 |
| `MemberMetadata.IsMessage` | 참조부 없는 internal 프로퍼티(`MemberMetadata.cs:14`) — 사멸 코드 | 마이크로 정리 후보, 발견으로 미채택 |
| `MessageAttributeRange.Validate`가 `InvalidOperationException` (ArgumentOutOfRangeException 아님) | 속성 ctor 내부 — 이미터·런타임 모두 이 경로를 통과하지 않는다(위 RG-05 참조) | RG-05에 흡수 |
| 0바이트 프레임 | 전 진입점 `ArgumentException` — SEC-03가 이미 커버 | 중복 |
| id 경계 값(0, 2^24-1) | MSGPROT005 상·하한 일관, 조립/분해 대칭(`DecomposeWireId`) | 정상 |
| 1차 문서 PERF-02/03/05/06/07/09·F1~F12 재검토 | 본질 전부 유효 — 이 문서에서 재제안하지 않는다(중복 방지) | 중복 회피 |

**소진 판정**: 위 영역을 전부 훑은 시점에서 추가 신규 발견의 기대치가 급감했다. 남은 미개봉 영역은
(1) 실제 DS_RPC 소비 코드(이 저장소 밖), (2) 벤치마크 실행 데이터(정적 분석 불가), (3) Unity IL2CPP 실측
— 전부 이 저장소의 정적 분석으로는 불가능하거나 PERF-08/F10이 이미 커버한 갭이다.

---

## 관련

- [Improvement-Feature](./Improvement-Feature.md) · [Improvement-Security](./Improvement-Security.md) · [Improvement-Performance](./Improvement-Performance.md) — 검증 대상 1차 웨이브 문서
- [Known-Issues](../06-Troubleshooting/Known-Issues.md) — 원장 대조 기준 (3장)
- [Feature-Spec](../02-Architecture/Feature-Spec.md) · [Public-API](../03-Reference/Public-API.md) — 계약 근거
- [Proposal-VersionTolerantSchema](../02-Architecture/Proposal-VersionTolerantSchema.md) — 3.2 major 결정 큐 관련
