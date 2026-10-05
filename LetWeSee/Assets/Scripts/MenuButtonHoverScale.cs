using UnityEngine;
using UnityEngine.EventSystems;

public sealed class MenuButtonHoverScale : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField, Range(1f, 1.3f)] private float hoveredScale = 1.12f;
    [SerializeField, Range(1f, 30f)] private float transitionSpeed = 14f;
    [SerializeField] private AudioClip hoverSound;

    private RectTransform target;
    private Vector3 restScale;
    private bool isHovered;
    private AudioSource hoverAudioSource;

    private void Awake()
    {
        target = transform as RectTransform;
        if (target != null) restScale = target.localScale;

        if (hoverSound != null)
        {
            hoverAudioSource = GetComponent<AudioSource>();
            if (hoverAudioSource == null) hoverAudioSource = gameObject.AddComponent<AudioSource>();
            hoverAudioSource.playOnAwake = false;
            hoverAudioSource.loop = false;
            hoverAudioSource.spatialBlend = 0f;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (isHovered) return;
        isHovered = true;
        if (hoverSound != null && hoverAudioSource != null)
            hoverAudioSource.PlayOneShot(hoverSound);
    }

    public void OnPointerExit(PointerEventData eventData) => isHovered = false;

    private void OnDisable()
    {
        isHovered = false;
        if (target != null) target.localScale = restScale;
    }

    private void Update()
    {
        if (target == null) return;
        Vector3 wantedScale = restScale * (isHovered ? hoveredScale : 1f);
        float blend = 1f - Mathf.Exp(-transitionSpeed * Time.unscaledDeltaTime);
        target.localScale = Vector3.Lerp(target.localScale, wantedScale, blend);
    }
}
