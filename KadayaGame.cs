using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

// KADAYA - THE LAST SHADOW
// Fully procedural 3D ninja action game (Ninja Arashi mood + Naruto-style ninja combat).
// Moonlit village, 3-hit katana combos, Shadow Dash, Shadow Vortex, waves + boss, post-processing.
public class KadayaGame : MonoBehaviour
{
    enum Kind { Player, Grunt, Brute, Boss }
    enum State { Intro, Playing, Dead, Victory }

    class Fighter
    {
        public Kind kind;
        public GameObject root;
        public Transform extras, model, armL, armR, legL, legR, head, tip, bar, barFill;
        public List<Transform[]> chains = new List<Transform[]>();
        public MeshRenderer[] rends;
        public Material[] orig;
        public Material eyeMat;
        public TrailRenderer trail;
        public ParticleSystem aura;
        public SpriteRenderer telegraph;
        public float hp, maxHp, dmg, speed, range, windupTime, scale = 1f, seed, strafe = 1f;
        public float flash, walk, curSpeed, stun, cd, windup, swingT = -1f, swingLen = .3f, invuln, specialT;
        public int swingType;
        public bool dealt, alive = true, flashed;
        public Vector3 impulse;
    }

    struct Ptr { public int id; public Vector2 pos; public bool began; }

    const float INTRO = 3.4f;
    const float PLAYER_SPEED = 6.2f;
    const float SPECIAL_COST = 60f;
    const float DASH_COST = 20f;

    // ---------- scene ----------
    Camera cam;
    Transform worldRoot, actors;
    Fighter player;
    readonly List<Fighter> enemies = new List<Fighter>();
    readonly List<Light> lampLights = new List<Light>();
    readonly List<float> lampBase = new List<float>();
    Light moonLight, playerLight;
    Transform moonRoot;
    Vector3 moonDir = new Vector3(.34f, .2f, .92f).normalized;

    // ---------- materials / sprites ----------
    Material mNinja, mShadow, mBrute, mBoss, mSkin, mSteel, mBladeP, mBladeE, mOrange, mPurple, mGold, mEyeP, mEyeE,
             mFlash, mBlue, mBarBg, mBarFill, mWall, mWood, mPlaster, mRoofA, mRoofB, mRoofDark, mRed, mGround, mPath,
             mStone, mFoliage, mBamboo, mTrunk, mLampGlow, mWinGlow, mMountain, mAdd, mSmoke;
    Sprite spSoft, spCircle, spRing, spWhite, spMoon;
    static readonly Color EyeP = new Color(.4f, 1.6f, 2.4f);
    static readonly Color EyeE = new Color(2.4f, .18f, .12f);

    // ---------- effects ----------
    ParticleSystem psSparks, psSmoke;

    // ---------- game state ----------
    State state = State.Intro;
    float introT, deadT, shake, fovKick, hitStopUntil, dmgFlash;
    int wave, kills;
    const int maxWave = 3;
    bool spawning;
    float chakra = 100f, dashT, dashCD, attackBuffer, comboTimer, hitsTimer;
    int combo, comboHits;
    Vector3 dashDir = Vector3.right, camPos, camLook;
    bool camInit;

    // ---------- input ----------
    readonly List<Ptr> ptrs = new List<Ptr>();
    int joyId = -99;
    Vector2 joyOrigin, joyVec;
    bool padAttack, padDash, padSpecial, anyBegan;

