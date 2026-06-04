# 전투 UI (CardHandHUD / NewCardView / CardRewardUI) 명세

> 대상 파일
> - `Assets/Script/Battle/UI/CardHandHUD.cs` (1434줄)
> - `Assets/Script/Battle/UI/NewCardView.cs` (659줄)
> - `Assets/Script/Battle/UI/CardRewardUI.cs` (288줄)
> - (보조) `Assets/Script/Battle/UI/CardEffectOverlay.cs`, `Assets/Script/Battle/UI/PlayerDamageOverlay.cs`
>
> 모든 `file:line`은 위 경로 기준.

---

## 1. 개요 / 책임

전투 화면의 카드 UI 3종 묶음.

- **CardHandHUD** (`CardHandHUD.cs:16`) — 손패 HUD. 다음을 한 컴포넌트가 모두 담당한다.
  - 손패 부채꼴/평면 레이아웃 + 카드 수에 따른 중앙 정렬 (`Refresh` `CardHandHUD.cs:750`, `PositionFanForCount` `CardHandHUD.cs:1334`)
  - 콤보 슬롯 3칸 + 콤보 스킬 목록 (각성 전용, `UpdateComboSlot` `CardHandHUD.cs:598`, `UpdateComboSkillList` `CardHandHUD.cs:684`)
  - 각성(Awaken) 게이지 자동 생성/앵커/색 갱신 (`UpdateAwakenGauge` `CardHandHUD.cs:287`)
  - 각성 입력 히스토리 좌측 세로 표시 (`UpdateAwakenInputHistory` `CardHandHUD.cs:517`)
  - 카드 선택 모드(패 1장 직접 선택) / 카드 픽커 모드(임의 목록 그리드 선택) (`EnterSelectionMode` `CardHandHUD.cs:976`, `EnterCardPickerMode` `CardHandHUD.cs:1040`)
  - 더미 뷰어(뽑을/버린 더미 카운트 클릭 → 내용 표시) (`TogglePileViewer` `CardHandHUD.cs:189`)
  - 카드 사용 중앙 연출 (`PlayCardUsePresentation` `CardHandHUD.cs:858`)
- **NewCardView** (`NewCardView.cs:15`) — 카드 한 장 프리팹에 붙는 표시 + 입력 컴포넌트. 카드 데이터 바인딩, 드래그/hover/클릭, 드로우 등장 연출을 담당. `IBeginDragHandler`/`IDragHandler`/`IEndDragHandler`/`IPointerEnterHandler`/`IPointerExitHandler`/`IPointerClickHandler` 구현 (`NewCardView.cs:15-18`).
- **CardRewardUI** (`CardRewardUI.cs:14`) — 전투 승리 후 카드 획득 보상 패널. **런타임 전체 생성**(프리팹/씬 작업 불필요), 등급 가중 랜덤으로 4장 제시 → 1장 선택 시 카드 ID 콜백.

---

## 2. 위치 / 타입

| 클래스 | 경로 | namespace | 기반 | 비고 |
|---|---|---|---|---|
| CardHandHUD | `Assets/Script/Battle/UI/CardHandHUD.cs` | `Battle.UI` | `MonoBehaviour` | 싱글톤 아님 |
| NewCardView | `Assets/Script/Battle/UI/NewCardView.cs` | `Battle.UI` | `MonoBehaviour` | 프리팹(NewSkillCard)에 부착 |
| CardRewardUI | `Assets/Script/Battle/UI/CardRewardUI.cs` | `Battle.UI` | `MonoBehaviour` | **싱글톤** `Instance` (`CardRewardUI.cs:16`), 없으면 `EnsureExists()`가 GameObject 생성 (`CardRewardUI.cs:36`) |

CardRewardUI 싱글톤 동작: `Awake`에서 `Instance` 선점, 중복 인스턴스는 자기 자신을 `Destroy`(`CardRewardUI.cs:24-28`). `EnsureExists()`는 `Instance` → 비활성 포함 씬 탐색(`FindFirstObjectByType(FindObjectsInactive.Include)`) → 새 `GameObject("CardRewardUI")` 순으로 보장 (`CardRewardUI.cs:36-43`).

---

## 3. 참조(의존)

