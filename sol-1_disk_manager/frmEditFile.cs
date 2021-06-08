using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace sol_1_disk_manager
{
    public partial class frmEditFile : Form
    {
        bool savekey = false;
        int oldcombo = -1;

        string originalfile = "";

        public enum EditorType
        {
            Binary,
            Text
        }
        EditorType _filetype = EditorType.Binary;
        public EditorType FileType
        {
            get { return _filetype; }
            set
            {
                _filetype = value;
                toolStripComboBox1.SelectedIndex = (int)value;
            }
        }




        public bool ShowUndo
        {
            get { return undoToolStripMenuItem.Visible; }
            set { undoToolStripMenuItem.Visible = value; }
        }

        public bool ShowEditorType
        {
            get { return toolStripComboBox1.Visible; }
            set { toolStripComboBox1.Visible = value; }
        }


        public frmEditFile()
        {
            InitializeComponent();

            FileType = EditorType.Binary;
            undoToolStripMenuItem.Enabled = false;
        }

        public bool getSaveKeyHit()
        {
            return savekey;

        }


        public void setTitle(String text)
        {
            this.Text = text;

        }
        public void setText(String text)
        {
            originalfile = text;
            textBox1.Text = text;
        }

        public string getText()
        {
            return textBox1.Text;
        }

        private void textBox1_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Control && (e.KeyValue == (int)'s' || e.KeyValue == (int)'S'))
            {
                savekey = true;
                this.Close();
            }

            undoToolStripMenuItem.Enabled = (textBox1.Text != originalfile);

        }



        public byte[] StringToByteArray(String hex)
        {
            int NumberChars = hex.Length;
            byte[] bytes = new byte[NumberChars / 2];
            for (int i = 0; i < NumberChars; i += 2)
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            return bytes;
        }

        private void toolStripComboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {

            if (toolStripComboBox1.SelectedIndex == 0)
            {
                FileType = EditorType.Binary;

                if (oldcombo == 1)
                {
                    try
                    {

                        Byte[] filearray = StringToByteArray(this.getText());
                        string file = "";
                        foreach (char a in textBox1.Text.ToCharArray())
                        {
                            file += Convert.ToByte(a).ToString("X2");
                        }
                        textBox1.Text = file;
                    }
                    catch
                    {
                        toolStripComboBox1.SelectedIndex = 1;
                    }
                }


                oldcombo = toolStripComboBox1.SelectedIndex;
            }
            else if (toolStripComboBox1.SelectedIndex == 1)
            {
                FileType = EditorType.Text;

                if (oldcombo == 0)
                {
                    Byte[] filearray = StringToByteArray(this.getText());
                    textBox1.Text = new string(filearray.Select(byteValue => Convert.ToChar(byteValue)).ToArray());
                }
                oldcombo = toolStripComboBox1.SelectedIndex;
            }
            else
            {
                toolStripComboBox1.SelectedIndex = 0;
            }
        }

        private void undoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            textBox1.Text = originalfile;
        }

        private void closeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
