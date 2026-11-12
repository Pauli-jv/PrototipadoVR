using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Meta.XR.BuildingBlocks.Editor;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using Oculus.Interaction.Input;
using Oculus.Interaction.PoseDetection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ConfigureHarvestSequence
{
    private const string Audio = "Assets/Main/Desarollo/Audio/Sonidos RV/";
    private const string Blocks = "Packages/com.meta.xr.sdk.interaction.ovr/Editor/Blocks/Interactables/";
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly string[] Names = { "Carne", "Mazorca", "Papa", "Platano", "Yuca", "Cilantro" };

    public static async Task<string> Configure()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (Application.isPlaying || !scene.path.EndsWith("Terreno_Dev.unity")) throw new Exception("Open Terreno_Dev in Edit Mode.");
        if (UnityEngine.Object.FindFirstObjectByType<CollectionProgress>()) throw new Exception("Harvest sequence already configured.");
        var all = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(t => t.gameObject.scene == scene).ToArray();
        var journal = UnityEngine.Object.FindFirstObjectByType<JourneyPanel>();
        var root = new GameObject("CollectionProgress");
        Undo.RegisterCreatedObjectUndo(root, "Configure harvest sequence");
        var progress = root.AddComponent<CollectionProgress>();
        progress.narration = root.AddComponent<AudioSource>();
        progress.narration.playOnAwake = false;
        var fx = new GameObject("CollectionEffects");
        fx.transform.SetParent(root.transform, false);
        progress.effects = fx.AddComponent<AudioSource>();
        progress.effects.playOnAwake = false;
        progress.notification = Clip("Sonidos Interactivos/Notification.wav");
        progress.collectionFinished = Clip("Guion/3 Fin de la recoleccion/Fin de recoleccion.mp3");
        progress.ingredients = Names.Select(name => new CollectionProgress.IngredientDialogue
        {
            ingredient = name,
            clips = new[] { Clip("Guion/2 Recoleccion de ingredientes/" + name + " 1.mp3"), Clip("Guion/2 Recoleccion de ingredientes/" + name + " 2.mp3") },
            checklist = journal.ingredientsPage.GetComponentsInChildren<TMP_Text>(true)
                .Single(t => t.text.Replace("á", "a").Equals(name, StringComparison.OrdinalIgnoreCase)).GetComponentInParent<UnityEngine.UI.Toggle>(true)
        }).ToArray();

        var banana = all.Single(t => t.name == "PlatanoInteractuable");
        var corn = all.Single(t => t.name == "MazorcaInteractuable");
        await AddGrab(banana.gameObject, false);
        await AddGrab(corn.gameObject, false);
        AddIngredientSetup(banana, "Platano");
        AddIngredientSetup(corn, "Mazorca");
        var meatSource = all.Single(t => t.name == "Carne_Entera");
        var meat = UnityEngine.Object.Instantiate(meatSource.gameObject);
        Undo.RegisterCreatedObjectUndo(meat, "Create harvest meat");
        meat.name = "CarneInteractuable";
        meat.transform.localScale = meatSource.lossyScale;
        meat.transform.SetPositionAndRotation(all.Single(t => t.name == "PositionForCarne").position + Vector3.up * 0.4f, meatSource.rotation);

        var items = UnityEngine.Object.FindObjectsByType<InteractableGlow>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(g => g.snapInteractor).ToArray();
        foreach (var glow in items)
        {
            await AddGrab(glow.gameObject, true);
            var item = glow.GetComponent<CollectibleIngredient>() ?? Undo.AddComponent<CollectibleIngredient>(glow.gameObject);
            item.ingredient = Names.Single(name => new SerializedObject(glow.snapInteractor.GetComponent<TagSet>()).FindProperty("_tags")
                .CastProperties().Contains(name));
            item.grabbable = glow.grabbable;
            item.snap = glow.snapInteractor;
            item.body = glow.GetComponent<Rigidbody>();
            item.grabInteractions = glow.GetComponentsInChildren<MonoBehaviour>(true).Where(IsGrabInteraction).Cast<Behaviour>().ToArray();
            item.grabbable.InjectOptionalRigidbody(item.body);
            item.grabbable.InjectOptionalKinematicWhileSelected(true);
            EditorUtility.SetDirty(item.grabbable);
            EditorUtility.SetDirty(item);
        }
        var bananaItem = banana.GetComponent<CollectibleIngredient>();
        var cornItem = corn.GetComponent<CollectibleIngredient>();
        var meatItem = meat.GetComponent<CollectibleIngredient>();
        foreach (var item in new[] { bananaItem, cornItem, meatItem })
        {
            item.Available = false;
            foreach (var grab in item.grabInteractions) grab.enabled = false;
        }
        meat.SetActive(false);

        ConfigureTarget(all.Single(t => t.name == "ArbolPlatano (13)"), bananaItem, progress, "Machete Platano.mp3", false,
            all.Single(t => t.name == "ArbolPlatano (13)").Find("Tallo_Arbol.001").GetComponent<MeshRenderer>().bounds);
        ConfigureTarget(all.Single(t => t.name == "PlantaMazorca"), cornItem, progress, "Machete Mazorca.mp3", false,
            all.Single(t => t.name == "PlantaMazorca").GetComponentsInChildren<MeshRenderer>().First(r => r.name.StartsWith("Plane")).bounds);
        foreach (var cow in all.Where(t => t.parent && t.parent.name == "VACAS" && t.name.StartsWith("Vaca")))
            ConfigureTarget(cow, meatItem, progress, "Machete Vaca.mp3", true, cow.GetComponent<MeshRenderer>().bounds);
        foreach (var machete in all.Where(t => t.name.StartsWith("Machete [BuildingBlock] Cube")))
        {
            var blade = machete.GetComponentsInChildren<MeshFilter>(true).Single(m => m.name == "Machete_Hoja").gameObject;
            var collider = blade.GetComponent<BoxCollider>() ?? Undo.AddComponent<BoxCollider>(blade);
            collider.isTrigger = true;
            var hit = blade.GetComponent<MacheteCollisionAudio>() ?? Undo.AddComponent<MacheteCollisionAudio>(blade);
            hit.machete = machete.GetComponent<Grabbable>();
        }

        return FinishConfiguration();
    }

    public static string FinishConfiguration()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var journal = UnityEngine.Object.FindFirstObjectByType<JourneyPanel>();
        var progress = UnityEngine.Object.FindFirstObjectByType<CollectionProgress>();
        var all = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(t => t.gameObject.scene == scene).ToArray();
        foreach (var basket in all.Where(t => t.name.StartsWith("canasta cafetera") && t.GetComponent<MeshRenderer>()).ToArray())
        {
            var slots = basket.GetComponentsInChildren<SnapInteractable>(true);
            var keep = slots.OrderBy(s => s.name).First();
            foreach (var other in slots.Where(s => s != keep)) Undo.DestroyObjectImmediate(other.gameObject);
            var bounds = basket.GetComponent<MeshRenderer>().bounds;
            keep.transform.position = new Vector3(bounds.center.x, bounds.min.y + bounds.size.y * 0.42f, bounds.center.z);
            keep.GetComponent<SphereCollider>().radius = 0.3f;
            keep.MaxSelectingInteractors = 1;
            var deposit = keep.GetComponent<BasketDepositSlot>() ?? keep.gameObject.AddComponent<BasketDepositSlot>();
            deposit.ingredient = keep.name.Split('_')[1];
            deposit.progress = progress;
        }
        ConfigurePanel(journal, progress, all.Where(t => t).ToArray());
        ConfigureGesture(journal);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        return "Configured " + UnityEngine.Object.FindObjectsByType<CollectibleIngredient>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length + " permanent collectibles, 7 single snaps, 4 machetes, 4 harvest targets, 12 collection clips, Thumbs Down and Casa.";
    }

    private static IEnumerable<string> CastProperties(this SerializedProperty array)
    {
        for (int i = 0; i < array.arraySize; i++) yield return array.GetArrayElementAtIndex(i).stringValue;
    }

    private static bool IsGrabInteraction(MonoBehaviour component)
    {
        if (!component) return false;
        var name = component.GetType().Name;
        return name == "HandGrabInteractable" || name == "GrabInteractable" || name == "DistanceHandGrabInteractable" || name == "DistanceGrabInteractable";
    }

    private static async Task AddGrab(GameObject target, bool distance)
    {
        if (distance && target.GetComponentsInChildren<MonoBehaviour>(true).Any(c => c && c.GetType().Name == "DistanceHandGrabInteractable")) return;
        if (!distance && target.GetComponentInChildren<HandGrabInteractable>(true)) return;
        foreach (var mesh in target.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mesh.GetComponent<Collider>()) continue;
            var collider = Undo.AddComponent<BoxCollider>(mesh.gameObject);
            collider.center = mesh.sharedMesh.bounds.center;
            collider.size = mesh.sharedMesh.bounds.size;
        }
        var block = AssetDatabase.LoadAssetAtPath<InterfaceBlockData>(Blocks + (distance ? "DistanceHandGrab.asset" : "HandGrab.asset"));
        var routine = AssetDatabase.LoadAssetAtPath<ScriptableObject>(Blocks + "InstallationRoutines/" + (distance ? "DistanceHandGrabInstallationRoutine.asset" : "HandGrabInstallationRoutine.asset"));
        var field = typeof(InterfaceBlockData).GetField("_selectedRoutine", Hidden);
        try
        {
            field.SetValue(block, routine);
            var install = typeof(BlockData).GetMethod("InstallAsync", Hidden, null, new[] { typeof(GameObject) }, null);
            await (Task<List<GameObject>>)install.Invoke(block, new object[] { target });
        }
        finally { field.SetValue(block, null); }
    }

    private static void AddIngredientSetup(Transform target, string category)
    {
        var grab = target.GetComponent<Grabbable>();
        var body = target.GetComponent<Rigidbody>();
        var snapObject = new GameObject("SnapInteractor");
        snapObject.transform.SetParent(target, false);
        snapObject.transform.position = target.GetComponent<MeshRenderer>().bounds.center;
        var snap = snapObject.AddComponent<SnapInteractor>();
        snap.InjectAllSnapInteractor(grab, body);
        snapObject.AddComponent<TagSet>().InjectOptionalTags(new List<string> { "Ingredient", category });
        var glow = target.gameObject.AddComponent<InteractableGlow>();
        glow.grabbable = grab;
        glow.snapInteractor = snap;
        glow.handGrabs = target.GetComponentsInChildren<HandGrabInteractable>(true);
        glow.controllerGrabs = target.GetComponentsInChildren<GrabInteractable>(true);
        glow.outlineMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Main/Desarollo/Art/InteractableGlow.mat");
    }

    private static void ConfigureTarget(Transform source, CollectibleIngredient item, CollectionProgress progress, string sound, bool hide, Bounds bounds)
    {
        var target = source.gameObject.AddComponent<MacheteHarvestTarget>();
        target.ingredient = item;
        target.effects = progress.effects;
        target.hitSound = Clip("Sonidos Interactivos/" + sound);
        target.hideAfterHarvest = hide;
        var collider = source.gameObject.AddComponent<BoxCollider>();
        var localBounds = new Bounds(source.InverseTransformPoint(bounds.min), Vector3.zero);
        for (int i = 0; i < 8; i++) localBounds.Encapsulate(source.InverseTransformPoint(bounds.center + Vector3.Scale(bounds.extents,
            new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))));
        collider.center = localBounds.center;
        collider.size = localBounds.size;
        collider.isTrigger = true;
    }

    private static void ConfigurePanel(JourneyPanel journal, CollectionProgress progress, Transform[] all)
    {
        journal.collection = progress;
        journal.homeDestination = all.Single(t => t.name == "TeleportToCasa");
        var cards = journal.mapPage.transform.Cast<Transform>().Where(t => t.name.StartsWith("Destination_") && t.name != "Destination_Casa").OrderBy(t => t.GetSiblingIndex()).ToList();
        var existingHome = journal.mapPage.transform.Find("Destination_Casa");
        var home = existingHome ? existingHome.gameObject : UnityEngine.Object.Instantiate(cards.Last().gameObject, journal.mapPage.transform);
        home.name = "Destination_Casa";
        var button = home.GetComponent<UnityEngine.UI.Button>();
        button.onClick = new UnityEngine.UI.Button.ButtonClickedEvent();
        UnityEditor.Events.UnityEventTools.AddPersistentListener(button.onClick, journal.TravelHome);
        button.interactable = false;
        journal.homeButton = progress.homeButton = button;
        home.transform.Find("IngredientImage").gameObject.SetActive(false);
        var label = home.transform.Find("Label").GetComponent<TMP_Text>();
        label.text = "Casa";
        ((RectTransform)label.transform).anchoredPosition = Vector2.zero;
        cards.Add(home.transform);
        for (int i = 0; i < cards.Count; i++)
        {
            var rt = (RectTransform)cards[i];
            rt.sizeDelta = new Vector2(152, 160);
            rt.anchoredPosition = new Vector2((i - 2.5f) * 164, -155);
            ((RectTransform)rt.Find("IngredientImage")).sizeDelta = new Vector2(138, 104);
            var text = rt.Find("Label").GetComponent<TMP_Text>();
            text.fontSize = 25;
            ((RectTransform)text.transform).sizeDelta = new Vector2(146, 42);
        }
        var rig = UnityEngine.Object.FindFirstObjectByType<OVRCameraRig>();
        journal.panel.transform.SetPositionAndRotation(rig.transform.position + Vector3.up * 1.5f + rig.transform.forward * 1.5f,
            Quaternion.Euler(0, rig.transform.eulerAngles.y, 0));
        journal.panel.SetActive(true);
        var preview = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Main/Desarollo/Art/MapIcons/TerrainMapPreview.png");
        if (preview) journal.mapImage.texture = preview;
        journal.ingredientsPage.SetActive(false);
        journal.mapPage.SetActive(true);
        journal.footer.text = "Pulgar abajo para abrir · Casa se habilita al completar los seis ingredientes";
        foreach (var text in journal.hint.GetComponentsInChildren<TMP_Text>(true))
            text.text = text.text.Replace("pulgar arriba", "pulgar abajo").Replace("Pulgar arriba", "Pulgar abajo");
        foreach (var entry in progress.ingredients)
        {
            if (!entry.checklist) entry.checklist = journal.ingredientsPage.GetComponentsInChildren<TMP_Text>(true)
                .Single(t => t.text.Replace("á", "a").Equals(entry.ingredient, StringComparison.OrdinalIgnoreCase)).GetComponentInParent<UnityEngine.UI.Toggle>(true);
            entry.checklist.interactable = false;
        }
        Selection.activeGameObject = journal.panel;
    }

    private static void ConfigureGesture(JourneyPanel journal)
    {
        foreach (var old in UnityEngine.Object.FindObjectsByType<SelectorUnityEventWrapper>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(p => p.name.StartsWith("JournalThumbsUp_") || p.name.StartsWith("JournalThumbsDown_")).ToArray()) Undo.DestroyObjectImmediate(old.gameObject);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Packages/com.meta.xr.sdk.interaction/Runtime/Sample/Prefabs/HandPose/ThumbsDownPose.prefab");
        foreach (var hand in UnityEngine.Object.FindObjectsByType<Hand>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(h => h.name == "ComprehensiveInteractorsLeft" || h.name == "ComprehensiveInteractorsRight"))
        {
            var pose = (GameObject)PrefabUtility.InstantiatePrefab(prefab, journal.transform);
            pose.name = "JournalThumbsDown_" + hand.name;
            pose.GetComponent<HandRef>().InjectHand(hand);
            pose.GetComponent<ShapeRecognizerActiveState>().InjectFingerFeatureStateProvider(hand.GetComponentInChildren<FingerFeatureStateProvider>(true));
            pose.GetComponent<TransformRecognizerActiveState>().InjectTransformFeatureStateProvider(hand.GetComponentInChildren<TransformFeatureStateProvider>(true));
            UnityEditor.Events.UnityEventTools.AddPersistentListener(pose.GetComponent<SelectorUnityEventWrapper>().WhenSelected, journal.OnThumbsDown);
        }
    }

    private static AudioClip Clip(string path)
    {
        return AssetDatabase.LoadAssetAtPath<AudioClip>(Audio + path) ?? throw new Exception("Missing audio: " + path);
    }
}
