---
project: DS_MessageProtocol
type: architecture
status: draft
tags: [feature-spec, rewrite, parity]
updated: 2026-08-31
---

# Feature Spec — 재작성 프로젝트 지원 기능

`Legacy/` 참조 구현이 지원하는 기능을 새 구현에서도 그대로 지원한다.
이 문서는 지원 범위(스펙)만 정의하며, 구현 방식은 재작성하며 바뀔 수 있다.

- **참조 구현**: `Legacy/` (Source · Test 50 · Examples · Document)
- **Legacy 문서**: `Legacy/Document/` (00-AI ~ 06-Troubleshooting)
- **판정 기준**: 아래 기능 스펙 + Legacy `Test/` 50개 테스트와 동등한 동작

## F1. 와이어 헤더

| 항목        | 스펙                                                                |
| --------- | ----------------------------------------------------------------- |
| Byte 0    | flags(상위 니블) + category(하위 니블)                                    |
| ID 메시지    | 헤더 4바이트. `MessageId = (headerByte << 24) \| (value & 0x00FFFFFF)` |
| NonId 메시지 | 헤더 1바이트                                                           |
| ID 값 범위   | `0 .. 2^24-1`                                                     |

헤더 규칙은 직렬화 런타임과 코드 생성기가 공유하는 단일 소스(Legacy: `Source/Shared` Link Compile)에서 온다.

## F2. 메시지 종류·카테고리

| 속성                               | 역할                           |
| -------------------------------- | ---------------------------- |
| `StandaloneMessage(uint id)`     | 독립 ID 메시지                    |
| `GroupRootMessage(uint id)`      | 그룹 루트                        |
| `GroupElementMessage(uint id)`   | 그룹 요소 (id ≠ 0, 상속 계층에 루트 필수) |
| `NonIdMessage`                   | ID 없는 메시지                    |
| `MessageCategory(Category0..15)` | 카테고리 니블                      |

- 메시지 타입은 `partial` 선언이 필수.
- 그룹 계층 규칙 위반은 컴파일 진단으로 거부 (F5).

## F3. 멤버 타입 지원

| 분류 | 지원 |
| ------ | ------ |
| 프리미티브 | `bool, byte, sbyte, short, ushort, int, uint, long, ulong, float, double, decimal, char` |
| 문자열 | `string` (nullable 허용) |
| 열거형 | underlying 프리미티브로 직렬화 |
| 컬렉션 | 1차원 배열 `T[]`, `List<T>`, `IList<T>` (요소 타입은 재귀적으로 위 규칙 적용) |
| 중첩 메시지 | 직렬화 대상 객체 멤버 (그래프 수집) |
| 순환 참조 | 객체 아이디 백레퍼런스로 처리 (무한 루프 없이 round-trip) |
| byte 데이터 | `byte[]` (길이 + 내용) |

미지원 타입은 컴파일 타임 에러 진단으로 거부한다 (조용한 스킵 없음).

## F4. 멤버 제어

| 속성 | 역할 |
|------|------|
| `MessageIgnore` | 직렬화 제외 |
| `MessageInclude` | 직렬화 포함 힌트 |

## F5. 코드 생성기 (컴파일 타임)

- 속성 붙은 `partial` 메시지 타입에서 `Serialize` / `Deserialize` / (ID면) `MessageId` 생성.
- `[ModuleInitializer]` 등록 코드 생성 → 모듈 로드 시 런타임에 자동 등록 (수동 등록 불필요).
- Incremental generator.
- 수동 구현 지원: 생성기 없이 동일한 계약 형태(`IMessageSerializable<T>` 등)를 직접 구현·등록 가능. 수동 구현 시 헤더는 사용자가 직접 쓴다.
- 진단 (Legacy 기준, 동등한 검출 필요):
  - `MSGPROT001` 메시지 타입은 partial 필수
  - `MSGPROT002` 중첩 메시지의 컨테이닝 타입 partial 필수
  - `MSGPROT003` 요소 메시지는 계층에 루트 필수
  - `MSGPROT004` 루트 메시지의 부모가 루트일 수 없음
  - `MSGPROT005` ID 값 범위 초과
  - `MSGPROT006` 미지원 멤버 타입

## F6. 런타임 `MessageSerializer`

| API | 설명 |
| ----- | ------ |
| `RegisterHasIdMessage<T>()` / `RegisterNonIdMessage<T>()` | ID / NonId 등록 (델리게이트 오버로드 포함) |
| `RegisterType(Type)` | 리플렉션 기반 등록 |
| `Serialize<T>(T)` | 선언 타입 기준 제네릭 캐시 경로 (`byte[]`) — 런타임 타입 미참조 |
| `Serialize(object)` / `SerializeToWriter` | 런타임 타입 dispatch — 다형성(베이스 변수 + 파생 인스턴스) |
| `SerializePooled*` | ArrayPool 기반 결과 (`PooledBuffer`, Dispose 멱등) |
| `Deserialize<T>(...)` | 제네릭 역직렬화 |
| `Deserialize(byte[] \| Span \| Memory)` | 헤더 MessageId 기반 object 역직렬화 (Standalone/Group만, NonId 불가) |

계약 인터페이스: `IMessageSerializable<T>`, `IHasIdMessageSerializable<T>` (+`static uint MessageId`).

## F7. 성능 계약

핫 경로에서 다음 특성을 유지한다 (회귀 시 벤치마크로 확인):

- 제네릭 경로는 Dictionary lookup·박싱 없음 (정적 캐시).
- `ArrayPool` + 풀링 버퍼 (`SerializePooled` / `PooledBuffer`).
- Span 기반 읽기·쓰기, 문자열은 중간 `ToArray` 없이 디코딩.
- `decimal`은 무할당 경로.
- 생성 코드는 고정 크기 프리미티브 구간을 일괄 `EnsureCapacity`.
- flat 메시지(멤버 1~2개)는 Dictionary 할당 없음.

## F8. 호환성

- 타깃: `netstandard2.1` (+ `net6.0`) — Unity 호환.
- `ModuleInitializer` polyfill 포함.
- 테스트는 최신 런타임 멀티타깃 (Legacy 기준 `net8.0;net9.0`).

## F9. 패키지 구성

| 패키지 | 내용 |
| -------- | ------ |
| **MessageProtocol** | 앱용 단일 진입점: Core 런타임 + 생성기를 `analyzers/dotnet/cs`로 동봉 |
| **MessageProtocol.Core** | 런타임 API 단독 |
| **MessageProtocol.CodeGenerator** | 생성기 단독 (고급·세분화 참조용) |

## F10. 검증 산출물

- 유닛 테스트: round-trip(타입 종류 × 멤버 조합) · 생성기 진단 · 등록/디스패치 — Legacy 50 테스트 동등.
- 벤치마크: 직렬화 핫 경로 (BenchmarkDotNet).
- 최소 예제: Standalone round-trip 콘솔 샘플.
- `Sandbox/`는 앞으로 구현될 기능의 실행 가능 인수 조건을 담는다.

## 범위 밖 (Legacy와 동일)

- 네트워크 전송 → DS_Communication
- RPC 디스패치·원격 호출 → DS_RPC
- Legacy에서 미지원이던 멤버 타입 추가 (`Dictionary`, nullable 값 타입 등) — 스펙 동결, 필요 시 별도 결정(05-ADR)으로 확장.

## 관련

- [[CONTEXT]]
- Legacy 문서: `Legacy/Document/01-Overview/Scope.md`, `Legacy/Document/03-Reference/Public-API.md`
