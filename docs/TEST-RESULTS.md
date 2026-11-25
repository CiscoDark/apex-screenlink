# Actual test results — 25 November 2026

Host: Windows x64, build 10.0.26300. SDK: .NET 8.0.408.

| Check | Observed result |
| --- | --- |
| Final C#/WPF compilation | PASS, zero warnings and errors |
| Final self-contained publication | PASS |
| Core automated suite before final refinements | PASS, 52 tests, 0 failures |
| Later core-suite rerun | BLOCKED by Windows Application Control, 0x800711C7 |
| Pre-redesign packaged application startup | PASS |
| All eight pre-redesign navigation pages | PASS, automated GUI smoke test |
| Invalid wireless input in GUI | PASS, clear IP validation error |
| Pre-redesign theme inspection | Found background mismatch; corrected in source |
| SDL/WPF native embedding harness | PASS |
| Native viewport resizing harness | PASS |
| SDL orderly close harness | PASS |
| Final redesigned application GUI verification | BLOCKED: Application Control rejected rebuilt Apex.Core.dll |
| Bundled engine SHA-256 checks | PASS |
| Engine and ADB version execution | PASS |
| PE architecture | PASS: Apex and scrcpy x64; official ADB helper x86 under WoW64 |
| Direct PE import scan | PASS, 473 PE candidates, no unresolved imports on this host |
| ADB/mDNS discovery | PASS, live Android services and device state observed |
| Attempted physical recording by agent | DID NOT START: phone endpoint changed and became offline |
| Physical-device test by user | User reported “it works fine”; specific feature coverage unspecified |
| USB, audio playback, recordings, input, rotation, long-running stability individually | Not independently verified |
| Clean Windows VM, Windows 10, mixed-DPI, long/non-ASCII deployment paths | Not verified |
| Package ZIP integrity | See PACKAGE-VERIFICATION.txt accompanying the ZIPs |

The 52-test run included device/transport parsing, unauthorized/offline handling,
IPv4/IPv6 validation, port bounds and injection-shaped input, six-digit codes,
QR protocol/entropy/expiry/matching/PNG generation, profile argument generation,
bounded retries, settings round-trip, deduplication, no pairing-code persistence,
mock process success/failure, stdin pairing, argument isolation, binary pipes,
timeout process cleanup and dependency tampering.

The GUI harness used the real bundled SDL2 2.32.8 library to create a clearly
labeled synthetic window. It tested HwndHost parenting, native client dimensions
after resize and WM_CLOSE termination. It did not emulate a successful Android
session or video decode. Earlier passing results must not be interpreted as a
full rerun of the final redesigned build.

Windows Code Integrity events 3033/3077 identified an enforced signing policy;
the later test runner and final app assembly were refused. Compilation and
static package inspection remained available. The security policy was not
disabled or bypassed. A trusted signing process or a permitted test environment
is needed to finish runtime verification of the final unsigned build.

Raw successful core and native integration results are in test-evidence/.
No user recordings, screenshots, pairing secrets or private ADB keys are included.
