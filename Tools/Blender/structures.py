# Authors the landmark structures on every board: the fungi's base (where the
# path ends, one model per environment) and the bacteria's spawn nest (a crater,
# where the path starts).
#
# Run headless from the repo root:
#   blender --background --python Tools/Blender/structures.py
#
# Writes Assets/Resources/Structures/{Base*,BacteriaNest}.obj.
#
# Why authored meshes here when the rest of the scenery is generated in code
# (MeshFactory): these were stacks of Unity primitives - a lilac sphere on a
# cylinder, and a black disc ringed with shards - and neither read as anything
# at play distance. They are the things the player has to find on every board,
# so they get real silhouettes. Everything else stays procedural.
#
# SEVEN BASES, one per environment. Before this there was one mushroom house
# recoloured per biome, so a biome changed hue and never shape. Each base is
# now its own build - a parasol, a bell, a snow lodge, a basalt cap, a pod
# cluster, a pagoda - but they all keep the same grammar, because the base is
# the thing the player is defending and it has to stay recognisable as theirs:
#   * a plinth on the ground, a body, and one big silhouette element above it
#   * a door on the front, at the foot, big enough to read
#   * lit windows. These are WARM on every biome, including the cold and the
#     toxic ones: a light on inside is the single cue that says this one is
#     home, as against the nest at the other end of the path.
#   * the same footprint and height band (radius <= 2.6, height 3.1-4.7) so the
#     camera framing, the flinch and the prop clearance hold without per-biome
#     numbers on the Unity side.
#
# WHERE THE WINDOWS GO, which cost two passes to work out. The play camera
# looks down at about 30 degrees, so a window under a wide cap is hidden by
# that cap's underside - and the instinct, to raise the window toward the gap,
# makes it WORSE. The sight line climbs at tan(30) = 0.577 per unit out, far
# faster than a cap's underside does, so a LOW window escapes past the rim
# before the ceiling catches it and a high one does not. For a window at
# radius r_w under a cap of radius R whose underside runs from z0 at the
# centre to z1 at the rim, it is visible when
#     z_w + 0.577 * (R - r_w) < z0 + (z1 - z0) * (R - r_w) / R  is FALSE
# at r = R, which in practice means: put it level with the door, not above it.
# The meadow base predates this and has both of its stem windows up at 1.85
# and 2.05, under a cap whose underside is flat at 2.62 - they have never been
# visible from the play camera. Left as it is, because it is the reference the
# other six are read against; see HANDOFF.
#
# Authoring contract, relied on by LevelDecorator:
#   * Blender Z-up, exported Y-up; the object sits on the ground plane and is
#     centred on the origin, sized in Unity units (a board cell is 5).
#   * the door faces Blender -Y. The Unity side turns the whole thing to face
#     the path, so this only has to be consistent.
#   * one material per part, NAMED after the part: LevelDecorator colours each
#     submesh from the biome palette by its material name. The part vocabulary
#     is shared by all seven bases, so one table on the Unity side covers them
#     all: Plinth, Stem, Cap, Spots, Door, Windows, Trim, Glow (and the nest's
#     Rim, Pool, Spikes, Bubbles). Trim is the structural accent (an awning,
#     icicles, tendrils, eaves) and Glow is the emissive biome accent (glowing
#     pods, lava fissures, alien pores) - a base uses them or it does not.
#     The .mtl colours are ignored.
#   * low-poly and flat-shaded, to match MeshFactory's scenery. 520-1,210
#     triangles each - negligible beside a single enemy body, which is 287k
#     verts - and six to eight renderers, which stay OUTSIDE the static batch
#     because the base moves when it is hit (BaseFlinch).
import bmesh
import bpy
import math
import os
import random

from mathutils import Vector

OUT = os.path.join("Assets", "Resources", "Structures")


def fresh():
  bpy.ops.wm.read_factory_settings(use_empty=True)


def finish(ob, name):
  ob.name = name
  ob.data.name = name
  # A material named after the part. Unity's OBJ importer merges objects into
  # one mesh, so the material slots - which survive as submeshes - are what
  # carries the part name across. The colour here is only a preview hint.
  ob.data.materials.clear()
  ob.data.materials.append(bpy.data.materials.get(name) or bpy.data.materials.new(name))
  for poly in ob.data.polygons:
    poly.use_smooth = False
  return ob


def jitter(ob, amount, seed, keep_bottom=True):
  rnd = random.Random(seed)
  for v in ob.data.vertices:
    if keep_bottom and v.co.z < 0.02:
      continue
    v.co.x += rnd.uniform(-amount, amount)
    v.co.y += rnd.uniform(-amount, amount)
    v.co.z += rnd.uniform(-amount, amount) * 0.6


def join(parts, name):
  bpy.ops.object.select_all(action='DESELECT')
  for p in parts:
    p.select_set(True)
  bpy.context.view_layer.objects.active = parts[0]
  bpy.ops.object.join()
  ob = bpy.context.view_layer.objects.active
  bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
  return finish(ob, name)


def apply_all(ob):
  bpy.context.view_layer.objects.active = ob
  bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)


# ------------------------------------------------------------------- shapes
#
# The six new bases are variations on four moves - a dome, a teardrop, a
# tapering stem, and a plate laid on a surface - so those live here rather
# than being spelled out six times.


def dome(radius, flatten, segments=16, rings=8):
  """The upper half of a sphere, closed underneath with a flat disc, sitting
  on z=0 so a caller can place its base directly. `flatten` scales the height
  only, so 0.3 is a parasol and 1.3 a bell."""
  bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings,
                                       radius=radius, location=(0, 0, 0))
  ob = bpy.context.object
  bm = bmesh.new()
  bm.from_mesh(ob.data)
  lower = [v for v in bm.verts if v.co.z < -0.01]
  bmesh.ops.delete(bm, geom=lower, context='VERTS')
  edges = [e for e in bm.edges if e.is_boundary]
  bmesh.ops.holes_fill(bm, edges=edges, sides=0)
  bm.to_mesh(ob.data)
  bm.free()
  for v in ob.data.vertices:
    v.co.z *= flatten
  return ob


def teardrop(radius, stretch, pinch=0.30, segments=14, rings=7):
  """A sphere stretched upright and squeezed to nothing at the bottom: the
  alien biome's pods. Sits on z=0."""
  bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings,
                                       radius=radius, location=(0, 0, 0))
  ob = bpy.context.object
  for v in ob.data.vertices:
    # Clamped: the bottom vertex can land a hair below -radius in floating
    # point, and a negative base with a fractional exponent is complex.
    t = min(1.0, max(0.0, (v.co.z + radius) / (2.0 * radius)))
    squeeze = pinch + (1.0 - pinch) * (t ** 0.55)
    v.co.x *= squeeze
    v.co.y *= squeeze
    v.co.z = (v.co.z + radius) * stretch
  return ob


def stem(radius, height, verts=12, taper=0.14, bulge=0.16, seed=2):
  """A slightly bulging, tapering trunk standing on z=0."""
  bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=radius,
                                      depth=height, location=(0, 0, height * 0.5))
  ob = bpy.context.object
  for v in ob.data.vertices:
    t = (v.co.z + height * 0.5) / height     # 0 at the base, 1 at the top
    w = 1.0 + bulge * math.sin(t * math.pi) - taper * t
    v.co.x *= w
    v.co.y *= w
  jitter(ob, 0.03, seed)
  apply_all(ob)
  return ob


def scallop(ob, lobes, amount, phase=0.7):
  """A soft wobble round a cap's rim, so it is not a perfect lathe."""
  for v in ob.data.vertices:
    ang = math.atan2(v.co.y, v.co.x)
    w = 1.0 + amount * math.sin(ang * lobes + phase)
    v.co.x *= w
    v.co.y *= w


