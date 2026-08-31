# DS_MessageProtocol

컴파일 타임 메시지 직렬화와 런타임 `MessageSerializer`를 제공하는 .NET 라이브러리 세트입니다. 런타임은 **.NET Standard 2.1**과 **net6.0**을 타깃으로 하여 Unity 및 일반 .NET 환경에서 사용할 수 있습니다.

## 패키지

| NuGet 패키지 | 설명 |
| -------------- | ------ |
| **MessageProtocol** | 애플리케이션에서 참조하는 메인 패키지. NuGet 패키지에는 런타임 DLL과 함께 `analyzers/dotnet/cs` 경로에 CodeGenerator 어셈블리가 포함됩니다. |
| **MessageProtocol.Core** | 직렬화 런타임 API(`MessageSerializer`, 메시지 계약 등). 다른 패키지 없이 코어만 필요할 때 사용합니다. |
| **MessageProtocol.CodeGenerator** | 메시지 타입용 생성 코드를 만드는 Roslyn 분석기 패키지 (netstandard2.0 분석기). 고급 시나리오 또는 세분화된 참조가 필요할 때 사용합니다. |

## 설치

```bash
dotnet add package MessageProtocol
```

코어만 필요한 경우:

```bash
dotnet add package MessageProtocol.Core
```

Unity Package Manager에서 NuGet을 쓰지 않는 경우, 위 패키지에서 빌드된 DLL을 프로젝트에 복사해 참조할 수 있습니다. 타깃은 **netstandard2.1**입니다.

## 요구 사항

- .NET Standard 2.1 또는 net6.0 이상을 지원하는 런타임, 혹은 Unity(해당 API 호환 버전).
- 메시지·멤버 속성(`StandaloneMessage`, `MessageIgnore` 등)은 모두 `MessageProtocol` 네임스페이스에 있습니다.

## 저장소 구조

| 경로 | 내용 |
| ------ | ------ |
| `Source/` | 제품 코드 — Core(런타임) · CodeGenerator(생성기) · MessageProtocol(메타 패키지) · Shared(와이어 규칙 공유 소스) |
| `Test/` | 스펙 기반 유닛 테스트 · BenchmarkDotNet 벤치마크 |
| `Sandbox/` | 기능 인수 조건을 실행하는 콘솔 시나리오 |
| `Document/` | Obsidian 문서 vault (진입점: `Document/00-AI/CONTEXT.md`) |
| `Legacy/` | v1 참조 구현 (읽기 전용) |

소스 코드 및 이슈: [https://github.com/BIGSUNGG/DS_MessageProtocol](https://github.com/BIGSUNGG/DS_MessageProtocol)

## 라이선스

저장소 루트의 라이선스 파일을 따릅니다(없을 경우 저장소 기본 정책을 확인하세요).
