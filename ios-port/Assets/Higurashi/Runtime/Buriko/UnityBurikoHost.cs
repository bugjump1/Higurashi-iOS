using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Higurashi.IOS.Buriko;
using Higurashi.IOS.Compatibility;
using Higurashi.IOS.Data;
using Higurashi.IOS.Playback;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Video;

namespace Higurashi.IOS.Runtime.Buriko
{
    public sealed class UnityBurikoHost : MonoBehaviour, IBurikoHost
    {
        private const int PersistentStateMagic = 0x31504848; // HHP1
        private const int PersistentUiStateMagic = 0x32554948; // HIU2
        private const int PersistentVisualStateMagic = 0x33564948; // HIV3
        private const int PersistentFragmentUiStateMagic = 0x34524948; // HIR4
        private const int PersistentAudioStateMagic = 0x35414848; // HHA5
        private const int PersistentHistoryVoiceStateMagic = 0x36564848; // HHV6
        private const int PersistentTipsUiStateMagic = 0x37544848; // HHT7
        private const int PersistentLastVoiceStateMagic = 0x38564848; // HHV8
        private const int PersistentLayerAnchorStateMagic = 0x39414848; // HHA9
        private const int PersistentFilmStateMagic = 0x31464848; // HHF1
        private const int PersistentMessageSpeedStateMagic = 0x31534848; // HHS1
        private const int PersistentTipReadingStateMagic = 0x31525448; // HTR1
        private readonly List<RuntimePathCascade> _artSets = new List<RuntimePathCascade>();
        private readonly List<RuntimePathCascade> _spriteSets = new List<RuntimePathCascade>();
        private readonly List<RuntimePathCascade> _backgroundSets = new List<RuntimePathCascade>();
        private readonly List<RuntimePathCascade> _bgmSets = new List<RuntimePathCascade>();
        private readonly List<RuntimePathCascade> _seSets = new List<RuntimePathCascade>();
        private readonly List<RuntimeAudioSet> _audioSets = new List<RuntimeAudioSet>();
        private readonly SortedDictionary<int, PresentationLayer> _layers =
            new SortedDictionary<int, PresentationLayer>();
        private readonly List<PresentationLayer> _previousSceneLayers =
            new List<PresentationLayer>();
        private readonly SceneLayerBatchTracker _sceneLayerBatch =
            new SceneLayerBatchTracker();
        private readonly List<string> _history = new List<string>();
        private readonly List<HistoryVoiceCue> _historyVoices = new List<HistoryVoiceCue>();
        private readonly HashSet<short> _reportedOperations = new HashSet<short>();
        private UnityAssetLoader _assets;
        private UnityAudioService _audio;
        private HigurashiUserSettings _settings;
        private string _streamingAssetsRoot;
        private string _backgroundName;
        private Texture2D _backgroundTexture;
        private Texture2D _previousBackgroundTexture;
        private Texture2D _backgroundTransitionMask;
        private float _backgroundTransitionStartedAt;
        private float _backgroundTransitionDuration;
        private float _blockingAnimationUntil;
        private float _shakeStartedAt;
        private float _shakeSwingDuration;
        private float _shakeIntensity;
        private float _shakeAttenuation;
        private float _shakeDuration;
        private int _shakeVector;
        private float _dialogueRevealStartedAt;
        private int _dialogueRevealStartIndex;
        private bool _dialogueRevealForced;
        private float _windowTransitionStartedAt;
        private float _windowTransitionDuration;
        private float _windowTransitionFrom = 1f;
        private float _windowTransitionTo = 1f;
        private float _filmTransitionStartedAt;
        private float _filmTransitionDuration;
        private float _filmStrength;
        private float _filmTargetStrength;
        private int _messageSpeedOverride = -1;
        private Texture2D _fragmentTexture;
        private string _fragmentTextureName = string.Empty;
        private string _fragmentStyle = string.Empty;
        private Texture2D _windowBackgroundTexture;
        private string _windowBackgroundName = string.Empty;
        private HigurashiFragmentCatalog _fragmentCatalog = HigurashiFragmentCatalog.Empty;
        private HigurashiTipsCatalog _tipsCatalog = HigurashiTipsCatalog.Empty;
        private bool _fragmentChapterVisible;
        private bool _fragmentListVisible;
        private bool _tipsChapterVisible;
        private bool _tipsListVisible;
        private bool _tipsLibraryStandalone;
        private int _tipsScope;
        private int _tipsPage;
        private int _selectedTipId = -1;
        private bool _tipReturnRequested;
        private bool _tipReading;
        private int _tipsVisibleChapterOverride = -1;
        private Texture2D _tipsBackgroundTexture;
        private int _fragmentPage;
        private int _selectedFragmentId = -1;
        private float _fragmentStartedAt;
        private float _fragmentTransitionStartedAt;
        private float _fragmentTransitionDuration;
        private float _fragmentTransitionFrom;
        private float _fragmentTransitionTo;
        private bool _appendNext;
        private int _currentVoiceCharacter = -1;
        private int _lastVoiceChannel = -1;
        private int _lastVoiceCharacter = -1;
        private string _lastVoiceFilename = string.Empty;
        private float _lastVoiceVolume;
        private int _lastVoiceIssuedForDialogueSerial = -1;
        private float _creditsPageChangedAt;
        private bool _chapterPreviewAccepted;
        private BurikoMemory _memory;
        private VideoPlayer _videoPlayer;

        public string Speaker { get; private set; } = string.Empty;
        public string Dialogue { get; private set; } = string.Empty;
        public bool WindowVisible { get; private set; } = true;
        public bool TitleVisible { get; private set; }
        public bool CreditsVisible { get; private set; }
        public int CreditsPage { get; private set; }
        public bool ChapterPreviewVisible { get; private set; }
        public bool FragmentChapterVisible => _fragmentChapterVisible;
        public bool FragmentListVisible => _fragmentListVisible;
        public bool TipsChapterVisible => _tipsChapterVisible;
        public bool TipsListVisible => _tipsListVisible;
        public bool TipsLibraryStandalone => _tipsLibraryStandalone;
        public bool TipReading => _tipReading;
        public bool AppendNext => _appendNext;
        public int TipsPage => _tipsPage;
        public int SelectedTipId => _selectedTipId;
        public int CurrentChapterNumber => _memory == null ? 0 : Math.Max(0, _memory.GetLocalFlag("ChapterNumber"));
        public Texture2D TipsBackgroundTexture => _tipsBackgroundTexture;
        public int FragmentPage => _fragmentPage;
        public int SelectedFragmentId => _selectedFragmentId;
        public bool GameplayUiVisible { get; private set; }
        public bool SavingEnabled { get; private set; } = true;
        public bool InterfaceEnabled { get; private set; } = true;
        public bool HistoryVisible { get; set; }
        public bool ChoiceVisible => Choices.Count > 0;
        public List<string> Choices { get; } = new List<string>();
        public int DialogueSerial { get; private set; }
        public Texture2D BackgroundTexture => _backgroundTexture;
        public Texture2D PreviousBackgroundTexture => _previousBackgroundTexture;
        public Texture2D BackgroundTransitionMask => _backgroundTransitionMask;
        public float BackgroundTransitionProgress
        {
            get
            {
                if (_backgroundTransitionDuration <= 0f)
                {
                    return 1f;
                }
                var progress = Mathf.Clamp01((Time.unscaledTime - _backgroundTransitionStartedAt) /
                                             _backgroundTransitionDuration);
                return progress >= 1f ? 1f : 1f - Mathf.Pow(2f, -10f * progress);
            }
        }
        public float NegativeFilmStrength
        {
            get
            {
                if (_filmTransitionDuration <= 0f)
                {
                    return Mathf.Clamp01(_filmTargetStrength);
                }
                var progress = Mathf.Clamp01((Time.unscaledTime - _filmTransitionStartedAt) /
                                             _filmTransitionDuration);
                return Mathf.Lerp(_filmStrength, _filmTargetStrength, progress);
            }
        }
        public Texture MovieTexture => _videoPlayer != null ? _videoPlayer.texture : null;
        public bool MovieVisible { get; private set; }
        public IReadOnlyDictionary<int, PresentationLayer> Layers => _layers;
        public IReadOnlyList<PresentationLayer> PreviousSceneLayers => _previousSceneLayers;
        public IReadOnlyList<string> History => _history;
        public IReadOnlyList<RuntimePathCascade> ArtSets => _artSets;
        public IReadOnlyList<RuntimePathCascade> SpriteSets => _spriteSets;
        public IReadOnlyList<RuntimePathCascade> BackgroundSets => _backgroundSets;
        public IReadOnlyList<RuntimeAudioSet> AudioSets => _audioSets;
        public int FontSize { get; private set; } = 30;
        public int WindowX { get; private set; }
        public int WindowY { get; private set; }
        public int WindowWidth { get; private set; } = 1200;
        public int WindowHeight { get; private set; } = 250;
        public string ScreenAspect { get; private set; } = "1.7777778";
        public event Action MovieFinished;

        public bool IsVoicePlaying => _audio != null && _audio.AnyVoicePlaying();
        public bool HasBlockingAnimation => _blockingAnimationUntil > 0f;
        public Vector2 PresentationOffset
        {
            get
            {
                if (_shakeDuration <= 0f)
                {
                    return Vector2.zero;
                }
                var elapsed = Time.unscaledTime - _shakeStartedAt;
                if (elapsed < 0f || elapsed >= _shakeDuration)
                {
                    return Vector2.zero;
                }
                var swing = Mathf.Max(0.01f, _shakeSwingDuration);
                var index = Mathf.FloorToInt(elapsed / swing);
                var phase = Mathf.Clamp01((elapsed - index * swing) / swing);
                var eased = -(Mathf.Cos(Mathf.PI * phase) - 1f) * 0.5f;
                var fromSign = index == 0 ? 0f : ((index & 1) == 0 ? -1f : 1f);
                var toSign = (index & 1) == 0 ? 1f : -1f;
                var sign = Mathf.Lerp(fromSign, toSign, eased);
                var intensity = _shakeIntensity * Mathf.Pow(1f - _shakeAttenuation,
                    Mathf.Max(0, index));
                switch (_shakeVector)
                {
                    case 0: return new Vector2(intensity * sign, 0f);
                    case 1: return new Vector2(intensity * sign, -intensity * sign);
                    case 2: return new Vector2(0f, intensity * sign);
                    case 3: return new Vector2(-intensity * sign, intensity * sign);
                    default:
                        return new Vector2(Mathf.Sin(elapsed * 71f), Mathf.Cos(elapsed * 53f)) * intensity;
                }
            }
        }
        public bool IsOpeningChoice => OpeningChoicePolicy.IsOpeningChoice(Dialogue, Choices);
        public bool IsConsoleChoiceMenu => ConsoleChoiceMenuPolicy.IsConsoleChoiceMenu(Dialogue, Choices);
        public bool IsDialogueRevealComplete => VisibleDialogueLength >= Dialogue.Length;
        public string VisibleDialogue => Dialogue.Substring(0, VisibleDialogueLength);
        public float WindowOpacity
        {
            get
            {
                if (_windowTransitionDuration <= 0f)
                {
                    return WindowVisible ? _windowTransitionTo : 0f;
                }
                var progress = Mathf.Clamp01(
                    (Time.unscaledTime - _windowTransitionStartedAt) / _windowTransitionDuration);
                return Mathf.Lerp(_windowTransitionFrom, _windowTransitionTo,
                    -(Mathf.Cos(Mathf.PI * progress) - 1f) * 0.5f);
            }
        }
        public Color DialogueColor
        {
            get
            {
                var value = _memory == null ? 0xFFFFFF : _memory.GetLocalFlag("LTextColor");
                return new Color32(
                    (byte)((value >> 16) & 0xFF),
                    (byte)((value >> 8) & 0xFF),
                    (byte)(value & 0xFF),
                    0xFF);
            }
        }
        public Texture2D FragmentTexture => _fragmentTexture;
        public Texture2D WindowBackgroundTexture => _windowBackgroundTexture;
        public Texture2D GetInterfaceTexture(string textureName)
        {
            return _memory == null ? null : LoadTexture(textureName, _memory);
        }
        public string FragmentStyle => _fragmentStyle;
        public float FragmentAnimationTime => Mathf.Max(0f, Time.unscaledTime - _fragmentStartedAt);
        public float FragmentOpacity
        {
            get
            {
                if (_fragmentTexture == null)
                {
                    return 0f;
                }
                if (_fragmentTransitionDuration <= 0f)
                {
                    return _fragmentTransitionTo;
                }
                var progress = Mathf.Clamp01(
                    (Time.unscaledTime - _fragmentTransitionStartedAt) / _fragmentTransitionDuration);
                return Mathf.Lerp(_fragmentTransitionFrom, _fragmentTransitionTo,
                    -(Mathf.Cos(Mathf.PI * progress) - 1f) * 0.5f);
            }
        }

        private int VisibleDialogueLength
        {
            get
            {
                if (_dialogueRevealForced || string.IsNullOrEmpty(Dialogue))
                {
                    return Dialogue.Length;
                }
                var speed = _settings == null ? 50 : _settings.textSpeed;
                var charactersPerSecond = MessageSpeedPolicy.CharactersPerSecond(
                    speed, _messageSpeedOverride);
                var animated = Mathf.FloorToInt((Time.unscaledTime - _dialogueRevealStartedAt) * charactersPerSecond);
                return Mathf.Clamp(_dialogueRevealStartIndex + animated, 0, Dialogue.Length);
            }
        }

        public void Initialize(string installedGameDataRoot, HigurashiUserSettings settings)
        {
            _settings = settings ?? new HigurashiUserSettings();
            _streamingAssetsRoot = Path.Combine(installedGameDataRoot, "StreamingAssets");
            _assets = new UnityAssetLoader(installedGameDataRoot);
            _fragmentCatalog = HigurashiFragmentCatalog.Load(_streamingAssetsRoot);
            _tipsCatalog = HigurashiTipsCatalog.Load(installedGameDataRoot);
            _audio = gameObject.AddComponent<UnityAudioService>();
            _audio.Initialize(installedGameDataRoot, this);
            ApplyAudioSettings();
            _videoPlayer = gameObject.AddComponent<VideoPlayer>();
            _videoPlayer.playOnAwake = false;
            _videoPlayer.isLooping = false;
            _videoPlayer.renderMode = VideoRenderMode.APIOnly;
            _videoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;
            _videoPlayer.loopPointReached += OnMovieEnded;
            _videoPlayer.errorReceived += OnMovieError;
        }

        private void Update()
        {
            UpdateWindowTransition();
            UpdateFragmentTransition();
            UpdateLipSync();
        }

        public bool ConsumeCompletedBlockingAnimation()
        {
            if (_blockingAnimationUntil <= 0f || Time.unscaledTime < _blockingAnimationUntil)
            {
                return false;
            }

            _blockingAnimationUntil = 0f;
            _previousSceneLayers.Clear();
            if (_shakeDuration > 0f && Time.unscaledTime - _shakeStartedAt >= _shakeDuration)
            {
                _shakeDuration = 0f;
            }
            return true;
        }

        public bool SkipBlockingAnimation()
        {
            if (_blockingAnimationUntil <= 0f)
            {
                return false;
            }

            CompletePresentationAnimations();
            _blockingAnimationUntil = 0f;
            return true;
        }

        public void ApplySettings(BurikoMemory memory)
        {
            if (_settings == null)
            {
                return;
            }

            RebuildVisualStyleCatalog();
            var spriteIndex = ClampIndex(_settings.spriteStyleIndex, _spriteSets.Count);
            var backgroundIndex = ClampIndex(_settings.backgroundStyleIndex, _backgroundSets.Count);
            _settings.spriteStyleIndex = spriteIndex;
            _settings.backgroundStyleIndex = backgroundIndex;
            _settings.artSetIndex = spriteIndex;
            memory.SetGlobalFlag("GArtStyle", spriteIndex);
            memory.SetGlobalFlag("GLipSync", _settings.lipSync ? 1 : 0);
            memory.SetGlobalFlag("GCensor", _settings.censorshipLevel);

            if (_audioSets.Count > 0)
            {
                var audioIndex = ClampIndex(_settings.audioPresetIndex, _audioSets.Count);
                _settings.audioPresetIndex = audioIndex;
                var audioSet = _audioSets[audioIndex];
                memory.SetGlobalFlag("GAudioSet", audioIndex + 1);
                memory.SetGlobalFlag("GAltBGM", audioSet.AltBgm);
                memory.SetGlobalFlag("GAltBGMflow", audioSet.AltBgmFlow);
                memory.SetGlobalFlag("GAltSE", audioSet.AltSe);
                memory.SetGlobalFlag("GAltSEflow", audioSet.AltSeFlow);
            }

            ReloadVisualAssets(memory);
            ApplyAudioSettings();
        }

        public void ApplyAudioSettings()
        {
            _audio?.SetBgmMasterVolume((_settings?.bgmVolume ?? 100) / 100f);
        }

        public bool ReplayHistoryVoice(int index)
        {
            if (index < 0 || index >= _historyVoices.Count)
            {
                return false;
            }

            var cue = _historyVoices[index];
            if (!cue.IsPlayable || _audio == null || _memory == null)
            {
                return false;
            }

            _audio.StopAllVoices();
            _currentVoiceCharacter = cue.Character;
            _audio.PlayVoice(cue.Channel, cue.Filename, cue.Volume, _memory);
            return true;
        }

