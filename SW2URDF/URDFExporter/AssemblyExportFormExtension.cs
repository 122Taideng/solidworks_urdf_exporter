﻿/*
Copyright (c) 2015 Stephen Brawner

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.  IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using System.Xml.Serialization;

using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SW2URDF
{
    //This source file contains all the non-handler methods for the assembly export form,
    // the ones that are helpers.
    public partial class AssemblyExportForm : Form
    {
        //From the link, this method fills the property boxes on the Link Properties page
        public void FillLinkPropertyBoxes(Link Link)
        {
            FillBlank(linkBoxes);
            if (!Link.isFixedFrame)
            {
                Link.Visual.Origin.FillBoxes(textBoxVisualOriginX,
                                             textBoxVisualOriginY,
                                             textBoxVisualOriginZ,
                                             textBoxVisualOriginRoll,
                                             textBoxVisualOriginPitch,
                                             textBoxVisualOriginYaw,
                                             "G5");

                Link.Inertial.Origin.FillBoxes(textBoxInertialOriginX,
                                               textBoxInertialOriginY,
                                               textBoxInertialOriginZ,
                                               textBoxInertialOriginRoll,
                                               textBoxInertialOriginPitch,
                                               textBoxInertialOriginYaw,
                                               "G5");

                Link.Inertial.Mass.FillBoxes(textBoxMass, "G5");

                Link.Inertial.Inertia.FillBoxes(textBoxIxx,
                                                textBoxIxy,
                                                textBoxIxz,
                                                textBoxIyy,
                                                textBoxIyz,
                                                textBoxIzz,
                                                "G5");

                Link.Visual.Material.FillBoxes(comboBoxMaterials, "G5");
                textBoxTexture.Text = Link.Visual.Material.Texture.wFilename;

                Link.Visual.Material.Color.FillBoxes(domainUpDownRed,
                                                     domainUpDownGreen,
                                                     domainUpDownBlue,
                                                     domainUpDownAlpha,
                                                     "G5");

                radioButtonFine.Checked = Link.STLQualityFine;
                radioButtonCourse.Checked = !Link.STLQualityFine;
            }
        }

        //Fills the property boxes on the joint properties page
        public void FillJointPropertyBoxes(Joint Joint)
        {
            FillBlank(jointBoxes);
            AutoUpdatingForm = true;
            if (Joint != null) //For the base_link or if none is selected
            {
                Joint.FillBoxes(textBoxJointName, comboBoxJointType);
                Joint.Parent.FillBoxes(labelParent);
                Joint.Child.FillBoxes(labelChild);

                //G5: Maximum decimal places to use (not counting exponential notation) is 5

                Joint.Origin.FillBoxes(textBoxJointX,
                                       textBoxJointY,
                                       textBoxJointZ,
                                       textBoxJointRoll,
                                       textBoxJointPitch,
                                       textBoxJointYaw,
                                       "G5");

                Joint.Axis.FillBoxes(textBoxAxisX, textBoxAxisY, textBoxAxisZ, "G5");

                if (Joint.Limit != null)
                {
                    Joint.Limit.FillBoxes(textBoxLimitLower,
                                          textBoxLimitUpper,
                                          textBoxLimitEffort,
                                          textBoxLimitVelocity,
                                          "G5");
                }

                if (Joint.Calibration != null)
                {
                    Joint.Calibration.FillBoxes(textBoxCalibrationRising,
                                                textBoxCalibrationFalling,
                                                "G5");
                }

                if (Joint.Dynamics != null)
                {
                    Joint.Dynamics.FillBoxes(textBoxDamping,
                                             textBoxFriction,
                                             "G5");
                }

                if (Joint.Safety != null)
                {
                    Joint.Safety.FillBoxes(textBoxSoftLower,
                                           textBoxSoftUpper,
                                           textBoxKPosition,
                                           textBoxKVelocity,
                                           "G5");
                }
            }

            if (Joint != null && (Joint.Type == "revolute" || Joint.Type == "continuous"))
            {
                labelLowerLimit.Text = "lower (rad)";
                labelLimitUpper.Text = "upper (rad)";
                labelEffort.Text = "effort (N-m)";
                labelVelocity.Text = "velocity (rad/s)";
                labelFriction.Text = "friction (N-m)";
                labelDamping.Text = "damping (N-m-s/rad)";
                labelSoftLower.Text = "soft lower limit (rad)";
                labelSoftUpper.Text = "soft upper limit (rad)";
                labelKPosition.Text = "k position";
                labelKVelocity.Text = "k velocity";
            }
            else if (Joint != null && Joint.Type == "prismatic")
            {
                labelLowerLimit.Text = "lower (m)";
                labelLimitUpper.Text = "upper (m)";
                labelEffort.Text = "effort (N)";
                labelVelocity.Text = "velocity (m/s)";
                labelFriction.Text = "friction (N)";
                labelDamping.Text = "damping (N-s/m)";
                labelSoftLower.Text = "soft lower limit (m)";
                labelSoftUpper.Text = "soft upper limit (m)";
                labelKPosition.Text = "k position";
                labelKVelocity.Text = "k velocity";
            }
            else
            {
                labelLowerLimit.Text = "lower";
                labelLimitUpper.Text = "upper";
                labelEffort.Text = "effort";
                labelVelocity.Text = "velocity";
                labelFriction.Text = "friction";
                labelDamping.Text = "damping";
                labelSoftLower.Text = "soft lower limit";
                labelSoftUpper.Text = "soft upper limit";
                labelKPosition.Text = "k position";
                labelKVelocity.Text = "k velocity";
            }
            comboBoxOrigin.Items.Clear();
            List<string> originNames = Exporter.FindRefGeoNames("CoordSys");
            comboBoxOrigin.Items.AddRange(originNames.ToArray());
            comboBoxAxis.Items.Clear();
            List<string> axesNames = Exporter.FindRefGeoNames("RefAxis");
            comboBoxAxis.Items.AddRange(axesNames.ToArray());
            comboBoxOrigin.SelectedIndex =
                comboBoxOrigin.FindStringExact(Joint.CoordinateSystemName);
            if (String.IsNullOrWhiteSpace(Joint.AxisName))
            {
                comboBoxAxis.SelectedIndex = comboBoxAxis.FindStringExact(Joint.AxisName);
            }
            AutoUpdatingForm = false;
        }

        public void FillBlank(Control[] boxes)
        {
            foreach (Control box in boxes)
            {
                box.Text = "";
            }
        }

        //Converts the text boxes back into values for the link
        public void SaveLinkDataFromPropertyBoxes(Link Link)
        {
            if (!Link.isFixedFrame)
            {
                Link.Inertial.Origin.Update(textBoxInertialOriginX,
                                            textBoxInertialOriginY,
                                            textBoxInertialOriginZ,
                                            textBoxInertialOriginRoll,
                                            textBoxInertialOriginPitch,
                                            textBoxInertialOriginYaw);

                Link.Visual.Origin.Update(textBoxVisualOriginX,
                                          textBoxVisualOriginY,
                                          textBoxVisualOriginZ,
                                          textBoxVisualOriginRoll,
                                          textBoxVisualOriginPitch,
                                          textBoxVisualOriginYaw);

                Link.Inertial.Mass.Update(textBoxMass);

                Link.Inertial.Inertia.Update(textBoxIxx,
                                             textBoxIxy,
                                             textBoxIxz,
                                             textBoxIyy,
                                             textBoxIyz,
                                             textBoxIzz);

                Link.Visual.Material.Name = comboBoxMaterials.Text;
                Link.Visual.Material.Texture.wFilename = textBoxTexture.Text;

                Link.Visual.Material.Color.Update(domainUpDownRed,
                                                  domainUpDownGreen,
                                                  domainUpDownBlue,
                                                  domainUpDownAlpha);

                Link.STLQualityFine = radioButtonFine.Checked;
            }
        }

        //Saves data from text boxes back into a joint
        public void SaveJointDataFromPropertyBoxes(Joint Joint)
        {
            Joint.Update(textBoxJointName, comboBoxJointType);

            Joint.Parent.Update(labelParent);
            Joint.Child.Update(labelChild);

            Joint.CoordinateSystemName = comboBoxOrigin.Text;
            Joint.AxisName = comboBoxAxis.Text;

            Joint.Origin.Update(textBoxJointX,
                                textBoxJointY,
                                textBoxJointZ,
                                textBoxJointRoll,
                                textBoxJointPitch,
                                textBoxJointYaw);

            Joint.Axis.Update(textBoxAxisX,
                              textBoxAxisY,
                              textBoxAxisZ);

            if (String.IsNullOrWhiteSpace(textBoxLimitLower.Text) &&
                String.IsNullOrWhiteSpace(textBoxLimitUpper.Text) &&
                String.IsNullOrWhiteSpace(textBoxLimitEffort.Text) &&
                String.IsNullOrWhiteSpace(textBoxLimitVelocity.Text))
            {
                if (Joint.Type == "prismatic" || Joint.Type == "revolute")
                {
                    if (Joint.Limit == null)
                    {
                        Joint.Limit = new Limit();
                    }
                    else
                    {
                        Joint.Limit.Effort = 0;
                        Joint.Limit.Velocity = 0;
                    }
                }
                else
                {
                    Joint.Limit = null;
                }
            }
            else
            {
                if (Joint.Limit == null)
                {
                    Joint.Limit = new Limit();
                }
                Joint.Limit.SetValues(textBoxLimitLower,
                                   textBoxLimitUpper,
                                   textBoxLimitEffort,
                                   textBoxLimitVelocity);
            }

            if (String.IsNullOrWhiteSpace(textBoxCalibrationRising.Text) &&
                String.IsNullOrWhiteSpace(textBoxCalibrationFalling.Text))
            {
                Joint.Calibration = null;
            }
            else
            {
                if (Joint.Calibration == null)
                {
                    Joint.Calibration = new Calibration();
                }
                Joint.Calibration.SetValues(textBoxCalibrationRising,
                                         textBoxCalibrationFalling);
            }

            if (String.IsNullOrWhiteSpace(textBoxFriction.Text) &&
                String.IsNullOrWhiteSpace(textBoxDamping.Text))
            {
                Joint.Dynamics = null;
            }
            else
            {
                if (Joint.Dynamics == null)
                {
                    Joint.Dynamics = new Dynamics();
                }
                Joint.Dynamics.SetValues(textBoxDamping,
                                      textBoxFriction);
            }

            if (String.IsNullOrWhiteSpace(textBoxSoftLower.Text) &&
                String.IsNullOrWhiteSpace(textBoxSoftUpper.Text) && String.IsNullOrWhiteSpace(textBoxKPosition.Text) && String.IsNullOrWhiteSpace(textBoxKVelocity.Text))
            {
                Joint.Safety = null;
            }
            else
            {
                if (Joint.Safety == null)
                {
                    Joint.Safety = new SafetyController();
                }
                Joint.Safety.SetValues(textBoxSoftLower,
                                    textBoxSoftUpper,
                                    textBoxKPosition,
                                    textBoxKVelocity);
            }
        }

        //Fills either TreeView from the URDF robot
        public void FillTreeViewFromRobot(Robot Robot, TreeView tree)
        {
            LinkNode baseNode = new LinkNode();
            Link baseLink = Robot.BaseLink;
            baseNode.Name = baseLink.Name;
            baseNode.Text = baseLink.Name;
            baseNode.Link = baseLink;
            baseNode.IsBaseNode = true;
            baseNode.LinkName = baseLink.Name;
            baseNode.Components = baseLink.SWcomponents;
            baseNode.CoordsysName = "Origin_global";
            baseNode.IsIncomplete = false;

            foreach (Link child in baseLink.Children)
            {
                baseNode.Nodes.Add(CreateLinkNodeFromLink(child));
            }
            tree.Nodes.Add(baseNode);
            tree.ExpandAll();
        }

        //Fills specifically the joint TreeView
        public void FillJointTree()
        {
            treeViewJointTree.Nodes.Clear();

            while (BaseNode.Nodes.Count > 0)
            {
                LinkNode node = (LinkNode)BaseNode.FirstNode;
                BaseNode.Nodes.Remove(node);
                treeViewJointTree.Nodes.Add(node);
                UpdateNodeText(node, true);
            }
            treeViewJointTree.ExpandAll();
        }

        public void FillLinkTree()
        {
            treeViewLinkProperties.Nodes.Clear();
            treeViewLinkProperties.Nodes.Add(BaseNode);
            UpdateNodeText(BaseNode, false);
            treeViewLinkProperties.ExpandAll();
        }

        public void UpdateNodeText(LinkNode node, bool useJointName)
        {
            if (useJointName)
            {
                node.Text = node.Link.Joint.Name;
            }
            else
            {
                node.Text = node.Link.Name;
            }
            foreach (LinkNode child in node.Nodes)
            {
                UpdateNodeText(child, useJointName);
            }
        }

        //Converts a Link to a LinkNode
        public LinkNode CreateLinkNodeFromLink(Link Link)
        {
            LinkNode node = new LinkNode
            {
                Name = Link.Name,
                Text = Link.Name,
                Link = Link,
                IsBaseNode = false,
                LinkName = Link.Name,
                JointName = Link.Joint.Name,
                Components = Link.SWcomponents,
                CoordsysName = Link.Joint.CoordinateSystemName,
                AxisName = Link.Joint.AxisName,
                JointType = Link.Joint.Type,
                IsIncomplete = false
            };

            foreach (Link child in Link.Children)
            {
                node.Nodes.Add(CreateLinkNodeFromLink(child));
            }

            // Need to erase the children from the embedded link because they may be rearranged later.
            node.Link.Children.Clear();
            return node;
        }

        //Converts a TreeView back into a robot
        public Robot CreateRobotFromTreeView(TreeView tree)
        {
            //TODO: This needs to properly handle the new differences between the trees.
            Robot Robot = Exporter.URDFRobot;
            Robot.BaseLink = CreateLinkFromLinkNode((LinkNode)tree.Nodes[0]);
            Robot.Name = Exporter.URDFRobot.Name;
            return Robot;
        }

        //Converts a LinkNode into a Link
        public Link CreateLinkFromLinkNode(LinkNode node)
        {
            Link Link = node.Link;
            Link.Children.Clear();
            foreach (LinkNode child in node.Nodes)
            {
                Link childLink = CreateLinkFromLinkNode(child);
                Link.Children.Add(childLink); // Recreates the children of each embedded link
            }
            return Link;
        }

        private bool CheckLinkAlpha(Link node)
        {
            return node.Visual.Material.Color.Alpha == 1.0;
        }

        private void CheckLinksForWarnings(Link node, StringBuilder builder)
        {
            if (!CheckLinkAlpha(node))
            {
                builder.Append(node.Name + " has an Visual Alpha color below 1.0");
            }
            foreach (Link child in node.Children)
            {
                CheckLinksForWarnings(child, builder);
            }
        }

        private string CheckLinksForWarnings(Link baseNode)
        {
            StringBuilder builder = new StringBuilder();
            CheckLinksForWarnings(baseNode, builder);
            return builder.ToString();
        }

        public void SaveConfigTree(ModelDoc2 model, LinkNode BaseNode, bool warnUser)
        {
            Object[] objects = model.FeatureManager.GetFeatures(true);
            string oldData = "";
            Parameter param;
            foreach (Object obj in objects)
            {
                Feature feat = (Feature)obj;
                string t = feat.GetTypeName2();
                if (feat.GetTypeName2() == "Attribute")
                {
                    SolidWorks.Interop.sldworks.Attribute att =
                        (SolidWorks.Interop.sldworks.Attribute)feat.GetSpecificFeature2();
                    if (att.GetName() == "URDF Export Configuration")
                    {
                        param = att.GetParameter("data");
                        oldData = param.GetStringValue();
                    }
                }
            }

            Common.RetrieveSWComponentPIDs(model, BaseNode);
            SerialNode sNode = new SerialNode(BaseNode);

            StringWriter stringWriter;
            XmlSerializer serializer = new XmlSerializer(typeof(SerialNode));
            stringWriter = new StringWriter();
            serializer.Serialize(stringWriter, sNode);
            stringWriter.Flush();
            stringWriter.Close();

            string newData = stringWriter.ToString();
            if (oldData != newData)
            {
                if (!warnUser ||
                    (warnUser &&
                    MessageBox.Show("The configuration has changed, would you like to save?",
                        "Save Export Configuration", MessageBoxButtons.YesNo) == DialogResult.Yes))
                {
                    int ConfigurationOptions = (int)swInConfigurationOpts_e.swAllConfiguration;
                    SolidWorks.Interop.sldworks.Attribute saveExporterAttribute =
                        CreateSWSaveAttribute(swApp, "URDF Export Configuration");
                    param = saveExporterAttribute.GetParameter("data");
                    param.SetStringValue2(stringWriter.ToString(), ConfigurationOptions, "");
                    param = saveExporterAttribute.GetParameter("name");
                    param.SetStringValue2("config1", ConfigurationOptions, "");
                    param = saveExporterAttribute.GetParameter("date");
                    param.SetStringValue2(DateTime.Now.ToString(), ConfigurationOptions, "");
                    param = saveExporterAttribute.GetParameter("exporterVersion");
                    param.SetStringValue2("1.1", ConfigurationOptions, "");
                }
            }
        }

        private SolidWorks.Interop.sldworks.Attribute CreateSWSaveAttribute(ISldWorks iSwApp, string name)
        {
            int Options = 0;
            int ConfigurationOptions = (int)swInConfigurationOpts_e.swAllConfiguration;
            ModelDoc2 ActiveSWModel = iSwApp.ActiveDoc;
            Object[] objects = ActiveSWModel.FeatureManager.GetFeatures(true);
            foreach (Object obj in objects)
            {
                Feature feat = (Feature)obj;
                string t = feat.GetTypeName2();
                if (feat.GetTypeName2() == "Attribute")
                {
                    SolidWorks.Interop.sldworks.Attribute att =
                        (SolidWorks.Interop.sldworks.Attribute)feat.GetSpecificFeature2();
                    if (att.GetName() == name)
                    {
                        return att;
                    }
                }
            }

            SolidWorks.Interop.sldworks.Attribute saveExporterAttribute =
                saveConfigurationAttributeDef.CreateInstance5(ActiveSWModel, null,
                "URDF Export Configuration", Options, ConfigurationOptions);
            return saveExporterAttribute;
        }

        public void ChangeAllNodeFont(LinkNode node, Font font)
        {
            node.NodeFont = font;
            foreach (LinkNode child in node.Nodes)
            {
                ChangeAllNodeFont(child, font);
            }
        }
    }
}