    // ---------- UI ----------
    Canvas canvas;
    Font font;
    Image hpFill, hpGhost, chFill, dmgImg, joyBase, joyKnob, btnAtk, btnDash, btnSp, dashCdImg, spCdImg, barTop, barBottom;
    Text waveText, killText, comboText, msgText, subText, titleText, tagText;
    GameObject touchRoot;
    float msgT, msgAge;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (FindFirstObjectByType<KadayaGame>() == null)
            new GameObject("KADAYA_GAME").AddComponent<KadayaGame>();
    }

    // =====================================================================
    //  SETUP
    // =====================================================================
    void Awake()
    {
        Application.targetFrameRate = 60;
        Screen.sleepTimeout = SleepTimeout.NeverSleep;

        // HARD LOCK: landscape only.
        Screen.autorotateToPortrait = false;
        Screen.autorotateToPortraitUpsideDown = false;
        Screen.autorotateToLandscapeLeft = false;
        Screen.autorotateToLandscapeRight = false;
        Screen.orientation = ScreenOrientation.LandscapeLeft;

        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        DisableSceneDefaults();
        MakeSprites();
        MakeMaterials();
        actors = new GameObject("Actors").transform;
        SetupLighting();
        BuildWorld();
        BuildEffects();
        BuildAmbience();
        player = BuildFighter(Kind.Player, new Vector3(-12f, 0f, 0f));
        SetupCamera();
        PostFX();
        BuildUI();
    }

    void DisableSceneDefaults()
    {
        foreach (var c in FindObjectsByType<Camera>(FindObjectsSortMode.None)) c.gameObject.SetActive(false);
        foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None)) l.gameObject.SetActive(false);
    }

    // ---------- sprites ----------
    Sprite MakeSprite(int size, System.Func<float, float, Color> f)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + .5f) / size * 2f - 1f, v = (y + .5f) / size * 2f - 1f;
                px[y * size + x] = f(u, v);
            }
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(.5f, .5f), size);
    }

    void MakeSprites()
    {
        spSoft = MakeSprite(64, (u, v) =>
        {
            float r = Mathf.Sqrt(u * u + v * v);
            float a = Mathf.Clamp01(1f - r);
            return new Color(1, 1, 1, a * a);
        });
        spCircle = MakeSprite(128, (u, v) =>
        {
            float r = Mathf.Sqrt(u * u + v * v);
            return new Color(1, 1, 1, Mathf.Clamp01((1f - r) * 14f));
        });
        spRing = MakeSprite(256, (u, v) =>
        {
            float r = Mathf.Sqrt(u * u + v * v);
            float band = Mathf.Clamp01(1f - Mathf.Abs(r - .86f) / .09f);
            float glow = Mathf.Clamp01(1f - Mathf.Abs(r - .86f) / .3f) * .25f;
            float edge = Mathf.Clamp01((1f - r) * 20f);
            return new Color(1, 1, 1, Mathf.Max(band, glow) * edge);
        });
        spWhite = MakeSprite(4, (u, v) => Color.white);
        spMoon = MakeSprite(256, (u, v) =>
        {
            float r = Mathf.Sqrt(u * u + v * v);
            float a = Mathf.Clamp01((.92f - r) * 22f);
            float n = Mathf.PerlinNoise(u * 2.6f + 7f, v * 2.6f + 3f) * .6f + Mathf.PerlinNoise(u * 7f, v * 7f) * .25f;
            float shade = Mathf.Lerp(.68f, 1f, n);
            return new Color(.82f * shade + .12f, .9f * shade + .08f, 1f, a);
        });
    }

    // ---------- materials ----------
    Material Lit(Color c, float smooth = .45f, float metal = 0f, Color? emit = null)
    {
        Shader sh = null;
        if (GraphicsSettings.currentRenderPipeline != null) sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null) sh = Shader.Find("Standard");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        var m = new Material(sh);
        m.color = c;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metal);
        if (emit.HasValue)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emit.Value);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        return m;
    }

    Material Additive(Texture tex)
    {
        var sh = Shader.Find("Mobile/Particles/Additive");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        var m = new Material(sh);
        m.mainTexture = tex;
        return m;
    }

    void MakeMaterials()
    {
        mNinja = Lit(new Color(.02f, .025f, .04f), .65f);
        mShadow = Lit(new Color(.012f, .01f, .02f), .4f);
        mBrute = Lit(new Color(.05f, .01f, .035f), .4f);
        mBoss = Lit(new Color(.03f, .0f, .05f), .5f, 0f, new Color(.12f, .0f, .2f));
        mSkin = Lit(new Color(.62f, .38f, .26f), .3f);
        mSteel = Lit(new Color(.6f, .66f, .74f), .92f, .9f);
        mBladeP = Lit(new Color(.75f, .85f, .95f), .95f, .9f, new Color(.15f, .5f, .8f));
        mBladeE = Lit(new Color(.2f, .15f, .22f), .8f, .8f, new Color(.5f, .03f, .06f));
        mOrange = Lit(new Color(.85f, .33f, .04f), .4f, 0f, new Color(.28f, .07f, .0f));
        mPurple = Lit(new Color(.25f, .05f, .38f), .4f, 0f, new Color(.2f, .02f, .35f));
        mGold = Lit(new Color(.9f, .7f, .2f), .85f, .9f);
        mEyeP = Lit(Color.black, .2f, 0f, EyeP);
        mEyeE = Lit(Color.black, .2f, 0f, EyeE);
        mFlash = Lit(Color.white, .2f, 0f, new Color(3f, 3f, 3f));
        mBlue = Lit(new Color(.05f, .1f, .26f), .5f);
        mBarBg = Lit(new Color(.02f, .02f, .03f), .1f);
        mBarFill = Lit(new Color(.9f, .1f, .12f), .1f, 0f, new Color(1.4f, .1f, .12f));

        mWall = Lit(new Color(.11f, .08f, .07f), .25f);
        mWood = Lit(new Color(.07f, .045f, .035f), .3f);
        mPlaster = Lit(new Color(.30f, .32f, .38f), .2f);
        mRoofA = Lit(new Color(.07f, .10f, .16f), .55f);
        mRoofB = Lit(new Color(.30f, .05f, .07f), .55f);
        mRoofDark = Lit(new Color(.03f, .04f, .07f), .5f);
        mRed = Lit(new Color(.55f, .06f, .05f), .45f, 0f, new Color(.12f, .01f, .01f));
        mGround = Lit(new Color(.04f, .06f, .07f), .15f);
        mPath = Lit(new Color(.16f, .17f, .21f), .25f);
        mStone = Lit(new Color(.19f, .21f, .26f), .3f);
        mFoliage = Lit(new Color(.88f, .42f, .58f), .2f, 0f, new Color(.2f, .05f, .09f));
        mBamboo = Lit(new Color(.12f, .3f, .16f), .45f);
        mTrunk = Lit(new Color(.09f, .06f, .06f), .2f);
        mLampGlow = Lit(new Color(1f, .7f, .3f), .2f, 0f, new Color(3.2f, 1.7f, .5f));
        mWinGlow = Lit(new Color(1f, .75f, .35f), .2f, 0f, new Color(2.2f, 1.2f, .4f));
        mMountain = Lit(new Color(.035f, .055f, .1f), .05f);

        mAdd = Additive(spSoft.texture);
        var sd = Shader.Find("Sprites/Default");
        mSmoke = new Material(sd != null ? sd : Shader.Find("Standard"));
        mSmoke.mainTexture = spSoft.texture;
    }

    // ---------- primitives ----------
    GameObject P(PrimitiveType t, string n, Transform parent, Vector3 lp, Vector3 ls, Material m, Vector3 euler = default(Vector3))
    {
        var g = GameObject.CreatePrimitive(t);
        g.name = n;
        var c = g.GetComponent<Collider>();
        if (c != null) Destroy(c);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = lp;
        g.transform.localEulerAngles = euler;
        g.transform.localScale = ls;
        g.GetComponent<Renderer>().sharedMaterial = m;
        return g;
    }

    Transform Pivot(string n, Transform parent, Vector3 lp)
    {
        var t = new GameObject(n).transform;
        t.SetParent(parent, false);
        t.localPosition = lp;
        return t;
    }

    // ---------- lighting ----------
    void SetupLighting()
    {
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(.11f, .15f, .28f);
        RenderSettings.ambientEquatorColor = new Color(.07f, .09f, .16f);
        RenderSettings.ambientGroundColor = new Color(.03f, .035f, .06f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(.04f, .07f, .13f);
        RenderSettings.fogStartDistance = 24f;
        RenderSettings.fogEndDistance = 135f;

        var sky = Shader.Find("Skybox/Procedural");
        if (sky != null)
        {
            var sm = new Material(sky);
            sm.EnableKeyword("_SUNDISK_NONE");
            sm.SetFloat("_SunSize", 0f);
            sm.SetColor("_SkyTint", new Color(.07f, .12f, .3f));
            sm.SetColor("_GroundColor", new Color(.03f, .05f, .09f));
            sm.SetFloat("_Exposure", .55f);
            sm.SetFloat("_AtmosphereThickness", .45f);
            RenderSettings.skybox = sm;
        }

        moonLight = new GameObject("MoonLight").AddComponent<Light>();
        moonLight.type = LightType.Directional;
        moonLight.color = new Color(.55f, .7f, 1f);
        moonLight.intensity = 1.15f;
        moonLight.shadows = LightShadows.Soft;
        moonLight.shadowStrength = .85f;
        moonLight.transform.rotation = Quaternion.Euler(42f, -35f, 0f);
        RenderSettings.sun = moonLight;
    }

    // ---------- world ----------
    void BuildWorld()
    {
        worldRoot = new GameObject("World").transform;
        var lights = new GameObject("Lights").transform;

        P(PrimitiveType.Cube, "Ground", worldRoot, new Vector3(0, -.5f, 18), new Vector3(240, 1, 150), mGround);
        P(PrimitiveType.Cube, "Path", worldRoot, new Vector3(0, .02f, -.5f), new Vector3(80, .06f, 5.6f), mPath);
        P(PrimitiveType.Cube, "EdgeA", worldRoot, new Vector3(0, .05f, -3.4f), new Vector3(80, .12f, .35f), mStone);
        P(PrimitiveType.Cube, "EdgeB", worldRoot, new Vector3(0, .05f, 2.4f), new Vector3(80, .12f, .35f), mStone);
        for (int i = 0; i < 46; i++)
            P(PrimitiveType.Cube, "Stone", worldRoot,
                new Vector3(Random.Range(-37f, 37f), .06f, Random.Range(-2.9f, 1.8f)),
                new Vector3(Random.Range(.5f, 1.2f), .09f, Random.Range(.4f, .9f)), mStone,
                new Vector3(0, Random.Range(0f, 90f), 0));

        // houses - front row and back row
        for (int i = 0; i < 9; i++)
            House(new Vector3(-40f + i * 9.6f + Random.Range(-1f, 1f), 0, 9f + Random.Range(0f, 2f)),
                new Vector3(Random.Range(5.5f, 7.5f), Random.Range(3f, 4.2f), Random.Range(4.5f, 6f)),
                Random.value > .45f, i % 3 == 0 ? mRoofB : mRoofA);
        for (int i = 0; i < 7; i++)
            House(new Vector3(-44f + i * 14f + Random.Range(-2f, 2f), 0, 24f + Random.Range(0f, 4f)),
                new Vector3(Random.Range(7f, 10f), Random.Range(4.5f, 6f), Random.Range(6f, 8f)),
                Random.value > .5f, mRoofDark);

        Pagoda(new Vector3(-15f, 0, 17f));
        Torii(new Vector3(24f, 0, 2.2f));
        Torii(new Vector3(-26f, 0, 2.2f));

        for (int i = -4; i <= 4; i++)
            Lantern(new Vector3(i * 7.6f, 0, -4.4f), i % 2 == 0, lights);

        Sakura(new Vector3(-21f, 0, 6.8f));
        Sakura(new Vector3(4f, 0, 7.4f));
        Sakura(new Vector3(29f, 0, 5.8f));
        Sakura(new Vector3(-34f, 0, 6f));
        Sakura(new Vector3(34f, 0, -4.5f));

        for (int i = 0; i < 22; i++)
        {
            float side = i % 2 == 0 ? -1f : 1f;
            Bamboo(new Vector3(side * Random.Range(30f, 40f), 0, Random.Range(-1f, 8f)));
        }

        for (int i = 0; i < 14; i++)
        {
            float s = Random.Range(.5f, 1.4f);
            P(PrimitiveType.Sphere, "Rock", worldRoot,
                new Vector3(Random.Range(-38f, 38f), s * .2f, Random.value > .5f ? Random.Range(3.4f, 7f) : Random.Range(-7.5f, -5.5f)),
                new Vector3(s * 1.5f, s * .8f, s), mStone,
                new Vector3(0, Random.Range(0f, 180f), 0));
        }

        for (int i = 0; i < 8; i++)
            P(PrimitiveType.Sphere, "Mountain", worldRoot,
                new Vector3(-110f + i * 32f + Random.Range(-6f, 6f), -9f, 100f + Random.Range(-6f, 10f)),
                new Vector3(Random.Range(60f, 80f), Random.Range(38f, 58f), 40f), mMountain);

        StaticBatchingUtility.Combine(worldRoot.gameObject);
    }

    void House(Vector3 p, Vector3 s, bool lit, Material roof)
    {
        float w = s.x, h = s.y, d = s.z;
        var r = new GameObject("House").transform;
        r.SetParent(worldRoot, false);
        r.position = p;
        P(PrimitiveType.Cube, "Walls", r, new Vector3(0, h * .5f, 0), new Vector3(w, h, d), mWall);
        P(PrimitiveType.Cube, "Plaster", r, new Vector3(0, h * .5f, -d * .5f - .02f), new Vector3(w * .82f, h * .62f, .05f), mPlaster);
        foreach (float x in new[] { -.5f, .5f })
        {
            P(PrimitiveType.Cube, "Pillar", r, new Vector3(x * (w * .82f), h * .5f, -d * .5f - .05f), new Vector3(.2f, h, .2f), mWood);
            P(PrimitiveType.Cube, "PillarB", r, new Vector3(x * w, h * .5f, d * .5f), new Vector3(.22f, h, .22f), mWood);
        }
        P(PrimitiveType.Cube, "Beam", r, new Vector3(0, h * .78f, -d * .5f - .06f), new Vector3(w * .86f, .16f, .14f), mWood);
        if (lit)
        {
            P(PrimitiveType.Cube, "Window", r, new Vector3(-w * .18f, h * .48f, -d * .5f - .07f), new Vector3(w * .22f, h * .28f, .05f), mWinGlow);
            P(PrimitiveType.Cube, "WindowB", r, new Vector3(w * .2f, h * .48f, -d * .5f - .07f), new Vector3(w * .16f, h * .28f, .05f), mWinGlow);
        }
        float sd = d * .5f + .9f;
        float ang = 28f * Mathf.Deg2Rad;
        float cy = h + .2f + sd * Mathf.Sin(ang) * .5f;
        float cz = sd * Mathf.Cos(ang) * .5f;
        P(PrimitiveType.Cube, "RoofF", r, new Vector3(0, cy, -cz), new Vector3(w + 1.6f, .22f, sd), roof, new Vector3(-28f, 0, 0));
        P(PrimitiveType.Cube, "RoofB", r, new Vector3(0, cy, cz), new Vector3(w + 1.6f, .22f, sd), roof, new Vector3(28f, 0, 0));
        P(PrimitiveType.Cube, "Ridge", r, new Vector3(0, h + .26f + sd * Mathf.Sin(ang), 0), new Vector3(w + 1.8f, .22f, .42f), mRoofDark);
    }

    void Pagoda(Vector3 p)
    {
        var r = new GameObject("Pagoda").transform;
        r.SetParent(worldRoot, false);
        r.position = p;
        float y = 0;
        for (int i = 0; i < 4; i++)
        {
            float w = 6.2f - i * 1.1f;
            P(PrimitiveType.Cube, "Tier", r, new Vector3(0, y + 1.1f, 0), new Vector3(w, 2.2f, w), mWall);
            P(PrimitiveType.Cube, "TierWin", r, new Vector3(0, y + 1.2f, -w * .5f - .03f), new Vector3(w * .35f, 1f, .05f), mWinGlow);
            y += 2.2f;
            P(PrimitiveType.Cube, "EaveA", r, new Vector3(0, y + .1f, 0), new Vector3(w + 2.8f, .28f, w + 2.8f), mRoofB);
            P(PrimitiveType.Cube, "EaveB", r, new Vector3(0, y + .3f, 0), new Vector3(w + 1.6f, .24f, w + 1.6f), mRoofDark, new Vector3(0, 45f, 0));
            y += .45f;
        }
        P(PrimitiveType.Cylinder, "Spire", r, new Vector3(0, y + 1.6f, 0), new Vector3(.18f, 1.6f, .18f), mGold);
        P(PrimitiveType.Sphere, "SpireOrb", r, new Vector3(0, y + 1.1f, 0), new Vector3(.7f, .7f, .7f), mGold);
    }

    void Torii(Vector3 p)
    {
        var r = new GameObject("Torii").transform;
        r.SetParent(worldRoot, false);
        r.position = p;
        foreach (float x in new[] { -2.4f, 2.4f })
            P(PrimitiveType.Cylinder, "Pillar", r, new Vector3(x, 2.6f, 0), new Vector3(.42f, 2.6f, .42f), mRed);
        P(PrimitiveType.Cube, "Kasagi", r, new Vector3(0, 5.35f, 0), new Vector3(7.6f, .42f, .6f), mRoofDark);
        P(PrimitiveType.Cube, "KasagiRed", r, new Vector3(0, 5.05f, 0), new Vector3(6.9f, .3f, .5f), mRed);
        P(PrimitiveType.Cube, "Nuki", r, new Vector3(0, 4.2f, 0), new Vector3(5.2f, .26f, .32f), mRed);
    }

    void Lantern(Vector3 p, bool withLight, Transform lightParent)
    {
        var r = new GameObject("Lantern").transform;
        r.SetParent(worldRoot, false);
        r.position = p;
        P(PrimitiveType.Cube, "Base", r, new Vector3(0, .12f, 0), new Vector3(.62f, .24f, .62f), mStone);
        P(PrimitiveType.Cylinder, "Post", r, new Vector3(0, .85f, 0), new Vector3(.2f, .62f, .2f), mStone);
        P(PrimitiveType.Cube, "Box", r, new Vector3(0, 1.75f, 0), new Vector3(.58f, .52f, .58f), mLampGlow);
        P(PrimitiveType.Cube, "Cap", r, new Vector3(0, 2.1f, 0), new Vector3(.95f, .16f, .95f), mStone);
        P(PrimitiveType.Cube, "CapTop", r, new Vector3(0, 2.26f, 0), new Vector3(.45f, .16f, .45f), mStone);
        if (withLight)
        {
            var l = new GameObject("LampLight").AddComponent<Light>();
            l.transform.SetParent(lightParent, false);
            l.transform.position = p + new Vector3(0, 1.8f, -.5f);
            l.type = LightType.Point;
            l.color = new Color(1f, .64f, .3f);
            l.range = 11f;
            l.intensity = 2.6f;
            l.shadows = LightShadows.None;
            lampLights.Add(l);
            lampBase.Add(l.intensity);
        }
    }

    void Sakura(Vector3 p)
    {
        var r = new GameObject("Sakura").transform;
        r.SetParent(worldRoot, false);
        r.position = p;
        P(PrimitiveType.Cylinder, "Trunk", r, new Vector3(0, 1.9f, 0), new Vector3(.42f, 1.9f, .42f), mTrunk, new Vector3(0, 0, Random.Range(-6f, 6f)));
        P(PrimitiveType.Cube, "Branch", r, new Vector3(.9f, 3.2f, 0), new Vector3(2f, .22f, .22f), mTrunk, new Vector3(0, 0, 25f));
        P(PrimitiveType.Cube, "BranchB", r, new Vector3(-.9f, 3.0f, 0), new Vector3(2f, .2f, .2f), mTrunk, new Vector3(0, 0, -28f));
        for (int i = 0; i < 6; i++)
        {
            float s = Random.Range(2.2f, 3.6f);
            P(PrimitiveType.Sphere, "Bloom", r,
                new Vector3(Random.Range(-2f, 2f), Random.Range(3.6f, 5f), Random.Range(-1.2f, 1.2f)),
                new Vector3(s, s * .8f, s), mFoliage);
        }
    }

    void Bamboo(Vector3 p)
    {
        for (int i = 0; i < 5; i++)
        {
            float h = Random.Range(7f, 11f);
            P(PrimitiveType.Cylinder, "Bamboo", worldRoot,
                p + new Vector3(Random.Range(-1.2f, 1.2f), h * .5f, Random.Range(-1f, 1f)),
                new Vector3(.13f, h * .5f, .13f), mBamboo,
                new Vector3(Random.Range(-3f, 3f), 0, Random.Range(-4f, 4f)));
        }
    }

    // ---------- particles ----------
    ParticleSystem MakePS(string n, Material mat, int max, bool world = true)
    {
        var go = new GameObject(n);
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.simulationSpace = world ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
        main.maxParticles = max;
        main.loop = true;
        var em = ps.emission; em.enabled = false;
        var sh = ps.shape; sh.enabled = false;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat;
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        return ps;
    }

    static Gradient FadeGradient(bool fadeIn)
    {
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            fadeIn
                ? new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, .2f), new GradientAlphaKey(1f, .8f), new GradientAlphaKey(0f, 1f) }
                : new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        return g;
    }

    void BuildEffects()
    {
        psSparks = MakePS("FX_Sparks", mAdd, 600);
        var m1 = psSparks.main; m1.gravityModifier = .7f;
        var c1 = psSparks.colorOverLifetime; c1.enabled = true; c1.color = new ParticleSystem.MinMaxGradient(FadeGradient(false));
        var s1 = psSparks.sizeOverLifetime; s1.enabled = true; s1.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, 0));
        psSparks.Play();

        psSmoke = MakePS("FX_Smoke", mSmoke, 300);
        var m2 = psSmoke.main; m2.gravityModifier = -.1f;
        var c2 = psSmoke.colorOverLifetime; c2.enabled = true; c2.color = new ParticleSystem.MinMaxGradient(FadeGradient(false));
        var s2 = psSmoke.sizeOverLifetime; s2.enabled = true; s2.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, .6f, 1, 1.6f));
        psSmoke.Play();
    }

    void Burst(ParticleSystem ps, Vector3 pos, int n, float speed, float size, float life, Color col, float upBias = .4f)
    {
        var ep = new ParticleSystem.EmitParams();
        for (int i = 0; i < n; i++)
        {
            ep.position = pos + Random.insideUnitSphere * .15f;
            ep.velocity = (Random.onUnitSphere + Vector3.up * upBias) * speed * Random.Range(.35f, 1f);
            ep.startSize = size * Random.Range(.5f, 1.2f);
            ep.startLifetime = life * Random.Range(.6f, 1f);
            ep.startColor = col;
            ps.Emit(ep, 1);
        }
    }

    void BuildAmbience()
    {
        // fireflies
        var ff = MakePS("Fireflies", mAdd, 120);
        ff.transform.position = new Vector3(0, 2.4f, 2f);
        var fm = ff.main;
        fm.startLifetime = new ParticleSystem.MinMaxCurve(6f, 10f);
        fm.startSpeed = new ParticleSystem.MinMaxCurve(.1f, .5f);
        fm.startSize = new ParticleSystem.MinMaxCurve(.2f, .38f);
        fm.startColor = new Color(.85f, 1f, .45f, 1f);
        var fe = ff.emission; fe.enabled = true; fe.rateOverTime = 9f;
        var fs = ff.shape; fs.enabled = true; fs.shapeType = ParticleSystemShapeType.Box; fs.scale = new Vector3(74f, 5f, 18f);
        var fn = ff.noise; fn.enabled = true; fn.strength = 1.3f; fn.frequency = .35f;
        var fc = ff.colorOverLifetime; fc.enabled = true; fc.color = new ParticleSystem.MinMaxGradient(FadeGradient(true));
        ff.Play();

        // sakura petals
        var pt = MakePS("Petals", mAdd, 200);
        pt.transform.position = new Vector3(0, 9f, 2f);
        var pm = pt.main;
        pm.startLifetime = new ParticleSystem.MinMaxCurve(8f, 12f);
        pm.startSpeed = 0f;
        pm.startSize = new ParticleSystem.MinMaxCurve(.14f, .26f);
        pm.startColor = new Color(1f, .5f, .68f, .8f);
        pm.gravityModifier = .06f;
        var pe = pt.emission; pe.enabled = true; pe.rateOverTime = 13f;
        var psh = pt.shape; psh.enabled = true; psh.shapeType = ParticleSystemShapeType.Box; psh.scale = new Vector3(76f, .5f, 20f);
        var pn = pt.noise; pn.enabled = true; pn.strength = 1.4f; pn.frequency = .3f;
        var pc = pt.colorOverLifetime; pc.enabled = true; pc.color = new ParticleSystem.MinMaxGradient(FadeGradient(true));
        pt.Play();

        // ground mist
        var mist = MakePS("Mist", mAdd, 80);
        mist.transform.position = new Vector3(0, .8f, 1f);
        var mm = mist.main;
        mm.startLifetime = new ParticleSystem.MinMaxCurve(9f, 13f);
        mm.startSpeed = new ParticleSystem.MinMaxCurve(.2f, .5f);
        mm.startSize = new ParticleSystem.MinMaxCurve(8f, 14f);
        mm.startColor = new Color(.3f, .42f, .6f, .07f);
        var me = mist.emission; me.enabled = true; me.rateOverTime = 3.5f;
        var msh = mist.shape; msh.enabled = true; msh.shapeType = ParticleSystemShapeType.Box; msh.scale = new Vector3(76f, .5f, 20f);
        var mc = mist.colorOverLifetime; mc.enabled = true; mc.color = new ParticleSystem.MinMaxGradient(FadeGradient(true));
        mist.Play();

        // stars (Sprites/Default has no fog, so they stay visible)
        var stars = MakePS("Stars", mSmoke, 320);
        var sr = stars.GetComponent<ParticleSystemRenderer>();
        sr.sortingOrder = -30;
        stars.Play();
        var ep = new ParticleSystem.EmitParams();
        for (int i = 0; i < 280; i++)
        {
            Vector3 d = Random.onUnitSphere;
            d.y = Mathf.Abs(d.y) * .85f + .06f;
            ep.position = d.normalized * 210f;
            ep.velocity = Vector3.zero;
            ep.startSize = Random.Range(.35f, 1.3f);
            ep.startLifetime = 1e7f;
            ep.startColor = new Color(.85f, .92f, 1f, Random.Range(.35f, 1f));
            stars.Emit(ep, 1);
        }

        // moon (billboard sprites parented to a root that follows the camera)
        moonRoot = new GameObject("MoonRoot").transform;
        var glow = new GameObject("MoonGlow").AddComponent<SpriteRenderer>();
        glow.transform.SetParent(moonRoot, false);
        glow.sprite = spSoft;
        glow.color = new Color(.45f, .6f, 1f, .5f);
        glow.transform.localScale = Vector3.one * 150f;
        glow.sortingOrder = -20;
        var disc = new GameObject("MoonDisc").AddComponent<SpriteRenderer>();
        disc.transform.SetParent(moonRoot, false);
        disc.sprite = spMoon;
        disc.color = Color.white;
        disc.transform.localScale = Vector3.one * 34f;
        disc.sortingOrder = -19;
    }

    // ---------- camera / post ----------
    void SetupCamera()
    {
        cam = new GameObject("KadayaCamera").AddComponent<Camera>();
        cam.tag = "MainCamera";
        cam.fieldOfView = 56f;
        cam.nearClipPlane = .1f;
        cam.farClipPlane = 420f;
        cam.allowHDR = true;
        if (RenderSettings.skybox != null) cam.clearFlags = CameraClearFlags.Skybox;
        else
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(.03f, .05f, .1f);
        }
        cam.gameObject.AddComponent<AudioListener>();
    }

    void PostFX()
    {
        if (GraphicsSettings.currentRenderPipeline == null) return; // needs URP (run KADAYA > Setup URP Graphics)
        var data = cam.GetUniversalAdditionalCameraData();
        data.renderPostProcessing = true;

        var go = new GameObject("PostFX");
        var vol = go.AddComponent<Volume>();
        vol.isGlobal = true;
        var prof = ScriptableObject.CreateInstance<VolumeProfile>();
        vol.sharedProfile = prof;

        var bloom = prof.Add<Bloom>(true);
        bloom.threshold.Override(.8f);
        bloom.intensity.Override(1.15f);
        bloom.scatter.Override(.72f);

        var vig = prof.Add<Vignette>(true);
        vig.intensity.Override(.4f);
        vig.smoothness.Override(.55f);

        var ca = prof.Add<ColorAdjustments>(true);
        ca.postExposure.Override(.15f);
        ca.contrast.Override(18f);
        ca.saturation.Override(14f);

        var tm = prof.Add<Tonemapping>(true);
        tm.mode.Override(TonemappingMode.ACES);
    }

    // =====================================================================
    //  FIGHTERS
    // =====================================================================
    Transform[] Chain(Transform parent, Vector3 pos, int n, float segLen, float width, Material m)
    {
        var arr = new Transform[n];
        Transform prev = Pivot("Chain0", parent, pos);
        for (int i = 0; i < n; i++)
        {
            arr[i] = prev;
            P(PrimitiveType.Cube, "Seg", prev, new Vector3(0, 0, -segLen * .5f),
                new Vector3(width * (1f - i * .09f), .035f, segLen * 1.03f), m);
            if (i < n - 1) prev = Pivot("Chain" + (i + 1), prev, new Vector3(0, 0, -segLen));
        }
        return arr;
    }

    Fighter BuildFighter(Kind k, Vector3 pos)
    {
        bool en = k != Kind.Player;
        var f = new Fighter { kind = k, seed = Random.value * 10f };
        switch (k)
        {
            case Kind.Player: f.maxHp = 100; f.scale = 1f; break;
            case Kind.Grunt: f.maxHp = 70; f.dmg = 9; f.speed = 2.7f; f.range = 2.1f; f.windupTime = .55f; f.scale = 1f; break;
            case Kind.Brute: f.maxHp = 190; f.dmg = 16; f.speed = 2.0f; f.range = 2.9f; f.windupTime = .8f; f.scale = 1.35f; break;
            default: f.maxHp = 700; f.dmg = 24; f.speed = 2.3f; f.range = 4.2f; f.windupTime = .95f; f.scale = 2.1f; break;
        }
        f.hp = f.maxHp;
        f.strafe = Random.value < .5f ? -1f : 1f;

        Material body = !en ? mNinja : (k == Kind.Boss ? mBoss : (k == Kind.Brute ? mBrute : mShadow));
        Material accent = en ? mPurple : mOrange;
        Material cloth2 = en ? mShadow : mBlue;
        Material skin = en ? mShadow : mSkin;
        Material blade = en ? mBladeE : mBladeP;

        var root = new GameObject(!en ? "KADAYA" : "SHADOW_" + k);
        root.transform.SetParent(actors, false);
        root.transform.position = pos;
        root.transform.localScale = Vector3.one * f.scale;
        f.root = root;
        f.extras = new GameObject("Extras").transform;
        f.extras.SetParent(actors, false);

        f.model = Pivot("Model", root.transform, Vector3.zero);
        var model = f.model;

        // legs
        f.legL = Pivot("LegL", model, new Vector3(-.15f, .92f, 0));
        f.legR = Pivot("LegR", model, new Vector3(.15f, .92f, 0));
        foreach (var leg in new[] { f.legL, f.legR })
        {
            P(PrimitiveType.Cube, "Leg", leg, new Vector3(0, -.44f, 0), new Vector3(.2f, .88f, .24f), body);
            P(PrimitiveType.Cube, "Wrap", leg, new Vector3(0, -.62f, 0), new Vector3(.235f, .14f, .27f), cloth2);
            P(PrimitiveType.Cube, "Foot", leg, new Vector3(0, -.9f, .07f), new Vector3(.22f, .12f, .42f), cloth2);
        }

        // torso
        P(PrimitiveType.Cube, "Hips", model, new Vector3(0, .96f, 0), new Vector3(.5f, .22f, .3f), body);
        P(PrimitiveType.Cube, "Torso", model, new Vector3(0, 1.38f, 0), new Vector3(.56f, .7f, .32f), body);
        P(PrimitiveType.Cube, "Vest", model, new Vector3(0, 1.44f, .02f), new Vector3(.5f, .5f, .34f), cloth2);
        P(PrimitiveType.Cube, "Sash", model, new Vector3(0, 1.05f, 0), new Vector3(.6f, .15f, .36f), accent);
        P(PrimitiveType.Cube, "SashTail", model, new Vector3(.2f, .82f, -.12f), new Vector3(.1f, .42f, .04f), accent, new Vector3(0, 0, 8f));
        P(PrimitiveType.Cube, "Collar", model, new Vector3(0, 1.74f, 0), new Vector3(.42f, .15f, .4f), accent);

        // arms
        f.armL = Pivot("ArmL", model, new Vector3(-.37f, 1.62f, 0));
        f.armR = Pivot("ArmR", model, new Vector3(.37f, 1.62f, 0));
        foreach (var arm in new[] { f.armL, f.armR })
        {
            P(PrimitiveType.Cube, "Arm", arm, new Vector3(0, -.33f, 0), new Vector3(.17f, .66f, .19f), body);
            P(PrimitiveType.Cube, "Bracer", arm, new Vector3(0, -.5f, 0), new Vector3(.205f, .2f, .225f), mSteel);
            P(PrimitiveType.Sphere, "Hand", arm, new Vector3(0, -.7f, 0), new Vector3(.15f, .15f, .15f), skin);
        }

        // katana (right hand)
        var hand = Pivot("Hand", f.armR, new Vector3(0, -.7f, .04f));
        P(PrimitiveType.Cube, "Handle", hand, new Vector3(0, 0, -.08f), new Vector3(.055f, .055f, .34f), mWood);
        P(PrimitiveType.Cylinder, "Tsuba", hand, new Vector3(0, 0, .12f), new Vector3(.2f, .01f, .2f), mGold, new Vector3(90f, 0, 0));
        P(PrimitiveType.Cube, "Blade", hand, new Vector3(0, 0, .8f), new Vector3(.035f, .1f, 1.35f), blade);
        f.tip = Pivot("Tip", hand, new Vector3(0, 0, 1.46f));

        // head
        f.head = Pivot("Head", model, new Vector3(0, 1.9f, 0));
        P(PrimitiveType.Sphere, "Hood", f.head, Vector3.zero, new Vector3(.46f, .48f, .46f), body);
        P(PrimitiveType.Cube, "Face", f.head, new Vector3(0, -.01f, .175f), new Vector3(.32f, .12f, .1f), skin);
        f.eyeMat = new Material(en ? mEyeE : mEyeP);
        foreach (float x in new[] { -.09f, .09f })
            P(PrimitiveType.Sphere, "Eye", f.head, new Vector3(x, 0f, .225f), new Vector3(.075f, .05f, .05f), f.eyeMat);
        P(PrimitiveType.Cube, "Band", f.head, new Vector3(0, .13f, 0), new Vector3(.49f, .09f, .49f), cloth2);
        P(PrimitiveType.Cube, "Plate", f.head, new Vector3(0, .13f, .248f), new Vector3(.2f, .085f, .03f), mSteel);
        if (en && k != Kind.Grunt)
            foreach (float x in new[] { -1f, 1f })
                P(PrimitiveType.Cube, "Horn", f.head, new Vector3(x * .2f, .32f, 0), new Vector3(.07f, .34f, .07f), mSteel, new Vector3(0, 0, -x * 24f));
        if (!en)
            for (int i = 0; i < 5; i++) // spiky hair tufts under the hood
                P(PrimitiveType.Cube, "Tuft", f.head, new Vector3(-.2f + i * .1f, .26f, -.06f), new Vector3(.07f, .2f, .07f), mOrange, new Vector3(-14f, 0, (i - 2) * 14f));

        // cloth chains (scarf + headband tails)
        f.chains.Add(Chain(model, new Vector3(0, 1.72f, -.12f), 5, .28f, .2f, accent));
        f.chains.Add(Chain(f.head, new Vector3(0, .13f, -.24f), 4, .26f, .09f, cloth2));

        // collect renderers for hit-flash
        f.rends = root.GetComponentsInChildren<MeshRenderer>();
        f.orig = new Material[f.rends.Length];
        for (int i = 0; i < f.rends.Length; i++) f.orig[i] = f.rends[i].sharedMaterial;

        // slash trail
        f.trail = f.tip.gameObject.AddComponent<TrailRenderer>();
        f.trail.material = mAdd;
        f.trail.time = .22f;
        f.trail.widthMultiplier = .9f * f.scale;
        f.trail.widthCurve = AnimationCurve.Linear(0, 1, 1, 0);
        f.trail.minVertexDistance = .04f;
        f.trail.shadowCastingMode = ShadowCastingMode.Off;
        var tg = new Gradient();
        Color tc = en ? new Color(1f, .25f, .3f) : new Color(.45f, .9f, 1f);
        tg.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(tc, .35f), new GradientColorKey(tc, 1f) },
            new[] { new GradientAlphaKey(.95f, 0f), new GradientAlphaKey(0f, 1f) });
        f.trail.colorGradient = tg;
        f.trail.emitting = false;

        // aura
        f.aura = MakePS("Aura", mAdd, 120);
        f.aura.transform.SetParent(root.transform, false);
        f.aura.transform.localPosition = new Vector3(0, .95f, 0);
        var am = f.aura.main;
        am.startLifetime = new ParticleSystem.MinMaxCurve(.55f, .9f);
        am.startSpeed = 0f;
        am.startSize = new ParticleSystem.MinMaxCurve(.3f, .6f);
        am.startColor = en ? new Color(.45f, .1f, .8f, .55f) : new Color(.3f, .7f, 1f, .5f);
        var ae = f.aura.emission; ae.enabled = true; ae.rateOverTime = en ? 14f : 22f;
        var ash = f.aura.shape; ash.enabled = true; ash.shapeType = ParticleSystemShapeType.Box; ash.scale = new Vector3(.6f, 1.7f, .4f);
        var av = f.aura.velocityOverLifetime; av.enabled = true; av.space = ParticleSystemSimulationSpace.World;
        av.x = new ParticleSystem.MinMaxCurve(0f); av.y = new ParticleSystem.MinMaxCurve(1.4f); av.z = new ParticleSystem.MinMaxCurve(0f);
        var asz = f.aura.sizeOverLifetime; asz.enabled = true; asz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, 0));
        var acl = f.aura.colorOverLifetime; acl.enabled = true; acl.color = new ParticleSystem.MinMaxGradient(FadeGradient(false));
        f.aura.Play();

        if (!en)
        {
            playerLight = new GameObject("KadayaLight").AddComponent<Light>();
            playerLight.transform.SetParent(root.transform, false);
            playerLight.transform.localPosition = new Vector3(0, 2.4f, -1f);
            playerLight.type = LightType.Point;
            playerLight.color = new Color(.4f, .75f, 1f);
            playerLight.range = 7f;
            playerLight.intensity = 1.5f;
            playerLight.shadows = LightShadows.None;
        }
        else
        {
            // hp bar
            f.bar = Pivot("Bar", f.extras, Vector3.zero);
            P(PrimitiveType.Cube, "BG", f.bar, Vector3.zero, new Vector3(1.3f, .14f, .03f), mBarBg);
            var fp = Pivot("FillPivot", f.bar, new Vector3(-.62f, 0, -.03f));
            P(PrimitiveType.Cube, "Fill", fp, new Vector3(.5f, 0, 0), Vector3.one, mBarFill);
            f.barFill = fp;
            f.bar.localScale = Vector3.one * (k == Kind.Boss ? 2.2f : 1f);
            foreach (var br in f.bar.GetComponentsInChildren<Renderer>()) br.shadowCastingMode = ShadowCastingMode.Off;

            // attack telegraph
            var tgo = new GameObject("Telegraph");
            tgo.transform.SetParent(f.extras, false);
            tgo.transform.rotation = Quaternion.Euler(90f, 0, 0);
            f.telegraph = tgo.AddComponent<SpriteRenderer>();
            f.telegraph.sprite = spCircle;
            f.telegraph.color = new Color(1f, .1f, .1f, 0f);
            f.telegraph.enabled = false;
        }

        // strip shadows from tiny trims for perf
        foreach (var r in f.rends)
            if (r.transform.localScale.magnitude < .25f) r.shadowCastingMode = ShadowCastingMode.Off;

        return f;
    }

    // =====================================================================
    //  UI
    // =====================================================================
    Image Img(string n, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Color c, Sprite sp = null, bool filled = false)
    {
        var g = new GameObject(n, typeof(RectTransform));
        g.transform.SetParent(parent, false);
        var im = g.AddComponent<Image>();
        im.sprite = sp != null ? sp : spWhite;
        im.color = c;
        im.raycastTarget = false;
        var rt = im.rectTransform;
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        if (filled)
        {
            im.type = Image.Type.Filled;
            im.fillMethod = Image.FillMethod.Horizontal;
            im.fillOrigin = 0;
            im.fillAmount = 1f;
        }
        return im;
    }

    Text Txt(string n, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, int fs, TextAnchor al, Color c)
    {
        var g = new GameObject(n, typeof(RectTransform));
        g.transform.SetParent(parent, false);
        var t = g.AddComponent<Text>();
        t.font = font;
        t.fontSize = fs;
        t.alignment = al;
        t.color = c;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        var o = g.AddComponent<Outline>();
        o.effectColor = new Color(0, 0, 0, .85f);
        o.effectDistance = new Vector2(2, -2);
        var rt = t.rectTransform;
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return t;
    }

    Image MakeButton(string label, Vector2 pos, float size, Color c, out Image cd)
    {
        var b = Img(label + "_BTN", touchRoot.transform, new Vector2(1, 0), new Vector2(.5f, .5f), pos, new Vector2(size, size), c, spCircle);
        Img("Ring", b.transform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(size * 1.06f, size * 1.06f),
            new Color(1, 1, 1, .35f), spRing);
        cd = Img("Cooldown", b.transform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(size, size), new Color(0, 0, 0, .65f), spCircle);
        cd.type = Image.Type.Filled;
        cd.fillMethod = Image.FillMethod.Radial360;
        cd.fillAmount = 0f;
        var t = Txt("Label", b.transform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(size, 40), size > 150 ? 30 : 20, TextAnchor.MiddleCenter, Color.white);
        t.text = label;
        return b;
    }

    void BuildUI()
    {
        var g = new GameObject("HUD");
        canvas = g.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var sc = g.AddComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1280, 720);
        sc.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        sc.matchWidthOrHeight = .5f;
        var tr = g.transform;

        dmgImg = Img("DamageFlash", tr, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, new Color(.8f, 0, 0, 0));
        dmgImg.rectTransform.anchorMin = Vector2.zero;
        dmgImg.rectTransform.anchorMax = Vector2.one;
        dmgImg.rectTransform.offsetMin = Vector2.zero;
        dmgImg.rectTransform.offsetMax = Vector2.zero;

        // player bars
        tagText = Txt("Name", tr, new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -16), new Vector2(300, 30), 24, TextAnchor.MiddleLeft, new Color(1f, .62f, .25f));
        tagText.text = "KADAYA";
        Img("HpFrame", tr, new Vector2(0, 1), new Vector2(0, 1), new Vector2(36, -50), new Vector2(430, 26), new Color(0, 0, 0, .6f));
        hpGhost = Img("HpGhost", tr, new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -54), new Vector2(422, 18), new Color(1f, .85f, .5f, .9f), null, true);
        hpFill = Img("HpFill", tr, new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -54), new Vector2(422, 18), new Color(.95f, .15f, .1f), null, true);
        Img("ChFrame", tr, new Vector2(0, 1), new Vector2(0, 1), new Vector2(36, -80), new Vector2(330, 16), new Color(0, 0, 0, .6f));
        chFill = Img("ChFill", tr, new Vector2(0, 1), new Vector2(0, 1), new Vector2(39, -83), new Vector2(324, 10), new Color(.3f, .8f, 1f), null, true);

        // top-right info
        waveText = Txt("Wave", tr, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-40, -18), new Vector2(400, 34), 28, TextAnchor.MiddleRight, new Color(.85f, .75f, 1f));
        killText = Txt("Kills", tr, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-40, -54), new Vector2(400, 28), 20, TextAnchor.MiddleRight, new Color(.8f, .85f, 1f));
        comboText = Txt("Combo", tr, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-40, -100), new Vector2(400, 44), 36, TextAnchor.MiddleRight, new Color(1f, .7f, .2f));

        // center messages
        msgText = Txt("Msg", tr, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, 110), new Vector2(1000, 90), 64, TextAnchor.MiddleCenter, Color.white);
        subText = Txt("Sub", tr, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, 50), new Vector2(1000, 40), 24, TextAnchor.MiddleCenter, new Color(.8f, .6f, 1f));
        msgText.text = ""; subText.text = "";

        // intro title + letterbox
        titleText = Txt("Title", tr, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, 40), new Vector2(1100, 140), 110, TextAnchor.MiddleCenter, new Color(1f, .55f, .15f));
        titleText.text = "KADAYA";
        var st = Txt("TitleSub", titleText.transform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, -85), new Vector2(900, 40), 28, TextAnchor.MiddleCenter, new Color(.75f, .6f, 1f));
        st.text = "THE  LAST  SHADOW";
        barTop = Img("BarTop", tr, new Vector2(.5f, 1), new Vector2(.5f, 1), Vector2.zero, new Vector2(3000, 90), Color.black);
        barBottom = Img("BarBottom", tr, new Vector2(.5f, 0), new Vector2(.5f, 0), Vector2.zero, new Vector2(3000, 90), Color.black);

        // touch controls
        touchRoot = new GameObject("Touch", typeof(RectTransform));
        touchRoot.transform.SetParent(tr, false);
        var trt = (RectTransform)touchRoot.transform;
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;

        joyBase = Img("JoyBase", tr, Vector2.zero, new Vector2(.5f, .5f), new Vector2(190, 170), new Vector2(220, 220), new Color(1, 1, 1, .12f), spCircle);
        Img("JoyRing", joyBase.transform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(220, 220), new Color(1, 1, 1, .3f), spRing);
        joyKnob = Img("JoyKnob", tr, Vector2.zero, new Vector2(.5f, .5f), new Vector2(190, 170), new Vector2(96, 96), new Color(1f, .6f, .2f, .55f), spCircle);
        joyBase.transform.SetParent(touchRoot.transform, false);
        joyKnob.transform.SetParent(touchRoot.transform, false);

        Image cdA;
        btnAtk = MakeButton("ATTACK", new Vector2(-150, 150), 180, new Color(.9f, .3f, .05f, .5f), out cdA);
        btnDash = MakeButton("DASH", new Vector2(-350, 105), 112, new Color(.1f, .45f, .9f, .5f), out dashCdImg);
        btnSp = MakeButton("VORTEX", new Vector2(-300, 270), 112, new Color(.55f, .15f, .9f, .5f), out spCdImg);
        cdA.fillAmount = 0f;

        touchRoot.SetActive(Application.isMobilePlatform || Input.touchSupported);
    }

    void ShowMsg(string main, float dur, string sub = "")
    {
        msgText.text = main;
        subText.text = sub;
        msgT = dur;
        msgAge = 0f;
    }

    // =====================================================================
    //  INPUT
    // =====================================================================
    bool HitBtn(Image b, Vector2 p)
    {
        if (b == null || !touchRoot.activeSelf) return false;
        float rad = b.rectTransform.sizeDelta.x * .5f * canvas.scaleFactor * 1.2f;
        return Vector2.Distance(p, (Vector2)b.rectTransform.position) < rad;
    }

    void GatherPointers()
    {
        ptrs.Clear();
        anyBegan = false;
        for (int i = 0; i < Input.touchCount; i++)
        {
            var t = Input.GetTouch(i);
            if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled) continue;
            ptrs.Add(new Ptr { id = t.fingerId, pos = t.position, began = t.phase == TouchPhase.Began });
        }
        if (Input.touchCount == 0 && (Input.GetMouseButton(0) || Input.GetMouseButtonDown(0)))
            ptrs.Add(new Ptr { id = -1, pos = Input.mousePosition, began = Input.GetMouseButtonDown(0) });

        for (int i = 0; i < ptrs.Count; i++)
        {
            var p = ptrs[i];
            if (!p.began) continue;
            anyBegan = true;
            if (HitBtn(btnAtk, p.pos)) padAttack = true;
            else if (HitBtn(btnDash, p.pos)) padDash = true;
            else if (HitBtn(btnSp, p.pos)) padSpecial = true;
            else if (joyId == -99 && p.pos.x < Screen.width * .5f) { joyId = p.id; joyOrigin = p.pos; }
        }

        bool alive = false;
        for (int i = 0; i < ptrs.Count; i++)
            if (ptrs[i].id == joyId)
            {
                alive = true;
                joyVec = Vector2.ClampMagnitude((ptrs[i].pos - joyOrigin) / (Screen.height * .11f), 1f);
            }
        if (!alive) { joyId = -99; joyVec = Vector2.zero; }
    }

    Vector2 ReadMove()
    {
        var kb = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        return kb.sqrMagnitude > .01f ? kb : joyVec;
    }

    // =====================================================================
    //  UPDATE
    // =====================================================================
    void Update()
    {
        if (player == null) return;
        float dt = Time.deltaTime;

        GatherPointers();
        Time.timeScale = Time.unscaledTime < hitStopUntil ? .06f : 1f;

        switch (state)
        {
            case State.Intro:
                introT += Time.unscaledDeltaTime;
                if (introT >= INTRO) { state = State.Playing; ShowMsg("WAVE 1", 2.4f); }
                break;

            case State.Playing:
                UpdatePlayer(dt);
                WaveLogic();
                break;

            case State.Dead:
            case State.Victory:
                deadT += Time.unscaledDeltaTime;
                if (deadT > 1.2f && (anyBegan || Input.GetKeyDown(KeyCode.R) || Input.GetKeyDown(KeyCode.Return))) ResetGame();
                break;
        }

        UpdateEnemies(dt);

        // animation / flash / bars
        Animate(player, dt);
        TickFlash(player, dt);
        foreach (var e in enemies)
        {
            if (!e.alive) continue;
            Animate(e, dt);
            TickFlash(e, dt);
            if (e.bar != null)
            {
                e.bar.position = e.root.transform.position + Vector3.up * (2.6f * e.scale);
                e.bar.rotation = cam.transform.rotation;
                e.barFill.localScale = new Vector3(1.24f * Mathf.Clamp01(e.hp / e.maxHp), .1f, .03f);
            }
        }
        enemies.RemoveAll(x => !x.alive);

        // timers
        comboTimer -= dt;
        hitsTimer -= dt;
        if (hitsTimer <= 0f) comboHits = 0;
        dmgFlash = Mathf.MoveTowards(dmgFlash, 0f, dt * 2.5f);

        // lantern flicker
        for (int i = 0; i < lampLights.Count; i++)
            lampLights[i].intensity = lampBase[i] * (1f + .09f * Mathf.Sin(Time.time * 7f + i * 1.7f) + .05f * Mathf.Sin(Time.time * 13f + i));

        UpdateHUD(dt);
    }

    void TickFlash(Fighter f, float dt)
    {
        if (f.flash > 0f)
        {
            f.flash -= dt;
            if (!f.flashed)
            {
                foreach (var r in f.rends) if (r != null) r.sharedMaterial = mFlash;
                f.flashed = true;
            }
        }
        else if (f.flashed)
        {
            for (int i = 0; i < f.rends.Length; i++) if (f.rends[i] != null) f.rends[i].sharedMaterial = f.orig[i];
            f.flashed = false;
        }
    }

    // ---------- player ----------
    void UpdatePlayer(float dt)
    {
        var p = player;
        var t = p.root.transform;

        // buttons / keys
        if (padAttack || Input.GetKeyDown(KeyCode.J) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Z)) attackBuffer = .22f;
        if (padDash || Input.GetKeyDown(KeyCode.K) || Input.GetKeyDown(KeyCode.LeftShift)) TryDash();
        if (padSpecial || Input.GetKeyDown(KeyCode.L) || Input.GetKeyDown(KeyCode.E)) TrySpecial();
        padAttack = padDash = padSpecial = false;
        attackBuffer -= dt;

        Vector2 inp = ReadMove();
        Vector3 wish = new Vector3(inp.x, 0, inp.y);
        if (wish.sqrMagnitude > 1f) wish.Normalize();
        if (wish.sqrMagnitude > .05f) dashDir = wish.normalized;

        float speedMul = p.swingT >= 0 ? .45f : 1f;
        if (p.specialT > 0f) speedMul = 0f;
        Vector3 vel = wish * PLAYER_SPEED * speedMul;

        if (dashT > 0f)
        {
            dashT -= dt;
            vel = dashDir * 26f;
            p.invuln = Mathf.Max(p.invuln, .08f);
            Burst(psSparks, t.position + Vector3.up * 1f, 3, 1.2f, .5f, .35f, new Color(.3f, .7f, 1f, .8f), 0f);
            Burst(psSmoke, t.position + Vector3.up * .9f, 1, .5f, 1.1f, .4f, new Color(.2f, .5f, 1f, .35f), 0f);
        }
        dashCD -= dt;
        p.invuln -= dt;
        p.specialT -= dt;

        // attack start (allowed when idle or late in the current swing)
        if (attackBuffer > 0f && dashT <= 0f && p.specialT <= 0f && (p.swingT < 0f || p.swingT > p.swingLen * .65f)) StartAttack();

        Vector3 pos = t.position + (vel + p.impulse) * dt;
        pos.x = Mathf.Clamp(pos.x, -33f, 33f);
        pos.z = Mathf.Clamp(pos.z, -6.5f, 6.5f);
        pos.y = 0f;
        p.curSpeed = Mathf.Min(vel.magnitude, 8f);
        t.position = pos;
        p.impulse = Vector3.Lerp(p.impulse, Vector3.zero, 9f * dt);

        if (wish.sqrMagnitude > .01f && p.swingT < 0f && dashT <= 0f)
            t.forward = Vector3.Slerp(t.forward, wish.normalized, 14f * dt);

        // swing progress
        if (p.swingT >= 0f)
        {
            p.swingT += dt;
            float u = p.swingT / p.swingLen;
            p.trail.emitting = u > .08f && u < .85f;
            if (!p.dealt && u >= .4f) { p.dealt = true; PlayerHit(); }
            if (p.swingT >= p.swingLen) { p.swingT = -1f; p.trail.emitting = false; }
        }

        chakra = Mathf.Min(100f, chakra + 3.2f * dt);
        var em = p.aura.emission;
        em.rateOverTime = 16f + chakra * .22f + (chakra >= SPECIAL_COST ? 10f : 0f);
    }

    Fighter Nearest(Vector3 from, float maxDist)
    {
        Fighter best = null;
        float bd = maxDist;
        foreach (var e in enemies)
        {
            if (!e.alive) continue;
            float d = Vector3.Distance(from, e.root.transform.position);
            if (d < bd) { bd = d; best = e; }
        }
        return best;
    }

    void StartAttack()
    {
        var p = player;
        if (comboTimer <= 0f) combo = 0;
        p.swingType = combo % 3;
        p.swingLen = p.swingType == 2 ? .46f : .3f;
        p.swingT = 0f;
        p.dealt = false;
        attackBuffer = 0f;
        combo++;
        comboTimer = .95f;

        var t = p.root.transform;
        var tgt = Nearest(t.position, 7f);
        if (tgt != null)
        {
            Vector3 d = tgt.root.transform.position - t.position; d.y = 0;
            if (d.sqrMagnitude > .01f)
            {
                t.forward = d.normalized;
                p.impulse += d.normalized * (p.swingType == 2 ? 9f : 7f);
            }
        }
        else p.impulse += t.forward * 3.5f;
    }

    void PlayerHit()
    {
        var p = player;
        var t = p.root.transform;
        bool heavy = p.swingType == 2;
        float range = heavy ? 3.5f : 2.8f;
        float dmg = heavy ? 65f : 38f;
        bool any = false;

        foreach (var e in enemies)
        {
            if (!e.alive) continue;
            Vector3 d = e.root.transform.position - t.position; d.y = 0;
            float dist = d.magnitude;
            if (dist < range + e.scale * .35f && (dist < 1.3f || heavy || Vector3.Dot(t.forward, d / dist) > .1f))
            {
                HurtEnemy(e, dmg, t.position, heavy ? 13f : 6f, heavy);
                any = true;
            }
        }
        Burst(psSparks, t.position + t.forward * 1.6f + Vector3.up * 1.3f, any ? 5 : 8, 5f, .16f, .22f, new Color(.5f, .9f, 1f), .2f);
        if (any)
        {
            shake = Mathf.Max(shake, heavy ? .55f : .3f);
            hitStopUntil = Time.unscaledTime + (heavy ? .085f : .045f);
            fovKick = heavy ? -3f : -1.5f;
        }
    }

    void TryDash()
    {
        if (state != State.Playing || dashCD > 0f || chakra < DASH_COST || player.specialT > 0f) return;
        chakra -= DASH_COST;
        dashCD = .7f;
        dashT = .17f;
        player.invuln = Mathf.Max(player.invuln, .3f);
        player.swingT = -1f;
        player.trail.emitting = false;
        fovKick = 9f;
        player.root.transform.forward = dashDir;
        Burst(psSmoke, player.root.transform.position + Vector3.up * .5f, 8, 3f, 1.2f, .5f, new Color(.25f, .5f, 1f, .4f), 0f);
    }

    void TrySpecial()
    {
        if (state != State.Playing || chakra < SPECIAL_COST || player.specialT > 0f) return;
        chakra -= SPECIAL_COST;
        StartCoroutine(Vortex());
    }

    IEnumerator Vortex()
    {
        var p = player;
        p.specialT = .6f;
        p.swingT = -1f;
        p.trail.emitting = false;
        p.invuln = .7f;
        var pos = p.root.transform.position;

        var ring = new GameObject("VortexRing").AddComponent<SpriteRenderer>();
        ring.sprite = spRing;
        ring.transform.position = pos + Vector3.up * .12f;
        ring.transform.rotation = Quaternion.Euler(90f, 0, 0);
        var ring2 = new GameObject("VortexGlow").AddComponent<SpriteRenderer>();
        ring2.sprite = spSoft;
        ring2.transform.position = pos + Vector3.up * .1f;
        ring2.transform.rotation = Quaternion.Euler(90f, 0, 0);

        float t = 0f;
        bool dealt = false;
        while (t < .55f)
        {
            t += Time.deltaTime;
            float k = t / .55f;
            float ease = 1f - (1f - k) * (1f - k);
            ring.transform.localScale = Vector3.one * Mathf.Lerp(1f, 17f, ease);
            ring.color = new Color(.5f, .9f, 1f, 1f - k);
            ring2.transform.localScale = Vector3.one * Mathf.Lerp(1f, 15f, ease);
            ring2.color = new Color(.3f, .6f, 1f, (1f - k) * .6f);
            Burst(psSparks, pos + Vector3.up * 1f, 3, 6f, .3f, .5f, new Color(.4f, .85f, 1f), .6f);

            if (!dealt && t > .1f)
            {
                dealt = true;
                shake = .85f;
                fovKick = 6f;
                hitStopUntil = Time.unscaledTime + .09f;
                Burst(psSparks, pos + Vector3.up * 1f, 70, 14f, .35f, .7f, new Color(.5f, .9f, 1f), .3f);
                foreach (var en in enemies)
                {
                    if (!en.alive) continue;
                    if (Vector3.Distance(pos, en.root.transform.position) < 8f)
                        HurtEnemy(en, 110f, pos, 17f, true);
                }
            }
            yield return null;
        }
        Destroy(ring.gameObject);
        Destroy(ring2.gameObject);
    }

    // ---------- damage ----------
    void HurtEnemy(Fighter e, float dmg, Vector3 from, float knock, bool heavy)
    {
        if (!e.alive) return;
        e.hp -= dmg;
        e.flash = .09f;
        float resist = e.kind == Kind.Boss ? 3.5f : (e.kind == Kind.Brute ? 1.6f : 1f);
        e.stun = (heavy ? .45f : .25f) / (e.kind == Kind.Boss ? 3f : 1f);
        Vector3 d = e.root.transform.position - from; d.y = 0;
        d = d.sqrMagnitude > .001f ? d.normalized : Vector3.forward;
        e.impulse += d * knock / resist;
        if (e.kind != Kind.Boss || heavy)
        {
            e.windup = 0f;
            e.swingT = -1f;
        }
        e.cd = Mathf.Max(e.cd, .55f);

        var hitPos = e.root.transform.position + Vector3.up * 1.3f * e.scale;
        Burst(psSparks, hitPos, heavy ? 28 : 14, 9f, .26f, .38f, new Color(.6f, .92f, 1f));
        Burst(psSmoke, hitPos, heavy ? 6 : 3, 2.5f, 1f, .6f, new Color(.25f, .05f, .4f, .6f));

        chakra = Mathf.Min(100f, chakra + (heavy ? 12f : 7f));
        comboHits++;
        hitsTimer = 1.7f;

        if (e.hp <= 0f) Kill(e);
    }

    void Kill(Fighter e)
    {
        e.alive = false;
        kills++;
        e.flash = 0f;
        var pos = e.root.transform.position + Vector3.up * 1.1f * e.scale;
        Burst(psSparks, pos, 40, 11f, .3f, .7f, new Color(.7f, .3f, 1f), .5f);
        Burst(psSmoke, pos, 14, 3.5f, 1.8f, 1.1f, new Color(.2f, .04f, .35f, .7f));
        shake = Mathf.Max(shake, e.kind == Kind.Boss ? 1f : .35f);
        hitStopUntil = Time.unscaledTime + (e.kind == Kind.Boss ? .2f : .06f);
        StartCoroutine(DieRoutine(e));
    }

    IEnumerator DieRoutine(Fighter e)
    {
        float t = 0f;
        Vector3 s0 = e.root.transform.localScale;
        if (e.bar != null) e.bar.gameObject.SetActive(false);
        if (e.telegraph != null) e.telegraph.enabled = false;
        e.trail.emitting = false;
        while (t < .4f)
        {
            t += Time.deltaTime;
            float k = t / .4f;
            e.root.transform.localScale = s0 * (1f - k * k);
            e.root.transform.position += Vector3.up * Time.deltaTime * 1.2f;
            yield return null;
        }
        Destroy(e.root);
        Destroy(e.extras.gameObject);
    }

    void DamagePlayer(float amt, Vector3 from)
    {
        if (state != State.Playing || player.invuln > 0f) return;
        var p = player;
        p.hp -= amt;
        p.flash = .12f;
        p.invuln = .45f;
        Vector3 d = p.root.transform.position - from; d.y = 0;
        d = d.sqrMagnitude > .001f ? d.normalized : -p.root.transform.forward;
        p.impulse += d * 8f;
        p.swingT = -1f;
        p.trail.emitting = false;
        shake = Mathf.Max(shake, .55f);
        hitStopUntil = Time.unscaledTime + .07f;
        dmgFlash = 1f;
        comboHits = 0;
        Burst(psSparks, p.root.transform.position + Vector3.up * 1.2f, 18, 7f, .22f, .45f, new Color(1f, .3f, .2f));
        if (p.hp <= 0f) { p.hp = 0f; StartCoroutine(PlayerDeath()); }
    }

    IEnumerator PlayerDeath()
    {
        state = State.Dead;
        deadT = 0f;
        ShowMsg("YOU FELL", 999f, "TAP  /  PRESS R TO RISE AGAIN");
        var tr = player.root.transform;
        Quaternion r0 = tr.rotation;
        float t = 0f;
        while (t < .6f && state == State.Dead)
        {
            t += Time.deltaTime;
            tr.rotation = r0 * Quaternion.Euler(-88f * Mathf.Clamp01(t / .6f), 0, 0);
            yield return null;
        }
    }

    // ---------- enemies ----------
    void UpdateEnemies(float dt)
    {
        if (enemies.Count == 0) return;
        var pp = player.root.transform.position;
        bool active = state == State.Playing;
        int attackers = 0;
        foreach (var e in enemies) if (e.alive && (e.windup > 0f || e.swingT >= 0f)) attackers++;

        foreach (var e in enemies)
        {
            if (!e.alive) continue;
            var t = e.root.transform;
            Vector3 pos = t.position;
            Vector3 to = pp - pos; to.y = 0;
            float dist = to.magnitude;
            Vector3 dir = dist > .01f ? to / dist : t.forward;
            Vector3 move = Vector3.zero;

            if (e.stun > 0f) e.stun -= dt;
            else if (active)
            {
                if (e.swingT >= 0f)
                {
                    e.swingT += dt;
                    float u = e.swingT / e.swingLen;
                    e.trail.emitting = u > .1f && u < .9f;
                    if (!e.dealt && u >= .5f)
                    {
                        e.dealt = true;
                        if (dist < e.range + .5f) DamagePlayer(e.dmg, pos);
                        if (e.kind != Kind.Grunt)
                        {
                            shake = Mathf.Max(shake, e.kind == Kind.Boss ? .6f : .3f);
                            Burst(psSparks, pos + dir * e.range * .6f + Vector3.up * .2f, 14, 6f, .3f, .5f, new Color(1f, .35f, .5f), .3f);
                        }
                    }
                    if (e.swingT >= e.swingLen)
                    {
                        e.swingT = -1f;
                        e.trail.emitting = false;
                        e.cd = Random.Range(.9f, 1.7f);
                    }
                }
                else if (e.windup > 0f)
                {
                    e.windup -= dt;
                    if (e.windup <= 0f)
                    {
                        e.windup = 0f;
                        e.swingT = 0f;
                        e.swingLen = .26f;
                        e.swingType = 3;
                        e.dealt = false;
                        e.impulse += dir * (e.kind == Kind.Boss ? 3f : 6f);
                    }
                }
                else
                {
                    e.cd -= dt;
                    if (dist > e.range * .8f) move = dir * e.speed;
                    else if (e.cd <= 0f && attackers < 2) { e.windup = e.windupTime; attackers++; }
                    else move = Vector3.Cross(Vector3.up, dir) * e.speed * .35f * e.strafe;
                }

                if (dist > .01f && e.stun <= 0f)
                    t.forward = Vector3.Slerp(t.forward, dir, 10f * dt);
            }

            pos += (move + e.impulse) * dt;
            foreach (var o in enemies)
            {
                if (o == e || !o.alive) continue;
                Vector3 d = pos - o.root.transform.position; d.y = 0;
                float m = d.magnitude;
                float min = (e.scale + o.scale) * .55f + .2f;
                if (m < min && m > .001f) pos += d / m * (min - m) * .5f;
            }
            float pm = Mathf.Max(.9f, e.scale * .8f);
            if (dist < pm && dist > .001f) pos -= dir * (pm - dist) * .6f;
            pos.x = Mathf.Clamp(pos.x, -34f, 34f);
            pos.z = Mathf.Clamp(pos.z, -6.8f, 6.8f);
            pos.y = 0f;
            t.position = pos;
            e.impulse = Vector3.Lerp(e.impulse, Vector3.zero, 8f * dt);
            e.curSpeed = move.magnitude;

            // telegraph + eye flash
            if (e.telegraph != null)
            {
                bool show = e.windup > 0f;
                e.telegraph.enabled = show;
                if (show)
                {
                    float k = 1f - e.windup / e.windupTime;
                    e.telegraph.transform.position = new Vector3(pos.x, .07f, pos.z);
                    e.telegraph.transform.localScale = Vector3.one * (e.range * 2.2f);
                    e.telegraph.color = new Color(1f, .1f, .1f, .12f + .5f * k);
                }
            }
            if (e.eyeMat != null && !e.flashed)
                e.eyeMat.SetColor("_EmissionColor", e.windup > 0f ? new Color(4f, 2.4f, 1f) : EyeE);
        }
    }

    // ---------- waves ----------
    void WaveLogic()
    {
        if (spawning || enemies.Count > 0) return;
        if (wave >= maxWave)
        {
            state = State.Victory;
            deadT = 0f;
            ShowMsg("VICTORY", 999f, "THE SHADOWS ARE BANISHED  -  TAP / PRESS R TO PLAY AGAIN");
            return;
        }
        wave++;
        StartCoroutine(SpawnWave(wave));
    }

    IEnumerator SpawnWave(int w)
    {
        spawning = true;
        ShowMsg("WAVE " + w, 2.4f, w == maxWave ? "THE SHADOW LORD AWAKENS" : "");
        yield return new WaitForSeconds(1.6f);

        var list = new List<Kind>();
        if (w == 1) for (int i = 0; i < 4; i++) list.Add(Kind.Grunt);
        else if (w == 2) { for (int i = 0; i < 5; i++) list.Add(Kind.Grunt); list.Add(Kind.Brute); }
        else { list.Add(Kind.Boss); for (int i = 0; i < 3; i++) list.Add(Kind.Grunt); }

        foreach (var k in list)
        {
            if (state != State.Playing) break;
            SpawnEnemy(k);
            yield return new WaitForSeconds(k == Kind.Boss ? 1.2f : .45f);
        }
        spawning = false;
    }

    void SpawnEnemy(Kind k)
    {
        float px = player.root.transform.position.x;
        float side = Random.value < .5f ? -1f : 1f;
        float x = Mathf.Clamp(px + side * Random.Range(11f, 15f), -31f, 31f);
        if (Mathf.Abs(x - px) < 8f) x = Mathf.Clamp(px - side * 13f, -31f, 31f);
        var e = BuildFighter(k, new Vector3(x, 0, Random.Range(-5f, 5f)));
        e.cd = Random.Range(.6f, 1.4f);
        enemies.Add(e);
        var pos = e.root.transform.position + Vector3.up;
        Burst(psSmoke, pos, 18, 3f, 1.6f, 1f, new Color(.25f, .05f, .45f, .7f));
        Burst(psSparks, pos, 24, 8f, .3f, .6f, new Color(.7f, .3f, 1f));
    }

    void ResetGame()
    {
        StopAllCoroutines();
        spawning = false;
        foreach (var e in enemies)
        {
            if (e.root != null) Destroy(e.root);
            if (e.extras != null) Destroy(e.extras.gameObject);
        }
        enemies.Clear();
        foreach (var r in FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
            if (r.gameObject.name.StartsWith("Vortex")) Destroy(r.gameObject);

        var p = player;
        p.hp = p.maxHp;
        p.alive = true;
        p.invuln = 1f;
        p.swingT = -1f;
        p.specialT = 0f;
        p.impulse = Vector3.zero;
        p.root.transform.rotation = Quaternion.identity;
        p.root.transform.position = new Vector3(-12f, 0, 0);
        chakra = 100f;
        dashCD = 0f; dashT = 0f;
        wave = 0; kills = 0; combo = 0; comboHits = 0;
        state = State.Playing;
        ShowMsg("WAVE 1", 2.4f);
    }

    // =====================================================================
    //  ANIMATION (procedural)
    // =====================================================================
    void Animate(Fighter f, float dt)
    {
        float sp = Mathf.Clamp01(f.curSpeed / 6f);
        f.walk += dt * (5f + sp * 9f) * (sp > .05f ? 1f : 0f);
        float sw = Mathf.Sin(f.walk) * 50f * sp;
        float t = Time.time + f.seed;

        f.legL.localRotation = Quaternion.Euler(sw, 0, 0);
        f.legR.localRotation = Quaternion.Euler(-sw, 0, 0);

        Quaternion aL = Quaternion.Euler(-sw * .8f - 8f, 0, -6f);
        Quaternion aR = Quaternion.Euler(-45f + sw * .4f, 0, 6f);
        float lean = sp * 11f, spin = 0f, hop = 0f;

        if (f.windup > 0f)
        {
            float k = 1f - f.windup / f.windupTime;
            aR = Quaternion.Euler(Mathf.Lerp(-45f, -170f, k), 0, 0);
            aL = Quaternion.Euler(Mathf.Lerp(-8f, -120f, k), 0, -6f);
            lean = -8f * k;
        }
        if (f.swingT >= 0f)
        {
            float u = Mathf.Clamp01(f.swingT / f.swingLen);
            float k = 1f - (1f - u) * (1f - u);
            switch (f.swingType)
            {
                case 0: aR = Quaternion.Euler(Mathf.Lerp(-170f, 20f, k), 0, 0); lean = Mathf.Lerp(-6f, 22f, k); break;
                case 1: aR = Quaternion.Euler(-85f, Mathf.Lerp(95f, -85f, k), 0); spin = Mathf.Lerp(25f, -25f, k); break;
                case 2: aR = Quaternion.Euler(-90f, 0, 0); aL = Quaternion.Euler(-70f, 0, -20f); spin = 360f * k; break;
                default: aR = Quaternion.Euler(Mathf.Lerp(-175f, 35f, k), 0, 0); aL = Quaternion.Euler(Mathf.Lerp(-120f, 20f, k), 0, -6f); lean = Mathf.Lerp(-10f, 28f, k); break;
            }
        }
        if (f.specialT > 0f)
        {
            float k = 1f - f.specialT / .6f;
            aR = Quaternion.Euler(-175f, 0, 12f);
            aL = Quaternion.Euler(-175f, 0, -12f);
            hop = Mathf.Sin(Mathf.Clamp01(k * 1.4f) * Mathf.PI) * .45f;
            lean = -6f;
        }

        f.armL.localRotation = aL;
        f.armR.localRotation = aR;
        f.model.localRotation = Quaternion.Euler(lean, spin, 0);
        f.model.localPosition = new Vector3(0, Mathf.Abs(Mathf.Sin(f.walk)) * .07f * sp + Mathf.Sin(t * 2f) * .012f + hop, 0);
        f.head.localRotation = Quaternion.Euler(Mathf.Sin(t * 1.3f) * 2f, 0, 0);

        foreach (var ch in f.chains)
            for (int i = 0; i < ch.Length; i++)
                ch[i].localRotation = Quaternion.Euler(
                    -10f + sp * 32f + Mathf.Sin(t * 7f + i * .9f) * 9f,
                    Mathf.Sin(t * 4f + i * .7f) * 12f, 0);
    }

    // =====================================================================
    //  CAMERA + HUD
    // =====================================================================
    void LateUpdate()
    {
        if (player == null || cam == null) return;
        float udt = Time.unscaledDeltaTime;
        var pt = player.root.transform.position;
        Vector3 target = pt + new Vector3(0, 3.8f, -10.5f);
        target.x = Mathf.Clamp(target.x, -27f, 27f);
        Vector3 look = pt + new Vector3(0, 1.4f, 0) + player.root.transform.forward * 1.2f;

        if (state == State.Intro)
        {
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(introT / INTRO));
            camPos = Vector3.Lerp(pt + new Vector3(15f, 10f, -19f), target, k);
            camLook = Vector3.Lerp(pt + new Vector3(0, 3f, 6f), look, k);
            camInit = true;
        }
        else if (!camInit) { camPos = target; camLook = look; camInit = true; }
        else
        {
            camPos = Vector3.Lerp(camPos, target, 1f - Mathf.Exp(-6f * udt));
            camLook = Vector3.Lerp(camLook, look, 1f - Mathf.Exp(-8f * udt));
        }

        Vector3 off = Random.insideUnitSphere * shake * .35f;
        shake = Mathf.MoveTowards(shake, 0f, udt * 2.2f);
        cam.transform.position = camPos + off;
        cam.transform.LookAt(camLook);
        fovKick = Mathf.Lerp(fovKick, 0f, 1f - Mathf.Exp(-8f * udt));
        cam.fieldOfView = 56f + fovKick;

        if (moonRoot != null)
        {
            moonRoot.position = cam.transform.position + moonDir * 250f;
            moonRoot.rotation = cam.transform.rotation;
        }
    }

    void UpdateHUD(float dt)
    {
        float r = Mathf.Clamp01(player.hp / player.maxHp);
        hpFill.fillAmount = r;
        hpGhost.fillAmount = Mathf.MoveTowards(hpGhost.fillAmount, r, Time.unscaledDeltaTime * .35f);
        hpFill.color = Color.Lerp(new Color(.95f, .12f, .1f), new Color(.95f, .38f, .12f), r);
        chFill.fillAmount = chakra / 100f;
        chFill.color = chakra >= SPECIAL_COST
            ? Color.Lerp(new Color(.3f, .8f, 1f), Color.white, .5f + .5f * Mathf.Sin(Time.unscaledTime * 8f))
            : new Color(.3f, .8f, 1f);

        waveText.text = "WAVE  " + Mathf.Max(wave, 1) + " / " + maxWave;
        killText.text = "SHADOWS BANISHED   " + kills;
        if (comboHits >= 2 && hitsTimer > 0f)
        {
            comboText.text = comboHits + "  HIT COMBO";
            comboText.fontSize = 34 + Mathf.Min(comboHits, 12);
        }
        else comboText.text = "";

        dmgImg.color = new Color(.8f, 0, 0, dmgFlash * .35f);

        // center message fade
        if (msgT > 0f)
        {
            msgT -= Time.unscaledDeltaTime;
            msgAge += Time.unscaledDeltaTime;
        }
        float a = msgT > 0f ? Mathf.Clamp01(Mathf.Min(msgAge * 3f, msgT * 2f)) : 0f;
        msgText.color = new Color(1f, 1f, 1f, a);
        subText.color = new Color(.8f, .6f, 1f, a);

        // intro title + letterbox
        if (state == State.Intro)
        {
            float k = Mathf.Clamp01(introT / INTRO);
            float ta = Mathf.Clamp01(introT * 1.4f) * Mathf.Clamp01((INTRO - .5f - introT) * 2f);
            titleText.color = new Color(1f, .55f, .15f, ta);
            foreach (var c in titleText.GetComponentsInChildren<Text>())
                if (c != titleText) c.color = new Color(.75f, .6f, 1f, ta);
            float h = 90f * (1f - Mathf.SmoothStep(0f, 1f, k));
            barTop.rectTransform.sizeDelta = new Vector2(3000, h);
            barBottom.rectTransform.sizeDelta = new Vector2(3000, h);
        }
        else if (titleText.gameObject.activeSelf)
        {
            titleText.gameObject.SetActive(false);
            barTop.gameObject.SetActive(false);
            barBottom.gameObject.SetActive(false);
        }

        // touch visuals
        if (touchRoot.activeSelf)
        {
            float rad = joyBase.rectTransform.sizeDelta.x * .5f * canvas.scaleFactor * .55f;
            if (joyId != -99)
            {
                joyBase.rectTransform.position = joyOrigin;
                joyKnob.rectTransform.position = joyOrigin + joyVec * rad;
                joyBase.color = new Color(1, 1, 1, .2f);
            }
            else
            {
                joyBase.rectTransform.anchoredPosition = new Vector2(190, 170);
                joyKnob.rectTransform.anchoredPosition = new Vector2(190, 170);
                joyBase.color = new Color(1, 1, 1, .1f);
            }
            dashCdImg.fillAmount = Mathf.Clamp01(dashCD / .7f);
            spCdImg.fillAmount = chakra >= SPECIAL_COST ? 0f : 1f - chakra / SPECIAL_COST;
        }
    }
}
