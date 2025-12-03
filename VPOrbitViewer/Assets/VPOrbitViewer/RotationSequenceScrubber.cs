using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class RotationSequenceScrubber : MonoBehaviour
{
    [Header("Source Frames")]
    [Tooltip("Sprites in Reihenfolge der linksgerichteten 360°-Rotation.")]
    public Sprite[] frames;

    [Header("Target Output")]
    public Image targetImage;

    [Header("Scroll Input")]
    [Tooltip("Wie viele Frames pro Scroll-Notch weitergeschaltet wird (kann auch < 1 sein).")]
    public float framesPerScrollStep = 1f;
    public bool invertScroll = false;

    [Header("Mouse Drag Input (LMB hold)")]
    [Tooltip("Wie viele Frames pro Pixel Mausbewegung (X) gescrubbt werden.")]
    public float framesPerPixelDrag = 0.15f;

    [Tooltip("Wenn true: Maus nach rechts = vorwärts in der Sequenz. Sonst invertiert.")]
    public bool dragRightGoesForward = true;

    [Tooltip("Optional: Nur drehen, wenn der Mauszeiger über dem targetImage ist (UI Raycast).")]
    public bool requirePointerOverImage = false;

    [Header("Behavior")]
    public bool wrap = true;
    public bool applyOnEnable = true;
    public int startIndex = 0;

    // internal state
    private float _frameCursor = 0f;
    private int _lastAppliedIndex = -1;

    // drag state
    private bool _dragging = false;
    private Vector3 _lastMousePos;

    void Reset()
    {
        targetImage = GetComponent<Image>();
        framesPerScrollStep = 1f;
        framesPerPixelDrag = 0.15f;
        wrap = true;
        invertScroll = false;
        dragRightGoesForward = true;
        requirePointerOverImage = false;
    }

    void OnEnable()
    {
        if (applyOnEnable)
            SetIndex(startIndex, force: true);
    }

    void Update()
    {
        if (frames == null || frames.Length == 0 || targetImage == null) return;

        HandleScroll();
        HandleMouseDrag();
    }

    private void HandleScroll()
    {
        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) < 0.0001f) return;

        float dir = invertScroll ? -1f : 1f;

        // Linksgerichtete Rotation: standardmäßig scroll up => vorwärts
        _frameCursor += scroll * dir * framesPerScrollStep;
        ApplyCursorToFrame();
    }

    private void HandleMouseDrag()
    {
        // Drag starten
        if (Input.GetMouseButtonDown(0))
        {
            if (!requirePointerOverImage || IsPointerOverTargetImage())
            {
                _dragging = true;
                _lastMousePos = Input.mousePosition;
            }
        }

        // Drag beenden
        if (Input.GetMouseButtonUp(0))
        {
            _dragging = false;
        }

        if (!_dragging) return;

        Vector3 current = Input.mousePosition;
        float deltaX = current.x - _lastMousePos.x;
        _lastMousePos = current;

        if (Mathf.Abs(deltaX) < 0.0001f) return;

        float dir = dragRightGoesForward ? 1f : -1f;

        // Maus nach rechts/links scrubbt Frames
        _frameCursor += deltaX * dir * framesPerPixelDrag;
        ApplyCursorToFrame();
    }

    private bool IsPointerOverTargetImage()
    {
        // Sehr lightweight Check: innerhalb RectTransform Bounds
        // (Kein EventSystem/Raycast nötig)
        RectTransform rt = targetImage.rectTransform;
        return RectTransformUtility.RectangleContainsScreenPoint(rt, Input.mousePosition, null);
    }

    private void ApplyCursorToFrame()
    {
        int count = frames.Length;

        if (wrap)
        {
            _frameCursor %= count;
            if (_frameCursor < 0f) _frameCursor += count;
        }
        else
        {
            _frameCursor = Mathf.Clamp(_frameCursor, 0f, count - 1);
        }

        // Round für "snap" auf Frames; wenn du lieber weicher willst, nimm Floor statt Round.
        int index = Mathf.RoundToInt(_frameCursor) % count;
        if (index < 0) index += count;

        if (index != _lastAppliedIndex)
        {
            targetImage.sprite = frames[index];
            _lastAppliedIndex = index;
        }
    }

    public void SetIndex(int index, bool force = false)
    {
        if (frames == null || frames.Length == 0 || targetImage == null) return;

        int count = frames.Length;

        if (wrap)
        {
            index %= count;
            if (index < 0) index += count;
        }
        else
        {
            index = Mathf.Clamp(index, 0, count - 1);
        }

        _frameCursor = index;

        if (force || index != _lastAppliedIndex)
        {
            targetImage.sprite = frames[index];
            _lastAppliedIndex = index;
        }
    }

    public int GetCurrentIndex()
    {
        if (frames == null || frames.Length == 0) return 0;
        int idx = Mathf.RoundToInt(_frameCursor) % frames.Length;
        if (idx < 0) idx += frames.Length;
        return idx;
    }
}
