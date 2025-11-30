using UnityEngine;

public class HitSprite2D : MonoBehaviour
{
    [Header("General")]
    public float lifetime = 1.5f;

    private Rigidbody2D rb;
    private SpriteRenderer sr;
    private float startTime;

    private Vector2 initialVelocity;
    private float curveStrength;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody2D>();

        rb.gravityScale = 4.0f;
        rb.mass = 0.05f;
        rb.linearDamping = 0f;
        rb.angularDamping = 0f;
        rb.freezeRotation = true;

        sr = GetComponent<SpriteRenderer>();
        if (sr == null)
        {
            sr = gameObject.AddComponent<SpriteRenderer>();
            Texture2D tex = new Texture2D(4, 4);
            Color[] cols = new Color[16];
            for (int i = 0; i < cols.Length; i++) cols[i] = Color.white;
            tex.SetPixels(cols);
            tex.Apply();
            sr.sprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
        }

        transform.localScale = Vector3.one * 0.25f;

        startTime = Time.time;
    }

    public void Initialize(Vector2 force)
    {
        initialVelocity = force;
        curveStrength = Random.Range(0.8f, 2.5f); // 곡선 커브 강도
        rb.linearVelocity = initialVelocity;
    }

    void Update()
    {
        float t = (Time.time - startTime) / lifetime;

        Vector2 perpendicular = new Vector2(rb.linearVelocity.y, -rb.linearVelocity.x).normalized;
        rb.linearVelocity += perpendicular * curveStrength * Time.deltaTime * 5f;

        float scale = Mathf.Lerp(0.25f, 0f, t);
        transform.localScale = Vector3.one * scale;

        if (sr != null)
        {
            Color c = sr.color;
            c.a = Mathf.Lerp(1f, 0f, t);
            sr.color = c;
        }

        if (t >= 1f)
        {
            Destroy(gameObject);
        }
    }
}
