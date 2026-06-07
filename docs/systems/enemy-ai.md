# 적 AI (FCM-RBFN) 명세

> 대상 코드(전부 실독):
> - `Assets/Script/Battle/AI/EnemyDisruptionAI.cs`
> - `Assets/Script/Battle/AI/EnemyAiModel.cs`
> - `Assets/Script/Battle/AI/DisruptionLogger.cs`
>
> 본 명세의 모든 동작/수치는 위 파일에서 직접 확인한 내용이며, `file:line` 으로 근거를 표기한다.

---

## 1. 개요 / 책임

적이 방해행동(disruption)을 무작위가 아니라 **학습 모델 추론**으로 고른다.

- **입력**: 플레이어 덱 유형(5종 퍼지 소속도) + 현재 전투 컨텍스트(8차원).
- **출력**: 방해행동 6종 중 1개의 인덱스(`action 0..5`).
- **방식**: 오프라인 Python(FCM-RBFN) 사전학습 → 가중치를 `.cs` 배열로 **임베드** → 런타임에서 그 가중치로 실시간 추론(argmax). (`EnemyAiModel.cs:4-11`, `EnemyDisruptionAI.cs:24-27`)
- 추론식 (`EnemyAiModel.cs:8-9`):
  `Q_total(s,a) = Σ_i μ_i(x)·QTypeWeights[i,a] + Σ_j c_j·QContextWeights[j,a] + QBias[a]`
  여기서 `μ` = 마할라노비스 퍼지 소속도, `x` = 특성6 `[f1,f2,f3,f4,f5,f7]`, `c` = 컨텍스트8.
- **유형 5종**(`EnemyAiModel.cs:22`): 화상형 / 파편형 / 연속타격형 / 피버형 / 안정형.
- **행동 6종**(`EnemyAiModel.cs:10,21`): `0 저주(curse)` `1 탈진(exhaust)` `2 흡수(absorb)` `3 강화(enhance)` `4 회복(recover)` `5 버리기(discard)`.

---

## 2. 위치 / 타입

- **경로**: `Assets/Script/Battle/AI/` 아래 3개 파일.
- **namespace**: 셋 다 `Battle.AI` (`EnemyDisruptionAI.cs:6`, `EnemyAiModel.cs:1`, `DisruptionLogger.cs:6`).
- **타입 종류**: 셋 다 `static class`.
  - `public static class EnemyDisruptionAI` (`EnemyDisruptionAI.cs:28`) — 추론 본체.
  - `public static class EnemyAiModel` (`EnemyAiModel.cs:13`) — 임베드된 가중치/상수.
  - `public static class DisruptionLogger` (`DisruptionLogger.cs:15`) — CSV 로깅.
- **부속 타입**(둘 다 `EnemyDisruptionAI.cs` 파일 내, `Battle.AI` 네임스페이스):
  - `public enum CardCategory { Burn, Fragment, Chain, Defense, Neutral }` (`EnemyDisruptionAI.cs:9`).
  - `public struct AiDecision` (`EnemyDisruptionAI.cs:12-22`) — 결정 결과 1건(통합/로깅용).

---

## 3. 참조 (의존하는 것 — 바깥 → 이 시스템)

`EnemyDisruptionAI` 가 끌어다 쓰는 외부 의존(`using Battle.Card; using Battle.Deck;` — `EnemyDisruptionAI.cs:3-4`):

