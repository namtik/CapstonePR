# 전투 시스템 개편 프로토타입 — 인수인계 문서

## 0. 한 줄 요약
기존 전투(ElementSlotSystem 4슬롯 + ComboSystem)를 **카드 기반(손패 5장 드래그 + 피버/콤보 + 연쇄)** 으로 교체 중. 본 게임 흐름은 건드리지 않고 **격리된 테스트 씬**에서 검증하는 단계.

근거 문서: `전투시스템 변경.pdf`, `카드DBtest.xlsx` (둘 다 사용자 Downloads 폴더).

---

## 1. 환경
- Unity **6000.3.1f1**
- 프로젝트 루트: `D:\CapstonePR`
- 테스트 씬: `Assets/Scenes/BattleTestScene.unity` (SampleScene 복사본)
- 캔버스: Screen Space - Camera, CanvasScaler "Scale With Screen Size", Reference 1920×1080
- 카드 프리팹: `Assets/Prefab/NewSkillCard.prefab` (root scale=1, 300×400)
- 콤보 스킬 항목 프리팹: `Assets/Prefab/SkillListItem.prefab`

---

## 2. 새 시스템 파일 (`Assets/Script/Battle/`)

| 파일 | 역할 |
|------|------|
| `BattleTestController.cs` | 테스트 진입점. 인스펙터로 플레이어 HP / 적 / 덱 / 라운드 설정 후 **한 전투만** 실행. `[DefaultExecutionOrder(-1000)]`로 레거시 시스템 사전 비활성화 |
| `NewBattleController.cs` | 전투 핵심 — 덱/드로우/피버/콤보/방해/저주/연쇄/게이지 |
| `ComboSkillDef.cs` | 콤보 스킬 정의 (slot1/2/3 속성 + 효과). `ComboEffectType` enum |
| `Card/CardEnums.cs` | `CardElement`(Fire/Water/Wind/Earth/Neutral), `CardType`(Attack/Skill/Power) |
| `Card/CardData.cs` | `CardData`(정적 정의) + `CardInstance`(런타임, cursed/transient 플래그) |
| `Card/CardDatabase.cs` | **카드 91종 전체 정의**(카드DBtest.xlsx 기준, ID 100~424) + 시작 덱(`DefaultPrototypeDeckEntries`) + 파편 생성 |
| `Deck/CardDeckSystem.cs` | 뽑을/패/버린/소멸 더미 관리, 드로우, 셔플, 패 한도(5) |
| `Deck/CardEffectResolver.cs` | 카드 효과 실행(ID별 switch) + 파워/연쇄 컨텍스트(`CardEffectContext`) |
| `UI/NewCardView.cs` | NewSkillCard 프리팹 표시 + 드래그/hover/클릭. 자식 이름 자동 바인딩 |
| `UI/CardHandHUD.cs` | 5장 손패 HUD(fan layout) + 콤보 슬롯/스킬 UI + 카드 선택 모드 |

### 수정한 기존 파일 (모두 "새 시스템 모드" 가드 추가 — 본 게임 영향 없음)
- `Round/Roundmanager.cs` — `IsNewBattleSystemActive()`로 ElementSlot/Combo 자동 셋업·스타터 스킬 스킵
- `Round/CombatStageController.cs` — 새 시스템이면 레거시 덱/콤보 초기화 스킵
- `Enemy/EnemyController.cs` — 화상 처리 / 공격 시퀀스(16·18·40) / 방해행동 위임 / 공격 예고
- `Enemy/EnemyView.cs` — `SetAttackPreviewDamage()` (공격 예고 텍스트)
- `UI/SkillListItemUI.cs` — `SetupForCombo()` (콤보 스킬 표시)

---

## 3. 핵심 동작

### 진입 흐름
```
Play → BattleTestController.Awake (레거시 ElementSlot/Combo/HUD/CardSystem 비활성)
     → Start (combatStage 활성, 다른 스테이지 숨김)
     → StartTestBattle (delay) → Roundmanager.StartRound(인스펙터 라운드)
     → 1~2프레임 후 NewBattleController.StartBattle()
        → 덱 빌드(인스펙터 startingDeck 우선, 없으면 기본 덱)
        → CardHandHUD 자동 생성/바인딩, EventSystem 보장
        → 5장 드로우
```

### 카드 사용
- 손패 카드를 **위로 드래그**(스크린 Y > `useThresholdY`) → 사용
- 카드별 게이지 → `EnemyStat.ConsumeGaugeStep()`로 적 행동 게이지 누적
- 효과는 `CardEffectResolver.Resolve(card)` → `ResolveResult`(소멸/파워잔류/선택요구/게이지스킵/재사용 등)

