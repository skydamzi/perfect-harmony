using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum MultiplayerState { Lobby, Loading, Ready, Playing }

public class MultiplayerManager : MonoBehaviour
{
    public static MultiplayerManager Instance { get; private set; }

    [Header("Network Settings")]
    public string currentRoomId = "Lobby";
    public string selectedInstrument = "Piano"; // Added for instrument selection
    public UDPManager udpManager;
    public string localPlayerId;

    [Header("Game State")]
    public MultiplayerState state = MultiplayerState.Lobby;
    public Dictionary<string, PlayerData> connectedPlayers = new Dictionary<string, PlayerData>();

    // [복구] 기존 스크립트들이 참조하는 변수
    public bool gameStarted 
    { 
        get { return state != MultiplayerState.Lobby; }
        set { if (value) state = MultiplayerState.Loading; else state = MultiplayerState.Lobby; }
    }

    public class PlayerData
    {
        public string playerId;   // [복구] 기존 명칭
        public string playerName; // [복구] 기존 명칭
        public bool isReady;      // Lobby ready
        public bool isChartReady; // Analysis done
        public int score;
        public int combo;

        public PlayerData(string id) { 
            this.playerId = id; 
            this.playerName = "Player_" + id.Substring(0, Mathf.Min(4, id.Length));
        }
    }

