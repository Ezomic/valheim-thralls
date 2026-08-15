# Changelog

Notable changes to Thralls. Format follows [Keep a Changelog](https://keepachangelog.com),
and the mod uses [semantic versioning](https://semver.org).

## [1.0.0] — 2026-08-16

First release.

### The crew

- **Summoning altar**, built with the hammer under Crafting. Binds thralls, keeps the crew
  list and the roll of the dead, and cannot be killed by a raid because it is a piece
  rather than a person.
- **Two kinds of thrall**, and they are separate careers rather than rungs of a ladder — a
  brute never becomes a draugr:
  - **Greydwarf brute**, once the Elder is down.
  - **Draugr elite**, once Bonemass is down and a bog stone stands beside the altar.
- **Binding** costs goods from the breed's own biome and one head of that creature in
  particular. A draugr's head to raise a draugr.
- **Levelling is earned, not bought.** Thralls gain experience by working and level to 20 on
  their own, hitting harder, working faster and carrying more. Tier fixes the tool tier and
  no amount of experience moves it, so a levelled brute still cannot fell an oak.
- **Stars** at level 10 and 20, using the game's own creature levels, so a veteran looks
  like one.

### Work

- Jobs: **chop, mine, gather, farm, repair**, plus follow and idle.
- A thrall works a **radius around a base point** rather than a single target, picks the
  nearest valid resource, walks over, swings on a timer and picks up what falls.
- **Farming is the whole loop** — harvest, sow from the pack, and restock seed from the
  depot when it runs out. It runs the crop's own checks before sowing, so it will not waste
  seed on ground where nothing grows.
- **Repairing** makes a thrall the site foreman for its radius: it finds the most damaged
  piece, mends it, and moves to the next worst.
- Thralls never target anything with a Piece or WearNTear, so they will not chop up your
  buildings.

### The depot

- **A store you build**, and the only place a crew unloads. Any thrall whose work base is
  within 60m hauls its pack there and draws its seed there.
- Nothing is stored on the thrall: the depot is found from where it works each time one is
  needed, so moving the store re-points the whole crew at once.
- **Build as many as you like.** A thrall walks to the nearest, and carries on to the next
  when that one is full.

### Talking to a thrall

- Press use on one for its orders panel: follow me, **work from here**, do something else,
  show me its pack, take your load in, wait here, send it to rest.
- **Work from here** moves the radius to the ground you are standing on and leaves the job
  alone, so a crew can be moved without being retasked.
- The panel shows what it is doing, how full its pack is, and how far it is from both its
  depot and its work base.
- **Its pack** can be listed and taken from, stack by stack, without waiting for a trip.

### Away and back

- **Send it to rest** puts a thrall on the altar's roll for nothing; it hands its load in
  first, and keeps its name, level and tool.
- **Call back** wakes a resting thrall for goods from its biome, about half the price of a
  raise.
- **Raise** brings back one that died, at the level it died with. Experience is never lost,
  only goods.

### Elsewhere

- Thralls appear on the **map**, with a checkbox to turn them off and labels that can be
  turned off separately.
- Building aids on the numpad: time of day, ground levelling, god mode, and an altar effect
  cycle for diagnosing what you can see. There are no keys for ordering thralls about —
  that is all menus.
- Around 140 config settings, every one of them read by something.

### Not in this release

- **The golem, the berserker and the seeker.** All three are finished — bodies, costs,
  altar upgrades and hand-built models — and are switched off behind `Breeds`. Set it to
  3, 4 or 5 to turn them on. Their pieces stay registered whatever it is set to, so raising
  the cap later loses nothing already standing in a world.
- **The golem's bare-handed felling**, which knocks trees down without an axe and wastes
  most of the wood. It arrives with the golem.

### Known limits

- Exercised in single-player. It should work in co-op, since each thrall is driven by
  whoever owns its ZDO, but dedicated-server use is untested.
- The three shelved upgrade models are not in the package. If `Breeds` is raised without
  putting them back in `assets/`, those pieces build as stand-ins assembled from vanilla
  props rather than wearing their own mesh.
