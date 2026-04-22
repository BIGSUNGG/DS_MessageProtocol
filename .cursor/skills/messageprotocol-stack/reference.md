# MessageProtocol Stack Reference

## Purpose
This skill is the top-level coordination guide for work that crosses the runtime contract in `Source/MessageProtocol.Core` and the source generator in `Source/MessageProtocol.CodeGenerator`.

Use it when the hardest part of the task is not "how do I edit this file?" but "which side owns the behavior, and what else must change with it?"

## Mental Model
The repository has one protocol stack split into two layers:

1. `MessageProtocol.Core`
   - Public attributes
   - `IMessageSerializable<T>` runtime contract
   - `MessageSerializer` runtime dispatch
   - Shared interpretation of message flags and category bits
2. `MessageProtocol.CodeGenerator`
   - Roslyn incremental generator
   - Type discovery and validation
   - Metadata extraction and object-graph expansion
   - Emission of generated `Serialize`, `Deserialize`, `MessageId`, and registration code

The layers are separate projects but one protocol surface.

## Shared Contract Surface
These behaviors are jointly owned:

- Attribute metadata names
- Which message kinds exist
- Member inclusion semantics
- `MessageFlag` values
- `MessageCategory` nibble usage
- `MessageId` layout
- Static interface shape for generated types
- Runtime registration expectations

If any of these change, assume both modules and tests may need updates.

## Ownership Matrix

### Mostly Core-Owned
Change usually starts in `Source/MessageProtocol.Core` when it affects:

- Public attribute declarations
- Range validation rules in attribute constructors
- `IMessageSerializable<T>` / `IHasIdMessageSerializable<T>`
- Runtime registration
- Generic vs object dispatch
- `ReadMessageId` behavior
- Exceptions thrown by runtime deserialize/serialize paths

Also inspect:

- `Source/MessageProtocol.CodeGenerator/Reference/AttributeReferences.cs`
- `Source/MessageProtocol.CodeGenerator/Metadata/TypeMetadata.cs`
- `Source/MessageProtocol.CodeGenerator/Generate/MessageSerializeCodeEmitter.Method.cs`

### Mostly Generator-Owned
Change usually starts in `Source/MessageProtocol.CodeGenerator` when it affects:

- Which types are discovered as messages
- Validation diagnostics
- Which members are serialized
- How nested plain objects are handled
- Which collection shapes are supported
- How helper methods are emitted

Also inspect:

- `Source/MessageProtocol.Core/Serialize/IMessageSerializable.cs`
- `Source/MessageProtocol.Core/Serialize/MessageSerializer*.cs`

### Always Shared
Treat these as mandatory two-sided changes:

- `MessageId` format
- First-byte flag/category encoding
- New message attribute kinds
- Registration flow between generated code and runtime
- Any change that alters how object deserialize chooses a target type

## Change Recipes

### 1. Add Or Change A Message Attribute
Inspect:

- `Source/MessageProtocol.Core/MessageTypeAttributes.cs`
- `Source/MessageProtocol.CodeGenerator/Reference/AttributeReferences.cs`
- `Source/MessageProtocol.CodeGenerator/Generate/MessageCodeGenerator.cs`
- `Source/MessageProtocol.CodeGenerator/Metadata/TypeMetadata.cs`
- Generator validation tests

Likely also runtime tests if the attribute affects dispatch or header bits.

### 2. Change Header Encoding
Inspect:

- `Source/Shared/MessageFlag.cs`
- `Source/MessageProtocol.Core/MessageCategory.cs`
- `Source/MessageProtocol.Core/Serialize/MessageSerializer.Deserialize.cs`
- `Source/MessageProtocol.CodeGenerator/Metadata/TypeMetadata.cs`
- `Source/MessageProtocol.CodeGenerator/Generate/MessageSerializeCodeEmitter.Method.cs`
- Serialize round-trip tests

Do not update only the runtime parser or only the generator emitter.

### 3. Change Runtime Dispatch
Inspect:

