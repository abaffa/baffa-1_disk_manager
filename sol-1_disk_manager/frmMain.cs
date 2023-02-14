using RawDiskLib;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Security.Principal;
using System.Globalization;

namespace sol_1_disk_manager
{
    public partial class frmMain : Form
    {
        //attributes (1)	|_|_|file_type(3bits)|x|w|r| types: file, directory, character device
        const byte ATTR_R = 0b00000001;
        const byte ATTR_W = 0b00000010;
        const byte ATTR_X = 0b00000100;


        const byte ATTR_DIR = 0b00001000;
        const byte ATTR_FILE = 0b000010000;
        const byte ATTR_DEV = 0b000100000;

        private ListViewColumnSorter lvwColumnSorter;

        private String current_filename = "";
        private bool isRenaming = false;
        private DiskImage diskImage = new DiskImage();
        


        private Dictionary<string, byte[]> clipboard = new Dictionary<string, byte[]>();

        // memory card
        DiskInfo selDisk;
        int selectedVol = -1;
        int selectedUser = -1;
        long ClustersToRead = 100;
        //

        public frmMain()
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;

            InitializeComponent();

            contextMenuStrip1_Opening(null, null);

            openFileToolStripMenuItem.Enabled = false;
            deleteToolStripMenuItem1.Enabled = false;

            IniFile ini = new IniFile(System.Environment.CurrentDirectory + "\\" + "config.ini");

            listView1.Columns.Add("Title");
            listView1.Columns.Add("Attributes");
            listView1.Columns.Add("Size");
            listView1.Columns.Add("Creation Date");
            listView1.Columns.Add("Type");
            listView1.Columns.Add("_Size");
            listView1.Columns.Add("_Start");

            lvwColumnSorter = new ListViewColumnSorter();
            this.listView1.ListViewItemSorter = lvwColumnSorter;

            try
            {
                listView1.Columns[0].Width = int.Parse(ini.IniReadValue("general", "col_0"));
                listView1.Columns[1].Width = int.Parse(ini.IniReadValue("general", "col_1"));
                listView1.Columns[2].Width = int.Parse(ini.IniReadValue("general", "col_2"));
                listView1.Columns[3].Width = int.Parse(ini.IniReadValue("general", "col_3"));
                listView1.Columns[4].Width = int.Parse(ini.IniReadValue("general", "col_4"));

                lvwColumnSorter.SortColumn = int.Parse(ini.IniReadValue("general", "sort_column"));
                lvwColumnSorter.Order = (SortOrder)int.Parse(ini.IniReadValue("general", "column_order"));

                refresh_sort_dir();

            }
            catch { }

            bool isElevated;
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                isElevated = principal.IsInRole(WindowsBuiltInRole.Administrator);
            }


