using System;
using System.Collections.Generic;
using System.IO;
using Higurashi.IOS.Buriko;
using Higurashi.IOS.Compatibility;
using Higurashi.IOS.Input;
using Higurashi.IOS.Playback;
using Higurashi.IOS.Runtime.Buriko;
using Higurashi.IOS.Runtime.Data;
using Higurashi.IOS.Runtime.Input;
using UnityEngine;

namespace Higurashi.IOS.Runtime
{
    public sealed partial class HigurashiGameRuntime : MonoBehaviour, INovelTraversalDriver
    {
        private const string SettingsKey = "higurashi-ios-settings-v1";
        private const string HelpSeenKey = "higurashi-ios-help-seen-v1";
        private const int SaveFileMagic = 0x31534748; // HGS1
        private const int SaveFileVersion = 1;
        private readonly FastTraversalController _fastTraversal = new FastTraversalController(10f);
        private readonly DataPackImportService _dataPack = new DataPackImportService();
        private readonly CheckpointTimeline<RuntimeCheckpoint> _timeline =
            new CheckpointTimeline<RuntimeCheckpoint>(200);
        private readonly List<PresentationLayer> _orderedLayers = new List<PresentationLayer>();
        private TouchInputBehaviour _touchInput;
        private BurikoRuntime _runtime;
        private UnityBurikoHost _host;
        private HigurashiUserSettings _settings;
        private string _runtimeStatus = "Waiting for game data";
        private int _capturedDialogueSerial;
        private int _suppressInputUntilFrame;
        private bool _settingsVisible;
        private bool _helpVisible;
        private bool _systemMenuVisible;
        private bool _saveLoadVisible;
        private bool _saveMode = true;
        private bool _autoMode;
        private float _nextAutoAdvanceAt;
        private bool _initializationAttempted;
        private Vector2 _historyScroll;
        private GUIStyle _dialogueStyle;
        private GUIStyle _speakerStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _statusStyle;
        private Texture2D _solidWhite;

        private void Awake()
        {
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            _settings = LoadSettings();
            _touchInput = gameObject.AddComponent<TouchInputBehaviour>();
            _touchInput.ActionRaised += HandleInput;
            _touchInput.UiHitTest = UiConsumesPoint;
            _fastTraversal.ModeChanged += mode => _runtimeStatus = "Traversal: " + mode;

            if (DataPackImportService.IsInstalled(Application.persistentDataPath))
            {
                InitializeRuntime();
            }
        }

        private void OnDestroy()
        {
            if (_touchInput != null)
            {
                _touchInput.ActionRaised -= HandleInput;
            }
            if (_host != null)
            {
                _host.MovieFinished -= HandleMovieFinished;
            }
        }

        private void Update()
        {
            if (_runtime == null && !_initializationAttempted &&
                DataPackImportService.IsInstalled(Application.persistentDataPath))
            {
                InitializeRuntime();
            }

            if (_runtime != null && _runtime.BlockReason == BurikoBlockReason.WaitForTime)
            {
                _runtime.AdvanceTime(Mathf.CeilToInt(Time.unscaledDeltaTime * 1000f));
                if (_runtime.BlockReason == BurikoBlockReason.None)
                {
                    DriveRuntime(false);
                }
            }

            _touchInput.FastTraversalActive = _fastTraversal.IsActive;
            _fastTraversal.Tick(Time.unscaledDeltaTime, this);
            if (_autoMode && _runtime != null && _host != null && !IsModalVisible &&
                !_host.TitleVisible && !_host.CreditsVisible && !_host.ChoiceVisible &&
                !_host.HistoryVisible && _runtime.BlockReason == BurikoBlockReason.WaitForInput &&
                Time.unscaledTime >= _nextAutoAdvanceAt)
            {
                StepForward();
                ScheduleNextAutoAdvance();
            }
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                _fastTraversal.Stop();
                SaveSettings();
            }
        }

