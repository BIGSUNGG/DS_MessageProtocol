using MessageProtocol.CodeGenerator.Reference;
using Microsoft.CodeAnalysis;

namespace MessageProtocol.CodeGenerator.Metadata
{
    /// <summary>직렬화 대상 멤버 하나의 메타데이터.</summary>
    internal class MemberMetadata
    {
        public ISymbol Symbol { get; }
        public string Name { get; }
        public ITypeSymbol Type { get; }
        public bool IsField { get; }
        public bool IsProperty { get; }
        public bool IsMessage { get; }

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
