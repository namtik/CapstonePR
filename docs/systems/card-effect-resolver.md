# 카드 효과 엔진 (CardEffectResolver) 명세

> 대상 파일: `Assets/Script/Battle/Deck/CardEffectResolver.cs` (1,460줄)
> 효과 스키마: `Assets/Script/Battle/Card/CardEffectData.cs`
> 데이터 로딩: `Assets/Script/Battle/Card/CardDatabase.cs`
> 본 문서의 모든 동작은 실제 코드(file:line)에서 확인한 내용이며 추측을 포함하지 않는다.

---

## 1. 개요 / 책임

`CardEffectResolver`는 데이터 드리븐(data-driven) 카드 효과 실행기다. 카드 ID로 효과 메서드를 하드코딩 분기하지 않고, `Resources/CardDB/CardEffects.json`에 정의된 한 줄 단위 효과 블록(`CardEffectData`)을 해석해 실행한다 (`CardEffectResolver.cs:93-96`).

효과 한 블록은 다음 5요소(+부가)로 기술된다:

- **When** (`when`): 발동 시점. 즉시(`ON_USE`/`ON_USE_BEFORE_GAUGE`)인지, 등록형 파워 트리거(예: `ON_CARD_USED`, `ON_CARD_DAMAGE_HIT_DEALT`)인지 분류한다 (`IsImmediateTrigger`, `CardEffectResolver.cs:409-420`).
- **If** (`ifCond`): 실행 전제 조건 문자열 (`EvaluateCondition`, `CardEffectResolver.cs:646-724`).
- **Do** (`doAction`): 실제 동작 동사 (`ApplyAction`, `CardEffectResolver.cs:883-1201`).
- **Target** (`target`): 대상 더미/존 등 (예: `EXHAUST_CARD`의 `DRAW_PILE`/`DISCARD_PILE`).
- **Formula** (`formula` / `hitFormula`): 수치/타격 수를 런타임 상태로 동적 계산 (`EvalFormula` `:744`, `EvalHitFormula` `:838`).

ID별 분기가 전혀 없는 것은 아니다. 정확히는 **카드 ID 분기는 없지만**, When/Do/status/element 조합으로 효과 종류를 추론해 `CardEffectContext`의 불리언 플래그/누적값을 켜는 방식이다 (`RegisterPowerTrigger`, `CardEffectResolver.cs:426-623`). 주석은 한국어로 각 분기에 대응하는 카드 번호(예: `불19(118)`, `바람22(321)`)를 병기한다.

특수 동작(패에서 카드 선택, 마지막 카드 효과 재발동 등)은 직접 실행하지 않고 `ResolveResult` 플래그로 외부(`NewBattleController`)에 위임한다 (`CardEffectResolver.cs:95`).

---

## 2. 위치 / 타입

| 항목 | 값 |
|---|---|
| 경로 | `Assets/Script/Battle/Deck/CardEffectResolver.cs` |
| namespace | `Battle.Deck` (`CardEffectResolver.cs:6`) |
| 주 타입 | `public class CardEffectResolver` (일반 클래스, MonoBehaviour 아님) (`:97`) |
| 동반 타입 | `public class CardEffectContext` (상태 백, `:12-91`) |
| 중첩 타입 | `public struct ResolveResult` (`Resolve` 반환 구조체, `:99-122`) |

`CardEffectResolver`는 Unity 컴포넌트가 아닌 순수 C# 클래스로, 소유자(`NewBattleController`)가 직접 `new`로 생성한다 (`NewBattleController.cs:85`). 실행에 필요한 모든 외부 상태(플레이어, 적, 덱 등)는 `CardEffectContext` 인스턴스를 통해 주입받는다.

---

## 3. 참조 (의존)

`CardEffectContext`가 외부 시스템에 대한 핸들을 모두 보유하며, 리졸버는 이를 통해 접근한다 (`CardEffectResolver.cs:12-18`).

