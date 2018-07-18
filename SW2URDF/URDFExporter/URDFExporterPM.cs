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
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SolidWorks.Interop.swpublished;

namespace SW2URDF
{
    [ComVisible(true)]
    [Serializable]
    public sealed partial class URDFExporterPM : PropertyManagerPage2Handler9
    {
        #region class variables

        private static readonly log4net.ILog logger = Logger.GetLogger();
        public SldWorks swApp;
        public ModelDoc2 ActiveSWModel;

        public URDFExporter Exporter;
        public LinkNode previouslySelectedNode;
        public Link previouslySelectedLink;
        public List<Link> linksToVisit;
        public LinkNode rightClickedNode;
        private ContextMenuStrip docMenu;

        //General objects required for the PropertyManager page

        private PropertyManagerPage2 PMPage;
        private PropertyManagerPageGroup PMGroup;
        private PropertyManagerPageSelectionbox PMSelection;
        private PropertyManagerPageButton PMButtonExport;
        private PropertyManagerPageTextbox PMTextBoxLinkName;
        private PropertyManagerPageTextbox PMTextBoxJointName;
        private PropertyManagerPageNumberbox PMNumberBoxChildCount;
        private PropertyManagerPageCombobox PMComboBoxGlobalCoordsys;
        private PropertyManagerPageCombobox PMComboBoxAxes;
        private PropertyManagerPageCombobox PMComboBoxCoordSys;
        private PropertyManagerPageCombobox PMComboBoxJointType;

        private PropertyManagerPageLabel PMLabelLinkName;
        private PropertyManagerPageLabel PMLabelJointName;
        private PropertyManagerPageLabel PMLabelSelection;
        private PropertyManagerPageLabel PMLabelChildCount;
        private PropertyManagerPageLabel PMLabelParentLink;
        private PropertyManagerPageLabel PMLabelParentLinkLabel;
        private PropertyManagerPageLabel PMLabelAxes;
        private PropertyManagerPageLabel PMLabelCoordSys;
        private PropertyManagerPageLabel PMLabelJointType;
        private PropertyManagerPageLabel PMLabelGlobalCoordsys;

        private PropertyManagerPageWindowFromHandle PMTree;

        public TreeView Tree
        { get; set; }

        private bool automaticallySwitched = false;

        //Each object in the page needs a unique ID

        private const int GroupID = 1;
        private const int TextBoxLinkNameID = 2;
        private const int SelectionID = 3;
        private const int ComboID = 4;
        private const int ListID = 5;
        private const int ButtonSaveID = 6;
        private const int NumBoxChildCountID = 7;
        private const int LabelLinkNameID = 8;
        private const int LabelSelectionID = 9;
        private const int LabelChildCountID = 10;
        private const int LabelParentLinkID = 11;
        private const int LabelParentLinkLabelID = 12;
        private const int TextBoxJointNameID = 13;
        private const int LabelJointNameID = 14;
        private const int dotNetTree = 16;
        private const int ButtonExportID = 17;
        private const int ComboBoxAxesID = 18;
        private const int ComboBoxCoordSysID = 19;
        private const int LabelAxesID = 20;
        private const int LabelCoordSysID = 21;
        private const int ComboBoxJointTypeID = 22;
        private const int LabelJointTypeID = 23;
        private const int IDGlobalCoordsys = 24;
        private const int IDLabelGlobalCoordsys = 25;

        #endregion class variables

        public void Show()
        {
            PMPage.Show2(0);
        }

        //The following runs when a new instance of the class is created
        public URDFExporterPM(SldWorks swAppPtr)
        {
            swApp = swAppPtr;
            ActiveSWModel = swApp.ActiveDoc;
            Exporter = new URDFExporter(swApp);
            Exporter.URDFRobot = new Robot();
            Exporter.URDFRobot.Name = ActiveSWModel.GetTitle();

            linksToVisit = new List<Link>();
            docMenu = new ContextMenuStrip();

            string PageTitle = null;
            string caption = null;
            string tip = null;
            long options = 0;
            int longerrors = 0;
            int controlType = 0;
            int alignment = 0;
            string[] listItems = new string[4];

            ActiveSWModel.ShowConfiguration2("URDF Export");

            #region Create and instantiate components of PM page

            //Set the variables for the page
            PageTitle = "URDF Exporter";
            options = (int)swPropertyManagerPageOptions_e.swPropertyManagerOptions_OkayButton +
                (int)swPropertyManagerPageOptions_e.swPropertyManagerOptions_CancelButton +
                (int)swPropertyManagerPageOptions_e.swPropertyManagerOptions_HandleKeystrokes;

            //Create the PropertyManager page
            PMPage = (PropertyManagerPage2)swApp.CreatePropertyManagerPage(
                PageTitle, (int)options, this, ref longerrors);

            //Make sure that the page was created properly
            if (longerrors == (int)swPropertyManagerPageStatus_e.swPropertyManagerPage_Okay)
            {
                SetupPropertyManagerPage(ref caption, ref tip, ref options,
                    ref controlType, ref alignment);
            }
            else
            {
                //If the page is not created
                logger.Error("An error occurred while attempting to create the PropertyManager Page\nError: " + longerrors);
                MessageBox.Show("There was a problem setting up the property manager: " +
                    "\nEmail your maintainer with the log file found at " + Logger.GetFileName());
            }

            #endregion Create and instantiate components of PM page
        }

