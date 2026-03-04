using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Tutorial Manager - Add this check to your player input scripts:
/// if (TutorialManager.IsInputBlocked) return;
/// 
/// Place this at the start of Update() or any input handling method.
/// </summary>
public class TutorialManager : MonoBehaviour
{
    // Static flag that your player scripts can check
    public static bool IsInputBlocked { get; private set; } = false;

    [System.Serializable]
    public class TutorialStep
    {
        public string stepName;
        public BoxCollider triggerCollider;
        public Image tutorialImage;
        [Tooltip("Should this trigger only activate once?")]
        public bool triggerOnce = true;
        [HideInInspector]
        public bool hasTriggered = false;
        [HideInInspector]
        public CanvasGroup canvasGroup;
    }

    [Header("Tutorial Steps")]
    public TutorialStep[] tutorialSteps;

    [Header("Animation Settings")]
    [Range(0f, 1f)]
    public float slowMotionScale = 0.2f;
    public float animationDuration = 0.5f;
    public AnimationCurve easeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Player Settings")]
    public string playerTag = "Player";

    [Header("Optional: Disable Specific Scripts")]
    [Tooltip("Drag your player controller scripts here to disable them during tutorials")]
    public MonoBehaviour[] scriptsToDisable;

    [Header("Dismiss Settings")]
    public KeyCode dismissKey = KeyCode.Space;
    public bool autoDismissAfterDelay = false;
    public float autoDismissDelay = 3f;

    private TutorialStep currentStep;
    private bool isTutorialActive = false;
    private Coroutine dismissCoroutine;

    private void Start()
    {
        // Initialize all tutorial images as hidden
        foreach (var step in tutorialSteps)
        {
            if (step.tutorialImage != null)
            {
                // Get or add CanvasGroup for fading
                step.canvasGroup = step.tutorialImage.GetComponent<CanvasGroup>();
                if (step.canvasGroup == null)
                {
                    step.canvasGroup = step.tutorialImage.gameObject.AddComponent<CanvasGroup>();
                }

                step.canvasGroup.alpha = 0f;
                step.tutorialImage.gameObject.SetActive(false);
            }

            if (step.triggerCollider != null)
            {
                step.triggerCollider.isTrigger = true;

                // Add TutorialTrigger component to each collider
                TutorialTrigger trigger = step.triggerCollider.gameObject.GetComponent<TutorialTrigger>();
                if (trigger == null)
                {
                    trigger = step.triggerCollider.gameObject.AddComponent<TutorialTrigger>();
                }
                trigger.Initialize(this, step);
            }
        }
    }

    private void Update()
    {
        // Allow player to dismiss tutorial with key press
        if (isTutorialActive && Input.GetKeyDown(dismissKey))
        {
            DismissTutorial();
        }
    }

    public void TriggerTutorial(TutorialStep step)
    {
        // Check if already triggered and set to trigger once
        if (step.triggerOnce && step.hasTriggered)
            return;

        // Don't trigger if another tutorial is active
        if (isTutorialActive)
            return;

        step.hasTriggered = true;
        currentStep = step;
        StartCoroutine(ShowTutorial(step));
    }

    private IEnumerator ShowTutorial(TutorialStep step)
    {
        isTutorialActive = true;
        IsInputBlocked = true; // Block input globally

        // Disable specified scripts if any
        DisablePlayerScripts();

        // Slow down time
        Time.timeScale = slowMotionScale;
        Time.fixedDeltaTime = 0.02f * Time.timeScale;

        // Activate and fade in image
        if (step.tutorialImage != null && step.canvasGroup != null)
        {
            step.tutorialImage.gameObject.SetActive(true);
            yield return StartCoroutine(FadeImage(step.canvasGroup, 0f, 1f));
        }

        // Auto-dismiss if enabled
        if (autoDismissAfterDelay)
        {
            dismissCoroutine = StartCoroutine(AutoDismissAfterDelay());
        }
    }

    private IEnumerator AutoDismissAfterDelay()
    {
        yield return new WaitForSecondsRealtime(autoDismissDelay);
        DismissTutorial();
    }

    public void DismissTutorial()
    {
        if (!isTutorialActive || currentStep == null)
            return;

        if (dismissCoroutine != null)
        {
            StopCoroutine(dismissCoroutine);
            dismissCoroutine = null;
        }

        StartCoroutine(HideTutorial(currentStep));
    }

    private IEnumerator HideTutorial(TutorialStep step)
    {
        // Fade out image
        if (step.tutorialImage != null && step.canvasGroup != null)
        {
            yield return StartCoroutine(FadeImage(step.canvasGroup, 1f, 0f));
            step.tutorialImage.gameObject.SetActive(false);
        }

        // Restore time scale
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;

        // Re-enable scripts
        EnablePlayerScripts();

        IsInputBlocked = false; // Unblock input globally
        isTutorialActive = false;
        currentStep = null;
    }

    private void DisablePlayerScripts()
    {
        if (scriptsToDisable != null && scriptsToDisable.Length > 0)
        {
            foreach (var script in scriptsToDisable)
            {
                if (script != null && script.enabled)
                {
                    script.enabled = false;
                }
            }
        }
    }

    private void EnablePlayerScripts()
    {
        if (scriptsToDisable != null && scriptsToDisable.Length > 0)
        {
            foreach (var script in scriptsToDisable)
            {
                if (script != null)
                {
                    script.enabled = true;
                }
            }
        }
    }

    private IEnumerator FadeImage(CanvasGroup canvasGroup, float startAlpha, float endAlpha)
    {
        float elapsed = 0f;

        while (elapsed < animationDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / animationDuration;
            float curveValue = easeCurve.Evaluate(t);

            canvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, curveValue);

            yield return null;
        }

        canvasGroup.alpha = endAlpha;
    }
}

// Separate component that gets added to trigger colliders
public class TutorialTrigger : MonoBehaviour
{
    private TutorialManager manager;
    private TutorialManager.TutorialStep step;

    public void Initialize(TutorialManager tutorialManager, TutorialManager.TutorialStep tutorialStep)
    {
        manager = tutorialManager;
        step = tutorialStep;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(manager.playerTag))
        {
            manager.TriggerTutorial(step);
        }
    }
}