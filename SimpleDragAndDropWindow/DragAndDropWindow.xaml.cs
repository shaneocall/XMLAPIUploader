using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml;
using System.Xml.Schema;
using Microsoft.Win32;

namespace SimpleDragAndDropWindow
{
  /// <summary>
  /// Interaction logic for MainWindow.xaml
  /// </summary>
  public partial class DragAndDropWindow : Window
  {
    public DragAndDropWindow()
    {
      InitializeComponent();

      SetWindowToDefaults();

      _apiProcessCodeDict = new Dictionary<string, string>();

      PopulateFakeAPIs();

      PopulateApiDropdown();
    }

    private string _currentLoadedFilePath;
    private string _initialTextDropWindowText = "Drop an XML file here..";

    private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
    {
      try
      {
        var openFileDialog = new OpenFileDialog
        {
          DefaultExt = "xml",
          Filter = "XML Files|*.xml",
          Multiselect = false
        };

        if (FileLoaded && !string.IsNullOrEmpty(Path.GetDirectoryName(_currentLoadedFilePath)))
          openFileDialog.InitialDirectory =
            Path.GetDirectoryName(_currentLoadedFilePath) ?? throw new InvalidOperationException();
        else
        {
          openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        }

        openFileDialog.ShowDialog();

        if (!string.IsNullOrEmpty(openFileDialog.FileName))
        {
          OpenFile(openFileDialog.FileName);
        }
      }
      catch (Exception ex)
      {
        MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
      }

    }

    private void UIElement_OnDrop(object sender, DragEventArgs e)
    {
      try
      {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

        if (!(e.Data.GetData(DataFormats.FileDrop) is string[] droppedFilePaths))
        {
          throw new Exception("Error opening file.");
        }

        if (droppedFilePaths.Length > 1)
        {
          throw new Exception("Only one file can be opened at a time.");
        }

        OpenFile(droppedFilePaths[0]);
      }

      catch (Exception ex)
      {
        MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
      }

    }


    private void OpenFile(string filePath)
    {
      if (!File.Exists(filePath) ||
          !string.Equals(Path.GetExtension(filePath), ".xml", StringComparison.OrdinalIgnoreCase))
      {
        if (!FileLoaded) SetWindowToDefaults();
        throw new Exception("Error opening file, ensure the file is a valid XML file.");
      }

      try
      {
        //Set the file path in the path textbox
        _currentLoadedFilePath = TextBox_FilePath.Text = filePath;

        //Setup the properties on the xml textbox

        TextBox_XMlDisplay.Text = File.ReadAllText(filePath);
        TextBox_XMlDisplay.HorizontalContentAlignment = 0;
        TextBox_XMlDisplay.VerticalContentAlignment = 0;
        TextBox_XMlDisplay.FontSize = 12;
        TextBox_XMlDisplay.FontFamily = new FontFamily("Consolas");
        TextBox_XMlDisplay.IsReadOnly = false;

        GenerateSchema(filePath);

        Btn_Validate.IsEnabled = true;
      }
      catch (Exception ex)
      {
        //Any Errors then reset all the properties, only at this point, we don't want to empty the
        //window of the old data yet
        SetWindowToDefaults();
        throw ex;
      }

    }

    private void SetWindowToDefaults()
    {
      _currentLoadedFilePath = TextBox_FilePath.Text = string.Empty;

      TextBox_XMlDisplay.Text = _initialTextDropWindowText;
      TextBox_XMlDisplay.IsReadOnly = true;
      TextBox_XMlDisplay.HorizontalContentAlignment = HorizontalAlignment.Center;
      TextBox_XMlDisplay.VerticalContentAlignment = VerticalAlignment.Center;
      TextBox_XMlDisplay.FontSize = 20;
      TextBox_XMlDisplay.FontFamily = new FontFamily("Arial");
      Btn_Validate.IsEnabled = false;

    }


    private void TextBox_XMlDisplay_OnDragEnter(object sender, DragEventArgs e)
    {
      TextBox_XMlDisplay.Opacity = 0.4;
    }


