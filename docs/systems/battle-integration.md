# 전투 연동 (Roundmanager / Enemy) 명세

> 신규 카드 전투 시스템(`Battle.NewBattleController`)이 본 게임 흐름(`Roundmanager`)과 적(`EnemyStat` / `EnemyController`)에 어떻게 배선되는지 정리한 명세서.
> 실제 코드 기준이며, 모든 동작은 `파일:line`으로 근거를 표기한다. 추측 없음.
>
> 대상 파일
> - `Assets/Script/Round/Roundmanager.cs`
> - `Assets/Script/Enemy/EnemyStat.cs`
> - `Assets/Script/Enemy/EnemyController.cs`
> - (참조) `Assets/Script/Enemy/EnemyData.cs`, `Assets/Script/Battle/Run/RunDeckState.cs`, `Assets/Script/Battle/NewBattleController.cs`, `Assets/Script/Player.cs`, `Assets/Script/Round/IRoundHandler.cs`

---

## 1. 개요 / 책임

라운드 진입/종료 시점에 신규 전투 시스템을 구동하고, 적의 게이지·공격·방해행동·화상·강화·회복을 본 게임 객체에 연결하며, 전투 종료 후 보상으로 분기하는 **연동(배선) 레이어**다.

- **라운드 진입/종료 구동**: 노드 진입 시 적을 스폰하고 `NewBattleController.StartBattle()`을 호출, 노드의 모든 적 소진 시 `EndRound()`에서 `EndBattle()` + 보상으로 분기. (`Roundmanager.cs:202`, `Roundmanager.cs:176`)
- **신규/레거시 분기 가드**: `IsNewBattleSystemActive()`(= `NewBattleController.Instance != null`)로 레거시 ElementSlot/Combo/HUD 자동 셋업, 레거시 `StartBattle/EndBattle`, 처치당 골드, starter 스킬 UI를 모두 건너뛴다. (`Roundmanager.cs:56`)
- **적 게이지 → 방해/공격 hook**: 플레이어 행동마다 `EnemyStat.ConsumeGaugeStep()`이 게이지를 올리고, 절반에서 `OnMidPattern`(방해행동), 최대에서 `OnGaugeFull`(공격)을 발화. (`EnemyStat.cs:117`)
- **화상/강화/회복 hook**: 적 공격 직전 화상 고정피해(`ApplyBurnBeforeAttack`), 방해행동 "강화"로 다음 공격 +50%(`BuffNextAttack`), 방해행동 "회복"으로 적 HP 30% 회복(`EnemyStat.Heal`). (`EnemyController.cs:228`, `EnemyController.cs:169`, `EnemyStat.cs:96`)
- **보상 분기**: 전투 종료 후 등급별 골드 일괄 지급(`GrantTieredCombatGold`) + 신규 시스템이면 카드 보상(`ShowCardReward`), 레거시면 스킬 보상 허브. (`Roundmanager.cs:284`)

---

## 2. 위치 / 타입

| 타입 | 경로 | namespace | 종류 | 비고 |
|---|---|---|---|---|
| `Roundmanager` | `Assets/Script/Round/Roundmanager.cs` | 전역(global) | `MonoBehaviour` | 씬에 1개 (배선 허브) (`Roundmanager.cs:6`) |
| `EnemyStat` | `Assets/Script/Enemy/EnemyStat.cs` | 전역(global) | `MonoBehaviour` | **싱글톤 아님 — 적 인스턴스마다 1개** (`EnemyStat.cs:5`) |
| `EnemyController` | `Assets/Script/Enemy/EnemyController.cs` | 전역(global) | `MonoBehaviour`, `IBattleUnit` | 적 인스턴스마다 1개 (`EnemyController.cs:5`) |
| `EnemyData` | `Assets/Script/Enemy/EnemyData.cs` | 전역(global) | `ScriptableObject` | 적 정의 에셋 (`EnemyData.cs:4`) |

- 세 클래스 모두 namespace 없는 전역 타입. 신규 전투 측은 `Battle` / `Battle.UI` / `Battle.AI` namespace에 있어 연동 코드에서 `Battle.NewBattleController` 식으로 정규화 참조한다.
- `EnemyStat`은 정적 `Instance`가 없다. `currentEnemy`(현재 활성 적)는 `Roundmanager`가 필드로 들고 있고(`Roundmanager.cs:22`), `NewBattleController`는 `FindFirstObjectByType<EnemyController>()`로 매 전투 시작 시 재참조한다(`NewBattleController.cs:248`).

