using UnityEngine;

// Draws this object after the listed objects (and their children), so with the Mesh2D-Lit-OnTop shader
// it shows through them instead of being cut off where it clips into them.
[ExecuteAlways]
[RequireComponent(typeof(Renderer))]
public sealed class ShowInFrontOf : MonoBehaviour
{
    [SerializeField] private GameObject[] objects = new GameObject[0];

    private void OnEnable() => Apply();
    private void OnValidate() => Apply();

    private void Apply()
    {
        var self = GetComponent<Renderer>();
        int highest = int.MinValue;
        int layer = self.sortingLayerID;
        foreach (GameObject target in objects)
        {
            if (target == null) continue;
            foreach (Renderer r in target.GetComponentsInChildren<Renderer>(true))
            {
                if (r == self) continue;
                if (r.sortingOrder >= highest)
                {
                    highest = r.sortingOrder;
                    layer = r.sortingLayerID;
                }
            }
        }
        if (highest == int.MinValue) return;

        self.sortingLayerID = layer;
        self.sortingOrder = highest + 1;
    }
}