| 의존 대상 | 접근 경로 | 용도 |
|---|---|---|
| `CardDatabase.GetEffects(id)` | static | 카드 ID의 효과 블록 목록 조회 (`:156`, `:231`, `:256`, `:295`, `:354`) |
| `CardDatabase.CreateFragmentInstance()` / `FragmentIds` | static | 파편 카드 생성·추가 (`:199`, `:203`, `:1222`, `:1234`, `:1243`) |
| `CardDeckSystem` | `Ctx.deck` | 드로우/버리기/소멸/존 이동/패 한도 등 모든 덱 조작 (`:973`, `:979`, `:992`, `:1269` 등) |
| `Player` | `Ctx.player` | 피해(`TakeDamage`)·회복(`Heal`)·방어도(`AddGuard`)·상태(`AddStatus`)·HP·attackDamage·guard (`:149`, `:1413`, `:1447`, `:1397` 등) |
| `EnemyController` | `Ctx.enemy` | 적 피해(`TakeDamage`)·상태(`AddStatus`/`SetStatus`) (`:1374`, `:1422`, `:1146`) |
| `EnemyStat` | `Ctx.enemyStat` | 적 HP/생존(`IsAlive`)·상태 사전(`statusEffects`)·보스/정예(`IsBossOrElite`) (`:1376`, `:1441`, `:1166`) |
| `CardInstance` | `Resolve(card)` 인자 | `Id`/`Type`/`Element`/`data`/`cursed`/`justDrawn`/`gaugeSinceDrawn`/`selfUseCount` (`CardData.cs:72-101`) |
| `Battle.RunDeckState.Instance` | static | 영구 공격력 누적(`AddPermanentAttackPower`) (`:612`, `:1064`) |
| `Battle.NewBattleController.Instance` | static | `LastResolvedCard` 조회, `ClearAllCurses()` 호출 (`:701-703`, `:1077`) |

> `CardInstance.HasTag(...)`는 `card.data.HasTag(...)`로 호출된다 (`CardData`가 `displayName`/`HasTag`/`element`/`type`/`gauge`를 제공, `CardData.cs`).

---

## 4. 피참조 (호출자)

유일한 호출자는 `NewBattleController`다 (`NewBattleController.cs:85`에서 `_resolver` 보유).

| 호출 지점 | file:line | 의미 |
|---|---|---|
| `_resolver.Bind(_ctx)` | `NewBattleController.cs:163` | Awake 시 컨텍스트 주입 |
| `_resolver.Resolve(card)` | `:716` | 카드 사용 효과 처리(중앙 연출 종료 시점) |
| `_resolver.Resolve(_lastResolvedCard)` | `:744` | `recastLastCard` 결과에 따른 마지막 카드 재발동(물12/211) |
| `_resolver.NotifyExile(card)` | `:738`, `:758` | 카드 소멸 시 — 자기 `ON_SELF_EXHAUST`·파워 화상 트리거 |
| `_resolver.NotifyCardEnteredHand(card)` | `:191` | 드로우로 패 진입 시 — `ON_CARD_RETURNED_TO_HAND` (각성 중 제외) |
| `_resolver.NotifyCardDiscardedFromHand(card)` | `:197` | 패에서 '버려질' 때 — `ON_SELF_DISCARDED` + `ON_CARD_MOVED_TO_DISCARD` |
| `_resolver.NotifyCardMovedToDiscard(card)` | `:732` | 사용 후 버린 더미로 이동 시 — `ON_CARD_MOVED_TO_DISCARD` |

`ResolveResult`의 `requires*Selection`/`recastLastCard`/`returnToHandInsteadOfDiscard`/`skipNextGaugeCost`/`currentCardFreeThisUse`/`exile`/`keepOnField` 등의 플래그는 모두 `NewBattleController.ResolveCardUse`(`:705-`)가 해석해 후속 UI/덱 동작을 수행한다.

---

## 5. 공개 API

### 5.1 메서드

| 멤버 | 시그니처 | 설명 | file:line |
|---|---|---|---|
| `Ctx` | `CardEffectContext Ctx { get; private set; }` | 현재 바인딩된 컨텍스트 | `:124` |
| `Bind` | `void Bind(CardEffectContext ctx)` | 컨텍스트 주입 + `ctx.resolver = this` 역참조 설정 | `:131-135` |
| `Resolve` | `ResolveResult Resolve(CardInstance card)` | 메인 진입점. 저주→효과 루프→소멸/파워→공통 파워 트리거 순으로 처리 | `:141-245` |
| `NotifyExile` | `void NotifyExile(CardInstance card)` | 카드 소멸 콜백. 파워 `burnOnExile` 적용 + `exhaustedCardCount` 증가 + 해당 카드의 `ON_SELF_EXHAUST` 실행 | `:247-267` |
| `NotifyCardDiscardedFromHand` | `void NotifyCardDiscardedFromHand(CardInstance card)` | `ON_SELF_DISCARDED` + `ON_CARD_MOVED_TO_DISCARD` 실행 | `:274-278` |
| `NotifyCardMovedToDiscard` | `void NotifyCardMovedToDiscard(CardInstance card)` | `ON_CARD_MOVED_TO_DISCARD`만 실행 | `:281-284` |
| `NotifyCardEnteredHand` | `void NotifyCardEnteredHand(CardInstance card)` | `ON_CARD_RETURNED_TO_HAND` 실행 | `:287-290` |

