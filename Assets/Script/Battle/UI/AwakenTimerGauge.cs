using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Battle.UI
{
    /// <summary>
    /// 각성(피버) 동안 화면 상단에 남은 시간을 표시하는 전용 게이지.
    /// 오브젝트는 씬에 직접 배치하고 인스펙터에서 필드를 연결한다(코드로 생성하지 않음).
    /// NewBattleController가 각성 시작/종료/매 프레임에 Show()/Hide()/SetTime()을 호출한다.
    ///
    /// [씬 배치 방법]
    /// 1) 전투 캔버스 상단에 빈 UI 오브젝트(예: "AwakenTimerGauge")를 만들고 이 스크립트를 붙인다.
    /// 2) 그 아래에 게이지 바(Slider 또는 Image[Filled])와 남은 초 텍스트(TMP)를 자식으로 배치한다.
    /// 3) 아래 필드(fillSlider 또는 fillImage, timeText)를 인스펙터에서 연결한다.
    /// 4) NewBattleController 인스펙터의 'awakenTimerGauge' 필드에 이 오브젝트를 연결한다.
    /// </summary>
    public class AwakenTimerGauge : MonoBehaviour
    {
        [Tooltip("켜고 끌 루트 오브젝트. 비우면 이 컴포넌트가 붙은 GameObject를 토글.")]
        [SerializeField] private GameObject root;
        [Tooltip("남은 시간 비율(0~1)을 표시할 Slider. (Image fill 방식이면 비워도 됨)")]
        [SerializeField] private Slider fillSlider;
        [Tooltip("남은 시간 비율(0~1)을 fillAmount로 표시할 Image(Image Type=Filled). (Slider 방식이면 비워도 됨)")]
        [SerializeField] private Image fillImage;
        [Tooltip("남은 초를 표시할 TMP 텍스트.")]
        [SerializeField] private TMP_Text timeText;
        [Tooltip("시간 텍스트 포맷. {0}에 남은 초가 들어감. 예: \"{0:0.0}s\"")]
        [SerializeField] private string timeFormat = "{0:0.0}s";

        GameObject Root => root != null ? root : gameObject;

        /// <summary>게이지를 표시.</summary>
        public void Show()
        {
            var go = Root;
            if (!go.activeSelf) go.SetActive(true);
            BringToFront();
        }

        /// <summary>
        /// 각성 딤 오버레이(런타임에 캔버스 최상단으로 올라옴)보다 위에 그려지도록
        /// 이 게이지가 속한 '캔버스 직계 가지'를 형제 중 최상단으로 보낸다.
        /// 게이지가 딤과 같은 캔버스에 있을 때 유효. (별도 캔버스+Override Sorting을 쓰면 이 호출은 무해)
        /// </summary>
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

        /// <summary>게이지를 숨김.</summary>
        public void Hide()
        {
            var go = Root;
            if (go.activeSelf) go.SetActive(false);
        }

        /// <summary>남은 시간/최대 시간으로 fill과 텍스트를 갱신.</summary>
        public void SetTime(float remaining, float max)
        {
            float ratio = max > 0f ? Mathf.Clamp01(remaining / max) : 0f;
            if (fillSlider != null) fillSlider.value = ratio;
            if (fillImage != null) fillImage.fillAmount = ratio;
            if (timeText != null) timeText.text = string.Format(timeFormat, Mathf.Max(0f, remaining));
        }
    }
}
