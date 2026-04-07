namespace MessageProtocol.CodeGenerator.Metadata
{
    internal enum TypeDeclarationKind
    {
        Class,
        Struct,
        RecordClass,
        RecordStruct,
        Interface,
    }

    internal static class TypeDeclarationKindHelper
    {
        public static TypeDeclarationKind GetDeclarationKind(Microsoft.CodeAnalysis.INamedTypeSymbol symbol)
        {
            if (symbol.TypeKind == Microsoft.CodeAnalysis.TypeKind.Interface)
            {
                return TypeDeclarationKind.Interface;
            }

            if (symbol.IsRecord)
            {
                return symbol.TypeKind == Microsoft.CodeAnalysis.TypeKind.Struct
                    ? TypeDeclarationKind.RecordStruct
                    : TypeDeclarationKind.RecordClass;
            }

            return symbol.TypeKind == Microsoft.CodeAnalysis.TypeKind.Struct
                ? TypeDeclarationKind.Struct
                : TypeDeclarationKind.Class;
        }

        public static string GetDeclarationKeyword(TypeDeclarationKind declarationKind)
        {
            switch (declarationKind)
            {
                case TypeDeclarationKind.Struct:
                    return "struct";
                case TypeDeclarationKind.RecordClass:
                    return "record";
                case TypeDeclarationKind.RecordStruct:
                    return "record struct";
                case TypeDeclarationKind.Interface:
                    return "interface";
                default:
                    return "class";
            }
        }
    }
}
