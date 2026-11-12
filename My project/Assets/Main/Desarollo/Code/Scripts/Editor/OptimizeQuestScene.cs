using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class OptimizeQuestScene
{
    public static string Apply()
    {
        if (Application.isPlaying) throw new System.InvalidOperationException("Stop Play Mode");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var mobile = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/Mobile_RPAsset.asset");
        mobile.renderScale = 1f;
        mobile.msaaSampleCount = 4;
        mobile.supportsHDR = false;
        mobile.supportsCameraDepthTexture = false;
        mobile.supportsCameraOpaqueTexture = false;
        mobile.shadowDistance = 25f;
        mobile.shadowCascadeCount = 1;
        var mobileSettings = new SerializedObject(mobile);
        mobileSettings.FindProperty("m_SoftShadowsSupported").boolValue = false;
        mobileSettings.FindProperty("m_AdditionalLightsRenderingMode").intValue = 0;
        mobileSettings.FindProperty("m_AdditionalLightShadowsSupported").boolValue = false;
        mobileSettings.ApplyModifiedPropertiesWithoutUndo();
        mobile.useSRPBatcher = true;
        EditorUtility.SetDirty(mobile);
        QualitySettings.SetQualityLevel(0, true);
        QualitySettings.renderPipeline = mobile;
        QualitySettings.vSyncCount = 0;
        PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
        var roots = scene.GetRootGameObjects();
        var rig = Object.FindFirstObjectByType<OVRCameraRig>();
        var cooking = Object.FindFirstObjectByType<CookingSequence>();
        var perf = Object.FindFirstObjectByType<QuestStandalonePerformance>();
        if (!perf) perf = new GameObject("QuestStandalonePerformance").AddComponent<QuestStandalonePerformance>();
        perf.playerCamera = rig.centerEyeAnchor.GetComponent<Camera>();
        var renderers = roots.SelectMany(r => r.GetComponentsInChildren<MeshRenderer>(true)).ToArray();
        perf.distantDecoration = renderers.Where(r =>
            !r.transform.IsChildOf(rig.transform) && !r.transform.IsChildOf(cooking.kitchen.transform) &&
            !r.GetComponentInParent<Canvas>() && !r.GetComponentInParent<Oculus.Interaction.Grabbable>() &&
            !r.GetComponentInParent<MacheteHarvestTarget>() && !r.GetComponentInParent<Animator>() &&
            !r.GetComponentInParent<Rigidbody>() && r.bounds.size.magnitude < 20f).Cast<Renderer>().ToArray();
        foreach (var renderer in perf.distantDecoration)
        {
            var flags = GameObjectUtility.GetStaticEditorFlags(renderer.gameObject);
            GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, flags | StaticEditorFlags.BatchingStatic);
        }
        foreach (var terrain in roots.SelectMany(r => r.GetComponentsInChildren<Terrain>(true)))
        {
            terrain.drawInstanced = true;
            terrain.heightmapPixelError = 12f;
            terrain.detailObjectDistance = 30f;
            terrain.detailObjectDensity = 0.5f;
            terrain.basemapDistance = 80f;
            terrain.treeDistance = 120f;
            EditorUtility.SetDirty(terrain);
        }
        var journal = Object.FindFirstObjectByType<JourneyPanel>();
        journal.cachedMap = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Main/Desarollo/Art/MapIcons/TerrainMapPreview.png");
        journal.mapCamera.enabled = false;
        journal.mapCamera.allowHDR = false;
        journal.mapCamera.stereoTargetEye = StereoTargetEyeMask.None;
        if (journal.mapCamera.TryGetComponent<UniversalAdditionalCameraData>(out var mapData))
        { mapData.renderShadows = false; mapData.renderPostProcessing = false; }
        int textures = 0;
        var dependencies = AssetDatabase.GetDependencies(scene.path, true);
        foreach (var path in dependencies.Where(p => p.StartsWith("Assets/")))
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (!importer || importer.textureShape != TextureImporterShape.Texture2D || importer.textureType == TextureImporterType.Lightmap) continue;
            var settings = importer.GetPlatformTextureSettings("Android");
            settings.overridden = true;
            settings.maxTextureSize = Mathf.Min(importer.maxTextureSize, path.Contains("/UI/") ? 2048 : 1024);
            settings.format = path.Contains("/UI/") ? TextureImporterFormat.ASTC_4x4 : TextureImporterFormat.ASTC_6x6;
            settings.compressionQuality = 50;
            importer.SetPlatformTextureSettings(settings);
            importer.SaveAndReimport();
            textures++;
        }
        EditorUtility.SetDirty(journal);
        EditorUtility.SetDirty(perf);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        return $"Mobile 1.0x, MSAA4, Vulkan ARM64, {textures} Android ASTC textures, {perf.distantDecoration.Length} distance-culled static renderers, cached map, terrain optimized.";
    }
}
