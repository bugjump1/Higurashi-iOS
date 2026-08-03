using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Higurashi.IOS.Buriko;
using Higurashi.IOS.Compatibility;
using Higurashi.IOS.Data;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Video;

namespace Higurashi.IOS.Runtime.Buriko
{
    public sealed class UnityBurikoHost : MonoBehaviour, IBurikoHost
    {
        private const int PersistentStateMagic = 0x31504848; // HHP1
        private readonly List<RuntimePathCascade> _artSets = new List<RuntimePathCascade>();
        private readonly List<RuntimePathCascade> _bgmSets = new List<RuntimePathCascade>();
        private readonly List<RuntimePathCascade> _seSets = new List<RuntimePathCascade>();
        private readonly List<RuntimeAudioSet> _audioSets = new List<RuntimeAudioSet>();
        private readonly SortedDictionary<int, PresentationLayer> _layers =
            new SortedDictionary<int, PresentationLayer>();
        private readonly List<string> _history = new List<string>();
        private readonly HashSet<short> _reportedOperations = new HashSet<short>();
        private UnityAssetLoader _assets;
        private UnityAudioService _audio;
        private HigurashiUserSettings _settings;
        private string _streamingAssetsRoot;
        private string _backgroundName;
        private Texture2D _backgroundTexture;
        private Texture2D _previousBackgroundTexture;
        private float _backgroundTransitionStartedAt;
        private float _backgroundTransitionDuration;
        private float _dialogueRevealStartedAt;
        private int _dialogueRevealStartIndex;
        private bool _dialogueRevealForced;
        private int _currentVoiceCharacter = -1;
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
        public bool GameplayUiVisible { get; private set; }
        public bool SavingEnabled { get; private set; } = true;
        public bool InterfaceEnabled { get; private set; } = true;
        public bool HistoryVisible { get; set; }
        public bool ChoiceVisible => Choices.Count > 0;
        public List<string> Choices { get; } = new List<string>();
        public int DialogueSerial { get; private set; }
        public Texture2D BackgroundTexture => _backgroundTexture;
        public Texture2D PreviousBackgroundTexture => _previousBackgroundTexture;
        public float BackgroundTransitionProgress => _backgroundTransitionDuration <= 0f
            ? 1f
            : Mathf.Clamp01((Time.unscaledTime - _backgroundTransitionStartedAt) /
                            _backgroundTransitionDuration);
        public Texture MovieTexture => _videoPlayer != null ? _videoPlayer.texture : null;
        public bool MovieVisible { get; private set; }
        public IReadOnlyDictionary<int, PresentationLayer> Layers => _layers;
        public IReadOnlyList<string> History => _history;
        public IReadOnlyList<RuntimePathCascade> ArtSets => _artSets;
        public IReadOnlyList<RuntimeAudioSet> AudioSets => _audioSets;
        public int FontSize { get; private set; } = 30;
        public int WindowX { get; private set; }
        public int WindowY { get; private set; }
        public int WindowWidth { get; private set; } = 1200;
        public int WindowHeight { get; private set; } = 250;
        public string ScreenAspect { get; private set; } = "1.7777778";
        public event Action MovieFinished;

        public bool IsVoicePlaying => _audio != null && _audio.AnyVoicePlaying();
        public bool IsOpeningChoice => ChoiceVisible && Dialogue.IndexOf("OP 动画", StringComparison.OrdinalIgnoreCase) >= 0;
        public bool IsDialogueRevealComplete => VisibleDialogueLength >= Dialogue.Length;
        public string VisibleDialogue => Dialogue.Substring(0, VisibleDialogueLength);

        private int VisibleDialogueLength
        {
            get
            {
                if (_dialogueRevealForced || string.IsNullOrEmpty(Dialogue))
                {
                    return Dialogue.Length;
                }
                var speed = _settings == null ? 50 : Mathf.Clamp(_settings.textSpeed, 0, 100);
                var charactersPerSecond = Mathf.Lerp(18f, 90f, speed / 100f);
                var animated = Mathf.FloorToInt((Time.unscaledTime - _dialogueRevealStartedAt) * charactersPerSecond);
                return Mathf.Clamp(_dialogueRevealStartIndex + animated, 0, Dialogue.Length);
            }
        }

