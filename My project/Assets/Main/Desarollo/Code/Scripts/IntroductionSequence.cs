using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public sealed class IntroductionSequence : MonoBehaviour
{
    [Serializable]
    public sealed class Step
    {
        public CanvasGroup introduction;
        public CanvasGroup image;
    }

    [SerializeField] private Step[] steps = Array.Empty<Step>();
    [SerializeField, Min(0f)] private float fadeDuration = 0.4f;
    [SerializeField] private UnityEvent onCompleted = new UnityEvent();
    private int current;
    private bool transitioning;

    public int CurrentStep => current;
    public bool IsTransitioning => transitioning;

    private void Awake()
    {
        current = 0;
        for (int i = 0; i < steps.Length; i++) SetStep(i, i == current, 1f, i == current);
    }

    public void Continue()
    {
        if (!isActiveAndEnabled || transitioning || current >= steps.Length) return;
        StartCoroutine(Advance());
    }

    private IEnumerator Advance()
    {
        transitioning = true;
        SetStep(current, true, 1f, false);
        yield return Fade(current, 1f, 0f);
        SetStep(current, false, 0f, false);
        current++;
        if (current < steps.Length)
        {
            SetStep(current, true, 0f, false);
            yield return Fade(current, 0f, 1f);
            SetStep(current, true, 1f, true);
        }
        transitioning = false;
        if (current == steps.Length) onCompleted.Invoke();
    }

    private IEnumerator Fade(int index, float from, float to)
    {
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float alpha = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / fadeDuration)));
            SetStep(index, true, alpha, false);
            yield return null;
        }
        SetStep(index, true, to, false);
    }

    private void SetStep(int index, bool visible, float alpha, bool interactive)
    {
        SetGroup(steps[index].introduction, visible, alpha, interactive);
        SetGroup(steps[index].image, visible, alpha, false);
    }

    private static void SetGroup(CanvasGroup group, bool visible, float alpha, bool interactive)
    {
        if (!group) return;
        group.alpha = alpha;
        group.interactable = interactive;
        group.blocksRaycasts = interactive;
        group.gameObject.SetActive(visible);
    }
}
