using System;
using System.Collections.Generic;

namespace Battle.Card
{
    // 카드 효과 한 블록을 나타내는 데이터 모델
    [Serializable]
    public class CardEffectData
    {
        public int cardId;        // 대상 카드 ID
        public int index;         // 효과 순서 인덱스
        public string when;       // 발동 시점
        public string ifCond;     // 발동 조건
        public string doAction;   // 실행 동사
        public string target;     // 대상
        public int amount;        // 수치
        public string formula;    // 수치 공식
        public int hits;          // 타격 횟수
        public string hitFormula; // 타격 횟수 공식
        public string status;     // 상태이상 키
        public string cardFilter; // 카드 필터
        public string fromZone;   // 출발 존
        public string toZone;     // 도착 존
        public string select;     // 선택 모드
        public string repeat;     // 반복 모드
        public string extra;      // 추가 파라미터
        public string runtimeKey; // 런타임 키
    }

    // 카드 효과 목록(JSON 파싱용 래퍼)
    [Serializable]
    public class CardEffectDataList
    {
        public List<CardEffectData> effects = new List<CardEffectData>(); // 효과 리스트
    }
}