> 그 외 `Register*`/`Trigger*`/`Eval*`/`Handle*`/`Add*`/`Deal*` 등은 모두 `private` 내부 메서드다. 외부에 노출되는 생애주기 훅은 위 4개 `Notify*`가 전부다.

### 5.2 `ResolveResult` 필드 (`:99-122`)

| 필드 | 타입 | 의미 |
|---|---|---|
| `exile` | bool | 카드 소멸(EXHAUST). `EXHAUST` 태그 또는 바람314 N회 소진 시 true (`:177`, `:1302`) |
| `keepOnField` | bool | 파워 카드 — 필드 잔류 (`:182`) |
| `totalDamage` | int | 이번 처리로 적에게 입힌 누적 피해(연쇄·재발동·공통 파워 피해 포함) |
| `requiresHandExileSelection` | bool | 패에서 1장 선택 → 소멸 (`EXHAUST_CARD` + `SELECT_ONE_FROM_HAND`, `:1015`) |
| `requiresHandDiscardSelection` | bool | 패에서 1장 선택 → 버리기 (`DISCARD_CARD` + `SELECT_ONE_FROM_HAND`, `:1003`) |
| `requiresHandCopySelection` | bool | 패에서 1장 선택 → 복사 (`COPY_CARD` + `SELECT_ONE_FROM_HAND`, `:1326`) |
| `requiresDrawPileMoveSelection` | bool | 뽑을 더미에서 1장 선택 → 이동 (`:1289`) |
| `requiresDiscardMoveSelection` | bool | 버린 더미에서 1장 선택 → 이동 (`:1291`) |
| `requiresFragmentPoolSelection` | bool | 파편 풀에서 선택 (`ADD_CARD` + `SELECT_ONE_FROM_FRAGMENT_POOL`, `:1251`) |
| `skipNextGaugeCost` | bool | 다음 카드 게이지 무료 (바람14/313, `SET_NEXT_CARD_FREE`, `:1086`) |
| `recastLastCard` | bool | 마지막 사용 카드 효과 재발동 (물12/211, `COPY_LAST_CARD_EFFECT`, `:1112`) |
| `currentCardFreeThisUse` | bool | 이 카드 자체 게이지 무료 (바람11/310, `SET_GAUGE_COST` amount=0, `:1091`) |
| `poweredField` | bool | `keepOnField`와 동의어(구버전 호환). 파워 시 함께 true (`:183`) |
| `returnToHandInsteadOfDiscard` | bool | 사용 후 버린 더미 대신 패로 복귀 (바람314, `:1304`) |
| `handSelectionCardFilter` | string | 패 선택 모드 카드 필터(예: `NEUTRAL_CARD`, 비우면 전체) |
| `fragmentPickerTargetZone` | string | 파편 선택 결과 존(`HAND`/`DISCARD_PILE`/`DISCARD_PILE_SHUFFLE`) |
| `fragmentPickerCopyCount` | int | 파편 선택 시 같은 ID로 복제할 수량(414=10) |

---

## 6. 내부 동작 / 데이터 흐름

### 6.1 `Resolve` 파이프라인 (`:141-245`)

1. **초기화**: `Ctx.enemyKilledThisResolve = false` (땅410 처치 추적 시작, `:144`).
2. **저주 처리** (`:147-152`): `card.cursed && Ctx.player != null`이면 플레이어 6 피해 + 로그 + `cursed = false`.
3. **효과 루프** (`:155-174`):
   - `isPower = card.Type == Power || data.HasTag("POWER")`.
   - `CardDatabase.GetEffects(card.Id)` 순회.
   - **파워 등록 분기**: `isPower && !IsImmediateTrigger(when)`이면 `RegisterPowerTrigger` 호출 후 `continue` (`:161-165`).
   - **즉시 분기**: `IsImmediateTrigger(when)`가 아니면 skip → 통과 시 `EvaluateCondition` → `ApplyAction` (`:166-168`).
   - 효과가 비어 있으면 `LogWarning` (`:173`).
4. **EXHAUST 태그** (`:177`): `data.HasTag("EXHAUST")` → `result.exile = true`.
5. **파워 필드** (`:180-184`): `isPower`면 `keepOnField = poweredField = true`.
6. **공통 파워 트리거** (`:187-242`): `applyPowerEffects = !result.keepOnField` (파워 카드 자신은 제외). 등록된 파워가 활성일 때 매 카드 사용마다 발동:
   - 바람321 추가 피해(`bonusDamagePerCard`), 땅419 방어도(`blockOnCardUse`), 땅420/421 파편 추가, 물221 회복, 바람325 연쇄 획득, 땅425 파편 사용 시 피해, 물226 N회마다 빙결, 불123 소멸 카드 효과 1회 재발동.
