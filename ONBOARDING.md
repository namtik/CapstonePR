# 전투 시스템 개편 프로토타입 — 인수인계 문서

> 최종 갱신: 유물 시스템 + 콤보 스킬 DB(Excel→JSON 데이터 드리븐) + 콤보 인스펙터 선택 UI 반영본

## 0. 한 줄 요약
기존 전투(ElementSlotSystem 4슬롯 + ComboSystem)를 **카드 기반(손패 5장 + 각성(Awaken) + 콤보 큐 + 연쇄)** 으로 교체 중. 본 게임 흐름은 건드리지 않고 **격리된 테스트 씬**에서 검증.

**카드 DB / 콤보 DB 모두 Excel/JSON 기반 데이터 드리븐** — 기획자가 xlsx만 수정 → 변환기 → 게임 반영.

근거 문서: `기획서 0.6v.pdf`, `Card_DB_v2 (1).xlsx`, `ComboSkill_DB_(1).xlsx` (사용자 Downloads 폴더).

> ⚠️ **용어 변경**: 구 "피버(Fever)"는 전부 **"각성(Awaken)"** 으로 개명됨. 코드 식별자도 `Awaken*`/`_awaken*`, 로그·UI·주석은 "각성". 단 효과 에셋명 `Fire_ATK`처럼 `Fever_ATK`(Resources 파일명)만 그대로 둠.

---

## 1. 환경
- Unity **6000.3.1f1** (프로젝트는 6000.3 계열)
- 프로젝트 루트: `C:\Users\hp\Desktop\CapstonePR`
- 테스트 씬: `Assets/Scenes/BattleTestScene.unity`
- 캔버스: Screen Space - Camera, Scale With Screen Size 1920×1080
- 카드 프리팹: `Assets/Prefab/NewSkillCard.prefab` (root scale=1, 300×400)
- 콤보 스킬 항목 프리팹: `Assets/Prefab/SkillListItem.prefab`

---

## 2. 카드 DB 파이프라인 (Excel → JSON → 게임)

### 폴더 구조
```
Assets/Editor/CardDBImporter.cs          — Unity Editor 메뉴 컨버터 (xlsx=zip+XML 직접 파싱, 외부 의존성 X)
Assets/Resources/CardDB/Cards.json       — 카드 메타데이터 (자동 생성)
Assets/Resources/CardDB/CardEffects.json — 효과 정의 (자동 생성)
Assets/Resources/CardEffects/{EffectName}.png — 효과 스프라이트 시트
```

### 갱신 워크플로우
- `Tools > Card DB > Excel → JSON 변환...` (파일 다이얼로그)
- `Tools > Card DB > 마지막 파일 다시 변환` (원클릭)
- `Tools > Card DB > 출력 폴더 열기`

### Card_DB_v2 시트 구조
- **Cards**: CardID, Name, Element, CardType, GaugeCost, **Rarity**, Tags, ComboSlot, EffectName, SkillImg, DescriptionKR
- **CardEffects**: CardID, EffectIndex, When, If, Do, Target, Amount, Formula, Hits, HitFormula, Status, CardFilter, FromZone, ToZone, Select, Repeat, Extra, RuntimeKey

### 카드 ID 체계 (v2 = 총 112장 / 효과 159건)
- FIRE  100~126 (기본+공격/스킬 + **파워 122~126**)
- WATER 200~226 (+ **파워 222~226**)
- WIND  300~326 (+ **파워 322~326**)
- EARTH 400~426 (+ **파워 422~426**)
- NEUTRAL 500 (방해행동 더미), FRAGMENT 501~503 (파편)
- 등급: **Normal 52 / Rare 40 / Epic 20** (`CardData.rarity`. 프리팹 표시·보상 가중치는 미구현)

---

## 3. 콤보 스킬 DB 파이프라인 (Excel → JSON → 게임)

