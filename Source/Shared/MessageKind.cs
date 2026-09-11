namespace MessageProtocol
{
#if MESSAGE_PROTOCOL_CODE_GENERATOR
    internal enum MessageKind
#else
    /// <summary>[Message] 생성자로 지정하는 메시지 종류.</summary>
    public enum MessageKind
#endif
    {
        /// <summary>
        /// 계층에서 자동 추론: 조상에 메시지가 있으면 Child, 없고 동일 컴파일에 [Message] 파생이 있으면 Parent,
        /// 나머지는 Standalone.
        /// </summary>
        Automatic = 0,

        /// <summary>독립 ID 메시지. 헤더 4바이트.</summary>
        Standalone = 1,

        /// <summary>부모 메시지. 상속 계층의 꼭대기.</summary>
        Parent = 2,

        /// <summary>자식 메시지. 상속 계층에 부모가 필수이며 수동 id 는 0 일 수 없다.</summary>
        Child = 3,

        /// <summary>ID 없는 메시지. 헤더 1바이트. object Deserialize 대상이 아니다. id·category 인자는 쓸 수 없다.</summary>
        NonId = 4,
    }
}
