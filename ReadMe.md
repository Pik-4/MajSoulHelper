# Maj-Soul Helper

This is a maj-soul plugin based on BepInEx6. The plugin source code is located at github.com/Pik-4/MajSoulHelper, the plugin will not collect any information about you; the project follows `AGPL-3.0`, and is used for learning and research only.

Initial implementation, currently only supports [Steam](https://store.steampowered.com/app/1329410/_/) version. Testing is limited to brand-new accounts only. We assume no responsibility for account suspensions. 

### Realized Functions

1. Support for Gear Fast and Slow 8x speed (settings allow expansion to Fast and Slow 32x)

2. Allow modifying game frame rate. (unlock vsync)

3. Unlocks all characters and skins. (only local)

### Setup

BepInEx IL2CPP plugin. Plugin config page at http://127.0.0.1:23333/. After modifying the configuration, you may need to restart the game.(Need Fix.)

### Shortcut keys

1. Up arrow doubles animation speed 
2. Down arrow halves animation speed 
3. Left arrow resets to default speed 
4. Right arrow sets to 4x or 8x speed 
5. NumPad Plus key increases frame rate by 60fps 
6. NumPad Minus key decreases frame rate by 60fps 

Special note: If monitor refresh rate is below 60Hz, initial frame rate defaults to 120fps

### Groups

Telegram @MajSoulMod (**Suggest and Recommend**)

### Reference

1. [HsMod](https://github.com/Pik-4/HsMod) (AGPL-3.0)

2. [MajsoulMax](https://github.com/Avenshy/MajsoulMax) (GPL-3.0)

