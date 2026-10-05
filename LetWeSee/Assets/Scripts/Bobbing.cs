using UnityEngine;

// Gently bobs an object up and down, like an item waiting to be picked up.
// Adds its offset on top of the current position, so other scripts can still move the object.
public sealed class Bobbing : MonoBehaviour
{
    [Tooltip("How far up and down it moves from its resting spot, in world units.")]
    [SerializeField, Range(0f, 2f)] private float height = 0.12f;
    [Tooltip("Bobs per second.")]
    [SerializeField, Range(0.05f, 5f)] private float speed = 0.6f;

    private Vector3 appliedOffset;
    private float phase;

    private void OnEnable()
    {
        phase = Random.value * Mathf.PI * 2f;
    }

    private void OnDisable()
    {
        transform.position -= appliedOffset;
        appliedOffset = Vector3.zero;
    }

    private void Update()
    {
        Vector3 offset = Vector3.up * (Mathf.Sin(Time.time * speed * Mathf.PI * 2f + phase) * height);
        transform.position += offset - appliedOffset;
        appliedOffset = offset;
    }
}
