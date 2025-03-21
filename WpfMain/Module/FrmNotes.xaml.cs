using System.Windows.Controls;
using Tools.App;

namespace WpfMain.Module
{
    /// <summary>
    /// FrmNotes.xaml 的交互逻辑
    /// </summary>
    public partial class FrmNotes : UserControl, ICustom
    {
        public FrmNotes()
        {
            InitializeComponent(); 
        }

        public void Closed()
        {
            richEditControl1.Dispose();
        }

        public void Refresh()
        { 
        }
    }
}
