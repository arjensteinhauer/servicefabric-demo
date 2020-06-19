namespace ShapeActor
{
    using ilionx.MicroServices.Models;
    using Interfaces;
    using Microsoft.ServiceFabric.Actors;
    using Microsoft.ServiceFabric.Actors.Runtime;
    using System;
    using System.Threading.Tasks;

    /// <remarks>
    /// This class represents an actor.
    /// Every ActorID maps to an instance of this class.
    /// The StatePersistence attribute determines persistence and replication of actor state:
    ///  - Persisted: State is written to disk and replicated.
    ///  - Volatile: State is kept in memory only and replicated.
    ///  - None: State is kept in memory only and not replicated.
    /// </remarks>
    [StatePersistence(StatePersistence.Persisted)]
    internal class ShapeActor : Actor, IShapeActor
    {
        /// <summary>
        /// Timer, used to move the shape to a new position.
        /// </summary>
        private IActorTimer calculateNewPositionTimer;

        /// <summary>
        /// Initializes a new instance of ShapeActor.
        /// </summary>
        /// <param name="actorService">The Microsoft.ServiceFabric.Actors.Runtime.ActorService that will host this actor instance.</param>
        /// <param name="actorId">The Microsoft.ServiceFabric.Actors.ActorId for this actor instance.</param>
        public ShapeActor(ActorService actorService, ActorId actorId)
            : base(actorService, actorId)
        {
        }

        /// <summary>
        /// Gets the current spahe position.
        /// </summary>
        /// <returns>The shape with the current position.</returns>
        public async Task<Shape> GetCurrentPositionAsync()
        {
            var result = await StateManager.TryGetStateAsync<Shape>("shape");
            return result.Value;
        }

        /// <summary>
        /// This method is called whenever an actor is activated.
        /// An actor is activated the first time any of its methods are invoked.
        /// </summary>
        /// <returns>Async task.</returns>
        protected override Task OnActivateAsync()
        {
            // logging
            ActorEventSource.Current.ActorMessage(this, "Actor activated.");

            // this is the first time the actor is activated --> initiate the actor state
            this.StateManager.TryAddStateAsync("shape", CreateNewShape());

            // start a timer job for calculating new positions
            calculateNewPositionTimer = RegisterTimer(
                CalculateNewPosition,            // Callback method
                null,                            // Parameter to pass to the callback method
                TimeSpan.FromMilliseconds(10),   // Amount of time to delay before the callback is invoked
                TimeSpan.FromMilliseconds(10));  // Time interval between invocations of the callback method

            // base
            return base.OnActivateAsync();
        }

        /// <summary>
        /// Called when the actor is deactivated.
        /// </summary>
        /// <returns>Async task.</returns>
        protected override Task OnDeactivateAsync()
        {
            // stop the timer job
            if (calculateNewPositionTimer != null)
            {
                UnregisterTimer(calculateNewPositionTimer);
            }

            // base
            return base.OnDeactivateAsync();
        }

        /// <summary>
        /// Create a new shape with a random position.
        /// </summary>
        /// <returns>The created shape.</returns>
        private Shape CreateNewShape()
        {
            var randomizer = new Random();
            var diff = new int[2] { -1, 1 };

            return new Shape()
            {
                X = randomizer.Next(10, 900),
                Y = randomizer.Next(10, 600),
                Angle = 0,
                DiffX = diff[randomizer.Next(0, 2)],
                DiffY = diff[randomizer.Next(0, 2)]
            };
        }

        /// <summary>
        /// Timer callback for calculating the new position.
        /// </summary>
        /// <param name="state">Timer job state object.</param>
        /// <returns>Async task.</returns>
        private async Task CalculateNewPosition(object state)
        {
            // get the current state
            var result = await StateManager.TryGetStateAsync<Shape>("shape");
            if (result.HasValue)
            {
                // calculate the new position
                var shape = result.Value;

                if (shape.X > 900) shape.DiffX = -1;
                if (shape.X < 10) shape.DiffX = 1;
                if (shape.Y > 600) shape.DiffY = -1;
                if (shape.Y < 10) shape.DiffY = 1;

                shape.X += shape.DiffX;
                shape.Y += shape.DiffY;

                #region DEMO
                // demo: new implementation - let's also rotate the shape
                //shape.Angle++;
                #endregion DEMO

                // save the new position in the state
                await this.StateManager.SetStateAsync("shape", shape);

                // notify any subscribers the position has changed
                var @event = GetEvent<IShapeEvents>();
                @event.ShapeChanged(shape);
            }
        }
    }
}