        public BurikoHostResponse Execute(BurikoOperationInvocation invocation, BurikoMemory memory)
        {
            _memory = memory;
            switch (invocation.Specification.Code)
            {
                case 13:
                    SavingEnabled = !_tipReading && invocation.Arguments[0].AsBool(memory);
                    return BurikoHostResponse.Continue;
                case 85:
                    InterfaceEnabled = invocation.Arguments[0].AsBool(memory);
                    return BurikoHostResponse.Continue;
                case 15:
                case 19:
                case 22:
                case 23:
                case 36:
                case 37:
                case 38:
                case 39:
                case 40:
                case 41:
                case 42:
                case 43:
                case 44:
                case 68:
                case 70:
                case 71:
                case 72:
                case 73:
                case 74:
                case 75:
                case 76:
                case 78:
                case 81:
                case 83:
                case 84:
                case 106:
                case 107:
                case 110:
                case 111:
                case 112:
                case 113:
                case 114:
                case 121:
                case 122:
                case 123:
                case 124:
                case 125:
                case 126:
                case 132:
                case 133:
                case 134:
                case 136:
                case 137:
                case 140:
                case 141:
                case 147:
                case 148:
                case 149:
                case 155:
                    ReportApproximated(invocation);
                    return BurikoHostResponse.Continue;
                case 20:
                    _messageSpeedOverride = MessageSpeedPolicy.ScriptOverride(
                        invocation.Arguments[0].AsBool(memory), Int(invocation, 1, memory));
                    return BurikoHostResponse.Continue;
                case 61:
                    CommitPendingPresentation();
                    return BurikoHostResponse.Continue;
                case 16:
                    CommitPendingPresentation();
                    return SetDialogue(
                        Text(invocation, 0, memory),
                        Text(invocation, 1, memory),
                        Text(invocation, 2, memory),
                        Text(invocation, 3, memory),
                        Int(invocation, 4, memory));
                case 17:
                    CommitPendingPresentation();
                    return SetDialogue(
                        Text(invocation, 0, memory),
                        Text(invocation, 1, memory),
                        string.Empty,
                        string.Empty,
                        Int(invocation, 2, memory));
                case 18:
                    Speaker = string.Empty;
                    Dialogue = string.Empty;
                    return BurikoHostResponse.Continue;
                case 21:
                    StartWindowTransition(false, 0.5f);
                    return AnimationResponse(0.5f, true);
                case 151:
                    StartWindowTransition(true, 0.5f);
                    return AnimationResponse(0.5f, true);
                case 153:
                {
                    var duration = Int(invocation, 0, memory) / 1000f;
                    StartWindowTransition(false, duration);
                    return AnimationResponse(duration, true);
                }
                case 154:
                {
                    var red = Mathf.Clamp(Int(invocation, 1, memory), 0, 255);
                    var green = Mathf.Clamp(Int(invocation, 2, memory), 0, 255);
                    var blue = Mathf.Clamp(Int(invocation, 3, memory), 0, 255);
                    memory.SetLocalFlag("LTextColor", (red << 16) | (green << 8) | blue);
                    return BurikoHostResponse.Continue;
                }
                case 156:
                {
                    var exclusiveMaximum = Math.Max(1, Int(invocation, 0, memory) - 1);
                    return new BurikoHostResponse(
                        BurikoValue.FromInt(UnityEngine.Random.Range(0, exclusiveMaximum)));
                }
                case 157:
                {
                    var duration = Int(invocation, 2, memory) / 1000f;
                    StartFragment(Text(invocation, 0, memory), Text(invocation, 1, memory),
                        duration, memory);
                    return AnimationResponse(duration, true);
                }
                case 158:
                    StopFragment(Int(invocation, 0, memory) / 1000f);
                    return BurikoHostResponse.Continue;
                case 159:
                    DrawFixedSizeSprite(invocation, memory, false);
                    _sceneLayerBatch.Prepare(Int(invocation, 0, memory));
                    return AnimationResponse(Int(invocation, 16, memory) / 1000f,
                        invocation.Arguments[17].AsBool(memory));
                case 160:
                    DrawFixedSizeSprite(invocation, memory, true);
                    _sceneLayerBatch.Prepare(Int(invocation, 0, memory));
                    return AnimationResponse(Int(invocation, 13, memory) / 1000f,
                        invocation.Arguments[14].AsBool(memory));
                case 161:
                    return BurikoHostResponse.Continue;
                case 163:
                    _fragmentChapterVisible = true;
                    _fragmentListVisible = false;
                    _selectedFragmentId = -1;
                    GameplayUiVisible = false;
                    SetWindowVisibilityImmediate(false);
                    return new BurikoHostResponse(BurikoValue.Null, BurikoBlockReason.Host);
                case 164:
                    _fragmentChapterVisible = false;
                    _fragmentListVisible = true;
                    _fragmentPage = Mathf.Max(0, memory.GetLocalFlag("LFragmentPage"));
                    _selectedFragmentId = -1;
                    GameplayUiVisible = false;
                    SetWindowVisibilityImmediate(false);
                    return new BurikoHostResponse(BurikoValue.Null, BurikoBlockReason.Host);
                case 165:
                    _windowBackgroundName = Text(invocation, 0, memory);
                    _windowBackgroundTexture = string.IsNullOrWhiteSpace(_windowBackgroundName)
                        ? null
                        : LoadBackgroundTexture(_windowBackgroundName, memory);
                    return BurikoHostResponse.Continue;
                case 24:
                    ShowChoices(invocation, memory);
                    return new BurikoHostResponse(BurikoValue.Null, BurikoBlockReason.Choice);
                case 25:
                    PlayBgm(invocation, memory, false);
                    return BurikoHostResponse.Continue;
                case 26:
                    _audio.StopBgm(Int(invocation, 0, memory));
                    return BurikoHostResponse.Continue;
                case 27:
                    _audio.SetBgmVolume(
                        Int(invocation, 0, memory),
                        Int(invocation, 1, memory) / 128f);
                    return BurikoHostResponse.Continue;
                case 28:
                    _audio.StopBgm(Int(invocation, 0, memory));
                    return BurikoHostResponse.Continue;
                case 29:
                    for (var channel = Int(invocation, 0, memory);
                         channel <= Int(invocation, 1, memory);
                         channel++)
                    {
                        _audio.StopBgm(channel);
                    }
                    return BurikoHostResponse.Continue;
                case 30:
                    _audio.PlaySe(
                        Int(invocation, 0, memory),
                        AddOgg(Text(invocation, 1, memory)),
                        Int(invocation, 2, memory) / 128f,
                        memory);
                    return BurikoHostResponse.Continue;
                case 31:
                case 32:
                    _audio.StopSe(Int(invocation, 0, memory));
                    return BurikoHostResponse.Continue;
                case 33:
                case 35:
                    return BurikoValueResponse(_audio.IsChannelPlaying(
                        invocation.Specification.Code == 33 ? RuntimeAudioKind.Se : RuntimeAudioKind.Voice,
                        Int(invocation, 0, memory)));
                case 34:
                    PlayTrackedVoice(
                        Int(invocation, 0, memory),
                        -1,
                        AddOgg(Text(invocation, 1, memory)),
                        VoiceVolume(Int(invocation, 2, memory) / 128f),
                        memory);
                    return BurikoHostResponse.Continue;
                case 45:
                {
                    var swing = Mathf.Max(0.01f, Int(invocation, 2, memory) / 1000f * 2f);
                    var loops = Math.Max(0, Int(invocation, 3, memory));
                    var duration = loops == 0 ? 0f : (swing + 0.005f) * loops;
                    StartScreenShake(Int(invocation, 0, memory), Int(invocation, 1, memory),
                        Int(invocation, 4, memory), swing, duration);
                    return AnimationResponse(duration, loops != 0);
                }
                case 46:
                {
                    const float swing = 1f;
                    const int loops = 30;
                    var duration = (swing + 0.005f) * loops;
                    StartScreenShake(Int(invocation, 0, memory), Int(invocation, 1, memory),
                        5, swing, duration);
                    return AnimationResponse(duration, true);
                }
                case 47:
                {
                    var duration = Int(invocation, 1, memory) / 1000f;
                    SetBackground(Text(invocation, 0, memory), memory, false, duration);
                    return AnimationResponse(duration, invocation.Arguments[2].AsBool(memory));
                }
                case 50:
                {
                    var duration = Int(invocation, 1, memory) / 1000f;
                    SetBackground(Text(invocation, 0, memory), memory, true, duration);
                    return AnimationResponse(duration, true);
                }
                case 51:
                    if (string.Equals(Text(invocation, 0, memory), "black", StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(_backgroundName, "07th-mod", StringComparison.OrdinalIgnoreCase))
                    {
                        var creditsBackground = "haikei";
                        if (HigurashiActiveChapter.Profile.EpisodeNumber == 8)
                        {
                            creditsBackground = memory.GetGlobalFlag("GFlag_GameClear") == 0
                                ? "background/moon"
                                : "background/jt1";
                        }
                        SetBackground(creditsBackground, memory, true, 1f);
                        CreditsVisible = true;
                        CreditsPage = 1;
                        _creditsPageChangedAt = Time.unscaledTime;
                        SetWindowVisibilityImmediate(false);
                        return new BurikoHostResponse(BurikoValue.Null, BurikoBlockReason.Host);
                    }
                {
                    var duration = Int(invocation, 4, memory) / 1000f;
                    SetBackground(Text(invocation, 0, memory), memory, true, duration,
                        Text(invocation, 1, memory));
                    return AnimationResponse(duration, true);
                }
                case 52:
                {
                    var duration = Int(invocation, 2, memory) / 1000f;
                    SetBackground(Text(invocation, 0, memory), memory, true, duration);
                    return AnimationResponse(duration, true);
                }
                case 48:
                {
                    var duration = Int(invocation, 0, memory) / 1000f;
                    SetBackground("black", memory, false, duration);
                    return AnimationResponse(duration, invocation.Arguments[1].AsBool(memory));
                }
                case 53:
                {
                    var duration = Int(invocation, 0, memory) / 1000f;
                    SetBackground("black", memory, true, duration);
                    return AnimationResponse(duration, true);
                }
                case 54:
                {
                    var duration = Int(invocation, 3, memory) / 1000f;
                    SetBackground("black", memory, true, duration, Text(invocation, 0, memory));
                    return AnimationResponse(duration, true);
                }
                case 49:
                {
                    var duration = Int(invocation, 3, memory) / 1000f;
                    SetBackground(Text(invocation, 0, memory), memory, false, duration,
                        Text(invocation, 1, memory));
                    return AnimationResponse(duration, invocation.Arguments[4].AsBool(memory));
                }
                case 55:
                    DrawAnimatedLayer(invocation, memory, true, 0, 1, 2, 3, 4, 5, 6, 7, 8, 13, 14);
                    _sceneLayerBatch.Prepare(Int(invocation, 0, memory));
                    return AnimationResponse(Int(invocation, 14, memory) / 1000f,
                        invocation.Arguments[15].AsBool(memory));
                case 56:
                    MoveBustshot(invocation, memory);
                    _sceneLayerBatch.Prepare(Int(invocation, 0, memory));
                    return AnimationResponse(Int(invocation, 6, memory) / 1000f,
                        invocation.Arguments[7].AsBool(memory));
                case 57:
                    FadeBustshot(invocation, memory);
                    _sceneLayerBatch.Discard(Int(invocation, 0, memory));
                    return AnimationResponse(Int(invocation, 6, memory) / 1000f,
                        invocation.Arguments[7].AsBool(memory));
                case 64:
                    FadeLayer(Int(invocation, 0, memory), Int(invocation, 1, memory) / 1000f);
                    _sceneLayerBatch.Discard(Int(invocation, 0, memory));
                    return AnimationResponse(Int(invocation, 1, memory) / 1000f,
                        invocation.Arguments[2].AsBool(memory));
                case 65:
                    FadeLayerWithMask(Int(invocation, 0, memory), Text(invocation, 1, memory),
                        Int(invocation, 2, memory), Int(invocation, 3, memory) / 1000f, memory);
                    _sceneLayerBatch.Discard(Int(invocation, 0, memory));
                    return AnimationResponse(Int(invocation, 3, memory) / 1000f,
                        invocation.Arguments[4].AsBool(memory));
                case 58:
                    DrawAnimatedLayer(invocation, memory, true, 0, 1, 4, 5, 10, 6, 7, 8, 9, 12, 13);
                    SetLayerMask(Int(invocation, 0, memory), Text(invocation, 2, memory),
                        0, false, memory);
                    _sceneLayerBatch.Prepare(Int(invocation, 0, memory));
                    return AnimationResponse(Int(invocation, 13, memory) / 1000f,
                        invocation.Arguments[14].AsBool(memory));
                case 152:
                    ChangeBustshot(invocation, memory);
                    _sceneLayerBatch.Prepare(Int(invocation, 0, memory));
                    return AnimationResponse(Int(invocation, 2, memory) / 1000f,
                        invocation.Arguments[3].AsBool(memory));
                case 59:
                    DrawLayer(1000, Text(invocation, 0, memory), 213, 131, 0, 1000, memory,
                        false, 1f, Int(invocation, 1, memory) / 1000f);
                    _sceneLayerBatch.Prepare(1000);
                    return BurikoHostResponse.Continue;
                case 60:
                    FadeLayer(1000, Int(invocation, 0, memory) / 1000f);
                    _sceneLayerBatch.Discard(1000);
                    return BurikoHostResponse.Continue;
                case 62:
                    DrawLayer(
                        Int(invocation, 0, memory),
                        Text(invocation, 1, memory),
                        Int(invocation, 3, memory),
                        Int(invocation, 4, memory),
                        Int(invocation, 5, memory),
                        Int(invocation, 13, memory),
                        memory,
                        false,
                        1f - Int(invocation, 12, memory) / 256f,
                        Int(invocation, 14, memory) / 1000f);
                    _sceneLayerBatch.Prepare(Int(invocation, 0, memory));
                    return AnimationResponse(Int(invocation, 14, memory) / 1000f,
                        invocation.Arguments[15].AsBool(memory));
                case 63:
                    DrawLayer(
                        Int(invocation, 0, memory),
                        Text(invocation, 1, memory),
                        Int(invocation, 4, memory),
                        Int(invocation, 5, memory),
                        0,
                        Int(invocation, 10, memory),
                        memory,
                        false,
                        1f,
                        Int(invocation, 11, memory) / 1000f);
                    SetLayerMask(Int(invocation, 0, memory), Text(invocation, 2, memory),
                        Int(invocation, 3, memory), false, memory);
                    _sceneLayerBatch.Prepare(Int(invocation, 0, memory));
                    return AnimationResponse(Int(invocation, 11, memory) / 1000f,
                        invocation.Arguments[12].AsBool(memory));
                case 66:
                    MoveLayer(invocation, memory);
                    _sceneLayerBatch.Prepare(Int(invocation, 0, memory));
                    return AnimationResponse(Int(invocation, 8, memory) / 1000f,
                        invocation.Arguments[9].AsBool(memory));
                case 67:
                    ReportApproximated(invocation);
                    return BurikoHostResponse.Continue;
                case 69:
                    ReportApproximated(invocation);
                    return BurikoHostResponse.Continue;
                case 77:
                {
                    var duration = Int(invocation, 0, memory) / 1000f;
                    StartFilmTransition(0f, duration);
                    return AnimationResponse(duration, invocation.Arguments[1].AsBool(memory));
                }
                case 79:
                    FadeLayerRange(1, 19, Int(invocation, 0, memory) / 1000f);
                    DiscardPreparedLayerRange(1, 19);
                    return BurikoHostResponse.Continue;
                case 82:
                {
                    var duration = Int(invocation, 0, memory) / 1000f;
                    StartFilmTransition(255f / 256f, duration);
                    return AnimationResponse(duration, invocation.Arguments[1].AsBool(memory));
                }
                case 80:
                    FadeLayerWithMask(Int(invocation, 0, memory), Text(invocation, 1, memory),
                        Int(invocation, 2, memory), Int(invocation, 6, memory) / 1000f, memory);
                    _sceneLayerBatch.Discard(Int(invocation, 0, memory));
                    return AnimationResponse(Int(invocation, 6, memory) / 1000f,
                        invocation.Arguments[7].AsBool(memory));
                case 98:
                    FadeLayerRange(2, 3, Int(invocation, 0, memory) / 1000f);
                    DiscardPreparedLayerRange(2, 3);
                    return BurikoHostResponse.Continue;
                case 99:
                    FadeLayerRange(5, 8, Int(invocation, 0, memory) / 1000f);
                    DiscardPreparedLayerRange(5, 8);
                    return BurikoHostResponse.Continue;
                case 89:
                    if (_tipReading)
                    {
                        // Content scripts return into flow.txt. Some flows immediately
                        // request the next chapter preview before yielding, so signal
                        // the runtime here rather than rendering that preview for a frame.
                        _tipReturnRequested = true;
                        return new BurikoHostResponse(BurikoValue.Null, BurikoBlockReason.Host);
                    }
                    ChapterPreviewVisible = true;
                    _chapterPreviewAccepted = false;
                    _fragmentChapterVisible = false;
                    _fragmentListVisible = false;
                    _tipsChapterVisible = false;
                    _tipsListVisible = false;
                    _tipsLibraryStandalone = false;
                    _selectedFragmentId = -1;
                    GameplayUiVisible = false;
                    SetWindowVisibilityImmediate(false);
                    return new BurikoHostResponse(BurikoValue.Null, BurikoBlockReason.Host);
                case 86:
                    _tipReading = false;
                    _tipsVisibleChapterOverride = -1;
                    _tipsBackgroundTexture = LoadBackgroundTexture("ex_tips", memory);
                    _tipsChapterVisible = false;
                    _tipsListVisible = true;
                    _tipsLibraryStandalone = false;
                    _tipsScope = Mathf.Clamp(Int(invocation, 0, memory), 0, 2);
                    _tipsPage = 0;
                    _selectedTipId = -1;
                    GameplayUiVisible = false;
                    SetWindowVisibilityImmediate(false);
                    return new BurikoHostResponse(BurikoValue.Null, BurikoBlockReason.Host);
                case 87:
                    _tipReading = false;
                    _tipsVisibleChapterOverride = -1;
                    _tipsChapterVisible = true;
                    _tipsListVisible = false;
                    _tipsLibraryStandalone = false;
                    _tipsScope = 0;
                    _tipsPage = 0;
                    _selectedTipId = -1;
                    GameplayUiVisible = false;
                    SetWindowVisibilityImmediate(false);
                    return new BurikoHostResponse(BurikoValue.Null, BurikoBlockReason.Host);
                case 101:
                    if (_tipReading)
                    {
                        _tipReturnRequested = true;
                        return new BurikoHostResponse(BurikoValue.Null, BurikoBlockReason.Host);
                    }
                    TitleVisible = true;
                    ChapterPreviewVisible = false;
                    _fragmentChapterVisible = false;
                    _fragmentListVisible = false;
                    _tipsChapterVisible = false;
                    _tipsListVisible = false;
                    _tipsLibraryStandalone = false;
                    _selectedFragmentId = -1;
                    GameplayUiVisible = false;
                    SetWindowVisibilityImmediate(false);
                    return new BurikoHostResponse(BurikoValue.Null, BurikoBlockReason.Host);
                case 109:
                    memory.SetGlobalFlag("GLanguage", 0);
                    return BurikoHostResponse.Continue;
                case 115:
                    FontSize = Math.Max(12, Int(invocation, 0, memory));
                    return BurikoHostResponse.Continue;
                case 116:
                    WindowX = Int(invocation, 0, memory);
                    WindowY = Int(invocation, 1, memory);
                    return BurikoHostResponse.Continue;
                case 117:
                    WindowWidth = Int(invocation, 0, memory);
                    WindowHeight = Int(invocation, 1, memory);
                    return BurikoHostResponse.Continue;
                case 118:
                    return BurikoHostResponse.Continue;
                case 119:
                    return BurikoHostResponse.Continue;
                case 120:
                    ScreenAspect = Text(invocation, 0, memory);
                    return BurikoHostResponse.Continue;
                case 127:
                    ReportApproximated(invocation);
                    return BurikoHostResponse.Continue;
                case 128:
                    DrawModCharacter(invocation, memory, false);
                    _sceneLayerBatch.Prepare(Int(invocation, 0, memory));
                    return AnimationResponse(Int(invocation, 16, memory) / 1000f,
                        invocation.Arguments[17].AsBool(memory));
                case 129:
                    DrawModCharacter(invocation, memory, true);
                    SetLayerMask(Int(invocation, 0, memory), Text(invocation, 4, memory),
                        0, false, memory);
                    _sceneLayerBatch.Prepare(Int(invocation, 0, memory));
                    return AnimationResponse(Int(invocation, 15, memory) / 1000f,
                        invocation.Arguments[16].AsBool(memory));
                case 130:
                    PlayTrackedVoice(
                        Int(invocation, 0, memory),
                        Int(invocation, 1, memory),
                        AddOgg(Text(invocation, 2, memory)),
                        VoiceVolume(Int(invocation, 3, memory) / 128f),
                        memory);
                    return BurikoHostResponse.Continue;
                case 131:
                    return StartMovie(Text(invocation, 0, memory));
                case 135:
                    return BurikoValueResponse(memory.GetGlobalFlag(
                        "GHighestChapter" + Int(invocation, 0, memory)));
                case 138:
                    AddCascade(_artSets, invocation, memory);
                    RebuildVisualStyleCatalog();
                    return BurikoHostResponse.Continue;
                case 139:
                    _artSets.Clear();
                    _spriteSets.Clear();
                    _backgroundSets.Clear();
                    return BurikoHostResponse.Continue;
                case 142:
                    PlayBgm(invocation, memory, true);
                    return BurikoHostResponse.Continue;
                case 143:
                    if (memory.GetGlobalFlag("GAltBGMflow") == Int(invocation, 3, memory))
                    {
                        _audio.StopBgm(Int(invocation, 0, memory));
                    }
                    return BurikoHostResponse.Continue;
                case 144:
                    AddCascade(_bgmSets, invocation, memory);
                    return BurikoHostResponse.Continue;
                case 145:
                    AddCascade(_seSets, invocation, memory);
                    return BurikoHostResponse.Continue;
                case 146:
                    _audioSets.Add(new RuntimeAudioSet(
                        Text(invocation, 0, memory),
                        Text(invocation, 2, memory),
                        Int(invocation, 4, memory),
                        Int(invocation, 5, memory),
                        Int(invocation, 6, memory),
                        Int(invocation, 7, memory)));
                    return BurikoHostResponse.Continue;
                default:
                    ReportApproximated(invocation);
                    return BurikoHostResponse.Continue;
            }
        }

        public bool StartFromTitle(BurikoMemory memory)
        {
            if (!TitleVisible)
            {
                return false;
            }

            memory.SetLocalFlag("LOCALWORK_NO_RESULT", 0);
            CommitPendingPresentation();
            TitleVisible = false;
            ChapterPreviewVisible = false;
            _fragmentChapterVisible = false;
            _fragmentListVisible = false;
            _tipsChapterVisible = false;
            _tipsListVisible = false;
            _tipsLibraryStandalone = false;
            _selectedFragmentId = -1;
            _tipReading = false;
            _chapterPreviewAccepted = false;
            GameplayUiVisible = false;
            SetWindowVisibilityImmediate(false);
            return true;
        }

        public void PrepareStoryContinuation()
        {
            TitleVisible = false;
            CreditsVisible = false;
            CreditsPage = 0;
            ChapterPreviewVisible = false;
            _fragmentChapterVisible = false;
            _fragmentListVisible = false;
            _tipsChapterVisible = false;
            _tipsListVisible = false;
            _tipsLibraryStandalone = false;
            _tipReading = false;
            _selectedFragmentId = -1;
            _selectedTipId = -1;
            Choices.Clear();
            HistoryVisible = false;
            GameplayUiVisible = true;
            SavingEnabled = true;
            InterfaceEnabled = true;
            SetWindowVisibilityImmediate(false);
        }

        public void PrepareForChapterJump()
        {
            CommitPendingPresentation();
            // A chapter jump leaves the title screen and starts a new script
            // scene. Do not let the title background or its transition state
            // remain visible until the first background command is executed.
            _backgroundName = string.Empty;
            _backgroundTexture = null;
            _previousBackgroundTexture = null;
            _backgroundTransitionMask = null;
            _backgroundTransitionStartedAt = Time.unscaledTime;
            _backgroundTransitionDuration = 0f;
            _previousSceneLayers.Clear();
            _layers.Clear();
            TitleVisible = false;
            ChapterPreviewVisible = false;
            _fragmentChapterVisible = false;
            _fragmentListVisible = false;
            _tipsChapterVisible = false;
            _tipsListVisible = false;
            _tipsLibraryStandalone = false;
            _tipReading = false;
            _selectedFragmentId = -1;
            _selectedTipId = -1;
            Choices.Clear();
            HistoryVisible = false;
            GameplayUiVisible = true;
            SavingEnabled = true;
            InterfaceEnabled = true;
            _chapterPreviewAccepted = true;
            _history.Clear();
            _historyVoices.Clear();
            SetWindowVisibilityImmediate(false);
        }

        public bool ResolveChapterPreview(bool start, BurikoMemory memory)
        {
            if (!ChapterPreviewVisible)
            {
                return false;
            }

            memory.SetLocalFlag("LOCALWORK_NO_RESULT", start ? 1 : 0);
            ChapterPreviewVisible = false;
            _chapterPreviewAccepted = start;
            SetWindowVisibilityImmediate(false);
            return true;
        }

        public bool ResolveFragmentChapterToList(BurikoMemory memory)
        {
            if (!_fragmentChapterVisible || memory == null)
            {
                return false;
            }

            memory.SetLocalFlag("TipsMode", 1);
            _fragmentChapterVisible = false;
            _fragmentListVisible = false;
            _selectedFragmentId = -1;
            SetWindowVisibilityImmediate(false);
            return true;
        }

        public bool ResolveTipsChapterToList(BurikoMemory memory, bool allUnlocked)
        {
            if (!_tipsChapterVisible || memory == null)
            {
                return false;
            }

            memory.SetLocalFlag("TipsMode", allUnlocked ? 4 : 3);
            memory.SetLocalFlag("LOCALWORK_NO_RESULT", 1);
            _tipsChapterVisible = false;
            _selectedTipId = -1;
            _tipReading = false;
            SavingEnabled = true;
            SetWindowVisibilityImmediate(false);
            return true;
        }

        public bool ContinuePastTips(BurikoMemory memory)
        {
            if (!_tipsChapterVisible || memory == null)
            {
                return false;
            }

            memory.SetLocalFlag("TipsMode", 0);
            memory.SetLocalFlag("LOCALWORK_NO_RESULT", 0);
            _tipsChapterVisible = false;
            _selectedTipId = -1;
            _tipReading = false;
            SavingEnabled = true;
            SetWindowVisibilityImmediate(false);
            return true;
        }

        public bool OpenTipsLibrary(BurikoMemory memory, int unlockedChapter)
        {
            if (memory == null || _tipsCatalog.IsEmpty ||
                !_tipsCatalog.HasVisibleThrough(Math.Max(0, unlockedChapter)))
            {
                return false;
            }

            _tipsChapterVisible = false;
            _tipsListVisible = true;
            _tipsLibraryStandalone = true;
            _tipsScope = 1;
            _tipsPage = 0;
            _selectedTipId = -1;
            _tipsVisibleChapterOverride = Math.Max(0, unlockedChapter);
            TitleVisible = false;
            _tipsBackgroundTexture = LoadBackgroundTexture("ex_tips", memory);
            GameplayUiVisible = true;
            InterfaceEnabled = true;
            HistoryVisible = false;
            SetWindowVisibilityImmediate(false);
            return true;
        }

        public bool ExitTips(BurikoMemory memory)
        {
            if (!_tipsListVisible || memory == null)
            {
                return false;
            }

            var standalone = _tipsLibraryStandalone;
            _tipsListVisible = false;
            _tipsLibraryStandalone = false;
            _selectedTipId = -1;
            if (!standalone)
            {
                // Closing the list in the original PC flow returns to the
                // four-button chapter screen. Clearing TipsMode here would
                // resume flow.txt and accidentally enter the next chapter.
                _tipsChapterVisible = true;
                _tipReading = false;
                SavingEnabled = true;
                _tipsVisibleChapterOverride = -1;
                GameplayUiVisible = false;
                SetWindowVisibilityImmediate(false);
            }
            else
            {
                _tipReading = false;
                SavingEnabled = true;
                TitleVisible = true;
                GameplayUiVisible = true;
                SetWindowVisibilityImmediate(true);
            }
            return true;
        }

        internal IReadOnlyList<string> GetChapterJumpSections()
        {
            var result = new List<string>();
            var episode = HigurashiActiveChapter.Profile.EpisodeNumber;
            var mappedCount = EpisodeChapterJumpMap.Count(episode);
            if (mappedCount > 0)
            {
                for (var i = 0; i < mappedCount; i++)
                {
                    result.Add(EpisodeChapterJumpMap.Token(episode, i));
                }
                return result;
            }

            var candidates = new[]
            {
                Path.Combine(_streamingAssetsRoot ?? string.Empty, "CompiledChineseScripts", "flow.mg"),
                Path.Combine(_streamingAssetsRoot ?? string.Empty, "CompiledUpdateScripts", "flow.mg"),
                Path.Combine(_streamingAssetsRoot ?? string.Empty, "CompiledScripts", "flow.mg")
            };

            for (var i = 0; i < candidates.Length; i++)
            {
                if (!File.Exists(candidates[i]))
                {
                    continue;
                }

                try
                {
                    var container = CompiledScriptContainer.ReadFile(candidates[i]);
                    foreach (var pair in container.Blocks)
                    {
                        if (pair.Key.StartsWith("Day", StringComparison.OrdinalIgnoreCase) &&
                            !result.Contains(pair.Key))
                        {
                            result.Add(pair.Key);
                        }
                    }
                    break;
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("Unable to read chapter jump sections: " + exception.Message);
                }
            }

            result.Sort(CompareChapterJumpSections);
            return result;
        }

        private static int CompareChapterJumpSections(string left, string right)
        {
            var leftNumber = GetChapterJumpNumber(left);
            var rightNumber = GetChapterJumpNumber(right);
            var result = leftNumber.CompareTo(rightNumber);
            return result != 0 ? result : string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
        }

        internal static int GetChapterJumpNumber(string section)
        {
            if (string.IsNullOrWhiteSpace(section) || section.Length <= 3)
            {
                return int.MaxValue;
            }

            var end = 3;
            while (end < section.Length && char.IsDigit(section[end]))
            {
                end++;
            }

            return int.TryParse(section.Substring(3, end - 3), out var number)
                ? number
                : int.MaxValue;
        }

        internal IReadOnlyList<HigurashiTipDefinition> GetVisibleTips(BurikoMemory memory)
        {
            return _tipsCatalog.GetVisible(memory, _tipsScope, _tipsVisibleChapterOverride);
        }

        internal bool HasUnlockedTips(BurikoMemory memory, int unlockedChapter)
        {
            return memory != null && !_tipsCatalog.IsEmpty &&
                   _tipsCatalog.HasVisibleThrough(Math.Max(0, unlockedChapter));
        }

        internal bool HasTipsForChapter(int chapter)
        {
            return _tipsCatalog.HasEntryAtChapter(Math.Max(0, chapter));
        }

        internal HigurashiTipDefinition GetSelectedTip()
        {
            return _tipsCatalog.Find(_selectedTipId);
        }

        internal Texture2D GetTipPreview(HigurashiTipDefinition entry, BurikoMemory memory)
        {
            return entry == null || string.IsNullOrWhiteSpace(entry.PreviewName)
                ? null
                : LoadTexture(entry.PreviewName, memory);
        }

        internal Texture2D GetTipPreview(HigurashiTipDefinition entry, BurikoMemory memory, bool selected)
        {
            if (entry == null)
            {
                return null;
            }

            var episode = HigurashiActiveChapter.Profile.EpisodeNumber;
            var sprite = (episode == 1 ? "tips" : "tips_") + entry.Id.ToString("000") +
                         (selected ? "_hover" : "_normal");
            var bundled = Resources.Load<Texture2D>(
                "TipsPreviews/ep" + episode.ToString("00") + "/" + sprite);
            if (bundled != null)
            {
                return bundled;
            }

            var name = selected && !string.IsNullOrWhiteSpace(entry.SelectedPreviewName)
                ? entry.SelectedPreviewName
                : entry.PreviewName;
            var texture = LoadTexture(name, memory);
            return texture ?? LoadTexture(entry.PreviewName, memory);
        }

        public bool SelectTip(int id, BurikoMemory memory)
        {
            if (!_tipsListVisible || memory == null)
            {
                return false;
            }
            var visible = _tipsCatalog.GetVisible(memory, _tipsScope, _tipsVisibleChapterOverride);
            for (var i = 0; i < visible.Count; i++)
            {
                if (visible[i].Id == id)
                {
                    _selectedTipId = id;
                    return true;
                }
            }
            return false;
        }

        public void ChangeTipsPage(int delta, BurikoMemory memory)
        {
            if (!_tipsListVisible || memory == null)
            {
                return;
            }
            var pageCount = Math.Max(1, Mathf.CeilToInt(
                _tipsCatalog.GetVisible(memory, _tipsScope, _tipsVisibleChapterOverride).Count / 8f));
            _tipsPage = Mathf.Clamp(_tipsPage + delta, 0, pageCount - 1);
            _selectedTipId = -1;
        }

        public bool TryStartSelectedTip(BurikoMemory memory, out string scriptName)
        {
            scriptName = string.Empty;
            if (!_tipsListVisible || memory == null)
            {
                return false;
            }
            var tip = GetSelectedTip();
            if (tip == null || string.IsNullOrWhiteSpace(tip.Script))
            {
                return false;
            }

            scriptName = tip.Script;
            _tipsListVisible = false;
            _selectedTipId = -1;
            _tipReturnRequested = false;
            _tipReading = true;
            SavingEnabled = false;
            GameplayUiVisible = true;
            InterfaceEnabled = true;
            HistoryVisible = false;
            if (!_tipsLibraryStandalone)
            {
                memory.SetLocalFlag("TipsMode", _tipsScope + 3);
                memory.SetLocalFlag("LOCALWORK_NO_RESULT", 1);
            }
            SetWindowVisibilityImmediate(false);
            return true;
        }

        public void ReopenTipsLibrary()
        {
            _tipsChapterVisible = false;
            _tipsListVisible = true;
            _tipsLibraryStandalone = true;
            _tipReading = false;
            _tipsScope = 1;
            _tipsPage = 0;
            _selectedTipId = -1;
            _tipReturnRequested = false;
            GameplayUiVisible = true;
            InterfaceEnabled = true;
            HistoryVisible = false;
            TitleVisible = false;
            SetWindowVisibilityImmediate(false);
        }

        public bool ExitFragmentList(BurikoMemory memory)
        {
            if (!_fragmentListVisible || memory == null)
            {
                return false;
            }

            memory.SetLocalFlag("LFragmentPage", _fragmentPage);
            memory.SetLocalFlag("TipsMode", 0);
            _fragmentListVisible = false;
            _selectedFragmentId = -1;
            SetWindowVisibilityImmediate(false);
            return true;
        }

        public bool ConsumeTipReturnRequest()
        {
            if (!_tipReturnRequested)
            {
                return false;
            }

            _tipReturnRequested = false;
            return true;
        }

        internal IReadOnlyList<HigurashiFragmentDefinition> GetVisibleFragments(BurikoMemory memory)
        {
            return _fragmentCatalog.GetVisible(memory);
        }

        internal HigurashiFragmentDefinition GetSelectedFragment()
        {
            return _fragmentCatalog.Find(_selectedFragmentId);
        }

        internal HigurashiFragmentViewState GetFragmentViewState(
            HigurashiFragmentDefinition entry,
            BurikoMemory memory)
        {
            return _fragmentCatalog.GetViewState(entry, memory);
        }

        internal bool AreFragmentPrerequisitesMet(
            HigurashiFragmentDefinition entry,
            BurikoMemory memory)
        {
            return _fragmentCatalog.ArePrerequisitesMet(entry, memory);
        }

        internal string FragmentPrerequisiteSummary(
            HigurashiFragmentDefinition entry,
            BurikoMemory memory)
        {
            return _fragmentCatalog.BuildPrerequisiteSummary(entry, memory);
        }

        public bool SelectFragment(int id, BurikoMemory memory)
        {
            if (!_fragmentListVisible || memory == null)
            {
                return false;
            }

            var visible = _fragmentCatalog.GetVisible(memory);
            for (var i = 0; i < visible.Count; i++)
            {
                if (visible[i].Id == id)
                {
                    _selectedFragmentId = id;
                    return true;
                }
            }
            return false;
        }

        public void ChangeFragmentPage(int delta, BurikoMemory memory)
        {
            if (!_fragmentListVisible || memory == null)
            {
                return;
            }

            var pageCount = Math.Max(1,
                Mathf.CeilToInt(_fragmentCatalog.GetVisible(memory).Count / 8f));
            _fragmentPage = Mathf.Clamp(_fragmentPage + delta, 0, pageCount - 1);
            memory.SetLocalFlag("LFragmentPage", _fragmentPage);
            _selectedFragmentId = -1;
        }

        public bool TryStartSelectedFragment(BurikoMemory memory, out string scriptName)
        {
            scriptName = string.Empty;
            if (!_fragmentListVisible || memory == null)
            {
                return false;
            }

            var entry = GetSelectedFragment();
            if (entry == null)
            {
                return false;
            }

            var available = _fragmentCatalog.ArePrerequisitesMet(entry, memory);
            if (available)
            {
                if (memory.GetLocalFlag(HigurashiFragmentCatalog.FragmentReadFlag(entry.Id)) == 0)
                {
                    memory.SetLocalFlag("LFragmentRead", memory.GetLocalFlag("LFragmentRead") + 1);
                }
                memory.SetLocalFlag(HigurashiFragmentCatalog.FragmentReadFlag(entry.Id), 1);
                memory.SetLocalFlag(HigurashiFragmentCatalog.FragmentStatusFlag(entry.Id), 1);
                scriptName = entry.Script;
            }
            else
            {
                memory.SetLocalFlag(HigurashiFragmentCatalog.FragmentStatusFlag(entry.Id), 2);
                scriptName = "kakera_miss";
            }

            memory.SetLocalFlag("LFragmentPage", _fragmentPage);
            _fragmentListVisible = false;
            _selectedFragmentId = -1;
            SetWindowVisibilityImmediate(false);
            return !string.IsNullOrWhiteSpace(scriptName);
        }

        public bool CompleteCredits()
        {
            if (!CreditsVisible)
            {
                return false;
            }

            // A fast tap used to skip the preceding logo must not also skip a
            // whole credits page on touch-up or on the next rendered frame.
            if (Time.unscaledTime - _creditsPageChangedAt < 0.4f)
            {
                return false;
            }

            CreditsVisible = false;
            CreditsPage = 0;
            return true;
        }

        public bool Choose(int index, BurikoMemory memory)
        {
            if (index < 0 || index >= Choices.Count)
            {
                return false;
            }

            memory.SetChoiceResult(index);
            Choices.Clear();
            return true;
        }

        public void CompleteDialogueReveal()
        {
            _dialogueRevealForced = true;
        }

        public void StopVoices()
        {
            _audio?.StopAllVoices();
            _currentVoiceCharacter = -1;
            _lastVoiceChannel = -1;
            _lastVoiceCharacter = -1;
            _lastVoiceFilename = string.Empty;
            _lastVoiceVolume = 0f;
            ResetLipSyncFrames();
        }

        public void ReplayRestoredVoice(BurikoMemory memory)
        {
            if (_audio == null || _lastVoiceChannel < 0 || string.IsNullOrEmpty(_lastVoiceFilename))
            {
                return;
            }

            _audio.StopAllVoices();
            _currentVoiceCharacter = _lastVoiceCharacter;
            _audio.PlayVoice(
                _lastVoiceChannel,
                _lastVoiceFilename,
                _lastVoiceVolume,
                memory);
        }

        public void StopAllAudio()
        {
            _audio?.StopAll();
            _currentVoiceCharacter = -1;
            _lastVoiceChannel = -1;
            _lastVoiceCharacter = -1;
            _lastVoiceFilename = string.Empty;
            _lastVoiceVolume = 0f;
            ResetLipSyncFrames();
        }

        public void StopTransientAudio()
        {
            _audio?.StopNonBgm();
            _currentVoiceCharacter = -1;
            ResetLipSyncFrames();
        }

        public void StopBgmChannel(int channel)
        {
            _audio?.StopBgm(channel);
        }

        public RuntimeBgmState[] CaptureBgmState()
        {
            return _audio != null ? _audio.CaptureBgmState() : Array.Empty<RuntimeBgmState>();
        }

        public void RestoreBgmState(RuntimeBgmState[] state, BurikoMemory memory)
        {
            _audio?.RestoreBgmState(state, memory);
        }

        public void ToggleWindow()
        {
            SetWindowVisibilityImmediate(!WindowVisible);
        }

        public bool CompleteMovie()
        {
            if (!MovieVisible)
            {
                return false;
            }

            _videoPlayer.Stop();
            FinishMovie();
            return true;
        }

        public UnityBurikoHostSnapshot CaptureSnapshot()
        {
            var layers = new PresentationLayer[_layers.Count];
            var index = 0;
            foreach (var pair in _layers)
            {
                layers[index++] = pair.Value.CloneWithoutTexture();
            }

            return new UnityBurikoHostSnapshot(
                _backgroundName,
                layers,
                _history.ToArray(),
                _historyVoices.ToArray(),
                Speaker,
                Dialogue,
                WindowVisible,
                TitleVisible,
                DialogueSerial,
                SavingEnabled,
                InterfaceEnabled,
                GameplayUiVisible,
                ChapterPreviewVisible,
                _chapterPreviewAccepted,
                _fragmentChapterVisible,
                _fragmentListVisible,
                _fragmentPage,
                _selectedFragmentId,
                _appendNext,
                _lastVoiceChannel,
                _lastVoiceCharacter,
                _lastVoiceFilename,
                _lastVoiceVolume,
                FontSize,
                WindowX,
                WindowY,
                WindowWidth,
                WindowHeight,
                ScreenAspect,
                _fragmentTextureName,
                _fragmentStyle,
                _windowBackgroundName,
                NegativeFilmStrength,
                _messageSpeedOverride);
        }

        public void ReplayRestoredCheckpointAnimations(
            UnityBurikoHostSnapshot previous,
            BurikoMemory memory)
        {
            if (previous == null)
            {
                return;
            }

            const float replayDuration = 0.28f;
            var now = Time.unscaledTime;
            _dialogueRevealStartIndex = 0;
            _dialogueRevealStartedAt = now;
            _dialogueRevealForced = false;

            var backgroundChanged = !string.Equals(previous.BackgroundName, _backgroundName,
                StringComparison.OrdinalIgnoreCase);
            _previousBackgroundTexture = backgroundChanged
                ? LoadBackgroundTexture(previous.BackgroundName, memory)
                : null;
            _backgroundTransitionMask = null;
            _backgroundTransitionStartedAt = now;
            _backgroundTransitionDuration = backgroundChanged ? replayDuration : 0f;

            var previousLayers = new Dictionary<int, PresentationLayer>();
            for (var i = 0; i < previous.Layers.Length; i++)
            {
                previousLayers[previous.Layers[i].Id] = previous.Layers[i];
            }

            _previousSceneLayers.Clear();
            foreach (var pair in _layers)
            {
                var current = pair.Value;
                if (!previousLayers.TryGetValue(pair.Key, out var old))
                {
                    current.FromAlpha = 0f;
                    current.TransitionStartedAt = now;
                    current.TransitionDuration = replayDuration;
                    continue;
                }

                previousLayers.Remove(pair.Key);
                var sameTexture = string.Equals(old.TextureName, current.TextureName,
                    StringComparison.OrdinalIgnoreCase);
                current.FromX = old.X;
                current.FromY = old.Y;
                current.FromZ = old.Z;
                current.FromAlpha = sameTexture ? old.Alpha : 0f;
                current.TransitionStartedAt = now;
                current.TransitionDuration = replayDuration;
                if (!sameTexture)
                {
                    current.PreviousTexture = LoadSpriteTexture(old.TextureName, memory);
                    current.PreviousX = old.X;
                    current.PreviousY = old.Y;
                    current.PreviousZ = old.Z;
                    current.PreviousAlpha = old.Alpha;
                    current.PreviousIsBustshot = old.IsBustshot;
                    current.PreviousIsCentered = old.IsCentered;
                    current.PreviousOverrideWidth = old.OverrideWidth;
                    current.PreviousOverrideHeight = old.OverrideHeight;
                }
            }

            foreach (var pair in previousLayers)
            {
                var old = pair.Value.CloneWithoutTexture();
                old.Texture = LoadSpriteTexture(old.TextureName, memory);
                if (old.Texture != null)
                {
                    _previousSceneLayers.Add(old);
                }
            }
            if (_previousSceneLayers.Count > 0 && _previousBackgroundTexture == null)
            {
                _previousBackgroundTexture = _backgroundTexture;
                _backgroundTransitionStartedAt = now;
                _backgroundTransitionDuration = replayDuration;
            }

            StartFilmTransitionFrom(previous.NegativeFilmStrength, NegativeFilmStrength,
                replayDuration);
        }

        public void WritePersistentState(Stream output)
        {
            using (var writer = new BinaryWriter(output, System.Text.Encoding.UTF8, true))
            {
                writer.Write(PersistentStateMagic);
                writer.Write(_backgroundName ?? string.Empty);
                writer.Write(Speaker ?? string.Empty);
                writer.Write(Dialogue ?? string.Empty);
                writer.Write(WindowVisible);
                writer.Write(TitleVisible);
                writer.Write(DialogueSerial);
                writer.Write(FontSize);
                writer.Write(WindowX);
                writer.Write(WindowY);
                writer.Write(WindowWidth);
                writer.Write(WindowHeight);
                writer.Write(ScreenAspect ?? string.Empty);
                WriteStrings(writer, _history);
                WriteStrings(writer, Choices);
                writer.Write(_layers.Count);
                foreach (var pair in _layers)
                {
                    var layer = pair.Value;
                    writer.Write(layer.Id);
                    writer.Write(layer.TextureName ?? string.Empty);
                    writer.Write(layer.X);
                    writer.Write(layer.Y);
                    writer.Write(layer.Z);
                    writer.Write(layer.Priority);
                    writer.Write(layer.Alpha);
                    writer.Write(layer.IsBustshot);
                }
                // Optional tail keeps old save files readable while preserving the
                // script-controlled UI mode in new saves.
                writer.Write(PersistentUiStateMagic);
                writer.Write(SavingEnabled);
                writer.Write(InterfaceEnabled);
                writer.Write(GameplayUiVisible);
                writer.Write(ChapterPreviewVisible);
                writer.Write(_chapterPreviewAccepted);
                writer.Write(_appendNext);
                writer.Write(PersistentVisualStateMagic);
                writer.Write(_layers.Count);
                foreach (var pair in _layers)
                {
                    writer.Write(pair.Key);
                    writer.Write(pair.Value.OverrideWidth);
                    writer.Write(pair.Value.OverrideHeight);
                }
                writer.Write(_fragmentTextureName ?? string.Empty);
                writer.Write(_fragmentStyle ?? string.Empty);
                writer.Write(_windowBackgroundName ?? string.Empty);
                writer.Write(PersistentFragmentUiStateMagic);
                writer.Write(_fragmentChapterVisible);
                writer.Write(_fragmentListVisible);
                writer.Write(_fragmentPage);
                writer.Write(_selectedFragmentId);
                writer.Write(PersistentAudioStateMagic);
                var bgmState = CaptureBgmState();
                writer.Write(bgmState.Length);
                for (var i = 0; i < bgmState.Length; i++)
                {
                    writer.Write(bgmState[i].Channel);
                    writer.Write(bgmState[i].Filename ?? string.Empty);
                    writer.Write(bgmState[i].Volume);
                }
                writer.Write(PersistentTipsUiStateMagic);
                writer.Write(_tipsChapterVisible);
                writer.Write(_tipsListVisible);
                writer.Write(_tipsLibraryStandalone);
                writer.Write(_tipsScope);
                writer.Write(_tipsPage);
                writer.Write(_selectedTipId);
                writer.Write(PersistentHistoryVoiceStateMagic);
                writer.Write(_historyVoices.Count);
                for (var i = 0; i < _historyVoices.Count; i++)
                {
                    var cue = _historyVoices[i];
                    writer.Write(cue.Channel);
                    writer.Write(cue.Character);
                    writer.Write(cue.Filename ?? string.Empty);
                    writer.Write(cue.Volume);
                }
                writer.Write(PersistentLastVoiceStateMagic);
                writer.Write(_lastVoiceChannel);
                writer.Write(_lastVoiceCharacter);
                writer.Write(_lastVoiceFilename ?? string.Empty);
                writer.Write(_lastVoiceVolume);
                writer.Write(_lastVoiceIssuedForDialogueSerial);
                writer.Write(PersistentLayerAnchorStateMagic);
                writer.Write(_layers.Count);
                foreach (var pair in _layers)
                {
                    writer.Write(pair.Key);
                    writer.Write(pair.Value.IsCentered);
                }
                writer.Write(PersistentTipReadingStateMagic);
                writer.Write(_tipReading);
                writer.Write(_tipsVisibleChapterOverride);
                writer.Write(PersistentFilmStateMagic);
                writer.Write(NegativeFilmStrength);
                writer.Write(PersistentMessageSpeedStateMagic);
                writer.Write(_messageSpeedOverride);
            }
        }

        public void ReadPersistentState(Stream input, BurikoMemory memory)
        {
            var hasPersistedUiState = false;
            var hasPersistedAppendState = false;
            var hasPersistedBgmState = false;
            var hasPersistedFilmState = false;
            var hasPersistedMessageSpeedState = false;
            var persistedFilmStrength = 0f;
            var persistedMessageSpeedOverride = -1;
            var persistedBgmState = Array.Empty<RuntimeBgmState>();
            _fragmentTextureName = string.Empty;
            _fragmentStyle = string.Empty;
            _windowBackgroundName = string.Empty;
            _windowBackgroundTexture = null;
            _fragmentChapterVisible = false;
            _fragmentListVisible = false;
            _tipsChapterVisible = false;
            _tipsListVisible = false;
            _tipsLibraryStandalone = false;
            // TIPS reading is a transient surface and cannot be saved. Clear the
            // current screen before restoring so it cannot taint a story save.
            _tipReading = false;
            _tipsVisibleChapterOverride = -1;
            _tipsScope = 0;
            _tipsPage = 0;
            _selectedTipId = -1;
            _fragmentPage = 0;
            _selectedFragmentId = -1;
            _messageSpeedOverride = -1;
            using (var reader = new BinaryReader(input, System.Text.Encoding.UTF8, true))
            {
                if (reader.ReadInt32() != PersistentStateMagic)
                {
                    throw new InvalidDataException("This is not a Higurashi iOS presentation state.");
                }
                _backgroundName = reader.ReadString();
                Speaker = reader.ReadString();
                Dialogue = reader.ReadString();
                SetWindowVisibilityImmediate(reader.ReadBoolean());
                TitleVisible = reader.ReadBoolean();
                DialogueSerial = reader.ReadInt32();
                FontSize = reader.ReadInt32();
                WindowX = reader.ReadInt32();
                WindowY = reader.ReadInt32();
                WindowWidth = reader.ReadInt32();
                WindowHeight = reader.ReadInt32();
                ScreenAspect = reader.ReadString();
                ReadStrings(reader, _history, 500);
                _historyVoices.Clear();
                for (var i = 0; i < _history.Count; i++)
                {
                    _historyVoices.Add(HistoryVoiceCue.None);
                }
                ReadStrings(reader, Choices, 100);
                var layerCount = ReadCount(reader, 10000, "presentation layer");
                CommitPendingPresentation();
                _layers.Clear();
                for (var i = 0; i < layerCount; i++)
                {
                    var layer = new PresentationLayer
                    {
                        Id = reader.ReadInt32(),
                        TextureName = reader.ReadString(),
                        X = reader.ReadInt32(),
                        Y = reader.ReadInt32(),
                        Z = reader.ReadInt32(),
                        Priority = reader.ReadInt32(),
                        Alpha = reader.ReadSingle(),
                        IsBustshot = reader.ReadBoolean()
                    };
                    layer.FromX = layer.X;
                    layer.FromY = layer.Y;
                    layer.FromZ = layer.Z;
                    layer.FromAlpha = layer.Alpha;
                    layer.IsCentered = layer.IsBustshot || (layer.X == 0 && layer.Y == 0);
                    layer.Texture = LoadSpriteTexture(layer.TextureName, memory);
                    _layers[layer.Id] = layer;
                }

                if (input.CanSeek && input.Length - input.Position >= sizeof(int))
                {
                    var tailPosition = input.Position;
                    if (reader.ReadInt32() == PersistentUiStateMagic &&
                        input.Length - input.Position >= 5)
                    {
                        SavingEnabled = reader.ReadBoolean();
                        InterfaceEnabled = reader.ReadBoolean();
                        GameplayUiVisible = reader.ReadBoolean();
                        ChapterPreviewVisible = reader.ReadBoolean();
                        _chapterPreviewAccepted = reader.ReadBoolean();
                        if (input.Length - input.Position >= 1)
                        {
                            _appendNext = reader.ReadBoolean();
                            hasPersistedAppendState = true;
                        }
                        hasPersistedUiState = true;

                        if (input.Length - input.Position >= sizeof(int))
                        {
                            var visualTailPosition = input.Position;
                            if (reader.ReadInt32() == PersistentVisualStateMagic)
                            {
                                var visualLayerCount = ReadCount(reader, 10000,
                                    "visual presentation layer");
                                for (var i = 0; i < visualLayerCount; i++)
                                {
                                    var id = reader.ReadInt32();
                                    var overrideWidth = reader.ReadInt32();
                                    var overrideHeight = reader.ReadInt32();
                                    if (_layers.TryGetValue(id, out var visualLayer))
                                    {
                                        visualLayer.OverrideWidth = Math.Max(0, overrideWidth);
                                        visualLayer.OverrideHeight = Math.Max(0, overrideHeight);
                                    }
                                }
                                _fragmentTextureName = reader.ReadString();
                                _fragmentStyle = reader.ReadString();
                                // The window skin was added after the fragment tail.
                                // Keep saves made before that addition readable.
                                if (input.Length > input.Position)
                                {
                                    _windowBackgroundName = reader.ReadString();
                                }
                                if (input.Length - input.Position >= sizeof(int))
                                {
                                    var fragmentUiTailPosition = input.Position;
                                    if (reader.ReadInt32() == PersistentFragmentUiStateMagic &&
                                        input.Length - input.Position >= 10)
                                    {
                                        _fragmentChapterVisible = reader.ReadBoolean();
                                        _fragmentListVisible = reader.ReadBoolean();
                                        _fragmentPage = Math.Max(0, reader.ReadInt32());
                                        _selectedFragmentId = reader.ReadInt32();
                                    }
                                    else
                                    {
                                        input.Position = fragmentUiTailPosition;
                                    }
                                }
                            }
                            else
                            {
                                input.Position = visualTailPosition;
                            }
                        }
                    }
                    else
                    {
                        input.Position = tailPosition;
                    }
                }

                if (input.CanSeek && input.Length - input.Position >= sizeof(int) * 2)
                {
                    var audioTailPosition = input.Position;
                    if (reader.ReadInt32() == PersistentAudioStateMagic)
                    {
                        var bgmCount = ReadCount(reader, 64, "BGM channel");
                        persistedBgmState = new RuntimeBgmState[bgmCount];
                        for (var i = 0; i < bgmCount; i++)
                        {
                            persistedBgmState[i] = new RuntimeBgmState(
                                reader.ReadInt32(),
                                reader.ReadString(),
                                reader.ReadSingle());
                        }
                        hasPersistedBgmState = true;
                    }
                    else
                    {
                        input.Position = audioTailPosition;
                    }
                }

                if (input.CanSeek && input.Length - input.Position >= sizeof(int))
                {
                    var tipsTailPosition = input.Position;
                    if (reader.ReadInt32() == PersistentTipsUiStateMagic &&
                        input.Length - input.Position >= 3 + sizeof(int) * 3)
                    {
                        _tipsChapterVisible = reader.ReadBoolean();
                        _tipsListVisible = reader.ReadBoolean();
                        _tipsLibraryStandalone = reader.ReadBoolean();
                        _tipsScope = Mathf.Clamp(reader.ReadInt32(), 0, 2);
                        _tipsPage = Math.Max(0, reader.ReadInt32());
                        _selectedTipId = reader.ReadInt32();
                    }
                    else
                    {
                        input.Position = tipsTailPosition;
                    }
                }

                if (input.CanSeek && input.Length - input.Position >= sizeof(int) * 2)
                {
                    var historyVoiceTailPosition = input.Position;
                    if (reader.ReadInt32() == PersistentHistoryVoiceStateMagic)
                    {
                        var historyVoiceCount = ReadCount(reader, 500, "history voice");
                        var restored = new List<HistoryVoiceCue>(historyVoiceCount);
                        for (var i = 0; i < historyVoiceCount; i++)
                        {
                            restored.Add(new HistoryVoiceCue(reader.ReadInt32(), reader.ReadInt32(),
                                reader.ReadString(), reader.ReadSingle()));
                        }
                        if (historyVoiceCount == _history.Count)
                        {
                            _historyVoices.Clear();
                            _historyVoices.AddRange(restored);
                        }
                    }
                    else
                    {
                        input.Position = historyVoiceTailPosition;
                    }
                }

                if (input.CanSeek && input.Length - input.Position >= sizeof(int) * 4 + sizeof(float))
                {
                    var lastVoiceTailPosition = input.Position;
                    if (reader.ReadInt32() == PersistentLastVoiceStateMagic)
                    {
                        _lastVoiceChannel = reader.ReadInt32();
                        _lastVoiceCharacter = reader.ReadInt32();
                        _lastVoiceFilename = reader.ReadString();
                        _lastVoiceVolume = reader.ReadSingle();
                        _lastVoiceIssuedForDialogueSerial = reader.ReadInt32();
                    }
                    else
                    {
                        input.Position = lastVoiceTailPosition;
                    }
                }

                if (input.CanSeek && input.Length - input.Position >= sizeof(int) * 2)
                {
                    var layerAnchorTailPosition = input.Position;
                    if (reader.ReadInt32() == PersistentLayerAnchorStateMagic)
                    {
                        var layerAnchorCount = ReadCount(reader, 10000,
                            "presentation layer anchor");
                        for (var i = 0; i < layerAnchorCount; i++)
                        {
                            var id = reader.ReadInt32();
                            var isCentered = reader.ReadBoolean();
                            if (_layers.TryGetValue(id, out var layer))
                            {
                                layer.IsCentered = isCentered;
                            }
                        }
                    }
                    else
                    {
                        input.Position = layerAnchorTailPosition;
                    }
                }

                if (input.CanSeek && input.Length - input.Position >= sizeof(int) * 2 + 1)
                {
                    var tipReadingTailPosition = input.Position;
                    if (reader.ReadInt32() == PersistentTipReadingStateMagic)
                    {
                        _tipReading = reader.ReadBoolean();
                        _tipsVisibleChapterOverride = reader.ReadInt32();
                    }
                    else
                    {
                        input.Position = tipReadingTailPosition;
                    }
                }

                if (input.CanSeek && input.Length - input.Position >= sizeof(int) + sizeof(float))
                {
                    var filmTailPosition = input.Position;
                    if (reader.ReadInt32() == PersistentFilmStateMagic)
                    {
                        persistedFilmStrength = Mathf.Clamp01(reader.ReadSingle());
                        hasPersistedFilmState = true;
                    }
                    else
                    {
                        input.Position = filmTailPosition;
                    }
                }

                if (input.CanSeek && input.Length - input.Position >= sizeof(int) * 2)
                {
                    var messageSpeedTailPosition = input.Position;
                    if (reader.ReadInt32() == PersistentMessageSpeedStateMagic)
                    {
                        persistedMessageSpeedOverride = reader.ReadInt32();
                        hasPersistedMessageSpeedState = true;
                    }
                    else
                    {
                        input.Position = messageSpeedTailPosition;
                    }
                }
            }

            CreditsVisible = false;
            CreditsPage = 0;
            if (!hasPersistedUiState)
            {
                ChapterPreviewVisible = false;
                _fragmentChapterVisible = false;
                _fragmentListVisible = false;
                _fragmentPage = 0;
                _selectedFragmentId = -1;
                GameplayUiVisible = !TitleVisible;
                _chapterPreviewAccepted = GameplayUiVisible;
                SavingEnabled = !TitleVisible;
                InterfaceEnabled = true;
            }
            HistoryVisible = false;
            MovieVisible = false;
            _backgroundTexture = LoadBackgroundTexture(_backgroundName, memory);
            _previousBackgroundTexture = null;
            _backgroundTransitionMask = null;
            _backgroundTransitionDuration = 0f;
            _fragmentTexture = string.IsNullOrWhiteSpace(_fragmentTextureName)
                ? null
                : LoadSpriteTexture(_fragmentTextureName, memory);
            _windowBackgroundTexture = string.IsNullOrWhiteSpace(_windowBackgroundName)
                ? null
                : LoadBackgroundTexture(_windowBackgroundName, memory);
            _fragmentStartedAt = Time.unscaledTime;
            _fragmentTransitionDuration = 0f;
            _fragmentTransitionFrom = _fragmentTexture != null ? 1f : 0f;
            _fragmentTransitionTo = _fragmentTransitionFrom;
            _blockingAnimationUntil = 0f;
            StartFilmTransitionFrom(
                hasPersistedFilmState ? persistedFilmStrength : 0f,
                hasPersistedFilmState ? persistedFilmStrength : 0f,
                0f);
            _messageSpeedOverride = hasPersistedMessageSpeedState
                ? persistedMessageSpeedOverride
                : -1;
            _previousSceneLayers.Clear();
            _dialogueRevealForced = true;
            if (!hasPersistedAppendState)
            {
                _appendNext = false;
            }
            if (hasPersistedBgmState)
            {
                RestoreBgmState(persistedBgmState, memory);
            }
        }

        private static void WriteStrings(BinaryWriter writer, IReadOnlyList<string> values)
        {
            writer.Write(values.Count);
            for (var i = 0; i < values.Count; i++)
            {
                writer.Write(values[i] ?? string.Empty);
            }
        }

        private static void ReadStrings(BinaryReader reader, List<string> target, int maximum)
        {
            var count = ReadCount(reader, maximum, "string");
            target.Clear();
            for (var i = 0; i < count; i++)
            {
                target.Add(reader.ReadString());
            }
        }

        private static int ReadCount(BinaryReader reader, int maximum, string description)
        {
            var count = reader.ReadInt32();
            if (count < 0 || count > maximum)
            {
                throw new InvalidDataException("Invalid " + description + " count: " + count);
            }
            return count;
        }

        public void RestoreSnapshot(UnityBurikoHostSnapshot snapshot, BurikoMemory memory)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            Speaker = snapshot.Speaker;
            Dialogue = snapshot.Dialogue;
            _dialogueRevealForced = true;
            SetWindowVisibilityImmediate(snapshot.WindowVisible);
            TitleVisible = snapshot.TitleVisible;
            ChapterPreviewVisible = snapshot.ChapterPreviewVisible;
            GameplayUiVisible = snapshot.GameplayUiVisible;
            _chapterPreviewAccepted = snapshot.ChapterPreviewAccepted;
            _fragmentChapterVisible = snapshot.FragmentChapterVisible;
            _fragmentListVisible = snapshot.FragmentListVisible;
            _fragmentPage = snapshot.FragmentPage;
            _selectedFragmentId = snapshot.SelectedFragmentId;
            _appendNext = snapshot.AppendNext;
            _lastVoiceChannel = snapshot.LastVoiceChannel;
            _lastVoiceCharacter = snapshot.LastVoiceCharacter;
            _lastVoiceFilename = snapshot.LastVoiceFilename;
            _lastVoiceVolume = snapshot.LastVoiceVolume;
            _currentVoiceCharacter = -1;
            SavingEnabled = snapshot.SavingEnabled;
            InterfaceEnabled = snapshot.InterfaceEnabled;
            DialogueSerial = snapshot.DialogueSerial;
            FontSize = snapshot.FontSize;
            WindowX = snapshot.WindowX;
            WindowY = snapshot.WindowY;
            WindowWidth = snapshot.WindowWidth;
            WindowHeight = snapshot.WindowHeight;
            ScreenAspect = snapshot.ScreenAspect;
            _backgroundName = snapshot.BackgroundName;
            _backgroundTexture = LoadBackgroundTexture(_backgroundName, memory);
            _previousBackgroundTexture = null;
            _backgroundTransitionMask = null;
            _backgroundTransitionDuration = 0f;
            _blockingAnimationUntil = 0f;
            StartFilmTransitionFrom(snapshot.NegativeFilmStrength,
                snapshot.NegativeFilmStrength, 0f);
            _messageSpeedOverride = snapshot.MessageSpeedOverride;
            _fragmentTextureName = snapshot.FragmentTextureName;
            _fragmentStyle = snapshot.FragmentStyle;
            _fragmentTexture = string.IsNullOrWhiteSpace(_fragmentTextureName)
                ? null
                : LoadSpriteTexture(_fragmentTextureName, memory);
            _windowBackgroundName = snapshot.WindowBackgroundName;
            _windowBackgroundTexture = string.IsNullOrWhiteSpace(_windowBackgroundName)
                ? null
                : LoadBackgroundTexture(_windowBackgroundName, memory);
            _fragmentStartedAt = Time.unscaledTime;
            _fragmentTransitionDuration = 0f;
            _fragmentTransitionFrom = _fragmentTexture != null ? 1f : 0f;
            _fragmentTransitionTo = _fragmentTransitionFrom;
            _previousSceneLayers.Clear();
            _history.Clear();
            _history.AddRange(snapshot.History);
            _historyVoices.Clear();
            _historyVoices.AddRange(snapshot.HistoryVoices);
            while (_historyVoices.Count < _history.Count)
            {
                _historyVoices.Add(HistoryVoiceCue.None);
            }
            if (_historyVoices.Count > _history.Count)
            {
                _historyVoices.RemoveRange(_history.Count, _historyVoices.Count - _history.Count);
            }
            CommitPendingPresentation();
            _layers.Clear();
            for (var i = 0; i < snapshot.Layers.Length; i++)
            {
                var layer = snapshot.Layers[i].CloneWithoutTexture();
                layer.Texture = LoadSpriteTexture(layer.TextureName, memory);
                _layers[layer.Id] = layer;
            }
        }

