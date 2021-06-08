using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace sol_1_disk_manager
{
    public partial class frmMain : Form
    {
        public frmMain()
        {
            InitializeComponent();

            contextMenuStrip1_Opening(null, null);

            openRecentToolStripMenuItem_Click(null, null);

            openFileToolStripMenuItem.Enabled = false;
            deleteToolStripMenuItem1.Enabled = false;


            listView1.Columns.Add("Title");
            listView1.Columns.Add("Attributes");
            listView1.Columns.Add("Size");
            listView1.Columns.Add("Creation Date");

        }

        String current_filename = "";

        const int FST_ENTRY_SIZE = 32;
        const int FST_FILES_PER_SECT = 512 / FST_ENTRY_SIZE;
        const int FST_FILES_PER_DIR = 16;
        const int FST_NBR_DIRECTORIES = 64;

        // 1 sector for header, the rest is for the list of files/dirs
        const int FST_SECTORS_PER_DIR = (1 + (FST_ENTRY_SIZE * FST_FILES_PER_DIR / 512));
        const int FST_TOTAL_SECTORS = (FST_SECTORS_PER_DIR * FST_NBR_DIRECTORIES);
        const int FST_LBA_START = 32;
        const int FST_LBA_END = (FST_LBA_START + FST_TOTAL_SECTORS - 1);

        const int FS_NBR_FILES = (FST_NBR_DIRECTORIES * FST_FILES_PER_DIR);
        const int FS_SECTORS_PER_FILE = 32;             // the first sector is always a header with a NULL parameter(first byte)
                                                        // so that we know which blocks are free or taken
        const int FS_FILE_SIZE = (FS_SECTORS_PER_FILE * 512);
        const int FS_TOTAL_SECTORS = (FS_NBR_FILES * FS_SECTORS_PER_FILE);
        const int FS_LBA_START = (FST_LBA_END + 1);
        const int FS_LBA_END = (FS_LBA_START + FS_NBR_FILES - 1);

        const int CF_CARD_LBA_SIZE = 0x800;         // temporary small size

        const int ROOT_LBA = FST_LBA_START;


        int current_dir_LBA = ROOT_LBA;
        //int parent_dir_LBA = 0x00;
        Stack<int> parentStack = new Stack<int>();

        Byte[] fileData;


        String getStringFromByteArray(byte[] fileBytes2, int start, int max)
        {
            String ret = "";
            for (int i = 0; i < max && fileBytes2[start + i] != 0x00; i++)
                ret += Convert.ToChar(fileBytes2[start + i]);

            return ret.Trim('\0');
        }





        Byte[] getByteArray(byte[] fileBytes2, int start, int max)
        {
            Byte[] ret = new byte[max];
            for (int i = 0; i < max; i++)
                ret[i] = fileBytes2[start + i];

            return ret;
        }


        public byte[] ReadAllBytes(BinaryReader reader)
        {
            const int bufferSize = 4096;
            using (var ms = new MemoryStream())
            {
                byte[] buffer = new byte[bufferSize];
                int count;
                while ((count = reader.Read(buffer, 0, buffer.Length)) != 0)
                    ms.Write(buffer, 0, count);
                return ms.ToArray();
            }

        }


        void cmd_ls()
        {

            listView1.Items.Clear();
            int a = current_dir_LBA;
            a++;
            int b = a;
            Byte[] disk_buffer = getByteArray(fileData, b * 0x200, 0x200);
            int index = 0;
            int d = 0;

            //int disk_buffer = current_dir_LBA * 512;
            //int d = disk_buffer + 0x200;


            if (parentStack.Count > 0)
            {
                ListViewItem lvi = new ListViewItem();
                lvi.ImageIndex = 0;
                lvi.Name = "Title";
                lvi.Text = "..";

                lvi.Tag = "-1";

                lvi.SubItems.Add(new ListViewItem.ListViewSubItem() { Name = "Type", Text = "Directory" });

                listView1.Items.Add(lvi);
            }

            int itemCount = 0;

            while (index < FST_FILES_PER_DIR)//cmd_ls_L1:
            {
                if (disk_buffer[d] != 0x00)
                {
                    itemCount++;
                    String title = getStringFromByteArray(disk_buffer, d, 24);

                    String type = "";
                    String attributes = "";
                    if ((disk_buffer[d + 24] & 0b00000001) != 0x00)
                    {
                        attributes += "d";
                        type = "Directory";
                    }
                    else
                    {
                        attributes += "-";
                        type = "File";
                    }


                    if ((disk_buffer[d + 24] & 0b00000010) != 0x00)
                        attributes += "r";
                    else
                        attributes += "-";

                    if ((disk_buffer[d + 24] & 0b00000100) != 0x00)
                        attributes += "w";
                    else
                        attributes += "-";

                    if ((disk_buffer[d + 24] & 0b00001000) != 0x00)
                        attributes += "x";
                    else
                        attributes += "-";

                    int num = disk_buffer[d + 28] * 256 + disk_buffer[d + 27];
                    String size = "";
                    if (num > 1000)
                        size = (num / 1000.0).ToString("N3") + " KB";
                    else
                        size = num.ToString("N0") + " B";

                    String creationDate = Convert.ToUInt16(disk_buffer[d + 29]).ToString("X2");
                    creationDate += "/";
                    creationDate += Convert.ToUInt16(disk_buffer[d + 30]).ToString("X2");
                    creationDate += "/";
                    creationDate += int.Parse(Convert.ToUInt16(disk_buffer[d + 31]).ToString("X2")) + 2000;

                    ListViewItem lvi = new ListViewItem();
                    lvi.ImageIndex = (disk_buffer[d + 24] & 0b00000001) != 0x00 ? 0 : 2;
                    lvi.Text = title;
                    lvi.Tag = disk_buffer[d + 25];
                    lvi.SubItems.Add(new ListViewItem.ListViewSubItem() { Name = "Attributes", Text = attributes });
                    lvi.SubItems.Add(new ListViewItem.ListViewSubItem() { Name = "Size", Text = size });
                    lvi.SubItems.Add(new ListViewItem.ListViewSubItem() { Name = "Creation Date", Text = creationDate });
                    lvi.SubItems.Add(new ListViewItem.ListViewSubItem() { Name = "Type", Text = type });
                    lvi.SubItems.Add(new ListViewItem.ListViewSubItem() { Name = "_Size", Text = num.ToString() });


                    listView1.Items.Add(lvi);



                    index++; //cmd_ls_next:
                    if (index == FST_FILES_PER_DIR)
                        return;
                }

                index++; //cmd_ls_next:
                d += 0x20;
            }

            if (itemCount == 1)
                statusCount.Text = itemCount.ToString() + " item";

            else
                statusCount.Text = itemCount.ToString() + " items";
        }

        void cmd_cd(int new_lba)
        {
            if (new_lba == -1)
                current_dir_LBA = parentStack.Pop();
            else
            {
                parentStack.Push(current_dir_LBA);
                current_dir_LBA = new_lba;
            }

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
                        cmd_cd(Convert.ToInt16(listView1.SelectedItems[0].Tag));
                    }
                    else if (listView1.SelectedItems[0].SubItems["Type"].Text == "File")
                    {

                        int size = Convert.ToInt16(listView1.SelectedItems[0].SubItems["_Size"].Text);
                        int b = Convert.ToInt16(listView1.SelectedItems[0].Tag);
                        b++;
                        //Byte[] diskBuffer = getByteArray(fileData,b, 512);
                        int d = b * 512;
                        int index = 0;

                        String file = "";
                        while (index < size)// != FS_SECTORS_PER_FILE - 1)
                        {
                            file += fileData[d + index].ToString("X2");
                            index++;
                        }

                        frmEditFile frmedit = new frmEditFile();
                        frmedit.setTitle("File: " + listView1.SelectedItems[0].Text);
                        frmedit.setText(file);
                        frmedit.ShowDialog(this);

                        if (file != frmedit.getText())
                        {

                            if (frmedit.getSaveKeyHit())
                            {
                                int parent_lba = current_dir_LBA;
                                parent_lba++;

                                int bb = parent_lba * 512 + find_file(listView1.SelectedItems[0].Text, parent_lba);

                                if (frmedit.FileType == frmEditFile.EditorType.Binary)
                                {
                                    Byte[] filearray = StringToByteArray(frmedit.getText());
                                    byte[] bytesize = BitConverter.GetBytes(filearray.Length);
                                    fileData[bb + 27] = bytesize[0];
                                    fileData[bb + 28] = bytesize[1];

                                    for (int i = 0; i < filearray.Length; i++)
                                        fileData[d + i] = filearray[i];
                                }
                                else if (frmedit.FileType == frmEditFile.EditorType.Text)
                                {
                                    String text = frmedit.getText();
                                    byte[] bytesize = BitConverter.GetBytes(text.Length);
                                    fileData[bb + 27] = bytesize[0];
                                    fileData[bb + 28] = bytesize[1];

                                    for (int i = 0; i < text.Length; i++)
                                        fileData[d + i] = Convert.ToByte(text[i]);
                                }
                                cmd_ls();
                            }
                            else
                            {
                                DialogResult dialogResult = MessageBox.Show("Confirm Edit of " + listView1.SelectedItems[0].SubItems["Type"].Text + " \"" + listView1.SelectedItems[0].Text + "\"?", "Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                                if (dialogResult == DialogResult.Yes)
                                {
                                    int parent_lba = current_dir_LBA;
                                    parent_lba++;

                                    int bb = parent_lba * 512 + find_file(listView1.SelectedItems[0].Text, parent_lba);

                                    if (frmedit.FileType == frmEditFile.EditorType.Binary)
                                    {
                                        Byte[] filearray = StringToByteArray(frmedit.getText());
                                        byte[] bytesize = BitConverter.GetBytes(filearray.Length);
                                        fileData[bb + 27] = bytesize[0];
                                        fileData[bb + 28] = bytesize[1];

                                        for (int i = 0; i < filearray.Length; i++)
                                            fileData[d + i] = filearray[i];
                                    }
                                    else if (frmedit.FileType == frmEditFile.EditorType.Text)
                                    {
                                        String text = frmedit.getText();
                                        byte[] bytesize = BitConverter.GetBytes(text.Length);
                                        fileData[bb + 27] = bytesize[0];
                                        fileData[bb + 28] = bytesize[1];

                                        for (int i = 0; i < text.Length; i++)
                                            fileData[d + i] = Convert.ToByte(text[i]);
                                    }

                                    cmd_ls();
                                }
                            }
                        }

                    }
                }
            }
        }

        void cmd_mkdir(string str)
        {

            int b = FST_LBA_START + 0x02;
            Byte[] diskbuffer = getByteArray(fileData, b * 512, 512);
            //int d = b * 512;
            int d = 0;
            while (diskbuffer[0] != 0x00)
            {
                b = b + FST_SECTORS_PER_DIR;
                diskbuffer = getByteArray(fileData, b * 512, 512);
            }

            int lbaCandidate = b;

            d = b * 512;

            fileData[d + 64] = Convert.ToByte(current_dir_LBA);

            //b++;

            int istr = 0;
            for (istr = 0; istr < str.Length; istr++)
                fileData[d + istr] = Convert.ToByte(str[istr]);
            fileData[d + istr] = 0x00;

            d = (current_dir_LBA + 1) * 512;
            while (fileData[d] != 0x00)
            {
                d = d + FST_ENTRY_SIZE;

            }

            for (istr = 0; istr < str.Length; istr++)
                fileData[d + istr] = Convert.ToByte(str[istr]);
            fileData[d + istr] = 0x00;
            fileData[d + 24] = 0b00000111; //  attributes
            fileData[d + 25] = Convert.ToByte(lbaCandidate); //LBA
            fileData[d + 27] = 0; //SIZE
            fileData[d + 28] = 0;//SIZE
            fileData[d + 29] = ConvertIntHexToByte(DateTime.Today.Day); //day
            fileData[d + 30] = ConvertIntHexToByte(DateTime.Today.Month); //momth
            fileData[d + 31] = ConvertIntHexToByte(DateTime.Today.Year - 2000); //year
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



        Byte ConvertIntHexToByte(int d)
        {
            return Convert.ToByte(d.ToString(), 16);

        }


        private static DialogResult ShowInputDialog(ref string input, String Title, Form owner)
        {
            System.Drawing.Size size = new System.Drawing.Size(200, 70);
            Form inputBox = new Form();
            inputBox.StartPosition = FormStartPosition.CenterParent;

            inputBox.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            inputBox.ClientSize = size;
            inputBox.Text = Title;

            System.Windows.Forms.TextBox textBox = new TextBox();
            textBox.Size = new System.Drawing.Size(size.Width - 10, 23);
            textBox.Location = new System.Drawing.Point(5, 5);
            textBox.Text = input;
            inputBox.Controls.Add(textBox);

            Button okButton = new Button();
            okButton.DialogResult = System.Windows.Forms.DialogResult.OK;
            okButton.Name = "okButton";
            okButton.Size = new System.Drawing.Size(75, 23);
            okButton.Text = "&OK";
            okButton.Location = new System.Drawing.Point(size.Width - 80 - 80, 39);
            inputBox.Controls.Add(okButton);

            Button cancelButton = new Button();
            cancelButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            cancelButton.Name = "cancelButton";
            cancelButton.Size = new System.Drawing.Size(75, 23);
            cancelButton.Text = "&Cancel";
            cancelButton.Location = new System.Drawing.Point(size.Width - 80, 39);
            inputBox.Controls.Add(cancelButton);

            inputBox.AcceptButton = okButton;
            inputBox.CancelButton = cancelButton;

            DialogResult result = inputBox.ShowDialog(owner);
            input = textBox.Text;
            return result;
        }


        int find_file(string title, int lba)
        {
            int a = lba;

            Byte[] diskbuffer = getByteArray(fileData, a * 512, 512);
            int d = 0;

            int index = 0;

            while (getStringFromByteArray(diskbuffer, d, 0x20) != title && index < FST_FILES_PER_DIR)
            {
                d = d + 0x20;
                index++;
                if (index == FST_FILES_PER_DIR)
                    return -1;
            }

            return d;
        }

        void rmdir(string title)
        {
            int lba = current_dir_LBA;//Convert.ToInt16(listView1.SelectedItems[0].Tag);

            int a = lba;
            a++;


            Byte[] diskbuffer = getByteArray(fileData, a * 512, 512);
            int d = find_file(title, a);

            Byte b = diskbuffer[d + 0x19];

            fileData[a * 512 + d] = 0x00;

            fileData[b * 512] = 0x00;


            cmd_ls();
        }


        public byte[] StringToByteArray(String hex)
        {
            int NumberChars = hex.Length;
            byte[] bytes = new byte[NumberChars / 2];
            for (int i = 0; i < NumberChars; i += 2)
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            return bytes;
        }

        void cmd_mktxt(string str, string data)
        {

            int b = FST_LBA_START + 0x02;
            Byte[] diskbuffer = getByteArray(fileData, b * 512, 512);
            //int d = b * 512;
            int d = 0;
            while (diskbuffer[0] != 0x00)
            {
                b = b + FST_SECTORS_PER_DIR;
                diskbuffer = getByteArray(fileData, b * 512, 512);
            }

            int lbaCandidate = b;

            d = b * 512;

            fileData[d + 64] = Convert.ToByte(current_dir_LBA);

            //b++;

            int istr = 0;
            for (istr = 0; istr < str.Length; istr++)
                fileData[d + istr] = Convert.ToByte(str[istr]);
            fileData[d + istr] = 0x00;

            d = (current_dir_LBA + 1) * 512;
            while (fileData[d] != 0x00)
            {
                d = d + FST_ENTRY_SIZE;

            }


            byte[] bytesize = BitConverter.GetBytes(data.Length);

            for (istr = 0; istr < str.Length; istr++)
                fileData[d + istr] = Convert.ToByte(str[istr]);
            fileData[d + istr] = 0x00;
            fileData[d + 24] = 0b00001110; //  attributes
            fileData[d + 25] = Convert.ToByte(lbaCandidate); //LBA
            fileData[d + 27] = bytesize[0];
            fileData[d + 28] = bytesize[1];
            fileData[d + 29] = ConvertIntHexToByte(DateTime.Today.Day); //day
            fileData[d + 30] = ConvertIntHexToByte(DateTime.Today.Month); //momth
            fileData[d + 31] = ConvertIntHexToByte(DateTime.Today.Year - 2000); //year


            int filestart = (b + 1) * 512;

            for (int i = 0; i < data.Length; i++)
                fileData[filestart + i] = Convert.ToByte(data[i]);
        }

        void cmd_mkbin(string str, string data)
        {

            int b = FST_LBA_START + 0x02;
            Byte[] diskbuffer = getByteArray(fileData, b * 512, 512);
            //int d = b * 512;
            int d = 0;
            while (diskbuffer[0] != 0x00)
            {
                b = b + FST_SECTORS_PER_DIR;
                diskbuffer = getByteArray(fileData, b * 512, 512);
            }

            int lbaCandidate = b;

            d = b * 512;

            fileData[d + 64] = Convert.ToByte(current_dir_LBA);

            //b++;

            int istr = 0;
            for (istr = 0; istr < str.Length; istr++)
                fileData[d + istr] = Convert.ToByte(str[istr]);
            fileData[d + istr] = 0x00;

            d = (current_dir_LBA + 1) * 512;
            while (fileData[d] != 0x00)
            {
                d = d + FST_ENTRY_SIZE;

            }

            Byte[] filearray = StringToByteArray(data);
            byte[] bytesize = BitConverter.GetBytes(filearray.Length);

            for (istr = 0; istr < str.Length; istr++)
                fileData[d + istr] = Convert.ToByte(str[istr]);
            fileData[d + istr] = 0x00;
            fileData[d + 24] = 0b00001110; //  attributes
            fileData[d + 25] = Convert.ToByte(lbaCandidate); //LBA
            fileData[d + 27] = bytesize[0];
            fileData[d + 28] = bytesize[1];
            fileData[d + 29] = ConvertIntHexToByte(DateTime.Today.Day); //day
            fileData[d + 30] = ConvertIntHexToByte(DateTime.Today.Month); //momth
            fileData[d + 31] = ConvertIntHexToByte(DateTime.Today.Year - 2000); //year


            int filestart = (b + 1) * 512;

            for (int i = 0; i < filearray.Length; i++)
                fileData[filestart + i] = filearray[i];
        }

        void cmd_mkbin(string str, byte[] filearray)
        {

            int b = FST_LBA_START + 0x02;
            Byte[] diskbuffer = getByteArray(fileData, b * 512, 512);
            //int d = b * 512;
            int d = 0;
            while (diskbuffer[0] != 0x00)
            {
                b = b + FST_SECTORS_PER_DIR;
                diskbuffer = getByteArray(fileData, b * 512, 512);
            }

            int lbaCandidate = b;

            d = b * 512;

            fileData[d + 64] = Convert.ToByte(current_dir_LBA);

            //b++;

            int istr = 0;
            for (istr = 0; istr < str.Length; istr++)
                fileData[d + istr] = Convert.ToByte(str[istr]);
            fileData[d + istr] = 0x00;

            d = (current_dir_LBA + 1) * 512;
            while (fileData[d] != 0x00)
            {
                d = d + FST_ENTRY_SIZE;

            }


            byte[] bytesize = BitConverter.GetBytes(filearray.Length);

            for (istr = 0; istr < str.Length; istr++)
                fileData[d + istr] = Convert.ToByte(str[istr]);
            fileData[d + istr] = 0x00;
            fileData[d + 24] = 0b00001110; //  attributes
            fileData[d + 25] = Convert.ToByte(lbaCandidate); //LBA
            fileData[d + 27] = bytesize[0];
            fileData[d + 28] = bytesize[1];
            fileData[d + 29] = ConvertIntHexToByte(DateTime.Today.Day); //day
            fileData[d + 30] = ConvertIntHexToByte(DateTime.Today.Month); //momth
            fileData[d + 31] = ConvertIntHexToByte(DateTime.Today.Year - 2000); //year


            int filestart = (b + 1) * 512;

            for (int i = 0; i < filearray.Length; i++)
                fileData[filestart + i] = filearray[i];
        }

        private void listView1_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete)
            {
                if (listView1.SelectedItems.Count == 1)
                {

                    DialogResult dialogResult = MessageBox.Show("Confirm Delete " + listView1.SelectedItems[0].SubItems["Type"].Text + " \"" + listView1.SelectedItems[0].Text + "\"?", "Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (dialogResult == DialogResult.Yes)
                        rmdir(listView1.SelectedItems[0].Text);
                }
            }

            else if (e.KeyCode == Keys.F2)
            {
                if (listView1.SelectedItems.Count == 1)
                {
                    listView1.SelectedItems[0].BeginEdit();
                }
            }

        }

        private void listView1_AfterLabelEdit(object sender, LabelEditEventArgs e)
        {
            if (e.Label != null)
            {
                string title = listView1.Items[e.Item].Text;
                int lba = Convert.ToUInt16(listView1.Items[e.Item].Tag);
                int parent_lba = fileData[lba * 512 + 0x40];
                parent_lba++;

                int b = parent_lba * 512 + find_file(title, parent_lba);
                int d = lba * 512;
                int i = 0;
                for (i = 0; i < e.Label.Length; i++)
                {
                    fileData[b + i] = Convert.ToByte(e.Label[i]);
                    fileData[d + i] = Convert.ToByte(e.Label[i]);
                }
                fileData[b + i] = 0x00;
                fileData[d + i] = 0x00;

            }
        }

        private void contextMenuStrip1_Opening(object sender, CancelEventArgs e)
        {

            newDirectoryToolStripMenuItem.Enabled = listView1.SelectedItems.Count == 0;
            newFileToolStripMenuItem.Enabled = listView1.SelectedItems.Count == 0;

            renameToolStripMenuItem.Enabled = listView1.SelectedItems.Count == 1;
            deleteToolStripMenuItem.Enabled = listView1.SelectedItems.Count == 1;
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
            DialogResult dialogResult = MessageBox.Show("Confirm Delete " + listView1.SelectedItems[0].SubItems["Type"].Text + " \"" + listView1.SelectedItems[0].Text + "\"?", "Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (dialogResult == DialogResult.Yes)
                rmdir(listView1.SelectedItems[0].Text);
        }

        private void listView1_ItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e)
        {
            int itemCount = listView1.SelectedItems.Count;
            int itemSize = 0;
            String itemSizeText = "";

            if (itemCount != 0)
            {
                foreach (ListViewItem item in listView1.SelectedItems)
                {
                    if (item.SubItems["Type"].Text == "File")
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
                statusSelectedCount.Text = "";


            newDirectoryToolStripMenuItem1.Enabled = listView1.SelectedItems.Count == 0;
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

        bool READ(String fileName)
        {
            if (File.Exists(fileName))
            {

                current_filename = fileName;
                current_dir_LBA = ROOT_LBA;
                FileInfo fi = new FileInfo(fileName);

                fileData = new byte[fi.Length];

                using (BinaryReader b = new BinaryReader(
                File.Open(fileName, FileMode.Open)))
                {
                    // 2.
                    // Position and length variables.
                    int pos = 0;
                    // 2A.
                    // Use BaseStream.
                    int length = (int)b.BaseStream.Length;
                    while (pos < length)
                    {

                        fileData[pos] = b.ReadByte();
                        // 3.
                        // Read integer.
                        //int v = b.ReadInt32();
                        //Console.WriteLine(v);

                        // 4.
                        // Advance our position variable.
                        pos += sizeof(byte);
                    }
                }

                cmd_ls();
                return true;
            }
            return false;
        }

        private void openImageToolStripMenuItem_Click(object sender, EventArgs e)
        {


            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                string filename = openFileDialog1.FileName;

                IniFile ini = new IniFile(System.Environment.CurrentDirectory + "\\" + "config.ini");
                ini.IniWriteValue("general", "last_open", filename);

                if (READ(filename))
                {
                    this.Text = filename;
                    READ(filename);

                }
            }
        }

        private void saveImageToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (current_filename != "")
            {
                if (MessageBox.Show("Save file?", "Confirm save", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    WRITE(current_filename);
                }
            };

        }

        bool WRITE(string fileName)
        {
            using (BinaryWriter b = new BinaryWriter(File.Open(fileName, FileMode.Create)))
            {
                for (int i = 0; i < fileData.Length; i++)
                    b.Write(fileData[i]);
            }

            return true;
        }

        private void newDirectoryToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            String str = "";
            if (ShowInputDialog(ref str, "Directory Name", this) == DialogResult.OK)
                if (str.Trim() != "")
                {
                    cmd_mkdir(str);

                    cmd_ls();
                }
        }

        private void deleteToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            if (listView1.SelectedItems.Count == 1)
            {
                DialogResult dialogResult = MessageBox.Show("Confirm Delete " + listView1.SelectedItems[0].SubItems["Type"].Text + " \"" + listView1.SelectedItems[0].Text + "\"?", "Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (dialogResult == DialogResult.Yes)
                    rmdir(listView1.SelectedItems[0].Text);
            }
        }

        private void textToolStripMenuItem_Click(object sender, EventArgs e)
        {
            String str = "";

            if (ShowInputDialog(ref str, "File Name", this) == DialogResult.OK)
                if (str.Trim() != "")
                {


                    frmEditFile frmedit = new frmEditFile();
                    frmedit.setTitle("New Text File: " + str);
                    frmedit.ShowUndo = false;
                    frmedit.ShowEditorType = false;
                    frmedit.setText("");
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

            if (ShowInputDialog(ref str, "File Name", this) == DialogResult.OK)
                if (str.Trim() != "")
                {


                    frmEditFile frmedit = new frmEditFile();
                    frmedit.setTitle("New Binary File: " + str);
                    frmedit.ShowUndo = false;
                    frmedit.ShowEditorType = false;
                    frmedit.setText("");
                    frmedit.ShowDialog(this);

                    DialogResult dialogResult = MessageBox.Show("Save New File \"" + str + "\"?", "Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (dialogResult == DialogResult.Yes)
                    {
                        cmd_mkbin(str, frmedit.getText());
                        cmd_ls();

                    }

                }
        }

        private void textToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            String str = "";

            if (ShowInputDialog(ref str, "File Name", this) == DialogResult.OK)
                if (str.Trim() != "")
                {


                    frmEditFile frmedit = new frmEditFile();
                    frmedit.setTitle("New Text File: " + str);
                    frmedit.ShowUndo = false;
                    frmedit.ShowEditorType = false;
                    frmedit.setText("");
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

            if (ShowInputDialog(ref str, "File Binary Name", this) == DialogResult.OK)
                if (str.Trim() != "")
                {


                    frmEditFile frmedit = new frmEditFile();
                    frmedit.setTitle("New File: " + str);
                    frmedit.ShowUndo = false;
                    frmedit.ShowEditorType = false;
                    frmedit.setText("");
                    frmedit.ShowDialog(this);

                    DialogResult dialogResult = MessageBox.Show("Save New File \"" + str + "\"?", "Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (dialogResult == DialogResult.Yes)
                    {
                        cmd_mkbin(str, frmedit.getText());
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
                        Byte[] data = ReadAllBytes(b);
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
                this.Text = filename;
                READ(filename);
            }
        }

        private void saveAsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (saveFileDialog1.ShowDialog() == DialogResult.OK)
            {
                string filename = saveFileDialog1.FileName;
                if (WRITE(filename))
                    this.Text = filename;
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

        private void newDirectoryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            String str = "";
            if (ShowInputDialog(ref str, "Directory Name", this) == DialogResult.OK)
                if (str.Trim() != "")
                {
                    cmd_mkdir(str);

                    cmd_ls();
                }
        }
    }
}