**CardHandHUD →**
- `CardDeckSystem` (`Battle.Deck`) — `Bind(deck)`로 묶고 `OnPileChanged` 이벤트 구독(`CardHandHUD.cs:249-255`). `_deck.Hand`/`DrawPile`/`DiscardPile`/`DrawCount`/`DiscardCount` 읽음.
- `NewCardView` (`cardPrefab`) — 손패 슬롯/픽커/사용 연출/더미 뷰어 카드를 모두 이 프리팹 인스턴스로 만든다. 콤보 슬롯·히스토리의 속성 sprite도 `cardPrefab.GetElementSprite(...)`로 공유 (`CardHandHUD.cs:559`, `615`, `711-714`).
- `ComboSkillDef` — 콤보 스킬 목록 데이터 (`UpdateComboSkillList` `CardHandHUD.cs:684`).
- `SkillListItemUI` (`comboSkillItemPrefab`) — 콤보 스킬 항목 프리팹, `SetupForCombo(...)` 호출 (`CardHandHUD.cs:727`).
- `CardInstance` / `CardElement` (`Battle.Card`) — 카드 단위, 속성 enum.
- `Canvas` — `ResolveTargetCanvas()`로 좌표계 기준 캔버스 탐색 (`CardHandHUD.cs:1265`).
- `NewBattleController.Instance` 는 직접 참조하지 않음(아래 NewCardView가 참조).

**NewCardView →**
- `CardInstance` / `CardData` / `CardElement` / `CardType` (`Battle.Card`).
- `CardHandHUD` (`Hud`) — 드래그 사용(`Hud.TryUseFromDrag`)·클릭 사용(`Hud.TryUseFromClick`)·선택/픽커 클릭(`Hud.OnCardClicked`)을 위임 (`NewCardView.cs:571`, `639`, `646`).
- `Battle.NewBattleController.Instance` — `Refresh()`에서 글로벌 속성 저주 상태를 카드에 동기화(`Card.cursed = ...IsElementCursed(Card.Element)`) (`NewCardView.cs:214-217`).
- `Resources` — 카드 아이콘(`CardIcons/{skillImg}` 또는 `CardIcons/{cardId}`) 로드 (`NewCardView.cs:308`, `333`).

**CardRewardUI →**
- `CardDatabase` (`Battle.Card`) — `EnsureInit()` + `All` 로 보상 풀 구성 (`CardRewardUI.cs:81-97`).
- `CardData` / `CardInstance` / `CardRarity` / `CardElement` — 등급 가중 추첨 (`RollChoices`/`WeightedPick` `CardRewardUI.cs:79-134`).
- `NewCardView` (`cardPrefab` 인자) — 보상 카드 비주얼 렌더에 재사용. 호출자(Roundmanager)가 `CardHandHUD.CardPrefab`을 넘긴다.
- `RunDeckState` — **이 파일 안에서는 직접 참조하지 않음**. 선택된 카드 ID는 `onPicked` 콜백으로 호출자에게 전달되고, 덱 반영은 호출자(Roundmanager) 책임.
- `Canvas` — `ResolveCombatCanvas()`로 `CombatStage` 우선 탐색 (`CardRewardUI.cs:246-255`).

---

## 4. 피참조

**NewBattleController** (`Assets/Script/Battle/NewBattleController.cs`) → CardHandHUD (`handHud` 필드 경유):

| 호출 | 위치 |
|---|---|
| `handHud.Bind(_deck)` | `NewBattleController.cs:290` |
| `handHud.UseCardCallback = OnUseCardRequested` | `:291` |
| `handHud.SelectionClosedCallback = TryActivatePendingAwaken` | `:292` |
| `handHud.SetAwakenMode(false/true)` | `:293`, `:975`, `:1068` |
| `handHud.SetAwakenGaugeAnchor(hpBar RectTransform)` | `:318`, `:1337` |
| `handHud.CancelCardUsePresentations()` | `:335` (전투 종료 시) |
| `handHud.UpdateComboSlot(_comboInput)` | `:671`, `:976`, `:1069` |
| `handHud.UpdateComboSkillList(ownedComboSkills, _comboCooldown)` | `:362`, `:672`, `:977`, `:1070` |
| `handHud.UpdateAwakenInputHistory(_awakenInputHistory)` | `:673`, `:978`, `:1071` |
| `handHud.PlayCardUsePresentation(card, ..., interruptable: true)` (각성 비차단) | `:676` |
| `handHud.PlayCardUsePresentation(card, () => ResolveCardUse(card))` (일반 차단) | `:692` |
| `handHud.EnterSelectionMode(...)` | `:524`, `:1288` |
| `handHud.EnterCardPickerMode(...)` | `:531` |
| `handHud.IsSelectionMode / IsPickerMode / IsViewerMode` (입력 게이트) | `:602`, `:936` |
| `handHud.SetAwakenText(label)` + `handHud.UpdateAwakenGauge(...)` | `:1345-1346` |

**Roundmanager** (`Assets/Script/Round/Roundmanager.cs`) → CardRewardUI:
- `var rewardUI = Battle.UI.CardRewardUI.EnsureExists();` (`Roundmanager.cs:330`)
- `rewardUI.Present(cardPrefab, pickedCardId => { ... });` (`Roundmanager.cs:331`)

