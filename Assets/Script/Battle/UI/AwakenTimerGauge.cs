using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Battle.UI
{
    // 각성 동안 상단에 남은 시간을 표시하는 전용 게이지
    public class AwakenTimerGauge : MonoBehaviour
    {
        [Tooltip("켜고 끌 루트 오브젝트. 비우면 이 컴포넌트가 붙은 GameObject를 토글.")]
        [SerializeField] private GameObject root; // 토글할 루트 오브젝트
        [Tooltip("남은 시간 비율(0~1)을 표시할 Slider. (Image fill 방식이면 비워도 됨)")]
        [SerializeField] private Slider fillSlider; // 비율 표시용 슬라이더
        [Tooltip("남은 시간 비율(0~1)을 fillAmount로 표시할 Image(Image Type=Filled). (Slider 방식이면 비워도 됨)")]
        [SerializeField] private Image fillImage; // 비율 표시용 채움 이미지
        [Tooltip("남은 초를 표시할 TMP 텍스트.")]
        [SerializeField] private TMP_Text timeText; // 남은 초 텍스트
        [Tooltip("시간 텍스트 포맷. {0}에 남은 초가 들어감. 예: \"{0:0.0}s\"")]
        [SerializeField] private string timeFormat = "{0:0.0}s"; // 시간 텍스트 포맷

        // 토글 대상 루트(없으면 자기 GameObject)
        GameObject Root => root != null ? root : gameObject;

        // 게이지를 표시
        public void Show()
        {
            var go = Root;
            if (!go.activeSelf) go.SetActive(true);
            BringToFront();
        }

        // 게이지가 속한 캔버스 직계 가지를 형제 중 최상단으로 보냄
        void BringToFront()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;
            Transform canvasT = canvas.transform;
            Transform node = transform;
            while (node.parent != null && node.parent != canvasT)
                node = node.parent;
            if (node.parent == canvasT) node.SetAsLastSibling();
        }

        // 게이지를 숨김
        public void Hide()
        {
            var go = Root;
            if (go.activeSelf) go.SetActive(false);
        }

        // 남은/최대 시간으로 fill과 텍스트를 갱신
        public void SetTime(float remaining, float max)
        {
            float ratio = max > 0f ? Mathf.Clamp01(remaining / max) : 0f;
            if (fillSlider != null) fillSlider.value = ratio;
            if (fillImage != null) fillImage.fillAmount = ratio;
            if (timeText != null) timeText.text = string.Format(timeFormat, Mathf.Max(0f, remaining));
        }
    }
}
