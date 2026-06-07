# 적 AI (FCM-RBFN) — 단계별 계산 & 코드 동작 문서

플레이어 덱 성향(유형) + 전투 상황으로 방해행동을 고르는 2단계 AI가 **각 단계에서 무엇을 계산하고 어떤 코드가 도는지**를 수식과 함께 정리.
- **오프라인(Python, 1회)**: 단계 1~4 — 데이터 → FCM → RBFN → 가중치 산출 → 유니티 임베드
- **런타임(유니티, 매 방해결정)**: 단계 5~6 — 소속도 → Q 가중합 → argmax (+ 온라인 학습)

## 기호 / 차원
| 기호 | 의미 | 차원 |
|---|---|---|
| `x` | 특성 벡터 [f1,f2,f3,f4,f5,f7] (유형 판별) | 6 |
| `c` | 컨텍스트 [enemy_hp, player_hp, hand_fire/frag/chain/def/null, hand_count/10] | 8 |
| `v_i`, `A_i` | 유형 i 중심점, 정규화 거리행렬(공분산역) | 6 / 6×6 |
| `μ_i` | 유형 i 퍼지 소속도 (Σμ=1) | 5 |
| `Wtype`,`Wctx`,`b` | RBFN Q가중치(유형/컨텍스트), 바이어스 | 5×6 / 8×6 / 6 |
| `m` | 퍼지 지수 = 2.0 | — |
| 유형(5) | 화상형·파편형·연속타격형·피버형·안정형 | |
| 행동(6) | 0저주 1탈진 2흡수 3강화 4회복 5버리기 | |

---

# 단계 1 — 데이터 수집 (런타임 로깅)

**무엇을**: 플레이 중 매 방해결정 시점의 `(특성6, 컨텍스트8, 선택행동)`을 CSV로 적재.

**코드**: `Assets/Script/Battle/AI/DisruptionLogger.cs` → `Log(AiDecision)` → `{persistentDataPath}/ai_logs/run_*.csv` append.
```
스키마: f1,f2,f3,f4,f5,f7, enemy_hp,player_hp, hand_fire,hand_frag,hand_chain,hand_def,hand_null, hand_count, recover_ready, action
```
- `NewBattleController.TriggerDisruption`에서 `logAiDecisions` ON이면 결정마다 호출.
- `hand_count`는 **원값** 기록(모델 입력 시 /10 정규화는 Python `_ctx_norm`이 수행).
- ⚠️ 의미있는 RBFN 학습엔 `action`이 **다양**해야 함 → `aiExplorationEpsilon`(탐험)으로 변동 확보.

**출력**: CSV 파일들 (Python `--data <폴더>`가 읽음).

---

# 단계 2 — FCM(Gustafson-Kessel) 중심점 산출 (오프라인)

**무엇을**: 특성 `x`(6D)만으로 플레이어를 5유형으로 **퍼지 군집** → 유형별 중심점 `v_i`와 정규화 거리행렬 `A_i`.
GK는 일반 FCM(원형)과 달리 **유형마다 공분산을 학습**해 타원형 분포를 잡고 **마할라노비스 거리**를 씀.

**반복 수식** (수렴까지; `fcm_retrain_gk.py`):
```
① 중심점        v_i = Σ_k (u_ik)^m · x_k / Σ_k (u_ik)^m            (_update_centers)
② 퍼지 공분산   F_i = Σ_k (u_ik)^m (x_k−v_i)(x_k−v_i)ᵀ / Σ_k (u_ik)^m
③ 정규화 거리   A_i = det(F_i)^(1/d) · F_i⁻¹   (d=6)               (_update_covariances)
④ 마할라노비스  d²_ik = (x_k−v_i)ᵀ A_i (x_k−v_i)                   (_compute_distances)
⑤ 소속도 갱신   u_ik = 1 / Σ_j (d²_ik / d²_jk)^(1/(m−1))           (_update_membership)
```
- 수렴: `‖U−U_old‖ < error`. 10개 시드로 학습 → **FPC**(=Σu²/N, 군집 선명도) 최고 결과 채택 (`train_gk`).
- 특이행렬 방지: 공분산 고유값 하한 정규화 (`_update_covariances`의 eigval clip).
- 클러스터 순서 무작위 → `PROTOTYPES`(5×6)와 **1:1 최근접 배정**으로 "클러스터=유형" 확정 (`assign_types`).

