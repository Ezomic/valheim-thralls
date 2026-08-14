# Thralls

A Valheim mod that lets you put workers to task. Recruit a thrall, point it at a forest,
a copper vein or a berry patch, and it will work the area and haul what it gathers to a
depot you build.

Built against the installed game (Unity 6000.0.61, BepInEx 5.4.23.3, Harmony 2.9).

## Controls

All keys are rebindable in the config. Defaults use the numpad.

| Key | What it does |
| --- | --- |
| `Numpad 0` | Recruit a thrall where you are looking |
| `Numpad 1` | Assign a job at whatever you are pointing at |
| `Numpad 3` | Toggle follow-me |
| `Numpad 4` | Look at a thrall and dismiss it (it drops what it carries) |
| `Numpad 5` | Post the steward where you are looking, or move the one you have |
| `Numpad 6` | Record the hammer's current placement as a build order |

The job is inferred from what you point at:

- tree, stump or fallen log → **chop**
- ore vein or rock → **mine**
- berry bush or mushroom on wild ground → **gather**
- anything on tilled soil, or bare tilled soil → **farm**
- anything you built → **repair**
- ground with build orders pending nearby → **build**
- other bare ground → **stand here**
- an existing thrall → prints its status instead of giving an order

Orders go to the nearest thrall within `CommandRadius`, preferring one that is idle.
Point at a thrall to see its name, its job and how full its pack is.

## Talking to a thrall

Walk up to one and press `E`. Its orders panel opens: what it is doing down the left, what
you can tell it down the right.

| Order | What it does |
| --- | --- |
| Follow me | walks with you, and stops working |
| **Work from here** | moves its base to **where you are standing**, job unchanged |
| Do something else | pick a new job, worked from the base it already has |
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
`DepotRange` (60m) hauls its pack there, draws its seed there, and fetches build materials
there. Nothing needs pointing at and nothing is stored on the thrall, so tearing the depot
down and rebuilding it somewhere better re-points the whole crew at once.

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

There are four kinds of thrall. They are **separate careers, not a ladder** — a brute never
becomes a golem. You choose which to bind, and each is then trained on its own.

| Tier | Body | Hired with | Tool tier |
| --- | --- | --- | --- |
| 1 | Greydwarf brute | black forest heads | 1 |
| 2 | Draugr elite | swamp heads | 2 |
| 3 | Stone golem | mountain heads | 3 |
| 4 | Fuling berserker | plains heads | 4 |

**Binding one** costs 10 heads of that tier or better. That is the only thing heads buy.

**Levelling is earned, not bought.** A thrall gains experience by working, and levels up on
its own at 150 / 450 / 1000 xp:

| Doing | xp |
| --- | --- |
| each swing | 1 |
| felling a tree, breaking a vein, picking a crop | 10 |
| sowing a seed | 3 |
| a repair | 3 |
| raising a piece | 20 |

So the thrall you actually put to work is the one that gets good at it, and a thrall left
standing at the stones stays a novice no matter how many heads you have. All the values,
and the level thresholds, are config.

Tier sets the **tool tier**, which is the hard gate: a tier 1 thrall cannot fell an oak or
mine silver however long you leave it, exactly as a flint axe cannot. Level never changes
that — it only makes the thrall hit harder and swing faster:

- each **tier** above the first: +50% damage, +12% speed
- each **level** above the first: +25% damage, +8% speed

So a tier 4 level 4 berserker works roughly four times as fast as a fresh brute, but a
levelled brute still cannot touch silver.

Sacrifices always spend the *cheapest* acceptable heads first, so hiring never quietly eats
a boss trophy.

Hire from the steward's panel, which shows the head price per tier and lights up when you
can afford it. The recruit hotkey binds the best tier you can currently pay for. Experience
shows in the panel and in hover text.

## Death and raising

A thrall that dies is written into the steward's roll of the dead, keeping its name, tier
and level. Raise it from the steward's panel for goods from its own biome:

| Tier | Cost to raise |
| --- | --- |
| 1 | 20 greydwarf eyes, 20 wood |
| 2 | 15 bloodbags, 5 iron scrap |
| 3 | 10 crystal, 5 wolf pelt |
| 4 | 15 black metal scrap, 10 needles |

It comes back at the level it died with, so the experience it earned is never lost — only
the goods. All four costs are config. Without a steward there is nobody keeping the roll,
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

Point a thrall at anything you have built and it becomes the site foreman for that area: it
finds the most damaged piece within its work radius, walks over, and repairs it, then moves
on to the next worst. Repair costs nothing, exactly as it does when you swing the hammer
yourself. Leave one posted at a base and it will quietly undo storm and raid wear.

## Building

Building runs on **orders**, not guesswork.

1. Take out a hammer and line up a piece the way you normally would.
2. Instead of clicking, press `Numpad 6`. The placement is recorded as a build order and a
   translucent ghost stays behind to show what goes there.
3. Point a thrall at the site. It fetches the materials from the depot, walks over and
   raises the piece.

Orders are only accepted when the game itself says the placement is valid — the mod reads
the hammer's own placement ghost and its status, so a thrall is never sent to build
somewhere the game would have refused you. The piece is then placed through the game's own
`PlacePiece` routine, so creator, ward setup, wear and placement effects all behave normally.

Materials come out of the depot and are consumed at the piece's real build cost. A thrall
short on materials says so once and stops making the trip for two minutes rather than
pacing back and forth to an empty depot.

Orders live on the steward, so you need one posted before you can plan. The steward panel
shows how many are pending and can cancel them all.

## The steward

`Numpad 5` posts a steward — a stationary NPC who keeps your books. Walk up and press `E`
to open the ledger, which lists every thrall with:

- a rename field
- what it is doing, how full its pack is, how far away it is, and whether a depot is in
  reach of where it works
- job buttons that put it to work where it stands, including repair and build
- **Come**, to call it to the steward
- **Release**, to dismiss it

There is also **Recall all here** and, when any are pending, **Cancel build orders**. The
panel closes with its own button, `Numpad 5`, or by walking more than 8m away. You only ever
have one steward; pressing the key again moves it.

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

- `HeadsPerWorker` / `HeadsPerUpgrade` — default 10 each. Set to 0 to recruit for nothing.
- `Rank2Trophies` / `Rank3Trophies` / `Rank4Trophies` — which heads count as what.
- `RecruitCost` — an *extra* cost on top of the heads, e.g. `Coins:50`. Empty by default.
- `MaxThralls` — default 5.
- `ToolTier` — tool tier of a **rank 1** thrall, default 1. Each rank adds one on top.
- `ChopDamage` / `PickaxeDamage` / `SwingInterval` — how fast they work.
- `SeedsPerTrip` — seed drawn from the depot per visit, default 20. Set to 0 to stop them
  restocking, so they only sow what you hand them directly.
- `StewardPrefab` — `DvergerMageSupport` by default, so the steward looks distinct from the workers.
- `WorkRadius` — how far from the assigned spot they will roam for more of the same resource.
- `WorkerPrefab` — `Dverger` by default; `Skeleton_Friendly` also works.
- `DepotRange` — how far a depot reaches, default 60. Measured from the thrall's work base,
  not from the thrall, so a crew spread along a treeline all agree on one store.
- `DepotWidth` / `DepotHeight` — how much it holds, default 6 by 4. A full depot stops the
  crew working, so this is a larger chest and a half rather than a chest.
- `DepotCost` — what it takes to build, default wood, log, scraps and 2 iron.
- `TalkOnUse` — whether pressing use on a thrall opens its orders panel. On by default.

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
Copyright (c) 2026 Robbin Thijssen. See `LICENSE`.
