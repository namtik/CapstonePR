using UnityEngine;

/// <summary>
/// 정식(비개발) 빌드에서는 Debug.Log 출력을 끄고 경고·오류만 남긴다.
/// 에디터와 Development Build에서는 모든 로그가 그대로 출력된다.
/// 씬에 배치할 필요 없이 첫 씬 로드 전에 자동 실행된다.
/// </summary>
public static class ReleaseLogFilter
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Apply()
    {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
        Debug.unityLogger.filterLogType = LogType.Warning;
#endif
    }
}
