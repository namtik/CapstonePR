using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class SeaStackBedrockImpact : MonoBehaviour
{
    private enum Motion { Bedrock, TidalWave, AbyssalPull, AbyssalBreak, HullFist }

    private const float HullFistDuration = 2.86f;
    private const float HullFistHit = 1.62f;
    private const float HullFistApproachDistance = 15.55f;

    [SerializeField] private Transform stageCamera;
    [SerializeField] private Transform battleBoat;
    [SerializeField] private ParticleSystem spray;
    [SerializeField] private Transform abyssalColumn;

    private readonly System.Random random = new System.Random(327);
    private Pose cameraRest;
    private Pose boatRest;
    private float impactTime;
    private float nextBindingCheck;
    private bool recoiling;
    private Motion motion;

    public void BindStage(Transform cameraTransform, Transform boatTransform, ParticleSystem impactSpray)
    {
        RestorePose();
        stageCamera = cameraTransform;
        battleBoat = boatTransform;
        spray = impactSpray;
    }

    private void Start()
    {
        ResolveStageBindings();
    }

    private void ResolveStageBindings()
    {
        nextBindingCheck = Time.unscaledTime + 0.5f;
        Scene bossScene = gameObject.scene;
        if (!battleBoat)
        {
            Scene preferredScene = spray ? spray.gameObject.scene : bossScene;
            battleBoat = FindUnique<Transform>(preferredScene, "BattleBoat");

            if (!battleBoat)
            {
                Transform candidate = null;
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    Scene scene = SceneManager.GetSceneAt(i);
                    if (scene == preferredScene)
                        continue;

                    var boat = FindUnique<Transform>(scene, "BattleBoat");
                    if (!boat)
                        continue;

                    if (candidate)
                        return;

                    candidate = boat;
                }
                battleBoat = candidate;
            }
        }

        if (!battleBoat)
            return;

        Scene stageScene = battleBoat.gameObject.scene;
        if (!spray)
            spray = FindUnique<ParticleSystem>(stageScene, "BedrockImpactSpray");

        if (!stageCamera)
        {
            stageCamera = FindUnique<Transform>(stageScene, "StageCamera");
            if (!stageCamera && stageScene != bossScene)
                stageCamera = FindUnique<Transform>(bossScene, "StageCamera");

            if (!stageCamera)
                stageCamera = Camera.main?.transform;
        }
    }

    private static T FindUnique<T>(Scene scene, string objectName) where T : Component
    {
        if (!scene.isLoaded)
            return null;

        T match = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name != objectName || !child.TryGetComponent(out T component))
                    continue;

                if (match)
                    return null;

                match = component;
            }
        }
        return match;
    }

    // Called by DeepSeaGiant_Attack_BedrockThrow on the first contact frame.
    public void PlayBedrockImpact()
    {
        BeginMotion(Motion.Bedrock);
    }

    public void PlayHullFistDraw()
    {
        BeginMotion(Motion.HullFist);
    }

    // Called as the TidalWave crest breaks at the bow.
    public void PlayTidalWaveImpact()
    {
        BeginMotion(Motion.TidalWave);
    }

    public void PlayAbyssalEyePull()
    {
        BeginMotion(Motion.AbyssalPull);
    }

    public void PlayAbyssalEyeImpact()
    {
        BeginMotion(Motion.AbyssalBreak);
    }

    public void PlayAbyssalEyeEruption()
    {
        if (!isActiveAndEnabled)
            return;

        if (!spray)
            ResolveStageBindings();

        if (!spray || !abyssalColumn)
            return;

        Vector3 center = abyssalColumn.position + Vector3.up * (abyssalColumn.lossyScale.y * 0.52f);
        spray.Play();
        for (int index = 0; index < 64; index++)
        {
            float angle = Range(0f, Mathf.PI * 2f);
            var radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            var droplet = new ParticleSystem.EmitParams
            {
                position = center + radial * Range(0.6f, 2.6f) + Vector3.up * Range(-2.1f, 2.2f),
                velocity = radial * Range(2.2f, 6.7f) + Vector3.up * Range(0.1f, 4.1f),
                startSize = Range(0.09f, 0.20f),
                startLifetime = Range(0.43f, 0.59f),
                startColor = new Color(0.75f, 0.91f, 0.96f, Range(0.48f, 0.93f))
            };
            spray.Emit(droplet, 1);
        }
    }

    private void BeginMotion(Motion next)
    {
        if (!isActiveAndEnabled)
            return;

        RestorePose();
        if (!stageCamera || !battleBoat || !spray)
            ResolveStageBindings();

        motion = next;
        cameraRest = LocalPose(stageCamera);
        boatRest = LocalPose(battleBoat);
        impactTime = Time.time;
        recoiling = true;
        ApplyMotion(0f);

        if (motion != Motion.AbyssalPull && motion != Motion.HullFist && spray && stageCamera)
            EmitSpray();
    }

    private void LateUpdate()
    {
        if (!recoiling)
        {
            if ((!stageCamera || !battleBoat || !spray) && Time.unscaledTime >= nextBindingCheck)
                ResolveStageBindings();

            return;
        }

        float elapsed = Time.time - impactTime;
        float duration = motion switch
        {
            Motion.AbyssalPull => 2.70f,
            Motion.AbyssalBreak => 1.34f,
            Motion.TidalWave => 1.07f,
            Motion.HullFist => HullFistDuration,
            _ => 0.76f
        };
        if (elapsed >= duration)
            RestorePose();
        else
            ApplyMotion(elapsed);
    }

    private void OnDisable()
    {
        RestorePose();
        if (spray)
            spray.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void ApplyMotion(float elapsed)
    {
        if (motion == Motion.HullFist)
        {
            Pose hull = SampleHullFistBoat(elapsed);
            if (battleBoat)
            {
                battleBoat.SetLocalPositionAndRotation(boatRest.position + boatRest.rotation * hull.position,
                    boatRest.rotation * hull.rotation);
            }
            if (stageCamera)
            {
                Pose view = SampleHullFistCamera(elapsed, hull);
                stageCamera.SetLocalPositionAndRotation(cameraRest.position + boatRest.rotation * view.position,
                    cameraRest.rotation * view.rotation);
            }
            return;
        }

        if (motion == Motion.AbyssalPull)
        {
            ApplyPull(elapsed);
            return;
        }

        bool tidalWave = motion != Motion.Bedrock;
        float weight = motion == Motion.AbyssalBreak ? 1.28f : 1f;
        float time = elapsed / weight;
        float kick = Mathf.Exp(-time * (tidalWave ? 10f : 20f));
        float ringing = tidalWave
            ? Mathf.Sin(time * 19f) * Mathf.Exp(-time * 5.8f)
            : Mathf.Sin(time * 50f) * Mathf.Exp(-time * 11f);
        if (stageCamera)
        {
            var offset = new Vector3(0.064f * ringing, 0.04f * kick + 0.027f * ringing, -0.115f * kick) * weight;
            var rotation = Quaternion.Euler(new Vector3(-1.6f * kick + 1.1f * ringing, 0.44f * ringing, -0.64f * ringing) * weight);
            stageCamera.SetLocalPositionAndRotation(cameraRest.position + cameraRest.rotation * offset, cameraRest.rotation * rotation);
        }

        if (battleBoat)
        {
            float damping = Mathf.Exp(-time * (tidalWave ? 3.4f : 5.4f));
            float rocking = Mathf.Sin(time * (tidalWave ? 9.2f : 12.5f)) * damping * weight;
            float heave = Mathf.Sin(time * (tidalWave ? 6.4f : 9.4f)) * damping * weight;
            var rotation = Quaternion.Euler(-(tidalWave ? 8.5f : 6.8f) * rocking, 0f, (tidalWave ? 2.4f : 1.5f) * rocking);
            battleBoat.SetLocalPositionAndRotation(
                boatRest.position + Vector3.up * ((tidalWave ? 0.21f : 0.13f) * heave), boatRest.rotation * rotation);
        }
    }

    public static Pose SampleHullFistBoat(float elapsed)
    {
        float draw = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.26f, 1.48f, elapsed));
        float impact = Mathf.Max(0f, elapsed - HullFistHit);
        float retreat = Mathf.InverseLerp(0f, 1.18f, impact);
        float settle = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1.0f, 1.18f, impact));
        float crash = Mathf.Sin(12f * impact) * Mathf.Exp(-4.7f * impact) * settle;
        float tow = Mathf.Sin(draw * Mathf.PI);
        float tug = Mathf.Sin(Mathf.PI * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.10f, 0.76f, elapsed)));
        float travel = HullFistApproachDistance * draw * (1f - retreat) * (1f - retreat);
        return new Pose(new Vector3(0.34f * tug + 0.22f * crash, -0.22f * tug - 0.11f * tow - 0.44f * crash, travel),
            Quaternion.Euler(6f * tug - 2.3f * tow + 14f * crash, -2.2f * tug + 0.9f * crash, -3.7f * tug - 3.7f * crash));
    }

    public static Pose SampleHullFistCamera(float elapsed, Pose hull)
    {
        float impact = Mathf.Max(0f, elapsed - HullFistHit);
        float shake = Mathf.Sin(impact * 42f) * Mathf.Exp(-9.5f * impact);
        float closeness = hull.position.z / HullFistApproachDistance;
        float lookUp = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1.39f, HullFistHit, elapsed));
        float followThrough = Mathf.Sin(Mathf.PI * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.18f, 1.48f, elapsed)));
        float seeCurrent = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.23f, elapsed))
            * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.68f, 1.34f, elapsed)));
        Vector3 shift = hull.position + new Vector3(0.21f * closeness + 0.05f * shake,
            -0.12f * closeness + 0.055f * shake + 0.95f * seeCurrent,
            -1.37f * closeness - 1.35f * followThrough - 0.025f * shake);
        return new Pose(shift, Quaternion.Euler((0.9f - 6.1f * lookUp) * closeness - 1.9f * shake + 6f * seeCurrent,
            0.8f * shake, -1.2f * shake));
    }

    private void ApplyPull(float elapsed)
    {
        float load = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / 1.55f));
        float release = Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(2.05f, 2.70f, elapsed));
        float pull = load * release;
        if (battleBoat)
        {
            var shift = new Vector3(-0.12f, -0.11f, 0.33f) * pull;
            var tilt = Quaternion.Euler(2.9f * pull, -1.7f * pull, 4.5f * pull);
            battleBoat.SetLocalPositionAndRotation(boatRest.position + shift, boatRest.rotation * tilt);
        }

        if (stageCamera)
        {
            var shift = new Vector3(-0.07f, -0.06f, 0.19f) * pull;
            stageCamera.SetLocalPositionAndRotation(cameraRest.position + shift,
                cameraRest.rotation * Quaternion.Euler(0.6f * pull, -0.55f * pull, 1.8f * pull));
        }
    }

    private void EmitSpray()
    {
        Vector3 origin = spray.transform.position;
        Vector3 towardDeck = Vector3.ProjectOnPlane(stageCamera.position - origin, Vector3.up).normalized;
        Vector3 acrossDeck = Vector3.Cross(Vector3.up, towardDeck);
        spray.Play();

        // Wider, heavier splash stays near the bow. A smaller fast group reaches the deck.
        int heavy = motion == Motion.AbyssalBreak ? 76 : motion == Motion.TidalWave ? 66 : 48;
        int distant = motion == Motion.AbyssalBreak ? 44 : motion == Motion.TidalWave ? 42 : 30;
        EmitDroplets(origin, towardDeck, acrossDeck, heavy, false);
        EmitDroplets(origin + Vector3.up * 0.5f, towardDeck, acrossDeck, distant, true);
    }

    private void EmitDroplets(Vector3 origin, Vector3 towardDeck, Vector3 acrossDeck, int count, bool longReach)
    {
        for (int index = 0; index < count; index++)
        {
            float lateral = Range(-1f, 1f);
            var velocity = towardDeck * (longReach ? Range(6.2f, 10.2f) : Range(0.7f, 3.5f))
                + acrossDeck * lateral * (longReach ? Range(1.1f, 3.0f) : Range(3.4f, 7.2f))
                + Vector3.up * (longReach ? Range(4.1f, 6.3f) : Range(3.0f, 7.5f));
            var droplet = new ParticleSystem.EmitParams
            {
                position = origin + acrossDeck * lateral * Range(0f, 0.36f),
                velocity = velocity,
                startSize = longReach ? Range(0.035f, 0.085f) : Range(0.065f, 0.19f),
                startLifetime = longReach ? Range(0.40f, 0.64f) : Range(0.35f, 0.68f),
                startColor = Color.Lerp(new Color(0.52f, 0.72f, 0.79f, 0.63f), new Color(0.9f, 0.97f, 1f, 0.95f), Range(0f, 1f))
            };
            spray.Emit(droplet, 1);
        }
    }

    private float Range(float min, float max)
    {
        return Mathf.Lerp(min, max, (float)random.NextDouble());
    }

    private void RestorePose()
    {
        if (!recoiling)
            return;

        if (stageCamera)
            stageCamera.SetLocalPositionAndRotation(cameraRest.position, cameraRest.rotation);
        if (battleBoat)
            battleBoat.SetLocalPositionAndRotation(boatRest.position, boatRest.rotation);
        recoiling = false;
    }

    private static Pose LocalPose(Transform target)
    {
        return target ? new Pose(target.localPosition, target.localRotation) : Pose.identity;
    }
}
