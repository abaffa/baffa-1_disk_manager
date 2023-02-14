using RawDiskLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace sol_1_disk_manager
{





    public enum DiskImageFormat
    {
        _4MB
    }
    public class DiskImage
    {
        const int FST_ENTRY_SIZE = 32;
        const int FST_FILES_PER_DIR = 16;
        const int FST_SECTOR_SIZE = 0x200;

        const int FS_SECTORS_PER_FILE = 32;             // the first sector is always a header with a NULL parameter(first byte)
                                                        // so that we know which blocks are free or taken


        const int FST_FILES_PER_SECT = FST_SECTOR_SIZE / FST_ENTRY_SIZE; // DIR FILE ENTRIES PER SEC    
                                                                         // 1 sector for header, the rest is for the list of files/dirs
        const int FST_SECTORS_PER_DIR = (1 + (FST_ENTRY_SIZE * FST_FILES_PER_DIR / FST_SECTOR_SIZE));

        const int FS_FILE_DIR_SIZE = (FST_SECTORS_PER_DIR + FS_SECTORS_PER_FILE) * 512;

        const int FS_FILE_SIZE = (FS_SECTORS_PER_FILE * 512);




        const int FST_NBR_DIRECTORIES = 64;
        const int FST_TOTAL_SECTORS = (FST_SECTORS_PER_DIR * FST_NBR_DIRECTORIES);
        const int FST_LBA_START = 32;
        const int FST_LBA_END = (FST_LBA_START + FST_TOTAL_SECTORS - 1);

        const int FS_NBR_FILES = (FST_NBR_DIRECTORIES * FST_FILES_PER_DIR);

        const int FS_TOTAL_SECTORS = (FS_NBR_FILES * FS_SECTORS_PER_FILE);
        const int FS_LBA_START = (FST_LBA_END + 1);
        const int FS_LBA_END = (FS_LBA_START + FS_NBR_FILES - 1);

        public const int ROOT_LBA = FST_LBA_START;

        //attributes (1)	|_|_|file_type(3bits)|x|w|r| types: file, directory, character device
        const byte ATTR_R = 0b00000001;
        const byte ATTR_W = 0b00000010;
        const byte ATTR_X = 0b00000100;


        const byte ATTR_DIR = 0b00001000;
        const byte ATTR_FILE = 0b000010000;
        const byte ATTR_DEV = 0b000100000;

        //const int CF_CARD_LBA_SIZE = 0x800;         // temporary small size

        //const int DISK_SIZE = 0x800000;

        Byte[] fileData;
        
        Dictionary<string, List<FileEntry>> file_entries = new Dictionary<string, List<FileEntry>>();
        List<FileEntry> fileEntryList = new List<FileEntry>();

        //Directory directory;
        

        int _current_dir_LBA = 0;
        public int current_dir_LBA { get { if (_current_dir_LBA < ROOT_LBA) return ROOT_LBA; return _current_dir_LBA; } set { _current_dir_LBA = value; } }

        //public Directory Directory { get { return this.directory; } }

        public DiskImageFormat DiskImageFormat { get; set; }

        int clusters_4mb = 0x2000;       //   4MB Image

        int default_disksize = 0x400000;
        int default_firstdiskstart = 0;

        public Stack<int> parentStack = new Stack<int>();

        public DiskImage()
        {
            DiskImageFormat = DiskImageFormat._4MB;
            current_dir_LBA = FST_LBA_START;

            SetFormat(DiskImageFormat);
        }

        public DiskImage(DiskImageFormat _imageSize)
        {
            DiskImageFormat = _imageSize;
            current_dir_LBA = FST_LBA_START;

            SetFormat(DiskImageFormat);
        }


        private int UpdateCurrentDiskStart()
        {

            int current_disksize = default_disksize;
            //disk_start = default_firstdiskstart;
            current_disksize = default_disksize;

            return current_disksize;
        }


        public void SetFormat(DiskImageFormat _diskImageSize)
        {
            int clusters = clusters_4mb;
            //directory = new Directory();

            DiskImageFormat = _diskImageSize;

            parentStack.Clear();

            fileData = new byte[512 * clusters];

            current_dir_LBA = FST_LBA_START;

            UpdateCurrentDiskStart();


            createEmptyFileEntries();

            UpdateDiskList();
        }


        public void NewImage(DiskImageFormat _diskImageSize)
        {
            SetFormat(_diskImageSize);
        }


        private void createEmptyFileEntries()
        {
            UpdateCurrentDiskStart();

            for (int file_entry = current_dir_LBA; file_entry < current_dir_LBA + default_firstdiskstart; file_entry += 0x20)
            {
                fileData[file_entry] = 0xe5; //empty entry

                for (int i = 1; i < 0x0C; i++)
                    fileData[file_entry + i] = 0x20; //noname file
            }
        }

        public void UpdateDiskList()
        {
            UpdateCurrentDiskStart();
            
        }

        public void cmd_cd(int new_lba)
        {

            if (new_lba <= ROOT_LBA)
                current_dir_LBA = parentStack.Pop();
            else
            {
                parentStack.Push(current_dir_LBA);
                current_dir_LBA = new_lba;
            }

            UpdateDiskList();
        }

        public List<FileEntry> current_file_list()
        {
            return fileEntryList;
        }
        public List<FileEntry> get_file_entries(int dir_LBA)
        {
            int size = FST_ENTRY_SIZE * FST_FILES_PER_DIR;
            int start = (dir_LBA + 1) * FST_SECTOR_SIZE;
            if (fileData.Length - start < size)
                size = fileData.Length - start;

            Byte[] _folderData = new byte[size];
            Buffer.BlockCopy(fileData, start, _folderData, 0, size);

            /////////////////////////////////////////
            ///
            List<FileEntry> _FAT = new List<FileEntry>();
            file_entries.Clear();

            //int size = FST_ENTRY_SIZE * FST_FILES_PER_DIR;


            for (int d = 0; d < size; d += 0x20)
            {
                if (_folderData[d] != 0x00)
                {

                    FileEntry f = new FileEntry()
                    {
                        _entry = d,

                        _name = Utils.getStringFromByteArray(_folderData, d, 24),
                        _attributes = _folderData[d + 24],
                        _size = (_folderData[d + 28] << 8) + _folderData[d + 27],
                        _start = (_folderData[d + 26] << 8) + _folderData[d + 25],
                        _creation_date = new byte[] { _folderData[d + 29], _folderData[d + 30], _folderData[d + 31] },
                    };


                    string filename = Utils.getStringFromByteArray(_folderData, d, 24);
                    if (!file_entries.ContainsKey(filename))
                    {
                        _FAT.Add(f);
                        file_entries.Add(filename, new List<FileEntry>(new FileEntry[] { f }));
                    }
                    else
                    {
                        file_entries[filename].Add(f);
                    }
                }
            }

            return _FAT;
        }
        public List<FileEntry> cmd_ls()
        {
            fileEntryList = get_file_entries(current_dir_LBA);
            return fileEntryList;
        }



        int find_file(string title, int lba)
        {
            int b = lba;
            Byte[] diskbuffer = new byte[FST_SECTOR_SIZE];
            Buffer.BlockCopy(fileData, b * FST_SECTOR_SIZE, diskbuffer, 0, FST_SECTOR_SIZE);

            int d = 0;

            int index = 0;

            while (Utils.getStringFromByteArray(diskbuffer, d, 0x20) != title && index < FST_FILES_PER_DIR)
            {
                d = d + 0x20;
                index++;
                if (index == FST_FILES_PER_DIR)
                    return -1;
            }

            return d;
        }


        public void cmd_rmdir(string filename)
        {
            int start = current_dir_LBA + 1;

            cmd_rmdir(filename, start);
        }

        private void cmd_rmdir(string filename, int candidate_dir_start)
        {

            int d = find_file(filename, candidate_dir_start);

            if (d >= 0)
            {
                d = d + (candidate_dir_start * FST_SECTOR_SIZE);


                Byte _attributes = fileData[d + 24];
                String _name = Utils.getStringFromByteArray(fileData, d, 24);
                int _child_start = (fileData[d + 26] << 8) + fileData[d + 25];

                if ((_attributes & ATTR_DIR) != 0x00)
                {
                    List<FileEntry> _fat = get_file_entries(_child_start);
                    foreach(FileEntry f in _fat) { 
                        if (_child_start != f._start && candidate_dir_start != (f._start + 1)) {
                            Console.WriteLine(_name + " > " + f._name);
                            cmd_rmdir(f._name, _child_start + 1);
                        }
                    }
                }

                fileData[d] = 0x00;
                fileData[_child_start * FST_SECTOR_SIZE] = 0x00;
            }

            
        }


        private int get_free_lba_dir_entry()
        {
            int b = FST_LBA_START + 0x02;

            Byte[] diskbuffer = new byte[FST_SECTOR_SIZE];
            Buffer.BlockCopy(fileData, b * FST_SECTOR_SIZE, diskbuffer, 0, FST_SECTOR_SIZE);

            while (diskbuffer[0] != 0x00)
            {
                b = b + FST_SECTORS_PER_DIR;
                Buffer.BlockCopy(fileData, b * 512, diskbuffer, 0, 512);
            }

            return b;
        }

        private int get_free_lba_file_entry()
        {
            int b = FST_LBA_START + 0x02;

            Byte[] diskbuffer = new byte[FST_SECTOR_SIZE];
            Buffer.BlockCopy(fileData, b * FST_SECTOR_SIZE, diskbuffer, 0, FST_SECTOR_SIZE);

            while (diskbuffer[0] != 0x00)
            {
                b = b + FS_SECTORS_PER_FILE;
                Buffer.BlockCopy(fileData, b * 512, diskbuffer, 0, 512);
            }

            return b;
        }


        public void cmd_mkdir(string str)
        {
            if (// str.ToArray().Where(p => p == '.').Count() <= 1 &&
                str.IndexOf('\0') == -1
                && str.IndexOf('\\') == -1
                && str.IndexOf('/') == -1
                && str.IndexOf(':') == -1
                && str.IndexOf('*') == -1
                && str.IndexOf('?') == -1
                && str.IndexOf('\'') == -1
                && str.IndexOf('\"') == -1
                && str.IndexOf('<') == -1
                && str.IndexOf('>') == -1
                && str.IndexOf('|') == -1
                && str.Trim().IndexOf(' ') == -1)
            {

                int lbaCandidate = get_free_lba_dir_entry();
                int start = current_dir_LBA + 1;

                int d = lbaCandidate * FST_SECTOR_SIZE;

                //Create Directory Entry List
                fileData[d + 64] = Convert.ToByte(current_dir_LBA);

                int istr = 0;
                for (istr = 0; istr < str.Length; istr++) fileData[d + istr] = Convert.ToByte(str[istr]);
                for (; istr < 24; istr++) fileData[d + istr] = 0x00;

                //Reset pointer to current directory
                d = (start) * 512;

                //Find Empty Entry in current Directory
                while (fileData[d] != 0x00)
                {
                    d = d + FST_ENTRY_SIZE;

                }

                //Add Entry to current Directory
                for (istr = 0; istr < str.Length; istr++)
                    fileData[d + istr] = Convert.ToByte(str[istr]);
                for (; istr < 24; istr++) fileData[d + istr] = 0x00;
                fileData[d + 24] = ATTR_R | ATTR_W | ATTR_DIR; //  attributes
                fileData[d + 25] = Convert.ToByte(lbaCandidate % 0x0100); //LBA
                fileData[d + 26] = Convert.ToByte(lbaCandidate / 0x0100); //LBA
                fileData[d + 27] = 0; //SIZE
                fileData[d + 28] = 0;//SIZE
                fileData[d + 29] = Utils.ConvertIntHexToByte(DateTime.Today.Day); //day
                fileData[d + 30] = Utils.ConvertIntHexToByte(DateTime.Today.Month); //momth
                fileData[d + 31] = Utils.ConvertIntHexToByte(DateTime.Today.Year - 2000); //year

                


                int filestart = (lbaCandidate + 1) * 512;

                for (int i = 0; i < FST_ENTRY_SIZE * FST_FILES_PER_DIR; i++)
                    fileData[filestart + i] = 0x00;


                


                fileData[filestart + 0] = (byte)'.';
                fileData[filestart + 1] = (byte)'.';
                fileData[filestart + 24] = 0x0b; //  attributes
                fileData[filestart + 25] = Convert.ToByte(current_dir_LBA % 0x0100); //LBA
                fileData[filestart + 26] = Convert.ToByte(current_dir_LBA / 0x0100); //LBA
                fileData[filestart + 27] = 0;
                fileData[filestart + 28] = 0;
                fileData[filestart + 29] = Utils.ConvertIntHexToByte(DateTime.Today.Day); //day
                fileData[filestart + 30] = Utils.ConvertIntHexToByte(DateTime.Today.Month); //momth
                fileData[filestart + 31] = Utils.ConvertIntHexToByte(DateTime.Today.Year - 2000); //year

                fileData[filestart + 32 + 0] = (byte)'.';
                fileData[filestart + 32 + 24] = 0x0b; //  attributes
                fileData[filestart + 32 + 25] = Convert.ToByte(lbaCandidate % 0x0100); //LBA
                fileData[filestart + 32 + 26] = Convert.ToByte(lbaCandidate / 0x0100); //LBA
                fileData[filestart + 32 + 27] = 0;
                fileData[filestart + 32 + 28] = 0;
                fileData[filestart + 32 + 29] = Utils.ConvertIntHexToByte(DateTime.Today.Day); //day
                fileData[filestart + 32 + 30] = Utils.ConvertIntHexToByte(DateTime.Today.Month); //momth
                fileData[filestart + 32 + 31] = Utils.ConvertIntHexToByte(DateTime.Today.Year - 2000); //year

                this.UpdateDiskList();
            }
        }


        public void cmd_mkbin(string str, byte[] data)
        {
            string newname = str;

            if (//newname.ToArray().Where(p => p == '.').Count() <= 1 &&
                 newname.IndexOf('\0') == -1
                && newname.IndexOf('\\') == -1
                && newname.IndexOf('/') == -1
                && newname.IndexOf(':') == -1
                && newname.IndexOf('*') == -1
                && newname.IndexOf('?') == -1
                && newname.IndexOf('\'') == -1
                && newname.IndexOf('\"') == -1
                && newname.IndexOf('<') == -1
                && newname.IndexOf('>') == -1
                && newname.IndexOf('|') == -1
                && newname.Trim().IndexOf(' ') == -1)
            {
                int lbaCandidate = get_free_lba_file_entry();

                int d = lbaCandidate * FST_SECTOR_SIZE;
                int start = current_dir_LBA + 1;

                d = lbaCandidate * 512;

                fileData[d + 64] = Convert.ToByte(current_dir_LBA);

                //b++;

                int istr = 0;
                for (istr = 0; istr < str.Length; istr++)
                    fileData[d + istr] = Convert.ToByte(str[istr]);
                for (; istr < 24; istr++) fileData[d + istr] = 0x00;

                d = (start) * 512;
                while (fileData[d] != 0x00)
                {
                    d = d + FST_ENTRY_SIZE;

                }


                byte[] bytesize = BitConverter.GetBytes(data.Length);

                for (istr = 0; istr < str.Length; istr++)
                    fileData[d + istr] = Convert.ToByte(str[istr]);
                fileData[d + istr] = 0x00;
                fileData[d + 24] = ATTR_R | ATTR_W | ATTR_X;// | ATTR_FILE; //  attributes
                fileData[d + 25] = Convert.ToByte(lbaCandidate % 0x0100); //LBA
                fileData[d + 26] = Convert.ToByte(lbaCandidate / 0x0100); //LBA
                fileData[d + 27] = bytesize[0];
                fileData[d + 28] = bytesize[1];
                fileData[d + 29] = Utils.ConvertIntHexToByte(DateTime.Today.Day); //day
                fileData[d + 30] = Utils.ConvertIntHexToByte(DateTime.Today.Month); //momth
                fileData[d + 31] = Utils.ConvertIntHexToByte(DateTime.Today.Year - 2000); //year


                int filestart = (lbaCandidate + 1) * 512;

                for (int i = 0; i < data.Length; i++)
                    fileData[filestart + i] = data[i];

                this.UpdateDiskList();
            }
        }


        public bool cmd_rename(String filename, String newname)
        {
            if (newname.ToArray().Where(p => p == '.').Count() <= 1
                && newname.IndexOf('\0') == -1
                && newname.IndexOf('\\') == -1
                && newname.IndexOf('/') == -1
                && newname.IndexOf(':') == -1
                && newname.IndexOf('*') == -1
                && newname.IndexOf('?') == -1
                && newname.IndexOf('\'') == -1
                && newname.IndexOf('\"') == -1
                && newname.IndexOf('<') == -1
                && newname.IndexOf('>') == -1
                && newname.IndexOf('|') == -1
                && newname.IndexOf(',') == -1
                && newname.IndexOf(';') == -1
                && newname.IndexOf('=') == -1
                && newname.IndexOf('[') == -1
                && newname.IndexOf(']') == -1
                && newname.IndexOf('(') == -1
                && newname.IndexOf(')') == -1
                && newname.IndexOf('%') == -1
                && newname.Trim().IndexOf(' ') == -1)
            {
                String name = newname;

                int start = current_dir_LBA + 1;

                int d = find_file(filename, start);

                if (d >= 0)
                {
                    // Rename in current dir list
                    d = d + (start * FST_SECTOR_SIZE);
                    int istr = 0;
                    for (; istr < name.Length && istr < 24; istr++) fileData[d + istr] = Convert.ToByte(name[istr]);
                    for (; istr < 24; istr++) fileData[d + istr] = 0x00;

                    // Rename in Data entry
                    int _start = (fileData[d + 26] << 8) + fileData[d + 25];
                    d = (_start * FST_SECTOR_SIZE);
                    istr = 0;
                    for (istr = 0; istr < name.Length; istr++) fileData[d + istr] = Convert.ToByte(name[istr]);
                    for (; istr < 24; istr++) fileData[d + istr] = 0x00;
                }

                //name = name.Length > 0 ? name.Substring(0, Math.Min(name.Length, 8)) : "";
                //ext = ext.Length > 0 ? ext.Substring(0, Math.Min(ext.Length, 3)) : "";

                //name = name.PadRight(8);
                //ext = ext.PadRight(3);

                //directory.RenameFile(filename, name, ext);

                //foreach (FileEntry f in directory.GetFileEntries(name + ext))
                //{
                //    Buffer.BlockCopy(f.GetDataEntry(), 0, fileData, current_dir_LBA + f._entry, 32);
                //}

                

                return true;
            }

            return false;
        }

        public byte[] cmd_get_file(string filename)
        {

            byte[] content = null;
            int start = current_dir_LBA + 1;

            int d = find_file(filename, start);

            if (d >= 0)
            {
                d = d + (start * FST_SECTOR_SIZE);

                int _size = (fileData[d + 28] << 8) + fileData[d + 27];
                int _start = (fileData[d + 26] << 8) + fileData[d + 25];
                _start++;

                content = new byte[_size];

                Buffer.BlockCopy(fileData, _start * FST_SECTOR_SIZE, content, 0, _size);

            }
            return content;

        }

        public bool ReadImageFile(String fileName, ToolStripProgressBar progressBar = null)
        {
            try
            {
                if (File.Exists(fileName))
                {

                    FileInfo fi = new FileInfo(fileName);

                    DiskImageFormat = DiskImageFormat._4MB;

                    SetFormat(DiskImageFormat);
                    //directory = new Directory();


                    //fileData = new byte[fi.Length];

                    if (progressBar != null)
                    {
                        progressBar.Minimum = 0;
                        progressBar.Maximum = (int)fi.Length;
                        progressBar.Visible = true;
                    }


                    using (BinaryReader b = new BinaryReader(
                    File.Open(fileName, FileMode.Open)))
                    {
                        // 2.
                        // Position and length variables.
                        int pos = 0;
                        // 2A.
                        // Use BaseStream.
                        int length = (int)b.BaseStream.Length;
                        while (pos < length && pos < fileData.Length)
                        {

                            fileData[pos] = b.ReadByte();
                            // 3.
                            // Read integer.
                            //int v = b.ReadInt32();
                            //Console.WriteLine(v);

                            // 4.
                            // Advance our position variable.

                            if (progressBar != null)
                            {
                                if (pos % 1000000 == 0)
                                {
                                    progressBar.Value = pos;
                                    Application.DoEvents();
                                }
                            }

                            pos += sizeof(byte);
                        }
                    }

                    if (progressBar != null)
                        progressBar.Visible = false;

                    UpdateCurrentDiskStart();

                    this.UpdateDiskList();
                    return true;
                }
            }
            catch (Exception Ex)
            {
                MessageBox.Show("An error occurred while reading the image file.\nPlease check the file.", "Reading Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            if (progressBar != null)
                progressBar.Visible = false;

            return false;
        }


        public bool SaveImageFile(string fileName, ToolStripProgressBar progressBar = null)
        {
            try
            {
                if (progressBar != null)
                {
                    progressBar.Minimum = 0;
                    progressBar.Maximum = (int)fileData.Length;
                    progressBar.Visible = true;
                }


                using (BinaryWriter b = new BinaryWriter(File.Open(fileName, FileMode.Create)))
                {
                    for (int pos = 0; pos < fileData.Length; pos++)
                    {
                        b.Write(fileData[pos]);
                        if (progressBar != null)
                        {
                            if (pos % 1000000 == 0)
                            {
                                progressBar.Value = pos;
                                Application.DoEvents();
                            }
                        }
                    }
                }


                if (progressBar != null)
                    progressBar.Visible = false;

                return true;

            }
            catch
            {
                MessageBox.Show("An error occurred while writing the image file.\nPlease check the file.", "Writing Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            if (progressBar != null)
                progressBar.Visible = false;

            return false;
        }


        public void ReadRawDisk(int selectedVol)
        {
            try
            {
                int clusters = DiskImageFormat == DiskImageFormat._4MB ? clusters_4mb : clusters_4mb;

                using (RawDisk disk = new RawDisk(DiskNumberType.Volume, selectedVol, FileAccess.ReadWrite))
                {
                    fileData = new byte[512 * clusters];
                    fileData = disk.ReadClusters(0, clusters);
                }

                UpdateCurrentDiskStart();

                this.UpdateDiskList();

            }
            catch
            {
                MessageBox.Show("An error occurred while reading disk.", "Reading Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void WriteRawDisk(int selectedVol)
        {
            try
            {
                using (RawDisk disk = new RawDisk(DiskNumberType.Volume, selectedVol, FileAccess.ReadWrite))
                {
                    disk.WriteClusters(fileData, 0);
                }
            }
            catch
            {
                MessageBox.Show("An error occurred while writing the disk.", "Writing Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }




        public Byte[] get_boot()
        {
            return fileData.Take(0x400).ToArray();
        }

        public void set_boot(Byte[] filearray)
        {
            int i = 0;
            for (; i < filearray.Length && i < 0x400; i++)
                fileData[i] = filearray[i];

            for (; i < 0x400; i++)
                fileData[i] = 0x00;
        }


        public Byte[] get_kernel()
        {
            return fileData.Skip(0x400).Take(0x4000 - 0x400).ToArray();
        }

        public void set_kernel(Byte[] filearray)
        {
            int i = 0;
            for (; i < filearray.Length && (i + 0x400) < 0x4000; i++)
                fileData[i + 0x400] = filearray[i];

            for (; i < 0x4000; i++)
                fileData[i + 0x400] = 0x00;
        }
    }
}
