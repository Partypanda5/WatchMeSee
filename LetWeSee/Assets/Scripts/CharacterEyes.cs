using UnityEngine;
using UnityEngine.InputSystem;

// Sprite-based eye controls for the Character prefab. Click an eye and drag to move its pupil.
// Intended to replace the UI-based EyeGazeController once the world view is hooked up.
public sealed class CharacterEyes : MonoBehaviour
{
    [System.Serializable]
    public sealed class Eye
    {
        public Transform root;
        public SpriteRenderer eyeBall;
        public Transform pupil;
        public Transform topLid;
        public Transform bottomLid;

        [System.NonSerialized] public Vector2 center;
        [System.NonSerialized] public Vector2 radius;
        [System.NonSerialized] public Vector2 pupilExtents;
        [System.NonSerialized] public Vector2 pupilRest;
        [System.NonSerialized] public Vector2 pupilTarget;
        [System.NonSerialized] public Vector2 pupilCurrent;
        [System.NonSerialized] public Vector2 topLidRest;
        [System.NonSerialized] public Vector2 bottomLidRest;
        [System.NonSerialized] public float topEdgeRest;
        [System.NonSerialized] public float bottomEdgeRest;
        [System.NonSerialized] public float topOffset;
        [System.NonSerialized] public float bottomOffset;
    }

    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private Camera viewCamera;
    [SerializeField] private Eye leftEye = new Eye();
    [SerializeField] private Eye rightEye = new Eye();

    [Header("Pupil")]
    [SerializeField, Range(0.1f, 3f)] private float dragSensitivity = 1f;
    [SerializeField, Range(1f, 30f)] private float pupilFollowSpeed = 14f;

    [Header("Eyelids")]
    [Tooltip("Space kept between each lid edge and the pupil.")]
    [SerializeField, Range(0f, 0.5f)] private float lidGap = 0.04f;
    [Tooltip("The gap used when the pupil is at the very top or bottom of the eye, so it tucks in closer to the lid.")]
    [SerializeField, Range(-0.2f, 0.5f)] private float lidGapAtEdge = 0f;
    [Tooltip("How much the lids drift towards the pupil when they don't have to.")]
    [SerializeField, Range(0f, 1f)] private float lidFollow = 0.6f;
    [SerializeField, Range(1f, 30f)] private float lidFollowSpeed = 10f;
    [Tooltip("How far above its authored pose the top lid drifts on its own. It only opens further when the pupil pushes it.")]
    [SerializeField, Range(0f, 1f)] private float topLidMaxRaise = 0.08f;
    [SerializeField, Range(0f, 2f)] private float topLidMaxLower = 0.25f;
    [Tooltip("How far above its authored pose the bottom lid can rise.")]
    [SerializeField, Range(0f, 1f)] private float bottomLidMaxRaise = 0.06f;
    [SerializeField, Range(0f, 2f)] private float bottomLidMaxLower = 0.15f;

    [Header("Face features")]
    [SerializeField] private Transform leftBrow;
    [SerializeField] private Transform rightBrow;
    [Tooltip("Parts that slide sideways together with the average gaze: ears, nose, forehead, mouth.")]
    [SerializeField] private Transform[] turningParts = new Transform[0];
    [Tooltip("How far each brow moves up or down when its eye looks fully up or down.")]
    [SerializeField, Range(0f, 1f)] private float browTravel = 0.12f;
    [Tooltip("How far the turning parts slide sideways when the eyes, on average, look fully left or right. Negative flips the direction.")]
    [SerializeField, Range(-1f, 1f)] private float turnTravel = 0.07f;
    [SerializeField, Range(1f, 30f)] private float featureFollowSpeed = 8f;

    [Header("Hand")]
    [Tooltip("Follows the pointer by its pivot (the fingertip). While dragging an eye it only moves as far as the pupil does.")]
    [SerializeField] private SpriteRenderer hand;
    [SerializeField] private Sprite pointingHand;
    [SerializeField] private Sprite pressingHand;
    [SerializeField] private AudioClip faceTapSound;
    [SerializeField] private AudioClip eyeDragSquishSound;

