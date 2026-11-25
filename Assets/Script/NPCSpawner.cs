
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// NPC Spawner untuk game Endless Driving dua jalur.
/// NPC muncul secara acak di lane kiri atau kanan,
/// bergerak melewati player, dan despawn melalui EnvironmentMover.
/// </summary>
public class NPCSpawner : MonoBehaviour
{
    [Header("Referensi Player")]
    [SerializeField] private Transform playerCar;

    [Header("Prefab Mobil NPC")]
    [SerializeField] private GameObject[] npcPrefabs;

    [Header("Pengaturan 2 Jalur")]
    [SerializeField] private float roadCenterX = -1.63f;
    [SerializeField] private float laneOffset = 1.404f;

    [Header("Pengaturan Jarak Spawn & Despawn")]
    [SerializeField] private float spawnDistanceAhead = 30f;
    [SerializeField] private float minimumSpawnDistance = 5f;
    [SerializeField] private float spawnSearchStep = 1f;
    [SerializeField] private float despawnBehindDistance = 35f;

    [Tooltip("False jika CarRoot menghadap -Z (Rotation Y = 180)")]
    [SerializeField] private bool moveOppositePlayerForward = false;

    [Header("Pre-Spawn Awal")]
    [SerializeField] private bool preSpawnOnStart = true;

    [SerializeField] private float[] initialSpawnDistances =
        new float[] { 12f, 30f };

    [Header("Pengaturan Kecepatan")]
    [SerializeField] private float playerSpeed = 12f;
    [SerializeField] private float npcForwardSpeed = 7f;

    [Header("Pengaturan Interval & Anti-Tembok Buntu")]
    [SerializeField] private float minSpawnInterval = 2f;
    [SerializeField] private float maxSpawnInterval = 4f;
    [SerializeField] private int maxActiveNPCs = 6;

    [SerializeField] private float minSameLaneSeparation = 28f;
    [SerializeField] private float minCrossLaneGap = 8f;

    [SerializeField] private bool spawnImmediately = true;

    [Header("Ground Detection")]
    [SerializeField] private Collider groundSurface;
    [SerializeField] private LayerMask groundLayers;
    [SerializeField] private float groundOffset = 0f;
    [SerializeField] private float raycastHeight = 30f;
    [SerializeField] private float raycastDistance = 100f;

    private class ActiveNPC
    {
        public GameObject obj;
        public int laneIndex;
    }

    private readonly List<ActiveNPC> activeNPCs =
        new List<ActiveNPC>();

    private float nextSpawnTime;

    // Menyimpan jalur NPC terakhir.
    // -1 berarti belum ada NPC yang berhasil spawn.
    private int lastSpawnedLane = -1;

    private static bool spawningEnabled = true;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSpawnerState()
    {
        spawningEnabled = true;
    }

    private void Awake()
    {
        spawningEnabled = true;
        lastSpawnedLane = -1;
    }

    private void Start()
    {
        if (playerCar == null)
        {
            CarController car =
                FindFirstObjectByType<CarController>();

            if (car != null)
                playerCar = car.transform;
        }

        if (playerCar == null)
        {
            Debug.LogError(
                "[NPCSpawner] Player Car belum dihubungkan!"
            );

            enabled = false;
            return;
        }

        // Jalankan diagnostik kedua jalur sebelum spawn.
        DiagnoseBothLanes();

        // Spawn beberapa NPC saat awal permainan
        if (preSpawnOnStart &&
            initialSpawnDistances != null)
        {
            foreach (float distance in initialSpawnDistances)
            {
                if (activeNPCs.Count >= maxActiveNPCs)
                    break;

                TrySpawnNPCAtDistance(distance);
            }
        }

        if (spawnImmediately)
        {
            nextSpawnTime = Time.time;
        }
        else
        {
            ScheduleNextSpawn();
        }
    }

    private void Update()
    {
        if (!spawningEnabled)
            return;

        activeNPCs.RemoveAll(item => item.obj == null);

        if (Time.time < nextSpawnTime)
            return;

        ScheduleNextSpawn();

        if (activeNPCs.Count >= maxActiveNPCs)
            return;

        TrySpawnNPC();
    }

