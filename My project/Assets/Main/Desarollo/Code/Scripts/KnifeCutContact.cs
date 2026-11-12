using Oculus.Interaction;
using UnityEngine;

public sealed class KnifeCutContact : MonoBehaviour
{
    public Grabbable knife;
    private void OnTriggerEnter(Collider other)
    {
        if (!knife || knife.SelectingPointsCount == 0) return;
        var target = other.GetComponentInParent<CookingCutTarget>();
        if (target) target.food.Strike();
    }
}
