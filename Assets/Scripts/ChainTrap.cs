using System.Collections;
using UnityEngine;

// An iron chain that shoots out of a wall, floor or ceiling on a timer.
// The mount shakes first (warning), then the chain lashes out, hangs for a moment, and pulls back.
// Put it on an object with a SpriteRenderer (the mount plate). The chain is made in code.
[RequireComponent(typeof(SpriteRenderer))]
public class ChainTrap : MonoBehaviour
{
    [SerializeField] Vector2 direction = Vector2.down;
    [SerializeField] float length = 10f;
    [SerializeField] float thickness = 0.45f;

    [Header("Timing")]
    [SerializeField] float startDelay = 0f;
    [SerializeField] float restTime = 1.6f;
    [SerializeField] float warnTime = 0.5f;
    [SerializeField] float holdTime = 0.35f;
    [SerializeField] float extendSpeed = 45f;
    [SerializeField] float retractSpeed = 14f;

    [Header("Damage")]
    [SerializeField] int damage = 12;
    [SerializeField] float knockback = 10f;

    [Header("Look")]
    [SerializeField] Color chainColor = new Color(0.45f, 0.45f, 0.5f);
    [SerializeField] Color warnColor = new Color(1f, 0.4f, 0.2f);

    Transform chain;
    DamageZone zone;
    SpriteRenderer plate;
    Color plateColor;
    Vector3 platePos;

    void Awake()
    {
        plate = GetComponent<SpriteRenderer>();
        plateColor = plate.color;
        platePos = transform.position;

        chain = new GameObject("Chain").transform;
        chain.SetParent(null);
        chain.position = transform.position;
        chain.right = direction.normalized;
        var links = new GameObject("Links");
        links.transform.SetParent(chain, false);
        links.transform.localPosition = new Vector3(0.5f, 0f, 0f);
        links.transform.localScale = new Vector3(1f, thickness, 1f);
        var sr = links.AddComponent<SpriteRenderer>();
        sr.sprite = plate.sprite;
        sr.color = chainColor;
        sr.sortingOrder = plate.sortingOrder - 1;
        var col = links.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        zone = links.AddComponent<DamageZone>();
        zone.Setup(damage, knockback, false, 0, 0f, 1f);
        SetLength(0f);
    }

    void OnEnable() => StartCoroutine(Loop());

    void OnDestroy() { if (chain) Destroy(chain.gameObject); }

    IEnumerator Loop()
    {
        yield return new WaitForSeconds(startDelay);
        while (true)
        {
            yield return new WaitForSeconds(restTime);

            // Warning: the mount shakes and glows
            for (float t = 0f; t < warnTime; t += Time.deltaTime)
            {
                plate.color = Mathf.PingPong(t * 10f, 1f) > 0.5f ? warnColor : plateColor;
                transform.position = platePos + (Vector3)(Random.insideUnitCircle * 0.06f);
                yield return null;
            }
            plate.color = plateColor;
            transform.position = platePos;

            float len = 0f;
            while (len < length) { len = Mathf.MoveTowards(len, length, extendSpeed * Time.deltaTime); SetLength(len); yield return null; }
            yield return new WaitForSeconds(holdTime);
            while (len > 0f) { len = Mathf.MoveTowards(len, 0f, retractSpeed * Time.deltaTime); SetLength(len); yield return null; }
        }
    }

    void SetLength(float len)
    {
        chain.localScale = new Vector3(Mathf.Max(len, 0.001f), 1f, 1f);
        chain.gameObject.SetActive(len > 0.05f);
        zone.Active = len > 0.3f;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawLine(transform.position, transform.position + (Vector3)(direction.normalized * length));
    }
}