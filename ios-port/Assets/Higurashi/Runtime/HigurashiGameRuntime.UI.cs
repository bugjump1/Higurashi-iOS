using System.Globalization;
using Higurashi.IOS.Buriko;
using Higurashi.IOS.Compatibility;
using Higurashi.IOS.Runtime.Data;
using UnityEngine;

namespace Higurashi.IOS.Runtime
{
    public sealed partial class HigurashiGameRuntime
    {
        private void OnGUI()
        {
            EnsureStyles();
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _solidWhite);
            GUI.color = Color.white;

            if (_runtime == null)
            {
                DrawImportScreen();
                return;
            }

            DrawPresentation();
            if (_host.MovieVisible)
            {
                if (_host.MovieTexture != null)
                {
                    GUI.DrawTexture(GetContentRect(), _host.MovieTexture, ScaleMode.ScaleToFit, true);
                }
                return;
            }
            if (_runtime.BlockReason == BurikoBlockReason.Faulted)
            {
                GUI.Box(Inset(GetGuiSafeArea(), 40), _runtimeStatus, _statusStyle);
            }
            else if (_host.TitleVisible)
            {
                DrawTitleScreen();
            }
            else if (_host.ChoiceVisible)
            {
                DrawChoices();
            }
            else if (_host.HistoryVisible)
            {
                DrawHistory();
            }
            else if (_host.WindowVisible)
            {
                DrawMessageWindow();
            }
        }

        private void DrawPresentation()
        {
            var content = GetContentRect();
            if (_host.BackgroundTexture != null)
            {
                GUI.DrawTexture(
                    content,
                    _host.BackgroundTexture,
                    _settings.presentationMode == MobilePresentationMode.Fill
                        ? ScaleMode.ScaleAndCrop
                        : ScaleMode.ScaleToFit,
                    true);
            }

            _orderedLayers.Clear();
            foreach (var pair in _host.Layers)
            {
                if (pair.Value.Texture != null)
                {
                    _orderedLayers.Add(pair.Value);
                }
            }
            _orderedLayers.Sort((left, right) => left.Priority.CompareTo(right.Priority));

            var scale = content.height / 720f;
            for (var i = 0; i < _orderedLayers.Count; i++)
            {
                var layer = _orderedLayers[i];
                var width = layer.Texture.width * scale;
                var height = layer.Texture.height * scale;
                var x = content.center.x + layer.X * scale - width * 0.5f;
                var y = content.center.y - layer.Y * scale - height * 0.5f;
                var previousColor = GUI.color;
                GUI.color = new Color(1, 1, 1, layer.Alpha);
                GUI.DrawTexture(new Rect(x, y, width, height), layer.Texture, ScaleMode.StretchToFill, true);
                GUI.color = previousColor;
            }
        }

        private void DrawMessageWindow()
        {
            var safe = GetGuiSafeArea();
            var height = Mathf.Clamp(safe.height * 0.34f, 180f, 330f);
            var rect = new Rect(safe.x + 24f, safe.yMax - height - 18f, safe.width - 48f, height);
            var opacity = Mathf.Clamp01(_settings.windowOpacity / 100f);
            GUI.color = new Color(0.02f, 0.025f, 0.04f, Mathf.Lerp(0.35f, 0.92f, opacity));
            GUI.DrawTexture(rect, _solidWhite);
            GUI.color = Color.white;

            var left = rect.x + 34f;
            var top = rect.y + 22f;
            if (!string.IsNullOrEmpty(_host.Speaker))
            {
                GUI.Label(new Rect(left, top, rect.width - 68f, 42f), _host.Speaker, _speakerStyle);
                top += 46f;
            }
            GUI.Label(
                new Rect(left, top, rect.width - 68f, rect.yMax - top - 20f),
                _host.Dialogue,
                _dialogueStyle);
        }

        private void DrawTitleScreen()
        {
            var safe = GetGuiSafeArea();
            if (_settingsVisible)
            {
                DrawSettings(safe);
                return;
            }

            GUI.Label(new Rect(safe.x, safe.y + 30f, safe.width, 64f), "ひぐらしのなく頃に", _titleStyle);
            var width = Mathf.Min(420f, safe.width * 0.44f);
            var x = safe.xMax - width - 56f;
            var y = safe.y + safe.height * 0.52f;
            if (GUI.Button(new Rect(x, y, width, 62f), "开始游戏"))
            {
                StartGame();
            }
            y += 76f;
            if (GUI.Button(new Rect(x, y, width, 62f), "07th-Mod / iOS 设置"))
            {
                _settingsVisible = true;
                _suppressInputUntilFrame = Time.frameCount + 2;
            }
        }

