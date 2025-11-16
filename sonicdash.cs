using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ItemStatsSystem;
using UnityEngine;

public class SonicDash : MonoBehaviour
{
    // 🔹 옵션에서 제어하는 순간이동 기능 ON/OFF
    private bool _teleportEnabled = true;

    // 🔹 옵션창 표시 여부 (F7로 열고 닫음)
    private bool _showOptions = false;

    // 🔹 옵션창 위치
    private Rect _optionsWindowRect = new Rect(40f, 120f, 260f, 130f);

    private void Update()
    {
        // ===========================
        // 1) 옵션창 토글 (F7)
        // ===========================
        if (Input.GetKeyDown(KeyCode.F7))
        {
            _showOptions = !_showOptions;
        }

        // ===========================
        // 2) 순간이동 기능 꺼져 있으면
        //    아래 원래 로직 전부 스킵
        // ===========================
        if (!_teleportEnabled)
        {
            return;
        }

        // ===========================
        // 3) 🔻 여기부터 네가 원래 쓰던
        //    순간이동 / 대시 코드 붙이는 자리
        //    (기존 Update() 안 내용)
        // ===========================

        // 예시(너는 이 밑을 네 코드로 교체하면 됨):
        //
        // if (Input.GetKeyDown(KeyCode.Space))
        // {
        //     TryTeleport();
        // }

        // ===========================
        // 3) 끝
        // ===========================
    }

    private void OnGUI()
    {
        if (!_showOptions)
        {
            return;
        }

        // GUI.Window(고유 ID, 위치, 그릴 함수, 제목)
        _optionsWindowRect = GUI.Window(
            987654321, 
            _optionsWindowRect, 
            DrawOptionsWindow, 
            "SonicDash 옵션"
        );
    }

    private void DrawOptionsWindow(int windowId)
    {
        GUILayout.Label("순간이동 기능");

        // 현재 상태 표시 + 토글
        bool newEnabled = GUILayout.Toggle(
            _teleportEnabled,
            _teleportEnabled ? "사용 (ON)" : "사용 안 함 (OFF)"
        );

        if (newEnabled != _teleportEnabled)
        {
            _teleportEnabled = newEnabled;
            Debug.Log("[sonicdash] 순간이동 " + (_teleportEnabled ? "ON" : "OFF"));
        }

        GUILayout.Space(10f);
        GUILayout.Label("F7: 이 창 열기 / 닫기");

        // 창 드래그 가능 영역
        GUI.DragWindow(new Rect(0f, 0f, 10000f, 20f));
    }

    // 🔸 필요하면 네 순간이동 함수에 이 패턴 써도 됨
    //     (예: Dash, Teleport 함수 위에 방어막 한 겹 더)
    private void TryTeleport()
    {
        if (!_teleportEnabled)
        {
            return; // 옵션에서 꺼져 있으면 아예 작동 X
        }

        // 여기부터는 네가 실제로 쓰고 있는 순간이동 구현
        // (좌표 이동, 레이캐스트, 대미지 등)
    }
}
