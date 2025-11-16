using System;
using UnityEngine;

namespace enemyaimwaring
{
    // Duckov 로더가 찾는 엔트리 포인트:
    //   enemyaimwaring.ModBehaviour
    public class ModBehaviour : Duckov.Modding.ModBehaviour
    {
       protected override void OnAfterSetup()
{
    try
    {
        GameObject go = new GameObject("EnemyAimWarningRoot");
        UnityEngine.Object.DontDestroyOnLoad(go);

        // 🔽 왼쪽 위 HUD 매니저 붙이기
        go.AddComponent<EnemyAimWarningManager>();

        // 🔽 대쉬 러너도 같이 붙이기
        go.AddComponent<SonicDashRunner>();

        Debug.Log("[EnemyAimWarning] ModBehaviour.OnAfterSetup - HUD + Dash 초기화 완료");
    }
    catch (Exception ex)
    {
        Debug.Log("[EnemyAimWarning] 초기화 예외: " + ex);
            }
        }
    }

    // 왼쪽 Alt 순간이동 + 파란 수평 빔
    public class SonicDashRunner : MonoBehaviour
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

            Transform playerTf = FindPlayerTransform();
            if (playerTf == null)
            {
                Debug.Log("[EnemyAimWarning] dash: 플레이어를 찾지 못함");
                return;
            }

            Vector3 dir = GetFacingDirection(playerTf);
            Debug.Log("[EnemyAimWarning] dash 방향: " + dir.ToString("F2"));

            TryTeleport(playerTf, dir);

            _nextAvailableTime = Time.time + _cooldown;
        }

        // ─────────────── 플레이어 찾기 ───────────────
        private Transform FindPlayerTransform()
        {
            // 1) CharacterMainControl.Main 우선
            try
            {
                CharacterMainControl main = CharacterMainControl.Main;
                if (main != null)
                {
                    MonoBehaviour mb = main;
                    if (mb != null)
                        return mb.transform;
                }
            }
            catch
            {
            }

            // 2) 씬 전체에서 카메라와 가장 가까운 CharacterMainControl 사용
            try
            {
                CharacterMainControl[] all = UnityEngine.Object.FindObjectsOfType<CharacterMainControl>();
                if (all != null && all.Length > 0)
                {
                    Camera cam = Camera.main;
                    Transform best = null;
                    float bestScore = float.NegativeInfinity;

                    foreach (CharacterMainControl c in all)
                    {
                        if (c == null) continue;

                        float score = 0f;
                        if (cam != null)
                            score = -Vector3.Distance(cam.transform.position, c.transform.position);

                        if (score > bestScore)
                        {
                            bestScore = score;
                            best = c.transform;
                        }
                    }

                    if (best != null)
                        return best;
                }
            }
            catch
            {
            }

            return null;
        }

        // ─────────────── 방향 계산 ───────────────
        private Vector3 GetFacingDirection(Transform player)
        {
            // 1) 축 입력 (패드 + 키보드)
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            Vector3 dir = new Vector3(h, 0f, v);
            if (dir.sqrMagnitude > 0.001f)
            {
                dir.Normalize();
                return dir;
            }

            // 2) WASD 개별 키
            Vector3 keyDir = Vector3.zero;
            if (Input.GetKey(KeyCode.W)) keyDir += new Vector3(0f, 0f, 1f);
            if (Input.GetKey(KeyCode.S)) keyDir += new Vector3(0f, 0f, -1f);
            if (Input.GetKey(KeyCode.A)) keyDir += new Vector3(-1f, 0f, 0f);
            if (Input.GetKey(KeyCode.D)) keyDir += new Vector3(1f, 0f, 0f);

            if (keyDir.sqrMagnitude > 0.001f)
            {
                keyDir.Normalize();
                return keyDir;
            }

            // 3) 입력이 없으면 캐릭터 forward
            Vector3 fwd = player.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.0001f)
                fwd = Vector3.forward;

            return fwd.normalized;
        }

        // ─────────────── 순간이동 + 빔 ───────────────
        private void TryTeleport(Transform player, Vector3 dir)
        {
            Vector3 startPos = player.position;
            Vector3 targetPos = startPos + dir * _teleportDistance;

            RaycastHit hit;
            if (Physics.Raycast(
                    startPos + Vector3.up * 0.1f,
                    dir,
                    out hit,
                    _teleportDistance,
                    ~0,
                    QueryTriggerInteraction.Ignore))
            {
                targetPos = hit.point - dir * 0.1f;
                targetPos.y = startPos.y;
            }

            Debug.Log("[EnemyAimWarning] 순간이동 시도: " + startPos + " -> " + targetPos);

            // 이동 경로 따라 수평 파란 빔
            SpawnBeamPillar(startPos, targetPos);

            player.position = targetPos;
        }

        private void SpawnBeamPillar(Vector3 from, Vector3 to)
        {
            try
            {
                Vector3 dir = to - from;
                float dist = dir.magnitude;
                if (dist <= 0.01f)
                    return;

                dir /= dist;

                // from~to 중간 지점 + 살짝 위로
                Vector3 center = (from + to) * 0.5f;
                center.y += 0.2f;

                // 이동거리의 80% 길이만큼만 그리기
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

                lr.startWidth = 0.25f;
                lr.endWidth = 0.25f;
                lr.alignment = LineAlignment.View;

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
                        new GradientColorKey(new Color(0.05f, 0.15f, 0.5f, 1f), 1f)
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
                Debug.Log("[EnemyAimWarning] SpawnBeamPillar 예외: " + ex);
            }
        }
    }
}
