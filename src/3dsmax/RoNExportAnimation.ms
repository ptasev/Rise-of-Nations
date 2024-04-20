-- Ris of Nations Animation Exporter
-- By Petar Tasev
-- Version 1.0.2015.0906
(
	struct BhBoneTrack
	(
		keys, \
		transforms, \
		children, \
		dataLength, \
		totDataLength \
	)
	
	animFile = getSaveFileName types:"BHa file (*.BHa)|*.BHa" historyCategory:"Rise of Nations animations"
	
	if animFile != undefined do
	(
		start = timeStamp()
		f = fopen animFile "wb"
		
		local rootBoneTrack
		
		function ConvertAnimKeys frm =
		(
			if frm < animationRange.start or frm > animationRange.end then
			(
				-1.0
			)
			else
			(
				(frm - animationRange.start) as integer / 4800.0
			)
		)

		function GetBoneAnimKeys boneNode =
		(
			local keys = #()
			
			if (boneNode[#transform] != undefined) then
			(
				numBoneKeys = boneNode[#transform].controller.keys.count
				for j = 1 to numBoneKeys do
				(
					appendIfUnique keys (ConvertAnimKeys boneNode[#transform].controller.keys[j].time)
				)
				
				if (boneNode[#transform][#position] != undefined) then
				(
					numBoneKeys = boneNode[#transform][#position].keys.count
					for j = 1 to numBoneKeys do
					(
						appendIfUnique keys (ConvertAnimKeys boneNode[#transform][#position].keys[j].time)
					)
				)
				
				if (boneNode[#transform][#rotation] != undefined) then
				(
					numBoneKeys = boneNode[#transform][#rotation].keys.count
					local rotKeys
					if (numBoneKeys == -1) then
					( 
						numBoneKeys = boneNode[#transform][#rotation][1].keys.count
						rotKeys = boneNode[#transform][#rotation][1].keys
					)
					else rotKeys = boneNode[#transform][#rotation].keys
					for j = 1 to numBoneKeys do
					(
						appendIfUnique keys (ConvertAnimKeys rotKeys[j].time)
					)
				)
				
				if (boneNode[#transform][#scale] != undefined) then
				(
					numBoneKeys = boneNode[#transform][#scale].keys.count
					for j = 1 to numBoneKeys do
					(
						appendIfUnique keys (ConvertAnimKeys boneNode[#transform][#scale].keys[j].time)
					)
				)
				
				if (boneNode[#transform][#vertical] != undefined) then
				(
					numBoneKeys = boneNode[#transform][#vertical].keys.count
					for j = 1 to numBoneKeys do
					(
						appendIfUnique keys (ConvertAnimKeys boneNode[#transform][#vertical].keys[j].time)
					)
				)
				
				if (boneNode[#transform][#horizontal] != undefined) then
				(
					numBoneKeys = boneNode[#transform][#horizontal].keys.count
					for j = 1 to numBoneKeys do
					(
						appendIfUnique keys (ConvertAnimKeys boneNode[#transform][#horizontal].keys[j].time)
					)
				)
				
				if (boneNode[#transform][#turning] != undefined) then
				(
					numBoneKeys = boneNode[#transform][#turning].keys.count
					for j = 1 to numBoneKeys do
					(
						appendIfUnique keys (ConvertAnimKeys boneNode[#transform][#turning].keys[j].time)
					)
				)
			)
			
			if (classof boneNode == Editable_Mesh) then
			(
				numAnimKeys = boneNode.baseobject.mesh[1].keys.count
				for i = 1 to numAnimKeys do
				(
					appendIfUnique keys (ConvertAnimKeys boneNode.baseobject.mesh[1].keys[i].time)
				)
			)
			
			if keys.count <= 0 then
			(
				appendIfUnique keys (ConvertAnimKeys animationRange.start)
			)
			
			sort keys
			
			if keys[1] < 0.0 then
			(
				deleteItem keys 1
			)
			
			keys
		)

		function ExportSceneNodes scNode &parentBoneTrack =
		(
			local scNodeClass = classof scNode
			
			local boneTrack
			if scNodeClass != MAXRootNode then
			(
				boneTrack = BhBoneTrack #() #() #() 12 0
				local keys = GetBoneAnimKeys scNode
				local keyCount = keys.count
				local baseKeyTime = ((keys[1] * frameRate) as time)
				local baseTM = inverse(at time baseKeyTime (scNode[3].controller.value))
				
				for ti = 2 to keyCount do
				(
					local currTime = ((keys[ti] * frameRate) as time)
					append boneTrack.keys ((currTime - baseKeyTime) as integer / 4800.0)
					append boneTrack.transforms (at time currTime (scNode[3].controller.value * baseTM))
						
					baseKeyTime = currTime
				)
				
				if keyCount > 0 do
				(
					boneTrack.dataLength += (keyCount - 1) * 36
				)
				
				if classof parentBoneTrack == BhBoneTrack then
				(
					append parentBoneTrack.children boneTrack
				)
				else if classof parentBoneTrack == MAXRootNode then
				(
					rootBoneTrack = boneTrack
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
		
		ExportSceneNodes rootNode undefined
		calcBoneTrackTotDataLength rootBoneTrack
		
		writeLong f (rootBoneTrack.totDataLength + 8)
		writeShort f 0
		writeShort f 1
		
		fn writeBoneTrack binStr boneTrack =
		(
			local keyCount = boneTrack.keys.count
			
			writeLong binStr boneTrack.totDataLength
			writeShort binStr 8
			writeShort binStr (boneTrack.children.count + 1)
			
			writeLong binStr boneTrack.dataLength
			writeShort binStr 7
			writeShort binStr 0
			writeLong binStr keyCount
			
			for ti = 1 to keyCount do
			(
				local bRot = boneTrack.transforms[ti].rotation
				local bPos = boneTrack.transforms[ti].translation
				
				writeFloat binStr boneTrack.keys[ti]
				
				writeFloat binStr bRot.x
				writeFloat binStr bRot.y
				writeFloat binStr bRot.z
				writeFloat binStr bRot.w
				
				writeFloat binStr bPos.x
				writeFloat binStr bPos.y
				writeFloat binStr bPos.z
				
				writeFloat binStr bRot.x
			)
			
			for child in boneTrack.children do
			(
				writeBoneTrack binStr child
			)
		)
		
		writeBoneTrack f rootBoneTrack
		
		fclose f
		
		end = timeStamp()
		format "Export took % seconds\n" ((end - start) / 1000.0)
	)
)