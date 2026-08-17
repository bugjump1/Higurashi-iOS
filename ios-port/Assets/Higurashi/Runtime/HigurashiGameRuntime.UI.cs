using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Higurashi.IOS.Buriko;
using Higurashi.IOS.Compatibility;
using Higurashi.IOS.Runtime.Buriko;
using Higurashi.IOS.Runtime.Diagnostics;
using UnityEngine;

namespace Higurashi.IOS.Runtime
{
    public sealed partial class HigurashiGameRuntime
    {
        private GUIStyle _pcButtonStyle;
        private GUIStyle _pcSmallButtonStyle;
        private GUIStyle _historyStyle;
        private GUIStyle _panelTitleStyle;
        private GUIStyle _saveSummaryStyle;
        private GUIStyle _tipCardStyle;
        private GUIStyle _slotStyle;
        private GUIStyle _sliderStyle;
        private GUIStyle _sliderThumbStyle;
        private GUIStyle _sectionHeaderStyle;
        private GUIStyle _toastStyle;
        private GUIStyle _portTitleStyle;
        private GUIStyle _portSubtitleStyle;
        private GUIStyle _importTitleStyle;
        private GUIStyle _importStepStyle;
        private GUIStyle _importStatusStyle;
        private GUIStyle _importPercentStyle;
        private GUIStyle _importDetailStyle;
        private GUIStyle _importDetailRightStyle;
        private Texture2D _buttonNormal;
        private Texture2D _buttonHover;
        private Texture2D _buttonActive;
        private Texture2D _roundedPanel;
        private Texture2D _sliderTrack;
        private Texture2D _sliderFill;
        private Texture2D _sliderThumb;
        private Texture2D _sectionHeader;
        private Texture2D _transparent;
        private Texture2D _creditsSeriesLogo;
        private Texture2D _creditsChapterTitle;
        private Material _maskedTransitionMaterial;
        private Font _uiFont;
        private float _styledForHeight;
        private string _toast = string.Empty;
        private float _toastUntil;
        private int _deleteConfirmSlot = -1;
        private bool _returnTitleConfirm;
        private bool _extrasVisible;
        private bool _chapterJumpVisible;
        private readonly List<string> _chapterJumpSections = new List<string>();
        private Vector2 _chapterJumpScroll;
        private Vector2 _settingsScroll;
        private int _tipsChapterAutoSavedChapter = -1;
        private int _portCreditTapCount;
        private float _portCreditTapDeadline;

