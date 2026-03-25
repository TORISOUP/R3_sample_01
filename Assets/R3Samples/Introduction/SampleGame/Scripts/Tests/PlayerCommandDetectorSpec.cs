using System;
using System.Collections.Generic;
using Microsoft.Extensions.Time.Testing;
using NUnit.Framework;
using R3;

namespace R3Samples.Introduction.SampleGame.Tests
{
    /// <summary>
    /// PlayerCommandDetector の仕様確認
    /// </summary>
    public class PlayerCommandDetectorSpec
    {
        /// <summary>
        /// 1フレーム時間
        /// </summary>
        private static readonly TimeSpan Frame = TimeSpan.FromSeconds(1d / 60d);

        [Test]
        public void FireShot_右向きコマンドを時間内に入力すると検知する()
        {
            using var context = new DetectorTestContext();
            var intervalFrames = MaxFramesWithinWindow(PlayerCommandDetector.FireShotOrUpperPunchWindow, 2);

            context.EmitDirection(InputDirection.Down);
            context.AdvanceFrames(intervalFrames);
            context.EmitDirection(InputDirection.DownRight);
            context.AdvanceFrames(intervalFrames);
            context.EmitDirection(InputDirection.Right);

            CollectionAssert.AreEqual(new[] { CommandMove.FireShot }, context.DetectedMoves);
        }

        [Test]
        public void FireShot_左向きコマンドを時間内に入力すると検知する()
        {
            using var context = new DetectorTestContext();
            var intervalFrames = MaxFramesWithinWindow(PlayerCommandDetector.FireShotOrUpperPunchWindow, 2);

            context.EmitDirection(InputDirection.Down);
            context.AdvanceFrames(intervalFrames);
            context.EmitDirection(InputDirection.DownLeft);
            context.AdvanceFrames(intervalFrames);
            context.EmitDirection(InputDirection.Left);

            CollectionAssert.AreEqual(new[] { CommandMove.FireShot }, context.DetectedMoves);
        }

        [Test]
        public void UpperPunch_右向きコマンドを時間内に入力すると検知する()
        {
            using var context = new DetectorTestContext();
            var intervalFrames = MaxFramesWithinWindow(PlayerCommandDetector.FireShotOrUpperPunchWindow, 2);

            context.EmitDirection(InputDirection.Right);
            context.AdvanceFrames(intervalFrames);
            context.EmitDirection(InputDirection.Down);
            context.AdvanceFrames(intervalFrames);
            context.EmitDirection(InputDirection.DownRight);

            CollectionAssert.AreEqual(new[] { CommandMove.UpperPunch }, context.DetectedMoves);
        }

        [Test]
        public void UpperPunch_末尾に余計な入力があっても直前3入力が成立していれば検知する()
        {
            using var context = new DetectorTestContext();
            var intervalFrames = MaxFramesWithinWindow(PlayerCommandDetector.UpperPunchTrailingWindow, 3);

            context.EmitDirection(InputDirection.Right);
            context.AdvanceFrames(intervalFrames);
            context.EmitDirection(InputDirection.Down);
            context.AdvanceFrames(intervalFrames);
            context.EmitDirection(InputDirection.DownRight); // ここで１コ目成立
            context.AdvanceFrames(intervalFrames);
            context.EmitDirection(InputDirection.Right); // 直近3つを見るとFireShotだが、UpperPunch扱いになるはず

            CollectionAssert.AreEqual(new[] { CommandMove.UpperPunch, CommandMove.UpperPunch }, context.DetectedMoves);
        }

        [Test]
        public void SpinAttack_右回りコマンドを時間内に入力すると検知する()
        {
            using var context = new DetectorTestContext();
            var intervalFrames = MaxFramesWithinWindow(PlayerCommandDetector.SpinAttackWindow, 4);

            context.EmitDirection(InputDirection.Left);
            context.AdvanceFrames(intervalFrames);
            context.EmitDirection(InputDirection.DownLeft);
            context.AdvanceFrames(intervalFrames);
            context.EmitDirection(InputDirection.Down);
            context.AdvanceFrames(intervalFrames);
            context.EmitDirection(InputDirection.DownRight);
            context.AdvanceFrames(intervalFrames);
            context.EmitDirection(InputDirection.Right);

            CollectionAssert.AreEqual(new[] { CommandMove.SpinAttack }, context.DetectedMoves);
        }

        [Test]
        public void 同じ方向の連続入力とNoneはコマンド判定から除外される()
        {
            using var context = new DetectorTestContext();

            context.EmitDirection(InputDirection.None);
            context.EmitDirection(InputDirection.Down);
            context.AdvanceFrames(3);
            context.EmitDirection(InputDirection.Down);
            context.AdvanceFrames(3);
            context.EmitDirection(InputDirection.None);
            context.AdvanceFrames(3);
            context.EmitDirection(InputDirection.DownRight);
            context.AdvanceFrames(3);
            context.EmitDirection(InputDirection.Right);

            CollectionAssert.AreEqual(new[] { CommandMove.FireShot }, context.DetectedMoves);
        }

