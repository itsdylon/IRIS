using System;
using System.Collections.Generic;

namespace IRIS.Networking
{
    [Serializable]
    public class PosePayload
    {
        public float px { get; set; }
        public float py { get; set; }
        public float pz { get; set; }
        public float rx { get; set; }
        public float ry { get; set; }
        public float rz { get; set; }
        public float rw { get; set; }
    }

    [Serializable]
    public class AnchorSharePayload
    {
        public string sessionId { get; set; }
        public string anchorId { get; set; }
        public string groupUuid { get; set; }
        public PosePayload pose { get; set; }
        public double calibrationLat { get; set; }
        public double calibrationLng { get; set; }
        public double calibrationAlt { get; set; }
    }

    [Serializable]
    public class AnchorLoadPayload
    {
        public string groupUuid { get; set; }
    }

    [Serializable]
    public class AnchorErasePayload
    {
        public string anchorId { get; set; }
    }

    [Serializable]
    public class AnchorSharedPayload
    {
        public string anchorId { get; set; }
        public string groupUuid { get; set; }
        public PosePayload pose { get; set; }
        public double calibrationLat { get; set; }
        public double calibrationLng { get; set; }
        public double calibrationAlt { get; set; }
        public string sharedBy { get; set; }
        public string sharedAt { get; set; }
    }

    [Serializable]
    public class AnchorLoadResponsePayload
    {
        public List<AnchorSharedPayload> anchors { get; set; }
    }

    [Serializable]
    public class SessionCreatePayload { }

    [Serializable]
    public class SessionCreatedPayload
    {
        public string sessionId { get; set; }
        public string hostDeviceId { get; set; }
    }

    [Serializable]
    public class SessionJoinPayload
    {
        public string sessionId { get; set; }
    }

    [Serializable]
    public class SessionStatePayload
    {
        public string sessionId { get; set; }
        public string hostDeviceId { get; set; }
        public List<string> devices { get; set; }
        public AnchorSharedPayload calibration { get; set; }
        public List<AnchorSharedPayload> anchors { get; set; }
    }

    [Serializable]
    public class SessionJoinedPayload
    {
        public string sessionId { get; set; }
        public string deviceId { get; set; }
    }
}
