# MoveWindowToDesktop

Tiny tray app: hotkeys that move the active window to the adjacent virtual desktop on Windows 11.
Replaces Eun/MoveToDesktop (2016, archived 2020), which no longer works on Windows 11.

| Hotkey | Action |
|---|---|
| Win+Ctrl+Alt+Left / Right | move window to the neighbouring desktop and follow it |
| Win+Ctrl+Alt+Shift+Left / Right | move window, stay on this desktop |

Win+Alt+Arrow is not used because Windows 11 itself owns those (snap layouts), and
`RegisterHotKey` fails with error 1409 for anything already owned by the OS.

Desktop access is via [VirtualDesktopAccessor.dll](https://github.com/Ciantic/VirtualDesktopAccessor)
(MIT), which tracks Microsoft's undocumented virtual-desktop COM interfaces. When a Windows feature
update breaks it, the app shows a balloon tip; drop the newer DLL into the project and republish.

## Build

    dotnet publish -c Release

Output: `MoveWindowToDesktop\bin\Release\net10.0-windows\win-x64\publish\MoveWindowToDesktop.exe`
(single file, framework-dependent, native DLL bundled).
