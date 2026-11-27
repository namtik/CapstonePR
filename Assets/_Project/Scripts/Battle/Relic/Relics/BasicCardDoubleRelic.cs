using UnityEngine;

namespace Battle.Relic
{
    // 태초의 서 (10004): 획득 시, 기본 카드(시작 8종)의 효과 수치가 2배가 된다. (런 지속)
    [CreateAssetMenu(menuName = "Relic/BasicCardDouble", fileName = "BasicCardDoubleRelic")]
    public class BasicCardDoubleRelic : RelicSO
    {
        public override void OnAcquired(IRelicRunContext run) => run.EnableBasicCardEffectDouble();
    }
}
