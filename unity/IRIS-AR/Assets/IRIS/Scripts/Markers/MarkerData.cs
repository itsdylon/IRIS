using System;
using UnityEngine;

namespace IRIS.Markers
{
    [Serializable]
    public class MarkerData
    {
        public string id;
        public string label;
        public string type;
        public string status;
        public MarkerPosition position;
        public string createdAt;
        public string placedAt;

        public MarkerData(string id, string label, string type)
        {
            this.id = id;
            this.label = label;
            this.type = type;
            this.status = "pending";
            this.position = null;
            this.createdAt = DateTime.UtcNow.ToString("o");
            this.placedAt = null;
        }

        public Vector3 GetPositionVector()
        {
            if (position == null) return Vector3.zero;
            return new Vector3(position.x, position.y, position.z);
        }
    }

    [Serializable]
    public class MarkerPosition
    {
        public float x;
        public float y;
        public float z;

        public MarkerPosition(Vector3 v)
        {
            x = v.x;
            y = v.y;
            z = v.z;
        }
    }
}
