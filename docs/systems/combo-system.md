# 콤보 시스템 명세

> 대상 코드(검증 기준):
> - `Assets/Script/Battle/ComboSkillDef.cs`
> - `Assets/Script/Battle/Card/ComboSkillData.cs`
> - `Assets/Script/Battle/Deck/ComboSkillDatabase.cs`
> - `Assets/Script/Battle/Deck/ComboEffectResolver.cs`
> - 소비처: `Assets/Script/Battle/NewBattleController.cs`, `Assets/Editor/NewBattleControllerEditor.cs`
> - 데이터: `Assets/Resources/ComboDB/ComboSkills.json`, `Assets/Resources/ComboDB/ComboEffects.json`

---

## 1. 개요 / 책임

각성(Awaken) 동안 플레이어가 입력한 카드 속성의 **최근 3장 슬라이딩 윈도우**를 보유 콤보와 매칭하고, 매칭 시 콤보를 큐에 적재한다. 각성 종료 시 큐에 쌓인 콤보를 **일괄 발동**하며, 각 콤보의 효과는 JSON으로 정의된 **데이터 드리븐 다중 효과**로 실행된다.

책임 분담:
- `ComboSkillDef` — 보유 콤보 1개의 런타임 정의 + 3장 시퀀스 매칭 판정(`ComboSkillDef.cs:26`, `Matches` `ComboSkillDef.cs:43`).
- `ComboSkillData` / `ComboEffectData` — JSON 한 줄에 대응하는 DTO(`ComboSkillData.cs:12`, `ComboSkillData.cs:35`).
- `ComboSkillDatabase` — Resources의 두 JSON을 로드/캐시하고, refComboId 집합 → 보유 `ComboSkillDef` 리스트를 빌드하는 static 로더(`ComboSkillDatabase.cs:13`).
- `ComboEffectResolver` — `ComboSkillDef.dbEffects`를 순서대로 해석/실행하는 간소화 효과 엔진(`ComboEffectResolver.cs:35`).
- 슬라이딩 윈도우 수집 · 큐잉 · 쿨다운 · 리졸버 호출은 `NewBattleController`가 담당(아래 4·6장).

핵심 데이터 흐름: **속성 3장 입력 → `Matches` 매칭 → 큐 적재(+쿨다운) → 각성 종료 시 큐 일괄 발동 → `ComboEffectResolver.Resolve`로 효과 실행.**

---

## 2. 위치 / 타입

| 파일 | namespace | 타입 | 종류 |
|---|---|---|---|
| `Assets/Script/Battle/ComboSkillDef.cs` | `Battle` | `ComboEffectType` (enum), `ComboSkillDef` | 데이터 클래스 `[System.Serializable]` (`ComboSkillDef.cs:25-26`) |
| `Assets/Script/Battle/Card/ComboSkillData.cs` | `Battle.Card` | `ComboSkillData`, `ComboSkillDataList`, `ComboEffectData`, `ComboEffectDataList` | 직렬화 DTO `[Serializable]` (`ComboSkillData.cs:12,25,35,58`) |
| `Assets/Script/Battle/Deck/ComboSkillDatabase.cs` | `Battle` | `ComboSkillDatabase` | `static class` 로더 (`ComboSkillDatabase.cs:13`) |
| `Assets/Script/Battle/Deck/ComboEffectResolver.cs` | `Battle` | `ComboResolveContext`, `ComboEffectResolver` | 일반 클래스(인스턴스화) (`ComboEffectResolver.cs:10,35`) |

- `ComboEffectType` 열거형: `Damage, Burn, Guard, Heal, Draw` (`ComboSkillDef.cs:7-14`). DB 콤보에서는 표시/시각효과 분기용으로만 쓰이고(아래 6장), 실제 효과는 `dbEffects`가 결정한다.
- `ComboSkillDef`는 레거시(Inspector 수동)와 DB 두 소스를 함께 표현한다(`ComboSkillDef.cs:16-24`).

---

## 3. 참조(의존)

- **Resources/ComboDB JSON 2종** — `ComboSkillDatabase`가 `Resources.Load<TextAsset>`로 읽음:
  - `ComboDB/ComboSkills` (`SKILLS_RESOURCE_PATH`, `ComboSkillDatabase.cs:15`)
  - `ComboDB/ComboEffects` (`EFFECTS_RESOURCE_PATH`, `ComboSkillDatabase.cs:16`)
  - 파싱은 `JsonUtility.FromJson<ComboSkillDataList>` / `<ComboEffectDataList>` (`ComboSkillDatabase.cs:167,191`).