| 의존 대상 | 사용처 | 근거 |
|---|---|---|
| `EnemyAiModel` (가중치/상수) | 소속도·Q·마스크 전 구간 | `EnemyDisruptionAI.cs:143-196` |
| `RunDeckState.Instance` | `BuildFeatures()` 에서 런 덱/프로파일 스캔 | `EnemyDisruptionAI.cs:73-101` |
| ─ `RunDeckState.RunDeck` (`IReadOnlyList<CardDatabase.DeckEntry>`) | 덱 카드 순회(`cardId`, `count`) | `EnemyDisruptionAI.cs:78-90`, `RunDeckState.cs:28` |
| ─ `RunDeckState.DefenseWindowRatio` | `f4` 방어성향 | `EnemyDisruptionAI.cs:98`, `RunDeckState.cs:40-49` |
| ─ `RunDeckState.NonAwakenCardUses` / `AwakenActivations` | `f7` 피버사이클 | `EnemyDisruptionAI.cs:100-101`, `RunDeckState.cs:50-51` |
| `CardDatabase.GetById(int)` → `CardData` | 덱 항목 ID → 카드 정의 | `EnemyDisruptionAI.cs:81`, `CardDatabase.cs:37` |
| `CardDatabase.GetEffects(int)` → `IReadOnlyList<CardEffectData>` | `Classify` 의 키워드 판정 | `EnemyDisruptionAI.cs:44`, `CardDatabase.cs:48` |
| `CardData` (`element`, `gauge`, `IsFragment`, `IsNeutral`) | 분류/특성 | `EnemyDisruptionAI.cs:38-66`, `CardData.cs:16,18,57,58` |
| `CardEffectData` (`doAction`, `status`, `hits`) | 키워드 분류 | `EnemyDisruptionAI.cs:50-58`, `CardEffectData.cs:18,22,24` |
| `CardDeckSystem.Hand` / `.HandCount` | 컨텍스트의 손패 구성/장수 | `EnemyDisruptionAI.cs:114-119`, `CardDeckSystem.cs:31` |
| `EnemyStat` (`currentHp`, `maxHp`) | 컨텍스트 `enemy_hp` | `EnemyDisruptionAI.cs:109-110`, `EnemyStat.cs:7-8` (float) |
| `Player` (`currentHp`, `maxHp`) | 컨텍스트 `player_hp` | `EnemyDisruptionAI.cs:111-112`, `Player.cs:10-11` (int) |
| `UnityEngine.Mathf` / `Random` | 정규화·클램프·epsilon 난수 | `EnemyDisruptionAI.cs:97,131,136,211,215` |

`DisruptionLogger` 의존: `UnityEngine.Application.persistentDataPath`, `System.IO`, `EnemyAiModel.ActionNames` (`DisruptionLogger.cs:1-4,28,50`).

`EnemyAiModel` 의존: 없음(상수 전용, `using` 없음). (`EnemyAiModel.cs:1`)

---

## 4. 피참조 (이 시스템을 쓰는 것 — 이 시스템 → 바깥에서 호출)

- **`NewBattleController.TriggerDisruption()`** 이 추론·로깅을 호출하는 단일 진입점:
  - `EnemyDisruptionAI.Decide(_deck, _enemyStat, _player, recoverReady, aiExplorationEpsilon)` (`NewBattleController.cs:1202`).
  - 호출 가드: `if (useAiDisruption && Battle.AI.EnemyAiModel.Loaded)` (`NewBattleController.cs:1198`). 미사용/`Loaded==false`/예외 시 `RandomFallbackPick()` 로 폴백 (`NewBattleController.cs:1211-1217`, `1180-1187`).
  - `DisruptionLogger.Log(decision)` 은 `logAiDecisions` 토글이 켜졌을 때만 (`NewBattleController.cs:1207`).
  - `TriggerDisruption()` 자체는 `EnemyController` 가 호출 (`EnemyController.cs:150`).
- **`RunDeckState.RecordCardUse(CardCategory)`** 호출 시 카테고리는 `EnemyDisruptionAI.Classify(card.data)` 로 산출 (`NewBattleController.cs:473`). 즉 `Classify` 는 추론뿐 아니라 **런 프로파일 누적(f4)** 에도 재사용된다.
- **Python 재학습**: 오프라인 학습(`fcm_retrain_gk.py` / `rbfn_train_gk.py`)이 출력한 `.cs` 배열을 `EnemyAiModel` 의 `Centroids` / `CovInvs` / `QTypeWeights` / `QContextWeights` / `QBias` 값에 **그대로 교체**해 넣는 구조 (`EnemyAiModel.cs:5-6`). 입력 데이터는 `DisruptionLogger` 가 쌓은 CSV.

---