        private void ExceptionHandler(object sender, ThreadExceptionEventArgs e)
        {
            logger.Warn("Exception encountered in URDF configuration form\n" +
                "Email your maintainer with the log file found at " + Logger.GetFileName(),
                e.Exception);
        }

        private void UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            logger.Error("Unhandled exception in URDF configuration form\n" +
                "Email your maintainer with the log file found at " + Logger.GetFileName(),
                (Exception)e.ExceptionObject);
        }

        #region Implemented Property Manager Page Handler Methods

        void IPropertyManagerPage2Handler9.AfterActivation()
        {
            //Turns the selection box blue so that selected components are added to the PMPage
            // selection box
            PMSelection.SetSelectionFocus();
        }

        private void OnButtonPress(int Id)
        {
            if (Id == ButtonExportID) //If the export button was pressed
            {
                SaveActiveNode();
                if (CheckIfNamesAreUnique((LinkNode)Tree.Nodes[0]) && CheckNodesComplete(Tree)) // Only if everything is A-OK, then do we proceed.
                {
                    PMPage.Close(true); //It saves automatically when sending Okay as true;
                    AssemblyDoc assy = (AssemblyDoc)ActiveSWModel;

                    //This call can be a real sink of processing time if the model is large.
                    //Unfortunately there isn't a way around it I believe.
                    int result = assy.ResolveAllLightWeightComponents(true);

                    // If the user confirms to resolve the components and they are successfully
                    // resolved we can continue
                    if (result == (int)swComponentResolveStatus_e.swResolveOk)
                    {
                        // Builds the links and joints from the PMPage configuration
                        LinkNode BaseNode = (LinkNode)Tree.Nodes[0];
                        automaticallySwitched = true;
                        Tree.Nodes.Remove(BaseNode);

                        Exporter.CreateRobotFromTreeView(BaseNode);
                        AssemblyExportForm exportForm = new AssemblyExportForm(swApp, BaseNode);
                        exportForm.Exporter = Exporter;
                        exportForm.Show();
                    }
                    else if (result == (int)swComponentResolveStatus_e.swResolveError ||
                        result == (int)swComponentResolveStatus_e.swResolveNotPerformed)
                    {
                        logger.Warn("Resolving components failed. Warning user to do so on their own");
                        MessageBox.Show("Resolving components failed. In order to export to URDF, " +
                            " this tool needs all components to be resolved. Try resolving " +
                            "lightweight components manually before attempting to export again");
                    }
                    else if (result == (int)swComponentResolveStatus_e.swResolveAbortedByUser)
                    {
                        logger.Warn("Components were not resolved by user");
                        MessageBox.Show("In order to export to URDF, this tool needs all " +
                            "components to be resolved. You can resolve them manually or try " +
                            "exporting again");
                    }
                }
            }
        }

        // Called when a PropertyManagerPageButton is pressed. In our case, that's only the
        // export button for now
        void IPropertyManagerPage2Handler9.OnButtonPress(int Id)
        {
            try
            {
                OnButtonPress(Id);
            }
            catch (Exception e)
            {
                logger.Error("Exception caught handling button press " + Id, e);
                MessageBox.Show("There was a problem with the configuration property manager: \n\"" +
                    e.Message + "\"\nEmail your maintainer with the log file found at " + Logger.GetFileName());
            }
        }

        void IPropertyManagerPage2Handler9.OnClose(int Reason)
        {
            try
            {
                if (Reason ==
                    (int)swPropertyManagerPageCloseReasons_e.swPropertyManagerPageClose_Cancel)
                {
                    logger.Info("Configuration canceled");
                    SaveActiveNode();
                }
                else if (Reason ==
                    (int)swPropertyManagerPageCloseReasons_e.swPropertyManagerPageClose_Okay)
                {
                    logger.Info("Configuration saved");
                    SaveActiveNode();
                    SaveConfigTreeXML(ActiveSWModel, (LinkNode)Tree.Nodes[0], false);
                }
            }
            catch (Exception e)
            {
                logger.Error("Exception caught on close ", e);
                MessageBox.Show("There was a problem closing the property manager: \n\"" +
                    e.Message + "\"\nEmail your maintainer with the log file found at " + Logger.GetFileName());
            }
        }

        void IPropertyManagerPage2Handler9.OnGainedFocus(int Id)
        {
        }

        bool IPropertyManagerPage2Handler9.OnHelp()
        {
            return true;
        }

        bool IPropertyManagerPage2Handler9.OnKeystroke(int Wparam, int Message, int Lparam, int Id)
        {
            if (Wparam == (int)Keys.Enter)
            {
                return true;
            }
            return false;
        }

        void IPropertyManagerPage2Handler9.OnLostFocus(int Id)
        {
            Debug.Print("Control box " + Id + " has lost focus");
        }

