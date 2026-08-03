using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Scripting;

namespace Higurashi.IOS.Runtime.Data
{
    [Preserve]
    public sealed class IOSDataPackFilePicker : MonoBehaviour
    {
        private Action<string> _onSelected;
        private Action<string> _onFailed;
        private bool _isPresenting;

        public bool IsPresenting => _isPresenting;

        public void Pick(
            string destinationPath,
            Action<string> onSelected,
            Action<string> onFailed)
        {
            if (_isPresenting)
            {
                return;
            }

            _onSelected = onSelected;
            _onFailed = onFailed;

#if UNITY_IOS && !UNITY_EDITOR
            _isPresenting = true;
            Higurashi_ShowDataPackPicker(destinationPath, gameObject.name);
#else
            _isPresenting = false;
            _onFailed?.Invoke("当前平台不支持 iOS 文件选择器。");
            ClearCallbacks();
#endif
        }

        [Preserve]
        public void OnDataPackPicked(string selectedPath)
        {
            _isPresenting = false;
            var callback = _onSelected;
            ClearCallbacks();
            callback?.Invoke(selectedPath);
        }

        [Preserve]
        public void OnDataPackPickerFailed(string message)
        {
            _isPresenting = false;
            var callback = _onFailed;
            ClearCallbacks();
            callback?.Invoke(string.IsNullOrEmpty(message) ? "未能读取所选文件。" : message);
        }

        private void ClearCallbacks()
        {
            _onSelected = null;
            _onFailed = null;
        }

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void Higurashi_ShowDataPackPicker(
            string destinationPath,
            string callbackGameObject);
#endif
    }
}
