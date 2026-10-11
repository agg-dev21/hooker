using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

// Part of PoisonLevelBuilder (see PoisonLevelBuilder.cs for its settings and the main loop).
// The pieces the level is made of: poison hazards, rings, platforms, chests, checkpoints.
public static partial class PoisonLevelBuilder
{
    // ---------------- Pieces ----------------

    // Poison puddle along the top of a floor from x0 up to (not including) x1, at surface height y
    static void Puddle(string name, Transform parent, int x0, int x1, float y)
    {
        float width = x1 - x0;
        GameObject go = Box(name, parent, new Vector2(x0 + width * 0.5f, y + 0.12f), new Vector2(width, 0.25f), PoisonColor);
        var col = go.GetComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = new Vector2(1f, 3f);    // reaches 0.6 above the surface (scaled by the 0.25 height)
        col.offset = new Vector2(0f, 1f);
        go.AddComponent<PoisonHazard>().Setup(0, 0f, 2, 2f, 0.5f, false);
    }

    // Slimy wall strip: touching it poisons
    static void Slime(string name, Transform parent, Vector2 center, Vector2 size)
    {
        GameObject go = Box(name, parent, center, size, PoisonColor);
        go.GetComponent<BoxCollider2D>().isTrigger = true;
        go.AddComponent<PoisonHazard>().Setup(0, 0f, 2, 2f, 0.5f, false);
    }

    static void Dripper(string name, Transform parent, Vector2 pos, float delay)
    {
        GameObject go = Box(name, parent, pos, new Vector2(0.8f, 0.8f), VentColor);
        Object.DestroyImmediate(go.GetComponent<BoxCollider2D>());
        var so = new SerializedObject(go.AddComponent<PoisonDripper>());
        so.FindProperty("startDelay").floatValue = delay;
        so.FindProperty("groundLayer").intValue = 1 << groundLayer;
        so.FindProperty("interval").floatValue = DripInterval;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Swinging spiked poison ball hanging from a pivot
    static void Pendulum(string name, Transform parent, Vector2 pivot, float length, float phase)
    {
        var root = new GameObject(name);
        root.transform.SetParent(parent);
        root.transform.position = pivot + Vector2.up * lift;
        var so = new SerializedObject(root.AddComponent<PoisonPendulum>());
        so.FindProperty("phase").floatValue = phase;
        so.ApplyModifiedPropertiesWithoutUndo();

        GameObject chain = Box("Chain", root.transform, pivot + Vector2.down * length * 0.5f, new Vector2(0.15f, length), ChainColor);
        Object.DestroyImmediate(chain.GetComponent<BoxCollider2D>());

        GameObject ball = Box("SpikeBall", root.transform, pivot + Vector2.down * length, new Vector2(1.3f, 1.3f), SpikeColor);
        ball.transform.rotation = Quaternion.Euler(0f, 0f, 45f);
        Object.DestroyImmediate(ball.GetComponent<BoxCollider2D>());
        var col = ball.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.55f;
        ball.AddComponent<PoisonHazard>().Setup(10, 11f, 2, 3f, 0.5f, false);

        GameObject spikes = Box("Spikes", ball.transform, pivot + Vector2.down * length, new Vector2(1.3f, 1.3f), SpikeColor);
        spikes.transform.localScale = Vector3.one;
        spikes.transform.localRotation = Quaternion.Euler(0f, 0f, 45f); // second square turned 45 degrees: an 8-point star
        Object.DestroyImmediate(spikes.GetComponent<BoxCollider2D>());
    }

    // Slow slime that chases you along the floor. Touch = damage + poison.
    static void Sludge(string name, Transform parent, Vector2 pos)
    {
        GameObject go = Box(name, parent, pos, new Vector2(1.2f, 1.1f), SludgeColor);
        AddHealth(go, 16);
        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 3f;
        rb.freezeRotation = true;
        var chase = new SerializedObject(go.AddComponent<EnemyChase>());
        chase.FindProperty("speed").floatValue = 3f;
        chase.FindProperty("detectRange").floatValue = 10f;
        chase.FindProperty("stopDistance").floatValue = 0.3f;
        chase.FindProperty("groundLayer").intValue = 1 << groundLayer;
        chase.ApplyModifiedPropertiesWithoutUndo();
        var touch = new SerializedObject(go.AddComponent<ContactDamage>());
        touch.FindProperty("damage").intValue = 8;
        touch.ApplyModifiedPropertiesWithoutUndo();

        var aura = new GameObject("PoisonTouch");
        aura.transform.SetParent(go.transform, false);
        var auraCol = aura.AddComponent<BoxCollider2D>();
        auraCol.isTrigger = true;
        auraCol.size = new Vector2(1.25f, 1.25f);
        aura.AddComponent<PoisonHazard>().Setup(0, 0f, 2, 3f, 0.5f, false);
    }

    // Stands still and shoots at you
    static void Spitter(string name, Transform parent, Vector2 pos)
    {
        GameObject go = Box(name, parent, pos, new Vector2(1f, 2f), SpitterColor);
        AddHealth(go, 12);
        var so = new SerializedObject(go.AddComponent<EnemyShooter>());
        so.FindProperty("projectilePrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Projectile>(ProjectilePrefab);
        so.FindProperty("range").floatValue = 12f;
        so.FindProperty("fireRate").floatValue = 1.4f;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void Ring(string name, Transform parent, Vector2 pos, float launchSpeed)
    {
        GameObject ring = Box(name, parent, pos, new Vector2(0.6f, 0.6f), RingColor);
        Object.DestroyImmediate(ring.GetComponent<BoxCollider2D>());
        var col = ring.AddComponent<CircleCollider2D>();
        col.radius = 1f;
        col.isTrigger = true;
        var so = new SerializedObject(ring.AddComponent<HookPoint>());
        so.FindProperty("launchSpeed").floatValue = launchSpeed;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Thin platform you can jump up through from below, and drop through by holding Down (the dotted lines)
    static void OneWay(string name, Transform parent, int x, int y, int width)
    {
        GameObject go = Box(name, parent, new Vector2(x + width * 0.5f, y + 0.75f), new Vector2(width, 0.5f), OneWayColor);
        go.GetComponent<BoxCollider2D>().usedByEffector = true;
        var effector = go.AddComponent<PlatformEffector2D>();
        effector.useOneWay = true;
        effector.surfaceArc = 160f;
        go.AddComponent<OneWayPlatform>(); // hold Down to drop through it
    }

    static void Chest(string name, Transform parent, Vector2 pos)
    {
        GameObject chest = Box(name, parent, pos, new Vector2(1.5f, 1f), ChestColor);
        var col = chest.GetComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = new Vector2(2f, 2.5f);
        chest.AddComponent<Chest>();
        AddPrompt(chest, chest.GetComponent<Chest>());
    }

    static void Antidote(string name, Transform parent, Vector2 pos)
    {
        GameObject go = Box(name, parent, pos, new Vector2(0.6f, 0.6f), AntidoteColor);
        go.GetComponent<BoxCollider2D>().isTrigger = true;
        go.AddComponent<HealthPickup>();
    }

    static void Checkpoint(string name, Transform parent, Vector2 pos)
    {
        GameObject cp = Box(name, parent, pos, new Vector2(0.3f, 1.5f), CheckpointColor);
        cp.GetComponent<BoxCollider2D>().isTrigger = true;
        cp.AddComponent<Checkpoint>();
    }

    static void MakeKillZone(string name, Vector2 center, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.position = center;
        var col = go.AddComponent<BoxCollider2D>();
        col.size = size;
        col.isTrigger = true;
        go.AddComponent<KillZone>();
    }
}
