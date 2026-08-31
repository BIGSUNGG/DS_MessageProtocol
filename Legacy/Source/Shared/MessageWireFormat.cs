using System;

namespace MessageProtocol
{
#if MESSAGE_PROTOCOL_CODE_GENERATOR
    internal static class MessageWireFormat
#else
    public static class MessageWireFormat
#endif
    {
        public const int NonIdHeaderSize = 1;
        public const int IdHeaderSize = 4;
        public const int NullSizedPayloadLength = -1;
        public const int DefaultStreamCapacity = 256;

        public const byte NibbleMask = 0x0F;
        public const uint MessageIdValueMask = 0x00FF_FFFF;

        public static byte ComposeHeaderByte(MessageFlag flags, byte category)
        {
            return (byte)((((byte)flags) & NibbleMask) << 4 | (category & NibbleMask));
        }

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

        public static bool HasEmbeddedMessageId(byte headerByte)
        {
            return (GetFlags(headerByte) & MessageFlag.NonIdMessage) == 0;
        }
    }
}
