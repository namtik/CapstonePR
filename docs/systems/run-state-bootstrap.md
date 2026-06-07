# 런 상태 & 부트스트랩 명세

> 대상 코드
> - `Assets/Script/Battle/Run/RunDeckState.cs`
> - `Assets/Script/Battle/NewBattleSystemBootstrap.cs`
>
> 본 명세는 위 두 파일을 직접 읽어 작성했으며, 인용 위치는 `파일:line` 형식으로 표기한다.

---

## 1. 개요 / 책임

| 클래스 | 책임 |
|---|---|
| **RunDeckState** | **런(run) 단위 보존소.** 전투 간 플레이어 덱(`DeckEntry` = 카드ID+수량)을 유지하고, 매 전투마다 새 `CardInstance`를 생성해 주입한다. 더불어 영구 공격력 보너스와 적 AI용 "런 플레이어 프로파일"(f4 방어성향 / f7 피버사이클)을 누적·보존한다. (`RunDeckState.cs:7-14`) |
| **NewBattleSystemBootstrap** | **SampleScene(본 게임)에서 신규 카드 전투 시스템을 "항상" 활성화하는 부트스트랩.** `Awake` 단계에서 레거시 전투 오브젝트를 차단하고 `NewBattleController`/유물 시스템/`RunDeckState`를 보장한 뒤, `Start`에서 시작 유물을 지급한다. (`NewBattleSystemBootstrap.cs:8-19`) |

핵심 설계 의도:
- `CardInstance`는 런타임 상태를 가지므로 재사용 시 오염된다 → 보존은 `DeckEntry`(불변 데이터)로 하고, 전투용 인스턴스는 매번 새로 만든다. (`RunDeckState.cs:9-11`)
- SampleScene은 스테이지 토글 방식의 단일 씬이라 씬 오브젝트가 런 내내 유지된다. → 별도 영속화(DontDestroyOnLoad) 없이 씬 싱글톤만으로 런 상태가 보존된다. (`RunDeckState.cs:12`)
- Bootstrap은 `NewBattleController`를 먼저 생성해, `Roundmanager`의 `IsNewBattleSystemActive()`(= `Instance != null`) 가드가 라운드 시작 전에 `true`가 되도록 보장한다. (`NewBattleSystemBootstrap.cs:11-13`)

---

## 2. 위치 / 타입

| 항목 | RunDeckState | NewBattleSystemBootstrap |
|---|---|---|
| 경로 | `Assets/Script/Battle/Run/RunDeckState.cs` | `Assets/Script/Battle/NewBattleSystemBootstrap.cs` |
| namespace | `Battle` (`RunDeckState.cs:5`) | `Battle` (`NewBattleSystemBootstrap.cs:6`) |
| 기반 타입 | `MonoBehaviour` (`RunDeckState.cs:14`) | `MonoBehaviour` (`NewBattleSystemBootstrap.cs:19`) |
| 싱글톤 | **예** — `static Instance { get; private set; }` (`RunDeckState.cs:16`) | 아니오 (씬 배치 컴포넌트) |
| 실행 순서 속성 | 없음 | `[DefaultExecutionOrder(-1000)]` (`NewBattleSystemBootstrap.cs:18`) |
| 씬 영속 방식 | 씬 싱글톤 (단일 씬 유지로 런 내내 생존). `DontDestroyOnLoad`는 사용하지 않음. | 씬 배치 — `Awake`/`Start` 자동 실행 |

`using` 의존: `RunDeckState` → `System.Collections.Generic`, `UnityEngine`, `Battle.Card` (`RunDeckState.cs:1-3`) / `Bootstrap` → `System.Collections.Generic`, `UnityEngine`, `Battle.Relic`, `Battle.UI` (`NewBattleSystemBootstrap.cs:1-4`).

---

## 3. 참조(의존) — 이 시스템이 호출/사용하는 대상

