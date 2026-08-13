using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Higurashi.IOS.Buriko;
using Higurashi.IOS.Compatibility;
using Higurashi.IOS.Input;
using Higurashi.IOS.Playback;
using Higurashi.IOS.Persistence;
using Higurashi.IOS.Runtime.Buriko;
using Higurashi.IOS.Runtime.Data;
using Higurashi.IOS.Runtime.Diagnostics;
using Higurashi.IOS.Runtime.Input;
using UnityEngine;

namespace Higurashi.IOS.Runtime
{
    public sealed partial class HigurashiGameRuntime : MonoBehaviour, INovelTraversalDriver
    {
        private const string SettingsKey = "higurashi-ios-settings-v1";
        private const string HelpSeenKey = "higurashi-ios-help-seen-v1";
        private const string FragmentTutorialSeenKey = "higurashi-ios-ep08-fragment-tutorial-seen-v1";
        private const string OpeningPreferenceKey = "higurashi-ios-opening-preference-v1";
        private const string TipsMenuUnlockedKeyPrefix = "higurashi-ios-tips-menu-unlocked-ep";
        private const string TipsUnlockedChapterKeyPrefix = "higurashi-ios-tips-unlocked-chapter-ep";
        private const string ChapterJumpUnlockedKeyPrefix = "higurashi-ios-chapter-jump-unlocked-ep";
        private const string BonusContentUnlockedKeyPrefix = "higurashi-ios-bonus-unlocked-ep";
        private const int SaveFileMagic = 0x31534748; // HGS1
        private const int SaveFileVersion = 1;
        private const int SaveTimelineMagic = 0x314C5448; // HTL1
        private const int PersistedTimelineLimit = 200;
        // This is a read-only summary slot in the save/load UI. Every successful
        // save path mirrors its current state here, so Continue and quick load
        // always have one unambiguous latest save to use.
        private const int LatestSaveSlot = 1;
        private readonly FastTraversalController _fastTraversal = new FastTraversalController(10f);
        private readonly DataPackImportService _dataPack = new DataPackImportService();
        private readonly CheckpointTimeline<RuntimeCheckpoint> _timeline =
            new CheckpointTimeline<RuntimeCheckpoint>(200, preserveFirst: true);
        private readonly List<PresentationLayer> _orderedLayers = new List<PresentationLayer>();
        private TouchInputBehaviour _touchInput;
        private IOSDataPackFilePicker _dataPackFilePicker;
        private BurikoRuntime _runtime;
        private UnityBurikoHost _host;
        private HigurashiUserSettings _settings;
        private string _runtimeStatus = "Waiting for game data";
        private int _capturedDialogueSerial;
        private int _dialoguesSinceAutoSave;
        private float _lastAutoSaveAt;
        private float _nextSaveProgressScanAt;
        private int _suppressInputUntilFrame;
        private bool _settingsVisible;
        private bool _helpVisible;
        private bool _systemMenuVisible;
        private bool _saveLoadVisible;
        private bool _autoMode;
        private bool _autoWasVoicePlaying;
        private bool _showHelpWhenGameplayStarts;
        private float _nextAutoAdvanceAt;
        private bool _initializationAttempted;
        private RuntimeCheckpoint _titleCheckpoint;
        private RuntimeCheckpoint _tipsLibraryReturnCheckpoint;
        private RuntimeCheckpoint _bonusContentReturnCheckpoint;
        private RuntimeCheckpoint _storyChoiceCheckpoint;
        private int _tipsLibraryReturnCallDepth;
        private int _bonusContentReturnCallDepth;
        private int _timelineChapterNumber = -1;
        private string _storyChoiceScript = string.Empty;
        private int _storyChoiceLine = -1;
        private bool _badEndingPlaybackActive;
        private bool _badEndingDecisionVisible;
        private Vector2 _historyScroll;
        private bool _historyAutoScrollPending;
        private GUIStyle _dialogueStyle;
        private GUIStyle _speakerStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _statusStyle;
        private Texture2D _solidWhite;

        private string TipsMenuUnlockedKey => TipsMenuUnlockedKeyPrefix +
            HigurashiActiveChapter.Profile.EpisodeNumber.ToString("00");

        private string TipsMenuUnlockMarkerPath => Path.Combine(Application.persistentDataPath,
            "tips-menu-unlocked-ep" + HigurashiActiveChapter.Profile.EpisodeNumber.ToString("00") + ".flag");

        private string BonusContentUnlockedKey => BonusContentUnlockedKeyPrefix +
            HigurashiActiveChapter.Profile.EpisodeNumber.ToString("00");

        private string BonusContentUnlockMarkerPath => Path.Combine(Application.persistentDataPath,
            "bonus-unlocked-ep" + HigurashiActiveChapter.Profile.EpisodeNumber.ToString("00") + ".flag");

        private string BonusContentName =>
            HigurashiActiveChapter.Profile.EpisodeNumber <= 4 ? "慰劳茶会" : "工作室闲谈";

        private string BonusContentScript =>
            HigurashiActiveChapter.Profile.EpisodeNumber <= 4
                ? "omake_" + HigurashiActiveChapter.Profile.EpisodeNumber.ToString("00")
                : "staffroom";

        private bool IsBonusContentUnlocked
        {
            get
            {
                if (File.Exists(BonusContentUnlockMarkerPath))
                {
                    return true;
                }

                // LiveContainer may keep PlayerPrefs after Documents has been
                // cleared. Only migrate the old preference when a real save is
                // also present, matching the TIPS unlock migration policy.
                var saveDirectory = Path.Combine(Application.persistentDataPath, "Saves");
                if (PlayerPrefs.GetInt(BonusContentUnlockedKey, 0) != 0 &&
                    Directory.Exists(saveDirectory) &&
                    Directory.GetFiles(saveDirectory, "slot-*.hgs").Length > 0)
                {
                    try
                    {
                        File.WriteAllText(BonusContentUnlockMarkerPath, "unlocked");
                    }
                    catch
                    {
                        // The preference remains usable for this session.
                    }
                    return true;
                }
                return false;
            }
        }

        private bool IsTipsMenuUnlocked
        {
            get
            {
                if (File.Exists(TipsMenuUnlockMarkerPath))
                {
                    return true;
                }

                // LiveContainer can retain PlayerPrefs even when an app is
                // reinstalled. Only migrate the old preference when this app
                // also has a real save file; a clean installation stays locked.
                var saveDirectory = Path.Combine(Application.persistentDataPath, "Saves");
                if (PlayerPrefs.GetInt(TipsMenuUnlockedKey, 0) != 0 &&
                    Directory.Exists(saveDirectory) &&
                    Directory.GetFiles(saveDirectory, "slot-*.hgs").Length > 0)
                {
                    UnlockTipsMenu();
                    return true;
                }
                return false;
            }
        }

        private string ChapterJumpUnlockedKey => ChapterJumpUnlockedKeyPrefix +
            HigurashiActiveChapter.Profile.EpisodeNumber.ToString("00");

        private string TipsUnlockedChapterKey => TipsUnlockedChapterKeyPrefix +
            HigurashiActiveChapter.Profile.EpisodeNumber.ToString("00");

        private void Awake()
        {
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            HigurashiDiagnosticLog.Initialize(Application.persistentDataPath);
            _settings = LoadSettings();
            _touchInput = gameObject.AddComponent<TouchInputBehaviour>();
            _dataPackFilePicker = gameObject.AddComponent<IOSDataPackFilePicker>();
            _touchInput.ActionRaised += HandleInput;
            _touchInput.UiHitTest = UiConsumesPoint;
            _fastTraversal.ModeChanged += mode => _runtimeStatus = "Traversal: " + mode;
            HigurashiDiagnosticLog.Info("App", BuildDiagnosticState("Awake"));

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
            SaveOpeningPreference();
            HigurashiDiagnosticLog.Shutdown();
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

            if (_runtime != null && _host != null &&
                _runtime.BlockReason == BurikoBlockReason.Host &&
                _host.ConsumeCompletedBlockingAnimation())
            {
                _runtime.ResumeInput();
                DriveRuntime(false);
            }

            _touchInput.FastTraversalActive = _fastTraversal.IsActive;
            if (_host != null && _host.MovieVisible && _fastTraversal.IsActive)
            {
                _fastTraversal.Stop();
                _autoMode = false;
                _touchInput.FastTraversalActive = false;
            }
            UpdateHistoryTouchScroll();
            _fastTraversal.Tick(Time.unscaledDeltaTime, this);
            if (_showHelpWhenGameplayStarts && _host != null && _host.GameplayUiVisible &&
                _host.SavingEnabled && _host.InterfaceEnabled &&
                _runtime != null && _runtime.BlockReason == BurikoBlockReason.WaitForInput &&
                !IsModalVisible)
            {
                _showHelpWhenGameplayStarts = false;
                _helpVisible = true;
            }
            if (_autoMode && _runtime != null && _host != null && !IsModalVisible &&
                !_host.TitleVisible && !_host.CreditsVisible && !_host.ChoiceVisible &&
                !_host.HistoryVisible && _runtime.BlockReason == BurikoBlockReason.WaitForInput &&
                _host.IsDialogueRevealComplete)
            {
                if (_host.IsVoicePlaying)
                {
                    _autoWasVoicePlaying = true;
                }
                else
                {
                    if (_autoWasVoicePlaying)
                    {
                        _autoWasVoicePlaying = false;
                        _nextAutoAdvanceAt = Mathf.Max(_nextAutoAdvanceAt, Time.unscaledTime + 0.7f);
                    }
                    if (Time.unscaledTime >= _nextAutoAdvanceAt)
                    {
                        StepForward();
                        ScheduleNextAutoAdvance();
                    }
                }
            }

            UpdateChapterJumpUnlockProgress();
            UpdateBonusContentUnlockProgress();
            CaptureStoryChoiceCheckpointIfNeeded();
            UpdateBadEndingDecision();
            if (_host != null && _host.TitleVisible &&
                Time.unscaledTime >= _nextSaveProgressScanAt)
            {
                _nextSaveProgressScanAt = Time.unscaledTime + 2f;
                RefreshUnlockProgressFromSaves();
            }
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                _fastTraversal.Stop();
                MaybeAutoSave(true);
                SaveSettings();
                SaveOpeningPreference();
            }
        }

