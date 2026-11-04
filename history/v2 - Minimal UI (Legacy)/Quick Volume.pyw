"""
Quick Volume v2 - Minimalistic Context Menu (NVIDIA Tray Style)
- Ultra-clean, compact flat right-click context menu.
- No bulky pill buttons, toggle switches, or heavy glass gradients.
- Simple checkmarks for active volume preset (25% / 100%) and Start with Windows.
- Right-click only tray activation, instant dismissal on selection or outside click.
- Silent background execution under pythonw, single-instance protected.
"""

import sys
import os
import traceback
import ctypes

from PySide6.QtWidgets import (
    QApplication, QWidget, QVBoxLayout, QFrame, QSystemTrayIcon
)
from PySide6.QtCore import (
    Qt, Signal, QPointF, QRectF, QEvent
)
from PySide6.QtGui import (
    QColor, QPainter, QBrush, QPen, QFont, QIcon, QCursor, QGuiApplication
)

from pycaw.pycaw import AudioUtilities

MUTEX_NAME = "Global\\QuickVolumeTrayApp_SingleInstance"
ERROR_ALREADY_EXISTS = 183
LOG_FILE = os.path.join(os.path.dirname(os.path.abspath(__file__)), "quick_volume.log")


def log(msg):
    try:
        with open(LOG_FILE, "a", encoding="utf-8") as f:
            f.write(f"{msg}\n")
    except Exception:
        pass


def acquire_single_instance():
    try:
        kernel32 = ctypes.windll.kernel32
        mutex = kernel32.CreateMutexW(None, False, MUTEX_NAME)
        err = kernel32.GetLastError()
        if err == ERROR_ALREADY_EXISTS:
            log("Another instance is running. Exiting.")
            return None
        return mutex
    except Exception as e:
        log(f"Mutex error: {e}")
        return 1


def get_startup_shortcut_path():
    startup_dir = os.path.expandvars(r"%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup")
    return os.path.join(startup_dir, "Quick Volume.lnk")


def is_startup_enabled():
    return os.path.exists(get_startup_shortcut_path())


def set_startup(enable=True):
    lnk_path = get_startup_shortcut_path()
    if enable:
        try:
            import win32com.client
            script_path = os.path.abspath(__file__)
            work_dir = os.path.dirname(script_path)

            pythonw_exe = sys.executable
            if "python.exe" in pythonw_exe.lower():
                candidate = pythonw_exe.lower().replace("python.exe", "pythonw.exe")
                if os.path.exists(candidate):
                    pythonw_exe = candidate

            ico_path = os.path.join(work_dir, "QuickVolume.ico")
            if not os.path.exists(ico_path):
                parent_ico = os.path.join(os.path.dirname(work_dir), "QuickVolume.ico")
                if os.path.exists(parent_ico):
                    ico_path = parent_ico

            shell = win32com.client.Dispatch("WScript.Shell")
            shortcut = shell.CreateShortcut(lnk_path)
            shortcut.TargetPath = pythonw_exe
            shortcut.Arguments = f'"{script_path}"'
            shortcut.WorkingDirectory = work_dir
            if os.path.exists(ico_path):
                shortcut.IconLocation = f"{ico_path},0"
            shortcut.Description = "Quick Volume Tray Switcher (25% / 100%)"
            shortcut.Save()
            log("Startup shortcut enabled.")
            return True
        except Exception as e:
            log(f"Failed to enable startup: {e}")
            return False
    else:
        try:
            if os.path.exists(lnk_path):
                os.remove(lnk_path)
            log("Startup shortcut removed.")
            return True
        except Exception as e:
            log(f"Failed to remove startup shortcut: {e}")
            return False


def set_volume_scalar(level: float):
    try:
        speakers = AudioUtilities.GetSpeakers()
        if not speakers:
            return False
        endpoint = speakers.EndpointVolume
        endpoint.SetMasterVolumeLevelScalar(level, None)
        if endpoint.GetMute():
            endpoint.SetMute(0, None)
        return True
    except Exception as e:
        log(f"Failed to set volume: {e}")
        return False


def get_volume_scalar() -> float:
    try:
        speakers = AudioUtilities.GetSpeakers()
        if not speakers:
            return -1.0
        endpoint = speakers.EndpointVolume
        return endpoint.GetMasterVolumeLevelScalar()
    except Exception:
        return -1.0


