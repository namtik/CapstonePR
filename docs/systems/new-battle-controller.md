# 전투 코어 (NewBattleController) 명세

> 대상 파일: `Assets/Script/Battle/NewBattleController.cs` (약 1465줄)
> 작성 기준: 실제 코드 기준(추측 없음). 의존 시그니처는 각 소스에서 확인.

---

## 1. 개요 / 책임

새 카드 배틀 시스템의 **단일 진입점/오케스트레이터**. 한 전투의 전 과정을 조율한다.

담당 영역:
- **덱/드로우**: `CardDeckSystem`(`_deck`)을 소유하고 `Draw`/`D키 드로우`를 구동 (`NewBattleController.cs:84`, `608`)
- **카드 사용**: 사용 요청 → 중앙 연출 → 효과 실행(`OnUseCardRequested`/`ResolveCardUse`, `632`/`705`)
- **각성(Awaken)**: 속성 카드 10장 사용 시 발동, 지속시간 동안 콤보 입력 누적, 종료 시 콤보 일괄 발동 (`918`~`1073`)
- **콤보 큐**: 슬라이딩 윈도우 3장 매칭 → 큐 적재 → 각성 종료 시 일괄 발동 (`_queuedComboSkills`, `1079`~`1154`)
- **방해(Disruption)**: 적 행동게이지 중간 단계 도달 시 6종 방해행동 실행 (`TriggerDisruption`, `1193`)
- **저주(Curse)**: 한 속성 저주 부여/적용/해제 (`ApplyCurseOnCardUse`/`ClearAllCurses`, `1305`/`477`)
- **연쇄(Chain)**: `_ctx.chainCount`를 플레이어 상태 패널에 동기화 (`SyncChainStatus`, `221`)
- **게이지**: 카드 게이지를 `EnemyStat.ConsumeGaugeStep`으로 누적 (`AccrueEnemyGauge`, `892`)
- **이펙트**: 카드/각성 데미지 스프라이트 연출, 피격 플래시, 화면 셰이크 (`effectOverlay`/`damageOverlay`)

---

## 2. 위치 / 타입

| 항목 | 값 |
|---|---|
| 경로 | `Assets/Script/Battle/NewBattleController.cs` |
| namespace | `Battle` (`NewBattleController.cs:10`) |
| 기반 타입 | `MonoBehaviour` (`:19`) |
| 싱글톤 | **있음** — `public static NewBattleController Instance { get; private set; }` (`:31`). `Awake`에서 설정, 중복 인스턴스는 `Destroy`(`:159`~`162`), `OnDestroy`에서 해제(`:181`) |

---

## 3. 참조(의존)

이 클래스가 호출/사용하는 외부:

| 의존 | 무엇 위해 |
|---|---|
| `CardDeckSystem` (`_deck`) | 손패/뽑을·버린·소멸 더미 관리, 드로우, 사용 후 카드 배치, 더미 스냅샷/복원 |
| `CardEffectResolver` (`_resolver`) + `CardEffectContext` (`_ctx`) | 카드 단일 효과 처리(`Resolve`)와 모든 런타임 상태/플래그·카운터 보관(`_ctx`) |
| `ComboEffectResolver` (`_comboResolver`) + `ComboResolveContext` (`_comboCtx`) | 데이터 드리븐(DB) 콤보의 다중 효과 실행 |
| `ComboSkillDatabase` | `BuildOwnedCombos(refIds)`로 보유 콤보 로드(전투 시작/런타임 갱신) (`:282`) |
| `ComboSkillDef` | 보유 콤보 정의 — `Matches`/`ComboString`/`effect`/`fromDatabase`/`refComboId` |
| `RunDeckState` | 영구 공격력 복원(`PermanentAttackPower`), 적 AI 런 프로파일 기록(`RecordCardUse`/`RecordAwakenActivation`) (`:258`,`473`,`944`) |
| `RelicManager` | 콤보 보너스 시간 증가/각성 게이지 회복 유물 효과 조회(`HasEffect`) (`:656`,`1045`) |
| `Battle.AI.EnemyDisruptionAI` / `EnemyAiModel` | 방해행동 선택(FCM-RBFN 추론, `Decide`) 및 카드 분류(`Classify`) (`:1202`,`473`) |
| `Battle.AI.DisruptionLogger` | 방해행동 결정 CSV 로깅(오프라인 재학습용) (`:1207`) |
| `CardHandHUD` (`handHud`) | 손패 UI 바인딩, 사용 콜백, 카드 사용 연출, 선택/픽커 모드, 콤보 슬롯/스킬 UI, 각성 게이지 표시 |
| `CardEffectOverlay` (`effectOverlay`) | 카드/각성 데미지 스프라이트 시트 애니메이션 재생 |
| `PlayerDamageOverlay` (`damageOverlay`) | 화면 셰이크(각성 데미지 강조) |
| `Player` (`_player`) | 피해/회복/방어도/상태표시 — `TakeDamage`/`Heal`/`AddGuard`/`SetStatus`, 이벤트 구독 |
| `EnemyController` (`_enemy`) | 적 피해/상태/강화 — `TakeDamage`/`AddStatus`/`SetStatus`/`BuffNextAttack` |
| `EnemyStat` (`_enemyStat`) | 게이지 누적(`ConsumeGaugeStep`), 회복(`Heal`), 빙결(`statusEffects["frost"]`), `OnGaugeFull` 이벤트 |
| `CardDatabase` | 기본 덱/파편/무속성 더미 인스턴스 생성(`BuildDefaultPrototypeDeck`, `CreateFragmentInstance`, `CreateNeutralFillerInstance` 등) |

