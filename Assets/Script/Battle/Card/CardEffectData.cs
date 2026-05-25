using System;
using System.Collections.Generic;

namespace Battle.Card
{
    /// <summary>
    /// CardEffects.json 한 줄 = 한 효과 블록.
    /// 카드 한 장은 EffectIndex 1, 2, ... 로 여러 효과를 가질 수 있다.
    /// (Phase 1: 데이터로 로드해 메타데이터로 보존. 인터프리터는 단계적으로 확장.)
    /// </summary>
    [Serializable]
    public class CardEffectData
    {
        public int cardId;
        public int index;
        public string when;
        public string ifCond;
        public string doAction;
        public string target;
        public int amount;
        public string formula;
        public int hits;
        public string hitFormula;
        public string status;
        public string cardFilter;
        public string fromZone;
        public string toZone;
        public string select;
        public string repeat;
        public string extra;
        public string runtimeKey;
    }

    [Serializable]
    public class CardEffectDataList
    {
        public List<CardEffectData> effects = new List<CardEffectData>();
    }
}
