using UnityEngine;
using UnityEngine.UI;

public class LobbyUI : MonoBehaviour
{
    [Header("UI Elements")]
    public InputField inviteCodeInput;
    public Text inviteCodeDisplay;
    public Button createGameButton;
    public Button joinGameButton;
    public Text statusText;
    public GameObject lobbyPanel;
    public GameObject gamePanel;
    
    private LobbyManager lobbyManager;
    
    private void Start()
    {
        lobbyManager = FindFirstObjectByType<LobbyManager>();
        if (lobbyManager == null)
        {
            GameObject lobbyObj = new GameObject("LobbyManager");
            lobbyManager = lobbyObj.AddComponent<LobbyManager>();
        }
        
        SetupUI();
    }
    
    private void SetupUI()
    {
        if (createGameButton) createGameButton.onClick.AddListener(CreateGame);
        if (joinGameButton) joinGameButton.onClick.AddListener(JoinGame);
        
        UpdateInviteCodeDisplay();
    }
    
    private void CreateGame()
    {
        if (lobbyManager != null)
        {
            lobbyManager.CreateGame();
            UpdateInviteCodeDisplay();
            
            if (statusText) statusText.text = "게임 방이 생성되었습니다!";
            if (inviteCodeDisplay) inviteCodeDisplay.gameObject.SetActive(true);
            
            Debug.Log("게임 방 생성됨. 초대 코드: " + lobbyManager.GetInviteCode());
        }
    }
    
    private void JoinGame()
    {
        if (lobbyManager != null)
        {
            if (inviteCodeInput != null && !string.IsNullOrEmpty(inviteCodeInput.text))
            {
                lobbyManager.JoinGame(inviteCodeInput.text.Trim());
                
                if (statusText) statusText.text = inviteCodeInput.text + " 코드로 참가 시도 중...";
            }
            else
            {
                if (statusText) statusText.text = "초대 코드를 입력해주세요!";
            }
        }
    }
    
    private void UpdateInviteCodeDisplay()
    {
        if (lobbyManager != null && inviteCodeDisplay != null)
        {
            string code = lobbyManager.GetInviteCode();
            if (!string.IsNullOrEmpty(code))
            {
                inviteCodeDisplay.text = "초대 코드: " + code;
                inviteCodeDisplay.gameObject.SetActive(true);
            }
            else
            {
                inviteCodeDisplay.gameObject.SetActive(false);
            }
        }
    }
    
    // 게임 시작 시 UI 전환
    public void ShowGameUI()
    {
        if (lobbyPanel) lobbyPanel.SetActive(false);
        if (gamePanel) gamePanel.SetActive(true);
    }
    
    // 로비로 돌아갈 때
    public void ShowLobbyUI()
    {
        if (lobbyPanel) lobbyPanel.SetActive(true);
        if (gamePanel) gamePanel.SetActive(false);
    }
}