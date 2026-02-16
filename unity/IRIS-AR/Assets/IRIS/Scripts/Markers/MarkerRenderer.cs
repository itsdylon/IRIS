using UnityEngine;

namespace IRIS.Markers
{
    public class MarkerRenderer : MonoBehaviour
    {
        private MarkerData _data;

        public void Initialize(MarkerData data)
        {
            _data = data;
            gameObject.name = $"Marker_{data.label}";
        }

        public MarkerData GetData()
        {
            return _data;
        }
    }
}
