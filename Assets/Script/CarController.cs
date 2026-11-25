using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

[RequireComponent(typeof(Rigidbody))]
public class CarController : MonoBehaviour
{
    private enum StartingLane
    {
        Left,
        Right
    }

    [Header("Two Lane Settings")]
    [SerializeField] private float roadCenterX = -0.14f;
    [SerializeField] private float laneOffset = 3f;
    [SerializeField] private StartingLane startingLane = StartingLane.Right;
    [SerializeField] private float laneChangeSpeed = 8f;
    [SerializeField] private float acceleration = 30f;
    [SerializeField] private float followStrength = 12f;

    [Header("Mouse Drag & Left Area Settings")]
    [SerializeField] private float swipeThresholdPixels = 60f;
    [Tooltip("Batas maksimal posisi horizontal mouse (0.5 = separuh kiri layar) untuk interaksi kemudi & menyalakan outline setir")]
    [Range(0.2f, 0.8f)]
    [SerializeField] private float steeringAreaMaxNormalizedX = 0.5f;
    [Tooltip("Jika aktif, mouse yang berada di atas UI layar HP / tombol game tidak akan memicu setir atau menyalakan outline")]
    [SerializeField] private bool blockSteeringOverUI = true;

    [Header("Steering Wheel Settings")]
    [SerializeField] private Transform steeringWheelPivot;
    [SerializeField] private float maxSteeringAngle = 120f;
    [SerializeField] private float steeringHoldDuration = 0.08f;
    [SerializeField] private float steeringTurnSmoothTime = 0.05f;
    [SerializeField] private float steeringReturnSmoothTime = 0.12f;
    [SerializeField] private Vector3 steeringAxis = Vector3.forward;
    [SerializeField] private bool invertSteering = false;

    [Header("Steering Wheel Outline (Menyala Saat Mouse di Area Kiri)")]
    [SerializeField] private bool enableSteeringOutline = true;
    [SerializeField] private Shader outlineShader;
    [ColorUsage(true, true)]
    [SerializeField] private Color outlineColor = new Color(0.25f, 1.40f, 1.85f, 1.0f);
    [Range(0.001f, 0.03f)]
    [SerializeField] private float outlineWidth = 0.009f;
    [SerializeField] private float outlineFadeSpeed = 14f;

    [Header("Lampu Mobil, Pantulan Kabin & Kabut Ujung Jalan")]
    [SerializeField] private bool configureLightingAndFogOnStart = true;
    [SerializeField] private Color headlightColor = new Color(1.0f, 0.93f, 0.80f, 1.0f);
    [SerializeField] private float headlightIntensity = 2500f;
    [SerializeField] private float headlightRange = 115f;
    [SerializeField] private float headlightPitchDown = 7.5f;
    [Tooltip("Pantulan cahaya lampu depan yang masuk lewat kaca depan menyinari dashboard & setir secara natural")]
    [SerializeField] private float cabinBounceIntensity = 1.15f;
    [SerializeField] private float cabinBounceRange = 2.2f;
    [Tooltip("Jarak mulai muncul kabut (0 - nilai ini jalanan bersih tanpa kabut)")]
    [SerializeField] private float linearFogStart = 42f;
    [Tooltip("Jarak kabut tertutup 100% (mobil NPC spawn di luar jarak ini agar muncul perlahan dari kabut)")]
    [SerializeField] private float linearFogEnd = 105f;

    private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
    private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");

    private Rigidbody rb;

    private bool isDragging;
    private float startMouseX;

    // 0 = Left, 1 = Right
    private int currentLane;
    private float targetX;

    private Quaternion initialWheelRotation;
    private float currentSteeringAngle;
    private float steeringTargetAngle;
    private float steeringAngularVelocity;
    private float steeringTimer;

