using Microsoft.Kinect;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using KVector4 = Microsoft.Kinect.Vector4;

namespace ArPhysics2
{
    static class Extensions
    {
        public static Vector3 ToXNA(this CameraSpacePoint p) => new Vector3(p.X, p.Y, -p.Z); // the Z is flipped!
        public static Quaternion ToXNA(this KVector4 v) => new Quaternion(v.X, v.Y, -v.Z, -v.W);

        // enable tuple syntax for KeyValuePair
        public static void Deconstruct<K, V>(this KeyValuePair<K, V> source, out K key, out V value)
        {
            key = source.Key;
            value = source.Value;
        }

        public static T Rnd<T>(this IEnumerable<T> e) => e.ElementAt(new Random().Next(e.Count()));

        public static int Mod(this int x, int m) => (x % m + m) % m;

        public static Dictionary<K, OutV> MapToDictionary<K, InV, OutV>(
            this IEnumerable<(K, InV)> kvPairs, Func<InV, OutV> mapper)
        {
            var dict = new Dictionary<K, OutV>(kvPairs.Count());
            foreach (var (key, value) in kvPairs)
                dict.Add(key, mapper(value));
            return dict;
        }

        public static Dictionary<K, V> ToDictionary<K, V>(this IEnumerable<(K, V)> kvPairs) => 
            kvPairs.MapToDictionary(v => v);
    }
}
