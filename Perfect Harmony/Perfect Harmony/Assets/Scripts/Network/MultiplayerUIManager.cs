using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class MultiplayerUIManager : MonoBehaviour
{
    [Header("Player UI Elements")]
    public Text player1ScoreText;
    public Text player1ComboText;
    public Text player2ScoreText;
    public Text player2ComboText;
    
    [Header("Game Status UI")]
    public Text gameStatusText;
    public Text connectionStatusText;
    public Button startGameButton;
    public Button disconnectButton;
    
    [Header("Player Names")]
    public Text player1NameText;
    public Text player2NameText;
    
    private MultiplayerManager mpManager;
    private ScoreManager localScoreManager;
    private Dictionary<string, PlayerScoreUI> playerUIElements = new Dictionary<string, PlayerScoreUI>();
    
    private class PlayerScoreUI
    {
        public Text scoreText;
        public Text comboText;
        public Text nameText;
        
        public PlayerScoreUI(Text score, Text combo, Text name)
        {
            scoreText = score;
            comboText = combo;
            nameText = name;
        }
    }

    private void Start()
    {
        mpManager = FindFirstObjectByType<MultiplayerManager>();
        localScoreManager = FindFirstObjectByType<ScoreManager>();

        // Initialize UI elements
        InitializeUI();

        // Set up button listeners
        if (startGameButton != null)
        {
            startGameButton.onClick.AddListener(OnStartGameClicked);
        }

        if (disconnectButton != null)
        {
            disconnectButton.onClick.AddListener(OnDisconnectClicked);
        }

        // Update initial UI state
        UpdateConnectionStatus();
    }
    
    private void InitializeUI()
    {
        // Create dictionary to map player IDs to UI elements
        if (player1ScoreText != null && player1ComboText != null && player1NameText != null)
        {
            PlayerScoreUI player1UI = new PlayerScoreUI(player1ScoreText, player1ComboText, player1NameText);
            // We'll assign this based on player order later
        }
        
        if (player2ScoreText != null && player2ComboText != null && player2NameText != null)
        {
            PlayerScoreUI player2UI = new PlayerScoreUI(player2ScoreText, player2ComboText, player2NameText);
            // We'll assign this based on player order later
        }
    }

    private void Update()
    {
        // Update UI elements regularly
        UpdateScoreDisplay();
        UpdateConnectionStatus();
        UpdateGameStatus();
    }

    // Update score displays for all players
    private void UpdateScoreDisplay()
    {
        if (mpManager == null) return;
        
        // Update local player score
        if (localScoreManager != null)
        {
            if (mpManager.localPlayerId == GetPlayerIdByIndex(0))
            {
                if (player1ScoreText != null)
                    player1ScoreText.text = $"Score: {localScoreManager.currentScore}";
                if (player1ComboText != null)
                    player1ComboText.text = $"Combo: {localScoreManager.currentCombo}";
            }
            else
            {
                if (player2ScoreText != null)
                    player2ScoreText.text = $"Score: {localScoreManager.currentScore}";
                if (player2ComboText != null)
                    player2ComboText.text = $"Combo: {localScoreManager.currentCombo}";
            }
        }
        
        // Update remote player scores
        foreach (var player in mpManager.connectedPlayers)
        {
            if (player.Key != mpManager.localPlayerId)
            {
                if (mpManager.localPlayerId == GetPlayerIdByIndex(0))
                {
                    // Local player is player 1, remote is player 2
                    if (player2ScoreText != null)
                        player2ScoreText.text = $"Score: {player.Value.score}";
                    if (player2ComboText != null)
                        player2ComboText.text = $"Combo: {player.Value.combo}";
                    if (player2NameText != null)
                        player2NameText.text = player.Value.playerName;
                }
                else
                {
                    // Local player is player 2, remote is player 1
                    if (player1ScoreText != null)
                        player1ScoreText.text = $"Score: {player.Value.score}";
                    if (player1ComboText != null)
                        player1ComboText.text = $"Combo: {player.Value.combo}";
                    if (player1NameText != null)
                        player1NameText.text = player.Value.playerName;
                }
            }
        }
    }

    // Update connection status display
    private void UpdateConnectionStatus()
    {
        if (connectionStatusText != null && mpManager != null)
        {
            int playerCount = mpManager.connectedPlayers.Count;
            connectionStatusText.text = $"Players Connected: {playerCount}/2";
            
            if (playerCount == 2)
            {
                connectionStatusText.color = Color.green;
            }
            else
            {
                connectionStatusText.color = Color.red;
            }
        }
    }

    // Update game status display
    private void UpdateGameStatus()
    {
        if (gameStatusText != null && mpManager != null)
        {
            if (mpManager.gameStarted)
            {
                gameStatusText.text = "Game In Progress";
                gameStatusText.color = Color.green;
                
                // Disable start game button when game is running
                if (startGameButton != null)
                {
                    startGameButton.interactable = false;
                }
            }
            else
            {
                gameStatusText.text = "Waiting to Start";
                gameStatusText.color = Color.yellow;
                
                // Enable start game button when game is not running
                if (startGameButton != null)
                {
                    startGameButton.interactable = mpManager.HasRequiredPlayers();
                }
            }
        }
    }

    // Get player ID by index (for determining UI assignment)
    private string GetPlayerIdByIndex(int index)
    {
        if (mpManager != null && mpManager.connectedPlayers.Count > index)
        {
            int i = 0;
            foreach (var player in mpManager.connectedPlayers)
            {
                if (i == index)
                {
                    return player.Key;
                }
                i++;
            }
        }
        return null;
    }

    // Called when start game button is clicked
    private void OnStartGameClicked()
    {
        if (mpManager != null && mpManager.isHost && mpManager.HasRequiredPlayers())
        {
            mpManager.SendGameStart();
        }
    }

    // Called when disconnect button is clicked
    private void OnDisconnectClicked()
    {
        if (mpManager != null)
        {
            // Send disconnect packet
            MessagePacket disconnectPacket = new MessagePacket(PacketType.Disconnect, mpManager.localPlayerId, null);
            mpManager.udpManager.SendPacket(disconnectPacket);
            
            // Stop the connection
            mpManager.udpManager.StopConnection();
        }
    }

    // Show win/lose message after game ends
    public void ShowGameResult(bool isWinner)
    {
        if (gameStatusText != null)
        {
            gameStatusText.text = isWinner ? "VICTORY!" : "DEFEAT!";
            gameStatusText.color = isWinner ? Color.cyan : Color.red;
        }
    }
}