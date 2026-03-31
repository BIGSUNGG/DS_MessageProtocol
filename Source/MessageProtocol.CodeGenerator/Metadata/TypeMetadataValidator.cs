using MessageProtocol.CodeGenerator.Reference;
using Microsoft.CodeAnalysis;

namespace MessageProtocol.CodeGenerator.Metadata
{
    internal static class TypeMetadataValidator
    {
        public static bool TryValidateMessageIdRange(
            INamedTypeSymbol typeSymbol,
            AttributeReferences references,
            out string attributeName,
            out string attributeValue)
        {
            var current = typeSymbol;
            while (current != null && current.SpecialType != SpecialType.System_Object)
            {
                if (!TryValidateSingleAttribute(current, references.MessageStandaloneAttributeType, out attributeName, out attributeValue) ||
                    !TryValidateSingleAttribute(current, references.MessageGroupRootAttributeType, out attributeName, out attributeValue) ||
                    !TryValidateSingleAttribute(current, references.MessageGroupElementAttributeType, out attributeName, out attributeValue))
                {
                    return false;
                }

                current = current.BaseType;
            }

            attributeName = string.Empty;
            attributeValue = string.Empty;
            return true;
        }

        static bool TryValidateSingleAttribute(
            INamedTypeSymbol typeSymbol,
            INamedTypeSymbol? attributeType,
            out string attributeName,
            out string attributeValue)
        {
            var attribute = typeSymbol.FindAttribute(attributeType);
            if (attribute == null || attribute.ConstructorArguments.Length == 0)
            {
                attributeName = string.Empty;
                attributeValue = string.Empty;
                return true;
            }

            var rawValue = attribute.ConstructorArguments[0].Value;
            if (!TypeMetadata.TryConvertToUInt32(rawValue, out uint value) || value > TypeMetadata.MaxMessageAttributeValue)
            {
                attributeName = attribute.AttributeClass?.Name ?? "MessageAttribute";
                attributeValue = rawValue?.ToString() ?? "null";
                return false;
            }

            attributeName = string.Empty;
            attributeValue = string.Empty;
            return true;
        }
    }
}
