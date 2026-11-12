using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ConfigureIntroductionReadability
{
    public static string Apply()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var all = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToArray();
        var welcome = all.Single(t => t.name == "CanvasBienvenida");
        var root = welcome.parent;
        var rig = Object.FindFirstObjectByType<OVRCameraRig>();
        var arrival = Object.FindFirstObjectByType<GrandmotherArrival>();
        var marker = all.Single(t => t.name == "PlayerStart_Introduction");
        Vector3 forward = Vector3.ProjectOnPlane(welcome.forward, Vector3.up).normalized;
        Vector3 feet = welcome.position - forward * 0.7f;
        feet.y = rig.transform.position.y;
        marker.SetPositionAndRotation(feet, Quaternion.LookRotation(forward));
        rig.transform.SetPositionAndRotation(feet, marker.rotation);
        arrival.introductionStart = marker;
        arrival.introductionScreens = root;
        arrival.welcomeScreen = welcome;
        var eye = feet + Vector3.up * 1.6f;
        var panels = root.GetComponentsInChildren<Canvas>(true);
        var font = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        var paths = new System.Collections.Generic.HashSet<string>();
        foreach (var canvas in panels)
        {
            bool image = canvas.name.StartsWith("ImagenesIntro");
            bool legend = canvas.name.StartsWith("CanvasIntro");
            if (!image && !legend && canvas.transform != welcome) continue;
            float angle = image ? -35f : legend && canvas.name != "CanvasIntro4" ? 35f : 0f;
            var direction = Quaternion.AngleAxis(angle, Vector3.up) * forward;
            canvas.transform.SetPositionAndRotation(eye + direction * 0.7f, Quaternion.LookRotation(direction));
            float width = angle == 0 ? 0.95f : 0.68f;
            var rt = (RectTransform)canvas.transform;
            canvas.transform.localScale = Vector3.one * (width / rt.rect.width);
            canvas.renderMode = RenderMode.WorldSpace;
            foreach (var graphic in canvas.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
            {
                graphic.raycastTarget = graphic.GetComponent<UnityEngine.UI.Button>() != null;
                var path = AssetDatabase.GetAssetPath(graphic.mainTexture);
                if (path.StartsWith("Assets/")) paths.Add(path);
            }
            foreach (var button in canvas.GetComponentsInChildren<UnityEngine.UI.Button>(true))
            {
                button.interactable = true;
                var rect = (RectTransform)button.transform;
                rect.localScale = Vector3.one;
                rect.sizeDelta = new Vector2(700, 160);
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0);
                rect.pivot = new Vector2(0.5f, 0);
                rect.anchoredPosition = new Vector2(0, 75);
                var text = button.GetComponentInChildren<TMPro.TMP_Text>(true);
                text.font = font;
                text.fontSize = 90;
                text.enableAutoSizing = false;
                text.fontStyle = TMPro.FontStyles.Bold;
                text.raycastTarget = false;
                var label = text.rectTransform;
                label.anchorMin = Vector2.zero; label.anchorMax = Vector2.one;
                label.offsetMin = new Vector2(10, 8); label.offsetMax = new Vector2(-10, -8);
            }
            foreach (var pointable in canvas.GetComponentsInChildren<Oculus.Interaction.PointableCanvas>(true)) pointable.InjectCanvas(canvas);
            foreach (var clipper in canvas.GetComponentsInChildren<Oculus.Interaction.Surfaces.BoundsClipper>(true))
            {
                var size = clipper.Size;
                size.z = 50f; // Avoid sub-micron clipping tolerance on rotated, millimetre-scaled canvases far from the origin.
                clipper.Size = size;
                EditorUtility.SetDirty(clipper);
            }
            var overlay = canvas.GetComponent<OVROverlayCanvas>();
            if (overlay)
            {
                overlay.maxTextureSize = 2048;
                var so = new SerializedObject(overlay);
                so.FindProperty("_dynamicResolution").boolValue = false;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorUtility.SetDirty(canvas.gameObject);
        }
        foreach (var path in paths)
        {
            if (!(AssetImporter.GetAtPath(path) is TextureImporter ti)) continue;
            ti.filterMode = FilterMode.Bilinear;
            ti.mipmapEnabled = false;
            var android = ti.GetPlatformTextureSettings("Android");
            android.overridden = true; android.maxTextureSize = 2048; android.format = TextureImporterFormat.ASTC_4x4;
            ti.SetPlatformTextureSettings(android);
            ti.SaveAndReimport();
        }
        var journal = Object.FindFirstObjectByType<JourneyPanel>(); journal.panelDistance = 0.5f;
        var module = Object.FindFirstObjectByType<Oculus.Interaction.PointableCanvasModule>();
        module.enabled = true; module.ExclusiveMode = true;
        foreach (var other in module.GetComponents<UnityEngine.EventSystems.BaseInputModule>())
            if (other != module) other.enabled = false;
        EditorUtility.SetDirty(arrival); EditorUtility.SetDirty(journal);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        return "Start 0.7m; gesture 0.5m; readable buttons and fixed 2048 overlays; initial locomotion lock.";
    }
}
