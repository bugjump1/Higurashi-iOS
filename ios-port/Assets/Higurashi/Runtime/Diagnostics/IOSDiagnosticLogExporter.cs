using System.Runtime.InteropServices;
using UnityEngine;

namespace Higurashi.IOS.Runtime.Diagnostics
{
    internal static class IOSDiagnosticLogExporter
    {
        public static bool Share(string path)
        {
#if UNITY_IOS && !UNITY_EDITOR
            return Higurashi_ShareDiagnosticLog(path) != 0;
#else
            Debug.Log("Diagnostic log created at: " + path);
            return false;
#endif
        }

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern int Higurashi_ShareDiagnosticLog(string path);
#endif
    }
}
