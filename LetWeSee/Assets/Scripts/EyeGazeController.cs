using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class EyeGazeController : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private InputActionAsset inputActions;

    [Header("Face panel")]
    [SerializeField] private RectTransform leftEyeRect;
    [SerializeField] private RectTransform rightEyeRect;
    [SerializeField] private RectTransform leftPupil;
    [SerializeField] private RectTransform rightPupil;

    [Header("World panel")]
    [SerializeField] private RectTransform worldPanel;
    [SerializeField] private Camera leftCamera;
    [SerializeField] private Camera rightCamera;
    [SerializeField] private Transform focusTarget;
    [SerializeField] private RawImage rightEyeView;

    [Header("Tuning")]
    [SerializeField, Range(0f, 30f)] private float pupilTravel = 14f;
    [SerializeField, Range(0f, 1.5f)] private float cameraTravel = 0.45f;
    [SerializeField, Range(0.1f, 2f)] private float focusHitRadius = 0.9f;
    [SerializeField, Range(1f, 20f)] private float followSpeed = 10f;
    [SerializeField, Range(0f, 1f)] private float startingEyeAimRange = 0.7f;
    [SerializeField] private Vector2 focusTargetRandomRange = new Vector2(2.5f, 1.6f);


    private enum DraggedEye { None, Left, Right }

    private InputActionMap eyeMap;
    private InputAction pointerPosition;
    private InputAction eyeDrag;
    private InputAction focusClick;
    private DraggedEye draggedEye;
    private Vector2 leftAim;
    private Vector2 rightAim;
    private bool focused;
    private Vector3 leftCameraStart;
    private Vector3 rightCameraStart;
    private Vector2 leftPupilStart;
    private Vector2 rightPupilStart;
    private Color rightImageColor;
    private const float DoubleVisionAlpha = 0.48f;
    private const float FocusAlpha = 0.08f;

    private void Awake()
    {
        if (leftCamera != null) leftCameraStart = leftCamera.transform.position;
        if (rightCamera != null) rightCameraStart = rightCamera.transform.position;
        if (leftPupil != null) leftPupilStart = leftPupil.anchoredPosition;
        if (rightPupil != null) rightPupilStart = rightPupil.anchoredPosition;
        if (rightEyeView != null) rightImageColor = rightEyeView.color;

        leftAim = RandomStartingAim();
        rightAim = RandomStartingAim();

        if (focusTarget != null)
        {
            Vector3 target = focusTarget.position;
            Vector2 viewCenter = new Vector2(
                (leftCameraStart.x + rightCameraStart.x) * 0.5f,
                (leftCameraStart.y + rightCameraStart.y) * 0.5f);
            focusTarget.position = new Vector3(
                viewCenter.x + Random.Range(-focusTargetRandomRange.x, focusTargetRandomRange.x),
                viewCenter.y + Random.Range(-focusTargetRandomRange.y, focusTargetRandomRange.y),
                target.z);
        }

        if (inputActions == null) return;
        eyeMap = inputActions.FindActionMap("EyeControls", true);
        pointerPosition = eyeMap.FindAction("PointerPosition", true);
        eyeDrag = eyeMap.FindAction("EyeDrag", true);
        focusClick = eyeMap.FindAction("FocusClick", true);
    }

    private void OnEnable()
    {
        if (eyeMap != null) eyeMap.Enable();
    }

    private void OnDisable()
    {
        if (eyeMap != null) eyeMap.Disable();
        draggedEye = DraggedEye.None;
    }

    private void Update()
    {
        if (pointerPosition == null || eyeDrag == null || focusClick == null ||
            leftEyeRect == null || rightEyeRect == null || leftPupil == null ||
            rightPupil == null || worldPanel == null || leftCamera == null || rightCamera == null)
            return;

        Vector2 pointer = pointerPosition.ReadValue<Vector2>();
        bool overFace = pointer.x < Screen.width * 0.4f;

        // Choose the eye once, on mouse-down. Keep that choice until mouse-up.
        if (eyeDrag.WasPressedThisFrame())
        {
            draggedEye = DraggedEye.None;
            if (overFace && RectTransformUtility.RectangleContainsScreenPoint(leftEyeRect, pointer, null))
                draggedEye = DraggedEye.Left;
            else if (overFace && RectTransformUtility.RectangleContainsScreenPoint(rightEyeRect, pointer, null))
                draggedEye = DraggedEye.Right;

            // Starting a new eye drag exits focus and returns both eyes to their neutral aim.
            if (draggedEye != DraggedEye.None && focused)
            {
                focused = false;
                leftAim = Vector2.zero;
                rightAim = Vector2.zero;
            }
        }

        if (!eyeDrag.IsPressed())
            draggedEye = DraggedEye.None;

        if (eyeDrag.IsPressed() && draggedEye == DraggedEye.Left)
        {
            focused = false;
            leftAim = ReadEyeAim(leftEyeRect, pointer);
        }
        else if (eyeDrag.IsPressed() && draggedEye == DraggedEye.Right)
        {
            focused = false;
            rightAim = ReadEyeAim(rightEyeRect, pointer);
        }

        if (!overFace && focusClick.WasPressedThisFrame())
            focused = IsPointerOnFocusTarget(pointer);

        Vector2 wantedLeftPupil = focused ? Vector2.zero : leftAim * pupilTravel;
        Vector2 wantedRightPupil = focused ? Vector2.zero : rightAim * pupilTravel;
        leftPupil.anchoredPosition = Vector2.Lerp(
            leftPupil.anchoredPosition, leftPupilStart + wantedLeftPupil, Time.deltaTime * followSpeed);
        rightPupil.anchoredPosition = Vector2.Lerp(
            rightPupil.anchoredPosition, rightPupilStart + wantedRightPupil, Time.deltaTime * followSpeed);

        Vector3 wantedLeftCamera = focused
            ? new Vector3(focusTarget.position.x, focusTarget.position.y, leftCameraStart.z)
            : leftCameraStart + new Vector3(leftAim.x * cameraTravel, leftAim.y * cameraTravel, 0f);
        Vector3 wantedRightCamera = focused
            ? new Vector3(focusTarget.position.x, focusTarget.position.y, rightCameraStart.z)
            : rightCameraStart + new Vector3(rightAim.x * cameraTravel, rightAim.y * cameraTravel, 0f);

        leftCamera.transform.position = Vector3.Lerp(
            leftCamera.transform.position, wantedLeftCamera, Time.deltaTime * followSpeed);
        rightCamera.transform.position = Vector3.Lerp(
            rightCamera.transform.position, wantedRightCamera, Time.deltaTime * followSpeed);

        if (rightEyeView != null)
        {
            Color tint = focused ? Color.white : rightImageColor;
            tint.a = Mathf.Lerp(rightEyeView.color.a, focused ? FocusAlpha : DoubleVisionAlpha,
                Time.deltaTime * followSpeed);
            rightEyeView.color = tint;
        }
    }

    private Vector2 RandomStartingAim()
    {
        return new Vector2(
            Random.Range(-startingEyeAimRange, startingEyeAimRange),
            Random.Range(-startingEyeAimRange, startingEyeAimRange));
    }

    private static Vector2 ReadEyeAim(RectTransform eye, Vector2 screenPointer)
    {
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(eye, screenPointer, null, out Vector2 local))
            return Vector2.zero;

        float x = Mathf.Max(1f, eye.rect.width * 0.5f);
        float y = Mathf.Max(1f, eye.rect.height * 0.5f);
        return new Vector2(Mathf.Clamp(local.x / x, -1f, 1f), Mathf.Clamp(local.y / y, -1f, 1f));
    }

    private bool IsPointerOnFocusTarget(Vector2 screenPointer)
    {
        if (focusTarget == null ||
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(worldPanel, screenPointer, null, out Vector2 local))
            return false;

        Rect panelRect = worldPanel.rect;
        float u = Mathf.InverseLerp(panelRect.xMin, panelRect.xMax, local.x);
        float v = Mathf.InverseLerp(panelRect.yMin, panelRect.yMax, local.y);

        // The target can be selected from either eye's offset image.
        return IsTargetUnderPointer(leftCamera, u, v) || IsTargetUnderPointer(rightCamera, u, v);
    }

    private bool IsTargetUnderPointer(Camera eyeCamera, float u, float v)
    {
        float orthoHeight = eyeCamera.orthographicSize * 2f;
        float aspect = eyeCamera.targetTexture != null
            ? (float)eyeCamera.targetTexture.width / eyeCamera.targetTexture.height
            : (float)worldPanel.rect.width / Mathf.Max(1f, worldPanel.rect.height);
        Vector3 cameraPosition = eyeCamera.transform.position;
        Vector2 clickedWorldPoint = new Vector2(
            cameraPosition.x + (u - 0.5f) * orthoHeight * aspect,
            cameraPosition.y + (v - 0.5f) * orthoHeight);

        return Vector2.Distance(clickedWorldPoint, focusTarget.position) <= focusHitRadius;
    }
}




