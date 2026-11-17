using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawner Mobil NPC di 2 jalur jalan raya.
/// - Spawn jauh di depan (dalam kabut) agar tidak terlihat pop-in mendadak.
/// - Kecepatan relatif seragam (playerSpeed - npcForwardSpeed) sehingga terlihat mundur perlahan.
/// - Menjamin tidak pernah membuat tembok buntu (kedua jalur tertutup di Z yang sama).
/// - Jika prefab mobil belum ada, otomatis membuat mobil kotak 3D placeholder lengkap dengan lampu rem merah.
/// </summary>
public class NPCSpawner : MonoBehaviour
{
    [Header("Referensi Player")]
    [SerializeField] private Transform playerCar;

    [Header("Prefab Mobil NPC (Opsional - Jika kosong pakai Kotak 3D Placeholder)")]
    [SerializeField] private GameObject[] npcPrefabs;

    [Header("Pengaturan 2 Jalur (Samakan dengan CarController)")]
    [SerializeField] private float roadCenterX = -0.14f;
    [SerializeField] private float laneOffset = 3f;

    [Header("Pengaturan Jarak Spawn & Despawn")]
    [Tooltip("Jarak spawn di depan mobil player (taruh jauh di dalam kabut, misal 75)")]
    [SerializeField] private float spawnDistanceAhead = 75f;

    [Tooltip("Jarak tambahan di belakang mobil player sebelum NPC dihancurkan")]
    [SerializeField] private float despawnBehindDistance = 35f;

    [Tooltip("Samakan dengan arah conveyor TreeSpawner (false jika Car Root rotasi Y=180)")]
    [SerializeField] private bool moveOppositePlayerForward = false;

    [Header("Pre-Spawn Awal (Agar Jalan Langsung Ramai Saat Mulai)")]
    [Tooltip("Langsung spawn beberapa mobil di depan saat game dimulai")]
    [SerializeField] private bool preSpawnOnStart = true;

    [Tooltip("Jarak mobil-mobil awal di depan player saat baru Play")]
    [SerializeField] private float[] initialSpawnDistances = new float[] { 35f, 60f };

    [Header("Pengaturan Kecepatan Seragam")]
    [Tooltip("Kecepatan jalan player (samakan dengan moveSpeed RoadLineSpawner, misal 12)")]
    [SerializeField] private float playerSpeed = 12f;

    [Tooltip("Kecepatan laju mobil NPC ke depan (harus lebih pelan dari playerSpeed, misal 7)")]
    [SerializeField] private float npcForwardSpeed = 7f;

    [Header("Pengaturan Interval & Anti-Tembok Buntu")]
    [SerializeField] private float minSpawnInterval = 2.0f;
    [SerializeField] private float maxSpawnInterval = 4.0f;
    [SerializeField] private int maxActiveNPCs = 6;

    [Tooltip("Jarak aman minimal antar NPC di jalur yang SAMA")]
    [SerializeField] private float minSameLaneSeparation = 28f;

    [Tooltip("Jarak aman minimal antara NPC jalur kiri dan kanan agar TIDAK jadi tembok buntu")]
    [SerializeField] private float minCrossLaneGap = 16f;

    [SerializeField] private bool spawnImmediately = true;

    [Header("Deteksi Ketinggian Aspal (Opsional)")]
    [SerializeField] private Collider groundSurface;
    [SerializeField] private LayerMask groundLayers;
    [SerializeField] private float groundOffset = 0f;

    private class ActiveNPC
    {
        public GameObject obj;
        public int laneIndex;
    }

    private readonly List<ActiveNPC> activeNPCs = new List<ActiveNPC>();
    private float nextSpawnTime;

    private static bool spawningEnabled = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSpawnerState()
    {
        spawningEnabled = true;
    }

    private void Start()
    {
        if (playerCar == null)
        {
            CarController car = FindFirstObjectByType<CarController>();
            if (car != null) playerCar = car.transform;
        }

        if (playerCar == null)
        {
            Debug.LogError("[NPCSpawner] Referensi playerCar belum diisi!");
            enabled = false;
            return;
        }

        if (preSpawnOnStart && initialSpawnDistances != null)
        {
            for (int i = 0; i < initialSpawnDistances.Length; i++)
            {
                if (activeNPCs.Count >= maxActiveNPCs) break;
                TrySpawnNPCAtDistance(initialSpawnDistances[i]);
            }
        }

        if (spawnImmediately)
            nextSpawnTime = Time.time;
        else
            ScheduleNextSpawn();
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
        float min = Mathf.Min(minSpawnInterval, maxSpawnInterval);
        float max = Mathf.Max(minSpawnInterval, maxSpawnInterval);
        nextSpawnTime = Time.time + Random.Range(min, max);
    }

