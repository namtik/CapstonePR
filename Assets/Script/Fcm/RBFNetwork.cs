using System.Collections.Generic;
using UnityEngine;

// RBF 네트워크 + 온라인 학습(트리 모방 + Soft Update)
[System.Serializable]
public class RBFNetwork
{
    public const int InputDim = 5;    // 입력 차원
    public const int NumNeurons = 4;  // 뉴런(중심) 수
    public const int OutputDim = 3;   // 출력 행동 수

    // 뉴런 중심점(= FCM 중심점)
    [SerializeField]
    private float[] centers = new float[]
    {
        0.9006f, 0.6300f, 0.2852f, 0.4533f, 0.4917f,
        0.8046f, 0.4385f, 0.4241f, 0.8153f, 0.4407f,
        0.7454f, 0.4123f, 0.2149f, 0.4026f, 0.6215f,
        0.0986f, 0.0142f, 0.5773f, 0.0150f, 0.3609f,
    };

    [SerializeField] private float[] sigmas = new float[] { 0.4000f, 0.4000f, 0.4000f, 0.4000f }; // RBF 폭

    // 출력 가중치(오프라인 초기값 → Soft Update 갱신)
    [SerializeField]
    private float[] weights = new float[]
    {
        0.8789f, -0.9828f, 0.1040f,
        -0.4298f, 0.4924f, -0.0625f,
        0.0444f, 0.1719f, -0.2163f,
        -0.7558f, -0.1379f, 0.8939f,
    };

    [SerializeField] private float[] biases = new float[] { 0.5022f, 0.4383f, 0.0593f }; // 출력 바이어스

    [Header("온라인 학습")]
    [Tooltip("새 가중치 반영 비율 (0.1=보수적, 0.3=적극적)")]
    [SerializeField] private float learningRate = 0.2f;   // 학습률

    private const float L2_REGULARIZATION = 0.01f;        // L2 정규화 계수
    private const int MIN_SAMPLES_FOR_LEARNING = 5;       // 학습 최소 샘플 수

    // 순전파: 입력 → 행동 확률(softmax)
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

    // 확률에서 행동 선택(확정 argmax 또는 확률적)
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

    // 최소자승으로 이상 가중치 계산 후 Lerp로 점진 적용
    public void RetrainWeightsOnline(List<float[]> recentFeatures, List<int> recentActions,
                                     int maxMemorySize = 30)
    {
        int N = recentFeatures.Count;
        int K = NumNeurons;

        if (N < MIN_SAMPLES_FOR_LEARNING) return;

        float[,] Phi = new float[N, K + 1];
        for (int i = 0; i < N; i++)
        {
            float[] phi = CalcActivations(recentFeatures[i]);
            for (int k = 0; k < K; k++)
                Phi[i, k] = phi[k];
            Phi[i, K] = 1f;
        }

        float[,] Y = new float[N, OutputDim];
        for (int i = 0; i < N; i++)
        {
            int action = recentActions[i];
            if (action >= 0 && action < OutputDim)
                Y[i, action] = 1f;
        }

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

        float[,] B = new float[K + 1, OutputDim];
        for (int i = 0; i < K + 1; i++)
            for (int j = 0; j < OutputDim; j++)
            {
                float sum = 0f;
                for (int n = 0; n < N; n++)
                    sum += Phi[n, i] * Y[n, j];
                B[i, j] = sum;
            }

        float[,] W = SolveLinearSystem(A, B);

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

    // 가우스-조르단 소거법으로 선형계 A x = B 풀이
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

    // 각 RBF 뉴런 활성값(가우시안) 계산
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

    // softmax 정규화
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

    // 가중치·바이어스·시그마 외부 설정
    public void SetWeights(float[] w, float[] b, float[] s)
    {
        if (w != null && w.Length == NumNeurons * OutputDim) System.Array.Copy(w, weights, weights.Length);
        if (b != null && b.Length == OutputDim) System.Array.Copy(b, biases, biases.Length);
        if (s != null && s.Length == NumNeurons) System.Array.Copy(s, sigmas, sigmas.Length);
    }

    // 중심점 외부 설정
    public void SetCenters(float[] c)
    {
        if (c != null && c.Length == NumNeurons * InputDim) System.Array.Copy(c, centers, centers.Length);
    }

    // 가중치 복사본 반환
    public float[] GetWeights() => (float[])weights.Clone();
    // 바이어스 복사본 반환
    public float[] GetBiases() => (float[])biases.Clone();
}
