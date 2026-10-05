using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
[RequireComponent(typeof(Graphic))]
public sealed class MenuReflectionBlur : MonoBehaviour
{
    [SerializeField, Range(0f, 12f)]
    [Tooltip("Adjust the softness of the character reflection in the mirror.")]
    private float blurAmount = 4f;

    private Graphic graphic;
    private Material originalMaterial;
    private Material blurMaterial;

    private void OnEnable()
    {
        CacheGraphic();
        ApplyBlur();
    }

    private void OnValidate()
    {
        blurAmount = Mathf.Clamp(blurAmount, 0f, 12f);
        CacheGraphic();
        ApplyBlur();
    }

    private void Update()
    {
        if (blurMaterial == null)
            ApplyBlur();
        else
            blurMaterial.SetFloat("_BlurAmount", blurAmount);
    }

    private void OnDisable()
    {
        if (graphic != null && graphic.material == blurMaterial)
            graphic.material = originalMaterial;

        if (blurMaterial == null) return;
        if (Application.isPlaying) Destroy(blurMaterial);
        else DestroyImmediate(blurMaterial);
        blurMaterial = null;
    }

    private void CacheGraphic()
    {
        if (graphic != null) return;
        graphic = GetComponent<Graphic>();
        if (graphic != null && blurMaterial == null)
            originalMaterial = graphic.material;
    }

    private void ApplyBlur()
    {
        if (graphic == null) return;
        if (blurMaterial == null)
        {
            Shader shader = Shader.Find("UI/MenuReflectionBlur");
            if (shader == null) return;
            blurMaterial = new Material(shader)
            {
                name = "Menu Reflection Blur (Runtime)",
                hideFlags = HideFlags.HideAndDontSave
            };
            graphic.material = blurMaterial;
        }
        blurMaterial.SetFloat("_BlurAmount", blurAmount);
    }
}