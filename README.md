# XIVLauncher [![Actions Status](https://img.shields.io/github/actions/workflow/status/goatcorp/FFXIVQuickLauncher/ci-workflow.yml?branch=master)](https://github.com/goaaats/FFXIVQuickLauncher/actions) [![Discord Shield](https://discordapp.com/api/guilds/581875019861328007/widget.png?style=shield)](https://discord.gg/3NMcUV5) [![Crowdin](https://badges.crowdin.net/ffxivquicklauncher/localized.svg)](https://crowdin.com/project/ffxivquicklauncher) [![GitHub release (latest by date)](https://img.shields.io/github/v/release/goatcorp/FFXIVQuickLauncher)](https://github.com/goatcorp/FFXIVQuickLauncher/releases/latest) <a href="https://github.com/goatcorp/FFXIVQuickLauncher/releases"><img src="https://github.com/goatcorp/FFXIVQuickLauncher/raw/master/src/XIVLauncher/Resources/logo.png" alt="XL logo" width="100" align="right"/></a> 

Read in: [日本語](https://github.com/goatcorp/FFXIVQuickLauncher/wiki/Info-(Japanese)) | [Français](https://github.com/goatcorp/FFXIVQuickLauncher/wiki/Info-(French)) | [Deutsch](https://github.com/goatcorp/FFXIVQuickLauncher/wiki/Info-(German)) | [Español](https://github.com/goatcorp/FFXIVQuickLauncher/wiki/Info-(Spanish)) | [简体中文](https://github.com/goatcorp/FFXIVQuickLauncher/wiki/Info-(Chinese-Simplified)) | [繁體中文](https://github.com/goatcorp/FFXIVQuickLauncher/wiki/Info-(Chinese-Traditional)) | [한국어](https://github.com/goatcorp/FFXIVQuickLauncher/wiki/Info-(Korean)) | [Italiano](https://github.com/goatcorp/FFXIVQuickLauncher/wiki/Info-(Italian))

XIVLauncher (abbreviated as XL) is a faster launcher for our favorite critically acclaimed MMO, with various available addons and enhancements to the game!

<p align="center">
  <a href="https://github.com/goatcorp/FFXIVQuickLauncher/releases">
    <img src="https://raw.githubusercontent.com/goatcorp/FFXIVQuickLauncher/master/misc/screenshot.png" alt="drawing" width="500"/>
  </a>
</p>

## Why?

The original launcher is slow and cannot save your password. This project aims to fix that and add some QoL features to the game, such as:

* Auto-login
* Fast patching
* Discord Rich Presence
* Fast in-game market board price checks
* Chat filtering
* Chat bridge to Discord
* Discord notifications for duties, retainer sales, etc.

## How to install the launcher

[Download the latest "Setup.exe" from the releases](https://github.com/goatcorp/FFXIVQuickLauncher/releases/latest) page and run it. XIVLauncher will start and will be installed to your start menu.
To uninstall, you can use the Windows Programs & Apps menu or right click XIVLauncher in your start menu.

⚠ <b>Attention!</b> If you receive errors during the installation or if the launcher doesn't work correctly, make sure to <b>check your antivirus</b> first and disable it for XIVLauncher. Many commercial antivirus programs detect XIVLauncher as a false positive. You can check for yourself on VirusTotal. If you need any help with this, please [check our FAQ](https://goatcorp.github.io/faq/xl_troubleshooting#q-how-do-i-whitelist-xivlauncher-and-dalamud-so-my-antivirus-leaves-them-alone) or join our Discord channel.

## On Steam Deck and Desktop Linux
XIVLauncher now has a native Linux version that works on Steam Deck and Desktop Linux - no more messing around with scripts and command lines, just a few easy steps to install the game and add it to Steam, with a wine version especially tuned to XIV.

Please see a guide here: https://goatcorp.github.io/faq/steamdeck

## How to install plugins
XIVLauncher supports the installation of __plugins/addons__ made by __third-party developers__ in-game.

Open the system menu in-game by pressing escape, and then select "Dalamud Plugins" from there. You can also use the ``/xlplugins`` command in chat.<br>If the command is not found, or the options don't show up, make sure that the in-game addon is enabled in the launcher settings.

## Need any help?
[Please check out our exhaustive FAQ](https://goatcorp.github.io/faq/), you may find what you need there.<br>You can also join our discord at [https://discord.gg/3NMcUV5](https://discord.gg/3NMcUV5) and ask our incredibly forthcoming community.

## Is XIVLauncher safe to use?
We put a lot of effort into ensuring that XIVLauncher is safe to use for everyone.<br>Please read through our [FAQ entry](https://goatcorp.github.io/faq/xl_troubleshooting#q-are-xivlauncher-dalamud-and-dalamud-plugins-safe-to-use) on this matter if you want to know more.

<br>
<br>

## Plugin API

XIVLauncher lets you use many community-created plugins that improve your game. Please check [this site](https://goatcorp.github.io/DalamudPlugins/plugins) for a list of them.
<br>To make your own in-game plugins for XIVLauncher, check out the [API documentation](https://goatcorp.github.io/Dalamud/api/index.html) and the [sample plugin](https://github.com/goatcorp/SamplePlugin).

If you want to contribute to the plugin API itself, you can check it out [here](https://github.com/goatcorp/Dalamud).

### Is this cheating?

We don't think so - our official guideline for plugins on this launcher is this:<br>

Make sure that your plugin does not directly interact with the game servers in a way that is...
<br>a) *outside of specification*, as in allowing the player to do things or submit data to the server that would not be possible by normal means or by a normal player on PC or PS4.
<br>b) *automatic*, as in polling data or making requests without direct interaction from the user which could create unwanted load on the server or give away that XIVLauncher is being used.
<br>c) *circumvent paywalls*, as in any attempts to bypass restrictions that require purchase to be lifted such as enabling access to mog station emotes.

We feel like that this offers developers the __freedom to improve the game's functionality__ in ways that SE can't, while officially disallowing plugins that can give __unfair advantages over players on other platforms__.

<br>
<br>

## Disclaimer
XIVLauncher is not in-line with the game's ToS. We are doing our best to make it safe to use for everyone, and to our knowledge, no one ever got into trouble for using XIVLauncher, but please be aware that it is a possibility.<br>You can read through our [FAQ entry](https://goatcorp.github.io/faq/xl_troubleshooting#q-are-xivlauncher-dalamud-and-dalamud-plugins-safe-to-use) on this matter if you want to know more.

##### FINAL FANTASY is a registered trademark of Square Enix Holdings Co., Ltd. FINAL FANTASY XIV © 2010-2022 SQUARE ENIX CO., LTD. All Rights Reserved. We are not affiliated with SQUARE ENIX CO., LTD. in any way.
