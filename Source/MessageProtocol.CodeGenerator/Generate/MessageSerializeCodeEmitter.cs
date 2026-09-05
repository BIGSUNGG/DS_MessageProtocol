using MessageProtocol.CodeGenerator.Graph;
using MessageProtocol.CodeGenerator.Metadata;
using MessageProtocol.CodeGenerator.Reference;
using Microsoft.CodeAnalysis;
using System.Collections.Immutable;
using System.Text;

namespace MessageProtocol.CodeGenerator.Generate
{
    /// <summary>메시지 타입 하나에 대한 생성 코드 이미터.</summary>
    internal static partial class MessageSerializeCodeEmitter
    {
        public static bool TryEmit(
            TypeMetadata typeMeta,
            AttributeReferences attributeReferences,
            bool hasCollectionsMarshal,
            out string? code,
            out ImmutableArray<UnsupportedMemberInfo> unsupportedMembers)
        {
            var state = new EmitState(hasCollectionsMarshal);
            var serializationGraph = SerializationGraph.Create(typeMeta, attributeReferences);
            var sb = new StringBuilder();

            sb.Append(Header.Emit(typeMeta, out bool hasNamespace));
            sb.Append(Define.Emit(typeMeta, serializationGraph, attributeReferences, state));

            if (hasNamespace)
            {
                sb.Append(Header.EmitCloseNamespace());
            }

            if (state.UnsupportedMembers.Count > 0)
            {
                code = null;
                unsupportedMembers = state.UnsupportedMembers.ToImmutableArray();
                return false;
            }

            code = sb.ToString();
            unsupportedMembers = ImmutableArray<UnsupportedMemberInfo>.Empty;
            return true;
        }

        static string GetTypeDisplayName(ITypeSymbol typeSymbol)
        {
            return typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        }
    }
}
