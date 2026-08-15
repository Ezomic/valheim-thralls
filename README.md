# Thralls

A Valheim mod that lets you put workers to task. Bind a thrall at an altar, lead it to a
forest, a copper vein or a berry patch, and tell it to work from there. It clears the
ground around that spot and hauls what it gathers to a depot you build.

Built against the installed game (Unity 6000.0.61, BepInEx 5.4.23.3, Harmony 2.9).

## Controls

There are no keys for ordering thralls about. Everything is a menu:

| What you want | Where |
| --- | --- |
| Bind a thrall, see the whole crew, raise the dead | press `E` on the altar |
| Order one thrall — job, where it works, follow, release | press `E` on that thrall |
| Somewhere to unload | build a depot |

The keys that remain are building aids rather than thrall commands, and all four are
rebindable in the config.

| Key | What it does |
| --- | --- |
| `Numpad 7` | Step the time of day through dawn, midday, dusk and night |
| `Numpad 8` | Level a wide circle of ground where you are looking |
| `Numpad 9` | Toggle unlimited health and stamina |
| `Numpad -` | Cycle the altar's light and motes, for diagnosing what you can see |

## Talking to a thrall

Walk up to one and press `E`. Its orders panel opens: what it is doing down the left, what
you can tell it down the right.

| Order | What it does |
| --- | --- |
| Follow me | walks with you, and stops working |
| **Work from here** | moves its base to **where you are standing**, job unchanged |
| Do something else | pick a new job, worked from the base it already has |
| Show me its pack | lists what it is carrying, with a **Take** button per stack |
| Take your load in | sends it to the depot now rather than when its pack fills |
| Wait here | stands where you are, doing nothing |
| Go free | released, and kept on the altar's roll so it can be called back |

The left rail carries the two numbers you walked over to find out: how far the depot is, and
how far its base is. A thrall standing next to you with its base three hundred metres away
is a thrall that will walk off the moment you stop talking, and there was previously no way
to see that.

**Work from here** is the whole point of the panel. A thrall works within `WorkRadius` of a
base point, and until now that point could only be set by giving a fresh job — so moving a
crew twenty metres up the treeline meant re-assigning every one of them and losing what they
were in the middle of. Now you lead them over and say it. The order deliberately takes the
ground under *your* feet rather than the thrall's position or your crosshair: you walked to
the spot in order to say it, so that is the spot you meant.

Changing the job does **not** move the base, and moving the base does not change the job.
Keeping those two apart is what stops a crew drifting towards the player every time somebody
is retasked.

## The depot

Thralls unload into a **depot**, a piece you build, and nowhere else.

It is a chest underneath — a real container, six by four, that you can open and take from
like any other. What it adds is a radius: any thrall whose *work base* is within
`DepotRange` (60m) hauls its pack there and draws its seed there. Nothing needs nominating
and nothing is stored on the thrall, so tearing the depot down and rebuilding it somewhere
better re-points the whole crew at once.

**Build as many as you like.** A thrall walks to the nearest depot to the ground it works,
and if that one is full it carries on to the next nearest instead of stopping. A second
depot behind the first is overflow without being configured as overflow. Only when every
one of them in range is full does a thrall say so and go back to standing by.

It was a chest you nominated with a keypress, once per thrall, which was a chore repeated as
many times as you had workers. Softening that with an auto-adopt made it worse in a
different way: a thrall would quietly claim whichever box happened to be nearest the altar,
including one you were keeping something else in. Building the store where you want the
store says everything, in one action, in the world rather than in a setting you cannot see.

The depot is a **tally mast** — a bound post with a crossarm, a basket and a sack hung off
it and a plank bin at its foot. It stands 2.7m, which is deliberate: it governs a radius, so
it has to be findable from inside that radius. Look at it to see how many of your crew are
using it.

## Tiers, levels and heads

There are five kinds of thrall. They are **separate careers, not a ladder** — a brute never
becomes a golem. You choose which to bind, and each is then trained on its own.

Each answers only once the boss of **its own** biome is down, so a breed arrives about when
you start working the ground it came from.

