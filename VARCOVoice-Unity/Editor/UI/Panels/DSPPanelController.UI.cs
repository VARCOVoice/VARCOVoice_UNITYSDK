using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using VARCOVoice.DSP;
using Object = UnityEngine.Object;

namespace VARCOVoice.Editor
{
    public partial class DSPPanelController
    {
        // ===================================================
        // NOTE: IMGUI OnGUI is no longer used.
        // EQ Visualizer is now fully UI Toolkit based.
        // This file is kept for reference/legacy methods.
        // ===================================================

        // Keyboard shortcuts are now handled in the main Initialize method
        // via UI Toolkit's KeyDownEvent if needed.
    }
}
