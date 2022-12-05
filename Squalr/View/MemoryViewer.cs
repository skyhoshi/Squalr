namespace Squalr.View
{
    using System;
    using System.Windows;
    using System.Windows.Controls;

    public partial class MemoryViewer : UserControl
    {
        public MemoryViewer()
        {
            this.InitializeComponent();
        }

        private void HexEditorSizeChanged(Object sender, SizeChangedEventArgs e)
        {
            const Double addressColumnSize = 38.0;
            const Double seperationBufferSize = 30.0;
            const Double hexColumnSize = 168.0;
            const Double asciiColumnSize = 86.0;

            Double width = e.NewSize.Width - addressColumnSize - seperationBufferSize;
            Double sections = width / (hexColumnSize + asciiColumnSize);

            Int32 sectionsRounded = Math.Clamp((Int32)sections, 1, 8);

            this.hexEditor.BytePerLine = sectionsRounded * 8;
        }
    }
    //// End class
}
//// End namespace