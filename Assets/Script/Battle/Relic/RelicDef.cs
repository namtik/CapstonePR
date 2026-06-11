using UnityEngine;

namespace Battle.Relic
{
    // 유물 효과 종류
    public enum RelicEffectType
    {
        None, // 효과 없음
        AwakenGaugeRecoverPerCombo, // 이무기의 여의주: 각성 종료 후 성공 콤보 수만큼 게이지 회복
        ComboBonusSecondsBoost, // 비급서: 각성 중 콤보 성공 시 보너스 시간 +0.5초
        FrostEnemyOnBattleStart, // 한설의 결정: 전투 라운드 진입 시 적에게 빙결 7 부여
    }

    // 유물 한 종의 정의 데이터
    [System.Serializable]
    public class RelicDef
    {
        public string id; // 고유 식별자
        public string displayName; // 표시 이름
        [TextArea(2, 4)] public string description; // 설명
        public Sprite icon; // 아이콘
        public RelicEffectType effect; // 효과 종류
    }
}
