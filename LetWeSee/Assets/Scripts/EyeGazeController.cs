using System.Collections;
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
    [SerializeField] private RectTransform leftPanel;
    [SerializeField] private RectTransform armVisual;
    [SerializeField] private RectTransform handVisual;

    [Header("World panel")]
    [SerializeField] private RectTransform worldPanel;
    [SerializeField] private Camera leftCamera;
    [SerializeField] private Camera rightCamera;
    [SerializeField] private Transform focusTarget;
    [SerializeField] private Transform deodorantTarget;
    [SerializeField] private Transform mouthwashTarget;
    [SerializeField] private Transform mirrorTarget;
    [SerializeField] private RawImage rightEyeView;

    [Header("Tuning")]
    [SerializeField, Range(0f, 30f)] private float pupilTravel = 14f;
    [SerializeField, Range(0f, 12f)] private float cameraTravel = 8f;
    [SerializeField, Range(0.1f, 2f)] private float focusHitRadius = 0.9f;
    [SerializeField, Range(0f, 100f)] private float focusAlignmentTolerancePixels = 48f;
    [SerializeField, Range(1f, 20f)] private float followSpeed = 10f;
    [SerializeField, Range(0.1f, 2f)] private float normalEyeAimSensitivity = 1f;
    [SerializeField, Range(0.01f, 1f)] private float draggingEyeAimSensitivity = 0.2f;
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
    private bool sequenceEnabled;
    private int sequenceIndex;
    private bool sequenceComplete;
    private bool focusTargetCurrentlyAligned;
    private Coroutine alignmentSnapRoutine;
    private Transform snappingTarget;
    private Vector3 snappingTargetOriginalScale;
    private Transform lockedFocusTarget;
    private Vector3 leftCameraStart;
    private Vector3 rightCameraStart;
    private Vector2 leftPupilStart;
    private Vector2 rightPupilStart;
    private Color rightImageColor;
    private Image leftEyeImage;
    private Image rightEyeImage;
    private Color leftEyeBaseColor;
    private Color rightEyeBaseColor;
    private bool previousCursorVisible;
    private CursorLockMode previousCursorLockState;
    private const float DoubleVisionAlpha = 0.48f;
    private const float FocusAlpha = 0.08f;
    private static Sprite runtimePlaceholderSprite;

    private void Awake()
    {
        CleanPlaceholderVisuals();
        if (leftCamera != null) leftCameraStart = leftCamera.transform.position;
        if (rightCamera != null) rightCameraStart = rightCamera.transform.position;
        if (leftPupil != null) leftPupilStart = leftPupil.anchoredPosition;
        if (leftEyeRect != null) leftEyeImage = leftEyeRect.GetComponent<Image>();
        if (rightEyeRect != null) rightEyeImage = rightEyeRect.GetComponent<Image>();
        if (leftEyeImage != null) leftEyeBaseColor = leftEyeImage.color;
        if (rightEyeImage != null) rightEyeBaseColor = rightEyeImage.color;
        if (rightPupil != null) rightPupilStart = rightPupil.anchoredPosition;
        if (rightEyeView != null) rightImageColor = rightEyeView.color;

        leftAim = RandomStartingAim();
        rightAim = RandomStartingAim();

        if (deodorantTarget == null && GameObject.Find("DeodorantTarget") != null)
            deodorantTarget = GameObject.Find("DeodorantTarget").transform;
        if (mouthwashTarget == null && GameObject.Find("MouthwashTarget") != null)
            mouthwashTarget = GameObject.Find("MouthwashTarget").transform;
        if (mirrorTarget == null && GameObject.Find("MirrorTarget") != null)
            mirrorTarget = GameObject.Find("MirrorTarget").transform;

        EnsureSequenceTargets();
        if (!sequenceEnabled && focusTarget != null)
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



    private void EnsureSequenceTargets()
    {
        Sprite placeholderSprite = GetRuntimePlaceholderSprite();
        if (deodorantTarget == null) deodorantTarget = FindTarget("DeodorantTarget");
        if (mouthwashTarget == null) mouthwashTarget = FindTarget("MouthwashTarget");
        if (mirrorTarget == null) mirrorTarget = FindTarget("MirrorTarget");

        if (deodorantTarget == null || mouthwashTarget == null || mirrorTarget == null)
        {

            if (deodorantTarget == null)
                deodorantTarget = CreateBottleTarget("DeodorantTarget", "DEODORANT", new Vector2(-3.1f, 0.4f), placeholderSprite,
                    new Color(0.88f, 0.87f, 0.78f), new Color(0.24f, 0.25f, 0.28f), new Color(0.78f, 0.63f, 0.35f), false);
            if (mouthwashTarget == null)
                mouthwashTarget = CreateBottleTarget("MouthwashTarget", "MOUTH WASH", new Vector2(0f, 0.4f), placeholderSprite,
                    new Color(0.30f, 0.75f, 0.80f), new Color(0.10f, 0.23f, 0.40f), new Color(0.92f, 0.93f, 0.84f), true);
            if (mirrorTarget == null)
                mirrorTarget = CreateMirrorTarget("MirrorTarget", new Vector2(3f, 0.55f), placeholderSprite);
        }

        sequenceEnabled = deodorantTarget != null && mouthwashTarget != null && mirrorTarget != null;
        if (sequenceEnabled)
        {
            cameraTravel = Mathf.Max(cameraTravel, 10f);
            EnsureRoomBackdrop(placeholderSprite);
        }
        if (sequenceEnabled)
        {
            RandomizeSequenceTargetPositions();
            UpdateSequenceLabelVisibility();
        }
        if (sequenceEnabled && focusTarget != null && focusTarget != deodorantTarget)
            focusTarget.gameObject.SetActive(false);
    }


    private static Sprite GetRuntimePlaceholderSprite()
    {
        if (runtimePlaceholderSprite == null)
        {
            Texture2D whiteTexture = Texture2D.whiteTexture;
            runtimePlaceholderSprite = Sprite.Create(whiteTexture,
                new Rect(0f, 0f, whiteTexture.width, whiteTexture.height), new Vector2(0.5f, 0.5f));
        }
        return runtimePlaceholderSprite;
    }

    private void EnsureRoomBackdrop(Sprite sprite)
    {
        if (GameObject.Find("BathroomRoomBackdrop") != null) return;
        float halfHeight = leftCamera != null ? leftCamera.orthographicSize : 5f;
        float aspect = leftCamera != null && leftCamera.targetTexture != null
            ? (float)leftCamera.targetTexture.width / leftCamera.targetTexture.height : 1f;
        float width = 2f * (halfHeight * aspect + cameraTravel + 1f);
        float height = 2f * (halfHeight + cameraTravel + 1f);
        var wall = new GameObject("BathroomRoomBackdrop", typeof(SpriteRenderer));
        wall.transform.position = new Vector3((leftCameraStart.x + rightCameraStart.x) * 0.5f,
            (leftCameraStart.y + rightCameraStart.y) * 0.5f, 2f);
        wall.transform.localScale = new Vector3(width, height, 1f);
        var renderer = wall.GetComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = new Color(0.34f, 0.39f, 0.43f, 1f);
        renderer.sortingOrder = -30;
        Color grout = new Color(0.66f, 0.70f, 0.72f, 0.22f);
        for (float x = -width * 0.5f + 2f; x < width * 0.5f; x += 2.5f)
            AddWorldPart(wall.transform, "TileSeamVertical", sprite, new Vector2(x, 0f), new Vector2(0.025f, height), grout, -29);
        for (float y = -height * 0.5f + 2f; y < height * 0.5f; y += 2.5f)
            AddWorldPart(wall.transform, "TileSeamHorizontal", sprite, new Vector2(0f, y), new Vector2(width, 0.025f), grout, -29);
    }

    private static Transform FindTarget(string targetName)
    {
        GameObject found = GameObject.Find(targetName);
        return found != null ? found.transform : null;
    }

    private static Transform CreateBottleTarget(string objectName, string label, Vector2 position, Sprite sprite,
        Color bodyColor, Color capColor, Color labelColor, bool wide)
    {
        var root = new GameObject(objectName);
        root.transform.position = new Vector3(position.x, position.y, 0f);
        float bodyWidth = wide ? 0.82f : 0.62f;
        AddWorldPart(root.transform, "Bottle", sprite, Vector2.zero, new Vector2(bodyWidth, 1.35f), bodyColor, 4);
        AddWorldPart(root.transform, "Neck", sprite, new Vector2(0f, 0.76f), new Vector2(wide ? 0.46f : 0.50f, 0.25f), bodyColor, 5);
        AddWorldPart(root.transform, "Cap", sprite, new Vector2(0f, 0.98f), new Vector2(0.52f, 0.28f), capColor, 6);
        AddWorldPart(root.transform, "Label", sprite, new Vector2(0f, -0.03f), new Vector2(wide ? 0.68f : 0.52f, 0.42f), labelColor, 7);
        AddWorldText(root.transform, label, new Vector2(0f, -0.03f), wide ? 0.075f : 0.085f, new Color(0.11f, 0.16f, 0.20f), 8);
        AddWorldText(root.transform, label, new Vector2(0f, -1.02f), 0.10f, Color.white, 8);
        return root.transform;
    }

    private static Transform CreateMirrorTarget(string objectName, Vector2 position, Sprite sprite)
    {
        var root = new GameObject(objectName);
        root.transform.position = new Vector3(position.x, position.y, 0f);
        AddWorldPart(root.transform, "MirrorFrame", sprite, new Vector2(0f, 0.15f), new Vector2(2.1f, 1.85f), new Color(0.42f, 0.31f, 0.30f), 3);
        AddWorldPart(root.transform, "ReflectiveGlass", sprite, new Vector2(0f, 0.15f), new Vector2(1.82f, 1.57f), new Color(0.54f, 0.78f, 0.82f), 4);
        AddWorldPart(root.transform, "Reflection", sprite, new Vector2(-0.42f, 0.15f), new Vector2(0.16f, 1.35f), new Color(0.86f, 0.94f, 0.91f, 0.72f), 5);
        AddWorldText(root.transform, "MIRROR", new Vector2(0f, -1.10f), 0.11f, Color.white, 8);
        return root.transform;
    }

    private static void AddWorldPart(Transform parent, string partName, Sprite sprite, Vector2 localPosition,
        Vector2 size, Color color, int sortingOrder)
    {
        var part = new GameObject(partName, typeof(SpriteRenderer));
        part.transform.SetParent(parent, false);
        part.transform.localPosition = new Vector3(localPosition.x, localPosition.y, 0f);
        part.transform.localScale = new Vector3(size.x, size.y, 1f);
        var renderer = part.GetComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;
    }

    private static void AddWorldText(Transform parent, string value, Vector2 localPosition,
        float characterSize, Color color, int sortingOrder)
    {
        var label = new GameObject("Label", typeof(TextMesh));
        label.transform.SetParent(parent, false);
        label.transform.localPosition = new Vector3(localPosition.x, localPosition.y, -0.02f);
        var text = label.GetComponent<TextMesh>();
        text.text = value;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 48;
        text.characterSize = characterSize;
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.color = color;
        label.GetComponent<MeshRenderer>().sortingOrder = sortingOrder;
    }

    private void CleanPlaceholderVisuals()
    {
        if (leftPanel == null) return;

        // Old editor setup passes could leave extra hand objects outside the controller references.
        for (int i = leftPanel.childCount - 1; i >= 0; i--)
        {
            Transform child = leftPanel.GetChild(i);
            bool duplicateArm = child.name == "ArmPlaceholder" &&
                (armVisual == null || child != armVisual);
            bool duplicateHand = child.name == "HandPlaceholder" &&
                (handVisual == null || child != handVisual);
            bool orphanedFinger = child.name == "FingerIndex" || child.name == "FingerMiddle" ||
                child.name == "FingerRing" || child.name == "Thumb";

            if (duplicateArm || duplicateHand || orphanedFinger)
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
        }

        // Clip the arm and hand at the face-panel edge so they cannot bleed into the world view.
        if (leftPanel.GetComponent<RectMask2D>() == null)
            leftPanel.gameObject.AddComponent<RectMask2D>();

        if (armVisual != null) armVisual.SetAsLastSibling();
        if (handVisual != null) handVisual.SetAsLastSibling();
    }

    private void OnEnable()
    {
        if (eyeMap != null) eyeMap.Enable();

        if (Application.isPlaying)
        {
            previousCursorVisible = Cursor.visible;
            previousCursorLockState = Cursor.lockState;
            Cursor.lockState = CursorLockMode.None;
        }
    }

    private void OnDisable()
    {
        if (eyeMap != null) eyeMap.Disable();
        draggedEye = DraggedEye.None;

        if (Application.isPlaying)
        {
            Cursor.visible = previousCursorVisible;
            Cursor.lockState = previousCursorLockState;
        }
    }

    private void Update()
    {
        if (pointerPosition == null || eyeDrag == null || focusClick == null ||
            leftEyeRect == null || rightEyeRect == null || leftPupil == null ||
            rightPupil == null || worldPanel == null || leftCamera == null || rightCamera == null)
            return;

        Vector2 pointer = pointerPosition.ReadValue<Vector2>();
        bool overLeftPanel = leftPanel != null && RectTransformUtility.RectangleContainsScreenPoint(leftPanel, pointer, null);
        Cursor.visible = !overLeftPanel;
        UpdateArm(pointer, overLeftPanel);
        UpdateEyeFocusFeedback(pointer);
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
                lockedFocusTarget = null;
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

        if (!sequenceComplete && !overFace && focusClick.WasPressedThisFrame() && IsPointerOnFocusTarget(pointer))
        {
            Transform newlyFocusedTarget = CurrentFocusTarget;
            if (newlyFocusedTarget != null)
            {
                lockedFocusTarget = newlyFocusedTarget;
                focused = true;
                LogTargetInteraction(newlyFocusedTarget);
                AdvanceFocusSequence();
            }
        }

        Vector2 wantedLeftPupil = focused ? Vector2.zero : leftAim * pupilTravel;
        Vector2 wantedRightPupil = focused ? Vector2.zero : rightAim * pupilTravel;
        leftPupil.anchoredPosition = Vector2.Lerp(
            leftPupil.anchoredPosition, leftPupilStart + wantedLeftPupil, Time.deltaTime * followSpeed);
        rightPupil.anchoredPosition = Vector2.Lerp(
            rightPupil.anchoredPosition, rightPupilStart + wantedRightPupil, Time.deltaTime * followSpeed);

        Transform cameraFocusTarget = lockedFocusTarget != null ? lockedFocusTarget : CurrentFocusTarget;
        Vector3 wantedLeftCamera = focused && cameraFocusTarget != null
            ? new Vector3(cameraFocusTarget.position.x, cameraFocusTarget.position.y, leftCameraStart.z)
            : leftCameraStart + new Vector3(leftAim.x * cameraTravel, leftAim.y * cameraTravel, 0f);
        Vector3 wantedRightCamera = focused && cameraFocusTarget != null
            ? new Vector3(cameraFocusTarget.position.x, cameraFocusTarget.position.y, rightCameraStart.z)
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

    private void UpdateArm(Vector2 screenPointer, bool overLeftPanel)
    {
        if (armVisual == null || handVisual == null) return;
        armVisual.gameObject.SetActive(overLeftPanel);
        handVisual.gameObject.SetActive(overLeftPanel);
        if (!overLeftPanel || leftPanel == null ||
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(leftPanel, screenPointer, null, out Vector2 local))
            return;

        Rect panelBounds = leftPanel.rect;
        float handHalfWidth = handVisual.rect.width * 0.5f;
        float handHalfHeight = handVisual.rect.height * 0.5f;
        local.x = Mathf.Clamp(local.x, panelBounds.xMin + handHalfWidth, panelBounds.xMax - handHalfWidth);
        local.y = Mathf.Clamp(local.y, panelBounds.yMin + handHalfHeight, panelBounds.yMax - handHalfHeight);

        Vector2 armBase = new Vector2(
            panelBounds.xMin + panelBounds.width * 0.16f,
            panelBounds.yMin + panelBounds.height * 0.08f);
        Vector2 direction = local - armBase;
        float length = Mathf.Max(1f, direction.magnitude);

        armVisual.anchoredPosition = armBase;
        armVisual.sizeDelta = new Vector2(52f, length);
        armVisual.localRotation = Quaternion.Euler(
            0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f);

        handVisual.anchoredPosition = local;
        handVisual.localRotation = Quaternion.identity;
    }

    private Vector2 RandomStartingAim()
    {
        return new Vector2(
            Random.Range(-startingEyeAimRange, startingEyeAimRange),
            Random.Range(-startingEyeAimRange, startingEyeAimRange));
    }

    private Vector2 ReadEyeAim(RectTransform eye, Vector2 screenPointer)
    {
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(eye, screenPointer, null, out Vector2 local))
            return Vector2.zero;

        float x = Mathf.Max(1f, eye.rect.width * 0.5f);
        float y = Mathf.Max(1f, eye.rect.height * 0.5f);
        return new Vector2(
            Mathf.Clamp(local.x / x * CurrentEyeAimSensitivity, -1f, 1f),
            Mathf.Clamp(local.y / y * CurrentEyeAimSensitivity, -1f, 1f));
    }

    private float CurrentEyeAimSensitivity
    {
        get
        {
            bool activelyDraggingEye = draggedEye != DraggedEye.None && eyeDrag != null && eyeDrag.IsPressed();
            return activelyDraggingEye ? draggingEyeAimSensitivity : normalEyeAimSensitivity;
        }
    }

    private Transform CurrentFocusTarget
    {
        get
        {
            if (!sequenceEnabled) return focusTarget;
            switch (sequenceIndex)
            {
                case 0: return deodorantTarget;
                case 1: return mouthwashTarget;
                case 2: return mirrorTarget;
                default: return null;
            }
        }
    }

    private void LogTargetInteraction(Transform target)
    {
        if (!sequenceEnabled)
        {
            Debug.Log("you interacted with " + (target != null ? target.name : "the object"));
            return;
        }

        switch (sequenceIndex)
        {
            case 0: Debug.Log("you interacted with deodorant"); break;
            case 1: Debug.Log("you interacted with mouthwash"); break;
            case 2: Debug.Log("you interacted with the mirror"); break;
        }
    }

    private void AdvanceFocusSequence()
    {
        if (!sequenceEnabled || sequenceComplete) return;

        switch (sequenceIndex)
        {
            case 0: sequenceIndex = 1; break;
            case 1: sequenceIndex = 2; break;
            case 2: sequenceComplete = true; break;
        }

        UpdateSequenceLabelVisibility();
    }

    private void UpdateSequenceLabelVisibility()
    {
        if (!sequenceEnabled) return;
        Transform[] targets = { deodorantTarget, mouthwashTarget, mirrorTarget };
        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] == null) continue;
            TextMesh[] labels = targets[i].GetComponentsInChildren<TextMesh>(true);
            foreach (TextMesh label in labels)
                label.gameObject.SetActive(i == sequenceIndex);
        }
    }

    private void RandomizeSequenceTargetPositions()
    {
        Transform[] targets = { deodorantTarget, mouthwashTarget, mirrorTarget };
        float centerX = (leftCameraStart.x + rightCameraStart.x) * 0.5f;
        float centerY = (leftCameraStart.y + rightCameraStart.y) * 0.5f;
        float halfHeight = leftCamera != null ? leftCamera.orthographicSize : 5f;
        float aspect = leftCamera != null && leftCamera.targetTexture != null
            ? (float)leftCamera.targetTexture.width / leftCamera.targetTexture.height
            : worldPanel != null ? worldPanel.rect.width / Mathf.Max(1f, worldPanel.rect.height) : 1f;
        float halfWidth = halfHeight * aspect;
        float roomHalfWidth = halfWidth + cameraTravel - 1.6f;
        float roomHalfHeight = halfHeight + cameraTravel - 1.6f;
        Vector2[] viewCenters =
        {
            new Vector2(leftCameraStart.x, leftCameraStart.y),
            new Vector2(rightCameraStart.x, rightCameraStart.y),
            new Vector2(leftCameraStart.x + leftAim.x * cameraTravel, leftCameraStart.y + leftAim.y * cameraTravel),
            new Vector2(rightCameraStart.x + rightAim.x * cameraTravel, rightCameraStart.y + rightAim.y * cameraTravel)
        };
        Vector2[] placed = new Vector2[targets.Length];
        int[] cornerOrder = { 0, 1, 2, 3 };
        for (int i = 0; i < cornerOrder.Length; i++)
        {
            int swap = Random.Range(i, cornerOrder.Length);
            (cornerOrder[i], cornerOrder[swap]) = (cornerOrder[swap], cornerOrder[i]);
        }

        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] == null) continue;
            int corner = cornerOrder[i];
            float xSign = (corner == 0 || corner == 2) ? -1f : 1f;
            float ySign = (corner < 2) ? -1f : 1f;
            Vector2 candidate = Vector2.zero;
            bool accepted = false;
            for (int attempt = 0; attempt < 160; attempt++)
            {
                candidate = new Vector2(
                    centerX + xSign * Random.Range(roomHalfWidth - 0.7f, roomHalfWidth),
                    centerY + ySign * Random.Range(roomHalfHeight - 0.7f, roomHalfHeight));

                bool insideAnyStartingView = false;
                foreach (Vector2 viewCenter in viewCenters)
                {
                    bool insidePaddedView = Mathf.Abs(candidate.x - viewCenter.x) <= halfWidth + 1f &&
                        Mathf.Abs(candidate.y - viewCenter.y) <= halfHeight + 1f;
                    if (insidePaddedView)
                    {
                        insideAnyStartingView = true;
                        break;
                    }
                }

                bool tooCloseToEarlierTarget = false;
                for (int previous = 0; previous < i; previous++)
                {
                    if (Vector2.Distance(candidate, placed[previous]) < 8f)
                    {
                        tooCloseToEarlierTarget = true;
                        break;
                    }
                }

                if (!insideAnyStartingView && !tooCloseToEarlierTarget)
                {
                    accepted = true;
                    break;
                }
            }

            if (!accepted)
            {
                candidate = new Vector2(centerX + xSign * roomHalfWidth, centerY + ySign * roomHalfHeight);
            }

            placed[i] = candidate;
            Vector3 oldPosition = targets[i].position;
            targets[i].position = new Vector3(candidate.x, candidate.y, oldPosition.z);
        }
    }


    private void UpdateEyeFocusFeedback(Vector2 screenPointer)
    {
        bool leftOnTarget = false;
        bool rightOnTarget = false;
        Transform target = CurrentFocusTarget;
        bool targetAligned = target != null &&
            IsEyeTargetAligned(target) &&
            IsTargetCenterVisible(leftCamera, target) &&
            IsTargetCenterVisible(rightCamera, target);

        if (targetAligned && !focusTargetCurrentlyAligned)
            PlayAlignmentSnap(target);
        focusTargetCurrentlyAligned = targetAligned;

        if (target != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(
            worldPanel, screenPointer, null, out Vector2 local))
        {
            Rect panelRect = worldPanel.rect;
            float u = Mathf.InverseLerp(panelRect.xMin, panelRect.xMax, local.x);
            float v = Mathf.InverseLerp(panelRect.yMin, panelRect.yMax, local.y);
            leftOnTarget = IsTargetUnderPointer(leftCamera, target, u, v);
            rightOnTarget = IsTargetUnderPointer(rightCamera, target, u, v);
        }

        if (leftEyeImage != null)
            leftEyeImage.color = leftOnTarget ? new Color(0.62f, 1f, 0.72f, leftEyeBaseColor.a) : leftEyeBaseColor;
        if (rightEyeImage != null)
            rightEyeImage.color = rightOnTarget ? new Color(0.62f, 1f, 0.72f, rightEyeBaseColor.a) : rightEyeBaseColor;
    }

    private bool IsEyeTargetAligned(Transform target)
    {
        Vector3 leftTargetView = leftCamera.WorldToViewportPoint(target.position);
        Vector3 rightTargetView = rightCamera.WorldToViewportPoint(target.position);
        if (leftTargetView.z <= 0f || rightTargetView.z <= 0f) return false;

        Vector2 targetImageSeparation = new Vector2(
            (leftTargetView.x - rightTargetView.x) * worldPanel.rect.width,
            (leftTargetView.y - rightTargetView.y) * worldPanel.rect.height);
        return targetImageSeparation.magnitude <= focusAlignmentTolerancePixels;
    }

    private static bool IsTargetCenterVisible(Camera eyeCamera, Transform target)
    {
        Vector3 targetView = eyeCamera.WorldToViewportPoint(target.position);
        return targetView.z > 0f && targetView.x >= 0f && targetView.x <= 1f &&
            targetView.y >= 0f && targetView.y <= 1f;
    }

    private void PlayAlignmentSnap(Transform target)
    {
        if (alignmentSnapRoutine != null)
        {
            StopCoroutine(alignmentSnapRoutine);
            if (snappingTarget != null)
                snappingTarget.localScale = snappingTargetOriginalScale;
        }

        snappingTarget = target;
        snappingTargetOriginalScale = target.localScale;
        alignmentSnapRoutine = StartCoroutine(AlignmentSnapAnimation(target, snappingTargetOriginalScale));
    }

    private IEnumerator AlignmentSnapAnimation(Transform target, Vector3 originalScale)
    {
        Vector3 popScale = originalScale * 1.35f;
        const float popDuration = 0.18f;
        const float settleDuration = 0.36f;

        for (float elapsed = 0f; elapsed < popDuration; elapsed += Time.deltaTime)
        {
            if (target == null) yield break;
            target.localScale = Vector3.Lerp(originalScale, popScale, elapsed / popDuration);
            yield return null;
        }

        for (float elapsed = 0f; elapsed < settleDuration; elapsed += Time.deltaTime)
        {
            if (target == null) yield break;
            target.localScale = Vector3.Lerp(popScale, originalScale, elapsed / settleDuration);
            yield return null;
        }

        if (target != null) target.localScale = originalScale;
        alignmentSnapRoutine = null;
        snappingTarget = null;
    }

    private bool IsPointerOnFocusTarget(Vector2 screenPointer)
    {
        Transform target = CurrentFocusTarget;
        if (target == null ||
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(worldPanel, screenPointer, null, out Vector2 local))
            return false;

        Rect panelRect = worldPanel.rect;
        float u = Mathf.InverseLerp(panelRect.xMin, panelRect.xMax, local.x);
        float v = Mathf.InverseLerp(panelRect.yMin, panelRect.yMax, local.y);

        // The target views can be close without requiring pixel-perfect convergence.
        if (!IsEyeTargetAligned(target)) return false;

        // The click must land on the target in both eye views.
        return IsTargetUnderPointer(leftCamera, target, u, v) && IsTargetUnderPointer(rightCamera, target, u, v);
    }

    private bool IsTargetUnderPointer(Camera eyeCamera, Transform target, float u, float v)
    {
        if (u < 0f || u > 1f || v < 0f || v > 1f) return false;

        SpriteRenderer[] renderers = target.GetComponentsInChildren<SpriteRenderer>();
        if (renderers.Length == 0)
        {
            Vector3 targetView = eyeCamera.WorldToViewportPoint(target.position);
            if (targetView.z <= 0f || targetView.x < 0f || targetView.x > 1f || targetView.y < 0f || targetView.y > 1f)
                return false;

            float radiusU = focusHitRadius / (2f * eyeCamera.orthographicSize * Mathf.Max(0.01f, eyeCamera.aspect));
            float radiusV = focusHitRadius / (2f * eyeCamera.orthographicSize);
            return Mathf.Abs(targetView.x - u) <= radiusU && Mathf.Abs(targetView.y - v) <= radiusV;
        }

        float minU = float.PositiveInfinity;
        float maxU = float.NegativeInfinity;
        float minV = float.PositiveInfinity;
        float maxV = float.NegativeInfinity;
        bool anyVisibleInFront = false;

        foreach (SpriteRenderer spriteRenderer in renderers)
        {
            Bounds bounds = spriteRenderer.bounds;
            for (int x = 0; x < 2; x++)
            for (int y = 0; y < 2; y++)
            for (int z = 0; z < 2; z++)
            {
                Vector3 corner = new Vector3(
                    x == 0 ? bounds.min.x : bounds.max.x,
                    y == 0 ? bounds.min.y : bounds.max.y,
                    z == 0 ? bounds.min.z : bounds.max.z);
                Vector3 viewport = eyeCamera.WorldToViewportPoint(corner);
                if (viewport.z <= 0f) continue;

                anyVisibleInFront = true;
                minU = Mathf.Min(minU, viewport.x);
                maxU = Mathf.Max(maxU, viewport.x);
                minV = Mathf.Min(minV, viewport.y);
                maxV = Mathf.Max(maxV, viewport.y);
            }
        }

        if (!anyVisibleInFront) return false;

        float paddingU = focusHitRadius / (2f * eyeCamera.orthographicSize * Mathf.Max(0.01f, eyeCamera.aspect));
        float paddingV = focusHitRadius / (2f * eyeCamera.orthographicSize);
        return u >= minU - paddingU && u <= maxU + paddingU &&
            v >= minV - paddingV && v <= maxV + paddingV;
    }
}





