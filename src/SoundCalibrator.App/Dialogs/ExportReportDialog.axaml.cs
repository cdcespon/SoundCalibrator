using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace SoundCalibrator.App.Dialogs;

public enum ExportReportFormat
{
    Pdf,
    Html,
    Text
}

public partial class ExportReportDialog : Window
{
    private static readonly IBrush SelectedBorderBrush = new SolidColorBrush(Color.Parse("#00F0FF"));
    private static readonly IBrush DefaultBorderBrush = new SolidColorBrush(Color.Parse("#1E2638"));

    public ExportReportFormat? ResultFormat { get; private set; }

    public ExportReportDialog()
    {
        InitializeComponent();

        PdfCard.PointerPressed += (s, e) => { PdfRadio.IsChecked = true; UpdateBorders(); };
        HtmlCard.PointerPressed += (s, e) => { HtmlRadio.IsChecked = true; UpdateBorders(); };
        TextCard.PointerPressed += (s, e) => { TextRadio.IsChecked = true; UpdateBorders(); };

        PdfRadio.IsCheckedChanged += (s, e) => UpdateBorders();
        HtmlRadio.IsCheckedChanged += (s, e) => UpdateBorders();
        TextRadio.IsCheckedChanged += (s, e) => UpdateBorders();

        CancelBtn.Click += (s, e) => Close(null);
        CloseIconBtn.Click += (s, e) => Close(null);

        ExportBtn.Click += (s, e) =>
        {
            if (PdfRadio.IsChecked == true) ResultFormat = ExportReportFormat.Pdf;
            else if (HtmlRadio.IsChecked == true) ResultFormat = ExportReportFormat.Html;
            else ResultFormat = ExportReportFormat.Text;

            Close(ResultFormat);
        };
    }

    private void UpdateBorders()
    {
        PdfCard.BorderBrush = PdfRadio.IsChecked == true ? SelectedBorderBrush : DefaultBorderBrush;
        HtmlCard.BorderBrush = HtmlRadio.IsChecked == true ? SelectedBorderBrush : DefaultBorderBrush;
        TextCard.BorderBrush = TextRadio.IsChecked == true ? SelectedBorderBrush : DefaultBorderBrush;
    }
}
