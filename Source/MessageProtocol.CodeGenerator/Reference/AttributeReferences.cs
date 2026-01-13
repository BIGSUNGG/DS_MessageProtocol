using Microsoft.CodeAnalysis;

namespace MessageProtocol.CodeGenerator.Reference
{
    internal class AttributeReferences
    {
        public INamedTypeSymbol? MessageGroupRootAttributeType { get; set; }
        public INamedTypeSymbol? MessageGroupElementAttributeType { get; set; }
        public INamedTypeSymbol? MessageStandaloneAttributeType { get; set; }
        public INamedTypeSymbol? MessageIgnoreAttributeType { get; set; }
        public INamedTypeSymbol? MessageIncludeAttributeType { get; set; }
               
        public AttributeReferences(Compilation compilation)
        {
            MessageGroupRootAttributeType = compilation.GetTypeByMetadataName("MessageProtocol.MessageGroupRootAttribute");
            MessageGroupElementAttributeType = compilation.GetTypeByMetadataName("MessageProtocol.MessageGroupElementAttribute");
            MessageStandaloneAttributeType = compilation.GetTypeByMetadataName("MessageProtocol.MessageStandaloneAttribute");
            MessageIgnoreAttributeType = compilation.GetTypeByMetadataName("MessageProtocol.MessageIgnoreAttribute");
            MessageIncludeAttributeType = compilation.GetTypeByMetadataName("MessageProtocol.MessageIncludeAttribute");
        }
    }
}
