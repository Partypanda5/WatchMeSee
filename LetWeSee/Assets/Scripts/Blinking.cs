using UnityEngine;

// Every so often swaps this renderer's texture for a blink texture for a moment, hiding the listed renderers (the pupils) meanwhile.
[RequireComponent(typeof(Renderer))]
public sealed class Blinking : MonoBehaviour
{
    [SerializeField] private Texture blinkTexture;
    [SerializeField] private Renderer[] hideWhileBlinking = new Renderer[0];
    [Tooltip("Shortest and longest wait between blinks, in seconds.")]
    [SerializeField] private Vector2 interval = new Vector2(1f, 2f);
    [SerializeField, Range(0.02f, 0.5f)] private float blinkDuration = 0.12f;

    private static readonly int MainTex = Shader.PropertyToID("_MainTex");
    private Renderer target;
    private MaterialPropertyBlock block;
    private float nextChange;
    private bool blinking;

    private void Awake()
    {
        target = GetComponent<Renderer>();
        block = new MaterialPropertyBlock();
        ScheduleNextBlink();
    }

    private void OnDisable()
    {
        if (blinking) SetBlinking(false);
    }

    private void Update()
    {
        if (Time.time < nextChange) return;
        if (blinking)
        {
            SetBlinking(false);
            ScheduleNextBlink();
        }
        else
        {
            SetBlinking(true);
            nextChange = Time.time + blinkDuration;
        }
    }

    private void ScheduleNextBlink()
    {
        nextChange = Time.time + Random.Range(Mathf.Min(interval.x, interval.y), Mathf.Max(interval.x, interval.y));
    }

    private void SetBlinking(bool value)
    {
        blinking = value;
        target.GetPropertyBlock(block);
        if (value && blinkTexture != null) block.SetTexture(MainTex, blinkTexture);
        else block.Clear();
        target.SetPropertyBlock(block);

        foreach (Renderer r in hideWhileBlinking)
            if (r != null) r.enabled = !value;
    }
}
