using System;

[Serializable]
public class PlayerInputData
{
    public int lane; // Corresponds to NoteLane enum value
    public float inputTime;
    
    public PlayerInputData(int lane, float inputTime)
    {
        this.lane = lane;
        this.inputTime = inputTime;
    }
}