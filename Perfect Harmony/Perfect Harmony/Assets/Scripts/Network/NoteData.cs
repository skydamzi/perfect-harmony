using System;

[Serializable]
public class NoteData
{
    public float beatNumber;
    public int lane; // Corresponds to NoteLane enum value
    public float spawnTime;

    public NoteData(float beatNumber, int lane, float spawnTime)
    {
        this.beatNumber = beatNumber;
        this.lane = lane;
        this.spawnTime = spawnTime;
    }
}