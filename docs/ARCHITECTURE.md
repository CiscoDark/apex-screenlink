# Architecture and upstream review

Apex is a native WPF desktop application with a bundled, out-of-process scrcpy
engine. Its studio embeds the engine's SDL HWND through HwndHost/SetParent.
This preserves upstream decoding, input, clipboard, audio and recording code
without a fragile reimplementation of its version-specific wire protocol.
It is not a browser UI and does not download an engine at runtime.

WPF was chosen for a self-contained Windows build, native HWND interoperability,
accessibility and asynchronous process management. Qt is not included. This
architecture still uses upstream SDL/FFmpeg for the complete media pipeline;
WPF does not copy or decode video frames. Each session has a separate process
and viewport. ADB uses local-only port 5039 so normal port-5037 tooling is not
deliberately stopped. A single-instance mutex prevents Apex instances competing.

## Reviewed sources at fb6381f5b9bb96f3fa823d899f4c32de2ec84ab3

- `doc/develop.md`, `doc/connection.md`, `doc/video.md`, `doc/shortcuts.md`,
  `doc/windows.md`: protocol, capability and lifecycle documentation.
- `app/src/scrcpy.c`: component graph, SDL setup and termination.
- `app/src/demuxer.c`: codec IDs, 12-byte packet headers, timestamps/config flags.
- `app/src/decoder.c`: FFmpeg packet submission, frames and ownership.
- `app/src/control_msg.h`: control types, clipboard and UHID message constraints.
- `app/src/recorder.c`: MP4/MKV muxers, packet references and timestamps.
- `app/src/screen.c`, `fps_counter.c`, `cli.c`: rendering, DPI, real FPS output
  and supported options.
- `app/deps/*.sh`, `release/build_windows.sh`: pinned dependency versions,
  checksums, FFmpeg feature selection and Windows distribution layout.

Upstream pushes the Android Java server to /data/local/tmp and starts it with
app_process under the authorized ADB shell identity. Video, audio and controls
use separate sockets. The exact server and client versions must match. Android
MediaCodec handles encoding; the client demuxes and decodes with FFmpeg, renders
with SDL and forwards input through the control channel. Recording muxes the
encoded stream without re-encoding. Device rotation changes received dimensions.
Wireless TLS pairing and authorization are delegated to official ADB. Apex never
reimplements, bypasses or stores Android pairing codes.

QR pairing follows AOSP's documented `WIFI:T:ADB;S:...;P:...;;` format. A
cryptographically random service name and secret are encoded locally by
QRCoder. Apex accepts only the matching `_adb-tls-pairing` service, sends the
secret over ADB stdin and expires/cancels the session after two minutes. Only
connect services with the paired phone's address are candidates for subsequent
connection. No QR image or secret is persisted. Protocol reference:
https://android.googlesource.com/platform/packages/modules/adb/+/HEAD/docs/dev/adb_wifi.md

## Original modules

- Apex.Core/Models: device/mDNS parsing, endpoint and profile validation,
  persisted settings, recent sessions, device profiles and retry budget.
- Apex.Core/Processes: asynchronous argument-list-based subprocess execution,
  deadlines, stdin pairing, binary screenshots, ADB facade, dependency hashes.
- Apex.Desktop/MainWindow: eight navigation views, device polling, discovery,
  remembered endpoints, file operations, diagnostics, settings and session list.
- Apex.Desktop/StudioWindow: process/recording lifecycle, controls, real engine
  FPS lines and bounded reconnect attempts.
- Apex.Desktop/NativeHost: native window parenting, resize and focus.

Device scans never overlap. Commands use ProcessStartInfo.ArgumentList with
UseShellExecute=false. Only fixed Android shell subcommands are exposed; user
input is never concatenated into a host shell command. APK installation is
confirmed. Native SDL file dropping is disabled after embedding, so file/APK
transfer uses the app's confirmation-aware Device Manager.

Recording starts/stops by restarting the stream; WM_CLOSE permits upstream
muxers to finalize. An eight-second deadline precedes forced termination, which
is logged because it can damage an unfinished recording. A network loss starts
a new segment on reconnection. Diagnostics are kept in a bounded in-memory log
and written only when explicitly exported. Settings are atomically replaced.

This review informed the integration; it is not an exhaustive security audit of
all scrcpy or dependency source. Known gaps and unverified behavior are explicit
in KNOWN-LIMITATIONS.md and TEST-RESULTS.md.
