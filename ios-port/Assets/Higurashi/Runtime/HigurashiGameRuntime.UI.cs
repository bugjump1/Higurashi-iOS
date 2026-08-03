using System;
using System.Globalization;
using System.IO;
using Higurashi.IOS.Buriko;
using Higurashi.IOS.Compatibility;
using UnityEngine;

namespace Higurashi.IOS.Runtime
{
    public sealed partial class HigurashiGameRuntime
    {
        private GUIStyle _pcButtonStyle;
        private GUIStyle _pcSmallButtonStyle;
        private GUIStyle _panelTitleStyle;
        private GUIStyle _slotStyle;
        private GUIStyle _sliderStyle;
        private GUIStyle _sliderThumbStyle;
        private GUIStyle _sectionHeaderStyle;
        private GUIStyle _toastStyle;
        private Texture2D _buttonNormal;
        private Texture2D _buttonHover;
        private Texture2D _buttonActive;
        private Texture2D _roundedPanel;
        private Texture2D _sliderTrack;
        private Texture2D _sliderFill;
        private Texture2D _sliderThumb;
        private Texture2D _sectionHeader;
        private Texture2D _transparent;
        private Material _maskedTransitionMaterial;
        private Font _uiFont;
        private float _styledForHeight;
        private string _toast = string.Empty;
        private float _toastUntil;
        private int _deleteConfirmSlot = -1;
        private bool _returnTitleConfirm;

        private bool IsModalVisible =>
            _settingsVisible || _helpVisible || _systemMenuVisible || _saveLoadVisible;

        private float UiScale => Mathf.Clamp(GetGuiSafeArea().height / 900f, 0.8f, 2.2f);

        private void OnGUI()
        {
            EnsureStyles();
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _solidWhite);
            GUI.color = Color.white;

            if (_runtime == null)
            {
                DrawImportScreen();
                DrawToast();
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
                DrawPanel(Inset(GetGuiSafeArea(), 32f * UiScale), new Color(0.15f, 0f, 0f, 0.96f));
                GUI.Label(Inset(GetGuiSafeArea(), 52f * UiScale), _runtimeStatus, _statusStyle);
            }
            else if (_host.CreditsVisible)
            {
                DrawCreditsScreen();
            }
            else if (_host.TitleVisible)
            {
                DrawTitleScreen();
            }
            else if (_host.ChapterPreviewVisible)
            {
                DrawChapterPreviewControls();
            }
            else
            {
                if (_host.ChoiceVisible)
                {
                    DrawChoices();
                }
                else if (_host.GameplayUiVisible && _host.HistoryVisible)
                {
                    DrawHistory();
                }
                else if (_host.GameplayUiVisible && _host.WindowVisible)
                {
                    if (_host.SavingEnabled)
                    {
                        DrawMessageWindow();
                    }
                    else
                    {
                        DrawCinematicDialogue();
                    }
                }

                if (_host.GameplayUiVisible)
                {
                    DrawGameplayControls();
                }
                if (_systemMenuVisible)
                {
                    DrawSystemMenu();
                }
                else if (_saveLoadVisible)
                {
                    DrawSaveLoadScreen();
                }
                else if (_settingsVisible)
                {
                    DrawSettings(GetGuiSafeArea());
                }
                else if (_helpVisible)
                {
                    DrawHelpScreen();
                }
            }

            DrawToast();
        }

