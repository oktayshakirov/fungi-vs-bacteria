# Authors the two landmark structures on every board: the fungi's base (a
# mushroom house, where the path ends) and the bacteria's spawn nest (a crater,
# where the path starts).
#
# Run headless from the repo root:
#   blender --background --python Tools/Blender/structures.py
#
# Writes Assets/Resources/Structures/{MotherMushroom,BacteriaNest}.obj.
#
# Why authored meshes here when the rest of the scenery is generated in code
# (MeshFactory): these two were stacks of Unity primitives - a lilac sphere on a
# cylinder, and a black disc ringed with shards - and neither read as anything
# at play distance. They are the two things the player has to find on every
# board, so they get real silhouettes. Everything else stays procedural.
#
# Authoring contract, relied on by LevelDecorator:
#   * Blender Z-up, exported Y-up; the object sits on the ground plane and is
#     centred on the origin, sized in Unity units (a board cell is 5).
#   * the house's door faces Blender -Y. The Unity side turns the whole thing to
#     face the path, so this only has to be consistent.
#   * one material per part, NAMED after the part: LevelDecorator colours each
#     submesh from the biome palette by its material name (Cap, Spots, Stem,
#     Door, Windows, Plinth / Rim, Pool, Spikes, Bubbles). The .mtl colours are
#     ignored.
#   * low-poly and flat-shaded, to match MeshFactory's scenery.
import bmesh
import bpy
import math
import os
import random

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


# ------------------------------------------------------------------ mushroom

def mother_mushroom():
  # Plinth: a low ring of flat stones the house stands on.
  bpy.ops.mesh.primitive_cylinder_add(vertices=10, radius=2.35, depth=0.32, location=(0, 0, 0.16))
  plinth = bpy.context.object
  jitter(plinth, 0.06, 1)
  apply_all(plinth)
  finish(plinth, "Plinth")

  # Stem: a slightly bulging, tapering trunk.
  bpy.ops.mesh.primitive_cylinder_add(vertices=12, radius=1.0, depth=2.5, location=(0, 0, 1.55))
  stem = bpy.context.object
  for v in stem.data.vertices:
    t = (v.co.z + 1.25) / 2.5          # 0 at the base, 1 at the top
    bulge = 1.0 + 0.16 * math.sin(t * math.pi) - 0.14 * t
    v.co.x *= bulge
    v.co.y *= bulge
  jitter(stem, 0.03, 2)
  apply_all(stem)
  finish(stem, "Stem")

  # Cap: a squashed dome with an overhanging rim and a flat underside.
  bpy.ops.mesh.primitive_uv_sphere_add(segments=16, ring_count=8, radius=2.55, location=(0, 0, 0))
  cap = bpy.context.object
  bm = bmesh.new()
  bm.from_mesh(cap.data)
  # Drop the lower hemisphere, then close it with a flat disc.
  lower = [v for v in bm.verts if v.co.z < -0.01]
  bmesh.ops.delete(bm, geom=lower, context='VERTS')
  edges = [e for e in bm.edges if e.is_boundary]
  bmesh.ops.holes_fill(bm, edges=edges, sides=0)
  bm.to_mesh(cap.data)
  bm.free()
  rnd = random.Random(3)
  for v in cap.data.vertices:
    v.co.z *= 0.62
    # A soft wobble round the rim so it is not a perfect lathe.
    ang = math.atan2(v.co.y, v.co.x)
    wob = 1.0 + 0.045 * math.sin(ang * 5.0 + 0.7)
    v.co.x *= wob
    v.co.y *= wob
    if v.co.z > 0.05:
      v.co.z += rnd.uniform(-0.05, 0.05)
  cap.location = (0, 0, 2.62)
  apply_all(cap)
  finish(cap, "Cap")

  # Spots: flattened blobs sat on the cap's surface, facing outward.
  spots = []
  spot_rnd = random.Random(4)
  placements = [(0.0, 0.0, 0.62), (0.35, 0.3, 0.42), (2.2, 0.55, 0.40), (3.9, 0.5, 0.36),
                (5.2, 0.6, 0.40), (1.2, 0.8, 0.30), (3.0, 0.82, 0.28), (4.6, 0.85, 0.26)]
  for i, (ang, polar, size) in enumerate(placements):
    # polar 0 = top of the dome, 1 = the rim.
    theta = polar * math.pi * 0.5
    r = 2.55
    x = math.cos(ang) * math.sin(theta) * r
    y = math.sin(ang) * math.sin(theta) * r
    z = math.cos(theta) * r * 0.62 + 2.62
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1, radius=size, location=(0, 0, 0))
    s = bpy.context.object
    s.scale = (1.0, 1.0, 0.32)
    normal = (x, y, (z - 2.62) / (0.62 * 0.62))
    s.rotation_mode = 'QUATERNION'
    from mathutils import Vector
    s.rotation_quaternion = Vector((0, 0, 1)).rotation_difference(Vector(normal).normalized())
    s.location = (x, y, z)
    spots.append(s)
  spots_ob = join(spots, "Spots")

  # Door: an arched plank door on the front (-Y) of the stem.
  bpy.ops.mesh.primitive_cube_add(size=1, location=(0, -1.02, 0.95))
  door = bpy.context.object
  door.scale = (0.78, 0.22, 1.25)
  apply_all(door)
  bpy.ops.mesh.primitive_cylinder_add(vertices=10, radius=0.39, depth=0.22,
                                      location=(0, -1.02, 1.57), rotation=(math.pi / 2, 0, 0))
  arch = bpy.context.object
  door_ob = join([door, arch], "Door")

  # Windows: two round lit windows higher up the stem, and one in the cap.
  wins = []
  for (x, z, r) in ((-0.55, 2.05, 0.2), (0.58, 1.85, 0.17)):
    bpy.ops.mesh.primitive_cylinder_add(vertices=10, radius=r, depth=0.2,
                                        location=(x, -0.93, z), rotation=(math.pi / 2, 0, 0))
    wins.append(bpy.context.object)
  bpy.ops.mesh.primitive_cylinder_add(vertices=10, radius=0.26, depth=0.3,
                                      location=(0.9, -2.0, 3.25), rotation=(1.1, 0, 0.25))
  wins.append(bpy.context.object)
  win_ob = join(wins, "Windows")

  return [plinth, stem, cap, spots_ob, door_ob, win_ob]


