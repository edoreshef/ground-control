#region Using directives

using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Win32;
using JWC;

#endregion

namespace MruToolStripMenuDemo
{
    partial class MruToolStripMenuDemo : Form
    {
		protected MruStripMenu mruMenu;
		static string mruRegKey = "SOFTWARE\\JWC\\MruMenuDemo";

		protected string curFileName;

		public MruToolStripMenuDemo()
        {
            InitializeComponent();

			RegistryKey regKey = Registry.CurrentUser.OpenSubKey(mruRegKey);
			if (regKey != null)
            {
                menuClearRegistryOnExit.Checked = (int)regKey.GetValue("delSubkey", 1) != 0;
                regKey.Close();
            }
			
			mruMenu = new MruStripMenuInline(menuFile, menuRecentFile, new MruStripMenu.ClickedHandler(OnMruFile), mruRegKey + "\\MRU", 16);

			IncFilename();
		}

		private void menuOpen_Click(object sender, EventArgs e)
        {
			OpenFileDialog openFileDialog = new OpenFileDialog();

			openFileDialog.InitialDirectory = "c:\\";
			openFileDialog.Filter = "txt files (*.txt)|*.txt|All files (*.*)|*.*";
			openFileDialog.FilterIndex = 2;
			openFileDialog.RestoreDirectory = true;

			if (openFileDialog.ShowDialog() == DialogResult.OK)
			{
				Stream inStream;
				if ((inStream = openFileDialog.OpenFile()) != null)
				{
					curFileName = openFileDialog.FileName;
					mruMenu.AddFile(curFileName);
				}
				inStream.Close();
			}
        }

        private void menuSave_Click(object sender, EventArgs e)
        {
            MessageBox.Show("File saved.", "File Save");

            // The default behavior of Windows Application is that the menu
            // position will not change on a simple save, but will on a SaveAs
        }

        private void menuSaveAs_Click(object sender, EventArgs e)
        {
			SaveFileDialog saveFileDialog = new SaveFileDialog();

			saveFileDialog.FileName = curFileName;
			saveFileDialog.Filter = "txt files (*.txt)|*.txt|All files (*.*)|*.*";
			saveFileDialog.FilterIndex = 2;
			saveFileDialog.RestoreDirectory = true;

			if (saveFileDialog.ShowDialog() == DialogResult.OK)
			{
				if (curFileName != saveFileDialog.FileName)
				{
					mruMenu.AddFile(saveFileDialog.FileName);
					curFileName = saveFileDialog.FileName;
				}
			}
        }

        private void menuExit_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void menuClearRegistryOnExit_Click(object sender, EventArgs e)
        {
            menuClearRegistryOnExit.Checked = !menuClearRegistryOnExit.Checked;
        }

		private void OnMruFile(int number, String filename)
		{
			DialogResult result = MessageBox.Show(
							"MRU open (" + number.ToString() + ") " + filename
							+ "\n\nClick \"Yes\" to mimic the behavior of the file opening successfully\n"
							+ "Click \"No\" to mimic the behavior of the file open failing"
							, "MruToolStripMenu Demo"
							, MessageBoxButtons.YesNo);

			// You may want to use exception handlers in your code

			if (result == DialogResult.Yes)
			{
				mruMenu.SetFirstFile(number);
			}
			else
			{
				MessageBox.Show("The file '" + filename + "'cannot be opened and will be removed from the Recent list(s)"
					, "MruToolStripMenu Demo"
					, MessageBoxButtons.OK
					, MessageBoxIcon.Error);
				mruMenu.RemoveFile(number);
			}
		}

		private int m_curFileNum = 0;

        private void IncFilename()
        {
            m_curFileNum++;
            int index = comboCurrentFile.Items.Add("File" + m_curFileNum.ToString());
            comboCurrentFile.SelectedIndex = index;
        }

        private void btnAdd_Click(object sender, EventArgs e)
        {
			mruMenu.AddFile((String)comboCurrentFile.SelectedItem);
			IncFilename();
        }

        private void btnRemove_Click(object sender, EventArgs e)
        {
			mruMenu.RemoveFile(Path.GetFullPath((String)comboCurrentFile.SelectedItem));
		}

        private void btnRemoveAll_Click(object sender, EventArgs e)
        {
			mruMenu.RemoveAll();
		}

        private void btnSave_Click(object sender, EventArgs e)
        {
			mruMenu.SaveToRegistry();
		}

        private void btnLoad_Click(object sender, EventArgs e)
        {
			mruMenu.LoadFromRegistry();
		}

        private void btnClearRegistry_Click(object sender, EventArgs e)
        {
            RegistryKey regKey = Registry.CurrentUser;
			regKey.DeleteSubKey(mruRegKey + "\\MRU", false);
		}

        bool m_mruIsInline = true;

        private void btnUseSubmenu_Click(object sender, EventArgs e)
        {
			if (m_mruIsInline)
			{
				String[] filenames = mruMenu.GetFiles();
				mruMenu.RemoveAll();

				mruMenu = new MruStripMenu(menuRecentFile, new MruStripMenu.ClickedHandler(OnMruFile), mruRegKey + "\\MRU", false);
				mruMenu.SetFiles(filenames);

				btnUseSubmenu.Text = "&Use Inline";
				m_mruIsInline = false;
			}
			else
			{
				String[] filenames = mruMenu.GetFiles();
				mruMenu.RemoveAll();

				mruMenu = new MruStripMenuInline(menuFile, menuRecentFile, new MruStripMenu.ClickedHandler(OnMruFile), mruRegKey + "\\MRU", 16);
				mruMenu.SetFiles(filenames);

				btnUseSubmenu.Text = "&Use Submenu";
				m_mruIsInline = true;
			}
		}

        protected override void OnClosing(CancelEventArgs e)
        {
			RegistryKey regKey = Registry.CurrentUser.CreateSubKey(mruRegKey);
			if (regKey != null)
            {
                regKey.SetValue("delSubkey", menuClearRegistryOnExit.Checked ? 1 : 0);
                regKey.Close();
            }

            if (menuClearRegistryOnExit.Checked)
            {
                regKey = Registry.CurrentUser;
                regKey.DeleteSubKey(mruRegKey + "\\MRU", false);
            }

            base.OnClosing(e);
        }

    }
}