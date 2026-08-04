using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Higurashi.IOS.Buriko;
using Higurashi.IOS.Compatibility;
using Higurashi.IOS.Runtime.Buriko;
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
        private GUIStyle _portTitleStyle;
        private GUIStyle _portSubtitleStyle;
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
            else if (_host.FragmentChapterVisible)
            {
                DrawFragmentChapterScreen();
            }
            else if (_host.FragmentListVisible)
            {
                DrawFragmentListScreen();
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
                        previousLayer.IsCentered, screenScale, false,
                        previousLayer.OverrideWidth, previousLayer.OverrideHeight);
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
                        layer.PreviousIsCentered, screenScale, false,
                        layer.PreviousOverrideWidth, layer.PreviousOverrideHeight);
                }
                layer.GetRenderState(out var layerX, out var layerY, out var layerZ, out var layerAlpha);
                if (layer.MaskTexture != null && layer.TransitionProgress < 1f)
                {
                    var maskProgress = layer.MaskReverse
                        ? 1f - layer.TransitionProgress
                        : layer.TransitionProgress;
                    DrawMaskedPresentationTexture(content, layer.Texture, layer.MaskTexture,
                        layerX, layerY, layerZ, layer.MaskReverse ? layer.FromAlpha : layer.Alpha,
                        layer.IsCentered, screenScale, maskProgress, layer.MaskFuzziness,
                        IsCinemaMatte(layer.TextureName), layer.OverrideWidth, layer.OverrideHeight);
                }
                else
                {
                    DrawPresentationTexture(content, layer.Texture, layerX, layerY, layerZ,
                        layerAlpha, layer.IsCentered, screenScale, IsCinemaMatte(layer.TextureName),
                        layer.OverrideWidth, layer.OverrideHeight);
                }
            }

            if (_host.FragmentTexture != null && _host.FragmentOpacity > 0f)
            {
                DrawFragmentEffect(content);
            }
        }

        private void DrawFragmentEffect(Rect content)
        {
            var texture = _host.FragmentTexture;
            var opacity = Mathf.Clamp01(_host.FragmentOpacity);
            var time = _host.FragmentAnimationTime;
            var cube = _host.FragmentStyle.IndexOf("Cube", StringComparison.OrdinalIgnoreCase) >= 0;
            var weird = _host.FragmentStyle.IndexOf("Weird", StringComparison.OrdinalIgnoreCase) >= 0;
            var previousColor = GUI.color;
            var previousMatrix = GUI.matrix;
            for (var i = 0; i < 6; i++)
            {
                var column = i % 3;
                var row = i / 3;
                var phase = time * (0.24f + i * 0.017f) + i * 1.31f;
                var centerX = content.x + content.width * ((column + 0.5f) / 3f) +
                              Mathf.Sin(phase) * content.width * (weird ? 0.10f : 0.045f);
                var centerY = content.y + content.height * ((row + 0.5f) / 2f) +
                              Mathf.Cos(phase * 0.83f) * content.height * (cube ? 0.035f : 0.07f);
                var width = content.width * (cube ? 0.30f : 0.38f) *
                            (weird ? 0.72f + (i % 3) * 0.16f : 1f);
                var height = content.height * (cube ? 0.42f : 0.56f) *
                             (weird ? 0.78f + (i & 1) * 0.18f : 1f);
                var rect = new Rect(centerX - width * 0.5f, centerY - height * 0.5f, width, height);
                GUI.matrix = previousMatrix;
                GUIUtility.RotateAroundPivot(Mathf.Sin(phase * 0.7f) * (weird ? 22f : 9f), rect.center);
                GUI.color = new Color(1f, 1f, 1f, opacity * (0.34f + i * 0.045f));
                GUI.DrawTextureWithTexCoords(rect, texture,
                    new Rect(i / 6f, 0f, 1f / 6f, 1f), true);
            }
            GUI.matrix = previousMatrix;
            GUI.color = previousColor;
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
            float layerX, float layerY, float layerZ, float alpha, bool centered, float screenScale,
            bool cropTransparentEdges = false, int overrideWidth = 0, int overrideHeight = 0)
        {
            var canonicalHeight = overrideHeight > 0 ? overrideHeight : Mathf.Min(texture.height, 480f);
            var canonicalWidth = overrideWidth > 0
                ? overrideWidth
                : texture.width * canonicalHeight / texture.height;
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
            var destination = new Rect(x, y, width, height);
            if (cropTransparentEdges)
            {
                var edgeInset = Mathf.Min(3f / Mathf.Max(1f, texture.height), 0.01f);
                GUI.DrawTextureWithTexCoords(destination, texture,
                    new Rect(0f, edgeInset, 1f, 1f - edgeInset * 2f), true);
            }
            else
            {
                GUI.DrawTexture(destination, texture, ScaleMode.StretchToFill, true);
            }
            GUI.color = previousColor;
        }

        private void DrawMaskedPresentationTexture(Rect content, Texture2D texture, Texture2D mask,
            float layerX, float layerY, float layerZ, float alpha, bool centered,
            float screenScale, float progress, float fuzziness, bool cropTransparentEdges = false,
            int overrideWidth = 0, int overrideHeight = 0)
        {
            var canonicalHeight = overrideHeight > 0 ? overrideHeight : Mathf.Min(texture.height, 480f);
            var canonicalWidth = overrideWidth > 0
                ? overrideWidth
                : texture.width * canonicalHeight / texture.height;
            var depthScale = Mathf.Max(0.05f, 1f - layerZ / 400f);
            var width = canonicalWidth * screenScale * depthScale;
            var height = canonicalHeight * screenScale * depthScale;
            var x = centered
                ? content.center.x + layerX * screenScale - width * 0.5f
                : content.center.x + layerX * screenScale;
            var y = centered
                ? content.center.y + layerY * screenScale - height * 0.5f
                : content.center.y + layerY * screenScale;
            var edgeInset = cropTransparentEdges
                ? Mathf.Min(3f / Mathf.Max(1f, texture.height), 0.01f)
                : 0f;
            DrawMaskedTexture(new Rect(x, y, width, height), texture, mask,
                new Rect(0f, edgeInset, 1f, 1f - edgeInset * 2f), progress, fuzziness, alpha);
        }

        private static bool IsCinemaMatte(string textureName)
        {
            return string.Equals(Path.GetFileNameWithoutExtension(textureName), "cinema",
                StringComparison.OrdinalIgnoreCase);
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

        private void DrawFragmentChapterScreen()
        {
            var safe = GetGuiSafeArea();
            if (_saveLoadVisible)
            {
                DrawSaveLoadScreen();
                return;
            }

            DrawModalShade(safe);
            var scale = UiScale;
            var panel = new Rect(safe.x + safe.width * 0.14f, safe.y + safe.height * 0.13f,
                safe.width * 0.72f, safe.height * 0.74f);
            DrawPcModalPanel(panel);
            DrawSectionHeader(panel, "碎片编织");
            var text =
                "剧情将在这里进入碎片编织流程。\n\n" +
                "进入碎片列表后，先轻触碎片查看解锁条件；\n" +
                "满足条件的碎片可以阅读，读完会推动后续可阅读内容。\n\n" +
                "所有可读碎片都需要依次阅读，不会出现选错路线。";
            GUI.Label(new Rect(panel.x + 42f * scale, panel.y + 86f * scale,
                panel.width - 84f * scale, panel.height - 210f * scale), text, _dialogueStyle);

            var buttonWidth = Mathf.Min(360f * scale, panel.width * 0.42f);
            var buttonHeight = 46f * scale;
            var buttonY = panel.yMax - buttonHeight - 30f * scale;
            var left = panel.center.x - buttonWidth - 12f * scale;
            if (PcButton(new Rect(left, buttonY, buttonWidth, buttonHeight), "进入碎片列表", true))
            {
                EnterFragmentList();
            }
            if (PcButton(new Rect(panel.center.x + 12f * scale, buttonY,
                    buttonWidth, buttonHeight), "保存与载入", true))
            {
                _saveLoadVisible = true;
                SuppressInput();
            }
        }

        private void DrawFragmentListScreen()
        {
            var safe = GetGuiSafeArea();
            if (_saveLoadVisible)
            {
                DrawSaveLoadScreen();
                return;
            }

            DrawModalShade(safe);
            var scale = UiScale;
            var panel = new Rect(safe.x + safe.width * 0.045f, safe.y + safe.height * 0.055f,
                safe.width * 0.91f, safe.height * 0.89f);
            DrawPcModalPanel(panel);
            DrawSectionHeader(panel, "碎片列表");
            var tutorialPending = PlayerPrefs.GetInt(FragmentTutorialSeenKey, 0) == 0;
            var previousGuiEnabled = GUI.enabled;
            if (tutorialPending)
            {
                GUI.enabled = false;
            }

            var entries = _host.GetVisibleFragments(_runtime.Memory);
            var pageCount = Mathf.Max(1, Mathf.CeilToInt(entries.Count / 8f));
            var page = Mathf.Clamp(_host.FragmentPage, 0, pageCount - 1);
            var gridX = panel.x + 22f * scale;
            var gridY = panel.y + 65f * scale;
            var gridWidth = panel.width - 44f * scale;
            var gridHeight = Mathf.Min(panel.height * 0.40f, 220f * scale);
            const int columns = 4;
            const int rows = 2;
            var gap = 8f * scale;
            var cardWidth = (gridWidth - gap * (columns - 1)) / columns;
            var cardHeight = (gridHeight - gap * (rows - 1)) / rows;
            var first = page * 8;
            for (var i = 0; i < 8; i++)
            {
                var entryIndex = first + i;
                if (entryIndex >= entries.Count)
                {
                    break;
                }
                var column = i % columns;
                var row = i / columns;
                var card = new Rect(gridX + column * (cardWidth + gap),
                    gridY + row * (cardHeight + gap), cardWidth, cardHeight);
                DrawFragmentEntry(card, entries[entryIndex]);
            }

            var footerHeight = 46f * scale;
            var footerY = panel.yMax - footerHeight - 18f * scale;
            var info = new Rect(panel.x + 22f * scale, gridY + gridHeight + 14f * scale,
                panel.width - 44f * scale, footerY - gridY - gridHeight - 26f * scale);
            DrawFragmentDetails(info);

            var navWidth = Mathf.Min(142f * scale, panel.width * 0.18f);
            if (page > 0 && PcButton(new Rect(panel.x + 22f * scale, footerY,
                    navWidth, footerHeight), "上一页", true))
            {
                _host.ChangeFragmentPage(-1, _runtime.Memory);
                SuppressInput();
            }
            else if (page == 0)
            {
                DrawDisabledPcButton(new Rect(panel.x + 22f * scale, footerY,
                    navWidth, footerHeight), "上一页", true);
            }

            GUI.Label(new Rect(panel.center.x - 60f * scale, footerY - 25f * scale,
                120f * scale, 24f * scale),
                (page + 1).ToString() + " / " + pageCount.ToString(), _panelTitleStyle);
            if (PcButton(new Rect(panel.center.x - 96f * scale, footerY,
                    192f * scale, footerHeight), "返回总览", true))
            {
                ExitFragmentList();
            }

            var nextRect = new Rect(panel.xMax - 22f * scale - navWidth, footerY,
                navWidth, footerHeight);
            if (page < pageCount - 1 && PcButton(nextRect, "下一页", true))
            {
                _host.ChangeFragmentPage(1, _runtime.Memory);
                SuppressInput();
            }
            else if (page >= pageCount - 1)
            {
                DrawDisabledPcButton(nextRect, "下一页", true);
            }

            GUI.enabled = previousGuiEnabled;
            if (tutorialPending)
            {
                DrawFragmentTutorialIfNeeded();
            }
        }

        private void DrawFragmentEntry(Rect rect, HigurashiFragmentDefinition entry)
        {
            var state = _host.GetFragmentViewState(entry, _runtime.Memory);
            var available = _host.AreFragmentPrerequisitesMet(entry, _runtime.Memory);
            var selected = _host.SelectedFragmentId == entry.Id;
            var border = selected
                ? new Color(0.92f, 0.08f, 0.03f, 1f)
                : FragmentStateColor(state, available);
            FillRect(rect, border);
            var label = entry.Id.ToString("00") + "  " + FragmentStateLabel(state, available) +
                "\n" + (entry.Title ?? string.Empty);
            if (GUI.Button(Inset(rect, Mathf.Max(2f, 2f * UiScale)), label, _pcSmallButtonStyle))
            {
                if (selected)
                {
                    StartSelectedFragment();
                }
                else
                {
                    _host.SelectFragment(entry.Id, _runtime.Memory);
                    SuppressInput();
                }
            }
        }

        private void DrawFragmentDetails(Rect rect)
        {
            DrawPcModalPanel(rect);
            var scale = UiScale;
            var entry = _host.GetSelectedFragment();
            if (entry == null)
            {
                GUI.Label(Inset(rect, 18f * scale), "轻触上方碎片，查看它的状态和解锁条件。",
                    _panelTitleStyle);
                return;
            }

            var state = _host.GetFragmentViewState(entry, _runtime.Memory);
            var available = _host.AreFragmentPrerequisitesMet(entry, _runtime.Memory);
            var header = entry.Id.ToString("00") + "　" + (entry.Title ?? string.Empty) +
                "　[" + FragmentStateLabel(state, available) + "]";
            GUI.Label(new Rect(rect.x + 18f * scale, rect.y + 10f * scale,
                rect.width - 36f * scale, 32f * scale), header, _panelTitleStyle);

            var actionWidth = Mathf.Min(220f * scale, rect.width * 0.29f);
            var textRect = new Rect(rect.x + 22f * scale, rect.y + 48f * scale,
                rect.width - actionWidth - 58f * scale, rect.height - 58f * scale);
            var detail = "说明：" + (entry.Description ?? string.Empty) + "\n\n前置碎片：\n" +
                _host.FragmentPrerequisiteSummary(entry, _runtime.Memory);
            GUI.Label(textRect, detail, _statusStyle);

            var actionRect = new Rect(rect.xMax - actionWidth - 18f * scale,
                rect.yMax - 52f * scale, actionWidth, 38f * scale);
            var action = available ? "阅读此碎片" : "查看未解锁提示";
            if (PcButton(actionRect, action, true))
            {
                StartSelectedFragment();
            }
        }

        private void DrawFragmentTutorialIfNeeded()
        {
            if (PlayerPrefs.GetInt(FragmentTutorialSeenKey, 0) != 0)
            {
                return;
            }

            var safe = GetGuiSafeArea();
            DrawModalShade(safe);
            var scale = UiScale;
            var panel = new Rect(safe.x + safe.width * 0.14f, safe.y + safe.height * 0.09f,
                safe.width * 0.72f, safe.height * 0.82f);
            DrawPcModalPanel(panel);
            DrawSectionHeader(panel, "碎片操作说明");
            var text =
                "轻触一个碎片\n显示名称、状态与解锁条件。\n\n" +
                "再次轻触该碎片，或按“阅读此碎片”\n进入已经满足条件的内容。\n\n" +
                "灰色/锁定状态表示条件尚未满足；\n" +
                "可用底部“上一页／下一页”浏览全部碎片。";
            GUI.Label(new Rect(panel.x + 42f * scale, panel.y + 84f * scale,
                panel.width - 84f * scale, panel.height - 155f * scale), text, _dialogueStyle);
            if (PcButton(new Rect(panel.center.x - 160f * scale, panel.yMax - 58f * scale,
                    320f * scale, 43f * scale), "我知道了", true))
            {
                PlayerPrefs.SetInt(FragmentTutorialSeenKey, 1);
                PlayerPrefs.Save();
                SuppressInput();
            }
        }

        private static string FragmentStateLabel(HigurashiFragmentViewState state, bool available)
        {
            switch (state)
            {
                case HigurashiFragmentViewState.Viewed:
                    return "已读";
                case HigurashiFragmentViewState.BrokenButFixable:
                    return "可读";
                case HigurashiFragmentViewState.Broken:
                    return "未解锁";
                default:
                    return available ? "可读" : "未解锁";
            }
        }

        private static Color FragmentStateColor(HigurashiFragmentViewState state, bool available)
        {
            switch (state)
            {
                case HigurashiFragmentViewState.Viewed:
                    return new Color(0.26f, 0.52f, 0.31f, 0.98f);
                case HigurashiFragmentViewState.BrokenButFixable:
                    return new Color(0.75f, 0.48f, 0.04f, 0.98f);
                case HigurashiFragmentViewState.Broken:
                    return new Color(0.22f, 0.22f, 0.24f, 0.98f);
                default:
                    return available
                        ? new Color(0.70f, 0.06f, 0.03f, 0.98f)
                        : new Color(0.22f, 0.22f, 0.24f, 0.98f);
            }
        }

        private void DrawMessageWindow()
        {
            var content = GetContentRect();
            var scale = UiScale;
            var windowFade = _host.WindowOpacity;
            var height = Mathf.Clamp(content.height * 0.16f, 112f * scale, 175f * scale);
            var rect = new Rect(content.x, content.yMax - height, content.width, height);
            var opacity = Mathf.Clamp01(_settings.windowOpacity / 100f);
            if (_host.WindowBackgroundTexture != null)
            {
                var previous = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, opacity * windowFade);
                GUI.DrawTexture(rect, _host.WindowBackgroundTexture, ScaleMode.StretchToFill, true);
                GUI.color = previous;
            }
            else
            {
                FillRect(rect, new Color(0.005f, 0.005f, 0.008f,
                    Mathf.Lerp(0.30f, 0.76f, opacity) * windowFade));
            }
            FillRect(new Rect(rect.x, rect.y, rect.width, Mathf.Max(1f, scale)),
                new Color(1f, 1f, 1f, 0.34f * windowFade));

            var left = rect.x + 24f * scale;
            var top = rect.y + 10f * scale;
            var toolbarReserve = Mathf.Min(rect.width * 0.18f, 250f * scale);
            var previousGuiColor = GUI.color;
            GUI.color = new Color(previousGuiColor.r, previousGuiColor.g,
                previousGuiColor.b, previousGuiColor.a * windowFade);
            if (!string.IsNullOrEmpty(_host.Speaker))
            {
                GUI.Label(new Rect(left, top, rect.width - 56f * scale - toolbarReserve, 40f * scale),
                    _host.Speaker, _speakerStyle);
                top += 39f * scale;
            }
            var previousDialogueColor = _dialogueStyle.normal.textColor;
            _dialogueStyle.normal.textColor = _host.DialogueColor;
            GUI.Label(
                new Rect(left, top, rect.width - 56f * scale - toolbarReserve, rect.yMax - top - 14f * scale),
                _host.VisibleDialogue + (_host.IsDialogueRevealComplete ? "　▼" : string.Empty),
                _dialogueStyle);
            _dialogueStyle.normal.textColor = previousDialogueColor;
            GUI.color = previousGuiColor;
        }

        private void DrawCinematicDialogue()
        {
            var content = GetContentRect();
            var scale = UiScale;
            var rect = new Rect(content.x + content.width * 0.10f,
                content.y + content.height * 0.72f,
                content.width * 0.80f, content.height * 0.20f);
            var text = _host.VisibleDialogue + (_host.IsDialogueRevealComplete ? "　▼" : string.Empty);
            var previousGuiColor = GUI.color;
            var windowFade = _host.WindowOpacity;
            GUI.color = new Color(previousGuiColor.r, previousGuiColor.g,
                previousGuiColor.b, previousGuiColor.a * windowFade);
            var previousDialogueColor = _dialogueStyle.normal.textColor;
            _dialogueStyle.normal.textColor = _host.DialogueColor;
            DrawShadowLabel(rect, text, _dialogueStyle);
            _dialogueStyle.normal.textColor = previousDialogueColor;
            GUI.color = previousGuiColor;
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
            var isEpisodeEight = HigurashiActiveChapter.Profile.EpisodeNumber == 8;
            DrawShadowLabel(new Rect(left, top, safe.width * 0.52f, 64f * scale),
                isEpisodeEight ? "参与人员" : "YCX STUDIOS 汉化组", _titleStyle);
            GUI.Label(new Rect(safe.xMax - safe.width * 0.38f - 30f * scale,
                    safe.y + 26f * scale, safe.width * 0.38f, 82f * scale),
                "寒蝉鸣泣之时\n" + HigurashiActiveChapter.Profile.ChineseChapterTitle,
                _panelTitleStyle);
            top += 88f * scale;
            var credits = isEpisodeEight
                ? "翻译：990，麻生早纪\n" +
                  "校对：枝瀬愛\n" +
                  "程序：饭\n" +
                  "润色：990，麻生早纪\n" +
                  "特别鸣谢：蝉吧全体吧友，DS，DB，GPT"
                : "参与人员\n" +
                  "原翻译：mayurina（里娜），srwfe（繁），纯真な工房（简），NNET，雪\n" +
                  "原润色：61y，晴，只是路人，Mize\n" +
                  "监制：ycx\n技术：ycx\n翻译：ycx\n" +
                  "校对＆润色：ycx，ReKo，DoSun，Xuee\n" +
                  "美工：ycx\n测试：ycx";
            GUI.Label(new Rect(left, top, safe.width * 0.72f, safe.height * 0.62f), credits, _dialogueStyle);
            if (!isEpisodeEight)
            {
                DrawShadowLabel(new Rect(safe.x, safe.yMax - 145f * scale, safe.width, 58f * scale),
                    "简体中文版汉化补丁 Ver 1.4", _titleStyle);
                GUI.Label(new Rect(safe.x, safe.yMax - 82f * scale, safe.width, 38f * scale),
                    "哔哩哔哩专栏　×　其乐 KeyLol　共同发布", _panelTitleStyle);
            }
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
            var copyrightY = safe.yMax - 43f * scale;
            var portY = Mathf.Min(y + buttonHeight + 8f * scale, copyrightY - 66f * scale);
            DrawOutlinedLabel(new Rect(safe.x, portY, safe.width, 34f * scale),
                "iOS版移植", _portTitleStyle);
            DrawOutlinedLabel(new Rect(safe.x, portY + 31f * scale, safe.width, 27f * scale),
                "贴吧@bugjump bilibili@Hyperion233", _portSubtitleStyle);
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
            var canSave = CanSaveGame() && slot != LatestSaveSlot;
            var buttonWidth = 82f * scale;
            var buttonGap = 6f * scale;
            var buttonCount = info == null ? (canSave ? 1 : 0) : (canSave ? 3 : 2);
            var controlsWidth = buttonCount <= 0
                ? 0f
                : buttonCount * buttonWidth + (buttonCount - 1) * buttonGap;
            var textX = rect.x + 14f * scale;
            var textWidth = rect.width - 28f * scale - controlsWidth;
            GUI.Label(new Rect(textX, rect.y + 7f * scale, textWidth, 29f * scale),
                slot == LatestSaveSlot
                    ? "最新保存"
                    : "文件 " + (slot - 1).ToString("00", CultureInfo.InvariantCulture),
                _speakerStyle);
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
                        ShowToast(slot == LatestSaveSlot
                            ? "已删除最新保存"
                            : "已删除文件 " + (slot - 1).ToString("00", CultureInfo.InvariantCulture));
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
            DrawPcModalPanel(panel);
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
            DrawPcModalPanel(panel);
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
            DrawShadowLabel(new Rect(left, top, width, 66f * scale),
                HigurashiActiveChapter.Profile.FullChineseTitle, _titleStyle);
            top += 88f * scale;
            GUI.Label(new Rect(left, top, width, 120f * scale),
                _initializationAttempted ? _runtimeStatus : _dataPack.Status, _statusStyle);
            top += 132f * scale;
            if (_dataPack.IsRunning)
            {
                var track = new Rect(left, top + 8f * scale, width, 18f * scale);
                GUI.DrawTexture(track, _sliderTrack, ScaleMode.StretchToFill, true);
                var fill = new Rect(track.x, track.y, track.width * Mathf.Clamp01(_dataPack.Progress), track.height);
                if (fill.width > 2f)
                {
                    GUI.DrawTexture(fill, _sliderFill, ScaleMode.StretchToFill, true);
                }
            }
            else if (!_initializationAttempted && PcButton(
                         new Rect(left, top, width, 58f * scale),
                         "请选择数据包"))
            {
                BeginDataPackSelection();
            }
            GUI.Label(new Rect(left, top + 76f * scale, width, 100f * scale),
                "点击按钮后将打开 iOS“文件”。选取 " +
                HigurashiActiveChapter.Profile.DataPackFileName +
                "，通过整包 SHA-256 校验后会自动解压并启动。",
                _statusStyle);
        }

        private bool UiConsumesPoint(Vector2 guiPoint)
        {
            if (_runtime == null || _host == null)
            {
                return true;
            }
            if (_host.MovieVisible)
            {
                // A movie is a full-screen modal even when it is letterboxed.  Do not let
                // stale title/gameplay controls consume taps in the black bars; every tap
                // must reach HandleInput, where it can only skip the current movie.
                return false;
            }
            if (_host.TitleVisible || _host.FragmentChapterVisible || _host.FragmentListVisible ||
                IsModalVisible || _host.ChoiceVisible)
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

        private void DrawDisabledPcButton(Rect rect, string text, bool small = false)
        {
            var enabled = GUI.enabled;
            GUI.enabled = false;
            GUI.Button(rect, text, small ? _pcSmallButtonStyle : _pcButtonStyle);
            GUI.enabled = enabled;
        }

        private void DrawPanel(Rect rect, Color color)
        {
            var previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, _roundedPanel, ScaleMode.StretchToFill, true);
            GUI.color = previous;
        }

        private void DrawPcModalPanel(Rect rect)
        {
            var border = Mathf.Max(1f, 1.5f * UiScale);
            FillRect(rect, new Color(0.88f, 0.88f, 0.88f, 0.72f));
            FillRect(Inset(rect, border), new Color(0.005f, 0.005f, 0.008f, 0.91f));
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

        private void DrawOutlinedLabel(Rect rect, string text, GUIStyle style)
        {
            var original = style.normal.textColor;
            var distance = Mathf.Max(2f, 2f * UiScale);
            style.normal.textColor = Color.black;
            for (var y = -1; y <= 1; y++)
            {
                for (var x = -1; x <= 1; x++)
                {
                    if (x == 0 && y == 0)
                    {
                        continue;
                    }
                    GUI.Label(new Rect(
                        rect.x + x * distance,
                        rect.y + y * distance,
                        rect.width,
                        rect.height), text, style);
                }
            }
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
            _portTitleStyle = MakeStyle(FontPixels(0.026f, 23, 42) * textScale,
                TextAnchor.MiddleCenter, FontStyle.Bold, Color.white);
            _portSubtitleStyle = MakeStyle(FontPixels(0.017f, 16, 28) * textScale,
                TextAnchor.MiddleCenter, FontStyle.Normal, new Color(0.96f, 0.96f, 0.96f));

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