---

## 3. 참조(의존) — 누구를 부르는가

### Roundmanager →
- **`Battle.NewBattleController`** (정적 `Instance`): 활성 여부 가드(`Roundmanager.cs:58`), `EndBattle()`(`Roundmanager.cs:181`), `SetCustomStartingDeck(...)` + `StartBattle()`(`Roundmanager.cs:195-196`).
- **`Battle.RunDeckState`**: `EnsureExists()` → `EnsureSeeded()` → `InstantiateForBattle()`로 런 덱 주입(`Roundmanager.cs:193-195`), 카드 보상 시 `AddCard(...)`(`Roundmanager.cs:334`).
- **`Battle.UI.CardRewardUI`** / **`Battle.UI.CardHandHUD`**: 카드 보상 표시. HUD에서 `CardPrefab`을 얻어 `CardRewardUI.EnsureExists().Present(...)`(`Roundmanager.cs:327-331`).
- **`MoneyManager`** (정적 `Instance`): 등급별 골드 `AddMoney(gold)`(`Roundmanager.cs:348`), 레거시 처치당 `OnEnemyKilled()`(`Roundmanager.cs:456`).
- **`GameStateController`** (정적 `Instance`): 노드 클리어 처리 `MarkNodeCleared(lastVisitedNodeIndex)` + `ShowMap()`(`Roundmanager.cs:368-371`).
- **`Player`**: `FindFirstObjectByType<Player>()`로 찾아 `UpdateUIForExternalSync()`(`Roundmanager.cs:33`), `ResetStatusForNewBattle()`(`Roundmanager.cs:209`), `Heal(int)`(`Roundmanager.cs:274`).
- **`EnemyStat` / `EnemyView` / `EnemyController` / `MonsterMidPattern`**: 스폰 시 `GetComponent`로 모두 취득, `stat.Initialize(...)`(`Roundmanager.cs:425`), `stat.OnDied += HandleEnemyDied`(`Roundmanager.cs:434`).
- **`DifficultyConfig`** (`[SerializeField]`): `Initialize` 인자로 적에 전달(`Roundmanager.cs:425`).
- 그 외 레거시: `ElementSlotSystem`, `CardSystem`, `ComboSystem`, `SkillDataParser`, `RewardHubUIController`, `CombatStageController` (신규 가드로 대부분 스킵).

### EnemyController →
- **`Battle.NewBattleController`** (정적 `Instance`): 방해행동 `TriggerDisruption()`(`EnemyController.cs:150`), 화상 유지 여부 `BurnPersistsOnEnemyTurn`(`EnemyController.cs:244-245`), 활성 가드(`EnemyController.cs:56`, `148`, `178`, `187`).
- **`EnemyStat`** (`GetComponent`, `Awake`): HP/공격/게이지/상태/방어 상태 보유체. 이벤트 구독(`EnemyController.cs:36-39`).
- **`EnemyView`** (`GetComponent`): 데미지/게이지/공격예고/방해 알림 표시(`EnemyController.cs:39`, `71`, `88`, `165`, `239`).
- **`Player`** (`FindFirstObjectByType`): 피격 `TakeDamage(damage)`(`EnemyController.cs:255`), `launcher` 상태 참조/소모(`EnemyController.cs:81-84`).
- **`MonsterMidPattern`** (`GetComponent`): FCM 폴백 방해행동, `OnGauge10/OnBattleEnd/Execute`(`EnemyController.cs:40`, `155`, `173`, `272`).

### EnemyStat →
- **`EnemyData`**: `maxHp/attackDamage/gaugeSpeed/baseAttackCount/actionGaugeMax/enemyName`을 읽어 런타임 수치로 전개(`EnemyStat.cs:55-77`).
- **`DifficultyConfig`**: `GetHpMultiplier(...)` / `GetAttackCount(...)`로 열·노드별 보정(`EnemyStat.cs:62`, `105`).
- **`EnemyController`** (`GetComponent`): 빙결/동결 상태 소모를 컨트롤러 경유로 갱신(`EnemyStat.cs:124-143`).

