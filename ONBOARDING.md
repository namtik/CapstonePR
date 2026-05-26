# 전투 시스템 개편 프로토타입 — 인수인계 문서

## 0. 한 줄 요약
기존 전투(ElementSlotSystem 4슬롯 + ComboSystem)를 **카드 기반(손패 5장 + 피버 타이머/콤보 큐 + 연쇄)** 으로 교체 중. 본 게임 흐름은 건드리지 않고 **격리된 테스트 씬**에서 검증.

**카드 DB는 Excel/JSON 기반의 데이터 드리븐 엔진** — 기획자가 `Card_DB.xlsx`만 수정하면 변환기를 거쳐 게임에 반영. 코드 수정 없이 효과 수치/조건/공식 조정 가능.

근거 문서: `전투시스템 변경.pdf`, `Card_DB.xlsx` (사용자 Downloads 폴더).

---

## 1. 환경
- Unity **6000.3.1f1**
- 프로젝트 루트: `D:\CapstonePR`
- 테스트 씬: `Assets/Scenes/BattleTestScene.unity` (SampleScene 복사본)
- 캔버스: Screen Space - Camera, CanvasScaler "Scale With Screen Size", Reference 1920×1080
- 카드 프리팹: `Assets/Prefab/NewSkillCard.prefab` (root scale=1, 300×400)
- 콤보 스킬 항목 프리팹: `Assets/Prefab/SkillListItem.prefab`

---

## 2. 카드 DB 파이프라인 (Excel → JSON → 게임)

### 폴더 구조
```
Tools/ConvertCardDB.py                — Python 컨버터 (CLI)
Assets/Editor/CardDBImporter.cs       — Unity Editor 메뉴 컨버터
Assets/Resources/CardDB/Cards.json    — 카드 메타데이터 (자동 생성)
Assets/Resources/CardDB/CardEffects.json — 효과 정의 (자동 생성)
Assets/Resources/CardIcons/{SkillImg}.png — 카드 아이콘 (Inspector 추후 배치)
Assets/Resources/CardEffects/{EffectName}.png — 효과 스프라이트 시트
```

### 기획자 갱신 워크플로우
**권장**: Unity 에디터 메뉴
- `Tools > Card DB > Excel → JSON 변환...` (파일 다이얼로그)
- `Tools > Card DB > 마지막 파일 다시 변환` (원클릭 재변환)
- `Tools > Card DB > 출력 폴더 열기`

**대안**: CLI — `python Tools/ConvertCardDB.py [엑셀 경로]`

### Card_DB.xlsx 시트 구조
- **Cards**: CardID, Name, Element, CardType, GaugeCost, Tags, ComboSlot, EffectName, SkillImg, DescriptionKR
- **CardEffects**: CardID, EffectIndex, When, If, Do, Target, Amount, Formula, Hits, HitFormula, Status, CardFilter, FromZone, ToZone, Select, Repeat, Extra, RuntimeKey
- **키값_한글정리**: 코드값 사전(BURN/CHAIN/CURSE 등)

### 카드 ID 체계
- FIRE 100~121 (22장)
- WATER 200~221 (22장)
- WIND 300~321 (22장)
- EARTH 400~421 (22장)
- NEUTRAL 500 (방해 행동용 더미 1장)
- FRAGMENT 501~503 (땅 카드가 생성하는 파편 3장)
- 총 **92장**, 효과 약 **119건**

---

## 3. 새 시스템 파일 (`Assets/Script/Battle/`)

