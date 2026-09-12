# Authors the four "trait" meshes that tell the variety enemy types apart.
#
# Run headless:
#   blender --background --python Tools/Blender/enemy_traits.py -- <outDir>
#
# Verified byte-identical (ignoring the version comment the exporter writes)
# under Blender 4.3.2 and 5.2.1, so either will do. `blender` on PATH is the
# Homebrew cask's wrapper and tracks the current release; 4.3.2 is kept at
# /Applications/Blender 4.3.app because it is what the committed meshes were
# first generated with.
#
# Why traits and not whole new enemies: the four base models in
# Assets/Meshes/Enemies are authored, detailed and heavy (up to 124k verts),
# and they carry the family look (shared BodySkin / EyeWhite / Iris / Pupile
# materials). A hand-scripted full body would read as a different game, and
# four more 20MB OBJs would bloat the repo. So each new type reuses a base
# body and gets one small piece of extra geometry that changes its SILHOUETTE.
#
# Authoring contract, relied on by EnemyArtSetup on the Unity side:
#   * +Y is up, the mesh is centred on the origin in X/Z and sits on Y=0,
#   * it fits inside a 1x1x1 box, so the Unity tool can scale it by the base
#     body's measured bounds instead of baking a per-base magic number here,
#   * one object per file, one material, triangle count in the hundreds.
import bpy
import math
import os
import sys


def fresh():
  bpy.ops.wm.read_factory_settings(use_empty=True)


def mesh(name, verts, faces, color):
  me = bpy.data.meshes.new(name)
  me.from_pydata(verts, [], faces)
  me.validate()
  me.update()
  ob = bpy.data.objects.new(name, me)
  bpy.context.collection.objects.link(ob)
  mat = bpy.data.materials.new(name + "Mat")
  mat.use_nodes = True
  mat.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = color
  me.materials.append(mat)
  return ob


def cone(verts, faces, base, tip, radius, axis, segments=6):
  """A tapered spike: a fan of `segments` sides from a ring at `base` to `tip`."""
  # Build a ring perpendicular to the spike direction.
  d = [tip[i] - base[i] for i in range(3)]
  length = math.sqrt(sum(c * c for c in d)) or 1.0
  d = [c / length for c in d]
  # Any vector not parallel to d, to seed the perpendicular basis.
  seed = (0.0, 0.0, 1.0) if abs(d[2]) < 0.9 else (1.0, 0.0, 0.0)
  u = [d[1] * seed[2] - d[2] * seed[1],
       d[2] * seed[0] - d[0] * seed[2],
       d[0] * seed[1] - d[1] * seed[0]]
  un = math.sqrt(sum(c * c for c in u)) or 1.0
  u = [c / un for c in u]
  v = [d[1] * u[2] - d[2] * u[1],
       d[2] * u[0] - d[0] * u[2],
       d[0] * u[1] - d[1] * u[0]]
  start = len(verts)
  for s in range(segments):
    a = 2.0 * math.pi * s / segments
    verts.append(tuple(base[i] + radius * (math.cos(a) * u[i] + math.sin(a) * v[i])
                       for i in range(3)))
  verts.append(tuple(tip))
  apex = start + segments
  for s in range(segments):
    faces.append((start + s, start + (s + 1) % segments, apex))
  faces.append(tuple(range(start, start + segments)))  # cap the base
  return axis


def ico(verts, faces, centre, radius, rings=5, segments=8):
  """A low-poly sphere as a lat/long grid - cheaper than subdividing an ico."""
  start = len(verts)
  verts.append((centre[0], centre[1] + radius, centre[2]))
  for r in range(1, rings):
    phi = math.pi * r / rings
    for s in range(segments):
      th = 2.0 * math.pi * s / segments
      verts.append((centre[0] + radius * math.sin(phi) * math.cos(th),
                    centre[1] + radius * math.cos(phi),
                    centre[2] + radius * math.sin(phi) * math.sin(th)))
  verts.append((centre[0], centre[1] - radius, centre[2]))
  bottom = len(verts) - 1
  for s in range(segments):
    faces.append((start, start + 1 + s, start + 1 + (s + 1) % segments))
  for r in range(rings - 2):
    a = start + 1 + r * segments
    b = a + segments
    for s in range(segments):
      faces.append((a + s, b + s, b + (s + 1) % segments, a + (s + 1) % segments))
  last = start + 1 + (rings - 2) * segments
  for s in range(segments):
    faces.append((bottom, last + (s + 1) % segments, last + s))


def cilia_fringe():
  """Swarm: a fringe of cilia flicked backwards, so a tiny body reads as FAST
  and as a colony rather than as a shrunken Basic enemy."""
  verts, faces = [], []
  count = 12
  for i in range(count):
    a = 2.0 * math.pi * i / count
    bx, bz = 0.34 * math.cos(a), 0.34 * math.sin(a)
    # Splay outward and down so the fringe reads as a skirt of cilia. Note the
    # normalize() pass re-centres X/Z, so a deliberate fore/aft offset does NOT
    # survive export - per-type placement is the Unity tool's job instead.
    tx = bx * 1.55
    tz = bz * 1.55
    cone(verts, faces, (bx, 0.06, bz), (tx, -0.32, tz), 0.035, None, segments=4)
  return mesh("CiliaFringe", verts, faces, (0.85, 0.92, 0.55, 1.0))


