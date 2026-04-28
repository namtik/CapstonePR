using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// RBFN + 온라인 학습 (트리 모방 + Soft Update)
/// 
/// 온라인 학습:
///   게이지 5: 트리 정답을 슬라이딩 윈도우에 저장
///   게이지 10: 최소자승법으로 "이상적 가중치" 계산 → Lerp로 점진 적용
/// 
/// Soft Update:
///   weights = Lerp(현재, 새값, adjustedRate)
///   adjustedRate = learningRate * (버퍼크기 / maxMemory)
///   → 데이터 적으면 보수적, 많으면 적극적
///   → 초기 가중치가 자연스럽게 유지되다가 점진적으로 적응
/// </summary>
[System.Serializable]
public class RBFNetwork
{
    public const int InputDim = 5;
    public const int NumNeurons = 4;
    public const int OutputDim = 3;

    // ─── 뉴런 중심 (= FCM 중심점) ───
    [SerializeField]
    private float[] centers = new float[]
    {
        0.9006f, 0.6300f, 0.2852f, 0.4533f, 0.4917f,  // 러시형
        0.8046f, 0.4385f, 0.4241f, 0.8153f, 0.4407f,  // 의존형
        0.7454f, 0.4123f, 0.2149f, 0.4026f, 0.6215f,  // 탐색형
        0.0986f, 0.0142f, 0.5773f, 0.0150f, 0.3609f,  // 버스트형
    };

    [SerializeField] private float[] sigmas = new float[] { 0.4000f, 0.4000f, 0.4000f, 0.4000f };

    // ─── 가중치 (오프라인 초기값 → Soft Update 갱신) ───
    [SerializeField]
    private float[] weights = new float[]
    {
         1.7302f, -0.6265f, -1.1037f,  // 러시형
        -0.5709f, 0.3101f, 0.2609f,  // 의존형
        -0.8731f, -0.0484f, 0.9215f,  // 탐색형
        -0.5787f, 0.5496f, 0.0294f,  // 버스트형
    };

    [SerializeField] private float[] biases = new float[] { 0.3941f, 0.3545f, 0.2512f };

    // ─── 온라인 학습 설정 ───
    [Header("온라인 학습")]
    [Tooltip("새 가중치 반영 비율 (0.1=보수적, 0.3=적극적)")]
    [SerializeField] private float learningRate = 0.2f;

    private const float L2_REGULARIZATION = 0.01f;
    private const int MIN_SAMPLES_FOR_LEARNING = 5;

    // ═══════════════════════════════════
    // 순전파
    // ═══════════════════════════════════

    public float[] Forward(float[] input)
    {
        float[] phi = CalcActivations(input);
        float[] raw = new float[OutputDim];
        for (int j = 0; j < OutputDim; j++)
        {
            float sum = biases[j];
            for (int k = 0; k < NumNeurons; k++)
                sum += weights[k * OutputDim + j] * phi[k];
            raw[j] = sum;
        }
        return Softmax(raw);
    }

    public int SelectAction(float[] probs, bool deterministic = false)
    {
        if (deterministic)
        {
            int best = 0;
            for (int i = 1; i < OutputDim; i++)
                if (probs[i] > probs[best]) best = i;
            return best;
        }
        float r = Random.value;
        float cum = 0f;
        for (int i = 0; i < OutputDim; i++)
        {
            cum += probs[i];
            if (r <= cum) return i;
        }
        return OutputDim - 1;
    }

    // ═══════════════════════════════════
    // 온라인 학습 (최소자승법 + Soft Update)
    // ═══════════════════════════════════

