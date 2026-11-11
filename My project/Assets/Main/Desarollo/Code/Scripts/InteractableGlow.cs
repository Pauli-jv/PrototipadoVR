using System.Collections.Generic;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class InteractableGlow : MonoBehaviour
{
    public Material outlineMaterial;
    public Grabbable grabbable;
    public SnapInteractor snapInteractor;
    public HandGrabInteractable[] handGrabs;
    public GrabInteractable[] controllerGrabs;
    private readonly List<Renderer> outlines = new List<Renderer>();
    public bool OutlineVisible { get; private set; }

    private void Awake()
    {
        if (!outlineMaterial) return;
        // Reuse each original mesh; add only a visual shell, with no extra collider.
        foreach (var source in GetComponentsInChildren<MeshFilter>(true))
        {
            if (!source.sharedMesh || !source.TryGetComponent<MeshRenderer>(out var original)) continue;
            if (original.GetComponentInParent<Grabbable>() != grabbable) continue;
            var shell = new GameObject("InteractionOutline", typeof(MeshFilter), typeof(MeshRenderer));
            shell.layer = source.gameObject.layer;
            shell.transform.SetParent(source.transform, false);
            shell.GetComponent<MeshFilter>().sharedMesh = source.sharedMesh;
            var renderer = shell.GetComponent<MeshRenderer>();
            var materials = new Material[source.sharedMesh.subMeshCount];
            for (int i = 0; i < materials.Length; i++) materials[i] = outlineMaterial;
            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            outlines.Add(renderer);
        }
    }

    private void LateUpdate()
    {
        bool selected = grabbable && grabbable.SelectingPointsCount > 0;
        if (snapInteractor && snapInteractor.State == InteractorState.Select) selected = true;
        foreach (var hand in handGrabs)
            if (hand && hand.State == InteractableState.Select) selected = true;
        foreach (var controller in controllerGrabs)
            if (controller && controller.State == InteractableState.Select) selected = true;
        SetVisible(!selected);
    }

    private void SetVisible(bool visible)
    {
        OutlineVisible = visible;
        foreach (var outline in outlines) if (outline) outline.enabled = visible;
    }

    private void OnDisable() { SetVisible(false); }
}
