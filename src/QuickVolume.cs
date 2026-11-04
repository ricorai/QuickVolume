using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace QuickVolume
{
    #region COM Interop Definitions

    [ComImport]
    [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    internal class MMDeviceEnumeratorComObject { }

    [ComImport]
    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr devices);
        [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice endpoint);
        [PreserveSig] int GetDevice(string id, out IMMDevice device);
        [PreserveSig] int RegisterEndpointNotificationCallback(IntPtr client);
        [PreserveSig] int UnregisterEndpointNotificationCallback(IntPtr client);
    }

    [ComImport]
    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid id, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object interfacePointer);
        [PreserveSig] int OpenPropertyStore(int stgmAccess, out IntPtr properties);
        [PreserveSig] int GetId(out IntPtr id);
        [PreserveSig] int GetState(out int state);
    }

    [ComImport]
    [Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioEndpointVolume
    {
        [PreserveSig] int RegisterControlChangeNotify(IntPtr pNotify);
        [PreserveSig] int UnregisterControlChangeNotify(IntPtr pNotify);
        [PreserveSig] int GetChannelCount(out uint pnChannelCount);
        [PreserveSig] int SetMasterVolumeLevel(float fLevelDB, IntPtr pguidEventContext);
        [PreserveSig] int SetMasterVolumeLevelScalar(float fLevel, IntPtr pguidEventContext);
        [PreserveSig] int GetMasterVolumeLevel(out float pfLevelDB);
        [PreserveSig] int GetMasterVolumeLevelScalar(out float pfLevel);
        [PreserveSig] int SetChannelVolumeLevel(uint nChannel, float fLevelDB, IntPtr pguidEventContext);
        [PreserveSig] int SetChannelVolumeLevelScalar(uint nChannel, float fLevel, IntPtr pguidEventContext);
        [PreserveSig] int GetChannelVolumeLevel(uint nChannel, out float pfLevelDB);
        [PreserveSig] int GetChannelVolumeLevelScalar(uint nChannel, out float pfLevel);
        [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool bMute, IntPtr pguidEventContext);
        [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool pbMute);
        [PreserveSig] int GetVolumeStepInfo(out uint pnStep, out uint pnStepCount);
        [PreserveSig] int VolumeStepUp(IntPtr pguidEventContext);
        [PreserveSig] int VolumeStepDown(IntPtr pguidEventContext);
        [PreserveSig] int QueryHardwareSupport(out uint pdwHardwareSupportMask);
        [PreserveSig] int GetVolumeRange(out float pflVolumeMindB, out float pflVolumeMaxdB, out float pflVolumeIncrementdB);
    }

    [ComImport]
    [Guid("5F3034DE-144D-4858-82E8-880CEE3E60F8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioEndpointVolumeFallback
    {
        [PreserveSig] int RegisterControlChangeNotify(IntPtr pNotify);
        [PreserveSig] int UnregisterControlChangeNotify(IntPtr pNotify);
        [PreserveSig] int GetChannelCount(out uint pnChannelCount);
        [PreserveSig] int SetMasterVolumeLevel(float fLevelDB, IntPtr pguidEventContext);
        [PreserveSig] int SetMasterVolumeLevelScalar(float fLevel, IntPtr pguidEventContext);
        [PreserveSig] int GetMasterVolumeLevel(out float pfLevelDB);
        [PreserveSig] int GetMasterVolumeLevelScalar(out float pfLevel);
        [PreserveSig] int SetChannelVolumeLevel(uint nChannel, float fLevelDB, IntPtr pguidEventContext);
        [PreserveSig] int SetChannelVolumeLevelScalar(uint nChannel, float fLevel, IntPtr pguidEventContext);
        [PreserveSig] int GetChannelVolumeLevel(uint nChannel, out float pfLevelDB);
        [PreserveSig] int GetChannelVolumeLevelScalar(uint nChannel, out float pfLevel);
        [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool bMute, IntPtr pguidEventContext);
        [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool pbMute);
        [PreserveSig] int GetVolumeStepInfo(out uint pnStep, out uint pnStepCount);
        [PreserveSig] int VolumeStepUp(IntPtr pguidEventContext);
        [PreserveSig] int VolumeStepDown(IntPtr pguidEventContext);
        [PreserveSig] int QueryHardwareSupport(out uint pdwHardwareSupportMask);
        [PreserveSig] int GetVolumeRange(out float pflVolumeMindB, out float pflVolumeMaxdB, out float pflVolumeIncrementdB);
    }

    #endregion

    #region Audio Manager

    public static class AudioManager
    {
        private static readonly Guid IID_IAudioEndpointVolume = new Guid("5CDF2C82-841E-4546-9722-0CF74078229A");
        private static readonly Guid IID_IAudioEndpointVolume_Fallback = new Guid("5F3034DE-144D-4858-82E8-880CEE3E60F8");

        public static float GetMasterVolume()
        {
            IMMDeviceEnumerator enumerator = null;
            IMMDevice device = null;
            IAudioEndpointVolume volume = null;
            try
            {
                enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
                int hr = enumerator.GetDefaultAudioEndpoint(0, 0, out device);
                if (hr != 0 || device == null)
                {
                    hr = enumerator.GetDefaultAudioEndpoint(0, 1, out device);
                    if (hr != 0 || device == null) return -1.0f;
                }

                object epvObj = null;
                Guid iid = IID_IAudioEndpointVolume;
                hr = device.Activate(ref iid, 23, IntPtr.Zero, out epvObj);
                if (hr != 0 || epvObj == null)
                {
                    Guid iidFallback = IID_IAudioEndpointVolume_Fallback;
                    hr = device.Activate(ref iidFallback, 23, IntPtr.Zero, out epvObj);
                    if (hr != 0 || epvObj == null) return -1.0f;
                }

                volume = (IAudioEndpointVolume)epvObj;
                float level;
                hr = volume.GetMasterVolumeLevelScalar(out level);
                return (hr == 0) ? level : -1.0f;
            }
            catch
            {
                return -1.0f;
            }
            finally
            {
                if (volume != null) Marshal.ReleaseComObject(volume);
                if (device != null) Marshal.ReleaseComObject(device);
                if (enumerator != null) Marshal.ReleaseComObject(enumerator);
            }
        }

        public static bool SetMasterVolume(float level)
        {
            IMMDeviceEnumerator enumerator = null;
            IMMDevice device = null;
            IAudioEndpointVolume volume = null;
            try
            {
                if (level < 0.0f) level = 0.0f;
                if (level > 1.0f) level = 1.0f;

                enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
                int hr = enumerator.GetDefaultAudioEndpoint(0, 0, out device);
                if (hr != 0 || device == null)
                {
                    hr = enumerator.GetDefaultAudioEndpoint(0, 1, out device);
                    if (hr != 0 || device == null) return false;
                }

                object epvObj = null;
                Guid iid = IID_IAudioEndpointVolume;
                hr = device.Activate(ref iid, 23, IntPtr.Zero, out epvObj);
                if (hr != 0 || epvObj == null)
                {
                    Guid iidFallback = IID_IAudioEndpointVolume_Fallback;
                    hr = device.Activate(ref iidFallback, 23, IntPtr.Zero, out epvObj);
                    if (hr != 0 || epvObj == null) return false;
                }

                volume = (IAudioEndpointVolume)epvObj;
                hr = volume.SetMasterVolumeLevelScalar(level, IntPtr.Zero);
                if (hr != 0) return false;

                bool isMuted;
                if (volume.GetMute(out isMuted) == 0 && isMuted)
                {
                    volume.SetMute(false, IntPtr.Zero);
                }

                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                if (volume != null) Marshal.ReleaseComObject(volume);
                if (device != null) Marshal.ReleaseComObject(device);
                if (enumerator != null) Marshal.ReleaseComObject(enumerator);
            }
        }

        public static float CycleMasterVolume(IList<int> slots)
        {
            if (slots == null || slots.Count == 0) return 0.25f;

            float current = GetMasterVolume();
            int currentPct = (current >= 0.0f) ? (int)Math.Round(current * 100.0f) : 25;

            int targetIndex = 0;
            int matchIdx = -1;
            for (int i = 0; i < slots.Count; i++)
            {
                if (Math.Abs(currentPct - slots[i]) <= 2)
                {
                    matchIdx = i;
                    break;
                }
            }

            if (matchIdx >= 0)
            {
                targetIndex = (matchIdx + 1) % slots.Count;
            }
            else
            {
                bool found = false;
                for (int i = 0; i < slots.Count; i++)
                {
                    if (slots[i] > currentPct)
                    {
                        targetIndex = i;
                        found = true;
                        break;
                    }
                }
                if (!found) targetIndex = 0;
            }

            float targetScalar = slots[targetIndex] / 100.0f;
            SetMasterVolume(targetScalar);
            return targetScalar;
        }
    }

    #endregion

    #region Settings & Persistence

    public class QuickVolumeConfig
    {
        public List<int> Slots = new List<int>() { 0, 25, 100 };
    }

    public static class SettingsManager
    {
        private const string ConfigFileName = "QuickVolume.json";
        public static readonly int[] DefaultSlots = new int[] { 0, 25, 100 };

        private static string GetConfigPath()
        {
            try
            {
                string appDir = AppDomain.CurrentDomain.BaseDirectory;
                return Path.Combine(appDir, ConfigFileName);
            }
            catch
            {
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                return Path.Combine(localAppData, "QuickVolume\\" + ConfigFileName);
            }
        }

        public static QuickVolumeConfig Load()
        {
            var config = new QuickVolumeConfig();
            string path = GetConfigPath();
            try
            {
                if (File.Exists(path))
                {
                    string text = File.ReadAllText(path, Encoding.UTF8);
                    MatchCollection matches = Regex.Matches(text, @"\b(\d+)\b");
                    var loadedSlots = new List<int>();
                    foreach (Match m in matches)
                    {
                        int val;
                        if (int.TryParse(m.Groups[1].Value, out val))
                        {
                            if (val < 0) val = 0;
                            if (val > 100) val = 100;
                            loadedSlots.Add(val);
                            if (loadedSlots.Count >= 10) break;
                        }
                    }

                    if (loadedSlots.Count > 0)
                    {
                        config.Slots = loadedSlots;
                        return config;
                    }
                }
            }
            catch {}

            config.Slots = new List<int>(DefaultSlots);
            Save(config);
            return config;
        }

        public static void Save(QuickVolumeConfig config)
        {
            try
            {
                if (config == null || config.Slots == null || config.Slots.Count == 0)
                {
                    config = new QuickVolumeConfig();
                }

                if (config.Slots.Count > 10)
                {
                    config.Slots = config.Slots.GetRange(0, 10);
                }

                string path = GetConfigPath();
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var sb = new StringBuilder();
                sb.AppendLine("{");
                sb.AppendLine("  \"slots\": [");
                for (int i = 0; i < config.Slots.Count; i++)
                {
                    int val = Math.Max(0, Math.Min(100, config.Slots[i]));
                    sb.Append("    " + val);
                    if (i < config.Slots.Count - 1) sb.Append(",");
                    sb.AppendLine();
                }
                sb.AppendLine("  ]");
                sb.AppendLine("}");

                File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            }
            catch {}
        }
    }

    #endregion

    #region Startup Manager

    public static class StartupManager
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "QuickVolume";

        public static bool IsStartupEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false))
                {
                    if (key == null) return false;
                    object val = key.GetValue(ValueName);
                    if (val == null) return false;
                    string strVal = val.ToString().Trim('\"');
                    return !string.IsNullOrEmpty(strVal);
                }
            }
            catch
            {
                return false;
            }
        }

        public static void SetStartup(bool enable)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true))
                {
                    if (key == null) return;
                    if (enable)
                    {
                        string exePath = Application.ExecutablePath;
                        key.SetValue(ValueName, "\"" + exePath + "\"");
                    }
                    else
                    {
                        if (key.GetValue(ValueName) != null)
                        {
                            key.DeleteValue(ValueName, false);
                        }
                    }
                }
            }
            catch {}
        }
    }

    #endregion

    #region Windows 11 Fluent Theme Palette & Geometry Helpers

    internal static class DarkTheme
    {
        public static readonly Color BgColor = ColorTranslator.FromHtml("#1F1F1F");
        public static readonly Color BgGradientBottom = ColorTranslator.FromHtml("#1C1C1C");
        public static readonly Color TileHover = Color.FromArgb(13, 255, 255, 255);
        public static readonly Color ActiveRowBg = Color.FromArgb(15, 255, 255, 255);
        public static readonly Color TextColor = ColorTranslator.FromHtml("#EDEDED");
        public static readonly Color TextActiveColor = Color.White;
        public static readonly Color MutedColor = ColorTranslator.FromHtml("#8E8E8E");
        public static readonly Color BorderColor = ColorTranslator.FromHtml("#353535");
        public static readonly Color DividerSecondary = ColorTranslator.FromHtml("#2B2B2B");
        public static readonly Color AccentColor = ColorTranslator.FromHtml("#A6A5A1");
        public static readonly Color AccentHover = ColorTranslator.FromHtml("#B8B7B3");
        public static readonly Color TrackBg = ColorTranslator.FromHtml("#383838");
        public static readonly Color BtnSecBg = ColorTranslator.FromHtml("#252525");
        public static readonly Color BtnSecBorder = ColorTranslator.FromHtml("#3A3A3A");
        public static readonly Color BtnSecHoverBg = ColorTranslator.FromHtml("#2E2E2E");
        public static readonly Color BtnSecHoverBorder = ColorTranslator.FromHtml("#4A4A4A");
        public static readonly Color DeleteHoverColor = ColorTranslator.FromHtml("#FF8080");
        public static readonly Color DeleteHoverBg = Color.FromArgb(30, 255, 90, 90);

        public static GraphicsPath CreateRoundedRect(Rectangle r, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static void DrawVectorCheck(Graphics g, float cx, float cy, Color color)
        {
            using (var pen = new Pen(color, 1.4f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;
                PointF[] pts = new PointF[] {
                    new PointF(cx - 4.2f, cy + 0.2f),
                    new PointF(cx - 1.5f, cy + 3.0f),
                    new PointF(cx + 4.5f, cy - 3.0f)
                };
                g.DrawLines(pen, pts);
            }
        }

        public static void DrawVectorMinus(Graphics g, float cx, float cy, Color color)
        {
            using (var pen = new Pen(color, 1.3f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                g.DrawLine(pen, cx - 4.5f, cy, cx + 4.5f, cy);
            }
        }

        public static void DrawVectorPlus(Graphics g, float cx, float cy, Color color)
        {
            using (var pen = new Pen(color, 1.3f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                g.DrawLine(pen, cx - 4.5f, cy, cx + 4.5f, cy);
                g.DrawLine(pen, cx, cy - 4.5f, cx, cy + 4.5f);
            }
        }

        public static void DrawVectorClose(Graphics g, float cx, float cy, Color color)
        {
            using (var pen = new Pen(color, 1.3f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                g.DrawLine(pen, cx - 4.2f, cy - 4.2f, cx + 4.2f, cy + 4.2f);
                g.DrawLine(pen, cx + 4.2f, cy - 4.2f, cx - 4.2f, cy + 4.2f);
            }
        }

        public static void DrawVectorBackArrow(Graphics g, float cx, float cy, Color color)
        {
            using (var pen = new Pen(color, 1.3f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;
                g.DrawLine(pen, cx + 4f, cy, cx - 4f, cy);
                g.DrawLine(pen, cx - 0.5f, cy - 3.5f, cx - 4f, cy);
                g.DrawLine(pen, cx - 0.5f, cy + 3.5f, cx - 4f, cy);
            }
        }

        public static void DrawVectorGear(Graphics g, Rectangle r, Color color)
        {
            float cx = r.X + r.Width / 2.0f;
            float cy = r.Y + r.Height / 2.0f;

            float rOut = 5.6f;
            float rIn = 3.8f;
            float rHole = 1.9f;

            using (var cog = new GraphicsPath())
            using (var b = new SolidBrush(color))
            {
                int teeth = 8;
                PointF[] pts = new PointF[teeth * 4];
                for (int i = 0; i < teeth; i++)
                {
                    double a0 = (i * 360.0 / teeth - 12) * Math.PI / 180.0;
                    double a1 = (i * 360.0 / teeth - 6) * Math.PI / 180.0;
                    double a2 = (i * 360.0 / teeth + 6) * Math.PI / 180.0;
                    double a3 = (i * 360.0 / teeth + 12) * Math.PI / 180.0;
                    pts[i * 4 + 0] = new PointF(cx + (float)(rIn * Math.Cos(a0)), cy + (float)(rIn * Math.Sin(a0)));
                    pts[i * 4 + 1] = new PointF(cx + (float)(rOut * Math.Cos(a1)), cy + (float)(rOut * Math.Sin(a1)));
                    pts[i * 4 + 2] = new PointF(cx + (float)(rOut * Math.Cos(a2)), cy + (float)(rOut * Math.Sin(a2)));
                    pts[i * 4 + 3] = new PointF(cx + (float)(rIn * Math.Cos(a3)), cy + (float)(rIn * Math.Sin(a3)));
                }
                cog.AddPolygon(pts);
                cog.AddEllipse(cx - rHole, cy - rHole, rHole * 2, rHole * 2);
                cog.FillMode = FillMode.Alternate;
                g.FillPath(b, cog);
            }
        }
    }

    #endregion

    #region Custom Smooth Dark Volume Slider Control

    internal class DarkSlider : Control
    {
        private int _value = 25;
        private bool _isDragging = false;
        private bool _isHovered = false;

        public event EventHandler ValueChanged;

        public int Value
        {
            get { return _value; }
            set
            {
                int clamped = Math.Max(0, Math.Min(100, value));
                if (_value != clamped)
                {
                    _value = clamped;
                    Invalidate();
                    if (ValueChanged != null) ValueChanged(this, EventArgs.Empty);
                }
            }
        }

        public DarkSlider()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.Selectable, true);
            Height = 42;
            Cursor = Cursors.Hand;
            BackColor = DarkTheme.BgColor;
        }

        private Rectangle GetTrackBounds()
        {
            return new Rectangle(36, Height / 2 - 5, Width - 44, 10);
        }

        private float GetThumbX()
        {
            Rectangle track = GetTrackBounds();
            return track.X + (track.Width * (_value / 100.0f));
        }

        private RectangleF GetThumbBounds()
        {
            float tx = GetThumbX();
            float cy = Height / 2.0f;
            return new RectangleF(tx - 7.5f, cy - 7.5f, 15f, 15f);
        }

        private void UpdateValueFromX(int mouseX)
        {
            Rectangle track = GetTrackBounds();
            float frac = (float)(mouseX - track.X) / track.Width;
            if (frac < 0f) frac = 0f;
            if (frac > 1f) frac = 1f;
            Value = (int)Math.Round(frac * 100.0f);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                Focus();
                _isDragging = true;
                Capture = true;
                UpdateValueFromX(e.X);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            bool wasHovered = _isHovered;
            _isHovered = GetThumbBounds().Contains(e.Location) || _isDragging;
            if (wasHovered != _isHovered) Invalidate();

            if (_isDragging)
            {
                UpdateValueFromX(e.X);
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (_isDragging)
            {
                _isDragging = false;
                Capture = false;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_isHovered)
            {
                _isHovered = false;
                Invalidate();
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            int step = (Control.ModifierKeys == Keys.Shift) ? 5 : 2;
            int delta = (e.Delta > 0) ? step : -step;
            Value += delta;
        }

        protected override bool IsInputKey(Keys keyData)
        {
            switch (keyData & Keys.KeyCode)
            {
                case Keys.Left:
                case Keys.Right:
                case Keys.Up:
                case Keys.Down:
                case Keys.Home:
                case Keys.End:
                    return true;
            }
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Down) Value -= 1;
            else if (e.KeyCode == Keys.Right || e.KeyCode == Keys.Up) Value += 1;
            else if (e.KeyCode == Keys.Home) Value = 0;
            else if (e.KeyCode == Keys.End) Value = 100;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // 1. Speaker Icon (centered at cx = 14, cy = Height / 2)
            float scx = 14f;
            float scy = Height / 2.0f;

            PointF[] conePts = new PointF[] {
                new PointF(scx - 8f, scy - 2.5f),
                new PointF(scx - 4.5f, scy - 2.5f),
                new PointF(scx, scy - 6.5f),
                new PointF(scx, scy + 6.5f),
                new PointF(scx - 4.5f, scy + 2.5f),
                new PointF(scx - 8f, scy + 2.5f)
            };
            using (var b = new SolidBrush(DarkTheme.TextColor))
            {
                g.FillPolygon(b, conePts);
            }

            using (var pen = new Pen(DarkTheme.TextColor, 1.3f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;

                if (_value == 0)
                {
                    g.DrawLine(pen, scx + 3f, scy - 3f, scx + 7f, scy + 3f);
                    g.DrawLine(pen, scx + 7f, scy - 3f, scx + 3f, scy + 3f);
                }
                else
                {
                    g.DrawArc(pen, scx + 1f, scy - 3f, 4f, 6f, -60, 120);
                    if (_value >= 33)
                    {
                        g.DrawArc(pen, scx + 3.5f, scy - 5.5f, 6f, 11f, -60, 120);
                    }
                    if (_value >= 66)
                    {
                        g.DrawArc(pen, scx + 6f, scy - 8f, 8f, 16f, -60, 120);
                    }
                }
            }

            // 2. Track Background (rx = 5, fill = #383838)
            Rectangle track = GetTrackBounds();
            using (var b = new SolidBrush(DarkTheme.TrackBg))
            using (var path = DarkTheme.CreateRoundedRect(track, 5))
            {
                g.FillPath(b, path);
            }

            // 3. Active Track Fill (rx = 5, fill = #EDEDED)
            float tx = GetThumbX();
            int fillWidth = (int)Math.Max(0, tx - track.X);
            if (fillWidth > 0)
            {
                Rectangle fillRect = new Rectangle(track.X, track.Y, fillWidth, track.Height);
                using (var b = new SolidBrush(DarkTheme.TextColor))
                using (var path = DarkTheme.CreateRoundedRect(fillRect, 5))
                {
                    g.FillPath(b, path);
                }
            }

            // 4. Thumb Drop Shadow
            RectangleF thumb = GetThumbBounds();
            RectangleF thumbShadow = new RectangleF(thumb.X, thumb.Y + 1.5f, thumb.Width, thumb.Height);
            using (var b = new SolidBrush(Color.FromArgb(70, 0, 0, 0)))
            {
                g.FillEllipse(b, thumbShadow);
            }

            // 5. Thumb Outer Circle (r = 7.5, fill = #FFFFFF, stroke = #CCCCCC 1px)
            using (var b = new SolidBrush(Color.White))
            using (var pen = new Pen(ColorTranslator.FromHtml("#CCCCCC"), 1f))
            {
                g.FillEllipse(b, thumb);
                g.DrawEllipse(pen, thumb);
            }

            // 6. Thumb Inner Dot (r = 3.6, fill = #EDEDED)
            float dotR = 3.6f;
            RectangleF dotRect = new RectangleF(tx - dotR, Height / 2.0f - dotR, dotR * 2, dotR * 2);
            using (var b = new SolidBrush(DarkTheme.AccentColor))
            {
                g.FillEllipse(b, dotRect);
            }
        }
    }

    #endregion

    #region Volume Readout Component (Centered, Unclipped Typography)

    internal class VolumeReadoutControl : Control
    {
        private int _percentage = 100;

        public int Percentage
        {
            get { return _percentage; }
            set
            {
                int clamped = Math.Max(0, Math.Min(100, value));
                if (_percentage != clamped)
                {
                    _percentage = clamped;
                    Invalidate();
                }
            }
        }

        public override string Text
        {
            get { return _percentage + "%"; }
            set
            {
                int val;
                if (!string.IsNullOrEmpty(value) && int.TryParse(value.Replace("%", "").Trim(), out val))
                {
                    Percentage = val;
                }
            }
        }

        public VolumeReadoutControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            BackColor = DarkTheme.BgColor;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            string pctStr = _percentage + "%";

            using (var fontPct = new Font("Segoe UI", 18f, FontStyle.Bold))
            using (var fontSub = new Font("Segoe UI", 9f, FontStyle.Regular))
            {
                Size szPct = TextRenderer.MeasureText(g, pctStr, fontPct, new Size(Width, 100), 
                    TextFormatFlags.NoPadding | TextFormatFlags.NoClipping);
                Size szSub = TextRenderer.MeasureText(g, "Target Volume", fontSub, new Size(Width, 100), 
                    TextFormatFlags.NoPadding | TextFormatFlags.NoClipping);

                int totalH = szPct.Height + 4 + szSub.Height;
                int startY = (Height - totalH) / 2 + 2;

                Rectangle rPct = new Rectangle(0, startY, Width, szPct.Height);
                TextRenderer.DrawText(g, pctStr, fontPct, rPct, DarkTheme.TextColor, 
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoClipping | TextFormatFlags.NoPadding);

                Rectangle rSub = new Rectangle(0, startY + szPct.Height + 4, Width, szSub.Height);
                TextRenderer.DrawText(g, "Target Volume", fontSub, rSub, DarkTheme.MutedColor, 
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoClipping | TextFormatFlags.NoPadding);
            }
        }
    }

    #endregion

    #region Slot Row Component (Dual-Division Row: Text | - | ⚙)

    internal class SlotRowControl : Control
    {
        public int SlotIndex { get; set; }
        public int Percentage { get; set; }
        public bool IsActiveVolume { get; set; }
        public bool CanRemove { get; set; }

        public event Action<int> VolumeClicked;
        public event Action<int> RemoveClicked;
        public event Action<int> ConfigureClicked;

        private bool _isRowHovered = false;
        private bool _isMinusHovered = false;
        private bool _isGearHovered = false;

        public SlotRowControl(int index, int percentage)
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);

            SlotIndex = index;
            Percentage = percentage;
            Height = 28;
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            bool mh = (e.X >= 158 && e.X < 188 && CanRemove);
            bool gh = (e.X >= 188);
            bool rh = (e.X < 158);

            if (mh != _isMinusHovered || gh != _isGearHovered || rh != _isRowHovered)
            {
                _isMinusHovered = mh;
                _isGearHovered = gh;
                _isRowHovered = rh;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_isMinusHovered || _isGearHovered || _isRowHovered)
            {
                _isMinusHovered = false;
                _isGearHovered = false;
                _isRowHovered = false;
                Invalidate();
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                if (e.X >= 188)
                {
                    if (ConfigureClicked != null) ConfigureClicked(SlotIndex);
                }
                else if (e.X >= 158)
                {
                    if (CanRemove && RemoveClicked != null) RemoveClicked(SlotIndex);
                }
                else
                {
                    if (VolumeClicked != null) VolumeClicked(Percentage);
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // 1. Row background
            if (IsActiveVolume)
            {
                Rectangle activeRc = new Rectangle(3, 1, Width - 6, Height - 2);
                using (var b = new SolidBrush(DarkTheme.ActiveRowBg))
                using (var path = DarkTheme.CreateRoundedRect(activeRc, 4))
                {
                    g.FillPath(b, path);
                }
            }
            else if (_isRowHovered)
            {
                Rectangle hoverRc = new Rectangle(2, 1, 155, Height - 2);
                using (var b = new SolidBrush(DarkTheme.TileHover))
                using (var path = DarkTheme.CreateRoundedRect(hoverRc, 4))
                {
                    g.FillPath(b, path);
                }
            }

            // 2. Checkmark (x = 20, cy = 14)
            if (IsActiveVolume)
            {
                DarkTheme.DrawVectorCheck(g, 20f, Height / 2.0f, DarkTheme.TextColor);
            }

            // 3. Text (x = 36, Segoe UI 9pt)
            string text = string.Format("{0}% Volume", Percentage);
            Rectangle textRect = new Rectangle(36, 0, 122, Height);
            using (var font = new Font("Segoe UI", 9f, IsActiveVolume ? FontStyle.Bold : FontStyle.Regular))
            {
                TextRenderer.DrawText(g, text, font, textRect, IsActiveVolume ? DarkTheme.TextActiveColor : DarkTheme.TextColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            }

            // 4. Divider 1 (x = 158, y = 6..22)
            using (var pen = new Pen(DarkTheme.BorderColor, 1f))
            {
                g.DrawLine(pen, 158, 6, 158, Height - 6);
            }

            // 5. Minus Button [-] (x = 158..188, cx = 173)
            if (_isMinusHovered && CanRemove)
            {
                Rectangle minusPill = new Rectangle(160, 2, 26, 24);
                using (var b = new SolidBrush(DarkTheme.DeleteHoverBg))
                using (var path = DarkTheme.CreateRoundedRect(minusPill, 4))
                {
                    g.FillPath(b, path);
                }
            }
            Color minusColor = CanRemove ? (_isMinusHovered ? DarkTheme.DeleteHoverColor : ColorTranslator.FromHtml("#B8B8B8")) : Color.FromArgb(60, 60, 60);
            DarkTheme.DrawVectorMinus(g, 173f, Height / 2.0f, minusColor);

            // 6. Divider 2 (x = 188, y = 6..22)
            using (var pen = new Pen(DarkTheme.BorderColor, 1f))
            {
                g.DrawLine(pen, 188, 6, 188, Height - 6);
            }

            // 7. Settings Gear Button [⚙] (x = 188..218, cx = 203)
            if (_isGearHovered)
            {
                Rectangle gearPill = new Rectangle(190, 2, 26, 24);
                using (var b = new SolidBrush(Color.FromArgb(20, 255, 255, 255)))
                using (var path = DarkTheme.CreateRoundedRect(gearPill, 4))
                {
                    g.FillPath(b, path);
                }
            }
            Color gearColor = _isGearHovered ? DarkTheme.TextActiveColor : ColorTranslator.FromHtml("#B8B8B8");
            DarkTheme.DrawVectorGear(g, new Rectangle(191, 2, 24, 24), gearColor);
        }
    }

    #endregion

    #region The Fixed-Size Windows 11 Flyout Form (Chrome-Style In-Menu Page Flip)

    internal class TrayFlyoutForm : Form
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_SHOWWINDOW = 0x0040;

        // Visual Layout Constants
        public const int FlyoutWidth = 220;
        public const int FlyoutHeight = 242; // Fixed height matching 5 slots exactly!
        public const int ViewportHeight = 140; // 5 slots * 28px = 140px

        private readonly QuickVolumeConfig _config;
        private readonly Action _onConfigChanged;
        private readonly Action _onExitAppRequested;

        // View Panels
        private Panel _pnlPresets;
        private Panel _pnlConfigure;

        // Presets View Controls
        private Panel _slotsViewport;
        private Panel _slotsCanvas;
        private Button _btnHeaderPlus;
        private Button _btnStartup;
        private Button _btnClose;
        private int _scrollOffset = 0;

        // Configure View Controls
        private Button _btnBack;
        private Button _btnExit;
        private VolumeReadoutControl _lblPercentage;
        private DarkSlider _slider;
        private Button _btnReset;
        private Button _btnCancel;
        private Button _btnSave;
        private int _editingSlotIndex = -1;

        public TrayFlyoutForm(QuickVolumeConfig config, Action onConfigChanged, Action onExitAppRequested)
        {
            _config = config;
            _onConfigChanged = onConfigChanged;
            _onExitAppRequested = onExitAppRequested;

            InitializeFlyoutWindow();
            BuildPresetsView();
            BuildConfigureView();

            SwitchToPresetsView();
        }

        private void InitializeFlyoutWindow()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Size = new Size(FlyoutWidth, FlyoutHeight);
            BackColor = DarkTheme.BgColor;
            ShowInTaskbar = false;
            TopMost = true;
            DoubleBuffered = true;
            Font = new Font("Segoe UI", 9f);

            // Enable DWM rounded corners for Windows 11
            try
            {
                int val = 2; // DWMWCP_ROUND
                DwmSetWindowAttribute(Handle, 33, ref val, sizeof(int));
            }
            catch {}
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ClassStyle |= 0x00020000; // CS_DROPSHADOW
                cp.ExStyle |= 0x00000008;    // WS_EX_TOPMOST
                return cp;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Outer 1px acrylic border
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var pen = new Pen(DarkTheme.BorderColor, 1f))
            using (var path = DarkTheme.CreateRoundedRect(r, 8))
            {
                g.DrawPath(pen, path);
            }
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            base.OnPaintBackground(e);
            // Windows 11 Acrylic Surface Gradient (#1F1F1F to #1C1C1C)
            using (var brush = new LinearGradientBrush(ClientRectangle, DarkTheme.BgColor, DarkTheme.BgGradientBottom, 90f))
            {
                e.Graphics.FillRectangle(brush, ClientRectangle);
            }
        }

        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            Hide();
            SwitchToPresetsView();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape)
            {
                if (_pnlConfigure.Visible)
                {
                    SwitchToPresetsView();
                }
                else
                {
                    Hide();
                }
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_MOUSEWHEEL = 0x020A;
            if (m.Msg == WM_MOUSEWHEEL && _pnlPresets != null && _pnlPresets.Visible)
            {
                short delta = (short)((m.WParam.ToInt64() >> 16) & 0xFFFF);
                Point pt = PointToClient(Cursor.Position);
                if (_slotsViewport != null && _slotsViewport.Bounds.Contains(pt))
                {
                    ScrollSlots(delta);
                    return;
                }
            }
            base.WndProc(ref m);
        }

        public void ShowFlyout(Point mousePos)
        {
            SwitchToPresetsView();
            RebuildSlotRows();
            UpdateStartupButton();

            // Position above system tray icon
            Screen scr = Screen.FromPoint(mousePos);
            int x = Math.Max(scr.WorkingArea.Left + 8, Math.Min(scr.WorkingArea.Right - Width - 8, mousePos.X - Width / 2));
            int y = scr.WorkingArea.Bottom - Height - 8;

            Location = new Point(x, y);

            // Force topmost Z-order and claim foreground to dismiss Explorer tray overflow
            Show();
            SetWindowPos(Handle, HWND_TOPMOST, x, y, Width, Height, SWP_SHOWWINDOW);
            BringToFront();
            Activate();
            SetForegroundWindow(Handle);
        }

        #region Presets View Construction & Logic

        private void BuildPresetsView()
        {
            _pnlPresets = new Panel()
            {
                Location = new Point(1, 1),
                Size = new Size(FlyoutWidth - 2, FlyoutHeight - 2),
                BackColor = DarkTheme.BgColor
            };

            // 1. Header (y: 0..32, PRESETS [+] with vertical hairline divider at x = 188)
            Panel pnlHeader = new Panel()
            {
                Location = new Point(0, 0),
                Size = new Size(FlyoutWidth - 2, 32),
                BackColor = DarkTheme.BgColor
            };
            pnlHeader.Paint += (s, e) =>
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                // "PRESETS" title
                using (var f = new Font("Segoe UI", 7.5f, FontStyle.Bold))
                using (var b = new SolidBrush(DarkTheme.MutedColor))
                {
                    g.DrawString("PRESETS", f, b, 16, 9);
                }

                // Vertical hairline divider between PRESETS and "+" (x = 188, y = 0..32)
                using (var pen = new Pen(DarkTheme.BorderColor, 1f))
                {
                    g.DrawLine(pen, 188, 0, 188, 32);
                    // Horizontal header bottom boundary (y = 32)
                    g.DrawLine(pen, 0, 31, FlyoutWidth - 2, 31);
                }
            };

            // "+" Add Preset Button (x: 188..218, centered at cx = 203, cy = 16)
            _btnHeaderPlus = new Button()
            {
                Location = new Point(188, 0),
                Size = new Size(FlyoutWidth - 2 - 188, 31),
                FlatStyle = FlatStyle.Flat,
                BackColor = DarkTheme.BgColor,
                Cursor = Cursors.Hand
            };
            _btnHeaderPlus.FlatAppearance.BorderSize = 0;
            _btnHeaderPlus.FlatAppearance.MouseOverBackColor = DarkTheme.BgColor;
            _btnHeaderPlus.FlatAppearance.MouseDownBackColor = DarkTheme.BgColor;
            bool isPlusHovered = false;
            _btnHeaderPlus.MouseEnter += (s, e) => { isPlusHovered = true; _btnHeaderPlus.Invalidate(); };
            _btnHeaderPlus.MouseLeave += (s, e) => { isPlusHovered = false; _btnHeaderPlus.Invalidate(); };
            _btnHeaderPlus.Paint += (s, e) =>
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                if (isPlusHovered && _btnHeaderPlus.Enabled)
                {
                    Rectangle pill = new Rectangle(2, 3, _btnHeaderPlus.Width - 4, _btnHeaderPlus.Height - 6);
                    using (var b = new SolidBrush(Color.FromArgb(20, 255, 255, 255)))
                    using (var path = DarkTheme.CreateRoundedRect(pill, 4))
                    {
                        g.FillPath(b, path);
                    }
                }

                Color plusColor = _btnHeaderPlus.Enabled ? (isPlusHovered ? Color.White : ColorTranslator.FromHtml("#EDEDED")) : Color.FromArgb(70, 70, 70);
                DarkTheme.DrawVectorPlus(g, _btnHeaderPlus.Width / 2.0f, _btnHeaderPlus.Height / 2.0f, plusColor);
            };
            _btnHeaderPlus.Click += (s, e) => AddSlotAtTop();
            pnlHeader.Controls.Add(_btnHeaderPlus);

            _pnlPresets.Controls.Add(pnlHeader);

            // 2. Presets Viewport (Strictly y: 32..172, height: 140px = 5 slots * 28px)
            _slotsViewport = new Panel()
            {
                Location = new Point(0, 32),
                Size = new Size(FlyoutWidth - 2, ViewportHeight),
                BackColor = DarkTheme.BgColor
            };
            _slotsViewport.Paint += (s, e) =>
            {
                // Draw thin Windows 11 scrollbar if > 5 presets
                if (_config.Slots.Count > 5)
                {
                    Graphics g = e.Graphics;
                    g.SmoothingMode = SmoothingMode.AntiAlias;

                    int totalH = _config.Slots.Count * 28;
                    float trackLen = ViewportHeight - 6;
                    float thumbHeight = Math.Max(24f, (ViewportHeight / (float)totalH) * trackLen);
                    float thumbTravel = trackLen - thumbHeight;
                    int maxScroll = totalH - ViewportHeight;
                    float progress = maxScroll > 0 ? ((float)_scrollOffset / maxScroll) : 0f;
                    float thumbY = 3 + progress * thumbTravel;

                    RectangleF thumbRect = new RectangleF(FlyoutWidth - 2 - 5, thumbY, 2.5f, thumbHeight);
                    using (var b = new SolidBrush(DarkTheme.MutedColor))
                    using (var path = DarkTheme.CreateRoundedRect(new Rectangle((int)thumbRect.X, (int)thumbRect.Y, 3, (int)thumbRect.Height), 1))
                    {
                        g.FillPath(b, path);
                    }
                }
            };
            _slotsViewport.MouseWheel += OnSlotsViewportMouseWheel;

            _slotsCanvas = new Panel()
            {
                Location = new Point(0, 0),
                Size = new Size(FlyoutWidth - 2, ViewportHeight),
                BackColor = DarkTheme.BgColor
            };
            _slotsCanvas.MouseWheel += OnSlotsViewportMouseWheel;
            _slotsViewport.Controls.Add(_slotsCanvas);

            _pnlPresets.Controls.Add(_slotsViewport);

            // 3. Primary Boundary Line above "Start with Windows" (y = 172)
            Panel sep1 = new Panel()
            {
                Location = new Point(0, 172),
                Size = new Size(FlyoutWidth - 2, 1),
                BackColor = DarkTheme.BorderColor
            };
            _pnlPresets.Controls.Add(sep1);

            // 4. "Start with Windows" Row (y: 173..206, height 33px)
            _btnStartup = new Button()
            {
                Location = new Point(0, 173),
                Size = new Size(FlyoutWidth - 2, 33),
                FlatStyle = FlatStyle.Flat,
                BackColor = DarkTheme.BgColor,
                Cursor = Cursors.Hand
            };
            _btnStartup.FlatAppearance.BorderSize = 0;
            _btnStartup.FlatAppearance.MouseOverBackColor = DarkTheme.BgColor;
            _btnStartup.FlatAppearance.MouseDownBackColor = DarkTheme.BgColor;
            bool isStartupHovered = false;
            _btnStartup.MouseEnter += (s, e) => { isStartupHovered = true; _btnStartup.Invalidate(); };
            _btnStartup.MouseLeave += (s, e) => { isStartupHovered = false; _btnStartup.Invalidate(); };
            _btnStartup.Paint += (s, e) =>
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                if (isStartupHovered)
                {
                    Rectangle pill = new Rectangle(2, 2, _btnStartup.Width - 4, _btnStartup.Height - 4);
                    using (var b = new SolidBrush(DarkTheme.TileHover))
                    using (var path = DarkTheme.CreateRoundedRect(pill, 4))
                    {
                        g.FillPath(b, path);
                    }
                }

                if (StartupManager.IsStartupEnabled())
                {
                    DarkTheme.DrawVectorCheck(g, 20f, _btnStartup.Height / 2.0f, DarkTheme.TextColor);
                }

                Rectangle textRect = new Rectangle(36, 0, _btnStartup.Width - 40, _btnStartup.Height);
                TextRenderer.DrawText(g, "Start with Windows", Font, textRect, DarkTheme.TextColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            };
            _btnStartup.Click += (s, e) =>
            {
                bool newState = !StartupManager.IsStartupEnabled();
                StartupManager.SetStartup(newState);
                UpdateStartupButton();
            };
            _pnlPresets.Controls.Add(_btnStartup);

            // 5. Secondary Divider Line (y = 206)
            Panel sep2 = new Panel()
            {
                Location = new Point(8, 206),
                Size = new Size(FlyoutWidth - 2 - 16, 1),
                BackColor = DarkTheme.DividerSecondary
            };
            _pnlPresets.Controls.Add(sep2);

            // 6. "Close" Row (y: 207..241, height 34px)
            _btnClose = new Button()
            {
                Location = new Point(0, 207),
                Size = new Size(FlyoutWidth - 2, 33),
                FlatStyle = FlatStyle.Flat,
                BackColor = DarkTheme.BgColor,
                Cursor = Cursors.Hand
            };
            _btnClose.FlatAppearance.BorderSize = 0;
            _btnClose.FlatAppearance.MouseOverBackColor = DarkTheme.BgColor;
            _btnClose.FlatAppearance.MouseDownBackColor = DarkTheme.BgColor;
            bool isCloseHovered = false;
            _btnClose.MouseEnter += (s, e) => { isCloseHovered = true; _btnClose.Invalidate(); };
            _btnClose.MouseLeave += (s, e) => { isCloseHovered = false; _btnClose.Invalidate(); };
            _btnClose.Paint += (s, e) =>
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                if (isCloseHovered)
                {
                    Rectangle pill = new Rectangle(2, 2, _btnClose.Width - 4, _btnClose.Height - 4);
                    using (var b = new SolidBrush(DarkTheme.TileHover))
                    using (var path = DarkTheme.CreateRoundedRect(pill, 4))
                    {
                        g.FillPath(b, path);
                    }
                }

                Rectangle textRect = new Rectangle(36, 0, _btnClose.Width - 40, _btnClose.Height);
                TextRenderer.DrawText(g, "Close", Font, textRect, DarkTheme.TextColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            };
            _btnClose.Click += (s, e) =>
            {
                Hide();
                if (_onExitAppRequested != null) _onExitAppRequested();
            };
            _pnlPresets.Controls.Add(_btnClose);

            Controls.Add(_pnlPresets);
        }

        private void OnSlotsViewportMouseWheel(object sender, MouseEventArgs e)
        {
            ScrollSlots(e.Delta);
        }

        private void ScrollSlots(int delta)
        {
            if (_config.Slots.Count <= 5) return;
            int totalH = _config.Slots.Count * 28;
            int maxScroll = totalH - ViewportHeight;

            int step = 28;
            if (delta > 0) _scrollOffset = Math.Max(0, _scrollOffset - step);
            else _scrollOffset = Math.Min(maxScroll, _scrollOffset + step);

            _slotsCanvas.Location = new Point(0, -_scrollOffset);
            _slotsViewport.Invalidate();
        }

        public void RebuildSlotRows()
        {
            _slotsCanvas.Controls.Clear();
            _btnHeaderPlus.Enabled = (_config.Slots.Count < 10);
            _btnHeaderPlus.Invalidate();

            float currentVol = AudioManager.GetMasterVolume();
            int currentPct = (currentVol >= 0f) ? (int)Math.Round(currentVol * 100.0f) : -1;

            int count = _config.Slots.Count;
            bool isScrollable = count > 5;
            int totalH = isScrollable ? (count * 28) : ViewportHeight;
            _slotsCanvas.Size = new Size(FlyoutWidth - 2, totalH);

            int maxScroll = Math.Max(0, totalH - ViewportHeight);
            if (_scrollOffset > maxScroll) _scrollOffset = maxScroll;
            _slotsCanvas.Location = new Point(0, isScrollable ? -_scrollOffset : 0);

            // Argon Web Baseline Stacking & Gravity:
            // When count <= 5: Bottom-anchored directly resting flush on bottom boundary (y = 140)
            // When count > 5: Stacked from top (y = 0) downwards and scrolled via _scrollOffset
            for (int i = 0; i < count; i++)
            {
                int slotIdx = i;
                int pct = _config.Slots[i];
                int rowY = isScrollable
                    ? (i * 28)
                    : (ViewportHeight - (count - i) * 28);

                var row = new SlotRowControl(slotIdx, pct)
                {
                    Width = FlyoutWidth - 2,
                    Location = new Point(0, rowY),
                    CanRemove = (count > 1),
                    IsActiveVolume = (currentPct >= 0 && Math.Abs(currentPct - pct) <= 2)
                };

                row.MouseWheel += OnSlotsViewportMouseWheel;

                row.VolumeClicked += (val) =>
                {
                    AudioManager.SetMasterVolume(val / 100.0f);
                    Hide();
                };

                row.RemoveClicked += (idx) =>
                {
                    RemoveSlotAt(idx);
                };

                row.ConfigureClicked += (idx) =>
                {
                    OpenConfigureView(idx);
                };

                _slotsCanvas.Controls.Add(row);
            }

            _slotsViewport.Invalidate();
        }

        public void UpdateActiveStates()
        {
            float currentVol = AudioManager.GetMasterVolume();
            int currentPct = (currentVol >= 0f) ? (int)Math.Round(currentVol * 100.0f) : -1;

            foreach (Control c in _slotsCanvas.Controls)
            {
                var row = c as SlotRowControl;
                if (row != null)
                {
                    row.IsActiveVolume = (currentPct >= 0 && Math.Abs(currentPct - row.Percentage) <= 2);
                    row.Invalidate();
                }
            }
        }

        private void UpdateStartupButton()
        {
            _btnStartup.Invalidate();
        }

        private void AddSlotAtTop()
        {
            if (_config.Slots.Count >= 10) return;

            // Every new slot starts with 0%
            _config.Slots.Insert(0, 0);
            SettingsManager.Save(_config);

            if (_config.Slots.Count > 5)
            {
                _scrollOffset = 0;
            }
            RebuildSlotRows();
            if (_onConfigChanged != null) _onConfigChanged();
        }

        private void RemoveSlotAt(int slotIndex)
        {
            if (_config.Slots.Count <= 1) return;
            if (slotIndex < 0 || slotIndex >= _config.Slots.Count) return;

            // Stacking gravity: remove and collapse
            _config.Slots.RemoveAt(slotIndex);
            SettingsManager.Save(_config);
            RebuildSlotRows();
            if (_onConfigChanged != null) _onConfigChanged();
        }

        #endregion

        #region Configure View Construction & Logic (Chrome-Style In-Menu Drill-Down)

        private void BuildConfigureView()
        {
            _pnlConfigure = new Panel()
            {
                Location = new Point(1, 1),
                Size = new Size(FlyoutWidth - 2, FlyoutHeight - 2),
                BackColor = DarkTheme.BgColor,
                Visible = false
            };

            // 1. Header Navigation Bar (y: 0..38)
            Panel pnlHeader = new Panel()
            {
                Location = new Point(0, 0),
                Size = new Size(FlyoutWidth - 2, 38),
                BackColor = DarkTheme.BgColor
            };
            pnlHeader.Paint += (s, e) =>
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                // Header horizontal divider line at y = 37.5
                using (var pen = new Pen(DarkTheme.BorderColor, 1f))
                {
                    g.DrawLine(pen, 1, 37, FlyoutWidth - 2, 37);
                }
            };

            // Back Button (x = 8, y = 6, width = 62, height = 24)
            _btnBack = new Button()
            {
                Location = new Point(8, 6),
                Size = new Size(62, 24),
                FlatStyle = FlatStyle.Flat,
                BackColor = DarkTheme.BgColor,
                Cursor = Cursors.Hand
            };
            _btnBack.FlatAppearance.BorderSize = 0;
            _btnBack.FlatAppearance.MouseOverBackColor = DarkTheme.BgColor;
            _btnBack.FlatAppearance.MouseDownBackColor = DarkTheme.BgColor;
            bool isBackHovered = false;
            _btnBack.MouseEnter += (s, e) => { isBackHovered = true; _btnBack.Invalidate(); };
            _btnBack.MouseLeave += (s, e) => { isBackHovered = false; _btnBack.Invalidate(); };
            _btnBack.Paint += (s, e) =>
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                if (isBackHovered)
                {
                    Rectangle pill = new Rectangle(0, 0, _btnBack.Width, _btnBack.Height);
                    using (var b = new SolidBrush(Color.FromArgb(20, 255, 255, 255)))
                    using (var path = DarkTheme.CreateRoundedRect(pill, 4))
                    {
                        g.FillPath(b, path);
                    }
                }

                // Arrow M22 14 L17.5 18 L22 22
                using (var pen = new Pen(DarkTheme.TextColor, 1.3f))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    pen.LineJoin = LineJoin.Round;
                    g.DrawLines(pen, new PointF[] {
                        new PointF(14f, 7f),
                        new PointF(9.5f, 12f),
                        new PointF(14f, 17f)
                    });
                }

                Rectangle textRect = new Rectangle(18, 0, _btnBack.Width - 18, _btnBack.Height);
                TextRenderer.DrawText(g, "Back", Font, textRect, DarkTheme.TextColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            };
            _btnBack.Click += (s, e) => SwitchToPresetsView();
            pnlHeader.Controls.Add(_btnBack);

            // Close "✕" Button (x = 186, y = 6, width = 26, height = 24)
            _btnExit = new Button()
            {
                Location = new Point(FlyoutWidth - 2 - 32, 6),
                Size = new Size(26, 24),
                FlatStyle = FlatStyle.Flat,
                BackColor = DarkTheme.BgColor,
                Cursor = Cursors.Hand
            };
            _btnExit.FlatAppearance.BorderSize = 0;
            _btnExit.FlatAppearance.MouseOverBackColor = DarkTheme.BgColor;
            _btnExit.FlatAppearance.MouseDownBackColor = DarkTheme.BgColor;
            bool isExitHovered = false;
            _btnExit.MouseEnter += (s, e) => { isExitHovered = true; _btnExit.Invalidate(); };
            _btnExit.MouseLeave += (s, e) => { isExitHovered = false; _btnExit.Invalidate(); };
            _btnExit.Paint += (s, e) =>
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                if (isExitHovered)
                {
                    Rectangle pill = new Rectangle(0, 0, _btnExit.Width, _btnExit.Height);
                    using (var b = new SolidBrush(Color.FromArgb(196, 43, 28)))
                    using (var path = DarkTheme.CreateRoundedRect(pill, 4))
                    {
                        g.FillPath(b, path);
                    }
                }

                DarkTheme.DrawVectorClose(g, _btnExit.Width / 2.0f, _btnExit.Height / 2.0f, isExitHovered ? Color.White : DarkTheme.TextColor);
            };
            _btnExit.Click += (s, e) => Hide();
            pnlHeader.Controls.Add(_btnExit);

            _pnlConfigure.Controls.Add(pnlHeader);

            // 2. Percentage Readout (Centered in space between header and slider, unclipped 18pt bold)
            _lblPercentage = new VolumeReadoutControl()
            {
                Location = new Point(0, 38),
                Size = new Size(FlyoutWidth - 2, 78),
                Percentage = 100,
                BackColor = DarkTheme.BgColor
            };
            _pnlConfigure.Controls.Add(_lblPercentage);

            // 3. Fluent Volume Slider with Speaker Icon (y = 116..158)
            _slider = new DarkSlider()
            {
                Location = new Point(12, 116),
                Size = new Size(FlyoutWidth - 2 - 24, 42),
                Value = 100,
                BackColor = DarkTheme.BgColor
            };
            _slider.ValueChanged += (s, e) =>
            {
                _lblPercentage.Percentage = _slider.Value;
            };
            _pnlConfigure.Controls.Add(_slider);

            // 4. Pill-Shaped Action Buttons (y = 186, height = 32, rx = 16)
            // Reset Button (x = 14, width = 56, height = 32, rx = 16)
            _btnReset = new Button()
            {
                Location = new Point(14, 186),
                Size = new Size(56, 32),
                FlatStyle = FlatStyle.Flat,
                BackColor = DarkTheme.BgColor,
                Cursor = Cursors.Hand
            };
            _btnReset.FlatAppearance.BorderSize = 0;
            _btnReset.FlatAppearance.MouseOverBackColor = DarkTheme.BgColor;
            _btnReset.FlatAppearance.MouseDownBackColor = DarkTheme.BgColor;
            bool isResetHovered = false;
            _btnReset.MouseEnter += (s, e) => { isResetHovered = true; _btnReset.Invalidate(); };
            _btnReset.MouseLeave += (s, e) => { isResetHovered = false; _btnReset.Invalidate(); };
            _btnReset.Paint += (s, e) =>
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                Rectangle r = new Rectangle(0, 0, _btnReset.Width - 1, _btnReset.Height - 1);
                using (var b = new SolidBrush(isResetHovered ? Color.FromArgb(20, 255, 255, 255) : Color.FromArgb(8, 255, 255, 255)))
                using (var pen = new Pen(isResetHovered ? ColorTranslator.FromHtml("#555555") : DarkTheme.TrackBg, 1f))
                using (var path = DarkTheme.CreateRoundedRect(r, 15))
                {
                    g.FillPath(b, path);
                    g.DrawPath(pen, path);
                }

                using (var font = new Font("Segoe UI", 8.5f))
                {
                    TextRenderer.DrawText(g, "Reset", font, _btnReset.ClientRectangle, DarkTheme.AccentColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
            };
            _btnReset.Click += (s, e) =>
            {
                int defaultVal = 0;
                if (_editingSlotIndex >= 0 && _editingSlotIndex < SettingsManager.DefaultSlots.Length)
                {
                    defaultVal = SettingsManager.DefaultSlots[_editingSlotIndex];
                }
                _slider.Value = defaultVal;
            };
            _pnlConfigure.Controls.Add(_btnReset);

            // Cancel Button (x = 76, width = 60, height = 32, rx = 16)
            _btnCancel = new Button()
            {
                Location = new Point(76, 186),
                Size = new Size(60, 32),
                FlatStyle = FlatStyle.Flat,
                BackColor = DarkTheme.BgColor,
                Cursor = Cursors.Hand
            };
            _btnCancel.FlatAppearance.BorderSize = 0;
            _btnCancel.FlatAppearance.MouseOverBackColor = DarkTheme.BgColor;
            _btnCancel.FlatAppearance.MouseDownBackColor = DarkTheme.BgColor;
            bool isCancelHovered = false;
            _btnCancel.MouseEnter += (s, e) => { isCancelHovered = true; _btnCancel.Invalidate(); };
            _btnCancel.MouseLeave += (s, e) => { isCancelHovered = false; _btnCancel.Invalidate(); };
            _btnCancel.Paint += (s, e) =>
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                Rectangle r = new Rectangle(0, 0, _btnCancel.Width - 1, _btnCancel.Height - 1);
                using (var b = new SolidBrush(isCancelHovered ? Color.FromArgb(20, 255, 255, 255) : Color.FromArgb(8, 255, 255, 255)))
                using (var pen = new Pen(isCancelHovered ? ColorTranslator.FromHtml("#555555") : DarkTheme.TrackBg, 1f))
                using (var path = DarkTheme.CreateRoundedRect(r, 15))
                {
                    g.FillPath(b, path);
                    g.DrawPath(pen, path);
                }

                using (var font = new Font("Segoe UI", 8.5f))
                {
                    TextRenderer.DrawText(g, "Cancel", font, _btnCancel.ClientRectangle, DarkTheme.TextColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
            };
            _btnCancel.Click += (s, e) => SwitchToPresetsView();
            _pnlConfigure.Controls.Add(_btnCancel);

            // Save Button (x = 142, width = 64, height = 32, rx = 16, solid fill = #EDEDED, font-weight 600, text = #1C1C1C)
            _btnSave = new Button()
            {
                Location = new Point(142, 186),
                Size = new Size(64, 32),
                FlatStyle = FlatStyle.Flat,
                BackColor = DarkTheme.BgColor,
                Cursor = Cursors.Hand
            };
            _btnSave.FlatAppearance.BorderSize = 0;
            _btnSave.FlatAppearance.MouseOverBackColor = DarkTheme.BgColor;
            _btnSave.FlatAppearance.MouseDownBackColor = DarkTheme.BgColor;
            bool isSaveHovered = false;
            _btnSave.MouseEnter += (s, e) => { isSaveHovered = true; _btnSave.Invalidate(); };
            _btnSave.MouseLeave += (s, e) => { isSaveHovered = false; _btnSave.Invalidate(); };
            _btnSave.Paint += (s, e) =>
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                Rectangle r = new Rectangle(0, 0, _btnSave.Width - 1, _btnSave.Height - 1);
                using (var b = new SolidBrush(isSaveHovered ? Color.White : DarkTheme.TextColor))
                using (var path = DarkTheme.CreateRoundedRect(r, 15))
                {
                    g.FillPath(b, path);
                }

                using (var font = new Font("Segoe UI", 8.5f, FontStyle.Bold))
                {
                    TextRenderer.DrawText(g, "Save", font, _btnSave.ClientRectangle, DarkTheme.BgGradientBottom, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
            };
            _btnSave.Click += (s, e) =>
            {
                if (_editingSlotIndex >= 0 && _editingSlotIndex < _config.Slots.Count)
                {
                    _config.Slots[_editingSlotIndex] = _slider.Value;
                    SettingsManager.Save(_config);
                    if (_onConfigChanged != null) _onConfigChanged();
                }
                SwitchToPresetsView();
            };
            _pnlConfigure.Controls.Add(_btnSave);

            Controls.Add(_pnlConfigure);
        }

        private void OpenConfigureView(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _config.Slots.Count) return;

            _editingSlotIndex = slotIndex;
            int currentVal = _config.Slots[slotIndex];
            _slider.Value = currentVal;
            _lblPercentage.Percentage = currentVal;

            _pnlPresets.Visible = false;
            _pnlConfigure.Visible = true;
            SetWindowPos(Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
            BringToFront();
            SetForegroundWindow(Handle);
            _slider.Focus();
        }

        public void SwitchToPresetsView()
        {
            _pnlConfigure.Visible = false;
            _pnlPresets.Visible = true;
            RebuildSlotRows();
        }

        #endregion
    }

    #endregion

    #region Application Context & Main Entry

    internal class QuickVolumeAppContext : ApplicationContext
    {
        private const int WM_CLOSE = 0x0010;
        private const string CloseMessageName = "QuickVolume_Shutdown_Message";
        private const string CloseEventName = @"Global\QuickVolumeTrayApp_CloseEvent";

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int RegisterWindowMessage(string lpString);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        private readonly NotifyIcon _trayIcon;
        private readonly TrayFlyoutForm _flyout;
        private readonly System.Windows.Forms.Timer _pollTimer;

        private readonly HostMessageWindow _hostWindow;
        private readonly EventWaitHandle _closeEvent;
        private readonly SynchronizationContext _syncContext;

        private QuickVolumeConfig _config;

        public QuickVolumeAppContext()
        {
            _syncContext = SynchronizationContext.Current ?? new SynchronizationContext();

            // Load configuration
            _config = SettingsManager.Load();

            // Build Windows 11 Flyout Form
            _flyout = new TrayFlyoutForm(
                _config,
                () => UpdateTooltip(AudioManager.GetMasterVolume()),
                () => ExitApplication()
            );

            // Set up System Tray Icon
            _trayIcon = new NotifyIcon();
            _trayIcon.Icon = LoadAppIcon();
            _trayIcon.MouseClick += OnTrayMouseClick;
            _trayIcon.Visible = true;

            UpdateTooltip(AudioManager.GetMasterVolume());

            // Background poll timer to synchronize tooltip and active checkmarks
            _pollTimer = new System.Windows.Forms.Timer();
            _pollTimer.Interval = 1000;
            _pollTimer.Tick += (s, e) =>
            {
                float vol = AudioManager.GetMasterVolume();
                UpdateTooltip(vol);
                if (_flyout != null && _flyout.Visible)
                {
                    _flyout.UpdateActiveStates();
                }
            };
            _pollTimer.Start();

            // IPC receiver window for graceful --close signaling
            _hostWindow = new HostMessageWindow(ExitApplication);

            try
            {
                _closeEvent = new EventWaitHandle(false, EventResetMode.AutoReset, CloseEventName);
                _closeEvent.Reset();
                ThreadPool.RegisterWaitForSingleObject(_closeEvent, (state, timedOut) =>
                {
                    if (!timedOut)
                    {
                        _syncContext.Post(_ => ExitApplication(), null);
                    }
                }, null, -1, false);
            }
            catch {}
        }

        private void OnTrayMouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                // Left-click cycles through configured presets
                float newVol = AudioManager.CycleMasterVolume(_config.Slots);
                UpdateTooltip(newVol);
                if (_flyout != null && _flyout.Visible)
                {
                    _flyout.UpdateActiveStates();
                }
            }
            else if (e.Button == MouseButtons.Right)
            {
                // Right-click opens the fixed-size Windows 11 flyout right above tray icon!
                if (_flyout != null)
                {
                    _flyout.ShowFlyout(Cursor.Position);
                }
            }
        }

        private void UpdateTooltip(float volume)
        {
            if (_trayIcon == null) return;
            if (volume < 0f)
            {
                _trayIcon.Text = "Quick Volume";
                return;
            }
            int pct = (int)Math.Round(volume * 100.0f);
            if (pct < 0) pct = 0;
            if (pct > 100) pct = 100;
            _trayIcon.Text = string.Format("Quick Volume ({0}%)", pct);
        }

        private void ExitApplication()
        {
            if (_pollTimer != null)
            {
                _pollTimer.Stop();
                _pollTimer.Dispose();
            }
            if (_flyout != null)
            {
                _flyout.Close();
                _flyout.Dispose();
            }
            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
            }
            if (_hostWindow != null)
            {
                _hostWindow.DestroyHandle();
            }
            if (_closeEvent != null)
            {
                _closeEvent.Close();
            }
            ExitThread();
        }

        private static Icon LoadAppIcon()
        {
            try
            {
                Icon exeIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                if (exeIcon != null) return exeIcon;
            }
            catch {}

            try
            {
                string appDir = AppDomain.CurrentDomain.BaseDirectory;
                string[] candidates = new string[]
                {
                    Path.Combine(appDir, "assets\\quickvolume.ico"),
                    Path.Combine(appDir, "..\\assets\\quickvolume.ico"),
                    Path.Combine(appDir, "QuickVolume.ico")
                };
                foreach (var path in candidates)
                {
                    if (File.Exists(path)) return new Icon(path);
                }
            }
            catch {}

            return CreateProceduralIcon();
        }

        private static Icon CreateProceduralIcon()
        {
            using (var bmp = new Bitmap(32, 32))
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);

                // Speaker cone
                using (var brush = new SolidBrush(Color.White))
                {
                    g.FillRectangle(brush, 4, 11, 7, 10);
                    Point[] poly = new Point[] {
                        new Point(11, 11),
                        new Point(19, 5),
                        new Point(19, 27),
                        new Point(11, 21)
                    };
                    g.FillPolygon(brush, poly);
                }

                // Windows 11 Warm Silver / Monochrome sound waves
                using (var pen = new Pen(Color.FromArgb(237, 237, 237), 2.2f))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    g.DrawArc(pen, 16, 9, 8, 14, -45, 90);
                    g.DrawArc(pen, 20, 5, 8, 22, -45, 90);
                }

                IntPtr hIcon = bmp.GetHicon();
                return (Icon)Icon.FromHandle(hIcon).Clone();
            }
        }

        private class HostMessageWindow : NativeWindow
        {
            private readonly Action _onCloseRequested;
            private readonly int _shutdownMsg;

            public HostMessageWindow(Action onCloseRequested)
            {
                _onCloseRequested = onCloseRequested;
                _shutdownMsg = RegisterWindowMessage(CloseMessageName);

                CreateParams cp = new CreateParams();
                cp.Caption = "QuickVolume_Hidden_Host";
                CreateHandle(cp);
            }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == _shutdownMsg || m.Msg == WM_CLOSE)
                {
                    _onCloseRequested();
                    return;
                }
                base.WndProc(ref m);
            }
        }
    }

    internal static class Program
    {
        private const string MutexName = @"Global\QuickVolumeTrayApp_SingleInstance";
        private const string CloseEventName = @"Global\QuickVolumeTrayApp_CloseEvent";
        private const string CloseMessageName = "QuickVolume_Shutdown_Message";

        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int RegisterWindowMessage(string lpString);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", EntryPoint = "FindWindow", CharSet = CharSet.Auto)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [STAThread]
        private static void Main(string[] args)
        {
            try { SetProcessDPIAware(); } catch {}

            // Handle CLI operations first (< 20ms execution)
            if (args != null && args.Length > 0)
            {
                string arg = args[0].ToLowerInvariant().Trim();

                if (arg == "--toggle" || arg == "-toggle" || arg == "/toggle" || arg == "--next" || arg == "-next")
                {
                    var cfg = SettingsManager.Load();
                    AudioManager.CycleMasterVolume(cfg.Slots);
                    return;
                }

                if (arg == "--set" || arg == "-set" || arg == "/set")
                {
                    if (args.Length > 1)
                    {
                        int targetPct;
                        if (int.TryParse(args[1], out targetPct))
                        {
                            AudioManager.SetMasterVolume(targetPct / 100.0f);
                        }
                    }
                    return;
                }

                if (arg == "--25" || arg == "-25" || arg == "/25")
                {
                    AudioManager.SetMasterVolume(0.25f);
                    return;
                }

                if (arg == "--100" || arg == "-100" || arg == "/100")
                {
                    AudioManager.SetMasterVolume(1.00f);
                    return;
                }

                if (arg == "--close" || arg == "-close" || arg == "/close" || arg == "--exit" || arg == "-exit")
                {
                    SignalClose();
                    return;
                }
            }

            // Normal launch: run system tray application under single-instance protection
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            bool createdNew = false;
            Mutex mutex = null;
            try
            {
                MutexSecurity mSec = new MutexSecurity();
                mSec.AddAccessRule(new MutexAccessRule(
                    new SecurityIdentifier(WellKnownSidType.WorldSid, null),
                    MutexRights.FullControl,
                    AccessControlType.Allow));
                mutex = new Mutex(true, MutexName, out createdNew, mSec);
            }
            catch
            {
                try
                {
                    mutex = new Mutex(true, MutexName, out createdNew);
                }
                catch
                {
                    return;
                }
            }

            using (mutex)
            {
                if (!createdNew)
                {
                    return;
                }

                using (var app = new QuickVolumeAppContext())
                {
                    Application.Run(app);
                }
            }
        }

        private static void SignalClose()
        {
            try
            {
                using (var evt = EventWaitHandle.OpenExisting(CloseEventName))
                {
                    evt.Set();
                }
            }
            catch {}

            int msg = RegisterWindowMessage(CloseMessageName);
            if (msg != 0)
            {
                PostMessage((IntPtr)0xFFFF, (uint)msg, IntPtr.Zero, IntPtr.Zero);
            }

            IntPtr hwnd = FindWindow(null, "QuickVolume_Hidden_Host");
            if (hwnd != IntPtr.Zero)
            {
                PostMessage(hwnd, 0x0010, IntPtr.Zero, IntPtr.Zero);
            }
        }
    }

    #endregion
}
