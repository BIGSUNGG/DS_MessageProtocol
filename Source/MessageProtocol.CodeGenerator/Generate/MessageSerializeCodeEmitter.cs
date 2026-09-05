using MessageProtocol.CodeGenerator.Graph;
using MessageProtocol.CodeGenerator.Metadata;
using MessageProtocol.CodeGenerator.Reference;
using Microsoft.CodeAnalysis;
using System.Collections.Immutable;
using System.Text;

namespace MessageProtocol.CodeGenerator.Generate
{
    /// <summary>메시지 타입 하나에 대한 생성 코드 이미터.</summary>
    internal static partial class MessageSerializeCodeEmitter
    {
        public static bool TryEmit(
            TypeMetadata typeMeta,
            AttributeReferences attributeReferences,
            bool hasCollectionsMarshal,
            out string? code,
            out ImmutableArray<UnsupportedMemberInfo> unsupportedMembers)
        {
            var state = new EmitState(hasCollectionsMarshal);
            var serializationGraph = SerializationGraph.Create(typeMeta, attributeReferences);
            var sb = new StringBuilder();

            sb.Append(Header.Emit(typeMeta, out bool hasNamespace));
            sb.Append(Define.Emit(typeMeta, serializationGraph, attributeReferences, state));

            if (hasNamespace)
            {
                sb.Append(Header.EmitCloseNamespace());
            }

            if (state.UnsupportedMembers.Count > 0)
            {
                code = null;
                unsupportedMembers = state.UnsupportedMembers.ToImmutableArray();
                return false;
            }

            code = sb.ToString();
            unsupportedMembers = ImmutableArray<UnsupportedMemberInfo>.Empty;
            return true;
        }

        /// <summary>
        /// 파생 메시지 타입의 생성 정적 멤버에 붙일 `new` 수식어 — **베이스가 실제로 정적 계약을 방출할 때만** 붙인다.
        /// 베이스가 방출하지 않는데 `new` 를 붙이면 가릴 멤버가 없어 소비자 빌드에 CS0109 가 뜬다
        /// (경고 누적, `TreatWarningsAsErrors` 환경에서는 빌드 실패). 방출이 없는 베이스: abstract 그룹 루트
        /// (상속 전용이라 생성을 건너뜀), abstract·기본 생성 불가 타입(MSGPROT010), partial 이 아닌 타입(MSGPROT001).
        /// 이미터 선언부(`Define`)와 메서드 방출(`Method`)이 이 한 구현을 공유한다 (Known-Issues KI-28).
        /// </summary>
        static string GetStaticHidingModifier(TypeMetadata typeMeta)
        {
            var baseType = typeMeta.BaseTypeMetadata;
            if (baseType == null)
            {
                return string.Empty;
            }

            if (!baseType.IsNonIdMessage && !baseType.IsStandaloneMessage && !baseType.IsGroupMessage)
            {
                return string.Empty;
            }

            return BaseEmitsStaticContract(baseType) ? "new " : string.Empty;
        }

        /// <summary>베이스 메시지 타입에 생성 정적 멤버가 실제로 존재하는지 여부.</summary>
        static bool BaseEmitsStaticContract(TypeMetadata baseType)
        {
            var symbol = baseType.Symbol;

            // 다른 어셈블리(메타데이터) 베이스는 구문 참조가 없어 partial 여부를 알 수 없다 —
            // 그쪽 컴파일에서 생성됐다고 보고 기존대로 `new` 를 유지한다(잘못 내리면 CS0108/CS0114 로 역전).
            if (!symbol.Locations.Any(static location => location.IsInSource))
            {
                return true;
            }

            return MessageCodeGenerator.IsPartial(symbol) && MessageCodeGenerator.IsConstructibleMessageType(symbol);
        }

        static string GetTypeDisplayName(ITypeSymbol typeSymbol)
        {
            return typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        }
    }
}
