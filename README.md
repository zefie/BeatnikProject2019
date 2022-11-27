# BeatnikProject2019
Relive your nostalgia with the Beatnik Audio Engine (Windows Only)
## Status:
- Beta
  - Basic functionality should work, but glitches or bugs are expected to occur.
  
## Requirements:
- Windows XP or newer (should work on any windows supporting .NET since the Beatnik OCX worked on 98, Tested on Windows 10 x64)
  - May have minor graphical issues on Windows XP but functions propertly ([v1.1.7142.6918](https://github.com/zefie/BeatnikProject2019/releases/tag/v1.1.7142.6918) tested on Pentium 3 with WinXP and 256MB RAM)
- .NET Framework 4.0

## Features:
- Play Beatnik RMF, MIDI, MIDI Karaoke Files using classic Beatnik Soundbanks
- Basic MIDI Controls, such as Transpose, Tempo, Volume, Seeking, Channel Mute
- Ability to switch soundbanks almost seamlessly
- 17 Soundbanks to choose from, including rare Soundbanks from WebTV and Nokia devices.
- User Configuration Support to retain common settings
- Drag-n-Drop Support
- File association support (Use "Open With", then check "Always Open With" after browsing to BXPlayerGUI.exe)

## How to use:
- Unrar archive
- Run BXPlayerGUI
- If the Open Button is greyed out, and it says "Current Patch Bank: None", or you want to change the bank
  - Click "Patch Bank Switcher"
  - Choose a patchset and apply it
- Load MIDI and enjoy!

## Note regarding Firewalls:
Opening .kar files runs a internal "HTTP Server" (not standard by any means),
because Beatnik won't open a .kar even though its just a .mid. The miniHTTP is used
to make Beatnik think its getting a .mid, but we send it a .kar, without having to modify your
filesystem or create temporary files. The miniHTTP is also used to send files in which Beatnik
chokes on the filename, such as files with ```[]``` in them. Therefore, if you get any prompts
from your firewall regarding the application listening on a port (should be localhost!), then this is why.

## Screenshot
![](https://archive.midnightchannel.net/zefie/media/Images/Miscellaneous/BXPlayerGUI_v1.1.7136.34589.png)
