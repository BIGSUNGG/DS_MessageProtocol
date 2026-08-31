---
project: DS_MessageProtocol
type: context
status: stable
tags: [ai, glossary]
updated: 2026-08-31
---

# Glossary

도메인 용어. 새 용어는 여기 먼저 추가한다.

| 용어 | 설명 |
| ------ | ------ |
| MessageSerializer | 런타임 직렬화/역직렬화 진입점 (`MessageProtocol.Serialize`) |
| CodeGenerator | 메시지 타입용 생성 코드를 만드는 Roslyn incremental source generator |
| Message contract | 직렬화 대상 메시지 타입·계약 (`IMessageSerializable<T>` 등) |
| Analyzer path | NuGet `analyzers/dotnet/cs` 에 포함되는 생성기 어셈블리 경로 |
| MessageId | 헤더 byte0 + 24비트 값으로 조립된 `uint` 식별자 |
| MessageFlag | 헤더 상위 니블: NonId / Standalone / GroupRoot / GroupElement / Generic(0, 제네릭 전용) |
| MessageCategory | 헤더 하위 니블 0..15 (`MessageCategoryAttribute`) |
| NonId | ID 없는 메시지. 헤더 1바이트. object `Deserialize` 불가 |
| Standalone / GroupRoot / GroupElement | ID를 가진 메시지 종류. object deserialize 대상 |
| MessageIgnore / MessageInclude | 멤버 제어 속성. **`MessageProtocol` 네임스페이스 소속** (v2에서 전역 네임스페이스에서 이동 — Legacy 버그 수정) |
| ModuleInitializer | 생성 코드가 모듈 로드 시 `Register*` 를 호출하는 훅 |
| Shared Link | `Source/Shared`를 Core·Generator에 Compile Link로 공유 (와이어 규칙 단일 소스) |
| ReferenceKind | 중첩 객체 참조 태그: Null=0, NewObject=1, BackReference=2 (와이어 규격) |
| 수동 구현 | 생성기 없이 계약 인터페이스 + 정적 메서드를 직접 구현·등록 (헤더는 작성자가 직접 기록) |
| GenericMessage | 제네릭 구성 선언 속성. `(닫힌 구성, ClassId)` 반복 부착 — 선언부·캐리어 무관 ([ADR-0005](../05-Decisions/ADR-0005-Generic-Attribute-Unification.md)) |
| 구성 클래스 ID (ClassId) | 제네릭 헤더의 24비트 구성 식별자(1 .. 2^24-1). (MessageId, ClassId) 키로 닫힌 구성 디스패치 |

## 공통 (DS 스택)

| 용어 | 설명 |
| ------ | ------ |
| netstandard2.1 | Unity 및 다중 .NET 런타임 호환 타깃 (Core) |
| netstandard2.0 | Roslyn 분석기 표준 타깃 (CodeGenerator) |
| NuGet | 패키지 배포 단위 |
| Sandbox | 실행 가능 인수 조건 프로젝트 |
| Legacy | `Legacy/` — v1 참조 구현 (테스트 50개 포함, 수정 금지) |

## 관련

- [CONTEXT](./CONTEXT.md)
- [CONVENTIONS](./CONVENTIONS.md)
- [Public-API](../03-Reference/Public-API.md)
