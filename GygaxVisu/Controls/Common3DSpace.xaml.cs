﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media.Media3D;
using GygaxCore;
using GygaxCore.Ifc;
using GygaxCore.Interfaces;
using GygaxVisu.Helpers;
using HelixToolkit.Wpf.SharpDX;
using NLog;
using SharpDX;
using Xceed.Wpf.Toolkit.Core.Converters;
using Color = SharpDX.Color;

namespace GygaxVisu.Controls
{
    /// <summary>
    /// Interaction logic for Common3DSpace.xaml
    /// </summary>
    public partial class Common3DSpace : UserControl
    {
        public ObservableCollection<IStreamable> Items
        {
            get; set;
        }

        public PhongMaterial Material = PhongMaterials.Red;

        public Common3DSpace()
        {
            InitializeComponent();

            Viewport.RenderTechniquesManager = new DefaultRenderTechniquesManager();
            Viewport.RenderTechnique = Viewport.RenderTechniquesManager.RenderTechniques[DefaultRenderTechniqueNames.Blinn];
            Viewport.EffectsManager = new DefaultEffectsManager(Viewport.RenderTechniquesManager);

            SetLight();

            SetBinding(DataContextProperty, new Binding());

            Viewport.MouseDoubleClick += ViewportOnMouseDoubleClick;

            DatastreamTree.SelectedItemChanged += DatastreamTreeOnSelectedItemChanged;
        }

        private void DatastreamTreeOnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> routedPropertyChangedEventArgs)
        {
            try
            {
                if (routedPropertyChangedEventArgs.OldValue != null)
                ((IfcMeshGeometryModel3D)((TreeViewItem)routedPropertyChangedEventArgs.OldValue).DataContext).Material = originalMaterialOfSelectedItem;
                
                var element =
                    (IfcMeshGeometryModel3D) ((TreeViewItem) routedPropertyChangedEventArgs.NewValue).DataContext;

                element.IsSelected = true;
                originalMaterialOfSelectedItem = (PhongMaterial) element.Material;

                element.Material = PhongMaterials.Blue;
            }
            catch (Exception)
            {
            }
        }

        private void ViewportOnMouseDoubleClick(object sender, MouseButtonEventArgs mouseButtonEventArgs)
        {
            var x = Viewport.CurrentPosition;
        }

        private void SetLight()
        {
            Viewport.Items.Add(new DirectionalLight3D {Direction = new SharpDX.Vector3(1, 1, 1)});
            Viewport.Items.Add(new DirectionalLight3D { Direction = new SharpDX.Vector3(-1, -1, -1) });
        }
        
        public void UpdateView()
        {
            if (Visibility != Visibility.Visible)
                return;

            foreach (var streamable in Items)
            {
                if (streamable.Name == null || streamable.Name == "")
                    continue;

                var foundOne = false;

                //Check if datastream is already in the tree
                foreach (var item in DatastreamTree.Items)
                {
                    if (((TreeViewItem)item).Header == null || ((TreeViewItem) item).Header.Equals(streamable.Name))
                    {
                        foundOne = true;
                        break;
                    }
                }

                if (foundOne) continue;

                var treeItem = new TreeViewItem()
                {
                    Header = streamable.Name
                };

                DatastreamTree.Items.Add(treeItem);

                //var contextItem = new MenuItem()
                //{
                //    Header = streamable.Name
                //};

                //ContextMenuLayers.Items.Add(contextItem);

                var elements = Visualizer.Visualizer.GetModels(streamable.Data);

                if (elements == null) continue;
                
                foreach (var model in elements)
                {
                    if (Viewport.RenderHost.RenderTechnique != null)
                    {
                        model.Attach(Viewport.RenderHost);
                    }
                    
                    var subItem = new TreeViewItem()
                    {
                        Header = model.Name,
                        DataContext = model
                    };

                    try
                    {
                        var ifcTree = ((IfcMeshGeometryModel3D) model).IfcTreeNode;
                        addSubtree(ref subItem,ifcTree);
                    }
                    catch(Exception)
                    { }

                    treeItem.Items.Add(subItem);
                    
                    Viewport.Items.Add(model);
                }
            }

            ViewportHelper.ZoomExtents(Viewport);
        }

        private void addSubtree(ref TreeViewItem viewTree, TreeNode<TreeElement> ifcTree)
        {
            foreach (var child in ifcTree.Children)
            {
                var t = new TreeViewItem()
                {
                    Header = child.Value.Key + " " + child.Value.Value + " " + child.Value.GlobalId
                };

                addSubtree(ref t, child);

                viewTree.Items.Add(t);
            }
        }

        public void ItemsOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs notifyCollectionChangedEventArgs)
        {
            UpdateView();
        }

        public static readonly DependencyProperty DataContextProperty = DependencyProperty.Register(
            "DataContext",
            typeof(Object),
            typeof(Common3DSpace),
            new PropertyMetadata(DataContextChanged)
        );

        private static void DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            Common3DSpace myControl = (Common3DSpace)sender;
            myControl.Items = (e.NewValue as ViewModel).Items;

            if (myControl.Items != null)
            {
                myControl.Items.CollectionChanged += myControl.ItemsOnCollectionChanged;
            }
        }

        public void ViewModelOnClearWorkspace(object sender, EventArgs eventArgs)
        {
            Viewport.Items.Clear();
            DatastreamTree.Items.Clear();
        }

        private PhongMaterial originalMaterialOfSelectedItem;
        
        private void Viewport_OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            var hits = Viewport.FindHits(e.GetPosition(this));

            if (hits.Count == 0) return;

            var txt = hits[0].PointHit.X + "," + hits[0].PointHit.Y + "," + hits[0].PointHit.Z;
            LogManager.GetCurrentClassLogger().Info("Hit at " + txt);

            try
            {
                var element =
                    (IfcMeshGeometryModel3D)
                    hits.OrderBy(q => q.Distance).Select(r => r.ModelHit).First(s => s.Visibility == Visibility.Visible);
                
                var treeItem = getTreeItem(element, DatastreamTree.Items);
                treeItem.IsSelected = true;
                treeItem.IsExpanded = true;
            }
            catch (Exception)
            {
            }
        }

        private TreeViewItem getTreeItem(IfcMeshGeometryModel3D element, ItemCollection items)
        {
            foreach (TreeViewItem item in items)
            {
                if (item.DataContext.Equals(element))
                    return item;

                if (item.Items.Count <= 0) continue;

                var ti = getTreeItem(element, item.Items);
                if (ti != null)
                    return ti;
            }

            return null;
        }
    }

    

    public class MyVisibilityConverterToBool : IValueConverter
    {
        public object Convert(object value, Type targetType, 
            object parameter, CultureInfo culture)
        {
            try
            {
                Visibility visibilityToConvert = (Visibility)value;
                if (visibilityToConvert == Visibility.Visible)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch
            {
                return false;
            }
        }

        public object ConvertBack(object value, Type targetType,
            object parameter, CultureInfo culture)
        {
            try
            {
                bool boolToConvert = (bool)value;
                if (boolToConvert)
                {
                    return Visibility.Visible;
                }
                else
                {
                    return Visibility.Hidden;
                }
            }
            catch
            {
                return Visibility.Visible;
            }


        }
    }
}
