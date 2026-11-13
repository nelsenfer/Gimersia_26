
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

    [Header("Ground Detection")]
    [SerializeField] private Collider groundSurface;
    [SerializeField] private LayerMask groundLayers;
    [SerializeField] private float raycastStartHeight = 50f;
    [SerializeField] private float raycastDistance = 200f;

    [Header("Ground Alignment")]
    [SerializeField] private bool alignTreeToGround = true;
    [SerializeField] private float groundOffset = 0f;

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
            nextSpawnTime = Time.time;
        else
            ScheduleNextSpawn();
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
        float min = Mathf.Min(minSpawnInterval, maxSpawnInterval);
        float max = Mathf.Max(minSpawnInterval, maxSpawnInterval);

        nextSpawnTime = Time.time + Random.Range(min, max);
    }

    private void SpawnTree()
    {
        List<Transform> availablePoints = new();

        // Find available spawn points
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
                availablePoints.Add(point);
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

        // Detect ground before spawning
        float groundY = selectedPoint.position.y;

        if (alignTreeToGround)
        {
            if (!TryGetGroundHeight(
                selectedPoint.position,
                out groundY))
            {
                Debug.LogWarning(
                    "Ground tidak ditemukan pada " +
                    selectedPoint.name
                );

                return;
            }
        }

        // Preserve prefab rotation
        Quaternion spawnYaw = Quaternion.Euler(
            0f,
            selectedPoint.eulerAngles.y,
            0f
        );

        Quaternion prefabOriginalRotation =
            selectedPrefab.transform.rotation;

        Quaternion spawnRotation =
            spawnYaw * prefabOriginalRotation;

        // Determine spawn position
        Vector3 spawnPosition = new Vector3(
            selectedPoint.position.x,
            groundY,
            selectedPoint.position.z
        );

        // Instantiate tree
        GameObject newTree = Instantiate(
            selectedPrefab,
            spawnPosition,
            spawnRotation
        );

        // Add EnvironmentMover first so Rigidbody exists
        EnvironmentMover mover =
            newTree.GetComponent<EnvironmentMover>();

        if (mover == null)
        {
            mover = newTree.AddComponent<EnvironmentMover>();
        }

        // Keep tree physics stable
        Rigidbody treeRb =
            newTree.GetComponent<Rigidbody>();

        if (treeRb != null)
        {
            treeRb.isKinematic = true;
            treeRb.useGravity = false;
        }

        // Align tree to actual ground
        if (alignTreeToGround)
        {
            AlignTreeToGround(newTree, groundY);
        }

        // Determine movement direction
        Vector3 direction = moveOppositePlayerForward
            ? -playerCar.forward
            : playerCar.forward;

        direction.y = 0f;
        direction.Normalize();

        // Initialize tree movement
        mover.Initialize(
            direction,
            moveSpeed,
            despawnDistance
        );

        activeTrees.Add(newTree);

        Debug.Log(
            "Tree spawned: " + selectedPrefab.name +
            " | Spawn: " + selectedPoint.name +
            " | Ground Y: " + groundY
        );
    }

    // Detect actual ground below spawn point
    private bool TryGetGroundHeight(
        Vector3 spawnPosition,
        out float groundY)
    {
        Vector3 rayOrigin =
            spawnPosition +
            Vector3.up * raycastStartHeight;

        Ray ray = new Ray(rayOrigin, Vector3.down);

        RaycastHit hit;

        bool foundGround;

        // Prefer explicitly assigned Road Collider
        if (groundSurface != null)
        {
            foundGround = groundSurface.Raycast(
                ray,
                out hit,
                raycastDistance
            );
        }
        else
        {
            foundGround = Physics.Raycast(
                ray,
                out hit,
                raycastDistance,
                groundLayers,
                QueryTriggerInteraction.Ignore
            );
        }

        if (foundGround)
        {
            groundY = hit.point.y;

            Debug.Log(
                "Ground detected: " + hit.collider.name +
                " | Height: " + groundY
            );

            Debug.DrawRay(
                rayOrigin,
                Vector3.down * raycastDistance,
                Color.green,
                2f
            );

            return true;
        }

        groundY = spawnPosition.y;

        Debug.LogWarning(
            "Ground tidak ditemukan di " +
            spawnPosition
        );

        return false;
    }

    // Align lowest visual point of the tree to ground
    private void AlignTreeToGround(
        GameObject tree,
        float groundY)
    {
        Renderer[] renderers =
            tree.GetComponentsInChildren<Renderer>(true);

        bool rendererFound = false;

        Bounds combinedBounds = new Bounds();

        foreach (Renderer renderer in renderers)
        {
            if (!renderer.enabled ||
                !renderer.gameObject.activeInHierarchy)
                continue;

            if (!rendererFound)
            {
                combinedBounds = renderer.bounds;
                rendererFound = true;
            }
            else
            {
                combinedBounds.Encapsulate(renderer.bounds);
            }
        }

        if (!rendererFound)
        {
            Debug.LogWarning(
                "Renderer tidak ditemukan pada " + tree.name
            );

            return;
        }

        // Calculate the tree's lowest visual point
        float lowestPoint = combinedBounds.min.y;

        // Calculate vertical correction
        float correction =
            groundY - lowestPoint + groundOffset;

        // Apply corrected position
        Vector3 correctedPosition =
            tree.transform.position +
            Vector3.up * correction;

        tree.transform.position = correctedPosition;

        // Synchronize Rigidbody with Transform
        Rigidbody rb = tree.GetComponent<Rigidbody>();

        if (rb != null)
        {
            rb.position = correctedPosition;
            rb.rotation = tree.transform.rotation;
        }

        // Synchronize Unity physics
        Physics.SyncTransforms();

        Debug.Log(
            "TREE ALIGNMENT: " + tree.name +
            " | Ground Y: " + groundY.ToString("F3") +
            " | Bottom Before: " + lowestPoint.ToString("F3") +
            " | Correction: " + correction.ToString("F3") +
            " | Bottom After: " +
            (lowestPoint + correction).ToString("F3")
        );
    }

    public static void StopSpawning()
    {
        spawningEnabled = false;
    }
}
