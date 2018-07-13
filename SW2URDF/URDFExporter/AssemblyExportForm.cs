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
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SW2URDF
{
    public partial class AssemblyExportForm : Form
    {
        private static readonly log4net.ILog logger = Logger.GetLogger();

        public URDFExporter Exporter;
        public bool AutoUpdatingForm;
        public AttributeDef saveConfigurationAttributeDef;

        private ISldWorks swApp;
        private ModelDoc2 ActiveSWModel;
        private StringBuilder NewNodeMap = new StringBuilder(128);
        private LinkNode previouslySelectedNode;
        private Control[] jointBoxes;
        private Control[] linkBoxes;
        private LinkNode BaseNode;

        public AssemblyExportForm(ISldWorks iSwApp, LinkNode node)
        {
            Application.ThreadException +=
                new ThreadExceptionEventHandler(ExceptionHandler);
            AppDomain.CurrentDomain.UnhandledException +=
                new UnhandledExceptionEventHandler(UnhandledException);
            InitializeComponent();
            swApp = iSwApp;
            BaseNode = node;
            ActiveSWModel = swApp.ActiveDoc;
            Exporter = new URDFExporter(iSwApp);
            AutoUpdatingForm = false;

            jointBoxes = new Control[] {
                textBoxJointName, comboBoxAxis, comboBoxJointType,
                textBoxAxisX, textBoxAxisY, textBoxAxisZ,
                textBoxJointX, textBoxJointY, textBoxJointZ,
                textBoxJointPitch, textBoxJointRoll, textBoxJointYaw,
                textBoxLimitLower, textBoxLimitUpper, textBoxLimitEffort, textBoxLimitVelocity,
                textBoxDamping, textBoxFriction,
                textBoxCalibrationFalling, textBoxCalibrationRising,
                textBoxSoftLower, textBoxSoftUpper, textBoxKPosition, textBoxKVelocity
            };
            linkBoxes = new Control[] {
                textBoxInertialOriginX, textBoxInertialOriginY, textBoxInertialOriginZ,
                textBoxInertialOriginRoll, textBoxInertialOriginPitch, textBoxInertialOriginYaw,
                textBoxVisualOriginX, textBoxVisualOriginY, textBoxVisualOriginZ,
                textBoxVisualOriginRoll, textBoxVisualOriginPitch, textBoxVisualOriginYaw,
                textBoxIxx, textBoxIxy, textBoxIxz, textBoxIyy, textBoxIyz, textBoxIzz,
                textBoxMass,
                domainUpDownRed, domainUpDownGreen, domainUpDownBlue, domainUpDownAlpha,
                comboBoxMaterials,
                textBoxTexture
            };

            saveConfigurationAttributeDef = iSwApp.DefineAttribute("URDF Export Configuration");
            int Options = 0;

            saveConfigurationAttributeDef.AddParameter(
                "data", (int)swParamType_e.swParamTypeString, 0, Options);
            saveConfigurationAttributeDef.AddParameter(
                "name", (int)swParamType_e.swParamTypeString, 0, Options);
            saveConfigurationAttributeDef.AddParameter(
                "date", (int)swParamType_e.swParamTypeString, 0, Options);
            saveConfigurationAttributeDef.AddParameter(
                "exporterVersion", (int)swParamType_e.swParamTypeDouble, 1.0, Options);
            saveConfigurationAttributeDef.Register();
        }

        private void ExceptionHandler(object sender, ThreadExceptionEventArgs e)
        {
            logger.Error("Exception encountered in Assembly export form", e.Exception);
            MessageBox.Show("There was a problem with the export form: \n\"" +
                e.Exception.Message + "\"\nEmail your maintainer with the log file found at " +
                Logger.GetFileName());
        }

        private void UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = (Exception)e.ExceptionObject;
            logger.Error("Unhandled exception in Assembly Export form", ex);
            MessageBox.Show("There was a problem with the export form: \n\"" +
                ex.Message + "\"\nEmail your maintainer with the log file found at " +
                Logger.GetFileName());
        }

        //Joint form configuration controls
        private void AssemblyExportFormLoad(object sender, EventArgs e)
        {
            FillJointTree();
        }

        private void ButtonJointNextClick(object sender, EventArgs e)
        {
            if (!(previouslySelectedNode == null || previouslySelectedNode.Link.Joint == null))
            {
                SaveJointDataFromPropertyBoxes(previouslySelectedNode.Link.Joint);
            }
            previouslySelectedNode = null; // Need to clear this for the link properties page

            while (treeViewJointTree.Nodes.Count > 0)
            {
                LinkNode node = (LinkNode)treeViewJointTree.Nodes[0];
                treeViewJointTree.Nodes.Remove(node);
                BaseNode.Nodes.Add(node);
            }
            ChangeAllNodeFont(BaseNode, new Font(treeViewJointTree.Font, FontStyle.Regular));

            FillLinkTree();
            panelLinkProperties.Visible = true;
            Focus();
        }

        private void Button_Joint_Cancel_Click(object sender, EventArgs e)
        {
            if (previouslySelectedNode != null)
            {
                SaveJointDataFromPropertyBoxes(previouslySelectedNode.Link.Joint);
            }
            while (treeViewJointTree.Nodes.Count > 0)
            {
                LinkNode node = (LinkNode)treeViewJointTree.Nodes[0];
                treeViewJointTree.Nodes.Remove(node);
                BaseNode.Nodes.Add(node);
            }
            SaveConfigTree(ActiveSWModel, BaseNode, true);
            Close();
        }

        private void ButtonLinksCancelClick(object sender, EventArgs e)
        {
            if (previouslySelectedNode != null)
            {
                SaveLinkDataFromPropertyBoxes(previouslySelectedNode.Link);
            }
            SaveConfigTree(ActiveSWModel, BaseNode, true);
            Close();
        }

        private void ButtonLinksPreviousClick(object sender, EventArgs e)
        {
            LinkNode node = (LinkNode)treeViewLinkProperties.SelectedNode;
            if (node != null)
            {
                SaveLinkDataFromPropertyBoxes(node.Link);
            }
            previouslySelectedNode = null;
            ChangeAllNodeFont(BaseNode, new Font(treeViewJointTree.Font, FontStyle.Regular));
            FillJointTree();
            panelLinkProperties.Visible = false;
        }

        private void ButtonLinksFinishClick(object sender, EventArgs e)
        {
            FinishExport(true);
        }

        private void ButtonLinksExportUrdfOnlyClick(object sender, EventArgs e)
        {
            FinishExport(false);
        }

        private void FinishExport(bool exportSTL)
        {
            logger.Info("Completing URDF export");
            SaveConfigTree(ActiveSWModel, BaseNode, false);
            SaveFileDialog saveFileDialog1 = new SaveFileDialog
            {
                RestoreDirectory = true,
                InitialDirectory = Exporter.SavePath,
                FileName = Exporter.PackageName
            };

            if (saveFileDialog1.ShowDialog() == DialogResult.OK)
            {
                Exporter.SavePath = Path.GetDirectoryName(saveFileDialog1.FileName);
                Exporter.PackageName = Path.GetFileName(saveFileDialog1.FileName);
                LinkNode node = (LinkNode)treeViewLinkProperties.SelectedNode;
                if (node != null)
                {
                    SaveLinkDataFromPropertyBoxes(node.Link);
                }
                Exporter.URDFRobot = CreateRobotFromTreeView(treeViewLinkProperties);

                Exporter.ExportRobot(exportSTL);
                Close();
            }
        }

        private void TreeViewLinkPropertiesAfterSelect(object sender, TreeViewEventArgs e)
        {
            Font fontRegular = new Font(treeViewJointTree.Font, FontStyle.Regular);
            Font fontBold = new Font(treeViewJointTree.Font, FontStyle.Bold);
            if (previouslySelectedNode != null)
            {
                SaveLinkDataFromPropertyBoxes(previouslySelectedNode.Link);
                previouslySelectedNode.NodeFont = fontRegular;
            }
            LinkNode node = (LinkNode)e.Node;
            node.NodeFont = fontBold;
            node.Text = node.Text;
            ActiveSWModel.ClearSelection2(true);
            SelectionMgr manager = ActiveSWModel.SelectionManager;

            SelectData data = manager.CreateSelectData();
            data.Mark = -1;
            if (node.Link.SWComponent != null)
            {
                node.Link.SWComponent.Select4(false, data, false);
            }
            else
            {
                foreach (Component2 component in node.Link.SWcomponents)
                {
                    component.Select4(true, data, false);
                }
            }
            FillLinkPropertyBoxes(node.Link);
            treeViewLinkProperties.Focus();
            previouslySelectedNode = node;
        }

        #region Link Properties Controls Handlers

        private void ButtonTextureBrowseClick(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog1 = new OpenFileDialog
            {
                RestoreDirectory = true
            };

            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                textBoxTexture.Text = openFileDialog1.FileName;
            }
        }

        #endregion Link Properties Controls Handlers

        #region Joint Properties Controls Handlers

        private void TreeViewJointtreeAfterSelect(object sender, TreeViewEventArgs e)
        {
            Font fontRegular = new Font(treeViewJointTree.Font, FontStyle.Regular);
            Font fontBold = new Font(treeViewJointTree.Font, FontStyle.Bold);
            if (previouslySelectedNode != null && !previouslySelectedNode.IsBaseNode)
            {
                SaveJointDataFromPropertyBoxes(previouslySelectedNode.Link.Joint);
            }
            if (previouslySelectedNode != null)
            {
                previouslySelectedNode.NodeFont = fontRegular;
            }
            LinkNode node = (LinkNode)e.Node;
            ActiveSWModel.ClearSelection2(true);
            SelectionMgr manager = ActiveSWModel.SelectionManager;

            SelectData data = manager.CreateSelectData();
            data.Mark = -1;
            if (node.Link.SWComponent != null)
            {
                node.Link.SWComponent.Select4(false, data, false);
            }
            else
            {
                foreach (Component2 component in node.Link.SWcomponents)
                {
                    component.Select4(true, data, false);
                }
            }
            node.NodeFont = fontBold;
            node.Text = node.Text;
            FillJointPropertyBoxes(node.Link.Joint);
            previouslySelectedNode = node;
        }

        private void ComboBoxAxisSelectedIndexChanged(object sender, EventArgs e)
        {
            if (!AutoUpdatingForm)
            {
                if (!(String.IsNullOrWhiteSpace(comboBoxOrigin.Text) ||
                    String.IsNullOrWhiteSpace(comboBoxAxis.Text)))
                {
                    double[] Axis = Exporter.EstimateAxis(comboBoxAxis.Text);
                    Axis = Exporter.LocalizeAxis(Axis, comboBoxOrigin.Text);
                    textBoxAxisX.Text = Axis[0].ToString("G5");
                    textBoxAxisY.Text = Axis[1].ToString("G5");
                    textBoxAxisZ.Text = Axis[2].ToString("G5");
                }
            }
        }

        #endregion Joint Properties Controls Handlers
    }
}