class MinimalMenuSurface(QWidget):
    """Compact, flat context menu container styled after the NVIDIA tray right-click menu."""
    def __init__(self, parent=None):
        super().__init__(parent)
        self.setAttribute(Qt.WidgetAttribute.WA_TranslucentBackground)

    def paintEvent(self, event):
        p = QPainter(self)
        p.setRenderHint(QPainter.Antialiasing)

        w = self.width()
        h = self.height()

        # Flat, clean dark charcoal surface with crisp 1px border
        p.setBrush(QBrush(QColor(32, 32, 32)))
        p.setPen(QPen(QColor(58, 58, 58), 1.0))
        p.drawRoundedRect(QRectF(0.5, 0.5, w - 1.0, h - 1.0), 4.0, 4.0)


class MinimalMenuItem(QWidget):
    """Slim, classic context-menu row with left checkmark gutter and flat hover highlight."""
    clicked = Signal()

    def __init__(self, text: str, checkable: bool = False, checked: bool = False, parent=None):
        super().__init__(parent)
        self.text = text
        self.checkable = checkable
        self.checked = checked
        self.is_hovered = False
        self.setFixedHeight(26)
        self.setCursor(Qt.ArrowCursor)

    def setChecked(self, checked: bool):
        if self.checked != checked:
            self.checked = checked
            self.update()

    def enterEvent(self, event):
        self.is_hovered = True
        self.update()

    def leaveEvent(self, event):
        self.is_hovered = False
        self.update()

    def mouseReleaseEvent(self, event):
        if event.button() == Qt.LeftButton and self.rect().contains(event.pos()):
            self.clicked.emit()

    def paintEvent(self, event):
        p = QPainter(self)
        p.setRenderHint(QPainter.Antialiasing)
        p.setRenderHint(QPainter.TextAntialiasing)

        w = self.width()
        h = self.height()

        # Subtle flat hover highlight bar
        if self.is_hovered:
            p.setBrush(QBrush(QColor(52, 52, 52)))
            p.setPen(Qt.NoPen)
            p.drawRoundedRect(QRectF(2.0, 1.0, w - 4.0, h - 2.0), 3.0, 3.0)

        # Left checkmark (26px gutter)
        if self.checkable and self.checked:
            cy = h / 2.0
            p.setPen(QPen(QColor(235, 235, 235), 1.5, Qt.SolidLine, Qt.RoundCap, Qt.RoundJoin))
            p.drawLine(QPointF(9.0, cy + 0.5), QPointF(12.0, cy + 3.5))
            p.drawLine(QPointF(12.0, cy + 3.5), QPointF(17.5, cy - 3.0))

        # Menu label
        font = QFont("Segoe UI", 9)
        p.setFont(font)
        p.setPen(QColor(235, 235, 235))
        p.drawText(QRectF(28.0, 0.0, w - 34.0, h), Qt.AlignVCenter | Qt.AlignLeft, self.text)


