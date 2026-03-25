using System;
using System.Linq;
using System.Text;
using System.Threading;
using R3;

namespace R3Samples.Introduction.SampleGame
{
    /// <summary>
    /// 入力履歴からコマンド技を判定する責務を持つ。
    /// 方向入力の並びと入力時間を見て必殺技コマンドを通知する。
    /// </summary>
    public sealed class PlayerCommandDetector : IDisposable
    {
        /// <summary>
        /// 時刻付き方向入力
        /// </summary>
        private record TimedDirection(long Timestamp, InputDirection Direction);

        /// <summary>
        /// 回転攻撃成立時間
        /// </summary>
        public static readonly TimeSpan SpinAttackWindow = TimeSpan.FromSeconds(50f / 60);
        /// <summary>
        /// 昇竜拳緩和成立時間
        /// </summary>
        public static readonly TimeSpan UpperPunchTrailingWindow = TimeSpan.FromSeconds(20f / 60);
        /// <summary>
        /// 波動拳/昇竜拳成立時間
        /// </summary>
        public static readonly TimeSpan FireShotOrUpperPunchWindow = TimeSpan.FromSeconds(20f / 60);

        private readonly CancellationTokenSource _cts = new();
        private readonly Subject<CommandMove> _commandMoveSubject = new();
        private TimeProvider _timeProvider = TimeProvider.System;
        public Observable<CommandMove> CommandMove => _commandMoveSubject.Prepend(SampleGame.CommandMove.None);

        /// <summary>
        /// Update時間基準での生成
        /// </summary>
        public PlayerCommandDetector(IInputEventProvider inputEventProvider)
        {
            Initialize(inputEventProvider, UnityTimeProvider.Update);
        }

        /// <summary>
        /// 時間供給元指定生成
        /// </summary>
        public PlayerCommandDetector(IInputEventProvider inputEventProvider, TimeProvider timeProvider)
        {
            Initialize(inputEventProvider, timeProvider);
        }

        /// <summary>
        /// 判定パイプライン初期化
        /// </summary>
        private void Initialize(IInputEventProvider inputEventProvider, TimeProvider timeProvider)
        {
            _timeProvider = timeProvider;

            // ゼロ埋めに使うデータ定義
            var zeroPaddingData 
                = Enumerable.Repeat(new TimedDirection(0, InputDirection.None), 5)
                    .ToArray();
            
            // 8方向入力の変化のみを取り出す
            var directionChanges = inputEventProvider.Direction
                // 差分のみを検知
                .DistinctUntilChanged()
                // 無入力は無視する
                .Where(direction => direction != InputDirection.None);

            // 入力にタイムスタンプを付与する
            var timedDirections = directionChanges
                .Timestamp(timeProvider)
                .Select(x => new TimedDirection(x.Timestamp, x.Value));

            // 直近の5回分の入力を保持する
            var commandWindows = timedDirections
                // Chunkを満たして即コマンド判定できるようにしておく
                .Prepend(zeroPaddingData)
                .Chunk(5, 1);

            // 直近の5回分の入力をもとにコマンドの判定処理を行う
            commandWindows
                .Subscribe(CheckCommands)
                .RegisterTo(_cts.Token);
        }
        
        /// <summary>
        /// 入力コマンドの配列を比較して技が成立したかを見る
        /// </summary>
        /// <param name="commands"></param>
        private void CheckCommands(TimedDirection[] commands)
        {
            var commandSpan = commands.AsSpan();

            if (TryDetectSpinAttack(commandSpan)) return;
            if (TryDetectUpperPunchWithTrailingInput(commandSpan)) return;
            if (TryDetectFireShotOrUpperPunch(commandSpan)) return;
        }

        /// <summary>
        /// 回転攻撃判定
        /// </summary>
        private bool TryDetectSpinAttack(ReadOnlySpan<TimedDirection> commands)
        {
            if (commands.Length != 5) return false;
            // 入力猶予以内に成立したか
            if (GetElapsedTime(commands) > SpinAttackWindow) return false;

            if (IsDirectionSequence(commands,
                    InputDirection.Left,
                    InputDirection.DownLeft,
                    InputDirection.Down,
                    InputDirection.DownRight,
                    InputDirection.Right) ||
                IsDirectionSequence(commands,
                    InputDirection.Right,
                    InputDirection.DownRight,
                    InputDirection.Down,
                    InputDirection.DownLeft,
                    InputDirection.Left))
            {
                _commandMoveSubject.OnNext(SampleGame.CommandMove.SpinAttack);
                return true;
            }

            return false;
        }