        void IPropertyManagerPage2Handler9.OnNumberboxChanged(int Id, double Value)
        {
            if (Id == NumBoxChildCountID)
            {
                LinkNode node = (LinkNode)Tree.SelectedNode;
                CreateNewNodes(node);
            }
        }

        void IPropertyManagerPage2Handler9.OnSelectionboxFocusChanged(int Id)
        {
            Debug.Print("The focus has moved to selection box " + Id);
        }

        void IPropertyManagerPage2Handler9.OnSelectionboxListChanged(int Id, int Count)
        {
            // Move focus to next selection box if right-mouse button pressed
            PMPage.SetCursor((int)swPropertyManagerPageCursors_e.swPropertyManagerPageCursors_Advance);
        }

        bool IPropertyManagerPage2Handler9.OnSubmitSelection(
            int Id, object Selection, int SelType, ref string ItemText)
        {
            // This method must return true for selections to occur
            return true;
        }

        void IPropertyManagerPage2Handler9.OnTextboxChanged(int Id, string Text)
        {
            if (Id == TextBoxLinkNameID)
            {
                LinkNode node = (LinkNode)Tree.SelectedNode;
                node.Text = PMTextBoxLinkName.Text;
                node.Name = PMTextBoxLinkName.Text;
            }
        }

        int IPropertyManagerPage2Handler9.OnWindowFromHandleControlCreated(int Id, bool Status)
        {
            return 0;
        }

        #endregion Implemented Property Manager Page Handler Methods

        #region TreeView handler methods

        // Upon selection of a node, the node displayed on the PMPage is saved and the
        // selected one is then set
        private void TreeAfterSelect(object sender, TreeViewEventArgs e)
        {
            try
            {
                if (!automaticallySwitched && e.Node != null)
                {
                    SwitchActiveNodes((LinkNode)e.Node);
                }
                automaticallySwitched = false;
            }
            catch (Exception ex)
            {
                logger.Error("Exception caught on tree view AfterSelect ", ex);
                MessageBox.Show("There was a problem with the property manager: \n\"" +
                    ex.Message + "\"\nEmail your maintainer with the log file found at " +
                    Logger.GetFileName());
            }
        }

