using Microsoft.CodeAnalysis;
using System.Text;

namespace MessageProtocol.CodeGenerator
{
    /// <summary>
    /// 심볼에서 유일한 식별자 접미사를 만든다.
    /// 네임스페이스·중첩 타입 체인·제네릭 인자를 모두 포함해 동명 심볼이 겹치지 않게 하고,
    /// 이름 체계가 우연히 겹치면 사용 접미사 집합으로 구분자를 부여한다.
    /// </summary>
    internal static class SymbolNaming
    {
        public static string MakeUniqueSuffix(INamedTypeSymbol symbol, HashSet<string> usedSuffixes)
        {
            var sb = new StringBuilder();
            if (symbol.ContainingNamespace != null && !symbol.ContainingNamespace.IsGlobalNamespace)
            {
                sb.Append(symbol.ContainingNamespace.ToDisplayString()).Append('.');
            }

            AppendTypeName(sb, symbol);

            string suffix = SanitizeIdentifier(sb.ToString());
            string unique = suffix;
            for (int discriminator = 2; !usedSuffixes.Add(unique); discriminator++)
            {
                unique = $"{suffix}_{discriminator}";
            }

            return unique;
        }

        static void AppendTypeName(StringBuilder sb, INamedTypeSymbol symbol)
        {
            if (symbol.ContainingType != null)
            {
                AppendTypeName(sb, symbol.ContainingType);
                sb.Append('+');
            }

            // MetadataName 의 제네릭 차수 표기(`)는 유효한 식별자가 아니므로 이후 치환한다.
            sb.Append(symbol.MetadataName);

            foreach (var typeArgument in symbol.TypeArguments)
            {
                sb.Append('[');
                AppendTypeArgument(sb, typeArgument);
                sb.Append(']');
            }
        }

        static void AppendTypeArgument(StringBuilder sb, ITypeSymbol typeArgument)
        {
            switch (typeArgument)
            {
                case IArrayTypeSymbol arrayType:
                    AppendTypeArgument(sb, arrayType.ElementType);
                    sb.Append("Array");
                    if (arrayType.Rank > 1)
                    {
                        sb.Append(arrayType.Rank);
                    }
                    break;
                case ITypeParameterSymbol typeParameter:
                    sb.Append(typeParameter.Name);
                    break;
                case INamedTypeSymbol namedType:
                    AppendTypeName(sb, namedType);
                    break;
                default:
                    sb.Append(typeArgument.MetadataName);
                    break;
            }
        }

        static string SanitizeIdentifier(string raw)
        {
            var sb = new StringBuilder(raw.Length);
            foreach (char c in raw)
            {
                sb.Append(char.IsLetterOrDigit(c) ? c : '_');
            }

            return sb.ToString();
        }
    }
}
