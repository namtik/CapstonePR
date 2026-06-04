# 유물 시스템 명세

> 대상 코드: `Assets/Script/Battle/Relic/RelicManager.cs`, `Assets/Script/Battle/Relic/RelicDef.cs`, `Assets/Script/Battle/UI/RelicHUD.cs`
> 본 문서는 실제 소스를 읽고 작성됨. 모든 동작 근거는 `파일:line`으로 표기.

---

## 1. 개요 / 책임

런(run) 동안 지속되는 **유물 보유 목록 관리**와 **효과 조회**, 그리고 화면 **좌상단 HUD 표시**를 담당하는 시스템.

- `RelicManager` — 보유 유물 리스트를 들고 있으며, 특정 효과(`RelicEffectType`) 보유 여부를 외부에 조회시켜 줌. 유물 효과 자체의 *실행*은 하지 않고, "이 효과를 가졌는가"만 알려주는 조회 책임만 가진다 (`RelicManager.cs:73-78`).
- 실제 효과 적용(각성 게이지 회복, 콤보 보너스 시간 가산)은 `NewBattleController`가 전투 로직 흐름 안에서 `HasEffect(...)`를 호출해 분기하는 방식으로 구현됨 (`NewBattleController.cs:656-658`, `1045-1051`).
- `RelicHUD` — 보유 유물 아이콘을 좌상단에 획득 순서대로 표시하고, 마우스 호버 시 이름+설명 툴팁을 띄움 (`RelicHUD.cs:10-13`).

---

## 2. 위치 / 타입

| 구성요소 | 경로 | namespace | 타입 |
|---|---|---|---|
| `RelicManager` | `Assets/Script/Battle/Relic/RelicManager.cs` | `Battle.Relic` (`RelicManager.cs:5`) | `MonoBehaviour` 싱글톤 (`RelicManager.cs:12,14`) |
| `RelicDef` | `Assets/Script/Battle/Relic/RelicDef.cs` | `Battle.Relic` (`RelicDef.cs:3`) | `[System.Serializable]` 데이터 클래스 (`RelicDef.cs:14-15`) |
| `RelicEffectType` | `Assets/Script/Battle/Relic/RelicDef.cs` | `Battle.Relic` (`RelicDef.cs:3`) | `enum` (`RelicDef.cs:5-12`) |
| `RelicHUD` | `Assets/Script/Battle/UI/RelicHUD.cs` | `Battle.UI` (`RelicHUD.cs:8`) | `MonoBehaviour`, 런타임 생성 (`RelicHUD.cs:14`) |

- 싱글톤 패턴: `RelicManager`, `RelicHUD` 모두 `Awake`에서 `Instance` 설정, 중복 시 `Destroy`, `OnDestroy`에서 `Instance` 해제 (`RelicManager.cs:41-50`, `RelicHUD.cs:51-60`).
- `RelicHUD`는 프리팹/씬 작업 없이 코드로 자식 UI를 전부 생성한다 (`RelicHUD.cs:78-167`). 즉 "런타임 생성" 타입.

---

## 3. 참조 (의존)

`RelicHUD`가 외부에 의존하는 대상:

- **Canvas** — HUD가 자기 UI를 붙일 부모. `GetComponentInParent<Canvas>()`로 먼저 찾고, 없으면 `FindFirstObjectByType<Canvas>()`로 탐색. 둘 다 없으면 레이아웃 구성을 중단(`return`)함 (`RelicHUD.cs:80-82`).
- **스프라이트 아이콘** (`RelicDef.icon`, `Sprite`) — 아이콘 이미지 소스. `null`이면 기본 색상 박스로 대체(fallback) (`RelicHUD.cs:189-197`, `225-229`).
- **TMP 폰트** (`tooltipFont`, `TMP_FontAsset`) — 툴팁 텍스트 글씨체. `null`이면 TMP 기본 폰트 사용 (`RelicHUD.cs:26`, `149`, `164`).

`RelicManager`는 `RelicHUD.Instance?.Refresh(...)`로 HUD에 갱신을 통지한다 (`RelicManager.cs:69`). 단 null-conditional이라 HUD가 없어도 안전.

---

## 4. 피참조 (이 시스템을 사용하는 쪽)

