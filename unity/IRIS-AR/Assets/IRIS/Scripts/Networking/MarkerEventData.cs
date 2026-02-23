using System;

namespace IRIS.Networking
{
    [Serializable]
    public class MarkerCreatePayload
    {
        public string label;
        public string type;
    }

    [Serializable]
    public class MarkerPlacePayload
    {
        public string id;
        public PositionPayload position;
    }

    [Serializable]
    public class PositionPayload
    {
        public float x;
        public float y;
        public float z;

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
        public string id;
    }

    [Serializable]
    public class DeviceRegisterPayload
    {
        public string name;
        public string type;
    }

    [Serializable]
    public class DeviceRegisteredPayload
    {
        public string id;
        public string name;
        public string type;
    }

    [Serializable]
    public class DeviceHeartbeatPayload
    {
        public string id;
    }
}
