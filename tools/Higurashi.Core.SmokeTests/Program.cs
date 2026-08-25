using Higurashi.IOS.Data;
using Higurashi.IOS.Input;
using Higurashi.IOS.Playback;
using Higurashi.IOS.Buriko;
using Higurashi.IOS.Compatibility;
using Higurashi.IOS.Persistence;
using System.Text;

internal static class Program
{
    private static int Main()
    {
        var tests = new Action[]
        {
            SingleTapAdvances,
            SingleFingerLeftSwipeAdvances,
            SingleFingerRightSwipeReturnsToPreviousTextBox,
            SingleFingerSwipeRecoversAfterMissingRelease,
            SwipeUpOpensHistory,
            ThreeFingerLeftStartsFastForward,
            ThreeFingerRightStartsFastRewind,
            NewTouchStopsFastTraversal,
            TimelineClearsFutureBranch,
            TimelineHonorsCapacity,
            TimelineCopiesOnlyThroughCurrent,
            TimelineCanPreserveChapterFloor,
            TimelineCanDiscardFutureForScriptReplay,
            AutoAdvanceDelayStartsAfterReveal,
            AutoAdvanceDelayResetsForNewDialogue,
            AutoAdvanceWaitsForVoiceCompletion,
            MessageSpeedOverrideMatchesPcIntegerSemantics,
            LayerFilterPolicyMatchesPcMatrices,
            SceneLayerBatchPreservesOnlyPreparedLayers,
            SavePolicyRejectsContentBrowsers,
            SavePolicyRejectsRuntimeControlScripts,
            SavePolicyKeepsExplicitResumePoints,
            FastTraversalRendersOneStepPerTick,
            SafePathRejectsTraversal,
            AssetCascadeFallsBackInOrder,
            CompiledScriptHeaderIsParsed,
            ChapterProfilesHaveWholeZipFingerprints,
            AllEpisodeChapterJumpMapsMatchOriginalFlows,
            EpisodeEightChapterProgressMapsToOriginalFlow,
            EpisodeEightFragmentContinuationRecoversOnlyUnexpectedFinalExit,
            EpisodeEightLegacyFragmentSaveRestoresMissingGlobals,
            OpeningChoiceLocalizationRecognizesEpisodeEight,
            ConsoleChoiceMenuLocalizationAndClassification,
            BadEndingChoicesMatchOriginalFlows,
            StoryChoiceLocalizationCoversAllStoryBranches,
            StoryChoiceResultMirrorsOriginalEngineFlags,
            MobileOptionNamesAreLocalized,
            VisualStylePresetsStayConsistent,
            BurikoTextContinuationFollowsPreviousMode,
            Episode02OperationCatalogNormalizesShiftedModCodes,
            Episode03OperationCatalogNormalizesShiftedModCodes,
            Episode04OperationCatalogNormalizesShiftedCodes,
            Episode05OperationCatalogNormalizesShiftedCodes,
            Episode06OperationCatalogNormalizesShiftedCodes,
            Episode07OperationCatalogNormalizesShiftedCodes,
            Episode08OperationCatalogNormalizesFragmentCodes,
            NegativeFilmOperationsRemainAvailableForAllEpisodes,
            BurikoRuntimeExecutesDialogueAndFlags,
            BurikoRuntimeCommitsPresentationAtWaitBoundaries,
            BurikoRuntimeCallsAndReturnsFromScript,
            BurikoRuntimeCallsFragmentScriptFromUi,
            BurikoRuntimeSnapshotRestoresExecutionAndMemory,
            BurikoRuntimePersistentStateRoundTrips,
            FragmentProgressPersistentStateRoundTrips,
            BurikoPersistentStateMetadataReadsChapter,
            BurikoRuntimeHandlesModCrossScriptSectionCall,
            BurikoRuntimeHandlesEpisode04ReturnOperation,
            BurikoMemoryInitializesTextColorToWhite
        };

        foreach (var test in tests)
        {
            test();
            Console.WriteLine("PASS " + test.Method.Name);
        }

        Console.WriteLine($"{tests.Length} smoke tests passed.");
        return 0;
    }

    private static void SingleTapAdvances()
    {
        var input = new TouchGestureInterpreter();
        Equal(NovelInputAction.None, Frame(input, 0, false, P(1, 500, 300, PointerPhase.Began)));
        Equal(NovelInputAction.Advance, Frame(input, 0.1, false, P(1, 502, 301, PointerPhase.Ended)));
    }

    private static void MessageSpeedOverrideMatchesPcIntegerSemantics()
    {
        Equal(-1, MessageSpeedPolicy.ScriptOverride(false, 128));
        Equal(0, MessageSpeedPolicy.ScriptOverride(true, 16));
        Equal(50, MessageSpeedPolicy.ScriptOverride(true, 128));
        Equal(100, MessageSpeedPolicy.ScriptOverride(true, 255));
        Equal(54f, MessageSpeedPolicy.CharactersPerSecond(50, -1));
        Equal(18f, MessageSpeedPolicy.CharactersPerSecond(50, 0));
        Equal(54f, MessageSpeedPolicy.CharactersPerSecond(0, 50));
    }

    private static void LayerFilterPolicyMatchesPcMatrices()
    {
        True(LayerFilterPolicy.TryResolve("night", out var night));
        Equal(222, night.Rr);
        Equal(222, night.Gg);
        Equal(256, night.Bb);
        True(LayerFilterPolicy.TryResolve("grayscale", out var grayscale));
        Equal(55, grayscale.Rr);
        Equal(185, grayscale.Rg);
        Equal(18, grayscale.Rb);
        True(LayerFilterPolicy.TryResolve("128,64,32", out var diagonal));
        Equal(128, diagonal.Rr);
        Equal(64, diagonal.Gg);
        Equal(32, diagonal.Bb);
        True(!LayerFilterPolicy.TryResolve("unsupported", out _));
    }

    private static void NegativeFilmOperationsRemainAvailableForAllEpisodes()
    {
        for (var episode = 1; episode <= 8; episode++)
        {
            BurikoOperationCatalog.ConfigureForEpisode(episode);
            BurikoOperationSpecification negative = default;
            BurikoOperationSpecification fadeFilm = default;
            var foundNegative = false;
            var foundFadeFilm = false;
            for (short rawCode = 0; rawCode < 256; rawCode++)
            {
                try
                {
                    var specification = BurikoOperationCatalog.Get(rawCode);
                    if (specification.Name == "Negative")
                    {
                        negative = specification;
                        foundNegative = true;
                    }
                    else if (specification.Name == "FadeFilm")
                    {
                        fadeFilm = specification;
                        foundFadeFilm = true;
                    }
                }
                catch (ArgumentOutOfRangeException)
                {
                    // Sparse operation tables contain unused raw codes.
                }
            }
            True(foundNegative);
            Equal("Negative", negative.Name);
            Equal("ib", negative.Signature);
            True(foundFadeFilm);
            Equal("FadeFilm", fadeFilm.Name);
            Equal("ib", fadeFilm.Signature);
        }
        BurikoOperationCatalog.ConfigureForEpisode(1);
    }
    private static void Episode02OperationCatalogNormalizesShiftedModCodes()
    {
        BurikoOperationCatalog.ConfigureForEpisode(2);
        var loading = BurikoOperationCatalog.Get(122);
        Equal((short)148, loading.Code);
        Equal("SetValidityOfLoading", loading.Name);
        Equal("b", loading.Signature);

        var voice = BurikoOperationCatalog.Get(132);
        Equal((short)130, voice.Code);
        Equal("ModPlayVoiceLS", voice.Name);
        Equal("iisib", voice.Signature);

        BurikoOperationCatalog.ConfigureForEpisode(1);
        Equal("ModPlayVoiceLS", BurikoOperationCatalog.Get(130).Name);
    }

    private static void Episode03OperationCatalogNormalizesShiftedModCodes()
    {
        BurikoOperationCatalog.ConfigureForEpisode(3);
        var loading = BurikoOperationCatalog.Get(122);
        Equal((short)148, loading.Code);
        Equal("SetValidityOfLoading", loading.Name);

        var voice = BurikoOperationCatalog.Get(132);
        Equal((short)130, voice.Code);
        Equal("ModPlayVoiceLS", voice.Name);
        Equal("iisib", voice.Signature);

        BurikoOperationCatalog.ConfigureForEpisode(1);
    }