        public void ReloadVisualAssets(BurikoMemory memory)
        {
            _backgroundTexture = LoadBackgroundTexture(_backgroundName, memory);
            _previousBackgroundTexture = null;
            _backgroundTransitionDuration = 0f;
            _fragmentTexture = string.IsNullOrWhiteSpace(_fragmentTextureName)
                ? null
                : LoadSpriteTexture(_fragmentTextureName, memory);
            _windowBackgroundTexture = string.IsNullOrWhiteSpace(_windowBackgroundName)
                ? null
                : LoadBackgroundTexture(_windowBackgroundName, memory);

            // A style change is an immediate presentation replacement. Do not
            // keep a previous-style transition texture or scene-layer snapshot.
            _previousSceneLayers.Clear();
            foreach (var pair in _layers)
            {
                pair.Value.CompleteTransition();
                pair.Value.Texture = LoadSpriteTexture(pair.Value.TextureName, memory);
            }
        }

        internal IReadOnlyList<string> CurrentBgmFolders(BurikoMemory memory)
        {
            return CascadeFolders(_bgmSets, memory.GetGlobalFlag("GAltBGM"), "BGM");
        }

        internal IReadOnlyList<string> CurrentSeFolders(BurikoMemory memory)
        {
            return CascadeFolders(_seSets, memory.GetGlobalFlag("GAltSE"), "SE");
        }