        private void DrawSettings(Rect safe)
        {
            var panel = new Rect(
                safe.x + safe.width * 0.16f,
                safe.y + 28f,
                safe.width * 0.68f,
                safe.height - 56f);
            GUI.color = new Color(0.02f, 0.025f, 0.04f, 0.94f);
            GUI.DrawTexture(panel, _solidWhite);
            GUI.color = Color.white;
            GUI.Label(new Rect(panel.x, panel.y + 18f, panel.width, 52f), "游戏设置", _titleStyle);

            var x = panel.x + 36f;
            var y = panel.y + 88f;
            var width = panel.width - 72f;
            var artName = _host.ArtSets.Count == 0
                ? "CG"
                : _host.ArtSets[Mathf.Clamp(_settings.artSetIndex, 0, _host.ArtSets.Count - 1)].DisplayName;
            if (GUI.Button(new Rect(x, y, width, 52f), "立绘与背景：" + artName))
            {
                _settings.artSetIndex = Next(_settings.artSetIndex, _host.ArtSets.Count);
                _host.ApplySettings(_runtime.Memory);
            }
            y += 62f;

            var audioName = _host.AudioSets.Count == 0
                ? "脚本默认"
                : _host.AudioSets[Mathf.Clamp(_settings.audioPresetIndex, 0, _host.AudioSets.Count - 1)].DisplayName;
            if (GUI.Button(new Rect(x, y, width, 52f), "BGM / SE：" + audioName))
            {
                _settings.audioPresetIndex = Next(_settings.audioPresetIndex, _host.AudioSets.Count);
                _host.ApplySettings(_runtime.Memory);
            }
            y += 62f;

            if (GUI.Button(new Rect(x, y, width, 52f), "画面适配：" + PresentationModeName(_settings.presentationMode)))
            {
                _settings.presentationMode = (MobilePresentationMode)(((int)_settings.presentationMode + 1) % 3);
            }
            y += 62f;

            if (GUI.Button(new Rect(x, y, width, 52f), "口型同步：" + (_settings.lipSync ? "开" : "关")))
            {
                _settings.lipSync = !_settings.lipSync;
                _host.ApplySettings(_runtime.Memory);
            }
            y += 62f;

            GUI.Label(new Rect(x, y, width, 32f), "语音音量 " + _settings.voiceVolume + "%", _statusStyle);
            y += 34f;
            _settings.voiceVolume = Mathf.RoundToInt(GUI.HorizontalSlider(
                new Rect(x, y, width, 30f), _settings.voiceVolume, 0, 100));
            y += 48f;

            GUI.Label(new Rect(x, y, width, 32f), "文本框透明度 " + _settings.windowOpacity + "%", _statusStyle);
            y += 34f;
            _settings.windowOpacity = Mathf.RoundToInt(GUI.HorizontalSlider(
                new Rect(x, y, width, 30f), _settings.windowOpacity, 0, 100));

            if (GUI.Button(new Rect(x, panel.yMax - 70f, width, 50f), "保存并返回"))
            {
                _host.ApplySettings(_runtime.Memory);
                SaveSettings();
                _settingsVisible = false;
                _suppressInputUntilFrame = Time.frameCount + 2;
            }
        }

        private void DrawChoices()
        {
            var safe = GetGuiSafeArea();
            var width = Mathf.Min(760f, safe.width - 80f);
            var x = safe.x + (safe.width - width) * 0.5f;
            var totalHeight = _host.Choices.Count * 66f;
            var y = safe.y + (safe.height - totalHeight) * 0.5f;
            for (var i = 0; i < _host.Choices.Count; i++)
            {
                if (GUI.Button(new Rect(x, y + i * 66f, width, 54f), _host.Choices[i]))
                {
                    SelectChoice(i);
                }
            }
        }

        private void DrawHistory()
        {
            var safe = Inset(GetGuiSafeArea(), 28f);
            GUI.color = new Color(0.015f, 0.02f, 0.035f, 0.96f);
            GUI.DrawTexture(safe, _solidWhite);
            GUI.color = Color.white;
            var contentHeight = Mathf.Max(safe.height, _host.History.Count * 86f + 30f);
            _historyScroll = GUI.BeginScrollView(
                new Rect(safe.x + 18f, safe.y + 18f, safe.width - 36f, safe.height - 36f),
                _historyScroll,
                new Rect(0, 0, safe.width - 70f, contentHeight));
            var y = 10f;
            for (var i = 0; i < _host.History.Count; i++)
            {
                GUI.Label(new Rect(10f, y, safe.width - 90f, 78f), _host.History[i], _dialogueStyle);
                y += 86f;
            }
            GUI.EndScrollView();
        }