    private static void Episode04OperationCatalogNormalizesShiftedCodes()
    {
        BurikoOperationCatalog.ConfigureForEpisode(4);
        Equal((short)150, BurikoOperationCatalog.Get(10).Code);
        Equal("Return", BurikoOperationCatalog.Get(10).Name);
        Equal((short)10, BurikoOperationCatalog.Get(11).Code);
        Equal("Wait", BurikoOperationCatalog.Get(11).Name);
        Equal((short)16, BurikoOperationCatalog.Get(17).Code);
        Equal("OutputLine", BurikoOperationCatalog.Get(17).Name);
        Equal((short)148, BurikoOperationCatalog.Get(123).Code);
        Equal("SetValidityOfLoading", BurikoOperationCatalog.Get(123).Name);
        Equal((short)130, BurikoOperationCatalog.Get(133).Code);
        Equal("ModPlayVoiceLS", BurikoOperationCatalog.Get(133).Name);
        Equal((short)147, BurikoOperationCatalog.Get(150).Code);
        Equal("ModGenericCall", BurikoOperationCatalog.Get(150).Name);
        BurikoOperationCatalog.ConfigureForEpisode(1);
    }

    private static void Episode05OperationCatalogNormalizesShiftedCodes()
    {
        BurikoOperationCatalog.ConfigureForEpisode(5);
        Equal((short)150, BurikoOperationCatalog.Get(10).Code);
        Equal("Return", BurikoOperationCatalog.Get(10).Name);
        Equal((short)10, BurikoOperationCatalog.Get(11).Code);
        Equal("Wait", BurikoOperationCatalog.Get(11).Name);
        Equal((short)151, BurikoOperationCatalog.Get(23).Code);
        Equal("DisplayWindow", BurikoOperationCatalog.Get(23).Name);
        Equal((short)55, BurikoOperationCatalog.Get(57).Code);
        Equal("DrawBustshot", BurikoOperationCatalog.Get(57).Name);
        Equal((short)152, BurikoOperationCatalog.Get(60).Code);
        Equal("ChangeBustshot", BurikoOperationCatalog.Get(60).Name);
        Equal((short)148, BurikoOperationCatalog.Get(125).Code);
        Equal("SetValidityOfLoading", BurikoOperationCatalog.Get(125).Name);
        Equal((short)130, BurikoOperationCatalog.Get(135).Code);
        Equal("ModPlayVoiceLS", BurikoOperationCatalog.Get(135).Name);
        Equal((short)147, BurikoOperationCatalog.Get(152).Code);
        Equal("ModGenericCall", BurikoOperationCatalog.Get(152).Name);
        BurikoOperationCatalog.ConfigureForEpisode(1);
    }

    private static void Episode06OperationCatalogNormalizesShiftedCodes()
    {
        BurikoOperationCatalog.ConfigureForEpisode(6);
        Equal((short)150, BurikoOperationCatalog.Get(10).Code);
        Equal("Return", BurikoOperationCatalog.Get(10).Name);
        Equal((short)151, BurikoOperationCatalog.Get(23).Code);
        Equal("DisplayWindow", BurikoOperationCatalog.Get(23).Name);
        Equal((short)153, BurikoOperationCatalog.Get(24).Code);
        Equal("HideWindow", BurikoOperationCatalog.Get(24).Name);
        Equal((short)154, BurikoOperationCatalog.Get(26).Code);
        Equal("SetColorOfMessage", BurikoOperationCatalog.Get(26).Name);
        Equal((short)155, BurikoOperationCatalog.Get(54).Code);
        Equal("RotateBG", BurikoOperationCatalog.Get(54).Name);
        Equal((short)152, BurikoOperationCatalog.Get(63).Code);
        Equal("ChangeBustshot", BurikoOperationCatalog.Get(63).Name);
        Equal((short)156, BurikoOperationCatalog.Get(97).Code);
        Equal("GetRandomNumber", BurikoOperationCatalog.Get(97).Name);
        Equal((short)148, BurikoOperationCatalog.Get(129).Code);
        Equal("SetValidityOfLoading", BurikoOperationCatalog.Get(129).Name);
        Equal((short)130, BurikoOperationCatalog.Get(139).Code);
        Equal("ModPlayVoiceLS", BurikoOperationCatalog.Get(139).Name);
        Equal((short)147, BurikoOperationCatalog.Get(156).Code);
        Equal("ModGenericCall", BurikoOperationCatalog.Get(156).Name);
        BurikoOperationCatalog.ConfigureForEpisode(1);
    }

    private static void Episode07OperationCatalogNormalizesShiftedCodes()
    {
        BurikoOperationCatalog.ConfigureForEpisode(7);
        Equal((short)157, BurikoOperationCatalog.Get(131).Code);
        Equal("DrawFragment", BurikoOperationCatalog.Get(131).Name);
        Equal((short)158, BurikoOperationCatalog.Get(132).Code);
        Equal("StopFragment", BurikoOperationCatalog.Get(132).Name);
        Equal((short)159, BurikoOperationCatalog.Get(133).Code);
        Equal("DrawSpriteFixedSize", BurikoOperationCatalog.Get(133).Name);
        Equal((short)160, BurikoOperationCatalog.Get(134).Code);
        Equal("DrawSpriteWithFilteringFixedSize", BurikoOperationCatalog.Get(134).Name);
        Equal((short)161, BurikoOperationCatalog.Get(135).Code);
        Equal("Update", BurikoOperationCatalog.Get(135).Name);
        Equal((short)127, BurikoOperationCatalog.Get(141).Code);
        Equal("ModCallScriptSection", BurikoOperationCatalog.Get(141).Name);
        Equal((short)130, BurikoOperationCatalog.Get(144).Code);
        Equal("ModPlayVoiceLS", BurikoOperationCatalog.Get(144).Name);
        Equal((short)147, BurikoOperationCatalog.Get(161).Code);
        Equal("ModGenericCall", BurikoOperationCatalog.Get(161).Name);
        BurikoOperationCatalog.ConfigureForEpisode(1);
    }

    private static void SceneLayerBatchPreservesOnlyPreparedLayers()
    {
        var tracker = new SceneLayerBatchTracker();
        tracker.Prepare(2);
        tracker.Prepare(1);
        tracker.Prepare(2);
        Equal(2, tracker.Count);

        var prepared = tracker.ConsumeForSceneChange();
        Equal(2, prepared.Length);
        Equal(1, prepared[0]);
        Equal(2, prepared[1]);
        Equal(0, tracker.Count);

        tracker.Prepare(7);
        tracker.Prepare(8);
        tracker.Discard(7);
        var remaining = tracker.ConsumeForSceneChange();
        Equal(1, remaining.Length);
        Equal(8, remaining[0]);

        tracker.Prepare(7);
        tracker.Commit();
        Equal(0, tracker.Count);
        Equal(0, tracker.ConsumeForSceneChange().Length);
    }

    private static void SavePolicyRejectsContentBrowsers()
    {
        Equal(true, SaveStatePolicy.CanWriteRegularSave(SaveSurface.Story, true, true));
        Equal(false, SaveStatePolicy.CanWriteRegularSave(SaveSurface.TipsList, true, true));
        Equal(false, SaveStatePolicy.CanWriteRegularSave(SaveSurface.TipReading, true, true));
        Equal(true, SaveStatePolicy.CanWriteRegularSave(SaveSurface.FragmentChapter, true, true));
        Equal(true, SaveStatePolicy.CanWriteRegularSave(SaveSurface.FragmentList, true, true));
        Equal(true, SaveStatePolicy.CanWriteRegularSave(SaveSurface.FragmentReading, true, true));
        Equal(true, SaveStatePolicy.CanWriteRegularSave(SaveSurface.BonusContent, true, true));
        Equal(false, SaveStatePolicy.CanWriteRegularSave(SaveSurface.Story, false, true));
        Equal(true, SaveStatePolicy.IsKnownLegacyTipsBrowserSave(
            "flow", "OP 动画中包含剧透，是否要启用？"));
        Equal(true, SaveStatePolicy.IsKnownLegacyTipsBrowserSave(
            "flow", "开场动画包含剧透，要播放吗？"));
        Equal(false, SaveStatePolicy.IsKnownLegacyTipsBrowserSave(
            "onik_002", "OP 动画中包含剧透，是否要启用？"));
        Equal(false, SaveStatePolicy.IsKnownLegacyTipsBrowserSave(
            "flow", "第 1 章完成（TIPS 已解锁）"));
    }

