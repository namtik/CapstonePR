# 다음 세션 핸드오프 (2026-06-05 — 효과추적 보상 ① 병합·컴파일 완료)

> 브랜치 `mig/sys0.2`. 작업 범위 = **SampleScene 전용** (BattleTestScene/Controller 손대지 않음, 공유 전투 스크립트는 OK).

---

## 🎯 즉시 할 일 — Unity 플레이 검증 (효과추적 보상 ①)

코드 병합·컴파일(`dotnet build` 오류 0) 완료. 이제 **Unity Play로 런타임 검증**만 남음:

1. `NewBattleController` 인스펙터: **`onlineLearning` ON**, **`logVerbose` ON** (선택 `aiExplorationEpsilon` 0.15 유지).
   - 화면 좌상단 **FCM Debug Overlay**(`Tab` 토글, 기본 항상표시)로 특성 x[6]·FCM유형 μ[5]·컨텍스트·Q값/마스크/선택·**보상 r+drift**를 실시간 확인. (`AiDebug` 허브 폴링 — `EnemyDisruptionAI.Decide`가 결정을, 보상 정산이 reward를 게시.)
2. 한 판 플레이하며 콘솔 `[적AI학습]` 로그 확인:
   - **즉시형**(흡수/회복/버리기): `... r=±0.00 (즉시) drift=...` — 발동 즉시 정산.
   - **발현형**(저주/탈진/강화): `... r=±0.00 (발현) drift=...` — 자해발생/카드뽑힘/적공격 시 정산.
   - `drift` 가 점점 증가하면 온라인 가중치가 베이스에서 멀어지는 것(학습 진행).
3. 각 보상이 "의도 달성"대로인지:
   | 방해 | 기대 보상 |
   |---|---|
   | 저주 | 자해 1회 +0.75 / 2회 +1.0 / **회피(안 씀) +0.2 (실패 아님)** |
   | 탈진 | 삽입 카드 뽑힘 +0.7 / 안 뽑히고 전투끝 −0.3 |
   | 흡수 | 각성 게이지 높을 때 + / 낮을 때 − |
   | 강화 | 적 공격 발현 +0.4~(받은 HP피해 비례 가산) / 미발현 −0.3 |
   | 회복 | 적 빈사일수록 + / 만피 회복 − |
   | 버리기 | 손패 가득 + / 적을 때 − |
4. 이상 없으면: `git stash drop stash@{0}` (효과추적 stash 정리) + GitHub Desktop 으로 `NewBattleController.cs` 커밋.

> ⚠️ stash 정리·커밋은 **플레이 검증 통과 후에만**. 그 전엔 `stash@{0}` 보존(롤백 안전망).

---

## 🆕 추가 (2026-06-10) — 온라인 Q-러닝에 TD(0) 적용 (컴파일 ✓, 플레이검증 대기)

밴딧(즉시보상 회귀)이던 온라인 학습을 **TD(0)** 로 확장. "스텝"=**방해행동 결정 1회**(게이지 도달), 실제 시간 아님 → 게이지 턴제와 정합. `target = r + γ·max_a' Q(s',a')`, s'=다음 방해결정 상태.

