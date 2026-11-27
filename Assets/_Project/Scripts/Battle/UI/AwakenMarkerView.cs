using UnityEngine;
using UnityEngine.UI;

namespace Battle.UI
{
    // 각성 콤보 표식의 등장(팝인) 연출. 생성 직후 Begin() 호출.
    // 팝인 후에는 회전·맥동 없이 완전히 고정된다. 폭발(제거)은 CardEffectOverlay가 처리.
    [RequireComponent(typeof(RectTransform))]
    public class AwakenMarkerView : MonoBehaviour
    {
        [SerializeField] private float popInSeconds = 0.18f; // 팝인(등장) 시간

        private RectTransform _rt;   // 표식 트랜스폼
        private float _elapsed;      // 생성 후 경과 시간
        private bool _settled;       // 팝인 종료 후 고정 여부

        // 생성 직후 호출 — 스케일 0에서 시작해 Update로 등장 연출
        public void Begin()
        {
            _rt = (RectTransform)transform;
            _elapsed = 0f;
            _settled = false;
            _rt.localScale = Vector3.zero;
            _rt.localRotation = Quaternion.identity; // 회전 없음(고정)
        }

        // 팝인만 갱신하고, 끝나면 스케일을 1로 고정한 뒤 더 이상 움직이지 않음
        void Update()
        {
            if (_rt == null || _settled) return;
            _elapsed += Time.deltaTime;

            if (_elapsed < popInSeconds)
            {
                // 살짝 튀어오르는 ease-out back으로 등장
                float t = popInSeconds > 0f ? _elapsed / popInSeconds : 1f;
                _rt.localScale = Vector3.one * EaseOutBack(t);
            }
            else
            {
                // 등장 종료 — 정확히 1로 고정하고 정지
                _rt.localScale = Vector3.one;
                _settled = true;
            }
        }

        // 1을 살짝 넘었다 정착하는 등장 곡선
        static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float p = t - 1f;
            return 1f + c3 * p * p * p + c1 * p * p;
        }
    }
}
