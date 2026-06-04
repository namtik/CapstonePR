# 다음 세션 핸드오프 (2026-06-04 기준)

> 브랜치 `mig/sys0.2`. 작업 범위 = **SampleScene(실제 게임) 전용**. 격리 BattleTestScene/BattleTestController는 손대지 않음(공유 전투 스크립트 편집은 OK).

---

## ⚠️ 가장 먼저 할 일 (검증 게이트)
이번 세션의 **모든 코드 변경은 미컴파일 상태**다. 순서:
1. **Unity 열어 컴파일 에러 확인** — 신규 `Assets/Script/Battle/AI/*` + 다수 편집(NewBattleController/EnemyStat/EnemyController/Player/CardEffectResolver/RunDeckState/CardHandHUD/Roundmanager/NewBattleSystemBootstrap/EnemyDisruptionAI).
2. **SampleScene 씬 셋업**(아래 체크리스트) — 이게 돼야 신규 전투 시스템이 돌고 그 위 기능들(AI/보상/각성/방해)을 검증 가능.
3. 플레이로 핵심 기능 확인.

---

## SampleScene 씬 셋업 체크리스트 (검증 전 필수)
SampleScene엔 신규 시스템(CardHandHUD/NewBattleController)이 **아직 배치 안 됨**. 코드 자동생성으론 CardHandHUD가 비어서 안 그려짐 → 수동 작업 필요:
1. **CardHandHUD 가져오기 (method A)**: BattleTestScene에서 `CombatStage > Canvas`(handRoot/Comboslot/SkillIconParent/dragLayer 포함) + 최상위 `CardHandHUD`를 **함께 복사** → SampleScene `CombatStage` 밑으로. 붙인 Canvas의 Render Camera 확인. `CardHandHUD` Inspector에 Missing 참조 없는지 확인.
2. **부트스트랩 배치**: SampleScene 영속 오브젝트에 `NewBattleSystemBootstrap` 컴포넌트 추가.
3. **NewBattleController·RelicManager 씬 루트에 미리 배치**(권장, 인스펙터 설정용) — 부트스트랩이 있으면 찾아 씀(중복 생성 안 함). RelicManager는 relicDefinitions(유물 아이콘) 설정.
4. **각성 게이지**: SampleScene엔 Player 오브젝트도 "PlayerHpBar"도 없음 → 게이지는 **상단 중앙 fallback**으로 뜸(방금 fix). HP바 아래로 붙이려면 ⓐ HP 슬라이더 이름을 `PlayerHpBar`로, 또는 ⓑ `CardHandHUD.awakenGaugeAnchor`에 직접 RectTransform 드래그.

---

## 이번 세션에 한 일 (전부 미검증/미컴파일)

### 전투 버그픽스
- **보상↔카드효과 겹침 레이스**: EndBattle이 진행중 카드연출/지연효과 취소 + `ResolveCardUse`에 `!_inBattle` 가드. (CardHandHUD `CancelCardUsePresentations`)
- **카드 121 무작위 소멸**: `CardEffectResolver` EXHAUST_CARD에 `select=RANDOM` 분기 추가(패 무작위 1장 소멸+NotifyExile).
- **선택/픽커 중 D 드로우**: `HandleInput`에 가드(IsSelectionMode/Picker/Viewer/연출 중 차단).
- **카드 410 영구 공격력**: 런 단위 지속(`RunDeckState.PermanentAttackPower`, GAIN_STAT Permanent=true, StartBattle서 복원).

### 기획서 0.6v 반영
저주 5→6 / 휴식 30→20% / 카드보상 3→4장 / 방해행동 4종→**6종**(흡수·강화 + 회복·버리기) / 기본덱 8장(`DefaultStartingDeckEntries`) / 연쇄=공격력10% / 적게이지 per-enemy 10~30(`EnemyData.actionGaugeMax`) / 스택 999 / 티어 골드(일반30-50·정예100-150·보스200-250).