        private void BeginDataPackSelection()
        {
            if (_dataPack.IsRunning ||
                (_dataPackFilePicker != null && _dataPackFilePicker.IsPresenting))
            {
                return;
            }

            _dataPack.SetWaitingStatus("请在 iOS“文件”中选择本章数据包…");
            _dataPackFilePicker.Pick(
                DataPackImportService.GetIncomingPackPath(Application.persistentDataPath),
                selectedPath =>
                {
                    _dataPack.SetWaitingStatus("已选择数据包，准备校验…");
                    _dataPack.BeginSelectedImport(Application.persistentDataPath, selectedPath);
                },
                message => _dataPack.SetWaitingStatus(message));
        }

        private void InitializeRuntime()
        {
            _initializationAttempted = true;
            try
            {
                BurikoOperationCatalog.ConfigureForEpisode(HigurashiActiveChapter.Profile.EpisodeNumber);
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
                var openingPreference = PlayerPrefs.GetInt(OpeningPreferenceKey, 0);
                if (openingPreference > 0)
                {
                    _runtime.Memory.SetGlobalFlag("GVideoOpening", openingPreference);
                }
                DriveRuntime(false);

                if (_runtime.BlockReason == BurikoBlockReason.Faulted)
                {
                    _runtimeStatus = FormatRuntimeFault();
                    return;
                }

                _host.ApplySettings(_runtime.Memory);
                _capturedDialogueSerial = _host.DialogueSerial;
                _lastAutoSaveAt = Time.unscaledTime;
                RefreshUnlockProgressFromSaves();
                _runtimeStatus = "Ready";
                HigurashiDiagnosticLog.Info("Runtime", BuildDiagnosticState("Initialized"));
            }
            catch (Exception exception)
            {
                _runtimeStatus = "Runtime initialization failed: " + exception;
                HigurashiDiagnosticLog.Warning("Runtime", _runtimeStatus);
                _runtime = null;
            }
        }

