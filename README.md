# PeasAPI

PeasAPI is an API for developing *Among Us* mods, designed to provide convenient functional support for mod developers and simplify the mod development process.

## Features

1. **Role Management**: Offers rich role-related functionalities, supporting the creation, assignment, and management of custom roles. It enables easy implementation of team judgment between roles, role attribute settings, and more.

2. **Game Mode Expansion**: Supports custom game modes, allowing switching and configuration of different game modes within the game.

3. **Network Communication**: Integrates network RPC communication capabilities to facilitate synchronization of game states, role information, and other data among players.

4. **Update Management**: Equipped with automatic update checking. It can detect updates for related mods, prompt users, and support fetching updates from GitHub repositories.

5. **Configuration Options**: Allows developers to configure the plugin through configuration files, including log switches, custom server configurations, etc.

6. **Extended Tools**: Provides various practical extension methods such as player object acquisition, color processing, vector operations, etc., to simplify the development process.

7. **Watermark Management**: Supports adding custom watermark information to the game interface, with the ability to adjust the watermark position and content according to the game state.

## Installation Instructions

1. Ensure the BepInEx framework is installed.
2. Place `PeasAPI.dll` into the `BepInEx\plugins` folder under the *Among Us* game directory.
3. Launch the game to load the plugin.

## Usage Method

Search for PeasAPI-R in the Nuget package manager of your programming software to add the dependency to your project for use.

## License

This project is open-source under the GNU AFFERO GENERAL PUBLIC LICENSE Version 3. For details, please refer to the [LICENSE](LICENSE) file.

## Disclaimer

This mod is not affiliated with *Among Us* or Innersloth LLC, and the content contained herein is not endorsed or sponsored by Innersloth LLC. Portions of the materials are the property of Innersloth LLC. © Innersloth LLC.