        private void InitializeRuntime()
        {
            _initializationAttempted = true;
            try
            {
                var installRoot = DataPackImportService.GetInstallPath(Application.persistentDataPath);
                var streamingAssets = Path.Combine(installRoot, "StreamingAssets");
                var repository = new DirectoryBurikoScriptRepository(
                    Path.Combine(streamingAssets, "CompiledChineseScripts"),
                    Path.Combine(streamingAssets, "CompiledUpdateScripts"),
                    Path.Combine(streamingAssets, "CompiledScripts"));

                _host = gameObject.AddComponent<UnityBurikoHost>();
                _host.Initialize(installRoot, _settings);
                _host.MovieFinished += HandleMovieFinished;
                _runtime = new BurikoRuntime(repository, _host);
                _runtime.Start("init");
                DriveRuntime(true);

                if (_runtime.BlockReason == BurikoBlockReason.Faulted)
                {
                    _runtimeStatus = FormatRuntimeFault();
                    return;
                }

                _host.ApplySettings(_runtime.Memory);
                _capturedDialogueSerial = _host.DialogueSerial;
                _runtimeStatus = "Ready";
            }
            catch (Exception exception)
            {
                _runtimeStatus = "Runtime initialization failed: " + exception;
                _runtime = null;
            }
        }

        private void HandleInput(NovelInputAction action)
        {
            if (Time.frameCount <= _suppressInputUntilFrame || _runtime == null)
            {
                return;
            }

            if (_host.MovieVisible)
            {
                if (action == NovelInputAction.Advance)
                {
                    _host.CompleteMovie();
                }
                return;
            }

            if (_host.CreditsVisible)
            {
                if (action == NovelInputAction.Advance && _host.CompleteCredits())
                {
                    _runtime.ResumeInput();
                    _suppressInputUntilFrame = Time.frameCount + 2;
                    DriveRuntime(false);
                }
                return;
            }

            if (_host.TitleVisible || IsModalVisible || _host.ChoiceVisible)
            {
                return;
            }

            switch (action)
            {
                case NovelInputAction.StartFastForward:
                    _host.HistoryVisible = false;
                    _fastTraversal.StartForward();
                    break;
                case NovelInputAction.StartFastRewind:
                    _host.HistoryVisible = false;
                    _fastTraversal.StartRewind();
                    break;
                case NovelInputAction.StopFastTraversal:
                    _fastTraversal.Stop();
                    break;
                case NovelInputAction.Advance:
                    _autoMode = false;
                    if (_host.HistoryVisible)
                    {
                        _host.HistoryVisible = false;
                    }
                    else
                    {
                        StepForward();
                    }
                    break;
                case NovelInputAction.OpenHistory:
                    _fastTraversal.Stop();
                    _host.HistoryVisible = true;
                    break;
                case NovelInputAction.ToggleTextWindow:
                    _fastTraversal.Stop();
                    _host.ToggleWindow();
                    break;
            }
        }

        public bool StepForward()
        {
            if (_runtime == null || _runtime.BlockReason == BurikoBlockReason.Faulted ||
                _host.TitleVisible || _host.ChoiceVisible)
            {
                return false;
            }

            if (_timeline.TryMoveNext(out var existing))
            {
                RestoreCheckpoint(existing);
                return true;
            }

            if (_runtime.BlockReason == BurikoBlockReason.WaitForInput ||
                _runtime.BlockReason == BurikoBlockReason.Host)
            {
                _runtime.ResumeInput();
            }
            else if (_runtime.BlockReason == BurikoBlockReason.WaitForTime)
            {
                _runtime.AdvanceTime(int.MaxValue);
            }

            var previousSerial = _host.DialogueSerial;
            DriveRuntime(true);
            CaptureDialogueCheckpoint();
            return _host.DialogueSerial != previousSerial ||
                   _runtime.BlockReason == BurikoBlockReason.Completed;
        }

        public bool StepBackward()
        {
            if (_runtime == null || !_timeline.TryMovePrevious(out var checkpoint))
            {
                return false;
            }

            RestoreCheckpoint(checkpoint);
            return true;
        }

        private void DriveRuntime(bool skipTimedWaits)
        {
            for (var boundaryCount = 0; boundaryCount < 1000; boundaryCount++)
            {
                if (_runtime.BlockReason == BurikoBlockReason.None)
                {
                    _runtime.RunUntilBlocked();
                }

                if (_runtime.BlockReason == BurikoBlockReason.WaitForTime && skipTimedWaits)
                {
                    _runtime.AdvanceTime(int.MaxValue);
                    continue;
                }

                CaptureDialogueCheckpoint();
                if (_runtime.BlockReason == BurikoBlockReason.Faulted)
                {
                    _runtimeStatus = FormatRuntimeFault();
                    _fastTraversal.Stop();
                }
                return;
            }

            _runtimeStatus = "Runtime exceeded its interactive-boundary budget.";
            _fastTraversal.Stop();
        }

