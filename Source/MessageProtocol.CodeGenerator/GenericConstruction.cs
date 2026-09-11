using MessageProtocol.CodeGenerator.Generate;
using MessageProtocol.CodeGenerator.Metadata;
using MessageProtocol.CodeGenerator.Reference;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;
using System.Text;

namespace MessageProtocol.CodeGenerator
{
    // ------- 제네릭 구성 등록 하위 기능 (MessageCodeGenerator 에서 분리 — 2026-09-08 구조 감사 FINDING 3) -------
    //
    // [GenericMessage(typeof(구성), ClassId)] 선언의 파싱·검증·충돌 수집·등록 캐리어 방출을 담당한다.
    // MSGPROT008/014/015 진단과 KI-27·KI-31·KI-32 규약이 여기 산다 — 메시지 방출(MessageCodeGenerator.Generate)과의
    // 결합은 Generate 가 이 클래스의 4개 진입점을 호출하는 것뿐이다.

    internal static class GenericConstruction
    {
    static bool TryGetRegisteredGenericWireMessageId(
        INamedTypeSymbol declaration,
        AttributeReferences attributeReferences,
        out uint messageId)
    {
        messageId = 0;

        if (!declaration.IsGenericType
            || !(declaration.ContainAttribute(attributeReferences.StandaloneMessageAttributeType)
                || declaration.ContainAttribute(attributeReferences.MessageAttributeType)))
        {
            return false;
        }

        if (!MessageCodeGenerator.IsPartial(declaration) || !MessageCodeGenerator.IsConstructibleMessageType(declaration))
        {
            return false;
        }

        if (!TypeMetadataValidator.TryValidateMessageIdRange(declaration, attributeReferences, out _, out _) ||
            !TypeMetadataValidator.TryValidateCategoryRange(declaration, attributeReferences, out _))
        {
            return false;
        }

        var typeMeta = new TypeMetadata(declaration, attributeReferences);
        if (MessageCodeGenerator.HasMultipleMessageAttributes(typeMeta) || !typeMeta.IsGenericWireMessage)
        {
            return false;
        }

        messageId = typeMeta.GetMessageId();
        return true;
    }

    static bool TryGetRegisteredWireMessageId(
        INamedTypeSymbol typeSymbol,
        AttributeReferences attributeReferences,
        out uint messageId)
    {
        messageId = 0;

        if (typeSymbol.IsGenericType || !MessageCodeGenerator.HasMessageAttribute(typeSymbol, attributeReferences))
        {
            return false;
        }

        if (!MessageCodeGenerator.IsPartial(typeSymbol) || !MessageCodeGenerator.IsConstructibleMessageType(typeSymbol))
        {
            return false;
        }

        // MSGPROT005(ID 값 범위)·MSGPROT013(카테고리 범위)으로 이미 거부될 타입은 생성·등록되지 않으므로
        // 충돌 판정에서 뺀다 — 그렇지 않으면 그 타입 때문에 **문제없는 상대 타입까지** MSGPROT014 를 맞고
        // 생성이 막힌다(연쇄 오탐). 실제로 등록될 형태만 세는 것이 이 게이트의 규약이다.
        if (!TypeMetadataValidator.TryValidateMessageIdRange(typeSymbol, attributeReferences, out _, out _) ||
            !TypeMetadataValidator.TryValidateCategoryRange(typeSymbol, attributeReferences, out _))
        {
            return false;
        }

        var typeMeta = new TypeMetadata(typeSymbol, attributeReferences);
        if (typeMeta.IsNonIdMessage || (!typeMeta.IsStandaloneMessage && !typeMeta.IsGroupMessage))
        {
            return false;
        }

        messageId = typeMeta.GetMessageId();
        return MessageWireFormat.HasEmbeddedMessageId((byte)(messageId >> 24));
    }

    internal    static List<(INamedTypeSymbol? Construction, uint ClassId)> ParseConstructionEntries(
        INamedTypeSymbol typeSymbol,
        AttributeReferences attributeReferences)
    {
        var entries = new List<(INamedTypeSymbol?, uint)>();
        if (attributeReferences.GenericMessageAttributeType == null)
        {
            return entries;
        }

        foreach (var attribute in typeSymbol.GetAttributes())
        {
            if (!SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeReferences.GenericMessageAttributeType))
            {
                continue;
            }

            INamedTypeSymbol? construction = attribute.ConstructorArguments.Length > 0
                && attribute.ConstructorArguments[0].Kind == TypedConstantKind.Type
                && attribute.ConstructorArguments[0].Value is INamedTypeSymbol named
                    ? named
                    : null;

            uint classId = 0;
            bool classIdIsError = false;
            foreach (var namedArgument in attribute.NamedArguments)
            {
                if (namedArgument.Key != "ClassId")
                {
                    continue;
                }

                // 오류형 인자(선언되지 않은 상수 등)는 컴파일러가 속성 사용 위치에서 이미 1차 오류(CS0103 등)를
                // 보고한다 — 여기서 classId=0 을 그대로 흘려보내면 "missing 'ClassId'" 라는 2차 오안내가
                // 원인을 가렸다(2026-09-08 메타데이터 감사 FINDING 2). 이 항목은 건너뛴다(등록 없음·진단 없음).
                if (namedArgument.Value.Kind == TypedConstantKind.Error)
                {
                    classIdIsError = true;
                    continue;
                }

                if (namedArgument.Value.Kind == TypedConstantKind.Primitive
                    && namedArgument.Value.Value is uint parsed)
                {
                    classId = parsed;
                }
            }

            if (classIdIsError)
            {
                continue;
            }

            entries.Add((construction, classId));
        }

