using UnityEngine;

// FCM 플레이어 유형 분석기 (5차원 특성 → 4유형 소속도)
public static class FCMAnalyzer
{
    // 유형별 중심점 [f1,f3,f5,f7,f8]
    private static readonly float[,] Centroids = new float[,]
    {
        { 0.9006f, 0.6300f, 0.2852f, 0.4533f, 0.4917f },
        { 0.8046f, 0.4385f, 0.4241f, 0.8153f, 0.4407f },
        { 0.7454f, 0.4123f, 0.2149f, 0.4026f, 0.6215f },
        { 0.0986f, 0.0142f, 0.5773f, 0.0150f, 0.3609f }
    };

    public const int ClusterCount = 4;   // 유형 수
    public const int FeatureDim = 5;     // 특성 차원

    // 특성 벡터로 각 유형 소속도 계산
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

    // 소속도가 가장 높은 유형 인덱스 반환
    public static int GetDominantType(float[] membership)
    {
        int best = 0;
        for (int i = 1; i < membership.Length; i++)
            if (membership[i] > membership[best]) best = i;
        return best;
    }

    // 유형 인덱스를 이름 문자열로 변환
    public static string GetTypeName(int idx) => idx switch
    {
        0 => "콤보 러시형",
        1 => "경로 의존형",
        2 => "탐색/분산형",
        3 => "버스트형",
        _ => "?"
    };
}
