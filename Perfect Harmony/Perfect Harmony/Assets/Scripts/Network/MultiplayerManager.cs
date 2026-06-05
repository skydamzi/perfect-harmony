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
        GUILayout.Box("Central Server Multiplayer");
        GUILayout.Label($"Local ID: {localPlayerId}");
        GUILayout.Label($"Room: {currentRoomId}");
        GUILayout.Label($"Game Started: {gameStarted}");
        
        GUILayout.Space(10);
        GUILayout.Label("Room Players:");
        foreach(var p in connectedPlayers.Values)
        {
            GUILayout.Label($"- {p.playerId.Substring(0, 8)}... : Ready={p.isReady}, Score={p.score}");
        }

        GUILayout.Space(10);
        GUILayout.Label($"Last Packet: {lastPacketTypeReceived} @ {lastPacketTime:F2}");

        if (TimingSyncManager.Instance != null)
        {
            GUILayout.Label($"Server Latency: {TimingSyncManager.Instance.packetExchangeLatency:F1} ms");
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
                MessagePacket replyPacket = new MessagePacket(PacketType.Connect, localPlayerId, currentRoomId, null);
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
        if (packet.playerId == localPlayerId) return;

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
            Debug.Log($"Player {packet.playerId} is ready in room {currentRoomId}");
        }
    }

    // Handle game start command from the central server
    private void HandleGameStart(MessagePacket packet)
    {
        Debug.Log($"Received GameStart from server for room {currentRoomId}. Loading 'Playing' scene.");
        gameStarted = true;
        SceneManager.LoadSceneAsync("Playing");
    }

    // Handle game stop command
    private void HandleGameStop(MessagePacket packet)
    {
        Debug.Log("Game stopped by server");
        gameStarted = false;
    }

    private void HandleNoteHit(MessagePacket packet)
    {
        if (packet.playerId == localPlayerId) return;

        NoteHitData hitData = packet.GetData<NoteHitData>();
        if (hitData != null)
        {
            MultiplayerInputHandler mpInputHandler = FindFirstObjectByType<MultiplayerInputHandler>();
            if (mpInputHandler != null)
            {
                mpInputHandler.HandleRemoteNoteHit(hitData.lane, hitData.timingResult);
            }
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

        MessagePacket packet = new MessagePacket(PacketType.JoinRoom, localPlayerId, currentRoomId, null);
        udpManager.SendPacket(packet);
        Debug.Log($"Sending JoinRoom request for: {roomId}");
    }

    public void SendPlayerInput(int lane, float inputTime)
    {
        PlayerInputData inputData = new PlayerInputData(lane, inputTime);
        MessagePacket packet = new MessagePacket(PacketType.PlayerInput, localPlayerId, currentRoomId, inputData);
        udpManager.SendPacket(packet);
    }

    public void SendPlayerScore(int score, int combo, TimingResult timingResult)
    {
        PlayerScoreData scoreData = new PlayerScoreData(score, combo, timingResult);
        MessagePacket packet = new MessagePacket(PacketType.PlayerScore, localPlayerId, currentRoomId, scoreData);
        udpManager.SendPacket(packet);
    }

    public void SendPlayerReady()
    {
        if (connectedPlayers.ContainsKey(localPlayerId))
        {
            connectedPlayers[localPlayerId].isReady = true;
        }

        MessagePacket packet = new MessagePacket(PacketType.PlayerReady, localPlayerId, currentRoomId, null);
        udpManager.SendPacket(packet);
    }

    public void SendNoteHit(int lane, TimingResult result)
    {
        NoteHitData data = new NoteHitData(lane, result, Time.time);
        MessagePacket packet = new MessagePacket(PacketType.NoteHit, localPlayerId, currentRoomId, data);
        udpManager.SendPacket(packet);
    }

    public void SendGameStartRequest()
    {
        // In central server mode, we request the server to start the game
        MessagePacket packet = new MessagePacket(PacketType.GameStart, localPlayerId, currentRoomId, null);
        udpManager.SendPacket(packet);
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