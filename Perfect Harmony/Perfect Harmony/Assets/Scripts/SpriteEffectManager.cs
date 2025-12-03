using UnityEngine;
using System.Collections.Generic;

public class SpriteEffectManager : MonoBehaviour
{
    [Header("Performance Mode")]
    [Tooltip("체크 해제 시 Instantiate/Destroy 방식(렉 유발)을 사용하여 성능 차이를 체감할 수 있습니다.")]
    public bool useObjectPooling = true; // 성능 해결 스토리의 핵심 스위치

    [Header("Particle Prefabs")]
    public GameObject hitSpritePrefab;

    // 최적화된 개수 설정
    public int perfectCount = 50;
    public int goodCount = 30;
    public int okayCount = 15;

    public float forceMin = 80f;
    public float forceMax = 200f;

    [Header("Pooling Settings")]
    public int initialPoolSize = 1000; // 1000개면 충분합니다. (50000개는 메모리 낭비)
    public bool canGrow = true;

    [Header("Debug Info")]
    public int currentPoolCount = 0;
    public int activeSpriteCount = 0;

    public static SpriteEffectManager Instance;

    // 오브젝트 풀 (대기열)
    private Queue<HitSprite2D> pool = new Queue<HitSprite2D>();
    
    // **핵심 최적화**: 활성화된 스프라이트를 리스트로 관리하여 중앙에서 업데이트 (CPU 오버헤드 최소화)
    private List<HitSprite2D> activeSprites = new List<HitSprite2D>(1000);
    
    private Transform poolRoot;

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        GameObject rootObj = new GameObject("HitSpritePool");
        rootObj.transform.SetParent(transform);
        poolRoot = rootObj.transform;

        // 풀링 모드일 때만 미리 생성 (Pre-warming)
        if (useObjectPooling)
        {
            ExpandPool(initialPoolSize);
        }
    }

    // **중앙 관리형 업데이트 (Manager-driven Update)**
    // 수천 개의 개별 Monobehaviour Update 호출 비용을 제거하여 성능을 극대화합니다.
    void Update()
    {
        if (!useObjectPooling) return; // 풀링 안 쓰면 각자 알아서 업데이트 하도록 둠

        // 역순회 (리스트에서 요소를 제거해도 인덱스 문제 없도록)
        for (int i = activeSprites.Count - 1; i >= 0; i--)
        {
            HitSprite2D sprite = activeSprites[i];
            
            // 스프라이트 업데이트 실행 (수명이 다하면 false 반환)
            bool isAlive = sprite.ManualUpdate(Time.deltaTime);

            if (!isAlive)
            {
                // 수명 다함 -> 반환
                ReturnSprite(sprite);
                
                // 리스트에서 제거 (Swap & Pop 방식으로 최적화 가능하지만 순서 무관하므로 RemoveAt 사용)
                activeSprites.RemoveAt(i);
            }
        }
        
        activeSpriteCount = activeSprites.Count;
    }

    private void ExpandPool(int count)
    {
        if (hitSpritePrefab == null) return;

        for (int i = 0; i < count; i++)
        {
            HitSprite2D sprite = CreateNewSprite();
            if (sprite != null)
            {
                sprite.gameObject.SetActive(false);
                pool.Enqueue(sprite);
            }
        }
        currentPoolCount = pool.Count;
    }

    private HitSprite2D CreateNewSprite()
    {
        GameObject obj = Instantiate(hitSpritePrefab, poolRoot);
        HitSprite2D hs = obj.GetComponent<HitSprite2D>();
        if (hs == null) hs = obj.AddComponent<HitSprite2D>();
        return hs;
    }

    private HitSprite2D GetSprite()
    {
        HitSprite2D sprite = null;

        while (pool.Count > 0)
        {
            sprite = pool.Dequeue();
            if (sprite != null)
            {
                sprite.gameObject.SetActive(true);
                break;
            }
        }

        if (sprite == null && canGrow)
        {
            sprite = CreateNewSprite();
            sprite.gameObject.SetActive(true);
        }

        currentPoolCount = pool.Count;
        return sprite;
    }

    public void ReturnSprite(HitSprite2D sprite)
    {
        if (sprite == null) return;

        sprite.gameObject.SetActive(false);
        pool.Enqueue(sprite);
        currentPoolCount = pool.Count;
    }

    public void SpawnHitSprites(TimingResult result, Vector3 position)
    {
        int count = result switch
        {
            TimingResult.Perfect => perfectCount,
            TimingResult.Good => goodCount,
            TimingResult.Okay => okayCount,
            _ => 0
        };

        for (int i = 0; i < count; i++)
            SpawnOne(position);
    }

    private void SpawnOne(Vector3 pos)
    {
        Vector3 spawnPos = pos + new Vector3(
            Random.Range(-0.1f, 0.1f),
            Random.Range(-0.1f, 0.1f),
            0
        );
        
        Vector2 dir = Random.insideUnitCircle.normalized; // 랜덤 방향 최적화
        float force = Random.Range(forceMin, forceMax);

        if (useObjectPooling)
        {
            // [성능 해결 스토리: 풀링 사용]
            HitSprite2D hs = GetSprite();
            if (hs != null)
            {
                hs.transform.position = spawnPos;
                hs.Setup(dir * force, true); // true = 매니저가 관리함
                activeSprites.Add(hs);
            }
        }
        else
        {
            // [성능 저하 스토리: Instantiate/Destroy]
            // 매번 생성하고 파괴하는 비용 발생 (렉 유발 원인)
            GameObject obj = Instantiate(hitSpritePrefab, spawnPos, Quaternion.identity);
            HitSprite2D hs = obj.GetComponent<HitSprite2D>();
            if (hs == null) hs = obj.AddComponent<HitSprite2D>();
            
            hs.Setup(dir * force, false); // false = 알아서 업데이트 하다가 죽음
        }
    }
}
