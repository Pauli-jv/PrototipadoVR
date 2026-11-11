using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ConfigureMapDestinations
{
    private const string Icons = "Assets/Main/Desarollo/Art/MapIcons";
    private static readonly string[] Names = { "Yuca", "Papa", "Cilantro", "Mazorca", "Carne" };

    public static string CreateIcons()
    {
        Directory.CreateDirectory(Icons);
        var transforms = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(t => t.gameObject.scene == UnityEngine.SceneManagement.SceneManager.GetActiveScene()).ToArray();
        var sources = new[]
        {
            transforms.Single(t => t.name == "Yuca_2"),
            transforms.Single(t => t.name == "Papa" && t.position.z > 580),
            transforms.Single(t => t.name == "Cilantro" && t.position.z > 580),
            transforms.Single(t => t.name.Trim() == "mazorcas cocina"),
            transforms.Single(t => t.name == "Carne_Entera")
        };
        for (int i = 0; i < Names.Length; i++) RenderIcon(sources[i], Icons + "/" + Names[i] + ".png");
        AssetDatabase.Refresh();
        return "Created five ingredient images from scene models.";
    }

    public static string Configure(string yuca, string papa, string cilantro, string mazorca, string carne)
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (!scene.path.EndsWith("Terreno_Dev.unity") || Application.isPlaying) throw new Exception("Open Terreno_Dev in Edit Mode.");
        var journal = UnityEngine.Object.FindFirstObjectByType<JourneyPanel>();
        Undo.RecordObject(journal, "Configure map destinations");
        journal.panelDistance = 1.5f;
        journal.footer = journal.panel.transform.Find("Footer").GetComponent<TMPro.TMP_Text>();
        Undo.RecordObject(journal.mapCamera.transform, "Include all ingredient destinations");
        journal.mapCamera.transform.position = new Vector3(459, 160, 557);
        var destinations = new[] { yuca, papa, cilantro, mazorca, carne };
        var transforms = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(t => t.gameObject.scene == scene).ToArray();
        journal.mapDestinations = destinations.Select(n => transforms.Single(t => t.name == n && t.parent != null && t.parent.name == "TeleportTo")).ToArray();
        var page = (RectTransform)journal.mapPage.transform;
        if (page.Find("Destination_Yuca")) throw new Exception("Destination buttons already configured.");
        Undo.RecordObject(page, "Resize map section");
        page.sizeDelta = new Vector2(1000, 500);
        var oldMapImage = journal.mapImage;
        Undo.RecordObject(oldMapImage, "Move map image to viewport");
        oldMapImage.enabled = false;
        var view = Rect("TerrainMapView", page, new Vector2(0, 85), new Vector2(440, 290));
        journal.mapImage = view.gameObject.AddComponent<UnityEngine.UI.RawImage>();
        journal.mapImage.raycastTarget = false;
        // Keep the terrain square inside the wide map viewport, without stretching it.
        view.sizeDelta = new Vector2(290, 290);
        Undo.SetTransformParent(journal.playerMarker, view, "Move player marker");
        journal.playerMarker.localScale = Vector3.one;
        for (int i = 0; i < Names.Length; i++)
        {
            var card = Rect("Destination_" + Names[i], page, new Vector2((i - 2) * 196, -155), new Vector2(180, 160));
            var background = card.gameObject.AddComponent<UnityEngine.UI.Image>();
            background.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            background.type = UnityEngine.UI.Image.Type.Sliced;
            background.color = new Color(0.12f, 0.16f, 0.22f, 1f);
            var button = card.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = background;
            var colors = button.colors;
            colors.highlightedColor = new Color(0.6f, 0.8f, 1f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;
            UnityEditor.Events.UnityEventTools.AddIntPersistentListener(button.onClick, journal.TravelToIngredient, i);
            var imageRect = Rect("IngredientImage", card, new Vector2(0, 18), new Vector2(152, 104));
            var image = imageRect.gameObject.AddComponent<UnityEngine.UI.RawImage>();
            image.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Icons + "/" + Names[i] + ".png");
            image.raycastTarget = false;
            if (!image.texture) throw new Exception("Missing icon " + Names[i]);
            var labelRect = Rect("Label", card, new Vector2(0, -56), new Vector2(170, 42));
            var label = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
            label.text = Names[i];
            label.fontSize = 28;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
        }
        EditorUtility.SetDirty(journal);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return string.Join("\n", Names.Select((n, i) => n + " -> " + journal.mapDestinations[i].name));
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Add map destination");
        go.layer = 5;
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
        return rt;
    }

    private static void RenderIcon(Transform source, string path)
    {
        var clone = UnityEngine.Object.Instantiate(source.gameObject);
        clone.name = "IngredientIconPreview";
        clone.transform.SetPositionAndRotation(new Vector3(0, -10000, 0), source.rotation);
        clone.transform.localScale = source.lossyScale;
        clone.SetActive(true);
        var materials = new System.Collections.Generic.List<Material>();
        var cameraObject = new GameObject("IconPreviewCamera");
        var camera = cameraObject.AddComponent<Camera>();
        var texture = new RenderTexture(384, 256, 24);
        Texture2D image = null;
        var previous = RenderTexture.active;
        try
        {
            foreach (var component in clone.GetComponentsInChildren<Behaviour>(true)) component.enabled = false;
            foreach (var child in clone.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 31;
            var renderers = clone.GetComponentsInChildren<Renderer>(true);
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers)
            {
                renderer.enabled = true;
                bounds.Encapsulate(renderer.bounds);
                renderer.sharedMaterials = renderer.sharedMaterials.Select(original =>
                {
                    var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                    if (original)
                    {
                        material.SetTexture("_BaseMap", original.HasProperty("_BaseMap") ? original.GetTexture("_BaseMap") : original.mainTexture);
                        material.SetColor("_BaseColor", original.HasProperty("_BaseColor") ? original.GetColor("_BaseColor") : Color.white);
                    }
                    materials.Add(material);
                    return material;
                }).ToArray();
            }
            camera.enabled = false;
            camera.cullingMask = 1 << 31;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.12f, 0.16f, 0.22f, 1f);
            camera.orthographic = true;
            camera.aspect = 1.5f;
            camera.orthographicSize = bounds.extents.magnitude * 1.1f;
            camera.nearClipPlane = 0.001f;
            camera.farClipPlane = Mathf.Max(10, bounds.size.magnitude * 10);
            camera.transform.position = bounds.center + new Vector3(0.4f, 1f, -0.9f).normalized * Mathf.Max(1, bounds.size.magnitude * 3);
            camera.transform.LookAt(bounds.center);
            camera.targetTexture = texture;
            camera.Render();
            RenderTexture.active = texture;
            image = new Texture2D(384, 256, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 384, 256), 0, 0);
            image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            camera.targetTexture = null;
            texture.Release();
            UnityEngine.Object.DestroyImmediate(texture);
            if (image) UnityEngine.Object.DestroyImmediate(image);
            UnityEngine.Object.DestroyImmediate(clone);
            UnityEngine.Object.DestroyImmediate(cameraObject);
            foreach (var material in materials) UnityEngine.Object.DestroyImmediate(material);
        }
    }
}
