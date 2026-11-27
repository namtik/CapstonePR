using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class SeaStackBedrockImpact : MonoBehaviour
{
    private enum Motion { Bedrock, TidalWave, AbyssalPull, AbyssalBreak, HullFist }
    private enum CameraShot { None, AbyssalDecree, DreadGaze }

    private const float HullFistDuration = 2.86f;
    private const float HullFistHit = 1.62f;
    private const float HullFistApproachDistance = 15.55f;
    private const string AbyssalDecreeClip = "DeepSeaGiant_Attack_AbyssalDecree";
    private const string DreadGazeClip = "DeepSeaGiant_Curse_DreadGaze";
    private const float NoDecreeCameraRequest = -1f;

    private static readonly Dictionary<Transform, SeaStackBedrockImpact> MotionOwners = new();

    [SerializeField] private Transform stageCamera;
    [SerializeField] private Transform battleBoat;
    [SerializeField] private ParticleSystem spray;
    [SerializeField] private Transform abyssalColumn;
    [SerializeField, HideInInspector] private float decreeCameraRequest = NoDecreeCameraRequest;

    private readonly System.Random random = new System.Random(327);
    private readonly List<AnimatorClipInfo> cameraClips = new();
    private Animator bossAnimator;
    private Camera decreeCamera;
    private Renderer decreeCameraWash;
    private Renderer dreadGazeVeil;
    private bool cinematicShown;
    private SeaStackStageCameraLock stageCameraRecovery;
    private SeaStackStageCameraLock motionCameraRecovery;
    private int cameraMotionToken;
    private int cameraMotionLayer = -1;
    private int cameraMotionState;
    private float cameraMotionProgress;
    private Transform motionBoat;
    private Pose boatRest;
    private float impactTime;
    private float nextBindingCheck;
    private bool recoiling;
    private Motion motion;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetMotionOwners()
    {
        MotionOwners.Clear();
    }

    public void BindStage(Transform cameraTransform, Transform boatTransform, ParticleSystem impactSpray)
    {
        SetCameraShot(CameraShot.None);
        RestorePose();
        stageCamera = cameraTransform;
        battleBoat = boatTransform;
        spray = impactSpray;
        LockStageCamera();
    }

    private void Awake()
    {
        bossAnimator = GetComponent<Animator>();
        decreeCamera = transform.Find("DeepSeaGiant_AttackFX/AbyssalDecreeCamera")?.GetComponent<Camera>();
        if (decreeCamera)
        {
            decreeCameraWash = decreeCamera.transform.Find("AbyssalDecreeCameraWash")?.GetComponent<Renderer>();
            dreadGazeVeil = decreeCamera.transform.Find("DreadGazeVeil")?.GetComponent<Renderer>();
            SetCameraShot(CameraShot.None);
        }
    }

    private void Start()
    {
        ResolveStageBindings();
    }

    private void LockStageCamera()
    {
        stageCameraRecovery = stageCamera
            ? SeaStackStageCameraLock.Attach(stageCamera, battleBoat ? battleBoat.gameObject.scene : stageCamera.gameObject.scene)
            : null;
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
        {
            LockStageCamera();
            return;
        }

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

        LockStageCamera();
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

        // A newly spawned boss may share the boat with an attack still in progress.
        RestorePreviousOwner(battleBoat);
        motionBoat = battleBoat;
        boatRest = LocalPose(motionBoat);
        if (motionBoat)
            MotionOwners[motionBoat] = this;

        motion = next;
        impactTime = Time.time;
        recoiling = true;
        motionCameraRecovery = stageCameraRecovery;
        if (motionCameraRecovery)
        {
            cameraMotionToken = motionCameraRecovery.BeginAttack(MotionDuration(motion));
            TrackCameraMotionState();
        }
        ApplyMotion(0f);

        if (motion != Motion.AbyssalPull && motion != Motion.HullFist && spray && stageCamera)
            EmitSpray();
    }

    private void LateUpdate()
    {
        if (decreeCamera)
            UpdateCameraShot();

        if (!recoiling)
        {
            if ((!stageCamera || !battleBoat || !spray) && Time.unscaledTime >= nextBindingCheck)
                ResolveStageBindings();

            return;
        }

        if (motionCameraRecovery && cameraMotionLayer >= 0 && HasCameraMotionStateEnded())
            EndCameraMotion();

        float elapsed = Time.time - impactTime;
        if (elapsed >= MotionDuration(motion))
            RestorePose();
        else
            ApplyMotion(elapsed);
    }

    private static float MotionDuration(Motion motion) => motion switch
    {
        Motion.AbyssalPull => 2.70f,
        Motion.AbyssalBreak => 1.34f,
        Motion.TidalWave => 1.07f,
        Motion.HullFist => HullFistDuration,
        _ => 0.76f
    };

    private string CameraMotionClip => motion switch
    {
        Motion.HullFist => "DeepSeaGiant_Attack_HullFist",
        Motion.TidalWave => "DeepSeaGiant_Attack_TidalWave",
        Motion.AbyssalPull or Motion.AbyssalBreak => "DeepSeaGiant_Attack_AbyssalEye",
        _ => "DeepSeaGiant_Attack_BedrockThrow"
    };

    private void TrackCameraMotionState()
    {
        cameraMotionLayer = -1;
        if (!bossAnimator || !bossAnimator.isActiveAndEnabled || !bossAnimator.isInitialized)
            return;

        for (int layer = 0; layer < bossAnimator.layerCount; layer++)
        {
            bool incoming = bossAnimator.IsInTransition(layer);
            if (!TryGetClipState(layer, incoming, CameraMotionClip, out var state) &&
                (!incoming || !TryGetClipState(layer, false, CameraMotionClip, out state)))
                continue;

            cameraMotionLayer = layer;
            cameraMotionState = state.fullPathHash;
            cameraMotionProgress = state.normalizedTime;
            return;
        }
    }

    private bool HasCameraMotionStateEnded()
    {
        if (!bossAnimator || !bossAnimator.isActiveAndEnabled || !bossAnimator.isInitialized ||
            cameraMotionLayer >= bossAnimator.layerCount)
            return true;

        bool incoming = bossAnimator.IsInTransition(cameraMotionLayer);
        if (!TryGetClipState(cameraMotionLayer, incoming, CameraMotionClip, out var state) ||
            state.fullPathHash != cameraMotionState ||
            state.normalizedTime + 0.001f < cameraMotionProgress ||
            Mathf.Floor(state.normalizedTime) > Mathf.Floor(cameraMotionProgress))
            return true;

        cameraMotionProgress = state.normalizedTime;
        return false;
    }

    private bool TryGetClipState(int layer, bool incoming, string clipName, out AnimatorStateInfo state)
    {
        cameraClips.Clear();
        if (incoming)
            bossAnimator.GetNextAnimatorClipInfo(layer, cameraClips);
        else
            bossAnimator.GetCurrentAnimatorClipInfo(layer, cameraClips);

        foreach (AnimatorClipInfo info in cameraClips)
        {
            if (!info.clip || info.clip.name != clipName)
                continue;

            state = incoming
                ? bossAnimator.GetNextAnimatorStateInfo(layer)
                : bossAnimator.GetCurrentAnimatorStateInfo(layer);
            return true;
        }

        state = default;
        return false;
    }

    private void OnDisable()
    {
        SetCameraShot(CameraShot.None);
        decreeCameraRequest = NoDecreeCameraRequest;
        RestorePose();
        if (spray)
            spray.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void UpdateCameraShot()
    {
        CameraShot shot = GetCurrentCameraShot();
        if (shot != CameraShot.AbyssalDecree)
            decreeCameraRequest = NoDecreeCameraRequest;

        // Dread Gaze and older Decree clips key Camera.enabled on the shared camera.
        bool requested = shot == CameraShot.AbyssalDecree && decreeCameraRequest != NoDecreeCameraRequest
            ? decreeCameraRequest > 0.5f
            : shot != CameraShot.None && decreeCamera.enabled;
        if (!requested)
        {
            SetCameraShot(CameraShot.None);
            return;
        }

        if (shot == CameraShot.AbyssalDecree && stageCamera)
        {
            Transform cameraTransform = decreeCamera.transform;
            Vector3 stageHeading = Vector3.ProjectOnPlane(SeaStackStageCameraLock.WorldForward(stageCamera), Vector3.up);
            Vector3 shotHeading = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up);
            if (stageHeading.sqrMagnitude > 0.0001f && shotHeading.sqrMagnitude > 0.0001f)
            {
                float headingCorrection = Vector3.SignedAngle(shotHeading, stageHeading, Vector3.up);
                cameraTransform.rotation = Quaternion.AngleAxis(headingCorrection, Vector3.up) * cameraTransform.rotation;
            }
        }

        SetCameraShot(shot);
    }

    private void SetCameraShot(CameraShot shot)
    {
        bool shown = shot != CameraShot.None;
        if (decreeCamera && decreeCamera.enabled != shown)
            decreeCamera.enabled = shown;
        if (cinematicShown && !shown && stageCameraRecovery)
            stageCameraRecovery.RequestReturn();
        cinematicShown = shown;

        SetOverlayVisibility(decreeCameraWash, shot == CameraShot.AbyssalDecree);
        SetOverlayVisibility(dreadGazeVeil, shot == CameraShot.DreadGaze);
    }

    private static void SetOverlayVisibility(Renderer overlay, bool shown)
    {
        if (overlay && overlay.forceRenderingOff == shown)
            overlay.forceRenderingOff = !shown;
    }

    private CameraShot GetCurrentCameraShot()
    {
        if (!bossAnimator || !bossAnimator.isActiveAndEnabled || !bossAnimator.isInitialized)
            return CameraShot.None;

        for (int layer = 0; layer < bossAnimator.layerCount; layer++)
        {
            if (layer > 0 && bossAnimator.GetLayerWeight(layer) <= 0f)
                continue;

            cameraClips.Clear();
            // Once a transition starts, only the incoming state can retain the camera.
            if (bossAnimator.IsInTransition(layer))
                bossAnimator.GetNextAnimatorClipInfo(layer, cameraClips);
            else
                bossAnimator.GetCurrentAnimatorClipInfo(layer, cameraClips);

            foreach (AnimatorClipInfo info in cameraClips)
            {
                if (info.weight <= 0f || !info.clip)
                    continue;
                if (info.clip.name == AbyssalDecreeClip)
                    return CameraShot.AbyssalDecree;
                if (info.clip.name == DreadGazeClip)
                    return CameraShot.DreadGaze;
            }
        }

        return CameraShot.None;
    }

    private void ApplyMotion(float elapsed)
    {
        if (motion == Motion.HullFist)
        {
            Pose hull = SampleHullFistBoat(elapsed);
            if (motionBoat)
            {
                motionBoat.SetLocalPositionAndRotation(boatRest.position + boatRest.rotation * hull.position,
                    boatRest.rotation * hull.rotation);
            }
            if (motionCameraRecovery)
            {
                Pose view = SampleHullFistCamera(elapsed, hull);
                ApplyCameraOffset(boatRest.rotation * view.position, view.rotation);
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
        if (motionCameraRecovery)
        {
            float kick = Mathf.Exp(-time * (tidalWave ? 10f : 20f));
            float ringing = tidalWave
                ? Mathf.Sin(time * 19f) * Mathf.Exp(-time * 5.8f)
                : Mathf.Sin(time * 50f) * Mathf.Exp(-time * 11f);
            var offset = new Vector3(0.064f * ringing, 0.04f * kick + 0.027f * ringing, -0.115f * kick) * weight;
            var rotation = Quaternion.Euler((-1.6f * kick + 1.1f * ringing) * weight,
                0.44f * ringing * weight, -0.64f * ringing * weight);
            ApplyCameraOffset(SeaStackStageCameraLock.StageRotation * offset, rotation);
        }
        if (motionBoat)
        {
            float damping = Mathf.Exp(-time * (tidalWave ? 3.4f : 5.4f));
            float rocking = Mathf.Sin(time * (tidalWave ? 9.2f : 12.5f)) * damping * weight;
            float heave = Mathf.Sin(time * (tidalWave ? 6.4f : 9.4f)) * damping * weight;
            var rotation = Quaternion.Euler(-(tidalWave ? 8.5f : 6.8f) * rocking, 0f, (tidalWave ? 2.4f : 1.5f) * rocking);
            motionBoat.SetLocalPositionAndRotation(
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
        if (motionBoat)
        {
            var shift = new Vector3(-0.12f, -0.11f, 0.33f) * pull;
            var tilt = Quaternion.Euler(2.9f * pull, -1.7f * pull, 4.5f * pull);
            motionBoat.SetLocalPositionAndRotation(boatRest.position + shift, boatRest.rotation * tilt);
        }

        if (motionCameraRecovery)
        {
            var offset = new Vector3(-0.07f, -0.06f, 0.19f) * pull;
            ApplyCameraOffset(offset, Quaternion.Euler(0.6f * pull, -0.55f * pull, 1.8f * pull));
        }
    }

    private void ApplyCameraOffset(Vector3 offset, Quaternion rotation)
    {
        motionCameraRecovery.ApplyAttackPose(cameraMotionToken,
            SeaStackStageCameraLock.StagePosition + offset,
            SeaStackStageCameraLock.StageRotation * rotation);
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

        recoiling = false;
        EndCameraMotion();
        ReleaseTarget(motionBoat, boatRest);
        motionBoat = null;
    }

    private void EndCameraMotion()
    {
        if (motionCameraRecovery)
            motionCameraRecovery.EndAttack(cameraMotionToken);
        motionCameraRecovery = null;
        cameraMotionLayer = -1;
    }

    private static void RestorePreviousOwner(Transform target)
    {
        if (target && MotionOwners.TryGetValue(target, out var owner))
            owner.RestorePose();
    }

    private void ReleaseTarget(Transform target, Pose rest)
    {
        if (ReferenceEquals(target, null) || !MotionOwners.TryGetValue(target, out var owner) || owner != this)
            return;

        MotionOwners.Remove(target);
        if (target)
            target.SetLocalPositionAndRotation(rest.position, rest.rotation);
    }

    private static Pose LocalPose(Transform target)
    {
        return target ? new Pose(target.localPosition, target.localRotation) : Pose.identity;
    }
}
