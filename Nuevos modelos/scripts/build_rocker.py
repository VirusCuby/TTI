# Interruptor de balancin a partir de "Switch Fig.fbx" (modelo original del proyecto).
# Uso: blender -b --python build_rocker.py -- <carpeta con common.py> <Switch Fig.fbx> <salida.fbx> [prefijo_preview]
import bpy, bmesh, math, sys, numpy as np
from mathutils import Vector
sys.path.insert(0, sys.argv[-4] if len(sys.argv)>4 and sys.argv[-4].endswith("scratchpad") else "")
args = sys.argv[sys.argv.index("--")+1:]
import importlib; sys.path.insert(0, args[0]); import common
common.SRC = args[1]; OUT = args[2]; PREV = args[3] if len(args) > 3 else None

bpy.ops.wm.read_factory_settings(use_empty=True)
ob = common.load_aligned()          # cm, Z arriba, alineado a los ejes
bm = bmesh.new(); bm.from_mesh(ob.data)

CUT = 1.83
geom = bm.verts[:] + bm.edges[:] + bm.faces[:]
bmesh.ops.bisect_plane(bm, geom=geom, plane_co=(0, 0, CUT), plane_no=(0, 0, 1))

def grupo(f):
    z = f.calc_center_median().z
    if z > CUT: return "tecla"
    if z >= 1.56: return "marco"
    if z >= 0.58: return "cuerpo"
    return "pata"
caras = {"tecla": [], "marco": [], "cuerpo": [], "pata": []}
for f in bm.faces: caras[grupo(f)].append(f)
print({k: len(v) for k, v in caras.items()})

def extraer(faces):
    nb = bmesh.new(); mapa = {}
    for f in faces:
        vs = []
        for v in f.verts:
            if v.index not in mapa: mapa[v.index] = nb.verts.new(v.co)
            vs.append(mapa[v.index])
        try: nb.faces.new(vs)
        except ValueError: pass
    return nb

# ---- Transformacion global: centro del marco -> origen, escala k, sobre la base
k = 2.0; PLACA_H = 0.25; CX = -0.22
allv = np.array([v.co[:] for v in bm.verts])
marco = np.array([v.co[:] for f in caras["marco"] for v in f.verts])
cx0 = (marco[:,0].min()+marco[:,0].max())/2; cy0 = (marco[:,1].min()+marco[:,1].max())/2
print("marco x", marco[:,0].min(), marco[:,0].max(), "y", marco[:,1].min(), marco[:,1].max(), "centro", cx0, cy0)
def T(co): return Vector((k*(co[0]-cx0)+CX, k*(co[1]-cy0), k*co[2]+PLACA_H))

CM = 0.01
def nuevo_objeto(nombre, nb, transformar=True, z_stretch=None):
    for v in nb.verts:
        c = v.co.copy()
        if z_stretch: c.z = CUT + (c.z-CUT)*z_stretch
        v.co = T(c) * CM if transformar else c*CM
    me = bpy.data.meshes.new(nombre); nb.to_mesh(me); nb.free()
    o = bpy.data.objects.new(nombre, me); bpy.context.scene.collection.objects.link(o)
    for p in me.polygons: p.use_smooth = False
    return o

obj_marco  = nuevo_objeto("Interruptor_Marco",  extraer(caras["marco"]))
obj_cuerpo = nuevo_objeto("Interruptor_Cuerpo", extraer(caras["cuerpo"]))
obj_pata   = nuevo_objeto("Interruptor_Pata",   extraer(caras["pata"]))

# ---- Tecla: cerrar el fondo, estirar un poco y colgarla de un pivote
tb = extraer(caras["tecla"])
borde = [e for e in tb.edges if len(e.link_faces) == 1]
print("aristas de borde de la tecla:", len(borde))
bmesh.ops.holes_fill(tb, edges=borde, sides=len(borde))
tv = np.array([v.co[:] for v in tb.verts])
pcx = (tv[:,0].min()+tv[:,0].max())/2; pcy = (tv[:,1].min()+tv[:,1].max())/2
print("tecla bbox", tv.min(0), tv.max(0))
STRETCH = 1.5
obj_tecla = nuevo_objeto("Interruptor_Palanca_Tecla", tb, z_stretch=STRETCH)
piv_pos = T(Vector((pcx, pcy, CUT))) * CM
bpy.ops.object.empty_add(type='PLAIN_AXES', location=piv_pos)
pivote = bpy.context.active_object; pivote.name = "Interruptor_Palanca"
obj_tecla.parent = pivote; obj_tecla.matrix_parent_inverse = pivote.matrix_world.inverted()

