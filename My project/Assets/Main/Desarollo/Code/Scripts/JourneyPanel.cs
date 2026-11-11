using UnityEngine;
using UnityEngine.Video;

public sealed class JourneyPanel : MonoBehaviour
{
    public GrandmotherArrival arrival;
    public GameObject panel;
    public GameObject ingredientsPage;
    public GameObject mapPage;
    public GameObject hint;
    public Camera mapCamera;
    public RectTransform playerMarker;
    public Transform eyes;
    public VideoPlayer tutorialVideo;
    private bool unlocked;
    private float nextGestureTime;
    private RenderTexture mapTexture;
    private RenderTexture videoTexture;
    public UnityEngine.UI.RawImage mapImage;
    public UnityEngine.UI.RawImage videoImage;
    [Min(0.5f)] public float panelDistance = 1.5f;
    public Transform[] mapDestinations = new Transform[5];
    public TMPro.TMP_Text footer;

    private void Awake()
    {
        mapTexture = new RenderTexture(1024, 1024, 16);
        mapTexture.name = "Mapa del terreno";
        mapCamera.targetTexture = mapTexture;
        mapImage.texture = mapTexture;
        videoTexture = new RenderTexture(640, 360, 0);
        videoTexture.name = "Tutorial de poses Meta";
        tutorialVideo.targetTexture = videoTexture;
        videoImage.texture = videoTexture;
        panel.SetActive(false);
        hint.SetActive(false);
        mapCamera.gameObject.SetActive(false);
    }

    public void Unlock() { unlocked = true; }

    // Connected to the unmodified Meta ThumbsUpPose selector on both hands.
    public void OnThumbsUp()
    {
        if (!unlocked || arrival.IsTransitioning || Time.unscaledTime < nextGestureTime) return;
        nextGestureTime = Time.unscaledTime + 0.8f;
        if (panel.activeSelf) { Close(); return; }
        hint.SetActive(false);
        arrival.PlaceInFront(panel.transform, panelDistance);
        panel.SetActive(true);
        ShowIngredients();
    }

    public void Close()
    {
        arrival.SetMapLocomotionBlocked(false);
        panel.SetActive(false);
        mapCamera.gameObject.SetActive(false);
    }

    public void ShowIngredients()
    {
        arrival.SetMapLocomotionBlocked(false);
        if (footer) footer.text = "Marca los ingredientes que ya recogiste · Pulgar arriba para abrir o cerrar";
        ingredientsPage.SetActive(true);
        mapPage.SetActive(false);
        mapCamera.gameObject.SetActive(false);
    }

    public void ShowMap()
    {
        arrival.SetMapLocomotionBlocked(true);
        if (footer) footer.text = "Selecciona un ingrediente para viajar · Pulgar arriba para abrir o cerrar";
        ingredientsPage.SetActive(false);
        mapPage.SetActive(true);
        mapCamera.gameObject.SetActive(true);
    }

    // Order in the inspector: Yuca, Papa, Cilantro, Mazorca, Carne.
    public void TravelToIngredient(int index)
    {
        if (!unlocked || index < 0 || index >= mapDestinations.Length) return;
        arrival.TeleportTo(mapDestinations[index]);
    }

    private void LateUpdate()
    {
        if (!mapPage.activeInHierarchy) return;
        Vector3 point = mapCamera.WorldToViewportPoint(eyes.position);
        playerMarker.anchorMin = playerMarker.anchorMax = new Vector2(Mathf.Clamp01(point.x), Mathf.Clamp01(point.y));
        playerMarker.anchoredPosition = Vector2.zero;
    }

    private void OnDestroy()
    {
        if (mapCamera) mapCamera.targetTexture = null;
        if (tutorialVideo) tutorialVideo.targetTexture = null;
        if (mapTexture) { mapTexture.Release(); Destroy(mapTexture); }
        if (videoTexture) { videoTexture.Release(); Destroy(videoTexture); }
    }

    private void OnDisable()
    {
        if (arrival) arrival.SetMapLocomotionBlocked(false);
    }
}
