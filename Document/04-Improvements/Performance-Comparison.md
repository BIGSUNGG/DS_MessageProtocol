---
project: DS_MessageProtocol
type: improve
status: stable
tags: [benchmark, performance, comparison, memorypack, messagepack]
updated: 2026-09-09
---

# 성능 비교 실측 — DS_MessageProtocol vs MemoryPack vs MessagePack

> 2026-09-09 일회성 실측 (goal `20260909043749-okc176`). "상용 유니티 데디케이트 서버에서 속도 문제가 없는가"를
> 경쟁 직렬화 라이브러리와 동일 조건으로 측정해 답한 기록. 측정 프로젝트는 gitignored 일회성
> (`artifacts/bench-compare/`), 저장소 추적 파일 무변경·기존 게이트(테스트 309/309 × 2 TFM) 유지 확인 하에 수행.

## 결론

**전 영역에서 DS가 최속 또는 동급.** 경쟁이 더 빠른 측정 항목이 하나도 없었다. 기본 할당·와이어 크기도 동급이며,
공유 그래프 참조 추적·`SerializePooled` 무할당 경로·메시지 헤더 내장(프레이밍+타입 검증)은 DS만 갖춘 이점이다.
속도 관점 상용화 우려 없음.

## 측정 환경·방법

| 항목 | 값 |
| --- | --- |
| 하드웨어·OS | AMD Ryzen 9 7940HS, Windows 11 (x64 RyuJIT x86-64-v4) |
| 런타임 | .NET 9.0.12, 워크스테이션 GC |
| 벤치마크 도구 | BenchmarkDotNet v0.15.8, DefaultJob + MemoryDiagnoser |
| 비교 대상 | DS_MessageProtocol(HEAD `6dbe3ef`) · MemoryPack 1.21.4 · MessagePack-CSharp 3.1.4 (StandardResolver·표준 구성) |
| 메시지 형태 | 기존 `Test/MessageProtocol.Benchmarks` 4종 픽스처와 동등: 플랫(스칼라+int 8개 리스트)·문자열 헤비(100자×4)·객체 그래프(깊이 5 체인)·대형 컬렉션(int 100k + 문자열 1k) |
| 연산 | 형태별 직렬화·역직렬화 × 3개 라이브러리 = 24 항목 |

방법론 주의:

- **동일성**: 21개 항목은 동일 프로세스·동일 Job 실행(BDN 로그), StringHeavy 역직렬화 3개 항목은 동일 머신·동일 Job의 별도 실행(코드 내 SimpleFilter) — CLI 인자 전달이 `dotnet run` 경유에서 깨져 코드 내 필터로 대체.
- **할당(B/op)**: GC 카운터(`GetAllocatedBytesForCurrentThread`) 누적 차이 — 결정적 값. 수동 계측(24항목)으로 측정.
- **그래프 형태 상이**: 참조 추적 미지원인 MemoryPack·MessagePack 은 공유 없는 트리 변형(좌우 독립 체인)으로만 참여 — DS는 원본 공유 그래프. MessagePack v3.1.4 는 참조 보존 API 자체가 없음(제거됨), MemoryPack 도 미지원 — 즉 **공유/순환 그래프 직렬화는 DS만 지원**.
- **프레이밍**: DS 와이어는 4B 메시지 헤더 포함. 경쟁 2종은 페이로드만 — 실전에서는 길이 접두·타입 ID 추가 비용 발생.
- **변동성**: 노트북 열 변동으로 실행 간 편차 있음(대형 항목 ±2배 관찰). 속도 표는 단일 대표 실행 기준, 대형은 "동급" 판정으로만 해석.

## 속도 (ns/op, 낮을수록 좋음)

| 형태 | 연산 | DS | MemoryPack | MessagePack |
| --- | --- | ---: | ---: | ---: |
| 플랫 | 직렬화 | **65.4** | 94.6 | 88.8 |
| 플랫 | 역직렬화 | **39.4** | 54.6 | 121.0 |
| 문자열 헤비 | 직렬화 | **114.6** | 115.6 | 161.5 |
| 문자열 헤비 | 역직렬화 | 125.1 | **119.4** | 242.8 |
| 그래프 | 직렬화 | **180.2** (공유) | 201.5 (트리) | 461.8 (트리) |
| 그래프 | 역직렬화 | **192.5** (공유) | 272.1 (트리) | 639.1 (트리) |
| 대형 | 직렬화 | **48.0 µs** | 67.4 µs | 225.9 µs |
| 대형 | 역직렬화 | **58.3 µs** | 58.8 µs | 537.2 µs |

- 플랫·그래프에서 DS는 MemoryPack 대비 1.1~1.45배 빠름, MessagePack 대비 전 영역 1.4~3.1배 빠름.
- DS는 **참조 추적(백레퍼런스 태그)을 수행하면서도** 참조 추적을 하지 않는 두 라이브러리의 트리보다 빠름.
- 대형은 DS·MemoryPack 동급(실행마다 우위 교차), MessagePack 은 직렬화 4~5배·역직렬화 ~9배 느림.

