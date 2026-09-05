using Microsoft.CodeAnalysis;

namespace MessageProtocol.CodeGenerator
{
    internal static class DiagnosticDescriptors
    {
        const string Category = "MessageProtocol";

        public static readonly DiagnosticDescriptor MustBePartial = new(
            id: "MSGPROT001",
            title: "Message type must be partial",
            messageFormat: "The message type '{0}' must be partial",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor NestedContainingTypesMustBePartial = new(
            id: "MSGPROT002",
            title: "Nested message type's containing type(s) must be partial",
            messageFormat: "The message type '{0}' containing type(s) must be partial",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor ElementMessageMustHaveRoot = new(
            id: "MSGPROT003",
            title: "Element message must have a root message",
            messageFormat: "The element message type '{0}' must have a root message in its inheritance hierarchy",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor RootMessageCannotHaveRootParent = new(
            id: "MSGPROT004",
            title: "Root message cannot have a root message as parent",
            messageFormat: "The root message type '{0}' cannot have a root message in its parent hierarchy",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor MessageAttributeValueOutOfRange = new(
            id: "MSGPROT005",
            title: "Message attribute value is out of range",
            messageFormat: "Type '{0}' has invalid value '{2}' in '{1}'. Allowed range is 0 to 16777215 (2^24 - 1).",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor UnsupportedMemberType = new(
            id: "MSGPROT006",
            title: "Unsupported member type",
            messageFormat: "Member '{1}' has unsupported type '{0}' for MessageProtocol serialization",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor DuplicateMessageAttributes = new(
            id: "MSGPROT007",
            title: "Message attributes are mutually exclusive",
            messageFormat: "Message type '{0}' has multiple message attributes ({1}); they are mutually exclusive and code generation is skipped",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor InvalidGenericMessageDeclaration = new(
            id: "MSGPROT008",
            title: "Invalid GenericMessage declaration",
            messageFormat: "Type '{0}' has an invalid GenericMessage declaration: {1}",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor UnconstructibleMessageType = new(
            id: "MSGPROT010",
            title: "Message type must be constructible",
            messageFormat: "Message type '{0}' cannot be deserialized: it must be a concrete type with a parameterless constructor (abstract types and positional records are not supported)",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor NotAssignableMember = new(
            id: "MSGPROT011",
            title: "Message member must be assignable",
            messageFormat: "Member '{1}' (type '{0}') cannot be deserialized: it has no setter assignable from generated code (get-only, init-only and read-only members are not supported)",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor PolymorphicMemberSerializesByDeclaredType = new(
            id: "MSGPROT012",
            title: "Message member serializes by declared type",
            messageFormat: "Member '{1}' is declared as '{0}', which has derived message type(s) in this compilation. Serialization writes only '{0}' members, so assigning a derived instance silently drops its extra members. Declare '{0}' abstract to write the concrete element via runtime dispatch, declare the member as the concrete element type, or send the whole message through MessageSerializer.Serialize(object).",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);
    }
}