| 사용처 | 사용 내용 | 근거 |
|---|---|---|
| `NewBattleController` | `RelicManager.Instance.HasEffect(ComboBonusSecondsBoost)` — 콤보 매칭 시 각성 지속시간 보너스에 +0.5s 가산 | `NewBattleController.cs:656-658` |
| `NewBattleController` | `RelicManager.Instance.HasEffect(AwakenGaugeRecoverPerCombo)` — 각성 종료 처리 시 성공 콤보 수만큼 각성 게이지 회복 | `NewBattleController.cs:1045-1051` |
| `NewBattleSystemBootstrap` | `EnsureRelicSystems()`에서 `RelicManager`·`RelicHUD` 자동 생성, `Start()`에서 `startingRelics`를 `GiveRelicByEffect(fx)`로 지급 | `NewBattleSystemBootstrap.cs:39-41`, `76-98` |
| `BattleTestController` | (격리 테스트 씬) `RelicManager`·`RelicHUD` 자동 생성, `testRelics`를 `GiveRelicByEffect`로 지급 | `BattleTestController.cs:155-179` |
| `RelicManager.relicDefinitions` | Inspector에서 아이콘/이름/설명을 직접 편집하는 유물 정의 카탈로그 | `RelicManager.cs:19-36` |

> 참고: 작업 범위 지침상 실제 게임(SampleScene) 경로는 `NewBattleSystemBootstrap`이고, `BattleTestController`(격리 테스트 씬)는 유지보수 중단 대상이다. 위 표의 BattleTestController 항목은 동일 셋업 패턴을 공유한다는 사실 기록용.

---

## 5. 공개 API

### `RelicManager`

| 멤버 | 시그니처 | 설명 | line |
|---|---|---|---|
| `Instance` | `static RelicManager { get; private set; }` | 싱글톤 인스턴스 | `RelicManager.cs:14` |
| `COMBO_BONUS_SECONDS_EXTRA` | `const float = 0.5f` | 비급서: 콤보 성공 시 기본 1s에 더해지는 추가 보너스 초 | `RelicManager.cs:16-17` |
| `OwnedRelics` | `IReadOnlyList<RelicDef>` (getter) | 현재 보유 유물 목록(읽기 전용) | `RelicManager.cs:38-39` |
| `GiveRelicByEffect` | `void GiveRelicByEffect(RelicEffectType effectType)` | `relicDefinitions`에서 해당 effect 가진 정의를 찾아 지급. 없으면 `LogWarning` 후 무시 | `RelicManager.cs:52-62` |
| `AddRelic` | `void AddRelic(RelicDef relic)` | 유물 직접 추가. `null` 무시, **id 중복이면 무시**, 추가 시 HUD `Refresh` 호출 | `RelicManager.cs:64-71` |
| `HasEffect` | `bool HasEffect(RelicEffectType type)` | 보유 유물 중 해당 effect가 하나라도 있으면 `true` | `RelicManager.cs:73-78` |
| `relicDefinitions` | `[SerializeField] List<RelicDef>` (private) | 사용 가능 유물 정의 카탈로그(2종 기본값 코드 내장) — Inspector 편집용. **외부 공개 아님** | `RelicManager.cs:19-36` |

### `RelicDef` / `RelicEffectType`

| 필드 | 타입 | 비고 | line |
|---|---|---|---|
| `id` | `string` | 유물 고유 식별자(중복 판정 키) | `RelicDef.cs:17` |
| `displayName` | `string` | 표시 이름 | `RelicDef.cs:18` |
| `description` | `string` | 설명. `[TextArea(2,4)]` | `RelicDef.cs:19` |
| `icon` | `Sprite` | 아이콘. null 허용(fallback 색) | `RelicDef.cs:20` |
| `effect` | `RelicEffectType` | 이 유물의 효과 종류 | `RelicDef.cs:21` |

`RelicEffectType` 값 (`RelicDef.cs:5-12`):

| 값 | 의미 |
|---|---|
| `None` | 효과 없음(기본값) |
| `AwakenGaugeRecoverPerCombo` | 이무기의 여의주: 각성 종료 후 성공한 콤보 수만큼 각성 게이지 회복 |
| `ComboBonusSecondsBoost` | 비급서: 각성 중 콤보 성공 시 보너스 시간 +0.5초 추가 |

### `RelicHUD`

| 멤버 | 시그니처 | 설명 | line |
|---|---|---|---|
| `Instance` | `static RelicHUD { get; private set; }` | 싱글톤 인스턴스 | `RelicHUD.cs:16` |
| `Refresh` | `void Refresh(IReadOnlyList<RelicDef> relics)` | 보유 목록 갱신 → 아이콘 재구성(`RebuildIcons`) | `RelicHUD.cs:69-74` |

