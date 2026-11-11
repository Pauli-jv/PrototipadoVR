using System;
using System.Linq;
using Oculus.Interaction;
using Oculus.Interaction.Input;
using Oculus.Interaction.Locomotion;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Video;

public static class ConfigureGrandmotherJourney
{
    private const string Meta = "Packages/com.meta.xr.sdk.interaction/Runtime/Sample/";
    private static readonly Color Background = new Color(0.055f, 0.065f, 0.09f, 0.98f);
    private static readonly Color Card = new Color(0.12f, 0.14f, 0.18f, 1f);
    private static readonly Color Accent = new Color(0.25f, 0.64f, 1f, 1f);

    public static string Configure()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (!scene.path.EndsWith("Terreno_Dev.unity")) throw new Exception("Open Terreno_Dev first.");
        if (UnityEngine.Object.FindFirstObjectByType<GrandmotherArrival>()) throw new Exception("Journey already configured.");
        var rig = UnityEngine.Object.FindFirstObjectByType<OVRCameraRig>();
        var elder = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Single(t => t.gameObject.scene == scene && t.name.Equals("anciana", StringComparison.OrdinalIgnoreCase));
        var intro = UnityEngine.Object.FindFirstObjectByType<IntroductionSequence>();
        var root = new GameObject("GrandmotherJourney");
        Undo.RegisterCreatedObjectUndo(root, "Configure grandmother journey");
        var arrival = root.AddComponent<GrandmotherArrival>();
        var journal = root.AddComponent<JourneyPanel>();
        arrival.journal = journal;
        journal.arrival = arrival;
        arrival.eyes = journal.eyes = rig.centerEyeAnchor;
        arrival.locomotor = UnityEngine.Object.FindFirstObjectByType<FirstPersonLocomotor>();
        arrival.locomotionConnections = UnityEngine.Object.FindObjectsByType<LocomotionEventsConnection>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(c => c.gameObject.scene == scene).Cast<Behaviour>().ToArray();

