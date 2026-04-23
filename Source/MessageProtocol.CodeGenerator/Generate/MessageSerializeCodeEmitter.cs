using MessageProtocol.CodeGenerator.Metadata;
using MessageProtocol.CodeGenerator.Reference;
using MessageProtocol.CodeGenerator.Graph;
using System.Text;

namespace MessageProtocol.CodeGenerator.Generate
{
    internal static partial class MessageSerializeCodeEmitter
    {
        public static string Emit(TypeMetadata typeMeta, AttributeReferences attributeReferences)
        {
            var serializationGraph = SerializationGraph.Create(typeMeta, attributeReferences);
            StringBuilder sb = new StringBuilder();
            
            // Header: 네임스페이스와 using 추가
            sb.Append(Header.Emit(typeMeta, out bool hasNamespace));
            
            // Class: 클래스 선언 및 상속
            sb.Append(Define.Emit(typeMeta, serializationGraph, attributeReferences));
            
            // 네임스페이스 닫기
            if (hasNamespace)
            {
                sb.Append(Header.EmitCloseNamespace());
            }

            return sb.ToString();
        }
    }
}