7. **반환**: `ResolveResult`.

### 6.2 트리거 분류 — `IsImmediateTrigger` (`:409-420`)

`when`이 비었거나(`null` → 즉시 간주) `ON_USE`/`ON_USE_BEFORE_GAUGE`면 즉시. 그 외 모든 `When`은 등록형(파워) 또는 생애주기 트리거로 분류된다.

### 6.3 조건 평가 — `EvaluateCondition` (`:646-724`)

| 조건 키 | 판정 |
|---|---|
| `PLAYER_HP_FULL` | `currentHp >= maxHp` |
| `PLAYER_HP_RATE_LTE:N` | 플레이어 HP% ≤ N |
| `ENEMY_STATUS_GTE:KEY:N` | 적 상태(KEY) ≥ N |
| `PLAYER_BLOCK_EQ:N` | 반올림한 방어도 == N |
| `CARD_ELEMENT_EQ:E` | 카드 속성 == E (`ElementMatches`) |
| `PREVIOUS_CARD_TYPE_EQ:T` | 직전 카드 타입 == T |
| `FIRST_CARD_AFTER_ENEMY_ATTACK` | `Ctx.firstCardAfterEnemyAttack` |
| `USED_IMMEDIATELY_AFTER_DRAW` | `card.justDrawn` |
| `LAST_USED_CARD_EXISTS_EXCLUDING_SELF` | `NewBattleController.LastResolvedCard != null && != card` |
| `EXCLUDE_TRIGGER_SOURCE:*` / `DAMAGE_SOURCE_IS_CARD_EFFECT` | 항상 true(데이터 흐름에서 별도 처리) |
| `ENEMY_KILLED_BY_THIS_CARD` | `Ctx.enemyKilledThisResolve` (땅410) |
| `USED_CARD_HAS_TAG:T` | `card.data.HasTag(T)` |
| `CARD_TYPE_EQ:T` | `card.Type == T` |
| (그 외) | `LogWarning` 후 **true 반환(보수적 통과)** (`:722-723`) |

### 6.4 동작 디스패치 — `ApplyAction` (`:883-1201`)

`amount`는 `formula`가 있으면 그 평가값을 우선 사용한다 (`EvalFormula`). 단, **`-1` 센티넬**은 "amount 유지하되 조건 충족 시 ×2"를 의미하고(`multiplyBy2`), `GAUGE_SINCE_DRAWN`은 특례로 `card.gaugeSinceDrawn`을 사용한다 (`:888-906`). `hits`도 `hitFormula`가 있으면 우선 (`:909-914`).