        public void Initialize(string installedGameDataRoot, HigurashiUserSettings settings)
        {
            _settings = settings ?? new HigurashiUserSettings();
            _streamingAssetsRoot = Path.Combine(installedGameDataRoot, "StreamingAssets");
            _assets = new UnityAssetLoader(installedGameDataRoot);
            _audio = gameObject.AddComponent<UnityAudioService>();
            _audio.Initialize(installedGameDataRoot, this);
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
            UpdateLipSync();
        }

        public void ApplySettings(BurikoMemory memory)
        {
            if (_settings == null)
            {
                return;
            }

            var artIndex = ClampIndex(_settings.artSetIndex, _artSets.Count);
            _settings.artSetIndex = artIndex;
            memory.SetGlobalFlag("GArtStyle", artIndex);
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
        }

        public BurikoHostResponse Execute(BurikoOperationInvocation invocation, BurikoMemory memory)
        {
            _memory = memory;
            switch (invocation.Specification.Code)
            {
                case 13:
                    SavingEnabled = invocation.Arguments[0].AsBool(memory);
                    return BurikoHostResponse.Continue;
                case 85:
                    InterfaceEnabled = invocation.Arguments[0].AsBool(memory);
                    return BurikoHostResponse.Continue;
                case 15:
                case 19:
                case 20:
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
                case 45:
                case 46:
                case 61:
                case 68:
                case 70:
                case 71:
                case 72:
                case 73:
                case 74:
                case 75:
                case 76:
                case 77:
                case 78:
                case 81:
                case 82:
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
                    ReportApproximated(invocation);
                    return BurikoHostResponse.Continue;
                case 16:
                    return SetDialogue(
                        Text(invocation, 0, memory),
                        Text(invocation, 1, memory),
                        Text(invocation, 2, memory),
                        Text(invocation, 3, memory),
                        Int(invocation, 4, memory));
                case 17:
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
                    WindowVisible = false;
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
                    _currentVoiceCharacter = -1;
                    _audio.PlayVoice(
                        Int(invocation, 0, memory),
                        AddOgg(Text(invocation, 1, memory)),
                        VoiceVolume(Int(invocation, 2, memory) / 128f),
                        memory);
                    return BurikoHostResponse.Continue;
                case 47:
                    SetBackground(Text(invocation, 0, memory), memory, false,
                        Int(invocation, 1, memory) / 1000f);
                    return BurikoHostResponse.Continue;
                case 50:
                    SetBackground(Text(invocation, 0, memory), memory, true,
                        Int(invocation, 1, memory) / 1000f);
                    return BurikoHostResponse.Continue;
                case 51:
                    if (string.Equals(Text(invocation, 0, memory), "black", StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(_backgroundName, "07th-mod", StringComparison.OrdinalIgnoreCase))
                    {
                        SetBackground("haikei", memory, true, 1f);
                        CreditsVisible = true;
                        CreditsPage = 1;
                        _creditsPageChangedAt = Time.unscaledTime;
                        WindowVisible = false;
                        return new BurikoHostResponse(BurikoValue.Null, BurikoBlockReason.Host);
                    }
                    SetBackground(Text(invocation, 0, memory), memory, true,
                        Int(invocation, 4, memory) / 1000f);
                    return BurikoHostResponse.Continue;
                case 52:
                    SetBackground(Text(invocation, 0, memory), memory, true,
                        Int(invocation, 2, memory) / 1000f);
                    return BurikoHostResponse.Continue;
                case 48:
                    SetBackground("black", memory, false, Int(invocation, 0, memory) / 1000f);
                    return BurikoHostResponse.Continue;
                case 53:
                    SetBackground("black", memory, true, Int(invocation, 0, memory) / 1000f);
                    return BurikoHostResponse.Continue;
                case 54:
                    SetBackground("black", memory, true, Int(invocation, 3, memory) / 1000f);
                    return BurikoHostResponse.Continue;
                case 49:
                    SetBackground(Text(invocation, 0, memory), memory, false,
                        Int(invocation, 3, memory) / 1000f);
                    return BurikoHostResponse.Continue;
                case 55:
                    DrawAnimatedLayer(invocation, memory, true, 0, 1, 2, 3, 4, 5, 6, 7, 8, 13, 14);
                    return BurikoHostResponse.Continue;
                case 56:
                    MoveBustshot(invocation, memory);
                    return BurikoHostResponse.Continue;
                case 57:
                    FadeBustshot(invocation, memory);
                    return BurikoHostResponse.Continue;
                case 64:
                    FadeLayer(Int(invocation, 0, memory), Int(invocation, 1, memory) / 1000f);
                    return BurikoHostResponse.Continue;
                case 65:
                    FadeLayer(Int(invocation, 0, memory), Int(invocation, 3, memory) / 1000f);
                    return BurikoHostResponse.Continue;
                case 58:
                    DrawAnimatedLayer(invocation, memory, true, 0, 1, 4, 5, 10, 6, 7, 8, 9, 12, 13);
                    return BurikoHostResponse.Continue;
                case 59:
                    DrawLayer(1000, Text(invocation, 0, memory), 213, 131, 0, 1000, memory,
                        false, 1f, Int(invocation, 1, memory) / 1000f);
                    return BurikoHostResponse.Continue;
                case 60:
                    FadeLayer(1000, Int(invocation, 0, memory) / 1000f);
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
                    return BurikoHostResponse.Continue;
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
                    return BurikoHostResponse.Continue;
                case 66:
                    MoveLayer(invocation, memory);
                    return BurikoHostResponse.Continue;
                case 67:
                    ReportApproximated(invocation);
                    return BurikoHostResponse.Continue;
                case 69:
                    ReportApproximated(invocation);
                    return BurikoHostResponse.Continue;
                case 79:
                    FadeLayerRange(1, 19, Int(invocation, 0, memory) / 1000f);
                    return BurikoHostResponse.Continue;
                case 80:
                    FadeLayer(Int(invocation, 0, memory), Int(invocation, 3, memory) / 1000f);
                    return BurikoHostResponse.Continue;
                case 98:
                    FadeLayerRange(2, 3, Int(invocation, 0, memory) / 1000f);
                    return BurikoHostResponse.Continue;
                case 99:
                    FadeLayerRange(5, 8, Int(invocation, 0, memory) / 1000f);
                    return BurikoHostResponse.Continue;
                case 89:
                    ChapterPreviewVisible = true;
                    _chapterPreviewAccepted = false;
                    GameplayUiVisible = false;
                    WindowVisible = false;
                    return new BurikoHostResponse(BurikoValue.Null, BurikoBlockReason.Host);
                case 101:
                    TitleVisible = true;
                    ChapterPreviewVisible = false;
                    GameplayUiVisible = false;
                    WindowVisible = false;
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
                    return BurikoHostResponse.Continue;
                case 129:
                    DrawModCharacter(invocation, memory, true);
                    return BurikoHostResponse.Continue;
                case 130:
                    _currentVoiceCharacter = Int(invocation, 1, memory);
                    _audio.PlayVoice(
                        Int(invocation, 0, memory),
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
                    return BurikoHostResponse.Continue;
                case 139:
                    _artSets.Clear();
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
            TitleVisible = false;
            ChapterPreviewVisible = false;
            GameplayUiVisible = false;
            WindowVisible = false;
            return true;
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
            WindowVisible = false;
            return true;
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

            if (CreditsPage == 1)
            {
                CreditsPage = 2;
                _creditsPageChangedAt = Time.unscaledTime;
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

            memory.SetLocalFlag("SelectResult", index);
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
            ResetLipSyncFrames();
        }

        public void ToggleWindow()
        {
            WindowVisible = !WindowVisible;
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
                Speaker,
                Dialogue,
                WindowVisible,
                TitleVisible,
                DialogueSerial);
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
            }
        }

        public void ReadPersistentState(Stream input, BurikoMemory memory)
        {
            using (var reader = new BinaryReader(input, System.Text.Encoding.UTF8, true))
            {
                if (reader.ReadInt32() != PersistentStateMagic)
                {
                    throw new InvalidDataException("This is not a Higurashi iOS presentation state.");
                }
                _backgroundName = reader.ReadString();
                Speaker = reader.ReadString();
                Dialogue = reader.ReadString();
                WindowVisible = reader.ReadBoolean();
                TitleVisible = reader.ReadBoolean();
                DialogueSerial = reader.ReadInt32();
                FontSize = reader.ReadInt32();
                WindowX = reader.ReadInt32();
                WindowY = reader.ReadInt32();
                WindowWidth = reader.ReadInt32();
                WindowHeight = reader.ReadInt32();
                ScreenAspect = reader.ReadString();
                ReadStrings(reader, _history, 500);
                ReadStrings(reader, Choices, 100);
                var layerCount = ReadCount(reader, 10000, "presentation layer");
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
                    layer.Texture = LoadTexture(layer.TextureName, memory);
                    _layers[layer.Id] = layer;
                }
            }

            CreditsVisible = false;
            CreditsPage = 0;
            ChapterPreviewVisible = false;
            GameplayUiVisible = !TitleVisible;
            _chapterPreviewAccepted = GameplayUiVisible;
            SavingEnabled = !TitleVisible;
            InterfaceEnabled = true;
            HistoryVisible = false;
            MovieVisible = false;
            _backgroundTexture = LoadTexture(_backgroundName, memory);
            _previousBackgroundTexture = null;
            _backgroundTransitionDuration = 0f;
            _dialogueRevealForced = true;
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
            WindowVisible = snapshot.WindowVisible;
            TitleVisible = snapshot.TitleVisible;
            ChapterPreviewVisible = false;
            GameplayUiVisible = !snapshot.TitleVisible;
            _chapterPreviewAccepted = GameplayUiVisible;
            SavingEnabled = !snapshot.TitleVisible;
            InterfaceEnabled = true;
            DialogueSerial = snapshot.DialogueSerial;
            _backgroundName = snapshot.BackgroundName;
            _backgroundTexture = LoadTexture(_backgroundName, memory);
            _previousBackgroundTexture = null;
            _backgroundTransitionDuration = 0f;
            _layers.Clear();
            for (var i = 0; i < snapshot.Layers.Length; i++)
            {
                var layer = snapshot.Layers[i].CloneWithoutTexture();
                layer.Texture = LoadTexture(layer.TextureName, memory);
                _layers[layer.Id] = layer;
            }
        }

        public void ReloadVisualAssets(BurikoMemory memory)
        {
            _backgroundTexture = LoadTexture(_backgroundName, memory);
            foreach (var pair in _layers)
            {
                pair.Value.Texture = LoadTexture(pair.Value.TextureName, memory);
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
            var append = textMode == 1 || textMode == 3 || textMode == 4;
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
            WindowVisible = true;
            _dialogueRevealStartIndex = revealStart;
            _dialogueRevealStartedAt = Time.unscaledTime;
            _dialogueRevealForced = false;
            DialogueSerial++;
            var waitsForInput = textMode == 0 || textMode == 2;
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
                    if (_history.Count > 500)
                    {
                        _history.RemoveAt(0);
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
                Choices.Add(memory.Get(new BurikoReference(reference.Name, i)).AsString(memory));
            }

            if (Dialogue.IndexOf("OP 动画", StringComparison.OrdinalIgnoreCase) >= 0 && Choices.Count >= 2)
            {
                Choices[0] = "启用 OP 动画";
                Choices[1] = "禁用 OP 动画";
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
            float duration = 0f)
        {
            var nextTexture = LoadTexture(textureName, memory);
            _previousBackgroundTexture = duration > 0f ? _backgroundTexture : null;
            _backgroundName = textureName;
            _backgroundTexture = nextTexture;
            _backgroundTransitionStartedAt = Time.unscaledTime;
            _backgroundTransitionDuration = Mathf.Max(0f, duration);
            if (clearLayers)
            {
                _layers.Clear();
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
            float duration = 0f)
        {
            Texture2D previousTexture = null;
            float previousX = x;
            float previousY = y;
            float previousZ = z;
            float previousAlpha = 0f;
            var previousIsBustshot = isBustshot;
            var previousIsCentered = isBustshot || (x == 0 && y == 0);
            if (duration > 0f && _layers.TryGetValue(id, out var previous))
            {
                previousTexture = previous.Texture;
                previous.GetRenderState(out previousX, out previousY, out previousZ, out previousAlpha);
                previousIsBustshot = previous.IsBustshot;
                previousIsCentered = previous.IsCentered;
            }

            _layers[id] = new PresentationLayer
            {
                Id = id,
                TextureName = textureName,
                Texture = LoadTexture(textureName, memory),
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
                PreviousIsCentered = previousIsCentered
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
                duration);
            var textureName = Text(invocation, 1, memory);
            if (!string.IsNullOrEmpty(textureName))
            {
                layer.TextureName = textureName;
                layer.Texture = LoadTexture(textureName, memory);
            }
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
                    duration);
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
            layer.BeginTransition(layer.X, layer.Y, layer.Z, 0f, duration);
            layer.LipSyncBaseName = null;
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
                    Int(invocation, 8, memory) / 1000f);
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
                    layer.Texture = LoadTexture(textureName, memory);
                }
            }
        }