        private BurikoHostResponse SetDialogue(
            string primaryName,
            string primaryText,
            string fallbackName,
            string fallbackText,
            int textMode)
        {
            // CompiledChineseScripts stores its translated line in the second language slot.
            var name = string.IsNullOrEmpty(fallbackName) ? primaryName : fallbackName;
            var text = (string.IsNullOrEmpty(fallbackText) ? primaryText : fallbackText)
                .Replace("\\n", "\n");
            // Buriko's continuation flag belongs to the previous output operation:
            // Continue marks the following line for append, while Normal clears it.
            // Looking only at the current mode reverses sequences such as
            // Continue("为什么那么冷淡呢。") -> Normal("…呢？").
            var append = _appendNext;
            var appendToInProgressReveal = append && !_dialogueRevealForced &&
                                           VisibleDialogueLength < Dialogue.Length;
            var revealStart = append ? Dialogue.Length : 0;
            if (append)
            {
                if (!string.IsNullOrEmpty(name))
                {
                    Speaker = name;
                }
                Dialogue += text;
            }
            else
            {
                Speaker = name;
                Dialogue = text;
            }
            var openingPrompt = OpeningChoicePolicy.IsOpeningPrompt(Dialogue);
            var consoleChoicePrompt = ConsoleChoiceMenuPolicy.IsConsoleChoicePrompt(Dialogue);
            if (openingPrompt)
            {
                Speaker = string.Empty;
                Dialogue = OpeningChoicePolicy.LocalizedPrompt;
            }
            else if (consoleChoicePrompt)
            {
                Speaker = string.Empty;
                Dialogue = ConsoleChoiceMenuPolicy.LocalizedPrompt;
            }
            SetWindowVisibilityImmediate(true);
            if (!appendToInProgressReveal)
            {
                _dialogueRevealStartIndex = revealStart;
                _dialogueRevealStartedAt = Time.unscaledTime;
            }
            _dialogueRevealForced = false;
            _appendNext = textMode != 0;
            DialogueSerial++;
            // OpeningQuestion immediately follows this prompt with Select. Keeping
            // Line_Normal blocked leaves a blank-looking screen until an extra tap.
            var waitsForInput = (textMode == 0 || textMode == 2) &&
                                !openingPrompt && !consoleChoicePrompt;
            if (waitsForInput && _chapterPreviewAccepted)
            {
                GameplayUiVisible = true;
            }
            if (waitsForInput)
            {
                var historyLine = string.IsNullOrEmpty(Speaker) ? Dialogue : Speaker + "\n" + Dialogue;
                if (!string.IsNullOrEmpty(historyLine))
                {
                    _history.Add(historyLine);
                    _historyVoices.Add(_lastVoiceIssuedForDialogueSerial == DialogueSerial
                        ? new HistoryVoiceCue(_lastVoiceChannel, _lastVoiceCharacter,
                            _lastVoiceFilename, _lastVoiceVolume)
                        : HistoryVoiceCue.None);
                    if (_history.Count > 500)
                    {
                        _history.RemoveAt(0);
                        _historyVoices.RemoveAt(0);
                    }
                }
            }

            return waitsForInput
                ? new BurikoHostResponse(BurikoValue.Null, BurikoBlockReason.WaitForInput)
                : BurikoHostResponse.Continue;
        }

