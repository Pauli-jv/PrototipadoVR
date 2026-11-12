using UnityEngine;

// Run after OVRScreenFade.Start has created its renderer and material.
[DefaultExecutionOrder(1000)]
[RequireComponent(typeof(OVRScreenFade))]
public sealed class InitializeArrivalFade : MonoBehaviour
{
    // Explicit asset reference includes the shader used by OVRScreenFade's Shader.Find in builds.
    public Shader fadeShader;

    private void Start()
    {
        var fade = GetComponent<OVRScreenFade>();
        fade.SetExplicitFade(0f);
        var renderer = GetComponent<MeshRenderer>();
        if (renderer)
        {
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }
}
