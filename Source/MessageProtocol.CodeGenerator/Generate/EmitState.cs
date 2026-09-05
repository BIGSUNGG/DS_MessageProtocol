using Microsoft.CodeAnalysis;

namespace MessageProtocol.CodeGenerator.Generate
{
    /// <summary>이미트 진행 상태: CollectionsMarshal 가용성·미지원 멤버 수집·생성 로컬 이름 번호.</summary>
    internal sealed class EmitState
    {
        readonly HashSet<string> _reportedKeys = new();
        int _uniqueId;

        public EmitState(bool hasCollectionsMarshal)
        {
            HasCollectionsMarshal = hasCollectionsMarshal;
        }

        /// <summary>
        /// 생성 코드 안 로컬 이름(`__item3`·`__coll1` 등)에 붙일 유일 번호. **이미트 단위 상태**라
        /// 같은 입력은 항상 같은 번호를 얻는다. 프로세스 전역 정적 카운터였을 때는 생성 코드가 그 컴파일러
        /// 프로세스의 이전 컴파일 이력에 의존해 비결정적(동일 입력 → 다른 텍스트)이었고, 그것이 Roslyn 의
        /// 생성 출력 비교를 매번 깨뜨려 무관한 편집에도 생성 트리가 교체·재컴파일됐다 (Known-Issues KI-3).
        /// 이미트는 타입별로 단일 스레드에서 돌므로 잠금은 필요 없다(전역 카운터가 `Interlocked` 를 필요로 했던 이유 자체가 사라졌다).
        /// </summary>
        public int NextUniqueId() => ++_uniqueId;

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
