using Microsoft.CodeAnalysis;
using System.Collections.Immutable;

namespace MessageProtocol.CodeGenerator.Reference
{
    /// <summary>컴파일에서 자주 조회하는 심볼을 한 번에 묶어 둔 캐시.</summary>
    internal class AttributeReferences
    {
        public INamedTypeSymbol? NonIdMessageAttributeType { get; }
        public INamedTypeSymbol? MessageAttributeType { get; }
        public INamedTypeSymbol? GroupRootMessageAttributeType { get; }
        public INamedTypeSymbol? GroupElementMessageAttributeType { get; }
        public INamedTypeSymbol? StandaloneMessageAttributeType { get; }
        public INamedTypeSymbol? MessageIgnoreAttributeType { get; }
        public INamedTypeSymbol? MessageIncludeAttributeType { get; }
        public INamedTypeSymbol? MessageCategoryAttributeType { get; }
        public INamedTypeSymbol? MessageSerializableInterfaceType { get; }
        public INamedTypeSymbol? HasIdMessageSerializableInterfaceType { get; }
        public INamedTypeSymbol? GenericMessageAttributeType { get; }

        /// <summary>
        /// 이 컴파일에 선언된 [Message] 타입이 상속하는 베이스 집합 — [Message] 베이스의 GroupRoot 자동 승격 근거.
        /// 참조 어셈블리의 베이스는 제외한다: 그 베이스의 와이어 정체성(플래그)은 선언부 어셈블리에서 이미 확정됐고,
        /// 소비 컴파일에서 다른 종류로 재해석하면 어셈블리 간 플래그 불일치가 된다.
        /// </summary>
        public ImmutableHashSet<INamedTypeSymbol> MessageDescendantBases { get; }

        /// <summary>이 컴파일에 [Message] 파생이 존재해 <paramref name="typeSymbol"/> 이 GroupRoot 로 승격되는지.</summary>
        public bool HasMessageDescendant(INamedTypeSymbol typeSymbol) => MessageDescendantBases.Contains(typeSymbol);

        public AttributeReferences(Compilation compilation, ImmutableHashSet<INamedTypeSymbol>? messageDescendantBases = null)
        {
            MessageDescendantBases = messageDescendantBases
                ?? ImmutableHashSet.Create<INamedTypeSymbol>(SymbolEqualityComparer.Default);
            NonIdMessageAttributeType = compilation.GetTypeByMetadataName(MetadataNames.NonIdMessageAttribute);
            MessageAttributeType = compilation.GetTypeByMetadataName(MetadataNames.MessageAttribute);
            GroupRootMessageAttributeType = compilation.GetTypeByMetadataName(MetadataNames.GroupRootMessageAttribute);
            GroupElementMessageAttributeType = compilation.GetTypeByMetadataName(MetadataNames.GroupElementMessageAttribute);
            StandaloneMessageAttributeType = compilation.GetTypeByMetadataName(MetadataNames.StandaloneMessageAttribute);
            MessageIgnoreAttributeType = compilation.GetTypeByMetadataName(MetadataNames.MessageIgnoreAttribute);
            MessageIncludeAttributeType = compilation.GetTypeByMetadataName(MetadataNames.MessageIncludeAttribute);
            MessageCategoryAttributeType = compilation.GetTypeByMetadataName(MetadataNames.MessageCategoryAttribute);
            MessageSerializableInterfaceType = compilation.GetTypeByMetadataName(MetadataNames.MessageSerializableInterface);
            HasIdMessageSerializableInterfaceType = compilation.GetTypeByMetadataName(MetadataNames.HasIdMessageSerializableInterface);
            GenericMessageAttributeType = compilation.GetTypeByMetadataName(MetadataNames.GenericMessageAttribute);
        }
    }
}