        return entries;
    }

    /// <summary>
    /// 컴파일 전체에서 같은 구성 중복 선언·(선언, ClassId) 충돌을 찾아낸다.
    /// 방치하면 모듈 로드 시 등록 충돌로 크래시하므로 컴파일 진단으로 승격한다.
    /// </summary>
    internal    static ConstructionConflicts CollectConstructionConflicts(
        ImmutableArray<INamedTypeSymbol> types,
        AttributeReferences attributeReferences)
    {
        var constructionCounts = new Dictionary<INamedTypeSymbol, int>(SymbolEqualityComparer.Default);
        var idCounts = new Dictionary<(INamedTypeSymbol Declaration, uint ClassId), int>(DeclarationClassIdComparer.Instance);
        var conflicts = new ConstructionConflicts();

        foreach (var type in types)
        {
            foreach (var (construction, classId) in ParseConstructionEntries(type, attributeReferences))
            {
                if (construction == null)
                {
                    continue;
                }

                constructionCounts[construction] = constructionCounts.TryGetValue(construction, out int c) ? c + 1 : 1;
                var idKey = (construction.OriginalDefinition, classId);
                idCounts[idKey] = idCounts.TryGetValue(idKey, out int n) ? n + 1 : 1;

                // 조립된 런타임 키 (제네릭 와이어 MessageId, ClassId) 별 선언 소유자 — 서로 다른 두 선언이
                // 같은 키를 쓰면 `RegisterGenericReaderInvoker` 가 모듈 이니셜라이저에서 충돌해
                // TypeInitializationException(어셈블리 로드 실패)이 된다. (Declaration, ClassId) 키로는
                // 선언이 다르면 다른 키로 봐서 이 형태를 못 잡는다 (감사 원장 MEDIUM, 2026-09-06 패스).
                if (classId != 0 && classId <= TypeMetadata.MaxMessageAttributeValue
                    && TryGetRegisteredGenericWireMessageId(construction.OriginalDefinition, attributeReferences, out uint genericWireId))
                {
                    conflicts.AddRuntimeKeyOwner((genericWireId, classId), construction.OriginalDefinition);
                }
            }
        }

        // 비제네릭 메시지의 와이어 MessageId 소유자 — 동일 id 를 조립하는 두 타입은 모듈 로드 시
        // `_registeredMessageIds` 등록 충돌로 어셈블리 로드가 실패하므로 컴파일에서 잡는다 (Known-Issues KI-31).
        foreach (var type in types)
        {
            if (!TryGetRegisteredWireMessageId(type, attributeReferences, out uint wireMessageId))
            {
                continue;
            }

            conflicts.AddMessageIdOwner(wireMessageId, type);
        }

        foreach (var pair in constructionCounts)
        {
            if (pair.Value > 1)
            {
                conflicts.AddDuplicateConstruction(pair.Key);
            }
        }
        foreach (var pair in idCounts)
        {
            if (pair.Value > 1)
            {
                conflicts.AddCollidedId(pair.Key);
            }
        }
        return conflicts;
    }

