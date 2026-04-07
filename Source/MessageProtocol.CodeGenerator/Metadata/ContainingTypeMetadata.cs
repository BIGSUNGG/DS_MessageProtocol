using Microsoft.CodeAnalysis;
using System.Linq;

namespace MessageProtocol.CodeGenerator.Metadata
{
    internal readonly struct ContainingTypeMetadata
    {
        public string DeclarationKeyword { get; }
        public string Name { get; }
        public string TypeParameters { get; }
        public string Constraints { get; }

        public ContainingTypeMetadata(INamedTypeSymbol symbol)
        {
            DeclarationKeyword = GetDeclarationKeyword(symbol);
            Name = symbol.Name;
            TypeParameters = GetTypeParameters(symbol);
            Constraints = GetTypeConstraints(symbol);
        }

        static string GetDeclarationKeyword(INamedTypeSymbol symbol)
        {
            switch (symbol.TypeKind)
            {
                case TypeKind.Struct:
                    return "struct";
                case TypeKind.Interface:
                    return "interface";
                default:
                    return symbol.IsRecord ? "record" : "class";
            }
        }

        static string GetTypeParameters(INamedTypeSymbol symbol)
        {
            if (symbol.TypeParameters.Length == 0)
            {
                return string.Empty;
            }

            return "<" + string.Join(", ", symbol.TypeParameters.Select(tp => tp.Name)) + ">";
        }

        static string GetTypeConstraints(INamedTypeSymbol symbol)
        {
            if (symbol.TypeParameters.Length == 0)
            {
                return string.Empty;
            }

            var constraints = symbol.TypeParameters
                .Select(GetConstraintClause)
                .Where(clause => !string.IsNullOrEmpty(clause));

            return string.Concat(constraints);
        }

        static string GetConstraintClause(ITypeParameterSymbol typeParameter)
        {
            var parts = new System.Collections.Generic.List<string>();

            if (typeParameter.HasReferenceTypeConstraint)
            {
                parts.Add("class");
            }

            if (typeParameter.HasValueTypeConstraint)
            {
                parts.Add("struct");
            }

            foreach (var constraintType in typeParameter.ConstraintTypes)
            {
                parts.Add(constraintType.ToDisplayString());
            }

            if (typeParameter.HasConstructorConstraint)
            {
                parts.Add("new()");
            }

            if (parts.Count == 0)
            {
                return string.Empty;
            }

            return $" where {typeParameter.Name} : {string.Join(", ", parts)}";
        }
    }
}
