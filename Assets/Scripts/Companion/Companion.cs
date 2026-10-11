using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The companion you free from the jail in Level 2. Once freed she stays with you for the rest of the game
// (saved, every scene). If you never find her, she is gone for good.
//  - She floats behind you in calm places.
//  - When enemies, obstacles or a boss are near, she fades away into sparkles, and comes back when it is safe.
//  - Press Companion (R / RS click by default, changeable in Controls): she appears and calls down a column of
//    light on the nearest enemy in front of you. It hurts every enemy and boss inside it.
//    Then she fades away again if it is still dangerous.
// All the numbers are in Assets/Resources/CompanionSettings.asset. Nothing needs to be set up in scenes.
public class Companion : MonoBehaviour
{
    public const string FoundSave = "companion.found";
    public static bool Found => PlayerPrefs.GetInt(FoundSave, 0) == 1;

    // Freed from the jail: she joins you from now on. Pass where she is standing so she starts there.
    public static void Join(Vector3 at)
    {
        PlayerPrefs.SetInt(FoundSave, 1);
        PlayerPrefs.SetInt(FoundSave + ".new", 1);
        PlayerPrefs.Save();
        Spawn(at);
    }

    // Keep her out (not hiding) for at least this long, e.g. while she is talking
    public static void StayFor(float seconds)
    {
        Companion c = FindAnyObjectByType<Companion>();
        if (c) c.stayUntil = Time.time + seconds;
    }

    // Testing: forget her
    public static void Leave()
    {
        PlayerPrefs.DeleteKey(FoundSave);
        PlayerPrefs.DeleteKey(FoundSave + ".new");
        PlayerPrefs.Save();
        Companion c = FindAnyObjectByType<Companion>();
        if (c) Destroy(c.gameObject);
    }

