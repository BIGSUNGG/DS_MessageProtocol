using Microsoft.CodeAnalysis;

namespace MessageProtocol.CodeGenerator.Metadata
{
    internal class MemberMetadata
    {
        public ISymbol Symbol { get; set; }
        public string Name { get; set; }
        public ITypeSymbol Type { get; set; }
        public bool IsField { get; set; }
        public bool IsProperty { get; set; }

        public MemberMetadata(ISymbol symbol)
        {
            Symbol = symbol;
            Name = symbol.Name;
            Type = symbol is IFieldSymbol field ? field.Type : ((IPropertySymbol)symbol).Type;
            IsField = symbol is IFieldSymbol;
            IsProperty = symbol is IPropertySymbol;
        }
    }
}

