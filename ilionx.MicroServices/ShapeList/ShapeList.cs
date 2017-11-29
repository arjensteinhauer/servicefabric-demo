namespace ShapeList
{
    using global::ShapeList.Interfaces;
    using Microsoft.ServiceFabric.Data.Collections;
    using Microsoft.ServiceFabric.Services.Communication.Runtime;
    using Microsoft.ServiceFabric.Services.Remoting.Runtime;
    using Microsoft.ServiceFabric.Services.Runtime;
    using System;
    using System.Collections.Generic;
    using System.Fabric;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// An instance of this class is created for each service replica by the Service Fabric runtime.
    /// </summary>
    internal sealed class ShapeList : StatefulService, IShapeList
    {
        public ShapeList(StatefulServiceContext context)
            : base(context)
        { }

        /// <summary>
        /// Optional override to create listeners (e.g., HTTP, Service Remoting, WCF, etc.) for this service replica to handle client or user requests.
        /// </summary>
        /// <remarks>
        /// For more information on service communication, see https://aka.ms/servicefabricservicecommunication
        /// </remarks>
        /// <returns>A collection of listeners.</returns>
        protected override IEnumerable<ServiceReplicaListener> CreateServiceReplicaListeners()
        {
            return new[]
            {
                new ServiceReplicaListener(context => this.CreateServiceRemotingListener(context))
            };
        }

        /// <summary>
        /// Gets the shapes.
        /// </summary>
        /// <param name="clientId">The client ID to get the shape list for.</param>
        /// <returns>The shapes.</returns>
        public async Task<List<Guid>> GetShapes(Guid clientId)
        {
            var shapesForClient = new List<Guid>();
            var shapeList = await this.StateManager.GetOrAddAsync<IReliableDictionary<Guid, Guid>>("shapeList");

            using (var transaction = this.StateManager.CreateTransaction())
            {
                var enumerator = (await shapeList.CreateEnumerableAsync(transaction, EnumerationMode.Unordered)).GetAsyncEnumerator();
                while (await enumerator.MoveNextAsync(CancellationToken.None))
                {
                    if (enumerator.Current.Value == clientId)
                    {
                        shapesForClient.Add(enumerator.Current.Key);
                    }
                }
            }

            return shapesForClient;
        }

        /// <summary>
        /// Adds a new shape to the shape list.
        /// </summary>
        /// <param name="clientId">The client ID to add the shape list for.</param>
        /// <param name="shapeId">The shape ID to add.</param>
        /// <returns>Async task.</returns>
        public async Task AddShape(Guid clientId, Guid shapeId)
        {
            var shapeList = await this.StateManager.GetOrAddAsync<IReliableDictionary<Guid, Guid>>("shapeList");

            using (var transaction = this.StateManager.CreateTransaction())
            {
                await shapeList.AddOrUpdateAsync(transaction, shapeId, clientId, (key, value) => clientId);

                // If an exception is thrown before calling CommitAsync, the transaction aborts, all changes are 
                // discarded, and nothing is saved to the secondary replicas.
                await transaction.CommitAsync();
            }
        }

        /// <summary>
        /// Removes an existing shape from the shape list.
        /// </summary>
        /// <param name="shapeId">The shape ID to remove.</param>
        /// <returns>Async task.</returns>
        public async Task RemoveShape(Guid shapeId)
        {
            var shapeList = await this.StateManager.GetOrAddAsync<IReliableDictionary<Guid, Guid>>("shapeList");

            using (var transaction = this.StateManager.CreateTransaction())
            {
                if (await shapeList.ContainsKeyAsync(transaction, shapeId))
                {
                    await shapeList.TryRemoveAsync(transaction, shapeId);
                }

                // If an exception is thrown before calling CommitAsync, the transaction aborts, all changes are 
                // discarded, and nothing is saved to the secondary replicas.
                await transaction.CommitAsync();
            }
        }
    }
}
