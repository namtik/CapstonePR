using UnityEngine;

namespace Battle.Relic
{
    public enum RelicEffectType
    {
        None,
        /// <summary>이무기의 여의주: 각성 종료 후 성공한 콤보 수만큼 각성 게이지 회복.</summary>
        AwakenGaugeRecoverPerCombo,
        /// <summary>비급서: 각성 중 콤보 성공 시 보너스 시간 +0.5초 추가.</summary>
        ComboBonusSecondsBoost,
    }

    [System.Serializable]
    public class RelicDef
    {
        public string id;
        public string displayName;
        [TextArea(2, 4)] public string description;
        public Sprite icon;
        public RelicEffectType effect;
    }
}
