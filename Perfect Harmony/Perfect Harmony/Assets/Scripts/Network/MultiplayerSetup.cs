using UnityEngine;
using UnityEngine.SceneManagement;

public class MultiplayerSetup : MonoBehaviour
{
    [Header("Central Server Configuration")]
    public string serverIP = "116.127.190.78";
    public int port = 8080;
    public string defaultRoomId = "Lobby";
    
    [Header("References")]
    public UDPManager udpManager;
    public MultiplayerManager mpManager;

    private void Start()
    {
        SetupCentralServerSystem();
    }

    private void SetupCentralServerSystem()
    {
        // Get or create UDP manager
        if (udpManager == null)
        {
            udpManager = FindFirstObjectByType<UDPManager>();
            if (udpManager == null)
            {
                GameObject udpObj = new GameObject("UDPManager");
                udpManager = udpObj.AddComponent<UDPManager>();
            }
        }

        // Configure network settings for central server
        udpManager.isServer = false;
        udpManager.serverIP = serverIP;
        udpManager.port = port;

        // Get or create multiplayer manager
        if (mpManager == null)
        {
            mpManager = FindFirstObjectByType<MultiplayerManager>();
            if (mpManager == null)
            {
                GameObject mpObj = new GameObject("MultiplayerManager");
                mpManager = mpObj.AddComponent<MultiplayerManager>();
            }
        }

        // Set multiplayer manager properties
        mpManager.currentRoomId = defaultRoomId;
        mpManager.udpManager = udpManager;

        // Start managers
        if (udpManager != null) 
        {
            udpManager.enabled = true;
            udpManager.InitializeClient(serverIP, port);
        }
        
        if (mpManager != null) mpManager.enabled = true;
        
        Debug.Log($"Multiplayer Setup: Connecting to {serverIP}:{port} in room {defaultRoomId}");
    }

    // Method to join a specific room
    public void JoinRoom(string roomId)
    {
        if (mpManager != null)
        {
            mpManager.JoinRoom(roomId);
        }
    }

    // Method to start the multiplayer game scene
    public void StartMultiplayerGame()
    {
        if (mpManager != null)
        {
            mpManager.SendGameStartRequest();
        }
    }
}