APEX SCREENLINK 2.0 — Windows x64 portable build

Extract the entire ZIP into a writable folder. Double-click ApexScreenLink.exe.
All runtime, Android server, ADB, video, audio and input dependencies are bundled.
No developer tools, separate scrcpy download, account or internet are required.

USB: Enable USB debugging on the phone, connect a data cable and accept the
Android authorization prompt. Select the device and open Mirror Studio.

Cable-free Wi-Fi (Android 11+): Enable Wireless debugging, open Pair device with
pairing code, and enter the phone IP, PAIRING port and six-digit code on the
Wireless page. Then use the CONNECTION port on the phone's main Wireless
debugging screen to connect. These are generally different ports. Both devices
must be on a network allowing local communication. No initial USB is required.

This build compiles and includes earlier automated/GUI test evidence. The user
reported that the baseline app worked on their phone. Individual hardware
features have not all been independently tested. Windows Application Control
blocked the final redesigned build on the build PC (0x800711C7); it may require
a trusted signing process in policy-enforced environments. Final GUI validation
is incomplete. This is not a production-certification claim.

Read docs/USER-GUIDE.md, docs/KNOWN-LIMITATIONS.md and docs/TEST-RESULTS.md before
relying on this build. Source, build scripts, tests and matching upstream source
are provided in the accompanying ApexScreenLink-v2.0-Source.zip.

Apex is an independent application, not an official Genymobile product.
It embeds an unmodified scrcpy 3.3.4 native rendering window in a WPF studio.
The upstream engine and server are reused under their original licenses.

Settings/history: data/settings.json (beside this executable).
Media: recordings/ and screenshots/, or the folder selected in Settings.
ADB keys: your Windows user's standard .android directory; keys are not placed
in the portable release. Never share your private ADB authorization key.
No telemetry, analytics, cloud service or screen transmission to a third party.

Normal operation runs without administrator rights. A manufacturer USB driver
may be necessary on a computer that has never connected to that phone.
The unsigned executable may show Windows publisher/reputation prompts.
