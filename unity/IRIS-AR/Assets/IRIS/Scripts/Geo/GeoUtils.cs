using UnityEngine;

namespace IRIS.Geo
{
    public static class GeoUtils
    {
        private const double MetersPerDegreeLat = 110540.0;
        private const double MetersPerDegreeLng = 111320.0;

        public static Vector3 LatLngToUnityPosition(double lat, double lng, double refLat, double refLng)
        {
            double cosRef = System.Math.Cos(refLat * System.Math.PI / 180.0);
            float x = (float)((lng - refLng) * MetersPerDegreeLng * cosRef);
            float z = (float)((lat - refLat) * MetersPerDegreeLat);
            float y = 1.5f;
            return new Vector3(x, y, z);
        }

        public static (double lat, double lng) UnityPositionToLatLng(Vector3 pos, double refLat, double refLng)
        {
            double cosRef = System.Math.Cos(refLat * System.Math.PI / 180.0);
            double lng = pos.x / (MetersPerDegreeLng * cosRef) + refLng;
            double lat = pos.z / MetersPerDegreeLat + refLat;
            return (lat, lng);
        }
    }
}