| Do 동사 | 동작 | file:line |
|---|---|---|
| `DAMAGE` | `DealDamage`로 적 피해. `repeat == REPEAT_BY_RESOURCE_CONSUME_ALL` + `Resource=CHAIN`이면 연쇄 0까지 반복(안전장치 200회) | `:918-950` |
| `GAIN_BLOCK` | `AddPlayerGuard(amount)` | `:952-954` |
| `HEAL` | `HealPlayer(amount)` | `:956-958` |
| `LOSE_HP` | 플레이어 `TakeDamage(amount,"self_loss")` + `lastHpLost` 기록 + 불122(공격력↑) | `:960-970` |
| `DRAW_CARD` | `deck.Draw(amount)` | `:972-974` |
| `DISCARD_HAND` | `DiscardAllFromHand` + 물220면 버린 수만큼 드로우 | `:976-983` |
| `EXHAUST_HAND` | 패 전체 소멸 + 각 카드 `NotifyExile` | `:985-996` |
| `DISCARD_CARD` | `SELECT_ONE_FROM_HAND`이면 `requiresHandDiscardSelection` 플래그 | `:998-1007` |
| `EXHAUST_CARD` | `SELECT`/`RANDOM`(패 무작위 1장)/`ALL`(`DRAW_PILE`·`DISCARD_PILE` 전체 소멸) | `:1009-1033` |
| `APPLY_STATUS` | `AddEnemyStatus(status, amount, isCardEffect:true)` | `:1035-1041` |
| `GAIN_STATUS` | `CHAIN`→연쇄(999 클램프), `ATTACK_POWER`/`CHAIN_DAMAGE_BONUS`→보너스, 그 외→플레이어 상태이상 | `:1043-1055` |
| `GAIN_STAT` | `ATTACK_POWER`(Permanent면 RunDeckState 영구), `CHAIN_DAMAGE_BONUS` | `:1057-1068` |
| `CLEANSE_STATUS` | `CURSE`면 `ClearAllCurses()` + 물219 회복 | `:1070-1083` |
| `SET_NEXT_CARD_FREE` | `skipNextGaugeCost` + `nextCardIsFree` | `:1085-1088` |
| `SET_GAUGE_COST` | amount==0이면 `currentCardFreeThisUse` | `:1090-1092` |
| `MODIFY_HAND_LIMIT` | `handLimitBonus` + `deck.IncreaseHandLimit` | `:1094-1097` |
| `ADD_CARD` | `HandleAddCard`(파편 추가/선택) | `:1099-1100`, `:1205-1260` |
| `MOVE_CARD` | `HandleMoveCard`(더미 선택 이동/SELF 복귀) | `:1103-1104`, `:1285-1309` |
| `COPY_CARD` | `HandleCopyCard`(자기복사/패선택/패전체복사) | `:1107-1108`, `:1311-1346` |
| `COPY_LAST_CARD_EFFECT` | `recastLastCard` 플래그 | `:1111-1113` |
| `NO_EFFECT` | 효과 없음(500 카드) | `:1115-1117` |
| `MODIFY_DAMAGE_MULTIPLIER` / `MODIFY_HIT_COUNT` | 파워 등록 전용. ON_USE로 와도 무시 | `:1119-1122` |
| `MODIFY_AWAKEN_GAUGE_MAX` | `awakenGaugeMaxDelta += amount` (바람326) | `:1124-1127` |
| `MODIFY_STATUS_RULE` | `BURN`이면 `burnPersistsOnEnemyTurn=true` (불126) | `:1129-1133` |
| `MODIFY_STATUS_TARGET` | `BURN`이면 `burnSkillHitsAllActive=true` (불124) | `:1135-1139` |
| `CONSUME_STATUS` | 적 상태 모두 소모 + `burn`이면 `lastConsumedBurn` 기록 (불116) | `:1141-1149` |
| `DOUBLE_STATUS` | 적 상태 스택 ×2 (물210) | `:1151-1158` |
| `INSTANT_KILL` | 즉사. `ExcludeBossElite=true` + 보스/정예면 제외 (물208) | `:1160-1170` |
| `TRANSFORM_STATUS_TO_BLOCK` | `CHAIN` 전량을 방어도로 전환 (바람320) | `:1172-1182` |
| `TRIGGER_HAND_CARDS` | 패 카드들 효과 발동(이동 없음) (바람321/땅407) | `:1184-1188` |
| `TRIGGER_DRAW_PILE_CARDS` | 뽑을 더미의 파편 전부 발동 후 소멸 (땅419) | `:1190-1194` |
| (그 외 비어있지 않은 동사) | `LogWarning("미구현 동사")` (`:1196-1199`) |

### 6.5 공식 평가 — `EvalFormula` (`:744-836`)

데미지/수량을 런타임 상태로 계산: `ENEMY_STATUS_VALUE:KEY`, `PLAYER_BLOCK`, `FLOOR(PLAYER_CURRENT_HP/2)`, `LAST_HP_LOST[*N]`, `LAST_CONSUMED_BURN[*N]`(불116), `USED_ATTACK_CARD_COUNT`, `USED_FRAGMENT_CARD_COUNT`, `LAST_DISCARDED_COUNT`, `EXHAUSTED_CARD_COUNT`(불115), `CONSUMED_CHAIN_COUNT`(바람310), `HIT_ENEMY_COUNT`(땅409), `ENEMY_COUNT[*N]`(단일 적 → 1). **센티넬 `-1` 반환**: `IF_ENEMY_HAS_FROST_MULTIPLY_2`(물209), `IF_PLAYER_HP_RATE_LT_50_MULTIPLY_2` → `multiplyBy2`로 amount ×2 신호. `GAUGE_SINCE_DRAWN`은 여기서 0 반환, 호출 측에서 `card.gaugeSinceDrawn` 대입 (`:789-794`, `:898-901`). 미지원 공식은 `LogWarning` 후 0.

### 6.6 타격 수 공식 — `EvalHitFormula` (`:838-877`)

`USED_CARD_NAME_COUNT:NAME`(자기 자신이면 +1), `USED_ATTACK_CARD_COUNT`(현재 공격이면 +1, 바람309), `USED_SAME_NAME_CARD_COUNT`(현재 포함, 바람311), `DRAW_PILE_FRAGMENT_COUNT`(땅408). 미지원은 `LogWarning` 후 0.

### 6.7 피해·연쇄 — `DealDamage` / `TriggerChain`

