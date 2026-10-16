using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// ふしぎな空間の床: パステル色の市松模様の板 (タイル) を丸く並べた、宙に浮かぶ床。崩れない。
    /// 色ごとに1つのメッシュ (描く回数 2回)。
    /// </summary>
    public class WonderFloor : MonoBehaviour
    {
        [Tooltip("市松の2色の材質")]
        public Material lightMaterial, darkMaterial;
        [Tooltip("床の半径 / タイル1枚の大きさ / 厚み (m)")]
        public float radius = 22f;
        public float tileSize = 1.5f;
        public float thickness = 0.3f;

        struct Tile
        {
            public Vector3 home;
            public int set, first;
        }

        Tile[] tiles;
        Mesh[] meshes;
        Vector3[][] verts, norms;
        Vector3[] localVerts, localNorms; // タイル1枚の形 (24 頂点。どのタイルも同じ)

        void Awake() { if (tiles == null) Build(); }

        void Pose(int i, float scale)
        {
            var tile = tiles[i];
            var v = verts[tile.set];
            var n = norms[tile.set];
            for (int k = 0; k < 24; k++)
            {
                v[tile.first + k] = tile.home + localVerts[k] * scale;
                n[tile.first + k] = localNorms[k];
            }
        }

        void Apply()
        {
            for (int s = 0; s < 2; s++)
            {
                meshes[s].vertices = verts[s];
                meshes[s].normals = norms[s];
            }
        }

        // タイル (上面・下面・4つの側面の箱) を丸く並べた、色ごとのメッシュを作る
        void Build()
        {
            float h = tileSize * 0.5f - 0.01f, d = thickness;
            localVerts = new Vector3[24];
            localNorms = new Vector3[24];
            var faces = new[]
            {
                (Vector3.up, new[] { new Vector3(-h, 0f, -h), new Vector3(-h, 0f, h), new Vector3(h, 0f, h), new Vector3(h, 0f, -h) }),
                (Vector3.down, new[] { new Vector3(-h, -d, -h), new Vector3(h, -d, -h), new Vector3(h, -d, h), new Vector3(-h, -d, h) }),
                (Vector3.forward, new[] { new Vector3(-h, -d, h), new Vector3(h, -d, h), new Vector3(h, 0f, h), new Vector3(-h, 0f, h) }),
                (Vector3.back, new[] { new Vector3(h, -d, -h), new Vector3(-h, -d, -h), new Vector3(-h, 0f, -h), new Vector3(h, 0f, -h) }),
                (Vector3.right, new[] { new Vector3(h, -d, h), new Vector3(h, -d, -h), new Vector3(h, 0f, -h), new Vector3(h, 0f, h) }),
                (Vector3.left, new[] { new Vector3(-h, -d, -h), new Vector3(-h, -d, h), new Vector3(-h, 0f, h), new Vector3(-h, 0f, -h) }),
            };
            for (int f = 0; f < 6; f++)
                for (int k = 0; k < 4; k++)
                {
                    localVerts[f * 4 + k] = faces[f].Item2[k];
                    localNorms[f * 4 + k] = faces[f].Item1;
                }

            var list = new System.Collections.Generic.List<Tile>();
            int[] count = new int[2];
            int n = Mathf.CeilToInt(radius / tileSize);
            for (int ix = -n; ix <= n; ix++)
                for (int iz = -n; iz <= n; iz++)
                {
                    var home = new Vector3(ix * tileSize, 0f, iz * tileSize);
                    float dist = new Vector2(home.x, home.z).magnitude;
                    if (dist > radius) continue;
                    int set = (ix + iz) & 1;
                    list.Add(new Tile { home = home, set = set, first = count[set] * 24 });
                    count[set]++;
                }
            tiles = list.ToArray();

            meshes = new Mesh[2];
            verts = new Vector3[2][];
            norms = new Vector3[2][];
            var mats = new[] { lightMaterial, darkMaterial };
            for (int s = 0; s < 2; s++)
            {
                verts[s] = new Vector3[count[s] * 24];
                norms[s] = new Vector3[count[s] * 24];
                var tris = new int[count[s] * 36];
                for (int t = 0; t < count[s]; t++)
                    for (int f = 0; f < 6; f++)
                    {
                        int v = t * 24 + f * 4, o = t * 36 + f * 6;
                        tris[o] = v; tris[o + 1] = v + 1; tris[o + 2] = v + 2;
                        tris[o + 3] = v; tris[o + 4] = v + 2; tris[o + 5] = v + 3;
                    }
                var mesh = new Mesh { name = s == 0 ? "WonderFloorLight" : "WonderFloorDark" };
                mesh.MarkDynamic();
                mesh.vertices = verts[s];
                mesh.triangles = tris;
                mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 200f);
                meshes[s] = mesh;
                var go = new GameObject(mesh.name);
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mats[s];
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            for (int i = 0; i < tiles.Length; i++) Pose(i, 1f);
            Apply();
        }
    }
}