    internal    static bool ValidateConstructionEntries(
        INamedTypeSymbol host,
        List<(INamedTypeSymbol? Construction, uint ClassId)> entries,
        AttributeReferences attributeReferences,
        ConstructionConflicts conflicts,
        SourceProductionContext context,
        Location location)
    {
        var seenClassIds = new HashSet<uint>();
        var seenConstructions = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        foreach (var (construction, classId) in entries)
        {
            if (construction == null)
            {
                ReportInvalidConstruction(context, location, host, "GenericMessage requires a closed generic construction type");
                return false;
            }

            if (classId == 0)
            {
                ReportInvalidConstruction(context, location, host, $"construction '{construction.ToDisplayString()}' is missing 'ClassId' (must be 1 .. {TypeMetadata.MaxMessageAttributeValue})");
                return false;
            }

            // ClassId 는 MessageId 와 같은 24비트 와이어 슬롯(`GenericIdHeaderSize` 의 뒤 3바이트)에 담긴다.
            // 상한 초과는 와이어에 잘리므로 런타임 등록이 거부하는데, 그 등록은 **모듈 이니셜라이저** 안에서 돌아
            // ArgumentOutOfRangeException 이 TypeInitializationException(어셈블리 로드 실패)으로 번진다.
            // 런타임 크래시 대신 컴파일 진단으로 승격한다 (Known-Issues KI-27).
            if (classId > TypeMetadata.MaxMessageAttributeValue)
            {
                ReportInvalidConstruction(context, location, host, $"ClassId {classId} is out of range for construction '{construction.ToDisplayString()}' (must be 1 .. {TypeMetadata.MaxMessageAttributeValue})");
                return false;
            }

            if (!seenClassIds.Add(classId))
            {
                ReportInvalidConstruction(context, location, host, $"ClassId {classId} is declared more than once");
                return false;
            }

            if (!seenConstructions.Add(construction))
            {
                ReportInvalidConstruction(context, location, host, $"construction '{construction.ToDisplayString()}' is declared more than once");
                return false;
            }

            if (construction.IsUnboundGenericType)
            {
                ReportInvalidConstruction(context, location, host, $"'{construction.ToDisplayString()}' is an unbound generic type; declare a closed construction like typeof({construction.Name}<...>)");
                return false;
            }

            var declaration = construction.OriginalDefinition;
            if (!construction.IsGenericType
                || !declaration.IsGenericType
                || !(declaration.ContainAttribute(attributeReferences.StandaloneMessageAttributeType)
                    || declaration.ContainAttribute(attributeReferences.MessageAttributeType)))
            {
                ReportInvalidConstruction(context, location, host, $"'{construction.ToDisplayString()}' is not a construction of a generic message declaration ('[StandaloneMessage]' or '[Message]' required)");
                return false;
            }

            if (conflicts.IsConflicting(construction, classId))
            {
                ReportInvalidConstruction(context, location, host, $"construction '{construction.ToDisplayString()}' (or its ClassId) is declared more than once in this compilation");
                return false;
            }

            // 서로 다른 제네릭 선언이 같은 (MessageId, ClassId) 런타임 키를 조립하는지 — 같은 선언의 중복은
            // 위 IsConflicting 이 잡는다. 방치하면 모듈 이니셜라이저의 RegisterGenericReaderInvoker 가
            // 충돌해 어셈블리 로드가 실패한다 (MSGPROT015).
            if (TryGetRegisteredGenericWireMessageId(declaration, attributeReferences, out uint genericWireId)
                && conflicts.TryGetGenericRuntimeKeyPeers(genericWireId, classId, declaration, out string genericPeers))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.DuplicateGenericRuntimeKey,
                    location,
                    host.Name,
                    construction.ToDisplayString(),
                    genericWireId.ToString("X8"),
                    classId.ToString(),
                    genericPeers));
                return false;
            }
        }

        return true;
    }

    static void ReportInvalidConstruction(
        SourceProductionContext context,
        Location location,
        INamedTypeSymbol host,
        string reason)
    {
        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.InvalidGenericMessageDeclaration,
            location,
            host.Name,
            reason));
    }

    internal    static void EmitConstructionRegistration(
        INamedTypeSymbol host,
        List<(INamedTypeSymbol? Construction, uint ClassId)> entries,
        SourceProductionContext context,
        HashSet<string> usedCarrierSuffixes)
    {
        string suffix = SymbolNaming.MakeUniqueSuffix(host, usedCarrierSuffixes);
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("using System.Runtime.CompilerServices;");
        sb.AppendLine("using MessageProtocol.Serialize;");
        sb.AppendLine();
        sb.AppendLine($"internal static class __GenericConstructionRegistration_{suffix}");
        sb.AppendLine("{");
        sb.AppendLine("    [ModuleInitializer]");
        sb.AppendLine("    internal static void Initialize()");
        sb.AppendLine("    {");
        foreach (var (construction, classId) in entries)
        {
            string constructionName = construction!.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            sb.AppendLine($"        MessageSerializer.RegisterGenericConstruction<{constructionName}>({classId});");
        }
        sb.AppendLine("    }");
        sb.AppendLine("}");

        context.AddSource(
            MessageCodeGenerator.SanitizeHintName($"__GenericConstructionRegistration_{suffix}"),
            SourceText.From(sb.ToString(), Encoding.UTF8));
    }
    }

    /// <summary>컴파일 전체 구성 선언 중복 상태.</summary>
    internal sealed class ConstructionConflicts
    {
        // 상태는 비공개 — 구축은 GenericConstruction.CollectConstructionConflicts 가 내부 뮤테이터로만
        // 하고 소비(Generate 의 진단 보고)는 읽기 접근자로만 한다. 공개 가변 사전이면 KI-31 "실제 등록될
        // 형태만 센다" 불변식을 우회해 외부에서 사후 삽입할 수 있다(2026-09-08 구조 감사 FINDING 4).
        readonly HashSet<INamedTypeSymbol> _duplicateConstructions = new(SymbolEqualityComparer.Default);
        readonly HashSet<(INamedTypeSymbol Declaration, uint ClassId)> _collidedIds = new(DeclarationClassIdComparer.Instance);

        /// <summary>와이어 MessageId → 그 id 를 조립하는 타입들(모듈 로드 시 실제 등록될 형태만).</summary>
        readonly Dictionary<uint, List<INamedTypeSymbol>> _messageIdOwners = new();

        /// <summary>조립된 제네릭 런타임 키 (MessageId, ClassId) → 그 키를 쓰는 제네릭 선언들(등록될 형태만).</summary>
        readonly Dictionary<(uint MessageId, uint ClassId), List<INamedTypeSymbol>> _genericRuntimeKeyOwners = new();

        internal void AddDuplicateConstruction(INamedTypeSymbol construction) => _duplicateConstructions.Add(construction);
        internal void AddCollidedId((INamedTypeSymbol Declaration, uint ClassId) key) => _collidedIds.Add(key);

        internal void AddMessageIdOwner(uint messageId, INamedTypeSymbol type)
        {
            if (!_messageIdOwners.TryGetValue(messageId, out var owners))
            {
                owners = new List<INamedTypeSymbol>();
                _messageIdOwners[messageId] = owners;
            }
            owners.Add(type);
        }

        /// <summary>같은 선언의 중복 추가는 무시한다(키당 소유자 목록은 집합처럼 동작).</summary>
        internal void AddRuntimeKeyOwner((uint MessageId, uint ClassId) key, INamedTypeSymbol declaration)
        {
            if (!_genericRuntimeKeyOwners.TryGetValue(key, out var owners))
            {
                owners = new List<INamedTypeSymbol>();
                _genericRuntimeKeyOwners[key] = owners;
            }
            if (!owners.Any(owner => SymbolEqualityComparer.Default.Equals(owner, declaration)))
            {
                owners.Add(declaration);
            }
        }

        public bool IsConflicting(INamedTypeSymbol construction, uint classId)
        {
            return _duplicateConstructions.Contains(construction)
                || _collidedIds.Contains((construction.OriginalDefinition, classId));
        }

        /// <summary>자신을 제외한 같은 와이어 MessageId 소유자가 있으면 그 이름들을 반환한다 (Known-Issues KI-31).</summary>
        public bool TryGetMessageIdPeers(uint messageId, INamedTypeSymbol self, out string peers)
        {
            peers = string.Empty;
            if (!_messageIdOwners.TryGetValue(messageId, out var owners) || owners.Count < 2)
            {
                return false;
            }

            peers = string.Join(
                ", ",
                owners
                    .Where(owner => !SymbolEqualityComparer.Default.Equals(owner, self))
                    .Select(owner => $"'{owner.ToDisplayString()}'"));
            return true;
        }

        /// <summary>자신의 선언을 제외한 같은 런타임 키 (MessageId, ClassId) 소유자가 있으면 그 이름들을 반환한다 (MSGPROT015).</summary>
        public bool TryGetGenericRuntimeKeyPeers(uint messageId, uint classId, INamedTypeSymbol selfDeclaration, out string peers)
        {
            peers = string.Empty;
            if (!_genericRuntimeKeyOwners.TryGetValue((messageId, classId), out var owners) || owners.Count < 2)
            {
                return false;
            }

            peers = string.Join(
                ", ",
                owners
                    .Where(owner => !SymbolEqualityComparer.Default.Equals(owner, selfDeclaration))
                    .Select(owner => $"'{owner.ToDisplayString()}'"));
            return peers.Length > 0;
        }
    }

    sealed class DeclarationClassIdComparer : IEqualityComparer<(INamedTypeSymbol Declaration, uint ClassId)>
    {
        public static readonly DeclarationClassIdComparer Instance = new();

        public bool Equals((INamedTypeSymbol Declaration, uint ClassId) x, (INamedTypeSymbol Declaration, uint ClassId) y)
        {
            return SymbolEqualityComparer.Default.Equals(x.Declaration, y.Declaration) && x.ClassId == y.ClassId;
        }

        public int GetHashCode((INamedTypeSymbol Declaration, uint ClassId) obj)
        {
            unchecked
            {
                return (SymbolEqualityComparer.Default.GetHashCode(obj.Declaration) * 397) ^ (int)obj.ClassId;
            }
        }
    }
}