        private void CaptureDialogueCheckpoint()
        {
            if (_runtime == null || _host == null ||
                _host.DialogueSerial == _capturedDialogueSerial ||
                _runtime.BlockReason == BurikoBlockReason.Faulted)
            {
                return;
            }

            _timeline.Push(new RuntimeCheckpoint(
                _runtime.CaptureSnapshot(),
                _host.CaptureSnapshot()));
            _capturedDialogueSerial = _host.DialogueSerial;
        }

        private void RestoreCheckpoint(RuntimeCheckpoint checkpoint)
        {
            _runtime.RestoreSnapshot(checkpoint.Runtime);
            _host.RestoreSnapshot(checkpoint.Presentation, _runtime.Memory);
            _capturedDialogueSerial = _host.DialogueSerial;
            _runtimeStatus = "Restored " + _runtime.CurrentScriptName + ":" + _runtime.CurrentLine;
        }

        private void StartGame()
        {
            if (_runtime == null || !_host.StartFromTitle(_runtime.Memory))
            {
                return;
            }

            _runtime.ResumeInput();
            _suppressInputUntilFrame = Time.frameCount + 2;
            DriveRuntime(true);
            CaptureDialogueCheckpoint();
            if (PlayerPrefs.GetInt(HelpSeenKey, 0) == 0)
            {
                _helpVisible = true;
            }
        }

        private void SelectChoice(int index)
        {
            if (!_host.Choose(index, _runtime.Memory))
            {
                return;
            }

            _runtime.ResumeInput();
            _suppressInputUntilFrame = Time.frameCount + 2;
            DriveRuntime(true);
            CaptureDialogueCheckpoint();
        }

        private void HandleMovieFinished()
        {
            if (_runtime == null || _runtime.BlockReason != BurikoBlockReason.Host)
            {
                return;
            }

            _runtime.ResumeInput();
            _suppressInputUntilFrame = Time.frameCount + 2;
            DriveRuntime(false);
        }

