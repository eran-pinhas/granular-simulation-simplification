public interface ICollisionListener
{
    void informCollision(Particle a, Particle b);

    void informCollisionRemoved(Particle a, Particle b);
}
