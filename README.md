# Performance Fish
![](About/Preview.png?raw=true)  
  
Performance Mod for Rimworld.  
Requires [Prepatcher](https://github.com/Zetrith/Prepatcher) and [Fishery](https://github.com/bbradson/Fishery).  
  
Performance Fish attempts to improve overall framerates and tick times by patching various methods for improved efficiency, while keeping functionality identical. Designed to be used alongside other performance mods, like RocketMan, and intended to be compatible with very large modlists. Most patches become more impactful as the game progresses further into the lategame.  
  
A settings menu includes short descriptions for every patch, and each of them can be freely toggled in there. They get entirely unpatched in a disabled state, immediately. Nothing is stored in savefiles of specific game-sessions, ensuring that no errors get thrown when removing this mod.  
Special patches exist to add new entries and features to Dub's Performance Analyzer, including a right-click function allowing to profile overrides of functions.  
  
Almost all mods, including Combat Extended, Multiplayer, Vanilla Expanded, RocketMan and Performance Optimizer, are compatible.  
  
RimThreaded, RimWorld Rick, Oskar Obnoxious, No Laggy Beds and Better GC are currently marked as incompatible.  

# Hot to build
If you have an IDE that can open `.sln` directly and build, use that IDE.

Or use [dotnet](https://learn.microsoft.com/en-us/dotnet/core/install/) command.
```
git clone https://github.com/bbradson/Performance-Fish.git
git clone https://github.com/bbradson/Fishery.git
cd Performance-Fish/Source/PerformanceFish
dotnet build PerformanceFish.sln 
```

Licensed under [MPL-2.0](https://tldrlegal.com/license/mozilla-public-license-2.0-(mpl-2))