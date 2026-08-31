using System;

namespace MessageProtocol
{
    /// <summary>헤더 하위 니블(0~15) 메시지 카테고리.</summary>
    [Flags]
    public enum MessageCategory : byte
    {
        /// <summary>기본 카테고리.</summary>
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