- **`CardElement`** — 슬롯 속성 타입. `ComboSkillData`의 문자열 슬롯("FIRE"/"WATER"/"WIND"/"EARTH")을 `ParseElement`로 변환(`ComboSkillDatabase.cs:135-145`). 미인식 값은 `CardElement.Neutral`로 폴백.
- **`CardEffectData`와 동일 스키마** — `ComboEffectData`는 카드 효과와 같은 데이터 드리븐 필드 집합을 공유(`ComboSkillData.cs:30-55`의 주석/필드).
- **효과 실행 시 런타임 참조(`ComboResolveContext`)** — `EnemyController enemy`, `EnemyStat enemyStat`, `Player player`, `Deck.CardDeckSystem deck` (`ComboEffectResolver.cs:12-15`). `ComboEffectResolver`는 이들을 통해 데미지/상태/회복/방어/드로우/버리기를 적용(아래 5·6장).
- `ComboSkillDef.cs`는 `using Battle.Card;`로 `CardElement`/`ComboEffectData` 참조(`ComboSkillDef.cs:3`).

---

## 4. 피참조

### NewBattleController (`Assets/Script/Battle/NewBattleController.cs`)
- **보유 콤보 빌드**: `useComboDatabase`가 ON이면 `ComboSkillDatabase.BuildOwnedCombos(ownedComboRefIds)`로 `ownedComboSkills`를 채움(초기화 `NewBattleController.cs:280-284`; 런타임 갱신 `RefreshOwnedCombosRuntime` `:348-366`; 1개 추가 `AddOwnedCombo` `:369-382`).
- **슬라이딩 윈도우 수집**: `AddToComboSlot`이 `_comboInput`에 추가하고 4개 이상이면 맨 앞 제거(최근 3장 유지) (`:1079-1085`).
- **콤보 매칭/큐**: `TryActivateCombo`가 보유 콤보를 순회하며 `skill.Matches(_comboInput)` 판정 → 매칭 시 `_queuedComboSkills`에 적재하고 슬롯은 클리어하지 않음(`:1099-1119`). 매칭된 콤보에는 `_comboCooldown[i] = COMBO_REUSE_INPUT`(=5) 부여, 쿨다운>0인 콤보는 매칭 제외(`:1106,1112`).
- **각성 종료 시 일괄 발동**: 각성 종료 처리에서 `_queuedComboSkills`를 순회해 `ActivateComboSkill` 호출(`:999-1053`).
- **ComboEffectResolver 호출**: DB 콤보(`skill.fromDatabase`)는 `FillComboContext(skill)` 후 `_comboResolver.Resolve(skill, _comboCtx)` 실행(`ActivateComboSkill` `:1121-1130`). 리졸버/컨텍스트는 컨트롤러가 1개씩 보유(`:87-88`).
- **반복 공식 카운터 공급**: `_awakenComboCountThisEnd`(이번 각성 발동 콤보 총수, `:1010`)와 `_comboSelfUseCount`(refComboId별 발동 횟수, `:1015-1016`)를 `FillComboContext`에서 컨텍스트로 전달(`:1157-1174`).

### NewBattleControllerEditor (`Assets/Editor/NewBattleControllerEditor.cs`)
- `useComboDatabase`가 ON일 때 `ownedComboRefIds`를 숫자 배열 대신 **설명 포함 체크박스 목록**으로 그림(`DrawComboChecklist` `:37-121`).
- 표시용 콤보 목록은 `ComboSkillDatabase.ResetCache()` 후 `ComboSkillDatabase.BuildOwnedCombos(null)`(=전체 canonical 20종)으로 로드(`ReloadCombos` `:123-128`).
- 각 체크박스 라벨은 `[refComboId] ComboString()` + 툴팁 `descriptionKR`(`:90-99`). 플레이 중에는 "지금 적용" 버튼으로 `RefreshOwnedCombosRuntime()` 호출(`:110-118`).

---

## 5. 공개 API

### `ComboSkillDatabase` (static)

