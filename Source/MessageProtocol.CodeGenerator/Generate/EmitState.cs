using Microsoft.CodeAnalysis;

namespace MessageProtocol.CodeGenerator.Generate
{
    /// <summary>이미트 진행 상태: CollectionsMarshal 가용성과 미지원 멤버 수집.</summary>
    internal sealed class EmitState
    {
        readonly HashSet<string> _reportedKeys = new();

        public EmitState(bool hasCollectionsMarshal)
        {
            HasCollectionsMarshal = hasCollectionsMarshal;
        }

        public bool HasCollectionsMarshal { get; }

        public List<UnsupportedMemberInfo> UnsupportedMembers { get; } = new();

        public void ReportUnsupported(Location location, string typeName, string memberOrTypeName)
        {
            Report(location, typeName, memberOrTypeName, UnsupportedMemberKind.UnsupportedType);
        }

        public void ReportNotAssignable(Location location, string typeName, string memberOrTypeName)
        {
            Report(location, typeName, memberOrTypeName, UnsupportedMemberKind.NotAssignable);
        }

        void Report(Location location, string typeName, string memberOrTypeName, UnsupportedMemberKind kind)
        {
            string key = memberOrTypeName + "\0" + typeName;
            if (!_reportedKeys.Add(key))
            {
                return;
            }

            UnsupportedMembers.Add(new UnsupportedMemberInfo(location, typeName, memberOrTypeName, kind));
        }
    }

    /// <summary>미지원 멤버 사유: 타입 자체 미지원 또는 대입 불가(설정자 없음).</summary>
    internal enum UnsupportedMemberKind
    {
        UnsupportedType,
        NotAssignable,
    }

    internal readonly struct UnsupportedMemberInfo
    {
        public UnsupportedMemberInfo(Location location, string typeName, string memberOrTypeName, UnsupportedMemberKind kind)
        {
            Location = location;
            TypeName = typeName;
            MemberOrTypeName = memberOrTypeName;
            Kind = kind;
        }

        public Location Location { get; }
        public string TypeName { get; }
        public string MemberOrTypeName { get; }
        public UnsupportedMemberKind Kind { get; }
    }
}
