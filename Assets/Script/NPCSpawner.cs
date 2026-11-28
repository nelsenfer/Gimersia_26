
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

    [Header("2 NPC Troll Spesial (Mobil Kampanye & Kapal Bajak Laut)")]
    [Tooltip("Prefab NPC Troll 1: Mobil Kampanye (mobil kampanye benar.prefab - Audio: Reveille Loud FAST.wav)")]
    [SerializeField] private GameObject campaignNpcPrefab;
    [Tooltip("Prefab NPC Troll 2: Kapal Bajak Laut (Bajak Lawt Benar.prefab - Audio: pirate with radio full video.mp3)")]
    [SerializeField] private GameObject pirateShipNpcPrefab;
    [Tooltip("Pada spawn NPC ke-berapa NPC Troll pertama muncul (Default: mobil ke-3)")]
    [SerializeField] private int firstTrollSpawnAtCount = 3;
    [Tooltip("Interval kemunculan NPC Troll berikutnya secara bergantian (Default: tiap 5 mobil sekali -> mobil ke-3, ke-8, ke-13, dst. agar muncul ~3x di Level 1)")]
    [SerializeField] private int trollSpawnIntervalCount = 5;
    [Tooltip("Jarak despawn jauh di belakang pemain khusus untuk NPC Troll (meter) agar suara 3D-nya fade-out secara natural sampai habis sebelum objek dihapus (Default: 82m)")]
    [SerializeField] private float trollDespawnBehindDistance = 82f;

    [Header("Manuver Belok Komedi NPC Troll (Setelah Player Menyalip Mobil di Depannya)")]
    [Tooltip("Aktifkan fitur NPC Troll (Kampanye & Kapal Bajak Laut) bisa tiba-tiba banting setir pindah jalur tepat setelah pemain menyalip mobil di depannya")]
    [SerializeField] private bool enableTrollLaneSwitch = true;
    [Tooltip("Peluang (0 - 100%) NPC Troll melakukan manuver banting setir pindah jalur (Default: 75%)")]
    [Range(0, 100)]
    [SerializeField] private int trollLaneSwitchChancePercent = 75;
    [Tooltip("Jarak maksimal di depan pemain (meter) saat NPC Troll mulai banting setir setelah mobil di depannya lewat (Default: 32m = sudah keluar dari kabut & terlihat jelas)")]
    [SerializeField] private float trollSwitchMaxTriggerDistance = 32f;
    [Tooltip("Jarak minimal aman di depan pemain (meter) agar pemain masih punya waktu reaksi ~1.6 - 2.2 detik untuk menghindar (Default: 19m)")]
    [SerializeField] private float trollSwitchMinSafeDistance = 19f;
    [Tooltip("Durasi geser pindah jalur NPC Troll (detik)")]
    [SerializeField] private float trollLaneSwitchDuration = 0.48f;

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
        public EnvironmentMover mover;
        public int laneIndex;
        public bool isMajorTroll;
        public bool willSwitchLane;
        public bool hasSwitchedLane;
    }

    private readonly List<ActiveNPC> activeNPCs =
        new List<ActiveNPC>();

    private float nextSpawnTime;
    private float cachedRoadGroundY = 0.05f;
    private bool hasCachedRoadGroundY = false;

    // Menyimpan jalur NPC terakhir.
    // -1 berarti belum ada NPC yang berhasil spawn.
    private int lastSpawnedLane = -1;
    private int totalNormalNpcSpawnCount = 0;
    private int nextTrollVariantIndex = 0; // 0 = Mobil Kampanye, 1 = Kapal Bajak Laut (bergantian!)

    private static bool spawningEnabled = true;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSpawnerState()
    {
        spawningEnabled = true;
    }

    private void OnValidate()
    {
        AutoBindTrollPrefabs();
    }

    private void Awake()
    {
        spawningEnabled = true;
        lastSpawnedLane = -1;
        totalNormalNpcSpawnCount = 0;
        nextTrollVariantIndex = 0;
        AutoBindTrollPrefabs();
    }

    private void AutoBindTrollPrefabs()
    {
#if UNITY_EDITOR
        if (campaignNpcPrefab == null)
        {
            campaignNpcPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/ASSET FIX asli/mobil kampanye benar.prefab"
            );
        }
        if (pirateShipNpcPrefab == null)
        {
            pirateShipNpcPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/ASSET FIX asli/Bajak Lawt Benar.prefab"
            );
        }
