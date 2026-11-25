# Build and reproduce

The end user needs no toolchain. Developers need Windows x64 and .NET SDK
8.0.408. `global.json` pins the SDK. The self-contained WPF target pins the
runtime bundled with that SDK (8.0.15). QRCoder 1.6.0 is compiled from its
included, unmodified C# source through Apex.QrCodec, targeting the portable
PNG renderer without System.Drawing. There are no third-party NuGet runtime
packages to restore. The upstream MIT license and source archive are included.

From the extracted source directory, using PowerShell:

```powershell
./scripts/build.ps1 -Dotnet 'C:/path/to/dotnet.exe'
./scripts/verify-package.ps1 -Directory './artifacts/ApexScreenLink'
```

The source archive includes the original Windows engine ZIP. The build script
checks its published SHA-256 before extraction. If absent, it fetches that exact
release. SDK runtime packs may be downloaded on the first restore. Subsequent
offline builds work with those packs cached. Build outputs are reproducible from
pinned inputs; byte-for-byte determinism of the ZIP container is not asserted.

Tests:

```powershell
dotnet run --project tests/Apex.Tests/Apex.Tests.csproj -c Release
dotnet publish tests/Apex.GuiTests/Apex.GuiTests.csproj -c Release -o artifacts/gui-tests
Copy-Item artifacts/ApexScreenLink/engine/SDL2.dll artifacts/gui-tests/
./artifacts/gui-tests/Apex.GuiTests.exe
./artifacts/ApexScreenLink/ApexScreenLink.exe --smoke-test
```

The GUI harness starts a clearly labeled synthetic SDL window, tests embedding,
resize and orderly shutdown. It does not simulate a successful Android session.
`--smoke-test` exercises all eight application pages and writes smoke-result.txt.
The regular test runner launches itself as a mock ADB process to test stdin,
stdout, binary streams, failures, argument isolation and timeouts.

To rebuild scrcpy, see `third-party/scrcpy/doc/build.md` and
`third-party/scrcpy/release/build_windows.sh`. Its release script cross-compiles
the native client, linking FFmpeg, SDL and libusb; the Java server uses Gradle and
the Android SDK. Matching dependency source archives are in third-party/source.
This build used the official unmodified upstream binary instead of rebuilding
that engine. Apex has no private protocol fork.

To rebuild or replace an LGPL library, follow its included source/build files,
place the resulting compatible DLL in engine/, and update its hash entry in
dependency-hashes.json. The manifest is an accidental-corruption check, not a
signature or restriction on modifying/relinking the application. Debugging and
reverse engineering for library modifications are permitted by the applicable
licenses. Keep copyright and license notices with redistributions.

Publish into a clean directory, copy docs/licenses, then ZIP the entire
ApexScreenLink directory. Never distribute development data/settings.json,
recordings, ADB authorization keys or test logs containing personal identifiers.
