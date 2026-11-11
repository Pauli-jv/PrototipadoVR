using System.Collections;
using Oculus.Interaction.Locomotion;
using UnityEngine;

public sealed class GrandmotherArrival : MonoBehaviour
{
    public FirstPersonLocomotor locomotor;
    public Behaviour[] locomotionConnections;
    public Transform destination;
    public Transform eyes;
    public OVRScreenFade screenFade;
    public AudioSource voice;
    public AudioClip[] dialogue;
    public GameObject gestureHint;
    public JourneyPanel journal;
    public float fadeSeconds = 0.7f;
    private bool started;
    private bool locked;
    private bool mapMovementLocked;
    private bool movementStateCaptured;
    private bool[] connectionStates;
    public bool DialogueFinished { get; private set; }
    public bool IsTransitioning => locked;

    public void TeleportTo(Transform target)
    {
        if (!DialogueFinished || locked || !target || !isActiveAndEnabled) return;
        StartCoroutine(MapTeleport(target));
    }

    private IEnumerator MapTeleport(Transform target)
    {
        LockLocomotion();
        journal.Close();
        yield return TeleportUnderBlack(target);
        RestoreLocomotion();
    }

    private IEnumerator TeleportUnderBlack(Transform target)
    {
        yield return Fade(0f, 1f);
        locomotor.HandleLocomotionEvent(new LocomotionEvent(0,
            new Pose(target.position, Quaternion.Euler(0f, target.eulerAngles.y, 0f)),
            LocomotionEvent.TranslationType.Absolute, LocomotionEvent.RotationType.Absolute));
        // The Meta locomotor applies its queued teleport during LateUpdate.
        yield return null;
        yield return null;
        yield return Fade(1f, 0f);
    }

    public void BeginArrival()
    {
        if (started) return;
        if (!locomotor || !destination || !screenFade || !voice || dialogue.Length == 0)
        {
            Debug.LogError("GrandmotherArrival: faltan referencias de la secuencia.", this);
            return;
        }
        started = true;
        StartCoroutine(Arrival());
    }

    private IEnumerator Arrival()
    {
        LockLocomotion();
        yield return TeleportUnderBlack(destination);
        foreach (var clip in dialogue)
        {
            if (!clip) continue;
            voice.clip = clip;
            voice.Play();
            // Wait for playback, including a paused application, rather than a guessed duration.
            do { yield return null; } while (voice.isPlaying || AudioListener.pause);
            yield return new WaitForSecondsRealtime(0.25f);
        }
        RestoreLocomotion();
        DialogueFinished = true;
        journal.Unlock();
        PlaceInFront(gestureHint.transform, 1.3f);
        gestureHint.SetActive(true);
    }

    private IEnumerator Fade(float from, float to)
    {
        float elapsed = 0f;
        while (elapsed < fadeSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            screenFade.SetExplicitFade(Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / fadeSeconds)));
            yield return null;
        }
        screenFade.SetExplicitFade(to);
    }

    private void LockLocomotion()
    {
        locked = true;
        RefreshLocomotionLock();
    }

    public void SetMapLocomotionBlocked(bool blocked)
    {
        mapMovementLocked = blocked;
        RefreshLocomotionLock();
    }

    private void RefreshLocomotionLock()
    {
        bool shouldLock = locked || mapMovementLocked;
        if (shouldLock == movementStateCaptured || !locomotor) return;
        if (!shouldLock)
        {
            locomotor.EnableMovement();
            for (int i = 0; i < locomotionConnections.Length; i++)
                if (locomotionConnections[i]) locomotionConnections[i].enabled = connectionStates[i];
            movementStateCaptured = false;
            return;
        }
        connectionStates = new bool[locomotionConnections.Length];
        for (int i = 0; i < locomotionConnections.Length; i++)
        {
            if (!locomotionConnections[i]) continue;
            connectionStates[i] = locomotionConnections[i].enabled;
            locomotionConnections[i].enabled = false;
        }
        locomotor.DisableMovement();
        movementStateCaptured = true;
    }

    private void RestoreLocomotion()
    {
        locked = false;
        RefreshLocomotionLock();
    }

    public void PlaceInFront(Transform panel, float distance)
    {
        Vector3 forward = Vector3.ProjectOnPlane(eyes.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
        panel.SetPositionAndRotation(eyes.position + forward * distance,
            Quaternion.LookRotation(forward, Vector3.up));
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        if (voice) voice.Stop();
        if (screenFade) screenFade.SetExplicitFade(0f);
        mapMovementLocked = false;
        RestoreLocomotion();
    }
}

