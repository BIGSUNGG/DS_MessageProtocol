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
                                        MessageRootId = (byte)parentRootIdValue;
                                        break; // 찾았으면 중단
                                    }
                                }
                                baseType = baseType.BaseType;
                            }
                        }
                    }
                }
            }

            // 현재 타입과 모든 부모 타입의 멤버를 수집
            // 먼저 타입 체인을 수집 (부모 -> 자식 순서)
            var typeChain = new System.Collections.Generic.List<INamedTypeSymbol>();
            INamedTypeSymbol? currentType = typeSymbol;
            while (currentType != null && currentType.SpecialType != SpecialType.System_Object)
            {
                typeChain.Add(currentType);
                currentType = currentType.BaseType;
            }
            
            // 타입 체인을 역순으로 정렬 (부모가 먼저, 자식이 나중)
            typeChain.Reverse();
            
            // 멤버를 수집 (부모부터 시작하여 자식까지, 같은 이름이면 나중 것이 우선)
            var allMembers = new System.Collections.Generic.List<ISymbol>();
            var collectedNames = new System.Collections.Generic.HashSet<string>();
            
            foreach (var type in typeChain)
            {
                var typeMembers = type.GetMembers()
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
                    });
                
                foreach (var member in typeMembers)
                {
                    // 같은 이름의 멤버가 이미 있으면 제거하고 새 것으로 교체 (자식이 부모를 override)
                    if (collectedNames.Contains(member.Name))
                    {
                        allMembers.RemoveAll(m => m.Name == member.Name);
                        collectedNames.Remove(member.Name);
                    }
                    
                    if (collectedNames.Add(member.Name))
                    {
                        allMembers.Add(member);
                    }
                }
            }
            
            Members = allMembers
                .Select(m => new MemberMetadata(m))
                .ToArray();
        }

        public uint GetMessageId(TypeMetadata typeMeta)
        {
            return ((uint)typeMeta.MessageStandaloneId << 24) | ((uint)typeMeta.MessageRootId << 16) | (uint)typeMeta.MessageElementId;
        }

    }
}