### 폴더 구조
```
Assets/Editor/ComboSkillDBImporter.cs          — Unity Editor 메뉴 컨버터 (CardDBImporter와 동일 패턴)
Assets/Resources/ComboDB/ComboSkills.json      — 콤보 정의 (자동 생성, 현재 xlsx 기준으로 이미 생성됨)
Assets/Resources/ComboDB/ComboEffects.json     — 효과 정의 (자동 생성)
```

### 갱신 워크플로우
- `Tools > Combo DB > Excel → JSON 변환...`
- `Tools > Combo DB > 마지막 파일 다시 변환`

### ComboSkill_DB 시트 구조
- **ComboSkills**: ComboID, Slot1, Slot2, Slot3, Cooldown, ComboName, DescriptionKR, **RefComboID**
- **ComboEffects**: RefComboID, EffectIndex, When, If, Do, Target, Amount, Formula, Hits, HitFormula, Status, CardFilter, FromZone, ToZone, Select, Repeat, Extra, RuntimeKey (카드 효과와 동일 스키마)

### 콤보 ID 체계 (총 64개)
- **canonical(1000~1019)**: 고유 조합 20종. 효과 정의는 이 ID 기준.
- **alias(1020~1063)**: 동일 속성 조합의 순서 변형. `RefComboID`로 canonical 효과 공유.
- 매칭은 **순서무관** — 같은 refComboId를 가진 모든 슬롯 순서 허용(`acceptedOrders`).

### 보유 콤보 설정 (NewBattleController Inspector)
- **Use Combo Database** ON (기본): 각성 시 DB 콤보 사용. Inspector 수동 리스트 덮어씀.
- **보유 콤보 선택** — 체크박스 목록으로 20종 중 원하는 콤보만 선택. 아무것도 체크 안 하면 전체 보유.
- **DB 새로고침** 버튼으로 xlsx 재변환 후 반영 가능.

### 콤보 효과 실행 (간소화판 — 단일 적 프로토타입 기준)
| 지원 | 미지원(경고 후 스킵) |
|------|---------------------|
| DAMAGE, APPLY_STATUS, GAIN_STATUS(CHAIN→연쇄), HEAL, GAIN_BLOCK, DRAW_CARD, DISCARD_HAND | MOVE_CARD, MODIFY_ENEMY_STAT, BUFF_NEXT_COMBO, SKIP_COOLDOWN |
| Formula: MAX_HP×0.1, N+TOKEN, ENEMY_STATUS_VALUE:STATUS, PLAYER_BLOCK/CHAIN/FRAGMENT/FIRE 카운트 | |
| Repeat: AWAKEN_COMBO_COUNT, RESOURCE_CONSUME_ALL(연쇄소모), AWAKEN_FRAGMENT_EXHAUSTED, SELF_AWAKEN_USE_COUNT | |
- ALL_ENEMIES/RANDOM_ENEMY/ENEMY → 현재 적 1체에 적용 (단일 적 구조)

---

## 4. 새 시스템 파일 (`Assets/Script/Battle/`)