    private void TextBox_XMlDisplay_OnDragLeave(object sender, DragEventArgs e)
    {
      TextBox_XMlDisplay.Opacity = 1;
    }

    private bool FileLoaded => !string.IsNullOrEmpty(_currentLoadedFilePath);

    private object _schemaSet;

    private void GenerateSchema(string filePath)
    {
      try
      {
        using (var xmlReader = XmlReader.Create(filePath))
        {
          XmlSchemaInference schema = new XmlSchemaInference();
          _schemaSet = schema.InferSchema(xmlReader);
        }
      }
      catch (Exception ex)
      {
        MessageBox.Show("An XML schema cannot be infered.\n\n"+ex.Message, "Invalid XML", MessageBoxButton.OK, MessageBoxImage.Error);
        _schemaSet = ex;
      }
    }

    private void Btn_ValidateSchema(object sender, RoutedEventArgs e)
    {
      try
      {

        XmlReaderSettings settings = new XmlReaderSettings { ValidationType = ValidationType.Schema };
      settings.ValidationFlags |= XmlSchemaValidationFlags.ProcessInlineSchema;
      settings.ValidationFlags |= XmlSchemaValidationFlags.ProcessSchemaLocation;
      settings.ValidationFlags |= XmlSchemaValidationFlags.ReportValidationWarnings;

        bool shouldValidate = true;

      switch (_schemaSet)
      {
        case XmlSchemaSet schema:
          settings.Schemas.Add(schema);
          break;
        case Exception _:
          shouldValidate = false;
          break;
      }
        using (TextReader stReader = new StringReader(TextBox_XMlDisplay.Text))
        {
          var reader = XmlReader.Create(stReader, settings);

          var document = new XmlDocument();
          document.Load(reader);

          // the following call to Validate succeeds.
        if(shouldValidate) document.Validate(ValidationCallBack);
        }
      }
      catch (Exception ex)
      {
        MessageBox.Show(ex.Message, "Validation Failed", MessageBoxButton.OK, MessageBoxImage.Error);
        return;
      }

      MessageBox.Show("XML is valid", "Validation Succeeded", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ValidationCallBack(object sender, ValidationEventArgs args)
    {
      if (args.Severity == XmlSeverityType.Warning)
      MessageBox.Show("Matching schema not found.No validation occurred." + args.Message, "Validation Failed", MessageBoxButton.OK, MessageBoxImage.Error);
      else
        MessageBox.Show(args.Message + args.Message, "Validation Failed", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private IDictionary<string, string> _apiProcessCodeDict;

    private void PopulateApiDropdown()
    {
      foreach (var api in _apiProcessCodeDict)
      {
        CbmBox_API.Items.Add(api.Key);
      }

      if (CbmBox_API.Items.Count > 0) CbmBox_API.SelectedItem = CbmBox_API.Items[0];
    }

    private  void PopulateFakeAPIs()
    {
      _apiProcessCodeDict =  new Dictionary<string, string>
      {
        {"API 1", "Process Code 1"},
        {"API 2", "Process Code 2"},
        {"API 3", "Process Code 3"},
        {"API 4", "Process Code 4"},
        {"API 5", "Process Code 5"}
      };

    }

    private void Btn_Upload_OnClick(object sender, RoutedEventArgs e)
    {
      try
      {
        if (!FileLoaded) return;

        var processCode = string.Empty;

        if (string.IsNullOrEmpty(CbmBox_API.SelectedItem as string) ||
            !_apiProcessCodeDict.TryGetValue(CbmBox_API.SelectedItem.ToString(), out processCode))
          throw new Exception("API not selected");


        if (MessageBox.Show(
              string.Format("Upload file to API: {0}, Process Code: {1}?", CbmBox_API.SelectedItem, processCode),
              "Upload", MessageBoxButton.OKCancel, MessageBoxImage.Information) == MessageBoxResult.Cancel) return;

        MessageBox.Show("Uploaded!", "Upload", MessageBoxButton.OK, MessageBoxImage.Information);

      }
      catch (Exception ex)
      {
        MessageBox.Show(ex.Message, "Error Uploading", MessageBoxButton.OK, MessageBoxImage.Error);

      }
    }


  }
}
