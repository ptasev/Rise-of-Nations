import bpy
from mathutils import Vector
from ..formats.bh3.bh3bone import BH3Bone
from ..formats.bh3.bh3file import BH3File
from time import process_time


class BH3FileExporter:
    def __init__(self):
        self._file = None
        self._model = None
        self._mesh = None
        self._vertex_count = 0
        self._normals = []
        self._uv_loops = None
        self._vertex_old_new_index = dict()
        self._traversed_loops = set()

    def save(self, ctx, filename):
        start_time = process_time()
        self._file = BH3File()
        self._model = ctx.scene.objects.active
        # self._mesh = object_.data
        self._mesh = self._model.to_mesh(ctx.scene, True, 'PREVIEW', True)
        self._mesh.calc_normals_split()
        self._vertex_count = len(self._mesh.vertices)

        uv_layer = self._mesh.uv_layers.active
        self._uv_loops = uv_layer.data if uv_layer is not None else None

        for v in self._mesh.vertices:
            average_normal = Vector()
            for loop in self._mesh.loops:
                if loop.vertex_index == v.index:
                    average_normal += loop.normal
            average_normal.normalize()
            self._normals.append(average_normal)

        # TODO: Find the skin mod properly
        skin_mod = self._model.modifiers[0]
        skin = skin_mod.object
        ctx.scene.objects.active = skin
        bpy.ops.object.mode_set(mode='EDIT')
        armature = skin.data

        self._file.root_bone = self._create_bh3_bones(armature.edit_bones[0], [0])

        for face in self._mesh.polygons:
            self._file.faces.append([face.vertices[0],
                                     face.vertices[1],
                                     face.vertices[2]])

        bpy.ops.object.mode_set(mode='OBJECT')
        ctx.scene.objects.active = self._model
        bpy.data.meshes.remove(self._mesh)

        self._file.write(filename)

        print('Export took {:f} seconds'.format(process_time() - start_time))
        return {'FINISHED'}

    def _create_bh3_bones(self, abone, vertex_index):
        bone = BH3Bone()
        bone.name = abone.name

        bone_vertices = dict()
        transform_inverse = abone.matrix.inverted()
        nrm_mtx = transform_inverse.transposed().inverted()

        for loop in self._mesh.loops:
            if not (loop.index in self._traversed_loops):
                new_vertex_index = -1

                if not (loop.vertex_index in self._vertex_old_new_index):
                    # If vertex does not already belong to a bone
                    for vg in self._mesh.vertices[loop.vertex_index].groups:
                        # only one bone may take a vertex, so break after first find
                        if self._model.vertex_groups[vg.group].name == abone.name:
                            new_vertex_index = len(self._vertex_old_new_index)
                            bone_vertices[loop.vertex_index] = dict()
                            break

                if loop.vertex_index in bone_vertices:
                    self._traversed_loops.add(loop.index)
                    if new_vertex_index >= 0:
                        # First time this vertex has been seen
                        self._file.vertices.append(
                            list(transform_inverse * self._mesh.vertices[loop.vertex_index].co))
                        self._file.normals.append(list(nrm_mtx * self._normals[loop.vertex_index]))

                        if self._uv_loops:
                            uv = tuple(self._uv_loops[loop.index].uv)
                            self._file.uvs.append(list(uv))
                            bone_vertices[loop.vertex_index][uv] = new_vertex_index
                        else:
                            self._file.uvs.append([0, 1])

                        self._vertex_old_new_index[loop.vertex_index] = new_vertex_index
                        loop.vertex_index = new_vertex_index
                    else:
                        # Second+ time we see this vertex index
                        if not self._uv_loops:
                            loop.vertex_index = self._vertex_old_new_index[loop.vertex_index]
                            continue

                        uv_dict = bone_vertices[loop.vertex_index]
                        uv = tuple(self._uv_loops[loop.index].uv)

                        # print(str(loop.vertex_index) + ' ' + str(uv_dict))

                        if uv in uv_dict:
                            # We have UVs, but they have been accounted for
                            loop.vertex_index = uv_dict[uv]
                        else:
                            # Unique UV, copy the vertex to avoid seams
                            print('Hellooooooooooooooooooooooooooooooooooooooooooooooo')
                            self._file.vertices.append(
                                list(transform_inverse * self._mesh.vertices[loop.vertex_index].co))
                            self._file.normals.append(list(nrm_mtx * self._normals[loop.vertex_index]))
                            self._file.uvs.append(list(uv))

                            new_vertex_index = len(self._vertex_old_new_index)
                            bone_vertices[self._vertex_count] = dict()
                            uv_dict[uv] = new_vertex_index

                            self._vertex_old_new_index[self._vertex_count] = new_vertex_index
                            loop.vertex_index = new_vertex_index
                            self._vertex_count += 1

        # Calculate the local rotation and position for the bone
        if abone.parent:
            transform = abone.parent.matrix.inverted() * abone.matrix
            bone.rotation = list(transform.transposed().to_quaternion())
            bone.position = list(transform.to_translation())
        else:
            bone.rotation = list(abone.matrix.transposed().to_quaternion())
            bone.position = list(abone.matrix.to_translation())

        bone.vertex_count = len(bone_vertices)
        bone.vertex_index = vertex_index[0]
        vertex_index[0] += bone.vertex_count

        for achild in abone.children:
            child = self._create_bh3_bones(achild, vertex_index)
            child.parent = bone
            bone.children.append(child)

        return bone