| 멤버 | 시그니처 | 설명 |
|---|---|---|
| `EnsureInit` | `void EnsureInit()` | 캐시 미로드 시 `LoadAll()` 실행(`:22-26`). |
| `ResetCache` | `void ResetCache()` | `_all/_byId/_effectsByRef`를 null로 비워 재로드 유도(에디터 재변환용) (`:29-34`). |
| `All` | `IReadOnlyList<ComboSkillData> All` | 전체 콤보 데이터(지연 로드) (`:36-39`). |
| `GetEffects` | `IReadOnlyList<ComboEffectData> GetEffects(int refComboId)` | refComboId의 효과 리스트(없으면 빈 배열) (`:41-47`). |
| `BuildOwnedCombos` | `List<ComboSkillDef> BuildOwnedCombos(IEnumerable<int> ownedRefIds = null)` | refComboId 집합 → 보유 `ComboSkillDef` 리스트 빌드. 자세한 동작 6장(`:58-124`). |
| `HasDamage` (static, 비공개 헬퍼) | `bool HasDamage(List<ComboEffectData>)` | 효과 중 `doAction=="DAMAGE"` 존재 여부(`:126-133`). |
| `ParseElement` | `CardElement ParseElement(string s)` | "FIRE/WATER/WIND/EARTH"→`CardElement`, 그 외 `Neutral`(`:135-145`). |

`BuildOwnedCombos`의 canonical/alias 처리(`:62-124`):
- refComboId별로 **모든 슬롯 순서 키**를 `acceptedOrders`에 모음(`ordersByRef`, `:63,73-78`) → 순서무관 매칭.
- refComboId별 **canonical 데이터** 선정: `id == refComboId`인 행 우선, 없으면 첫 등장 행(`canonicalByRef`, `:65,80-82`). 이 canonical 슬롯이 표시/`ComboString` 기준.
- `ownedRefIds`가 null이거나 빈 집합이면 **전체 canonical 보유**(`:85-90`, `:96`).
- 결과는 refComboId 오름차순 정렬(`:122`).

### `ComboSkillDef`

| 멤버 | 시그니처 | 설명 |
|---|---|---|
| 필드 | `string displayName` | 표시 이름(기본 "콤보스킬"). DB에서는 `comboName` 또는 `"콤보 {refId}"` 폴백(`ComboSkillDef.cs:28`, 세팅 `ComboSkillDatabase.cs:112-114`). |
| 필드 | `CardElement slot1/slot2/slot3` | 슬롯 속성(기본 모두 Fire) (`ComboSkillDef.cs:29-31`). |
| 필드 | `ComboEffectType effect` | 레거시 단일 효과 종류. DB에서는 시각효과 분기용(`ComboSkillDef.cs:32`). |
| 필드 | `int amount` | 레거시 단일 효과 수치(기본 10) (`ComboSkillDef.cs:33`). |
| 필드 | `bool fromDatabase` `[NonSerialized]` | DB 소스 여부(`ComboSkillDef.cs:36`). |
| 필드 | `int refComboId` `[NonSerialized]` | 효과 참조 ID(`ComboSkillDef.cs:37`). |
| 필드 | `string descriptionKR` `[NonSerialized]` | 한글 설명(툴팁용) (`ComboSkillDef.cs:38`). |
| 필드 | `List<ComboEffectData> dbEffects` `[NonSerialized]` | 실행할 효과 블록 목록(`ComboSkillDef.cs:39`). |
| 필드 | `HashSet<string> acceptedOrders` `[NonSerialized]` | 순서무관 매칭용 슬롯 순서 키 집합(`ComboSkillDef.cs:41`). |
| 메서드 | `bool Matches(IList<CardElement> input)` | 3장 이상일 때, DB면 `acceptedOrders.Contains(OrderKey(...))`, 레거시면 slot1/2/3 정순 비교(`ComboSkillDef.cs:43-51`). |
| 메서드(static) | `string OrderKey(CardElement a,b,c)` | `"a,b,c"` 키 생성(`ComboSkillDef.cs:53-54`). |
| 메서드 | `string ComboString()` | `"불-물-바람"`식 한글 약어(slot1/2/3 기준) (`ComboSkillDef.cs:56-59`; `Short` `:61-69`). |

### `ComboEffectResolver` + `ComboResolveContext`

| 멤버 | 시그니처 | 설명 |
|---|---|---|
| `Resolve` | `void Resolve(ComboSkillDef combo, ComboResolveContext ctx)` | `combo.dbEffects`를 순회하며 조건 통과 시 반복 횟수만큼 `ApplyEffect` 실행(`ComboEffectResolver.cs:39-52`). null 가드 후 `_lastDiscardedCount` 0 초기화. |
| (내부) `PassesCondition` | `bool` | `ifCond` 평가(`:56-70`). |
| (내부) `ResolveRepeatCount` | `int` | `repeat` 토큰 → 반복 횟수(`:74-89`). |
| (내부) `ApplyEffect` | `void` | 동사(`doAction`) 실행(`:93-153`). |
| (내부) `EvalAmount` / `TokenValue` / `EnemyStatus` | `int` | `formula`/토큰 평가(`:160-218`). |

