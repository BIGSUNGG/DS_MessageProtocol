---
project: DS_MessageProtocol
type: guide
status: draft
tags: [guide]
updated: 2026-07-11
---

# Getting Started

## 사전 요구

- .NET Standard 2.1을 지원하는 런타임 또는 Unity (해당 API 호환 버전)
- 샘플 실행 시 .NET 9 SDK (또는 `Examples/MinimalConsole` TFM에 맞는 SDK)

## 빠른 시작

1. 저장소 클론 또는 NuGet 패키지 추가
2. [[Packages]]에서 필요한 패키지 선택
3. 최소 예제: `Examples/MinimalConsole` — `[StandaloneMessage]` 타입 하나와 Serialize/Deserialize round-trip

```bash
dotnet run --project Examples/MinimalConsole -c Release
# 성공 시 OK 출력
```

솔루션의 `Examples` 폴더에 포함되어 있다.

## 관련

- [[How-To]]
- [[Packages]]
- [[FAQ]]
- [[Home]]
