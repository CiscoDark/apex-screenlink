# Contributing

Bug reports should include the Apex version, Windows version, phone model,
Android version, USB or wireless transport, and concise reproduction steps.
Do not post pairing codes, ADB private keys, personal recordings or unredacted
diagnostic logs.

Before submitting a change, build the desktop app and run the core tests in a
permitted development environment. For native-window changes, run the SDL GUI
harness and document the Windows/DPI configuration. Describe physical-device
coverage precisely; do not infer a hardware pass from a mock-process test.

Keep Android's authorization model intact and preserve third-party licenses.
Do not add telemetry, hidden remote access or runtime dependency downloads.
