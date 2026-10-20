/*
* MIT License
*
* Copyright (c) 2025 Open Media Transport Contributors
*
* Permission is hereby granted, free of charge, to any person obtaining a copy
* of this software and associated documentation files (the "Software"), to deal
* in the Software without restriction, including without limitation the rights
* to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
* copies of the Software, and to permit persons to whom the Software is
* furnished to do so, subject to the following conditions:
*
* The above copyright notice and this permission notice shall be included in all
* copies or substantial portions of the Software.
*
* THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
* IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
* FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
* AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
* LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
* OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
* SOFTWARE.
*
*/

using libomtnet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace omtplugin
{
    internal class OBSReceiveInstance : OBSSourceInstance
    {
        private bool threadRunning = false;
        private Thread? thread;
        private OMTReceive? receiver;
        private IntPtr hxReceiver = IntPtr.Zero;
        private string? address = null;
        private OMTQuality suggestedQuality = OMTQuality.Default;
        private bool previewMode = false;
        private int activeMode = NativeOmthx.ModeFull;
        private string? activeAddress = null;
        private OBS.DisplayColorSpace displayColorSpace = OBS.DisplayColorSpace.Default;
        private OBS.DisplayColorSpace activeColorSpace = OBS.DisplayColorSpace.Default;

        private object lockSync = new object();

        private IntPtr tempData = IntPtr.Zero;
        private int tempDataLength = 0;

        public OBSReceiveInstance(IntPtr source, IntPtr settings) : base(source,settings)
        {
            this.receiver = null;
            this.thread = null;
            this.threadRunning = false;
            UpdateSettings(settings);
        }

        private void StartThread()
        {
            lock (lockSync)
            {
                if (thread == null)
                {
                    threadRunning = true;
                    thread = new Thread(activeMode == NativeOmthx.ModeHx ? VideoProcHx : VideoProc);
                    thread.IsBackground = true;
                    thread.Start();
                    OMTLogging.Write("VideoProc.Started", "OMTSource");
                }
            }

        }
        private void StopThread()
        {
            lock (lockSync)
            {
                if (thread != null)
                {
                    threadRunning = false;
                    thread.Join(5000);
                    thread = null;
                    OMTLogging.Write("VideoProc.Stopped", "OMTSource");
                }
            }
        }
        private void CreateReceiver()
        {
            try
            {
                StopThread();
                CloseReceivers();
                if (!String.IsNullOrEmpty(this.address))
                {
                    // The source says whether it sends Full or HX; the user does not choose. A source that does
                    // not answer in time is read as Full, the path that needs no native library.
                    int detected = NativeOmthx.Available ? NativeOmthx.DetectMode(this.address) : NativeOmthx.ModeFull;
                    if (detected == NativeOmthx.ModeHx)
                    {
                        hxReceiver = NativeOmthx.ReceiverOpen(this.address);
                        if (hxReceiver == IntPtr.Zero)
                        {
                            OMTLogging.Write("OMT HX receiver failed: " + NativeOmthx.Error(), "OMTSource");
                            return;
                        }
                        // A source already running may be mid-GOP: ask for a keyframe so a picture arrives now.
                        NativeOmthx.ReceiverKeyframe(hxReceiver);
                        OMTLogging.Write("New OMT HX Receiver: " + this.address + " (" +
                                         NativeOmthx.ReceiverDecoder(hxReceiver) + ")", "OMTSource");
                    }
                    else
                    {
                        OMTPreferredVideoFormat preferred = OMTPreferredVideoFormat.UYVYorBGRA;
                        if (displayColorSpace == OBS.DisplayColorSpace.BT709_P010 || displayColorSpace == OBS.DisplayColorSpace.BT2100_PQ_P010 || displayColorSpace == OBS.DisplayColorSpace.BT2100_HLG_P010)
                        {
                            preferred = OMTPreferredVideoFormat.P216;
                        }
                        receiver = new OMTReceive(this.address, OMTFrameType.Video | OMTFrameType.Audio, preferred, OMTReceiveFlags.None);
                        OMTLogging.Write("New Receiver: " + this.address + "," + preferred.ToString(), "OMTSource");
                    }
                    activeColorSpace = displayColorSpace;
                    activeMode = detected;
                    activeAddress = this.address;
                    StartThread();
                }
            }
            catch (Exception ex)
            {
                OMTLogging.Write(ex.ToString(), "OMTSource.CreateReceiver");
            }
        }

        private void CloseReceivers()
        {
            if (receiver != null)
            {
                receiver.Dispose();
                receiver = null;
            }
            if (hxReceiver != IntPtr.Zero)
            {
                NativeOmthx.ReceiverClose(hxReceiver);
                hxReceiver = IntPtr.Zero;
            }
        }

        private void UpdateReceiver()
        {
            try
            {
                lock (lockSync)
                {
                    bool connected = receiver != null || hxReceiver != IntPtr.Zero;
                    if (connected)
                    {
                        if (activeAddress != this.address || activeColorSpace != displayColorSpace)
                        {
                            CreateReceiver();
                        }
                    }
                    else
                    {
                        CreateReceiver();
                    }
                    if (receiver != null)
                    {
                        receiver.SetSuggestedQuality(suggestedQuality);
                        if (previewMode)
                        {
                            receiver.SetFlags(OMTReceiveFlags.Preview);
                        }
                        else
                        {
                            receiver.SetFlags(OMTReceiveFlags.None);
                        }
                    }
                    UpdateTally();
                }
            }
            catch (Exception ex)
            {
                OMTLogging.Write(ex.ToString(), "OMTSource.UpdateReceiver");
            }
        }

        // [MODIFICATION OMT TALLY] Lifecycle overrides triggered by OBS Studio
        public override void Activate()
        {
            UpdateTally();
        }

        public override void Deactivate()
        {
            UpdateTally();
        }

        public override void Show()
        {
            UpdateTally();
        }

        public override void Hide()
        {
            UpdateTally();
        }

        /// <summary>
        /// Queries the current OBS state for this source:
        /// - Program (Red): obs_source_active(source) is true (the source is active in Program output).
        /// - Preview (Green): obs_source_showing(source) is true while not active in Program (e.g. In Studio Mode Preview).
        /// - Off: Not active and not showing.
        /// Sends the tally state over TCP via receiver.SetTally().
        /// </summary>
        public void UpdateTally()
        {
            try
            {
                lock (lockSync)
                {
                    if ((receiver != null || hxReceiver != IntPtr.Zero) && source != IntPtr.Zero)
                    {
                        bool isActive = OBS.obs_source_active(source);
                        bool isShowing = OBS.obs_source_showing(source);

                        int program = isActive ? 1 : 0;
                        int preview = (!isActive && isShowing) ? 1 : 0;

                        OMTLogging.Write($"UpdateTally: showing={isShowing}, active={isActive} -> Preview={preview}, Program={program}", "OMTSource");
                        if (hxReceiver != IntPtr.Zero)
                        {
                            NativeOmthx.ReceiverTally(hxReceiver, preview == 1, program == 1);
                        }
                        else if (receiver != null)
                        {
                            receiver.SetTally(new OMTTally(preview, program));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                OMTLogging.Write(ex.ToString(), "OMTSource.UpdateTally");
            }
        }

        public override nint GetProperties()
        {
            try
            {
                IntPtr properties = OBS.obs_properties_create();
                if (properties != IntPtr.Zero)
                {
                    IntPtr sourceProperty = OBS.obs_properties_add_list(properties, "sourceProperty", "Source", OBS.obs_combo_type.OBS_COMBO_TYPE_LIST, OBS.obs_combo_format.OBS_COMBO_FORMAT_STRING);
                    if (sourceProperty != IntPtr.Zero)
                    {
                        OBS.obs_property_list_add_string(sourceProperty, "", "");
                        string[] sources = OMTDiscovery.GetInstance().GetAddresses();
                        if (sources != null)
                        {
                            foreach (string source in sources)
                            {
                                OBS.obs_property_list_add_string(sourceProperty, source, source);
                            }
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
                    IntPtr csProperty = OBS.obs_properties_add_list(properties, "csProperty", "Color Space", OBS.obs_combo_type.OBS_COMBO_TYPE_LIST, OBS.obs_combo_format.OBS_COMBO_FORMAT_INT);
                    if (csProperty != IntPtr.Zero)
                    {
                        foreach (OBS.DisplayColorSpace cs in Enum.GetValues<OBS.DisplayColorSpace>())
                        {
                            OBS.obs_property_list_add_int(csProperty, cs.ToString(), (long)cs);
                        }
                    }
                    IntPtr previewProperty = OBS.obs_properties_add_bool(properties, "previewProperty", "Preview Mode");
                }
                return properties;
            }
            catch (Exception ex)
            {
                OMTLogging.Write(ex.ToString(), "OMTSource.GetProperties");
            }
            return base.GetProperties();
        }
        public override void UpdateSettings(nint settings)
        {
            try
            {
                if (settings != IntPtr.Zero)
                {
                    IntPtr pSource = OBS.obs_data_get_string(settings, "sourceProperty");
                    if (pSource != IntPtr.Zero)
                    {
                        string? source = Marshal.PtrToStringUTF8(pSource);
                        if (!String.IsNullOrEmpty(source))
                        {
                            this.address = source;
                        }
                        else
                        {
                            this.address = null;
                        }
                    }
                    else
                    {
                        this.address = null;
                    }
                    Int64 q = OBS.obs_data_get_int(settings, "qualityProperty");
                    this.suggestedQuality = (OMTQuality)q;
                    this.displayColorSpace = (OBS.DisplayColorSpace)OBS.obs_data_get_int(settings, "csProperty");
                    this.previewMode = OBS.obs_data_get_bool(settings, "previewProperty");
                    
                }
                UpdateReceiver();
            }
            catch (Exception ex)
            {
                OMTLogging.Write(ex.ToString(), "OMTSource.UpdateSettings");
            }
            base.UpdateSettings(settings);
        }

        private void VideoProc()
        {
            try
            {
                OBS.obs_source_frame frame = new OBS.obs_source_frame();
                frame.linesize = new uint[8];
                frame.data = new nint[8];
                frame.color_matrix = new float[16];
                frame.color_range_min = new float[3];
                frame.color_range_max = new float[3];
                frame.trc = (byte)OBS.video_trc.VIDEO_TRC_DEFAULT;

                OBS.obs_source_audio audio = new OBS.obs_source_audio();
                audio.data = new nint[8];

                OBS.video_colorspace cs = OBS.video_colorspace.VIDEO_CS_DEFAULT;
                if (activeColorSpace == OBS.DisplayColorSpace.BT2100_PQ_P010)
                {
                    cs = OBS.video_colorspace.VIDEO_CS_2100_PQ;
                    frame.trc = (byte)OBS.video_trc.VIDEO_TRC_PQ;
                } else if (activeColorSpace == OBS.DisplayColorSpace.BT2100_HLG_P010)
                {
                    cs = OBS.video_colorspace.VIDEO_CS_2100_HLG;
                    frame.trc = (byte)OBS.video_trc.VIDEO_TRC_HLG;
                } else if (activeColorSpace == OBS.DisplayColorSpace.BT601)
                {
                    cs = OBS.video_colorspace.VIDEO_CS_601;
                } else if (activeColorSpace == OBS.DisplayColorSpace.BT709 || activeColorSpace == OBS.DisplayColorSpace.BT709)
                {
                    cs = OBS.video_colorspace.VIDEO_CS_709;
                }

                if (OBS.video_format_get_parameters(cs, OBS.video_range_type.VIDEO_RANGE_PARTIAL,
                    frame.color_matrix, frame.color_range_min, frame.color_range_max) == true)
                {
                    OMTLogging.Write("ColorFormatRetrieved: " + cs.ToString(), "OMTSource");
                }
                OMTMediaFrame mediaFrame = new OMTMediaFrame();

                while (threadRunning)
                {
                    OMTReceive? r = receiver;
                    if (r == null) break;
                    if (r.Receive(OMTFrameType.Video | OMTFrameType.Audio, 100, ref mediaFrame))
                    {
                        if (mediaFrame.DataLength > 0)
                        {
                            if (mediaFrame.Type == OMTFrameType.Audio)
                            {
                                audio.format = OBS.audio_format.AUDIO_FORMAT_FLOAT_PLANAR;
                                audio.speakers = OBS.speaker_layout.SPEAKERS_STEREO;
                                audio.samples_per_sec = (uint)mediaFrame.SampleRate;
                                audio.timestamp = (ulong)mediaFrame.Timestamp * 100;
                                audio.frames = (uint)mediaFrame.SamplesPerChannel;
                                audio.data[0] = mediaFrame.Data;
                                audio.data[1] = mediaFrame.Data + (mediaFrame.SamplesPerChannel * 4);
                                OBS.obs_source_output_audio(this.source, ref audio);
                            }
                            else if (mediaFrame.Type == OMTFrameType.Video)
                            {
                                frame.width = (uint)mediaFrame.Width;
                                frame.height = (uint)mediaFrame.Height;
                                frame.timestamp = (ulong)mediaFrame.Timestamp * 100;
                                frame.data[0] = mediaFrame.Data;

                                if (mediaFrame.Codec == (int)OMTCodec.UYVY)
                                {
                                    frame.format = OBS.video_format.VIDEO_FORMAT_UYVY;
                                    frame.linesize[0] = frame.width * 2;
                                    OBS.obs_source_output_video(this.source, ref frame);
                                }
                                else if (mediaFrame.Codec == (int)OMTCodec.BGRA)
                                {
                                    frame.format = OBS.video_format.VIDEO_FORMAT_BGRA;
                                    frame.linesize[0] = frame.width * 4;
                                    OBS.obs_source_output_video(this.source, ref frame);
                                } else if (mediaFrame.Codec == (int)OMTCodec.P216)
                                {
                                    frame.format = OBS.video_format.VIDEO_FORMAT_P010;

                                    int len = mediaFrame.DataLength;
                                    if (tempDataLength != len)
                                    {
                                        if (tempData != IntPtr.Zero)
                                        {
                                            Marshal.FreeHGlobal(tempData);
                                            tempData = IntPtr.Zero;
                                        }
                                        tempDataLength = len;
                                        tempData = Marshal.AllocHGlobal(len);
                                    }

                                    IntPtr dstY = tempData;
                                    IntPtr dstUV = tempData + (mediaFrame.Stride * mediaFrame.Height);
                                    Utils.P216ToP010(frame.data[0], mediaFrame.Stride, dstY, mediaFrame.Stride, dstUV, mediaFrame.Stride, mediaFrame.Width, mediaFrame.Height); 

                                    frame.linesize[0] = (uint)mediaFrame.Stride;
                                    frame.linesize[1] = (uint)mediaFrame.Stride;
                                    frame.data[0] = dstY;
                                    frame.data[1] = dstUV;

                                    OBS.obs_source_output_video(this.source, ref frame);
                                }
                            }
                        }
                    }
                }
                OMTLogging.Write("ThreadExited", "OMTSource");
            }
            catch (Exception ex)
            {
                OMTLogging.Write(ex.ToString(), "OMTSource.VideoProc");
            }
        }

        private void VideoProcHx()
        {
            try
            {
                OBS.obs_source_frame frame = new OBS.obs_source_frame();
                frame.linesize = new uint[8];
                frame.data = new nint[8];
                frame.color_matrix = new float[16];
                frame.color_range_min = new float[3];
                frame.color_range_max = new float[3];
                frame.trc = (byte)OBS.video_trc.VIDEO_TRC_DEFAULT;
                frame.format = OBS.video_format.VIDEO_FORMAT_BGRA;
                frame.full_range = 1;  // OMT-HX is 8-bit BT.709 full range

                OBS.video_format_get_parameters(OBS.video_colorspace.VIDEO_CS_709, OBS.video_range_type.VIDEO_RANGE_FULL,
                    frame.color_matrix, frame.color_range_min, frame.color_range_max);

                OBS.obs_source_audio audio = new OBS.obs_source_audio();
                audio.data = new nint[8];
                audio.format = OBS.audio_format.AUDIO_FORMAT_FLOAT_PLANAR;

                while (threadRunning)
                {
                    IntPtr r = hxReceiver;
                    if (r == IntPtr.Zero) break;
                    if (NativeOmthx.ReceiverVideo(r, 100, out NativeOmthx.VideoFormat vf, out _, out IntPtr pixels, out long ts) == 1)
                    {
                        frame.width = (uint)vf.Width;
                        frame.height = (uint)vf.Height;
                        frame.timestamp = (ulong)ts * 100;
                        frame.data[0] = pixels;
                        frame.linesize[0] = (uint)vf.Width * 4;
                        OBS.obs_source_output_video(this.source, ref frame);
                    }
                    if (NativeOmthx.ReceiverAudio(r, 0, out IntPtr planar, out int rate, out int channels, out int samples, out long ats) == 1)
                    {
                        if (channels == 1 || channels == 2)
                        {
                            audio.speakers = channels == 1 ? OBS.speaker_layout.SPEAKERS_MONO : OBS.speaker_layout.SPEAKERS_STEREO;
                            audio.samples_per_sec = (uint)rate;
                            audio.timestamp = (ulong)ats * 100;
                            audio.frames = (uint)samples;
                            audio.data[0] = planar;
                            audio.data[1] = channels == 2 ? planar + (samples * 4) : IntPtr.Zero;
                            OBS.obs_source_output_audio(this.source, ref audio);
                        }
                    }
                }
                OMTLogging.Write("HX ThreadExited", "OMTSource");
            }
            catch (Exception ex)
            {
                OMTLogging.Write(ex.ToString(), "OMTSource.VideoProcHx");
            }
        }

        protected override void DisposeInternal()
        {
            StopThread();
            CloseReceivers();
            if (tempData != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(tempData);
                tempData = IntPtr.Zero;
                tempDataLength = 0;
            }
            base.DisposeInternal();
        }
    }
}