### 카드 선택 모드 (불13/112 소멸, 물8/207 버리기)
- 효과가 패에서 카드 선택을 요구하면 `CardHandHUD.EnterSelectionMode`
- **화면 dim 오버레이**(`dimColor`)로 어둡게 + 손패를 오버레이 위로 올려 강조, 안내 텍스트 표시
- 카드 클릭(`NewCardView.OnPointerClick` → `Hud.OnCardClicked`) → 콜백 발동 → `ExitSelectionMode`(오버레이 해제)
- 오버레이 `raycastTarget=true`로 선택 중 다른 UI 오클릭 차단

### 키 입력
- **D**: 1장 드로우 + 적 게이지 +1 (손패 가득이면 불가)
- **F**: 피버 발동 (충전됐을 때만)

### 피버 (PDF 명세)
- 속성 카드 **15회** 입력 → 충전(`FEVER READY`)
- F 발동 → 적 게이지 정지, 무속성 카드 격리, **콤보 슬롯/스킬 패널 활성화**, 모든 카드 효과 "드로우 1"로 치환
- 속성 카드 **5회** 입력 시 종료 (콤보 발동 시 입력 차감 X)
- 종료 → 무속성 복원, 콤보 UI 숨김
- 콤보: 피버 중 사용한 속성을 3칸 슬롯에 누적 → `ownedComboSkills`와 매칭되면 발동(이번 사이클 1회). 모든 콤보 발동 시 강제 종료

### 행동 게이지 (EnemyStat)
- `GAUGE_MAX_STEPS = 20` (const). 카드 게이지가 누적되어 **20** 도달 시 적 공격
- **중간 패턴(방해 행동)은 10**(= `GAUGE_MAX_STEPS / 2`)에서 발동
- 이 const는 본 게임 적에도 공통 적용됨(새 시스템 전용 아님)

### 방해 행동 (게이지 10 = 중간 도달, `EnemyController.HandleMidPattern` → `NewBattleController.TriggerDisruption`)
- 50% 무속성 카드 1장 뽑을 더미에 삽입 / 50% 무작위 속성 **저주**
- 저주: 해당 속성 카드 사용 시 플레이어 -5, **카드 사용 2회 동안** 지속. 저주된 카드는 보라 틴트

### 적 공격 (`EnemyController`, 새 시스템 모드)
- 행동 게이지 **20** 도달 → **화상 먼저 발동**(적이 화상 스택만큼 자해 + 스택 절반) → 공격
- 공격 피해: `newSystemAttackSequence`(인스펙터, 현재 `{8, 9, 20}`) 순환, `EnemyView.attackPreviewText`에 다음 피해 예고

### 연쇄 (Chain)
- 공격 카드로 피해를 줄 때 **각 타격마다**: **(1) 이전에 쌓인 연쇄 발동**(1 소비 + 추가 피해 `1 + chainBonusDamage`) → **(2) 318이 깔려있으면 연쇄 +1**
- 순서가 발동→획득이라 **방금 얻은 연쇄는 다음 타격/공격부터** 발동
- 멀티히트 카드(2x3 등)는 `DealDamage(amount, baseHits:N)` → **각 타격마다 연쇄 발동**(연쇄 보유량만큼)
- 무한 연쇄 방지: 연쇄 추가 피해는 318을 재트리거하지 않음

### 손패 Fan Layout (`CardHandHUD`)
- 가상 원 위에 카드 배치(`fanRadius`/`fanArcAngle`/`fanVerticalDip`), 각 카드 회전
- 카드 수가 적으면 가운데 정렬(`PositionFanForCount`)
- 드래그 중엔 똑바로, 놓으면 슬롯 회전 복귀
- hover 시 위로 `hoverPositionOffset`(기본 100) + 최상단 렌더

---

## 4. 카드 효과 구현 현황

**카드는 91종 모두 CardDatabase에 등록되어 손패에 정상 표시됨.** 단 효과(CardEffectResolver)는 일부만 구현.

### 효과 구현 완료 ID
`104, 106, 107, 112, 115, 116, 118, 121` (불)
`201, 203, 207, 208, 211, 216, 218, 220, 221` (물)
`306, 308, 309, 312, 313, 314, 317, 318, 320, 321` (바람)
`400, 401, 407, 411, 414, 420` (땅)
`422, 423, 424` (파편)

### 기본 테스트 덱 (`CardDatabase.DefaultPrototypeDeckEntries`, 사용자 지정)
- 불: 106×1, 107×1, 115×1, 116×1, 118×1
- 물: 203×2, 207×1, 211×1, 216×1, 218×1, 220×1, 221×1
- 바람: 308×2, 312×1, 313×2, 318×1, 320×1
- → 이 덱의 효과는 **전부 구현됨**

