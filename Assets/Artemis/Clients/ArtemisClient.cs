using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Artemis.Packets;
using Artemis.Settings;
using Artemis.UserInterface;
using Artemis.Utilities;
using Artemis.ValueObjects;
using UnityEngine;

namespace Artemis.Clients
{
    public class ArtemisClient : ReliableClient
    {
        private readonly Dictionary<string, TaskCompletionSource<object>> _responses = new();
        private readonly Dictionary<Type, Action<Message, Address>> _messageHandlers = new();
        private readonly Dictionary<Type, Action<Request, Address>> _requestHandlers = new();

        public ArtemisClient(IEnumerable<Handler> handlers, int port = 0) : base(port)
        {
            foreach (var handler in handlers)
            {
                handler.Bind(this);
            }
        }
        
        public void RegisterHandler<T>(Action<T> handler)
        {
            throw new NotImplementedException();
            
            if (typeof(T).GetGenericTypeDefinition() == typeof(Message<>))
            {
                
            }

            if (typeof(T).GetGenericTypeDefinition() == typeof(Request<>))
            {
                
            }

            throw new Exception($"{typeof(T).FullName} cannot be handled.");
        }

        public void RegisterMessageHandler<T>(Action<Message<T>> handler)
        {
            _messageHandlers.Add(typeof(T), (message, sender) =>
            {
                handler.Invoke(new Message<T>((T) message.Payload, sender));
            });
        }

        public void RegisterRequestHandler<T>(Action<Request<T>> handler)
        {
            _requestHandlers.Add(typeof(T), (request, sender) =>
            {
                handler.Invoke(new Request<T>(request.Id, (T) request.Payload, sender, this));
            });
        }

        protected override void HandleMessage(Message message, Address sender)
        {
            switch (message.Payload)
            {
                case Request request:
                    HandleRequest(request, sender);
                    break;
                case Response response:
                    HandleResponse(response, sender);
                    break;
                default:
                    HandleUserMessage(message, sender);
                    break;
            }
        }
        
        private void HandleUserMessage(Message message, Address sender)
        {
            if (_messageHandlers.TryGetValue(message.Payload.GetType(), out var handler))
            {
                handler.Invoke(message, sender);
            }
            else
            {
                Debug.LogError($"Message handler not found for type '{message.Payload.GetType().FullName}'.");
            }
        }

        protected virtual void HandleRequest(Request request, Address sender)
        {
            if (_requestHandlers.TryGetValue(request.Payload.GetType(), out var handler))
            {
                handler.Invoke(request, sender);
            }
            else
            {
                Debug.LogError($"Request handler not found for type '{request.Payload.GetType().FullName}'.");
            }
        }

        protected virtual void HandleResponse(Response response, Address sender)
        {
            //Debug.Log($"Received a response of type {response.Payload.GetType().FullName} from {sender}");
            _responses[response.Id].TrySetResult(response);
        }

        public Task<object> RequestAsync<T>(T obj, Address recepient, CancellationToken ct = default)
        {
            return RequestAsync(obj, recepient, Configuration.RequestTimeout, ct);
        }
        
        public Task<object> RequestAsync<T>(T obj, Address recepient, TimeSpan timeout, CancellationToken ct = default)
        {
            var request = new Request(obj);
            SendMessage(request, recepient, DeliveryMethod.Reliable);
            var tcs = new TaskCompletionSource<object>();
            _responses.Add(request.Id, tcs);
            return tcs.Task.TimeoutAfter(timeout, ct);
        }
    }
}