`ComboResolveContext` 필드(`ComboEffectResolver.cs:10-27`):
- 참조: `EnemyController enemy`, `EnemyStat enemyStat`, `Player player`, `Deck.CardDeckSystem deck`.
- 카운터: `int usedFireCardCount`, `usedFragmentCardCount`, `playerChain`, `awakenComboCount`, `awakenFragmentExhausted`, `selfRepeatCount`.
- 위임: `Action<int> onGainChain` — `GAIN_STATUS CHAIN` 시 플레이어 연쇄 증가(`NewBattleController`가 `_ctx.chainCount += n`로 배선, `NewBattleController.cs:1173`).

### `ComboSkillData` / `ComboEffectData` (DTO 필드)
- `ComboSkillData`(`ComboSkillData.cs:12-22`): `int id`, `string slot1/slot2/slot3`, `int cooldown`, `string comboName`, `string description`, `int refComboId`.
- `ComboEffectData`(`ComboSkillData.cs:35-55`): `int refComboId`, `int index`, `string when`, `string ifCond`, `string doAction`, `string target`, `int amount`, `string formula`, `int hits`, `string hitFormula`, `string status`, `string cardFilter`, `string fromZone`, `string toZone`, `string select`, `string repeat`, `string extra`, `string runtimeKey`.
- 래퍼: `ComboSkillDataList.combos`, `ComboEffectDataList.effects`(`ComboSkillData.cs:25-28,58-61`).

---

## 6. 내부 동작 / 데이터 흐름

### 6.1 canonical / alias 구조
- **canonical(1000~1019)**: `id == refComboId`인 콤보. 효과가 여기 정의되고(`ComboEffects.json`의 refComboId 범위 1000~1019), 표시 슬롯/이름 기준이 됨(`ComboSkillDatabase.cs:80-82`).
- **alias(1020~1063)**: 같은 속성 조합의 다른 슬롯 순서. 자기 `id`는 다르지만 `refComboId`로 canonical의 효과를 공유(예: id 1020 → refComboId 1008, id 1021/1022 → refComboId 1009; `ComboSkills.json:204-232`).
- 빌드 시 같은 refComboId의 alias 슬롯 순서가 모두 `acceptedOrders`로 합쳐져(`ComboSkillDatabase.cs:73-78`), **하나의 보유 콤보가 모든 순서 변형을 인식**한다.

### 6.2 순서무관 매칭
- `Matches`(`ComboSkillDef.cs:43-51`): DB 콤보면 `_comboInput`의 최근 3장으로 만든 `OrderKey`가 `acceptedOrders`에 있는지로 판정(순서무관). 레거시면 slot1/2/3 정순서 일치만.
- 슬라이딩 윈도우는 `NewBattleController._comboInput`(최근 3장, `NewBattleController.cs:1081-1082`). 매칭 후에도 윈도우를 비우지 않아(`:1113`) 다음 입력과 겹쳐 연속 매칭 가능.

### 6.3 큐잉 · 쿨다운 · 일괄 발동
- 매칭된 콤보는 즉시 발동하지 않고 `_queuedComboSkills`에 적재, `_comboCooldown[i]=5`(`COMBO_REUSE_INPUT`) 부여(`NewBattleController.cs:1111-1112`). 입력 1회마다 `TickComboCooldowns`로 1씩 감소(`:1088-1092`).
- **각성 종료 시** 큐를 일괄 발동(`:999-1053`). 발동 직전 이번 각성 총 콤보 수(`_awakenComboCountThisEnd`)와 refComboId별 발동 횟수(`_comboSelfUseCount`)를 집계해 반복 공식에 공급(`:1010,1015-1016`).
- 참고: DTO의 `cooldown`(JSON, 기본 5)은 기획상 "재사용까지 필요한 각성 입력 횟수"(`ComboSkillData.cs:18`)지만, 런타임 쿨다운은 코드 상수 `COMBO_REUSE_INPUT`(=5)를 사용한다(`BuildOwnedCombos`는 `cooldown`을 `ComboSkillDef`로 옮기지 않음).