| 파일 | 역할 |
|------|------|
| `BattleTestController.cs` | 테스트 진입점. 레거시 시스템 사전 비활성화 후 한 전투 실행. `testRelics` 필드로 유물 자동 지급 |
| `NewBattleController.cs` | 전투 핵심 — 덱/드로우/**각성**/콤보 큐/방해/저주/연쇄/게이지/이펙트 트리거/연쇄 동기화 |
| `ComboSkillDef.cs` | 콤보 스킬 정의. 레거시(Inspector 수동) + **DB 기반(fromDatabase/refComboId/dbEffects/acceptedOrders)** |
| `Card/CardEnums.cs` | `CardElement`, `CardType`, `CardRarity`(Normal/Rare/Epic) |
| `Card/CardData.cs` | `CardData`(+`rarity`, `selfUseCount`) + `CardInstance` |
| `Card/CardEffectData.cs` | CardEffects.json 한 줄 DTO |
| `Card/CardDatabase.cs` | Resources/CardDB/*.json 로드 |
| `Card/ComboSkillData.cs` | **ComboSkillData / ComboEffectData DTO** (ComboDB용) |
| `Deck/CardDeckSystem.cs` | 4더미+패. `HandLimit` 동적(MAX=10). |
| `Deck/CardEffectResolver.cs` | **카드 데이터 드리븐 효과 엔진** |
| `Deck/ComboSkillDatabase.cs` | **ComboDB JSON 로더** + `BuildOwnedCombos(refIds)` + `ResetCache()` |
| `Deck/ComboEffectResolver.cs` | **콤보 데이터 드리븐 효과 엔진(간소화)** + `ComboResolveContext` |
| `Relic/RelicDef.cs` | 유물 데이터 클래스 + `RelicEffectType` 열거형 |
| `Relic/RelicManager.cs` | 유물 보유 목록 싱글톤. `GiveRelicByEffect()`, `HasEffect()`, `relicDefinitions`(Inspector 아이콘 설정) |
| `UI/NewCardView.cs` | 카드 표시 + 드래그/hover/클릭(더블클릭 사용) |
| `UI/CardHandHUD.cs` | 손패 HUD + 콤보 UI + 카드 픽커/선택 모드 + 덱/버린덱 뷰어 + 각성 게이지/히스토리/dim |
| `UI/CardEffectOverlay.cs` / `CardEffectAnimator.cs` | 카드 사용 이펙트(스프라이트 시트) |
| `UI/PlayerDamageOverlay.cs` | 피격 화면 플래시/셰이크/HP바 깜빡임 |
| `UI/RelicHUD.cs` | **화면 좌상단 유물 아이콘 + 호버 툴팁 UI** (런타임 자동 생성) |

### Editor 파일
| 파일 | 역할 |
|------|------|
| `Assets/Editor/CardDBImporter.cs` | 카드 DB Excel → JSON 변환기 |
| `Assets/Editor/ComboSkillDBImporter.cs` | **콤보 DB Excel → JSON 변환기** |
| `Assets/Editor/NewBattleControllerEditor.cs` | **NewBattleController 커스텀 인스펙터** — 콤보 체크박스 목록 |

### 수정한 기존 파일 (새 시스템 가드)
- `Round/Roundmanager.cs` — 새 시스템이면 레거시 셋업 스킵
- `Enemy/EnemyController.cs` — 화상 유지 가드, 방해행동 위임, frost 키 지원
- `Enemy/EnemyStat.cs` — `statusEffects["frost"]` 추가, ConsumeGaugeStep frost 차단
- `Enemy/EnemyView.cs` — 데미지 텍스트(멀티히트/색/오프셋)
- `Player.cs` — 이벤트 + `statusEffects["chain"]`
- `UI/StatusPanelUI.cs` / `StatusIconDatabase.cs` — burn/frost/chain 아이콘 등록

---

## 5. 유물 시스템

### 구조
```
Assets/Script/Battle/Relic/RelicDef.cs      — 유물 데이터(id, displayName, description, icon, effect)
Assets/Script/Battle/Relic/RelicManager.cs  — 싱글톤. relicDefinitions 리스트(Inspector에서 아이콘 설정)
Assets/Script/Battle/UI/RelicHUD.cs         — 좌상단 아이콘 표시 + 호버 툴팁 (런타임 자동 생성)
```

### 현재 구현된 유물 (테스트용 2종)
| ID | 이름 | 효과 |
|----|------|------|
| `imugi_yeouiju` | 이무기의 여의주 | 각성 종료 후 성공한 콤보 수만큼 각성 게이지 회복 (재발동 방지를 위해 max-1 상한) |
| `secret_manual` | 비급서 | 각성 중 콤보 성공 시 보너스 시간 +0.5초 추가 |

### 테스트 지급 방법
`BattleTestController` Inspector → **Test Relics** 리스트에 `AwakenGaugeRecoverPerCombo` / `ComboBonusSecondsBoost` 추가.

### 아이콘 설정 방법
`RelicManager` 오브젝트 Inspector → **Relic Definitions** 리스트 → 각 항목의 **Icon** 필드에 스프라이트 연결.
씬에 RelicManager GameObject를 미리 배치하면 플레이 중 설정값이 유지됨.

---

## 6. 데이터 드리븐 카드 효과 엔진 (전종 구현 완료)

### Do (동사)
DAMAGE, GAIN_BLOCK, HEAL, LOSE_HP, DRAW_CARD, DISCARD_HAND, DISCARD_CARD, EXHAUST_HAND, **EXHAUST_CARD**, APPLY_STATUS, GAIN_STATUS, GAIN_STAT, ADD_CARD, MOVE_CARD(**SELF=자가복귀 314**), COPY_CARD(**ALL_IN_HAND 418**), COPY_LAST_CARD_EFFECT, CLEANSE_STATUS, SET_GAUGE_COST, SET_NEXT_CARD_FREE, MODIFY_HAND_LIMIT, MODIFY_HIT_COUNT, MODIFY_DAMAGE_MULTIPLIER, NO_EFFECT, **CONSUME_STATUS(116)**, **DOUBLE_STATUS(210)**, **INSTANT_KILL(208)**, **TRANSFORM_STATUS_TO_BLOCK(320)**, **TRIGGER_HAND_CARDS(321/407)**, **TRIGGER_DRAW_PILE_CARDS(419)**, **MODIFY_AWAKEN_GAUGE_MAX(326)**, **MODIFY_STATUS_RULE(126)**, **MODIFY_STATUS_TARGET(124)**

### When (트리거)
즉시: ON_USE, ON_USE_BEFORE_GAUGE
파워 등록형: ON_CARD_USED, ON_CARD_USED_BEFORE_GAUGE, ON_ELEMENT_CARD_USED, ON_FRAGMENT_CARD_USED, ON_ATTACK_CARD_DAMAGE_CALC, ON_CARD_DAMAGE_HIT_DEALT, ON_BLOCK_CONSUMED_BY_ATTACK, ON_PLAYER_HEALED_ACTUAL, ON_PLAYER_HIT_INCLUDING_BLOCK, ON_CARD_EXHAUSTED, ON_CARD_DISCARDED_BY_CARD_EFFECT, ON_CURSE_CLEANSED, ON_BURN_APPLIED_BY_CARD_EFFECT, **ON_PLAYER_LOSE_HP_BY_CARD_EFFECT(122)**, **ON_FROST_APPLIED_BY_CARD_EFFECT(225)**, **ON_ENEMY_ATTACK_RESOLVED(424)**
생애주기형: ON_SELF_EXHAUST, **ON_SELF_DISCARDED(205/206/219)**, **ON_CARD_MOVED_TO_DISCARD(318)**, **ON_CARD_RETURNED_TO_HAND(319)**

### If (조건)
PLAYER_HP_FULL, PLAYER_HP_RATE_LTE:N, ENEMY_STATUS_GTE:STATUS:N, PLAYER_BLOCK_EQ:N, CARD_ELEMENT_EQ:*, PREVIOUS_CARD_TYPE_EQ:*, FIRST_CARD_AFTER_ENEMY_ATTACK, USED_IMMEDIATELY_AFTER_DRAW, LAST_USED_CARD_EXISTS_EXCLUDING_SELF, DAMAGE_SOURCE_IS_CARD_EFFECT, EXCLUDE_TRIGGER_SOURCE, **ENEMY_KILLED_BY_THIS_CARD(410)**, **USED_CARD_HAS_TAG:TAG**, **CARD_TYPE_EQ:TYPE**

### Formula / HitFormula
ENEMY_STATUS_VALUE:*, PLAYER_BLOCK, FLOOR(PLAYER_CURRENT_HP/2), LAST_HP_LOST(*N), **LAST_CONSUMED_BURN(*N)**, GAUGE_SINCE_DRAWN, USED_ATTACK_CARD_COUNT, USED_FRAGMENT_CARD_COUNT, LAST_DISCARDED_COUNT, **EXHAUSTED_CARD_COUNT(115)**, **CONSUMED_CHAIN_COUNT(310)**, **HIT_ENEMY_COUNT(409)**, **ENEMY_COUNT(*N, 416)**, IF_PLAYER_HP_RATE_LT_50_MULTIPLY_2, **IF_ENEMY_HAS_FROST_MULTIPLY_2(209)**

---

## 7. 핵심 동작

### 각성 (Awaken)
- **발동 조건**: 속성 카드 **10장** 사용 시 즉시 발동. `EffectiveAwakenInput = AWAKEN_ACTIVATION_INPUT(10) + _ctx.awakenGaugeMaxDelta`
- **발동 시**: 10초 타이머, 적 게이지 정지, 무속성/파편 임시 격리, 패 가득 드로우, 모든 카드 효과 "드로우 1"로 치환
- **발동 중**: 슬라이딩 3장 콤보 매칭 → 큐 적재 + `COMBO_BONUS_SECONDS`(현재 0.1s) 시간 추가. 비급서 착용 시 +0.5s 추가. 쿨다운: 발동 후 5입력 뒤 재매칭.
- **종료**: 패/뽑을더미/버린더미 스냅샷 복원 → 큐 콤보 일괄 발동(Damage 이펙트 분산+셰이크)
- **주요 상수** (`NewBattleController.cs`):
  - `AWAKEN_DURATION_SECONDS = 10f` — 기본 지속 시간
  - `COMBO_BONUS_SECONDS = 0.1f` — 콤보 1회 성공 시 추가 시간
  - `COMBO_REUSE_INPUT = 5` — 콤보 재사용까지 필요 입력 횟수

### 유물 효과 연결 지점 (NewBattleController)
- **이무기의 여의주**: `EndAwaken()` — 큐 콤보 수를 `_elementInputCount`에 복원
- **비급서**: `OnUseCardRequested()` — 콤보 매칭 시 totalBonus = `COMBO_BONUS_SECONDS + 0.5f`

### 빙결 (FROST)
- `"frost"` 키로 EnemyStat 관리. ConsumeGaugeStep에서 frost>0면 게이지 상승 1 차단 + frost 1 소모.
- 빙결 카드가 부여한 빙결은 그 카드의 게이지 처리에 소모되지 않음(frostBeforeCard 기록).

### 연쇄 (Chain)
- 공격 타격마다 이전 연쇄 발동(1 소모 + `1+chainBonusDamage` 피해)
- SyncChainStatus()가 매 프레임 플레이어 상태 패널에 동기화

### 행동 게이지 / 방해 / 적 공격
- EnemyStat `GAUGE_MAX_STEPS=20`, 중간(10)=방해, 가득=공격
- 방해: 50% 무속성 삽입 / 50% 속성 저주(해당 속성 사용 시 -5, 2회 지속)

---

## 8. 알려진 한계 / 설계 메모
- **단일 적 구조**: ALL_ENEMIES/RANDOM_ENEMY → 현재 적 1체 적용. 콤보 MODIFY_ENEMY_STAT/BUFF_NEXT_COMBO/SKIP_COOLDOWN/MOVE_CARD는 미지원(경고 후 스킵)
- **ComboName 미정**: DB의 ComboName 컬럼이 전부 비어있음 → 인게임 콤보 이름 표시 미정
- **유물 아이콘**: RelicDef.icon 미할당 시 임시 색상으로 표시. 씬에 RelicManager 미리 배치 후 Inspector에서 스프라이트 연결 필요
- **연쇄/빙결 아이콘**: 전용 아트 없어 임시 아이콘 재사용 — StatusIconDatabase.asset에서 교체
- **321/407(TRIGGER_HAND_CARDS)**: CardFilter 없어 패 전체 발동. 바람/파편만 발동시키려면 xlsx CardFilter에 입력

---

## 9. 최근 수정 이력

0. **카드 드로우 등장 연출** — 패에 새로 들어온 카드가 아래에서 위로 떠오르며(페이드+살짝 확대) 등장. 직전 패와 참조 비교로 "새 카드만" 연출, 연속 드로우는 stagger로 차례 등장. 부채꼴 회전을 상쇄해 화면 기준 수직 상승. 튜닝: `NewCardView`의 `drawIntro*`(길이/거리/시작배율/ON-OFF), `CardHandHUD.drawIntroStagger`. 드래그/hover/카드교체 시 자동 취소
1. **유물 시스템 추가** — RelicDef/RelicManager/RelicHUD. 이무기의 여의주·비급서 2종 테스트 구현
2. **콤보 스킬 DB 통합** — ComboSkill_DB.xlsx → JSON → 데이터 드리븐 실행. 64개 콤보(canonical 20종 + alias 44종), 순서무관 매칭
3. **콤보 인스펙터 선택 UI** — NewBattleControllerEditor 커스텀 에디터, 체크박스 목록으로 보유 콤보 지정
4. **COMBO_BONUS_SECONDS 조정**: 1f → 0.1f
5. *(이전)* 각성 개편(구 피버→Awaken), Card DB v2(112장/등급), 효과 전종 구현, 빙결/게이지 순서 버그 수정

---

## 10. 다음 작업 후보
1. **카드 등급 프리팹 표시** — `CardData.rarity` 준비됨. NewCardView/프리팹에 등급 뱃지 추가
2. 등급 기반 **보상 등장 가중치** 로직
3. **연쇄/빙결 전용 아이콘** + 유물 아이콘 아트
4. **다중 적 전투** — ALL_ENEMIES/RANDOM_ENEMY 실제 타겟팅, 콤보 미지원 동사 실구현
5. 각성/게이지 수치 밸런스 (COMBO_BONUS_SECONDS, AWAKEN_DURATION_SECONDS 등)
6. 카드 아이콘/효과 스프라이트 시트 채우기, 사운드
7. 검증 후 본 게임 씬(SampleScene) 마이그레이션

---

## 11. 디버그 팁
`NewBattleController.logVerbose` ON → 카드/각성/콤보/저주/연쇄/유물 로그.
```
[NewBattle] {카드명} 사용 — 게이지+N, 연쇄=N
[각성] 발동! 격리된 무속성=N, 지속 시간=10s
[각성] ⏱ 콤보 매칭 → 시간 +0.1s (X→Y)          ← 비급서 착용 시 +0.6s
[콤보 큐] {스킬명} ({콤보}, ref=N) — 각성 종료 시 발동
[콤보 스킬] {스킬명} (ref=N) 발동 — 효과 N건
[유물] 이무기의 여의주 — 각성 게이지 N/10 회복
[유물] 획득: {유물명}
[ComboDB] 로드 완료 — 콤보 64개, 효과 정의 20종
[ComboEffect] 미지원 Repeat 'XXX' — 1회만 실행
```

---

## 12. 주의사항
- 새 시스템은 `NewBattleController.Instance != null`로 본 게임과 분리
- Card DB / Combo DB 수치는 xlsx가 정본. JSON 직접 수정 시 **재임포트하면 덮어쓰임** — xlsx도 같이 수정할 것
- 일부 기존 파일은 인코딩이 깨져 보일 수 있음(EUC-KR 잔재) — 수정 시 UTF-8 유지
- 손패 한도 동적(418)은 MAX_HAND_LIMIT=10 상한, UI 슬롯도 10개 사전 생성
- RelicManager는 런타임 자동 생성(BattleTestController)되므로 아이콘 등 영구 설정은 씬에 오브젝트를 미리 배치할 것
- RelicHUD 툴팁의 글씨체(`tooltipFont`)·이름/설명 글자 크기·가로 폭은 Inspector에서 설정(세로 높이는 설명 길이에 맞춰 자동). 단 런타임 자동 생성이므로 글씨체를 지정하려면 씬에 RelicHUD 오브젝트를 Canvas 하위에 미리 배치할 것
