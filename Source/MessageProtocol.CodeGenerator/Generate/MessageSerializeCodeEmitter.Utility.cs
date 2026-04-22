using MessageProtocol.CodeGenerator.Metadata;
using Microsoft.CodeAnalysis;
using System.Collections.Generic;

namespace MessageProtocol.CodeGenerator.Generate
{
    internal static partial class MessageSerializeCodeEmitter
    {
        static IEnumerable<MemberMetadata> GetAllMembers(TypeMetadata typeMeta)
        {
            var memberDict = new Dictionary<string, MemberMetadata>();

            if (typeMeta.BaseTypeMetadata != null)
            {
                foreach (var member in GetAllMembers(typeMeta.BaseTypeMetadata))
                {
                    memberDict[member.Name] = member;
                }
            }

            foreach (var member in typeMeta.Members)
            {
                memberDict[member.Name] = member;
            }

            return memberDict.Values;
        }

        static string GetTypeDisplayName(ITypeSymbol typeSymbol)
        {
            return typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        }

        static bool IsPrimitiveLike(ITypeSymbol typeSymbol)
        {
            if (typeSymbol.TypeKind == TypeKind.Enum)
            {
                return true;
            }

            switch (typeSymbol.SpecialType)
            {
                case SpecialType.System_Boolean:
                case SpecialType.System_Byte:
                case SpecialType.System_SByte:
                case SpecialType.System_Int16:
                case SpecialType.System_UInt16:
                case SpecialType.System_Int32:
                case SpecialType.System_UInt32:
                case SpecialType.System_Int64:
                case SpecialType.System_UInt64:
                case SpecialType.System_Single:
                case SpecialType.System_Double:
                case SpecialType.System_Decimal:
                case SpecialType.System_Char:
                case SpecialType.System_String:
                    return true;
                default:
                    return false;
            }
        }
    }
}
