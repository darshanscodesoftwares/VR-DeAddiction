// FirstPersonController.cs
//
// Simple first-person controller for walking through the pub environment.
// Uses Unity's built-in CharacterController. No external packages needed.
//
// Controls:
//   WASD / Arrow keys  - Move
//   Mouse              - Look around
//   Left Shift         - Sprint
//   Escape             - Toggle cursor lock
//   Q / E              - Move down / up (handy for reviewing tall areas)

using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class FirstPersonController : MonoBehaviour
{
    [Header("Movement")]
    public float walkSpeed = 3.0f;
    public float sprintSpeed = 5.5f;
    public float gravity = 9.81f;

    [Header("Look")]
    public float mouseSensitivity = 2.0f;
    public float lookClampAngle = 85f;

    float _verticalVelocity;
    float _rotX;   // pitch (up/down) applied to camera only
    float _rotY;   // yaw (left/right) applied to player body
    CharacterController _cc;
    Transform _camTransform;
    bool _cursorLocked = true;

    void Start()
    {
        _cc = GetComponent<CharacterController>();

        // Find the child camera.
        Camera cam = GetComponentInChildren<Camera>();
        if (cam != null)
            _camTransform = cam.transform;

        _rotY = transform.eulerAngles.y;
        _rotX = 0f;

        SetCursorLock(true);
    }

    void Update()
    {
        HandleCursorToggle();
        if (_cursorLocked)
            HandleLook();
        HandleMovement();
    }

    void HandleLook()
    {
        float mx = Input.GetAxis("Mouse X") * mouseSensitivity;
        float my = Input.GetAxis("Mouse Y") * mouseSensitivity;

        // Yaw rotates the whole player body (so movement direction follows).
        _rotY += mx;
        transform.localEulerAngles = new Vector3(0f, _rotY, 0f);

        // Pitch rotates only the camera.
        _rotX -= my;
        _rotX = Mathf.Clamp(_rotX, -lookClampAngle, lookClampAngle);
        if (_camTransform != null)
            _camTransform.localEulerAngles = new Vector3(_rotX, 0f, 0f);
    }

    void HandleMovement()
    {
        float speed = Input.GetKey(KeyCode.LeftShift) ? sprintSpeed : walkSpeed;

        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        Vector3 forward = transform.forward;
        Vector3 right = transform.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        Vector3 move = (forward * v + right * h).normalized * speed;

        // Q/E for vertical movement (review mode).
        if (Input.GetKey(KeyCode.E))
            _verticalVelocity = 3f;
        else if (Input.GetKey(KeyCode.Q))
            _verticalVelocity = -3f;
        else if (_cc.isGrounded)
            _verticalVelocity = -0.5f;
        else
            _verticalVelocity -= gravity * Time.deltaTime;

        move.y = _verticalVelocity;
        _cc.Move(move * Time.deltaTime);
    }

    void HandleCursorToggle()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            SetCursorLock(!_cursorLocked);
    }

    void SetCursorLock(bool locked)
    {
        _cursorLocked = locked;
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}
