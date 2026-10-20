using System;
using System.Runtime.InteropServices;

namespace omtplugin
{
    // Mode picks which settings apply: Full has Quality; HX has Codec and Alpha. Hide the others, as DistroAV
    // does, so the dialog matches the mode.
    internal static class ModeVisibility
    {
        // Kept in a static field: OBS holds the pointer, and the delegate must outlive the dialog.
        private static readonly OBS.obs_property_modified callback = Apply;

        public static void Attach(IntPtr modeProperty)
        {
            OBS.obs_property_set_modified_callback(modeProperty, Marshal.GetFunctionPointerForDelegate(callback));
        }

        public static void Show(IntPtr properties, int mode)
        {
            bool hx = mode == NativeOmthx.ModeHx;
            Visible(properties, "qualityProperty", !hx);
            Visible(properties, "codecProperty", hx);
            Visible(properties, "alphaProperty", hx);
        }

        // The source has no Mode: these belong to a Full source, so hide them once the selected source is HX.
        public static void ShowFull(IntPtr properties, bool full)
        {
            Visible(properties, "qualityProperty", full);
            Visible(properties, "csProperty", full);
            Visible(properties, "previewProperty", full);
        }

        private static bool Apply(IntPtr properties, IntPtr property, IntPtr settings)
        {
            Show(properties, (int)OBS.obs_data_get_int(settings, "modeProperty"));
            return true;  // rebuild the property list
        }

        private static void Visible(IntPtr properties, string name, bool visible)
        {
            IntPtr property = OBS.obs_properties_get(properties, name);
            if (property != IntPtr.Zero)
            {
                OBS.obs_property_set_visible(property, visible);
            }
        }
    }
}