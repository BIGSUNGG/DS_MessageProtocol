using MessageProtocol.CodeGenerator.Metadata;
using MessageProtocol.CodeGenerator.Reference;
using MessageProtocol.CodeGenerator.Graph;
using System.Collections.Immutable;
using System.Text;

namespace MessageProtocol.CodeGenerator.Generate
{
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
            StringBuilder sb = new StringBuilder();

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
    }
}
