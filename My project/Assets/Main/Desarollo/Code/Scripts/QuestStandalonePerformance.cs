using UnityEngine;
using UnityEngine.Rendering.Universal;

public sealed class QuestStandalonePerformance : MonoBehaviour
{
    public Camera playerCamera;
    public Renderer[] distantDecoration;
    public bool previewInEditor;
    private CullingGroup culling;
    private int[] limits;

    private void Start()
    {
        if (Application.platform != RuntimePlatform.Android && !previewInEditor) return;
        Application.targetFrameRate = 72;
        QualitySettings.vSyncCount = 0;
        if (OVRManager.display != null) OVRManager.display.displayFrequency = 72f;
        OVRManager.foveatedRenderingLevel = OVRManager.FoveatedRenderingLevel.Medium;
        OVRManager.useDynamicFoveatedRendering = true;
        foreach (var camera in FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            camera.allowHDR = false;
            if (camera.TryGetComponent<UniversalAdditionalCameraData>(out var data)) data.renderPostProcessing = false;
        }
        var spheres = new BoundingSphere[distantDecoration.Length];
        limits = new int[spheres.Length];
        for (int i = 0; i < spheres.Length; i++)
        {
            var bounds = distantDecoration[i].bounds;
            spheres[i] = new BoundingSphere(bounds.center, bounds.extents.magnitude);
            limits[i] = bounds.extents.magnitude < 1f ? 1 : 2;
        }
        culling = new CullingGroup();
        culling.targetCamera = playerCamera;
        culling.SetDistanceReferencePoint(playerCamera.transform);
        culling.SetBoundingDistances(new[] { 60f, 120f });
        culling.SetBoundingSpheres(spheres);
        culling.SetBoundingSphereCount(spheres.Length);
        culling.onStateChanged = change =>
        {
            var renderer = distantDecoration[change.index];
            if (renderer) renderer.forceRenderingOff = change.currentDistance >= limits[change.index];
        };
    }

    private void OnDestroy()
    {
        culling?.Dispose();
        foreach (var renderer in distantDecoration) if (renderer) renderer.forceRenderingOff = false;
    }
}
