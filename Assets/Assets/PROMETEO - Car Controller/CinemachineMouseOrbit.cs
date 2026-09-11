using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

public class CinemachineMouseOrbit : MonoBehaviour
{
    [Header("Mouse Rotation")]
    [SerializeField] private float horizontalSensitivity = 0.12f;
    [SerializeField] private float verticalSensitivity = 0.12f;
    [SerializeField] private bool holdRightMouseToRotate = false;

    [Header("Zoom")]
    [SerializeField] private float zoomSensitivity = 0.01f;
    [SerializeField] private float minimumDistance = 3f;
    [SerializeField] private float maximumDistance = 15f;

    [Header("Cursor")]
    [SerializeField] private bool lockCursorOnPlay = true;

    private CinemachineOrbitalFollow orbitalFollow;
    private bool cursorLocked;

    private void Awake()
    {
        orbitalFollow = GetComponent<CinemachineOrbitalFollow>();

        if (orbitalFollow == null)
        {
            Debug.LogError(
                "Set Cinemachine Position Control to Orbital Follow.",
                this
            );

            enabled = false;
        }
    }

    private void Start()
    {
        if (lockCursorOnPlay)
            SetCursorLock(true);
    }

    private void Update()
    {
        Mouse mouse = Mouse.current;

        if (mouse == null)
            return;

        // Escape releases the mouse.
        if (Keyboard.current != null &&
            Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            SetCursorLock(false);
        }

        // Left-click inside Game view to capture the mouse again.
        if (lockCursorOnPlay &&
            !cursorLocked &&
            mouse.leftButton.wasPressedThisFrame)
        {
            SetCursorLock(true);
        }

        bool allowRotation =
            !holdRightMouseToRotate ||
            mouse.rightButton.isPressed;

        if (allowRotation && (!lockCursorOnPlay || cursorLocked))
        {
            Vector2 mouseDelta = mouse.delta.ReadValue();

            InputAxis horizontal = orbitalFollow.HorizontalAxis;

            horizontal.Value = horizontal.ClampValue(
                horizontal.Value +
                mouseDelta.x * horizontalSensitivity
            );

            orbitalFollow.HorizontalAxis = horizontal;

            InputAxis vertical = orbitalFollow.VerticalAxis;

            vertical.Value = vertical.ClampValue(
                vertical.Value -
                mouseDelta.y * verticalSensitivity
            );

            orbitalFollow.VerticalAxis = vertical;
        }

        // Mouse-wheel zoom.
        float scroll = mouse.scroll.ReadValue().y;

        if (Mathf.Abs(scroll) > 0.01f)
        {
            orbitalFollow.Radius = Mathf.Clamp(
                orbitalFollow.Radius -
                scroll * zoomSensitivity,
                minimumDistance,
                maximumDistance
            );
        }
    }

    private void OnDisable()
    {
        SetCursorLock(false);
    }

    private void SetCursorLock(bool locked)
    {
        cursorLocked = locked;
        Cursor.lockState = locked
            ? CursorLockMode.Locked
            : CursorLockMode.None;

        Cursor.visible = !locked;
    }
}