### 미구현 / 제약
- 위 목록 외 카드(예: 100~103, 105, 108~111, 113, 114, 117, 119, 120, 200, 202, 204~206, 209, 210, 212~215, 217, 219, 300~305, 307, 310, 311, 315, 316, 319, 402~406, 408~410, 412, 413, 415~419, 421)는 **default case = 효과 없음**(게이지만 누적)
- `315`(바람16, 연쇄=공격 카드 사용 수): 공격 카드 카운터 미구현
- `211`(물12, 마지막 카드 재사용): 단순 재호출 — 마지막이 파워면 중복 적용 가능
- 연쇄는 318만 덱에 있으므로 "공격마다 +1" 형태. 306/314로 모아두는 플레이는 덱에 넣어야 확인 가능

---

## 5. Unity 인스펙터 세팅 체크리스트

### BattleTestController (씬 GameObject)
- Use New Battle System ✅
- Player Max Hp / Start Hp / Attack Damage
- Round Type (Combat/Elite/Boss), Difficulty Config, Column Index, Enemies(EnemyData)
- Starting Deck — 비우면 기본 덱 자동(OnValidate). 직접 ID+수량 편집 가능
- New Battle Controller 참조 (없으면 자동 생성)

### NewBattleController
- Hand Hud 참조
- **Owned Combo Skills** — 콤보 스킬 리스트(displayName, slot1/2/3, effect, amount)

### CardHandHUD
- Card Prefab → `NewSkillCard.prefab`
- Combo Slot Panel / Combo Skill Panel (피버 시 활성)
- Combo Slot Images (비우면 패널 자식에서 자동 탐색)
- **Combo Skill Item Prefab → `SkillListItem.prefab`**, Combo Skill Item Container
- Fan Radius / Arc / Dip (카드 scale=1이라 간격 조정 필요할 수 있음)
- Selection Prompt Text (카드 선택 안내, 옵션)
- **Dim Overlay / Dim Color** (선택 모드 화면 어둡게 — 비우면 자동 생성, 알파로 어둡기 조절)

### EnemyController (적 프리팹)
- **New System Attack Sequence** — 공격 피해 순환값(현재 `{8, 9, 20}`). 적별로 조정 가능

### NewSkillCard 프리팹의 NewCardView
- 속성 sprite 5종(attributeImg), 배경 sprite 5종(Background) 매핑
- Use Element Sprite As Icon ✅ (카드 아이콘 미완성 임시)
- Use Element Background Sprite ✅
- Drag/Hover Scale, Hover Position Offset
- 카드 타입 텍스트(cardType) / 배경(baseCardType) 자동 바인딩

---

## 6. 다음 작업 후보 (우선순위 순)
1. **Play 검증**: 기본 덱으로 한 전투 — 카드 표시/드래그/효과/피버/연쇄/방해/적 공격 시퀀스 확인
2. **게이지 20 밸런스**: 한 사이클에 카드를 더 써야 적이 공격하므로, 카드 게이지값·D드로우 비중·공격 시퀀스(`{8,9,20}`) 재조정 검토
3. 콤보 스킬 데이터(`ownedComboSkills`) 실제 밸런스로 채우기
4. 나머지 카드 효과 구현 (덱 확장 시 필요한 것부터)
5. 연쇄 UI 표시 (현재 로그만 — `_ctx.chainCount`)
6. 더미 카운트/상태 UI 폴리싱
7. 검증 끝나면 본 게임 씬(SampleScene)에 마이그레이션 전략 결정

---

## 7. 디버그 팁
- `NewBattleController.logVerbose` ON이면 카드 사용/피버/콤보/저주/연쇄 로그 출력
- 카드 사용 로그: `{카드명} 사용 — 게이지+N, 연쇄=N`
- 연쇄 발동 로그: `[연쇄] 발동 — 추가 피해 N, 남은 연쇄 N`
- 적 공격 로그: `[적 공격] 시퀀스 인덱스=N, 피해=N`
- 누락 카드 경고: `[CardDatabase] 알 수 없는 카드 ID N 무시` → CardDatabase에 해당 ID 없음

---

## 8. 주의사항
- 새 시스템은 **`NewBattleController.Instance != null`** 로 본 게임과 분리됨. 기존 파일 수정은 전부 이 가드 안에서만 동작
- `SkillListItemUI.cs`, `Player.cs` 등 일부 기존 파일은 인코딩이 깨져 보일 수 있음(EUC-KR). Edit 시 Write로 전체 재작성하는 게 안전
- 카드 ID 체계가 옛 22종 → 새 91종으로 바뀌면서 일부 ID 의미가 달라짐(예: 400 = 옛 "방어도3" → 새 "땅1 피해5"). CardEffectResolver는 새 DB 기준
