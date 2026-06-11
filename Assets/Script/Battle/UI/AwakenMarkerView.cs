using UnityEngine;
using UnityEngine.UI;

namespace Battle.UI
{
    // 각성 콤보 표식의 등장(팝인)·대기(맥동/회전) 연출. 생성 직후 Begin() 호출.
    // 폭발(제거)은 CardEffectOverlay가 별도 이펙트와 함께 처리한다.
    [RequireComponent(typeof(RectTransform))]
    public class AwakenMarkerView : MonoBehaviour
    {
        [SerializeField] private float popInSeconds = 0.18f;       // 팝인(등장) 시간
        [SerializeField] private float idlePulseAmplitude = 0.08f; // 대기 맥동 진폭(스케일)
        [SerializeField] private float idlePulseSpeed = 3.2f;      // 대기 맥동 속도
        [SerializeField] private float spinDegPerSec = 35f;        // 대기 회전 속도(도/초)

        private RectTransform _rt;   // 표식 트랜스폼
        private float _elapsed;      // 생성 후 경과 시간

        // 생성 직후 호출 — 스케일 0에서 시작해 Update로 등장 연출
        public void Begin()
        {
            _rt = (RectTransform)transform;
            _elapsed = 0f;
            _rt.localScale = Vector3.zero;
        }

        // 팝인 후 대기 상태(맥동+회전)를 매 프레임 갱신
        void Update()
        {
            if (_rt == null) return;
            _elapsed += Time.deltaTime;

            if (_elapsed < popInSeconds)
            {
                // 살짝 튀어오르는 ease-out back으로 등장
                float t = popInSeconds > 0f ? _elapsed / popInSeconds : 1f;
                _rt.localScale = Vector3.one * EaseOutBack(t);
            }
            else
            {
                // 은은한 맥동으로 대기
                float pulse = 1f + Mathf.Sin((_elapsed - popInSeconds) * idlePulseSpeed) * idlePulseAmplitude;
                _rt.localScale = Vector3.one * pulse;
            }

            _rt.localRotation = Quaternion.Euler(0f, 0f, -_elapsed * spinDegPerSec);
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
