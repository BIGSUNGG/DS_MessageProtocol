using MessageProtocol.CodeGenerator.Metadata;
using Microsoft.CodeAnalysis;

namespace MessageProtocol.CodeGenerator.Graph
{
    /// <summary>직렬화 그래프에 포함된 타입 하나와 그 헬퍼 메서드 이름 규칙.</summary>
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

        /// <summary>멤버 본문을 쓰는 헬퍼.</summary>
        public string WritePayloadMethodName => $"__WritePayload_{HelperSuffix}";
        /// <summary>기존 인스턴스에 멤버를 채우는 헬퍼.</summary>
        public string PopulatePayloadMethodName => $"__PopulatePayload_{HelperSuffix}";
        /// <summary>값 타입 본문을 읽어 새 인스턴스를 반환하는 헬퍼.</summary>
        public string ReadPayloadMethodName => $"__ReadPayload_{HelperSuffix}";
        /// <summary>참조 타입 역직렬화 전 빈 인스턴스를 만드는 헬퍼.</summary>
        public string CreateInstanceMethodName => $"__CreateInstance_{HelperSuffix}";
    }
}
