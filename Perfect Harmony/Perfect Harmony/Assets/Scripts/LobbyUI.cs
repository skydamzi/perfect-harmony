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
            // Show and enable the start button only for the host when 2 players are connected.
            bool shouldBeActive = mpManager.isHost && mpManager.connectedPlayers.Count >= 2;
            if (startGameButton.gameObject.activeSelf != shouldBeActive)
            {
                startGameButton.gameObject.SetActive(shouldBeActive);
            }
        }

        // Update status text with player count
        if (statusText != null && mpManager != null)
        {
            statusText.text = $"Players Connected: {mpManager.connectedPlayers.Count} / 2";
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
        if (lobbyManager != null)
        {
            if (inviteCodeInput != null && !string.IsNullOrEmpty(inviteCodeInput.text))
            {
                lobbyManager.JoinGame(inviteCodeInput.text.Trim());
                if (statusText) statusText.text = "Joining game with code: " + inviteCodeInput.text;

                // Client has attempted to join, disable join/create buttons
                createGameButton.interactable = false;
                joinGameButton.interactable = false;
            }
            else
            {
                if (statusText) statusText.text = "Please enter Host IP!";
            }
        }
    }

    private void OnStartGameClicked()
    {
        if (mpManager != null && mpManager.isHost)
        {
            Debug.Log("Start Game button clicked by host. Sending start command and loading 'Playing' scene.");
            mpManager.SendGameStart();
            SceneManager.LoadScene("Playing");
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
