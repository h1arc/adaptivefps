set -e
NEW="/Users/mario/Documents/projects/adaptivefps"
SRC="/Users/mario/Documents/projects/template"
# 1) create new folder
mkdir -p "$NEW"
# 2) copy AdaptiveFPS project and solution
rsync -a --delete --exclude '.git' "$SRC/AdaptiveFPS/" "$NEW/AdaptiveFPS/"
cp -f "$SRC/AdaptiveFPS.sln" "$NEW/AdaptiveFPS.sln"
# 3) add .gitignore and README
cat > "$NEW/.gitignore" << 'EOF'
# .NET / VS / Dalamud
bin/
obj/
.vs/
*.user
*.suo
*.cache
*.ide/
*.DS_Store
# Rider
.idea/
# Logs
*.log
EOF

cat > "$NEW/README.md" << 'EOF'
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

EOF

# 4) init git
cd "$NEW"
git init
git add -A
git commit -m "Initial import from template: AdaptiveFPS status-bar plugin"
