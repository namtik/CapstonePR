using UnityEngine;

namespace Battle.Relic
{
    // 비급서 (10014): 각성 중 콤보 발동 성공 시 각성 시간이 추가로 증가한다.
    [CreateAssetMenu(menuName = "Relic/ComboAwakenTime", fileName = "ComboAwakenTimeRelic")]
    public class ComboAwakenTimeRelic : RelicSO
    {
        [Header("효과")]
        [Tooltip("콤보 성공 1회당 추가로 늘어나는 각성 시간(초) — 기본 콤보 보너스에 더해진다")]
        public float bonusSeconds = 0.5f; // 콤보당 추가 각성 시간

        // 콤보 매칭 성공 — 각성 시간 추가
        public override void OnComboTriggered(IRelicBattleContext ctx)
        {
            ctx.AddAwakenTimeSeconds(bonusSeconds);
        }
    }
}
