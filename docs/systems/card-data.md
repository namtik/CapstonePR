# 카드 데이터 (CardDatabase / CardData) 명세

> 대상 코드: `Assets/Script/Battle/Card/CardDatabase.cs`, `CardData.cs`, `CardEffectData.cs`, `CardEnums.cs`
> 데이터: `Assets/Resources/CardDB/Cards.json`, `Assets/Resources/CardDB/CardEffects.json`
> 본 문서는 위 코드/데이터를 직접 읽어 작성됨. (추측 없음, file:line 표기)

---

## 1. 개요 / 책임

카드 시스템의 **정적 데이터 계층**. 다음을 담당한다.

- **JSON 로드**: `Resources/CardDB/Cards.json`(카드 정의) + `Resources/CardDB/CardEffects.json`(효과 블록)을 `JsonUtility`로 역직렬화 (`CardDatabase.cs:184-262`).
- **카드 모델 제공**: 정적 정의 `CardData`, 런타임 인스턴스 `CardInstance`, 효과 데이터 `CardEffectData` (`CardData.cs`, `CardEffectData.cs`).
- **조회 API**: ID로 카드 조회(`GetById`), 전체 목록(`All`), 카드별 효과 목록(`GetEffects`) (`CardDatabase.cs:37-54`).
- **시작 덱 구성**: 기획 8장 시작 덱(`DefaultStartingDeckEntries`)과 테스트용 22장 덱(`DefaultPrototypeDeckEntries`)을 정의하고, `DeckEntry` 리스트 → `CardInstance` 리스트로 변환(`InstantiateDeck`) (`CardDatabase.cs:60-145`).
- **특수 카드 생성**: 파편 카드(`CreateFragmentInstance`)·무속성 더미 카드(`CreateNeutralFillerInstance`)를 임시(transient) 인스턴스로 생성 (`CardDatabase.cs:151-172`).

데이터 출처: 기획자가 `Card_DB.xlsx` → `Tools/ConvertCardDB.py`(주석상 `Tools/ConvertCardDB.py`)로 JSON을 갱신하면 게임에 반영 (`CardDatabase.cs:8-9`). 효과의 **실행**은 본 클래스가 아니라 `CardEffectResolver`가 id로 분기하여 처리한다 (`CardDatabase.cs:10`, `CardData.cs:8`).

---

## 2. 위치 / 타입

- **경로**: `Assets/Script/Battle/Card/`
- **namespace**: `Battle.Card` (4개 파일 모두 동일)

| 타입 | 종류 | 파일 | 역할 |
|---|---|---|---|
| `CardDatabase` | **static class** | `CardDatabase.cs:12` | JSON 로드 · 조회 · 덱/파편 생성 진입점 |
| `CardData` | `[Serializable]` 데이터 클래스 | `CardData.cs:12` | 카드 정적 정의 |
| `CardInstance` | 일반 클래스 | `CardData.cs:72` | 런타임 카드 인스턴스(덱/패/더미 순회) |
| `CardEffectData` | `[Serializable]` 데이터 클래스 | `CardEffectData.cs:12` | 효과 블록 1개(CardEffects.json 한 줄) |
| `CardEffectDataList` | `[Serializable]` | `CardEffectData.cs:35` | `JsonUtility` 파싱용 래퍼 |
| `CardElement` / `CardType` / `CardRarity` / `CardTarget` | **enum** | `CardEnums.cs` | 속성 / 타입 / 등급 / 드롭 타깃 |

`CardDatabase` 내부 private DTO: `CardJsonList`(`CardDatabase.cs:311`), `CardJson`(`CardDatabase.cs:317`) — Cards.json 스키마와 1:1.

---

## 3. 참조 (의존)

- `UnityEngine.Resources` — `Resources.Load<TextAsset>(...)`로 JSON 텍스트 에셋 로드 (`CardDatabase.cs:189, 233`).
- `UnityEngine.JsonUtility` — JSON → DTO 역직렬화 (`CardDatabase.cs:200, 243`).
- `UnityEngine.Debug` — 로드 실패/알 수 없는 값 경고 로깅 (`CardDatabase.cs:133, 192, 204, 236, 247, 276, 290, 304`).
- `UnityEngine.Random` — 파편 무작위 선택 (`CardDatabase.cs:154`).

