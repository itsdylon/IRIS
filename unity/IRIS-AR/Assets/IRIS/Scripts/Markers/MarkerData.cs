using System;
using UnityEngine;

namespace IRIS.Markers
{
    [Serializable]
    public class MarkerData
    {
        public string id;
        public float lat;
        public float lng;
        public string label;
        public string type;
        public string createdAt;

        public MarkerData(string id, float lat, float lng, string label, string type)
        {
            this.id = id;
            this.lat = lat;
            this.lng = lng;
            this.label = label;
            this.type = type;
            this.createdAt = DateTime.UtcNow.ToString("o");
        }
    }
}
