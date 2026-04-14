using UnityEngine;

/// <summary>
/// FCM 플레이어 유형 분석기
/// 4차원 특성 벡터를 받아 3개 클러스터에 대한 소속도를 계산한다.
/// 
/// 클러스터:
///   0 = 콤보 러시형 (긴급도·반복성 높음)
///   1 = 경로 의존형 (집중도 높음)
///   2 = 탐색/분산형 (전부 낮음)
/// </summary>
public static class FCMAnalyzer
{
    // ─── 학습된 중심점 (오프라인 FCM 학습 결과) ───
    // 순서: [f1 긴급도, f2 집중도, f3 반복성, f4 오염도]
    private static readonly float[,] Centroids = new float[,]
    {
        { 0.7993f, 0.4005f, 0.7725f, 0.1395f },  // 콤보 러시형
        { 0.5034f, 0.8088f, 0.4789f, 0.1753f },  // 경로 의존형
        { 0.1681f, 0.2110f, 0.2148f, 0.1672f }   // 탐색/분산형
    };

    public const int ClusterCount = 3;
    public const int FeatureDim = 4;

    /// <summary>
    /// 특성 벡터로부터 3개 클러스터 소속도를 계산한다.
    /// </summary>
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
        0 => "콤보 러시형", 1 => "경로 의존형", 2 => "탐색/분산형", _ => "?"
    };
}
