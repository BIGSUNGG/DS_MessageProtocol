using MessageProtocol.CodeGenerator.Metadata;
using System.Text;

namespace MessageProtocol.CodeGenerator.Generate
{
    internal sealed partial class SerializeCodeEmitter
    {
        TypeMetadata _typeMeta;

        public SerializeCodeEmitter(TypeMetadata typeMeta)
        {
            _typeMeta = typeMeta;
        }

        public string Emit()
        {
            StringBuilder sb = new StringBuilder();
            
            // Header: 네임스페이스와 using 추가
            sb.Append(Header.Emit(_typeMeta, out bool hasNamespace));
            
            // Class: 클래스 선언 및 상속
            sb.Append(Class.Emit(_typeMeta));
            
            // 네임스페이스 닫기
            if (hasNamespace)
            {
                sb.Append(Header.EmitCloseNamespace());
            }

            return sb.ToString();
        }
    }
}