**코드 핵심** (`GustafsonKessel.fit`): U 초기화 → [①②③④⑤] 반복 → `centers_`, `cov_invs_`, `u_`, `fpc_`.

**출력**: `gk_result/fcm_centers.npz`(centers, cov_invs, sigmas) + `new_centroids_gk.cs`(C# 임베드용).

---

# 단계 3 — RBFN 보상기반 Q학습 (오프라인)

**무엇을**: "어떤 상황에서 어떤 방해행동이 가치 있는가(Q)"를 학습. 은닉층=FCM 소속도 μ(중심점 **고정**), 출력 가중치 `W,b`만 학습.

### 3.1 은닉층 = 소속도 μ (FCM 멤버십, 추론용 단순형, m=2)
`rbfn_train_gk.py` `fuzzy_membership`:
```
d²_k = (x−v_k)ᵀ A_k (x−v_k)            # 단계2 ④와 동일
inv_k = (d²_k)^(−1/(m−1))   → m=2면 1/d²_k
μ_k = inv_k / Σ_j inv_j
```

### 3.2 입력 결합 = [μ ; 컨텍스트] (13차원)
`build_features`: `H = hstack(μ(5), c(8))`. 컨텍스트 정규화 `_ctx_norm`: `hand_count/10`(나머지는 0~1 그대로).

### 3.3 보상 (현재: 휴리스틱)
`compute_reward` → `action_is_good(type=argmax μ, action, ctx)` → **+0.5 / −0.5** (`R_GOOD/R_BAD`).
규칙 요약(`action_is_good`):
```
패 ≥4 → 버리기 good (유형무관)        |  적HP ≤0.3 → 회복 good (유형무관)
유형 적합 행동이면서:  저주→압박속성 손패 ≥0.25 / 버리기→패≥2 / 회복→적HP≤0.5 / 그외(탈진·흡수·강화)→good
유형별 적합행동 TYPE_CORRECT_ACTIONS: 화상형=저주 / 파편형=탈진 / 연속타격형=저주·강화·버리기 / 피버형=흡수 / 안정형=강화·회복
```

### 3.4 미니배치 Q회귀 (`train_q_minibatch`, B=4)
```
선택 행동 a의 Q를 보상 r로 회귀(1-step bandit):
 pred = H·W[:,a] + b[a]
 err  = pred − r
 W[:,a] −= lr·( (Σ err·H)/|batch| + reg·W[:,a] )      # MSE grad + L2
 b[a]   −= lr·( Σ err / |batch| )
```
- 미니배치 무작위 추출 평균오차로 SGD, L2 정규화로 안정화. (PDF 2.5)

**출력**: `rbfn_result_gk/rbfn_weights_gk.cs` = `QTypeWeights(5×6)` + `QContextWeights(8×6)` + `QBias(6)` (+CovInvs). 평가지표: 좋은행동선택률.

> ⚠️ 학습은 **라벨 분류가 아니라 보상 회귀**. 현재 보상=휴리스틱이라 실데이터에선 "실제 결과(승패/피해)" 기반 보상으로 바꿔야 진짜 의미. (단계 6 온라인 학습이 그 첫 걸음)

---

# 단계 4 — 유니티 임베드 (오프라인 → 코드)

**무엇을**: 단계2·3 산출물(.cs)을 유니티 상수로 박음.
**코드**: `Assets/Script/Battle/AI/EnemyAiModel.cs` (static):
```
Centroids[5,6]  ← new_centroids_gk.cs
CovInvs[5][6,6] · QTypeWeights[5,6] · QContextWeights[8,6] · QBias[6]  ← rbfn_weights_gk.cs
```
재학습 시 Python이 새로 출력한 .cs 배열을 이 파일에 교체하면 끝.

---

# 단계 5 — 런타임 추론 (유니티, 매 방해결정)

진입: `EnemyController.HandleMidPattern` → `NewBattleController.TriggerDisruption` → `EnemyDisruptionAI.Decide(...)`.

### 5.1 특성 x[6] 계산 — `BuildFeatures()` (RunDeck/플레이 분석)
```
f1 = 화상카드수 / 덱        f2 = 파편카드수 / 덱      f3 = 연쇄카드수 / 덱   (RunDeck 스캔 + Classify)
f5 = Clamp01((3 − 덱평균게이지) / 2)
f4 = RunDeckState.DefenseWindowRatio        (최근 10회 카드사용 중 방어/회복 비율)
f7 = nonAwaken==0 ? 0 : min(1, 각성발동수 / (비각성사용수 / 15))
```
- 카드 분류 `Classify(CardData)`: **키워드(효과) 기준** — 화상=APPLY/GAIN/CONSUME_STATUS BURN, 연쇄=CHAIN 또는 hits≥2, 방어=GAIN_BLOCK/HEAL, 파편=Fragment+땅원소, 그 외=무속성. (원소색 폴백 X)

### 5.2 컨텍스트 c[8] — `BuildContext(...)`
```
[ enemy_hp(비율), player_hp(비율), hand_fire, hand_frag, hand_chain, hand_def, hand_null(손패 카테고리 비율 합≈1), hand_count/10(Clamp1) ]
```

### 5.3 소속도 μ[5] — `FuzzyMembership(x)` (단계3.1과 동일)
```
for k in 5:
   diff = x − Centroids[k]
   d²_k = diffᵀ · CovInvs[k] · diff      (MahalanobisSq, 1e-12 하한)
   inv_k = pow(d²_k, −1/(M−1))           // M=2 → 1/d²_k
μ_k = inv_k / Σ inv
```

### 5.4 Q[6] = 가중합 — `QTotal(μ, c)`
```
Q(a) = QBias[a] + Σ_i μ_i·QTypeWeights[i,a] + Σ_j c_j·QContextWeights[j,a]
       └ 유형 기반 선호 ──┘     └── 상황 기반 보정 ──┘
```

### 5.5 가용성 마스크 → argmax — `AvailabilityMask` + 선택
```
mask: 버리기(손패<2) 제외, 회복(쿨다운 중) 제외
ε 탐험: 확률 ε로 가용 행동 무작위 (데이터 다양성)
아니면: argmax_a (mask[a] ? Q(a) : −∞)  → 행동 인덱스
```
반환 `AiDecision{ action, features x, context c, membership μ, q, handCount, recoverReady }`.

### 5.6 실행
`NewBattleController.TriggerDisruption`의 switch가 `action`(0~5)으로 6개 방해 분기 실행(저주/탈진/흡수/강화/회복/버리기).

---

# 단계 6 — 하이브리드 온라인 학습 (런타임, 옵트인)

`onlineLearning` ON이면 게임 중 실제 결과로 **RBFN Q-가중치만** 미세조정(베이스에서 warm-start, FCM 고정).

### 6.1 추론 가중치 분기 — `EnemyDisruptionAI.Decide(..., onlineLearning)`
```
q = onlineLearning ? OnlineQLearner.QTotal(μ,c)   // 세션 학습 가중치
                   : QTotal(μ,c)                   // 고정 베이스
```
`OnlineQLearner`는 세션 시작 시 `EnemyAiModel`에서 복사(warm-start).

### 6.2 보상 윈도우 — `NewBattleController`
```
방해 발동 → pending(decision) + 플레이어/적 HP 스냅샷 + 타이머 4s 시작 (BeginDisruptionRewardWindow)
Update에서 타이머 소진/다음방해/EndBattle → CloseDisruptionReward:
  playerLost = Clamp01(Δ플레이어HP / maxHP)        // AI에 +
  enemyLost  = Clamp01(Δ적HP / maxHP)              // AI에 −
  outcome = Clamp(2·playerLost − 2·enemyLost, −1, 1)
  heur    = EnemyDisruptionAI.HeuristicReward(d)   // 단계3.3 이식, +1/−1
  reward  = rewardBlend·heur + (1−rewardBlend)·outcome
  OnlineQLearner.Observe(μ, c, action, reward)
```

### 6.3 온라인 SGD + 베이스 L2 — `OnlineQLearner.Observe → TrainMinibatch`
```
경험 (state=[μ;c], action a, reward r) → 리플레이 버퍼(256)
미니배치(B=4):
  pred = state·W[:,a] + b[a]
  err  = pred − r
  W[i,a] −= LR·( err·state[i] + REG·(W[i,a] − Wbase[i,a]) )   // 그래디언트 + 베이스로 L2 당김(드리프트 방지)
  b[a]   −= LR·err
(LR=0.02, REG=0.01)
```
- **지속=세션**: 런(ResetRun) 넘어 누적, Play 재시작 시 `[RuntimeInitializeOnLoadMethod]`로 베이스 복귀.
- 모니터: `WeightDriftFromBase()`(베이스 대비 L2) — 발산 없이 서서히 증가하면 정상.

---

# 부록 A — 전체 데이터 흐름
```
[오프라인 Python]
 CSV(f1..f7, ctx, action) ──FCM(6특성)──▶ Centroids v_i, CovInvs A_i ──┐
                                                                        ├─RBFN: μ=membership → H=[μ;ctx] → 보상 Q회귀 → W,b
                                                                        ┘
   └────────────── new_centroids_gk.cs + rbfn_weights_gk.cs ───────────▶ EnemyAiModel.cs (임베드)
[런타임 유니티]
 RunDeck/플레이 ─BuildFeatures▶ x(6) ─FuzzyMembership(v,A)▶ μ(5) ┐
 전투상황 ───────BuildContext▶ c(8) ───────────────────────────┴─QTotal(W,b)▶ Q(6) ─mask·argmax▶ 방해행동
                                                                    ▲ (온라인 시 W,b는 OnlineQLearner가 결과보상으로 세션 미세조정)
```

# 부록 B — 코드 위치 빠른참조
| 단계 | 파일 | 핵심 함수 |
|---|---|---|
| 1 수집 | `AI/DisruptionLogger.cs` | `Log` |
| 2 FCM | `fcm_retrain_gk.py` | `GustafsonKessel.fit`, `_update_centers/covariances/distances/membership`, `train_gk`, `assign_types` |
| 3 RBFN | `rbfn_train_gk.py` | `fuzzy_membership`, `build_features`, `compute_reward`/`action_is_good`, `train_q_minibatch` |
| 4 임베드 | `AI/EnemyAiModel.cs` | (상수 배열) |
| 5 추론 | `AI/EnemyDisruptionAI.cs` | `BuildFeatures`, `BuildContext`, `FuzzyMembership`, `QTotal`, `AvailabilityMask`, `Decide`, `Classify` |
| 6 온라인 | `AI/OnlineQLearner.cs`, `NewBattleController.cs` | `QTotal/Observe/TrainMinibatch`, `Begin/Tick/CloseDisruptionReward`, `HeuristicReward` |

# 부록 C — 핵심 상수
`m(퍼지)=2.0` · 유형 5 · 행동 6 · 특성 6 · 컨텍스트 8 · `LR=0.02` · `REG=0.01` · `BATCH=4` · `BUFFER=256` · 보상윈도우 4s · `rewardBlend=0.5`(기본) · `R_GOOD/BAD=±0.5`(오프라인) / `±1`(온라인 휴리스틱)

---

# 부록 D — 단계별 수치 예시 (실데이터 손계산 ↔ C# 대조)

실제 수집된 한 줄(기본 8장 덱, 전투 초반)로 단계 5·6을 손으로 따라간다. **C#은 동일 수식 + 동일 임베드 가중치라 출력이 같다** (`logVerbose` ON이면 `[적AI] μ=[..] → 선택=X` 로 확인).

**입력 데이터**
```
x(특성) = [f1 .125, f2 .125, f3 .125, f4 .3, f5 1.0, f7 0]
c(컨텍스트) = [enemy_hp .7917, player_hp 1.0, hand_fire .2, hand_frag 0, hand_chain .2, hand_def .6, hand_null 0, hand_cnt/10 .5]
recover_ready=1, hand_count=5   (실제 선택: 버리기)
```
> 단계 2⑤(FCM 멤버십)·3.4(Q회귀)는 런타임 5.3·6.3과 **수식이 동일** → 아래 예시가 오프라인 단계의 손계산도 겸한다.

### 단계 5.3 — 소속도 μ
```
diff(화상형) = x − Centroids[0] = [-.529, -.078, -.152, +.087, +.556, -.294]
dist²_k = diffᵀ·CovInvs[k]·diff  →  [화 .677, 파 .842, 연 .586, 피 .778, 안 .672]
inv_k = 1/dist²  →  [1.478, 1.188, 1.707, 1.286, 1.488],  Σ=7.148
μ_k = inv/Σ  →  화상형 .207 · 파편형 .166 · 연속타격형 .239 · 피버형 .180 · 안정형 .208
```
→ **거의 균등**(기본덱=유형 미분화). 유형 기반 선호가 약하게 작동할 것.

### 단계 5.4 — Q = 가중합 (type + ctx + bias)
| 행동 | Q | = type | + ctx | + bias |
|---|---|---|---|---|
| 저주 | **−0.060** | +0.024 | +0.037 | −0.120 |
| 탈진 | −0.291 | −0.061 | −0.144 | −0.086 |
| 흡수 | −0.189 | −0.048 | −0.016 | −0.124 |
| 강화 | +0.121 | +0.055 | +0.044 | +0.022 |
| 회복 | −0.384 | +0.071 | **−0.889** | +0.434 |
| 버리기 | **+0.397** | +0.019 | **+0.291** | +0.086 |

**왜 버리기?** μ가 균등 → type 항 미미(±0.07). **결정은 ctx가 좌우**:
```
버리기 ctx: hand_cnt/10 .5×(+.599)=+.300  +  hand_def .6×(+.038)=+.023  +  player_hp 1.0×(−.041)=−.041  ⇒ +.291
회복  ctx: enemy_hp .79×(−1.167)=−.924 가 회복 bias(+.434)를 눌러버림  ⇒ Q −.384 (가용은 했지만 탈락)
```
가용성 마스크(전부 통과: 회복 쿨다운0·손패5≥2) → **argmax = 버리기** ✅ (로그의 action=discard와 일치).

### 단계 6 — 온라인 1스텝 (이 결정에 보상)
가정: 윈도우 4초 동안 플레이어 HP 12 감소(/100), 적 HP 변화 0. `rewardBlend=0.5`.
```
outcome = clamp(2×0.12 − 2×0.00, −1,1) = +0.240        // 플레이어가 피해받음 → AI에 +
heur    = +1.0                                          // 손패5≥4 → 버리기 good
reward  = 0.5×(+1.0) + 0.5×(+0.240) = +0.620

state=[μ;c], action=버리기(5):
  pred = Q[버리기] = +0.397
  err  = pred − reward = +0.397 − 0.620 = −0.223
  W[hand_cnt,버리기]: +0.5992 −= 0.02×(−0.223×0.5 + REG·0) → +0.6014   (Δ +0.00223)
```
err<0(보상이 예측보다 큼) → 관련 가중치 ↑ → **다음에 비슷한 상황의 Q[버리기]↑**. 모든 μ·c 가중치가 동시에 미세조정되고, 베이스에서 멀어지면 `REG`가 다시 끌어당긴다.

---

# 설계 근거 — "왜 이렇게?"

### 왜 퍼지(FCM)인가 — 하드 분류 대신
플레이어는 한 유형이 아니다. 불 위주지만 파편도 섞은 덱 = "화상형 0.6 + 파편형 0.3" 같은 **부분 소속**. 하드 분류(단일 라벨)면 혼합 빌드를 못 담고 경계에서 행동이 튄다. 퍼지 소속도 μ는 **여러 유형에 동시 부분 소속** → Q도 μ 가중합이라 빌드가 섞이거나 변할 때 **연속적·부드럽게** 대응(부록 D처럼 균등이면 자연히 상황이 결정).

### 왜 GK(Gustafson-Kessel)인가 — 일반 FCM 대신
일반 FCM은 **원형 군집(유클리드)** 가정. 하지만 유형마다 모양이 다르다 — 연속타격형은 "연쇄↑ + 저코스트↑"가 함께 묶인 **기울어진 타원**. 원형 가정은 이런 상관/신축을 못 잡아 오군집. GK는 **유형마다 공분산 F_i를 학습**해 타원형·기울어진 분포를 잡는다(그래서 거리도 마할라노비스).

### 왜 마할라노비스 거리인가 — 유클리드 대신
마할라노비스 `(x−v)ᵀA(x−v)`는 **그 유형의 퍼짐(공분산)으로 정규화한 거리**. 유형이 넓게 퍼진 방향의 차이는 "덜 멀게", 좁은 방향은 "더 멀게" 친다. 예: 안정형은 f4(방어) 분산이 커서 f4가 좀 달라도 같은 유형으로 포섭. 유클리드는 모든 축을 똑같이 취급 → 길쭉한 유형을 놓친다. 코드의 `A_k`(=CovInvs[k])가 그 유형의 "모양"을 인코딩한다.

### 왜 μ + 컨텍스트를 분리하나 (핵심)
- **유형(μ)** = 천천히 변하는 **빌드 성향**(덱 구성). **컨텍스트(c)** = 매 순간 바뀌는 **전투 상태**(HP/손패).
- 분리의 이득:
  - FCM은 **안정적인 덱 특성만** 군집 → 순간 상황 노이즈에 안 흔들림(같은 플레이어가 HP 따라 다른 유형으로 잡히지 않음).
  - RBFN이 그 위에 **상황 보정**을 선형으로 더함(적HP↓→회복Q↑, 손패↑→버리기Q↑).
  - 결과: "같은 화상형이어도 손패에 불카드 없으면 저주 대신 버리기"처럼 **유형 × 상황** 둘 다 반영.
- 만약 컨텍스트까지 FCM에 넣으면 유형 군집이 매 턴 출렁여 불안정 + 유형 의미가 흐려진다 → 분리가 정답. (부록 D가 실증: μ 균등인데 **ctx가 결정**)

### 왜 RBFN(은닉 μ + 선형 출력)인가
은닉층(RBF=μ)은 **비선형 유형 표현**(마할라노비스), 출력은 **선형**(Q=가중합). 선형 출력이라 빠르고 안정적이며 **유니티에 배열로 임베드** + **온라인 SGD**가 간단. 게임 런타임은 매 결정 = 행렬곱 몇 번 → 딥넷 불필요.

### 왜 보상기반 Q인가 — 지도분류 대신
"이 상태에서 이 행동이 얼마나 가치있나"를 **보상으로** 학습 → 규칙을 일일이 코딩 안 하고 데이터로 일반화. 지도분류는 "정답 행동" 라벨이 필요한데 실제론 정답이 하나가 아님(상황 의존). 보상회귀는 그대로 **온라인 학습으로 확장**된다(같은 식을 런타임 미니배치로).

### 왜 하이브리드(오프라인 베이스 + 온라인 미세조정)인가
- 순수 온라인만 → 초반 바보(데이터 0) + 발산 위험. 순수 오프라인만 → 실제 플레이어에 안 맞춤.
- 하이브리드 → 오프라인 베이스로 **즉시 그럴듯** + 온라인으로 **그 세션 플레이어에 적응**, **베이스로의 L2(REG)** 가 발산을 막는다(드리프트 제어). 부록 D의 `Δ +0.00223`처럼 한 번에 조금씩, 안전하게.
