using System;
using System.Collections.Generic;
using System.Linq;
using Rebus.Activation;
using Rebus.Bus;
using Rebus.Config;
using Rebus.Handlers;
using Rebus.Tests.Contracts.Activation;
using Unity;
using Unity.Injection;

namespace Rebus.Unity.Tests
{
    public class UnityContainerAdapterFactory : IActivationContext
    {
        public IHandlerActivator CreateActivator(Action<IHandlerRegistry> configureHandlers, out IActivatedContainer container)
        {
            var unityContainer = new UnityContainer();

            configureHandlers.Invoke(new HandlerRegistry(unityContainer));

            container = new ActivatedContainer(unityContainer);

            return new UnityContainerAdapter(unityContainer);
        }

        public IBus CreateBus(Action<IHandlerRegistry> configureHandlers, Func<RebusConfigurer, RebusConfigurer> configureBus, out IActivatedContainer container)
        {
            var unityContainer = new UnityContainer();

            configureHandlers.Invoke(new HandlerRegistry(unityContainer));

            container = new ActivatedContainer(unityContainer);

            return configureBus(Configure.With(new UnityContainerAdapter(unityContainer))).Start();
        }

        class HandlerRegistry : IHandlerRegistry
        {
            readonly UnityContainer _unityContainer;

            public HandlerRegistry(UnityContainer unityContainer)
            {
                _unityContainer = unityContainer;
            }

            public IHandlerRegistry Register<THandler>() where THandler : class, IHandleMessages
            {
                foreach (var handlerInterfaceType in GetHandlerInterfaces<THandler>())
                {
                    var componentName = $"{typeof(THandler).FullName}:{handlerInterfaceType.FullName}";

                    _unityContainer.RegisterType(handlerInterfaceType, typeof(THandler), componentName, new InjectionMember[0]);
                }
                return this;
            }

            static IEnumerable<Type> GetHandlerInterfaces<THandler>() where THandler : class, IHandleMessages
            {
                return typeof(THandler).GetInterfaces().Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IHandleMessages<>));
            }
        }

        class ActivatedContainer : IActivatedContainer
        {
            readonly UnityContainer _unityContainer;

            public ActivatedContainer(UnityContainer unityContainer)
            {
                _unityContainer = unityContainer;
            }

            public IBus ResolveBus()
            {
                return _unityContainer.Resolve<IBus>();
            }

            public void Dispose()
            {
                _unityContainer.Dispose();
            }
        }
    }
}
