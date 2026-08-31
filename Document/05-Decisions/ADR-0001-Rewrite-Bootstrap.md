---
project: DS_MessageProtocol
type: adr
status: accepted
tags: [adr, rewrite, parity]
updated: 2026-08-31
---

# ADR 0001: 재작성 부트스트랩 결정

## Status

Accepted (2026-08-31)

## Context

Legacy를 `Legacy/`로 옮기고 기능 스펙([[Feature-Spec]])을 확정했다.
구현 착수 전, 소비자 호환과 검증 방식에 영향을 주는 기본 결정을 고정한다.

## Decision

1. **네임스페이스·타입 이름: Legacy와 동일 유지** — `MessageProtocol`, `MessageProtocol.Serialize` 등 공개 표면을 그대로 둔다.
2. **테스트: 스펙 기반으로 새로 작성** — Legacy 테스트 50개는 이식하지 않고 참조만 한다.
3. **구현 순서: 예시 우선** — 사용할 예시·인수 조건을 먼저 만들고, 그걸 통과하도록 구현한다.
4. **패키지: 아이디 3종(`MessageProtocol`, `.Core`, `.CodeGenerator`) 유지, 버전은 2.0.0부터.**

## Consequences

### Positive

- DS_RPC·DS_Communication 등 소비자 수정 없이 drop-in 교체 가능 (와이어 + API 완전 호환).
- 새 테스트는 스펙 검증에 집중, Legacy 구현 디테일에 묶이지 않음.
- 예시 우선으로 인수 조건이 실행 가능한 형태로 먼저 고정됨.

### Negative

- Legacy 테스트를 그대로 쓰지 않아 초기 작성 비용이 든다.
- 이름 유지로 구식 명명·설계가 있어도 공개 표면은 못 바꾼다.

### Neutral

- 버전 2.0.0으로 재작성임을 명시.

## Alternatives considered

- 네임스페이스 재설계 (`DS.MessageProtocol` 등) — 소비자 일괄 마이그레이션 비용 때문에 기각.
- Legacy 테스트 그대로 이식 / 이식 + 크로스 검증 — 스펙 기반 신규 작성으로 결정.
- 1.x 버전 라인 유지 — 재작성 의미를 약화시켜 기각.

## 관련

- [[Feature-Spec]]
- [[CONTEXT]]