        private void DrawPresentation()
        {
            var content = GetContentRect();
            var presentationOffset = _host.PresentationOffset * (content.height / 480f);
            content.position += presentationOffset;
            var screenScale = content.height / 480f;
            var backgroundProgress = _host.BackgroundTransitionProgress;
            if (_host.PreviousBackgroundTexture != null && backgroundProgress < 1f)
            {
                DrawBackgroundTexture(content, _host.PreviousBackgroundTexture, 1f);
                _orderedLayers.Clear();
                for (var i = 0; i < _host.PreviousSceneLayers.Count; i++)
                {
                    if (_host.PreviousSceneLayers[i].Texture != null)
                    {
                        _orderedLayers.Add(_host.PreviousSceneLayers[i]);
                    }
                }
                _orderedLayers.Sort((left, right) => left.Priority.CompareTo(right.Priority));
                for (var i = 0; i < _orderedLayers.Count; i++)
                {
                    var previousLayer = _orderedLayers[i];
                    previousLayer.GetRenderState(out var x, out var y, out var z, out var alpha);
                    DrawPresentationTexture(content, previousLayer.Texture, x, y, z, alpha,
                        previousLayer.IsCentered, screenScale);
                }
            }
            if (_host.BackgroundTexture != null)
            {
                if (_host.BackgroundTransitionMask != null &&
                    _host.PreviousBackgroundTexture != null && backgroundProgress < 1f)
                {
                    DrawMaskedBackgroundTexture(content, _host.BackgroundTexture,
                        _host.BackgroundTransitionMask, backgroundProgress, 0.45f);
                }
                else
                {
                    DrawBackgroundTexture(content, _host.BackgroundTexture,
                        _host.PreviousBackgroundTexture != null ? backgroundProgress : 1f);
                }
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

            // The PC engine renders in a 640x480 coordinate space and clamps tall
            // textures to 480 units before applying the script's Z scale.
            for (var i = 0; i < _orderedLayers.Count; i++)
            {
                var layer = _orderedLayers[i];
                if (layer.PreviousTexture != null && layer.TransitionProgress < 1f)
                {
                    DrawPresentationTexture(content, layer.PreviousTexture,
                        layer.PreviousX, layer.PreviousY, layer.PreviousZ,
                        layer.PreviousAlpha * (1f - layer.TransitionProgress),
                        layer.PreviousIsCentered, screenScale);
                }
                layer.GetRenderState(out var layerX, out var layerY, out var layerZ, out var layerAlpha);
                if (layer.MaskTexture != null && layer.TransitionProgress < 1f)
                {
                    var maskProgress = layer.MaskReverse
                        ? 1f - layer.TransitionProgress
                        : layer.TransitionProgress;
                    DrawMaskedPresentationTexture(content, layer.Texture, layer.MaskTexture,
                        layerX, layerY, layerZ, layer.MaskReverse ? layer.FromAlpha : layer.Alpha,
                        layer.IsCentered, screenScale, maskProgress, layer.MaskFuzziness);
                }
                else
                {
                    DrawPresentationTexture(content, layer.Texture, layerX, layerY, layerZ,
                        layerAlpha, layer.IsCentered, screenScale);
                }
            }
        }

        private void DrawMaskedBackgroundTexture(Rect content, Texture2D texture, Texture2D mask,
            float progress, float fuzziness)
        {
            GetBackgroundGeometry(content, texture, out var destination, out var source);
            DrawMaskedTexture(destination, texture, mask, source, progress, fuzziness, 1f);
        }

        private void DrawBackgroundTexture(Rect content, Texture texture, float alpha)
        {
            var previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
            GUI.DrawTexture(content, texture,
                _settings.presentationMode == MobilePresentationMode.Fill
                    ? ScaleMode.ScaleAndCrop
                    : ScaleMode.ScaleToFit,
                true);
            GUI.color = previous;
        }

        private void GetBackgroundGeometry(Rect content, Texture texture, out Rect destination,
            out Rect source)
        {
            if (_settings.presentationMode == MobilePresentationMode.Fill)
            {
                var scale = Mathf.Max(content.width / texture.width, content.height / texture.height);
                var visibleWidth = Mathf.Clamp01(content.width / (texture.width * scale));
                var visibleHeight = Mathf.Clamp01(content.height / (texture.height * scale));
                destination = content;
                source = new Rect((1f - visibleWidth) * 0.5f,
                    (1f - visibleHeight) * 0.5f, visibleWidth, visibleHeight);
                return;
            }

            var fitScale = Mathf.Min(content.width / texture.width, content.height / texture.height);
            var width = texture.width * fitScale;
            var height = texture.height * fitScale;
            destination = new Rect(content.center.x - width * 0.5f,
                content.center.y - height * 0.5f, width, height);
            source = new Rect(0f, 0f, 1f, 1f);
        }

        private static void DrawPresentationTexture(Rect content, Texture2D texture,
            float layerX, float layerY, float layerZ, float alpha, bool centered, float screenScale)
        {
            var canonicalHeight = Mathf.Min(texture.height, 480f);
            var canonicalWidth = texture.width * canonicalHeight / texture.height;
            var depthScale = Mathf.Max(0.05f, 1f - layerZ / 400f);
            var width = canonicalWidth * screenScale * depthScale;
            var height = canonicalHeight * screenScale * depthScale;
            float x;
            float y;
            if (centered)
            {
                x = content.center.x + layerX * screenScale - width * 0.5f;
                y = content.center.y + layerY * screenScale - height * 0.5f;
            }
            else
            {
                x = content.center.x + layerX * screenScale;
                y = content.center.y + layerY * screenScale;
            }
            var previousColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.DrawTexture(new Rect(x, y, width, height), texture, ScaleMode.StretchToFill, true);
            GUI.color = previousColor;
        }

        private void DrawMaskedPresentationTexture(Rect content, Texture2D texture, Texture2D mask,
            float layerX, float layerY, float layerZ, float alpha, bool centered,
            float screenScale, float progress, float fuzziness)
        {
            var canonicalHeight = Mathf.Min(texture.height, 480f);
            var canonicalWidth = texture.width * canonicalHeight / texture.height;
            var depthScale = Mathf.Max(0.05f, 1f - layerZ / 400f);
            var width = canonicalWidth * screenScale * depthScale;
            var height = canonicalHeight * screenScale * depthScale;
            var x = centered
                ? content.center.x + layerX * screenScale - width * 0.5f
                : content.center.x + layerX * screenScale;
            var y = centered
                ? content.center.y + layerY * screenScale - height * 0.5f
                : content.center.y + layerY * screenScale;
            DrawMaskedTexture(new Rect(x, y, width, height), texture, mask,
                new Rect(0f, 0f, 1f, 1f), progress, fuzziness, alpha);
        }

        private void DrawMaskedTexture(Rect destination, Texture texture, Texture mask, Rect source,
            float progress, float fuzziness, float alpha)
        {
            if (_maskedTransitionMaterial == null)
            {
                var shader = Resources.Load<Shader>("HigurashiMaskedTransition");
                if (shader != null)
                {
                    _maskedTransitionMaterial = new Material(shader);
                }
            }

            if (_maskedTransitionMaterial == null || mask == null)
            {
                var previous = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha * progress));
                GUI.DrawTextureWithTexCoords(destination, texture, source, true);
                GUI.color = previous;
                return;
            }