        private void DrawImportScreen()
        {
            var safe = GetGuiSafeArea();
            var width = Mathf.Min(820f, safe.width - 60f);
            var left = safe.x + (safe.width - width) * 0.5f;
            var top = safe.y + safe.height * 0.2f;
            GUI.Label(new Rect(left, top, width, 64f), "Higurashi 01 iOS", _titleStyle);
            top += 90f;
            GUI.Label(
                new Rect(left, top, width, 120f),
                _initializationAttempted ? _runtimeStatus : _dataPack.Status,
                _statusStyle);
            top += 138f;
            if (_dataPack.IsRunning)
            {
                GUI.HorizontalSlider(new Rect(left, top, width, 30f), _dataPack.Progress, 0f, 1f);
            }
            else if (!_initializationAttempted && GUI.Button(
                         new Rect(left, top, width, 58f),
                         "导入 Higurashi-01-data.zip"))
            {
                _dataPack.BeginImport(Application.persistentDataPath);
            }
            GUI.Label(
                new Rect(left, top + 76f, width, 90f),
                "请先把数据包放进本 App 的“文件”目录。原版游戏资源不会上传到 GitHub。",
                _statusStyle);
        }

        private Rect GetContentRect()
        {
            var safe = GetGuiSafeArea();
            if (_settings.presentationMode == MobilePresentationMode.Fill)
            {
                return safe;
            }

            var ratio = _settings.presentationMode == MobilePresentationMode.OriginalFourByThree
                ? 4f / 3f
                : ParseAspect(_host.ScreenAspect);
            var width = safe.width;
            var height = width / ratio;
            if (height > safe.height)
            {
                height = safe.height;
                width = height * ratio;
            }
            return new Rect(
                safe.x + (safe.width - width) * 0.5f,
                safe.y + (safe.height - height) * 0.5f,
                width,
                height);
        }

        private void EnsureStyles()
        {
            if (_titleStyle != null)
            {
                return;
            }

            _solidWhite = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            _solidWhite.SetPixel(0, 0, Color.white);
            _solidWhite.Apply();
            _titleStyle = MakeStyle(34, TextAnchor.MiddleCenter, FontStyle.Bold, Color.white);
            _speakerStyle = MakeStyle(25, TextAnchor.UpperLeft, FontStyle.Bold, new Color(0.98f, 0.9f, 0.65f));
            _dialogueStyle = MakeStyle(25, TextAnchor.UpperLeft, FontStyle.Normal, Color.white);
            _dialogueStyle.wordWrap = true;
            _dialogueStyle.richText = false;
            _statusStyle = MakeStyle(20, TextAnchor.UpperLeft, FontStyle.Normal, Color.white);
            _statusStyle.wordWrap = true;
        }

        private static GUIStyle MakeStyle(
            int fontSize,
            TextAnchor alignment,
            FontStyle fontStyle,
            Color color)
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize,
                alignment = alignment,
                fontStyle = fontStyle
            };
            style.normal.textColor = color;
            return style;
        }

        private static int Next(int value, int count)
        {
            return count <= 0 ? 0 : (value + 1) % count;
        }

        private static string PresentationModeName(MobilePresentationMode mode)
        {
            switch (mode)
            {
                case MobilePresentationMode.OriginalFourByThree: return "原始 4:3";
                case MobilePresentationMode.Fill: return "铺满（裁切）";
                default: return "完整显示";
            }
        }

        private static float ParseAspect(string value)
        {
            if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var ratio) || ratio <= 0)
            {
                return 16f / 9f;
            }
            return ratio < 1f ? 1f / ratio : ratio;
        }

        private static Rect Inset(Rect rect, float amount)
        {
            return new Rect(
                rect.x + amount,
                rect.y + amount,
                rect.width - amount * 2,
                rect.height - amount * 2);
        }

        private static Rect GetGuiSafeArea()
        {
            var safe = Screen.safeArea;
            return new Rect(safe.x, Screen.height - safe.yMax, safe.width, safe.height);
        }
    }
}
