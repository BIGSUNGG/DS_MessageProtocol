---
project: DS_MessageProtocol
type: adr
status: superseded
tags: [adr, generator, generics]
updated: 2026-08-31
---

# ADR 0003: 제네릭 메시지 직렬화 지원 (대체됨)

> **Superseded by [ADR-0004](./ADR-0004-Generic-Message-Wire-Format.md) (2026-08-31)** — 구성 간 ID 공유·수신 측 수동 등록 문제를 전용 속성 + 구성 클래스 ID 와이어로 재설계.
> 아래 `T` 멤버 디스패치 메커니즘은 ADR-0004에서도 그대로 승계된다.

## Status

Superseded (2026-08-31) — 원래: Accepted (2026-08-31), [ADR-0002](./ADR-0002-Generic-Message-Serialization.md)(연기 결정)를 대체했었음.

## Status

Accepted (2026-08-31) — [ADR-0002](./ADR-0002-Generic-Message-Serialization.md)(연기 결정)를 대체한다.

## Context

[ADR-0002](./ADR-0002-Generic-Message-Serialization.md)는 제네릭 메시지 직렬화를 추후 지원으로 연기했으나, 바로 지원하기로 결정했다. 해결 대상은 [Known-Issues](../06-Troubleshooting/Known-Issues.md) KI-1: 메시지 속성이 붙은 제네릭 타입이 진단 없이 컴파일 불가 코드를 생성하던 문제.

핵심 설계 제약:

- 정적 멤버 계약(`static void Serialize(T, ref ...)` 등)은 netstandard2.1에서 타입 매개변수로 직접 호출할 수 없다 (정적 추상 멤버 불가).
- `[ModuleInitializer]`는 제네릭 타입(또는 제네릭 컨테이닝 타입 안)에서 사용할 수 없다.
- 닫힌 구성(`Msg<int>`)만 런타임에 등록·디스패치할 수 있다.

## Decision

1. **제네릭 메시지 타입을 생성기가 지원한다.** partial 선언·시그니처·계약 인터페이스에 타입 매개변수를 유지하고, 헬퍼 이름의 백틱(`MetadataName`의 `Msg`1`)은`_`로 변환한다.
2. **타입 매개변수 `T`를 타입으로 갖는 멤버는 런타임 메시지 디스패치로 직렬화한다.** `ReferenceKind` 태그(Null=0/NewObject=1) + `SerializeToWriter`/`DeserializeFromReader` 위임 — 그래프 밖 메시지 위임과 같은 형태. 컬렉션 요소가 `T`인 경우도 같다.
   - **제약**: `T` 에는 object dispatch 가능한(헤더에 ID가 있는) 등록된 메시지 타입만 올 수 있다. NonId 메시지는 규격상 디스패치 대상이 아니라 `T` 구성으로 라우팅 불가. 비메시지(원시) 타입 인스턴스화는 미지원.
3. **제네릭 타입은 자동 등록을 생성하지 않는다.** `[ModuleInitializer]` 미생성. 제네릭 경로(`Serialize<Msg<int>>`)는 등록 없이 동작하고, object 경로는 닫힌 구성 단위 지연 등록(첫 `Serialize(object)`) 또는 `RegisterType` 수동 등록을 쓴다.
4. **닫힌 구성들은 선언의 MessageId 를 공유한다.** object dispatch 등록은 ID 당 하나의 구성만 가능(기존 "이미 등록됨" 가드). 구성별 별도 ID가 필요하면 기존 패스트 경로(`RegisterHasIdMessage<T>(..., messageId)`)로 재정의한다.

## Consequences

### Positive

- KI-1 해결: 제네릭 메시지 타입이 진단·컴파일 오류 없이 생성·왕복한다.
- 런타임 변경 없음 — 기존 디스패치·등록 인프라(지연 등록 포함) 재사용.
- 와이어 포맷 변경 없음.

### Negative / 제약

- `T` 멤버는 백레퍼런스 추적 밖이라 `T` 경계를 넘는 공유·순환 참조는 복원되지 않는다 (그래프 밖 위임과 동일).
- 구성별 MessageId 공유로, 동시에 여러 닫힌 구성을 object dispatch 에 등록할 수 없다.
- **네트워크 수신 측 등록 필요**: 지연 등록은 `Serialize(object)` 경로에서만 일어난다. 역직렬화만 하는 수신 프로세스는 자신이 받을 닫힌 구성을 시작 시점에 명시적으로 등록해야 한다 (`RegisterType(typeof(Envelope<Ping>))`). ID 는 구성 간 공유라 바이트만으로는 구성을 유추할 수 없어 자동 감지는 불가능하다.
- `T` 멤버 직렬화는 박싱·딕셔너리 조회를 거친다 (핫 경로가 아닌 것으로 허용).

## Alternatives considered

- `T` 멤버 컴파일 타임 정적 디스패치 — 정적 추상 멤버가 필요해 타깃(netstandard2.1)에서 불가.
- 구성별 자동 ID 파생 — 프로토콜 매직이 생겨 기각 (명시적 재정의 경로가 이미 존재).

## 관련

- [Known-Issues](../06-Troubleshooting/Known-Issues.md) (KI-1 해결)
- [ADR-0002](./ADR-0002-Generic-Message-Serialization.md) (대체됨)
- [Feature-Spec](../02-Architecture/Feature-Spec.md)
