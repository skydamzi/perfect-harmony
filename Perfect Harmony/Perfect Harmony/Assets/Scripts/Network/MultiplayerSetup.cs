using UnityEngine;
using UnityEngine.SceneManagement;

public class MultiplayerSetup : MonoBehaviour
{
    [Header("Network Configuration")]
    public bool isHost = false;
    public string serverIP = "127.0.0.1";
    public int port = 8080;
    
    [Header("References")]
    public UDPManager udpManager;
    public MultiplayerManager mpManager;

    private void Start()
    {
        // Set up the multiplayer system based on whether we're a host or client
        SetupMultiplayerSystem();
    }

    private void SetupMultiplayerSystem()
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

        // Configure network settings
        udpManager.isServer = isHost;
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
        mpManager.isHost = isHost;
        mpManager.udpManager = udpManager;

        // If we're the host, also add the host manager
        if (isHost)
        {
            MultiplayerHost host = FindFirstObjectByType<MultiplayerHost>();
            if (host == null)
            {
                GameObject hostObj = new GameObject("MultiplayerHost");
                host = hostObj.AddComponent<MultiplayerHost>();
            }

            host.multiplayerManager = mpManager;
        }

        // Start managers
        if (udpManager != null) udpManager.enabled = true;
        if (mpManager != null) mpManager.enabled = true;
    }

    // Method to switch to host mode
    public void SetAsHost()
    {
        isHost = true;
        
        // Explicitly set UDPManager state
        if (udpManager != null)
        {
            udpManager.isServer = true;
            udpManager.InitializeServer();
        }

        if (mpManager != null)
        {
            mpManager.isHost = true;
        }

        // Ensure MultiplayerHost exists and is configured
        // Attach it to the same object as MultiplayerManager to ensure persistence
        MultiplayerHost host = null;
        if (mpManager != null)
        {
            host = mpManager.GetComponent<MultiplayerHost>();
            if (host == null)
            {
                host = mpManager.gameObject.AddComponent<MultiplayerHost>();
            }
            host.multiplayerManager = mpManager;
        }
    }

    // Method to switch to client mode
    public void SetAsClient(string ip)
    {
        isHost = false;
        serverIP = ip;
        
        // Explicitly set UDPManager state
        if (udpManager != null)
        {
            udpManager.isServer = false;
            udpManager.serverIP = ip;
            udpManager.InitializeClient(ip);
        }

        if (mpManager != null)
        {
            mpManager.isHost = false;
        }
    }

    // Method to switch to client mode with a specific port
    public void SetAsClient(string ip, int newPort)
    {
        isHost = false;
        serverIP = ip;
        port = newPort;
        
        // Explicitly set UDPManager state
        if (udpManager != null)
        {
            udpManager.isServer = false;
            udpManager.InitializeClient(ip, newPort);
        }

        if (mpManager != null)
        {
            mpManager.isHost = false;
        }
    }

    // Method to start the multiplayer game scene
    public void StartMultiplayerGame()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1); // Load next scene or game scene
    }
}