    private InputAction pointerPosition;
    private InputAction eyeDrag;
    private Eye dragged;
    private AudioSource faceTapAudioSource;
    private AudioSource eyeDragAudioSource;
    private Vector2 lastDragScreenPosition;
    private float accumulatedDragScreenMovement;
    private float lastEyeMovementTime;
    private float dragSquishBasePitch = 1f;
    private float dragSquishPitchPhase;
    private bool faceTapPending;
    private Vector2 dragStartPointer;
    private Vector2 dragStartPupil;
    private Vector3 leftBrowRest;
    private Vector3 rightBrowRest;
    private Vector3[] turningRest;
    private float leftBrowOffset;
    private float rightBrowOffset;
    private float turnOffset;

    private bool initialized;

    // Fired when the player grabs an eye, before the drag starts moving it.
    public event System.Action DragStarted;

    // -1..1 aim of each pupil within its reachable range: where the pupil is now...
    public Vector2 LeftAim => Aim(leftEye, leftEye.pupilCurrent);
    public Vector2 RightAim => Aim(rightEye, rightEye.pupilCurrent);
    // ...and where it is heading.
    public Vector2 LeftAimTarget => Aim(leftEye, leftEye.pupilTarget);
    public Vector2 RightAimTarget => Aim(rightEye, rightEye.pupilTarget);
    public Sprite PointingHandSprite => pointingHand;

    // Sends both pupils towards a -1..1 aim. The pupils ease there like a drag would.
    public void SetAims(Vector2 left, Vector2 right)
    {
        EnsureInitialized();
        SetAim(leftEye, left);
        SetAim(rightEye, right);
    }

    private void SetAim(Eye eye, Vector2 aim)
    {
        if (eye.root == null || eye.pupil == null) return;
        Vector2 room = Vector2.Max(eye.radius - eye.pupilExtents, Vector2.one * 0.001f);
        eye.pupilTarget = ClampPupil(eye, eye.center + new Vector2(aim.x * room.x, aim.y * room.y));
    }

    private void Awake()
    {
        EnsureInitialized();

        if (inputActions == null) return;
        InputActionMap map = inputActions.FindActionMap("EyeControls", true);
        pointerPosition = map.FindAction("PointerPosition", true);
        eyeDrag = map.FindAction("EyeDrag", true);
    }

    private void EnsureInitialized()
    {
        if (initialized) return;
        initialized = true;
        Init(leftEye);
        Init(rightEye);
        if (leftBrow != null) leftBrowRest = leftBrow.localPosition;
        if (rightBrow != null) rightBrowRest = rightBrow.localPosition;
        turningRest = new Vector3[turningParts.Length];
        for (int i = 0; i < turningParts.Length; i++)
            if (turningParts[i] != null) turningRest[i] = turningParts[i].localPosition;
    }

    private void OnEnable()
    {
        if (pointerPosition != null) pointerPosition.actionMap.Enable();
    }

    private void OnDisable()
    {
        dragged = null;
        if (eyeDragAudioSource != null)
        {
            eyeDragAudioSource.Stop();
            eyeDragAudioSource.loop = false;
            eyeDragAudioSource.pitch = 1f;
        }
    }

    private void Init(Eye eye)
    {
        if (eye.root == null || eye.eyeBall == null || eye.pupil == null) return;

        Bounds ball = eye.eyeBall.bounds;
        eye.center = ToEye(eye, ball.center);
        eye.radius = ToEyeSize(eye, ball.extents);

        SpriteRenderer pupilRenderer = eye.pupil.GetComponent<SpriteRenderer>();
        eye.pupilExtents = pupilRenderer != null ? ToEyeSize(eye, pupilRenderer.bounds.extents) : Vector2.zero;
        eye.pupilRest = ToEye(eye, eye.pupil.position);
        eye.pupilTarget = eye.pupilRest;
        eye.pupilCurrent = eye.pupilRest;

        if (eye.topLid != null)
        {
            eye.topLidRest = ToEye(eye, eye.topLid.position);
            eye.topEdgeRest = LidEdge(eye, eye.topLid, true);
        }
        if (eye.bottomLid != null)
        {
            eye.bottomLidRest = ToEye(eye, eye.bottomLid.position);
            eye.bottomEdgeRest = LidEdge(eye, eye.bottomLid, false);
        }
    }

