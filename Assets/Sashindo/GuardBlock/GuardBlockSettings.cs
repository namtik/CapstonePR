using UnityEngine;

namespace Sashindo.VFX
{
    [CreateAssetMenu(menuName = "Sashindo/VFX/Guard Block Settings")]
    public sealed class GuardBlockSettings : ScriptableObject
    {
        [Header("금빛 막")]
        [InspectorName("막 색상")] public Color FilmColor = new Color(0.87f, 0.65f, 0.17f, 1f);
        [Range(0f, 1f), InspectorName("막 불투명도")] public float FilmOpacity = 0.10f;
        [Min(0.01f), InspectorName("색이 차오르는 시간 (초)")] public float FadeIn = 0.10f;
        [Min(0f), InspectorName("색이 유지되는 시간 (초)")] public float Hold = 0.04f;
        [Min(0.01f), InspectorName("색이 사라지는 시간 (초)")] public float FadeOut = 0.46f;

        [Header("빛 파동")]
        [InspectorName("파동 색상")] public Color WaveColor = new Color(1f, 0.96f, 0.81f, 1f);
        [Range(0f, 1f), InspectorName("파동 밝기")] public float WaveBrightness = 0.36f;
        [Range(0f, 2f), InspectorName("굽어 보이는 강도 (% 화면 높이)")] public float Refraction = 0.57f;
        [Range(0.1f, 2.5f), InspectorName("파동 반경 (화면 높이 기준)")] public float Radius = 1.12f;
        [Range(0.25f, 3f), InspectorName("파동 세로 비율 (1 = 원형)")] public float VerticalRatio = 1f;
        [Range(0.004f, 0.08f), InspectorName("파동 폭 (화면 높이 기준)")] public float Width = 0.018f;
        [Min(0.02f), InspectorName("파동이 퍼지는 시간 (초)")] public float TravelTime = 0.54f;
        [InspectorName("파동 중심 (0~1)"), Tooltip("왼쪽 아래가 (0, 0), 화면 중앙은 (0.5, 0.5)입니다.")]
        public Vector2 Center = new Vector2(0.49f, 0.60f);
        [Min(0f), InspectorName("두 번째 파동까지 시간 (초)")] public float EchoDelay = 0.085f;
        [Range(0f, 1f), InspectorName("두 번째 파동 강도")] public float EchoStrength = 0.30f;

        public float Duration => Mathf.Max(
            Mathf.Max(0.01f, FadeIn) + Mathf.Max(0f, Hold) + Mathf.Max(0.01f, FadeOut),
            Mathf.Max(0.02f, TravelTime) + (EchoStrength > 0f ? Mathf.Max(0f, EchoDelay) : 0f));
    }
}
