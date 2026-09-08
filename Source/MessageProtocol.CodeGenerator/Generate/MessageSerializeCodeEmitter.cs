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
            IAssemblySymbol consumerAssembly,
            out string? code,
            out ImmutableArray<UnsupportedMemberInfo> unsupportedMembers)
        {
            var state = new EmitState(hasCollectionsMarshal);
            var serializationGraph = SerializationGraph.Create(typeMeta, attributeReferences);
            var sb = new StringBuilder();

            sb.Append(Header.Emit(typeMeta, out bool hasNamespace));
            sb.Append(Define.Emit(typeMeta, serializationGraph, attributeReferences, state, consumerAssembly));

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
        static string GetStaticHidingModifier(TypeMetadata typeMeta, bool isModuleInitializer = false, IAssemblySymbol? consumerAssembly = null)
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

            // Initialize() 는 internal 이다 — 어셈블리 밖의 베이스(메타데이터 참조)가 가진 Initialize 는
            // 기본적으로 이 컴파일에서 접근 불가능하므로 가릴 대상이 애초에 없다. 교차 어셈블리 파생(프로토콜 DLL +
            // 서버/클라이언트 DLL 분리 — 상용 표준 구성)에서 `new` 를 유지하면 CS0109 가 타입당 확정된다.
            // 단 베이스 어셈블리가 InternalsVisibleTo 로 소비자 어셈블리에 internal 접근을 열면 Initialize 는
            // 가릴 대상이 되돌아온다 — 이때 `new` 를 빼면 사용자가 수정할 수 없는 CS0108 이 생성 코드에 뜨므로
            // 접근이 열린 경우에만 방출 여부 판정으로 되돌아간다.
            if (isModuleInitializer && !BaseIsInThisCompilation(baseType) && !BaseInternalsAreAccessible(baseType, consumerAssembly))
            {
                return string.Empty;
            }

            return BaseEmitsStaticContract(baseType) ? "new " : string.Empty;
        }

        /// <summary>베이스 어셈블리가 소비자(이 컴파일) 어셈블리에 InternalsVisibleTo 로 internal 접근을 여는지.</summary>
        static bool BaseInternalsAreAccessible(TypeMetadata baseType, IAssemblySymbol? consumerAssembly)
        {
            return consumerAssembly != null && baseType.Symbol.ContainingAssembly.GivesAccessTo(consumerAssembly);
        }

        /// <summary>베이스가 이 컴파일의 소스에 정의돼 있는지(메타데이터 참조가 아니라).</summary>
        static bool BaseIsInThisCompilation(TypeMetadata baseType)
        {
            return baseType.Symbol.Locations.Any(static location => location.IsInSource);
        }

        /// <summary>베이스 메시지 타입에 생성 정적 멤버가 실제로 존재하는지 여부.</summary>
        static bool BaseEmitsStaticContract(TypeMetadata baseType)
        {
            var symbol = baseType.Symbol;

            // abstract 메시지 타입은 소스·메타데이터 무관하게 정적 계약을 절대 방출하지 않는다
            // (abstract 그룹 루트는 생성을 건너뛰고, 그 외 abstract 는 MSGPROT010 으로 거부) —
            // abstract 여부는 메타데이터만으로 확정되므로 교차 어셈블리 베이스에서도 안전하게 내린다.
            // 소스 베이스는 KI-28 이 이 지점에서 걸러왔다; 메타데이터 베이스는 이번에 같은 규칙으로 맞췄다.
            if (symbol.IsAbstract)
            {
                return false;
            }

            // 다른 어셈블리(메타데이터)의 구체 베이스는 구문 참조가 없어 partial 여부를 알 수 없다 —
            // 그쪽 컴파일에서 생성됐다고 보고 기존대로 `new` 를 유지한다(잘못 내리면 CS0108/CS0114 로 역전).
            if (!BaseIsInThisCompilation(baseType))
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