### FCM-RBFN 적 AI (방해행동 선택)
- 신규 `Assets/Script/Battle/AI/`: `EnemyAiModel`(임베드 가중치=**합성, 플레이스홀더**), `EnemyDisruptionAI`(Classify 키워드기준+특성6+컨텍스트8+μ+Q+마스크+argmax), `DisruptionLogger`(CSV).
- `RunDeckState` 프로파일(f4/f7), `NewBattleController` 통합 + 토글(`useAiDisruption`/`aiExplorationEpsilon`/`logAiDecisions`).
- **하이브리드 온라인 학습**: `OnlineQLearner`(세션 static, warm-start+미니배치 SGD+베이스 L2) + 보상 윈도우(4s, blend=휴리스틱+ΔHP) — 토글 `onlineLearning`(기본 OFF)/`rewardBlend`.

### 도구/문서
- 각성 게이지 fallback 위치, 콤보 런타임 추가(`RefreshOwnedCombosRuntime`/`AddOwnedCombo` + 에디터 "지금 적용" 버튼, `debugAddComboRefId`).
- 문서: `시스템_명세서.md`(개요), `docs/systems/`(시스템별 10 + README), `docs/systems/enemy-ai-pipeline.md`(수식+코드+수치예시+설계근거).

---

## TODO (우선순위)
1. **[게이트] 컴파일 + 씬셋업 + 플레이 검증** (위 2개 섹션).
2. **버그1 미해결**: "패 특정 자리 카드 선택 안됨"(부채꼴 겹침으로 가려진 카드 hover/클릭 불가). **사용자 결정 대기** → (A) 선택모드에서 손패 펼치기 / (B) 패 선택을 픽커 그리드로. 결정되면 구현.
3. **콤보·유물 보상 UI** (task #9 잔여 — 티어 골드는 됨): 정예/보스 콤보 4택1 + 보스 유물 3택1. RunComboState(런 콤보 보유) 신설 + 픽 UI. 현재 콤보는 인스펙터 수동 추가만.
4. **맵 구조 개편** (task #10): 고정 층(1-2전투/5유물/10보스), 스테이지 2회 클리어=게임클리어, `NodeType.Relic`+유물 라운드. (+상점/이벤트/휴식 명상·깨달음 선택 UI는 기획서 미반영분)
5. **적 AI 실데이터 루프**: `logAiDecisions` ON 수집 → Python(`fcm_retrain_gk.py`/`rbfn_train_gk.py`) 재학습 → 산출 .cs를 `EnemyAiModel.cs`에 교체(현재 합성 가중치 대체).
6. **온라인 학습 정교화(선택)**: 보상 outcome에 행동별 의도성공(저주발동/각성지연) 항 가산, 학습 모니터 UI(drift/행동분포).
7. **미세 항목**: 보스 라운드 `SpawnNextAfterDelay` BossRoundData 분기 누락, EnemyStat.guard 직접쓰기 999 클램프 비대칭, 적 공격 시퀀스 주석(16-18-40) vs 실제({8,9,20}). (docs/systems/battle-integration.md에 기록)

---

## 핵심 결정/제약 (반드시 유지)
- **SampleScene 전용** — BattleTestScene/Controller 수정·셋업 안 함(공유 스크립트는 OK).
- **적 AI 항상 신규**: SampleScene 부트스트랩이 NewBattleController 생성 → IsNewBattleSystemActive 가드로 레거시 스킵.
- **온라인 학습**: 보상=휴리스틱+결과 혼합, 지속=세션 단위(디스크 영속 미채택), Q-가중치만 학습(FCM 고정).
- **카드 분류**: 키워드(효과) 기준 — f1 화상=burn효과 카드(불-색 폴백 X), f2 파편=땅+파편(원소 유지).
- 일부 레거시 파일 EUC-KR 잔재(주석) — 편집 시 UTF-8 유지.

## 핵심 참조
- 개요: `시스템_명세서.md` / 시스템별: `docs/systems/README.md` / 적AI 계산: `docs/systems/enemy-ai-pipeline.md`
- 기획서 추출본: `.git/gdd_0.6v_extracted.txt` / FCM-RBFN 소스: `C:\Users\dusdn\OneDrive\바탕 화면\FcmRbfnCode (1)\FcmRbfnCode\FCMRbfnoffline_0.2\`
- 메모리: `migration-newbattle-into-samplescene.md`, `gdd-0.6v-reflection.md`, `fcm-rbfn-ai-integration.md`
