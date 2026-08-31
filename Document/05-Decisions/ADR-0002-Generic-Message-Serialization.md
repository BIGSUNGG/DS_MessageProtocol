---
project: DS_MessageProtocol
type: adr
status: accepted
tags: [adr, generator, generics, roadmap]
updated: 2026-08-31
---

# ADR 0002: 제네릭 메시지 직렬화는 추후 지원으로 연기

## Status

Accepted (2026-08-31)

## Context

v2 코드 리뷰([Known-Issues](../06-Troubleshooting/Known-Issues.md) KI-1)에서 확인: 메시지 속성이 붙은 **제네릭 타입**은 진단 없이 컴파일 불가 코드를 생성한다.

- `MakeHelperSuffix`가 `MetadataName`을 사용해 백틱이 식별자에 유입 (`__WritePayload_N_Msg`1`).
- `Define.Emit`이 `Symbol.Name`만 사용해 partial 선언이 타입 매개변수를 잃음.
- 멤버 타입이 전부 지원 대상이면 진단도 없이 방출되어 사용자는 수십 개의 무관한 CS 구문 에러를 만난다.

[[Feature-Spec]] 기준 제네릭 메시지 타입은 지원 범위에 없다. 지금 거부 진단을 추가하면 추후 제네릭 지원 시 다시 풀어야 하므로, **지원 자체를 로드맵 항목으로 연기**하고 그 방향을 고정한다.

## Decision

1. **제네릭 메시지 직렬화를 추후 버전에서 지원한다** — 거부 진단을 지금 추가하지 않는다.
2. **임시 가이드**: 제네릭 타입에는 메시지 속성을 붙이지 않는다. (멤버 타입이 타입 매개변수면 `MSGPROT006`으로 잡히지만, 멤버가 전부 지원 타입이면 깨진 코드가 생성될 수 있다.)
3. 지원 시 설계 점검 항목:
   - 힌트 이름·헬퍼 접미사의 백틱 처리 (MetadataName `Msg`1` → 유효 식별자로 변환).
   - partial 선언에 타입 매개변수·제약 재구성 (컨테이닝 타입은 `ContainingTypeMetadata`가 이미 처리 — 동일한 방식 재사용).
   - 등록은 닫힌 제네릭 단위만 가능 — `ModuleInitializer` 자동 등록의 범위 결정 (선언 시점에 알 수 없는 구성은 수동 등록 유도).
   - 구성별(예: `Msg<int>` vs `Msg<string>`) MessageId 공유 여부.

## Consequences

### Positive

- 지금 거부 진단을 넣었다가 제네릭 지원 시 제거하는 왕복 작업을 피한다.
- 지원 시점에 와이어·등록 모델을 한 번에 설계할 수 있다.

### Negative

- 지원 전까지 제네릭 타입에 속성을 붙이면 깨진 생성 코드(무진단)를 만난다 — 임시 가이드로만 방어한다.
- 리뷰 발견 결함이 해결 대신 유예 상태로 남는다 ([Known-Issues](../06-Troubleshooting/Known-Issues.md) KI-1).

## Alternatives considered

- 지금 거부 진단 추가 — 추후 지원 시 되돌려야 해서 기각.
- 즉시 제네릭 지원 — 닫힌 제네릭 등록·MessageId 배분 설계가 필요해 범위 초과로 기각.

## 관련

- [Known-Issues](../06-Troubleshooting/Known-Issues.md) (KI-1)
- [Feature-Spec](../02-Architecture/Feature-Spec.md) (범위 밖)
- [CONTEXT](../00-AI/CONTEXT.md)
