using System;
using System.Reflection;

namespace MessageProtocol.Serialize
{
    public static partial class MessageSerializer
    {
        /// <summary>
        /// 제네릭 hot path 가 호출하는 ref-기반 직렬화 델리게이트.
        /// </summary>
        public delegate void TypedSerializeRefAction<T>(T message, ref MessageBufferWriter writer);

        /// <summary>
        /// 제네릭 hot path 가 호출하는 ref-기반 역직렬화 델리게이트.
        /// </summary>
        public delegate T TypedDeserializeRefFunc<T>(ref MessageBufferReader reader);

        /// <summary>
        /// <see cref="SerializerCache{T}"/> 정적 생성자보다 먼저 델리게이트를 심기 위한 홀더.
        /// 다른 타입이므로 Prefill 시 <see cref="SerializerCache{T}"/> cctor 가 트리거되지 않습니다.
        /// </summary>
        static class SerializerCachePrefill<T>
        {
            public static TypedSerializeRefAction<T>? Serialize;
            public static TypedDeserializeRefFunc<T>? Deserialize;
            public static Func<T, byte[]>? SerializeBytes;
            public static Func<byte[], T>? DeserializeBytes;
            public static uint MessageId;
            public static bool HasId;
            public static bool IsSet;
        }

        /// <summary>
        /// 타입 인자 <typeparamref name="T"/> 전용 정적 캐시.
        /// <see cref="RegisterHasIdMessage{T}(TypedSerializeRefAction{T}, TypedDeserializeRefFunc{T}, uint, Func{T, byte[]}?, Func{byte[], T}?)"/> 등으로
        /// Prefill 되면 리플렉션 없이 채워지고, 그렇지 않으면 첫 접근 시 1회 리플렉션으로 채워집니다.
        /// </summary>
        internal static class SerializerCache<T>
        {
            public static readonly TypedSerializeRefAction<T> Serialize;
            public static readonly TypedDeserializeRefFunc<T>? Deserialize;
            public static readonly Func<T, byte[]> SerializeBytes;
            public static readonly uint MessageId;
            public static readonly bool HasId;
            public static readonly Func<byte[], T>? DeserializeBytes;

            static SerializerCache()
            {
                if (SerializerCachePrefill<T>.IsSet)
                {
                    Serialize = SerializerCachePrefill<T>.Serialize!;
                    Deserialize = SerializerCachePrefill<T>.Deserialize;
                    SerializeBytes = SerializerCachePrefill<T>.SerializeBytes!;
                    DeserializeBytes = SerializerCachePrefill<T>.DeserializeBytes;
                    MessageId = SerializerCachePrefill<T>.MessageId;
                    HasId = SerializerCachePrefill<T>.HasId;
                    return;
                }

                Type type = typeof(T);

                MethodInfo serializeRef = ResolveSerializeRefMethod(type);
                Serialize = (TypedSerializeRefAction<T>)serializeRef.CreateDelegate(typeof(TypedSerializeRefAction<T>));

                MethodInfo serializeBytes = ResolveSerializeBytesMethod(type);
                SerializeBytes = (Func<T, byte[]>)serializeBytes.CreateDelegate(typeof(Func<T, byte[]>));

                MethodInfo? deserializeRef = TryResolveDeserializeRefMethod(type);
                if (deserializeRef != null)
                {
                    Deserialize = (TypedDeserializeRefFunc<T>)deserializeRef.CreateDelegate(typeof(TypedDeserializeRefFunc<T>));
                }

                MethodInfo? deserializeBytes = TryResolveDeserializeBytesMethod(type);
                if (deserializeBytes != null)
                {
                    DeserializeBytes = (Func<byte[], T>)deserializeBytes.CreateDelegate(typeof(Func<byte[], T>));
                }

                if (TryResolveMessageIdGetter(type, out uint id))
                {
                    MessageId = id;
                    HasId = true;
                }
            }
        }

        static readonly Type ByRefBufferWriterType = typeof(MessageBufferWriter).MakeByRefType();
        static readonly Type ByRefBufferReaderType = typeof(MessageBufferReader).MakeByRefType();

        static MethodInfo ResolveSerializeRefMethod(Type type)
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
            throw new InvalidOperationException(
                $"Type '{type.FullName}' must define 'public static void Serialize({type.Name}, ref MessageBufferWriter)'. " +
                $"Ensure the type is generated via MessageProtocol.CodeGenerator or implements the message contract manually.");
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

        static MethodInfo ResolveSerializeBytesMethod(Type type)
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
            throw new InvalidOperationException(
                $"Type '{type.FullName}' must define 'public static byte[] Serialize({type.Name})'.");
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
