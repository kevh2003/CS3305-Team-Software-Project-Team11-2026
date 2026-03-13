# CS3305 Group 11: Dead By Deadline

*See bottom of this readme for 'How to Install and Play'

### Contributors
- Kevin - 123496032 - https://github.com/kevh2003  
- Thomas - 123425562 - https://github.com/thomas-flower1  
- Anatoly - 123502989 - https://github.com/AidanLaceda  
- Tom - 123457342 - https://github.com/TomSu11ivan  
- Aidan - 123456856 - https://github.com/AidanLaceda  
- Rachel - 123387566 - https://github.com/racheltaylor22  

## Introducing the team members
| Name   | Contribution |
|--------|--------------|
| Kevin  |- Networking Infrastructure <br> - Multiplayer Setup <br> - Map creation (ground floor, 2nd,3rd, and 4th floors, Final Level Area)<br> - Map Lighting and moving objects (Doors and Elevators) <br> - Audio : updated player movement & interaction sounds <br> - Performance improvements <br> - Objective cleanup (networking/sync) <br> - So many bug fixes.. |
| Thomas |- Map creation (all assets, and 1st floor) and all textures <br> - Player models and animations <br> - Enemy model and collectable item models <br> - NPC dialogue <br> - Wifi Task|
| Anatoly|- Camera interaction system <br> - Camera movement system <br> - Enemy AI luring <br> - Red light green light game <br> - Light switching colours mechanic|
| Tom    |- Pick-up and Drop functionality<br> - Inventory UI and functionality<br> - Health Bar UI<br> - Stamina Bar UI<br> - Crosshair UI<br> - Screen messages for game ending, players being caught and interaction prompts<br> - Game Timer|
| Aidan  | - Enemy AI Design<br> - Enemy Interaction with Player<br> - Player Health<br> - Player SoundFX<br> - Enemy SoundFX<br>|
| Rachel |- Objectives UI<br> - Objectives Functionality - Removal from display when completed <br> - Interaction with different items around the map to complete objectives e.g. PC for Assignment and Duck Objects <br> - Improving player movements with addition of sprinting for a period of time and jumping<br> - Added different background theme music to different parts of the map e.g. Lobby, Main Game Scene and Final Level. |

## Gameplay Loop Explained

In Dead by Deadline, players have discovered something unusual going on in UCC's Western Gateway building. Firstly, ducks -and a lot of them- have begun appearing all over the WGB building. But perhaps more importantly, bad grades have been given to 6 individuals in particular... how intriguing. Because of this, these 6 hero's decide to investigate. How? By breaking into the Western Gateway Building of course!

Players begin at one of the entrances to the WGB. They must then complete a series of tasks in order to progress through the game. 

- Firstly they must fix the Wifi in 4 areas of the map (typical Eduroam..). 
- Once this is complete, players must each submit an assignment in the 1.10 lab.
- During this time, they may come across a number of rubber ducks spread through the map that they must pick up. How strange...
- Completion of the above tasks will spawn a 'Security Office Key' in one of 4 set spawn points on the map, once found this can be used to unlock the Security Office on the Ground Floor.
- Unlocking the Security Office grants access to the CCTV system, and 3 suspicious looking Buttons... hmm. 

- Pressing the first button begins the next stage of the game, the pressure plates. Depending on the number of players (n), n-1 pressure plates will be powered on throughout the map and emit a blue glow. Standing on a pressure plate will activate it and change its colour from blue to green. All powered pressure plates MUST be activated simultaneously in order for the second button to become usable. Since only n-1 pressure plates need to be activated (e.g. 5 plates for a 6 player game, 2 plates for 3 players), this allows one player to remain in the Security Office and make use of the CCTV system. This system connects to a number of security cameras placed around the map, covering all potential pressure plate areas. However, it also has a very useful 'Lure' feature.

- ENEMIES : As mentioned above, the CCTV system has a Lure ability. But what is this for? Well, throughout the game loop, players will come across a number of unforgiving enemies. Upon closer inspection, players may discover that these enemies are... Roombas? With a tripod and iPad set up neatly on top of them? Terrifying stuff we know, but you don't want to mess with these machines of destruction. If spotted, these enemies will chase and attempt to catch a player. Doing so will deal 1 damage to the players 3 health points. Losing all 3 health points will result in that player becoming 'caught' and enter a 'spectator' state. If all players are caught, the game will end. 

    This is where the CCTV Lure system comes in handy. When using a security camera, the player can 'ping' an area on the map. As a result, any nearby enemies will become immediately attracted to this ping and investigate. This can help players who are getting chased, or help players enter an area without the risk of getting spotted.

- Successfully activating all pressure plates will lock them in place and begin a 60 second timer, and if 2nd button is not pressed within this time frame, the pressure plates will be reset. 

- Successfully pressing the second button will unlock one final button which controls a set of elevator doors. Opening these doors will reveal a dark, bottomless elevator shaft (so scary). If players are brave enough, they must dive into this abyss to progress to the final area. 

- FINAL AREA : Players will discover that they have entered a large area with hundreds of data servers, and one long, unsuspecting corridor. At the end of this corridor is our culprit, one giant rubber duck who seems to be in control of the enemies and perhaps more importantly, is responsible for these 6 players bad grades (because there's no other reason they could possibly have bad grades.. right?). 

    Behind this giant abomination is one large server rack, with the power to change grades on demand. Perfect.

    However, if players try move towards this goal, they will discover a trap. The duck has been waiting all along, and will begin a game of Red Light Green Light.

    The ducks eyes as well as a set of lights within the room will begin to change colours from green to red intermittently. Players can freely move towards the end of the corridor while the lights are green, but once they turn red, players that move will immediately get caught (yikes).

    If players are successful in getting to the end of the corridor they will have one final obstacle to get across.

    The pit of doom and despair.

    Players must jump across a number of platforms above this pit in order to get to the other side. 

    Finally, any surviving player can access the server and change their grades once and for all, winning the game.

    Queue credits.

## Credits and Acknowledgements

All audio, music, and texture assets were sourced under the Creative Commons license from a number of sites, such as;

- [Pixabay](https://pixabay.com)
- [iStock](https://www.istockphoto.com)
- [OpenGameArt.org](https://opengameart.org)
- [Freepik](https://www.freepik.com)
- [TextureLabs](https://texturelabs.org)
- [Pexels](https://www.pexels.com)

## How to Install and Play

- Windows: [Download](https://github.com/kevh2003/CS3305-Team-Software-Project-Team11-2026/releases/latest/download/DeadByDeadline-Windows.zip)
- macOS: [Download](https://github.com/kevh2003/CS3305-Team-Software-Project-Team11-2026/releases/latest/download/DeadByDeadline-macOS.zip)


**Note:** these may require/ask for special permissions and/or firewall access. It is perfectly safe, we promise it won't mine bitcoin secretly.