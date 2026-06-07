using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MultiplayerManager : MonoBehaviour
{
    public static MultiplayerManager Instance { get; private set; }

    [Header("Network Settings")]
    public string currentRoomId = "Lobby"; // Added for Central Server routing
    public UDPManager udpManager;

    [Header("Player Data")]
    public string localPlayerId;
    public Dictionary<string, PlayerData> connectedPlayers = new Dictionary<string, PlayerData>();

    [Header("Game State")]
    public bool gameStarted = false;
    private bool pendingSceneLoad = false; // Added for thread-safe scene loading

    public bool IsAuthority
    {
        get
        {
            // Simple authority: the first player in the list (usually the room creator)
            if (connectedPlayers.Count == 0) return true;
            foreach (var id in connectedPlayers.Keys)
            {
                return id == localPlayerId;
            }
            return true;
        }
    }

    public int GetPlayerSlot()
    {
        int index = 0;
        foreach (var id in connectedPlayers.Keys)
        {
            if (id == localPlayerId) return index;
            index++;
        }
        return 0;
    }

    public class PlayerData
    {
        public string playerId;
        public string playerName;
        public int score;
        public int combo;
        public bool isReady;
        public bool isChartReady; // Added: Track if chart analysis is done
        
        public PlayerData(string id, string name)
        {
            playerId = id;
            playerName = name;
            score = 0;
            combo = 0;
            isReady = false;
            isChartReady = false;
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
        localPlayerId = SystemInfo.deviceUniqueIdentifier + "_" + Random.Range(0, 10000);

        // Add local player to the dictionary
        if (!connectedPlayers.ContainsKey(localPlayerId))
        {
            connectedPlayers.Add(localPlayerId, new PlayerData(localPlayerId, "Player_Local"));
        }
    }

    private void Update()
    {
        // Safety: Trigger scene load from the main thread
        if (pendingSceneLoad)
        {
            pendingSceneLoad = false;
            gameStarted = true;
            Debug.Log("Executing pending scene load: Playing");
            SceneManager.LoadScene("Playing");
        }

        // Host logic: Check if everyone is chart ready and trigger countdown
        if (IsAuthority && gameStarted && SceneManager.GetActiveScene().name == "Playing")
        {
            CheckAllPlayersChartReady();
        }
    }

    private bool countdownTriggered = false;
    private void CheckAllPlayersChartReady()
    {
        if (countdownTriggered) return;

        bool allReady = true;
        foreach (var p in connectedPlayers.Values)
        {
            if (!p.isChartReady)
            {
                allReady = false;
                break;
            }
        }

        if (allReady && connectedPlayers.Count >= 2)
        {
            countdownTriggered = true;
            // Everyone is ready! Sync start in 2 seconds
            float syncStartTime = TimingSyncManager.Instance.GetAdjustedServerTime() + 2.0f;
            SendSyncStart(syncStartTime);
        }
    }

    private void SendSyncStart(float networkStartTime)
    {
        MessagePacket packet = MessagePacket.CreateSyncStartPacket(localPlayerId, currentRoomId, networkStartTime);
        udpManager.SendPacket(packet);
        
        // Local start
        ProcessSyncStart(networkStartTime);
    }

    private void ProcessSyncStart(float networkStartTime)
    {
        if (RhythmGameManager.Instance != null)
        {
            RhythmGameManager.Instance.StartCountdownSync(networkStartTime);
        }
    }

    [Header("Debug Info")]
    public string lastPacketTypeReceived;
    public float lastPacketTime;

    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 300, 550));
        GUILayout.Box("Central Server Multiplayer");
        GUILayout.Label($"Local ID: {localPlayerId}");
        GUILayout.Label($"Room: {currentRoomId}");
        GUILayout.Label($"Game Started: {gameStarted}");
        
        GUILayout.Space(10);
        GUILayout.Label("Room Players:");
        foreach(var p in connectedPlayers.Values)
        {
            string status = p.isChartReady ? "Chart OK" : (p.isReady ? "Ready" : "Waiting");
            GUILayout.Label($"- {p.playerId.Substring(0, 8)}... : {status}, Score={p.score}");
        }

        GUILayout.Space(10);
        GUILayout.Label($"Last Packet: {lastPacketTypeReceived} @ {lastPacketTime:F2}");

        if (TimingSyncManager.Instance != null)
        {
            GUILayout.Label($"Server Latency: {TimingSyncManager.Instance.packetExchangeLatency:F1} ms");
            GUILayout.Label($"Server Time: {TimingSyncManager.Instance.GetAdjustedServerTime():F2}");
        }

        if (GUILayout.Button("Force Load 'Playing' Scene"))
        {
            SceneManager.LoadSceneAsync("Playing");
        }
        
        GUILayout.EndArea();
    }

    // Handle incoming packets from central server
    private void HandlePacketReceived(MessagePacket packet, System.Net.IPEndPoint sender)
    {
        // For central server, we only process packets that belong to our room or global system packets
        if (!string.IsNullOrEmpty(packet.roomId) && packet.roomId != currentRoomId && packet.roomId != "Global")
        {
            return;
        }

        lastPacketTypeReceived = packet.type.ToString();
        lastPacketTime = Time.time;

        switch (packet.type)
        {
            case PacketType.Connect:
            case PacketType.JoinRoom:
                HandlePlayerConnect(packet, sender);
                break;
            case PacketType.Disconnect:
            case PacketType.LeaveRoom:
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

    // Handle player connection/join
    private void HandlePlayerConnect(MessagePacket packet, System.Net.IPEndPoint sender)
    {
        if (packet.playerId == localPlayerId) return;

        if (!connectedPlayers.ContainsKey(packet.playerId))
        {
            connectedPlayers[packet.playerId] = new PlayerData(packet.playerId, $"Player_{connectedPlayers.Count}");
            Debug.Log($"Player joined room: {packet.playerId}");

            // 만약 내가 이미 이 방에 있던 사람이라면, 새로 들어온 사람에게 내 정보를 알려줘야 함
            // JoinRoom 패킷을 받았을 때만 응답 (무한 루프 방지 위해 Connect 타입으로 응답)
            if (packet.type == PacketType.JoinRoom)
            {
                MessagePacket replyPacket = new MessagePacket(PacketType.Connect, localPlayerId, currentRoomId);
                udpManager.SendPacket(replyPacket);
                Debug.Log($"Sent discovery reply to new player: {packet.playerId}");
            }
        }
    }

    // Handle player disconnect/leave
    private void HandlePlayerDisconnect(MessagePacket packet)
    {
        if (connectedPlayers.ContainsKey(packet.playerId))
        {
            connectedPlayers.Remove(packet.playerId);
            Debug.Log($"Player left room: {packet.playerId}");
        }
    }

    // Handle player input
    private void HandlePlayerInput(MessagePacket packet)
    {
        if (packet.playerId == localPlayerId) return;

        MultiplayerInputHandler mpInputHandler = FindFirstObjectByType<MultiplayerInputHandler>();
        if (mpInputHandler != null)
        {
            mpInputHandler.ProcessRemoteInput(packet.lane, packet.hitTime, packet.playerId);
        }
    }

    // Handle player score update
    private void HandlePlayerScore(MessagePacket packet)
    {
        if (packet.playerId == localPlayerId) return;

        if (connectedPlayers.ContainsKey(packet.playerId))
        {
            connectedPlayers[packet.playerId].score = packet.score;
            connectedPlayers[packet.playerId].combo = packet.combo;

            MultiplayerInputHandler mpInputHandler = FindFirstObjectByType<MultiplayerInputHandler>();
            if (mpInputHandler != null)
            {
                mpInputHandler.HandleRemoteScoreUpdate(packet.playerId, packet.score, packet.combo, (TimingResult)packet.timingResult);
            }
        }
    }

    // Handle player ready state
    private void HandlePlayerReady(MessagePacket packet)
    {
        if (connectedPlayers.ContainsKey(packet.playerId))
        {
            // 씬에 따라 로비 레디와 인게임 채보 레디를 구분
            if (SceneManager.GetActiveScene().name == "Playing")
            {
                connectedPlayers[packet.playerId].isChartReady = true;
                Debug.Log($"Player {packet.playerId} CHART is ready.");
            }
            else
            {
                connectedPlayers[packet.playerId].isReady = true;
                Debug.Log($"Player {packet.playerId} LOBBY is ready.");
            }
        }
    }

    // Handle game start command from the central server
    private void HandleGameStart(MessagePacket packet)
    {
        // 1. Scene load logic (if not started)
        if (!gameStarted && !pendingSceneLoad)
        {
            Debug.Log($"Received GameStart from server for room {currentRoomId}. Scheduling scene load.");
            pendingSceneLoad = true;
            countdownTriggered = false; // Reset for new game
            return;
        }

        // 2. Synchronized rhythm start logic (if already in scene)
        if (packet.serverTime > 0)
        {
            Debug.Log($"Received Synchronized Start signal. Starting in {packet.serverTime - TimingSyncManager.Instance.GetAdjustedServerTime()}s");
            ProcessSyncStart(packet.serverTime);
        }
    }

    // Handle game stop command
    private void HandleGameStop(MessagePacket packet)
    {
        Debug.Log("Game stopped by server");
        gameStarted = false;
        countdownTriggered = false;
    }

    private void HandleNoteHit(MessagePacket packet)
    {
        if (packet.playerId == localPlayerId) return;

        MultiplayerInputHandler mpInputHandler = FindFirstObjectByType<MultiplayerInputHandler>();
        if (mpInputHandler != null)
        {
            mpInputHandler.HandleRemoteNoteHit(packet.lane, (TimingResult)packet.timingResult);
        }
    }

    private void HandleSyncTime(MessagePacket packet)
    {
        // Central server will send timing sync packets
    }

    private void HandleSyncGameState(MessagePacket packet)
    {
        // Central server will sync game state for the room
    }

    public void JoinRoom(string roomId)
    {
        currentRoomId = roomId;
        connectedPlayers.Clear();
        // Add local player back
        connectedPlayers.Add(localPlayerId, new PlayerData(localPlayerId, "Player_Local"));

        MessagePacket packet = new MessagePacket(PacketType.JoinRoom, localPlayerId, currentRoomId);
        udpManager.SendPacket(packet);
        Debug.Log($"Sending JoinRoom request for: {roomId}");
    }

    public void SendPlayerInput(int lane, float inputTime)
    {
        MessagePacket packet = new MessagePacket(PacketType.PlayerInput, localPlayerId, currentRoomId);
        packet.lane = lane;
        packet.hitTime = inputTime;
        udpManager.SendPacket(packet);
    }

    public void SendPlayerScore(int score, int combo, TimingResult timingResult)
    {
        MessagePacket packet = MessagePacket.CreateScorePacket(localPlayerId, currentRoomId, score, combo, (int)timingResult);
        udpManager.SendPacket(packet);
    }

    public void SendPlayerReady()
    {
        if (connectedPlayers.ContainsKey(localPlayerId))
        {
            connectedPlayers[localPlayerId].isReady = true;
        }

        MessagePacket packet = new MessagePacket(PacketType.PlayerReady, localPlayerId, currentRoomId);
        udpManager.SendPacket(packet);
    }

    public void SendChartReady()
    {
        if (connectedPlayers.ContainsKey(localPlayerId))
        {
            connectedPlayers[localPlayerId].isChartReady = true;
        }

        // Use PlayerReady packet type to signal chart is ready
        MessagePacket packet = new MessagePacket(PacketType.PlayerReady, localPlayerId, currentRoomId);
        udpManager.SendPacket(packet);
        Debug.Log("Sent ChartReady signal to other players.");
    }

    public void SendNoteHit(int lane, TimingResult result)
    {
        MessagePacket packet = MessagePacket.CreateHitPacket(localPlayerId, currentRoomId, lane, (int)result, Time.time);
        udpManager.SendPacket(packet);
    }

    public void SendGameStartRequest()
    {
        // In central server mode, we request the server to start the game
        MessagePacket packet = new MessagePacket(PacketType.GameStart, localPlayerId, currentRoomId, null);
        udpManager.SendPacket(packet);
        
        Debug.Log("Sent GameStart request to server. Triggering local start.");
        // Host (sender) should also start their own scene transition
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