        private Texture2D LoadTexture(string textureName, BurikoMemory memory)
        {
            if (string.IsNullOrWhiteSpace(textureName) || _assets == null)
            {
                return null;
            }

            var index = ClampIndex(memory.GetGlobalFlag("GArtStyle"), _artSets.Count);
            var folders = _artSets.Count == 0
                ? new[] { "CG" }
                : _artSets[index].Folders;
            // GLanguage 0 is the installed Chinese script set.  The un-suffixed
            // textures are localized; the optional _j files are Japanese.
            return _assets.LoadTexture(textureName, folders, preferAsianVariant: false);
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
                var texture = LoadTexture(textureName, _memory);
                if (texture == null && targetFrame != 0)
                {
                    textureName = layer.LipSyncBaseName + "0";
                    texture = LoadTexture(textureName, _memory);
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
                var texture = LoadTexture(textureName, _memory);
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
                return progress * progress * (3f - 2f * progress);
            }
        }

        public void BeginTransition(int x, int y, int z, float alpha, float duration)
        {
            GetRenderState(out FromX, out FromY, out FromZ, out FromAlpha);
            X = x;
            Y = y;
            Z = z;
            Alpha = Mathf.Clamp01(alpha);
            TransitionStartedAt = Time.unscaledTime;
            TransitionDuration = Mathf.Max(0f, duration);
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
                CharacterId = CharacterId,
                LipSyncBaseName = LipSyncBaseName,
                LipSyncRestName = LipSyncRestName,
                LipSyncFrame = LipSyncFrame,
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
            string speaker,
            string dialogue,
            bool windowVisible,
            bool titleVisible,
            int dialogueSerial)
        {
            BackgroundName = backgroundName;
            Layers = layers;
            Speaker = speaker;
            Dialogue = dialogue;
            WindowVisible = windowVisible;
            TitleVisible = titleVisible;
            DialogueSerial = dialogueSerial;
        }

