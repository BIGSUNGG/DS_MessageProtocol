using MessageProtocol.CodeGenerator.Metadata;
using MessageProtocol.CodeGenerator.Reference;
using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;

namespace MessageProtocol.CodeGenerator.Graph
{
    internal sealed class SerializationGraph
    {
        readonly AttributeReferences _references;
        readonly Dictionary<ITypeSymbol, SerializableTypeModel> _lookup;

        SerializationGraph(
            SerializableTypeModel rootType,
            AttributeReferences references,
            Dictionary<ITypeSymbol, SerializableTypeModel> lookup)
        {
            RootType = rootType;
            _references = references;
            _lookup = lookup;
        }

        public SerializableTypeModel RootType { get; }
        public IReadOnlyCollection<SerializableTypeModel> ReachableTypes => _lookup.Values;

        public static SerializationGraph Create(TypeMetadata rootType, AttributeReferences references)
        {
            var rootModel = new SerializableTypeModel(rootType, "Root");
            var lookup = new Dictionary<ITypeSymbol, SerializableTypeModel>(SymbolEqualityComparer.Default);
            var graph = new SerializationGraph(rootModel, references, lookup);
            graph.Collect(rootType);
            return graph;
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

            if (IsPrimitiveLike(typeSymbol) || IsMessageType(typeSymbol))
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

            var typeModel = new SerializableTypeModel(
                new TypeMetadata(namedType, _references),
                $"{(namedType.ContainingNamespace == null || namedType.ContainingNamespace.IsGlobalNamespace ? "" : namedType.ContainingNamespace.ToDisplayString().Replace('.', '_') + "_")}{namedType.MetadataName}");
           

            _lookup[namedType] = typeModel;

            Collect(typeModel.Metadata);
        }

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

            return !namedType.IsAnonymousType;
        }
    }
}
