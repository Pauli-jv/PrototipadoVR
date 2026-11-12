using Oculus.Interaction;
using UnityEngine;

public sealed class MacheteCollisionAudio : MonoBehaviour
{
    public Grabbable machete;
    private void OnTriggerEnter(Collider other)
    {
        if (!machete || machete.SelectingPointsCount == 0) return;
        var target = other.GetComponentInParent<MacheteHarvestTarget>();
        if (target) target.Strike();
    }
}
