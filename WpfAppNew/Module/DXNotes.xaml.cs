using System;
using System.Windows; 


namespace WpfAppNew.Module
{
    /// <summary>
    /// Interaction logic for DXNotes.xaml
    /// </summary>
    public partial class DXNotes : Window
    {
        public DXNotes()
        {
            InitializeComponent();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            //this.Resources.Clear();
            EditControl.Dispose();
            //GC.Collect();
        }
    }
}