    private void ScheduleNextSpawn()
    {
        float min = Mathf.Min(
            minSpawnInterval,
            maxSpawnInterval
        );

        float max = Mathf.Max(
            minSpawnInterval,
            maxSpawnInterval
        );

        nextSpawnTime =
            Time.time + Random.Range(min, max);
    }

    // Determine environment movement direction
    private Vector3 GetMoveDirection()
    {
        Vector3 dir = moveOppositePlayerForward
            ? -playerCar.forward
            : playerCar.forward;

        dir.y = 0f;

        return dir.sqrMagnitude > 0.001f
            ? dir.normalized
            : Vector3.back;
    }

    // Get exact X position of each lane
    private float GetLaneX(int laneIndex)
    {
        return laneIndex == 0
            ? roadCenterX - laneOffset
            : roadCenterX + laneOffset;
    }

    private void TrySpawnNPC()
    {
        TrySpawnNPCAtDistance(spawnDistanceAhead);
    }

    private void TrySpawnNPCAtDistance(float distanceAhead)
    {
        Vector3 moveDir = GetMoveDirection();
        Vector3 aheadDir = -moveDir;

        // Prioritaskan lane yang berbeda dari NPC terakhir.
        int firstLane;

        if (lastSpawnedLane == -1)
        {
            // Spawn pertama memilih lane secara acak.
            firstLane = Random.Range(0, 2);
        }
        else
        {
            // Spawn berikutnya mencoba lane sebaliknya.
            firstLane = 1 - lastSpawnedLane;
        }

        int secondLane = 1 - firstLane;

        int chosenLane;
        Vector3 spawnPosition;

        // Search for a valid spawn position on the road
        bool foundPosition = TryFindSpawnPosition(
            distanceAhead,
            aheadDir,
            firstLane,
            secondLane,
            out chosenLane,
            out spawnPosition
        );

        if (!foundPosition)
        {
            Debug.LogWarning(
                "[NPCSpawner] Tidak menemukan posisi spawn " +
                "yang aman di atas aspal. Periksa Ground Surface " +
                "dan Ground Layers."
            );

            return;
        }

        // Create NPC
        GameObject npcObj =
            CreateNPCObject(spawnPosition, aheadDir);

        if (npcObj == null)
            return;

        // Setup NPC movement
        EnvironmentMover mover =
            npcObj.GetComponent<EnvironmentMover>();

        if (mover == null)
            mover = npcObj.AddComponent<EnvironmentMover>();

        Rigidbody rb = npcObj.GetComponent<Rigidbody>();

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
        }

        // Relative movement speed
        float relativeSpeed = Mathf.Max(
            0.5f,
            playerSpeed - npcForwardSpeed
        );

        float totalTravelDistance = Mathf.Max(
            40f,
            distanceAhead + despawnBehindDistance
        );

        mover.Initialize(
            moveDir,
            relativeSpeed,
            totalTravelDistance
        );

        GameplayAudioManager.AttachNPCEngineAudio(npcObj);

        activeNPCs.Add(new ActiveNPC
        {
            obj = npcObj,
            laneIndex = chosenLane
        });

        // Simpan lane terakhir yang berhasil digunakan.
        lastSpawnedLane = chosenLane;

