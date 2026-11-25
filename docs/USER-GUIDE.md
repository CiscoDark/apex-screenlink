# Apex ScreenLink 2.0

Extract the complete portable ZIP into a writable folder and open
`ApexScreenLink.exe`. Keep its engine and runtime files together. No installation,
account or internet connection is needed after extraction.

## USB

Enable Developer options and USB debugging on Android. Connect a data-capable
cable, unlock the device and accept Android's authorization dialog. Dashboard
and USB show the model, serial, transport and state. Select an authorized device
and open Mirror Studio. Unauthorized and offline entries cannot start a mirror.
Device Manager reads manufacturer, model, Android release and API level.
Android 5.0+ is supported by the underlying USB video/control engine; audio and
some input modes require newer Android versions. OEM USB drivers may be needed.

## Cable-free pairing

**QR option:** On Apex's Wireless page select Pair with QR code. On the phone,
open Wireless debugging → Pair device with QR code and scan the displayed code.
Apex detects the matching mDNS pairing service, completes native Android pairing
and attempts to connect to that phone. The QR expires after two minutes; Generate
new QR code replaces its temporary secret. No QR image or secret is saved.
Use the six-digit method below when your network blocks mDNS.

1. On Android 11+, enable Developer options → Wireless debugging.
2. Connect the PC and phone to the same network. Guest networks, VPNs or access
   point client isolation may block local traffic.
3. On the phone select **Pair device with pairing code**.
4. On Apex's Wireless page enter the IP, **pairing port** and six-digit code.
   You may fill the address from a discovered PAIR service. Click Pair securely.
5. Return to the phone's main Wireless debugging screen and obtain its
   **connection port**. Enter that address under Connect a paired device, or
   choose a discovered CONNECT service, then click Connect.
6. Open the newly authorized device in Dashboard or Mirror Studio.

No USB cable is required for these steps. Pairing and connection ports normally
differ and may change after toggling Wireless debugging or changing networks.
The pairing code expires and is never saved. ADB manages the authorization key
in your Windows profile. Previously paired devices may reconnect through ADB's
native mDNS support; saved IP endpoints also receive bounded retry attempts.
If discovery fails, manual IP and port entry works without mDNS.

Mobile hotspots are supported only insofar as they allow the PC to reach the
phone's Wireless debugging service. Apex cannot override hotspot isolation.

## Legacy wireless and switching

For older phones, first authorize a USB connection. On Wireless select that USB
device and Enable legacy port 5555, acknowledge the unencrypted-network warning,
then connect to the phone's local IP on port 5555. Use only a trusted network.
Return to USB disables the legacy TCP listener by asking the device to restart
adbd in USB mode. Reattach USB if needed. Switching transports ends the old
stream; select the new transport to start another. There is no seamless handoff.

Disconnect stops the selected wireless endpoint and turns off automatic
reconnection globally; re-enable it in Settings when desired. Forget removes
Apex's endpoint/profile, but Android's paired-computer entry must also be
removed on the phone to revoke its trust.

## Mirror Studio

Each device has its own native studio. Multiple device studios may run together.
Click the screen to give mouse and keyboard input to Android. Dragging and
scrolling are handled by upstream scrcpy. Android navigation, volume,
notification/quick-settings and wake/sleep actions are available from the
toolbar's More menu, together with volume and Always on top.
Some Android shell commands depend on Android/OEM support and may report errors.

Use **Fullscreen** on the Apex toolbar; Escape returns to the normal window
when WPF has keyboard focus. If the native mirror holds focus, click the visible
Fullscreen toolbar button. Always on top pins the studio. The engine preserves
aspect ratio and responds to device rotation.

Default modifier is Left Alt (change to Right Ctrl or Left Windows in Settings):

| Action | Shortcut while the mirror has focus |
| --- | --- |
| Home / Back / Recents | Alt+H / Alt+B / Alt+S |
| Rotate the device screen | Alt+R |
| Phone screen off, keep mirroring | Alt+O |
| Phone screen on | Alt+Shift+O |
| Clipboard paste / copy | Alt+V / Alt+C |
| Open keyboard settings with UHID | Alt+K |

Clipboard sync requires Android 7+. The bundled upstream shortcut reference is
included with the source. Do not use upstream Alt+F in the embedded viewport:
its own fullscreen handling is not integrated with the surrounding WPF window.

Settings control H.264/H.265/AV1, max dimension (0 for native), frame limit,
bitrates, audio, UHID keyboard/gamepad, crop and optional virtual display.
Camera front/back capture requires Android 12+ and compatible hardware. Virtual
displays require Android 10+ and device support. Codec and mode failures are
reported in Diagnostics; selecting an unsupported feature cannot add device
capabilities. FPS limits are maxima; actual FPS is read from engine output.

## Captures and files

Record restarts the stream and records into MP4 or MKV. Stop recording finalizes
the file and starts an unrecorded stream. Audio toggle also restarts the stream
and changes both playback and recording audio; it is not independent playback
mute. A reconnect or audio change creates a separate recording segment. The
timer reflects the current engine segment, not the final media duration.
Always stop a recording cleanly before copying its file.

Screenshot saves a native PNG of the phone's main display through ADB. It does
not capture a mirrored camera or a virtual display. Recordings lists media from
the configured storage directory and supports open, folder, rename and confirmed
permanent deletion. Other previously configured storage locations must be
opened separately. No thumbnail preview or built-in media player is included.

Drop files on Device Manager's transfer card, or use Browse files. Normal files
go to `/sdcard/Download/`. APK installation always requires confirmation. Native
engine-window file dropping is disabled after embedding. Large transfers have
a three-minute deadline and may need retrying on slow networks.

## Preferences, privacy and troubleshooting

Save settings before starting a new studio. Device Manager can save the current
global streaming settings as a device-specific profile, or revert to global.
Profiles use ADB transport serials, so USB and wireless endpoints can have
different profiles. Settings are grouped into Video, Audio & input, Capture,
Appearance and Advanced tabs. Theme supports System, Light and Dark; System is
checked at startup or settings save. Short page transitions and hover fades
respect Windows animation preferences. Appearance → Reduce motion disables them.

The app stores preferences, endpoint history and recent sessions in data/ beside
the executable. It writes no hidden analytics or persistent diagnostic logs.
Exported logs may include local paths, IPs and serials; inspect before sharing.
The ADB private key remains in the Windows user's `.android` directory. Do not
include it when copying the portable app to another person or computer.

Diagnostics verifies bundled hashes, shows ADB/mDNS output and local interfaces,
and exports current logs. No-device detection cannot distinguish disabled
debugging from a missing cable/driver. An offline device normally requires
replugging or re-enabling Wireless debugging. Verify the current port after a
network change. Automatic retries have a five-attempt budget; manually reconnect
or reopen a studio after fixing the issue.
