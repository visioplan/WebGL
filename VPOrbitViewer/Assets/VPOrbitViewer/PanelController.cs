using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class PanelController : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Fade")]
    [Min(0f)][SerializeField] private float fadeDuration = 0.5f;

    [Header("Behavior")]
    [Tooltip("If true, ignores clicks over UI (so buttons won't trigger the first click).")]
    [SerializeField] private bool ignoreClicksOverUI = false;

    private bool _hasTriggered = false;
    private Coroutine _fadeRoutine;

    void Reset()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        fadeDuration = 0.5f;
    }

    void Awake()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (canvasGroup == null)
            Debug.LogError($"{nameof(PanelController)} requires a CanvasGroup reference.", this);
    }

    void Update()
    {
        if (_hasTriggered || canvasGroup == null) return;

        if (Input.GetMouseButtonDown(0))
        {
            if (ignoreClicksOverUI && IsPointerOverUI()) return;

            _hasTriggered = true;
            FadeOut();
        }
    }

    public void FadeOut()
    {
        if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
        _fadeRoutine = StartCoroutine(FadeCanvasGroup(canvasGroup, canvasGroup.alpha, 0f, fadeDuration, disableInteractionAtEnd: true));
    }

    private IEnumerator FadeCanvasGroup(CanvasGroup cg, float from, float to, float duration, bool disableInteractionAtEnd)
    {
        // Stop interactions immediately so the click doesn't also press UI behind it.
        cg.blocksRaycasts = false;
        cg.interactable = false;

        if (duration <= 0f)
        {
            cg.alpha = to;
            if (disableInteractionAtEnd) { cg.blocksRaycasts = false; cg.interactable = false; }
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime; // unaffected by timeScale
            float u = Mathf.Clamp01(t / duration);
            // Smoothstep for a nicer fade curve
            float eased = u * u * (3f - 2f * u);
            cg.alpha = Mathf.Lerp(from, to, eased);
            yield return null;
        }

        cg.alpha = to;

        if (disableInteractionAtEnd)
        {
            cg.blocksRaycasts = false;
            cg.interactable = false;
        }
    }

    private bool IsPointerOverUI()
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        // Requires EventSystem in scene. If none exists, just return false.
        return UnityEngine.EventSystems.EventSystem.current != null &&
               UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
#else
        return false;
#endif
    }
}
