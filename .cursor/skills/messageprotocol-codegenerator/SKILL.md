---
name: messageprotocol-codegenerator
description: Maintain the Roslyn generator in Source/MessageProtocol.CodeGenerator that emits MessageId, Serialize, Deserialize, helper methods, and module registration code. Use when a message type fails to generate, member inclusion is wrong, nested plain objects or lists need support, diagnostics need updates, or generated code disagrees with the runtime contract in MessageProtocol.Core.
---

# MessageProtocol Code Generator

## Use This Skill When

- Editing `Source/MessageProtocol.CodeGenerator`
- Fixing generated `Serialize` or `Deserialize` code
- Adding or changing message attributes such as `NonIdMessage`, `StandaloneMessage`, `GroupRootMessage`, or `GroupElementMessage`
- Changing `MessageId`, header/category encoding, or root/element hierarchy rules
- Adding support for new member types or collection types
- Debugging mismatches between generated code and `Source/MessageProtocol.Core/Serialize/MessageSerializer*`

## Quick Workflow

1. Start from `Generate/MessageCodeGenerator.cs` to confirm how the type is discovered and validated.
2. Read `Metadata/TypeMetadata.cs` when the issue is about included members, inheritance, `MessageId`, or category bits.
3. Read `Graph/SerializationGraph.cs` when the issue involves nested plain objects, lists, arrays, or helper-method generation.
4. Read `Generate/MessageSerializeCodeEmitter.Member.cs` when adding support for a new value shape.
5. Read `Generate/MessageSerializeCodeEmitter.Method.cs` when changing helper methods, reference tracking, or shared serialization behavior.
6. If the wire format changes, update the runtime side in `Source/MessageProtocol.Core/Serialize/MessageSerializer*` too.
7. Verify behavior with generator tests in `Test/MessageProtocol.CoreTests/Generator`.

## Rules Of Thumb

- Treat compile-time generation and runtime serialization as one system.
- New message attribute kinds usually require coordinated updates in `Reference/AttributeReferences.cs`, `Generate/MessageCodeGenerator.cs`, `Graph/SerializationGraph.cs`, and `Metadata/MemberMetadata.cs`.
- New collection support usually requires changes in both `Graph/SerializationGraph.cs` and `Generate/MessageSerializeCodeEmitter.Member.cs`.
- Header or `MessageId` changes are incomplete unless the runtime reader logic is updated too.
- Plain reference types are materialized with `new T()`, so parameterless construction must be possible.
- Current behavior collapses `null` string to empty string and `null` list/array to zero length; preserve or deliberately change this with tests.

## Fast Triage Map

- "Why is this type not generating?" -> `Generate/MessageCodeGenerator.cs`
- "Why is this member excluded?" -> `Metadata/TypeMetadata.cs`
- "Why is this nested type not getting helper methods?" -> `Graph/SerializationGraph.cs`
- "How do I add a new supported type?" -> `Generate/MessageSerializeCodeEmitter.Member.cs`
- "Why did runtime deserialize break?" -> `Source/MessageProtocol.Core/Serialize/MessageSerializer.Deserialize.cs`

## Validation

- Prefer updating or adding focused tests in `Test/MessageProtocol.CoreTests/Generator/GeneratorTest.cs`
- For wire-format changes, also inspect runtime serialization/deserialization paths
- When generated output looks wrong, use the debug dump path in `MessageCodeGenerator` if `C:\Debug` exists

## Additional Reference

- For architecture, caveats, and file-by-file guidance, see [reference.md](reference.md)
