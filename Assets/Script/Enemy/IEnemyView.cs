using System;
using UnityEngine;

// 적 시각 표현(2D UI / 3D 모델)이 구현하는 공통 뷰 인터페이스.
// EnemyController/RoundManager는 이 인터페이스에만 의존하므로, 표현 방식(UI Image ↔ 3D Animator)을
// 바꿔도 전투 로직은 영향을 받지 않는다.
public interface IEnemyView
{
    // ── 수치/게이지/텍스트 ──
    // 행동 게이지 비율(0~1)을 갱신한다. stat.OnGaugeStepChanged에 구독됨.
    void UpdateActionGauge(float ratio);
    // 다음 공격 예고 데미지를 표시한다.
    void SetAttackPreviewDamage(int damage);
    // 데미지 숫자를 띄운다.
    void ShowDamage(float damage);
    // 데미지 숫자를 띄운다(화상 여부로 색 분기).
    void ShowDamage(float damage, bool isBurn);
    // 방해행동/특이사항 알림 텍스트를 표시한다.
    void ShowMidPatternNotice(string message);

    // ── 연출 ──
    // 카드 타입(Q/W/E/R/L)에 맞는 피격 이펙트를 재생한다.
    void PlayHitEffect(string cardType);
    // 적 공격 모션을 재생한다.
    void PlayAttackMotion();
    // 공격 모션 재생 가능 여부.
    bool HasAttackMotion { get; }
    // 진행 중인 피격 연출의 잔여 시간(초).
    float HitReactionRemaining { get; }
    // 공격 모션이 임팩트 프레임에 도달할 때 발생(공격 VFX 동기화용).
    event Action OnAttackMotionLastFrame;

    // ── 사망 ──
    // 사망 연출을 시작한다(3D: Die 애니, 2D: no-op).
    void PlayDeath();
    // 사망 연출 후 오브젝트 파괴까지 대기할 시간(초).
    float DeathDuration { get; }

    // ── 비주얼 주입(2D 스프라이트 기반; 3D 프리팹은 no-op) ──
    void SetSprite(Sprite sprite);
    void SetHitSprite(Sprite sprite);
    void SetAttackSprites(Sprite[] sprites);

    // 상태이상 패널이 따라갈 추적 대상(2D: 흔들리는 RectTransform, 3D: 모델 트랜스폼).
    Transform ShakeTarget { get; }
}
