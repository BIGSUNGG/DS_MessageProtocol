using MessageProtocol.CodeGenerator.Reference;
using Microsoft.CodeAnalysis;

namespace MessageProtocol.CodeGenerator.Metadata
{
    /// <summary>속성 값 범위 검증 (MSGPROT005 ID 값 · MSGPROT013 카테고리 값). ID 는 상속 계층 전체를 검사한다.</summary>
    internal static class TypeMetadataValidator
    {
        /// <summary>헤더 category 니블 상한(<see cref="MessageWireFormat.NibbleMask"/>) — 와이어에서 4비트다.</summary>
        public const uint MaxCategoryValue = MessageWireFormat.NibbleMask;

        /// <summary>
        /// `MessageCategory` 값이 4비트 니블 범위(0..15)인지 검사한다 (MSGPROT013).
        /// 벗어나면 이미터가 0x0F 로 **조용히 마스킹**해 와이어 MessageId 가 개발자 의도와 달라진다 —
        /// 다른 메시지의 ID 와 충돌하면 모듈 이니셜라이저에서 등록 충돌 예외(어셈블리 로드 실패)가 나고,
        /// 충돌이 없으면 피어가 의도하지 않은 카테고리로 라우팅한다 (Known-Issues KI-8).
        /// 속성이 `Inherited = false` 라 베이스 계층을 걸을 필요 없이 자기 선언만 본다.
        /// </summary>
        public static bool TryValidateCategoryRange(
            INamedTypeSymbol typeSymbol,
            AttributeReferences references,
            out string categoryValue)
        {
            categoryValue = string.Empty;

            var attribute = typeSymbol.FindAttribute(references.MessageCategoryAttributeType);
            if (attribute == null || attribute.ConstructorArguments.Length == 0)
            {
                return true;
            }

            var rawValue = attribute.ConstructorArguments[0].Value;
            if (!TypeMetadata.TryConvertToUInt32(rawValue, out uint value) || value > MaxCategoryValue)
            {
                categoryValue = rawValue?.ToString() ?? "null";
                return false;
            }

            return true;
        }

        public static bool TryValidateMessageIdRange(
            INamedTypeSymbol typeSymbol,
            AttributeReferences references,
            out string attributeName,
            out string attributeValue)
        {
            var current = typeSymbol;
            while (current != null && current.SpecialType != SpecialType.System_Object)
            {
                if (!TryValidateSingleAttribute(current, references.StandaloneMessageAttributeType, out attributeName, out attributeValue) ||
                    !TryValidateSingleAttribute(current, references.GroupRootMessageAttributeType, out attributeName, out attributeValue) ||
                    !TryValidateSingleAttribute(current, references.GroupElementMessageAttributeType, out attributeName, out attributeValue))
                {
                    return false;
                }

                current = current.BaseType;
            }

            attributeName = string.Empty;
            attributeValue = string.Empty;
            return true;
        }

        static bool TryValidateSingleAttribute(
            INamedTypeSymbol typeSymbol,
            INamedTypeSymbol? attributeType,
            out string attributeName,
            out string attributeValue)
        {
            var attribute = typeSymbol.FindAttribute(attributeType);
            if (attribute == null || attribute.ConstructorArguments.Length == 0)
            {
                attributeName = string.Empty;
                attributeValue = string.Empty;
                return true;
            }

            var rawValue = attribute.ConstructorArguments[0].Value;
            if (!TypeMetadata.TryConvertToUInt32(rawValue, out uint value) || value > TypeMetadata.MaxMessageAttributeValue)
            {
                attributeName = attribute.AttributeClass?.Name ?? "NonIdMessageAttribute";
                attributeValue = rawValue?.ToString() ?? "null";
                return false;
            }

            attributeName = string.Empty;
            attributeValue = string.Empty;
            return true;
        }
    }
}