        // Captures which node was right clicked
        private void TreeNodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            rightClickedNode = (LinkNode)e.Node;
        }

        //When a keyboard key is pressed on the tree
        private void TreeKeyDown(object sender, KeyEventArgs e)
        {
            if (rightClickedNode.IsEditing)
            {
                if (e.KeyCode == Keys.Enter)
                {
                    rightClickedNode.EndEdit(false);
                }
                else if (e.KeyCode == Keys.Escape)
                {
                    rightClickedNode.EndEdit(true);
                }
            }
        }

        // The callback for the configuration page context menu 'Add Child' option
        private void AddChildClick(object sender, EventArgs e)
        {
            try
            {
                CreateNewNodes(rightClickedNode, 1);
            }
            catch (Exception ex)
            {
                logger.Error("Exception caught on tree view add child ", ex);
                MessageBox.Show("There was a problem with the property manager: \n\"" +
                    ex.Message + "\"\nEmail your maintainer with the log file found at " +
                    Logger.GetFileName());
            }
        }

        // The callback for the configuration page context menu 'Remove Child' option
        private void RemoveChildClick(object sender, EventArgs e)
        {
            try
            {
                LinkNode parent = (LinkNode)rightClickedNode.Parent;
                parent.Nodes.Remove(rightClickedNode);
            }
            catch (Exception ex)
            {
                logger.Error("Exception caught on tree view remove child ", ex);
                MessageBox.Show("There was a problem with the property manager: \n\"" +
                    ex.Message + "\"\nEmail your maintainer with the log file found at " +
                    Logger.GetFileName());
            }
        }

        // The callback for the configuration page context menu 'Rename Child' option
        // This isn't really working right now, so the option was deactivated from the
        // context menu
        private void RenameChildClick(object sender, EventArgs e)
        {
            try
            {
                Tree.SelectedNode = rightClickedNode;
                Tree.LabelEdit = true;
                rightClickedNode.BeginEdit();
                PMPage.SetFocus(dotNetTree);
            }
            catch (Exception ex)
            {
                logger.Error("Exception caught on tree view rename child ", ex);
                MessageBox.Show("There was a problem with the property manager: \n\"" +
                    ex.Message + "\"\nEmail your maintainer with the log file found at " +
                    Logger.GetFileName());
            }
        }

        private void TreeItemDrag(object sender, ItemDragEventArgs e)
        {
            try
            {
                Tree.DoDragDrop(e.Item, DragDropEffects.Move);
            }
            catch (Exception ex)
            {
                logger.Error("Exception caught on tree view Drag ", ex);
                MessageBox.Show("There was a problem with the property manager: \n\"" +
                    ex.Message + "\"\nEmail your maintainer with the log file found at " +
                    Logger.GetFileName());
            }
        }

        private void TreeDragOver(object sender, DragEventArgs e)
        {
            try
            {
                // Retrieve the client coordinates of the mouse position.
                Point targetPoint = Tree.PointToClient(new Point(e.X, e.Y));

                // Select the node at the mouse position.
                Tree.SelectedNode = Tree.GetNodeAt(targetPoint);
                e.Effect = DragDropEffects.Move;
            }
            catch (Exception ex)
            {
                logger.Error("Exception caught on tree view Drag Over ", ex);
                MessageBox.Show("There was a problem with the property manager: \n\"" +
                    ex.Message + "\"\nEmail your maintainer with the log file found at " +
                    Logger.GetFileName());
            }
        }

        private void TreeDragEnter(object sender, DragEventArgs e)
        {
            try
            {
                // Retrieve the client coordinates of the mouse position.
                Point targetPoint = Tree.PointToClient(new Point(e.X, e.Y));

                // Select the node at the mouse position.
                Tree.SelectedNode = Tree.GetNodeAt(targetPoint);
                e.Effect = DragDropEffects.Move;
            }
            catch (Exception ex)
            {
                logger.Error("Exception caught on tree view DragEnter ", ex);
                MessageBox.Show("There was a problem with the property manager: \n\"" +
                    ex.Message + "\"\nEmail your maintainer with the log file found at " +
                    Logger.GetFileName());
            }
        }

        private void DoDragDrop(DragEventArgs e)
        {
            // Retrieve the client coordinates of the drop location.
            Point targetPoint = Tree.PointToClient(new Point(e.X, e.Y));

            // Retrieve the node at the drop location.
            LinkNode targetNode = (LinkNode)Tree.GetNodeAt(targetPoint);

            LinkNode draggedNode;
            // Retrieve the node that was dragged.

            //Retrieve the node/item that was dragged
            if ((LinkNode)e.Data.GetData(typeof(LinkNode)) != null)
            {
                draggedNode = (LinkNode)e.Data.GetData(typeof(LinkNode));
            }
            else
            {
                return;
            }

            // If the node is picked up and dragged back on to itself, please don't crash
            if (draggedNode == targetNode)
            {
                return;
            }

            // If the it was dropped into the box itself, but not onto an actual node
            if (targetNode == null)
            {
                // If for some reason the tree is empty
                if (Tree.Nodes.Count == 0)
                {
                    draggedNode.Remove();
                    Tree.Nodes.Add(draggedNode);
                    Tree.ExpandAll();
                    return;
                }
                else
                {
                    targetNode = (LinkNode)Tree.TopNode;
                    draggedNode.Remove();
                    targetNode.Nodes.Add(draggedNode);
                    targetNode.ExpandAll();
                    return;
                }
            }
            else
            {
                // If dragging a node closer onto its ancestor do parent swapping kungfu
                if (draggedNode.Level < targetNode.Level)
                {
                    int levelDiff = targetNode.Level - draggedNode.Level;
                    LinkNode ancestorNode = targetNode;
                    for (int i = 0; i < levelDiff; i++)
                    {
                        //Ascend up the target's node (new parent) parent tree the level
                        // difference to get the ancestoral node that is at the same level
                        // as the dragged node (the new child)
                        ancestorNode = (LinkNode)ancestorNode.Parent;
                    }
                    // If the dragged node is in the same line as the target node, then the real
                    // kungfu begins
                    if (ancestorNode == draggedNode)
                    {
                        LinkNode newParent = targetNode;
                        LinkNode newChild = draggedNode;
                        LinkNode sameGrandparent = (LinkNode)draggedNode.Parent;
                        newParent.Remove(); // Remove the target node from the tree
                        newChild.Remove();  // Remove the dragged node from the tree
                        newParent.Nodes.Add(newChild); //
                        if (sameGrandparent == null)
                        {
                            Tree.Nodes.Add(newParent);
                        }
                        else
                        {
                            sameGrandparent.Nodes.Add(newParent);
                        }
                    }
                }
                draggedNode.Remove();
                targetNode.Nodes.Add(draggedNode);
                targetNode.ExpandAll();
            }
        }

        private void TreeDragDrop(object sender, DragEventArgs e)
        {
            try
            {
                DoDragDrop(e);
            }
            catch (Exception ex)
            {
                logger.Error("Exception caught on tree view Drag Drop ", ex);
                MessageBox.Show("There was a problem with the property manager: \n\"" +
                    ex.Message + "\"\nEmail your maintainer with the log file found at " +
                    Logger.GetFileName());
            }
        }

        #endregion TreeView handler methods

        //A method that sets up the Property Manager Page
        private void SetupPropertyManagerPage(ref string caption, ref string tip,
            ref long options, ref int controlType, ref int alignment)
        {
            //Begin adding the controls to the page
            //Create the group box
            caption = "Configure and Organize Links";
            options = (int)swAddGroupBoxOptions_e.swGroupBoxOptions_Visible +
                (int)swAddGroupBoxOptions_e.swGroupBoxOptions_Expanded;
            PMGroup = (PropertyManagerPageGroup)PMPage.AddGroupBox(GroupID, caption, (int)options);

            //Create the parent link label (static)
            controlType = (int)swPropertyManagerPageControlType_e.swControlType_Label;
            caption = "Parent Link";
            alignment = (int)swPropertyManagerPageControlLeftAlign_e.swControlAlign_LeftEdge;
            options = (int)swAddControlOptions_e.swControlOptions_Visible +
                (int)swAddControlOptions_e.swControlOptions_Enabled;
            PMLabelParentLinkLabel = (PropertyManagerPageLabel)PMGroup.AddControl(
                LabelLinkNameID, (short)controlType, caption, (short)alignment, (int)options, "");

            //Create the parent link name label, the one that is updated
            controlType = (int)swPropertyManagerPageControlType_e.swControlType_Label;
            caption = "";
            alignment = (int)swPropertyManagerPageControlLeftAlign_e.swControlAlign_Indent;
            options = (int)swAddControlOptions_e.swControlOptions_Visible + (int)swAddControlOptions_e.swControlOptions_Enabled;
            PMLabelParentLink = (PropertyManagerPageLabel)PMGroup.AddControl(
                LabelLinkNameID, (short)controlType, caption, (short)alignment, (int)options, "");

            //Create the link name text box label
            controlType = (int)swPropertyManagerPageControlType_e.swControlType_Label;
            caption = "Link Name";
            tip = "Enter the name of the link";
            alignment = (int)swPropertyManagerPageControlLeftAlign_e.swControlAlign_LeftEdge;
            options = (int)swAddControlOptions_e.swControlOptions_Visible +
                (int)swAddControlOptions_e.swControlOptions_Enabled;
            PMLabelLinkName = (PropertyManagerPageLabel)PMGroup.AddControl(
                LabelLinkNameID, (short)controlType, caption, (short)alignment, (int)options, tip);

            //Create the link name text box
            controlType = (int)swPropertyManagerPageControlType_e.swControlType_Textbox;
            caption = "base_link";
            alignment = (int)swPropertyManagerPageControlLeftAlign_e.swControlAlign_Indent;
            tip = "Enter the name of the link";
            options = (int)swAddControlOptions_e.swControlOptions_Visible +
                (int)swAddControlOptions_e.swControlOptions_Enabled;
            PMTextBoxLinkName = (PropertyManagerPageTextbox)PMGroup.AddControl(
                TextBoxLinkNameID, (short)(controlType), caption, (short)alignment, (int)options, tip);

            //Create the joint name text box label
            controlType = (int)swPropertyManagerPageControlType_e.swControlType_Label;
            caption = "Joint Name";
            tip = "Enter the name of the joint";
            alignment = (int)swPropertyManagerPageControlLeftAlign_e.swControlAlign_LeftEdge;
            options = (int)swAddControlOptions_e.swControlOptions_Visible;
            PMLabelJointName = (PropertyManagerPageLabel)PMGroup.AddControl(
                LabelJointNameID, (short)controlType, caption, (short)alignment, (int)options, tip);

            //Create the joint name text box
            controlType = (int)swPropertyManagerPageControlType_e.swControlType_Textbox;
            caption = "";
            alignment = (int)swPropertyManagerPageControlLeftAlign_e.swControlAlign_Indent;
            tip = "Enter the name of the joint";
            options = (int)swAddControlOptions_e.swControlOptions_Visible;
            PMTextBoxJointName = (PropertyManagerPageTextbox)PMGroup.AddControl(
                TextBoxLinkNameID, (short)(controlType), caption, (short)alignment, (int)options, tip);

            //Create the global origin coordinate sys label
            controlType = (int)swPropertyManagerPageControlType_e.swControlType_Label;
            caption = "Global Origin Coordinate System";
            tip = "Select the reference coordinate system for the global origin";
            alignment = (int)swPropertyManagerPageControlLeftAlign_e.swControlAlign_LeftEdge;
            options = (int)swAddControlOptions_e.swControlOptions_Visible;
            PMLabelGlobalCoordsys = (PropertyManagerPageLabel)PMGroup.AddControl(
                IDLabelGlobalCoordsys, (short)controlType, caption, (short)alignment, (int)options, tip);

            // Create pull down menu for Coordinate systems
            controlType = (int)swPropertyManagerPageControlType_e.swControlType_Combobox;
            caption = "Global Origin Coordinate System Name";
            tip = "Select the reference coordinate system for the global origin";
            alignment = (int)swPropertyManagerPageControlLeftAlign_e.swControlAlign_Indent;
            options = (int)swAddControlOptions_e.swControlOptions_Visible;
            PMComboBoxGlobalCoordsys = (PropertyManagerPageCombobox)PMGroup.AddControl(
                IDGlobalCoordsys, (short)controlType, caption, (short)alignment, (int)options, tip);
            PMComboBoxGlobalCoordsys.Style =
                (int)swPropMgrPageComboBoxStyle_e.swPropMgrPageComboBoxStyle_EditBoxReadOnly;

            //Create the ref coordinate sys label
            controlType = (int)swPropertyManagerPageControlType_e.swControlType_Label;
            caption = "Reference Coordinate System";
            tip = "Select the reference coordinate system for the joint origin";
            alignment = (int)swPropertyManagerPageControlLeftAlign_e.swControlAlign_LeftEdge;
            options = 0;
            PMLabelCoordSys = (PropertyManagerPageLabel)PMGroup.AddControl(
                LabelCoordSysID, (short)controlType, caption, (short)alignment, (int)options, tip);

            // Create pull down menu for Coordinate systems
            controlType = (int)swPropertyManagerPageControlType_e.swControlType_Combobox;
            caption = "Reference Coordinate System Name";
            tip = "Select the reference coordinate system for the joint origin";
            alignment = (int)swPropertyManagerPageControlLeftAlign_e.swControlAlign_Indent;
            options = 0;
            PMComboBoxCoordSys = (PropertyManagerPageCombobox)PMGroup.AddControl(
                ComboBoxCoordSysID, (short)controlType, caption, (short)alignment, (int)options, tip);
            PMComboBoxCoordSys.Style =
                (int)swPropMgrPageComboBoxStyle_e.swPropMgrPageComboBoxStyle_EditBoxReadOnly;

            //Create the ref axis label
            controlType = (int)swPropertyManagerPageControlType_e.swControlType_Label;
            caption = "Reference Axis";
            tip = "Select the reference axis for the joint";
            alignment = (int)swPropertyManagerPageControlLeftAlign_e.swControlAlign_LeftEdge;
            options = (int)swAddControlOptions_e.swControlOptions_Visible;
            PMLabelAxes = (PropertyManagerPageLabel)PMGroup.AddControl(
                LabelAxesID, (short)controlType, caption, (short)alignment, (int)options, tip);

            // Create pull down menu for axes
            controlType = (int)swPropertyManagerPageControlType_e.swControlType_Combobox;
            caption = "Reference Axis Name";
            tip = "Select the reference axis for the joint";
            alignment = (int)swPropertyManagerPageControlLeftAlign_e.swControlAlign_Indent;
            options = (int)swAddControlOptions_e.swControlOptions_Visible;
            PMComboBoxAxes = (PropertyManagerPageCombobox)PMGroup.AddControl(
                ComboBoxCoordSysID, (short)controlType, caption, (short)alignment, (int)options, tip);
            PMComboBoxAxes.Style =
                (int)swPropMgrPageComboBoxStyle_e.swPropMgrPageComboBoxStyle_EditBoxReadOnly;

            //Create the joint type label
            controlType = (int)swPropertyManagerPageControlType_e.swControlType_Label;
            caption = "Joint Type";
            tip = "Select the joint type";
            alignment = (int)swPropertyManagerPageControlLeftAlign_e.swControlAlign_LeftEdge;
            options = (int)swAddControlOptions_e.swControlOptions_Visible;
            PMLabelJointType = (PropertyManagerPageLabel)PMGroup.AddControl(
                LabelAxesID, (short)controlType, caption, (short)alignment, (int)options, tip);

            // Create pull down menu for joint type
            controlType = (int)swPropertyManagerPageControlType_e.swControlType_Combobox;
            caption = "Joint type";
            tip = "Select the joint type";
            alignment = (int)swPropertyManagerPageControlLeftAlign_e.swControlAlign_Indent;
            options = (int)swAddControlOptions_e.swControlOptions_Visible;
            PMComboBoxJointType = (PropertyManagerPageCombobox)PMGroup.AddControl(
                ComboBoxCoordSysID, (short)controlType, caption, (short)alignment, (int)options, tip);
            PMComboBoxJointType.Style =
                (int)swPropMgrPageComboBoxStyle_e.swPropMgrPageComboBoxStyle_EditBoxReadOnly;
            PMComboBoxJointType.AddItems(new string[] {
                "Automatically Detect", "continuous", "revolute", "prismatic", "fixed" });

            //Create the selection box label
            controlType = (int)swPropertyManagerPageControlType_e.swControlType_Label;
            caption = "Link Components";
            tip = "Select components associated with this link";
            alignment = (int)swPropertyManagerPageControlLeftAlign_e.swControlAlign_LeftEdge;
            options = (int)swAddControlOptions_e.swControlOptions_Visible +
                (int)swAddControlOptions_e.swControlOptions_Enabled;
            PMLabelSelection = (PropertyManagerPageLabel)PMGroup.AddControl(
                LabelLinkNameID, (short)controlType, caption, (short)alignment, (int)options, tip);

            //Create selection box
            controlType = (int)swPropertyManagerPageControlType_e.swControlType_Selectionbox;
            caption = "Link Components";
            alignment = (int)swPropertyManagerPageControlLeftAlign_e.swControlAlign_Indent;
            options = (int)swAddControlOptions_e.swControlOptions_Visible + (int)swAddControlOptions_e.swControlOptions_Enabled;
            tip = "Select components associated with this link";
            PMSelection = (PropertyManagerPageSelectionbox)PMGroup.AddControl(
                SelectionID, (short)controlType, caption, (short)alignment, (int)options, tip);

            swSelectType_e[] filters = new swSelectType_e[1];
            filters[0] = swSelectType_e.swSelCOMPONENTS;
            object filterObj = null;
            filterObj = filters;

            PMSelection.AllowSelectInMultipleBoxes = true;
            PMSelection.SingleEntityOnly = false;
            PMSelection.AllowMultipleSelectOfSameEntity = false;
            PMSelection.Height = 50;
            PMSelection.SetSelectionFilters(filterObj);

            //Create the number box label
            //Create the link name text box label
            controlType = (int)swPropertyManagerPageControlType_e.swControlType_Label;
            caption = "Number of child links";
            tip = "Enter the number of child links and they will be automatically added";
            alignment = (int)swPropertyManagerPageControlLeftAlign_e.swControlAlign_LeftEdge;
            options = (int)swAddControlOptions_e.swControlOptions_Visible +
                (int)swAddControlOptions_e.swControlOptions_Enabled;
            PMLabelChildCount = (PropertyManagerPageLabel)PMGroup.AddControl
                (LabelLinkNameID, (short)controlType, caption, (short)alignment, (int)options, tip);

            //Create the number box
            controlType = (int)swPropertyManagerPageControlType_e.swControlType_Numberbox;
            caption = "";
            alignment = (int)swPropertyManagerPageControlLeftAlign_e.swControlAlign_Indent;
            tip = "Enter the number of child links and they will be automatically added";
            options = (int)swAddControlOptions_e.swControlOptions_Enabled +
                (int)swAddControlOptions_e.swControlOptions_Visible;
            PMNumberBoxChildCount = PMGroup.AddControl(
                NumBoxChildCountID, (short)controlType, caption, (short)alignment, (int)options, tip);
            PMNumberBoxChildCount.SetRange2(
                (int)swNumberboxUnitType_e.swNumberBox_UnitlessInteger, 0, int.MaxValue, true, 1, 1, 1);
            PMNumberBoxChildCount.Value = 0;

            PMButtonExport = PMGroup.AddControl(ButtonExportID,
                (short)swPropertyManagerPageControlType_e.swControlType_Button,
                "Preview and Export...", 0, (int)options, "");

            controlType = (int)swPropertyManagerPageControlType_e.swControlType_WindowFromHandle;
            caption = "Link Tree";
            alignment = (int)swPropertyManagerPageControlLeftAlign_e.swControlAlign_LeftEdge;
            options = (int)swAddControlOptions_e.swControlOptions_Visible +
                (int)swAddControlOptions_e.swControlOptions_Enabled;
            PMTree = PMPage.AddControl(dotNetTree,
                (short)swPropertyManagerPageControlType_e.swControlType_WindowFromHandle, caption, 0, (int)options, "");
            PMTree.Height = 163;
            Tree = new TreeView
            {
                Height = 163,
                Visible = true
            };

            Tree.AfterSelect += new TreeViewEventHandler(TreeAfterSelect);
            Tree.NodeMouseClick += new TreeNodeMouseClickEventHandler(TreeNodeMouseClick);
            Tree.KeyDown += new KeyEventHandler(TreeKeyDown);
            Tree.DragDrop += new DragEventHandler(TreeDragDrop);
            Tree.DragOver += new DragEventHandler(TreeDragOver);
            Tree.DragEnter += new DragEventHandler(TreeDragEnter);
            Tree.ItemDrag += new ItemDragEventHandler(TreeItemDrag);
            Tree.AllowDrop = true;
            PMTree.SetWindowHandlex64(Tree.Handle.ToInt64());

            ToolStripMenuItem addChild = new ToolStripMenuItem();
            ToolStripMenuItem removeChild = new ToolStripMenuItem();
            //ToolStripMenuItem renameChild = new ToolStripMenuItem();
            addChild.Text = "Add Child Link";
            addChild.Click += new EventHandler(AddChildClick);

            removeChild.Text = "Remove";
            removeChild.Click += new EventHandler(RemoveChildClick);
            //renameChild.Text = "Rename";
            //renameChild.Click += new System.EventHandler(this.renameChild_Click);
            //docMenu.Items.AddRange(new ToolStripMenuItem[] { addChild, removeChild, renameChild });
            docMenu.Items.AddRange(new ToolStripMenuItem[] { addChild, removeChild });
            LinkNode node = CreateEmptyNode(null);
            node.ContextMenuStrip = docMenu;
            Tree.Nodes.Add(node);
            Tree.SelectedNode = Tree.Nodes[0];
            PMSelection.SetSelectionFocus();
            PMPage.SetFocus(dotNetTree);
            //updateNodeNames(tree);
        }

        #region Not implemented handler methods

        // These methods are still active. The exceptions that are thrown only cause the debugger
        // to pause. Comment out the exception if you choose not to implement it, but it gets
        // regularly called anyway
        void IPropertyManagerPage2Handler9.OnCheckboxCheck(int Id, bool Checked)
        {
            logger.Info("OnCheckboxCheck called. This method no longer throws an Exception. " +
                " It just silently does nothing. Ok, except for this logging message");
        }

        void IPropertyManagerPage2Handler9.OnComboboxEditChanged(int Id, string Text)
        {
            logger.Info("OnComboboxEditChanged called. This method no longer throws an Exception." +
                " It just silently does nothing. Ok, except for this logging message");
        }

        void IPropertyManagerPage2Handler9.OnComboboxSelectionChanged(int Id, int Item)
        {
            logger.Info("OnComboboxSelectionChanged called. This method no longer throws an " +
                "Exception. It just silently does nothing. Ok, except for this logging message");
        }

        void IPropertyManagerPage2Handler9.OnGroupCheck(int Id, bool Checked)
        {
            logger.Info("OnGroupCheck called. This method no longer throws an Exception. It just " +
                "silently does nothing. Ok, except for this logging message");
        }

        void IPropertyManagerPage2Handler9.OnGroupExpand(int Id, bool Expanded)
        {
            logger.Info("OnGroupExpand called. This method no longer throws an Exception. It just " +
                "silently does nothing. Ok, except for this logging message");
        }

        void IPropertyManagerPage2Handler9.OnListboxSelectionChanged(int Id, int Item)
        {
            logger.Info("OnListboxSelectionChanged called. This method no longer throws an " +
                "Exception. It just silently does nothing. Ok, except for this logging message");
        }

        bool IPropertyManagerPage2Handler9.OnNextPage()
        {
            logger.Info("OnNextPage called. This method no longer throws an Exception. It just " + "" +
                "silently does nothing. Ok, except for this logging message");
            return true;
        }

        void IPropertyManagerPage2Handler9.OnOptionCheck(int Id)
        {
            logger.Info("OnOptionCheck called. This method no longer throws an Exception. " +
                "It just silently does nothing. Ok, except for this logging message");
        }

        void IPropertyManagerPage2Handler9.OnPopupMenuItem(int Id)
        {
            logger.Info("OnPopupMenuItem called. This method no longer throws an Exception. " +
                "It just silently does nothing. Ok, except for this logging message");
        }

        void IPropertyManagerPage2Handler9.OnPopupMenuItemUpdate(int Id, ref int retval)
        {
            logger.Info("OnPopupMenuItemUpdate called. This method no longer throws an Exception. " +
                "It just silently does nothing. Ok, except for this logging message");
        }

        bool IPropertyManagerPage2Handler9.OnPreview()
        {
            logger.Info("OnPreview called. This method no longer throws an Exception. " +
                "It just silently does nothing. Ok, except for this logging message");
            return true;
        }

        bool IPropertyManagerPage2Handler9.OnPreviousPage()
        {
            logger.Info("OnPreviousPage called. This method no longer throws an Exception. " +
                "It just silently does nothing. Ok, except for this logging message");
            return true;
        }

        void IPropertyManagerPage2Handler9.OnRedo()
        {
            logger.Info("OnRedo called. This method no longer throws an Exception. " +
                "It just silently does nothing. Ok, except for this logging message");
        }

        void IPropertyManagerPage2Handler9.OnSelectionboxCalloutCreated(int Id)
        {
            logger.Info("OnSelectionboxCalloutCreated called. This method no longer throws " +
                " an Exception. It just silently does nothing. Ok, except for this logging message");
        }

        void IPropertyManagerPage2Handler9.OnSelectionboxCalloutDestroyed(int Id)
        {
            logger.Info("OnSelectionboxCalloutDestroyed called. This method no longer throws " +
                "an Exception. It just silently does nothing. Ok, except for this logging message");
        }

        void IPropertyManagerPage2Handler9.OnSliderPositionChanged(int Id, double Value)
        {
            logger.Info("OnSliderPositionChanged called. This method no longer throws an " +
                "Exception. It just silently does nothing. Ok, except for this logging message");
        }

        void IPropertyManagerPage2Handler9.OnSliderTrackingCompleted(int Id, double Value)
        {
            logger.Info("OnSliderTrackingCompleted called. This method no longer throws an " +
                "Exception. It just silently does nothing. Ok, except for this logging message");
        }

        bool IPropertyManagerPage2Handler9.OnTabClicked(int Id)
        {
            logger.Info("OnTabClicked called. This method no longer throws an Exception. It " +
                " just silently does nothing. Ok, except for this logging message");
            return true;
        }

        void IPropertyManagerPage2Handler9.OnUndo()
        {
            logger.Info("OnUndo called. This method no longer throws an Exception. It just " +
                "silently does nothing. Ok, except for this logging message");
        }

        void IPropertyManagerPage2Handler9.OnWhatsNew()
        {
            logger.Info("OnWhatsNew called. This method no longer throws an Exception. It just " +
                " silently does nothing. Ok, except for this logging message");
        }

        void IPropertyManagerPage2Handler9.OnListboxRMBUp(int Id, int PosX, int PosY)
        {
            logger.Info("OnListboxRMBUp called. This method no longer throws an Exception. It " +
                " just silently does nothing. Ok, except for this logging message");
        }

        void IPropertyManagerPage2Handler9.OnNumberBoxTrackingCompleted(int Id, double Value)
        {
            logger.Info("OnNumberBoxTrackingCompleted called. This method no longer throws an " +
                "Exception. It just silently does nothing. Ok, except for this logging message");
        }

        void IPropertyManagerPage2Handler9.AfterClose()
        {
            logger.Info("AfterClose called. This method no longer throws an Exception. It just " +
                "silently does nothing. Ok, except for this logging message");
        }

        int IPropertyManagerPage2Handler9.OnActiveXControlCreated(int Id, bool Status)
        {
            logger.Info("OnActiveXControlCreated called. This method no longer throws an " +
                "Exception. It just silently does nothing. Ok, except for this logging message");
            return 0;
        }

        #endregion Not implemented handler methods
    }
}