| 파일 | 역할 |
|------|------|
| `BattleTestController.cs` | 테스트 진입점. 인스펙터로 HP/적/덱/라운드 설정 후 한 전투만 실행. 레거시 시스템 사전 비활성화 |
| `NewBattleController.cs` | 전투 핵심 — 덱/드로우/피버 타이머/콤보 큐/방해/저주/연쇄/게이지/이펙트 트리거 |
| `ComboSkillDef.cs` | 콤보 스킬 정의(slot1/2/3 속성 + ComboEffectType) |
| `Card/CardEnums.cs` | `CardElement`(Fire/Water/Wind/Earth/Neutral/Fragment), `CardType`(Attack/Skill/Power) |
| `Card/CardData.cs` | `CardData`(JSON 매핑: id/name/element/type/gauge/tags/comboSlot/effectName/skillImg/description) + `CardInstance`(transient/cursed/gaugeSinceDrawn/justDrawn) |
| `Card/CardEffectData.cs` | CardEffects.json 한 줄(When/If/Do/Formula/...) 매핑 DTO |
| `Card/CardDatabase.cs` | **Resources/CardDB/*.json 자동 로드**. FRAGMENT_1/2/3=501/502/503, NEUTRAL_FILLER=500 |
| `Deck/CardDeckSystem.cs` | 4더미 + 패 관리. `HandLimit` 동적, `MAX_HAND_LIMIT=10`. `OnCardDrawn` 이벤트, `MoveFromDrawPileToHand`/`MoveFromDiscardToDrawPileTop`/`AddToHand`/`IncreaseHandLimit` 등 |
| `Deck/CardEffectResolver.cs` | **데이터 드리븐 효과 엔진**. When/If/Do/Formula 인터프리터 + 파워 트리거 자동 등록 |
| `UI/NewCardView.cs` | 카드 표시 + 드래그/hover/클릭. **더블클릭 사용 지원** |
| `UI/CardHandHUD.cs` | 손패 HUD (fan, 최대 10슬롯) + 콤보 UI + 카드 픽커 모드 + 피버 히스토리 + 피버 dim |
| `UI/CardEffectAnimator.cs` | 스프라이트 시트 프레임 단위 재생, 끝나면 자동 파괴 |
| `UI/CardEffectOverlay.cs` | 카드 사용 시 EffectName으로 시트 자동 로드/재생. 위치/크기 오버라이드 + Earth_ATK/Heal_EFF 특수 처리 |
| `UI/PlayerDamageOverlay.cs` | 피격 시 화면 빨간 플래시 + 캔버스 자식 셰이크 + HP 바 깜빡임 |

### 수정한 기존 파일 (모두 새 시스템 모드 가드)
- `Round/Roundmanager.cs` — `IsNewBattleSystemActive()`로 ElementSlot/Combo 자동 셋업 스킵
- `Round/CombatStageController.cs` — 새 시스템이면 레거시 덱/콤보 초기화 스킵
- `Enemy/EnemyController.cs` — 화상 발동(`isBurn`로 화상 데미지 색 분리) / 공격 시퀀스 / 방해행동 위임
- `Enemy/EnemyView.cs` — 데미지 텍스트 강화(멀티히트 인스턴스 복제, 색=리치텍스트 강제, X/Y 랜덤 오프셋, burst→base 스케일, 화상=빨강)
- `Player.cs` — `OnPlayerHit`/`OnBlockConsumedByAttack`/`OnHpDecreased(int)` 이벤트 추가

---

## 4. 데이터 드리븐 효과 엔진

### 지원 동사 (CardEffects.Do)
DAMAGE, GAIN_BLOCK, HEAL, LOSE_HP, DRAW_CARD, DISCARD_HAND, DISCARD_CARD,
EXHAUST_HAND, EXHAUST_CARD, APPLY_STATUS, GAIN_STATUS, GAIN_STAT,
ADD_CARD, MOVE_CARD, COPY_CARD, COPY_LAST_CARD_EFFECT, CLEANSE_STATUS,
SET_GAUGE_COST, SET_NEXT_CARD_FREE, MODIFY_HAND_LIMIT, MODIFY_HIT_COUNT,
MODIFY_DAMAGE_MULTIPLIER, No_EFFECT — **23종 전부 처리**

### 지원 트리거 (When) — 16종
- 즉시: `ON_USE`, `ON_USE_BEFORE_GAUGE`
- 파워 등록형: `ON_CARD_USED`, `ON_CARD_USED_BEFORE_GAUGE`, `ON_ELEMENT_CARD_USED`, `ON_FRAGMENT_CARD_USED`, `ON_ATTACK_CARD_DAMAGE_CALC`, `ON_CARD_DAMAGE_HIT_DEALT`, `ON_BLOCK_CONSUMED_BY_ATTACK`, `ON_PLAYER_HEALED_ACTUAL`, `ON_PLAYER_HIT_INCLUDING_BLOCK`, `ON_SELF_EXHAUST`, `ON_CARD_EXHAUSTED`, `ON_CARD_DISCARDED_BY_CARD_EFFECT`, `ON_CURSE_CLEANSED`, `ON_BURN_APPLIED_BY_CARD_EFFECT`

### 지원 조건 (If) — 12종
PLAYER_HP_FULL, PLAYER_HP_RATE_LTE:N, ENEMY_STATUS_GTE:STATUS:N, PLAYER_BLOCK_EQ:N,
CARD_ELEMENT_EQ:*, PREVIOUS_CARD_TYPE_EQ:*, FIRST_CARD_AFTER_ENEMY_ATTACK,
USED_IMMEDIATELY_AFTER_DRAW (per-card `justDrawn`), LAST_USED_CARD_EXISTS_EXCLUDING_SELF,
DAMAGE_SOURCE_IS_CARD_EFFECT, EXCLUDE_TRIGGER_SOURCE

### 지원 공식 (Formula / HitFormula)
ENEMY_STATUS_VALUE:BURN, PLAYER_BLOCK, FLOOR(PLAYER_CURRENT_HP/2),
LAST_HP_LOST(*N), GAUGE_SINCE_DRAWN (per-card), USED_ATTACK_CARD_COUNT,
USED_FRAGMENT_CARD_COUNT, LAST_DISCARDED_COUNT, IF_PLAYER_HP_RATE_LT_50_MULTIPLY_2,
USED_CARD_NAME_COUNT:CARDNAME (HitFormula 전용)

### 런타임 통계 (CardEffectContext)
`lastHpLost`, `lastDiscardedCount`, `usedAttackCardCount`, `usedFragmentCardCount`,
`previousCardType`, `firstCardAfterEnemyAttack`, `nextCardIsFree`,
`currentCardJustDrawn`, `usedCardNameCounts` Dictionary, `handLimitBonus`, `firstCardAfterAttackFreeActive`

---

## 5. 핵심 동작

### 진입 흐름
```
Play → BattleTestController.Awake (레거시 시스템 비활성)
     → StartTestBattle → Roundmanager.StartRound
     → NewBattleController.StartBattle()
        → 덱 빌드(인스펙터 startingDeck 우선, 없으면 기본 덱)
        → CardHandHUD/CardEffectOverlay/PlayerDamageOverlay 자동 생성
        → Player 이벤트 구독(OnPlayerHit, OnHpDecreased), EnemyStat.OnGaugeFull 구독
        → 5장 드로우
```

### 카드 사용 — 2가지 방법
- **드래그**: 화면 위로 올림(Y > `useThresholdY`)
- **더블클릭**: 손패 위 카드를 더블클릭

선택/픽커 모드 중에는 사용 차단. 효과는 `CardEffectResolver.Resolve(card)`가 데이터로부터 해석.

### 카드 픽커 / 선택 모드
- **EnterSelectionMode** (손패 1장 클릭): 209/103/112(소멸), 207(버리기), 213(복사). `cardFilter` 지원(NEUTRAL_CARD/FRAGMENT_CARD/ATTACK_CARD/ANY_CARD)
- **EnterCardPickerMode** (별도 그리드 UI): 206(뽑을 더미), 210(버린 더미), 409(파편 1장 패), 414(파편 1장 선택 → 같은 ID 10장 버린 더미)
- 선택 UI는 직전 효과(예: 207의 DRAW)가 먼저 시각화되도록 **1프레임 deferred coroutine** 호출

### 키 입력
- **D**: 1장 드로우 + 적 게이지 +1 (손패 가득이면 불가, 더미 고갈 시 진단 로그)
- **F**: 피버 발동 (충전됐을 때만)

### 피버 (시간 기반)
- 속성 카드 **15회** 입력 → 충전 (`FEVER READY`)
- F 발동 → **10초 타이머 시작**, 적 게이지 정지, 무속성/파편 격리, 콤보 UI 활성화, 모든 카드 효과 "드로우 1"로 치환
- 카드 사용 시 콤보 슬롯에 입력 누적(슬라이딩 윈도우 3장)
  - 매칭 시 **큐에 적재**(즉시 발동 X), **+0.5초 보너스**
  - **슬롯은 클리어하지 않음** — 슬라이딩 윈도우 계속 진행
- 카운트다운 0 도달 또는 모든 콤보 매칭 → EndFever
  - **큐에 쌓인 모든 콤보를 일괄 발동** → 격리 해제 → 손패 한도 정리
- **피버 dim 오버레이**: 화면 어두워지고(보라톤) 손패/콤보/히스토리는 dim 위에서 강조됨
- **피버 입력 히스토리** (왼쪽 세로): 8개씩 컬럼, 8개 넘으면 오른쪽 새 열로 wrap (최대 64장)

### 행동 게이지 (EnemyStat)
- `GAUGE_MAX_STEPS = 20`, 중간 패턴(방해) = 10
- 가운데 도달 → `EnemyController.HandleMidPattern` → `NewBattleController.TriggerDisruption`
- 가득 → 적 공격. 공격 직전 **화상 자동 발동**(스택만큼 자해 + 절반 감소, 화상 데미지는 **빨간 텍스트**)

### 방해 행동
- 50% 무속성(500) 카드 1장 뽑을 더미에 삽입 / 50% 무작위 속성 **저주**
- 저주: 해당 속성 카드 사용 시 플레이어 -5, **카드 사용 2회 동안** 지속. 저주된 카드는 보라 틴트

### 적 공격 (`EnemyController`)
- 행동 게이지 20 → 화상 먼저 → 공격
- `newSystemAttackSequence`(현재 `{8, 9, 20}`) 순환, `EnemyView.attackPreviewText`에 다음 피해 예고

### 연쇄 (Chain)
- 공격 카드 타격마다 (1) 이전 연쇄 발동(1 소비 + `1 + chainBonusDamage` 추가 피해) → (2) 318 활성 시 연쇄 +1
- 멀티히트(`Hits`)는 각 타격마다 연쇄 발동
- 무한 연쇄 방지: 연쇄 추가 피해는 318을 재트리거하지 않음

### 손패 Fan Layout
- 슬롯 사전 생성 **10개**(MAX_HAND_LIMIT, 동적 패 한도 대응), 표시 위치는 5장 기준 간격
- `fanArcAngle = 40°`(권장), `fanRadius = 1800`, `slotSpacing.x = 700`
- 카드 수가 적으면 가운데 정렬, 더블클릭 사용 가능

---

## 6. 시각 피드백 시스템

### 카드 아이콘
- 1순위: `Resources/CardIcons/{Card.data.skillImg}.png` (예: `100Img.png`)
- 폴백: Inspector 매핑 → `Resources/CardIcons/{cardId}.png` → 속성 sprite (`useElementSpriteAsIcon`)

### 카드 사용 이펙트 (CardEffectOverlay)
- 카드 사용 시 `Card.data.effectName`으로 `Resources/CardEffects/{EffectName}.png` 자동 로드
- 스프라이트 시트(다중 슬라이스)를 프레임 단위로 재생(`defaultFps`, 보통 12~24)
- **위치 결정**:
  1. `positionOverrides` Inspector 리스트
  2. Earth_ATK 전용 위치 (땅 공격은 아래)
  3. *_ATK, Burn_*, Chain_* → 적 위치
  4. 그 외(*_DEF, *_POW, Heal_EFF) → 플레이어 위치
- **크기 결정**:
  1. `sizeOverrides` Inspector 리스트
  2. Heal_EFF 전용 (높이만 사용, 가로는 자동 stretch)
  3. 기본 `effectSize`
- **Heal_EFF 특수 모드**: 화면 하단 풀폭 띠 — anchor stretch (0,0)-(1,0), pivot 하단, preserveAspect=false
- 시트가 없으면 조용히 스킵 (이펙트 미완성 안전)

### 데미지 텍스트 (EnemyView)
- 히트마다 **TMP_Text 인스턴스 복제** — 5x5 같은 멀티히트가 각자 떠오름
- 색은 **리치텍스트 `<color>` 태그**로 박아넣음 (TMP 머티리얼/그래디언트 우회)
- **일반**: 노란색 / **화상(`isBurn=true`)**: 빨간색
- 출현 직후 `burstScale → baseScale`로 임팩트, 위로 floating, CanvasGroup으로 페이드
- `randomXOffset` / `randomYOffset`로 X/Y 분산 (5x5 시 겹침 방지)

### 플레이어 피격 (PlayerDamageOverlay)
모든 피격(자해 LOSE_HP 포함 — Player.OnHpDecreased) 시 동시 발동:
1. **화면 빨간 플래시** — 풀스크린 Image 페이드 (데미지 크기에 비례)
2. **화면 흔들기** — Screen Space - Camera 호환을 위해 **캔버스 자식들을 다중으로 흔듦** (카메라 흔들기는 게임뷰에 안 보여서 변경)
3. **HP 바 깜빡임** — Player.hpBar.fillRect 자동 탐색, 흰색 → 원본 색 Lerp

각각 Inspector에서 ON/OFF 가능. 컴포넌트 파괴 시 OnDestroy에서 원위치 복원.

---

## 7. Unity 인스펙터 세팅 체크리스트

### BattleTestController (씬 GameObject)
- Use New Battle System ✅
- Player Max Hp / Start Hp / Attack Damage
- Round Type (Combat/Elite/Boss), Difficulty Config, Column Index, Enemies(EnemyData)
- Starting Deck — 비우면 `CardDatabase.BuildDefaultPrototypeDeck` 자동
- New Battle Controller 참조 (없으면 자동 생성)

### NewBattleController
- Hand Hud / Card Effect Overlay / Player Damage Overlay 참조 (비우면 자동 생성)
- **Owned Combo Skills** — 콤보 스킬 리스트(displayName, slot1/2/3, effect, amount)

### CardHandHUD
- Card Prefab → `NewSkillCard.prefab`
- Combo Slot Panel / Combo Skill Panel
- Combo Skill Item Prefab → `SkillListItem.prefab`
- Fan Radius / Arc / Slot Spacing
- **Fever Dim Color** (피버 색)
- **Fever History 옵션**: 컬럼 wrap 항목 수 (8), 컬럼 간격 (8), 최대 표시 (64)

### CardEffectOverlay
- Enemy/Player Anchored Pos (효과 위치)
- Earth_ATK / Heal_EFF 전용 옵션
- Position Overrides / Size Overrides (effectName 기반 미세조정)
- Default FPS (시트 프레임 수에 맞춰)

### PlayerDamageOverlay
- 화면 플래시 / 카메라 셰이크(=캔버스 자식 셰이크) / HP 바 깜빡임 각각 ON/OFF + 색/지속 조정

### EnemyController (적 프리팹)
- **New System Attack Sequence** — 공격 피해 순환값

### NewSkillCard 프리팹의 NewCardView
- 속성 sprite 5종(attributeImg), 배경 sprite 5종(Background)
- Use Element Sprite As Icon — 카드 아이콘 미완성 임시
- Drag/Hover Scale, Hover Position Offset

---

## 8. 다음 작업 후보

1. **카드 아이콘 / 효과 스프라이트 시트 채우기** — `Resources/CardIcons/`, `Resources/CardEffects/`에 아트 추가하면 즉시 표시됨
2. **콤보 스킬 데이터(`ownedComboSkills`) 실제 밸런스로 채우기**
3. **밸런스 조정**: 게이지 시퀀스(`{8,9,20}`), 카드 게이지값, 피버 충전/지속 시간, 콤보 시간 보너스
4. **연쇄 UI 표시** (현재 `_ctx.chainCount` 로그만)
5. **상태이상 UI** — 플레이어/적 화상/저주 표시 강화
6. **사운드** — 카드 사용, 피버 발동/종료, 콤보 발동, 피격 사운드
7. **검증 끝나면 본 게임 씬(SampleScene)에 마이그레이션 전략 결정**

---

## 9. 디버그 팁

### 로그 토글
`NewBattleController.logVerbose` ON이면 카드 사용/피버/콤보/저주/연쇄 로그 출력.

### 주요 로그 패턴
```
[NewBattle] {카드명} 사용 — 게이지+N, 연쇄=N
[연쇄] 발동 — 추가 피해 N, 남은 연쇄 N
[적 공격] 시퀀스 인덱스=N, 피해=N
[피버] 발동! 격리된 무속성=N, 지속 시간=10s
[피버] ⏱ 콤보 매칭 → 시간 +0.5s (X.XXs → Y.YYs)
[콤보 큐] {스킬명} ({콤보문자열}) — 피버 종료 시 발동
[피버] 종료 — 콤보 N건 일괄 발동
[CardDatabase] 알 수 없는 카드 ID N 무시   ← JSON 누락
[CardEffect] 미구현 카드 id=N             ← 효과 정의 없음
[CardEffect] 알 수 없는 조건 'XXX'         ← If 미지원
[ShowDamage] dmg=N isBurn=True → 색=RGBA(...) ← 화상 색 검증
[DeckSystem] 드로우 실패 — 더미 비어있음   ← D키 무반응 원인
```

---

## 10. 주의사항

- 새 시스템은 `NewBattleController.Instance != null` 로 본 게임과 분리됨. 기존 파일 수정은 전부 이 가드 안에서만 동작
- 일부 기존 파일(`SkillListItemUI.cs`, `Player.cs` 등)은 인코딩이 EUC-KR로 깨져 보일 수 있음 — 수정 시 UTF-8로 전체 재작성 권장
- 카드 ID 체계가 옛 22종(파편 422~424) → 새 92종(무속성 500 + 파편 501~503)으로 바뀜. `CardDatabase.FRAGMENT_1/2/3`은 신 ID 사용
- 데이터 드리븐 엔진이 모르는 동사/조건/공식이 나오면 `Debug.LogWarning`만 출력하고 효과 스킵 — 게임 진행 영향 X
- 손패 한도 동적 변경(418)은 `MAX_HAND_LIMIT=10`에서 상한 — UI 슬롯도 10개까지 사전 생성