def rim_lift(ob, radius, start, amount):
  """Bends a cap's outer rim up (positive) or down (negative), squared so the
  middle of the cap is left alone. A down-bend gives a skirt that overhangs
  the stem; an up-bend gives a pagoda's eaves."""
  for v in ob.data.vertices:
    t = (math.hypot(v.co.x, v.co.y) / radius - start) / max(1e-6, 1.0 - start)
    if t > 0.0:
      v.co.z += amount * min(t, 1.0) ** 2


def on_dome(radius, flatten, base_z, ang, polar):
  """A point on a flattened dome's surface and its outward normal. `polar` is
  0 at the top of the dome and 1 at the rim."""
  theta = polar * math.pi * 0.5
  x = math.cos(ang) * math.sin(theta) * radius
  y = math.sin(ang) * math.sin(theta) * radius
  z = math.cos(theta) * radius * flatten + base_z
  normal = Vector((x, y, (z - base_z) / max(1e-6, flatten * flatten)))
  return Vector((x, y, z)), normal.normalized()


def on_teardrop(radius, stretch, base_z, ang, t, pinch=0.30):
  """A point on a teardrop's surface and its outward normal. `t` runs 0 at the
  bottom to 1 at the top, the same parameter teardrop() squeezes by - laying a
  pore on a pod with on_dome() instead puts it inside the pod, because a
  teardrop is pinched where a dome is not."""
  s = radius * math.sqrt(max(0.0, 1.0 - (2.0 * t - 1.0) ** 2))
  r = s * (pinch + (1.0 - pinch) * (t ** 0.55))
  pos = Vector((math.cos(ang) * r, math.sin(ang) * r, base_z + t * 2.0 * radius * stretch))
  return pos, Vector((math.cos(ang), math.sin(ang), 0.0))


def plate(pos, normal, size, thickness=0.32, stretch=(1.0, 1.0), subdivisions=1):
  """A flattened blob laid on a surface, facing along its normal: cap spots,
  settled snow, embers, glowing pores."""
  bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=subdivisions, radius=size,
                                        location=(0, 0, 0))
  ob = bpy.context.object
  ob.scale = (stretch[0], stretch[1], thickness)
  ob.rotation_mode = 'QUATERNION'
  ob.rotation_quaternion = Vector((0, 0, 1)).rotation_difference(normal)
  ob.location = pos
  return ob


def arch_door(y, z0, width, height, depth=0.22, verts=10):
  """An arched plank door standing on z0, on the front (-Y) face. Returns the
  two pieces for the caller to join, so a base can add its own frame."""
  body = height - width * 0.5
  bpy.ops.mesh.primitive_cube_add(size=1, location=(0, y, z0 + body * 0.5))
  panel = bpy.context.object
  panel.scale = (width, depth, body)
  apply_all(panel)
  bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=width * 0.5, depth=depth,
                                      location=(0, y, z0 + body),
                                      rotation=(math.pi / 2, 0, 0))
  return [panel, bpy.context.object]


def disc(radius, depth, z, verts=10):
  bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=radius, depth=depth,
                                      location=(0, 0, z))
  return bpy.context.object


def porthole(x, y, z, radius, depth=0.2, verts=10):
  """A round window in the front face."""
  bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=radius, depth=depth,
                                      location=(x, y, z), rotation=(math.pi / 2, 0, 0))
  return bpy.context.object


def slit(x, y, z, w, h, depth=0.2):
  """A narrow upright window."""
  bpy.ops.mesh.primitive_cube_add(size=1, location=(x, y, z))
  ob = bpy.context.object
  ob.scale = (w, depth, h)
  apply_all(ob)
  return ob


def spike(ang, radius, base_z, length, thickness, lean, verts=5, down=False,
          rooted=True):
  """A cone pointing out and up from a ring (tendrils, reeds) or straight down
  (icicles). `rooted` puts its BASE on the placement point; the nest's cilia
  predate this helper and are centred on theirs, so they pass rooted=False to
  keep that mesh unchanged."""
  bpy.ops.mesh.primitive_cone_add(vertices=verts, radius1=thickness, radius2=0.0,
                                  depth=length, location=(0, 0, length * 0.5))
  ob = bpy.context.object
  if rooted:
    apply_all(ob)
  ob.rotation_euler = (math.pi if down else 0.0, lean, ang)
  ob.location = (math.cos(ang) * radius, math.sin(ang) * radius, base_z)
  return ob


# ------------------------------------------------- Environment 1: the meadow
#
# The original, and the family's reference: a red toadstool cottage. Every
# other base is read against this one, so it is left exactly as it was.

def base_meadow():
  # Plinth: a low ring of flat stones the house stands on.
  plinth = disc(2.35, 0.32, 0.16)
  jitter(plinth, 0.06, 1)
  apply_all(plinth)
  finish(plinth, "Plinth")

  trunk = finish(stem(1.0, 2.5, bulge=0.16, taper=0.14), "Stem")
  trunk.location = (0, 0, 0.3)
  apply_all(trunk)

  # Cap: a squashed dome with an overhanging rim and a flat underside.
  cap = dome(2.55, 0.62)
  scallop(cap, 5.0, 0.045)
  rnd = random.Random(3)
  for v in cap.data.vertices:
    if v.co.z > 0.05:
      v.co.z += rnd.uniform(-0.05, 0.05)
  cap.location = (0, 0, 2.62)
  apply_all(cap)
  finish(cap, "Cap")

  # Spots: flattened blobs sat on the cap's surface, facing outward.
  spots = []
  for ang, polar, size in ((0.0, 0.0, 0.62), (0.35, 0.3, 0.42), (2.2, 0.55, 0.40),
                           (3.9, 0.5, 0.36), (5.2, 0.6, 0.40), (1.2, 0.8, 0.30),
                           (3.0, 0.82, 0.28), (4.6, 0.85, 0.26)):
    pos, normal = on_dome(2.55, 0.62, 2.62, ang, polar)
    spots.append(plate(pos, normal, size))
  spots_ob = join(spots, "Spots")

  door_ob = join(arch_door(-1.02, 0.33, 0.78, 1.25), "Door")

  # Windows: two round lit windows up the stem, and one in the cap.
  wins = [porthole(-0.55, -0.93, 2.05, 0.2), porthole(0.58, -0.93, 1.85, 0.17)]
  bpy.ops.mesh.primitive_cylinder_add(vertices=10, radius=0.26, depth=0.3,
                                      location=(0.9, -2.0, 3.25), rotation=(1.1, 0, 0.25))
  wins.append(bpy.context.object)
  win_ob = join(wins, "Windows")

  return [plinth, trunk, spots_ob, cap, door_ob, win_ob]


# -------------------------------------------- Environment 2: the dune parasol
#
# Sand under a golden dusk. A desert parasol: tall and thin where the meadow
# is squat, with a wide, nearly flat cap held high like a sun-shade, a canvas
# awning over the door and a stepped sandstone footing. The silhouette is read
# against a low sun, so the cap's overhang is what carries it.