**NewCardView → CardHandHUD** (역위임): `Hud.OnCardClicked(this)` (`NewCardView.cs:639`), `Hud.TryUseFromClick(this)` (`NewCardView.cs:646`), `Hud.TryUseFromDrag(this, ev)` (`NewCardView.cs:571`).

---

## 5. 공개 API

### CardHandHUD

| 멤버 | 시그니처 / 타입 | 위치 | 설명 |
|---|---|---|---|
| `Bind` | `void Bind(CardDeckSystem deck)` | `:249` | 덱 연결 + `OnPileChanged` 구독 + 즉시 `Refresh()`. 이전 덱 구독 해제. |
| `UseCardCallback` | `System.Func<CardInstance,bool>` (public 필드) | `:130` | 카드 사용 요청 콜백. true 반환 시 사용 성공. |
| `SelectionClosedCallback` | `System.Action` (public 필드) | `:132` | 선택/픽커 모드 완료 직후 호출(보류 각성 발동 등). |
| `CardPrefab` | `NewCardView CardPrefab { get; }` | `:26` | 카드 프리팹 읽기 전용 접근자(보상 UI 등이 재사용). |
| `DragLayer` | `RectTransform DragLayer { get; }` | `:33` | 드래그 레이어(없으면 `handRoot`). |
| `IsSelectionMode` | `bool { get; }` | `:141` | `_selectionCallback != null`. |
| `IsPickerMode` | `bool { get; }` | `:1021` | `_pickerCallback != null`. |
| `IsViewerMode` | `bool { get; }` | `:169` | 더미 뷰어 활성 여부. |
| `EnterSelectionMode` | `void EnterSelectionMode(string prompt, Action<CardInstance> cb, Func<CardInstance,bool> filter=null, CardInstance excludeCard=null)` | `:976` | 패에서 1장 직접 선택. dim + 안내문. |
| `ExitSelectionMode` | `void ExitSelectionMode()` | `:1000` | 선택 모드 종료. |
| `EnterCardPickerMode` | `void EnterCardPickerMode(string prompt, IList<CardInstance> cards, Action<CardInstance> cb)` | `:1040` | 임의 카드 목록을 스크롤 그리드로 깔고 1장 선택. 빈 목록이면 `cb(null)` 즉시. |
| `ExitPickerMode` | `void ExitPickerMode()` | `:1080` | 픽커 종료. |
| `TogglePileViewer` | `void TogglePileViewer(int pile)` | `:189` | `0`=뽑을 더미, `1`=버린 더미 내용을 ID순으로 표시. 같은 더미 재호출 시 닫힘. |
| `CloseViewer` | `void CloseViewer()` | `:236` | 더미 뷰어 닫기. |
| `UpdateAwakenGauge` | `void UpdateAwakenGauge(int chargeCur, int chargeMax, bool active, float timeRemaining, float timeMax)` | `:287` | 비활성=충전(`cur/max`), 활성=카운트다운(`timeRemaining/timeMax`). 게이지 없으면 자동 생성. |
| `SetAwakenText` | `void SetAwakenText(string text)` | `:262` | `awakenCountText` 갱신. |
| `SetAwakenGaugeAnchor` | `void SetAwakenGaugeAnchor(RectTransform anchor)` | `:272` | 게이지 부착 기준(보통 Player.hpBar). 같은 anchor면 즉시 return(매 프레임 호출 안전). |
| `SetAwakenMode` | `void SetAwakenMode(bool active)` | `:467` | 콤보 슬롯/스킬/히스토리 패널 토글 + 각성 dim on/off. |
| `UpdateComboSlot` | `void UpdateComboSlot(IList<CardElement> input)` | `:598` | 콤보 슬롯 3칸을 입력 시퀀스로 갱신. 빈 칸은 회색. |
| `UpdateComboSkillList` | `void UpdateComboSkillList(IList<ComboSkillDef> skills, int[] cooldownRemaining)` | `:684` | 콤보 스킬 목록 표시. 쿨다운>0이면 흐리게+남은 횟수. |
| `UpdateAwakenInputHistory` | `void UpdateAwakenInputHistory(IList<CardElement> history)` | `:517` | 각성 중 입력 속성 전체 히스토리를 좌측 세로(열 wrap)로 표시. |
| `PlayCardUsePresentation` | `void PlayCardUsePresentation(CardInstance card, Action onDisappear, bool interruptable=false)` | `:858` | 사용 카드를 중앙에 잠깐 띄움. 사라지는 순간 `onDisappear` 호출(=효과 실행 타이밍). 연출 꺼짐/표시 불가 시 즉시 콜백. |
| `CancelCardUsePresentations` | `void CancelCardUsePresentations()` | `:884` | 진행 중 중앙 연출(일반+각성)을 `onDisappear` 없이 즉시 정리(전투 종료 시). |
| `OnCardClicked` | `void OnCardClicked(NewCardView view)` | `:1240` | 뷰어/픽커/선택 모드 클릭 라우팅. |
| `TryUseFromClick` | `bool TryUseFromClick(NewCardView view)` | `:819` | 더블클릭 사용. 선택/픽커/뷰어 중엔 false. |
| `TryUseFromDrag` | `bool TryUseFromDrag(NewCardView view, PointerEventData ev)` | `:808` | 드래그 종료 사용. `ev.position.y >= useThresholdY` 일 때만. |
| `Refresh` | `void Refresh()` | `:750` | 손패 슬롯 재구성/배치 + 드로우 등장 연출 + 더미 카운트 갱신. `OnPileChanged`에 연결. |
| `BringAwakenGaugeToFront` | `void BringAwakenGaugeToFront()` | `:449` | 게이지를 캔버스 최상단으로(다른 오버레이에 가렸을 때). |

