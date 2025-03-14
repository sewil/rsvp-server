using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using WvsBeta.Common;
using WzTools.Extra;
using WzTools.FileSystem;
using WzTools.Helpers;
using WzTools.Helpers.Helpers;
using WzTools.Objects;
using Int8 = System.SByte;
using UInt8 = System.Byte;

namespace WvsBeta.Launcher
{
    public partial class DataEdit : Form
    {
        BindingList<DataGridLine> _dataGridData = new();
        List<DataGridLine> _deletedGridData = new();

        public DataEdit()
        {
            InitializeComponent();
            dataGridView1.DataSource = _dataGridData;

            PcomObject.RegisterObjectType<WzCanvas>();
        }

        NodeData? SelectedNodeData => tvImg.SelectedNode?.Tag as NodeData;

        private void toolStripMenuItem1_Click(object sender, EventArgs e)
        {

        }

        private void DataEdit_Load(object sender, EventArgs e)
        {
            AddImgs(tvImg.Nodes.Add("DataSvr"), new DirectoryInfo(Program.DataSvr));

            AddImgs(tvImg.Nodes.Add("AppData MapleGlobal"), new DirectoryInfo(Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MapleGlobal")));

        }

        void AddImgs(TreeNode node, DirectoryInfo dirInfo)
        {
            node.Tag = dirInfo;

            foreach (var directoryInfo in dirInfo.GetDirectories())
            {
                AddImgs(node.Nodes.Add(directoryInfo.Name), directoryInfo);
            }

            foreach (var fileInfo in dirInfo.GetFiles("*.img"))
            {
                var tn = node.Nodes.Add(fileInfo.Name);
                tn.Tag = fileInfo;
            }

            if (node.Nodes.Count == 0)
            {
                // Useless node, remove
                node.Remove();
            }
        }

        /*
         * For a given TreeNode, try to load its subnodes.
         * In case of an IMG, load the IMG as property.
         * 
         */
        bool LoadSubnodes(TreeNode node)
        {
            if (node.Nodes.Count != 0) return false;

            if (node.Tag is FileInfo fileInfo)
            {
                if (fileInfo.Extension != ".img")
                {
                    Debug.WriteLine("File no IMG {0}", fileInfo);

                    return false;
                }

                Debug.WriteLine("Loading IMG {0}", fileInfo);
                node.Tag = new NodeData()
                {
                    Node = node,
                    Modified = false,
                    File = new FSFile(fileInfo.FullName),
                    Property = null,
                };
            }

            if (node.Tag is NodeData nd && nd.Property == null)
            {
                Debug.WriteLine("Loading file nodes");
                try
                {
                    nd.Property = nd.File.Object as WzProperty;

                    AddWzProp(node, nd.File);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("This file is not a valid .img!");
                    node.Tag = null;
                    return false;
                }
            }

            return true;
        }

        void AddWzProp(TreeNode node, IEnumerable<WzProperty> subProps)
        {
            foreach (var item in subProps)
            {
                var tn = node.Nodes.Add(item.Name);
                tn.Tag = new NodeData()
                {
                    Node = tn,
                    Modified = false,
                    Property = item,
                    File = null,
                };


                AddWzProp(tn, item.PropertyChildren);
            }
        }

        class NodeData
        {
            public TreeNode Node { get; set; }
            public bool Modified { get; set; }
            public WzProperty? Property { get; set; }
            public FSFile? File { get; set; }

            public Dictionary<string, object?> Modifications { get; set; } = new();

            public HashSet<object> InheritedModifications { get; set; } = new();

            public void UpdateNodeState()
            {
                var markerPos = Node.Text.IndexOf(ModifiedMarker);

                if (InheritedModifications.Count == 0 && Modifications.Count == 0)
                {
                    Modified = false;

                    if (markerPos > 0)
                    {
                        Node.Text = Node.Text.Remove(markerPos);
                    }
                }
                else
                {
                    Modified = true;

                    if (markerPos == -1)
                    {
                        Node.Text += ModifiedMarker;
                    }
                }
            }

        }
        public void UpdateModifiedState()
        {
            var nd = SelectedNodeData;
            if (nd == null) return;

            var modified = false;

            foreach (var item in _dataGridData)
            {
                if (item.New || item.OriginalValue != item.Value)
                {
                    modified = true;
                    break;
                }
            }

            if (_deletedGridData.Count > 0)
                modified = true;

            Debug.WriteLine("Node {0} modified: {1}", nd.Node.Text, modified);
        }

        public void StoreModificationsInNodeData()
        {
            var nd = SelectedNodeData;
            if (nd == null) return;

            var prop = nd.Property;
            if (prop == null) return;

            var mods = nd.Modifications ??= new Dictionary<string, object?>();
            mods.Clear();

            foreach (var item in _deletedGridData)
            {
                mods.Add(item.Name, null);
            }

            foreach (var item in _dataGridData)
            {
                if (item.Changed)
                {
                    mods.Add(item.Name, item.Value);
                }
            }

            if (mods.Count > 0)
            {
                MarkIMGModified();
            }
            else
            {
                MarkIMGUnmodified();
            }

            nd.UpdateNodeState();

            UpdateMenus();
        }

        void DisplayNodeInfo(NodeData nd)
        {
            _dataGridData.Clear();
            _deletedGridData.Clear();

            var property = nd.Property;
            if (property == null) return;


            if (property is WzCanvas canvas)
            {
                _dataGridData.Add(new DataGridLineImage(canvas));
                pictureBox1.Image = canvas.GetImage();
            }
            else
            {
                pictureBox1.Image = null;
            }

            foreach (var kvp in property)
            {
                if (kvp.Value is PcomObject || kvp.Value is byte[]) continue;

                var name = kvp.Key;

                object changedValue = kvp.Value;
                if (nd.Modifications?.TryGetValue(name, out var tmp) ?? false)
                {
                    changedValue = tmp;
                    if (changedValue == null)
                    {
                        // Value was deleted... or maybe null?
                        continue;
                    }
                }

                var dgl = new DataGridLine(name, kvp.Value, changedValue);

                _dataGridData.Add(dgl);
            }

            foreach (var item in nd.Modifications.Where(x => !property.Keys.Contains(x.Key)))
            {
                var dgl = new DataGridLine(item.Key, "", item.Value, isNew: true);
                _dataGridData.Add(dgl);
            }
        }

        class DataGridLineImage : DataGridLine
        {
            private readonly WzCanvas _canvas = null;

            public int Width => _canvas?.Width ?? 0;
            public int Height => _canvas?.Height ?? 0;

            public DataGridLineImage() : base() { }

            public DataGridLineImage(WzCanvas canvas, bool isNew = false) : base("[image]", canvas, canvas, isNew)
            {
                _canvas = canvas;
            }
        }

        class DataGridLine : INotifyPropertyChanged
        {
            public string Name { get; set; }
            private object? _value;
            public object? Value
            {
                get => _value;
                set
                {
                    SetValue(value);
                }
            }

            [DisplayName("Original Value")]
            [ReadOnly(true)]
            public object OriginalValue { get; init; }

            public string Type => Value?.GetType().Name ?? "[unknown]";

            public bool New { get; init; }

            public bool Changed
            {
                get
                {
                    if (OriginalValue is string of && Value is string cf) return !of.Equals(cf);
                    return OriginalValue?.ToString() != Value?.ToString();
                }
            }

            public event PropertyChangedEventHandler? PropertyChanged;

            public DataGridLine()
            {
                New = true;
            }

            public DataGridLine(string name, object originalValue, object changedValue, bool isNew = false)
            {
                Name = name;
                _value = changedValue;
                OriginalValue = originalValue;
                New = isNew;
            }

            private void SetValue(object? value)
            {
                var currentValue = _value;
                var resultValue = value;

                if (currentValue == null || resultValue == null)
                {
                }
                else if (currentValue.GetType().Equals(resultValue.GetType()))
                {
                    // same type, no conversion needed.
                }
                else if (value is string s)
                {
                    switch (currentValue)
                    {
                        case Int8 x:
                            if (!Int8.TryParse(s, out x)) goto fail;
                            resultValue = x;
                            break;

                        case UInt8 x:
                            if (!UInt8.TryParse(s, out x)) goto fail;
                            resultValue = x;
                            break;


                        case Int16 x:
                            if (!Int16.TryParse(s, out x)) goto fail;
                            resultValue = x;
                            break;
                        case UInt16 x:
                            if (!UInt16.TryParse(s, out x)) goto fail;
                            resultValue = x;
                            break;


                        case Int32 x:
                            if (!Int32.TryParse(s, out x)) goto fail;
                            resultValue = x;
                            break;
                        case UInt32 x:
                            if (!UInt32.TryParse(s, out x)) goto fail;
                            resultValue = x;
                            break;


                        case Int64 x:
                            if (!Int64.TryParse(s, out x)) goto fail;
                            resultValue = x;
                            break;
                        case UInt64 x:
                            if (!UInt64.TryParse(s, out x)) goto fail;
                            resultValue = x;
                            break;


                        case Single x:
                            if (!Single.TryParse(s, out x)) goto fail;
                            resultValue = x;
                            break;
                        case Double x:
                            if (!Double.TryParse(s, out x)) goto fail;
                            resultValue = x;
                            break;

                        case string x:
                            resultValue = x;
                            break;

                        case bool x:
                            if (!bool.TryParse(s, out x)) goto fail;
                            resultValue = x;
                            break;

                        case DateTime x:
                            if (!DateTime.TryParse(s, out x)) goto fail;
                            resultValue = x;
                            break;

                        default:
                            MessageBox.Show($"Unsupported value to cast to: {currentValue.GetType().Name}");
                            return;
                    }
                }


                accept_type_conversion:
                if (New)
                {
                    // See if we can interpret it as an int32, which is the savest bet for internal data.
                    if (resultValue is string x && Int32.TryParse(x, out var y)) resultValue = y;
                }

                _value = resultValue;

                return;
                fail:
                if (New) goto accept_type_conversion;
                MessageBox.Show($"Unable to convert '{value}' to '{currentValue.GetType()}'");
            }
        }

        private void tvImg_BeforeExpand(object sender, TreeViewCancelEventArgs e)
        {
        }

        private void tvImg_BeforeSelect(object sender, TreeViewCancelEventArgs e)
        {
            StoreModificationsInNodeData();

            var node = e.Node;

            // Auto expand if this node was just loaded
            if (LoadSubnodes(node))
            {
                node.Expand();
            }
        }

        private void tvImg_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (e.Node.Tag is NodeData nd)
            {
                DisplayNodeInfo(nd);
            }

            UpdateMenus();
        }

        private void dataGridView1_UserDeletedRow(object sender, DataGridViewRowEventArgs e)
        {
        }

        private void dataGridView1_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            Debug.WriteLine($"Cell editing of row {e.RowIndex}, col {e.ColumnIndex} finished.");

            StoreModificationsInNodeData();
        }

