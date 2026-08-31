using MessageProtocol;
using MessageProtocol.CodeGenerator.Reference;
using Microsoft.CodeAnalysis;

namespace MessageProtocol.CodeGenerator.Metadata
{
    /// <summary>메시지 타입 하나의 속성·멤버·계층 메타데이터.</summary>
    internal sealed class TypeMetadata
    {
        public const uint MaxMessageAttributeValue = MessageWireFormat.MessageIdValueMask;

        public INamedTypeSymbol Symbol { get; }
        public TypeDeclarationKind DeclarationKind { get; }
        public string DeclarationKeyword => TypeDeclarationKindHelper.GetDeclarationKeyword(DeclarationKind);

        public bool IsNonIdMessage { get; }
        public bool IsStandaloneMessage { get; }
        public bool IsGroupMessage { get; }
        public bool IsGroupRootMessage { get; }
        public bool IsGroupElementMessage { get; }

        public uint StandaloneMessageId { get; }
        public uint GroupRootMessageId { get; }
        public uint GroupElementMessageId { get; }

        /// <summary>헤더 하위 니블(0~15). MessageCategoryAttribute 가 없으면 0.</summary>
        public byte Category { get; }

        public TypeMetadata? BaseTypeMetadata { get; }
        public ContainingTypeMetadata[] ContainingTypes { get; }
        public MemberMetadata[] Members { get; }

        public TypeMetadata(INamedTypeSymbol typeSymbol, AttributeReferences references)
        {
            Symbol = typeSymbol;
            DeclarationKind = TypeDeclarationKindHelper.GetDeclarationKind(typeSymbol);
            ContainingTypes = GetContainingTypes(typeSymbol);

            var nonIdMessageAttribute = typeSymbol.FindAttribute(references.NonIdMessageAttributeType);
            var standaloneMessageAttribute = typeSymbol.FindAttribute(references.StandaloneMessageAttributeType);
            var groupRootMessageAttribute = typeSymbol.FindAttribute(references.GroupRootMessageAttributeType);
            var groupElementMessageAttribute = typeSymbol.FindAttribute(references.GroupElementMessageAttributeType);

            IsNonIdMessage = nonIdMessageAttribute != null;
            IsStandaloneMessage = standaloneMessageAttribute != null;
            IsGroupRootMessage = groupRootMessageAttribute != null;
            IsGroupElementMessage = groupElementMessageAttribute != null;
            IsGroupMessage = IsGroupRootMessage || IsGroupElementMessage;

            StandaloneMessageId = ReadMessageIdOrDefault(standaloneMessageAttribute);
            GroupRootMessageId = ReadMessageIdOrDefault(groupRootMessageAttribute);
            GroupElementMessageId = ReadMessageIdOrDefault(groupElementMessageAttribute);

            Category = ReadMessageCategoryOrDefault(typeSymbol.FindAttribute(references.MessageCategoryAttributeType));

            var baseTypeSymbol = typeSymbol.BaseType;
            if (baseTypeSymbol != null &&
                baseTypeSymbol.SpecialType != SpecialType.System_Object &&
                baseTypeSymbol.SpecialType != SpecialType.System_ValueType)
            {
                BaseTypeMetadata = new TypeMetadata(baseTypeSymbol, references);
            }

            // 무시 속성 > 포함 속성 > public 순으로 직렬화 대상을 고른다.
            Members = typeSymbol.GetMembers()
                .Where(m => m is IFieldSymbol || m is IPropertySymbol)
                .Where(m => !m.IsStatic)
                .Where(m =>
                {
                    bool ignore = m.ContainAttribute(references.MessageIgnoreAttributeType);
                    if (ignore) return false;
                    bool include = m.ContainAttribute(references.MessageIncludeAttributeType);
                    if (include) return true;
                    return m.DeclaredAccessibility == Accessibility.Public;
                })
                .Select(m => new MemberMetadata(m, references))
                .ToArray();
        }

        /// <summary>flags + category + id 값을 조립한 프로토콜 MessageId.</summary>
        public uint GetMessageId()
        {
            MessageFlag flags = MessageFlag.None;
            if (IsNonIdMessage) flags |= MessageFlag.NonIdMessage;
            if (IsStandaloneMessage) flags |= MessageFlag.Standalone;
            if (IsGroupRootMessage) flags |= MessageFlag.GroupRoot;
            if (IsGroupElementMessage) flags |= MessageFlag.GroupElement;

            return MessageWireFormat.ComposeMessageId(flags, Category, GetMessageIdValue());
        }

        uint GetMessageIdValue()
        {
            if (IsStandaloneMessage) return StandaloneMessageId;
            if (IsGroupElementMessage) return GroupElementMessageId;
            if (IsGroupRootMessage) return GroupRootMessageId;
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

        static byte ReadMessageCategoryOrDefault(AttributeData? attributeData)
        {
            if (attributeData == null || attributeData.ConstructorArguments.Length == 0)
            {
                return 0;
            }

            if (!TryConvertToUInt32(attributeData.ConstructorArguments[0].Value, out uint value))
            {
                return 0;
            }

            return (byte)(value & 0x0Fu);
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
            var containingTypes = new Stack<ContainingTypeMetadata>();
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
