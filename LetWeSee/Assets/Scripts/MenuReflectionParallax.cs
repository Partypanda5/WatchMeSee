using UnityEngine;
using UnityEngine.InputSystem;

public sealed class MenuReflectionParallax : MonoBehaviour
{
    [SerializeField] private RectTransform artworkArea;
    [SerializeField] private RectTransform reflection;
    [SerializeField, Range(0f, 0.04f)] private float travelAsScreenFraction = 0.012f;
    [SerializeField, Range(1f, 20f)] private float followSpeed = 7f;

    private Vector2 restingPosition;
    private Canvas parentCanvas;

    private void Awake()
    {
        if (reflection == null) reflection = transform as RectTransform;
        if (artworkArea == null && reflection != null)
            artworkArea = reflection.parent as RectTransform;
        if (reflection != null) restingPosition = reflection.anchoredPosition;
        if (artworkArea != null) parentCanvas = artworkArea.GetComponentInParent<Canvas>();
    }

    private void Update()
    {
        if (artworkArea == null || reflection == null || Mouse.current == null) return;

        Camera uiCamera = parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? parentCanvas.worldCamera
            : null;
        Vector2 pointer = Mouse.current.position.ReadValue();
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                artworkArea, pointer, uiCamera, out Vector2 localPointer))
            return;

        Rect bounds = artworkArea.rect;
        float x = Mathf.Clamp((localPointer.x - bounds.center.x) / Mathf.Max(1f, bounds.width * 0.5f), -1f, 1f);
        float y = Mathf.Clamp((localPointer.y - bounds.center.y) / Mathf.Max(1f, bounds.height * 0.5f), -1f, 1f);
        float travel = Mathf.Min(bounds.width, bounds.height) * travelAsScreenFraction;
        Vector2 target = restingPosition + new Vector2(-x, -y) * travel;
        float blend = 1f - Mathf.Exp(-followSpeed * Time.unscaledDeltaTime);
        reflection.anchoredPosition = Vector2.Lerp(reflection.anchoredPosition, target, blend);
    }
}