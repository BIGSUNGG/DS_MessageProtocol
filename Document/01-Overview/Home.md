---
project: DS_MessageProtocol
type: overview
status: stable
tags: [moc, home]
updated: 2026-08-31
---

# DS_MessageProtocol — Home

컴파일 타임 메시지 직렬화와 런타임 MessageSerializer를 제공하는 .NET 라이브러리. Unity / .NET Standard 2.1. v2 재작성 버전(패키지 2.0.0).

## Map of Content

### AI

- [CONTEXT](../00-AI/CONTEXT.md) — 에이전트 진입점
- [GLOSSARY](../00-AI/GLOSSARY.md)
- [CONVENTIONS](../00-AI/CONVENTIONS.md)

### Architecture

- [Feature-Spec](../02-Architecture/Feature-Spec.md) — 지원 기능 스펙 (F1–F10) + Legacy 대비 변경점
- [Overview](../02-Architecture/Overview.md) — 3층 구조

### Reference

- [Packages](../03-Reference/Packages.md)
- [Public-API](../03-Reference/Public-API.md)

### Decisions

- [ADR-0001 Rewrite Bootstrap](../05-Decisions/ADR-0001-Rewrite-Bootstrap.md) — 재작성 부트스트랩 결정

### Meta

- [Changelog](../_meta/Changelog.md)

## 빠른 시작

1. `MessageProtocol` 패키지 참조 → 메시지 타입에 속성 + `partial` 선언 → 생성 코드가 직렬화·등록을 담당.
2. 실행 인수 조건 확인: `dotnet run --project Sandbox/MessageProtocol.Sandbox`
3. v1 참조 구현·구 문서: `Legacy/`

## 외부

- GitHub: <https://github.com/BIGSUNGG/DS_MessageProtocol>
- 루트 README: 저장소 `README.md`