---

## 4. 피참조 (누가 호출하나)

| 호출자 | 호출 내용 |
|---|---|
| `Roundmanager.BeginNewBattleForNode` | 노드 진입 시 `SetCustomStartingDeck(runDeck.InstantiateForBattle())` → `StartBattle()` (`Roundmanager.cs:195`~`196`) |
| `Roundmanager`(노드 종료) | `Battle.NewBattleController.Instance?.EndBattle()` (`Roundmanager.cs:181`) |
| `Roundmanager.IsNewBattleSystemActive` | `Instance != null`로 신규 시스템 활성 여부 판정 → 구 ElementSlot/Combo 셋업 스킵 (`Roundmanager.cs:58`) |
| `EnemyController.HandleMidPattern` | 방해 단계 도달 시 `Instance.TriggerDisruption()` 호출, 반환 메시지를 패턴 메시지로 사용 (`EnemyController.cs:148`~`150`) |
| `EnemyController`(적 턴 데미지) | `Instance.BurnPersistsOnEnemyTurn` 조회 — 불126 화상 유지 여부 (`EnemyController.cs:244`~`245`) |
| `NewBattleSystemBootstrap.EnsureNewBattleController` | `Instance`가 없으면 GameObject 자동 생성(`AddComponent`로 Awake에서 Instance 설정). StartBattle/EndBattle은 호출하지 않음 (`NewBattleSystemBootstrap.cs:62`~`73`) |
| `CardHandHUD` 콜백 | `UseCardCallback`=`OnUseCardRequested`, `SelectionClosedCallback`=`TryActivatePendingAwaken` (전투 시작 시 등록, `:291`~`292`) |
| `Player`/`EnemyStat` 이벤트 | `OnPlayerHit`/`OnBlockConsumedByAttack`/`OnGaugeFull` 구독 콜백으로 역호출(아래 7절) |
| `NewCardView`(카드 뷰) | `IsElementCursed(element)`로 카드 저주 표시 여부 조회 (`:1299`) |
| `BattleTestController` | (레거시/테스트 씬 전용) `SetCustomStartingDeck` 주입 — **현 작업 범위 외, 유지보수 중단** |

---

## 5. 공개 API

