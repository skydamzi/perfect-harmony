using System.Collections.Generic;
using UnityEngine;

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

        // Generate local player ID
        localPlayerId = SystemInfo.deviceUniqueIdentifier;

        // Add local player to the dictionary
        connectedPlayers[localPlayerId] = new PlayerData(localPlayerId, "Player_Local");
    }

    // Handle incoming packets
    private void HandlePacketReceived(MessagePacket packet)
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
            
            // If we're the host and game has started, send current game state
            if (isHost && gameStarted)
            {
                // Send current game state to new player
                // TODO: Implement state sync logic
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
        // Forward to game logic
        if (packet.data is PlayerInputData inputData)
        {
            // Process the input from remote player
            ProcessRemoteInput(inputData);

            // Also send to the multiplayer input handler if available
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

            Debug.Log($"Player {packet.playerId} score updated: {scoreData.score}, combo: {scoreData.combo}");

            // Send score to the multiplayer input handler if it exists
            MultiplayerInputHandler mpInputHandler = FindFirstObjectByType<MultiplayerInputHandler>();
            if (mpInputHandler != null)
            {
                mpInputHandler.HandleRemoteScoreUpdate(packet.playerId, scoreData.score, scoreData.combo, scoreData.timingResult);
            }
        }
    }

    // Handle game start command
    private void HandleGameStart(MessagePacket packet)
    {
        if (isHost) return; // Only clients should handle this
        
        Debug.Log("Game started from server");
        gameStarted = true;
        
        // Start the game logic
        StartGameOnClient();
    }

    // Handle game stop command
    private void HandleGameStop(MessagePacket packet)
    {
        Debug.Log("Game stopped");
        gameStarted = false;
    }

    // Handle note result (hit/miss)
    private void HandleNoteResult(MessagePacket packet)
    {
        // Handle remote player's note hit/miss event
        Debug.Log($"Remote player {packet.playerId} {(packet.type == PacketType.NoteHit ? "hit" : "missed")} a note");
    }

    // Handle time synchronization
    private void HandleSyncTime(MessagePacket packet)
    {
        // Handle time synchronization
        // TODO: Implement time sync logic
    }

    // Handle game state synchronization
    private void HandleSyncGameState(MessagePacket packet)
    {
        // Handle game state sync
        // TODO: Implement game state sync logic
    }

    // Process input from a remote player
    private void ProcessRemoteInput(PlayerInputData inputData)
    {
        // Handle the input but don't process it locally for local player
        Debug.Log($"Remote input received: Lane {inputData.lane} at time {inputData.inputTime}");
    }

    // Start the game on client (called when server sends game start)
    private void StartGameOnClient()
    {
        // Notify the game logic to start
        RhythmGameManager.Instance?.StartCountdown();
    }

    // Send player input to server
    public void SendPlayerInput(int lane, float inputTime)
    {
        if (udpManager != null)
        {
            PlayerInputData inputData = new PlayerInputData(lane, inputTime);
            MessagePacket packet = new MessagePacket(PacketType.PlayerInput, localPlayerId, inputData);
            udpManager.SendPacket(packet);
        }
    }

    // Send player score to server
    public void SendPlayerScore(int score, int combo, TimingResult timingResult)
    {
        if (udpManager != null)
        {
            PlayerScoreData scoreData = new PlayerScoreData(score, combo, timingResult);
            MessagePacket packet = new MessagePacket(PacketType.PlayerScore, localPlayerId, scoreData);
            udpManager.SendPacket(packet);
        }
    }

    // Send game start command (host only)
    public void SendGameStart()
    {
        if (isHost && udpManager != null)
        {
            MessagePacket packet = new MessagePacket(PacketType.GameStart, localPlayerId, null);
            udpManager.SendPacket(packet);
        }
    }

    // Send game stop command
    public void SendGameStop()
    {
        if (udpManager != null)
        {
            MessagePacket packet = new MessagePacket(PacketType.GameStop, localPlayerId, null);
            udpManager.SendPacket(packet);
        }
    }

    // Send note hit result
    public void SendNoteResult(bool isHit)
    {
        if (udpManager != null)
        {
            PacketType type = isHit ? PacketType.NoteHit : PacketType.NoteMiss;
            MessagePacket packet = new MessagePacket(type, localPlayerId, null);
            udpManager.SendPacket(packet);
        }
    }

    // Check if we have enough players to start the game
    public bool HasRequiredPlayers()
    {
        return connectedPlayers.Count >= 2; // For 2-player game
    }

    private void OnDestroy()
    {
        if (udpManager != null)
        {
            udpManager.OnPacketReceived -= HandlePacketReceived;
        }
    }
}