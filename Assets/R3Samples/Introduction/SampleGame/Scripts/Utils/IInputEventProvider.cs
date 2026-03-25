using R3;

namespace R3Samples.Introduction.SampleGame
{
    /// <summary>
    /// ユーザーの入力操作を提供する
    /// </summary>
    public interface IInputEventProvider
    {
        /// <summary>
        /// 8方向入力
        /// </summary>
        public ReadOnlyReactiveProperty<InputDirection> Direction { get; }
        /// <summary>
        /// ジャンプ入力状態
        /// </summary>
        public ReadOnlyReactiveProperty<bool> IsJumpButton { get; }
        /// <summary>
        /// 攻撃入力状態
        /// </summary>
        public ReadOnlyReactiveProperty<bool> IsAttackButton { get; }
    }
    
    /// <summary>
    /// 8方向入力種別
    /// </summary>
    public enum InputDirection
    {
        None,
        Up,
        UpRight,
        Right,
        Down,
        DownRight,
        DownLeft,
        Left,
        UpLeft,
    }
}
