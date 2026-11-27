using UnityEngine;

namespace CreatureAnim
{
    // The apple belongs to this one preview clip. Always clear it when another
    // controller/clip is selected, including a switch in the middle of eating.
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class FireBearRecoveryApple : MonoBehaviour
    {
        public const string RecoveryClipName = "FireBear_Recovery_AppleEat";
        public Animator animator;
        public Transform apple;
        public Renderer[] renderers;

        void OnEnable() => RefreshVisibility();
        void LateUpdate() => RefreshVisibility();
        void OnDisable() => Hide();

        bool IsRecovery()
        {
            if (!animator || !animator.isActiveAndEnabled || !animator.runtimeAnimatorController) return false;
            if (Application.isPlaying && animator.isInitialized)
            {
                foreach (var info in animator.GetCurrentAnimatorClipInfo(0))
                    if (info.clip && info.clip.name == RecoveryClipName && info.weight > 0) return true;
                return false;
            }
            foreach (var clip in animator.runtimeAnimatorController.animationClips)
                if (clip && clip.name == RecoveryClipName) return true;
            return false;
        }

        public void RefreshVisibility()
        {
            bool recovery = IsRecovery();
            if (!recovery && apple && apple.localScale != Vector3.zero) apple.localScale = Vector3.zero;
            bool visible = recovery && apple && apple.localScale.sqrMagnitude > .000001f;
            if (renderers == null) return;
            foreach (var renderer in renderers)
                if (renderer && renderer.enabled != visible) renderer.enabled = visible;
        }

        void Hide()
        {
            if (apple && apple.localScale != Vector3.zero) apple.localScale = Vector3.zero;
            if (renderers == null) return;
            foreach (var renderer in renderers)
                if (renderer && renderer.enabled) renderer.enabled = false;
        }
    }
}
