using System.Collections;
using System.Collections.Generic;
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
    [Tooltip("Optional sprite face. When set, it drives the eye aim instead of the UI eyes above.")]
    [SerializeField] private CharacterEyes characterEyes;

    [Header("World panel")]
    [SerializeField] private RectTransform worldPanel;
    [SerializeField] private Camera leftCamera;
    [SerializeField] private Camera rightCamera;
    [SerializeField] private Transform focusTarget;
    [SerializeField] private Transform deodorantTarget;
    [SerializeField] private Transform mouthwashTarget;
    [SerializeField] private Transform toothbrushTarget;
    [SerializeField] private Transform mirrorTarget;
    [SerializeField] private RawImage rightEyeView;
    [SerializeField] private AudioClip grabSound;
    [SerializeField, Range(0.2f, 1f)] private float mirrorFaceWidthRatio = 0.82f;
    [SerializeField, Range(0.2f, 1f)] private float mirrorFaceHeightRatio = 0.88f;

    [Header("Tuning")]
    [SerializeField, Range(0f, 30f)] private float pupilTravel = 14f;
    [SerializeField, Range(0f, 12f)] private float cameraTravel = 8f;
    [SerializeField, Range(0f, 45f)] private float cameraAimRotationDegrees = 28f;
    [SerializeField, Range(0f, 0.8f)] private float fisheyeStrength = 0.45f;
    [SerializeField, Range(0f, 1f)] private float maximumMisalignmentBlur = 1f;
    [SerializeField, Range(0f, 24f)] private float maximumBlurRadiusPixels = 20f;
    [SerializeField, Range(0.1f, 45f)] private float maximumCameraSeparationForBlurDegrees = 20f;
    [SerializeField, Range(1f, 100f)] private float focusRayDistance = 40f;
    [SerializeField, Range(0.05f, 1f)] private float focusSphereCastRadius = 0.6f;
    [SerializeField, Range(0.005f, 0.2f)] private float focusPointRadius = 0.03f;
    [SerializeField, Range(0f, 100f)] private float focusAlignmentTolerancePixels = 48f;
    [SerializeField, Range(1f, 20f)] private float followSpeed = 10f;
    [SerializeField, Range(0.1f, 2f)] private float normalEyeAimSensitivity = 1f;
    [SerializeField, Range(0.01f, 1f)] private float draggingEyeAimSensitivity = 0.2f;
    [SerializeField, Range(0f, 1f)] private float startingEyeAimRange = 0.7f;


    private enum DraggedEye { None, Left, Right }

    private InputActionMap eyeMap;
    private InputAction pointerPosition;
    private InputAction eyeDrag;
    private InputAction focusClick;
    private DraggedEye draggedEye;
    private bool characterEyeDragActive;
    private Vector2 leftAim;
    private Vector2 rightAim;
    private bool focused;
    private bool sequenceEnabled;
    private bool sequenceComplete;
    private bool focusTargetCurrentlyAligned;
    private Coroutine alignmentSnapRoutine;
    private Transform snappingTarget;
    private Vector3 snappingTargetOriginalScale;
    private Transform grownFocusTarget;
    private Transform lockedFocusTarget;
    private Transform alignedFocusTarget;
    private readonly HashSet<Transform> collectedItems = new HashSet<Transform>();
    private Vector3 leftCameraStart;
    private Vector3 rightCameraStart;
    private Quaternion leftCameraStartRotation;
    private Quaternion rightCameraStartRotation;
    private Vector2 leftPupilStart;
    private Vector2 rightPupilStart;
    private Color rightImageColor;
    private readonly List<RawImage> fisheyeImages = new List<RawImage>();
    private readonly List<Material> originalFisheyeMaterials = new List<Material>();
    private readonly List<Material> runtimeFisheyeMaterials = new List<Material>();


    private CharacterEyes mirrorCharacterEyes;
    private SpriteRenderer mirrorFrameRenderer;
    private SpriteRenderer mirrorSurfaceRenderer;
    private SpriteRenderer[] mirrorCharacterParts = new SpriteRenderer[0];
    private Canvas grabCanvas;
    private Image grabHandImage;
    private AudioSource grabAudioSource;
    private Coroutine grabRoutine;
    private SpriteRenderer[] focusRayRenderers = new SpriteRenderer[0];
    private Image leftEyeImage;
    private Image rightEyeImage;
    private Color leftEyeBaseColor;
    private Color rightEyeBaseColor;
    private bool previousCursorVisible;
    private CursorLockMode previousCursorLockState;
    private const float DoubleVisionAlpha = 0.48f;
    private static Sprite runtimePlaceholderSprite;

    private void Awake()
    {
        CleanPlaceholderVisuals();
        grabAudioSource = GetComponent<AudioSource>();
        if (grabAudioSource == null) grabAudioSource = gameObject.AddComponent<AudioSource>();
        grabAudioSource.playOnAwake = false;
        grabAudioSource.spatialBlend = 0f;
        if (leftCamera != null)
        {
            leftCameraStart = leftCamera.transform.position;
            leftCameraStartRotation = leftCamera.transform.rotation;
        }
        if (rightCamera != null)
        {
            rightCameraStart = rightCamera.transform.position;
            rightCameraStartRotation = rightCamera.transform.rotation;
        }
        if (leftPupil != null) leftPupilStart = leftPupil.anchoredPosition;
        if (leftEyeRect != null) leftEyeImage = leftEyeRect.GetComponent<Image>();
        if (rightEyeRect != null) rightEyeImage = rightEyeRect.GetComponent<Image>();
        if (leftEyeImage != null) leftEyeBaseColor = leftEyeImage.color;
        if (rightEyeImage != null) rightEyeBaseColor = rightEyeImage.color;
        if (rightPupil != null) rightPupilStart = rightPupil.anchoredPosition;
        if (rightEyeView != null) rightImageColor = rightEyeView.color;


        leftAim = RandomStartingAim();
        rightAim = RandomStartingAim();
        if (characterEyes != null)
        {
            characterEyes.SetAims(leftAim, rightAim);
            characterEyes.DragStarted += OnCharacterDragStarted;
        }

        if (deodorantTarget == null && GameObject.Find("DeodorantTarget") != null)
            deodorantTarget = GameObject.Find("DeodorantTarget").transform;
        if (mouthwashTarget == null && GameObject.Find("MouthwashTarget") != null)
            mouthwashTarget = GameObject.Find("MouthwashTarget").transform;
        if (toothbrushTarget == null && GameObject.Find("ToothbrushTarget") != null)
            toothbrushTarget = GameObject.Find("ToothbrushTarget").transform;
        if (mirrorTarget == null && GameObject.Find("MirrorTarget") != null)
            mirrorTarget = GameObject.Find("MirrorTarget").transform;

        EnsureSequenceTargets();
        CreateMirrorCharacter();
        SetupFisheyeEffect();
        CacheFocusRayRenderers();
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
        if (toothbrushTarget == null) toothbrushTarget = FindTarget("ToothbrushTarget");
        if (mirrorTarget == null) mirrorTarget = FindTarget("MirrorTarget");

        if (deodorantTarget == null || mouthwashTarget == null || toothbrushTarget == null || mirrorTarget == null)
        {

            if (deodorantTarget == null)
                deodorantTarget = CreateBottleTarget("DeodorantTarget", "DEODORANT", new Vector2(-3.1f, 0.4f), placeholderSprite,
                    new Color(0.88f, 0.87f, 0.78f), new Color(0.24f, 0.25f, 0.28f), new Color(0.78f, 0.63f, 0.35f), false);
            if (mouthwashTarget == null)
                mouthwashTarget = CreateBottleTarget("MouthwashTarget", "MOUTH WASH", new Vector2(0f, 0.4f), placeholderSprite,
                    new Color(0.30f, 0.75f, 0.80f), new Color(0.10f, 0.23f, 0.40f), new Color(0.92f, 0.93f, 0.84f), true);
            if (toothbrushTarget == null)
                toothbrushTarget = CreateToothbrushTarget(placeholderSprite, new Vector2(2f, 0.4f));
            if (mirrorTarget == null)
                mirrorTarget = CreateMirrorTarget("MirrorTarget", new Vector2(3f, 0.55f), placeholderSprite);
        }

        sequenceEnabled = deodorantTarget != null && mouthwashTarget != null && toothbrushTarget != null && mirrorTarget != null;
        if (sequenceEnabled)
        {
            cameraTravel = Mathf.Max(cameraTravel, 10f);
            EnsureRoomBackdrop(placeholderSprite);
            EnsureBathroomEnvironment(placeholderSprite);
        }
        if (sequenceEnabled)
            UpdateSequenceLabelVisibility();
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

    private void EnsureBathroomEnvironment(Sprite sprite)
    {
        if (GameObject.Find("BathroomEnvironment") != null) return;

        Transform room = new GameObject("BathroomEnvironment").transform;

        AddWorldPart(room, "BathroomFloor", sprite, new Vector2(0f, -4.25f), new Vector2(38f, 1.5f),
            new Color(0.25f, 0.31f, 0.34f), -20);
        AddWorldPart(room, "FloorEdge", sprite, new Vector2(0f, -3.48f), new Vector2(38f, 0.08f),
            new Color(0.75f, 0.78f, 0.77f), -19);

        AddWorldPart(room, "SinkCabinet", sprite, new Vector2(0f, -2.03f), new Vector2(6.6f, 1.48f),
            new Color(0.48f, 0.57f, 0.60f), 1);
        AddWorldPart(room, "LeftCabinetDoor", sprite, new Vector2(-1.72f, -2.02f), new Vector2(2.95f, 1.24f),
            new Color(0.58f, 0.66f, 0.67f), 2);
        AddWorldPart(room, "RightCabinetDoor", sprite, new Vector2(1.72f, -2.02f), new Vector2(2.95f, 1.24f),
            new Color(0.58f, 0.66f, 0.67f), 2);
        AddWorldPart(room, "CabinetCenterSeam", sprite, new Vector2(0f, -2.02f), new Vector2(0.045f, 1.2f),
            new Color(0.31f, 0.40f, 0.43f), 3);
        AddWorldPart(room, "LeftCabinetHandle", sprite, new Vector2(-0.35f, -2.02f), new Vector2(0.12f, 0.42f),
            new Color(0.82f, 0.82f, 0.76f), 3);
        AddWorldPart(room, "RightCabinetHandle", sprite, new Vector2(0.35f, -2.02f), new Vector2(0.12f, 0.42f),
            new Color(0.82f, 0.82f, 0.76f), 3);
        AddWorldPart(room, "SinkCountertop", sprite, new Vector2(0f, -1.25f), new Vector2(7.1f, 0.24f),
            new Color(0.85f, 0.84f, 0.77f), 3);
        AddWorldPart(room, "SinkBasinRim", sprite, new Vector2(0f, -1.10f), new Vector2(2.15f, 0.27f),
            new Color(0.58f, 0.69f, 0.70f), 4);
        AddWorldPart(room, "SinkBasin", sprite, new Vector2(0f, -1.08f), new Vector2(1.72f, 0.16f),
            new Color(0.35f, 0.48f, 0.51f), 5);
        AddWorldPart(room, "FaucetStem", sprite, new Vector2(0f, -0.83f), new Vector2(0.12f, 0.42f),
            new Color(0.79f, 0.82f, 0.80f), 4);
        AddWorldPart(room, "FaucetSpout", sprite, new Vector2(0.20f, -0.62f), new Vector2(0.48f, 0.10f),
            new Color(0.79f, 0.82f, 0.80f), 4);
        AddWorldPart(room, "BathMat", sprite, new Vector2(0f, -3.45f), new Vector2(3.8f, 0.38f),
            new Color(0.46f, 0.62f, 0.62f), 1);

        Transform toilet = new GameObject("BathroomToilet").transform;
        toilet.SetParent(room, false);
        toilet.localPosition = new Vector3(-6.6f, -3.0f, 0f);
        AddWorldPart(toilet, "ToiletPedestal", sprite, new Vector2(0f, -0.18f), new Vector2(0.78f, 0.70f),
            new Color(0.80f, 0.83f, 0.79f), 2);
        AddWorldPart(toilet, "ToiletBowl", sprite, new Vector2(0f, 0.23f), new Vector2(1.48f, 0.62f),
            new Color(0.88f, 0.88f, 0.81f), 3);
        AddWorldPart(toilet, "ToiletSeat", sprite, new Vector2(0f, 0.48f), new Vector2(1.16f, 0.18f),
            new Color(0.58f, 0.67f, 0.68f), 4);
        AddWorldPart(toilet, "ToiletTank", sprite, new Vector2(0f, 1.14f), new Vector2(1.12f, 1.12f),
            new Color(0.83f, 0.85f, 0.80f), 2);
        AddWorldPart(toilet, "ToiletTankLid", sprite, new Vector2(0f, 1.73f), new Vector2(1.28f, 0.16f),
            new Color(0.91f, 0.90f, 0.83f), 3);

        AddWorldPart(room, "WindowFrame", sprite, new Vector2(-6.6f, 2.75f), new Vector2(2.65f, 1.95f),
            new Color(0.79f, 0.82f, 0.77f), 1);
        AddWorldPart(room, "WindowGlass", sprite, new Vector2(-6.6f, 2.75f), new Vector2(2.36f, 1.66f),
            new Color(0.47f, 0.69f, 0.73f), 2);
        AddWorldPart(room, "WindowCrossbar", sprite, new Vector2(-6.6f, 2.75f), new Vector2(0.08f, 1.66f),
            new Color(0.83f, 0.84f, 0.78f), 3);
        AddWorldPart(room, "WindowSill", sprite, new Vector2(-6.6f, 1.76f), new Vector2(2.85f, 0.16f),
            new Color(0.86f, 0.85f, 0.78f), 3);

        AddWorldPart(room, "WallCabinet", sprite, new Vector2(6.4f, 2.85f), new Vector2(2.45f, 1.8f),
            new Color(0.72f, 0.75f, 0.69f), 1);
        AddWorldPart(room, "WallCabinetDoorLeft", sprite, new Vector2(5.79f, 2.85f), new Vector2(1.10f, 1.55f),
            new Color(0.84f, 0.83f, 0.75f), 2);
        AddWorldPart(room, "WallCabinetDoorRight", sprite, new Vector2(7.01f, 2.85f), new Vector2(1.10f, 1.55f),
            new Color(0.84f, 0.83f, 0.75f), 2);
        AddWorldPart(room, "TowelRail", sprite, new Vector2(6.35f, 0.50f), new Vector2(2.25f, 0.10f),
            new Color(0.77f, 0.81f, 0.78f), 3);
        AddWorldPart(room, "Towel", sprite, new Vector2(6.35f, -0.15f), new Vector2(1.45f, 1.08f),
            new Color(0.67f, 0.79f, 0.75f), 2);
        AddWorldPart(room, "TowelStripe", sprite, new Vector2(6.35f, -0.48f), new Vector2(1.45f, 0.12f),
            new Color(0.47f, 0.66f, 0.64f), 3);

        AddWorldPart(room, "ShowerBack", sprite, new Vector2(11.0f, -1.55f), new Vector2(4.4f, 4.35f),
            new Color(0.50f, 0.65f, 0.68f), 0);
        AddWorldPart(room, "ShowerCurtain", sprite, new Vector2(10.2f, -1.55f), new Vector2(2.65f, 4.15f),
            new Color(0.68f, 0.76f, 0.74f, 0.92f), 1);
        AddWorldPart(room, "ShowerCurtainStripe", sprite, new Vector2(10.2f, -1.55f), new Vector2(0.12f, 4.15f),
            new Color(0.49f, 0.65f, 0.66f), 2);
        AddWorldPart(room, "ShowerRail", sprite, new Vector2(10.2f, 0.64f), new Vector2(3.2f, 0.12f),
            new Color(0.79f, 0.82f, 0.79f), 3);
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

    private static Transform CreateToothbrushTarget(Sprite sprite, Vector2 position)
    {
        var root = new GameObject("ToothbrushTarget");
        root.transform.position = new Vector3(position.x, position.y, 0f);
        AddWorldPart(root.transform, "Handle", sprite, Vector2.zero, new Vector2(0.16f, 1.25f),
            new Color(0.35f, 0.72f, 0.70f), 4);
        AddWorldPart(root.transform, "BrushHead", sprite, new Vector2(0f, 0.70f), new Vector2(0.38f, 0.28f),
            new Color(0.91f, 0.88f, 0.78f), 5);
        AddWorldPart(root.transform, "Bristles", sprite, new Vector2(0f, 0.88f), new Vector2(0.30f, 0.12f),
            new Color(0.55f, 0.77f, 0.82f), 6);
        AddWorldText(root.transform, "TOOTHBRUSH", new Vector2(0f, -0.86f), 0.085f, Color.white, 8);
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

    private void OnDestroy()
    {
        if (characterEyes != null) characterEyes.DragStarted -= OnCharacterDragStarted;
        for (int i = 0; i < fisheyeImages.Count; i++)
        {
            if (fisheyeImages[i] != null)
                fisheyeImages[i].material = originalFisheyeMaterials[i];
            if (runtimeFisheyeMaterials[i] != null)
                Destroy(runtimeFisheyeMaterials[i]);
        }

    }

    private void CreateMirrorCharacter()
    {
        if (characterEyes == null || mirrorTarget == null) return;

        SpriteRenderer[] mirrorParts = mirrorTarget.GetComponentsInChildren<SpriteRenderer>(true);
        float largestSpriteArea = 0f;
        foreach (SpriteRenderer candidate in mirrorParts)
        {
            // The mirror surface is a large sprite too, but it must stay behind the reflected character.
            if (candidate == null || candidate.gameObject.name == "Reflection" || candidate.sprite == null || candidate.sprite.texture == null ||
                candidate.sprite.texture.width < 64 || candidate.sprite.texture.height < 64)
                continue;

            float area = candidate.bounds.size.x * candidate.bounds.size.y;
            if (area > largestSpriteArea)
            {
                largestSpriteArea = area;
                mirrorFrameRenderer = candidate;
            }
        }

        if (mirrorFrameRenderer == null) return;

        foreach (Transform child in mirrorTarget)
        {
            if (child.name == "Reflection")
            {
                mirrorSurfaceRenderer = child.GetComponent<SpriteRenderer>();
                break;
            }
        }
        Bounds mirrorSurfaceBounds = mirrorSurfaceRenderer != null
            ? mirrorSurfaceRenderer.bounds
            : mirrorFrameRenderer.bounds;
        if (mirrorSurfaceRenderer != null)
            mirrorSurfaceRenderer.sortingOrder = -1;

        GameObject mirrorFace = Instantiate(characterEyes.gameObject, mirrorTarget, false);
        mirrorFace.name = "MirrorCharacterFace";
        mirrorCharacterEyes = mirrorFace.GetComponent<CharacterEyes>();
        if (mirrorCharacterEyes != null) mirrorCharacterEyes.SetMirrorCopyMode();

        mirrorFace.transform.localPosition = new Vector3(0f, 0f, 0.18f);
        mirrorFace.transform.localRotation = Quaternion.identity;
        mirrorFace.transform.localScale = characterEyes.transform.localScale;

        SpriteRenderer[] faceParts = mirrorFace.GetComponentsInChildren<SpriteRenderer>(true);
        mirrorCharacterParts = faceParts;
        Bounds faceBounds = default;
        bool hasFaceBounds = false;
        int minimumFaceSortingOrder = int.MaxValue;
        int maximumFaceSortingOrder = int.MinValue;
        foreach (SpriteRenderer part in faceParts)
        {
            if (part == null) continue;
            if (part.sprite == null) continue;
            minimumFaceSortingOrder = Mathf.Min(minimumFaceSortingOrder, part.sortingOrder);
            maximumFaceSortingOrder = Mathf.Max(maximumFaceSortingOrder, part.sortingOrder);
            if (part.gameObject.name == "Hand") continue;
            if (!hasFaceBounds)
            {
                faceBounds = part.bounds;
                hasFaceBounds = true;
            }
            else
            {
                faceBounds.Encapsulate(part.bounds);
            }
        }

        if (!hasFaceBounds) return;

        float availableWidth = mirrorSurfaceBounds.size.x * mirrorFaceWidthRatio;
        float availableHeight = mirrorSurfaceBounds.size.y * mirrorFaceHeightRatio;
        float fitScale = Mathf.Min(availableWidth / Mathf.Max(0.001f, faceBounds.size.x),
            availableHeight / Mathf.Max(0.001f, faceBounds.size.y));
        mirrorFace.transform.localScale *= fitScale;
        mirrorFace.transform.localScale = new Vector3(-Mathf.Abs(mirrorFace.transform.localScale.x),
            mirrorFace.transform.localScale.y, mirrorFace.transform.localScale.z);
        foreach (SpriteRenderer part in faceParts)
            if (part != null && part.sprite != null)
                part.sortingOrder = MapMirrorFaceSortingOrder(part.sortingOrder, minimumFaceSortingOrder, maximumFaceSortingOrder);
        foreach (SpriteMask mask in mirrorFace.GetComponentsInChildren<SpriteMask>(true))
        {
            mask.backSortingOrder = MapMirrorFaceSortingOrder(mask.backSortingOrder, minimumFaceSortingOrder, maximumFaceSortingOrder);
            mask.frontSortingOrder = MapMirrorFaceSortingOrder(mask.frontSortingOrder, minimumFaceSortingOrder, maximumFaceSortingOrder);
        }

        // Clip the reflected hand to the mirror glass so it never appears over the frame or outside the mirror.
        if (mirrorSurfaceRenderer != null && mirrorSurfaceRenderer.sprite != null)
        {
            GameObject handMaskObject = new GameObject("MirrorHandMask", typeof(SpriteMask));
            handMaskObject.transform.SetParent(mirrorTarget, false);
            SpriteMask handMask = handMaskObject.GetComponent<SpriteMask>();
            handMask.sprite = mirrorSurfaceRenderer.sprite;
            handMask.isCustomRangeActive = true;
            handMask.backSortingLayerID = mirrorSurfaceRenderer.sortingLayerID;
            handMask.frontSortingLayerID = mirrorSurfaceRenderer.sortingLayerID;
            handMask.backSortingOrder = 104;
            handMask.frontSortingOrder = 106;
            handMask.transform.localPosition = mirrorSurfaceRenderer.transform.localPosition;
            handMask.transform.localRotation = mirrorSurfaceRenderer.transform.localRotation;
            handMask.transform.localScale = mirrorSurfaceRenderer.transform.localScale;

            foreach (SpriteRenderer part in faceParts)
            {
                if (part == null || part.gameObject.name != "Hand") continue;
                part.sortingOrder = 105;
                part.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            }
        }

        faceBounds = GetVisibleFaceBounds(faceParts, true);
        Vector3 mirrorCenter = mirrorSurfaceBounds.center;
        Vector3 bottomAlignedCenter = new Vector3(mirrorCenter.x,
            mirrorSurfaceBounds.min.y + faceBounds.extents.y, mirrorCenter.z);
        mirrorFace.transform.position += bottomAlignedCenter - faceBounds.center;

        // Keep the frame above the face while leaving the transparent mirror opening clear.
        mirrorFrameRenderer.sortingOrder = Mathf.Max(mirrorFrameRenderer.sortingOrder, 110);
    }

    private static int MapMirrorFaceSortingOrder(int order, int minimum, int maximum)
    {
        if (maximum <= minimum) return 50;
        return order + 8 - minimum;
    }

    private static Bounds GetVisibleFaceBounds(SpriteRenderer[] faceParts, bool excludeHand)
    {
        Bounds result = default;
        bool found = false;
        foreach (SpriteRenderer part in faceParts)
        {
            if (part == null || part.sprite == null || (excludeHand && part.gameObject.name == "Hand")) continue;
            if (!found)
            {
                result = part.bounds;
                found = true;
            }
            else
            {
                result.Encapsulate(part.bounds);
            }
        }
        return result;
    }

    private void UpdateMirrorCharacter()
    {
        if (characterEyes == null || mirrorCharacterEyes == null) return;
        mirrorCharacterEyes.CopyVisualPoseFrom(characterEyes);
        if (mirrorSurfaceRenderer == null) return;

        Bounds characterBounds = GetVisibleFaceBounds(mirrorCharacterParts, true);
        float bottomCorrection = mirrorSurfaceRenderer.bounds.min.y - characterBounds.min.y;
        mirrorCharacterEyes.transform.position += Vector3.up * bottomCorrection;
    }
    private void SetupFisheyeEffect()
    {
        if (worldPanel == null) return;

        Shader fisheyeShader = Resources.Load<Shader>("RightEyeFisheye");
        if (fisheyeShader == null)
        {
            Debug.LogWarning("Could not find the right-panel fisheye shader.");
            return;
        }

        RawImage[] worldImages = worldPanel.GetComponentsInChildren<RawImage>(true);
        foreach (RawImage image in worldImages)
        {
            if (image == null || !(image.texture is RenderTexture)) continue;
            Material fisheyeMaterial = new Material(fisheyeShader);
            fisheyeMaterial.SetFloat("_FisheyeStrength", fisheyeStrength);
            fisheyeMaterial.SetFloat("_BlurRadiusPixels", maximumBlurRadiusPixels);
            fisheyeImages.Add(image);
            originalFisheyeMaterials.Add(image.material);
            runtimeFisheyeMaterials.Add(fisheyeMaterial);
            image.material = fisheyeMaterial;
        }
    }

    // Grabbing an eye on the sprite face exits focus, same as grabbing a UI eye.
    private void OnCharacterDragStarted()
    {
        characterEyeDragActive = true;
        if (!focused) return;
        focused = false;
        lockedFocusTarget = null;
        leftAim = Vector2.zero;
        rightAim = Vector2.zero;
        characterEyes.SetAims(leftAim, rightAim);
    }

    private void LateUpdate()
    {
        UpdateMirrorCharacter();
    }

    private void Update()
    {
        bool uiEyesReady = leftEyeRect != null && rightEyeRect != null && leftPupil != null && rightPupil != null;
        if (pointerPosition == null || eyeDrag == null || focusClick == null ||
            (characterEyes == null && !uiEyesReady) || worldPanel == null || leftCamera == null || rightCamera == null)
            return;

        Vector2 pointer = pointerPosition.ReadValue<Vector2>();
        if (grabRoutine != null || (characterEyes != null && characterEyes.IsUsingDeodorant))
        {
            Cursor.visible = false;
            return;
        }

        bool releasedEyeDrag = eyeDrag.WasReleasedThisFrame() &&
            (draggedEye != DraggedEye.None || characterEyeDragActive);
        bool overLeftPanel = leftPanel != null && RectTransformUtility.RectangleContainsScreenPoint(leftPanel, pointer, null);
        Cursor.visible = !overLeftPanel;
        UpdateArm(pointer, overLeftPanel);
        UpdateEyeFocusFeedback();
        bool overFace = pointer.x < Screen.width * 0.4f;

        if (characterEyes != null)
        {
            leftAim = characterEyes.LeftAimTarget;
            rightAim = characterEyes.RightAimTarget;
        }
        // Choose the eye once, on mouse-down. Keep that choice until mouse-up.
        else if (eyeDrag.WasPressedThisFrame())
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

        if (releasedEyeDrag)
        {
            characterEyeDragActive = false;
            if (!sequenceComplete && !focused && focusTargetCurrentlyAligned &&
                alignedFocusTarget != null && grownFocusTarget == alignedFocusTarget)
            {
                Transform newlyFocusedTarget = alignedFocusTarget;
                if (newlyFocusedTarget != null)
                {
                    lockedFocusTarget = newlyFocusedTarget;
                    focused = true;
                    if (lockedFocusTarget == mirrorTarget)
                    {
                        Debug.Log("wow you look so pretty");
                        sequenceComplete = true;
                    }
                }
            }
        }

        if (!sequenceComplete && !overFace && focused && focusClick.WasPressedThisFrame() &&
            IsPickupTarget(lockedFocusTarget) && grabRoutine == null &&
            IsPointerOnFocusTarget(pointer, lockedFocusTarget))
        {
            LogTargetInteraction(lockedFocusTarget);
            grabRoutine = StartCoroutine(GrabTargetRoutine(lockedFocusTarget));
        }

        if (characterEyes == null)
        {
            Vector2 wantedLeftPupil = leftAim * pupilTravel;
            Vector2 wantedRightPupil = rightAim * pupilTravel;
            leftPupil.anchoredPosition = Vector2.Lerp(
                leftPupil.anchoredPosition, leftPupilStart + wantedLeftPupil, Time.deltaTime * followSpeed);
            rightPupil.anchoredPosition = Vector2.Lerp(
                rightPupil.anchoredPosition, rightPupilStart + wantedRightPupil, Time.deltaTime * followSpeed);
        }

        Transform cameraFocusTarget = lockedFocusTarget != null ? lockedFocusTarget : alignedFocusTarget;
        Vector3 wantedLeftCamera = leftCameraStart;
        Vector3 wantedRightCamera = rightCameraStart;

        Quaternion wantedLeftRotation = focused && cameraFocusTarget != null
            ? Quaternion.LookRotation(cameraFocusTarget.position - leftCameraStart, Vector3.up)
            : leftCameraStartRotation * Quaternion.Euler(
                -leftAim.y * cameraAimRotationDegrees,
                leftAim.x * cameraAimRotationDegrees,
                0f);
        Quaternion wantedRightRotation = focused && cameraFocusTarget != null
            ? Quaternion.LookRotation(cameraFocusTarget.position - rightCameraStart, Vector3.up)
            : rightCameraStartRotation * Quaternion.Euler(
                -rightAim.y * cameraAimRotationDegrees,
                rightAim.x * cameraAimRotationDegrees,
                0f);

        leftCamera.transform.position = Vector3.Lerp(
            leftCamera.transform.position, wantedLeftCamera, Time.deltaTime * followSpeed);
        rightCamera.transform.position = Vector3.Lerp(
            rightCamera.transform.position, wantedRightCamera, Time.deltaTime * followSpeed);
        leftCamera.transform.rotation = Quaternion.Slerp(
            leftCamera.transform.rotation, wantedLeftRotation, Time.deltaTime * followSpeed);
        rightCamera.transform.rotation = Quaternion.Slerp(
            rightCamera.transform.rotation, wantedRightRotation, Time.deltaTime * followSpeed);
        UpdateFocusBlur();

        if (rightEyeView != null)
        {
            Color tint = rightImageColor;
            tint.a = Mathf.Lerp(rightEyeView.color.a, DoubleVisionAlpha, Time.deltaTime * followSpeed);
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
        Vector2 flatterDirection = new Vector2(direction.x, direction.y * 0.86f);
        float armLength = Mathf.Max(1f, flatterDirection.magnitude * 0.9f);

        armVisual.anchoredPosition = armBase;
        armVisual.sizeDelta = new Vector2(46f, armLength);
        armVisual.localRotation = Quaternion.Euler(
            0f, 0f, Mathf.Atan2(flatterDirection.y, flatterDirection.x) * Mathf.Rad2Deg - 90f);

        handVisual.anchoredPosition = local;
        handVisual.localRotation = Quaternion.identity;
        handVisual.localScale = Vector3.one * 0.9f;
    }

    private IEnumerator GrabTargetRoutine(Transform target)
    {
        if (!IsPickupTarget(target))
        {
            grabRoutine = null;
            yield break;
        }

        Image handImage = CreateGrabHand();
        if (handImage == null)
        {
            PlayGrabSound();
            target.gameObject.SetActive(false);
            CompleteGrabInteraction(target);
            grabRoutine = null;
            yield break;
        }

        RectTransform canvasRect = grabCanvas.transform as RectTransform;
        Camera canvasCamera = grabCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : grabCanvas.worldCamera;
        Vector2 startScreen = GetGrabStartScreenPoint(canvasCamera);
        Vector2 targetScreen = GetTargetScreenPoint(target, canvasCamera);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, startScreen, canvasCamera, out Vector2 startLocal);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, targetScreen, canvasCamera, out Vector2 targetLocal);

        handImage.rectTransform.anchoredPosition = startLocal;
        handImage.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -8f);
        handImage.color = Color.white;
        handImage.gameObject.SetActive(true);
        handImage.transform.SetAsLastSibling();

        yield return AnimateGrabHand(startLocal, targetLocal, 0.62f, false);
        if (target != null)
        {
            PlayGrabSound();
            target.gameObject.SetActive(false);
        }

        yield return new WaitForSeconds(0.12f);
        yield return AnimateGrabHand(targetLocal, startLocal, 0.58f, true);
        handImage.gameObject.SetActive(false);

        CompleteGrabInteraction(target);
        grabRoutine = null;
    }

    private void PlayGrabSound()
    {
        if (grabSound != null && grabAudioSource != null)
            grabAudioSource.PlayOneShot(grabSound);
    }

    private void CompleteGrabInteraction(Transform target)
    {
        if (target != null && target != mirrorTarget)
            collectedItems.Add(target);

        ResetAlignmentSnap();
        focusTargetCurrentlyAligned = false;
        focused = false;
        lockedFocusTarget = null;
        alignedFocusTarget = null;
        grownFocusTarget = null;
        UpdateSequenceLabelVisibility();
        if (target == deodorantTarget && characterEyes != null)
            characterEyes.BeginDeodorantUse();
    }

    private Image CreateGrabHand()
    {
        if (grabHandImage != null) return grabHandImage;

        Sprite pointingHandSprite = characterEyes != null ? characterEyes.PointingHandSprite : null;
        if (pointingHandSprite == null && handVisual != null)
        {
            Image existingHand = handVisual.GetComponent<Image>();
            if (existingHand != null) pointingHandSprite = existingHand.sprite;
        }
        if (pointingHandSprite == null || worldPanel == null) return null;

        grabCanvas = worldPanel.GetComponentInParent<Canvas>();
        if (grabCanvas == null) return null;

        GameObject handObject = new GameObject("Focus Grab Hand", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        handObject.transform.SetParent(grabCanvas.transform, false);
        grabHandImage = handObject.GetComponent<Image>();
        grabHandImage.sprite = pointingHandSprite;
        grabHandImage.preserveAspect = true;
        grabHandImage.raycastTarget = false;

        RectTransform handRect = grabHandImage.rectTransform;
        handRect.anchorMin = new Vector2(0.5f, 0.5f);
        handRect.anchorMax = new Vector2(0.5f, 0.5f);
        handRect.pivot = new Vector2(
            pointingHandSprite.pivot.x / pointingHandSprite.rect.width,
            pointingHandSprite.pivot.y / pointingHandSprite.rect.height);
        handRect.sizeDelta = pointingHandSprite.rect.size * (100f / pointingHandSprite.pixelsPerUnit) * 0.72f;
        handObject.SetActive(false);
        return grabHandImage;
    }

    private Vector2 GetGrabStartScreenPoint(Camera canvasCamera)
    {
        if (leftPanel != null)
        {
            Vector3[] corners = new Vector3[4];
            leftPanel.GetWorldCorners(corners);
            Vector3 point = Vector3.Lerp(corners[3], corners[2], 0.2f);
            return RectTransformUtility.WorldToScreenPoint(canvasCamera, point);
        }

        return new Vector2(Screen.width * 0.39f, Screen.height * 0.2f);
    }

    private Vector2 GetTargetScreenPoint(Transform target, Camera canvasCamera)
    {
        Vector3 worldPoint = GetVisibleTargetCenter(target);
        Vector3 leftView = leftCamera.WorldToViewportPoint(worldPoint);
        Vector3 rightView = rightCamera.WorldToViewportPoint(worldPoint);
        Vector2 leftOutput = FisheyeSourceToOutputUv(new Vector2(leftView.x, leftView.y));
        Vector2 rightOutput = FisheyeSourceToOutputUv(new Vector2(rightView.x, rightView.y));
        Vector2 outputUv = (leftOutput + rightOutput) * 0.5f;
        Rect panelRect = worldPanel.rect;
        Vector3 panelLocal = new Vector3(
            Mathf.Lerp(panelRect.xMin, panelRect.xMax, outputUv.x),
            Mathf.Lerp(panelRect.yMin, panelRect.yMax, outputUv.y),
            0f);
        Vector3 panelWorld = worldPanel.TransformPoint(panelLocal);
        return RectTransformUtility.WorldToScreenPoint(canvasCamera, panelWorld);
    }

    private static Vector3 GetVisibleTargetCenter(Transform target)
    {
        if (target == null) return Vector3.zero;
        SpriteRenderer[] renderers = target.GetComponentsInChildren<SpriteRenderer>();
        if (renderers.Length == 0) return target.position;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);
        return bounds.center;
    }

    private bool IsPickupTarget(Transform target)
    {
        return sequenceEnabled && target != null &&
            (target == deodorantTarget || target == mouthwashTarget || target == toothbrushTarget) &&
            !collectedItems.Contains(target);
    }

    private IEnumerator AnimateGrabHand(Vector2 from, Vector2 to, float duration, bool fadeOut)
    {
        float canvasScale = grabCanvas != null ? Mathf.Max(0.01f, grabCanvas.scaleFactor) : 1f;
        Vector2 control = (from + to) * 0.5f + Vector2.up * (Screen.height / canvasScale * 0.07f);
        Color handColor = grabHandImage.color;
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = t * t * (3f - 2f * t);
            float inverse = 1f - eased;
            grabHandImage.rectTransform.anchoredPosition =
                inverse * inverse * from + 2f * inverse * eased * control + eased * eased * to;
            handColor.a = fadeOut ? 1f - Mathf.InverseLerp(0.35f, 1f, t) : 1f;
            grabHandImage.color = handColor;
            yield return null;
        }

        grabHandImage.rectTransform.anchoredPosition = to;
        handColor.a = fadeOut ? 0f : 1f;
        grabHandImage.color = handColor;
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
            if (lockedFocusTarget != null) return lockedFocusTarget;
            if (alignedFocusTarget != null) return alignedFocusTarget;
            return AreAllItemsCollected() ? mirrorTarget : null;
        }
    }

    private Transform FindAlignedFocusTarget()
    {
        if (!sequenceEnabled)
            return focusTarget != null && CenterRayHitsTarget(leftCamera, focusTarget) &&
                CenterRayHitsTarget(rightCamera, focusTarget) ? focusTarget : null;

        if (AreAllItemsCollected())
            return mirrorTarget != null && CenterRayHitsTarget(leftCamera, mirrorTarget) &&
                CenterRayHitsTarget(rightCamera, mirrorTarget) ? mirrorTarget : null;

        if (IsAvailableAndAligned(deodorantTarget)) return deodorantTarget;
        if (IsAvailableAndAligned(mouthwashTarget)) return mouthwashTarget;
        if (IsAvailableAndAligned(toothbrushTarget)) return toothbrushTarget;

        return null;
    }

    private bool IsAvailableAndAligned(Transform target)
    {
        return target != null && !collectedItems.Contains(target) && target.gameObject.activeInHierarchy &&
            CenterRayHitsTarget(leftCamera, target) && CenterRayHitsTarget(rightCamera, target);
    }

    private bool AreAllItemsCollected()
    {
        return deodorantTarget != null && mouthwashTarget != null && toothbrushTarget != null &&
            collectedItems.Contains(deodorantTarget) && collectedItems.Contains(mouthwashTarget) &&
            collectedItems.Contains(toothbrushTarget);
    }

    private void LogTargetInteraction(Transform target)
    {
        if (!sequenceEnabled)
        {
            Debug.Log("you interacted with " + (target != null ? target.name : "the object"));
            return;
        }

        if (target == deodorantTarget) Debug.Log("you interacted with deodorant");
        else if (target == mouthwashTarget) Debug.Log("you interacted with mouthwash");
        else if (target == toothbrushTarget) Debug.Log("you interacted with toothbrush");
        else Debug.Log("you interacted with " + (target != null ? target.name : "the object"));
    }

    private void UpdateSequenceLabelVisibility()
    {
        if (!sequenceEnabled) return;
        bool mirrorAvailable = AreAllItemsCollected();
        Transform[] targets = { deodorantTarget, mouthwashTarget, toothbrushTarget, mirrorTarget };
        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] == null) continue;
            TextMesh[] labels = targets[i].GetComponentsInChildren<TextMesh>(true);
            foreach (TextMesh label in labels)
            {
                bool itemAvailable = i < 3 && !collectedItems.Contains(targets[i]) && targets[i].gameObject.activeInHierarchy;
                bool showLabel = i == 3 ? mirrorAvailable : itemAvailable;
                label.gameObject.SetActive(showLabel);
            }
        }
    }




    private void CacheFocusRayRenderers()
    {
        EnsureFocusTargetCollider(focusTarget);
        EnsureFocusTargetCollider(deodorantTarget);
        EnsureFocusTargetCollider(mouthwashTarget);
        EnsureFocusTargetCollider(toothbrushTarget);
        EnsureFocusTargetCollider(mirrorTarget);
        focusRayRenderers = FindObjectsOfType<SpriteRenderer>();
        Physics.SyncTransforms();
    }

    private void EnsureFocusTargetCollider(Transform target)
    {
        if (target == null) return;
        SpriteRenderer[] renderers = target.GetComponentsInChildren<SpriteRenderer>();
        if (renderers.Length == 0) return;

        Bounds worldBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            worldBounds.Encapsulate(renderers[i].bounds);

        Bounds localBounds = new Bounds(target.InverseTransformPoint(worldBounds.center), Vector3.zero);
        for (int x = 0; x < 2; x++)
        for (int y = 0; y < 2; y++)
        for (int z = 0; z < 2; z++)
        {
            Vector3 corner = new Vector3(
                x == 0 ? worldBounds.min.x : worldBounds.max.x,
                y == 0 ? worldBounds.min.y : worldBounds.max.y,
                z == 0 ? worldBounds.min.z : worldBounds.max.z);
            localBounds.Encapsulate(target.InverseTransformPoint(corner));
        }

        BoxCollider targetCollider = target.GetComponent<BoxCollider>();
        if (targetCollider == null) targetCollider = target.gameObject.AddComponent<BoxCollider>();
        targetCollider.center = localBounds.center;
        targetCollider.size = Vector3.Max(localBounds.size, Vector3.one * 0.01f);
        targetCollider.isTrigger = true;
    }

    private struct CenterRayHit
    {
        public SpriteRenderer Renderer;
        public Vector3 Point;
        public float Distance;
    }

    private CenterRayHit FindCenterRayHit(Camera eyeCamera)
    {
        Ray ray = eyeCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        float nearestDistance = Mathf.Max(eyeCamera.nearClipPlane, focusRayDistance);
        SpriteRenderer nearestRenderer = null;

        foreach (SpriteRenderer candidate in focusRayRenderers)
        {
            if (candidate == null || !candidate.enabled || !candidate.gameObject.activeInHierarchy ||
                (eyeCamera.cullingMask & (1 << candidate.gameObject.layer)) == 0)
                continue;

            if (candidate.bounds.IntersectRay(ray, out float distance) &&
                distance >= eyeCamera.nearClipPlane && distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestRenderer = candidate;
            }
        }

        return new CenterRayHit
        {
            Renderer = nearestRenderer,
            Distance = nearestDistance,
            Point = ray.GetPoint(nearestDistance)
        };
    }

    private static bool RendererBelongsToTarget(SpriteRenderer renderer, Transform target)
    {
        return renderer != null && target != null && (renderer.transform == target || renderer.transform.IsChildOf(target));
    }

    private bool CenterRayHitsTarget(Camera eyeCamera, Transform target)
    {
        if (eyeCamera == null || target == null) return false;
        Ray ray = eyeCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        RaycastHit[] hits = Physics.SphereCastAll(
            ray, focusSphereCastRadius, focusRayDistance, eyeCamera.cullingMask, QueryTriggerInteraction.Collide);

        float nearestDistance = float.PositiveInfinity;
        Collider nearestCollider = null;
        foreach (RaycastHit hit in hits)
        {
            if (hit.distance >= nearestDistance) continue;
            nearestDistance = hit.distance;
            nearestCollider = hit.collider;
        }

        return nearestCollider != null &&
            (nearestCollider.transform == target || nearestCollider.transform.IsChildOf(target));
    }

    private void UpdateEyeFocusFeedback()
    {
        Physics.SyncTransforms();
        Transform target = focused && lockedFocusTarget != null
            ? lockedFocusTarget
            : FindAlignedFocusTarget();

        if (target != alignedFocusTarget ||
            (snappingTarget != null && snappingTarget != target) ||
            (grownFocusTarget != null && grownFocusTarget != target))
        {
            ResetAlignmentSnap();
            focusTargetCurrentlyAligned = false;
        }
        alignedFocusTarget = target;

        bool targetAligned = target != null;
        if (targetAligned && !focusTargetCurrentlyAligned)
            PlayAlignmentSnap(target);
        else if (!targetAligned && focusTargetCurrentlyAligned)
            ResetAlignmentSnap();
        focusTargetCurrentlyAligned = targetAligned;

        if (leftEyeImage != null)
            leftEyeImage.color = target != null && CenterRayHitsTarget(leftCamera, target)
                ? new Color(0.62f, 1f, 0.72f, leftEyeBaseColor.a) : leftEyeBaseColor;
        if (rightEyeImage != null)
            rightEyeImage.color = target != null && CenterRayHitsTarget(rightCamera, target)
                ? new Color(0.62f, 1f, 0.72f, rightEyeBaseColor.a) : rightEyeBaseColor;
    }

    private void PlayAlignmentSnap(Transform target)
    {
        ResetAlignmentSnap();

        // MirrorTarget contains the mirror art and reflection, so never scale this root for the snap pop.
        if (target == mirrorTarget)
        {
            grownFocusTarget = target;
            return;
        }

        snappingTarget = target;
        snappingTargetOriginalScale = target.localScale;
        alignmentSnapRoutine = StartCoroutine(AlignmentSnapAnimation(target, snappingTargetOriginalScale));
    }

    private void ResetAlignmentSnap()
    {
        if (alignmentSnapRoutine != null) StopCoroutine(alignmentSnapRoutine);
        if (snappingTarget != null) snappingTarget.localScale = snappingTargetOriginalScale;
        alignmentSnapRoutine = null;
        snappingTarget = null;
        grownFocusTarget = null;
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

        if (target == null) yield break;
        target.localScale = popScale;
        grownFocusTarget = target;

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

    private bool IsPointerOnFocusTarget(Vector2 screenPointer, Transform target)
    {
        // Focus is already locked to the item after both eye rays converge and the player
        // releases the drag. Use a right-panel click to activate that locked item; testing
        // the pixel bounds of both distorted, offset images made some targets unclickable.
        return target != null && target == lockedFocusTarget && focused && worldPanel != null &&
            RectTransformUtility.RectangleContainsScreenPoint(worldPanel, screenPointer, null);
    }

    private Vector2 FisheyeSourceToOutputUv(Vector2 sourceUv)
    {
        float strength = Mathf.Clamp(fisheyeStrength, 0f, 0.8f);
        if (strength <= 0.0001f) return sourceUv;

        Vector2 centered = sourceUv * 2f - Vector2.one;
        float sourceRadius = centered.magnitude;
        if (sourceRadius <= 0.0001f) return sourceUv;

        float discriminant = Mathf.Max(0f, 1f - 2f * strength * sourceRadius * sourceRadius);
        float outputRadius = (2f * sourceRadius) / (1f + Mathf.Sqrt(discriminant));
        return centered * (outputRadius / sourceRadius) * 0.5f + Vector2.one * 0.5f;
    }

    private void UpdateFocusBlur()
    {
        float cameraSeparation = Vector3.Angle(leftCamera.transform.forward, rightCamera.transform.forward);
        float separationAmount = Mathf.InverseLerp(
            0f, Mathf.Max(0.1f, maximumCameraSeparationForBlurDegrees), cameraSeparation);
        float blurAmount = Mathf.SmoothStep(0f, maximumMisalignmentBlur, separationAmount);
        Transform target = focused && lockedFocusTarget != null ? lockedFocusTarget : CurrentFocusTarget;
        CenterRayHit leftHit = FindCenterRayHit(leftCamera);
        CenterRayHit rightHit = FindCenterRayHit(rightCamera);
        Physics.SyncTransforms();
        bool bothRaysHitTarget = target != null &&
            CenterRayHitsTarget(leftCamera, target) && CenterRayHitsTarget(rightCamera, target);

        for (int i = 0; i < fisheyeImages.Count; i++)
        {
            if (fisheyeImages[i] == null || runtimeFisheyeMaterials[i] == null) continue;


            Camera eyeCamera = fisheyeImages[i] == rightEyeView ? rightCamera : leftCamera;
            CenterRayHit rayHit = eyeCamera == rightCamera ? rightHit : leftHit;
            SetFocusBlurForCamera(runtimeFisheyeMaterials[i], eyeCamera, rayHit, target, bothRaysHitTarget, blurAmount);
        }

    }

    private void SetFocusBlurForCamera(Material material, Camera eyeCamera, CenterRayHit rayHit,
        Transform target, bool bothRaysHitTarget, float blurAmount)
    {
        material.SetFloat("_BlurStrength", blurAmount);
        Vector3 centerView = eyeCamera.WorldToViewportPoint(rayHit.Point);
        Vector2 focusCenter = FisheyeSourceToOutputUv(new Vector2(centerView.x, centerView.y));

        float focusRadius = focusPointRadius;

        bool rayHitsActiveTarget = RendererBelongsToTarget(rayHit.Renderer, target);
        SpriteRenderer[] focusRenderers = bothRaysHitTarget
            ? target.GetComponentsInChildren<SpriteRenderer>()
            : rayHit.Renderer == null || rayHitsActiveTarget
                ? new SpriteRenderer[0]
                : new[] { rayHit.Renderer };

        foreach (SpriteRenderer spriteRenderer in focusRenderers)
        {
            if (spriteRenderer == null) continue;
            Bounds bounds = spriteRenderer.bounds;
            for (int x = 0; x < 2; x++)
            for (int y = 0; y < 2; y++)
            for (int z = 0; z < 2; z++)
            {
                Vector3 corner = new Vector3(
                    x == 0 ? bounds.min.x : bounds.max.x,
                    y == 0 ? bounds.min.y : bounds.max.y,
                    z == 0 ? bounds.min.z : bounds.max.z);
                Vector3 cornerView = eyeCamera.WorldToViewportPoint(corner);
                if (cornerView.z <= 0f) continue;
                Vector2 cornerOutput = FisheyeSourceToOutputUv(new Vector2(cornerView.x, cornerView.y));
                focusRadius = Mathf.Max(focusRadius, Vector2.Distance(focusCenter, cornerOutput));
            }
        }

        focusRadius = Mathf.Clamp(focusRadius, focusPointRadius, 0.3f);
        material.SetVector("_FocusCenter", new Vector4(focusCenter.x, focusCenter.y, 0f, 0f));
        material.SetFloat("_FocusRadius", focusRadius);
        material.SetFloat("_FocusFeather", focusRadius * 1.5f + 0.02f);
    }

}





