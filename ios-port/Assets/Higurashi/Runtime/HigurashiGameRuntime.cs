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

            if (_host.TitleVisible || _settingsVisible || _host.ChoiceVisible)
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
                    return JsonUtility.FromJson<HigurashiUserSettings>(json) ?? new HigurashiUserSettings();
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