        private void ShowChoices(BurikoOperationInvocation invocation, BurikoMemory memory)
        {
            Choices.Clear();
            var count = Math.Max(0, Int(invocation, 0, memory));
            var reference = invocation.Arguments[1].Reference;
            if (reference == null)
            {
                return;
            }

            for (var i = 0; i < count; i++)
            {
                var choice = memory.Get(new BurikoReference(reference.Name, i)).AsString(memory);
                Choices.Add(ConsoleChoiceMenuPolicy.Localize(
                    StoryChoiceLocalization.Localize(choice)));
            }

            if (OpeningChoicePolicy.IsOpeningChoice(Dialogue, Choices))
            {
                Choices[0] = OpeningChoicePolicy.LocalizedEnable;
                Choices[1] = OpeningChoicePolicy.LocalizedDisable;
            }
        }

        private void PlayBgm(BurikoOperationInvocation invocation, BurikoMemory memory, bool modVariant)
        {
            if (modVariant && memory.GetGlobalFlag("GAltBGMflow") != Int(invocation, 4, memory))
            {
                return;
            }

            _audio.PlayBgm(
                Int(invocation, 0, memory),
                AddOgg(Text(invocation, 1, memory)),
                Int(invocation, 2, memory) / 128f,
                memory);
        }

        private BurikoHostResponse StartMovie(string movieName)
        {
            var safeName = Path.GetFileNameWithoutExtension(movieName);
            if (string.IsNullOrWhiteSpace(safeName))
            {
                return BurikoHostResponse.Continue;
            }

            var path = SafePath.ResolveUnderRoot(
                _streamingAssetsRoot,
                "movies/" + safeName.ToLowerInvariant() + ".mp4");
            if (!File.Exists(path))
            {
                Debug.LogWarning("Movie asset was not found: " + path);
                return BurikoHostResponse.Continue;
            }

            MovieVisible = true;
            _videoPlayer.url = new Uri(path).AbsoluteUri;
            _videoPlayer.Play();
            return new BurikoHostResponse(BurikoValue.Null, BurikoBlockReason.Host);
        }

        private void OnMovieEnded(VideoPlayer player)
        {
            FinishMovie();
        }

        private void OnMovieError(VideoPlayer player, string message)
        {
            Debug.LogWarning("Movie playback failed: " + message);
            FinishMovie();
        }

        private void FinishMovie()
        {
            if (!MovieVisible)
            {
                return;
            }

            MovieVisible = false;
            MovieFinished?.Invoke();
        }

        private void SetBackground(string textureName, BurikoMemory memory, bool clearLayers = true,
            float duration = 0f, string transitionMask = null)
        {
            var preparedLayerIds = clearLayers
                ? _sceneLayerBatch.ConsumeForSceneChange()
                : System.Array.Empty<int>();
            if (!clearLayers)
            {
                _sceneLayerBatch.Commit();
            }
            var preparedLayers = new List<PresentationLayer>(preparedLayerIds.Length);
            var preparedLookup = new HashSet<int>(preparedLayerIds);
            for (var i = 0; i < preparedLayerIds.Length; i++)
            {
                if (_layers.TryGetValue(preparedLayerIds[i], out var prepared))
                {
                    preparedLayers.Add(prepared);
                }
            }

            var nextTexture = LoadBackgroundTexture(textureName, memory);
            _previousBackgroundTexture = duration > 0f ? _backgroundTexture : null;
            _backgroundTransitionMask = duration > 0f && !string.IsNullOrWhiteSpace(transitionMask)
                ? LoadBackgroundTexture(transitionMask, memory)
                : null;
            _backgroundName = textureName;
            _backgroundTexture = nextTexture;
            _backgroundTransitionStartedAt = Time.unscaledTime;
            _backgroundTransitionDuration = Mathf.Max(0f, duration);
            if (clearLayers)
            {
                _previousSceneLayers.Clear();
                if (duration > 0f)
                {
                    foreach (var pair in _layers)
                    {
                        if (preparedLookup.Contains(pair.Key))
                        {
                            continue;
                        }
                        var source = pair.Value;
                        source.GetRenderState(out var x, out var y, out var z, out var alpha);
                        var copy = source.CloneWithoutTexture();
                        copy.Texture = source.Texture;
                        copy.X = Mathf.RoundToInt(x);
                        copy.Y = Mathf.RoundToInt(y);
                        copy.Z = Mathf.RoundToInt(z);
                        copy.Alpha = alpha;
                        copy.FromX = copy.X;
                        copy.FromY = copy.Y;
                        copy.FromZ = copy.Z;
                        copy.FromAlpha = alpha;
                        copy.TransitionDuration = 0f;
                        _previousSceneLayers.Add(copy);
                    }
                }
                _layers.Clear();
                for (var i = 0; i < preparedLayers.Count; i++)
                {
                    _layers[preparedLayers[i].Id] = preparedLayers[i];
                }
            }
        }

        public void CommitPendingPresentation()
        {
            _sceneLayerBatch.Commit();
        }

        private void DiscardPreparedLayerRange(int first, int last)
        {
            for (var id = first; id <= last; id++)
            {
                _sceneLayerBatch.Discard(id);
            }
        }

