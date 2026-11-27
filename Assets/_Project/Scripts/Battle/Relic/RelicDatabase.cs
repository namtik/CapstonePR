using System.Collections.Generic;
using UnityEngine;

namespace Battle.Relic
{
    // 전체 유물 정의(RelicSO 에셋) 목록을 담는 데이터베이스.
    // 보상/상점 스테이지가 후보 풀로 사용한다. Resources/RelicDatabase.asset에 두면 자동 로드.
    [CreateAssetMenu(menuName = "Relic/RelicDatabase", fileName = "RelicDatabase")]
    public class RelicDatabase : ScriptableObject
    {
        [Tooltip("이 런에서 등장 가능한 모든 유물 에셋. 새 유물 .asset을 여기 등록한다.")]
        [SerializeField] private List<RelicSO> relics = new List<RelicSO>(); // 전체 유물 목록

        public IReadOnlyList<RelicSO> Relics => relics; // 읽기 전용 뷰
    }
}
