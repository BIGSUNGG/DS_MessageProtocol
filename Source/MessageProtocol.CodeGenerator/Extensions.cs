using Microsoft.CodeAnalysis;
using System.Linq;

namespace MessageProtocol.CodeGenerator
{
    internal static class Extensions
    {
        public static bool ContainAttribute(this ISymbol self, INamedTypeSymbol? attributeSymbol)
        {
            if (attributeSymbol == null) return false;
            return self.GetAttributes().Any(attr => SymbolEqualityComparer.Default.Equals(attr.AttributeClass, attributeSymbol));
        }

        public static AttributeData? FindAttribute(this ISymbol self, INamedTypeSymbol? attributeSymbol)
        {
            if (attributeSymbol == null) return null;
            return self.GetAttributes().FirstOrDefault(attr => SymbolEqualityComparer.Default.Equals(attr.AttributeClass, attributeSymbol));
        }
    }
}
