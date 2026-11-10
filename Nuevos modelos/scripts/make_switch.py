import bpy, bmesh, math, sys
from mathutils import Vector

out = sys.argv[-1]
bpy.ops.wm.read_factory_settings(use_empty=True)
CM = 0.01
AX, BX = -2.51*CM, 2.07*CM          # terminales A y B (eje X, centro de la tarjeta)
TOP = 0.55*CM                        # altura de la cara superior de la base
PIV_Z = 1.05*CM                      # altura del eje de giro (bisagra)

def mat(name, rgb):
    m = bpy.data.materials.new(name); m.diffuse_color = (*rgb, 1); return m

def box(name, size, loc, bevel=0.0, matname=None, parent=None):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc)
    o = bpy.context.active_object; o.name = name
    o.scale = size; bpy.ops.object.transform_apply(scale=True)
    if bevel:
        bm = bmesh.new(); bm.from_mesh(o.data)
        bmesh.ops.bevel(bm, geom=bm.edges[:], offset=bevel, segments=2, affect='EDGES')
        bm.to_mesh(o.data); bm.free()
    if matname: o.data.materials.append(bpy.data.materials[matname])
    if parent: o.parent = parent
    bpy.ops.object.shade_smooth() if False else None
    return o

def cyl(name, r, h, loc, matname, rot=(0,0,0), verts=24):
    bpy.ops.mesh.primitive_cylinder_add(radius=r, depth=h, vertices=verts, location=loc, rotation=rot)
    o = bpy.context.active_object; o.name = name
    o.data.materials.append(bpy.data.materials[matname]); return o

mat("Interruptor_Base",    (0.10, 0.22, 0.45))   # azul oscuro (plástico)
mat("Interruptor_Borde",   (0.04, 0.05, 0.08))   # casi negro
mat("Interruptor_Metal",   (0.80, 0.50, 0.18))   # cobre / latón
mat("Interruptor_Tornillo",(0.75, 0.76, 0.80))   # plata
mat("Interruptor_Mango",   (0.90, 0.35, 0.05))   # naranja (aislante)
mat("Interruptor_Indicador",(0.50, 0.50, 0.50))  # se tiñe verde / rojo desde Unity

# ---- Base: placa con tapa de borde
box("Interruptor_Base",  (5.9*CM, 3.2*CM, 0.50*CM), (0.0, 0.0, 0.25*CM), 0.04*CM, "Interruptor_Base")
box("Interruptor_Borde", (6.0*CM, 3.3*CM, 0.12*CM), (0.0, 0.0, 0.06*CM), 0.02*CM, "Interruptor_Borde")

# ---- Contactos fijos con tornillo
for nm, x in (("A", AX), ("B", BX)):
    box("Interruptor_Metal_%s" % nm, (0.9*CM, 1.2*CM, 0.25*CM), (x, 0.0, 0.50*CM+0.125*CM), 0.03*CM, "Interruptor_Metal")
    cyl("Interruptor_Tornillo_%s" % nm, 0.30*CM, 0.20*CM, (x, 0.0, 0.75*CM+0.10*CM), "Interruptor_Tornillo")
# Soporte (horquilla) de la bisagra en A: dos postes
for sy in (-0.55*CM, 0.55*CM):
    box("Interruptor_Metal_Poste", (0.5*CM, 0.18*CM, PIV_Z-0.50*CM+0.25*CM), (AX, sy, 0.50*CM+(PIV_Z-0.50*CM+0.25*CM)/2), 0.02*CM, "Interruptor_Metal")
# Mordaza en B (contacto que recibe la cuchilla)
for sy in (-0.38*CM, 0.38*CM):
    box("Interruptor_Metal_Mordaza", (0.50*CM, 0.12*CM, 0.50*CM), (BX, sy, 0.75*CM+0.25*CM), 0.02*CM, "Interruptor_Metal")

# ---- Indicador (se tiñe desde Unity)
cyl("Interruptor_Indicador", 0.42*CM, 0.14*CM, (0.0, -1.05*CM, 0.50*CM+0.06*CM), "Interruptor_Indicador")

# ---- Palanca: origen en la bisagra; cerrada = horizontal, apuntando a +X
bpy.ops.object.empty_add(type='PLAIN_AXES', location=(AX, 0.0, PIV_Z))
pivot = bpy.context.active_object; pivot.name = "Interruptor_Palanca"
L = (BX + 0.40*CM) - AX
# cuchilla (metal)
blade = box("Interruptor_Palanca_Cuchilla", (L, 0.50*CM, 0.12*CM), (AX + L/2, 0.0, PIV_Z), 0.015*CM, "Interruptor_Metal", pivot)
# eje en la bisagra
ax = cyl("Interruptor_Palanca_Eje", 0.16*CM, 1.3*CM, (AX, 0.0, PIV_Z), "Interruptor_Tornillo", rot=(math.radians(90),0,0)); ax.parent = pivot
# mango aislante al extremo
mango = box("Interruptor_Palanca_Mango", (1.0*CM, 0.80*CM, 0.60*CM), (BX + 0.40*CM + 0.45*CM, 0.0, PIV_Z+0.18*CM), 0.10*CM, "Interruptor_Mango", pivot)

# Los hijos conservan su posición mundial al asignar parent (sin matriz inversa): corregimos
for ch in pivot.children:
    ch.matrix_parent_inverse = pivot.matrix_world.inverted()

# Material por objeto (Unity asigna por nombre del objeto): ya hecho arriba
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=out, use_selection=True, apply_scale_options='FBX_SCALE_ALL',
    axis_forward='-Z', axis_up='Y', bake_space_transform=False, add_leaf_bones=False,
    object_types={'MESH','EMPTY'}, mesh_smooth_type='FACE')
print("EXPORT OK", out)
