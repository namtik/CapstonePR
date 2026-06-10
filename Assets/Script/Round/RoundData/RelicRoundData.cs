using System.Collections.Generic;
using UnityEngine;
using Battle.Relic;

[CreateAssetMenu(menuName = "Round/RelicRound")]
public class RelicRoundData : RoundData
{
    [Header("후보 유물 효과 (비어있으면 랜덤 전체에서 선택)")]
    public List<RelicEffectType> candidateRelics = new List<RelicEffectType>();

    public override IRoundHandler CreateHandler()
    {
        return new RelicRoundHandler(this);
    }

    void OnValidate()
    {
        roundType = NodeType.Relic;
    }
}