            _maskedTransitionMaterial.SetTexture("_MaskTex", mask);
            _maskedTransitionMaterial.SetFloat("_Progress", Mathf.Clamp01(progress));
            _maskedTransitionMaterial.SetFloat("_Fuzziness", Mathf.Max(0.001f, fuzziness));
            Graphics.DrawTexture(destination, texture, source, 0, 0, 0, 0,
                new Color(1f, 1f, 1f, Mathf.Clamp01(alpha)), _maskedTransitionMaterial);
        }

        private void DrawChapterPreviewControls()
        {
            // The localized scenario texture already contains the PC labels and
            // artwork.  Keep those visuals untouched and place touch targets over
            // its “开始” and “退出” rows.
            var content = GetContentRect();
            var scale = UiScale;
            var width = Mathf.Clamp(content.width * 0.24f, 190f * scale, 330f * scale);
            var height = Mathf.Clamp(content.height * 0.065f, 42f * scale, 62f * scale);
            var x = content.x + content.width * 0.075f;
            var y = content.y + content.height * 0.785f;
            if (PcButton(new Rect(x, y, width, height), "开始"))
            {
                ResolveChapterPreview(true);
            }
            if (PcButton(new Rect(x, y + height + 9f * scale, width, height), "退出"))
            {
                ResolveChapterPreview(false);
            }
        }

        private void ResolveChapterPreview(bool start)
        {
            if (!_host.ResolveChapterPreview(start, _runtime.Memory))
            {
                return;
            }
            _runtime.ResumeInput();
            SuppressInput();
            DriveRuntime(false);
            CaptureDialogueCheckpoint();
        }

        private void DrawMessageWindow()
        {
            var content = GetContentRect();
            var scale = UiScale;
            var height = Mathf.Clamp(content.height * 0.16f, 112f * scale, 175f * scale);
            var rect = new Rect(content.x, content.yMax - height, content.width, height);
            var opacity = Mathf.Clamp01(_settings.windowOpacity / 100f);
            FillRect(rect, new Color(0.005f, 0.005f, 0.008f, Mathf.Lerp(0.30f, 0.76f, opacity)));
            FillRect(new Rect(rect.x, rect.y, rect.width, Mathf.Max(1f, scale)),
                new Color(1f, 1f, 1f, 0.34f));

            var left = rect.x + 24f * scale;
            var top = rect.y + 10f * scale;
            var toolbarReserve = Mathf.Min(rect.width * 0.18f, 250f * scale);
            if (!string.IsNullOrEmpty(_host.Speaker))
            {
                GUI.Label(new Rect(left, top, rect.width - 56f * scale - toolbarReserve, 40f * scale),
                    _host.Speaker, _speakerStyle);
                top += 39f * scale;
            }
            GUI.Label(
                new Rect(left, top, rect.width - 56f * scale - toolbarReserve, rect.yMax - top - 14f * scale),
                _host.VisibleDialogue + (_host.IsDialogueRevealComplete ? "　▼" : string.Empty),
                _dialogueStyle);
        }

        private void DrawCinematicDialogue()
        {
            var content = GetContentRect();
            var scale = UiScale;
            var rect = new Rect(content.x + content.width * 0.10f,
                content.y + content.height * 0.72f,
                content.width * 0.80f, content.height * 0.20f);
            var text = _host.VisibleDialogue + (_host.IsDialogueRevealComplete ? "　▼" : string.Empty);
            DrawShadowLabel(rect, text, _dialogueStyle);
        }

        private void DrawCreditsScreen()
        {
            var safe = GetGuiSafeArea();
            var scale = UiScale;
            GUI.color = new Color(0f, 0f, 0f, 0.16f);
            GUI.DrawTexture(safe, _solidWhite);
            GUI.color = Color.white;

            if (_host.CreditsPage == 2)
            {
                DrawShadowLabel(new Rect(safe.x, safe.center.y - 85f * scale,
                    safe.width, 76f * scale), "iOS版移植", _titleStyle);
                GUI.Label(new Rect(safe.x, safe.center.y - 5f * scale,
                        safe.width, 46f * scale),
                    "贴吧@bugjump　bilibili@Hyperion233", _panelTitleStyle);
                GUI.Label(new Rect(safe.x, safe.yMax - 42f * scale, safe.width, 30f * scale),
                    "轻触屏幕继续", _statusStyle);
                return;
            }

            var left = safe.x + 34f * scale;
            var top = safe.y + 22f * scale;
            DrawShadowLabel(new Rect(left, top, safe.width * 0.52f, 64f * scale),
                "YCX STUDIOS 汉化组", _titleStyle);
            GUI.Label(new Rect(safe.xMax - safe.width * 0.38f - 30f * scale,
                    safe.y + 26f * scale, safe.width * 0.38f, 82f * scale),
                "寒蝉鸣泣之时\n鬼隐篇", _panelTitleStyle);
            top += 88f * scale;
            var credits =
                "参与人员\n" +
                "原翻译：mayurina（里娜），srwfe（繁），纯真な工房（简），NNET，雪\n" +
                "原润色：61y，晴，只是路人，Mize\n" +
                "监制：ycx\n技术：ycx\n翻译：ycx\n" +
                "校对＆润色：ycx，ReKo，DoSun，Xuee\n" +
                "美工：ycx\n测试：ycx";
            GUI.Label(new Rect(left, top, safe.width * 0.72f, safe.height * 0.62f), credits, _dialogueStyle);
            DrawShadowLabel(new Rect(safe.x, safe.yMax - 145f * scale, safe.width, 58f * scale),
                "简体中文版汉化补丁 Ver 1.4", _titleStyle);
            GUI.Label(new Rect(safe.x, safe.yMax - 82f * scale, safe.width, 38f * scale),
                "哔哩哔哩专栏　×　其乐 KeyLol　共同发布", _panelTitleStyle);
            GUI.Label(new Rect(safe.x, safe.yMax - 42f * scale, safe.width, 30f * scale),
                "轻触屏幕继续", _statusStyle);
        }

        private void DrawTitleScreen()
        {
            var safe = GetGuiSafeArea();
            if (_settingsVisible)
            {
                DrawSettings(safe);
                return;
            }
            if (_saveLoadVisible)
            {
                DrawSaveLoadScreen();
                return;
            }
            if (_helpVisible)
            {
                DrawHelpScreen();
                return;
            }

            var scale = UiScale;
            var width = Mathf.Clamp(safe.width * 0.25f, 300f * scale, 430f * scale);
            var x = safe.center.x - width * 0.5f;
            var buttonHeight = 54f * scale;
            var gap = 10f * scale;
            var y = safe.y + safe.height * 0.52f;
            if (PcButton(new Rect(x, y, width, buttonHeight), "开始游戏"))
            {
                StartGame();
            }
            y += buttonHeight + gap;
            if (PcButton(new Rect(x, y, width, buttonHeight), "继续游戏"))
            {
                _saveLoadVisible = true;
                SuppressInput();
            }
            y += buttonHeight + gap;
            if (PcButton(new Rect(x, y, width, buttonHeight), "系统设置"))
            {
                _settingsVisible = true;
                SuppressInput();
            }
            y += buttonHeight + gap;
            if (PcButton(new Rect(x, y, width, buttonHeight), "操作说明"))
            {
                _helpVisible = true;
                SuppressInput();
            }
            GUI.Label(new Rect(safe.x, safe.yMax - 43f * scale, safe.width, 30f * scale),
                "(C) 龙骑士07 / 07th Expansion", _panelTitleStyle);
        }

        private void DrawGameplayControls()
        {
            if (!_host.GameplayUiVisible || !_host.SavingEnabled || !_host.InterfaceEnabled ||
                IsModalVisible || _host.ChoiceVisible ||
                _host.CreditsVisible || _host.ChapterPreviewVisible)
            {
                return;
            }

            var safe = GetGuiSafeArea();
            var scale = UiScale;
            var railWidth = 98f * scale;
            var buttonHeight = 44f * scale;
            var x = safe.xMax - railWidth - 12f * scale;
            var y = safe.y + safe.height * 0.52f;
            if (PcButton(new Rect(x, y, railWidth, buttonHeight), _autoMode ? "自动中" : "自动", true))
            {
                ToggleAutoMode();
                SuppressInput();
            }
            y += buttonHeight + 7f * scale;
            if (PcButton(new Rect(x, y, railWidth, buttonHeight),
                    _fastTraversal.IsActive ? "停止" : "快进", true))
            {
                if (_fastTraversal.IsActive) _fastTraversal.Stop();
                else _fastTraversal.StartForward();
                SuppressInput();
            }
            y += buttonHeight + 7f * scale;
            if (PcButton(new Rect(x, y, railWidth, buttonHeight), "记录", true))
            {
                _host.HistoryVisible = !_host.HistoryVisible;
                if (_host.HistoryVisible)
                {
                    _historyAutoScrollPending = true;
                }
                SuppressInput();
            }
            y += buttonHeight + 7f * scale;
            if (PcButton(new Rect(x, y, railWidth, buttonHeight), "菜单", true))
            {
                _systemMenuVisible = true;
                _fastTraversal.Stop();
                SuppressInput();
            }

            var quickWidth = 150f * scale;
            var quickY = safe.yMax - 43f * scale;
            if (PcButton(new Rect(safe.xMax - quickWidth * 2f - 22f * scale, quickY,
                    quickWidth, 35f * scale), "快速保存", true))
            {
                SaveQuickGame();
                SuppressInput();
            }
            if (PcButton(new Rect(safe.xMax - quickWidth - 12f * scale, quickY,
                    quickWidth, 35f * scale), "快速读取", true))
            {
                LoadLatestQuickGame();
                SuppressInput();
            }
        }

        private void DrawSystemMenu()
        {
            var safe = GetGuiSafeArea();
            DrawModalShade(safe);
            var scale = UiScale;
            var panel = new Rect(safe.x + safe.width * 0.12f, safe.y + safe.height * 0.08f,
                safe.width * 0.76f, safe.height * 0.84f);
            FillRect(panel, new Color(0.005f, 0.005f, 0.008f, 0.70f));
            var redField = new Rect(panel.x + panel.width * 0.55f, panel.y,
                panel.width * 0.45f, panel.height);
            FillRect(redField, new Color(0.55f, 0.005f, 0.005f, 0.27f));
            FillRect(new Rect(redField.x, redField.y, Mathf.Max(2f, 3f * scale), redField.height),
                new Color(0.92f, 0.02f, 0.01f, 0.44f));
            DrawSectionHeader(panel, "系统菜单");

            var width = Mathf.Min(panel.width * 0.44f, 470f * scale);
            var x = panel.xMax - width - 28f * scale;
            var y = panel.y + 78f * scale;
            var h = 58f * scale;
            if (PcButton(new Rect(x, y, width, h), "保存与载入"))
            {
                _systemMenuVisible = false;
                _saveLoadVisible = true;
                SuppressInput();
            }
            y += h + 12f * scale;
            if (PcButton(new Rect(x, y, width, h), "系统设置"))
            {
                _systemMenuVisible = false;
                _settingsVisible = true;
                SuppressInput();
            }
            y += h + 12f * scale;
            if (PcButton(new Rect(x, y, width, h), "隐藏文本框"))
            {
                _host.ToggleWindow();
                CloseAllModals();
                SuppressInput();
            }
            y += h + 12f * scale;
            if (PcButton(new Rect(x, y, width, h),
                    _returnTitleConfirm ? "再次点击返回主菜单" : "返回到主菜单"))
            {
                if (_returnTitleConfirm)
                {
                    _returnTitleConfirm = false;
                    ReturnToTitle();
                }
                else
                {
                    _returnTitleConfirm = true;
                    ShowToast("再次点击以返回主菜单；当前进度将自动保存");
                }
                SuppressInput();
            }
            y += h + 12f * scale;
            if (PcButton(new Rect(x, y, width, h), "操作说明"))
            {
                _systemMenuVisible = false;
                _helpVisible = true;
                SuppressInput();
            }
            y += h + 12f * scale;
            if (PcButton(new Rect(x, y, width, h), "关闭菜单"))
            {
                CloseAllModals();
                SuppressInput();
            }

            GUI.Label(new Rect(panel.x + 34f * scale, panel.y + 115f * scale,
                    panel.width * 0.43f, panel.height * 0.55f),
                "触控快捷操作\n\n单指轻触　推进剧情\n上划　查看记录\n下划　隐藏／显示文本框\n三指左→右　快速回退\n三指右→左　快速前进\n快进中任意触摸　停止",
                _dialogueStyle);
        }

        private void DrawSaveLoadScreen()
        {
            var safe = GetGuiSafeArea();
            DrawModalShade(safe);
            var scale = UiScale;
            var panel = new Rect(safe.x + safe.width * 0.06f, safe.y + safe.height * 0.04f,
                safe.width * 0.88f, safe.height * 0.92f);
            DrawPanel(panel, new Color(0.09f, 0f, 0f, 0.94f));
            DrawSectionHeader(panel, "保存与载入");

            var margin = 24f * scale;
            var gap = 12f * scale;
            var gridTop = panel.y + 68f * scale;
            var footer = 164f * scale;
            var cellWidth = (panel.width - margin * 2f - gap) * 0.5f;
            var cellHeight = (panel.yMax - footer - gridTop - gap * 4f) / 5f;
            for (var slot = 1; slot <= 10; slot++)
            {
                var column = (slot - 1) % 2;
                var row = (slot - 1) / 2;
                var rect = new Rect(panel.x + margin + column * (cellWidth + gap),
                    gridTop + row * (cellHeight + gap), cellWidth, cellHeight);
                DrawSaveSlot(slot, rect);
            }

            DrawSpecialSaveRow(panel, panel.yMax - 146f * scale, "快速存档", 101);
            DrawSpecialSaveRow(panel, panel.yMax - 100f * scale, "自动存档", 201);

            if (PcButton(new Rect(panel.center.x - 145f * scale, panel.yMax - 48f * scale,
                    290f * scale, 38f * scale), "关闭", true))
            {
                CloseAllModals();
                SuppressInput();
            }
        }

        private void DrawSpecialSaveRow(Rect panel, float y, string label, int firstSlot)
        {
            var scale = UiScale;
            var margin = 24f * scale;
            var labelWidth = 145f * scale;
            var gap = 10f * scale;
            GUI.Label(new Rect(panel.x + margin, y, labelWidth, 38f * scale), label, _speakerStyle);
            var x = panel.x + margin + labelWidth;
            var width = (panel.width - margin * 2f - labelWidth - gap * 2f) / 3f;
            for (var index = 0; index < 3; index++)
            {
                var slot = firstSlot + index;
                var info = ReadSaveSlotInfo(slot);
                var text = (index + 1).ToString("00", CultureInfo.InvariantCulture) + "　" +
                           (info == null ? "— 无存档 —" : info.Timestamp.ToString("MM-dd HH:mm"));
                using (new GuiEnabledScope(info != null))
                {
                    if (PcButton(new Rect(x + index * (width + gap), y, width, 38f * scale),
                            text, true))
                    {
                        LoadGame(slot);
                        SuppressInput();
                    }
                }
            }
        }

        private void DrawSaveSlot(int slot, Rect rect)
        {
            var scale = UiScale;
            GUI.Box(rect, GUIContent.none, _slotStyle);
            var info = ReadSaveSlotInfo(slot);
            var canSave = CanSaveGame();
            var buttonWidth = 82f * scale;
            var buttonGap = 6f * scale;
            var buttonCount = info == null ? (canSave ? 1 : 0) : (canSave ? 3 : 2);
            var controlsWidth = buttonCount <= 0
                ? 0f
                : buttonCount * buttonWidth + (buttonCount - 1) * buttonGap;
            var textX = rect.x + 14f * scale;
            var textWidth = rect.width - 28f * scale - controlsWidth;
            GUI.Label(new Rect(textX, rect.y + 7f * scale, textWidth, 29f * scale),
                "文件 " + slot.ToString("00", CultureInfo.InvariantCulture), _speakerStyle);
            GUI.Label(new Rect(textX, rect.y + 35f * scale, textWidth, rect.height - 40f * scale),
                info == null
                    ? "— 空存档 —"
                    : info.Timestamp.ToString("yyyy-MM-dd HH:mm") + "\n" + info.Summary,
                _statusStyle);

            var buttonX = rect.xMax - controlsWidth - 10f * scale;
            if (canSave && PcButton(new Rect(buttonX, rect.center.y - 19f * scale,
                    buttonWidth, 38f * scale), info == null ? "保存" : "覆盖", true))
            {
                SaveGame(slot);
                SuppressInput();
            }
            if (canSave)
            {
                buttonX += buttonWidth + buttonGap;
            }
            if (info != null && PcButton(new Rect(buttonX, rect.center.y - 19f * scale,
                    buttonWidth, 38f * scale), "载入", true))
            {
                LoadGame(slot);
                SuppressInput();
            }
            if (info != null)
            {
                buttonX += buttonWidth + buttonGap;
            }
            if (info != null && PcButton(new Rect(buttonX, rect.center.y - 19f * scale,
                    buttonWidth, 38f * scale),
                    _deleteConfirmSlot == slot ? "确认" : "删除", true))
            {
                if (_deleteConfirmSlot == slot)
                {
                    try
                    {
                        File.Delete(SaveSlotPath(slot));
                        ShowToast("已删除槽位 " + slot);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogWarning("Unable to delete save: " + exception.Message);
                        ShowToast("删除失败");
                    }
                    _deleteConfirmSlot = -1;
                }
                else
                {
                    _deleteConfirmSlot = slot;
                }
                SuppressInput();
            }
        }

        private void DrawSettings(Rect safe)
        {
            DrawModalShade(safe);
            var scale = UiScale;
            var panel = new Rect(safe.x + safe.width * 0.12f, safe.y + safe.height * 0.06f,
                safe.width * 0.76f, safe.height * 0.88f);
            DrawPanel(panel, new Color(0.09f, 0f, 0f, 0.95f));
            DrawSectionHeader(panel, "系统设置");

            var x = panel.x + 34f * scale;
            var y = panel.y + 82f * scale;
            var width = panel.width - 68f * scale;
            var buttonHeight = 45f * scale;
            var artName = _host.ArtSets.Count == 0
                ? "CG"
                : _host.ArtSets[Mathf.Clamp(_settings.artSetIndex, 0, _host.ArtSets.Count - 1)].DisplayName;
            if (PcButton(new Rect(x, y, width, buttonHeight), "立绘与背景：" + artName, true))
            {
                _settings.artSetIndex = Next(_settings.artSetIndex, _host.ArtSets.Count);
                _host.ApplySettings(_runtime.Memory);
            }
            y += buttonHeight + 9f * scale;

            var audioName = _host.AudioSets.Count == 0
                ? "脚本默认"
                : _host.AudioSets[Mathf.Clamp(_settings.audioPresetIndex, 0, _host.AudioSets.Count - 1)].DisplayName;
            if (PcButton(new Rect(x, y, width, buttonHeight), "BGM / SE：" + audioName, true))
            {
                _settings.audioPresetIndex = Next(_settings.audioPresetIndex, _host.AudioSets.Count);
                _host.ApplySettings(_runtime.Memory);
            }
            y += buttonHeight + 9f * scale;
            if (PcButton(new Rect(x, y, width, buttonHeight),
                    "画面适配：" + PresentationModeName(_settings.presentationMode), true))
            {
                _settings.presentationMode = (MobilePresentationMode)(((int)_settings.presentationMode + 1) % 3);
            }
            y += buttonHeight + 9f * scale;
            if (PcButton(new Rect(x, y, width, buttonHeight),
                    "口型同步（仅主机版立绘）：" + (_settings.lipSync ? "开" : "关"), true))
            {
                _settings.lipSync = !_settings.lipSync;
                _host.ApplySettings(_runtime.Memory);
            }
            y += buttonHeight + 9f * scale;
            if (PcButton(new Rect(x, y, width, buttonHeight),
                    "自动保存：" + (_settings.autoSave ? "开" : "关"), true))
            {
                _settings.autoSave = !_settings.autoSave;
            }
            y += buttonHeight + 15f * scale;

            DrawSliderRow(x, ref y, width, "语音音量", ref _settings.voiceVolume, 0, 100);
            DrawSliderRow(x, ref y, width, "文本框透明度", ref _settings.windowOpacity, 0, 100);
            DrawSliderRow(x, ref y, width, "文字大小", ref _settings.textScale, 80, 150);
            DrawSliderRow(x, ref y, width, "文本速度", ref _settings.textSpeed, 0, 100);
            DrawSliderRow(x, ref y, width, "自动播放速度", ref _settings.autoSpeed, 0, 100);

            if (PcButton(new Rect(x, panel.yMax - 56f * scale, width, 43f * scale), "保存并返回", true))
            {
                _host.ApplySettings(_runtime.Memory);
                SaveSettings();
                _settingsVisible = false;
                _styledForHeight = 0f;
                SuppressInput();
            }
        }

        private void DrawSliderRow(float x, ref float y, float width, string label,
            ref int value, int minimum, int maximum)
        {
            var scale = UiScale;
            GUI.Label(new Rect(x, y, width, 28f * scale), label + " " + value + "%", _statusStyle);
            y += 28f * scale;
            var sliderRect = new Rect(x, y, width, 26f * scale);
            var trackRect = new Rect(x, y + 8f * scale, width, 10f * scale);
            GUI.DrawTexture(trackRect, _sliderTrack, ScaleMode.StretchToFill, true);
            var normalized = Mathf.InverseLerp(minimum, maximum, value);
            GUI.DrawTexture(new Rect(trackRect.x, trackRect.y, trackRect.width * normalized,
                trackRect.height), _sliderFill, ScaleMode.StretchToFill, true);
            value = Mathf.RoundToInt(GUI.HorizontalSlider(sliderRect,
                value, minimum, maximum, _sliderStyle, _sliderThumbStyle));
            y += 38f * scale;
        }

        private void DrawHelpScreen()
        {
            var safe = GetGuiSafeArea();
            DrawModalShade(safe);
            var scale = UiScale;
            var panel = new Rect(safe.x + safe.width * 0.14f, safe.y + safe.height * 0.09f,
                safe.width * 0.72f, safe.height * 0.82f);
            DrawPanel(panel, new Color(0.08f, 0f, 0f, 0.96f));
            DrawSectionHeader(panel, "操作说明");
            var text =
                "单指轻触\n推进到下一句台词\n\n" +
                "单指向上滑动\n打开剧情记录\n\n" +
                "单指向下滑动\n隐藏或显示文本框\n\n" +
                "三指从左向右滑动\n逐句快速回退\n\n" +
                "三指从右向左滑动\n逐句快速前进；任意触摸立即停止";
            GUI.Label(new Rect(panel.x + 42f * scale, panel.y + 84f * scale,
                panel.width - 84f * scale, panel.height - 155f * scale), text, _dialogueStyle);
            if (PcButton(new Rect(panel.center.x - 160f * scale, panel.yMax - 58f * scale,
                    320f * scale, 43f * scale), "我知道了", true))
            {
                PlayerPrefs.SetInt(HelpSeenKey, 1);
                PlayerPrefs.Save();
                _helpVisible = false;
                SuppressInput();
            }
        }

        private void DrawChoices()
        {
            var safe = GetGuiSafeArea();
            DrawModalShade(safe);
            var scale = UiScale;
            var width = Mathf.Min(820f * scale, safe.width - 80f * scale);
            var x = safe.x + (safe.width - width) * 0.5f;
            var height = 56f * scale;
            var totalHeight = _host.Choices.Count * (height + 10f * scale);
            var promptHeight = 110f * scale;
            var y = safe.y + (safe.height - totalHeight - promptHeight) * 0.5f;
            GUI.Label(new Rect(x, y, width, promptHeight - 15f * scale),
                _host.Dialogue, _panelTitleStyle);
            y += promptHeight;
            for (var i = 0; i < _host.Choices.Count; i++)
            {
                if (PcButton(new Rect(x, y + i * (height + 10f * scale), width, height), _host.Choices[i]))
                {
                    SelectChoice(i);
                }
            }
        }

        private void DrawHistory()
        {
            var scale = UiScale;
            var safe = Inset(GetGuiSafeArea(), 14f * scale);
            FillRect(safe, new Color(0.005f, 0.005f, 0.008f, 0.78f));
            DrawSectionHeader(safe, "剧情记录");
            var lineHeight = 90f * scale;
            var viewport = new Rect(safe.x + 22f * scale, safe.y + 66f * scale,
                safe.width - 44f * scale, safe.height - 82f * scale);
            var contentHeight = Mathf.Max(viewport.height,
                _host.History.Count * lineHeight + 25f * scale);
            var maxScroll = Mathf.Max(0f, contentHeight - viewport.height);
            if (_historyAutoScrollPending)
            {
                _historyScroll.y = maxScroll;
                _historyAutoScrollPending = false;
            }
            _historyScroll.y = Mathf.Clamp(_historyScroll.y, 0f, maxScroll);
            _historyScroll = GUI.BeginScrollView(
                viewport,
                _historyScroll,
                new Rect(0, 0, safe.width - 82f * scale, contentHeight));
            var y = 8f * scale;
            for (var i = 0; i < _host.History.Count; i++)
            {
                GUI.Label(new Rect(10f * scale, y, safe.width - 105f * scale, lineHeight - 8f * scale),
                    _host.History[i], _dialogueStyle);
                y += lineHeight;
            }
            GUI.EndScrollView();
        }

        private void DrawImportScreen()
        {
            var safe = GetGuiSafeArea();
            var scale = UiScale;
            var width = Mathf.Min(850f * scale, safe.width - 60f * scale);
            var left = safe.x + (safe.width - width) * 0.5f;
            var top = safe.y + safe.height * 0.18f;
            DrawShadowLabel(new Rect(left, top, width, 66f * scale), "寒蝉鸣泣之时 鬼隐篇", _titleStyle);
            top += 88f * scale;
            GUI.Label(new Rect(left, top, width, 120f * scale),
                _initializationAttempted ? _runtimeStatus : _dataPack.Status, _statusStyle);
            top += 132f * scale;
            if (_dataPack.IsRunning)
            {
                GUI.HorizontalSlider(new Rect(left, top, width, 30f * scale), _dataPack.Progress, 0f, 1f);
            }
            else if (!_initializationAttempted && PcButton(
                         new Rect(left, top, width, 58f * scale), "导入 Higurashi-01-data.zip"))
            {
                _dataPack.BeginImport(Application.persistentDataPath);
            }
            GUI.Label(new Rect(left, top + 76f * scale, width, 100f * scale),
                "请先把数据包放进本 App 的“文件”目录。原版游戏资源不会上传到 GitHub。",
                _statusStyle);
        }

        private bool UiConsumesPoint(Vector2 guiPoint)
        {
            if (_runtime == null || _host == null || _host.TitleVisible || IsModalVisible || _host.ChoiceVisible)
            {
                return true;
            }
            if (_host.HistoryVisible)
            {
                return true;
            }
            if (_host.CreditsVisible)
            {
                return false;
            }
            if (_host.ChapterPreviewVisible)
            {
                return true;
            }
            if (!_host.GameplayUiVisible || !_host.SavingEnabled || !_host.InterfaceEnabled)
            {
                return false;
            }
            var safe = GetGuiSafeArea();
            var scale = UiScale;
            var rightRail = new Rect(safe.xMax - 125f * scale, safe.y + safe.height * 0.47f,
                125f * scale, safe.height * 0.49f);
            var quickBar = new Rect(safe.xMax - 335f * scale, safe.yMax - 55f * scale,
                335f * scale, 55f * scale);
            return rightRail.Contains(guiPoint) || quickBar.Contains(guiPoint);
        }

        private void CloseAllModals()
        {
            _settingsVisible = false;
            _helpVisible = false;
            _systemMenuVisible = false;
            _saveLoadVisible = false;
            _deleteConfirmSlot = -1;
            _returnTitleConfirm = false;
        }

        private void SuppressInput()
        {
            _suppressInputUntilFrame = Time.frameCount + 2;
        }

        private void ShowToast(string message)
        {
            _toast = message ?? string.Empty;
            _toastUntil = Time.unscaledTime + 2.4f;
        }

        private void DrawToast()
        {
            if (string.IsNullOrEmpty(_toast) || Time.unscaledTime >= _toastUntil)
            {
                return;
            }
            var safe = GetGuiSafeArea();
            var scale = UiScale;
            var width = Mathf.Min(safe.width - 32f * scale, 760f * scale);
            var rect = new Rect(safe.center.x - width * 0.5f, safe.y + 22f * scale,
                width, 46f * scale);
            DrawPanel(rect, new Color(0.08f, 0f, 0f, 0.94f));
            GUI.Label(rect, _toast, _toastStyle);
        }

        private bool PcButton(Rect rect, string text, bool small = false)
        {
            return GUI.Button(rect, text, small ? _pcSmallButtonStyle : _pcButtonStyle);
        }

        private void DrawPanel(Rect rect, Color color)
        {
            var previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, _roundedPanel, ScaleMode.StretchToFill, true);
            GUI.color = previous;
        }

        private void FillRect(Rect rect, Color color)
        {
            var previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, _solidWhite, ScaleMode.StretchToFill, true);
            GUI.color = previous;
        }

        private void DrawModalShade(Rect safe)
        {
            var previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.52f);
            GUI.DrawTexture(safe, _solidWhite);
            GUI.color = previous;
        }

        private void DrawSectionHeader(Rect panel, string title)
        {
            var scale = UiScale;
            var rect = new Rect(panel.x + 18f * scale, panel.y + 14f * scale,
                Mathf.Min(panel.width * 0.48f, 470f * scale), 40f * scale);
            GUI.DrawTexture(rect, _sectionHeader, ScaleMode.StretchToFill, true);
            GUI.Label(new Rect(rect.x + 20f * scale, rect.y, rect.width - 30f * scale, rect.height),
                title, _sectionHeaderStyle);
        }

        private void DrawShadowLabel(Rect rect, string text, GUIStyle style)
        {
            var original = style.normal.textColor;
            style.normal.textColor = Color.black;
            GUI.Label(new Rect(rect.x + 3f * UiScale, rect.y + 3f * UiScale, rect.width, rect.height), text, style);
            style.normal.textColor = original;
            GUI.Label(rect, text, style);
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
            return new Rect(safe.x + (safe.width - width) * 0.5f,
                safe.y + (safe.height - height) * 0.5f, width, height);
        }

        private void EnsureStyles()
        {
            var safeHeight = GetGuiSafeArea().height;
            if (_titleStyle != null && Mathf.Abs(_styledForHeight - safeHeight) < 2f)
            {
                GUI.skin.font = _uiFont;
                return;
            }

            if (_solidWhite == null)
            {
                _solidWhite = NewSolidTexture(Color.white);
                _buttonNormal = NewRoundedTexture(new Color(0.015f, 0.015f, 0.018f, 0.98f), Color.white);
                _buttonHover = NewRoundedTexture(new Color(0.55f, 0.015f, 0.015f, 0.98f), Color.white);
                _buttonActive = NewRoundedTexture(new Color(0.85f, 0.025f, 0.015f, 0.98f), Color.white);
                _roundedPanel = NewRoundedTexture(Color.white, new Color(1f, 1f, 1f, 0.7f));
                _sliderTrack = NewRoundedTexture(new Color(0.015f, 0.015f, 0.018f, 1f), Color.white);
                _sliderFill = NewRoundedTexture(new Color(0.8f, 0.025f, 0.015f, 1f), Color.white);
                _sliderThumb = NewRoundedTexture(new Color(0.92f, 0.03f, 0.02f, 1f), Color.white);
                _sectionHeader = NewSectionHeaderTexture();
                _transparent = NewSolidTexture(Color.clear);
            }
            if (_uiFont == null)
            {
                _uiFont = CreateCjkFont();
            }
            GUI.skin.font = _uiFont;

            var textScale = Mathf.Clamp(_settings != null ? _settings.textScale : 100, 80, 150) / 100f;
            _titleStyle = MakeStyle(FontPixels(0.043f, 34, 72) * textScale,
                TextAnchor.MiddleCenter, FontStyle.Bold, Color.white);
            _speakerStyle = MakeStyle(FontPixels(0.029f, 25, 47) * textScale,
                TextAnchor.MiddleLeft, FontStyle.Bold, new Color(1f, 0.18f, 0.12f));
            _dialogueStyle = MakeStyle(FontPixels(0.032f, 29, 54) * textScale,
                TextAnchor.UpperLeft, FontStyle.Normal, Color.white);
            _dialogueStyle.wordWrap = true;
            _dialogueStyle.richText = false;
            _statusStyle = MakeStyle(FontPixels(0.021f, 19, 35) * textScale,
                TextAnchor.UpperLeft, FontStyle.Normal, new Color(0.92f, 0.92f, 0.92f));
            _statusStyle.wordWrap = true;
            _panelTitleStyle = MakeStyle(FontPixels(0.026f, 23, 43) * textScale,
                TextAnchor.MiddleCenter, FontStyle.Bold, Color.white);
            _sectionHeaderStyle = MakeStyle(FontPixels(0.027f, 24, 44) * textScale,
                TextAnchor.MiddleLeft, FontStyle.Bold, Color.white);
            _toastStyle = MakeStyle(FontPixels(0.021f, 18, 32) * textScale,
                TextAnchor.MiddleCenter, FontStyle.Bold, Color.white);
            _toastStyle.wordWrap = false;

            _pcButtonStyle = MakeButtonStyle(FontPixels(0.029f, 26, 48) * textScale);
            _pcSmallButtonStyle = MakeButtonStyle(FontPixels(0.020f, 18, 34) * textScale);
            _slotStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = _buttonNormal },
                border = new RectOffset(18, 18, 18, 18),
                padding = new RectOffset(10, 10, 8, 8)
            };
            _sliderStyle = new GUIStyle(GUI.skin.horizontalSlider)
            {
                normal = { background = _transparent },
                fixedHeight = 26f * UiScale
            };
            _sliderThumbStyle = new GUIStyle(GUI.skin.horizontalSliderThumb)
            {
                normal = { background = _sliderThumb },
                hover = { background = _sliderThumb },
                active = { background = _sliderThumb },
                fixedWidth = 28f * UiScale,
                fixedHeight = 28f * UiScale
            };
            _styledForHeight = safeHeight;
        }

        private GUIStyle MakeButtonStyle(float fontSize)
        {
            var style = new GUIStyle(GUI.skin.button)
            {
                font = _uiFont,
                fontSize = Mathf.RoundToInt(fontSize),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                border = new RectOffset(20, 20, 20, 20),
                padding = new RectOffset(12, 12, 5, 5)
            };
            style.normal.background = _buttonNormal;
            style.hover.background = _buttonHover;
            style.active.background = _buttonActive;
            style.focused.background = _buttonHover;
            style.normal.textColor = Color.white;
            style.hover.textColor = Color.white;
            style.active.textColor = Color.white;
            style.focused.textColor = Color.white;
            return style;
        }

        private static Font CreateCjkFont()
        {
            try
            {
                return Font.CreateDynamicFontFromOSFont(new[]
                {
                    "PingFang SC", "PingFang TC", "Hiragino Sans GB", "Hiragino Sans",
                    "Microsoft YaHei UI", "Microsoft YaHei", "Noto Sans CJK SC", "Arial Unicode MS"
                }, 48);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Unable to create a CJK system font: " + exception.Message);
                return GUI.skin.font;
            }
        }

        private static Texture2D NewSolidTexture(Color color)
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        private static Texture2D NewSectionHeaderTexture()
        {
            const int width = 256;
            const int height = 32;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color[width * height];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var t = x / (width - 1f);
                    var fade = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.42f, 1f, t));
                    pixels[y * width + x] = Color.Lerp(
                        new Color(0.84f, 0.025f, 0.015f, 0.96f),
                        new Color(0.015f, 0.008f, 0.01f, 0.96f), fade);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        private static Texture2D NewRoundedTexture(Color fill, Color border)
        {
            const int size = 64;
            const float radius = 15f;
            const float borderWidth = 2.5f;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = Mathf.Max(radius - x, 0f, x - (size - 1f - radius));
                    var dy = Mathf.Max(radius - y, 0f, y - (size - 1f - radius));
                    var distance = Mathf.Sqrt(dx * dx + dy * dy);
                    var outsideAlpha = Mathf.Clamp01(radius + 0.5f - distance);
                    var borderBlend = Mathf.Clamp01(distance - (radius - borderWidth));
                    var color = Color.Lerp(fill, border, borderBlend);
                    color.a *= outsideAlpha;
                    pixels[y * size + x] = color;
                }
            }
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        private float FontPixels(float heightFraction, int minimum, int maximum)
        {
            return Mathf.Clamp(GetGuiSafeArea().height * heightFraction, minimum, maximum);
        }

        private static GUIStyle MakeStyle(float fontSize, TextAnchor alignment,
            FontStyle fontStyle, Color color)
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(fontSize),
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
            return new Rect(rect.x + amount, rect.y + amount,
                rect.width - amount * 2f, rect.height - amount * 2f);
        }

        private static Rect GetGuiSafeArea()
        {
            var safe = Screen.safeArea;
            return new Rect(safe.x, Screen.height - safe.yMax, safe.width, safe.height);
        }

        private readonly struct GuiEnabledScope : IDisposable
        {
            private readonly bool _previous;

            public GuiEnabledScope(bool enabled)
            {
                _previous = GUI.enabled;
                GUI.enabled = enabled;
            }

            public void Dispose()
            {
                GUI.enabled = _previous;
            }
        }
    }
}