### RunDeckState 의 의존
| 대상 | 사용처 |
|---|---|
| `CardDatabase.DeckEntry` (struct) | 덱 저장 단위 `_runDeck` (`RunDeckState.cs:18`); struct이므로 수량 변경 시 재할당 (`RunDeckState.cs:128-130`) |
| `CardDatabase.DefaultStartingDeckEntries()` | 기본 시작 덱 시드 (`RunDeckState.cs:101`) |
| `CardDatabase.InstantiateDeck(...)` | 전투용 `CardInstance` 리스트 생성 (`RunDeckState.cs:110`) |
| `CardDatabase.GetById(int)` | `AddCard` 시 카드 유효성 검사 (`RunDeckState.cs:117`) |
| `CardData` (`displayName`) | 로그 출력 (`RunDeckState.cs:131,137`) |
| `CardInstance` | 반환 타입 (`RunDeckState.cs:107`) |
| `Battle.AI.CardCategory` (특히 `Defense`) | 프로파일 윈도우 `_recentUses` 및 방어 비율 계산 (`RunDeckState.cs:24,46`) |

### NewBattleSystemBootstrap 의 의존
| 대상 | 사용처 |
|---|---|
| `NewBattleController` | `Instance` 확인 후 없으면 자동 생성 (`NewBattleSystemBootstrap.cs:60-74`) |
| `RelicManager` (`Battle.Relic`) | `Instance` 보장 + 시작 유물 지급(`GiveRelicByEffect`) (`NewBattleSystemBootstrap.cs:39-41,76-84`) |
| `RelicHUD` (`Battle.UI`) | `Canvas` 하위에 자동 생성 (`NewBattleSystemBootstrap.cs:88-98`) |
| `RelicEffectType` (`Battle.Relic`) | 직렬화 필드 `startingRelics` 타입 (`NewBattleSystemBootstrap.cs:23`) |
| `RunDeckState` | `EnsureExists().EnsureSeeded()` 호출 (`NewBattleSystemBootstrap.cs:32`) |
| 레거시 컴포넌트: `ElementSlotSystem`, `ComboSystem`, `ElementSlotHUD`, `CardSystem` | `PreDisableLegacyCombatObjects()`에서 전부 비활성화 (`NewBattleSystemBootstrap.cs:45-51`) |
| `Canvas` | `RelicHUD` 부모 탐색 (`NewBattleSystemBootstrap.cs:91`) |

---

## 4. 피참조 — 이 시스템을 사용하는 외부(코드 주석/메서드명 기준)

> 아래는 `RunDeckState`의 공개 API 설계 의도(주석)와 사용자 지정 명세 기준 정리. 호출자 코드 자체는 본 명세 대상 두 파일 밖에 있다.

### RunDeckState 피참조
| 호출자 | 사용 API |
|---|---|
| `NewBattleController` | `InstantiateForBattle()`(전투 덱 주입), `PermanentAttackPower`(전투 시작 시 `Ctx.attackPowerBonus`로 복원 — `RunDeckState.cs:30`), `RecordCardUse(category)`, `RecordAwakenActivation()`, 그리고 프로파일 getter `DefenseWindowRatio`/`AwakenActivations`/`NonAwakenCardUses` |
| `Roundmanager` | `EnsureExists()`, `AddCard(cardId)` |
| `GameStateController` | `ResetRun()` (런 재시작) |
| `CardEffectResolver` | `AddPermanentAttackPower(n)` (땅410 등 영구 공격력 효과) |
| `EnemyDisruptionAI` | `RunDeck` 및 프로파일 getter(방어성향/각성 카운터)로 적 AI 입력 구성 |

### NewBattleSystemBootstrap 피참조
- 직접 호출자는 없음. **씬에 배치**되어 `Awake`(`NewBattleSystemBootstrap.cs:25`) / `Start`(`NewBattleSystemBootstrap.cs:35`)가 자동 실행되는 것이 유일한 트리거.

---

## 5. 공개 API

### RunDeckState

