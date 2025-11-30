using UnityEngine;
using System.Collections.Generic;

public class LobbyManager : MonoBehaviour
{
    public static LobbyManager Instance { get; private set; }
    
    [Header("Lobby Settings")]
    public string currentInviteCode = "";
    public UDPManager udpManager;
    public MultiplayerManager mpManager;
    
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
    }
    
    // 호스트(방 생성자)로 시작
    public void CreateGame()
    {
        currentInviteCode = GenerateInviteCode();
        Debug.Log("게임 방 생성됨. 초대 코드: " + currentInviteCode);
        
        // 멀티플레이어 설정
        MultiplayerSetup setup = GetComponent<MultiplayerSetup>();
        if (setup != null)
        {
            setup.SetAsHost();
        }
        else
        {
            GameObject setupObj = new GameObject("MultiplayerSetup");
            setup = setupObj.AddComponent<MultiplayerSetup>();
            setup.SetAsHost();
        }
        
        // 게임 시작을 기다리는 상태로 설정
        if (mpManager != null)
        {
            mpManager.gameStarted = false;
        }
    }
    
    // 클라이언트(초대 받은 사람)로 연결
    public void JoinGame(string inviteCode)
    {
        if (string.IsNullOrEmpty(inviteCode))
        {
            Debug.LogError("초대 코드가 없습니다!");
            return;
        }
        
        Debug.Log(inviteCode + " 코드로 게임에 참가 시도 중...");
        
        // 멀티플레이어 설정
        MultiplayerSetup setup = GetComponent<MultiplayerSetup>();
        if (setup != null)
        {
            setup.SetAsClient("127.0.0.1"); // 간단한 테스트를 위해 localhost 사용
        }
        else
        {
            GameObject setupObj = new GameObject("MultiplayerSetup");
            setup = setupObj.AddComponent<MultiplayerSetup>();
            setup.SetAsClient("127.0.0.1");
        }
    }
    
    // 초대 코드 생성
    private string GenerateInviteCode()
    {
        string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        System.Text.StringBuilder code = new System.Text.StringBuilder();
        
        System.Random random = new System.Random();
        for (int i = 0; i < 8; i++)
        {
            if (i == 4) code.Append('-'); // 4자리 뒤에 하이픈
            code.Append(chars[random.Next(chars.Length)]);
        }
        
        return code.ToString();
    }
    
    // 초대 코드 가져오기
    public string GetInviteCode()
    {
        return currentInviteCode;
    }
}