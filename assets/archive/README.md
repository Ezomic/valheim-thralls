# Shelved altar shapes

The shapes parked here while the bindstone and its three upgrades are the ones being
shipped. Nothing was deleted: the Blender builders for all of them are still in
`tools/altar_model.py`, and their prop and mote recipes are still in the config.

The summoning bench (`worktable`) joined them once the bindstone replaced it. It read as
a workbench rather than an altar - four legs under an apron, a boarded counter and a
post-and-beam tool rack, which is the vanilla workbench's whole vocabulary - and no
amount of runes carved into the top changed what the silhouette said first.

They live outside `assets/` because the build's deploy step copies everything in
`assets/` next to the plugin, so anything still in there ships.

The `pit` joined them the other way round. It was built to replace the bindstone and held
the `bindstone` key for a while - which is why that key's shape and its name have not
matched each other for some of this mod's life, and why the key is best read as a slot
rather than a description. The bindstone is back in it.

| Shape | What it is |
| --- | --- |
| `pit` | sunken ring with an arch over it; briefly the shape behind the `bindstone` key |
| `worktable` | the summoning bench, replaced by the bindstone |
| `shrine` | bench-sized offering stone with candles |
| `plinth` | round stepped platform, torch-lit |
| `dolmen` | standing stones under a capstone, cold blue |
| `cairn` | rough piled stone with a fire in its crown |
| `circle` | ring of menhirs, green fire |
| `barrow` | necromancer's howe, corpse-light and banners |

## Bringing one back

1. Add its name to `ACTIVE` in `tools/altar_model.py`.
2. Regenerate: `blender --background --python tools/altar_model.py`
   (or `-- --all` to rebuild every shape at once, archived ones included).
3. Add its name to `AltarShapes` in the config — **including the saved
   `robbin.valheim.thralls.cfg`**, since BepInEx keeps a saved value over a new default.

Or, to skip Blender, move its `.obj`, `.png` and `.col` back into `assets/` and do step 3.

## Careful

An altar already standing in a world disappears when its prefab stops being registered —
ZNetScene discards ZDOs whose prefab it cannot resolve.

But shelving the **files** is not what does that, despite what `AltarShapes`' own
description claims. `Shapes()` builds its prefab list from the config string alone and
never checks the disk, so a shape named in `AltarShapes` with no model on disk still
registers and simply falls back to the `AltarParts` assembly of vanilla pieces. Standing
altars survive that and just change appearance.

It is removing the name from **`AltarShapes`** that discards the ZDOs. So archiving a
shape is safe in two steps: shelve the files whenever you like, and take the name out of
`AltarShapes` only once nothing of that shape is left standing in any world.