| 시그니처 | 설명 | 호출 시점 / 주의 |
|---|---|---|
| `static NewBattleController Instance { get; }` | 싱글톤 인스턴스 | `Awake`에서 설정, `OnDestroy`에서 해제 |
| `void StartBattle()` | 전투 진입 — 참조 수집, 컨텍스트 초기화, 덱·HUD·콤보 셋업, 시작 5장 드로우 | `Roundmanager.BeginNewBattleForNode`. `SetCustomStartingDeck` 이후 호출해야 사용자 덱 반영 |
| `void EndBattle()` | 전투 종료 — `_inBattle=false`, 각성 종료, 덱 종료, **진행 중 연출/지연효과 취소**, HUD 콜백 해제 | `Roundmanager`(노드 종료). 보상 화면과 효과 겹침 방지 |
| `void SetCustomStartingDeck(List<CardInstance> deck)` | 시작 덱 주입(`_customStartingDeck`) | `StartBattle` 전에 호출. 없으면 `CardDatabase.BuildDefaultPrototypeDeck()` 사용 (`:276`) |
| `string TriggerDisruption()` | 방해행동 1종(0~5) 선택·실행, UI용 메시지 반환 | `EnemyController.HandleMidPattern`. AI(`EnemyAiModel.Loaded`)면 추론, 아니면 랜덤 |
| `void NotifyEnemyAttackFinished()` | 다음 카드에 `FIRST_CARD_AFTER_ENEMY_ATTACK` 적용 플래그 set | 적 공격 종료 시 외부 호출용 (`:487`). 참고: 내부적으로 `OnGaugeFull`도 동일 플래그 set |
| `void RefreshOwnedCombosRuntime()` | 인스펙터 `ownedComboRefIds`를 즉시 적용(런타임 콤보 갱신) | `[ContextMenu]`. `useComboDatabase` OFF면 경고 후 무시, **각성 중이면 보류**(다음 각성부터) (`:348`) |
| `void AddOwnedCombo(int refComboId)` | 콤보(1000~1019) 1개 추가 후 즉시 적용 | 콤보 보상 UI 대체용. 빈 리스트(=전체 보유)에서 첫 추가 시 '지정 집합'으로 전환됨(주의) (`:369`) |
| `void ClearAllCurses()` | 플레이어의 모든 저주 해제 + HUD 갱신 | 물15(214) 효과 등 (`:477`) |
| `bool IsElementCursed(CardElement e)` | 해당 속성이 현재 저주 중인지 | `NewCardView` 표시용 (`:1299`) |
| `bool IsAwakenActive` | 각성 활성 여부(`_awakenActive`) | 읽기 전용 프로퍼티 (`:141`) |
| `CardDeckSystem Deck` | 덱 시스템 노출 | 읽기 전용 (`:142`) |
| `CardInstance LastResolvedCard` | 마지막으로 효과 처리된 카드(211 재사용 대상) | 읽기 전용 (`:143`) |
| `CardElement? CursedElement` | 현재 저주된 속성(없으면 null) | 읽기 전용 (`:144`) |
| `bool BurnPersistsOnEnemyTurn` | 적 행동 시 화상이 줄지 않는지(`_ctx.burnPersistsOnEnemyTurn`) | `EnemyController`가 조회 (`:146`) |
| `int EffectiveAwakenInput` | 각성 발동에 필요한 실제 속성 카드 수 = `max(1, 10 + _ctx.awakenGaugeMaxDelta)` | 바람326 등 보정 반영 (`:148`) |
| `IReadOnlyList<ComboSkillDef> OwnedComboSkills` | 보유 콤보 목록 | 읽기 전용 (`:43`) |

상수: `AWAKEN_ACTIVATION_INPUT=10`, `AWAKEN_DURATION_SECONDS=10f`, `COMBO_BONUS_SECONDS=0.1f`, `COMBO_REUSE_INPUT=5`, `START_DRAW=5`, `CURSE_DURATION_USES=2`, `CURSE_PLAYER_DAMAGE=6` (`:22`~`29`, `:129`~`130`).

> 주의: 주석/로그 일부는 콤보 보너스를 "+1s"로 표기하나, 실제 상수 `COMBO_BONUS_SECONDS`는 **0.1f**다 (`:26`, 적용부 `:659`).

---

## 6. 내부 동작 / 데이터 흐름

### 6-1. 전투 진입 (`StartBattle`, `:245`)
1. `Player`/`EnemyController`/`EnemyStat`를 `FindFirstObjectByType`로 수집, `_ctx`에 주입.
2. `ResetContextFlags()`로 `_ctx`의 모든 효과 플래그/카운터 초기화 (`:387`~`456`).
3. `RunDeckState.Instance.PermanentAttackPower`를 `_ctx.attackPowerBonus`에 가산(런 누적 공격력 복원).
4. 이벤트 구독: `Player.OnPlayerHit`, `Player.OnBlockConsumedByAttack`, `EnemyStat.OnGaugeFull` (중복 해제 후 등록).
5. 덱 시작: `_deck.StartBattle(_customStartingDeck ?? BuildDefaultPrototypeDeck())`.
6. `useComboDatabase`면 `ownedComboSkills = ComboSkillDatabase.BuildOwnedCombos(ownedComboRefIds)`.
7. HUD 바인딩: `handHud.Bind(_deck)`, `UseCardCallback`/`SelectionClosedCallback` 등록, `SetAwakenMode(false)`.
8. 오버레이/EventSystem 자동 생성(없으면), 각성 게이지 앵커를 `Player.hpBar` 아래 배치.
9. 상태 플래그 리셋, `_deck.Draw(START_DRAW)`.

