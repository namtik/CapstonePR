using UnityEngine;

/// <summary>
/// RBFN
/// 구조:
///   입력층: 5차원 [f1, f3, f5, f7, f8]
///   은닉층: RBF 뉴런 4개 (중심 = FCM 중심점)
///   출력층: 3개 [셔플 확률, 저주 확률, 무속성 확률]
/// 
/// 뉴런 출력: phi_k = exp(-||x - c_k||^2 / (2 * sigma_k^2))
/// 최종 출력: y_j = sum_k(w_kj * phi_k) + b_j → softmax → 확률
/// </summary>
[System.Serializable]
public class RBFNetwork
{
    public const int InputDim = 5;    // f1, f3, f5, f7, f8
    public const int NumNeurons = 4;  // FCM 클러스터 수
    public const int OutputDim = 3;   // 셔플, 저주, 무속성

    // ─── RBF 뉴런 중심 (= FCM 중심점) ───
    [SerializeField] private float[] centers = new float[]
    {
        // 러시형:  f1     f3     f5     f7     f8
                   0.76f, 0.76f, 0.40f, 0.77f, 0.80f,
        // 의존형:
                   0.67f, 0.22f, 0.41f, 0.48f, 0.90f,
        // 탐색형:
                   0.18f, 0.14f, 0.21f, 0.08f, 0.82f,
        // 버스트형:
                   0.12f, 0.08f, 0.79f, 0.06f, 0.19f,
    };

    // ─── 스프레드 (각 뉴런의 가우시안 폭) ───
    [SerializeField] private float[] sigmas = new float[]
    {
        0.40f,  // 러시형
        0.40f,  // 의존형
        0.40f,  // 탐색형
        0.40f,  // 버스트형
    };

    // ─── 가중치 행렬 [뉴런 x 출력] = 4x3 = 12개 ───
    // 초기값: 의사결정 트리 모방 학습 결과
    // 순서: [러시→셔플, 러시→저주, 러시→무속성,
    //        의존→셔플, 의존→저주, 의존→무속성,
    //        탐색→셔플, 탐색→저주, 탐색→무속성,
    //        버스트→셔플, 버스트→저주, 버스트→무속성]
    [SerializeField] private float[] weights = new float[]
    {
        // 초기값: 트리 규칙 근사
        // 러시형 → 셔플 우세
         0.80f, 0.15f, 0.05f,
        // 의존형 → 저주 우세
         0.10f, 0.70f, 0.20f,
        // 탐색형 → 무속성 우세
         0.05f, 0.15f, 0.80f,
        // 버스트형 → 무속성 우세 (보충 전 삽입)
         0.10f, 0.20f, 0.70f,
    };

    // ─── 바이어스 [출력 3개] ───
    [SerializeField] private float[] biases = new float[] { 0f, 0f, 0f };

    // ═══════════════════════════════════════
    // 순전파 (Forward Pass)
    // ═══════════════════════════════════════

    /// <summary>
    /// 5차원 입력 → 행동 확률 3개 반환
    /// [0] = 셔플 확률, [1] = 저주 확률, [2] = 무속성 확률
    /// </summary>
    public float[] Forward(float[] input)
    {
        // 1. RBF 뉴런 활성도 계산
        float[] phi = CalcActivations(input);

        // 2. 선형 결합 + 바이어스
        float[] raw = new float[OutputDim];
        for (int j = 0; j < OutputDim; j++)
        {
            float sum = biases[j];
            for (int k = 0; k < NumNeurons; k++)
                sum += weights[k * OutputDim + j] * phi[k];
            raw[j] = sum;
        }

        // 3. Softmax → 확률
        return Softmax(raw);
    }

    /// <summary>
    /// 행동 확률에서 행동을 선택한다.
    /// deterministic=true: 가장 높은 확률의 행동 선택 (테스트용)
    /// deterministic=false: 확률적으로 선택 (실전용, 예측 불가능성)
    /// </summary>
    public int SelectAction(float[] probabilities, bool deterministic = false)
    {
        if (deterministic)
        {
            int best = 0;
            for (int i = 1; i < OutputDim; i++)
                if (probabilities[i] > probabilities[best]) best = i;
            return best;
        }

        // 확률적 선택 (룰렛 휠)
        float r = Random.value;
        float cumulative = 0f;
        for (int i = 0; i < OutputDim; i++)
        {
            cumulative += probabilities[i];
            if (r <= cumulative) return i;
        }
        return OutputDim - 1;
    }

    // ═══════════════════════════════════════
    // RBF 뉴런
    // ═══════════════════════════════════════

    /// <summary>
    /// 각 뉴런의 가우시안 활성도 계산
    /// phi_k = exp(-||x - c_k||^2 / (2 * sigma_k^2))
    /// </summary>
    float[] CalcActivations(float[] input)
    {
        float[] phi = new float[NumNeurons];

        for (int k = 0; k < NumNeurons; k++)
        {
            float distSq = 0f;
            for (int d = 0; d < InputDim; d++)
            {
                float diff = input[d] - centers[k * InputDim + d];
                distSq += diff * diff;
            }
            phi[k] = Mathf.Exp(-distSq / (2f * sigmas[k] * sigmas[k]));
        }

        return phi;
    }

    /// <summary>안정적인 Softmax (오버플로우 방지)</summary>
    float[] Softmax(float[] raw)
    {
        float max = raw[0];
        for (int i = 1; i < raw.Length; i++)
            if (raw[i] > max) max = raw[i];

        float[] result = new float[raw.Length];
        float sum = 0f;
        for (int i = 0; i < raw.Length; i++)
        {
            result[i] = Mathf.Exp(raw[i] - max);
            sum += result[i];
        }
        for (int i = 0; i < raw.Length; i++)
            result[i] /= sum;

        return result;
    }

    // ═══════════════════════════════════════
    // 파라미터 접근 (학습 결과 적용용)
    // ═══════════════════════════════════════

    /// <summary>Python 학습 결과를 적용</summary>
    public void SetWeights(float[] newWeights, float[] newBiases, float[] newSigmas)
    {
        if (newWeights != null && newWeights.Length == NumNeurons * OutputDim)
            System.Array.Copy(newWeights, weights, weights.Length);
        if (newBiases != null && newBiases.Length == OutputDim)
            System.Array.Copy(newBiases, biases, biases.Length);
        if (newSigmas != null && newSigmas.Length == NumNeurons)
            System.Array.Copy(newSigmas, sigmas, sigmas.Length);
    }

    /// <summary>FCM 중심점 갱신 시 함께 갱신</summary>
    public void SetCenters(float[] newCenters)
    {
        if (newCenters != null && newCenters.Length == NumNeurons * InputDim)
            System.Array.Copy(newCenters, centers, centers.Length);
    }

    /// <summary>디버그: 뉴런 활성도 반환</summary>
    public float[] GetActivations(float[] input) => CalcActivations(input);

    /// <summary>디버그: 현재 가중치 반환</summary>
    public float[] GetWeights() => (float[])weights.Clone();
    public float[] GetBiases() => (float[])biases.Clone();
    public float[] GetSigmas() => (float[])sigmas.Clone();
}