| Tier | Body | Answers after | Hired with | Tool tier |
| --- | --- | --- | --- | --- |
| 1 | Greydwarf brute | The Elder | black forest heads | 1 |
| 2 | Draugr elite | Bonemass | swamp heads | 2 |
| 3 | Stone golem | Moder | mountain heads | 3 |
| 4 | Fuling berserker | Yagluth | plains heads | 4 |
| 5 | Seeker brute | The Queen | mistlands heads | 5 |

On top of the boss, each tier above the first wants one more upgrade raised beside the
altar. Both gates are config: `TierNRequiresBoss` takes a world key, empty for no gate, and
`UpgradesGateTiers` turns the upgrade requirement off.

**Binding one** costs a single head of that tier or better, plus the breed's own price in
biome goods. That head is the only thing heads buy.

**Levelling is earned, not bought.** A thrall gains experience by working and levels on its
own, at 150 xp for level 2 and rising to 33,000 for level 20:

| Doing | xp |
| --- | --- |
| each swing | 1 |
| felling a tree, breaking a vein, picking a crop | 10 |
| sowing a seed | 3 |
| a repair | 3 |

So the thrall you actually put to work is the one that gets good at it, and a thrall left
standing at the altar stays a novice no matter how many heads you have. All the values, and
the level thresholds, are config.

Tier sets the **tool tier**, which is the hard gate: a tier 1 thrall cannot fell an oak or
mine silver however long you leave it, exactly as a flint axe cannot. Level never changes
that — it only makes the thrall hit harder and swing faster:

- each **tier** above the first: +50% damage, +12% speed
- each **level** above the first: +8% damage, +3% speed

A fresh brute chops for 40 a swing every 1.6s; a fresh seeker for 120 every 0.83s, and at
level 20 that same seeker hits for 302 every 0.32s. A levelled brute still cannot touch
silver.

### The golem smashes

The golem is the one breed that does not use an axe. Set it to chopping with empty hands
and it walks at trees and knocks them down, which makes it far and away the fastest way to
clear a treeline.

It keeps **a fifth of what the tree drops** and wastes the rest, and the wasted part does
not fall on the ground for you to pick up either — it is gone. So the golem is a tool for
clearing ground, not for getting wood. If you want the wood, send something with an axe.

Both halves are config. `SmashTiers` lists which breeds work this way, so you can hand the
trait to another one or take it off the golem, and `SmashYield` sets how much survives:
1 removes the penalty and leaves only the no-axe part, 0 means it brings back nothing at
all. Handing a golem an axe anyway is still allowed and changes nothing.

Sacrifices always spend the *cheapest* acceptable heads first, so hiring never quietly eats
a boss trophy.

Hire from the altar's ledger, which shows the head price per tier and lights up when you
can afford it. The recruit hotkey binds the best tier you can currently pay for. Experience
shows in the panel and in hover text.

## Death and raising

A thrall that dies is written into the altar's roll of the dead, keeping its name, tier
and level. Raise it from the altar's ledger for goods from its own biome:

| Tier | Cost to raise |
| --- | --- |
| 1 | 20 greydwarf eyes, 20 wood |
| 2 | 15 bloodbags, 5 iron scrap |
| 3 | 10 crystal, 5 wolf pelt |
| 4 | 15 black metal scrap, 10 needles |
| 5 | 10 eitr, 2 black cores |

It comes back at the level it died with, so the experience it earned is never lost — only
the goods. All five costs are config. Without an altar there is nobody keeping the roll,
and death is final.

## Farming

A farming thrall does the full loop, not just the picking:

1. Harvests anything ripe inside its work radius.
2. Sows seed from its pack onto free tilled soil, nearest spot first.
3. When it runs out of seed, walks to the depot, delivers the produce and draws a fresh
   batch of seed (`SeedsPerTrip`, default 20).

Before sowing it runs the same checks the crop would run on itself — cultivated ground,
right biome, open sky, no crowding — so it will not waste seed on soil where nothing grows.
If the depot has no seed it says so once and stops making the trip for two minutes rather
than pacing back and forth.

What counts as a seed is read off the cultivator's own piece table, so any plant a mod adds
to the cultivator is sown too, at that plant's own seed cost.