    // Lid edge height in eye space: the bottom of the top lid's masks, or the top of the bottom lid's.
    private float LidEdge(Eye eye, Transform lid, bool top)
    {
        float edge = top ? float.MaxValue : float.MinValue;
        bool found = false;
        foreach (SpriteMask mask in lid.GetComponentsInChildren<SpriteMask>(true))
        {
            Bounds b = mask.bounds;
            float y = ToEye(eye, top ? b.min : b.max).y;
            edge = top ? Mathf.Min(edge, y) : Mathf.Max(edge, y);
            found = true;
        }
        if (!found)
        {
            foreach (SpriteRenderer sprite in lid.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (!sprite.enabled || sprite.maskInteraction != SpriteMaskInteraction.None) continue;
                Bounds b = sprite.bounds;
                float y = ToEye(eye, top ? b.min : b.max).y;
                edge = top ? Mathf.Min(edge, y) : Mathf.Max(edge, y);
                found = true;
            }
        }
        if (found) return edge;
        return top ? eye.center.y + eye.radius.y : eye.center.y - eye.radius.y;
    }

    private void Update()
    {
        if (pointerPosition != null && eyeDrag != null)
            HandleInput();

        UpdateDragSquishAudio();
        UpdateEye(leftEye);
        UpdateEye(rightEye);
        UpdateFaceFeatures();
    }

    // Each brow follows its own eye up and down; the turning parts slide sideways together with the average gaze.
    private void UpdateFaceFeatures()
    {
        float step = Time.deltaTime * featureFollowSpeed;
        Vector2 left = LeftAim;
        Vector2 right = RightAim;

        leftBrowOffset = Mathf.Lerp(leftBrowOffset, left.y * browTravel, step);
        rightBrowOffset = Mathf.Lerp(rightBrowOffset, right.y * browTravel, step);
        turnOffset = Mathf.Lerp(turnOffset, (left.x + right.x) * 0.5f * turnTravel, step);

        if (leftBrow != null) leftBrow.localPosition = leftBrowRest + Vector3.up * leftBrowOffset;
        if (rightBrow != null) rightBrow.localPosition = rightBrowRest + Vector3.up * rightBrowOffset;
        for (int i = 0; i < turningParts.Length; i++)
            if (turningParts[i] != null) turningParts[i].localPosition = turningRest[i] + Vector3.right * turnOffset;
    }

