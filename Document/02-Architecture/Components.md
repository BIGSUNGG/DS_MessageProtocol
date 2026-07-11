---
project: DS_MessageProtocol
type: architecture
status: stub
tags: [architecture, components]
updated: 2026-07-11
---

# Components

패키지/어셈블리 단위 컴포넌트 맵.

| NuGet 패키지 | 설명 |
|--------------|------|
| **MessageProtocol** | 메인 패키지. 런타임 DLL + analyzers에 CodeGenerator 포함 |
| **MessageProtocol.Core** | 직렬화 런타임 API (`MessageSerializer`, 메시지 계약) |
| **MessageProtocol.CodeGenerator** | Roslyn 분석기/소스 생성기 (고급·세분화 참조용) |

## 상세

| 컴포넌트 | 책임 | 의존 |
|----------|------|------|
| (추가 예정) | | |

## 관련

- [[Overview]]
- [[Packages]]
- [[Data-Flow]]