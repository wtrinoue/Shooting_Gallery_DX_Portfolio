using System.Collections.Generic;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// 本物の立体の世界を、その場で灰色にする (Unknown ステージの「世界が止まる」演出。写真を貼るのと違って、奥行き・両目の立体感がそのまま残る)。
    /// 見えている物の材質を、色だけ灰色にした複製に差し替える (複製は初めの1回だけ作って使い回す)。3D の文字の色と、明かりの色も灰色にする。
    /// Set(k) で 1 = 灰色 ⇔ 0 = 元の色 の間をなめらかに変えられる (色が戻る演出)。Restore で元の材質に戻す。
    /// GrayMaterials で、割れる部品 (WorldShatter) にも同じ灰色の材質を渡す。
    /// 後処理エフェクトは使わない (VR・WebGL で重いので)。テクスチャの色は残る (このゲームの物はほとんど単色なので目立たない)
    /// </summary>
    public class GrayWorld
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        class Entry
        {
            public Material copy;
            public bool hasBase, hasColor, hasEmission;
            public Color baseColor, color, emission;
        }

        readonly Dictionary<Material, Entry> cache = new Dictionary<Material, Entry>();
        readonly List<Entry> used = new List<Entry>();
        readonly HashSet<Entry> usedSet = new HashSet<Entry>();
        readonly List<Renderer> renderers = new List<Renderer>();
        readonly List<Material[]> originals = new List<Material[]>();
        readonly List<TextMesh> texts = new List<TextMesh>();
        readonly List<Color> textColors = new List<Color>();
        readonly List<Light> lights = new List<Light>();
        readonly List<Color> lightColors = new List<Color>();

        /// <summary>灰色の色合い (少しだけ青み)</summary>
        public Color tint = new Color(0.98f, 0.99f, 1.03f);
        public bool Active { get; private set; }

        /// <summary>root の下の見えている物を灰色にする (exclude の下は除く: プレイヤー・HUD・演出そのもの)</summary>
        public void Apply(Transform root, ICollection<Transform> exclude)
        {
            if (Active) Restore();
            Active = true;
            used.Clear();
            usedSet.Clear();
            foreach (var r in root.GetComponentsInChildren<Renderer>(false))
            {
                if (Excluded(r.transform, root, exclude)) continue;
                var mats = r.sharedMaterials;
                renderers.Add(r);
                originals.Add(mats);
                r.sharedMaterials = GrayMaterials(mats);
            }
            foreach (var t in root.GetComponentsInChildren<TextMesh>(false))
            {
                if (Excluded(t.transform, root, exclude)) continue;
                texts.Add(t);
                textColors.Add(t.color);
            }
            foreach (var l in root.GetComponentsInChildren<Light>(false))
            {
                if (Excluded(l.transform, root, exclude)) continue;
                lights.Add(l);
                lightColors.Add(l.color);
            }
            Set(1f);
        }

        /// <summary>材質の灰色の複製 (割れる部品にも使う)。渡した時点で灰色にしてある</summary>
        public Material[] GrayMaterials(Material[] src)
        {
            var gray = new Material[src.Length];
            for (int i = 0; i < src.Length; i++)
            {
                var e = EntryOf(src[i]);
                if (e == null) continue;
                SetEntry(e, 1f);
                gray[i] = e.copy;
            }
            return gray;
        }

        /// <summary>k = 1 で灰色、0 で元の色</summary>
        public void Set(float k)
        {
            if (!Active) return;
            foreach (var e in used) SetEntry(e, k);
            for (int i = 0; i < texts.Count; i++) if (texts[i] != null) texts[i].color = Color.Lerp(textColors[i], Gray(textColors[i]), k);
            for (int i = 0; i < lights.Count; i++) if (lights[i] != null) lights[i].color = Color.Lerp(lightColors[i], Gray(lightColors[i]), k);
        }

        /// <summary>元の材質・文字の色・明かりの色に戻す</summary>
        public void Restore()
        {
            for (int i = 0; i < renderers.Count; i++) if (renderers[i] != null) renderers[i].sharedMaterials = originals[i];
            for (int i = 0; i < texts.Count; i++) if (texts[i] != null) texts[i].color = textColors[i];
            for (int i = 0; i < lights.Count; i++) if (lights[i] != null) lights[i].color = lightColors[i];
            renderers.Clear();
            originals.Clear();
            texts.Clear();
            textColors.Clear();
            lights.Clear();
            lightColors.Clear();
            Active = false;
        }

        public Color Gray(Color c)
        {
            float l = c.r * 0.299f + c.g * 0.587f + c.b * 0.114f;
            return new Color(l * tint.r, l * tint.g, l * tint.b, c.a);
        }

        void SetEntry(Entry e, float k)
        {
            if (e.copy == null) return;
            if (e.hasBase) e.copy.SetColor(BaseColorId, Color.Lerp(e.baseColor, Gray(e.baseColor), k));
            if (e.hasColor) e.copy.SetColor(ColorId, Color.Lerp(e.color, Gray(e.color), k));
            if (e.hasEmission) e.copy.SetColor(EmissionId, Color.Lerp(e.emission, Gray(e.emission), k));
        }

        Entry EntryOf(Material m)
        {
            if (m == null) return null;
            if (!cache.TryGetValue(m, out var e))
            {
                e = new Entry { copy = new Material(m) { name = m.name + "_Gray" } };
                e.hasBase = m.HasProperty(BaseColorId);
                e.hasColor = m.HasProperty(ColorId);
                e.hasEmission = m.HasProperty(EmissionId);
                if (e.hasBase) e.baseColor = m.GetColor(BaseColorId);
                if (e.hasColor) e.color = m.GetColor(ColorId);
                if (e.hasEmission) e.emission = m.GetColor(EmissionId);
                cache.Add(m, e);
            }
            if (usedSet.Add(e)) used.Add(e);
            return e;
        }

        static bool Excluded(Transform t, Transform root, ICollection<Transform> exclude)
        {
            if (exclude == null) return false;
            for (var p = t; p != null && p != root; p = p.parent)
                if (exclude.Contains(p)) return true;
            return false;
        }
    }
}
