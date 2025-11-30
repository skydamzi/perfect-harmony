using System;

[Serializable]
public class PlayerScoreData
{
    public int score;
    public int combo;
    public TimingResult timingResult;
    
    public PlayerScoreData(int score, int combo, TimingResult timingResult)
    {
        this.score = score;
        this.combo = combo;
        this.timingResult = timingResult;
    }
}