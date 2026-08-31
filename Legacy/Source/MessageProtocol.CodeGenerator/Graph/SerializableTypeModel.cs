using MessageProtocol.CodeGenerator.Metadata;
using Microsoft.CodeAnalysis;

namespace MessageProtocol.CodeGenerator.Graph
{
    internal sealed class SerializableTypeModel
    {
        public SerializableTypeModel(TypeMetadata metadata, string helperSuffix)
        {
            Metadata = metadata;
            HelperSuffix = helperSuffix;
        }

        public TypeMetadata Metadata { get; }
        public string HelperSuffix { get; }
        public bool IsReferenceType => Metadata.Symbol.IsReferenceType;
        public string TypeName => Metadata.Symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        // 현재 타입의 payload(멤버 본문) 직렬화 코드를 생성할 때 사용하는 헬퍼 메서드 이름
        public string WritePayloadMethodName => $"__WritePayload_{HelperSuffix}";
        // 역직렬화 시 이미 생성된 인스턴스에 멤버 값을 채워 넣는 헬퍼 메서드 이름
        public string PopulatePayloadMethodName => $"__PopulatePayload_{HelperSuffix}";
        // 값 타입 payload를 읽어 새 인스턴스를 반환하는 헬퍼 메서드 이름
        public string ReadPayloadMethodName => $"__ReadPayload_{HelperSuffix}";
        // 참조 타입 역직렬화 전에 빈 인스턴스를 만드는 헬퍼 메서드 이름
        public string CreateInstanceMethodName => $"__CreateInstance_{HelperSuffix}";
    }
}