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
        // This method controls the visibility of the "Start Game" button.
        if (mpManager != null && startGameButton != null)
        {
            // Show and enable the start button when 2 players are connected (Host or Guest).
            bool shouldBeActive = mpManager.connectedPlayers.Count >= 2;
            if (startGameButton.gameObject.activeSelf != shouldBeActive)
            {
                startGameButton.gameObject.SetActive(shouldBeActive);
                if (shouldBeActive)
                {
                     // Change button text to "Ready" if it hasn't been clicked yet
                     Text btnText = startGameButton.GetComponentInChildren<Text>();
                     if (btnText != null && startGameButton.interactable)
                     {
                         btnText.text = "Ready";
                     }
                }
            }
        }

        // Update status text with player count
        if (statusText != null && mpManager != null)
        {
            int readyCount = 0;
            foreach(var p in mpManager.connectedPlayers.Values)
            {
                if(p.isReady) readyCount++;
            }
            statusText.text = $"Players: {mpManager.connectedPlayers.Count}/2 | Ready: {readyCount}/2";
        }
    }

    private void SetupUI()
    {
        if (createGameButton) createGameButton.onClick.AddListener(CreateGame);
        if (joinGameButton) joinGameButton.onClick.AddListener(JoinGame);
        if (startGameButton) startGameButton.onClick.AddListener(OnStartGameClicked);
        
        UpdateInviteCodeDisplay();
    }
    
    private void CreateGame()
    {
        if (lobbyManager != null)
        {
            lobbyManager.CreateGame();
            UpdateInviteCodeDisplay();
            
            if (statusText) statusText.text = "Room created. Waiting for player...";
            if (inviteCodeDisplay) inviteCodeDisplay.gameObject.SetActive(true);
            
            // Host has created a game, disable join/create buttons
            createGameButton.interactable = false;
            joinGameButton.interactable = false;
        }
    }
    
    private void JoinGame()
    {
        if (lobbyManager == null || inviteCodeInput == null || string.IsNullOrEmpty(inviteCodeInput.text))
        {
            if (statusText) statusText.text = "Please enter Host IP and Port!";
            return;
        }

        string inputText = inviteCodeInput.text.Trim();
        string[] parts = inputText.Split(':');
        
        string ip = parts[0];
        int port = 8080; // Default port

        if (parts.Length > 1)
        {
            if (!int.TryParse(parts[1], out port))
            {
                if (statusText) statusText.text = "Invalid Port number!";
                return;
            }
        }

        if (string.IsNullOrEmpty(ip))
        {
            if (statusText) statusText.text = "IP address cannot be empty!";
            return;
        }

        lobbyManager.JoinGame(ip, port);
        if (statusText) statusText.text = $"Joining game at {ip}:{port}...";

        // Disable buttons after attempting to join
        createGameButton.interactable = false;
        joinGameButton.interactable = false;
    }

    private void OnStartGameClicked()
    {
        if (mpManager != null)
        {
            Debug.Log("Ready button clicked. Sending Ready command.");
            mpManager.SendPlayerReady();
            
            // Disable button after clicking
            startGameButton.interactable = false;
            Text btnText = startGameButton.GetComponentInChildren<Text>();
            if (btnText != null) btnText.text = "Waiting...";
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