    private static void SavePolicyKeepsExplicitResumePoints()
    {
        Equal(true, SaveStatePolicy.IsRecoverableStorySave(SaveSurface.Story, true, true));
        Equal(true, SaveStatePolicy.IsRecoverableStorySave(SaveSurface.Choice, true, true));
        Equal(true, SaveStatePolicy.IsRecoverableStorySave(SaveSurface.TipsChapter, true, true));
        Equal(true, SaveStatePolicy.IsRecoverableStorySave(SaveSurface.FragmentList, true, true));
        Equal(true, SaveStatePolicy.IsRecoverableStorySave(SaveSurface.FragmentReading, true, true));
        Equal(true, SaveStatePolicy.IsRecoverableStorySave(SaveSurface.BonusContent, true, true));
        Equal(false, SaveStatePolicy.IsRecoverableStorySave(SaveSurface.Story, false, true));
        Equal(false, SaveStatePolicy.IsRecoverableStorySave(SaveSurface.TipsList, true, true));
        Equal(false, SaveStatePolicy.IsRecoverableStorySave(SaveSurface.Title, true, true));
    }

    private static void SavePolicyRejectsRuntimeControlScripts()
    {
        Equal(true, SaveStatePolicy.IsRuntimeControlScript("flow"));
        Equal(true, SaveStatePolicy.IsRuntimeControlScript("FLOW"));
        Equal(true, SaveStatePolicy.IsRuntimeControlScript("init"));
        Equal(true, SaveStatePolicy.IsRuntimeControlScript("&opening"));
        Equal(false, SaveStatePolicy.IsRuntimeControlScript("onik_002"));
        Equal(true, SaveStatePolicy.IsKnownInvalidControlFlowSave("flow", string.Empty));
        Equal(true, SaveStatePolicy.IsKnownInvalidControlFlowSave("init", "   "));
        Equal(false, SaveStatePolicy.IsKnownInvalidControlFlowSave(
            "flow", "第 1 章完成（TIPS 已解锁）"));
        Equal(false, SaveStatePolicy.IsKnownInvalidControlFlowSave("onik_002", string.Empty));
        Equal(false, SaveStatePolicy.HasStableResumeSummary(SaveSurface.Story, string.Empty));
        Equal(false, SaveStatePolicy.HasStableResumeSummary(SaveSurface.Story, "   "));
        Equal(true, SaveStatePolicy.HasStableResumeSummary(SaveSurface.Story, "剧情台词"));
        Equal(true, SaveStatePolicy.HasStableResumeSummary(SaveSurface.Choice, string.Empty));
        Equal(true, SaveStatePolicy.HasStableResumeSummary(SaveSurface.TipsChapter, string.Empty));
    }

    private static void EpisodeEightChapterProgressMapsToOriginalFlow()
    {
        var jumpValues = new[] { 0, 3, 5, 7, 8, 11, 13, 15, 17, 19 };
        var completionValues = new[] { 2, 4, 6, 7, 10, 12, 14, 16, 18, 25 };
        Equal(jumpValues.Length, EpisodeEightChapterMap.Count);
        for (var i = 0; i < jumpValues.Length; i++)
        {
            Equal(true, EpisodeEightChapterMap.TryGetJumpValue(
                EpisodeEightChapterMap.Token(i), out var jump));
            Equal(jumpValues[i], jump);
            Equal(i, EpisodeEightChapterMap.CompletedChapterCount(completionValues[i] - 1));
            Equal(i + 1, EpisodeEightChapterMap.CompletedChapterCount(completionValues[i]));
        }

        // Chapter 5 starts at s_jump=8, immediately before StartFragmentLoop.
        Equal(true, EpisodeEightChapterMap.TryGetJumpValue(
            EpisodeEightChapterMap.Token(4), out var fragmentJump));
        Equal(8, fragmentJump);
        Equal(false, EpisodeEightChapterMap.TryGetJumpValue("Day1", out _));
        Equal(false, EpisodeEightChapterMap.TryGetJumpValue("EP08_CHAPTER_10", out _));
    }

    private static void AllEpisodeChapterJumpMapsMatchOriginalFlows()
    {
        var expectedCounts = new[] { 0, 12, 12, 13, 6, 14, 13, 12, 10 };
        for (var episode = 1; episode <= 8; episode++)
        {
            Equal(expectedCounts[episode], EpisodeChapterJumpMap.Count(episode));
            for (var chapter = 0; chapter < expectedCounts[episode]; chapter++)
            {
                Equal(false, string.IsNullOrWhiteSpace(
                    EpisodeChapterJumpMap.Token(episode, chapter)));
            }
        }

        Equal("Day1", EpisodeChapterJumpMap.Token(1, 0));
        Equal("Day15", EpisodeChapterJumpMap.Token(1, 11));
        Equal("Day12", EpisodeChapterJumpMap.Token(2, 11));
        Equal("Day14", EpisodeChapterJumpMap.Token(3, 12));
        Equal("Day4", EpisodeChapterJumpMap.Token(4, 5));
        Equal("Day3", EpisodeChapterJumpMap.Token(4, 3));
        Equal("Day3_4", EpisodeChapterJumpMap.Token(4, 4));
        Equal(false, ContainsToken(4, "Day2_2"));
        Equal(false, ContainsToken(4, "Day3_2"));
        Equal(false, ContainsToken(4, "Day3_3"));
        Equal(false, ContainsToken(4, "Day3_5"));

        AssertFlowJumpValues(5, new[] { 1, 2, 4, 6, 8, 10, 12, 14, 15, 17, 19, 21, 23, 25 });
        AssertFlowJumpValues(6, new[] { 1, 2, 4, 6, 8, 10, 12, 14, 16, 18, 20, 22, 24 });
        AssertFlowJumpValues(7, new[] { 1, 2, 3, 6, 9, 11, 13, 15, 17, 19, 21, 25 });
    }

