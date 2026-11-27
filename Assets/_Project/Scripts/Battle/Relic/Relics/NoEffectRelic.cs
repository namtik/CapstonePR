using UnityEngine;

namespace Battle.Relic
{
    // 효과가 아직 없는(개발중) 유물 — 아이콘/이름만 보유 표시용.
    // 스프라이트 전용 보상(RelicManager.TryAddSpriteOnlyRelic)이 런타임 생성에 사용한다.
    [CreateAssetMenu(menuName = "Relic/NoEffectRelic", fileName = "NoEffectRelic")]
    public class NoEffectRelic : RelicSO
    {
        // 효과 없음 — 모든 훅/수정자를 베이스 기본값(무동작) 그대로 사용.
    }
}