        private void cmsTreeView_Opening(object sender, CancelEventArgs e)
        {
            var nd = SelectedNodeData;
            if (nd == null)
            {
                e.Cancel = true;
                return;
            }

            UpdateMenus();
        }

        private void UpdateMenus()
        {
            Debug.WriteLine($"Updating menus");
            var nd = SelectedNodeData;

            nd?.UpdateNodeState();

            var isProperty = nd != null && nd.Property != null && nd.File == null;

            var isEditing = dataGridView1.IsCurrentCellInEditMode;

            addWzPropertyAfterToolStripMenuItem.Visible = isProperty && false;
            addWzPropertyBeforeToolStripMenuItem.Visible = isProperty && false;
            addWzPropertyInsideToolStripMenuItem.Visible = isProperty && false;
            deleteWzPropertyToolStripMenuItem.Visible = isProperty && !isEditing;

            saveIMGToolStripMenuItem1.Enabled = nd != null && nd.Property != null && !isEditing && nd.Modified;
        }

        private void deleteWzPropertyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var nd = SelectedNodeData;
            if (nd == null || nd.Property == null) return;

            var pnd = nd.Node.Parent.Tag as NodeData;
            if (pnd == null || pnd.Property == null) return;

            pnd.Modifications.Add(nd.Property.Name, null);