    private static bool ContainsToken(int episode, string token)
    {
        for (var chapter = 0; chapter < EpisodeChapterJumpMap.Count(episode); chapter++)
        {
            if (string.Equals(EpisodeChapterJumpMap.Token(episode, chapter), token,
                StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static void AssertFlowJumpValues(int episode, int[] expected)
    {
        Equal(expected.Length, EpisodeChapterJumpMap.Count(episode));
        for (var i = 0; i < expected.Length; i++)
        {
            Equal(true, EpisodeChapterJumpMap.TryGetFlowJumpValue(
                episode, EpisodeChapterJumpMap.Token(episode, i), out var actual));
            Equal(expected[i], actual);
        }
    }

    private static void EpisodeEightFragmentContinuationRecoversOnlyUnexpectedFinalExit()
    {
        Equal(9, EpisodeEightFragmentContinuationPolicy.ResumeStoryJumpValue);
        Equal(true, EpisodeEightFragmentContinuationPolicy.ShouldContinueStoryAfterFragment50(
            8, 50, 0, 1));
        Equal(false, EpisodeEightFragmentContinuationPolicy.ShouldContinueStoryAfterFragment50(
            8, 50, 1, 1));
        Equal(false, EpisodeEightFragmentContinuationPolicy.ShouldContinueStoryAfterFragment50(
            8, 50, 0, 0));
        Equal(false, EpisodeEightFragmentContinuationPolicy.ShouldContinueStoryAfterFragment50(
            8, 49, 0, 1));
        Equal(true, EpisodeEightFragmentContinuationPolicy.ShouldRecoverFromUnexpectedExit(
            8, 50, 0, 1, true, false));
        Equal(true, EpisodeEightFragmentContinuationPolicy.ShouldRecoverFromUnexpectedExit(
            8, 50, 0, 1, false, true));
        Equal(false, EpisodeEightFragmentContinuationPolicy.ShouldRecoverFromUnexpectedExit(
            8, 50, 1, 1, true, false));
        Equal(false, EpisodeEightFragmentContinuationPolicy.ShouldRecoverFromUnexpectedExit(
            8, 50, 0, 0, true, false));
        Equal(false, EpisodeEightFragmentContinuationPolicy.ShouldRecoverFromUnexpectedExit(
            8, 51, 0, 1, true, false));
        Equal(false, EpisodeEightFragmentContinuationPolicy.ShouldRecoverFromUnexpectedExit(
            7, 50, 0, 1, true, false));
        Equal(true, EpisodeEightFragmentContinuationPolicy.HasReachedStoryContinuation(
            8, 50, 0, "_mats_009"));
        Equal(false, EpisodeEightFragmentContinuationPolicy.HasReachedStoryContinuation(
            8, 50, 0, "_mats_010"));
        Equal(false, EpisodeEightFragmentContinuationPolicy.HasReachedStoryContinuation(
            8, 50, 1, "_mats_009"));
    }

    private static void EpisodeEightLegacyFragmentSaveRestoresMissingGlobals()
    {
        var legacyFragmentMemory = new BurikoMemory();
        legacyFragmentMemory.SetLocalFlag("LFragmentLoop", 1);

        Equal(true, EpisodeEightFragmentContinuationPolicy.RestoreMissingFragmentDefaults(
            8, legacyFragmentMemory));
        Equal(1, legacyFragmentMemory.GetGlobalFlag("GADVMode"));
        Equal(0, legacyFragmentMemory.GetGlobalFlag("GLinemodeSp"));
        Equal(50, legacyFragmentMemory.GetGlobalFlag("GWindowOpacity"));
        Equal(75, legacyFragmentMemory.GetGlobalFlag("GVoiceVolume"));
        Equal(50, legacyFragmentMemory.GetGlobalFlag("GBGMVolume"));
        Equal(50, legacyFragmentMemory.GetGlobalFlag("GSEVolume"));
        Equal(0, legacyFragmentMemory.GetGlobalFlag("GLanguage"));
        Equal(3, legacyFragmentMemory.GetGlobalFlag("GMOD_SETTING_LOADER"));

        legacyFragmentMemory.SetGlobalFlag("GADVMode", 0);
        Equal(false, EpisodeEightFragmentContinuationPolicy.RestoreMissingFragmentDefaults(
            8, legacyFragmentMemory));

        var anotherEpisodeMemory = new BurikoMemory();
        anotherEpisodeMemory.SetLocalFlag("LFragmentLoop", 1);
        Equal(false, EpisodeEightFragmentContinuationPolicy.RestoreMissingFragmentDefaults(
            7, anotherEpisodeMemory));
    }

    private static void OpeningChoiceLocalizationRecognizesEpisodeEight()
    {
        var englishChoices = new[] { "Enable opening", "Disable opening" };
        True(OpeningChoicePolicy.IsOpeningPrompt("开场动画包含剧透，要播放吗？"));
        True(OpeningChoicePolicy.IsOpeningPrompt(
            "开场动画中包含了一些剧透的要素，要启用播放吗？"));
        True(OpeningChoicePolicy.IsOpeningPrompt(
            "Video opening might contain minor spoilers. Do you want to enable it anyway?"));
        True(OpeningChoicePolicy.IsOpeningPrompt(
            "オープニング動画は多少のネタバレ要素を含んでいますが、再生を有効にしますか？"));
        Equal(false, OpeningChoicePolicy.IsOpeningPrompt("The opening ceremony starts now."));
        True(OpeningChoicePolicy.IsOpeningChoice("开场动画包含剧透，要播放吗？", englishChoices));
        True(OpeningChoicePolicy.IsOpeningChoice("OP 动画中包含剧透，是否要启用？", englishChoices));
        True(OpeningChoicePolicy.IsOpeningChoice("Unrelated prompt", englishChoices));

        var japaneseChoices = new[] { "動画再生を有効化", "動画再生を無効化" };
        True(OpeningChoicePolicy.IsOpeningChoice("動画を再生しますか？", japaneseChoices));

        var laterChineseChoices = new[] { "启用播放", "禁用播放" };
        True(OpeningChoicePolicy.IsOpeningChoice(string.Empty, laterChineseChoices));

        var storyChoices = new[] { "寻找机会", "向他求饶" };
        Equal(false, OpeningChoicePolicy.IsOpeningChoice("你要怎么做？", storyChoices));
        Equal("OP 动画中包含剧透，是否要启用？", OpeningChoicePolicy.LocalizedPrompt);
        Equal("启用 OP 动画", OpeningChoicePolicy.LocalizedEnable);
        Equal("禁用 OP 动画", OpeningChoicePolicy.LocalizedDisable);
    }

    private static void MobileOptionNamesAreLocalized()
    {
        Equal("主机版", MobileOptionDisplayName.ArtSet("Console"));
        Equal("重制版", MobileOptionDisplayName.ArtSet("Remake"));
        Equal("原版", MobileOptionDisplayName.ArtSet("Original"));
        Equal("新版 BGM/SE", MobileOptionDisplayName.AudioSet("New BGM/SE"));
        Equal("GIN 版 BGM/SE", MobileOptionDisplayName.AudioSet("GIN's BGM/SE"));
        Equal("自定义", MobileOptionDisplayName.ArtSet("自定义"));
    }

    private static void VisualStylePresetsStayConsistent()
    {
        Equal(VisualStylePolicy.ConsolePreset, VisualStylePolicy.PresetFor(0, 0));
        Equal(VisualStylePolicy.RemakePreset, VisualStylePolicy.PresetFor(1, 0));
        Equal(VisualStylePolicy.OriginalPreset, VisualStylePolicy.PresetFor(2, 1));
        Equal(VisualStylePolicy.CustomPreset, VisualStylePolicy.PresetFor(0, 1));
        Equal(VisualStylePolicy.CustomPreset, VisualStylePolicy.PresetFor(2, 0));

        var settings = new HigurashiUserSettings();
        VisualStylePolicy.ApplyPreset(settings, VisualStylePolicy.OriginalPreset);
        Equal(2, settings.spriteStyleIndex);
        Equal(1, settings.backgroundStyleIndex);
        Equal(2, settings.artSetIndex);
    }

    private static void StoryChoiceLocalizationCoversAllStoryBranches()
    {
        Equal("打开红色箱子", StoryChoiceLocalization.Localize("Open the red box"));
        Equal("打开蓝色箱子", StoryChoiceLocalization.Localize("Open the blue box"));
        Equal("打开红色箱子", StoryChoiceLocalization.Localize("赤い箱を開ける"));
        Equal("打开蓝色箱子", StoryChoiceLocalization.Localize("青い箱を開ける"));
        Equal("向他求饶", StoryChoiceLocalization.Localize("命乞いをする"));
        Equal("寻找机会", StoryChoiceLocalization.Localize("隙を窺う"));
        Equal("那时，我注意到了圭一的视线",
            StoryChoiceLocalization.Localize("その時、私は圭一の視線に気がついた"));
        Equal("然后，我回头看向圭一",
            StoryChoiceLocalization.Localize("そして私は、圭一に振り返った "));
        Equal("Ａ．建议圭一要把人偶交给谁",
            StoryChoiceLocalization.Localize("Ａ．圭一に人形を誰に渡すべきか助言した。"));
        Equal("Ｂ．什么都不做，在旁边看着",
            StoryChoiceLocalization.Localize("Ｂ．私は何もせず、成り行きを見守った。"));
        Equal("未识别选项", StoryChoiceLocalization.Localize("未识别选项"));
    }

    private static void ConsoleChoiceMenuLocalizationAndClassification()
    {
        Equal("不要不要，我只想要好结局", ConsoleChoiceMenuPolicy.Localize(
            "Skip additional choices. Show only content from PC version"));
        Equal("我就是想看到坏选项", ConsoleChoiceMenuPolicy.Localize(
            "コンソール版に追加した選択を見せます"));
        Equal("可以哦，但请标记下正确选项", ConsoleChoiceMenuPolicy.Localize(
            "Prompt choices and highlight correct answers"));
        True(ConsoleChoiceMenuPolicy.IsConsoleChoicePrompt(
            "This arc includes choices that were added in the console version."));
        True(ConsoleChoiceMenuPolicy.IsConsoleChoiceMenu("无关提示", new[]
        {
            "不要不要，我只想要好结局",
            "我就是想看到坏选项",
            "可以哦，但请标记下正确选项"
        }));
        Equal(false, ConsoleChoiceMenuPolicy.IsConsoleChoiceMenu("你要怎么做？", new[]
        {
            "打开红色箱子", "打开蓝色箱子", "向他求饶"
        }));
    }

    private static void StoryChoiceResultMirrorsOriginalEngineFlags()
    {
        var memory = new BurikoMemory();

        memory.SetChoiceResult(0);
        Equal(0, memory.GetLocalFlag("SelectResult"));
        Equal(0, memory.GetLocalFlag("LOCALWORK_NO_RESULT"));

        memory.SetChoiceResult(1);
        Equal(1, memory.GetLocalFlag("SelectResult"));
        Equal(1, memory.GetLocalFlag("LOCALWORK_NO_RESULT"));
    }

    private static void BadEndingChoicesMatchOriginalFlows()
    {
        True(BadEndingChoicePolicy.IsBadEndingChoice(4, "hima_003_03", 1));
        Equal(false, BadEndingChoicePolicy.IsBadEndingChoice(4, "hima_003_03", 0));
        True(BadEndingChoicePolicy.IsBadEndingChoice(5, "_meak_024", 0));
        Equal(false, BadEndingChoicePolicy.IsBadEndingChoice(5, "_meak_024", 1));
        True(BadEndingChoicePolicy.IsBadEndingChoice(6, "_tsum_024_1", 1));
        True(BadEndingChoicePolicy.IsBadEndingChoice(6, "_tsum_026", 0));
        Equal(false, BadEndingChoicePolicy.IsBadEndingChoice(7, "_mina_002_1", 0));
    }

    private static void TimelineCopiesOnlyThroughCurrent()
    {
        var timeline = new CheckpointTimeline<int>(10);
        timeline.Push(1);
        timeline.Push(2);
        timeline.Push(3);
        Equal(true, timeline.TryMovePrevious(out var previous));
        Equal(2, previous);
        var copied = timeline.CopyThroughCurrent();
        Equal(2, copied.Length);
        Equal(1, copied[0]);
        Equal(2, copied[1]);
    }

    private static void TimelineCanPreserveChapterFloor()
    {
        var timeline = new CheckpointTimeline<int>(3, preserveFirst: true);
        timeline.Push(10);
        timeline.Push(20);
        timeline.Push(30);
        timeline.Push(40);
        var copied = timeline.CopyThroughCurrent();
        Equal(3, copied.Length);
        Equal(10, copied[0]);
        Equal(30, copied[1]);
        Equal(40, copied[2]);
    }

    private static void TimelineCanDiscardFutureForScriptReplay()
    {
        var timeline = new CheckpointTimeline<int>(10);
        timeline.Push(1);
        timeline.Push(2);
        timeline.Push(3);
        True(timeline.TryMovePrevious(out var previous) && previous == 2);
        timeline.DiscardFuture();
        Equal(2, timeline.Count);
        True(!timeline.CanMoveNext);
        True(timeline.TryGetCurrent(out var current) && current == 2);
    }

    private static void AutoAdvanceDelayStartsAfterReveal()
    {
        var scheduler = new AutoAdvanceScheduler();
        Equal(false, scheduler.ShouldAdvance(1, false, false, 0, 2, 0.7));
        Equal(false, scheduler.ShouldAdvance(1, false, false, 20, 2, 0.7));
        Equal(false, scheduler.ShouldAdvance(1, true, false, 20, 2, 0.7));
        Equal(false, scheduler.ShouldAdvance(1, true, false, 21.99, 2, 0.7));
        Equal(true, scheduler.ShouldAdvance(1, true, false, 22, 2, 0.7));
    }

    private static void AutoAdvanceDelayResetsForNewDialogue()
    {
        var scheduler = new AutoAdvanceScheduler();
        Equal(false, scheduler.ShouldAdvance(1, true, false, 0, 2, 0.7));
        Equal(false, scheduler.ShouldAdvance(2, true, false, 10, 2, 0.7));
        Equal(false, scheduler.ShouldAdvance(2, true, false, 11.99, 2, 0.7));
        Equal(true, scheduler.ShouldAdvance(2, true, false, 12, 2, 0.7));
    }

    private static void AutoAdvanceWaitsForVoiceCompletion()
    {
        var scheduler = new AutoAdvanceScheduler();
        Equal(false, scheduler.ShouldAdvance(1, true, true, 0, 2, 0.7));
        Equal(false, scheduler.ShouldAdvance(1, true, true, 3, 2, 0.7));
        Equal(false, scheduler.ShouldAdvance(1, true, false, 3, 2, 0.7));
        Equal(false, scheduler.ShouldAdvance(1, true, false, 3.69, 2, 0.7));
        Equal(true, scheduler.ShouldAdvance(1, true, false, 3.7, 2, 0.7));
    }

    private static void SingleFingerLeftSwipeAdvances()
    {
        var input = new TouchGestureInterpreter();
        Frame(input, 0, false, P(1, 800, 300, PointerPhase.Began));
        Equal(NovelInputAction.Advance,
            Frame(input, 0.2, false, P(1, 500, 310, PointerPhase.Ended)));
    }

    private static void SingleFingerRightSwipeReturnsToPreviousTextBox()
    {
        var input = new TouchGestureInterpreter();
        Frame(input, 0, false, P(1, 200, 300, PointerPhase.Began));
        Equal(NovelInputAction.PreviousTextBox,
            Frame(input, 0.2, false, P(1, 500, 290, PointerPhase.Ended)));
    }

    private static void SingleFingerSwipeRecoversAfterMissingRelease()
    {
        var input = new TouchGestureInterpreter();
        Equal(NovelInputAction.None,
            Frame(input, 0, false, P(11, 150, 300, PointerPhase.Began)));
        // Simulate iOS dropping Ended/Canceled while a system gesture takes over.
        Equal(NovelInputAction.None, Frame(input, 0.1, false));
        Equal(NovelInputAction.None,
            Frame(input, 0.2, false, P(12, 180, 300, PointerPhase.Began)));
        Equal(NovelInputAction.PreviousTextBox,
            Frame(input, 0.35, false, P(12, 520, 295, PointerPhase.Ended)));
    }

    private static void Episode08OperationCatalogNormalizesFragmentCodes()
    {
        BurikoOperationCatalog.ConfigureForEpisode(8);
        Equal((short)162, BurikoOperationCatalog.Get(136).Code);
        Equal("ShiftSection", BurikoOperationCatalog.Get(136).Name);
        Equal((short)163, BurikoOperationCatalog.Get(137).Code);
        Equal("FragmentViewChapterScreen", BurikoOperationCatalog.Get(137).Name);
        Equal((short)164, BurikoOperationCatalog.Get(138).Code);
        Equal("FragmentListScreen", BurikoOperationCatalog.Get(138).Name);
        Equal((short)165, BurikoOperationCatalog.Get(139).Code);
        Equal("SetWindowBackground", BurikoOperationCatalog.Get(139).Name);
        Equal((short)166, BurikoOperationCatalog.Get(140).Code);
        Equal("JumpScriptSection", BurikoOperationCatalog.Get(140).Name);
        Equal((short)127, BurikoOperationCatalog.Get(146).Code);
        Equal("ModCallScriptSection", BurikoOperationCatalog.Get(146).Name);
        Equal((short)147, BurikoOperationCatalog.Get(166).Code);
        Equal("ModGenericCall", BurikoOperationCatalog.Get(166).Name);
        BurikoOperationCatalog.ConfigureForEpisode(1);
    }

    private static void ChapterProfilesHaveWholeZipFingerprints()
    {
        var episode01 = HigurashiChapterProfiles.ForEpisode(1);
        Equal(1919394073L, episode01.ExpectedDataPackSize);
        Equal(64, episode01.ExpectedDataPackSha256.Length);
        Equal("82EA7368576B2EC1E313505E854C784B67D44FBD36472F70A54FD6BE480CEB4F",
            episode01.ExpectedDataPackSha256);

        var episode02 = HigurashiChapterProfiles.ForEpisode(2);
        Equal(2269419044L, episode02.ExpectedDataPackSize);
        Equal(64, episode02.ExpectedDataPackSha256.Length);
        Equal("0481E9D02ED7A993BFC0CC4BEA378DC35E16621BEEBC09578057533FE0DC1CF0",
            episode02.ExpectedDataPackSha256);

        var episode03 = HigurashiChapterProfiles.ForEpisode(3);
        Equal("HigurashiEp03", episode03.ProductName);
        Equal("com.bugjump.higurashi.ep03", episode03.BundleIdentifier);
        Equal("Higurashi-03-data.zip", episode03.DataPackFileName);
        Equal("higurashi-03", episode03.GameId);
        Equal("tatarigoroshi", episode03.ChapterSlug);
        Equal(2079546842L, episode03.ExpectedDataPackSize);
        Equal("13F2957DC7D6F2A6A7A9DAE737E3C4029D30A20F4E34B200AE5499C79C3A5FEF",
            episode03.ExpectedDataPackSha256);

        var episode04 = HigurashiChapterProfiles.ForEpisode(4);
        Equal("HigurashiEp04", episode04.ProductName);
        Equal("com.bugjump.higurashi.ep04", episode04.BundleIdentifier);
        Equal("Higurashi-04-data.zip", episode04.DataPackFileName);
        Equal("higurashi-04", episode04.GameId);
        Equal("himatsubushi", episode04.ChapterSlug);
        Equal(1416754682L, episode04.ExpectedDataPackSize);
        Equal("473DA280F2F4D98BE3B961FAD4D871D369CB71CF4DA51DCF395A2D542AC557ED",
            episode04.ExpectedDataPackSha256);

        var episode05 = HigurashiChapterProfiles.ForEpisode(5);
        Equal("HigurashiEp05", episode05.ProductName);
        Equal("com.bugjump.higurashi.ep05", episode05.BundleIdentifier);
        Equal("Higurashi-05-data.zip", episode05.DataPackFileName);
        Equal("higurashi-05", episode05.GameId);
        Equal("meakashi", episode05.ChapterSlug);
        Equal(1961020275L, episode05.ExpectedDataPackSize);
        Equal("AFAAD2CCBF45C9BC6729C020DE6E86A58CB741EFD889280681181B243644A302",
            episode05.ExpectedDataPackSha256);

        var episode06 = HigurashiChapterProfiles.ForEpisode(6);
        Equal("HigurashiEp06", episode06.ProductName);
        Equal("com.bugjump.higurashi.ep06", episode06.BundleIdentifier);
        Equal("Higurashi-06-data.zip", episode06.DataPackFileName);
        Equal("higurashi-06", episode06.GameId);
        Equal("tsumihoroboshi", episode06.ChapterSlug);
        Equal(2524592182L, episode06.ExpectedDataPackSize);
        Equal("460C397D1F7B4B7FC756E3273A238DD1AC9FF2D4F89BFD417341038CD7B47869",
            episode06.ExpectedDataPackSha256);

        var episode07 = HigurashiChapterProfiles.ForEpisode(7);
        Equal("HigurashiEp07", episode07.ProductName);
        Equal("com.bugjump.higurashi.ep07", episode07.BundleIdentifier);
        Equal("Higurashi-07-data.zip", episode07.DataPackFileName);
        Equal("higurashi-07", episode07.GameId);
        Equal("minagoroshi", episode07.ChapterSlug);
        Equal(2565499174L, episode07.ExpectedDataPackSize);
        Equal("189A0538BE429C9C66CC5F3B74D20ED2E945A50C64F2C50CCF1600121D6C8318",
            episode07.ExpectedDataPackSha256);

        var episode08 = HigurashiChapterProfiles.ForEpisode(8);
        Equal("HigurashiEp08", episode08.ProductName);
        Equal("com.bugjump.higurashi.ep08", episode08.BundleIdentifier);
        Equal("Higurashi-08-data.zip", episode08.DataPackFileName);
        Equal("higurashi-08", episode08.GameId);
        Equal("matsuribayashi", episode08.ChapterSlug);
        Equal(2866026480L, episode08.ExpectedDataPackSize);
        Equal("63641448EEC692688370171DDCF7A7263E3C0534B7933B75C5B565DEB4487A35",
            episode08.ExpectedDataPackSha256);
    }

    private static void BurikoTextContinuationFollowsPreviousMode()
    {
        var appendNext = false;
        Equal(false, BeginTextLine(ref appendNext, 1)); // Continue starts a fresh line.
        Equal(true, BeginTextLine(ref appendNext, 0));  // Normal appends to that Continue.
        Equal(false, appendNext);                       // Normal clears continuation.

        Equal(false, BeginTextLine(ref appendNext, 2)); // WaitForInput starts a fresh line.
        Equal(true, BeginTextLine(ref appendNext, 0));  // After the click, retain it and append.
    }

    private static bool BeginTextLine(ref bool appendNext, int textMode)
    {
        var append = appendNext;
        appendNext = textMode != 0;
        return append;
    }

    private static void SwipeUpOpensHistory()
    {
        var input = new TouchGestureInterpreter();
        Frame(input, 0, false, P(1, 500, 100, PointerPhase.Began));
        Equal(NovelInputAction.OpenHistory, Frame(input, 0.2, false, P(1, 500, 500, PointerPhase.Ended)));
    }

    private static void ThreeFingerLeftStartsFastForward()
    {
        var input = new TouchGestureInterpreter();
        Frame(input, 0, false,
            P(1, 800, 300, PointerPhase.Began),
            P(2, 800, 400, PointerPhase.Began),
            P(3, 800, 500, PointerPhase.Began));
        Equal(NovelInputAction.StartFastForward, Frame(input, 0.05, false,
            P(1, 500, 300, PointerPhase.Moved),
            P(2, 500, 400, PointerPhase.Moved),
            P(3, 500, 500, PointerPhase.Moved)));
    }

    private static void ThreeFingerRightStartsFastRewind()
    {
        var input = new TouchGestureInterpreter();
        Frame(input, 0, false,
            P(1, 200, 300, PointerPhase.Began),
            P(2, 200, 400, PointerPhase.Began),
            P(3, 200, 500, PointerPhase.Began));
        Equal(NovelInputAction.StartFastRewind, Frame(input, 0.05, false,
            P(1, 500, 300, PointerPhase.Moved),
            P(2, 500, 400, PointerPhase.Moved),
            P(3, 500, 500, PointerPhase.Moved)));
    }

    private static void NewTouchStopsFastTraversal()
    {
        var input = new TouchGestureInterpreter();
        Frame(input, 0, false,
            P(1, 800, 300, PointerPhase.Began),
            P(2, 800, 400, PointerPhase.Began),
            P(3, 800, 500, PointerPhase.Began));
        Frame(input, 0.05, false,
            P(1, 500, 300, PointerPhase.Moved),
            P(2, 500, 400, PointerPhase.Moved),
            P(3, 500, 500, PointerPhase.Moved));
        Frame(input, 0.1, true,
            P(1, 500, 300, PointerPhase.Ended),
            P(2, 500, 400, PointerPhase.Ended),
            P(3, 500, 500, PointerPhase.Ended));

        Equal(NovelInputAction.StopFastTraversal,
            Frame(input, 0.2, true, P(4, 600, 300, PointerPhase.Began)));
    }

    private static void TimelineClearsFutureBranch()
    {
        var timeline = new CheckpointTimeline<int>(10);
        timeline.Push(1);
        timeline.Push(2);
        timeline.Push(3);
        True(timeline.TryMovePrevious(out var previous) && previous == 2);
        timeline.Push(9);
        Equal(3, timeline.Count);
        True(!timeline.CanMoveNext);
        True(timeline.TryGetCurrent(out var current) && current == 9);
    }

    private static void TimelineHonorsCapacity()
    {
        var timeline = new CheckpointTimeline<int>(3);
        timeline.Push(1);
        timeline.Push(2);
        timeline.Push(3);
        timeline.Push(4);
        Equal(3, timeline.Count);
        True(timeline.TryMovePrevious(out var previous) && previous == 3);
    }

    private static void FastTraversalRendersOneStepPerTick()
    {
        var driver = new CountingDriver();
        var traversal = new FastTraversalController(10);
        traversal.StartForward();
        True(traversal.Tick(1f, driver));
        Equal(1, driver.ForwardSteps);
    }

    private static void SafePathRejectsTraversal()
    {
        var root = Path.Combine(Path.GetTempPath(), "higurashi-safe-path-test");
        var safe = SafePath.ResolveUnderRoot(root, "StreamingAssets/CG/test.png");
        True(safe.StartsWith(Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase));

        var rejected = false;
        try
        {
            SafePath.ResolveUnderRoot(root, "../escape.txt");
        }
        catch (InvalidDataException)
        {
            rejected = true;
        }

        True(rejected);
    }

    private static void AssetCascadeFallsBackInOrder()
    {
        var root = Path.Combine(Path.GetTempPath(), "higurashi-cascade-" + Guid.NewGuid().ToString("N"));
        try
        {
            var expected = Path.Combine(root, "StreamingAssets", "CG", "sprite", "rena0.png");
            Directory.CreateDirectory(Path.GetDirectoryName(expected)!);
            File.WriteAllText(expected, "fixture");

            var resolver = new AssetCascadeResolver(root);
            True(resolver.TryResolve(
                "sprite/rena0.png",
                new[] { "CGAlt", "CG" },
                out var resolved));
            Equal(Path.GetFullPath(expected), resolved);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    private static void CompiledScriptHeaderIsParsed()
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
        {
            writer.Write(Encoding.ASCII.GetBytes("MGSC"));
            writer.Write(1);
            writer.Write(1);
            writer.Write(1);
            writer.Write(2);
            writer.Write("main");
            writer.Write(0);
            writer.Write(0);
            writer.Write((short)0);
        }

        stream.Position = 0;
        var script = CompiledScriptContainer.Read(stream);
        Equal(2, script.Data.Length);
        Equal(1, script.Blocks.Count);
        Equal(1, script.LineOffsets.Count);
    }

    private static void BurikoRuntimeExecutesDialogueAndFlags()
    {
        var data = BuildBytecode(writer =>
        {
            WriteLineNumber(writer, 0);
            WriteOperation(writer, 3, () =>
            {
                WriteReferenceValue(writer, "GTest");
                WriteIntValue(writer, 7);
            });
            WriteOperation(writer, 17, () =>
            {
                WriteNullValue(writer);
                WriteStringValue(writer, "hello");
                WriteIntValue(writer, 0);
            });
            WriteOperation(writer, 11, null);
            writer.Write((short)0);
        });

        var host = new CapturingHost();
        var runtime = new BurikoRuntime(
            new DictionaryScriptRepository(("init", WrapScript(data))),
            host);
        runtime.Start();

        Equal(BurikoBlockReason.WaitForInput, runtime.RunUntilBlocked());
        Equal(7, runtime.Memory.GetGlobalFlag("GTest"));
        Equal("hello", host.LastDialogue);

        runtime.ResumeInput();
        Equal(BurikoBlockReason.Completed, runtime.RunUntilBlocked());
    }

    private static void BurikoRuntimeCallsAndReturnsFromScript()
    {
        var init = BuildBytecode(writer =>
        {
            WriteOperation(writer, 6, () => WriteStringValue(writer, "flow"));
            writer.Write((short)0);
        });
        var flow = BuildBytecode(writer =>
        {
            WriteOperation(writer, 3, () =>
            {
                WriteReferenceValue(writer, "GFlowReached");
                WriteIntValue(writer, 1);
            });
            writer.Write((short)0);
        });

        var runtime = new BurikoRuntime(
            new DictionaryScriptRepository(
                ("init", WrapScript(init)),
                ("flow", WrapScript(flow))),
            new CapturingHost());
        runtime.Start();

        Equal(BurikoBlockReason.Completed, runtime.RunUntilBlocked());
        Equal(1, runtime.Memory.GetGlobalFlag("GFlowReached"));
    }

    private static void BurikoRuntimeCallsFragmentScriptFromUi()
    {
        var init = BuildBytecode(writer =>
        {
            WriteOperation(writer, 6, () => WriteStringValue(writer, "flow"));
            writer.Write((short)0);
        });
        var flow = BuildBytecode(writer =>
        {
            WriteOperation(writer, 11, null);
            WriteOperation(writer, 6, () => WriteStringValue(writer, "nextchapter"));
            writer.Write((short)0);
        });
        var fragment = BuildBytecode(writer =>
        {
            WriteOperation(writer, 6, () => WriteStringValue(writer, "fragment02"));
            WriteOperation(writer, 2, () =>
            {
                WriteReferenceValue(writer, "LFragmentLoop");
                WriteIntValue(writer, 0);
            });
            writer.Write((short)0);
        });
        var fragment02 = BuildBytecode(writer =>
        {
            WriteOperation(writer, 3, () =>
            {
                WriteReferenceValue(writer, "GFragmentRead");
                WriteIntValue(writer, 1);
            });
            writer.Write((short)0);
        });
        var nextChapter = BuildBytecode(writer =>
        {
            WriteOperation(writer, 3, () =>
            {
                WriteReferenceValue(writer, "GChapterSixReached");
                WriteIntValue(writer, 1);
            });
            writer.Write((short)0);
        });

        var runtime = new BurikoRuntime(
            new DictionaryScriptRepository(
                ("init", WrapScript(init)),
                ("flow", WrapScript(flow)),
                ("fragment", WrapScript(fragment)),
                ("fragment02", WrapScript(fragment02)),
                ("nextchapter", WrapScript(nextChapter))),
            new CapturingHost());
        runtime.Start();
        runtime.Memory.SetLocalFlag("LFragmentLoop", 1);
        Equal(BurikoBlockReason.WaitForInput, runtime.RunUntilBlocked());
        runtime.CallScriptFromUi("fragment");

        Equal(BurikoBlockReason.Completed, runtime.RunUntilBlocked());
        Equal(1, runtime.Memory.GetGlobalFlag("GFragmentRead"));
        Equal(0, runtime.Memory.GetLocalFlag("LFragmentLoop"));
        Equal(1, runtime.Memory.GetGlobalFlag("GChapterSixReached"));
    }

    private static void BurikoRuntimeSnapshotRestoresExecutionAndMemory()
    {
        var data = BuildBytecode(writer =>
        {
            WriteOperation(writer, 3, () =>
            {
                WriteReferenceValue(writer, "GCheckpoint");
                WriteIntValue(writer, 1);
            });
            WriteOperation(writer, 11, null);
            WriteOperation(writer, 3, () =>
            {
                WriteReferenceValue(writer, "GCheckpoint");
                WriteIntValue(writer, 2);
            });
            WriteOperation(writer, 11, null);
            writer.Write((short)0);
        });

        var runtime = new BurikoRuntime(
            new DictionaryScriptRepository(("init", WrapScript(data))),
            new CapturingHost());
        runtime.Start();
        Equal(BurikoBlockReason.WaitForInput, runtime.RunUntilBlocked());
        var snapshot = runtime.CaptureSnapshot();

        runtime.ResumeInput();
        Equal(BurikoBlockReason.WaitForInput, runtime.RunUntilBlocked());
        Equal(2, runtime.Memory.GetGlobalFlag("GCheckpoint"));

        runtime.RestoreSnapshot(snapshot);
        Equal(BurikoBlockReason.WaitForInput, runtime.BlockReason);
        Equal(1, runtime.Memory.GetGlobalFlag("GCheckpoint"));
        runtime.ResumeInput();
        Equal(BurikoBlockReason.WaitForInput, runtime.RunUntilBlocked());
        Equal(2, runtime.Memory.GetGlobalFlag("GCheckpoint"));
    }

    private static void BurikoRuntimeHandlesModCrossScriptSectionCall()
    {
        var init = BuildBytecode(writer =>
        {
            WriteOperation(writer, 127, () =>
            {
                WriteStringValue(writer, "flow");
                WriteStringValue(writer, "entry");
            });
            writer.Write((short)0);
        });
        var flowData = BuildBytecode(writer =>
        {
            WriteOperation(writer, 3, () =>
            {
                WriteReferenceValue(writer, "GModSectionReached");
                WriteIntValue(writer, 1);
            });
            writer.Write((short)0);
        });
        var flow = WrapScriptWithBlock(flowData, "entry");

        var runtime = new BurikoRuntime(
            new DictionaryScriptRepository(
                ("init", WrapScript(init)),
                ("flow", flow)),
            new CapturingHost());
        runtime.Start();

        Equal(BurikoBlockReason.Completed, runtime.RunUntilBlocked());
        Equal(1, runtime.Memory.GetGlobalFlag("GModSectionReached"));
    }

    private static void BurikoRuntimePersistentStateRoundTrips()
    {
        var data = BuildBytecode(writer =>
        {
            WriteOperation(writer, 3, () =>
            {
                WriteReferenceValue(writer, "GPersistent");
                WriteIntValue(writer, 7);
            });
            WriteOperation(writer, 11, null);
            WriteOperation(writer, 3, () =>
            {
                WriteReferenceValue(writer, "GPersistent");
                WriteIntValue(writer, 9);
            });
            writer.Write((short)0);
        });
        var runtime = new BurikoRuntime(
            new DictionaryScriptRepository(("init", WrapScript(data))),
            new CapturingHost());
        runtime.Start();
        Equal(BurikoBlockReason.WaitForInput, runtime.RunUntilBlocked());

        using var state = new MemoryStream();
        runtime.WritePersistentState(state);
        runtime.ResumeInput();
        Equal(BurikoBlockReason.Completed, runtime.RunUntilBlocked());
        Equal(9, runtime.Memory.GetGlobalFlag("GPersistent"));

        state.Position = 0;
        runtime.ReadPersistentState(state);
        Equal(BurikoBlockReason.WaitForInput, runtime.BlockReason);
        Equal(7, runtime.Memory.GetGlobalFlag("GPersistent"));
        runtime.ResumeInput();
        Equal(BurikoBlockReason.Completed, runtime.RunUntilBlocked());
        Equal(9, runtime.Memory.GetGlobalFlag("GPersistent"));
    }

    private static void BurikoPersistentStateMetadataReadsChapter()
    {
        var data = BuildBytecode(writer =>
        {
            WriteOperation(writer, 11, null);
            writer.Write((short)0);
        });
        var runtime = new BurikoRuntime(
            new DictionaryScriptRepository(("init", WrapScript(data))),
            new CapturingHost());
        runtime.Start();
        runtime.Memory.SetLocalFlag("ChapterNumber", 4);
        Equal(BurikoBlockReason.WaitForInput, runtime.RunUntilBlocked());

        using var state = new MemoryStream();
        runtime.WritePersistentState(state);
        state.Position = 0;
        Equal(true, BurikoRuntime.TryReadPersistentLocalFlag(
            state, "ChapterNumber", out var chapter));
        Equal(4, chapter);
        Equal(BurikoBlockReason.WaitForInput, runtime.BlockReason);
        Equal(4, runtime.Memory.GetLocalFlag("ChapterNumber"));
    }

    private static void FragmentProgressPersistentStateRoundTrips()
    {
        var data = BuildBytecode(writer =>
        {
            WriteOperation(writer, 11, null);
            writer.Write((short)0);
        });
        var runtime = new BurikoRuntime(
            new DictionaryScriptRepository(("init", WrapScript(data))),
            new CapturingHost());
        runtime.Start();
        runtime.Memory.SetLocalFlag("LFragmentLoop", 1);
        runtime.Memory.SetLocalFlag("LFragmentRead", 6);
        runtime.Memory.SetLocalFlag("FragmentRead09", 1);
        runtime.Memory.SetLocalFlag("FragmentStatus09", 1);
        Equal(BurikoBlockReason.WaitForInput, runtime.RunUntilBlocked());

        using var state = new MemoryStream();
        runtime.WritePersistentState(state);
        runtime.Memory.SetLocalFlag("LFragmentRead", 20);
        runtime.Memory.SetLocalFlag("FragmentRead09", 0);
        runtime.Memory.SetLocalFlag("FragmentStatus09", 2);

        state.Position = 0;
        runtime.ReadPersistentState(state);
        Equal(1, runtime.Memory.GetLocalFlag("LFragmentLoop"));
        Equal(6, runtime.Memory.GetLocalFlag("LFragmentRead"));
        Equal(1, runtime.Memory.GetLocalFlag("FragmentRead09"));
        Equal(1, runtime.Memory.GetLocalFlag("FragmentStatus09"));
    }

    private static void BurikoRuntimeHandlesEpisode04ReturnOperation()
    {
        BurikoOperationCatalog.ConfigureForEpisode(4);
        var init = BuildBytecode(writer =>
        {
            WriteOperation(writer, 6, () => WriteStringValue(writer, "flow"));
            WriteOperation(writer, 3, () =>
            {
                WriteReferenceValue(writer, "GAfterReturn");
                WriteIntValue(writer, 2);
            });
            writer.Write((short)0);
        });
        var flow = BuildBytecode(writer =>
        {
            WriteOperation(writer, 3, () =>
            {
                WriteReferenceValue(writer, "GAfterReturn");
                WriteIntValue(writer, 1);
            });
            WriteOperation(writer, 10, null);
            WriteOperation(writer, 3, () =>
            {
                WriteReferenceValue(writer, "GAfterReturn");
                WriteIntValue(writer, 99);
            });
            writer.Write((short)0);
        });

        var runtime = new BurikoRuntime(
            new DictionaryScriptRepository(
                ("init", WrapScript(init)),
                ("flow", WrapScript(flow))),
            new CapturingHost());
        runtime.Start();
        Equal(BurikoBlockReason.Completed, runtime.RunUntilBlocked());
        Equal(2, runtime.Memory.GetGlobalFlag("GAfterReturn"));
        BurikoOperationCatalog.ConfigureForEpisode(1);
    }

    private static void BurikoMemoryInitializesTextColorToWhite()
    {
        var memory = new BurikoMemory();
        Equal(0xFFFFFF, memory.GetLocalFlag("LTextColor"));
    }

    private static void BurikoRuntimeCommitsPresentationAtWaitBoundaries()
    {
        var data = BuildBytecode(writer =>
        {
            WriteOperation(writer, 10, () => WriteIntValue(writer, 1));
            WriteOperation(writer, 11, null);
            writer.Write((short)0);
        });
        var host = new CapturingHost();
        var runtime = new BurikoRuntime(
            new DictionaryScriptRepository(("init", WrapScript(data))), host);

        runtime.Start();
        Equal(BurikoBlockReason.WaitForTime, runtime.RunUntilBlocked());
        Equal(1, host.PresentationCommitCount);
        runtime.AdvanceTime(1);
        Equal(BurikoBlockReason.WaitForInput, runtime.RunUntilBlocked());
        Equal(2, host.PresentationCommitCount);
    }

    private static byte[] BuildBytecode(Action<BinaryWriter> write)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
        write(writer);
        writer.Flush();
        return stream.ToArray();
    }

    private static CompiledScriptContainer WrapScript(byte[] data)
    {
        return WrapScriptWithBlock(data, "main");
    }

    private static CompiledScriptContainer WrapScriptWithBlock(byte[] data, string blockName)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
        {
            writer.Write(Encoding.ASCII.GetBytes("MGSC"));
            writer.Write(1);
            writer.Write(1);
            writer.Write(0);
            writer.Write(data.Length);
            writer.Write(blockName);
            writer.Write(0);
            writer.Write(data);
        }

        stream.Position = 0;
        return CompiledScriptContainer.Read(stream);
    }

    private static void WriteLineNumber(BinaryWriter writer, int line)
    {
        writer.Write((short)1);
        writer.Write(line);
    }

    private static void WriteOperation(BinaryWriter writer, short code, Action writeArguments)
    {
        writer.Write((short)2);
        writer.Write(code);
        writeArguments?.Invoke();
    }

    private static void WriteNullValue(BinaryWriter writer)
    {
        writer.Write((short)1);
    }

    private static void WriteIntValue(BinaryWriter writer, int value)
    {
        writer.Write((short)2);
        writer.Write(value);
    }

    private static void WriteStringValue(BinaryWriter writer, string value)
    {
        writer.Write((short)3);
        writer.Write(value);
    }

    private static void WriteReferenceValue(BinaryWriter writer, string name, int index = -1)
    {
        writer.Write((short)5);
        writer.Write(name);
        WriteIntValue(writer, index);
        writer.Write(false);
    }

    private static NovelInputAction Frame(
        TouchGestureInterpreter interpreter,
        double time,
        bool fast,
        params PointerSample[] pointers)
    {
        return interpreter.ProcessFrame(pointers, 1000, 700, time, fast);
    }

    private static PointerSample P(int id, float x, float y, PointerPhase phase)
    {
        return new PointerSample(id, x, y, phase);
    }

    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"Expected {expected}, got {actual}.");
        }
    }

