using UnityEngine;

namespace Sashindo.VFX
{
    public sealed class AwakeningBattlePreset : ScriptableObject
    {
        [SerializeField] private AwakeningVfx entryPrefab;

        public AwakeningVfx EntryPrefab => entryPrefab;
    }
}
