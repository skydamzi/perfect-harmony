using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MultiplayerManager : MonoBehaviour
{
    public static MultiplayerManager Instance { get; private set; }

    [Header("Network Settings")]
    public bool isHost = false; // True if this instance is the host/server
    public UDPManager udpManager;

    [Header("Player Data")]
    public string localPlayerId;
    public Dictionary<string, PlayerData> connectedPlayers = new Dictionary<string, PlayerData>();

    [Header("Game State")]
    public bool gameStarted = false;

    public class PlayerData
    {
        public string playerId;
        public string playerName;
        public int score;
        public int combo;
        public bool isReady;
        
        public PlayerData(string id, string name)
        {
            playerId = id;
            playerName = name;
            score = 0;
            combo = 0;
            isReady = false;
        }
    }

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

    private void Start()
    {
        // Get or create UDP manager
        if (udpManager == null)
        {
            udpManager = FindFirstObjectByType<UDPManager>();
            if (udpManager == null)
            {
                GameObject udpManagerObj = new GameObject("UDPManager");
                udpManager = udpManagerObj.AddComponent<UDPManager>();
            }
        }

        // Set up network event handlers
        if (udpManager != null)
        {
            udpManager.OnPacketReceived += HandlePacketReceived;
        }

        // Generate local player ID (Append random number for local testing support)
        localPlayerId = SystemInfo.deviceUniqueIdentifier + "_" + Random.Range(0, 10000);

        // Add local player to the dictionary
        if (!connectedPlayers.ContainsKey(localPlayerId))
        {
            connectedPlayers.Add(localPlayerId, new PlayerData(localPlayerId, "Player_Local"));
        }
    }

    // Handle incoming packets
    private void HandlePacketReceived(MessagePacket packet, System.Net.IPEndPoint sender)
    {
        switch (packet.type)
        {
            case PacketType.Connect:
                HandlePlayerConnect(packet);
                break;
            case PacketType.Disconnect:
                HandlePlayerDisconnect(packet);
                break;
            case PacketType.PlayerInput:
                HandlePlayerInput(packet);
                break;
            case PacketType.PlayerScore:
                HandlePlayerScore(packet);
                break;
            case PacketType.GameStart:
                HandleGameStart(packet);
                break;
            case PacketType.GameStop:
                HandleGameStop(packet);
                break;
            case PacketType.NoteHit:
            case PacketType.NoteMiss:
                HandleNoteResult(packet);
                break;
            case PacketType.SyncTime:
                HandleSyncTime(packet);
                break;
            case PacketType.SyncGameState:
                HandleSyncGameState(packet);
                break;
        }
    }

    // Handle player connection
    private void HandlePlayerConnect(MessagePacket packet)
    {
        if (!connectedPlayers.ContainsKey(packet.playerId))
        {
            connectedPlayers[packet.playerId] = new PlayerData(packet.playerId, $"Player_{connectedPlayers.Count}");
            Debug.Log($"Player connected: {packet.playerId}");
            
            if (isHost && gameStarted)
            {
                // TODO: Implement state sync logic for late joiners
            }
        }
    }

    // Handle player disconnect
    private void HandlePlayerDisconnect(MessagePacket packet)
    {
        if (connectedPlayers.ContainsKey(packet.playerId))
        {
            connectedPlayers.Remove(packet.playerId);
            Debug.Log($"Player disconnected: {packet.playerId}");
        }
    }

    // Handle player input
    private void HandlePlayerInput(MessagePacket packet)
    {
        if (packet.data is PlayerInputData inputData)
        {
            MultiplayerInputHandler mpInputHandler = FindFirstObjectByType<MultiplayerInputHandler>();
            if (mpInputHandler != null)
            {
                mpInputHandler.ProcessRemoteInput(inputData.lane, inputData.inputTime, packet.playerId);
            }
        }
    }

    // Handle player score update
    private void HandlePlayerScore(MessagePacket packet)
    {
        if (packet.data is PlayerScoreData scoreData && connectedPlayers.ContainsKey(packet.playerId))
        {
            connectedPlayers[packet.playerId].score = scoreData.score;
            connectedPlayers[packet.playerId].combo = scoreData.combo;

            MultiplayerInputHandler mpInputHandler = FindFirstObjectByType<MultiplayerInputHandler>();
            if (mpInputHandler != null)
            {
                mpInputHandler.HandleRemoteScoreUpdate(packet.playerId, scoreData.score, scoreData.combo, scoreData.timingResult);
            }
        }
    }

    // Handle game start command from the server
    private void HandleGameStart(MessagePacket packet)
    {
        if (isHost) return; // Only clients should handle this packet

        Debug.Log("Received GameStart command from server. Loading 'Playing' scene.");
        gameStarted = true;
        
        // Load the game scene
        SceneManager.LoadScene("Playing");
    }

    // Handle game stop command
    private void HandleGameStop(MessagePacket packet)
    {
        Debug.Log("Game stopped");
        gameStarted = false;
        // Optional: Load lobby scene here
        // SceneManager.LoadScene("Lobby");
    }

    private void HandleNoteResult(MessagePacket packet)
    {
        Debug.Log($"Remote player {packet.playerId} {(packet.type == PacketType.NoteHit ? "hit" : "missed")} a note");
    }

    private void HandleSyncTime(MessagePacket packet)
    {
        // TODO: Implement time sync logic
    }

    private void HandleSyncGameState(MessagePacket packet)
    {
        // TODO: Implement game state sync logic
    }

    public void SendPlayerInput(int lane, float inputTime)
    {
        if (udpManager != null)
        {
            PlayerInputData inputData = new PlayerInputData(lane, inputTime);
            MessagePacket packet = new MessagePacket(PacketType.PlayerInput, localPlayerId, inputData);
            udpManager.SendPacket(packet);
        }
    }

    public void SendPlayerScore(int score, int combo, TimingResult timingResult)
    {
        if (udpManager != null)
        {
            PlayerScoreData scoreData = new PlayerScoreData(score, combo, timingResult);
            MessagePacket packet = new MessagePacket(PacketType.PlayerScore, localPlayerId, scoreData);
            udpManager.SendPacket(packet);
        }
    }

    public void SendGameStart()
    {
        if (isHost && udpManager != null)
        {
            MessagePacket packet = new MessagePacket(PacketType.GameStart, localPlayerId, null);
            
            MultiplayerHost host = FindFirstObjectByType<MultiplayerHost>();
            if (host != null)
            {
                host.BroadcastToAll(packet);
            }
            else
            {
                // Fallback if host component not found
                udpManager.SendPacket(packet);
            }
        }
    }

    public bool HasRequiredPlayers()
    {
        return connectedPlayers.Count >= 2;
    }

    private void OnDestroy()
    {
        if (udpManager != null)
        {
            udpManager.OnPacketReceived -= HandlePacketReceived;
        }
    }
}