| 멤버 | 시그니처 | 설명 / 위치 |
|---|---|---|
| `Instance` | `static RunDeckState Instance { get; private set; }` | 싱글톤 인스턴스 (`RunDeckState.cs:16`) |
| `EnsureExists` | `static RunDeckState EnsureExists()` | 없으면 생성해 `Instance` 보장 후 반환 (`RunDeckState.cs:87-94`) |
| `RunDeck` | `IReadOnlyList<CardDatabase.DeckEntry> RunDeck { get; }` | 현재 보존 덱(읽기 전용) (`RunDeckState.cs:28`) |
| `TotalCardCount` | `int TotalCardCount { get; }` | 수량 합산 총 장수 (`RunDeckState.cs:65-73`) |
| `EnsureSeeded` | `void EnsureSeeded()` | 비어 있으면 기본 시작 덱(8장)으로 시드 (`RunDeckState.cs:97-104`) |
| `InstantiateForBattle` | `List<CardInstance> InstantiateForBattle()` | 보존 덱에서 새 전투용 인스턴스 생성(내부에서 `EnsureSeeded`) (`RunDeckState.cs:107-111`) |
| `AddCard` | `void AddCard(int cardId)` | 카드 1장 추가(동일 ID면 수량 +1) (`RunDeckState.cs:114-138`) |
| `ResetRun` | `void ResetRun()` | 런 전체 상태 초기화 후 기본 덱 복원 (`RunDeckState.cs:141-151`) |
| `PermanentAttackPower` | `int PermanentAttackPower { get; }` | 영구 공격력 보너스(런 지속) (`RunDeckState.cs:30-31`) |
| `AddPermanentAttackPower` | `void AddPermanentAttackPower(int n)` | 영구 공격력 누적(`n==0`이면 무시) (`RunDeckState.cs:32-37`) |
| `DefenseWindowRatio` | `float DefenseWindowRatio { get; }` | f4: 최근 10회 중 `Defense` 비율(`/10` 고정 분모) (`RunDeckState.cs:39-49`) |
| `AwakenActivations` | `int AwakenActivations { get; }` | 각성(피버) 누적 발동 횟수 (`RunDeckState.cs:50`) |
| `NonAwakenCardUses` | `int NonAwakenCardUses { get; }` | 비각성 카드 누적 사용 횟수 (`RunDeckState.cs:51`) |
| `RecordCardUse` | `void RecordCardUse(Battle.AI.CardCategory category)` | 일반(비각성) 카드 1장 사용 기록: 윈도우 갱신 + 비각성 카운터 증가 (`RunDeckState.cs:54-59`) |
| `RecordAwakenActivation` | `void RecordAwakenActivation()` | 각성 발동 1회 기록 (`RunDeckState.cs:62`) |

### NewBattleSystemBootstrap

| 멤버 | 시그니처 | 설명 / 위치 |
|---|---|---|
| (public 메서드) | — | **공개 API 거의 없음.** 동작은 전적으로 `Awake()`/`Start()` 라이프사이클로 수행 (`NewBattleSystemBootstrap.cs:25-42`) |
| `startingRelics` | `[SerializeField] private List<RelicEffectType>` | 인스펙터 직렬화 필드(시작 유물 목록). 클래스 외부에서 코드로 접근하지는 않음 (`NewBattleSystemBootstrap.cs:21-23`) |

> 내부 헬퍼(전부 private): `PreDisableLegacyCombatObjects()`, `DisableAll<T>()`, `EnsureNewBattleController()`, `EnsureRelicSystems()` (`NewBattleSystemBootstrap.cs:45-99`).

---

## 6. 내부 동작 / 데이터 흐름

### RunDeckState

**상태 필드** (`RunDeckState.cs:18-26`)
- `_runDeck : List<DeckEntry>` — 보존 덱
- `_seeded : bool` — 시드 여부
- `_permanentAttackPower : int` — 영구 공격력 보너스
- `_recentUses : Queue<CardCategory>` — 최근 사용 링버퍼(상한 `PROFILE_WINDOW = 10`, `RunDeckState.cs:23`)
- `_awakenActivations : int`, `_nonAwakenCardUses : int` — f7 피버사이클 카운터