### NewCardView

| 멤버 | 시그니처 / 타입 | 위치 | 설명 |
|---|---|---|---|
| `Card` | `CardInstance { get; private set; }` | `:91` | 현재 표시 카드. |
| `SlotIndex` | `int { get; set; }` | `:92` | 슬롯 인덱스(-1=픽커/연출용 임시 뷰). |
| `Hud` | `CardHandHUD { get; set; }` | `:93` | 소속 HUD. |
| `IsPlayingDrawIntro` | `bool { get; }` | `:109` | 드로우 등장 연출 진행 중. HUD가 슬롯 위치 리셋 스킵 판단에 사용. |
| `Bind` | `void Bind(CardHandHUD hud, int slotIndex)` | `:177` | HUD/슬롯 연결. |
| `SetCard` | `void SetCard(CardInstance card)` | `:183` | 카드 교체. 카드가 바뀌면 hover/drag/등장연출 상태 리셋 후 `Refresh()`. |
| `Refresh` | `void Refresh()` | `:202` | 이름/설명/게이지/타입/속성/아이콘/배경 갱신. 카드 null이면 비활성. |
| `CaptureHome` | `void CaptureHome()` | `:361` | 현재 위치/부모/sibling을 홈으로 캡처(hover offset 제거된 상태 가정). |
| `ReturnHome` | `void ReturnHome()` | `:371` | 홈 위치/스케일/회전으로 복귀. |
| `ResetToHomeScale` | `void ResetToHomeScale()` | `:383` | 스케일만 베이스로 복원. |
| `PlayDrawIntro` | `void PlayDrawIntro(float delay=0f)` | `:398` | 새 카드를 아래에서 떠오르게(stagger 지원). `CaptureHome` 직후 호출 전제. |
| `GetElementSprite` | `Sprite GetElementSprite(CardElement element)` | `:136` | 속성별 sprite 접근자(콤보 슬롯/히스토리에서 공유). |
| `OnBeginDrag/OnDrag/OnEndDrag` | (`IBegin/Drag/IEndDragHandler`) | `:515`/`:550`/`:565` | 드래그 → 드래그레이어 이동, 종료 시 `Hud.TryUseFromDrag`. |
| `OnPointerEnter/Exit` | (`IPointerEnter/ExitHandler`) | `:590`/`:606` | hover 확대·상승·최상단 sibling. |
| `OnPointerClick` | (`IPointerClickHandler`) | `:631` | 선택/픽커=단일클릭→`OnCardClicked`, 일반=더블클릭→`TryUseFromClick`. |

> 참고: 인스펙터 직렬화 필드(아이콘/속성/배경 sprite 매핑 등)는 §7 참조. public 메서드 외에는 모두 `[SerializeField] private`.

### CardRewardUI

| 멤버 | 시그니처 / 타입 | 위치 | 설명 |
|---|---|---|---|
| `Instance` | `static CardRewardUI { get; private set; }` | `:16` | 싱글톤 인스턴스. |
| `EnsureExists` | `static CardRewardUI EnsureExists()` | `:36` | 없으면 생성해 `Instance` 보장 후 반환. |
| `Present` | `void Present(NewCardView cardPrefab, Action<int> onPicked)` | `:49` | 4장 보상 제시. 선택 시(또는 풀 비어 스킵 시 `cardId<=0`) `onPicked(cardId)` 호출 후 UI 닫힘. 표시 동안 `Time.timeScale=0`. |

---

## 6. 내부 동작 / 데이터 흐름

