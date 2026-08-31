using Microsoft.CodeAnalysis;

namespace MessageProtocol.CodeGenerator.Reference
{
    /// <summary>컴파일에서 자주 조회하는 심볼을 한 번에 묶어 둔 캐시.</summary>
    internal class AttributeReferences
    {
        public INamedTypeSymbol? NonIdMessageAttributeType { get; }
        public INamedTypeSymbol? GroupRootMessageAttributeType { get; }
        public INamedTypeSymbol? GroupElementMessageAttributeType { get; }
        public INamedTypeSymbol? StandaloneMessageAttributeType { get; }
        public INamedTypeSymbol? MessageIgnoreAttributeType { get; }
        public INamedTypeSymbol? MessageIncludeAttributeType { get; }
        public INamedTypeSymbol? MessageCategoryAttributeType { get; }
        public INamedTypeSymbol? MessageSerializableInterfaceType { get; }
        public INamedTypeSymbol? HasIdMessageSerializableInterfaceType { get; }
        public INamedTypeSymbol? GenericMessageAttributeType { get; }

        public AttributeReferences(Compilation compilation)
        {
            NonIdMessageAttributeType = compilation.GetTypeByMetadataName(MetadataNames.NonIdMessageAttribute);
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
