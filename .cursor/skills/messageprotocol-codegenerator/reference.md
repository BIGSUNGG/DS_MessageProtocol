# MessageProtocol.CodeGenerator Reference

## Purpose
`MessageProtocol.CodeGenerator` is a Roslyn incremental generator that reads message attributes from source types and emits `Serialize`, `Deserialize`, `MessageId`, and runtime registration code.

When changing this module, always think in two layers:

1. Compile time: how message types are discovered, validated, and turned into generated source
2. Runtime: how the generated code integrates with `Source/MessageProtocol.Core/Serialize/MessageSerializer`

## End-To-End Flow
The entry point is `Generate/MessageCodeGenerator.cs`.

1. The generator scans the full `Compilation` for message-attributed types.
2. It validates `partial` requirements, root/element hierarchy rules, and `MessageId` range.
3. It builds `Metadata/TypeMetadata.cs` for type/member/inheritance information.
4. It builds `Graph/SerializationGraph.cs` for reachable plain object helper types.
5. `Generate/MessageSerializeCodeEmitter*.cs` produces the final generated source text.
6. Generated types register themselves via `[ModuleInitializer]` and `MessageSerializer.RegisterType(...)`.

## Message Type Detection
The generator currently treats these attributes as message markers:

- `NonIdMessage`
- `StandaloneMessage`
- `GroupRootMessage`
- `GroupElementMessage`

Primary locations:

- Attribute symbol lookup: `Reference/AttributeReferences.cs`
- Message-type detection: `Generate/MessageCodeGenerator.cs`
- Graph-time message detection: `Graph/SerializationGraph.cs`
- Member-level message detection: `Metadata/MemberMetadata.cs`

If a new message attribute kind is introduced, update those locations together.

## Metadata Layer
The metadata model lives in `Metadata/*`.

- `TypeMetadata`
  - Computes message kind, category, and `MessageId`
  - Recursively captures base-type metadata
  - Selects serializable members
- `MemberMetadata`
  - Stores field/property identity and member type
- `ContainingTypeMetadata`
  - Reconstructs nested containing-type declarations for generated partial types
- `TypeDeclarationKind`
  - Preserves class/struct/record/record struct shape

### Member Inclusion Rules

- Exclude `static` members
- Exclude members with `MessageIgnore`
- Include members with `MessageInclude`
- Otherwise include only `public` fields and properties

If a member unexpectedly does not serialize, start at `TypeMetadata.Members`.

## MessageId Rules
`TypeMetadata.GetMessageId()` builds the wire header value.

- Upper 8 bits: message flags plus category
- Lower 24 bits: the numeric value from the message attribute

Validation:

- Allowed range: `0 ~ 16777215 (2^24 - 1)`
- Validation code: `Metadata/TypeMetadataValidator.cs`
- Diagnostics: `DiagnosticDescriptors.cs`

Important constraints:

- `GroupElementMessage` must have a `GroupRootMessage` somewhere in its inheritance chain
- `GroupRootMessage` must not inherit from another root message
- Nested message types require all containing types to be `partial`

## Serialization Graph
`Graph/SerializationGraph.cs` collects helper-target types that are not message types but still need generated read/write helpers.

These are plain source-defined object types used inside messages.

Collection rules:

- Skip primitives, enums, and `string`
- Skip message types because they use the runtime `MessageSerializer.Serialize/Deserialize` path
- Recurse into arrays, `List<T>`, and `IList<T>`
- Only generate helpers for source-defined `class` or `struct` types

The graph exists so a message can contain:

- A plain class member
- A plain struct member
- A plain object that contains more plain objects
- Cyclic or shared reference graphs for reference types

## Generated Code Layout
The generator emitter is split across `Generate/MessageSerializeCodeEmitter*.cs`.

- `Header`
  - Emits `using` statements and namespace wrappers
- `Define`
  - Emits partial type declarations
  - Emits `MessageId`
  - Inserts `Serialize`, `Deserialize`, and helper methods
- `Method`
  - Emits public entry methods
  - Emits `[ModuleInitializer]`
  - Emits shared context and helper methods
- `Member`
  - Emits per-member read/write logic
- `Utility`
  - Shared helpers for merged member enumeration and type display formatting

### Best File By Task

- Add support for a new member shape -> `MessageSerializeCodeEmitter.Member.cs`
- Change generated class signature or interfaces -> `MessageSerializeCodeEmitter.Define.cs`
- Change helper behavior or reference tracking -> `MessageSerializeCodeEmitter.Method.cs`
- Change discovery or validation rules -> `MessageCodeGenerator.cs`

