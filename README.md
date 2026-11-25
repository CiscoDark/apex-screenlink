# Apex ScreenLink

**Your Android. A larger workspace.**

A portable Windows desktop application for Android mirroring and control,
built with a native WPF interface and the bundled scrcpy engine.

## Download

Get the Windows portable ZIP from [Releases](https://github.com/CiscoDark/apex-screenlink/releases).
Extract the entire folder and open `ApexScreenLink.exe`. No separate scrcpy,
.NET installation, account or subscription is required for normal use.

> **v2.0 is a prerelease.** The redesigned build compiles, but Windows Application
> Control blocked its unsigned assembly on the development PC. Final runtime
> and visual testing are incomplete. An earlier baseline was tested successfully
> by the project owner. See [test results](docs/TEST-RESULTS.md) and
> [known limitations](docs/KNOWN-LIMITATIONS.md) before use. Do not disable
> operating-system security protections to run this build.

## What it includes

- USB discovery and authorization-state reporting.
- Android Wireless Debugging with QR or six-digit pairing and mDNS discovery.
- Native Studio windows, mouse/keyboard control and clipboard support through scrcpy.
- Video quality profiles, audio forwarding, MP4/MKV recording and PNG screenshots.
- File transfer, confirmed APK installation and local capture management.
- Camera, virtual-display and UHID options where Android/device support permits.
- Light/dark themes, streamlined navigation, tabbed settings and optional motion.
- Bundled ADB, Android server, FFmpeg, SDL, libusb and .NET runtime.

Capabilities vary by phone and Android version. Hardware decoding, adaptive
bitrate and seamless transport/recording transitions are not implemented.
The [feature limitations](docs/KNOWN-LIMITATIONS.md) distinguish implementation
from individually verified behavior.

## Connect

**USB:** enable USB debugging, connect a data cable, then approve the computer
on the phone. Select the device and open Mirror Studio.

**Wireless, without USB:** on Android 11+, open Developer options → Wireless
debugging. Choose **Pair with QR code** in Apex, then scan it using the phone's
Wireless debugging QR scanner. Alternatively, use the six-digit pairing form.
The pairing port and connection port are different. Both devices need a local
network that permits communication.

For older Android versions, legacy TCP/IP normally needs an initial authorized
USB connection and is unencrypted. Apex displays a warning before enabling it.

## Privacy

No telemetry, analytics or cloud screen streaming. Captures stay local. Pairing
secrets are temporary and never saved. Android authorization is always required.
ADB manages its key in the Windows user's standard profile. Diagnostic exports
can contain local paths and device identifiers; review them before sharing.

## Build

Windows x64 and **.NET SDK 8.0.408**:

```powershell
./scripts/build.ps1
./scripts/verify-package.ps1 -Directory ./artifacts/ApexScreenLink
```

Read [build instructions](docs/BUILD.md) for the test harnesses and pinned
dependencies. The repository contains application and engine source. Matching
dependency source archives and the original engine distribution are included in
the full Source ZIP attached to the release. The build script downloads the
checksum-pinned engine if that archive is absent.

## Documentation

- [User guide](docs/USER-GUIDE.md)
- [Architecture](docs/ARCHITECTURE.md)
- [Design notes](docs/DESIGN-NOTES.md)
- [Test results](docs/TEST-RESULTS.md)
- [Dependencies and licenses](docs/DEPENDENCIES.md)
- [Known limitations](docs/KNOWN-LIMITATIONS.md)

## License and attribution

Original Apex code is [MIT licensed](LICENSE). Third-party components retain
their own licenses in [licenses/](licenses/).

The streaming engine is [scrcpy](https://github.com/Genymobile/scrcpy), by
Genymobile and contributors, reused unmodified under Apache-2.0. Apex is an
independent project and is not endorsed by Genymobile, Google or Microsoft.
