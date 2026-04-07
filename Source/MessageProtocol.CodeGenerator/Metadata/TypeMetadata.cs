using MessageProtocol.CodeGenerator.Reference;
using MessageProtocol;
using Microsoft.CodeAnalysis;
using System.Linq;

namespace MessageProtocol.CodeGenerator.Metadata
{
    internal sealed class TypeMetadata
    {
        const uint MessageIdValueMask = 0x00FF_FFFF;

        public const uint MaxMessageAttributeValue = MessageIdValueMask;

        public INamedTypeSymbol Symbol { get; }
        public TypeDeclarationKind DeclarationKind { get; }
        public string DeclarationKeyword => TypeDeclarationKindHelper.GetDeclarationKeyword(DeclarationKind);

        public bool IsMessage { get; }
        public bool IsStandaloneMessage { get; }
        public bool IsGroupedMessage { get; }
        public bool IsGroupedRootMessage { get; }
        public bool IsGroupedElementMessage { get; }

        public uint MessageStandaloneId { get; }
        public uint MessageRootId { get; }
        public uint MessageElementId { get; }

        public TypeMetadata? BaseTypeMetadata { get; }
        public ContainingTypeMetadata[] ContainingTypes { get; }
        public MemberMetadata[] Members { get; }

        public TypeMetadata(INamedTypeSymbol typeSymbol, AttributeReferences references)
        {
            Symbol = typeSymbol;
            DeclarationKind = TypeDeclarationKindHelper.GetDeclarationKind(typeSymbol);
            ContainingTypes = GetContainingTypes(typeSymbol);

            var messageAttribute = typeSymbol.FindAttribute(references.MessageAttributeType);
            var standaloneAttribute = typeSymbol.FindAttribute(references.MessageStandaloneAttributeType);
            var rootAttribute = typeSymbol.FindAttribute(references.MessageGroupRootAttributeType);
            var elementAttribute = typeSymbol.FindAttribute(references.MessageGroupElementAttributeType);

            IsMessage = messageAttribute != null;
            IsStandaloneMessage = standaloneAttribute != null;
            IsGroupedRootMessage = rootAttribute != null;
            IsGroupedElementMessage = elementAttribute != null;
            IsGroupedMessage = IsGroupedRootMessage || IsGroupedElementMessage;

            MessageStandaloneId = ReadMessageIdOrDefault(standaloneAttribute);
            MessageRootId = ReadMessageIdOrDefault(rootAttribute);
            MessageElementId = ReadMessageIdOrDefault(elementAttribute);

            var baseTypeSymbol = typeSymbol.BaseType;
            if (baseTypeSymbol != null &&
                baseTypeSymbol.SpecialType != SpecialType.System_Object &&
                baseTypeSymbol.SpecialType != SpecialType.System_ValueType)
            {
                BaseTypeMetadata = new TypeMetadata(baseTypeSymbol, references);
            }

            Members = typeSymbol.GetMembers()
                .Where(m => m is IFieldSymbol || m is IPropertySymbol)
                .Where(m => !m.IsStatic)
                .Where(m =>
                {
                    bool include = m.ContainAttribute(references.MessageIncludeAttributeType);
                    bool ignore = m.ContainAttribute(references.MessageIgnoreAttributeType);
                    if (ignore) return false;
                    if (include) return true;
                    return m.DeclaredAccessibility is Accessibility.Public;
                })
                .Select(m => new MemberMetadata(m))
                .ToArray();
        }

        public uint GetMessageId()
        {
            uint flags = 0;
            if (IsMessage)
            {
                flags |= (uint)MessageFlag.Message;
            }

            if (IsStandaloneMessage)
            {
                flags |= (uint)MessageFlag.Standalone;
            }

            if (IsGroupedRootMessage)
            {
                flags |= (uint)MessageFlag.GroupRoot;
            }

            if (IsGroupedElementMessage)
            {
                flags |= (uint)MessageFlag.GroupElement;
            }

            return (flags << 24) | (GetMessageIdValue() & MessageIdValueMask);
        }

        uint GetMessageIdValue()
        {
            if (IsStandaloneMessage)
            {
                return MessageStandaloneId;
            }

            if (IsGroupedElementMessage)
            {
                return MessageElementId;
            }

            if (IsGroupedRootMessage)
            {
                return MessageRootId;
            }

            return 0;
        }

        static uint ReadMessageIdOrDefault(AttributeData? attributeData)
        {
            if (attributeData == null || attributeData.ConstructorArguments.Length == 0)
            {
                return 0;
            }

            return TryConvertToUInt32(attributeData.ConstructorArguments[0].Value, out uint value)
                ? value
                : 0;
        }

        internal static bool TryConvertToUInt32(object? value, out uint result)
        {
            switch (value)
            {
                case byte byteValue:
                    result = byteValue;
                    return true;
                case sbyte sbyteValue when sbyteValue >= 0:
                    result = (uint)sbyteValue;
                    return true;
                case ushort ushortValue:
                    result = ushortValue;
                    return true;
                case short shortValue when shortValue >= 0:
                    result = (uint)shortValue;
                    return true;
                case uint uintValue:
                    result = uintValue;
                    return true;
                case int intValue when intValue >= 0:
                    result = (uint)intValue;
                    return true;
                case ulong ulongValue when ulongValue <= uint.MaxValue:
                    result = (uint)ulongValue;
                    return true;
                case long longValue when longValue >= 0 && longValue <= uint.MaxValue:
                    result = (uint)longValue;
                    return true;
                default:
                    result = 0;
                    return false;
            }
        }

        static ContainingTypeMetadata[] GetContainingTypes(INamedTypeSymbol typeSymbol)
        {
            var containingTypes = new System.Collections.Generic.Stack<ContainingTypeMetadata>();
            var current = typeSymbol.ContainingType;
            while (current != null)
            {
                containingTypes.Push(new ContainingTypeMetadata(current));
                current = current.ContainingType;
            }

            return containingTypes.ToArray();
        }
    }
}