> 내부 메서드: `BuildLayout`(레이아웃 1회 구성, `Start`에서 호출), `RebuildIcons`/`CreateIconItem`(아이콘 생성), `ShowTooltip`(툴팁 표시) — 모두 private (`RelicHUD.cs:78-253`). 외부에서 직접 호출하는 공개 갱신 진입점은 `Refresh` 하나.

---

## 6. 내부 동작

### 6.1 유물 지급 / 보유 관리 (`RelicManager`)

- `GiveRelicByEffect(effectType)` → `relicDefinitions.Find(r => r.effect == effectType)`로 정의 검색 → 못 찾으면 `[유물] 정의를 찾을 수 없음` 경고 후 종료, 찾으면 `AddRelic` 호출 (`RelicManager.cs:53-62`).
- `AddRelic`: `null` 가드 → `_owned.Exists(r => r.id == relic.id)`로 **id 기준 중복 차단** → 리스트 추가 → `RelicHUD.Instance?.Refresh(_owned)`로 HUD 즉시 갱신 → `[유물] 획득: {displayName}` 로그 (`RelicManager.cs:64-71`).

### 6.2 효과 연결 지점 (실행은 `NewBattleController`)

- **비급서 (`ComboBonusSecondsBoost`)** — 각성 중 콤보가 매칭(`comboTriggered`)되면, 비급서 보유 시 `extraBonus = COMBO_BONUS_SECONDS_EXTRA(0.5f)`, 미보유 시 `0f`. `totalBonus = COMBO_BONUS_SECONDS + extraBonus`를 각성 잔여시간(`_awakenTimeRemaining`)과 최대시간(`_awakenMaxTime`)에 동시 가산. 보너스가 0보다 크면 로그에 `[비급서]` 태그 (`NewBattleController.cs:652-665`).
- **이무기의 여의주 (`AwakenGaugeRecoverPerCombo`)** — 각성 종료 처리 분기에서 보유 시, `recover = Min(_queuedComboSkills.Count, EffectiveAwakenInput - 1)`만큼 각성 입력 카운트(`_elementInputCount`)를 회복. 즉 "성공한 콤보 수만큼" 회복하되 **즉시 재발동을 막기 위해 `max-1` 상한** 적용 (`NewBattleController.cs:1044-1051`).

### 6.3 HUD 런타임 자동 생성 / 아이콘 fallback (`RelicHUD`)

- `Start()`에서 `BuildLayout()` 1회 실행 후, `RelicManager.Instance`가 있으면 그 `OwnedRelics`로 초기 `Refresh` (`RelicHUD.cs:62-67`).
- `BuildLayout`: Canvas 하위에
  - **아이콘 컨테이너** — 좌상단(anchor `(0,1)`) 고정, `anchoredPosition (PANEL_PADDING, -PANEL_PADDING)`, `HorizontalLayoutGroup`(spacing 8) + `ContentSizeFitter`(가로 PreferredSize) (`RelicHUD.cs:86-103`).
  - **툴팁 패널** — 동일 좌상단 앵커, 마지막 형제로 배치, 반투명 어두운 배경, 내부에 아이콘/이름/설명 텍스트 자식 생성. 마지막에 `SetActive(false)`로 숨김 (`RelicHUD.cs:105-167`).
- `CreateIconItem`: 유물마다 `Relic_{id}` 오브젝트 생성, `icon`이 있으면 스프라이트+흰색, **없으면 기본 색 `(0.6, 0.5, 0.2, 1)` 박스로 fallback**. 금색 `Outline` 추가. `EventTrigger`로 PointerEnter→`ShowTooltip`, PointerExit→툴팁 숨김 (`RelicHUD.cs:180-216`).
- `ShowTooltip`: 아이콘/이름/설명 갱신. 아이콘 fallback 동일 처리. **설명 길이에 맞춰 `GetPreferredValues`로 세로 높이 자동 조절**(`tooltipMinHeight` 하한). 위치는 컨테이너와 툴팁이 같은 좌상단 앵커임을 이용해 `iconIndex` 기반 오프셋으로 해당 아이콘 바로 아래에 배치 (`RelicHUD.cs:220-253`).
- 레이아웃 상수: `ICON_SIZE 60`, `ICON_SPACING 8`, `PANEL_PADDING 10`, `TOOLTIP_PAD 12`, `TT_ICON_SIZE 48` (`RelicHUD.cs:18-22`).

---

## 7. 직렬화 필드 (Inspector 노출)

