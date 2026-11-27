using UnityEngine;
using UnityEngine.UI;

namespace Battle.UI
{
    // 스프라이트 시트를 프레임 단위로 재생하는 1회용 컴포넌트(끝나면 자동 파괴)
    [RequireComponent(typeof(Image))]
    public class CardEffectAnimator : MonoBehaviour
    {
        private Sprite[] _frames; // 재생할 프레임 배열
        private float _fps = 12f; // 초당 프레임 수
        private float _timer; // 프레임 전환 누적 시간
        private int _currentFrame; // 현재 프레임 인덱스
        private bool _loop; // 반복 재생 여부
        private bool _initialized; // 재생 초기화 완료 여부
        private Image _image; // 프레임을 그릴 이미지

        // 프레임 배열과 FPS로 애니메이션 재생을 시작
        public void Play(Sprite[] frames, float fps = 12f, bool loop = false, bool preserveAspect = true)
        {
            _image = GetComponent<Image>();
            if (frames == null || frames.Length == 0)
            {
                Destroy(gameObject);
                return;
            }
            _frames = frames;
            _fps = Mathf.Max(1f, fps);
            _timer = 0f;
            _currentFrame = 0;
            _loop = loop;
            _initialized = true;

            _image.sprite = _frames[0];
            _image.color = Color.white;
            _image.preserveAspect = preserveAspect;
            _image.raycastTarget = false;
        }

        // 경과 시간에 따라 프레임을 진행하고 끝나면 파괴
        void Update()
        {
            if (!_initialized || _frames == null || _frames.Length == 0) return;

            _timer += Time.deltaTime;
            float frameDuration = 1f / _fps;
            if (_timer < frameDuration) return;

            _timer -= frameDuration;
            _currentFrame++;
            if (_currentFrame >= _frames.Length)
            {
                if (_loop) { _currentFrame = 0; }
                else { Destroy(gameObject); return; }
            }
            _image.sprite = _frames[_currentFrame];
        }
    }
}