        var destination = new GameObject("ArrivalInFrontOfAnciana").transform;
        destination.SetParent(root.transform);
        // Use the existing house teleport point nearest the front of the character.
        var housePoint = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(t => t.name == "[BuildingBlock] Teleport Interactable" && t.gameObject.scene == scene)
            .OrderBy(t => Vector3.Distance(t.position, elder.position + Vector3.forward * 2f)).First();
        destination.position = housePoint.position;
        destination.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(elder.position - destination.position, Vector3.up));
        arrival.destination = destination;
        var fadeObject = new GameObject("ArrivalBlackFade");
        fadeObject.transform.SetParent(rig.centerEyeAnchor, false);
        arrival.screenFade = fadeObject.AddComponent<OVRScreenFade>();
        arrival.screenFade.fadeOnStart = false;
        arrival.screenFade.fadeColor = Color.black;

        var voiceObject = new GameObject("AncianaIntroductionVoice");
        voiceObject.transform.SetParent(elder, false);
        arrival.voice = voiceObject.AddComponent<AudioSource>();
        arrival.voice.playOnAwake = false;
        arrival.voice.spatialBlend = 0f;
        arrival.dialogue = Enumerable.Range(1, 5).Select(i => AssetDatabase.LoadAssetAtPath<AudioClip>(
            "Assets/Main/Desarollo/Audio/Sonidos RV/Guion/1 Introduccion casa/Introduccion " + i + ".mp3")).ToArray();
        if (arrival.dialogue.Any(c => !c)) throw new Exception("Missing introduction audio.");

        foreach (var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(c => c.gameObject.scene == scene && c.GetComponentsInChildren<UnityEngine.UI.Button>(true).Length > 0).ToArray())
            MakeInteractive(canvas);

        var hint = NewCanvas("ThumbsUpTutorial", root.transform, new Vector2(1000, 680));
        BackgroundImage(hint.transform, Background);
        Label(hint.transform, "Title", "Tu próxima tarea", new Vector2(0, 265), new Vector2(880, 65), 42);
        Label(hint.transform, "Instruction", "Haz el gesto de pulgar arriba para abrir tu lista y el mapa.", new Vector2(0, 175), new Vector2(880, 110), 30);
        Label(hint.transform, "Hint", "Usa cualquiera de las manos. Repite el gesto para cerrar.", new Vector2(0, -275), new Vector2(880, 65), 25);
        var videoRect = Rect("MetaPoseTutorialVideo", hint.transform, new Vector2(0, -55), new Vector2(640, 360));
        journal.videoImage = videoRect.gameObject.AddComponent<UnityEngine.UI.RawImage>();
        journal.videoImage.raycastTarget = false;
        journal.tutorialVideo = videoRect.gameObject.AddComponent<VideoPlayer>();
        journal.tutorialVideo.clip = AssetDatabase.LoadAssetAtPath<VideoClip>(Meta + "Objects/Environment/Tooltips/DialogVideos/Instruction_Poses.mp4");
        journal.tutorialVideo.renderMode = VideoRenderMode.RenderTexture;
        journal.tutorialVideo.audioOutputMode = VideoAudioOutputMode.None;
        journal.tutorialVideo.isLooping = true;
        journal.tutorialVideo.playOnAwake = true;
        arrival.gestureHint = journal.hint = hint.gameObject;
        hint.gameObject.SetActive(false);

        var panel = NewCanvas("IngredientsAndMapPanel", root.transform, new Vector2(1100, 850));
        BackgroundImage(panel.transform, Background);
        Label(panel.transform, "Title", "El encargo de la abuela", new Vector2(-50, 350), new Vector2(900, 75), 42);
        Button(panel.transform, "IngredientsTab", "Ingredientes", new Vector2(-250, 250), new Vector2(460, 70), journal.ShowIngredients);
        Button(panel.transform, "MapTab", "Mapa", new Vector2(250, 250), new Vector2(460, 70), journal.ShowMap);
        Button(panel.transform, "Close", "Cerrar", new Vector2(410, 350), new Vector2(180, 65), journal.Close);
        var list = Rect("IngredientsSection", panel.transform, new Vector2(0, -55), new Vector2(980, 500));
        journal.ingredientsPage = list.gameObject;
        var ingredients = new[] { "Carne", "Mazorca", "Papa", "Plátano", "Yuca", "Cilantro" };
        for (int i = 0; i < ingredients.Length; i++)
        {
            var row = Rect("Ingredient_" + i, list, new Vector2(0, 205 - i * 78), new Vector2(960, 68));
            var rowImage = BackgroundImage(row, Card);
            var toggle = row.gameObject.AddComponent<UnityEngine.UI.Toggle>();
            toggle.targetGraphic = rowImage;
            var box = Rect("Checkbox", row, new Vector2(-425, 0), new Vector2(38, 38));
            BackgroundImage(box, new Color(0.25f, 0.29f, 0.35f));
            var check = Rect("Checked", box, Vector2.zero, new Vector2(26, 26));
            toggle.graphic = BackgroundImage(check, Accent);
            Label(row, "Name", ingredients[i], new Vector2(25, 0), new Vector2(780, 55), 30);
        }
        Label(panel.transform, "Footer", "Marca los ingredientes que ya recogiste · Pulgar arriba para abrir o cerrar", new Vector2(0, -365), new Vector2(990, 65), 23);
        var map = Rect("MapSection", panel.transform, new Vector2(0, -45), new Vector2(500, 500));
        journal.mapPage = map.gameObject;
        journal.mapImage = map.gameObject.AddComponent<UnityEngine.UI.RawImage>();
        journal.mapImage.raycastTarget = false;
        var marker = Rect("YouAreHere", map, Vector2.zero, new Vector2(20, 20));
        BackgroundImage(marker, Accent).raycastTarget = false;
        journal.playerMarker = marker;
        var mapCameraObject = new GameObject("TerrainMapCamera");
        mapCameraObject.transform.SetParent(root.transform);
        mapCameraObject.transform.SetPositionAndRotation(new Vector3(432, 160, 557), Quaternion.Euler(90, 0, 0));
        journal.mapCamera = mapCameraObject.AddComponent<Camera>();
        journal.mapCamera.orthographic = true;
        journal.mapCamera.orthographicSize = 95;
        journal.mapCamera.nearClipPlane = 0.1f;
        journal.mapCamera.farClipPlane = 300;
        journal.mapCamera.cullingMask = ~(1 << 5);
        journal.mapCamera.stereoTargetEye = StereoTargetEyeMask.None;
        mapCameraObject.SetActive(false);
        journal.panel = panel.gameObject;
        MakeInteractive(panel);
        panel.gameObject.SetActive(false);
        map.gameObject.SetActive(false);

        foreach (var handName in new[] { "ComprehensiveInteractorsLeft", "ComprehensiveInteractorsRight" })
        {
            var hand = UnityEngine.Object.FindObjectsByType<Hand>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single(h => h.name == handName);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Meta + "Prefabs/HandPose/ThumbsUpPose.prefab");
            var pose = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
            pose.name = "JournalThumbsUp_" + handName;
            pose.GetComponent<HandRef>().InjectHand(hand);
            pose.GetComponent<Oculus.Interaction.PoseDetection.ShapeRecognizerActiveState>().InjectFingerFeatureStateProvider(
                hand.GetComponentInChildren<Oculus.Interaction.PoseDetection.FingerFeatureStateProvider>(true));
            pose.GetComponent<Oculus.Interaction.PoseDetection.TransformRecognizerActiveState>().InjectTransformFeatureStateProvider(
                hand.GetComponentInChildren<Oculus.Interaction.PoseDetection.TransformFeatureStateProvider>(true));
            UnityEditor.Events.UnityEventTools.AddPersistentListener(pose.GetComponent<SelectorUnityEventWrapper>().WhenSelected, journal.OnThumbsUp);
        }
        // UnityEventTools records a persistent callback that survives scene reloads.
        var eventField = typeof(IntroductionSequence).GetField("onCompleted", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        UnityEditor.Events.UnityEventTools.AddPersistentListener((UnityEvent)eventField.GetValue(intro), arrival.BeginArrival);
        EditorUtility.SetDirty(intro);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return "Configured VR ray/poke, 5 audio clips, arrival at " + destination.position + ", two Meta thumbs-up detectors, tutorial and journal.";
    }

    private static void MakeInteractive(Canvas canvas)
    {
        canvas.renderMode = RenderMode.WorldSpace;
        foreach (string guid in new[] { "8369d93f7b6b99742bbea0649a41b7b1", "4db41829582c7d24f80ee9603868dd67" })
        {
            var template = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            if (!template) throw new Exception("Missing Meta canvas template: " + guid);
            var child = UnityEngine.Object.Instantiate(template, canvas.transform, false);
            child.name = template.name;
            var rt = child.GetComponent<RectTransform>();
            rt.localPosition = Vector3.zero;
            rt.localRotation = Quaternion.identity;
            rt.localScale = Vector3.one;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = Vector2.one * 0.5f;
            rt.sizeDelta = Vector2.zero;
            child.GetComponent<PointableCanvas>().InjectCanvas(canvas);
        }
    }

    private static Canvas NewCanvas(string name, Transform parent, Vector2 size)
    {
        var rt = Rect(name, parent, Vector2.zero, size);
        rt.localScale = Vector3.one * 0.001f;
        var c = rt.gameObject.AddComponent<Canvas>();
        c.renderMode = RenderMode.WorldSpace;
        rt.gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();
        rt.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        return c;
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.sizeDelta = size;
        rt.anchoredPosition = position;
        return rt;
    }

    private static UnityEngine.UI.Image BackgroundImage(Transform parent, Color color)
    {
        var image = parent.gameObject.AddComponent<UnityEngine.UI.Image>();
        image.color = color;
        image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        image.type = UnityEngine.UI.Image.Type.Sliced;
        return image;
    }

    private static void Label(Transform parent, string name, string text, Vector2 position, Vector2 size, float fontSize)
    {
        var rt = Rect(name, parent, position, size);
        var label = rt.gameObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.raycastTarget = false;
    }

    private static void Button(Transform parent, string name, string text, Vector2 position, Vector2 size, UnityAction action)
    {
        var rt = Rect(name, parent, position, size);
        var image = BackgroundImage(rt, Card);
        var button = rt.gameObject.AddComponent<UnityEngine.UI.Button>();
        button.targetGraphic = image;
        UnityEditor.Events.UnityEventTools.AddPersistentListener(button.onClick, action);
        Label(rt, "Label", text, Vector2.zero, size, 28);
    }
}
