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

        /// <summary>생성 코드 선언·시그니처에 쓰는 이름 (타입 매개변수 포함, 예: <c>Msg&lt;T&gt;</c>).</summary>
        public string DeclarationName => Symbol.Name + (Symbol.TypeParameters.Length == 0
            ? string.Empty
            : "<" + string.Join(", ", Symbol.TypeParameters.Select(static tp => tp.Name)) + ">");

        /// <summary>
        /// 자동 등록([ModuleInitializer]) 가능 여부. 제네릭 타입·제네릭 컨테이닝 타입 안의 타입은 불가능하다.
        /// </summary>
        public bool CanUseModuleInitializer => !Symbol.IsGenericType
            && ContainingTypes.All(static c => string.IsNullOrEmpty(c.TypeParameters));

        /// <summary>헤더 하위 니블(0~15). MessageCategoryAttribute 가 없으면 0.</summary>
        public byte Category { get; }

        public TypeMetadata? BaseTypeMetadata { get; }
        public ContainingTypeMetadata[] ContainingTypes { get; }
        public MemberMetadata[] Members { get; }
        public GenericConstructionMetadata[] GenericConstructions { get; }

        /// <summary>
        /// GenericMessage 구성이 선언된 제네릭 메시지인지 여부.
        /// true 면 헤더 플래그는 Generic(0), 와이어에 구성 클래스 ID 3바이트가 따라온다.
        /// </summary>
        public bool IsGenericMessageDeclaration => Symbol.IsGenericType && GenericConstructions.Length > 0;

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

            GenericConstructions = GetGenericConstructions(typeSymbol, references);

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
            MessageFlag flags;
            if (IsGenericMessageDeclaration)
            {
                // 제네릭 메시지는 전용 헤더 플래그(0) — 구성 클래스 ID가 헤더 뒤에 따라온다.
                flags = MessageFlag.Generic;
            }
            else
            {
                flags = MessageFlag.None;
                if (IsNonIdMessage) flags |= MessageFlag.NonIdMessage;
                if (IsStandaloneMessage) flags |= MessageFlag.Standalone;
                if (IsGroupRootMessage) flags |= MessageFlag.GroupRoot;
                if (IsGroupElementMessage) flags |= MessageFlag.GroupElement;
            }

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

        static GenericConstructionMetadata[] GetGenericConstructions(INamedTypeSymbol typeSymbol, AttributeReferences references)
        {
            if (references.GenericMessageAttributeType == null)
            {
                return Array.Empty<GenericConstructionMetadata>();
            }

            var list = new List<GenericConstructionMetadata>();
            foreach (var attribute in typeSymbol.GetAttributes())
            {
                if (!SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, references.GenericMessageAttributeType))
                {
                    continue;
                }

                ITypeSymbol[] typeArguments;
                if (attribute.ConstructorArguments.Length == 0 || attribute.ConstructorArguments[0].Kind != TypedConstantKind.Array)
                {
                    typeArguments = Array.Empty<ITypeSymbol>();
                }
                else
                {
                    typeArguments = attribute.ConstructorArguments[0].Values
                        .Where(static v => v.Kind == TypedConstantKind.Type && v.Value is ITypeSymbol)
                        .Select(static v => (ITypeSymbol)v.Value!)
                        .ToArray();
                }

                uint classId = 0;
                foreach (var named in attribute.NamedArguments)
                {
                    if (named.Key == nameof(GenericConstructionMetadata.ClassId)
                        && named.Value.Kind == TypedConstantKind.Primitive
                        && named.Value.Value is uint parsed)
                    {
                        classId = parsed;
                    }
                }

                list.Add(new GenericConstructionMetadata(typeArguments, classId));
            }

            return list.ToArray();
        }
    }

    /// <summary>GenericMessage 속성으로 선언된 닫힌 제네릭 구성 하나의 메타데이터.</summary>
    internal sealed class GenericConstructionMetadata
    {
        public GenericConstructionMetadata(ITypeSymbol[] typeArguments, uint classId)
        {
            TypeArguments = typeArguments;
            ClassId = classId;
        }

        public ITypeSymbol[] TypeArguments { get; }

        /// <summary>헤더에 기록되는 구성 식별자 (1 .. 2^24-1). 0 은 미설정·잘못된 선언.</summary>
        public uint ClassId { get; }
    }
}