            // Delete in Property
            pnd.Property.Remove(nd.Property.Name);

            // Remove from tree
            nd.Node.Remove();

            MarkIMGModified();
        }

        static readonly string ModifiedMarker = " [modified]";
        void MarkIMGModified(NodeData nd = null)
        {
            IterateUntilIMG(nd ?? SelectedNodeData, (nd, rootModificationNode) =>
            {
                nd.InheritedModifications.Add(rootModificationNode);

                nd.UpdateNodeState();
            });
        }

        NodeData? GetIMGNode(NodeData nd)
        {
            if (nd == null) return null;
            if (nd.File != null) return nd;

            do
            {
                nd = nd.Node.Parent?.Tag as NodeData;
            }
            while (nd != null && nd.File == null);

            if (nd == null || nd.File == null) return null;
            return nd;
        }

        void IterateUntilIMG(NodeData? nd, Action<NodeData, NodeData> action)
        {
            if (nd == null) return;
            var rootModificationNode = nd;

            bool first = true;
            do
            {
                action(nd, rootModificationNode);
                nd = nd.Node.Parent?.Tag as NodeData;
            }
            while (nd != null && nd.Property != null);
        }

        void MarkIMGUnmodified(NodeData? nd = null)
        {
            IterateUntilIMG(nd ?? SelectedNodeData, (nd, rootModificationNode) =>
            {
                nd.InheritedModifications.Remove(rootModificationNode);

                nd.UpdateNodeState();
            });
        }

