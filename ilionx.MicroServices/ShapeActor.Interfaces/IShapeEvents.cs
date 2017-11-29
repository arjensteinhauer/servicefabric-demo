namespace ShapeActor.Interfaces
{
    using ilionx.MicroServices.Models;
    using Microsoft.ServiceFabric.Actors;

    public interface IShapeEvents : IActorEvents
    {
        void ShapeChanged(Shape shape);
    }
}
