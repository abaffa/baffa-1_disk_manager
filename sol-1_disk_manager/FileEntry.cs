using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sol_1_disk_manager
{
    // filename    24 [0-23]
    // attributes   1 [24]
    // lba entry    2 [25-26]
    // size         2 [27-28]
    // creation dt  3 [29-31]

    public class FileEntry
    {
        public int _entry { get; set; }

        public String _name { get; set; }

        public byte _attributes { get; set; }

        public int _start { get; set; }

        public int _size { get; set; }

        public byte[] _creation_date { get; set; }

        public byte[] GetDataEntry()
        {
            byte[] ret = new byte[32];
            string name = _name.PadRight(24);

            // filename    24 [0-23]
            for (int i = 0; i < 24; i++)
                ret[i] = (byte)name[i];

            // attributes   1 [24]
            ret[0x18] = _attributes;

            // lba entry    2 [25-26]
            ret[0x19] = (byte)(_start & 0b11111111);
            ret[0x1A] = (byte)((_start >> 8) & 0b11111111);

            // size         2 [27-28]
            ret[0x1B] = (byte)(_size & 0b11111111);
            ret[0x1C] = (byte)((_size >> 8) & 0b11111111);


            // creation dt  3 [29-31]
            ret[0x1D] = _creation_date[0];
            ret[0x1E] = _creation_date[1];
            ret[0x1F] = _creation_date[2];
            return ret;
        }

        
        public static int CalcNumOfBlocks(int size)
        {
            return (int)Math.Ceiling((decimal)size / 0x1000);
        }
        
    }
}
