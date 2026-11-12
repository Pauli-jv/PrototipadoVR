using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Meta.XR.BuildingBlocks.Editor;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class ConfigureCollectionInteractions
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private const string BlockFolder = "Packages/com.meta.xr.sdk.interaction.ovr/Editor/Blocks/Interactables/";
    private static readonly string[] Ingredients = { "Yuca", "Papa", "Cilantro", "Mazorca", "Carne", "Platano" };
    public static string Status { get; private set; } = "Not started";

    public static async Task<string> Configure()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (Application.isPlaying || !scene.path.EndsWith("Terreno_Dev.unity")) throw new Exception("Open Terreno_Dev in Edit Mode.");
        var root = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single(t => t.name == "---Interactive---");
        var targets = new List<Transform>();
        foreach (Transform child in root)
        {
            if (child.GetComponent<Grabbable>() || child.name == "papa (58)") targets.Add(child);
        }
        var kitchen = root.Cast<Transform>().Single(t => t.name == "--- Cocina ---");
        foreach (Transform child in kitchen)
        {
            if (child.name == "CuchilloCocina" || child.name.StartsWith("Olla")) continue;
            if (child.name == "Cilantro") targets.Add(child);
            else targets.AddRange(child.GetComponentsInChildren<MeshRenderer>(true).Select(r => r.transform));
        }
        targets = targets.Distinct().ToList();
        var block = AssetDatabase.LoadAssetAtPath<InterfaceBlockData>(BlockFolder + "HandGrab.asset");
        var routine = AssetDatabase.LoadAssetAtPath<ScriptableObject>(BlockFolder + "InstallationRoutines/HandGrabInstallationRoutine.asset");
        var selectedRoutine = typeof(InterfaceBlockData).GetField("_selectedRoutine", Hidden);
        var install = typeof(BlockData).GetMethod("InstallAsync", Hidden, null, new[] { typeof(GameObject) }, null);
        var material = CreateGlowMaterial();
        int count = 0;
        try
        {
            selectedRoutine.SetValue(block, routine);
            foreach (var target in targets)
            {
                Status = "Configuring " + target.name;
                Undo.RegisterFullObjectHierarchyUndo(target.gameObject, "Configure collection grab");
                RemoveDummyMeshes(target);
                EnsureColliders(target);
                if (!target.GetComponentInChildren<HandGrabInteractable>(true))
                    await (Task<List<GameObject>>)install.Invoke(block, new object[] { target.gameObject });
                var grabbable = target.GetComponent<Grabbable>();
                if (!grabbable) throw new Exception("Building Block did not add Grabbable: " + target.name);
                var body = target.GetComponent<Rigidbody>();
                body.isKinematic = true;
                body.useGravity = false;
                string ingredient = IngredientOf(target);
                SnapInteractor snap = null;
                if (ingredient != null)
                {
                    var snapTransform = target.Find("SnapInteractor");
                    if (!snapTransform)
                    {
                        snapTransform = new GameObject("SnapInteractor").transform;
                        Undo.RegisterCreatedObjectUndo(snapTransform.gameObject, "Create ingredient snap");
                        snapTransform.SetParent(target, false);
                    }
                    snapTransform.position = BoundsOf(target).center;
                    snap = snapTransform.GetComponent<SnapInteractor>() ?? Undo.AddComponent<SnapInteractor>(snapTransform.gameObject);
                    snap.InjectAllSnapInteractor(grabbable, body);
                    snap.DistanceThreshold = 0.01f;
                    var tags = snapTransform.GetComponent<TagSet>() ?? Undo.AddComponent<TagSet>(snapTransform.gameObject);
                    tags.InjectOptionalTags(new List<string> { "Ingredient", ingredient });
                    EditorUtility.SetDirty(tags);
                    EditorUtility.SetDirty(snap);
                }
                var glow = target.GetComponent<InteractableGlow>() ?? Undo.AddComponent<InteractableGlow>(target.gameObject);
                glow.outlineMaterial = material;
                glow.grabbable = grabbable;
                glow.snapInteractor = snap;
                glow.handGrabs = target.GetComponentsInChildren<HandGrabInteractable>(true);
                glow.controllerGrabs = target.GetComponentsInChildren<GrabInteractable>(true);
                EditorUtility.SetDirty(glow);
                count++;
            }
        }
        finally { selectedRoutine.SetValue(block, null); }

        var baskets = root.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.name.StartsWith("canasta cafetera")).ToArray();
        foreach (var basket in baskets) ConfigureBasket(basket);
        ConfigureBloom();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Status = "Complete: " + count + " hand-grab objects, " + baskets.Length + " fixed baskets, filtered snap slots and glow.";
        return Status;
    }

    private static void ConfigureBasket(MeshRenderer basket)
    {
        // No Grab components or demo geometry are added to a basket.
        if (basket.GetComponentInParent<Grabbable>()) throw new Exception("Basket must remain fixed: " + basket.name);
        if (!basket.TryGetComponent<MeshCollider>(out var collider)) collider = Undo.AddComponent<MeshCollider>(basket.gameObject);
        collider.sharedMesh = basket.GetComponent<MeshFilter>().sharedMesh;
        collider.convex = false;
        string category = basket.name.Contains("(1)") ? "Yuca" : basket.name.Contains("(2)") ? "Mazorca" :
            basket.name.Contains("(3)") ? "Platano" : basket.name.Contains("(4)") ? "Carne" :
            basket.name.Contains("(5)") ? "Papa" : basket.name.Contains("(6)") ? "Cilantro" : "Ingredient";
        var bounds = basket.bounds;
        var holder = basket.transform.Find("IngredientSnapSlots");
        if (holder) return;
        holder = new GameObject("IngredientSnapSlots").transform;
        Undo.RegisterCreatedObjectUndo(holder.gameObject, "Create basket snap slots");
        holder.SetParent(basket.transform, true);
        for (int i = 0; i < 6; i++)
        {
            var slot = new GameObject("Snap_" + category + "_" + (i + 1));
            slot.transform.SetParent(holder, false);
            slot.transform.position = new Vector3(bounds.center.x + (i % 3 - 1) * bounds.size.x * 0.23f,
                bounds.min.y + bounds.size.y * 0.42f, bounds.center.z + (i / 3 - 0.5f) * bounds.size.z * 0.3f);
            slot.transform.rotation = Quaternion.identity;
            var body = slot.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            var trigger = slot.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 0.22f;
            var filter = slot.AddComponent<TagSetFilter>();
            filter.InjectOptionalRequireTags(new[] { "Ingredient", category }.Distinct().ToArray());
            var snap = slot.AddComponent<SnapInteractable>();
            snap.InjectAllSnapInteractable(body);
            snap.MaxSelectingInteractors = 1;
            snap.InjectOptionalInteractorFilters(new List<IGameObjectFilter> { filter });
        }
    }

    private static string IngredientOf(Transform item)
    {
        for (var current = item; current; current = current.parent)
        {
            string name = current.name.ToLowerInvariant().Replace("á", "a");
            foreach (string ingredient in Ingredients)
                if (name.Contains(ingredient.ToLowerInvariant())) return ingredient;
            if (current.name == "---Interactive---") break;
        }
        return null;
    }

    private static void EnsureColliders(Transform target)
    {
        foreach (var filter in target.GetComponentsInChildren<MeshFilter>(true))
        {
            if (!filter.sharedMesh || filter.GetComponent<Collider>()) continue;
            if (!filter.TryGetComponent<MeshRenderer>(out _)) continue;
            var collider = Undo.AddComponent<BoxCollider>(filter.gameObject);
            collider.center = filter.sharedMesh.bounds.center;
            collider.size = filter.sharedMesh.bounds.size;
        }
    }

    private static void RemoveDummyMeshes(Transform root)
    {
        foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (!filter.sharedMesh || AssetDatabase.GetAssetPath(filter.sharedMesh) != "Library/unity default resources") continue;
            if (filter.TryGetComponent<MeshRenderer>(out var renderer)) Undo.DestroyObjectImmediate(renderer);
            Undo.DestroyObjectImmediate(filter);
        }
    }

    private static Bounds BoundsOf(Transform root)
    {
        var renderers = root.GetComponentsInChildren<MeshRenderer>(true);
        var bounds = renderers[0].bounds;
        foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
        return bounds;
    }

    private static Material CreateGlowMaterial()
    {
        const string path = "Assets/Main/Desarollo/Art/InteractableGlow.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!material)
        {
            material = new Material(Shader.Find("Project/InteractableGlow"));
            material.SetColor("_GlowColor", new Color(0.15f, 2.8f, 3.6f, 1f));
            material.SetFloat("_Width", 0.004f);
            AssetDatabase.CreateAsset(material, path);
        }
        return material;
    }

    private static void ConfigureBloom()
    {
        // Add only Bloom to a separate profile; preserve the existing scene grading.
        const string path = "Assets/Main/Desarollo/Art/InteractionGlowVolume.asset";
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        if (!profile) { profile = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(profile, path); }
        if (!profile.TryGet<Bloom>(out var bloom))
        {
            bloom = profile.Add<Bloom>();
            AssetDatabase.AddObjectToAsset(bloom, profile);
        }
        bloom.threshold.Override(1.1f);
        bloom.intensity.Override(0.35f);
        bloom.scatter.Override(0.55f);
        bloom.highQualityFiltering.Override(false);
        var volumeObject = GameObject.Find("InteractionGlowVolume") ?? new GameObject("InteractionGlowVolume");
        var volume = volumeObject.GetComponent<Volume>() ?? Undo.AddComponent<Volume>(volumeObject);
        volume.isGlobal = true;
        volume.priority = 10;
        volume.sharedProfile = profile;
        var rig = UnityEngine.Object.FindFirstObjectByType<OVRCameraRig>();
        foreach (var camera in rig.GetComponentsInChildren<Camera>(true))
        {
            Undo.RecordObject(camera, "Enable interaction glow");
            camera.allowHDR = true;
            var data = camera.GetUniversalAdditionalCameraData();
            Undo.RecordObject(data, "Enable interaction glow");
            data.renderPostProcessing = true;
            data.volumeLayerMask |= 1 << volumeObject.layer;
        }
        EditorUtility.SetDirty(profile);
        EditorUtility.SetDirty(bloom);
    }
}
