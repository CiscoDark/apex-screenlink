# Dependencies and redistribution

| Component | Pinned version | License / provenance |
| --- | --- | --- |
| Apex original application | 2.0.0 | MIT, root LICENSE |
| QRCoder / Apex.QrCodec | 1.6.0 | MIT, compiled from included source for offline QR rendering |
| scrcpy native client and Android server | 3.3.4 | Apache-2.0; unmodified official Windows release |
| Android Debug Bridge + AdbWinApi/AdbWinUsbApi | 36.0.0-13206524 | AOSP and component notices; licenses/ADB-NOTICE.txt |
| FFmpeg shared libraries | 7.1.1 | LGPL-2.1-or-later; upstream build does not enable GPL/nonfree |
| SDL2 | 2.32.8 | zlib license |
| libusb | 1.0.29 | LGPL-2.1-or-later |
| dav1d, statically linked into FFmpeg | 1.5.0 | BSD-2-Clause |
| zlib support in upstream FFmpeg | upstream toolchain dependency | zlib license; see upstream toolchain/source notes |
| .NET + Windows Desktop runtime | 8.0.15 | Microsoft's binary redistribution terms, MIT source and third-party notices |

Exact engine file hashes are in dependency-hashes.json. The .NET runtime is
deployed by dotnet publish --self-contained, including WPF native libraries and
runtime licensing files. ADB keeps Google's original notices. The downloaded
archives were checked against upstream release/API digests or the pinned
scrcpy dependency scripts. This is integrity verification, not a publisher
signature audit or a full legal analysis.

Matching scrcpy, FFmpeg, libusb, SDL and dav1d source is distributed alongside
the binary release in ApexScreenLink-v2.0-Source.zip. The upstream scrcpy build
scripts record exact configure flags, including the disabled GPL/nonfree and
network-dependent FFmpeg features. The original platform-tools archive is also
included for provenance/notices. The source package includes no private keys.

Upstream locations:
- https://github.com/Genymobile/scrcpy/tree/v3.3.4
- https://ffmpeg.org/releases/ffmpeg-7.1.1.tar.xz
- https://github.com/libsdl-org/SDL/tree/release-2.32.8
- https://github.com/libusb/libusb/tree/v1.0.29
- https://code.videolan.org/videolan/dav1d/-/tree/1.5.0
- https://android.googlesource.com/platform/packages/modules/adb/
- https://github.com/dotnet/runtime/tree/v8.0.15
- https://github.com/dotnet/wpf/tree/v8.0.15

No third-party trademarks or logos are used as Apex branding. scrcpy attribution
is retained in documentation and its bundled binary. Apex is not endorsed by
Genymobile, Google, Microsoft or the dependency authors.