### 6.4 콤보 효과 실행(간소화판)
`ComboEffectResolver.Resolve`는 단일 적 프로토타입에 맞춘 간소화 엔진이다(`ComboEffectResolver.cs:29-34`). `target`의 `ALL_ENEMIES/RANDOM_ENEMY/ENEMY`는 모두 현재 적 1체에 적용(파싱하지 않음).

**지원 동사**(`ApplyEffect` `:99-153`):
| 동사 | 동작 |
|---|---|
| `DAMAGE` | `enemy.TakeDamage(amount)`를 `hits`회(최소 1) (`:101-104`) |
| `APPLY_STATUS` | `enemy.AddStatus(status소문자, amount)`. 단 `status=="ATTACK_POWER"`는 미지원 경고 후 스킵(`:106-113`) |
| `GAIN_STATUS` | `status=="CHAIN"`이면 `onGainChain(amount)`, 아니면 `player.AddStatus(...)` (`:115-123`) |
| `HEAL` | `player.Heal(amount)` (`:125-127`) |
| `GAIN_BLOCK` | `player.AddGuard(amount)` (`:129-131`) |
| `DRAW_CARD` | `deck.Draw(max(0,amount))` (`:133-135`) |
| `DISCARD_HAND` | `_lastDiscardedCount = deck.DiscardAllFromHand()` (`:137-139`) |

**미지원 동사** `MOVE_CARD / MODIFY_ENEMY_STAT / BUFF_NEXT_COMBO / SKIP_COOLDOWN` → `WarnSkip` 경고 후 스킵(`:142-147`, `:155-156`). 그 외 미정의 동사는 "미구현 동사" 경고만(`:149-151`).

**조건(`ifCond`)**(`PassesCondition` `:56-70`): 빈 값=통과. `ENEMY_KILLED_BY_THIS_COMBO`는 처치 추적 미구현이라 보수적으로 false(스킵). 그 외 미지정 조건은 경고 후 통과.

### 6.5 Repeat / Formula
- **Repeat**(`ResolveRepeatCount` `:74-89`, 각 `Mathf.Max(1, ...)`): `REPEAT_BY_AWAKEN_COMBO_COUNT`→`awakenComboCount`, `REPEAT_BY_RESOURCE_CONSUME_ALL`→`playerChain`, `REPEAT_BY_AWAKEN_FRAGMENT_EXHAUSTED`→`awakenFragmentExhausted`, `REPEAT_BY_SELF_AWAKEN_USE_COUNT`→`selfRepeatCount`. 빈 값=1회, 그 외=경고 후 1회.
- **Formula**(`EvalAmount` `:160-194`): 빈 값이면 `eff.amount`.
  - `ENEMY_STATUS_VALUE:BURN` → 적 상태치 조회(`EnemyStatus` `:212-218`).
  - `MAX_HP*0.1` → `floor(player.maxHp * 배수)` (`:174-180`).
  - `기본값+TOKEN`(예 `0+USED_FIRE_CARD_COUNT`) → `좌측정수 + TokenValue(우측)` (`:182-189`).
  - 단일 토큰 → `TokenValue`.
- **Token**(`TokenValue` `:196-210`): `USED_FIRE_CARD_COUNT`, `USED_FRAGMENT_CARD_COUNT`, `PLAYER_BLOCK`(=`round(player.guard)`), `PLAYER_CHAIN`, `LAST_DISCARDED_COUNT`. 숫자면 파싱, 미인식은 경고 후 `fallback`(=`eff.amount`).
- `DISCARD_HAND` 후 `LAST_DISCARDED_COUNT`를 쓰는 연계 가능(`_lastDiscardedCount`, `:37,138,204`). `Resolve` 진입 시 0으로 초기화(`:42`).

---

## 7. 데이터

### 7.1 ComboSkills.json (`Assets/Resources/ComboDB/ComboSkills.json`)
루트 `{ "combos": [ ... ] }`(`ComboSkillDataList`). 각 원소 = `ComboSkillData`.
```json
{
  "id": 1000,            // ComboID (1000~1063)
  "slot1": "FIRE",       // FIRE/WATER/WIND/EARTH
  "slot2": "FIRE",
  "slot3": "FIRE",
  "cooldown": 5,         // 기획상 재사용 입력 횟수(런타임은 COMBO_REUSE_INPUT 사용)
  "comboName": "",       // 표시 이름(현재 전부 빈 값)
  "description": "모든 적에게 피해 20, 모든 적에게 화상을 10 부여한다.",
  "refComboId": 1000     // 효과 참조 ID (1000~1019)
}
```

