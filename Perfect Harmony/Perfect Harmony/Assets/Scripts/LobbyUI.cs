using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class LobbyUI : MonoBehaviour
{
    [Header("UI Elements")]
    public InputField inviteCodeInput;
    public Text inviteCodeDisplay;
    public Button createGameButton;
    public Button joinGameButton;
    public Button startGameButton; // Added for starting the game
    public Text statusText;
    public GameObject lobbyPanel;
    public GameObject gamePanel;
    
    private LobbyManager lobbyManager;
    private MultiplayerManager mpManager;
    
    private void Start()
    {
        // Managers are created by other scripts (LobbySceneController, MultiplayerManager)
        // We just need to find them.
        lobbyManager = FindFirstObjectByType<LobbyManager>();
        if (lobbyManager == null)
        {
            // LobbyManager is essential, let's add it if missing.
            GameObject lobbyObj = new GameObject("LobbyManager");
            lobbyManager = lobbyObj.AddComponent<LobbyManager>();
        }

        mpManager = FindFirstObjectByType<MultiplayerManager>();

        SetupUI();
    }
    
    private void Update()
    {
        if (mpManager == null) return;

        // Update status text with player count and ready status
        if (statusText != null)
        {
            int readyCount = 0;
            foreach (var p in mpManager.connectedPlayers.Values)
            {
                if (p.isReady) readyCount++;
            }
            statusText.text = $"Players: {mpManager.connectedPlayers.Count}/2 | Ready: {readyCount}/2";
        }

        // Control the "Ready / Start" button
        if (startGameButton != null)
        {
            bool enoughPlayers = mpManager.connectedPlayers.Count >= 2;
            
            if (!enoughPlayers)
            {
                startGameButton.gameObject.SetActive(false);
                return;
            }

            startGameButton.gameObject.SetActive(true);
            Text btnText = startGameButton.GetComponentInChildren<Text>();
            
            // Check local player's ready state
            bool localReady = false;
            if (mpManager.connectedPlayers.ContainsKey(mpManager.localPlayerId))
            {
                localReady = mpManager.connectedPlayers[mpManager.localPlayerId].isReady;
            }

            // Check if everyone is ready
            bool everyoneReady = true;
            foreach (var p in mpManager.connectedPlayers.Values)
            {
                if (!p.isReady) everyoneReady = false;
            }

            if (!localReady)
            {
                // State 1: Local player needs to get ready
                startGameButton.interactable = true;
                if (btnText != null) btnText.text = "Ready";
            }
            else if (mpManager.IsAuthority && everyoneReady)
            {
                // State 2: Authority player can start the game
                startGameButton.interactable = true;
                if (btnText != null) btnText.text = "Start Game";
            }
            else
            {
                // State 3: Waiting for others
                startGameButton.interactable = false;
                if (btnText != null) btnText.text = everyoneReady ? "Starting..." : "Waiting...";
            }
        }
    }

    private void OnStartGameClicked()
    {
        if (mpManager == null) return;

        bool localReady = false;
        if (mpManager.connectedPlayers.ContainsKey(mpManager.localPlayerId))
        {
            localReady = mpManager.connectedPlayers[mpManager.localPlayerId].isReady;
        }

        if (!localReady)
        {
            Debug.Log("Local player is now READY.");
            mpManager.SendPlayerReady();
        }
        else if (mpManager.IsAuthority)
        {
            // Only the authority can trigger the actual start request
            Debug.Log("Authority is STARTING the game.");
            mpManager.SendGameStartRequest();
        }
    }
    
    private void UpdateInviteCodeDisplay()
    {
        if (lobbyManager != null && inviteCodeDisplay != null)
        {
            string code = lobbyManager.GetInviteCode();
            if (!string.IsNullOrEmpty(code))
            {
                inviteCodeDisplay.text = "Host IP: " + code;
                inviteCodeDisplay.gameObject.SetActive(true);
            }
            else
            {
                inviteCodeDisplay.gameObject.SetActive(false);
            }
        }
    }
}
