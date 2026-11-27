using UnityEngine;

namespace Battle.Relic
{
    // 황룡향로 (10009): 각성 유지 시간이 증가한다.
    [CreateAssetMenu(menuName = "Relic/AwakenDuration", fileName = "AwakenDurationRelic")]
    public class AwakenDurationRelic : RelicSO
    {
        [Header("효과")]
        [Tooltip("각성 유지 시간 증가량(초)")]
        public float extraSeconds = 2f; // 각성 지속 증가(초)

        public override float ModifyAwakenDurationSeconds() => extraSeconds;
    }
}
