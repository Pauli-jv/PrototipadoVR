using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ConfigureCookingSequence
{
    private static readonly string[] Whole = { "Carne_Entera", "Papa", "YucaEntera", "Platano_Entero", "MazorcaEntera", "CilantroEntero" };
    private static readonly string[] Cut = { "CarneCortada", "PapaCortada", "YucaCortada", "PlatanoCortado", "MazorcaCortada", "MataCilantroCortada" };
    private static readonly string[] Positions = { "PositionCarneCortada", "PositionPapaCortada", "PositionYucaCortada", "PositionPlatanoCortado", "PositionMazorcaCortada", "PostionMataCilatroCortada" };
    public static async Task<string> Configure()
    {
        if (Application.isPlaying) throw new Exception("Stop Play first");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();

        var all = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Transform>(true)).ToArray();
        var kitchen = all.Single(t => t && t.name == "--CocinaCortarAlimentos--");
        // Unpack only instances in the kitchen so individual pieces can become a compound object.
        foreach (var t in kitchen.GetComponentsInChildren<Transform>(true))
            if (t && PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject))
                PrefabUtility.UnpackPrefabInstance(t.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        var seq = UnityEngine.Object.FindFirstObjectByType<CookingSequence>() ?? new GameObject("CookingSequence").AddComponent<CookingSequence>();
        seq.kitchen = kitchen.gameObject;
        seq.collection = UnityEngine.Object.FindFirstObjectByType<CollectionProgress>();
        seq.narration = seq.GetComponent<AudioSource>() ?? seq.gameObject.AddComponent<AudioSource>();
        seq.narration.playOnAwake = false;
        seq.preparation = Enumerable.Range(1, 3).Select(n => Clip("5 Preparacion de los ingredientes/Preparacion " + n + ".mp3")).ToArray();
        seq.cooking1 = Clip("6 Coccion/Coccion 1.mp3");
        seq.cooking3 = Clip("6 Coccion/Coccion 3.mp3");
        seq.cooking6 = Clip("6 Coccion/Coccion 6.mp3");
        seq.finalization1 = Clip("7 Finalizacion/Finalizacion 1.mp3");
        seq.result1 = Clip("8 Resultado/Resultado 1.mp3");
        seq.result2 = Clip("8 Resultado/Resultado 2.mp3");
        var transforms = kitchen.GetComponentsInChildren<Transform>(true);
        var positions = transforms.Single(t => t && t.name == "SnapPositionComida");
        if (seq.foods == null || seq.foods.Length != 6) seq.foods = new CookingFood[6];
        for (int i = 0; i < 6; i++)
        {
            if (seq.foods[i]) continue;
            var whole = transforms.Single(t => t && t.name == Whole[i]);
            var pieces = transforms.Single(t => t && t.name == Cut[i]);
            Clean(whole); Clean(pieces);
            Recenter(pieces);
            var food = new GameObject("Cooking_" + Cut[i]).AddComponent<CookingFood>();
            food.transform.SetParent(seq.transform, false);
            food.whole = whole.gameObject;
            food.pieces = pieces.gameObject;
            food.wholeGlow = Glow(whole.gameObject);
            food.wholeGlow.HighlightEnabled = false;
            whole.gameObject.AddComponent<CookingCutTarget>().food = food;
            foreach (var c in whole.GetComponentsInChildren<Collider>()) c.isTrigger = true;
            var position = positions.Find(Positions[i]);
            if (!position)
            {
                position = new GameObject(Positions[i]).transform;
                position.SetParent(positions, false);
                position.position = positions.position + new Vector3((i - 2) * 0.065f, -0.04f, 0.05f);
            }
            await SetupMovable(food, position, "Cooking_" + i);
            seq.foods[i] = food;
            whole.gameObject.SetActive(true);
            pieces.gameObject.SetActive(false);
        }
        var pot = transforms.Single(t => t && t.name == "OllaCocinaSnapAlimentos");
        var lidTransform = pot.Find("Olla_Tapa");
        Clean(lidTransform); Recenter(lidTransform);
        seq.lid = new GameObject("Cooking_Lid").AddComponent<CookingFood>();
        seq.lid.transform.SetParent(seq.transform, false);
        seq.lid.pieces = lidTransform.gameObject;
        await SetupMovable(seq.lid, pot.Find("SnapTapaOlla"), "Cooking_Lid");
        var knife = transforms.Single(t => t && t.name == "CuchilloCocina");
        Clean(knife);
        await Grab(knife.gameObject, false);
        await Grab(knife.gameObject, true);
        var kb = knife.GetComponent<Rigidbody>(); kb.isKinematic = true; kb.useGravity = false;
        knife.GetComponent<Grabbable>().InjectOptionalRigidbody(kb);
        knife.GetComponent<Grabbable>().InjectOptionalKinematicWhileSelected(true);
        foreach (var col in knife.GetComponentsInChildren<Collider>(true))
        {
            col.isTrigger = true;
            var contact = col.gameObject.AddComponent<KnifeCutContact>();
            contact.knife = knife.GetComponent<Grabbable>();
        }
        Glow(knife.gameObject);
        var arrival = UnityEngine.Object.FindFirstObjectByType<GrandmotherArrival>();
        arrival.cooking = seq;
        ConfigureMovement();
        var rig = UnityEngine.Object.FindFirstObjectByType<OVRCameraRig>();
        var panel = all.Single(t => t && t.name == "CanvasBienvenida");
        var forward = Vector3.ProjectOnPlane(panel.forward, Vector3.up).normalized;
        var start = panel.position - forward * 2f;
        start.y = rig.transform.position.y;
        rig.transform.SetPositionAndRotation(start, Quaternion.LookRotation(forward));
        var marker = new GameObject("PlayerStart_Introduction").transform;
        marker.SetPositionAndRotation(start, rig.transform.rotation);
        kitchen.gameObject.SetActive(false);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        return "Cooking configured: six compound foods, knife, lid, 7 pot snaps, initial pose and Meta movement.";
    }

    private static void Clean(Transform root)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true).Where(t => t != root && (t.name.StartsWith("[BuildingBlock]") || t.name == "SnapInteractor")).ToArray())
            if (t) UnityEngine.Object.DestroyImmediate(t.gameObject);
        foreach (var m in root.GetComponentsInChildren<MonoBehaviour>(true)) UnityEngine.Object.DestroyImmediate(m);
        foreach (var r in root.GetComponentsInChildren<Rigidbody>(true)) UnityEngine.Object.DestroyImmediate(r);
        foreach (var mesh in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (!mesh.GetComponent<Collider>())
            {
                var c = mesh.gameObject.AddComponent<BoxCollider>();
                c.center = mesh.sharedMesh.bounds.center; c.size = mesh.sharedMesh.bounds.size;
            }
        }
    }

    private static void Recenter(Transform root)
    {
        if (root.GetComponent<MeshFilter>()) return;
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        var bounds = renderers[0].bounds;
        foreach (var r in renderers) bounds.Encapsulate(r.bounds);
        var children = root.Cast<Transform>().ToArray();
        foreach (var c in children) c.SetParent(null, true);
        var parent = root.parent;
        root.SetParent(null, true);
        root.SetPositionAndRotation(bounds.center, Quaternion.identity);
        root.localScale = Vector3.one;
        root.SetParent(parent, true);
        foreach (var c in children) c.SetParent(root, true);
    }

    private static async Task SetupMovable(CookingFood food, Transform position, string tag)
    {
        await Grab(food.pieces, false);
        await Grab(food.pieces, true);
        food.body = food.pieces.GetComponent<Rigidbody>();
        food.body.isKinematic = true; food.body.useGravity = false;
        var grabbable = food.pieces.GetComponent<Grabbable>();
        grabbable.InjectOptionalRigidbody(food.body);
        grabbable.InjectOptionalKinematicWhileSelected(true);
        food.grabs = food.pieces.GetComponentsInChildren<MonoBehaviour>(true).Where(c => c is HandGrabInteractable || c is GrabInteractable || c is DistanceHandGrabInteractable || c is DistanceGrabInteractable).Cast<Behaviour>().ToArray();
        foreach (var grab in food.grabs) grab.enabled = false;
        var snapGO = new GameObject("SnapInteractor"); snapGO.transform.SetParent(food.pieces.transform, false);
        food.snap = snapGO.AddComponent<SnapInteractor>();
        food.snap.InjectAllSnapInteractor(grabbable, food.body);
        snapGO.AddComponent<TagSet>().InjectOptionalTags(new List<string> { tag });
        var slotGO = new GameObject("Snap_" + tag);
        slotGO.transform.SetPositionAndRotation(position.position, food.pieces.transform.rotation);
        slotGO.transform.SetParent(position, true);
        var rb = slotGO.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false;
        var collider = slotGO.AddComponent<SphereCollider>(); collider.isTrigger = true; collider.radius = 0.2f;
        var filter = slotGO.AddComponent<TagSetFilter>(); filter.InjectOptionalRequireTags(new[] { tag });
        food.slot = slotGO.AddComponent<SnapInteractable>();
        food.slot.InjectAllSnapInteractable(rb);
        food.slot.InjectOptionalInteractorFilters(new List<IGameObjectFilter> { filter });
        food.slot.MaxSelectingInteractors = 1;
        food.piecesGlow = Glow(food.pieces);
        food.piecesGlow.snapInteractor = food.snap;
        food.piecesGlow.HighlightEnabled = false;
        food.snap.enabled = food.slot.enabled = false;
    }

    private static InteractableGlow Glow(GameObject go)
    {
        var glow = go.AddComponent<InteractableGlow>();
        glow.outlineMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Main/Desarollo/Art/InteractableGlow.mat");
        glow.grabbable = go.GetComponent<Grabbable>();
        glow.handGrabs = go.GetComponentsInChildren<HandGrabInteractable>(true);
        glow.controllerGrabs = go.GetComponentsInChildren<GrabInteractable>(true);
        return glow;
    }

    private static Task Grab(GameObject go, bool distance)
    {
        return (Task)typeof(ConfigureHarvestSequence).GetMethod("AddGrab", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { go, distance });
    }

    public static void ConfigureMovement()
    {
        foreach (var snap in UnityEngine.Object.FindObjectsByType<SnapInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            snap.InjectOptionalMovementProvider(Movement(snap.gameObject, 1.2f));
        foreach (var hand in UnityEngine.Object.FindObjectsByType<DistanceHandGrabInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var provider = Movement(hand.gameObject, 0.35f);
            hand.InjectOptionalMovementProvider(provider);
            var controller = hand.GetComponent<DistanceGrabInteractable>();
            if (controller) controller.InjectOptionalMovementProvider(provider);
        }
    }

    private static MoveTowardsTargetProvider Movement(GameObject go, float seconds)
    {
        var p = go.GetComponent<MoveTowardsTargetProvider>() ?? go.AddComponent<MoveTowardsTargetProvider>();
        var so = new SerializedObject(p);
        var data = so.FindProperty("_travellingData");
        data.FindPropertyRelative("_travelSpeed").floatValue = seconds;
        data.FindPropertyRelative("_useFixedTravelTime").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();
        return p;
    }

    private static AudioClip Clip(string path) => AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Main/Desarollo/Audio/Sonidos RV/Guion/" + path) ?? throw new Exception("Missing audio " + path);
}

