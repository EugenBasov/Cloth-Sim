using UnityEngine;

public class ClothParticle
{
    public int vertexIdx;
    public Vector3 position;
    public ClothParticle(Vector3 vertexPos, int vertexIdx)
    {
        position = vertexPos;
        this.vertexIdx = vertexIdx;
    }   
}
