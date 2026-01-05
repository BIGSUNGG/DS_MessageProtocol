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
}

