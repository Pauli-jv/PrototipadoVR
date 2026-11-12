using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class CookingSequence : MonoBehaviour
{
    public GameObject kitchen;
    public CollectionProgress collection;
    public CookingFood[] foods; // Carne, Papa, Yuca, Platano, Mazorca, Cilantro.
    public CookingFood lid;
    public AudioSource narration;
    public AudioClip[] preparation;
    public AudioClip cooking1, cooking3, cooking6, finalization1, result1, result2;
    public bool Begun { get; private set; }
    public bool Completed { get; private set; }
    public string Stage { get; private set; } = "Esperando llegada a casa";
    public readonly List<string> PlayedClips = new List<string>();

    private void Awake()
    {
        foreach (var food in foods) food.ResetForCooking();
        lid.ResetForCooking();
        kitchen.SetActive(false);
    }

    public void BeginCooking()
    {
        if (Begun || !collection.AllCollected) return;
        Begun = true;
        kitchen.SetActive(true);
        StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        yield return new WaitForSeconds(1.2f);
        while (collection.NarrationPlaying) yield return null;
        for (int i = 0; i < foods.Length; i++)
        {
            Stage = "Cortar " + foods[i].name;
            foods[i].AllowCut();
            if (i == 0) yield return Speak(preparation[0]);
            if (i == 1) yield return Speak(preparation[1]);
            if (i == 4) yield return Speak(preparation[2]);
            yield return new WaitUntil(() => foods[i].Cut);
        }
        Stage = "Olla: carne y mazorca";
        foods[0].AllowDeposit(); foods[4].AllowDeposit();
        yield return Speak(cooking1);
        yield return new WaitUntil(() => foods[0].Stored && foods[4].Stored);
        Stage = "Olla: papa, yuca y platano";
        for (int i = 1; i <= 3; i++) foods[i].AllowDeposit();
        yield return Speak(cooking3);
        yield return new WaitUntil(() => foods[1].Stored && foods[2].Stored && foods[3].Stored);
        Stage = "Olla: cilantro";
        foods[5].AllowDeposit();
        yield return new WaitUntil(() => foods[5].Stored);
        Stage = "Tapar olla";
        lid.AllowDeposit();
        yield return Speak(cooking6);
        yield return new WaitUntil(() => lid.Stored);
        Stage = "Finalizacion";
        yield return Speak(finalization1);
        yield return Speak(result1);
        yield return Speak(result2);
        Completed = true;
        Stage = "Completado";
    }

    private IEnumerator Speak(AudioClip clip)
    {
        if (!clip) yield break;
        PlayedClips.Add(clip.name);
        narration.clip = clip;
        narration.Play();
        do { yield return null; } while (narration.isPlaying || AudioListener.pause);
    }
}
