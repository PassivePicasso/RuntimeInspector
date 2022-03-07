# Runtime Inspector for BepInEx

Enable the Runtime Inspector overlay by pressing I
The key to open the Inspector can be changed using the config file: BepInEx/configs/com.PassivePicasso.RuntimeInspectorPlugin.cfg

Additionally default widths for the Hierarchy and Inspector can be set in the configuration file.

Inspector and Hierarchy can be resized, floated, and docked anywhere on the game screen.

Drag and drop the Tabs to move the panels around the screen.

![Runtime Inspector Docking](https://i.imgur.com/VCqxz0M.gif)

# Third Party Notices

Runtime Inspector for BepInEx is a BepInPlugin that configures and launches [yasirkula's RuntimeInspector](https://github.com/yasirkula/UnityRuntimeInspector)

Layout is handled using [yasirkula's UnityDynamicPanels](https://github.com/yasirkula/UnityDynamicPanels) for docking and rafting of Inspector and Hierarchy

# Changes

### 3.0.0
* Updated to BepInEx 5.4.1902

### 2.0.4 
* Fix Default Width settings not being honored
* Add Drag border
* Make Tab more visible and readable

### 2.0.3 - Fix ShowInspector Key toggling Inspector invisibility while keyboard focus is in a text box

### 2.0.2 - Add default config file

### 2.0.1 - Fix Gif in readme

### 2.0.0 - Rewrite

This update rewrites the Runtime Inspector integration from scratch to provide a signficantly improved experience without the baggage of a common wrapper library.