        private void DrawLayer(
            int id,
            string textureName,
            int x,
            int y,
            int z,
            int priority,
            BurikoMemory memory,
            bool isBustshot,
            float alpha = 1f,
            float duration = 0f,
            int overrideWidth = 0,
            int overrideHeight = 0)
        {
            Texture2D previousTexture = null;
            float previousX = x;
            float previousY = y;
            float previousZ = z;
            float previousAlpha = 0f;
            var previousIsBustshot = isBustshot;
            var previousIsCentered = isBustshot || (x == 0 && y == 0);
            var previousOverrideWidth = 0;
            var previousOverrideHeight = 0;
            if (duration > 0f && _layers.TryGetValue(id, out var previous))
            {
                previousTexture = previous.Texture;
                previous.GetRenderState(out previousX, out previousY, out previousZ, out previousAlpha);
                previousIsBustshot = previous.IsBustshot;
                previousIsCentered = previous.IsCentered;
                previousOverrideWidth = previous.OverrideWidth;
                previousOverrideHeight = previous.OverrideHeight;
            }

            _layers[id] = new PresentationLayer
            {
                Id = id,
                TextureName = textureName,
                Texture = LoadSpriteTexture(textureName, memory),
                X = x,
                Y = y,
                Z = z,
                Priority = priority == 0 ? id : priority,
                Alpha = Mathf.Clamp01(alpha),
                IsBustshot = isBustshot,
                IsCentered = isBustshot || (x == 0 && y == 0),
                FromX = x,
                FromY = y,
                FromZ = z,
                FromAlpha = duration > 0f ? 0f : Mathf.Clamp01(alpha),
                TransitionStartedAt = Time.unscaledTime,
                TransitionDuration = Mathf.Max(0f, duration),
                PreviousTexture = previousTexture,
                PreviousX = previousX,
                PreviousY = previousY,
                PreviousZ = previousZ,
                PreviousAlpha = previousAlpha,
                PreviousIsBustshot = previousIsBustshot,
                PreviousIsCentered = previousIsCentered,
                OverrideWidth = Math.Max(0, overrideWidth),
                OverrideHeight = Math.Max(0, overrideHeight),
                PreviousOverrideWidth = previousOverrideWidth,
                PreviousOverrideHeight = previousOverrideHeight,
                EaseType = duration > 0f ? 13 : 0
            };
        }

        private void DrawAnimatedLayer(
            BurikoOperationInvocation invocation,
            BurikoMemory memory,
            bool isBustshot,
            int idIndex,
            int textureIndex,
            int xIndex,
            int yIndex,
            int zIndex,
            int moveIndex,
            int oldXIndex,
            int oldYIndex,
            int oldZIndex,
            int priorityIndex,
            int durationIndex)
        {
            var id = Int(invocation, idIndex, memory);
            var x = Int(invocation, xIndex, memory);
            var y = Int(invocation, yIndex, memory);
            var z = Int(invocation, zIndex, memory);
            DrawLayer(
                id,
                Text(invocation, textureIndex, memory),
                x,
                y,
                z,
                Int(invocation, priorityIndex, memory),
                memory,
                isBustshot,
                1f,
                Int(invocation, durationIndex, memory) / 1000f);
            if (invocation.Arguments[moveIndex].AsBool(memory))
            {
                var layer = _layers[id];
                layer.FromX = Int(invocation, oldXIndex, memory);
                layer.FromY = Int(invocation, oldYIndex, memory);
                layer.FromZ = Int(invocation, oldZIndex, memory);
                layer.EaseType = 0;
            }
        }

        private void MoveBustshot(BurikoOperationInvocation invocation, BurikoMemory memory)
        {
            var id = Int(invocation, 0, memory);
            var duration = Int(invocation, 6, memory) / 1000f;
            if (!_layers.TryGetValue(id, out var layer))
            {
                DrawLayer(
                    id,
                    Text(invocation, 1, memory),
                    Int(invocation, 2, memory),
                    Int(invocation, 3, memory),
                    Int(invocation, 4, memory),
                    id,
                    memory,
                    true,
                    1f,
                    duration);
                return;
            }

            layer.BeginTransition(
                Int(invocation, 2, memory),
                Int(invocation, 3, memory),
                Int(invocation, 4, memory),
                layer.Alpha,
                duration,
                0);
            var textureName = Text(invocation, 1, memory);
            if (!string.IsNullOrEmpty(textureName))
            {
                layer.TextureName = textureName;
                layer.Texture = LoadSpriteTexture(textureName, memory);
            }
        }

        private void ChangeBustshot(BurikoOperationInvocation invocation, BurikoMemory memory)
        {
            var id = Int(invocation, 0, memory);
            var textureName = Text(invocation, 1, memory);
            var duration = Int(invocation, 2, memory) / 1000f;
            if (!_layers.TryGetValue(id, out var previous))
            {
                DrawLayer(id, textureName, 0, 0, 0, id, memory, true, 1f, duration);
                return;
            }

            DrawLayer(
                id,
                textureName,
                previous.X,
                previous.Y,
                previous.Z,
                previous.Priority,
                memory,
                previous.IsBustshot,
                previous.Alpha,
                duration);
            var changed = _layers[id];
            changed.CharacterId = previous.CharacterId;
            changed.LipSyncBaseName = previous.LipSyncBaseName;
            changed.LipSyncRestName = previous.LipSyncRestName;
            changed.LipSyncFrame = previous.LipSyncFrame;
        }

        private void DrawFixedSizeSprite(
            BurikoOperationInvocation invocation,
            BurikoMemory memory,
            bool filtering)
        {
            var id = Int(invocation, 0, memory);
            if (filtering)
            {
                DrawLayer(
                    id,
                    Text(invocation, 1, memory),
                    Int(invocation, 4, memory),
                    Int(invocation, 5, memory),
                    0,
                    Int(invocation, 12, memory),
                    memory,
                    false,
                    1f,
                    Int(invocation, 13, memory) / 1000f,
                    Int(invocation, 6, memory),
                    Int(invocation, 7, memory));
                SetLayerMask(id, Text(invocation, 2, memory),
                    Int(invocation, 3, memory), false, memory);
                return;
            }

            DrawLayer(
                id,
                Text(invocation, 1, memory),
                Int(invocation, 3, memory),
                Int(invocation, 4, memory),
                Int(invocation, 5, memory),
                Int(invocation, 15, memory),
                memory,
                false,
                1f - Int(invocation, 14, memory) / 256f,
                Int(invocation, 16, memory) / 1000f,
                Int(invocation, 8, memory),
                Int(invocation, 9, memory));
        }

        private void FadeBustshot(BurikoOperationInvocation invocation, BurikoMemory memory)
        {
            var id = Int(invocation, 0, memory);
            if (!_layers.TryGetValue(id, out var layer))
            {
                return;
            }

            var duration = Int(invocation, 6, memory) / 1000f;
            if (invocation.Arguments[1].AsBool(memory))
            {
                layer.BeginTransition(
                    Int(invocation, 2, memory),
                    Int(invocation, 3, memory),
                    Int(invocation, 4, memory),
                    layer.Alpha,
                    duration,
                    0);
            }
            else
            {
                FadeLayer(id, duration);
            }
        }

        private void FadeLayer(int id, float duration)
        {
            if (!_layers.TryGetValue(id, out var layer))
            {
                return;
            }
            layer.BeginTransition(layer.X, layer.Y, layer.Z, 0f, duration, 13);
            layer.LipSyncBaseName = null;
        }

        private void FadeLayerWithMask(int id, string maskName, int style, float duration,
            BurikoMemory memory)
        {
            if (!_layers.TryGetValue(id, out var layer))
            {
                return;
            }

            layer.BeginTransition(layer.X, layer.Y, layer.Z, 0f, duration, 13);
            SetLayerMask(id, maskName, style, true, memory);
            layer.LipSyncBaseName = null;
        }

        private void SetLayerMask(int id, string maskName, int style, bool reverse,
            BurikoMemory memory)
        {
            if (!_layers.TryGetValue(id, out var layer) || string.IsNullOrWhiteSpace(maskName))
            {
                return;
            }

            layer.MaskName = maskName;
            layer.MaskTexture = LoadTexture(maskName, memory);
            layer.MaskFuzziness = style == 0 ? 0.45f : 0.15f;
            layer.MaskReverse = reverse;
        }

        private void FadeLayerRange(int first, int last, float duration)
        {
            for (var id = first; id <= last; id++)
            {
                FadeLayer(id, duration);
            }
        }

        private void DrawModCharacter(
            BurikoOperationInvocation invocation,
            BurikoMemory memory,
            bool filtered)
        {
            var texture = Text(invocation, 2, memory);
            var expression = Text(invocation, 3, memory);
            var renderedTexture = _settings != null && _settings.lipSync
                ? texture + "0"
                : texture + expression;
            var xIndex = filtered ? 6 : 4;
            var yIndex = filtered ? 7 : 5;
            var zIndex = filtered ? 12 : 6;
            var priorityIndex = filtered ? 14 : 15;
            DrawLayer(
                Int(invocation, 0, memory),
                renderedTexture,
                Int(invocation, xIndex, memory),
                Int(invocation, yIndex, memory),
                Int(invocation, zIndex, memory),
                Int(invocation, priorityIndex, memory),
                memory,
                true,
                1f,
                Int(invocation, filtered ? 15 : 16, memory) / 1000f);
            var layer = _layers[Int(invocation, 0, memory)];
            layer.CharacterId = Int(invocation, 1, memory);
            layer.LipSyncBaseName = texture;
            layer.LipSyncRestName = renderedTexture;
            var moveIndex = filtered ? 8 : 7;
            if (invocation.Arguments[moveIndex].AsBool(memory))
            {
                layer.FromX = Int(invocation, filtered ? 9 : 8, memory);
                layer.FromY = Int(invocation, filtered ? 10 : 9, memory);
                layer.FromZ = Int(invocation, filtered ? 11 : 10, memory);
                layer.EaseType = 0;
            }
        }

        private void MoveLayer(BurikoOperationInvocation invocation, BurikoMemory memory)
        {
            var id = Int(invocation, 0, memory);
            if (!_layers.TryGetValue(id, out var layer))
            {
                return;
            }

            // Both MoveSprite variants begin with layer and target x/y after optional texture data.
            var offset = invocation.Specification.Code == 67 ? 3 : 1;
            if (invocation.Specification.Code == 66)
            {
                layer.BeginTransition(
                    Int(invocation, 1, memory),
                    Int(invocation, 2, memory),
                    Int(invocation, 3, memory),
                    1f - Int(invocation, 5, memory) / 256f,
                    Int(invocation, 8, memory) / 1000f,
                    Int(invocation, 6, memory));
            }
            else
            {
                layer.X = Int(invocation, offset, memory);
                layer.Y = Int(invocation, offset + 1, memory);
            }
            if (invocation.Specification.Code == 67)
            {
                var textureName = Text(invocation, 1, memory);
                if (!string.IsNullOrEmpty(textureName))
                {
                    layer.TextureName = textureName;
                    layer.Texture = LoadSpriteTexture(textureName, memory);
                }
            }
        }

        private Texture2D LoadTexture(string textureName, BurikoMemory memory)
        {
            return LoadSpriteTexture(textureName, memory);
        }

        private Texture2D LoadSpriteTexture(string textureName, BurikoMemory memory)
        {
            return LoadTextureFromSet(textureName, memory, _spriteSets,
                _settings == null ? 0 : _settings.spriteStyleIndex, "CG");
        }

        private Texture2D LoadBackgroundTexture(string textureName, BurikoMemory memory)
        {
            return LoadTextureFromSet(textureName, memory, _backgroundSets,
                _settings == null ? 0 : _settings.backgroundStyleIndex, "CG");
        }

        private Texture2D LoadTextureFromSet(string textureName, BurikoMemory memory,
            List<RuntimePathCascade> sets, int index, string fallback)
        {
            if (string.IsNullOrWhiteSpace(textureName) || _assets == null)
            {
                return null;
            }

            var selected = ClampIndex(index, sets.Count);
            var folders = sets.Count == 0 ? new[] { fallback } : sets[selected].Folders;
            // GLanguage 0 is the installed Chinese script set.  The un-suffixed
            // textures are localized; the optional _j files are Japanese.
            return _assets.LoadTexture(textureName, folders, preferAsianVariant: false);
        }

        private void RebuildVisualStyleCatalog()
        {
            _spriteSets.Clear();
            _backgroundSets.Clear();
            if (_artSets.Count == 0)
            {
                return;
            }

            var console = _artSets[0];
            for (var i = 0; i < _artSets.Count; i++)
            {
                var source = _artSets[i];
                var isOriginal = i == _artSets.Count - 1 && _artSets.Count >= 3;
                var isRemake = i == 1 && _artSets.Count >= 3;
                var spriteFolders = VisualStyleFolderPolicy.SpriteFoldersFor(
                    i, _artSets.Count, source.Folders);
                var backgroundFolders = isOriginal
                    ? VisualStyleFolderPolicy.BackgroundFoldersFor(
                        1, _artSets.Count, source.Folders)
                    : VisualStyleFolderPolicy.BackgroundFoldersFor(
                        0, _artSets.Count, console.Folders);

                _spriteSets.Add(new RuntimePathCascade(source.NameEnglish,
                    source.NameAsian, spriteFolders));
                if (i == 0 || isOriginal)
                {
                    _backgroundSets.Add(new RuntimePathCascade(source.NameEnglish,
                        source.NameAsian, backgroundFolders));
                }
            }
        }

        private void UpdateLipSync()
        {
            if (_memory == null || _settings == null || !_settings.lipSync)
            {
                return;
            }

            var voicePlaying = _audio != null && _audio.AnyVoicePlaying();
            var framePattern = new[] { 0, 1, 0, 2 };
            var frame = voicePlaying
                ? framePattern[Mathf.FloorToInt(Time.unscaledTime * 10f) % framePattern.Length]
                : 0;
            foreach (var pair in _layers)
            {
                var layer = pair.Value;
                if (string.IsNullOrEmpty(layer.LipSyncBaseName))
                {
                    continue;
                }
                var shouldAnimate = voicePlaying &&
                    (_currentVoiceCharacter < 0 || layer.CharacterId == _currentVoiceCharacter);
                var targetFrame = shouldAnimate ? frame : 0;
                if (layer.LipSyncFrame == targetFrame)
                {
                    continue;
                }

                var textureName = layer.LipSyncBaseName + targetFrame;
                var texture = LoadSpriteTexture(textureName, _memory);
                if (texture == null && targetFrame != 0)
                {
                    textureName = layer.LipSyncBaseName + "0";
                    texture = LoadSpriteTexture(textureName, _memory);
                }
                if (texture != null)
                {
                    layer.TextureName = textureName;
                    layer.Texture = texture;
                    layer.LipSyncFrame = targetFrame;
                }
            }
        }

        private void ResetLipSyncFrames()
        {
            if (_memory == null)
            {
                return;
            }
            foreach (var pair in _layers)
            {
                var layer = pair.Value;
                if (string.IsNullOrEmpty(layer.LipSyncBaseName))
                {
                    continue;
                }
                var textureName = layer.LipSyncBaseName + "0";
                var texture = LoadSpriteTexture(textureName, _memory);
                if (texture != null)
                {
                    layer.TextureName = textureName;
                    layer.Texture = texture;
                }
                layer.LipSyncFrame = 0;
            }
        }

        private static void AddCascade(
            List<RuntimePathCascade> target,
            BurikoOperationInvocation invocation,
            BurikoMemory memory)
        {
            target.Add(new RuntimePathCascade(
                Text(invocation, 0, memory),
                Text(invocation, 1, memory),
                Text(invocation, 2, memory).Split(new[] { ':' }, StringSplitOptions.RemoveEmptyEntries)));
        }

        private static IReadOnlyList<string> CascadeFolders(
            List<RuntimePathCascade> cascades,
            int index,
            string fallback)
        {
            return cascades.Count == 0
                ? new[] { fallback }
                : cascades[ClampIndex(index, cascades.Count)].Folders;
        }

        private static int ClampIndex(int index, int count)
        {
            return count <= 0 ? 0 : Math.Max(0, Math.Min(count - 1, index));
        }

        private float VoiceVolume(float scriptVolume)
        {
            return scriptVolume * ((_settings?.voiceVolume ?? 75) / 100f);
        }

        private void PlayTrackedVoice(
            int channel,
            int character,
            string filename,
            float volume,
            BurikoMemory memory)
        {
            _currentVoiceCharacter = character;
            _lastVoiceChannel = channel;
            _lastVoiceCharacter = character;
            _lastVoiceFilename = filename ?? string.Empty;
            _lastVoiceVolume = volume;
            _lastVoiceIssuedForDialogueSerial = DialogueSerial + 1;
            _audio.PlayVoice(channel, _lastVoiceFilename, volume, memory);
        }

        private static string Text(BurikoOperationInvocation invocation, int index, BurikoMemory memory)
        {
            return invocation.Arguments[index].AsString(memory);
        }

        private static int Int(BurikoOperationInvocation invocation, int index, BurikoMemory memory)
        {
            return invocation.Arguments[index].AsInt(memory);
        }

        private static string AddOgg(string name)
        {
            return name.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase) ? name : name + ".ogg";
        }

        private static BurikoHostResponse BurikoValueResponse(int value)
        {
            return new BurikoHostResponse(BurikoValue.FromInt(value));
        }

        private static BurikoHostResponse BurikoValueResponse(bool value)
        {
            return new BurikoHostResponse(BurikoValue.FromInt(value ? 1 : 0));
        }

        private void StartFilmTransition(float targetStrength, float duration)
        {
            var currentStrength = NegativeFilmStrength;
            _filmStrength = currentStrength;
            _filmTargetStrength = Mathf.Clamp01(targetStrength);
            _filmTransitionStartedAt = Time.unscaledTime;
            _filmTransitionDuration = Mathf.Max(0f, duration);
            if (_filmTransitionDuration <= 0f)
            {
                _filmStrength = _filmTargetStrength;
            }
        }

        private void StartFilmTransitionFrom(float currentStrength, float targetStrength,
            float duration)
        {
            _filmStrength = Mathf.Clamp01(currentStrength);
            _filmTargetStrength = Mathf.Clamp01(targetStrength);
            _filmTransitionStartedAt = Time.unscaledTime;
            _filmTransitionDuration = Mathf.Max(0f, duration);
            if (_filmTransitionDuration <= 0f)
            {
                _filmStrength = _filmTargetStrength;
            }
        }

        private BurikoHostResponse AnimationResponse(float duration, bool blocking)
        {
            if (!blocking || duration <= 0f)
            {
                return BurikoHostResponse.Continue;
            }

            _blockingAnimationUntil = Mathf.Max(_blockingAnimationUntil,
                Time.unscaledTime + duration);
            return new BurikoHostResponse(BurikoValue.Null, BurikoBlockReason.Host);
        }

        private void SetWindowVisibilityImmediate(bool visible)
        {
            WindowVisible = visible;
            _windowTransitionDuration = 0f;
            _windowTransitionFrom = visible ? 1f : 0f;
            _windowTransitionTo = _windowTransitionFrom;
            _windowTransitionStartedAt = Time.unscaledTime;
        }

