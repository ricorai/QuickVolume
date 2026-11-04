# Quick Volume

> **Design Philosophy: The Iceberg Principle**  
> *Simple surface, deep structure. Make the complex simple.*  
> On the surface: a featherweight system tray toggle that instantly cycles between your custom volume presets in one click. Beneath the surface: a pure native Win32 Core Audio COM implementation running in `<10ms` with zero resident Python/Qt overhead.

---

## Highlights

- **Featherweight Native C# Architecture:**
  - Replaces the legacy Python/PySide6 stack (~80MB+ RAM footprint) with a native, zero-dependency .NET 4.0 WinForms/COM executable (~70 KB, ~12MB RAM).
  - Direct Win32 COM Interop with Windows Core Audio APIs (`IMMDeviceEnumerator`, `IAudioEndpointVolume`).
- **Fixed 5-Slot Windows 11 Flyout (`220 × 242 px`):**
  - **Zero "Accordion" Jumping:** Fixed viewport height accommodating exactly 5 visible slot rows (`140px`).
  - **Bottom Baseline Stacking & Gravity:** When presets are $\le 5$, rows sit anchored flush against the bottom boundary line above "Start with Windows"; adding a preset stacks upward toward the header, and removing a preset causes upper rows to drop down with gravity into the vacant space.
  - **Smooth Viewport Scrolling:** When slots exceed 5 (up to 10 max), mouse wheel scrolling moves through presets smoothly within the fixed viewport.
  - **Dual-Division Slot Rows (`[✓] XX% Volume | - | ⚙`):**
    - **Left:** Clicking the volume text applies that preset immediately and dismisses the flyout.
    - **Middle (`-`):** Delete that specific slot with upward stacking compaction.
    - **Right (`⚙`):** Flips in-place to the in-menu Configuration View.
  - **Top-Insert `[+]` Button:** Appends new slots directly from the top (`Insert(0, ...)`), snapping into view.
- **Chrome-Style In-Menu Configuration View:**
  - **In-Situ Drill-Down:** Flips view directly inside the existing 220 × 242 px flyout window (no external popup windows or taskbar items).
  - **Clean & Compact:** Large bold percentage readout, custom smooth GDI+ `DarkSlider`, `Reset`, `Cancel`, `Save`, `← Back`, and `✕` dismiss.
  - **Windows 11 Acrylic & Warm Silver Palette:** Strict `#1F1F1F` surface, `#313131` hover, `#A6A5A1` Warm Silver accent, and crisp `#EDEDED` typography.
- **Persistence & Automation:**
  - Preset configurations persist automatically in `QuickVolume.json`.
  - **CLI Automation Support:**
    - `QuickVolume.exe --toggle` $\rightarrow$ Cycles to next configured preset (<20ms).
    - `QuickVolume.exe --set <pct>` $\rightarrow$ Sets volume to exact percentage (0-100).
    - `QuickVolume.exe --25` / `--100` $\rightarrow$ Legacy instant presets.
    - `QuickVolume.exe --close` $\rightarrow$ Closes any active background tray instance.
- **Single-Instance Protection:**
  - Guarded by global named mutex `Global\QuickVolumeTrayApp_SingleInstance`.
- **Startup Integration:**
  - Native `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` toggle.

---

## Directory Layout

```
Quick Volume/
├── assets/
│   ├── MakeQuickVolumeIcon.cs # Vector icon generator
│   ├── quickvolume.ico        # Multi-resolution 32-bit ARGB icon (16x16 to 256x256)
│   └── quickvolume.png        # High-res preview
├── history/
│   ├── v1 - Acrylic Flyout (Legacy)/
│   └── v2 - Minimal UI (Legacy)/
├── src/
│   ├── QuickVolume.cs         # Native C# Core Audio Engine & Tray UI
│   └── build.bat              # Native csc.exe compiler script
├── .gitignore
├── QuickVolume.exe            # Root standalone executable (~76 KB)
├── QuickVolume.json           # User preset slot configurations
├── Quick Volume.lnk           # 1-click launcher shortcut
└── README.md
```