    private static void True(bool condition)
    {
        if (!condition)
        {
            throw new InvalidOperationException("Assertion failed.");
        }
    }

    private sealed class CountingDriver : INovelTraversalDriver
    {
        public int ForwardSteps { get; private set; }

        public bool StepForward()
        {
            ForwardSteps++;
            return true;
        }

        public bool StepBackward()
        {
            return false;
        }
    }

    private sealed class DictionaryScriptRepository : IBurikoScriptRepository
    {
        private readonly Dictionary<string, CompiledScriptContainer> _scripts;

        public DictionaryScriptRepository(params (string Name, CompiledScriptContainer Script)[] scripts)
        {
            _scripts = scripts.ToDictionary(item => item.Name, item => item.Script, StringComparer.OrdinalIgnoreCase);
        }

        public CompiledScriptContainer Load(string scriptName)
        {
            return _scripts.TryGetValue(scriptName, out var script)
                ? script
                : throw new FileNotFoundException(scriptName);
        }
    }

    private sealed class CapturingHost : IBurikoHost
    {
        public string LastDialogue { get; private set; }
        public int PresentationCommitCount { get; private set; }

        public BurikoHostResponse Execute(BurikoOperationInvocation invocation, BurikoMemory memory)
        {
            if (invocation.Specification.Code == 17)
            {
                LastDialogue = invocation.Arguments[1].AsString(memory);
            }

            return BurikoHostResponse.Continue;
        }

        public void CommitPendingPresentation()
        {
            PresentationCommitCount++;
        }
    }
}
