using System;
using UnityEngine;

namespace PlatformerKit
{
    /// <summary>
    /// 演示级 2D 平台跳跃控制器：左右移动 + 跳跃（含土狼时间），
    /// 与 LevelBuilder 生成的危险物 / 收集品 / 终点 / 检查点交互。
    /// 正式项目可换成自己的控制器，只需同样响应 Level* 标记组件。
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
    public class SimplePlatformerController : MonoBehaviour
    {
        public float moveSpeed = 7f;
        public float jumpSpeed = 13f;

        [Tooltip("离开地面后仍可起跳的宽限时间（秒）")]
        public float coyoteTime = 0.1f;

        [Tooltip("留空 = 自动查找场景中的 LevelBuilder")]
        public LevelBuilder builder;

        public event Action<LevelCollectible> OnCollected;
        public event Action OnGoalReached;
        public event Action OnDied;

        Rigidbody2D body;
        BoxCollider2D box;
        Vector3 respawnPoint;
        float lastGroundedTime = float.NegativeInfinity;
        bool goalReached;

        void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            box = GetComponent<BoxCollider2D>();
            body.gravityScale = 3.5f;
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }

        void Start()
        {
            if (builder == null) builder = FindObjectOfType<LevelBuilder>();
            respawnPoint = builder != null ? builder.PlayerSpawnPosition : transform.position;
            transform.position = respawnPoint;
        }

        void Update()
        {
            float horizontal = Input.GetAxisRaw("Horizontal");
            body.velocity = new Vector2(horizontal * moveSpeed, body.velocity.y);

            if (IsGrounded())
                lastGroundedTime = Time.time;

            bool jumpPressed = Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.W) ||
                               Input.GetKeyDown(KeyCode.UpArrow);
            if (jumpPressed && Time.time - lastGroundedTime <= coyoteTime)
            {
                body.velocity = new Vector2(body.velocity.x, jumpSpeed);
                lastGroundedTime = float.NegativeInfinity;
            }
        }

        bool IsGrounded()
        {
            if (body.velocity.y > 0.1f) return false;

            var bounds = box.bounds;
            var feetCenter = new Vector2(bounds.center.x, bounds.min.y - 0.05f);
            var feetSize = new Vector2(bounds.size.x * 0.9f, 0.1f);

            foreach (var hit in Physics2D.OverlapBoxAll(feetCenter, feetSize, 0f))
            {
                if (hit == box || hit.isTrigger) continue;
                return true;
            }
            return false;
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (other.TryGetComponent<LevelHazard>(out _))
            {
                Die();
                return;
            }

            if (other.TryGetComponent<LevelCollectible>(out var collectible))
            {
                OnCollected?.Invoke(collectible);
                Destroy(collectible.gameObject);
                return;
            }

            if (other.TryGetComponent<LevelCheckpoint>(out var checkpoint))
            {
                respawnPoint = checkpoint.transform.position;
                return;
            }

            if (!goalReached && other.TryGetComponent<LevelGoal>(out _))
            {
                goalReached = true;
                Debug.Log("到达终点！");
                OnGoalReached?.Invoke();
            }
        }

        void Die()
        {
            body.velocity = Vector2.zero;
            transform.position = respawnPoint;
            OnDied?.Invoke();
        }
    }

    /// <summary>演示用相机跟随：平滑跟随目标，锁 Z。</summary>
    public class SimpleCameraFollow : MonoBehaviour
    {
        public Transform target;
        public Vector2 offset = new Vector2(0f, 1.5f);
        public float smoothTime = 0.15f;

        Vector3 velocity;

        void LateUpdate()
        {
            if (target == null) return;
            var goal = new Vector3(target.position.x + offset.x, target.position.y + offset.y,
                transform.position.z);
            transform.position = Vector3.SmoothDamp(transform.position, goal, ref velocity, smoothTime);
        }
    }
}