`DealDamage(amount, isAttackCard, baseHits)` (`:1352-1389`):
- 공격 카드면 `attackHitCountBonus`만큼 타격 추가 (`:1357-1358`).
- 피해량 `dmg = amount + (공격 카드면 attackPowerBonus)` (`:1360`).
- 불120: 공격 카드 + `damageMultiplierActive > 1` + 플레이어 HP% ≤ 임계 시 `dmg *= 배율` (`:1363-1368`).
- `lastHitEnemyCount = 1` 고정(단일 적). 타격 루프마다 `enemy.TakeDamage(dmg)`, 적 사망 시 `enemyKilledThisResolve=true`, `burnOnHit`이면 화상, 공격 카드면 `TriggerChain()` 누적, `chainGainOnHit`이면 `chainCount++` (`:1370-1387`).

`TriggerChain()` (`:1391-1402`): `chainCount<=0`이면 0. 아니면 `chainCount--`, `consumedChainCount++`. **연쇄 피해 = max(1, round(player.attackDamage × 0.1)) + chainBonusDamage** (기획서 0.6v: 공격력 10%, 최소 1). 적에게 즉시 적용.

### 6.8 등록형 파워 트리거 — `RegisterPowerTrigger` (`:426-623`)

파워 카드의 비-즉시 효과를 `When`+`Do`(+`status`/element)로 추론해 `Ctx` 플래그/누적값을 켠다. 주요 매핑(발췌):

| When | 조건 | 켜는 Ctx |
|---|---|---|
| `ON_CARD_DAMAGE_HIT_DEALT` | `APPLY_STATUS BURN` / `GAIN_STATUS CHAIN` | `burnOnHit*` / `chainGainOnHitActive` |
| `ON_BURN_APPLIED_BY_CARD_EFFECT` | `DAMAGE` / `MODIFY_STATUS_TARGET` | `burnOnByCard*` / `burnSkillHitsAllActive` |
| `ON_ATTACK_CARD_DAMAGE_CALC` | `MODIFY_HIT_COUNT` / `MODIFY_DAMAGE_MULTIPLIER` | `attackHitCountBonus*` / `damageMultiplier*`(임계는 `ifCond`에서 파싱, 기본 25) |
| `ON_CARD_USED` | `DAMAGE`/`GAIN_BLOCK`/`GAIN_STATUS CHAIN`/`COPY_LAST_CARD_EFFECT` | `bonusDamagePerCard`/`blockOnCardUse`/`chainGainOnCardUse`/`recastExhaustCardActive` |
| `ON_ELEMENT_CARD_USED` | element=WATER/EARTH (+ `HEAL`/`ADD_CARD`/`APPLY_STATUS FROST`) | `waterUseHeal`/`fragmentOnEarthUse`/`frostOnWaterUse*` |
| `ON_FRAGMENT_CARD_USED` | `ADD_CARD` / `DAMAGE` | `fragmentOnFragmentUse` / `damageOnFragmentUse*` |
| `ON_CARD_EXHAUSTED` | `APPLY_STATUS BURN` | `burnOnExile*` |
| `ON_PLAYER_HEALED_ACTUAL` / `ON_PLAYER_HIT_INCLUDING_BLOCK` / `ON_CURSE_CLEANSED` / `ON_CARD_DISCARDED_BY_CARD_EFFECT` / `ON_BLOCK_CONSUMED_BY_ATTACK` | 각 `DRAW_CARD`/`HEAL`/... | `healToDraw`/`healOnPlayerHit`/`healOnCurseCleanse*`/`discardToDraw`/`fragmentOnBlockConsume` |
| `ON_CARD_USED_BEFORE_GAUGE` | `SET_GAUGE_COST` amount=0 | `firstCardAfterAttackFreeActive` |
| `ON_PLAYER_LOSE_HP_BY_CARD_EFFECT` | `GAIN_STAT`/`GAIN_STATUS` | `attackPowerOnHpLoss*` |
| `ON_FROST_APPLIED_BY_CARD_EFFECT` | `APPLY_STATUS` | `frostOnFrostByCard*` |
| `ON_ENEMY_ATTACK_RESOLVED` | `GAIN_BLOCK` | `blockOnEnemyAttack*` |

추가로 `When`과 무관하게 `GAIN_STAT ATTACK_POWER`(불117, Permanent면 RunDeckState 영구), `CHAIN_DAMAGE_BONUS`, `MODIFY_HAND_LIMIT`(땅418, 즉시 적용)을 처리한다 (`:606-622`).

### 6.9 다른 카드 효과 발동 / 재귀 방지