    private Material outlineMaterialInstance;
    private readonly List<MeshRenderer> outlineRenderers = new List<MeshRenderer>();
    private readonly List<Mesh> generatedOutlineMeshes = new List<Mesh>();
    private readonly List<RaycastResult> uiRaycastResults = new List<RaycastResult>();
    private float currentOutlineWeight = 0f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.constraints |= RigidbodyConstraints.FreezeRotation;

        // Set starting lane
        currentLane = startingLane == StartingLane.Left ? 0 : 1;

        targetX = GetLanePosition(currentLane);

        // Place car directly in selected lane
        Vector3 position = rb.position;
        position.x = targetX;
        rb.position = position;

        // Save original steering wheel rotation
        if (steeringWheelPivot != null)
        {
            initialWheelRotation = steeringWheelPivot.localRotation;
            InitializeSteeringOutline();
        }

        SetupCarLightingAndHorizonFog();
        GameplayAudioManager.EnsureExists();
    }

    private void SetupCarLightingAndHorizonFog()
    {
        if (!configureLightingAndFogOnStart) return;

        // 1. Atur Linear Fog di ujung jalan agar area dekat mobil jernih dan ujung jalan tertutup kabut tebal
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = Mathf.Max(5f, linearFogStart);
        RenderSettings.fogEndDistance = Mathf.Max(RenderSettings.fogStartDistance + 15f, linearFogEnd);

        // 2. Rapikan posisi & sorotan kedua Lampu Mobil agar berada di depan bumper (tidak terhalang mesh kap mobil sendiri)
        Light[] childLights = GetComponentsInChildren<Light>(true);
        for (int i = 0; i < childLights.Length; i++)
        {
            Light l = childLights[i];
            if (l == null || !l.gameObject.name.Contains("Lampu mobil")) continue;

            Vector3 localPos = l.transform.localPosition;
            // Majukan ke depan moncong mobil (z = -1.18) jika masih terkubur di belakang kap (z > -0.8)
            if (localPos.z > -0.8f)
            {
                localPos.z = -1.18f;
                localPos.y = 0.18f;
                l.transform.localPosition = localPos;
            }

            l.transform.localRotation = Quaternion.Euler(headlightPitchDown, 180f, 0f);
            l.type = LightType.Spot;
            l.color = headlightColor;
            l.intensity = headlightIntensity;
            l.range = headlightRange;
            l.spotAngle = 68f;
            l.innerSpotAngle = 44f;
            l.shadows = LightShadows.None;
        }

        // 3. Tambahkan Cabin Headlight Bounce Light (pantulan lembut cahaya lampu mobil dari jalan/kap yang masuk lewat kaca depan ke kabin)
        Transform bounceTf = transform.Find("CabinHeadlightBounceLight");
        if (bounceTf == null)
        {
            GameObject bounceObj = new GameObject("CabinHeadlightBounceLight");
            bounceObj.transform.SetParent(transform, false);
            bounceTf = bounceObj.transform;
        }

        // Posisi tepat di area kaca depan menghadap masuk ke arah dashboard & setir (+Z lokal Car Root)
        bounceTf.localPosition = new Vector3(0f, 0.42f, -0.35f);
        bounceTf.localRotation = Quaternion.Euler(14f, 0f, 0f);

        Light bounceLight = bounceTf.GetComponent<Light>();
        if (bounceLight == null)
        {
            bounceLight = bounceTf.gameObject.AddComponent<Light>();
        }

        bounceLight.type = LightType.Spot;
        bounceLight.color = headlightColor;
        bounceLight.intensity = cabinBounceIntensity;
        bounceLight.range = cabinBounceRange;
        bounceLight.spotAngle = 108f;
        bounceLight.innerSpotAngle = 55f;
        bounceLight.shadows = LightShadows.None;
        bounceLight.enabled = true;
    }

    private void OnDisable()
    {
        isDragging = false;
        currentOutlineWeight = 0f;
        ApplyOutlineVisibility(false);
    }

    private void OnDestroy()
    {
        if (outlineMaterialInstance != null)
        {
            Destroy(outlineMaterialInstance);
            outlineMaterialInstance = null;
        }

        for (int i = 0; i < generatedOutlineMeshes.Count; i++)
        {
            if (generatedOutlineMeshes[i] != null)
            {
                Destroy(generatedOutlineMeshes[i]);
            }
        }
        generatedOutlineMeshes.Clear();
    }

    private void Update()
    {
        bool shouldHighlightOutline = false;

        if (Mouse.current != null && !IsGameplayInputLocked())
        {
            Vector2 mousePos = Mouse.current.position.ReadValue();
            bool inLeftZone = IsMouseInLeftSteeringArea(mousePos);

            // Start mouse drag hanya jika penekanan awal terjadi di area kiri & tidak di atas layar HP
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                if (inLeftZone)
                {
                    isDragging = true;
                    startMouseX = mousePos.x;
                }
                else
                {
                    isDragging = false;
                }
            }

            // Detect drag release hanya untuk tarikan yang dimulai dari area kiri
            if (isDragging && Mouse.current.leftButton.wasReleasedThisFrame)
            {
                float dragDistance = mousePos.x - startMouseX;

                if (Mathf.Abs(dragDistance) >= swipeThresholdPixels)
                {
                    if (dragDistance < 0f)
                    {
                        ChangeLane(0);
                    }
                    else
                    {
                        ChangeLane(1);
                    }
                }

                isDragging = false;
            }

            // Outline menyala saat kursor berada di area kiri (atau sedang aktif menarik setir),
            // dan tidak menyala saat pemain sedang menahan drag dari konten HP di kanan masuk ke kiri.
            bool holdingNonSteeringDrag = Mouse.current.leftButton.isPressed && !isDragging;
            shouldHighlightOutline = (inLeftZone && !holdingNonSteeringDrag) || isDragging;
        }
        else
        {
            isDragging = false;
        }

        UpdateSteeringWheel();
        UpdateSteeringOutlineVisual(shouldHighlightOutline);
    }

    private bool IsGameplayInputLocked()
    {
        return ConsciousnessManager.Instance != null && ConsciousnessManager.Instance.IsGameEnded;
    }

    private bool IsMouseInLeftSteeringArea(Vector2 mousePos)
    {
        if (Screen.width <= 0 || Screen.height <= 0) return false;
        if (mousePos.x < 0f || mousePos.y < 0f || mousePos.y > Screen.height) return false;

        float normalizedX = mousePos.x / Screen.width;
        if (normalizedX > steeringAreaMaxNormalizedX) return false;

        if (blockSteeringOverUI && IsPointerOverPhoneOrBlockingUI(mousePos))
            return false;

        return true;
    }

    private bool IsPointerOverPhoneOrBlockingUI(Vector2 mousePos)
    {
        if (EventSystem.current == null) return false;

        PointerEventData eventData = new PointerEventData(EventSystem.current)
        {
            position = mousePos
        };

        uiRaycastResults.Clear();
        EventSystem.current.RaycastAll(eventData, uiRaycastResults);

        for (int i = 0; i < uiRaycastResults.Count; i++)
        {
            GameObject hitObj = uiRaycastResults[i].gameObject;
            if (hitObj == null) continue;

            if (hitObj.GetComponentInParent<PhoneFeedScrollController>() != null)
                return true;

            if (hitObj.GetComponentInParent<UnityEngine.UI.Selectable>() != null)
                return true;
        }

        return false;
    }

    private void InitializeSteeringOutline()
    {
        outlineRenderers.Clear();

        if (!enableSteeringOutline || steeringWheelPivot == null)
            return;

        Shader shader = outlineShader != null
            ? outlineShader
            : Shader.Find("Gimersia/URP/SteeringOutline");

        if (shader == null)
        {
            Debug.LogWarning("[CarController] Shader 'Gimersia/URP/SteeringOutline' tidak ditemukan. Outline setir tidak aktif.");
            return;
        }

        outlineMaterialInstance = new Material(shader)
        {
            hideFlags = HideFlags.DontSave
        };

        MeshFilter[] meshFilters = steeringWheelPivot.GetComponentsInChildren<MeshFilter>(true);
        for (int i = 0; i < meshFilters.Length; i++)
        {
            MeshFilter sourceFilter = meshFilters[i];
            if (sourceFilter == null || sourceFilter.sharedMesh == null) continue;
            if (sourceFilter.gameObject.name.Contains("_OutlineShell")) continue;

            MeshRenderer sourceRenderer = sourceFilter.GetComponent<MeshRenderer>();
            if (sourceRenderer == null) continue;

            GameObject shellObj = new GameObject(sourceFilter.gameObject.name + "_OutlineShell");
            shellObj.transform.SetParent(sourceFilter.transform, false);
            shellObj.transform.localPosition = Vector3.zero;
            shellObj.transform.localRotation = Quaternion.identity;
            shellObj.transform.localScale = Vector3.one;
            shellObj.layer = sourceFilter.gameObject.layer;

            MeshFilter shellFilter = shellObj.AddComponent<MeshFilter>();
            shellFilter.sharedMesh = CreateSmoothedOutlineMesh(sourceFilter.sharedMesh);

            MeshRenderer shellRenderer = shellObj.AddComponent<MeshRenderer>();
            shellRenderer.sharedMaterial = outlineMaterialInstance;
            shellRenderer.shadowCastingMode = ShadowCastingMode.Off;
            shellRenderer.receiveShadows = false;
            shellRenderer.lightProbeUsage = LightProbeUsage.Off;
            shellRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            shellRenderer.enabled = false;

            outlineRenderers.Add(shellRenderer);
        }
    }

    private Mesh CreateSmoothedOutlineMesh(Mesh source)
    {
        if (!source.isReadable)
            return source;

        Mesh smoothed = new Mesh
        {
            name = source.name + "_OutlineSmoothed",
            indexFormat = source.indexFormat,
            hideFlags = HideFlags.DontSave
        };

        Vector3[] vertices = source.vertices;
        Vector3[] sourceNormals = source.normals;
        Vector3[] averagedNormals = new Vector3[vertices.Length];

        // Gabungkan normal di posisi vertex yang sama agar mesh low-poly tidak pecah saat diekstrusi
        Dictionary<Vector3Int, Vector3> normalSumByPos = new Dictionary<Vector3Int, Vector3>(vertices.Length);
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3Int key = QuantizePosition(vertices[i]);
            Vector3 n = (sourceNormals != null && i < sourceNormals.Length) ? sourceNormals[i] : Vector3.up;

            if (normalSumByPos.TryGetValue(key, out Vector3 sum))
                normalSumByPos[key] = sum + n;
            else
                normalSumByPos[key] = n;
        }

        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3Int key = QuantizePosition(vertices[i]);
            Vector3 sum = normalSumByPos[key];
            averagedNormals[i] = sum.sqrMagnitude > 1e-8f ? sum.normalized : Vector3.up;
        }

        smoothed.vertices = vertices;
        smoothed.normals = averagedNormals;
        smoothed.subMeshCount = source.subMeshCount;
        for (int s = 0; s < source.subMeshCount; s++)
        {
            smoothed.SetTriangles(source.GetTriangles(s), s, false);
        }
        smoothed.bounds = source.bounds;

        generatedOutlineMeshes.Add(smoothed);
        return smoothed;
    }

    private static Vector3Int QuantizePosition(Vector3 v)
    {
        return new Vector3Int(
            Mathf.RoundToInt(v.x * 10000f),
            Mathf.RoundToInt(v.y * 10000f),
            Mathf.RoundToInt(v.z * 10000f)
        );
    }

    private void UpdateSteeringOutlineVisual(bool shouldHighlight)
    {
        if (outlineMaterialInstance == null || outlineRenderers.Count == 0)
            return;

        float target = (enableSteeringOutline && shouldHighlight) ? 1f : 0f;
        currentOutlineWeight = Mathf.MoveTowards(currentOutlineWeight, target, Time.deltaTime * outlineFadeSpeed);

        bool visible = enableSteeringOutline && currentOutlineWeight > 0.01f;
        ApplyOutlineVisibility(visible);

        if (visible)
        {
            Color effectiveColor = outlineColor;
            effectiveColor.a *= currentOutlineWeight;

            float pulse = 1f + Mathf.Sin(Time.time * 4.5f) * 0.08f * currentOutlineWeight;
            outlineMaterialInstance.SetColor(OutlineColorId, effectiveColor);
            outlineMaterialInstance.SetFloat(OutlineWidthId, outlineWidth * Mathf.Lerp(0.65f, 1f, currentOutlineWeight) * pulse);
        }
    }

    private void ApplyOutlineVisibility(bool visible)
    {
        for (int i = 0; i < outlineRenderers.Count; i++)
        {
            if (outlineRenderers[i] != null && outlineRenderers[i].enabled != visible)
            {
                outlineRenderers[i].enabled = visible;
            }
        }
    }

    private float GetLanePosition(int lane)
    {
        if (lane == 0)
            return roadCenterX - laneOffset;

        return roadCenterX + laneOffset;
    }

    private void ChangeLane(int targetLane)
    {
        targetLane = Mathf.Clamp(targetLane, 0, 1);

        // Ignore input if already in the selected lane
        if (currentLane == targetLane)
            return;

        // Determine steering direction
        int steeringDirection = targetLane > currentLane ? 1 : -1;

        currentLane = targetLane;

        // Set target lane position
        targetX = GetLanePosition(currentLane);

        // Turn steering wheel immediately
        float direction = invertSteering ? -1f : 1f;

        steeringTargetAngle =
            maxSteeringAngle *
            steeringDirection *
            direction;

        // Start automatic steering return timer
        steeringTimer = steeringHoldDuration;

        GameplayAudioManager.PlayLaneChangeSwerve();
    }

    private void UpdateSteeringWheel()
    {
        if (steeringWheelPivot == null)
            return;

        // Automatically return steering wheel after turning
        if (steeringTimer > 0f)
        {
            steeringTimer -= Time.deltaTime;
        }
        else
        {
            steeringTargetAngle = 0f;
        }

        // Use different smoothing for turning and returning
        float smoothTime = Mathf.Approximately(steeringTargetAngle, 0f)
            ? steeringReturnSmoothTime
            : steeringTurnSmoothTime;

        // Smooth steering animation
        currentSteeringAngle = Mathf.SmoothDampAngle(
            currentSteeringAngle,
            steeringTargetAngle,
            ref steeringAngularVelocity,
            smoothTime,
            Mathf.Infinity,
            Time.deltaTime
        );

        Vector3 axis = steeringAxis.sqrMagnitude > 0.001f
            ? steeringAxis.normalized
            : Vector3.forward;

        // Apply rotation without changing scale
        steeringWheelPivot.localRotation =
            initialWheelRotation *
            Quaternion.AngleAxis(currentSteeringAngle, axis);
    }

    private void FixedUpdate()
    {
        float error = targetX - rb.position.x;

        float desiredVelocityX = Mathf.Clamp(
            error * followStrength,
            -laneChangeSpeed,
            laneChangeSpeed
        );

        Vector3 velocity = rb.linearVelocity;

        velocity.x = Mathf.MoveTowards(
            velocity.x,
            desiredVelocityX,
            acceleration * Time.fixedDeltaTime
        );

        // Stop horizontal movement when reaching target lane
        if (Mathf.Abs(error) < 0.02f &&
            Mathf.Abs(velocity.x) < 0.15f)
        {
            velocity.x = 0f;
        }

        rb.linearVelocity = velocity;
    }
}