### 6-2. 전투 종료 (`EndBattle`, `:325`)
`_inBattle=false`, `_awakenActive/_awakenPending=false`, `_deck.EndBattle()`, **`handHud.CancelCardUsePresentations()`로 진행 중 연출/지연효과 취소**, HUD 콜백 null화.

### 6-3. 카드 사용 (`OnUseCardRequested` → 연출 → `ResolveCardUse`)
- **요청 진입** `OnUseCardRequested` (`:632`): `_inBattle`/card null 검사.
  - **각성 중이면**: 효과를 무시하고 `MoveAfterUse` + `Draw(1)`로 치환. `comboSlot` 카드면 `AddToComboSlot` → `TickComboCooldowns` → `TryActivateCombo`. 매칭 시 잔여시간/최대치 +`COMBO_BONUS_SECONDS`(유물 시 +0.5s). 연출은 **비차단(interruptable)**으로 연타 허용. 즉시 `return true` (`:638`~`681`).
  - **일반이면**: `_cardPresenting`이면 거부. `_deck.PullFromHand(card)`로 즉시 패에서 제거 + `_cardPresenting=true`. `handHud.PlayCardUsePresentation(card, 콜백)` — **연출 종료 콜백에서 `ResolveCardUse(card)` 실행 후 `_cardPresenting=false`** (HUD 없으면 즉시 실행) (`:685`~`698`).
- **효과 실행** `ResolveCardUse` (`:705`):
  1. `_inBattle` 재확인(종료 후 지연콜백 방어), `ApplyCurseOnCardUse`.
  2. 카드 사용 전 적 빙결량 기록(이 카드가 부여한 빙결이 자기 게이지 상승을 막지 않도록) (`:714`).
  3. `_resolver.Resolve(card)` → `ResolveResult`. `card.selfUseCount++`.
  4. 사용 후 배치: 바람314는 패 복귀, 그 외 `PlaceCardAfterUse(exile, keepOnField)` + 비소멸 시 `NotifyCardMovedToDiscard`.
  5. `effectOverlay.Play`, 소멸 시 `NotifyExile`, 물12(211) `recastLastCard`면 `_lastResolvedCard` 재Resolve.
  6. `ResolveResult` 플래그별 선택/픽커 모드 진입(소멸/버리기/복사/뽑을더미·버린더미 이동/파편풀) — 모두 `DeferredSelection`/`DeferredPicker`(1프레임 지연).
  7. 게이지 비용 산정(`card.Gauge`, 바람11/바람20 무료 보정), 빙결 임시 되돌림→`AccrueEnemyGauge`→복원, `skipNextGaugeCost`면 카운트.
  8. `UpdateUsageStats`(공격/파편/불 카운터 + RunDeckState 기록), `comboSlot`이면 `AccumulateAwakenInput`.
  9. `_awakenPending`이면 `DeferredPendingAwakenCheck`(2프레임 후 발동 시도).
  10. `_lastResolvedCard` 갱신(211 제외), `previousCardType` 등 후처리, `justDrawn` 만료.

