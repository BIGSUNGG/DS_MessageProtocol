---
project: DS_MessageProtocol
type: architecture
status: draft
tags: [architecture, components]
updated: 2026-07-11
---

# Components

패키지/어셈블리 단위 컴포넌트 맵.

| NuGet 패키지 | 어셈블리 | 설명 |
|--------------|----------|------|
| **MessageProtocol** | (메타) | Core ProjectReference + CodeGenerator Analyzer. pack 시 `analyzers/dotnet/cs`에 생성기 DLL |
| **MessageProtocol.Core** | `MessageProtocol.Core` | 직렬화 런타임 API |
| **MessageProtocol.CodeGenerator** | `MessageProtocol.CodeGenerator` | Roslyn incremental source generator |

## 상세

| 컴포넌트 | 책임 | 의존 |
|----------|------|------|
| MessageProtocol | 앱용 단일 NuGet 진입점 | → Core (런타임), → CodeGenerator (Analyzer, `ReferenceOutputAssembly=false`) |
| MessageProtocol.Core | 공개 속성·인터페이스·`MessageSerializer`·버퍼 | System.Buffers, System.Memory; Shared Link |
| MessageProtocol.CodeGenerator | 메시지 타입 검증·코드 생성·진단(MSGPROT001–005) | Microsoft.CodeAnalysis.*; Shared Link |
| Shared (`Source/Shared`) | `MessageFlag`, `MessageWireFormat` (헤더/ID 규칙) | 없음 (Link Compile만) |
| MessageProtocol.Tests | 런타임 round-trip + 생성기/진단 | Core + CodeGenerator |
| MessageProtocol.Benchmarks | 직렬화 벤치마크 | MessageProtocol |

## MessageProtocol.Core 폴더

| 경로 | 내용 |
|------|------|
| `MessageTypeAttributes.cs` | `Standalone` / `GroupRoot` / `GroupElement` / `NonId` / `MessageCategory` |
| `MessageMemberAttributes.cs` | `MessageIgnore` / `MessageInclude` |
| `MessageCategory.cs` | Category0..15 |
| `Serialize/IMessageSerializable.cs` | `IMessageSerializable<T>`, `IHasIdMessageSerializable<T>` |
| `Serialize/MessageSerializer*.cs` | 등록·Serialize·Deserialize·Cache |
| `Serialize/MessageBufferWriter.cs` / `Reader.cs` | 와이어 버퍼 I/O |
| `Serialize/PooledBuffer.cs` | 풀링된 직렬화 결과 |

네임스페이스: `MessageProtocol`, `MessageProtocol.Serialize`.

## MessageProtocol.CodeGenerator 폴더

| 경로 | 내용 |
|------|------|
| `Generate/MessageCodeGenerator.cs` | `IIncrementalGenerator` 진입점 |
| `Generate/MessageSerializeCodeEmitter*.cs` | `*.g.cs` Emit (헤더·메서드·멤버) |
| `Metadata/` | `TypeMetadata`, `MemberMetadata`, Validator |
| `Graph/` | `SerializationGraph`, plain object 그래프 |
| `Reference/` | 속성·메타데이터 이름 |
| `DiagnosticDescriptors.cs` | MSGPROT001–005 |

네임스페이스: `MessageProtocol.CodeGenerator`, `.Generate`, `.Metadata`, `.Graph`, `.Reference`.

## Test 맵

| 영역 | 위치 | 커버 |
|------|------|------|
| Serialize | `Test/.../Serialize/` | round-trip, dispatch, NonId/object 제한, 동시성 |
| Generator | `Test/.../Generator/` | 속성별 생성, MSGPROT 진단 |
| Benchmarks | `Test/MessageProtocol.Benchmarks/` | 직렬화 성능 |

## 관련

- [[Overview]]
- [[Packages]]
- [[Data-Flow]]
- [[Public-API]]
