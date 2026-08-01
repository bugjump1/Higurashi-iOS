using System;
using System.Collections.Generic;
using Higurashi.IOS.Input;
using UnityEngine;

namespace Higurashi.IOS.Runtime.Input
{
    public sealed class TouchInputBehaviour : MonoBehaviour
    {
        private readonly TouchGestureInterpreter _interpreter = new TouchGestureInterpreter();
        private readonly List<PointerSample> _samples = new List<PointerSample>(5);
        private readonly HashSet<int> _uiPointerIds = new HashSet<int>();
        private bool _mouseIsDown;

        public event Action<NovelInputAction> ActionRaised;

        public bool FastTraversalActive { get; set; }
        public Func<Vector2, bool> UiHitTest { get; set; }

        private void Update()
        {
            _samples.Clear();

            if (UnityEngine.Input.touchCount > 0)
            {
                for (var i = 0; i < UnityEngine.Input.touchCount; i++)
                {
                    var touch = UnityEngine.Input.GetTouch(i);
                    var guiPoint = new Vector2(touch.position.x, Screen.height - touch.position.y);
                    if (touch.phase == UnityEngine.TouchPhase.Began &&
                        UnityEngine.Input.touchCount == 1 &&
                        UiHitTest != null && UiHitTest(guiPoint))
                    {
                        _uiPointerIds.Add(touch.fingerId);
                    }
                    if (_uiPointerIds.Contains(touch.fingerId))
                    {
                        if (touch.phase == UnityEngine.TouchPhase.Ended ||
                            touch.phase == UnityEngine.TouchPhase.Canceled)
                        {
                            _uiPointerIds.Remove(touch.fingerId);
                        }
                        continue;
                    }
                    _samples.Add(new PointerSample(
                        touch.fingerId,
                        touch.position.x,
                        touch.position.y,
                        ConvertPhase(touch.phase)));
                }
            }
#if UNITY_EDITOR || UNITY_STANDALONE
            else
            {
                AddMouseFallback();
            }
#endif

            var action = _interpreter.ProcessFrame(
                _samples,
                Screen.width,
                Screen.height,
                Time.realtimeSinceStartupAsDouble,
                FastTraversalActive);

            if (action != NovelInputAction.None)
            {
                ActionRaised?.Invoke(action);
            }
        }

#if UNITY_EDITOR || UNITY_STANDALONE
        private void AddMouseFallback()
        {
            var position = UnityEngine.Input.mousePosition;
            if (UnityEngine.Input.GetMouseButtonDown(0))
            {
                _mouseIsDown = true;
                if (UiHitTest != null && UiHitTest(new Vector2(position.x, Screen.height - position.y)))
                {
                    _uiPointerIds.Add(0);
                }
                if (_uiPointerIds.Contains(0))
                {
                    return;
                }
                _samples.Add(new PointerSample(0, position.x, position.y, PointerPhase.Began));
            }
            else if (UnityEngine.Input.GetMouseButtonUp(0))
            {
                _mouseIsDown = false;
                if (_uiPointerIds.Remove(0))
                {
                    return;
                }
                _samples.Add(new PointerSample(0, position.x, position.y, PointerPhase.Ended));
            }
            else if (_mouseIsDown)
            {
                if (_uiPointerIds.Contains(0))
                {
                    return;
                }
                _samples.Add(new PointerSample(0, position.x, position.y, PointerPhase.Stationary));
            }
        }
#endif

        private static PointerPhase ConvertPhase(UnityEngine.TouchPhase phase)
        {
            switch (phase)
            {
                case UnityEngine.TouchPhase.Began:
                    return PointerPhase.Began;
                case UnityEngine.TouchPhase.Moved:
                    return PointerPhase.Moved;
                case UnityEngine.TouchPhase.Stationary:
                    return PointerPhase.Stationary;
                case UnityEngine.TouchPhase.Ended:
                    return PointerPhase.Ended;
                default:
                    return PointerPhase.Canceled;
            }
        }
    }
}
