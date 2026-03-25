using UnityEngine;

namespace R3Samples.Introduction.SampleGame
{
    /// <summary>
    /// 被ダメージ対象
    /// </summary>
    public interface IDamageable
    {
        /// <summary>
        /// 攻撃主体種別
        /// </summary>
        AttackerType Type { get; }
        /// <summary>
        /// 指定座標との相対方向取得
        /// </summary>
        Vector2 GetDirection(Vector2 position);
        /// <summary>
        /// 被ダメージ通知
        /// </summary>
        void OnDamaged(Damage damage);
    }

    /// <summary>
    /// ダメージ情報
    /// </summary>
    public readonly struct Damage
    {
        /// <summary>
        /// 攻撃主体種別
        /// </summary>
        public AttackerType Type { get; }
        /// <summary>
        /// ダメージ量
        /// </summary>
        public float DamageValue { get; }
        /// <summary>
        /// 付与速度
        /// </summary>
        public Vector2 Velocity { get; }

        /// <summary>
        /// ダメージ情報生成
        /// </summary>
        public Damage(AttackerType type, float damageValue, Vector2 velocity)
        {
            Type = type;
            DamageValue = damageValue;
            Velocity = velocity;
        }
    }

    /// <summary>
    /// 攻撃主体種別
    /// </summary>
    public enum AttackerType
    {
        Unknown,
        Player,
        Enemy
    }
}
