using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class GumihoProcessionDirector : MonoBehaviour
{
    public const float Duration = 5.4f;
    [SerializeField] Animator actor;
    [SerializeField] RuntimeAnimatorController procession;
    [Tooltip("Assign the animation clip to detect the procession in an existing combat controller.")]
    [SerializeField] AnimationClip processionClip;
    [Tooltip("Fallback camera if no loaded stage has a StageCamera.")]
    [SerializeField] Camera battleCamera;
    [Tooltip("Fallback light if no loaded stage has a Directional Light.")]
    [SerializeField] Light keyLight;
    [Tooltip("Optional. Place this transform at the desired low-angle camera pose.")]
    [SerializeField] Transform shotAnchor;
    [Tooltip("Preview when entering Play Mode or enabling this component. Leave off when triggering from battle logic.")]
    [SerializeField] bool playOnEnable;
    [SerializeField, ColorUsage(false)] Color nightBackground = new Color(.022f,.030f,.065f);
    [SerializeField] bool cameraForegroundFire = true;

    RuntimeAnimatorController previousController;
    readonly List<AnimatorClipInfo> stateClips = new List<AnimatorClipInfo>(2);
    bool launchedByDirector;
    bool suppressObservedPlayback;
    float stoppedStateTime;
    Camera activeCamera;
    Light activeLight;
    Transform activeShotAnchor;
    Scene ambientScene;
    bool refreshStageBindings;
    Vector3 cameraPosition;
    Quaternion cameraRotation;
    CameraClearFlags clearFlags;
    Color background;
    float ambientIntensity;
    bool adjustAmbient;
    Renderer[] eyeRenderers;
    MaterialPropertyBlock[] eyeOriginal;
    MaterialPropertyBlock eyeBlock;
    float cameraFov, lightIntensity;
    bool running;
    GameObject foreground;
    Mesh foregroundMesh;
    MeshRenderer foregroundRenderer;
    const int ForegroundFlames = 7;
    readonly Vector3[] foregroundVertices = new Vector3[ForegroundFlames * 4];
    readonly Color[] foregroundColors = new Color[ForegroundFlames * 4];

    void OnEnable()
    {
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
        if (!processionClip && procession && procession.animationClips.Length > 0)
            processionClip = procession.animationClips[0];
        if (Application.isPlaying && playOnEnable) PlayProcession();
    }

    [ContextMenu("Play Procession")]
    public void PlayProcession()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Enter Play Mode, then run Play Procession. Enable Play On Enable to preview automatically.", this);
            return;
        }
        if (!isActiveAndEnabled || !actor || !actor.isActiveAndEnabled || !procession)
        {
            Debug.LogWarning("Enable the director and Animator, and assign Actor and Procession before playing.", this);
            return;
        }
        if (!processionClip)
        {
            Debug.LogWarning("The Procession controller has no animation clip.", this);
            return;
        }
        if (running) StopProcession();
        suppressObservedPlayback = false;
        previousController = actor.runtimeAnimatorController;
        launchedByDirector = true;
        actor.runtimeAnimatorController = procession;
        actor.Play(0, 0, 0f);
        BeginEffects();
    }

    void BeginEffects()
    {
        CaptureStageBindings();
        eyeRenderers = System.Array.FindAll(actor.GetComponentsInChildren<Renderer>(true),
            renderer => renderer.sharedMaterial && (renderer.sharedMaterial.shader.name == "Sasindo/Gumiho Eye Fire"
                || renderer.sharedMaterial.HasProperty("_EyeIntensity")));
        eyeOriginal = new MaterialPropertyBlock[eyeRenderers.Length];
        eyeBlock = new MaterialPropertyBlock();
        for (int i = 0; i < eyeRenderers.Length; i++)
        {
            eyeOriginal[i] = new MaterialPropertyBlock();
            eyeRenderers[i].GetPropertyBlock(eyeOriginal[i]);
        }
        running = true;
    }

    void CaptureStageBindings()
    {
        refreshStageBindings = false;
        var stage = SceneManager.GetActiveScene();
        FindStageBindings(stage, out activeCamera, out activeLight);
        if (!activeCamera)
        {
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                var candidate = SceneManager.GetSceneAt(i);
                if (candidate == stage) continue;
                FindStageBindings(candidate, out var camera, out var light);
                if (!camera) continue;
                activeCamera = camera;
                activeLight = light;
                stage = candidate;
                break;
            }
        }
        if (!activeCamera && battleCamera && battleCamera.gameObject.scene == stage) activeCamera = battleCamera;
        if (!activeLight && keyLight && keyLight.gameObject.scene == stage) activeLight = keyLight;
        activeShotAnchor = shotAnchor && (shotAnchor.gameObject.scene == stage || shotAnchor.IsChildOf(actor.transform))
            ? shotAnchor : null;
        if (activeCamera)
        {
            cameraPosition = activeCamera.transform.position;
            cameraRotation = activeCamera.transform.rotation;
            cameraFov = activeCamera.fieldOfView;
            clearFlags = activeCamera.clearFlags;
            background = activeCamera.backgroundColor;
        }
        lightIntensity = activeLight ? activeLight.intensity : 0f;
        ambientScene = SceneManager.GetActiveScene();
        ambientIntensity = RenderSettings.ambientIntensity;
        adjustAmbient = activeLight && activeLight.gameObject.scene == ambientScene;
    }

    static void FindStageBindings(Scene scene, out Camera camera, out Light light)
    {
        camera = null;
        light = null;
        if (!scene.IsValid() || !scene.isLoaded) return;
        foreach (var root in scene.GetRootGameObjects())
        {
            if (!camera)
                foreach (var candidate in root.GetComponentsInChildren<Camera>())
                    if (candidate.name == "StageCamera" && candidate.isActiveAndEnabled)
                    {
                        camera = candidate;
                        break;
                    }
            if (!light)
                foreach (var candidate in root.GetComponentsInChildren<Light>())
                    if (candidate.name == "Directional Light" && candidate.type == LightType.Directional && candidate.isActiveAndEnabled)
                    {
                        light = candidate;
                        break;
                    }
            if (camera && light) return;
        }
    }

    void OnActiveSceneChanged(Scene previous, Scene next) => refreshStageBindings = true;
    void OnSceneLoaded(Scene scene, LoadSceneMode mode) => refreshStageBindings = true;
    void OnSceneUnloaded(Scene scene) => refreshStageBindings = true;

    void LateUpdate()
    {
        if (!TryGetProcessionTime(out float stateTime))
        {
            suppressObservedPlayback = false;
            if (running) EndEffects(false);
            return;
        }
        if (suppressObservedPlayback)
        {
            if (stateTime >= stoppedStateTime) return;
            suppressObservedPlayback = false;
        }
        float time = stateTime * processionClip.length;
        if (launchedByDirector && time >= Duration)
        {
            StopProcession();
            return;
        }
        if (!launchedByDirector) time = Mathf.Repeat(stateTime, 1f) * processionClip.length;
        if (!running) BeginEffects();
        ApplyFrame(time);
    }

    bool TryGetProcessionTime(out float stateTime)
    {
        stateTime = 0f;
        if (!processionClip || !actor || !actor.isActiveAndEnabled || !actor.runtimeAnimatorController || actor.layerCount == 0)
            return false;
        stateClips.Clear();
        bool transitioning = actor.IsInTransition(0);
        if (transitioning) actor.GetNextAnimatorClipInfo(0, stateClips);
        else actor.GetCurrentAnimatorClipInfo(0, stateClips);
        foreach (var info in stateClips)
        {
            if (info.clip != processionClip) continue;
            stateTime = (transitioning ? actor.GetNextAnimatorStateInfo(0) : actor.GetCurrentAnimatorStateInfo(0)).normalizedTime;
            return true;
        }
        return false;
    }

    void ApplyFrame(float time)
    {
        if (!running) return;
        if (refreshStageBindings)
        {
            RestoreStageBindings();
            CaptureStageBindings();
        }
        if (activeCamera) FrameShot(activeCamera, actor.transform, cameraPosition, cameraRotation, cameraFov, time, activeShotAnchor);
        UpdateForegroundFire(time);
        float dark = Darkness(time);
        if (activeCamera && (clearFlags == CameraClearFlags.Skybox || clearFlags == CameraClearFlags.SolidColor))
        {
            activeCamera.clearFlags = dark > .01f ? CameraClearFlags.SolidColor : clearFlags;
            activeCamera.backgroundColor = Color.Lerp(background, nightBackground, dark);
        }
        if (activeLight)
        {
            activeLight.intensity = lightIntensity * Mathf.Lerp(1f, .045f, dark);
        }
        if (adjustAmbient) RenderSettings.ambientIntensity = ambientIntensity * Mathf.Lerp(1f, .20f, dark);
        foreach (var eye in eyeRenderers)
        {
            if (!eye) continue;
            var material = eye.sharedMaterial;
            if (!material) continue;
            string property = material.HasProperty("_EyeIntensity") ? "_EyeIntensity" : "_Intensity";
            if (!material.HasProperty(property)) continue;
            eye.GetPropertyBlock(eyeBlock);
            eyeBlock.SetFloat(property, material.GetFloat(property) * Mathf.Lerp(1f, 2.7f, dark));
            eye.SetPropertyBlock(eyeBlock);
        }
    }

    public void StopProcession()
    {
        if (!running) return;
        bool currentProcession = TryGetProcessionTime(out stoppedStateTime);
        suppressObservedPlayback = currentProcession;
        EndEffects(currentProcession && launchedByDirector && actor.runtimeAnimatorController == procession);
    }

    void EndEffects(bool restoreController)
    {
        running = false;
        launchedByDirector = false;
        if (restoreController)
        {
            var fx = actor.transform.Find("gumiho_rig/MotionRoot/Gumiho_AnimationAttackFX");
            if (fx)
            {
                foreach (var renderer in fx.GetComponentsInChildren<MeshRenderer>()) renderer.enabled = false;
                foreach (var filter in fx.GetComponentsInChildren<MeshFilter>()) filter.sharedMesh = null;
                var flameLight = fx.GetComponentInChildren<Light>();
                if (flameLight) flameLight.intensity = 0f;
            }
            if (actor.runtimeAnimatorController != previousController)
            {
                actor.runtimeAnimatorController = previousController;
                if (previousController) { actor.Rebind(); actor.Update(0f); }
            }
        }
        previousController = null;
        RestoreStageBindings();
        for (int i = 0; i < eyeRenderers.Length; i++)
            if (eyeRenderers[i]) eyeRenderers[i].SetPropertyBlock(eyeOriginal[i]);
    }

    void RestoreStageBindings()
    {
        ClearForegroundFire();
        if (activeCamera)
        {
            activeCamera.transform.SetPositionAndRotation(cameraPosition, cameraRotation);
            activeCamera.fieldOfView = cameraFov;
            activeCamera.clearFlags = clearFlags;
            activeCamera.backgroundColor = background;
        }
        if (activeLight) activeLight.intensity = lightIntensity;
        if (adjustAmbient && SceneManager.GetActiveScene() == ambientScene)
            RenderSettings.ambientIntensity = ambientIntensity;
        activeCamera = null;
        activeLight = null;
        activeShotAnchor = null;
        adjustAmbient = false;
    }

    void OnDisable()
    {
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
        StopProcession();
    }

    void UpdateForegroundFire(float time)
    {
        if (!cameraForegroundFire || !activeCamera || time < 2.83f || time > 3.56f)
        {
            if (foregroundRenderer) foregroundRenderer.enabled = false;
            return;
        }
        if (!foreground)
        {
            var source = actor.transform.Find("gumiho_rig/MotionRoot/Gumiho_AnimationAttackFX/Flames");
            if (!source || !source.TryGetComponent<MeshRenderer>(out var sourceRenderer) || !sourceRenderer.sharedMaterial) return;
            foreground = new GameObject("Azure Procession Foreground Fire");
            foreground.hideFlags = HideFlags.DontSave;
            foreground.layer = source.gameObject.layer;
            foreground.transform.SetParent(activeCamera.transform, false);
            foregroundMesh = new Mesh { name = "Azure Procession Foreground Flame Mesh" };
            foregroundMesh.MarkDynamic();
            var uv = new Vector2[foregroundVertices.Length];
            var triangles = new int[ForegroundFlames * 6];
            for (int i = 0; i < ForegroundFlames; i++)
            {
                int vertex = i * 4;
                uv[vertex] = Vector2.zero;
                uv[vertex + 1] = Vector2.right;
                uv[vertex + 2] = Vector2.up;
                uv[vertex + 3] = Vector2.one;
                int triangle = i * 6;
                triangles[triangle] = vertex;
                triangles[triangle + 1] = vertex + 2;
                triangles[triangle + 2] = vertex + 1;
                triangles[triangle + 3] = vertex + 1;
                triangles[triangle + 4] = vertex + 2;
                triangles[triangle + 5] = vertex + 3;
            }
            foregroundMesh.vertices = foregroundVertices;
            foregroundMesh.uv = uv;
            foregroundMesh.triangles = triangles;
            foreground.AddComponent<MeshFilter>().sharedMesh = foregroundMesh;
            foregroundRenderer = foreground.AddComponent<MeshRenderer>();
            foregroundRenderer.sharedMaterial = sourceRenderer.sharedMaterial;
            foregroundRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            foregroundRenderer.receiveShadows = false;
        }

        for (int i = 0; i < ForegroundFlames; i++)
        {
            float age = time - (2.83f + Mathf.Abs(i - 3) * .024f);
            float rise = Ease(age / .22f);
            float opacity = Ease(age / .09f) * (1 - Ease((age - .25f) / .38f));
            float x = .04f + i * .153f;
            float y = -.37f + .20f * rise + .18f * Ease((age - .20f) / .40f);
            float height = (.55f + .14f * Mathf.Cos(i * 1.9f)) * rise;
            float width = .25f + .07f * Mathf.Sin(i * 2.4f);
            float depth = activeCamera.nearClipPlane + .27f + i * .017f;
            for (int corner = 0; corner < 4; corner++)
            {
                bool top = corner >= 2;
                var point = new Vector3(x + (corner % 2 == 0 ? -.5f : .5f) * width
                    + (top ? .044f * Mathf.Sin(i + age * 5f) : 0f), y + (top ? height : 0f), depth);
                int vertex = i * 4 + corner;
                foregroundVertices[vertex] = foreground.transform.InverseTransformPoint(activeCamera.ViewportToWorldPoint(point));
                foregroundColors[vertex] = new Color(.045f, .38f + .10f * Mathf.Sin(i), 1.65f, opacity);
            }
        }
        foregroundMesh.vertices = foregroundVertices;
        foregroundMesh.colors = foregroundColors;
        foregroundMesh.RecalculateBounds();
        foregroundRenderer.enabled = true;
    }

    void ClearForegroundFire()
    {
        if (foreground) { foreground.SetActive(false); Destroy(foreground); }
        if (foregroundMesh) Destroy(foregroundMesh);
        foreground = null;
        foregroundMesh = null;
        foregroundRenderer = null;
    }

    static float Ease(float value) => Mathf.SmoothStep(0f,1f,Mathf.Clamp01(value));
    public static float Darkness(float time) => Ease(time/.78f)*(1f-Ease((time-3.88f)/1.42f));

    public static void FrameShot(Camera camera, Transform fox, Vector3 originalPosition,
        Quaternion originalRotation, float originalFov, float time, Transform anchor = null)
    {
        float blend=Ease(time/.90f)*(1f-Ease((time-4.02f)/1.29f));
        float drift=Ease((time-.75f)/2.02f);
        var position=anchor ? anchor.position : fox.TransformPoint(new Vector3(.23f-.26f*drift,.095f,.93f-.08f*drift));
        var focus=fox.TransformPoint(new Vector3(0,.32f,.14f));
        camera.transform.SetPositionAndRotation(Vector3.Lerp(originalPosition,position,blend),
            Quaternion.Slerp(originalRotation,anchor ? anchor.rotation : Quaternion.LookRotation(focus-position,Vector3.up),blend));
        camera.fieldOfView=Mathf.Lerp(originalFov,53f,blend)+4f*Ease((time-2.58f)/.14f)*(1f-Ease((time-2.77f)/.64f));
    }
}