### 7.2 ComboEffects.json (`Assets/Resources/ComboDB/ComboEffects.json`)
루트 `{ "effects": [ ... ] }`(`ComboEffectDataList`). 각 원소 = `ComboEffectData`. `refComboId`별로 묶이고 `index` 오름차순 실행(`ComboSkillDatabase.cs:196-206`).
```json
{
  "refComboId": 1000, "index": 1,
  "when": "ON_AWAKEN", "ifCond": "",
  "doAction": "DAMAGE", "target": "ALL_ENEMIES",
  "amount": 20, "formula": "",
  "hits": 0, "hitFormula": "",
  "status": "", "cardFilter": "",
  "fromZone": "", "toZone": "", "select": "",
  "repeat": "", "extra": "", "runtimeKey": ""
}
```
- `when`은 전부 `ON_AWAKEN`(각성 종료 일괄 발동 시점과 일치). 코드에서는 `when`을 분기 조건으로 읽지 않는다.

### 7.3 ID 체계 (현재 데이터 기준)
- ComboSkills.json `id`: 1000~1063 연속 = **64개**.
  - **canonical 20개**: id 1000~1019 (`id == refComboId`).
  - **alias 44개**: id 1020~1063 (`refComboId`는 1000~1019 중 하나를 가리킴).
- ComboEffects.json: 효과 블록 **34행**, refComboId 1000~1019(20종 모두 효과 보유; 일부 refComboId는 1행, 일부는 2~3행). 예: 1000→2행(DAMAGE+APPLY_STATUS), 1010→3행.

---

## 8. 주의 / 엣지케이스

- **미지원 동사 경고 스킵**: `MOVE_CARD / MODIFY_ENEMY_STAT / BUFF_NEXT_COMBO / SKIP_COOLDOWN` 및 `APPLY_STATUS:ATTACK_POWER`는 단일 적/프로토타입 범위 밖이라 실행되지 않고 `Debug.LogWarning`만 남긴다(`ComboEffectResolver.cs:109,142-147,155-156`). 해당 효과를 가진 alias/콤보는 일부 효과가 누락된 채 발동된다.
- **`ENEMY_KILLED_BY_THIS_COMBO` 조건 항상 스킵**: 처치 추적 미구현으로 false 반환(`:63-65`). 이 조건은 `SKIP_COOLDOWN` 전용이라 진행에는 영향이 없다고 주석에 명시.
- **ComboName 미정**: 현재 JSON의 `comboName`이 전부 빈 문자열이라 표시 이름은 `"콤보 {refId}"`로 폴백(`ComboSkillDatabase.cs:112-114`). 레거시 기본값은 "콤보스킬"(`ComboSkillDef.cs:28`).
- **단일 적 가정**: `target`의 `ALL_ENEMIES/RANDOM_ENEMY/ENEMY` 구분 없이 모두 `ctx.enemy` 1체에 적용(`ComboEffectResolver.cs:31-32`). 다적 전투로 확장 시 리졸버 수정 필요.
- **쿨다운 출처 이원화**: JSON `cooldown`(5) ≠ 런타임 사용값. 런타임은 상수 `COMBO_REUSE_INPUT`(=5)만 사용하며 JSON 값은 빌드에 반영되지 않는다(6.3 참고).
- **슬라이딩 윈도우 비-클리어**: 매칭 후 입력 슬롯을 비우지 않아 직전 2장이 다음 매칭에 재사용된다(`NewBattleController.cs:1113`). 의도된 동작이나, 동일 콤보 연속 매칭은 쿨다운(5입력)으로 억제된다.
- **각성 종료 일괄 발동**: 콤보 효과는 매칭 즉시가 아니라 각성 종료 시 큐로 한꺼번에 실행되므로, 반복 공식(`awakenComboCount`/`selfRepeatCount`)은 그 시점의 집계값을 사용(`NewBattleController.cs:1009-1016`).
- **`effect` 필드 의미 축소(DB)**: DB 콤보의 `effect`는 데미지 포함 시 `Damage`, 아니면 `Draw`로 설정되며(`ComboSkillDatabase.cs:116`) 실제 효과가 아니라 EndAwaken 시각효과(셰이크/분산) 분기에만 쓰인다.
- **로드 실패 처리**: ComboSkills.json 부재 시 `LogError` 후 빈 상태, ComboEffects.json 부재 시 `LogWarning` 후 "효과 없는 콤보"로 로드(`ComboSkillDatabase.cs:158-163,182-187`).
