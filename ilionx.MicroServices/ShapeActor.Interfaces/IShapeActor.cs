using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Actors;
using ilionx.MicroServices.Models;

namespace ShapeActor.Interfaces
{
    /// <summary>
    /// This interface defines the methods exposed by an actor.
    /// Clients use this interface to interact with the actor that implements it.
    /// </summary>
    public interface IShapeActor : IActor, IActorEventPublisher<IShapeEvents>
    {
        /// <summary>
        /// Gets the current spahe position.
        /// </summary>
        /// <returns>The shape with the current position.</returns>
        Task<Shape> GetCurrentPositionAsync();
    }
}