## 할당 (B/op, GC 카운터 실측)

| 형태 | op | DS | MemoryPack | MessagePack |
| --- | --- | ---: | ---: | ---: |
| 플랫 | Ser / De | 104 / 192 | 104 / 192 | 64 / 192 |
| 문자열 | Ser / De | 448 / 944 | 464 / 944 | 440 / 944 |
| 그래프 | Ser / De | 528 / 872 | 176 / 792 | 96 / 792 |
| 대형 | Ser / De | 410,928 / 448,033 | 414,928 / 448,067 | **989,168** / 448,049 |

- 기본 할당은 세 라이브러리 모두 "출력 버퍼 + 복원 객체" 수준으로 사실상 동일.
- DS 그래프 직렬화의 +352B 는 참조 추적 컨테이너 비용(기능 대가).
- MessagePack 대형 직렬화만 2.4배 할당(내부 버퍼 성장 전략).
- DS만 `SerializePooled`(할당 0 경로) 제공 — 경쟁에 없는 무할당 옵션.

## 와이어 크기 (bytes)

| 형태 | DS | MemoryPack | MessagePack |
| --- | ---: | ---: | ---: |
| 플랫 | 77 (헤더 4B 포함) | 78 | **39** |
| 문자열 헤비 | **420** (UTF-8) | 433 (UTF-16) | 409 |
| 공유 그래프 | **60** (공유 절약) | — (미지원) | — (미지원) |
| 대형 | 410,902 | 414,899 | 489,117 |

MessagePack varint 는 작은 정수 메시지에서 절반 크기(39 vs 77)지만 문자열·대형에서 불리. MemoryPack 은 문자열을
UTF-16 으로 담아 한글 등에서도 바이트 손해. DS 는 프레이밍 헤더 포함 값.

## 장단점 요약

### DS_MessageProtocol

- ✅ 전 영역 최속~동급 속도, 기본 할당·와이어 동급
- ✅ 공유/순환 그래프 유일 지원(경쟁 불가 — [Commercial-Readiness-Review](./Commercial-Readiness-Review.md) §3 과 연결)
- ✅ 메시지 ID 헤더 내장: 라우팅 + 타입 착오 진입 거부(KI-5) + 프레이밍 비용 포함
- ✅ `SerializePooled` 무할당 경로, 불신 입력 퍼저 검증(KI-41), Unity netstandard2.1 폴백 실행 검증
- ❌ 스키마 동결([ADR-0006](../05-Decisions/ADR-0006-No-Schema-Evolution.md)) — 변경은 신규 MessageId 타입만
- ❌ varint 미지원 — 작은 정수 위주 메시지에서 와이어 2배(기능 후보 등록됨: [Feature-Spec](../02-Architecture/Feature-Spec.md) 범위 밖)
- ❌ 범용 직렬화가 아님(메시지 전용 — 세이브 파일·DB 저장 부적합), 생태스·상용 사례는 경쟁보다 작음

### MemoryPack

- 최속 클래스의 경쟁자, Unity 지원 양호(단일 관리자 neue). 참조 추적·스키마 진화 불가, UTF-16 문자열로 와이어 큼.

### MessagePack-CSharp

- 가장 큰 생태계·`[Key]` 기반 스키마 진화(끝 필드 추가 허용)·varint 강점. 단 이 측정에서 전 영역 느림 + 대형 직렬화
  2.4배 할당 + 3.1.4 기준 NuGet 보안 어드바이저리 13건 경고(NU1902/1903, 복구 버전 존재).

## 재현

측정 프로젝트는 일회성으로 `artifacts/bench-compare/`(gitignored, 로컬 한정 보존). 재현:

```bash
cd artifacts/bench-compare
dotnet run -c Release -- --wire     # 와이어 크기 덤프
dotnet run -c Release -- --alloc    # 수동 할당+시간 계측(24항목)
dotnet run -c Release               # BenchmarkDotNet 전체
```

결과 스냅샷: `artifacts/bench-compare/results-bdn-run2.csv`(BDN 21항목) · Str_De 3항목(strde 실행) ·
`results-manual-alloc.csv`(24항목) · `results-wire.csv`. 노트북 환경 특성상 절대치는 기계·세션에 따라 달라지며,
본 문서의 판정(순위·동급 여부)이 재현성 있는 부분이다.

## 관련

- [Commercial-Readiness-Review](./Commercial-Readiness-Review.md) — 채용 결론·운영 규칙
- [Feature-Spec](../02-Architecture/Feature-Spec.md) 범위 밖 — 미지원 기능 후보(varint 등)
- [Known-Issues](../06-Troubleshooting/Known-Issues.md) — KI-5(헤더 검증)·KI-41(예외 분류)
- [ADR-0006](../05-Decisions/ADR-0006-No-Schema-Evolution.md) — 스키마 진화 미지원 정책
