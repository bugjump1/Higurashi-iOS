using Higurashi.IOS.Input;
using Higurashi.IOS.Playback;
using Higurashi.IOS.Runtime.Data;
using Higurashi.IOS.Runtime.Input;
using UnityEngine;

namespace Higurashi.IOS.Runtime
{
    public sealed class HigurashiBootstrap : MonoBehaviour, INovelTraversalDriver
    {
        private readonly FastTraversalController _fastTraversal = new FastTraversalController(10f);
        private readonly DataPackImportService _dataPack = new DataPackImportService();
        private TouchInputBehaviour _touchInput;
        private string _lastAction = "None";
        private int _prototypeCheckpoint;
        private GUIStyle _titleStyle;
        private GUIStyle _bodyStyle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CreateRuntime()
        {
            if (FindFirstObjectByType<HigurashiGameRuntime>() != null)
            {
                return;
            }

            var root = new GameObject("Higurashi iOS Runtime");
            DontDestroyOnLoad(root);
            root.AddComponent<HigurashiGameRuntime>();
        }

        private void Awake()
        {
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            _touchInput = gameObject.AddComponent<TouchInputBehaviour>();
            _touchInput.ActionRaised += HandleInput;
            _fastTraversal.ModeChanged += mode => _lastAction = "Traversal: " + mode;
        }

        private void OnDestroy()
        {
            if (_touchInput != null)
            {
                _touchInput.ActionRaised -= HandleInput;
            }
        }

        private void Update()
        {
            _touchInput.FastTraversalActive = _fastTraversal.IsActive;
            _fastTraversal.Tick(Time.unscaledDeltaTime, this);
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                _fastTraversal.Stop();
            }
        }

        private void HandleInput(NovelInputAction action)
        {
            _lastAction = action.ToString();
            switch (action)
            {
                case NovelInputAction.StartFastForward:
                    _fastTraversal.StartForward();
                    break;
                case NovelInputAction.StartFastRewind:
                    _fastTraversal.StartRewind();
                    break;
                case NovelInputAction.StopFastTraversal:
                    _fastTraversal.Stop();
                    break;
                case NovelInputAction.Advance:
                    StepForward();
                    break;
            }
        }

        public bool StepForward()
        {
            _prototypeCheckpoint++;
            return true;
        }

        public bool StepBackward()
        {
            if (_prototypeCheckpoint <= 0)
            {
                return false;
            }

            _prototypeCheckpoint--;
            return true;
        }

        private void OnGUI()
        {
            EnsureStyles();

            var safe = Screen.safeArea;
            var width = Mathf.Min(820f, safe.width - 48f);
            var left = safe.x + (safe.width - width) * 0.5f;
            // Screen.safeArea is bottom-left based; IMGUI is top-left based.
            var top = Screen.height - safe.yMax + 28f;

            var profile = HigurashiActiveChapter.Profile;
            GUI.Label(new Rect(left, top, width, 52f),
                "Higurashi " + profile.EpisodeCode + " iOS", _titleStyle);
            top += 66f;
            GUI.Label(
                new Rect(left, top, width, 120f),
                "Prototype runtime\n" +
                "Last input: " + _lastAction + "\n" +
                "Traversal checkpoint: " + _prototypeCheckpoint + "\n" +
                "Data: " + (DataPackImportService.IsInstalled(Application.persistentDataPath)
                    ? "Ready"
                    : _dataPack.Status),
                _bodyStyle);
            top += 132f;

            if (_dataPack.IsRunning)
            {
                GUI.HorizontalSlider(new Rect(left, top, width, 30f), _dataPack.Progress, 0f, 1f);
            }
            else if (!DataPackImportService.IsInstalled(Application.persistentDataPath))
            {
                if (GUI.Button(new Rect(left, top, width, 56f),
                        "Import " + profile.DataPackFileName))
                {
                    _dataPack.BeginImport(Application.persistentDataPath);
                }

                top += 68f;
                GUI.Label(
                    new Rect(left, top, width, 80f),
                    "Place the data pack in this app's Files folder, then tap Import.",
                    _bodyStyle);
            }
        }

        private void EnsureStyles()
        {
            if (_titleStyle != null)
            {
                return;
            }

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 32,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };
            _bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                alignment = TextAnchor.UpperLeft,
                wordWrap = true
            };
        }
    }
}
