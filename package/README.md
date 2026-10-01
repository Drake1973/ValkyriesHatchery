# Valkyrie's Hatchery

> **AI disclosure:** Valkyrie's Hatchery is a vibe-coded project. The idea, the design decisions, and all of the in-game testing came from Drakexi. The code was written by Claude under Drakexi's direction. Every feature was tested in-game before release. If you'd rather avoid AI-written mods, you can make that choice knowingly.

Love fish, hate the fishing minigame? **Valkyrie's Hatchery lets you raise fish at home.** Build a hatchery in your base, fill it with roe, and come back to fresh fish, earning Fishing skill as you collect them.

Vanilla fishing is untouched. If you enjoy it, keep fishing. The hatchery is just another way to get there.

From the maker of [Valkyrie's Vision](https://thunderstore.io/c/valheim/p/Drakexi/ValkyriesVision/).

## How it works

1. **Build a Valkyrie's Hatchery** (hammer → Crafting): 20 Wood, 10 Fine Wood, 8 Stone, near a workbench. It looks like a dark-stained hot tub, and it works indoors.
2. **Get some Fish Roe.** Haldor sells it for 10 coins each. After that, make your own at the **food preparation table**: any one fish gives 2 roe (more from higher-quality fish).
3. **Put the roe in the hatchery.** Water fills the tub.
4. **Wait.** One roe hatches into a fish every 10 minutes. When a fish hatches you'll hear it splash, and the water keeps bubbling while fish are waiting.
5. **Collect your fish** by opening the hatchery and taking them out. You gain **Fishing skill** for every fish you collect.

## What hatches

Most fish (80%) come from **the biome the hatchery is built in**. The rest can be any fish from a biome whose boss you've defeated, so your fish selection grows as you progress.

| Fish | Home biome | Unlocked elsewhere by | Fishing XP |
|---|---|---|---|
| Perch, Pike | Meadows, Black Forest | Eikthyr | 10 |
| Trollfish | Black Forest | The Elder | 15 |
| Giant Herring | Swamp | Bonemass | 20 |
| Tetra (rare) | Mountains | Moder | 25 |
| Grouper | Plains | Yagluth | 25 |
| Tuna, Coral Cod | Ocean | The Queen | 30 |
| Pufferfish | Mistlands, Ocean | The Queen | 35 |
| Anglerfish | Mistlands | The Queen | 35 |
| Magmafish | Ashlands | Fader | 40 |
| Northern Salmon | Deep North | Kall Fimbulbringer | 40 |

Tetra lives only in frost caves in the wild, so it's rare from the hatchery too, unless you build in the Mountains.

Hatched fish are always **quality 1**. Trophy-sized fish are still a reward for real fishing.

## Good to know

- **Only roe goes in.** Anything else you put in the hatchery is handed back. Fish only arrive by hatching, so they can't be put back in for extra XP.
- **It works while you're away.** Leave for a while (or sleep) and the fish that were due are waiting when you return.
- **When it's full, it stops.** If there's no room for another fish, hatching pauses until you make space, then picks up at its normal pace.
- **Whoever takes the fish out gets the XP.**

> ⚠️ **Before uninstalling, empty your hatcheries.** If the mod is removed, the game deletes any hatcheries left in the world, along with everything inside them. This is how Valheim treats pieces from removed mods.

## Installation

Install with r2modman, Thunderstore Mod Manager, or Gale (available on Thunderstore and Hexium). Requires BepInEx and Jötunn.

**Multiplayer: the server and every player need the mod.** The hatchery and roe are new things in the world, and a game without the mod would delete them. Players without it get a clear message when they try to join. The server's settings apply to everyone and are locked while connected.

## Settings

Found in `BepInEx/config/com.drakexi.valkyrieshatchery.cfg`.

**Server-controlled** (on a server, its values apply to everyone and are locked):

| Setting | Default | Description |
|---|---|---|
| Slots | 12 | Storage slots per hatchery: 4, 6, 8, or 12. Hatcheries resize live; anything in removed slots is moved to a free slot or dropped in front of the hatchery. |
| HatchMinutes | 10 | Real minutes for one roe to hatch (0.5 to 120) |
| XpMultiplier | 1 | Scales the Fishing XP per fish (2 = double, 0 = none) |
| HomeBiomeChance | 80 | Percent of hatches from the hatchery's own biome, when other fish are unlocked |
| ProgressionSource | World | `World` (bosses defeated on the world) or `Player` (the boss kills of the player whose game is running the hatchery) |
| RoePrice | 10 | Haldor's price for one roe |

**Personal** (each player chooses):

| Setting | Default | Description |
|---|---|---|
| ShowXpMessage | true | Show the Fishing XP gained when you collect fish |
| PlayHatchSound | true | Play the splash sound when a fish hatches |

## Bugs & feature requests

Report bugs or suggest features on [GitHub Issues](https://github.com/Drake1973/ValkyriesHatchery/issues).
