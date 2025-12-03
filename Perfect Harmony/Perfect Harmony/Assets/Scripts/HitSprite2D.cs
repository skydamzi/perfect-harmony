using UnityEngine;

public class HitSprite2D : MonoBehaviour
{
    [Header("General")]
    public float lifetime = 1.5f;

    private SpriteRenderer sr;
    private float startTime;
    private Vector3 velocity;
    private float curveStrength;
    
    private static Sprite sharedSprite;
    private bool isManaged = false; // 매니저가 관리하는지 여부

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        if (sr == null)
        {
            sr = gameObject.AddComponent<SpriteRenderer>();
            
            // 텍스처 공유 (메모리 최적화)
            if (sharedSprite == null)
            {
                Texture2D tex = new Texture2D(4, 4);
                Color[] cols = new Color[16];
                for (int i = 0; i < cols.Length; i++) cols[i] = Color.white;
                tex.SetPixels(cols);
                tex.Apply();
                sharedSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
            }
            
            sr.sprite = sharedSprite;
        }

        transform.localScale = Vector3.one * 0.25f;

        // [성능 스토리 핵심]
        // 실제 게임에서는 프리팹 로드, 컴포넌트 검색, 데이터 파싱 등 "무거운 초기화"가 Awake에서 일어납니다.
        // 풀링을 안 쓰면(Instantiate) 이 무거운 작업이 매번 실행되어 프레임 드랍을 유발합니다.
        // 풀링을 쓰면 이 작업은 처음에만 딱 한 번 실행되고 이후엔 생략됩니다.
        PerformHeavyTask();
    }

    // "무거운 초기화 비용"을 시뮬레이션 하는 함수
    private void PerformHeavyTask()
    {
        // 약 5000번의 삼각함수 연산으로 CPU 부하 발생
        float value = 0f;
        for (int i = 0; i < 5000; i++)
        {
            value += Mathf.Sin(i) * Mathf.Cos(i);
        }
    }

    // 초기화 메서드
    public void Setup(Vector2 force, bool managed)
    {
        velocity = force;
        curveStrength = Random.Range(0.8f, 2.5f);
        startTime = Time.time;
        transform.localScale = Vector3.one * 0.25f;
        isManaged = managed;

        if (sr != null)
        {
            Color c = sr.color;
            c.a = 1f;
            sr.color = c;
        }
    }

    // Unity의 Update 대신 매니저가 호출하는 메서드 (성능 최적화의 핵심)
    // 반환값: true(생존), false(사망)
    public bool ManualUpdate(float deltaTime)
    {
        float elapsed = Time.time - startTime;
        float t = elapsed / lifetime;

        // 이동 로직 (Transform 직접 조작)
        Vector3 perpendicular = new Vector3(velocity.y, -velocity.x, 0).normalized;
        velocity += perpendicular * curveStrength * deltaTime * 5f;
        transform.position += velocity * deltaTime;

        // 크기 및 투명도
        float scale = Mathf.Lerp(0.25f, 0f, t);
        transform.localScale = Vector3.one * scale;

        if (sr != null)
        {
            Color c = sr.color;
            c.a = Mathf.Lerp(1f, 0f, t);
            sr.color = c;
        }

        // 수명이 다했으면 false 반환
        return t < 1f;
    }

    // 풀링을 사용하지 않을 때만 Unity의 Update가 돕니다. (성능 비교용)
    void Update()
    {
        if (isManaged) return; // 매니저가 관리하면 스스로 업데이트하지 않음

        // 스스로 업데이트 수행
        bool isAlive = ManualUpdate(Time.deltaTime);

        if (!isAlive)
        {
            Destroy(gameObject); // 풀링 안 쓰면 그냥 파괴
        }
    }
}