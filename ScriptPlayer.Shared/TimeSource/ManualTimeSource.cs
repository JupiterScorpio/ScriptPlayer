using System;
using System.Diagnostics;

namespace ScriptPlayer.Shared
{
    public class ManualTimeSource : TimeSource
    {
        public override string Name => "Manuel Timesource";
        public override bool ShowBanner => false;
        public override string ConnectInstructions => "";

        private readonly ISampleClock _clock;
        private readonly object _clocklock = new object();

        private TimeSpan _lastProgress;
        private DateTime _lastCheckpoint;
        private TimeSpan _maxOffset;
        private double _playbackRate = 1.0;

        public override double PlaybackRate
        {
            get => _playbackRate;
            set
            {
                if (value.Equals(_playbackRate)) return;
                _playbackRate = value;
                OnPropertyChanged();
                OnPlaybackRateChanged(_playbackRate);
            }
        }

        public override bool CanPlayPause => true;
        public override bool CanSeek => true;
        public override bool CanOpenMedia => true;

        public ManualTimeSource(ISampleClock clock) : this(clock, TimeSpan.FromMilliseconds(30))
        { }

        public ManualTimeSource(ISampleClock clock, TimeSpan maxOffset)
        {
            IsConnected = true;

            _maxOffset = maxOffset;
            _clock = clock;
            _clock.Tick += ClockOnTick;
        }

        public void SetDuration(TimeSpan duration)
        {
            if (Duration == duration)
                return;

            Duration = duration;
            Debug.WriteLine($"Duration of ManualTimeSource set to {duration:g}");
        }

        public void SetPlaybackRate(double playbackRate)
        {
            if (Math.Abs(PlaybackRate - playbackRate) < 0.01)
                return;

            PlaybackRate = playbackRate;
            Debug.WriteLine($"Duration of ManualTimeSource set to {playbackRate:g}");
        }

        public override void SetPosition(TimeSpan position)
        {
            //Calculate Expected Position and Compare:

            DateTime now = DateTime.Now;
            TimeSpan elapsed = (now - _lastCheckpoint).Multiply(PlaybackRate);
            TimeSpan expected = _lastProgress + elapsed;

            TimeSpan diff = expected - position;

            //Debug.WriteLine("Time Offset: " + diff.TotalMilliseconds.ToString("f2") + " ms [" + position.ToString("h\\:mm\\:ss\\.fff") + "]");

            if (Math.Abs(diff.TotalMilliseconds) < Math.Abs(_maxOffset.TotalMilliseconds))
                return;

            lock (_clocklock)
            {
                Debug.WriteLine("Offset too high ({0}), adjusting ...", diff);
                _lastCheckpoint = DateTime.Now;
                _lastProgress = position;
                Progress = position;
            }
        }

        public override void Play()
        {
            if (IsPlaying)
                return;

            RefreshProgress();
            IsPlaying = true;
        }

        public override void Pause()
        {
            if (!IsPlaying)
                return;

            RefreshProgress();
            IsPlaying = false;
        }

        private void ClockOnTick(object sender, EventArgs eventArgs)
        {
            RefreshProgress();
        }

        private void RefreshProgress()
        {
            if (IsPlaying)
            {
                lock (_clocklock)
                {
                    DateTime now = DateTime.Now;
                    TimeSpan elapsed = (now - _lastCheckpoint).Multiply(PlaybackRate);
                    _lastProgress += elapsed;
                    _lastCheckpoint = now;
                    Progress = _lastProgress;
                }
            }
            else
            {
                _lastCheckpoint = DateTime.Now;
            }
        }
    }
}