## Currently Supported Member Types
As implemented in `Generate/MessageSerializeCodeEmitter.Member.cs`, the generator handles:

- Primitive values
  - `bool`, integer types, floating-point types, `decimal`, `char`, `string`
- Enums
- Message types
  - Serialized as nested byte arrays via `MessageSerializer.Serialize/Deserialize`
- Arrays
- `List<T>` and `IList<T>`
- Source-defined plain classes and structs
  - Serialized using generated helper methods

Unsupported types currently emit generated `// TODO: Serialize value (...)` or `// TODO: Deserialize value (...)` placeholders.

To add a new type family, extend `EmitSerializeValue` and `EmitDeserializeValue`.

## Reference Tracking
Plain reference-type helpers use the following in `Method.cs`:

- `__SerializeContext`
- `__DeserializeContext`
- `__WriteSizedReference`
- `__ReadSizedReference`

Intent:

- Shared objects are encoded as back-references
- Cyclic graphs can be reconstructed using object IDs

Value types instead use `__WriteSizedValue` and `__ReadSizedValue`, without identity tracking.

## Important Existing Behaviors
These are easy to accidentally break:

1. `null` string is serialized as `string.Empty`
2. `null` arrays and lists are serialized as length `0`, so null and empty are not distinguished
3. Plain reference types are materialized with `new T()`, so parameterless construction must be available
4. Message-typed members use the `MessageSerializer` path, not the plain-object helper path
5. Abstract `GroupRootMessage` types are treated as inheritance-only and do not emit generated source
6. Generated file names for nested types join containing type names with `_`

Many regressions come from unintentionally changing one of those assumptions.

## Change Guides

### Add A New Message Attribute Kind
Likely update:

- `Reference/AttributeReferences.cs`
- `Generate/MessageCodeGenerator.cs`
- `Graph/SerializationGraph.cs`
- `Metadata/MemberMetadata.cs`
- Possibly `Metadata/TypeMetadata.cs`
- Possibly runtime files in `Source/MessageProtocol.Core/Serialize/*`

### Add A New Primitive Or Special Value Type
Primary file:

- `Generate/MessageSerializeCodeEmitter.Member.cs`

Typical touchpoints:

- `TryEmitPrimitiveWrite`
- `TryEmitPrimitiveRead`
- Or special branches in `EmitSerializeValue` / `EmitDeserializeValue`

### Add A New Collection Type
Primary files:

- `Graph/SerializationGraph.cs`
- `Generate/MessageSerializeCodeEmitter.Member.cs`

Why:

- The graph layer must discover the element type
- The emitter layer must know how to write and read the collection

### Fix Nested Type Or Inheritance Declaration Issues
Inspect first:

- `Metadata/ContainingTypeMetadata.cs`
- `Metadata/TypeDeclarationKind.cs`
- `Generate/MessageSerializeCodeEmitter.Define.cs`

### Change MessageId Or Header Layout
Update together:

- `Metadata/TypeMetadata.cs`
- `Metadata/TypeMetadataValidator.cs`
- `Source/MessageProtocol.Core/Serialize/MessageSerializer.Deserialize.cs`
- `Source/MessageProtocol.Core/Serialize/MessageSerializer.cs`

Do not change only the generator side; the runtime reader must match.

## Tests
Primary generator tests live in `Test/MessageProtocol.CoreTests/Generator/GeneratorTest.cs`.

Current coverage includes:

- Each message attribute kind generates compilable code
- Nested partial type generation
- Struct message generation
- Plain nested-type helper generation

When modifying this module, prefer focused test updates for:

- New message rules
- New supported types
- Null/empty behavior changes
- Nested type, base type, or partial-related rules

## Debugging Tips
`MessageCodeGenerator` writes generated `.g.debug.cs` files under `C:\Debug` if that directory exists.

When generated output looks wrong, the fastest debug order is usually:

1. Verify `TypeMetadata` captured the expected members and inheritance
2. Verify `SerializationGraph` collected the expected helper types
3. Verify `MessageSerializeCodeEmitter.Member.cs` took the correct branch for the target type
4. Verify runtime `MessageSerializer` logic still matches the generated wire format

## One-Line Summary
This module discovers message types, models them as metadata, expands reachable plain object graphs, and injects serializer source code into the Roslyn compilation.
