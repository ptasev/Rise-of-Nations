import bpy
from mathutils import Vector, Quaternion, Matrix
from ..formats.bh3.bh3file import BH3File
from time import process_time
import os


class BH3FileImporter:
    def __init__(self, import_normals):
        self._file = None
        self._model = None
        self._armature = None
        self._import_normals = import_normals

    def load(self, ctx, filename):
        start_time = process_time()
        model_name = os.path.splitext(os.path.basename(filename))[0]
        tex_path = filename[:-3] + "tga"

        self._file = BH3File()
        self._file.read(filename)

        collection = bpy.data.collections.new(model_name + "_Coll")
        ctx.scene.collection.children.link(collection)

        self._armature = bpy.data.armatures.new(model_name + "_Arm")
        skin = bpy.data.objects.new(model_name + "_Skin", self._armature)
        skin.location = [0, 0, 0]
        collection.objects.link(skin)
        ctx.view_layer.objects.active = skin
        ctx.view_layer.update()
        bpy.ops.object.mode_set(mode='EDIT')
        self._create_bones(self._file.root_bone, None)

        # create the mesh
        bpy.ops.object.mode_set(mode='OBJECT')
        mesh = bpy.data.meshes.new(model_name + "_Mesh")
        self._model = bpy.data.objects.new(model_name, mesh)
        self._model.location = [0, 0, 0]
        collection.objects.link(self._model)
        ctx.view_layer.objects.active = self._model
        ctx.view_layer.update()

        mesh.from_pydata(self._file.vertices, [], self._file.faces)
        mesh.update(calc_edges=True)
        if self._import_normals:
            mesh.normals_split_custom_set_from_vertices(self._file.normals)
        mesh.use_auto_smooth = True

        uv_layer = mesh.uv_layers.new(name=model_name + "_UV")
        uv_loops = uv_layer.data

        for loop in mesh.loops:
            uv_loops[loop.index].uv = self._file.uvs[loop.vertex_index]

        self._create_vertex_groups(self._file.root_bone)

        mod = self._model.modifiers.new(model_name + "_Arm_Mod", 'ARMATURE')
        mod.object = skin
        mod.use_bone_envelopes = False
        mod.use_vertex_groups = True

        material = bpy.data.materials.new(model_name + "_Mat")
        material.use_nodes = True
        bsdf = material.node_tree.nodes["Principled BSDF"]
        texture = material.node_tree.nodes.new('ShaderNodeTexImage')
        if os.path.isfile(tex_path):
            texture.image = bpy.data.images.load(tex_path)
        material.node_tree.links.new(bsdf.inputs['Base Color'], texture.outputs['Color'])

        mesh.materials.append(material)

        print("BH3 import took {:f} seconds".format(process_time() - start_time))
        return {'FINISHED'}

    def _create_bones(self, bone, parent):
        abone = self._armature.edit_bones.new(bone.name)
        abone.tail = Vector([0, 1, 0])

        if parent:
            abone.parent = parent

        rot_part = Quaternion(bone.rotation).inverted().to_matrix()
        pos_part = Vector(bone.position)
        transform = Matrix.Translation(bone.position) @ rot_part.to_4x4()

        if parent:
            transform = parent.matrix @ transform
            rot_part = transform.to_3x3()
            pos_part = transform.to_translation()

        # abone.transform(transform)
        abone.transform(rot_part)
        abone.translate(pos_part)

        nrm_mtx = transform.to_3x3()
        nrm_mtx.invert()
        nrm_mtx.transpose()

        for vi in range(0, bone.vertex_count):
            vt_ind = vi + bone.vertex_index
            self._file.vertices[vt_ind] = list(transform @ Vector(self._file.vertices[vt_ind]))
            self._file.normals[vt_ind] = list(nrm_mtx @ Vector(self._file.normals[vt_ind]))

        for child in bone.children:
            self._create_bones(child, abone)

    def _create_vertex_groups(self, bone):
        vertex_group = self._model.vertex_groups.new(name=bone.name)
        for vt in range(0, bone.vertex_count):
            vt_ind = vt + bone.vertex_index
            vertex_group.add([vt_ind], 1.0, 'ADD')

        for child in bone.children:
            self._create_vertex_groups(child)
