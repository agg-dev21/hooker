using UnityEngine;
using UnityEngine.SceneManagement;

// Fall damage for the player. Adds itself to the player in every scene (no need to add it by hand).
// Remembers the highest point of each fall and hurts the player on landing if the drop was too big.
// The numbers are in Assets/Resources/FallDamageSettings (one place for the whole game).
public class PlayerFallDamage : MonoBehaviour
{
    const float TeleportDistance = 6f; // moved this far in one frame = teleported (respawn, scene start): not a fall

    PlayerController2D controller;
    Health health;
    float highestY;
    bool wasGrounded = true;
    Vector3 lastPosition;

    FallDamageSettings S => FallDamageSettings.Current;

    // ---------------- Adds itself to the player in every scene ----------------

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded; // never hook it twice
        SceneManager.sceneLoaded += OnSceneLoaded;
        AddToPlayer();
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => AddToPlayer();

    static void AddToPlayer()
    {
        PlayerController2D pc = FindAnyObjectByType<PlayerController2D>();
        if (pc && !pc.GetComponent<PlayerFallDamage>()) pc.gameObject.AddComponent<PlayerFallDamage>();
    }

    // ---------------- Falling ----------------

    void Awake()
    {
        controller = GetComponent<PlayerController2D>();
        health = GetComponent<Health>();
    }

    void OnEnable()
    {
        PlayerRespawn.Respawned += ResetFall;
        ResetFall();
    }

    void OnDisable() => PlayerRespawn.Respawned -= ResetFall;

    void ResetFall()
    {
        highestY = transform.position.y;
        lastPosition = transform.position;
        wasGrounded = true;
    }

    void Update()
    {
        if (!controller || !health) return;
        Vector3 pos = transform.position;
        if ((pos - lastPosition).sqrMagnitude > TeleportDistance * TeleportDistance) ResetFall();
        lastPosition = pos;

        bool grounded = controller.IsGrounded;
        if (!grounded)
        {
            if (wasGrounded) highestY = pos.y;        // just left the ground
            highestY = Mathf.Max(highestY, pos.y);    // jumps and hook launches raise the top of the fall
        }
        else if (!wasGrounded)
        {
            Land(highestY - pos.y);
        }
        wasGrounded = grounded;
    }

    void Land(float fallHeight)
    {
        if (!S.fallDamageOn || fallHeight <= S.safeHeight) return;
        int damage = Mathf.Clamp(Mathf.CeilToInt((fallHeight - S.safeHeight) * S.damagePerTile), S.minDamage, S.maxDamage);

        if (health.IsInvincible)
        {
            if (S.dashDodgesFallDamage) return;
            health.TakeTickDamage(damage); // still hurts during invincibility
        }
        else health.TakeDamage(damage);
        GameFeel.FallHurt(damage, health.MaxHealth); // screen shake on a hard landing
    }
}