using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using UnityEngine;

public sealed class CollectibleIngredient : MonoBehaviour
{
    public string ingredient;
    public bool Available = true;
    public Grabbable grabbable;
    public SnapInteractor snap;
    public Rigidbody body;
    public Behaviour[] grabInteractions;
    public bool Stored { get; private set; }
    private float snapStarted = -1f;

    private void Start() { SetGrabEnabled(Available); }

    public void ReleaseHarvest()
    {
        if (Available) return;
        Available = true;
        transform.SetParent(null, true);
        gameObject.SetActive(true);
        SetGrabEnabled(true);
        body.isKinematic = false;
        body.useGravity = true;
        body.WakeUp();
    }

    private void LateUpdate()
    {
        if (Stored || !Available || !snap) return;
        if (snap.State != InteractorState.Select || !snap.Interactable) { snapStarted = -1f; return; }
        if (snapStarted < 0) snapStarted = Time.time;
        if (Time.time - snapStarted < 1.2f) return;
        var slot = snap.Interactable.GetComponent<BasketDepositSlot>();
        if (!slot || !slot.Accepts(this)) return;
        if (!snap.Interactable.PoseForInteractor(snap, out Pose pose) || Vector3.Distance(snap.SnapPose.position, pose.position) > 0.02f) return;
        Stored = true;
        // Keep the SnapInteractor selecting its slot; disable every path that could pull it out.
        SetGrabEnabled(false);
        body.isKinematic = true;
        body.useGravity = false;
        slot.Store(this);
    }

    private void SetGrabEnabled(bool value)
    {
        foreach (var interaction in grabInteractions) if (interaction) interaction.enabled = value;
    }
}