            readDiskToolStripMenuItem.Enabled = isElevated;
            writeDiskToolStripMenuItem.Enabled = isElevated;
        }



        void cmd_ls()
        {
            listView1.SelectedItems.Clear();
            listView1.Items.Clear();

            List<FileEntry> fileEntryList = diskImage.cmd_ls();

            if (diskImage.parentStack.Count > 0)
            {
                ListViewItem lvi = new ListViewItem();
                lvi.ImageIndex = 0;
                lvi.Name = "Title";
                lvi.Text = "..";

                lvi.Tag = "..";

                lvi.SubItems.Add(new ListViewItem.ListViewSubItem() { Name = "Attributes", Text = "" });
                lvi.SubItems.Add(new ListViewItem.ListViewSubItem() { Name = "Size", Text = "0 B" });
                lvi.SubItems.Add(new ListViewItem.ListViewSubItem() { Name = "Creation Date", Text = "" });
                lvi.SubItems.Add(new ListViewItem.ListViewSubItem() { Name = "Type", Text = "Directory" });
                lvi.SubItems.Add(new ListViewItem.ListViewSubItem() { Name = "_Size", Text = "0" });
                lvi.SubItems.Add(new ListViewItem.ListViewSubItem() { Name = "_Start", Text = "-1" });

                listView1.Items.Add(lvi);
            }

            int itemCount = 0;

            foreach (FileEntry f in fileEntryList)
            {
                int num = f._size;
                String size = "";
                String type = "";
                String attributes = "";

                if (num > 1000)
                    size = (num / 1000.0).ToString("N3") + " KB";
                else
                    size = num.ToString("N0") + " B";

                ListViewItem lvi = new ListViewItem();
                lvi.ImageIndex = 2;
                lvi.Text = f._name.Trim();
                lvi.Tag = f._name;


                if ((f._attributes & ATTR_DIR) != 0x00)
                {
                    attributes += "d";
                    type = "Directory";
                }
                else
                {
                    attributes += "-";
                    type = "File";
                }


                if ((f._attributes & ATTR_R) != 0x00)
                    attributes += "r";
                else
                    attributes += "-";

                if ((f._attributes & ATTR_W) != 0x00)
                    attributes += "w";
                else
                    attributes += "-";

                if ((f._attributes & ATTR_X) != 0x00)
                    attributes += "x";
                else
                    attributes += "-";

                String creationDate = Convert.ToUInt16(f._creation_date[0]).ToString("X2");
                creationDate += "/";
                creationDate += Convert.ToUInt16(f._creation_date[1]).ToString("X2");
                creationDate += "/";
                try
                {
                    creationDate += int.Parse(Convert.ToUInt16(f._creation_date[2]).ToString("X2")) + 2000;
                }
                catch { }

                lvi.ImageIndex = (f._attributes & ATTR_DIR) != 0x00 ? 0 : 2;

                lvi.SubItems.Add(new ListViewItem.ListViewSubItem() { Name = "Attributes", Text = attributes });
                lvi.SubItems.Add(new ListViewItem.ListViewSubItem() { Name = "Size", Text = size });
                lvi.SubItems.Add(new ListViewItem.ListViewSubItem() { Name = "Creation Date", Text = creationDate });
                lvi.SubItems.Add(new ListViewItem.ListViewSubItem() { Name = "Type", Text = type });
                lvi.SubItems.Add(new ListViewItem.ListViewSubItem() { Name = "_Size", Text = num.ToString() });
                lvi.SubItems.Add(new ListViewItem.ListViewSubItem() { Name = "_Start", Text = f._start.ToString() });

                listView1.Items.Add(lvi);

                itemCount++;
            }

            if (itemCount == 1)
                statusCount.Text = itemCount.ToString() + " item";

            else
                statusCount.Text = itemCount.ToString() + " items";
        }



        void cmd_cd(int new_lba)
        {

            diskImage.cmd_cd(new_lba);

            cmd_ls();
        }



        private void listView1_DoubleClick(object sender, EventArgs e)
        {
            if (listView1.SelectedItems.Count == 1)
            {
                if (listView1.SelectedItems[0].SubItems.ContainsKey("Type"))
                {
                    if (listView1.SelectedItems[0].SubItems["Type"].Text == "Directory")
                    {
                        if (listView1.SelectedItems[0].Text == "..")
                            cmd_cd(-1);
                        else if (listView1.SelectedItems[0].Text != ".")
                            cmd_cd(Convert.ToInt16(listView1.SelectedItems[0].SubItems[6].Text));
                        else
                            cmd_ls();
                    }
                    else if (listView1.SelectedItems[0].SubItems["Type"].Text == "File")
                    {

                        byte[] data = diskImage.cmd_get_file((string)listView1.SelectedItems[0].Tag);
                        if (data == null) data = new byte[0];

                        frmEditFile frmedit = new frmEditFile();
                        frmedit.setTitle("File: " + listView1.SelectedItems[0].Text);
                        frmedit.Start_Address = 0;
                        frmedit.setFilename(listView1.SelectedItems[0].Text);
                        frmedit.setBinary(data);
                        frmedit.ShowDialog(this);

                        byte[] newdata = frmedit.getBinary();
                        if (frmedit.getSaveKeyHit() || (!Utils.CompareByteArrays(data, newdata) && MessageBox.Show("Save file " + listView1.SelectedItems[0].Text + "?", "Confirm save", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes))
                        {
                            diskImage.cmd_rmdir((string)listView1.SelectedItems[0].Tag);
                            cmd_mkbin((string)listView1.SelectedItems[0].Text, frmedit.getBinary());
                        }
                    }
                }
            }
        }


        void refreshIconView()
        {

            Bitmap[] i = {
                global::sol_1_disk_manager.Properties.Resources.icoLargeIcon,
                global::sol_1_disk_manager.Properties.Resources.icoDetails,
            //    global::sol_1_disk_manager.Properties.Resources.ico62999,
                global::sol_1_disk_manager.Properties.Resources.icoSmallIcon,

                global::sol_1_disk_manager.Properties.Resources.icoList,
                global::sol_1_disk_manager.Properties.Resources.icoTile };
            toolStripSplitButton1.Image = i[(int)listView1.View];
        }

        void cmd_rmdir(string filename)
        {
            diskImage.cmd_rmdir(filename);
            cmd_ls();
        }


        void cmd_mktxt(string str, string txt)
        {

            byte[] data = System.Text.Encoding.ASCII.GetBytes(txt);

            cmd_mkbin(str, data);
        }


        void cmd_mkbin(string str, byte[] data)
        {
            diskImage.cmd_mkbin(str, data);

            cmd_ls();
        }


        private void listView1_KeyDown(object sender, KeyEventArgs e)
        {
            if (isRenaming) return;

            if (e.KeyCode == Keys.Delete)
            {
                deleteToolStripMenuItem_Click(sender, e);
            }

            else if (e.KeyCode == Keys.F2)
            {
                if (listView1.SelectedItems.Count == 1)
                {
                    isRenaming = true;
                    listView1.SelectedItems[0].BeginEdit();
                }
            }

            else if (e.KeyCode == Keys.Enter)
            {
                listView1_DoubleClick(null, null);
            }
            else if (e.Modifiers == Keys.Control && e.KeyCode == Keys.A)
            {
                foreach (ListViewItem item in listView1.Items)
                {
                    item.Selected = true;
                }
            }

            else if (e.Modifiers == Keys.Control && e.KeyCode == Keys.C && listView1.SelectedItems.Count > 0)
            {
                copyToolStripMenuItem_Click(null, null);
            }
            else if (e.Modifiers == Keys.Control && e.KeyCode == Keys.C && listView1.Items.Count > 0)
            {
                String clipboard = "";
                foreach (ListViewItem lvi in listView1.Items)
                {

                    clipboard += lvi.Text.PadRight(15);
                    clipboard += "U" + lvi.SubItems[1].Text.PadRight(5);
                    clipboard += lvi.SubItems[2].Text;
                    clipboard += "\r\n";
                }
                try
                {
                    Clipboard.SetText(clipboard);
                }
                catch { }
            }
            else if (e.Modifiers == Keys.Control && e.KeyCode == Keys.V && clipboard.Count > 0)
            {
                pasteToolStripMenuItem_Click(null, null);
            }


        }


        private bool cmd_rename(String filename, String newname, LabelEditEventArgs e)
        {
            if (diskImage.cmd_rename(filename, newname))
            {
                String name = newname;

                name = name.PadRight(24);

                listView1.Items[e.Item].Text = name.Trim();
                listView1.Items[e.Item].Tag = name;
                return true;
            }

            return false;
        }


        private void listView1_AfterLabelEdit(object sender, LabelEditEventArgs e)
        {
            if (e.Label != null)
            {
                e.CancelEdit = !cmd_rename((string)listView1.Items[e.Item].Tag, e.Label, e);
                e.CancelEdit = true;
            }

            isRenaming = false;
        }

        private void contextMenuStrip1_Opening(object sender, CancelEventArgs e)
        {
            newFileToolStripMenuItem.Enabled = listView1.SelectedItems.Count == 0;

            editToolStripMenuItem.Enabled = listView1.SelectedItems.Count == 1;
            renameToolStripMenuItem.Enabled = listView1.SelectedItems.Count == 1;
            deleteToolStripMenuItem.Enabled = listView1.SelectedItems.Count != 0;
            exportToolStripMenuItem.Enabled = listView1.Items.Count != 0;

            copyToolStripMenuItem.Enabled = listView1.SelectedItems.Count != 0;
            userToolStripMenuItem.Enabled = listView1.SelectedItems.Count != 0;

            pasteToolStripMenuItem.Enabled = clipboard.Count != 0;


            renToUppercaseToolStripMenuItem.Enabled = listView1.SelectedItems.Count != 0;
        }

        private void renameToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (listView1.SelectedItems.Count == 1)
            {
                listView1.SelectedItems[0].BeginEdit();
            }
        }

        private void deleteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (listView1.SelectedItems.Count == 1)
            {
                DialogResult dialogResult = MessageBox.Show("Confirm Delete \"" + listView1.SelectedItems[0].Text + "\"?", "Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (dialogResult == DialogResult.Yes)
                    cmd_rmdir((string)listView1.SelectedItems[0].Tag);
            }
            else if (listView1.SelectedItems.Count > 1)
            {

                DialogResult dialogResult = MessageBox.Show("Confirm Delete all \"" + listView1.SelectedItems.Count.ToString() + "\" files?", "Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (dialogResult == DialogResult.Yes)
                {

                    foreach (ListViewItem item in listView1.SelectedItems)
                    {
                        cmd_rmdir((string)item.Tag);
                        item.Selected = false;
                    }

                    listView1_ItemSelectionChanged(null, null);
                }
            }
        }

        private void listView1_ItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e)
        {
            int itemCount = listView1.SelectedItems.Count;
            int itemSize = 0;
            String itemSizeText = "";

            selectedUser = -1;

            if (itemCount != 0)
            {
                foreach (ListViewItem item in listView1.SelectedItems)
                {
                    itemSize += int.Parse(item.SubItems["_size"].Text);
                }
            }

            if (itemSize > 1000000)
            {
                itemSizeText = (itemSize / 1000000.0).ToString("N2") + " MB";
            }
            else if (itemSize > 1000)
            {
                itemSizeText = (itemSize / 1000.0).ToString("N2") + " KB";
            }
            else
                itemSizeText = itemSize + " B";

            
            if (itemCount == 1)
            {
                statusSelectedCount.Text = itemCount.ToString() + " item selected";
                statusSelectedSize.Text = itemSizeText;
            
            }
            else if (itemCount > 1)
            {
                statusSelectedCount.Text = itemCount.ToString() + " items selected";
                statusSelectedSize.Text = itemSizeText;
            }

            else
            {
                statusSelectedCount.Text = "";
                statusSelectedSize.Text = "";
            }

            newFileToolStripMenuItem1.Enabled = listView1.SelectedItems.Count == 0;
            openFileToolStripMenuItem.Enabled = listView1.SelectedItems.Count == 1;
            deleteToolStripMenuItem1.Enabled = listView1.SelectedItems.Count == 1;

        }


        private void toolStripSplitButton1_ButtonClick(object sender, EventArgs e)
        {
            if (listView1.View == View.Tile)
                listView1.View = 0;
            else
                listView1.View++;

            refreshIconView();

            IniFile ini = new IniFile(System.Environment.CurrentDirectory + "\\" + "config.ini");
            ini.IniWriteValue("general", "view", ((int)listView1.View).ToString());
        }

        private void largeIconsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            listView1.View = View.LargeIcon;
            refreshIconView();
        }

        private void detailsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            listView1.View = View.Details;
            refreshIconView();
        }

        private void smallIconsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            listView1.View = View.SmallIcon;
            refreshIconView();
        }

        private void listToolStripMenuItem_Click(object sender, EventArgs e)
        {
            listView1.View = View.List;
            refreshIconView();
        }

        private void tileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            listView1.View = View.Tile;
            refreshIconView();

        }

        private bool readImageFile(String fileName)
        {
            if (diskImage.ReadImageFile(fileName, toolStripProgressBar1))
            {

                selectedVol = -1;
                current_filename = fileName;

                cmd_ls();
                return true;
            }
            return false;
        }

        private void openImageToolStripMenuItem_Click(object sender, EventArgs e)
        {
            openFileDialog1.FileName = current_filename;
            openFileDialog1.AddExtension = true;
            openFileDialog1.DefaultExt = "dsk";
            openFileDialog1.CheckFileExists = true;
            openFileDialog1.Multiselect = false;
            openFileDialog1.Filter = "Disk Images (*.dsk, *.img)|*.dsk; *.img|All Files (*.*)|*.*";
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                string filename = openFileDialog1.FileName;

                IniFile ini = new IniFile(System.Environment.CurrentDirectory + "\\" + "config.ini");
                ini.IniWriteValue("general", "last_open", filename);

                if (readImageFile(filename))
                {
                    this.Text = "Baffa-1 Disk Manager " + filename;
                }
                else
                {
                    current_filename = "";
                    this.Text = "Baffa-1 Disk Manager (No File)";
                }

            }
        }

        private void saveImageToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (current_filename != "")
            {
                if (MessageBox.Show("Save file?", "Confirm save", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {

                    if (saveImageFile(current_filename))
                        this.Text = "Baffa-1 Disk Manager " + current_filename;
                    else
                    {
                        current_filename = "";
                        //this.Text = "Baffa-1 Disk Manager (No File)";
                    }
                }
            }

        }

        private bool saveImageFile(string fileName)
        {
            return diskImage.SaveImageFile(fileName, toolStripProgressBar1);
        }


        private void deleteToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            deleteToolStripMenuItem_Click(sender, e);
        }

        private void textToolStripMenuItem_Click(object sender, EventArgs e)
        {
            String str = "";

            if (Utils.ShowInputDialog(ref str, "File Name", this) == DialogResult.OK)
                if (str.Trim() != "")
                {
                    int start_address = 0;
                    try
                    {
                        IniFile ini = new IniFile(System.Environment.CurrentDirectory + "\\" + "config.ini");
                        String hex_start = ini.IniReadValue("address_start", "disk_buffer");
                        start_address = Convert.ToInt32(hex_start, 16);
                    }
                    catch
                    { }

                    frmEditFile frmedit = new frmEditFile();
                    frmedit.setTitle("New Text File: " + str);
                    frmedit.Start_Address = start_address;
                    frmedit.FileType = frmEditFile.EditorType.Text;
                    frmedit.newFile();
                    frmedit.ShowDialog(this);

                    DialogResult dialogResult = MessageBox.Show("Save New File \"" + str + "\"?", "Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (dialogResult == DialogResult.Yes)
                    {
                        cmd_mktxt(str, frmedit.getText());
                        cmd_ls();

                    }

                }
        }

        private void binaryToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            String str = "";

            if (Utils.ShowInputDialog(ref str, "File Name", this) == DialogResult.OK)
                if (str.Trim() != "")
                {

                    int start_address = 0;
                    try
                    {
                        IniFile ini = new IniFile(System.Environment.CurrentDirectory + "\\" + "config.ini");
                        String hex_start = ini.IniReadValue("address_start", "disk_buffer");
                        start_address = Convert.ToInt32(hex_start, 16);
                    }
                    catch
                    { }

                    frmEditFile frmedit = new frmEditFile();
                    frmedit.setTitle("New Binary File: " + str);
                    frmedit.Start_Address = start_address;
                    frmedit.FileType = frmEditFile.EditorType.Binary;
                    frmedit.newFile();
                    frmedit.ShowDialog(this);

                    DialogResult dialogResult = MessageBox.Show("Save New File \"" + str + "\"?", "Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (dialogResult == DialogResult.Yes)
                    {
                        Byte[] data = frmedit.getBinary();
                        cmd_mkbin(str, data);
                        cmd_ls();

                    }

                }
        }

        private void textToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            String str = "";

            if (Utils.ShowInputDialog(ref str, "File Name", this) == DialogResult.OK)
                if (str.Trim() != "")
                {

                    int start_address = 0;
                    try
                    {
                        IniFile ini = new IniFile(System.Environment.CurrentDirectory + "\\" + "config.ini");
                        String hex_start = ini.IniReadValue("address_start", "disk_buffer");
                        start_address = Convert.ToInt32(hex_start, 16);
                    }
                    catch
                    { }

                    frmEditFile frmedit = new frmEditFile();
                    frmedit.setTitle("New Text File: " + str);
                    frmedit.Start_Address = start_address;
                    frmedit.FileType = frmEditFile.EditorType.Text;
                    frmedit.newFile();
                    frmedit.ShowDialog(this);

                    DialogResult dialogResult = MessageBox.Show("Save New File \"" + str + "\"?", "Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (dialogResult == DialogResult.Yes)
                    {
                        cmd_mktxt(str, frmedit.getText());
                        cmd_ls();

                    }

                }
        }

        private void binaryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            String str = "";

            if (Utils.ShowInputDialog(ref str, "File Binary Name", this) == DialogResult.OK)
                if (str.Trim() != "")
                {
                    int start_address = 0;
                    try
                    {
                        IniFile ini = new IniFile(System.Environment.CurrentDirectory + "\\" + "config.ini");
                        String hex_start = ini.IniReadValue("address_start", "disk_buffer");
                        start_address = Convert.ToInt32(hex_start, 16);
                    }
                    catch
                    { }

                    frmEditFile frmedit = new frmEditFile();
                    frmedit.setTitle("New File: " + str);
                    frmedit.Start_Address = start_address;
                    frmedit.FileType = frmEditFile.EditorType.Binary;
                    frmedit.newFile();
                    frmedit.ShowDialog(this);

                    DialogResult dialogResult = MessageBox.Show("Save New File \"" + str + "\"?", "Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (dialogResult == DialogResult.Yes)
                    {
                        cmd_mkbin(str, frmedit.getBinary());
                        cmd_ls();

                    }

                }
        }

        private void listView1_DragDrop(object sender, DragEventArgs e)
        {
            bool newfile = false;
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            foreach (string file in files)
            {
                if (File.Exists(file))
                {
                    using (BinaryReader b = new BinaryReader(File.Open(file, FileMode.Open)))
                    {
                        string filename = Path.GetFileName(file);
                        Byte[] data = Utils.ReadAllBytes(b);
                        cmd_mkbin(filename, data);

                    }
                    newfile = true;
                }

            }
            if (newfile)
                cmd_ls();
        }

        private void listView1_DragEnter(object sender, DragEventArgs e)
        {
            e.Effect = DragDropEffects.Copy;
        }

        private void quitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void openRecentToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string filename = "";
            string viewstyle = "";

            IniFile ini = new IniFile(System.Environment.CurrentDirectory + "\\" + "config.ini");
            filename = ini.IniReadValue("general", "last_open");
            viewstyle = ini.IniReadValue("general", "view");
            int result = -1;
            if (int.TryParse(viewstyle, out result))
            {
                if (result >= 0 && result <= 4)
                {
                    listView1.View = (View)result;
                    refreshIconView();
                }
            }

            if (filename.Trim() != "")
            {
                if (readImageFile(filename))
                {
                    this.Text = "Baffa-1 Disk Manager " + filename;
                }
                else
                {
                    current_filename = "";
                    this.Text = "Baffa-1 Disk Manager (No File)";
                }
            }
        }

        private void saveAsToolStripMenuItem_Click(object sender, EventArgs e)
        {

            saveFileDialog1.FileName = current_filename;
            saveFileDialog1.AddExtension = true;
            saveFileDialog1.DefaultExt = "dsk";
            saveFileDialog1.CheckFileExists = false;
            saveFileDialog1.Filter = "Disk Images (*.dsk, *.img)|*.dsk; *.img|All Files (*.*)|*.*";

            if (saveFileDialog1.ShowDialog() == DialogResult.OK)
            {
                string filename = saveFileDialog1.FileName;
                if (saveImageFile(filename))
                {
                    this.Text = "Baffa-1 Disk Manager " + filename;
                }
                else
                {
                    current_filename = "";
                    //this.Text = "Baffa-1 Disk Manager (No File)";
                }
            }

        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            FrmAbout about = new FrmAbout();
            about.Show(this);
        }

        private void openFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            listView1_DoubleClick(null, null);
        }


        private void editBootToolStripMenuItem_Click(object sender, EventArgs e)
        {

            Byte[] data = diskImage.get_boot();

            int start_address = 0;
            try
            {
                IniFile ini = new IniFile(System.Environment.CurrentDirectory + "\\" + "config.ini");
                String hex_start = ini.IniReadValue("address_start", "boot_origin");
                start_address = Convert.ToInt32(hex_start, 16);
            }
            catch
            { }

            frmEditFile frmedit = new frmEditFile();
            frmedit.setTitle("Edit Boot");
            frmedit.Start_Address = start_address;
            frmedit.setBinary(data);
            frmedit.ShowDialog(this);

            byte[] newdata = frmedit.getBinary();
            if (!Utils.CompareByteArrays(data, newdata))
            {
                DialogResult dialogResult = MessageBox.Show("Confirm Edit of the \"Boot Sector\"?", "Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (dialogResult == DialogResult.Yes)
                {
                    if (frmedit.FileType == frmEditFile.EditorType.Binary)
                    {
                        diskImage.set_boot(frmedit.getBinary());
                    }
                }
            }
        }

        private void editKernelToolStripMenuItem_Click(object sender, EventArgs e)
        {

            Byte[] data = diskImage.get_kernel();

            int start_address = 0;
            try
            {
                IniFile ini = new IniFile(System.Environment.CurrentDirectory + "\\" + "config.ini");
                String hex_start = ini.IniReadValue("address_start", "kernel_origin");
                start_address = Convert.ToInt32(hex_start, 16);
            }
            catch
            { }

            frmEditFile frmedit = new frmEditFile();
            frmedit.setTitle("Edit Kernel");
            frmedit.Start_Address = start_address;
            frmedit.setBinary(data);
            frmedit.ShowDialog(this);

            byte[] newdata = frmedit.getBinary();
            if (!Utils.CompareByteArrays(data, newdata))
            {
                DialogResult dialogResult = MessageBox.Show("Confirm Edit of the \"Kernel Sector\"?", "Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (dialogResult == DialogResult.Yes)
                {
                    if (frmedit.FileType == frmEditFile.EditorType.Binary)
                    {

                        diskImage.set_kernel(frmedit.getBinary());

                    }
                }
            }
        }

        private void listView1_ColumnWidthChanged(object sender, ColumnWidthChangedEventArgs e)
        {
            IniFile ini = new IniFile(System.Environment.CurrentDirectory + "\\" + "config.ini");
            ini.IniWriteValue("general", "col_" + e.ColumnIndex.ToString(), listView1.Columns[e.ColumnIndex].Width.ToString());
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            listView1_DoubleClick(null, null);
        }

        private void refreshToolStripMenuItem_Click(object sender, EventArgs e)
        {
            cmd_ls();
        }

        private void newImageToolStripMenuItem_Click(object sender, EventArgs e)
        {


            selectedVol = -1;
            diskImage.NewImage(diskImage.DiskImageFormat);
            cmd_ls();


        }



        private void editorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            frmEditFile frmedit = new frmEditFile();
            frmedit.setTitle("Editor");
            frmedit.Start_Address = 0;
            frmedit.newFile();
            frmedit.ShowDialog(this);
        }


        private void listView1_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            // Determine if clicked column is already the column that is being sorted.
            if (e.Column == lvwColumnSorter.SortColumn)
            {
                // Reverse the current sort direction for this column.
                if (lvwColumnSorter.Order == SortOrder.Ascending)
                {
                    lvwColumnSorter.Order = SortOrder.Descending;
                }
                else
                {
                    lvwColumnSorter.Order = SortOrder.Ascending;
                }
            }
            else
            {
                // Set the column number that is to be sorted; default to ascending.
                lvwColumnSorter.SortColumn = e.Column;
                lvwColumnSorter.Order = SortOrder.Ascending;
            }


            IniFile ini = new IniFile(System.Environment.CurrentDirectory + "\\" + "config.ini");
            ini.IniWriteValue("general", "sort_column", lvwColumnSorter.SortColumn.ToString());
            ini.IniWriteValue("general", "column_order", ((int)lvwColumnSorter.Order).ToString());

            refresh_sort_dir();

            // Perform the sort with these new sort options.
            this.listView1.Sort();
        }


        private void refresh_sort_dir()
        {
            foreach (ColumnHeader c in listView1.Columns)
                Utils.SetSortArrow(c, SortOrder.None);

            Utils.SetSortArrow(listView1.Columns[lvwColumnSorter.SortColumn], lvwColumnSorter.Order);
        }

        //////////////////////////////////////////////////////////////////////////////////////////////
        /// CF CARD

        private void readDiskToolStripMenuItem_Click(object sender, EventArgs e)
        {
            frmDiskSelection frmDisk = new frmDiskSelection();
            frmDisk.ShowDialog(this);

            selDisk = frmDisk.SelDisk;

            if (selDisk.Id != -1)
            {
                DialogResult dialogResult = MessageBox.Show("Load Disk \"" + selDisk.Name + "\"?", "Load Media", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (dialogResult == DialogResult.Yes)
                {
                    selectedVol = selDisk.Id;
                    diskImage.ReadRawDisk(selectedVol);

                    cmd_ls();
                }
            }
            else
                selectedVol = -1;

        }

        private void writeDiskToolStripMenuItem_Click(object sender, EventArgs e)
        {

            if (selectedVol == -1)
            {
                frmDiskSelection frmDisk = new frmDiskSelection();
                frmDisk.ShowDialog(this);

                selDisk = frmDisk.SelDisk;

                if (selDisk.Id != -1)
                {
                    DialogResult dialogResult = MessageBox.Show("Save Disk \"" + selDisk.Name + "\"?", "Save Media", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (dialogResult == DialogResult.Yes)
                    {
                        int selectedVol = selDisk.Id;

                        try
                        {
                            diskImage.WriteRawDisk(selectedVol);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(ex.Message, "Save Media", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }

                    }
                }

            }
            else
            {
                DialogResult dialogResult = MessageBox.Show("Save Disk \"" + selDisk.Name + "\"?", "Save Media", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (dialogResult == DialogResult.Yes)
                {
                    int selectedVol = selDisk.Id;

                    try
                    {
                        diskImage.WriteRawDisk(selectedVol);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, "Save Media", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }


        }


        public void PresentResult(RawDisk disk)
        {
            byte[] data = disk.ReadClusters(0, (int)Math.Min(disk.ClusterCount, ClustersToRead));

            string fatType = Encoding.ASCII.GetString(data, 82, 8);     // Extended FAT parameters have a display name here.
            bool isFat = fatType.StartsWith("FAT");
            bool isNTFS = Encoding.ASCII.GetString(data, 3, 4) == "NTFS";

            // Optimization, if it's a known FS, we know it's not all zeroes.
            bool allZero = (!isNTFS || !isFat) && data.All(s => s == 0);

            Console.WriteLine("Size in bytes : {0:N0}", disk.SizeBytes);
            Console.WriteLine("Sectors       : {0:N0}", disk.ClusterCount);
            Console.WriteLine("SectorSize    : {0:N0}", disk.SectorSize);
            Console.WriteLine("ClusterCount  : {0:N0}", disk.ClusterCount);
            Console.WriteLine("ClusterSize   : {0:N0}", disk.ClusterSize);
            Console.WriteLine("Is NTFS       : {0}", isNTFS);
            Console.WriteLine("Is FAT        : {0}", isFat ? fatType : "False");

            Console.WriteLine("All bytes zero: {0}", allZero);
        }



        private void toolStripComboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            // apagar aqui
        }

        private void copyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            clipboard.Clear();


            foreach (ListViewItem item in listView1.SelectedItems)
            {
                String filename = (string)item.Tag;
                filename = filename.PadRight(11);
                filename = filename.Substring(0, 8).Trim() + "." + filename.Substring(8, 3).Trim();

                clipboard.Add(filename, diskImage.cmd_get_file((string)item.Tag));
            }

        }

        private void pasteToolStripMenuItem_Click(object sender, EventArgs e)
        {

            HashSet<String> files = new HashSet<string>();
            foreach (FileEntry f in diskImage.cmd_ls())
            {/*
                String filename = f._name.Trim() + "." + f._ext.Trim();
                if (!files.Contains(filename))
                    files.Add(filename);
                    */
            }

            foreach (String filename in clipboard.Keys)
            {

                if (files.Contains(filename))
                {
                    DialogResult dialogResult = MessageBox.Show("Overwrite File \"" + filename + "\"?", "Overwrite", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                    if (dialogResult == DialogResult.Yes)
                    {
                        diskImage.cmd_rmdir(filename);
                        Byte[] data = clipboard[filename];
                        cmd_mkbin(filename, data);
                    }
                    else if (dialogResult == DialogResult.Cancel)
                    {
                        break;
                    }
                }
                else
                {
                    Byte[] data = clipboard[filename];
                    cmd_mkbin(filename, data);
                }
            }
            cmd_ls();
        }

        private void frmMain_Shown(object sender, EventArgs e)
        {
            openRecentToolStripMenuItem_Click(null, null);
        }

        private void renToUppercaseToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (ListViewItem item in listView1.SelectedItems)
            {
                String filename = (string)item.Tag.ToString().ToUpper();
                filename = filename.PadRight(11);
                filename = filename.Substring(0, 8).Trim() + "." + filename.Substring(8, 3).Trim();

                diskImage.cmd_rename(item.Tag.ToString(), filename);
            }
            cmd_ls();
        }

        private void filesToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            if (listView1.SelectedItems.Count == 1)
            {
                byte[] data = diskImage.cmd_get_file((string)listView1.SelectedItems[0].Tag);

                String filename = listView1.SelectedItems[0].Text;

                saveFileDialog1.FileName = filename;
                if (saveFileDialog1.ShowDialog(this) == DialogResult.OK)
                {
                    filename = saveFileDialog1.FileName;

                    File.WriteAllBytes(filename, data);
                    //MessageBox.Show("FileS, "Save Media", MessageBoxButtons.OK, MessageBoxIcon.Error);

                }
            }
            else
            {


                if (folderBrowserDialog1.ShowDialog(this) == DialogResult.OK)
                {

                    List<ListViewItem> col = new List<ListViewItem>();
                    if (listView1.SelectedItems.Count > 0)
                    {
                        foreach (ListViewItem lvi in listView1.SelectedItems)
                        {
                            col.Add(lvi);
                        }
                    }
                    else
                    {
                        foreach (ListViewItem lvi in listView1.Items)
                        {
                            col.Add(lvi);
                        }
                    }

                    foreach (ListViewItem lvi in col)
                    {
                        byte[] data = diskImage.cmd_get_file((string)lvi.Tag);

                        String filename = lvi.Text;

                        File.WriteAllBytes(folderBrowserDialog1.SelectedPath + "\\" + filename, data);
                        //MessageBox.Show("FileS, "Save Media", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void filesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            openFileDialog1.FileName = "";
            openFileDialog1.AddExtension = true;
            openFileDialog1.DefaultExt = "*.*";
            openFileDialog1.CheckFileExists = true;
            openFileDialog1.Multiselect = true;
            openFileDialog1.Filter = "All Files (*.*)|*.*";
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                bool newfile = false;
                string[] files = openFileDialog1.FileNames;
                foreach (string file in files)
                {
                    if (File.Exists(file))
                    {
                        using (BinaryReader b = new BinaryReader(File.Open(file, FileMode.Open)))
                        {
                            string filename = Path.GetFileName(file);
                            Byte[] data = Utils.ReadAllBytes(b);
                            cmd_mkbin(filename, data);

                        }
                        newfile = true;
                    }

                }
                if (newfile)
                    cmd_ls();

            }
        }

        private void pKGToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            saveFileDialog1.FileName = "";
            saveFileDialog1.AddExtension = true;
            saveFileDialog1.DefaultExt = "pkg";
            saveFileDialog1.CheckFileExists = false;
            saveFileDialog1.Filter = "Package File (*.pkg)|*.pkg|All Files (*.*)|*.*";

            if (saveFileDialog1.ShowDialog() == DialogResult.OK)
            {
                string pkgfilename = saveFileDialog1.FileName;


                if (File.Exists(pkgfilename))
                    File.Delete(pkgfilename);

                using (TextWriter text_writer = File.CreateText(pkgfilename))
                {

                    List<ListViewItem> col = new List<ListViewItem>();
                    if (listView1.SelectedItems.Count > 0)
                    {
                        foreach (ListViewItem lvi in listView1.SelectedItems)
                        {
                            col.Add(lvi);
                        }
                    }
                    else
                    {
                        foreach (ListViewItem lvi in listView1.Items)
                        {
                            col.Add(lvi);
                        }
                    }

                    foreach (ListViewItem lvi in col)
                    {
                        byte[] data = diskImage.cmd_get_file((string)lvi.Tag);

                        string filename = lvi.Text;
                        string hexdata = Utils.ByteArrayToHexString(data);
                        string checksum = Utils.CalculateChecksum(data);

                        string pkgdata = "";
                        pkgdata += "A:DOWNLOAD " + filename + "\r\n";
                        pkgdata += "U0\r\n";
                        pkgdata += ":" + hexdata + ">" + checksum + "\r\n";
                        text_writer.Write(pkgdata);
                    }
                }
            }
        }

        private void pKGToolStripMenuItem_Click(object sender, EventArgs e)
        {
            openFileDialog1.FileName = "";
            openFileDialog1.AddExtension = true;
            openFileDialog1.DefaultExt = "*.pkg";
            openFileDialog1.CheckFileExists = true;
            openFileDialog1.Multiselect = false;
            openFileDialog1.Filter = "Package File (*.pkg)|*.pkg|All Files (*.*)|*.*";
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                String[] lines = File.ReadAllLines(openFileDialog1.FileName);
                bool newfile = false;
                int i = 0;

                string current_filename = "";
                int user = 0;

                while (i < lines.Length)
                {
                    if (lines[i].IndexOf(":DOWNLOAD ") > 0)
                        current_filename = lines[i].Substring(11);

                    if (lines[i].IndexOf("U") == 0)
                    {
                        int.TryParse(lines[i].Substring(1), out user);
                    }


                    if (current_filename != "" && lines[i].IndexOf(":") == 0 && lines[i].IndexOf(">") > 0)
                    {
                        string hexdata = lines[i].Substring(1, lines[i].IndexOf(">") - 1);
                        string checksum = lines[i].Substring(lines[i].IndexOf(">") + 1);
                        byte[] data = Utils.HexStringToByteArray(hexdata);

                        if (checksum == Utils.CalculateChecksum(data))
                        {
                            cmd_mkbin(current_filename, data);
                        }
                        current_filename = "";
                        user = 0;
                    }

                    i++;
                }
                if (newfile)
                    cmd_ls();
            }
        }

        void cmd_mkdir(string str)
        {
            diskImage.cmd_mkdir(str);

            cmd_ls();
        }


        private void newDirectoryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            String str = "";
            if (Utils.ShowInputDialog(ref str, "Directory Name", this) == DialogResult.OK)
                if (str.Trim() != "")
                {
                    cmd_mkdir(str);

                    cmd_ls();
                }
        }


    }
}