        /// <summary>
        /// 末尾余剰入力込み昇竜拳判定
        /// </summary>
        private bool TryDetectUpperPunchWithTrailingInput(ReadOnlySpan<TimedDirection> commands)
        {
            if (commands.Length < 4) return false;

            var last4 = commands.Slice(commands.Length - 4, 4);
            if (GetElapsedTime(last4) > UpperPunchTrailingWindow) return false;

            // 最後の入力は無視する緩め実装
            if (IsDirectionSequence(last4[..3],
                    InputDirection.Right,
                    InputDirection.Down,
                    InputDirection.DownRight) ||
                IsDirectionSequence(last4[..3],
                    InputDirection.Left,
                    InputDirection.Down,
                    InputDirection.DownLeft))
            {
                _commandMoveSubject.OnNext(SampleGame.CommandMove.UpperPunch);
                return true;
            }

            return false;
        }

        /// <summary>
        /// 波動拳または昇竜拳判定
        /// </summary>
        private bool TryDetectFireShotOrUpperPunch(ReadOnlySpan<TimedDirection> commands)
        {
            if (commands.Length < 3) return false;

            var last3 = commands.Slice(commands.Length - 3, 3);
            if (GetElapsedTime(last3) > FireShotOrUpperPunchWindow) return false;

            if (IsDirectionSequence(last3,
                    InputDirection.Down,
                    InputDirection.DownRight,
                    InputDirection.Right) ||
                IsDirectionSequence(last3,
                    InputDirection.Down,
                    InputDirection.DownLeft,
                    InputDirection.Left))
            {
                _commandMoveSubject.OnNext(SampleGame.CommandMove.FireShot);
                return true;
            }

            if (IsDirectionSequence(last3,
                    InputDirection.Right,
                    InputDirection.Down,
                    InputDirection.DownRight) ||
                IsDirectionSequence(last3,
                    InputDirection.Left,
                    InputDirection.Down,
                    InputDirection.DownLeft))
            {
                _commandMoveSubject.OnNext(SampleGame.CommandMove.UpperPunch);
                return true;
            }

            return false;
        }

        /// <summary>
        /// 入力区間時間取得
        /// </summary>
        private TimeSpan GetElapsedTime(ReadOnlySpan<TimedDirection> commands)
        {
            return _timeProvider.GetElapsedTime(commands[0].Timestamp, commands[^1].Timestamp);
        }

        /// <summary>
        /// 入力列一致判定
        /// </summary>
        private static bool IsDirectionSequence(
            ReadOnlySpan<TimedDirection> commands,
            params InputDirection[] expectedDirections)
        {
            if (commands.Length != expectedDirections.Length) return false;

            for (var i = 0; i < expectedDirections.Length; i++)
            {
                if (commands[i].Direction != expectedDirections[i]) return false;
            }

            return true;
        }

        /// <summary>
        /// 入力列デバッグ文字列化
        /// </summary>
        private static string DebugDirections(ReadOnlySpan<TimedDirection> commands)
        {
            var sb = new StringBuilder(commands.Length);
            foreach (var command in commands)
            {
                sb.Append(command.Direction switch
                {
                    InputDirection.Up => $"↑({command.Timestamp}) ",
                    InputDirection.UpRight => $"↗({command.Timestamp}) ",
                    InputDirection.Right => $"→({command.Timestamp}) ",
                    InputDirection.DownRight => $"↘({command.Timestamp}) ",
                    InputDirection.Down => $"↓({command.Timestamp}) ",
                    InputDirection.DownLeft => $"↙({command.Timestamp}) ",
                    InputDirection.Left => $"←({command.Timestamp}) ",
                    InputDirection.UpLeft => $"↖({command.Timestamp}) ",
                    _ => "・",
                });
            }

            return sb.ToString();
        }

        /// <summary>
        /// 購読解除
        /// </summary>
        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();

            _commandMoveSubject.Dispose();
        }
    }

    /// <summary>
    /// コマンド技種別
    /// </summary>
    public enum CommandMove
    {
        None,
        UpperPunch,
        SpinAttack,
        FireShot
    }
}
