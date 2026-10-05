using System.Collections.Generic;
using UnityEngine;

// Builds a grid mesh of wall tiles, picking one of the tile sprites at random for each cell.
// Each sprite gets its own submesh, so the renderer needs one material slot per sprite.
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public sealed class TiledWall : MonoBehaviour
{
    [SerializeField] private Sprite[] tiles = new Sprite[0];
    [SerializeField] private Vector2Int tileCount = new Vector2Int(12, 9);
    [Tooltip("Change to get a different random layout. The same seed always gives the same wall.")]
    [SerializeField] private int seed = 1;
    [SerializeField] private Color tint = Color.white;
    [Tooltip("Gives every tile a slightly different shade of the tint.")]
    [SerializeField] private bool randomizeTint;
    [Tooltip("How much each tile's shade can differ when randomizing.")]
    [SerializeField, Range(0f, 0.5f)] private float tintVariation = 0.08f;

    private Mesh mesh;
    private MaterialPropertyBlock block;
    private static readonly int MainTex = Shader.PropertyToID("_MainTex");

    private void OnEnable() => Build();
    private void OnValidate() => Build();

    private void OnDisable()
    {
        if (mesh == null) return;
        if (Application.isPlaying) Destroy(mesh);
        else DestroyImmediate(mesh);
        mesh = null;
    }

    private void Build()
    {
        if (tiles == null || tiles.Length == 0 || tiles[0] == null) return;
        int columns = Mathf.Max(1, tileCount.x);
        int rows = Mathf.Max(1, tileCount.y);

        if (mesh == null)
            mesh = new Mesh { name = "TiledWall", hideFlags = HideFlags.HideAndDontSave };
        mesh.Clear();

        Vector2 size = tiles[0].bounds.size;
        Vector2 origin = new Vector2(-columns * size.x * 0.5f, -rows * size.y * 0.5f);
        var vertices = new List<Vector3>();
        var uvs = new List<Vector2>();
        var colors = new List<Color>();
        var triangles = new List<int>[tiles.Length];
        for (int i = 0; i < tiles.Length; i++) triangles[i] = new List<int>();

        var random = new System.Random(seed);
        // Separate stream so turning tint randomizing on or off doesn't reshuffle the tiles.
        var tintRandom = new System.Random(seed + 7919);
        for (int y = 0; y < rows; y++)
        for (int x = 0; x < columns; x++)
        {
            int pick = random.Next(tiles.Length);
            Sprite sprite = tiles[pick] != null ? tiles[pick] : tiles[0];
            Rect uv = UvRect(sprite);
            Vector2 min = origin + new Vector2(x * size.x, y * size.y);

            int start = vertices.Count;
            vertices.Add(new Vector3(min.x, min.y, 0f));
            vertices.Add(new Vector3(min.x, min.y + size.y, 0f));
            vertices.Add(new Vector3(min.x + size.x, min.y + size.y, 0f));
            vertices.Add(new Vector3(min.x + size.x, min.y, 0f));
            uvs.Add(new Vector2(uv.xMin, uv.yMin));
            uvs.Add(new Vector2(uv.xMin, uv.yMax));
            uvs.Add(new Vector2(uv.xMax, uv.yMax));
            uvs.Add(new Vector2(uv.xMax, uv.yMin));
            Color tileColor = randomizeTint ? VaryTint(tintRandom) : tint;
            for (int c = 0; c < 4; c++) colors.Add(tileColor);

            List<int> tris = triangles[tiles[pick] != null ? pick : 0];
            tris.Add(start); tris.Add(start + 1); tris.Add(start + 2);
            tris.Add(start); tris.Add(start + 2); tris.Add(start + 3);
        }

        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetColors(colors);
        mesh.subMeshCount = tiles.Length;
        for (int i = 0; i < tiles.Length; i++) mesh.SetTriangles(triangles[i], i);
        mesh.RecalculateBounds();
        GetComponent<MeshFilter>().sharedMesh = mesh;

        // Point each material slot at its sprite's texture without touching the shared material.
        var meshRenderer = GetComponent<MeshRenderer>();
        if (block == null) block = new MaterialPropertyBlock();
        for (int i = 0; i < tiles.Length && i < meshRenderer.sharedMaterials.Length; i++)
        {
            if (tiles[i] == null) continue;
            block.Clear();
            block.SetTexture(MainTex, tiles[i].texture);
            meshRenderer.SetPropertyBlock(block, i);
        }
    }

    // Mostly a lighter or darker shade of the tint, with a little colour drift per channel.
    private Color VaryTint(System.Random random)
    {
        float Range() => (float)(random.NextDouble() * 2.0 - 1.0) * tintVariation;
        float shade = 1f + Range();
        return new Color(
            Mathf.Clamp01(tint.r * (shade + Range() * 0.3f)),
            Mathf.Clamp01(tint.g * (shade + Range() * 0.3f)),
            Mathf.Clamp01(tint.b * (shade + Range() * 0.3f)),
            tint.a);
    }

    private static Rect UvRect(Sprite sprite)
    {
        Rect r = sprite.textureRect;
        float w = sprite.texture.width;
        float h = sprite.texture.height;
        return new Rect(r.x / w, r.y / h, r.width / w, r.height / h);
    }
}
