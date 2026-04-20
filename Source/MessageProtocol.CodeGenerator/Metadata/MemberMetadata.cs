using Microsoft.CodeAnalysis;
using MessageProtocol.CodeGenerator.Reference;

namespace MessageProtocol.CodeGenerator.Metadata
{
    internal class MemberMetadata
    {
        public ISymbol Symbol { get; set; }
        public string Name { get; set; }
        public ITypeSymbol Type { get; set; }
        public bool IsField { get; set; }
        public bool IsProperty { get; set; }
        public bool IsMessage { get; set; }

        public MemberMetadata(ISymbol symbol, AttributeReferences references)
        {
            Symbol = symbol;
            Name = symbol.Name;
            Type = symbol is IFieldSymbol field ? field.Type : ((IPropertySymbol)symbol).Type;
            IsField = symbol is IFieldSymbol;
            IsProperty = symbol is IPropertySymbol;
            IsMessage = IsMessageType(Type, references);
        }

        static bool IsMessageType(ITypeSymbol typeSymbol, AttributeReferences references)
        {
            if (typeSymbol is not INamedTypeSymbol namedType)
            {
                return false;
            }

            return namedType.ContainAttribute(references.NonIdMessageAttributeType)
                || namedType.ContainAttribute(references.StandaloneMessageAttributeType)
                || namedType.ContainAttribute(references.GroupRootMessageAttributeType)
                || namedType.ContainAttribute(references.GroupElementMessageAttributeType);
        }
    }
}