        private void saveIMGToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var nd = GetIMGNode(SelectedNodeData);
            if (nd == null) return;

            // Apply all mods
            foreach (var item in nd.InheritedModifications)
            {
                if (item is NodeData ind)
                {
                    foreach (var kvp in ind.Modifications)
                    {
                        if (kvp.Value == null)
                        {
                            ind.Property.Remove(kvp.Key);
                        }
                        else
                        {
                            ind.Property.Set(kvp.Key, kvp.Value);
                        }
                    }
                    ind.Modifications.Clear();
                    MarkIMGUnmodified(ind);
                }
            }

            using var sfd = new SaveFileDialog();
            sfd.FileName = nd.File.RealPath;
            sfd.InitialDirectory = Path.GetDirectoryName(sfd.FileName);
            sfd.Filter = "IMG file|*.img";

            if (sfd.ShowDialog() != DialogResult.OK) return;

            using var writer = sfd.OpenFile();
            using var archiveWriter = new ArchiveWriter(writer);
            PcomObject.WriteToBlob(archiveWriter, nd.File.Object);
        }

        private void dataGridView1_UserDeletingRow(object sender, DataGridViewRowCancelEventArgs e)
        {
            Debug.WriteLine($"Deleting {e.Row}");
            var dgl = e.Row.DataBoundItem as DataGridLine;
            if (dgl == null || dgl.New)
            {
                return;
            }

            _deletedGridData.Add(dgl);
        }

        private void addWzPropertyToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }

        private void addWzPropertyInsideToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var nd = SelectedNodeData;
            if (nd == null) return;


        }

        private void dataGridView1_CellBeginEdit(object sender, DataGridViewCellCancelEventArgs e)
        {
            Debug.WriteLine($"Editing row {e.RowIndex}, col {e.ColumnIndex}");

            var dgl = dataGridView1.Rows[e.RowIndex].DataBoundItem as DataGridLine;
            if (e.ColumnIndex == 0)
            {
                // Only on new rows it should be editable
                if (dgl != null && !dgl.New)
                {
                    // Do not allow editing the name
                    e.Cancel = true;
                    return;
                }
            }
            else if (dgl is DataGridLineImage dgli)
            {
                e.Cancel = true;
                var canvas = dgli.Value as WzCanvas;
                var imageEdit = new ImageEdit(canvas);
                if (imageEdit.ShowDialog() == DialogResult.Yes)
                {
                    canvas.ChangeImage(imageEdit.NewBitmap, (WzPixFormat)imageEdit.NewFormat);
                }
            }

            UpdateMenus();
        }

        private void dataGridView1_CellToolTipTextNeeded(object sender, DataGridViewCellToolTipTextNeededEventArgs e)
        {
            Debug.WriteLine($"Getting tooltip for {e.RowIndex}, col {e.ColumnIndex}");


        }
    }
}
