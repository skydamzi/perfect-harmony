using UnityEngine;
using System.Collections;

public class FallingNote : MonoBehaviour
{
    [Header("Note Properties")]
    public NoteLane lane;
    public float beatNumber;
    public float spawnTime;
    public float targetTime; // 이건 이제 계산식에서 활용함

    [Header("Movement")]
    public float fallSpeed = 5.0f;
    public Transform targetPosition;
    public Transform spawnPosition;

    public bool isHit = false;
    public bool isMissed = false;

    void Start()
    {
        spawnTime = Time.realtimeSinceStartup;

        // 시작 위치 초기화
        if (spawnPosition != null)
        {
            transform.position = spawnPosition.position;
        }
    }

    void Update()
    {
        // 1. 카운트다운 중이거나 플레이 중일 때만 움직임
        bool canMove = RhythmGameManager.Instance.isPlaying || RhythmGameManager.Instance.isCountingDown;

        if (canMove && !isHit && !isMissed)
        {
            float currentSongTime = RhythmGameManager.Instance.songPosition;
            float noteTargetTime = RhythmGameManager.Instance.BeatToTime(beatNumber);
            float travelDuration = RhythmGameManager.Instance.spawnOffset;

            // 진행도 계산 (0: 스폰지점, 1: 판정선)
            float progress = (currentSongTime - (noteTargetTime - travelDuration)) / travelDuration;

            if (spawnPosition != null && targetPosition != null)
            {
                // LerpUnclamped를 써야 판정선을 지나쳐도 자연스럽게 내려감
                transform.position = Vector3.LerpUnclamped(spawnPosition.position, targetPosition.position, progress);
            }

            // 판정선을 너무 많이 지나치면 미스 처리
            if (currentSongTime > noteTargetTime + RhythmGameManager.Instance.okayWindow)
            {
                MissNote();
            }
        }
    }

    // --- InputHandler 에러 해결용 함수 ---
    public void HitNote(TimingResult timingResult)
    {
        OnNoteHit(timingResult);
    }

    public void OnNoteHit(TimingResult timingResult)
    {
        if (!isHit && !isMissed)
        {
            isHit = true;

            if (SpriteEffectManager.Instance != null)
                SpriteEffectManager.Instance.SpawnHitSprites(timingResult, transform.position);

            if (InputHandler.Instance != null)
                InputHandler.Instance.UnregisterNote(this);

            if (RhythmGameController.Instance != null)
                RhythmGameController.Instance.OnNoteHit(timingResult, this);

            if (MultiplayerManager.Instance != null && MultiplayerManager.Instance.gameStarted)
                MultiplayerManager.Instance.SendNoteHit((int)lane, timingResult, beatNumber);

            SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                Color hitColor = Color.white;
                switch (timingResult)
                {
                    case TimingResult.Perfect: hitColor = Color.yellow; break;
                    case TimingResult.Good: hitColor = Color.green; break;
                    case TimingResult.Okay: hitColor = Color.blue; break;
                }
                StartCoroutine(AnimateNoteHit(spriteRenderer, hitColor));
            }
            else
            {
                Destroy(gameObject, 0.1f);
            }
        }
    }

    private IEnumerator AnimateNoteHit(SpriteRenderer spriteRenderer, Color hitColor)
    {
        float duration = 0.2f;
        float elapsed = 0f;
        Vector3 initialScale = transform.localScale;
        spriteRenderer.color = hitColor;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float newY = Mathf.Lerp(initialScale.y, 0f, t);
            transform.localScale = new Vector3(initialScale.x, newY, initialScale.z);
            yield return null;
        }
        Destroy(gameObject);
    }

    public void MissNote()
    {
        if (isHit || isMissed) return;
        isMissed = true;

        if (InputHandler.Instance != null)
            InputHandler.Instance.UnregisterNote(this);

        if (RhythmGameController.Instance != null)
            RhythmGameController.Instance.OnNoteMissed(this);

        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null) spriteRenderer.color = Color.red;

        Destroy(gameObject, 0.1f);
    }
}