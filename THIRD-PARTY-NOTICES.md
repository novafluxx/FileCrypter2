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

## Microsoft.Extensions Libraries

FileCrypter receives Microsoft.Extensions.DependencyInjection.Abstractions and
Microsoft.Extensions.Logging.Abstractions transitively through Avalonia.

Source: https://github.com/dotnet/runtime
License: MIT
Copyright: Copyright (c) Microsoft Corporation. All rights reserved.

## Microsoft.IO.RecyclableMemoryStream

FileCrypter receives Microsoft.IO.RecyclableMemoryStream transitively through
Avalonia.

Source: https://github.com/microsoft/Microsoft.IO.RecyclableMemoryStream
License: MIT
Copyright: Copyright (c) Microsoft Corporation. All rights reserved.

## Tmds.DBus.Protocol

FileCrypter receives Tmds.DBus.Protocol transitively through Avalonia on
platforms that use D-Bus integration.

Source: https://github.com/tmds/Tmds.DBus
License: MIT
Copyright: Copyright (c) Tom Deseyn

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

PREAMBLE

The goals of the Open Font License (OFL) are to stimulate worldwide
development of collaborative font projects, to support the font creation
efforts of academic and linguistic communities, and to provide a free and open
framework in which fonts may be shared and improved in partnership with
others.

The OFL allows the licensed fonts to be used, studied, modified and
redistributed freely as long as they are not sold by themselves. The fonts,
including any derivative works, can be bundled, embedded, redistributed and/or
sold with any software provided that any reserved names are not used by
derivative works. The fonts and derivatives, however, cannot be released under
any other type of license. The requirement for fonts to remain under this
license does not apply to any document created using the fonts or their
derivatives.

DEFINITIONS

"Font Software" refers to the set of files released by the Copyright Holder(s)
under this license and clearly marked as such. This may include source files,
build scripts and documentation.

"Reserved Font Name" refers to any names specified as such after the copyright
statement(s).

"Original Version" refers to the collection of Font Software components as
distributed by the Copyright Holder(s).

"Modified Version" refers to any derivative made by adding to, deleting, or
substituting -- in part or in whole -- any of the components of the Original
Version, by changing formats or by porting the Font Software to a new
environment.

"Author" refers to any designer, engineer, programmer, technical writer or
other person who contributed to the Font Software.

PERMISSION & CONDITIONS

Permission is hereby granted, free of charge, to any person obtaining a copy of
the Font Software, to use, study, copy, merge, embed, modify, redistribute, and
sell modified and unmodified copies of the Font Software, subject to the
following conditions:

1) Neither the Font Software nor any of its individual components, in Original
or Modified Versions, may be sold by itself.

2) Original or Modified Versions of the Font Software may be bundled,
redistributed and/or sold with any software, provided that each copy contains
the above copyright notice and this license. These can be included either as
stand-alone text files, human-readable headers or in the appropriate
machine-readable metadata fields within text or binary files as long as those
fields can be easily viewed by the user.

3) No Modified Version of the Font Software may use the Reserved Font Name(s)
unless explicit written permission is granted by the corresponding Copyright
Holder. This restriction only applies to the primary font name as presented to
the users.

4) The name(s) of the Copyright Holder(s) or the Author(s) of the Font Software
shall not be used to promote, endorse or advertise any Modified Version, except
to acknowledge the contribution(s) of the Copyright Holder(s) and the
Author(s) or with their explicit written permission.

5) The Font Software, modified or unmodified, in part or in whole, must be
distributed entirely under this license, and must not be distributed under any
other license. The requirement for fonts to remain under this license does not
apply to any document created using the Font Software.

TERMINATION

This license becomes null and void if any of the above conditions are not met.

DISCLAIMER

THE FONT SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS
OR IMPLIED, INCLUDING BUT NOT LIMITED TO ANY WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT OF COPYRIGHT, PATENT,
TRADEMARK, OR OTHER RIGHT. IN NO EVENT SHALL THE COPYRIGHT HOLDER BE LIABLE FOR
ANY CLAIM, DAMAGES OR OTHER LIABILITY, INCLUDING ANY GENERAL, SPECIAL,
INDIRECT, INCIDENTAL, OR CONSEQUENTIAL DAMAGES, WHETHER IN AN ACTION OF
CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF THE USE OR INABILITY TO USE
THE FONT SOFTWARE OR FROM OTHER DEALINGS IN THE FONT SOFTWARE.

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
