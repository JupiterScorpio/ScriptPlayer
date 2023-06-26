using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace ScriptPlayer.Shared
{
    public class ZoomPlayerTimeSource : TimeSource, IDisposable, IOnScreenDisplay
    {
        public override string Name => "Zoom Player";
        public override bool ShowBanner => true;
        public override string ConnectInstructions => "Not connected.\r\nStart Zoom Player and enable External TCP Control (Advanced Mode - System).";

        private ZoomPlayerConnectionSettings _connectionSettings;

        private readonly Thread _clientLoop;
        private readonly ManualTimeSource _timeSource;

        private bool _running = true;
        private TcpClient _client;
        private TimeSpan _lastReceivedTimestamp = TimeSpan.MaxValue;
        private string _previousDesignation;

        public ZoomPlayerTimeSource(ISampleClock clock, ZoomPlayerConnectionSettings connectionSettings)
        {
            _connectionSettings = connectionSettings;

            _timeSource = new ManualTimeSource(clock, TimeSpan.FromMilliseconds(100));
            _timeSource.DurationChanged += TimeSourceOnDurationChanged;
            _timeSource.IsPlayingChanged += TimeSourceOnIsPlayingChanged;
            _timeSource.ProgressChanged += TimeSourceOnProgressChanged;
            _timeSource.PlaybackRateChanged += TimeSourceOnPlaybackRateChanged;

            _clientLoop = new Thread(ClientLoop);
            _clientLoop.Start();
        }

        private void TimeSourceOnPlaybackRateChanged(object sender, double d)
        {
            OnPlaybackRateChanged(d);
        }

        public void UpdateConnectionSettings(ZoomPlayerConnectionSettings connectionSettings)
        {
            _connectionSettings = connectionSettings;
        }

        private void TimeSourceOnProgressChanged(object sender, TimeSpan progress)
        {
            Progress = progress;
        }

        private void TimeSourceOnDurationChanged(object sender, TimeSpan duration)
        {
            Duration = duration;
        }

        private void TimeSourceOnIsPlayingChanged(object sender, bool isPlaying)
        {
            IsPlaying = isPlaying;
        }

        private void ClientLoop()
        {
            while (_running)
            {
                try
                {
                    _client = new TcpClient();
                    _client.Connect(_connectionSettings.ToEndpoint());

                    SetConnected(true);

                    SendCommand(ZoomPlayerCommandCodes.RequestPlayingFileName, "");
                    Thread.Sleep(1000);
                    SendCommand(ZoomPlayerCommandCodes.SendTimelineUpdate, "1");
                    Thread.Sleep(1000);
                    SendCommand(ZoomPlayerCommandCodes.SendTimelineUpdate, "2");

                    using (NetworkStream stream = _client.GetStream())
                    {
                        using (StreamReader reader = new StreamReader(stream))
                        {
                            while (!reader.EndOfStream)
                            {
                                string line = reader.ReadLine();
                                InterpretLine(line);
                            }
                        }
                    }
                }
                catch (ThreadAbortException)
                {
                    return;
                }
                catch (ThreadInterruptedException)
                {
                    return;
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e.Message);
                    Thread.Sleep(1000);
                }
                finally
                {
                    DispatchPause();
                    _client.Dispose();
                    _client = null;

                    if (_running)
                        SetConnected(false);
                }
            }
        }

        private void DispatchPause()
        {
            if (CheckAccess())
                _timeSource.Pause();
            else
            {
                try
                {
                    Dispatcher.Invoke(DispatchPause);
                }
                catch
                {

                }
            }
        }

        private void SetConnected(bool isConnected)
        {
            if (CheckAccess())
                IsConnected = isConnected;
            else
            {
                try
                {
                    Dispatcher.Invoke(() => { SetConnected(isConnected); });
                }
                catch
                {

                }
            }
        }

        private void InterpretLine(string line)
        {
            if (_timeSource.CheckAccess())
            {
                //Debug.WriteLine(line);

                ZoomPlayerMessageCodes commandCode = (ZoomPlayerMessageCodes)int.Parse(line.Substring(0, 4));
                string parameter = line.Substring(4).Trim();

                switch (commandCode)
                {
                    case ZoomPlayerMessageCodes.StateChanged:
                        ZoomPlayerPlaybackStates state = (ZoomPlayerPlaybackStates)int.Parse(parameter);
                        if (state == ZoomPlayerPlaybackStates.Playing)
                            _timeSource.Play();
                        else
                            _timeSource.Pause();

                        break;
                    case ZoomPlayerMessageCodes.PositionUpdate:
                        string[] parts = parameter.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(s => s.Trim()).ToArray();

                        string[] timeFormats = { "hh\\:mm\\:ss", "mm\\:ss" };

                        TimeSpan position = TimeSpan.ParseExact(parts[0], timeFormats, CultureInfo.InvariantCulture);
                        TimeSpan duration = TimeSpan.ParseExact(parts[1], timeFormats, CultureInfo.InvariantCulture);

                        _timeSource.SetDuration(duration);
                        _timeSource.SetPosition(position);
                        break;
                    case ZoomPlayerMessageCodes.CurrentlyLoadedFile:
                        if (!String.IsNullOrWhiteSpace(parameter))
                            OnFileOpened(parameter);
                        break;
                }
            }
            else
            {
                _timeSource.Dispatcher.Invoke(() => InterpretLine(line));
            }
        }

        public override double PlaybackRate
        {
            get => _timeSource.PlaybackRate;
            set => _timeSource.PlaybackRate = value;
        }
        public override bool CanPlayPause => true;
        public override bool CanSeek => true;
        public override bool CanOpenMedia => false;

        public override void Play()
        {
            SendCommand(ZoomPlayerCommandCodes.CallFunction, "fnPlay");
        }

        public override void Pause()
        {
            SendCommand(ZoomPlayerCommandCodes.CallFunction, "fnPause");
        }

        public override void SetPosition(TimeSpan position)
        {
            SendCommand(ZoomPlayerCommandCodes.SetCurrentPosition, position.TotalSeconds.ToString("f3"));
        }

        private void SendCommand(ZoomPlayerCommandCodes command, string parameter)
        {
            try
            {
                string message = $"{((int)command):0000} {parameter}\r\n";
                byte[] data = Encoding.ASCII.GetBytes(message);

                _client?.GetStream().Write(data, 0, data.Length);
            }
            catch (Exception e)
            {
                Debug.WriteLine("Exception in ZoomPlayerTimeSource.SendCommand: " + e.Message);
            }
        }

        public void SetDuration(TimeSpan duration)
        {
            _timeSource.SetDuration(duration);
        }

        public void Dispose()
        {
            _running = false;
            _client?.Dispose();
            _clientLoop?.Interrupt();
            _clientLoop?.Abort();
        }

        public override IOnScreenDisplay OnScreenDisplay => this;

        public void ShowMessage(string designation, string text, TimeSpan duration)
        {
            _previousDesignation = designation;
            SendCommand(ZoomPlayerCommandCodes.SetOsdDuration, Math.Round(duration.TotalSeconds).ToString());
            SendCommand(ZoomPlayerCommandCodes.ShowOsd, text);
        }

        public void HideMessage(string designation)
        {
            if (_previousDesignation != designation)
                return;

            SendCommand(ZoomPlayerCommandCodes.SetOsdDuration, "0");
            SendCommand(ZoomPlayerCommandCodes.ShowOsd, "");
        }

        public void ShowSkipButton()
        { }

        public void ShowSkipNextButton()
        { }

        public void HideSkipButton()
        { }
    }

    public enum ZoomPlayerMessageCodes
    {
        StateChanged = 1000,
        PositionUpdate = 1100,
        CurrentlyLoadedFile = 1800
    }

    /*
        1200 - Show a PopUp OSD Text         | Parameter is a UTF8 encoded text to be
                                               shown as a PopUp OSD
        1201 - Temp Disable PopUp OSD        | Temporarily Disables the PopUp OSD
        1202 - Re-Enable PopUp OSD           | Re-Enables the PopUp OSD
        1210 - Set OSD Visible Duration      | Value in Seconds
     */

    public enum ZoomPlayerCommandCodes
    {
        SendTimelineUpdate = 1100, //0 = off, 1 = on, 2 = resend
        RequestPlayingFileName = 1800,
        SetCurrentPosition = 5000, // s.fff
        CallFunction = 5100, // fnPlay

        ShowOsd = 1200,
        DisableOsd = 1201,
        EnableOsd = 1202,
        SetOsdDuration = 1210
    }

    public enum ZoomPlayerPlaybackStates
    {
        Closed = 0,
        Stopped = 1,
        Paused = 2,
        Playing = 3
    }
}