### 6.1 손패 레이아웃 (CardHandHUD)
- 슬롯 수 `SLOT_COUNT = CardDeckSystem.MAX_HAND_LIMIT` 만큼 앵커를 **사전 생성**(`BuildSlotAnchors` `CardHandHUD.cs:1319`). 간격 기준은 `LAYOUT_REFERENCE_COUNT = CardDeckSystem.HAND_LIMIT` (`CardHandHUD.cs:19-21`).
- `fanLayout`(기본 ON): 호(arc) 위에 배치 + Z 회전. 점 좌표 `x = sin(θ)·fanRadius`, `y = (cos(θ)-1)·fanRadius - fanVerticalDip`, 회전 `-θ` (`BuildFanSlotAnchors` `CardHandHUD.cs:1404-1431`). OFF면 평면 일렬(`BuildLinearSlotAnchors` `CardHandHUD.cs:1384`).
- 매 `Refresh`마다 활성 카드 수에 맞춰 슬롯 재배치 → 카드 적으면 가운데로 모임. 부채꼴 호 각도는 카드 수에 비례 축소(`PositionFanForCount` `CardHandHUD.cs:1334-1365`). 비활성 슬롯은 `(0,-10000)`으로 화면 밖.
- `Refresh` (`CardHandHUD.cs:750`): 슬롯별로 뷰 부족 시 `Instantiate(cardPrefab)`→`Bind`. 사용 등으로 드래그레이어에 옮겨졌을 수 있어 매번 슬롯으로 부모/위치/회전/스케일 복원(단 `IsPlayingDrawIntro` 중인 뷰는 건드리지 않음). 직전 패와 비교(`_prevHandSet`)해 **이번에 새로 들어온 카드만** `PlayDrawIntro(order * drawIntroStagger)`로 stagger 등장.

### 6.2 카드 데이터 표시 (NewCardView)
- `autoBindOnAwake`면 표준 자식 이름으로 자동 바인딩: `IconImage`/`attributeImg`/`Background`/`Nametxt`/`Desctxt`/`guageCost`/`cardType`/`baseCardType` (`AutoBind` `NewCardView.cs:146-156`).
- 아이콘 우선순위(`ApplyCardIcon` `NewCardView.cs:301`): ① `Resources/CardIcons/{data.skillImg}` → ② 인스펙터 `cardIcons` 매핑(cardId) → ③ `Resources/CardIcons/{cardId}` → ④ (임시) 속성 sprite → ⑤ 속성 색.
- 배경(`ApplyBackgroundSprite` `:238`): 속성별 sprite 우선, 저주 시 보라 틴트(`0.7,0.45,0.75`), sprite 없으면 속성 색 틴트(`backgroundTintAlpha`).
- 저주 상태는 `Refresh` 시 `NewBattleController.Instance.IsElementCursed(Element)`에서 동기화 (`:214-217`).

### 6.3 입력/연출 (NewCardView)
- 드래그: `OnBeginDrag`에서 hover 복원→`CaptureHome`→`DragLayer`로 이동(`SetAsLastSibling`)→`blocksRaycasts=false`→`dragScaleMultiplier`확대+회전 제거. `OnDrag`은 `delta / canvas.scaleFactor`로 이동. `OnEndDrag`에서 `Hud.TryUseFromDrag`; 미사용이면 `ReturnHome`(hover 중이면 hover 표현 재적용).
- hover: `hoverScaleMultiplier` 확대 + `hoverPositionOffset` 상승 + 슬롯을 부모 안 최상단 sibling으로(다른 카드 위에 렌더), exit 시 원복.
- 드로우 등장(`PlayDrawIntro`/`DrawIntroRoutine` `:398`/`:436`): 홈 아래(`drawIntroRiseDistance`, 슬롯 회전 보정 `ComputeBelowOffset` `:427`)에서 알파0+`drawIntroStartScale`로 시작 → `Time.unscaledDeltaTime` 기반 ease-out cubic으로 홈 정착. delay 동안은 아래에 숨김.

### 6.4 각성 게이지 (CardHandHUD)
- `awakenGauge` 미배선 시 첫 갱신/`SetAwakenGaugeAnchor` 때 **런타임 Slider 자동 생성**(배경/Fill Area/Fill/Label까지)(`EnsureAwakenGauge` `:317-422`).
- 부모 결정: `awakenGaugeAnchor`(보통 Player.hpBar) 있으면 그 부모에 형제로 배치 → `RefitAwakenGaugeToAnchor`로 anchor와 같은 정렬+`awakenGaugeOffset`, `LayoutElement.ignoreLayout=true`로 부모 LayoutGroup 영향 차단 (`:424-446`).
- **anchor 미배선 fallback**: 정중앙(0,0)은 적/카드에 가려지므로 화면 상단 중앙(`anchor=pivot=(0.5,1)`) + `awakenGaugeFallbackPos`(기본 `(0,-40)`)로 배치 (`:415-421`).
- 갱신(`UpdateAwakenGauge` `:287`): `active`면 `fill=timeRemaining/timeMax`, 색 `awakenActiveColor`, 라벨 `"{t}s"`; 비활성이면 `fill=cur/max`, 색 `awakenChargeColor`, 라벨 `"{cur}/{max}"`. (각성은 10장 도달 즉시 발동이라 'READY 대기' 상태 없음 — 주석 `:285`.)

