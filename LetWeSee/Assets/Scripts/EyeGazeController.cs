using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

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
    [SerializeField] private Camera faceCamera;

    [Header("World panel")]
    [SerializeField] private RectTransform worldPanel;
    [SerializeField] private Camera leftCamera;
    [SerializeField] private Camera rightCamera;
    [SerializeField] private Transform focusTarget;
    [Tooltip("Extra objects the eyes can lock onto outside the bathroom sequence.")]
    [SerializeField] private Transform[] focusTargets = new Transform[0];
    [SerializeField] private Transform deodorantTarget;
    [SerializeField] private Transform mouthwashTarget;
    [SerializeField] private Transform toothbrushTarget;
    [SerializeField] private Transform mirrorTarget;
    [SerializeField] private RawImage rightEyeView;
    [SerializeField] private AudioClip grabSound;
    [Header("Intro dialogue")]
    [SerializeField] private Sprite dialogueBoxSprite;
    [SerializeField] private Font dialogueFont;
    [SerializeField] private Transform dateDialogueTarget;
    [SerializeField, Range(0.2f, 1f)] private float mirrorFaceWidthRatio = 0.82f;
    [SerializeField, Range(0.2f, 1f)] private float mirrorFaceHeightRatio = 0.88f;

    [Header("Tuning")]
    [SerializeField, Range(0f, 30f)] private float pupilTravel = 14f;
    [SerializeField, Range(0f, 12f)] private float cameraTravel = 10f;
    [Tooltip("Distance between the two eye cameras on the x axis, centred on where they start. Can be changed while playing.")]
    [SerializeField, Range(0f, 5f)] private float eyeSeparation = 1f;
    [Tooltip("When the two eyes' gaze points are within this angle of each other (seen from between the eyes), the moving eye snaps onto the other's point. An angle works the same up close and far away.")]
    [SerializeField, Range(0f, 10f)] private float gazeSnapAngle = 0.7f;
    [Tooltip("Angle the moving eye's own point has to get from the other's before the snap breaks off. Keep it above the snap angle.")]
    [SerializeField, Range(0f, 10f)] private float gazeSnapReleaseAngle = 1f;
    [SerializeField, Range(0f, 45f)] private float cameraAimRotationDegrees = 28f;
    [SerializeField, Range(0f, 0.8f)] private float fisheyeStrength = 0.45f;
    [SerializeField, Range(0f, 1f)] private float maximumMisalignmentBlur = 1f;
    [SerializeField, Range(0f, 24f)] private float maximumBlurRadiusPixels = 12f;
    [SerializeField, Range(0.1f, 45f)] private float maximumCameraSeparationForBlurDegrees = 20f;
    [SerializeField, Range(0.05f, 1f)] private float focusSphereCastRadius = 0.4f;
    [SerializeField, Range(0.005f, 0.2f)] private float focusPointRadius = 0.1f;
    [SerializeField, Range(1f, 20f)] private float followSpeed = 10f;
    [SerializeField, Range(0.1f, 2f)] private float normalEyeAimSensitivity = 1f;
    [SerializeField, Range(0.01f, 1f)] private float draggingEyeAimSensitivity = 0.01f;
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
    private bool sceneTransitioning;
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
    private bool gazeSnapped;
    private Renderer[] gazeRenderers;
    private bool rightEyeMovedLast = true;
    private Vector2 previousLeftAim;
    private Vector2 previousRightAim;
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
    private Renderer[] focusRayRenderers = new Renderer[0];
    private Image leftEyeImage;
    private Image rightEyeImage;
    private Color leftEyeBaseColor;
    private Color rightEyeBaseColor;
    private bool previousCursorVisible;
    private CursorLockMode previousCursorLockState;
    private bool faceUseCursorConfined;
    private bool introDialogueActive;
    private int introDialogueIndex;
    private RectTransform introDialoguePanel;
    private Text introDialogueText;
    private string[] activeDialogueLines;
    private bool cafeDialogueSequence;
    private int cafeDialoguePhase;
    private bool dialogueOnDateSide;
    private Coroutine dialogueTextAnimation;
    private static readonly string[] IntroDialogueLines =
    {
        "ugh... my eyes get so damn itchy when i wake up",
        "time to make myself pretty...",
        "teeth... deodorant... breath - that should be enough",
        "ugh MY EYyESZSZsSzzAA"
    };
    private const float DoubleVisionAlpha = 0.48f;

    private void Start()
    {
        if (SceneManager.GetActiveScene().name == "Cafe")
        {
            if (dateDialogueTarget == null)
            {
                GameObject dateObject = GameObject.Find("Date");
                if (dateObject != null) dateDialogueTarget = dateObject.transform;
            }

            cafeDialogueSequence = true;
            cafeDialoguePhase = 1;
            BeginDialogue(new[]
            {
                "wow this restaurant is so pretty",
                "you took a while to get here, is everything okay?"
            }, true);
            return;
        }

        BeginDialogue(IntroDialogueLines);
    }

    private void BeginDialogue(string[] lines, bool onDateSide = false)
    {
        if (lines == null || lines.Length == 0 || focusClick == null) return;
        dialogueOnDateSide = onDateSide;
        if ((dialogueOnDateSide && worldPanel == null) || (!dialogueOnDateSide && leftPanel == null)) return;

        activeDialogueLines = lines;
        introDialogueIndex = 0;
        introDialogueActive = true;
        if (introDialoguePanel == null)
            CreateIntroDialogueBox();
        else
            SetIntroDialogueLine(activeDialogueLines[0]);

        if (introDialoguePanel == null)
        {
            introDialogueActive = false;
            return;
        }

        if (characterEyes != null) characterEyes.SetIntroDialogueLocked(true);
        Cursor.visible = true;
    }

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

        if (SceneManager.GetActiveScene().name == "Cafe" && focusTarget == null)
        {
            GameObject coffeeObject = GameObject.Find("YOUR COFFEE");
            if (coffeeObject == null) coffeeObject = GameObject.Find("Coffee");
            if (coffeeObject != null) focusTarget = coffeeObject.transform;
        }

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



    // The bathroom items and room are authored in the scene; nothing is generated here.
    private void EnsureSequenceTargets()
    {
        if (deodorantTarget == null) deodorantTarget = FindTarget("DeodorantTarget");
        if (mouthwashTarget == null) mouthwashTarget = FindTarget("MouthwashTarget");
        if (toothbrushTarget == null) toothbrushTarget = FindTarget("ToothbrushTarget");
        if (mirrorTarget == null) mirrorTarget = FindTarget("MirrorTarget");

        sequenceEnabled = deodorantTarget != null && mouthwashTarget != null && toothbrushTarget != null && mirrorTarget != null;
        if (sequenceEnabled)
        {
            cameraTravel = Mathf.Max(cameraTravel, 10f);
            UpdateSequenceLabelVisibility();
        }
        if (sequenceEnabled && focusTarget != null && focusTarget != deodorantTarget)
            focusTarget.gameObject.SetActive(false);
    }

    private static Transform FindTarget(string targetName)
    {
        GameObject found = GameObject.Find(targetName);
        return found != null ? found.transform : null;
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
        faceUseCursorConfined = false;
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

    // Grabbing an eye on the sprite face exits focus. The eyes stay where they were aimed.
    private void OnCharacterDragStarted()
    {
        characterEyeDragActive = true;
        if (!focused) return;
        focused = false;
        lockedFocusTarget = null;
    }

    private void LateUpdate()
    {
        UpdateMirrorCharacter();
        PositionIntroDialogueAtMouth();
    }

    private void ConfineCursorToLeftPanel(Vector2 pointer)
    {
        if (leftPanel == null || Mouse.current == null) return;

        // Let the pointer enter the face area from the world side, then keep it there
        // for the mouth interaction. Unity's built-in Confined mode only supports the
        // whole game window, so warp at the panel boundary instead.
        if (!faceUseCursorConfined)
        {
            if (!RectTransformUtility.RectangleContainsScreenPoint(leftPanel, pointer, null)) return;
            faceUseCursorConfined = true;
        }

        Vector3[] corners = new Vector3[4];
        leftPanel.GetWorldCorners(corners);
        Rect panelRect = new Rect(float.PositiveInfinity, float.PositiveInfinity,
            float.NegativeInfinity, float.NegativeInfinity);
        Canvas canvas = leftPanel.GetComponentInParent<Canvas>();
        Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera : null;
        for (int i = 0; i < corners.Length; i++)
        {
            Vector2 screenCorner = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[i]);
            panelRect.xMin = Mathf.Min(panelRect.xMin, screenCorner.x);
            panelRect.yMin = Mathf.Min(panelRect.yMin, screenCorner.y);
            panelRect.xMax = Mathf.Max(panelRect.xMax, screenCorner.x);
            panelRect.yMax = Mathf.Max(panelRect.yMax, screenCorner.y);
        }

        const float inset = 2f;
        Vector2 confinedPointer = new Vector2(
            Mathf.Clamp(pointer.x, panelRect.xMin + inset, panelRect.xMax - inset),
            Mathf.Clamp(pointer.y, panelRect.yMin + inset, panelRect.yMax - inset));
        if ((confinedPointer - pointer).sqrMagnitude > 0.01f)
            Mouse.current.WarpCursorPosition(confinedPointer);
    }
    private void PositionIntroDialogueAtMouth()
    {
        if (!introDialogueActive || introDialoguePanel == null) return;
        if (dialogueOnDateSide)
        {
            PositionDateDialogueUnderCharacter();
            return;
        }
        if (leftPanel == null || characterEyes == null || faceCamera == null) return;

        Vector3 mouthScreen = faceCamera.WorldToScreenPoint(characterEyes.MouthWorldPosition);
        if (mouthScreen.z < 0f) return;

        Canvas canvas = leftPanel.GetComponentInParent<Canvas>();
        Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                leftPanel, mouthScreen, uiCamera, out Vector2 mouthLocal))
            return;

        Vector2 tailOffset = new Vector2(
            (0.62f - introDialoguePanel.pivot.x) * introDialoguePanel.rect.width * introDialoguePanel.localScale.x,
            0.92f * introDialoguePanel.rect.height * introDialoguePanel.localScale.y);
        Vector2 anchorReference = new Vector2(
            Mathf.Lerp(leftPanel.rect.xMin, leftPanel.rect.xMax, 0.5f),
            Mathf.Lerp(leftPanel.rect.yMin, leftPanel.rect.yMax, 0.095f));
        introDialoguePanel.anchoredPosition = mouthLocal - anchorReference - tailOffset;
    }

    private void PositionDateDialogueUnderCharacter()
    {
        if (dateDialogueTarget == null || rightCamera == null || worldPanel == null) return;

        // The mouth sits just above the vertical center of the Date portrait texture.
        // Anchor the speech bubble tip there so the box hangs below it, toward the bottom of the panel.
        const float mouthHeightFromPortraitBottom = 0.54f;
        Vector3 dateAnchor = dateDialogueTarget.TransformPoint(
            new Vector3(0f, mouthHeightFromPortraitBottom - 0.5f, 0f));

        Vector3 viewport = rightCamera.WorldToViewportPoint(dateAnchor);
        if (viewport.z < 0f) return;

        Rect panelRect = worldPanel.rect;
        Vector2 dateLocal = new Vector2(
            Mathf.Lerp(panelRect.xMin, panelRect.xMax, Mathf.Clamp01(viewport.x)),
            Mathf.Lerp(panelRect.yMin, panelRect.yMax, Mathf.Clamp01(viewport.y)));
        Vector2 tailOffset = new Vector2(
            (0.62f - introDialoguePanel.pivot.x) * introDialoguePanel.rect.width * introDialoguePanel.localScale.x,
            0.92f * introDialoguePanel.rect.height * introDialoguePanel.localScale.y);
        Vector2 anchorReference = new Vector2(
            Mathf.Lerp(panelRect.xMin, panelRect.xMax, 0.5f), panelRect.yMin);
        introDialoguePanel.anchoredPosition = dateLocal - anchorReference - tailOffset;
    }

    private void CreateIntroDialogueBox()
    {
        RectTransform dialogueParent = dialogueOnDateSide ? worldPanel : leftPanel;
        if (dialogueParent == null) return;

        string dialogueObjectName = dialogueOnDateSide ? "Date Dialogue" : "Bathroom Intro Dialogue";
        GameObject panelObject = new GameObject(dialogueObjectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        introDialoguePanel = panelObject.GetComponent<RectTransform>();
        introDialoguePanel.SetParent(dialogueParent, false);
        float dialogueAnchorY = dialogueOnDateSide ? 0f : 0.095f;
        introDialoguePanel.anchorMin = new Vector2(0.15f, dialogueAnchorY);
        introDialoguePanel.anchorMax = new Vector2(0.85f, dialogueAnchorY);
        introDialoguePanel.pivot = new Vector2(0.5f, 0f);
        introDialoguePanel.anchoredPosition = Vector2.zero;
        introDialoguePanel.offsetMin = Vector2.zero;
        introDialoguePanel.offsetMax = Vector2.zero;
        AspectRatioFitter dialogueAspect = panelObject.AddComponent<AspectRatioFitter>();
        dialogueAspect.aspectMode = AspectRatioFitter.AspectMode.WidthControlsHeight;
        dialogueAspect.aspectRatio = 1907f / 908f;
        introDialoguePanel.SetAsLastSibling();

        Image background = panelObject.GetComponent<Image>();
        background.sprite = dialogueBoxSprite;
        background.preserveAspect = true;
        background.color = Color.white;
        background.raycastTarget = true;

        GameObject textObject = new GameObject("Dialogue Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.SetParent(introDialoguePanel, false);
        textRect.anchorMin = new Vector2(0.04f, 0.05f);
        textRect.anchorMax = new Vector2(0.96f, 0.53f);
        textRect.offsetMin = new Vector2(8f, 4f);
        textRect.offsetMax = new Vector2(-8f, -50f);

        introDialogueText = textObject.GetComponent<Text>();
        introDialogueText.font = dialogueFont != null
            ? dialogueFont
            : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        introDialogueText.fontSize = 32;
        introDialogueText.resizeTextForBestFit = true;
        introDialogueText.resizeTextMinSize = 24;
        introDialogueText.resizeTextMaxSize = 32;
        introDialogueText.alignment = TextAnchor.MiddleCenter;
        introDialogueText.horizontalOverflow = HorizontalWrapMode.Wrap;
        introDialogueText.verticalOverflow = VerticalWrapMode.Truncate;
        introDialogueText.color = new Color(0.16f, 0.08f, 0.05f, 1f);
        introDialogueText.raycastTarget = false;
        SetIntroDialogueLine(activeDialogueLines[0]);
    }
    private void SetIntroDialogueLine(string line)
    {
        StopIntroDialogueAnimation();
        if (characterEyes != null)
            characterEyes.PlayDialogueVoice(dialogueOnDateSide);

        dialogueTextAnimation = StartCoroutine(AnimateIntroDialogueLine(line));
    }

    private void StopIntroDialogueAnimation()
    {
        if (dialogueTextAnimation == null) return;
        StopCoroutine(dialogueTextAnimation);
        dialogueTextAnimation = null;
    }

    private IEnumerator AnimateIntroDialogueLine(string line)
    {
        if (introDialogueText == null || introDialoguePanel == null)
        {
            dialogueTextAnimation = null;
            yield break;
        }

        RectTransform textRect = introDialogueText.rectTransform;
        Vector2 restingPosition = textRect.anchoredPosition;
        string[] words = line.Split(' ');
        string completedText = string.Empty;
        introDialogueText.text = string.Empty;

        foreach (string word in words)
        {
            if (introDialogueText == null || textRect == null || introDialoguePanel == null)
            {
                dialogueTextAnimation = null;
                yield break;
            }
            if (string.IsNullOrEmpty(word)) continue;

            completedText += word + " ";
            introDialogueText.text = completedText.TrimEnd();
            const float popDuration = 0.29f;
            float elapsed = 0f;
            textRect.localScale = Vector3.one;
            textRect.anchoredPosition = restingPosition + new Vector2(-16f, 0f);

            while (elapsed < popDuration)
            {
                if (introDialogueText == null || textRect == null || introDialoguePanel == null)
                {
                    dialogueTextAnimation = null;
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / popDuration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                float slide = Mathf.Lerp(-16f, 0f, eased) + Mathf.Sin(t * Mathf.PI) * 5f;
                float lift = Mathf.Sin(t * Mathf.PI) * 4f;
                textRect.anchoredPosition = restingPosition + new Vector2(
                    Mathf.Round(slide), Mathf.Round(lift));
                yield return null;
            }

            if (introDialogueText == null || textRect == null || introDialoguePanel == null)
            {
                dialogueTextAnimation = null;
                yield break;
            }

            textRect.localScale = Vector3.one;
            textRect.anchoredPosition = restingPosition;
            yield return new WaitForSecondsRealtime(0.055f);
        }

        if (introDialogueText != null && textRect != null && introDialoguePanel != null)
        {
            textRect.localScale = Vector3.one;
            textRect.anchoredPosition = restingPosition;
        }
        dialogueTextAnimation = null;
    }

    private void UpdateIntroDialogue()
    {
        Cursor.visible = true;
        if (pointerPosition == null || focusClick == null || introDialoguePanel == null) return;

        Vector2 pointer = pointerPosition.ReadValue<Vector2>();
        bool hovering = RectTransformUtility.RectangleContainsScreenPoint(introDialoguePanel, pointer, null);
        float targetScale = hovering ? 1.04f : 1f;
        float scaleBlend = 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime);
        introDialoguePanel.localScale = Vector3.Lerp(
            introDialoguePanel.localScale, Vector3.one * targetScale, scaleBlend);

        if (dialogueTextAnimation != null || !focusClick.WasPressedThisFrame() || !hovering)
            return;

        introDialogueIndex++;
        if (activeDialogueLines != null && introDialogueIndex < activeDialogueLines.Length)
        {
            SetIntroDialogueLine(activeDialogueLines[introDialogueIndex]);
            return;
        }

        if (cafeDialogueSequence)
        {
            AdvanceCafeDialogueSequence();
            return;
        }

        StopIntroDialogueAnimation();
        introDialogueActive = false;
        RectTransform completedPanel = introDialoguePanel;
        introDialoguePanel = null;
        introDialogueText = null;
        if (completedPanel != null) Destroy(completedPanel.gameObject);
        if (sequenceComplete)
        {
            StartCoroutine(FadeToCafe());
            return;
        }
        if (characterEyes != null) characterEyes.PopHandUpAfterIntroDialogue();
    }

    private void AdvanceCafeDialogueSequence()
    {
        StopIntroDialogueAnimation();
        if (introDialoguePanel != null) Destroy(introDialoguePanel.gameObject);
        introDialoguePanel = null;
        introDialogueText = null;
        introDialogueActive = false;

        if (cafeDialoguePhase == 1)
        {
            cafeDialoguePhase = 2;
            BeginDialogue(new[] { "yeah my eyes are a bit sore" });
            return;
        }

        if (cafeDialoguePhase == 2)
        {
            cafeDialoguePhase = 3;
            BeginDialogue(new[] { "shame babe, well let's order something" }, true);
            return;
        }

        cafeDialogueSequence = false;
        if (characterEyes != null) characterEyes.PopHandUpAfterIntroDialogue();
    }
    private IEnumerator FadeToCafe()
    {
        sceneTransitioning = true;
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.None;

        Canvas fadeCanvas = leftPanel != null ? leftPanel.GetComponentInParent<Canvas>() : null;
        if (fadeCanvas == null) fadeCanvas = FindAnyObjectByType<Canvas>();
        if (fadeCanvas == null)
        {
            Debug.LogError("Could not find a Canvas for the scene transition fade.");
            SceneManager.LoadScene("Cafe");
            yield break;
        }

        GameObject fadeObject = new GameObject("Scene Fade", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform fadeRect = fadeObject.GetComponent<RectTransform>();
        fadeRect.SetParent(fadeCanvas.transform, false);
        fadeRect.anchorMin = Vector2.zero;
        fadeRect.anchorMax = Vector2.one;
        fadeRect.offsetMin = Vector2.zero;
        fadeRect.offsetMax = Vector2.zero;
        fadeRect.SetAsLastSibling();

        Image fadeImage = fadeObject.GetComponent<Image>();
        fadeImage.color = new Color(0f, 0f, 0f, 0f);
        fadeImage.raycastTarget = false;

        const float fadeDuration = 1.25f;
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float alpha = Mathf.Clamp01(elapsed / fadeDuration);
            fadeImage.color = new Color(0f, 0f, 0f, alpha);
            yield return null;
        }

        fadeImage.color = Color.black;
        SceneManager.LoadScene("Cafe");
    }
    private void Update()
    {
        if (sceneTransitioning) return;
        if (introDialogueActive)
        {
            UpdateIntroDialogue();
            return;
        }

        bool uiEyesReady = leftEyeRect != null && rightEyeRect != null && leftPupil != null && rightPupil != null;
        if (pointerPosition == null || eyeDrag == null || focusClick == null ||
            (characterEyes == null && !uiEyesReady) || worldPanel == null || leftCamera == null || rightCamera == null)
            return;

        Vector2 pointer = pointerPosition.ReadValue<Vector2>();
        bool mouthwashActive = characterEyes != null && characterEyes.IsUsingMouthwash;
        bool toothbrushActive = characterEyes != null && characterEyes.IsUsingToothbrush;
        bool coffeeActive = characterEyes != null && characterEyes.IsUsingCoffee;
        bool faceUseActive = mouthwashActive || toothbrushActive || coffeeActive;
        if (grabRoutine != null || (characterEyes != null && (characterEyes.IsUsingDeodorant || faceUseActive)))
        {
            Cursor.visible = false;
            if (faceUseActive)
                ConfineCursorToLeftPanel(pointer);
            else
                faceUseCursorConfined = false;
            return;
        }
        faceUseCursorConfined = false;
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
                        sequenceComplete = true;
                        BeginDialogue(new[] { "that's as good as it's gonna get..." });
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

        Vector3 eyesCenter = (leftCameraStart + rightCameraStart) * 0.5f;
        Vector3 wantedLeftCamera = eyesCenter + Vector3.left * (eyeSeparation * 0.5f);
        Vector3 wantedRightCamera = eyesCenter + Vector3.right * (eyeSeparation * 0.5f);

        // The cameras always follow the eyes; focusing on an item doesn't turn them towards it.
        Quaternion wantedLeftRotation = leftCameraStartRotation * Quaternion.Euler(
            -leftAim.y * cameraAimRotationDegrees,
            leftAim.x * cameraAimRotationDegrees,
            0f);
        Quaternion wantedRightRotation = rightCameraStartRotation * Quaternion.Euler(
            -rightAim.y * cameraAimRotationDegrees,
            rightAim.x * cameraAimRotationDegrees,
            0f);

        ApplyGazeSnap(wantedLeftCamera, wantedRightCamera, ref wantedLeftRotation, ref wantedRightRotation);

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

    // If the two eyes' free gaze points are close enough, turn the eye that moved last onto the other eye's point.
    // The free points keep following the aims, so dragging on past the release distance breaks the snap.
    private void ApplyGazeSnap(Vector3 leftPosition, Vector3 rightPosition,
        ref Quaternion leftRotation, ref Quaternion rightRotation)
    {
        Vector2 leftMoved = leftAim - previousLeftAim;
        Vector2 rightMoved = rightAim - previousRightAim;
        if (leftMoved.sqrMagnitude > 0.000001f || rightMoved.sqrMagnitude > 0.000001f)
            rightEyeMovedLast = rightMoved.sqrMagnitude >= leftMoved.sqrMagnitude;
        previousLeftAim = leftAim;
        previousRightAim = rightAim;

        Vector3 leftPoint = FindGazePoint(new Ray(leftPosition, leftRotation * Vector3.forward), leftCamera);
        Vector3 rightPoint = FindGazePoint(new Ray(rightPosition, rightRotation * Vector3.forward), rightCamera);
        Vector3 eyesMiddle = (leftPosition + rightPosition) * 0.5f;
        float limit = gazeSnapped ? Mathf.Max(gazeSnapAngle, gazeSnapReleaseAngle) : gazeSnapAngle;
        gazeSnapped = Vector3.Angle(leftPoint - eyesMiddle, rightPoint - eyesMiddle) <= limit;

        // Visible in the Scene view while playing: red = left eye, cyan = right eye, green when snapped.
        Debug.DrawLine(leftPosition, leftPoint, gazeSnapped ? Color.green : Color.red);
        Debug.DrawLine(rightPosition, rightPoint, gazeSnapped ? Color.green : Color.cyan);
        if (!gazeSnapped) return;

        // Converge on the nearer point: it's the object being looked at, not the wall behind it.
        // On a tie, the eye that moved last is the one that turns.
        float leftDepth = Vector3.Distance(leftPosition, leftPoint);
        float rightDepth = Vector3.Distance(rightPosition, rightPoint);
        bool useLeftPoint = Mathf.Abs(leftDepth - rightDepth) > 0.01f ? leftDepth < rightDepth : rightEyeMovedLast;

        if (useLeftPoint)
            rightRotation = Quaternion.LookRotation(leftPoint - rightPosition, Vector3.up);
        else
            leftRotation = Quaternion.LookRotation(rightPoint - leftPosition, Vector3.up);
    }

    // Where an eye ray first meets anything the eye camera can see (sprites and meshes), or the far clip distance.
    private Vector3 FindGazePoint(Ray ray, Camera eyeCamera)
    {
        if (gazeRenderers == null) gazeRenderers = FindObjectsByType<Renderer>();
        float nearestDistance = eyeCamera.farClipPlane;
        foreach (Renderer candidate in gazeRenderers)
        {
            if (candidate == null || !candidate.enabled || !candidate.gameObject.activeInHierarchy ||
                candidate is SpriteMask || (eyeCamera.cullingMask & (1 << candidate.gameObject.layer)) == 0)
                continue;

            if (RayHitsRenderer(ray, candidate, out float distance) &&
                distance >= eyeCamera.nearClipPlane && distance < nearestDistance)
                nearestDistance = distance;
        }
        return ray.GetPoint(nearestDistance);
    }

    // Ray test against the renderer's actual sprite or mesh triangles, so tilted planes aren't hit early by
    // their bounding box. Falls back to the bounding box for anything without readable geometry.
    private bool RayHitsRenderer(Ray ray, Renderer renderer, out float distance)
    {
        distance = 0f;
        if (!renderer.bounds.IntersectRay(ray, out float boxDistance)) return false;
        if (!TryGetRayShape(renderer, out Vector3[] vertices, out int[] triangles, out Vector2[] uvs))
        {
            distance = boxDistance;
            return true;
        }

        // In local space the ray keeps its world-space length, so hit distances stay in world units.
        Matrix4x4 toLocal = renderer.transform.worldToLocalMatrix;
        Vector3 origin = toLocal.MultiplyPoint3x4(ray.origin);
        Vector3 direction = toLocal.MultiplyVector(ray.direction);
        if (renderer is SpriteRenderer sprite)
        {
            if (sprite.flipX) { origin.x = -origin.x; direction.x = -direction.x; }
            if (sprite.flipY) { origin.y = -origin.y; direction.y = -direction.y; }
        }

        Texture2D alphaTexture = GetReadableAlphaTexture(renderer, out Vector2 uvScale, out Vector2 uvOffset);
        if (uvs == null || uvs.Length != vertices.Length) alphaTexture = null;

        float nearest = float.PositiveInfinity;
        for (int i = 0; i + 2 < triangles.Length; i += 3)
        {
            int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
            if (!RayHitsTriangle(origin, direction, vertices[a], vertices[b], vertices[c],
                    out float hit, out float u, out float v) || hit >= nearest)
                continue;

            // See-through pixels don't stop the ray.
            if (alphaTexture != null)
            {
                Vector2 uv = uvs[a] * (1f - u - v) + uvs[b] * u + uvs[c] * v;
                uv = Vector2.Scale(uv, uvScale) + uvOffset;
                if (alphaTexture.GetPixelBilinear(uv.x, uv.y).a < RayAlphaCutoff) continue;
            }
            nearest = hit;
        }
        if (float.IsPositiveInfinity(nearest)) return false;
        distance = nearest;
        return true;
    }

    private const float RayAlphaCutoff = 0.1f;
    private MaterialPropertyBlock rayBlock;

    // The texture a ray should alpha-test against, if Unity can read its pixels (Read/Write enabled).
    private Texture2D GetReadableAlphaTexture(Renderer renderer, out Vector2 uvScale, out Vector2 uvOffset)
    {
        uvScale = Vector2.one;
        uvOffset = Vector2.zero;
        Texture texture = null;
        if (renderer is SpriteRenderer sprite)
        {
            if (sprite.sprite != null) texture = sprite.sprite.texture;
        }
        else if (renderer.sharedMaterials.Length == 1 && renderer.sharedMaterial != null)
        {
            Material material = renderer.sharedMaterial;
            if (!material.HasProperty("_MainTex")) return null;
            if (rayBlock == null) rayBlock = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(rayBlock);
            texture = rayBlock.GetTexture("_MainTex");
            if (texture == null) texture = material.mainTexture;
            uvScale = material.mainTextureScale;
            uvOffset = material.mainTextureOffset;
        }
        return texture is Texture2D readable && readable.isReadable ? readable : null;
    }

    private bool TryGetRayShape(Renderer renderer, out Vector3[] vertices, out int[] triangles, out Vector2[] uvs)
    {
        vertices = null;
        triangles = null;
        uvs = null;
        Object key = null;
        if (renderer is SpriteRenderer sprite)
        {
            if (sprite.sprite == null || sprite.drawMode != SpriteDrawMode.Simple) return false;
            key = sprite.sprite;
        }
        else if (renderer is MeshRenderer && renderer.GetComponent<TextMesh>() == null)
        {
            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null || !filter.sharedMesh.isReadable) return false;
            key = filter.sharedMesh;
        }
        if (key == null) return false;

        if (!rayShapes.TryGetValue(key, out RayShape shape))
        {
            shape = new RayShape();
            if (key is Sprite spriteAsset)
            {
                Vector2[] points = spriteAsset.vertices;
                shape.Vertices = new Vector3[points.Length];
                for (int i = 0; i < points.Length; i++) shape.Vertices[i] = points[i];
                ushort[] indices = spriteAsset.triangles;
                shape.Triangles = new int[indices.Length];
                for (int i = 0; i < indices.Length; i++) shape.Triangles[i] = indices[i];
                shape.Uvs = spriteAsset.uv;
            }
            else
            {
                Mesh mesh = (Mesh)key;
                shape.Vertices = mesh.vertices;
                shape.Triangles = mesh.triangles;
                shape.Uvs = mesh.uv;
            }
            rayShapes[key] = shape;
        }
        vertices = shape.Vertices;
        triangles = shape.Triangles;
        uvs = shape.Uvs;
        return true;
    }

    private sealed class RayShape
    {
        public Vector3[] Vertices;
        public int[] Triangles;
        public Vector2[] Uvs;
    }

    private readonly Dictionary<Object, RayShape> rayShapes = new Dictionary<Object, RayShape>();

    // Möller–Trumbore, hitting either side of the triangle.
    private static bool RayHitsTriangle(Vector3 origin, Vector3 direction, Vector3 a, Vector3 b, Vector3 c,
        out float distance, out float u, out float v)
    {
        distance = 0f;
        u = 0f;
        v = 0f;
        Vector3 edge1 = b - a;
        Vector3 edge2 = c - a;
        Vector3 p = Vector3.Cross(direction, edge2);
        float determinant = Vector3.Dot(edge1, p);
        if (Mathf.Abs(determinant) < 1e-8f) return false;
        float inverse = 1f / determinant;
        Vector3 s = origin - a;
        u = Vector3.Dot(s, p) * inverse;
        if (u < 0f || u > 1f) return false;
        Vector3 q = Vector3.Cross(s, edge1);
        v = Vector3.Dot(direction, q) * inverse;
        if (v < 0f || u + v > 1f) return false;
        distance = Vector3.Dot(edge2, q) * inverse;
        return distance > 0f;
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
        else if (target == mouthwashTarget && characterEyes != null)
            characterEyes.BeginMouthwashUse();
        else if (target == toothbrushTarget && characterEyes != null)
            characterEyes.BeginToothbrushUse();
        else if (!sequenceEnabled && target == focusTarget &&
            SceneManager.GetActiveScene().name == "Cafe" && characterEyes != null)
            characterEyes.BeginCoffeeUse();
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
        if (target == null || collectedItems.Contains(target)) return false;
        if (!sequenceEnabled) return target == focusTarget;
        return target == deodorantTarget || target == mouthwashTarget || target == toothbrushTarget;
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
            if (!sequenceEnabled)
                return lockedFocusTarget != null ? lockedFocusTarget
                    : alignedFocusTarget != null ? alignedFocusTarget : focusTarget;
            if (lockedFocusTarget != null) return lockedFocusTarget;
            if (alignedFocusTarget != null) return alignedFocusTarget;
            return AreAllItemsCollected() ? mirrorTarget : null;
        }
    }

    private Transform FindAlignedFocusTarget()
    {
        if (!sequenceEnabled)
        {
            if (IsAligned(focusTarget)) return focusTarget;
            foreach (Transform target in focusTargets)
                if (IsAligned(target)) return target;
            return null;
        }

        if (AreAllItemsCollected())
            return mirrorTarget != null && CenterRayHitsTarget(leftCamera, mirrorTarget) &&
                CenterRayHitsTarget(rightCamera, mirrorTarget) ? mirrorTarget : null;

        if (IsAvailableAndAligned(deodorantTarget)) return deodorantTarget;
        if (IsAvailableAndAligned(mouthwashTarget)) return mouthwashTarget;
        if (IsAvailableAndAligned(toothbrushTarget)) return toothbrushTarget;

        return null;
    }

    private bool IsAligned(Transform target)
    {
        return target != null && target.gameObject.activeInHierarchy &&
            CenterRayHitsTarget(leftCamera, target) && CenterRayHitsTarget(rightCamera, target);
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
        foreach (Transform target in focusTargets) EnsureFocusTargetCollider(target);
        EnsureFocusTargetCollider(deodorantTarget);
        EnsureFocusTargetCollider(mouthwashTarget);
        EnsureFocusTargetCollider(toothbrushTarget);
        EnsureFocusTargetCollider(mirrorTarget);
        focusRayRenderers = FindObjectsByType<Renderer>();
        Physics.SyncTransforms();
    }

    private void EnsureFocusTargetCollider(Transform target)
    {
        if (target == null) return;
        // Sprites or meshes; skip sprite masks, which have no visible area of their own.
        List<Renderer> renderers = new List<Renderer>();
        foreach (Renderer r in target.GetComponentsInChildren<Renderer>())
            if (!(r is SpriteMask)) renderers.Add(r);
        if (renderers.Count == 0) return;

        Bounds worldBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Count; i++)
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
        public Renderer Renderer;
        public Vector3 Point;
        public float Distance;
    }

    private CenterRayHit FindCenterRayHit(Camera eyeCamera)
    {
        return FindRayHit(eyeCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f)), eyeCamera);
    }

    // Nearest sprite or mesh along the ray, or the point at the camera's far clip distance if nothing is hit.
    private CenterRayHit FindRayHit(Ray ray, Camera eyeCamera)
    {
        float nearestDistance = eyeCamera.farClipPlane;
        Renderer nearestRenderer = null;

        foreach (Renderer candidate in focusRayRenderers)
        {
            if (candidate == null || !candidate.enabled || !candidate.gameObject.activeInHierarchy ||
                candidate is SpriteMask || (eyeCamera.cullingMask & (1 << candidate.gameObject.layer)) == 0)
                continue;

            if (RayHitsRenderer(ray, candidate, out float distance) &&
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

    private static bool RendererBelongsToTarget(Renderer renderer, Transform target)
    {
        return renderer != null && target != null && (renderer.transform == target || renderer.transform.IsChildOf(target));
    }

    private bool CenterRayHitsTarget(Camera eyeCamera, Transform target)
    {
        if (eyeCamera == null || target == null) return false;
        Ray ray = eyeCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        RaycastHit[] hits = Physics.SphereCastAll(
            ray, focusSphereCastRadius, eyeCamera.farClipPlane, eyeCamera.cullingMask, QueryTriggerInteraction.Collide);

        float targetDistance = float.PositiveInfinity;
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.transform == target || hit.collider.transform.IsChildOf(target))
                targetDistance = Mathf.Min(targetDistance, hit.distance);
        }
        if (float.IsPositiveInfinity(targetDistance)) return false;

        // Blocked only by something visibly solid in front: see-through parts of planes and their colliders don't count.
        CenterRayHit centerHit = FindRayHit(ray, eyeCamera);
        return centerHit.Renderer == null || RendererBelongsToTarget(centerHit.Renderer, target) ||
            centerHit.Distance >= targetDistance;
    }

    private void UpdateEyeFocusFeedback()
    {
        Physics.SyncTransforms();
        // Items only pop (and so can be locked and used) once the eyes have snapped together on them.
        Transform target = focused && lockedFocusTarget != null
            ? lockedFocusTarget
            : gazeSnapped ? FindAlignedFocusTarget() : null;

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
        Renderer[] focusRenderers = bothRaysHitTarget
            ? target.GetComponentsInChildren<Renderer>()
            : rayHit.Renderer == null || rayHitsActiveTarget
                ? new Renderer[0]
                : new[] { rayHit.Renderer };

        foreach (Renderer focusRenderer in focusRenderers)
        {
            if (focusRenderer == null || focusRenderer is SpriteMask) continue;
            Bounds bounds = focusRenderer.bounds;
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





