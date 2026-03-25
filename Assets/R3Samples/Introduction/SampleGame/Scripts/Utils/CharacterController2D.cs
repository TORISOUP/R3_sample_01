using R3;
using UnityEngine;

namespace R3Samples.Introduction.SampleGame
{
    /// <summary>
    /// 2Dキャラクター移動制御
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(BoxCollider2D))]
    public class CharacterController2D : MonoBehaviour
    {
        [Header("Move")]
        /// <summary>
        /// 水平移動速度
        /// </summary>
        public float moveSpeed = 8f;

        [Header("Gravity")]
        /// <summary>
        /// 重力加速度
        /// </summary>
        public float gravity = -30f;

        /// <summary>
        /// 最大落下速度
        /// </summary>
        public float maxFallSpeed = -25f;

        [Header("Ground Check")]
        /// <summary>
        /// 地面判定レイヤー
        /// </summary>
        public LayerMask groundLayer;

        /// <summary>
        /// 地面判定距離
        /// </summary>
        public float groundCheckDistance = 0.1f;

        /// <summary>
        /// 地面判定内側余白
        /// </summary>
        public float groundRayInset = 0.05f;

        private Rigidbody2D _rigidbody2D;
        private BoxCollider2D _boxCollider2D;

        private Vector2 _velocity;
        private float _moveInput;

        private bool _hasConstantVelocityX;
        private float _constantVelocityX;
        private bool _isControlLocked;

        private ContactFilter2D _checkGroundFilter;
        private readonly RaycastHit2D[] _hits = new RaycastHit2D[1];

        private readonly ReactiveProperty<bool> _isGrounded = new(false);
        public ReadOnlyReactiveProperty<bool> IsGrounded => this._isGrounded;
        public Vector2 CurrentVelocity => this._rigidbody2D.linearVelocity;

        /// <summary>
        /// 初期化
        /// </summary>
        private void Awake()
        {
            this._rigidbody2D = this.GetComponent<Rigidbody2D>();
            this._boxCollider2D = this.GetComponent<BoxCollider2D>();

            this._rigidbody2D.gravityScale = 0f;
            this._rigidbody2D.freezeRotation = true;
            this._rigidbody2D.interpolation = RigidbodyInterpolation2D.Interpolate;

            this._isGrounded.AddTo(this);

            this._checkGroundFilter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = this.groundLayer,
                useTriggers = false
            };
        }

        /// <summary>
        /// 物理更新
        /// </summary>
        private void FixedUpdate()
        {
            this.UpdateGrounded();
            this.ApplyHorizontalMovement();
            this.ApplyGravity();
            this.ApplyVelocity();
            this.UpdateGrounded();
        }

        /// <summary>
        /// 通常移動入力設定
        /// </summary>
        public void Move(float x)
        {
            if (this._isControlLocked)
            {
                this._moveInput = 0f;
                return;
            }

            this._hasConstantVelocityX = false;
            this._moveInput = Mathf.Clamp(x, -1f, 1f);
        }

        /// <summary>
        /// 水平固定速度設定
        /// </summary>
        public void SetConstantVelocityX(float velocityX)
        {
            this._hasConstantVelocityX = true;
            this._constantVelocityX = velocityX;
        }

        /// <summary>
        /// 水平固定速度解除
        /// </summary>
        public void ClearConstantVelocityX()
        {
            this._hasConstantVelocityX = false;
        }

        /// <summary>
        /// 速度加算
        /// </summary>
        public void AddVelocity(Vector2 velocity)
        {
            this._velocity += velocity;
        }

        /// <summary>
        /// 速度上書き
        /// </summary>
        public void OverrideVelocity(Vector2 velocity)
        {
            this._velocity = velocity;
        }

        /// <summary>
        /// 操作ロック
        /// </summary>
        public void LockControl()
        {
            this._isControlLocked = true;
            this._moveInput = 0f;
        }

        /// <summary>
        /// 操作ロック解除
        /// </summary>
        public void UnlockControl()
        {
            this._isControlLocked = false;
        }

        /// <summary>
        /// 射出速度適用
        /// </summary>
        public void Launch(Vector2 initialVelocity)
        {
            this._moveInput = 0f;
            this._hasConstantVelocityX = true;
            this._constantVelocityX = initialVelocity.x;
            this.OverrideVelocity(initialVelocity);
        }

        /// <summary>
        /// 水平移動反映
        /// </summary>
        private void ApplyHorizontalMovement()
        {
            if (this._hasConstantVelocityX)
            {
                this._velocity.x = this._constantVelocityX;
                return;
            }

            this._velocity.x = this._moveInput * this.moveSpeed;
        }

        /// <summary>
        /// 重力反映
        /// </summary>
        private void ApplyGravity()
        {
            if (this._isGrounded.Value && this._velocity.y <= 0f)
            {
                this._velocity.y = 0f;
                return;
            }

            this._velocity.y += this.gravity * Time.fixedDeltaTime;

            if (this._velocity.y < this.maxFallSpeed)
            {
                this._velocity.y = this.maxFallSpeed;
            }
        }

        /// <summary>
        /// Rigidbody速度反映
        /// </summary>
        private void ApplyVelocity()
        {
            Vector2 currentVelocity = this._rigidbody2D.linearVelocity;
            currentVelocity.x = this._velocity.x;
            currentVelocity.y = this._velocity.y;
            this._rigidbody2D.linearVelocity = currentVelocity;
        }

        /// <summary>
        /// 接地判定更新
        /// </summary>
        private void UpdateGrounded()
        {
            // コライダー下端の3点をレイ判定し、段差や端でも接地を取りこぼしにくくしている。
            Bounds bounds = this._boxCollider2D.bounds;

            float leftX = bounds.min.x + this.groundRayInset;
            float centerX = bounds.center.x;
            float rightX = bounds.max.x - this.groundRayInset;
            float originY = bounds.min.y + 0.02f;

            bool isGrounded =
                this.CheckGround(new Vector2(leftX, originY)) ||
                this.CheckGround(new Vector2(centerX, originY)) ||
                this.CheckGround(new Vector2(rightX, originY));

            this._isGrounded.Value = isGrounded;

            if (this._isGrounded.Value && this._velocity.y < 0f)
            {
                this._velocity.y = 0f;

                Vector2 rbVelocity = this._rigidbody2D.linearVelocity;
                rbVelocity.y = 0f;
                this._rigidbody2D.linearVelocity = rbVelocity;
            }
        }

        /// <summary>
        /// 単一点接地判定
        /// </summary>
        private bool CheckGround(Vector2 origin)
        {
            int hitCount = Physics2D.Raycast(
                origin,
                Vector2.down,
                this._checkGroundFilter,
                this._hits,
                this.groundCheckDistance
            );

            return hitCount > 0;
        }

#if UNITY_EDITOR
        /// <summary>
        /// 接地判定可視化
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            BoxCollider2D boxCollider2D = this.GetComponent<BoxCollider2D>();
            if (boxCollider2D == null)
            {
                return;
            }

            Bounds bounds = boxCollider2D.bounds;

            float inset = this.groundRayInset;
            float originY = bounds.min.y + 0.02f;

            Vector2 leftOrigin = new Vector2(bounds.min.x + inset, originY);
            Vector2 centerOrigin = new Vector2(bounds.center.x, originY);
            Vector2 rightOrigin = new Vector2(bounds.max.x - inset, originY);

            Gizmos.color = Color.green;
            Gizmos.DrawLine(leftOrigin, leftOrigin + Vector2.down * this.groundCheckDistance);
            Gizmos.DrawLine(centerOrigin, centerOrigin + Vector2.down * this.groundCheckDistance);
            Gizmos.DrawLine(rightOrigin, rightOrigin + Vector2.down * this.groundCheckDistance);
        }
#endif
    }
}