### 6.5 선택/픽커/뷰어 모드 (CardHandHUD)
- 공통: 전체화면 dim 오버레이 자동 생성(`EnsureDimOverlay` `:1210`, `raycastTarget=true`로 뒤쪽 클릭 차단), 안내문 표시, 시블링 순서 재배치(손패/픽커를 dim 위로). 각성 dim은 별도 색 `awakenDimColor`(`ShowAwakenDim` `:477`).
- 선택 모드: 패의 `NewCardView` 단일클릭 → `OnCardClicked`에서 `excludeCard`/`filter` 검사 후 콜백 → `ExitSelectionMode` → `SelectionClosedCallback` (`:1240-1258`).
- 픽커 모드: `ScrollRect`(세로) + `RectMask2D` viewport + 가변 높이 content를 런타임 생성(`EnsurePickerRoot` `:1092`). `pickerColumns`/`pickerColumnSpacing`/`pickerRowSpacing`로 그리드 배치(`BuildPickerCards` `:1154`). 선택 시 `ExitPickerMode`→콜백→`SelectionClosedCallback` (`TryHandlePickerClick` `:1195`).
- 더미 뷰어: 픽커 인프라(dim/pickerRoot) 재사용. ID순 정렬 표시. dim 빈 곳 클릭 또는 카드 클릭 시 닫힘(`:1232`, `:1243`).

### 6.6 카드 사용 중앙 연출 (CardHandHUD)
- `PlayCardUsePresentation`(`:858`)/`CardUsePresentationRoutine`(`:892`): 손패 풀과 **독립된 임시 카드 뷰**를 캔버스에 만들어 중앙(`cardUsePresentPos`)에 등장(작게+투명→`cardUsePresentScale`)→유지(`cardUsePresentHold`)→**사라지기 시작하는 순간 `onDisappear` 호출**→페이드아웃. 모두 `Time.unscaledDeltaTime` 사용.
- 두 트랙:
  - 일반(차단, `interruptable=false`): `_normalPresentCo` 1개. 호출자는 `onDisappear`에서 실제 효과(`ResolveCardUse`)를 지연 실행 (`NewBattleController.cs:692`).
  - 각성용(비차단, `interruptable=true`): 직전 연출을 즉시 정리하고 새 카드로 **교체**(항상 최신 카드만 표시) (`:866-872`). 효과는 별도로 즉시 처리되어 입력을 막지 않음.
- `CancelCardUsePresentations`(`:884`): 두 트랙 모두 `onDisappear` 없이 정리 — 전투 종료 시 지연 효과가 보상 화면과 겹치는 레이스 방지.

### 6.7 카드 보상 (CardRewardUI)
- `Present`(`:49`): `RollChoices(4)`로 등급 가중 추첨 → `BuildUI` 런타임 생성 → `Time.timeScale=0`.
- 풀: `CardDatabase.All` 중 4속성(Fire/Water/Wind/Earth) 카드만(무속성 필러 500 / 파편 501~503 제외) (`RollChoices` `:79-110`).
- 가중치: Normal 60 / Rare 30 / Epic 10 (`RarityWeight` `:112`). 중복 없이 `min(4, pool)`장 추첨(누적 가중 `WeightedPick` `:120`).
- UI: `CombatStage` 캔버스에 dim + 타이틔 + 카드 행. 각 카드는 `cardPrefab` 인스턴스(`enabled=false`로 드래그/hover 비활성, 모든 Graphic `raycastTarget=false`) + 투명 ClickCatcher Button. 클릭 시 `Pick(cardId)`→`Finish`(`timeScale=1` 복구 + 콜백 + cleanup) (`:140-231`).

### 6.8 보조 오버레이 (역할만)
- **CardEffectOverlay** (`CardEffectOverlay.cs:14`): 카드/콤보의 `effectName`으로 `Resources/CardEffects/{name}.png` 스프라이트 시트를 로드해 위치(적=상단/플레이어=하단, `*_ATK`/`BURN*`/`CHAIN*`은 적 대상)·크기 규칙대로 1회 애니메이션 재생(`Play`/`PlayByName`/`PlayByNameAtOffset`). 시트 없으면 조용히 스킵.
- **PlayerDamageOverlay** (`PlayerDamageOverlay.cs:13`): `Player.OnHpDecreased`를 구독해 화면 빨간 플래시 + 캔버스 흔들기 + HP바 깜빡임. 데미지 크기에 비례. 풀스크린 Image 자동 생성. `TriggerShake(float)`로 외부에서 셰이크만 요청 가능(피격 아닌 임팩트용).

