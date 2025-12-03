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
        currentInviteCode = GetLocalIPAddress();
        Debug.Log("게임 방 생성됨. 호스트 IP: " + currentInviteCode);
        
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
            Debug.LogError("IP 주소가 없습니다!");
            return;
        }
        
        Debug.Log(inviteCode + " 주소로 게임에 참가 시도 중...");
        
        // 멀티플레이어 설정
        MultiplayerSetup setup = GetComponent<MultiplayerSetup>();
        if (setup != null)
        {
            setup.SetAsClient(inviteCode); // 입력된 IP 주소 사용
        }
        else
        {
            GameObject setupObj = new GameObject("MultiplayerSetup");
            setup = setupObj.AddComponent<MultiplayerSetup>();
            setup.SetAsClient(inviteCode);
        }
    }
    
    // 로컬 IP 주소 가져오기 (소켓 방식 - 더 빠르고 정확함)
    private string GetLocalIPAddress()
    {
        string localIP = "127.0.0.1";
        try
        {
            using (System.Net.Sockets.Socket socket = new System.Net.Sockets.Socket(System.Net.Sockets.AddressFamily.InterNetwork, System.Net.Sockets.SocketType.Dgram, System.Net.Sockets.ProtocolType.Udp))
            {
                socket.Connect("8.8.8.8", 65530);
                System.Net.IPEndPoint endPoint = socket.LocalEndPoint as System.Net.IPEndPoint;
                localIP = endPoint.Address.ToString();
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("IP 주소 가져오기 실패 (소켓 방식), DNS 방식 시도: " + e.Message);
        }

        // Log all available IPs for debugging
        try
        {
            var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
            Debug.Log("=== Available IP Addresses ===");
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    Debug.Log(" - " + ip.ToString());
                    // If socket failed, prefer standard LAN IPs
                    if (localIP == "127.0.0.1" && !ip.ToString().StartsWith("127.") && !ip.ToString().StartsWith("169.254"))
                    {
                        localIP = ip.ToString();
                    }
                }
            }
            Debug.Log("==============================");
        }
        catch { }

        return localIP;
    }
    
    // 초대 코드 가져오기
    public string GetInviteCode()
    {
        return currentInviteCode;
    }
}