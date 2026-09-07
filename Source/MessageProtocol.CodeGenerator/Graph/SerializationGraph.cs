using MessageProtocol.CodeGenerator.Metadata;
using MessageProtocol.CodeGenerator.Reference;
using Microsoft.CodeAnalysis;

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
        readonly SerializableTypeModel[] _reachableTypes;

        SerializationGraph(
            SerializableTypeModel rootType,
            AttributeReferences references,
            Dictionary<ITypeSymbol, SerializableTypeModel> lookup,
            HashSet<string> usedHelperSuffixes,
            SerializableTypeModel[] reachableTypes)
        {
            RootType = rootType;
            _references = references;
            _lookup = lookup;
            _usedHelperSuffixes = usedHelperSuffixes;
            _reachableTypes = reachableTypes;
        }

        public SerializableTypeModel RootType { get; }

        /// <summary>
        /// 도달 가능 타입을 **타입 이름 오름차순**으로 반환한다. 이전에는 `Dictionary.Values` 열거에
        /// 의존해 헬퍼 메서드 방출 순서가 BCL 구현 세부에 실려 있었다(.NET Dictionary 는 제거가 없으면
        /// 삽입 순으로 열거하지만 이는 문서화된 규약이 아니다 — 감사 원장 LOW, 2026-09-08).
        /// 정렬은 생성 **텍스트 배치**만 안정화한다 — 와이어 바이트는 멤버 순서(`TypeMetadata.GetWireMembers`)
        /// 가 담당하므로 불변이다. 헬퍼 접미사는 그래프 수집 시점에 이미 확정되어 방출 순서와 무관하다.
        /// </summary>
        public IReadOnlyCollection<SerializableTypeModel> ReachableTypes => _reachableTypes;

        public static SerializationGraph Create(TypeMetadata rootType, AttributeReferences references)
        {
            var usedHelperSuffixes = new HashSet<string>();
            var rootModel = new SerializableTypeModel(rootType, SymbolNaming.MakeUniqueSuffix(rootType.Symbol, usedHelperSuffixes));
            var lookup = new Dictionary<ITypeSymbol, SerializableTypeModel>(SymbolEqualityComparer.Default)
            {
                [rootType.Symbol] = rootModel,
            };
            // Collect 는 인스턴스 메서드라 임시 그래프로 수집한 뒤, 정렬된 배열을 넣어 최종 그래프를 만든다.
            var collector = new SerializationGraph(rootModel, references, lookup, usedHelperSuffixes, Array.Empty<SerializableTypeModel>());
            collector.Collect(rootType);
            // 방출 순서 안정화: 타입 이름 기준 정렬(KI-4 계열 — BCL Dictionary 열거 순서 의존 제거).
            var sorted = lookup.Values
                .OrderBy(static model => model.TypeName, StringComparer.Ordinal)
                .ToArray();
            return new SerializationGraph(rootModel, references, lookup, usedHelperSuffixes, sorted);
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
            foreach (var member in TypeMetadata.GetWireMembers(typeMeta))
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

            var typeModel = new SerializableTypeModel(new TypeMetadata(namedType, _references), SymbolNaming.MakeUniqueSuffix(namedType, _usedHelperSuffixes));
            _lookup[namedType] = typeModel;

            Collect(typeModel.Metadata);
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
