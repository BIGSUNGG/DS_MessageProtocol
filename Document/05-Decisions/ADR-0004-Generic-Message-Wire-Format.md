---
project: DS_MessageProtocol
type: adr
status: accepted
tags: [adr, generator, generics, wire-format]
updated: 2026-08-31
---

# ADR 0004: 제네릭 메시지 전용 속성과 구성 클래스 ID 와이어

## Status

Accepted (2026-08-31) — [ADR-0003](./ADR-0003-Generic-Message-Serialization.md)(디스패치 위임 모델)을 대체한다.

## Context

ADR-0003 모델의 결함:

- 닫힌 구성들이 **선언의 MessageId 하나를 공유** → object dispatch 등록이 ID 당 한 구성만 가능.
- 지연 등록은 `Serialize(object)` 경로에서만 일어나 **역직렬화만 하는 수신 프로세스는 수동 등록이 필수** — 네트워크 시나리오에서 수신 실패.
- 어떤 구성을 직렬화 지원하는지 선언부가 드러나지 않음.

## Decision

1. **전용 속성 `[GenericMessage]`** — 제네릭 메시지 선언에 구성마다 반복 부착 (`AllowMultiple`):
   `[GenericMessage(typeof(Ping), ClassId = 1)]`, 다중 매개변수는 `[GenericMessage(typeof(A), typeof(B), ClassId = 3)]`.
   기존 메시지 선언 속성과 별개이며, 제네릭 선언에는 ID 원천으로 `[StandaloneMessage(id)]`가 필수.
2. **와이어 포맷 확장** — 제네릭 메시지는 헤더 플래그 **Generic(0)** + MessageId 24비트 뒤에 **구성 클래스 ID(ClassId) 24비트(1 .. 2^24-1)**:
   `[헤더 1B][MsgId 3B][ClassId 3B][페이로드]` (`GenericIdHeaderSize = 7`). 비제네릭 와이어는 불변.
3. **디스패치 키 (MessageId, ClassId)** — 선언된 구성은 생성기가 등록 클래스(`[ModuleInitializer]`)로 모듈 로드 시 자동 등록 → **송수신 양쪽 무설정**. 미선언 구성은 직렬화 시 명시 예외(`__GenericClassId == 0` 가드).
4. **진단** — `MSGPROT008`(잘못된 선언: 비제네릭 부착·인수 개수 불일치·ClassId 누락/중복), `MSGPROT009`(제네릭 선언에 `[StandaloneMessage]` 누락).
5. 런타임 지연 등록 경로(`RegisterCore`)는 제네릭 헤더 플래그를 거부 → 구성 등록은 `RegisterGenericConstruction<T>(classId)` 전용.

## Consequences

### Positive

- 수신 측 등록 없이 네트워크 역직렬화 동작 (같은 프로토콜 어셈블리를 참조하면 모듈 로드 시 자동 등록).
- 같은 선언의 여러 닫힌 구성이 서로 다른 ClassId 로 공존·디스패치.
- 지원하는 구성이 선언부에 명시되어 프로토콜 표면이 읽기 쉬워짐.

### Negative

- 제네릭 와이어가 ADR-0003 대비 파괴적 변경 (같은 날 도입, 외부 소비자 없음 — 허용).
- 제네릭 헤더 7바이트.
- 구성은 반드시 사전 선언 필요 — 선언 없이 닫힌 구성만 쓰는 것은 불가.
- ADR-0003의 `T` 멤버 제약(등록된 ID 메시지만, 백레퍼런스 추적 밖)은 그대로 승계.

## Alternatives considered

- 어셈블리 수준 구성 나열 속성 — 선언과 등록이 분리되어 기각 (선언부 반복 속성 채택, 사용자 결정).
- ClassId 자동 채번 — 선언 순서 의존 매직이 생겨 기각 (명시 부여).
- 구성별 MessageId 수동 재정의 유지 — ClassId 가 식별자 역할을 대신해 제거.

## 관련

- [ADR-0003](./ADR-0003-Generic-Message-Serialization.md) (대체됨)
- [Feature-Spec](../02-Architecture/Feature-Spec.md) (F1·F2·F5)
- [Known-Issues](../06-Troubleshooting/Known-Issues.md) (KI-1)