        private void HandleInput(NovelInputAction action)
        {
            if (Time.frameCount <= _suppressInputUntilFrame || _runtime == null)
            {
                return;
            }
            HigurashiDiagnosticLog.Info("Input", action + " " + RuntimeLocation());

            if (_host.MovieVisible)
            {
                if (action == NovelInputAction.Advance ||
                    action == NovelInputAction.StopFastTraversal)
                {
                    _fastTraversal.Stop();
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

            if (_host.TitleVisible || _host.ChapterPreviewVisible ||
                _host.FragmentChapterVisible || _host.FragmentListVisible ||
                IsModalVisible || _host.ChoiceVisible)
            {
                return;
            }

            switch (action)
            {
                case NovelInputAction.StartFastForward:
                    _autoMode = false;
                    _autoWasVoicePlaying = false;
                    _host.StopVoices();
                    _host.HistoryVisible = false;
                    _fastTraversal.StartForward();
                    break;
                case NovelInputAction.StartFastRewind:
                    _autoMode = false;
                    _autoWasVoicePlaying = false;
                    _host.StopVoices();
                    _host.HistoryVisible = false;
                    _fastTraversal.StartRewind();
                    break;
                case NovelInputAction.StopFastTraversal:
                    _fastTraversal.Stop();
                    if (_runtime.BlockReason == BurikoBlockReason.WaitForInput)
                    {
                        _host.ReplayRestoredVoice(_runtime.Memory);
                    }
                    break;
                case NovelInputAction.Advance:
                    _autoMode = false;
                    if (_host.HistoryVisible)
                    {
                        _host.HistoryVisible = false;
                    }
                    else
                    {
                        if (!_host.IsDialogueRevealComplete &&
                            _runtime.BlockReason == BurikoBlockReason.WaitForInput)
                        {
                            _host.CompleteDialogueReveal();
                        }
                        else
                        {
                            StepForward();
                        }
                    }
                    break;
                case NovelInputAction.PreviousTextBox:
                    _fastTraversal.Stop();
                    StepBackwardToPreviousTextBox();
                    break;
                case NovelInputAction.OpenHistory:
                    _fastTraversal.Stop();
                    _host.HistoryVisible = true;
                    _historyAutoScrollPending = true;
                    break;
                case NovelInputAction.ToggleTextWindow:
                    _fastTraversal.Stop();
                    _host.ToggleWindow();
                    break;
            }
        }

        private void UpdateHistoryTouchScroll()
        {
            if (_host == null || !_host.HistoryVisible || UnityEngine.Input.touchCount != 1)
            {
                return;
            }

            var touch = UnityEngine.Input.GetTouch(0);
            if (touch.phase == UnityEngine.TouchPhase.Moved)
            {
                _historyScroll.y = Mathf.Max(0f, _historyScroll.y + touch.deltaPosition.y);
            }
        }

        public bool StepForward()
        {
            if (_runtime == null || _runtime.BlockReason == BurikoBlockReason.Faulted ||
                _host.TitleVisible || _host.ChoiceVisible)
            {
                return false;
            }

            if (_host.MovieVisible)
            {
                _fastTraversal.Stop();
                _autoMode = false;
                return false;
            }

            if (_timeline.TryMoveNext(out var existing))
            {
                var previousPresentation = _host.CaptureSnapshot();
                _host.StopVoices();
                RestoreCheckpoint(existing);
                _host.ReplayRestoredCheckpointAnimations(previousPresentation, _runtime.Memory);
                if (!_fastTraversal.IsActive)
                {
                    _host.ReplayRestoredVoice(_runtime.Memory);
                }
                return true;
            }

            if (!_fastTraversal.IsActive &&
                _runtime.BlockReason == BurikoBlockReason.WaitForInput &&
                !_host.IsDialogueRevealComplete)
            {
                _host.CompleteDialogueReveal();
                return true;
            }

            _host.StopVoices();
            var skippedTimedWait = _runtime.BlockReason == BurikoBlockReason.WaitForTime;

            if (_runtime.BlockReason == BurikoBlockReason.Host && _host.SkipBlockingAnimation())
            {
                _runtime.ResumeInput();
                DriveRuntime(_fastTraversal.IsActive);
                CaptureDialogueCheckpoint();
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
            DriveRuntime(_fastTraversal.IsActive);
            CaptureDialogueCheckpoint();
            var advanced = _host.DialogueSerial != previousSerial ||
                           _runtime.BlockReason == BurikoBlockReason.Completed || skippedTimedWait;
            if (!advanced && _fastTraversal.IsActive &&
                _runtime.BlockReason != BurikoBlockReason.Completed &&
                _runtime.BlockReason != BurikoBlockReason.Faulted &&
                !_host.TitleVisible && !_host.ChoiceVisible)
            {
                // Voice-bearing lines can contain an intermediate non-dialogue boundary.
                // Keep traversal alive and retry on the next rendered frame instead of
                // treating that transient boundary as the end of fast traversal.
                return true;
            }
            return advanced;
        }

        public bool StepBackward()
        {
            if (!TryMovePreviousInCurrentChapter(out var checkpoint))
            {
                return false;
            }

            _host.StopVoices();
            RestoreCheckpoint(checkpoint);
            return true;
        }

        private bool StepBackwardToPreviousTextBox()
        {
            if (!TryMovePreviousInCurrentChapter(out var checkpoint))
            {
                return false;
            }

            // WaitForInput continuation checkpoints are the first half of a
            // text box which will later receive another line. A one-finger
            // rewind targets the previous completed text box instead.
            while (checkpoint.AppendNext &&
                   TryMovePreviousInCurrentChapter(out var previous))
            {
                checkpoint = previous;
            }

            _host.StopVoices();
            RestoreCheckpoint(checkpoint);
            _host.CompleteDialogueReveal();
            _host.ReplayRestoredVoice(_runtime.Memory);
            return true;
        }

        private bool TryMovePreviousInCurrentChapter(out RuntimeCheckpoint checkpoint)
        {
            checkpoint = null;
            if (_runtime == null || !_timeline.TryMovePrevious(out var previous))
            {
                return false;
            }

            var currentChapter = Math.Max(0, _runtime.Memory.GetLocalFlag("ChapterNumber"));
            if (previous.ChapterNumber != currentChapter)
            {
                // Put the cursor back where it was. The presentation/runtime was
                // never restored, so this is only a timeline cursor correction.
                _timeline.TryMoveNext(out _);
                return false;
            }

            checkpoint = previous;
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

                if (_tipsLibraryReturnCheckpoint != null &&
                    (_runtime.CallDepth <= _tipsLibraryReturnCallDepth ||
                     _host.TitleVisible ||
                     _runtime.BlockReason == BurikoBlockReason.Completed))
                {
                    RestoreCheckpoint(_tipsLibraryReturnCheckpoint);
                    _tipsLibraryReturnCheckpoint = null;
                    return;
                }

                if (_bonusContentReturnCheckpoint != null &&
                    (_runtime.CallDepth <= _bonusContentReturnCallDepth ||
                     _host.TitleVisible ||
                     _runtime.BlockReason == BurikoBlockReason.Completed))
                {
                    RestoreCheckpoint(_bonusContentReturnCheckpoint);
                    _bonusContentReturnCheckpoint = null;
                    CloseAllModals();
                    _extrasVisible = true;
                    SuppressInput();
                    HigurashiDiagnosticLog.Info("Bonus",
                        "Returned to extras after " + BonusContentScript);
                    return;
                }

                if (_runtime.BlockReason == BurikoBlockReason.WaitForTime && skipTimedWaits)
                {
                    _runtime.AdvanceTime(int.MaxValue);
                    continue;
                }

                CaptureDialogueCheckpoint();
                CaptureTitleCheckpointIfNeeded();
                CaptureStoryChoiceCheckpointIfNeeded();
                UpdateBadEndingDecision();
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

            var chapter = Math.Max(0, _runtime.Memory.GetLocalFlag("ChapterNumber"));
            if (_timelineChapterNumber != chapter)
            {
                // A chapter boundary is a hard rewind floor. The first dialogue
                // captured below becomes the earliest reachable checkpoint.
                _timeline.Clear();
                _timelineChapterNumber = chapter;
                HigurashiDiagnosticLog.Info("Timeline",
                    "Chapter boundary reset; chapter=" + chapter + " " + RuntimeLocation());
            }
            _timeline.Push(CaptureCurrentCheckpoint());
            _capturedDialogueSerial = _host.DialogueSerial;
            _dialoguesSinceAutoSave++;
            MaybeAutoSave(false);
        }

        private void CaptureTitleCheckpointIfNeeded()
        {
            if (_runtime == null || _host == null || !_host.TitleVisible ||
                _runtime.BlockReason != BurikoBlockReason.Host)
            {
                return;
            }
            _titleCheckpoint = CaptureCurrentCheckpoint();
        }

        private void RestoreCheckpoint(RuntimeCheckpoint checkpoint)
        {
            using (var runtimeState = new MemoryStream(checkpoint.RuntimeState, false))
            using (var presentationState = new MemoryStream(checkpoint.PresentationState, false))
            {
                _host.StopAllAudio();
                _runtime.ReadPersistentState(runtimeState);
                _host.ApplySettings(_runtime.Memory);
                _host.ReadPersistentState(presentationState, _runtime.Memory);
            }
            _capturedDialogueSerial = _host.DialogueSerial;
            _timelineChapterNumber = checkpoint.ChapterNumber;
            _runtimeStatus = "Restored " + _runtime.CurrentScriptName + ":" + _runtime.CurrentLine;
            HigurashiDiagnosticLog.Info("Timeline",
                "Restored checkpoint chapter=" + checkpoint.ChapterNumber +
                " script=" + checkpoint.ScriptName + " line=" + checkpoint.LineNumber);
        }

        private RuntimeCheckpoint CaptureCurrentCheckpoint()
        {
            byte[] runtimeState;
            byte[] presentationState;
            using (var stream = new MemoryStream())
            {
                _runtime.WritePersistentState(stream);
                runtimeState = stream.ToArray();
            }
            using (var stream = new MemoryStream())
            {
                _host.WritePersistentState(stream);
                presentationState = stream.ToArray();
            }

            return new RuntimeCheckpoint(
                Math.Max(0, _runtime.Memory.GetLocalFlag("ChapterNumber")),
                _host.AppendNext,
                _runtime.CurrentScriptName,
                _runtime.CurrentLine,
                runtimeState,
                presentationState);
        }

        private void CaptureStoryChoiceCheckpointIfNeeded()
        {
            if (_runtime == null || _host == null || !_host.ChoiceVisible ||
                _host.IsOpeningChoice || !_host.GameplayUiVisible ||
                !_host.SavingEnabled || !_host.InterfaceEnabled ||
                _runtime.BlockReason != BurikoBlockReason.Choice)
            {
                return;
            }

            if (_storyChoiceCheckpoint != null &&
                string.Equals(_storyChoiceScript, _runtime.CurrentScriptName,
                    StringComparison.OrdinalIgnoreCase) &&
                _storyChoiceLine == _runtime.CurrentLine)
            {
                return;
            }

            _storyChoiceCheckpoint = CaptureCurrentCheckpoint();
            _storyChoiceScript = _runtime.CurrentScriptName ?? string.Empty;
            _storyChoiceLine = _runtime.CurrentLine;
            _badEndingPlaybackActive = false;
            _badEndingDecisionVisible = false;

            try
            {
                // A branch choice is itself a safe resume point. Persist it in
                // the rotating auto-save group and mirror it to Latest Save so
                // quitting at the decision cannot lose the preceding chapter.
                WriteSaveGame(FindOldestOrEmptySlot(201, 203), null, "story-choice");
                WriteSaveGame(LatestSaveSlot, null, "story-choice-latest");
                RefreshUnlockProgressFromSaves();
                _dialoguesSinceAutoSave = 0;
                _lastAutoSaveAt = Time.unscaledTime;
                ShowToast("已在剧情选项前自动保存");
                HigurashiDiagnosticLog.Info("Save",
                    "Saved story choice checkpoint " + RuntimeLocation());
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Unable to auto-save story choice: " + exception);
                HigurashiDiagnosticLog.Warning("Save",
                    "Story choice save failed " + exception.Message + " " + RuntimeLocation());
                ShowToast("选项前自动保存失败");
            }
        }

        private void UpdateBadEndingDecision()
        {
            if (_runtime == null || _host == null || _badEndingDecisionVisible)
            {
                return;
            }

            var script = _runtime.CurrentScriptName ?? string.Empty;
            if (script.IndexOf("badend", StringComparison.OrdinalIgnoreCase) >= 0 ||
                script.IndexOf("bad_end", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                _badEndingPlaybackActive = true;
            }

            if (!_badEndingPlaybackActive || _storyChoiceCheckpoint == null ||
                !_host.TitleVisible)
            {
                return;
            }

            _badEndingPlaybackActive = false;
            _badEndingDecisionVisible = true;
            _fastTraversal.Stop();
            _autoMode = false;
            _host.StopAllAudio();
            CloseAllModals();
            SuppressInput();
        }

        private void ReturnToStoryChoice()
        {
            if (_storyChoiceCheckpoint == null)
            {
                _badEndingDecisionVisible = false;
                ShowToast("选项前状态不可用，请读取自动存档");
                return;
            }

            _host.StopAllAudio();
            _fastTraversal.Stop();
            _autoMode = false;
            _timeline.Clear();
            RestoreCheckpoint(_storyChoiceCheckpoint);
            _timeline.Push(_storyChoiceCheckpoint);
            _badEndingPlaybackActive = false;
            _badEndingDecisionVisible = false;
            CloseAllModals();
            SuppressInput();
        }

        private void AcceptBadEndingAndReturnToTitle()
        {
            ResetStoryChoiceState();
            CloseAllModals();
            SuppressInput();
        }

        private void ResetStoryChoiceState()
        {
            _storyChoiceCheckpoint = null;
            _storyChoiceScript = string.Empty;
            _storyChoiceLine = -1;
            _badEndingPlaybackActive = false;
            _badEndingDecisionVisible = false;
        }

        private void UnlockTipsMenu()
        {
            try
            {
                File.WriteAllText(TipsMenuUnlockMarkerPath, "unlocked");
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Unable to write TIPS unlock marker: " + exception.Message);
            }
            PlayerPrefs.SetInt(TipsMenuUnlockedKey, 1);
            PlayerPrefs.Save();
        }

        private void RefreshUnlockProgressFromSaves()
        {
            if (_host == null)
            {
                return;
            }

            var sectionCount = _host.GetChapterJumpSections().Count;
            if (sectionCount <= 0)
            {
                return;
            }

            var highestJump = Mathf.Clamp(
                PlayerPrefs.GetInt(ChapterJumpUnlockedKey, 0), 0, sectionCount);
            var highestTips = Mathf.Clamp(
                PlayerPrefs.GetInt(TipsUnlockedChapterKey, 0), 0, sectionCount);
            var saveDirectory = Path.Combine(Application.persistentDataPath, "Saves");
            if (Directory.Exists(saveDirectory))
            {
                var paths = Directory.GetFiles(saveDirectory, "slot-*.hgs");
                for (var i = 0; i < paths.Length; i++)
                {
                    if (!TryReadSavedChapter(paths[i], out var chapter))
                    {
                        continue;
                    }

                    highestJump = Math.Max(highestJump,
                        Mathf.Clamp(chapter + 1, 0, sectionCount));
                    highestTips = Math.Max(highestTips,
                        Mathf.Clamp(chapter, 0, sectionCount));
                }
            }

            var changed = false;
            if (highestJump > PlayerPrefs.GetInt(ChapterJumpUnlockedKey, 0))
            {
                PlayerPrefs.SetInt(ChapterJumpUnlockedKey, highestJump);
                changed = true;
            }
            if (highestTips > PlayerPrefs.GetInt(TipsUnlockedChapterKey, 0))
            {
                PlayerPrefs.SetInt(TipsUnlockedChapterKey, highestTips);
                changed = true;
            }
            if (highestTips > 0 && !IsTipsMenuUnlocked)
            {
                UnlockTipsMenu();
                changed = false; // UnlockTipsMenu already flushes PlayerPrefs.
            }
            if (changed)
            {
                PlayerPrefs.Save();
            }
        }

        private static bool TryReadSavedChapter(string path, out int chapter)
        {
            chapter = 0;
            try
            {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                           FileShare.ReadWrite))
                using (var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, true))
                {
                    ReadSaveHeader(reader);
                    return BurikoRuntime.TryReadPersistentLocalFlag(
                        stream, "ChapterNumber", out chapter);
                }
            }
            catch
            {
                return false;
            }
        }

        private void UpdateChapterJumpUnlockProgress()
        {
            if (_runtime == null || _host == null || _host.TitleVisible)
            {
                return;
            }

            var chapter = _host.CurrentChapterNumber;
            if (_host.GameplayUiVisible && !_host.TipsChapterVisible &&
                !_host.TipsListVisible && !_host.TipReading)
            {
                chapter++;
            }
            if (chapter <= PlayerPrefs.GetInt(ChapterJumpUnlockedKey, 0))
            {
                return;
            }

            PlayerPrefs.SetInt(ChapterJumpUnlockedKey, chapter);
            PlayerPrefs.Save();
        }

        private void UnlockTipsThroughChapter(int chapter)
        {
            chapter = Math.Max(0, chapter);
            if (chapter <= PlayerPrefs.GetInt(TipsUnlockedChapterKey, 0))
            {
                return;
            }
            PlayerPrefs.SetInt(TipsUnlockedChapterKey, chapter);
            PlayerPrefs.Save();
        }

        private bool UnlockBonusContent(bool showToast)
        {
            if (IsBonusContentUnlocked)
            {
                return false;
            }

            try
            {
                File.WriteAllText(BonusContentUnlockMarkerPath, "unlocked");
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Unable to write bonus unlock marker: " + exception.Message);
            }

            PlayerPrefs.SetInt(BonusContentUnlockedKey, 1);
            PlayerPrefs.Save();
            UnlockTipsMenu();
            if (showToast)
            {
                ShowToast("已解锁追加内容：" + BonusContentName);
            }
            HigurashiDiagnosticLog.Info("Bonus",
                "Unlocked " + BonusContentName + " episode=" +
                HigurashiActiveChapter.Profile.EpisodeNumber.ToString("00"));
            return true;
        }

        private void UpdateBonusContentUnlockProgress()
        {
            if (_runtime == null || _host == null || !_host.TitleVisible ||
                _runtime.Memory.GetGlobalFlag("GFlag_GameClear") == 0)
            {
                return;
            }

            // The original PC scripts set this flag only after the normal final
            // route. Bad endings return to the title without setting it.
            UnlockBonusContent(true);
        }

        private int GetTipsUnlockedChapter()
        {
            var stored = PlayerPrefs.GetInt(TipsUnlockedChapterKey, -1);
            if (stored >= 0)
            {
                return stored;
            }
            var migrated = IsTipsMenuUnlocked
                ? Math.Max(_host == null ? 0 : _host.CurrentChapterNumber,
                    Math.Max(0, PlayerPrefs.GetInt(ChapterJumpUnlockedKey, 0) - 1))
                : 0;
            PlayerPrefs.SetInt(TipsUnlockedChapterKey, migrated);
            PlayerPrefs.Save();
            return migrated;
        }

        private void UnlockNextChapterFromPortCredit()
        {
            if (_host == null)
            {
                return;
            }
            var sections = _host.GetChapterJumpSections();
            if (sections.Count <= 0)
            {
                ShowToast("当前篇章没有可用的章节跳跃入口");
                return;
            }

            var unlockedJump = Mathf.Clamp(
                PlayerPrefs.GetInt(ChapterJumpUnlockedKey, 0), 0, sections.Count);
            var unlockedTips = Mathf.Clamp(
                GetTipsUnlockedChapter(), 0, sections.Count);
            if (unlockedJump >= sections.Count && unlockedTips >= sections.Count)
            {
                UnlockTipsMenu();
                if (!UnlockBonusContent(true))
                {
                    ShowToast("隐藏解锁：本篇追加内容已全部解锁");
                }
                return;
            }

            var activeChapter = Math.Max(1, Math.Max(_host.CurrentChapterNumber + 1,
                unlockedJump));
            var tipsChapter = Math.Min(activeChapter, sections.Count);
            var jumpChapter = Math.Min(activeChapter + 1, sections.Count);
            UnlockTipsMenu();
            UnlockTipsThroughChapter(tipsChapter);
            if (jumpChapter > PlayerPrefs.GetInt(ChapterJumpUnlockedKey, 0))
            {
                PlayerPrefs.SetInt(ChapterJumpUnlockedKey, jumpChapter);
                PlayerPrefs.Save();
            }
            ShowToast("隐藏解锁：第" + jumpChapter + "章跳跃／第" + tipsChapter + "章 TIPS");
        }

        private void StartBonusContent()
        {
            if (_runtime == null || _host == null || !IsBonusContentUnlocked ||
                !_host.TitleVisible)
            {
                return;
            }

            try
            {
                var returnCheckpoint = CaptureCurrentCheckpoint();
                if (!_host.StartFromTitle(_runtime.Memory))
                {
                    return;
                }

                _host.StopAllAudio();
                _host.PrepareForChapterJump();
                _bonusContentReturnCheckpoint = returnCheckpoint;
                _bonusContentReturnCallDepth = _runtime.CallDepth;
                _fastTraversal.Stop();
                _autoMode = false;
                _timeline.Clear();
                _runtime.CallScriptFromUi(BonusContentScript);
                CloseAllModals();
                _suppressInputUntilFrame = Time.frameCount + 2;
                HigurashiDiagnosticLog.Info("Bonus",
                    "Started " + BonusContentScript + " " + RuntimeLocation());
                DriveRuntime(false);
                CaptureDialogueCheckpoint();
            }
            catch (Exception exception)
            {
                _bonusContentReturnCheckpoint = null;
                if (_titleCheckpoint != null)
                {
                    RestoreCheckpoint(_titleCheckpoint);
                }
                CloseAllModals();
                _extrasVisible = true;
                ShowToast(BonusContentName + "启动失败");
                HigurashiDiagnosticLog.Error("Bonus",
                    "Unable to start " + BonusContentScript, exception);
            }
        }

        private void StartGame()
        {
            if (_runtime == null || !_host.StartFromTitle(_runtime.Memory))
            {
                return;
            }

            ResetStoryChoiceState();
            _runtime.ResumeInput();
            _suppressInputUntilFrame = Time.frameCount + 2;
            DriveRuntime(false);
            CaptureDialogueCheckpoint();
            _showHelpWhenGameplayStarts = PlayerPrefs.GetInt(HelpSeenKey, 0) == 0;
        }

        private void EnterFragmentList()
        {
            if (_runtime == null || !_host.ResolveFragmentChapterToList(_runtime.Memory))
            {
                return;
            }

            _fastTraversal.Stop();
            _autoMode = false;
            _runtime.ResumeInput();
            _suppressInputUntilFrame = Time.frameCount + 2;
            HigurashiDiagnosticLog.Info("Fragment",
                "Opened fragment list read=" +
                _runtime.Memory.GetLocalFlag("LFragmentRead") + " " + RuntimeLocation());
            DriveRuntime(false);
            CaptureDialogueCheckpoint();
        }

        private void ExitFragmentList()
        {
            if (_runtime == null || !_host.ExitFragmentList(_runtime.Memory))
            {
                return;
            }

            _fastTraversal.Stop();
            _autoMode = false;
            _runtime.ResumeInput();
            _suppressInputUntilFrame = Time.frameCount + 2;
            HigurashiDiagnosticLog.Info("Fragment",
                "Closed fragment list read=" +
                _runtime.Memory.GetLocalFlag("LFragmentRead") + " " + RuntimeLocation());
            DriveRuntime(false);
            CaptureDialogueCheckpoint();
        }

        private void StartSelectedFragment()
        {
            if (_runtime == null ||
                !_host.TryStartSelectedFragment(_runtime.Memory, out var scriptName))
            {
                return;
            }

            _fastTraversal.Stop();
            _autoMode = false;
            _host.StopVoices();
            _runtime.CallScriptFromUi(scriptName);
            _suppressInputUntilFrame = Time.frameCount + 2;
            HigurashiDiagnosticLog.Info("Fragment",
                "Started script=" + scriptName + " read=" +
                _runtime.Memory.GetLocalFlag("LFragmentRead") + " " + RuntimeLocation());
            DriveRuntime(false);
            CaptureDialogueCheckpoint();
        }

        private void EnterChapterTips(bool allUnlocked)
        {
            if (_runtime == null || !_host.ResolveTipsChapterToList(_runtime.Memory, allUnlocked))
            {
                return;
            }

            _runtime.ResumeInput();
            _suppressInputUntilFrame = Time.frameCount + 2;
            DriveRuntime(false);
        }

        private void ContinuePastTips()
        {
            var nextChapter = _host == null ? 0 : _host.CurrentChapterNumber + 1;
            if (_runtime == null || !_host.ContinuePastTips(_runtime.Memory))
            {
                return;
            }

            if (nextChapter > PlayerPrefs.GetInt(ChapterJumpUnlockedKey, 0))
            {
                PlayerPrefs.SetInt(ChapterJumpUnlockedKey, nextChapter);
                PlayerPrefs.Save();
            }
            UnlockTipsThroughChapter(Math.Max(0, nextChapter - 1));

            _runtime.ResumeInput();
            _suppressInputUntilFrame = Time.frameCount + 2;
            DriveRuntime(false);
            CaptureDialogueCheckpoint();
        }

        private void OpenTipsLibrary()
        {
            RefreshUnlockProgressFromSaves();
            if (_runtime == null || !_host.OpenTipsLibrary(_runtime.Memory,
                    GetTipsUnlockedChapter()))
            {
                return;
            }

            CloseAllModals();
            _fastTraversal.Stop();
            SuppressInput();
            HigurashiDiagnosticLog.Info("TIPS",
                "Opened standalone library; unlockedChapter=" + GetTipsUnlockedChapter());
        }

        private void ExitTipsLibrary()
        {
            if (_runtime == null || _host == null)
            {
                return;
            }
            var standalone = _host.TipsLibraryStandalone;
            if (!_host.ExitTips(_runtime.Memory))
            {
                return;
            }

            if (!standalone && !_host.TipsChapterVisible)
            {
                // Standalone and in-flow TIPS use different return states. The
                // in-flow list closes back to the four-button chapter screen;
                // resuming the script here would skip that screen and enter the
                // next chapter.
                _runtime.ResumeInput();
                DriveRuntime(false);
            }
            SuppressInput();
            HigurashiDiagnosticLog.Info("TIPS",
                "Closed library; standalone=" + standalone);
        }

        private void StartSelectedTip()
        {
            if (_runtime == null || _host == null)
            {
                return;
            }

            var returnCheckpoint = CaptureCurrentCheckpoint();
            if (!_host.TryStartSelectedTip(_runtime.Memory, out var scriptName))
            {
                return;
            }

            // Both the chapter-end TIPS flow and the main-menu standalone
            // library must return to the exact list that launched the tip.
            _tipsLibraryReturnCheckpoint = returnCheckpoint;
            _tipsLibraryReturnCallDepth = _runtime.CallDepth;

            _fastTraversal.Stop();
            _autoMode = false;
            _host.StopVoices();
            _runtime.CallScriptFromUi(scriptName);
            HigurashiDiagnosticLog.Info("TIPS",
                "Started script=" + scriptName + " standalone=" + _host.TipsLibraryStandalone);
            _suppressInputUntilFrame = Time.frameCount + 2;
            DriveRuntime(false);
            CaptureDialogueCheckpoint();
        }

        private void SelectChoice(int index)
        {
            var openingChoice = _host.IsOpeningChoice;
            if (!_host.Choose(index, _runtime.Memory))
            {
                return;
            }

            _runtime.ResumeInput();
            _suppressInputUntilFrame = Time.frameCount + 2;
            DriveRuntime(false);
            if (openingChoice)
            {
                SaveOpeningPreference();
            }
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
            SaveOpeningPreference();
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
                    if (json.IndexOf("\"autoSave\"", StringComparison.Ordinal) < 0)
                    {
                        loaded.autoSave = true;
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
                _autoWasVoicePlaying = _host != null && _host.IsVoicePlaying;
                ScheduleNextAutoAdvance();
                ShowToast("自动播放：开");
            }
            else
            {
                _autoWasVoicePlaying = false;
                ShowToast("自动播放：关");
            }
        }

        private void ScheduleNextAutoAdvance()
        {
            var normalized = Mathf.Clamp01(_settings.autoSpeed / 100f);
            _nextAutoAdvanceAt = Time.unscaledTime + Mathf.Lerp(6f, 1.2f, normalized);
        }

        private void SaveOpeningPreference()
        {
            if (_runtime == null)
            {
                return;
            }
            var preference = _runtime.Memory.GetGlobalFlag("GVideoOpening");
            if (preference <= 0)
            {
                return;
            }
            PlayerPrefs.SetInt(OpeningPreferenceKey, preference);
            PlayerPrefs.Save();
        }

        private bool CanSaveGame()
        {
            return _runtime != null && _host != null &&
                   SaveStatePolicy.CanWriteRegularSave(
                       CurrentSaveSurface(), _host.SavingEnabled, _host.InterfaceEnabled);
        }

        private void SaveGame(int slot, bool showToast = true, bool updateLatest = true)
        {
            var surface = CurrentSaveSurface();
            HigurashiDiagnosticLog.Info("Save",
                "Requested slot=" + slot + " kind=" + SaveKind(slot) +
                " mirrorLatest=" + updateLatest + " surface=" + surface + " " +
                RuntimeLocation());
            if (!CanSaveGame())
            {
                HigurashiDiagnosticLog.Warning("Save",
                    "Rejected slot=" + slot + " surface=" + surface +
                    " savingEnabled=" + (_host != null && _host.SavingEnabled) +
                    " interfaceEnabled=" + (_host != null && _host.InterfaceEnabled) + " " +
                    RuntimeLocation());
                if (showToast)
                {
                    ShowToast("当前画面不能保存");
                }
                return;
            }

            try
            {
                WriteSaveGame(slot, null, SaveKind(slot));
                if (updateLatest && slot != LatestSaveSlot)
                {
                    WriteSaveGame(LatestSaveSlot, null, "latest-mirror");
                }
                if (showToast)
                {
                    ShowToast(SaveCompletedMessage(slot));
                }
                RefreshUnlockProgressFromSaves();
                HigurashiDiagnosticLog.Info("Save",
                    "Saved slot=" + slot + " updateLatest=" + updateLatest + " " + RuntimeLocation());
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Unable to save game: " + exception);
                HigurashiDiagnosticLog.Warning("Save",
                    "Save failed slot=" + slot + " " + exception.Message);
                if (showToast)
                {
                    ShowToast("保存失败");
                }
            }
        }

        private void WriteSaveGame(
            int slot,
            string summaryOverride = null,
            string saveReason = "unspecified")
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
                writer.Write(summaryOverride ?? SaveSummary());
                _runtime.WritePersistentState(stream);
                _host.WritePersistentState(stream);
                WriteTimelineState(stream);
                stream.Flush();
            }
            if (File.Exists(path))
            {
                File.Delete(path);
            }
            File.Move(temporaryPath, path);
            HigurashiDiagnosticLog.Info("SaveIO",
                "Write completed slot=" + slot + " reason=" + saveReason +
                " bytes=" + new FileInfo(path).Length + " " + RuntimeLocation());
        }

        private void LoadGame(int slot)
        {
            var path = SaveSlotPath(slot);
            if (!File.Exists(path))
            {
                HigurashiDiagnosticLog.Warning("Load", "Requested empty slot=" + slot);
                ShowToast("该槽位没有存档");
                return;
            }

            var returnCheckpoint = CaptureCurrentCheckpoint();
            try
            {
                _tipsLibraryReturnCheckpoint = null;
                _bonusContentReturnCheckpoint = null;
                var requestedInfo = ReadSaveSlotInfo(slot);
                HigurashiDiagnosticLog.Info("Load",
                    "Requested slot=" + slot +
                    " savedAt=" + (requestedInfo == null
                        ? "unknown"
                        : requestedInfo.Timestamp.ToString("yyyy-MM-dd HH:mm:ss")) +
                    " savedLocation=" + (requestedInfo == null
                        ? "unknown"
                        : requestedInfo.Script + ":" + requestedInfo.Line));
                RestoreSaveGame(path);
                SaveSlotInfo recoveredInfo = null;
                if (IsKnownLegacyTipsBrowserSave(requestedInfo) ||
                    !IsRecoverableStorySaveState())
                {
                    recoveredInfo = TryRestoreLatestRecoverableStorySlot(slot);
                    if (recoveredInfo == null)
                    {
                        RestoreCheckpoint(returnCheckpoint);
                        ShowToast("该存档是旧版误存的内容菜单，且没有可恢复的剧情存档");
                        HigurashiDiagnosticLog.Warning("Load",
                            "Rejected browser-only save slot=" + slot);
                        return;
                    }

                    if (slot == LatestSaveSlot)
                    {
                        RepairLatestSaveAfterRecovery(path, recoveredInfo.Summary);
                    }
                }
                ResetStoryChoiceState();
                _capturedDialogueSerial = _host.DialogueSerial;
                var loadedCheckpoint = CaptureCurrentCheckpoint();
                if (!_timeline.TryGetCurrent(out var restoredCurrent) ||
                    restoredCurrent.ChapterNumber != loadedCheckpoint.ChapterNumber ||
                    !string.Equals(restoredCurrent.ScriptName, loadedCheckpoint.ScriptName,
                        StringComparison.OrdinalIgnoreCase) ||
                    restoredCurrent.LineNumber != loadedCheckpoint.LineNumber)
                {
                    if (restoredCurrent != null &&
                        restoredCurrent.ChapterNumber != loadedCheckpoint.ChapterNumber)
                    {
                        _timeline.Clear();
                    }
                    _timeline.Push(loadedCheckpoint);
                }
                _timelineChapterNumber = loadedCheckpoint.ChapterNumber;
                RefreshUnlockProgressFromSaves();
                _fastTraversal.Stop();
                _showHelpWhenGameplayStarts = false;
                CloseAllModals();
                _suppressInputUntilFrame = Time.frameCount + 2;
                ShowToast(recoveredInfo != null
                    ? (slot == LatestSaveSlot
                        ? "最新保存异常，已恢复最近有效剧情存档"
                        : "该存档异常，已恢复最近有效剧情存档")
                    : LoadCompletedMessage(slot));
                HigurashiDiagnosticLog.Info("Load",
                    "Loaded slot=" + slot + " recoveredSlot=" +
                    (recoveredInfo == null ? -1 : recoveredInfo.Slot) +
                    " timeline=" + _timeline.Count +
                    " cursor=" + _timeline.Cursor + " " + RuntimeLocation());
            }
            catch (Exception exception)
            {
                try
                {
                    RestoreCheckpoint(returnCheckpoint);
                }
                catch
                {
                    // Keep the original load error as the diagnostic root cause.
                }
                Debug.LogWarning("Unable to load game: " + exception);
                HigurashiDiagnosticLog.Warning("Load",
                    "Load failed slot=" + slot + " " + exception.Message);
                ShowToast("存档损坏或版本不兼容");
            }
        }

        private void RestoreSaveGame(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, true))
            {
                ReadSaveHeader(reader);
                // Stop every old channel first so BGM/SE from the pre-load scene
                // cannot leak into the restored scene.
                _host.StopAllAudio();
                _runtime.ReadPersistentState(stream);
                // Art/audio choices are app-wide preferences. An older save
                // restores story flags, but must not override Settings.
                _host.ApplySettings(_runtime.Memory);
                _host.ReadPersistentState(stream, _runtime.Memory);
                ReadTimelineState(stream);
            }
            HigurashiDiagnosticLog.Info("LoadIO",
                "State restored file=" + Path.GetFileName(path) + " surface=" +
                CurrentSaveSurface() + " " + RuntimeLocation());
        }

        private bool IsRecoverableStorySaveState()
        {
            return _runtime != null && _host != null &&
                   SaveStatePolicy.IsRecoverableStorySave(
                       CurrentSaveSurface(), _host.SavingEnabled, _host.InterfaceEnabled);
        }

        private SaveSurface CurrentSaveSurface()
        {
            if (_runtime == null || _host == null ||
                _runtime.BlockReason == BurikoBlockReason.Faulted)
            {
                return SaveSurface.Faulted;
            }
            if (_runtime.BlockReason == BurikoBlockReason.Completed)
            {
                return SaveSurface.Completed;
            }
            if (_host.TitleVisible) return SaveSurface.Title;
            if (_host.CreditsVisible) return SaveSurface.Credits;
            if (_host.MovieVisible) return SaveSurface.Movie;
            if (_host.TipsListVisible) return SaveSurface.TipsList;
            if (_host.TipReading) return SaveSurface.TipReading;
            if (_host.FragmentListVisible) return SaveSurface.FragmentList;
            if (_host.FragmentChapterVisible) return SaveSurface.FragmentChapter;
            if (_host.ChapterPreviewVisible) return SaveSurface.ChapterPreview;
            if (_host.TipsChapterVisible) return SaveSurface.TipsChapter;
            if (_host.ChoiceVisible) return SaveSurface.Choice;
            if (IsBonusContentScript(_runtime.CurrentScriptName)) return SaveSurface.BonusContent;
            if (IsFragmentReadingScript(_runtime.CurrentScriptName, _runtime.Memory))
            {
                return SaveSurface.FragmentReading;
            }
            return SaveSurface.Story;
        }

        private bool IsBonusContentScript(string scriptName)
        {
            return !string.IsNullOrWhiteSpace(scriptName) &&
                   string.Equals(scriptName, BonusContentScript,
                       StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsFragmentReadingScript(string scriptName, BurikoMemory memory)
        {
            if (memory == null || memory.GetLocalFlag("LFragmentLoop") <= 0 ||
                string.IsNullOrWhiteSpace(scriptName))
            {
                return false;
            }

            return scriptName.StartsWith("_kakera", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(scriptName, "kakera_miss", StringComparison.OrdinalIgnoreCase);
        }

        private SaveSlotInfo TryRestoreLatestRecoverableStorySlot(int excludedSlot)
        {
            var candidates = new[]
            {
                0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10,
                101, 102, 103, 201, 202, 203
            };
            var saves = new List<SaveSlotInfo>();
            for (var i = 0; i < candidates.Length; i++)
            {
                var candidate = candidates[i];
                if (candidate == excludedSlot)
                {
                    continue;
                }

                var info = ReadSaveSlotInfo(candidate);
                if (info != null && info.Timestamp > DateTime.MinValue &&
                    !IsKnownLegacyTipsBrowserSave(info))
                {
                    saves.Add(info);
                }
            }
            saves.Sort((left, right) => right.Timestamp.CompareTo(left.Timestamp));

            for (var i = 0; i < saves.Count; i++)
            {
                try
                {
                    HigurashiDiagnosticLog.Info("Load",
                        "Trying recovery candidate slot=" + saves[i].Slot +
                        " savedLocation=" + saves[i].Script + ":" + saves[i].Line);
                    RestoreSaveGame(SaveSlotPath(saves[i].Slot));
                    if (IsRecoverableStorySaveState())
                    {
                        HigurashiDiagnosticLog.Info("Load",
                            "Accepted recovery candidate slot=" + saves[i].Slot +
                            " surface=" + CurrentSaveSurface());
                        return saves[i];
                    }
                    HigurashiDiagnosticLog.Warning("Load",
                        "Rejected recovery candidate slot=" + saves[i].Slot +
                        " surface=" + CurrentSaveSurface());
                }
                catch (Exception exception)
                {
                    HigurashiDiagnosticLog.Warning("Load",
                        "Recovery candidate failed slot=" + saves[i].Slot + " " + exception.Message);
                }
            }
            return null;
        }

        private void RepairLatestSaveAfterRecovery(string invalidPath, string recoveredSummary)
        {
            try
            {
                var backup = invalidPath + ".invalid-" +
                             DateTime.UtcNow.Ticks + ".bak";
                if (File.Exists(invalidPath))
                {
                    File.Copy(invalidPath, backup, false);
                }
                WriteSaveGame(LatestSaveSlot, recoveredSummary, "recovery-repair");
                HigurashiDiagnosticLog.Info("Load",
                    "Repaired latest save; backup=" + Path.GetFileName(backup));
            }
            catch (Exception exception)
            {
                HigurashiDiagnosticLog.Warning("Load",
                    "Unable to repair latest save: " + exception.Message);
            }
        }

        private bool SaveChapterCompletionProgress(int chapter)
        {
            try
            {
                var summary = "第 " + Math.Max(1, chapter) + " 章完成（TIPS 已解锁）";
                WriteSaveGame(FindOldestOrEmptySlot(201, 203), summary, "chapter-complete");
                WriteSaveGame(LatestSaveSlot, summary, "chapter-complete-latest");
                RefreshUnlockProgressFromSaves();
                HigurashiDiagnosticLog.Info("Save",
                    "Saved chapter completion chapter=" + chapter + " " + RuntimeLocation());
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Unable to save chapter completion: " + exception);
                HigurashiDiagnosticLog.Warning("Save",
                    "Chapter completion save failed chapter=" + chapter + " " + exception.Message);
                return false;
            }
        }

        private void WriteTimelineState(Stream output)
        {
            var all = _timeline.CopyThroughCurrent();
            var chapter = Math.Max(0, _runtime.Memory.GetLocalFlag("ChapterNumber"));
            var sameChapter = new List<RuntimeCheckpoint>(all.Length);
            for (var i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].ChapterNumber == chapter)
                {
                    sameChapter.Add(all[i]);
                }
            }

            var selected = new List<RuntimeCheckpoint>();
            if (sameChapter.Count > 0)
            {
                // Always preserve the chapter's first dialogue, then keep the
                // most recent checkpoints up to the compact persistence limit.
                selected.Add(sameChapter[0]);
                var start = Math.Max(1, sameChapter.Count - (PersistedTimelineLimit - 1));
                for (var i = start; i < sameChapter.Count; i++)
                {
                    selected.Add(sameChapter[i]);
                }
            }

            using (var writer = new BinaryWriter(output, System.Text.Encoding.UTF8, true))
            {
                writer.Write(SaveTimelineMagic);
                writer.Write(chapter);
                writer.Write(selected.Count);
                for (var i = 0; i < selected.Count; i++)
                {
                    WritePersistedCheckpoint(writer, selected[i]);
                }
            }
        }

        private void ReadTimelineState(Stream input)
        {
            _timeline.Clear();
            _timelineChapterNumber = Math.Max(0, _runtime.Memory.GetLocalFlag("ChapterNumber"));
            if (!input.CanSeek || input.Length - input.Position < sizeof(int) * 3)
            {
                return;
            }

            var start = input.Position;
            using (var reader = new BinaryReader(input, System.Text.Encoding.UTF8, true))
            {
                if (reader.ReadInt32() != SaveTimelineMagic)
                {
                    input.Position = start;
                    return;
                }
                var chapter = reader.ReadInt32();
                var count = reader.ReadInt32();
                if (chapter != _timelineChapterNumber || count < 0 || count > PersistedTimelineLimit)
                {
                    throw new InvalidDataException("Invalid saved rewind timeline.");
                }
                for (var i = 0; i < count; i++)
                {
                    var checkpoint = ReadPersistedCheckpoint(reader, chapter);
                    _timeline.Push(checkpoint);
                }
            }
        }

        private static void WritePersistedCheckpoint(BinaryWriter writer, RuntimeCheckpoint checkpoint)
        {
            writer.Write(checkpoint.AppendNext);
            writer.Write(checkpoint.ScriptName ?? string.Empty);
            writer.Write(checkpoint.LineNumber);
            WriteCompressedCheckpointBytes(writer, checkpoint.RuntimeState);
            WriteCompressedCheckpointBytes(writer, checkpoint.PresentationState);
        }

        private static RuntimeCheckpoint ReadPersistedCheckpoint(BinaryReader reader, int chapter)
        {
            var appendNext = reader.ReadBoolean();
            var scriptName = reader.ReadString();
            var lineNumber = reader.ReadInt32();
            var runtimeState = ReadCheckpointBytes(reader, "runtime");
            var presentationState = ReadCheckpointBytes(reader, "presentation");
            return new RuntimeCheckpoint(chapter, appendNext, scriptName, lineNumber,
                runtimeState, presentationState);
        }

        private static byte[] ReadCheckpointBytes(BinaryReader reader, string description)
        {
            var originalLength = reader.ReadInt32();
            var compressedLength = reader.ReadInt32();
            if (originalLength < 0 || originalLength > 32 * 1024 * 1024 ||
                compressedLength < 0 || compressedLength > 32 * 1024 * 1024)
            {
                throw new InvalidDataException("Invalid saved " + description + " checkpoint size.");
            }
            var compressed = reader.ReadBytes(compressedLength);
            if (compressed.Length != compressedLength)
            {
                throw new EndOfStreamException("Incomplete saved " + description + " checkpoint.");
            }
            using (var input = new MemoryStream(compressed, false))
            using (var gzip = new GZipStream(input, CompressionMode.Decompress))
            using (var output = new MemoryStream(originalLength))
            {
                gzip.CopyTo(output);
                var bytes = output.ToArray();
                if (bytes.Length != originalLength)
                {
                    throw new InvalidDataException("Saved " + description + " checkpoint length mismatch.");
                }
                return bytes;
            }
        }

        private static void WriteCompressedCheckpointBytes(BinaryWriter writer, byte[] bytes)
        {
            bytes = bytes ?? Array.Empty<byte>();
            byte[] compressed;
            using (var output = new MemoryStream())
            {
                using (var gzip = new GZipStream(
                           output, System.IO.Compression.CompressionLevel.Fastest, true))
                {
                    gzip.Write(bytes, 0, bytes.Length);
                }
                compressed = output.ToArray();
            }
            writer.Write(bytes.Length);
            writer.Write(compressed.Length);
            writer.Write(compressed);
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
                    var info = ReadSaveHeader(reader);
                    return new SaveSlotInfo(slot, info.Timestamp, info.Script, info.Line, info.Summary);
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

        private static bool IsKnownLegacyTipsBrowserSave(SaveSlotInfo info)
        {
            return info != null && SaveStatePolicy.IsKnownLegacyTipsBrowserSave(
                info.Script, info.Summary);
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
            var quick = FindLatestSlot(101, 103);
            var quickInfo = quick >= 0 ? ReadSaveSlotInfo(quick) : null;
            if (quickInfo != null && quickInfo.Timestamp > newest)
            {
                newest = quickInfo.Timestamp;
                result = quick;
            }
            var automatic = FindLatestSlot(201, 203);
            var automaticInfo = automatic >= 0 ? ReadSaveSlotInfo(automatic) : null;
            if (automaticInfo != null && automaticInfo.Timestamp > newest)
            {
                result = automatic;
            }
            return result;
        }

        private int FindLatestSlot(int first, int last)
        {
            var result = -1;
            var newest = DateTime.MinValue;
            for (var slot = first; slot <= last; slot++)
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

        private int FindOldestOrEmptySlot(int first, int last)
        {
            var result = first;
            var oldest = DateTime.MaxValue;
            for (var slot = first; slot <= last; slot++)
            {
                var info = ReadSaveSlotInfo(slot);
                if (info == null)
                {
                    return slot;
                }
                if (info.Timestamp < oldest)
                {
                    oldest = info.Timestamp;
                    result = slot;
                }
            }
            return result;
        }

        private void SaveQuickGame()
        {
            SaveGame(FindOldestOrEmptySlot(101, 103));
        }

        private void LoadLatestQuickGame()
        {
            var slot = ReadSaveSlotInfo(LatestSaveSlot) != null
                ? LatestSaveSlot
                : FindLatestSlot(101, 103);
            if (slot < 0 && ReadSaveSlotInfo(0) != null)
            {
                slot = 0;
            }
            if (slot < 0)
            {
                ShowToast("没有快速存档");
                return;
            }
            LoadGame(slot);
        }

        private void MaybeAutoSave(bool force)
        {
            if (_settings == null || !_settings.autoSave || !CanSaveGame() ||
                (!force && _dialoguesSinceAutoSave < 12 &&
                Time.unscaledTime - _lastAutoSaveAt < 45f))
            {
                if (force)
                {
                    HigurashiDiagnosticLog.Info("AutoSave",
                        "Forced auto-save skipped enabled=" +
                        (_settings != null && _settings.autoSave) +
                        " surface=" + CurrentSaveSurface() + " " + RuntimeLocation());
                }
                return;
            }
            HigurashiDiagnosticLog.Info("AutoSave",
                "Triggered force=" + force + " dialogues=" + _dialoguesSinceAutoSave +
                " surface=" + CurrentSaveSurface() + " " + RuntimeLocation());
            SaveGame(FindOldestOrEmptySlot(201, 203), showToast: false);
            _dialoguesSinceAutoSave = 0;
            _lastAutoSaveAt = Time.unscaledTime;
        }

        private void ReturnToTitle()
        {
            if (_titleCheckpoint == null)
            {
                ShowToast("主菜单状态尚未准备完成");
                return;
            }
            MaybeAutoSave(true);
            _host.StopAllAudio();
            _autoMode = false;
            _showHelpWhenGameplayStarts = false;
            _fastTraversal.Stop();
            _timeline.Clear();
            ResetStoryChoiceState();
            RestoreCheckpoint(_titleCheckpoint);
            CloseAllModals();
            _suppressInputUntilFrame = Time.frameCount + 2;
        }

        private static string SaveCompletedMessage(int slot)
        {
            if (slot >= 201) return "自动保存完成";
            if (slot >= 101) return "快速保存完成";
            if (slot == 0 || slot == LatestSaveSlot) return "最新保存已更新";
            return "已保存到文件 " + (slot - 1).ToString("00");
        }

        private static string LoadCompletedMessage(int slot)
        {
            if (slot >= 201) return "已读取自动存档 " + (slot - 200);
            if (slot >= 101) return "已读取快速存档 " + (slot - 100);
            if (slot == 0 || slot == LatestSaveSlot) return "已读取最新保存";
            return "已读取文件 " + (slot - 1).ToString("00");
        }

        private static string SaveKind(int slot)
        {
            if (slot >= 201) return "auto";
            if (slot >= 101) return "quick";
            if (slot == LatestSaveSlot || slot == 0) return "latest";
            return "manual";
        }

        private string SaveSlotPath(int slot)
        {
            return Path.Combine(Application.persistentDataPath, "Saves", "slot-" + slot + ".hgs");
        }

        private string SaveSummary()
        {
            var fragmentProgress = HigurashiActiveChapter.Profile.EpisodeNumber == 8 && _runtime != null &&
                (_host.FragmentChapterVisible || _host.FragmentListVisible ||
                 _runtime.Memory.GetLocalFlag("LFragmentLoop") > 0 ||
                 _runtime.Memory.GetLocalFlag("LFragmentRead") > 0);

            var text = string.IsNullOrEmpty(_host.Speaker)
                ? _host.Dialogue
                : _host.Speaker + "　" + _host.Dialogue;
            text = (text ?? string.Empty).Replace('\n', ' ').Replace('\r', ' ').Trim();
            text = text.Length <= 80 ? text : text.Substring(0, 80) + "…";
            return fragmentProgress
                ? "已解锁碎片\n" + (string.IsNullOrEmpty(text) ? "碎片编织中" : text)
                : text;
        }

        private void ExportDiagnosticLog()
        {
            try
            {
                var path = HigurashiDiagnosticLog.CreateExport(
                    Application.persistentDataPath,
                    BuildDiagnosticReport());
                if (IOSDiagnosticLogExporter.Share(path))
                {
                    ShowToast("系统日志已生成，请选择保存或分享");
                }
                else
                {
                    ShowToast("系统日志已保存到 Documents/logs");
                }
            }
            catch (Exception exception)
            {
                HigurashiDiagnosticLog.Warning("Export", exception.Message);
                ShowToast("系统日志导出失败");
            }
        }

        private string BuildDiagnosticReport()
        {
            var builder = new System.Text.StringBuilder(2048);
            builder.AppendLine("Higurashi iOS diagnostic report");
            builder.AppendLine("Generated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss zzz"));
            builder.AppendLine("Episode: " + HigurashiActiveChapter.Profile.EpisodeCode);
            builder.AppendLine("Product: " + HigurashiActiveChapter.Profile.ProductName);
            builder.AppendLine("App version: " + Application.version);
            builder.AppendLine("Unity: " + Application.unityVersion);
            builder.AppendLine("Platform: " + Application.platform);
            builder.AppendLine("OS: " + SystemInfo.operatingSystem);
            builder.AppendLine("Device model: " + SystemInfo.deviceModel);
            builder.AppendLine("Device type: " + SystemInfo.deviceType);
            builder.AppendLine("CPU: " + SystemInfo.processorType + " x" + SystemInfo.processorCount);
            builder.AppendLine("Memory MB: " + SystemInfo.systemMemorySize);
            builder.AppendLine("Graphics: " + SystemInfo.graphicsDeviceName + " / " + SystemInfo.graphicsDeviceVersion);
            builder.AppendLine("Screen: " + Screen.width + "x" + Screen.height + " @" + Screen.dpi + "dpi");
            builder.AppendLine("Safe area: " + Screen.safeArea);
            builder.AppendLine("Orientation: " + Screen.orientation);
            builder.AppendLine("Runtime: " + RuntimeLocation());
            builder.AppendLine("Save surface: " + CurrentSaveSurface() +
                               " savingEnabled=" + (_host != null && _host.SavingEnabled) +
                               " interfaceEnabled=" + (_host != null && _host.InterfaceEnabled));
            builder.AppendLine("Timeline: count=" + _timeline.Count + " cursor=" + _timeline.Cursor +
                               " chapterFloor=" + _timelineChapterNumber);
            builder.AppendLine("UI: title=" + (_host != null && _host.TitleVisible) +
                               " gameplay=" + (_host != null && _host.GameplayUiVisible) +
                               " window=" + (_host != null && _host.WindowVisible) +
                               " tipsList=" + (_host != null && _host.TipsListVisible) +
                               " tipReading=" + (_host != null && _host.TipReading) +
                               " chapterPreview=" + (_host != null && _host.ChapterPreviewVisible) +
                               " fragmentChapter=" + (_host != null && _host.FragmentChapterVisible) +
                               " fragmentList=" + (_host != null && _host.FragmentListVisible));
            builder.AppendLine("Fragments: active=" +
                               (_runtime != null && _runtime.Memory.GetLocalFlag("LFragmentLoop") > 0) +
                               " read=" + (_runtime == null
                                   ? 0
                                   : _runtime.Memory.GetLocalFlag("LFragmentRead")) +
                               " page=" + (_host == null ? 0 : _host.FragmentPage));
            if (_settings != null)
            {
                builder.AppendLine("Settings: artSet=" + _settings.artSetIndex +
                                   " lipSync=" + _settings.lipSync +
                                   " autoSave=" + _settings.autoSave +
                                   " bgmVolume=" + _settings.bgmVolume +
                                   " voiceVolume=" + _settings.voiceVolume +
                                   " presentation=" + _settings.presentationMode);
            }
            builder.AppendLine("Unlocks: chapterJump=" + PlayerPrefs.GetInt(ChapterJumpUnlockedKey, 0) +
                               " tips=" + PlayerPrefs.GetInt(TipsUnlockedChapterKey, 0) +
                               " tipsMenu=" + IsTipsMenuUnlocked +
                               " bonus=" + IsBonusContentUnlocked +
                               " gameClear=" + (_runtime != null
                                   ? _runtime.Memory.GetGlobalFlag("GFlag_GameClear")
                                   : 0));
            builder.AppendLine("Data installed: " +
                               DataPackImportService.IsInstalled(Application.persistentDataPath));
            builder.AppendLine("Save slots:");
            AppendSaveDiagnostics(builder);
            return builder.ToString();
        }

        private void AppendSaveDiagnostics(System.Text.StringBuilder builder)
        {
            var groups = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10,
                101, 102, 103, 201, 202, 203 };
            for (var i = 0; i < groups.Length; i++)
            {
                var info = ReadSaveSlotInfo(groups[i]);
                if (info == null)
                {
                    continue;
                }
                builder.AppendLine("  slot=" + groups[i] + " time=" +
                                   info.Timestamp.ToString("yyyy-MM-dd HH:mm:ss") +
                                   " kind=" + SaveKind(groups[i]) +
                                   " script=" + info.Script + " line=" + info.Line +
                                   " legacyTipsBrowser=" + IsKnownLegacyTipsBrowserSave(info));
            }
        }

        private string RuntimeLocation()
        {
            if (_runtime == null)
            {
                return "runtime=null";
            }
            return "script=" + (_runtime.CurrentScriptName ?? string.Empty) +
                   " line=" + _runtime.CurrentLine +
                   " chapter=" + _runtime.Memory.GetLocalFlag("ChapterNumber") +
                   " block=" + _runtime.BlockReason;
        }

        private string BuildDiagnosticState(string reason)
        {
            return reason + " episode=" + HigurashiActiveChapter.Profile.EpisodeCode +
                   " app=" + Application.version + " unity=" + Application.unityVersion +
                   " device=" + SystemInfo.deviceModel + " os=" + SystemInfo.operatingSystem +
                   " screen=" + Screen.width + "x" + Screen.height + " " + RuntimeLocation();
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
            public RuntimeCheckpoint(
                int chapterNumber,
                bool appendNext,
                string scriptName,
                int lineNumber,
                byte[] runtimeState,
                byte[] presentationState)
            {
                ChapterNumber = Math.Max(0, chapterNumber);
                AppendNext = appendNext;
                ScriptName = scriptName ?? string.Empty;
                LineNumber = Math.Max(0, lineNumber);
                RuntimeState = runtimeState ?? Array.Empty<byte>();
                PresentationState = presentationState ?? Array.Empty<byte>();
            }

            public int ChapterNumber { get; }
            public bool AppendNext { get; }
            public string ScriptName { get; }
            public int LineNumber { get; }
            public byte[] RuntimeState { get; }
            public byte[] PresentationState { get; }
        }
    }
}
