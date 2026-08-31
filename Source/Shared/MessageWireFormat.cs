using System;

namespace MessageProtocol
{
#if MESSAGE_PROTOCOL_CODE_GENERATOR
    internal static class MessageWireFormat
#else
    /// <summary>헤더·MessageId 조립 규칙. 런타임과 코드 생성기가 공유하는 단일 소스.</summary>
    public static class MessageWireFormat
#endif
    {
        public const int NonIdHeaderSize = 1;
        public const int IdHeaderSize = 4;

        public const byte NibbleMask = 0x0F;
        public const uint MessageIdValueMask = 0x00FF_FFFF;

        /// <summary>flags(상위 니블) + category(하위 니블) 로 헤더 첫 바이트를 만든다.</summary>
        public static byte ComposeHeaderByte(MessageFlag flags, byte category)
        {
            return (byte)((((byte)flags) & NibbleMask) << 4 | (category & NibbleMask));
        }

        /// <summary>헤더 바이트 + 24비트 ID 값으로 MessageId 를 조립한다.</summary>
        public static uint ComposeMessageId(MessageFlag flags, byte category, uint messageIdValue)
        {
            return ((uint)ComposeHeaderByte(flags, category) << 24) | (messageIdValue & MessageIdValueMask);
        }

        public static MessageFlag GetFlags(byte headerByte)
        {
            return (MessageFlag)((headerByte >> 4) & NibbleMask);
        }

        public static byte GetCategory(byte headerByte)
        {
            return (byte)(headerByte & NibbleMask);
        }

        /// <summary>헤더 뒤에 3바이트 ID 값이 따라오는 메시지인지 여부.</summary>
        public static bool HasEmbeddedMessageId(byte headerByte)
        {
            return (GetFlags(headerByte) & MessageFlag.NonIdMessage) == 0;
        }
    }
}