# ---- Patas: posiciones para las pistas (en cm, ya transformadas)
pv = np.array([T(v.co)[:] for f in caras["pata"] for v in f.verts])
pins = pv[pv[:,2] < PLACA_H + 0.9]
xs = sorted(set(np.round(pins[:,0], 1)))
print("x de patas (cm):", xs)

# ---- Base, pistas y terminales
def mat_box(nombre, size, loc):
    bpy.ops.mesh.primitive_cube_add(size=1, location=(loc[0]*CM, loc[1]*CM, loc[2]*CM))
    o = bpy.context.active_object; o.name = nombre
    o.scale = (size[0]*CM, size[1]*CM, size[2]*CM); bpy.ops.object.transform_apply(scale=True)
    return o
def cil(nombre, r, h, loc):
    bpy.ops.mesh.primitive_cylinder_add(radius=r*CM, depth=h*CM, vertices=24, location=(loc[0]*CM, loc[1]*CM, loc[2]*CM))
    o = bpy.context.active_object; o.name = nombre; return o

AX, BX = -2.51, 2.07
mat_box("Interruptor_Base", (6.4, 3.4, PLACA_H), (CX, 0, PLACA_H/2))
pata_izq = min(xs); pata_der = [x for x in xs if abs(x-CX) < 0.3]
pata_der = pata_der[0] if pata_der else max(xs)
# pista A: del terminal A a la pata izquierda;  pista B: del terminal B a la pata central
def pista(nombre, x0, x1):
    x1 = x1 + (0.45 if x1 > x0 else -0.45)   # que la pata quede apoyada sobre la pista
    xm = (x0+x1)/2; L = abs(x1-x0)
    mat_box(nombre, (L, 0.55, 0.10), (xm, 0, PLACA_H+0.05))
pista("Interruptor_Metal_PistaA", AX, pata_izq)
pista("Interruptor_Metal_PistaB", BX, pata_der)
for nm, x in (("A", AX), ("B", BX)):
    cil("Interruptor_Metal_Pad"+nm, 0.55, 0.10, (x, 0, PLACA_H+0.05))
    cil("Interruptor_Tornillo_"+nm, 0.30, 0.22, (x, 0, PLACA_H+0.10+0.11))

# bordes de la placa un poco biselados no hacen falta; materiales por nombre en Unity
bpy.ops.object.select_all(action='DESELECT')
for o in bpy.data.objects:
    if o.name != "sw": o.select_set(True)
bpy.data.objects.remove(bpy.data.objects["sw"])
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=OUT, use_selection=True, apply_scale_options='FBX_SCALE_ALL',
    axis_forward='-Z', axis_up='Y', bake_space_transform=False, add_leaf_bones=False,
    object_types={'MESH','EMPTY'}, mesh_smooth_type='FACE')
print("EXPORT OK", OUT)

if PREV:
    colores = {"Base":(0.10,0.22,0.45),"Marco":(0.05,0.06,0.08),"Cuerpo":(0.12,0.13,0.16),"Pata":(0.75,0.76,0.80),
               "Metal":(0.80,0.50,0.18),"Tornillo":(0.75,0.76,0.80),"Palanca_Tecla":(0.85,0.2,0.15)}
    for o in bpy.data.objects:
        if o.type == 'MESH':
            c = next((v for kk, v in colores.items() if kk in o.name), (0.5,0.5,0.5))
            m = bpy.data.materials.new(o.name); m.diffuse_color = (*c, 1); o.data.materials.clear(); o.data.materials.append(m)
    sc = bpy.context.scene; sc.render.engine = 'BLENDER_WORKBENCH'
    sc.display.shading.light = 'STUDIO'; sc.display.shading.color_type = 'MATERIAL'
    sc.render.resolution_x = 900; sc.render.resolution_y = 600
    sc.world = bpy.data.worlds.new("w"); sc.world.color = (0.8, 0.85, 0.9)
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(cam); sc.camera = cam
    cam.data.clip_start = 0.001; cam.data.clip_end = 10
    cam.location = (0.0, -0.13, 0.10)
    cam.rotation_euler = (Vector((0, 0, 0.012)) - cam.location).to_track_quat('-Z', 'Y').to_euler()
    for nombre, ang in (("cerrado", 0), ("abierto", 22)):
        pivote.rotation_euler = (0, math.radians(-ang), 0)
        sc.render.filepath = f"{PREV}_{nombre}.png"; bpy.ops.render.render(write_still=True)
