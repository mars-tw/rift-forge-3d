"""Run with Blender --background --python; public CC0 sources are in Logs/r6/model-source."""
import bpy
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'Assets/_Project/Resources/Characters'
OUT.mkdir(parents=True, exist_ok=True)
receipt = []
for source, name, main, accent in [
    ('Knight', 'Sentinel', (0.08, .48, .43, 1), (.9, .46, .12, 1)),
    ('Zombie', 'Stalker', (.48, .18, .62, 1), (.25, .7, .52, 1)),
    ('Knight', 'Lancer', (.62, .12, .19, 1), (.95, .45, .22, 1)),
    ('Viking', 'Warden', (.58, .28, .09, 1), (1, .65, .18, 1)),
]:
    bpy.ops.wm.open_mainfile(filepath=str(ROOT / f'Logs/r6/model-source/{source}.blend'))
    # Preserve the author's facial geometry, rig, weights, and actions. Restyle materials.
    for mat in bpy.data.materials:
        low = mat.name.lower()
        color = (.72, .43, .26, 1) if 'skin' in low else accent if any(w in low for w in ('detail', 'red', 'hair')) else main
        if 'dark' in low or 'black' in low:
            color = tuple(c * .52 for c in main[:3]) + (1,)
        mat.diffuse_color = color
        mat.use_nodes = True
        bsdf = mat.node_tree.nodes.get('Principled BSDF')
        if bsdf:
            bsdf.inputs['Base Color'].default_value = color
            bsdf.inputs['Metallic'].default_value = .35 if 'armor' in low else 0
            bsdf.inputs['Roughness'].default_value = .55
    meshes = [o for o in bpy.data.objects if o.type == 'MESH']
    rigs = [o for o in bpy.data.objects if o.type == 'ARMATURE']
    bpy.ops.export_scene.fbx(filepath=str(OUT / f'{name}.fbx'), object_types={'MESH', 'ARMATURE'},
        use_selection=False, add_leaf_bones=False, bake_anim=True, bake_anim_use_all_actions=True,
        bake_anim_use_nla_strips=False, axis_forward='-Z', axis_up='Y')
    receipt.append({'name': name, 'source': source, 'vertices': sum(len(o.data.vertices) for o in meshes),
                    'bones': sum(len(o.data.bones) for o in rigs), 'actions': [a.name for a in bpy.data.actions],
                    'materials': {m.name: list(m.diffuse_color) for m in bpy.data.materials}})
(ROOT / 'docs/store/character-conversion.json').write_text(json.dumps(receipt, indent=2), encoding='utf8')
