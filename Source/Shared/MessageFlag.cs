using System;

namespace MessageProtocol
{
#if MESSAGE_PROTOCOL_CODE_GENERATOR
    [Flags]
    internal enum MessageFlag : byte
#else
    /// <summary>헤더 상위 니블에 기록되는 메시지 종류 플래그.</summary>
    [Flags]
    public enum MessageFlag : byte
#endif
    {
        None = 0,
        /// <summary>제네릭 독립 메시지용 예약 헤더 플래그(값 0). 헤더 뒤에 3바이트 구성 타입 ID 가 따라온다.</summary>
        Generic = 0,
        NonIdMessage = 1 << 0,
        Standalone = 1 << 1,
        GroupRoot = 1 << 2,
        GroupElement = 1 << 3,
        StandaloneOrGroup = Standalone | GroupRoot | GroupElement,
    }
}