**덱 라이프사이클**
1. **시드** — `EnsureSeeded()`: `_seeded && _runDeck.Count>0`이면 조기 반환(중복 시드 방지), 아니면 `_runDeck.Clear()` 후 `CardDatabase.DefaultStartingDeckEntries()`(기획 기본 8장)를 채우고 `_seeded=true`. (`RunDeckState.cs:97-104`)
2. **전투 인스턴스화** — `InstantiateForBattle()`: 내부에서 `EnsureSeeded()` 호출 후 `CardDatabase.InstantiateDeck(_runDeck)`로 매 전투 새 `CardInstance` 리스트 반환. (`RunDeckState.cs:107-111`)
3. **카드 추가** — `AddCard(cardId)`: `EnsureSeeded()` → `GetById`로 유효성 검사(없으면 경고 후 무시, `RunDeckState.cs:118-122`) → 기존 항목이면 `entry.count += 1` 후 **재할당**(struct이므로 필수, `RunDeckState.cs:128-130`), 없으면 `new DeckEntry(cardId, 1)` 추가. (`RunDeckState.cs:114-138`)

**영구 공격력**
- `AddPermanentAttackPower(n)`: `n==0` 무시, 아니면 누적 + 로그. (`RunDeckState.cs:32-37`)
- 전투 시작 시 `PermanentAttackPower`를 읽어 `Ctx.attackPowerBonus`로 복원(주석 기준, `RunDeckState.cs:30`).

**적 AI 프로파일**
- `RecordCardUse(category)`: `Enqueue` 후 `while(Count > 10) Dequeue()`로 최근 10개만 유지(링버퍼), `_nonAwakenCardUses++`. (`RunDeckState.cs:54-59`)
- `DefenseWindowRatio`: 비었으면 `0f`, 아니면 `Defense` 개수 / **고정 분모 10**(`PROFILE_WINDOW`). → 윈도우가 10 미만으로 채워졌을 때 비율이 실제 표본 대비 낮게 나오는 의도적 설계. (`RunDeckState.cs:39-49`)
- `RecordAwakenActivation()`: `_awakenActivations++`. (`RunDeckState.cs:62`)

**싱글톤 / 생명주기**
- `Awake()`: `Instance==null`이면 자기 등록, 이미 다른 인스턴스가 있으면 자신을 `Destroy`. (`RunDeckState.cs:75-79`)
- `OnDestroy()`: 자신이 `Instance`면 `null`로 해제. (`RunDeckState.cs:81-84`)
- `EnsureExists()`: `Instance` → `FindFirstObjectByType(Include)` → 새 `GameObject("RunDeckState")` 순으로 탐색·생성(Awake에서 Instance 설정). (`RunDeckState.cs:87-94`)

### NewBattleSystemBootstrap

**Awake** (실행 순서 `-1000`, `NewBattleSystemBootstrap.cs:25-33`)
1. `PreDisableLegacyCombatObjects()` — 다른 Awake 실행 전에 레거시 전투 오브젝트 차단(`ElementSlotSystem`/`ComboSystem`/`ElementSlotHUD`/`CardSystem`을 `DisableAll<T>`로 비활성). (`NewBattleSystemBootstrap.cs:45-58`)
2. `EnsureNewBattleController()` — `Instance` 있으면 반환, 비활성 인스턴스 있으면 활성화, 없으면 `GameObject("NewBattleController")` 생성. → `Roundmanager` 가드 통과 보장. (`NewBattleSystemBootstrap.cs:60-74`)
3. `EnsureRelicSystems()` — `RelicManager`(없으면 생성), `RelicHUD`(없고 `Canvas` 있으면 Canvas 하위에 `RectTransform` 포함 생성). 시작 유물 지급은 여기서 하지 않음. (`NewBattleSystemBootstrap.cs:76-99`)
4. `RunDeckState.EnsureExists().EnsureSeeded()` — 런덱 보장 + 시드. (`NewBattleSystemBootstrap.cs:32`)

**Start** (`NewBattleSystemBootstrap.cs:35-42`)
- 모든 `Awake` 완료(= 싱글톤 `Instance` 보장 시점) 후, `startingRelics`가 비어있지 않고 `RelicManager.Instance != null`이면 각 효과를 `GiveRelicByEffect(fx)`로 지급.
- **Start로 분리한 이유**: 씬에 미리 배치된 `RelicManager`의 `Awake`가 부트스트랩(-1000)보다 늦게 실행돼도 정상 동작하도록 타이밍을 회피. (`NewBattleSystemBootstrap.cs:37-38`)

