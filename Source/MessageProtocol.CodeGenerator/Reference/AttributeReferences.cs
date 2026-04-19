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

        public AttributeReferences(Compilation compilation)
        {
            NonIdMessageAttributeType = compilation.GetTypeByMetadataName("MessageProtocol.NonIdMessageAttribute");
            GroupRootMessageAttributeType = compilation.GetTypeByMetadataName("MessageProtocol.GroupRootMessageAttribute");
            GroupElementMessageAttributeType = compilation.GetTypeByMetadataName("MessageProtocol.GroupElementMessageAttribute");
            StandaloneMessageAttributeType = compilation.GetTypeByMetadataName("MessageProtocol.StandaloneMessageAttribute");
            MessageIgnoreAttributeType = compilation.GetTypeByMetadataName("MessageProtocol.MessageIgnoreAttribute");
            MessageIncludeAttributeType = compilation.GetTypeByMetadataName("MessageProtocol.MessageIncludeAttribute");
            MessageCategoryAttributeType = compilation.GetTypeByMetadataName("MessageProtocol.MessageCategoryAttribute");
        }
    }
}
