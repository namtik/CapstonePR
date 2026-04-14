using UnityEngine;

/// <summary>
/// FCM 플레이어 유형 분석기 (3차원)
/// 
/// f1(긴급도), f2(집중도), f3(반복성)만으로 플레이어 유형을 판정한다.
/// f4(오염도)는 플레이어 유형과 무관한 게임 상태이므로 FCM에서 제외하고
/// 의사결정 트리에서 직접 참조한다.
/// </summary>
public static class FCMAnalyzer
{
    // 순서: [f1 긴급도, f2 집중도, f3 반복성]
    private static readonly float[,] Centroids = new float[,]
    {
        { 0.7993f, 0.4005f, 0.7725f },  // 콤보 러시형
        { 0.5034f, 0.8088f, 0.4789f },  // 경로 의존형
        { 0.1681f, 0.2110f, 0.2148f }   // 탐색/분산형
    };

    public const int ClusterCount = 3;
    public const int FeatureDim = 3; // f1, f2, f3만

    public static float[] CalcMembership(float[] features)
    {
        float[] distances = new float[ClusterCount];
        float[] membership = new float[ClusterCount];

        for (int k = 0; k < ClusterCount; k++)
        {
            float sum = 0f;
            for (int d = 0; d < FeatureDim; d++)
            {
                float diff = features[d] - Centroids[k, d];
                sum += diff * diff;
            }
            distances[k] = Mathf.Sqrt(sum);
        }

        for (int k = 0; k < ClusterCount; k++)
        {
            if (distances[k] < 1e-6f)
            {
                for (int j = 0; j < ClusterCount; j++)
                    membership[j] = (j == k) ? 1f : 0f;
                return membership;
            }
        }

        for (int i = 0; i < ClusterCount; i++)
        {
            float sum = 0f;
            for (int j = 0; j < ClusterCount; j++)
            {
                float ratio = distances[i] / distances[j];
                sum += ratio * ratio;
            }
            membership[i] = 1f / sum;
        }

        return membership;
    }

    public static int GetDominantType(float[] membership)
    {
        int best = 0;
        for (int i = 1; i < membership.Length; i++)
            if (membership[i] > membership[best]) best = i;
        return best;
    }

    public static string GetTypeName(int idx) => idx switch
    {
        0 => "콤보 러시형",
        1 => "경로 의존형",
        2 => "탐색/분산형",
        _ => "?"
    };
}