    private Vector3 GetMoveDirection()
    {
        Vector3 dir = moveOppositePlayerForward ? -playerCar.forward : playerCar.forward;
        dir.y = 0f;
        return dir.sqrMagnitude > 0.001f ? dir.normalized : Vector3.back;
    }

    private float GetLaneX(int laneIndex)
    {
        // 0 = Kiri, 1 = Kanan (sama seperti CarController)
        return laneIndex == 0 ? roadCenterX - laneOffset : roadCenterX + laneOffset;
    }

    private void TrySpawnNPC()
    {
        TrySpawnNPCAtDistance(spawnDistanceAhead);
    }

    private void TrySpawnNPCAtDistance(float distanceAhead)
    {
        Vector3 moveDir = GetMoveDirection();
        Vector3 aheadDir = -moveDir;

        float spawnZ = playerCar.position.z + aheadDir.z * distanceAhead;

        // Acak urutan pengecekan jalur (0 atau 1)
        int firstLane = Random.Range(0, 2);
        int secondLane = 1 - firstLane;

        int chosenLane = -1;
        if (IsLaneSafeToSpawn(firstLane, spawnZ))
        {
            chosenLane = firstLane;
        }
        else if (IsLaneSafeToSpawn(secondLane, spawnZ))
        {
            chosenLane = secondLane;
        }

        if (chosenLane == -1)
        {
            // Kedua jalur masih terlalu dekat dengan NPC lain -> tunda agar tidak bikin tembok buntu
            return;
        }

        float laneX = GetLaneX(chosenLane);
        float groundY = GetGroundY(new Vector3(laneX, playerCar.position.y, spawnZ));
        Vector3 spawnPos = new Vector3(laneX, groundY, spawnZ);

        GameObject npcObj = CreateNPCObject(spawnPos, aheadDir);

        // Pasang & atur EnvironmentMover dengan kecepatan relatif seragam
        EnvironmentMover mover = npcObj.GetComponent<EnvironmentMover>();
        if (mover == null)
        {
            mover = npcObj.AddComponent<EnvironmentMover>();
        }

        Rigidbody rb = npcObj.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        float relativeSpeed = Mathf.Max(0.5f, playerSpeed - npcForwardSpeed);
        float totalTravelDistance = Mathf.Max(40f, distanceAhead + despawnBehindDistance);
        mover.Initialize(moveDir, relativeSpeed, totalTravelDistance);

        activeNPCs.Add(new ActiveNPC
        {
            obj = npcObj,
            laneIndex = chosenLane
        });
    }

    private bool IsLaneSafeToSpawn(int laneIndex, float spawnZ)
    {
        for (int i = 0; i < activeNPCs.Count; i++)
        {
            ActiveNPC npc = activeNPCs[i];
            if (npc.obj == null) continue;

            float distZ = Mathf.Abs(npc.obj.transform.position.z - spawnZ);

            if (npc.laneIndex == laneIndex)
            {
                // Jangan bertumpuk di jalur yang sama
                if (distZ < minSameLaneSeparation)
                    return false;
            }
            else
            {
                // Anti Tembok Buntu: Jangan sejajar dengan mobil di jalur sebelah!
                if (distZ < minCrossLaneGap)
                    return false;
            }
        }

        return true;
    }

    private float GetGroundY(Vector3 pos)
    {
        Vector3 rayOrigin = pos + Vector3.up * 30f;
        Ray ray = new Ray(rayOrigin, Vector3.down);
        RaycastHit hit;

        if (groundSurface != null)
        {
            if (groundSurface.Raycast(ray, out hit, 100f))
                return hit.point.y + groundOffset;
        }
        else if (groundLayers.value != 0)
        {
            if (Physics.Raycast(ray, out hit, 100f, groundLayers, QueryTriggerInteraction.Ignore))
                return hit.point.y + groundOffset;
        }

        return playerCar.position.y;
    }

