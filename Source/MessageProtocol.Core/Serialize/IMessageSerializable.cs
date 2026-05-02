namespace MessageProtocol.Serialize
{
    /// <summary>
    /// 메시지 직렬화 마커. 생성기 또는 수동 구현이 다음 public static 멤버를 노출하면 됩니다.
    /// <list type="bullet">
    ///   <item>void Serialize(T, ref MessageBufferWriter)</item>
    ///   <item>byte[] Serialize(T)</item>
    ///   <item>T Deserialize(ref MessageBufferReader)</item>
    ///   <item>T Deserialize(byte[])</item>
    /// </list>
    /// 정적 추상 멤버를 사용하지 않으므로 .NET Standard 2.0 까지 호환됩니다.
    /// 멤버는 등록 시점에 한 번 리플렉션으로 해석되어 <c>SerializerCache&lt;T&gt;</c> 에 캐싱됩니다.
    /// </summary>
    public interface IMessageSerializable<T>
    {
    }

    /// <summary>
    /// 프로토콜 식별자(<c>MessageId</c>)를 갖는 메시지 마커.
    /// 구현 타입은 추가로 <c>public static uint MessageId { get; }</c> 를 노출해야 합니다.
    /// </summary>
    public interface IHasIdMessageSerializable<T> : IMessageSerializable<T>
    {
    }
}
