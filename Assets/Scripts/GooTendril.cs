using UnityEngine;

// Venom jaisa goo tentacle. Deewar/chhat se timer pe bahar nikalta hai.
// Pehle chamakta hai (warning), phir lamba nikalta hai, chhoo liya toh damage + dhakka.
// Object pe SpriteRenderer + BoxCollider2D (Is Trigger ON) chahiye. Object ki position = jahan se nikalta hai.
[RequireComponent(typeof(SpriteRenderer), typeof(BoxCollider2D))]
public class GooTendril : MonoBehaviour
{
    [SerializeField] Vector2 direction = Vector2.down; // kis taraf nikle
    [SerializeField] float length = 8f;
    [SerializeField] float thickness = 0.6f;

    [Header("Timing")]
    [SerializeField] float startDelay = 0f;  // alag alag tendrils ko alag time pe chalane ke liye
    [SerializeField] float restTime = 1.5f;  // andar chupa rehta hai
    [SerializeField] float warnTime = 0.6f;  // chamakta hai
    [SerializeField] float warnPeek = 0.4f;  // warning mein kitna bahar jhaanke
    [SerializeField] float warnFlashSpeed = 8f;
    [SerializeField] float outTime = 0.8f;   // bahar rehta hai
    [SerializeField] float extendSpeed = 40f;

    [Header("Damage")]
    [SerializeField] int damage = 1;
    [SerializeField] float knockback = 10f;
    [SerializeField] float knockbackUp = 0.5f; // dhakke mein upar kitna (knockback ka hissa)

    [Header("Colors")]
    [SerializeField] Color gooColor = new Color(0.08f, 0.05f, 0.12f);
    [SerializeField] Color warnColor = new Color(0.6f, 0.1f, 0.9f);

    enum State { Rest, Warn, Out }

    State state = State.Rest;
    Vector3 basePos;
    SpriteRenderer sr;
    BoxCollider2D col;
    float current;
    float timer;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        col = GetComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = Vector2.one;
        direction = direction.normalized;
        basePos = transform.position;
        transform.right = direction; // lambai direction ki taraf
        timer = -startDelay;
        SetLength(0f);
    }

    void Update()
    {
        timer += Time.deltaTime;
        float target = 0f;

        switch (state)
        {
            case State.Rest:
                if (timer >= restTime) { state = State.Warn; timer = 0f; }
                break;
            case State.Warn:
                target = warnPeek; // chhota sa bahar jhaankta hai
                if (timer >= warnTime) { state = State.Out; timer = 0f; }
                break;
            case State.Out:
                target = length;
                if (timer >= outTime) { state = State.Rest; timer = 0f; }
                break;
        }

        current = Mathf.MoveTowards(current, target, extendSpeed * Time.deltaTime);
        SetLength(current);

        col.enabled = state == State.Out && current > 0.5f;
        sr.color = state == State.Warn && Mathf.PingPong(timer * warnFlashSpeed, 1f) > 0.5f ? warnColor : gooColor;
    }

    void SetLength(float l)
    {
        transform.localScale = new Vector3(Mathf.Max(l, 0.001f), thickness, 1f);
        transform.position = basePos + (Vector3)(direction * l * 0.5f);
        sr.enabled = l > 0.01f;
    }

    void OnTriggerStay2D(Collider2D other)
    {
        if (!other.TryGetComponent(out PlayerController2D _) || !other.TryGetComponent(out Health health)) return;

        int before = health.CurrentHealth;
        health.TakeDamage(damage);

        // Damage sach mein laga (dash ke time nahi lagta), tabhi dhakka
        if (health.CurrentHealth < before && other.attachedRigidbody)
            other.attachedRigidbody.linearVelocity = direction * knockback + Vector2.up * knockback * knockbackUp;
    }
}