리소스 경로 상수: `CARDS_RESOURCE_PATH = "CardDB/Cards"`, `EFFECTS_RESOURCE_PATH = "CardDB/CardEffects"` (`CardDatabase.cs:24-25`). (`.json` 확장자 없이 Resources 폴더 기준 상대경로.)

---

## 4. 피참조 (이 시스템을 쓰는 쪽)

| 호출처 | 사용 API | file:line |
|---|---|---|
| `CardEffectResolver` | `GetEffects` (효과 실행 분기), `CreateFragmentInstance`, `FragmentIds` | `Deck/CardEffectResolver.cs:156, 199, 203, 231, 256, 295, 354, 1222, 1229, 1234, 1241, 1243` |
| `NewBattleController` | `BuildDefaultPrototypeDeck`, `CreateFragmentInstance`(2종), `FragmentIds`, `CreateNeutralFillerInstance` | `NewBattleController.cs:276, 503, 818, 820, 832, 1237` |
| `RunDeckState` | `DefaultStartingDeckEntries`, `InstantiateDeck`, `GetById` | `Run/RunDeckState.cs:101, 110, 117` |
| `CardRewardUI` | `EnsureInit`, `All` | `UI/CardRewardUI.cs:81, 84` |
| `EnemyDisruptionAI` | `GetEffects`(Classify), `GetById` | `AI/EnemyDisruptionAI.cs:44, 81` |
| `BattleTestController` | `DefaultPrototypeDeckEntries`, `InstantiateDeck` | `BattleTestController.cs:192, 194, 202` |

> 참고: 실제 게임 런(SampleScene)의 시작 덱은 `RunDeckState`가 `DefaultStartingDeckEntries`(8장)로 구성. `DefaultPrototypeDeckEntries`(22장)는 테스트 경로(`BattleTestController`, `NewBattleController.BuildDefaultPrototypeDeck`)에서만 사용.

---

## 5. 공개 API

### 5.1 `CardDatabase` (static)

| 멤버 | 시그니처 | 반환/효과 | file:line |
|---|---|---|---|
| `EnsureInit` | `void EnsureInit()` | `_byId == null`일 때만 `LoadAll()` 1회 실행(지연 로드, 멱등) | `CardDatabase.cs:31-35` |
| `GetById` | `CardData GetById(int id)` | ID 매칭 `CardData`, 없으면 **null** (`EnsureInit` 자동 호출) | `CardDatabase.cs:37-41` |
| `All` | `IReadOnlyList<CardData> All` (get) | 전체 카드 목록(로드 순서) | `CardDatabase.cs:43-46` |
| `GetEffects` | `IReadOnlyList<CardEffectData> GetEffects(int cardId)` | 카드 효과 목록, 없으면 **빈 배열**(`Array.Empty`) | `CardDatabase.cs:48-54` |
| `DefaultPrototypeDeckEntries` | `List<DeckEntry> DefaultPrototypeDeckEntries()` | 테스트 덱(불/물/바람 일부, 합계 22장) | `CardDatabase.cs:74-99` |
| `DefaultStartingDeckEntries` | `List<DeckEntry> DefaultStartingDeckEntries()` | 기획 0.6v 시작 덱(8장) | `CardDatabase.cs:105-118` |
| `InstantiateDeck` | `List<CardInstance> InstantiateDeck(IList<DeckEntry> entries)` | `count`만큼 `CardInstance` 복제. `count<=0` 스킵, 미지 ID는 경고 후 스킵 | `CardDatabase.cs:121-140` |
| `BuildDefaultPrototypeDeck` | `List<CardInstance> BuildDefaultPrototypeDeck()` | `InstantiateDeck(DefaultPrototypeDeckEntries())` | `CardDatabase.cs:142-145` |
| `CreateFragmentInstance` | `CardInstance CreateFragmentInstance()` | `FragmentIds` 중 무작위 1개를 transient 인스턴스로 생성 | `CardDatabase.cs:151-156` |
| `CreateFragmentInstance` | `CardInstance CreateFragmentInstance(int fragmentId)` | 지정 ID 파편(transient). 미존재 시 **null** | `CardDatabase.cs:158-163` |
| `CreateNeutralFillerInstance` | `CardInstance CreateNeutralFillerInstance()` | 무속성 더미(ID 500) transient 인스턴스. 미존재 시 **null** | `CardDatabase.cs:166-172` |