## Repairing

Set a thrall to repair and it becomes the site foreman for that area: it finds the most
damaged piece within its work radius, walks over, and repairs it, then moves on to the next
worst. Repair costs nothing, exactly as it does when you swing the hammer
yourself. Leave one posted at a base and it will quietly undo storm and raid wear.

## The altar's ledger

Press `E` on a summoning altar to open the ledger, which lists every thrall with:

- a rename field
- what it is doing, how full its pack is, how far away it is, and whether a depot is in
  reach of where it works
- job buttons that put it to work where it stands
- **Come**, to call it to the altar
- **Release**, to dismiss it

There is also **Recall all here**. The panel closes with its own button, with Escape, or by
walking more than 8m away.

The ledger is the crew view — everyone at once, and hiring. For one thrall where it stands,
talk to it instead.

## How it works

A thrall is a tamed `Dverger` with an extra behaviour component. Vanilla AI still does the
pathing and the self-defence; the mod only parks an invisible waypoint and swings at
resources once the thrall arrives. That keeps the mod small and means it survives game
updates better than a hand-rolled AI would.

Each thrall picks the nearest valid resource within `WorkRadius` of the spot you assigned,
walks over, swings on a timer, picks up what falls, and carries a full pack to the depot.
Targets it cannot reach or cannot break get set aside for a minute so it does not get stuck.

State (job, work area, backpack contents) lives in the creature's ZDO, so thralls keep
working across saves and relogs. The depot is deliberately *not* part of that state - it
is looked up fresh from the work area every time one is needed, so moving your storage
needs no re-pointing.

## Notable config

`BepInEx/config/ezomic.valheim.thralls.cfg`, written on first run.

- `HeadsPerWorker` — heads per thrall, default 1. Set to 0 to recruit for nothing.
- `Rank2Trophies` / `Rank3Trophies` / `Rank4Trophies` — which heads count as what.
- `RecruitCost` — an *extra* cost on top of the heads, e.g. `Coins:50`. Empty by default.
- `MaxThralls` — default 5.
- `ToolTier` — tool tier of a **rank 1** thrall, default 1. Each rank adds one on top.
- `ChopDamage` / `PickaxeDamage` / `SwingInterval` — how fast they work.
- `SeedsPerTrip` — seed drawn from the depot per visit, default 20. Set to 0 to stop them
  restocking, so they only sow what you hand them directly.
- `WorkRadius` — how far from the assigned spot they will roam for more of the same resource.
- `DepotRange` — how far a depot reaches, default 60. Measured from the thrall's work base,
  not from the thrall, so a crew spread along a treeline all agree on one store.
- `DepotWidth` / `DepotHeight` — how much it holds, default 6 by 4. A full depot stops the
  crew working, so this is a larger chest and a half rather than a chest.
- `DepotCost` — what it takes to build, default wood, log, scraps and 2 iron.
- `TalkOnUse` — whether pressing use on a thrall opens its orders panel. On by default.
- `SmashTiers` / `SmashYield` — which breeds fell trees bare-handed, and how much of the
  tree survives it. Defaults to the golem keeping a fifth.

## Safety rails

- Thralls never target anything with a `Piece` or `WearNTear` component, so they will not
  chop up your buildings.
- Only a thrall that is actively working picks items off the ground. An idle or following
  thrall will not pocket something you dropped on purpose.
- Tamed creatures do not attack player structures, which is vanilla behaviour the mod relies on.

## Building

```bash
dotnet build Thralls.csproj -c Release
```

The build copies `Thralls.dll` into the Thunderstore `Default` profile's plugin folder.
Override `ValheimDir` or `ProfileDir` on the command line to target a different install
or a different profile.

## Multiplayer

Each thrall is driven by whoever owns its ZDO, which is normally the player who recruited
it and is standing nearby. It works in co-op, but the mod has only been exercised in
single-player so far — treat dedicated-server use as untested.

## Author

Thralls is an original mod by **Robbin Thijssen** (Thijssen Software).
Copyright (c) 2026 Robbin Thijssen. MIT licensed — see `LICENSE`.