    private GameObject CreateNPCObject(Vector3 spawnPos, Vector3 facingDir)
    {
        Quaternion facingRot = Quaternion.LookRotation(facingDir, Vector3.up);

        // Jika tim sudah memasukkan prefab mobil, pakai prefab tersebut
        if (npcPrefabs != null && npcPrefabs.Length > 0)
        {
            GameObject prefab = npcPrefabs[Random.Range(0, npcPrefabs.Length)];
            if (prefab != null)
            {
                GameObject instance = Instantiate(prefab, spawnPos, facingRot * prefab.transform.rotation);
                EnsureObstacleSetup(instance);
                AlignBottomToGround(instance, spawnPos.y);
                return instance;
            }
        }

        // Jika belum ada asset prefab, buat Mobil Kotak 3D Placeholder + 2 Lampu Belakang Merah
        GameObject car = new GameObject("NPC_Car_Placeholder");
        car.transform.position = spawnPos;
        car.transform.rotation = facingRot;
        car.tag = "Obstacle";

        BoxCollider col = car.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.center = new Vector3(0f, 0.7f, 0f);
        col.size = new Vector3(1.8f, 1.4f, 3.8f);

        // Badan Mobil
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "Body";
        Destroy(body.GetComponent<Collider>());
        body.transform.SetParent(car.transform, false);
        body.transform.localPosition = new Vector3(0f, 0.45f, 0f);
        body.transform.localScale = new Vector3(1.8f, 0.8f, 3.8f);
        SetSimpleColor(body, new Color(0.25f, 0.30f, 0.38f));

        // Kabin Atas
        GameObject cabin = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cabin.name = "Cabin";
        Destroy(cabin.GetComponent<Collider>());
        cabin.transform.SetParent(car.transform, false);
        cabin.transform.localPosition = new Vector3(0f, 1.1f, -0.2f);
        cabin.transform.localScale = new Vector3(1.45f, 0.55f, 1.8f);
        SetSimpleColor(cabin, new Color(0.18f, 0.22f, 0.28f));

        // 2 Lampu Belakang Merah Menyala (Menghadap Player)
        CreateTailLight(car.transform, new Vector3(-0.62f, 0.52f, -1.92f));
        CreateTailLight(car.transform, new Vector3(0.62f, 0.52f, -1.92f));

        return car;
    }

    private void CreateTailLight(Transform parent, Vector3 localPos)
    {
        GameObject lightBox = GameObject.CreatePrimitive(PrimitiveType.Cube);
        lightBox.name = "TailLight";
        Destroy(lightBox.GetComponent<Collider>());
        lightBox.transform.SetParent(parent, false);
        lightBox.transform.localPosition = localPos;
        lightBox.transform.localScale = new Vector3(0.32f, 0.16f, 0.08f);

        Renderer rend = lightBox.GetComponent<Renderer>();
        if (rend != null)
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            Material mat = new Material(urpLit != null ? urpLit : Shader.Find("Standard"));
            mat.color = Color.red;
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", Color.red * 3.5f);
            rend.sharedMaterial = mat;
        }
    }

    private void SetSimpleColor(GameObject obj, Color color)
    {
        Renderer rend = obj.GetComponent<Renderer>();
        if (rend != null)
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            Material mat = new Material(urpLit != null ? urpLit : Shader.Find("Standard"));
            mat.color = color;
            rend.sharedMaterial = mat;
        }
    }

    private void EnsureObstacleSetup(GameObject obj)
    {
        if (!obj.CompareTag("Obstacle"))
            obj.tag = "Obstacle";

        Collider[] cols = obj.GetComponentsInChildren<Collider>(true);
        if (cols == null || cols.Length == 0)
        {
            BoxCollider box = obj.AddComponent<BoxCollider>();
            box.isTrigger = true;
        }
        else
        {
            for (int i = 0; i < cols.Length; i++)
            {
                cols[i].isTrigger = true;
            }
        }
    }

    private void AlignBottomToGround(GameObject obj, float groundY)
    {
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        float correction = groundY - bounds.min.y;
        obj.transform.position += Vector3.up * correction;
    }

    public static void StopSpawning()
    {
        spawningEnabled = false;
    }
}