**상수 / 필드**

| 이름 | 값 | file:line |
|---|---|---|
| `FRAGMENT_1` | `501` | `CardDatabase.cs:15` |
| `FRAGMENT_2` | `502` | `CardDatabase.cs:16` |
| `FRAGMENT_3` | `503` | `CardDatabase.cs:17` |
| `NEUTRAL_FILLER` | `500` | `CardDatabase.cs:20` |
| `FragmentIds` | `{ 501, 502, 503 }` (readonly int[]) | `CardDatabase.cs:22` |

**struct `DeckEntry`** (`[Serializable]`, `CardDatabase.cs:60-71`)

| 필드 | 타입 | 설명 |
|---|---|---|
| `cardId` | `int` | 카드 ID |
| `count` | `int` | 보유 수량 |

생성자: `DeckEntry(int cardId, int count)`.

### 5.2 `CardData` (`CardData.cs:12-69`)

| 필드 | 타입 | 설명 | file:line |
|---|---|---|---|
| `id` | `int` | 카드 ID | `CardData.cs:14` |
| `displayName` | `string` | 표시명(JSON `name`에서 매핑) | `CardData.cs:15` |
| `element` | `CardElement` | 속성 | `CardData.cs:16` |
| `type` | `CardType` | 타입 | `CardData.cs:17` |
| `gauge` | `int` | 사용 게이지 비용 | `CardData.cs:18` |
| `description` | `string` | 설명 | `CardData.cs:19` |
| `rarity` | `CardRarity` | 등급(보상 가중치 등) | `CardData.cs:22` |
| `tags` | `string[]` | BASIC/EXHAUST/ONE_TIME/POWER/FRAGMENT/NO_COMBO_SLOT/TEMPORARY_ON_CREATE 등 | `CardData.cs:25` |
| `comboSlot` | `bool` | 사용 시 콤보 슬롯에 입력되는지(파편/무속성은 false) | `CardData.cs:28` |
| `effectName` | `string` | 아트 바인딩용(EffectName 컬럼) | `CardData.cs:31` |
| `skillImg` | `string` | 아트 바인딩용(SkillImg 컬럼) | `CardData.cs:34` |

> 주의: JSON 키는 `name`이지만 모델 필드는 `displayName`. 생성자(`CardDatabase.cs:212-224`)에서 `raw.name → displayName`으로 매핑된다.

**계산 프로퍼티 / 메서드**

| 멤버 | 의미 | file:line |
|---|---|---|
| `IsFragment` | `element == CardElement.Fragment` | `CardData.cs:57` |
| `IsNeutral` | `element == CardElement.Neutral` | `CardData.cs:58` |
| `BypassComboSlot` | `!comboSlot` (콤보 슬롯 미진입 카드) | `CardData.cs:60` |
| `HasTag(string tag)` | 대소문자 무시 태그 보유 검사(`OrdinalIgnoreCase`), null/빈 태그는 false | `CardData.cs:62-68` |

생성자: 기본 생성자 + 전체 인자 생성자(`tags=null→Array.Empty`, `comboSlot=true`, `rarity=Normal` 기본값) (`CardData.cs:36-55`).

### 5.3 `CardInstance` (`CardData.cs:72-101`)

런타임 카드 1장. (덱/패/더미를 순회.)