## 5. 공개 API

### `EnemyDisruptionAI`
| 멤버 | 시그니처 | 설명 | 근거 |
|---|---|---|---|
| `Classify` | `static CardCategory Classify(CardData card)` | 카드 1장을 화상/파편/연쇄/방어/무속성으로 분류 | `EnemyDisruptionAI.cs:36-68` |
| `Decide` | `static AiDecision Decide(CardDeckSystem deck, EnemyStat enemyStat, Player player, bool recoverReady, float epsilon)` | 현재 상태로 방해행동 1개 결정(결과 전체 반환) | `EnemyDisruptionAI.cs:200-241` |

> 그 외 `BuildFeatures` / `BuildContext` / `FuzzyMembership` / `MahalanobisSq` / `QTotal` / `AvailabilityMask` 는 전부 `static`(파일 내부)이며 접근 한정자 없는 기본(internal-equivalent) — 외부 비공개. (`EnemyDisruptionAI.cs:71-196`)

### `EnemyAiModel` (전부 `public static`)
| 멤버 | 타입 | 값/차원 | 근거 |
|---|---|---|---|
| `Loaded` | `bool` (get-only) | 배열 차원이 기대치와 일치하면 true(임베드라 사실상 항상 true) | `EnemyAiModel.cs:25-29` |
| `ActionNames` | `string[6]` | `curse, exhaust, absorb, enhance, recover, discard` | `EnemyAiModel.cs:21` |
| `TypeNames` | `string[5]` | `화상형, 파편형, 연속타격형, 피버형, 안정형` | `EnemyAiModel.cs:22` |
| `Centroids` | `float[5,6]` | FCM 중심점 [유형×특성] | `EnemyAiModel.cs:32-39` |
| `CovInvs` | `float[5][,]` (각 6×6) | 공분산 역행렬(마할라노비스용) | `EnemyAiModel.cs:42-89` |
| `QTypeWeights` | `float[5,6]` | RBFN 유형 가중치 [유형×행동] | `EnemyAiModel.cs:92-99` |
| `QContextWeights` | `float[8,6]` | RBFN 컨텍스트 가중치 [컨텍스트×행동] | `EnemyAiModel.cs:102-112` |
| `QBias` | `float[6]` | RBFN 행동 바이어스 | `EnemyAiModel.cs:115` |
| `M` | `const float` = `2f` | 퍼지 지수 m | `EnemyAiModel.cs:15` |
| `TYPES` | `const int` = `5` | 유형 수 | `EnemyAiModel.cs:16` |
| `ACTIONS` | `const int` = `6` | 행동 수 | `EnemyAiModel.cs:17` |
| `FEAT` | `const int` = `6` | 특성 차원 | `EnemyAiModel.cs:18` |
| `CTX` | `const int` = `8` | 컨텍스트 차원 | `EnemyAiModel.cs:19` |

### `DisruptionLogger`
| 멤버 | 시그니처 | 설명 | 근거 |
|---|---|---|---|
| `Log` | `static void Log(AiDecision d)` | 결정 1건을 CSV 한 줄로 append(첫 호출 시 파일/헤더 생성) | `DisruptionLogger.cs:42-66` |

### `AiDecision` (struct 필드) — `EnemyDisruptionAI.cs:12-22`
| 필드 | 타입 | 의미 |
|---|---|---|
| `action` | `int` | 선택된 행동 `0..5` (0 저주 ~ 5 버리기) |
| `features` | `float[]` | `[f1,f2,f3,f4,f5,f7]` (주석상 6개 특성) |
| `context` | `float[]` | `[enemy_hp, player_hp, hand_fire, hand_frag, hand_chain, hand_def, hand_null, hand_count/10]` |
| `membership` | `float[]` | 퍼지 소속도 μ [5] |
| `q` | `float[]` | Q_total [6] |
| `handCount` | `int` | 손패 장수(원값) |
| `recoverReady` | `bool` | 회복 쿨다운 가용 여부 |
| `explored` | `bool` | epsilon 탐험으로 무작위 선택됐는지 |

---

