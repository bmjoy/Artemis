using System.Collections.Generic;
using System.Threading;
using Artemis.Exceptions;
using Artemis.Extensions;
using Artemis.Packets;
using Artemis.ValueObjects;
using UnityEngine;

namespace Artemis.Clients
{
    public class ReliableClient : ObjectClient
    {
        private readonly Thread _resendReliablePacketsThread;
        private readonly List<PendingAckMessage> _pendingAckPackets = new();
        private readonly PacketSequenceStorage _outgoingSequenceStorage = new();
        private readonly PacketSequenceStorage _incomingSequenceStorage = new();

        protected ReliableClient(int port = 0) : base(port)
        {
            _resendReliablePacketsThread = new Thread(ResendPendingAckPackets);
        }

        public override void Start()
        {
            base.Start();
            _resendReliablePacketsThread.Start();
        }

        public override void Dispose()
        {
            _resendReliablePacketsThread.Abort();
            base.Dispose();
        }

        public void SendMessage<T>(T obj, Address recepient, DeliveryMethod deliveryMethod)
        {
            var message = new Message(
                _outgoingSequenceStorage.Get(recepient, deliveryMethod, 0) + 1,
                obj, deliveryMethod);

            _outgoingSequenceStorage.Set(recepient, deliveryMethod, message.Sequence);

            if (deliveryMethod == DeliveryMethod.Reliable)
            {
                lock (_pendingAckPackets)
                {
                    _pendingAckPackets.Add(new PendingAckMessage(message, recepient));
                }
            }

            SendObject(message, recepient);
        }

        private void ResendPendingAckPackets()
        {
            while (true)
            {
                Thread.Sleep(64);

                lock (_pendingAckPackets)
                {
                    foreach (var pam in _pendingAckPackets)
                    {
                        SendObject(pam.Message, pam.Recepient);
                    }
                }
            }
        }

        protected virtual void HandleMessage(Message message, Address sender)
        {
            Debug.Log($"Received message containing {message.Payload.GetType().FullName} from {sender}");
        }

        private void HandlePacket(Message message, Address sender)
        {
            var expectedSequence = _incomingSequenceStorage.Get(sender, message.DeliveryMethod, 0) + 1;

            if (message.Sequence != expectedSequence)
            {
                Debug.LogWarning($"Discarding reliable packet #{message.Sequence} with {message.Payload.GetType().Name} as expected sequence is #{expectedSequence}");
                return; // Discard duplicate or out or order
            }

            if (message.DeliveryMethod == DeliveryMethod.Reliable)
            {
                SendObject(new Ack {Sequence = message.Sequence}, sender);
            }

            Debug.Log($"Received packet #{message.Sequence}");
            _incomingSequenceStorage.Set(sender, message.DeliveryMethod, message.Sequence);
            HandleMessage(message, sender);
        }

        private void HandleAcknowledgement(Ack ack, Address sender)
        {
            lock (_pendingAckPackets)
            {
                _pendingAckPackets.Remove(pam => pam.Message.Sequence == ack.Sequence && pam.Recepient == sender);
            }
        }

        protected override void HandleObject(object obj, Address sender)
        {
            base.HandleObject(obj, sender);

            switch (obj)
            {
                case Message message:
                    HandlePacket(message, sender);
                    break;
                case Ack acknowledgement:
                    HandleAcknowledgement(acknowledgement, sender);
                    break;
                default: throw new ObjectTypeUnhandledException(obj);
            }
        }
    }
}