| 멤버 | 타입 | 설명 | file:line |
|---|---|---|---|
| `data` | `CardData` | 참조하는 정적 정의 | `CardData.cs:74` |
| `transient` | `bool` | 이번 전투 한정 임시 카드(파편). true면 전투 종료 시 소실 | `CardData.cs:76` |
| `cursed` | `bool` | 저주 카드. 사용 시 플레이어가 피해 | `CardData.cs:78` |
| `gaugeSinceDrawn` | `int` | 패 진입 후 진행 게이지 수(GAUGE_SINCE_DRAWN, 땅6/땅16) | `CardData.cs:81` |
| `justDrawn` | `bool` | 드로우 직후 미사용 상태(USED_IMMEDIATELY_AFTER_DRAW, 바람11/310) | `CardData.cs:84` |
| `selfUseCount` | `int` | 누적 사용 횟수(SELF_USE_COUNT, 바람314) | `CardData.cs:87` |

생성자: `CardInstance(CardData data, bool transient = false)` — `gaugeSinceDrawn=0`, `justDrawn=false` 초기화 (`CardData.cs:89-95`). (참고: `cursed`/`selfUseCount`는 생성자에서 명시 초기화하지 않음 → C# 기본값 false/0.)

**위임 프로퍼티**: `Id → data.id`, `Element → data.element`, `Type → data.type`, `Gauge → data.gauge` (`CardData.cs:97-100`).

### 5.4 `CardEffectData` (`CardEffectData.cs:12-32`)

CardEffects.json 한 줄 = 효과 블록 1개. 한 카드는 `index` 1,2,…로 여러 효과 보유 가능 (`CardEffectData.cs:7-8`). **Phase 1: 데이터로 로드해 메타데이터로 보존, 인터프리터는 단계적 확장** (`CardEffectData.cs:9`).

| 필드 | 타입 | 필드 | 타입 |
|---|---|---|---|
| `cardId` | `int` | `hitFormula` | `string` |
| `index` | `int` | `status` | `string` |
| `when` | `string` | `cardFilter` | `string` |
| `ifCond` | `string` | `fromZone` | `string` |
| `doAction` | `string` | `toZone` | `string` |
| `target` | `string` | `select` | `string` |
| `amount` | `int` | `repeat` | `string` |
| `formula` | `string` | `extra` | `string` |
| `hits` | `int` | `runtimeKey` | `string` |

### 5.5 `CardEnums` (`CardEnums.cs`)

| enum | 멤버(선언 순서) | file:line |
|---|---|---|
| `CardElement` | `Fire, Water, Wind, Earth, Neutral, Fragment` | `CardEnums.cs:3-11` |
| `CardType` | `Attack, Skill, Power` | `CardEnums.cs:13-18` |
| `CardRarity` | `Normal, Rare, Epic` | `CardEnums.cs:21-26` |
| `CardTarget` | `Enemy, Player, Self` (카드 사용 시 드롭 타깃) | `CardEnums.cs:28-34` |

---

## 6. 내부 동작

### 6.1 로드 흐름

`EnsureInit()` → (`_byId == null`이면) `LoadAll()` → `LoadCards()` + `LoadEffects()` (`CardDatabase.cs:31-35, 178-182`).

**`LoadCards()`** (`CardDatabase.cs:184-228`)
1. `_all`/`_byId` 초기화.
2. `Resources.Load<TextAsset>("CardDB/Cards")`. null이면 **LogError 후 return**(빈 DB).
3. `JsonUtility.FromJson<CardJsonList>(...)`. 예외 시 LogError 후 return.
4. `payload?.cards == null`이면 return.
5. 각 `CardJson` → `CardData` 생성. 이때 `ParseElement/ParseType/ParseRarity`로 문자열→enum 변환, `tags ?? Array.Empty`, `effectName ?? ""`, `skillImg ?? ""` 널 가드. `_all.Add` + `_byId[id]=card`(중복 ID 시 마지막이 덮어씀).

**`LoadEffects()`** (`CardDatabase.cs:230-262`)
1. `_effectsByCard` 초기화.
2. `Resources.Load<TextAsset>("CardDB/CardEffects")`. null이면 **LogWarning**(Cards와 달리 Error 아님) 후 효과 없이 진행.
3. 파싱 → `eff.cardId` 키로 그룹핑하여 `Dictionary<int, List<CardEffectData>>` 구성(JSON 등장 순서 유지).

### 6.2 문자열 → enum 파싱 (대소문자 무시, 미지값 fallback + 경고)

| 메서드 | 입력 매핑 | fallback | file:line |
|---|---|---|---|
| `ParseElement` | FIRE/WATER/WIND/EARTH/FRAGMENT/NEUTRAL | `Neutral` (빈 문자열/미지값) | `CardDatabase.cs:264-279` |
| `ParseType` | ATTACK/SKILL/POWER | `Skill` (빈 문자열/미지값) | `CardDatabase.cs:281-293` |
| `ParseRarity` | NORMAL/RARE/EPIC | `Normal` (빈 문자열/미지값) | `CardDatabase.cs:295-307` |

모두 `raw.Trim().ToUpperInvariant()` 비교. 미지값은 LogWarning 후 fallback.

### 6.3 기본 시작 덱

- **`DefaultStartingDeckEntries`** (기획 0.6v, 8장) — 4속성 × (기본 공격 + 기본 방어) (`CardDatabase.cs:105-118`):
  - 불 100/101, 물 200/201, 바람 300/301, 땅 400/401 (각 1장).
  - 주석: 기본 공격 게이지1/피해5, 기본 방어 게이지1/방어도3.
- **`DefaultPrototypeDeckEntries`** (테스트 전용, 합계 22장) — 불 5장(106·107·115·116·118), 물 9장(203×2·207·211·216·218·220·221), 바람 8장(308×2·312·313×2·318·320) (`CardDatabase.cs:74-99`). 땅 미포함.

---

## 7. 데이터

### 7.1 `Cards.json` 스키마 (`CardDatabase.cs:316-330`의 `CardJson`과 1:1)

```json
{
  "cards": [
    {
      "id": 100,
      "name": "불 기본 공격",
      "element": "FIRE",
      "type": "ATTACK",
      "gauge": 1,
      "rarity": "Normal",
      "tags": ["BASIC"],
      "comboSlot": true,
      "effectName": "Fire_ATK",
      "skillImg": "100Img",
      "description": "피해를 5 준다."
    }
  ]
}
```

루트 객체에 `cards` 배열. (Cards.json:1-15 실제 예시 확인.)

### 7.2 `CardEffects.json` 스키마 (`CardEffectData`와 1:1)

```json
{
  "effects": [
    {
      "cardId": 100, "index": 1,
      "when": "ON_USE", "ifCond": "",
      "doAction": "DAMAGE", "target": "ENEMY",
      "amount": 5, "formula": "",
      "hits": 0, "hitFormula": "",
      "status": "", "cardFilter": "",
      "fromZone": "", "toZone": "",
      "select": "", "repeat": "",
      "extra": "", "runtimeKey": ""
    }
  ]
}
```

루트 객체에 `effects` 배열. 예: 100=DAMAGE/ENEMY/5, 101=GAIN_BLOCK/PLAYER/3 (CardEffects.json:3-42).

### 7.3 카드 ID 체계 (Cards.json 실측, 총 **112장**)

| 속성 | ID 범위 | 장수 |
|---|---|---|
| FIRE(불) | 100 ~ 126 | 27 |
| WATER(물) | 200 ~ 226 | 27 |
| WIND(바람) | 300 ~ 326 | 27 |
| EARTH(땅) | 400 ~ 426 | 27 |
| NEUTRAL(무속성 더미) | 500 | 1 |
| FRAGMENT(파편) | 501 ~ 503 | 3 |

- 각 속성 `x00` = 기본 공격(ATTACK), `x01` = 기본 방어(SKILL).
- **NEUTRAL 500** (`Cards.json:1407-1419`): `type=SKILL`, `comboSlot=false`, `tags=["NO_COMBO_SLOT","TEMPORARY_ON_CREATE"]`, `effectName=""`, 설명 "효과 X". → 효과 없는 더미, 방해 행동용.
- **FRAGMENT 501/502/503** (`Cards.json:1420-1458`): 모두 `element=FRAGMENT`, `comboSlot=false`, `tags=["EXHAUST","FRAGMENT","NO_COMBO_SLOT","TEMPORARY_ON_CREATE"]`.
  - 501 파편스킬(SKILL, `Earth_DEF`): 소멸, 방어도 5.
  - 502 파편공격(ATTACK, `Earth_ATK`): 소멸, 피해 8 + 드로우 1.
  - 503 파편공격(ATTACK, `Earth_ATK`): 소멸, 피해 10 + 방어도 5.

### 7.4 등급 분포 (Cards.json 전체 112장 실측, `rarity` 키 카운트)

| 등급 | 장수 |
|---|---|
| Normal | 52 |
| Rare | 40 |
| Epic | 20 |
| 합계 | 112 |

---

## 8. 주의 / 엣지케이스

- **테스트 덱 vs 기획 덱 혼동 주의**: `DefaultPrototypeDeckEntries()`(22장, 불/물/바람만, 테스트 전용)와 `DefaultStartingDeckEntries()`(8장, 4속성 기본 공·방, 실제 게임 런)은 별개. 실제 런(`RunDeckState`)은 8장 덱으로 시작하고, 테스트 경로(`BattleTestController`, `NewBattleController.BuildDefaultPrototypeDeck`)만 22장 덱을 쓴다 (`CardDatabase.cs:73-118`).
- **EnsureInit 지연 로드**: 모든 조회/생성 API가 진입 시 `EnsureInit()`를 호출하므로 명시적 초기화는 선택적. 단 한 번만 로드되며(`_byId != null` 가드), 런타임 중 JSON을 다시 읽지 않는다 → 게임 실행 중 JSON 갱신은 반영되지 않음. 강제 재로드 API 없음 (`CardDatabase.cs:31-35`).
- **JSON 재임포트 시 덮어씀**: `Card_DB.xlsx` → `Tools/ConvertCardDB.py` 재실행 시 Cards.json/CardEffects.json이 통째로 갱신됨(코드 주석 `CardDatabase.cs:8-9, 192-193`). 모델/코드는 수정 불필요하나 ID·태그·스키마 키가 바뀌면 파싱 결과가 달라진다.
- **로드 실패 처리 비대칭**: `Cards.json` 누락은 **LogError**(`CardDatabase.cs:192`), `CardEffects.json` 누락은 **LogWarning**(`CardDatabase.cs:236`) — 효과 데이터는 없어도 진행. 어느 쪽이든 빈 컬렉션으로 남아 `GetById`는 null, `GetEffects`는 빈 배열 반환.
- **미지 enum 문자열은 throw 안 함**: `ParseElement/Type/Rarity` 모두 미지값을 fallback(Neutral/Skill/Normal) + 경고로 흡수 (`CardDatabase.cs:264-307`). 오타가 조용히 기본값으로 처리될 수 있음.
- **`GetById` null 가능성**: 미존재 ID는 null 반환(`CardDatabase.cs:40`). `InstantiateDeck`는 이를 감지해 경고 후 스킵하지만(`CardDatabase.cs:131-134`), 호출처가 직접 `GetById` 결과를 쓰면 null 가드 필요.
- **`CreateFragmentInstance(int)` / `CreateNeutralFillerInstance` null 반환**: ID가 DB에 없으면 null (`CardDatabase.cs:161, 169`). 파편/무속성 카드가 Cards.json에서 빠지면 생성이 조용히 실패.
- **transient 인스턴스**: 파편·무속성 더미는 `transient: true`로 생성되어 전투 종료 시 소실 대상(`CardDatabase.cs:155, 162, 170`; 의미는 `CardData.cs:76`).
- **JSON `name` ↔ 모델 `displayName` 키 불일치**: DTO는 `name`, 모델은 `displayName`. 매핑은 생성자에서만 일어나므로 모델을 직접 직렬화할 때 키가 달라짐 주의 (`CardData.cs:15`, `CardDatabase.cs:213, 318`).
- **중복 ID**: `_byId[card.id] = card`는 덮어쓰기 — Cards.json에 같은 ID가 둘이면 마지막 항목만 조회됨(`_all`에는 둘 다 존재) (`CardDatabase.cs:226`).