---

## 4. 피참조 — 누가 부르는가

- **라운드 진입** (`Roundmanager.StartRound`): GameState/맵 흐름이 `RoundData`를 넘겨 호출 → `EnsureElementCombatSystems()` 후 `ContinueStartRound`에서 `RoundData.CreateHandler().OnEnterRound(this)`로 핸들러에 위임(`Roundmanager.cs:117`, `172-173`).
- **핸들러(`IRoundHandler` 구현체)** → Roundmanager 진입/종료 API 호출 (`Assets/Script/Round/IRoundHandler.cs`):
  - `CombatRoundHandler.OnEnterRound` → `rm.StartCombat(Data)`, `OnExitRound` → `rm.ShowSkillReward()` (`IRoundHandler.cs:19`, `24`)
  - `EliteRoundHandler.OnEnterRound` → `rm.StartCombat(Data)`, `OnExitRound` → `rm.ShowSkillReward()` (`IRoundHandler.cs:39`, `44`)
  - `BossRoundHandler.OnEnterRound` → `rm.StartBoss(Data)`, `OnExitRound` → `rm.ShowSkillReward()` (`IRoundHandler.cs:58`, `63`)
  - `RestRoundHandler` 등 비전투 핸들러는 `HealPlayer`/`OpenShop`/`ReturnToMap` 경로 사용.
- **`EndRound`** → `currentRoundHandler.OnExitRound(this)`를 호출(`Roundmanager.cs:183`)하므로, **전투 종료 → `ShowSkillReward` 진입은 핸들러 경유**. (`EndBattle()`은 보상을 직접 트리거하지 않는다.)
- **적 사망 이벤트 구독자**:
  - `Roundmanager.HandleEnemyDied` ← `EnemyStat.OnDied` (스폰 시 구독, `Roundmanager.cs:434`)
  - `EnemyController.HandleDeath` ← `EnemyStat.OnDied` (`EnemyController.cs:36`)
- **`EnemyStat` 게이지/HP 이벤트 구독자**: `EnemyController`가 `OnMidPattern`/`OnGaugeFull`/`OnGaugeStepChanged`를 구독(`EnemyController.cs:37-39`); `EnemyView.UpdateActionGauge`가 `OnGaugeStepChanged`에 직접 연결됨(`EnemyController.cs:39`).
- **게이지 전진 트리거**: `EnemyController.OnPlayerAction()` → `stat.ConsumeGaugeStep()`. 플레이어가 슬롯/카드를 사용할 때 외부(전투 시스템)에서 호출(`EnemyController.cs:138-141`).

---

## 5. 공개 API

### Roundmanager (`Assets/Script/Round/Roundmanager.cs`)

| 멤버 | 시그니처 / line | 설명 |
|---|---|---|
| `StartRound` | `void StartRound(RoundData)` `:117` | 라운드 진입. 시스템 셋업 후 핸들러 위임. 신규 모드는 starter 스킬 UI 스킵. |
| `StartCombat` (일반) | `void StartCombat(CombatRoundData)` `:202` | 일반 전투. 적 스폰 → 신규면 `BeginNewBattleForNode()`. |
| `StartCombat` (정예) | `void StartCombat(EliteRoundData)` `:220` | 정예 전투. 위와 동일 흐름. |
| `StartBoss` | `void StartBoss(BossRoundData)` `:238` | 보스 전투. `SpawnEnemy(boss, …, NodeType.Boss)` 후 `BeginNewBattleForNode()`. |
| `EndRound` | `void EndRound()` `:176` | 신규면 `NewBattleController.EndBattle()`, 레거시면 `ElementSlotSystem.EndBattle()`. 핸들러 `OnExitRound` + `OnRoundClear` 발화. |
| `ShowSkillReward` | `void ShowSkillReward()` `:284` | 등급 골드 지급 후, 신규면 `ShowCardReward()`, 레거시면 보상 허브/스킬 UI. |
| `ReturnToMap` | `void ReturnToMap()` `:355` | 레거시면 `EndBattle()`. 노드 클리어 마킹 + `ShowMap()`. |
| `HealPlayer` | `void HealPlayer(float healPercent)` `:268` | `Player.maxHp * percent` 회복(최소 1) 후 맵 복귀. |
| `HandleEnemyDied` | `void HandleEnemyDied()` `:443` | `OnDied` 콜백. 구독 해지, (레거시만)처치 골드, 인덱스++ , 딜레이 다음 스폰. |
| `EnsurePlayerUiSync` | `void EnsurePlayerUiSync()` `:50` | 런타임 Player 보장 + 상태 패널 타깃 연결. |
| `BeginNewBattleForNode` *(신규, private)* | `void BeginNewBattleForNode()` `:188` | 런 덱 주입(`SetCustomStartingDeck`) 후 `StartBattle()`. |
| `ShowCardReward` *(신규, private)* | `void ShowCardReward()` `:323` | 카드 보상 3택 → 선택 ID를 `RunDeckState.AddCard` 후 맵 복귀. |
| `GrantTieredCombatGold` *(신규, private)* | `void GrantTieredCombatGold()` `:340` | 보스 200~250 / 정예 100~150 / 일반 30~50, 비전투는 미지급. |
| `IsNewBattleSystemActive` *(private)* | `bool IsNewBattleSystemActive()` `:56` | `NewBattleController.Instance != null`. 레거시 스킵 가드. |
| `OnRoundClear` | `event System.Action` `:23` | 라운드 클리어 알림. |

