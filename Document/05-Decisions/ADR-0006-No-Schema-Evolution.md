---
project: DS_MessageProtocol
type: adr
status: accepted
tags: [adr, wire-format, schema]
updated: 2026-09-09
---

# ADR 0006: 와이어 스키마 진화 미지원

## Status

Accepted (2026-09-09) — [Proposal-VersionTolerantSchema](../02-Architecture/Proposal-VersionTolerantSchema.md)를 **기각**한다. 와이어 형식(위치 기반)은 그대로 유지된다.

## Context

[Proposal-VersionTolerantSchema](../02-Architecture/Proposal-VersionTolerantSchema.md)는 롤링 배포·강제 업데이트 불가 환경에서 서로 다른 스키마 버전의 피어가 통신하는 문제를 다루며, 필드 태그(옵션 A)·프레임 버전(옵션 B)·부가 전용 규약(옵션 C)을 제시했다. 와이어 형식 변경을 수반하는 major 결정이라 PENDING 에서 사용자 판단을 기다리는 상태였다.

반면 상용화 검토([Commercial-Readiness-Review](../04-Improvements/Commercial-Readiness-Review.md) §1)는 "스키마 진화 미지원"을 구조적 제약으로 기록하고 레이아웃 동결 운영 규칙을 제안한 상태였다.

이 프로젝트의 실제 배치 형태는 **Unity 클라이언트와 서버가 같은 라이브러리(같은 메시지 계약)를 함께 소비**한다:

- 피어 간 버전 불일치는 라이브러리·계약 버전을 올릴 때만 발생하며, 그 시점에 양쪽을 함께 재컴파일·재배포하면 된다.
- 단일 조직이 클라이언트(Unity)·서버를 함께 통제하므로 "업데이트할 수 없는 외부 피어"라는 제안의 전제가 성립하지 않는다.
- 따라서 스키마 진화(버전 내성)가 풀어야 할 문제 자체가 이 프로젝트에는 존재하지 않는다.

## Decision

1. **와이어 스키마 진화를 지원하지 않는다** — 위치 기반(positional) 와이어 형식을 유지하고, 버전 내성 스키마(필드 태그·프레임 버전·부가 규약 포함)는 도입하지 않는다.
2. **스키마 변경 운영 규칙** — 배포된 메시지의 멤버 레이아웃은 동결한다. 스키마 변경이 필요하면 **새 MessageId 의 신규 타입 추가**로 하고 구형 타입은 등록 유지한다(헤더 MessageId 검증(KI-5)이 혼선을 진입에서 차단). 이 규칙을 공식 컨벤션으로 확정한다.
3. **재검토 조건** — 향후 외부 배포(제3자 클라이언트·버전 혼합이 불가피한 환경)로 전제가 바뀌면 새 ADR 로 다룬다. 그 전까지 본 제안은 재기되지 않는다.

## Consequences

### Positive

- 프레임당 태그 오버헤드(+1B/멤버)·tag→setter 디스패치·양쪽 형식 판독기 없음 — 현재의 위치 기반 성능·크기 그대로.
- 생성기 단순성 유지 — 버전 관련 신규 진단·퍼저 코퍼스·기준선 재측정 작업 불필요.
- "지원 안 함"이 명문화되어 상위(DS_RPC)가 부가 필드 우회 같은 기술 부채로 진화를 흉내 낼 이유가 사라짐.

### Negative

- 스키마가 다른 피어 간 통신은 프레임 오염 — 라이브러리 버전 동기 배포가 필수(운영 규칙 2로 완화).
- 미래에 진화 지원이 필요해지면 와이어 파괴 변경(major)을 감수해야 함 — 결정 3의 재검토 조건으로 명시.

## Alternatives considered

- **옵션 A — 필드 태그 + Skip(protobuf 방식)** : 상위 호환이 구조적으로 성립하나, 전제가 없는 이 프로젝트에는 영구 오버헤드·복잡성만 남음 — 기각.
- **옵션 B — 프레임 버전 접두** : 구현은 쉽지만 삭제 내성 없음·판독기 선형 증가 — 기각.
- **옵션 C — 부가 전용 규약** : 와이어 무변경이지만 삭제·이동에 여전히 파괴적인 반쪽짜리 — 기각.

## 관련

- [Proposal-VersionTolerantSchema](../02-Architecture/Proposal-VersionTolerantSchema.md) (기각된 제안, 근거·옵션 보존)
- [Commercial-Readiness-Review](../04-Improvements/Commercial-Readiness-Review.md) §1 (운영 규칙 출처)
- [ADR-0004](./ADR-0004-Generic-Message-Wire-Format.md) (현재 와이어 형식)
