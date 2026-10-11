using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static UiKit; // Panel, Fill, Label, WhiteSprite (shared UI helpers)

// Part of Companion (see Companion.cs for its settings and the main loop).
// Ariel's light-beam ability: aiming, the cursor, and the beams.
public partial class Companion
{
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
        sr.sprite = WhiteSprite;
        sr.color = color;
        sr.sortingOrder = order;
        return go;
    }

    static void SetAlpha(GameObject go, float a)
    {
        var sr = go.GetComponent<SpriteRenderer>();
        Color c = sr.color; c.a = a; sr.color = c;
    }
}
