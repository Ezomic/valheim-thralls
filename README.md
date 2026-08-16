# Thralls

A Valheim mod that lets you put workers to task. Bind a thrall at a bindstone, lead it to a
forest, a copper vein or a berry patch, and tell it to work from there. It clears the
ground around that spot and hauls what it gathers to a depot you build.

Built against the installed game (Unity 6000.0.61, BepInEx 5.4.23.3, Harmony 2.9).

## Controls

**Thralls binds no keys at all.** Everything is a menu:

| What you want | Where |
| --- | --- |
| Bind a thrall, see the whole crew, raise the dead | press `E` on the bindstone |
| Order one thrall — job, where it works, follow, release | press `E` on that thrall |
| Somewhere to unload | build a depot |

That is deliberate, and it went in two stages. Recruit, assign, follow, dismiss and
open-the-ledger became menu entries because a key that shadows a menu entry is a second
thing to keep in step. The four that outlasted them — time of day, flatten ground, god
mode and a light/motes diagnostic — were build aids rather than thrall commands, and they
have moved out of the mod entirely.

Three reasons stacked up. They quietly owned most of the numpad, which caused two silent
collisions with another of my mods before a bind audit was written to catch it. **Most
Macs have no numpad**, and Valheim ships a macOS build, so a Mac player pressed nothing
that worked and reasonably concluded the mod had not loaded. And a mod that ships to
players should not carry a one-press ground-levelling cheat.

They live in Devkit now, which is the mod that exists for that and never ships. Nothing in
Thralls replaced them — the count is zero, not four moved to safer keys.

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
| Send it to rest | hands its load in and steps onto the bindstone's roll, name, level and tool intact |

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

## Sending one away and getting it back

Three different things, and all three keep the thrall's name, level and tool.

| You want | Where | Cost |
| --- | --- | --- |
| **Put one away** | its own panel — *Send it to rest* | nothing; it hands its load in first |
| **Bring that one back** | bindstone ledger — **Resting**, *Call back* | biome goods, about half a raise |
| **Raise one that died** | bindstone ledger — **Fallen**, *Bring back* | biome goods, below |

Putting one away is free and waking it is not, on purpose. Resting is you deciding you have
too many mouths at the treeline, and charging for tidying up only teaches people to leave
thralls standing in a field instead. Waking is the other half: without a price the roll is
free storage, and you would bind five, rest four, and swap whichever you needed for nothing.

| Tier | To call back | To raise from the dead |
| --- | --- | --- |
| 1 | 10 greydwarf eyes, 10 wood | 20 greydwarf eyes, 20 wood |

Both are config, and an empty recall cost makes calling back free again. The other four
tiers keep their own prices in the config for when `Breeds` turns them on.

Resting and dying are not the same list. A thrall you sent away is waiting; a thrall that
was killed is on the roll of the dead and costs goods to raise. Either way it returns at
the level it had, so experience is never lost — only the goods are.

Without a bindstone there is no roll, so a thrall released in the field is released for good.
The panel says which of the two it did.

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
different way: a thrall would quietly claim whichever box happened to be nearest the bindstone,
including one you were keeping something else in. Building the store where you want the
store says everything, in one action, in the world rather than in a setting you cannot see.

The depot is a **tally mast** — a bound post with a crossarm, a basket and a sack hung off
it and a plank bin at its foot. It stands 2.7m, which is deliberate: it governs a radius, so
it has to be findable from inside that radius. Look at it to see how many of your crew are
using it.

## The thrall

This release ships **one** kind: the greydwarf brute. It answers once **The Elder** is
down, so it arrives about when you start working the ground it came from.

| Tier | Body | Answers after | Tool tier |
| --- | --- | --- | --- |
| 1 | Greydwarf brute | The Elder | 1 |

One rather than five is a deliberate cut, and not a placeholder. A thrall is a whole thing
to learn — where it works, what it carries, what it will and will not touch — and one kind
finished and tested is a release, where five half-tested ones are a wishlist. The draugr,
golem, berserker and seeker are written and their code stays in; `Breeds` turns them back
on one at a time.

**There are no bindstone upgrades.** An upgrade existed only to unlock the breed above it,
so with a single breed they were four buildable pieces that did nothing. They come back
automatically if you raise `Breeds`, since the mod counts them off the breeds on offer
rather than building a fixed four.