    private void HandleInput()
    {
        Camera cam = viewCamera != null ? viewCamera : Camera.main;
        if (cam == null) return;

        Vector2 screen = pointerPosition.ReadValue<Vector2>();
        Vector3 world = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -cam.transform.position.z));

        if (eyeDrag.WasPressedThisFrame())
        {
            dragged = null;
            faceTapPending = false;
            if (IsOverEye(leftEye, world)) dragged = leftEye;
            else if (IsOverEye(rightEye, world)) dragged = rightEye;

            if (dragged != null)
            {
                DragStarted?.Invoke();
                dragStartPointer = ToEye(dragged, world);
                dragStartPupil = dragged.pupilTarget;
                lastDragScreenPosition = screen;
                accumulatedDragScreenMovement = 0f;
            }
            else if (cam.pixelRect.Contains(screen))
            {
                faceTapPending = true;
            }
        }

        if (!eyeDrag.IsPressed())
        {
            dragged = null;
            faceTapPending = false;
        }

        if (dragged != null)
        {
            Vector2 screenMovement = screen - lastDragScreenPosition;
            lastDragScreenPosition = screen;
            Vector2 delta = ToEye(dragged, world) - dragStartPointer;
            Vector2 previousPupilTarget = dragged.pupilTarget;
            dragged.pupilTarget = ClampPupil(dragged, dragStartPupil + delta * dragSensitivity);

            bool pupilMoved = (dragged.pupilTarget - previousPupilTarget).sqrMagnitude > 0.000001f;
            if (pupilMoved)
            {
                accumulatedDragScreenMovement += screenMovement.magnitude;
                lastEyeMovementTime = Time.unscaledTime;
                if (accumulatedDragScreenMovement >= 6f)
                {
                    StartDragSquishLoop();
                    accumulatedDragScreenMovement = 0f;
                }
            }
        }

        UpdateHand(cam, screen, world);
    }

    private void PlayFaceTap()
    {
        if (faceTapSound == null) return;
        if (faceTapAudioSource == null)
        {
            faceTapAudioSource = GetComponent<AudioSource>();
            if (faceTapAudioSource == null) faceTapAudioSource = gameObject.AddComponent<AudioSource>();
            faceTapAudioSource.playOnAwake = false;
            faceTapAudioSource.spatialBlend = 0f;
        }

        faceTapAudioSource.PlayOneShot(faceTapSound);
    }

    private void StartDragSquishLoop()
    {
        if (eyeDragSquishSound == null) return;

        if (eyeDragAudioSource == null)
        {
            eyeDragAudioSource = gameObject.AddComponent<AudioSource>();
            eyeDragAudioSource.playOnAwake = false;
            eyeDragAudioSource.spatialBlend = 0f;
        }

        // Use one looping source so movement never stacks overlapping one-shot voices.
        if (eyeDragAudioSource.isPlaying && eyeDragAudioSource.loop && eyeDragAudioSource.clip == eyeDragSquishSound)
            return;

        eyeDragAudioSource.Stop();
        eyeDragAudioSource.clip = eyeDragSquishSound;
        eyeDragAudioSource.loop = true;
        dragSquishBasePitch = Random.Range(0.92f, 1.08f);
        dragSquishPitchPhase = Random.Range(0f, Mathf.PI * 2f);
        eyeDragAudioSource.pitch = dragSquishBasePitch;
        eyeDragAudioSource.Play();
    }

    private void UpdateDragSquishAudio()
    {
        if (eyeDragAudioSource == null) return;

        bool movingEye = eyeDrag != null && eyeDrag.IsPressed() && dragged != null &&
                         Time.unscaledTime - lastEyeMovementTime <= 0.3f;
        if (!movingEye)
        {
            if (eyeDragAudioSource.isPlaying) eyeDragAudioSource.Stop();
            eyeDragAudioSource.loop = false;
            eyeDragAudioSource.pitch = 1f;
            return;
        }

        if (eyeDragAudioSource.isPlaying)
        {
            eyeDragAudioSource.pitch = dragSquishBasePitch +
                Mathf.Sin(Time.unscaledTime * 10f + dragSquishPitchPhase) * 0.035f;
        }
    }
    private void UpdateHand(Camera cam, Vector2 screen, Vector3 world)
    {
        if (hand == null) return;

        // During an eye drag, keep the fingertip on the selected pupil even if the pointer leaves the face panel.
        bool draggingEye = eyeDrag.IsPressed() && dragged != null;
        bool visible = draggingEye || cam.pixelRect.Contains(screen);
        hand.enabled = visible;
        if (!visible) return;

        Vector3 tip = draggingEye
            ? ToWorld(dragged, dragged.pupilCurrent, world.z)
            : world;
        hand.transform.position = new Vector3(tip.x, tip.y, hand.transform.position.z);
        hand.transform.localScale = Vector3.one * 0.88f;
        hand.transform.localRotation = Quaternion.Euler(0f, 0f, -8f);

        Sprite wanted = eyeDrag.IsPressed() ? pressingHand : pointingHand;
        if (wanted != null) hand.sprite = wanted;
        if (faceTapPending && !draggingEye && pressingHand != null && wanted == pressingHand)
        {
            PlayFaceTap();
            faceTapPending = false;
        }
    }

    private bool IsOverEye(Eye eye, Vector3 world)
    {
        if (eye.root == null || eye.eyeBall == null) return false;
        Vector2 local = ToEye(eye, world) - eye.center;
        Vector2 r = Vector2.Max(eye.radius, Vector2.one * 0.001f);
        return (local.x * local.x) / (r.x * r.x) + (local.y * local.y) / (r.y * r.y) <= 1f;
    }

    private void UpdateEye(Eye eye)
    {
        if (eye.root == null || eye.pupil == null) return;

        eye.pupilTarget = ClampPupil(eye, eye.pupilTarget);
        eye.pupilCurrent = Vector2.Lerp(eye.pupilCurrent, eye.pupilTarget, Time.deltaTime * pupilFollowSpeed);
        eye.pupilCurrent = ClampPupil(eye, eye.pupilCurrent);
        eye.pupil.position = ToWorld(eye, eye.pupilCurrent, eye.pupil.position.z);

        float pupilTop = eye.pupilCurrent.y + eye.pupilExtents.y;
        float pupilBottom = eye.pupilCurrent.y - eye.pupilExtents.y;
        float lidStep = Time.deltaTime * lidFollowSpeed;
        float aimY = Aim(eye, eye.pupilCurrent).y;

        if (eye.topLid != null)
        {
            // Lazily drift towards the pupil within the lid's range, but always open far enough to clear it.
            float clearEdge = pupilTop + Mathf.Lerp(lidGap, lidGapAtEdge, Mathf.Max(0f, aimY));
            float edge = Mathf.Clamp(Mathf.Lerp(eye.topEdgeRest, clearEdge, lidFollow),
                eye.topEdgeRest - topLidMaxLower, eye.topEdgeRest + topLidMaxRaise);
            edge = Mathf.Max(edge, clearEdge);
            float wanted = edge - eye.topEdgeRest;
            // Opening snaps so the pupil is never covered; closing eases.
            eye.topOffset = wanted > eye.topOffset ? wanted : Mathf.Lerp(eye.topOffset, wanted, lidStep);
            eye.topLid.position = ToWorld(eye, eye.topLidRest + Vector2.up * eye.topOffset, eye.topLid.position.z);
        }

        if (eye.bottomLid != null)
        {
            float clearEdge = pupilBottom - Mathf.Lerp(lidGap, lidGapAtEdge, Mathf.Max(0f, -aimY));
            float edge = Mathf.Clamp(Mathf.Lerp(eye.bottomEdgeRest, clearEdge, lidFollow),
                eye.bottomEdgeRest - bottomLidMaxLower, eye.bottomEdgeRest + bottomLidMaxRaise);
            edge = Mathf.Min(edge, clearEdge);
            float wanted = edge - eye.bottomEdgeRest;
            eye.bottomOffset = wanted < eye.bottomOffset ? wanted : Mathf.Lerp(eye.bottomOffset, wanted, lidStep);
            eye.bottomLid.position = ToWorld(eye, eye.bottomLidRest + Vector2.up * eye.bottomOffset, eye.bottomLid.position.z);
        }
    }

    // Keeps the pupil inside the eyeball. The lids move out of its way rather than limiting it.
    private Vector2 ClampPupil(Eye eye, Vector2 position)
    {
        Vector2 room = Vector2.Max(eye.radius - eye.pupilExtents, Vector2.one * 0.001f);
        Vector2 offset = position - eye.center;
        Vector2 normalized = new Vector2(offset.x / room.x, offset.y / room.y);
        if (normalized.sqrMagnitude > 1f)
        {
            normalized.Normalize();
            offset = new Vector2(normalized.x * room.x, normalized.y * room.y);
        }
        return eye.center + offset;
    }

    private Vector2 Aim(Eye eye, Vector2 pupilPosition)
    {
        Vector2 room = Vector2.Max(eye.radius - eye.pupilExtents, Vector2.one * 0.001f);
        Vector2 offset = pupilPosition - eye.center;
        return new Vector2(Mathf.Clamp(offset.x / room.x, -1f, 1f), Mathf.Clamp(offset.y / room.y, -1f, 1f));
    }

    private static Vector2 ToEye(Eye eye, Vector3 world) => eye.root.InverseTransformPoint(world);

    private static Vector2 ToEyeSize(Eye eye, Vector3 worldExtents)
    {
        Vector3 scale = eye.root.lossyScale;
        return new Vector2(Mathf.Abs(worldExtents.x / scale.x), Mathf.Abs(worldExtents.y / scale.y));
    }

    private static Vector3 ToWorld(Eye eye, Vector2 local, float z)
    {
        Vector3 world = eye.root.TransformPoint(local);
        world.z = z;
        return world;
    }
}
