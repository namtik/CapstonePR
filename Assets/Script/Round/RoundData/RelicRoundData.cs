using System.Collections.Generic;
using UnityEngine;
using Battle.Relic;

[CreateAssetMenu(menuName = "Round/RelicRound")]
public class RelicRoundData : RoundData
{
    [Header("후보 유물 효과 (비어있으면 랜덤 전체에서 선택)")]
    public List<RelicEffectType> candidateRelics = new List<RelicEffectType>(); // 지급 후보 유물 효과 목록

    // 유물 라운드 핸들러를 생성한다.
    public override IRoundHandler CreateHandler()
    {
        return new RelicRoundHandler(this);
    }

    // 에디터에서 값 변경 시 라운드 타입을 Relic으로 강제한다.
    void OnValidate()
    {
        roundType = NodeType.Relic;
    }
}