> 전투 시작(`StartBattle`)/종료(`EndBattle`)는 부트스트랩이 아니라 `Roundmanager`가 노드 진입/종료 시 호출한다. (`NewBattleSystemBootstrap.cs:15`)

---

## 7. 직렬화 필드

| 클래스 | 필드 | 타입 | 어트리뷰트 / 위치 |
|---|---|---|---|
| NewBattleSystemBootstrap | `startingRelics` | `List<RelicEffectType>` (초기값 `new List<RelicEffectType>()`) | `[Header("시작 유물 (테스트/밸런스)")]`, `[Tooltip(...)]`, `[SerializeField] private` (`NewBattleSystemBootstrap.cs:21-23`) |

- Tooltip 예시: 이무기의 여의주 = `AwakenGaugeRecoverPerCombo`, 비급서 = `ComboBonusSecondsBoost`. (`NewBattleSystemBootstrap.cs:22`)
- **RunDeckState에는 직렬화 필드가 없다.** 모든 상태는 런타임 전용(`private`, 비직렬화)이며 인스펙터에 노출되지 않는다.

---

## 8. 주의 / 엣지케이스

- **중복 생성 방지**: `EnsureExists`/`EnsureNewBattleController`/`EnsureRelicSystems`는 모두 먼저 `Instance`를 확인하고, 없으면 `FindFirstObjectByType(..., FindObjectsInactive.Include)`로 씬에 **미리 배치한 매니저를 인식**한 뒤에야 새로 생성한다. → 씬에 수동 배치해도 중복 생성되지 않는다. (`RunDeckState.cs:88-93`, `NewBattleSystemBootstrap.cs:62-69,79-83,89-90`)
- **씬 배치 위치 권장**: 부트스트랩/매니저는 스테이지 토글로 비활성화되지 않도록 **씬 루트에 배치** 권장(비활성 부모 하위면 자동 생성 로직과 충돌 가능). `DisableAll`/`FindFirstObjectByType`가 `FindObjectsInactive.Include`를 쓰므로 비활성 오브젝트도 탐색 대상.
- **ResetRun의 초기화 범위**: 덱뿐 아니라 `_permanentAttackPower`, `_recentUses`, `_awakenActivations`, `_nonAwakenCardUses`까지 **AI 프로파일·영구 공격력을 모두 0/clear**로 되돌린 뒤 `EnsureSeeded()`로 기본 덱 복원. → 런 리셋 시 적 AI 학습 입력과 영구 버프가 함께 초기화된다. (`RunDeckState.cs:141-151`)
- **DefenseWindowRatio 분모 고정**: 분모가 실제 표본 수가 아니라 항상 `PROFILE_WINDOW(10)`이라, 사용 횟수가 10 미만일 때 비율이 과소평가된다(의도적). (`RunDeckState.cs:39-49`)
- **DeckEntry는 struct**: `AddCard`에서 수량을 늘릴 때 리스트 요소를 **재할당**해야 반영된다(값 복사 특성). (`RunDeckState.cs:128-130`)
- **AddCard 미지의 ID**: `CardDatabase.GetById`가 `null`이면 경고 로그만 남기고 조용히 무시(예외 미발생). (`RunDeckState.cs:118-122`)
- **RelicHUD 생성 조건**: 씬에 `Canvas`가 없으면 `RelicHUD`는 생성되지 않는다(유물 자체 로직엔 영향 없음, HUD만 누락). (`NewBattleSystemBootstrap.cs:91-97`)
- **시작 유물 지급 타이밍**: `RelicManager.Instance`가 `Start` 시점에도 `null`이면 유물은 지급되지 않는다(조용히 스킵). (`NewBattleSystemBootstrap.cs:39`)
- **CardInstance 재사용 금지**: 보존을 `DeckEntry`로 하고 매 전투 새 인스턴스를 만드는 이유는 `CardInstance`의 런타임 상태 오염 방지. 전투 결과를 보존 덱에 직접 반영하면 안 된다. (`RunDeckState.cs:9-11`)