---

## 7. 직렬화 필드 (중요 위주)

### CardHandHUD (`[SerializeField]`)
| 필드 | 기본값 | 위치 | 용도 |
|---|---|---|---|
| `cardPrefab` (`NewCardView`) | — | `:24` | **필수.** 손패/픽커/연출/뷰어 카드 프리팹. |
| `handRoot` (`RectTransform`) | (비우면 자동 생성) | `:30` | 슬롯 부모. |
| `dragLayer` (`RectTransform`) | (자동) | `:32` | 드래그 시 카드 이동 레이어. |
| `slotSpacing` (`Vector2`) | `(700,0)` | `:37` | 평면 슬롯 간격. |
| `handAnchoredPos` (`Vector2`) | `(0,240)` | `:38` | handRoot 자동 생성 위치. |
| `useThresholdY` (`float`) | `500` | `:40` | 드래그 종료 시 사용 판정 Y. |
| `fanLayout` (`bool`) | `true` | `:44` | 부채꼴 ON/OFF. |
| `fanRadius` (`float`) | `1800` | `:46` | 부채꼴 반지름. |
| `fanArcAngle` (`float`) | `40` | `:48` | 부채꼴 전체 각도. |
| `fanVerticalDip` (`float`) | `50` | `:50` | 호 하강 깊이. |
| `drawIntroStagger` (`float`) | `0.05` | `:54` | 다중 드로우 등장 간격. |
| `comboSlotPanel` / `comboSkillPanel` (`GameObject`) | — | `:58`/`:60` | 각성 발동 시 활성화될 콤보 패널. |
| `comboSlotImages` (`Image[3]`) | — | `:62` | 콤보 슬롯 3칸(비우면 패널 자식 자동 탐색 `:638`). |
| `comboSkillItemPrefab` (`SkillListItemUI`) | — | `:67` | 콤보 스킬 항목 프리팹. |
| `comboSkillItemContainer` (`RectTransform`) | (비우면 panel) | `:69` | 콤보 스킬 컨테이너. |
| `drawCountText` / `discardCountText` / `awakenCountText` (`TMP_Text`) | — | `:72-75` | 더미 카운트/각성 텍스트(`awakenCountText`는 구 `feverCountText`). |
| `awakenGauge` (`Slider`) | (자동 생성) | `:80` | 각성 게이지. |
| `awakenGaugeAnchor` (`RectTransform`) | (런타임 주입) | `:83` | 게이지 부착 기준(보통 hpBar). |
| `awakenGaugeSize` (`Vector2`) | `(300,22)` | `:86` | 자동 생성 크기. |
| `awakenGaugeOffset` (`Vector2`) | `(0,-32)` | `:89` | anchor 기준 오프셋(HP바 아래). |
| `awakenGaugeFallbackPos` (`Vector2`) | `(0,-40)` | `:91` | **anchor 미배선 시** 상단 중앙 표시 위치. |
| `awakenGaugeLabel` (`TMP_Text`) | (자동) | `:94` | 게이지 라벨. |
| `awakenChargeColor` / `awakenActiveColor` (`Color`) | 주황 / 보라 | `:97`/`:100` | 충전/발동 색. |
| `awakenHistory*` (container/itemSize/spacing/itemsPerColumn/columnSpacing/anchoredPos/maxItems) | `:105-123` | 각성 입력 히스토리 좌측 표시 파라미터. |
| `selectionPromptText` (`TMP_Text`) | — | `:145` | 선택/픽커/뷰어 안내문. |
| `dimOverlay` (`RectTransform`) | (자동) | `:147` | 선택/픽커 dim. |
| `dimColor` / `awakenDimColor` (`Color`) | `:149`/`:152` | 일반 dim / 각성 dim 색(독립). |
| `cardUsePresentEnabled` (`bool`) | `true` | `:833` | 중앙 사용 연출 ON/OFF. |
| `cardUsePresentPos/Scale/EnterTime/Hold/ExitTime` | `:835-843` | 중앙 연출 파라미터(퇴장 직전 효과 발동). |
| `picker*` (columns/columnSpacing/rowSpacing/viewportSize/contentPadding/scrollSensitivity) | `:1025-1035` | 픽커 그리드/스크롤 파라미터. |

