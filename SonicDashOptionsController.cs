using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ItemStatsSystem;
using UnityEngine;

// 항상 씬에 붙어 돌아다니면서
// F7 옵션창 + 순간이동 ON/OFF만 담당하는 컨트롤러
public class SonicDashOptionsController : MonoBehaviour
{
    // 🔹 순간이동 사용 여부 (기본 ON)
    public static bool TeleportEnabled = true;

    private static SonicDashOptionsController _instance;

    // 옵션창 표시 여부
    private bool _showOptions = false;

    // 옵션창 위치
    private Rect _windowRect = new Rect(40f, 120f, 260f, 130f);

    // 🔹 게임이 로드되면 자동으로 오브젝트 하나 생성해서 붙음
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateSingleton()
    {
        if (_instance != null)
        {
            return;
        }

        GameObject obj = new GameObject("SonicDashOptionsController");
        UnityEngine.Object.DontDestroyOnLoad(obj);
        _instance = obj.AddComponent<SonicDashOptionsController>();
        Debug.Log("[sonicdash] SonicDashOptionsController created");
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(base.gameObject);
            return;
        }

        _instance = this;
    }

    private void Update()
    {
        // F7으로 옵션창 열기/닫기
        if (Input.GetKeyDown(KeyCode.F7))
        {
            _showOptions = !_showOptions;
        }
    }

    private void OnGUI()
    {
        if (!_showOptions)
        {
            return;
        }

        _windowRect = GUI.Window(
            963271,               // 고유 ID
            _windowRect,
            DrawOptionsWindow,    // 그릴 함수
            "SonicDash 옵션"      // 제목
        );
    }

    private void DrawOptionsWindow(int windowId)
    {
        GUILayout.Label("순간이동 기능");

        bool newEnabled = GUILayout.Toggle(
            TeleportEnabled,
            TeleportEnabled ? "사용 (ON)" : "사용 안 함 (OFF)"
        );

        if (newEnabled != TeleportEnabled)
        {
            TeleportEnabled = newEnabled;
            Debug.Log("[sonicdash] 순간이동 " + (TeleportEnabled ? "ON" : "OFF"));
        }

        GUILayout.Space(10f);
        GUILayout.Label("F7: 이 창 열기 / 닫기");

        // 창 드래그 가능 영역
        GUI.DragWindow(new Rect(0f, 0f, 10000f, 20f));
    }
}
