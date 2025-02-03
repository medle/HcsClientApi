
using System;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using System.ServiceModel.Dispatcher;

namespace Hcs.ClientApi.RemoteCaller
{
    public class GostSigningEndpointBehavior : IEndpointBehavior
    {
        private HcsClientConfig clientConfig;

        public GostSigningEndpointBehavior(HcsClientConfig clientConfig)
        {
            this.clientConfig = clientConfig;
        }

        public void Validate(ServiceEndpoint endpoint)
        {
        }

        public void AddBindingParameters(ServiceEndpoint endpoint, BindingParameterCollection bindingParameters)
        {
        }

        public void ApplyDispatchBehavior(ServiceEndpoint endpoint, EndpointDispatcher endpointDispatcher)
        {
        }

        public void ApplyClientBehavior(ServiceEndpoint endpoint, ClientRuntime clientRuntime)
        {
            clientRuntime.MessageInspectors.Add(
                new GostSigningMessageInspector(clientConfig));
        }
    }
}
