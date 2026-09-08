---
project: DS_MessageProtocol
type: analysis
status: draft
tags: [improvements, test, ci, infrastructure, benchmark]
updated: 2026-09-08
---

# Improvement — 테스트·CI·개발 인프라 (Test/CI Infrastructure Lane)

> 2차 웨이브 — 소스 코드가 아닌 **그것을 지탱하는 검증·개발 인프라**의 개선점.
> 1차 웨이브의 F1–F12·SEC-01–08·PERF-01–09 와 중복되지 않는 항목만 수록한다.
> 조사 대상: `.github/workflows/`, `Test/` 3프로젝트, `Sandbox/`, `Directory.Build.props`, `MessageProtocol.sln`, `Document/03-Reference/Packages.md`, `_meta/Changelog.md` 의 반복 수동 절차.

## 조사 범위·방법

- CI YAML(`nuget-publish.yml` — 저장소의 유일한 워크플로)을 단계별로 읽고 트리거·게이트·캐시·매트릭스·아티팩트 전략을 판정했다.
- 테스트 3프로젝트의 csproj(TFM·패키지·생성 코드 디스크 출력)와 퍼저 스케일 노브(`DeserializerFuzzTests.cs:89-90`), 벤치마크 실행 구성(`Program.cs`)을 대조했다.
- Changelog 의 사이클 기록에서 **반복되는 수동 의식**(골든 비교, 심층 퍼징 캠페인, DS_RPC 로컬 팩 검증, 라인 래핑)을 추려 자동화 여지를 판정했다.
- 1차 웨이브와의 경계: PERF-08(Unity 프로파일 수치 부재)·PERF-04(생성기 재생성 비용)·F10(IL2CPP 산출물)·F11(테스트 키트)은 기능/성능 레인 소유 — 이 문서는 그 밖의 인프라만.

## 발견 항목 요약

| ID | 항목 | 유형 | 우선순위 | 난이도 |
| -- | ---- | ---- | -------- | ------ |
| INFRA-01 | PR/push 검증 CI 부재 — 검증이 릴리스 시점에만 존재 | 검증 공백 | **P1** | 중 |
| INFRA-02 | 벤치마크 회귀 감지 체계 부재 — 결과 휘발성, 기준선 대조 수작업 | 측정 공백 | **P1** | 중 |
| INFRA-03 | 퍼저 심층 캠페인 자동화 부재 — 노브만 있고 정기 실행 없음 | 검증 공백 | **P1** | 하 |
| INFRA-04 | 골든 생성 코드 비교의 수작업 의식 — 리팩터링마다 스태시 A/B | 검증 공백 | P2 | 중 |
| INFRA-05 | CI OS/아키텍처 매트릭스 부재 — Ubuntu x64 단일 | 검증 공백 | P2 | 하 |
| INFRA-06 | 테스트 결과 가시화 부재 — TRX/실패 아티팩트 업로드 없음 | DX | P2 | 하 |
| INFRA-07 | SDK 버전 고정 부재 — 생성기 결정론 계약이 SDK 부동에 노출 | 재현성 | P2 | 하 |
| INFRA-08 | 코드 커버리지 측정 부재 — 예외 경로 커버리지 미관측 | 측정 공백 | P2 | 하 |
| INFRA-09 | 포맷·스타일 게이트 부재 — .editorconfig 없음, 수동 라인 래핑 이력 | DX | P2 | 하 |
| INFRA-10 | DS_RPC 로컬 팩 소비자 검증의 수동 반복 | 프로세스 | P2 | 중 |
| INFRA-11 | 태그–props 버전 일치 게이트 부재 | 프로세스 | P3 | 하 |
| INFRA-12 | NuGet 캐시 부재 | 비용 | P3 | 하 |
| INFRA-13 | Dependabot 부재 — 의존성 갱신 전부 수동 | 프로세스 | P3 | 하 |
| INFRA-14 | 벤치마크 실행기 선택성 부재 — 하드코딩 단일 Runner | DX | P3 | 하 |

