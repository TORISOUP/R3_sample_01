using UnityEngine;
using UnityEngine.InputSystem;
using R3;

namespace R3Samples.Introduction.SampleGame
{
    /// <summary>
    /// プレイヤー入力を読み取り、方向入力、ジャンプ、攻撃の状態を公開する責務を持つ
    /// キーボードとゲームパッドの差異はここで吸収する
    /// </summary>
    public class InputSystemInputEventProvider : MonoBehaviour, IInputEventProvider
    {
        /// <summary>
        /// スティック無効範囲
        /// </summary>
        [SerializeField] private float _deadZone = 0.3f;

        /// <summary>
        /// 方向入力状態
        /// </summary>
        private readonly ReactiveProperty<InputDirection> _direction = new(InputDirection.None);
        /// <summary>
        /// ジャンプ入力状態
        /// </summary>
        private readonly ReactiveProperty<bool> _isJump = new(false);
        /// <summary>
        /// 攻撃入力状態
        /// </summary>
        private readonly ReactiveProperty<bool> _isAttack = new(false);
        
        /// <summary>
        /// 移動入力アクション
        /// </summary>
        private InputAction _moveAction;
        /// <summary>
        /// ジャンプ入力アクション
        /// </summary>
        private InputAction _jumpAction;
        /// <summary>
        /// 攻撃入力アクション
        /// </summary>
        private InputAction _attackAction;

        public ReadOnlyReactiveProperty<InputDirection> Direction => _direction;
        public ReadOnlyReactiveProperty<bool> IsJumpButton => _isJump;
        public ReadOnlyReactiveProperty<bool> IsAttackButton => _isAttack;

        /// <summary>
        /// 入力アクション生成とイベント登録
        /// </summary>
        private void Awake()
        {
            _moveAction = CreateMoveAction();
            _jumpAction = CreateJumpAction();
            _attackAction = CreateAttackAction();

            _moveAction.performed += OnMovePerformed;
            _moveAction.canceled += OnMoveCanceled;
            _jumpAction.performed += OnJumpPerformed;
            _jumpAction.canceled += OnJumpCanceled;
            _attackAction.performed += OnAttackPerformed;
            _attackAction.canceled += OnAttackCanceled;
        }

        /// <summary>
        /// 入力受付開始
        /// </summary>
        private void OnEnable()
        {
            _moveAction.Enable();
            _jumpAction.Enable();
            _attackAction.Enable();
        }

        /// <summary>
        /// 入力受付停止
        /// </summary>
        private void OnDisable()
        {
            _moveAction.Disable();
            _jumpAction.Disable();
            _attackAction.Disable();
        }

        /// <summary>
        /// イベント解除と破棄
        /// </summary>
        private void OnDestroy()
        {
            _moveAction.performed -= OnMovePerformed;
            _moveAction.canceled -= OnMoveCanceled;
            _jumpAction.performed -= OnJumpPerformed;
            _jumpAction.canceled -= OnJumpCanceled;
            _attackAction.performed -= OnAttackPerformed;
            _attackAction.canceled -= OnAttackCanceled;

            _moveAction.Dispose();
            _jumpAction.Dispose();
            _attackAction.Dispose();
        }

        /// <summary>
        /// 移動アクション生成
        /// </summary>
        private static InputAction CreateMoveAction()
        {
            var action = new InputAction("Move", InputActionType.Value);
            action.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            action.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");
            action.AddBinding("<Gamepad>/leftStick");
            return action;
        }

        /// <summary>
        /// ジャンプアクション生成
        /// </summary>
        private static InputAction CreateJumpAction()
        {
            var action = new InputAction("Jump", InputActionType.Button);
            action.AddBinding("<Keyboard>/space");
            action.AddBinding("<Gamepad>/buttonSouth");
            return action;
        }

        /// <summary>
        /// 攻撃アクション生成
        /// </summary>
        private static InputAction CreateAttackAction()
        {
            var action = new InputAction("Attack", InputActionType.Button);
            action.AddBinding("<Keyboard>/z");
            action.AddBinding("<Gamepad>/buttonWest");
            return action;
        }

        /// <summary>
        /// 移動入力反映
        /// </summary>
        private void OnMovePerformed(InputAction.CallbackContext context)
        {
            var input = context.ReadValue<Vector2>().normalized;
            _direction.OnNext(CheckDirection(input, _deadZone));
        }

        /// <summary>
        /// 移動入力解除反映
        /// </summary>
        private void OnMoveCanceled(InputAction.CallbackContext _)
        {
            _direction.OnNext(InputDirection.None);
        }

        /// <summary>
        /// ジャンプ押下反映
        /// </summary>
        private void OnJumpPerformed(InputAction.CallbackContext _)
        {
            _isJump.OnNext(true);
        }

        /// <summary>
        /// ジャンプ離上反映
        /// </summary>
        private void OnJumpCanceled(InputAction.CallbackContext _)
        {
            _isJump.OnNext(false);
        }

        /// <summary>
        /// 攻撃押下反映
        /// </summary>
        private void OnAttackPerformed(InputAction.CallbackContext _)
        {
            _isAttack.OnNext(true);
        }

        /// <summary>
        /// 攻撃離上反映
        /// </summary>
        private void OnAttackCanceled(InputAction.CallbackContext _)
        {
            _isAttack.OnNext(false);
        }

        /// <summary>
        /// ベクトルから8方向変換
        /// </summary>
        private static InputDirection CheckDirection(Vector2 input, float deadZone)
        {
            if (input.magnitude < deadZone)
            {
                return InputDirection.None;
            }

            var angle = Mathf.Atan2(input.y, input.x) * Mathf.Rad2Deg;
            if (angle < 0f) angle += 360f;

            if (angle >= 337.5f || angle < 22.5f) return InputDirection.Right;
            if (angle < 67.5f) return InputDirection.UpRight;
            if (angle < 112.5f) return InputDirection.Up;
            if (angle < 157.5f) return InputDirection.UpLeft;
            if (angle < 202.5f) return InputDirection.Left;
            if (angle < 247.5f) return InputDirection.DownLeft;
            if (angle < 292.5f) return InputDirection.Down;
            return InputDirection.DownRight;
        }
    }
}