def base_dunes():
  steps = [disc(2.30, 0.26, 0.13), disc(1.80, 0.24, 0.37)]
  for s in steps:
    jitter(s, 0.05, 11)
    apply_all(s)
  plinth = join(steps, "Plinth")

  # A thin stem with a bulbous foot, the way a real parasol mushroom grows.
  trunk = stem(0.66, 3.30, verts=10, bulge=0.10, taper=0.22, seed=12)
  for v in trunk.data.vertices:
    t = v.co.z / 3.30
    if t < 0.26:
      swell = 1.0 + 0.78 * (1.0 - t / 0.26) ** 2
      v.co.x *= swell
      v.co.y *= swell
  trunk.location = (0, 0, 0.49)
  apply_all(trunk)
  finish(trunk, "Stem")

  # Cap: wide, shallow, and drooping at the rim, with a small central umbo.
  cap = dome(2.52, 0.30, segments=18)
  scallop(cap, 8.0, 0.055, phase=0.3)
  rim_lift(cap, 2.52, 0.55, -0.34)
  # A small central umbo, the bump a parasol keeps where it was once a bud.
  bpy.ops.mesh.primitive_cone_add(vertices=12, radius1=0.46, radius2=0.0, depth=0.40,
                                  location=(0, 0, 0.72))
  tip = bpy.context.object
  apply_all(tip)
  cap.location = (0, 0, 3.52)
  apply_all(cap)
  tip.location = (0, 0, 3.52)
  apply_all(tip)
  cap_ob = join([cap, tip], "Cap")

  # Spots: cracks radiating across the sun-baked cap, laid flat on it.
  cracks = []
  for i in range(7):
    ang = i / 7.0 * math.pi * 2 + 0.4
    pos, normal = on_dome(2.52, 0.30, 3.52, ang, 0.46)
    # Sunk into the surface, or they read as flakes of debris lying on it.
    cracks.append(plate(pos - normal * 0.07, normal, 0.34, thickness=0.09,
                        stretch=(2.0, 0.20)))
  spots_ob = join(cracks, "Spots")

  door_ob = join(arch_door(-0.84, 0.49, 0.74, 1.24), "Door")

  wins = [slit(-0.36, -0.70, 2.12, 0.18, 0.46), slit(0.36, -0.66, 2.38, 0.18, 0.46),
          porthole(0.0, -0.58, 2.88, 0.15)]
  win_ob = join(wins, "Windows")

  # Trim: the ring left by the cap as it opened, and a canvas awning that
  # shades the door - the one thing on the model that is not fungus, and what
  # says somebody lives in it.
  bpy.ops.mesh.primitive_torus_add(major_segments=12, minor_segments=5,
                                   major_radius=0.86, minor_radius=0.13,
                                   location=(0, 0, 2.55))
  ring = bpy.context.object
  for v in ring.data.vertices:
    v.co.z *= 0.55
  apply_all(ring)
  # A porch roof over the door, sloping DOWN at the front (a positive rotation
  # about X; the first pass used a negative one and tipped it up into the air),
  # with two posts that reach its front edge. The edge is computed rather than
  # guessed, because a post that stops short of its roof reads as scaffolding.
  tilt = 0.38
  awning_y, awning_z, half_depth, half_width = -1.05, 1.95, 0.475, 0.85
  bpy.ops.mesh.primitive_cube_add(size=1, location=(0, awning_y, awning_z))
  awning = bpy.context.object
  awning.scale = (half_width * 2, half_depth * 2, 0.09)
  awning.rotation_euler = (tilt, 0, 0)
  apply_all(awning)
  front_y = awning_y - half_depth * math.cos(tilt)
  front_z = awning_z - half_depth * math.sin(tilt)
  posts = [awning, ring]
  for sx in (-1.0, 1.0):
    depth = front_z - 0.49
    bpy.ops.mesh.primitive_cylinder_add(vertices=6, radius=0.085, depth=depth,
                                        location=(sx * (half_width - 0.08), front_y,
                                                  0.49 + depth * 0.5))
    posts.append(bpy.context.object)
  trim_ob = join(posts, "Trim")

  return [plinth, trunk, cap_ob, spots_ob, door_ob, win_ob, trim_ob]


# ------------------------------------------- Environment 3: the marsh lantern
#
# A purple toxic night. A bell mushroom leaning back off the path, with glowing
# pods strung under its rim and pale gills showing beneath: on the darkest
# biome the base has to emit, not reflect, or it is a black lump. The lean is
# away from the door so the front stays square to the camera.

def base_marsh():
  mound = [disc(2.10, 0.30, 0.15, verts=9), disc(1.55, 0.26, 0.40, verts=8)]
  for m in mound:
    jitter(m, 0.08, 21)
    apply_all(m)
  plinth = join(mound, "Plinth")

  # The stem curves back as it rises; the cap is placed on its tip.
  height = 2.55
  lean = 0.34
  trunk = stem(0.92, height, verts=11, bulge=0.12, taper=0.26, seed=22)
  for v in trunk.data.vertices:
    t = v.co.z / height
    v.co.y += lean * t * t
  trunk.location = (0, 0, 0.44)
  apply_all(trunk)
  finish(trunk, "Stem")

  # Cap: a tall bell, pinched to a point and flaring into a skirt at the rim.
  cap = dome(1.80, 1.00, segments=15, rings=9)
  for v in cap.data.vertices:
    if v.co.z > 1.15:
      pinch = 1.0 - 0.62 * min(1.0, (v.co.z - 1.15) / 0.65)
      v.co.x *= pinch
      v.co.y *= pinch
  scallop(cap, 6.0, 0.065, phase=1.1)
  rim_lift(cap, 1.80, 0.48, -0.42)
  cap.location = (0, lean, 2.84)
  apply_all(cap)
  finish(cap, "Cap")

  # Spots: the gills, radiating under the cap. Pale, so the dark underside of
  # the bell does not close the silhouette off.
  gills = []
  for i in range(10):
    ang = i / 10.0 * math.pi * 2
    r = 1.10
    bpy.ops.mesh.primitive_cube_add(size=1, location=(0, 0, 0))
    fin = bpy.context.object
    fin.scale = (1.05, 0.09, 0.16)
    fin.rotation_euler = (0, 0, ang)
    fin.location = (math.cos(ang) * r, math.sin(ang) * r + lean, 2.76)
    apply_all(fin)
    gills.append(fin)
  spots_ob = join(gills, "Spots")

  door_ob = join(arch_door(-0.90, 0.44, 0.70, 1.18), "Door")

  wins = [porthole(-0.42, -0.84, 1.96, 0.19), porthole(0.46, -0.80, 2.24, 0.16)]
  win_ob = join(wins, "Windows")

  # Glow: pods hung round the cap's rim on short stalks, the biome accent.
  pods = []
  for i in range(7):
    ang = i / 7.0 * math.pi * 2 + 0.2
    r = 1.58
    x, y = math.cos(ang) * r, math.sin(ang) * r + lean
    drop = 0.46 + 0.22 * math.sin(ang * 2.0)
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1, radius=0.19,
                                          location=(x, y, 2.86 - drop))
    pods.append(bpy.context.object)
    bpy.ops.mesh.primitive_cylinder_add(vertices=5, radius=0.035, depth=drop,
                                        location=(x, y, 2.86 - drop * 0.5))
    pods.append(bpy.context.object)
  glow_ob = join(pods, "Glow")

  # Trim: reeds standing out of the mound, so the base is rooted in the swamp.
  reeds = []
  for i in range(5):
    ang = i / 5.0 * math.pi * 2 + 0.9
    reeds.append(spike(ang, 1.72, 0.30, 1.05 + 0.35 * math.sin(ang * 3.0), 0.085, 0.30))
  trim_ob = join(reeds, "Trim")

  return [plinth, trunk, cap, spots_ob, door_ob, win_ob, glow_ob, trim_ob]


# -------------------------------------------- Environment 4: the tundra lodge
#
# Bright overcast snow. Squat and heavy where the dunes are tall: a low wide
# dome weighed down with settled snow, icicles along the rim and a stone
# chimney breaking the cap's line. The one base shorter than it is wide.

