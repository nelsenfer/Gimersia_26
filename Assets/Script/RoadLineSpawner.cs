
using System.Collections.Generic;
using UnityEngine;

public class RoadLineSpawner : MonoBehaviour
{
    [Header("Player Reference")]
    [SerializeField] private Transform playerCar;

    [Header("Road Line Prefab")]
    [SerializeField] private GameObject linePrefab;

    [Header("Prefab Rotation")]
    [SerializeField] private Vector3 prefabRotationOffset =
        new Vector3(90f, 0f, 0f);

    [Header("Road Position")]
    [SerializeField] private float roadCenterX = -1.63f;
    [SerializeField] private float lineHeight = 0.0725f;

    [Header("Line Pattern")]
    [SerializeField] private int lineCount = 20;
    [SerializeField] private float lineSpacing = 5f;
    [SerializeField] private float startDistance = 8f;

    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 12f;
    [SerializeField] private float recycleBehindDistance = 25f;
    [SerializeField] private bool moveOppositePlayerForward = true;

    [Header("Roadside Conveyor Loop (Gedung, Lampu Jalan & Pohon)")]
    [Tooltip("Jika aktif, seluruh gedung, lampu jalan, dan pohon pinggir jalan ikut bergerak secepat garis jalan & di-recycle di balik kabut")]
    [SerializeField] private bool enableRoadsideConveyor = true;
    [Tooltip("Prefab Gedung (Otomatis dipakai jika objek gedung di Hierarchy dihapus)")]
    [SerializeField] private GameObject buildingPrefab;
    [Tooltip("Material alternatif Gedung (untuk variasi warna gedung tinggi/rendah)")]
    [SerializeField] private Material buildingAltMaterial;
    [Tooltip("Prefab Lampu Jalan (Otomatis dipakai jika objek Lampu jalan di Hierarchy dihapus)")]
    [SerializeField] private GameObject streetLampPrefab;
    [Tooltip("Prefab Pohon (Otomatis dipakai jika objek pohon 1 di Hierarchy dihapus)")]
    [SerializeField] private GameObject treePrefab;
    [Tooltip("Jarak ke depan (meter) untuk recycle gedung, lampu jalan, dan pohon (Default 92m: tepat di dalam kabut tebal 105m sehingga tidak terlihat muncul mendadak tapi jumlah objek 50% lebih ringan!)")]
    [SerializeField] private float roadsideMaxDistanceAhead = 92f;
    [Tooltip("Jarak di belakang mobil (meter) sebelum objek pinggir jalan di-recycle ke depan (Default 12m, sudah di belakang kamera)")]
    [SerializeField] private float roadsideRecycleBehindDistance = 12f;
    [Tooltip("Jarak maksimal (meter) di mana lampu jalan menyalakan komponen Light-nya (Default 42m, hanya 2 lampu terdekat yang menyala agar FPS tetap tinggi!)")]
    [SerializeField] private float maxActiveStreetLampLightDistance = 42f;
    [Tooltip("Jarak antar gedung dalam 1 sisi jalan (meter). Default 3.1m agar gedung rapat dempet tanpa celah hitam!")]
    [SerializeField] private float buildingRowSpacing = 3.1f;
    [Tooltip("Jarak zig-zag antar lampu jalan kiri-kanan (meter)")]
    [SerializeField] private float streetLampZigZagStep = 24f;
    [Tooltip("Jarak zig-zag antar pohon kiri-kanan (meter)")]
    [SerializeField] private float treeZigZagStep = 22f;

    private class RoadsidePoolGroup
    {
        public readonly List<Transform> items = new List<Transform>();
        public readonly List<Light> itemLights = new List<Light>();
        public float stepSpacing;
        public bool hasLights;
        public bool randomizeSpacingAndScale;
        public float baseLeftX;
        public float baseRightX;
        public Vector3 baseTemplateScale;
    }

    private readonly List<RoadsidePoolGroup> roadsideGroups = new List<RoadsidePoolGroup>();
    private Transform roadsideContainer;

    private Transform[] roadLines;

    private Vector3 moveDirection;
    private Vector3 spawnDirection;