**Five thralls can work at once**, flat. Nothing raises it — there is nothing to earn with
the upgrades gone. `BaseWorkSlots` and `MaxWorkSlots` are both 5, and the count is clamped
between them, so lowering the first makes station extensions near the bindstone count
again.

`MaxThralls` (20) is a separate number: how many you may *keep*. Five of them work and the
rest stand by, rest, or follow.

**Binding one** costs goods from its biome and one head of that creature in particular:

| Tier | What it costs |
| --- | --- |
| 1 | 5 bronze, 20 resin, 10 greydwarf eyes, 25 round logs, 25 stone, 1 greydwarf brute trophy |

The head is the point of it: a greydwarf brute's head to raise a greydwarf brute. There used
to be a second trophy price on top, taking any head of the tier or better, from a mechanism
older than these lists — so every thrall quietly wanted two trophies. That mechanism is gone
and this line is the whole cost.

**Levelling is earned, not bought.** A thrall gains experience by working and levels on its
own, at 150 xp for level 2 and rising to 33,000 for level 20:

| Doing | xp |
| --- | --- |
| each swing | 1 |
| felling a tree, breaking a vein, picking a crop | 10 |
| sowing a seed | 3 |
| a repair | 3 |

So the thrall you actually put to work is the one that gets good at it, and a thrall left
standing at the bindstone stays a novice however long you keep it. All the values, and
the level thresholds, are config.

Tier sets the **tool tier**, which is the hard gate: a tier 1 thrall cannot fell an oak or
mine silver however long you leave it, exactly as a flint axe cannot. Level never changes
that — it only makes the thrall hit harder and swing faster:

- each **tier** above the first: +50% damage, +12% speed
- each **level** above the first: +8% damage, +3% speed

A fresh brute chops for 40 a swing every 1.6s, and at level 20 the same brute hits for 100
every 0.99s. It still cannot fell an oak — tier fixes the tool, and no amount of experience
moves it.

### The golem smashes (not in this release)

The golem is tier 3, so this waits for the release that turns it on — it is written and
works, and setting `Breeds` to 3 brings it in along with the draugr below it.

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

Hire from the bindstone's ledger, which shows each breed's price and lights up when you
can afford it. The recruit hotkey binds the best tier you can currently pay for. Experience
shows in the panel and in hover text.

## Death and raising

A thrall that dies is written into the bindstone's roll of the dead, keeping its name, tier
and level. Raise it from the bindstone's ledger for goods from its own biome:

| Tier | Cost to raise |
| --- | --- |
| 1 | 20 greydwarf eyes, 20 wood |

It comes back at the level it died with, so the experience it earned is never lost — only
the goods. Every cost is config. Without a bindstone there is nobody keeping the roll,
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

## The bindstone's ledger

Press `E` on a bindstone to open the ledger, which lists every thrall with:

- a rename field
- what it is doing, how full its pack is, how far away it is, and whether a depot is in
  reach of where it works
- job buttons that put it to work where it stands
- **Come**, to call it to the bindstone
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

- `Breeds` — how many kinds are offered, default 1. Raise to 5 for all of them; it also
  decides how many bindstone upgrades exist, so at 1 there are none.
- `BaseWorkSlots` / `MaxWorkSlots` — both 5, which is what makes the crew a flat five.
  Lower the first to make station extensions near the bindstone earn slots again.
- `TierNCost` — what each breed costs to bind, materials and its own head.
- `RecruitCost` — an *extra* cost on every breed, e.g. `Coins:50`. Empty by default.
- `MaxThralls` — how many you may keep in total, default 20.
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
  tree survives it. Set to the golem, so dormant until `Breeds` reaches 3.

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

## Core is optional

Thralls installs and runs on its own. [Core](https://github.com/Ezomic/valheim-core) is a
**soft** dependency: present, it is used; absent, nothing here is degraded. Installing
Thralls from Thunderstore no longer installs Core with it.

What Core adds is the **version gate** — a handshake that compares mod versions and build
ids on connect and refuses a client that does not match. Little is at risk here — a thrall is a tamed vanilla creature with a waypoint, so there is no unresolvable prefab to lose a ZDO to. What is given up is the report when two ends run different builds.

Solo, none of that applies and Core is not needed at all.
