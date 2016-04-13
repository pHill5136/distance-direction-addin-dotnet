﻿/******************************************************************************* 
  * Copyright 2016 Esri 
  *  
  *  Licensed under the Apache License, Version 2.0 (the "License"); 
  *  you may not use this file except in compliance with the License. 
  *  You may obtain a copy of the License at 
  *  
  *  http://www.apache.org/licenses/LICENSE-2.0 
  *   
  *   Unless required by applicable law or agreed to in writing, software 
  *   distributed under the License is distributed on an "AS IS" BASIS, 
  *   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
  *   See the License for the specific language governing permissions and 
  *   limitations under the License. 
  ******************************************************************************/

// System
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

// Esri
using ArcGIS.Desktop.Catalog;
using ArcGIS.Core.Geometry;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Mapping;

using DistanceAndDirectionLibrary;
using System.Windows;

namespace ProAppDistanceAndDirectionModule.Models
{
    class FeatureClassUtils
    {
        private string previousLocation = "";

        /// <summary>
        /// Prompts the user to save features
        /// 
        /// </summary>
        /// <returns>The path to selected output (fgdb/shapefile)</returns>
        public string PromptUserWithSaveDialog(bool featureChecked, bool shapeChecked, bool kmlChecked)
        {
            //Prep the dialog
            SaveItemDialog saveItemDlg = new SaveItemDialog();
            saveItemDlg.Title = "Select output";
            saveItemDlg.OverwritePrompt = true;
            if (!string.IsNullOrEmpty(previousLocation))
                saveItemDlg.InitialLocation = previousLocation;


            // Set the filters and default extension
            if (featureChecked)
            {
                saveItemDlg.Filter = ItemFilters.geodatabaseItems_all;
            }
            else if (shapeChecked)
            {
                saveItemDlg.Filter = ItemFilters.shapefiles;
                saveItemDlg.DefaultExt = "shp";
            }
            else if (kmlChecked)
            {
                saveItemDlg.Filter = ItemFilters.kml;
                saveItemDlg.DefaultExt = "kmz";
            }

            bool? ok = saveItemDlg.ShowDialog();

            //Show the dialog and get the response
            if (ok == true)
            {
                string folderName = System.IO.Path.GetDirectoryName(saveItemDlg.FilePath);
                previousLocation = folderName;

                return saveItemDlg.FilePath; 
            }
            return null;
        }

        /// <summary>
        /// Creates the output featureclass, either fgdb featureclass or shapefile
        /// </summary>
        /// <param name="outputPath">location of featureclass</param>
        /// <param name="saveAsType">Type of output selected, either fgdb featureclass or shapefile</param>
        /// <param name="graphicsList">List of graphics for selected tab</param>
        /// <param name="ipSpatialRef">Spatial Reference being used</param>
        /// <returns>Output featureclass</returns>
        public async Task CreateFCOutput(string outputPath, SaveAsType saveAsType, List<Graphic> graphicsList, SpatialReference spatialRef, MapView mapview, GeomType geomType, bool isKML = false)
        {
            string dataset = System.IO.Path.GetFileName(outputPath);
            string connection = System.IO.Path.GetDirectoryName(outputPath);
            
            try
            {
                await QueuedTask.Run(async () =>
                {
                    await CreateFeatureClass(dataset, geomType, connection, spatialRef, graphicsList, mapview, isKML);
                });  
            }
            catch (Exception ex)
            {

            }
        }

        /// <summary>
        /// Create polyline features from graphics and add to table
        /// </summary>
        /// <param name="graphicsList">List of graphics to add to table</param>
        /// <returns></returns>
        private static async Task CreateFeatures(List<Graphic> graphicsList)
        {
            RowBuffer rowBuffer = null;
                
            try
            {
                await QueuedTask.Run(async () =>
                {
                    var layer = MapView.Active.GetSelectedLayers()[0];
                    if (layer is FeatureLayer)
                    {
                        var featureLayer = layer as FeatureLayer;
                        using (var table = featureLayer.GetTable())
                        {
                            TableDefinition definition = table.GetDefinition();
                            int shapeIndex = definition.FindField("Shape");
                                
                            //EditOperation editOperation = new EditOperation();
                            //editOperation.Callback(context =>
                            //{
                                foreach (Graphic graphic in graphicsList)
                                {
                                    //int nameIndex = featureClassDefinition.FindField("NAME");
                                    rowBuffer = table.CreateRowBuffer();

                                    if (graphic.Geometry is Polyline)
                                        rowBuffer[shapeIndex] = new PolylineBuilder(graphic.Geometry as Polyline).ToGeometry();
                                    else if (graphic.Geometry is Polygon)
                                        rowBuffer[shapeIndex] = new PolygonBuilder(graphic.Geometry as Polygon).ToGeometry();

                                    Row row = table.CreateRow(rowBuffer);

                                    //To Indicate that the Map has to draw this feature and/or the attribute table to be updated
                                    //context.Invalidate(row);
                                }
                            //});
                            //bool editResult = editOperation.Execute();
                            //bool saveResult = await Project.Current.SaveEditsAsync();
                        }
                    }
                });

            }
            catch (GeodatabaseException exObj)
            {
                Console.WriteLine(exObj);
                throw;
            }
            finally
            {
                if (rowBuffer != null)
                    rowBuffer.Dispose();
            }
        }