    private static bool linesMoving = true;

    public Transform PlayerCar => playerCar;
    public float MoveSpeed => moveSpeed;
    public float RoadCenterX => roadCenterX;
    public float RecycleBehindDistance => recycleBehindDistance;
    public bool MoveOppositePlayerForward => moveOppositePlayerForward;
    public static bool IsLinesMoving => linesMoving;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState()
    {
        linesMoving = true;
    }

    private void Awake()
    {
        linesMoving = true;
    }

    private void Start()
    {
        if (playerCar == null || linePrefab == null)
        {
            Debug.LogError(
                "RoadLineSpawner: Player Car atau Line Prefab belum diisi!"
            );

            enabled = false;
            return;
        }

        lineCount = Mathf.Max(2, lineCount);
        lineSpacing = Mathf.Max(0.1f, lineSpacing);

        // Determine movement direction
        moveDirection = moveOppositePlayerForward
            ? -playerCar.forward
            : playerCar.forward;

        moveDirection.y = 0f;

        if (moveDirection.sqrMagnitude < 0.001f)
        {
            Debug.LogError("Arah pergerakan Road Line tidak valid!");
            enabled = false;
            return;
        }

        moveDirection.Normalize();
        spawnDirection = -moveDirection;

        roadLines = new Transform[lineCount];

        // Calculate road line rotation
        Quaternion baseRotation = Quaternion.LookRotation(
            moveDirection,
            Vector3.up
        );

        // Apply artist prefab rotation correction
        Quaternion rotationOffset =
            Quaternion.Euler(prefabRotationOffset);

        Quaternion lineRotation =
            baseRotation * rotationOffset;

        // Create initial road lines
        for (int i = 0; i < lineCount; i++)
        {
            Vector3 spawnPosition =
                playerCar.position +
                spawnDirection * (startDistance + i * lineSpacing);

            spawnPosition.x = roadCenterX;
            spawnPosition.y = lineHeight;

            GameObject newLine = Instantiate(
                linePrefab,
                spawnPosition,
                lineRotation,
                transform
            );

            newLine.name = "RoadLine_" + (i + 1).ToString("00");

            roadLines[i] = newLine.transform;
        }

        if (enableRoadsideConveyor)
        {
            InitializeRoadsideConveyor();
        }

        Debug.Log("Road Line Spawner berhasil dibuat!");
    }

    private void Update()
    {
        if (!linesMoving || roadLines == null)
            return;

        float movement = moveSpeed * Time.deltaTime;

        // Move all lines together
        foreach (Transform line in roadLines)
        {
            if (line == null)
                continue;

            line.position += moveDirection * movement;
        }

        // Find the furthest line ahead of the player
        float furthestDistance = 0f;

        foreach (Transform line in roadLines)
        {
            if (line == null)
                continue;

            float distanceAhead = Vector3.Dot(
                line.position - playerCar.position,
                spawnDirection
            );

            furthestDistance = Mathf.Max(
                furthestDistance,
                distanceAhead
            );
        }

        // Recycle lines after passing the player
        foreach (Transform line in roadLines)
        {
            if (line == null)
                continue;

            float distanceAhead = Vector3.Dot(
                line.position - playerCar.position,
                spawnDirection
            );

            if (distanceAhead < -recycleBehindDistance)
            {
                furthestDistance += lineSpacing;

                Vector3 newPosition =
                    playerCar.position +
                    spawnDirection * furthestDistance;

                newPosition.x = roadCenterX;
                newPosition.y = lineHeight;

                line.position = newPosition;
            }
        }

        // Move & recycle roadside environment (gedung, lampu jalan, pohon) at the exact same speed
        if (enableRoadsideConveyor && roadsideGroups.Count > 0)
        {
            UpdateRoadsideConveyor(movement);
        }
    }

