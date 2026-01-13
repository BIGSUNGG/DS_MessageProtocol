using MessageProtocol.CodeGenerator;
using MessageProtocol.CodeGenerator.Reference;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;

namespace MessageProtocol.CodeGenerator.Metadata
{
    internal class TypeMetadata
    {
        public INamedTypeSymbol Symbol { get; set; }

        public bool IsStandaloneMessage { get; set; }
        public byte MessageStandaloneId { get; set; }

        public bool IsGroupedMessage { get; set; }
        public bool IsGroupedRootMessage { get; set; }
        public byte MessageRootId { get; set; }
        public bool IsGroupedElementMessage { get; set; }
        public ushort MessageElementId { get; set; }

        public TypeMetadata? BaseTypeMetadata { get; set; }
        public MemberMetadata[] Members { get; set; }

        public TypeMetadata(INamedTypeSymbol typeSymbol, AttributeReferences references)
        {
            Symbol = typeSymbol;

            var standaloneAttribute = typeSymbol.FindAttribute(references.MessageStandaloneAttributeType);
            IsStandaloneMessage = standaloneAttribute != null;

            if (standaloneAttribute != null)
            {
                IsStandaloneMessage = true;
                MessageStandaloneId = (byte)(standaloneAttribute.ConstructorArguments[0].Value ?? 0);

                IsGroupedRootMessage = false;
                IsGroupedElementMessage = false;
                IsGroupedMessage = false;
                MessageRootId = 0;
                MessageElementId = 0;
            }
            else
            {
                IsStandaloneMessage = false;
                MessageStandaloneId = 0;

                var rootAttribute = typeSymbol.FindAttribute(references.MessageGroupRootAttributeType);
                var elementAttribute = typeSymbol.FindAttribute(references.MessageGroupElementAttributeType);

                IsGroupedRootMessage = rootAttribute != null;
                IsGroupedElementMessage = elementAttribute != null;
                IsGroupedMessage = IsGroupedRootMessage || IsGroupedElementMessage;

                // MessageRootId 추출
                if (IsGroupedRootMessage && rootAttribute != null && rootAttribute.ConstructorArguments.Length > 0)
                {
                    var rootIdValue = rootAttribute.ConstructorArguments[0].Value;
                    if (rootIdValue != null)
                    {
                        MessageRootId = (byte)rootIdValue;
                        MessageElementId = ushort.MaxValue;
                    }
                }

                // MessageElementId 추출
                if (IsGroupedElementMessage && elementAttribute != null && elementAttribute.ConstructorArguments.Length > 0)
                {
                    var elementIdValue = elementAttribute.ConstructorArguments[0].Value;
                    if (elementIdValue != null)
                    {
                        MessageElementId = (ushort)elementIdValue;
                    }
                }
            }

            var baseTypeSymbol = typeSymbol.BaseType;
            if (baseTypeSymbol != null && baseTypeSymbol.SpecialType != SpecialType.System_Object)
            {
                BaseTypeMetadata = new TypeMetadata(baseTypeSymbol, references);
                
                // Element 메시지인데 RootId가 아직 설정되지 않았다면 부모에서 찾기
                if (IsGroupedElementMessage && MessageRootId == 0)
                {
                    // 부모가 Root 메시지인 경우 (MessageRootId가 0이어도 유효함)
                    if (BaseTypeMetadata.IsGroupedRootMessage)
                    {
                        MessageRootId = BaseTypeMetadata.MessageRootId; // 0일 수도 있음
                    }
                    // 부모가 Element 메시지이고 이미 RootId를 가지고 있는 경우 (재귀적으로 찾기)
                    else if (BaseTypeMetadata.IsGroupedElementMessage)
                    {
                        // 부모의 BaseTypeMetadata를 재귀적으로 확인
                        var parentBase = BaseTypeMetadata.BaseTypeMetadata;
                        while (parentBase != null)
                        {
                            if (parentBase.IsGroupedRootMessage)
                            {
                                MessageRootId = parentBase.MessageRootId;
                                break;
                            }
                            parentBase = parentBase.BaseTypeMetadata;
                        }
                    }
                }
            }
            else
            {
                BaseTypeMetadata = null;
            }

            Members = typeSymbol.GetMembers()
                // 필드 또는 프로퍼티만 찾기
                .Where(m => m is IFieldSymbol || m is IPropertySymbol)
                .Where(m => !m.IsStatic)
                // 포함/제외 어트리뷰트 처리 및 공개 멤버만
                .Where(m =>
                {
                    bool include = m.ContainAttribute(references.MessageIncludeAttributeType);
                    bool ignore = m.ContainAttribute(references.MessageIgnoreAttributeType);
                    if (ignore) return false;
                    if (include) return true;
                    return m.DeclaredAccessibility is Accessibility.Public;
                })
                .Select(m => new MemberMetadata(m))
                .ToArray();
        }

        public uint GetMessageId(TypeMetadata typeMeta)
        {
            return ((uint)typeMeta.MessageStandaloneId << 24) | ((uint)typeMeta.MessageRootId << 16) | (uint)typeMeta.MessageElementId;
        }

    }
}
