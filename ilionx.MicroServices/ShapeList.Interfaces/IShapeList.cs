namespace ShapeList.Interfaces
{
    using Microsoft.ServiceFabric.Services.Remoting;
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    /// <summary>
    /// Describes the shape list service.
    /// </summary>
    public interface IShapeList : IService
    {
        /// <summary>
        /// Gets the shape list for the provided client ID.
        /// </summary>
        /// <param name="clientId">The client ID to get the shape list for.</param>
        /// <returns>The shapes.</returns>
        Task<List<Guid>> GetShapes(Guid clientId);

        /// <summary>
        /// Adds a new shape to the shape list.
        /// </summary>
        /// <param name="clientId">The client ID to add the shape list for.</param>
        /// <param name="shapeId">The shape ID to add.</param>
        /// <returns>Async task.</returns>
        Task AddShape(Guid clientId, Guid shapeId);

        /// <summary>
        /// Removes an existing shape from the shape list.
        /// </summary>
        /// <param name="shapeId">The shape ID to remove.</param>
        /// <returns>Async task.</returns>
        Task RemoveShape(Guid shapeId);
    }
}