    public bool IsAuthority
    {
        get
        {
            if (connectedPlayers.Count <= 1) return true;
            List<string> ids = new List<string>(connectedPlayers.Keys);
            ids.Sort();
            return ids[0] == localPlayerId;
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else { Instance = this; DontDestroyOnLoad(gameObject); }
    }

    private void Start()
    {
        if (udpManager == null) udpManager = FindFirstObjectByType<UDPManager>() ?? new GameObject("UDPManager").AddComponent<UDPManager>();
        udpManager.OnPacketReceived += HandlePacket;
        localPlayerId = SystemInfo.deviceUniqueIdentifier + "_" + Random.Range(0, 10000);
        connectedPlayers.Add(localPlayerId, new PlayerData(localPlayerId));
    }

    private void Update()
    {
        if (state == MultiplayerState.Ready && IsAuthority)
        {
            CheckAndStartGame();
        }
    }

    // --- Core Logic ---

    private void CheckAndStartGame()
    {
        bool everyoneReady = true;
        foreach (var p in connectedPlayers.Values)
        {
            if (!p.isChartReady) { everyoneReady = false; break; }
        }

        if (everyoneReady && connectedPlayers.Count >= 2)
        {
            // Start in 3 seconds using Server Time
            float startDelay = 3.0f;
            if (RhythmGameManager.Instance != null) startDelay = RhythmGameManager.Instance.startDelay;
            
            double targetTime = TimingSyncManager.Instance.GetAdjustedServerTime() + (double)startDelay;
            BroadcastSyncStart(targetTime);
        }
    }

    private void BroadcastSyncStart(double targetTime)
    {
        MessagePacket p = MessagePacket.CreateSyncStart(localPlayerId, currentRoomId, targetTime);
        p.serverTime = targetTime; 
        udpManager.SendPacket(p);
        ExecuteStart(targetTime);
    }

    private void ExecuteStart(double targetTime)
    {
        state = MultiplayerState.Playing;
        if (RhythmGameManager.Instance != null) RhythmGameManager.Instance.StartCountdownSync(targetTime);
    }

    // --- Packet Handlers ---

    private void HandlePacket(MessagePacket p, System.Net.IPEndPoint sender, double arrivalTimestamp)
    {
        if (!string.IsNullOrEmpty(p.roomId) && p.roomId != currentRoomId && p.roomId != "Global") return;

        switch (p.type)
        {
            case PacketType.Connect:
            case PacketType.JoinRoom: HandleJoin(p); break;
            case PacketType.PlayerReady: HandleReady(p); break;
            case PacketType.GameStart: HandleGameStart(p); break;
            case PacketType.PlayerInput: HandleInput(p); break;
            case PacketType.PlayerScore: HandleScore(p); break;
            case PacketType.NoteHit: HandleHit(p); break;
        }
    }

    private void HandleJoin(MessagePacket p)
    {
        if (p.playerId == localPlayerId) return;
        if (!connectedPlayers.ContainsKey(p.playerId))
        {
            connectedPlayers[p.playerId] = new PlayerData(p.playerId);
            // Reply to let them know we are here
            udpManager.SendPacket(new MessagePacket(PacketType.Connect, localPlayerId, currentRoomId));
        }
    }

    private void HandleReady(MessagePacket p)
    {
        if (connectedPlayers.ContainsKey(p.playerId))
        {
            if (SceneManager.GetActiveScene().name == "Playing")
            {
                connectedPlayers[p.playerId].isChartReady = true;
                Debug.Log($"[Sync] Player {p.playerId} CHART READY");
            }
            else
            {
                connectedPlayers[p.playerId].isReady = true;
            }
        }
    }

    private void HandleGameStart(MessagePacket p)
    {
        // Case 1: Initial transition from Lobby
        if (state == MultiplayerState.Lobby)
        {
            state = MultiplayerState.Loading;
            SceneManager.LoadScene("Playing");
        }
        // Case 2: Synchronized start in Playing scene
        else if (p.serverTime > 0 && state != MultiplayerState.Playing)
        {
            ExecuteStart(p.serverTime);
        }
    }

    private void HandleInput(MessagePacket p)
    {
        if (p.playerId == localPlayerId) return;
        var handler = FindFirstObjectByType<MultiplayerInputHandler>();
        if (handler) handler.ProcessRemoteInput(p.lane, p.hitTime, p.playerId);
    }

    private void HandleScore(MessagePacket p)
    {
        if (p.playerId == localPlayerId || !connectedPlayers.ContainsKey(p.playerId)) return;
        connectedPlayers[p.playerId].score = p.score;
        connectedPlayers[p.playerId].combo = p.combo;
        var handler = FindFirstObjectByType<MultiplayerInputHandler>();
        if (handler) handler.HandleRemoteScoreUpdate(p.playerId, p.score, p.combo, (TimingResult)p.timingResult);
    }

    private void HandleHit(MessagePacket p)
    {
        if (p.playerId == localPlayerId) return;
        var handler = FindFirstObjectByType<MultiplayerInputHandler>();
        if (handler) handler.HandleRemoteNoteHit(p.lane, (TimingResult)p.timingResult, p.beatNumber);
    }

    // --- [복구] API for other scripts ---

    public int GetPlayerSlot()
    {
        List<string> ids = new List<string>(connectedPlayers.Keys);
        ids.Sort();
        for (int i = 0; i < ids.Count; i++) {
            if (ids[i] == localPlayerId) return i;
        }
        return 0;
    }

    public bool HasRequiredPlayers()
    {
        return connectedPlayers.Count >= 2;
    }

    public void SendNoteHit(int lane, TimingResult res, float beatNumber)
    {
        udpManager.SendPacket(MessagePacket.CreateHit(localPlayerId, currentRoomId, lane, (int)res, beatNumber));
    }

    public void JoinRoom(string roomId)
    {
        currentRoomId = roomId;
        connectedPlayers.Clear();
        connectedPlayers.Add(localPlayerId, new PlayerData(localPlayerId));
        state = MultiplayerState.Lobby;
        udpManager.SendPacket(new MessagePacket(PacketType.JoinRoom, localPlayerId, currentRoomId));
    }

    public void SendChartReady()
    {
        state = MultiplayerState.Ready;
        if (connectedPlayers.ContainsKey(localPlayerId)) connectedPlayers[localPlayerId].isChartReady = true;
        CancelInvoke("RepeatChartReady");
        InvokeRepeating("RepeatChartReady", 0f, 0.5f);
    }

    private void RepeatChartReady()
    {
        if (state != MultiplayerState.Ready) { CancelInvoke("RepeatChartReady"); return; }
        udpManager.SendPacket(new MessagePacket(PacketType.PlayerReady, localPlayerId, currentRoomId));
    }

    public void SendGameStartRequest()
    {
        if (!IsAuthority) return;
        udpManager.SendPacket(new MessagePacket(PacketType.GameStart, localPlayerId, currentRoomId));
    }

    public void SendPlayerInput(int lane, float time)
    {
        MessagePacket p = new MessagePacket(PacketType.PlayerInput, localPlayerId, currentRoomId);
        p.lane = lane; p.hitTime = time;
        udpManager.SendPacket(p);
    }

    public void SendPlayerScore(int score, int combo, TimingResult res)
    {
        udpManager.SendPacket(MessagePacket.CreateScore(localPlayerId, currentRoomId, score, combo, (int)res));
    }

    public void SendPlayerReady()
    {
        if (connectedPlayers.ContainsKey(localPlayerId)) connectedPlayers[localPlayerId].isReady = true;
        udpManager.SendPacket(new MessagePacket(PacketType.PlayerReady, localPlayerId, currentRoomId));
    }
}