> 참고: `OpenShop`/`CloseShop`(`:255`/`:260`)도 공개되어 있으나 본 명세(전투/적) 범위 밖.

### EnemyStat (`Assets/Script/Enemy/EnemyStat.cs`)

| 멤버 | 시그니처 / line | 설명 |
|---|---|---|
| `Initialize` | `void Initialize(EnemyData, int columnIndex, NodeType, DifficultyConfig)` `:55` | 수치 전개. HP에 난이도 배수, 게이지 최대 `Clamp(actionGaugeMax,10,30)`, 공격계획 롤. |
| `TakeDamage` | `void TakeDamage(float)` `:79` | HP 차감, `OnHpChanged`, 사망 시 1회만 `OnDied`. |
| `Heal` | `void Heal(float)` `:96` | HP 회복(최대 클램프). 방해행동-회복용. |
| `ConsumeGaugeStep` | `void ConsumeGaugeStep()` `:117` | 게이지 1 전진. frost/freeze면 막고 스택 소모. 절반→`OnMidPattern`, 최대→리셋+`OnGaugeFull`. |
| `RollNewAttackPlan` | `void RollNewAttackPlan()` `:103` | 난이도 기반 공격 횟수 재산정 + `OnAttackCountChanged`. |
| `ReduceAttackCount` | `void ReduceAttackCount(int=1)` `:110` | 공격 횟수 감소(하한 0). |
| 이벤트 | `OnDied` `:38`, `OnMidPattern` `:40`, `OnGaugeFull` `:41`, `OnGaugeStepChanged(float)` `:42`, `OnHpChanged(float,float)` `:37`, `OnAttackCountChanged(int)` `:39` | HP/게이지/사망/공격횟수 알림. |
| 필드/프로퍼티 | `currentHp` `:8`, `maxHp` `:7`, `AttackDamage` `:9`, `GaugeSpeed` `:10`, `statusEffects` `:14`, `guard` `:13`, `IsAlive` `:11`, `GaugeStep` `:25`, `NodeType` `:33`, `IsBossOrElite` `:35`, `CurrentAttackCount` `:45`, `PlannedAttackCount` `:46`, `isNewlyFrozen` `:43` | 외부에서 직접 접근하는 상태값들. |
| 상수 | `GAUGE_MAX_STEPS = 20` `:21` | 게이지 기본/폴백 최대치(적별 10~30로 override). |

> `actionGaugeMax`는 `EnemyData` 필드(`EnemyData.cs:14`)이며, `Initialize`에서 `gaugeMaxSteps`(private)로 클램프되어 적용된다(`EnemyStat.cs:70`).

### EnemyController (`Assets/Script/Enemy/EnemyController.cs`)