# ---------------------------------------------------------------- bacteria nest

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
  bpy.ops.mesh.primitive_cylinder_add(vertices=16, radius=1.62, depth=0.12, location=(0, 0, 0.12))
  pool = bpy.context.object
  apply_all(pool)
  finish(pool, "Pool")

  # Spikes: bent cilia round the rim, leaning outward.
  spikes = []
  spike_rnd = random.Random(8)
  for i in range(9):
    ang = i / 9.0 * math.pi * 2 + spike_rnd.uniform(-0.15, 0.15)
    h = spike_rnd.uniform(1.4, 2.1)
    bpy.ops.mesh.primitive_cone_add(vertices=5, radius1=0.3, radius2=0.0, depth=h,
                                    location=(0, 0, h * 0.5))
    c = bpy.context.object
    lean = spike_rnd.uniform(0.25, 0.45)
    c.rotation_euler = (0.0, lean, ang)
    c.location = (math.cos(ang) * 2.15, math.sin(ang) * 2.15, 0.35)
    spikes.append(c)
  spikes_ob = join(spikes, "Spikes")

  # Bubbles: a few blobs breaking the pool's surface.
  bubbles = []
  for (x, y, r) in ((0.5, 0.3, 0.30), (-0.6, -0.2, 0.22), (0.1, -0.7, 0.18), (-0.2, 0.75, 0.15)):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1, radius=r, location=(x, y, 0.2))
    bubbles.append(bpy.context.object)
  bubbles_ob = join(bubbles, "Bubbles")

  return [rim, pool, spikes_ob, bubbles_ob]


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
  print(f"STRUCTURE {name} parts={len(parts)} tris={tris} -> {path}")


def main():
  fresh()
  export(mother_mushroom(), "MotherMushroom")
  fresh()
  export(bacteria_nest(), "BacteriaNest")
  print("STRUCTURES_DONE")


main()
