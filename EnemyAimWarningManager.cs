using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ItemStatsSystem;
using UnityEngine;

public class EnemyAimWarningManager : MonoBehaviour
{
    // ───────────────────────────────
    // 내부용 구조체
    // ───────────────────────────────
    private class EnemyInfo
    {
        public CharacterMainControl Character;
        public Transform AimOrigin;
    }

    // ───────────────────────────────
    // 필드
    // ───────────────────────────────

    private CharacterMainControl _player;
    private readonly List<EnemyInfo> _enemies = new List<EnemyInfo>();

    private bool _anyAiming;
    private bool _anySearching;
    private readonly List<string> _aimingNames = new List<string>();
    private readonly List<string> _searchNames = new List<string>();

    private static Texture2D AlertIconTexture;
    private static Texture2D SearchIconTexture;
    private float _searchPulseTime;
    private const float SearchPulseSpeed = 4f;
    private const float SearchPulseAmplitude = 15f;

    private static string _dllDirectory;

    // 펫 로그 도배 방지
    private static readonly HashSet<string> _loggedPetChildNames = new HashSet<string>();

    // ───────────────────────────────
    // 초기 엔트리
    // ───────────────────────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        try
        {
            GameObject go = new GameObject("EnemyAimWarningManager");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<EnemyAimWarningManager>();

            try
            {
                Assembly asm = Assembly.GetExecutingAssembly();
                _dllDirectory = System.IO.Path.GetDirectoryName(asm.Location);
                Debug.Log("[EnemyAimWarning] DLL 위치: " + _dllDirectory);
            }
            catch (Exception ex)
            {
                Debug.Log("[EnemyAimWarning] DLL 경로 조회 실패: " + ex);
            }
        }
        catch (Exception ex)
        {
            Debug.Log("[EnemyAimWarning] Init 예외: " + ex);
        }
    }

    private void Awake()
    {
        Debug.Log("[EnemyAimWarning] EnemyAimWarningManager Awake()");
        RefreshCharacters();
        TrySetupIconDirectoryAndLoad();
    }

    private void Update()
    {
        RefreshCharacters();
        CheckEnemyStates();
    }

    // ───────────────────────────────
    // 플레이어 + 적 캐시
    // ───────────────────────────────

    private void RefreshCharacters()
    {
        HashSet<CharacterMainControl> charSet = new HashSet<CharacterMainControl>();

        // 1) CharacterMainControl 전부
        CharacterMainControl[] allChars = GameObject.FindObjectsOfType<CharacterMainControl>();
        if (allChars != null)
        {
            for (int i = 0; i < allChars.Length; i++)
            {
                CharacterMainControl c = allChars[i];
                if (c != null)
                    charSet.Add(c);
            }
        }

        // 2) ItemAgentHolder 기준으로 다시 한 번
        ItemAgentHolder[] holders = GameObject.FindObjectsOfType<ItemAgentHolder>();
        if (holders != null)
        {
            for (int i = 0; i < holders.Length; i++)
            {
                ItemAgentHolder h = holders[i];
                if (h == null) continue;

                CharacterMainControl c = h.GetComponent<CharacterMainControl>();
                if (c != null)
                    charSet.Add(c);
            }
        }

        if (charSet.Count == 0)
            return;

        // 3) 플레이어 결정
        CharacterMainControl main = null;
        try
        {
            main = CharacterMainControl.Main;
        }
        catch
        {
        }

        if (main != null && charSet.Contains(main))
        {
            _player = main;
        }
        else
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                float bestScore = float.NegativeInfinity;
                CharacterMainControl best = null;

                foreach (CharacterMainControl c in charSet)
                {
                    if (c == null) continue;
                    float score = -Vector3.Distance(cam.transform.position, c.transform.position);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = c;
                    }
                }

                _player = best;
            }
        }

        // 4) 적 목록 구성
        _enemies.Clear();

        foreach (CharacterMainControl c in charSet)
        {
            if (c == null) continue;
            if (c == _player) continue;

            if (IsPetLike(c))
                continue;

            EnemyInfo info = new EnemyInfo();
            info.Character = c;
            info.AimOrigin = FindAimOrigin(c.transform);
            _enemies.Add(info);
        }
    }

    // 강아지/펫 제거
    private bool IsPetLike(CharacterMainControl c)
    {
        if (c == null) return false;

        Transform t = c.transform;
        if (t == null) return false;

        Transform[] children = t.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform ch = children[i];
            if (ch == null) continue;

            string n = ch.name;
            if (string.IsNullOrEmpty(n)) continue;

            string lower = n.ToLower();
            if (lower.Contains("0_charactermodel_pet_template"))
            {
                if (_loggedPetChildNames.Add(n))
                {
                    Debug.Log("[EnemyAimWarning] 강아지/펫: 자식 Transform 이름으로 구분됨 -> " + n);
                }
                return true;
            }
        }

        return false;
    }

    private Transform FindAimOrigin(Transform root)
    {
        if (root == null) return root;

        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        Transform best = root;

        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == null) continue;

            string n = t.name.ToLower();
            if (n.Contains("gun") || n.Contains("weapon") || n.Contains("muzzle"))
            {
                best = t;
                break;
            }
        }

        return best;
    }

    // ───────────────────────────────
    // 적 조준/수색 판정
    // ───────────────────────────────

    private const float AimMinDistance = 3.0f;
    private const float AimMaxDistance = 20.0f;
    private const float AimDotThreshold = 0.96f;

    private const float SearchMinDistance = 2.0f;
    private const float SearchMaxDistance = 35.0f;
    private const float SearchDotThreshold = 0.6f;

    private void CheckEnemyStates()
    {
        _anyAiming = false;
        _anySearching = false;
        _aimingNames.Clear();
        _searchNames.Clear();

        if (_player == null || _enemies.Count == 0)
            return;

        Vector3 playerPos = _player.transform.position;

        for (int i = 0; i < _enemies.Count; i++)
        {
            EnemyInfo e = _enemies[i];
            if (e == null || e.Character == null) continue;

            Transform origin = e.AimOrigin ?? e.Character.transform;
            Vector3 from = origin.position;
            Vector3 toPlayer = playerPos - from;
            float dist = toPlayer.magnitude;
            if (dist <= 0.01f) continue;

            Vector3 dirToPlayer = toPlayer / dist;
            Vector3 forward = origin.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
                forward = origin.forward;
            forward.Normalize();

            float dot = Vector3.Dot(forward, dirToPlayer);

            bool isAiming = false;
            bool isSearching = false;

            if (dist >= AimMinDistance && dist <= AimMaxDistance && dot >= AimDotThreshold)
            {
                isAiming = true;
            }
            else if (dist >= SearchMinDistance && dist <= SearchMaxDistance && dot >= SearchDotThreshold)
            {
                isSearching = true;
            }

            string displayName = GetDisplayName(e.Character);

            if (isAiming)
            {
                _anyAiming = true;
                if (!string.IsNullOrEmpty(displayName) && !_aimingNames.Contains(displayName))
                    _aimingNames.Add(displayName);
            }
            else if (isSearching)
            {
                _anySearching = true;
                if (!string.IsNullOrEmpty(displayName) && !_searchNames.Contains(displayName))
                    _searchNames.Add(displayName);
            }
        }

        _searchPulseTime += Time.deltaTime * SearchPulseSpeed;
    }

    // ───────────────────────────────
    // 게임 내 이름(표시 이름) 가져오기
    // ───────────────────────────────

    private static string GetDisplayName(CharacterMainControl c)
    {
        if (c == null) return "Unknown";

        try
        {
            object presetObj = null;
            Type t = c.GetType();

            FieldInfo fi = t.GetField("characterPreset",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (fi != null)
            {
                presetObj = fi.GetValue(c);
            }
            else
            {
                PropertyInfo pi = t.GetProperty("characterPreset",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (pi != null)
                {
                    presetObj = pi.GetValue(c, null);
                }
            }

            if (presetObj != null)
            {
                Type pt = presetObj.GetType();
                string displayName = null;

                PropertyInfo nameProp = pt.GetProperty("DisplayName",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (nameProp != null)
                {
                    displayName = nameProp.GetValue(presetObj, null) as string;
                }
                else
                {
                    FieldInfo nameField = pt.GetField("DisplayName",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (nameField != null)
                    {
                        displayName = nameField.GetValue(presetObj) as string;
                    }
                }

                if (!string.IsNullOrEmpty(displayName))
                    return displayName;
            }
        }
        catch (Exception ex)
        {
            Debug.Log("[EnemyAimWarning] GetDisplayName preset 예외: " + ex);
        }

        string n = c.name;
        if (string.IsNullOrEmpty(n)) return "Unknown";

        const string clone = "(Clone)";
        int idx = n.IndexOf(clone, StringComparison.Ordinal);
        if (idx >= 0)
        {
            n = n.Substring(0, idx);
        }

        return n;
    }

    // 주격 조사 "이/가"
    private static string GetSubjectParticle(string name)
    {
        if (string.IsNullOrEmpty(name))
            return "이";

        char lastHangul = '\0';
        for (int i = 0; i < name.Length; i++)
        {
            char ch = name[i];
            int code = ch - 0xAC00;
            if (code >= 0 && code <= 11171)
            {
                lastHangul = ch;
            }
        }

        if (lastHangul == '\0')
            return "이";

        int syllableIndex = lastHangul - 0xAC00;
        int jong = syllableIndex % 28;

        return (jong == 0) ? "가" : "이";
    }

    // ───────────────────────────────
    // 아이콘 로딩 + 대체 텍스처
    // ───────────────────────────────

    private static void TrySetupIconDirectoryAndLoad()
    {
        if (!string.IsNullOrEmpty(_dllDirectory))
        {
            LoadIconTexture("warning_icon.png", ref AlertIconTexture, new Color(1f, 0.3f, 0.3f, 1f));
            LoadIconTexture("search_icon.png", ref SearchIconTexture, new Color(1f, 0.9f, 0.3f, 1f));
        }
        else
        {
            Debug.Log("[EnemyAimWarning] DLL 디렉터리 정보가 없음, PNG 로드는 건너뜀");
        }

        if (AlertIconTexture == null)
        {
            AlertIconTexture = CreateFallbackIcon(new Color(1f, 0.3f, 0.3f, 1f));
            Debug.Log("[EnemyAimWarning] 경고 아이콘 대체 텍스처 생성");
        }

        if (SearchIconTexture == null)
        {
            SearchIconTexture = CreateFallbackIcon(new Color(1f, 0.9f, 0.3f, 1f));
            Debug.Log("[EnemyAimWarning] 수색 아이콘 대체 텍스처 생성");
        }
    }

    private static void LoadIconTexture(string fileName, ref Texture2D target, Color tint)
    {
        if (string.IsNullOrEmpty(_dllDirectory))
            return;

        string path = System.IO.Path.Combine(_dllDirectory, fileName);

        if (!System.IO.File.Exists(path))
        {
            Debug.Log("[EnemyAimWarning] 아이콘 파일 없음: " + path);
            return;
        }

        try
        {
            byte[] data = System.IO.File.ReadAllBytes(path);
            if (data == null || data.Length == 0)
            {
                Debug.Log("[EnemyAimWarning] 아이콘 데이터 비어있음: " + path);
                return;
            }

            Texture2D tex = new Texture2D(2, 2, TextureFormat.ARGB32, false);
            if (!tex.LoadImage(data))
            {
                UnityEngine.Object.Destroy(tex);
                Debug.Log("[EnemyAimWarning] 아이콘 LoadImage 실패: " + path);
                return;
            }

            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            Color[] pixels = tex.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
            {
                Color p = pixels[i];
                pixels[i] = new Color(
                    p.r * tint.r,
                    p.g * tint.g,
                    p.b * tint.b,
                    p.a * tint.a
                );
            }
            tex.SetPixels(pixels);
            tex.Apply();

            target = tex;
            Debug.Log("[EnemyAimWarning] 아이콘 로드 성공: " + path);
        }
        catch (Exception ex)
        {
            Debug.Log("[EnemyAimWarning] 아이콘 로드 예외: " + ex);
        }
    }

    private static Texture2D CreateFallbackIcon(Color color)
    {
        int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Color transparent = new Color(0f, 0f, 0f, 0f);
        Color inner = new Color(color.r, color.g, color.b, 0.18f);

        Vector2 center = new Vector2(size - 1, size - 1) * 0.5f;
        float radius = size * 0.45f;
        float innerRadius = radius * 0.55f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x, y);
                float dist = Vector2.Distance(p, center);

                if (dist > radius)
                {
                    tex.SetPixel(x, y, transparent);
                }
                else if (dist > innerRadius)
                {
                    tex.SetPixel(x, y, color);
                }
                else
                {
                    tex.SetPixel(x, y, inner);
                }
            }
        }

        tex.Apply();
        return tex;
    }

    // ───────────────────────────────
    // HUD
    // ───────────────────────────────

    private void OnGUI()
    {
        if (_player == null)
            return;

        float x = 10f;
        float y = 130f; // 네가 말한 위치

        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 14;
        style.normal.textColor = Color.white;
        style.alignment = TextAnchor.UpperLeft;

        GUI.Label(new Rect(x, y, 400f, 20f),
            "플레이어: " + _player.name, style);
        y += 18f;
        GUI.Label(new Rect(x, y, 400f, 20f),
            "벤치마크: 적 수 " + _enemies.Count, style);
        y += 18f;

        string summary = _anyAiming ? "누군가 너를 조준중" : "아무도 너를 조준중 아님";
        GUI.Label(new Rect(x, y, 500f, 20f), summary, style);
        y += 18f;

        if (_aimingNames.Count > 0)
        {
            GUI.Label(new Rect(x, y, 600f, 20f),
                "조준중: " + string.Join(", ", _aimingNames.ToArray()), style);
            y += 18f;
        }

        if (_searchNames.Count > 0)
        {
            GUI.Label(new Rect(x, y, 600f, 20f),
                "수색중: " + string.Join(", ", _searchNames.ToArray()), style);
            y += 18f;
        }

        DrawCenterIconAndMessage();
    }

    private void DrawCenterIconAndMessage()
{
    bool drawAlert = _anyAiming;
    bool drawSearch = !_anyAiming && _anySearching;

    if (!drawAlert && !drawSearch)
        return;

    float baseSize = 80f;
    float cx = Screen.width * 0.5f;
    float cy = 80f; // 아이콘 세로 위치

    float drawSize = baseSize;
    if (drawSearch)
    {
        float t = Mathf.Sin(_searchPulseTime) * 0.5f + 0.5f;
        drawSize = baseSize + t * SearchPulseAmplitude;
    }

    float drawX = cx - drawSize * 0.5f;
    float drawY = cy - drawSize * 0.5f;

    // 공통 아이콘 영역
    Rect iconRect = new Rect(drawX, drawY, drawSize, drawSize);

    // ───── 아이콘 텍스처(동그라미) ─────
    if (drawAlert)
    {
        GUI.DrawTexture(iconRect, AlertIconTexture);
    }
    else if (drawSearch)
    {
        GUI.DrawTexture(iconRect, SearchIconTexture);
    }

    // ───── 동그라미 안에 ! / ? 글자 올리기 ─────
    GUIStyle iconStyle = new GUIStyle(GUI.skin.label);
    iconStyle.alignment = TextAnchor.MiddleCenter;
    iconStyle.fontSize = 40;

    if (drawAlert)
    {
        iconStyle.normal.textColor = Color.red;
        GUI.Label(iconRect, "!", iconStyle);
    }
    else if (drawSearch)
    {
        iconStyle.normal.textColor = Color.yellow;
        GUI.Label(iconRect, "?", iconStyle);
    }

    // ───── 아이콘 아래 문장 ─────
    string centerMsg = null;

    if (_anyAiming && _aimingNames.Count > 0)
    {
        if (_aimingNames.Count == 1)
        {
            string n = _aimingNames[0];
            centerMsg = n + GetSubjectParticle(n) + " 너를 조준중";
        }
        else
        {
            string names = string.Join(", ", _aimingNames.ToArray());
            centerMsg = names + " 등이 너를 조준중";
        }
    }
    else if (_anySearching && _searchNames.Count > 0)
    {
        if (_searchNames.Count == 1)
        {
            string n = _searchNames[0];
            centerMsg = n + GetSubjectParticle(n) + " 너를 수색중";
        }
        else
        {
            string names = string.Join(", ", _searchNames.ToArray());
            centerMsg = names + " 등이 너를 수색중";
        }
    }

    if (!string.IsNullOrEmpty(centerMsg))
    {
        GUIStyle centerStyle = new GUIStyle(GUI.skin.label);
        centerStyle.alignment = TextAnchor.UpperCenter;
        centerStyle.fontSize = 18;
        centerStyle.normal.textColor = Color.white;

        GUI.Label(
            new Rect(0f, cy + baseSize + 5f, Screen.width, 30f),
            centerMsg,
            centerStyle
            );
        }
    }
}