        private void StartWindowTransition(bool visible, float duration)
        {
            var current = WindowOpacity;
            _windowTransitionFrom = current;
            _windowTransitionTo = visible ? 1f : 0f;
            _windowTransitionStartedAt = Time.unscaledTime;
            _windowTransitionDuration = Mathf.Max(0f, duration);
            WindowVisible = true;
            if (_windowTransitionDuration <= 0f)
            {
                WindowVisible = visible;
                _windowTransitionFrom = _windowTransitionTo;
            }
        }

        private void UpdateWindowTransition()
        {
            if (_windowTransitionDuration <= 0f ||
                Time.unscaledTime - _windowTransitionStartedAt < _windowTransitionDuration)
            {
                return;
            }

            _windowTransitionDuration = 0f;
            _windowTransitionFrom = _windowTransitionTo;
            WindowVisible = _windowTransitionTo > 0f;
        }

        private void StartFragment(
            string textureName,
            string style,
            float duration,
            BurikoMemory memory)
        {
            _fragmentTextureName = textureName ?? string.Empty;
            _fragmentTexture = LoadTexture(textureName, memory);
            _fragmentStyle = style ?? string.Empty;
            _fragmentStartedAt = Time.unscaledTime;
            _fragmentTransitionStartedAt = Time.unscaledTime;
            _fragmentTransitionDuration = Mathf.Max(0f, duration);
            _fragmentTransitionFrom = 0f;
            _fragmentTransitionTo = 1f;
        }

        private void StopFragment(float duration)
        {
            if (_fragmentTexture == null)
            {
                return;
            }

            _fragmentTransitionFrom = FragmentOpacity;
            _fragmentTransitionTo = 0f;
            _fragmentTransitionStartedAt = Time.unscaledTime;
            _fragmentTransitionDuration = Mathf.Max(0f, duration);
            if (_fragmentTransitionDuration <= 0f)
            {
                _fragmentTexture = null;
                _fragmentTextureName = string.Empty;
                _fragmentStyle = string.Empty;
            }
        }

        private void UpdateFragmentTransition()
        {
            if (_fragmentTexture == null || _fragmentTransitionDuration <= 0f ||
                Time.unscaledTime - _fragmentTransitionStartedAt < _fragmentTransitionDuration)
            {
                return;
            }

            _fragmentTransitionDuration = 0f;
            _fragmentTransitionFrom = _fragmentTransitionTo;
            if (_fragmentTransitionTo <= 0f)
            {
                _fragmentTexture = null;
                _fragmentTextureName = string.Empty;
                _fragmentStyle = string.Empty;
            }
        }

        private void StartScreenShake(int vector, int level, int attenuation, float swing,
            float duration)
        {
            _shakeVector = vector;
            _shakeIntensity = Mathf.Max(0f, level);
            _shakeAttenuation = Mathf.Clamp01(attenuation / 100f);
            _shakeSwingDuration = Mathf.Max(0.01f, swing);
            _shakeDuration = Mathf.Max(0f, duration);
            _shakeStartedAt = Time.unscaledTime;
        }

        private void CompletePresentationAnimations()
        {
            _backgroundTransitionStartedAt = Time.unscaledTime - _backgroundTransitionDuration;
            _previousBackgroundTexture = null;
            _backgroundTransitionMask = null;
            _previousSceneLayers.Clear();
            _shakeDuration = 0f;
            _windowTransitionDuration = 0f;
            _windowTransitionFrom = _windowTransitionTo;
            WindowVisible = _windowTransitionTo > 0f;
            _fragmentTransitionDuration = 0f;
            _fragmentTransitionFrom = _fragmentTransitionTo;
            if (_fragmentTransitionTo <= 0f)
            {
                _fragmentTexture = null;
                _fragmentTextureName = string.Empty;
                _fragmentStyle = string.Empty;
            }
            foreach (var pair in _layers)
            {
                pair.Value.CompleteTransition();
            }
        }

