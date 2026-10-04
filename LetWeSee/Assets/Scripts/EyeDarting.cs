using UnityEngine;

// Small, quick eye movements: every so often the object darts to a new spot near where it started.
public sealed class EyeDarting : MonoBehaviour
{
    [Tooltip("How far from its starting spot it can dart, in local units.")]
    [SerializeField, Range(0f, 0.5f)] private float range = 0.03f;
    [Tooltip("Shortest and longest wait between darts, in seconds.")]
    [SerializeField] private Vector2 interval = new Vector2(0.15f, 0.6f);
    [SerializeField, Range(1f, 60f)] private float speed = 25f;

    private Vector3 rest;
    private Vector3 target;
    private float nextDart;

    private void Awake()
    {
        rest = transform.localPosition;
        target = rest;
    }

    private void Update()
    {
        if (Time.time >= nextDart)
        {
            Vector2 offset = Random.insideUnitCircle * range;
            target = rest + new Vector3(offset.x, offset.y, 0f);
            nextDart = Time.time + Random.Range(Mathf.Min(interval.x, interval.y), Mathf.Max(interval.x, interval.y));
        }
        transform.localPosition = Vector3.Lerp(transform.localPosition, target, Time.deltaTime * speed);
    }
}
