using System;
using UnityEngine;

namespace sonicdash
{
    // Duckov 모드 로더에 등록되는 엔트리 클래스
    internal class ModBehaviour : Duckov.Modding.ModBehaviour
    {
        private void Awake()
        {
            Debug.Log("[sonicdash.ModBehaviour] sonicdashRunner 생성 시도");

            var go = new GameObject("sonicdashRunner");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<sonicdashRunner>();

            Debug.Log("[sonicdash.ModBehaviour] sonicdash 생성 완료");
        }
    }
}
