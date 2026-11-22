using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
[DefaultExecutionOrder(32000)]
public sealed class SeaStackStageCameraLock : MonoBehaviour
{
    public static readonly Vector3 StagePosition = new(0f, 1f, -9.5f);
    public static readonly Quaternion StageRotation = Quaternion.Euler(-10f, 0f, 0f);
    private const float DriftDelay = 0.9f;
    private const float ReturnDuration = 0.35f;
    private const float QuietDuration = 0.15f;

    private Scene arena;
    private int attackToken;
    private bool attackActive;
    private bool returnRequested;
    private float attackDeadline;
    private float driftStarted = -1f;
    private float returnStarted = -1f;
    private float quietStarted = -1f;
    private bool settling;
    private Pose returnFrom;

    public static SeaStackStageCameraLock Attach(Transform view, Scene arenaScene)
    {
        if (!view)
            return null;

        if (!view.TryGetComponent(out SeaStackStageCameraLock cameraLock))
            cameraLock = view.gameObject.AddComponent<SeaStackStageCameraLock>();

        cameraLock.arena = arenaScene;
        return cameraLock;
    }

    public static Vector3 WorldForward(Transform view)
    {
        Vector3 forward = StageRotation * Vector3.forward;
        return view.parent ? view.parent.TransformDirection(forward) : forward;
    }

    public int BeginAttack(float duration)
    {
        attackActive = true;
        attackDeadline = Time.unscaledTime + duration;
        driftStarted = returnStarted = quietStarted = -1f;
        settling = false;
        returnRequested = false;
        return ++attackToken;
    }

    public void ApplyAttackPose(int token, Vector3 position, Quaternion rotation)
    {
        if (attackActive && token == attackToken && Time.unscaledTime < attackDeadline)
            transform.SetLocalPositionAndRotation(position, rotation);
    }

    public void EndAttack(int token)
    {
        if (!attackActive || token != attackToken)
            return;

        attackActive = false;
        RequestReturn();
    }

    public void RequestReturn()
    {
        if (!attackActive && returnStarted < 0f && !settling)
            returnRequested = true;
    }

    private void Awake()
    {
        arena = gameObject.scene;
    }

    private void LateUpdate()
    {
        if (!arena.isLoaded)
        {
            if (attackActive || returnRequested || returnStarted >= 0f || driftStarted >= 0f || settling)
            {
                attackActive = returnRequested = settling = false;
                returnStarted = quietStarted = -1f;
                RestoreOrigin();
            }
            return;
        }

        float now = Time.unscaledTime;
        if (attackActive)
        {
            if (now < attackDeadline)
                return;

            EndAttack(attackToken);
        }

        if (returnRequested)
        {
            returnRequested = false;
            if (AtOrigin())
            {
                RestoreOrigin();
                settling = true;
                quietStarted = -1f;
            }
            else
            {
                returnFrom = new Pose(transform.localPosition, transform.localRotation);
                returnStarted = now;
            }
        }

        if (returnStarted >= 0f)
        {
            float progress = Mathf.Clamp01((now - returnStarted) / ReturnDuration);
            float blend = Mathf.SmoothStep(0f, 1f, progress);
            transform.SetLocalPositionAndRotation(
                Vector3.Lerp(returnFrom.position, StagePosition, blend),
                Quaternion.Slerp(returnFrom.rotation, StageRotation, blend));
            if (progress >= 1f)
            {
                RestoreOrigin();
                returnStarted = -1f;
                settling = true;
            }
            return;
        }

        if (settling)
        {
            // If another animation is still writing the old shot, keep the settled view
            // until it stops. A new boss attack always opens a fresh motion window.
            if (!AtOrigin())
            {
                quietStarted = -1f;
                RestoreOrigin();
                return;
            }
            if (quietStarted < 0f)
                quietStarted = now;
            if (now - quietStarted >= QuietDuration)
                settling = false;
            return;
        }

        if (AtOrigin())
            driftStarted = -1f;
        else if (driftStarted < 0f)
            driftStarted = now;
        else if (now - driftStarted >= DriftDelay)
            returnRequested = true;
    }

    private bool AtOrigin()
    {
        return (transform.localPosition - StagePosition).sqrMagnitude < 1e-10f &&
            Quaternion.Angle(transform.localRotation, StageRotation) < 0.01f;
    }

    private void RestoreOrigin()
    {
        driftStarted = -1f;
        transform.SetLocalPositionAndRotation(StagePosition, StageRotation);
    }
}
