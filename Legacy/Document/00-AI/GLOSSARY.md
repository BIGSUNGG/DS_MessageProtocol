---
project: DS_MessageProtocol
type: context
status: draft
tags: [ai, glossary]
updated: 2026-07-11
---

# Glossary

도메인 용어. 새 용어는 여기 먼저 추가한다.

| 용어 | 설명 |
|------|------|
| MessageSerializer | 런타임 직렬화/역직렬화 진입점 |
| CodeGenerator | 메시지 타입용 생성 코드를 만드는 Roslyn 분석기 |
| Message contract | 직렬화 대상 메시지 타입·계약 (`IMessageSerializable` 등) |
| Analyzer path | NuGet `analyzers/dotnet/cs` 에 포함되는 생성기 어셈블리 경로 |
| MessageId | 헤더 byte0 + 24비트 값으로 조립된 `uint` 식별자 |
| MessageFlag | 헤더 상위 니블: NonId / Standalone / GroupRoot / GroupElement |
| MessageCategory | 헤더 하위 니블 0..15 (`MessageCategoryAttribute`) |
| NonId | ID 없는 메시지. 헤더 1바이트. object `Deserialize` 불가 |
| Standalone / GroupRoot / GroupElement | ID를 가진 메시지 종류. object deserialize 대상 |
| ModuleInitializer | 생성 코드가 모듈 로드 시 `Register*` 를 호출하는 훅 |
| Shared Link | `Source/Shared`를 Core·Generator에 Compile Link로 공유 |

## 공통 (DS 스택)

| 용어 | 설명 |
|------|------|
| netstandard2.1 | Unity 및 다중 .NET 런타임 호환 타깃 |
| NuGet | 패키지 배포 단위 |
| Sandbox / Examples | 샘플·데모 프로젝트 (이 저장소에는 현재 없음) |

## 관련

- [[CONTEXT]]
- [[CONVENTIONS]]
- [[Public-API]]
