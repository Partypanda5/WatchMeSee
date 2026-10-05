using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public sealed class CafeSceneFadeIn : MonoBehaviour
{
    private const float FadeDuration = 1.25f;

    private void Awake()
    {
        GameObject fadeCanvasObject = new GameObject(
            "Cafe Fade Canvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));

        Canvas fadeCanvas = fadeCanvasObject.GetComponent<Canvas>();
        fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        fadeCanvas.overrideSorting = true;
        fadeCanvas.sortingOrder = short.MaxValue;

        CanvasScaler scaler = fadeCanvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject overlay = new GameObject(
            "Black Fade Overlay",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        RectTransform overlayRect = overlay.GetComponent<RectTransform>();
        overlayRect.SetParent(fadeCanvasObject.transform, false);
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        Image image = overlay.GetComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = true;
        StartCoroutine(FadeOut(image, fadeCanvasObject));
    }

    private IEnumerator FadeOut(Image image, GameObject fadeCanvasObject)
    {
        float elapsed = 0f;
        while (elapsed < FadeDuration)
        {
            if (image == null || fadeCanvasObject == null)
                yield break;

            elapsed += Time.unscaledDeltaTime;
            Color color = Color.black;
            color.a = 1f - Mathf.Clamp01(elapsed / FadeDuration);
            image.color = color;
            yield return null;
        }

        if (fadeCanvasObject != null)
            Destroy(fadeCanvasObject);
        Destroy(this);
    }
}