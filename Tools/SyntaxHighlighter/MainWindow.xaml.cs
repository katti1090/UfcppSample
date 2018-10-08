using System;
using System.Windows;
using System.Windows.Input;

namespace SyntaxHighlighter
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void CommandBinding_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            ParseClipboard();
        }

        static readonly string[] tagNames =
        {
            "source",
            "xsource",
        };

        private void ParseClipboard()
        {
            var data = Clipboard.GetDataObject();

            Mode mode = comboType.SelectedIndex == 0 ? Mode.Csharp : Mode.Xml;

            var converted = Parse(data, mode);

            if (converted != null)
            {
                var tag = tagNames[comboType.SelectedIndex];

                var source = String.Format("<pre class=\"{0}\" title=\"\">{1}<code>{2}{3}</code></pre>",
                        tag,
                        Environment.NewLine,
                        converted,
                        Environment.NewLine);

                this.block.Text = source;
                Clipboard.SetDataObject(source);
            }
        }

        private string? Parse(IDataObject data, Mode mode)
        {
            var html = data.GetData(DataFormats.Html) as string;

            if (html != null)
            {
                var p = new HtmlParser(mode);
                return p.Parse(html);
            }

            var rtf = data.GetData(DataFormats.Rtf) as string;

            if (rtf != null)
            {
                var p = new RtfParser(mode);
                return p.Parse(rtf);
            }

            return null;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            ParseClipboard();
        }
    }
}