## 상세

### INFRA-01 — PR/push 검증 CI 부재 (P1, 중)

- **근거 위치**: `.github/workflows/nuget-publish.yml:5` — 트리거가 `push: tags: v*` 하나뿐. `find .github -type f` 결과 이 워크플로가 유일.
- **현상**: 빌드·양 TFM 테스트·Sandbox 인수 전체가 **릴리스 태그 시점에만** 실행된다. main 브랜치에 올라가는 커밋·PR 은 CI 검증을 전혀 받지 않으며, 실제로는 Changelog 사이클마다 "로컬 게이트(테스트·Sandbox·DS_RPC 로컬 팩) 통과"가 수작업으로 반복되고 있다 — 그 수작업이 사실상의 PR CI 역할을 하고 있다는 뜻이다. 검증이 늦은 만큼 결함 발견도 늦는다(테스트 실패 커밋이 main 에 누적 → 다음 태그에서야 게이트가 막음).
- **영향**: 협업·다중 브랜치 시 결함 유입 시간이 릴리스 주기만큼 늘어난다. 부분적으로 완화된 점: 저장소가 단일 작성자로 운영되어 왔고 로컬 게이트 규율이 강하다는 것. 그러나 규율은 파이프라인이 아니다 — 이 저장소 자신의 철학("로컬 게이트는 절차 준수를 보장하지만 파이프라인 자체도 스스로 검증해야 한다", `nuget-publish.yml` 주석)과 같은 원리가 PR 에는 아직 적용되지 않았다.
- **구체적 제안**: `ci.yml` 신설 — `on: push(branches: main) + pull_request`, 단계: `dotnet build -c Release` → `dotnet test -c Release --no-build`(양 TFM) → Sandbox 실행 → (선택) 생성 코드 디스크 출력에 대한 골든 비교(INFRA-04 참조). `paths-ignore: Document/**` 로 문서 전용 PR 은 스킵. 릴리스 워크플로는 pack/push 전용으로 단순 유지.
- **우선순위**: P1 / **난이도**: 중 — 신규 워크플로 1개, 기존 단계 재사용. ubuntu 러너 시간이 늘지만 본 저장소 규모에선 분 단위.

### INFRA-02 — 벤치마크 회귀 감지 체계 부재 (P1, 중)

