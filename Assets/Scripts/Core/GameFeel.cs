using System.Collections;
using UnityEngine;

// Makes hits feel strong:
//   Hit-stop    - when YOUR punch or hook lands, the game freezes for a split second (longer on a finishing blow).
//   Screen shake - when YOU get hurt by a BOSS (any hit in a boss fight) or take FALL damage, the camera shakes
//                  (harder for bigger hits). Normal enemies, hazards and your own attacks don't shake it.
// Called from PlayerCombat (hits) and Health (player hurt). Screen shake can be turned off (ShakeOn).
public static class GameFeel
{
    // ---- Tuning ----
    const float HitStopTime = 0.065f;    // seconds the game freezes when a hit lands
    const float KillStopTime = 0.12f;    // ...and when the hit finishes an enemy off
    const float HitStopSpeed = 0.05f;    // how slow "frozen" is (almost stopped; exactly 0 would count as "paused")
    const float HurtShakeTime = 0.22f;
    const float HurtShakeMin = 0.18f;    // shake strength for a tiny hit...
    const float HurtShakeMax = 0.5f;     // ...up to this for a hit worth a quarter of your health or more

    const string ShakeSave = "settings.shake";
    public static bool ShakeOn
    {
        get => PlayerPrefs.GetInt(ShakeSave, 1) == 1;
        set { PlayerPrefs.SetInt(ShakeSave, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    static Runner runner;
    static float stopUntil;
    static bool announced;

    // One line in the Console the first time, so you can see it's working
    static void Announce(string what)
    {
        if (announced) return;
        announced = true;
        Debug.Log("GameFeel is working: " + what);
    }

    // Your hit landed on an enemy (killed = it died from it)
    public static void HitLanded(bool killed)
    {
        if (Time.timeScale == 0f) return; // paused or in a menu: leave time alone
        float t = killed ? KillStopTime : HitStopTime;
        stopUntil = Mathf.Max(stopUntil, Time.unscaledTime + t);
        Time.timeScale = HitStopSpeed;
        Run().StartStop();
        Announce(killed ? "finishing blow freeze" : "hit freeze");
    }

    // You got hurt (damage = how much, maxHealth = your max): shakes only during a boss fight
    public static void PlayerHurt(int damage, int maxHealth)
    {
        if (!BossAround()) return;
        Shake(damage, maxHealth);
    }

    // You hit the ground too hard
    public static void FallHurt(int damage, int maxHealth) => Shake(damage, maxHealth);

    static void Shake(int damage, int maxHealth)
    {
        if (!ShakeOn || !CameraFollow2D.Instance) return;
        float share = maxHealth > 0 ? Mathf.Clamp01(damage / (maxHealth * 0.25f)) : 0.5f;
        CameraFollow2D.Instance.Shake(HurtShakeTime, Mathf.Lerp(HurtShakeMin, HurtShakeMax, share));
        Announce("hurt shake");
    }

    // Is one of the four bosses here and still fighting?
    static bool BossAround()
    {
        return Alive(Object.FindAnyObjectByType<HardyBoss>()) || Alive(Object.FindAnyObjectByType<PoisonBoss>())
            || Alive(Object.FindAnyObjectByType<OctopusBoss>()) || Alive(Object.FindAnyObjectByType<FatherBoss>());
    }

    static bool Alive(Component boss) => boss && (!boss.TryGetComponent(out Health h) || h.CurrentHealth > 0);

    static Runner Run()
    {
        if (!runner)
        {
            var go = new GameObject("GameFeel");
            Object.DontDestroyOnLoad(go);
            runner = go.AddComponent<Runner>();
        }
        return runner;
    }

    // Puts time back to normal after a hit-stop (unless the game got paused meanwhile)
    class Runner : MonoBehaviour
    {
        bool running;

        public void StartStop()
        {
            if (!running) StartCoroutine(Stop());
        }

        IEnumerator Stop()
        {
            running = true;
            while (Time.unscaledTime < stopUntil) yield return null;
            if (Mathf.Approximately(Time.timeScale, HitStopSpeed)) Time.timeScale = 1f; // still our slow-down: undo it
            running = false;
        }

        void OnDisable()
        {
            if (Mathf.Approximately(Time.timeScale, HitStopSpeed)) Time.timeScale = 1f;
            running = false;
        }
    }
}