def base_tundra():
  slabs = [disc(2.45, 0.30, 0.15, verts=8), disc(1.90, 0.22, 0.41, verts=7)]
  for s in slabs:
    jitter(s, 0.07, 31)
    apply_all(s)
  # The chimney is masonry, so it goes in with the Plinth rather than the Trim
  # it started in - as Trim it took the icicles' colour and read as a block of
  # ice standing on the roof. It is also what finally separates this base from
  # a paler meadow dome at play distance.
  bpy.ops.mesh.primitive_cylinder_add(vertices=8, radius=0.32, depth=2.10,
                                      location=(1.18, 0.30, 2.70))
  stack = bpy.context.object
  jitter(stack, 0.05, 33, keep_bottom=False)
  apply_all(stack)
  slabs.append(stack)
  bpy.ops.mesh.primitive_cylinder_add(vertices=8, radius=0.42, depth=0.22,
                                      location=(1.18, 0.30, 3.84))
  slabs.append(bpy.context.object)
  plinth = join(slabs, "Plinth")

  trunk = stem(1.26, 1.62, verts=12, bulge=0.12, taper=0.10, seed=32)
  trunk.location = (0, 0, 0.46)
  apply_all(trunk)
  finish(trunk, "Stem")

  # Raised and pulled in from the first pass: the cap overhung the whole front
  # of the house and put the door and the lit windows in permanent shadow.
  cap = dome(2.46, 0.47, segments=18)
  scallop(cap, 7.0, 0.05, phase=2.1)
  rim_lift(cap, 2.46, 0.50, -0.26)
  cap.location = (0, 0, 2.08)
  apply_all(cap)
  finish(cap, "Cap")

  # Spots: snow lying on the cap - fewer and broader than a toadstool's, and
  # drifted toward the top, which is how settled snow reads at a glance.
  drifts = []
  for ang, polar, size in ((0.0, 0.0, 0.92), (0.8, 0.34, 0.62), (2.9, 0.40, 0.56),
                           (4.5, 0.46, 0.50), (1.9, 0.68, 0.38), (5.4, 0.72, 0.34)):
    pos, normal = on_dome(2.46, 0.47, 2.08, ang, polar)
    drifts.append(plate(pos, normal, size, thickness=0.26))
  spots_ob = join(drifts, "Spots")

  door_ob = join(arch_door(-1.28, 0.46, 0.84, 1.10), "Door")

  # Low, beside the door, and clear of the stem's surface, which bulges to
  # about 1.35 at this height: the first pass buried them inside the stem and
  # the second put them up under the cap, where the overhang hid them.
  wins = [porthole(-0.62, -1.30, 1.05, 0.19), porthole(0.62, -1.28, 1.02, 0.19)]
  win_ob = join(wins, "Windows")

  # Trim: icicles hung from the rim, and the chimney. The chimney is what
  # finally separates this one from the meadow dome at play distance - the cap
  # alone was a paler toadstool.
  ice = []
  for i in range(11):
    ang = i / 11.0 * math.pi * 2
    ice.append(spike(ang, 2.22, 2.02, 0.40 + 0.38 * abs(math.sin(ang * 2.5)), 0.10, 0.0,
                     down=True))
  trim_ob = join(ice, "Trim")

  return [plinth, trunk, cap, spots_ob, door_ob, win_ob, trim_ob]


# -------------------------------------------- Environment 5: the ember basalt
#
# Ash under an ember sky. Not a mushroom at all from a distance: an angular
# cooled-lava cap on a columnar basalt stem, with fissures glowing down its
# slope. Eight-sided and jittered, so every edge catches the low red light,
# and the only soft thing on it is the door.

def base_ember():
  slab = disc(2.28, 0.34, 0.17, verts=7)
  jitter(slab, 0.10, 41)
  apply_all(slab)
  # Broken columns round the footing, the biome's own geology.
  columns = [slab]
  for ang, r, h in ((0.5, 1.95, 0.85), (2.3, 2.05, 1.25), (3.6, 1.88, 0.62),
                    (5.1, 2.02, 1.00)):
    bpy.ops.mesh.primitive_cylinder_add(vertices=6, radius=0.34, depth=h,
                                        location=(math.cos(ang) * r, math.sin(ang) * r,
                                                  h * 0.5))
    col = bpy.context.object
    col.rotation_euler = (0, 0.10, ang)
    apply_all(col)
    columns.append(col)
  plinth = join(columns, "Plinth")

  trunk = stem(1.16, 2.35, verts=6, bulge=0.06, taper=0.16, seed=42)
  jitter(trunk, 0.06, 43)
  trunk.location = (0, 0, 0.34)
  apply_all(trunk)
  finish(trunk, "Stem")

  cap = dome(2.46, 0.56, segments=8, rings=5)
  rim_lift(cap, 2.46, 0.52, -0.30)
  jitter(cap, 0.12, 44, keep_bottom=False)
  cap.location = (0, 0, 2.52)
  apply_all(cap)
  finish(cap, "Cap")

  # Spots: ember chunks crusted into the cap.
  chunks = []
  for ang, polar, size in ((0.4, 0.22, 0.34), (2.6, 0.44, 0.40), (4.4, 0.36, 0.30),
                           (5.6, 0.70, 0.26)):
    pos, normal = on_dome(2.46, 0.56, 2.52, ang, polar)
    chunks.append(plate(pos, normal, size, thickness=0.42))
  spots_ob = join(chunks, "Spots")

  door_ob = join(arch_door(-1.16, 0.34, 0.74, 1.22), "Door")

  # Windows: angular slits, plus the glow of the doorway itself.
  # The third is the light spilling round the doorway, and it has to sit IN
  # FRONT of the door panel (which reaches y -1.27) or the panel hides it.
  wins = [slit(-0.46, -1.10, 1.86, 0.28, 0.22), slit(0.48, -1.08, 2.08, 0.28, 0.22),
          slit(0.0, -1.31, 0.86, 0.46, 0.74, depth=0.08)]
  win_ob = join(wins, "Windows")

  # Glow: fissures running down the cap's slope, and a hot seam where the cap
  # meets the stem. Thin and sunk slightly, so they read as cracks rather than
  # as stripes painted on.
  # Short and broken rather than long and radial: the first pass drew six
  # full-length spokes and they read as scratches down a brown rock, not as
  # lava showing through a crust.
  cracks = []
  for ang, polar, length in ((0.3, 0.34, 1.2), (1.4, 0.58, 1.0), (2.5, 0.30, 1.1),
                             (3.3, 0.62, 0.9), (4.4, 0.40, 1.2), (5.5, 0.56, 1.0)):
    pos, normal = on_dome(2.46, 0.56, 2.52, ang, polar)
    cracks.append(plate(pos * 0.985, normal, 0.30, thickness=0.10,
                        stretch=(length, 0.18)))
  bpy.ops.mesh.primitive_torus_add(major_segments=14, minor_segments=4,
                                   major_radius=1.05, minor_radius=0.09,
                                   location=(0, 0, 2.48))
  seam = bpy.context.object
  for v in seam.data.vertices:
    v.co.z *= 0.5
  apply_all(seam)
  cracks.append(seam)
  glow_ob = join(cracks, "Glow")

  # Trim: a buttress either side of the door, the only built-looking thing on
  # an otherwise geological base.
  # A pair of uprights and a lintel over the door: upright and symmetrical, so
  # the doorway reads as built. Leaning slabs read as rubble that had fallen
  # against the base.
  props = []
  for sx in (-1.0, 1.0):
    bpy.ops.mesh.primitive_cube_add(size=1, location=(sx * 0.70, -1.18, 0.90))
    b = bpy.context.object
    b.scale = (0.26, 0.42, 1.65)
    apply_all(b)
    props.append(b)
  bpy.ops.mesh.primitive_cube_add(size=1, location=(0, -1.18, 1.82))
  lintel = bpy.context.object
  lintel.scale = (1.80, 0.50, 0.26)
  apply_all(lintel)
  props.append(lintel)
  trim_ob = join(props, "Trim")

  return [plinth, trunk, cap, spots_ob, door_ob, win_ob, glow_ob, trim_ob]


