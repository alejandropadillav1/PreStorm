﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace PreStorm
{
    /// <summary>
    /// Provides extension methods for converting geometries into KML.
    /// </summary>
    public static class Kml
    {
        private static readonly XNamespace kml = "http://www.opengis.net/kml/2.2";

        private static string ToCoordinates(this Point geometry, double z)
        {
            return geometry.x + "," + geometry.y + "," + z;
        }

        private static string ToCoordinates(this double[] coordinates, double z)
        {
            return coordinates[0] + "," + coordinates[1] + "," + z;
        }

        private static string ToCoordinates(this double[][] coordinates, double z)
        {
            return string.Join(" ", coordinates.Select(c => c.ToCoordinates(z)));
        }

        private static bool IsInnerRing(double[][] ring)
        {
            return Enumerable.Range(0, ring.Length - 1)
                .Sum(i => ring[i][0] * ring[i + 1][1] - ring[i + 1][0] * ring[i][1]) > 0;
        }

        private static XElement ToKml(this Point geometry, double z, XElement[] extraElements)
        {
            return new XElement(kml + "Point", extraElements,
                new XElement(kml + "coordinates", geometry.ToCoordinates(z)));
        }

        private static XElement ToKml(this Multipoint geometry, double z, XElement[] extraElements)
        {
            return new XElement(kml + "MultiGeometry",
                geometry.points.Select(p => new Point { x = p[0], y = p[1] }.ToKml(z, extraElements)));
        }

        private static XElement ToKml(this Polyline geometry, double z, XElement[] extraElements)
        {
            return new XElement(kml + "MultiGeometry",
                geometry.paths.Select(p =>
                    new XElement(kml + "LineString", extraElements,
                        new XElement(kml + "coordinates", p.ToCoordinates(z)))));
        }

        private static XElement ToKml(this Polygon geometry, double z, XElement[] extraElements)
        {
            var polygons = new List<XElement>();

            foreach (var ring in geometry.rings)
            {
                var isInnerRing = IsInnerRing(ring);

                if (!isInnerRing)
                    polygons.Add(new XElement(kml + "Polygon", extraElements));

                polygons.Last().Add(new XElement(kml + (isInnerRing ? "innerBoundaryIs" : "outerBoundaryIs"),
                    new XElement(kml + "LinearRing",
                        new XElement(kml + "coordinates", ring.ToCoordinates(z)))));
            }

            return new XElement(kml + "MultiGeometry", polygons);
        }

        /// <summary>
        /// Converts the geometry into KML.
        /// </summary>
        /// <param name="geometry"></param>
        /// <param name="z">The altitude in meters.</param>
        /// <param name="extraGeometryElements">Any extra geometry elements (i.e. altitudeMode).</param>
        /// <returns></returns>
        public static XElement ToKml(this Geometry geometry, double z = 0, XElement[] extraGeometryElements = null)
        {
            if (geometry == null)
                return null;

            var point = geometry as Point;
            if (point != null)
                return point.ToKml(z, extraGeometryElements);

            var multipoint = geometry as Multipoint;
            if (multipoint != null)
                return multipoint.ToKml(z, extraGeometryElements);

            var polyline = geometry as Polyline;
            if (polyline != null)
                return polyline.ToKml(z, extraGeometryElements);

            var polygon = geometry as Polygon;
            if (polygon != null)
                return polygon.ToKml(z, extraGeometryElements);

            throw new Exception("This geometry type is not supported.");
        }

        /// <summary>
        /// Converts the feature into KML.
        /// </summary>
        /// <param name="feature"></param>
        /// <param name="name">The name for the placemark.</param>
        /// <param name="z">The altitude in meters.</param>
        /// <param name="extraGeometryElements">Any extra geometry elements (i.e. altitudeMode).</param>
        /// <param name="extraPlacemarkElements">Any extra placemark elements (i.e. styleUrl).</param>
        /// <returns></returns>
        public static XElement ToKml(this Feature feature, object name, double z = 0, XElement[] extraGeometryElements = null, XElement[] extraPlacemarkElements = null)
        {
            return new XElement(kml + "Placemark",
                       new XElement(kml + "name", name),
                       extraPlacemarkElements,
                       new XElement(kml + "ExtendedData",
                           from n in feature.AllFieldNames
                           select new XElement(kml + "Data", new XAttribute("name", n),
                                      new XElement(kml + "value", feature[n]))),
                                          ToKml(((dynamic)feature).Geometry, z, extraGeometryElements));
        }

        /// <summary>
        /// Converts the features into KML.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="features"></param>
        /// <param name="getName">The function that returns the name for the placemark.</param>
        /// <param name="getZ">The function that returns the elevation in meters.</param>
        /// <returns></returns>
        public static XElement ToKml<T>(this IEnumerable<T> features, Func<T, object> getName = null, Func<T, double> getZ = null) where T : Feature
        {
            var extraGeometryElements = getZ == null ? null : new[] { new XElement(kml + "extrude", 1), new XElement(kml + "altitudeMode", "relativeToGround") };

            return new XElement(kml + "kml",
                       new XElement(kml + "Document",
                           from f in features
                           select f.ToKml(getName == null ? f.OID : getName(f), getZ == null ? 0 : getZ(f), extraGeometryElements)));
        }
    }
}