    /// <summary>
    /// Merapikan objek gedung, lampu jalan, dan pohon yang ada di scene ke dalam 1 container rapi,
    /// menyusunnya memanjang hingga 175m ke depan (lebih jauh dari spawn mobil NPC 160m),
    /// serta membuat lampu jalan & pohon berpola zig-zag di sisi kiri dan kanan jalan.
    /// </summary>
    private void InitializeRoadsideConveyor()
    {
        roadsideGroups.Clear();

        GameObject containerObj = new GameObject("Roadside_Environment_Pool");
        containerObj.transform.SetParent(transform, false);
        roadsideContainer = containerObj.transform;

        List<Transform> leftBuildingTemplates = new List<Transform>();
        List<Transform> rightBuildingTemplates = new List<Transform>();
        Transform lampTemplate = null;
        Transform treeTemplate = null;

        GameObject[] rootObjects = gameObject.scene.GetRootGameObjects();
        for (int i = 0; i < rootObjects.Length; i++)
        {
            GameObject go = rootObjects[i];
            if (go == null || !go.activeInHierarchy || go == gameObject)
                continue;

            string lowerName = go.name.ToLowerInvariant();
            if (lowerName.StartsWith("gedung"))
            {
                if (go.transform.position.x < roadCenterX)
                    leftBuildingTemplates.Add(go.transform);
                else
                    rightBuildingTemplates.Add(go.transform);
            }
            else if (lowerName.StartsWith("lampu jalan"))
            {
                if (lampTemplate == null)
                    lampTemplate = go.transform;
                else
                    go.SetActive(false);
            }
            else if (lowerName.StartsWith("pohon"))
            {
                if (treeTemplate == null)
                    treeTemplate = go.transform;
                else
                    go.SetActive(false);
            }
        }

        // Jika user sudah menghapus objek gedung/lampu/pohon dari Hierarchy agar scene bersih,
        // buat template otomatis dari Prefab yang terhubung di RoadLineSpawner!
        if (leftBuildingTemplates.Count == 0 && buildingPrefab != null)
        {
            leftBuildingTemplates.Add(CreateBuildingFallbackTemplate("Gedung_Kiri_Tall", -6.476386f, 4.16f, Vector3.one, new Quaternion(0.000000015454312f, 0.7071067f, -0.00000001545431f, 0.7071068f), useAltMat: false));
            leftBuildingTemplates.Add(CreateBuildingFallbackTemplate("Gedung_Kiri_Short", -6.476386f, 2.88f, new Vector3(4.9362664f, 5.442112f, 1f), new Quaternion(0.000000015454312f, 0.7071067f, -0.00000001545431f, 0.7071068f), useAltMat: true));
        }

        if (rightBuildingTemplates.Count == 0 && buildingPrefab != null)
        {
            rightBuildingTemplates.Add(CreateBuildingFallbackTemplate("Gedung_Kanan_Tall", 3.15f, 4.16f, Vector3.one, new Quaternion(0.00000001545431f, 0.7071069f, 0.000000015454306f, -0.7071067f), useAltMat: false));
            rightBuildingTemplates.Add(CreateBuildingFallbackTemplate("Gedung_Kanan_Short", 3.15f, 2.88f, new Vector3(4.9362664f, 5.442112f, 1f), new Quaternion(0.00000001545431f, 0.7071069f, 0.000000015454306f, -0.7071067f), useAltMat: true));
        }

        if (lampTemplate == null && streetLampPrefab != null)
        {
            GameObject lampObj = Instantiate(
                streetLampPrefab,
                new Vector3(-5.1410403f, 0.17f, -7.15f),
                new Quaternion(-0.7071068f, 0f, 0f, 0.7071067f),
                roadsideContainer
            );
            lampObj.name = "LampuJalan_Template";

            // Tambahkan Spot Light pada lampu jalan jika prefab FBX aslinya belum punya Light
            if (lampObj.GetComponentInChildren<Light>(true) == null)
            {
                GameObject lightGo = new GameObject("Spot Light");
                lightGo.transform.SetParent(lampObj.transform, false);
                lightGo.transform.localPosition = new Vector3(11.7f, 0.3f, 44f);
                lightGo.transform.localRotation = Quaternion.identity;
                Light l = lightGo.AddComponent<Light>();
                l.type = LightType.Point;
                l.intensity = 3f;
                l.range = 10f;
                l.shadows = LightShadows.None;
            }
            lampTemplate = lampObj.transform;
        }

        if (treeTemplate == null && treePrefab != null)
        {
            GameObject treeObj = Instantiate(
                treePrefab,
                new Vector3(-6.14f, 2.09f, -2.0226288f),
                new Quaternion(-0.63676965f, -0.40141198f, -0.28186932f, 0.5949307f),
                roadsideContainer
            );
            treeObj.name = "Pohon_Template";
            treeTemplate = treeObj.transform;
        }

        // Urutkan gedung template berdasarkan jarak ke depan agar urutan variasi tingginya (Y = 2.88 / 4.16) tetap terjaga
        leftBuildingTemplates.Sort((a, b) =>
            Vector3.Dot(a.position - playerCar.position, spawnDirection)
                .CompareTo(Vector3.Dot(b.position - playerCar.position, spawnDirection)));
        rightBuildingTemplates.Sort((a, b) =>
            Vector3.Dot(a.position - playerCar.position, spawnDirection)
                .CompareTo(Vector3.Dot(b.position - playerCar.position, spawnDirection)));

        // 0. Pastikan Aspal & Trotoar memanjang penuh menembus kabut (>= 115m ke depan) agar kabut terlihat menyelimuti jalan & gedung
        ExtendStaticRoadAndSidewalkIntoFog(rootObjects);

        // 1. Barisan Gedung Sisi Kiri (sampai >= 92m ke depan)
        if (leftBuildingTemplates.Count > 0)
        {
            BuildBuildingSideGroup("Gedung_Kiri", leftBuildingTemplates);
        }

        // 2. Barisan Gedung Sisi Kanan (sampai >= 92m ke depan)
        if (rightBuildingTemplates.Count > 0)
        {
            BuildBuildingSideGroup("Gedung_Kanan", rightBuildingTemplates);
        }

        // 3. Barisan Lampu Jalan Zig-Zag Kiri & Kanan (jarak jauh ~38m agar hemat & realistis)
        if (lampTemplate != null)
        {
            BuildZigZagRoadsideGroup(
                "LampuJalan",
                lampTemplate,
                Mathf.Max(18f, streetLampZigZagStep),
                startAheadOffset: 10f,
                flipYawOnOppositeSide: true,
                randomizePlacement: false
            );
        }

        // 4. Barisan Pohon Random di Kiri & Kanan Jalan
        if (treeTemplate != null)
        {
            BuildZigZagRoadsideGroup(
                "Pohon",
                treeTemplate,
                Mathf.Max(10f, treeZigZagStep),
                startAheadOffset: 3f,
                flipYawOnOppositeSide: false,
                randomizePlacement: true
            );
        }
    }