# --------------------------------------------- Environment 6: the bloom pods
#
# Bioluminescent growth under a teal-violet sky. Three fused pods instead of
# one cap: the only base with no single dominant silhouette, which is the
# point - it reads as a colony, like the biome around it. Magenta pores do the
# work the windows do elsewhere, so the windows are small here.

def base_bloom():
  ring = [disc(2.18, 0.26, 0.13, verts=11)]
  for ang, r in ((0.7, 1.70), (2.5, 1.82), (4.3, 1.66), (5.6, 1.78)):
    d = disc(0.52, 0.30, 0.22, verts=7)
    d.location = (math.cos(ang) * r, math.sin(ang) * r, 0.22)
    apply_all(d)
    ring.append(d)
  for p in ring:
    jitter(p, 0.07, 51)
    apply_all(p)
  plinth = join(ring, "Plinth")

  # Three pods: a tall one carrying the door, and two shorter ones crowding it.
  pods = []
  stalks = []
  for (x, y, radius, stretch) in ((0.0, 0.0, 1.30, 1.55),
                                  (1.32, 0.52, 0.84, 1.45),
                                  (-1.26, -0.38, 0.62, 1.40)):
    pod = teardrop(radius, stretch)
    pod.location = (x, y, 0.30)
    apply_all(pod)
    pods.append(pod)
    s = disc(radius * 0.42, 0.44, 0.0, verts=8)
    s.location = (x, y, 0.32)
    apply_all(s)
    stalks.append(s)
  cap_ob = join(pods, "Cap")
  trunk = join(stalks, "Stem")

  # Spots: a crown of nodules budding off the top of the tall pod.
  buds = []
  for ang, t, size in ((0.4, 0.99, 0.30), (1.3, 0.90, 0.24), (3.4, 0.93, 0.21),
                       (5.1, 0.87, 0.19)):
    pos, normal = on_teardrop(1.30, 1.55, 0.30, ang, t)
    buds.append(plate(pos + normal * 0.04, normal, size, thickness=0.70))
  spots_ob = join(buds, "Spots")

  door_ob = join(arch_door(-1.12, 0.26, 0.66, 1.16), "Door")

  wins = [porthole(-0.44, -1.06, 1.74, 0.15), porthole(0.46, -1.04, 1.92, 0.13)]
  win_ob = join(wins, "Windows")

  # Glow: pores over all three pods. The pod surfaces are domes of known shape,
  # so each pore can be laid on one properly rather than floated near it.
  # On each pod's own surface and standing a little proud of it, spread round
  # the fat middle where the pod is widest: on_dome put the first pass's pores
  # inside the pods, where all that showed was a few specks near the footing.
  pores = []
  specs = ((0.0, 0.0, 1.30, 1.55, ((0.5, 0.62, 0.24), (1.7, 0.45, 0.21),
                                   (3.2, 0.68, 0.23), (4.6, 0.52, 0.20),
                                   (2.5, 0.33, 0.18), (5.6, 0.75, 0.17))),
           (1.32, 0.52, 0.84, 1.45, ((0.3, 0.48, 0.18), (2.2, 0.62, 0.16),
                                     (4.4, 0.40, 0.17))),
           (-1.26, -0.38, 0.62, 1.40, ((1.0, 0.52, 0.15), (3.9, 0.60, 0.14))))
  for (x, y, radius, stretch, places) in specs:
    for ang, t, size in places:
      pos, normal = on_teardrop(radius, stretch, 0.30, ang, t)
      pores.append(plate(Vector((pos.x + x, pos.y + y, pos.z)) + normal * 0.05,
                         normal, size, thickness=0.40))
  glow_ob = join(pores, "Glow")

  # Trim: tendrils reaching out of the footing. Tapered and leaning well out,
  # so the colony looks like it is spreading.
  tendrils = []
  for i in range(6):
    ang = i / 6.0 * math.pi * 2 + 0.35
    tendrils.append(spike(ang, 1.50, 0.30, 1.70 + 0.50 * math.sin(ang * 2.0), 0.12,
                          0.42 + 0.16 * math.cos(ang)))
  trim_ob = join(tendrils, "Trim")

  return [plinth, trunk, cap_ob, spots_ob, door_ob, win_ob, glow_ob, trim_ob]


# ------------------------------------------ Environment 7: the blossom pagoda
#
# Warm pink at golden hour. Two caps stacked with their eaves turned up, a
# finial on top, blossom clustered at the tiers: the only base with a built,
# symmetrical silhouette, which is what holds it apart from a biome made of
# soft round canopies.

def base_blossom():
  steps = [disc(2.32, 0.28, 0.14, verts=12), disc(1.86, 0.24, 0.40, verts=10)]
  for s in steps:
    jitter(s, 0.04, 61)
    apply_all(s)
  plinth = join(steps, "Plinth")

  trunk = stem(1.02, 2.05, verts=12, bulge=0.10, taper=0.18, seed=62)
  trunk.location = (0, 0, 0.46)
  apply_all(trunk)
  finish(trunk, "Stem")

  # Two tiers. The eaves turn UP, which is the whole read: every other cap in
  # the game droops.
  tiers = []
  for radius, flatten, z, lobes in ((2.48, 0.46, 2.18, 8.0), (1.58, 0.52, 3.16, 7.0)):
    tier = dome(radius, flatten, segments=18)
    scallop(tier, lobes, 0.04, phase=0.5)
    rim_lift(tier, radius, 0.60, 0.30)
    tier.location = (0, 0, z)
    apply_all(tier)
    tiers.append(tier)
  bpy.ops.mesh.primitive_cone_add(vertices=10, radius1=0.30, radius2=0.0, depth=0.52,
                                  location=(0, 0, 4.00))
  finial = bpy.context.object
  apply_all(finial)
  cap_ob = join(tiers + [finial], "Cap")

  # Spots: blossom clustered where the tiers meet the stem, and along the eaves.
  petals = []
  for radius, flatten, z, places in ((2.48, 0.46, 2.18, ((0.5, 0.66), (2.4, 0.72),
                                                         (4.2, 0.68), (5.5, 0.74))),
                                     (1.58, 0.52, 3.16, ((1.4, 0.62), (3.6, 0.70)))):
    for ang, polar in places:
      pos, normal = on_dome(radius, flatten, z, ang, polar)
      for (dx, dy, dz, size) in ((0.0, 0.0, 0.12, 0.27),
                                 (0.17, 0.13, -0.10, 0.20),
                                 (-0.15, 0.11, -0.07, 0.17)):
        bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1, radius=size,
                                              location=(pos.x + dx, pos.y + dy,
                                                        pos.z + dz))
        petals.append(bpy.context.object)
  spots_ob = join(petals, "Spots")

  door_ob = join(arch_door(-1.06, 0.46, 0.76, 1.24), "Door")

  # Low, outside the door frame's half-width, and well under the lower tier,
  # whose flat underside hid them when they sat just beneath it.
  wins = [porthole(-0.56, -1.00, 1.32, 0.18), porthole(0.56, -0.98, 1.30, 0.18)]
  win_ob = join(wins, "Windows")

  # Trim: the eave beams under each tier, the door frame, and the finial's
  # collar - the timber of a built thing, against the fungus of the rest.
  beams = []
  # AT the rim, not below it. rim_lift raises each tier's edge by 0.30, so the
  # eave sits at the tier's own z plus that - the first pass put the beams at
  # the tier's base height and they hung in the air under the overhang.
  for radius, z in ((2.44, 2.48), (1.54, 3.46)):
    bpy.ops.mesh.primitive_torus_add(major_segments=16, minor_segments=4,
                                     major_radius=radius, minor_radius=0.06,
                                     location=(0, 0, z))
    t = bpy.context.object
    for v in t.data.vertices:
      v.co.z *= 0.6
    apply_all(t)
    beams.append(t)
  # Short enough to clear the windows above it, which it used to stand in
  # front of.
  bpy.ops.mesh.primitive_cube_add(size=1, location=(0, -1.02, 1.08))
  frame = bpy.context.object
  frame.scale = (0.98, 0.16, 1.45)
  apply_all(frame)
  beams.append(frame)
  bpy.ops.mesh.primitive_cylinder_add(vertices=10, radius=0.22, depth=0.16,
                                      location=(0, 0, 3.78))
  beams.append(bpy.context.object)
  trim_ob = join(beams, "Trim")

  # The door frame is drawn before the door so the panel sits in front of it.
  return [plinth, trunk, cap_ob, spots_ob, trim_ob, door_ob, win_ob]


