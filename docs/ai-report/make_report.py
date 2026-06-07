# -*- coding: utf-8 -*-
"""
적 AI FCM-RBFN — 카드게임 도메인 시뮬레이션 & 보고서 그림 생성
================================================================
- 실제 게임에 임베드된 가중치(Assets/Script/Battle/AI/EnemyAiModel.cs)를 그대로 사용.
- 시나리오: 단일 플레이어 20턴.
    · 턴 1~10  : 화상형(불) 덱 → AI 정답 카운터 = '저주'(curse)
    · 턴 11~20 : 파편형(땅) 덱으로 급변 → AI 정답 카운터 = '탈진'(exhaust)
- 그림1 (fig1_membership.png) : FCM 소속도 μ 추이 (플레이어 유형 추적)
- 그림2 (fig2_action.png)     : AI 방해행동 선택 변화 + 학습/강인성(하이브리드 vs 순수온라인)
- 부수 산출: sim_session.csv(대표 세션), learning_curve.csv(턴별 정답률)

추론/학습 수식은 EnemyDisruptionAI.cs / OnlineQLearner.cs 를 1:1 이식.
"""
import os, csv
import numpy as np
import matplotlib.pyplot as plt
import matplotlib.font_manager as fm

DIR = os.path.dirname(os.path.abspath(__file__))

# ── 한글 폰트 ──
for _name in ['Malgun Gothic', 'AppleGothic', 'NanumGothic', 'DejaVu Sans']:
    if _name in {f.name for f in fm.fontManager.ttflist}:
        plt.rcParams['font.family'] = _name
        break
plt.rcParams['axes.unicode_minus'] = False

# ── 색상 ──
TYPE_COLORS = ['#E24B4A', '#8B5E3C', '#1D9E75', '#E6A817', '#3B73C4']   # 화상/파편/연속/피버/안정
ACTION_COLORS = ['#E24B4A', '#8B5E3C', '#3B73C4', '#E6A817', '#1D9E75', '#9B59B6']

# ════════════════════════════════════════════════════════════════════
# 1. 실제 임베드 모델 (EnemyAiModel.cs 값 그대로 — 게임과 100% 동일)
# ════════════════════════════════════════════════════════════════════
M, TYPES, ACTIONS, FEAT, CTX = 2.0, 5, 6, 6, 8
TYPE_NAMES = ['화상형', '파편형', '연속타격형', '피버형', '안정형']
ACTION_KR = ['저주', '탈진', '흡수', '강화', '회복', '버리기']

CENTROIDS = np.array([
    [0.6536, 0.2026, 0.2770, 0.2131, 0.4439, 0.2937],   # 화상형
    [0.1975, 0.6629, 0.2746, 0.2816, 0.4166, 0.2721],   # 파편형
    [0.2366, 0.2373, 0.6268, 0.2206, 0.6548, 0.3282],   # 연속타격형
    [0.3038, 0.2340, 0.3337, 0.2482, 0.4816, 0.6492],   # 피버형
    [0.2440, 0.2730, 0.2700, 0.6112, 0.2920, 0.3005],   # 안정형
])

