using Microsoft.CodeAnalysis;

namespace MessageProtocol.CodeGenerator.Reference
{
    internal class AttributeReferences
    {
        public INamedTypeSymbol? NonIdMessageAttributeType { get; set; }
        public INamedTypeSymbol? GroupRootMessageAttributeType { get; set; }
        public INamedTypeSymbol? GroupElementMessageAttributeType { get; set; }
        public INamedTypeSymbol? StandaloneMessageAttributeType { get; set; }
        public INamedTypeSymbol? MessageIgnoreAttributeType { get; set; }
        public INamedTypeSymbol? MessageIncludeAttributeType { get; set; }
        public INamedTypeSymbol? MessageCategoryAttributeType { get; set; }
        public INamedTypeSymbol? MessageSerializableInterfaceType { get; set; }
        public INamedTypeSymbol? HasIdMessageSerializableInterfaceType { get; set; }

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
        }
    }
}
