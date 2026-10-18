using System.Collections;
using UnityEngine;

// Animated effects that burst up from the ground (Assets/Resources/GroundFxArt.asset,
// made by Tools > Art > Ability Icons And Ground Effects):
//   Hardy's jump slam landing (HardyBoss) and the exploding barrels (ExplodingBarrel).
// Each is 6 frames that grow out of the ground. Empty: the old plain flash is used instead.
[CreateAssetMenu(fileName = "GroundFxArt", menuName = "Hooker/Ground Fx Art")]
public class GroundFxArt : ScriptableObject
{
    public Sprite[] hardySlamFrames;
    public Sprite[] barrelBlastFrames;
    public float fps = 14f;
    public float sizeMultiplier = 1f; // 1 = as wide as the area that gets hit

    public bool HasHardySlam => hardySlamFrames != null && hardySlamFrames.Length > 0 && hardySlamFrames[0];
    public bool HasBarrelBlast => barrelBlastFrames != null && barrelBlastFrames.Length > 0 && barrelBlastFrames[0];

    static GroundFxArt current;
    static bool loaded;

    public static GroundFxArt Current
    {
        get
        {
            if (!loaded || !current) // look again if it wasn't found (Unity keeps old values between plays here)
            {
                current = Resources.Load<GroundFxArt>("GroundFxArt");
                loaded = true;
            }
            return current;
        }
    }

    // Plays the frames once, standing on "ground" (the frames' pivot is their bottom middle), about "width" wide
    public static void Play(Sprite[] frames, Vector2 ground, float width, float fps)
    {
        if (frames == null || frames.Length == 0 || !frames[0]) return;
        var go = new GameObject("GroundFx");
        go.transform.position = ground;
        float frameWidth = Mathf.Max(frames[frames.Length - 1].bounds.size.x, 0.01f);
        float k = width * (Current ? Current.sizeMultiplier : 1f) / frameWidth;
        go.transform.localScale = new Vector3(k, k, 1f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = frames[0];
        sr.sortingOrder = 20; // in front of the floor and the characters
        go.AddComponent<Runner>().Begin(frames, fps);
    }

    // Steps through the frames, then removes itself
    class Runner : MonoBehaviour
    {
        public void Begin(Sprite[] frames, float fps) => StartCoroutine(Run(frames, fps));

        IEnumerator Run(Sprite[] frames, float fps)
        {
            var sr = GetComponent<SpriteRenderer>();
            float frameTime = 1f / Mathf.Max(fps, 1f);
            foreach (Sprite f in frames)
            {
                sr.sprite = f;
                yield return new WaitForSeconds(frameTime);
            }
            Destroy(gameObject);
        }
    }
}
