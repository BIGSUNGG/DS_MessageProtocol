using System;

namespace MessageProtocol.Serialize
{
    /// <summary>
    /// 메시지 직렬화 계약. 생성기가 자동으로 구현합니다.
    /// hot path 는 <see cref="MessageBufferWriter"/> / <see cref="MessageBufferReader"/> 를 사용하는 메서드입니다.
    /// byte[] 오버로드는 호환성 용도의 래퍼입니다.
    /// </summary>
    public interface IMessageSerializable<T>
    {
        /// <summary>hot path: 버퍼 writer 에 메시지 전체(헤더 + 페이로드)를 기록합니다.</summary>
        static abstract void Serialize(T message, ref MessageBufferWriter writer);

        /// <summary>hot path: 버퍼 reader 에서 메시지 전체를 역직렬화합니다.</summary>
        static abstract T Deserialize(ref MessageBufferReader reader);

        /// <summary>compatibility: byte[] 를 반환하는 기존 API.</summary>
        static abstract byte[] Serialize(T message);

        /// <summary>compatibility: byte[] 에서 역직렬화하는 기존 API.</summary>
        static abstract T Deserialize(byte[] data);
    }

    public interface IHasIdMessageSerializable<T> : IMessageSerializable<T>
    {
        static abstract uint MessageId { get; }
    }
}
