using System;
using System.Collections.Generic;

namespace Battle.Card
{
    // 콤보 1개(특정 슬롯 순서) 정의 데이터 모델
    [Serializable]
    public class ComboSkillData
    {
        public int id;            // 콤보 ID
        public string slot1;      // 슬롯1 속성
        public string slot2;      // 슬롯2 속성
        public string slot3;      // 슬롯3 속성
        public int cooldown;      // 재사용까지 필요한 각성 입력 횟수
        public string comboName;  // 표시 이름
        public string skillImg;   // 스킬 아이콘 이미지명
        public string description; // 설명 문구
        public int refComboId;    // 효과 정의 참조 ID
    }

    // 콤보 정의 목록(JSON 파싱용 래퍼)
    [Serializable]
    public class ComboSkillDataList
    {
        public List<ComboSkillData> combos = new List<ComboSkillData>(); // 콤보 리스트
    }

    // 콤보 효과 한 블록을 나타내는 데이터 모델
    [Serializable]
    public class ComboEffectData
    {
        public int refComboId;    // 참조 콤보 ID
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

    // 콤보 효과 목록(JSON 파싱용 래퍼)
    [Serializable]
    public class ComboEffectDataList
    {
        public List<ComboEffectData> effects = new List<ComboEffectData>(); // 효과 리스트
    }
}