| 멤버 | 시그니처 / line | 설명 |
|---|---|---|
| `HandleGaugeFull` *(private 핸들러)* | `void HandleGaugeFull()` `:171` | `OnGaugeFull` 수신. (신규)화상 선처리 → 공격 시퀀스/횟수 결정 → 멀티히트 → 공격계획 재롤 + 예고 갱신. |
| `HandleMidPattern` *(private 핸들러)* | `void HandleMidPattern()` `:143` | `OnMidPattern` 수신. 신규면 `TriggerDisruption()`, 아니면 `MonsterMidPattern`/`ElementSlotSystem` 폴백. 알림 표시. |
| `ApplyBurnBeforeAttack` *(private)* | `void ApplyBurnBeforeAttack()` `:228` | 공격 직전 화상 스택 고정피해 + (유지 옵션 아니면)스택 절반 감소. |
| `BuffNextAttack` | `void BuffNextAttack()` `:169` | 다음 적 공격 1회 +50% 플래그 set. 방해행동-강화에서 호출. |
| `SetStatus` | `void SetStatus(string, int)` `:119` | 상태 절대값 설정(0~999 클램프) + `OnStatusChanged`. |
| `AddStatus` | `void AddStatus(string, int)` `:99` | 상태 가감(0~999). freeze+wet 시 `isNewlyFrozen` set. |
| `AddGuard` | `void AddGuard(float)` `:133` | 방어도 가산, **0~999 클램프**. |
| `GetStatus` | `int GetStatus(string)` `:114` | 상태 스택 조회(없으면 0). |
| `OnPlayerAction` | `void OnPlayerAction()` `:138` | `stat.ConsumeGaugeStep()` 위임(플레이어 행동당 게이지 전진). |
| `TakeDamage` | `void TakeDamage(float, string="normal")` `:74` / `void TakeDamage(float)` `:93` | 적 피격. launcher 추가타 처리 후 `stat.TakeDamage` + 연출. |
| `GetAttackDamage` | `float GetAttackDamage()` `:128` | `stat.AttackDamage` 반환. |
| `OnStatusChanged` | `event Action<string,int>` `:98` | 상태이상 변경 알림(상태 패널 등). |
| 필드(인스펙터) | `newSystemAttackSequence = {8,9,20}` `:12`, `fallbackGaugeFullDamage = 10` `:8` | 신규 시스템 공격 시퀀스 / 레거시 폴백 피해. |

---

## 6. 내부 동작 / 데이터 흐름

### 6-1. 신규 전투 진입 (노드 시작)
1. 핸들러 `OnEnterRound` → `Roundmanager.StartCombat(data)` (보스는 `StartBoss`). (`IRoundHandler.cs:19`)
2. `EnsureElementCombatSystems()` 호출 — 단, **신규 활성 시 레거시 ElementSlot/HUD 자동생성·`CardSystem` 비활성화를 즉시 return으로 차단**(`Roundmanager.cs:66-67`). 레거시 `ElementSlotSystem.StartBattle()`도 가드로 스킵(`Roundmanager.cs:205-206`).
3. `Player.ResetStatusForNewBattle()` 호출(`Roundmanager.cs:209`), `currentEnemyIndex=0`, **첫 적 스폰** `SpawnNextEnemy(...)`(`Roundmanager.cs:211-212`).
4. 신규 활성이면 마지막에 `BeginNewBattleForNode()`(`Roundmanager.cs:214`):
   - `RunDeckState.EnsureExists()` → `EnsureSeeded()`(없으면 기본 8장 시드, `RunDeckState.cs:97`)
   - `nb.SetCustomStartingDeck(runDeck.InstantiateForBattle())` — 전투마다 **새 `CardInstance` 리스트** 주입(런타임 오염 방지)(`RunDeckState.cs:107`)
   - `nb.StartBattle()` — 신규 컨트롤러가 `Player`/`EnemyController`/`EnemyStat`를 `FindFirstObjectByType`로 재참조하고 컨텍스트/이벤트 바인딩(`NewBattleController.cs:245-323`)

