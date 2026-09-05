using System;
using System.Reflection;

namespace MessageProtocol.Serialize
{
    public static partial class MessageSerializer
    {
        /// <summary>제네릭 hot path 가 호출하는 ref 기반 직렬화 델리게이트.</summary>
        public delegate void TypedSerializeRefAction<T>(T message, ref MessageBufferWriter writer);

        /// <summary>제네릭 hot path 가 호출하는 ref 기반 역직렬화 델리게이트.</summary>
        public delegate T TypedDeserializeRefFunc<T>(ref MessageBufferReader reader);

        /// <summary>
        /// <see cref="SerializerCache{T}"/> cctor 보다 먼저 델리게이트를 심기 위한 홀더.
        /// 별도 타입이라 Prefill 중에는 캐시 cctor 가 돌지 않는다.
        /// </summary>
        static class SerializerCachePrefill<T>
        {
            public static TypedSerializeRefAction<T>? Serialize;
            public static TypedDeserializeRefFunc<T>? Deserialize;
            public static Func<T, byte[]>? SerializeBytes;
            public static Func<byte[], T>? DeserializeBytes;
            public static uint MessageId;
            public static bool HasId;

            // volatile = release store. cctor 는 이 플래그만 보고 나머지 필드를 읽으므로 publication 을 여기에 묶어,
            // 동시 cctor 가 IsSet=true 만 보고 델리게이트는 아직 null 인 **찢어진 상태**를 캐시에 고정하는 것을 막는다
            // (x86 에서는 관찰이 어렵지만 Unity ARM 은 store-store 재배열이 가능 — Known-Issues KI-11).
            public static volatile bool IsSet;
        }

        /// <summary>
        /// 타입 인자 전용 정적 캐시. 등록 시 Prefill 되면 리플렉션 없이 채워지고,
        /// 그렇지 않으면 첫 접근 시 1회 리플렉션으로 채워진다.
        /// <para>
        /// 필드는 <c>readonly</c> 가 아니고 cctor 는 **절대 던지지 않는다** — 둘 다 같은 이유다. cctor 가 던지면 CLR 이
        /// 그 실패를 타입별로 영구 캐싱해 이후 성공적인 델리게이트 등록으로도 복구할 수 없고(`TypeInitializationException`),
        /// readonly 면 등록 전 조기 접근으로 cctor 가 먼저 돌았을 때 Prefill 이 영원히 무시된다 (Known-Issues KI-11).
        /// 미해결 멤버는 null 로 남고 사용 지점에서 명확한 메시지로 보고한다.
        /// </para>
        /// </summary>
        internal static class SerializerCache<T>
        {
            public static TypedSerializeRefAction<T>? Serialize;
            public static TypedDeserializeRefFunc<T>? Deserialize;
            public static Func<T, byte[]>? SerializeBytes;
            public static Func<byte[], T>? DeserializeBytes;
            public static uint MessageId;
            public static bool HasId;

            static SerializerCache()
            {
                if (SerializerCachePrefill<T>.IsSet)
                {
                    Serialize = SerializerCachePrefill<T>.Serialize;
                    Deserialize = SerializerCachePrefill<T>.Deserialize;
                    SerializeBytes = SerializerCachePrefill<T>.SerializeBytes;
                    DeserializeBytes = SerializerCachePrefill<T>.DeserializeBytes;
                    MessageId = SerializerCachePrefill<T>.MessageId;
                    HasId = SerializerCachePrefill<T>.HasId;
                    return;
                }

                Type type = typeof(T);

                // 찾지 못한 멤버는 null 로 남긴다 — 여기서 던지면 CLR 이 타입별 초기화 실패를 영구 캐싱해
                // 이후 델리게이트 등록으로도 복구 불가능한 TypeInitializationException 이 된다 (KI-11).
                Serialize = TryCreateDelegate<TypedSerializeRefAction<T>>(TryResolveSerializeRefMethod(type));
                SerializeBytes = TryCreateDelegate<Func<T, byte[]>>(TryResolveSerializeBytesMethod(type));
                Deserialize = TryCreateDelegate<TypedDeserializeRefFunc<T>>(TryResolveDeserializeRefMethod(type));
                DeserializeBytes = TryCreateDelegate<Func<byte[], T>>(TryResolveDeserializeBytesMethod(type));

                if (TryResolveMessageIdGetter(type, out uint id))
                {
                    MessageId = id;
                    HasId = true;
                }
            }
        }

        /// <summary>리플렉션으로 찾은 static 멤버를 델리게이트로 만든다. 멤버가 없으면 null (cctor 비던짐 규약).</summary>
        static TDelegate? TryCreateDelegate<TDelegate>(MethodInfo? method) where TDelegate : class
        {
            return method is null ? null : (TDelegate)(object)method.CreateDelegate(typeof(TDelegate));
        }

        static readonly Type ByRefBufferWriterType = typeof(MessageBufferWriter).MakeByRefType();
        static readonly Type ByRefBufferReaderType = typeof(MessageBufferReader).MakeByRefType();

        /// <summary>`static void Serialize(T, ref MessageBufferWriter)` 를 찾는다. 없으면 null — cctor 가 던지면 CLR 이 실패를 영구 캐싱한다 (KI-11).</summary>
        static MethodInfo? TryResolveSerializeRefMethod(Type type)
        {
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                if (method.Name != "Serialize") continue;
                if (method.ReturnType != typeof(void)) continue;
                if (method.IsGenericMethodDefinition) continue;

                var parameters = method.GetParameters();
                if (parameters.Length != 2) continue;
                if (parameters[0].ParameterType != type) continue;
                if (parameters[1].ParameterType != ByRefBufferWriterType) continue;
                return method;
            }
            return null;
        }

        static MethodInfo? TryResolveDeserializeRefMethod(Type type)
        {
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                if (method.Name != "Deserialize") continue;
                if (method.ReturnType != type) continue;
                if (method.IsGenericMethodDefinition) continue;

                var parameters = method.GetParameters();
                if (parameters.Length != 1) continue;
                if (parameters[0].ParameterType != ByRefBufferReaderType) continue;
                return method;
            }
            return null;
        }

        /// <summary>`static byte[] Serialize(T)` 를 찾는다. 없으면 null (사유는 <see cref="TryResolveSerializeRefMethod"/> 와 동일).</summary>
        static MethodInfo? TryResolveSerializeBytesMethod(Type type)
        {
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                if (method.Name != "Serialize") continue;
                if (method.ReturnType != typeof(byte[])) continue;
                if (method.IsGenericMethodDefinition) continue;

                var parameters = method.GetParameters();
                if (parameters.Length != 1) continue;
                if (parameters[0].ParameterType != type) continue;
                return method;
            }
            return null;
        }

        static MethodInfo? TryResolveDeserializeBytesMethod(Type type)
        {
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                if (method.Name != "Deserialize") continue;
                if (method.ReturnType != type) continue;
                if (method.IsGenericMethodDefinition) continue;

                var parameters = method.GetParameters();
                if (parameters.Length != 1) continue;
                if (parameters[0].ParameterType != typeof(byte[])) continue;
                return method;
            }
            return null;
        }

        static bool TryResolveMessageIdGetter(Type type, out uint messageId)
        {
            var property = type.GetProperty(
                "MessageId",
                BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            if (property != null && property.PropertyType == typeof(uint) && property.CanRead)
            {
                messageId = (uint)property.GetValue(null)!;
                return true;
            }

            messageId = 0;
            return false;
        }
    }
}
