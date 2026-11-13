
using System.Collections.Generic;
using UnityEngine;

public class TreeSpawner : MonoBehaviour
{
    [Header("Player Reference")]
    [SerializeField] private Transform playerCar;

    [Header("Tree Prefabs")]
    [SerializeField] private GameObject[] treePrefabs;

    [Header("Spawn Points")]
    [SerializeField] private Transform[] spawnPoints;

    [Header("Spawn Settings")]
    [SerializeField] private float minSpawnInterval = 1.5f;
    [SerializeField] private float maxSpawnInterval = 3f;
    [SerializeField] private int maxActiveTrees = 8;
    [SerializeField] private float minSpawnSeparation = 8f;
    [SerializeField] private bool spawnImmediately = true;

    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 12f;
    [SerializeField] private float despawnDistance = 100f;
    [SerializeField] private bool moveOppositePlayerForward = false;

    [Header("Ground Alignment")]
    [SerializeField] private bool alignTreeToGround = true;
    [SerializeField] private float groundOffset = 0.02f;

    [Header("Ground Detection")]
    [SerializeField] private LayerMask groundLayers;
    [SerializeField] private float raycastStartHeight = 50f;
    [SerializeField] private float raycastDistance = 200f;

    private readonly List<GameObject> activeTrees = new();

    private float nextSpawnTime;

    private static bool spawningEnabled = true;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSpawnerState()
    {
        spawningEnabled = true;
    }

    private void Start()
    {
        if (playerCar == null ||
            treePrefabs == null ||
            treePrefabs.Length == 0 ||
            spawnPoints == null ||
            spawnPoints.Length == 0)
        {
            Debug.LogError(
                "TreeSpawner belum memiliki referensi lengkap!"
            );

            enabled = false;
            return;
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

        activeTrees.RemoveAll(tree => tree == null);

        if (Time.time < nextSpawnTime)
            return;

        ScheduleNextSpawn();

        if (activeTrees.Count >= maxActiveTrees)
            return;

        SpawnTree();
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

        nextSpawnTime = Time.time + Random.Range(min, max);
    }

    private void SpawnTree()
    {
        List<Transform> availablePoints = new();

        foreach (Transform point in spawnPoints)
        {
            if (point == null)
                continue;

            bool isBlocked = false;

            foreach (GameObject activeTree in activeTrees)
            {
                Vector2 pointPosition = new Vector2(
                    point.position.x,
                    point.position.z
                );

                Vector2 treePosition = new Vector2(
                    activeTree.transform.position.x,
                    activeTree.transform.position.z
                );

                if (Vector2.Distance(
                    pointPosition,
                    treePosition
                ) < minSpawnSeparation)
                {
                    isBlocked = true;
                    break;
                }
            }

            if (!isBlocked)
            {
                availablePoints.Add(point);
            }
        }

        if (availablePoints.Count == 0)
        {
            Debug.Log("Belum ada spawn point yang tersedia.");
            return;
        }

        // Select random spawn point
        Transform selectedPoint =
            availablePoints[
                Random.Range(0, availablePoints.Count)
            ];

        // Select random tree prefab
        GameObject selectedPrefab =
            treePrefabs[
                Random.Range(0, treePrefabs.Length)
            ];

        // Get Y rotation from spawn point
        Quaternion spawnYaw = Quaternion.Euler(
            0f,
            selectedPoint.eulerAngles.y,
            0f
        );

        // Preserve original prefab rotation
        Quaternion prefabOriginalRotation =
            selectedPrefab.transform.rotation;

        // Combine original prefab rotation with spawn rotation
        Quaternion spawnRotation =
            spawnYaw * prefabOriginalRotation;

        // Spawn tree
        GameObject newTree = Instantiate(
            selectedPrefab,
            selectedPoint.position,
            spawnRotation
        );

        // Detect actual ground below spawn point
        if (alignTreeToGround)
        {
            float groundY = GetGroundHeight(
                selectedPoint.position
            );

            AlignTreeToGround(
                newTree,
                groundY
            );
        }

        // Determine movement direction
        Vector3 direction = moveOppositePlayerForward
            ? -playerCar.forward
            : playerCar.forward;

        direction.y = 0f;
        direction.Normalize();

        // Get or add EnvironmentMover
        EnvironmentMover mover =
            newTree.GetComponent<EnvironmentMover>();

        if (mover == null)
        {
            mover = newTree.AddComponent<EnvironmentMover>();
        }

        mover.Initialize(
            direction,
            moveSpeed,
            despawnDistance
        );

        activeTrees.Add(newTree);

        Debug.Log(
            "Tree spawned: " + selectedPrefab.name +
            " at " + selectedPoint.name
        );
    }

    // Detect ground using Raycast
    private float GetGroundHeight(Vector3 spawnPosition)
    {
        Vector3 rayOrigin =
            spawnPosition + Vector3.up * raycastStartHeight;

        if (Physics.Raycast(
            rayOrigin,
            Vector3.down,
            out RaycastHit hit,
            raycastDistance,
            groundLayers,
            QueryTriggerInteraction.Ignore))
        {
            Debug.Log(
                "Ground detected: " + hit.collider.name +
                " | Height: " + hit.point.y
            );

            return hit.point.y;
        }

        Debug.LogWarning(
            "Ground tidak ditemukan di bawah Spawn Point: " +
            spawnPosition +
            ". Periksa Collider dan Ground Layer."
        );

        return spawnPosition.y;
    }

    // Align bottom of tree with detected ground
    private void AlignTreeToGround(
        GameObject tree,
        float groundY)
    {
        Renderer[] renderers =
            tree.GetComponentsInChildren<Renderer>(true);

        if (renderers.Length == 0)
        {
            Debug.LogWarning(
                "Renderer tidak ditemukan pada " + tree.name
            );

            return;
        }

        float lowestPoint = float.MaxValue;
        bool rendererFound = false;

        foreach (Renderer renderer in renderers)
        {
            if (!renderer.enabled)
                continue;

            lowestPoint = Mathf.Min(
                lowestPoint,
                renderer.bounds.min.y
            );

            rendererFound = true;
        }

        if (!rendererFound)
            return;

        // Calculate vertical correction
        float offsetY =
            groundY - lowestPoint + groundOffset;

        // Move tree to ground level
        tree.transform.position +=
            Vector3.up * offsetY;
    }

    public static void StopSpawning()
    {
        spawningEnabled = false;
    }
}
