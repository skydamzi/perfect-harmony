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
    [Header("Instrument Selection")]
    public Button pianoSelectButton;
    public Button drumSelectButton;
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

        // Update status text based on whether we are in a room or not
        if (statusText != null)
        {
            if (mpManager.currentRoomId == "Lobby")
            {
                statusText.text = "Welcome! Please Create or Join a Game.";
            }
            else
            {
                int readyCount = 0;
                foreach (var p in mpManager.connectedPlayers.Values)
                {
                    if (p.isReady) readyCount++;
                }
                statusText.text = $"Players: {mpManager.connectedPlayers.Count}/2 | Ready: {readyCount}/2";
            }
        }

        // Control the "Ready / Start" button
        if (startGameButton != null)
        {
            // Always keep the button visible as per user request
            startGameButton.gameObject.SetActive(true);
            
            Text btnText = startGameButton.GetComponentInChildren<Text>();
            Image btnImage = startGameButton.GetComponent<Image>();
            bool enoughPlayers = mpManager.connectedPlayers.Count >= 2;

            if (!enoughPlayers)
            {
                // State 0: Not enough players
                startGameButton.interactable = false;
                if (btnText != null) btnText.text = "Waiting for Players...";
                if (btnImage != null) btnImage.color = Color.gray;
                return;
            }

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
                if (btnImage != null) btnImage.color = Color.white;
            }
            else if (mpManager.IsAuthority && everyoneReady)
            {
                // State 2: Authority player can start the game (GREEN!)
                startGameButton.interactable = true;
                if (btnText != null) btnText.text = "Start Game";
                if (btnImage != null) btnImage.color = Color.green;
            }
            else
            {
                // State 3: Waiting for others
                startGameButton.interactable = false;
                if (btnText != null) btnText.text = everyoneReady ? "Starting..." : "Waiting for Others...";
                if (btnImage != null) btnImage.color = new Color(0.7f, 0.7f, 0.7f); // Slightly dimmed
            }
        }
    }

    private void SetupUI()
    {
        if (createGameButton) createGameButton.onClick.AddListener(CreateGame);
        if (joinGameButton) joinGameButton.onClick.AddListener(JoinGame);
        if (startGameButton) startGameButton.onClick.AddListener(OnStartGameClicked);
        
        // Setup Instrument Selection
        if (pianoSelectButton) pianoSelectButton.onClick.AddListener(() => SelectInstrument("Piano"));
        if (drumSelectButton) drumSelectButton.onClick.AddListener(() => SelectInstrument("Drums"));
        
        UpdateInviteCodeDisplay();
        UpdateInstrumentUI();
    }

    private void SelectInstrument(string instrument)
    {
        if (mpManager != null)
        {
            mpManager.selectedInstrument = instrument;
            Debug.Log($"Instrument selected: {instrument}");
            UpdateInstrumentUI();
        }
    }

    private void UpdateInstrumentUI()
    {
        if (mpManager == null) return;

        // Visual feedback for selection (simple color change as an example)
        if (pianoSelectButton) 
            pianoSelectButton.GetComponent<Image>().color = mpManager.selectedInstrument == "Piano" ? Color.green : Color.white;
        
        if (drumSelectButton) 
            drumSelectButton.GetComponent<Image>().color = mpManager.selectedInstrument == "Drums" ? Color.green : Color.white;
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
            // Always keep the display object active
            inviteCodeDisplay.gameObject.SetActive(true);
            
            string code = lobbyManager.GetInviteCode();
            if (!string.IsNullOrEmpty(code))
            {
                inviteCodeDisplay.text = "Host IP: " + code;
            }
            else
            {
                inviteCodeDisplay.text = "Host IP: (Not Created)";
            }
        }
    }
}