COV_INVS = [np.array(a) for a in [
    [[0.282546, 0.236618, 0.138131, 0.050305, 0.018885, -0.040634],
     [0.236618, 1.165412, 0.067680, -0.031883, 0.051406, -0.055375],
     [0.138131, 0.067680, 1.496882, 0.244895, -0.183721, -0.263416],
     [0.050305, -0.031883, 0.244895, 1.485455, -0.088881, -0.256718],
     [0.018885, 0.051406, -0.183721, -0.088881, 1.298722, -0.002473],
     [-0.040634, -0.055375, -0.263416, -0.256718, -0.002473, 1.487753]],
    [[1.228691, 0.240279, -0.163379, 0.067120, -0.208568, -0.079699],
     [0.240279, 0.312578, 0.107262, -0.111480, 0.047853, 0.115003],
     [-0.163379, 0.107262, 1.263483, 0.060140, 0.077501, 0.073855],
     [0.067120, -0.111480, 0.060140, 1.292757, 0.025528, -0.181403],
     [-0.208568, 0.047853, 0.077501, 0.025528, 1.596661, -0.045883],
     [-0.079699, 0.115003, 0.073855, -0.181403, -0.045883, 1.499597]],
    [[1.445083, 0.010714, 0.282955, -0.080884, 0.018394, 0.052865],
     [0.010714, 1.326710, 0.218885, 0.094258, 0.066178, -0.078550],
     [0.282955, 0.218885, 0.600686, 0.087061, -0.427283, -0.174066],
     [-0.080884, 0.094258, 0.087061, 1.354785, 0.022722, 0.081591],
     [0.018394, 0.066178, -0.427283, 0.022722, 0.919256, 0.063112],
     [0.052865, -0.078550, -0.174066, 0.081591, 0.063112, 1.553811]],
    [[1.159499, 0.137567, -0.113906, 0.056235, -0.163923, 0.066089],
     [0.137567, 1.174490, 0.041811, -0.003335, 0.024053, 0.281093],
     [-0.113906, 0.041811, 1.534461, -0.058689, 0.070176, -0.105652],
     [0.056235, -0.003335, -0.058689, 1.578969, 0.031531, 0.080261],
     [-0.163923, 0.024053, 0.070176, 0.031531, 1.441125, -0.149892],
     [0.066089, 0.281093, -0.105652, 0.080261, -0.149892, 0.315913]],
    [[1.207843, -0.070549, -0.001390, 0.193165, 0.012995, 0.040179],
     [-0.070549, 1.362177, 0.007252, 0.092950, -0.022120, -0.135809],
     [-0.001390, 0.007252, 1.234379, 0.127368, 0.187448, 0.112372],
     [0.193165, 0.092950, 0.127368, 0.495026, 0.521977, -0.069414],
     [0.012995, -0.022120, 0.187448, 0.521977, 1.306436, 0.009096],
     [0.040179, -0.135809, 0.112372, -0.069414, 0.009096, 1.620818]],
]]

QTYPE = np.array([
    [0.4849, -0.3705, -0.3456, -0.5292, 0.0241, -0.0353],   # 화상형
    [-0.4057, 1.0094, -0.1967, -0.3580, 0.0865, -0.0103],   # 파편형
    [0.6040, -0.3181, -0.2462, 0.7008, -0.0694, 0.1918],    # 연속타격형
    [-0.3860, -0.2166, 1.0087, -0.5234, -0.0090, -0.0596],  # 피버형
    [-0.4015, -0.1792, -0.3218, 0.7226, 0.3350, -0.0319],   # 안정형
])

QCTX = np.array([
    [0.0317, -0.0152, 0.0355, -0.0021, -1.1674, 0.0049],    # enemy_hp
    [-0.0089, -0.0112, -0.0017, -0.0043, 0.0677, -0.0410],  # player_hp
    [0.4715, -0.0601, -0.0232, 0.0787, 0.0454, 0.0358],     # hand_fire
    [-0.3579, 0.0648, -0.0116, -0.0516, 0.0816, 0.0064],    # hand_frag
    [0.1429, -0.0038, 0.0210, 0.0779, 0.1878, -0.0069],     # hand_chain
    [-0.1274, -0.0840, 0.0104, -0.0019, 0.0066, 0.0379],    # hand_def
    [-0.2343, 0.0076, -0.0976, -0.0908, 0.0458, -0.0186],   # hand_null
    [-0.0518, -0.1144, -0.0973, 0.0386, -0.1664, 0.5992],   # hand_count/10
])

QBIAS = np.array([-0.1204, -0.0860, -0.1241, 0.0223, 0.4338, 0.0865])

