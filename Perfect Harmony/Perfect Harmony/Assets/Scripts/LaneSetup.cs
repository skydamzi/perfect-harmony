using UnityEngine;

public class LaneSetup : MonoBehaviour
{
    [Header("Lane Configuration")]
    public Transform[] spawnPositions;  // Where notes spawn for each lane
    public Transform[] targetPositions; // Where notes should be hit for each lane
    public GameObject[] laneVisuals;    // Visual indicators for each lane
    
    [Header("Lane Settings")]
    public float laneSpacing = 2.0f;    // Distance between lanes
    public float spawnHeight = 5.0f;    // Height where notes spawn
    public float targetHeight = -3.0f;  // Height where notes should be hit
    
    void Start()
    {
        SetupLanes();
    }
    
    void SetupLanes()
    {
        // Calculate starting x position to center the lanes (Only used for NEW objects)
        int laneCount = 8; // Fixed to 8 for now based on previous context
        float startX = -(laneCount - 1) * laneSpacing / 2f;
        
        // Ensure arrays are large enough
        if (spawnPositions == null || spawnPositions.Length < laneCount) spawnPositions = new Transform[laneCount];
        if (targetPositions == null || targetPositions.Length < laneCount) targetPositions = new Transform[laneCount];

        for (int i = 0; i < laneCount; i++)
        {
            float xPosition = startX + i * laneSpacing;
            
            // Create spawn position ONLY if it doesn't exist
            if (spawnPositions[i] == null)
            {
                // Create a new transform if one doesn't exist
                GameObject spawnPosObj = new GameObject($"SpawnPos_Lane{i}");
                spawnPosObj.transform.SetParent(transform);
                spawnPosObj.transform.position = new Vector3(xPosition, spawnHeight, 0);
                spawnPositions[i] = spawnPosObj.transform;
            }
            // If it exists, WE DO NOT TOUCH IT. User controls position.
            
            // Create target position ONLY if it doesn't exist
            if (targetPositions[i] == null)
            {
                // Create a new transform if one doesn't exist
                GameObject targetPosObj = new GameObject($"TargetPos_Lane{i}");
                targetPosObj.transform.SetParent(transform);
                targetPosObj.transform.position = new Vector3(xPosition, targetHeight, 0);
                targetPositions[i] = targetPosObj.transform;
            }
            // If it exists, WE DO NOT TOUCH IT. User controls position.
            
            // Set up lane visuals
            if (laneVisuals != null && i < laneVisuals.Length && laneVisuals[i] != null)
            {
                // Optional: Only set if you want procedural visuals, otherwise comment this out too
                // laneVisuals[i].transform.position = new Vector3(xPosition, (spawnHeight + targetHeight) / 2f, 0);
            }
        }
    }
}