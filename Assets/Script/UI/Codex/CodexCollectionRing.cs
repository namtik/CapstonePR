using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Battle.UI
{
    // 도감 수집률 고리 — 12시 방향에서 시계 방향으로 차오르고 가운데에 퍼센트를 표시
    public class CodexCollectionRing : MonoBehaviour
    {
        [SerializeField] private Image fill;      // 채워지는 고리(Radial360)
        [SerializeField] private TMP_Text label;  // 가운데 퍼센트

        // 발견 수/전체로 고리와 문구를 갱신
        public void Set(int discovered, int total)
        {
            float ratio = total > 0 ? Mathf.Clamp01((float)discovered / total) : 0f;
            int pct = Mathf.RoundToInt(ratio * 100f);

            if (fill != null) fill.fillAmount = ratio;
            if (label != null) label.text = pct + "%";
        }
    }
}
