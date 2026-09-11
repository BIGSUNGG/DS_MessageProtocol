using System.Text;

namespace MessageProtocol
{
#if MESSAGE_PROTOCOL_CODE_GENERATOR
    internal static class MessageIdHash
#else
    /// <summary>
    /// <c>[Message]</c> 자동 MessageId 해시 — 타입 FullName 의 FNV-1a 32비트를 와이어 24비트(<c>0x00FF_FFFF</c>)로 마스크한다.
    /// 알고리즘과 FullName 문자열 형식은 와이어 호환을 위해 동결되어 있다(변경 시 기존 메시지 ID 전부 무효).
    /// </summary>
    public static class MessageIdHash
#endif
    {
        /// <summary>FNV-1a 32비트 오프셋 basis.</summary>
        public const uint OffsetBasis = 2166136261;
        /// <summary>FNV-1a 32비트 소수.</summary>
        public const uint Prime = 16777619;

        /// <summary>
        /// FullName(네임스페이스 점 구분 + 중첩 <c>+</c> + 제네릭 차수 <c>`n</c>, BCL <see cref="System.Type.FullName"/> 관례)의
        /// FNV-1a 32비트 해시를 24비트로 마스크해 반환한다.
        /// </summary>
        public static uint FromFullName(string fullName)
        {
            var hash = OffsetBasis;
            foreach (var b in Encoding.UTF8.GetBytes(fullName))
            {
                hash ^= b;
                hash *= Prime;
            }
            return hash & MessageWireFormat.MessageIdValueMask;
        }
    }
}