# 플레이어 '진짜 유형' 대표 특성 (make_virtual_data.py PROTOTYPES) — 로그 생성용
PROTO = np.array([
    [0.85, 0.15, 0.25, 0.20, 0.45, 0.30],   # 화상형
    [0.15, 0.85, 0.25, 0.30, 0.40, 0.25],   # 파편형
    [0.20, 0.20, 0.80, 0.20, 0.75, 0.35],   # 연속타격형
    [0.30, 0.20, 0.35, 0.25, 0.50, 0.85],   # 피버형
    [0.20, 0.25, 0.25, 0.80, 0.20, 0.30],   # 안정형
])

# ════════════════════════════════════════════════════════════════════
# 2. 추론 (EnemyDisruptionAI.cs 이식)
# ════════════════════════════════════════════════════════════════════
def membership(x):
    """마할라노비스 퍼지 소속도 μ[5]. FuzzyMembership() 이식."""
    inv = np.empty(TYPES)
    power = -1.0 / (M - 1.0)            # m=2 → -1
    for k in range(TYPES):
        d = x - CENTROIDS[k]
        dist2 = max(float(d @ COV_INVS[k] @ d), 1e-12)
        inv[k] = dist2 ** power
    s = inv.sum()
    return inv / s if s > 0 else np.full(TYPES, 1.0 / TYPES)


def avail_mask(hand_count, recover_ready):
    """AvailabilityMask() 이식."""
    m = np.ones(ACTIONS, dtype=bool)
    if hand_count < 2:
        m[5] = False                   # 버리기
    if recover_ready < 0.5:
        m[4] = False                   # 회복
    return m


# ════════════════════════════════════════════════════════════════════
# 3. 온라인 학습 에이전트 (OnlineQLearner.cs 이식)
# ════════════════════════════════════════════════════════════════════
LR, REG, BATCH, BUFFER = 0.02, 0.01, 4, 256


class QAgent:
    """base=True → 사전학습 베이스에서 warm-start(하이브리드).
       base=False → 작은 무작위 init(순수 온라인, 사전학습 없음)."""
    def __init__(self, base, seed):
        self.rng = np.random.RandomState(seed + 7)
        if base:
            self.Wt, self.Wc, self.b = QTYPE.copy(), QCTX.copy(), QBIAS.copy()
            self.bWt, self.bWc, self.bb = QTYPE, QCTX, QBIAS         # L2 당김 기준 = 사전학습값
        else:
            self.Wt = self.rng.normal(0, 0.05, (TYPES, ACTIONS))
            self.Wc = self.rng.normal(0, 0.05, (CTX, ACTIONS))
            self.b = np.zeros(ACTIONS)
            self.bWt = np.zeros((TYPES, ACTIONS))                    # L2 당김 기준 = 0
            self.bWc = np.zeros((CTX, ACTIONS))
            self.bb = np.zeros(ACTIONS)
        self.buf = []

    def q(self, mu, c):
        return self.b + mu @ self.Wt + c @ self.Wc

    def observe(self, mu, c, a, r):
        s = np.concatenate([mu, c])
        self.buf.append((s, a, float(np.clip(r, -1, 1))))
        if len(self.buf) > BUFFER:
            self.buf.pop(0)
        self._train()

    def _train(self):
        n = len(self.buf)
        if n == 0:
            return
        for _ in range(min(BATCH, n)):
            s, a, r = self.buf[self.rng.randint(n)]
            mu, c = s[:TYPES], s[TYPES:]
            pred = self.b[a] + mu @ self.Wt[:, a] + c @ self.Wc[:, a]
            err = pred - r
            self.Wt[:, a] -= LR * (err * mu + REG * (self.Wt[:, a] - self.bWt[:, a]))
            self.Wc[:, a] -= LR * (err * c + REG * (self.Wc[:, a] - self.bWc[:, a]))
            self.b[a] -= LR * err


def choose(agent, mu, c, hand_count, recover_ready, eps, rng):
    q = agent.q(mu, c)
    m = avail_mask(hand_count, recover_ready)
    if rng.rand() < eps:                               # ε 탐험
        av = np.where(m)[0]
        return int(rng.choice(av)), q, True
    qq = np.where(m, q, -1e18)
    return int(np.argmax(qq)), q, False


