using System;
using UnityEngine;

namespace sonicdash
{
    internal class sonicdashRunner : MonoBehaviour
    {
        private KeyCode _activationKey = KeyCode.LeftAlt;
        private float _teleportDistance = 5f;
        private float _cooldown = 0.3f;
        private float _nextAvailableTime;

        private void Update()
        {
            if (Time.time < _nextAvailableTime)
                return;

            if (!Input.GetKeyDown(_activationKey))
                return;

            Debug.Log("[sonicdash] 발동 키 입력 감지");

            Transform playerTf = FindPlayerTransform();
            if (playerTf == null)
            {
                Debug.Log("[sonicdash] 플레이어를 찾지 못해 발동 취소");
                return;
            }

            Vector3 dir = GetFacingDirection(playerTf);
            Debug.Log("[sonicdash] 순간이동 방향: " + dir.ToString("F2"));

            TryTeleport(playerTf, dir);

            _nextAvailableTime = Time.time + _cooldown;
        }

        // ─────────────── 플레이어 찾기 ───────────────

        private Transform FindPlayerTransform()
        {
            try
            {
                CharacterMainControl main = CharacterMainControl.Main;
                if (main != null)
                {
                    MonoBehaviour mb = main;
                    if (mb != null)
                    {
                        Debug.Log("[sonicdash] CharacterMainControl.Main 사용해 플레이어 획득");
                        return mb.transform;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Log("[sonicdash] CharacterMainControl.Main 접근 중 예외: " + ex);
            }

            try
            {
                CharacterMainControl[] all = UnityEngine.Object.FindObjectsOfType<CharacterMainControl>();
                if (all != null && all.Length > 0)
                {
                    Transform best = null;
                    float bestScore = float.NegativeInfinity;
                    Camera cam = Camera.main;

                    foreach (CharacterMainControl cmc in all)
                    {
                        if (cmc == null) continue;

                        float score = 0f;
                        if (cam != null)
                            score = -Vector3.Distance(cam.transform.position, cmc.transform.position);

                        if (score > bestScore)
                        {
                            bestScore = score;
                            best = cmc.transform;
                        }
                    }

                    if (best != null)
                    {
                        Debug.Log("[sonicdash] 플레이어 후보 선택: " + best.name);
                        return best;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Log("[sonicdash] FindObjectsOfType<CharacterMainControl> 예외: " + ex);
            }

            Debug.Log("[sonicdash] 플레이어를 끝내 찾지 못함, 기능 중단");
            return null;
        }

        // ─────────────── 방향 계산 ───────────────

        private Vector3 GetFacingDirection(Transform player)
        {
            Vector3 dir = Vector3.zero;

            if (Input.GetKey(KeyCode.W)) dir += new Vector3(0f, 0f, 1f);
            if (Input.GetKey(KeyCode.S)) dir += new Vector3(0f, 0f, -1f);
            if (Input.GetKey(KeyCode.A)) dir += new Vector3(-1f, 0f, 0f);
            if (Input.GetKey(KeyCode.D)) dir += new Vector3(1f, 0f, 0f);

            if (dir.sqrMagnitude > 0.001f)
            {
                dir.Normalize();
                Debug.Log("[sonicdash] 입력 기반 방향 사용: " + dir.ToString("F2"));
                return dir;
            }

            Vector3 fwd = player.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.0001f)
                fwd = Vector3.forward;

            Debug.Log("[sonicdash] 입력 없음, forward 기반 방향 사용: " + fwd.normalized.ToString("F2"));
            return fwd.normalized;
        }

        // ─────────────── 순간이동 ───────────────

        private void TryTeleport(Transform player, Vector3 dir)
        {
            Vector3 startPos = player.position;
            Vector3 targetPos = startPos + dir * _teleportDistance;

            RaycastHit hit;
            if (Physics.Raycast(startPos + Vector3.up * 0.1f, dir,
                                out hit, _teleportDistance,
                                ~0, QueryTriggerInteraction.Ignore))
            {
                targetPos = hit.point - dir * 0.1f;
                targetPos.y = startPos.y;
            }

            Debug.Log($"[sonicdash] 순간이동 시도: {startPos} -> {targetPos}");

            TeleportPlayer(player, startPos, targetPos);
        }

        private void TeleportPlayer(Transform player, Vector3 startPos, Vector3 targetPos)
        {
            player.position = targetPos;
            Debug.Log("[sonicdash] Transform 기반 순간이동 완료: " + targetPos);

            // 이동 방향을 따라가는 수평 파란 빔
            SpawnBeamPillar(startPos, targetPos);
        }

        // ─────────────── 수평 빔 (굵게 + 파란색) ───────────────

        private void SpawnBeamPillar(Vector3 from, Vector3 to)
        {
            try
            {
                Vector3 dir = to - from;
                float dist = dir.magnitude;
                if (dist <= 0.01f)
                    return;

                dir /= dist;

                // from~to 중간 지점
                Vector3 center = (from + to) * 0.5f;
                center.y += 0.2f;

                // 빔 길이 (이동거리의 80%)
                float halfLen = dist * 0.4f;
                Vector3 p0 = center - dir * halfLen;
                Vector3 p1 = center + dir * halfLen;

                GameObject beamRoot = new GameObject("HekirekiBeam");
                beamRoot.transform.position = center;

                LineRenderer lr = beamRoot.AddComponent<LineRenderer>();
                lr.useWorldSpace = true;
                lr.positionCount = 2;
                lr.SetPosition(0, p0);
                lr.SetPosition(1, p1);

                // 🔵 굵기 늘림
                lr.startWidth = 0.25f;
                lr.endWidth = 0.25f;
                lr.alignment = LineAlignment.View;

                // 🔵 파란색 계열
                Shader shader = Shader.Find("Sprites/Default");
                if (shader != null)
                {
                    Material mat = new Material(shader);
                    mat.color = new Color(0.1f, 0.4f, 1f, 1f); // 진한 파란빛
                    lr.material = mat;
                }

                Gradient grad = new Gradient();
                grad.SetKeys(
                    new[]
                    {
                        new GradientColorKey(new Color(0.2f, 0.5f, 1f, 1f), 0f),
                        new GradientColorKey(new Color(0.1f, 0.3f, 0.9f, 1f), 0.5f),
                        new GradientColorKey(new Color(0.05f, 0.15f, 0.5f, 0.4f), 1f)
                    },
                    new[]
                    {
                        new GradientAlphaKey(1f, 0f),
                        new GradientAlphaKey(0f, 1f)
                    }
                );
                lr.colorGradient = grad;

                UnityEngine.Object.Destroy(beamRoot, 0.6f);
            }
            catch (Exception ex)
            {
                Debug.Log("[sonicdash] SpawnBeamPillar 예외: " + ex);
            }
        }
    }
}
