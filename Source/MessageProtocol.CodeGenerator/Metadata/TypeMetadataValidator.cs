using MessageProtocol.CodeGenerator.Reference;
using Microsoft.CodeAnalysis;

namespace MessageProtocol.CodeGenerator.Metadata
{
    /// <summary>속성 값 검증 (MSGPROT005 ID 값 · MSGPROT013 카테고리 값 · MSGPROT018 인자·종류 정합성).</summary>
    internal static class TypeMetadataValidator
    {
        /// <summary>헤더 category 니블 상한(<see cref="MessageWireFormat.NibbleMask"/>) — 와이어에서 4비트다.</summary>
        public const uint MaxCategoryValue = MessageWireFormat.NibbleMask;

        /// <summary>
        /// [Message] 생성자의 `MessageCategory` 인자가 4비트 니블 범위(0..15)인지 검사한다 (MSGPROT013).
        /// 벗어나면 이미터가 0x0F 로 **조용히 마스킹**해 와이어 MessageId 가 개발자 의도와 달라진다 —
        /// 다른 메시지의 ID 와 충돌하면 모듈 이니셜라이저에서 등록 충돌 예외(어셈블리 로드 실패)가 나고,
        /// 충돌이 없으면 피어가 의도하지 않은 카테고리로 라우팅한다 (Known-Issues KI-8).
        /// 속성이 `Inherited = false` 라 자기 선언만 본다.
        /// </summary>
        public static bool TryValidateCategoryRange(
            INamedTypeSymbol typeSymbol,
            AttributeReferences references,
            out string categoryValue)
        {
            categoryValue = string.Empty;

            var attribute = typeSymbol.FindAttribute(references.MessageAttributeType);
            if (attribute == null)
            {
                return true;
            }

            foreach (var argument in attribute.ConstructorArguments)
            {
                if (argument.Type?.TypeKind != TypeKind.Enum || argument.Type.Name == nameof(MessageKind))
                {
                    continue;
                }

                var rawValue = argument.Value;
                if (!TypeMetadata.TryConvertToUInt32(rawValue, out uint value) || value > MaxCategoryValue)
                {
                    categoryValue = rawValue?.ToString() ?? "null";
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// [Message] 생성자 인자와 종류의 정합성을 검사한다 (MSGPROT018):
        /// (1) kind 가 정의되지 않은 값(캐스트로 만든 4 초과 값)이 아니고,
        /// (2) NonId 가 id·category 인자를 받지 않는다 — NonId 헤더는 1바이트(id 슬롯 없음)이고
        /// category 니블은 0 으로 고정한다.
        /// </summary>
        public static bool TryValidateMessageAttributeConsistency(
            INamedTypeSymbol typeSymbol,
            AttributeReferences references,
            out string detail)
        {
            detail = string.Empty;

            var attribute = typeSymbol.FindAttribute(references.MessageAttributeType);
            if (attribute == null)
            {
                return true;
            }

            if (!TypeMetadata.TryDecodeMessageAttribute(attribute, out MessageKind kind, out uint manualId, out byte category))
            {
                uint rawKind = 0;
                foreach (var argument in attribute.ConstructorArguments)
                {
                    if (argument.Type?.Name == nameof(MessageKind)
                        && TypeMetadata.TryConvertToUInt32(argument.Value, out uint value))
                    {
                        rawKind = value;
                    }
                }

                detail = $"'{rawKind}' is not a defined MessageKind value";
                return false;
            }

            if (kind == MessageKind.NonId && (manualId != 0 || category != 0))
            {
                detail = "MessageKind.NonId cannot take id or category arguments";
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
            attributeName = string.Empty;
            attributeValue = string.Empty;

            var attribute = typeSymbol.FindAttribute(references.MessageAttributeType);
            if (attribute == null)
            {
                return true;
            }

            foreach (var argument in attribute.ConstructorArguments)
            {
                // enum 인자(kind·category)는 각각 MSGPROT018·MSGPROT013 담당,
                // 무인수·id 생략(해시 Id)은 검사할 정수 인자가 없다.
                if (argument.Type?.TypeKind == TypeKind.Enum)
                {
                    continue;
                }

                var rawValue = argument.Value;
                if (!TypeMetadata.TryConvertToUInt32(rawValue, out uint value) || value > TypeMetadata.MaxMessageAttributeValue)
                {
                    attributeName = attribute.AttributeClass?.Name ?? "MessageAttribute";
                    attributeValue = rawValue?.ToString() ?? "null";
                    return false;
                }
            }

            return true;
        }
    }
}
