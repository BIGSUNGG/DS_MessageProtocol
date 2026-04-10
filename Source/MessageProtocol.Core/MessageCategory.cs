using System;

namespace MessageProtocol
{
#if MESSAGE_PROTOCOL_CODE_GENERATOR
    [Flags]
    internal enum MessageCategory : byte
#else
    [Flags]
    public enum MessageCategory : byte
#endif
    {   
        /// <summary>
        /// Default Category0
        /// </summary>
        Category0 = 0x00,
        Category1 = 0x01,
        Category2 = 0x02,
        Category3 = 0x03,
        Category4 = 0x04,
        Category5 = 0x05,
        Category6 = 0x06,
        Category7 = 0x07,
        Category8 = 0x08,
        Category9 = 0x09,
        Category10 = 0x0A,
        Category11 = 0x0B,
        Category12 = 0x0C,
        Category13 = 0x0D,
        Category14 = 0x0E,
        Category15 = 0x0F,

        CategoryMask = 0x0F,
    }
}
