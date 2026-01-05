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
        public bool IsGroupedMessage { get; set; }
        public bool IsGroupedRootMessage { get; set; }
        public ushort MessageRootId { get; set; }
        public bool IsGroupedElementMessage { get; set; }
        public ushort MessageElementId { get; set; }
        public bool IsStandaloneMessage { get; set; }
        public MemberMetadata[] Members { get; set; }

        public TypeMetadata(INamedTypeSymbol typeSymbol, AttributeReferences references)
        {
            Symbol = typeSymbol;

            var standaloneAttribute = typeSymbol.FindAttribute(references.MessageStandaloneAttributeType);
            IsStandaloneMessage = standaloneAttribute != null;

            if (IsStandaloneMessage)
            {
                IsGroupedRootMessage = false;
                IsGroupedElementMessage = false;
                IsGroupedMessage = false;
                MessageRootId = 0;
                MessageElementId = 0;
            }
            else
            {
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
                        MessageRootId = (ushort)rootIdValue;
                        MessageElementId = ushort.MaxValue; // Root 메시지는 ElementId가 없음
                    }
                }

                // MessageElementId 추출
                if (IsGroupedElementMessage && elementAttribute != null && elementAttribute.ConstructorArguments.Length > 0)
                {
                    var elementIdValue = elementAttribute.ConstructorArguments[0].Value;
                    if (elementIdValue != null)
                    {
                        MessageElementId = (ushort)elementIdValue;

                        // Element일 경우 부모 클래스에서 RootId 추출
                        if (MessageRootId == 0) // 아직 RootId가 설정되지 않았다면 부모에서 찾기
                        {
                            var baseType = typeSymbol.BaseType;
                            while (baseType != null && baseType.SpecialType != SpecialType.System_Object)
                            {
                                var parentRootAttribute = baseType.FindAttribute(references.MessageGroupRootAttributeType);
                                if (parentRootAttribute != null && parentRootAttribute.ConstructorArguments.Length > 0)
                                {
                                    var parentRootIdValue = parentRootAttribute.ConstructorArguments[0].Value;
                                    if (parentRootIdValue != null)
                                    {
                                        MessageRootId = (ushort)parentRootIdValue;
                                        break; // 찾았으면 중단
                                    }
                                }
                                baseType = baseType.BaseType;
                            }
                        }
                    }
                }
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
    }
}
