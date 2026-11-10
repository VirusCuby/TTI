import bpy, math, numpy as np, bmesh, sys
from mathutils import Vector
def load_aligned():
    """Carga 'Switch Fig.fbx', deshace la rotación y devuelve un objeto en el marco alineado (cm, Z arriba)."""
    bpy.ops.import_scene.fbx(filepath=SRC)
    o=bpy.data.objects["Power Switch"]
    P=np.array([tuple(o.matrix_world@v.co) for v in o.data.vertices])*100
    c=P[:,:2].mean(0)
    r=math.radians(46); R=np.array([[math.cos(r),-math.sin(r)],[math.sin(r),math.cos(r)]])
    q=(P[:,:2]-c)@R.T
    Q=np.column_stack([q,P[:,2]])
    me=bpy.data.meshes.new("sw_aligned")
    bm=bmesh.new(); bm.from_mesh(o.data)
    for v,p in zip(bm.verts,Q): v.co=Vector(p)
    bm.to_mesh(me); bm.free()
    bpy.data.objects.remove(o)
    ob=bpy.data.objects.new("sw",me); bpy.context.scene.collection.objects.link(ob)
    return ob
SRC=None