    private void ExtendStaticRoadAndSidewalkIntoFog(GameObject[] rootObjects)
    {
        if (rootObjects == null) return;

        float requiredAheadDist = Mathf.Max(110f, roadsideMaxDistanceAhead + 20f);

        for (int i = 0; i < rootObjects.Length; i++)
        {
            GameObject go = rootObjects[i];
            if (go == null || !go.activeInHierarchy) continue;

            string lower = go.name.ToLowerInvariant();
            if (!lower.StartsWith("aspal") && !lower.StartsWith("trotoar"))
                continue;

            Renderer rend = go.GetComponentInChildren<Renderer>();
            if (rend == null) continue;

            Bounds b = rend.bounds;
            float segLenZ = b.size.z;
            if (segLenZ < 5f) continue;

            // Hitung seberapa jauh ujung depan aspal/trotoar ini dari posisi mobil pemain
            float frontEdgeZ = spawnDirection.z < 0f ? b.min.z : b.max.z;
            float currentReachAhead = Mathf.Abs(frontEdgeZ - playerCar.position.z);

            if (currentReachAhead >= requiredAheadDist)
                continue;

            // Sambungkan potongan aspal/trotoar statis ke depan sampai menembus kabut
            int extraSegmentsNeeded = Mathf.CeilToInt((requiredAheadDist - currentReachAhead) / segLenZ);
            extraSegmentsNeeded = Mathf.Clamp(extraSegmentsNeeded, 1, 4);

            for (int s = 1; s <= extraSegmentsNeeded; s++)
            {
                Vector3 extPos = go.transform.position + spawnDirection * (segLenZ * s);
                GameObject extClone = Instantiate(go, extPos, go.transform.rotation, roadsideContainer);
                extClone.name = $"{go.name}_FogExt_{s}";
                StripCollidersAndEnableGPUInstancing(extClone);

                RoadBoundsReporter reporter = extClone.GetComponent<RoadBoundsReporter>();
                if (reporter != null) Destroy(reporter);
            }
        }
    }

