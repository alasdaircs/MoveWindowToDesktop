# MoveWindowToDesktop

Tiny tray app: hotkeys that move the active window to the adjacent virtual desktop on Windows 11.
Replaces Eun/MoveToDesktop (2016, archived 2020), which no longer works on Windows 11.

| Hotkey | Action |
|---|---|
| Win+Ctrl+Alt+Left / Right | move window to the neighbouring desktop and follow it |
| Win+Ctrl+Alt+Shift+Left / Right | move window, stay on this desktop |

Win+Alt+Arrow is not used because Windows 11 itself owns those (snap layouts), and
`RegisterHotKey` fails with error 1409 for anything already owned by the OS.

## VirtualDesktopAccessor.dll

Desktop access is via [VirtualDesktopAccessor.dll](https://github.com/Ciantic/VirtualDesktopAccessor)
(MIT, Rust), which wraps Microsoft's undocumented virtual-desktop COM interfaces. Those interfaces
change between Windows feature updates, so the DLL is built per Windows generation.

The DLL is **vendored**: the binary is committed in `MoveWindowToDesktop/` and referenced from the
csproj as `Content`, then bundled into the single-file exe (`IncludeNativeLibrariesForSelfExtract`).
The C# side calls it with plain `DllImport("VirtualDesktopAccessor.dll")`. There is no package feed
and no automatic update; it is pinned by whatever file is checked in.

| Vendored release | Supports |
|---|---|
| [2024-12-16-windows11](https://github.com/Ciantic/VirtualDesktopAccessor/releases/tag/2024-12-16-windows11) | Windows 11 24H2 (26100.2605+) and 25H2 (tested on 26200) |

When a Windows feature update breaks it, the app shows a balloon tip on the hotkey. To update:
download the new `VirtualDesktopAccessor.dll` from the releases page, replace the file, bump the
table above, `dotnet publish -c Release`, copy the exe over the deployed one.

## Build

    dotnet publish -c Release

Output: `MoveWindowToDesktop\bin\Release\net10.0-windows\win-x64\publish\MoveWindowToDesktop.exe`
(single file, framework-dependent, native DLL bundled).
