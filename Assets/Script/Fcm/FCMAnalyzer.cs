using UnityEngine;

/// <summary>
/// FCM 플레이어 유형 분석기 (5차원, 4유형)
/// 입력: [f1 긴급도, f3 반복성, f5 보충진행, f7 위협밀도, f8 슬롯균형]
/// </summary>
public static class FCMAnalyzer
{
    // [임시 중심점] collectData로 데이터 수집 후 fcm_retrain.py로 갱신할 것
    // 순서: [f1, f3, f5, f7, f8]
    private static readonly float[,] Centroids = new float[,]
    {
        { 0.76f, 0.76f, 0.40f, 0.77f, 0.80f },  // 콤보 러시형
        { 0.67f, 0.22f, 0.41f, 0.48f, 0.90f },  // 경로 의존형
        { 0.18f, 0.14f, 0.21f, 0.08f, 0.82f },  // 탐색/분산형
        { 0.12f, 0.08f, 0.79f, 0.06f, 0.19f },  // 버스트형
    };

    public const int ClusterCount = 4;
    public const int FeatureDim = 5;

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
        3 => "버스트형",
        _ => "?"
    };
}
