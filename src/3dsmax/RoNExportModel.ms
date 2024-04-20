-- Ris of Nations Model Exporter
-- By Petar Tasev
-- Version 1.0.2015.1230
global meshSetVertFunc = meshOp.setVert
global meshGetVertFunc = meshOp.getVert
global meshGetMapVertFunc = meshop.getmapvert
global skinGetBoneNameFunc = skinOps.GetBoneName
global skinGetVertWeightCountFunc = skinOps.GetVertexWeightCount
global skinGetVertWeightFunc = skinops.getvertexweight
global skinGetVertWeightBoneIDFunc = skinOps.GetVertexWeightBoneID
fn getBhBoneByName boneNode boneName =
		(
			if boneNode.name == boneName then
			(
				format "% %\n\n\n\n" boneNode.name boneName
				return boneNode
			)
			
			for child in boneNode.children do
			(
				local bhBoneNode = getBhBoneByName child boneName
				if bhBoneNode != undefined then
				(
					bhBoneNode
				)
			)
			
			undefined
		)
(
	struct BhBone
	(
		node,
		name,
		transform,
		children,
		vertIndexStart,
		vertIndexCount,
		dataLength,
		totDataLength
	)
	
	animFile = getSaveFileName types:"BH3 file (*.BH3)|*.BH3" historyCategory:"Rise of Nations models"
	
	if animFile != undefined and classof $ == Editable_Mesh do
	(
		start = timeStamp()
		f = fopen animFile "wb"
		
		global rootBone
		local mainObject = $
		local brgVertIndices
		local brgVertTVIndices
		local brgFaceArray
		
		function GetMeshSnapshotAtTime mObject tim =
		(
			local mMesh =  at time tim (snapshotAsMesh mObject)
			meshop.DeleteIsoVerts mMesh
			mMesh
		)
		
		function ExportPreservedTexCoordData mMesh =
		(
			brgVertIndices = #()
			brgVertTVIndices = #()
			brgFaceArray = #()
			
			local lastVert = mMesh.numverts
			local lastFace = mMesh.numfaces
			brgVertIndices.count = lastVert
			brgVertTVIndices.count = lastVert
			brgFaceArray.count = lastFace
			
			local VertsUsage = for v = 1 to lastVert collect #{}
			local FaceUsage = for v = 1 to lastVert collect #{}
			local meshGetMapFaceFunc = meshop.getMapFace
			for f = 1 to lastFace do
			(
				theFace = getFace mMesh f
				theMapFace = meshGetMapFaceFunc mMesh 1 f
				append VertsUsage[theFace.x] theMapFace.x
				append VertsUsage[theFace.y] theMapFace.y
				append VertsUsage[theFace.z] theMapFace.z
				append FaceUsage [theFace.x] f
				append FaceUsage [theFace.y] f
				append FaceUsage [theFace.z] f
				brgFaceArray[f] = theFace
			)
			
			local mMeshVertCount = lastVert
			for v = 1 to mMeshVertCount do
			(	
				local theMapVertsArray = VertsUsage[v] as array
				brgVertIndices[v] = v
				brgVertTVIndices[v] = theMapVertsArray[1]
				if theMapVertsArray.count > 1 do
				(
					local theFacesArray = FaceUsage[v] as array
					for mapv = 2 to theMapVertsArray.count do
					(
						local theMapVert = theMapVertsArray[mapv]
						lastVert += 1
						append brgVertIndices v
						append brgVertTVIndices theMapVert
						for f in theFacesArray do
						(
							local theFaceDef = brgFaceArray[f]--getFace mMesh f
							local theMapFaceDef = meshGetMapFaceFunc mMesh 1 f
							local changed = false
							
							if theMapFaceDef.x == theMapVert and theFaceDef.x == v do 
							(
								theFaceDef.x = lastVert
								--brgFaceArray[f] = theFaceDef
								changed = true
							)
							if theMapFaceDef.y == theMapVert and theFaceDef.y == v do 
							(
								theFaceDef.y = lastVert
								--brgFaceArray[f] = theFaceDef
								changed = true
							)
							if theMapFaceDef.z == theMapVert and theFaceDef.z == v do 
							(
								theFaceDef.z = lastVert
								--brgFaceArray[f] = theFaceDef
								changed = true
							)
							
							if changed then
							(
								brgFaceArray[f] = theFaceDef
							)
						)--end f loop
					)--end mapv loop
				)
			)
			
			mMesh
		)
		
		function CalculateAveragedNormals mObject &normals =
		(
			local vertCount = (meshop.getnumverts mObject)
			EN_convertVS = mObject.modifiers[#edit_normals].ConvertVertexSelection
			EN_getNormal = mObject.modifiers[#edit_normals].GetNormal
			for v in 1 to vertCount do
			(
				local vertexNormalIds = #{}
				EN_convertVS #{v} vertexNormalIds
				local averageNormal = [0,0,0]
				for n in vertexNormalIds do averageNormal += EN_getNormal n
				averageNormal = normalize (averageNormal / vertexNormalIds.numberset)
				normals[v] = averageNormal
			)
		)
		
		function ExportSceneNodes scNode &parentBone =
		(
			local scNodeClass = classof scNode
			
			local boneTrack
			if scNodeClass != MAXRootNode then
			(
				boneTrack = BhBone scNode scNode.name undefined #() -1 0 53 0
				boneTrack.transform = (at time animationRange.start (scNode[3].controller.value))
				boneTrack.dataLength += scNode.name.count
				
				
				if classof parentBone == BhBone then
				(
					append parentBone.children boneTrack
				)
				else if classof parentBone == MAXRootNode then
				(
					rootBone = boneTrack
				)
			)
			
			local scNodeChildren = for child in scNode.children where classof child == Dummy or \
			classof child == BoneGeometry or \
			classof child == Biped_Object collect (child)
			
			for child in scNodeChildren do
			(
				if (not child.isHidden) then
				(
					if boneTrack == undefined then
					(
						ExportSceneNodes child scNode
					)
					else
					(
						ExportSceneNodes child &boneTrack
					)
				)
			)
		)
		
		fn calcBoneTrackTotDataLength boneTrack =
		(
			boneTrack.totDataLength += boneTrack.dataLength + 8
			
			for child in boneTrack.children do
			(
				calcBoneTrackTotDataLength child
				boneTrack.totDataLength += child.totDataLength
			)
		)
		
		fn getBhBoneByName boneNode boneName =
		(
			if boneNode.name == boneName then
			(
				--format "% %\n\n\n\n" boneNode.name boneName
				return boneNode
			)
			
			for child in boneNode.children do
			(
				local bhBoneNode = getBhBoneByName child boneName
				if bhBoneNode != undefined then
				(
					return bhBoneNode
				)
			)
			
			return undefined
		)
		
		fn writeBone binStr boneNode =
		(
			writeLong binStr boneNode.totDataLength
			writeShort binStr 6
			writeShort binStr (boneNode.children.count + 1)
			
			writeLong binStr boneNode.dataLength
			writeShort binStr 7
			writeShort binStr 0
			
			writeLong binStr boneNode.vertIndexStart
			writeLong binStr boneNode.vertIndexCount
			
			writeLong binStr (boneNode.name.count + 1)
			writeString binStr boneNode.name
			
			local bRot = boneNode.transform.rotation
			local bPos = boneNode.transform.translation
			
			writeFloat binStr bRot.x
			writeFloat binStr bRot.y
			writeFloat binStr bRot.z
			writeFloat binStr bRot.w
			
			writeFloat binStr bPos.x
			writeFloat binStr bPos.y
			writeFloat binStr bPos.z
			
			writeFloat binStr bRot.x
			
			for child in boneNode.children do
			(
				writeBone binStr child
			)
		)
		
		ExportSceneNodes rootNode undefined
		calcBoneTrackTotDataLength rootBone
		
		local skinMod = mainObject.skin
		local mainMesh = GetMeshSnapshotAtTime mainObject animationRange.Start
		ExportPreservedTexCoordData mainMesh
		
		local vertCount = brgVertIndices.Count
		local tvCount = brgVertTVIndices.count
		local faceCount = brgFaceArray.count
		local meshDataLength = 40 * vertCount + 56 + 6 * faceCount
		
		global normals = #()
		local hasEditNormal = false
		if mainObject.modifiers[#edit_normals] == undefined then
		(
			addmodifier mainObject (Edit_Normals ()) ui:off
		)
		else
		(
			hasEditNormal = true
		)
		modPanel.setCurrentObject mainObject.modifiers[#edit_normals] ui:true
		CalculateAveragedNormals mainObject &normals
		--format "%" normals[1]
		if not(hasEditNormal) then
		(
			deleteModifier mainObject mainObject.modifiers[#edit_normals]
		)
		modPanel.setCurrentObject skinMod ui:true
		
		fn writeVertices binStr =
		(
			writeLong binStr (vertCount * 16 + 12)
			writeShort binStr 2
			writeShort binStr 0
			writeLong binStr vertCount
			
			for vi = 1 to vertCount do
			(
				local vertex = meshGetVertFunc mainMesh brgVertIndices[vi]
				local boneId = skinGetVertWeightBoneIDFunc skinMod vi 1
				local boneName = skinGetBoneNameFunc skinMod boneId 1
				local bhBoneNode = getBhBoneByName rootBone boneName
				
				-- Get the transpose of the inverse bone matrix
			   local nrmMtx = inverse bhBoneNode.node.transform
			   nrmMtx = matrix3 [nrmMtx[1][1],nrmMtx[2][1],nrmMtx[3][1]] [nrmMtx[1][2],nrmMtx[2][2],nrmMtx[3][2]] [nrmMtx[1][3],nrmMtx[2][3],nrmMtx[3][3]] [0,0,0]
				
				vertex = vertex * inverse(bhBoneNode.node.transform)
				normals[vi] = (normalize(normals[vi] * inverse(nrmMtx)))
				if bhBoneNode.vertIndexStart < 0 then
				(
					bhBoneNode.vertIndexStart = vi - 1
				)
				bhBoneNode.vertIndexCount += 1
				
				writeFloat binStr vertex.x
				writeFloat binStr vertex.y
				writeFloat binStr vertex.z
				writeFloat binStr 1.0
			)
		)
		
		fn writeNormals binStr =
		(
			writeLong binStr (vertCount * 16 + 12)
			writeShort binStr 3
			writeShort binStr 0
			writeLong binStr vertCount
			
			for vi = 1 to vertCount do
			(
				local normal = normals[vi]
				writeFloat binStr normal.x
				writeFloat binStr normal.y
				writeFloat binStr normal.z
			)
			
			for vi = 1 to vertCount do
			(
				writeByte binStr 255
				writeByte binStr 255
				writeByte binStr 255
				writeByte binStr 255
			)
		)
		
		fn writeTexCoords binStr =
		(
			writeLong binStr (tvCount * 8 + 12)
			writeShort binStr 4
			writeShort binStr 0
			writeLong binStr tvCount
			
			for tv = 1 to tvCount do
			(
				local tvert = meshGetMapVertFunc mainMesh 1 brgVertTVIndices[tv]
				
				writeFloat binStr tvert.x
				writeFloat binStr (1.0 - tvert.y)
			)
		)
		
		fn writeFaces binStr =
		(
			writeLong binStr (faceCount * 6 + 12)
			writeShort binStr 5
			writeShort binStr 0
			writeLong binStr (faceCount * 3)
			
			for fi = 1 to faceCount do
			(
				local face = brgFaceArray[fi]
				
				writeShort binStr (face.z - 1)
				writeShort binStr (face.y - 1)
				writeShort binStr (face.x - 1)
			)
		)
		
		writeLong f (rootBone.totDataLength + 8 + meshDataLength)
		writeShort f 0
		writeShort f 2
		
		writeLong f meshDataLength
		writeShort f 1
		writeShort f 4
		
		writeVertices f
		writeNormals f
		writeTexCoords f
		writeFaces f
		
		writeBone f rootBone
		
		fclose f
		
		end = timeStamp()
		format "Export took % seconds\n" ((end - start) / 1000.0)
	)
)