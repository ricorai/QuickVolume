"""
Quick Volume - Accurate Windows 11 Fluent Acrylic System Tray Flyout
Pixel-perfect replication of reference UI:
- Clean transparent frosted glass card (no outer DWM grey boundary).
- Centered Volume Pill Buttons with authentic Windows 11 accent blue (#4A72B4), thin checkmark, 3-wave speaker icon, and Segoe UI Variable typography.
- Authentic Windows 11 soft sky-blue capsule toggle with dark knob (Start with Windows).
- Clean Close row with geometric cross icon and subtle hover feedback.
- Right-click only tray activation, auto-dismissal on outside click.
- Silent background execution under pythonw, single-instance protected.
"""

import sys
import os
import traceback
import ctypes
from ctypes import wintypes

from PySide6.QtWidgets import (
    QApplication, QWidget, QVBoxLayout, QHBoxLayout,
    QLabel, QGraphicsDropShadowEffect, QFrame, QSystemTrayIcon
)
from PySide6.QtCore import (
    Qt, Signal, QPropertyAnimation, Property, QPoint, QPointF, QRectF, QEvent
)
from PySide6.QtGui import (
    QColor, QPainter, QBrush, QPen, QFont, QFontMetrics,
    QLinearGradient, QIcon, QCursor, QGuiApplication
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


class AcrylicGlassCard(QWidget):
    """Frosted Transparent Glass Card with specular highlights and inner bevel."""
    def __init__(self, parent=None):
        super().__init__(parent)
        self.setAttribute(Qt.WidgetAttribute.WA_TranslucentBackground)

    def paintEvent(self, event):
        p = QPainter(self)
        p.setRenderHint(QPainter.Antialiasing)

        w = self.width()
        h = self.height()
        radius = 18.0

        # Transparent frosted glass gradient (see-through to desktop)
        bg_grad = QLinearGradient(0, 0, 0, h)
        bg_grad.setColorAt(0.0, QColor(42, 54, 76, 115))   # Frosted slate translucent top
        bg_grad.setColorAt(0.4, QColor(24, 28, 38, 105))   # Smooth transparent mid
        bg_grad.setColorAt(1.0, QColor(14, 16, 22, 120))   # Deep transparent base
        p.setBrush(QBrush(bg_grad))

        # Dual-layer specular border
        pen_grad = QLinearGradient(0, 0, 0, h)
        pen_grad.setColorAt(0.0, QColor(255, 255, 255, 115)) # Crisp top specular rim
        pen_grad.setColorAt(0.4, QColor(255, 255, 255, 40))
        pen_grad.setColorAt(1.0, QColor(255, 255, 255, 20))  # Soft bottom boundary
        p.setPen(QPen(QBrush(pen_grad), 1.2))

        rect = QRectF(1.0, 1.0, w - 2.0, h - 2.0)
        p.drawRoundedRect(rect, radius, radius)

        # Inner specular light lip along top curve
        inner_lip_grad = QLinearGradient(0, 2, 0, 14)
        inner_lip_grad.setColorAt(0.0, QColor(255, 255, 255, 45))
        inner_lip_grad.setColorAt(1.0, QColor(255, 255, 255, 0))
        p.setPen(QPen(QBrush(inner_lip_grad), 1.0))
        p.drawRoundedRect(QRectF(2.5, 2.5, w - 5.0, 14.0), radius - 1.5, radius - 1.5)


class VolumePillButton(QWidget):
    """Accurate Pill-shaped volume selection button with centered content."""
    clicked = Signal()

    def __init__(self, text, is_active=False, parent=None):
        super().__init__(parent)
        self.text = text
        self.is_active = is_active
        self.is_hovered = False
        self.setFixedHeight(46)
        self.setCursor(Qt.PointingHandCursor)

    def setActive(self, active):
        if self.is_active != active:
            self.is_active = active
            self.update()

    def enterEvent(self, event):
        self.is_hovered = True
        self.update()

    def leaveEvent(self, event):
        self.is_hovered = False
        self.update()

    def mousePressEvent(self, event):
        if event.button() == Qt.LeftButton:
            self.clicked.emit()

    def paintEvent(self, event):
        p = QPainter(self)
        p.setRenderHint(QPainter.Antialiasing)
        p.setRenderHint(QPainter.TextAntialiasing)

        w = self.width()
        h = self.height()
        radius = h / 2.0

        if self.is_active:
            # Active Blue Accent Capsule
            bg_grad = QLinearGradient(0, 0, 0, h)
            bg_grad.setColorAt(0.0, QColor(74, 114, 180)) # Vibrant Windows 11 Blue
            bg_grad.setColorAt(1.0, QColor(53, 87, 146))
            p.setBrush(QBrush(bg_grad))

            pen_grad = QLinearGradient(0, 0, 0, h)
            pen_grad.setColorAt(0.0, QColor(255, 255, 255, 115))
            pen_grad.setColorAt(1.0, QColor(255, 255, 255, 30))
            p.setPen(QPen(QBrush(pen_grad), 1.2))
        else:
            # Inactive Charcoal Capsule
            bg_grad = QLinearGradient(0, 0, 0, h)
            if self.is_hovered:
                bg_grad.setColorAt(0.0, QColor(76, 82, 98, 205))
                bg_grad.setColorAt(1.0, QColor(54, 59, 70, 220))
                p.setBrush(QBrush(bg_grad))
                
                pen_grad = QLinearGradient(0, 0, 0, h)
                pen_grad.setColorAt(0.0, QColor(255, 255, 255, 80))
                pen_grad.setColorAt(1.0, QColor(255, 255, 255, 25))
                p.setPen(QPen(QBrush(pen_grad), 1.0))
            else:
                bg_grad.setColorAt(0.0, QColor(60, 64, 76, 175))
                bg_grad.setColorAt(1.0, QColor(42, 45, 54, 195))
                p.setBrush(QBrush(bg_grad))
                
                pen_grad = QLinearGradient(0, 0, 0, h)
                pen_grad.setColorAt(0.0, QColor(255, 255, 255, 50))
                pen_grad.setColorAt(1.0, QColor(255, 255, 255, 15))
                p.setPen(QPen(QBrush(pen_grad), 1.0))

        p.drawRoundedRect(QRectF(1, 1, w - 2, h - 2), radius, radius)

        # Typography
        font = QFont("Segoe UI Variable Text", 10)
        font.setWeight(QFont.Weight.Medium if self.is_active else QFont.Weight.Normal)
        p.setFont(font)
        fm = QFontMetrics(font)
        text_w = fm.horizontalAdvance(self.text)

        # Icon Geometry & Centering Calculations
        icon_w = 18
        gap = 10
        check_w = 13 if self.is_active else 0

        total_content_w = text_w + icon_w + gap
        if self.is_active:
            total_content_w += check_w + gap

        start_x = (w - total_content_w) / 2.0
        cy = h / 2.0
        curr_x = start_x

        # 1. Checkmark (Visible if active)
        if self.is_active:
            p.setPen(QPen(QColor(255, 255, 255), 2.2, Qt.SolidLine, Qt.RoundCap, Qt.RoundJoin))
            p.drawLine(QPointF(curr_x, cy + 1), QPointF(curr_x + 4.0, cy + 5.0))
            p.drawLine(QPointF(curr_x + 4.0, cy + 5.0), QPointF(curr_x + 11.5, cy - 4.5))
            curr_x += check_w + gap

        # 2. Speaker Icon (Housing + Flare + 3 Sound Waves)
        icon_color = QColor(255, 255, 255) if self.is_active else QColor(215, 220, 230)
        p.setPen(QPen(icon_color, 1.5, Qt.SolidLine, Qt.RoundCap, Qt.RoundJoin))
        p.setBrush(Qt.NoBrush)

        spk_x = curr_x
        p.drawLine(QPointF(spk_x, cy - 3.0), QPointF(spk_x + 3.0, cy - 3.0))
        p.drawLine(QPointF(spk_x + 3.0, cy - 3.0), QPointF(spk_x + 7.5, cy - 7.0))
        p.drawLine(QPointF(spk_x + 7.5, cy - 7.0), QPointF(spk_x + 7.5, cy + 7.0))
        p.drawLine(QPointF(spk_x + 7.5, cy + 7.0), QPointF(spk_x + 3.0, cy + 3.0))
        p.drawLine(QPointF(spk_x + 3.0, cy + 3.0), QPointF(spk_x, cy + 3.0))
        p.drawLine(QPointF(spk_x, cy + 3.0), QPointF(spk_x, cy - 3.0))

        # 3 Concentric Sound Waves
        p.drawArc(QRectF(spk_x + 5.5, cy - 4.0, 6.5, 8.0), int(-35 * 16), int(70 * 16))
        p.drawArc(QRectF(spk_x + 7.5, cy - 6.5, 9.0, 13.0), int(-40 * 16), int(80 * 16))
        p.drawArc(QRectF(spk_x + 9.5, cy - 9.0, 11.5, 18.0), int(-45 * 16), int(90 * 16))

        curr_x += icon_w + gap

        # 3. Label Text
        text_color = QColor(255, 255, 255) if self.is_active else QColor(196, 201, 214)
        p.setPen(text_color)
        p.drawText(QRectF(curr_x, 0, text_w + 10, h), Qt.AlignVCenter | Qt.AlignLeft, self.text)


class ToggleSwitch(QWidget):
    """Accurate Windows 11 Pill Toggle Switch."""
    toggled = Signal(bool)

    def __init__(self, checked=False, parent=None):
        super().__init__(parent)
        self.setFixedSize(42, 22)
        self.setCursor(Qt.PointingHandCursor)
        self._checked = checked
        self._position = 22.0 if checked else 3.0
        self._anim = QPropertyAnimation(self, b"thumb_pos", self)
        self._anim.setDuration(120)

    def get_thumb_pos(self):
        return self._position

    def set_thumb_pos(self, pos):
        self._position = pos
        self.update()

    thumb_pos = Property(float, get_thumb_pos, set_thumb_pos)

    def isChecked(self):
        return self._checked

    def setChecked(self, checked):
        if self._checked != checked:
            self._checked = checked
            self._anim.stop()
            self._anim.setStartValue(self._position)
            self._anim.setEndValue(22.0 if checked else 3.0)
            self._anim.start()
            self.toggled.emit(self._checked)
            self.update()

    def mousePressEvent(self, event):
        if event.button() == Qt.LeftButton:
            self.setChecked(not self._checked)

    def paintEvent(self, event):
        p = QPainter(self)
        p.setRenderHint(QPainter.Antialiasing)

        # In reference image: Track is soft sky-blue (#94B5F2) and Knob is dark (#202229)
        if self._checked:
            bg_color = QColor(148, 181, 242)   # Sky-blue capsule matching reference
            border_color = QColor(170, 198, 250)
            knob_color = QColor(32, 33, 38)     # Dark circle knob
        else:
            bg_color = QColor(42, 45, 54)
            border_color = QColor(255, 255, 255, 60)
            knob_color = QColor(230, 233, 240)  # White circle knob

        p.setBrush(QBrush(bg_color))
        p.setPen(QPen(border_color, 1))
        p.drawRoundedRect(QRectF(1, 1, 40, 20), 10, 10)

        # Knob
        p.setPen(Qt.NoPen)
        p.setBrush(QBrush(knob_color))
        p.drawEllipse(QRectF(self._position, 3.0, 16.0, 16.0))


class ClickableRow(QWidget):
    """Full-width clickable row with smooth rounded hover highlight."""
    clicked = Signal()

    def __init__(self, parent=None):
        super().__init__(parent)
        self.is_hovered = False
        self.setCursor(Qt.PointingHandCursor)
        self.setFixedHeight(36)

    def enterEvent(self, event):
        self.is_hovered = True
        self.update()

    def leaveEvent(self, event):
        self.is_hovered = False
        self.update()

    def mousePressEvent(self, event):
        if event.button() == Qt.LeftButton:
            self.clicked.emit()

    def paintEvent(self, event):
        p = QPainter(self)
        p.setRenderHint(QPainter.Antialiasing)
        if self.is_hovered:
            p.setBrush(QBrush(QColor(255, 255, 255, 18)))
            p.setPen(Qt.NoPen)
            p.drawRoundedRect(self.rect(), 8, 8)


class ModernVolumeFlyout(QWidget):
    """Windows 11 Transparent Acrylic Flyout for Quick Volume."""
    def __init__(self, on_close_app_callback):
        super().__init__(
            None,
            Qt.WindowType.FramelessWindowHint |
            Qt.WindowType.WindowStaysOnTopHint |
            Qt.WindowType.Tool
        )
        self.setAttribute(Qt.WidgetAttribute.WA_TranslucentBackground)
        self.on_close_app = on_close_app_callback

        self.setup_ui()

    def setup_ui(self):
        # Outer layout: zero margins around card so no outer boundary exists
        outer_layout = QVBoxLayout(self)
        outer_layout.setContentsMargins(0, 0, 0, 0)

        # Transparent Acrylic Glass Card
        self.card = AcrylicGlassCard()
        self.card.setFixedWidth(276)

        card_layout = QVBoxLayout(self.card)
        card_layout.setContentsMargins(14, 16, 14, 14)
        card_layout.setSpacing(8)

        # Preset 1: 25% Volume
        self.btn_25 = VolumePillButton("25% Volume")
        self.btn_25.clicked.connect(lambda: self.select_volume(0.25))
        card_layout.addWidget(self.btn_25)

        # Preset 2: 100% Volume
        self.btn_100 = VolumePillButton("100% Volume")
        self.btn_100.clicked.connect(lambda: self.select_volume(1.00))
        card_layout.addWidget(self.btn_100)

        # Divider line
        divider = QFrame()
        divider.setFrameShape(QFrame.HLine)
        divider.setFrameShadow(QFrame.Plain)
        divider.setStyleSheet("background-color: rgba(255, 255, 255, 0.10); max-height: 1px; border: none; margin: 4px 4px;")
        card_layout.addWidget(divider)

        # Row: Start with Windows
        self.row_startup = ClickableRow()
        row_startup_layout = QHBoxLayout(self.row_startup)
        row_startup_layout.setContentsMargins(8, 0, 8, 0)
        row_startup_layout.setSpacing(12)

        self.switch_startup = ToggleSwitch(checked=is_startup_enabled())
        self.switch_startup.toggled.connect(self.on_startup_toggled)

        lbl_startup = QLabel("Start with Windows")
        lbl_startup.setAttribute(Qt.WidgetAttribute.WA_TransparentForMouseEvents)
        lbl_startup.setStyleSheet("color: #ECEEF2; font-family: 'Segoe UI Variable Text', 'Segoe UI', sans-serif; font-size: 13.5px; font-weight: 500;")

        row_startup_layout.addWidget(self.switch_startup)
        row_startup_layout.addWidget(lbl_startup)
        row_startup_layout.addStretch()

        self.row_startup.clicked.connect(lambda: self.switch_startup.setChecked(not self.switch_startup.isChecked()))
        card_layout.addWidget(self.row_startup)

        # Row: Close
        self.row_close = ClickableRow()
        row_close_layout = QHBoxLayout(self.row_close)
        row_close_layout.setContentsMargins(10, 0, 8, 0)
        row_close_layout.setSpacing(14)

        class CloseIcon(QWidget):
            def __init__(self):
                super().__init__()
                self.setFixedSize(14, 14)
                self.setAttribute(Qt.WidgetAttribute.WA_TransparentForMouseEvents)
            def paintEvent(self, e):
                p = QPainter(self)
                p.setRenderHint(QPainter.Antialiasing)
                p.setPen(QPen(QColor(255, 255, 255, 170), 1.8, Qt.SolidLine, Qt.RoundCap))
                p.drawLine(QPointF(2, 2), QPointF(12, 12))
                p.drawLine(QPointF(12, 2), QPointF(2, 12))

        lbl_close_text = QLabel("Close")
        lbl_close_text.setAttribute(Qt.WidgetAttribute.WA_TransparentForMouseEvents)
        lbl_close_text.setStyleSheet("color: #DDE0E8; font-family: 'Segoe UI Variable Text', 'Segoe UI', sans-serif; font-size: 13.5px; font-weight: 500;")

        row_close_layout.addWidget(CloseIcon())
        row_close_layout.addWidget(lbl_close_text)
        row_close_layout.addStretch()

        self.row_close.clicked.connect(self.on_close_app)
        card_layout.addWidget(self.row_close)

        outer_layout.addWidget(self.card)
        self.setFixedWidth(276)

    def select_volume(self, level):
        set_volume_scalar(level)
        self.update_active_states()

    def update_active_states(self):
        vol = get_volume_scalar()
        is_25 = (vol >= 0 and abs(vol - 0.25) < 0.05)
        is_100 = (vol >= 0 and abs(vol - 1.0) < 0.05)

        self.btn_25.setActive(is_25)
        self.btn_100.setActive(is_100)
        self.switch_startup.setChecked(is_startup_enabled())

    def on_startup_toggled(self, checked):
        set_startup(checked)

    def show_above_tray(self, tray_geom):
        self.update_active_states()
        self.adjustSize()

        screen = QGuiApplication.primaryScreen().availableGeometry()
        w = self.width()
        h = self.height()

        if tray_geom.isValid():
            x = int(tray_geom.center().x() - w / 2)
            y = int(tray_geom.top() - h - 6)
        else:
            cur = QCursor.pos()
            x = int(cur.x() - w / 2)
            y = int(cur.y() - h - 10)

        # Clamping
        if x + w > screen.right() - 8:
            x = screen.right() - w - 8
        if x < screen.left() + 8:
            x = screen.left() + 8
        if y < screen.top() + 8:
            y = screen.top() + 8

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
        self.tray.setToolTip("Quick Volume (Right-click: 25% / 100%)")

        self.flyout = ModernVolumeFlyout(self.close_app)
        self.tray.activated.connect(self.on_tray_activated)

        if not is_startup_enabled():
            set_startup(True)

    def on_tray_activated(self, reason):
        # Only functions on right-click
        if reason == QSystemTrayIcon.ActivationReason.Context:
            if self.flyout.isVisible():
                self.flyout.hide()
            else:
                self.flyout.show_above_tray(self.tray.geometry())
        elif reason == QSystemTrayIcon.ActivationReason.Trigger:
            pass

    def close_app(self):
        log("Closing Quick Volume via user request.")
        self.flyout.hide()
        self.tray.hide()
        self.app.quit()

    def run(self):
        self.tray.show()
        log("Quick Volume tray started.")
        return self.app.exec()


def main():
    log("Starting Quick Volume...")
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
