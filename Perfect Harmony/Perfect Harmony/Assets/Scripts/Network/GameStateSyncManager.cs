using UnityEngine;

public class GameStateSyncManager : MonoBehaviour
{
    // [리팩토링] 이 스크립트는 이제 더 이상 사용되지 않습니다.
    // 모든 동기화 로직은 TimingSyncManager와 MultiplayerManager로 이전되었습니다.
    // 기존 코드와의 호환성을 위해 클래스만 남겨두며, 기능은 모두 정지되었습니다.

    public static GameStateSyncManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else { Instance = this; DontDestroyOnLoad(gameObject); }
    }

    public void RefreshReferences() { /* Do nothing */ }
    public void SendNoteSpawn(MessagePacket p) { /* Do nothing */ }
}