## 6. 내부 동작 / 데이터 흐름

`Decide()` 파이프라인 (`EnemyDisruptionAI.cs:200-241`):
`BuildFeatures()` → `BuildContext()` → `FuzzyMembership(x)` → `QTotal(μ,c)` → `AvailabilityMask()` → epsilon-greedy argmax.

### 6.1 특성 `x[6] = [f1,f2,f3,f4,f5,f7]` — `BuildFeatures()` (`EnemyDisruptionAI.cs:71-104`)
- `RunDeckState.Instance.RunDeck` 전체를 순회하며 카드별 `Classify` 후 수량 가중 집계(`total`, `burn`, `frag`, `chain`, `gaugeSum`). `run == null` 이면 모두 0 처리.
- `f1` = 화상 비율 `burn/total` (화상의존도).
- `f2` = 파편 비율 `frag/total` (파편의존도).
- `f3` = 연쇄 비율 `chain/total` (연속/연쇄의존도).
- `f5` = `Clamp01((3 - avgGauge)/2)`, `avgGauge = gaugeSum/total`(`total==0`이면 1) — 덱 평균 코스트(저코스트↔고코스트).
- `f4` = `run.DefenseWindowRatio` — 최근 윈도우 방어성향(아래 6.6).
- `f7` = `NonAwakenCardUses>0` 일 때 `Min(1, AwakenActivations / (NonAwakenCardUses/15))`, 아니면 0 — 피버(각성) 사이클.
- 반환은 `{ f1, f2, f3, f4, f5, f7 }` 순서(=f6 없음).

### 6.2 컨텍스트 `c[8]` — `BuildContext()` (`EnemyDisruptionAI.cs:107-138`)
순서대로:
1. `enemy_hp` = `Clamp01(enemyStat.currentHp/maxHp)` (`maxHp<=0`/null → 1).
2. `player_hp` = `Clamp01(player.currentHp/maxHp)` (동일 폴백).
3~7. `hand_fire / hand_frag / hand_chain / hand_def / hand_null` = 손패를 `Classify` 한 카테고리별 개수 × `1/handCount`(비율). `handCount==0` 이면 `inv=0` → 전부 0.
8. `hand_count/10` = `Min(handCount/10, 1)`.
- `handCount` 는 `out` 으로 원값도 반환(로깅용).

### 6.3 퍼지 소속도 `μ[5]` — `FuzzyMembership()` (`EnemyDisruptionAI.cs:141-159`)
- 유형 k별: `diff = x - Centroids[k]`, `dist2 = MahalanobisSq(diff, CovInvs[k])`, `dist2 = max(dist2, 1e-12)`.
- `inv[k] = dist2^power`, `power = -1/(M-1)` (M=2 → `-1`).
- 정규화 `μ[k] = inv[k]/Σinv` (Σ=0이면 `1/K`).
- `MahalanobisSq(diff, A) = diffᵀ·A·diff` (이중 루프, `EnemyDisruptionAI.cs:161-172`).

### 6.4 `Q_total[6]` — `QTotal()` (`EnemyDisruptionAI.cs:175-186`)
행동 a마다 `QBias[a] + Σ_i μ_i·QTypeWeights[i,a] + Σ_j c_j·QContextWeights[j,a]`.

### 6.5 가용성 마스크 — `AvailabilityMask(handCount, recoverReady)` (`EnemyDisruptionAI.cs:189-196`)
- 전부 `true` 로 시작.
- `handCount < 2` → 행동 5(버리기) 비활성.
- `!recoverReady` → 행동 4(회복) 비활성.
- 행동 0~3(저주/탈진/흡수/강화)은 항상 가용.

### 6.6 epsilon-greedy 선택 (`EnemyDisruptionAI.cs:209-228`)
- `epsilon > 0 && Random.value < epsilon` → 가용 행동 중 무작위 1개(`explored=true`).
- 아니면 마스크 통과 행동 중 `q` 최대(argmax). 모두 막혔으면 안전망으로 `action=0`(0~3은 항상 가용이라 사실상 도달 X).