### NewCardView (`[SerializeField]`)
- 참조: `iconImage`/`attributeImage`/`backgroundImage`/`nameText`/`descText`/`gaugeText`/`cardTypeText`/`cardTypeBackground` (`:28-37`), `autoBindOnAwake`(`:41`).
- 속성 sprite: `fire/water/wind/earth/neutralElementSprite` (`:44-48`).
- 배경 sprite: `useElementBackgroundSprite` + `fire/water/wind/earth/neutralBackgroundSprite` (`:52-57`).
- 아이콘: `cardIcons`(List), `resourcesFallback`, `useElementSpriteAsIcon` (`:60-64`).
- 크기 연출: `dragScaleMultiplier`(1.15), `hoverScaleMultiplier`(1.05), `hoverPositionOffset`(0,100) (`:68-72`).
- 드로우 등장: `drawIntroEnabled`, `drawIntroDuration`(0.28), `drawIntroRiseDistance`(240), `drawIntroStartScale`(0.92) (`:76-82`).
- 색 fallback: `tintBackgroundByElement`, `backgroundTintAlpha`(0.35), `tintCardTypeBackground` (`:85-89`).

### CardRewardUI
- **직렬화 필드 없음.** UI를 전부 런타임 생성. 내부 상수 `CHOICE_COUNT = 4` (`:18`), 보상 카드 행 간격 `spacing = 360f` (`:178`).

---

## 8. 주의 / 엣지케이스

1. **`cardPrefab` 인스펙터 배선 필수** — CardHandHUD는 `cardPrefab`을 자동 로드하지 않는다. 없으면 손패가 안 그려지고, 더미 보기(`:204`)·픽커(`:1048`)는 경고 후 폴백(픽커는 무작위 자동 선택). CardRewardUI도 `cardPrefab==null`이면 보상 생략(`cardId=-1`) (`CardRewardUI.cs:55-60`).
2. **부채꼴 겹침** — `fanLayout`에서 카드가 호로 회전·겹치므로, 뒤에 가려진 카드의 빈 영역은 클릭이 어렵다. hover/드래그 시 해당 슬롯을 최상단 sibling으로 올려 일부 완화하지만, 정지 상태에서 겹친 부분 클릭은 상위 카드가 가로챌 수 있음 (`OnPointerEnter` `NewCardView.cs:598`).
3. **선택/연출 중 입력 차단** — `TryUseFromDrag`/`TryUseFromClick`은 `IsSelectionMode || IsPickerMode || IsViewerMode`면 false (`CardHandHUD.cs:812`, `:822`). NewBattleController도 D드로우/카드사용 분기에서 이 모드들을 게이트(`NewBattleController.cs:602`, `:936`). `TogglePileViewer`는 선택/픽커 중 무시 (`:192`).
4. **각성 게이지 anchor 미배선** — hpBar RectTransform이 주입되지 않으면 게이지가 화면 정중앙(가려짐) 대신 상단 중앙(`awakenGaugeFallbackPos`)에 표시된다(완전 미표시는 아님). hpBar 배선되면 fallback 무시 (`EnsureAwakenGauge` `:415-421`).
5. **사용 연출과 효과 실행 타이밍** — 일반 연출은 카드가 **사라지기 시작할 때** `onDisappear`(=`ResolveCardUse`)를 호출하므로 효과가 ~`enter+hold` 만큼 지연된다. 전투가 그 사이 끝나면 `CancelCardUsePresentations`로 콜백 없이 취소해야 보상 화면과 겹치지 않음 (`:835-843`, `:884`, `NewBattleController.cs:335`).
6. **카드 사용 후 잔여 상태** — `SetCard`에서 카드가 바뀌면 hover/drag/등장연출 상태와 슬롯 sibling을 리셋한다(특히 더블클릭 직후 마우스가 같은 위치일 때 정렬 어긋남 방지) (`NewCardView.cs:185-197`).
7. **연출 카드 풀 독립** — 중앙 사용 연출/픽커/뷰어 카드는 손패 풀(`_cardViews`)과 별개의 임시 인스턴스라 `Refresh`의 슬롯 복원 영향을 받지 않으며, 종료 시 `Destroy`로 정리된다 (`CardUsePresentationRoutine` `:898`, `ClearPickerViews` `:1145`).
8. **CardRewardUI는 `Time.timeScale`을 0으로 만든다** — `Present` 동안 게임 정지, `Finish`에서 반드시 1로 복구(`:71`, `:226`). 중복 `Present` 호출 시 기존 패널 `Cleanup` 후 재구성 (`:51`).
9. **콤보 슬롯 자동 바인딩** — `comboSlotImages`가 비면 `comboSlotPanel` 자식에서 Image 3개를 자동 탐색하지만, 패널/자식이 없으면 경고만 남기고 갱신 실패 (`EnsureComboSlotImagesBound` `:638`, `UpdateComboSlot` 경고 `:604`).
10. **`Bind` 재호출 안전** — 기존 덱 `OnPileChanged` 구독을 먼저 해제하고 새로 구독한다(`:251-253`). `OnDestroy`에서도 해제(`:257-260`).
