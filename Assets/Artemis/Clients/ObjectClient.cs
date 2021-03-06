using Artemis.ValueObjects;
using Artemis.Serialization;

namespace Artemis.Clients
{
    public class ObjectClient : ByteClient
    {
        private static readonly ISerializer _serializer = new BinarySerializer();

        protected ObjectClient(int port = 0) : base(port)
        {
        }

        protected void SendObject<T>(T obj, Address recipient)
        {
            var bytes = _serializer.Serialize(obj);
            SendBytes(bytes, recipient);
        }

        protected virtual void HandleObject(object obj, Address sender)
        {
            //UnityEngine.Debug.Log($"{nameof(ObjectClient)} received {obj.GetType().Name} bytes from {sender}");
        }

        protected override void HandleBytes(byte[] bytes, Address sender)
        {
            base.HandleBytes(bytes, sender);
            var obj = _serializer.Deserialize(bytes);
            HandleObject(obj, sender);
        }
    }
}