### 6-4. 각성 (Awaken)
- **누적** `AccumulateAwakenInput` (`:918`): 비각성 시 `_elementInputCount++`, `>= EffectiveAwakenInput`이면 즉시 발동하지 않고 `_awakenPending=true`.
- **발동 시도** `TryActivatePendingAwaken` (`:933`): 선택/픽커 모드 진행 중이면 대기, 아니면 `ActivateAwaken`. (카드 효과의 선택이 끝난 뒤 발동하도록 보류 설계)
- **발동** `ActivateAwaken` (`:941`): `_awakenActive=true`, `RecordAwakenActivation`, 잔여/최대 시간=10s, **패/뽑을·버린 더미 스냅샷 저장**, `ExtractAllNeutralCards`로 무속성/파편 임시 격리, `Draw(HandLimit)`로 패 보충, 콤보 입력/쿨다운/큐/히스토리 초기화, `handHud.SetAwakenMode(true)`.
- **콤보 매칭** `TryActivateCombo` (`:1099`): 슬롯 3장 이상 + 쿨다운 0인 콤보 중 `Matches` 첫 매칭 → **큐 적재(즉시 발동 X)** + 해당 콤보에 `COMBO_REUSE_INPUT(5)` 쿨다운 부여. 슬라이딩 윈도우는 유지.
- **종료** `EndAwaken` (`:984`): `RestorePiles`로 각성 직전 상태 복원 → 큐의 콤보 **일괄 발동**(`ActivateComboSkill`). DB 콤보는 `ComboEffectResolver`, 레거시는 effect 스위치(Damage/Burn/Guard/Heal/Draw). Damage 콤보는 `ComputeAwakenDamageOffset`로 이펙트 분산 + 수에 비례한 셰이크(상한 `awakenShakeIntensityMax`). 유물 `AwakenGaugeRecoverPerCombo`면 `_elementInputCount`를 콤보 수만큼 회복(상한 `EffectiveAwakenInput-1`). 마지막에 콤보 슬롯/쿨다운/히스토리 초기화 + `SetAwakenMode(false)`.
- **타이머** `TickAwakenTimer`(`:230`): `Update`마다 `_awakenTimeRemaining -= deltaTime`, 0 이하면 `EndAwaken`. (콤보는 쿨다운으로 재사용 가능 → "전부 소진" 종료 없음, 시간 종료로만 끝남)

### 6-5. 적 게이지 `AccrueEnemyGauge` (`:892`)
- **각성 중이면 증가 안 함**(`return`).
- 바람14(313) `_skipNextGaugeCount`>0면 1 감소 후 스킵.
- `amount`만큼 `_enemyStat.ConsumeGaugeStep()` 호출 + 패 전 카드 `gaugeSinceDrawn++`.
- 호출처: 일반 카드 사용 게이지 비용(`ResolveCardUse`), D키 드로우(`TryDDraw`, +1).

### 6-6. 방해 `TriggerDisruption` (`:1193`) — 6종
선택: `useAiDisruption && EnemyAiModel.Loaded`면 `EnemyDisruptionAI.Decide(...)`(예외 시 랜덤 폴백), 아니면 `RandomFallbackPick`(0~3 항상, 회복4는 쿨다운 가용 시, 버리기5는 패≥2). 회복(4)이 아니면 `_recoverCooldown`을 1 감소.

| pick | 행동 | 효과 |
|---|---|---|
| 0 | 저주 | 무작위 1속성에 `CURSE_DURATION_USES(2)`회 지속 저주 |
| 1 | 탈진 | 무속성 더미 카드(500)를 뽑을 더미에 셔플 삽입 |
| 2 | 흡수 | 각성 게이지(`_elementInputCount`) -2 |
| 3 | 강화 | `_enemy.BuffNextAttack()`(다음 공격 +50%, 1회) |
| 4 | 회복 | 적 HP `maxHp*0.3` 회복, `_recoverCooldown=2`(다른 방해 2회 후 재사용) |
| 5 | 버리기 | 플레이어가 패에서 `min(2, HandCount)`장 직접 선택해 버림(`EnemyForcedDiscardRoutine`) |

### 6-7. 저주 적용 `ApplyCurseOnCardUse` (`:1305`)
저주 속성과 카드 속성 일치 시 `Player.TakeDamage(CURSE_PLAYER_DAMAGE=6)`. 카드 사용 2회 동안 지속(속성 불문 카운트 감소), 0이면 해제.

### 6-8. 주요 상태 필드
| 필드 | 의미 |
|---|---|
| `_inBattle` | 전투 진행 중 — Update/입력/효과 가드 |
| `_cardPresenting` | 일반 카드 중앙 연출 진행 중(중첩/재진입 방지) |
| `_elementInputCount` | 각성 발동용 누적 속성 카드 수(흡수/유물로 변동) |
| `_awakenActive` / `_awakenPending` | 각성 활성 / 발동 보류(선택 완료 대기) |
| `_awakenTimeRemaining` / `_awakenMaxTime` | 각성 잔여 시간 / 게이지 fill 계산용 동적 최대치 |
| `_awakenSnapshotHand/Draw/Discard` | 각성 직전 더미 스냅샷(종료 시 복원) |
| `_awakenStoredNeutralCards` | 각성 중 격리한 무속성/파편 카드 |
| `_comboInput` | 콤보 슬라이딩 윈도우(최대 3장) |
| `_comboCooldown[]` | 콤보별 재사용 대기 입력 수(ownedComboSkills 인덱스 정렬) |
| `_queuedComboSkills` | 각성 종료 시 일괄 발동 큐 |
| `_awakenInputHistory` | 각성 중 입력 속성 전체 기록(UI 좌측 표시) |
| `_usedFireCardCount` / `_awakenFragmentExhausted` / `_awakenComboCountThisEnd` / `_comboSelfUseCount` | DB 콤보 공식용 카운터 |
| `_cursedElement` / `_curseRemainingUses` | 저주 속성 / 잔여 사용 횟수 |
| `_recoverCooldown` | 방해(회복) 재사용 쿨다운 |
| `_skipNextGaugeCount` | 바람14 다음 카드 게이지 스킵 횟수 |
| `_lastResolvedCard` | 물12(211) 재사용 대상 |

