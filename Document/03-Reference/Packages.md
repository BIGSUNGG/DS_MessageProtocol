---
project: DS_MessageProtocol
type: reference
status: draft
tags: [packages, nuget]
updated: 2026-07-11
---

# Packages

| NuGet 패키지 | 설명 |
|--------------|------|
| **MessageProtocol** | 메인 패키지. 런타임 DLL + analyzers에 CodeGenerator 포함 |
| **MessageProtocol.Core** | 직렬화 런타임 API (`MessageSerializer`, 메시지 계약) |
| **MessageProtocol.CodeGenerator** | Roslyn 분석기/소스 생성기 (고급·세분화 참조용) |

## 설치

루트 `README.md` 및 NuGet.org 패키지 ID를 참고한다.

## 버전

- 패키지 버전·의존 버전은 저장소 `Directory.Build.props` (및 각 csproj)에서 관리한다.

## 관련

- [[Public-API]]
- [[Configuration]]
- [[Scope]]