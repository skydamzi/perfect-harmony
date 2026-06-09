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
    public Dictionary<string, PlayerData> players = new Dictionary<string, PlayerData>();

    [Header("State")]
    public MultiplayerState state = MultiplayerState.Lobby;

    public class PlayerData
    {
        public string id;
        public bool isReady;
        public bool isChartReady;
        public int score;
        public int combo;
        public PlayerData(string id) { this.id = id; }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else { Instance = this; DontDestroyOnLoad(gameObject); }
    }

    private void Start()
    {
        if (udpManager == null) udpManager = FindFirstObjectByType<UDPManager>();
        udpManager.OnPacketReceived += HandlePacket;
        
        localPlayerId = SystemInfo.deviceUniqueIdentifier + "_" + Random.Range(0, 10000);
        players.Add(localPlayerId, new PlayerData(localPlayerId));
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
            List<string> ids = new List<string>(players.Keys);
            ids.Sort();
            return ids.Count > 0 && ids[0] == localPlayerId;
        }
    }

    private void CheckAndRequestStart()
    {
        bool allReady = true;
        foreach (var p in players.Values) if (!p.isChartReady) allReady = false;

        // 동기화도 완료되어야 함
        if (allReady && TimingSyncManager.Instance.IsSynced && players.Count >= 2)
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
                if (!players.ContainsKey(p.playerId))
                {
                    players[p.playerId] = new PlayerData(p.playerId);
                    udpManager.SendPacket(new MessagePacket(PacketType.Connect, localPlayerId, currentRoomId));
                }
                break;

            case PacketType.PlayerReady:
                if (players.ContainsKey(p.playerId))
                {
                    if (SceneManager.GetActiveScene().name == "Playing") players[p.playerId].isChartReady = true;
                    else players[p.playerId].isReady = true;
                }
                break;

            case PacketType.GameStart:
                // 서버가 보내준 절대 시작 시각(serverTime) 수신
                if (p.serverTime > 0)
                {
                    ExecuteSyncStart(p.serverTime);
                }
                else if (state == MultiplayerState.Lobby)
                {
                    state = MultiplayerState.Loading;
                    SceneManager.LoadScene("Playing");
                }
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

    public void JoinRoom(string roomId)
    {
        currentRoomId = roomId;
        players.Clear();
        players.Add(localPlayerId, new PlayerData(localPlayerId));
        state = MultiplayerState.Lobby;
        udpManager.SendPacket(new MessagePacket(PacketType.JoinRoom, localPlayerId, currentRoomId));
    }

    public void SendChartReady()
    {
        state = MultiplayerState.Ready;
        players[localPlayerId].isChartReady = true;
        CancelInvoke("RepeatReady");
        InvokeRepeating("RepeatReady", 0f, 0.5f);
    }

    private void RepeatReady()
    {
        if (state != MultiplayerState.Ready) { CancelInvoke("RepeatReady"); return; }
        udpManager.SendPacket(new MessagePacket(PacketType.PlayerReady, localPlayerId, currentRoomId));
    }
}
