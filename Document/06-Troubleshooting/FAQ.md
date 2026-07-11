---
project: DS_MessageProtocol
type: troubleshoot
status: draft
tags: [faq]
updated: 2026-07-11
---

# FAQ

## Q: 핫 패스에서 어떤 Serialize API를 써야 하나?

A: 타입이 고정이면 `SerializePooled<T>` 또는 `ref MessageBufferWriter` 경로. `Serialize<T>`의 `byte[]` 반환은 복사 할당이 난다. 다형성(베이스→파생)만 `Serialize(object)`. 상세: [[Known-Issues]] §2, [[Public-API]].

## Q: Unity / netstandard2.1에서 생성 코드가 깨진다

A: **P4·P5는 해결됨.** List는 CollectionsMarshal 가용 시 고속·아니면 폴백. Core는 `netstandard2.1`에 `ModuleInitializerAttribute` polyfill을 포함하고 `net6.0`도 멀티타깃한다. 잔여 호환 이슈가 있으면 [[Known-Issues]] §3(P3/P6/P7)과 아래 RegisterType 항목을 본다.

## Q: ModuleInitializer가 실행되지 않으면?

A: 일부 Unity/구형 툴체인은 `[ModuleInitializer]`를 호출하지 않을 수 있다. 그 경우 앱 시작 시 공개 API로 등록한다.

```csharp
MessageSerializer.RegisterType(typeof(HelloMessage));
```

생성 `Initialize()`는 `internal`이며 ModuleInitializer가 호출한다. 핫 경로 등록은 `RegisterHasIdMessage<T>(serialize, deserialize, messageId)` 델리게이트 오버로드다. 수동 구현 타입만 리플렉션 fallback을 탄다. 상세: [[Public-API]].

## Q: 메시지 필드가 round-trip에서 빠진다

A: 미지원 타입은 이제 `MSGPROT006` Error로 빌드가 실패한다([[Known-Issues]] S2 해결). 지원 목록은 [[How-To]] / [[Public-API]]를 따른다.

## 관련

- [[Known-Issues]] — 구조·성능·병목·해결 방안
- [[How-To]]
- [[Getting-Started]]