- **근거 위치**: `Test/MessageProtocol.Benchmarks/Program.cs:18` — 수동 `BenchmarkRunner.Run<>`; `git check-ignore BenchmarkDotNet.Artifacts` → **무시됨(결과 미커밋)**; `Document/03-Reference/Performance-Baseline.md` — 수치가 손으로 복사된 표.
- **현상**: 벤치마크는 개발자가 로컬에서 수동 실행하고, 결과는 무시되는 디렉터리에 휘발한다. 기준선 문서의 수치 갱신도 수동 사본이다. 과거 실적(사이클 7: ASCII 선스캔 +68% 회귀를 **수동 측정으로 발견·기각**)이 보여주듯, 성능 보존 계약("와이어 불변 + 속도 동급" 등 Changelog 의 반복 문구)을 지탱하는 유일한 장치가 기억과 수작업이다.
- **영향**: PERF-01 같은 개선이나 리팩터링이 성능을 깨도 아무 게이트가 울지 않는다. 1차 웨이브 PERF-08(Unity 프로파일 부재)과 달리 이건 **데스크톱 기준선조차 자동 대조되지 않는** 문제다.
- **구체적 제안**: ① 벤치마크 결과(`BenchmarkDotNet.Artifacts/results/*.md`)를 저장소에 커밋된 기준선으로 고정. ② `workflow_dispatch`(수동 트리거) 벤치마크 잡 추가 — 기준선 브랜치 결과와 [BenchmarkDotNet ResultsComparer](https://github.com/dotnet/performance/tree/main/src/tools/ResultsComparer)로 대조, 임계값(예: 15%) 초과 퇴보 시 실패. ③ (선택) 주간 cron 으로 무감시 측정 축적. InProcessEmitToolchain 노이즈(±10–15%)를 감안해 임계값·중앙값 비교를 명시할 것.
- **우선순위**: P1 / **난이도**: 중 — 워크플로 1개 + 비교 도구 연결. 기준선 노이즈 정책 수립이 실질 작업.

### INFRA-03 — 퍼저 심층 캠페인 자동화 부재 (P1, 하)

- **근거 위치**: `Test/MessageProtocol.Tests/DeserializerFuzzTests.cs:89-90` — `MSGPROT_FUZZ_SCALE` 노브 주석에 "로컬 심층 캠페인(예: 15) — CI 는 기본 1(속도 우선)" 명시. Changelog 사이클 28의 15배 캠페인(7시드×3만 변이×2진입 ≈ 42만 판독)은 **수동 실행** 기록.
- **현상**: 심층 퍼징의 도구(노브)는 완비됐지만 정기 실행이 없어, 깊은 결함 꼬리 탐사는 개발자가 기억해서 돌릴 때만 일어난다. 상시 회귀(2천/시드)는 릴리스 게이트마다 돌지만, 그 아래 층위는 공백.
- **영향**: 퍼저의 가치는 누적 변이량에 비례하는데, 캠페인 빈도가 사람의 기억에 의존한다. 결함 꼬리(iter 116·291 같은 사례)는 회전수에서 나온다.
- **구체적 제안**: `schedule: cron 주간` + `workflow_dispatch` 워크플로 — `MSGPROT_FUZZ_SCALE=15 dotnet test` 실행, 실패 시 issue 자동 생성, 소요 시간 기록을 아티팩트로 저장(캠페인 비용 추적). CI 기본 1은 그대로 유지(속도 우선 설계 존중).
- **우선순위**: P1 / **난이도**: 하 — 기존 노브를 부르는 워크플로 1개.

### INFRA-04 — 골든 생성 코드 비교의 수작업 의식 (P2, 중)

- **근거 위치**: `Test/MessageProtocol.Tests/MessageProtocol.Tests.csproj:7`·`NetStandardFixtures.csproj:10` — `EmitCompilerGeneratedFiles`(디스크 출력) 상시 활성; Changelog 사이클 9–11·2.3.3 — 리팩터링마다 "골든 비교 29개 `.g.cs` 바이트 동일"을 **스태시 A/B 빌드로 수동 수행**한 기록 반복.
- **현상**: "행동 보존 리팩터링 = 생성 바이트 불변" 검증이 이 저장소의 핵심 안전장치인데, 그 실행이 커밋된 골든 파일도 CI 단계도 아닌 수작업 비교다. 기존 결정론 테스트(사이클 5 KI-3)는 "같은 입력 → 같은 출력"(자기 일관성)만 보장하지, "리팩터링 전후 출력 불변"(교차 커밋)은 보장하지 않는다.
- **영향**: 이미터 리팩터링 시 실수로 생성 코드가 바뀌어도 테스트는 통과한다(와이어·왕복이 우연히 보존되는 한). 수동 의식을 잊으면 무의도적 드리프트가 그대로 커밋된다.
- **구체적 제안**: `Test/GoldenSources/`(또는 `Source/.goldens/`)에 생성 `.g.cs` 29개+ 커밋. CI/테스트 단계에서 `obj/**/generated` 디스크 출력과 diff — 불일치 시 실패 + "의도한 변경이면 골든 갱신 커밋" 안내. 골든 갱신 절차를 Packages.md 에 한 줄 문서화. 생성 파일 수가 늘어나면 자동 동기화 스크립트로 관리.
- **우선순위**: P2 / **난이도**: 중 — 골든 초기 채택 + diff 단계. INFRA-01 의 PR CI 에 흡수하면 시너지.

### INFRA-05 — CI OS/아키텍처 매트릭스 부재 (P2, 하)

- **근거 위치**: `.github/workflows/nuget-publish.yml:13` — `runs-on: ubuntu-latest` 단일.
- **현상**: 테스트는 Ubuntu x64 에서만 굴러본다. 주 개발 환경은 Windows(저장소 상태 기준)인데 CI 는 Windows 를, 그리고 ARM 은 전혀 검증하지 않는다. KI-39(ARM 메모리 순서 결함, 캐시 필드 volatile 화)는 "관찰 불가결이라 메모리 모델 추론으로 인자화"된 사례 — ARM 실행 환경이 CI 에 있었다면 추론이 아니라 측정이었을 수 있다. 이 라이브러리의 1차 소비 환경은 Unity 이며 IL2CPP 타깃에 ARM 이 포함된다.
- **영향**: OS별 파일 I/O·경로·스레드 스케줄링 차이와 ARM 순서 문제가 릴리스 후 소비자 환경에서 처음 발견될 수 있다.
- **구체적 제안**: 검증 잡을 `strategy.matrix: [ubuntu-latest, windows-latest]` 로. ARM 은 GitHub `ubuntu-24.04-arm`(공개 저장소 무료 티어 지원) 러너에서 동시 스트레스·퍼저 중심 경량 스위트만 선택 실행(`--filter`).
- **우선순위**: P2 / **난이도**: 하 — 매트릭스 선언. 러너 시간 증가만이 비용.

### INFRA-06 — 테스트 결과 가시화 부재 (P2, 하)

- **근거 위치**: `.github/workflows/nuget-publish.yml:39` — `dotnet test MessageProtocol.sln -c Release --no-build` (logger 옵션·아티팩트 업로드 없음, 양 TFM 단일 잡 순차).
- **현상**: CI 테스트 실패 시 로그는 콘솔 스크롤뿐이다. TRX 미생성, 실패 시 아티팩트 업로드 없음, TFM 병렬 분할 없음(net8 실패가 net9 실행을 막는 직렬 구조).
- **영향**: 실패 원인 파악이 재실행·스크롤 탐색으로 이어진다. net8/net9 중 하나만 실패하는 회귀(이 저장소 실제 사례: 폴백 경로가 특정 TFM 에서만 문제)의 위치 파악이 느리다.
- **구체적 제안**: `--logger "trx;LogFileName={tfm}.trx"` + `actions/upload-artifact`(`if: failure()`). 선택: 테스트 잡을 TFM 매트릭스로 분할(`dotnet test -f net8.0` / `-f net9.0`).
- **우선순위**: P2 / **난이도**: 하 — 워크플로 몇 줄.

### INFRA-07 — SDK 버전 고정 부재 (P2, 하)

- **근거 위치**: `global.json` 없음(확인). `.github/workflows/nuget-publish.yml:19-22` — `dotnet-version: 8.0.x / 9.0.x` 부동 버전.
- **현상**: 이 프로젝트의 핵심 계약 중 하나는 **생성 코드 결정론**(KI-3, 안정성 테스트)이다. 그런데 생성기의 입력인 Roslyn 컴파일러 버전이 SDK 에 묶여 있는데 SDK 고정이 없다 — CI 는 새 SDK 부 버전이 나오면 자동으로 따라가고, 로컬 개발자 SDK 와도 달라진다. 소스 생성기는 컴파일러 버전에 따라 분석 동작·진단이 달라질 수 있다.
- **영향**: 어느 날 CI 의 SDK 업그레이드로 생성 출력·진단이 달라지면 원인 규명(SDK? 코드?)에 시간이 든다. 골든 비교(INFRA-04)를 도입하면 특히 SDK 탓과 코드 탓을 분리해야 하므로 고정이 전제가 된다.
- **구체적 제안**: `global.json` `{ "sdk": { "version": "9.0.xxx", "rollForward": "latestFeature" } }` + setup-dotnet 이 이를 따르도록 정리. SDK 갱신은 의도적 커밋(골든 갱신과 묶임)으로.
- **우선순위**: P2 / **난이도**: 하 — 파일 1개.

### INFRA-08 — 코드 커버리지 측정 부재 (P2, 하)

- **근거 위치**: `Test/MessageProtocol.Tests/MessageProtocol.Tests.csproj` — coverlet 참조 없음(전 파일 검토). CI 에 커버리지 단계 없음.
- **현상**: Fact/Theory 224개 + 적대 스위트(퍼저·스트레스·경계값)로 실측 커버리지는 높을 것이나 **측정 자체가 없어** 보이지 않는다. 특히 이 저장소의 자산인 예외·가드 경로(신뢰 경계 거부 분기들)의 분기 커버리지가 관측되지 않는다.
- **영향**: "가드가 있는 줄 알았던 분기"의 공백이 테스트 추가 우선순위 판단 없이 묻힌다. 커버리지 수치가 곧 품질은 아니지만, 없으면 방향을 못 잡는다.
- **구체적 제안**: `coverlet.collector` 추가 + CI `dotnet test --collect:"XPlat Code Coverage"`. 리포트 업로드(artifacts)까지만 — 커버리지 임계치 게이트는 수치 안정화 전까지 보류(과잉 게이트 방지).
- **우선순위**: P2 / **난이도**: 하.

### INFRA-09 — 포맷·스타일 게이트 부재 (P2, 하)

- **근거 위치**: `.editorconfig` 없음(확인). Changelog 사이클 26 — "3개 과잉 길이 라인 래핑·LF 정규화"를 **수동** 수행한 기록.
- **현상**: 포맷 규칙이 도구로 강제되지 않아 개행·길이·줄끔 공백이 커밋마다 사람 손으로 관리됐다. 에디터별 설정 편차도 어디에도 고정돼 있지 않다(`MessageProtocol.sln.DotSettings.user` 는 존재하지만 gitignore 로 이미 무시되는 개인 파일 — 검증 완료).
- **영향**: 사소하지만 반복되는 수작업 + 리뷰 노이즈. PR CI(INFRA-01)와 세트로 묶을 때 비용이 거의 0.
- **구체적 제안**: `.editorconfig` 도입(기존 스타일 준수: 탭/스페이스·UTF-8·LF·줄 길이 관행 반영) + CI `dotnet format --verify-no-changes`.
- **우선순위**: P2 / **난이도**: 하.

### INFRA-10 — DS_RPC 로컬 팩 소비자 검증의 수동 반복 (P2, 중)

- **근거 위치**: `Document/03-Reference/Packages.md:55-56` — 수동 절차 명문화(로컬 팩 검증 + 발행 후 실재 확인 명령). Changelog 의 사실상 모든 릴리스 항목에 "DS_RPC 로컬 팩 빌드+테스트 통과 후 태그 푸시" 반복, 2.3.9 사후엔 nuget.org 소비 검증 명령까지 수동 수행(사이클 31–32).
- **현상**: 이 저장소의 가장 중요한 소비자 계약(형제 프로젝트 DS_RPC) 검증이 매 릴리스 수동 의식이다. 절차 문서가 정교할수록 — 오히려 — 반복 수작업이 정당화되는 구조. 발행 후 색인 전파 확인(flatcontainer 목록·GET 200)도 명령을 손으로 친다.
- **영향**: 절차 누락 시(바쁜 릴리스) 소비자 회귀가 태그 푸시 후 발견된다. 사이클 31(NU1102 로 이행 실패 → PENDING 등록)처럼 절차 자체가 실패 상태를 만난다.
- **구체적 제안**: `scripts/verify-consumer.ps1|sh` — ① 로컬 pack → ② DS_RPC 를 로컬 피드로 복원·빌드·테스트(기존 절차 자동화) ③ 발행 후 확인 모드(`--post-push <version>`: flatcontainer 목록 + nupkg GET 200 폴링, 타임아웃 포함). 릴리스 워크플로 푸시 단계 뒤에 ③ 자동 실행도 가능.
- **우선순위**: P2 / **난이도**: 중 — DS_RPC 저장소 경로·피드 구성을 스크립트 인자화하는 작업.

### INFRA-11 — 태그–props 버전 일치 게이트 부재 (P3, 하)

- **근거 위치**: `Source/Directory.Build.props:8` — `<Version>2.3.9</Version>` 수동 관리. `.github/workflows/nuget-publish.yml:25-30` — 패키지 버전은 태그에서 추출해 `-p:Version` 으로 주입(props 무시).
- **현상**: 게시 버전의 진실원은 태그인데, props 의 Version 은 수동 별도 갱신 대상이라 태그와 어긋날 수 있다. 어긋나도 CI 는 조용히 태그 버전으로 게시한다 — 로컬 빌드·NuGet 캐시·문서 표기가 모두 props 버전을 따르는 동안 패키지만 다른 버전이 되는 불일치가 가능.
- **영향**: 낮지만 실재하는 혼란원(기준선 문서·이슈 재현 지시 시 "2.3.x 로 재현"이 어느 쪽인지 모호).
- **구체적 제안**: 릴리스 워크플로 초반에 `Source/Directory.Build.props` 의 Version 과 태그 일치 검사 — 불일치 시 명확한 오류로 실패(또는 자동 커밋으로 props 승격).
- **우선순위**: P3 / **난이도**: 하.

### INFRA-12 — NuGet 캐시 부재 (P3, 하)

- **근거 위치**: `.github/workflows/nuget-publish.yml:19-22` — `actions/setup-dotnet@v5` 에 `cache:` 옵션 없음, `actions/cache` 단계 없음.
- **현상**: 모든 CI 실행이 NuGet 패키지 전체를 새로 복원한다. 저장소 규모가 작아 체감 비용은 분 단위 이하.
- **영향**: 러너 시간·외부 피드 의존(일시적 nuget.org 지연 시 실패). INFRA-01 로 실행 빈도가 늘어날수록 누적 비용이 커진다.
- **구체적 제안**: setup-dotnet 에 `cache: true` + `cache-dependency-path`(csproj들). 패키지 수가 적어 효과는 제한적 — 다른 개선과 함께 손보는 순서.
- **우선순위**: P3 / **난이도**: 하.

### INFRA-13 — Dependabot 부재 (P3, 하)

- **근거 위치**: `.github/dependabot.yml` 없음(`find .github` 확인). 주요 고정 의존성: xunit 2.9.3·Microsoft.NET.Test.Sdk 17.12.0·Roslyn(CSharp) 4.14.0·BenchmarkDotNet 0.15.8(Tests/Benchmarks csproj), actions v6/v5/v4(워크플로).
- **현상**: 의존성 갱신이 전부 수동. 특히 Roslyn 4.x 는 소스 생성기 동작과 직결되는 의존성이라 갱신 시점에 생성 출력·진단 변화 검증이 필요한데, 알림 체계가 없어 갱신 자체가 늦어진다.
- **영향**: 보안 패치·호출부 호환 갱신 누락. 반대로 갱신이 일어나도 PR 단위 검증(INFRA-01)이 없으면 위험.
- **구체적 제안**: `dependabot.yml` — `package-ecosystem: nuget`(Test/·Source/) + `github-actions`, 주간. Roslyn 갱신 PR 에는 생성 코드 골든(INFRA-04)이 자동 검증으로 붙는 구조가 이상적.
- **우선순위**: P3 / **난이도**: 하.

### INFRA-14 — 벤치마크 실행기 선택성 부재 (P3, 하)

- **근거 위치**: `Test/MessageProtocol.Benchmarks/Program.cs:18` — `BenchmarkRunner.Run<SerializationBenchmarks>()` 하드코딩. 시나리오 10개가 단일 클래스에 모임.
- **현상**: 특정 시나리오만 재려면 코드 수정 또는 BDN 콘솔 인자 조합을 외워야 한다. 빠른 로컬 측정(ShortRun 잡)과 정밀 기준선 측정(Default 잡)의 프로필 구분도 없다 — InProcessEmitToolchain 노이즈(±10–15%) 속에서 "빠르게 감만 잡기" 실행이 하나뿐이다.
- **영향**: 측정 진입 장벽이 PERF 항목(1차 웨이브 PERF-01·05·06 등 "벤치마크 편승 측정" 권고)의 실행을 늦춘다.
- **구체적 제안**: `BenchmarkSwitcher` 전환(`BenchmarkRunner.Run(typeof(SerializationBenchmarks).Assembly, args)`) + 두 잡 프로필 정의(QuickRun: ShortRun·기준선: Default)와 사용법을 Performance-Baseline.md 에 3줄 문서화.
- **우선순위**: P3 / **난이도**: 하.

## 조사했으나 보류·판정한 항목 (소진 기록)

| 대상 | 판정 | 근거 |
| ---- | ---- | ---- |
| Unity Editor/Mono 실런타임 CI 통합 | 보류 | Unity 라이선스·에디터 설치·헤드리스 실행 비용이 검증 이득을 상회. Unity 프로파일 수치 부재 자체는 PERF-08(1차 웨이브)이 소유 — 이 레인에서 추가 인프라로 급을 매기기 어려움 |
| `TreatWarningsAsErrors` 전역 강제 | 보류 | 제품 코드 경고 정책은 소스 레인 판단 사항. 현재 경고 0 관리 중이며 RS2007/2008 등 위험 게이트는 이미 존재(`CodeGenerator.csproj:13`). CI 옵션 검증 형태는 가능하나 단독 가치 낮음 |
| 푸시 단계 `--skip-duplicate` 제거 검토 | **유지 판정** | 태그 불변성 가정 하 중복 게시는 발생하지 않고, 게이트 실패 후 재실행 시 안전망으로 기여. 제거 시 이득 없음 |
| Sandbox Debug 구성 실행 추가 | 보류 | 디버그 전용 어서트·경로가 현재 Sandbox/소스에 존재하지 않음(검토). Release 단일로 충분 |
| 테스트 파일 15개 재조직화 | 보류 | 도메인별 응집(가드·퍼저·스트레스·스냅샷·왕복)이 명확해 구조 문제 없음. 커버리지 구멍 판별은 INFRA-08 측정이 선행되어야 함 |
| xUnit → 다른 러너(MSTest/NUnit) 전환 | 기각 | 현 체계(xunit 2.9.3) 안정적, 전환은 비용만 추가. 러너 갱신은 INFRA-13 이 따라감 |
| TFM 별 테스트 잡 분할 | INFRA-06에 흡수 | 단독 항목이 아닌 결과 가시화 개선의 일부로 충분 |
| 벤치마크 CI 상시 실행(PR마다) | 기각 | InProcessEmitToolchain 노이즈(±10–15%)로 PR 단위 판정 불가 — INFRA-02 의 수동 트리거+기준선 대조가 올바른 형태 |

## 관련

- [README](./README.md) — 전체 인덱스
- [Improvement-Feature](./Improvement-Feature.md) · [Improvement-Security](./Improvement-Security.md) · [Improvement-Performance](./Improvement-Performance.md) — 1차 웨이브
- [Performance-Baseline](../03-Reference/Performance-Baseline.md) — 기준선 문서(INFRA-02 대상)
- [Packages](../03-Reference/Packages.md) — 패키징·검증 절차(INFRA-10 대상)
