---
project: DS_MessageProtocol
type: context
status: draft
tags: [ai, entry]
updated: 2026-07-11
---

# CONTEXT — DS_MessageProtocol

> **AI: 이 vault를 다룰 때 먼저 이 파일을 읽는다.**

## 한 줄 요약

컴파일 타임 메시지 직렬화와 런타임 MessageSerializer를 제공하는 .NET 라이브러리. Unity / .NET Standard 2.1.

## 저장소

- GitHub: https://github.com/BIGSUNGG/DS_MessageProtocol
- 문서 vault 루트: `Document/` (이 폴더가 Obsidian Vault)
- 솔루션: `MessageProtocol.sln`
- 제품: `Source/` (Core · CodeGenerator · MessageProtocol · Shared)
- 테스트: `Test/` (CoreTests `net8.0;net9.0` · Benchmarks)
- 예제: `Examples/MinimalConsole` (Standalone round-trip)

## 읽을 순서

1. [[CONTEXT]] (지금)
2. [[GLOSSARY]]
3. [[Scope]]
4. [[Overview]] (Architecture)
5. [[Packages]]
6. `05-Decisions/` ADR (있을 경우)
7. [[CONVENTIONS]]

## 패키지 요약

| NuGet 패키지 | 설명 |
|--------------|------|
| **MessageProtocol** | 메인 패키지. 런타임 DLL + analyzers에 CodeGenerator 포함 |
| **MessageProtocol.Core** | 직렬화 런타임 API (`MessageSerializer`, 메시지 계약) |
| **MessageProtocol.CodeGenerator** | Roslyn 분석기/소스 생성기 (고급·세분화 참조용) |

## 형제 프로젝트

- DS_Communication — 네트워크 전송 (TCP/RUDP)
- DS_MessageProtocol — 메시지 직렬화
- DS_RPC — 분산 RPC (위 둘에 의존)

의존 방향: **DS_RPC → DS_MessageProtocol, DS_Communication**

## 관련 노트

- 사람용 시작: [[Home]]
- 범위: [[Scope]]
- 구조: [[Overview]] · [[Components]] · [[Data-Flow]]
- 구조·성능·병목: [[Known-Issues]]
- 규칙: [[CONVENTIONS]]
