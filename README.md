# BeatnikProject2019
Relive your nostalgia with the Beatnik Audio Engine (Windows Only)
## Status:
- Beta
  - Basic functionality should work, but glitches or bugs are expected to occur.
  
## Requirements:
- Windows XP(?) or newer (should work on any windows supporting .NET since the Beatnik OCX worked on 98, Tested on Windows 10 x64)
- .NET Framework 4.0
- Admin access (because Beatnik is hardcoded to GetWindowsDirectoryA() + "\patches.hsb")

## Features:
- Play Beatnik RMF, MIDI, or MIDI Karaoke Files using classic Beatnik Soundbanks
- Basic MIDI Controls, such as Transpose, Tempo, Volume, Seeking, Channel Mute
- Ability to switch soundbanks almost seamlessly
- Rare soundbanks including WebTV Classic and WebTV Plus

## How to use:
- Unrar archive
- Run BXPlayerGUI
- If the Open Button is greyed out, and it says "Current Patch Bank: None", or you want to change the bank
  - Click "Patch Bank Switcher"
  - Choose a patchset and apply it
- Load MIDI and enjoy!

## Note regarding Junction
As stated in the requirements, Beatnik looks for the patches.hsb file in your Windows Directory.
Therefore we will always need admin at least once. But if we install a junction (kinda like a symlink for those linux folks),
then we can just point %WINDIR%\patches.hsb to our local folder, thus not needing admin each time you switch a patchset in the future.

However, you still need to restart the program due to the Beatnik Library not releasing the patchset.
Because of this, I have added features to help the program resume where you left off, if you choose "Yes"
when the patch bank switcher asks if you would like to run the player again.

## Note regarding Firewalls:

Opening .kar files runs a internal "HTTP Server" (not standard by any means),
because Beatnik won't open a .kar even though its just a .mid. The miniHTTP is used
to make Beatnik think its getting a .mid, but we send it a .kar, without having to modify your
filesystem or create temporary files. The miniHTTP is also used to send files in which Beatnik
chokes on the filename, such as files with ```[]``` in them. Therefore, if you get any prompts
regarding a firewall listening on a port (should be localhost!), then this is why.

## Screenshot
![](https://i.imgur.com/YOnOuNJ.png)
