using UnityEngine;

public class DefaultNoteSetup : MonoBehaviour
{
    void Start()
    {
        NoteSpawner noteSpawner = FindFirstObjectByType<NoteSpawner>();
        if (noteSpawner != null)
        {
            // Clear any existing spawn events
            noteSpawner.ClearSpawnEvents();

            // Add some default notes for testing (every beat for 4 lanes)
            int beats = 16; // 4 measures of 4/4 time
            for (int beat = 0; beat < beats; beat++)
            {
                // Alternate between lanes to create a simple pattern
                NoteLane lane = (NoteLane)(beat % 4);
                noteSpawner.AddSpawnEvent(beat, lane);
            }
        }
        else
        {
            Debug.LogError("NoteSpawner not found in scene!");
        }
    }
}