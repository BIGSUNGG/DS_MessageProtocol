using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace DS.MessageProtocol
{
    internal class MessageTypeCollector
    {
        public static MessageTypeCollector Instance => _instance.Value;
        static Lazy<MessageTypeCollector> _instance = new Lazy<MessageTypeCollector>(() => new MessageTypeCollector());

        /// <summary>
        /// Key : MessageGroupElement 속성을 가지는 클래스 타입
        /// Value : 해당 MessageGroupElement 속성
        /// </summary>
        public IReadOnlyDictionary<Type, MessageGroupElement> MessageGroupElements => _messageGroupElements;
        Dictionary<Type, MessageGroupElement> _messageGroupElements = new Dictionary<Type, MessageGroupElement>();

        /// <summary>
        /// Key : MessageGroupRoot 속성을 가지는 클래스 타입
        /// Value : 해당 MessageGroupRoot 속성
        /// </summary>
        public IReadOnlyDictionary<Type, MessageGroupRoot> MessageGroupRoots => _messageGroupRoots;
        Dictionary<Type, MessageGroupRoot> _messageGroupRoots = new Dictionary<Type, MessageGroupRoot>();

        /// <summary>
        /// Key : MessageGroupElement 타입
        /// Value : 해당 Element의 최상위 MessageGroupRoot 타입
        /// </summary>
        public IReadOnlyDictionary<Type, Type> MessageGroupRootByElement => _messageGroupRootByElement;
        Dictionary<Type, Type> _messageGroupRootByElement = new Dictionary<Type, Type>();

        /// <summary>
        /// Key : MessageGroupRoot 타입
        /// Value : 해당 Root의 모든 MessageGroupElement 타입
        /// </summary> 
        public IReadOnlyDictionary<Type, List<Type>> MessageElementByRoot => _messageElementByRoot;
        Dictionary<Type, List<Type>> _messageElementByRoot = new Dictionary<Type, List<Type>>();

        public MessageTypeCollector()
        {
            var assembly = Assembly.GetExecutingAssembly();
            foreach(var type in assembly.GetTypes())
            {
                var elementAttribute = type.GetCustomAttribute<MessageGroupElement>(false);
                var rootAttribute = type.GetCustomAttribute<MessageGroupRoot>(false);
                
                if(elementAttribute != null)
                    _messageGroupElements[type] = elementAttribute;
                if(rootAttribute != null)
                    _messageGroupRoots[type] = rootAttribute;

                if(elementAttribute != null && rootAttribute != null)
                    throw new InvalidOperationException($"Type {type.FullName} cannot have both MessageGroupElement and MessageGroupRoot attributes");
            }

            // Root에서 Element를 찾는 방식으로 변경 (성능 개선)
            foreach(var root in _messageGroupRoots)
            {
                var rootType = root.Key;
                // 해당 Root 타입을 상속받거나 구현하는 모든 Element 찾기
                foreach (var element in _messageGroupElements)
                {
                    var elementType = element.Key;
                    
                    // Element가 Root의 자식 타입인지 확인
                    if(rootType.IsAssignableFrom(elementType))
                    {
                        // 이미 다른 Root에 매핑되어 있는지 확인
                        if(_messageGroupRootByElement.TryGetValue(elementType, out var existingRoot))
                            throw new InvalidOperationException(
                                $"Type {elementType.FullName} is a child of multiple MessageGroupRoot types: " +
                                $"{existingRoot.FullName} and {rootType.FullName}");
                        else
                            _messageGroupRootByElement[elementType] = rootType;
                        
                        if(_messageElementByRoot.TryGetValue(rootType, out var existingElements) == false)
                            _messageElementByRoot[rootType] = new List<Type>();
                            
                        existingElements.Add(elementType);
                    }
                }
            }

            // 모든 Element가 Root에 매핑되었는지 확인
            foreach(var element in _messageGroupElements)
            {
                if(_messageGroupRootByElement.ContainsKey(element.Key) == false)
                    throw new InvalidOperationException($"Type {element.Key.FullName} is not a child of any MessageGroupRoot");
            }
        }

        public Type? GetMessageGroupRootByElement(Type messageElementType)
        {
            if(_messageGroupRootByElement.TryGetValue(messageElementType, out var messageGroupRoot))
                return messageGroupRoot;

            return null;
        }
    }
}   