---

## 7. 직렬화 필드 / 이벤트

### Inspector 노출 필드 (`[SerializeField]`)
| 필드 | 헤더 | 기본값 / 비고 |
|---|---|---|
| `handHud` (`CardHandHUD`) | UI | 비우면 `ResolveOrCreateHandHud`로 자동 생성 |
| `effectOverlay` (`CardEffectOverlay`) | UI | 비우면 자동 생성. 시트 없으면 조용히 스킵 |
| `damageOverlay` (`PlayerDamageOverlay`) | UI | 피격 플래시/셰이크. 비우면 자동 생성 |
| `ownedComboSkills` (`List<ComboSkillDef>`) | 보유 콤보 스킬 | 수동 리스트. DB 사용 시 덮어써짐 |
| `useComboDatabase` (bool) | 콤보 스킬 DB | 기본 `true`. ON이면 Resources/ComboDB 사용 |
| `ownedComboRefIds` (`List<int>`) | 콤보 스킬 DB | 보유할 RefComboID(1000~1019). 비우면 전체 |
| `debugAddComboRefId` (int) | 콤보 스킬 DB | 기본 1000. 컨텍스트 메뉴 수동 획득용 |
| `awakenDamageEffectName` (string) | 각성 콤보 이펙트 | 기본 `"Fever_ATK"`(`feverDamageEffectName`에서 rename) |
| `awakenDamageEffectSpread` (Vector2) | 각성 콤보 이펙트 | 기본 `(220, 80)`. Damage 콤보 다중 시 분산 |
| `awakenShakeIntensityPerHit` (float) | 각성 콤보 이펙트 | 기본 `0.6`. 콤보 1건당 셰이크 |
| `awakenShakeIntensityMax` (float) | 각성 콤보 이펙트 | 기본 `2.0`. 셰이크 상한 |
| `logVerbose` (bool) | 디버그 | 기본 `true`. `Log()` 출력 토글 |
| `useAiDisruption` (bool) | 적 AI | 기본 `true`. OFF면 방해행동 랜덤 |
| `aiExplorationEpsilon` (float, Range 0~1) | 적 AI | 기본 `0.15`. 탐험 비율(데이터 수집용) |
| `logAiDecisions` (bool) | 적 AI | 기본 `false`. 방해 결정 CSV 로깅 |

> `awaken*` 4종은 `[FormerlySerializedAs("fever...")]`로 이전 직렬화명 호환(피버→각성 rename).

### 구독 이벤트
| 이벤트 | 핸들러 | 동작 |
|---|---|---|
| `_deck.OnCardDrawn` | `OnCardDrawnHandler` (`:184`) | `gaugeSinceDrawn=0`, `justDrawn=true`; 비각성 시 `NotifyCardEnteredHand`(바람319). `Awake`에서 구독 |
| `_deck.OnCardDiscarded` | `OnCardDiscardedHandler` (`:194`) | 비각성 시 `NotifyCardDiscardedFromHand`(물205/206/219). `Awake`에서 구독 |
| `Player.OnPlayerHit` | `OnPlayerHitHandler` (`:492`) | 물18(217): 피격 시 회복 1. `StartBattle`에서 구독 |
| `Player.OnBlockConsumedByAttack` | `OnBlockConsumedHandler` (`:499`) | 땅18(417): 방어도 소모 시 파편 1장 버린 더미. `StartBattle`에서 구독 |
| `EnemyStat.OnGaugeFull` | `OnEnemyAttackFiredHandler` (`:506`) | `firstCardAfterEnemyAttack=true`(바람20/319), 땅424 방어도 획득. `StartBattle`에서 구독 |

