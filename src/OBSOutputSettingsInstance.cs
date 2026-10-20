using libomtnet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace omtplugin
{
    internal class OBSOutputSettingsInstance : OBSSourceInstance
    {

        private OBSOutputSettings parent;

        public OBSOutputSettingsInstance(OBSOutputSettings parent, nint source, nint settings) : base(source, settings)
        {
            this.parent = parent;
            UpdateSettings(settings);
        }

        protected override void DisposeInternal()
        {
            base.DisposeInternal();
        }

        public override void UpdateSettings(IntPtr settings)
        {
            try
            {
                if (settings != IntPtr.Zero)
                {
                    OMTLogging.Write("UpdateSettings", "OMTOutput");
                    parent.SaveSettings(settings);
                    OBSOutput.UpdateMainOutput();
                }
            }
            catch (Exception ex)
            {
                OMTLogging.Write(ex.ToString(), "OMTOutputSettings.UpdateSettings");
            }
            base.UpdateSettings(settings);
        }

        public override nint GetProperties()
        {
            try
            {
                IntPtr properties = OBS.obs_properties_create();
                if (properties != IntPtr.Zero)
                {
                    OBS.obs_properties_set_flags(properties, OBS.OBS_PROPERTIES_DEFER_UPDATE);
                    IntPtr enabledProperty = OBS.obs_properties_add_bool(properties, "enabledProperty", "Output Enabled");
                    IntPtr nameProperty = OBS.obs_properties_add_text(properties, "nameProperty", "Output Name", 0);
                    IntPtr modeProperty = OBS.obs_properties_add_list(properties, "modeProperty", "Mode", OBS.obs_combo_type.OBS_COMBO_TYPE_LIST, OBS.obs_combo_format.OBS_COMBO_FORMAT_INT);
                    if (modeProperty != IntPtr.Zero)
                    {
                        OBS.obs_property_list_add_int(modeProperty, "OMT (Full)", NativeOmthx.ModeFull);
                        if (NativeOmthx.Available)
                        {
                            OBS.obs_property_list_add_int(modeProperty, "OMT HX (HEVC/H.264)", NativeOmthx.ModeHx);
                        }
                    }
                    IntPtr qualityProperty = OBS.obs_properties_add_list(properties, "qualityProperty", "Full Quality", OBS.obs_combo_type.OBS_COMBO_TYPE_LIST, OBS.obs_combo_format.OBS_COMBO_FORMAT_INT);
                    if (qualityProperty != IntPtr.Zero)
                    {
                        foreach (OMTQuality quality in Enum.GetValues<OMTQuality>())
                        {
                            OBS.obs_property_list_add_int(qualityProperty, quality.ToString(), (long)quality);
                        }
                    }
                    IntPtr codecProperty = OBS.obs_properties_add_list(properties, "codecProperty", "HX Codec", OBS.obs_combo_type.OBS_COMBO_TYPE_LIST, OBS.obs_combo_format.OBS_COMBO_FORMAT_INT);
                    if (codecProperty != IntPtr.Zero)
                    {
                        OBS.obs_property_list_add_int(codecProperty, "H.265 (HEVC)", NativeOmthx.CodecHevc);
                        OBS.obs_property_list_add_int(codecProperty, "H.264 (AVC)", NativeOmthx.CodecH264);
                    }
                    OBS.obs_properties_add_bool(properties, "alphaProperty", "HX Alpha");
                    OMTLogging.Write("GetProperties", "OMTOutput");
                }
                return properties;
            }
            catch (Exception ex)
            {
                OMTLogging.Write(ex.ToString(), "OMTSource.GetProperties");
            }
            return base.GetProperties();
        }
    }
}
