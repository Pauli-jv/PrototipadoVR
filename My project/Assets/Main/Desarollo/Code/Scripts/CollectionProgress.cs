using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class CollectionProgress : MonoBehaviour
{
    [Serializable]
    public sealed class IngredientDialogue
    {
        public string ingredient;
        public AudioClip[] clips;
        public UnityEngine.UI.Toggle checklist;
    }
    public IngredientDialogue[] ingredients;
    public AudioSource narration;
    public AudioSource effects;
    public AudioClip notification;
    public AudioClip collectionFinished;
    public UnityEngine.UI.Button homeButton;
    private readonly HashSet<string> collected = new HashSet<string>();
    private readonly Queue<AudioClip> pending = new Queue<AudioClip>();
    private Coroutine playback;
    public bool AllCollected => ingredients != null && collected.Count == ingredients.Length;
    public int CollectedCount => collected.Count;
    public bool NarrationPlaying => playback != null;
    public readonly List<string> PlayedClips = new List<string>();

    private void Awake()
    {
        homeButton.interactable = false;
        foreach (var entry in ingredients)
            UpdateChecklist(entry, false);
    }

    public void Deposited(string ingredient)
    {
        if (notification) effects.PlayOneShot(notification);
        var entry = Array.Find(ingredients, item => item.ingredient == ingredient);
        if (entry == null || !collected.Add(ingredient)) return;
        UpdateChecklist(entry, true);
        foreach (var clip in entry.clips) if (clip) pending.Enqueue(clip);
        if (AllCollected)
        {
            homeButton.interactable = true;
            if (collectionFinished) pending.Enqueue(collectionFinished);
        }
        if (playback == null) playback = StartCoroutine(PlayQueue());
    }

    private static void UpdateChecklist(IngredientDialogue entry, bool deposited)
    {
        if (!entry.checklist) return;
        entry.checklist.SetIsOnWithoutNotify(deposited);
        entry.checklist.interactable = false;
        foreach (var label in entry.checklist.GetComponentsInChildren<TMPro.TMP_Text>(true))
            label.fontStyle = deposited
                ? label.fontStyle | TMPro.FontStyles.Strikethrough
                : label.fontStyle & ~TMPro.FontStyles.Strikethrough;
    }

    private IEnumerator PlayQueue()
    {
        while (pending.Count > 0)
        {
            narration.clip = pending.Dequeue();
            PlayedClips.Add(narration.clip.name);
            narration.Play();
            do { yield return null; } while (narration.isPlaying || AudioListener.pause);
            yield return new WaitForSecondsRealtime(0.2f);
        }
        playback = null;
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        if (narration) narration.Stop();
        playback = null;
    }
}