    private Transform CreateBuildingFallbackTemplate(
        string name,
        float xPos,
        float yPos,
        Vector3 localScale,
        Quaternion rotation,
        bool useAltMat)
    {
        GameObject obj = Instantiate(
            buildingPrefab,
            new Vector3(xPos, yPos, -10f),
            rotation,
            roadsideContainer
        );
        obj.name = name;
        obj.transform.localScale = localScale;

        if (useAltMat && buildingAltMaterial != null)
        {
            Renderer rend = obj.GetComponentInChildren<Renderer>(true);
            if (rend != null)
            {
                rend.sharedMaterial = buildingAltMaterial;
            }
        }

        return obj.transform;
    }

    private void StripCollidersAndEnableGPUInstancing(GameObject obj)
    {
        if (obj == null) return;

        // 1. Hapus semua Collider pada objek dekorasi pinggir jalan agar Physics engine tidak terbebani
        Collider[] cols = obj.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++)
        {
            if (cols[i] != null)
            {
                Destroy(cols[i]);
            }
        }

        // 2. Matikan shadow casting/receiving & aktifkan GPU Instancing pada material dekorasi pinggir jalan
        Renderer[] rends = obj.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < rends.Length; i++)
        {
            Renderer r = rends[i];
            if (r == null) continue;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            if (r.sharedMaterial != null)
            {
                r.sharedMaterial.enableInstancing = true;
            }
        }
    }

    private void BuildBuildingSideGroup(string groupLabel, List<Transform> templates)
    {
        // Ukur lebar Z asli mesh gedung agar gedung berjejer rapat (dempet) tanpa celah hitam di antaranya!
        float measuredDepthZ = 0f;
        for (int t = 0; t < templates.Count; t++)
        {
            if (templates[t] == null) continue;
            Renderer rend = templates[t].GetComponentInChildren<Renderer>();
            if (rend != null && rend.bounds.size.z > 0.5f)
            {
                measuredDepthZ = rend.bounds.size.z;
                break;
            }
        }

        // Gunakan jarak dempet (lebar gedung sebenarnya dikurangi 0.04m agar tidak ada celah hitam sama sekali)
        float step = measuredDepthZ > 0.5f
            ? Mathf.Max(1.5f, measuredDepthZ - 0.04f)
            : Mathf.Clamp(buildingRowSpacing, 2.0f, 3.5f);

        float maxDist = Mathf.Clamp(roadsideMaxDistanceAhead, 65f, 115f);
        float behindDist = Mathf.Max(8f, roadsideRecycleBehindDistance);
        int totalNeeded = Mathf.CeilToInt((maxDist + behindDist) / step) + 1;

        for (int t = 0; t < templates.Count; t++)
        {
            if (templates[t] != null)
            {
                StripCollidersAndEnableGPUInstancing(templates[t].gameObject);
            }
        }

        // Jika jumlah gedung di scene lebih banyak dari yang dibutuhkan, nonaktifkan sisanya agar hemat
        for (int extra = totalNeeded; extra < templates.Count; extra++)
        {
            if (templates[extra] != null)
            {
                templates[extra].gameObject.SetActive(false);
            }
        }

        RoadsidePoolGroup group = new RoadsidePoolGroup
        {
            stepSpacing = step,
            hasLights = false
        };

        float baseStartAhead = -behindDist + 2f;

        for (int i = 0; i < totalNeeded; i++)
        {
            Transform sourceTemplate = templates[i % templates.Count];
            Transform itemTf;

            if (i < templates.Count)
            {
                // Gunakan objek yang sudah ada di scene dan rapikan ke dalam container
                itemTf = sourceTemplate;
                itemTf.SetParent(roadsideContainer, true);
            }
            else
            {
                GameObject clone = Instantiate(
                    sourceTemplate.gameObject,
                    sourceTemplate.position,
                    sourceTemplate.rotation,
                    roadsideContainer
                );
                StripCollidersAndEnableGPUInstancing(clone);
                itemTf = clone.transform;
                itemTf.localScale = sourceTemplate.lossyScale;
            }

            itemTf.name = $"{groupLabel}_{(i + 1):00}";

            float distAhead = baseStartAhead + i * step;
            Vector3 targetPos = playerCar.position + spawnDirection * distAhead;
            targetPos.x = sourceTemplate.position.x;
            targetPos.y = sourceTemplate.position.y;
            itemTf.position = targetPos;

            group.items.Add(itemTf);
        }

        roadsideGroups.Add(group);
    }

    private void BuildZigZagRoadsideGroup(
        string groupLabel,
        Transform template,
        float zigZagStep,
        float startAheadOffset,
        bool flipYawOnOppositeSide,
        bool randomizePlacement)
    {
        float maxDist = Mathf.Clamp(roadsideMaxDistanceAhead, 65f, 115f);
        float behindDist = Mathf.Max(8f, roadsideRecycleBehindDistance);
        int totalNeeded = Mathf.CeilToInt((maxDist + behindDist) / zigZagStep) + 1;

        StripCollidersAndEnableGPUInstancing(template.gameObject);

        float templateOffsetFromCenter = template.position.x - roadCenterX;
        float leftX = roadCenterX - Mathf.Abs(templateOffsetFromCenter);
        float rightX = roadCenterX + Mathf.Abs(templateOffsetFromCenter);
        float baseY = template.position.y;
        Vector3 baseScale = template.lossyScale;
        Quaternion baseRot = template.rotation;
        Quaternion oppositeRot = flipYawOnOppositeSide
            ? Quaternion.Euler(0f, 180f, 0f) * baseRot
            : baseRot;

        bool templateIsLeft = templateOffsetFromCenter <= 0f;

        // Matikan shadow pada Spot Light lampu jalan kloningan agar ringan dan tidak berat di GPU
        Light[] templateLights = template.GetComponentsInChildren<Light>(true);
        bool groupHasLights = templateLights != null && templateLights.Length > 0;
        for (int l = 0; l < templateLights.Length; l++)
        {
            if (templateLights[l] != null)
            {
                templateLights[l].shadows = LightShadows.None;
            }
        }

        RoadsidePoolGroup group = new RoadsidePoolGroup
        {
            stepSpacing = zigZagStep,
            hasLights = groupHasLights,
            randomizeSpacingAndScale = randomizePlacement,
            baseLeftX = leftX,
            baseRightX = rightX,
            baseTemplateScale = baseScale
        };

        float currentDistAhead = -behindDist + startAheadOffset;

        for (int i = 0; i < totalNeeded; i++)
        {
            Transform itemTf;
            if (i == 0)
            {
                itemTf = template;
                itemTf.SetParent(roadsideContainer, true);
            }
            else
            {
                GameObject clone = Instantiate(
                    template.gameObject,
                    template.position,
                    template.rotation,
                    roadsideContainer
                );
                StripCollidersAndEnableGPUInstancing(clone);
                itemTf = clone.transform;
                itemTf.localScale = baseScale;
            }

            // Untuk pohon (randomizePlacement = true), acak sisi kiri/kanan, jarak Z, offset X, serta skala tinggi-rendahnya!
            bool placeOnLeft = randomizePlacement
                ? (Random.value > 0.5f)
                : (i % 2 == 0);
            itemTf.name = $"{groupLabel}_{(placeOnLeft ? "L" : "R")}_{(i + 1):00}";

            float sideX = placeOnLeft ? leftX : rightX;
            if (randomizePlacement)
            {
                // Jangan geser X pohon ke arah gedung (outward) agar ranting pohon tidak menabrak tembok gedung sesuai posisi X yang sudah diatur user!
                float randomScaleFactor = Random.Range(0.86f, 1.06f);
                itemTf.localScale = baseScale * randomScaleFactor;
            }

            Vector3 targetPos = playerCar.position + spawnDirection * currentDistAhead;
            targetPos.x = sideX;
            targetPos.y = baseY;
            itemTf.position = targetPos;

            bool isSameSideAsTemplate = (placeOnLeft == templateIsLeft);
            itemTf.rotation = isSameSideAsTemplate ? baseRot : oppositeRot;

            group.items.Add(itemTf);
            if (groupHasLights)
            {
                Light itemLight = itemTf.GetComponentInChildren<Light>(true);
                if (itemLight != null)
                {
                    itemLight.enabled = (currentDistAhead >= -4f && currentDistAhead <= maxActiveStreetLampLightDistance);
                }
                group.itemLights.Add(itemLight);
            }

            float stepAdvance = randomizePlacement
                ? zigZagStep * Random.Range(0.55f, 1.45f)
                : zigZagStep;
            currentDistAhead += stepAdvance;
        }

        roadsideGroups.Add(group);
    }

    private void UpdateRoadsideConveyor(float movement)
    {
        float behindDist = Mathf.Max(8f, roadsideRecycleBehindDistance);
        float maxDist = Mathf.Clamp(roadsideMaxDistanceAhead, 65f, 115f);

        for (int g = 0; g < roadsideGroups.Count; g++)
        {
            RoadsidePoolGroup group = roadsideGroups[g];
            if (group == null || group.items.Count == 0)
                continue;

            float furthestAhead = -99999f;

            for (int i = 0; i < group.items.Count; i++)
            {
                Transform item = group.items[i];
                if (item == null) continue;

                item.position += moveDirection * movement;

                float distAhead = Vector3.Dot(
                    item.position - playerCar.position,
                    spawnDirection
                );
                if (distAhead > furthestAhead)
                {
                    furthestAhead = distAhead;
                }
            }

            for (int i = 0; i < group.items.Count; i++)
            {
                Transform item = group.items[i];
                if (item == null) continue;

                float distAhead = Vector3.Dot(
                    item.position - playerCar.position,
                    spawnDirection
                );

                if (distAhead < -behindDist)
                {
                    float advanceStep = group.randomizeSpacingAndScale
                        ? group.stepSpacing * Random.Range(0.55f, 1.45f)
                        : group.stepSpacing;
                    furthestAhead = Mathf.Max(furthestAhead + advanceStep, maxDist);
                    distAhead = furthestAhead;

                    Vector3 recycledPos = playerCar.position + spawnDirection * furthestAhead;
                    if (group.randomizeSpacingAndScale)
                    {
                        bool placeLeft = Random.value > 0.5f;
                        // Gunakan posisi X trotoar persis tanpa menggeser ke arah gedung agar ranting pohon tidak menabrak gedung
                        recycledPos.x = placeLeft ? group.baseLeftX : group.baseRightX;
                        item.localScale = group.baseTemplateScale * Random.Range(0.86f, 1.06f);
                    }
                    else
                    {
                        recycledPos.x = item.position.x;
                    }
                    recycledPos.y = item.position.y;
                    item.position = recycledPos;
                }

                // OPTIMASI GPU PENTING: Nyalakan komponen Light lampu jalan HANYA saat dekat mobil (0..42m)
                if (group.hasLights && i < group.itemLights.Count)
                {
                    Light l = group.itemLights[i];
                    if (l != null)
                    {
                        bool shouldLightBeOn = distAhead >= -4f && distAhead <= maxActiveStreetLampLightDistance;
                        if (l.enabled != shouldLightBeOn)
                        {
                            l.enabled = shouldLightBeOn;
                        }
                    }
                }
            }
        }
    }

    public static void StopLines()
    {
        linesMoving = false;
    }

    public static void ResetLines()
    {
        linesMoving = true;
    }
}