    // ---------------- Appears in every scene once found ----------------

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded; // never hook it twice
        SceneManager.sceneLoaded += OnSceneLoaded;
        if (Found) Spawn(null);
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (Found) Spawn(null);
    }

    static void Spawn(Vector3? at)
    {
        if (FindAnyObjectByType<Companion>()) return;
        if (SceneManager.GetActiveScene().name.StartsWith("Tutorial")) return; // never in the tutorial
        PlayerController2D pc = FindAnyObjectByType<PlayerController2D>();
        if (!pc) return;
        var go = new GameObject("Companion");
        go.transform.position = at ?? pc.transform.position + new Vector3(-1.5f, 0.5f, 0f);
        go.AddComponent<Companion>();
    }

    // ---------------- Setup ----------------

    CompanionSettings S => CompanionSettings.Current;
    bool HasArt => S.idleFrames != null && S.idleFrames.Length > 0;
    bool Drawn => HasArt || S.sprite;

    PlayerController2D player;
    Collider2D playerCol;
    SpriteRenderer body;
    Light2D glow;
    Vector3 velocity;
    float visible = 1f;       // 1 = fully shown, 0 = gone
    bool hidden;
    bool attacking;
    float safeTimer;
    float checkTimer;
    float cooldownLeft;
    float idleTime, appearTime = 99f;
    float stayUntil; // no hiding before this time

    GameObject iconRoot;
    Image cooldownSweep;
    TMP_Text keyText, newText;
    static Sprite whiteSprite;

    void Awake()
    {
        if (RunOrder.InTutorial) { Destroy(gameObject); return; } // never in the tutorial rooms
        if (!whiteSprite) whiteSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
        player = FindAnyObjectByType<PlayerController2D>();
        if (player) playerCol = player.GetComponent<Collider2D>();

        var look = new GameObject("Body");
        look.transform.SetParent(transform, false);
        body = look.AddComponent<SpriteRenderer>();
        body.sprite = HasArt ? S.idleFrames[0] : S.sprite ? S.sprite : whiteSprite;
        body.color = Drawn ? Color.white : S.color;
        body.sortingOrder = 4;
        if (!Drawn) look.transform.localScale = new Vector3(S.size.x, S.size.y, 1f);
        appearTime = 0f; // pops in with the appearing animation
        stayUntil = Time.time + S.joinStayTime;

        glow = new GameObject("Glow").AddComponent<Light2D>();
        glow.transform.SetParent(transform, false);
        glow.lightType = Light2D.LightType.Point;
        glow.color = S.glowColor;
        glow.intensity = 0.8f;
        glow.pointLightOuterRadius = 3.5f;
        glow.pointLightInnerRadius = 0.3f;

        BuildIcon();

        // A boss room: she starts hidden (no free 5 seconds out)
        foreach (MonoBehaviour mb in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            if (mb is IChainStunnable && mb.TryGetComponent(out Health bh) && bh.CurrentHealth > 0)
            {
                hidden = true;
                visible = 0f;
                stayUntil = 0f;
                break;
            }
    }

    void OnDestroy()
    {
        if (iconRoot) Destroy(iconRoot);
        if (cursor) Destroy(cursor.gameObject);
        IsAiming = false;
    }

    // ---------------- Every frame ----------------

    void Update()
    {
        if (!body) return; // removed (tutorial room)
        if (!player) { player = FindAnyObjectByType<PlayerController2D>(); return; }

        Follow();

        // Danger nearby? Check a few times a second.
        checkTimer -= Time.deltaTime;
        if (checkTimer <= 0f && !attacking)
        {
            checkTimer = 0.25f;
            bool danger = DangerNear();
            if (danger) { safeTimer = 0f; if (!hidden && Time.time >= stayUntil) Hide(true); }
            else if (hidden)
            {
                safeTimer += 0.25f;
                if (safeTimer >= S.safeTimeToReturn) Hide(false);
            }
        }

        // Fade in / out
        float target = hidden && !attacking ? 0f : 1f;
        visible = Mathf.MoveTowards(visible, target, Time.deltaTime / Mathf.Max(S.fadeTime, 0.01f));
        Color c = body.color; c.a = visible; body.color = c;
        body.transform.localScale = Drawn ? Vector3.one * S.drawScale * Mathf.Lerp(0.6f, 1f, visible)
                                          : new Vector3(S.size.x, S.size.y * Mathf.Lerp(0.3f, 1f, visible), 1f);
        if (HasArt) Animate();
        glow.intensity = 0.8f * visible;

        // Light beam
        if (cooldownLeft > 0f) cooldownLeft -= Time.deltaTime;
        UpdateIcon();
        if (Time.timeScale == 0f || attacking || cooldownLeft > 0f) return;
        if (!player.enabled || player.IsRooted) return;
        if (GameInput.CompanionPressed) StartCoroutine(LightBeam());
    }

    void Follow()
    {
        float facing = player.FacingDirection;
        Vector3 target = player.transform.position + new Vector3(-facing * S.followDistance, S.followHeight, 0f);
        target.y += Mathf.Sin(Time.time * S.bobSpeed) * S.bobHeight;
        transform.position = Vector3.SmoothDamp(transform.position, target, ref velocity, S.followSmoothTime);
        if (Drawn) body.flipX = facing < 0f;
    }

    // Idle float, or the appearing animation right after she pops in
    void Animate()
    {
        appearTime += Time.deltaTime;
        int appearCount = S.appearFrames != null ? S.appearFrames.Length : 0;
        int a = Mathf.FloorToInt(appearTime * Mathf.Max(S.appearFps, 0.01f));
        if (a < appearCount) { body.sprite = S.appearFrames[a]; return; }
        idleTime += Time.deltaTime;
        body.sprite = S.idleFrames[Mathf.FloorToInt(idleTime * Mathf.Max(S.idleFps, 0.01f)) % S.idleFrames.Length];
    }

    // Enemies, obstacles and bosses within the danger radius (pits and walls don't count)
    bool DangerNear()
    {
        // A boss fight: she always stays hidden while any boss is still standing
        foreach (MonoBehaviour mb in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            if (mb is IChainStunnable && mb.TryGetComponent(out Health bh) && bh.CurrentHealth > 0) return true;

        int ground = LayerMask.GetMask("Ground");
        Vector2 me = player.transform.position;
        foreach (Collider2D hit in Physics2D.OverlapCircleAll(me, S.dangerRadius))
        {
            if (hit.transform.IsChildOf(player.transform) || hit.transform.IsChildOf(transform)) continue;
            if (S.ignoreDangerBehindWalls && BehindWall(me, hit, ground)) continue;
            if (hit.GetComponentInParent<IChainStunnable>() != null) return true; // a boss
            if (hit.GetComponentInParent<PoisonHazard>() || hit.GetComponentInParent<DamageZone>()
                || hit.GetComponentInParent<ContactDamage>() || hit.GetComponentInParent<EnemyChase>()
                || hit.GetComponentInParent<EnemyShooter>() || hit.GetComponentInParent<ChainTrap>()
                || hit.GetComponentInParent<ExplodingBarrel>() || hit.GetComponentInParent<PoisonPendulum>())
                return true;
            Health h = hit.GetComponentInParent<Health>();
            if (h && h.CurrentHealth > 0 && !h.GetComponent<PlayerController2D>() && !h.GetComponent<BreakableWall>()) return true;
        }
        return false;
    }

    // Is there a wall or floor between the player and this thing? (Its own collider doesn't count.)
    static bool BehindWall(Vector2 from, Collider2D target, int ground)
    {
        foreach (RaycastHit2D h in Physics2D.LinecastAll(from, target.bounds.center, ground))
            if (h.collider != target && !h.collider.transform.IsChildOf(target.transform.root)) return true;
        return false;
    }

    // Fades away (or back) with a burst of sparkles
    void Hide(bool hide)
    {
        if (hidden == hide) return;
        hidden = hide;
        if (!hide) { appearTime = 0f; stayUntil = Time.time + S.minShowTime; }
        StartCoroutine(Sparkles(12));
    }

    IEnumerator Sparkles(int count)
    {
        var bits = new Transform[count];
        var dirs = new Vector2[count];
        var srs = new SpriteRenderer[count];
        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("Sparkle");
            go.transform.position = transform.position + (Vector3)(Random.insideUnitCircle * 0.5f);
            go.transform.localScale = Vector3.one * Random.Range(0.08f, 0.16f);
            srs[i] = go.AddComponent<SpriteRenderer>();
            srs[i].sprite = whiteSprite;
            srs[i].color = S.glowColor;
            srs[i].sortingOrder = 6;
            bits[i] = go.transform;
            dirs[i] = new Vector2(Random.Range(-1f, 1f), Random.Range(0.5f, 2f));
        }
        for (float t = 0f; t < 0.7f; t += Time.deltaTime)
        {
            for (int i = 0; i < count; i++)
            {
                if (!bits[i]) continue;
                bits[i].position += (Vector3)(dirs[i] * Time.deltaTime);
                Color c = S.glowColor; c.a = 1f - t / 0.7f; srs[i].color = c;
            }
            yield return null;
        }
        foreach (Transform b in bits) if (b) Destroy(b.gameObject);
    }

    // ---------------- Light beams ----------------
    // Press R: a target circle follows the mouse (controller: the right stick moves it).
    //   Click (tap)         = 3 short beams land near the circle.
    //   Hold the click      = charge one strong beam on the circle: the longer you hold, the wider, longer and
    //                         stronger it is. Let go to fire. (R works as the click too, for controllers.)
    //   No click in Aim Time = 3 short beams land at random spots in a circle around you.

    public static bool IsAiming { get; private set; } // the player's punch and hook wait while you aim

    Transform cursor;
    SpriteRenderer cursorSr;
    static Sprite ringSprite;

    IEnumerator LightBeam()
    {
        attacking = true;
        IsAiming = true;
        PlayerPrefs.SetInt(FoundSave + ".new", 0);
        if (hidden) { StartCoroutine(Sparkles(10)); appearTime = 0f; }
        visible = 1f;
        Vector2 aim = (Vector2)player.transform.position + Vector2.right * player.FacingDirection * 5f;
        ShowCursor(true, aim, 0f);

        float waited = 0f, held = 0f;
        bool pressed = false;
        yield return null; // the R press that started aiming doesn't count as a click
        while (true)
        {
            aim = UpdateAim(aim);
            if (!pressed)
            {
                if (ClickPressed()) pressed = true;
                else if ((waited += Time.deltaTime) >= S.aimTime) break;
            }
            else
            {
                held += Time.deltaTime;
                if (!ClickHeld()) break;
            }
            float charge = pressed ? Mathf.Clamp01((held - S.tapTime) / Mathf.Max(S.maxChargeTime, 0.01f)) : 0f;
            ShowCursor(true, aim, charge);
            glow.intensity = Mathf.Lerp(0.8f, 3f, charge);
            yield return null;
        }
        ShowCursor(false, aim, 0f);
        IsAiming = false;
        cooldownLeft = S.cooldown;

        if (!pressed)
        {
            // She picks: 3 short beams in a circle around you
            for (int i = 0; i < 3; i++)
            {
                Vector2 at = (Vector2)player.transform.position + Random.insideUnitCircle.normalized * Random.Range(S.autoRadius * 0.4f, S.autoRadius);
                StartCoroutine(Strike(at, S.shortBeamWidth, S.shortBeamTime, S.shortBeamDamage));
                yield return new WaitForSeconds(0.12f);
            }
        }
        else if (held < S.tapTime)
        {
            // Tap: 3 short beams near where you clicked
            for (int i = 0; i < 3; i++)
            {
                StartCoroutine(Strike(aim + Random.insideUnitCircle * S.scatterRadius, S.shortBeamWidth, S.shortBeamTime, S.shortBeamDamage));
                yield return new WaitForSeconds(0.12f);
            }
        }
        else
        {
            // Held: one strong beam, sized by how long you charged
            float k = Mathf.Clamp01((held - S.tapTime) / Mathf.Max(S.maxChargeTime, 0.01f));
            yield return Strike(aim, Mathf.Lerp(S.strongWidthMin, S.strongWidthMax, k), Mathf.Lerp(S.strongTimeMin, S.strongTimeMax, k),
                Mathf.RoundToInt(Mathf.Lerp(S.strongDamageMin, S.strongDamageMax, k)));
        }

        yield return new WaitForSeconds(0.4f);
        glow.intensity = 0.8f;
        attacking = false;
        checkTimer = 0f; // look for danger again right away (fades out if it's still dangerous)
    }

    // The target follows the mouse; the controller's right stick moves it instead
    Vector2 UpdateAim(Vector2 aim)
    {
        Vector2 me = player.transform.position;
        Gamepad pad = Gamepad.current;
        Vector2 stick = pad != null ? pad.rightStick.ReadValue() : Vector2.zero;
        Mouse mouse = Mouse.current;
        if (stick.sqrMagnitude > 0.04f) aim += stick * S.cursorSpeed * Time.deltaTime;
        else if (mouse != null && Camera.main) aim = Camera.main.ScreenToWorldPoint(mouse.position.ReadValue());
        Vector2 off = aim - me;
        if (off.magnitude > S.maxAimDistance) aim = me + off.normalized * S.maxAimDistance;
        return aim;
    }

    static bool ClickPressed()
    {
        Mouse m = Mouse.current;
        return (m != null && m.leftButton.wasPressedThisFrame) || GameInput.CompanionPressed;
    }

    static bool ClickHeld()
    {
        Mouse m = Mouse.current;
        return (m != null && m.leftButton.isPressed) || GameInput.Held(GameAction.Companion);
    }

    // The glowing target circle; it grows while charging
    void ShowCursor(bool show, Vector2 at, float charge)
    {
        if (!cursor)
        {
            if (!ringSprite) ringSprite = MakeRing();
            var go = new GameObject("BeamTarget");
            cursorSr = go.AddComponent<SpriteRenderer>();
            cursorSr.sprite = ringSprite;
            cursorSr.sortingOrder = 30;
            cursor = go.transform;
        }
        cursor.gameObject.SetActive(show);
        if (!show) return;
        cursor.position = at;
        float size = Mathf.Lerp(S.shortBeamWidth, S.strongWidthMax, charge) + Mathf.Sin(Time.time * 10f) * 0.08f;
        cursor.localScale = Vector3.one * size;
        cursorSr.color = Color.Lerp(S.glowColor, Color.white, charge);
    }

    // A soft ring picture made in code (no art needed)
    static Sprite MakeRing()
    {
        const int n = 64;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n * 0.5f, n * 0.5f)) / (n * 0.5f);
                float a = Mathf.Clamp01(1f - Mathf.Abs(d - 0.85f) * 12f) + (d < 0.1f ? 0.8f : 0f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
    }

    // A column of light strikes the ground under "at": width wide, stays "lasts" seconds, hits each enemy once
    IEnumerator Strike(Vector2 at, float width, float lasts, int damage)
    {
        RaycastHit2D ground = Physics2D.Raycast(at + Vector2.up * 0.5f, Vector2.down, 30f, LayerMask.GetMask("Ground"));
        float groundY = ground ? ground.point.y : at.y - 1f;
        Vector2 center = new Vector2(at.x, groundY + S.beamHeight * 0.5f);
        Vector2 size = new Vector2(width, S.beamHeight);

        var beamLight = new GameObject("BeamLight").AddComponent<Light2D>();
        beamLight.transform.position = new Vector3(at.x, groundY + 2f, 0f);
        beamLight.lightType = Light2D.LightType.Point;
        beamLight.color = S.glowColor;
        beamLight.pointLightOuterRadius = 3f + width;
        beamLight.intensity = 0f;

        if (S.beamFrames != null && S.beamFrames.Length > 0)
        {
            var fx = new GameObject("LightStrike");
            fx.transform.position = new Vector3(at.x, groundY, 0f);
            var fsr = fx.AddComponent<SpriteRenderer>();
            fsr.sortingOrder = 14;
            Vector2 full = S.beamFrames[0].rect.size / S.beamFrames[0].pixelsPerUnit;
            fx.transform.localScale = new Vector3(width * 1.6f / Mathf.Max(full.x, 0.01f), S.beamHeight / Mathf.Max(full.y, 0.01f), 1f);
            float frameTime = 1f / Mathf.Max(S.beamFps, 0.01f);
            int hit = Mathf.Clamp(S.beamHitFrame, 0, S.beamFrames.Length - 1);
            for (int f = 0; f <= hit; f++) { fsr.sprite = S.beamFrames[f]; yield return new WaitForSeconds(frameTime); }

            HitColumn(center, size, damage);
            beamLight.intensity = 1.5f + width * 0.3f;
            if (CameraFollow2D.Instance) CameraFollow2D.Instance.Shake(0.1f + width * 0.04f, 0.15f);
            // The bright part stays as long as the beam lasts
            int a = hit, b = Mathf.Min(hit + 1, S.beamFrames.Length - 1);
            for (float t = 0f; t < lasts; t += frameTime)
            {
                fsr.sprite = S.beamFrames[((int)(t / frameTime)) % 2 == 0 ? a : b];
                yield return new WaitForSeconds(frameTime);
            }
            for (int f = b + 1; f < S.beamFrames.Length; f++)
            {
                fsr.sprite = S.beamFrames[f];
                beamLight.intensity *= 0.6f;
                yield return new WaitForSeconds(frameTime);
            }
            Destroy(fx);
        }
        else
        {
            // No art yet: a plain bright column
            HitColumn(center, size, damage);
            GameObject beam = Bar(center, size, S.beamColor, 14);
            GameObject core = Bar(center, new Vector2(width * 0.35f, S.beamHeight), Color.white, 15);
            for (float t = 0f; t < lasts; t += Time.deltaTime)
            {
                float k = 1f - t / lasts;
                SetAlpha(beam, S.beamColor.a * k);
                SetAlpha(core, k);
                beamLight.intensity = 2f * k;
                yield return null;
            }
            Destroy(beam);
            Destroy(core);
        }
        Destroy(beamLight.gameObject);
    }

    // One hit on every enemy and boss inside the column
    void HitColumn(Vector2 center, Vector2 size, int damage)
    {
        var hitOnce = new System.Collections.Generic.HashSet<Health>();
        foreach (Collider2D hit in Physics2D.OverlapBoxAll(center, size, 0f))
        {
            Health h = hit.GetComponentInParent<Health>();
            if (!h || hitOnce.Contains(h) || h.GetComponent<PlayerController2D>()) continue;
            hitOnce.Add(h);
            h.TakeDamage(damage);
        }
    }

    GameObject Bar(Vector2 center, Vector2 size, Color color, int order)
    {
        var go = new GameObject("LightBeam");
        go.transform.position = center;
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = whiteSprite;
        sr.color = color;
        sr.sortingOrder = order;
        return go;
    }

    static void SetAlpha(GameObject go, float a)
    {
        var sr = go.GetComponent<SpriteRenderer>();
        Color c = sr.color; c.a = a; sr.color = c;
    }

    // ---------------- Icon (bottom right, left of the chain icon) ----------------

    void BuildIcon()
    {
        iconRoot = new GameObject("CompanionIcon");
        var canvas = iconRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = iconRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        const float size = 100f;
        RectTransform frame = Panel("Frame", iconRoot.transform, new Color(0.1f, 0.08f, 0.05f, 0.85f));
        frame.anchorMin = frame.anchorMax = new Vector2(1f, 0f);
        frame.pivot = new Vector2(1f, 0f);
        frame.anchoredPosition = new Vector2(-340f, 80f);
        frame.sizeDelta = new Vector2(size, size);
        RectTransform border = Panel("Border", frame, new Color(1f, 0.85f, 0.45f));
        Fill(border, -4f);
        border.SetAsFirstSibling();

        RectTransform pic = Panel("Picture", frame, S.color); // placeholder: her color
        pic.anchorMin = pic.anchorMax = new Vector2(0.5f, 0.5f);
        pic.sizeDelta = new Vector2(size * 0.3f, size * 0.55f);

        RectTransform sweepRt = Panel("Cooldown", frame, new Color(0f, 0f, 0f, 0.7f));
        Fill(sweepRt, 0f);
        cooldownSweep = sweepRt.GetComponent<Image>();
        cooldownSweep.type = Image.Type.Filled;
        cooldownSweep.fillMethod = Image.FillMethod.Radial360;
        cooldownSweep.fillOrigin = (int)Image.Origin360.Top;
        cooldownSweep.fillClockwise = false;
        cooldownSweep.fillAmount = 0f;

        keyText = Label("Key", frame, "R", 28f, new Vector2(0f, -30f));
        newText = Label("New", frame, "NEW!", 24f, new Vector2(0f, size * 0.5f + 20f));
        newText.color = new Color(1f, 0.85f, 0.2f);
    }

    void UpdateIcon()
    {
        if (!iconRoot) return;
        cooldownSweep.fillAmount = Mathf.Clamp01(cooldownLeft / Mathf.Max(S.cooldown, 0.01f));
        keyText.text = GameInput.KeyName(GameInput.GetKey(GameAction.Companion));
        newText.gameObject.SetActive(PlayerPrefs.GetInt(FoundSave + ".new", 0) == 1);
    }

    static RectTransform Panel(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = whiteSprite;
        img.color = color;
        img.raycastTarget = false;
        return (RectTransform)go.transform;
    }

    static void Fill(RectTransform rt, float inset)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
    }

    static TMP_Text Label(string name, Transform parent, string text, float size, Vector2 pos)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, pos.y < 0f ? 0f : 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(200f, 40f);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.alignment = TextAlignmentOptions.Center;
        t.color = Color.white;
        t.raycastTarget = false;
        return t;
    }
}