- `Source/MessageProtocol.Core/Serialize/MessageSerializer.cs`
- `Source/MessageProtocol.Core/Serialize/MessageSerializer.Serialize.cs`
- `Source/MessageProtocol.Core/Serialize/MessageSerializer.Deserialize.cs`
- Generated interface assumptions in `Source/MessageProtocol.CodeGenerator/Generate/MessageSerializeCodeEmitter.Define.cs`
- Generated registration code in `Source/MessageProtocol.CodeGenerator/Generate/MessageSerializeCodeEmitter.Method.cs`

### 4. Add A Supported Payload Shape
Inspect:

- `Source/MessageProtocol.CodeGenerator/Generate/MessageSerializeCodeEmitter.Member.cs`
- `Source/MessageProtocol.CodeGenerator/Graph/SerializationGraph.cs`
- Possibly `Source/MessageProtocol.Core/Serialize/MessageSerializer*` if dispatch assumptions change
- Generator compile tests and runtime round-trip tests

Examples:

- New primitive-like type
- New collection type
- New nested object shape
- Changed null-handling semantics

### 5. Fix End-To-End Round-Trip Bugs
Recommended order:

1. Confirm the message type was generated
2. Confirm the generated type implements the expected interface
3. Confirm `MessageId` and first-byte header match expectations
4. Confirm runtime registration happened
5. Confirm the correct generic/object overload path is being used
6. Confirm generated member code and helper methods match the intended payload shape

## Where To Start By Symptom

### "The type does not generate"
Start in:

- `Source/MessageProtocol.CodeGenerator/Generate/MessageCodeGenerator.cs`
- `Source/MessageProtocol.CodeGenerator/Metadata/TypeMetadata.cs`

Then confirm the public attribute exists and is named as expected in `Source/MessageProtocol.Core`.

### "Generated code compiles, but runtime deserialize fails"
Start in:

- `Source/MessageProtocol.Core/Serialize/MessageSerializer.Deserialize.cs`
- Generated `MessageId` and registration path

Then verify header and ID semantics against generator output.

### "A member is missing or serialized incorrectly"
Start in:

- `Source/MessageProtocol.CodeGenerator/Metadata/TypeMetadata.cs`
- `Source/MessageProtocol.CodeGenerator/Generate/MessageSerializeCodeEmitter.Member.cs`

Then confirm runtime expectations are still valid.

### "Object deserialize returns the wrong behavior"
Start in:

- `Source/MessageProtocol.Core/Serialize/MessageSerializer.Deserialize.cs`
- `Source/MessageProtocol.Core/Serialize/MessageSerializer.cs`

Then check generated `MessageId` and registration.

### "A diagnostic rule seems wrong"
Start in:

- `Source/MessageProtocol.CodeGenerator/DiagnosticDescriptors.cs`
- `Source/MessageProtocol.CodeGenerator/Generate/MessageCodeGenerator.cs`

Then verify the public rule actually belongs to generator validation rather than runtime attribute construction.

## Existing Behaviors Worth Preserving Intentionally
These behaviors cross the boundary and should not change accidentally:

1. First-byte upper nibble is `MessageFlag`; lower nibble is `MessageCategory`
2. `NonIdMessage` object deserialize is invalid
3. Generated standalone/group messages rely on runtime registration
4. Plain reference graphs preserve cycles and shared identity through generated helpers
5. Null and empty handling for strings and collections currently has protocol-visible behavior

If changing any of these, prefer paired generator/runtime tests.

## Test Strategy
Use both test suites when the contract crosses layers:

- `Test/MessageProtocol.CoreTests/Serialize/SerializeTest.cs`
  - Runtime dispatch
  - Header behavior
  - End-to-end round trips
  - Error cases
- `Test/MessageProtocol.CoreTests/Generator/GeneratorTest.cs`
  - Generated code compiles
  - Message kinds and shape support
  - Nested/plain object helper generation
- `Test/MessageProtocol.CoreTests/Generator/ValidateTest.cs`
  - Validation diagnostics
  - Partial/root/range constraints

## Recommended Working Pattern

1. Identify whether the task changes public contract, generator behavior, runtime behavior, or all three
2. Read the owning module first
3. Read the coupled module immediately after
4. Make coordinated edits
5. Verify with the smallest relevant generator and runtime tests

## Related Module Skills
Use these when you are ready to go deeper:

- `messageprotocol-core`
- `messageprotocol-codegenerator`

This stack skill is the router; the module skills contain lower-level guidance.
