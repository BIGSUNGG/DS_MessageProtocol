using Microsoft.CodeAnalysis;

namespace MessageProtocol.CodeGenerator;

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
}