# ---------------------------------------------------------------- the nests
#
# SEVEN NESTS as well, on the same principle as the bases, and with one hard
# constraint the bases do not have: EnemySpawner puts every enemy at
# pathPoints[0] at ground height, which is the nest's exact centre. So a nest
# may not close over or build up in the middle - a cone with the pool on top
# would spawn the wave inside itself. Every one of them keeps a ground-level
# pool and a clear mouth of radius 1.3 or more, and does its distinguishing
# work in the RING around that mouth.
#
# The pool stays the same hostile magenta on every biome, the way the bases'
# windows stay warm on every biome: it is the one cue that says this end of the
# path is theirs. The ring, the shards and the crust take the environment.
#
# Part vocabulary, shared by all seven: Rim, Pool, Spikes, Bubbles, plus Crust
# (the biome-matched plates, slabs or petals around the mouth) and Glow (a
# secondary emissive, where a nest has one).


def nest_shard(ang, radius, z, width, length, tilt, thickness=0.14):
  """A flat slab standing on the ring and tilted outward: broken pack ice, dry
  crust plates, the petals of the blossom biome's flower."""
  bpy.ops.mesh.primitive_cube_add(size=1, location=(0, 0, 0))
  ob = bpy.context.object
  ob.scale = (length, width, thickness)
  ob.rotation_euler = (0, tilt, ang)
  ob.location = (math.cos(ang) * radius, math.sin(ang) * radius, z)
  apply_all(ob)
  return ob


def nest_ring(major, minor, z, flatten=0.70, segments=14, minor_segments=6,
              lump=0.0, lobes=3.0, seed=0):
  """A flattened ring sitting on the ground, open in the middle. Every nest's
  surround has to be an ANNULUS and not a disc: the pool is at ground level in
  the centre, so a solid disc of any height caps it and the mouth disappears -
  which is what happened to the dunes, ember, bloom and blossom nests on the
  first pass, all four of which came back as a lid."""
  bpy.ops.mesh.primitive_torus_add(major_segments=segments, minor_segments=minor_segments,
                                   major_radius=major, minor_radius=minor,
                                   location=(0, 0, 0))
  ob = bpy.context.object
  rnd = random.Random(seed)
  for v in ob.data.vertices:
    v.co.z *= flatten
    if lump > 0.0:
      ang = math.atan2(v.co.y, v.co.x)
      w = 1.0 + lump * math.sin(ang * lobes + 1.1) + rnd.uniform(-lump * 0.4, lump * 0.4)
      v.co.x *= w
      v.co.y *= w
    v.co.z = max(v.co.z, -0.05) + z
  apply_all(ob)
  return ob


def nest_pool(radius, z, verts=16):
  pool = disc(radius, 0.12, z, verts=verts)
  apply_all(pool)
  return finish(pool, "Pool")


def nest_bubbles(places):
  bubbles = []
  for (x, y, r, z) in places:
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1, radius=r, location=(x, y, z))
    bubbles.append(bpy.context.object)
  return join(bubbles, "Bubbles")


# ------------------------------------------- Environment 1: the meadow crater
#
# The original, and the reference the other six are read against: left exactly
# as it was.

def bacteria_nest():
  # Rim: a lumpy crater ring.
  bpy.ops.mesh.primitive_torus_add(major_segments=20, minor_segments=7,
                                   major_radius=1.95, minor_radius=0.62, location=(0, 0, 0))
  rim = bpy.context.object
  rnd = random.Random(7)
  for v in rim.data.vertices:
    v.co.z *= 0.7
    ang = math.atan2(v.co.y, v.co.x)
    lump = 1.0 + 0.10 * math.sin(ang * 4.0 + 1.3) + rnd.uniform(-0.04, 0.04)
    v.co.x *= lump
    v.co.y *= lump
    v.co.z = max(v.co.z, -0.05) + 0.22
  apply_all(rim)
  finish(rim, "Rim")

  # Pool: the glowing mouth enemies climb out of, sunk slightly into the ring.
  pool = disc(1.62, 0.12, 0.12, verts=16)
  apply_all(pool)
  finish(pool, "Pool")

  # Spikes: bent cilia round the rim, leaning outward.
  spikes = []
  spike_rnd = random.Random(8)
  for i in range(9):
    ang = i / 9.0 * math.pi * 2 + spike_rnd.uniform(-0.15, 0.15)
    h = spike_rnd.uniform(1.4, 2.1)
    spikes.append(spike(ang, 2.15, 0.35, h, 0.3, spike_rnd.uniform(0.25, 0.45),
                        rooted=False))
  spikes_ob = join(spikes, "Spikes")

  # Bubbles: a few blobs breaking the pool's surface.
  bubbles = []
  for (x, y, r) in ((0.5, 0.3, 0.30), (-0.6, -0.2, 0.22), (0.1, -0.7, 0.18), (-0.2, 0.75, 0.15)):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1, radius=r, location=(x, y, 0.2))
    bubbles.append(bpy.context.object)
  bubbles_ob = join(bubbles, "Bubbles")

  return [rim, pool, spikes_ob, bubbles_ob]


# --------------------------------------------- Environment 2: the sand funnel
#
# A berm of drifted sand round the mouth, terraced in two steps and broken by
# cracked crust plates. Low and wide where the marsh's is lumpy, and the only
# nest whose ring is tidy - wind-built rather than grown.

def nest_dunes():
  # Two terraces, both open in the middle, so the berm steps down to the pool.
  berm = [nest_ring(2.12, 0.50, 0.20, flatten=0.62, segments=13, lump=0.05, seed=71),
          nest_ring(1.62, 0.34, 0.50, flatten=0.60, segments=12, lump=0.04, seed=72)]
  rim = join(berm, "Rim")

  pool = nest_pool(1.32, 0.12)

  crust = []
  for i in range(8):
    ang = i / 8.0 * math.pi * 2 + 0.3
    crust.append(nest_shard(ang, 2.16 + 0.14 * math.sin(ang * 3.0), 0.50,
                            0.42, 0.62, -0.34, thickness=0.11))
  crust_ob = join(crust, "Crust")

  spikes = []
  rnd = random.Random(73)
  for i in range(6):
    ang = i / 6.0 * math.pi * 2 + rnd.uniform(-0.2, 0.2)
    spikes.append(spike(ang, 1.62, 0.62, rnd.uniform(0.9, 1.5), 0.20,
                        rnd.uniform(0.45, 0.70)))
  spikes_ob = join(spikes, "Spikes")

  bubbles = nest_bubbles(((0.44, 0.26, 0.26, 0.20), (-0.52, -0.18, 0.19, 0.20),
                          (0.08, -0.60, 0.16, 0.20), (-0.16, 0.64, 0.13, 0.20)))
  return [rim, pool, crust_ob, spikes_ob, bubbles]


