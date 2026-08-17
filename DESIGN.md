# Thralls design notes

Why it works the way it does, and how it is built. None of this is needed to play; for that
see the [README](README.md).

## Why there are no keybinds

That is deliberate, and it went in two stages. Recruit, assign, follow, dismiss and
open-the-ledger became menu entries because a key that shadows a menu entry is a second
thing to keep in step. The four that outlasted them (time of day, flatten ground, god
mode and a light/motes diagnostic) were build aids rather than thrall commands, and they
have moved out of the mod entirely.

Three reasons stacked up. They quietly owned most of the numpad, which caused two silent
collisions with another of my mods before a bind audit was written to catch it. **Most
Macs have no numpad**, and Valheim ships a macOS build, so a Mac player pressed nothing
that worked and reasonably concluded the mod had not loaded. And a mod that ships to
players should not carry a one-press ground-levelling cheat.

They live in Devkit now, which is the mod that exists for that and never ships. Nothing in
Thralls replaced them: the count is zero, not four moved to safer keys.

## Why one breed rather than five

One rather than five is a deliberate cut, and not a placeholder. A thrall is a whole thing
to learn (where it works, what it carries, what it will and will not touch), and one kind
finished and tested is a release, where five half-tested ones are a wishlist. The draugr,
golem, berserker and seeker are written and their code stays in; `Breeds` turns them back
on one at a time.

**There are no bindstone upgrades.** An upgrade existed only to unlock the breed above it,
so with a single breed they were four buildable pieces that did nothing. They come back
automatically if you raise `Breeds`, since the mod counts them off the breeds on offer
rather than building a fixed four.

## Why the base is separate from the job

**Work from here** is the whole point of the orders panel. A thrall works within `WorkRadius` of a
base point, and until now that point could only be set by giving a fresh job, so moving a
crew twenty metres up the treeline meant re-assigning every one of them and losing what they
were in the middle of. Now you lead them over and say it. The order deliberately takes the
ground under *your* feet rather than the thrall's position or your crosshair: you walked to
the spot in order to say it, so that is the spot you meant.

Changing the job does **not** move the base, and moving the base does not change the job.
Keeping those two apart is what stops a crew drifting towards the player every time somebody
is retasked.

## Why resting is free and waking is not

Putting one away is free and waking it is not, on purpose. Resting is you deciding you have
too many mouths at the treeline, and charging for tidying up only teaches people to leave
thralls standing in a field instead. Waking is the other half: without a price the roll is
free storage, and you would bind five, rest four, and swap whichever you needed for nothing.

## The trophy price

The head is the point of it: a greydwarf brute's head to raise a greydwarf brute. There used
to be a second trophy price on top, taking any head of the tier or better, from a mechanism
older than these lists, so every thrall quietly wanted two trophies. That mechanism is gone
and this line is the whole cost.

## The golem smashes (not in this release)

The golem is tier 3, so this waits for the release that turns it on. It is written and
works, and setting `Breeds` to 3 brings it in along with the draugr below it.

The golem is the one breed that does not use an axe. Set it to chopping with empty hands
and it walks at trees and knocks them down, which makes it far and away the fastest way to
clear a treeline.

It keeps **a fifth of what the tree drops** and wastes the rest, and the wasted part does
not fall on the ground for you to pick up either. It is gone. So the golem is a tool for
clearing ground, not for getting wood. If you want the wood, send something with an axe.

Both halves are config. `SmashTiers` lists which breeds work this way, so you can hand the
trait to another one or take it off the golem, and `SmashYield` sets how much survives:
1 removes the penalty and leaves only the no-axe part, 0 means it brings back nothing at
all. Handing a golem an axe anyway is still allowed and changes nothing.

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
