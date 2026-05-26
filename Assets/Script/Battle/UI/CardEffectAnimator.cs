using UnityEngine;
using UnityEngine.UI;

namespace Battle.UI
{
    /// <summary>
    /// 스프라이트 시트(여러 Sprite로 슬라이스된 시트)를 프레임 단위로 재생하는 1회용 컴포넌트.
    /// 재생 끝나면 자동으로 GameObject 파괴.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class CardEffectAnimator : MonoBehaviour
    {
        private Sprite[] _frames;
        private float _fps = 12f;
        private float _timer;
        private int _currentFrame;
        private bool _loop;
        private bool _initialized;
        private Image _image;

        /// <summary>
        /// 프레임 배열 + FPS로 애니메이션 시작.
        /// loop=false이면 끝까지 재생 후 자기 자신 파괴.
        /// preserveAspect=false이면 RectTransform 폭에 맞춰 늘림(풀폭 띠 같은 형태).
        /// </summary>
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