class MinimalContextMenu(QWidget):
    """Minimalistic NVIDIA-style right-click context menu for Quick Volume."""
    def __init__(self, on_close_app_callback):
        super().__init__(
            None,
            Qt.WindowType.Popup |
            Qt.WindowType.FramelessWindowHint |
            Qt.WindowType.NoDropShadowWindowHint
        )
        self.setAttribute(Qt.WidgetAttribute.WA_TranslucentBackground)
        self.on_close_app = on_close_app_callback
        self.setup_ui()

    def _create_separator(self):
        sep = QFrame()
        sep.setFrameShape(QFrame.HLine)
        sep.setFrameShadow(QFrame.Plain)
        sep.setStyleSheet("background-color: #363636; max-height: 1px; border: none; margin: 2px 6px;")
        return sep

    def setup_ui(self):
        outer_layout = QVBoxLayout(self)
        outer_layout.setContentsMargins(0, 0, 0, 0)

        self.surface = MinimalMenuSurface()
        self.surface.setFixedWidth(172)

        menu_layout = QVBoxLayout(self.surface)
        menu_layout.setContentsMargins(3, 4, 3, 4)
        menu_layout.setSpacing(1)

        # 25% Volume
        self.item_25 = MinimalMenuItem("25% Volume", checkable=True)
        self.item_25.clicked.connect(lambda: self.select_volume(0.25))
        menu_layout.addWidget(self.item_25)

        # 100% Volume
        self.item_100 = MinimalMenuItem("100% Volume", checkable=True)
        self.item_100.clicked.connect(lambda: self.select_volume(1.00))
        menu_layout.addWidget(self.item_100)

        menu_layout.addWidget(self._create_separator())

        # Start with Windows
        self.item_startup = MinimalMenuItem("Start with Windows", checkable=True, checked=is_startup_enabled())
        self.item_startup.clicked.connect(self.toggle_startup)
        menu_layout.addWidget(self.item_startup)

        menu_layout.addWidget(self._create_separator())

        # Close
        self.item_close = MinimalMenuItem("Close", checkable=False)
        self.item_close.clicked.connect(self.on_close_app)
        menu_layout.addWidget(self.item_close)

        outer_layout.addWidget(self.surface)
        self.setFixedWidth(172)

    def select_volume(self, level: float):
        set_volume_scalar(level)
        self.update_active_states()
        self.hide()

    def toggle_startup(self):
        new_state = not is_startup_enabled()
        set_startup(new_state)
        self.item_startup.setChecked(is_startup_enabled())
        self.hide()

    def update_active_states(self):
        vol = get_volume_scalar()
        is_25 = (vol >= 0 and abs(vol - 0.25) < 0.05)
        is_100 = (vol >= 0 and abs(vol - 1.0) < 0.05)

        self.item_25.setChecked(is_25)
        self.item_100.setChecked(is_100)
        self.item_startup.setChecked(is_startup_enabled())

    def show_at_tray(self, tray_geom):
        self.update_active_states()
        self.adjustSize()

        screen = QGuiApplication.primaryScreen().availableGeometry()
        w = self.width()
        h = self.height()

        cur = QCursor.pos()
        if cur.x() != 0 or cur.y() != 0:
            x = int(cur.x() - w + 12)
            y = int(cur.y() - h - 4)
        elif tray_geom.isValid():
            x = int(tray_geom.center().x() - w / 2)
            y = int(tray_geom.top() - h - 4)
        else:
            x = screen.right() - w - 12
            y = screen.bottom() - h - 12

        if x + w > screen.right() - 6:
            x = screen.right() - w - 6
        if x < screen.left() + 6:
            x = screen.left() + 6
        if y < screen.top() + 6:
            y = screen.top() + 6
        if y + h > screen.bottom() - 4:
            y = screen.bottom() - h - 4

        self.move(x, y)
        self.show()
        self.raise_()
        self.activateWindow()

    def changeEvent(self, event):
        if event.type() == QEvent.ActivationChange:
            if not self.isActiveWindow():
                self.hide()
        super().changeEvent(event)


class QuickVolumeApp:
    def __init__(self):
        self.app = QApplication(sys.argv)
        self.app.setQuitOnLastWindowClosed(False)

        base_dir = os.path.dirname(os.path.abspath(__file__))
        ico_file = os.path.join(base_dir, "QuickVolume.ico")
        png_file = os.path.join(base_dir, "QuickVolume.png")

        if os.path.exists(ico_file):
            self.icon = QIcon(ico_file)
        elif os.path.exists(png_file):
            self.icon = QIcon(png_file)
        else:
            self.icon = QIcon()

        self.tray = QSystemTrayIcon(self.icon, self.app)
        self.tray.setToolTip("Quick Volume")

        self.menu = MinimalContextMenu(self.close_app)
        self.tray.activated.connect(self.on_tray_activated)

        if not is_startup_enabled():
            set_startup(True)

    def on_tray_activated(self, reason):
        if reason == QSystemTrayIcon.ActivationReason.Context:
            if self.menu.isVisible():
                self.menu.hide()
            else:
                self.menu.show_at_tray(self.tray.geometry())

    def close_app(self):
        log("Closing Quick Volume via user request.")
        self.menu.hide()
        self.tray.hide()
        self.app.quit()

    def run(self):
        self.tray.show()
        log("Quick Volume v2 tray started.")
        return self.app.exec()


def main():
    log("Starting Quick Volume v2...")
    mutex = acquire_single_instance()
    if not mutex:
        sys.exit(0)

    try:
        app = QuickVolumeApp()
        sys.exit(app.run())
    except Exception as e:
        log(f"Exception: {e}\n{traceback.format_exc()}")
        sys.exit(1)


if __name__ == "__main__":
    main()
