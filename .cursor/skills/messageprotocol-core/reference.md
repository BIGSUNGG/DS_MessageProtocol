# MessageProtocol.Core Reference

## Purpose
`Source/MessageProtocol.Core` defines the public attribute API, the runtime serialization interfaces, and the dispatcher used by generated message code.

This module is not just a utility library. It is the runtime contract that `Source/MessageProtocol.CodeGenerator` targets. Many changes here are incomplete unless the generator is updated to match.

## Main Responsibilities
The core module does three things:

1. Exposes message and member attributes such as `NonIdMessage`, `StandaloneMessage`, `GroupRootMessage`, `GroupElementMessage`, `MessageIgnore`, `MessageInclude`, and `MessageCategory`
2. Defines the generated contract through `IMessageSerializable<T>` and `IHasIdMessageSerializable<T>`
3. Dispatches runtime serialization and deserialization through `MessageSerializer`

## File Map

- `MessageTypeAttributes.cs`
  - Public message-type attributes
  - Constructor-time range validation for 24-bit message IDs
- `MessageMemberAttributes.cs`
  - Member include/exclude annotations
- `MessageCategory.cs`
  - Public category enum used in the low nibble of the first byte
- `Serialize/IMessageSerializable.cs`
  - Static abstract runtime contract implemented by generated types
- `Serialize/MessageSerializer.cs`
  - Type registration and reflection-based `MessageId` lookup
- `Serialize/MessageSerializer.Serialize.cs`
  - Generic/object serialize dispatch and per-type invoker cache
- `Serialize/MessageSerializer.Deserialize.cs`
  - Generic/object deserialize dispatch, `MessageId` parsing, and per-ID invoker cache
- `../Shared/MessageFlag.cs`
  - Shared flag enum used by both runtime and generator

## Public Attribute API
`MessageTypeAttributes.cs` defines the message model exposed to consumers.

### Message Attributes

- `GroupRootMessageAttribute`
- `GroupElementMessageAttribute`
- `StandaloneMessageAttribute`
- `NonIdMessageAttribute`
- `MessageCategoryAttribute`

### Important Behavior

- Message ID values are restricted to `0 ~ 16777215 (2^24 - 1)`
- `GroupElementMessageAttribute` rejects `0`
- Attributes can currently target `class`, `struct`, and `interface`

If attribute constructor rules, names, or target allowances change, also inspect the generator side:

- `Source/MessageProtocol.CodeGenerator/Reference/AttributeReferences.cs`
- `Source/MessageProtocol.CodeGenerator/Generate/MessageCodeGenerator.cs`
- `Source/MessageProtocol.CodeGenerator/Metadata/TypeMetadata.cs`

### Member Attributes
`MessageMemberAttributes.cs` currently exposes:

- `MessageIgnoreAttribute`
- `MessageIncludeAttribute`

These are consumed by the generator to decide which fields and properties enter the wire payload.

## Message Flags And Category
Header semantics are split between:

- `../Shared/MessageFlag.cs`
- `MessageCategory.cs`

Current layout:

- First byte upper nibble: `MessageFlag`
- First byte lower nibble: `MessageCategory`

`MessageFlag` values:

- `NonIdMessage`
- `Standalone`
- `GroupRoot`
- `GroupElement`
- `StandaloneOrGroup`

`MessageCategory` currently provides `Category0` through `Category15`, plus `CategoryMask`.

If header semantics change, update both runtime parsing and generator emission together.

## Generated Runtime Contract
Generated types are expected to implement one of these interfaces from `Serialize/IMessageSerializable.cs`:

- `IMessageSerializable<T>`
  - Requires static abstract `Serialize(T)`
  - Requires static abstract `Deserialize(byte[])`
- `IHasIdMessageSerializable<T>`
  - Extends `IMessageSerializable<T>`
  - Requires static abstract `MessageId`

This matters because `MessageSerializer` relies on those contracts and reflective `MessageId` access. If interface shape changes, generated code will no longer compile or dispatch correctly.

## Registration Flow
Runtime registration lives in `Serialize/MessageSerializer.cs`.

Key points:

- Generated code calls `MessageSerializer.RegisterType(typeof(T))` from a `[ModuleInitializer]`
- Registration reflects `MessageId` from the generated type
- Registration primes serialize and deserialize invokers

The registry is part of the dispatch mechanism for object-based serialization/deserialization, especially for standalone and grouped messages.

## Serialize Flow
The main entry points are in `Serialize/MessageSerializer.Serialize.cs`.

### Generic Path
`Serialize<T>(T message)`:

- Rejects `null`
- If `message` implements `IHasIdMessageSerializable<T>`, it routes through object serialization
- Otherwise it calls `T.Serialize(message)` directly

Interpretation:

- `NonIdMessage` types stay on the generic path
- Standalone and grouped messages use the object-dispatch path

### Object Path
`Serialize(object message)`:

- Uses the runtime type of the object
- Looks up a cached non-generic invoker per `Type`
- Registers an invoker on demand if needed

The invoker is built with reflection over `GenericSerializeInvoker<T>` where `T : IMessageSerializable<T>`.

