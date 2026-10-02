import bpy,json,os,sys
from mathutils import Vector
# Decimates the two expensive enemy bodies for mobile.
#
#   1. EnemyMeshOptimization.Export        (Unity, writes the originals as OBJ)
#   2. blender --background --python this.py -- Builds/MeshOptimization
#   3. EnemyMeshOptimization.Import        (Unity, writes Assets/Models/OptimizedEnemies)
#
# Basic and Armored were 161,280 and 245,760 triangles, against 4,078 for the
# Boss and 6,361 for Fast. A late wave holds 30+ enemies, so those two bodies
# alone were most of the triangles on the board.
#
# 8,000 is measured, not guessed: 4,000 facets the trumpet mouths and the eye
# whites badly enough to read as broken art rather than as a cheaper model,
# 12,000 is indistinguishable from 8,000 even in EnemyPreview's close-up, which
# is far nearer than the play camera ever gets. Re-render the close-ups and the
# lineup before changing it.
#
# The originals are left untouched in Assets/Meshes/Enemies; the decimated
# bodies are separate mesh assets and the prefab points at those.
root=os.path.abspath(sys.argv[sys.argv.index('--')+1] if '--' in sys.argv else 'Builds/MeshOptimization')
# Triangle targets. Overridable per run (BASIC_TRIS / ARMORED_TRIS) so the
# budget can be swept and judged on a render instead of guessed at.
targets=[('BasicEnemy',int(os.environ.get('BASIC_TRIS',8000))),
         ('ArmoredEnemy',int(os.environ.get('ARMORED_TRIS',8000)))]
for name,target in targets:
 bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
 bpy.ops.wm.obj_import(filepath=f'{root}/{name}.obj',forward_axis='Y',up_axis='Z')
 ob=bpy.context.selected_objects[0];bpy.context.view_layer.objects.active=ob
 # Axis conversion is whatever the importer does, undone by reading positions
 # through matrix_world below. Verified by comparing the printed bounds against
 # the source OBJ's: they match to under a hundredth of a unit.
 bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.mesh.remove_doubles(threshold=0.000001);bpy.ops.object.mode_set(mode='OBJECT')
 mod=ob.modifiers.new('Mobile simplification','DECIMATE');mod.ratio=min(1,target/len(ob.data.polygons));mod.use_collapse_triangulate=True
 bpy.ops.object.modifier_apply(modifier=mod.name)
 mesh=ob.data
 for p in mesh.polygons:p.use_smooth=True
 mesh.calc_loop_triangles();uv=mesh.uv_layers.active
 vertices=[];normals=[];uvs=[];triangles=[];lookup={}
 for tri in mesh.loop_triangles:
  for li in tri.loops:
   loop=mesh.loops[li];v=mesh.vertices[loop.vertex_index];tex=uv.data[li].uv if uv else (0,0)
   pos=ob.matrix_world@v.co;normal=ob.matrix_world.to_3x3()@mesh.corner_normals[li].vector
   key=tuple(pos)+tuple(normal)+tuple(tex)
   if key not in lookup:
    lookup[key]=len(vertices);vertices.append({'x':pos.x,'y':pos.y,'z':pos.z});normals.append({'x':normal.x,'y':normal.y,'z':normal.z});uvs.append({'x':tex[0],'y':tex[1]})
   triangles.append(lookup[key])
 with open(f'{root}/{name}.json','w')as f:json.dump(dict(vertices=vertices,normals=normals,uv=uvs,triangles=triangles),f)
 print(name,len(vertices),'vertices',len(triangles)//3,'triangles', 'bounds',[(min(v[k]for v in vertices),max(v[k]for v in vertices)) for k in ['x','y','z']])
