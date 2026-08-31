---
project: DS_MessageProtocol
type: adr
status: accepted
tags: [adr, generator, generics, attributes]
updated: 2026-08-31
---

# ADR 0005: 제네릭 구성 선언 속성 통합

## Status

Accepted (2026-08-31) — [ADR-0004](./ADR-0004-Generic-Message-Wire-Format.md)의 선언 모델(결정 1·6)을 대체한다. 와이어 포맷(결정 2)·디스패치 모델은 그대로 유지된다.

## Context

ADR-0004 체제는 `[GenericMessage(typeof(인수들), ClassId)]`(선언부)와 `[GenericConstruction(typeof(구성), ClassId)]`(캐리어) 두 속성이었다.

- 역할이 같은 두 속성의 선택 비용(무엇이 다른가?)이 지속적으로 발생.
- 소비자 미발생 시점(2.0.0 게시 전) — 속성 표면을 바꿀 수 있는 유일한 시점. 와이어·속성 계약은 게시 후 변경 불가.
- "제네릭 + `StandaloneMessage` = 제네릭 와이어" 규칙(이 결정의 결정 2)이 도입되면 선언부 마커가 불필요해져 통합이 단순해짐.

## Decision

1. **단일 속성** — `[GenericMessage(typeof(닫힌 구성), ClassId = n)]` 하나로 통일. 선언부·캐리어 등 임의 타입 선언에 부착 가능 (`AllowMultiple`). 타입 인수 전용 문법과 `GenericConstructionAttribute` 폐기.
2. **제네릭 와이어 규칙** — `StandaloneMessage`가 붙은 제네릭 선언은 구성 선언 여부와 무관하게 항상 제네릭 와이어(플래그 0 + MessageId 3B + ClassId 3B).
3. **구성 선언 필수** — 어디에서도 선언되지 않은 구성의 직렬화는 선언 방법을 안내하는 예외를 던진다. 구성 선언 없는 제네릭 메시지는 컴파일은 되지만 직렬화 불가.
4. **안전 진단 강화** — 같은 컴파일 내 동일 구성 중복 선언은 `MSGPROT008` 컴파일 에러(방치 시 모듈 로드 크래시를 컴파일 타임으로 승격). 미바운드 제네릭(`typeof(Envelope<>)`) 선언 거부. 타 어셈블리 간 중복은 런타임 감지 유지(문서화).
5. **등록 모델 불변** — 부착 위치마다 생성기가 등록 클래스(`[ModuleInitializer]`) 출력, 모듈 로드 시 자동 등록. 수동 등록 `RegisterGenericConstruction` 은 공개 유지(탈출구).
6. `MSGPROT009`(스탠드얼론 누락)는 `MSGPROT008` 사유로 흡수·삭제.

## Consequences

### Positive

- 속성 1개·문법 1개 — 선언 위치 선택의 인지 비용 제거.
- 선언부 부착과 분산(캐리어) 부착이 동일 의미로 통일.
- 중복 선언이 크래시 대신 컴파일 에러로 검출.
- 구성 선언 없이 제네릭 경로를 쓰는 경우가 사라져 규칙이 단순해짐.

### Negative

- ADR-0004 대비 속성 표면 파괴적 변경 — 같은 날 도입·외부 소비자 없음으로 허용.
- 닫힌 구성 문법이 타입 인수 문법보다 조금 길다 (`typeof(Envelope<Ping>)`).

## Alternatives considered

- 속성 폐기·수동 등록 전용 — 초기화 순서·누락 리스크가 돌아와 기각 (속성 기본 + 수동 탈출구 공존 유지).
- 인자 없는 마커 속성 — "제네릭 + 스탠드얼론 = 제네릭 와이어" 규칙으로 불필요해져 기각.
- 현 2속성 유지 — 인지 비용 대비 이점 없음, 변경 가능 시점 제한으로 기각.

## 관련

- [ADR-0004](./ADR-0004-Generic-Message-Wire-Format.md) (선언 모델 대체됨, 와이어 유지)
- [Feature-Spec](../02-Architecture/Feature-Spec.md) (F2·F5)
- [Known-Issues](../06-Troubleshooting/Known-Issues.md)
