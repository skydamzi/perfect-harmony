using System;

[Serializable]
public class NoteData
{
    public int beatNumber;
    public int lane; // Corresponds to NoteLane enum value
    public float spawnTime;

    public NoteData(int beatNumber, int lane, float spawnTime)
    {
        this.beatNumber = beatNumber;
        this.lane = lane;
        this.spawnTime = spawnTime;
    }
}