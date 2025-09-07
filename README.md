# AdaptiveFPS

Adaptive framerate cap for FFXIV (Dalamud):
- In combat: 60 fps (configurable)
- Out of combat: your chosen cap; left-click the status bar entry to cycle Main → 60 → 30
- Right-click the entry to toggle Adaptive mode on/off

Notes
- No ImGui UI. Status bar only.
- Requires Dalamud dev environment; the SDK supplies FFXIVClientStructs and services.

Build
- Open `AdaptiveFPS.sln` in your IDE with Dalamud dev setup, or run `dotnet build -c Release` from that environment.

Install (dev)
- In-game: `Dalamud > Settings > Experimental > Enable dev plugin loading`, then use “Install from Disk” and select the built folder.