        private bool IsModalVisible =>
            _settingsVisible || _helpVisible || _systemMenuVisible || _saveLoadVisible ||
            _extrasVisible || _chapterJumpVisible || _badEndingDecisionVisible;

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
            else if (_badEndingDecisionVisible)
            {
                DrawBadEndingDecision();
            }
            else if (_host.TitleVisible)
            {
                DrawTitleScreen();
            }
            else if (_host.TipsChapterVisible)
            {
                DrawTipsChapterScreen();
            }
            else if (_host.TipsListVisible)
            {
                DrawTipsListScreen();
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
                    if (_host.SavingEnabled || _host.TipReading)
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
                    DrawPresentationTexture(content, previousLayer.Texture, x, y, z,
                        alpha * (1f - backgroundProgress),
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
                if (IsFullFrameBlack(layer.TextureName))
                {
                    DrawFullFrameBlackLayer(content, layer);
                    continue;
                }
                if (layer.PreviousTexture != null && layer.TransitionProgress < 1f)
                {
                    if (IsFullFrameBlack(layer.PreviousTexture.name))
                    {
                        var previousColor = GUI.color;
                        GUI.color = new Color(1f, 1f, 1f,
                            layer.PreviousAlpha * (1f - layer.TransitionProgress));
                        GUI.DrawTexture(content, layer.PreviousTexture,
                            ScaleMode.StretchToFill, true);
                        GUI.color = previousColor;
                    }
                    else
                    {
                        DrawPresentationTexture(content, layer.PreviousTexture,
                            layer.PreviousX, layer.PreviousY, layer.PreviousZ,
                            layer.PreviousAlpha * (1f - layer.TransitionProgress),
                            layer.PreviousIsCentered, screenScale, false,
                            layer.PreviousOverrideWidth, layer.PreviousOverrideHeight);
                    }
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

        private static bool IsFullFrameBlack(string textureName)
        {
            return string.Equals(Path.GetFileNameWithoutExtension(textureName), "black",
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

        private void DrawTipsChapterScreen()
        {
            if (_saveLoadVisible)
            {
                DrawSaveLoadScreen();
                return;
            }

            UnlockTipsMenu();
            var currentChapter = ChapterProgressCount(_host.CurrentChapterNumber);
            UnlockTipsThroughChapter(currentChapter);
            if (currentChapter > PlayerPrefs.GetInt(ChapterJumpUnlockedKey, 0))
            {
                PlayerPrefs.SetInt(ChapterJumpUnlockedKey, currentChapter);
                PlayerPrefs.Save();
            }

            var safe = GetGuiSafeArea();
            FillRect(safe, Color.black);
            var scale = UiScale;
            if (_tipsChapterAutoSavedChapter != currentChapter)
            {
                // This checkpoint is deliberately independent of the optional
                // auto-save setting: reaching the PC chapter screen must never
                // make the player lose the completed chapter.
                if (SaveChapterCompletionProgress(currentChapter))
                {
                    _tipsChapterAutoSavedChapter = currentChapter;
                    ShowToast("已自动保存本章节进度");
                }
            }
            var entries = _host.GetVisibleTips(_runtime.Memory);
            var width = Mathf.Min(safe.width * 0.34f, 470f * scale);
            var height = 62f * scale;
            var gap = 22f * scale;
            var totalHeight = height * 4f + gap * 3f;
            var x = safe.center.x - width * 0.5f;
            var y = safe.center.y - totalHeight * 0.5f;
            if (entries.Count > 0 && PcButton(new Rect(x, y, width, height), "新的 TIPS", true))
            {
                EnterChapterTips(false);
            }
            else if (entries.Count == 0)
            {
                DrawDisabledPcButton(new Rect(x, y, width, height), "新的 TIPS", true);
            }
            y += height + gap;
            if (PcButton(new Rect(x, y, width, height), "所有的 TIPS", true))
            {
                EnterChapterTips(true);
            }
            y += height + gap;
            if (PcButton(new Rect(x, y, width, height), "保存与载入", true))
            {
                _saveLoadVisible = true;
                SuppressInput();
            }
            y += height + gap;
            if (PcButton(new Rect(x, y, width, height), "继续", true))
            {
                ContinuePastTips();
            }
            GUI.Label(new Rect(safe.x, y + height + 18f * scale,
                    safe.width, 48f * scale),
                "按“继续”进入下一章；之后仍可从主菜单的“追加内容”打开 TIPS。",
                _panelTitleStyle);
        }

        private void DrawTipsListScreen()
        {
            var safe = GetGuiSafeArea();
            if (_saveLoadVisible)
            {
                DrawSaveLoadScreen();
                return;
            }

            var scale = UiScale;
            FillRect(safe, Color.black);
            var content = GetContentRect();
            var panel = new Rect(content.x + content.width * 0.03f, content.y + content.height * 0.035f,
                content.width * 0.94f, content.height * 0.93f);
            FillRect(panel, new Color(0.025f, 0.025f, 0.028f, 0.98f));
            DrawSectionHeader(panel, _host.TipsLibraryStandalone ? "TIPS 菜单" : "TIPS");
            var entries = _host.GetVisibleTips(_runtime.Memory);
            var pageCount = Mathf.Max(1, Mathf.CeilToInt(entries.Count / 8f));
            var page = Mathf.Clamp(_host.TipsPage, 0, pageCount - 1);
            var horizontalPadding = Mathf.Max(16f, 24f * scale);
            var footerHeight = Mathf.Max(40f, 34f * scale);
            var footerBottomPadding = Mathf.Max(8f, 12f * scale);
            var footerY = panel.yMax - footerBottomPadding - footerHeight;
            var gridTop = panel.y + Mathf.Max(54f, 70f * scale);
            var sectionGap = Mathf.Max(8f, 12f * scale);
            var bodyBottom = footerY - sectionGap;
            var availableBodyHeight = Mathf.Max(120f, bodyBottom - gridTop);
            var gridHeight = Mathf.Min(panel.height * 0.44f, availableBodyHeight * 0.54f);
            gridHeight = Mathf.Max(Mathf.Min(96f, availableBodyHeight * 0.5f), gridHeight);
            var grid = new Rect(panel.x + horizontalPadding, gridTop,
                panel.width - horizontalPadding * 2f, gridHeight);
            var gap = Mathf.Max(7f, 10f * scale);
            var cardWidth = (grid.width - gap * 3f) / 4f;
            var cardHeight = (grid.height - gap) * 0.5f;
            var first = page * 8;
            for (var i = 0; i < 8; i++)
            {
                var index = first + i;
                if (index >= entries.Count)
                {
                    break;
                }
                var card = new Rect(grid.x + (i % 4) * (cardWidth + gap),
                    grid.y + (i / 4) * (cardHeight + gap), cardWidth, cardHeight);
                DrawTipEntry(card, entries[index]);
            }

            var selected = _host.GetSelectedTip();
            var info = new Rect(panel.x + horizontalPadding, grid.yMax + sectionGap,
                panel.width - horizontalPadding * 2f,
                Mathf.Max(1f, bodyBottom - grid.yMax - sectionGap));
            FillRect(info, new Color(0f, 0f, 0f, 0.36f));
            if (selected == null)
            {
                GUI.Label(Inset(info, 16f * scale), "轻触一条 TIPS 查看；再次轻触即可阅读。", _statusStyle);
            }
            else
            {
                var detail = "TIPS " + (selected.Id + 1).ToString("00") + "\n" + selected.DisplayTitle +
                    "\n简介：" + (string.IsNullOrWhiteSpace(selected.Description)
                        ? "再次轻触此预览框进入阅读。"
                        : selected.Description);
                var detailPaddingX = Mathf.Max(10f, 16f * scale);
                var detailPaddingY = Mathf.Max(7f, 10f * scale);
                var detailRect = new Rect(info.x + detailPaddingX, info.y + detailPaddingY,
                    info.width - detailPaddingX * 2f, info.height - detailPaddingY * 2f);
                GUI.Label(detailRect, detail,
                    FitWrappedLabelStyle(_panelTitleStyle, detail, detailRect,
                        Mathf.Max(14, Mathf.RoundToInt(15f * scale))));
            }

            var navWidth = Mathf.Min(150f * scale, panel.width * 0.17f);
            if (page > 0 && PcButton(new Rect(panel.xMax - navWidth * 2f - 38f * scale,
                    footerY, navWidth, footerHeight),
                    "上一页", true))
            {
                _host.ChangeTipsPage(-1, _runtime.Memory);
                SuppressInput();
            }
            if (page < pageCount - 1 && PcButton(new Rect(panel.xMax - navWidth - 26f * scale,
                    footerY, navWidth, footerHeight), "下一页", true))
            {
                _host.ChangeTipsPage(1, _runtime.Memory);
                SuppressInput();
            }
            GUI.Label(new Rect(panel.center.x - 70f * scale, footerY, 140f * scale, footerHeight),
                (page + 1) + " / " + pageCount, _panelTitleStyle);
            if (PcButton(new Rect(panel.x + 18f * scale, footerY,
                    160f * scale, footerHeight), "关闭", true))
            {
                ExitTipsLibrary();
            }
        }

        private void DrawFullFrameBlackLayer(Rect content, PresentationLayer layer)
        {
            var progress = layer.TransitionProgress;
            if (layer.PreviousTexture != null && progress < 1f &&
                !IsFullFrameBlack(layer.PreviousTexture.name))
            {
                DrawPresentationTexture(content, layer.PreviousTexture,
                    layer.PreviousX, layer.PreviousY, layer.PreviousZ,
                    layer.PreviousAlpha * (1f - progress), layer.PreviousIsCentered,
                    content.height / 480f, false,
                    layer.PreviousOverrideWidth, layer.PreviousOverrideHeight);
            }

            layer.GetRenderState(out _, out _, out _, out var alpha);
            if (layer.MaskTexture != null && progress < 1f)
            {
                var maskProgress = layer.MaskReverse ? 1f - progress : progress;
                DrawMaskedTexture(content, layer.Texture, layer.MaskTexture,
                    new Rect(0f, 0f, 1f, 1f), maskProgress,
                    layer.MaskFuzziness, layer.MaskReverse ? layer.FromAlpha : layer.Alpha);
                return;
            }

            var previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
            GUI.DrawTexture(content, layer.Texture, ScaleMode.StretchToFill, true);
            GUI.color = previous;
        }

        private void DrawTipEntry(Rect rect, HigurashiTipDefinition entry)
        {
            var selected = _host.SelectedTipId == entry.Id;
            FillRect(rect, new Color(0.12f, 0.015f, 0.015f, 0.96f));
            var preview = _host.GetTipPreview(entry, _runtime.Memory, selected);
            if (preview != null)
            {
                GUI.DrawTexture(rect, preview, ScaleMode.StretchToFill, true);
                FillRect(rect, new Color(0f, 0f, 0f, selected ? 0.04f : 0.16f));
            }
            else
            {
                FillRect(rect, new Color(0.12f, 0.015f, 0.015f, 0.88f));
            }
            var caption = new Rect(rect.x, rect.y + rect.height * 0.58f,
                rect.width, rect.height * 0.42f);
            FillRect(caption, new Color(0f, 0f, 0f, selected ? 0.68f : 0.78f));
            var label = "TIPS " + (entry.Id + 1).ToString("00") + "\n" + entry.DisplayTitle;
            GUI.Label(Inset(caption, 4f * UiScale), label, _tipCardStyle);
            if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
            {
                if (selected)
                {
                    StartSelectedTip();
                }
                else
                {
                    _host.SelectTip(entry.Id, _runtime.Memory);
                    SuppressInput();
                }
            }
        }

        private void DrawExtrasScreen(Rect safe)
        {
            var scale = UiScale;
            FillRect(safe, Color.black);
            var panel = new Rect(safe.x + safe.width * 0.16f, safe.y + safe.height * 0.12f,
                safe.width * 0.68f, safe.height * 0.76f);
            DrawPcModalPanel(panel);
            DrawSectionHeader(panel, "追加内容");

            if (_chapterJumpSections.Count == 0 && _host != null)
            {
                _chapterJumpSections.AddRange(_host.GetChapterJumpSections());
            }

            var width = Mathf.Min(panel.width * 0.66f, 520f * scale);
            var height = 58f * scale;
            var gap = 14f * scale;
            var x = panel.center.x - width * 0.5f;
            var buttonCount = IsBonusContentUnlocked ? 4 : 3;
            var groupHeight = buttonCount * height + (buttonCount - 1) * gap;
            var y = panel.center.y - groupHeight * 0.5f;
            var unlocked = Mathf.Max(ChapterProgressCount(_host.CurrentChapterNumber),
                PlayerPrefs.GetInt(ChapterJumpUnlockedKey, 0));
            if (_chapterJumpSections.Count > 0 && unlocked > 0 &&
                PcButton(new Rect(x, y, width, height), "章节跳跃", true))
            {
                _chapterJumpVisible = true;
                SuppressInput();
            }
            else if (_chapterJumpSections.Count == 0 || unlocked <= 0)
            {
                DrawDisabledPcButton(new Rect(x, y, width, height), "章节跳跃", true);
            }

            y += height + gap;
            var tipsUnlockedChapter = GetTipsUnlockedChapter();
            var hasUnlockedTips = _host.HasUnlockedTips(
                _runtime.Memory, tipsUnlockedChapter);
            if (hasUnlockedTips &&
                PcButton(new Rect(x, y, width, height), "查看 TIPS", true))
            {
                OpenTipsLibrary();
            }
            else if (!hasUnlockedTips)
            {
                DrawDisabledPcButton(new Rect(x, y, width, height),
                    "查看 TIPS（未解锁）", true);
            }
            y += height + gap;
            if (IsBonusContentUnlocked)
            {
                if (PcButton(new Rect(x, y, width, height), BonusContentName, true))
                {
                    StartBonusContent();
                }
                y += height + gap;
            }
            if (PcButton(new Rect(x, y, width, height), "返回", true))
            {
                _extrasVisible = false;
                _chapterJumpVisible = false;
                SuppressInput();
            }
        }

        private void DrawChapterJumpScreen(Rect safe)
        {
            var scale = UiScale;
            FillRect(safe, Color.black);
            var panel = new Rect(safe.x + safe.width * 0.10f, safe.y + safe.height * 0.06f,
                safe.width * 0.80f, safe.height * 0.88f);
            DrawPcModalPanel(panel);
            DrawSectionHeader(panel, "章节跳跃");

            if (_chapterJumpSections.Count == 0 && _host != null)
            {
                _chapterJumpSections.AddRange(_host.GetChapterJumpSections());
            }

            var unlocked = Mathf.Clamp(Mathf.Max(ChapterProgressCount(_host.CurrentChapterNumber),
                PlayerPrefs.GetInt(ChapterJumpUnlockedKey, 0)), 0, _chapterJumpSections.Count);
            var list = new Rect(panel.x + 30f * scale, panel.y + 72f * scale,
                panel.width - 60f * scale, panel.height - 142f * scale);
            if (unlocked <= 0)
            {
                GUI.Label(Inset(list, 18f * scale), "完成章节后，这里会显示可跳转的章节。", _dialogueStyle);
            }
            else
            {
                var gap = 10f * scale;
                var columns = safe.width < 700f * scale ? 1 : 2;
                var rowHeight = 52f * scale;
                var rows = Mathf.CeilToInt(unlocked / (float)columns);
                var content = new Rect(0f, 0f, list.width, rows * (rowHeight + gap));
                _chapterJumpScroll = GUI.BeginScrollView(list, _chapterJumpScroll, content);
                for (var i = 0; i < unlocked; i++)
                {
                    var section = _chapterJumpSections[i];
                    var label = "第" + (i + 1) +
                                (HigurashiActiveChapter.Profile.EpisodeNumber >= 5 ? "章" : "天");
                    var column = i % columns;
                    var row = i / columns;
                    var button = new Rect(column * (list.width / columns) + gap * 0.5f,
                        row * (rowHeight + gap), list.width / columns - gap, rowHeight);
                    if (PcButton(button, HigurashiActiveChapter.Profile.ChineseChapterTitle + " " + label,
                            true))
                    {
                        StartChapterJump(section);
                    }
                }
                GUI.EndScrollView();
            }

            if (PcButton(new Rect(panel.center.x - 145f * scale, panel.yMax - 52f * scale,
                    290f * scale, 38f * scale), "返回", true))
            {
                _chapterJumpVisible = false;
                SuppressInput();
            }
        }

        private void StartChapterJump(string section)
        {
            if (_runtime == null || _host == null || string.IsNullOrWhiteSpace(section) ||
                !_host.StartFromTitle(_runtime.Memory))
            {
                return;
            }

            try
            {
                _runtime.Memory.SetLocalFlag("TipsMode", 0);
                _runtime.Memory.SetLocalFlag("LOCALWORK_NO_RESULT", 0);
                var chapterIndex = _chapterJumpSections.IndexOf(section);
                var runtimeSection = section;
                var episode = HigurashiActiveChapter.Profile.EpisodeNumber;
                if (EpisodeChapterJumpMap.TryGetFlowJumpValue(episode, section, out var jumpValue))
                {
                    _runtime.Memory.SetLocalFlag("s_jump", jumpValue);
                    _runtime.Memory.SetLocalFlag("ChapterNumber",
                        episode == 8 ? jumpValue : Math.Max(0, chapterIndex));
                    runtimeSection = "Game";
                }
                else if (chapterIndex >= 0)
                {
                    _runtime.Memory.SetLocalFlag("ChapterNumber", chapterIndex);
                }
                _host.StopAllAudio();
                _host.PrepareForChapterJump();
                _fastTraversal.Stop();
                _autoMode = false;
                _timeline.Clear();
                _runtime.JumpToSectionFromUi(runtimeSection);
                HigurashiDiagnosticLog.Info("ChapterJump",
                    "Started section=" + runtimeSection + " token=" + section +
                    " chapterIndex=" + chapterIndex);
                CloseAllModals();
                SuppressInput();
                DriveRuntime(false);
                CaptureDialogueCheckpoint();
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Unable to jump to chapter " + section + ": " + exception.Message);
                ShowToast("章节跳跃失败");
            }
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

            var footerHeight = Mathf.Max(44f, 46f * scale);
            var footerY = panel.yMax - footerHeight - 18f * scale;
            var info = new Rect(panel.x + 22f * scale, gridY + gridHeight + 14f * scale,
                panel.width - 44f * scale, footerY - gridY - gridHeight - 26f * scale);
            DrawFragmentDetails(info);

            var navWidth = Mathf.Min(142f * scale, panel.width * 0.18f);
            if (page > 0 && PcButton(new Rect(panel.x + 22f * scale, footerY,
                    navWidth, footerHeight), "上一页", true))
            {
                _host.ChangeFragmentPage(-1, _runtime.Memory);
                HigurashiDiagnosticLog.Info("Fragment",
                    "Changed page=" + _host.FragmentPage + " direction=previous " +
                    RuntimeLocation());
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
            var centerAvailable = Mathf.Max(260f * scale,
                panel.width - navWidth * 2f - 92f * scale);
            var centerGap = 8f * scale;
            var centerButtonWidth = Mathf.Min(145f * scale,
                (centerAvailable - centerGap * 2f) / 3f);
            var centerGroupWidth = centerButtonWidth * 3f + centerGap * 2f;
            var centerX = panel.center.x - centerGroupWidth * 0.5f;
            if (FittedPcButton(new Rect(centerX, footerY,
                    centerButtonWidth, footerHeight), "返回总览", 11))
            {
                ExitFragmentList();
            }
            if (FittedPcButton(new Rect(centerX + centerButtonWidth + centerGap, footerY,
                    centerButtonWidth, footerHeight), "保存与载入", 11))
            {
                _saveLoadVisible = true;
                SuppressInput();
            }
            if (FittedPcButton(new Rect(centerX + (centerButtonWidth + centerGap) * 2f, footerY,
                    centerButtonWidth, footerHeight), "返回主菜单", 11))
            {
                HigurashiDiagnosticLog.Info("Fragment",
                    "Returning to title from fragment list page=" + page + " " +
                    RuntimeLocation());
                ReturnToTitle();
            }

            var nextRect = new Rect(panel.xMax - 22f * scale - navWidth, footerY,
                navWidth, footerHeight);
            if (page < pageCount - 1 && PcButton(nextRect, "下一页", true))
            {
                _host.ChangeFragmentPage(1, _runtime.Memory);
                HigurashiDiagnosticLog.Info("Fragment",
                    "Changed page=" + _host.FragmentPage + " direction=next " +
                    RuntimeLocation());
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
            var toolbarReserve = Mathf.Min(content.width * 0.18f, 250f * scale);
            var dialogueWidth = content.width - 56f * scale - toolbarReserve;
            var layoutText = _host.Dialogue + (_host.IsDialogueRevealComplete ? "　▼" : string.Empty);
            var speakerHeight = string.IsNullOrEmpty(_host.Speaker) ? 0f : 39f * scale;
            var dialogueHeight = _dialogueStyle.CalcHeight(new GUIContent(layoutText), dialogueWidth);
            var minimumHeight = Mathf.Max(content.height * 0.18f, 132f * scale);
            // Long PC text boxes can contain several appended lines. Let the
            // window grow before clipping instead of cutting the last line.
            var maximumHeight = Mathf.Max(minimumHeight, content.height * 0.48f);
            var height = Mathf.Clamp(speakerHeight + dialogueHeight + 26f * scale,
                minimumHeight, maximumHeight);
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
            var previousGuiColor = GUI.color;
            GUI.color = new Color(previousGuiColor.r, previousGuiColor.g,
                previousGuiColor.b, previousGuiColor.a * windowFade);
            if (!string.IsNullOrEmpty(_host.Speaker))
            {
                GUI.Label(new Rect(left, top, dialogueWidth, 40f * scale),
                    _host.Speaker, _speakerStyle);
                top += 39f * scale;
            }
            var previousDialogueColor = _dialogueStyle.normal.textColor;
            _dialogueStyle.normal.textColor = _host.DialogueColor;
            GUI.Label(
                new Rect(left, top, dialogueWidth, rect.yMax - top - 14f * scale),
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
            var content = GetContentRect();
            var scale = UiScale;
            var isEpisodeEight = HigurashiActiveChapter.Profile.EpisodeNumber == 8;
            GUI.color = new Color(0f, 0f, 0f, 0.16f);
            GUI.DrawTexture(content, _solidWhite);
            GUI.color = Color.white;

            if (isEpisodeEight)
            {
                DrawEpisodeEightCredits(content, scale);
                return;
            }

            var left = content.x + 34f * scale;
            var top = content.y + 22f * scale;
            DrawShadowLabel(new Rect(left, top, content.width * 0.52f, 64f * scale),
                "YCX STUDIOS 汉化组", _titleStyle);
            GUI.Label(new Rect(content.xMax - content.width * 0.38f - 30f * scale,
                    content.y + 26f * scale, content.width * 0.38f, 82f * scale),
                "寒蝉鸣泣之时\n" + HigurashiActiveChapter.Profile.ChineseChapterTitle,
                _panelTitleStyle);
            top += 88f * scale;
            var credits = "参与人员\n" +
                "原翻译：mayurina（里娜），srwfe（繁），纯真な工房（简），NNET，雪\n" +
                "原润色：61y，晴，只是路人，Mize\n" +
                "监制：ycx\n技术：ycx\n翻译：ycx\n" +
                "校对＆润色：ycx，ReKo，DoSun，Xuee\n" +
                "美工：ycx\n测试：ycx";
            GUI.Label(new Rect(left, top, content.width * 0.72f, content.height * 0.62f),
                credits, _dialogueStyle);
            DrawShadowLabel(new Rect(content.x, content.yMax - 145f * scale,
                    content.width, 58f * scale),
                "简体中文版汉化补丁 Ver 1.4", _titleStyle);
            GUI.Label(new Rect(content.x, content.yMax - 82f * scale,
                    content.width, 38f * scale),
                "哔哩哔哩专栏　×　其乐 KeyLol　共同发布", _panelTitleStyle);
            GUI.Label(new Rect(content.x, content.yMax - 42f * scale,
                    content.width, 30f * scale),
                "轻触屏幕继续", _statusStyle);
        }

        private void DrawEpisodeEightCredits(Rect content, float scale)
        {
            var marginX = Mathf.Max(18f * scale, content.width * 0.027f);
            var marginY = Mathf.Max(14f * scale, content.height * 0.028f);
            var headingHeight = Mathf.Clamp(content.height * 0.075f,
                38f * scale, 62f * scale);
            var headingStyle = new GUIStyle(_sectionHeaderStyle)
            {
                alignment = TextAnchor.MiddleLeft
            };
            DrawShadowLabel(new Rect(content.x + marginX, content.y + marginY,
                    content.width * 0.48f, headingHeight),
                "参与人员", headingStyle);

            var credits = "翻译：990，麻生早纪\n" +
                          "校对：枝瀬愛\n" +
                          "程序：饭\n" +
                          "润色：990，麻生早纪\n" +
                          "特别鸣谢：蝉吧全体吧友\n" +
                          "　　　　　DS，DB，GPT";
            var bodyRect = new Rect(content.x + marginX,
                content.y + marginY + headingHeight + 4f * scale,
                content.width * 0.64f,
                content.height - marginY * 2f - headingHeight - 4f * scale);
            var bodyStyle = new GUIStyle(_dialogueStyle)
            {
                alignment = TextAnchor.UpperLeft,
                wordWrap = true,
                clipping = TextClipping.Clip
            };
            var bodyContent = new GUIContent(credits);
            var minimumFontSize = Mathf.Max(16, Mathf.RoundToInt(18f * scale));
            while (bodyStyle.fontSize > minimumFontSize &&
                   bodyStyle.CalcHeight(bodyContent, bodyRect.width) > bodyRect.height)
            {
                bodyStyle.fontSize--;
            }
            DrawShadowLabel(bodyRect, credits, bodyStyle);

            if (_creditsSeriesLogo == null)
            {
                _creditsSeriesLogo = _host.GetInterfaceTexture("logo");
            }
            if (_creditsChapterTitle == null)
            {
                _creditsChapterTitle = _host.GetInterfaceTexture("scenario/title");
            }

            var logoWidth = Mathf.Min(content.width * 0.41f, content.height * 0.77f);
            var logoHeight = logoWidth / 2.9569f;
            var logoRect = new Rect(content.xMax - marginX - logoWidth,
                content.y + marginY, logoWidth, logoHeight);
            var chapterWidth = logoWidth * 0.48f;
            var chapterHeight = chapterWidth / 2.4315f;
            var chapterRect = new Rect(content.xMax - marginX - chapterWidth,
                logoRect.yMax + 5f * scale, chapterWidth, chapterHeight);

            if (_creditsSeriesLogo != null && _creditsChapterTitle != null)
            {
                DrawTextureRegion(logoRect, _creditsSeriesLogo,
                    new Rect(206f / 1920f, 285f / 1080f, 1508f / 1920f, 510f / 1080f));
                DrawTextureRegion(chapterRect, _creditsChapterTitle,
                    new Rect(308f / 1920f, 829f / 1080f, 355f / 1920f, 146f / 1080f));
            }
            else
            {
                var fallbackRect = new Rect(content.xMax - marginX - content.width * 0.38f,
                    content.y + marginY, content.width * 0.38f, content.height * 0.22f);
                DrawShadowLabel(fallbackRect, "寒蝉鸣泣之时解\n祭囃篇", _panelTitleStyle);
            }
        }

        private static void DrawTextureRegion(Rect destination, Texture texture, Rect source)
        {
            GUI.DrawTextureWithTexCoords(destination, texture, source, true);
        }

        private void DrawTitleScreen()
        {
            var safe = GetGuiSafeArea();
            if (_chapterJumpVisible)
            {
                DrawChapterJumpScreen(safe);
                return;
            }
            if (_extrasVisible)
            {
                DrawExtrasScreen(safe);
                return;
            }
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
            var buttonCount = IsTipsMenuUnlocked ? 5 : 4;
            var groupHeight = buttonCount * buttonHeight + (buttonCount - 1) * gap;
            var copyrightY = safe.yMax - 43f * scale;
            var portY = copyrightY - 66f * scale;
            var y = Mathf.Min(safe.y + safe.height * 0.52f, portY - groupHeight - 10f * scale);
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
            if (IsTipsMenuUnlocked && PcButton(new Rect(x, y, width, buttonHeight), "追加内容"))
            {
                RefreshUnlockProgressFromSaves();
                _extrasVisible = true;
                SuppressInput();
            }
            if (IsTipsMenuUnlocked)
            {
                y += buttonHeight + gap;
            }
            if (PcButton(new Rect(x, y, width, buttonHeight), "操作说明"))
            {
                _helpVisible = true;
                SuppressInput();
            }
            DrawOutlinedLabel(new Rect(safe.x, portY, safe.width, 34f * scale),
                "iOS版移植", _portTitleStyle);
            DrawOutlinedLabel(new Rect(safe.x, portY + 31f * scale, safe.width, 27f * scale),
                "贴吧@bugjump bilibili@Hyperion233", _portSubtitleStyle);
            if (GUI.Button(new Rect(safe.x, portY - 4f * scale,
                    safe.width, 66f * scale), GUIContent.none, GUIStyle.none))
            {
                RegisterPortCreditTap();
            }
            GUI.Label(new Rect(safe.x, safe.yMax - 43f * scale, safe.width, 30f * scale),
                "(C) 龙骑士07 / 07th Expansion", _panelTitleStyle);
        }

        private void RegisterPortCreditTap()
        {
            if (Time.unscaledTime > _portCreditTapDeadline)
            {
                _portCreditTapCount = 0;
            }
            _portCreditTapDeadline = Time.unscaledTime + 2.5f;
            _portCreditTapCount++;
            if (_portCreditTapCount < 5)
            {
                SuppressInput();
                return;
            }
            _portCreditTapCount = 0;
            _portCreditTapDeadline = 0f;
            UnlockNextChapterFromPortCredit();
            SuppressInput();
        }

        private void DrawGameplayControls()
        {
            if (!_host.GameplayUiVisible || (!_host.SavingEnabled && !_host.TipReading) ||
                !_host.InterfaceEnabled ||
                IsModalVisible || _host.ChoiceVisible ||
                _host.CreditsVisible || _host.ChapterPreviewVisible || _host.HistoryVisible)
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

            if (_host.SavingEnabled)
            {
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
                "触控快捷操作\n\n单指轻触／右→左　推进剧情\n单指左→右　回到上一文本框\n上划　查看记录\n下划　隐藏／显示文本框\n三指左→右　快速回退\n三指右→左　快速前进\n快进中任意触摸　停止",
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
            GUI.Label(new Rect(textX, rect.y + 29f * scale, textWidth, rect.height - 31f * scale),
                info == null
                    ? "— 空存档 —"
                    : info.Timestamp.ToString("MM-dd HH:mm") + "  " +
                      (IsKnownLegacyTipsBrowserSave(info) ||
                       IsKnownInvalidControlFlowSave(info)
                          ? "检测到异常流程存档，载入时自动恢复"
                          : info.Summary),
                _saveSummaryStyle);

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
                        HigurashiDiagnosticLog.Info("Save",
                            "Deleted slot=" + slot + " kind=" + SaveKind(slot));
                        ShowToast(slot == LatestSaveSlot
                            ? "已删除最新保存"
                            : "已删除文件 " + (slot - 1).ToString("00", CultureInfo.InvariantCulture));
                    }
                    catch (Exception exception)
                    {
                        Debug.LogWarning("Unable to delete save: " + exception.Message);
                        HigurashiDiagnosticLog.Warning("Save",
                            "Delete failed slot=" + slot + " " + exception.Message);
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

            var innerX = panel.x + 34f * scale;
            var innerWidth = panel.width - 68f * scale;
            var columnGap = 24f * scale;
            var width = (innerWidth - columnGap) * 0.5f;
            var buttonHeight = Mathf.Max(44f, 45f * scale);
            var contentTop = panel.y + 76f * scale;
            var contentBottom = panel.yMax - 66f * scale;
            var viewport = new Rect(innerX, contentTop, innerWidth,
                Mathf.Max(80f * scale, contentBottom - contentTop));
            var leftContentHeight = 6f * buttonHeight + 5f * 9f * scale;
            var rightContentHeight = 6f * 66f * scale;
            var contentHeight = Mathf.Max(viewport.height,
                Mathf.Max(leftContentHeight, rightContentHeight) + 8f * scale);
            var content = new Rect(0f, 0f,
                Mathf.Max(1f, viewport.width - 18f * scale), contentHeight);
            _settingsScroll = GUI.BeginScrollView(viewport, _settingsScroll, content);

            var x = 0f;
            var y = 0f;
            var artName = _host.ArtSets.Count == 0
                ? "CG"
                : MobileOptionDisplayName.ArtSet(
                    _host.ArtSets[Mathf.Clamp(_settings.artSetIndex, 0, _host.ArtSets.Count - 1)]
                        .DisplayName);
            if (FittedPcButton(new Rect(x, y, width, buttonHeight), "立绘与背景：" + artName, 13))
            {
                _settings.artSetIndex = Next(_settings.artSetIndex, _host.ArtSets.Count);
                _host.ApplySettings(_runtime.Memory);
            }
            y += buttonHeight + 9f * scale;

            var audioName = _host.AudioSets.Count == 0
                ? "脚本默认"
                : MobileOptionDisplayName.AudioSet(
                    _host.AudioSets[Mathf.Clamp(_settings.audioPresetIndex, 0,
                        _host.AudioSets.Count - 1)].DisplayName);
            if (FittedPcButton(new Rect(x, y, width, buttonHeight), "BGM / SE：" + audioName, 11,
                    "BGM / SE\n" + audioName))
            {
                _settings.audioPresetIndex = Next(_settings.audioPresetIndex, _host.AudioSets.Count);
                _host.ApplySettings(_runtime.Memory);
            }
            y += buttonHeight + 9f * scale;
            if (FittedPcButton(new Rect(x, y, width, buttonHeight),
                    "画面适配：" + PresentationModeName(_settings.presentationMode), 13))
            {
                _settings.presentationMode = (MobilePresentationMode)(((int)_settings.presentationMode + 1) % 3);
            }
            y += buttonHeight + 9f * scale;
            if (FittedPcButton(new Rect(x, y, width, buttonHeight),
                    "口型同步（仅主机版立绘）：" + (_settings.lipSync ? "开" : "关"), 13))
            {
                _settings.lipSync = !_settings.lipSync;
                _host.ApplySettings(_runtime.Memory);
            }
            y += buttonHeight + 9f * scale;
            if (FittedPcButton(new Rect(x, y, width, buttonHeight),
                    "自动保存：" + (_settings.autoSave ? "开" : "关"), 13))
            {
                _settings.autoSave = !_settings.autoSave;
            }
            y += buttonHeight + 9f * scale;
            if (FittedPcButton(new Rect(x, y, width, buttonHeight), "导出系统日志", 13))
            {
                ExportDiagnosticLog();
                SuppressInput();
            }
            y += buttonHeight + 15f * scale;

            x = width + columnGap;
            y = 0f;
            var previousBgmVolume = _settings.bgmVolume;
            DrawSliderRow(x, ref y, width, "背景音乐音量", ref _settings.bgmVolume, 0, 100);
            if (_settings.bgmVolume != previousBgmVolume)
            {
                _host.ApplyAudioSettings();
            }
            DrawSliderRow(x, ref y, width, "语音音量", ref _settings.voiceVolume, 0, 100);
            DrawSliderRow(x, ref y, width, "文本框透明度", ref _settings.windowOpacity, 0, 100);
            DrawSliderRow(x, ref y, width, "文字大小", ref _settings.textScale, 80, 150);
            DrawSliderRow(x, ref y, width, "文本速度", ref _settings.textSpeed, 0, 100);
            DrawSliderRow(x, ref y, width, "自动播放速度", ref _settings.autoSpeed, 0, 100);

            GUI.EndScrollView();

            if (PcButton(new Rect(innerX, panel.yMax - 56f * scale,
                    innerWidth, 43f * scale), "保存并返回", true))
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
                "单指轻触／右→左　推进剧情\n" +
                "单指左→右　回到上一完整文本框，并恢复画面与声音\n" +
                "单指上划　打开剧情记录\n" +
                "单指下划　隐藏或显示文本框\n" +
                "三指左→右　逐句快速回退\n" +
                "三指右→左　逐句快速前进\n" +
                "快速前进／回退中任意触摸　立即停止";
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
            var safe = GetGuiSafeArea();
            FillRect(safe, new Color(0f, 0f, 0f, 0.20f));
            DrawSectionHeader(safe, "剧情记录");

            // Keep the PC layout's scenery visible through a dark, centered reading panel.
            var panel = new Rect(safe.x + safe.width * 0.095f, safe.y + 68f * scale,
                safe.width * 0.81f, safe.height - 88f * scale);
            FillRect(panel, new Color(0.035f, 0.055f, 0.060f, 0.66f));
            var closeWidth = Mathf.Min(190f * scale, panel.width * 0.18f);
            var closeHeight = 43f * scale;
            var closeRect = new Rect(panel.xMax - closeWidth - 18f * scale,
                panel.yMax - closeHeight - 16f * scale, closeWidth, closeHeight);
            var viewport = new Rect(panel.x + 25f * scale, panel.y + 20f * scale,
                panel.width - 50f * scale, panel.height - closeHeight - 50f * scale);
            var contentWidth = viewport.width - 26f * scale;
            var entryWidth = contentWidth - 20f * scale;
            var contentHeight = 16f * scale;
            for (var i = 0; i < _host.History.Count; i++)
            {
                contentHeight += Mathf.Max(36f * scale,
                    _historyStyle.CalcHeight(new GUIContent(_host.History[i]), entryWidth)) + 12f * scale;
            }
            contentHeight = Mathf.Max(viewport.height, contentHeight);
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
                new Rect(0, 0, contentWidth, contentHeight));
            var y = 8f * scale;
            for (var i = 0; i < _host.History.Count; i++)
            {
                var entryHeight = Mathf.Max(36f * scale,
                    _historyStyle.CalcHeight(new GUIContent(_host.History[i]), entryWidth));
                var entryRect = new Rect(10f * scale, y, entryWidth, entryHeight);
                if (GUI.Button(entryRect, GUIContent.none, GUIStyle.none) &&
                    _host.ReplayHistoryVoice(i))
                {
                    ShowToast("正在重播该句语音");
                }
                GUI.Label(entryRect, _host.History[i], _historyStyle);
                y += entryHeight + 12f * scale;
            }
            GUI.EndScrollView();
            if (PcButton(closeRect, "关闭", true))
            {
                _host.HistoryVisible = false;
                _historyAutoScrollPending = false;
                SuppressInput();
            }
        }

        private void DrawBadEndingDecision()
        {
            var safe = GetGuiSafeArea();
            DrawModalShade(safe);
            var scale = UiScale;
            var width = Mathf.Min(760f * scale, safe.width - 64f * scale);
            var height = Mathf.Min(310f * scale, safe.height - 54f * scale);
            var panel = new Rect(safe.center.x - width * 0.5f,
                safe.center.y - height * 0.5f, width, height);
            DrawPcModalPanel(panel);
            DrawSectionHeader(panel, "坏结局");

            GUI.Label(new Rect(panel.x + 38f * scale, panel.y + 82f * scale,
                    panel.width - 76f * scale, 62f * scale),
                "本路线已结束。可以返回刚才的剧情选项重新选择，或返回主菜单。",
                _dialogueStyle);

            var gap = 18f * scale;
            var buttonWidth = (panel.width - 76f * scale - gap) * 0.5f;
            var buttonY = panel.yMax - 72f * scale;
            if (PcButton(new Rect(panel.x + 38f * scale, buttonY,
                    buttonWidth, 46f * scale), "回到选项", true))
            {
                ReturnToStoryChoice();
            }
            if (PcButton(new Rect(panel.x + 38f * scale + buttonWidth + gap,
                    buttonY, buttonWidth, 46f * scale), "返回主菜单", true))
            {
                AcceptBadEndingAndReturnToTitle();
            }
        }

        private void DrawImportScreen()
        {
            var safe = GetGuiSafeArea();
            var scale = UiScale;
            var panelWidth = Mathf.Min(1096f * scale, safe.width - 48f * scale);
            var panelTop = safe.y + safe.height * 0.15f;
            var availableHeight = Mathf.Max(1f, safe.yMax - panelTop - 8f * scale);
            var panelHeight = Mathf.Min(520f * scale, availableHeight);
            panelHeight = Mathf.Max(panelHeight, Mathf.Min(280f, availableHeight));
            var panel = new Rect(safe.center.x - panelWidth * 0.5f,
                panelTop, panelWidth, panelHeight);

            var titleHeight = Mathf.Max(38f, 52f * scale);
            GUI.Label(new Rect(panel.x, Mathf.Max(safe.y, panel.y - titleHeight - 10f * scale),
                    panel.width, titleHeight),
                HigurashiActiveChapter.Profile.FullChineseTitle + "　 iOS 移植版",
                _importTitleStyle);

            DrawPcModalPanel(panel);
            var paddingX = Mathf.Max(22f * scale, panel.width * 0.055f);
            var header = new Rect(panel.x + 24f * scale, panel.y + panel.height * 0.035f,
                Mathf.Min(430f * scale, panel.width * 0.48f),
                Mathf.Clamp(panel.height * 0.085f, 30f, 42f * scale));
            GUI.DrawTexture(header, _sectionHeader, ScaleMode.StretchToFill, true);
            GUI.Label(new Rect(header.x + 16f * scale, header.y,
                    header.width - 24f * scale, header.height),
                "数据包导入", _sectionHeaderStyle);

            var packNameWidth = Mathf.Max(1f, panel.xMax - paddingX - header.xMax - 18f * scale);
            GUI.Label(new Rect(header.xMax + 18f * scale, header.y,
                    packNameWidth, header.height),
                HigurashiActiveChapter.Profile.DataPackFileName, _importDetailRightStyle);

            var status = _initializationAttempted ? _runtimeStatus : _dataPack.Status;
            var progress = _initializationAttempted ? 1f : Mathf.Clamp01(_dataPack.Progress);
            var stage = ImportStage(status, progress, _dataPack.IsRunning, _initializationAttempted);
            DrawImportSteps(new Rect(panel.x + paddingX, panel.y + panel.height * 0.19f,
                panel.width - paddingX * 2f, panel.height * 0.20f), stage, scale);

            var body = new Rect(panel.x + paddingX, panel.y + panel.height * 0.49f,
                panel.width - paddingX * 2f, panel.height * 0.29f);
            var failed = IsImportFailure(status) ||
                         (_initializationAttempted &&
                          (ContainsImportText(status, "failed") ||
                           ContainsImportText(status, "fault")));
            var headline = ImportHeadline(status, _dataPack.IsRunning, _initializationAttempted);
            var percentWidth = Mathf.Clamp(body.width * 0.16f, 76f * scale, 150f * scale);
            var statusHeight = Mathf.Max(34f, body.height * 0.31f);
            var statusStyle = failed ? new GUIStyle(_importStatusStyle) : _importStatusStyle;
            if (failed)
            {
                statusStyle.normal.textColor = new Color(1f, 0.32f, 0.27f);
            }
            GUI.Label(new Rect(body.x, body.y, body.width - percentWidth - 12f * scale,
                    statusHeight), headline, statusStyle);

            if (_dataPack.IsRunning || progress > 0f || _initializationAttempted)
            {
                GUI.Label(new Rect(body.xMax - percentWidth, body.y,
                        percentWidth, statusHeight),
                    Mathf.RoundToInt(progress * 100f) + "%", _importPercentStyle);
                var track = new Rect(body.x, body.y + statusHeight + 8f * scale,
                    body.width, Mathf.Max(18f, 22f * scale));
                DrawImportProgressBar(track, progress, scale);

                var detailY = track.yMax + 11f * scale;
                var currentFile = _dataPack.CurrentFile;
                var detail = failed ? status : currentFile;
                GUI.Label(new Rect(body.x, detailY, body.width * 0.68f, 26f * scale),
                    detail, _importDetailStyle);
                var count = _dataPack.TotalFiles > 0
                    ? _dataPack.CurrentFileIndex.ToString("N0") + " / " +
                      _dataPack.TotalFiles.ToString("N0") + " 个文件"
                    : string.Empty;
                GUI.Label(new Rect(body.x + body.width * 0.68f, detailY,
                        body.width * 0.32f, 26f * scale),
                    count, _importDetailRightStyle);
            }
            else if (!_initializationAttempted && !failed)
            {
                var buttonWidth = Mathf.Clamp(body.width * 0.46f, 260f * scale, 520f * scale);
                var buttonHeight = Mathf.Max(44f, 54f * scale);
                if (PcButton(new Rect(body.center.x - buttonWidth * 0.5f,
                        body.y + statusHeight + 10f * scale, buttonWidth, buttonHeight),
                        "请选择数据包"))
                {
                    BeginDataPackSelection();
                }
            }

            var footerY = panel.y + panel.height * 0.82f;
            var footerHeight = panel.yMax - footerY;
            FillRect(new Rect(panel.x + paddingX, footerY,
                panel.width - paddingX * 2f, Mathf.Max(1f, 1f * scale)),
                new Color(0.30f, 0.30f, 0.32f, 0.82f));
            var footerText = failed ? status : ImportSecurityStatus(stage);
            FillRect(new Rect(panel.x + paddingX, footerY + footerHeight * 0.5f - 3f * scale,
                Mathf.Max(6f, 7f * scale), Mathf.Max(6f, 7f * scale)),
                failed ? new Color(0.65f, 0.05f, 0.03f) : new Color(0.78f, 0.02f, 0.015f));
            GUI.Label(new Rect(panel.x + paddingX + 16f * scale, footerY + 7f * scale,
                    panel.width * 0.57f, Mathf.Max(1f, footerHeight - 10f * scale)),
                footerText, _importDetailStyle);
            if (failed && !_initializationAttempted && !_dataPack.IsRunning)
            {
                var retryWidth = Mathf.Clamp(panel.width * 0.27f, 150f * scale, 260f * scale);
                var retryHeight = Mathf.Max(44f, 44f * scale);
                if (PcButton(new Rect(panel.xMax - paddingX - retryWidth,
                        footerY + 3f * scale, retryWidth, retryHeight),
                        "重新选择数据包", true))
                {
                    BeginDataPackSelection();
                }
            }
            else
            {
                GUI.Label(new Rect(panel.x + panel.width * 0.61f, footerY + 7f * scale,
                        panel.width - paddingX - panel.width * 0.61f,
                        Mathf.Max(1f, footerHeight - 10f * scale)),
                    "完成后将自动进入 OP 动画设置", _importDetailRightStyle);
            }
        }

        private void DrawImportSteps(Rect area, int activeStage, float scale)
        {
            var labels = new[] { "选择文件", "验证数据包", "解压与校验", "准备启动" };
            var markerSize = Mathf.Clamp(area.height * 0.52f, 30f, 42f * scale);
            var lineY = area.y + markerSize * 0.5f;
            var firstX = area.x + area.width * 0.125f;
            var lastX = area.x + area.width * 0.875f;
            FillRect(new Rect(firstX, lineY, lastX - firstX, Mathf.Max(1f, scale)),
                new Color(0.38f, 0.38f, 0.40f, 0.9f));
            if (activeStage > 0)
            {
                var redWidth = (lastX - firstX) * Mathf.Clamp01(activeStage / 3f);
                FillRect(new Rect(firstX, lineY, redWidth, Mathf.Max(1f, scale)),
                    new Color(0.72f, 0.02f, 0.015f, 0.95f));
            }

            for (var i = 0; i < labels.Length; i++)
            {
                var centerX = area.x + area.width * ((i + 0.5f) / labels.Length);
                var marker = new Rect(centerX - markerSize * 0.5f, area.y,
                    markerSize, markerSize);
                var completed = i < activeStage;
                var active = i == activeStage;
                FillRect(marker, active
                    ? new Color(0.93f, 0.025f, 0.015f, 1f)
                    : completed
                        ? new Color(0.48f, 0.005f, 0.005f, 1f)
                        : new Color(0.025f, 0.025f, 0.03f, 1f));
                var border = Mathf.Max(1f, active ? 2f * scale : scale);
                FillRect(new Rect(marker.x, marker.y, marker.width, border),
                    active ? new Color(1f, 0.32f, 0.28f) : new Color(0.52f, 0.52f, 0.55f));
                FillRect(new Rect(marker.x, marker.yMax - border, marker.width, border),
                    active ? new Color(1f, 0.32f, 0.28f) : new Color(0.52f, 0.52f, 0.55f));
                FillRect(new Rect(marker.x, marker.y, border, marker.height),
                    active ? new Color(1f, 0.32f, 0.28f) : new Color(0.52f, 0.52f, 0.55f));
                FillRect(new Rect(marker.xMax - border, marker.y, border, marker.height),
                    active ? new Color(1f, 0.32f, 0.28f) : new Color(0.52f, 0.52f, 0.55f));
                GUI.Label(marker, (i + 1).ToString(), _importStepStyle);
                GUI.Label(new Rect(centerX - area.width * 0.12f,
                        marker.yMax + 4f * scale, area.width * 0.24f,
                        Mathf.Max(20f, area.yMax - marker.yMax - 3f * scale)),
                    labels[i], _importStepStyle);
            }
        }

        private void DrawImportProgressBar(Rect rect, float progress, float scale)
        {
            FillRect(rect, new Color(0.68f, 0.68f, 0.70f, 0.88f));
            var border = Mathf.Max(1f, 2f * scale);
            var inner = Inset(rect, border);
            FillRect(inner, new Color(0.12f, 0.12f, 0.14f, 1f));
            var fillWidth = inner.width * Mathf.Clamp01(progress);
            if (fillWidth > 0f)
            {
                FillRect(new Rect(inner.x, inner.y, fillWidth, inner.height),
                    new Color(0.82f, 0.02f, 0.015f, 1f));
                var markerWidth = Mathf.Max(2f, 3f * scale);
                FillRect(new Rect(Mathf.Min(inner.xMax - markerWidth,
                        inner.x + fillWidth - markerWidth * 0.5f), rect.y,
                        markerWidth, rect.height), Color.white);
            }
        }

        private static int ImportStage(string status, float progress, bool running,
            bool initializationAttempted)
        {
            if (initializationAttempted || progress >= 0.995f || ContainsImportText(status, "导入成功"))
            {
                return 3;
            }
            if (progress >= 0.1f || ContainsImportText(status, "解压"))
            {
                return 2;
            }
            if (running || ContainsImportText(status, "校验") ||
                ContainsImportText(status, "SHA-256") || ContainsImportText(status, "已选择"))
            {
                return 1;
            }
            return 0;
        }

        private static string ImportHeadline(string status, bool running,
            bool initializationAttempted)
        {
            if (IsImportFailure(status)) return "数据包导入失败";
            if (initializationAttempted &&
                (ContainsImportText(status, "failed") || ContainsImportText(status, "fault")))
                return "游戏启动失败";
            if (initializationAttempted) return "正在启动游戏";
            if (ContainsImportText(status, "解压")) return "正在解压并校验";
            if (ContainsImportText(status, "SHA-256") || ContainsImportText(status, "验证"))
                return "正在验证数据包";
            if (ContainsImportText(status, "读取文件清单")) return "正在读取文件清单";
            if (ContainsImportText(status, "导入成功")) return "数据包导入完成";
            if (running || ContainsImportText(status, "已选择")) return "正在准备校验";
            return "请选择对应章节的数据包";
        }

        private static string ImportSecurityStatus(int stage)
        {
            if (stage >= 3) return "数据包校验完成，正在准备游戏资源";
            if (stage >= 2) return "整包 SHA-256 已通过，正在逐文件校验";
            if (stage == 1) return "正在进行整包 SHA-256 校验";
            return "将验证整包 SHA-256 与每个文件的完整性";
        }

        private static bool IsImportFailure(string status)
        {
            return ContainsImportText(status, "导入失败") ||
                   ContainsImportText(status, "未找到所选数据包");
        }

        private static bool ContainsImportText(string value, string expected)
        {
            return !string.IsNullOrEmpty(value) &&
                   value.IndexOf(expected, StringComparison.OrdinalIgnoreCase) >= 0;
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
            if (_host.TitleVisible || _host.TipsChapterVisible || _host.TipsListVisible ||
                _host.FragmentChapterVisible || _host.FragmentListVisible ||
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
            if (!_host.GameplayUiVisible || (!_host.SavingEnabled && !_host.TipReading) ||
                !_host.InterfaceEnabled)
            {
                return false;
            }
            var safe = GetGuiSafeArea();
            var scale = UiScale;
            var railWidth = 98f * scale;
            var buttonHeight = 44f * scale;
            var x = safe.xMax - railWidth - 12f * scale;
            var y = safe.y + safe.height * 0.52f;
            for (var i = 0; i < 4; i++)
            {
                if (new Rect(x, y + i * (buttonHeight + 7f * scale),
                        railWidth, buttonHeight).Contains(guiPoint))
                {
                    return true;
                }
            }

            if (_host.SavingEnabled)
            {
                var quickWidth = 150f * scale;
                var quickY = safe.yMax - 43f * scale;
                if (new Rect(safe.xMax - quickWidth * 2f - 22f * scale,
                        quickY, quickWidth, 35f * scale).Contains(guiPoint) ||
                    new Rect(safe.xMax - quickWidth - 12f * scale,
                        quickY, quickWidth, 35f * scale).Contains(guiPoint))
                {
                    return true;
                }
            }
            return false;
        }

        private void CloseAllModals()
        {
            _settingsVisible = false;
            _helpVisible = false;
            _systemMenuVisible = false;
            _saveLoadVisible = false;
            _extrasVisible = false;
            _chapterJumpVisible = false;
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

        private bool FittedPcButton(Rect rect, string text, int minimumFontSize,
            string wrappedText = null)
        {
            var content = new GUIContent(text ?? string.Empty);
            var style = _pcSmallButtonStyle;
            var measured = style.CalcSize(content);
            if (measured.x <= rect.width - 6f && measured.y <= rect.height - 4f)
            {
                return GUI.Button(rect, content, style);
            }

            var fitted = new GUIStyle(style);
            while (fitted.fontSize > minimumFontSize)
            {
                fitted.fontSize--;
                measured = fitted.CalcSize(content);
                if (measured.x <= rect.width - 6f && measured.y <= rect.height - 4f)
                {
                    break;
                }
            }

            if ((measured.x > rect.width - 6f || measured.y > rect.height - 4f) &&
                !string.IsNullOrEmpty(wrappedText))
            {
                content = new GUIContent(wrappedText);
                fitted = new GUIStyle(style)
                {
                    wordWrap = true,
                    padding = new RectOffset(style.padding.left, style.padding.right, 2, 2)
                };
                measured = fitted.CalcSize(content);
                while (fitted.fontSize > minimumFontSize &&
                       (measured.x > rect.width - 6f || measured.y > rect.height - 4f))
                {
                    fitted.fontSize--;
                    measured = fitted.CalcSize(content);
                }
            }
            return GUI.Button(rect, content, fitted);
        }

        private static GUIStyle FitWrappedLabelStyle(GUIStyle source, string text, Rect rect,
            int minimumFontSize)
        {
            var fitted = new GUIStyle(source)
            {
                alignment = TextAnchor.UpperCenter,
                wordWrap = true,
                clipping = TextClipping.Clip
            };
            var content = new GUIContent(text ?? string.Empty);
            while (fitted.fontSize > minimumFontSize &&
                   fitted.CalcHeight(content, rect.width) > rect.height)
            {
                fitted.fontSize--;
            }
            return fitted;
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
            _speakerStyle.richText = true;
            _dialogueStyle = MakeStyle(FontPixels(0.032f, 29, 54) * textScale,
                TextAnchor.UpperLeft, FontStyle.Normal, Color.white);
            _dialogueStyle.wordWrap = true;
            _dialogueStyle.richText = false;
            _historyStyle = new GUIStyle(_dialogueStyle)
            {
                richText = true
            };
            _statusStyle = MakeStyle(FontPixels(0.021f, 19, 35) * textScale,
                TextAnchor.UpperLeft, FontStyle.Normal, new Color(0.92f, 0.92f, 0.92f));
            _statusStyle.wordWrap = true;
            _saveSummaryStyle = MakeStyle(FontPixels(0.015f, 13, 24) * textScale,
                TextAnchor.UpperLeft, FontStyle.Normal, new Color(0.92f, 0.92f, 0.92f));
            _saveSummaryStyle.wordWrap = true;
            _tipCardStyle = MakeStyle(FontPixels(0.015f, 13, 23) * textScale,
                TextAnchor.MiddleCenter, FontStyle.Bold, Color.white);
            _tipCardStyle.wordWrap = true;
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
            _importTitleStyle = MakeStyle(FontPixels(0.034f, 27, 54),
                TextAnchor.MiddleLeft, FontStyle.Bold, Color.white);
            _importStepStyle = MakeStyle(FontPixels(0.017f, 13, 25),
                TextAnchor.MiddleCenter, FontStyle.Bold, Color.white);
            _importStatusStyle = MakeStyle(FontPixels(0.030f, 23, 46),
                TextAnchor.MiddleLeft, FontStyle.Bold, Color.white);
            _importPercentStyle = MakeStyle(FontPixels(0.038f, 28, 58),
                TextAnchor.MiddleRight, FontStyle.Normal, Color.white);
            _importDetailStyle = MakeStyle(FontPixels(0.016f, 13, 25),
                TextAnchor.MiddleLeft, FontStyle.Normal, new Color(0.68f, 0.68f, 0.72f));
            _importDetailStyle.clipping = TextClipping.Clip;
            _importDetailRightStyle = new GUIStyle(_importDetailStyle)
            {
                alignment = TextAnchor.MiddleRight
            };

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