        /// <summary>
        /// Create a feature class
        /// </summary>
        /// <param name="dataset">Name of the feature class to be created.</param>
        /// <param name="featureclassType">Type of feature class to be created. Options are:
        /// <list type="bullet">
        /// <item>POINT</item>
        /// <item>MULTIPOINT</item>
        /// <item>POLYLINE</item>
        /// <item>POLYGON</item></list></param>
        /// <param name="connection">connection path</param>
        /// <param name="spatialRef">SpatialReference</param>
        /// <param name="graphicsList">List of graphics</param>
        /// <param name="mapview">MapView object</param>
        /// <param name="isKML">Is this a kml output</param>
        /// <returns></returns>
        private static async Task CreateFeatureClass(string dataset, GeomType geomType, string connection, SpatialReference spatialRef, List<Graphic> graphicsList, MapView mapview, bool isKML = false)
        {
            try
            {
                string strGeomType = geomType == GeomType.PolyLine ? "POLYLINE" : "POLYGON";

                List<object> arguments = new List<object>();
                // store the results in the geodatabase
                arguments.Add(connection);
                // name of the feature class
                arguments.Add(dataset);
                // type of geometry
                arguments.Add(strGeomType);
                // no template
                arguments.Add("");
                // no z values
                arguments.Add("DISABLED");
                // no m values
                arguments.Add("DISABLED");
                arguments.Add(spatialRef);

                //IReadOnlyList<string> valueArray = null;
                //await QueuedTask.Run(async () =>
                //{
                //    valueArray = Geoprocessing.MakeValueArray(arguments.ToArray());
                //});

                //block the CIM for a second
                //Task.Delay(5000).Wait();
                var valueArray = Geoprocessing.MakeValueArray(arguments.ToArray());
                IGPResult result = await Geoprocessing.ExecuteToolAsync("CreateFeatureclass_management", valueArray);

                await CreateFeatures(graphicsList);

                if (isKML)
                {                
                    await KMLUtils.ConvertLayerToKML(connection, dataset, MapView.Active);

                    // Delete temporary Shapefile
                    string[] extensionNames = {".cpg", ".dbf", ".prj", ".shx", ".shp"};
                    string datasetNoExtension = Path.GetFileNameWithoutExtension(dataset);
                    foreach (string extension in extensionNames)
                    {
                        string shapeFile = Path.Combine(connection, datasetNoExtension + extension);
                        File.Delete(shapeFile);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }
/*
        /// <summary>
        /// Determines if selected feature class already exists
        /// </summary>
        /// <param name="gdbPath">Path to the file gdb</param>
        /// <param name="fcName">Name of selected feature class</param>
        /// <returns>True if already exists, false otherwise</returns>
        private bool DoesFeatureClassExist(string gdbPath, string fcName)
        {
            List<string> dsNames = GetAllDatasetNames(gdbPath);

            if (dsNames.Contains(fcName))
                return true;

            return false;
        }

        /// <summary>
        /// Retrieves all datasets names from filegdb
        /// </summary>
        /// <param name="gdbFilePath">Path to filegdb</param>
        /// <returns>List of names of all featureclasses in filegdb</returns>
        private List<string> GetAllDatasetNames(string gdbFilePath)
        {
            IWorkspaceFactory workspaceFactory = new FileGDBWorkspaceFactory();
            IWorkspace workspace = workspaceFactory.OpenFromFile (gdbFilePath, 0);
            IEnumDataset enumDataset = workspace.get_Datasets(esriDatasetType.esriDTAny);
            List<string> names = new List<string>();
            IDataset dataset = null;
            while((dataset = enumDataset.Next())!= null)
            {
                names.Add(dataset.Name);
            }
            return names;
        }

        /// <summary>
        /// Delete a featureclass
        /// </summary>
        /// <param name="fWorkspace">IFeatureWorkspace</param>
        /// <param name="fcName">Name of featureclass to delete</param>
        private void DeleteFeatureClass(IFeatureWorkspace fWorkspace, string fcName)
        {
            IDataset ipDs = fWorkspace.OpenFeatureClass(fcName) as IDataset;
            ipDs.Delete();
        }
 */

    }

    
}
