-- Ris of Nations Animation Importer
-- By Petar Tasev
-- Version 1.0.2015.0826
(
	fn readStr binstr =
	(
		str = ""
		for w=1 to (readLong binstr #unsigned) do (str += (bit.intasChar (readByte binstr #unsigned)))
		return str
	)
	
	modelFile = getOpenFilename types:"BHa file (*.BHa)|*.BHa" historyCategory:"Rise of Nations animations"
	
	if modelFile != undefined do
	(
		start = timeStamp()
		max modify mode
		
		modelPath = getFilenamePath modelFile
		
		f = fopen modelFile "rb"
		
		local boneIndex = 0
		local maxFrame = 0f
		
		fn readChunk binStr =
		(
			local dataLength = readLong binStr
			local chunkType = readShort binStr
			local numChildren = readShort binStr
			
			case chunkType of
			(
				7: -- Bone
				(
					boneIndex += 1
					local boneDum = dummies[boneIndex]
					local numElements = readLong binStr
					
					local currTime = 0f
					local baseTm
					with animate on (at time currTime
					(
						baseTm = boneDum[3].controller.value
					))
					
					for bk = 1 to numElements do
					(
						currTime += (((readFloat binStr) * FrameRate) as time)
						local boneRot = (quat (readFloat binStr) (readFloat binStr) (readFloat binStr) (readFloat binStr))
						local bonePos = [(readFloat binStr),(readFloat binStr),(readFloat binStr)]
						readFloat binStr
						
						local boneTm = transmatrix bonePos
						boneTm = (boneRot as matrix3) * boneTm
						with animate on (at time currTime
						(
							boneDum[3].controller.value = boneTm * baseTm;
						))
					)
					
					maxFrame = amax maxFrame currTime
				)
				default:
				(
					for c = 1 to numChildren do
					(
						readChunk binStr
					)
				)
			)
		)
		
		readChunk f
		
		animationRange = interval 0 maxFrame
		
		fclose f
		
		max select none
		max zoomext sel all
		
		end = timeStamp()
		format "Import took % seconds\n" ((end - start) / 1000.0)
	)
)