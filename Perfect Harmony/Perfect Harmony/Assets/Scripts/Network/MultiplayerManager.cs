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

    [Header("Debug Info")]
    public string lastPacketTypeReceived;
    public float lastPacketTime;

    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 300, 500));
        GUILayout.Box("Network Debugger");
        GUILayout.Label($"Local ID: {localPlayerId}");
        GUILayout.Label($"Role: {(isHost ? "HOST" : "CLIENT")}");
        GUILayout.Label($"Game Started: {gameStarted}");
        
        GUILayout.Space(10);
        GUILayout.Label("Connected Players:");
        foreach(var p in connectedPlayers.Values)
        {
            GUILayout.Label($"- {p.playerId.Substring(0, 8)}... : Ready={p.isReady}, Score={p.score}");
        }

        GUILayout.Space(10);
        GUILayout.Label($"Last Packet: {lastPacketTypeReceived} @ {lastPacketTime:F2}");

        if (GUILayout.Button("Force Load 'Playing' Scene"))
        {
            SceneManager.LoadSceneAsync("Playing");
        }
        
        GUILayout.EndArea();
    }

    // Handle incoming packets
    private void HandlePacketReceived(MessagePacket packet, System.Net.IPEndPoint sender)
    {
        lastPacketTypeReceived = packet.type.ToString();
        lastPacketTime = Time.time;

        // Debug.Log($"Packet received: {packet.type} from {packet.playerId}"); // Too noisy for input
        if (packet.type == PacketType.GameStart) Debug.Log($"Packet received: {packet.type} from {packet.playerId}");

        switch (packet.type)
        {
            case PacketType.Connect:
                HandlePlayerConnect(packet, sender);
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
            case PacketType.PlayerReady:
                HandlePlayerReady(packet);
                break;
            case PacketType.GameStart:
                HandleGameStart(packet);
                break;
            case PacketType.GameStop:
                HandleGameStop(packet);
                break;
            case PacketType.NoteHit:
            case PacketType.NoteMiss:
                HandleNoteHit(packet);
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
    private void HandlePlayerConnect(MessagePacket packet, System.Net.IPEndPoint sender)
    {
        if (!connectedPlayers.ContainsKey(packet.playerId))
        {
            connectedPlayers[packet.playerId] = new PlayerData(packet.playerId, $"Player_{connectedPlayers.Count}");
            Debug.Log($"Player connected: {packet.playerId}");
            
            // If we are the host, we need to:
            // 1. Send our own info back to the new client so they know who we are.
            // 2. (Optional) Send info about OTHER existing clients to the new client (for >2 players).
            if (isHost)
            {
                // Reply to the new client
                MessagePacket replyPacket = new MessagePacket(PacketType.Connect, localPlayerId, null);
                udpManager.SendPacketTo(replyPacket, sender);

                if (gameStarted)
                {
                    // TODO: Implement state sync logic for late joiners
                }
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
        PlayerInputData inputData = packet.GetData<PlayerInputData>();
        if (inputData != null)
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
        PlayerScoreData scoreData = packet.GetData<PlayerScoreData>();
        if (scoreData != null && connectedPlayers.ContainsKey(packet.playerId))
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

    // Handle player ready state
    private void HandlePlayerReady(MessagePacket packet)
    {
        if (connectedPlayers.ContainsKey(packet.playerId))
        {
            connectedPlayers[packet.playerId].isReady = true;
            Debug.Log($"Player {packet.playerId} is ready!");

            if (isHost)
            {
                CheckAllPlayersReady();
            }
        }
    }

    private void CheckAllPlayersReady()
    {
        // Host checks if everyone is ready
        bool allReady = true;
        foreach (var player in connectedPlayers.Values)
        {
            if (!player.isReady)
            {
                allReady = false;
                break;
            }
        }

        if (allReady && connectedPlayers.Count >= 2)
        {
            Debug.Log("All players ready. Starting game!");
            SendGameStart();
        }
    }

    // Handle game start command from the server
    private void HandleGameStart(MessagePacket packet)
    {
        // if (isHost) return; // Allow both Host and Client to handle GameStart logic (e.g. if Client starts it)

        Debug.Log($"Received GameStart command from {packet.playerId}. Loading 'Playing' scene async.");
        gameStarted = true;
        
        // Load the game scene asynchronously to avoid freezing the network stack
        SceneManager.LoadSceneAsync("Playing");
    }

    // Handle game stop command
    private void HandleGameStop(MessagePacket packet)
    {
        Debug.Log("Game stopped");
        gameStarted = false;
        // Optional: Load lobby scene here
        // SceneManager.LoadScene("Lobby");
    }

    private void HandleNoteHit(MessagePacket packet)
    {
        // Assuming payload contains NoteHitData (Lane, Timing)
        NoteHitData hitData = packet.GetData<NoteHitData>();
        if (hitData != null)
        {
            MultiplayerInputHandler mpInputHandler = FindFirstObjectByType<MultiplayerInputHandler>();
            if (mpInputHandler != null)
            {
                mpInputHandler.HandleRemoteNoteHit(hitData.lane, hitData.timingResult);
            }
            Debug.Log($"Remote note hit on lane {hitData.lane}: {hitData.timingResult}");
        }
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
        PlayerInputData inputData = new PlayerInputData(lane, inputTime);
        MessagePacket packet = new MessagePacket(PacketType.PlayerInput, localPlayerId, inputData);

        if (isHost)
        {
            MultiplayerHost host = FindFirstObjectByType<MultiplayerHost>();
            if (host != null)
            {
                host.BroadcastToAllExcept(packet, localPlayerId);
            }
        }
        else if (udpManager != null)
        {
            udpManager.SendPacket(packet);
        }
    }

    public void SendPlayerScore(int score, int combo, TimingResult timingResult)
    {
        PlayerScoreData scoreData = new PlayerScoreData(score, combo, timingResult);
        MessagePacket packet = new MessagePacket(PacketType.PlayerScore, localPlayerId, scoreData);

        if (isHost)
        {
            MultiplayerHost host = FindFirstObjectByType<MultiplayerHost>();
            if (host != null)
            {
                // Host broadcasts their own score to all clients
                host.BroadcastToAllExcept(packet, localPlayerId);
            }
        }
        else if (udpManager != null)
        {
            udpManager.SendPacket(packet);
        }
    }

    public void SendPlayerReady()
    {
        // Set local player ready
        if (connectedPlayers.ContainsKey(localPlayerId))
        {
            connectedPlayers[localPlayerId].isReady = true;
        }

        StartCoroutine(ReadyCheckRoutine());
    }

    public void SendNoteHit(int lane, TimingResult result)
    {
        NoteHitData data = new NoteHitData(lane, result, Time.time);
        MessagePacket packet = new MessagePacket(PacketType.NoteHit, localPlayerId, data);

        if (isHost)
        {
            MultiplayerHost host = FindFirstObjectByType<MultiplayerHost>();
            if (host != null)
            {
                host.BroadcastToAllExcept(packet, localPlayerId);
            }
        }
        else if (udpManager != null)
        {
            udpManager.SendPacket(packet);
        }
    }

    private System.Collections.IEnumerator ReadyCheckRoutine()
    {
        // Keep sending Ready packet until game starts or we are no longer ready
        while (!gameStarted && connectedPlayers.ContainsKey(localPlayerId) && connectedPlayers[localPlayerId].isReady)
        {
            MessagePacket packet = new MessagePacket(PacketType.PlayerReady, localPlayerId, null);

            if (isHost)
            {
                // Host broadcasts their ready status to all clients
                MultiplayerHost host = FindFirstObjectByType<MultiplayerHost>();
                if (host != null)
                {
                    host.BroadcastToAllExcept(packet, localPlayerId);
                }
                
                // Check immediately
                CheckAllPlayersReady();
            }
            else
            {
                // Client sends ready status to host
                if (udpManager != null)
                {
                    udpManager.SendPacket(packet);
                }
            }

            yield return new WaitForSeconds(0.5f);
        }
    }

    public void SendGameStart()
    {
        if (udpManager != null)
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

            // Start locally for the host with a slight delay to ensure packets start sending
            StartCoroutine(DelayedHostStart(packet));
        }
    }

    private System.Collections.IEnumerator DelayedHostStart(MessagePacket packet)
    {
        yield return new WaitForSeconds(0.2f);
        HandleGameStart(packet);
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
