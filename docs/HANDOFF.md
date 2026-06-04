# 다음 세션 핸드오프 (2026-06-05 기준)

> 브랜치 `mig/sys0.2`. 작업 범위 = **SampleScene 전용** (BattleTestScene/Controller 손대지 않음, 공유 전투 스크립트는 OK).

---

## 🎯 즉시 할 일 — 적 AI 보상 ① "효과추적(의도 달성)" 재설계 (확정)

②(단기 종합 결과) 보상이 **귀속 노이즈로 실패** → ①(행동별 의도) 전환하기로 사용자와 합의.

1. **`stash@{0}` 효과추적 복구 → 현재 코드에 병합**
   - `git stash show -p stash@{0}` 로 내용 확인.
   - ⚠️ stash는 *효과추적 시점*의 NewBattleController라 현재(입출력재설계+보상②)와 **충돌**. `git stash apply`는 충돌 → **수동 병합** 권장.
   - 효과추적 골격: `DisruptionTrack` 클래스 + `BeginTrack/ObserveDisruption/ResolveTrack/ResolveTrackUnfired` + 즉시형(흡수·회복·버리기 발동 즉시)·지연형(저주·탈진·강화 발현까지 추적) 분기 + 이벤트 훅(`ApplyCurseOnCardUse`=저주 자해, `OnCardDrawn`=탈진, `OnEnemyAttackFired`=강화).

2. **보상을 각 방해의 "의도 달성"으로 (HP/결과숫자 아님)** — 사용자 핵심 통찰:
   | 방해 | 성공 측정 |
   |---|---|
   | **저주** | **자해(씀) 강한+ / 회피(안 씀) 약한+ — 자해 0회도 실패 아님(제약 성공)** |
   | 탈진 | 그 카드 뽑힘(손패 낭비) + |
   | 흡수 | 각성 게이지 깎인 양 |
   | 강화 | 강화된 적 공격의 추가 피해 |
   | 회복 | 빈사일수록 가치↑, 만피 회복은 낭비(−) |
   | 버리기 | 버린 카드 가치 |
   - stash의 `CurseReward`는 `자해0회=−0.3(실패)`였음 → **`+0.2(제약 성공)`로 고칠 것.**

3. **②의 단기종합 보상 제거** (현재 `CloseDisruptionReward` = `2·플HP감소 − 2·적HP감소 + W·각성swing`).

4. 컴파일 + `onlineLearning` ON + `logVerbose` 로 `[적AI학습]` 로그 검증.

---

## ✅ 이번 세션 완료 (컴파일·플레이 확인됨)

### 적 AI 입출력 재설계
- **입력**: 덱 구성 → **실제 플레이 빈도** (`RunDeckState` 카테고리+코스트 윈도우=15, `BuildFeatures` 사용 프로파일, **p5 = 평균 사용 코스트**).
- **출력**: 6행동 + **self-state 마스크**(저주/강화 중복 제외, 게이지0 흡수 제외). `EnemyDisruptionAI.Decide(... curseActive, enhanceActive, awakenGauge)` 시그니처.
- **적 공격 시 각성 −5** (`OnEnemyAttackFiredHandler`) — 피버 빌드업 vs 적 공격 레이스.
- 파일: `EnemyController`(IsNextAttackBuffed), `RunDeckState`, `EnemyDisruptionAI`, `NewBattleController`.
- 검증: 로그 CSV(플레이 빈도 변동 확인), `[적AI학습]` 콘솔(정산 트리거 3종·drift 증가 확인).
- 한계: `f5≈1.0`(기본덱 저코스트라 초반 변별 약함, 정상). self-state는 CSV 미기록이라 마스크 검증 불완전.

### 보상 ② (단기 종합) — 구현했으나 **실패** (교훈)
- `CloseDisruptionReward` = ΔHP + 각성swing, 휴리스틱/rewardBlend 제거, 윈도우=다음 적 공격.
- **데이터상 거의 전부 음수 보상**. 원인: ①`플HP-0.00`(적 공격 피해 정산 전 타이밍) ②적HP감소·각성swing이 플레이어 *정상 행동*(귀속 노이즈) ③6행동 발현 시점 제각각 → 단일 윈도우로 못 잡음.

### 손패 UI 버그 4건 — **017def3 "버그 수정"으로 커밋됨**
- 슬롯 sibling 정규화, Refresh raycast 복원(`EnsureRaycastable`), 픽커/뷰어 손패 비활성(`SetHandInteractable`), 선택모드 hover 가드(`IsCardSelectable`).

### 기타 (017def3 커밋)
- 카드보상: 자리별 속성 80/20(`SlotElements`) + 등급 50/35/15(`RarityWeight`) + `RunDeckState.startingDeck` 인스펙터.
- FCM-RBFN 학술 보고서: `docs/ai-report/` (md+docx+그림5+시뮬코드).
- 오프라인 `fcm_retrain_gk.py` FEAT_NAMES 라벨을 플레이 빈도로.

---

## ⚠️ git / 상태
- **GitHub Desktop이 세션 중 `017def3 "버그 수정"` 커밋함**(손패UI/보상/덱). git status에 그 변경이 안 보이는 건 *정상*(이미 커밋됨).
- `NewBattleController`는 modified(입출력재설계+보상②, **미커밋**).
- **`stash@{0}`** = 효과추적 (복구 대기).
- 컴파일 OK (게임 로그 정상 생성).

## 핵심 통찰/결정 (반드시 유지)
- **보상 = 각 방해의 "의도 달성"** (HP/결과 숫자 아님). ②가 귀속 노이즈로 실패한 교훈.
- **저주: HP 못 깎아도 실패 아님** (회피=제약 성공).
- **오프라인 코드: ①은 온라인 보상이라 오프라인 필수 수정 없음** (휴리스틱 베이스 + 온라인 효과추적). 완전 정합(CSV에 보상 소급 기록 + 오프라인이 그 보상 사용)은 나중 실데이터 재학습 때.

## 기존 잔여 TODO (적 AI 마무리 후)
- 콤보·유물 보상 UI / 맵 개편 ([gdd-0.6v-reflection] 메모리).
- AI 실데이터 재학습 루프(`logAiDecisions` ON → Python 재학습 → `EnemyAiModel.cs` 교체).

## 참조
- 적 AI: `Assets/Script/Battle/AI/` + `NewBattleController.cs`(보상 = `CloseDisruptionReward`)
- 오프라인: `C:\Users\dusdn\OneDrive\바탕 화면\FcmRbfnCode (1)\FcmRbfnCode\FCMRbfnoffline_0.2\`
- 보고서: `docs/ai-report/`
- 메모리: `fcm-rbfn-ai-integration.md`, `gdd-0.6v-reflection.md`, `ai-report-fcm-rbfn-deliverable.md`
