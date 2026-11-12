using System.Collections.Generic;
using Oculus.Interaction;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public sealed class MacheteCollisionAudio : MonoBehaviour
{
    public Grabbable machete;
    private BoxCollider blade;
    private readonly Collider[] overlaps = new Collider[48];
    private readonly RaycastHit[] sweepHits = new RaycastHit[48];
    private readonly Dictionary<MacheteHarvestTarget, Contact> contacts = new Dictionary<MacheteHarvestTarget, Contact>();
    private sealed class Contact
    {
        public float seen, hit;
        public Vector3 position, direction;
    }
    private Vector3 previousCenter, direction;
    private float speed;
    private bool wasHeld;

    private void Awake() { blade = GetComponent<BoxCollider>(); }
    private void OnTriggerEnter(Collider other) { Detect(other); }
    private void OnTriggerStay(Collider other) { Detect(other); }

    private void FixedUpdate()
    {
        bool held = machete && machete.SelectingPointsCount > 0;
        Vector3 center = transform.TransformPoint(blade.center);
        if (!held) { wasHeld = false; contacts.Clear(); previousCenter = center; return; }
        Vector3 delta = wasHeld ? center - previousCenter : Vector3.zero;
        speed = delta.magnitude / Mathf.Max(Time.fixedDeltaTime, 0.001f);
        direction = delta.sqrMagnitude > 0.000001f ? delta.normalized : Vector3.zero;
        Vector3 scale = transform.lossyScale;
        Vector3 half = Vector3.Scale(blade.size * 0.5f, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
        half = Vector3.Max(half, Vector3.one * 0.015f);
        // Explicit queries also cover transform-driven hand motion and contact already present on grab.
        int count = Physics.OverlapBoxNonAlloc(center, half, overlaps, transform.rotation, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < count; i++) Detect(overlaps[i]);
        // Do not turn long teleports or distance-grab travel into a sword stroke.
        if (wasHeld && delta.magnitude > 0.001f && delta.magnitude < 1.5f)
        {
            count = Physics.BoxCastNonAlloc(previousCenter, half, direction, sweepHits, transform.rotation,
                delta.magnitude, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++) Detect(sweepHits[i].collider);
        }
        previousCenter = center;
        wasHeld = true;
    }

    private void Detect(Collider other)
    {
        if (!machete || machete.SelectingPointsCount == 0 || !other) return;
        var target = other.GetComponentInParent<MacheteHarvestTarget>();
        if (!target || !target.isActiveAndEnabled || target.Harvested) return;
        float now = Time.unscaledTime;
        Vector3 point = transform.TransformPoint(blade.center);
        bool fresh = !contacts.TryGetValue(target, out Contact contact);
        if (fresh) { contact = new Contact(); contacts.Add(target, contact); }
        bool reentered = !fresh && now - contact.seen > 0.15f;
        bool newStroke = !fresh && now - contact.hit >= target.hitCooldown && speed > 0.4f &&
            Vector3.Distance(point, contact.position) >= 0.12f &&
            (contact.direction.sqrMagnitude < 0.01f || Vector3.Dot(direction, contact.direction) < -0.25f);
        contact.seen = now;
        if (!fresh && !reentered && !newStroke) return;
        int before = target.HitCount;
        target.Strike();
        if (target.HitCount != before)
        {
            contact.hit = now;
            contact.position = point;
            contact.direction = direction;
        }
    }
}