        private void ReportApproximated(BurikoOperationInvocation invocation)
        {
            if (_reportedOperations.Add(invocation.Specification.Code))
            {
                Debug.LogWarning(
                    "Buriko operation currently uses a mobile approximation: " +
                    invocation.Specification.Name + " (" + invocation.Specification.Code + ")");
            }
        }

    }

    public sealed class PresentationLayer
    {
        public int Id;
        public string TextureName;
        public Texture2D Texture;
        public int X;
        public int Y;
        public int Z;
        public int Priority;
        public float Alpha = 1f;
        public bool IsBustshot;
        public bool IsCentered;
        public float FromX;
        public float FromY;
        public float FromZ;
        public float FromAlpha = 1f;
        public float TransitionStartedAt;
        public float TransitionDuration;
        public Texture2D PreviousTexture;
        public float PreviousX;
        public float PreviousY;
        public float PreviousZ;
        public float PreviousAlpha;
        public bool PreviousIsBustshot;
        public bool PreviousIsCentered;
        public int OverrideWidth;
        public int OverrideHeight;
        public int PreviousOverrideWidth;
        public int PreviousOverrideHeight;
        public Texture2D MaskTexture;
        public string MaskName;
        public float MaskFuzziness = 0.45f;
        public bool MaskReverse;
        public int EaseType;
        public int CharacterId = -1;
        public string LipSyncBaseName;
        public string LipSyncRestName;
        public int LipSyncFrame;

        public float TransitionProgress
        {
            get
            {
                if (TransitionDuration <= 0f)
                {
                    return 1f;
                }
                var progress = Mathf.Clamp01((Time.unscaledTime - TransitionStartedAt) / TransitionDuration);
                return ApplyEase(progress, EaseType);
            }
        }

        public void BeginTransition(int x, int y, int z, float alpha, float duration,
            int easeType = 0)
        {
            GetRenderState(out FromX, out FromY, out FromZ, out FromAlpha);
            X = x;
            Y = y;
            Z = z;
            Alpha = Mathf.Clamp01(alpha);
            TransitionStartedAt = Time.unscaledTime;
            TransitionDuration = Mathf.Max(0f, duration);
            EaseType = easeType;
        }

        public void CompleteTransition()
        {
            FromX = X;
            FromY = Y;
            FromZ = Z;
            FromAlpha = Alpha;
            TransitionStartedAt = Time.unscaledTime;
            TransitionDuration = 0f;
            PreviousTexture = null;
            PreviousOverrideWidth = 0;
            PreviousOverrideHeight = 0;
            if (Alpha <= 0f)
            {
                MaskTexture = null;
                MaskName = null;
            }
        }

        private static float ApplyEase(float value, int easeType)
        {
            switch (easeType)
            {
                case 1:
                case 2:
                    return -(Mathf.Cos(Mathf.PI * value) - 1f) * 0.5f;
                case 3:
                    return value < 0.5f
                        ? 2f * value * value
                        : 1f - Mathf.Pow(-2f * value + 2f, 2f) * 0.5f;
                case 4:
                    return 1f - Mathf.Cos(value * Mathf.PI * 0.5f);
                case 5:
                    return Mathf.Sin(value * Mathf.PI * 0.5f);
                case 6:
                    return value * value;
                case 7:
                    return 1f - (1f - value) * (1f - value);
                case 8:
                    return value * value * value;
                case 9:
                    return 1f - Mathf.Pow(1f - value, 3f);
                case 10:
                    return value * value * value * value;
                case 11:
                    return 1f - Mathf.Pow(1f - value, 4f);
                case 12:
                case 14:
                    return value <= 0f ? 0f : Mathf.Pow(2f, 10f * value - 10f);
                case 13:
                case 15:
                    return value >= 1f ? 1f : 1f - Mathf.Pow(2f, -10f * value);
                default:
                    return value;
            }
        }

        public void GetRenderState(out float x, out float y, out float z, out float alpha)
        {
            var progress = TransitionProgress;
            x = Mathf.Lerp(FromX, X, progress);
            y = Mathf.Lerp(FromY, Y, progress);
            z = Mathf.Lerp(FromZ, Z, progress);
            alpha = Mathf.Lerp(FromAlpha, Alpha, progress);
        }

        public PresentationLayer CloneWithoutTexture()
        {
            return new PresentationLayer
            {
                Id = Id,
                TextureName = TextureName,
                X = X,
                Y = Y,
                Z = Z,
                Priority = Priority,
                Alpha = Alpha,
                IsBustshot = IsBustshot,
                IsCentered = IsCentered,
                OverrideWidth = OverrideWidth,
                OverrideHeight = OverrideHeight,
                CharacterId = CharacterId,
                LipSyncBaseName = LipSyncBaseName,
                LipSyncRestName = LipSyncRestName,
                LipSyncFrame = LipSyncFrame,
                MaskName = MaskName,
                MaskFuzziness = MaskFuzziness,
                MaskReverse = MaskReverse,
                EaseType = EaseType,
                FromX = X,
                FromY = Y,
                FromZ = Z,
                FromAlpha = Alpha
            };
        }
    }

    public sealed class RuntimePathCascade
    {
        public RuntimePathCascade(string nameEnglish, string nameAsian, string[] folders)
        {
            NameEnglish = nameEnglish ?? string.Empty;
            NameAsian = nameAsian ?? string.Empty;
            Folders = folders ?? Array.Empty<string>();
        }

        public string NameEnglish { get; }
        public string NameAsian { get; }
        public string[] Folders { get; }
        public string DisplayName => string.IsNullOrEmpty(NameEnglish) ? NameAsian : NameEnglish;
    }

    public sealed class RuntimeAudioSet
    {
        public RuntimeAudioSet(
            string nameEnglish,
            string nameAsian,
            int altBgm,
            int altBgmFlow,
            int altSe,
            int altSeFlow)
        {
            NameEnglish = nameEnglish ?? string.Empty;
            NameAsian = nameAsian ?? string.Empty;
            AltBgm = altBgm;
            AltBgmFlow = altBgmFlow;
            AltSe = altSe;
            AltSeFlow = altSeFlow;
        }

        public string NameEnglish { get; }
        public string NameAsian { get; }
        public int AltBgm { get; }
        public int AltBgmFlow { get; }
        public int AltSe { get; }
        public int AltSeFlow { get; }
        public string DisplayName => string.IsNullOrEmpty(NameEnglish) ? NameAsian : NameEnglish;
    }

    public sealed class UnityBurikoHostSnapshot
    {
        public UnityBurikoHostSnapshot(
            string backgroundName,
            PresentationLayer[] layers,
            string[] history,
            HistoryVoiceCue[] historyVoices,
            string speaker,
            string dialogue,
            bool windowVisible,
            bool titleVisible,
            int dialogueSerial,
            bool savingEnabled,
            bool interfaceEnabled,
            bool gameplayUiVisible,
            bool chapterPreviewVisible,
            bool chapterPreviewAccepted,
            bool fragmentChapterVisible,
            bool fragmentListVisible,
            int fragmentPage,
            int selectedFragmentId,
            bool appendNext,
            int lastVoiceChannel,
            int lastVoiceCharacter,
            string lastVoiceFilename,
            float lastVoiceVolume,
            int fontSize,
            int windowX,
            int windowY,
            int windowWidth,
            int windowHeight,
            string screenAspect,
            string fragmentTextureName,
            string fragmentStyle,
            string windowBackgroundName,
            float negativeFilmStrength,
            int messageSpeedOverride = -1)
        {
            BackgroundName = backgroundName;
            Layers = layers;
            History = history ?? Array.Empty<string>();
            HistoryVoices = historyVoices ?? Array.Empty<HistoryVoiceCue>();
            Speaker = speaker;
            Dialogue = dialogue;
            WindowVisible = windowVisible;
            TitleVisible = titleVisible;
            DialogueSerial = dialogueSerial;
            SavingEnabled = savingEnabled;
            InterfaceEnabled = interfaceEnabled;
            GameplayUiVisible = gameplayUiVisible;
            ChapterPreviewVisible = chapterPreviewVisible;
            ChapterPreviewAccepted = chapterPreviewAccepted;
            FragmentChapterVisible = fragmentChapterVisible;
            FragmentListVisible = fragmentListVisible;
            FragmentPage = Math.Max(0, fragmentPage);
            SelectedFragmentId = selectedFragmentId;
            AppendNext = appendNext;
            LastVoiceChannel = lastVoiceChannel;
            LastVoiceCharacter = lastVoiceCharacter;
            LastVoiceFilename = lastVoiceFilename ?? string.Empty;
            LastVoiceVolume = lastVoiceVolume;
            FontSize = fontSize;
            WindowX = windowX;
            WindowY = windowY;
            WindowWidth = windowWidth;
            WindowHeight = windowHeight;
            ScreenAspect = screenAspect;
            FragmentTextureName = fragmentTextureName ?? string.Empty;
            FragmentStyle = fragmentStyle ?? string.Empty;
            WindowBackgroundName = windowBackgroundName ?? string.Empty;
            NegativeFilmStrength = Mathf.Clamp01(negativeFilmStrength);
            MessageSpeedOverride = messageSpeedOverride;
        }

        public string BackgroundName { get; }
        public PresentationLayer[] Layers { get; }
        public string[] History { get; }
        public HistoryVoiceCue[] HistoryVoices { get; }
        public string Speaker { get; }
        public string Dialogue { get; }
        public bool WindowVisible { get; }
        public bool TitleVisible { get; }
        public int DialogueSerial { get; }
        public bool SavingEnabled { get; }
        public bool InterfaceEnabled { get; }
        public bool GameplayUiVisible { get; }
        public bool ChapterPreviewVisible { get; }
        public bool ChapterPreviewAccepted { get; }
        public bool FragmentChapterVisible { get; }
        public bool FragmentListVisible { get; }
        public int FragmentPage { get; }
        public int SelectedFragmentId { get; }
        public bool AppendNext { get; }
        public int LastVoiceChannel { get; }
        public int LastVoiceCharacter { get; }
        public string LastVoiceFilename { get; }
        public float LastVoiceVolume { get; }
        public int FontSize { get; }
        public int WindowX { get; }
        public int WindowY { get; }
        public int WindowWidth { get; }
        public int WindowHeight { get; }
        public string ScreenAspect { get; }
        public string FragmentTextureName { get; }
        public string FragmentStyle { get; }
        public string WindowBackgroundName { get; }
        public float NegativeFilmStrength { get; }
        public int MessageSpeedOverride { get; }
    }

    internal enum HigurashiFragmentViewState
    {
        Unviewed,
        Broken,
        BrokenButFixable,
        Viewed
    }

    [Serializable]
    internal sealed class HigurashiFragmentDefinition
    {
        public int Id;
        public string Title;
        public string Description;
        public string TitleJp;
        public string DescriptionJp;
        public string Script;
        public int[] Prereqs;
    }

    [Serializable]
    internal sealed class HigurashiFragmentCatalogDocument
    {
        public HigurashiFragmentDefinition[] Items;
    }

    internal sealed class HigurashiFragmentCatalog
    {
        private readonly List<HigurashiFragmentDefinition> _entries;

        private HigurashiFragmentCatalog(IEnumerable<HigurashiFragmentDefinition> entries)
        {
            _entries = new List<HigurashiFragmentDefinition>();
            if (entries == null)
            {
                return;
            }
            foreach (var entry in entries)
            {
                if (entry != null && entry.Id > 0 && !string.IsNullOrWhiteSpace(entry.Script))
                {
                    entry.Prereqs = entry.Prereqs ?? new int[0];
                    _entries.Add(entry);
                }
            }
            _entries.Sort((left, right) => left.Id.CompareTo(right.Id));
        }

        public static HigurashiFragmentCatalog Empty { get; } =
            new HigurashiFragmentCatalog(new HigurashiFragmentDefinition[0]);

        public static HigurashiFragmentCatalog Load(string streamingAssetsRoot)
        {
            var path = Path.Combine(streamingAssetsRoot ?? string.Empty, "Data", "fragmentdata.txt");
            if (!File.Exists(path))
            {
                return Empty;
            }

            try
            {
                var json = File.ReadAllText(path);
                var document = JsonUtility.FromJson<HigurashiFragmentCatalogDocument>(
                    "{\"Items\":" + json + "}");
                return document == null || document.Items == null
                    ? Empty
                    : new HigurashiFragmentCatalog(document.Items);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Unable to load Higurashi fragment data: " + exception.Message);
                return Empty;
            }
        }

        public HigurashiFragmentDefinition Find(int id)
        {
            for (var i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].Id == id)
                {
                    return _entries[i];
                }
            }
            return null;
        }

        public List<HigurashiFragmentDefinition> GetVisible(BurikoMemory memory)
        {
            var result = new List<HigurashiFragmentDefinition>();
            if (memory == null)
            {
                return result;
            }

            for (var i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                if (entry.Id <= 50 ||
                    (entry.Id == 51 && ArePrerequisitesMet(entry, memory)) ||
                    (entry.Id == 52 && memory.GetGlobalFlag("GFlag_GameClear") != 0 &&
                     memory.GetLocalFlag("LFragmentMiss") == 0 &&
                     ArePrerequisitesMet(entry, memory)))
                {
                    result.Add(entry);
                }
            }
            return result;
        }

        public bool ArePrerequisitesMet(HigurashiFragmentDefinition entry, BurikoMemory memory)
        {
            if (entry == null || memory == null)
            {
                return false;
            }
            for (var i = 0; i < entry.Prereqs.Length; i++)
            {
                if (memory.GetLocalFlag(FragmentReadFlag(entry.Prereqs[i])) == 0)
                {
                    return false;
                }
            }
            return true;
        }

        public HigurashiFragmentViewState GetViewState(
            HigurashiFragmentDefinition entry,
            BurikoMemory memory)
        {
            var isAvailable = ArePrerequisitesMet(entry, memory);
            var state = memory == null ? 0 : memory.GetLocalFlag(FragmentStatusFlag(entry.Id));
            if (state == 1)
            {
                return HigurashiFragmentViewState.Viewed;
            }
            if (state == 2)
            {
                return isAvailable
                    ? HigurashiFragmentViewState.BrokenButFixable
                    : HigurashiFragmentViewState.Broken;
            }
            return HigurashiFragmentViewState.Unviewed;
        }

        public string BuildPrerequisiteSummary(HigurashiFragmentDefinition entry, BurikoMemory memory)
        {
            if (entry == null)
            {
                return string.Empty;
            }
            if (entry.Prereqs.Length == 0)
            {
                return "无需前置条件。";
            }

            var lines = new List<string>();
            for (var i = 0; i < entry.Prereqs.Length; i++)
            {
                var prerequisiteId = entry.Prereqs[i];
                var prerequisite = Find(prerequisiteId);
                var title = prerequisite == null || string.IsNullOrEmpty(prerequisite.Title)
                    ? "碎片 " + prerequisiteId.ToString("00")
                    : prerequisite.Title;
                var met = memory != null &&
                          memory.GetLocalFlag(FragmentReadFlag(prerequisiteId)) != 0;
                lines.Add((met ? "✓ " : "○ ") + title);
            }
            return string.Join("\n", lines.ToArray());
        }

        public static string FragmentReadFlag(int id)
        {
            return "FragmentRead" + id.ToString("00");
        }

        public static string FragmentStatusFlag(int id)
        {
            return "FragmentStatus" + id.ToString("00");
        }
    }

    [Serializable]
    internal sealed class HigurashiTipsCatalogDocument
    {
        public HigurashiTipDefinition[] Items;
    }

    [Serializable]
    internal sealed class HigurashiTipDefinition
    {
        public int Id;
        public string Script;
        public int UnlockChapter;
        public string Title;
        public string TitleJp;
        public string Description;
        [NonSerialized] public string PreviewName;
        [NonSerialized] public string SelectedPreviewName;

        public string DisplayTitle => !string.IsNullOrWhiteSpace(Title)
            ? Title
            : (!string.IsNullOrWhiteSpace(TitleJp) ? TitleJp : "TIPS " + (Id + 1).ToString("00"));
    }

    internal sealed class HigurashiTipsCatalog
    {
        private readonly List<HigurashiTipDefinition> _entries;

        private HigurashiTipsCatalog(IEnumerable<HigurashiTipDefinition> entries)
        {
            _entries = new List<HigurashiTipDefinition>();
            if (entries == null)
            {
                return;
            }
            foreach (var entry in entries)
            {
                if (entry != null && entry.Id >= 0 && !string.IsNullOrWhiteSpace(entry.Script))
                {
                    entry.PreviewName = BuildPreviewName(entry.Script);
                    entry.SelectedPreviewName = string.IsNullOrWhiteSpace(entry.PreviewName)
                        ? string.Empty
                        : entry.PreviewName + "_j";
                    _entries.Add(entry);
                }
            }
            _entries.Sort((left, right) => left.Id.CompareTo(right.Id));
        }

        public static HigurashiTipsCatalog Empty { get; } =
            new HigurashiTipsCatalog(new HigurashiTipDefinition[0]);

        public bool IsEmpty => _entries.Count == 0;

        public bool HasVisibleThrough(int chapter)
        {
            for (var i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].UnlockChapter <= chapter)
                {
                    return true;
                }
            }
            return false;
        }

        public bool HasEntryAtChapter(int chapter)
        {
            for (var i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].UnlockChapter == chapter)
                {
                    return true;
                }
            }
            return false;
        }

        public static HigurashiTipsCatalog Load(string installedGameDataRoot)
        {
            try
            {
                var path = Path.Combine(installedGameDataRoot ?? string.Empty, "tips.json");
                if (!File.Exists(path))
                {
                    path = Path.Combine(installedGameDataRoot ?? string.Empty,
                        "StreamingAssets", "Data", "tips.txt");
                    if (!File.Exists(path))
                    {
                        return Empty;
                    }
                }
                var json = File.ReadAllText(path);
                var start = json.IndexOf('[');
                var end = json.LastIndexOf(']');
                if (start < 0 || end < start)
                {
                    return Empty;
                }
                var document = JsonUtility.FromJson<HigurashiTipsCatalogDocument>(
                    "{\"Items\":" + json.Substring(start, end - start + 1) + "}");
                if (document == null || document.Items == null)
                {
                    return Empty;
                }
                HigurashiTipsLocalization.Apply(
                    HigurashiActiveChapter.Profile.EpisodeNumber, document.Items);
                return new HigurashiTipsCatalog(document.Items);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Unable to load Higurashi tips data: " + exception.Message);
                return Empty;
            }
        }

        public HigurashiTipDefinition Find(int id)
        {
            for (var i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].Id == id)
                {
                    return _entries[i];
                }
            }
            return null;
        }

        private static string BuildPreviewName(string script)
        {
            var marker = "_tips_";
            var index = script == null ? -1 : script.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                return string.Empty;
            }
            var suffix = script.Substring(index + marker.Length);
            if (!int.TryParse(suffix, out var number))
            {
                return string.Empty;
            }
            var prefix = script.Substring(0, index);
            if (prefix.StartsWith("_", StringComparison.Ordinal))
            {
                // Episodes 05-07 use gettip_<arc><number>.png while the
                // first four arcs use <arc><number>.png.
                prefix = "gettip_" + prefix.Substring(1);
            }
            return "tips/" + prefix + number.ToString("000");
        }

        public List<HigurashiTipDefinition> GetVisible(
            BurikoMemory memory,
            int scope,
            int chapterOverride = -1)
        {
            var result = new List<HigurashiTipDefinition>();
            var chapter = chapterOverride >= 0
                ? chapterOverride
                : (memory == null ? 0 : Math.Max(0, memory.GetLocalFlag("ChapterNumber")));
            for (var i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                var isNew = entry.UnlockChapter == chapter;
                var isUnlocked = entry.UnlockChapter <= chapter;
                if ((scope == 0 && isNew) || (scope != 0 && isUnlocked))
                {
                    result.Add(entry);
                }
            }
            return result;
        }
    }

    [Serializable]
    internal sealed class HigurashiTipsLocalizationDocument
    {
        public HigurashiTipsLocalizationEntry[] Items;
    }

    [Serializable]
    internal sealed class HigurashiTipsLocalizationEntry
    {
        public int Episode;
        public int Id;
        public string Title;
    }

    internal static class HigurashiTipsLocalization
    {
        private static HigurashiTipsLocalizationEntry[] _items;

        public static void Apply(int episode, HigurashiTipDefinition[] entries)
        {
            EnsureLoaded();
            if (_items == null)
            {
                return;
            }
            for (var i = 0; i < entries.Length; i++)
            {
                if (entries[i].UnlockChapter <= 0)
                {
                    entries[i].UnlockChapter = GetUnlockChapter(episode, entries[i].Id);
                }
                for (var j = 0; j < _items.Length; j++)
                {
                    if (_items[j].Episode == episode && _items[j].Id == entries[i].Id &&
                        !string.IsNullOrWhiteSpace(_items[j].Title))
                    {
                        entries[i].Title = _items[j].Title;
                        break;
                    }
                }
            }
        }

        private static int GetUnlockChapter(int episode, int id)
        {
            int[] chapters;
            int firstId;
            switch (episode)
            {
                case 5:
                    firstId = 1;
                    chapters = new[] { 1, 2, 3, 4, 4, 5, 6, 7, 7, 8, 8, 8,
                        9, 9, 9, 10, 10, 11, 12, 12, 13, 14, 14 };
                    break;
                case 6:
                    firstId = 24;
                    chapters = new[] { 1, 2, 3, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13 };
                    break;
                case 7:
                    firstId = 38;
                    chapters = new[] { 1, 2, 3, 4, 5, 6, 6, 7, 7, 8, 10, 11, 12 };
                    break;
                case 8:
                    return id == 51 ? 10 : int.MaxValue;
                default:
                    return 0;
            }
            var index = id - firstId;
            return index >= 0 && index < chapters.Length ? chapters[index] : int.MaxValue;
        }

        private static void EnsureLoaded()
        {
            if (_items != null)
            {
                return;
            }
            var asset = Resources.Load<TextAsset>("TipsPreviews/titles");
            if (asset == null)
            {
                _items = Array.Empty<HigurashiTipsLocalizationEntry>();
                return;
            }
            var document = JsonUtility.FromJson<HigurashiTipsLocalizationDocument>(asset.text);
            _items = document == null || document.Items == null
                ? Array.Empty<HigurashiTipsLocalizationEntry>()
                : document.Items;
        }
    }

    internal sealed class UnityAssetLoader
    {
        private readonly AssetCascadeResolver _resolver;
        private readonly Dictionary<string, Texture2D> _textures =
            new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);

        public UnityAssetLoader(string installedGameDataRoot)
        {
            _resolver = new AssetCascadeResolver(installedGameDataRoot);
        }

        public Texture2D LoadTexture(
            string textureName,
            IReadOnlyList<string> folders,
            bool preferAsianVariant)
        {
            var normalized = textureName.Replace('\\', '/').TrimStart('/').ToLowerInvariant();
            var extension = Path.GetExtension(normalized);
            string path;
            if (!string.IsNullOrEmpty(extension))
            {
                if (!_resolver.TryResolve(normalized, folders, out path, true))
                {
                    return null;
                }
            }
            else if (preferAsianVariant && _resolver.TryResolve(normalized + "_j.png", folders, out path, true))
            {
            }
            else if (!_resolver.TryResolve(normalized + ".png", folders, out path, true))
            {
                return null;
            }

            if (_textures.TryGetValue(path, out var cached))
            {
                return cached;
            }

            try
            {
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, true);
                if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(path), false))
                {
                    UnityEngine.Object.Destroy(texture);
                    return null;
                }

                texture.name = textureName;
                texture.filterMode = FilterMode.Bilinear;
                texture.wrapMode = TextureWrapMode.Clamp;
                _textures.Add(path, texture);
                return texture;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Unable to load texture " + path + ": " + exception.Message);
                return null;
            }
        }
    }

    public readonly struct HistoryVoiceCue
    {
        public static HistoryVoiceCue None { get; } = new HistoryVoiceCue(-1, -1, string.Empty, 0f);

        public HistoryVoiceCue(int channel, int character, string filename, float volume)
        {
            Channel = channel;
            Character = character;
            Filename = filename ?? string.Empty;
            Volume = volume;
        }

        public int Channel { get; }
        public int Character { get; }
        public string Filename { get; }
        public float Volume { get; }
        public bool IsPlayable => Channel >= 0 && !string.IsNullOrWhiteSpace(Filename) && Volume > 0f;
    }

    public sealed class RuntimeBgmState
    {
        public RuntimeBgmState(int channel, string filename, float volume)
        {
            Channel = channel;
            Filename = filename ?? string.Empty;
            Volume = volume;
        }

        public int Channel { get; }
        public string Filename { get; }
        public float Volume { get; }
    }

    internal enum RuntimeAudioKind
    {
        Bgm,
        Se,
        Voice
    }

        internal sealed class UnityAudioService : MonoBehaviour
        {
        private readonly Dictionary<string, int> _generations = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _pendingGenerations = new Dictionary<string, int>();
        private readonly Dictionary<string, AudioSource> _sources = new Dictionary<string, AudioSource>();
        private readonly Dictionary<int, RuntimeBgmState> _bgmState =
            new Dictionary<int, RuntimeBgmState>();
            private AssetCascadeResolver _resolver;
            private UnityBurikoHost _host;
            private float _bgmMasterVolume = 1f;

        public void Initialize(string installedGameDataRoot, UnityBurikoHost host)
        {
            _resolver = new AssetCascadeResolver(installedGameDataRoot);
            _host = host;
        }

        public void PlayBgm(int channel, string filename, float volume, BurikoMemory memory)
        {
            _bgmState[channel] = new RuntimeBgmState(channel, filename, volume);
            Play(RuntimeAudioKind.Bgm, channel, filename, volume * _bgmMasterVolume,
                _host.CurrentBgmFolders(memory), true);
        }

        public void PlaySe(int channel, string filename, float volume, BurikoMemory memory)
        {
            Play(RuntimeAudioKind.Se, channel, filename, volume, _host.CurrentSeFolders(memory), false);
        }

        public void PlayVoice(int channel, string filename, float volume, BurikoMemory memory)
        {
            Play(RuntimeAudioKind.Voice, channel, filename, volume, new[] { "voice" }, false);
        }

        public void StopBgm(int channel)
        {
            _bgmState.Remove(channel);
            Stop(RuntimeAudioKind.Bgm, channel);
        }
        public void StopSe(int channel) => Stop(RuntimeAudioKind.Se, channel);

        public bool AnyVoicePlaying()
        {
            var prefix = RuntimeAudioKind.Voice + ":";
            foreach (var pair in _pendingGenerations)
            {
                if (pair.Key.StartsWith(prefix, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            foreach (var pair in _sources)
            {
                if (pair.Key.StartsWith(prefix, StringComparison.Ordinal) && pair.Value.isPlaying)
                {
                    return true;
                }
            }
            return false;
        }

        public void StopAllVoices()
        {
            StopAllWithPrefix(RuntimeAudioKind.Voice + ":");
        }

        public void StopNonBgm()
        {
            StopAllWithPrefix(RuntimeAudioKind.Se + ":");
            StopAllWithPrefix(RuntimeAudioKind.Voice + ":");
        }

        public void StopAll()
        {
            StopAllWithPrefix(string.Empty);
            _bgmState.Clear();
        }

        public RuntimeBgmState[] CaptureBgmState()
        {
            var result = new RuntimeBgmState[_bgmState.Count];
            var index = 0;
            foreach (var pair in _bgmState)
            {
                var state = pair.Value;
                result[index++] = new RuntimeBgmState(state.Channel, state.Filename, state.Volume);
            }
            return result;
        }

        public void RestoreBgmState(RuntimeBgmState[] state, BurikoMemory memory)
        {
            var desired = new Dictionary<int, RuntimeBgmState>();
            if (state != null)
            {
                for (var i = 0; i < state.Length; i++)
                {
                    var item = state[i];
                    if (item != null && !string.IsNullOrEmpty(item.Filename))
                    {
                        desired[item.Channel] = item;
                    }
                }
            }

            var channels = new List<int>(_bgmState.Keys);
            for (var i = 0; i < channels.Count; i++)
            {
                var channel = channels[i];
                if (!desired.TryGetValue(channel, out var target) ||
                    !_bgmState.TryGetValue(channel, out var current) ||
                    !string.Equals(current.Filename, target.Filename,
                        StringComparison.OrdinalIgnoreCase))
                {
                    StopBgm(channel);
                }
            }

            foreach (var pair in desired)
            {
                var item = pair.Value;
                if (_bgmState.TryGetValue(item.Channel, out var current) &&
                    string.Equals(current.Filename, item.Filename,
                        StringComparison.OrdinalIgnoreCase) &&
                    IsChannelActive(RuntimeAudioKind.Bgm, item.Channel))
                {
                    _bgmState[item.Channel] =
                        new RuntimeBgmState(item.Channel, item.Filename, item.Volume);
                    SetBgmVolume(item.Channel, item.Volume);
                    continue;
                }

                PlayBgm(item.Channel, item.Filename, item.Volume, memory);
            }
        }

        private bool IsChannelActive(RuntimeAudioKind kind, int channel)
        {
            var key = Key(kind, channel);
            return _pendingGenerations.ContainsKey(key) ||
                   (_sources.TryGetValue(key, out var source) && source.isPlaying);
        }

        private void StopAllWithPrefix(string prefix)
        {
            var keys = new HashSet<string>();
            foreach (var pair in _generations)
            {
                if (pair.Key.StartsWith(prefix, StringComparison.Ordinal))
                {
                    keys.Add(pair.Key);
                }
            }
            foreach (var pair in _pendingGenerations)
            {
                if (pair.Key.StartsWith(prefix, StringComparison.Ordinal))
                {
                    keys.Add(pair.Key);
                }
            }
            foreach (var pair in _sources)
            {
                if (pair.Key.StartsWith(prefix, StringComparison.Ordinal))
                {
                    keys.Add(pair.Key);
                }
            }
            foreach (var key in keys)
            {
                NextGeneration(key);
                _pendingGenerations.Remove(key);
                if (_sources.TryGetValue(key, out var source))
                {
                    source.Stop();
                    if (source.clip != null)
                    {
                        Destroy(source.clip);
                        source.clip = null;
                    }
                }
            }
        }

        public void SetBgmVolume(int channel, float volume)
        {
            if (_bgmState.TryGetValue(channel, out var state))
            {
                _bgmState[channel] = new RuntimeBgmState(channel, state.Filename, volume);
            }
            if (_sources.TryGetValue(Key(RuntimeAudioKind.Bgm, channel), out var source))
            {
                source.volume = Mathf.Clamp01(volume * _bgmMasterVolume);
            }
        }

        public void SetBgmMasterVolume(float volume)
        {
            _bgmMasterVolume = Mathf.Clamp01(volume);
            foreach (var pair in _bgmState)
            {
                if (_sources.TryGetValue(Key(RuntimeAudioKind.Bgm, pair.Key), out var source))
                {
                    source.volume = Mathf.Clamp01(pair.Value.Volume * _bgmMasterVolume);
                }
            }
        }

        public bool IsChannelPlaying(RuntimeAudioKind kind, int channel)
        {
            return _sources.TryGetValue(Key(kind, channel), out var source) && source.isPlaying;
        }

        private void Play(
            RuntimeAudioKind kind,
            int channel,
            string filename,
            float volume,
            IReadOnlyList<string> folders,
            bool loop)
        {
            if (_resolver == null || !_resolver.TryResolve(filename.ToLowerInvariant(), folders, out var path))
            {
                Debug.LogWarning("Audio asset was not found: " + filename);
                return;
            }

            var key = Key(kind, channel);
            var generation = NextGeneration(key);
            _pendingGenerations[key] = generation;
            StartCoroutine(LoadAndPlay(key, generation, path, volume, loop));
        }

        private IEnumerator LoadAndPlay(
            string key,
            int generation,
            string path,
            float volume,
            bool loop)
        {
            using (var request = UnityWebRequestMultimedia.GetAudioClip(new Uri(path).AbsoluteUri, AudioType.OGGVORBIS))
            {
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                {
                    ClearPending(key, generation);
                    Debug.LogWarning("Unable to load audio " + path + ": " + request.error);
                    yield break;
                }

                var clip = DownloadHandlerAudioClip.GetContent(request);
                if (!_generations.TryGetValue(key, out var currentGeneration) || currentGeneration != generation)
                {
                    ClearPending(key, generation);
                    Destroy(clip);
                    yield break;
                }

                var source = GetOrCreateSource(key);
                ClearPending(key, generation);
                if (source.clip != null)
                {
                    Destroy(source.clip);
                }
                source.clip = clip;
                source.volume = Mathf.Clamp01(volume);
                source.loop = loop;
                source.Play();
            }
        }

        private void Stop(RuntimeAudioKind kind, int channel)
        {
            var key = Key(kind, channel);
            NextGeneration(key);
            _pendingGenerations.Remove(key);
            if (_sources.TryGetValue(key, out var source))
            {
                source.Stop();
                if (source.clip != null)
                {
                    Destroy(source.clip);
                    source.clip = null;
                }
            }
        }

        private AudioSource GetOrCreateSource(string key)
        {
            if (_sources.TryGetValue(key, out var source))
            {
                return source;
            }

            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0;
            _sources.Add(key, source);
            return source;
        }

        private int NextGeneration(string key)
        {
            var generation = _generations.TryGetValue(key, out var current) ? current + 1 : 1;
            _generations[key] = generation;
            return generation;
        }

        private void ClearPending(string key, int generation)
        {
            if (_pendingGenerations.TryGetValue(key, out var pending) && pending == generation)
            {
                _pendingGenerations.Remove(key);
            }
        }

        private static string Key(RuntimeAudioKind kind, int channel)
        {
            return kind + ":" + channel;
        }
    }
}
