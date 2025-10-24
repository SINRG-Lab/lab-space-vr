#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class FindKeywordSpaceMismatches
{
    [MenuItem("Tools/Rendering/Find Keyword-Space Mismatches (Open Scenes)")]
    static void Run()
    {
        int hits = 0;
        // Scan all renderers in all open scenes
        for (int s = 0; s < SceneManager.sceneCount; s++)
        {
            var scene = SceneManager.GetSceneAt(s);
            if (!scene.isLoaded) continue;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                {
                    var mats = r.sharedMaterials.Where(m => m != null);
                    foreach (var m in mats)
                    {
                        var sh = m.shader;
                        if (sh == null) continue;

                        bool looksMeta = sh.name.StartsWith("Meta/");
                        bool looksURP  = sh.name.StartsWith("Universal Render Pipeline/");

                        // Suspicious: this material says URP but carries many Meta-style keywords,
                        // or it’s Meta while other slots on the same renderer are URP (mix on one renderer).
                        bool mixedOnRenderer = r.sharedMaterials.Any(x => x && x.shader && x.shader.name.StartsWith("Meta/")) &&
                                               r.sharedMaterials.Any(x => x && x.shader && x.shader.name.StartsWith("Universal Render Pipeline/"));

                        if (mixedOnRenderer || looksMeta) // both are worth flagging
                        {
                            hits++;
                            Debug.Log($"[KeywordSpace suspect] Renderer: {GetPath(r.transform)}  Material: {m.name}  Shader: {sh.name}",
                                r.gameObject);
                        }
                    }
                }
            }
        }

        // Also list material assets by shader family (helps catch flipped assets living in Project)
        int metaAssets = 0, urpAssets = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Material"))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var mat  = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat || !mat.shader) continue;
            if (mat.shader.name.StartsWith("Meta/")) { metaAssets++; Debug.Log($"[Project:Meta] {mat.name} @ {path}", mat); }
            if (mat.shader.name.StartsWith("Universal Render Pipeline/")) { urpAssets++; }
        }

        Debug.Log($"Scan done. Scene suspects: {hits}. Project Meta mats: {metaAssets}, URP mats: {urpAssets}.");
    }

    static string GetPath(Transform t)
    {
        string p = t.name;
        while (t.parent) { t = t.parent; p = t.name + "/" + p; }
        return p;
    }
}
#endif
