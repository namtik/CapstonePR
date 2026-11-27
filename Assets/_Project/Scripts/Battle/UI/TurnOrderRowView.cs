using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Battle.UI
{
    // 턴 예고 바의 몬스터 한 행(프리팹) — 배치·이미지는 프리팹, 값 채우기는 TurnOrderTimeline이 한다.
    // 행동 칸 두 개는 종류가 고정이 아니다:
    //   1순위 칸(초상화 옆) = 가장 먼저 올 행동(공격 또는 방해), 2순위 칸(패널 밖) = 그다음 행동.
    //   아이콘은 행동 종류에 맞춰 TurnOrderTimeline이 바꿔 끼운다.
    [RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
    public class TurnOrderRowView : MonoBehaviour
    {
        [Header("패널")]
        [Tooltip("행 배경 패널. 행동 중일 때 색이 바뀐다.")]
        public Image panel;

        [Header("초상화")]
        public Image portrait;
        [Tooltip("초상화가 없을 때 이름 첫 글자")]
        public TMP_Text initial;

        [Header("1순위 행동 (초상화 옆) — 가장 먼저 올 행동")]
        [FormerlySerializedAs("attackIcon")]
        public Image primaryIcon;
        [Tooltip("행동까지 남은 턴")]
        [FormerlySerializedAs("attackNumber")]
        public TMP_Text primaryNumber;
        [Tooltip("아이콘 이미지가 없을 때 표시할 글자(선택)")]
        public TMP_Text primaryFallback;

        [Header("2순위 행동 (패널 밖) — 그다음 행동")]
        [Tooltip("2순위 아이콘·숫자를 묶은 부모. 다음 행동이 없으면 숨김.")]
        [FormerlySerializedAs("disruptGroup")]
        public GameObject secondaryGroup;
        [FormerlySerializedAs("disruptIcon")]
        public Image secondaryIcon;
        [Tooltip("아이콘 이미지가 없을 때 표시할 글자(선택)")]
        [FormerlySerializedAs("disruptFallback")]
        public TMP_Text secondaryFallback;
        [Tooltip("행동까지 남은 턴")]
        [FormerlySerializedAs("disruptNumber")]
        public TMP_Text secondaryNumber;
    }
}
