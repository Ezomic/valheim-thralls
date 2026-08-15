# Shelved, not archived

The hand-built models for the three altar upgrades this release does not offer:

| Files | Piece | Opens |
| --- | --- | --- |
| `thrall_altar_upgrade2.*` | Mountain cairn | Golem |
| `thrall_altar_upgrade3.*` | War totem | Berserker |
| `thrall_altar_upgrade4.*` | Rift stone | Seeker |

They are finished and they work. They are out of `assets/` only because the csproj copies
`assets/*.obj;*.png;*.col` into the plugin folder and a subdirectory is not copied — so
6.4 MB of models for pieces nobody can build was going into the package. What ships is now
4.7 MB.

This is different from `archive/`, which holds designs that were rejected. Nothing here was
rejected. Put the files back in `assets/` when `Breeds` goes above 2.

## Why this does not delete anybody's mountain cairn

The prefabs are registered whatever `Breeds` is set to, and that has to stay true: ZNetScene
keys saved pieces on a prefab name and silently discards any ZDO whose name no longer
resolves. Removing a prefab destroys every one already standing in a world.

Moving a *model* is safe by contrast. `AltarPrefab.Compose` tries the `.obj` first and falls
back to `UpgradeParts`, a recipe of vanilla props — `Upgrade2Parts` and its siblings, which
all still carry a full recipe. So a shelved upgrade still registers, still builds and still
works; it is assembled out of stone floors and pillars instead of wearing its own mesh.

That fallback is the thing to check before shelving anything else here. If a piece's parts
recipe were ever emptied, `Compose` would return false, `BuildPrefab` would return null, and
the prefab would not register at all — which is the case that does destroy saved pieces.

## Regenerating them

They come out of the same script as everything else, which still knows how to build all
five:

```
blender --background --python tools/altar_model.py -- --all
```