#endif
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
        CacheNearPlayerRoadGroundY();

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

        Vector3 moveDir = GetMoveDirection();
        Vector3 aheadDir = -moveDir;

        // Bersihkan NPC yang sudah hancur ATAU sudah lewat lebih dari 15m di belakang mobil pemain
        // supaya NPC Troll yang sedang memutar fade-out audio hingga 82m di belakang TIDAK menahan slot maxActiveNPCs!
        activeNPCs.RemoveAll(item =>
        {
            if (item.obj == null)
                return true;

            if (playerCar != null)
            {
                float signedDistanceAhead = Vector3.Dot(
                    item.obj.transform.position - playerCar.position,
                    aheadDir
                );
                if (signedDistanceAhead < -15f)
                    return true;
            }

            return false;
        });

        UpdateTrollLaneSwitchManeuvers(aheadDir);

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
        // Kunci ke arah pandangan Kamera utama agar meskipun rotasi model 3D mobil dibalik 180 derajat,
        // mobil NPC selalu spawn di depan pandangan kamera dan bergerak mendekati pemain.
        if (Camera.main != null)
        {
            Vector3 camFwd = Camera.main.transform.forward;
            camFwd.y = 0f;
            if (camFwd.sqrMagnitude > 0.001f)
            {
                return -camFwd.normalized;
            }
        }

        Vector3 dir = moveOppositePlayerForward
            ? -playerCar.forward
            : playerCar.forward;

        dir.y = 0f;

        return dir.sqrMagnitude > 0.001f
            ? dir.normalized
            : Vector3.forward;
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

        bool isTrollNpc = IsTrollNpcName(npcObj.name);
        float effectiveBehindDist = isTrollNpc
            ? Mathf.Max(despawnBehindDistance, trollDespawnBehindDistance)
            : despawnBehindDistance;

        float totalTravelDistance = Mathf.Max(
            40f,
            distanceAhead + effectiveBehindDist
        );

        // Matikan visual mesh & collider saat sudah lewat despawnBehindDistance (25m di belakang kamera),
        // tetapi biarkan GameObject + AudioSource 3D NPC Troll tetap jalan sampai 82m di belakang agar fade-out suaranya 100% natural!
        float hideVisualsAtDistance = distanceAhead + despawnBehindDistance;

        mover.Initialize(
            moveDir,
            relativeSpeed,
            totalTravelDistance,
            hideVisualsAtDistance
        );

        GameplayAudioManager.AttachNPCEngineAudio(npcObj);

        bool isMajorTroll = IsMajorTrollNpcName(npcObj.name);
        bool rollWillSwitch = enableTrollLaneSwitch &&
                              isMajorTroll &&
                              Random.Range(0, 100) < trollLaneSwitchChancePercent;

        activeNPCs.Add(new ActiveNPC
        {
            obj = npcObj,
            mover = mover,
            laneIndex = chosenLane,
            isMajorTroll = isMajorTroll,
            willSwitchLane = rollWillSwitch,
            hasSwitchedLane = false
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

    private void CacheNearPlayerRoadGroundY()
    {
        Vector3 aheadDir = -GetMoveDirection();
        Vector3 nearProbe = new Vector3(
            GetLaneX(0),
            playerCar.position.y,
            playerCar.position.z + (aheadDir * 8f).z
        );

        Vector3 rayOrigin = nearProbe + Vector3.up * raycastHeight;
        Ray ray = new Ray(rayOrigin, Vector3.down);
        RaycastHit hit;

        if (groundSurface != null && groundSurface.Raycast(ray, out hit, raycastDistance))
        {
            cachedRoadGroundY = hit.point.y + groundOffset;
            hasCachedRoadGroundY = true;
            return;
        }

        if (groundLayers.value != 0 &&
            Physics.Raycast(ray, out hit, raycastDistance, groundLayers, QueryTriggerInteraction.Ignore))
        {
            cachedRoadGroundY = hit.point.y + groundOffset;
            hasCachedRoadGroundY = true;
            return;
        }

        cachedRoadGroundY = 0.05f + groundOffset;
        hasCachedRoadGroundY = true;
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

        // Pastikan mobil NPC selalu spawn di jarak jauh (di balik kabut), tidak pernah mundur ke depan mata pemain (< 70% distanceAhead)
        float minDistance = Mathf.Max(
            distanceAhead * 0.75f,
            minimumSpawnDistance
        );

        float searchStep = Mathf.Max(
            2f,
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

        // Detect actual asphalt surface (atau gunakan tinggi aspal datar yang sudah di-cache jika di luar ujung mesh Aspal 1)
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
        if (hasCachedRoadGroundY)
        {
            // Karena jalanan kita lurus datar, langsung pakai tinggi Y aspal yang sudah di-cache (0 Raycast saat runtime = sangat ringan & bisa spawn sejauh apa pun di balik kabut!)
            groundY = cachedRoadGroundY;
            return true;
        }

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

        groundY = cachedRoadGroundY;
        return true;
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

    /// <summary>
    /// Mengecek apakah ada NPC Troll yang siap banting setir pindah jalur:
    /// 1. Pemain SUDAH menyalip/melewati mobil NPC di depannya (tidak ada mobil lain di antara pemain dan NPC Troll).
    /// 2. Jarak NPC Troll berada di zona terlihat & wajar (19m - 32m di depan pemain) sehingga pemain masih sempat membelokkan setir.
    /// 3. Jalur tujuan aman dari mobil di belakang NPC Troll (tidak menabrak/menumpuk dengan mobil lain).
    /// </summary>
    private void UpdateTrollLaneSwitchManeuvers(Vector3 aheadDir)
    {
        if (!enableTrollLaneSwitch || playerCar == null || activeNPCs.Count == 0)
            return;

        float minSafeDist = Mathf.Max(14f, trollSwitchMinSafeDistance);
        float maxTriggerDist = Mathf.Max(minSafeDist + 6f, trollSwitchMaxTriggerDistance);

        for (int i = 0; i < activeNPCs.Count; i++)
        {
            ActiveNPC troll = activeNPCs[i];
            if (troll == null || troll.obj == null || !troll.isMajorTroll || !troll.willSwitchLane || troll.hasSwitchedLane)
                continue;

            float trollDistAhead = Vector3.Dot(
                troll.obj.transform.position - playerCar.position,
                aheadDir
            );

            // Jika sudah terlalu dekat dan belum sempat belok, batalkan agar tidak curang/mustahil dihindari
            if (trollDistAhead < minSafeDist)
            {
                troll.hasSwitchedLane = true;
                continue;
            }

            // Tunggu sampai NPC Troll masuk ke jarak ideal di depan kaca mobil (19m - 32m)
            if (trollDistAhead > maxTriggerDist)
                continue;

            int targetLane = 1 - troll.laneIndex;
            bool hasCarInBetweenPlayerAndTroll = false;
            bool isTargetLaneBlockedNearTroll = false;

            for (int j = 0; j < activeNPCs.Count; j++)
            {
                if (i == j) continue;
                ActiveNPC other = activeNPCs[j];
                if (other == null || other.obj == null) continue;

                float otherDistAhead = Vector3.Dot(
                    other.obj.transform.position - playerCar.position,
                    aheadDir
                );

                // Syarat 1: Tunggu sampai pemain SUDAH menyalip/melewati mobil yang ada di depan pemain
                // (Jangan belok kalau masih ada mobil lain di antara hidung mobil pemain [-2m] dan NPC Troll!)
                if (otherDistAhead > -2.5f && otherDistAhead < trollDistAhead)
                {
                    hasCarInBetweenPlayerAndTroll = true;
                    break;
                }

                // Syarat 2: Pastikan di jalur tujuan tidak ada mobil lain yang terlalu berdekatan dengan NPC Troll
                if (other.laneIndex == targetLane && Mathf.Abs(otherDistAhead - trollDistAhead) < minCrossLaneGap * 0.85f)
                {
                    isTargetLaneBlockedNearTroll = true;
                    break;
                }
            }

            if (hasCarInBetweenPlayerAndTroll || isTargetLaneBlockedNearTroll)
                continue;

            // Semua syarat terpenuhi! Pemain baru saja menyalip mobil di depannya -> NPC Troll langsung banting setir pindah jalur!
            troll.hasSwitchedLane = true;
            troll.laneIndex = targetLane;

            float newLaneX = GetLaneX(targetLane);
            float currentCenterOffsetX = GetVisualCenterOffsetX(troll.obj);
            float targetRootX = newLaneX - currentCenterOffsetX;

            if (troll.mover != null)
            {
                troll.mover.StartTrollLaneChange(targetRootX, trollLaneSwitchDuration, 13f);
                GameplayAudioManager.PlayNPCLaneChangeSwerve(troll.obj);
            }
        }
    }

    private float GetVisualCenterOffsetX(GameObject obj)
    {
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
            return 0f;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return bounds.center.x - obj.transform.position.x;
    }

    private bool IsMajorTrollNpcName(string objName)
    {
        if (string.IsNullOrEmpty(objName)) return false;
        return objName.IndexOf("kampanye", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
               objName.IndexOf("Bajak", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
               objName.IndexOf("Pirate", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private bool IsTrollNpcName(string objName)
    {
        if (string.IsNullOrEmpty(objName)) return false;
        return objName.IndexOf("kampanye", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
               objName.IndexOf("Bajak", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
               objName.IndexOf("Pirate", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
               objName.IndexOf("Angkot", System.StringComparison.OrdinalIgnoreCase) >= 0;
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

        totalNormalNpcSpawnCount++;

        GameObject selectedPrefab = null;

        // 1. Cek apakah waktunya spawn NPC Troll Spesial (Mobil Kampanye & Kapal Bajak Laut bergantian!)
        // Muncul pertama di firstTrollSpawnAtCount (mobil ke-3), lalu setiap trollSpawnIntervalCount berikutnya (mobil ke-8, ke-13, dst.)
        int firstOrder = Mathf.Max(1, firstTrollSpawnAtCount);
        int interval = Mathf.Max(2, trollSpawnIntervalCount);
        bool isTrollTurn = (totalNormalNpcSpawnCount == firstOrder) ||
                           (totalNormalNpcSpawnCount > firstOrder && (totalNormalNpcSpawnCount - firstOrder) % interval == 0);

        if (isTrollTurn)
        {
            if (nextTrollVariantIndex == 0)
            {
                selectedPrefab = campaignNpcPrefab != null ? campaignNpcPrefab : pirateShipNpcPrefab;
                nextTrollVariantIndex = 1;
            }
            else
            {
                selectedPrefab = pirateShipNpcPrefab != null ? pirateShipNpcPrefab : campaignNpcPrefab;
                nextTrollVariantIndex = 0;
            }
        }

        // 2. Jika bukan giliran NPC Troll (Mobil Kampanye / Kapal Bajak Laut),
        // pilih dari daftar npcPrefabs standar (dan skip prefab kampanye / kapal bajak laut agar ritmenya tetap terjaga rapi!)
        if (selectedPrefab == null && npcPrefabs != null && npcPrefabs.Length > 0)
        {
            List<GameObject> candidates = new List<GameObject>();
            for (int i = 0; i < npcPrefabs.Length; i++)
            {
                GameObject p = npcPrefabs[i];
                if (p == null) continue;

                bool isSpecialTroll = (campaignNpcPrefab != null && p == campaignNpcPrefab) ||
                                      (pirateShipNpcPrefab != null && p == pirateShipNpcPrefab) ||
                                      p.name.IndexOf("kampanye", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                                      p.name.IndexOf("Bajak", System.StringComparison.OrdinalIgnoreCase) >= 0;

                if (isSpecialTroll)
                {
                    continue;
                }

                candidates.Add(p);
            }

            if (candidates.Count > 0)
            {
                selectedPrefab = candidates[Random.Range(0, candidates.Count)];
            }
            else
            {
                selectedPrefab = npcPrefabs[Random.Range(0, npcPrefabs.Length)];
            }
        }

        if (selectedPrefab != null)
        {
            GameObject instance = Instantiate(
                selectedPrefab,
                spawnPosition,
                facingRotation * selectedPrefab.transform.rotation
            );

            EnsureObstacleSetup(instance);

            AlignBottomToGround(
                instance,
                spawnPosition.x,
                spawnPosition.y
            );

            return instance;
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

    // Align NPC visual bottom to asphalt & center X to the exact lane center
    // (Mencegah mobil NPC melenceng ke garis putus-putus tengah jalan jika titik pivot FBX 3D-nya tidak pas di tengah mesh)
    private void AlignBottomToGround(
        GameObject obj,
        float targetLaneX,
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

        // Koreksi sumbu X agar titik tengah visual bodi/roda mobil (bounds.center.x)
        // tepat berada di tengah jalur (targetLaneX), tidak peduli bila pivot FBX agak miring ke kiri/kanan!
        float correctionX =
            targetLaneX - bounds.center.x;

        float correctionY =
            groundY - bounds.min.y;

        Vector3 correctedPosition =
            obj.transform.position +
            new Vector3(correctionX, correctionY, 0f);

        obj.transform.position = correctedPosition;

        // Pastikan BoxCollider trigger juga membungkus tepat di tengah bodi visual mobil
        BoxCollider rootBox = obj.GetComponent<BoxCollider>();
        if (rootBox != null)
        {
            Vector3 newBoundsCenter = bounds.center + new Vector3(correctionX, correctionY, 0f);
            rootBox.center = obj.transform.InverseTransformPoint(newBoundsCenter);
        }

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
