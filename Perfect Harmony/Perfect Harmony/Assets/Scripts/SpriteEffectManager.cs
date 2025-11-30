using UnityEngine;

public class SpriteEffectManager : MonoBehaviour
{
    public GameObject hitSpritePrefab;

    public int perfectCount = 60;
    public int goodCount = 40;
    public int okayCount = 20;

    public float forceMin = 80f;
    public float forceMax = 200f;

    public static SpriteEffectManager Instance;

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
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
        if (hitSpritePrefab == null)
        {
            Debug.LogWarning("hitSpritePrefab is missing.");
            return;
        }

        Vector3 spawnPos = pos + new Vector3(
            Random.Range(-0.1f, 0.1f),
            Random.Range(-0.1f, 0.1f),
            0
        );

        GameObject obj = Instantiate(hitSpritePrefab, spawnPos, Quaternion.identity);

        HitSprite2D hs = obj.GetComponent<HitSprite2D>();
        if (hs == null) hs = obj.AddComponent<HitSprite2D>();

        float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        float dirX = Mathf.Cos(angle);
        float dirY = Mathf.Sin(angle);
        Vector2 dir = new Vector2(dirX, dirY);

        float force = Random.Range(forceMin, forceMax);

        hs.Initialize(dir * force);
    }
}