# ════════════════════════════════════════════════════════════════════
# 4. 시나리오 (단일 세션 20턴, 화상형→파편형 급변)
# ════════════════════════════════════════════════════════════════════
TURNS = 20
SWITCH = 10            # 0-based: 턴 11(idx10)부터 파편형
HAND_ATTR = {0: 2, 1: 3, 2: 4, 4: 5}   # 유형 → 압박 속성의 컨텍스트 인덱스(hand_fire=2 ...)


def gen_features(turn_idx, rng):
    """덱 특성 x[6]. 급변은 idx10~11에 2턴 램프(부드러운 전환)."""
    if turn_idx <= SWITCH - 1:      w = 0.0      # 화상형
    elif turn_idx == SWITCH:        w = 0.5      # 전환 1턴째
    else:                           w = 1.0      # 파편형
    proto = (1 - w) * PROTO[0] + w * PROTO[1]
    return np.clip(proto + rng.normal(0, 0.045, FEAT), 0.0, 1.0)


def gen_context(turn_idx, rng):
    """컨텍스트 c[8]. 손패는 현재 유형 압박 속성 위주(가끔 비어있음)."""
    ptype = 0 if turn_idx <= SWITCH - 1 else 1
    base = np.ones(5) * 0.6
    if rng.rand() < 0.72:                          # 보통 유형 속성 카드 보유
        base[ptype] += 1.8                         # 화상형→hand_fire(idx0), 파편형→hand_frag(idx1)
    hand = rng.dirichlet(base)                     # [fire,frag,chain,def,null]
    enemy_hp = float(np.clip(rng.uniform(0.55, 1.0), 0, 1))
    player_hp = float(np.clip(rng.uniform(0.45, 1.0), 0, 1))
    hand_count = int(rng.randint(3, 7))
    recover_ready = 1.0 if rng.rand() > 0.2 else 0.0
    c = np.array([enemy_hp, player_hp, hand[0], hand[1], hand[2], hand[3], hand[4],
                  min(hand_count / 10.0, 1.0)])
    return c, hand_count, recover_ready


def correct_action(turn_idx):
    """시나리오 정답 카운터: 화상형→저주(0), 파편형→탈진(1)."""
    return 0 if turn_idx <= SWITCH - 1 else 1


def reward(turn_idx, a):
    """보상함수: 정답 카운터 +1.0, 그 외 -0.5."""
    return 1.0 if a == correct_action(turn_idx) else -0.5


def run_session(seed, base=True, online=True, eps=0.10):
    rng = np.random.RandomState(seed)
    agent = QAgent(base, seed)
    rec = []
    for t in range(TURNS):
        x = gen_features(t, rng)
        mu = membership(x)
        c, hc, rr = gen_context(t, rng)
        a, q, explored = choose(agent, mu, c, hc, rr, eps, rng)
        r = reward(t, a)
        if online:
            agent.observe(mu, c, a, r)
        rec.append(dict(turn=t + 1, mu=mu, x=x, c=c, action=a, q=q,
                        correct=int(a == correct_action(t)), explored=explored,
                        drift=float(np.sqrt(((agent.Wt - agent.bWt) ** 2).sum()
                                            + ((agent.Wc - agent.bWc) ** 2).sum()))))
    return rec


