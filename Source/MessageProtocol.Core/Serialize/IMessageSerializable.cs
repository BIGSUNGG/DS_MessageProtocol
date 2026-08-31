using System;

namespace MessageProtocol.Serialize
{
    /// <summary>
    /// 메시지 직렬화 계약 마커. 구현 타입은 다음 public static 멤버를 노출한다
    /// (생성기가 채워 주거나 수동 구현이 직접 작성):
    /// <list type="bullet">
    ///   <item><c>static void Serialize(T, ref MessageBufferWriter)</c></item>
    ///   <item><c>static byte[] Serialize(T)</c></item>
    ///   <item><c>static T Deserialize(ref MessageBufferReader)</c></item>
    ///   <item><c>static T Deserialize(byte[])</c></item>
    /// </list>
    /// 정적 추상 멤버를 쓰지 않아 netstandard2.1 에서도 동작한다.
    /// </summary>
    public interface IMessageSerializable<T>
    {
    }

    /// <summary>
    /// 프로토콜 식별자(MessageId)를 갖는 메시지 계약.
    /// 구현 타입은 추가로 <c>public static uint MessageId { get; }</c> 를 노출해야 한다.
    /// </summary>
    public interface IHasIdMessageSerializable<T> : IMessageSerializable<T>
    {
    }
}
