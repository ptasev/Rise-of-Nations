-- Ris of Nations Model Importer
-- By Petar Tasev
-- Version 1.0.2015.0825
(
	fn readStr binstr =
	(
		str = ""
		for w=1 to (readLong binstr #unsigned) do (str += (bit.intasChar (readByte binstr #unsigned)))
		return str
	)
	
	modelFile = getOpenFilename types:"BH3 file (*.BH3)|*.BH3" historyCategory:"Rise of Nations models"
	
	if modelFile != undefined do
	(
		resetMaxFile()
		start = timeStamp()
		max modify mode
		--cui.expertModeOn()
		--disableSceneRedraw()
		
		modelPath = getFilenamePath modelFile
		
		f = fopen modelFile "rb"
		
		local mainObjectName = getFileNameFile modelFile
		local diffMapFileName = (getFileNamePath modelFile) + mainObjectName + ".tga"
		
		local vertices = #()
		local normals = #()
		local texVerts = #()
		local faces = #()
		global dummies = #()
		local weights = #()
		
		fn readChunk binStr parent =
		(
			local dataLength = readLong binStr
			local chunkType = readShort binStr
			local numChildren = readShort binStr
			
			case chunkType of
			(
				2: -- Vertices
				(
					local numElements = readLong binStr
					for vt = 1 to numElements do
					(
						append vertices [(readFloat binStr),(readFloat binStr),(readFloat binStr)]
						readFloat binStr
					)
				)
				3: -- Unknown, Normals??
				(
					local numElements = readLong binStr
					for ni = 1 to numElements do
					(
						append normals [(readFloat binStr),(readFloat binStr),(readFloat binStr)]
					)
					for ni = 1 to numElements do
					(
						readFloat binStr
					)
				)
				4: -- UVs??
				(
					local numElements = readLong binStr
					for tv = 1 to numElements do
					(
						append texVerts [(readFloat binStr),1.0 - (readFloat binStr),0]
					)
				)
				5: -- Faces
				(
					local numElements = (readLong binStr) / 3
					local face
					for fa = 1 to numElements do
					(
						face = [0,0,0]
						face.z = (readShort binStr) + 1
						face.y = (readShort binStr) + 1
						face.x = (readShort binStr) + 1
						append faces face
					)
				)
				6: -- Bone Container
				(
					parent = readChunk binStr parent
					numChildren -= 1;
					while numChildren > 0 do
					(
						readChunk binStr parent
						numChildren -= 1
					)
				)
				7: -- Bone
				(
					local vertIndex = readLong binStr
					local vertCount = readLong binStr
					local boneName = readStr binStr
					local boneRot = inverse(quat (readFloat binStr) (readFloat binStr) (readFloat binStr) (readFloat binStr))
					local bonePos = [(readFloat binStr),(readFloat binStr),(readFloat binStr)]
				    local boneTm = transmatrix bonePos
					boneTm = (boneRot as matrix3) * boneTm
					readFloat binStr
					local boneDum = dummy name:boneName rotation:boneRot position:bonePos boxsize:[1,1,1]
					--local boneDum = BoneSys.createBone boneTm.row4 (boneTm.row4+boneTm.row1) boneTm.row3
					--boneDum.name = boneName
					--boneDum.showlinks = true
					--boneDum.setBoneEnable false 0
					if (not(parent == undefined)) then
					(
						boneDum.parent = parent
						boneDum.transform *= parent.transform
					)
					if vertCount > 0 then
					(
						local vtIndex
						for vt = 1 to vertCount do
						(
							vtIndex = vt + vertIndex
							vertices[vtIndex] = vertices[vtIndex] * boneDum.transform
						)
					)
					append weights [vertCount,vertIndex]
					append dummies boneDum
					boneDum
				)
				default:
				(
					for c = 1 to numChildren do
					(
						readChunk binStr undefined
					)
				)
			)
		)
		
		readChunk f undefined
		
		local mainObject = mesh vertices:vertices faces:faces name:mainObjectName
		meshop.setMapSupport mainObject 1 true
		setMesh mainObject tverts:texVerts
		
		--set smoothing group of all faces to 1 to get one normal per vertex
		for face = 1 to mainObject.numfaces do setFaceSmoothGroup mainObject face 1
		--set normals via edit normals modifier
		select mainObject
		addmodifier mainObject (Edit_Normals ()) ui:off
		mainObject.Edit_Normals.MakeExplicit selection:#{1..normals.count}
		EN_convertVS = mainObject.Edit_Normals.ConvertVertexSelection
		EN_setNormal = mainObject.Edit_Normals.SetNormal
		normID = #{}
		--apply normals
		for v = 1 to normals.count do
		(
			free normID
			EN_convertVS #{v} &normID
			for id in normID do EN_setNormal id normals[v]
		)
		collapseStack mainObject
		
		-- skin the mesh
		select mainObject
		addmodifier mainObject (Skin()) ui:off
		local skinMod = mainObject.Skin
		for bd = 1 to dummies.count do
		(
			skinOps.addBone skinMod dummies[bd] 0
		)
		skinOps.addBone skinMod dummies[dummies.count] 1
		
		--cui.expertModeOff()
		--enableSceneRedraw()
		completeRedraw()
		
		for bd = 1 to dummies.count do
		(
			local boneWeight = weights[bd]
			local boneVertCount = boneWeight[1]
			if boneVertCount> 0 then
			(
				local vtIndex = 0
				for wt = 1 to boneVertCount do
				(
					vtIndex = boneWeight[2] + wt
					skinOps.ReplaceVertexWeights skinMod vtIndex 1 1.0
					skinOps.ReplaceVertexWeights skinMod vtIndex bd 1.0
				)
			)
		)
		
		local mat = Standard name:mainObjectName showInViewport:true
		mat.diffuseMap = BitmapTexture filename:diffMapFileName name:mainObjectName AlphaSource:0 PreMultAlpha:false
		mainObject.material = mat
		
		fclose f
		
		max select none
		max zoomext sel all
		
		end = timeStamp()
		format "Import took % seconds\n" ((end - start) / 1000.0)
	)
)