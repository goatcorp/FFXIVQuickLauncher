# FFXIVQuickLauncher [![Actions Status](https://github.com/goaaats/FFXIVQuickLauncher/workflows/Build%20XIVLauncher/badge.svg)](https://github.com/goaaats/FFXIVQuickLauncher/actions) [![Discord Shield](https://discordapp.com/api/guilds/581875019861328007/widget.png?style=shield)](https://discord.gg/3NMcUV5)

A faster launcher for Final Fantasy XIV.

<img src="https://github.com/goaaats/FFXIVQuickLauncher/blob/master/images/screenshot.png?raw=true" alt="drawing" width="800"/>

## Why?

The original FFXIV launcher is slow, tedious, kinda ugly and cannot save your password. This project aims to fix that and add some QoL features to the game that were not there before, such as:

* Auto-login
* Launching tools like ACT when starting the game
* Discord Rich Presence
* Fast in-game market board price checks
* Integrity check for game files
* Chat filtering
* Chat bridge to Discord
* Discord notifications for Duty Finder, fates, retainer sales, etc.

Check the settings page and use the /xlhelp command in-game to see available commands.

## How to install

[Download the latest "Setup.exe" from the releases](https://github.com/goaaats/FFXIVQuickLauncher/releases/latest) page and run it. XIVLauncher will start and will be installed to your start menu.
To uninstall, you can use the Windows Programs & Apps menu or right click XIVLauncher in your start menu.

## Plugin API

To make your own in-game plugins for XIVLauncher, check out the [API documentation](https://goaaats.github.io/Dalamud/api/index.html).

As an example, check out the [market board plugin](https://github.com/goaaats/Dalamud.MbPlugin).

If you want to contribute to the plugin API itself, you can check it out [here](https://github.com/goaaats/Dalamud).

Compiled plugins go into the ``%AppData%\XIVLauncher\plugins`` folder.
Special thanks to Mino for his hooking base!

## Any questions?

[Please check out the FAQ](https://github.com/goaaats/FFXIVQuickLauncher/wiki/FAQ), you may find what you need there.

<br>
<br>

## Disclaimer
As with all of my stuff, this is technically not in line with Square Enix TOS - I've tried to make it as undetectable as possible and no one, as far as I know, has gotten into trouble for using this - but it always could be a possibility.