        [Test]
        public void FireShot_入力間隔が長すぎると検知しない()
        {
            using var context = new DetectorTestContext();
            var intervalFrames = FramesExceedingWindow(PlayerCommandDetector.FireShotOrUpperPunchWindow, 2);

            context.EmitDirection(InputDirection.Down);
            context.AdvanceFrames(intervalFrames);
            context.EmitDirection(InputDirection.DownRight);
            context.AdvanceFrames(intervalFrames);
            context.EmitDirection(InputDirection.Right);

            CollectionAssert.IsEmpty(context.DetectedMoves);
        }

        [Test]
        public void UpperPunch_3入力が時間窓を超えると検知しない()
        {
            using var context = new DetectorTestContext();
            var intervalFrames = FramesExceedingWindow(PlayerCommandDetector.FireShotOrUpperPunchWindow, 2);

            context.EmitDirection(InputDirection.Right);
            context.AdvanceFrames(intervalFrames);
            context.EmitDirection(InputDirection.Down);
            context.AdvanceFrames(intervalFrames);
            context.EmitDirection(InputDirection.DownRight);

            CollectionAssert.IsEmpty(context.DetectedMoves);
        }

        [Test]
        public void SpinAttack_入力間隔が長すぎると検知しない()
        {
            using var context = new DetectorTestContext();
            var intervalFrames = FramesExceedingWindow(PlayerCommandDetector.SpinAttackWindow, 4);

            context.EmitDirection(InputDirection.Left);
            context.AdvanceFrames(intervalFrames);
            context.EmitDirection(InputDirection.DownLeft);
            context.AdvanceFrames(intervalFrames);
            context.EmitDirection(InputDirection.Down);
            context.AdvanceFrames(intervalFrames);
            context.EmitDirection(InputDirection.DownRight);
            context.AdvanceFrames(intervalFrames);
            context.EmitDirection(InputDirection.Right);

            CollectionAssert.IsEmpty(context.DetectedMoves);
        }

        /// <summary>
        /// コマンド成立時間内に収まる待ち時間を作る
        /// </summary>
        private static int MaxFramesWithinWindow(TimeSpan totalWindow, int intervalCount)
        {
            var safeWindow = TimeSpan.FromTicks((long)(totalWindow.Ticks * 0.7d));
            return (int)(safeWindow.Ticks / (Frame.Ticks * intervalCount));
        }

        /// <summary>
        /// コマンド成立時間を超える待ち時間を作る
        /// </summary>
        private static int FramesExceedingWindow(TimeSpan totalWindow, int intervalCount)
        {
            return (int)(totalWindow.Ticks / (Frame.Ticks * intervalCount)) + 1;
        }

        /// <summary>
        /// テスト用の状態保存
        /// </summary>
        private sealed class DetectorTestContext : IDisposable
        {
            private readonly FakeInputEventProvider _inputEventProvider = new();
            private readonly FakeTimeProvider _timeProvider = new(DateTimeOffset.UnixEpoch);
            private readonly IDisposable _subscription;
            private readonly PlayerCommandDetector _detector;

            public DetectorTestContext()
            {
                _detector = new PlayerCommandDetector(_inputEventProvider, _timeProvider);
                _subscription = _detector.CommandMove
                    .Where(x => x != CommandMove.None)
                    .Subscribe(DetectedMoves.Add);
            }

            // 判定結果
            public List<CommandMove> DetectedMoves { get; } = new();

            // コマンド入力
            public void EmitDirection(InputDirection direction)
            {
                _inputEventProvider.EmitDirection(direction);
            }

            // フレーム経過
            public void AdvanceFrames(int frames)
            {
                _timeProvider.Advance(TimeSpan.FromTicks(Frame.Ticks * frames));
            }

            public void Dispose()
            {
                _subscription.Dispose();
                _detector.Dispose();
            }
        }

        /// <summary>
        /// テスト用入力供給元
        /// </summary>
        private sealed class FakeInputEventProvider : IInputEventProvider
        {
            private readonly ReactiveProperty<InputDirection> _direction = new(InputDirection.None);
            private readonly ReactiveProperty<bool> _isJumpButton = new(false);
            private readonly ReactiveProperty<bool> _isAttackButton = new(false);

            public ReadOnlyReactiveProperty<InputDirection> Direction => _direction;
            public ReadOnlyReactiveProperty<bool> IsJumpButton => _isJumpButton;
            public ReadOnlyReactiveProperty<bool> IsAttackButton => _isAttackButton;

            public void EmitDirection(InputDirection direction)
            {
                _direction.OnNext(direction);
            }
        }
    }
}
