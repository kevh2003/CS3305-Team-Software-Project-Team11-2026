

# Multiplayer Co-op Game Framework (Unity / NGO)

This readme contains all initial need-to-know/useful information regarding this framework (in its current state).

---

## Core Design Principles

* **Separation of concerns :**
  UI, gameplay, networking, and player logic are cleanly separated.

* **Host-authoritative multiplayer :**
  The host controls scene transitions and authoritative game flow.

* **LAN-first, Online-ready :**
  LAN multiplayer is implemented first; Relay/online support is designed to plug in without refactoring and will be implemented in a future commit (Hopefully).

* **Flexible :**
  Folder structure and systems are preliminary guidelines, not rigid rules.

---

## Scene Flow

The game currently follows a simple, explicit scene lifecycle:

```
00_Bootstrap
   ↓
01_MainMenu
   ↓
02_Lobby
   ↓
03_Game
```

### Scene Responsibilities

* **00_Bootstrap**

  * Initializes Unity Services (Authentication, Relay later)
  * Spawns the persistent NetRig
  * Loads the Main Menu

* **01_MainMenu**

  * Host / Join (LAN or Online)
  * No gameplay logic

* **02_Lobby**

  * Displays connected players
  * Host can start the game
  * Future commits will include player lists,character model selection etc. 

* **03_Game**

  * Gameplay scene
  * Basic Player input, cameras, and movement enabled atm.

---

## Networking Architecture

All networking logic is abstracted behind `INetSession`.

### Key Rule

> **UI and gameplay code must NEVER directly reference Netcode for GameObjects.**

For example instead do this:

```csharp
Services.NetSession.HostLan(...)
Services.NetSession.LoadSceneForAll(...)
```

### Why this matters

* Allows swapping LAN / Relay implementations
* Keeps UI and gameplay testable
* Prevents networking logic from leaking everywhere

This is the only rule we absolutely need to follow

---

## Project Folder Structure (Preliminary)

Git doesn't allow for empty folders to be committed, so the current repo only contains the folders and scripts that allow for the basic framework to function. So if you're wondering why there isn't a "World" folder in the repo for example, it's because there is no implementation of that yet.

Because of this, I've proposed a preliminary folder structure for the project below, outlining future folders that would likely best fit our needs. It includes the current folders/scripts that do currently exist, but you should reference this structure when adding your own ones.

See further folder guidelines below this graph.

```
Assets/
├─ _Project/                # Project-wide assets and prefabs (non-gameplay)
│  ├─ Art/
│  ├─ Audio/
│  ├─ Materials/
│  ├─ Prefabs/
│  └─ ScriptableObjects/
│
├─ Game/                    # All runtime game code
│  ├─ Core/                 # App bootstrap & global services
│  │  ├─ Bootstrapper.cs
│  │  └─ Services.cs
│  │
│  ├─ Net/                  # Networking layer
│  │  ├─ Abstractions/
│  │  │  └─ INetSession.cs
│  │  └─ Runtime/
│  │     ├─ LanDiscovery/
│  │     ├─ Relay/
│  │     └─ NgoNetSession.cs
│  │
│  ├─ Player/               # Player-related systems
│  │  ├─ Avatar/
│  │  │  └─ NetworkPlayer.cs
│  │  ├─ Identity/
│  │  └─ Interaction/
│  │
│  ├─ Gameplay/             # Objectives, mechanics, rules
│  ├─ Roles/                # Operator / Infiltrator logic
│  ├─ World/                # Doors, lights, interactables, etc.
│  ├─ UI/                   # Menus, HUD, lobby UI
│  ├─ AI/                   # NPCs, security, enemies
│  └─ Session/              # Match/session state (future)
│
├─ Scenes/                  # Unity scenes
├─ Settings/                # Input Actions, global configs
├─ TextMesh Pro/            # TMP package assets
└─ ThirdParty/              # External assets/plugins
```

### Folder Guidelines

* Teams can freely **add/remove subfolders inside their domain**
* Avoid reorganizing **Core**, **Net**, or **Services** without coordination
* `_Project/` is for assets and prefabs only
* `Game/` is for gameplay logic
* So basically do not place gameplay logic in `_Project`, or assets etc. is `Game/`. U get the gist.

---

## Player Lifecycle

* Player objects are **network-spawned automatically** by NGO
* Player exists across scenes
* Player input + camera are enabled **only in `03_Game`**
* Lobby and Menu scenes are UI-only

This prevents accidental movement or camera conflicts outside gameplay.

---

## Contributing Guidelines (Summary)

We may want to make a full `CONTRIBUTING.md` in future, especially if we go open source, but core rules:

These first 3 rules are pretty much the only ones we absolutely have to follow without exception;
* Do **not** bypass `INetSession`
* Do **not** directly access `NetworkManager` outside `Net/`
* Keep gameplay logic in `Game` and assets, prefabs etc. in `_Project`

--- 
In addition we should try to follow these rules as well;
* Keep scripts small and single-purpose
* Keep comments clear and concise, and comment the intent of a module not implementation (lmk what ye think of this).
* Communicate before large refactors or folder moves

---

## Current Status

*  LAN multiplayer (Host / Join) - DONE
*  Host-authoritative scene transitions - DONE
*  Networked player spawning - DONE
*  New Input System integration - DONE
*  Relay / Online support - IN PROGRESS
*  Gameplay systems - IN PROGRESS