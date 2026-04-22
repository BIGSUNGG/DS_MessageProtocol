---
name: messageprotocol-stack
description: Coordinate end-to-end MessageProtocol changes across Source/MessageProtocol.Core and Source/MessageProtocol.CodeGenerator. Use when a task spans both modules, changes message attributes, MessageFlag, MessageCategory, MessageId, wire-header format, registration flow, or serialization behavior, or when it is unclear whether the fix belongs in runtime or the generator.
---

# MessageProtocol Stack

## Use This Skill When

- A change spans both `Source/MessageProtocol.Core` and `Source/MessageProtocol.CodeGenerator`
- You are changing message attributes, `MessageFlag`, `MessageCategory`, or `MessageId` rules
- You are adding a new supported payload shape and both runtime and generated code behavior matter
- You are debugging an end-to-end serialization or deserialization bug
- You are unsure whether the fix belongs in runtime, generator, or both

## First Decision

Classify the task before editing:

1. Public contract change:
   attributes, interfaces, header bits, `MessageId`, registration expectations
2. Generator-only shape change:
   supported members, helper emission, graph expansion, diagnostics
3. Runtime-only dispatch change:
   generic vs object overloads, registration cache, `MessageId` lookup
4. End-to-end bug:
   generated code compiles, but runtime round-trip fails or dispatch chooses the wrong path

If the task is category 1 or 4, inspect both modules before editing.

## Quick Workflow

1. Start with `Source/MessageProtocol.Core` to identify the intended public contract.
2. Confirm the generator mirrors that contract in `Source/MessageProtocol.CodeGenerator`.
3. If the issue is wire format, compare first-byte/header rules on both sides first.
4. If the issue is payload shape support, inspect generator member emission and serialization graph behavior.
5. If the issue is dispatch, inspect runtime registration plus generic/object serialize/deserialize branches.
6. Update focused tests in both `Test/MessageProtocol.CoreTests/Serialize` and `Test/MessageProtocol.CoreTests/Generator` when the change crosses the boundary.

## Coordination Rules

- Attribute names, metadata names, and interface shapes are shared contract surface.
- `MessageFlag` and category nibble semantics must match in runtime and generator.
- `MessageId` layout changes are incomplete unless both emitter and runtime parser change together.
- Generated ID-carrying messages depend on runtime registration through `MessageSerializer.RegisterType(...)`.
- Null/empty/reference-identity behavior should be treated as protocol behavior, not incidental implementation detail.

## Fast Triage Map

- "Should this be fixed in Core or Generator?" -> read [reference.md](reference.md) first
- "Header bytes or MessageId look wrong" -> inspect both modules immediately
- "A member type is unsupported" -> mostly generator, then verify runtime expectations
- "Deserialize by object fails" -> start at runtime, then confirm generated `MessageId` and registration
- "Validation or diagnostics are wrong" -> mostly generator, but verify the public attribute contract first

## Related Skills

- For generator detail, use `messageprotocol-codegenerator`
- For runtime/core detail, use `messageprotocol-core`
- For cross-module architecture and change matrix, use [reference.md](reference.md)
