# Third-Party Notices

This file summarizes third-party software and assets used by FileCrypter.
It is intended to be distributed with release builds.

## .NET

FileCrypter is built on .NET and self-contained releases may include portions of
the .NET runtime and libraries.

Source: https://github.com/dotnet/runtime
License: MIT
Copyright: Copyright (c) .NET Foundation and Contributors

## Avalonia

FileCrypter uses Avalonia for the desktop user interface, including these
Avalonia packages:

- Avalonia
- Avalonia.Desktop
- Avalonia.Fonts.Inter
- Avalonia.Themes.Fluent
- Avalonia.HarfBuzz
- Avalonia.Native
- Avalonia.Skia
- Avalonia.Win32
- Avalonia.X11
- Avalonia.FreeDesktop
- Avalonia.FreeDesktop.AtSpi
- Avalonia.Remote.Protocol
- Avalonia.BuildServices

Source: https://github.com/AvaloniaUI/Avalonia
License: MIT
Copyright: Copyright 2013-2026 (c) The AvaloniaUI Project

## CommunityToolkit.Mvvm

FileCrypter uses CommunityToolkit.Mvvm for MVVM helpers.

Source: https://github.com/CommunityToolkit/dotnet
License: MIT
Copyright: (c) .NET Foundation and Contributors. All rights reserved.

## Konscious.Security.Cryptography

FileCrypter uses Konscious.Security.Cryptography.Argon2 and its Blake2
dependency for password-based key derivation.

Source: https://github.com/kmaragon/Konscious.Security.Cryptography
License: MIT
Copyright: (c) 2024 Keef Aragon

## ZstdSharp.Port

FileCrypter uses ZstdSharp.Port for Zstandard compression support.

Source: https://github.com/oleg-st/ZstdSharp
License: MIT
Copyright: Copyright Oleg Stepanischev 2026

## SkiaSharp

FileCrypter uses SkiaSharp through Avalonia's rendering stack. Release builds
may include SkiaSharp managed and native assets.

Source: https://github.com/mono/SkiaSharp
License: MIT
Copyright:

- Copyright (c) 2015-2016 Xamarin, Inc.
- Copyright (c) 2017-2018 Microsoft Corporation.
- (c) Microsoft Corporation. All rights reserved.

## HarfBuzzSharp

FileCrypter uses HarfBuzzSharp through Avalonia's text rendering stack. Release
builds may include HarfBuzzSharp managed and native assets.

Source: https://github.com/mono/SkiaSharp
License: MIT
Copyright:

- Copyright (c) 2015-2016 Xamarin, Inc.
- Copyright (c) 2017-2018 Microsoft Corporation.
- (c) Microsoft Corporation. All rights reserved.

## MicroCom.Runtime

FileCrypter uses MicroCom.Runtime transitively through Avalonia.

Source: https://github.com/kekekeks/MicroCom
License: MIT
Copyright: Copyright 2021 (c) Nikita Tsukanov

## Fluent UI System Icons

Some sidebar icon path data is adapted from Fluent UI System Icons.

Source: https://github.com/microsoft/fluentui-system-icons
License: MIT
Copyright: Copyright (c) 2020 Microsoft Corporation

## Inter Font

FileCrypter uses the Inter font through Avalonia.Fonts.Inter.

Source: https://github.com/rsms/inter
License: SIL Open Font License, Version 1.1
Copyright: Copyright (c) 2016 The Inter Project Authors

## ANGLE

Windows release builds may include Avalonia.Angle.Windows.Natives, which
contains ANGLE native binaries used by Avalonia.

Source: https://github.com/AvaloniaUI/angle/
License: BSD-style license
Copyright: Copyright 2018 The ANGLE Project Authors. All rights reserved.

## MIT License

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

## SIL Open Font License, Version 1.1

This Font Software is licensed under the SIL Open Font License, Version 1.1.
The full license text and FAQ are available from:

https://openfontlicense.org/open-font-license-official-text/

## ANGLE BSD-Style License

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

Redistributions of source code must retain the above copyright notice, this
list of conditions and the following disclaimer.

Redistributions in binary form must reproduce the above copyright notice, this
list of conditions and the following disclaimer in the documentation and/or
other materials provided with the distribution.

Neither the name of TransGaming Inc., Google Inc., 3DLabs Inc. Ltd., nor the
names of their contributors may be used to endorse or promote products derived
from this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE
FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
