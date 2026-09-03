using MessageProtocol.CodeGenerator.Metadata;
using MessageProtocol.CodeGenerator.Reference;
using Microsoft.CodeAnalysis;
using System.Text;

namespace MessageProtocol.CodeGenerator.Graph
{
    /// <summary>
    /// 루트 메시지부터 멤버를 따라 도달 가능한 모든 직렬화 대상 타입을 수집한다.
    /// 소스에 정의된 class/struct 는 메시지 속성 없이도 중첩 페이로드 대상이 된다.
    /// </summary>
    internal sealed class SerializationGraph
    {
        readonly AttributeReferences _references;
        readonly Dictionary<ITypeSymbol, SerializableTypeModel> _lookup;
        readonly HashSet<string> _usedHelperSuffixes;

        SerializationGraph(
            SerializableTypeModel rootType,
            AttributeReferences references,
            Dictionary<ITypeSymbol, SerializableTypeModel> lookup,
            HashSet<string> usedHelperSuffixes)
        {
            RootType = rootType;
            _references = references;
            _lookup = lookup;
            _usedHelperSuffixes = usedHelperSuffixes;
        }

        public SerializableTypeModel RootType { get; }
        public IReadOnlyCollection<SerializableTypeModel> ReachableTypes => _lookup.Values;

        public static SerializationGraph Create(TypeMetadata rootType, AttributeReferences references)
        {
            var usedHelperSuffixes = new HashSet<string>();
            var rootModel = new SerializableTypeModel(rootType, MakeHelperSuffix(rootType.Symbol, usedHelperSuffixes));
            var lookup = new Dictionary<ITypeSymbol, SerializableTypeModel>(SymbolEqualityComparer.Default)
            {
                [rootType.Symbol] = rootModel,
            };
            var graph = new SerializationGraph(rootModel, references, lookup, usedHelperSuffixes);
            graph.Collect(rootType);
            return graph;
        }

        static string MakeHelperSuffix(INamedTypeSymbol symbol, HashSet<string> usedSuffixes)
        {
            var sb = new StringBuilder();
            if (symbol.ContainingNamespace != null && !symbol.ContainingNamespace.IsGlobalNamespace)
            {
                sb.Append(symbol.ContainingNamespace.ToDisplayString()).Append('.');
            }

            AppendTypeName(sb, symbol);

            // 서로 다른 심볼이 같은 헬퍼 이름을 가지면 생성 코드가 CS0111 로 깨지므로,
            // 이름 체계가 우연히 겹쳐도 구분자를 붙여 유일성을 보장한다.
            string suffix = SanitizeIdentifier(sb.ToString());
            string unique = suffix;
            for (int discriminator = 2; !usedSuffixes.Add(unique); discriminator++)
            {
                unique = $"{suffix}_{discriminator}";
            }

            return unique;
        }

        /// <summary>네임스페이스·중첩 타입 체인·제네릭 인자를 포함한 타입 이름을 재귀 구성한다.</summary>
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

        public bool IsMessageType(ITypeSymbol typeSymbol)
        {
            if (typeSymbol is not INamedTypeSymbol namedType)
            {
                return false;
            }

            return namedType.ContainAttribute(_references.NonIdMessageAttributeType)
                || namedType.ContainAttribute(_references.StandaloneMessageAttributeType)
                || namedType.ContainAttribute(_references.GroupRootMessageAttributeType)
                || namedType.ContainAttribute(_references.GroupElementMessageAttributeType);
        }

        public bool TryGetSerializableObjectType(ITypeSymbol typeSymbol, out SerializableTypeModel typeModel)
        {
            return _lookup.TryGetValue(typeSymbol, out typeModel);
        }

        /// <summary>배열 또는 List&lt;T&gt;/IList&lt;T&gt; 의 요소 타입을 반환한다.</summary>
        public static bool TryGetCollectionElementType(ITypeSymbol typeSymbol, out ITypeSymbol elementType)
        {
            if (typeSymbol is IArrayTypeSymbol arrayType)
            {
                elementType = arrayType.ElementType;
                return true;
            }

            if (typeSymbol is INamedTypeSymbol namedType && namedType.IsGenericType)
            {
                string genericTypeName = namedType.ConstructedFrom.ToDisplayString();
                if (genericTypeName.StartsWith("System.Collections.Generic.List<") ||
                    genericTypeName.StartsWith("System.Collections.Generic.IList<"))
                {
                    elementType = namedType.TypeArguments[0];
                    return true;
                }
            }

            elementType = null!;
            return false;
        }

        void Collect(TypeMetadata typeMeta)
        {
            foreach (var member in GetAllMembers(typeMeta))
            {
                Collect(member.Type);
            }
        }

        void Collect(ITypeSymbol typeSymbol)
        {
            if (TryGetCollectionElementType(typeSymbol, out var elementType))
            {
                Collect(elementType);
                return;
            }

            if (IsPrimitiveLike(typeSymbol))
            {
                return;
            }

            if (typeSymbol is not INamedTypeSymbol namedType || !IsSerializableObjectType(namedType))
            {
                return;
            }

            if (_lookup.ContainsKey(namedType))
            {
                return;
            }

            var typeModel = new SerializableTypeModel(new TypeMetadata(namedType, _references), MakeHelperSuffix(namedType, _usedHelperSuffixes));
            _lookup[namedType] = typeModel;

            Collect(typeModel.Metadata);
        }

        /// <summary>베이스 체인의 멤버를 이름 기준으로 병합한다 (파생이 그림자 제거).</summary>
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

        static bool IsSerializableObjectType(INamedTypeSymbol namedType)
        {
            if (namedType.TypeKind != TypeKind.Class &&
                namedType.TypeKind != TypeKind.Struct)
            {
                return false;
            }

            if (!namedType.Locations.Any(location => location.IsInSource))
            {
                return false;
            }

            if (namedType.IsAnonymousType)
            {
                return false;
            }

            // 역직렬화는 기본 생성자로 인스턴스를 만든다 — 추상 클래스·포지셔널 레코드 등
            // 접근 가능한 매개변수 없는 생성자가 없는 참조 타입은 멤버 단위 진단으로 거른다.
            if (namedType.TypeKind == TypeKind.Class && !HasAccessibleParameterlessConstructor(namedType))
            {
                return false;
            }

            return true;
        }

        /// <summary>생성 코드는 루트 메시지 partial 클래스에 배치되므로 생성자는 최소 internal 접근 가능이어야 한다.</summary>
        static bool HasAccessibleParameterlessConstructor(INamedTypeSymbol namedType)
        {
            if (namedType.IsAbstract)
            {
                return false;
            }

            foreach (var constructor in namedType.InstanceConstructors)
            {
                if (constructor.Parameters.Length != 0)
                {
                    continue;
                }

                return constructor.DeclaredAccessibility == Accessibility.Public ||
                       constructor.DeclaredAccessibility == Accessibility.Internal;
            }

            return false;
        }
    }
}