### 6-2. 노드 종료 (적 소진)
1. 각 적 사망 → `EnemyStat.OnDied` → `Roundmanager.HandleEnemyDied`(`Roundmanager.cs:443`): 구독 해지, **신규면 처치 골드 OFF**(`Roundmanager.cs:454`), `currentEnemyIndex++`, `SpawnNextAfterDelay()`(기본 1.5초)(`Roundmanager.cs:462`).
2. 코루틴이 `SpawnNextEnemy`를 재호출(`Roundmanager.cs:469-472`). **인덱스가 적 수 이상이면** `EndRound()`(`Roundmanager.cs:378-382`).
3. `EndRound()`: 신규면 `NewBattleController.EndBattle()`(레거시면 `ElementSlotSystem.EndBattle()`)으로 전투 상태/연출 정리(`Roundmanager.cs:178-181`), `currentRoundHandler.OnExitRound(this)` → `ShowSkillReward()`(`Roundmanager.cs:183`, `IRoundHandler.cs:24`).
4. `ShowSkillReward()`: `GrantTieredCombatGold()` 후 신규면 `ShowCardReward()`(`Roundmanager.cs:287-293`).
5. `ShowCardReward()`: `CardHandHUD.CardPrefab`을 얻어 `CardRewardUI.Present(...)`, 선택 ID>0이면 `RunDeckState.AddCard(id)` 후 `ReturnToMap()`(`Roundmanager.cs:323-337`).

### 6-3. 적 행동 게이지 (per-enemy 10~30)
- 게이지 최대 = `Clamp(EnemyData.actionGaugeMax>0 ? actionGaugeMax : 20, 10, 30)`, `Initialize`에서 설정(`EnemyStat.cs:70`).
- 플레이어 행동마다 `EnemyController.OnPlayerAction()` → `ConsumeGaugeStep()`(`EnemyController.cs:138-141`).
- **frost(빙결)**: 스택>0이면 게이지 상승을 막고 스택 1 소모 후 return(`EnemyStat.cs:122-128`).
- **freeze(동결)**: `isNewlyFrozen`이면 그 호출은 소모만, 아니면 freeze -1 후 return(상승 차단)(`EnemyStat.cs:130-146`).
- `gaugeStep++` 후 `OnGaugeStepChanged(step/max)` 발화(`EnemyStat.cs:148-149`).
- **절반(`gaugeMaxSteps/2`) 도달**: 사이클당 1회 `OnMidPattern`(방해행동)(`EnemyStat.cs:152-156`).
- **최대 도달**: `gaugeStep=0`, midPattern 플래그 리셋, `OnGaugeStepChanged(0)` + `OnGaugeFull`(공격)(`EnemyStat.cs:159-165`). **초과분은 다음 호출에서 자연 이월**(리셋 후 다음 step부터 다시 누적).

### 6-4. 화상 피격 전 처리 (신규 전용)
- `HandleGaugeFull` 진입 직후 신규 활성이면 `ApplyBurnBeforeAttack()` 호출, 이때 화상으로 적이 죽으면 `return`(공격 안 함)(`EnemyController.cs:177-182`).
- 동작: `burn` 스택만큼 `stat.TakeDamage(burn)` 고정피해 → 데미지 표시(화상색)+알림(`EnemyController.cs:234-241`) → **잔여 화상**: `BurnPersistsOnEnemyTurn`이면 유지, 아니면 절반(`burn/2`)으로 감소 + `OnStatusChanged`(`EnemyController.cs:244-249`).

### 6-5. 적 공격 (게이지 풀)
- 신규 + `newSystemAttackSequence` 유효: `damagePerHit = seq[idx % len]`, `hitCount = 1`, 인덱스++ (PDF: 8→9→20 순환)(`EnemyController.cs:187-194`).
- 레거시: `damagePerHit = Round(stat.AttackDamage)`(0이하면 `fallbackGaugeFullDamage=10`), `hitCount = stat.CurrentAttackCount`(`EnemyController.cs:196-200`).
- **강화 플래그** set 시: `damagePerHit = Ceil(×1.5)`, 플래그 해제, "강화!" 알림(`EnemyController.cs:203-209`).
- `ExecuteMultiHit(hitCount, damagePerHit)` 코루틴 — 0.15초 간격으로 `player.TakeDamage(damage)` 반복(`EnemyController.cs:214`, `251-267`).
- 공격 후 `stat.RollNewAttackPlan()` + `UpdateAttackPreviewForNewSystem()`로 다음 공격 예고 갱신(`EnemyController.cs:217-220`).