### 6.7 `Classify` 규칙 (키워드 효과 우선) — `EnemyDisruptionAI.cs:36-68`
판정 순서(위에서 먼저 매칭되면 확정):
1. `card == null` → `Neutral`.
2. `card.IsFragment`(element==Fragment) → `Fragment`.
3. `card.IsNeutral`(element==Neutral) → `Neutral`.
4. `GetEffects(id)` 순회로 플래그 수집(대문자 비교):
   - `burn`: `status == "BURN"` 이고 `doAction ∈ {APPLY_STATUS, GAIN_STATUS, CONSUME_STATUS, DOUBLE_STATUS}` (단순 불-색 공격 제외).
   - `chain`: `status == "CHAIN"` 이고 `doAction ∈ {APPLY_STATUS, GAIN_STATUS}`.
   - `block`: `doAction == "GAIN_BLOCK"`.
   - `heal`: `doAction == "HEAL"`.
   - `maxHits` = 효과들의 `hits` 최댓값.
5. 분류 결정:
   - `burn` → `Burn`.
   - `chain || maxHits >= 2` → `Chain` (연쇄 또는 2타+ 다타격).
   - `block || heal` → `Defense`.
   - `element == Earth` → `Fragment` (땅 원소는 파편 묶음에 포함, f2 정의 때문).
   - 그 외(키워드 없는 불/물/바람 바닐라 단타) → `Neutral`.

---

## 7. 데이터

### 7.1 가중치 차원 (`EnemyAiModel.cs`)
| 배열 | 차원 | 의미 |
|---|---|---|
| `Centroids` | 5 × 6 | 유형 × 특성 |
| `CovInvs` | 5 × (6 × 6) | 유형별 공분산 역행렬 |
| `QTypeWeights` | 5 × 6 | 유형 × 행동 |
| `QContextWeights` | 8 × 6 | 컨텍스트 × 행동 |
| `QBias` | 6 | 행동 |

행동 인덱스: `0 curse, 1 exhaust, 2 absorb, 3 enhance, 4 recover, 5 discard` (`EnemyAiModel.cs:10,21`).
유형 인덱스: `0 화상형, 1 파편형, 2 연속타격형, 3 피버형, 4 안정형` (`EnemyAiModel.cs:22`).
컨텍스트 인덱스(QContextWeights 행 순서, `EnemyAiModel.cs:101`): `enemy_hp, player_hp, hand_fire, hand_frag, hand_chain, hand_def, hand_null, hand_count/10`.

### 7.2 CSV 로그 스키마 (`DisruptionLogger.cs:17-18, 54-58`)
헤더(16열, 콤마 구분):
```
f1,f2,f3,f4,f5,f7,enemy_hp,player_hp,hand_fire,hand_frag,hand_chain,hand_def,hand_null,hand_count,recover_ready,action
```
- `f1..f7` = `d.features[0..5]` (`F4` 포맷, `InvariantCulture`).
- `enemy_hp, player_hp, hand_fire..hand_null` = `d.context[0..6]` (`F4`). **컨텍스트 8번째 `hand_count/10` 은 기록 안 함** — 대신 `hand_count` 열에 `d.handCount` **원값**(정수) 사용 (`DisruptionLogger.cs:53,57`).
- `recover_ready` = `d.recoverReady ? 1 : 0`.
- `action` = `EnemyAiModel.ActionNames[d.action]` 문자열(범위 밖이면 `"unknown"`).

### 7.3 파일 경로 (`DisruptionLogger.cs:28-32`)
- 디렉터리: `{Application.persistentDataPath}/ai_logs/`.
- 파일명: `run_yyyyMMdd_HHmmss.csv` (세션당 1파일, 첫 `Log` 호출 시 헤더와 함께 생성).
- 실패 시 `_failed=true` 로 이후 로깅 비활성, 경고 로그만 출력.