### Common Failure Mode
If the type does not satisfy `IMessageSerializable<T>`, the code throws an `InvalidOperationException` that points back to likely generator/configuration issues.

## Deserialize Flow
The main entry points are in `Serialize/MessageSerializer.Deserialize.cs`.

### Generic Path
`Deserialize<T>(byte[] data)`:

- Rejects null or empty payloads
- Reads the first byte to inspect `MessageFlag`
- If the message is standalone or grouped, it forwards to object `Deserialize(byte[])` and casts the result
- Otherwise it calls `T.Deserialize(data)` directly

Interpretation:

- Generic `Deserialize<T>` supports both paths, but grouped/standalone messages are still ultimately ID-dispatched

### Object Path
`Deserialize(byte[] data)`:

- Rejects null or empty payloads
- Rejects messages that are not standalone/group
- Reads the 4-byte message ID unless the `NonIdMessage` flag is set
- Looks up a cached non-generic invoker by `uint messageId`

This is why plain `NonIdMessage` payloads cannot be deserialized through the object overload.

### MessageId Parsing
`ReadMessageId(byte[] data)`:

- Reads the first byte
- Builds the upper 8 bits from that first byte
- If `NonIdMessage` is set, returns immediately
- Otherwise reads the remaining 3 bytes to form the 32-bit message ID

If wire-format rules change, this function must stay consistent with generated code in `MessageProtocol.CodeGenerator`.

## Runtime Caches
Current caches:

- Type -> serialize invoker
- MessageId -> deserialize invoker
- Registered types

When debugging runtime behavior, check whether the issue is:

- The type was never registered
- The invoker was not created
- The wrong `MessageId` was computed
- The wrong dispatch path was selected based on flags

## Key Integration With The Generator
The tight coupling points with `Source/MessageProtocol.CodeGenerator` are:

1. Attribute metadata names
2. `IMessageSerializable<T>` / `IHasIdMessageSerializable<T>` shape
3. `MessageFlag` and category encoding
4. The `MessageId` first-byte layout
5. The existence of a public static `MessageId` property on generated ID-carrying message types
6. The expectation that generated types call `MessageSerializer.RegisterType(...)`

A change in any of those areas usually requires edits in both modules.

## Important Existing Behaviors
These are worth preserving intentionally or changing with tests:

1. `NonIdMessage` object deserialization throws `InvalidCastException`
2. Empty payloads throw `ArgumentException`
3. Too-short grouped/standalone headers throw `ArgumentException`
4. Unknown registered message IDs throw `KeyNotFoundException`
5. Generic serialize chooses object dispatch only for ID-carrying message shapes

## Tests
Primary runtime tests live in `Test/MessageProtocol.CoreTests/Serialize/SerializeTest.cs`.

They currently cover:

- Group root, group element, standalone, and non-id round trips
- Header and category encoding behavior
- Generic vs object deserialize behavior
- Nested message members
- Plain reference-type, struct, collection, cycle, and shared-reference round trips
- Error cases such as empty data, too-short headers, and unregistered IDs

Generator validation tests in `Test/MessageProtocol.CoreTests/Generator/ValidateTest.cs` are also relevant when core-side changes affect validity rules.

## Change Guides

### Change Public Attributes
Inspect and likely update:

- `MessageTypeAttributes.cs`
- `MessageMemberAttributes.cs`
- `Source/MessageProtocol.CodeGenerator/Reference/AttributeReferences.cs`
- `Source/MessageProtocol.CodeGenerator/Metadata/TypeMetadata.cs`
- Related tests in generator and serialize suites

### Change Header Encoding Or Flag Semantics
Inspect and likely update:

- `../Shared/MessageFlag.cs`
- `MessageCategory.cs`
- `Serialize/MessageSerializer.Deserialize.cs`
- `Source/MessageProtocol.CodeGenerator/Metadata/TypeMetadata.cs`
- `Source/MessageProtocol.CodeGenerator/Generate/MessageSerializeCodeEmitter.Method.cs`

### Change Serialize/Deserialize Dispatch Rules
Inspect and likely update:

- `Serialize/MessageSerializer.Serialize.cs`
- `Serialize/MessageSerializer.Deserialize.cs`
- Generator-emitted interfaces and entry points if contract assumptions change
- Runtime tests for generic and object overload behavior

### Change Registration Behavior
Inspect and likely update:

- `Serialize/MessageSerializer.cs`
- Generated `[ModuleInitializer]` code in `Source/MessageProtocol.CodeGenerator/Generate/MessageSerializeCodeEmitter.Method.cs`

## Fast Debugging Order
When runtime serialization breaks, the fastest order is usually:

1. Confirm the generated type implements the expected interface
2. Confirm the generated type exposes the expected static `MessageId`
3. Confirm `RegisterType` was called
4. Confirm the first-byte flag/category layout matches expectations
5. Confirm the chosen generic/object dispatch path is the intended one
6. Confirm cache lookup is using the same `Type` or `MessageId` you expect

## One-Line Summary
`MessageProtocol.Core` is the public annotation and runtime dispatch layer that generated message types plug into; most meaningful changes here also affect the source generator.