### 6-6. 방해행동 강화 (다음 공격 +50%)
- 방해행동(`NewBattleController.TriggerDisruption`)의 case 3 "강화"가 `_enemy.BuffNextAttack()` 호출(`NewBattleController.cs:1251-1255`) → `_nextAttackBuffed=true`(`EnemyController.cs:169`) → 다음 `HandleGaugeFull`에서 1회 +50% 적용 후 소진.

### 6-7. 방해행동 회복 (적 HP 30%)
- `TriggerDisruption` case 4 "회복": `_recoverCooldown=2`, `_enemyStat.Heal(maxHp*0.3f)`(`NewBattleController.cs:1257-1266`). 적 HP 회복은 `EnemyStat.Heal`이 최대치 클램프 처리(`EnemyStat.cs:96-101`).

### 6-8. 티어 골드 / 레거시 스킵
- 신규: 처치당 골드 OFF(`Roundmanager.cs:454`), 전투 종료 시 등급별 일괄 지급(`GrantTieredCombatGold`, `Roundmanager.cs:340-350`).
- `IsNewBattleSystemActive()` 가드가 적용되는 지점: 레거시 시스템 자동 셋업(`:66`), starter 스킬 UI(`:122`), 레거시 `StartBattle`(`:205/223/241`)·`EndBattle`(`:178/357`), 처치 골드(`:454`), 보상 분기(`:290`).

---

## 7. 데이터 / 이벤트

### EnemyData (`Assets/Script/Enemy/EnemyData.cs`)
| 필드 | 기본값 | line | 용도 |
|---|---|---|---|
| `enemyName` | — | `:6` | 로그/표시 |
| `enemySprite` | — | `:7` | `EnemyView.SetSprite` |
| `maxHp` | `1000f` | `:9` | `Initialize`에서 난이도 배수 곱 |
| `attackDamage` | `10f` | `:10` | `AttackDamage`(레거시 공격) |
| `gaugeSpeed` | `10f` | `:11` | `GaugeSpeed` |
| `baseAttackCount` | `3` | `:12` | 난이도 공격 횟수 기준 |
| `actionGaugeMax` | `20` (10~30) | `:14` | 게이지 최대치(`Clamp(…,10,30)`) |

### EnemyStat 이벤트 (`Assets/Script/Enemy/EnemyStat.cs`)
| 이벤트 | 시그니처 | line | 발화 시점 |
|---|---|---|---|
| `OnHpChanged` | `Action<float,float>` (cur, max) | `:37` | `Initialize`/`TakeDamage`/`Heal` |
| `OnDied` | `Action` | `:38` | HP≤0 최초 1회 |
| `OnAttackCountChanged` | `Action<int>` | `:39` | `RollNewAttackPlan`/`ReduceAttackCount` |
| `OnMidPattern` | `Action` | `:40` | 게이지 절반 도달(사이클당 1회) |
| `OnGaugeFull` | `Action` | `:41` | 게이지 최대 도달 |
| `OnGaugeStepChanged` | `Action<float>` (0~1) | `:42` | 게이지 step 변동 시 |

### EnemyController 공격 시퀀스
- `newSystemAttackSequence = { 8, 9, 20 }` (인스펙터 노출, 코드 주석은 "PDF: 16-18-40" 표기지만 **실제 기본값은 8/9/20**)(`EnemyController.cs:11-12`). `_newSystemAttackIndex`로 순환(`EnemyController.cs:190-192`).

### 상태 키 (`statusEffects` Dictionary)
- `Awake`에서 `burn/wet/freeze/frost` 0으로 초기화(`EnemyStat.cs:50-53`). `AddStatus`/`SetStatus`는 **등록된 키만** 처리(`EnemyController.cs:101`, `120`), 0~999 클램프.

---

## 8. 주의 / 엣지케이스

1. **`StartBattle`/`EndBattle` 노드당 1:1** — 신규 전투 세션은 노드 진입 시 `BeginNewBattleForNode`에서 1회 시작(`Roundmanager.cs:214`), 노드의 모든 적 소진 후 `EndRound`에서 1회 종료(`Roundmanager.cs:181`). 다중 적 노드라도 적 교체마다 재시작하지 않는다(전투 세션은 노드 단위).