### `RelicManager`
| 필드 | 타입 | 기본값 / 비고 | line |
|---|---|---|---|
| `relicDefinitions` | `[SerializeField] List<RelicDef>` | 코드에 2종(이무기의 여의주 / 비급서) 기본값 내장. **아이콘은 여기서 설정** | `RelicManager.cs:19-36` |

> `RelicDef`가 `[System.Serializable]`이므로 위 리스트의 각 항목(id/displayName/description/icon/effect)이 Inspector에서 직접 펼쳐 편집 가능 (`RelicDef.cs:14-21`).

### `RelicHUD`
| 필드 | 타입 | 기본값 | line |
|---|---|---|---|
| `tooltipFont` | `[SerializeField] TMP_FontAsset` | (비움 시 TMP 기본 폰트) | `RelicHUD.cs:24-26` |
| `nameFontSize` | `[SerializeField] float` | `18f` | `RelicHUD.cs:27-28` |
| `descFontSize` | `[SerializeField] float` | `15f` | `RelicHUD.cs:29-30` |
| `tooltipWidth` | `[SerializeField] float` | `340f` | `RelicHUD.cs:32-34` |
| `tooltipMinHeight` | `[SerializeField] float` | `90f` | `RelicHUD.cs:35-36` |

### `NewBattleSystemBootstrap` (유물 지급 관련, 참고)
| 필드 | 타입 | 비고 | line |
|---|---|---|---|
| `startingRelics` | `[SerializeField] List<RelicEffectType>` | 런 시작 시 자동 지급할 유물 효과 목록 | `NewBattleSystemBootstrap.cs:21-23` |

---

## 8. 주의 / 엣지케이스

1. **아이콘을 영구로 넣으려면 씬에 미리 배치해야 함.** Bootstrap/TestController가 `RelicManager`·`RelicHUD`를 *코드로 자동 생성*하면 `relicDefinitions`는 코드 기본값(아이콘 null)으로 채워져 아이콘이 fallback 색 박스로만 표시된다 (`RelicManager.cs:19-36`, `RelicHUD.cs:194-197`). 아이콘을 지정하려면 씬에 `RelicManager`(아이콘 채운 `relicDefinitions`)와 `RelicHUD`를 미리 배치해야 한다. 자동 생성 로직은 이미 존재하는 인스턴스가 있으면 새로 만들지 않도록 가드되어 있다 (`NewBattleSystemBootstrap.cs:79-98`, `BattleTestController.cs:156-179`).

2. **현재 정의된 유물은 2종뿐.** 이무기의 여의주(`AwakenGaugeRecoverPerCombo`)와 비급서(`ComboBonusSecondsBoost`)만 코드에 내장 (`RelicManager.cs:22-35`). 새 유물을 추가하려면 `RelicEffectType` enum 값 추가 + `relicDefinitions` 항목 추가 + `NewBattleController` 측 효과 적용 분기 작성이 모두 필요하다(효과 실행은 RelicManager가 하지 않으므로).

3. **중복 획득 규칙: id 기준으로 차단.** 같은 `id`를 이미 보유했으면 `AddRelic`이 조용히 무시한다(스택/중첩 불가) (`RelicManager.cs:67`). 따라서 `GiveRelicByEffect`를 같은 효과로 두 번 호출해도 1개만 보유.

4. **`GiveRelicByEffect`로 못 찾는 효과를 넘기면** 경고 로그만 남기고 지급되지 않는다 (`RelicManager.cs:56-60`). 예: `RelicEffectType.None`은 `relicDefinitions`에 정의가 없어 지급 실패.

5. **지급 타이밍 분리.** Bootstrap은 `Awake`(실행순서 -1000)에서 시스템을 보장 생성하되, 실제 시작 유물 지급은 `Start`에서 수행한다. 씬에 미리 배치된 `RelicManager`의 `Awake`가 Bootstrap의 `Awake`보다 늦게 실행돼도 `Instance`가 보장된 시점에 지급되도록 하기 위함 (`NewBattleSystemBootstrap.cs:35-42`, `86`).

6. **Canvas가 없으면 HUD 무동작.** `BuildLayout`이 Canvas를 못 찾으면 즉시 `return`하여 아이콘/툴팁이 생성되지 않는다. 이 경우 `RebuildIcons`도 `_iconContainer == null` 가드로 조용히 끝난다 (`RelicHUD.cs:82`, `171`). 또한 Bootstrap/TestController의 HUD 자동 생성도 Canvas가 없으면 건너뛴다 (`NewBattleSystemBootstrap.cs:91-97`).
