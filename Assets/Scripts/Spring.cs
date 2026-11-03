public class Spring
{
    public ClothParticle p1, p2;
    public float restLength;
    public Spring(ClothParticle p1, ClothParticle p2)
    {
        this.p1 = p1;
        this.p2 = p2;
        this.restLength = (p1.position - p2.position).magnitude;
    }
}
