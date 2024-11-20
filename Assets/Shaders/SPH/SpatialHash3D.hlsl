static const int3 offsets3D[27] =
{
	int3(-1, -1, -1),
	int3(-1, -1, 0),
	int3(-1, -1, 1),
	int3(-1, 0, -1),
	int3(-1, 0, 0),
	int3(-1, 0, 1),
	int3(-1, 1, -1),
	int3(-1, 1, 0),
	int3(-1, 1, 1),
	int3(0, -1, -1),
	int3(0, -1, 0),
	int3(0, -1, 1),
	int3(0, 0, -1),
	int3(0, 0, 0),
	int3(0, 0, 1),
	int3(0, 1, -1),
	int3(0, 1, 0),
	int3(0, 1, 1),
	int3(1, -1, -1),
	int3(1, -1, 0),
	int3(1, -1, 1),
	int3(1, 0, -1),
	int3(1, 0, 0),
	int3(1, 0, 1),
	int3(1, 1, -1),
	int3(1, 1, 0),
	int3(1, 1, 1)
};

// Constants used for hashing
static const uint hashK1 = 15823;
static const uint hashK2 = 9737333;
static const uint hashK3 = 440817757;

//for Sebastians
// Convert floating point position into an integer cell coordinate
int3 GetCell3D(float3 position, float radius)
{
	return (int3)floor(position / radius);
}

//for Default
uint3 GetCell2D(float3 position, float3 boxSize, float radius)
{
    float3 halfS = boxSize / 2;
    return uint3((position.x + halfS.x) / radius, (position.y + halfS.y) / radius, (position.z + halfS.z) / radius);

}

// Hash cell coordinate to a single unsigned integer
uint HashCell3D(int3 cellIndex, int particleLength, bool useSebastianHashing)
{
    if(useSebastianHashing)
    {
		//Sebastian's HashCel3dLogic
        const uint p1 = 73856093;
        const uint p2 = 19349663;
        const uint p3 = 83492791;
        return (p1 * cellIndex.x + p2 * cellIndex.y + p3 * cellIndex.z) % particleLength;
   
    }
    else
    {
		//Default HashCell logic
		return (cellIndex.x * 73856093 + cellIndex.y * 19349663 + cellIndex.z * 83492791) % particleLength;
    }
      
}

uint KeyFromHash(uint hash, uint tableSize)
{
	return hash % tableSize;
}