2. **적 스폰 동기 / Update 백업 재참조** — 스폰은 `SpawnEnemy`에서 동기 `Instantiate`(`Roundmanager.cs:396`). 단 `NewBattleController.Instance`가 스폰 직후 아직 없을 수 있어, `EnemyController.Update`가 `_attackPreviewInitialized` 플래그로 **한 번만 지연 예고 갱신**(`EnemyController.cs:55-61`). `HandleGaugeFull`에서 `player==null`이면 재탐색(`EnemyController.cs:174-175`).

3. **신규 시스템 처치당 골드 OFF (티어 일괄)** — 신규 활성 시 `MoneyManager.OnEnemyKilled()`를 호출하지 않고(`Roundmanager.cs:454`), 전투 종료 시 등급별 골드만 지급. 레거시와 골드 경로가 상호배타적이므로 둘을 동시에 켜면 안 됨.

4. **다중 적 노드** — `currentEnemy` 구독은 스폰 시 이전 적 `OnDied` 해지 후 교체(`Roundmanager.cs:428-435`). `currentEnemyIndex`로 순차 진행하며, `SpawnNextAfterDelay`는 `CombatRoundData`/`EliteRoundData`만 분기(보스 데이터는 여기서 처리 안 함, 아래 5번 참조).

5. **보스 `SpawnNextAfterDelay` 분기 주의** — `SpawnNextAfterDelay`는 `CombatRoundData`/`EliteRoundData`만 매칭(`Roundmanager.cs:469-472`). **`BossRoundData`는 매칭 분기가 없다.** 보스는 `StartBoss`가 `SpawnEnemy`로 단일 보스만 스폰하므로(`Roundmanager.cs:247`) 보스 사망 시 `HandleEnemyDied`가 `currentEnemyIndex++` 후 코루틴을 돌려도 재스폰되지 않고, 종료 처리는 핸들러 `OnExitRound`→`ShowSkillReward` 경로에 의존한다. (단일 보스 가정에서만 안전 — 다중 보스 데이터를 넣으면 두 번째 이후가 스폰되지 않음.)

6. **`Initialize` 호출 순서 의존** — `EnemyController.Awake`가 `stat`/`view`를 잡고, `Start`에서 `OnGaugeStepChanged += view.UpdateActionGauge` 등 구독(`EnemyController.cs:36-39`). `Roundmanager.SpawnEnemy`는 `Initialize`를 호출(`Roundmanager.cs:425`)하지만 컨트롤러 구독은 컴포넌트 `Start` 타이밍이라, 첫 `Initialize`의 `OnHpChanged`는 컨트롤러 구독 전일 수 있다(스폰 직후 1프레임). 이후 게이지/HP 이벤트는 정상 연결.

7. **화상 사망 시 공격 생략** — 신규에서 화상 선처리로 적이 사망하면 `HandleGaugeFull`이 `return`(`EnemyController.cs:181`). 단 `HandleDeath`가 `Destroy(gameObject)`(`EnemyController.cs:276`)로 즉시 파괴하므로, 화상 사망 → `OnDied` → `HandleEnemyDied`(Roundmanager) 경로로 다음 적/종료 처리가 이어진다.

8. **`EndBattle` ≠ 보상** — `NewBattleController.EndBattle()`은 전투 상태 정리와 **진행 중 카드 연출 취소**(`CancelCardUsePresentations`)만 수행(`NewBattleController.cs:325-340`). 보상은 핸들러 `OnExitRound`→`ShowSkillReward`에서 별도 트리거. `EndBattle`이 연출을 취소하는 이유는 보상 화면과 지연 카드효과가 겹치지 않게 하기 위함.

9. **`guard` 클램프 비대칭** — `EnemyStat.guard`는 필드로 직접 노출(하한/상한 없음, `:13`)이지만, 외부 가산 경로 `EnemyController.AddGuard`는 0~999로 클램프(`EnemyController.cs:135`). 즉 방어도 상한은 컨트롤러 경유 시에만 보장.

10. **레거시 폴백 방해행동** — 신규 비활성 시 `HandleMidPattern`은 `MonsterMidPattern.Execute()` 또는 `ElementSlotSystem.TriggerDisruptionPattern()`으로 폴백(`EnemyController.cs:153-160`). 신규 활성 시에는 항상 `NewBattleController.TriggerDisruption()`(AI/랜덤)로 분기.
