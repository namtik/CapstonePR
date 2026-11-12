using UnityEngine;

/// <summary>Changes the sword's pre-baked environment only when the stage time changes.</summary>
[ExecuteAlways, DisallowMultipleComponent]
public sealed class KatanaReflectionPresets : MonoBehaviour
{
    [SerializeField] StageLightingManager lighting;
    [SerializeField] Renderer sword;
    [SerializeField] Cubemap[] environments = new Cubemap[8];
    static readonly int ReflectionId = Shader.PropertyToID("_BladeReflection");
    MaterialPropertyBlock block;
    int applied = -1;

    public void Configure(StageLightingManager manager, Renderer target, Cubemap[] maps)
    {
        lighting = manager;
        sword = target;
        environments = maps;
        Refresh();
    }
    void OnEnable() { applied = -1; Refresh(); }
    void OnValidate() { applied = -1; Refresh(); }
    void Update()
    {
        if (lighting && (int)lighting.CurrentTime != applied) Refresh();
    }
    public void Refresh()
    {
        if (!lighting || !sword || environments == null) return;
        int index = (int)lighting.CurrentTime;
        if (index < 0 || index >= environments.Length || !environments[index]) return;
        if (block == null) block = new MaterialPropertyBlock();
        sword.GetPropertyBlock(block);
        block.SetTexture(ReflectionId, environments[index]);
        sword.SetPropertyBlock(block);
        applied = index;
    }
    void OnDisable()
    {
        if (sword && sword.sharedMaterial && sword.sharedMaterial.HasProperty(ReflectionId))
        {
            if (block == null) block = new MaterialPropertyBlock();
            sword.GetPropertyBlock(block);
            block.SetTexture(ReflectionId, sword.sharedMaterial.GetTexture(ReflectionId));
            sword.SetPropertyBlock(block);
        }
        applied = -1;
    }
}

