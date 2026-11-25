# Scope and release limitations

This is an actual compiled portable implementation, not a production-certified
fulfilment of every requested feature. The following distinctions are material.

## Implemented and exercised without a phone

Native WPF desktop, eight navigation pages, local settings/history/profile
storage, IP/port/code validation, asynchronous ADB process management, mDNS and
device-output parsing, bounded retries, dependency hashes, media library file
operations, bundled self-contained runtime and engine. Automated tests cover
core failure cases. The native SDL/WPF embedding, resize and orderly-close path
has a synthetic-window integration test. Real ADB runs and detected an Android
device during the session.

## Device-test evidence

The user reported that the app worked in their physical-device test before the
final visual refresh. No individual test cases were specified, so this is
recorded as user-confirmed baseline functionality rather than a claim that every
feature below passed. The agent observed mDNS/device enumeration and an offline
transition; its attempted recording did not start while the endpoint was offline.

## Not individually validated on Android hardware

USB and cable-free native TLS pairing, manual wireless connect/disconnect,
legacy TCP/IP, live mirror, keyboard/mouse/touch/clipboard, recording, audio,
screenshots, APK/file transfers, orientation, multiple concurrent devices,
physical screen-off control, UHID keyboard/gamepad, camera and virtual display.
The underlying implementation is upstream scrcpy/ADB, but this does not replace
testing Apex's integration on real devices. Network interruption recovery,
extended stability, every Android version and OEM, mixed-DPI monitors and exact
input focus behavior require additional hardware QA.

## Deliberate omissions and behavior differences

- CPU FFmpeg video decoding with SDL rendering; no configured hardware decoder.
- No automatic adaptive bitrate. Quality presets are fixed settings.
- No RTT, packet-loss, signal-strength or bandwidth estimate; actual engine FPS
  and skipped-frame lines are shown when available, plus ADB reachability.
- Session controls and live screen open in a separate native Studio window,
  while the main Mirror Studio page manages sessions.
- Stream restarts for recording and audio changes. No seamless recording toggle,
  seamless transport switching or independent playback mute.
- No independently editable key bindings beyond the upstream modifier selector.
- Settings profiles are per transport serial, not unified device identity.
- Wireless endpoint changes may require selecting a new discovered service;
  saved endpoint retries stop after five attempts. Sessions that fail before
  opening a viewport require manual restart. Closing the upstream viewport
  cleanly does not trigger automatic reopening.
- Main-display screenshots only. No camera/virtual-display screenshot selector.
- Native window file drop is disabled after embedding; use Device Manager.
- Light/dark surfaces, translucent-color navigation, page transitions and hover
  fades are implemented. Motion respects Windows animation preferences and
  Apex's Reduce motion setting. There is no background acrylic blur. System theme
  updates on launch and settings save. No custom borderless main-window mode;
  the studio has fullscreen mode and an embedded borderless viewport.
- Upstream Alt+F can interfere with embedded-window sizing; use Apex Fullscreen.
- USB disconnection means ending a session; Apex cannot physically disconnect
  a USB device. No per-device Android trust revocation API is provided.
- Sensitive ADB keys use ADB's normal Windows user profile, not Apex-managed
  encrypted portable storage. IP/profile/history data are ordinary local JSON.
- No crash dump collection, installer, code-signing certificate, automatic
  updates, clean-VM certification or Windows 10-specific test was available.
- Windows Application Control blocked the final rebuilt Apex.Core.dll on the
  build PC (0x800711C7). The redesigned app compiles, but final GUI testing and
  execution on this policy-enforced machine are blocked. An earlier rebuilt
  test runner was also blocked. No security settings were changed. Environments
  requiring signed applications need an appropriately trusted signing/release
  process; this unsigned build is not certified to run under those policies.
- Normal orderly shutdown is tested. Abrupt OS termination may leave ADB's
  background server running and can damage the last unfinished recording.

Driver installation is outside normal portable operation and may need the
manufacturer's installer. Network firewalls must allow the user's intended
local debugging connection. Android must always authorize this computer.
