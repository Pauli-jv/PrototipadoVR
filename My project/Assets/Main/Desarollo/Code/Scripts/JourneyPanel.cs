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
    public CollectionProgress collection;
    public Transform homeDestination;
    public UnityEngine.UI.Button homeButton;
    public Texture2D cachedMap;

    private void Awake()
    {
        mapCamera.enabled = false;
        if (cachedMap) mapImage.texture = cachedMap;
        else
        {
            mapTexture = new RenderTexture(512, 512, 16);
            mapTexture.name = "Mapa del terreno";
            mapCamera.targetTexture = mapTexture;
            mapImage.texture = mapTexture;
        }
        videoTexture = new RenderTexture(640, 360, 0);
        videoTexture.name = "Tutorial de poses Meta";
        tutorialVideo.targetTexture = videoTexture;
        videoImage.texture = videoTexture;
        panel.SetActive(false);
        hint.SetActive(false);
        mapCamera.gameObject.SetActive(false);
    }

    public void Unlock() { unlocked = true; }

    // Connected to Meta's ThumbsDownPose on both hands.
    public void OnThumbsDown()
    {
        if (!unlocked || arrival.IsTransitioning || Time.unscaledTime < nextGestureTime) return;
        nextGestureTime = Time.unscaledTime + 0.8f;
        if (panel.activeSelf) { Close(); return; }
        hint.SetActive(false);
        arrival.PlaceInFront(panel.transform, panelDistance);
        panel.SetActive(true);
        ShowIngredients();
    }

    // Retained for older editor setup helpers; the scene uses Thumbs Down.
    public void OnThumbsUp() { OnThumbsDown(); }

    public void Close()
    {
        arrival.SetMapLocomotionBlocked(false);
        panel.SetActive(false);
        mapCamera.gameObject.SetActive(false);
    }

    public void ShowIngredients()
    {
        arrival.SetMapLocomotionBlocked(false);
        if (footer) footer.text = "Guarda cada ingrediente en su canasta · Pulgar abajo para abrir o cerrar";
        ingredientsPage.SetActive(true);
        mapPage.SetActive(false);
        mapCamera.gameObject.SetActive(false);
    }

    public void ShowMap()
    {
        arrival.SetMapLocomotionBlocked(true);
        if (footer) footer.text = "Selecciona un destino · Casa se habilita al completar la recolección";
        ingredientsPage.SetActive(false);
        mapPage.SetActive(true);
        mapCamera.gameObject.SetActive(true);
        if (!cachedMap) mapCamera.Render();
    }

    // Order in the inspector: Yuca, Papa, Cilantro, Mazorca, Carne.
    public void TravelToIngredient(int index)
    {
        if (!unlocked || index < 0 || index >= mapDestinations.Length) return;
        arrival.TeleportTo(mapDestinations[index]);
    }

    public void TravelHome()
    {
        if (unlocked && collection && collection.AllCollected) arrival.TeleportTo(homeDestination);
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
