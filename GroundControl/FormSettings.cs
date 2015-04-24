using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GroundControl
{
    public partial class FormSettings : Form
    {
        private RocketProject m_project;

        public FormSettings(RocketProject project)
        {
            InitializeComponent();

            m_project = project;
            udRows.Value             = m_project.Rows;
            comboTimeFormatting.Text = m_project.TimeFormat;
            txtInputFile.Text        = m_project.AudioFile;
            udBPM.Value              = m_project.BPM         == 0 ? 120 : m_project.BPM;
            udRowsPerBeat.Value      = m_project.RowsPerBeat == 0 ?   8 : m_project.RowsPerBeat;
            udTimeOffset.Value       = m_project.AudioOffset;

            // Force UI update
            TimingValuesChanged(null, null);
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            m_project.Rows        = (int)udRows.Value;
            m_project.TimeFormat  = comboTimeFormatting.Text;
            m_project.AudioFile   = txtInputFile.Text;
            m_project.BPM         = (int)udBPM.Value;
            m_project.RowsPerBeat = (int)udRowsPerBeat.Value;
            m_project.AudioOffset = (int)udTimeOffset.Value;
        }

        private void btnOpenFile_Click(object sender, EventArgs e)
        {
            if (openFileDialog.ShowDialog(this) == System.Windows.Forms.DialogResult.OK)
                txtInputFile.Text = openFileDialog.FileName;
        }

        private void TimingValuesChanged(object sender, EventArgs e)
        {
            lblBeasPerSecond.Text = ((int)udBPM.Value * (int)udRowsPerBeat.Value / 60.0).ToString("0.0");
        }
    }
}
