# 전투 시스템 개편 프로토타입 — 인수인계 문서

> 최종 갱신: 각성(구 피버) 개편 + Card DB v2(112장/등급) + 효과 전종 구현 + 버그 수정 반영본

## 0. 한 줄 요약
기존 전투(ElementSlotSystem 4슬롯 + ComboSystem)를 **카드 기반(손패 5장 + 각성(Awaken) + 콤보 큐 + 연쇄)** 으로 교체 중. 본 게임 흐름은 건드리지 않고 **격리된 테스트 씬**에서 검증.

**카드 DB는 Excel/JSON 기반 데이터 드리븐 엔진** — 기획자가 xlsx만 수정 → 변환기 → 게임 반영. 코드 수정 없이 수치/조건/공식 조정 가능.

근거 문서: `기획서 0.6v.pdf`, `Card_DB_v2 (1).xlsx` (사용자 Downloads 폴더).

> ⚠️ **용어 변경**: 구 "피버(Fever)"는 전부 **"각성(Awaken)"** 으로 개명됨. 코드 식별자도 `Awaken*`/`_awaken*`, 로그·UI·주석은 "각성". 단 효과 에셋명 `Fire_ATK`처럼 `Fever_ATK`(Resources 파일명)만 그대로 둠.

---

## 1. 환경
- Unity **6000.3.1f1** (프로젝트는 6000.3 계열)
- 프로젝트 루트: `D:\CapstonePR`
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

> **현재 Cards.json/CardEffects.json은 `Card_DB_v2 (1).xlsx` 기준으로 이미 생성돼 있음**(Python으로 임포터 로직 그대로 재현해 직접 기록). 재임포트해도 동일 결과.

### Card_DB_v2 시트 구조
- **Cards**: CardID, Name, Element, CardType, GaugeCost, **Rarity**, Tags, ComboSlot, EffectName, SkillImg, DescriptionKR
- **CardEffects**: CardID, EffectIndex, When, If, Do, Target, Amount, Formula, Hits, HitFormula, Status, CardFilter, FromZone, ToZone, Select, Repeat, Extra, RuntimeKey
- **키값_한글정리**: 코드값 사전

### 카드 ID 체계 (v2 = 총 112장 / 효과 159건)
- FIRE  100~126 (기본+공격/스킬 + **파워 122~126**)
- WATER 200~226 (+ **파워 222~226**)
- WIND  300~326 (+ **파워 322~326**)
- EARTH 400~426 (+ **파워 422~426**)
- NEUTRAL 500 (방해행동 더미), FRAGMENT 501~503 (파편)
- 등급: **Normal 52 / Rare 40 / Epic 20**

> v2 추가 20장은 전부 POWER(122~126,222~226,322~326,422~426). 등급은 데이터 파이프라인까지 연동됨(`CardData.rarity`). **프리팹 카드에 등급 표시는 미구현(사용자가 추후)** — 보상 가중치 등 로직 활용도 미구현.

---

## 3. 새 시스템 파일 (`Assets/Script/Battle/`)