    /// <summary>
    /// 1. 최소자승법으로 "이상적 가중치" 계산
    /// 2. Lerp로 현재 가중치에서 이상적 가중치 방향으로 점진 이동
    /// 
    /// adjustedRate = learningRate * (버퍼크기 / maxMemory)
    /// → 버퍼 5개: 0.2 * 5/30 = 0.033 (거의 안 움직임)
    /// → 버퍼 30개: 0.2 * 30/30 = 0.2 (정상 학습)
    /// </summary>
    public void RetrainWeightsOnline(List<float[]> recentFeatures, List<int> recentActions,
                                     int maxMemorySize = 30)
    {
        int N = recentFeatures.Count;
        int K = NumNeurons;

        if (N < MIN_SAMPLES_FOR_LEARNING) return;

        // 1. Phi 행렬 [N x (K+1)]
        float[,] Phi = new float[N, K + 1];
        for (int i = 0; i < N; i++)
        {
            float[] phi = CalcActivations(recentFeatures[i]);
            for (int k = 0; k < K; k++)
                Phi[i, k] = phi[k];
            Phi[i, K] = 1f;
        }

        // 2. Y 원-핫 정답
        float[,] Y = new float[N, OutputDim];
        for (int i = 0; i < N; i++)
        {
            int action = recentActions[i];
            if (action >= 0 && action < OutputDim)
                Y[i, action] = 1f;
        }

        // 3. A = Phi^T * Phi + λI
        float[,] A = new float[K + 1, K + 1];
        for (int i = 0; i < K + 1; i++)
            for (int j = 0; j < K + 1; j++)
            {
                float sum = 0f;
                for (int n = 0; n < N; n++)
                    sum += Phi[n, i] * Phi[n, j];
                if (i == j) sum += L2_REGULARIZATION;
                A[i, j] = sum;
            }

        // 4. B = Phi^T * Y
        float[,] B = new float[K + 1, OutputDim];
        for (int i = 0; i < K + 1; i++)
            for (int j = 0; j < OutputDim; j++)
            {
                float sum = 0f;
                for (int n = 0; n < N; n++)
                    sum += Phi[n, i] * Y[n, j];
                B[i, j] = sum;
            }

        // 5. W_ideal = A^(-1) * B
        float[,] W = SolveLinearSystem(A, B);

        // 6. Soft Update: 현재 → 이상적 방향으로 점진 이동
        if (W != null)
        {
            float adjustedRate = learningRate * Mathf.Clamp01((float)N / maxMemorySize);

            for (int k = 0; k < K; k++)
                for (int j = 0; j < OutputDim; j++)
                {
                    int idx = k * OutputDim + j;
                    weights[idx] = Mathf.Lerp(weights[idx], W[k, j], adjustedRate);
                }

            for (int j = 0; j < OutputDim; j++)
                biases[j] = Mathf.Lerp(biases[j], W[K, j], adjustedRate);
        }
    }

    // ═══════════════════════════════════
    // 가우스-조르단 소거법
    // ═══════════════════════════════════

    private float[,] SolveLinearSystem(float[,] A, float[,] B)
    {
        int n = A.GetLength(0);
        int m = B.GetLength(1);

        float[,] aug = new float[n, n + m];
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++) aug[i, j] = A[i, j];
            for (int j = 0; j < m; j++) aug[i, n + j] = B[i, j];
        }

        for (int i = 0; i < n; i++)
        {
            float maxEl = Mathf.Abs(aug[i, i]);
            int maxRow = i;
            for (int k = i + 1; k < n; k++)
            {
                if (Mathf.Abs(aug[k, i]) > maxEl)
                {
                    maxEl = Mathf.Abs(aug[k, i]);
                    maxRow = k;
                }
            }

            if (maxEl < 1e-6f) return null;

            if (maxRow != i)
                for (int k = i; k < n + m; k++)
                {
                    float tmp = aug[maxRow, k];
                    aug[maxRow, k] = aug[i, k];
                    aug[i, k] = tmp;
                }

            float pivot = aug[i, i];
            for (int k = i; k < n + m; k++)
                aug[i, k] /= pivot;

            for (int k = 0; k < n; k++)
                if (k != i)
                {
                    float factor = aug[k, i];
                    for (int j = i; j < n + m; j++)
                        aug[k, j] -= factor * aug[i, j];
                }
        }

        float[,] X = new float[n, m];
        for (int i = 0; i < n; i++)
            for (int j = 0; j < m; j++)
                X[i, j] = aug[i, n + j];

        return X;
    }

    // ═══════════════════════════════════
    // RBF 뉴런
    // ═══════════════════════════════════

    public float[] CalcActivations(float[] input)
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

    // ═══════════════════════════════════
    // 접근자
    // ═══════════════════════════════════

    public void SetWeights(float[] w, float[] b, float[] s)
    {
        if (w != null && w.Length == NumNeurons * OutputDim) System.Array.Copy(w, weights, weights.Length);
        if (b != null && b.Length == OutputDim) System.Array.Copy(b, biases, biases.Length);
        if (s != null && s.Length == NumNeurons) System.Array.Copy(s, sigmas, sigmas.Length);
    }

    public void SetCenters(float[] c)
    {
        if (c != null && c.Length == NumNeurons * InputDim) System.Array.Copy(c, centers, centers.Length);
    }

    public float[] GetWeights() => (float[])weights.Clone();
    public float[] GetBiases() => (float[])biases.Clone();
}