- `TriggerOtherCards`(`:316-331`): 패 카드들의 즉시 효과만 발동(카드 이동 없음). `cardFilter`로 속성 필터(`PassesTriggerFilter`, `:365-377`). `_triggeringOtherCards` 가드로 무한 재귀 차단.
- `TriggerDrawPileFragments`(`:334-350`): 뽑을 더미 파편을 발동 후 버린 더미로 이동.
- `RunWhenTriggers`(`:292-309`): 생애주기 트리거 실행. `_inLifecycleTrigger` 가드로 재진입 차단.

### 6.10 `CardEffectContext` 주요 필드 (`:12-91`)

상태 백(state bag). 핵심만 발췌:

- **피해/공격력**: `attackPowerBonus`(불117, DAMAGE에 가산), `damageMultiplierActive`/`ThresholdHp`(불120), `attackHitCountBonus`(바람320).
- **연쇄**: `chainCount`(누적, 999 클램프), `chainBonusDamage`(바람317), `chainGainOnHitActive`(바람318), `consumedChainCount`.
- **화상/빙결**: `burnOnHit*`/`burnOnByCard*`/`burnOnExile*`(`burnOnExileAmount`), `burnPersistsOnEnemyTurn`(불126), `frostOnWaterUse*`/`frostOnFrostByCard*`(+`FrostTriggerReentrancy` 무한루프 가드, `:89-90`).
- **물/회복**: `healToDraw`/`discardToDraw`/`waterUseHeal`/`healOnCurseCleanse*`/`healOnPlayerHit`.
- **땅/파편**: `fragmentOnEarthUse`/`fragmentOnFragmentUse`/`blockOnCardUse*`/`damageOnFragmentUse*`/`fragmentOnBlockConsume`.
- **런타임 통계**: `lastHpLost`, `lastConsumedBurn`, `exhaustedCardCount`, `lastHitEnemyCount`, `enemyKilledThisResolve`, `lastDiscardedCount`, `usedCardNameCounts`(이름별 사용 횟수).
- **게이지/흐름**: `nextCardIsFree`(바람313), `currentCardJustDrawn`(바람310), `firstCardAfterEnemyAttack`, `handLimitBonus`(땅418), `awakenGaugeMaxDelta`(바람326), `previousCardType`.

---

## 7. 데이터 — `CardEffectData` 스키마

`CardEffects.json`의 한 줄 = `CardEffectData` 한 인스턴스. `CardDatabase.LoadEffects`가 `cardId`별 리스트로 그룹핑한다 (`CardDatabase.cs:230-262`). 카드 한 장은 `index` 1,2,...로 여러 효과를 가질 수 있다.

| 필드 | 타입 | 의미 / 사용처 |
|---|---|---|
| `cardId` | int | 소속 카드 ID (`CardEffectData.cs:14`) |
| `index` | int | 같은 카드 내 효과 순번 |
| `when` | string | 발동 시점. 즉시(`ON_USE`/`ON_USE_BEFORE_GAUGE`) 또는 등록/생애주기 트리거 (`IsImmediateTrigger`, `RegisterPowerTrigger`) |
| `ifCond` | string | 조건. `EvaluateCondition`이 해석 (예: `PLAYER_HP_RATE_LTE:25`) |
| `doAction` | string | 동작 동사. `ApplyAction` 디스패치 키 (예: `DAMAGE`, `GAIN_BLOCK`) |
| `target` | string | 대상 존. 주로 `EXHAUST_CARD`의 `DRAW_PILE`/`DISCARD_PILE` (`:1012`, `:1029-1030`) |
| `amount` | int | 기본 수치. `formula` 없을 때 사용 |
| `formula` | string | 동적 수치 계산식. `EvalFormula`(있으면 `amount`보다 우선) |
| `hits` | int | 타격 횟수(기본 1) |
| `hitFormula` | string | 동적 타격 수. `EvalHitFormula`(있으면 우선) |
| `status` | string | 상태/스탯 키 (예: `BURN`, `FROST`, `CHAIN`, `ATTACK_POWER`, `CURSE`) |
| `cardFilter` | string | 카드 필터 (`RANDOM_FRAGMENT_CARD`/`FIRE_CARD`/패 선택 필터 등) |
| `fromZone` | string | 출발 존 (`COPY_CARD`의 `SELF_CARD`/`HAND` 등) |
| `toZone` | string | 도착 존 (`HAND`/`DRAW_PILE[_SHUFFLE]`/`DISCARD_PILE[_SHUFFLE]`) — `PlaceCardInZone`(`:1262-1283`) |
| `select` | string | 선택 모드 (`SELECT_ONE_FROM_HAND`/`RANDOM`/`ALL`/`SELF`/`ALL_IN_HAND`/`SELECT_ONE_FROM_FRAGMENT_POOL` 등) |
| `repeat` | string | 반복 모드 (`REPEAT_BY_RESOURCE_CONSUME_ALL`, `:923-939`) |
| `extra` | string | `키=값;키=값` 부가 파라미터. `ExtraSubstring`/`ExtraIntOrDefault`로 파싱 (`:383-403`). 예: `Permanent=true`, `EveryNthUse=3`, `Resource=CHAIN`, `ConsumePerRepeat`, `ExhaustAfterUses`, `ReturnToHandInsteadOfDiscard`, `CopySelectedSameName`, `ExcludeBossElite` |
| `runtimeKey` | string | 데이터 보존용 키(현 리졸버 로직에서 직접 참조 없음) |

