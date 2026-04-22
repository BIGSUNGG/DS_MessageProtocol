---
name: messageprotocol-core
description: Maintain MessageProtocol runtime APIs in Source/MessageProtocol.Core: public message attributes, IMessageSerializable contracts, MessageSerializer registration and generic/object dispatch, MessageFlag, MessageCategory, and MessageId parsing. Use when working on runtime serialization or deserialization, registration/cache bugs, InvalidCastException or KeyNotFoundException paths, or public contract changes.
---

# MessageProtocol Core

## Use This Skill When

- Editing `Source/MessageProtocol.Core`
- Changing public message attributes such as `NonIdMessage`, `StandaloneMessage`, `GroupRootMessage`, `GroupElementMessage`, `MessageIgnore`, `MessageInclude`, or `MessageCategory`
- Changing `IMessageSerializable<T>` or `IHasIdMessageSerializable<T>`
- Modifying `MessageSerializer` registration, cache, serialize, or deserialize behavior
- Changing `MessageFlag`, category bits, or wire-header interpretation
- Debugging runtime failures caused by source generator output

## Quick Workflow

1. Start from `MessageTypeAttributes.cs` and `MessageMemberAttributes.cs` when the change is about the public annotation API.
2. Read `Serialize/IMessageSerializable.cs` when the change affects generated contract shape.
3. Read `Serialize/MessageSerializer.cs` for runtime registration and reflection-based `MessageId` lookup.
4. Read `Serialize/MessageSerializer.Serialize.cs` for generic-vs-object serialize dispatch and type-based cache behavior.
5. Read `Serialize/MessageSerializer.Deserialize.cs` for generic-vs-object deserialize rules, `MessageId` extraction, and ID-based cache behavior.
6. Read `MessageCategory.cs` and `../Shared/MessageFlag.cs` before changing first-byte header semantics.
7. If the public contract or header format changes, update `Source/MessageProtocol.CodeGenerator` too.
8. Verify with tests in `Test/MessageProtocol.CoreTests/Serialize` and generator validation tests when needed.

## Rules Of Thumb

- Treat `MessageProtocol.Core` as the runtime contract that the generator targets.
- Attribute names and metadata names are part of the generator integration surface; renames require generator updates.
- The first serialized byte is split into upper-nibble flags and lower-nibble category.
- `NonIdMessage` uses generic `Deserialize<T>`; object `Deserialize(byte[])` is only for standalone/group messages.
- Grouped or standalone object deserialization depends on prior runtime registration of generated types.
- If runtime dispatch breaks, inspect both caches and the `ReadMessageId` path before changing serializer logic.

## Fast Triage Map

- "Which attribute should I change?" -> `MessageTypeAttributes.cs` or `MessageMemberAttributes.cs`
- "Why is generated code not satisfying the runtime contract?" -> `Serialize/IMessageSerializable.cs`
- "Why is this type not deserializing by object?" -> `Serialize/MessageSerializer.Deserialize.cs`
- "Why is this type not registered?" -> `Serialize/MessageSerializer.cs`
- "Why did flag/category encoding change?" -> `../Shared/MessageFlag.cs` and `MessageCategory.cs`

## Validation

- Prefer focused runtime checks in `Test/MessageProtocol.CoreTests/Serialize/SerializeTest.cs`
- For contract or attribute changes, also run generator-side validation mentally against `Source/MessageProtocol.CodeGenerator`
- Keep `SKILL.md` high-level; use the reference for detailed module behavior

## Additional Reference

- For architecture, runtime behavior, and change guides, see [reference.md](reference.md)
