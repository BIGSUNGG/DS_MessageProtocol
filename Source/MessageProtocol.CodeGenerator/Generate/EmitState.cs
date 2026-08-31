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
            string key = memberOrTypeName + "\0" + typeName;
            if (!_reportedKeys.Add(key))
            {
                return;
            }

            UnsupportedMembers.Add(new UnsupportedMemberInfo(location, typeName, memberOrTypeName));
        }
    }

    internal readonly struct UnsupportedMemberInfo
    {
        public UnsupportedMemberInfo(Location location, string typeName, string memberOrTypeName)
        {
            Location = location;
            TypeName = typeName;
            MemberOrTypeName = memberOrTypeName;
        }

        public Location Location { get; }
        public string TypeName { get; }
        public string MemberOrTypeName { get; }
    }
}
