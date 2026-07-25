# Race Track Authoring

## Create a track

1. Open a race scene or duplicate the existing race scene as a starting point.
2. Choose **GameObject > GMTK > Race Track Authoring**.
3. Select the new `Race Track Authoring` object.
4. Click and drag the large cyan numbered points directly in the Scene view.
5. Edit each point's `width` and `bank` values in the Inspector.
6. Press **Bake & Play**.

The menu command connects the existing Racing Starter Kit systems in the scene.
If they do not exist, it creates the AI waypoint, checkpoint, and spawning roots
from the kit's configured prefabs.

## Editing

- **Add Point After Selected** inserts a point halfway to the next control point.
- **Remove Selected** removes the highlighted point.
- **Frame Whole Track** focuses the Scene camera on all editable points.
- **Rebuild Visible Control Points** recreates the editor-only cyan sphere objects
  if they were deleted. They live under `__TrackControlPoints`, can be clicked
  like ordinary GameObjects, and are excluded from builds.
- `Sample Spacing` controls road smoothness. Smaller values create more geometry.
- `AI Waypoint Spacing` controls AI path density.
- `Checkpoint Spacing` controls how many ordered race checkpoints are generated.
- `Start Distance` moves the finish line and starting grid along the loop.
- `Menu Camera Offset` controls the menu camera relative to the generated start
  line. Baking aims that camera down the new track automatically.
- Each control point can independently change road width and banking.

## Generated content

`__GeneratedTrack` and the generated children of the AI/checkpoint roots are
disposable. Do not hand-edit them; edit the authoring points and bake again.

The final road mesh is stored in `Assets/GeneratedTracks` so that it and its
pre-cooked static `MeshCollider` survive scene reloads and builds.
Every bake replaces that mesh asset from the current visible control-point
transforms; it does not retain geometry from the preceding bake.

Commit the authoring object, baked mesh asset, scene changes, and their `.meta`
files together.

## Replacing an imported track

Disable or remove the old imported road mesh and its colliders before playtesting
the generated road. Other scenery can remain in the scene and be repositioned
around the new track.