def carapace():
  """Shielded: overlapping plates banded across the back, open at the front AND
  at the crown so the body, its spikes and its eyes all still read. Toggled off
  in-game when the shield breaks.

  The latitude band is the load-bearing choice. An earlier version ran the
  plates from 12 to 74 degrees, i.e. across the whole top hemisphere, and no
  amount of narrowing the azimuth helped: seen from the game's downward camera
  it was a lid over the enemy whichever way it faced. Starting the band well
  below the pole is what keeps the crown clear."""
  verts, faces = [], []
  plates = 5
  span = math.radians(150)          # arc the plates cover, centred behind
  for p in range(plates):
    t = p / (plates - 1.0)
    yaw = -span * 0.5 + span * t + math.pi  # centred on +X, i.e. the back
    # Each plate is a curved strip: two arcs of latitude at slightly
    # different radii, so plates overlap like a beetle's elytra.
    inner, outer = 0.44, 0.56
    lat0, lat1 = math.radians(38), math.radians(88)
    steps = 5
    width = span / plates * 0.62
    ring = []
    for s in range(steps + 1):
      lat = lat0 + (lat1 - lat0) * s / steps
      r = inner + (outer - inner) * s / steps
      for side in (-1, 1):
        y = yaw + side * width
        ring.append((r * math.sin(lat) * math.cos(y),
                     r * math.cos(lat),
                     r * math.sin(lat) * math.sin(y)))
    base = len(verts)
    verts.extend(ring)
    for s in range(steps):
      a = base + s * 2
      faces.append((a, a + 1, a + 3, a + 2))
  return mesh("Carapace", verts, faces, (0.55, 0.78, 1.0, 1.0))


def bud_lobes():
  """Splitter: daughter cells already bulging out of the parent, so the player
  can see it is going to divide before it dies and does."""
  verts, faces = [], []
  ico(verts, faces, (0.26, 0.30, 0.20), 0.22)
  ico(verts, faces, (0.20, 0.14, -0.28), 0.17)
  ico(verts, faces, (0.34, 0.46, -0.06), 0.12)
  return mesh("BudLobes", verts, faces, (0.95, 0.65, 0.35, 1.0))


def spore_crown():
  """Healer: a cap on a stalk with a ring of spore orbs under its rim. The cap
  is the tallest trait, so a healer is pickable out of a pack from above."""
  verts, faces = [], []
  # Stalk.
  cone(verts, faces, (0.0, 0.0, 0.0), (0.0, 0.34, 0.0), 0.07, None, segments=6)
  # Cap: a shallow dome, built as a fan so it stays cheap.
  #
  # Winding matters here and cost a render to spot. The obvious fan order
  # (apex, rim[s], rim[s+1]) puts the cap's normals DOWNWARD, so URP's backface
  # culling removed the cap from every shot taken from above - which is every
  # shot in this game. The crown then read as six orbs floating around a bare
  # stalk. The order below faces the dome up.
  rim, top, segs = 0.30, 0.50, 10
  start = len(verts)
  verts.append((0.0, top, 0.0))
  for s in range(segs):
    a = 2.0 * math.pi * s / segs
    verts.append((rim * math.cos(a), 0.36, rim * math.sin(a)))
  for s in range(segs):
    faces.append((start, start + 1 + (s + 1) % segs, start + 1 + s))
  faces.append(tuple(reversed(range(start + 1, start + 1 + segs))))
  # Spore orbs hanging under the rim.
  for s in range(6):
    a = 2.0 * math.pi * s / 6
    ico(verts, faces, (0.23 * math.cos(a), 0.26, 0.23 * math.sin(a)), 0.055,
        rings=4, segments=6)
  return mesh("SporeCrown", verts, faces, (0.45, 0.95, 0.55, 1.0))


def normalize(ob):
  """Fit the mesh into a 1x1x1 box, centred in X/Z, sitting on Y=0 upward.

  The Unity side rescales by the base body's measured bounds, so the only thing
  that has to be true here is that the box is unit-sized and the origin is
  where the trait should attach."""
  vs = [v.co for v in ob.data.vertices]
  mnx, mxx = min(v.x for v in vs), max(v.x for v in vs)
  mny, mxy = min(v.y for v in vs), max(v.y for v in vs)
  mnz, mxz = min(v.z for v in vs), max(v.z for v in vs)
  extent = max(mxx - mnx, mxy - mny, mxz - mnz) or 1.0
  cx, cz = (mnx + mxx) * 0.5, (mnz + mxz) * 0.5
  for v in ob.data.vertices:
    v.co.x = (v.co.x - cx) / extent
    v.co.y = (v.co.y - mny) / extent
    v.co.z = (v.co.z - cz) / extent


def export(ob, out_dir, name):
  path = os.path.join(out_dir, name + ".obj")
  bpy.ops.object.select_all(action='DESELECT')
  ob.select_set(True)
  bpy.context.view_layer.objects.active = ob
  # The meshes above are authored directly in UNITY's convention (+Y up, and
  # -X facing to match the base models), so the exporter's axis conversion must
  # be the IDENTITY. Blender's own axes are forward=Y / up=Z; passing Unity's
  # -Z/Y here instead silently rotates every trait onto its side, which is how
  # the first run came out with Y centred and Z one-sided.
  bpy.ops.wm.obj_export(filepath=path, export_selected_objects=True,
                        forward_axis='Y', up_axis='Z',
                        export_materials=False, export_triangulated_mesh=True)
  tris = sum(len(p.vertices) - 2 for p in ob.data.polygons)
  print(f"TRAIT {name} verts={len(ob.data.vertices)} tris={tris} -> {path}")


def main():
  out_dir = sys.argv[-1]
  os.makedirs(out_dir, exist_ok=True)
  for name, build in (("CiliaFringe", cilia_fringe),
                      ("Carapace", carapace),
                      ("BudLobes", bud_lobes),
                      ("SporeCrown", spore_crown)):
    fresh()
    ob = build()
    normalize(ob)
    export(ob, out_dir, name)
  print("TRAITS_DONE")


main()
