using UnityEngine;

[DisallowMultipleComponent]
public class OrbitalCamera : MonoBehaviour
{
    [Header("Center / Target")]
    public Transform center;
    public Vector3 centerOffset = Vector3.zero;

    [Header("Distance / Zoom")]
    public float distance = 8f;
    public float minDistance = 2f;
    public float maxDistance = 200f;
    public float zoomSpeed = 4f;
    public bool invertZoom = false;

    [Header("Orbit (Yaw/Pitch)")]
    public float yaw = 0f;          // left/right around Y axis
    public float pitch = 20f;       // up/down
    public float minPitch = -20f;
    public float maxPitch = 80f;
    public float orbitSensitivity = 180f; // degrees per second at full input
    public bool invertY = false;

    [Header("Panning (moves center)")]
    public bool allowPan = true;
    public float panSpeed = 1.0f; // world units per second at full input
    public bool panInLocalPlane = true; // pan along camera right/forward-on-ground

    [Header("Smoothing")]
    public bool smooth = true;
    public float positionDamp = 12f;
    public float rotationDamp = 12f;

    [Header("Collision (optional)")]
    public bool collision = false;
    public LayerMask collisionMask = ~0;
    public float collisionRadius = 0.2f;
    public float collisionPadding = 0.1f;

    [Header("Auto 360° Rotation")]
    public bool autoRotate = false;
    public float autoRotateDegreesPerSecond = 45f;
    public bool autoRotateClockwise = true;

    [Tooltip("If > 0, calling StartFullTurn() rotates exactly 360° over this many seconds.")]
    public float fullTurnDurationSeconds = 8f;

    // Internal state
    private Vector3 _dynamicCenterOffset; // used when panning
    private float _pendingFullTurnDegrees = 0f;

    private Vector3 _desiredPos;
    private Quaternion _desiredRot;

    void Reset()
    {
        // Try to find something reasonable
        if (Camera.main != null && Camera.main.transform == transform)
        {
            // ok
        }
        yaw = transform.eulerAngles.y;
        pitch = transform.eulerAngles.x;
        distance = Mathf.Clamp(distance, minDistance, maxDistance);
    }

    void Awake()
    {
        _dynamicCenterOffset = Vector3.zero;
        NormalizeYaw();
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        distance = Mathf.Clamp(distance, minDistance, maxDistance);
    }

    void LateUpdate()
    {
        if (center == null) return;

        float dt = Time.unscaledDeltaTime;

        // --- Input (example bindings; change to your input system as needed) ---
        // Orbit: RMB drag
        bool orbitHeld = Input.GetMouseButton(1);
        if (orbitHeld)
        {
            float mx = Input.GetAxisRaw("Mouse X");
            float my = Input.GetAxisRaw("Mouse Y");

            yaw += mx * orbitSensitivity * dt * (autoRotateClockwise ? 1f : 1f);
            pitch += (invertY ? my : -my) * orbitSensitivity * dt;

            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
            NormalizeYaw(); // makes 360° seamless
        }

        // Zoom: scroll wheel
        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) > 0.0001f)
        {
            float z = (invertZoom ? -scroll : scroll);
            distance = Mathf.Clamp(distance - z * zoomSpeed, minDistance, maxDistance);
        }

        // Pan: MMB drag (moves pivot/center offset)
        if (allowPan && Input.GetMouseButton(2))
        {
            float mx = Input.GetAxisRaw("Mouse X");
            float my = Input.GetAxisRaw("Mouse Y");

            Vector3 right = transform.right;
            Vector3 forward = transform.forward;

            if (panInLocalPlane)
            {
                // Keep forward on a ground-ish plane so panning doesn't "climb"
                forward.y = 0f;
                forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
            }

            // Screen drag: move opposite direction
            Vector3 panDelta = (-right * mx + -forward * my) * panSpeed * dt;
            _dynamicCenterOffset += panDelta;
        }

        // --- Auto rotate (continuous) ---
        if (autoRotate)
        {
            float sign = autoRotateClockwise ? 1f : -1f;
            yaw += sign * autoRotateDegreesPerSecond * dt;
            NormalizeYaw();
        }

        // --- Full turn support (exact 360 while keeping config same) ---
        // When active, we consume pending degrees at a constant rate.
        if (_pendingFullTurnDegrees > 0f)
        {
            float rate = (fullTurnDurationSeconds > 0.01f)
                ? 360f / fullTurnDurationSeconds
                : autoRotateDegreesPerSecond;

            float step = Mathf.Min(_pendingFullTurnDegrees, rate * dt);
            yaw += (autoRotateClockwise ? 1f : -1f) * step;
            _pendingFullTurnDegrees -= step;

            NormalizeYaw();
        }

        // Compute desired transform from yaw/pitch/distance and pivot
        Vector3 pivot = center.position + centerOffset + _dynamicCenterOffset;

        Quaternion orbitRot = Quaternion.Euler(pitch, yaw, 0f);

        Vector3 camOffset = orbitRot * new Vector3(0f, 0f, -distance);
        _desiredPos = pivot + camOffset;
        _desiredRot = Quaternion.LookRotation((pivot - _desiredPos).normalized, Vector3.up);

        // Optional collision push-in
        if (collision)
        {
            Vector3 dir = (_desiredPos - pivot);
            float len = dir.magnitude;
            if (len > 0.0001f)
            {
                dir /= len;
                if (Physics.SphereCast(pivot, collisionRadius, dir, out RaycastHit hit, len, collisionMask,
                        QueryTriggerInteraction.Ignore))
                {
                    float safeDist = Mathf.Max(minDistance, hit.distance - collisionPadding);
                    Vector3 collidedPos = pivot + dir * safeDist;

                    _desiredPos = collidedPos;
                    _desiredRot = Quaternion.LookRotation((pivot - _desiredPos).normalized, Vector3.up);
                }
            }
        }

        // Apply smooth or immediate
        if (smooth)
        {
            transform.position = Vector3.Lerp(transform.position, _desiredPos, 1f - Mathf.Exp(-positionDamp * dt));
            transform.rotation = Quaternion.Slerp(transform.rotation, _desiredRot, 1f - Mathf.Exp(-rotationDamp * dt));
        }
        else
        {
            transform.SetPositionAndRotation(_desiredPos, _desiredRot);
        }
    }

    /// <summary>
    /// Starts an exact 360° rotation around the center while preserving camera configuration
    /// (distance + pitch + look-at behavior remain consistent; yaw wraps seamlessly).
    /// </summary>
    [ContextMenu("Start Full Turn Degrees")]
    public void StartFullTurn(bool clockwise = true, float? durationSeconds = null)
    {
        autoRotateClockwise = clockwise;
        if (durationSeconds.HasValue) fullTurnDurationSeconds = Mathf.Max(0.01f, durationSeconds.Value);
        _pendingFullTurnDegrees = 360f;
    }
    
    
    /// <summary>Stops any in-progress 360° scheduled turn.</summary>
    [ContextMenu("Stop Full Turn Degrees")]
    public void StopFullTurn()
    {
        _pendingFullTurnDegrees = 0f;
    }

    /// <summary>Sets a new orbit center target, optionally resetting pan offset.</summary>
    public void SetCenter(Transform newCenter, bool resetPanOffset = true)
    {
        center = newCenter;
        if (resetPanOffset) _dynamicCenterOffset = Vector3.zero;
    }

    private void NormalizeYaw()
    {
        // Keeps yaw in [0, 360) so it can spin forever without floating point drift.
        yaw %= 360f;
        if (yaw < 0f) yaw += 360f;
    }
}