> `CardDatabase`는 `JsonUtility.FromJson<CardEffectDataList>`로 로드하며, 효과 JSON이 없으면 경고 후 효과 없이 진행한다 (`CardDatabase.cs:233-238`).

---

## 8. 주의 / 엣지케이스

1. **영구 공격력(런 지속)**: `GAIN_STAT ATTACK_POWER` + `extra`에 `Permanent=true`(`IsPermanentExtra`, `:1404-1407`)면 `RunDeckState.Instance.AddPermanentAttackPower`로 전투 종료 후에도 유지된다 (땅410, `:612`/`:1064`). `RunDeckState`가 없으면(`?.`) 그 전투 한정으로만 적용.

2. **연쇄 999 클램프**: 연쇄 획득(`GAIN_STATUS CHAIN`, 바람325)은 항상 `Mathf.Min(999, ...)`로 상한 처리 (`:211`, `:1048`). 기획서 0.6v 기준.

3. **단일 적 구조**: `ALL`/`RANDOM_ENEMY`/`ENEMY_COUNT`는 모두 현재 적 1체로 귀결된다. `lastHitEnemyCount`는 항상 1(`:1371`), `ENEMY_COUNT` 공식은 생존 적이면 1(`:806`). `burnSkillHitsAllActive`(불124)·`damageOnFragmentUse`의 "무작위 적"은 단일 적 전투에서만 의미가 있다(코드 주석 명시, `:86`, `:213`).

4. **미지원 동사/조건/공식 = 경고 후 안전 처리**: 모르는 `doAction`은 `LogWarning("미구현 동사")` 후 무시(`:1196-1199`), 모르는 `ifCond`는 **true로 통과(보수적)**(`:722-723`), 모르는 `formula`/`hitFormula`는 0 반환(`:834`, `:875`). 또한 효과 정의 자체가 없으면 `LogWarning`(`:173`).

5. **공식 `-1` 센티넬 함정**: `EvalFormula`가 `-1`을 반환하면 "amount를 그대로 두되 조건 충족 시 ×2"라는 신호다(`multiplyBy2`). 실제 수치 `-1`을 의도하면 안 된다 (`:893-897`). 정상 0이 필요한 케이스(`GAUGE_SINCE_DRAWN`)는 0을 반환하고 호출 측에서 `card.gaugeSinceDrawn`로 덮어쓴다.

6. **파워 카드는 자기 자신 공통 트리거 제외**: `applyPowerEffects = !result.keepOnField`이므로(`:187`), 파워 카드 사용 그 자체로는 "카드 사용 시" 공통 파워 효과(바람321 등)가 발동하지 않는다.

7. **재귀 가드**: 다른 카드 효과 발동(`_triggeringOtherCards`)·생애주기 트리거(`_inLifecycleTrigger`)·물225 빙결 재부여(`FrostTriggerReentrancy`)는 각각 별도 가드로 무한 재귀/루프를 차단한다.

8. **저주 피해는 고정 6**: 저주 카드 사용 시 `TakeDamage(6)` 하드코딩(`:149`). `CLEANSE_STATUS CURSE`는 리졸버가 직접 해제하지 않고 `NewBattleController.ClearAllCurses()`에 위임(`:1077`).

9. **반복 피해 안전장치**: `REPEAT_BY_RESOURCE_CONSUME_ALL`(연쇄 소진 반복)은 `safety = 200` 카운터로 무한 루프를 방지하고, 종료 후 음수가 된 `chainCount`를 0으로 보정한다 (`:930-938`).

10. **`EXHAUST_HAND`/`EXHAUST_CARD RANDOM`는 사용 카드 자신 제외**: 사용 중인 카드는 이미 `PullFromHand`로 패에서 빠진 상태라는 전제로 동작한다(주석, `:988`·`:1020`).
