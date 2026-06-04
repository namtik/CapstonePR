# -*- coding: utf-8 -*-
"""FCM-RBFN 적 AI 시스템 구조도(블록 다이어그램) PNG 생성."""
import os
import matplotlib.pyplot as plt
import matplotlib.font_manager as fm
from matplotlib.patches import FancyBboxPatch

for _n in ['Malgun Gothic', 'AppleGothic', 'NanumGothic', 'DejaVu Sans']:
    if _n in {f.name for f in fm.fontManager.ttflist}:
        plt.rcParams['font.family'] = _n
        break
plt.rcParams['axes.unicode_minus'] = False
DIR = os.path.dirname(os.path.abspath(__file__))


def box(ax, x, y, w, h, text, fc, fs=10, bold=False, ec='#444'):
    ax.add_patch(FancyBboxPatch((x, y), w, h,
                 boxstyle="round,pad=0.015,rounding_size=0.10",
                 fc=fc, ec=ec, lw=1.5))
    ax.text(x + w / 2, y + h / 2, text, ha='center', va='center',
            fontsize=fs, fontweight='bold' if bold else 'normal', linespacing=1.45)


def arrow(ax, p1, p2, text=None, color='#333', rad=0.0, dx=0.0, dy=0.16):
    ax.annotate('', xy=p2, xytext=p1,
                arrowprops=dict(arrowstyle='-|>', color=color, lw=1.7,
                                connectionstyle=f'arc3,rad={rad}'))
    if text:
        ax.text((p1[0] + p2[0]) / 2 + dx, (p1[1] + p2[1]) / 2 + dy, text,
                ha='center', va='bottom', fontsize=9.5, color=color, fontweight='bold')


fig, ax = plt.subplots(figsize=(12, 8.4))
ax.set_xlim(0, 12); ax.set_ylim(0, 9); ax.axis('off')

# ── 영역 배경 ──
ax.add_patch(FancyBboxPatch((0.2, 4.85), 11.6, 3.75,
             boxstyle="round,pad=0.02,rounding_size=0.12",
             fc='#EAF2FB', ec='#3B73C4', lw=1.8, ls='--'))
ax.text(0.45, 8.35, '[ 오프라인 사전학습 · Python ]', ha='left',
        fontsize=12.5, fontweight='bold', color='#2C5AA0')
ax.add_patch(FancyBboxPatch((0.2, 0.2), 11.6, 3.95,
             boxstyle="round,pad=0.02,rounding_size=0.12",
             fc='#EAF7F0', ec='#1D9E75', lw=1.8, ls='--'))
ax.text(0.45, 3.9, '[ 실시간 추론 · Unity C# ]', ha='left',
        fontsize=12.5, fontweight='bold', color='#15805F')

# ── 오프라인 단계 ──
box(ax, 0.55, 6.35, 2.35, 1.2, '전투 로그 CSV\n특성 6 + 컨텍스트 8\n+ 선택 행동', '#FFFFFF', 9.5)
box(ax, 3.55, 6.15, 2.5, 1.6, '① FCM (GK)\n퍼지 군집\n중심점 v_i · 공분산역 A_i\n(유형 5)', '#FDE7E7', 9.5, True)
box(ax, 6.7, 6.15, 2.5, 1.6, '② RBFN Q학습\n보상기반 미니배치\nWtype · Wctx · b', '#FEF4DA', 9.5, True)
box(ax, 9.75, 6.35, 1.95, 1.2, 'EnemyAiModel.cs\n가중치 임베드', '#FFFFFF', 9.5)
arrow(ax, (2.9, 6.95), (3.55, 6.95))
arrow(ax, (6.05, 6.95), (6.7, 6.95), 'μ')
arrow(ax, (9.2, 6.95), (9.75, 6.95))

# ── 게임 빌드 연결 (오프라인 → 런타임) ──
arrow(ax, (10.72, 6.35), (10.72, 4.2), '게임 빌드에\n임베드', color='#888', dx=0.0, dy=-0.55)

# ── 런타임 단계 ──
box(ax, 0.5, 2.75, 2.45, 0.95, '플레이어 덱/플레이\n→ 특성 x(6)', '#FFFFFF', 9.5)
box(ax, 0.5, 1.05, 2.45, 0.95, '전투 상황(HP·손패)\n→ 컨텍스트 c(8)', '#FFFFFF', 9.5)
box(ax, 3.5, 2.55, 2.4, 1.1, '③ FCM 소속도\nμ(5)\n마할라노비스', '#FDE7E7', 9.5, True)
box(ax, 6.45, 1.75, 2.55, 1.45, '④ 퍼지 가중평균 융합\nQ(a)=Σ μ_i·Wtype\n+ Σ c_j·Wctx + b', '#FEF4DA', 9.5, True)
box(ax, 9.45, 1.75, 2.2, 1.45, '가용성 마스크\n→ argmax\n→ 방해행동 6종', '#E7F0FB', 9.5, True)
arrow(ax, (2.95, 3.22), (3.5, 3.15))
arrow(ax, (5.9, 3.0), (6.6, 2.75), 'μ')
arrow(ax, (2.95, 1.55), (6.45, 2.15), 'c')
arrow(ax, (9.0, 2.47), (9.45, 2.47))

# ── 온라인 학습 루프 ──
arrow(ax, (10.55, 1.75), (10.55, 0.62), color='#C0392B', dy=0.0)
arrow(ax, (10.55, 0.62), (7.7, 0.62), color='#C0392B', dy=0.0)
arrow(ax, (7.7, 0.62), (7.7, 1.75), color='#C0392B', dy=0.0)
ax.text(9.1, 0.46, '(온라인) 전투 결과 보상 → W 미세조정 + 베이스로 L2 정규화',
        ha='center', fontsize=9, color='#C0392B', fontweight='bold')

ax.set_title('FCM-RBFN 적 AI 시스템 구조도', fontsize=15, fontweight='bold', pad=2)
plt.tight_layout()
out = os.path.join(DIR, 'fig0_architecture.png')
plt.savefig(out, dpi=150, facecolor='white', bbox_inches='tight')
plt.close()
print('저장:', out)
