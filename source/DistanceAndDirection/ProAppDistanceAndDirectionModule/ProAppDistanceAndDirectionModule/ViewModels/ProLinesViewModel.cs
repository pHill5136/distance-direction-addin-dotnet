﻿// Copyright 2016 Esri 
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//    http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using DistanceAndDirectionLibrary;
using DistanceAndDirectionLibrary.Helpers;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ProAppDistanceAndDirectionModule.ViewModels
{
    public class ProLinesViewModel : ProTabBaseViewModel
    {
        public ProLinesViewModel()
        {
            IsActiveTab = true;

            LineFromType = LineFromTypes.Points;
            LineAzimuthType = AzimuthTypes.Degrees;

            ActivateToolCommand = new ArcGIS.Desktop.Framework.RelayCommand(async ()=> 
                {
                    await FrameworkApplication.SetCurrentToolAsync("ProAppDistanceAndDirectionModule_SketchTool");
                    Mediator.NotifyColleagues("SET_SKETCH_TOOL_TYPE", ArcGIS.Desktop.Mapping.SketchGeometryType.Line);
                });

            Mediator.Register("SKETCH_COMPLETE", OnSketchComplete);
        }
        LineFromTypes lineFromType = LineFromTypes.Points;
        public LineFromTypes LineFromType
        {
            get { return lineFromType; }
            set
            {
                lineFromType = value;
                RaisePropertyChanged(() => LineFromType);

                // reset points
                ResetPoints();

                // stop feedback when from type changes
                ResetFeedback();
            }
        }

        LineTypes lineType = LineTypes.Geodesic;
        public LineTypes LineType
        {
            get { return lineType;  }
            set
            {
                lineType = value;
                RaisePropertyChanged(() => LineType);
                ResetFeedback();
            }
        }

        public GeodeticCurveType DeriveCurveType(LineTypes type)
        {
            GeodeticCurveType curveType = GeodeticCurveType.Geodesic;
            if (type == LineTypes.Geodesic)
                curveType = GeodeticCurveType.Geodesic;
            else if (type == LineTypes.GreatElliptic)
                curveType = GeodeticCurveType.GreatElliptic;
            else if (type == LineTypes.Loxodrome)
                curveType = GeodeticCurveType.Loxodrome;
            return curveType;
        }

        public System.Windows.Visibility LineTypeComboVisibility
        {
            get { return System.Windows.Visibility.Visible; }
        }


        AzimuthTypes lineAzimuthType = AzimuthTypes.Degrees;
        public AzimuthTypes LineAzimuthType
        {
            get { return lineAzimuthType; }
            set
            {
                if (LineFromType == LineFromTypes.Points)
                {
                    var before = lineAzimuthType;
                    lineAzimuthType = value;
                    UpdateAzimuthFromTo(before, value);
                }
                else
                {
                    lineAzimuthType = value;
                }
            }
        }

        public override MapPoint Point1
        {
            get
            {
                return base.Point1;
            }
            set
            {
                base.Point1 = value;

                if (LineFromType == LineFromTypes.BearingAndDistance)
                {
                    ResetFeedback();
                }
            }
        }

        double distance = 0.0;
        public override double Distance
        {
            get { return distance; }
            set
            {
                distance = value;
                RaisePropertyChanged(() => Distance);

                if (LineFromType == LineFromTypes.BearingAndDistance)
                    UpdateManualFeedback();
                else
                    UpdateFeedback();

                DistanceString = distance.ToString("G");
                RaisePropertyChanged(() => DistanceString);
            }
        }

        public override DistanceTypes LineDistanceType
        {
            get
            {
                return base.LineDistanceType;
            }
            set
            {
                if (LineFromType == LineFromTypes.Points)
                {
                    var before = base.LineDistanceType;
                    Distance = ConvertFromTo(before, value, Distance);
                }
                base.LineDistanceType = value;
            }
        }

        public LinearUnit DeriveUnit(DistanceTypes distType)
        {
            LinearUnit lu = LinearUnit.Meters;
            if (distType == DistanceTypes.Feet)
                lu = LinearUnit.Feet;
            else if (distType == DistanceTypes.Kilometers)
                lu = LinearUnit.Kilometers;
            else if (distType == DistanceTypes.Meters)
                lu = LinearUnit.Meters;
            else if (distType == DistanceTypes.Miles)
                lu = LinearUnit.Miles;
            else if (distType == DistanceTypes.NauticalMile)
                lu = LinearUnit.NauticalMiles;
            else if (distType == DistanceTypes.Yards)
                lu = LinearUnit.Yards;
            return lu;
        }

        internal override async void UpdateFeedback()
        {
            GeodeticCurveType curveType = DeriveCurveType(LineType);
            LinearUnit lu = DeriveUnit(LineDistanceType);
            if (LineFromType == LineFromTypes.Points)
            {
                if (Point1 == null || Point2 == null)
                    return;

                var segment = QueuedTask.Run(() =>
                {
                    return LineBuilder.CreateLineSegment(Point1, Point2);
                }).Result;
          
                
                UpdateAzimuth(segment.Angle);

                await UpdateFeedbackWithGeoLine(segment, curveType, lu);
            }
            else
            {
                UpdateManualFeedback();
            }
        }

        double? azimuth = 0.0;
        public double? Azimuth
        {
            get { return azimuth; }
            set
            {
                if (value < 0.0)
                    throw new ArgumentException(DistanceAndDirectionLibrary.Properties.Resources.AEMustBePositive);

                azimuth = value;
                RaisePropertyChanged(() => Azimuth);

                if (!azimuth.HasValue)
                    throw new ArgumentException(DistanceAndDirectionLibrary.Properties.Resources.AEInvalidInput);

                AzimuthString = azimuth.Value.ToString("G");
                RaisePropertyChanged(() => AzimuthString);
            }
        }
        string azimuthString = string.Empty;
        public string AzimuthString
        {
            get { return azimuthString; }
            set
            {
                // lets avoid an infinite loop here
                if (string.Equals(azimuthString, value))
                    return;

                azimuthString = value;

                if (LineFromType == LineFromTypes.BearingAndDistance)
                {
                    // update azimuth
                    double d = 0.0;
                    if (double.TryParse(azimuthString, out d))
                    {
                        if (Azimuth == d)
                            return;

                        Azimuth = d;

                        UpdateManualFeedback();
                    }
                    else
                    {
                        Azimuth = null;
                        throw new ArgumentException(DistanceAndDirectionLibrary.Properties.Resources.AEInvalidInput);
                    }
                }
            }
        }

        private async void UpdateManualFeedback()
        {
            if (LineFromType == LineFromTypes.BearingAndDistance && Azimuth.HasValue && HasPoint1 && Point1 != null)
            {
                GeodeticCurveType curveType = DeriveCurveType(LineType);
                LinearUnit lu = DeriveUnit(LineDistanceType);
                // update feedback
                var segment = QueuedTask.Run(() =>
                {
                    var mpList = new List<MapPoint>() { Point1 };
                    // get point 2
                    // SDK Bug, GeometryEngine.GeodesicMove seems to not honor the LinearUnit passed in, always does Meters
                    var tempDistance = ConvertFromTo(LineDistanceType, DistanceTypes.Meters, Distance);
                    var results = GeometryEngine.Instance.GeodeticMove(mpList, MapView.Active.Map.SpatialReference, tempDistance, LinearUnit.Meters /*GetLinearUnit(LineDistanceType)*/, GetAzimuthAsRadians().Value, GetCurveType());
                    foreach (var mp in results)
                        Point2 = mp;
                    if (Point2 != null)
                        return LineBuilder.CreateLineSegment(Point1, Point2);
                    else
                        return null;
                }).Result;

                if (segment != null)
                    await UpdateFeedbackWithGeoLine(segment, curveType, lu);
            }
        }

        /// <summary>
        /// On top of the base class we need to make sure we have an azimuth
        /// </summary>
        public override bool CanCreateElement
        {
            get
            {
                return (Azimuth.HasValue && base.CanCreateElement);
            }
        }

        // sketch doesn't support geodesic lines
        // not using this for now
        private void OnSketchComplete(object obj)
        {
            AddGraphicToMap(obj as ArcGIS.Core.Geometry.Geometry);
        }

        public ArcGIS.Desktop.Framework.RelayCommand ActivateToolCommand { get; set; }

        /// <summary>
        /// Overrides TabBaseViewModel CreateMapElement
        /// </summary>
        internal override Geometry CreateMapElement()
        {
            Geometry geom = null;
            if (!CanCreateElement)
                return geom;

            base.CreateMapElement();
            geom = CreatePolyline();
            Reset(false);

            return geom;
        }

        internal override void Reset(bool toolReset)
        {
            base.Reset(toolReset);

            Azimuth = 0.0;
        }

        internal async override void OnNewMapPointEvent(object obj)
        {
            if (!IsActiveTab)
                return;

            var point = obj as MapPoint;

            if (point == null)
                return;

            // Bearing and Distance Mode
            if (LineFromType == LineFromTypes.BearingAndDistance)
            {
                ClearTempGraphics();
                Point1 = point;
                HasPoint1 = true;
                await AddGraphicToMap(Point1, ColorFactory.Instance.GreenRGB, null, true, 5.0);
                return;
            }

            base.OnNewMapPointEvent(obj);
        }

        internal override async void OnMouseMoveEvent(object obj)
        {
            GeodeticCurveType curveType = DeriveCurveType(LineType);
            LinearUnit lu = DeriveUnit(LineDistanceType);
            if (!IsActiveTab)
                return;

            var point = obj as MapPoint;

            if (point == null)
                return;

            if (LineFromType == LineFromTypes.BearingAndDistance)
                return;

            if (HasPoint1 && !HasPoint2)
            {
                // update azimuth from segment
                var segment = QueuedTask.Run(() =>
                {
                    return LineBuilder.CreateLineSegment(Point1, point);
                }).Result;

                UpdateAzimuth(segment.Angle);
                await UpdateFeedbackWithGeoLine(segment, curveType, lu);                        
            }

            base.OnMouseMoveEvent(obj);
        }

        private Geometry CreatePolyline()
        {
            if (Point1 == null || Point2 == null)
                return null;
            GeodeticCurveType curveType = DeriveCurveType(LineType);
            LinearUnit lu = DeriveUnit(LineDistanceType);
            try
            {
                // create line
                var polyline = QueuedTask.Run(() =>
                    {
                        var segment = LineBuilder.CreateLineSegment(Point1, Point2);
                        return PolylineBuilder.CreatePolyline(segment);
                    }).Result;
                Geometry newline = GeometryEngine.Instance.GeodeticDensifyByLength(polyline, 0, lu, curveType);

                // Hold onto the attributes in case user saves graphics to file later
                LineAttributes lineAttributes = new LineAttributes(){mapPoint1 = Point1, mapPoint2 = Point2, _distance = distance, angle = (double)azimuth};

                AddGraphicToMap(newline, (ProGraphicAttributes)lineAttributes);
                ResetPoints();

                return newline as Geometry;
            }
            catch(Exception ex)
            {
                // do nothing
                return null;
            }
        }

        private void UpdateAzimuth(double radians)
        {
            var degrees = radians * (180.0 / Math.PI);
            if (degrees <= 90.0)
                degrees = 90.0 - degrees;
            else
                degrees = 360.0 - (degrees - 90.0);

            if (LineAzimuthType == AzimuthTypes.Degrees)
                Azimuth = degrees;
            else if (LineAzimuthType == AzimuthTypes.Mils)
                Azimuth = degrees * 17.777777778;
        }

        private double? GetAzimuthAsRadians()
        {
            double? result = GetAzimuthAsDegrees();

            return result * (Math.PI / 180.0);
        }

        private double? GetAzimuthAsDegrees()
        {
            if (LineAzimuthType == AzimuthTypes.Mils)
            {
                return Azimuth * 0.05625;
            }

            return Azimuth;
        }


        private double GetAngleDegrees(double angle)
        {
            double bearing = (180.0 * angle) / Math.PI;
            if (bearing < 90.0)
                bearing = 90 - bearing;
            else
                bearing = 360.0 - (bearing - 90);

            if (LineAzimuthType == AzimuthTypes.Degrees)
            {
                return bearing;
            }

            if (LineAzimuthType == AzimuthTypes.Mils)
            {
                return bearing * 17.777777778;
            }

            return 0.0;
        }

        private void UpdateAzimuthFromTo(AzimuthTypes fromType, AzimuthTypes toType)
        {
            try
            {
                double angle = Azimuth.GetValueOrDefault();

                if (fromType == AzimuthTypes.Degrees && toType == AzimuthTypes.Mils)
                    angle *= 17.777777778;
                else if (fromType == AzimuthTypes.Mils && toType == AzimuthTypes.Degrees)
                    angle *= 0.05625;

                Azimuth = angle;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

    }
}