# ----------------------------------------------- Environment 3: the bog vent
#
# The wettest of the seven: a lumpy mound with ooze spilling over its lip and a
# thicket of thin cilia. Tall and unruly where the dunes' is low and tidy.

def nest_marsh():
  bpy.ops.mesh.primitive_torus_add(major_segments=18, minor_segments=7,
                                   major_radius=1.98, minor_radius=0.66,
                                   location=(0, 0, 0))
  ring = bpy.context.object
  rnd = random.Random(81)
  for v in ring.data.vertices:
    v.co.z *= 0.80
    ang = math.atan2(v.co.y, v.co.x)
    lump = 1.0 + 0.13 * math.sin(ang * 3.0 + 0.4) + rnd.uniform(-0.05, 0.05)
    v.co.x *= lump
    v.co.y *= lump
    v.co.z = max(v.co.z, -0.05) + 0.26
  apply_all(ring)
  mound = [ring]
  # Blisters swelling off the ring, so it reads as grown rather than piled.
  for ang, r, size, z in ((0.5, 2.05, 0.62, 0.42), (2.1, 1.95, 0.52, 0.50),
                          (3.5, 2.10, 0.70, 0.38), (5.0, 1.90, 0.46, 0.54)):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1, radius=size,
                                          location=(math.cos(ang) * r,
                                                    math.sin(ang) * r, z))
    blob = bpy.context.object
    blob.scale = (1.0, 1.0, 0.72)
    apply_all(blob)
    mound.append(blob)
  rim = join(mound, "Rim")

  pool = nest_pool(1.62, 0.14)

  spikes = []
  rnd = random.Random(82)
  for i in range(13):
    ang = i / 13.0 * math.pi * 2 + rnd.uniform(-0.12, 0.12)
    spikes.append(spike(ang, 1.96, 0.50, rnd.uniform(1.3, 2.0), 0.14,
                        rnd.uniform(0.12, 0.30)))
  spikes_ob = join(spikes, "Spikes")

  bubbles = nest_bubbles(((0.52, 0.34, 0.32, 0.22), (-0.64, -0.22, 0.26, 0.22),
                          (0.12, -0.74, 0.21, 0.22), (-0.22, 0.80, 0.18, 0.22),
                          (0.86, -0.34, 0.16, 0.22), (-0.90, 0.42, 0.14, 0.22),
                          (0.30, 0.92, 0.13, 0.22)))

  # Glow: ooze running over the lip and down the outside. The marsh biome is
  # the darkest board in the game, and the pool alone sits too flat on it.
  runs = []
  for ang, length in ((0.9, 1.1), (2.6, 0.9), (4.1, 1.2), (5.4, 0.8)):
    runs.append(nest_shard(ang, 2.28, 0.30, 0.24, length, 1.15, thickness=0.09))
  glow_ob = join(runs, "Glow")
  return [rim, pool, spikes_ob, bubbles, glow_ob]


# --------------------------------------------- Environment 4: the ice breach
#
# Something came up through the sheet: flat slabs of pack ice heaved and tilted
# round a dark hole, with shards driven up between them. The only nest built
# out of straight edges.

def nest_tundra():
  slabs = []
  rnd = random.Random(91)
  for i in range(9):
    ang = i / 9.0 * math.pi * 2 + rnd.uniform(-0.1, 0.1)
    slabs.append(nest_shard(ang, 1.98 + rnd.uniform(-0.12, 0.12), 0.26,
                            rnd.uniform(0.55, 0.80), rnd.uniform(0.70, 1.05),
                            rnd.uniform(-0.55, -0.22), thickness=0.17))
  rim = join(slabs, "Rim")

  pool = nest_pool(1.42, 0.10, verts=14)

  # Crust: snow still lying on the heaved slabs, which is what stops the ring
  # reading as bare blue rock on a white board.
  crust = []
  rnd = random.Random(92)
  for i in range(7):
    ang = i / 7.0 * math.pi * 2 + 0.4
    r = 2.05
    pos = Vector((math.cos(ang) * r, math.sin(ang) * r, 0.52))
    crust.append(plate(pos, Vector((0, 0, 1)), rnd.uniform(0.30, 0.46),
                       thickness=0.30))
  crust_ob = join(crust, "Crust")

  spikes = []
  rnd = random.Random(93)
  for i in range(7):
    ang = i / 7.0 * math.pi * 2 + 0.25
    spikes.append(spike(ang, 1.62, 0.22, rnd.uniform(1.0, 1.7), 0.22,
                        rnd.uniform(0.12, 0.34), verts=4))
  spikes_ob = join(spikes, "Spikes")

  bubbles = nest_bubbles(((0.42, 0.28, 0.24, 0.18), (-0.50, -0.16, 0.19, 0.18),
                          (0.06, -0.58, 0.15, 0.18), (-0.18, 0.62, 0.13, 0.18)))
  return [rim, pool, crust_ob, spikes_ob, bubbles]


# --------------------------------------------- Environment 5: the lava vent
#
# A collar of hexagonal basalt columns stood round the mouth, lava showing in
# the gaps between them. The columns echo the ones round the ember base's
# footing, so the two landmarks read as the same geology.

def nest_ember():
  columns = []
  rnd = random.Random(101)
  for i in range(11):
    ang = i / 11.0 * math.pi * 2
    h = rnd.uniform(0.70, 1.55)
    r = 1.86 + rnd.uniform(-0.10, 0.10)
    bpy.ops.mesh.primitive_cylinder_add(vertices=6, radius=rnd.uniform(0.30, 0.44),
                                        depth=h,
                                        location=(math.cos(ang) * r,
                                                  math.sin(ang) * r, h * 0.5))
    col = bpy.context.object
    col.rotation_euler = (0, rnd.uniform(-0.18, -0.04), ang)
    apply_all(col)
    columns.append(col)
  columns.append(nest_ring(2.04, 0.52, 0.15, flatten=0.52, segments=12, lump=0.06,
                           seed=102))
  rim = join(columns, "Rim")

  pool = nest_pool(1.44, 0.14, verts=14)

  # Crust: cooled scabs floating on the vent's lip.
  crust = []
  for i in range(6):
    ang = i / 6.0 * math.pi * 2 + 0.5
    crust.append(nest_shard(ang, 1.52, 0.30, 0.34, 0.46, -0.20, thickness=0.13))
  crust_ob = join(crust, "Crust")

  spikes = []
  rnd = random.Random(103)
  for i in range(6):
    ang = i / 6.0 * math.pi * 2 + 0.35
    spikes.append(spike(ang, 2.22, 0.18, rnd.uniform(0.8, 1.3), 0.20,
                        rnd.uniform(0.35, 0.60), verts=4))
  spikes_ob = join(spikes, "Spikes")

  bubbles = nest_bubbles(((0.44, 0.28, 0.26, 0.22), (-0.52, -0.18, 0.20, 0.22),
                          (0.08, -0.60, 0.16, 0.22), (-0.18, 0.64, 0.14, 0.22)))

  # Glow: lava in the gaps between the columns, at ground level round the ring.
  seams = []
  for i in range(11):
    ang = (i + 0.5) / 11.0 * math.pi * 2
    seams.append(nest_shard(ang, 1.80, 0.22, 0.17, 0.52, 0.0, thickness=0.22))
  glow_ob = join(seams, "Glow")
  return [rim, pool, crust_ob, spikes_ob, bubbles, glow_ob]


