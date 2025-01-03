# Grenade Indicator Plugin for SPT

A lightweight client plugin for SPT mods that provides a heads-up display (HUD) for grenade indicators. The plugin
displays information about the location and distance of grenades relative to the player, improving situational awareness
and enhancing gameplay.

## Features

- **Grenade Indicator:** Visual markers for incoming grenades.
- **Distance Display:** Approximate distance of grenades from the player (visual hint still gotta make it challenging
  for you to evade them).
- **Minimalist Design:** Non-intrusive UI that blends seamlessly with the game's HUD.

## Requirements

- **SPT Version:** Compatible with [SPT](https://www.sp-tarkov.com/) mods.
- **Platform:** PC

## Installation

1. Download the plugin files.
2. Place the plugin folder into your `BepInEx\plugins\GrenadeIndicator` directory.
3. Make sure the image assets that are required for the mod to create HUD elements are in the same folder as the mod
   .dll.
4. Launch SPT to load the plugin.

## Usage

- The grenade indicator will automatically activate when a grenade is detected in proximity.
- Customize the UI settings via the plugin configuration file (if available).

## Planned Features

- More HUD customization.
- Some animation improvements.
- Audio alerts for incoming grenades. (maybe)
- Advanced settings for indicator behavior.

## Acknowledgments

This plugin was inspired by the original mod [**Solarint.GrenadeIndicator
**](https://hub.sp-tarkov.com/files/file/2194-grenade-indicator/). I learned a lot from its code while creating this
plugin, and it served as a foundation for many of the features implemented here.

Special thanks to the following individuals for their invaluable feedback and help in debugging:

- [**Lacyway**](https://github.com/Lacyway)
- [**GrooveypenguinX**](https://github.com/GrooveypenguinX)

Your guidance and support made this plugin possible!

## Contributions

Contributions, suggestions, and bug reports are welcome! Feel free to open an issue or submit a pull request.

## Disclaimer

This plugin is intended for use with SPT mods only. It is not affiliated with or endorsed by Battlestate Games.

## License

This project is licensed under the terms of the [MIT License](LICENSE.md).
