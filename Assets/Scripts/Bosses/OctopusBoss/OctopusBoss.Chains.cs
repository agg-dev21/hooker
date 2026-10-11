using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Part of OctopusBoss (see OctopusBoss.cs for its settings and the main loop).
// Helpers: flashing, warning bars and the Chain object the attacks spawn.
public partial class OctopusBoss
{
    // ---------------- Helpers ----------------

    IEnumerator Flash(float time)
    {
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            FlashParts(Mathf.PingPong(t * 8f, 1f) > 0.5f);
            yield return null;
        }
        FlashParts(false);
    }

    void FlashParts(bool on)
    {
        for (int i = 0; i < bodyParts.Length; i++)
            if (bodyParts[i]) bodyParts[i].color = on ? flashColor * partColors[i] : partColors[i];
    }

    // A bar from 'from' going 'length' in direction 'dir' (warnings, anchor). Optional hitbox.
    GameObject Bar(Vector2 from, Vector2 dir, float length, float thickness, Color color, bool hurts)
    {
        var root = new GameObject("BossBar");
        root.transform.position = from;
        root.transform.right = dir;
        var piece = new GameObject("Piece");
        piece.transform.SetParent(root.transform, false);
        piece.transform.localPosition = new Vector3(length * 0.5f, 0f, 0f);
        piece.transform.localScale = new Vector3(length, thickness, 1f);
        var sr = piece.AddComponent<SpriteRenderer>();
        sr.sprite = boxSprite;
        sr.color = color;
        sr.sortingOrder = 8;
        spawned.Add(root);
        return root;
    }

    // An iron chain that grows out of a point. Damages (and knocks back) the player while out.
    class Chain
    {
        readonly OctopusBoss boss;
        readonly Transform root;
        readonly DamageZone zone;

        public Chain(OctopusBoss boss, Vector2 from, Vector2 dir, float thickness, int damage, float knockback)
        {
            this.boss = boss;
            var go = new GameObject("BossChain");
            root = go.transform;
            root.position = from;
            root.right = dir;
            var links = new GameObject("Links");
            links.transform.SetParent(root, false);
            links.transform.localPosition = new Vector3(0.5f, 0f, 0f);
            links.transform.localScale = new Vector3(1f, thickness, 1f);
            var sr = links.AddComponent<SpriteRenderer>();
            sr.sprite = boss.boxSprite;
            sr.color = boss.chainColor;
            sr.sortingOrder = 9;
            if (damage > 0)
            {
                var col = links.AddComponent<BoxCollider2D>();
                col.isTrigger = true;
                zone = links.AddComponent<DamageZone>();
                zone.Setup(damage, knockback, false, 0, 0f, 1f);
            }
            boss.spawned.Add(go);
            SetLength(0f);
        }

        public void SetLength(float len)
        {
            if (!root) return;
            root.localScale = new Vector3(Mathf.Max(len, 0.001f), 1f, 1f);
            if (zone) zone.Active = len > 0.3f;
        }

        public IEnumerator Extend(float length, float speed)
        {
            float len = 0f;
            while (len < length) { len = Mathf.MoveTowards(len, length, speed * Time.deltaTime); SetLength(len); yield return null; }
        }

        public IEnumerator Retract(float speed)
        {
            float len = root ? root.localScale.x : 0f;
            while (len > 0f) { len = Mathf.MoveTowards(len, 0f, speed * Time.deltaTime); SetLength(len); yield return null; }
            Destroy();
        }

        public void Destroy()
        {
            if (root) Object.Destroy(root.gameObject);
        }
    }

    void ClearSpawned()
    {
        foreach (GameObject go in spawned) if (go) Destroy(go);
        spawned.Clear();
    }
}
