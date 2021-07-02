using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Rebus.Activation;
using Rebus.Bus;
using Rebus.Bus.Advanced;
using Rebus.Extensions;
using Rebus.Handlers;
using Rebus.Pipeline;
using Rebus.Transport;
using Unity;
using Unity.Lifetime;
#pragma warning disable 1998

namespace Rebus.Unity
{
    /// <summary>
    /// Implementation of <see cref="IContainerAdapter"/> that uses Unity to do its thing
    /// </summary>
    public class UnityContainerAdapter : IContainerAdapter
    {
        readonly IUnityContainer _unityContainer;

        /// <summary>
        /// Constructs the container adapter
        /// </summary>
        public UnityContainerAdapter(IUnityContainer unityContainer)
        {
            if (unityContainer == null) throw new ArgumentNullException(nameof(unityContainer));
            _unityContainer = unityContainer;
        }

        /// <summary>
        /// Resolves all handlers for the given <typeparamref name="TMessage"/> message type
        /// </summary>
        public async Task<IEnumerable<IHandleMessages<TMessage>>> GetHandlers<TMessage>(TMessage message, ITransactionContext transactionContext)
        {
            var resolvedHandlerInstances = ResolvePoly<TMessage>();
            
            transactionContext.OnDisposed(_ =>
            {
                foreach (var disposableInstance in resolvedHandlerInstances.OfType<IDisposable>())
                {
                    disposableInstance.Dispose();
                }
            });
            
            return resolvedHandlerInstances;
        }

        List<IHandleMessages<TMessage>> ResolvePoly<TMessage>()
        {
            var handledMessageTypes = typeof(TMessage).GetBaseTypes()
                .Concat(new[] { typeof(TMessage) });

            return handledMessageTypes
                .SelectMany(handledMessageType =>
                {
                    var implementedInterface = typeof (IHandleMessages<>).MakeGenericType(handledMessageType);

                    return _unityContainer.ResolveAll(implementedInterface).Cast<IHandleMessages>();
                })
                .Cast<IHandleMessages<TMessage>>()
                .ToList();
        }

        /// <summary>
        /// Stores the bus instance
        /// </summary>
        public void SetBus(IBus bus)
        {
            if (_unityContainer.IsRegistered<IBus>())
            {
                throw new InvalidOperationException("Cannot register IBus because one has already been registered. If you want to host multiple Rebus endpoints in a single process, please use separate container instances for them.");
            }

            _unityContainer.RegisterInstance(bus, new ContainerControlledLifetimeManager());

            _unityContainer.RegisterFactory<ISyncBus>(c => c.Resolve<IBus>().Advanced.SyncBus);

            _unityContainer.RegisterFactory<IMessageContext>(c =>
            {
                var currentMessageContext = MessageContext.Current;
                if (currentMessageContext == null)
                {
                    throw new InvalidOperationException("Attempted to inject the current message context from MessageContext.Current, but it was null! Did you attempt to resolve IMessageContext from outside of a Rebus message handler?");
                }
                return currentMessageContext;
            });
        }
    }
}