# ---------------------------------------------- Environment 6: the egg sacs
#
# Bulbous sacs crowded round the mouth with tendrils reaching off them: the
# nest answering the bloom biome's base, which is built the same way. The only
# one whose ring has no ground plane at all - it is all body.

def nest_bloom():
  sacs = []
  for ang, r, radius, stretch in ((0.4, 1.94, 0.76, 0.92), (1.5, 2.00, 0.60, 1.05),
                                  (2.6, 1.90, 0.84, 0.86), (3.7, 2.02, 0.58, 1.02),
                                  (4.7, 1.92, 0.72, 0.95), (5.7, 1.98, 0.54, 1.08)):
    sac = teardrop(radius, stretch, pinch=0.46, segments=10, rings=5)
    sac.location = (math.cos(ang) * r, math.sin(ang) * r, 0.06)
    apply_all(sac)
    sacs.append(sac)
  sacs.append(nest_ring(1.88, 0.46, 0.13, flatten=0.48, segments=12, lump=0.05,
                        seed=111))
  rim = join(sacs, "Rim")

  pool = nest_pool(1.30, 0.16, verts=14)

  spikes = []
  rnd = random.Random(112)
  for i in range(7):
    ang = i / 7.0 * math.pi * 2 + 0.3
    spikes.append(spike(ang, 1.80, 0.26, rnd.uniform(1.1, 1.6), 0.13,
                        rnd.uniform(0.40, 0.62)))
  spikes_ob = join(spikes, "Spikes")

  bubbles = nest_bubbles(((0.40, 0.26, 0.25, 0.24), (-0.46, -0.16, 0.20, 0.24),
                          (0.06, -0.54, 0.16, 0.24), (-0.16, 0.58, 0.14, 0.24)))

  # Glow: pores on the sacs, placed on each sac's own surface the way the
  # bloom base's are - on_dome would put them inside.
  pores = []
  for ang, r, radius, stretch, places in (
      (0.4, 1.94, 0.76, 0.92, ((0.4, 0.58, 0.17), (3.0, 0.48, 0.14))),
      (2.6, 1.90, 0.84, 0.86, ((1.2, 0.62, 0.18), (4.2, 0.50, 0.15))),
      (4.7, 1.92, 0.72, 0.95, ((2.0, 0.56, 0.16), (5.0, 0.46, 0.13)))):
    for pang, t, size in places:
      pos, normal = on_teardrop(radius, stretch, 0.06, pang, t, pinch=0.46)
      pores.append(plate(Vector((pos.x + math.cos(ang) * r,
                                 pos.y + math.sin(ang) * r, pos.z)) + normal * 0.04,
                         normal, size, thickness=0.40))
  glow_ob = join(pores, "Glow")
  return [rim, pool, spikes_ob, bubbles, glow_ob]


# -------------------------------------------- Environment 7: the rot blossom
#
# A ring of broad petals opened round the mouth with filaments standing up out
# of it - a carrion flower. The nastiest shape of the seven, and deliberately
# so: it is the prettiest biome, and the thing at the start of the path should
# not get to blend into it.

def nest_blossom():
  rim = join([nest_ring(1.86, 0.46, 0.16, flatten=0.56, segments=12, lump=0.04,
                        seed=121)], "Rim")

  pool = nest_pool(1.28, 0.14, verts=14)

  # Crust: the petals. Broad, drooping away from the mouth, and overlapping at
  # two radii so the ring does not read as a cog.
  petals = []
  for i in range(7):
    ang = i / 7.0 * math.pi * 2
    petals.append(nest_shard(ang, 2.00, 0.46, 0.64, 1.05, -0.52, thickness=0.10))
  for i in range(7):
    ang = (i + 0.5) / 7.0 * math.pi * 2
    petals.append(nest_shard(ang, 1.74, 0.56, 0.48, 0.80, -0.70, thickness=0.09))
  crust_ob = join(petals, "Crust")

  spikes = []
  rnd = random.Random(122)
  for i in range(9):
    ang = i / 9.0 * math.pi * 2 + 0.2
    spikes.append(spike(ang, 1.30, 0.22, rnd.uniform(1.0, 1.6), 0.10,
                        rnd.uniform(0.04, 0.18)))
  spikes_ob = join(spikes, "Spikes")

  bubbles = nest_bubbles(((0.38, 0.24, 0.24, 0.22), (-0.44, -0.16, 0.19, 0.22),
                          (0.06, -0.52, 0.15, 0.22), (-0.16, 0.56, 0.13, 0.22)))
  return [rim, pool, crust_ob, spikes_ob, bubbles]


def export(parts, name):
  os.makedirs(OUT, exist_ok=True)
  path = os.path.join(OUT, name + ".obj")
  bpy.ops.object.select_all(action='DESELECT')
  for p in parts:
    p.select_set(True)
  # Blender Z-up to Unity Y-up: the exporter's standard conversion.
  bpy.ops.wm.obj_export(filepath=path, export_selected_objects=True,
                        forward_axis='NEGATIVE_Z', up_axis='Y',
                        export_materials=True, export_triangulated_mesh=True,
                        export_normals=True, export_uv=False)
  tris = sum(sum(len(poly.vertices) - 2 for poly in p.data.polygons) for p in parts)
  # Through the world matrix, not the raw coordinates: not every part has had
  # its transform applied, and the printed extents are what the height band in
  # the header is checked against.
  world = [p.matrix_world @ v.co for p in parts for v in p.data.vertices]
  size = max(max(abs(c.x), abs(c.y)) for c in world)
  top = max(c.z for c in world)
  print(f"STRUCTURE {name} parts={len(parts)} tris={tris} "
        f"radius={size:.2f} height={top:.2f} -> {path}")


# One base per environment. The key is the environment's own name on the Unity
# side, kept here so the two lists can be read against each other.
BASES = (
  ("BaseMeadow", base_meadow),      # Environment 1 - meadow, clear day
  ("BaseDunes", base_dunes),        # Environment 2 - wetland sand, warm dusk
  ("BaseMarsh", base_marsh),        # Environment 3 - toxic swamp, night
  ("BaseTundra", base_tundra),      # Environment 4 - frozen tundra
  ("BaseEmber", base_ember),        # Environment 5 - volcanic ash
  ("BaseBloom", base_bloom),        # Environment 6 - alien bioluminescence
  ("BaseBlossom", base_blossom),    # Environment 7 - blossom grove
)


# The nest at the other end of the same path, one per environment, keyed the
# same way. Read this table against BASES above.
NESTS = (
  ("NestMeadow", bacteria_nest),   # Environment 1 - the original crater
  ("NestDunes", nest_dunes),       # Environment 2 - a sand funnel
  ("NestMarsh", nest_marsh),       # Environment 3 - a bog vent
  ("NestTundra", nest_tundra),     # Environment 4 - a breach in the ice
  ("NestEmber", nest_ember),       # Environment 5 - a lava vent
  ("NestBloom", nest_bloom),       # Environment 6 - a cluster of egg sacs
  ("NestBlossom", nest_blossom),   # Environment 7 - a carrion flower
)


def main():
  for name, build in BASES + NESTS:
    fresh()
    export(build(), name)
  print("STRUCTURES_DONE")


main()
