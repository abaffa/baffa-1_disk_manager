using sol_1_disk_manager.tasm;
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
        string originaltextfile = "";

        string assembly = "";
        string ascii_bytes = "";
        string dissassembly = "";


        int _org = 0;

        public int Start_Address
        {
            get { return _org; }
            set { _org = value; }
        }
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

                disassemblyToolStripMenuItem.Visible = _filetype == EditorType.Binary;
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

            if (_filetype == EditorType.Binary)
            {
                originalfile = text;
                originaltextfile = stringByteToText(originalfile);
                disassemblyToolStripMenuItem.Visible = true;
            }
            else if (_filetype == EditorType.Text)
            {
                originaltextfile = text;
                originalfile = textToStringByte(originalfile);
                disassemblyToolStripMenuItem.Visible = false;
            }

            assembly = originalfile;
            ascii_bytes = originaltextfile;

            if (_filetype == EditorType.Binary)
                textBox1.Text = assembly;
            else if (_filetype == EditorType.Text)
                textBox1.Text = ascii_bytes;

            textBox1.ReadOnly = false;
        }

        public string getText()
        {
            if (_filetype == EditorType.Binary)
            {
                return textBox1.Text;
            }
            else if (_filetype == EditorType.Text)
            {
                return textToStringByte(textBox1.Text);
            }

            return "";
        }

        private void textBox1_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Control && (e.KeyValue == (int)'s' || e.KeyValue == (int)'S'))
            {
                savekey = true;
                this.Close();
            }


            if (disassemblyToolStripMenuItem.Text == "Disassembly")
            {
                if (_filetype == EditorType.Binary)
                {
                    undoToolStripMenuItem.Enabled = (textBox1.Text != originalfile);
                }
                else if (_filetype == EditorType.Text)
                {
                    undoToolStripMenuItem.Enabled = (textBox1.Text != originaltextfile);
                }
            }

        }

        private string stringByteToText(String bytetext)
        {
            Byte[] filearray = StringToByteArray(bytetext);

            string file = "";
            foreach (Byte b in filearray)
            {
                file += Convert.ToChar(b);
            }

            file = file.Replace("\r", "");
            file = file.Replace("\n", "\r\n");

            return file;
        }

        private string textToStringByte(String _text)
        {
            string file = "";
            foreach (char a in _text.ToCharArray())
            {
                file += Convert.ToByte(a).ToString("X2");
            }

            return file;
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
                disassemblyToolStripMenuItem.Visible = true;
                if (oldcombo == 1)
                {
                    try
                    {
                        if (textBox1.Text != ascii_bytes)
                        {
                            assembly = textToStringByte(textBox1.Text);
                            textBox1.Text = assembly;
                        }
                        else
                            textBox1.Text = assembly;
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
                disassemblyToolStripMenuItem.Visible = false;

                if (oldcombo == 0)
                {
                    if (textBox1.Text != assembly || ascii_bytes == "")
                    {
                        ascii_bytes = stringByteToText(textBox1.Text);
                        textBox1.Text = ascii_bytes;
                    }
                    else
                        textBox1.Text = ascii_bytes;


                }
                oldcombo = toolStripComboBox1.SelectedIndex;
            }
            else
            {
                toolStripComboBox1.SelectedIndex = 0;
                disassemblyToolStripMenuItem.Visible = true;
            }
        }

        private void undoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            assembly = originalfile;
            ascii_bytes = originaltextfile;

            if (_filetype == EditorType.Binary)
            {
                textBox1.Text = assembly;
            }
            else if (_filetype == EditorType.Text)
            {
                textBox1.Text = ascii_bytes;
            }
        }

        private void closeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void disassemblyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_filetype == EditorType.Binary)
            {
                if (disassemblyToolStripMenuItem.Text == "Disassembly")
                {
                    assembly = textBox1.Text;
                    dissassembly = "";
                    Dictionary<String, Tasm_Opcode> opcode_list = Tasm_Opcode.load();

                    for (int i = 0; i < assembly.Length;)
                    {
                        String current = assembly[i].ToString() + assembly[i + 1].ToString();

                        if (current == "FD")
                        {
                            current += assembly[i + 2].ToString() + assembly[i + 3].ToString();
                            i += 2;
                        }

                        if (opcode_list.ContainsKey(current))
                        {
                            Tasm_Opcode op = opcode_list[current];

                            String _params = "";
                            for (int j = op.size - 1; j > 0; j--)
                                _params += assembly[i + (j * 2)].ToString() + assembly[i + +(j * 2) + 1].ToString();

                            dissassembly += (_org + (i / 2)).ToString("X4") + ": ";

                            if (op.desc.IndexOf("@") > -1 && _params != "")
                            {
                                dissassembly += op.desc.Replace("@", "$" + _params);
                                dissassembly += "\r\n";
                            }
                            else if (op.desc.IndexOf("@") > -1)
                            {
                                dissassembly += op.desc;
                                dissassembly += " = $" + _params;
                                dissassembly += "\r\n";
                            }
                            else
                            {
                                dissassembly += op.desc;
                                dissassembly += "\r\n";
                            }

                            i += (op.size) * 2;
                        }
                        else
                        {
                            dissassembly += "; Unknown opcode: \"" + current + "\"";
                            dissassembly += "\r\n";

                            i += 2;
                        }

                    }

                    toolStripComboBox1.Enabled = false;
                    textBox1.Text = dissassembly;
                    textBox1.ReadOnly = true;
                    disassemblyToolStripMenuItem.Text = "Assembly";

                    undoToolStripMenuItem.Enabled = false;
                }
                else
                {
                    toolStripComboBox1.Enabled = true;
                    textBox1.Text = assembly;
                    textBox1.ReadOnly = false;
                    disassemblyToolStripMenuItem.Text = "Disassembly";


                    if (disassemblyToolStripMenuItem.Text == "Disassembly")
                    {
                        if (_filetype == EditorType.Binary)
                        {
                            undoToolStripMenuItem.Enabled = (textBox1.Text != originalfile);
                        }
                        else if (_filetype == EditorType.Text)
                        {
                            undoToolStripMenuItem.Enabled = (textBox1.Text != originaltextfile);
                        }
                    }
                }
            }
        }

        private void frmEditFile_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (disassemblyToolStripMenuItem.Text != "Disassembly")
            {
                disassemblyToolStripMenuItem_Click(null, null);
            }
        }
    }
}
