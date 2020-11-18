# XIVLauncher [![Actions Status](https://github.com/goaaats/FFXIVQuickLauncher/workflows/Build%20XIVLauncher/badge.svg)](https://github.com/goaaats/FFXIVQuickLauncher/actions) [![Discord Shield](https://discordapp.com/api/guilds/581875019861328007/widget.png?style=shield)](https://discord.gg/3NMcUV5) [![Crowdin](https://badges.crowdin.net/ffxivquicklauncher/localized.svg)](https://crowdin.com/project/ffxivquicklauncher) <a href="https://github.com/goatcorp/FFXIVQuickLauncher/releases"><img src="https://github.com/goatcorp/FFXIVQuickLauncher/raw/master/XIVLauncher/Resources/logo.png" alt="XL logo" width="100" align="right"/></a> 

XIVLauncher (abbreviated as XL) is a faster launcher for Final Fantasy XIV, with various available addons and enhancements to the game!

<p align="center">
  <a href="https://github.com/goatcorp/FFXIVQuickLauncher/releases">
    <img src="https://i.imgur.com/jxqlaAY.png" alt="drawing" width="500"/>
  </a>
</p>

## Why?

The original FFXIV launcher is slow, tedious, kinda ugly and cannot save your password. This project aims to fix that and add some QoL features to the game that were not there before, such as:

* Auto-login
* Fast patching
* Discord Rich Presence
* Fast in-game market board price checks
* Chat filtering
* Chat bridge to Discord
* Discord notifications for duties, retainer sales, etc.

Check the settings page and use the /xlhelp command in-game to see available commands.

## How to install

[Download the latest "Setup.exe" from the releases](https://github.com/goatcorp/FFXIVQuickLauncher/releases/latest) page and run it. XIVLauncher will start and will be installed to your start menu.
To uninstall, you can use the Windows Programs & Apps menu or right click XIVLauncher in your start menu.

## Plugin API

XIVLauncher lets you use many community-created plugins that improve your game. Please check [this site](https://goatcorp.github.io/DalamudPlugins/plugins) for a list of them.
<br>To make your own in-game plugins for XIVLauncher, check out the [API documentation](https://goatcorp.github.io/Dalamud/api/index.html) and the [sample plugin](https://github.com/goatcorp/SamplePlugin).

If you want to contribute to the plugin API itself, you can check it out [here](https://github.com/goatcorp/Dalamud).<br>Special thanks to Mino for his hooking base!

### Isn't this cheating?

We don't think so - our official guideline for plugins on this launcher is this:<br>

Make sure that your plugin does not directly interact with the game servers in a way that is...
<br>a) *outside of specification*, as in allowing the player to do things or submit data to the server that would not be possible by normal means or by a normal player on PC or PS4.
<br>b) *automatic*, as in polling data or making requests without direct interaction from the user which could create unwanted load on the server or give away that XIVLauncher is being used.

We feel like that this offers developers the __freedom to improve the game's functionality__ in ways that SE can't, while officially disallowing plugins that can give __unfair advantages to players on other platforms__.

## Any questions?

[Please check out the FAQ](https://github.com/goatcorp/FFXIVQuickLauncher/wiki/FAQ), you may find what you need there.<br>You can also join our discord at [https://discord.gg/3NMcUV5](https://discord.gg/3NMcUV5) and ask our incredibly forthcoming moderator team.

<br>
<br>

## Disclaimer
As with all of my stuff, this is technically not in line with Square Enix TOS - I've tried to make it as undetectable as possible and no one, as far as I know, has gotten into trouble for using this - but it always could be a possibility.
Sentry is used for error tracking, all personal data like usernames or passwords is stripped and will never be stored.

##### Final Fantasy XIV © 2010-2020 SQUARE ENIX CO., LTD. All Rights Reserved. We are not affiliated with SQUARE ENIX CO., LTD. in any way.