        Debug.Log(
            "[NPCSpawner] NPC Spawned | Lane: " +
            (chosenLane == 0 ? "Left" : "Right") +
            " | X: " + spawnPosition.x +
            " | Y: " + spawnPosition.y +
            " | Z: " + spawnPosition.z
        );
    }

    // Search backward from desired spawn distance
    // until a valid road surface is found.
    private bool TryFindSpawnPosition(
        float distanceAhead,
        Vector3 aheadDir,
        int firstLane,
        int secondLane,
        out int chosenLane,
        out Vector3 spawnPosition)
    {
        chosenLane = -1;
        spawnPosition = Vector3.zero;

        float minDistance = Mathf.Max(
            0.5f,
            minimumSpawnDistance
        );

        float searchStep = Mathf.Max(
            0.25f,
            spawnSearchStep
        );

        float searchDistance = Mathf.Max(
            distanceAhead,
            minDistance
        );

        for (
            float distance = searchDistance;
            distance >= minDistance;
            distance -= searchStep
        )
        {
            Vector3 spawnOffset = aheadDir * distance;

            float spawnZ =
                playerCar.position.z + spawnOffset.z;

            // Coba lane prioritas terlebih dahulu.
            if (TryLaneAtDistance(
                firstLane,
                spawnZ,
                out spawnPosition))
            {
                chosenLane = firstLane;
                return true;
            }

            // Jika gagal, coba lane alternatif.
            if (TryLaneAtDistance(
                secondLane,
                spawnZ,
                out spawnPosition))
            {
                chosenLane = secondLane;
                return true;
            }
        }

        return false;
    }

    // Check lane safety and actual road surface
    private bool TryLaneAtDistance(
        int laneIndex,
        float spawnZ,
        out Vector3 spawnPosition)
    {
        spawnPosition = Vector3.zero;

        // Avoid spawning too close to other NPCs
        if (!IsLaneSafeToSpawn(laneIndex, spawnZ))
            return false;

        float laneX = GetLaneX(laneIndex);

        Vector3 groundProbe = new Vector3(
            laneX,
            playerCar.position.y,
            spawnZ
        );

        // Detect actual asphalt surface
        if (!TryGetGroundY(groundProbe, out float groundY))
            return false;

        spawnPosition = new Vector3(
            laneX,
            groundY,
            spawnZ
        );

        return true;
    }

    // Check whether the selected lane is safe
    private bool IsLaneSafeToSpawn(
        int laneIndex,
        float spawnZ)
    {
        foreach (ActiveNPC npc in activeNPCs)
        {
            if (npc.obj == null)
                continue;

            float distanceZ = Mathf.Abs(
                npc.obj.transform.position.z - spawnZ
            );

            if (npc.laneIndex == laneIndex)
            {
                if (distanceZ < minSameLaneSeparation)
                    return false;
            }
            else
            {
                if (distanceZ < minCrossLaneGap)
                    return false;
            }
        }

        return true;
    }

    // Detect actual asphalt height
    private bool TryGetGroundY(
        Vector3 position,
        out float groundY)
    {
        Vector3 rayOrigin =
            position + Vector3.up * raycastHeight;

        Ray ray = new Ray(
            rayOrigin,
            Vector3.down
        );

        RaycastHit hit = default;

        // First try the assigned ground collider
        if (groundSurface != null)
        {
            if (groundSurface.Raycast(
                ray,
                out hit,
                raycastDistance))
            {
                groundY = hit.point.y + groundOffset;
                return true;
            }
        }

        // If the assigned collider does not cover
        // this position, try other colliders in Ground Layers.
        if (groundLayers.value != 0)
        {
            if (Physics.Raycast(
                ray,
                out hit,
                raycastDistance,
                groundLayers,
                QueryTriggerInteraction.Ignore))
            {
                groundY = hit.point.y + groundOffset;
                return true;
            }
        }

        groundY = 0f;
        return false;
    }

    // ==========================================
    // DIAGNOSTIC: CHECK LEFT AND RIGHT LANES
    // ==========================================
    private void DiagnoseBothLanes()
    {
        Vector3 aheadDir = -GetMoveDirection();

        float testDistance = 12f;

        float testZ = playerCar.position.z +
            (aheadDir * testDistance).z;

        Debug.Log("===== NPC LANE DIAGNOSTIC =====");

        Debug.Log(
            "Player Z: " + playerCar.position.z
        );

        Debug.Log(
            "Test Z: " + testZ
        );

        Debug.Log(
            "Road Center X: " + roadCenterX +
            " | Lane Offset: " + laneOffset
        );

        Debug.Log(
            "Left X: " + GetLaneX(0) +
            " | Right X: " + GetLaneX(1)
        );

        Debug.Log(
            "Ground Surface: " +
            (groundSurface != null
                ? groundSurface.name
                : "BELUM DIISI")
        );

        if (groundSurface != null)
        {
            Debug.Log(
                "Collider Enabled: " + groundSurface.enabled
            );

            Debug.Log(
                "Collider Bounds: " + groundSurface.bounds
            );

            MeshCollider meshCollider =
                groundSurface as MeshCollider;

            if (meshCollider != null)
            {
                Debug.Log(
                    "Mesh Collider Shared Mesh: " +
                    (meshCollider.sharedMesh != null
                        ? meshCollider.sharedMesh.name
                        : "BELUM DIISI")
                );

                Debug.Log(
                    "Mesh Collider Convex: " +
                    meshCollider.convex
                );
            }
        }

        Debug.Log(
            "Ground Layers: " + groundLayers.value +
            " | Cross Lane Gap: " + minCrossLaneGap
        );

        for (int lane = 0; lane < 2; lane++)
        {
            float laneX = GetLaneX(lane);

            Vector3 origin = new Vector3(
                laneX,
                playerCar.position.y + raycastHeight,
                testZ
            );

            Ray ray = new Ray(origin, Vector3.down);

            RaycastHit surfaceHit = default;

            bool directHit =
                groundSurface != null &&
                groundSurface.enabled &&
                groundSurface.Raycast(
                    ray,
                    out surfaceHit,
                    raycastDistance
                );

            RaycastHit[] allHits = Physics.RaycastAll(
                ray,
                raycastDistance,
                ~0,
                QueryTriggerInteraction.Ignore
            );

            string hitNames = "";

            foreach (RaycastHit hitInfo in allHits)
            {
                if (hitNames.Length > 0)
                    hitNames += ", ";

                hitNames +=
                    hitInfo.collider.name +
                    " (Layer: " +
                    LayerMask.LayerToName(
                        hitInfo.collider.gameObject.layer
                    ) +
                    ")";
            }

            if (hitNames.Length == 0)
                hitNames = "Tidak ada collider yang terkena";

            Debug.Log(
                "[NPC DIAGNOSTIC] " +
                (lane == 0 ? "LEFT" : "RIGHT") +
                " | X: " + laneX +
                " | Direct Mesh Hit: " + directHit +
                " | Hit Object: " +
                (directHit ? surfaceHit.collider.name : "-") +
                " | All Hits: " + hitNames
            );
        }

        Debug.Log("===== END NPC DIAGNOSTIC =====");
    }

    // Create NPC prefab or placeholder
    private GameObject CreateNPCObject(
        Vector3 spawnPosition,
        Vector3 facingDirection)
    {
        Quaternion facingRotation =
            Quaternion.LookRotation(
                facingDirection,
                Vector3.up
            );

        // Use assigned NPC prefab
        if (npcPrefabs != null && npcPrefabs.Length > 0)
        {
            GameObject prefab = npcPrefabs[
                Random.Range(0, npcPrefabs.Length)
            ];

            if (prefab != null)
            {
                GameObject instance = Instantiate(
                    prefab,
                    spawnPosition,
                    facingRotation * prefab.transform.rotation
                );

                EnsureObstacleSetup(instance);

                AlignBottomToGround(
                    instance,
                    spawnPosition.y
                );

                return instance;
            }
        }

        // Create placeholder NPC
        GameObject car = new GameObject(
            "NPC_Car_Placeholder"
        );

        car.transform.position = spawnPosition;
        car.transform.rotation = facingRotation;
        car.tag = "Obstacle";

        BoxCollider col = car.AddComponent<BoxCollider>();

        col.isTrigger = true;
        col.center = new Vector3(0f, 0.7f, 0f);
        col.size = new Vector3(1.8f, 1.4f, 3.8f);

        // Car Body
        GameObject body =
            GameObject.CreatePrimitive(PrimitiveType.Cube);

        body.name = "Body";

        Destroy(body.GetComponent<Collider>());

        body.transform.SetParent(car.transform, false);
        body.transform.localPosition =
            new Vector3(0f, 0.45f, 0f);

        body.transform.localScale =
            new Vector3(1.8f, 0.8f, 3.8f);

        SetSimpleColor(
            body,
            new Color(0.25f, 0.30f, 0.38f)
        );

        // Cabin
        GameObject cabin =
            GameObject.CreatePrimitive(PrimitiveType.Cube);

        cabin.name = "Cabin";

        Destroy(cabin.GetComponent<Collider>());

        cabin.transform.SetParent(car.transform, false);
        cabin.transform.localPosition =
            new Vector3(0f, 1.1f, -0.2f);

        cabin.transform.localScale =
            new Vector3(1.45f, 0.55f, 1.8f);

        SetSimpleColor(
            cabin,
            new Color(0.18f, 0.22f, 0.28f)
        );

        // Rear tail lights
        CreateTailLight(
            car.transform,
            new Vector3(-0.62f, 0.52f, -1.92f)
        );

        CreateTailLight(
            car.transform,
            new Vector3(0.62f, 0.52f, -1.92f)
        );

        return car;
    }

    private void CreateTailLight(
        Transform parent,
        Vector3 localPosition)
    {
        GameObject lightBox =
            GameObject.CreatePrimitive(PrimitiveType.Cube);

        lightBox.name = "TailLight";

        Destroy(lightBox.GetComponent<Collider>());

        lightBox.transform.SetParent(parent, false);
        lightBox.transform.localPosition = localPosition;

        lightBox.transform.localScale =
            new Vector3(0.32f, 0.16f, 0.08f);

        Renderer rend = lightBox.GetComponent<Renderer>();

        if (rend != null)
        {
            Shader shader = Shader.Find(
                "Universal Render Pipeline/Lit"
            );

            if (shader == null)
                shader = Shader.Find("Standard");

            if (shader != null)
            {
                Material mat = new Material(shader);

                mat.color = Color.red;

                mat.EnableKeyword("_EMISSION");
                mat.SetColor(
                    "_EmissionColor",
                    Color.red * 3.5f
                );

                rend.sharedMaterial = mat;
            }
        }
    }

    private void SetSimpleColor(
        GameObject obj,
        Color color)
    {
        Renderer rend = obj.GetComponent<Renderer>();

        if (rend != null)
        {
            Shader shader = Shader.Find(
                "Universal Render Pipeline/Lit"
            );

            if (shader == null)
                shader = Shader.Find("Standard");

            if (shader != null)
            {
                Material mat = new Material(shader);
                mat.color = color;
                rend.sharedMaterial = mat;
            }
        }
    }

    // Configure NPC collider and obstacle tag
    private void EnsureObstacleSetup(GameObject obj)
    {
        obj.tag = "Obstacle";

        Collider[] colliders =
            obj.GetComponentsInChildren<Collider>(true);

        if (colliders.Length == 0)
        {
            BoxCollider box =
                obj.AddComponent<BoxCollider>();

            box.isTrigger = true;
        }
        else
        {
            foreach (Collider col in colliders)
            {
                col.isTrigger = true;
            }
        }
    }

    // Align NPC visual bottom to asphalt
    private void AlignBottomToGround(
        GameObject obj,
        float groundY)
    {
        Renderer[] renderers =
            obj.GetComponentsInChildren<Renderer>(true);

        if (renderers.Length == 0)
            return;

        Bounds bounds = renderers[0].bounds;

        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        float correction =
            groundY - bounds.min.y;

        Vector3 correctedPosition =
            obj.transform.position +
            Vector3.up * correction;

        obj.transform.position = correctedPosition;

        Rigidbody rb = obj.GetComponent<Rigidbody>();

        if (rb != null)
        {
            rb.position = correctedPosition;
            rb.rotation = obj.transform.rotation;
        }

        Physics.SyncTransforms();
    }

    public static void StopSpawning()
    {
        spawningEnabled = false;
    }

    public static void ResetSpawning()
    {
        spawningEnabled = true;
    }
}
