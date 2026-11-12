using Oculus.Interaction;
using UnityEngine;

public sealed class CookingFood : MonoBehaviour
{
    public GameObject whole;
    public GameObject pieces;
    public InteractableGlow wholeGlow, piecesGlow;
    public Behaviour[] grabs;
    public Rigidbody body;
    public SnapInteractor snap;
    public SnapInteractable slot;
    public bool Cut { get; private set; }
    public bool Stored { get; private set; }
    public bool CanCut { get; private set; }
    public int Hits { get; private set; }
    private bool canDeposit;
    private float nextHit;
    private float snapStarted = -1f;

    public void ResetForCooking()
    {
        Cut = Stored = CanCut = canDeposit = false;
        Hits = 0;
        if (whole) whole.SetActive(true);
        pieces.SetActive(!whole);
        if (wholeGlow) wholeGlow.HighlightEnabled = false;
        piecesGlow.HighlightEnabled = false;
        EnableGrabs(false);
        snap.enabled = false;
        slot.enabled = false;
        body.isKinematic = true;
        body.useGravity = false;
    }

    public void AllowCut()
    {
        CanCut = true;
        wholeGlow.HighlightEnabled = true;
    }

    public void Strike()
    {
        if (!CanCut || Cut || Time.time < nextHit) return;
        nextHit = Time.time + 0.3f;
        if (++Hits < 3) return;
        Cut = true;
        CanCut = false;
        wholeGlow.HighlightEnabled = false;
        whole.SetActive(false);
        pieces.SetActive(true);
    }

    public void AllowDeposit()
    {
        canDeposit = true;
        pieces.SetActive(true);
        piecesGlow.HighlightEnabled = true;
        EnableGrabs(true);
        snap.enabled = slot.enabled = true;
    }

    private void LateUpdate()
    {
        if (!canDeposit || Stored) return;
        if (snap.State != InteractorState.Select || snap.Interactable != slot) { snapStarted = -1; return; }
        if (snapStarted < 0) snapStarted = Time.time;
        if (Time.time - snapStarted < 1.2f) return;
        if (!slot.PoseForInteractor(snap, out Pose pose) || Vector3.Distance(snap.SnapPose.position, pose.position) > 0.005f) return;
        Stored = true;
        EnableGrabs(false);
        piecesGlow.HighlightEnabled = false;
        body.isKinematic = true;
        body.useGravity = false;
    }

    private void EnableGrabs(bool value)
    {
        foreach (var grab in grabs) if (grab) grab.enabled = value;
    }
}
