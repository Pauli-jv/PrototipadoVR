using UnityEngine;

public sealed class MacheteHarvestTarget : MonoBehaviour
{
    public CollectibleIngredient ingredient;
    public AudioSource effects;
    public AudioClip hitSound;
    public bool hideAfterHarvest;
    public int requiredHits = 2;
    public float hitCooldown = 0.4f;
    private float nextHit;
    public int HitCount { get; private set; }
    public bool Harvested => HitCount >= requiredHits;

    public void Strike()
    {
        if (Harvested || Time.unscaledTime < nextHit) return;
        nextHit = Time.unscaledTime + hitCooldown;
        HitCount++;
        if (hitSound) effects.PlayOneShot(hitSound);
        if (!Harvested) return;
        ingredient.ReleaseHarvest();
        if (hideAfterHarvest) gameObject.SetActive(false);
    }
}