### 7.4 임베드 가중치 출처/상태
- 출처: `gk_result/new_centroids_gk.cs`(Centroids) + `rbfn_result_gk/rbfn_weights_gk.cs`(나머지) (`EnemyAiModel.cs:5`).
- 현재 값 = **합성데이터 기반** 사전학습(좋은행동선택률 90.6%), 실데이터 재학습 후 교체 예정 (`EnemyAiModel.cs:11`).

---

## 8. 주의 / 엣지케이스

- **가중치는 플레이스홀더(합성)**: 현재 `EnemyAiModel` 값은 합성데이터 학습 산출물이라 실제 플레이 분포와 다를 수 있다. 실데이터 재학습으로 교체 전제 (`EnemyAiModel.cs:11`).
- **재학습 루프**: `DisruptionLogger` CSV(상태+선택행동) → 오프라인 Python(FCM 재학습/RBFN 재학습) → 출력 `.cs` 배열을 `EnemyAiModel` 에 붙여넣기 (`EnemyAiModel.cs:5-6`, `DisruptionLogger.cs:8-13`).
- **보상 신호 부재 → epsilon 탐험 필수**: 현재 로그는 (상태·선택행동)만 담고 **실제 결과(승패/피해)가 없다**. 의미 있는 RBFN 보상엔 결과가 필요(향후). 그 전까진 `epsilon` 탐험으로 행동 다양성을 확보해야 순수 argmax 순환(같은 행동만 반복)을 막는다 (`DisruptionLogger.cs:13`, `EnemyDisruptionAI.cs:199,211-217`). 런타임 기본 `aiExplorationEpsilon = 0.15` (`NewBattleController.cs:80`).
- **로깅 기본 OFF**: `logAiDecisions` 기본값 `false` (`NewBattleController.cs:82`) — 데이터 수집하려면 명시적으로 켜야 한다.
- **`Loaded` 의 의미**: 차원 검증만 하며 임베드라 항상 true (`EnemyAiModel.cs:24-29`). 즉 "모델 미로드" 분기는 사실상 발생하지 않고, 실패는 `Decide` 내부 예외 → `NewBattleController` 의 try/catch 폴백으로 처리 (`NewBattleController.cs:1209-1213`).
- **로그 8번째 컨텍스트 누락 주의**: CSV의 `hand_count` 는 정규화값(`context[7]`)이 아니라 원값(`handCount`)이다. Python 학습 시 컨텍스트 8번째 입력은 `hand_count/10` 으로 다시 정규화해야 추론과 일치 (`DisruptionLogger.cs:53`, `EnemyDisruptionAI.cs:136`).
- **타입 분류는 효과 키워드 의존**: `Classify` 가 화상/연쇄를 `status`+`doAction` 키워드로 판정하므로, `CardEffects.json` 의 `doAction`/`status` 표기가 바뀌면 분류와 특성(f1/f3)이 함께 흔들린다. 키워드 없는 원소 공격은 (땅=Fragment 제외) `Neutral` 로 떨어진다 (`EnemyDisruptionAI.cs:50-67`).
- **f4/f7 데이터 의존**: `DefenseWindowRatio` 는 최근 10회 사용 분모 고정(/10), `f7` 분모는 `NonAwakenCardUses/15`. 이 카운터들이 `RunDeckState.RecordCardUse` / `RecordAwakenActivation` 호출로 갱신되지 않으면 두 특성은 0으로 남는다 (`RunDeckState.cs:40-62`).
- **HP 타입 불일치**: `EnemyStat.currentHp/maxHp` 는 `float`, `Player.currentHp/maxHp` 는 `int`. 둘 다 `Clamp01` 비율로 정규화되어 컨텍스트에 들어가므로 영향은 없지만, null/`maxHp<=0` 시 1(만피)로 폴백 (`EnemyDisruptionAI.cs:109-112`).
- **회복 쿨다운**: `recoverReady` 는 호출 측(`NewBattleController._recoverCooldown <= 0`)이 판단해 넘긴다. 회복(4) 발동 시 쿨다운 2로 설정, 비회복 행동마다 1 감소 — AI는 마스크로만 이를 반영할 뿐 쿨다운 상태를 자체 보유하지 않는다 (`NewBattleController.cs:1196,1221,1259`).
