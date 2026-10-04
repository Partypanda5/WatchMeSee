using UnityEngine;

// Tints this object and all its children that use the Mesh2D-Lit-Tint shader, without changing the shared materials.
[ExecuteAlways]
public sealed class TintGroup : MonoBehaviour
{
    [SerializeField] private Color tint = Color.white;

    private static readonly int TintId = Shader.PropertyToID("_Tint");
    private MaterialPropertyBlock block;

    public Color Tint
    {
        get => tint;
        set { tint = value; Apply(); }
    }

    private void OnEnable() => Apply();
    private void OnValidate() => Apply();

    private void Apply()
    {
        if (block == null) block = new MaterialPropertyBlock();
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
        {
            r.GetPropertyBlock(block);
            block.SetColor(TintId, tint);
            r.SetPropertyBlock(block);
        }
    }
}