- **인스펙터 신규**: `NewBattleController.aiRewardGamma`(Range 0~0.95, **기본 0.6**). `γ=0`이면 기존 즉시보상과 **정확히 동일**(롤백 스위치 겸용). onlineLearning ON일 때만 적용.
- `OnlineQLearner`: `ObserveTD(mu,ctx,a,r,nextMu,nextCtx,γ)` + `AddExperience`/`MaxQ` 추가. 버퍼 `Experience.reward`→`target`. 타깃 [-2,2] 클램프(발산 방지). L2-to-base·리플레이·LR 안정화 그대로.
- `NewBattleController`: `TdTransition` 큐 + `TdOnDecision`(결정 시 직전 전이 s' 채우고 완성분 학습→새 전이 적재) / `TdSetReward`(membership 레퍼런스로 매칭) / `TdFlush` / `TdFlushTerminal`(전투종료=부트스트랩0). `ObserveDisruption`·`ResolveTrack`이 γ>0이면 큐로 라우팅. 전투 시작 시 `_track=null`+`_tdQueue.Clear()`, `EndBattle`에서 `TdFlushTerminal()`.
- **검증 추가**: `logVerbose` 로그가 `(TD γ=0.60, 즉시/발현)` 또는 `(TD 터미널)` 로 뜨는지, γ 바꿔가며(0=기존, 0.6) drift 거동 확인. γ=0이면 로그가 다시 `(즉시)/(발현)`.
- 검증 환경 메모: Unity 생성 `Assembly-CSharp.csproj`가 **stale**(삭제파일 62개 참조 + 신규파일 누락 ex.`ComboSkillShopItem.cs`) → `dotnet build` 직접은 실패함. 디스크 실제 .cs로 컴파일목록 재생성한 임시 csproj로 검증 = **오류 0**. Unity 에디터 열면 csproj 자동 재생성됨.

---

## ✅ 이번 세션 완료 (`dotnet build Assembly-CSharp.csproj` → 오류 0 / 신규 경고 0)

### 적 AI 보상 ① "효과추적(의도 달성)"으로 재설계 — `stash@{0}` 수동 병합
- 보상② (`CloseDisruptionReward` = ΔHP + 각성swing, 다음-적-공격 윈도우) **완전 제거**. 관련 필드/메서드(`_pendingDisruption`, `rewardSafetyTimeout`, `awakenRewardWeight`, `Begin/Tick/CloseDisruptionReward`) 전부 삭제.
- 신규 골격: `DisruptionTrack` 클래스 + `BeginTrack` / `ObserveDisruption` / `ResolveTrack` / `ResolveTrackUnfired` / `TickDisruptionTrack`.
  - **즉시형**(흡수/회복/버리기) = 발동 직전 상황으로 즉시 측정(`ObserveDisruption`).
  - **지연형**(저주/탈진/강화) = 발현 이벤트까지 추적(동시 1개, 새 추적 시작 시 직전 미발현분 마감).
- 이벤트 훅:
  - `OnCardDrawnHandler` → 탈진 삽입 카드가 뽑히면 `ResolveTrack(0.7)`.
  - `OnEnemyAttackFiredHandler` → 강화 걸린 채 적 공격 발생 시 `ResolveTrack(EnhanceReward)`.
  - `ApplyCurseOnCardUse` → 저주 자해 시 `curseHits++`, 저주 소진 시 `ResolveTrack(CurseReward)`.
  - `EndBattle` / 타임아웃(25s) → `ResolveTrackUnfired` (저주는 부분 자해/회피분, 그 외 −0.3).
- 행동별 보상공식(모두 [-1,1]):
  - `AbsorbReward` = 각성 근접도(prox·2−0.6), `RecoverReward` = 적 빈사도((1−ratio)·2−1), `DiscardReward` = 손패 압박(pressure·2−0.8).
  - `CurseReward`: **hits=0 → +0.2(회피=제약 성공, 실패 아님)**, 1 → 0.75, 2 → 1.0. ※ stash 원본의 `−0.3` 을 사용자 통찰대로 `+0.2` 로 수정.
  - `EnhanceReward` = 0.4(발현 바닥, 가드 흡수도 성공 인정) + 받은 HP피해율·3.
- 입출력 재설계(덱→플레이빈도 입력, self-state 마스크)·**적 공격 시 각성 −5**·`Decide(... curseActive, enhanceActive, awakenGauge)` 시그니처는 **그대로 보존**.

### 핵심 타이밍 확인 (EnhanceReward HP 측정이 정상인 이유)
- `OnEnemyAttackFiredHandler`는 `EnemyStat.OnGaugeFull` 에 구독됨. 같은 이벤트에 `EnemyController.HandleGaugeFull`이 **먼저** 구독(객체 생성 시) → 멀티캐스트 순서상 먼저 실행.
- `HandleGaugeFull`이 `StartCoroutine(ExecuteMultiHit)` 호출 → 코루틴이 첫 `yield`(`WaitForSeconds 0.15`) **전에** `player.TakeDamage`를 동기 실행. 따라서 핸들러가 도는 시점엔 (신시스템 hitCount=1) 피해가 **이미 적용**됨 → `playerHpAtFire − currentHp` 가 실제 피해를 잡음.
- 보상②가 봤던 "플HP−0.00"은 플레이어 **가드/블록 흡수** 케이스로 추정 → 그래서 EnhanceReward는 +0.4 바닥값으로 발현 자체를 성공 인정(설계 의도 유지).

---

## ⚠️ git / 상태
- `NewBattleController.cs` = **modified, 미커밋** (효과추적 ① 병합분). GitHub Desktop 으로 커밋 예정(검증 후).
- `stash@{0}` = 효과추적 원본(병합 완료, **검증 통과 후 drop**).
- 이전 커밋: `cf0988d AI 업데이트`(입출력 재설계), `77d5044 업데이트5`(보상② 윈도우 변형 + 보고서) — 둘 다 푸시됨.
- 컴파일 OK (`dotnet build Assembly-CSharp.csproj`, Unity 6000.3.1f1).

## 핵심 통찰/결정 (반드시 유지)
- **보상 = 각 방해의 "의도 달성"** (HP/결과 숫자 아님). ②가 귀속 노이즈로 실패한 교훈 → ① 효과추적으로 전환 완료.
- **저주: HP 못 깎아도 실패 아님** (회피=제약 성공 +0.2).
- **오프라인 코드는 이 작업에 수정 불필요**: ①은 *온라인* 보상(휴리스틱 베이스에서 warm-start → 게임 중 효과추적으로 RBFN Q-가중치 미세조정). 오프라인(`FCMRbfnoffline_0.2`)은 휴리스틱 기반 사전학습용. 완전 정합(CSV에 효과추적 보상 소급 기록 + 오프라인이 그 보상으로 재학습)은 **나중 실데이터 재학습 때**.

## 기존 잔여 TODO (적 AI 마무리 후)
- 콤보·유물 보상 UI / 맵 개편 ([gdd-0.6v-reflection] 메모리).
- AI 실데이터 재학습 루프(`logAiDecisions` ON → Python 재학습 → `EnemyAiModel.cs` 교체). ⚠️ epsilon>0 유지(행동 다양성).

## 참조
- 적 AI: `Assets/Script/Battle/AI/` + `NewBattleController.cs`(효과추적 = `DisruptionTrack`/`BeginTrack`/`ResolveTrack`/`ObserveDisruption` + 보상공식 `*Reward`).
- 오프라인: `C:\Users\dusdn\OneDrive\바탕 화면\FcmRbfnCode (1)\FcmRbfnCode\FCMRbfnoffline_0.2\`
- 보고서: `docs/ai-report/`
- 메모리: `fcm-rbfn-ai-integration.md`, `gdd-0.6v-reflection.md`, `ai-report-fcm-rbfn-deliverable.md`
