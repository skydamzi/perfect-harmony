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
        // Calculate starting x position to center the lanes
        int laneCount = System.Enum.GetValues(typeof(NoteLane)).Length;
        float startX = -(laneCount - 1) * laneSpacing / 2f;
        
        for (int i = 0; i < laneCount; i++)
        {
            float xPosition = startX + i * laneSpacing;
            
            // Create spawn position
            if (spawnPositions != null && i < spawnPositions.Length && spawnPositions[i] != null)
            {
                spawnPositions[i].position = new Vector3(xPosition, spawnHeight, 0);
            }
            else if (spawnPositions != null && i < spawnPositions.Length)
            {
                // Create a new transform if one doesn't exist
                GameObject spawnPosObj = new GameObject($"SpawnPos_Lane{i}");
                spawnPosObj.transform.SetParent(transform);
                spawnPosObj.transform.position = new Vector3(xPosition, spawnHeight, 0);
                spawnPositions[i] = spawnPosObj.transform;
            }
            
            // Create target position
            if (targetPositions != null && i < targetPositions.Length && targetPositions[i] != null)
            {
                targetPositions[i].position = new Vector3(xPosition, targetHeight, 0);
            }
            else if (targetPositions != null && i < targetPositions.Length)
            {
                // Create a new transform if one doesn't exist
                GameObject targetPosObj = new GameObject($"TargetPos_Lane{i}");
                targetPosObj.transform.SetParent(transform);
                targetPosObj.transform.position = new Vector3(xPosition, targetHeight, 0);
                targetPositions[i] = targetPosObj.transform;
            }
            
            // Set up lane visuals
            if (laneVisuals != null && i < laneVisuals.Length && laneVisuals[i] != null)
            {
                laneVisuals[i].transform.position = new Vector3(xPosition, (spawnHeight + targetHeight) / 2f, 0);
                // Optionally set the height of the visual to span from spawn to target
                // This assumes the visual is something like a plane or quad
            }
        }
    }
}