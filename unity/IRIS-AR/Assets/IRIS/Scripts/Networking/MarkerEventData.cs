using System;

namespace IRIS.Networking
{
    [Serializable]
    public class MarkerCreatePayload
    {
        public double lat { get; set; }
        public double lng { get; set; }
        public string label { get; set; }
        public string type { get; set; }
    }

    [Serializable]
    public class MarkerPlacePayload
    {
        public string id { get; set; }
        public PositionPayload position { get; set; }
    }

    [Serializable]
    public class PositionPayload
    {
        public float x { get; set; }
        public float y { get; set; }
        public float z { get; set; }

        public PositionPayload(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }
    }

    [Serializable]
    public class MarkerDeletePayload
    {
        public string id { get; set; }
    }

    [Serializable]
    public class DeviceRegisterPayload
    {
        public string name { get; set; }
        public string type { get; set; }
    }

    [Serializable]
    public class DeviceRegisteredPayload
    {
        public string id { get; set; }
        public string name { get; set; }
        public string type { get; set; }
    }

    [Serializable]
    public class DeviceHeartbeatPayload
    {
        public string id { get; set; }
    }
}
