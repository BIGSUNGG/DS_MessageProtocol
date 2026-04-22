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
        public string WritePayloadMethodName => $"__MessageProtocolWritePayload_{HelperSuffix}";
        public string PopulatePayloadMethodName => $"__MessageProtocolPopulatePayload_{HelperSuffix}";
        public string ReadPayloadMethodName => $"__MessageProtocolReadPayload_{HelperSuffix}";
        public string CreateInstanceMethodName => $"__MessageProtocolCreate_{HelperSuffix}";
    }
}