using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MultiplayerManager : MonoBehaviour
{
    public static MultiplayerManager Instance { get; private set; }

    [Header("Identity")]
    public string localPlayerId;
    public string currentRoomId = "Lobby";
    public string playerName;

    [Header("Network")]
    public UDPManager udpManager;
    public string selectedInstrument = "Piano"; // [복구]
    public Dictionary<string, PlayerData> connectedPlayers = new Dictionary<string, PlayerData>(); // [복구] 명칭 변경

    [Header("State")]
    public MultiplayerState state = MultiplayerState.Lobby;

    public bool gameStarted 
    { 
        get { return state != MultiplayerState.Lobby; }
        set { if (value) state = MultiplayerState.Loading; else state = MultiplayerState.Lobby; }
    }

    public class PlayerData
    {
        public string playerId;   // [복구] 기존 명칭
        public string playerName; // [복구] 기존 명칭
        public bool isReady;
        public bool isChartReady;
        public int score;
        public int combo;

        public PlayerData(string id) { 
            this.playerId = id; 
            this.playerName = "Player_" + id.Substring(0, Mathf.Min(4, id.Length));
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
        // 호스트는 모두가 차트 준비되었는지 체크
        if (IsAuthority && state == MultiplayerState.Ready)
        {
            CheckAndRequestStart();
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

    private void CheckAndRequestStart()
    {
        bool allReady = true;
        foreach (var p in connectedPlayers.Values) if (!p.isChartReady) allReady = false;

        // 동기화도 완료되어야 함
        if (allReady && TimingSyncManager.Instance.IsSynced && connectedPlayers.Count >= 2)
        {
            // 서버에게 "우리 이제 시작할게!" 라고 요청
            MessagePacket p = new MessagePacket(PacketType.GameStart, localPlayerId, currentRoomId);
            udpManager.SendPacket(p);
        }
    }

    private void HandlePacket(MessagePacket p, System.Net.IPEndPoint sender, double arrivalTimestamp)
    {
        if (!string.IsNullOrEmpty(p.roomId) && p.roomId != currentRoomId && p.roomId != "Global") return;

        switch (p.type)
        {
            case PacketType.Connect:
            case PacketType.JoinRoom:
                if (!connectedPlayers.ContainsKey(p.playerId))
                {
                    connectedPlayers[p.playerId] = new PlayerData(p.playerId);
                    udpManager.SendPacket(new MessagePacket(PacketType.Connect, localPlayerId, currentRoomId));
                }
                break;

            case PacketType.PlayerReady:
                if (connectedPlayers.ContainsKey(p.playerId))
                {
                    if (SceneManager.GetActiveScene().name == "Playing") connectedPlayers[p.playerId].isChartReady = true;
                    else connectedPlayers[p.playerId].isReady = true;
                }
                break;

            case PacketType.GameStart:
                // [1단계] 로비에 있는 경우: 씬 전환을 최우선으로 수행
                if (state == MultiplayerState.Lobby)
                {
                    state = MultiplayerState.Loading;
                    SceneManager.LoadScene("Playing");
                }
                // [2단계] 이미 Playing 씬에 들어와서 준비(Ready) 상태인 경우에만 정밀 동기화 시작
                else if (p.serverTime > 0 && state == MultiplayerState.Ready)
                {
                    ExecuteSyncStart(p.serverTime);
                }
                break;
                
            case PacketType.PlayerScore:
                if (connectedPlayers.ContainsKey(p.playerId))
                {
                    connectedPlayers[p.playerId].score = p.score;
                    connectedPlayers[p.playerId].combo = p.combo;
                }
                break;

            case PacketType.PlayerInput:
                // 1대1 상황에서 상대방의 입력 시각 등을 시각화할 때 사용 가능
                break;

            case PacketType.NoteHit:
                var input = FindFirstObjectByType<MultiplayerInputHandler>();
                if (input) input.HandleRemoteNoteHit(p.lane, (TimingResult)p.timingResult, p.beatNumber);
                break;
        }
    }

    private void ExecuteSyncStart(double serverStartTime)
    {
        if (state == MultiplayerState.Playing) return;
        state = MultiplayerState.Playing;
        
        if (RhythmGameManager.Instance != null)
        {
            RhythmGameManager.Instance.StartSyncCountdown(serverStartTime);
        }
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
        CancelInvoke("RepeatReady");
        InvokeRepeating("RepeatReady", 0f, 0.5f);
    }

    private void RepeatReady()
    {
        if (state != MultiplayerState.Ready) { CancelInvoke("RepeatReady"); return; }
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
