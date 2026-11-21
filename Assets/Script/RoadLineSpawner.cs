
using UnityEngine;

public class RoadLineSpawner : MonoBehaviour
{
    [Header("Player Reference")]
    [SerializeField] private Transform playerCar;

    [Header("Road Line Prefab")]
    [SerializeField] private GameObject linePrefab;

    [Header("Road Position")]
    [SerializeField] private float roadCenterX = -0.14f;
    [SerializeField] private float lineHeight = 0.0125f;

    [Header("Line Pattern")]
    [SerializeField] private int lineCount = 20;
    [SerializeField] private float lineSpacing = 5f;
    [SerializeField] private float startDistance = 8f;

    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 12f;
    [SerializeField] private float recycleBehindDistance = 25f;
    [SerializeField] private bool moveOppositePlayerForward = true;

    private Transform[] roadLines;

    private Vector3 moveDirection;
    private Vector3 spawnDirection;

    private static bool linesMoving = true;

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

        Quaternion lineRotation = Quaternion.LookRotation(
            moveDirection,
            Vector3.up
        );

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

        // Recycle only after a line has passed the player
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
                // Place line after the furthest line
                furthestDistance += lineSpacing;

                Vector3 newPosition =
                    playerCar.position +
                    spawnDirection * furthestDistance;

                newPosition.x = roadCenterX;
                newPosition.y = lineHeight;

                line.position = newPosition;
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
