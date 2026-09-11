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
        Parent = 1 << 2,
        Child = 1 << 3,
        /// <summary>ID 를 가진 세 종류(Standalone/Parent/Child)의 조합 — NonIdMessage 의 반대 개념.</summary>
        IdMessage = Standalone | Parent | Child,
    }
}