        private HigurashiUserSettings LoadSettings()
        {
            try
            {
                var json = PlayerPrefs.GetString(SettingsKey, string.Empty);
                if (!string.IsNullOrEmpty(json))
                {
                    var loaded = JsonUtility.FromJson<HigurashiUserSettings>(json) ?? new HigurashiUserSettings();
                    if (loaded.textScale < 50)
                    {
                        loaded.textScale = 100;
                    }
                    return loaded;
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Unable to load settings: " + exception.Message);
            }
            return new HigurashiUserSettings();
        }

        private void SaveSettings()
        {
            try
            {
                PlayerPrefs.SetString(SettingsKey, JsonUtility.ToJson(_settings));
                PlayerPrefs.Save();
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Unable to save settings: " + exception.Message);
            }
        }

        private void ToggleAutoMode()
        {
            _autoMode = !_autoMode;
            _fastTraversal.Stop();
            if (_autoMode)
            {
                ScheduleNextAutoAdvance();
                ShowToast("自动播放：开");
            }
            else
            {
                ShowToast("自动播放：关");
            }
        }

        private void ScheduleNextAutoAdvance()
        {
            var normalized = Mathf.Clamp01(_settings.autoSpeed / 100f);
            _nextAutoAdvanceAt = Time.unscaledTime + Mathf.Lerp(6f, 1.2f, normalized);
        }

        private bool CanSaveGame()
        {
            return _runtime != null && _host != null && !_host.TitleVisible &&
                   !_host.CreditsVisible && !_host.MovieVisible && !_host.ChoiceVisible &&
                   _runtime.BlockReason != BurikoBlockReason.Faulted &&
                   _runtime.BlockReason != BurikoBlockReason.Completed;
        }

        private void SaveGame(int slot)
        {
            if (!CanSaveGame())
            {
                ShowToast("当前画面不能保存");
                return;
            }

            try
            {
                var directory = Path.Combine(Application.persistentDataPath, "Saves");
                Directory.CreateDirectory(directory);
                var path = SaveSlotPath(slot);
                var temporaryPath = path + ".tmp";
                using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
                {
                    writer.Write(SaveFileMagic);
                    writer.Write(SaveFileVersion);
                    writer.Write(DateTime.UtcNow.Ticks);
                    writer.Write(_runtime.CurrentScriptName ?? string.Empty);
                    writer.Write(_runtime.CurrentLine);
                    writer.Write(SaveSummary());
                    _runtime.WritePersistentState(stream);
                    _host.WritePersistentState(stream);
                    stream.Flush();
                }
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
                File.Move(temporaryPath, path);
                ShowToast(slot == 0 ? "快速保存完成" : "已保存到槽位 " + slot);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Unable to save game: " + exception);
                ShowToast("保存失败");
            }
        }

        private void LoadGame(int slot)
        {
            var path = SaveSlotPath(slot);
            if (!File.Exists(path))
            {
                ShowToast("该槽位没有存档");
                return;
            }

            try
            {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, true))
                {
                    ReadSaveHeader(reader);
                    _runtime.ReadPersistentState(stream);
                    _host.ReadPersistentState(stream, _runtime.Memory);
                }
                _timeline.Clear();
                _capturedDialogueSerial = _host.DialogueSerial;
                _timeline.Push(new RuntimeCheckpoint(
                    _runtime.CaptureSnapshot(),
                    _host.CaptureSnapshot()));
                _fastTraversal.Stop();
                CloseAllModals();
                _suppressInputUntilFrame = Time.frameCount + 2;
                ShowToast(slot == 0 ? "快速读取完成" : "已读取槽位 " + slot);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Unable to load game: " + exception);
                ShowToast("存档损坏或版本不兼容");
            }
        }

        private SaveSlotInfo ReadSaveSlotInfo(int slot)
        {
            var path = SaveSlotPath(slot);
            if (!File.Exists(path))
            {
                return null;
            }
            try
            {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, false))
                {
                    return ReadSaveHeader(reader);
                }
            }
            catch
            {
                return new SaveSlotInfo(slot, DateTime.MinValue, string.Empty, 0, "存档不可读取");
            }
        }

        private static SaveSlotInfo ReadSaveHeader(BinaryReader reader)
        {
            if (reader.ReadInt32() != SaveFileMagic || reader.ReadInt32() != SaveFileVersion)
            {
                throw new InvalidDataException("Unsupported Higurashi iOS save file.");
            }
            return new SaveSlotInfo(
                -1,
                new DateTime(reader.ReadInt64(), DateTimeKind.Utc).ToLocalTime(),
                reader.ReadString(),
                reader.ReadInt32(),
                reader.ReadString());
        }

        private int FindLatestSaveSlot()
        {
            var result = -1;
            var newest = DateTime.MinValue;
            for (var slot = 0; slot <= 10; slot++)
            {
                var info = ReadSaveSlotInfo(slot);
                if (info != null && info.Timestamp > newest)
                {
                    newest = info.Timestamp;
                    result = slot;
                }
            }
            return result;
        }

        private string SaveSlotPath(int slot)
        {
            return Path.Combine(Application.persistentDataPath, "Saves", "slot-" + slot + ".hgs");
        }

        private string SaveSummary()
        {
            var text = string.IsNullOrEmpty(_host.Speaker)
                ? _host.Dialogue
                : _host.Speaker + "　" + _host.Dialogue;
            text = (text ?? string.Empty).Replace('\n', ' ').Replace('\r', ' ').Trim();
            return text.Length <= 80 ? text : text.Substring(0, 80) + "…";
        }

        private sealed class SaveSlotInfo
        {
            public SaveSlotInfo(int slot, DateTime timestamp, string script, int line, string summary)
            {
                Slot = slot;
                Timestamp = timestamp;
                Script = script;
                Line = line;
                Summary = summary;
            }

            public int Slot { get; }
            public DateTime Timestamp { get; }
            public string Script { get; }
            public int Line { get; }
            public string Summary { get; }
        }

        private string FormatRuntimeFault()
        {
            return "Buriko runtime fault at " + _runtime.CurrentScriptName + ":" +
                   _runtime.CurrentLine + "\n" + _runtime.LastError;
        }

        private sealed class RuntimeCheckpoint
        {
            public RuntimeCheckpoint(BurikoRuntimeSnapshot runtime, UnityBurikoHostSnapshot presentation)
            {
                Runtime = runtime;
                Presentation = presentation;
            }

            public BurikoRuntimeSnapshot Runtime { get; }
            public UnityBurikoHostSnapshot Presentation { get; }
        }
    }
}
