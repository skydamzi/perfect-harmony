using System;

[Serializable]
public class NoteHitData
{
    public int lane;
    public TimingResult timingResult;
    public float hitTime;

    public NoteHitData(int lane, TimingResult result, float time)
    {
        this.lane = lane;
        this.timingResult = result;
        this.hitTime = time;
    }
}