| 파일 | 역할 |
|------|------|
| `BattleTestController.cs` | 테스트 진입점. 레거시 시스템 사전 비활성화 후 한 전투 실행 |
| `NewBattleController.cs` | 전투 핵심 — 덱/드로우/**각성**/콤보 큐/방해/저주/연쇄/게이지/이펙트 트리거/연쇄 동기화 |
| `ComboSkillDef.cs` | 콤보 스킬 정의(slot1/2/3 + ComboEffectType: Damage/Burn/Guard/Heal/Draw) |
| `Card/CardEnums.cs` | `CardElement`, `CardType`, **`CardRarity`(Normal/Rare/Epic)** |
| `Card/CardData.cs` | `CardData`(+`rarity`, `selfUseCount`) + `CardInstance` |
| `Card/CardEffectData.cs` | CardEffects.json 한 줄 DTO |
| `Card/CardDatabase.cs` | Resources/CardDB/*.json 로드 + `ParseRarity`. FRAGMENT_1/2/3=501/502/503, NEUTRAL_FILLER=500 |
| `Deck/CardDeckSystem.cs` | 4더미+패. `HandLimit` 동적(MAX=10). 이벤트 `OnCardDrawn`/**`OnCardDiscarded`**. 메서드: `RestorePiles`/`MoveFromDrawPileToDiscard`/`ExileWholeDrawPile`/`ExileWholeDiscardPile`/`MoveFromDrawPileToHand`(패한도 가드) 등 |
| `Deck/CardEffectResolver.cs` | **데이터 드리븐 효과 엔진** — When/If/Do/Formula 인터프리터 + 파워 트리거 등록 + 생애주기 트리거 |
| `UI/NewCardView.cs` | 카드 표시 + 드래그/hover/클릭(더블클릭 사용) |
| `UI/CardHandHUD.cs` | 손패 HUD + 콤보 UI + 카드 픽커/선택 모드 + **덱/버린덱 뷰어** + 각성 게이지/히스토리/dim |
| `UI/CardEffectOverlay.cs` / `CardEffectAnimator.cs` | 카드 사용 이펙트(스프라이트 시트) |
| `UI/PlayerDamageOverlay.cs` | 피격 화면 플래시/셰이크/HP바 깜빡임 |

### 수정한 기존 파일 (새 시스템 가드)
- `Round/Roundmanager.cs` — 새 시스템이면 레거시 셋업 스킵. **playerStatusPanel/enemyStatusPanel.SetTarget** 호출
- `Enemy/EnemyController.cs` — 화상 발동(불126 화상 유지 가드), 방해행동 위임, `AddStatus`/`SetStatus`(frost 키 지원)
- `Enemy/EnemyStat.cs` — `statusEffects["frost"]` 추가, **ConsumeGaugeStep에서 frost(빙결)가 게이지 상승 차단**, `NodeType`/`IsBossOrElite` 접근자
- `Enemy/EnemyView.cs` — 데미지 텍스트(멀티히트/색/오프셋/화상 빨강)
- `Player.cs` — 이벤트 + `statusEffects["chain"]`(연쇄 표시용)
- `UI/StatusPanelUI.cs` / `StatusIconDatabase.cs` / `.asset` — 상태 아이콘 패널. **burn/frost/chain 등록됨**

---

## 4. 데이터 드리븐 효과 엔진 (전종 구현 완료)

> 모든 (When·Do·If·Formula·HitFormula·Select) 토큰이 처리 경로를 가짐(자동 감사 통과). 미지원 동사/조건은 `Debug.LogWarning` 후 스킵.

### Do (동사)
DAMAGE, GAIN_BLOCK, HEAL, LOSE_HP, DRAW_CARD, DISCARD_HAND, DISCARD_CARD, EXHAUST_HAND, **EXHAUST_CARD(SELECT_ONE_FROM_HAND / ALL→더미전체소멸)**, APPLY_STATUS, GAIN_STATUS, GAIN_STAT, ADD_CARD, MOVE_CARD(**SELF=자가복귀 314**), COPY_CARD(**ALL_IN_HAND 418**), COPY_LAST_CARD_EFFECT, CLEANSE_STATUS, SET_GAUGE_COST, SET_NEXT_CARD_FREE, MODIFY_HAND_LIMIT, MODIFY_HIT_COUNT, MODIFY_DAMAGE_MULTIPLIER, NO_EFFECT, **CONSUME_STATUS(116)**, **DOUBLE_STATUS(210)**, **INSTANT_KILL(208,보스/정예 예외)**, **TRANSFORM_STATUS_TO_BLOCK(320)**, **TRIGGER_HAND_CARDS(321/407)**, **TRIGGER_DRAW_PILE_CARDS(419)**, **MODIFY_AWAKEN_GAUGE_MAX(326)**, **MODIFY_STATUS_RULE(126 화상유지)**, **MODIFY_STATUS_TARGET(124, 단일적이라 실질무효)**

### When (트리거)
즉시: ON_USE, ON_USE_BEFORE_GAUGE
파워 등록형: ON_CARD_USED, ON_CARD_USED_BEFORE_GAUGE, ON_ELEMENT_CARD_USED, ON_FRAGMENT_CARD_USED, ON_ATTACK_CARD_DAMAGE_CALC, ON_CARD_DAMAGE_HIT_DEALT, ON_BLOCK_CONSUMED_BY_ATTACK, ON_PLAYER_HEALED_ACTUAL, ON_PLAYER_HIT_INCLUDING_BLOCK, ON_CARD_EXHAUSTED, ON_CARD_DISCARDED_BY_CARD_EFFECT, ON_CURSE_CLEANSED, ON_BURN_APPLIED_BY_CARD_EFFECT, **ON_PLAYER_LOSE_HP_BY_CARD_EFFECT(122)**, **ON_FROST_APPLIED_BY_CARD_EFFECT(225)**, **ON_ENEMY_ATTACK_RESOLVED(424)**
생애주기형(특정 카드 자신): ON_SELF_EXHAUST, **ON_SELF_DISCARDED(205/206/219)**, **ON_CARD_MOVED_TO_DISCARD(318)**, **ON_CARD_RETURNED_TO_HAND(319)** — CardDeckSystem 이벤트 + NewBattleController가 리졸버 NotifyXxx 호출

### If (조건)
PLAYER_HP_FULL, PLAYER_HP_RATE_LTE:N, ENEMY_STATUS_GTE:STATUS:N, PLAYER_BLOCK_EQ:N, CARD_ELEMENT_EQ:*, PREVIOUS_CARD_TYPE_EQ:*, FIRST_CARD_AFTER_ENEMY_ATTACK, USED_IMMEDIATELY_AFTER_DRAW, LAST_USED_CARD_EXISTS_EXCLUDING_SELF, DAMAGE_SOURCE_IS_CARD_EFFECT, EXCLUDE_TRIGGER_SOURCE, **ENEMY_KILLED_BY_THIS_CARD(410)**, **USED_CARD_HAS_TAG:TAG**, **CARD_TYPE_EQ:TYPE**

### Formula / HitFormula
ENEMY_STATUS_VALUE:*, PLAYER_BLOCK, FLOOR(PLAYER_CURRENT_HP/2), LAST_HP_LOST(*N), **LAST_CONSUMED_BURN(*N, 116)**, GAUGE_SINCE_DRAWN, USED_ATTACK_CARD_COUNT, USED_FRAGMENT_CARD_COUNT, LAST_DISCARDED_COUNT, **EXHAUSTED_CARD_COUNT(115)**, **CONSUMED_CHAIN_COUNT(310)**, **HIT_ENEMY_COUNT(409)**, **ENEMY_COUNT(*N, 416)**, IF_PLAYER_HP_RATE_LT_50_MULTIPLY_2, **IF_ENEMY_HAS_FROST_MULTIPLY_2(209)**
HitFormula 전용: USED_CARD_NAME_COUNT:NAME, **USED_ATTACK_CARD_COUNT(309)**, **USED_SAME_NAME_CARD_COUNT(311)**, **DRAW_PILE_FRAGMENT_COUNT(408)**

> **배수 센티넬(-1) 처리 주의**: `IF_..._MULTIPLY_2`는 -1 반환 + `multiplyBy2` 플래그. ApplyAction에서 "조건 충족 시 eff.amount×2, 아니면 eff.amount 유지"로 처리(과거 조건 불충족 시 amount=-1로 효과가 사라지던 버그 수정됨).

### 런타임 통계 (CardEffectContext)
lastHpLost, **lastConsumedBurn**, **exhaustedCardCount**, **consumedChainCount**, **lastHitEnemyCount**, **enemyKilledThisResolve**, lastDiscardedCount, usedAttackCardCount, usedFragmentCardCount, previousCardType, usedCardNameCounts, handLimitBonus, chainCount, 그리고 각성/신규 효과 플래그 다수 — 모두 `ResetContextFlags()`에서 초기화.

---

## 5. 핵심 동작

### 각성 (Awaken, 구 피버)
- **발동 조건**: 무속성/파편 제외 속성 카드 **10장** 사용 시 **즉시 발동**(F키·armed 대기 없음). 임계값은 `EffectiveAwakenInput = AWAKEN_ACTIVATION_INPUT(10) + _ctx.awakenGaugeMaxDelta`(바람326이 -2).
- **선택 카드 처리**: 10번째가 선택(소멸/버리기/복사 등) 카드면, **선택 완료 후** 발동. (`_awakenPending` + `TryActivatePendingAwaken` + `DeferredPendingAwakenCheck`(2프레임) + `CardHandHUD.SelectionClosedCallback`)
- **발동 시**: 10초 타이머, 적 게이지 정지, 무속성/파편 임시 격리, **패 가득 드로우**, 모든 카드 효과 "드로우 1"로 치환, 콤보 UI 활성화
- **발동 중**: 콤보 슬롯(슬라이딩 3장) 매칭 → **큐 적재 + 제한시간 +1초**. 콤보 **재사용 쿨다운**: 발동 후 속성 5입력 뒤 재매칭(`_comboCooldown[]`). 콤보 리스트 UI는 쿨다운 중 흐림+남은 횟수 표시
- **종료**(시간 종료만): **패/뽑을더미/버린더미를 각성 직전 스냅샷으로 복원**(`_deck.RestorePiles`) → 큐 콤보 일괄 발동(Damage 이펙트 분산+셰이크). 복원이 콤보 발동 **전**이라 Draw 콤보는 복원된 덱에서 유효
- 콤보가 재사용 가능해져 '전부 소진 종료'는 제거됨

### 빙결 (FROST)
- 새 시스템 정식 키 `"frost"`(EnemyStat에 추가). **ConsumeGaugeStep에서 frost>0면 게이지 상승 1 차단 + frost 1 소모**(빙결=행동게이지 지연)
- **빙결/게이지 순서**: 빙결 카드가 부여한 빙결은 *그 카드의 게이지 처리*에 소모되지 않음. OnUseCardRequested가 카드 사용 전 frost값(`frostBeforeCard`)을 기록 → 게이지 처리 동안만 그 값으로 되돌렸다 처리 후 카드분 복원. (그래서 216은 빙결 2가 그대로 적용됨)
- 225(파워): 카드로 빙결 부여 시 +1 추가(재귀가드). 226: 물 3회마다 빙결.

### 연쇄 (Chain)
- 공격 타격마다 이전 연쇄 발동(1 소모 + `1+chainBonusDamage` 피해) → 318/323 활성 시 연쇄 +1
- **연쇄 스택 UI 표시**: `Ctx.chainCount`를 `NewBattleController.SyncChainStatus()`가 매 프레임 변화 시 `_player.SetStatus("chain", n)`로 플레이어 상태 패널에 동기화(아이콘은 바람 아이콘 임시 재사용)

### 상태 아이콘 (StatusPanelUI + StatusIconDatabase.asset)
- 적/플레이어 패널이 `IBattleUnit.OnStatusChanged(key,count)` 구독 → DB에 등록된 key만 아이콘 표시
- 등록 key: burn, wet, freeze, **frost(빙결, freeze 아이콘 재사용)**, **chain(연쇄, 바람 아이콘 재사용)**, launcher, fortify, guard

### 덱/버린덱 뷰어 (CardHandHUD)
- `drawCountText`/`discardCountText`(카운트) 클릭 → 해당 더미 카드를 **ID 순 정렬**로 펼쳐 보기(읽기전용, 카드 피커 UI 재사용). 카드/딤 클릭 또는 같은 카운트 재클릭으로 닫힘. 뷰어 중 카드 사용 차단

### 카드 사용 / 선택·픽커
- 드래그(Y>useThresholdY) 또는 더블클릭. 선택/픽커/뷰어 모드 중 차단
- 선택(EnterSelectionMode): 손패 1장 클릭 / 픽커(EnterCardPickerMode): 더미/파편 풀 그리드. **1프레임 deferred** 진입
- **패 한도 가드**: MoveFromDrawPileToHand는 패 가득 시 실패(초과 불가)

### 키 입력
- **D**: 1장 드로우 + 적 게이지 +1
- (F키 각성 발동은 제거됨 — 즉시 발동)

### 행동 게이지 / 방해 / 적 공격
- EnemyStat `GAUGE_MAX_STEPS=20`, 중간(10)=방해, 가득=공격(직전 화상 발동, 불126 활성 시 화상 미감소)
- 방해: 50% 무속성 삽입 / 50% 속성 저주(해당 속성 사용 시 -5, 2회 지속)

---

## 6. 알려진 한계 / 설계 메모
- **단일 적 구조**: `ALL_ENEMIES`/`RANDOM_ENEMY`는 현재 적 1체에 적용. **124(화상 전체대상화)는 플래그만, 실질 무효** — 다중 적 전투 생기면 타겟 분기 필요
- **321/407(TRIGGER_HAND_CARDS)**: 데이터에 CardFilter가 없어 **패 전체** 발동. 설명대로 바람/파편만 발동시키려면 xlsx CardFilter에 `WIND_CARD`/`FRAGMENT_CARD` 입력(코드의 `PassesTriggerFilter`가 이미 지원)
- **연쇄/빙결 아이콘**은 전용 아트가 없어 바람/빙결(freeze) 아이콘 임시 재사용 — `StatusIconDatabase.asset`에서 스프라이트만 교체하면 됨
- 데이터 드리븐 엔진이 모르는 토큰은 경고만 출력하고 스킵 — 진행 영향 X

---

## 7. 최근 수정 버그 (참고)
1. **110**: "패 제외 전체 소멸"(EXHAUST_CARD ALL on DRAW/DISCARD) 구현. 카드 자체 소멸은 EXHAUST 태그로 정상
2. **빙결/게이지 순서**: 빙결 카드가 자기 게이지를 먹던 문제 수정(위 §5 빙결). → 216 빙결 2 정상 표기
3. **패 초과**: MoveFromDrawPileToHand 패한도 가드
4. **연쇄 스택 UI 미표시**: 플레이어 chain 상태 동기화로 표시
5. 각성 종료 시 패/덱/버린덱 복원, 선택 카드 후 각성 진입

---

## 8. 다음 작업 후보
1. **카드 등급 프리팹 표시** — `CardData.rarity`는 준비됨. NewCardView/프리팹에 등급 뱃지 추가
2. 등급 기반 **보상 등장 가중치** 로직
3. **연쇄/빙결 전용 아이콘** 아트 + StatusIconDatabase 교체
4. **다중 적 전투** — ALL_ENEMIES/RANDOM_ENEMY 실제 타겟팅, 124 활성화
5. 콤보 스킬 데이터(`ownedComboSkills`) 밸런스, 게이지/각성 수치 밸런스
6. 카드 아이콘/효과 스프라이트 시트 채우기, 사운드
7. 검증 후 본 게임 씬(SampleScene) 마이그레이션

---

## 9. 디버그 팁
`NewBattleController.logVerbose` ON → 카드/각성/콤보/저주/연쇄 로그.
```
[NewBattle] {카드명} 사용 — 게이지+N, 연쇄=N
[각성] 발동! 격리된 무속성=N, 지속 시간=10s
[각성] ⏱ 콤보 매칭 → 시간 +1.0s (X→Y)
[콤보 큐] {스킬명} ({콤보}) — 각성 종료 시 발동 (재사용까지 5입력)
[각성] 종료 — 콤보 N건 일괄 발동
[화상] 적 N 고정피해, 잔여 화상=M (유지)        ← 불126
[CardEffect] 미구현 동사 'XXX' (cardId=N)        ← Do 미지원(현재 0건이어야 정상)
[CardEffect] 알 수 없는 Formula/조건 'XXX'        ← Formula/If 미지원
```

---

## 10. 주의사항
- 새 시스템은 `NewBattleController.Instance != null`로 본 게임과 분리. 기존 파일 수정은 이 가드 안에서만 동작
- 일부 기존 파일은 인코딩이 깨져 보일 수 있음(EUC-KR 잔재) — 수정 시 UTF-8 유지
- Card DB 수치는 xlsx가 정본. JSON 직접 수정 시 **재임포트하면 덮어쓰임** — xlsx도 같이 고칠 것
- 손패 한도 동적(418)은 MAX_HAND_LIMIT=10 상한, UI 슬롯도 10개 사전 생성
- **메모리 파일**: `~/.claude/projects/D--CapstonePR/memory/fever-renamed-to-awaken.md`에 각성 개편/DB v2/효과 구현/버그 수정 상세가 누적돼 있음(다음 세션 참고)