이벤트 구독 해제: `OnDestroy`(`:171`)에서 일괄, `StartBattle`은 재구독 전 `-=`로 중복 방지.

---

## 8. 주의 / 엣지케이스

- **효과는 연출 종료 시점 실행**: 일반 카드는 `OnUseCardRequested`가 즉시 `PullFromHand`만 하고, 실제 효과(`ResolveCardUse`)는 중앙 연출이 끝나는 콜백에서 실행된다. 따라서 효과 적용에 시각 연출 시간만큼 지연이 있다 (`:692`).
- **EndBattle 시 연출/지연효과 취소**: 보상 화면과 카드 효과가 겹쳐 실행되는 것을 막기 위해 `EndBattle`에서 `CancelCardUsePresentations()`를 호출하고, `ResolveCardUse`도 진입부에서 `_inBattle`을 재확인해 종료 후 지연 콜백을 무시한다 (`:335`, `:709`).
- **각성 중 콤보 갱신 보류**: `RefreshOwnedCombosRuntime`는 각성 중이면 desync 방지를 위해 적용을 보류하고 경고만 출력한다(다음 각성부터 반영). `_comboCooldown`이 `ownedComboSkills` 인덱스에 정렬돼 있어 각성 도중 길이가 바뀌면 어긋나기 때문 (`:355`).
- **각성 발동 타이밍(보류)**: 속성 10장 도달 시 즉시 발동하지 않고 `_awakenPending`으로 보류 → 진행 중인 카드 선택/픽커가 끝난 뒤(`TryActivatePendingAwaken`) 발동. 선택 모드는 `DeferredSelection`이 1프레임 뒤 진입하므로 `DeferredPendingAwakenCheck`가 2프레임 대기 후 확인 (`:872`, `:933`).
- **각성 중 카드 효과 치환**: 각성 동안 모든 카드는 효과 대신 "드로우 1"로 치환되고 콤보 슬롯에만 등록된다. 그래서 `OnCardDrawn/Discarded` 핸들러의 카드 트리거도 각성 중에는 제외된다 (`:191`, `:197`, `:640`).
- **각성 중 적 게이지 정지**: `AccrueEnemyGauge`는 각성 중 즉시 `return`하므로 각성 동안 적 게이지가 오르지 않는다 (`:894`).
- **더미 스냅샷 복원**: 각성 진입 시 패/뽑을·버린 더미를 스냅샷하고 종료 시 `RestorePiles`로 그대로 되돌린다. 각성 중의 드로우/사용 churn과 격리한 무속성/파편이 함께 복원되므로, 각성 중 카드 변화는 종료 후 사라진다 (`:949`, `:992`).
- **빙결 순서 처리**: 한 카드가 부여한 빙결이 자기 자신의 게이지 상승을 막지 않도록, 게이지 처리 동안만 빙결량을 사용 전 값으로 되돌렸다가 복원한다 (`:714`, `:847`~`860`).
- **콤보 보너스 시간 표기 불일치**: 코드 주석/로그는 "+1s"라 적힌 곳이 있으나 실제 상수는 `COMBO_BONUS_SECONDS = 0.1f`다 (`:26` vs `:659`).
- **콤보 "전부 소진" 종료 없음**: 콤보는 `COMBO_REUSE_INPUT(5)` 쿨다운 후 재사용 가능하므로 각성은 콤보 소진이 아닌 **시간 종료로만** 끝난다 (`:679`).
- **`AddOwnedCombo`의 전체→지정 전환**: `ownedComboRefIds`가 빈 리스트(=전체 보유)일 때 처음 1개를 추가하면 '지정 집합'으로 바뀌어 나머지 콤보를 잃는다(주석 경고 있음) (`:377`).
- **적 참조 유실 복구**: `Update`에서 `_enemy`가 null이거나 비활성이면 매 프레임 `FindFirstObjectByType<EnemyController>`로 재탐색해 `_ctx`에 다시 주입한다 (`:205`).
- **자동 생성 폴백**: `handHud`/`effectOverlay`/`damageOverlay`/EventSystem이 비어 있으면 `StartBattle`에서 자동 생성한다(테스트 편의). 결과적으로 Inspector 미연결이어도 동작은 하지만 경고 로그가 남는다 (`:287`~`304`).
