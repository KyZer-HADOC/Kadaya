#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// One-time automatic URP setup for KADAYA.
// Opens with the project, creates the URP + Renderer assets, assigns them to Graphics/Quality settings
// and adds the shaders the procedural game needs to the build. Menu: KADAYA > Setup URP Graphics
[InitializeOnLoad]
public static class KadayaSetup
{
    const string Dir = "Assets/Settings";

    static readonly string[] ShaderNames =
    {
        "Universal Render Pipeline/Lit",
        "Universal Render Pipeline/Unlit",
        "Sprites/Default",
        "Mobile/Particles/Additive",
        "Legacy Shaders/Particles/Alpha Blended",
        "Skybox/Procedural",
        "Standard",
        "UI/Default"
    };

    static KadayaSetup()
    {
        EditorApplication.delayCall += AutoRun;
    }

    static void AutoRun()
    {
        if (EditorApplication.isPlayingOrWillEnterPlaymode) return;
        if (GraphicsSettings.defaultRenderPipeline == null) Setup();
        else AddShaders();
    }

    [MenuItem("KADAYA/Setup URP Graphics")]
    public static void Setup()
    {
        if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets", "Settings");

        var rpPath = Dir + "/Kadaya_URP.asset";
        var rp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(rpPath);
        if (rp == null)
        {
            var rd = ScriptableObject.CreateInstance<UniversalRendererData>();
            try
            {
                // loads the default post-processing resources the same way the Reset() menu does
                var reset = rd.GetType().GetMethod("Reset",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic);
                if (reset != null) reset.Invoke(rd, null);
            }
            catch (System.Exception) { }

            AssetDatabase.CreateAsset(rd, Dir + "/Kadaya_Renderer.asset");
            rp = UniversalRenderPipelineAsset.Create(rd);
            rp.supportsHDR = true;
            rp.msaaSampleCount = 4;
            rp.shadowDistance = 45f;
            AssetDatabase.CreateAsset(rp, rpPath);
        }

        GraphicsSettings.defaultRenderPipeline = rp;
        int prev = QualitySettings.GetQualityLevel();
        for (int i = 0; i < QualitySettings.names.Length; i++)
        {
            QualitySettings.SetQualityLevel(i, false);
            QualitySettings.renderPipeline = rp;
        }
        QualitySettings.SetQualityLevel(prev, false);

        AddShaders();
        AssetDatabase.SaveAssets();
        Debug.Log("[KADAYA] URP graphics set up. Press Play!");
    }

    static void AddShaders()
    {
        var gs = AssetDatabase.LoadAssetAtPath<Object>("ProjectSettings/GraphicsSettings.asset");
        if (gs == null) return;
        var so = new SerializedObject(gs);
        var arr = so.FindProperty("m_AlwaysIncludedShaders");
        if (arr == null) return;

        foreach (var n in ShaderNames)
        {
            var sh = Shader.Find(n);
            if (sh == null) continue;
            bool has = false;
            for (int i = 0; i < arr.arraySize; i++)
                if (arr.GetArrayElementAtIndex(i).objectReferenceValue == sh) { has = true; break; }
            if (!has)
            {
                arr.arraySize++;
                arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = sh;
            }
        }
        so.ApplyModifiedProperties();
    }
}
#endif
