using UnityEngine;

/// <summary>
/// 게임 시작 시 프레임레이트를 60으로 제한한다(노트북 발열 감소).
/// 씬에 배치할 필요 없이 첫 씬 로드 전에 자동 실행된다.
/// </summary>
public static class FrameRateLimiter
{
    public const int TargetFrameRate = 60;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Apply()
    {
        // vSync가 켜져 있으면 targetFrameRate가 무시되므로 끈다.
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = TargetFrameRate;
    }
}