        public string BackgroundName { get; }
        public PresentationLayer[] Layers { get; }
        public string Speaker { get; }
        public string Dialogue { get; }
        public bool WindowVisible { get; }
        public bool TitleVisible { get; }
        public int DialogueSerial { get; }
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
        private AssetCascadeResolver _resolver;
        private UnityBurikoHost _host;

        public void Initialize(string installedGameDataRoot, UnityBurikoHost host)
        {
            _resolver = new AssetCascadeResolver(installedGameDataRoot);
            _host = host;
        }

        public void PlayBgm(int channel, string filename, float volume, BurikoMemory memory)
        {
            Play(RuntimeAudioKind.Bgm, channel, filename, volume, _host.CurrentBgmFolders(memory), true);
        }

        public void PlaySe(int channel, string filename, float volume, BurikoMemory memory)
        {
            Play(RuntimeAudioKind.Se, channel, filename, volume, _host.CurrentSeFolders(memory), false);
        }

        public void PlayVoice(int channel, string filename, float volume, BurikoMemory memory)
        {
            Play(RuntimeAudioKind.Voice, channel, filename, volume, new[] { "voice" }, false);
        }

        public void StopBgm(int channel) => Stop(RuntimeAudioKind.Bgm, channel);
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
            var prefix = RuntimeAudioKind.Voice + ":";
            var keys = new HashSet<string>();
            foreach (var pair in _generations)
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
            if (_sources.TryGetValue(Key(RuntimeAudioKind.Bgm, channel), out var source))
            {
                source.volume = Mathf.Clamp01(volume);
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
