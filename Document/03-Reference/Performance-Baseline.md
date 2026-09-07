---
project: DS_MessageProtocol
type: reference
status: stable
tags: [performance, benchmark, baseline]
updated: 2026-09-08
---

# Performance-Baseline — 직렬화 핫 경로 기준선

> BenchmarkDotNet 기준선. **수치 근거가 필요한 성능 변경은 이 표와 비교해 판정한다.**
> 처음 기록: 2026-09-08(2.3.1 + KI-5 헤더 검증 코드) — 이전까지 기록된 수치가 없었다.

## 실행

```bash
dotnet run -c Release --project Test/MessageProtocol.Benchmarks
```

- InProcessEmitToolchain(저장소에 Legacy 동명 프로젝트가 있어 csproj 도구 체인이 실패) — **상대 비교**에 적합, 절대값은 참고.
- net9.0, `[MemoryDiagnoser]`, BenchmarkDotNet 0.15.8.
- 실행 약 6분(시나리오 5개). 수치는 노이즈 ±10~15%(인프로세스 도구 체인) — 판정은 중앙값·할당 함께 볼 것.

## 기준선 (2026-09-08)

| 시나리오 | 내용 | Median | Allocated |
| --- | --- | --- | --- |
| SerializeBytes | `BenchMessage`(int·long·float·문자열 17자·`List<int>` 8) → byte[] | 54.57 ns | 104 B |
| SerializeStringHeavy | `StringHeavyMessage`(ASCII 100자 문자열 4개) → byte[] | 138.08 ns | 448 B |
| DeserializeStringHeavy | 위 바이트 역직렬화(문자열 4개 재생성 포함) | 168.55 ns | 944 B |
| DeserializeTyped | `BenchMessage` 제네릭 역직렬화 | 52.79 ns | 192 B |
| DeserializeDispatch | object dispatch 역직렬화 | 71.56 ns | 192 B |

참고: 할당의 대부분은 byte[] 결과 자체(정확 크기 복사)와 역직렬화 문자열 재생성 — 풀링 경로(`SerializePooled`·`PooledBuffer`)는 결과 복사를 회피한다(현재 벤치마크 미포함 — 필요 시 추가).

## 측정된 부정 결과 — WriteString ASCII 사전 스캔 (재시도 금지 근거)

2026-09-08 핫패스 감사(FINDING 2)의 가설: `WriteString` 의 `3n+3` 보수 용량 예약이 ASCII 문자열에 과잉(100자 → 307B 예약·실제 104B)이라, 전체가 ASCII 면 정확 산정(4+n)해 Grow+복사를 피하면 이득일 것.

**실험 결과: 부정.** ASCII 선검사(`CountAsciiBytes` 전수 스캔)를 넣자 `SerializeStringHeavy` 144.43 → **242.74 ns(+68%)** — 회피한 Grow·BlockCopy(~30ns)보다 이중 스캔 비용이 컸다. 되돌림 후 139.10 ns로 회복(원 기준선 이내). 결론: 보수 예약 + 풀 버킷은 이 워크로드에서 이미 근최적이며, `GetBytes` 가 이미 스캔·인코딩을 한 번에 하므로 선검사는 항상 이중 비용이다. SIMD 스캔(`Vector<ushort>`)으로 재시도하려면 이 표 대비 **양수 개선 수치**를 동반할 것.

## 같은 감사에서 수용한 변경

- `SerializeContext`·`DeserializeContext` 사전 승격 시 초기 용량 8(리사이즈 1→3→7 지연 제거, 할당 변화 없음) — 커밋 참조.

## 갭 (필요시 추가)

- `SerializePooled`/`PooledBuffer` 경로, 참조 추적 그래프(공유·순환), 대형 컬렉션, net8.0 TFM.
