using UnityEngine;
using UnityEngine.UI;

public class ScoreManager : MonoBehaviour
{
    [Header("Score Settings")]
    public int perfectScore = 100;
    public int goodScore = 50;
    public int okayScore = 25;

    [Header("UI References")]
    public Text scoreText;
    public Text comboText;
    public Text timingText; // Display timing result

    [Header("Combo Settings")]
    public int comboMultiplier = 1;
    public int maxCombo = 0;

    private int currentScore = 0;
    private int currentCombo = 0;
    private int totalNotesHit = 0;
    private int totalNotesMissed = 0;

    void Start()
    {
        UpdateUI();
    }

    // Process a hit note with timing result
    public void ProcessHit(TimingResult timingResult)
    {
        totalNotesHit++;
        currentCombo++;

        // Update max combo if needed
        if (currentCombo > maxCombo)
            maxCombo = currentCombo;

        // Calculate score based on timing
        int points = 0;
        switch (timingResult)
        {
            case TimingResult.Perfect:
                points = perfectScore * comboMultiplier;
                break;
            case TimingResult.Good:
                points = goodScore * comboMultiplier;
                break;
            case TimingResult.Okay:
                points = okayScore * comboMultiplier;
                break;
        }

        currentScore += points;

        // Update UI
        UpdateUI();
        ShowTimingFeedback(timingResult);
    }

    // Process a missed note
    public void ProcessMiss()
    {
        totalNotesMissed++;
        currentCombo = 0; // Reset combo on miss

        // Update UI
        UpdateUI();
        ShowTimingFeedback(TimingResult.Miss);
    }

    // Update UI elements with current values
    private void UpdateUI()
    {
        if (scoreText != null)
            scoreText.text = "Score: " + currentScore;

        if (comboText != null)
            comboText.text = "Combo: " + currentCombo;
    }

    // Show timing feedback to the player
    private void ShowTimingFeedback(TimingResult timingResult)
    {
        if (timingText != null)
        {
            switch (timingResult)
            {
                case TimingResult.Perfect:
                    timingText.text = "PERFECT!";
                    timingText.color = Color.yellow;
                    break;
                case TimingResult.Good:
                    timingText.text = "GOOD!";
                    timingText.color = Color.green;
                    break;
                case TimingResult.Okay:
                    timingText.text = "OKAY!";
                    timingText.color = Color.blue;
                    break;
                case TimingResult.Miss:
                    timingText.text = "MISS!";
                    timingText.color = Color.red;
                    break;
            }

            // Clear the timing text after a delay
            Invoke("ClearTimingText", 0.7f);
        }
    }

    // Clear the timing feedback text
    private void ClearTimingText()
    {
        if (timingText != null)
            timingText.text = "";
    }
}