# ════════════════════════════════════════════════════════════════════
# 5. 실행 + 그림
# ════════════════════════════════════════════════════════════════════
def main():
    # ── 대표 세션(소속도/선택 시계열) ──
    rep = run_session(seed=2026, base=True, online=True)
    mu_series = np.array([r['mu'] for r in rep])        # 20 x 5
    act_series = np.array([r['action'] for r in rep])
    corr_series = np.array([r['correct'] for r in rep])

    # ── 다(多)시드 평균 학습/적응 곡선 ──
    SEEDS = range(1000, 1040)                            # 40 세션
    hyb = np.array([[r['correct'] for r in run_session(s, base=True, online=True)] for s in SEEDS])
    onl = np.array([[r['correct'] for r in run_session(s, base=False, online=True)] for s in SEEDS])
    fix = np.array([[r['correct'] for r in run_session(s, base=True, online=False)] for s in SEEDS])
    hyb_rate, onl_rate, fix_rate = hyb.mean(0) * 100, onl.mean(0) * 100, fix.mean(0) * 100
    turns = np.arange(1, TURNS + 1)

    print(f"[정답률] 하이브리드 전체 {hyb.mean()*100:.1f}% / 순수온라인 {onl.mean()*100:.1f}% / 고정베이스 {fix.mean()*100:.1f}%")
    print(f"[급변직후 11~13턴] 하이브리드 {hyb[:,10:13].mean()*100:.1f}% / 순수온라인 {onl[:,10:13].mean()*100:.1f}%")

    # ─────────────────────────────────────────────────────────────
    # 그림 1 — FCM 소속도 추이
    # ─────────────────────────────────────────────────────────────
    fig, ax = plt.subplots(figsize=(9, 5.2))
    for t in range(TYPES):
        ax.plot(turns, mu_series[:, t], '-o', color=TYPE_COLORS[t],
                label=TYPE_NAMES[t], lw=2.2, ms=5)
    ax.axvspan(0.5, SWITCH + 0.5, color='#E24B4A', alpha=0.05)
    ax.axvspan(SWITCH + 0.5, TURNS + 0.5, color='#8B5E3C', alpha=0.05)
    ax.axvline(SWITCH + 0.5, color='#333', ls='--', lw=1.4)
    ax.annotate('급변 후 1턴 만에\n파편형으로 추적',
                xy=(12, 0.829), xytext=(13.0, 0.55), fontsize=9, color='#8B5E3C',
                ha='left', arrowprops=dict(arrowstyle='->', color='#8B5E3C', lw=1.2))
    ax.text(SWITCH + 0.5, 0.12, '스타일 급변', fontsize=8.5, color='#333', ha='center',
            bbox=dict(boxstyle='round,pad=0.25', fc='white', ec='#333', alpha=0.85))
    ax.text(3.2, 0.5, '화상형 덱 구간\nμ(화상형) 약 0.8', fontsize=9.5, color='#E24B4A', ha='center')
    ax.text(16.4, 0.62, '파편형 덱 구간\nμ(파편형) 약 0.8', fontsize=9.5, color='#8B5E3C', ha='center')
    ax.set_title('① FCM 소속도 추이 — 플레이어 유형 실시간 추적', fontsize=13, fontweight='bold', pad=24)
    ax.set_xlabel('전투 턴'); ax.set_ylabel('퍼지 소속도 μ')
    ax.set_xticks(turns); ax.set_ylim(0, 1.02); ax.set_xlim(0.5, TURNS + 0.5)
    ax.grid(alpha=0.3)
    ax.legend(loc='lower center', bbox_to_anchor=(0.5, 1.002), ncol=5, fontsize=8.5, framealpha=0.9)
    plt.tight_layout()
    p1 = os.path.join(DIR, 'fig1_membership.png')
    plt.savefig(p1, dpi=150, facecolor='white'); plt.close()
    print('저장:', p1)

    # ─────────────────────────────────────────────────────────────
    # 그림 2 — AI 방해행동 선택 + 학습/강인성
    # ─────────────────────────────────────────────────────────────
    fig, (axA, axB) = plt.subplots(2, 1, figsize=(11, 9.2),
                                   gridspec_kw={'height_ratios': [1.05, 1]})

    # (위) 대표 세션의 매 턴 선택 행동 + 정답 카운터 음영
    axA.axhspan(-0.4, 0.4, xmin=0, xmax=SWITCH / TURNS, color='#E24B4A', alpha=0.12)
    axA.axhspan(0.6, 1.4, xmin=SWITCH / TURNS, xmax=1, color='#8B5E3C', alpha=0.12)
    for t in range(TURNS):
        a = act_series[t]
        ok = corr_series[t]
        axA.scatter(t + 1, a, s=130, color=ACTION_COLORS[a],
                    edgecolors='#222' if ok else '#E24B4A',
                    linewidths=1.6 if ok else 2.4, zorder=3,
                    marker='o' if ok else 'X')
    axA.axvline(SWITCH + 0.5, color='#333', ls='--', lw=1.3)
    axA.text(5.5, 4.6, '화상형 구간 · 정답=저주', fontsize=9, color='#E24B4A', ha='center')
    axA.text(15.5, 4.6, '파편형 구간 · 정답=탈진', fontsize=9, color='#8B5E3C', ha='center')
    axA.set_title('② 적 AI 방해행동 선택 (대표 세션) — X 표시는 비정답/탐험',
                  fontsize=12.5, fontweight='bold')
    axA.set_xlabel('전투 턴'); axA.set_ylabel('선택한 방해행동')
    axA.set_yticks(range(ACTIONS)); axA.set_yticklabels(ACTION_KR)
    axA.set_xticks(turns); axA.set_xlim(0.5, TURNS + 0.5); axA.set_ylim(-0.6, 5.6)
    axA.grid(alpha=0.25, axis='x')

    # (아래) 다시드 평균 카운터 정답률 — 하이브리드 vs 순수온라인 vs 고정베이스
    axB.plot(turns, hyb_rate, '-o', color='#1D9E75', lw=2.4, ms=5, label='하이브리드(사전학습+온라인)')
    axB.plot(turns, onl_rate, '-s', color='#E6A817', lw=2.2, ms=5, label='순수 온라인(사전학습 없음)')
    axB.plot(turns, fix_rate, '--', color='#3B73C4', lw=1.8, label='고정 베이스(온라인 OFF)')
    axB.axvline(SWITCH + 0.5, color='#333', ls='--', lw=1.3)
    axB.annotate('급변 직후 일시 하락→회복\n(강인성)', (SWITCH + 0.7, 30), fontsize=9, color='#333')
    axB.set_title('③ 카운터 정답률 — 사전학습의 즉시성 + 온라인 학습의 적응 (40세션 평균)',
                  fontsize=12, fontweight='bold')
    axB.set_xlabel('전투 턴'); axB.set_ylabel('정답 카운터 선택률 (%)')
    axB.set_xticks(turns); axB.set_xlim(0.5, TURNS + 0.5); axB.set_ylim(0, 105)
    axB.grid(alpha=0.3); axB.legend(loc='lower right', fontsize=9)
    plt.tight_layout()
    p2 = os.path.join(DIR, 'fig2_action.png')
    plt.savefig(p2, dpi=150, facecolor='white'); plt.close()
    print('저장:', p2)

    # ── CSV 산출(재현성) ──
    with open(os.path.join(DIR, 'sim_session.csv'), 'w', encoding='utf-8-sig', newline='') as f:
        w = csv.writer(f)
        w.writerow(['turn'] + [f'mu_{n}' for n in TYPE_NAMES]
                   + ['action', 'action_kr', 'correct', 'explored', 'drift'])
        for r in rep:
            w.writerow([r['turn']] + [f"{v:.4f}" for v in r['mu']]
                       + [r['action'], ACTION_KR[r['action']], r['correct'],
                          int(r['explored']), f"{r['drift']:.4f}"])
    with open(os.path.join(DIR, 'learning_curve.csv'), 'w', encoding='utf-8-sig', newline='') as f:
        w = csv.writer(f)
        w.writerow(['turn', 'hybrid_rate', 'online_only_rate', 'fixed_base_rate'])
        for i in range(TURNS):
            w.writerow([i + 1, f'{hyb_rate[i]:.1f}', f'{onl_rate[i]:.1f}', f'{fix_rate[i]:.1f}'])
    print('CSV 저장 완료')


if __name__ == '__main__':
    main()
