using System.IO;
using ExamHelper.Core.Audio;
using ExamHelper.Core.Common;
using NAudio.CoreAudioApi;
using NAudio.Wave;
namespace ExamHelper.Windows.Audio;
public sealed record MicrophoneDevice(string Id, string Name);
public sealed class MicrophoneRecorder(string temporaryDirectory) : IMicrophoneRecorder, IAsyncDisposable
{
    private readonly string _root = Path.GetFullPath(temporaryDirectory);
    private readonly SemaphoreSlim _gate = new(1);
    private readonly object _audioSync = new();
    private WasapiCapture? _capture;
    private MMDevice? _device;
    private WaveFileWriter? _writer;
    private TaskCompletionSource? _stopped;
    private string? _rawPath, _encodedPath;
    private Guid _operationId;
    private volatile float _peakLevel;
    public float PeakLevel { get => _peakLevel; private set => _peakLevel = value; }
    public static IReadOnlyList<MicrophoneDevice> GetDevices()
    {
        using var devices = new MMDeviceEnumerator();
        var result = new List<MicrophoneDevice>();
        foreach (var device in devices.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active))
        {
            using (device) result.Add(new(device.ID, device.FriendlyName));
        }
        return result;
    }
    public async Task StartAsync(string deviceId, CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_capture is not null) throw new InvalidOperationException("麦克风已经打开。");
            Directory.CreateDirectory(_root);
            _operationId = Guid.NewGuid(); var name = _operationId.ToString("N");
            _rawPath = Path.Combine(_root, name + ".raw.wav"); _encodedPath = Path.Combine(_root, name + ".wav");
            using var devices = new MMDeviceEnumerator();
            _device = string.IsNullOrEmpty(deviceId) ? devices.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications) : devices.GetDevice(deviceId);
            if (_device.DataFlow != DataFlow.Capture) throw new ServiceFailure(FailureCategory.InvalidPayload, "请选择麦克风采集设备。");
            _capture = new WasapiCapture(_device);
            _writer = new WaveFileWriter(_rawPath, _capture.WaveFormat);
            _stopped = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _capture.DataAvailable += OnData;
            _capture.RecordingStopped += OnStopped;
            ct.ThrowIfCancellationRequested(); _capture.StartRecording();
        }
        catch (Exception error)
        {
            Cleanup(true);
            if (error is OperationCanceledException or ServiceFailure) throw;
            throw new ServiceFailure(FailureCategory.Permission, "无法打开麦克风，请检查设备与 Windows 麦克风权限。", inner: error);
        }
        finally { _gate.Release(); }
    }
    private void OnData(object? sender, WaveInEventArgs args)
    {
        lock (_audioSync)
        {
            _writer?.Write(args.Buffer, 0, args.BytesRecorded);
            PeakLevel = _capture?.WaveFormat is { } format ? AudioPeakMeter.Measure(args.Buffer.AsSpan(0,args.BytesRecorded),format) : 0;
        }
    }
    private void OnStopped(object? sender, StoppedEventArgs args)
    {
        lock (_audioSync) { _writer?.Dispose(); _writer = null; PeakLevel = 0; }
        if (args.Exception is not null) _stopped?.TrySetException(new ServiceFailure(FailureCategory.Transport, "麦克风录音已中断，请检查设备。", inner: args.Exception));
        else _stopped?.TrySetResult();
    }
    public async Task<RecordedAudio> StopAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_capture is null || _stopped is null || _rawPath is null || _encodedPath is null)
                throw new InvalidOperationException("没有正在采集的麦克风。");
            if (!_stopped.Task.IsCompleted) _capture.StopRecording();
            await _stopped.Task.WaitAsync(TimeSpan.FromSeconds(10), ct).ConfigureAwait(false);
            var raw = _rawPath; var encoded = _encodedPath;
            await Task.Run(() => AudioEncoder.EncodeToPcm16(raw, encoded), ct).ConfigureAwait(false);
            using var reader = new WaveFileReader(encoded);
            var result = new RecordedAudio(_operationId, encoded, reader.TotalTime);
            reader.Dispose(); Cleanup(false);
            return result;
        }
        catch { Cleanup(true); throw; }
        finally { _gate.Release(); }
    }
    public async Task CancelAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_capture is not null && _stopped is not null && !_stopped.Task.IsCompleted)
            {
                _capture.StopRecording();
                try { await _stopped.Task.WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false); } catch { }
            }
            Cleanup(true);
        }
        finally { _gate.Release(); }
    }
    private void Cleanup(bool deleteEncoded)
    {
        if (_capture is not null)
        {
            _capture.DataAvailable -= OnData; _capture.RecordingStopped -= OnStopped; _capture.Dispose(); _capture = null;
        }
        lock (_audioSync) { _writer?.Dispose(); _writer = null; }
        _device?.Dispose(); _device = null; _stopped = null;
        if (_rawPath is not null && File.Exists(_rawPath)) File.Delete(_rawPath);
        _rawPath = null;
        if (deleteEncoded && _encodedPath is not null && File.Exists(_encodedPath)) File.Delete(_encodedPath);
        if (deleteEncoded) _encodedPath = null;
        PeakLevel = 0;
    }
    public async ValueTask DisposeAsync() => await CancelAsync().ConfigureAwait(false);
}