---
project: DS_MessageProtocol
type: reference
status: draft
tags: [configuration]
updated: 2026-07-11
---

# Configuration

빌드·런타임·패키징 설정.

## Directory.Build.props

| 파일 | 역할 |
|------|------|
| 루트 `Directory.Build.props` | 기본 `IsPackable=false` (Test·Benchmarks 등) |
| `Source/Directory.Build.props` | `IsPackable=true`, `TargetFramework=netstandard2.1`, Nullable, XML doc, README pack |

## 타깃 프레임워크

| 프로젝트 | TFM |
|----------|-----|
| Source 패키지 3종 | netstandard2.1 |
| MessageProtocol.Tests | net9.0 |
| MessageProtocol.Benchmarks | net9.0 |

## 패키징 특이점

- **MessageProtocol**: pack 시 CodeGenerator DLL을 `analyzers/dotnet/cs`에 포함.
- **CodeGenerator**: `IncludeBuildOutput=false`, `IsRoslynAnalyzer=true`, `MESSAGE_PROTOCOL_CODE_GENERATOR` 정의 (Shared를 internal로 컴파일).
- **Core**: `AllowUnsafeBlocks=true`; `System.Buffers`, `System.Memory` 참조.

## 런타임 옵션

별도 configuration 객체·환경 변수 설정은 없다. 동작은 메시지 속성·등록·호출 API로 결정된다. 버퍼 기본 용량 등은 `MessageWireFormat.DefaultStreamCapacity` 상수.

## 관련

- [[Packages]]
- [[Overview]]
- [[Getting-Started]]
