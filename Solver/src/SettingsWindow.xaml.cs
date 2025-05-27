using Microsoft.Win32;
using Solver.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;

namespace Solver;

public partial class SettingsWindow : Window
{
    public List<ProgressIntervalOption> IntervalOptions { get; set; }

    public SettingsWindow()
    {
        InitializeComponent();
        InitializeIntervalOptions();
        LoadSettingsToUI();
    }

    private void InitializeIntervalOptions()
    {
        IntervalOptions = new List<ProgressIntervalOption>
        {
            new ProgressIntervalOption("Real-time (100ms)", 100),
            new ProgressIntervalOption("Very Fast (250ms)", 250),
            new ProgressIntervalOption("Fast (500ms)", 500),
            new ProgressIntervalOption("Normal (1000ms)", 1000),
            new ProgressIntervalOption("Slow (2000ms)", 2000),
            new ProgressIntervalOption("Very Slow (5000ms)", 5000)
        };
        ProgressIntervalComboBox.ItemsSource = IntervalOptions;
    }

    private void LoadSettingsToUI()
    {
        WordListPathTextBox.Text = App.ConfigService.Settings.WordListPath;
        ProgressIntervalComboBox.SelectedValue = App.ConfigService.Settings.ProgressUpdateIntervalMilliseconds;

        if (ProgressIntervalComboBox.SelectedItem == null)
        {
            ProgressIntervalComboBox.SelectedValue = 1000;
        }

        // Load DoubleClickBehavior setting
        DoubleClickBehaviorComboBox.SelectedItem = App.ConfigService.Settings.DoubleClickBehavior;
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        OpenFileDialog openFileDialog = new OpenFileDialog
        {
            Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
            Title = "Select Word List File"
        };

        string currentPath = WordListPathTextBox.Text;
        if (!string.IsNullOrEmpty(currentPath))
        {
            try
            {
                if (File.Exists(currentPath))
                {
                    openFileDialog.InitialDirectory = Path.GetDirectoryName(currentPath);
                    openFileDialog.FileName = Path.GetFileName(currentPath);
                }
                else if (Directory.Exists(Path.GetDirectoryName(currentPath)))
                {
                    openFileDialog.InitialDirectory = Path.GetDirectoryName(currentPath);
                }
                else
                {
                    openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                }
            }
            catch (ArgumentException)
            {
                openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            }
        }
        else
        {
            openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        }

        if (openFileDialog.ShowDialog() == true)
        {
            WordListPathTextBox.Text = openFileDialog.FileName;
        }
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        string wordListPath = WordListPathTextBox.Text;
        if (string.IsNullOrWhiteSpace(wordListPath))
        {
            MessageBox.Show(this, "Word list path cannot be empty.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        try
        {
            string fullPath = Path.GetFullPath(wordListPath);
            if (!File.Exists(fullPath))
            {
                MessageBox.Show(this, $"Word list file not found: {fullPath}", "File Not Found", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            App.ConfigService.Settings.WordListPath = fullPath;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Invalid word list path: {ex.Message}", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        if (ProgressIntervalComboBox.SelectedValue == null)
        {
            MessageBox.Show(this, "Please select a progress update speed.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        App.ConfigService.Settings.ProgressUpdateIntervalMilliseconds = (int)ProgressIntervalComboBox.SelectedValue;

        // Save DoubleClickBehavior setting
        if (DoubleClickBehaviorComboBox.SelectedItem != null)
        {
            App.ConfigService.Settings.DoubleClickBehavior = (SolutionDoubleClickAction)DoubleClickBehaviorComboBox.SelectedItem;
        }
        else // Should not happen if ComboBox is populated and has a default
        {
            App.ConfigService.Settings.DoubleClickBehavior = SolutionDoubleClickAction.AddToExcluded; // Fallback
        }

        App.ConfigService.SaveSettings();

        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}